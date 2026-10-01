using System;
using TMPro;
using UnityEngine;

[DefaultExecutionOrder(11000)]
[DisallowMultipleComponent]
public sealed class AtlasBoardHudTypographyPolish :
    MonoBehaviour
{
    private const float DiscoveryIntervalSeconds = 1f;

    private PlayerHudPanel[] cachedPanels =
        Array.Empty<PlayerHudPanel>();

    private UXOverlayController overlayController;

    private TMP_Text statusText;
    private TMP_Text shortcutHintText;

    private float nextDiscoveryAt;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private string lastLanguageCode = string.Empty;

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
                AtlasBoardHudTypographyPolish>() != null)
        {
            return;
        }

        GameObject root =
            new GameObject(
                "AtlasBoard_Phase14E_HUDTypographyPolish");

        root.AddComponent<
            AtlasBoardHudTypographyPolish>();
    }

    private void Awake()
    {
        DiscoverTargets();
        ApplyAll();
    }

    private void OnEnable()
    {
        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;
    }

    private void OnDisable()
    {
        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;
    }

    private void LateUpdate()
    {
        bool resolutionChanged =
            lastScreenWidth != Screen.width ||
            lastScreenHeight != Screen.height;

        string languageCode =
            GetLanguageCode();

        bool languageChanged =
            !string.Equals(
                lastLanguageCode,
                languageCode,
                StringComparison.OrdinalIgnoreCase);

        if (Time.unscaledTime >=
            nextDiscoveryAt)
        {
            nextDiscoveryAt =
                Time.unscaledTime +
                DiscoveryIntervalSeconds;

            DiscoverTargets();
        }

        if (resolutionChanged ||
            languageChanged)
        {
            lastScreenWidth =
                Screen.width;

            lastScreenHeight =
                Screen.height;

            lastLanguageCode =
                languageCode;
        }

        // Phase 14D reapplies structural HUD geometry in LateUpdate.
        // Phase 14E deliberately runs after it (execution order 11000)
        // and applies typography/responsive presentation only.
        ApplyAll();
    }

    private void HandleLanguageChanged()
    {
        lastLanguageCode =
            string.Empty;

        DiscoverTargets();
        ApplyAll();
    }

    private void DiscoverTargets()
    {
        cachedPanels =
            UnityEngine.Object.FindObjectsByType<
                PlayerHudPanel>(
                FindObjectsInactive.Include);

        if (overlayController == null)
        {
            overlayController =
                UnityEngine.Object.FindAnyObjectByType<
                    UXOverlayController>();
        }

        if (overlayController != null)
        {
            if (statusText == null)
            {
                statusText =
                    FindText(
                        overlayController.transform,
                        "StatusText");
            }

            if (shortcutHintText == null)
            {
                shortcutHintText =
                    FindText(
                        overlayController.transform,
                        "ShortcutHint");
            }
        }
    }

    private void ApplyAll()
    {
        string languageCode =
            GetLanguageCode();

        bool compactViewport =
            IsCompactViewport();

        for (int index = 0;
             index < cachedPanels.Length;
             index++)
        {
            PlayerHudPanel panel =
                cachedPanels[index];

            if (panel == null)
            {
                continue;
            }

            ApplyToPanel(
                panel,
                languageCode,
                compactViewport);
        }

        ApplyStatusBar(
            statusText,
            shortcutHintText,
            languageCode,
            compactViewport);
    }

    public static void ApplyToPanel(
        PlayerHudPanel panel,
        string languageCode,
        bool compactViewport)
    {
        if (panel == null ||
            panel.PanelRect == null)
        {
            return;
        }

        RectTransform root =
            panel.PanelRect;

        TMP_Text iconText =
            FindText(
                root,
                "IconText");

        TMP_Text playerName =
            FindText(
                root,
                "PlayerName");

        TMP_Text money =
            FindText(
                root,
                "Money");

        TMP_Text controlType =
            FindText(
                root,
                "ControlType");

        TMP_Text turnBadgeText =
            FindText(
                root,
                "Text",
                parentName: "TurnBadge");

        ApplyIconTypography(
            iconText,
            languageCode);

        ApplyPlayerNameTypography(
            playerName,
            languageCode,
            compactViewport);

        ApplyMoneyTypography(
            money,
            languageCode,
            compactViewport);

        ApplyControlTypography(
            controlType,
            languageCode,
            compactViewport);

        ApplyTurnBadgeTypography(
            turnBadgeText,
            languageCode,
            compactViewport);
    }

    private static void ApplyIconTypography(
        TMP_Text text,
        string languageCode)
    {
        if (text == null)
        {
            return;
        }

        ResolveLocalizedFont(
            text);

        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 17f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.characterSpacing = 0f;
        text.wordSpacing = 0f;
        text.lineSpacing = 0f;
        text.margin = new Vector4(
            2f,
            1f,
            2f,
            1f);
        text.raycastTarget = false;
    }

    private static void ApplyPlayerNameTypography(
        TMP_Text text,
        string languageCode,
        bool compactViewport)
    {
        if (text == null)
        {
            return;
        }

        ResolveLocalizedFont(
            text);

        int visibleLength =
            VisibleTextLength(
                text.text);

        float maxSize =
            compactViewport
                ? 16f
                : 18f;

        if (visibleLength >= 18)
        {
            maxSize -= 1f;
        }

        if (visibleLength >= 24)
        {
            maxSize -= 1.5f;
        }

        if (UsesWideGlyphScript(
                languageCode))
        {
            maxSize -= 0.5f;
        }

        text.enableAutoSizing = true;
        text.fontSizeMin =
            compactViewport
                ? 9.5f
                : 10f;
        text.fontSizeMax =
            Mathf.Max(
                text.fontSizeMin,
                maxSize);
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.characterSpacing =
            visibleLength >= 20
                ? -1.5f
                : -0.25f;
        text.wordSpacing = 0f;
        text.lineSpacing = 0f;
        text.margin = new Vector4(
            0f,
            0f,
            1f,
            0f);
        text.raycastTarget = false;
    }

    private static void ApplyMoneyTypography(
        TMP_Text text,
        string languageCode,
        bool compactViewport)
    {
        if (text == null)
        {
            return;
        }

        ResolveLocalizedFont(
            text);

        text.enableAutoSizing = true;
        text.fontSizeMin =
            compactViewport
                ? 11f
                : 12f;
        text.fontSizeMax =
            compactViewport
                ? 18f
                : 19f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.characterSpacing = 0.25f;
        text.wordSpacing = 0f;
        text.lineSpacing = 0f;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
    }

    private static void ApplyControlTypography(
        TMP_Text text,
        string languageCode,
        bool compactViewport)
    {
        if (text == null)
        {
            return;
        }

        ResolveLocalizedFont(
            text);

        int visibleLength =
            VisibleTextLength(
                text.text);

        float maxSize =
            compactViewport
                ? 10.5f
                : 11.5f;

        if (visibleLength >= 16)
        {
            maxSize -= 0.75f;
        }

        if (visibleLength >= 22)
        {
            maxSize -= 0.75f;
        }

        if (UsesWideGlyphScript(
                languageCode))
        {
            maxSize -= 0.25f;
        }

        text.enableAutoSizing = true;
        text.fontSizeMin = 7.5f;
        text.fontSizeMax =
            Mathf.Max(
                7.5f,
                maxSize);
        text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.characterSpacing =
            visibleLength >= 16
                ? -1f
                : 0f;
        text.wordSpacing = 0f;
        text.lineSpacing = 0f;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
    }

    private static void ApplyTurnBadgeTypography(
        TMP_Text text,
        string languageCode,
        bool compactViewport)
    {
        if (text == null)
        {
            return;
        }

        ResolveLocalizedFont(
            text);

        text.enableAutoSizing = true;
        text.fontSizeMin = 7f;
        text.fontSizeMax =
            compactViewport
                ? 9.5f
                : 10.5f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.characterSpacing =
            UsesWideGlyphScript(
                languageCode)
                ? -0.5f
                : 0f;
        text.wordSpacing = 0f;
        text.lineSpacing = 0f;
        text.margin = new Vector4(
            2f,
            1f,
            2f,
            1f);
        text.raycastTarget = false;
    }

    public static void ApplyStatusBar(
        TMP_Text status,
        TMP_Text hint,
        string languageCode,
        bool compactViewport)
    {
        if (status != null)
        {
            ResolveLocalizedFont(
                status);

            int visibleLength =
                VisibleTextLength(
                    status.text);

            float maxSize =
                compactViewport
                    ? 18f
                    : 21f;

            if (visibleLength >= 70)
            {
                maxSize -= 1.5f;
            }

            if (visibleLength >= 95)
            {
                maxSize -= 1.5f;
            }

            if (UsesWideGlyphScript(
                    languageCode))
            {
                maxSize -= 0.5f;
            }

            status.enableAutoSizing = true;
            status.fontSizeMin =
                compactViewport
                    ? 10f
                    : 11f;
            status.fontSizeMax =
                Mathf.Max(
                    status.fontSizeMin,
                    maxSize);
            status.fontStyle = FontStyles.Bold;
            status.alignment =
                TextAlignmentOptions.Center;
            status.textWrappingMode =
                TextWrappingModes.NoWrap;
            status.overflowMode =
                TextOverflowModes.Truncate;
            status.characterSpacing =
                visibleLength >= 70
                    ? -0.75f
                    : 0f;
            status.wordSpacing = 0f;
            status.lineSpacing = 0f;
            status.margin = new Vector4(
                8f,
                0f,
                8f,
                0f);
            status.raycastTarget = false;
        }

        if (hint != null)
        {
            ResolveLocalizedFont(
                hint);

            int visibleLength =
                VisibleTextLength(
                    hint.text);

            hint.enableAutoSizing = true;
            hint.fontSizeMin = 8f;
            hint.fontSizeMax =
                compactViewport
                    ? 12f
                    : 14f;
            hint.fontStyle = FontStyles.Normal;
            hint.alignment =
                TextAlignmentOptions.Center;
            hint.textWrappingMode =
                TextWrappingModes.NoWrap;
            hint.overflowMode =
                TextOverflowModes.Truncate;
            hint.characterSpacing =
                visibleLength >= 70
                    ? -0.75f
                    : 0f;
            hint.wordSpacing = 0f;
            hint.lineSpacing = 0f;
            hint.margin = new Vector4(
                8f,
                0f,
                8f,
                0f);
            hint.raycastTarget = false;
        }
    }

    private static void ResolveLocalizedFont(
        TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        AtlasBoardLocalizationManager manager =
            AtlasBoardLocalizationManager.Instance;

        if (manager == null)
        {
            return;
        }

        TMP_FontAsset current =
            text.font != null
                ? text.font
                : TMP_Settings.defaultFontAsset;

        TMP_FontAsset resolved =
            manager.ResolveFont(
                current);

        if (resolved != null &&
            resolved != text.font)
        {
            text.font =
                resolved;
        }
    }

    private static string GetLanguageCode()
    {
        AtlasBoardLocalizationManager manager =
            AtlasBoardLocalizationManager.Instance;

        return manager != null
            ? manager.CurrentLanguageCode ??
              "en"
            : "en";
    }

    private static bool UsesWideGlyphScript(
        string languageCode)
    {
        string normalized =
            (languageCode ?? "en")
                .ToLowerInvariant();

        return normalized == "ko" ||
               normalized == "ru" ||
               normalized == "de";
    }

    private static bool IsCompactViewport()
    {
        if (Screen.width <= 0 ||
            Screen.height <= 0)
        {
            return false;
        }

        Rect safeArea =
            Screen.safeArea;

        float width =
            safeArea.width > 0f
                ? safeArea.width
                : Screen.width;

        float height =
            safeArea.height > 0f
                ? safeArea.height
                : Screen.height;

        if (height <= 0f)
        {
            return false;
        }

        float aspect =
            width /
            height;

        return aspect < 1.55f ||
               width < 1100f;
    }

    private static int VisibleTextLength(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return 0;
        }

        int count = 0;
        bool insideTag = false;

        for (int index = 0;
             index < value.Length;
             index++)
        {
            char character =
                value[index];

            if (character == '<')
            {
                insideTag = true;
                continue;
            }

            if (insideTag)
            {
                if (character == '>')
                {
                    insideTag = false;
                }

                continue;
            }

            count++;
        }

        return count;
    }

    private static TMP_Text FindText(
        Transform root,
        string objectName,
        string parentName = null)
    {
        Transform child =
            FindChildRecursive(
                root,
                objectName,
                parentName);

        return child != null
            ? child.GetComponent<
                TMP_Text>()
            : null;
    }

    private static Transform FindChildRecursive(
        Transform root,
        string objectName,
        string parentName)
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

            bool nameMatches =
                string.Equals(
                    child.name,
                    objectName,
                    StringComparison.Ordinal);

            bool parentMatches =
                string.IsNullOrWhiteSpace(
                    parentName) ||
                (child.parent != null &&
                 string.Equals(
                     child.parent.name,
                     parentName,
                     StringComparison.Ordinal));

            if (nameMatches &&
                parentMatches)
            {
                return child;
            }

            Transform nested =
                FindChildRecursive(
                    child,
                    objectName,
                    parentName);

            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
