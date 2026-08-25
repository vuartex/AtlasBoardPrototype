using UnityEngine;

[System.Serializable]
public struct AtlasBoardGraphicsSettingsValues
{
    public int ResolutionWidth;
    public int ResolutionHeight;
    public int RefreshRate;

    // 0 = Exclusive Fullscreen
    // 1 = Borderless
    // 2 = Windowed
    public int DisplayMode;

    // 0 = Low, 1 = Medium, 2 = High, 3 = Very High
    public int QualityPreset;

    public bool VSync;

    // 0 = Unlimited
    public int FpsLimit;

    // 0 = Off, 1 = Low, 2 = Medium, 3 = High
    public int ShadowQuality;

    // 0 = Off, 1 = 2x, 2 = 4x, 3 = 8x
    public int AntiAliasing;

    public bool ShowFps;

    public static AtlasBoardGraphicsSettingsValues Default
    {
        get
        {
            Resolution current =
                Screen.currentResolution;

            int refresh =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        (float)current.refreshRateRatio.value));

            return new AtlasBoardGraphicsSettingsValues
            {
                ResolutionWidth =
                    Mathf.Max(
                        1280,
                        current.width),

                ResolutionHeight =
                    Mathf.Max(
                        720,
                        current.height),

                RefreshRate =
                    refresh,

                DisplayMode = 1,
                QualityPreset = 2,
                VSync = true,
                FpsLimit = 60,
                ShadowQuality = 3,
                AntiAliasing = 2,
                ShowFps = false
            };
        }
    }
}

[System.Serializable]
public struct AtlasBoardGameplaySettingsValues
{
    // User-facing values are always 1..100.
    // 50 = current tuned AtlasBoard baseline.
    public int CameraSensitivity;
    public int CameraZoomSensitivity;
    public int CameraPanSensitivity;

    public int BotTurnSpeed;

    public bool ReduceCameraMotion;
    public bool UiHints;
    public bool GameplayConfirmations;

    public static AtlasBoardGameplaySettingsValues Default =>
        new AtlasBoardGameplaySettingsValues
        {
            CameraSensitivity = 50,
            CameraZoomSensitivity = 50,
            CameraPanSensitivity = 50,
            BotTurnSpeed = 50,
            ReduceCameraMotion = false,
            UiHints = true,
            GameplayConfirmations = true
        };
}

[System.Serializable]
public struct AtlasBoardUserSettingsValues
{
    public AtlasBoardGraphicsSettingsValues Graphics;
    public AtlasBoardGameplaySettingsValues Gameplay;
    public bool AudioMuted;

    // Reserved for the next Localization phase.
    // No language switching is performed in Settings v2.
    public string LanguageCode;

    public static AtlasBoardUserSettingsValues Default =>
        new AtlasBoardUserSettingsValues
        {
            Graphics =
                AtlasBoardGraphicsSettingsValues.Default,

            Gameplay =
                AtlasBoardGameplaySettingsValues.Default,

            AudioMuted = false,
            LanguageCode = "en"
        };
}

public static class AtlasBoardUserSettingsStore
{
    private const string Prefix =
        "atlasboard.settings.";

