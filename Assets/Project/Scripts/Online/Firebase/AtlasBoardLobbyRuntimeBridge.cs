using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;


public sealed class AtlasBoardLobbyRuntimeBridge : MonoBehaviour
{
    private const string ProjectId = "atlasboard-usa";
    // Firebase Web API keys are public client configuration. The local Guest
    // uses this value only against the loopback Auth Emulator so it does not
    // need to initialize the native Firebase desktop SDK during QA startup.
    private const string FirebaseWebApiKey =
        "AIzaSyAOnxprB0-59IZ2D5uvWM4xBtixzA_cRtU";
    private const string Region = "europe-west1";
    private const string DefaultEmulatorHost = "127.0.0.1";
    private const int AuthEmulatorPort = 9099;
    private const int FunctionsEmulatorPort = 5001;


    private const string LocalEmulatorCommandLineSwitch =
        "-atlasLocalEmulators";


    private const string DevelopmentNameCommandLineSwitch =
        "-atlasDevName";


    private const string EmulatorHostCommandLineSwitch =
        "-atlasEmulatorHost";


#if UNITY_EDITOR
    // Optional editor-wide QA override: -1 follows the scene's serialized
    // value; 0 = live Spark; 1 = local emulator. Never compiled in players.
    internal const string EditorBackendPreferenceKey =
        "AtlasBoard.RTDB.EditorBackend.v1";
    [Header("Development")]
    [SerializeField]
    private bool useLocalEmulatorsInEditor = true;
#endif


    [SerializeField, Min(0.25f)]
    private float snapshotPollSeconds = 1f;


    [SerializeField, Min(2f)]
    private float hostHeartbeatSeconds = 5f;


    [SerializeField]
    private string developmentDisplayName = "Unity Editor Player";


    // The Windows QA Guest must retain the SAME Firebase anonymous principal
    // between app restarts. The refresh token is protected by current-user
    // Windows DPAPI; never log it or put it in PlayerPrefs / RTDB.
    private const string LiveGuestCredentialsFile = "live_spark_guest_v1.dpapi";
    private const string GuestNameClaimsUrl =
        "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app/guestNameClaims/";
    private string liveGuestRefreshToken = string.Empty;
    private string idToken = string.Empty;
    private string currentAccountId = string.Empty;
    private string currentLobbyId = string.Empty;
    private string currentRoomCode = string.Empty;
    private string joinIdempotencyKey = string.Empty;
    private AtlasLobbySnapshot currentSnapshot;
    private bool initialized;
    private bool initializing;
    private float nextIdentityRefreshAt;
    private bool pollInFlight;
    private float nextPollAt;
    private bool hostHeartbeatInFlight;
    private float nextHostHeartbeatAt;
    private bool pawnCosmeticSyncInFlight;


    public event Action<AtlasLobbySnapshot> SnapshotChanged;
    public event Action<string> LobbyAccessLost;


    public string CurrentAccountId => currentAccountId;
    public string CurrentLobbyId => currentLobbyId;


    internal string AuthTokenForOnlineSubsystems =>
        idToken;


    public async Task<bool> EnsureOnlineIdentityAsync()
    {
        AtlasLobbyOperationResult result =
            await EnsureIdentityAsync();


        return result.Success;
    }


    public async Task<bool> RebindIdentityFromFirebaseAuthAsync()
    {
        // The explicit loopback Development Guest is HTTP-only. Never wake
        // the native Firebase Auth SDK from this compatibility path.
        if (AtlasBoardAppCheckTokenSource.IsLocalEmulatorGuestSession ||
            AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
        {
            return false;
        }


        try
        {
            DependencyStatus dependencyStatus =
                await FirebaseApp.CheckAndFixDependenciesAsync();


            if (dependencyStatus != DependencyStatus.Available)
            {
                return false;
            }


            FirebaseAuth auth = FirebaseAuth.DefaultInstance;
            FirebaseUser user = auth != null
                ? auth.CurrentUser
                : null;


            if (user == null ||
                string.IsNullOrWhiteSpace(user.UserId))
            {
                return false;
            }


            string token = await user.TokenAsync(false);


            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }


            idToken = token;
            currentAccountId = user.UserId;
            initialized = true;


            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    public string CurrentRoomCode => currentRoomCode;
    public AtlasLobbySnapshot CurrentSnapshot => currentSnapshot;


    public bool HasLobby =>
        !string.IsNullOrWhiteSpace(currentLobbyId) &&
        currentSnapshot != null;


    public bool UsingLocalEmulators
    {
        get
        {
#if UNITY_EDITOR
            // A freshly instantiated lobby bridge must NOT silently revert to
            // the emulator after QA explicitly selected live Spark. This
            // EditorPrefs value cannot affect Android/Steam Release builds.
            int editorChoice = UnityEditor.EditorPrefs.GetInt(
                EditorBackendPreferenceKey, -1);
            return editorChoice < 0
                ? useLocalEmulatorsInEditor
                : editorChoice == 1;
#elif DEVELOPMENT_BUILD
            // Standalone development clients may use the emulator suite only
            // when explicitly launched with the dedicated development switch.
            // Release builds cannot enter this path.
            return HasCommandLineSwitch(
                LocalEmulatorCommandLineSwitch);
#else
            return false;
#endif
        }
    }


    public string EmulatorHostForOnlineSubsystems =>
        ResolveEmulatorHost();


    private bool ownedRoomSweepInFlight;
    private string lastOwnedRoomSweepUid = string.Empty;

    private async void Start()
    {
        // Background loading-screen opportunity, but never create a Firebase
        // account just for cleanup. Only reuse an already signed-in identity.
        if (UsingLocalEmulators) return;
        for (int i = 0; i < 40; ++i)
        {
            if (this == null || !Application.isPlaying) return;
            AtlasBoardAccountService account = AtlasBoardAccountService.Instance;
            if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession ||
                (account != null && account.IsSignedIn))
            {
                AtlasLobbyOperationResult auth = await EnsureIdentityAsync();
                if (auth.Success) await SweepOwnedRoomsOnceAsync(0);
                return;
            }
            await Task.Delay(500);
        }
    }

    private async Task SweepOwnedRoomsOnceAsync(int maxDeletes)
    {
        if (UsingLocalEmulators || ownedRoomSweepInFlight ||
            string.IsNullOrWhiteSpace(currentAccountId) ||
            string.IsNullOrWhiteSpace(idToken)) return;
        if (maxDeletes == 0 && lastOwnedRoomSweepUid == currentAccountId) return;
        ownedRoomSweepInFlight = true;
        try
        {
            var scan = await AtlasBoardRtdbCasualLobbyTransport.
                CleanupOwnedDormantRoomsAsync(currentAccountId, idToken,
                    currentLobbyId, maxDeletes);
            if (scan.scanned && maxDeletes == 0)
                lastOwnedRoomSweepUid = currentAccountId;
            if (!scan.scanned)
                Debug.LogWarning("ATB_RTDB_ORPHAN_CLEANUP scanned=false " +
                    "cause=owner_query_denied_or_unavailable");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_RTDB_ORPHAN_CLEANUP failed=true category=" +
                ex.GetType().Name);
        }
        finally { ownedRoomSweepInFlight = false; }
    }

    private async void Update()
    {
        if (!HasLobby)
        {
            return;
        }


        if (!hostHeartbeatInFlight &&
            IsCurrentLocalHost() &&
            Time.unscaledTime >= nextHostHeartbeatAt)
        {
            nextHostHeartbeatAt =
                Time.unscaledTime +
                Mathf.Max(UsingLocalEmulators ? 2f : 60f, hostHeartbeatSeconds);


            _ = SendHostHeartbeatAsync();
        }


        if (pollInFlight ||
            Time.unscaledTime < nextPollAt)
        {
            return;
        }


        nextPollAt =
            Time.unscaledTime +
            Mathf.Max(UsingLocalEmulators ? 0.25f : 3f, snapshotPollSeconds);


        pollInFlight = true;


        try
        {
            AtlasLobbyOperationResult result =
                await GetSnapshotAsync();


            if (result.Success &&
                result.Snapshot != null)
            {
                // Preserve the six-digit invite code even after a room
                // snapshot reload. A poll must never erase a known code.
                if (!string.IsNullOrWhiteSpace(result.RoomCode) &&
                    string.Equals(result.Snapshot.LobbyId, currentLobbyId,
                        StringComparison.Ordinal))
                {
                    currentRoomCode = result.RoomCode;
                }
                // GetSnapshotAsync already accepts and de-duplicates the
                // authoritative snapshot. Do not emit the same event twice.
            }
            else if (string.Equals(
                         result.ErrorLocalizationKey,
                         "lobby.error.kicked",
                         StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(
                         result.ErrorLocalizationKey,
                         "lobby.error.closed",
                         StringComparison.OrdinalIgnoreCase))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("ATB_RTDB_ROOM_TERMINATION received=true reason=" +
                    result.ErrorLocalizationKey + " local_host=" +
                    IsCurrentLocalHost());
#endif
                // Let existing UI/active-match subscribers detach before
                // discarding the authoritative lobby identity.
                LobbyAccessLost?.Invoke(result.ErrorLocalizationKey);
                ClearLocalLobbyState();
            }
        }
        finally
        {
            pollInFlight = false;
        }
    }


    private bool IsCurrentLocalHost()
    {
        return currentSnapshot != null &&
               !string.IsNullOrWhiteSpace(
                   currentAccountId) &&
               string.Equals(
                   currentSnapshot.HostAccountId,
                   currentAccountId,
                   StringComparison.Ordinal);
    }


    private async Task SendHostHeartbeatAsync()
    {
        if (hostHeartbeatInFlight ||
            !HasLobby ||
            !IsCurrentLocalHost())
        {
            return;
        }


        hostHeartbeatInFlight = true;


        try
        {
            await CallLobbyFunctionAsync(
                "lobbyHostHeartbeat",
                new LobbyHeartbeatRequest
                {
                    lobbyId = currentLobbyId
                });
        }
        finally
        {
            hostHeartbeatInFlight = false;
        }
    }


    public async Task<AtlasLobbyOperationResult> CreatePublicRoomAsync(
        AtlasBoardLobbySelection selection)
    {
        AtlasLobbyOperationResult init =
            await EnsureIdentityAsync();


        if (!init.Success)
        {
            return init;
        }


        LobbyCreateRequest request =
            BuildCreateRequest(selection);


        AtlasLobbyOperationResult result =
            await CallLobbyFunctionAsync(
                "lobbyCreatePublicRoom",
                request);


        if (result.Success &&
            result.Snapshot != null)
        {
            currentLobbyId =
                result.Snapshot.LobbyId;


            currentRoomCode =
                result.RoomCode ?? string.Empty;


            joinIdempotencyKey = string.Empty;
            AcceptSnapshot(result.Snapshot);
        }


        return result;
    }


    public async Task<AtlasPublicLobbyListResult>
        ListPublicRoomsAsync(
            int limit = 6)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float listStartedAt = Time.realtimeSinceStartup;
#endif
        AtlasLobbyOperationResult init =
            await EnsureIdentityAsync();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float identityMilliseconds =
            (Time.realtimeSinceStartup - listStartedAt) * 1000f;
#endif


