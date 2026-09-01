using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class AtlasBoardTwoClientLobbyTestV1
{
    private const string BuildFolderName =
        "AtlasBoard_3D3C_GuestBuild";

    private const string ExecutableName =
        "AtlasBoardGuest.exe";

    [MenuItem(
        "Atlas Board/Online/Build Two-Client Guest Test v1",
        false,
        455)]
    public static void BuildGuest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError(
                "Stop Play Mode before building the 3D.3C guest client.");
            return;
        }

        Scene activeScene =
            SceneManager.GetActiveScene();

        if (!activeScene.IsValid() ||
            string.IsNullOrWhiteSpace(
                activeScene.path))
        {
            Debug.LogError(
                "3D.3C guest build aborted: the currently active Unity scene has no saved asset path. Save the Main Menu/Lobby scene first.");
            return;
        }

        if (activeScene.isDirty)
        {
            if (!EditorSceneManager.SaveScene(
                    activeScene))
            {
                Debug.LogError(
                    "3D.3C guest build aborted: could not save the active Main Menu/Lobby scene.");
                return;
            }
        }

        List<string> scenePaths =
            new List<string>
            {
                activeScene.path
            };

        foreach (EditorBuildSettingsScene scene in
                 EditorBuildSettings.scenes)
        {
            if (scene == null ||
                !scene.enabled ||
                string.IsNullOrWhiteSpace(
                    scene.path) ||
                string.Equals(
                    scene.path,
                    activeScene.path,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            scenePaths.Add(
                scene.path);
        }

        string[] scenes =
            scenePaths.ToArray();

        Debug.Log(
            "AtlasBoard 3D.3C Guest startup scene forced to current active scene: " +
            activeScene.path);

        string outputFolder =
            GetBuildFolder();

        if (Directory.Exists(
                outputFolder))
        {
            Directory.Delete(
                outputFolder,
                true);
        }

        Directory.CreateDirectory(
            outputFolder);

        string executable =
            GetExecutablePath();

        BuildPlayerOptions options =
            new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = executable,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

        Debug.Log(
            "AtlasBoard 3D.3C: building standalone Guest development client. " +
            $"Output={executable}");

        BuildReport report =
            BuildPipeline.BuildPlayer(
                options);

        if (report.summary.result !=
            BuildResult.Succeeded)
        {
            Debug.LogError(
                "AtlasBoard 3D.3C guest build FAILED. " +
                $"Result={report.summary.result}, " +
                $"Errors={report.summary.totalErrors}.");
            return;
        }

        WriteLaunchScript();

        Debug.Log(
            "AtlasBoard 3D.3C guest build PASSED. " +
            $"Size={report.summary.totalSize} bytes. " +
            "The build is outside the Unity repository and uses local Firebase emulators only when launched with the supplied test arguments.");
    }

    [MenuItem(
        "Atlas Board/Online/Launch Two-Client Guest Test v1",
        false,
        456)]
    public static void LaunchGuest()
    {
        string executable =
            GetExecutablePath();

        if (!File.Exists(
                executable))
        {
            Debug.LogError(
                "3D.3C Guest executable was not found. Run " +
                "Atlas Board -> Online -> Build Two-Client Guest Test v1 first.");
            return;
        }

        ProcessStartInfo info =
            new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory =
                    Path.GetDirectoryName(
                        executable) ?? string.Empty,
                UseShellExecute = true,
                Arguments =
                    "-atlasLocalEmulators " +
                    "-atlasDevName=\"Guest Player\" " +
                    "-screen-width 1280 " +
                    "-screen-height 720 " +
                    "-screen-fullscreen 0"
            };

        Process.Start(
            info);

        Debug.Log(
            "AtlasBoard 3D.3C Guest launched with localhost Firebase emulators and a separate temporary Auth identity.");
    }

    [MenuItem(
        "Atlas Board/Online/Open Two-Client Guest Build Folder",
        false,
        457)]
    public static void OpenBuildFolder()
    {
        string folder =
            GetBuildFolder();

        Directory.CreateDirectory(
            folder);

        EditorUtility.RevealInFinder(
            folder);
    }

    [MenuItem(
        "Atlas Board/Online/Validate Two-Client Test Support v1",
        false,
        458)]
    public static void Validate()
    {
        AtlasBoardLobbyRuntimeBridge bridge =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardLobbyRuntimeBridge>(
                FindObjectsInactive.Include);

        if (bridge == null)
        {
            Debug.LogError(
                "AtlasBoard 3D.3C validation FAILED: AtlasBoardLobbyRuntimeBridge is not present in the loaded scene.");
            return;
        }

        Scene activeScene =
            SceneManager.GetActiveScene();

        if (!activeScene.IsValid() ||
            string.IsNullOrWhiteSpace(
                activeScene.path))
        {
            Debug.LogError(
                "AtlasBoard 3D.3C validation FAILED: active scene is not a saved scene asset.");
            return;
        }

        Debug.Log(
            "AtlasBoard Two-Client Lobby Test support v1 static validation PASSED. " +
            "Guest startup scene will be the CURRENT ACTIVE SCENE: " +
            activeScene.path +
            ". Actual PASS still requires Editor Host + standalone Guest against the same local Firebase emulators.");
    }

    private static string GetBuildFolder()
    {
        string projectRoot =
            Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    ".."));

        DirectoryInfo project =
            new DirectoryInfo(
                projectRoot);

        string parent =
            project.Parent != null
                ? project.Parent.FullName
                : projectRoot;

        return Path.Combine(
            parent,
            BuildFolderName);
    }

    private static string GetExecutablePath()
    {
        return Path.Combine(
            GetBuildFolder(),
            ExecutableName);
    }

    private static void WriteLaunchScript()
    {
        string folder =
            GetBuildFolder();

        string batch =
            Path.Combine(
                folder,
                "Launch_Guest_Local_Emulators.bat");

        string contents =
            "@echo off\r\n" +
            "cd /d \"%~dp0\"\r\n" +
            "start \"Atlas Board Guest\" \"" +
            ExecutableName +
            "\" -atlasLocalEmulators -atlasDevName=\"Guest Player\" -screen-width 1280 -screen-height 720 -screen-fullscreen 0\r\n";

        File.WriteAllText(
            batch,
            contents);
    }
}
