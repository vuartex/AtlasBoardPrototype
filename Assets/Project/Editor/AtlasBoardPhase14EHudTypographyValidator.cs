#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase14EHudTypographyValidator
{
    [MenuItem(
        "Atlas Board/Phases/Phase 14/14E - HUD Typography/Validate Typography & Responsive Rules")]
    public static void Validate()
    {
        PlayerHudPanel[] panels =
            UnityEngine.Object.FindObjectsByType<
                PlayerHudPanel>(
                FindObjectsInactive.Include);

        if (panels == null ||
            panels.Length < 4)
        {
            Debug.LogError(
                "Phase 14E validation FAILED: expected at least four " +
                $"PlayerHudPanel objects, found {panels?.Length ?? 0}.");
            return;
        }

        string languageCode =
            AtlasBoardLocalizationManager.Instance != null
                ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
                : "en";

        int errors = 0;

        foreach (PlayerHudPanel panel
                 in panels)
        {
            if (panel == null ||
                panel.PanelRect == null)
            {
                errors++;
                continue;
            }

            AtlasBoardHudTypographyPolish.ApplyToPanel(
                panel,
                languageCode,
                compactViewport: false);

            TMP_Text playerName =
                FindText(
                    panel.PanelRect,
                    "PlayerName");

            TMP_Text money =
                FindText(
                    panel.PanelRect,
                    "Money");

            TMP_Text control =
                FindText(
                    panel.PanelRect,
                    "ControlType");

            TMP_Text turnBadge =
                FindTextUnder(
                    panel.PanelRect,
                    "TurnBadge",
                    "Text");

            errors +=
                ValidateText(
                    panel,
                    playerName,
                    "PlayerName",
                    expectedAutoSize: true);

            errors +=
                ValidateText(
                    panel,
                    money,
                    "Money",
                    expectedAutoSize: true);

            errors +=
                ValidateText(
                    panel,
                    control,
                    "ControlType",
                    expectedAutoSize: true);

            errors +=
                ValidateText(
                    panel,
                    turnBadge,
                    "TurnBadge/Text",
                    expectedAutoSize: true);

            if (playerName != null &&
                playerName.fontSizeMin > 10.1f)
            {
                Debug.LogError(
                    $"Phase 14E: {panel.name} PlayerName minimum font size " +
                    "does not leave enough long-name shrink headroom.",
                    panel);
                errors++;
            }

            if (playerName != null &&
                playerName.rectTransform != null)
            {
                float availableWidth =
                    playerName.rectTransform.rect.width;

                if (availableWidth < 150f)
                {
                    Debug.LogError(
                        $"Phase 14E: {panel.name} PlayerName lane is too narrow " +
                        $"({availableWidth:0.#}).",
                        panel);
                    errors++;
                }
            }
        }

        UXOverlayController overlay =
            UnityEngine.Object.FindAnyObjectByType<
                UXOverlayController>();

        if (overlay == null)
        {
            Debug.LogError(
                "Phase 14E validation FAILED: UXOverlayController not found.");
            errors++;
        }
        else
        {
            TMP_Text status =
                FindText(
                    overlay.transform,
                    "StatusText");

            TMP_Text hint =
                FindText(
                    overlay.transform,
                    "ShortcutHint");

            AtlasBoardHudTypographyPolish.ApplyStatusBar(
                status,
                hint,
                languageCode,
                compactViewport: false);

            errors +=
                ValidateText(
                    overlay,
                    status,
                    "StatusText",
                    expectedAutoSize: true);

            errors +=
                ValidateText(
                    overlay,
                    hint,
                    "ShortcutHint",
                    expectedAutoSize: true);
        }

        if (errors > 0)
        {
            Debug.LogError(
                $"Phase 14E validation FAILED with {errors} issue(s).");
            return;
        }

        Debug.Log(
            "Phase 14E typography/responsive validation PASS. " +
            "Player HUD and gameplay status typography use autosizing, " +
            "single-line truncation, localized font resolution and " +
            "long-text shrink headroom.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14E - HUD Typography/Apply Typography Preview to Open Scene")]
    public static void ApplyPreview()
    {
        PlayerHudPanel[] panels =
            UnityEngine.Object.FindObjectsByType<
                PlayerHudPanel>(
                FindObjectsInactive.Include);

        string languageCode =
            AtlasBoardLocalizationManager.Instance != null
                ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
                : "en";

        int applied = 0;

        foreach (PlayerHudPanel panel
                 in panels)
        {
            if (panel == null)
            {
                continue;
            }

            AtlasBoardHudTypographyPolish.ApplyToPanel(
                panel,
                languageCode,
                compactViewport: false);

            EditorUtility.SetDirty(
                panel);

            applied++;
        }

        UXOverlayController overlay =
            UnityEngine.Object.FindAnyObjectByType<
                UXOverlayController>();

        if (overlay != null)
        {
            TMP_Text status =
                FindText(
                    overlay.transform,
                    "StatusText");

            TMP_Text hint =
                FindText(
                    overlay.transform,
                    "ShortcutHint");

            AtlasBoardHudTypographyPolish.ApplyStatusBar(
                status,
                hint,
                languageCode,
                compactViewport: false);
        }

        Debug.Log(
            $"Phase 14E typography preview applied to {applied} HUD panel(s).");
    }

    private static int ValidateText(
        UnityEngine.Object context,
        TMP_Text text,
        string label,
        bool expectedAutoSize)
    {
        if (text == null)
        {
            Debug.LogError(
                $"Phase 14E: missing {label}.",
                context);
            return 1;
        }

        int errors = 0;

        if (expectedAutoSize &&
            !text.enableAutoSizing)
        {
            Debug.LogError(
                $"Phase 14E: {label} autosizing is disabled.",
                context);
            errors++;
        }

        if (text.textWrappingMode !=
            TextWrappingModes.NoWrap)
        {
            Debug.LogError(
                $"Phase 14E: {label} must remain single-line.",
                context);
            errors++;
        }

        if (text.overflowMode !=
            TextOverflowModes.Truncate)
        {
            Debug.LogError(
                $"Phase 14E: {label} must use Truncate overflow.",
                context);
            errors++;
        }

        if (text.fontSizeMin <= 0f ||
            text.fontSizeMax <
                text.fontSizeMin)
        {
            Debug.LogError(
                $"Phase 14E: {label} has an invalid autosize range.",
                context);
            errors++;
        }

        return errors;
    }

    private static TMP_Text FindTextUnder(
        Transform root,
        string parentName,
        string childName)
    {
        Transform parent =
            FindChildRecursive(
                root,
                parentName);

        if (parent == null)
        {
            return null;
        }

        Transform child =
            FindChildRecursive(
                parent,
                childName);

        return child != null
            ? child.GetComponent<
                TMP_Text>()
            : null;
    }

    private static TMP_Text FindText(
        Transform root,
        string objectName)
    {
        Transform child =
            FindChildRecursive(
                root,
                objectName);

        return child != null
            ? child.GetComponent<
                TMP_Text>()
            : null;
    }

    private static Transform FindChildRecursive(
        Transform root,
        string objectName)
    {
        if (root == null)
        {
            return null;
        }

        for (int index = 0;
             index < root.childCount;
             index++)
        {
            Transform child =
                root.GetChild(
                    index);

            if (string.Equals(
                    child.name,
                    objectName,
                    StringComparison.Ordinal))
            {
                return child;
            }

            Transform nested =
                FindChildRecursive(
                    child,
                    objectName);

            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
#endif
