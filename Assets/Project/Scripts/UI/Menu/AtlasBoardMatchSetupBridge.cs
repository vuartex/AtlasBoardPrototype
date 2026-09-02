using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AtlasBoardMatchSetupBridge : MonoBehaviour
{
    [SerializeField]
    private GameObject existingMatchSetupCanvas;

    public bool TryStartExistingMatch(
        AtlasBoardLobbySelection selection)
    {
        if (selection == null)
        {
            return false;
        }

        if (existingMatchSetupCanvas == null)
        {
            existingMatchSetupCanvas =
                FindSceneObject(
                    "Canvas_MatchSetup");
        }

        if (existingMatchSetupCanvas == null)
        {
            Debug.LogWarning(
                "Canvas_MatchSetup was not found.");

            return false;
        }

        existingMatchSetupCanvas.SetActive(true);

        MatchSetupManager reusableSetup =
            existingMatchSetupCanvas.GetComponentInChildren<
                MatchSetupManager>(true);
        reusableSetup?.ResetForNewMatchSession();

        // PASS 1 — set controls that can rebuild/activate legacy rows.
        TMP_Dropdown[] firstPass =
            existingMatchSetupCanvas
                .GetComponentsInChildren<
                    TMP_Dropdown>(true);

        TMP_Dropdown mapDropdown =
            FindMapDropdown(firstPass);

        TMP_Dropdown playerCountDropdown =
            FindPlayerCountDropdown(firstPass);

        TMP_Dropdown roundDropdown =
            FindRoundDropdown(firstPass);

        bool mapMapped =
            SetMapOption(
                mapDropdown,
                selection.MapId);

        bool playerCountMapped =
            SetOption(
                playerCountDropdown,
                selection.PlayerCount.ToString());

        bool roundMapped =
            SetOption(
                roundDropdown,
                selection.RoundLimit.ToString());

        // PASS 2 — re-read after Player Count callback.
        TMP_Dropdown[] refreshed =
            existingMatchSetupCanvas
                .GetComponentsInChildren<
                    TMP_Dropdown>(true);

        List<PlayerTypeControl> playerTypeDropdowns =
            FindPlayerTypeDropdownsInVisualOrder(
                existingMatchSetupCanvas,
                refreshed);

        bool playerTypesMapped =
            ApplyPlayerTypesInOrder(
                playerTypeDropdowns,
                selection);

        ApplyToggles(
            existingMatchSetupCanvas,
            selection);

        Button startButton =
            FindStartButton(
                existingMatchSetupCanvas);

        bool ready =
            mapMapped &&
            playerCountMapped &&
            roundMapped &&
            playerTypesMapped &&
            startButton != null;

        if (!ready)
        {
            existingMatchSetupCanvas.SetActive(false);

            Debug.LogWarning(
                "Main Menu v1.3.6 could not auto-start legacy setup. " +
                $"Map={mapMapped}, " +
                $"PlayerCount={playerCountMapped}, " +
                $"Round={roundMapped}, " +
                $"PlayerTypes={playerTypesMapped}, " +
                $"StartButton={(startButton != null)}.");

            LogPlayerTypeDiagnostics(
                playerTypeDropdowns,
                selection);

            return false;
        }

        LogPlayerTypeDiagnostics(
            playerTypeDropdowns,
            selection);

        startButton.onClick.Invoke();

        if (existingMatchSetupCanvas != null &&
            existingMatchSetupCanvas.activeSelf)
        {
            existingMatchSetupCanvas.SetActive(false);
        }

        Debug.Log(
            "Main Menu v1.3.6 mapped lobby settings and started the match.");

        return true;
    }

    private class PlayerTypeControl
    {
        public TMP_Dropdown Tmp;
        public Dropdown Legacy;

        public Transform Transform =>
            Tmp != null
                ? Tmp.transform
                : Legacy != null
                    ? Legacy.transform
                    : null;

        public string Name =>
            Transform != null
                ? Transform.name
                : "<missing>";

        public int OptionCount
        {
            get
            {
                if (Tmp != null &&
                    Tmp.options != null)
                {
                    return Tmp.options.Count;
                }

                if (Legacy != null &&
                    Legacy.options != null)
                {
                    return Legacy.options.Count;
                }

                return 0;
            }
        }

        public string GetOptionText(
            int index)
        {
            if (Tmp != null &&
                Tmp.options != null &&
                index >= 0 &&
                index < Tmp.options.Count)
            {
                return Tmp.options[index].text;
            }

            if (Legacy != null &&
                Legacy.options != null &&
                index >= 0 &&
                index < Legacy.options.Count)
            {
                return Legacy.options[index].text;
            }

            return string.Empty;
        }

        public string GetCurrentText()
        {
            if (Tmp != null)
            {
                if (Tmp.options == null ||
                    Tmp.options.Count == 0 ||
                    Tmp.value < 0 ||
                    Tmp.value >= Tmp.options.Count)
                {
                    return string.Empty;
                }

                return Tmp.options[
                    Tmp.value].text;
            }

            if (Legacy != null)
            {
                if (Legacy.options == null ||
                    Legacy.options.Count == 0 ||
                    Legacy.value < 0 ||
                    Legacy.value >= Legacy.options.Count)
                {
                    return string.Empty;
                }

                return Legacy.options[
                    Legacy.value].text;
            }

            return string.Empty;
        }

        public bool SetOption(
            string desired)
        {
            for (int i = 0;
                 i < OptionCount;
                 i++)
            {
                if (!OptionMatches(
                        GetOptionText(i),
                        desired))
                {
                    continue;
                }

                if (Tmp != null)
                {
                    Tmp.value = i;
                    Tmp.RefreshShownValue();
                    Tmp.onValueChanged.Invoke(i);
                    return true;
                }

                if (Legacy != null)
                {
                    Legacy.value = i;
                    Legacy.RefreshShownValue();
                    Legacy.onValueChanged.Invoke(i);
                    return true;
                }
            }

            return false;
        }

        public bool HasOption(
            params string[] values)
        {
            for (int i = 0;
                 i < OptionCount;
                 i++)
            {
                string option =
                    GetOptionText(i);

                foreach (string value
                         in values)
                {
                    if (OptionMatches(
                            option,
                            value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    private static List<PlayerTypeControl>
        FindPlayerTypeDropdownsInVisualOrder(
            GameObject legacyRoot,
            TMP_Dropdown[] tmpDropdowns)
    {
        List<PlayerTypeControl> result =
            new List<PlayerTypeControl>();

        // Support TMP_Dropdown.
        foreach (TMP_Dropdown dropdown
                 in tmpDropdowns)
        {
            if (dropdown == null)
            {
                continue;
            }

            PlayerTypeControl control =
                new PlayerTypeControl
                {
                    Tmp = dropdown
                };

            if (control.HasOption(
                    "bot") &&
                control.HasOption(
                    "human",
                    "insan"))
            {
                result.Add(control);
            }
        }

        // Also support legacy UnityEngine.UI.Dropdown.
        // This makes the bridge robust even if the old setup mixes
        // TMP and classic UGUI dropdowns.
        Dropdown[] legacyDropdowns =
            legacyRoot.GetComponentsInChildren<
                Dropdown>(true);

        foreach (Dropdown dropdown
                 in legacyDropdowns)
        {
            if (dropdown == null)
            {
                continue;
            }

            PlayerTypeControl control =
                new PlayerTypeControl
                {
                    Legacy = dropdown
                };

            if (control.HasOption(
                    "bot") &&
                control.HasOption(
                    "human",
                    "insan"))
            {
                result.Add(control);
            }
        }

        result.Sort(
            (a, b) =>
            {
                float ay =
                    GetVisualY(
                        a.Transform);

                float by =
                    GetVisualY(
                        b.Transform);

                int yCompare =
                    by.CompareTo(ay);

                if (yCompare != 0)
                {
                    return yCompare;
                }

                return GetHierarchyPath(
                        a.Transform)
                    .CompareTo(
                        GetHierarchyPath(
                            b.Transform));
            });

        return result;
    }

    private static bool ApplyPlayerTypesInOrder(
        List<PlayerTypeControl> playerTypeDropdowns,
        AtlasBoardLobbySelection selection)
    {
        if (selection.PlayerTypes == null)
        {
            return false;
        }

        if (playerTypeDropdowns.Count <
            selection.PlayerCount)
        {
            Debug.LogWarning(
                $"Expected {selection.PlayerCount} legacy player-type dropdowns, " +
                $"but found {playerTypeDropdowns.Count}.");

            return false;
        }

        bool allMapped = true;

        for (int slot = 0;
             slot < selection.PlayerCount;
             slot++)
        {
            PlayerTypeControl control =
                playerTypeDropdowns[slot];

            string desired =
                slot < selection.PlayerTypes.Length
                    ? selection.PlayerTypes[slot]
                    : "Bot";

            bool mapped =
                control.SetOption(
                    desired);

            string actual =
                control.GetCurrentText();

            bool verified =
                mapped &&
                OptionMatches(
                    actual,
                    desired);

            if (!verified)
            {
                Debug.LogWarning(
                    $"P{slot + 1} type mapping failed. " +
                    $"Lobby='{desired}', Legacy='{actual}', " +
                    $"Control='{control.Name}'.");

                allMapped = false;
            }
        }

        return allMapped;
    }

    private static float GetVisualY(
        Transform transform)
    {
        if (transform == null)
        {
            return float.MinValue;
        }

        RectTransform rect =
            transform as RectTransform;

        if (rect != null)
        {
            Vector3[] corners =
                new Vector3[4];

            rect.GetWorldCorners(corners);

            return
                (corners[0].y +
                 corners[1].y +
                 corners[2].y +
                 corners[3].y) *
                0.25f;
        }

        return transform.position.y;
    }

    private static TMP_Dropdown FindMapDropdown(
        TMP_Dropdown[] dropdowns)
    {
        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            if (dropdown == null)
            {
                continue;
            }

            bool hasTurkey =
                HasOption(
                    dropdown,
                    "turkiye",
                    "turkey");

            bool hasColorado =
                HasOption(
                    dropdown,
                    "colorado");

            bool hasUsa =
                HasOption(
                    dropdown,
                    "usa",
                    "united states",
                    "united states of america",
                    "america",
                    "abd",
                    "amerika",
                    "estados unidos",
                    "ee. uu.",
                    "etats unis",
                    "vereinigte staaten",
                    "미국",
                    "сша");

            if (hasTurkey &&
                (hasColorado || hasUsa))
            {
                return dropdown;
            }
        }

        return FindByContext(
            dropdowns,
            "map",
            "harita");
    }

    private static TMP_Dropdown FindPlayerCountDropdown(
        TMP_Dropdown[] dropdowns)
    {
        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            if (dropdown == null)
            {
                continue;
            }

            bool has2 =
                HasNumericOption(
                    dropdown,
                    2);

            bool has3 =
                HasNumericOption(
                    dropdown,
                    3);

            bool has4 =
                HasNumericOption(
                    dropdown,
                    4);

            bool playerWords =
                OptionsContainAny(
                    dropdown,
                    "oyuncu",
                    "player");

            if (has2 &&
                has3 &&
                has4 &&
                playerWords)
            {
                return dropdown;
            }
        }

        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            if (dropdown == null ||
                dropdown.options == null)
            {
                continue;
            }

            if (dropdown.options.Count == 3 &&
                HasNumericOption(dropdown, 2) &&
                HasNumericOption(dropdown, 3) &&
                HasNumericOption(dropdown, 4))
            {
                return dropdown;
            }
        }

        return FindByContext(
            dropdowns,
            "player count",
            "playercount",
            "oyuncu say");
    }

    private static TMP_Dropdown FindRoundDropdown(
        TMP_Dropdown[] dropdowns)
    {
        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            if (dropdown == null)
            {
                continue;
            }

            bool has10 =
                HasNumericOption(
                    dropdown,
                    10);

            bool has20 =
                HasNumericOption(
                    dropdown,
                    20);

            bool roundWords =
                OptionsContainAny(
                    dropdown,
                    "tur",
                    "round");

            if (has10 &&
                has20 &&
                roundWords)
            {
                return dropdown;
            }
        }

        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            if (dropdown != null &&
                HasNumericOption(dropdown, 10) &&
                HasNumericOption(dropdown, 15) &&
                HasNumericOption(dropdown, 20) &&
                HasNumericOption(dropdown, 30))
            {
                return dropdown;
            }
        }

        return FindByContext(
            dropdowns,
            "round limit",
            "round",
            "tur say");
    }

    private static TMP_Dropdown FindByContext(
        TMP_Dropdown[] dropdowns,
        params string[] tokens)
    {
        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            if (dropdown == null)
            {
                continue;
            }

            string context =
                BuildLocalContext(
                    dropdown.transform);

            if (ContainsAny(
                    context,
                    tokens))
            {
                return dropdown;
            }
        }

        return null;
    }

    private static void ApplyToggles(
        GameObject root,
        AtlasBoardLobbySelection selection)
    {
        Toggle[] toggles =
            root.GetComponentsInChildren<
                Toggle>(true);

        bool balancedMapped = false;
        bool doublesMapped = false;
        bool tripleMapped = false;

        foreach (Toggle toggle in toggles)
        {
            if (toggle == null)
            {
                continue;
            }

            string context =
                BuildLocalContext(
                    toggle.transform);

            if (!balancedMapped &&
                ContainsAny(
                    context,
                    "balanced",
                    "denge",
                    "gelistirme",
                    "development"))
            {
                toggle.isOn =
                    selection.BalancedDevelopment;

                balancedMapped = true;
                continue;
            }

            if (!tripleMapped &&
                ContainsAny(
                    context,
                    "triple",
                    "3 cift",
                    "3 çift",
                    "ceza",
                    "penalty"))
            {
                toggle.isOn =
                    selection
                        .TripleDoublePenaltyEnabled;

                tripleMapped = true;
                continue;
            }

            if (!doublesMapped &&
                ContainsAny(
                    context,
                    "double",
                    "cift zar",
                    "çift zar"))
            {
                toggle.isOn =
                    selection.DoublesEnabled;

                doublesMapped = true;
            }
        }

        if (toggles.Length >= 3)
        {
            if (!balancedMapped)
            {
                toggles[0].isOn =
                    selection.BalancedDevelopment;
            }

            if (!doublesMapped)
            {
                toggles[1].isOn =
                    selection.DoublesEnabled;
            }

            if (!tripleMapped)
            {
                toggles[2].isOn =
                    selection.TripleDoublePenaltyEnabled;
            }
        }
    }

    private static bool SetMapOption(
        TMP_Dropdown dropdown,
        string mapId)
    {
        if (dropdown == null ||
            dropdown.options == null ||
            dropdown.options.Count == 0 ||
            string.IsNullOrWhiteSpace(
                mapId))
        {
            return false;
        }

        string canonicalMapId =
            Canonicalize(
                mapId);

        string[] aliases;

        int fallbackIndex;

        switch (canonicalMapId)
        {
            case "turkey":
                aliases =
                    new[]
                    {
                        "Turkey",
                        "Türkiye",
                        "Turkiye"
                    };

                fallbackIndex = 0;
                break;

            case "colorado":
                aliases =
                    new[]
                    {
                        "Colorado"
                    };

                fallbackIndex = 1;
                break;

            case "usa":
            case "united states":
            case "united states of america":
            case "america":
            case "abd":
            case "amerika":
                aliases =
                    new[]
                    {
                        "USA",
                        "U.S.A.",
                        "United States",
                        "United States of America",
                        "America",
                        "ABD",
                        "Amerika",
                        "Estados Unidos",
                        "EE. UU.",
                        "États-Unis",
                        "Etats-Unis",
                        "Vereinigte Staaten",
                        "미국",
                        "США"
                    };

                fallbackIndex = 2;
                break;

            default:
                aliases =
                    new[]
                    {
                        mapId
                    };

                fallbackIndex = -1;
                break;
        }

        foreach (string alias
                 in aliases)
        {
            if (SetOption(
                    dropdown,
                    alias))
            {
                return true;
            }
        }

        // Final fallback for the three stable AtlasBoard map slots.
        // This is only used if the legacy BoardMapDefinition display
        // name is customized to something outside the alias list.
        if (fallbackIndex >= 0 &&
            fallbackIndex <
                dropdown.options.Count)
        {
            dropdown.value =
                fallbackIndex;

            dropdown.RefreshShownValue();

            dropdown.onValueChanged.Invoke(
                fallbackIndex);

            Debug.LogWarning(
                "Legacy map label did not match a known alias. " +
                $"Mapped stable MapId='{mapId}' by slot index " +
                $"{fallbackIndex}. Legacy option='" +
                $"{dropdown.options[fallbackIndex].text}'.");

            return true;
        }

        LogMapDiagnostics(
            dropdown,
            mapId);

        return false;
    }

    private static void LogMapDiagnostics(
        TMP_Dropdown dropdown,
        string mapId)
    {
        if (dropdown == null)
        {
            Debug.LogWarning(
                $"Map mapping failed for '{mapId}': " +
                "legacy map dropdown was not found.");

            return;
        }

        StringBuilder options =
            new StringBuilder();

        for (int i = 0;
             i < dropdown.options.Count;
             i++)
        {
            if (i > 0)
            {
                options.Append(
                    " | ");
            }

            options.Append(
                $"[{i}] " +
                dropdown.options[i].text);
        }

        Debug.LogWarning(
            $"Map mapping failed for stable MapId='{mapId}'. " +
            $"Legacy options: {options}");
    }

    private static bool SetOption(
        TMP_Dropdown dropdown,
        string desired)
    {
        if (dropdown == null ||
            dropdown.options == null ||
            string.IsNullOrWhiteSpace(desired))
        {
            return false;
        }

        for (int i = 0;
             i < dropdown.options.Count;
             i++)
        {
            if (!OptionMatches(
                    dropdown.options[i].text,
                    desired))
            {
                continue;
            }

            dropdown.value = i;
            dropdown.RefreshShownValue();
            dropdown.onValueChanged.Invoke(i);

            return true;
        }

        return false;
    }

    private static string GetCurrentOptionText(
        TMP_Dropdown dropdown)
    {
        if (dropdown == null ||
            dropdown.options == null ||
            dropdown.options.Count == 0 ||
            dropdown.value < 0 ||
            dropdown.value >= dropdown.options.Count)
        {
            return string.Empty;
        }

        return dropdown.options[
            dropdown.value].text;
    }

    private static bool OptionMatches(
        string option,
        string desired)
    {
        string a =
            Canonicalize(option);

        string b =
            Canonicalize(desired);

        if (a == b)
        {
            return true;
        }

        if (TryExtractFirstInteger(
                a,
                out int aNumber) &&
            TryExtractFirstInteger(
                b,
                out int bNumber))
        {
            return aNumber == bNumber;
        }

        return a.Contains(b) ||
               b.Contains(a);
    }

    private static string Canonicalize(
        string value)
    {
        string normalized =
            Normalize(value);

        normalized =
            normalized.Replace(
                "turkiye",
                "turkey");

        normalized =
            normalized.Replace(
                "insan",
                "human");

        normalized =
            normalized.Replace(
                "oyuncu",
                "player");

        return normalized.Trim();
    }

    private static bool HasOption(
        TMP_Dropdown dropdown,
        params string[] values)
    {
        if (dropdown == null ||
            dropdown.options == null)
        {
            return false;
        }

        foreach (TMP_Dropdown.OptionData option
                 in dropdown.options)
        {
            foreach (string value in values)
            {
                if (OptionMatches(
                        option.text,
                        value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasNumericOption(
        TMP_Dropdown dropdown,
        int value)
    {
        if (dropdown == null ||
            dropdown.options == null)
        {
            return false;
        }

        foreach (TMP_Dropdown.OptionData option
                 in dropdown.options)
        {
            if (TryExtractFirstInteger(
                    Normalize(
                        option.text),
                    out int number) &&
                number == value)
            {
                return true;
            }
        }

        return false;
    }

    private static bool OptionsContainAny(
        TMP_Dropdown dropdown,
        params string[] values)
    {
        if (dropdown == null ||
            dropdown.options == null)
        {
            return false;
        }

        foreach (TMP_Dropdown.OptionData option
                 in dropdown.options)
        {
            if (ContainsAny(
                    option.text,
                    values))
            {
                return true;
            }
        }

        return false;
    }

    private static Button FindStartButton(
        GameObject root)
    {
        Button[] buttons =
            root.GetComponentsInChildren<
                Button>(true);

        foreach (Button button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            TMP_Text text =
                button.GetComponentInChildren<
                    TMP_Text>(true);

            if (text != null &&
                ContainsAny(
                    text.text,
                    "start",
                    "baslat",
                    "oyunu baslat"))
            {
                return button;
            }

            string context =
                BuildLocalContext(
                    button.transform);

            if (ContainsAny(
                    context,
                    "start",
                    "baslat",
                    "oyunu baslat"))
            {
                return button;
            }
        }

        return null;
    }

    private static string BuildLocalContext(
        Transform source)
    {
        List<string> parts =
            new List<string>();

        Transform cursor = source;

        for (int depth = 0;
             cursor != null &&
             depth < 3;
             depth++)
        {
            parts.Add(cursor.name);

            TMP_Text directText =
                cursor.GetComponent<
                    TMP_Text>();

            if (directText != null)
            {
                parts.Add(
                    directText.text);
            }

            cursor =
                cursor.parent;
        }

        TMP_Text ownCaption =
            source.GetComponentInChildren<
                TMP_Text>(true);

        if (ownCaption != null)
        {
            parts.Add(
                ownCaption.text);
        }

        return string.Join(
            " ",
            parts);
    }

    private static string GetHierarchyPath(
        Transform source)
    {
        if (source == null)
        {
            return string.Empty;
        }

        List<int> indices =
            new List<int>();

        Transform cursor =
            source;

        while (cursor != null)
        {
            indices.Add(
                cursor.GetSiblingIndex());

            cursor =
                cursor.parent;
        }

        indices.Reverse();

        return string.Join(
            ".",
            indices);
    }

    private static bool ContainsAny(
        string source,
        params string[] needles)
    {
        string normalized =
            Normalize(source);

        foreach (string needle in needles)
        {
            if (normalized.Contains(
                    Normalize(needle)))
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(
        string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        // Important for Turkish uppercase dotted I:
        // "İNSAN".ToLowerInvariant() may become "i̇nsan"
        // (letter i + Unicode combining dot), which does NOT equal "insan".
        // Normalize to FormD and remove all combining marks first.
        string decomposed =
            source.Trim()
                .Normalize(
                    NormalizationForm.FormD);

        StringBuilder cleaned =
            new StringBuilder(
                decomposed.Length);

        foreach (char c in decomposed)
        {
            UnicodeCategory category =
                CharUnicodeInfo
                    .GetUnicodeCategory(c);

            if (category ==
                    UnicodeCategory.NonSpacingMark ||
                category ==
                    UnicodeCategory.SpacingCombiningMark ||
                category ==
                    UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            cleaned.Append(c);
        }

        string value =
            cleaned.ToString()
                .Normalize(
                    NormalizationForm.FormC)
                .ToLowerInvariant()
                .Replace("_", " ")
                .Replace("-", " ")
                .Replace("ı", "i")
                .Replace("ş", "s")
                .Replace("ğ", "g")
                .Replace("ç", "c")
                .Replace("ö", "o")
                .Replace("ü", "u");

        StringBuilder builder =
            new StringBuilder(
                value.Length);

        bool lastWasSpace = false;

        foreach (char c in value)
        {
            bool isSpace =
                char.IsWhiteSpace(c);

            if (isSpace)
            {
                if (!lastWasSpace)
                {
                    builder.Append(' ');
                }

                lastWasSpace = true;
            }
            else
            {
                builder.Append(c);
                lastWasSpace = false;
            }
        }

        return builder
            .ToString()
            .Trim();
    }

    private static bool TryExtractFirstInteger(
        string source,
        out int value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        StringBuilder digits =
            new StringBuilder();

        foreach (char c in source)
        {
            if (char.IsDigit(c))
            {
                digits.Append(c);
            }
            else if (digits.Length > 0)
            {
                break;
            }
        }

        return digits.Length > 0 &&
               int.TryParse(
                   digits.ToString(),
                   out value);
    }

    private static void LogPlayerTypeDiagnostics(
        List<PlayerTypeControl> playerTypeDropdowns,
        AtlasBoardLobbySelection selection)
    {
        Debug.Log(
            $"Player-type dropdowns found: {playerTypeDropdowns.Count}.");

        for (int i = 0;
             i < playerTypeDropdowns.Count;
             i++)
        {
            PlayerTypeControl control =
                playerTypeDropdowns[i];

            string desired =
                selection.PlayerTypes != null &&
                i < selection.PlayerTypes.Length
                    ? selection.PlayerTypes[i]
                    : "<none>";

            string actual =
                control.GetCurrentText();

            float y =
                GetVisualY(
                    control.Transform);

            string kind =
                control.Tmp != null
                    ? "TMP_Dropdown"
                    : "UI.Dropdown";

            Debug.Log(
                $"Visual P{i + 1}: Y={y:0.0}, " +
                $"Lobby='{desired}', Legacy='{actual}', " +
                $"Type={kind}, Control='{control.Name}'.");
        }
    }

    private static GameObject FindSceneObject(
        string objectName)
    {
        GameObject[] all =
            Resources.FindObjectsOfTypeAll<
                GameObject>();

        foreach (GameObject item in all)
        {
            if (item == null ||
                !item.scene.IsValid() ||
                item.name != objectName)
            {
                continue;
            }

            return item;
        }

        return null;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        GameObject newExistingMatchSetupCanvas)
    {
        existingMatchSetupCanvas =
            newExistingMatchSetupCanvas;
    }
#endif
}
