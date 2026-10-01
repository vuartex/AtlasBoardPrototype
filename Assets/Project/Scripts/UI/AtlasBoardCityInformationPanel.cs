using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AtlasBoardCityInformationPanel : MonoBehaviour
{
    private const int CanvasSortingOrder = 535;
    private const float RefreshInterval = 0.20f;

    private Canvas canvas;
    private RectTransform safeAreaRoot;
    private GameObject panelRoot;
    private RectTransform panelRect;
    private Image groupStripe;
    private TMP_Text titleText;
    private TMP_Text subtitleText;
    private TMP_Text bodyText;
    private TMP_Text footerText;
    private TMP_Text closeButtonText;
    private Button closeButton;

    private BoardTile selectedTile;
    private BoardGenerator boardGenerator;
    private PropertyDevelopmentManager developmentManager;
    private Camera boardCamera;

    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private float nextRefreshAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        if (UnityEngine.Object.FindAnyObjectByType<BoardPath>() == null)
        {
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType<AtlasBoardCityInformationPanel>() != null)
        {
            return;
        }

        GameObject root = new GameObject("AtlasBoard_Phase14A_CityInformationPanel");
        root.AddComponent<AtlasBoardCityInformationPanel>();
    }

    private void Awake()
    {
        ResolveGameplayReferences();
        BuildUi();
        ApplySafeArea(true);
        Hide();
    }

    private void OnEnable()
    {
        AtlasBoardLocalizationManager.LanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        AtlasBoardLocalizationManager.LanguageChanged -= HandleLanguageChanged;
    }

    private void Update()
    {
        ApplySafeArea(false);

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame &&
            IsOpen)
        {
            Hide();
            return;
        }

        if (TryGetPointerPress(out Vector2 screenPosition))
        {
            HandleWorldPointerPress(screenPosition);
        }

        if (IsOpen && Time.unscaledTime >= nextRefreshAt)
        {
            nextRefreshAt = Time.unscaledTime + RefreshInterval;
            RefreshSelectedTile();
        }
    }

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    public void ShowTile(BoardTile tile)
    {
        if (tile == null || tile.TileType != TileType.City)
        {
            Hide();
            return;
        }

        selectedTile = tile;

        if (panelRoot != null && !panelRoot.activeSelf)
        {
            panelRoot.SetActive(true);
        }

        nextRefreshAt = 0f;
        RefreshSelectedTile();
    }

    public void Hide()
    {
        selectedTile = null;

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void ResolveGameplayReferences()
    {
        if (boardGenerator == null)
        {
            boardGenerator = UnityEngine.Object.FindAnyObjectByType<BoardGenerator>();
        }

        if (developmentManager == null)
        {
            developmentManager = UnityEngine.Object.FindAnyObjectByType<PropertyDevelopmentManager>();
        }

        if (boardCamera == null)
        {
            boardCamera = Camera.main;

            if (boardCamera == null)
            {
                boardCamera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            }
        }
    }

    private bool TryGetPointerPress(out Vector2 screenPosition)
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;

            if (touch.press.wasPressedThisFrame)
            {
                screenPosition = touch.position.ReadValue();
                return true;
            }
        }

        screenPosition = default;
        return false;
    }

    private void HandleWorldPointerPress(Vector2 screenPosition)
    {
        if (IsPointerOverUi(screenPosition))
        {
            return;
        }

        ResolveGameplayReferences();

        if (boardCamera == null)
        {
            return;
        }

        Ray ray = boardCamera.ScreenPointToRay(screenPosition);

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                1000f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            if (IsOpen)
            {
                Hide();
            }

            return;
        }

        BoardTile tile = hit.collider != null
            ? hit.collider.GetComponentInParent<BoardTile>()
            : null;

        if (tile == null || tile.TileType != TileType.City)
        {
            if (IsOpen)
            {
                Hide();
            }

            return;
        }

        ShowTile(tile);
    }

    private static bool IsPointerOverUi(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            return false;
        }

        PointerEventData pointerData = new PointerEventData(eventSystem)
        {
            position = screenPosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointerData, results);
        return results.Count > 0;
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject(
            "Canvas_CityInformation",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        safeAreaRoot = CreateRect("SafeArea", canvasObject.transform);
        Stretch(safeAreaRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        panelRoot = new GameObject(
            "CityInformationPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(Shadow));
        panelRoot.transform.SetParent(safeAreaRoot, false);

        panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.sizeDelta = new Vector2(430f, 540f);
        panelRect.anchoredPosition = new Vector2(-24f, 0f);

        Image panelImage = panelRoot.GetComponent<Image>();
        panelImage.color = new Color(0.045f, 0.055f, 0.075f, 0.975f);
        panelImage.raycastTarget = true;

        Shadow shadow = panelRoot.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(0f, -6f);

        GameObject stripeObject = new GameObject(
            "GroupStripe",
            typeof(RectTransform),
            typeof(Image));
        stripeObject.transform.SetParent(panelRoot.transform, false);

        RectTransform stripeRect = stripeObject.GetComponent<RectTransform>();
        stripeRect.anchorMin = new Vector2(0f, 0f);
        stripeRect.anchorMax = new Vector2(0f, 1f);
        stripeRect.pivot = new Vector2(0f, 0.5f);
        stripeRect.offsetMin = new Vector2(0f, 0f);
        stripeRect.offsetMax = new Vector2(7f, 0f);

        groupStripe = stripeObject.GetComponent<Image>();
        groupStripe.color = new Color(0.27f, 0.63f, 0.93f, 1f);
        groupStripe.raycastTarget = false;

        titleText = CreateText(
            "CityName",
            panelRoot.transform,
            28f,
            FontStyles.Bold,
            TextAlignmentOptions.Left,
            Color.white);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.offsetMin = new Vector2(28f, -70f);
        titleRect.offsetMax = new Vector2(-64f, -16f);
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 18f;
        titleText.fontSizeMax = 28f;
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        titleText.overflowMode = TextOverflowModes.Truncate;

        subtitleText = CreateText(
            "Context",
            panelRoot.transform,
            14f,
            FontStyles.Normal,
            TextAlignmentOptions.Left,
            new Color(0.76f, 0.81f, 0.88f, 1f));
        RectTransform subtitleRect = subtitleText.rectTransform;
        subtitleRect.anchorMin = new Vector2(0f, 1f);
        subtitleRect.anchorMax = new Vector2(1f, 1f);
        subtitleRect.pivot = new Vector2(0f, 1f);
        subtitleRect.offsetMin = new Vector2(28f, -104f);
        subtitleRect.offsetMax = new Vector2(-22f, -72f);
        subtitleText.enableAutoSizing = true;
        subtitleText.fontSizeMin = 10f;
        subtitleText.fontSizeMax = 14f;
        subtitleText.textWrappingMode = TextWrappingModes.NoWrap;
        subtitleText.overflowMode = TextOverflowModes.Truncate;

        BuildCloseButton();

        GameObject separator = new GameObject(
            "HeaderSeparator",
            typeof(RectTransform),
            typeof(Image));
        separator.transform.SetParent(panelRoot.transform, false);

        RectTransform separatorRect = separator.GetComponent<RectTransform>();
        separatorRect.anchorMin = new Vector2(0f, 1f);
        separatorRect.anchorMax = new Vector2(1f, 1f);
        separatorRect.pivot = new Vector2(0.5f, 1f);
        separatorRect.offsetMin = new Vector2(20f, -112f);
        separatorRect.offsetMax = new Vector2(-20f, -110f);

        Image separatorImage = separator.GetComponent<Image>();
        separatorImage.color = new Color(1f, 1f, 1f, 0.10f);
        separatorImage.raycastTarget = false;

        BuildScrollableBody();

        footerText = CreateText(
            "InformationOnlyFooter",
            panelRoot.transform,
            11f,
            FontStyles.Italic,
            TextAlignmentOptions.Center,
            new Color(0.64f, 0.70f, 0.78f, 1f));
        RectTransform footerRect = footerText.rectTransform;
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.offsetMin = new Vector2(20f, 14f);
        footerRect.offsetMax = new Vector2(-20f, 50f);
        footerText.textWrappingMode = TextWrappingModes.Normal;

        closeButton.onClick.AddListener(Hide);
    }

    private void BuildCloseButton()
    {
        GameObject buttonObject = new GameObject(
            "CloseButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(panelRoot.transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(40f, 40f);
        rect.anchoredPosition = new Vector2(-14f, -14f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.08f);

        closeButton = buttonObject.GetComponent<Button>();

        ColorBlock colors = closeButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        closeButton.colors = colors;

        closeButtonText = CreateText(
            "Label",
            buttonObject.transform,
            20f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white);
        Stretch(
            closeButtonText.rectTransform,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);
        closeButtonText.text = "×";
        closeButtonText.raycastTarget = false;
    }

    private void BuildScrollableBody()
    {
        GameObject scrollObject = new GameObject(
            "BodyScroll",
            typeof(RectTransform),
            typeof(ScrollRect));
        scrollObject.transform.SetParent(panelRoot.transform, false);

        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        Stretch(
            scrollRectTransform,
            Vector2.zero,
            Vector2.one,
            new Vector2(22f, 58f),
            new Vector2(-18f, -120f));

        ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 26f;

        GameObject viewportObject = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));
        viewportObject.transform.SetParent(scrollObject.transform, false);

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        Stretch(viewportRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Image viewportImage = viewportObject.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.001f);
        viewportImage.raycastTarget = true;

        GameObject contentObject = new GameObject("BodyContent", typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);

        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 10, 0, 12);
        layout.spacing = 0f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = contentObject.AddComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        bodyText = CreateText(
            "BodyText",
            contentObject.transform,
            15f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft,
            new Color(0.91f, 0.93f, 0.96f, 1f));
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.overflowMode = TextOverflowModes.Overflow;
        bodyText.richText = true;
        bodyText.lineSpacing = 8f;

        LayoutElement bodyLayout = bodyText.gameObject.AddComponent<LayoutElement>();
        bodyLayout.flexibleWidth = 1f;

        ContentSizeFitter bodyFitter = bodyText.gameObject.AddComponent<ContentSizeFitter>();
        bodyFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
    }

    private void RefreshSelectedTile()
    {
        if (selectedTile == null)
        {
            Hide();
            return;
        }

        ResolveGameplayReferences();

        string mapContext = ResolveMapContext();
        string groupName = string.IsNullOrWhiteSpace(selectedTile.GroupDisplayName)
            ? L("No group", "Grup yok", "Sin grupo", "Aucun groupe", "Keine Gruppe", "그룹 없음", "Без группы")
            : selectedTile.GroupDisplayName;

        titleText.text = selectedTile.DisplayName;
        subtitleText.text = string.IsNullOrWhiteSpace(mapContext)
            ? groupName
            : $"{mapContext}  •  {groupName}";

        Color stripeColor = selectedTile.GroupColor;
        if (stripeColor.a <= 0.01f)
        {
            stripeColor = new Color(0.27f, 0.63f, 0.93f, 1f);
        }
        stripeColor.a = 1f;
        groupStripe.color = stripeColor;

        bodyText.text = BuildBodyText(selectedTile, mapContext, groupName);
        footerText.text = L(
            "Information only — no gameplay action is performed.",
            "Yalnızca bilgi amaçlıdır — oyun eylemi gerçekleştirmez.",
            "Solo información — no realiza ninguna acción de juego.",
            "Informations uniquement — aucune action de jeu n’est effectuée.",
            "Nur Information — es wird keine Spielaktion ausgeführt.",
            "정보 전용 — 게임 동작을 실행하지 않습니다.",
            "Только информация — игровые действия не выполняются.");

        closeButtonText.text = "×";
        ApplyResolvedFonts();
    }

    private string BuildBodyText(BoardTile tile, string mapContext, string groupName)
    {
        int developmentLevel = developmentManager != null
            ? developmentManager.GetDevelopmentLevel(tile)
            : 0;

        int developmentCost = developmentManager != null
            ? developmentManager.GetDevelopmentCost(tile)
            : tile.DevelopmentCost;

        int effectiveRent = developmentManager != null
            ? developmentManager.GetEffectiveRent(tile)
            : tile.BaseRent;

        string owner = ResolveOwnerName(tile);
        string description = tile.Description;

        if (string.IsNullOrWhiteSpace(description))
        {
            string context = string.IsNullOrWhiteSpace(mapContext)
                ? L("current", "mevcut", "actual", "actuelle", "aktuellen", "현재", "текущей")
                : mapContext;

            description = L(
                "{0} is a featured location on the {1} Atlas Board map.",
                "{0}, Atlas Board'un {1} haritasındaki konumlardan biridir.",
                "{0} es una ubicación destacada del mapa {1} de Atlas Board.",
                "{0} est un lieu présenté sur la carte {1} d’Atlas Board.",
                "{0} ist ein Ort auf der Atlas-Board-Karte {1}.",
                "{0}은(는) Atlas Board의 {1} 지도에 등장하는 장소입니다.",
                "{0} — одна из локаций на карте {1} в Atlas Board.",
                tile.DisplayName,
                context);
        }

        StringBuilder builder = new StringBuilder();

        AppendSection(
            builder,
            L("ABOUT", "HAKKINDA", "ACERCA DE", "À PROPOS", "INFO", "정보", "О МЕСТЕ"),
            description);

        AppendValue(
            builder,
            L("Owner", "Sahip", "Propietario", "Propriétaire", "Besitzer", "소유자", "Владелец"),
            owner);

        AppendValue(
            builder,
            L("Property group", "Mülk grubu", "Grupo de propiedad", "Groupe de propriété", "Grundstücksgruppe", "부동산 그룹", "Группа собственности"),
            groupName);

        AppendValue(
            builder,
            L("Purchase price", "Satın alma fiyatı", "Precio de compra", "Prix d’achat", "Kaufpreis", "구매 가격", "Цена покупки"),
            $"{tile.PurchasePrice:N0} ₵");

        AppendValue(
            builder,
            L("Base rent", "Temel kira", "Renta base", "Loyer de base", "Grundmiete", "기본 임대료", "Базовая аренда"),
            $"{tile.BaseRent:N0} ₵");

        AppendValue(
            builder,
            L("Current rent", "Güncel kira", "Renta actual", "Loyer actuel", "Aktuelle Miete", "현재 임대료", "Текущая аренда"),
            $"{effectiveRent:N0} ₵");

        AppendValue(
            builder,
            L("Development cost", "Geliştirme maliyeti", "Coste de desarrollo", "Coût de développement", "Ausbaukosten", "개발 비용", "Стоимость развития"),
            $"{developmentCost:N0} ₵");

        int maximumLevel = developmentManager != null
            ? developmentManager.MaximumDevelopmentLevel
            : 4;

        AppendValue(
            builder,
            L("Development level", "Geliştirme seviyesi", "Nivel de desarrollo", "Niveau de développement", "Ausbaustufe", "개발 레벨", "Уровень развития"),
            $"{developmentLevel} / {maximumLevel}");

        return builder.ToString();
    }

    private string ResolveOwnerName(BoardTile tile)
    {
        if (tile == null || !tile.IsOwned)
        {
            return L("Unowned", "Sahipsiz", "Sin propietario", "Sans propriétaire", "Unbesetzt", "소유자 없음", "Без владельца");
        }

        PlayerGameState[] players = UnityEngine.Object.FindObjectsByType<PlayerGameState>();

        foreach (PlayerGameState player in players)
        {
            if (player != null && player.PlayerSlotIndex == tile.OwnerPlayerIndex)
            {
                return AtlasBoardL.PlayerName(player);
            }
        }

        return $"{L("Player", "Oyuncu", "Jugador", "Joueur", "Spieler", "플레이어", "Игрок")} {tile.OwnerPlayerIndex + 1}";
    }

    private string ResolveMapContext()
    {
        ResolveGameplayReferences();

        BoardMapDefinition map = boardGenerator != null
            ? boardGenerator.ActiveMapDefinition
            : null;

        return map != null
            ? map.DisplayName ?? string.Empty
            : string.Empty;
    }

    private static void AppendSection(StringBuilder builder, string heading, string value)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append("<size=12><color=#8EA0B8><b>");
        builder.Append(heading);
        builder.AppendLine("</b></color></size>");
        builder.AppendLine(value ?? string.Empty);
    }

    private static void AppendValue(StringBuilder builder, string label, string value)
    {
        builder.AppendLine();
        builder.Append("<color=#8EA0B8>");
        builder.Append(label);
        builder.Append("</color>\n<b>");
        builder.Append(value ?? "-");
        builder.AppendLine("</b>");
    }

    private void HandleLanguageChanged()
    {
        if (IsOpen)
        {
            RefreshSelectedTile();
        }
        else
        {
            ApplyResolvedFonts();
        }
    }

    private void ApplyResolvedFonts()
    {
        AtlasBoardLocalizationManager manager = AtlasBoardLocalizationManager.Instance;

        if (manager == null || panelRoot == null)
        {
            return;
        }

        TMP_Text[] texts = panelRoot.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text == null)
            {
                continue;
            }

            TMP_FontAsset baseFont = TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : text.font;

            TMP_FontAsset resolved = manager.ResolveFont(baseFont);
            if (resolved != null)
            {
                text.font = resolved;
            }
        }
    }

    private void ApplySafeArea(bool force)
    {
        if (safeAreaRoot == null)
        {
            return;
        }

        Rect safeArea = Screen.safeArea;
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);

        if (!force && safeArea == lastSafeArea && screenSize == lastScreenSize)
        {
            return;
        }

        lastSafeArea = safeArea;
        lastScreenSize = screenSize;

        if (Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        safeAreaRoot.anchorMin = anchorMin;
        safeAreaRoot.anchorMax = anchorMax;
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject gameObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        gameObject.transform.SetParent(parent, false);

        TMP_Text text = gameObject.GetComponent<TMP_Text>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.richText = true;

        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        return text;
    }

    private static void Stretch(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static string L(
        string en,
        string tr,
        string es,
        string fr,
        string de,
        string ko,
        string ru,
        params object[] args)
    {
        return AtlasBoardL.R(en, tr, es, fr, de, ko, ru, args);
    }
}
