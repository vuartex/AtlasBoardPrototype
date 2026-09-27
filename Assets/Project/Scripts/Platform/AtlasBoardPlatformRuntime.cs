using System;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(-9000)]
[DisallowMultipleComponent]
public sealed class AtlasBoardPlatformRuntime :
    MonoBehaviour
{
    private const float PresencePollSeconds = 1f;
    private const float AchievementPollSeconds = 30f;
    private const float ProviderSignInRetryInitialSeconds = 5f;
    private const float ProviderSignInRetryMaxSeconds = 30f;

    private static AtlasBoardPlatformRuntime instance;

    private AtlasBoardDevelopmentPlatformAdapter
        developmentAdapter;

    private AtlasBoardSteamPlatformAdapter
        steamAdapter;

    private IAtlasIdentityProvider identityProvider;
    private IAtlasInviteProvider inviteProvider;
    private IAtlasIncomingInviteProvider incomingInviteProvider;
    private IAtlasPresenceProvider presenceProvider;
    private IAtlasAchievementProjectionProvider achievementProvider;

    private AtlasBoardLobbyRuntimeBridge lobbyBridge;
    private AtlasBoardProgressionBridge progressionBridge;

    private float nextPresencePollAt;
    private float nextAchievementPollAt;

    private string lastPresenceFingerprint =
        string.Empty;

    private bool presenceSyncInFlight;
    private bool achievementSyncInFlight;
    private bool providerSignInStarted;
    private bool providerSignInComplete;
    private int providerSignInFailureCount;
    private float nextProviderSignInAttemptAt;
    private bool returningSteamSignInStarted;
    private bool returningSteamSignInComplete;
    private bool steamRecoveryAchievementResyncPending;
    private string steamRecoveryAchievementAccountId =
        string.Empty;

    private string pendingStartupJoinCode =
        string.Empty;

    public static AtlasBoardPlatformRuntime Instance =>
        instance;

    public string ActiveProviderId =>
        identityProvider != null
            ? identityProvider.ProviderId
            : "none";

    public AtlasPlatformKind ActivePlatform =>
        identityProvider != null
            ? identityProvider.Platform
            : AtlasPlatformKind.Unknown;

    public AtlasPlayerIdentity CurrentPlatformIdentity =>
        identityProvider != null
            ? identityProvider.CurrentIdentity
            : null;

    public bool IsSteamProviderActive =>
        steamAdapter != null &&
        ReferenceEquals(
            identityProvider,
            steamAdapter);

    public bool SteamInitialized =>
        steamAdapter != null &&
        steamAdapter.IsInitialized;

    public uint SteamAppId =>
        steamAdapter != null
            ? steamAdapter.CurrentAppId
            : 0;

    public ulong SteamId =>
        steamAdapter != null
            ? steamAdapter.CurrentSteamId
            : 0;

    public string SteamPersonaName =>
        steamAdapter != null
            ? steamAdapter.CurrentPersonaName
            : string.Empty;

    public string SteamLastError =>
        steamAdapter != null
            ? steamAdapter.LastError
            : string.Empty;

    public int SteamProjectedAchievementCount =>
        steamAdapter != null
            ? steamAdapter.ProjectedAchievementCount
            : 0;

    public AtlasBoardPlatformPresence CurrentPresence
    {
        get;
        private set;
    } = new AtlasBoardPlatformPresence();

    public event Action<string> PlatformJoinRequested;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntime()
    {
        if (instance != null)
        {
            return;
        }

        AtlasBoardPlatformRuntime existing =
            FindAnyObjectByType<
                AtlasBoardPlatformRuntime>(
                FindObjectsInactive.Include);

        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject root =
            new GameObject(
                "AtlasBoardPlatformRuntime");

        root.AddComponent<
            AtlasBoardPlatformRuntime>();
    }

    private void Awake()
    {
        if (instance != null &&
            instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        ConfigureProviders();
        CaptureStartupJoinPayload();

        _ = EnsureProviderSignedInAsync();
    }

    private void OnDestroy()
    {
        if (incomingInviteProvider != null)
        {
            incomingInviteProvider.JoinRequested -=
                HandleProviderJoinRequested;
        }

        steamAdapter?.Shutdown();

        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        steamAdapter?.RunCallbacks();

        ResolveRuntimeBridges();
        RefreshCanonicalIdentityBinding();

        if (!providerSignInStarted &&
            !providerSignInComplete &&
            Time.unscaledTime >= nextProviderSignInAttemptAt)
        {
            _ = EnsureProviderSignedInAsync();
        }

        if (!string.IsNullOrWhiteSpace(
                pendingStartupJoinCode))
        {
            string code =
                pendingStartupJoinCode;

            if (TryOpenJoinOverlay(code))
            {
                pendingStartupJoinCode =
                    string.Empty;
            }
        }

        if (Time.unscaledTime >=
            nextPresencePollAt)
        {
            nextPresencePollAt =
                Time.unscaledTime +
                PresencePollSeconds;

            _ = RefreshPresenceAsync();
        }

        if (steamRecoveryAchievementResyncPending &&
            !achievementSyncInFlight &&
            achievementProvider != null &&
            achievementProvider.SupportsAchievementProjection &&
            progressionBridge != null)
        {
            string accountId =
                steamRecoveryAchievementAccountId;

            steamRecoveryAchievementResyncPending = false;
            steamRecoveryAchievementAccountId = string.Empty;

            _ = SynchronizeAchievementsAfterSteamRecoveryAsync(
                accountId);
        }

        if (Time.unscaledTime >=
            nextAchievementPollAt)
        {
            nextAchievementPollAt =
                Time.unscaledTime +
                AchievementPollSeconds;

            _ = SynchronizeAchievementsNowAsync();
        }
    }

    public async Task<bool> ShowInviteUiAsync()
    {
        ResolveRuntimeBridges();

        if (inviteProvider == null ||
            lobbyBridge == null ||
            !lobbyBridge.HasLobby)
        {
            return false;
        }

        AtlasRoomDescriptor room =
            BuildRoomDescriptor(
                lobbyBridge);

        if (room == null ||
            string.IsNullOrWhiteSpace(
                room.RoomCode))
        {
            return false;
        }

        if (inviteProvider.SupportsNativeInvites)
        {
            return await inviteProvider
                .ShowNativeInviteAsync(room);
        }

        return false;
    }

    public string BuildCurrentShareableJoinLink()
    {
        ResolveRuntimeBridges();

        if (inviteProvider == null ||
            lobbyBridge == null ||
            !lobbyBridge.HasLobby ||
            !inviteProvider.SupportsShareableJoinLink)
        {
            return string.Empty;
        }

        return inviteProvider.BuildShareableJoinLink(
            BuildRoomDescriptor(lobbyBridge));
    }

    public async Task<
        AtlasBoardAchievementProjectionResult>
        SynchronizeAchievementsNowAsync()
    {
        if (achievementSyncInFlight)
        {
            return AtlasBoardAchievementProjectionResult.Fail(
                "Achievement synchronization is already in flight.");
        }

        ResolveRuntimeBridges();

        if (achievementProvider == null ||
            !achievementProvider
                .SupportsAchievementProjection ||
            progressionBridge == null)
        {
            return AtlasBoardAchievementProjectionResult.Fail(
                "No active achievement projection provider.");
        }

        achievementSyncInFlight = true;

        try
        {
            AtlasBoardProgressionSnapshot profile =
                await progressionBridge
                    .GetProfileAsync();

            if (profile == null ||
                !profile.Success)
            {
                return AtlasBoardAchievementProjectionResult.Fail(
                    profile != null
                        ? profile.TechnicalMessage
                        : "Progression profile is unavailable.");
            }

            string[] unlockedIds =
                profile.Achievements
                    .Where(
                        item =>
                            item != null &&
                            item.unlocked &&
                            !string.IsNullOrWhiteSpace(
                                item.achievementId))
                    .Select(
                        item =>
                            item.achievementId.Trim())
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

            return await achievementProvider
                .SynchronizeAchievementsAsync(
                    unlockedIds);
        }
        finally
        {
            achievementSyncInFlight = false;
        }
    }

#if UNITY_EDITOR
    public bool DevelopmentSimulateIncomingInvite(
        string payload)
    {
        if (developmentAdapter == null)
        {
            return false;
        }

        if (!AtlasBoardPlatformJoinPayload.TryParse(
                payload,
                out _))
        {
            return false;
        }

        developmentAdapter
            .SimulateIncomingInvite(payload);

        return true;
    }

    public int DevelopmentProjectedAchievementCount =>
        developmentAdapter != null
            ? developmentAdapter
                .ProjectedAchievementCount
            : 0;
#endif

    private async Task EnsureProviderSignedInAsync()
    {
        if (providerSignInStarted ||
            identityProvider == null)
        {
            return;
        }

        providerSignInStarted = true;

        try
        {
            bool signedIn =
                await identityProvider
                    .EnsureSignedInAsync();

            if (signedIn)
            {
                providerSignInComplete = true;
                providerSignInFailureCount = 0;
                nextProviderSignInAttemptAt =
                    float.PositiveInfinity;

                if (IsSteamProviderActive &&
                    !returningSteamSignInStarted &&
                    !returningSteamSignInComplete)
                {
                    _ = TryAutomaticReturningSteamSignInAsync();
                }

                return;
            }

            providerSignInFailureCount++;

            float retryDelaySeconds =
                Mathf.Min(
                    ProviderSignInRetryMaxSeconds,
                    ProviderSignInRetryInitialSeconds *
                    Mathf.Pow(
                        2f,
                        Mathf.Min(
                            providerSignInFailureCount - 1,
                            3)));

            nextProviderSignInAttemptAt =
                Time.unscaledTime + retryDelaySeconds;

            if (IsSteamProviderActive)
            {
                Debug.LogWarning(
                    "AtlasBoard Steam provider is selected but Steam " +
                    "identity is not ready. The game remains playable " +
                    "through canonical Firebase identity. " +
                    SteamLastError +
                    " Next Steam sign-in retry in " +
                    retryDelaySeconds.ToString("0") +
                    " seconds.");
            }
        }
        finally
        {
            providerSignInStarted = false;
        }
    }

    private async Task TryAutomaticReturningSteamSignInAsync()
    {
        if (returningSteamSignInStarted ||
            returningSteamSignInComplete ||
            !IsSteamProviderActive ||
            !SteamInitialized ||
            SteamId == 0)
        {
            return;
        }

        returningSteamSignInStarted = true;

        try
        {
            AtlasBoardSteamAccountLinkBridge bridge =
                AtlasBoardSteamAccountLinkBridge.Instance;

            if (bridge == null)
            {
                return;
            }

            AtlasBoardSteamReturningSignInResult result =
                await bridge
                    .AutomaticReturningSignInCurrentSteamAsync();

            if (result != null && result.Success)
            {
                returningSteamSignInComplete = true;
                Debug.Log(
                    "AtlasBoard automatic Steam returning sign-in restored " +
                    "canonical Atlas account " + result.AccountId + ".",
                    this);

                steamRecoveryAchievementAccountId =
                    result.AccountId ?? string.Empty;
                steamRecoveryAchievementResyncPending = true;
            }
            else
            {
                Debug.Log(
                    "AtlasBoard automatic Steam returning sign-in did not " +
                    "restore an existing account. Continuing with the " +
                    "current Atlas identity.",
                    this);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "AtlasBoard automatic Steam returning sign-in failed without " +
                "blocking gameplay: " + exception.Message,
                this);
        }
        finally
        {
            returningSteamSignInStarted = false;
        }
    }

    private async Task SynchronizeAchievementsAfterSteamRecoveryAsync(
        string accountId)
    {
        AtlasBoardAchievementProjectionResult result =
            await SynchronizeAchievementsNowAsync();

        string safeAccountId =
            accountId ?? string.Empty;

        if (result != null && result.Success)
        {
            Debug.Log(
                "AtlasBoard Steam recovery achievement re-sync completed. " +
                "AccountId=" + safeAccountId +
                ", Requested=" + result.RequestedCount +
                ", NewlyProjected=" + result.NewlyProjectedCount +
                ", TotalProjected=" + result.TotalProjectedCount + ".",
                this);
            return;
        }

        string technicalMessage =
            result != null
                ? result.TechnicalMessage
                : "Achievement synchronization returned no result.";

        Debug.LogWarning(
            "AtlasBoard Steam recovery achievement re-sync attempted but " +
            "did not complete. AccountId=" + safeAccountId +
            ". " + technicalMessage,
            this);
    }

    private void ConfigureProviders()
    {
        bool useSteam =
            ShouldUseSteamProvider();

        if (useSteam &&
            AtlasBoardSteamPlatformAdapter
                .IsSteamworksCompiled)
        {
            steamAdapter =
                new AtlasBoardSteamPlatformAdapter();

            identityProvider = steamAdapter;
            inviteProvider = steamAdapter;
            incomingInviteProvider = steamAdapter;
            presenceProvider = steamAdapter;
            achievementProvider = steamAdapter;
        }
        else
        {
            developmentAdapter =
                new AtlasBoardDevelopmentPlatformAdapter();

            identityProvider =
                developmentAdapter;
            inviteProvider =
                developmentAdapter;
            incomingInviteProvider =
                developmentAdapter;
            presenceProvider =
                developmentAdapter;
            achievementProvider =
                developmentAdapter;
        }

        if (incomingInviteProvider != null)
        {
            incomingInviteProvider.JoinRequested +=
                HandleProviderJoinRequested;
        }
    }

    private static bool ShouldUseSteamProvider()
    {
        if (!AtlasBoardSteamRuntimeSettings
                .IsDesktopSteamCandidate)
        {
            return false;
        }

#if UNITY_EDITOR
        return AtlasBoardSteamRuntimeSettings
            .UseSteamInEditor;
#else
        return true;
#endif
    }

    private void ResolveRuntimeBridges()
    {
        if (lobbyBridge == null)
        {
            AtlasBoardLobbyRuntimeBridge[] lobbies =
                Resources.FindObjectsOfTypeAll<
                    AtlasBoardLobbyRuntimeBridge>();

            lobbyBridge =
                lobbies.FirstOrDefault(
                    item =>
                        item != null &&
                        item.gameObject.scene.IsValid());
        }

        if (progressionBridge == null)
        {
            AtlasBoardProgressionBridge[] progression =
                Resources.FindObjectsOfTypeAll<
                    AtlasBoardProgressionBridge>();

            progressionBridge =
                progression.FirstOrDefault(
                    item =>
                        item != null &&
                        item.gameObject.scene.IsValid());
        }
    }

    private void RefreshCanonicalIdentityBinding()
    {
        if (lobbyBridge == null)
        {
            return;
        }

        string accountId =
            lobbyBridge.CurrentAccountId;

        if (string.IsNullOrWhiteSpace(
                accountId))
        {
            return;
        }

        string displayName =
            ResolveDisplayName(
                lobbyBridge.CurrentSnapshot,
                accountId);

        if (developmentAdapter != null)
        {
            developmentAdapter.BindCanonicalIdentity(
                accountId,
                displayName,
                ResolveNativePlatform());
        }

        if (steamAdapter != null)
        {
            steamAdapter.BindCanonicalIdentity(
                accountId,
                displayName);
        }
    }

    private async Task RefreshPresenceAsync()
    {
        if (presenceSyncInFlight ||
            presenceProvider == null ||
            !presenceProvider.SupportsRichPresence)
        {
            return;
        }

        AtlasBoardPlatformPresence next =
            BuildPresence();

        if (steamAdapter != null)
        {
            steamAdapter.SetConnectString(
                BuildCurrentSteamConnectString());
        }

        string fingerprint =
            next.Fingerprint() +
            "|" +
            BuildCurrentSteamConnectString();

        if (fingerprint ==
            lastPresenceFingerprint)
        {
            return;
        }

        presenceSyncInFlight = true;

        try
        {
            bool applied =
                await presenceProvider
                    .SetPresenceAsync(next);

            if (applied)
            {
                CurrentPresence =
                    next.Clone();

                lastPresenceFingerprint =
                    fingerprint;
            }
        }
        finally
        {
            presenceSyncInFlight = false;
        }
    }

    private string BuildCurrentSteamConnectString()
    {
        if (lobbyBridge == null ||
            !lobbyBridge.HasLobby ||
            lobbyBridge.CurrentSnapshot == null)
        {
            return string.Empty;
        }

        AtlasLobbySnapshot snapshot =
            lobbyBridge.CurrentSnapshot;

        bool safeJoinable =
            snapshot.LifecycleState ==
                AtlasRoomLifecycleState.Waiting &&
            snapshot.Visibility !=
                AtlasRoomVisibility.Private;

        if (!safeJoinable)
        {
            return string.Empty;
        }

        return AtlasBoardPlatformJoinPayload
            .BuildSteamConnectString(
                lobbyBridge.CurrentRoomCode);
    }

    private AtlasBoardPlatformPresence BuildPresence()
    {
        ResolveRuntimeBridges();

        if (lobbyBridge == null ||
            !lobbyBridge.HasLobby ||
            lobbyBridge.CurrentSnapshot == null)
        {
            return new AtlasBoardPlatformPresence
            {
                State =
                    AtlasBoardPlatformPresenceState.MainMenu
            };
        }

        AtlasLobbySnapshot snapshot =
            lobbyBridge.CurrentSnapshot;

        AtlasBoardPlatformPresenceState state =
            snapshot.LifecycleState ==
                AtlasRoomLifecycleState.Waiting
                ? AtlasBoardPlatformPresenceState.Lobby
                : snapshot.LifecycleState ==
                      AtlasRoomLifecycleState.Starting ||
                  snapshot.LifecycleState ==
                      AtlasRoomLifecycleState.InMatch
                    ? AtlasBoardPlatformPresenceState.Match
                    : AtlasBoardPlatformPresenceState.MainMenu;

        int connectedPlayers =
            snapshot.Members != null
                ? snapshot.Members.Count(
                    item =>
                        item != null &&
                        item.Active &&
                        item.IsHumanSeat)
                : 0;

        return new AtlasBoardPlatformPresence
        {
            State = state,
            MapId =
                snapshot.Settings != null
                    ? snapshot.Settings.MapId ?? string.Empty
                    : string.Empty,
            CurrentPlayers =
                Mathf.Max(0, connectedPlayers),
            MaxPlayers =
                snapshot.Settings != null
                    ? Mathf.Max(
                        1,
                        snapshot.Settings.MaxPlayers)
                    : AtlasOnlineDefaults.MaxPlayers,
            Joinable =
                snapshot.LifecycleState ==
                    AtlasRoomLifecycleState.Waiting &&
                snapshot.Visibility !=
                    AtlasRoomVisibility.Private
        };
    }

    private void HandleProviderJoinRequested(
        string payload)
    {
        if (!AtlasBoardPlatformJoinPayload.TryParse(
                payload,
                out string roomCode))
        {
            return;
        }

        PlatformJoinRequested?.Invoke(roomCode);

        if (!TryOpenJoinOverlay(roomCode))
        {
            pendingStartupJoinCode =
                roomCode;
        }
    }

    private bool TryOpenJoinOverlay(
        string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            return false;
        }

        AtlasBoardPrivateLobbyUIController[] controllers =
            Resources.FindObjectsOfTypeAll<
                AtlasBoardPrivateLobbyUIController>();

        AtlasBoardPrivateLobbyUIController controller =
            controllers.FirstOrDefault(
                item =>
                    item != null &&
                    item.gameObject.scene.IsValid());

        if (controller == null)
        {
            return false;
        }

        controller.ShowRoomEntryFromMainMenu();

        TMP_InputField[] inputs =
            Resources.FindObjectsOfTypeAll<
                TMP_InputField>();

        TMP_InputField codeInput =
            inputs.FirstOrDefault(
                item =>
                    item != null &&
                    item.gameObject.scene.IsValid() &&
                    item.name == "Input_JoinCode");

        if (codeInput == null)
        {
            return false;
        }

        codeInput.SetTextWithoutNotify(
            roomCode);

        codeInput.ForceLabelUpdate();

        return true;
    }

    private void CaptureStartupJoinPayload()
    {
        string[] args =
            Environment.GetCommandLineArgs();

        for (int index = 0;
             index < args.Length;
             index++)
        {
            string current =
                args[index] ?? string.Empty;

            if (current.StartsWith(
                    "-atlasJoinRoom=",
                    StringComparison.OrdinalIgnoreCase))
            {
                string payload =
                    current.Substring(
                        "-atlasJoinRoom=".Length);

                if (AtlasBoardPlatformJoinPayload.TryParse(
                        payload,
                        out string code))
                {
                    pendingStartupJoinCode =
                        code;
                    return;
                }
            }

            if (string.Equals(
                    current,
                    "+atlas_join",
                    StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length &&
                AtlasBoardPlatformJoinPayload.TryParse(
                    args[index + 1],
                    out string commandCode))
            {
                pendingStartupJoinCode =
                    commandCode;
                return;
            }
        }
    }

    private static AtlasRoomDescriptor BuildRoomDescriptor(
        AtlasBoardLobbyRuntimeBridge bridge)
    {
        if (bridge == null ||
            !bridge.HasLobby ||
            bridge.CurrentSnapshot == null)
        {
            return null;
        }

        AtlasLobbySnapshot snapshot =
            bridge.CurrentSnapshot;

        return new AtlasRoomDescriptor
        {
            SessionId =
                snapshot.LobbyId ?? string.Empty,
            RoomCode =
                bridge.CurrentRoomCode ?? string.Empty,
            HostAccountId =
                snapshot.HostAccountId ?? string.Empty,
            Visibility =
                snapshot.Visibility,
            LifecycleState =
                snapshot.LifecycleState,
            CrossplayMode =
                AtlasCrossplayPreference.Enabled
                    ? AtlasCrossplayMode.CrossPlatform
                    : AtlasCrossplayMode.SamePlatformOnly,
            MapId =
                snapshot.Settings != null
                    ? snapshot.Settings.MapId ?? string.Empty
                    : string.Empty,
            ThemeId =
                snapshot.Settings != null
                    ? snapshot.Settings.ThemeId ?? string.Empty
                    : string.Empty,
            RoundLimit =
                snapshot.Settings != null
                    ? snapshot.Settings.RoundLimit
                    : 20,
            MaxPlayers =
                snapshot.Settings != null
                    ? snapshot.Settings.MaxPlayers
                    : AtlasOnlineDefaults.MaxPlayers,
            MatchId =
                snapshot.MatchId ?? string.Empty,
            GameVersion =
                snapshot.GameVersion ?? string.Empty,
            ProtocolVersion =
                snapshot.ProtocolVersion,
            RulesVersion =
                snapshot.RulesVersion,
            ContentVersion =
                snapshot.ContentVersion ?? string.Empty,
            RegionId =
                snapshot.RegionId ?? string.Empty
        };
    }

    private static string ResolveDisplayName(
        AtlasLobbySnapshot snapshot,
        string accountId)
    {
        if (snapshot == null ||
            snapshot.Members == null ||
            string.IsNullOrWhiteSpace(accountId))
        {
            return "Atlas Player";
        }

        AtlasLobbyMemberSnapshot member =
            snapshot.Members.FirstOrDefault(
                item =>
                    item != null &&
                    string.Equals(
                        item.AccountId,
                        accountId,
                        StringComparison.OrdinalIgnoreCase));

        return member != null &&
               !string.IsNullOrWhiteSpace(
                   member.DisplayName)
            ? member.DisplayName.Trim()
            : "Atlas Player";
    }

    private static AtlasPlatformKind ResolveNativePlatform()
    {
        return Application.platform switch
        {
            RuntimePlatform.WindowsEditor =>
                AtlasPlatformKind.Windows,
            RuntimePlatform.WindowsPlayer =>
                AtlasPlatformKind.Windows,
            RuntimePlatform.OSXEditor =>
                AtlasPlatformKind.MacOS,
            RuntimePlatform.OSXPlayer =>
                AtlasPlatformKind.MacOS,
            RuntimePlatform.LinuxEditor =>
                AtlasPlatformKind.Linux,
            RuntimePlatform.LinuxPlayer =>
                AtlasPlatformKind.Linux,
            RuntimePlatform.Android =>
                AtlasPlatformKind.Android,
            RuntimePlatform.IPhonePlayer =>
                AtlasPlatformKind.IOS,
            _ =>
                AtlasPlatformKind.Unknown
        };
    }
}
