using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// On mobile, never show the same startup failure simultaneously as a blocking
/// progress overlay and a top-right toast. Throttle identical subsequent
/// toasts, but ALWAYS preserve error reporting and the underlying blocking
/// status and retry choices. Does not masquerade as a successful server call.
/// </summary>
public sealed class AtlasBoardMobileErrorPresentation : MonoBehaviour
{
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    private static AtlasBoardMobileErrorPresentation instance;
    private readonly Dictionary<string, float> recentlyDisplayed =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private string pendingCode;
    private bool suppressRepeated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var host = new GameObject("AtlasBoard_Mobile_Error_Presentation");
        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardMobileErrorPresentation>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        AtlasBoardFrontendErrorCodes.PublicCodeRaised += OnCodeReported;
    }

    private void OnCodeReported(string code, string _)
    {
        if (string.IsNullOrEmpty(code)) return;
        pendingCode = code;
        float previous;
        suppressRepeated = recentlyDisplayed.TryGetValue(code, out previous) &&
            Time.unscaledTime - previous < 75f;
        if (!suppressRepeated)
            recentlyDisplayed[code] = Time.unscaledTime;
    }

    private void LateUpdate()
    {
        if (string.IsNullOrEmpty(pendingCode)) return;
        string code = pendingCode;
        pendingCode = null;
        AtlasBoardLoadingExperience loader =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardLoadingExperience>(
                FindObjectsInactive.Include);
        if (loader == null) return;
        GameObject blocker = ReadField(loader, "blocker");
        GameObject toast = ReadField(loader, "toastRoot");
        if (toast == null || !toast.activeSelf) return;
        bool blockingOverlayVisible = blocker != null && blocker.activeSelf;
        // The fullscreen status remains visible when duplicate top toast hides.
        if (suppressRepeated || blockingOverlayVisible)
        {
            toast.SetActive(false);
            Debug.Log("ATB_MOBILE_REDUNDANT_TOAST_SUPPRESSED code=" + code +
                " reason=" + (blockingOverlayVisible ? "blocking_overlay_visible" :
                    "repeat_within_75s"));
        }
    }

    private static GameObject ReadField(Component target, string name)
    {
        FieldInfo field = target.GetType().GetField(name,
            BindingFlags.NonPublic | BindingFlags.Instance);
        return field == null ? null : field.GetValue(target) as GameObject;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        AtlasBoardFrontendErrorCodes.PublicCodeRaised -= OnCodeReported;
        instance = null;
    }
#endif
}
