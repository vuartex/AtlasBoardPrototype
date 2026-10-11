#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Development-only diagnostics. Standalone builds write independent per-launch
/// files to [folder containing AtlasBoardGuest.exe]/Logs automatically.
/// This component makes NO network calls and never changes gameplay state.
/// Native failures before managed startup still require Windows WER dumps.
/// </summary>
[DefaultExecutionOrder(-32000)]
[DisallowMultipleComponent]
public sealed class AtlasBoardRuntimeDiagnostics : MonoBehaviour
{
    private const int MaximumQueuedEntries = 2048;
    private const int MaximumSavedSessions = 40;
    private const long MaximumSessionBytes = 32L * 1024L * 1024L;
    private const int MaximumMessageLength = 2400;
    private const float FlushIntervalSeconds = 0.35f;

    private static readonly object Gate = new object();
    private static readonly Queue<string> Queue = new Queue<string>();
    private static AtlasBoardRuntimeDiagnostics instance;

    private StreamWriter writer;
    private string activeMarker;
    private string currentLog;
    private bool isClosing;
    private bool fileLimitReached;
    private float nextFlushTime;
    private float nextFpsTime;
    private float previousStallTime = -10f;
    private int framesInWindow;
    private float secondsInWindow;
    private int droppedMessages;

    // Register before scene initialization, so logs emitted by other runtime
    // initializers are retained even before our MonoBehaviour is constructed.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterEarlyLogging()
    {
        Application.logMessageReceivedThreaded -= OnUnityLog;
        Application.logMessageReceivedThreaded += OnUnityLog;
        lock (Gate)
        {
            Queue.Clear();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject gameObject = new GameObject("AtlasBoard_Runtime_Diagnostics");
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<AtlasBoardRuntimeDiagnostics>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        try
        {
            string preferredDirectory = ResolveLogDirectory();
            string selectedDirectory = preferredDirectory;
            try
            {
                Directory.CreateDirectory(selectedDirectory);
            }
            catch (UnauthorizedAccessException)
            {
                selectedDirectory = Path.Combine(Application.persistentDataPath, "Diagnostics");
                Directory.CreateDirectory(selectedDirectory);
            }
            catch (IOException)
            {
                selectedDirectory = Path.Combine(Application.persistentDataPath, "Diagnostics");
                Directory.CreateDirectory(selectedDirectory);
            }

            int pid = Process.GetCurrentProcess().Id;
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            currentLog = Path.Combine(selectedDirectory, "AtlasBoard-Session-" + stamp + "-" + pid + ".log");
            activeMarker = Path.Combine(selectedDirectory, "AtlasBoard-Active-" + pid + ".marker");
            writer = new StreamWriter(
                new FileStream(currentLog, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite),
                new UTF8Encoding(false));

            string buildDirectory = Application.isEditor
                ? "EDITOR (persistentDataPath)"
                : Path.GetDirectoryName(Application.dataPath);
            File.WriteAllText(activeMarker,
                "startedUtc=" + DateTime.UtcNow.ToString("O") + Environment.NewLine +
                "session=" + Path.GetFileName(currentLog), Encoding.UTF8);

            lock (Gate)
            {
                // Start-of-session metadata is always first in the file.
                writer.WriteLine(DateTime.UtcNow.ToString("O") + " SESSION_START " +
                    "local=" + DateTimeOffset.Now.ToString("O") +
                    " utc=" + stamp +
                    " pid=" + pid +
                    " unity=" + Application.unityVersion +
                    " version=" + Application.version +
                    " buildGUID=" + Application.buildGUID +
                    " development=" + UnityEngine.Debug.isDebugBuild +
                    " editor=" + Application.isEditor +
                    " os=" + SystemInfo.operatingSystem +
                    " gpu=" + Sanitize(SystemInfo.graphicsDeviceName) +
                    " cpuCores=" + SystemInfo.processorCount +
                    " memoryMB=" + SystemInfo.systemMemorySize +
                    " buildDir=" + Sanitize(buildDirectory));

                string exeFile = Application.isEditor ? string.Empty :
                    Path.Combine(buildDirectory, "AtlasBoardGuest.exe");
                if (!string.IsNullOrEmpty(exeFile) && File.Exists(exeFile))
                {
                    FileInfo exe = new FileInfo(exeFile);
                    writer.WriteLine(DateTime.UtcNow.ToString("O") +
                        " BUILD_FILE bytes=" + exe.Length +
                        " lastWriteUtc=" + exe.LastWriteTimeUtc.ToString("O") +
                        " name=" + Sanitize(exe.Name));
                }
                writer.WriteLine(DateTime.UtcNow.ToString("O") + " START_OPTIONS " +
                    "localEmulators=" + HasArgument("-atlasLocalEmulators") +
                    " d3d11=" + HasArgument("-force-d3d11") +
                    " directX12=" + HasArgument("-force-d3d12") +
                    " logDirectory=" + Sanitize(selectedDirectory));

                FlushLocked();
            }

            FindPreviousUncleanSessions(selectedDirectory, pid);
            Application.lowMemory += OnLowMemory;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            nextFpsTime = Time.realtimeSinceStartup + 5f;
            nextFlushTime = Time.realtimeSinceStartup + FlushIntervalSeconds;
            RemoveOldSessions(selectedDirectory);

            Record("DIAGNOSTICS_READY sessionLog=" + Sanitize(currentLog), true);
        }
        catch (Exception error)
        {
            // Never terminate the game because a diagnostic folder is unwritable.
            Record("DIAGNOSTICS_FAILED detail=" + Sanitize(error.Message), true);
        }
    }

    private static string ResolveLogDirectory()
    {
        if (Application.isEditor)
            return Path.Combine(Application.persistentDataPath, "Diagnostics");
        // Windows standalone Application.dataPath is .../AtlasBoardGuest_Data.
        // Parent is the build folder containing AtlasBoardGuest.exe.
        string executableDirectory = Path.GetDirectoryName(Application.dataPath);
        return Path.Combine(executableDirectory, "Logs");
    }

    private static bool HasArgument(string flag)
    {
        foreach (string arg in Environment.GetCommandLineArgs())
        {
            if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void Update()
    {
        if (writer == null || isClosing)
            return;

        float dt = Time.unscaledDeltaTime;
        framesInWindow++;
        secondsInWindow += dt;

        if (dt >= 0.4f && Time.realtimeSinceStartup - previousStallTime > 0.8f)
        {
            previousStallTime = Time.realtimeSinceStartup;
            Record("MAIN_THREAD_FRAME_STALL durationMs=" +
                (dt * 1000f).ToString("F0", CultureInfo.InvariantCulture) +
                " frame=" + Time.frameCount, true);
        }

        if (Time.realtimeSinceStartup >= nextFpsTime)
        {
            float fps = secondsInWindow > 0f ? framesInWindow / secondsInWindow : 0f;
            Record("FRAME_SUMMARY seconds=" +
                secondsInWindow.ToString("F2", CultureInfo.InvariantCulture) +
                " frames=" + framesInWindow +
                " avgFPS=" + fps.ToString("F1", CultureInfo.InvariantCulture) +
                " frame=" + Time.frameCount, false);
            framesInWindow = 0;
            secondsInWindow = 0f;
            nextFpsTime = Time.realtimeSinceStartup + 5f;
        }

        if (Time.realtimeSinceStartup >= nextFlushTime)
        {
            nextFlushTime = Time.realtimeSinceStartup + FlushIntervalSeconds;
            lock (Gate) FlushLocked();
        }
    }

    private static void OnUnityLog(string message, string stackTrace, LogType type)
    {
        bool critical = type == LogType.Exception || type == LogType.Error ||
            type == LogType.Assert || type == LogType.Warning;
        string content = "UNITY_" + type.ToString().ToUpperInvariant() + " " + Sanitize(message);
        if (critical && !string.IsNullOrEmpty(stackTrace))
            content += " | stack=" + Sanitize(stackTrace);
        Record(content, critical);
    }

    private void OnLowMemory()
    {
        Record("LOW_MEMORY_SIGNAL", true);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        Record("UNHANDLED_MANAGED_EXCEPTION terminating=" + args.IsTerminating +
            " detail=" + Sanitize(args.ExceptionObject?.ToString()), true);
    }

    private static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        string text = Regex.Replace(input,
            @"(?i)(Bearer\s+)[A-Za-z0-9._~+/=-]+", "$1[REDACTED]");
        text = Regex.Replace(text,
            @"(?i)((?:idToken|refreshToken|password|secret|apiKey|ticket|authorization)\s*[:=]\s*[""']?)[^,""'\s]+",
            "$1[REDACTED]");
        text = text.Replace('\r', ' ').Replace('\n', ' ');
        return text.Length <= MaximumMessageLength
            ? text : text.Substring(0, MaximumMessageLength) + "...[TRUNCATED]";
    }

    private static void Record(string message, bool flushImmediately)
    {
        string line = DateTime.UtcNow.ToString("O") + " " + message;
        lock (Gate)
        {
            if (Queue.Count >= MaximumQueuedEntries)
            {
                Queue.Dequeue();
                if (instance != null) instance.droppedMessages++;
            }
            Queue.Enqueue(line);
            if (flushImmediately && instance != null)
                instance.FlushLocked();
        }
    }

    // Only called while holding Gate; never logs recursively or throws into gameplay.
    private void FlushLocked()
    {
        if (writer == null || fileLimitReached)
            return;
        try
        {
            while (Queue.Count > 0)
            {
                if (writer.BaseStream.Position > MaximumSessionBytes)
                {
                    writer.WriteLine(DateTime.UtcNow.ToString("O") +
                        " LOG_FILE_LIMIT_REACHED maxBytes=" + MaximumSessionBytes);
                    writer.Flush();
                    fileLimitReached = true;
                    Queue.Clear();
                    break;
                }
                writer.WriteLine(Queue.Dequeue());
            }
            writer.Flush();
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (ObjectDisposedException) { }
    }

    private static void FindPreviousUncleanSessions(string directory, int currentPid)
    {
        try
        {
            foreach (string marker in Directory.GetFiles(directory, "AtlasBoard-Active-*.marker"))
            {
                string file = Path.GetFileNameWithoutExtension(marker);
                string[] chunks = file.Split('-');
                if (chunks.Length < 3 || !int.TryParse(chunks[chunks.Length - 1], out int pid))
                    continue;
                if (pid == currentPid) continue;
                try
                {
                    using (Process process = Process.GetProcessById(pid))
                        if (!process.HasExited) continue;
                }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }

                Record("PREVIOUS_SESSION_UNCLEAN_EXIT marker=" + Sanitize(Path.GetFileName(marker)) +
                    " (crash/forced close/power failure possible)", true);
                try { File.Delete(marker); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void RemoveOldSessions(string directory)
    {
        try
        {
            FileInfo[] sessions = new DirectoryInfo(directory).GetFiles("AtlasBoard-Session-*.log");
            Array.Sort(sessions, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            for (int i = MaximumSavedSessions; i < sessions.Length; ++i)
            {
                try { sessions[i].Delete(); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void OnApplicationQuit()
    {
        isClosing = true;
        Record("SESSION_CLEAN_EXIT droppedMessages=" + droppedMessages, true);
        if (!string.IsNullOrEmpty(activeMarker))
        {
            try { File.Delete(activeMarker); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        Application.lowMemory -= OnLowMemory;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        lock (Gate)
        {
            FlushLocked();
            if (writer != null)
            {
                writer.Dispose();
                writer = null;
            }
        }
        instance = null;
    }
}
#endif
