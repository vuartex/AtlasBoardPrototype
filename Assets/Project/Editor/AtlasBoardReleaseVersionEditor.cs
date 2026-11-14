#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keep the existing ProjectSettings version consistent with the owner-approved
/// current release family. It upgrades only the exact prior AtlasBoard build;
/// later versions are never overwritten by this Editor initializer.
/// </summary>
[InitializeOnLoad]
public static class AtlasBoardReleaseVersionEditor
{
    private const string NewVersion = "0.9.1";

    static AtlasBoardReleaseVersionEditor()
    {
        EditorApplication.delayCall += UpgradeLegacyProjectVersion;
    }

    private static void UpgradeLegacyProjectVersion()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string current = PlayerSettings.bundleVersion ?? string.Empty;
        if (string.Equals(current, "0.9.0-beta.1", StringComparison.Ordinal) ||
            string.Equals(current, "0.5.0", StringComparison.Ordinal) ||
            string.Equals(current, "0.1.0", StringComparison.Ordinal) ||
            string.Equals(current, "0.1", StringComparison.Ordinal))
        {
            PlayerSettings.bundleVersion = NewVersion;
            Debug.Log("ATB_VERSION_PROJECT upgraded_from=" + current +
                " version=" + NewVersion + " PlayerSettings=true");
        }
        if (string.Equals(PlayerSettings.bundleVersion, NewVersion, StringComparison.Ordinal) &&
            PlayerSettings.Android.bundleVersionCode < 10)
            PlayerSettings.Android.bundleVersionCode = 10;
    }
}
#endif
