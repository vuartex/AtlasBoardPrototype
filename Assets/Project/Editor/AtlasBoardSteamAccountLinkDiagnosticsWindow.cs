#if UNITY_EDITOR
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public sealed class AtlasBoardSteamAccountLinkDiagnosticsWindow :
    EditorWindow
{
    private string statusMessage =
        "Ready.";

    private MessageType statusType =
        MessageType.Info;

    private bool operationInFlight;

    [MenuItem(
        "Atlas Board/Platform/Steam Account Link Diagnostics")]
    private static void Open()
    {
        GetWindow<
            AtlasBoardSteamAccountLinkDiagnosticsWindow>(
                "Steam Account Link");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "PHASE 11C STEAM ACCOUNT LINK",
            EditorStyles.boldLabel);

        EditorGUILayout.Space(8f);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode. Phase 11C uses the current authenticated " +
                "Atlas account and the active Steam provider.",
                MessageType.Info);

            return;
        }

        AtlasBoardSteamAccountLinkBridge bridge =
            AtlasBoardSteamAccountLinkBridge.Instance;

        AtlasBoardPlatformRuntime platform =
            AtlasBoardPlatformRuntime.Instance;

        if (bridge == null ||
            platform == null)
        {
            EditorGUILayout.HelpBox(
                "Steam account-link runtime is not ready yet.",
                MessageType.Warning);

            return;
        }

        EditorGUILayout.LabelField(
            "Steam Provider",
            platform.IsSteamProviderActive
                ? "ACTIVE"
                : "INACTIVE");

        EditorGUILayout.LabelField(
            "Steam Initialized",
            platform.SteamInitialized
                ? "YES"
                : "NO");

        EditorGUILayout.LabelField(
            "Current SteamID",
            platform.SteamId.ToString());

        EditorGUILayout.LabelField(
            "Current Steam AppID",
            platform.SteamAppId.ToString());

        EditorGUILayout.LabelField(
            "Local Emulators",
            bridge.UsingLocalEmulators
                ? "YES"
                : "NO");

        EditorGUILayout.Space(8f);

        DrawStatus(
            bridge.CurrentStatus);

        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(
                   operationInFlight))
        {
            if (GUILayout.Button(
                    "Refresh Link Status"))
            {
                _ =
                    RefreshAsync(
                        bridge);
            }

            using (new EditorGUI.DisabledScope(
                       !bridge.UsingLocalEmulators ||
                       !platform.SteamInitialized))
            {
                if (GUILayout.Button(
                        "DEV LINK CURRENT STEAM (EMULATOR)"))
                {
                    _ =
                        DevLinkAsync(
                            bridge);
                }
            }

            bool realVerifierReadyForTest =
                platform.SteamInitialized &&
                platform.SteamAppId != 480;

            using (new EditorGUI.DisabledScope(
                       !realVerifierReadyForTest))
            {
                if (GUILayout.Button(
                        "VERIFY + LINK CURRENT STEAM"))
                {
                    _ =
                        VerifiedLinkAsync(
                            bridge);
                }
            }
        }

        if (platform.SteamAppId == 480)
        {
            EditorGUILayout.HelpBox(
                "AppID 480 can test Steam identity/overlay, but AtlasBoard " +
                "does not own Spacewar's publisher verification setup. Use " +
                "the emulator DEV LINK proof now. Real verified linking is " +
                "enabled when the actual AtlasBoard Steam AppID and server " +
                "publisher key are configured.",
                MessageType.Info);
        }

        EditorGUILayout.Space(6f);

        EditorGUILayout.HelpBox(
            statusMessage,
            statusType);
    }

    private static void DrawStatus(
        AtlasBoardSteamLinkStatus status)
    {
        if (status == null)
        {
            EditorGUILayout.LabelField(
                "Link Status",
                "UNKNOWN");

            return;
        }

        EditorGUILayout.LabelField(
            "Link Status",
            status.Linked
                ? "LINKED"
                : "NOT LINKED");

        EditorGUILayout.LabelField(
            "Server Verified",
            status.Verified
                ? "YES"
                : "NO");

        EditorGUILayout.LabelField(
            "Development Only",
            status.DevelopmentOnly
                ? "YES"
                : "NO");

        EditorGUILayout.LabelField(
            "Linked Atlas AccountId",
            status.AccountId ?? string.Empty);

        EditorGUILayout.LabelField(
            "Linked SteamID",
            status.SteamId ?? string.Empty);

        EditorGUILayout.LabelField(
            "Verification Mode",
            status.VerificationMode ?? string.Empty);

        EditorGUILayout.LabelField(
            "Mapping Integrity",
            status.IntegrityOk
                ? "OK"
                : "UNKNOWN");
    }

    private async Task RefreshAsync(
        AtlasBoardSteamAccountLinkBridge bridge)
    {
        await RunAsync(
            "Steam link status refreshed.",
            () => bridge.RefreshStatusAsync());
    }

    private async Task DevLinkAsync(
        AtlasBoardSteamAccountLinkBridge bridge)
    {
        await RunAsync(
            "Emulator development link completed.",
            () => bridge.DevLinkCurrentSteamAsync());
    }

    private async Task VerifiedLinkAsync(
        AtlasBoardSteamAccountLinkBridge bridge)
    {
        await RunAsync(
            "Verified Steam account link completed.",
            () => bridge.VerifyAndLinkCurrentSteamAsync());
    }

    private async Task RunAsync(
        string successMessage,
        System.Func<
            Task<AtlasBoardSteamLinkStatus>> action)
    {
        if (operationInFlight)
        {
            return;
        }

        operationInFlight = true;
        statusMessage =
            "Working...";
        statusType =
            MessageType.Info;

        Repaint();

        try
        {
            AtlasBoardSteamLinkStatus status =
                await action();

            if (status != null &&
                status.Success)
            {
                statusMessage =
                    successMessage +
                    " Linked=" +
                    status.Linked +
                    ", Verified=" +
                    status.Verified +
                    ", Applied=" +
                    status.Applied +
                    ", Replay=" +
                    status.IdempotentReplay +
                    ".";

                statusType =
                    MessageType.Info;
            }
            else
            {
                statusMessage =
                    "Operation failed: " +
                    (status?.ErrorKey ??
                     "unknown") +
                    " | " +
                    (status?.TechnicalMessage ??
                     string.Empty);

                statusType =
                    MessageType.Warning;
            }
        }
        finally
        {
            operationInFlight = false;
            Repaint();
        }
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
