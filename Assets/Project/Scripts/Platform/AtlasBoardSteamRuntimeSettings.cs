using System;
using UnityEngine;

public static class AtlasBoardSteamRuntimeSettings
{
    private const string UseSteamInEditorKey = "atlasboard.platform.use_steam_in_editor";
    private const string DevelopmentAppIdKey = "atlasboard.platform.steam_dev_app_id";
    public const uint DefaultDevelopmentAppId = 480;

    public static bool UseSteamInEditor
    {
        get => PlayerPrefs.GetInt(UseSteamInEditorKey, 0) == 1;
        set { PlayerPrefs.SetInt(UseSteamInEditorKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static uint DevelopmentAppId
    {
        get
        {
            int value = PlayerPrefs.GetInt(DevelopmentAppIdKey, (int)DefaultDevelopmentAppId);
            return value > 0 ? (uint)value : DefaultDevelopmentAppId;
        }
        set
        {
            uint safe = value > 0 ? value : DefaultDevelopmentAppId;
            PlayerPrefs.SetInt(DevelopmentAppIdKey, (int)Mathf.Min(safe, int.MaxValue));
            PlayerPrefs.Save();
        }
    }

    public static bool IsAccountQaBuild
    {
        get
        {
#if DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return (Application.dataPath ?? string.Empty).EndsWith(
                "AtlasBoardAccountQA_Data", StringComparison.OrdinalIgnoreCase);
#else
            return false;
#endif
        }
    }

    public static bool IsDesktopSteamCandidate
    {
        get
        {
            // Account QA is a separately named Development executable. It
            // requires a native Firebase Auth Guest without a Steam override.
            // Never disable Steam in retail releases, even if renamed.
            if (IsAccountQaBuild) return false;
            return Application.platform switch
            {
                RuntimePlatform.WindowsEditor => true,
                RuntimePlatform.WindowsPlayer => true,
                RuntimePlatform.OSXEditor => true,
                RuntimePlatform.OSXPlayer => true,
                RuntimePlatform.LinuxEditor => true,
                RuntimePlatform.LinuxPlayer => true,
                _ => false
            };
        }
    }
}
