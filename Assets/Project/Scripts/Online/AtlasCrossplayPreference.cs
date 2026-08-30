using UnityEngine;

public static class AtlasCrossplayPreference
{
    private const string Key =
        "atlasboard.online.crossplay.enabled";

    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(Key, 1) != 0;
        set
        {
            PlayerPrefs.SetInt(Key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
