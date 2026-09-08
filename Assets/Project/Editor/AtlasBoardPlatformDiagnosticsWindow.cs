#if UNITY_EDITOR
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public sealed class AtlasBoardPlatformDiagnosticsWindow :
    EditorWindow
{
    private string roomCode = "482731";

    private string statusMessage =
        "Ready.";

    private MessageType statusType =
        MessageType.Info;

    private bool achievementSyncInFlight;

    [MenuItem(
        "Atlas Board/Platform/Phase 11 Diagnostics")]
    private static void Open()
    {
        GetWindow<
            AtlasBoardPlatformDiagnosticsWindow>(
                "Atlas Platform");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "PHASE 11 PLATFORM DIAGNOSTICS",
            EditorStyles.boldLabel);

        EditorGUILayout.Space(8f);

        DrawSteamEditorSettings();

        EditorGUILayout.Space(8f);

        AtlasBoardPlatformRuntime runtime =
            Application.isPlaying
                ? AtlasBoardPlatformRuntime.Instance
                : null;

        if (runtime == null)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to inspect the runtime platform adapter. " +
                "If you changed the Steam-provider toggle/AppID, enter Play " +
                "Mode again so the provider is recreated.",
                MessageType.Info);
        }
        else
        {
            DrawRuntime(runtime);
        }

        EditorGUILayout.Space(12f);

        if (GUILayout.Button(
                "Run Phase 11A Static Validation"))
        {
            AtlasBoardPlatformPhase11Validator.Validate();

            statusMessage =
                "Phase 11A static validation completed. " +
                "Check Console for PASS 7/7.";

            statusType =
                MessageType.Info;
        }

        if (GUILayout.Button(
                "Run Phase 11B Steam Validation"))
        {
            AtlasBoardSteamPhase11BValidator.Validate();

            statusMessage =
                "Phase 11B static validation completed. " +
                "Check Console for PASS 8/8.";

            statusType =
                MessageType.Info;
        }
    }

    private void DrawSteamEditorSettings()
    {
        EditorGUILayout.LabelField(
            "STEAM EDITOR TEST SETTINGS",
            EditorStyles.boldLabel);

        bool useSteam =
            AtlasBoardSteamRuntimeSettings
                .UseSteamInEditor;

        bool nextUseSteam =
            EditorGUILayout.Toggle(
                "Use Steam Provider in Editor",
                useSteam);

        int appId =
            (int)AtlasBoardSteamRuntimeSettings
                .DevelopmentAppId;

        int nextAppId =
            EditorGUILayout.IntField(
                "Development Steam AppID",
                appId);

        if (nextUseSteam != useSteam ||
            nextAppId != appId)
        {
            AtlasBoardSteamRuntimeSettings
                .UseSteamInEditor =
                nextUseSteam;

            AtlasBoardSteamRuntimeSettings
                .DevelopmentAppId =
                nextAppId > 0
                    ? (uint)nextAppId
                    : AtlasBoardSteamRuntimeSettings
                        .DefaultDevelopmentAppId;

            EnsureDevelopmentAppIdFile();

            statusMessage =
                "Steam editor settings saved. Exit/re-enter Play Mode " +
                "to recreate the platform provider.";

            statusType =
                MessageType.Info;
        }

        EditorGUILayout.LabelField(
            "Steamworks.NET",
            AtlasBoardSteamPlatformAdapter
                .IsSteamworksCompiled
                ? "compiled"
                : "NOT compiled");

        EditorGUILayout.LabelField(
            "Package target",
            "2025.164.1");

        if (AtlasBoardSteamRuntimeSettings
                .DevelopmentAppId == 480)
        {
            EditorGUILayout.HelpBox(
                "AppID 480 (Spacewar) is suitable for Steam identity, " +
                "overlay and Rich Presence development tests. AtlasBoard's " +
                "14 custom achievement API names will not exist in Spacewar; " +
                "achievement projection needs your real Steam AppID with " +
                "those achievements published.",
                MessageType.Info);
        }
    }

    private void DrawRuntime(
        AtlasBoardPlatformRuntime runtime)
    {
        AtlasPlayerIdentity identity =
            runtime.CurrentPlatformIdentity;

        EditorGUILayout.LabelField(
            "Provider",
            runtime.ActiveProviderId);

        EditorGUILayout.LabelField(
            "Platform",
            runtime.ActivePlatform.ToString());

        EditorGUILayout.LabelField(
            "Canonical AccountId",
            identity != null
                ? identity.AccountId
                : string.Empty);

        EditorGUILayout.LabelField(
            "Platform User Id",
            identity != null
                ? identity.PlatformUserId
                : string.Empty);

        EditorGUILayout.LabelField(
            "Presence",
            runtime.CurrentPresence.State.ToString());

        if (runtime.IsSteamProviderActive)
        {
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField(
                "Steam Initialized",
                runtime.SteamInitialized
                    ? "YES"
                    : "NO");

            EditorGUILayout.LabelField(
                "Steam AppID",
                runtime.SteamAppId.ToString());

            EditorGUILayout.LabelField(
                "SteamID",
                runtime.SteamId.ToString());

            EditorGUILayout.LabelField(
                "Steam Persona",
                runtime.SteamPersonaName);

            EditorGUILayout.LabelField(
                "Steam Achievements Projected",
                runtime
                    .SteamProjectedAchievementCount
                    .ToString());

            if (!string.IsNullOrWhiteSpace(
                    runtime.SteamLastError))
            {
                EditorGUILayout.HelpBox(
                    runtime.SteamLastError,
                    MessageType.Warning);
            }

            if (GUILayout.Button(
                    "Open Steam Invite Dialog"))
            {
                _ =
                    OpenSteamInviteAsync(runtime);
            }
        }
        else
        {
            EditorGUILayout.Space(4f);

            roomCode =
                EditorGUILayout.TextField(
                    "Simulated Room Code",
                    roomCode);

            if (GUILayout.Button(
                    "Simulate Incoming Platform Invite"))
            {
                bool accepted =
                    runtime
                        .DevelopmentSimulateIncomingInvite(
                            roomCode);

                statusMessage =
                    accepted
                        ? "Incoming invite accepted. The existing room-entry " +
                          "flow should open with the room code pre-filled."
                        : "Invite payload was rejected or the development " +
                          "adapter is unavailable.";

                statusType =
                    accepted
                        ? MessageType.Info
                        : MessageType.Warning;
            }

            EditorGUILayout.LabelField(
                "Projected Achievements",
                runtime
                    .DevelopmentProjectedAchievementCount
                    .ToString());
        }

        EditorGUILayout.Space(6f);

        using (new EditorGUI.DisabledScope(
                   achievementSyncInFlight))
        {
            if (GUILayout.Button(
                    achievementSyncInFlight
                        ? "Syncing Achievements..."
                        : "Sync Achievements Now"))
            {
                _ =
                    SyncAchievementsAsync(
                        runtime);
            }
        }

        EditorGUILayout.HelpBox(
            statusMessage,
            statusType);
    }

    private async Task OpenSteamInviteAsync(
        AtlasBoardPlatformRuntime runtime)
    {
        bool opened =
            await runtime
                .ShowInviteUiAsync();

        statusMessage =
            opened
                ? "Steam invite dialog requested with the current room connect string."
                : "Steam invite dialog was not opened. Create/join a waiting " +
                  "room first and make sure Steam initialized.";

        statusType =
            opened
                ? MessageType.Info
                : MessageType.Warning;

        Repaint();
    }

    private async Task SyncAchievementsAsync(
        AtlasBoardPlatformRuntime runtime)
    {
        if (runtime == null ||
            achievementSyncInFlight)
        {
            return;
        }

        achievementSyncInFlight = true;

        statusMessage =
            "Reading canonical Phase 10 achievement state...";

        statusType =
            MessageType.Info;

        Repaint();

        try
        {
            AtlasBoardAchievementProjectionResult result =
                await runtime
                    .SynchronizeAchievementsNowAsync();

            if (result == null)
            {
                statusMessage =
                    "Achievement sync returned no result.";

                statusType =
                    MessageType.Warning;

                return;
            }

            if (!result.Success)
            {
                statusMessage =
                    "Achievement sync could not complete: " +
                    (string.IsNullOrWhiteSpace(
                         result.TechnicalMessage)
                        ? "unknown reason"
                        : result.TechnicalMessage);

                statusType =
                    MessageType.Warning;

                Debug.LogWarning(
                    "Phase 11 diagnostics achievement sync failed: " +
                    result.TechnicalMessage);

                return;
            }

            statusMessage =
                result.RequestedCount == 0
                    ? "Achievement sync completed. This account currently " +
                      "has 0 unlocked canonical achievements."
                    : "Achievement sync completed. Requested=" +
                      result.RequestedCount +
                      ", Newly projected=" +
                      result.NewlyProjectedCount +
                      ", Total projected=" +
                      result.TotalProjectedCount +
                      ".";

            statusType =
                MessageType.Info;

            Debug.Log(
                "Phase 11 diagnostics achievement sync PASS. " +
                "Requested=" +
                result.RequestedCount +
                ", NewlyProjected=" +
                result.NewlyProjectedCount +
                ", TotalProjected=" +
                result.TotalProjectedCount +
                ".");
        }
        finally
        {
            achievementSyncInFlight = false;
            Repaint();
        }
    }

    private static void EnsureDevelopmentAppIdFile()
    {
        string projectRoot =
            Directory.GetParent(
                    Application.dataPath)
                ?.FullName;

        if (string.IsNullOrWhiteSpace(
                projectRoot))
        {
            return;
        }

        string path =
            Path.Combine(
                projectRoot,
                "steam_appid.txt");

        File.WriteAllText(
            path,
            AtlasBoardSteamRuntimeSettings
                .DevelopmentAppId
                .ToString());
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            Repaint();
        }
    }
}
#endif
