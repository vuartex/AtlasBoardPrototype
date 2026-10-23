#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Phase 17 / Android QA recovery for Unity 6000.5 Build Profiles cases
/// where the UI shows Android as Active but the effective Editor build target
/// is still StandaloneWindows64. This is an explicitly invoked Editor-only
/// action. No builds, Firebase changes or PlayerSettings mutations occur here.
/// </summary>
public static class AtlasBoardAndroidTargetRecovery
{
    private const string Prefix = "ATLASBOARD_ANDROID_TARGET_RECOVERY";

    [MenuItem("Atlas Board/QA & Diagnostics/QA/Android/0 - Force Android Build Target", false, 599)]
    public static void ForceAndroidBuildTarget()
    {
        BuildTarget before = EditorUserBuildSettings.activeBuildTarget;
        Debug.Log(Prefix + " begin=true editorTargetBefore=" + before +
                  " desired=Android guiActiveBadgeNotVerified=true");

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogError(Prefix + " result=BLOCKED reason=editor_busy_or_play_mode " +
                           "editorTarget=" + before +
                           " action=stop_play_and_wait_for_import");
            return;
        }

        if (!BuildPipeline.IsBuildTargetSupported(
                BuildTargetGroup.Android, BuildTarget.Android))
        {
            Debug.LogError(Prefix + " result=BLOCKED reason=android_build_support_missing " +
                           "action=install_android_build_support_sdk_ndk_openjdk_in_unity_hub");
            return;
        }

        if (before == BuildTarget.Android)
        {
            Debug.Log(Prefix + " result=ALREADY_ANDROID editorTargetAfter=Android " +
                      "action=run_android_readiness_gate");
            return;
        }

        try
        {
            // The synchronous Unity API reimports platform-specific assets
            // before returning. Do not attempt an APK build within this call:
            // Unity may recompile/reload the Editor script domain.
            bool accepted = EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);
            BuildTarget after = EditorUserBuildSettings.activeBuildTarget;

            if (accepted && after == BuildTarget.Android)
            {
                Debug.Log(Prefix + " result=PASS editorTargetBefore=" + before +
                          " editorTargetAfter=" + after +
                          " action=run_android_readiness_gate");
            }
            else
            {
                Debug.LogError(Prefix + " result=FAIL switchAccepted=" + accepted +
                               " editorTargetBefore=" + before +
                               " editorTargetAfter=" + after +
                               " action=inspect_editor_log_and_platform_module");
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(Prefix + " result=EXCEPTION type=" +
                           exception.GetType().Name +
                           " detail=" + exception.Message);
        }
    }
}
#endif
