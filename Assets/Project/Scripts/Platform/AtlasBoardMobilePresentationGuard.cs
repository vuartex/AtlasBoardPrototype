using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mobile-only display safety layer. The existing desktop Settings controller
/// may still request a window resolution when audio settings are applied.
/// This guard restores the actual native landscape viewport and scales
/// screen-space UI to leave room for notches and gesture/navigation insets.
/// Never used on Editor, Windows or Steam.
/// </summary>
public sealed class AtlasBoardMobilePresentationGuard : MonoBehaviour
{
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    private static AtlasBoardMobilePresentationGuard instance;
    private readonly HashSet<CanvasScaler> configuredCanvases = new HashSet<CanvasScaler>();
    private int nativeLandscapeWidth;
    private int nativeLandscapeHeight;
    private int correctionAttempts;
    private float nextCheck;
    private bool startupLogged;
    private bool mobileGraphicsControlsLogged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var host = new GameObject("AtlasBoard_Mobile_Presentation_Guard");
        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardMobilePresentationGuard>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        Resolution physical = Screen.currentResolution;
        nativeLandscapeWidth = Mathf.Max(physical.width, physical.height);
        nativeLandscapeHeight = Mathf.Min(physical.width, physical.height);
        if (nativeLandscapeWidth < 400 || nativeLandscapeHeight < 240)
        {
            nativeLandscapeWidth = Mathf.Max(Screen.width, Screen.height);
            nativeLandscapeHeight = Mathf.Min(Screen.width, Screen.height);
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.65f;
        if (!startupLogged)
        {
            startupLogged = true;
            Debug.Log("ATB_MOBILE_VIEWPORT_START native=" + nativeLandscapeWidth + "x" +
                nativeLandscapeHeight + " screen=" + Screen.width + "x" + Screen.height +
                " safeArea=" + Screen.safeArea);
        }
        RepairResolution();
        if (QualitySettings.pixelLightCount > 1)
            QualitySettings.pixelLightCount = 1;
        FitCanvases();
        DisableDesktopOnlyResolutionControls();
    }

    private void RepairResolution()
    {
        // Android device size is OS-owned. A stored desktop resolution such as
        // 1080x2400 must never put a landscape touch UI into portrait pixels.
        bool portraitPixels = Screen.height > Screen.width;
        float actualAspect = (float)Mathf.Max(1, Screen.width) /
            Mathf.Max(1, Screen.height);
        float nativeAspect = (float)Mathf.Max(1, nativeLandscapeWidth) /
            Mathf.Max(1, nativeLandscapeHeight);
        bool wrongAspect = Mathf.Abs(actualAspect - nativeAspect) > 0.09f;
        bool implausibleSize = Screen.width < nativeLandscapeWidth * 0.67f ||
            Screen.height < nativeLandscapeHeight * 0.67f;
        if ((!portraitPixels && !wrongAspect && !implausibleSize) ||
            correctionAttempts >= 4)
            return;
        correctionAttempts++;
        Debug.LogWarning("ATB_MOBILE_RESOLUTION_REPAIR attempt=" + correctionAttempts +
            " observed=" + Screen.width + "x" + Screen.height +
            " requestedNative=" + nativeLandscapeWidth + "x" + nativeLandscapeHeight);
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        Screen.SetResolution(nativeLandscapeWidth, nativeLandscapeHeight,
            FullScreenMode.FullScreenWindow);
    }

    private void FitCanvases()
    {
        Rect safe = Screen.safeArea;
        float screenH = Mathf.Max(1f, Screen.height);
        float safeH = safe.height > screenH * 0.5f ? safe.height : screenH;
        // Reserve a further 4% vertical breathing room, beyond OS safe area.
        float referenceHeight = Mathf.Clamp(1200f * screenH / safeH * 1.04f,
            1248f, 1480f);
        CanvasScaler[] all = UnityEngine.Object.FindObjectsByType<CanvasScaler>(
            FindObjectsInactive.Include);
        foreach (CanvasScaler scaler in all)
        {
            if (scaler == null || !scaler.gameObject.scene.IsValid()) continue;
            Canvas canvas = scaler.GetComponent<Canvas>();
            if (canvas == null || !canvas.isRootCanvas ||
                canvas.renderMode == RenderMode.WorldSpace) continue;
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand ||
                Mathf.Abs(scaler.referenceResolution.y - referenceHeight) > 1f ||
                Mathf.Abs(scaler.referenceResolution.x - 1920f) > 1f)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                scaler.referenceResolution = new Vector2(1920f, referenceHeight);
            }
            if (configuredCanvases.Add(scaler))
                Debug.Log("ATB_MOBILE_CANVAS_FIT name=" + scaler.gameObject.name +
                    " ref=1920x" + referenceHeight.ToString("F0") +
                    " safe=" + safe + " device=" + Screen.width + "x" + Screen.height);
        }
    }

    private void DisableDesktopOnlyResolutionControls()
    {
        AtlasBoardSettingsV2Controller controller =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardSettingsV2Controller>(
                FindObjectsInactive.Include);
        if (controller == null) return;
        DisableDropdown(controller, "resolutionDropdown");
        DisableDropdown(controller, "displayModeDropdown");
        if (!mobileGraphicsControlsLogged)
        {
            mobileGraphicsControlsLogged = true;
            Debug.Log("ATB_MOBILE_GRAPHICS OS_OWNS_RESOLUTION desktop_dropdowns_disabled");
        }
    }

    private static void DisableDropdown(object controller, string name)
    {
        FieldInfo info = controller.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        TMP_Dropdown field = info == null ? null : info.GetValue(controller) as TMP_Dropdown;
        if (field != null && field.interactable) field.interactable = false;
    }
#endif
}