        if (!init.Success)
        {
            return AtlasPublicLobbyListResult.Fail(
                init.ErrorLocalizationKey,
                init.TechnicalMessage);
        }


        if (!UsingLocalEmulators)
            return await AtlasBoardRtdbCasualLobbyTransport.PublicRoomsAsync(
                idToken, Mathf.Clamp(limit, 1, 50));


        PublicLobbyListRequest request =
            new PublicLobbyListRequest
            {
                gameVersion = CurrentGameVersion(),
                protocolVersion =
                    AtlasOnlineDefaults.ProtocolVersion,
                rulesVersion =
                    AtlasOnlineDefaults.RulesVersion,
                contentVersion = "1",
                regionId = "eur3",
                limit = Mathf.Clamp(limit, 1, 20)
            };


        string functionName =
            "lobbyListPublicRooms";


        string url =
            UsingLocalEmulators
                ? $"http://{EmulatorHostForOnlineSubsystems}:{FunctionsEmulatorPort}/" +
                  $"{ProjectId}/{Region}/{functionName}"
                : $"https://{Region}-{ProjectId}.cloudfunctions.net/{functionName}";


        string dataJson =
            JsonUtility.ToJson(request);


        string callableJson =
            "{\"data\":" + dataJson + "}";


        using UnityWebRequest webRequest =
            new UnityWebRequest(
                url,
                UnityWebRequest.kHttpVerbPOST);


        webRequest.uploadHandler =
            new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(callableJson));


        webRequest.downloadHandler =
            new DownloadHandlerBuffer();


        webRequest.SetRequestHeader(
            "Content-Type",
            "application/json");


        webRequest.SetRequestHeader(
            "Authorization",
            $"Bearer {idToken}");


        webRequest.timeout = 20;


        await AtlasBoardAppCheckRequestSecurity.ApplyAsync(
            webRequest);
        await SendRequestAsync(webRequest);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            $"AtlasBoard lobby list timing: identity={identityMilliseconds:F0}ms, " +
            $"total={(Time.realtimeSinceStartup - listStartedAt) * 1000f:F0}ms, " +
            $"HTTP={webRequest.responseCode}.", this);
