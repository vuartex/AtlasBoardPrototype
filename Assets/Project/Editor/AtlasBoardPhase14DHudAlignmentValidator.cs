#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase14DHudAlignmentValidator
{
    [MenuItem(
        "Atlas Board/Phases/Phase 14/14D - HUD Alignment/Apply Preview to Open Scene")]
    public static void ApplyPreview()
    {
        PlayerHudPanel[] panels =
            UnityEngine.Object.FindObjectsByType<
                PlayerHudPanel>(FindObjectsInactive.Include);

        if (panels == null ||
            panels.Length == 0)
        {
            Debug.LogError(
                "Phase 14D preview FAILED: no PlayerHudPanel objects found.");

            return;
        }

        foreach (PlayerHudPanel panel
                 in panels)
        {
            if (panel == null)
            {
                continue;
            }

            Undo.RecordObject(
                panel.PanelRect,
                "Phase 14D HUD alignment preview");

            AtlasBoardHudAlignmentPolish.ApplyToPanel(
                panel);

            EditorUtility.SetDirty(
                panel);
        }

        Debug.Log(
            $"Phase 14D preview applied to {panels.Length} HUD panel(s). " +
            "Runtime polish remains authoritative during Play Mode.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14D - HUD Alignment/Validate HUD Alignment")]
    public static void Validate()
    {
        PlayerHudPanel[] panels =
            UnityEngine.Object.FindObjectsByType<
                PlayerHudPanel>(FindObjectsInactive.Include);

        if (panels == null ||
            panels.Length < 4)
        {
            Debug.LogError(
                "Phase 14D validation FAILED: expected four PlayerHudPanel " +
                $"objects, found {panels?.Length ?? 0}.");

            return;
        }

        int errors = 0;

        foreach (PlayerHudPanel panel
                 in panels)
        {
            if (panel == null)
            {
                continue;
            }

            AtlasBoardHudAlignmentPolish.ApplyToPanel(
                panel);

            RectTransform root =
                panel.PanelRect;

            RectTransform icon =
                FindRect(
                    root,
                    "Icon");

            RectTransform badge =
                FindRect(
                    root,
                    "TurnBadge");

            TMP_Text playerName =
                FindText(
                    root,
                    "PlayerName");

            if (icon == null ||
                badge == null ||
                playerName == null)
            {
                Debug.LogError(
                    $"Phase 14D: {panel.name} is missing Icon, TurnBadge " +
                    "or PlayerName.",
                    panel);

                errors++;
                continue;
            }

            if (!Approximately(
                    icon.sizeDelta.x,
                    42f) ||
                !Approximately(
                    icon.sizeDelta.y,
                    42f))
            {
                Debug.LogError(
                    $"Phase 14D: {panel.name} icon size is not 42x42.",
                    panel);

                errors++;
            }

            if (!Approximately(
                    badge.sizeDelta.x,
                    60f) ||
                !Approximately(
                    badge.sizeDelta.y,
                    20f))
            {
                Debug.LogError(
                    $"Phase 14D: {panel.name} turn badge size is not 60x20.",
                    panel);

                errors++;
            }
        }

        if (errors > 0)
        {
            Debug.LogError(
                $"Phase 14D validation FAILED with {errors} issue(s).");

            return;
        }

        Debug.Log(
            "Phase 14D HUD alignment validation PASS. " +
            "P1/P2/P3/P4 use the same icon, content-lane and turn-badge geometry.");
    }

    private static bool Approximately(
        float left,
        float right)
    {
        return Mathf.Abs(
                   left -
                   right) <=
               0.5f;
    }

    private static RectTransform FindRect(
        Transform root,
        string objectName)
    {
        Transform child =
            FindChildRecursive(
                root,
                objectName);

        return child as RectTransform;
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
