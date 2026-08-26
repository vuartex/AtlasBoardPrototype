using System;
using System.Globalization;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(-15000)]
[DisallowMultipleComponent]
public class AtlasBoardLocalizationManager :
    MonoBehaviour
{
    public static AtlasBoardLocalizationManager Instance
    {
        get;
        private set;
    }

    public static event Action LanguageChanged;

    [SerializeField]
    private AtlasBoardLocalizationDatabase database;

    [SerializeField]
    private AtlasBoardLocalizationFontProfile fontProfile;

    [SerializeField]
    private string currentLanguageCode =
        "en";

    public string CurrentLanguageCode =>
        currentLanguageCode;

    public AtlasBoardLocalizationDatabase Database =>
        database;

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        AtlasBoardUserSettingsValues settings =
            AtlasBoardUserSettingsStore.Load();

        currentLanguageCode =
            AtlasBoardLocalizationLanguages.Normalize(
                settings.LanguageCode);

        DontDestroyOnLoad(
            gameObject);
    }

    private void Start()
    {
        LanguageChanged?.Invoke();
    }

    public void SetLanguage(
        string languageCode)
    {
        string normalized =
            AtlasBoardLocalizationLanguages.Normalize(
                languageCode);

        if (string.Equals(
                currentLanguageCode,
                normalized,
                StringComparison.OrdinalIgnoreCase))
        {
            RefreshAll();
            return;
        }

        currentLanguageCode =
            normalized;

        RefreshAll();
    }

    public void RefreshAll()
    {
        LanguageChanged?.Invoke();
    }

    public string Translate(
        string key,
        params object[] args)
    {
        string format =
            database != null
                ? database.Get(
                    key,
                    currentLanguageCode)
                : key;

        if (args == null ||
            args.Length == 0)
        {
            return format;
        }

        try
        {
            return string.Format(
                GetCulture(),
                format,
                args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    public Font ResolveLegacyFont(
        Font currentFont)
    {
        if (fontProfile == null)
        {
            return currentFont;
        }

        return fontProfile.GetLegacyFont(
            currentLanguageCode,
            currentFont);
    }

    public TMP_FontAsset ResolveFont(
        TMP_FontAsset currentFont)
    {
        if (fontProfile == null)
        {
            return currentFont;
        }

        return fontProfile.GetFont(
            currentLanguageCode,
            currentFont);
    }

    public CultureInfo GetCulture()
    {
        string cultureName =
            currentLanguageCode switch
            {
                "tr" => "tr-TR",
                "es" => "es-ES",
                "fr" => "fr-FR",
                "de" => "de-DE",
                "ko" => "ko-KR",
                "ru" => "ru-RU",
                _ => "en-US"
            };

        try
        {
            return CultureInfo.GetCultureInfo(
                cultureName);
        }
        catch
        {
            return CultureInfo.InvariantCulture;
        }
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        AtlasBoardLocalizationDatabase newDatabase,
        AtlasBoardLocalizationFontProfile newFontProfile)
    {
        database =
            newDatabase;

        fontProfile =
            newFontProfile;
    }
#endif
}

public static class AtlasBoardL
{
    public static string PlayerName(
        PlayerGameState state)
    {
        if (state == null)
        {
            return T(
                "common.player");
        }

        string raw =
            state.DisplayName ??
            string.Empty;

        string[] defaultPrefixes =
        {
            "Player ",
            "Oyuncu ",
            "Jugador ",
            "Joueur ",
            "Spieler ",
            "플레이어 ",
            "Игрок "
        };

        foreach (string prefix
                 in defaultPrefixes)
        {
            if (raw.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return $"{T("common.player")} " +
                       $"{state.PlayerSlotIndex + 1}";
            }
        }

        return raw;
    }

    public static string TileName(
        TileType tileType,
        string fallback)
    {
        string key =
            tileType switch
            {
                TileType.Start =>
                    "tile.special.start",

                TileType.Event =>
                    "tile.special.event",

                TileType.Tax =>
                    "tile.special.tax",

                TileType.Auction =>
                    "tile.special.auction",

                TileType.Travel =>
                    "tile.special.travel",

                TileType.Vacation =>
                    "tile.special.vacation",

                TileType.RestArea =>
                    "tile.special.rest",

                TileType.Bonus =>
                    "tile.special.bonus",

                _ =>
                    string.Empty
            };

        if (string.IsNullOrWhiteSpace(
                key))
        {
            return fallback;
        }

        string localized =
            T(
                key);

        return string.Equals(
                   localized,
                   key,
                   StringComparison.Ordinal)
            ? fallback
            : localized;
    }

    public static string T(
        string key,
        params object[] args)
    {
        AtlasBoardLocalizationManager manager =
            AtlasBoardLocalizationManager.Instance;

        if (manager == null)
        {
            return args == null ||
                   args.Length == 0
                ? key
                : key;
        }

        return manager.Translate(
            key,
            args);
    }
}
