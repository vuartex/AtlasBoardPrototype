using System.Threading.Tasks;
using UnityEngine.Networking;

/// <summary>
/// Phase 15C transport seam for Firebase App Check.
///
/// Current behavior is intentionally non-breaking:
/// - If Firebase App Check SDK is absent, no header is added.
/// - If the SDK is present but no token is available, no header is added.
/// - If a token is available, X-Firebase-AppCheck is attached.
///
/// Cloud Functions App Check enforcement MUST remain disabled until the
/// Windows/Steam production provider is implemented and validated.
/// </summary>
public static class AtlasBoardAppCheckRequestSecurity
{
    public static int RequestsSeen { get; private set; }
    public static int HeadersAttached { get; private set; }
    public static int SdkUnavailableSkips { get; private set; }
    public static int TokenUnavailableSkips { get; private set; }

    public static async Task<bool> ApplyAsync(
        UnityWebRequest request,
        bool forceRefresh = false)
    {
        if (request == null)
        {
            return false;
        }

        RequestsSeen++;

        if (!AtlasBoardAppCheckTokenSource.IsSdkAvailable)
        {
            SdkUnavailableSkips++;
            return false;
        }

        string token =
            await AtlasBoardAppCheckTokenSource.GetTokenAsync(
                forceRefresh);

        if (string.IsNullOrWhiteSpace(token))
        {
            TokenUnavailableSkips++;
            return false;
        }

        request.SetRequestHeader(
            AtlasBoardAppCheckTokenSource.RequestHeaderName,
            token);

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
