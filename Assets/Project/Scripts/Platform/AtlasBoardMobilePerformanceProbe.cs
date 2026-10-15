using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

/// <summary>
/// Android/iOS hardware rendering evidence and conservative run-only budget.
/// This supplements but does not replace AtlasBoardMobileGameplayUX; it never
/// persists a quality setting or modifies Windows/Steam/UI/gameplay timing.
/// Native-resolution issues are diagnosed without Screen.SetResolution calls.
/// </summary>
[DefaultExecutionOrder(16100)]
public sealed class AtlasBoardMobilePerformanceProbe : MonoBehaviour
{
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    private static AtlasBoardMobilePerformanceProbe instance;
    private static readonly FrameTiming[] Timings = new FrameTiming[5];
    private readonly HashSet<Camera> preparedCameras = new HashSet<Camera>();
    private float nextCameraCheck;
    private float nextLog;
    private float previousTime;
    private int frames;
    private float frameMilliseconds;
    private int consecutiveSlowWindows;
    private float requestedBufferScale = 0.80f;
    private bool warnedResolution;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject host = new GameObject("AtlasBoard_Mobile_Performance_Probe");
        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardMobilePerformanceProbe>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        previousTime = Time.unscaledTime;
        nextLog = previousTime + 15f;
        // These graphics ceilings affect only the current mobile process;
        // do not call SetQualityLevel, Save, PlayerPrefs or Screen.SetResolution.
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.pixelLightCount = 0;
        QualitySettings.lodBias = Mathf.Min(QualitySettings.lodBias, 0.65f);
        QualitySettings.globalTextureMipmapLimit =
            Mathf.Max(QualitySettings.globalTextureMipmapLimit, 1);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        ConfigureDynamicScale(requestedBufferScale);
        Debug.Log("ATB_MOBILE_PROBE_START gpu=" + SystemInfo.graphicsDeviceName +
            " graphicsApi=" + SystemInfo.graphicsDeviceType +
            " shader=" + SystemInfo.graphicsShaderLevel +
            " processor=" + SystemInfo.processorType +
            " memoryMB=" + SystemInfo.systemMemorySize +
            " renderPipeline=" +
            (GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.GetType().Name
                : "BUILTIN_OR_DEFAULT"));
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        FrameTimingManager.CaptureFrameTimings();
        frames++;
        frameMilliseconds += Time.unscaledDeltaTime * 1000f;
        if (Time.unscaledTime >= nextCameraCheck)
        {
            nextCameraCheck = Time.unscaledTime + 8f;
            EnsureCameraBudgets();
        }
        if (Time.unscaledTime < nextLog) return;
        nextLog = Time.unscaledTime + 15f;
        float averageFps = frameMilliseconds > 0
            ? frames * 1000f / frameMilliseconds : 0f;
        FrameTimingManager.GetLatestTimings((uint)Timings.Length, Timings);
        FrameTiming latest = Timings[0];
        float gpu = (float)latest.gpuFrameTime;
        float cpu = (float)latest.cpuFrameTime;
        long allocated = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
        Debug.Log("ATB_MOBILE_FRAME_EVIDENCE avgFps=" + averageFps.ToString("F1") +
            " cpuMs=" + cpu.ToString("F1") +
            " gpuMs=" + gpu.ToString("F1") +
            " cpuGpuAvailable=" + ((cpu > 0.01f || gpu > 0.01f) ? "true" : "false") +
            " requestedDynamicBufferScale=" + requestedBufferScale.ToString("F2") +
            " reportedWidthScale=" + ScalableBufferManager.widthScaleFactor.ToString("F2") +
            " reportedHeightScale=" + ScalableBufferManager.heightScaleFactor.ToString("F2") +
            " nativeScreen=" + Screen.width + "x" + Screen.height +
            " allocatedMemoryMB=" + allocated +
            " cameraCount=" + preparedCameras.Count +
            " textureMipmapLimit=" + QualitySettings.globalTextureMipmapLimit +
            " note=reported_scaling_does_not_prove_backbuffer_downscale");
        if (averageFps > 0 && averageFps < 38f)
            consecutiveSlowWindows++;
        else consecutiveSlowWindows = 0;
        if (consecutiveSlowWindows >= 2 && requestedBufferScale > 0.71f)
        {
            requestedBufferScale = 0.70f;
            ConfigureDynamicScale(requestedBufferScale);
            Debug.LogWarning("ATB_MOBILE_RENDER_FALLBACK active=true " +
                "dynamicScaleRequested=0.70 measuredImprovement=NOT_YET_VERIFIED");
        }
        frames = 0;
        frameMilliseconds = 0f;
    }

    private void EnsureCameraBudgets()
    {
        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || !camera.gameObject.scene.IsValid())
                continue;
            if (!preparedCameras.Add(camera)) continue;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = true;
        }
        preparedCameras.RemoveWhere(camera => camera == null);
        if (GraphicsSettings.currentRenderPipeline == null && !warnedResolution)
        {
            warnedResolution = true;
            Debug.LogWarning("ATB_MOBILE_SCALE_LIMIT pipeline=builtin " +
                "requested dynamic buffer scale may not affect the final backbuffer; " +
                "use cpuMs/gpuMs and frame debugging before declaring FPS fixed");
        }
    }

    private static void ConfigureDynamicScale(float scale)
    {
        try
        {
            ScalableBufferManager.ResizeBuffers(scale, scale);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("ATB_MOBILE_DRS_UNSUPPORTED exception=" +
                exception.GetType().Name);
        }
    }
#endif
}