#endif


        string body =
            webRequest.downloadHandler != null
                ? webRequest.downloadHandler.text
                : string.Empty;


        if (webRequest.result !=
            UnityWebRequest.Result.Success)
        {
            CallableErrorEnvelope error =
                SafeFromJson<CallableErrorEnvelope>(body);


            string errorKey =
                error != null &&
                error.error != null &&
                error.error.details != null &&
                !string.IsNullOrWhiteSpace(
                    error.error.details.errorKey)
                    ? error.error.details.errorKey
                    : "lobby.error.unknown";


            string technical =
                error != null &&
                error.error != null &&
                !string.IsNullOrWhiteSpace(
                    error.error.message)
                    ? error.error.message
                    : $"HTTP {webRequest.responseCode}: {webRequest.error}";


            return AtlasPublicLobbyListResult.Fail(
                errorKey,
                technical);
        }


        CallablePublicLobbyListEnvelope envelope =
            SafeFromJson<CallablePublicLobbyListEnvelope>(
                body);


        if (envelope == null ||
            envelope.result == null ||
            !envelope.result.ok)
        {
            return AtlasPublicLobbyListResult.Fail(
                "lobby.error.service_unavailable",
                "Callable returned no valid public-lobby list. Body=" +
                body);
        }


        List<AtlasPublicLobbyCard> cards =
            new List<AtlasPublicLobbyCard>();


        if (envelope.result.rooms != null)
        {
            foreach (PublicLobbyWireCard wire
                     in envelope.result.rooms)
            {
                if (wire == null ||
                    string.IsNullOrWhiteSpace(
                        wire.lobbyId))
                {
                    continue;
                }


                cards.Add(
                    new AtlasPublicLobbyCard
                    {
                        LobbyId =
                            wire.lobbyId ?? string.Empty,
                        HostDisplayName =
                            wire.hostDisplayName ?? string.Empty,
                        MapId =
                            wire.mapId ?? string.Empty,
                        ThemeId =
                            wire.themeId ?? string.Empty,
                        RoundLimit =
                            wire.roundLimit,
                        MaxPlayers =
                            wire.maxPlayers,
                        OccupiedPlayers =
                            wire.occupiedPlayers,
                        OpenOnlineSeatCount =
                            wire.openOnlineSeatCount,
                        RegionId =
                            wire.regionId ?? string.Empty,
                        GameVersion =
                            wire.gameVersion ?? string.Empty,
                        ProtocolVersion =
                            wire.protocolVersion,
                        RulesVersion =
                            wire.rulesVersion,
                        ContentVersion =
                            wire.contentVersion ?? string.Empty,
                        SettingsRevision =
                            wire.settingsRevision,
                        HasPassword =
                            wire.hasPassword,
                        CreatedAtEpochMs =
                            wire.createdAtEpochMs
                    });
            }
        }


        return AtlasPublicLobbyListResult.Ok(
            cards);
    }


    public Task<AtlasLobbyOperationResult> CloseOwnedWaitingRoomsOnConsentAsync()
    {
        // The transport operation verifies owner UID again for every room.
        // Never start cleanup from a passive UI refresh or a failed Create.
        return CallLobbyFunctionAsync("lobbyCloseOwnedWaitingRooms",
            new LobbySnapshotRequest { lobbyId = currentLobbyId });
    }

    public async Task<AtlasLobbyOperationResult> CreatePrivateRoomAsync(
        AtlasBoardLobbySelection selection)
    {
        AtlasLobbyOperationResult init =
            await EnsureIdentityAsync();


        if (!init.Success)
        {
            return init;
        }


        LobbyCreateRequest request =
            BuildCreateRequest(selection);


        AtlasLobbyOperationResult result =
            await CallLobbyFunctionAsync(
                "lobbyCreatePrivateRoom",
                request);


        if (result.Success &&
            result.Snapshot != null)
        {
            currentLobbyId =
                result.Snapshot.LobbyId;


            currentRoomCode =
                result.RoomCode ?? string.Empty;


            joinIdempotencyKey = string.Empty;
            AcceptSnapshot(result.Snapshot);
        }


        return result;
    }


    public async Task<AtlasLobbyOperationResult> JoinByCodeAsync(
        string roomCode,
        string password = "")
    {
        AtlasLobbyOperationResult init =
            await EnsureIdentityAsync();


        if (!init.Success)
        {
            return init;
        }


        if (string.IsNullOrWhiteSpace(joinIdempotencyKey))
        {
            joinIdempotencyKey =
                $"unity_join_{Guid.NewGuid():N}";
        }


        LobbyJoinRequest request =
            new LobbyJoinRequest
            {
                roomCode = roomCode,
                password = password ?? string.Empty,
                idempotencyKey = joinIdempotencyKey,
                gameVersion = CurrentGameVersion(),
                protocolVersion = AtlasOnlineDefaults.ProtocolVersion,
                rulesVersion = AtlasOnlineDefaults.RulesVersion,
                contentVersion = "1",
                regionId = "eur3"
            };


        AtlasLobbyOperationResult result =
            await CallLobbyFunctionAsync(
                "lobbyJoinByCode",
                request);


        if (result.Success &&
            result.Snapshot != null)
        {
            currentLobbyId =
                result.Snapshot.LobbyId;


            currentRoomCode =
                roomCode ?? string.Empty;


            AcceptSnapshot(result.Snapshot);
        }


        return result;
    }


    public async Task<AtlasLobbyOperationResult> JoinPublicRoomAsync(
        string lobbyId,
        string password = "")
    {
        AtlasLobbyOperationResult init =
            await EnsureIdentityAsync();
        if (!init.Success) return init;


        if (string.IsNullOrWhiteSpace(joinIdempotencyKey))
        {
            joinIdempotencyKey = $"unity_public_join_{Guid.NewGuid():N}";
        }


        LobbyPublicJoinRequest request =
            new LobbyPublicJoinRequest
            {
                lobbyId = lobbyId,
                password = password ?? string.Empty,
                idempotencyKey = joinIdempotencyKey,
                gameVersion = CurrentGameVersion(),
                protocolVersion = AtlasOnlineDefaults.ProtocolVersion,
                rulesVersion = AtlasOnlineDefaults.RulesVersion,
                contentVersion = "1",
                regionId = "eur3"
            };


        AtlasLobbyOperationResult result =
            await CallLobbyFunctionAsync(
                "lobbyJoinPublicRoom",
                request);


        if (result.Success && result.Snapshot != null)
        {
            currentLobbyId = result.Snapshot.LobbyId;
            // The backend returns the same six-digit invite/reconnect code to
            // the authenticated member. The raw code is not stored in the
            // public discovery index.
            currentRoomCode = result.RoomCode ?? string.Empty;
            AcceptSnapshot(result.Snapshot);
        }


        return result;
    }


    public async Task<AtlasLobbyOperationResult> UpdateLobbyPasswordAsync(
        string password)
    {
        if (!HasLobby || currentSnapshot == null)
        {
            return AtlasLobbyOperationResult.Fail(
                "lobby.error.not_joinable",
                "No active lobby is available.");
        }


        LobbyPasswordRequest request =
            new LobbyPasswordRequest
            {
                lobbyId = currentLobbyId,
                expectedSettingsRevision = currentSnapshot.SettingsRevision,
                password = password ?? string.Empty
            };


        AtlasLobbyOperationResult result =
            await CallLobbyFunctionAsync(
                "lobbyUpdatePassword",
                request);


        if (result.Success && result.Snapshot != null)
        {
            AcceptSnapshot(result.Snapshot);
        }


        return result;
    }


    public async Task<AtlasLobbyOperationResult> CloseLobbyAsync()
    {
        if (!HasLobby)
        {
            return AtlasLobbyOperationResult.Ok(null);
        }


        LobbyCloseRequest request =
            new LobbyCloseRequest
            {
                lobbyId = currentLobbyId
            };


        AtlasLobbyOperationResult result =
            await CallLobbyFunctionAsync(
                "lobbyCloseRoom",
                request);


        if (result.Success)
        {
            ClearLocalLobbyState();
        }


        return result;
    }


    public async Task<AtlasLobbyOperationResult> SyncHostConfigurationAsync(
        AtlasBoardLobbySelection selection,
        string[] seatPolicies)
    {
        if (!HasLobby ||
            currentSnapshot == null)
        {
            return AtlasLobbyOperationResult.Fail(
                "lobby.error.not_joinable",
                "No active lobby snapshot is available.");
        }


        AtlasLobbySnapshot snapshot =
            currentSnapshot;


        bool rulesChanged =
            snapshot.Settings == null ||
            !string.Equals(
                snapshot.Settings.MapId,
                selection.MapId,
                StringComparison.Ordinal) ||
            !string.Equals(
                snapshot.Settings.ThemeId,
                selection.ThemeId,
                StringComparison.Ordinal) ||
            snapshot.Settings.RoundLimit != selection.RoundLimit ||
            snapshot.Settings.BalancedDevelopment != selection.BalancedDevelopment ||
            snapshot.Settings.DoublesEnabled != selection.DoublesEnabled ||
            snapshot.Settings.TripleDoublePenaltyEnabled != selection.TripleDoublePenaltyEnabled;


        AtlasLobbyOperationResult latest =
            AtlasLobbyOperationResult.Ok(snapshot);


        if (rulesChanged)
        {
            LobbyUpdateSettingsRequest settingsRequest =
                new LobbyUpdateSettingsRequest
                {
                    lobbyId = snapshot.LobbyId,
                    expectedSettingsRevision = snapshot.SettingsRevision,
                    mapId = selection.MapId,
                    themeId = selection.ThemeId,
                    roundLimit = selection.RoundLimit,
                    balancedDevelopment = selection.BalancedDevelopment,
                    doublesEnabled = selection.DoublesEnabled,
                    tripleDoublePenaltyEnabled = selection.TripleDoublePenaltyEnabled
                };


            latest =
                await CallLobbyFunctionAsync(
                    "lobbyUpdateSettings",
                    settingsRequest);


            if (!latest.Success ||
                latest.Snapshot == null)
            {
                return latest;
            }


            AcceptSnapshot(latest.Snapshot);
            snapshot = latest.Snapshot;
        }


        bool seatPolicyChanged =
            snapshot.Settings == null ||
            snapshot.Settings.MaxPlayers != selection.PlayerCount ||
            !SeatPoliciesMatchSnapshot(
                seatPolicies,
                snapshot);


        if (seatPolicyChanged)
        {
            LobbyConfigureSeatsRequest seatRequest =
                new LobbyConfigureSeatsRequest
                {
                    lobbyId = snapshot.LobbyId,
                    expectedSettingsRevision = snapshot.SettingsRevision,
                    maxPlayers = selection.PlayerCount,
                    seatPolicies = seatPolicies
                };


            latest =
                await CallLobbyFunctionAsync(
                    "lobbyConfigureSeats",
                    seatRequest);


            if (!latest.Success ||
                latest.Snapshot == null)
            {
                return latest;
            }


            AcceptSnapshot(latest.Snapshot);
        }


        return latest;
    }


    public async Task<AtlasLobbyOperationResult> SetPawnCosmeticAsync(
        int slotIndex,
        string pawnCosmeticId)
    {
        if (!HasLobby ||
            currentSnapshot == null)
        {
            return AtlasLobbyOperationResult.Fail(
                "lobby.error.not_joinable",
                "No active lobby snapshot is available.");
        }


        string normalized =
            pawnCosmeticId != null
                ? pawnCosmeticId.Trim()
                : string.Empty;


        if (slotIndex < 0 ||
            slotIndex >= 4 ||
            string.IsNullOrWhiteSpace(normalized))
        {
            return AtlasLobbyOperationResult.Fail(
                "lobby.error.invalid_pawn_cosmetic",
                "Pawn cosmetic selection is invalid.");
        }


        LobbyPawnCosmeticRequest request =
            new LobbyPawnCosmeticRequest
            {
                lobbyId = currentLobbyId,
                slotIndex = slotIndex,
                pawnCosmeticId = normalized
            };


        AtlasLobbyOperationResult result =
            await CallLobbyFunctionAsync(
                "lobbySetPawnCosmetic",
                request);


        if (result.Success &&
            result.Snapshot != null)
        {
            AcceptSnapshot(result.Snapshot);
        }


        return result;
    }


    public async Task<AtlasLobbyOperationResult> SetReadyAsync(
        bool ready)
    {
        if (!HasLobby ||
            currentSnapshot == null)
        {
            return AtlasLobbyOperationResult.Fail(
                "lobby.error.not_joinable",
                "No active lobby snapshot is available.");
        }


        LobbyReadyRequest request =
            new LobbyReadyRequest
            {
                lobbyId = currentLobbyId,
                expectedSettingsRevision =
                    currentSnapshot.SettingsRevision,
                ready = ready
            };


        AtlasLobbyOperationResult result =
            await CallLobbyFunctionAsync(
                "lobbySetReady",
                request);


        if (result.Success &&
            result.Snapshot != null)
        {
            AcceptSnapshot(result.Snapshot);
        }


        return result;
    }


    public async Task<AtlasLobbyOperationResult> LeaveLobbyAsync()
    {
        if (!HasLobby)
        {
            return AtlasLobbyOperationResult.Fail(
                "lobby.error.not_joinable",
                "No active lobby is available to leave.");
        }


        LobbyLeaveRequest request =
            new LobbyLeaveRequest
            {
                lobbyId = currentLobbyId
            };


        AtlasLobbyOperationResult result =
            await CallLobbyFunctionAsync(
                "lobbyLeaveRoom",
                request);


        if (result.Success)
        {
            currentSnapshot = null;
            currentLobbyId = string.Empty;
            currentRoomCode = string.Empty;
            joinIdempotencyKey = string.Empty;
        }


        return result;
    }


    public async Task<AtlasLobbyOperationResult> KickMemberAsync(
        int slotIndex)
    {
        if (!HasLobby ||
            currentSnapshot == null)
        {
            return AtlasLobbyOperationResult.Fail(
                "lobby.error.not_joinable",
                "No active lobby snapshot is available.");
        }


        LobbyKickRequest request =
            new LobbyKickRequest
            {
                lobbyId = currentLobbyId,
                expectedSettingsRevision =
                    currentSnapshot.SettingsRevision,
                slotIndex = slotIndex
            };


        AtlasLobbyOperationResult result =
            await CallLobbyFunctionAsync(
                "lobbyKickMember",
                request);


        if (result.Success &&
            result.Snapshot != null)
        {
            AcceptSnapshot(result.Snapshot);
        }


        return result;
    }


    public async Task<AtlasLobbyOperationResult> StartMatchAsync()
    {
        if (!HasLobby ||
            currentSnapshot == null)
        {
            return AtlasLobbyOperationResult.Fail(
                "lobby.error.not_joinable",
                "No active lobby snapshot is available.");
        }


        // The RTDB rematch transition is asynchronous. The room may be
        // Waiting on the server while this bridge still holds the previous
        // round's SettingsRevision. Never start with an unverified revision.
        AtlasLobbySnapshot originalSnapshot = currentSnapshot;
        LobbyStartRequest request =
            new LobbyStartRequest
            {
                lobbyId = currentLobbyId,
                expectedSettingsRevision =
                    originalSnapshot.SettingsRevision
            };


        AtlasLobbyOperationResult result =
            await CallLobbyFunctionAsync(
                "lobbyStartMatch",
                request);


        if (!result.Success &&
            string.Equals(result.ErrorLocalizationKey,
                "lobby.error.stale_settings", StringComparison.Ordinal) &&
            HasLobby)
        {
            // Exactly one bounded resync/retry. A genuine host settings
            // change or change in occupied remote seats is NOT silently
            // accepted. Ready status is checked again by the RTDB host.
            AtlasLobbyOperationResult refreshed =
                await GetSnapshotAsync();
            AtlasLobbySnapshot latest = refreshed.Snapshot;
            bool sameConfiguration =
                refreshed.Success &&
                latest != null &&
                latest.LifecycleState == AtlasRoomLifecycleState.Waiting &&
                string.Equals(latest.LobbyId,
                    originalSnapshot.LobbyId, StringComparison.Ordinal) &&
                string.Equals(latest.HostAccountId,
                    originalSnapshot.HostAccountId, StringComparison.Ordinal) &&
                AreMatchSettingsEquivalent(
                    originalSnapshot.Settings, latest.Settings) &&
                AreOccupiedHumanSeatsEquivalent(
                    originalSnapshot.Members, latest.Members);


            if (sameConfiguration &&
                latest.SettingsRevision !=
                    originalSnapshot.SettingsRevision)
            {
                Debug.Log(
                    "ATB_RTDB_MATCH_REVISION_RESYNC expected=" +
                    originalSnapshot.SettingsRevision +
                    " actual=" + latest.SettingsRevision +
                    " retry=once");
                request.expectedSettingsRevision =
                    latest.SettingsRevision;
                result = await CallLobbyFunctionAsync(
                    "lobbyStartMatch", request);
            }
            else
            {
                Debug.LogWarning(
                    "ATB_RTDB_MATCH_REVISION_RESYNC skipped=" +
                    (sameConfiguration
                        ? "revision_not_advanced"
                        : "configuration_or_lifecycle_changed"));
            }
        }


        if (result.Success && result.Snapshot != null)
        {
            AcceptSnapshot(result.Snapshot);
            // One old owned Waiting room at most, in background; never block
            // the newly-started match or remove this active room.
            _ = SweepOwnedRoomsOnceAsync(1);
        }
        return result;
    }


    // Compare player-selected game rules, not derived counts such as BotCount
    // (which legitimately changes when open slots become Bots at Start).
    private static bool AreMatchSettingsEquivalent(
        AtlasLobbySettings before, AtlasLobbySettings after)
    {
        return before != null && after != null &&
            string.Equals(before.MapId, after.MapId, StringComparison.Ordinal) &&
            string.Equals(before.ThemeId, after.ThemeId, StringComparison.Ordinal) &&
            before.RoundLimit == after.RoundLimit &&
            before.MaxPlayers == after.MaxPlayers &&
            before.BalancedDevelopment == after.BalancedDevelopment &&
            before.DoublesEnabled == after.DoublesEnabled &&
            before.TripleDoublePenaltyEnabled ==
                after.TripleDoublePenaltyEnabled;
    }


    // Remote/local human ownership is never silently altered by auto-retry.
    // Bot/OpenOnline differences after finishing a match are intentional.
    private static bool AreOccupiedHumanSeatsEquivalent(
        List<AtlasLobbyMemberSnapshot> before,
        List<AtlasLobbyMemberSnapshot> after)
    {
        if (before == null || after == null ||
            before.Count != after.Count)
        {
            return false;
        }
        for (int i = 0; i < before.Count; ++i)
        {
            AtlasLobbyMemberSnapshot a = before[i];
            AtlasLobbyMemberSnapshot b = after[i];
            if (a == null || b == null)
            {
                if (a != b) return false;
                continue;
            }
            if (a.SlotIndex != b.SlotIndex) return false;
            bool humanA = a.Active && a.IsHumanSeat;
            bool humanB = b.Active && b.IsHumanSeat;
            if (humanA != humanB) return false;
            if (humanA &&
                !string.Equals(a.AccountId, b.AccountId,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }


    public async Task<AtlasLobbyOperationResult> GetSnapshotAsync()
    {
        if (!HasLobby)
        {
            return AtlasLobbyOperationResult.Fail(
                "lobby.error.not_joinable",
                "No active lobby id is available.");
        }


        LobbySnapshotRequest request =
            new LobbySnapshotRequest
            {
                lobbyId = currentLobbyId
            };


        AtlasLobbyOperationResult result =
            await CallLobbyFunctionAsync(
                "lobbyGetSnapshot",
                request);


        // A read must also advance the bridge's authoritative local cache.
        // Otherwise rematch returns Waiting in RTDB while Start keeps using
        // the previous match's SettingsRevision until a separate poll fires.
        if (result.Success && result.Snapshot != null &&
            string.Equals(result.Snapshot.LobbyId,
                currentLobbyId, StringComparison.Ordinal))
        {
            AcceptSnapshot(result.Snapshot);
        }
        return result;
    }


    public void ClearLocalLobbyState()
    {
        currentLobbyId = string.Empty;
        currentRoomCode = string.Empty;
        joinIdempotencyKey = string.Empty;
        currentSnapshot = null;
        // Preserve the ephemeral authenticated Guest across room re-entry.
        hostHeartbeatInFlight = false;
        nextHostHeartbeatAt = 0f;
        pawnCosmeticSyncInFlight = false;
    }


    private async Task<AtlasLobbyOperationResult> EnsureIdentityAsync()
    {
        // Never keep using an old Firebase UID/token after account switching.
        // The offline Guest is not a production RTDB authentication principal.
        if (!UsingLocalEmulators &&
            !AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
        {
            AtlasBoardAccountService active = AtlasBoardAccountService.Instance;
            bool matches = active != null && active.IsSignedIn &&
                string.Equals(active.CurrentAccountId, currentAccountId,
                    StringComparison.Ordinal);
            if (!matches)
            {
                initialized = false;
                idToken = string.Empty;
                currentAccountId = string.Empty;
            }
            else if (initialized && Time.unscaledTime >= nextIdentityRefreshAt)
            {
                try
                {
                    string refreshed = await active.GetCurrentIdTokenAsync(false);
                    if (!string.IsNullOrWhiteSpace(refreshed))
                    {
                        idToken = refreshed;
                        nextIdentityRefreshAt = Time.unscaledTime + 1200f;
                    }
                }
                catch { initialized = false; }
            }
        }
        if (initialized &&
            !string.IsNullOrWhiteSpace(idToken) &&
            !string.IsNullOrWhiteSpace(currentAccountId))
        {
            if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession &&
                Time.unscaledTime >= nextIdentityRefreshAt)
                return await RefreshLiveGuestQaIdentityAsync();
            return AtlasLobbyOperationResult.Ok(null);
        }


        if (initializing)
        {
            while (initializing)
            {
                await Task.Yield();
            }


            return initialized
                ? AtlasLobbyOperationResult.Ok(null)
                : AtlasLobbyOperationResult.Fail(
                    "account.error.authentication_required",
                    "Lobby identity initialization failed.");
        }


        initializing = true;


        try
        {
            if (UsingLocalEmulators)
            {
                return await EnsureLocalEmulatorIdentityAsync();
            }
            if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
                return await EnsureLiveGuestQaIdentityAsync();

            AtlasBoardAccountService accountService =
                AtlasBoardAccountService.Instance;


            if (accountService == null)
            {
                return AtlasLobbyOperationResult.Fail(
                    "account.error.authentication_required",
                    "AtlasBoardAccountService is not available.");
            }


            await accountService.EnsureInitializedAsync();


            // Distinguish an absent email/Steam Firebase session from an
            // Anonymous provider error instead of losing the actual cause.
            string anonymousAuthFailure = string.Empty;
            if (!accountService.IsSignedIn)
            {
                // Spark-compatible CASUAL room identity for an actual Steam
                // session when no email account is linked. This is an ordinary
                // Firebase ANONYMOUS UID, NOT Steam ownership verification.
                // It is never eligible for competitive XP, wallet or rewards.
                // Sign-in happens only after a deliberate online room action.
                AtlasBoardPlatformRuntime steam =
                    AtlasBoardPlatformRuntime.Instance;
                bool actualSteamSession = steam != null &&
                    steam.IsSteamProviderActive &&
                    steam.SteamInitialized && steam.SteamId != 0;
                if (actualSteamSession && accountService.IsInitialized)
                {
                    try
                    {
                        await FirebaseAuth.DefaultInstance.SignInAnonymouslyAsync();
                        Debug.Log("ATB_RTDB_CASUAL_AUTH provider=firebase_anonymous " +
                                  "steam_session=true steam_link_verified=false " +
                                  "rewards=false");
                    }
                    catch (Exception error)
                    {
                        FirebaseException firebase = error as FirebaseException;
                        string numeric = firebase != null
                            ? firebase.ErrorCode.ToString()
                            : "n/a";
                        string category = firebase != null
                            ? ((AuthError)firebase.ErrorCode).ToString()
                            : error.GetType().Name;
                        anonymousAuthFailure = "Anonymous Firebase Auth failed: " +
                            category + " (code " + numeric + ").";
                        Debug.LogWarning(
                            "ATB_RTDB_CASUAL_AUTH result=failed category=" +
                            category + " firebaseCode=" + numeric +
                            " project=atlasboard-usa " +
                            "check=Authentication_Sign-in_method_Anonymous " +
                            "mode=" +
                            (UsingLocalEmulators ? "emulators" : "live_spark") +
                            " detail=" + AtlasBoardFrontendErrorCodes.SanitizeForLog(error.Message));
                    }
                }
            }


            if (!accountService.IsSignedIn)
            {
                return AtlasLobbyOperationResult.Fail(
                    "account.error.authentication_required",
                    "Online Private Rooms require Firebase Auth. " +
                    (string.IsNullOrWhiteSpace(anonymousAuthFailure)
                        ? "Sign in with an existing Firebase account, or enable " +
                          "Anonymous Auth for Steam Casual rooms."
                        : anonymousAuthFailure +
                          " Check Firebase Console > Authentication > " +
                          "Sign-in method > Anonymous, and verify network access.") +
                    " Local Offline remains available.");
            }


            idToken =
                await accountService.GetCurrentIdTokenAsync(false);


            currentAccountId =
                accountService.CurrentAccountId;


            initialized =
                !string.IsNullOrWhiteSpace(idToken) &&
                !string.IsNullOrWhiteSpace(currentAccountId);
            if (initialized) nextIdentityRefreshAt = Time.unscaledTime + 1200f;


            return initialized
                ? AtlasLobbyOperationResult.Ok(null)
                : AtlasLobbyOperationResult.Fail(
                    "account.error.authentication_required",
                    "Firebase Auth did not return a valid token.");
        }
        catch (Exception exception)
        {
            return AtlasLobbyOperationResult.Fail(
                "account.error.service_unavailable",
                exception.Message);
        }
        finally
        {
            initializing = false;
        }
    }


    // Development-only Windows Guest: reuse one anonymous Firebase UID across
    // restarts. Never use FirebaseAuth native SDK in the stripped Guest build.
    // No new principal is created when an existing secure session cannot be
    // restored: this prevents orphaned Auth users and accidental name changes.
    private async Task<AtlasLobbyOperationResult> EnsureLiveGuestQaIdentityAsync()
    {
        bool hasSession = File.Exists(LiveGuestCredentialsPath());
        if (hasSession)
        {
            if (!TryLoadLiveGuestCredentials(out string uid, out string refresh))
            {
                Debug.LogWarning("ATB_LIVE_GUEST_QA restore=failed reason=local_credentials_unreadable");
                return AtlasLobbyOperationResult.Fail("account.error.service_unavailable",
                    "Guest account could not be restored. Your guest name is reserved; do not create a new account automatically.");
            }

            currentAccountId = uid;
            liveGuestRefreshToken = refresh;
            // Reauthenticate before using this identity or binding the name.
            AtlasLobbyOperationResult refreshed = await RefreshLiveGuestQaIdentityAsync();
            if (!refreshed.Success)
                return refreshed;

            AtlasLobbyOperationResult existingClaim = await EnsureLiveGuestNameClaimAsync();
            if (!existingClaim.Success)
                return existingClaim;

            initialized = true;
            AtlasBoardGuestIdentity.BindLiveAuthenticatedUid(currentAccountId);
            Debug.Log("ATB_LIVE_GUEST_QA auth=ready provider=anonymous identity=reused " +
                      "local_credentials=dpapi guest_name_claim=verified rewards=false");
            return AtlasLobbyOperationResult.Ok(null);
        }

        if (!CanStoreLiveGuestCredentials())
        {
            Debug.LogError("ATB_LIVE_GUEST_QA auth=blocked reason=dpapi_unavailable");
            return AtlasLobbyOperationResult.Fail("account.error.service_unavailable",
                "Secure Windows Guest session storage is not available.");
        }

        string endpoint =
            "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" +
            UnityWebRequest.EscapeURL(FirebaseWebApiKey);
        HttpJsonResult<AuthSignupResponse> auth =
            await SendJsonAsync<AuthAnonymousRequest, AuthSignupResponse>(
                endpoint, new AuthAnonymousRequest { returnSecureToken = true },
                string.Empty);
        if (!auth.Success || auth.Value == null ||
            string.IsNullOrWhiteSpace(auth.Value.idToken) ||
            string.IsNullOrWhiteSpace(auth.Value.localId) ||
            string.IsNullOrWhiteSpace(auth.Value.refreshToken))
        {
            Debug.LogWarning("ATB_LIVE_GUEST_QA auth=failed transport=https " +
                "reason=network_or_anonymous_provider_disabled " +
                "diagnostic=" + AtlasBoardFrontendErrorCodes.SanitizeForLog(auth.Error));
            return AtlasLobbyOperationResult.Fail("account.error.service_unavailable",
                "Live Guest QA could not obtain anonymous Firebase Auth.");
        }

        idToken = auth.Value.idToken;
        currentAccountId = auth.Value.localId;
        liveGuestRefreshToken = auth.Value.refreshToken;
        // Persist before binding/displaying the new name. If storage fails,
        // explicitly delete the just-created anonymous Auth user.
        if (!TrySaveLiveGuestCredentials(currentAccountId, liveGuestRefreshToken))
        {
            await TryDeleteNewAnonymousUserAsync(idToken);
            idToken = currentAccountId = liveGuestRefreshToken = string.Empty;
            return AtlasLobbyOperationResult.Fail("account.error.service_unavailable",
                "Guest account cannot be saved securely. Please check Windows user profile permissions.");
        }

        AtlasLobbyOperationResult claim = await EnsureLiveGuestNameClaimAsync();
        if (!claim.Success)
            return claim; // Retain secure credentials so the SAME UID can retry.

        initialized = true;
        nextIdentityRefreshAt = Time.unscaledTime + 2700f;
        AtlasBoardGuestIdentity.BindLiveAuthenticatedUid(currentAccountId);
        Debug.Log("ATB_LIVE_GUEST_QA auth=ready provider=anonymous identity=created " +
                  "local_credentials=dpapi guest_name_claim=verified rewards=false");
        return AtlasLobbyOperationResult.Ok(null);
    }

    private async Task<AtlasLobbyOperationResult> EnsureLiveGuestNameClaimAsync()
    {
        string name = AtlasBoardGuestIdentity.NameForAuthenticatedUid(currentAccountId);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(idToken))
            return AtlasLobbyOperationResult.Fail("account.error.authentication_required",
                "Guest name reservation needs a verified Firebase Auth session.");
        string url = GuestNameClaimsUrl + name + ".json?auth=" +
                     UnityWebRequest.EscapeURL(idToken);
        bool exists;
        using (UnityWebRequest read = new UnityWebRequest(url, "GET"))
        {
            read.downloadHandler = new DownloadHandlerBuffer();
            read.timeout = 15;
            await SendRequestAsync(read);
            if (read.result != UnityWebRequest.Result.Success || read.responseCode != 200)
            {
                Debug.LogWarning("ATB_GUEST_CLAIM result=read_failed http=" + read.responseCode);
                return AtlasLobbyOperationResult.Fail("account.error.service_unavailable",
                    "Guest name registry cannot be read. Verify deployed RTDB rules and network.");
            }
            string body = read.downloadHandler.text;
            exists = !string.IsNullOrWhiteSpace(body) && body.Trim() != "null";
            if (exists)
            {
                GuestNameClaim stored = null;
                try { stored = JsonUtility.FromJson<GuestNameClaim>(body); }
                catch { /* refuse malformed claims */ }
                if (stored != null && stored.uid == currentAccountId && stored.displayName == name)
                    return AtlasLobbyOperationResult.Ok(null);
                Debug.LogWarning("ATB_GUEST_CLAIM result=owner_mismatch");
                return AtlasLobbyOperationResult.Fail("account.error.permission_denied",
                    "This guest name is already reserved by another account.");
            }
        }
        GuestNameClaim claim = new GuestNameClaim {
            uid = currentAccountId, displayName = name,
            createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        using (UnityWebRequest write = new UnityWebRequest(url, "PUT"))
        {
            write.uploadHandler = new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(JsonUtility.ToJson(claim)));
            write.downloadHandler = new DownloadHandlerBuffer();
            write.SetRequestHeader("Content-Type", "application/json");
            // A first-writer-wins conditional RTDB write. Never overwrite a
            // name reserved by any other Firebase UID, even under a race.
            write.SetRequestHeader("if-match", "null_etag");
            write.timeout = 15;
            await SendRequestAsync(write);
            if (write.result != UnityWebRequest.Result.Success ||
                (write.responseCode != 200 && write.responseCode != 201))
            {
                Debug.LogWarning("ATB_GUEST_CLAIM result=write_failed http=" + write.responseCode);
                return AtlasLobbyOperationResult.Fail("account.error.service_unavailable",
                    "Guest name could not be reserved. Check RTDB rules or network.");
            }
        }
        Debug.Log("ATB_GUEST_CLAIM result=reserved unique=true owner=firebase_uid");
        return AtlasLobbyOperationResult.Ok(null);
    }

    private async Task<AtlasLobbyOperationResult> RefreshLiveGuestQaIdentityAsync()
    {
        if (string.IsNullOrWhiteSpace(liveGuestRefreshToken))
        {
            initialized = false;
            return AtlasLobbyOperationResult.Fail(
                "account.error.authentication_required", "Live Guest QA has no refresh credentials.");
        }
        string url = "https://securetoken.googleapis.com/v1/token?key=" +
            UnityWebRequest.EscapeURL(FirebaseWebApiKey);
        string form = "grant_type=refresh_token&refresh_token=" +
            UnityWebRequest.EscapeURL(liveGuestRefreshToken);
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(form));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            request.timeout = 20;
            await SendRequestAsync(request);
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("ATB_LIVE_GUEST_QA auth_refresh=failed http=" + request.responseCode);
                return AtlasLobbyOperationResult.Fail("account.error.service_unavailable",
                    "Guest authentication refresh failed. Existing identity was preserved.");
            }
            AuthRefreshResponse renewed = null;
            try { renewed = JsonUtility.FromJson<AuthRefreshResponse>(request.downloadHandler.text); }
            catch { /* reject malformed refresh payload */ }
            if (renewed == null || string.IsNullOrWhiteSpace(renewed.access_token) ||
                !string.Equals(renewed.user_id, currentAccountId, StringComparison.Ordinal))
                return AtlasLobbyOperationResult.Fail("account.error.authentication_required",
                    "Guest authentication is invalid. Existing identity will not be replaced automatically.");
            string newRefresh = string.IsNullOrWhiteSpace(renewed.refresh_token)
                ? liveGuestRefreshToken : renewed.refresh_token;
            if (!TrySaveLiveGuestCredentials(currentAccountId, newRefresh))
                return AtlasLobbyOperationResult.Fail("account.error.service_unavailable",
                    "Guest authentication refreshed but secure storage failed.");
            liveGuestRefreshToken = newRefresh;
            idToken = renewed.access_token;
            nextIdentityRefreshAt = Time.unscaledTime + 2700f;
            Debug.Log("ATB_LIVE_GUEST_QA auth_refresh=success uid_unchanged=true");
            return AtlasLobbyOperationResult.Ok(null);
        }
    }

    [Serializable]
    private sealed class GuestNameClaim
    {
        public string uid;
        public string displayName;
        public long createdAt;
    }

    private static string LiveGuestCredentialsPath() =>
        Path.Combine(Application.persistentDataPath, LiveGuestCredentialsFile);

    private static bool CanStoreLiveGuestCredentials()
    {
        return TryProtectGuestCredentials(Encoding.UTF8.GetBytes("probe"), out _);
    }

    private static bool TryLoadLiveGuestCredentials(out string uid, out string refresh)
    {
        uid = refresh = string.Empty;
        try
        {
            byte[] encrypted = File.ReadAllBytes(LiveGuestCredentialsPath());
            if (!TryUnprotectGuestCredentials(encrypted, out byte[] raw)) return false;
            string[] parts = Encoding.UTF8.GetString(raw).Split('\n');
            if (parts.Length != 4 || parts[0] != "ATB_LIVE_GUEST_V1" ||
                string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]) ||
                parts[3] != AtlasBoardGuestIdentity.NameForAuthenticatedUid(parts[1]))
                return false;
            uid = parts[1]; refresh = parts[2];
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_LIVE_GUEST_QA local_restore_failed type=" + e.GetType().Name);
            return false;
        }
    }

    private static bool TrySaveLiveGuestCredentials(string uid, string refresh)
    {
        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(refresh)) return false;
        string temp = LiveGuestCredentialsPath() + ".tmp";
        try
        {
            string alias = AtlasBoardGuestIdentity.NameForAuthenticatedUid(uid);
            byte[] plain = Encoding.UTF8.GetBytes("ATB_LIVE_GUEST_V1\n" + uid + "\n" + refresh + "\n" + alias);
            if (!TryProtectGuestCredentials(plain, out byte[] encrypted)) return false;
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllBytes(temp, encrypted);
            string destination = LiveGuestCredentialsPath();
            if (File.Exists(destination)) File.Replace(temp, destination, null);
            else File.Move(temp, destination);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_LIVE_GUEST_QA local_save_failed type=" + e.GetType().Name);
            return false;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    // Windows DPAPI protects credentials for the currently signed-in Windows
    // user. No plaintext token or secret is stored in Unity PlayerPrefs.
    [StructLayout(LayoutKind.Sequential)]
    private struct DpapiBlob { public int Length; public IntPtr Data; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DpapiBlob data, string description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DpapiBlob output);
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DpapiBlob data, IntPtr description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DpapiBlob output);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr handle);
#endif

    private static bool TryProtectGuestCredentials(byte[] raw, out byte[] protectedData) =>
        TransformGuestCredentials(raw, true, out protectedData);
    private static bool TryUnprotectGuestCredentials(byte[] raw, out byte[] plain) =>
        TransformGuestCredentials(raw, false, out plain);

    private static bool TransformGuestCredentials(byte[] raw, bool protect, out byte[] result)
    {
        result = null;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (raw == null || raw.Length == 0) return false;
        DpapiBlob input = new DpapiBlob { Length = raw.Length,
            Data = Marshal.AllocHGlobal(raw.Length) };
        DpapiBlob output = new DpapiBlob();
        try
        {
            Marshal.Copy(raw, 0, input.Data, raw.Length);
            bool ok = protect
                ? CryptProtectData(ref input, "AtlasBoard Live Guest", IntPtr.Zero,
                    IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero,
                    IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok || output.Length <= 0 || output.Data == IntPtr.Zero) return false;
            result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_LIVE_GUEST_QA dpapi_failed type=" + e.GetType().Name);
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero) LocalFree(output.Data);
        }
