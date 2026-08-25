using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "AudioLibrary_Default",
    menuName = "Atlas Board/Audio/Audio Library")]
public class AtlasBoardAudioLibrary : ScriptableObject
{
    [Serializable]
    public class ThemeAudioEntry
    {
        public string themeId;
        public AudioClip ambienceOrMusic;
    }

    [Header("Main Music")]
    public AudioClip mainMenuMusic;
    public AudioClip gameplayMusic;

    [Header("Theme Audio")]
    public ThemeAudioEntry[] themeAudio =
    {
        new ThemeAudioEntry
        {
            themeId = "classic_table"
        },
        new ThemeAudioEntry
        {
            themeId = "garden"
        },
        new ThemeAudioEntry
        {
            themeId = "beach"
        },
        new ThemeAudioEntry
        {
            themeId = "pavilion"
        },
        new ThemeAudioEntry
        {
            themeId = "street"
        }
    };

    [Header("Dice")]
    public AudioClip[] diceRolls;

    [Header("UI")]
    public AudioClip uiClick;
    public AudioClip uiSelect;
    public AudioClip uiOpen;
    public AudioClip uiToggle;
    public AudioClip uiError;

    [Header("Board Effects")]
    public AudioClip pawnMove;
    public AudioClip card;
    public AudioClip coin;
    public AudioClip purchase;
    public AudioClip rent;
    public AudioClip auction;
    public AudioClip trade;
    public AudioClip success;
    public AudioClip warning;

    public AudioClip GetThemeClip(
        string themeId)
    {
        if (themeAudio == null ||
            string.IsNullOrWhiteSpace(
                themeId))
        {
            return null;
        }

        foreach (ThemeAudioEntry entry
                 in themeAudio)
        {
            if (entry == null)
            {
                continue;
            }

            if (string.Equals(
                    entry.themeId,
                    themeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return entry.ambienceOrMusic;
            }
        }

        return null;
    }

    public AudioClip GetRandomDice()
    {
        if (diceRolls == null ||
            diceRolls.Length == 0)
        {
            return null;
        }

        int start =
            UnityEngine.Random.Range(
                0,
                diceRolls.Length);

        for (int offset = 0;
             offset < diceRolls.Length;
             offset++)
        {
            int index =
                (start + offset) %
                diceRolls.Length;

            if (diceRolls[index] != null)
            {
                return diceRolls[index];
            }
        }

        return null;
    }
}
