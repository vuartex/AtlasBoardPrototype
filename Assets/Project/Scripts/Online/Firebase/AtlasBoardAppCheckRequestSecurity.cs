using System;
using System.Threading.Tasks;
using UnityEngine.Networking;

/// <summary>
/// Central App Check transport seam. Protected callable enforcement is staged
/// by the backend ATLAS_ENFORCE_APP_CHECK BooleanParam. Emulator-only dev
/// callables and the Steam token bootstrap remain explicit exceptions.
/// </summary>
public static class AtlasBoardAppCheckRequestSecurity
{
    public static int RequestsSeen { get; private set; }
    public static int HeadersAttached { get; private set; }
    public static int SdkUnavailableSkips { get; private set; }
    public static int TokenUnavailableSkips { get; private set; }

    public static async Task<bool> ApplyAsync(UnityWebRequest request, bool forceRefresh = false)
    {
        if (request == null) return false;
        RequestsSeen++;

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        // Phase 16 local Guest QA only: the Functions Emulator accepts
        // non-empty App Check headers without cryptographic verification.
        // Do not wait for production Steam App Check when the explicitly
        // launched Guest is addressing the loopback Functions Emulator.
        // This is NOT a production token and NOT a security acceptance proof.
        if (IsExplicitLocalGuestCallable(request.url))
        {
            request.SetRequestHeader(
                AtlasBoardAppCheckTokenSource.RequestHeaderName,
                "atlasboard-local-emulator-development-only");
            HeadersAttached++;
            return true;
        }
#endif

        if (!AtlasBoardAppCheckTokenSource.IsSdkAvailable)
        {
            SdkUnavailableSkips++;
            return false;
        }

        string token = await AtlasBoardAppCheckTokenSource.GetTokenAsync(forceRefresh);
        if (string.IsNullOrWhiteSpace(token))
        {
            TokenUnavailableSkips++;
            return false;
        }

        request.SetRequestHeader(AtlasBoardAppCheckTokenSource.RequestHeaderName, token);
        HeadersAttached++;
        return true;
    }

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
    private static bool IsExplicitLocalGuestCallable(string url)
    {
        bool localEmulatorRequested = false;
        foreach (string arg in Environment.GetCommandLineArgs())
        {
            if (string.Equals(
                arg,
                "-atlasLocalEmulators",
                StringComparison.OrdinalIgnoreCase))
            {
                localEmulatorRequested = true;
                break;
            }
        }

        if (!localEmulatorRequested ||
            !Uri.TryCreate(url, UriKind.Absolute, out Uri endpoint))
        {
            return false;
        }

        // Do not authorize token substitution for LAN/remote emulators,
        // non-callable paths, HTTPS/cloud endpoints or unexpected ports.
        bool loopback =
            string.Equals(endpoint.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase);

        return loopback &&
            endpoint.Scheme == Uri.UriSchemeHttp &&
            endpoint.Port == 5001 &&
            endpoint.AbsolutePath.StartsWith(
                "/atlasboard-usa/europe-west1/",
                StringComparison.Ordinal) &&
            endpoint.AbsolutePath.Length >
                "/atlasboard-usa/europe-west1/".Length;
    }
#endif

#if UNITY_EDITOR
    public static void EditorResetDiagnostics()
    {
        RequestsSeen = 0;
        HeadersAttached = 0;
        SdkUnavailableSkips = 0;
        TokenUnavailableSkips = 0;
    }
#endif
}
