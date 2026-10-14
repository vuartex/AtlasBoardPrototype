#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Android-first, opt-in Unity Editor smoke build gate.
/// This does not change Build Settings, active platform, scenes, signing,
/// package name, persistent Player Settings or production Firebase settings.
/// It is not a Google Play release or a real-device multiplayer acceptance.
/// </summary>
public static class AtlasBoardAndroidSmokeQA
{
    private const string ScenePath = "Assets/Board_Prototype.unity";
    private const string Menu = "Atlas Board/QA & Diagnostics/QA/Android/";
    private const string Marker = "ATLASBOARD_ANDROID_BUILD_GATE";

    [MenuItem(Menu + "1 - Validate Android Readiness", false, 600)]
    public static void ValidateFromMenu()
    {
        int problems = ValidateReadiness();
        if (problems == 0)
        {
            Debug.Log(Marker + " PREFLIGHT_PASS (Unity Editor only). " +
                "Run Android/2 - Build Development APK to compile actual Android assemblies.");
        }
        else
        {
            Debug.LogError(Marker + " PREFLIGHT_FAIL errors=" + problems +
                ". No build was attempted.");
        }
    }

    /// <summary>
    /// Optional command-line entrypoint after the correct Unity editor and
    /// Android Build Support module are installed and Android is selected.
    /// </summary>
    public static void BatchValidate()
    {
        int errors = ValidateReadiness();
        Debug.Log(Marker + (errors == 0 ? " PREFLIGHT_PASS" : " PREFLIGHT_FAIL") +
            " errors=" + errors);
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }
    }

    [MenuItem(Menu + "2 - Build Development APK", false, 601)]
    public static void BuildDevelopmentApk()
    {
        int result = BuildSmoketest();
        if (result != 0)
        {
            Debug.LogError(Marker + " APK_FAIL. No release approval.");
        }
    }

    /// <summary>
    /// Optional -batchmode -executeMethod entrypoint; no release build,
    /// signing change or Play Console operation is performed.
    /// </summary>
    public static void BatchBuildDevelopmentApk()
    {
        int exitCode = BuildSmoketest();
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(exitCode);
        }
    }

    private static int ValidateReadiness()
    {
        int errors = 0;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogError(Marker + " Scripts or assets are still importing.");
            errors++;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError(Marker + " Stop Play Mode before Android QA.");
            errors++;
        }
        if (!BuildPipeline.IsBuildTargetSupported(
            BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogError(Marker + " Android Build Support is not installed. " +
                "Install Android Build Support + SDK & NDK Tools + OpenJDK in Unity Hub.");
            errors++;
        }
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.LogError(Marker + " Active target is not Android. " +
                "In Unity Build Profiles select Android and Switch Platform.");
            errors++;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            Debug.LogError(Marker + " Missing canonical scene: " + ScenePath);
            errors++;
        }
        if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))
        {
            Debug.LogError(Marker + " Missing PlayerSettings.bundleVersion.");
            errors++;
        }
        if (string.IsNullOrWhiteSpace(PlayerSettings.GetApplicationIdentifier(
            NamedBuildTarget.Android)))
        {
            Debug.LogError(Marker + " Missing Android application identifier.");
            errors++;
        }
        bool sceneEnabled = false;
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene != null && scene.enabled && string.Equals(
                scene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                sceneEnabled = true;
                break;
            }
        }
        if (!sceneEnabled)
        {
            Debug.LogError(Marker + " Board_Prototype must be enabled in " +
                "Build Profiles scenes. Do not alter the project's scene order.");
            errors++;
        }
        if (errors == 0)
        {
            Debug.Log(Marker + " preflight: Android target, supported module, " +
                "Board_Prototype scene, bundle version and application identifier OK. " +
                "SampleScene is explicitly excluded from the smoke APK. " +
                "Android development APK forces LandscapeLeft temporarily. " +
                "Firebase staging and on-device function are NOT verified.");
        }
        return errors;
    }

    // Only delete previous successful QA APK outputs after the new one exists.
    // Never remove an unrelated APK, project library or signed release AAB.
    private static int CleanSuccessfulBuildArtifacts(string directory, string keepApk)
    {
        int removed = 0;
        foreach (string old in Directory.GetFiles(directory,
            "AtlasBoard-Android-Development-*.apk", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(Path.GetFullPath(old), Path.GetFullPath(keepApk),
                StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                File.Delete(old);
                removed++;
                // Unity may put optional symbol archives beside a dev APK.
                string symbols = old + ".symbols.zip";
                if (File.Exists(symbols)) File.Delete(symbols);
            }
            catch (Exception e)
            {
                Debug.LogWarning(Marker + " CLEANUP_SKIPPED file=" +
                    Path.GetFileName(old) + " reason=" + e.GetType().Name);
            }
        }
        return removed;
    }

    private static int BuildSmoketest()
    {
        if (ValidateReadiness() != 0)
        {
            return 1;
        }
        string root = Directory.GetParent(Application.dataPath).FullName;
        string outputDirectory = Path.Combine(root, "Builds", "QA", "Android");
        Directory.CreateDirectory(outputDirectory);
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        string apk = Path.Combine(outputDirectory,
            "AtlasBoard-Android-Development-" + stamp + ".apk");
        if (File.Exists(apk))
        {
            Debug.LogError(Marker + " Output already exists: " + apk);
            return 1;
        }

        InsecureHttpOption oldHttp = PlayerSettings.insecureHttpOption;
        UIOrientation previousOrientation = PlayerSettings.defaultInterfaceOrientation;
        bool oldAppBundle = EditorUserBuildSettings.buildAppBundle;
        bool oldExportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
        BuildReport report = null;
        try
        {
            // Development-only localhost transport may be used with the
            // owner's opt-in adb reverse staging/emulator configuration.
            // Do not modify any Firebase runtime endpoint here.
            PlayerSettings.insecureHttpOption =
                InsecureHttpOption.DevelopmentOnly;
            // APK starts landscape immediately, including native splash.
            // Restored in finally so desktop/iOS project settings are unchanged.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            Debug.Log(Marker + " starting Android Development APK build. " +
                "CANONICAL_SCENE=" + ScenePath + " OUTPUT=" + apk);
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = apk,
                target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            });
        }
        catch (Exception exception)
        {
            Debug.LogError(Marker + " BUILD_EXCEPTION " + exception);
            return 1;
        }
        finally
        {
            PlayerSettings.insecureHttpOption = oldHttp;
            PlayerSettings.defaultInterfaceOrientation = previousOrientation;
            EditorUserBuildSettings.buildAppBundle = oldAppBundle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = oldExportProject;
            Debug.Log(Marker + " RESTORE_PLAYER_SETTINGS completed.");
        }

        if (report == null || report.summary.result != BuildResult.Succeeded ||
            !File.Exists(apk))
        {
            Debug.LogError(Marker + " APK_FAIL result=" +
                (report == null ? "no_report" : report.summary.result.ToString()) +
                " errors=" + (report == null ? -1 : report.summary.totalErrors));
            return 1;
        }
        int oldApksRemoved = CleanSuccessfulBuildArtifacts(outputDirectory, apk);
        Debug.Log(Marker + " APK_OLD_BUILD_CLEANUP removed=" + oldApksRemoved +
            " onlyKnownDevelopmentApks=true currentPreserved=true");
        Debug.Log(Marker + " APK_BUILD_PASS reportTotalBytes=" +
            report.summary.totalSize + " apkFileBytes=" +
            new FileInfo(apk).Length + " file=" + apk +
            " (Android development build only; no device or backend proof).");
        return 0;
    }
}
#endif
