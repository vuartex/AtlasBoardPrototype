using System;
using System.Collections.Generic;
using UnityEngine;

public static class AtlasBoardLocalizationLanguages
{
    public static readonly string[] Codes =
    {
        "en",
        "tr",
        "es",
        "fr",
        "de",
        "ko",
        "ru"
    };

    public static readonly string[] NativeNames =
    {
        "English",
        "Türkçe",
        "Español",
        "Français",
        "Deutsch",
        "한국어",
        "Русский"
    };

    public static int IndexOf(
        string code)
    {
        if (string.IsNullOrWhiteSpace(
                code))
        {
            return 0;
        }

        for (int i = 0;
             i < Codes.Length;
             i++)
        {
            if (string.Equals(
                    Codes[i],
                    code,
                    StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    public static string Normalize(
        string code)
    {
        return Codes[
            IndexOf(code)];
    }
}

[CreateAssetMenu(
    fileName = "Localization_Default",
    menuName = "Atlas Board/Localization/Database")]
public class AtlasBoardLocalizationDatabase :
    ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string key;

        [TextArea]
        public string en;

        [TextArea]
        public string tr;

        [TextArea]
        public string es;

        [TextArea]
        public string fr;

        [TextArea]
        public string de;

        [TextArea]
        public string ko;

        [TextArea]
        public string ru;

        public string Get(
            string languageCode)
        {
            return languageCode switch
            {
                "tr" => tr,
                "es" => es,
                "fr" => fr,
                "de" => de,
                "ko" => ko,
                "ru" => ru,
                _ => en
            };
        }
    }

    [SerializeField]
    private List<Entry> entries =
        new List<Entry>();

    private Dictionary<string, Entry> cache;

    public IReadOnlyList<Entry> Entries =>
        entries;

    public string Get(
        string key,
        string languageCode)
    {
        EnsureCache();

        if (!cache.TryGetValue(
                key,
                out Entry entry) ||
            entry == null)
        {
            return key;
        }

        string normalized =
            AtlasBoardLocalizationLanguages.Normalize(
                languageCode);

        string value =
            entry.Get(
                normalized);

        if (string.IsNullOrWhiteSpace(
                value))
        {
            value =
                entry.en;
        }

        return string.IsNullOrWhiteSpace(
                   value)
            ? key
            : value;
    }

    public bool HasTranslation(
        string key,
        string languageCode)
    {
        EnsureCache();

        if (!cache.TryGetValue(
                key,
                out Entry entry) ||
            entry == null)
        {
            return false;
        }

        string value =
            entry.Get(
                AtlasBoardLocalizationLanguages.Normalize(
                    languageCode));

        return !string.IsNullOrWhiteSpace(
            value);
    }

    private void EnsureCache()
    {
        if (cache != null)
        {
            return;
        }

        cache =
            new Dictionary<string, Entry>(
                StringComparer.OrdinalIgnoreCase);

        foreach (Entry entry
                 in entries)
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(
                    entry.key))
            {
                continue;
            }

            cache[entry.key] =
                entry;
        }
    }

#if UNITY_EDITOR
    public void EditorReplaceEntries(
        List<Entry> newEntries)
    {
        entries =
            newEntries ??
            new List<Entry>();

        cache = null;
    }
#endif
}
