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
