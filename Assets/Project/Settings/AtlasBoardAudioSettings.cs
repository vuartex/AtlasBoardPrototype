using UnityEngine;

[System.Serializable]
public struct AtlasBoardAudioSettingsValues
{
    [Range(0f, 1f)]
    public float Master;

    [Range(0f, 1f)]
    public float MainMusic;

    [Range(0f, 1f)]
    public float Theme;

    [Range(0f, 1f)]
    public float Dice;

    [Range(0f, 1f)]
    public float Effects;

    public static AtlasBoardAudioSettingsValues Default =>
        new AtlasBoardAudioSettingsValues
        {
            Master = 1.00f,
            MainMusic = 0.40f,
            Theme = 0.80f,
            Dice = 0.70f,
            Effects = 0.30f
        };
}

public static class AtlasBoardAudioSettings
{
    public static event System.Action SettingsSaved;

    private const string KeyMaster =
        "atlasboard.audio.master";

    private const string KeyMainMusic =
        "atlasboard.audio.mainMusic";

    private const string KeyTheme =
        "atlasboard.audio.theme";

    private const string KeyDice =
        "atlasboard.audio.dice";

    private const string KeyEffects =
        "atlasboard.audio.effects";

    public static AtlasBoardAudioSettingsValues Load()
    {
        AtlasBoardAudioSettingsValues defaults =
            AtlasBoardAudioSettingsValues.Default;

        return new AtlasBoardAudioSettingsValues
        {
            Master =
                PlayerPrefs.GetFloat(
                    KeyMaster,
                    defaults.Master),

            MainMusic =
                PlayerPrefs.GetFloat(
                    KeyMainMusic,
                    defaults.MainMusic),

            Theme =
                PlayerPrefs.GetFloat(
                    KeyTheme,
                    defaults.Theme),

            Dice =
                PlayerPrefs.GetFloat(
                    KeyDice,
                    defaults.Dice),

            Effects =
                PlayerPrefs.GetFloat(
                    KeyEffects,
                    defaults.Effects)
        };
    }

    public static void Save(
        AtlasBoardAudioSettingsValues values)
    {
        values =
            Clamp(values);

        PlayerPrefs.SetFloat(
            KeyMaster,
            values.Master);

        PlayerPrefs.SetFloat(
            KeyMainMusic,
            values.MainMusic);

        PlayerPrefs.SetFloat(
            KeyTheme,
            values.Theme);

        PlayerPrefs.SetFloat(
            KeyDice,
            values.Dice);

        PlayerPrefs.SetFloat(
            KeyEffects,
            values.Effects);

        PlayerPrefs.Save();

        SettingsSaved?.Invoke();
    }

    public static AtlasBoardAudioSettingsValues Clamp(
        AtlasBoardAudioSettingsValues values)
    {
        values.Master =
            Mathf.Clamp01(values.Master);

        values.MainMusic =
            Mathf.Clamp01(values.MainMusic);

        values.Theme =
            Mathf.Clamp01(values.Theme);

        values.Dice =
            Mathf.Clamp01(values.Dice);

        values.Effects =
            Mathf.Clamp01(values.Effects);

        return values;
    }
}
