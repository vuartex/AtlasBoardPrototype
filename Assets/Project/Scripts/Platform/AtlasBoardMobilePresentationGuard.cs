using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mobile-only display safety layer.
///
/// Android/iOS own the physical fullscreen resolution. AtlasBoard must not
/// replay a desktop/portrait resolution from saved settings when the player
/// presses Apply. This component therefore keeps the mobile settings model on
/// the active landscape viewport, disables desktop-only display controls and
/// scales root canvases with a modest safe margin.
///
/// Desktop/Editor/Steam behavior is untouched.
/// </summary>
public sealed class AtlasBoardMobilePresentationGuard : MonoBehaviour
{
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    private static AtlasBoardMobilePresentationGuard instance;
    private readonly HashSet<CanvasScaler> configuredCanvases =
        new HashSet<CanvasScaler>();

    private int nativeLandscapeWidth;
    private int nativeLandscapeHeight;
    private float nextCheck;
    private bool startupLogged;
    private bool mobileGraphicsControlsLogged;
    private bool resolutionMismatchLogged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject host =
            new GameObject("AtlasBoard_Mobile_Presentation_Guard");

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
        if (Time.unscaledTime < nextCheck)
            return;

        nextCheck = Time.unscaledTime + 0.65f;

        if (!startupLogged)
        {
            startupLogged = true;
            Debug.Log(
                "ATB_MOBILE_VIEWPORT_START native=" +
                nativeLandscapeWidth + "x" + nativeLandscapeHeight +
                " screen=" + Screen.width + "x" + Screen.height +
                " safeArea=" + Screen.safeArea);
        }

        // IMPORTANT: never call Screen.SetResolution on Android/iOS here.
        // Unity/OS orientation transitions can briefly report portrait-shaped
        // pixels; forcing a SetResolution repair caused the visible Apply-time
        // 1080x2400 -> 2400x1080 jump observed on the Samsung S20 FE.
        ObserveResolutionOnly();

        if (QualitySettings.pixelLightCount > 1)
            QualitySettings.pixelLightCount = 1;

        FitCanvases();
        KeepMobileGraphicsModelNative();
    }

    private void ObserveResolutionOnly()
    {
        bool portraitPixels = Screen.height > Screen.width;

        if (!portraitPixels)
        {
            resolutionMismatchLogged = false;
            return;
        }

        if (resolutionMismatchLogged)
            return;

        resolutionMismatchLogged = true;
        Debug.LogWarning(
            "ATB_MOBILE_TRANSIENT_PORTRAIT_OBSERVED observed=" +
            Screen.width + "x" + Screen.height +
            " action=none os_owns_resolution=true");
    }

    private void FitCanvases()
    {
        Rect safe = Screen.safeArea;
        float screenHeight = Mathf.Max(1f, Screen.height);
        float safeHeight =
            safe.height > screenHeight * 0.5f
                ? safe.height
                : screenHeight;

        // v5.25.3 used 1248 on 2400x1080. It was safe but visually left too
        // much empty space. 1120 keeps a modest handset/tablet margin while
        // allowing the UI to use more of the available horizontal viewport.
        float referenceHeight = Mathf.Clamp(
            1120f * screenHeight / safeHeight,
            1120f,
            1300f);

        CanvasScaler[] all =
            UnityEngine.Object.FindObjectsByType<CanvasScaler>(
                FindObjectsInactive.Include);

        foreach (CanvasScaler scaler in all)
        {
            if (scaler == null || !scaler.gameObject.scene.IsValid())
                continue;

            Canvas canvas = scaler.GetComponent<Canvas>();

            if (canvas == null ||
                !canvas.isRootCanvas ||
                canvas.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            bool changed =
                scaler.uiScaleMode !=
                    CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                scaler.screenMatchMode !=
                    CanvasScaler.ScreenMatchMode.Expand ||
                Mathf.Abs(
                    scaler.referenceResolution.y -
                    referenceHeight) > 1f ||
                Mathf.Abs(
                    scaler.referenceResolution.x -
                    1920f) > 1f;

            if (changed)
            {
                scaler.uiScaleMode =
                    CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.screenMatchMode =
                    CanvasScaler.ScreenMatchMode.Expand;
                scaler.referenceResolution =
                    new Vector2(1920f, referenceHeight);
            }

            if (configuredCanvases.Add(scaler) || changed)
            {
                Debug.Log(
                    "ATB_MOBILE_CANVAS_FIT name=" +
                    scaler.gameObject.name +
                    " ref=1920x" + referenceHeight.ToString("F0") +
                    " safe=" + safe +
                    " device=" + Screen.width + "x" + Screen.height);
            }
        }
    }

    private void KeepMobileGraphicsModelNative()
    {
        AtlasBoardSettingsV2Controller controller =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardSettingsV2Controller>(
                    FindObjectsInactive.Include);

        if (controller == null)
            return;

        int width = Mathf.Max(Screen.width, Screen.height);
        int height = Mathf.Min(Screen.width, Screen.height);

        // AtlasBoardSettingsV2Controller.ReadUserUi reads the selected index
        // from its private availableResolutions list. Replace that mobile-only
        // list with the actual landscape viewport so Apply can never save the
        // stale 1080x2400 portrait option that previously triggered a flip.
        FieldInfo resolutionsInfo =
            controller.GetType().GetField(
                "availableResolutions",
                BindingFlags.Instance | BindingFlags.NonPublic);

        if (resolutionsInfo?.GetValue(controller) is List<Vector2Int> values)
        {
            if (values.Count != 1 ||
                values[0].x != width ||
                values[0].y != height)
            {
                values.Clear();
                values.Add(new Vector2Int(width, height));
            }
        }

        TMP_Dropdown resolution =
            ReadDropdown(controller, "resolutionDropdown");

        if (resolution != null)
        {
            string label = width + " x " + height;

            if (resolution.options.Count != 1 ||
                !string.Equals(
                    resolution.options[0].text,
                    label,
                    StringComparison.Ordinal))
            {
                resolution.ClearOptions();
                resolution.AddOptions(new List<string> { label });
                resolution.SetValueWithoutNotify(0);
                resolution.RefreshShownValue();
            }

            resolution.interactable = false;
        }

        TMP_Dropdown displayMode =
            ReadDropdown(controller, "displayModeDropdown");

        if (displayMode != null)
            displayMode.interactable = false;

        if (!mobileGraphicsControlsLogged)
        {
            mobileGraphicsControlsLogged = true;
            Debug.Log(
                "ATB_MOBILE_GRAPHICS OS_OWNS_RESOLUTION " +
                "native=" + width + "x" + height +
                " desktop_dropdowns_disabled setresolution_repair=disabled");
        }
    }

    private static TMP_Dropdown ReadDropdown(
        object controller,
        string name)
    {
        FieldInfo info =
            controller.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);

        return info == null
            ? null
            : info.GetValue(controller) as TMP_Dropdown;
    }
#endif
}