#else
        return false; // No insecure fallback outside the Windows QA Guest.
#endif
    }

    private async Task TryDeleteNewAnonymousUserAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            string url = "https://identitytoolkit.googleapis.com/v1/accounts:delete?key=" +
                UnityWebRequest.EscapeURL(FirebaseWebApiKey);
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(
                    Encoding.UTF8.GetBytes("{\"idToken\":\"" + token + "\"}"));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 12;
                await SendRequestAsync(request);
                Debug.Log("ATB_LIVE_GUEST_QA orphan_cleanup_attempted=true http=" +
                          request.responseCode);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_LIVE_GUEST_QA orphan_cleanup_failed type=" + e.GetType().Name);
        }
    }

    private async Task<AtlasLobbyOperationResult> EnsureLocalEmulatorIdentityAsync()
    {
        AtlasBoardPlatformRuntime platformRuntime =
            AtlasBoardPlatformRuntime.Instance;


        AtlasBoardSteamAccountLinkBridge steamLinkBridge =
            AtlasBoardSteamAccountLinkBridge.Instance;


        if (platformRuntime != null &&
            steamLinkBridge != null &&
            platformRuntime.IsSteamProviderActive &&
            platformRuntime.SteamInitialized &&
            platformRuntime.SteamId != 0)
        {
            AtlasBoardSteamReturningSignInResult steamResult =
                await steamLinkBridge
                    .AutomaticReturningSignInCurrentSteamAsync();


            if (steamResult != null &&
                steamResult.Success &&
                initialized &&
                !string.IsNullOrWhiteSpace(idToken) &&
                !string.IsNullOrWhiteSpace(currentAccountId))
            {
                Debug.Log(
                    "AtlasBoard Lobby Runtime Bridge: existing Steam-linked " +
                    "Atlas identity restored before emulator fallback. " +
                    $"UID={currentAccountId}.",
                    this);


                return AtlasLobbyOperationResult.Ok(null);
            }
        }


        /*
         * IMPORTANT:
         * This Development Guest already talks to the Auth/Functions
         * emulators over HTTP. Do not initialize FirebaseApp/Auth/Firestore
         * here. That native desktop initialization was racing with the
         * standalone Guest startup and could terminate the process before the
         * lobby identity became ready, especially while the Editor was also
         * running.
         */
        string devIdentityKey =
            BuildDeterministicDevelopmentIdentityKey(
                ResolveDevelopmentDisplayName());


        string devEmail =
            $"atlasboard.dev.{devIdentityKey}@example.com";


        string devPassword =
            $"AtlasBoardDev!{devIdentityKey}";


        AuthSignupRequest authRequest =
            new AuthSignupRequest
            {
                email = devEmail,
                password = devPassword,
                returnSecureToken = true
            };


        string apiKey =
            UnityWebRequest.EscapeURL(
                FirebaseWebApiKey);


        string signInUrl =
            $"http://{EmulatorHostForOnlineSubsystems}:{AuthEmulatorPort}/" +
            "identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=" +
            apiKey;


        HttpJsonResult<AuthSignupResponse> auth =
            await SendJsonAsync<AuthSignupRequest, AuthSignupResponse>(
                signInUrl,
                authRequest,
                string.Empty);


        if (!auth.Success ||
            auth.Value == null ||
            string.IsNullOrWhiteSpace(auth.Value.idToken) ||
            string.IsNullOrWhiteSpace(auth.Value.localId))
        {
            string signupUrl =
                $"http://{EmulatorHostForOnlineSubsystems}:{AuthEmulatorPort}/" +
                "identitytoolkit.googleapis.com/v1/accounts:signUp?key=" +
                apiKey;


            auth =
                await SendJsonAsync<AuthSignupRequest, AuthSignupResponse>(
                    signupUrl,
                    authRequest,
                    string.Empty);
        }


        if (!auth.Success ||
            auth.Value == null ||
            string.IsNullOrWhiteSpace(auth.Value.idToken) ||
            string.IsNullOrWhiteSpace(auth.Value.localId))
        {
            return AtlasLobbyOperationResult.Fail(
                "account.error.service_unavailable",
                "Stable Auth Emulator sign-in/sign-up failed: " +
                auth.Error);
        }


        idToken = auth.Value.idToken;
        currentAccountId = auth.Value.localId;


        DevBootstrapRequest bootstrap =
            new DevBootstrapRequest
            {
                displayName =
                    ResolveDevelopmentDisplayName()
            };


        AtlasLobbyOperationResult bootstrapResult =
            await CallLobbyFunctionAsync(
                "lobbyDevEnsureAccount",
                bootstrap,
                skipEnsureIdentity: true);


        if (!bootstrapResult.Success)
        {
            idToken = string.Empty;
            currentAccountId = string.Empty;
            return bootstrapResult;
        }


        initialized = true;


        Debug.Log(
            "AtlasBoard Lobby Runtime Bridge: local emulator identity ready. " +
            $"Name={ResolveDevelopmentDisplayName()}, " +
            $"UID={currentAccountId}, " +
            $"Client={(Application.isEditor ? "EDITOR" : "DEVELOPMENT BUILD")}, " +
            $"EmulatorHost={EmulatorHostForOnlineSubsystems}. " +
            "No production Auth/Firestore data was used.",
            this);


        return AtlasLobbyOperationResult.Ok(null);
    }


    private LobbyCreateRequest BuildCreateRequest(
        AtlasBoardLobbySelection selection)
    {
        return new LobbyCreateRequest
        {
            mapId = selection.MapId,
            themeId = selection.ThemeId,
            roundLimit = selection.RoundLimit,
            maxPlayers = selection.PlayerCount,
            balancedDevelopment = selection.BalancedDevelopment,
            doublesEnabled = selection.DoublesEnabled,
            tripleDoublePenaltyEnabled = selection.TripleDoublePenaltyEnabled,
            gameVersion = CurrentGameVersion(),
            protocolVersion = AtlasOnlineDefaults.ProtocolVersion,
            rulesVersion = AtlasOnlineDefaults.RulesVersion,
            contentVersion = "1",
            regionId = "eur3"
        };
    }


    private async Task<AtlasLobbyOperationResult> CallLobbyFunctionAsync<TRequest>(
        string functionName,
        TRequest request,
        bool skipEnsureIdentity = false)
    {
        if (!skipEnsureIdentity)
        {
            AtlasLobbyOperationResult init =
                await EnsureIdentityAsync();


            if (!init.Success)
            {
                return init;
            }
        }


        // Native Spark/RTDB transport. Emulator callable fixtures stay isolated.
        if (!UsingLocalEmulators)
        {
            return await AtlasBoardRtdbCasualLobbyTransport.ExecuteAsync(
                functionName, JsonUtility.ToJson(request), currentAccountId,
                idToken, currentLobbyId, currentSnapshot,
                AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession
                    ? AtlasBoardGuestIdentity.DisplayName
                    : AtlasBoardCanonicalPlayerIdentity.CurrentDisplayName);
        }


        string url =
            UsingLocalEmulators
                ? $"http://{EmulatorHostForOnlineSubsystems}:{FunctionsEmulatorPort}/" +
                  $"{ProjectId}/{Region}/{functionName}"
                : $"https://{Region}-{ProjectId}.cloudfunctions.net/{functionName}";


        string dataJson =
            JsonUtility.ToJson(request);


        string callableJson =
            "{\"data\":" + dataJson + "}";


        using UnityWebRequest webRequest =
            new UnityWebRequest(
                url,
                UnityWebRequest.kHttpVerbPOST);


        webRequest.uploadHandler =
            new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(callableJson));


        webRequest.downloadHandler =
            new DownloadHandlerBuffer();


        webRequest.SetRequestHeader(
            "Content-Type",
            "application/json");


        webRequest.SetRequestHeader(
            "Authorization",
            $"Bearer {idToken}");


        webRequest.timeout = 20;


        await AtlasBoardAppCheckRequestSecurity.ApplyAsync(
            webRequest);
        await SendRequestAsync(webRequest);


        string body =
            webRequest.downloadHandler != null
                ? webRequest.downloadHandler.text
                : string.Empty;


        if (webRequest.result !=
            UnityWebRequest.Result.Success)
        {
            CallableErrorEnvelope error =
                SafeFromJson<CallableErrorEnvelope>(body);


            string errorKey =
                error != null &&
                error.error != null &&
                error.error.details != null &&
                !string.IsNullOrWhiteSpace(error.error.details.errorKey)
                    ? error.error.details.errorKey
                    : "lobby.error.unknown";


            string technical =
                error != null &&
                error.error != null &&
                !string.IsNullOrWhiteSpace(error.error.message)
                    ? error.error.message
                    : $"HTTP {webRequest.responseCode}: {webRequest.error}";


            return AtlasLobbyOperationResult.Fail(
                errorKey,
                technical);
        }


        CallableLobbyEnvelope envelope =
            SafeFromJson<CallableLobbyEnvelope>(body);


        if (envelope == null ||
            envelope.result == null ||
            !envelope.result.ok)
        {
            return AtlasLobbyOperationResult.Fail(
                "lobby.error.service_unavailable",
                "Callable returned no valid lobby result. Body=" + body);
        }


        AtlasLobbySnapshot snapshot =
            envelope.result.snapshot != null
                ? ConvertSnapshot(envelope.result.snapshot)
                : null;


        return AtlasLobbyOperationResult.Ok(
            snapshot,
            envelope.result.roomCode,
            envelope.result.applied,
            envelope.result.started,
            envelope.result.idempotentReplay);
    }


    private static bool SeatPoliciesMatchSnapshot(
        string[] policies,
        AtlasLobbySnapshot snapshot)
    {
        if (policies == null ||
            policies.Length != 4 ||
            snapshot == null ||
            snapshot.Members == null)
        {
            return false;
        }


        for (int i = 0; i < 4; i++)
        {
            AtlasLobbyMemberSnapshot member =
                snapshot.Members.Find(
                    item => item != null &&
                            item.SlotIndex == i);


            if (member == null)
            {
                return false;
            }


            string expected =
                i == 0
                    ? "local_human"
                    : policies[i];


            if (expected == "online")
            {
                if (member.SeatMode != AtlasLobbySeatMode.OpenOnline &&
                    member.SeatMode != AtlasLobbySeatMode.RemoteHuman)
                {
                    return false;
                }
            }
            else if (expected == "local_human" &&
                     member.SeatMode != AtlasLobbySeatMode.HostLocal &&
                     member.SeatMode != AtlasLobbySeatMode.LocalHuman)
            {
                return false;
            }
            else if (expected == "bot" &&
                     member.SeatMode != AtlasLobbySeatMode.Bot)
            {
                return false;
            }
            else if (expected == "inactive" &&
                     member.SeatMode != AtlasLobbySeatMode.Inactive)
            {
                return false;
            }
        }


        return true;
    }


    private void AcceptSnapshot(
        AtlasLobbySnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }


        if (currentSnapshot != null &&
            string.Equals(currentSnapshot.LobbyId, snapshot.LobbyId,
                StringComparison.Ordinal) &&
            string.Equals(JsonUtility.ToJson(currentSnapshot),
                JsonUtility.ToJson(snapshot), StringComparison.Ordinal))
        {
            // Network polling may continue, but unchanged data must NEVER
            // rebuild the lobby, player HUD or match presentation.
            return;
        }


        if (currentSnapshot != null &&
            string.Equals(currentSnapshot.LobbyId, snapshot.LobbyId,
                StringComparison.Ordinal))
        {
            bool hasTransportRevisions =
                currentSnapshot.TransportRevision > 0 &&
                snapshot.TransportRevision > 0;


            if (hasTransportRevisions)
            {
                // RTDB roomState.revision is the lifecycle clock. A rematch
                // can be Starting -> Waiting at the SAME SettingsRevision.
                // Never use lifecycle rank to reject a newer authoritative
                // state, or accept a delayed pre-start Waiting poll.
                if (snapshot.TransportRevision <
                    currentSnapshot.TransportRevision)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log(
                        "ATB_RTDB_LOBBY_STALE_RESPONSE ignored=true " +
                        "incoming=" + snapshot.TransportRevision +
                        " cached=" + currentSnapshot.TransportRevision,
                        this);
#endif
                    return;
                }
                if (snapshot.TransportRevision ==
                        currentSnapshot.TransportRevision &&
                    snapshot.LifecycleState !=
                        currentSnapshot.LifecycleState)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(
                        "ATB_RTDB_LOBBY_INCONSISTENT_SAME_REV " +
                        "incoming=" + snapshot.LifecycleState +
                        " cached=" + currentSnapshot.LifecycleState +
                        " revision=" + snapshot.TransportRevision,
                        this);
#endif
                    return;
                }
            }
            else
            {
                // Backwards compatibility for older serialized rooms.
                // SettingsRevision alone was never a reliable lifecycle
                // clock; a real start/rematch still needs to be identified
                // by MatchId and state transition.
                if (snapshot.SettingsRevision <
                    currentSnapshot.SettingsRevision)
                {
                    return;
                }


                bool explicitRematchReturn =
                    (currentSnapshot.LifecycleState ==
                         AtlasRoomLifecycleState.Starting ||
                     currentSnapshot.LifecycleState ==
                         AtlasRoomLifecycleState.InMatch) &&
                    snapshot.LifecycleState ==
                        AtlasRoomLifecycleState.Waiting &&
                    !string.IsNullOrWhiteSpace(currentSnapshot.MatchId) &&
                    string.IsNullOrWhiteSpace(snapshot.MatchId);


                if (!explicitRematchReturn &&
                    snapshot.SettingsRevision ==
                        currentSnapshot.SettingsRevision &&
                    LifecycleRank(snapshot.LifecycleState) <
                        LifecycleRank(currentSnapshot.LifecycleState))
                {
                    return;
                }
            }
        }


        bool currentSnapshotIsActiveMatch =
            currentSnapshot != null &&
            (currentSnapshot.LifecycleState ==
                 AtlasRoomLifecycleState.Starting ||
             currentSnapshot.LifecycleState ==
                 AtlasRoomLifecycleState.InMatch);


        bool incomingSnapshotIsActiveMatch =
            snapshot.LifecycleState ==
                AtlasRoomLifecycleState.Starting ||
            snapshot.LifecycleState ==
                AtlasRoomLifecycleState.InMatch;


        bool suppressActiveMatchMemberOnlyBroadcast =
            currentSnapshotIsActiveMatch &&
            incomingSnapshotIsActiveMatch &&
            string.Equals(
                currentSnapshot.LobbyId,
                snapshot.LobbyId,
                StringComparison.Ordinal) &&
            string.Equals(
                currentSnapshot.MatchId,
                snapshot.MatchId,
                StringComparison.Ordinal) &&
            currentSnapshot.SettingsRevision ==
                snapshot.SettingsRevision;


        currentSnapshot = snapshot;
        currentLobbyId = snapshot.LobbyId;


        // During an active match, matchGetSnapshot is authoritative for
        // Human/TemporaryBot/PermanentBot lifecycle. The backend currently keeps
        // a started lobby in Starting while the match is active, so suppress
        // same-match Starting->Starting (and future InMatch->InMatch) member
        // churn. Re-broadcasting those snapshots re-entered the start-countdown
        // path and rebuilt local gameplay from round one when somebody left,
        // reconnected, or was AFK-removed. Lifecycle/match/settings transitions
        // are still broadcast normally.
        if (!suppressActiveMatchMemberOnlyBroadcast)
        {
            SnapshotChanged?.Invoke(snapshot);
        }


        if (snapshot.LifecycleState ==
            AtlasRoomLifecycleState.Waiting)
        {
            SyncLocallyOwnedPawnCosmeticsIfNeeded();
        }
    }


    private async void SyncLocallyOwnedPawnCosmeticsIfNeeded()
    {
        if (pawnCosmeticSyncInFlight ||
            currentSnapshot == null ||
            currentSnapshot.Members == null ||
            currentSnapshot.LifecycleState !=
                AtlasRoomLifecycleState.Waiting)
        {
            return;
        }


        AtlasBoardPawnCosmeticService service =
            AtlasBoardPawnCosmeticService.Instance;


        if (service == null ||
            service.Catalog == null)
        {
            return;
        }


        pawnCosmeticSyncInFlight = true;


        try
        {
            bool localIsHost =
                string.Equals(
                    currentSnapshot.HostAccountId,
                    currentAccountId,
                    StringComparison.Ordinal);


            foreach (AtlasLobbyMemberSnapshot member
                     in currentSnapshot.Members)
            {
                if (member == null ||
                    !member.Active)
                {
                    continue;
                }


                bool locallyOwned =
                    member.SeatMode ==
                        AtlasLobbySeatMode.RemoteHuman
                        ? string.Equals(
                            member.AccountId,
                            currentAccountId,
                            StringComparison.Ordinal)
                        : localIsHost &&
                          (member.SeatMode ==
                               AtlasLobbySeatMode.HostLocal ||
                           member.SeatMode ==
                               AtlasLobbySeatMode.LocalHuman ||
                           member.SeatMode ==
                               AtlasLobbySeatMode.Bot);


                if (!locallyOwned)
                {
                    continue;
                }


                PawnCosmeticDefinition cosmetic =
                    service.GetSelectedCosmetic(
                        member.SlotIndex);


                if (cosmetic == null ||
                    string.IsNullOrWhiteSpace(
                        cosmetic.CosmeticId) ||
                    string.Equals(
                        member.PawnCosmeticId,
                        cosmetic.CosmeticId,
                        StringComparison.Ordinal))
                {
                    continue;
                }


                AtlasLobbyOperationResult result =
                    await SetPawnCosmeticAsync(
                        member.SlotIndex,
                        cosmetic.CosmeticId);


                if (!result.Success)
                {
                    Debug.LogWarning(
                        "Automatic lobby pawn cosmetic sync failed for " +
                        $"P{member.SlotIndex + 1}: " +
                        result.TechnicalMessage,
                        this);
                    break;
                }
            }
        }
        finally
        {
            pawnCosmeticSyncInFlight = false;
        }
    }


    private static int LifecycleRank(
        AtlasRoomLifecycleState state)
    {
        return (int)state;
    }


    private static AtlasLobbySnapshot ConvertSnapshot(
        LobbyWireSnapshot wire)
    {
        AtlasLobbySnapshot snapshot =
            new AtlasLobbySnapshot
            {
                LobbyId = wire.lobbyId ?? string.Empty,
                HostAccountId = wire.hostAccountId ?? string.Empty,
                LifecycleState = ParseLifecycle(wire.lifecycleState),
                Visibility =
                    string.Equals(
                        wire.visibility,
                        "public",
                        StringComparison.OrdinalIgnoreCase)
                        ? AtlasRoomVisibility.Public
                        : AtlasRoomVisibility.Private,
                SettingsRevision = Mathf.Max(1, wire.settingsRevision),
                Settings = new AtlasLobbySettings
                {
                    MapId = wire.mapId ?? "Turkey",
                    ThemeId = wire.themeId ?? "classic_table",
                    RoundLimit = wire.roundLimit,
                    MaxPlayers = wire.maxPlayers,
                    RequiredHumanPlayers = wire.requiredHumanPlayers,
                    LocalHumanCount = wire.localHumanCount,
                    RemoteHumanCount = wire.remoteHumanCount,
                    RemoteReadyRequiredCount = wire.remoteReadyRequiredCount,
                    OpenOnlineSeatCount = wire.openOnlineSeatCount,
                    BotCount = wire.botCount,
                    BalancedDevelopment = wire.balancedDevelopment,
                    DoublesEnabled = wire.doublesEnabled,
                    TripleDoublePenaltyEnabled = wire.tripleDoublePenaltyEnabled
                },
                GameVersion = wire.gameVersion ?? string.Empty,
                ProtocolVersion = wire.protocolVersion,
                RulesVersion = wire.rulesVersion,
                ContentVersion = wire.contentVersion ?? string.Empty,
                RegionId = wire.regionId ?? string.Empty,
                MatchId = wire.matchId ?? string.Empty,
                StartEventId = wire.startEventId ?? string.Empty,
                StartCountdownEndsAtEpochMs =
                    wire.startCountdownEndsAtEpochMs,
                HasPassword = wire.hasPassword
            };


        if (wire.members != null)
        {
            foreach (LobbyWireMember member in wire.members)
            {
                if (member == null)
                {
                    continue;
                }


                AtlasLobbySeatMode seatMode =
                    ParseSeatMode(member.seatMode);


                snapshot.Members.Add(
                    new AtlasLobbyMemberSnapshot
                    {
                        SeatId = member.seatId ?? string.Empty,
                        SlotIndex = member.slotIndex,
                        Active = member.active,
                        SeatMode = seatMode,
                        IsHumanSeat =
                            seatMode == AtlasLobbySeatMode.HostLocal ||
                            seatMode == AtlasLobbySeatMode.LocalHuman ||
                            seatMode == AtlasLobbySeatMode.RemoteHuman ||
                            seatMode == AtlasLobbySeatMode.OpenOnline,
                        AccountId = member.accountId ?? string.Empty,
                        LocalOwnerAccountId = member.localOwnerAccountId ?? string.Empty,
                        DisplayName = member.displayName ?? string.Empty,
                        PawnCosmeticId = member.pawnCosmeticId ?? string.Empty,
                        IsHost = member.isHost,
                        ControllerKind = ParseControllerKind(member.controllerKind),
                        ConnectionState = ParseConnectionState(member.connectionState),
                        ReadyForRevision = member.readyForRevision,
                        RequiresReady = member.requiresReady
                    });
            }
        }


        return snapshot;
    }


    private static AtlasRoomLifecycleState ParseLifecycle(string value)
    {
        return value switch
        {
            "starting" => AtlasRoomLifecycleState.Starting,
            "in_match" => AtlasRoomLifecycleState.InMatch,
            "closing" => AtlasRoomLifecycleState.Closing,
            "closed" => AtlasRoomLifecycleState.Closed,
            _ => AtlasRoomLifecycleState.Waiting
        };
    }


    private static AtlasLobbySeatMode ParseSeatMode(string value)
    {
        return value switch
        {
            "host_local" => AtlasLobbySeatMode.HostLocal,
            "open_online" => AtlasLobbySeatMode.OpenOnline,
            "local_human" => AtlasLobbySeatMode.LocalHuman,
            "remote_human" => AtlasLobbySeatMode.RemoteHuman,
            "bot" => AtlasLobbySeatMode.Bot,
            "inactive" => AtlasLobbySeatMode.Inactive,
            _ => AtlasLobbySeatMode.Unknown
        };
    }


    private static AtlasSeatControllerKind ParseControllerKind(string value)
    {
        return value switch
        {
            "human" => AtlasSeatControllerKind.Human,
            "local_human" => AtlasSeatControllerKind.Human,
            "temporary_bot" => AtlasSeatControllerKind.TemporaryBot,
            "permanent_bot" => AtlasSeatControllerKind.PermanentBot,
            _ => AtlasSeatControllerKind.None
        };
    }


    private static AtlasSeatConnectionState ParseConnectionState(string value)
    {
        return value switch
        {
            "connected" => AtlasSeatConnectionState.Connected,
            "reconnecting" => AtlasSeatConnectionState.Reconnecting,
            "left" => AtlasSeatConnectionState.LeftVoluntarily,
            "afk_removed" => AtlasSeatConnectionState.AfkRemoved,
            "kicked" => AtlasSeatConnectionState.Kicked,
            _ => AtlasSeatConnectionState.Empty
        };
    }


    private static string BuildDeterministicDevelopmentIdentityKey(
        string displayName)
    {
        string normalized =
            string.IsNullOrWhiteSpace(displayName)
                ? "atlasboard-development-player"
                : displayName.Trim().ToLowerInvariant();


        using SHA256 sha256 =
            SHA256.Create();


        byte[] bytes =
            sha256.ComputeHash(
                Encoding.UTF8.GetBytes(
                    "atlasboard-local-emulator|" +
                    normalized));


        StringBuilder builder =
            new StringBuilder(24);


        for (int index = 0;
             index < 12 &&
             index < bytes.Length;
             index++)
        {
            builder.Append(
                bytes[index].ToString("x2"));
        }


        return builder.ToString();
    }


    private string ResolveDevelopmentDisplayName()
    {
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
            return AtlasBoardGuestIdentity.DisplayName;
        string commandLineName =
            ReadCommandLineValue(
                DevelopmentNameCommandLineSwitch);


        if (!string.IsNullOrWhiteSpace(
                commandLineName))
        {
            return commandLineName.Trim();
        }


        if (!string.IsNullOrWhiteSpace(
                developmentDisplayName))
        {
            return developmentDisplayName.Trim();
        }


        return Application.isEditor
            ? "Unity Editor Player"
            : "Unity Development Player";
    }


    private static bool HasCommandLineSwitch(
        string switchName)
    {
        if (string.IsNullOrWhiteSpace(
                switchName))
        {
            return false;
        }


        string[] args =
            Environment.GetCommandLineArgs();


        for (int i = 0;
             i < args.Length;
             i++)
        {
            if (string.Equals(
                    args[i],
                    switchName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }


        return false;
    }


    private static string ReadCommandLineValue(
        string switchName)
    {
        if (string.IsNullOrWhiteSpace(
                switchName))
        {
            return string.Empty;
        }


        string[] args =
            Environment.GetCommandLineArgs();


        string prefix =
            switchName + "=";


        for (int i = 0;
             i < args.Length;
             i++)
        {
            string arg =
                args[i] ?? string.Empty;


            if (arg.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return arg.Substring(
                    prefix.Length);
            }


            if (string.Equals(
                    arg,
                    switchName,
                    StringComparison.OrdinalIgnoreCase) &&
                i + 1 < args.Length)
            {
                return args[i + 1] ?? string.Empty;
            }
        }


        return string.Empty;
    }


    private string ResolveEmulatorHost()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string commandLineHost =
            ReadCommandLineValue(
                EmulatorHostCommandLineSwitch);


        if (TryNormalizeEmulatorHost(
                commandLineHost,
                out string normalized))
        {
            return normalized;
        }
#endif


        return DefaultEmulatorHost;
    }


    private static bool TryNormalizeEmulatorHost(
        string value,
        out string normalized)
    {
        normalized = string.Empty;


        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }


        string candidate = value.Trim();


        // The port is owned by AtlasBoard's emulator configuration. Accept only
        // a host/IP here so a launch argument cannot redirect individual paths
        // or inject a scheme. IPv4, localhost and private DNS/Tailscale names are
        // supported.
        if (candidate.Contains("://") ||
            candidate.Contains("/") ||
            candidate.Contains("\\") ||
            candidate.Contains("?") ||
            candidate.Contains("#") ||
            candidate.Contains(":") ||
            candidate.IndexOfAny(
                new[] { ' ', '\t', '\r', '\n' }) >= 0)
        {
            return false;
        }


        normalized = candidate;
        return true;
    }


    private static string CurrentGameVersion()
    {
        return string.IsNullOrWhiteSpace(Application.version)
            ? "0.1.0"
            : Application.version;
    }


    private static async Task SendRequestAsync(
        UnityWebRequest request)
    {
        UnityWebRequestAsyncOperation operation =
            request.SendWebRequest();


        TaskCompletionSource<bool> completion =
            new TaskCompletionSource<bool>();


        operation.completed +=
            _ => completion.TrySetResult(true);
        if (operation.isDone)
            completion.TrySetResult(true);

        await completion.Task;
    }


    private static async Task<HttpJsonResult<TResponse>> SendJsonAsync<TRequest, TResponse>(
        string url,
        TRequest request,
        string authorization)
        where TResponse : class
    {
        string json =
            JsonUtility.ToJson(request);


        using UnityWebRequest webRequest =
            new UnityWebRequest(
                url,
                UnityWebRequest.kHttpVerbPOST);


        webRequest.uploadHandler =
            new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(json));


        webRequest.downloadHandler =
            new DownloadHandlerBuffer();


        webRequest.SetRequestHeader(
            "Content-Type",
            "application/json");


        if (!string.IsNullOrWhiteSpace(authorization))
        {
            webRequest.SetRequestHeader(
                "Authorization",
                authorization);
        }


        webRequest.timeout = 20;
        await AtlasBoardAppCheckRequestSecurity.ApplyAsync(
            webRequest);
        await SendRequestAsync(webRequest);


        string body =
            webRequest.downloadHandler != null
                ? webRequest.downloadHandler.text
                : string.Empty;


        if (webRequest.result !=
            UnityWebRequest.Result.Success)
        {
            return new HttpJsonResult<TResponse>
            {
                Success = false,
                Error =
                    $"HTTP={webRequest.responseCode}, " +
                    $"Error={webRequest.error}, Body={body}"
            };
        }


        TResponse value =
            SafeFromJson<TResponse>(body);


        return new HttpJsonResult<TResponse>
        {
            Success = value != null,
            Value = value,
            Error = value == null
                ? "Response JSON could not be parsed. Body=" + body
                : string.Empty
        };
    }


    private static T SafeFromJson<T>(string json)
        where T : class
    {
        try
        {
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<T>(json);
        }
        catch
        {
            return null;
        }
    }


