#if UNITY_ANDROID && !UNITY_EDITOR && DEVELOPMENT_BUILD
using System;
using System.Reflection;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Phase 17 Android diagnostic-only probe. No gameplay, graphics, Firebase,
/// UserSettings, camera, frame rate or Steam/Windows changes.
/// Logs 10-second frame-time distributions and supported Unity counters,
/// distinguishing unavailable CPU/GPU counters from measured values.
/// Compiled into Android DEVELOPMENT builds only.
/// </summary>
[DefaultExecutionOrder(31000)]
public sealed class AtlasBoardAndroidFpsDiagnostics : MonoBehaviour
{
    // Development-only, random app-install QA ID. This is NOT Android ID,
    // Firebase UID, IMEI, serial, account identity or tracking telemetry.
    private const string QaDeviceIdKey = "AtlasBoard.QA.InstallId.v1";
    private static readonly string QaSessionId =
        Guid.NewGuid().ToString("N").Substring(0, 12);
    private static string cachedQaDeviceId;

    public static string QaTraceSuffix
    {
        get
        {
            if (string.IsNullOrEmpty(cachedQaDeviceId))
            {
                string saved = PlayerPrefs.GetString(QaDeviceIdKey, "");
                if (saved.Length != 12 || !IsHexId(saved))
                {
                    saved = Guid.NewGuid().ToString("N").Substring(0, 12);
                    PlayerPrefs.SetString(QaDeviceIdKey, saved);
                    PlayerPrefs.Save();
                }
                cachedQaDeviceId = saved;
            }
            return " qaDeviceId=" + cachedQaDeviceId +
                   " sessionId=" + QaSessionId;
        }
    }

