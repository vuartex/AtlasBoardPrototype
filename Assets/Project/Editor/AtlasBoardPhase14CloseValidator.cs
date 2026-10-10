#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AtlasBoardPhase14CloseValidator
{
    [MenuItem(
        "Atlas Board/Phases/Phase 14/FINAL - Close Phase 14/Validate Final Localization & Input")]
    public static void Validate()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Phase 14 final validation requires Play Mode.");
            return;
        }

        int errors = 0;
        int warnings = 0;

        AtlasBoardPhase14FinalLocalizationGuard localizationGuard =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardPhase14FinalLocalizationGuard>();

        AtlasBoardCityInfoCameraInputGuard inputGuard =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardCityInfoCameraInputGuard>();

        if (localizationGuard == null)
        {
            Debug.LogError(
                "Phase 14 FINAL: localization guard is missing.");
            errors++;
        }

        if (inputGuard == null)
        {
            Debug.LogError(
                "Phase 14 FINAL: City Information camera-input guard is missing.");
            errors++;
        }

        string language =
            AtlasBoardLocalizationManager.Instance != null
                ? AtlasBoardLocalizationManager.Instance
                    .CurrentLanguageCode
                : "en";

        if (!string.Equals(
                language,
                "tr",
                StringComparison.OrdinalIgnoreCase))
        {
            errors +=
                CountVisibleLegacyTurkishLeaks();
        }

        BoardTile[] tiles =
            UnityEngine.Object.FindObjectsByType<
                BoardTile>(
                    FindObjectsInactive.Include);

        foreach (BoardTile tile
                 in tiles)
        {
            if (tile == null ||
                string.IsNullOrWhiteSpace(
                    tile.GroupDisplayName))
            {
                continue;
            }

            if (!string.Equals(
                    language,
                    "tr",
                    StringComparison.OrdinalIgnoreCase) &&
                tile.GroupDisplayName.StartsWith(
                    "Bölge ",
                    StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError(
                    $"Phase 14 FINAL: raw Turkish group label remains on tile '{tile.DisplayName}': '{tile.GroupDisplayName}'.",
                    tile);
                errors++;
            }
        }

        AtlasBoardCityInformationPanel city =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardCityInformationPanel>();

        if (city == null)
        {
            Debug.LogWarning(
                "Phase 14 FINAL: City Information panel is not present in this scene.");
            warnings++;
        }
        else
        {
            Debug.Log(
                "Phase 14 FINAL City Information input rule: " +
                "while the pointer is over the open card, BoardCameraController is suspended; " +
                "ScrollRect owns wheel/drag input.");
        }

        if (errors > 0)
        {
            Debug.LogError(
                $"Phase 14 FINAL validation FAILED with {errors} error(s), {warnings} warning(s).");
            return;
        }

        Debug.Log(
            $"Phase 14 FINAL validation PASS. Language={language.ToUpperInvariant()}, " +
            $"legacy Turkish presentation leaks=0, warnings={warnings}.");
    }

    private static int CountVisibleLegacyTurkishLeaks()
    {
        int errors = 0;

        TMP_Text[] tmpTexts =
            UnityEngine.Object.FindObjectsByType<
                TMP_Text>(
                    FindObjectsInactive.Exclude);

        foreach (TMP_Text text
                 in tmpTexts)
        {
            if (text == null ||
                string.IsNullOrWhiteSpace(
                    text.text) ||
                !LooksLikeLegacyTurkishUi(
                    text.text))
            {
                continue;
            }

            Debug.LogError(
                $"Phase 14 FINAL: visible legacy Turkish TMP text while non-TR language is active: '{text.text}' at '{HierarchyPath(text.transform)}'.",
                text);

            errors++;
        }

        Text[] legacyTexts =
            UnityEngine.Object.FindObjectsByType<
                Text>(
                    FindObjectsInactive.Exclude);

        foreach (Text text
                 in legacyTexts)
        {
            if (text == null ||
                string.IsNullOrWhiteSpace(
                    text.text) ||
                !LooksLikeLegacyTurkishUi(
                    text.text))
            {
                continue;
            }

            Debug.LogError(
                $"Phase 14 FINAL: visible legacy Turkish UGUI text while non-TR language is active: '{text.text}' at '{HierarchyPath(text.transform)}'.",
                text);

            errors++;
        }

        return errors;
    }

    private static bool LooksLikeLegacyTurkishUi(
        string value)
    {
        string trimmed =
            value.Trim();

        if (string.Equals(
                trimmed,
                "HARİTA YOK",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                trimmed,
                "Eksik Harita",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                trimmed,
                "İNSAN",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                trimmed,
                "OYUNCU",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                trimmed,
                "TUR",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                trimmed,
                "HARİTA",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                trimmed,
                "Bölge",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                trimmed,
                "Konum",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return trimmed.StartsWith(
                   "Bölge ",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith(
                   " OYUNCU",
                   StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith(
                   " TUR",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string HierarchyPath(
        Transform source)
    {
        if (source == null)
        {
            return "<null>";
        }

        string path =
            source.name;

        Transform cursor =
            source.parent;

        while (cursor != null)
        {
            path =
                cursor.name +
                "/" +
                path;

            cursor =
                cursor.parent;
        }

        return path;
    }
}
#endif
