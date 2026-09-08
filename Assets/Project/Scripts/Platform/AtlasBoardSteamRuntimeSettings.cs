using UnityEngine;

public static class AtlasBoardSteamRuntimeSettings
{
    private const string UseSteamInEditorKey =
        "atlasboard.platform.use_steam_in_editor";

    private const string DevelopmentAppIdKey =
        "atlasboard.platform.steam_dev_app_id";

    public const uint DefaultDevelopmentAppId = 480;

    public static bool UseSteamInEditor
    {
        get =>
            PlayerPrefs.GetInt(
                UseSteamInEditorKey,
                0) == 1;
        set
        {
            PlayerPrefs.SetInt(
                UseSteamInEditorKey,
                value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static uint DevelopmentAppId
    {
        get
        {
            int value =
                PlayerPrefs.GetInt(
                    DevelopmentAppIdKey,
                    (int)DefaultDevelopmentAppId);

            return value > 0
                ? (uint)value
                : DefaultDevelopmentAppId;
        }
        set
        {
            uint safeValue =
                value > 0
                    ? value
                    : DefaultDevelopmentAppId;

            PlayerPrefs.SetInt(
                DevelopmentAppIdKey,
                (int)Mathf.Min(
                    safeValue,
                    int.MaxValue));
            PlayerPrefs.Save();
        }
    }

    public static bool IsDesktopSteamCandidate
    {
        get
        {
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
