#if UNITY_ANDROID && !UNITY_EDITOR && DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Device-local QA logs for Android Development APKs. The Android sandbox is
/// writable and does not depend on a Windows exe folder or network backend.
/// Native crashes still require adb logcat; release builds exclude this class.
/// </summary>
[DefaultExecutionOrder(-31990)]
public sealed class AtlasBoardAndroidDeviceDiagnostics : MonoBehaviour
{
    private const int MaxLogChars = 2200;
    private const long MaxFileBytes = 8 * 1024 * 1024;
    private const int MaxSavedFiles = 12;
    private static readonly object Gate = new object();
    private static readonly Queue<string> Pending = new Queue<string>();
    private static AtlasBoardAndroidDeviceDiagnostics instance;
    private StreamWriter writer;
    private string filename;
    private float nextFlush;
    private bool fileLimit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void HookEarly()
    {
        Application.logMessageReceivedThreaded -= OnUnityLog;
        Application.logMessageReceivedThreaded += OnUnityLog;
        lock (Gate) Pending.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject host = new GameObject("AtlasBoard_Android_Device_Logs");
        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardAndroidDeviceDiagnostics>();
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
            string directory = Path.Combine(Application.persistentDataPath, "Diagnostics");
            Directory.CreateDirectory(directory);
            filename = Path.Combine(directory, "AtlasBoard-Android-" +
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".log");
            writer = new StreamWriter(new FileStream(filename, FileMode.CreateNew,
                FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false));
            writer.AutoFlush = true;
            writer.WriteLine(DateTime.UtcNow.ToString("O") +
                " ATB_ANDROID_SESSION_START version=" + Application.version +
                " unity=" + Application.unityVersion +
                " model=" + Clean(SystemInfo.deviceModel) +
                " os=" + Clean(SystemInfo.operatingSystem) +
                " resolution=" + Screen.width + "x" + Screen.height +
                " file=" + Clean(filename));
            RetainLatest(directory);
            Flush();
            Debug.Log("ATB_ANDROID_DEVICE_LOG_READY path=" + filename);
        }
        catch (Exception error)
        {
            // Diagnostics must never prevent startup or online initialization.
            writer = null;
            Debug.LogWarning("ATB_ANDROID_DEVICE_LOG_UNAVAILABLE type=" +
                error.GetType().Name + " use_adb_logcat=true");
        }
    }

    private static void RetainLatest(string folder)
    {
        try
        {
            FileInfo[] logs = new DirectoryInfo(folder).GetFiles("AtlasBoard-Android-*.log");
            Array.Sort(logs, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            for (int i = MaxSavedFiles; i < logs.Length; i++)
                try { logs[i].Delete(); } catch (IOException) { }
        }
        catch (Exception) { /* Retention is best-effort. */ }
    }

    private static string Clean(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        s = Regex.Replace(s, @"(?i)(Bearer\s+)[A-Za-z0-9._~+/=-]+", "$1[REDACTED]");
        s = Regex.Replace(s,
            @"(?i)((?:idToken|refreshToken|password|secret|apiKey|ticket|authorization|accountId|steamId|firebaseUid|email)\s*[:=]\s*['"" ]?)[^,'""\s}]+",
            "$1[REDACTED]");
        s = Regex.Replace(s, @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", "[REDACTED_EMAIL]");
        s = s.Replace('\r', ' ').Replace('\n', ' ');
        if (s.Length > MaxLogChars) s = s.Substring(0, MaxLogChars) + "...[TRUNCATED]";
        return s;
    }

    private static void OnUnityLog(string message, string stackTrace, LogType type)
    {
        string line = DateTime.UtcNow.ToString("O") + " UNITY_" +
            type.ToString().ToUpperInvariant() + " " + Clean(message);
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            line += " stack=" + Clean(stackTrace);
        lock (Gate)
        {
            if (Pending.Count > 2500) Pending.Dequeue();
            Pending.Enqueue(line);
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < nextFlush) return;
        nextFlush = Time.unscaledTime + 0.5f;
        Flush();
    }

    private void Flush()
    {
        if (writer == null || fileLimit) return;
        lock (Gate)
        {
            try
            {
                while (Pending.Count > 0)
                {
                    if (writer.BaseStream.Position >= MaxFileBytes)
                    {
                        writer.WriteLine("ATB_ANDROID_LOG_CAP_REACHED");
                        fileLimit = true;
                        Pending.Clear();
                        break;
                    }
                    writer.WriteLine(Pending.Dequeue());
                }
                writer.Flush();
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        }
    }

    // Android 11+ restricts normal file-manager access to Android/data.
    // Share the actual sanitized on-device log from inside the app instead of
    // requiring USB debugging, root, or unsafe broad storage permissions.
    // Development APK only; never embedded into a Play release.
    private void OnGUI()
    {
        if (Event.current == null || writer == null) return;
        Rect safe = Screen.safeArea;
        float x = Mathf.Max(8f, safe.xMin + 12f);
        float y = Mathf.Max(8f, Screen.height - safe.yMax + 12f);
        if (GUI.Button(new Rect(x, y, 118f, 52f), "Share Logs"))
        {
            Debug.Log("ATB_ANDROID_LOG_SHARE user_requested=true");
            Flush();
            ShareCurrentLog();
        }
    }

    private void ShareCurrentLog()
    {
        try
        {
            if (string.IsNullOrEmpty(filename) || !File.Exists(filename))
            {
                Debug.LogWarning("ATB_ANDROID_LOG_SHARE log_file_missing");
                return;
            }
            // Android Intent extras have a Binder transaction size limit.
            // The last ~48K characters are sufficient for a recent failure.
            const int shareLimit = 48000;
            string body = "AtlasBoard Android QA logs\n" +
                "Build: " + Application.version + "\n" +
                "Device: " + SystemInfo.deviceModel + "\n" +
                "Session file: " + Path.GetFileName(filename) + "\n" +
                "\n" + ReadSanitizedTail(filename, shareLimit);
            using (AndroidJavaClass intentClass =
                new AndroidJavaClass("android.content.Intent"))
            using (AndroidJavaClass unity =
                new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity =
                    unity.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject send = new AndroidJavaObject(
                    "android.content.Intent", intentClass.GetStatic<string>("ACTION_SEND"));
                send.Call<AndroidJavaObject>("setType", "text/plain");
                send.Call<AndroidJavaObject>("putExtra",
                    "android.intent.extra.TEXT", body);
                AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>(
                    "createChooser", send, "Share AtlasBoard diagnostics");
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try { activity.Call("startActivity", chooser); }
                    catch (Exception error)
                    {
                        Debug.LogWarning("ATB_ANDROID_LOG_SHARE_FAILED type=" +
                            error.GetType().Name);
                    }
                    finally
                    {
                        chooser.Dispose();
                        send.Dispose();
                        activity.Dispose();
                    }
                }));
            }
        }
        catch (Exception error)
        {
            Debug.LogWarning("ATB_ANDROID_LOG_SHARE_FAILED type=" +
                error.GetType().Name);
        }
    }

    private static string ReadSanitizedTail(string path, int maxChars)
    {
        string data = File.ReadAllText(path);
        if (data.Length > maxChars)
            data = data.Substring(data.Length - maxChars);
        string[] lines = data.Split('\n');
        StringBuilder result = new StringBuilder();
        foreach (string line in lines)
            result.AppendLine(Clean(line));
        return result.ToString();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Flush();
    }

    private void OnApplicationQuit()
    {
        lock (Gate) Pending.Enqueue(DateTime.UtcNow.ToString("O") + " ATB_ANDROID_SESSION_CLEAN_EXIT");
        Flush();
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        Flush();
        if (writer != null) writer.Dispose();
        writer = null;
        instance = null;
    }
}
#endif