    public static AtlasBoardUserSettingsValues Load()
    {
        AtlasBoardUserSettingsValues defaults =
            AtlasBoardUserSettingsValues.Default;

        AtlasBoardUserSettingsValues values =
            defaults;

        values.Graphics.ResolutionWidth =
            PlayerPrefs.GetInt(
                Prefix + "graphics.width",
                defaults.Graphics.ResolutionWidth);

        values.Graphics.ResolutionHeight =
            PlayerPrefs.GetInt(
                Prefix + "graphics.height",
                defaults.Graphics.ResolutionHeight);

        values.Graphics.RefreshRate =
            PlayerPrefs.GetInt(
                Prefix + "graphics.refreshRate",
                defaults.Graphics.RefreshRate);

        values.Graphics.DisplayMode =
            PlayerPrefs.GetInt(
                Prefix + "graphics.displayMode",
                defaults.Graphics.DisplayMode);

        values.Graphics.QualityPreset =
            PlayerPrefs.GetInt(
                Prefix + "graphics.qualityPreset",
                defaults.Graphics.QualityPreset);

        values.Graphics.VSync =
            GetBool(
                Prefix + "graphics.vsync",
                defaults.Graphics.VSync);

        values.Graphics.FpsLimit =
            PlayerPrefs.GetInt(
                Prefix + "graphics.fpsLimit",
                defaults.Graphics.FpsLimit);

        values.Graphics.ShadowQuality =
            PlayerPrefs.GetInt(
                Prefix + "graphics.shadowQuality",
                defaults.Graphics.ShadowQuality);

        values.Graphics.AntiAliasing =
            PlayerPrefs.GetInt(
                Prefix + "graphics.antiAliasing",
                defaults.Graphics.AntiAliasing);

        values.Graphics.ShowFps =
            GetBool(
                Prefix + "graphics.showFps",
                defaults.Graphics.ShowFps);

        values.Gameplay.CameraSensitivity =
            PlayerPrefs.GetInt(
                Prefix + "gameplay.cameraSensitivity",
                defaults.Gameplay.CameraSensitivity);

        values.Gameplay.CameraZoomSensitivity =
            PlayerPrefs.GetInt(
                Prefix + "gameplay.cameraZoomSensitivity",
                defaults.Gameplay.CameraZoomSensitivity);

        values.Gameplay.CameraPanSensitivity =
            PlayerPrefs.GetInt(
                Prefix + "gameplay.cameraPanSensitivity",
                defaults.Gameplay.CameraPanSensitivity);

        values.Gameplay.BotTurnSpeed =
            PlayerPrefs.GetInt(
                Prefix + "gameplay.botTurnSpeed",
                defaults.Gameplay.BotTurnSpeed);

        values.Gameplay.ReduceCameraMotion =
            GetBool(
                Prefix + "gameplay.reduceCameraMotion",
                defaults.Gameplay.ReduceCameraMotion);

        values.Gameplay.UiHints =
            GetBool(
                Prefix + "gameplay.uiHints",
                defaults.Gameplay.UiHints);

        values.Gameplay.GameplayConfirmations =
            GetBool(
                Prefix + "gameplay.confirmations",
                defaults.Gameplay.GameplayConfirmations);

        values.AudioMuted =
            GetBool(
                Prefix + "audio.muted",
                defaults.AudioMuted);

        values.LanguageCode =
            PlayerPrefs.GetString(
                Prefix + "language",
                defaults.LanguageCode);

        return Clamp(values);
    }

    public static void Save(
        AtlasBoardUserSettingsValues values)
    {
        values =
            Clamp(values);

        PlayerPrefs.SetInt(
            Prefix + "graphics.width",
            values.Graphics.ResolutionWidth);

        PlayerPrefs.SetInt(
            Prefix + "graphics.height",
            values.Graphics.ResolutionHeight);

        PlayerPrefs.SetInt(
            Prefix + "graphics.refreshRate",
            values.Graphics.RefreshRate);

        PlayerPrefs.SetInt(
            Prefix + "graphics.displayMode",
            values.Graphics.DisplayMode);

        PlayerPrefs.SetInt(
            Prefix + "graphics.qualityPreset",
            values.Graphics.QualityPreset);

        SetBool(
            Prefix + "graphics.vsync",
            values.Graphics.VSync);

        PlayerPrefs.SetInt(
            Prefix + "graphics.fpsLimit",
            values.Graphics.FpsLimit);

        PlayerPrefs.SetInt(
            Prefix + "graphics.shadowQuality",
            values.Graphics.ShadowQuality);

        PlayerPrefs.SetInt(
            Prefix + "graphics.antiAliasing",
            values.Graphics.AntiAliasing);

        SetBool(
            Prefix + "graphics.showFps",
            values.Graphics.ShowFps);

        PlayerPrefs.SetInt(
            Prefix + "gameplay.cameraSensitivity",
            values.Gameplay.CameraSensitivity);

        PlayerPrefs.SetInt(
            Prefix + "gameplay.cameraZoomSensitivity",
            values.Gameplay.CameraZoomSensitivity);

        PlayerPrefs.SetInt(
            Prefix + "gameplay.cameraPanSensitivity",
            values.Gameplay.CameraPanSensitivity);

        PlayerPrefs.SetInt(
            Prefix + "gameplay.botTurnSpeed",
            values.Gameplay.BotTurnSpeed);

        SetBool(
            Prefix + "gameplay.reduceCameraMotion",
            values.Gameplay.ReduceCameraMotion);

        SetBool(
            Prefix + "gameplay.uiHints",
            values.Gameplay.UiHints);

        SetBool(
            Prefix + "gameplay.confirmations",
            values.Gameplay.GameplayConfirmations);

        SetBool(
            Prefix + "audio.muted",
            values.AudioMuted);

        PlayerPrefs.SetString(
            Prefix + "language",
            string.IsNullOrWhiteSpace(
                values.LanguageCode)
                    ? "en"
                    : values.LanguageCode);

        PlayerPrefs.Save();
    }

