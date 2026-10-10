#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class AtlasBoardPhase14KAccessibilityValidator
{
    [MenuItem(
        "Atlas Board/Phases/Phase 14/14K - Accessibility/Validate FINAL Phase 14K")]
    public static void Validate()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Phase 14K validation requires Play Mode.");
            return;
        }

        int errors = 0;
        int warnings = 0;

        AtlasBoardPhase14KAccessibilityController controller =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardPhase14KAccessibilityController>();

        if (controller == null)
        {
            Debug.LogError(
                "Phase 14K: accessibility controller is missing.");
            errors++;
        }

        AtlasBoardPurchasePresentationPolish purchase =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardPurchasePresentationPolish>();

        if (purchase == null)
        {
            Debug.LogError(
                "Phase 14K: purchase presentation polish is missing.");
            errors++;
        }

        AtlasBoardSpecialLocalizationRefresh localizationRefresh =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardSpecialLocalizationRefresh>();

        if (localizationRefresh == null)
        {
            Debug.LogError(
                "Phase 14K: special/event localization refresh bridge is missing.");
            errors++;
        }

        if (EventSystem.current == null)
        {
            Debug.LogError(
                "Phase 14K: no active EventSystem exists.");
            errors++;
        }

#if !ENABLE_INPUT_SYSTEM
        Debug.LogError(
            "Phase 14K: New Input System is not enabled. Controller navigation cannot run.");
        errors++;
#endif

        RectTransform[] rects =
            UnityEngine.Object.FindObjectsByType<
                RectTransform>(
                    FindObjectsInactive.Include);

        int legacyWorldBadges = 0;

        foreach (RectTransform rect
                 in rects)
        {
            if (rect != null &&
                string.Equals(
                    rect.name,
                    "__OwnerAccessibilityBadge",
                    StringComparison.Ordinal))
            {
                legacyWorldBadges++;
            }
        }

        if (legacyWorldBadges > 0)
        {
            Debug.LogError(
                $"Phase 14K: {legacyWorldBadges} legacy P# world ownership badge(s) still exist.");
            errors++;
        }

        ValidateLocalizationCoverage(
            ref errors,
            ref warnings);

        if (purchase != null &&
            purchase.PolishRoot != null &&
            purchase.PolishRoot.gameObject.activeInHierarchy)
        {
            Rect tablet =
                GetScreenRect(
                    purchase.TabletRoot);

            Rect polish =
                GetScreenRect(
                    purchase.PolishRoot);

            if (tablet.width <=
                    1f ||
                tablet.height <=
                    1f ||
                polish.width <
                    tablet.width *
                    0.90f ||
                polish.height <
                    tablet.height *
                    0.90f)
            {
                Debug.LogError(
                    $"Phase 14K: purchase polish geometry is invalid. " +
                    $"Purchase={polish.width:0.#}x{polish.height:0.#}, " +
                    $"Tablet={tablet.width:0.#}x{tablet.height:0.#}.");
                errors++;
            }
        }
        else
        {
            Debug.LogWarning(
                "Phase 14K: purchase screen is not currently open. " +
                "Land on an unowned City once to visually verify the new purchase card.");
            warnings++;
        }

        bool reduceMotion =
            AtlasBoardUserSettingsRuntime
                .ReduceCameraMotion;

        Debug.Log(
            $"Phase 14K reduced-motion setting currently: {reduceMotion}.");

        if (errors > 0)
        {
            Debug.LogError(
                $"Phase 14K FINAL validation FAILED with {errors} error(s), {warnings} warning(s).");
            return;
        }

        Debug.Log(
            $"Phase 14K FINAL validation PASS. " +
            $"Legacy world ownership badges=0, Localization warnings={warnings}.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14K - Accessibility/Audit Event & Special Localization")]
    public static void AuditLocalization()
    {
        int errors = 0;
        int warnings = 0;

        ValidateLocalizationCoverage(
            ref errors,
            ref warnings);

        if (errors > 0)
        {
            Debug.LogError(
                $"Phase 14K localization audit FAILED with {errors} missing translation issue(s).");
            return;
        }

        Debug.Log(
            $"Phase 14K localization audit PASS. EN/TR/ES/FR/DE/KO/RU are populated for Event/Special/Purchase keys. Warnings={warnings}.");
    }

    private static void ValidateLocalizationCoverage(
        ref int errors,
        ref int warnings)
    {
        AtlasBoardLocalizationManager manager =
            AtlasBoardLocalizationManager.Instance;

        if (manager == null ||
            manager.Database == null)
        {
            Debug.LogError(
                "Phase 14K localization audit: runtime localization database is missing.");

            errors++;
            return;
        }

        AtlasBoardLocalizationDatabase database =
            manager.Database;

        int checkedEntries = 0;

        foreach (AtlasBoardLocalizationDatabase.Entry entry
                 in database.Entries)
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(
                    entry.key))
            {
                continue;
            }

            bool relevant =
                entry.key.StartsWith(
                    "event.",
                    StringComparison.OrdinalIgnoreCase) ||
                entry.key.StartsWith(
                    "special.",
                    StringComparison.OrdinalIgnoreCase) ||
                entry.key.StartsWith(
                    "tile.special.",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    entry.key,
                    "purchase.prompt",
                    StringComparison.OrdinalIgnoreCase);

            if (!relevant)
            {
                continue;
            }

            checkedEntries++;

            foreach (string language
                     in AtlasBoardLocalizationLanguages.Codes)
            {
                if (database.HasTranslation(
                        entry.key,
                        language))
                {
                    continue;
                }

                Debug.LogError(
                    $"Phase 14K localization missing: key='{entry.key}', language='{language}'.");

                errors++;
            }
        }

        if (checkedEntries == 0)
        {
            Debug.LogError(
                "Phase 14K localization audit found zero Event/Special/Purchase entries.");

            errors++;
            return;
        }

        Debug.Log(
            $"Phase 14K localization coverage checked {checkedEntries} Event/Special/Purchase entries across 7 languages.");
    }

    private static Rect GetScreenRect(
        RectTransform rect)
    {
        if (rect == null)
        {
            return default;
        }

        Vector3[] corners =
            new Vector3[4];

        rect.GetWorldCorners(
            corners);

        Canvas canvas =
            rect.GetComponentInParent<
                Canvas>();

        Canvas root =
            canvas != null
                ? canvas.rootCanvas
                : null;

        Camera camera =
            root == null ||
            root.renderMode ==
                RenderMode.ScreenSpaceOverlay
                ? null
                : root.worldCamera;

        Vector2 bottomLeft =
            RectTransformUtility
                .WorldToScreenPoint(
                    camera,
                    corners[0]);

        Vector2 topRight =
            RectTransformUtility
                .WorldToScreenPoint(
                    camera,
                    corners[2]);

        return Rect.MinMaxRect(
            Mathf.Min(
                bottomLeft.x,
                topRight.x),
            Mathf.Min(
                bottomLeft.y,
                topRight.y),
            Mathf.Max(
                bottomLeft.x,
                topRight.x),
            Mathf.Max(
                bottomLeft.y,
                topRight.y));
    }
}
#endif
