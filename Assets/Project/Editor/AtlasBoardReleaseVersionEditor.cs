#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Align a legacy imported AtlasBoard project with the 0.9.0 beta release label.
/// Only upgrades the known 0.1.0 prototype ProjectSettings value. Never
/// overrides a version already changed by the owner for a later release.
/// </summary>
[InitializeOnLoad]
public static class AtlasBoardReleaseVersionEditor
{
    private const string NewVersion = "0.9.0-beta.1";

    static AtlasBoardReleaseVersionEditor()
    {
        EditorApplication.delayCall += UpgradeLegacyProjectVersion;
    }

    private static void UpgradeLegacyProjectVersion()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string current = PlayerSettings.bundleVersion ?? string.Empty;
        if (string.Equals(current, "0.5.0", StringComparison.Ordinal) ||
            string.Equals(current, "0.1.0", StringComparison.Ordinal) ||
            string.Equals(current, "0.1", StringComparison.Ordinal))
        {
            PlayerSettings.bundleVersion = NewVersion;
            Debug.Log("ATB_VERSION_PROJECT upgraded_from=" + current +
                " version=" + NewVersion + " PlayerSettings=true");
        }
        if (string.Equals(PlayerSettings.bundleVersion, NewVersion, StringComparison.Ordinal) &&
            PlayerSettings.Android.bundleVersionCode < 9)
            PlayerSettings.Android.bundleVersionCode = 9;
    }
}
#endif