    public static AtlasBoardUserSettingsValues Clamp(
        AtlasBoardUserSettingsValues values)
    {
        values.Graphics.ResolutionWidth =
            Mathf.Max(
                640,
                values.Graphics.ResolutionWidth);

        values.Graphics.ResolutionHeight =
            Mathf.Max(
                480,
                values.Graphics.ResolutionHeight);

        values.Graphics.RefreshRate =
            Mathf.Max(
                1,
                values.Graphics.RefreshRate);

        values.Graphics.DisplayMode =
            Mathf.Clamp(
                values.Graphics.DisplayMode,
                0,
                2);

        values.Graphics.QualityPreset =
            Mathf.Clamp(
                values.Graphics.QualityPreset,
                0,
                3);

        values.Graphics.FpsLimit =
            Mathf.Max(
                0,
                values.Graphics.FpsLimit);

        values.Graphics.ShadowQuality =
            Mathf.Clamp(
                values.Graphics.ShadowQuality,
                0,
                3);

        values.Graphics.AntiAliasing =
            Mathf.Clamp(
                values.Graphics.AntiAliasing,
                0,
                3);

        values.Gameplay.CameraSensitivity =
            Mathf.Clamp(
                values.Gameplay.CameraSensitivity,
                1,
                100);

        values.Gameplay.CameraZoomSensitivity =
            Mathf.Clamp(
                values.Gameplay.CameraZoomSensitivity,
                1,
                100);

        values.Gameplay.CameraPanSensitivity =
            Mathf.Clamp(
                values.Gameplay.CameraPanSensitivity,
                1,
                100);

        values.Gameplay.BotTurnSpeed =
            Mathf.Clamp(
                values.Gameplay.BotTurnSpeed,
                1,
                100);

        if (string.IsNullOrWhiteSpace(
                values.LanguageCode))
        {
            values.LanguageCode = "en";
        }

        return values;
    }

    private static bool GetBool(
        string key,
        bool fallback)
    {
        return PlayerPrefs.GetInt(
                   key,
                   fallback ? 1 : 0) !=
               0;
    }

    private static void SetBool(
        string key,
        bool value)
    {
        PlayerPrefs.SetInt(
            key,
            value ? 1 : 0);
    }
}

public static class AtlasBoardUserSettingsRuntime
{
    private static AtlasBoardUserSettingsValues current =
        AtlasBoardUserSettingsValues.Default;

    public static AtlasBoardUserSettingsValues Current =>
        current;

    public static bool UiHintsEnabled =>
        current.Gameplay.UiHints;

    public static bool GameplayConfirmationsEnabled =>
        current.Gameplay.GameplayConfirmations;

    public static bool ShowFps =>
        current.Graphics.ShowFps;

    public static bool ReduceCameraMotion =>
        current.Gameplay.ReduceCameraMotion;

    public static void SetCurrent(
        AtlasBoardUserSettingsValues values)
    {
        current =
            AtlasBoardUserSettingsStore.Clamp(
                values);
    }

    // 50 = 1x
    // 100 = 3x
    // 1 ~= 0.34x
    public static float PercentToThreeXMultiplier(
        int percent)
    {
        float clamped =
            Mathf.Clamp(
                percent,
                1,
                100);

        return Mathf.Pow(
            3f,
            (clamped - 50f) /
            50f);
    }

    public static float ScaleBotDelay(
        float baselineDelay)
    {
        if (baselineDelay <= 0f)
        {
            return baselineDelay;
        }

        float speedMultiplier =
            PercentToThreeXMultiplier(
                current.Gameplay.BotTurnSpeed);

        return baselineDelay /
               Mathf.Max(
                   0.05f,
                   speedMultiplier);
    }
}
