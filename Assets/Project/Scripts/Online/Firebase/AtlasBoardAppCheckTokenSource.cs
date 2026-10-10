using System;
using System.Threading.Tasks;
using Firebase.AppCheck;
using UnityEngine;

public static class AtlasBoardAppCheckTokenSource
{
    public const string RequestHeaderName =
        "X-Firebase-AppCheck";

    public const string DebugTokenEnvironmentVariable =
        "ATLAS_APP_CHECK_DEBUG_TOKEN";

    private static bool debugProviderConfigured;
    private static bool steamProviderConfigured;
    private static string configurationMessage =
        string.Empty;

    public static bool IsSdkAvailable =>
        true;

    public static bool DebugProviderConfigured =>
        debugProviderConfigured;

    public static bool SteamProviderConfigured =>
        steamProviderConfigured;

    public static string ConfigurationMessage =>
        configurationMessage;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ConfigureAtSubsystemRegistration()
    {
        debugProviderConfigured =
            false;

        steamProviderConfigured =
            false;

        configurationMessage =
            string.Empty;

        ConfigureProviderForCurrentBuild();
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureBeforeSceneLoad()
    {
        if (!debugProviderConfigured &&
            !steamProviderConfigured)
        {
            ConfigureProviderForCurrentBuild();
        }
    }

    public static bool TryConfigureDebugProviderFromEnvironment(
        out string message)
    {
#if UNITY_EDITOR
        if (debugProviderConfigured)
        {
            message =
                configurationMessage;

            return true;
        }

        string debugToken =
            ResolveDebugToken(
                out string debugTokenSource);

        if (string.IsNullOrWhiteSpace(
                debugToken))
        {
            configurationMessage =
                "Firebase App Check SDK is present, but no debug token was " +
                $"found in {DebugTokenEnvironmentVariable} " +
                "(process or Windows User environment).";

            message =
                configurationMessage;

            return false;
        }

        try
        {
            DebugAppCheckProviderFactory.Instance
                .SetDebugToken(
                    debugToken.Trim());

            FirebaseAppCheck
                .SetAppCheckProviderFactory(
                    DebugAppCheckProviderFactory.Instance);

            debugProviderConfigured =
                true;

            steamProviderConfigured =
                false;

            configurationMessage =
                "Firebase App Check debug provider configured before " +
                "Firebase initialization. " +
                $"TokenSource={debugTokenSource}. " +
                "Debug token value was not logged.";

            message =
                configurationMessage;

            Debug.Log(
                "Phase 15E App Check EDITOR provider READY. " +
                configurationMessage);

            return true;
        }
        catch (Exception exception)
        {
            debugProviderConfigured =
                false;

            configurationMessage =
                "Firebase App Check Editor debug-provider configuration " +
                "failed: " +
                (exception != null
                    ? exception.ToString()
                    : "unknown error");

            message =
                configurationMessage;

            Debug.LogWarning(
                "Phase 15E App Check Editor bootstrap failed. " +
                configurationMessage);

            return false;
        }
#else
        configurationMessage =
            "Debug App Check provider is Editor-only in Phase 15E.";

        message =
            configurationMessage;

        return false;
#endif
    }

    public static async Task<string> GetTokenAsync(
        bool forceRefresh = false)
    {
        if (!debugProviderConfigured &&
            !steamProviderConfigured)
        {
            ConfigureProviderForCurrentBuild();
        }

        if (!debugProviderConfigured &&
            !steamProviderConfigured)
        {
            return string.Empty;
        }

        try
        {
            AppCheckToken token =
                await FirebaseAppCheck
                    .DefaultInstance
                    .GetAppCheckTokenAsync(
                        forceRefresh);

            return !string.IsNullOrWhiteSpace(
                    token.Token)
                ? token.Token
                : string.Empty;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "Phase 15E could not acquire a Firebase App Check token. " +
                (exception != null
                    ? exception.ToString()
                    : "Unknown error."));

            return string.Empty;
        }
    }

    private static void ConfigureProviderForCurrentBuild()
    {
#if UNITY_EDITOR
        TryConfigureDebugProviderFromEnvironment(
            out _);
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
        try
        {
            FirebaseAppCheck
                .SetAppCheckProviderFactory(
                    new AtlasBoardSteamAppCheckProviderFactory());

            steamProviderConfigured =
                true;

            debugProviderConfigured =
                false;

            configurationMessage =
                "Steam custom App Check provider configured for desktop " +
                "standalone build.";

            Debug.Log(
                "Phase 15E App Check STEAM provider READY. " +
                "Steam Web API attestation will be requested when Firebase " +
                "needs an App Check token.");
        }
        catch (Exception exception)
        {
            steamProviderConfigured =
                false;

            configurationMessage =
                "Steam custom App Check provider configuration failed: " +
                (exception != null
                    ? exception.ToString()
                    : "unknown error");

            Debug.LogError(
                "Phase 15E App Check Steam bootstrap failed. " +
                configurationMessage);
        }
#else
        configurationMessage =
            "Phase 15E has no production App Check provider configured for " +
            "this platform yet.";

        Debug.LogWarning(
            configurationMessage);
#endif
    }

    private static string ResolveDebugToken(
        out string source)
    {
        source =
            "None";

        string processValue =
            Environment.GetEnvironmentVariable(
                DebugTokenEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(
                processValue))
        {
            source =
                "ProcessEnvironment";

            return processValue.Trim();
        }

#if UNITY_EDITOR_WIN
        try
        {
            string userValue =
                Environment.GetEnvironmentVariable(
                    DebugTokenEnvironmentVariable,
                    EnvironmentVariableTarget.User);

            if (!string.IsNullOrWhiteSpace(
                    userValue))
            {
                source =
                    "WindowsUserEnvironment";

                return userValue.Trim();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "Phase 15E could not read Windows User-level App Check " +
                "debug-token environment variable. " +
                exception.Message);
        }
#endif

        return string.Empty;
    }
}
