#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase14BNetworkTelemetryInstaller
{
    private const string BridgePath =
        "Assets/Project/Scripts/Online/Firebase/" +
        "AtlasBoardMatchRuntimeBridge.cs";

    private const string Marker =
        "AtlasBoardNetworkTelemetry.Report(";

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14B - Network Quality/1 - Apply Network Telemetry Instrumentation")]
    public static void ApplyInstrumentation()
    {
        if (!File.Exists(
                BridgePath))
        {
            Debug.LogError(
                "Phase 14B: AtlasBoardMatchRuntimeBridge.cs " +
                $"was not found at {BridgePath}.");

            return;
        }

        string source =
            File.ReadAllText(
                BridgePath);

        if (source.Contains(
                Marker,
                StringComparison.Ordinal))
        {
            Debug.Log(
                "Phase 14B: network telemetry instrumentation " +
                "is already applied.");

            return;
        }

        string patched =
            source;

        try
        {
            patched =
                ReplaceExactlyOnce(
                    patched,
                    @"        if (lobbyBridge == null)
        {
            return AtlasMatchNetworkResult.Fail(
                ""match.error.service_unavailable"",
                ""Lobby runtime bridge is missing."");
        }",
                    @"        if (lobbyBridge == null)
        {
            AtlasBoardNetworkTelemetry
                .ReportUnavailable(
                    functionName);

            return AtlasMatchNetworkResult.Fail(
                ""match.error.service_unavailable"",
                ""Lobby runtime bridge is missing."");
        }",
                    "missing lobby bridge telemetry");

            patched =
                ReplaceExactlyOnce(
                    patched,
                    @"        if (!identityReady ||
            string.IsNullOrWhiteSpace(
                lobbyBridge
                    .AuthTokenForOnlineSubsystems))
        {
            return AtlasMatchNetworkResult.Fail(
                ""account.error.authentication_required"",
                ""Online identity is unavailable."");
        }",
                    @"        if (!identityReady ||
            string.IsNullOrWhiteSpace(
                lobbyBridge
                    .AuthTokenForOnlineSubsystems))
        {
            AtlasBoardNetworkTelemetry
                .ReportUnavailable(
                    functionName);

            return AtlasMatchNetworkResult.Fail(
                ""account.error.authentication_required"",
                ""Online identity is unavailable."");
        }",
                    "identity-unavailable telemetry");

            patched =
                ReplaceExactlyOnce(
                    patched,
                    @"        UnityWebRequestAsyncOperation operation =
            request.SendWebRequest();",
                    @"        double requestStartedAt =
            Time.realtimeSinceStartupAsDouble;

        UnityWebRequestAsyncOperation operation =
            request.SendWebRequest();",
                    "request start timestamp");

            patched =
                ReplaceExactlyOnce(
                    patched,
                    @"        await completion.Task;

        string body =",
                    @"        await completion.Task;

        float requestElapsedMs =
            (float)(
                (Time.realtimeSinceStartupAsDouble -
                 requestStartedAt) *
                1000.0);

        string body =",
                    "request elapsed timestamp");

            patched =
                ReplaceExactlyOnce(
                    patched,
                    @"            return AtlasMatchNetworkResult.Fail(
                key,
                technical);
        }

        AtlasMatchNetworkResult result =
            parser(body);",
                    @"            AtlasBoardNetworkTelemetry.Report(
                functionName,
                false,
                requestElapsedMs);

            return AtlasMatchNetworkResult.Fail(
                key,
                technical);
        }

        AtlasMatchNetworkResult result =
            parser(body);

        AtlasBoardNetworkTelemetry.Report(
            functionName,
            result.Success,
            requestElapsedMs);",
                    "request result telemetry");
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogError(
                "Phase 14B instrumentation was NOT applied. " +
                exception.Message);

            return;
        }

        File.WriteAllText(
            BridgePath,
            patched,
            new UTF8Encoding(
                false));

        AssetDatabase.ImportAsset(
            BridgePath,
            ImportAssetOptions.ForceUpdate);

        AssetDatabase.Refresh();

        Debug.Log(
            "Phase 14B network telemetry instrumentation APPLIED. " +
            "Existing Firebase snapshot traffic will now feed the " +
            "Network Quality HUD; no additional ping callable was added.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14B - Network Quality/2 - Validate Network Quality HUD Source")]
    public static void ValidateSource()
    {
        if (!File.Exists(
                BridgePath))
        {
            Debug.LogError(
                "Phase 14B validation FAILED: match runtime bridge missing.");

            return;
        }

        string source =
            File.ReadAllText(
                BridgePath);

        string[] requiredMarkers =
        {
            "AtlasBoardNetworkTelemetry",
            "requestStartedAt",
            "requestElapsedMs",
            "ReportUnavailable(",
            "result.Success,"
        };

        foreach (string required
                 in requiredMarkers)
        {
            if (!source.Contains(
                    required,
                    StringComparison.Ordinal))
            {
                Debug.LogError(
                    "Phase 14B validation FAILED. Missing bridge marker: " +
                    required);

                return;
            }
        }

        Debug.Log(
            "Phase 14B source validation PASS. " +
            "AtlasBoardMatchRuntimeBridge is instrumented and " +
            "the HUD will use existing matchGetSnapshot traffic.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14B - Network Quality/3 - Log Live Network HUD State (Play Mode)")]
    public static void LogLiveHudState()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Enter Play Mode and start an online match first.");

            return;
        }

        AtlasBoardNetworkQualityHud hud =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardNetworkQualityHud>();

        if (hud == null)
        {
            Debug.LogError(
                "Phase 14B runtime HUD controller was not found.");

            return;
        }

        Debug.Log(
            "Phase 14B runtime HUD controller is active. " +
            "Use the visible top-right network cluster for live values.");
    }

    private static string ReplaceExactlyOnce(
        string source,
        string oldValue,
        string newValue,
        string operationName)
    {
        int firstIndex =
            source.IndexOf(
                oldValue,
                StringComparison.Ordinal);

        if (firstIndex < 0)
        {
            throw new InvalidOperationException(
                $"Could not find the expected source block for " +
                $"'{operationName}'. GitHub/local source may have changed.");
        }

        int secondIndex =
            source.IndexOf(
                oldValue,
                firstIndex +
                oldValue.Length,
                StringComparison.Ordinal);

        if (secondIndex >= 0)
        {
            throw new InvalidOperationException(
                $"Expected exactly one source block for " +
                $"'{operationName}', but found more than one.");
        }

        return source.Substring(
                   0,
                   firstIndex) +
               newValue +
               source.Substring(
                   firstIndex +
                   oldValue.Length);
    }
}
#endif