    private static bool IsHexId(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!((c >= '0' && c <= '9') ||
                  (c >= 'a' && c <= 'f')))
                return false;
        }
        return true;
    }

    private const float WindowSeconds = 10f;
    private const int HistogramMaxMs = 200;

    private static AtlasBoardAndroidFpsDiagnostics instance;
    private readonly int[] frameTimeMsHistogram = new int[HistogramMaxMs + 1];
    private int frameCount;
    private int framesOver33;
    private int framesOver50;
    private int framesOver100;
    private float frameSeconds;
    private float worstFrameMs;
    private float windowStarted;
    private int windowIndex;
    private bool hasFocus = true;
    private bool isPaused;
    private string lastPhase = "boot";
    private float mainThreadMs;
    private float renderThreadMs;
    private float gpuFrameMs;
    private long gcAllocatedMax;
    private long gcAllocatedTotal;
    private int gcSamples;
    private int netCount;
    private int netErrors;
    private int netMatchCount;
    private float netWorstMs;
    private float netTotalMs;

    private ProfilerRecorder mainThreadRecorder;
    private ProfilerRecorder renderThreadRecorder;
    private ProfilerRecorder gpuFrameRecorder;
    private ProfilerRecorder drawCallsRecorder;
    private ProfilerRecorder batchesRecorder;
    private ProfilerRecorder setPassRecorder;
    private ProfilerRecorder trianglesRecorder;
    private ProfilerRecorder gcAllocatedRecorder;
    private ProfilerRecorder gcUsedRecorder;
    private ProfilerRecorder waitForPresentRecorder;
    private ProfilerRecorder canvasBuildRecorder;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;
        GameObject host = new GameObject("AtlasBoard_Android_FPS_Diagnostics");
        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardAndroidFpsDiagnostics>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // Recorder names differ by pipeline / Unity build. Invalid counters
        // are logged as NA; never present NA as a measured zero.
        mainThreadRecorder = Start(ProfilerCategory.Internal, "Main Thread");
        renderThreadRecorder = Start(ProfilerCategory.Internal, "Render Thread");
        gpuFrameRecorder = Start(ProfilerCategory.Render, "GPU Frame Time");
        drawCallsRecorder = Start(ProfilerCategory.Render, "Draw Calls Count");
        batchesRecorder = Start(ProfilerCategory.Render, "Batches Count");
        setPassRecorder = Start(ProfilerCategory.Render, "SetPass Calls Count");
        trianglesRecorder = Start(ProfilerCategory.Render, "Triangles Count");
        gcAllocatedRecorder = Start(ProfilerCategory.Memory, "GC Allocated In Frame");
        gcUsedRecorder = Start(ProfilerCategory.Memory, "GC Used Memory");
        waitForPresentRecorder = Start(ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread");
        canvasBuildRecorder = Start(ProfilerCategory.Gui, "Canvas.BuildBatch");

        AtlasBoardNetworkTelemetry.SampleReported += OnNetworkSample;
        SceneManager.activeSceneChanged += OnSceneChanged;
        ResetWindow();
        Debug.Log("ATB_ANDROID_FPS_DIAG_START build=development_only" +
                  " model=" + Sanitize(SystemInfo.deviceModel) +
                  " os=" + Sanitize(SystemInfo.operatingSystem) +
                  " gpu=" + Sanitize(SystemInfo.graphicsDeviceName) +
                  " api=" + SystemInfo.graphicsDeviceType +
                  " cpuCores=" + SystemInfo.processorCount +
                  " ramMB=" + SystemInfo.systemMemorySize +
                  " gpuMB=" + SystemInfo.graphicsMemorySize +
                  " screen=" + Screen.width + "x" + Screen.height +
                  " targetFps=" + Application.targetFrameRate +
                  " quality=" + QualitySettings.GetQualityLevel() +
                  " unity=" + Application.unityVersion +
                  " deviceTag=" + Sanitize(SystemInfo.deviceModel) +
                  QaTraceSuffix +
                  " profilerCounters=" + (mainThreadRecorder.Valid ? "partial_available" : "main_thread_NA") +
                  " note=diagnostic_not_optimizer");
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;
        SceneManager.activeSceneChanged -= OnSceneChanged;
        AtlasBoardNetworkTelemetry.SampleReported -= OnNetworkSample;
        mainThreadRecorder.Dispose();
        renderThreadRecorder.Dispose();
        gpuFrameRecorder.Dispose();
        drawCallsRecorder.Dispose();
        batchesRecorder.Dispose();
        setPassRecorder.Dispose();
        trianglesRecorder.Dispose();
        gcAllocatedRecorder.Dispose();
        gcUsedRecorder.Dispose();
        waitForPresentRecorder.Dispose();
        canvasBuildRecorder.Dispose();
        instance = null;
    }

    private static ProfilerRecorder Start(ProfilerCategory category, string counter)
    {
        try
        {
            return ProfilerRecorder.StartNew(category, counter, 8);
        }
        catch (Exception)
        {
            return default;
        }
    }

    private void Update()
    {
        if (isPaused || !hasFocus)
            return;
        float dt = Time.unscaledDeltaTime;
        if (!(dt > 0f) || dt > 5f)
            return;

        frameCount++;
        frameSeconds += dt;
        float frameMs = 1000f * dt;
        worstFrameMs = Mathf.Max(worstFrameMs, frameMs);
        frameTimeMsHistogram[Mathf.Clamp(Mathf.FloorToInt(frameMs), 0, HistogramMaxMs)]++;
        if (frameMs > 33.33f) framesOver33++;
        if (frameMs > 50f) framesOver50++;
        if (frameMs > 100f) framesOver100++;

        // This is an in-frame stat; read it each frame to avoid only sampling
        // quiet frames. Recorder absent -> NA (not zero).
        if (gcAllocatedRecorder.Valid && gcAllocatedRecorder.Count > 0)
        {
            long bytes = Math.Max(0L, gcAllocatedRecorder.LastValue);
            gcAllocatedMax = Math.Max(gcAllocatedMax, bytes);
            gcAllocatedTotal += bytes;
            gcSamples++;
        }

        if (Time.unscaledTime - windowStarted >= WindowSeconds)
            EmitWindow();
    }

    private void EmitWindow()
    {
        if (frameCount < 2 || frameSeconds <= 0f)
        {
            ResetWindow();
            return;
        }

        string phase = GetPhase();
        float avg = frameCount / frameSeconds;
        int p50 = Percentile(0.50f);
        int p95 = Percentile(0.95f);
        int p99 = Percentile(0.99f);

        string cpu = ReadMilliseconds(mainThreadRecorder);
        string renderThread = ReadMilliseconds(renderThreadRecorder);
        string gpu = ReadMilliseconds(gpuFrameRecorder);
        string wait = ReadMilliseconds(waitForPresentRecorder);
        string classification = Classify(avg, cpu, gpu);

        // Every diagnostic line has its own device model; Android's existing
        // Share Logs tool can therefore provide comparable excerpts separately.
        Debug.Log("ATB_ANDROID_FPS_WINDOW model=" + Sanitize(SystemInfo.deviceModel) +
                  QaTraceSuffix +
                  " window=" + (++windowIndex) +
                  " phase=" + phase +
                  " avgFps=" + avg.ToString("F1") +
                  " p50FrameMs=" + p50 +
                  " p95FrameMs=" + p95 +
                  " p99FrameMs=" + p99 +
                  " worstFrameMs=" + worstFrameMs.ToString("F1") +
                  " over33ms=" + framesOver33 + "/" + frameCount +
                  " over50ms=" + framesOver50 + "/" + frameCount +
                  " over100ms=" + framesOver100 + "/" + frameCount +
                  " mainThreadMs=" + cpu +
                  " renderThreadMs=" + renderThread +
                  " gpuFrameMs=" + gpu +
                  " waitForPresentMs=" + wait +
                  " gcAllocMaxKB=" + (gcSamples == 0 ? "NA" : (gcAllocatedMax / 1024f).ToString("F1")) +
                  " gcAllocAvgKB=" + (gcSamples == 0 ? "NA" : (gcAllocatedTotal / (1024f * gcSamples)).ToString("F1")) +
                  " gcUsedMB=" + ReadMegabytes(gcUsedRecorder) +
                  " managedMemoryMB=" + (Profiler.GetMonoUsedSizeLong() / (1024L * 1024L)) +
                  " totalAllocatedMB=" + (Profiler.GetTotalAllocatedMemoryLong() / (1024L * 1024L)) +
                  " drawCalls=" + ReadCount(drawCallsRecorder) +
                  " batches=" + ReadCount(batchesRecorder) +
                  " setPass=" + ReadCount(setPassRecorder) +
                  " triangles=" + ReadCount(trianglesRecorder) +
                  " canvasBuildNs=" + ReadCount(canvasBuildRecorder) +
                  " rtNetCalls=" + netCount +
                  " rtMatchCalls=" + netMatchCount +
                  " rtNetErrors=" + netErrors +
                  " rtAvgLatencyMs=" + (netCount == 0 ? "NA" : (netTotalMs / netCount).ToString("F0")) +
                  " rtWorstLatencyMs=" + (netCount == 0 ? "NA" : netWorstMs.ToString("F0")) +
                  " nativeResolution=" + Screen.width + "x" + Screen.height +
                  " renderScale=" + GetRenderScale() +
                  " quality=" + QualitySettings.GetQualityLevel() +
                  " aa=" + QualitySettings.antiAliasing +
                  " shadows=" + QualitySettings.shadows +
                  " targetFps=" + Application.targetFrameRate +
                  " batteryLevel=" + SystemInfo.batteryLevel.ToString("F2") +
                  " batteryState=" + SystemInfo.batteryStatus +
                  " hypothesis=" + classification +
                  " thermalSensor=NA");

        if (avg < 45f && Application.targetFrameRate >= 45)
        {
            Debug.LogWarning("ATB_ANDROID_FPS_HINT model=" + Sanitize(SystemInfo.deviceModel) +
                             QaTraceSuffix +
                             " phase=" + phase + " hypothesis=" + classification +
                             " advice=compare_offline_vs_online_and_profiler_timeline" +
                             " note=no_auto_optimization_from_this_probe");
        }
        lastPhase = phase;
        ResetWindow();
    }

    private void OnSceneChanged(Scene previous, Scene next)
    {
        if (frameCount > 2)
            EmitWindow();
        Debug.Log("ATB_ANDROID_FPS_SCENE previous=" + Sanitize(previous.name) +
                  " next=" + Sanitize(next.name) + QaTraceSuffix);
        ResetWindow();
    }

    private void OnApplicationPause(bool paused)
    {
        isPaused = paused;
        Debug.Log("ATB_ANDROID_FPS_FOCUS paused=" + paused + " skip_background_frames=true" + QaTraceSuffix);
        ResetWindow();
    }

    private void OnApplicationFocus(bool focused)
    {
        hasFocus = focused;
        Debug.Log("ATB_ANDROID_FPS_FOCUS focused=" + focused + " skip_background_frames=true" + QaTraceSuffix);
        ResetWindow();
    }

    private void OnNetworkSample(AtlasBoardNetworkTelemetrySample sample)
    {
        if (!sample.RequestWasSent)
            return;
        netCount++;
        if (!sample.Success) netErrors++;
        if (sample.FunctionName.IndexOf("match", StringComparison.OrdinalIgnoreCase) >= 0)
            netMatchCount++;
        netWorstMs = Mathf.Max(netWorstMs, sample.RoundTripMs);
        netTotalMs += sample.RoundTripMs;
    }

    private string GetPhase()
    {
        try
        {
            AtlasBoardLobbyRuntimeBridge lobby = FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>();
            AtlasLobbySnapshot snapshot = lobby != null ? lobby.CurrentSnapshot : null;
            if (snapshot != null)
            {
                if (!string.IsNullOrWhiteSpace(snapshot.MatchId))
                    return "online_match";
                return "online_lobby";
            }
            TurnManager turn = FindAnyObjectByType<TurnManager>();
            if (turn != null && turn.IsMatchStarted && !turn.IsMatchFinished)
                return "offline_match";
            if (turn != null && turn.IsMatchFinished)
                return "match_result";
            return "menu";
        }
        catch (Exception)
        {
            return lastPhase;
        }
    }

    private void ResetWindow()
    {
        windowStarted = Time.unscaledTime;
        frameCount = 0;
        frameSeconds = 0f;
        worstFrameMs = 0f;
        framesOver33 = framesOver50 = framesOver100 = 0;
        Array.Clear(frameTimeMsHistogram, 0, frameTimeMsHistogram.Length);
        gcAllocatedMax = gcAllocatedTotal = 0L;
        gcSamples = 0;
        netCount = netErrors = netMatchCount = 0;
        netWorstMs = netTotalMs = 0f;
    }

    private int Percentile(float percentile)
    {
        int target = Mathf.Clamp(Mathf.CeilToInt(frameCount * percentile), 1, frameCount);
        int cumulative = 0;
        for (int i = 0; i < frameTimeMsHistogram.Length; i++)
        {
            cumulative += frameTimeMsHistogram[i];
            if (cumulative >= target)
                return i;
        }
        return HistogramMaxMs;
    }

    private static string ReadMilliseconds(ProfilerRecorder recorder)
    {
        // Built-in time counters, where available, are nanoseconds.
        return recorder.Valid && recorder.Count > 0
            ? (recorder.LastValue / 1000000d).ToString("F2")
            : "NA";
    }

    private static string ReadMegabytes(ProfilerRecorder recorder)
    {
        return recorder.Valid && recorder.Count > 0
            ? (recorder.LastValue / (1024d * 1024d)).ToString("F1")
            : "NA";
    }

    private static string ReadCount(ProfilerRecorder recorder)
    {
        return recorder.Valid && recorder.Count > 0
            ? recorder.LastValue.ToString()
            : "NA";
    }

    private static string Classify(float fps, string cpuStr, string gpuStr)
    {
        if (Application.targetFrameRate > 0 && Application.targetFrameRate < 45)
            return "target_cap_below_45";
        if (fps >= 45f)
            return "above_diagnostic_threshold";
        if (!float.TryParse(cpuStr, out float cpu) ||
            !float.TryParse(gpuStr, out float gpu))
            return "insufficient_cpu_gpu_counters";
        float frameMs = 1000f / Mathf.Max(fps, 1f);
        if (cpu > frameMs * 0.75f && gpu < cpu * 0.65f)
            return "main_thread_heavy_suspect_not_proof";
        if (gpu > frameMs * 0.75f && cpu < gpu * 0.65f)
            return "gpu_heavy_suspect_not_proof";
        return "mixed_or_waiting_requires_profiler";
    }

    private static string GetRenderScale()
    {
        try
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null)
                return "1.00_builtin_or_no_urp";
            PropertyInfo property = pipeline.GetType().GetProperty("renderScale",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property == null || property.PropertyType != typeof(float))
                return "NA_pipeline_unsupported";
            object current = property.GetValue(pipeline);
            return current is float value ? value.ToString("F2") : "NA_pipeline_unsupported";
        }
        catch (Exception)
        {
            return "NA_pipeline_error";
        }
    }

    private static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input)) return "unknown";
        return input.Replace(' ', '_').Replace('\n', '_').Replace('\r', '_').Replace('=', '_');
    }
}
#endif
