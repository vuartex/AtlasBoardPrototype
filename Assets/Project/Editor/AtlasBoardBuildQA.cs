#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Non-building Unity project readiness gate.
/// Windows guest builds remain owned by AtlasBoardTwoClientLobbyTestV1.
/// </summary>
public static class AtlasBoardBuildQA
{
    private const string MainScene = "Assets/Board_Prototype.unity";
    private const string Menu = "Atlas Board/QA & Diagnostics/QA/";

    [MenuItem(Menu + "Validate Build Readiness")]
    public static void ValidateReadiness()
    {
        int errors = Validate();
        if (errors != 0)
        {
            Debug.LogError(
                "AtlasBoard readiness FAILED: " + errors +
                " error(s). No build was attempted.");
            return;
        }

        Debug.Log(
            "AtlasBoard build readiness PASS (Editor structure only). " +
            "Build the guest using Atlas Board > Online & Backend > " +
            "Online > Build Two-Client Guest Test v1. " +
            "Runtime, graphics, localization and real multiplayer still require QA.");
    }

    /// <summary>
    /// Unity batchmode gate:
    /// -executeMethod AtlasBoardBuildQA.BatchValidate
    /// Does not generate a player build.
    /// </summary>
    public static void BatchValidate()
    {
        int errors = Validate();
        if (errors == 0)
        {
            Debug.Log("AtlasBoard batch readiness PASS (structure only).");
        }
        else
        {
            Debug.LogError(
                "AtlasBoard batch readiness FAILED: " + errors);
        }

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(errors == 0 ? 0 : 1);
        }
    }

    private static int Validate()
    {
        int errors = 0;

        if (EditorApplication.isCompiling)
        {
            Debug.LogError(
                "Wait until Unity script compilation finishes.");
            errors++;
        }

        if (EditorUserBuildSettings.activeBuildTarget !=
            BuildTarget.StandaloneWindows64)
        {
            Debug.LogError(
                "Set the active Unity build target to Windows x86_64.");
            errors++;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScene) == null)
        {
            Debug.LogError(
                "AtlasBoard main scene is missing: " + MainScene);
            errors++;
        }

        if (string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))
        {
            Debug.LogError("PlayerSettings.bundleVersion is empty.");
            errors++;
        }

        bool mainSceneEnabled = false;
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene != null &&
                scene.enabled &&
                string.Equals(
                    scene.path, MainScene,
                    StringComparison.OrdinalIgnoreCase))
            {
                mainSceneEnabled = true;
                break;
            }
        }

        if (!mainSceneEnabled)
        {
            Debug.LogError(
                "Enable Board_Prototype in Unity Build Settings.");
            errors++;
        }

        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() &&
            !string.Equals(
                active.path, MainScene,
                StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning(
                "Two-Client Guest Test uses the CURRENT active scene as " +
                "its startup scene. For the regular multiplayer QA flow, " +
                "open Assets/Board_Prototype.unity before building.");
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError(
                "Stop Play Mode before validating or building.");
            errors++;
        }

        return errors;
    }
}
#endif
