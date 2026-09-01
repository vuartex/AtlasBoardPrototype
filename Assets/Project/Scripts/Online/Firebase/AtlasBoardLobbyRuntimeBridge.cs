using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Firebase;
using UnityEngine;
using UnityEngine.Networking;

public sealed class AtlasBoardLobbyRuntimeBridge : MonoBehaviour
{
    private const string ProjectId = "atlasboard-usa";
    private const string Region = "europe-west1";
    private const string EmulatorHost = "127.0.0.1";
    private const int AuthEmulatorPort = 9099;
    private const int FunctionsEmulatorPort = 5001;

    private const string LocalEmulatorCommandLineSwitch =
        "-atlasLocalEmulators";

    private const string DevelopmentNameCommandLineSwitch =
        "-atlasDevName";

    [Header("Development")]
    [SerializeField]
    private bool useLocalEmulatorsInEditor = true;

    [SerializeField, Min(0.25f)]
    private float snapshotPollSeconds = 1f;

    [SerializeField]
    private string developmentDisplayName = "Unity Editor Player";

    private string idToken = string.Empty;
    private string currentAccountId = string.Empty;
    private string currentLobbyId = string.Empty;
    private string currentRoomCode = string.Empty;
    private string joinIdempotencyKey = string.Empty;
    private AtlasLobbySnapshot currentSnapshot;
    private bool initialized;
    private bool initializing;
    private bool pollInFlight;
    private float nextPollAt;

    public event Action<AtlasLobbySnapshot> SnapshotChanged;
    public event Action<string> LobbyAccessLost;