#if UNITY_EDITOR
    public void EditorConfigureDefaults()
    {
        useLocalEmulatorsInEditor = true;
        snapshotPollSeconds = 1f;


        if (string.IsNullOrWhiteSpace(developmentDisplayName))
        {
            developmentDisplayName = "Unity Editor Player";
        }
    }
#endif


    [Serializable]
    private sealed class PublicLobbyListRequest
    {
        public string gameVersion;
        public int protocolVersion;
        public int rulesVersion;
        public string contentVersion;
        public string regionId;
        public int limit;
    }


    [Serializable]
    private sealed class PublicLobbyWireCard
    {
        public string lobbyId;
        public string hostDisplayName;
        public string mapId;
        public string themeId;
        public int roundLimit;
        public int maxPlayers;
        public int occupiedPlayers;
        public int openOnlineSeatCount;
        public string regionId;
        public string gameVersion;
        public int protocolVersion;
        public int rulesVersion;
        public string contentVersion;
        public int settingsRevision;
        public bool hasPassword;
        public long createdAtEpochMs;
    }


    [Serializable]
    private sealed class CallablePublicLobbyListResult
    {
        public bool ok;
        public PublicLobbyWireCard[] rooms;
    }


    [Serializable]
    private sealed class CallablePublicLobbyListEnvelope
    {
        public CallablePublicLobbyListResult result;
    }


    [Serializable]
    private sealed class LobbyCreateRequest
    {
        public string mapId;
        public string themeId;
        public int roundLimit;
        public int maxPlayers;
        public bool balancedDevelopment;
        public bool doublesEnabled;
        public bool tripleDoublePenaltyEnabled;
        public string gameVersion;
        public int protocolVersion;
        public int rulesVersion;
        public string contentVersion;
        public string regionId;
    }


    [Serializable]
    private sealed class LobbyJoinRequest
    {
        public string roomCode;
        public string password;
        public string idempotencyKey;
        public string gameVersion;
        public int protocolVersion;
        public int rulesVersion;
        public string contentVersion;
        public string regionId;
    }


    [Serializable]
    private sealed class LobbyPublicJoinRequest
    {
        public string lobbyId;
        public string password;
        public string idempotencyKey;
        public string gameVersion;
        public int protocolVersion;
        public int rulesVersion;
        public string contentVersion;
        public string regionId;
    }


    [Serializable]
    private sealed class LobbyPasswordRequest
    {
        public string lobbyId;
        public int expectedSettingsRevision;
        public string password;
    }


    [Serializable]
    private sealed class LobbyCloseRequest
    {
        public string lobbyId;
    }


    [Serializable]
    private sealed class LobbyHeartbeatRequest
    {
        public string lobbyId;
    }


    [Serializable]
    private sealed class LobbyUpdateSettingsRequest
    {
        public string lobbyId;
        public int expectedSettingsRevision;
        public string mapId;
        public string themeId;
        public int roundLimit;
        public bool balancedDevelopment;
        public bool doublesEnabled;
        public bool tripleDoublePenaltyEnabled;
    }


    [Serializable]
    private sealed class LobbyConfigureSeatsRequest
    {
        public string lobbyId;
        public int expectedSettingsRevision;
        public int maxPlayers;
        public string[] seatPolicies;
    }


    [Serializable]
    private sealed class LobbyReadyRequest
    {
        public string lobbyId;
        public int expectedSettingsRevision;
        public bool ready;
    }


    [Serializable]
    private sealed class LobbyPawnCosmeticRequest
    {
        public string lobbyId;
        public int slotIndex;
        public string pawnCosmeticId;
    }


    [Serializable]
    private sealed class LobbyLeaveRequest
    {
        public string lobbyId;
    }


    [Serializable]
    private sealed class LobbyKickRequest
    {
        public string lobbyId;
        public int expectedSettingsRevision;
        public int slotIndex;
    }


    [Serializable]
    private sealed class LobbyStartRequest
    {
        public string lobbyId;
        public int expectedSettingsRevision;
    }


    [Serializable]
    private sealed class LobbySnapshotRequest
    {
        public string lobbyId;
    }


    [Serializable]
    private sealed class DevBootstrapRequest
    {
        public string displayName;
    }


    [Serializable]
    private sealed class AuthAnonymousRequest
    {
        public bool returnSecureToken;
    }

    [Serializable]
    private sealed class AuthRefreshResponse
    {
        public string access_token;
        public string refresh_token;
        public string user_id;
    }

    [Serializable]
    private sealed class AuthSignupRequest
    {
        public string email;
        public string password;
        public bool returnSecureToken;
    }


    [Serializable]
    private sealed class AuthSignupResponse
    {
        public string idToken;
        public string localId;
        public string email;
        public string refreshToken;
    }


    [Serializable]
    private sealed class CallableLobbyEnvelope
    {
        public LobbyCallableResult result;
    }


    [Serializable]
    private sealed class LobbyCallableResult
    {
        public bool ok;
        public bool applied;
        public bool started;
        public bool idempotentReplay;
        public string roomCode;
        public LobbyWireSnapshot snapshot;
    }


    [Serializable]
    private sealed class LobbyWireSnapshot
    {
        public string lobbyId;
        public string hostAccountId;
        public string lifecycleState;
        public string visibility;
        public int settingsRevision;
        public string mapId;
        public string themeId;
        public int roundLimit;
        public int maxPlayers;
        public int requiredHumanPlayers;
        public int localHumanCount;
        public int remoteHumanCount;
        public int remoteReadyRequiredCount;
        public int openOnlineSeatCount;
        public int botCount;
        public bool balancedDevelopment;
        public bool doublesEnabled;
        public bool tripleDoublePenaltyEnabled;
        public string gameVersion;
        public int protocolVersion;
        public int rulesVersion;
        public string contentVersion;
        public string regionId;
        public string matchId;
        public string startEventId;
        public long startCountdownEndsAtEpochMs;
        public bool hasPassword;
        public LobbyWireMember[] members;
    }


    [Serializable]
    private sealed class LobbyWireMember
    {
        public string seatId;
        public int slotIndex;
        public bool active;
        public string seatMode;
        public string seatType;
        public string accountId;
        public string localOwnerAccountId;
        public string displayName;
        public string pawnCosmeticId;
        public bool isHost;
        public string connectionState;
        public string controllerKind;
        public int readyForRevision;
        public bool requiresReady;
    }


    [Serializable]
    private sealed class CallableErrorEnvelope
    {
        public CallableError error;
    }


    [Serializable]
    private sealed class CallableError
    {
        public string status;
        public string message;
        public CallableErrorDetails details;
    }


    [Serializable]
    private sealed class CallableErrorDetails
    {
        public string errorKey;
    }


    private sealed class HttpJsonResult<T>
        where T : class
    {
        public bool Success;
        public T Value;
        public string Error;
    }
}

