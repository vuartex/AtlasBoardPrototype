using System;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(14750)]
[DisallowMultipleComponent]
public sealed class AtlasBoardPhase14FinalLocalizationGuard :
    MonoBehaviour
{
    private const float RefreshInterval = 0.35f;

    private static AtlasBoardPhase14FinalLocalizationGuard
        instance;

    private static readonly FieldInfo GroupDisplayNameField =
        typeof(BoardTile).GetField(
            "groupDisplayName",
            BindingFlags.Instance |
            BindingFlags.NonPublic);

    private float nextRefreshAt;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        AtlasBoardPhase14FinalLocalizationGuard existing =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardPhase14FinalLocalizationGuard>();

        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject root =
            new GameObject(
                "AtlasBoard_Phase14_FinalLocalizationGuard");

        DontDestroyOnLoad(
            root);

        instance =
            root.AddComponent<
                AtlasBoardPhase14FinalLocalizationGuard>();
    }

    private void Awake()
    {
        if (instance != null &&
            instance != this)
        {
            Destroy(
                gameObject);
            return;
        }

        instance = this;
    }

    private void OnEnable()
    {
        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;

        nextRefreshAt =
            0f;
    }

    private void OnDisable()
    {
        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;
    }

    private void Update()
    {
        if (Time.unscaledTime <
            nextRefreshAt)
        {
            return;
        }

        nextRefreshAt =
            Time.unscaledTime +
            RefreshInterval;

        RefreshPresentation();
    }

    private void HandleLanguageChanged()
    {
        RefreshPresentation();
        nextRefreshAt =
            Time.unscaledTime +
            RefreshInterval;
    }

    private static void RefreshPresentation()
    {
        AtlasBoardLocalizationManager manager =
            AtlasBoardLocalizationManager.Instance;

        if (manager == null)
        {
            return;
        }

        RefreshGenericGroupNames();
        RefreshTmpLegacyText(
            manager);
        RefreshLegacyText(
            manager);
    }

    private static void RefreshGenericGroupNames()
    {
        if (GroupDisplayNameField == null)
        {
            return;
        }

        BoardTile[] tiles =
            UnityEngine.Object.FindObjectsByType<
                BoardTile>(
                    FindObjectsInactive.Include);

        foreach (BoardTile tile
                 in tiles)
        {
            if (tile == null ||
                tile.TileType !=
                    TileType.City)
            {
                continue;
            }

            if (!TryGetGroupNumber(
                    tile,
                    out int groupNumber))
            {
                continue;
            }

            string raw =
                tile.GroupDisplayName;

            if (!string.IsNullOrWhiteSpace(
                    raw) &&
                !LooksLikeGenericGroupName(
                    raw))
            {
                continue;
            }

            string localized =
                AtlasBoardL.T(
                    "development.group_number",
                    groupNumber);

            if (string.IsNullOrWhiteSpace(
                    localized) ||
                string.Equals(
                    localized,
                    "development.group_number",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(
                    raw,
                    localized,
                    StringComparison.Ordinal))
            {
                GroupDisplayNameField.SetValue(
                    tile,
                    localized);
            }
        }
    }

    private static void RefreshTmpLegacyText(
        AtlasBoardLocalizationManager manager)
    {
        TMP_Text[] texts =
            UnityEngine.Object.FindObjectsByType<
                TMP_Text>(
                    FindObjectsInactive.Include);

        foreach (TMP_Text text
                 in texts)
        {
            if (text == null ||
                string.IsNullOrWhiteSpace(
                    text.text) ||
                !TryTranslateKnownLegacyText(
                    text.text,
                    out string localized))
            {
                continue;
            }

            text.text =
                localized;

            TMP_FontAsset current =
                text.font != null
                    ? text.font
                    : TMP_Settings.defaultFontAsset;

            TMP_FontAsset resolved =
                manager.ResolveFont(
                    current);

            if (resolved != null)
            {
                text.font =
                    resolved;
            }
        }
    }

    private static void RefreshLegacyText(
        AtlasBoardLocalizationManager manager)
    {
        Text[] texts =
            UnityEngine.Object.FindObjectsByType<
                Text>(
                    FindObjectsInactive.Include);

        foreach (Text text
                 in texts)
        {
            if (text == null ||
                string.IsNullOrWhiteSpace(
                    text.text) ||
                !TryTranslateKnownLegacyText(
                    text.text,
                    out string localized))
            {
                continue;
            }

            text.text =
                localized;

            Font resolved =
                manager.ResolveLegacyFont(
                    text.font);

            if (resolved != null)
            {
                text.font =
                    resolved;
            }
        }
    }

    private static bool TryTranslateKnownLegacyText(
        string source,
        out string localized)
    {
        localized =
            null;

        string trimmed =
            source.Trim();

        if (string.Equals(
                trimmed,
                "HARİTA YOK",
                StringComparison.OrdinalIgnoreCase))
        {
            localized =
                AtlasBoardL.R(
                    "NO MAPS",
                    "HARİTA YOK",
                    "SIN MAPAS",
                    "AUCUNE CARTE",
                    "KEINE KARTEN",
                    "맵 없음",
                    "НЕТ КАРТ");
            return true;
        }

        if (string.Equals(
                trimmed,
                "Eksik Harita",
                StringComparison.OrdinalIgnoreCase))
        {
            localized =
                AtlasBoardL.R(
                    "Missing Map",
                    "Eksik Harita",
                    "Mapa ausente",
                    "Carte manquante",
                    "Fehlende Karte",
                    "누락된 맵",
                    "Карта отсутствует");
            return true;
        }

        if (string.Equals(
                trimmed,
                "İNSAN",
                StringComparison.OrdinalIgnoreCase))
        {
            localized =
                AtlasBoardL.R(
                    "HUMAN",
                    "İNSAN",
                    "HUMANO",
                    "HUMAIN",
                    "MENSCH",
                    "인간",
                    "ЧЕЛОВЕК");
            return true;
        }

        if (string.Equals(
                trimmed,
                "OYUNCU",
                StringComparison.OrdinalIgnoreCase))
        {
            localized =
                AtlasBoardL.R(
                    "PLAYER",
                    "OYUNCU",
                    "JUGADOR",
                    "JOUEUR",
                    "SPIELER",
                    "플레이어",
                    "ИГРОК");
            return true;
        }

        if (string.Equals(
                trimmed,
                "TUR",
                StringComparison.OrdinalIgnoreCase))
        {
            localized =
                AtlasBoardL.R(
                    "ROUND",
                    "TUR",
                    "RONDA",
                    "TOUR",
                    "RUNDE",
                    "라운드",
                    "РАУНД");
            return true;
        }

        if (string.Equals(
                trimmed,
                "HARİTA",
                StringComparison.OrdinalIgnoreCase))
        {
            localized =
                AtlasBoardL.R(
                    "MAP",
                    "HARİTA",
                    "MAPA",
                    "CARTE",
                    "KARTE",
                    "맵",
                    "КАРТА");
            return true;
        }

        if (string.Equals(
                trimmed,
                "Bölge",
                StringComparison.OrdinalIgnoreCase))
        {
            localized =
                AtlasBoardL.R(
                    "GROUP",
                    "BÖLGE",
                    "GRUPO",
                    "GROUPE",
                    "GRUPPE",
                    "그룹",
                    "ГРУППА");
            return true;
        }

        if (string.Equals(
                trimmed,
                "Konum",
                StringComparison.OrdinalIgnoreCase))
        {
            localized =
                AtlasBoardL.R(
                    "LOCATION",
                    "KONUM",
                    "UBICACIÓN",
                    "EMPLACEMENT",
                    "STANDORT",
                    "위치",
                    "МЕСТО");
            return true;
        }

        if (TryParseLeadingNumber(
                trimmed,
                " OYUNCU",
                out int players))
        {
            localized =
                AtlasBoardL.R(
                    "{0} PLAYERS",
                    "{0} OYUNCU",
                    "{0} JUGADORES",
                    "{0} JOUEURS",
                    "{0} SPIELER",
                    "플레이어 {0}명",
                    "{0} ИГРОКА",
                    players);
            return true;
        }

        if (TryParseLeadingNumber(
                trimmed,
                " TUR",
                out int rounds))
        {
            localized =
                AtlasBoardL.R(
                    "{0} ROUNDS",
                    "{0} TUR",
                    "{0} RONDAS",
                    "{0} TOURS",
                    "{0} RUNDEN",
                    "{0} 라운드",
                    "{0} РАУНДОВ",
                    rounds);
            return true;
        }

        if (TryParseGenericGroupNumber(
                trimmed,
                out int groupNumber))
        {
            localized =
                AtlasBoardL.T(
                    "development.group_number",
                    groupNumber);
            return true;
        }

        return false;
    }

    private static bool TryParseLeadingNumber(
        string value,
        string suffix,
        out int number)
    {
        number =
            0;

        if (string.IsNullOrWhiteSpace(
                value) ||
            !value.EndsWith(
                suffix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string numberText =
            value.Substring(
                0,
                value.Length -
                suffix.Length)
                .Trim();

        return int.TryParse(
                   numberText,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out number) &&
               number > 0;
    }

    private static bool TryGetGroupNumber(
        BoardTile tile,
        out int groupNumber)
    {
        groupNumber =
            0;

        if (tile == null)
        {
            return false;
        }

        string id =
            tile.GroupId;

        if (!string.IsNullOrWhiteSpace(
                id))
        {
            int end =
                id.Length - 1;

            while (end >= 0 &&
                   char.IsDigit(
                       id[end]))
            {
                end--;
            }

            if (end <
                id.Length - 1)
            {
                string digits =
                    id.Substring(
                        end + 1);

                if (int.TryParse(
                        digits,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out groupNumber) &&
                    groupNumber > 0)
                {
                    return true;
                }
            }
        }

        return TryParseGenericGroupNumber(
            tile.GroupDisplayName,
            out groupNumber);
    }

    private static bool TryParseGenericGroupNumber(
        string value,
        out int groupNumber)
    {
        groupNumber =
            0;

        if (string.IsNullOrWhiteSpace(
                value) ||
            !LooksLikeGenericGroupName(
                value))
        {
            return false;
        }

        string trimmed =
            value.Trim();

        int index =
            trimmed.Length - 1;

        while (index >= 0 &&
               char.IsDigit(
                   trimmed[index]))
        {
            index--;
        }

        if (index ==
            trimmed.Length - 1)
        {
            return false;
        }

        string digits =
            trimmed.Substring(
                index + 1);

        return int.TryParse(
                   digits,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out groupNumber) &&
               groupNumber > 0;
    }

    private static bool LooksLikeGenericGroupName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        string normalized =
            value.Trim();

        string[] prefixes =
        {
            "Bölge ",
            "BÖLGE ",
            "Group ",
            "GROUP ",
            "Grupo ",
            "GRUPO ",
            "Groupe ",
            "GROUPE ",
            "Gruppe ",
            "GRUPPE ",
            "그룹 ",
            "Группа ",
            "ГРУППА "
        };

        foreach (string prefix
                 in prefixes)
        {
            if (!normalized.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Only treat the complete string as a generic group name
            // when the part after the localized prefix is purely numeric.
            //
            // Example:
            //   "Group 3"          -> generic group name (translate it)
            //   "GROUP • Group 3"  -> composite Purchase UI label (leave it alone)
            //
            // The previous prefix-only check caused the Purchase presentation
            // and the localization guard to rewrite the same TMP text back and
            // forth several times per second.
            string numberText =
                normalized.Substring(
                    prefix.Length)
                    .Trim();

            return int.TryParse(
                       numberText,
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out int groupNumber) &&
                   groupNumber > 0;
        }

        return false;
    }
}