    public string CurrentAccountId => currentAccountId;
    public string CurrentLobbyId => currentLobbyId;
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
            return useLocalEmulatorsInEditor;
#elif DEVELOPMENT_BUILD
            // Standalone test clients may use localhost emulators only when
            // explicitly launched with the dedicated development switch.
            // Normal/release builds cannot enter this path.
            return HasCommandLineSwitch(
                LocalEmulatorCommandLineSwitch);
#else
            return false;
#endif
        }
    }

    private async void Update()
    {
        if (!HasLobby ||
            pollInFlight ||
            Time.unscaledTime < nextPollAt)
        {
            return;
        }

        nextPollAt =
            Time.unscaledTime +
            Mathf.Max(0.25f, snapshotPollSeconds);

        pollInFlight = true;

        try
        {
            AtlasLobbyOperationResult result =
                await GetSnapshotAsync();

            if (result.Success &&
                result.Snapshot != null)
            {
                AcceptSnapshot(result.Snapshot);
            }
            else if (string.Equals(
                         result.ErrorLocalizationKey,
                         "lobby.error.kicked",
                         StringComparison.OrdinalIgnoreCase))
            {
                LobbyAccessLost?.Invoke(
                    result.ErrorLocalizationKey);

                ClearLocalLobbyState();
            }
        }
        finally
        {
            pollInFlight = false;
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
        AtlasLobbyOperationResult init =
            await EnsureIdentityAsync();

        if (!init.Success)
        {
            return AtlasPublicLobbyListResult.Fail(
                init.ErrorLocalizationKey,
                init.TechnicalMessage);
        }

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
                ? $"http://{EmulatorHost}:{FunctionsEmulatorPort}/" +
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

        LobbyStartRequest request =
            new LobbyStartRequest
            {
                lobbyId = currentLobbyId,
                expectedSettingsRevision =
                    currentSnapshot.SettingsRevision
            };

        AtlasLobbyOperationResult result =
            await CallLobbyFunctionAsync(
                "lobbyStartMatch",
                request);

        if (result.Success &&
            result.Snapshot != null)
        {
            AcceptSnapshot(result.Snapshot);
        }

        return result;
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

        return await CallLobbyFunctionAsync(
            "lobbyGetSnapshot",
            request);
    }

    public void ClearLocalLobbyState()
    {
        currentLobbyId = string.Empty;
        currentRoomCode = string.Empty;
        joinIdempotencyKey = string.Empty;
        currentSnapshot = null;
    }

    private async Task<AtlasLobbyOperationResult> EnsureIdentityAsync()
    {
        if (initialized &&
            !string.IsNullOrWhiteSpace(idToken) &&
            !string.IsNullOrWhiteSpace(currentAccountId))
        {
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

            AtlasBoardAccountService accountService =
                AtlasBoardAccountService.Instance;

            if (accountService == null)
            {
                return AtlasLobbyOperationResult.Fail(
                    "account.error.authentication_required",
                    "AtlasBoardAccountService is not available.");
            }

            await accountService.EnsureInitializedAsync();

            if (!accountService.IsSignedIn)
            {
                return AtlasLobbyOperationResult.Fail(
                    "account.error.authentication_required",
                    "A signed-in Atlas account is required.");
            }

            idToken =
                await accountService.GetCurrentIdTokenAsync(false);

            currentAccountId =
                accountService.CurrentAccountId;

            initialized =
                !string.IsNullOrWhiteSpace(idToken) &&
                !string.IsNullOrWhiteSpace(currentAccountId);

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

    private async Task<AtlasLobbyOperationResult> EnsureLocalEmulatorIdentityAsync()
    {
        DependencyStatus dependencyStatus =
            await FirebaseApp.CheckAndFixDependenciesAsync();

        if (dependencyStatus != DependencyStatus.Available)
        {
            return AtlasLobbyOperationResult.Fail(
                "account.error.service_unavailable",
                "Firebase dependencies unavailable: " + dependencyStatus);
        }

        FirebaseApp app =
            FirebaseApp.DefaultInstance;

        if (app == null ||
            app.Options == null ||
            app.Options.ProjectId != ProjectId)
        {
            return AtlasLobbyOperationResult.Fail(
                "account.error.service_unavailable",
                "Default Firebase project is not atlasboard-usa.");
        }

        string unique =
            Guid.NewGuid().ToString("N");

        AuthSignupRequest signupRequest =
            new AuthSignupRequest
            {
                email =
                    $"atlasboard.unity.{unique}@example.com",
                password =
                    $"AtlasBoardUnity!{unique}",
                returnSecureToken = true
            };

        string authUrl =
            $"http://{EmulatorHost}:{AuthEmulatorPort}/" +
            "identitytoolkit.googleapis.com/v1/accounts:signUp?key=" +
            UnityWebRequest.EscapeURL(app.Options.ApiKey ?? "fake-api-key");

        HttpJsonResult<AuthSignupResponse> signup =
            await SendJsonAsync<AuthSignupRequest, AuthSignupResponse>(
                authUrl,
                signupRequest,
                string.Empty);

        if (!signup.Success ||
            signup.Value == null ||
            string.IsNullOrWhiteSpace(signup.Value.idToken) ||
            string.IsNullOrWhiteSpace(signup.Value.localId))
        {
            return AtlasLobbyOperationResult.Fail(
                "account.error.service_unavailable",
                "Auth Emulator sign-up failed: " + signup.Error);
        }

        idToken = signup.Value.idToken;
        currentAccountId = signup.Value.localId;

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
            $"Client={(Application.isEditor ? "EDITOR" : "DEVELOPMENT BUILD")}. " +
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

        string url =
            UsingLocalEmulators
                ? $"http://{EmulatorHost}:{FunctionsEmulatorPort}/" +
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
            string.Equals(
                currentSnapshot.LobbyId,
                snapshot.LobbyId,
                StringComparison.Ordinal))
        {
            // A polling request can begin before a host mutation and finish
            // after the mutation response. Never let that older response move
            // the bridge back to a lower settings revision.
            if (snapshot.SettingsRevision <
                currentSnapshot.SettingsRevision)
            {
                return;
            }

            // Starting/Started style lifecycle progression must also never be
            // repainted by an older Waiting response at the same revision.
            if (snapshot.SettingsRevision ==
                    currentSnapshot.SettingsRevision &&
                LifecycleRank(snapshot.LifecycleState) <
                    LifecycleRank(currentSnapshot.LifecycleState))
            {
                return;
            }
        }

        currentSnapshot = snapshot;
        currentLobbyId = snapshot.LobbyId;
        SnapshotChanged?.Invoke(snapshot);
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

    private string ResolveDevelopmentDisplayName()
    {
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
