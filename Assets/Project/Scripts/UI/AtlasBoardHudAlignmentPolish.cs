using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class AtlasBoardHudAlignmentPolish :
    MonoBehaviour
{
    private const float IconSize = 42f;
    private const float IconCenterX = 30f;
    private const float ContentLeft = 62f;
    private const float NameRight = 80f;
    private const float BodyRight = 14f;
    private const float BadgeWidth = 60f;
    private const float BadgeHeight = 20f;

    private PlayerHudPanel[] cachedPanels =
        Array.Empty<PlayerHudPanel>();

    private float nextDiscoveryAt;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        if (UnityEngine.Object.FindAnyObjectByType<
                BoardPath>() == null)
        {
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardHudAlignmentPolish>() != null)
        {
            return;
        }

        GameObject root =
            new GameObject(
                "AtlasBoard_Phase14D_HUDAlignmentPolish");

        root.AddComponent<
            AtlasBoardHudAlignmentPolish>();
    }

    private void Awake()
    {
        DiscoverPanels();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >=
            nextDiscoveryAt)
        {
            nextDiscoveryAt =
                Time.unscaledTime +
                1f;

            DiscoverPanels();
        }

        for (int index = 0;
             index < cachedPanels.Length;
             index++)
        {
            PlayerHudPanel panel =
                cachedPanels[index];

            if (panel == null ||
                !panel.gameObject.activeInHierarchy)
            {
                continue;
            }

            ApplyToPanel(
                panel);
        }
    }

    private void DiscoverPanels()
    {
        PlayerHudPanel[] all =
            UnityEngine.Object.FindObjectsByType<
                PlayerHudPanel>(FindObjectsInactive.Include);

        if (all == null ||
            all.Length == 0)
        {
            cachedPanels =
                Array.Empty<PlayerHudPanel>();

            return;
        }

        Array.Sort(
            all,
            ComparePanels);

        cachedPanels =
            all;
    }

    private static int ComparePanels(
        PlayerHudPanel left,
        PlayerHudPanel right)
    {
        string leftName =
            left != null
                ? left.gameObject.name
                : string.Empty;

        string rightName =
            right != null
                ? right.gameObject.name
                : string.Empty;

        return string.Compare(
            leftName,
            rightName,
            StringComparison.Ordinal);
    }

    public static void ApplyToPanel(
        PlayerHudPanel panel)
    {
        if (panel == null)
        {
            return;
        }

        RectTransform panelRect =
            panel.PanelRect;

        if (panelRect == null)
        {
            return;
        }

        RectTransform accent =
            FindRect(
                panelRect,
                "Accent");

        RectTransform icon =
            FindRect(
                panelRect,
                "Icon");

        TMP_Text iconText =
            FindText(
                panelRect,
                "IconText");

        TMP_Text playerName =
            FindText(
                panelRect,
                "PlayerName");

        TMP_Text money =
            FindText(
                panelRect,
                "Money");

        TMP_Text controlType =
            FindText(
                panelRect,
                "ControlType");

        RectTransform turnBadge =
            FindRect(
                panelRect,
                "TurnBadge");

        TMP_Text turnBadgeText =
            FindText(
                turnBadge,
                "Text");

        if (accent != null)
        {
            accent.anchorMin =
                new Vector2(
                    0f,
                    0f);

            accent.anchorMax =
                new Vector2(
                    0f,
                    1f);

            accent.pivot =
                new Vector2(
                    0f,
                    0.5f);

            accent.offsetMin =
                Vector2.zero;

            accent.offsetMax =
                new Vector2(
                    6f,
                    0f);
        }

        if (icon != null)
        {
            icon.anchorMin =
                new Vector2(
                    0f,
                    0.5f);

            icon.anchorMax =
                new Vector2(
                    0f,
                    0.5f);

            icon.pivot =
                new Vector2(
                    0.5f,
                    0.5f);

            icon.anchoredPosition =
                new Vector2(
                    IconCenterX,
                    0f);

            icon.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                IconSize);

            icon.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                IconSize);

            Image iconImage =
                icon.GetComponent<
                    Image>();

            if (iconImage != null)
            {
                iconImage.raycastTarget =
                    false;
            }
        }

        if (iconText != null)
        {
            Stretch(
                iconText.rectTransform,
                2f);

            iconText.alignment =
                TextAlignmentOptions.Center;

            iconText.enableAutoSizing =
                true;

            iconText.fontSizeMin =
                11f;

            iconText.fontSizeMax =
                18f;

            iconText.fontStyle =
                FontStyles.Bold;

            iconText.textWrappingMode =
                TextWrappingModes.NoWrap;

            iconText.overflowMode =
                TextOverflowModes.Truncate;

            iconText.raycastTarget =
                false;
        }

        ConfigureTopLane(
            playerName,
            ContentLeft,
            NameRight,
            5f,
            19f,
            TextAlignmentOptions.TopLeft,
            11f,
            18f,
            FontStyles.Bold);

        ConfigureTopLane(
            money,
            ContentLeft,
            BodyRight,
            29f,
            21f,
            TextAlignmentOptions.TopLeft,
            12f,
            20f,
            FontStyles.Bold);

        ConfigureBottomLane(
            controlType,
            ContentLeft,
            BodyRight,
            6f,
            17f,
            TextAlignmentOptions.BottomLeft,
            8f,
            12f,
            FontStyles.Normal);

        if (turnBadge != null)
        {
            turnBadge.anchorMin =
                new Vector2(
                    1f,
                    1f);

            turnBadge.anchorMax =
                new Vector2(
                    1f,
                    1f);

            turnBadge.pivot =
                new Vector2(
                    1f,
                    1f);

            turnBadge.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                BadgeWidth);

            turnBadge.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                BadgeHeight);

            turnBadge.anchoredPosition =
                new Vector2(
                    -10f,
                    -8f);

            Image badgeImage =
                turnBadge.GetComponent<
                    Image>();

            if (badgeImage != null)
            {
                badgeImage.raycastTarget =
                    false;
            }
        }

        if (turnBadgeText != null)
        {
            Stretch(
                turnBadgeText.rectTransform,
                2f);

            turnBadgeText.alignment =
                TextAlignmentOptions.Center;

            turnBadgeText.enableAutoSizing =
                true;

            turnBadgeText.fontSizeMin =
                7f;

            turnBadgeText.fontSizeMax =
                11f;

            turnBadgeText.fontStyle =
                FontStyles.Bold;

            turnBadgeText.textWrappingMode =
                TextWrappingModes.NoWrap;

            turnBadgeText.overflowMode =
                TextOverflowModes.Truncate;

            turnBadgeText.raycastTarget =
                false;
        }
    }

    private static void ConfigureTopLane(
        TMP_Text target,
        float left,
        float right,
        float top,
        float height,
        TextAlignmentOptions alignment,
        float minSize,
        float maxSize,
        FontStyles style)
    {
        if (target == null)
        {
            return;
        }

        RectTransform rect =
            target.rectTransform;

        rect.anchorMin =
            new Vector2(
                0f,
                1f);

        rect.anchorMax =
            new Vector2(
                1f,
                1f);

        rect.pivot =
            new Vector2(
                0f,
                1f);

        rect.offsetMin =
            new Vector2(
                left,
                -(top + height));

        rect.offsetMax =
            new Vector2(
                -right,
                -top);

        ConfigureText(
            target,
            alignment,
            minSize,
            maxSize,
            style);
    }

    private static void ConfigureBottomLane(
        TMP_Text target,
        float left,
        float right,
        float bottom,
        float height,
        TextAlignmentOptions alignment,
        float minSize,
        float maxSize,
        FontStyles style)
    {
        if (target == null)
        {
            return;
        }

        RectTransform rect =
            target.rectTransform;

        rect.anchorMin =
            new Vector2(
                0f,
                0f);

        rect.anchorMax =
            new Vector2(
                1f,
                0f);

        rect.pivot =
            new Vector2(
                0f,
                0f);

        rect.offsetMin =
            new Vector2(
                left,
                bottom);

        rect.offsetMax =
            new Vector2(
                -right,
                bottom + height);

        ConfigureText(
            target,
            alignment,
            minSize,
            maxSize,
            style);
    }

    private static void ConfigureText(
        TMP_Text target,
        TextAlignmentOptions alignment,
        float minSize,
        float maxSize,
        FontStyles style)
    {
        target.alignment =
            alignment;

        target.enableAutoSizing =
            true;

        target.fontSizeMin =
            minSize;

        target.fontSizeMax =
            maxSize;

        target.fontStyle =
            style;

        target.textWrappingMode =
            TextWrappingModes.NoWrap;

        target.overflowMode =
            TextOverflowModes.Truncate;

        target.raycastTarget =
            false;
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

    private static void Stretch(
        RectTransform rect,
        float inset)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            new Vector2(
                inset,
                inset);

        rect.offsetMax =
            new Vector2(
                -inset,
                -inset);
    }
}
