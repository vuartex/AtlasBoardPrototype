using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


/// <summary>
/// Main Menu player help and license presentation, built from Unity UI and TMP.
/// No IMGUI window, second menu authority or gameplay effects. The main menu
/// canvas owns all surfaces; ScrollRect works with touch, mouse and controllers.
/// </summary>
[DefaultExecutionOrder(32001)]
public sealed class AtlasBoardPlayerHelpCredits : MonoBehaviour
{
    private static AtlasBoardPlayerHelpCredits instance;
    private AtlasBoardMainMenuController menu;
    private RectTransform menuRoot;
    private RectTransform overlay;
    private RectTransform contents;
    private TextMeshProUGUI helpButtonLabel;
    private TextMeshProUGUI creditsButtonLabel;
    private TextMeshProUGUI titleLabel;
    private TextMeshProUGUI subtitleLabel;
    private TextMeshProUGUI helpTabLabel;
    private TextMeshProUGUI creditsTabLabel;
    private TextMeshProUGUI closeLabel;
    private Image helpTabBackground;
    private Image creditsTabBackground;
    private TMP_FontAsset menuFont;
    private static Sprite roundedSprite;
    private bool showingHelp = true;
    private string currentLanguage = string.Empty;


    private static readonly Color Ink = Hex("#F0F6FF");
    private static readonly Color Secondary = Hex("#A9BED5");
    private static readonly Color Midnight = Hex("#102239");
    private static readonly Color Panel = Hex("#162E49");
    private static readonly Color Card = Hex("#213C5A");
    private static readonly Color Cyan = Hex("#39C7E5");
    private static readonly Color Gold = Hex("#F7B928");


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject host = new GameObject("AtlasBoard_Help_Credits_Presentation");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AtlasBoardPlayerHelpCredits>();
    }


    private void OnDestroy() { if (instance == this) instance = null; }


    private void Update()
    {
        if (overlay == null || !overlay.gameObject.activeSelf) return;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
#endif
    }


    private void LateUpdate()
    {
        if (menu == null || menuRoot == null || !menuRoot)
        {
            menu = UnityEngine.Object.FindAnyObjectByType<AtlasBoardMainMenuController>();
            if (menu == null) return;
            foreach (RectTransform child in menu.GetComponentsInChildren<RectTransform>(true))
                if (child != null && child.name == "MainMenu") { menuRoot = child; break; }
            if (menuRoot == null) return;
        }
        if (overlay == null || !overlay) BuildUi();
        string language = Language;
        if (!string.Equals(language, currentLanguage, StringComparison.Ordinal))
        {
            currentLanguage = language;
            RefreshText();
        }
        // The menu resets visibility between matches. An open modal may never
        // bleed over the lobby, game, Career or Store.
        if (!menuRoot.gameObject.activeInHierarchy && overlay != null)
            overlay.gameObject.SetActive(false);
    }


    private string Language
    {
        get
        {
            AtlasBoardLocalizationManager lm = AtlasBoardLocalizationManager.Instance;
            return lm == null ? "en" : (lm.CurrentLanguageCode ?? "en").ToLowerInvariant();
        }
    }


    private static string Tr(string lang, string en, string tr, string es,
        string fr, string de, string ko, string ru)
    {
        switch (lang)
        {
            case "tr": return tr;
            case "es": return es;
            case "fr": return fr;
            case "de": return de;
            case "ko": return ko;
            case "ru": return ru;
            default: return en;
        }
    }


    private string HelpTitle => Tr(Language, "HELP / FAQ", "YARDIM / SSS", "AYUDA / PREGUNTAS", "AIDE / FAQ", "HILFE / FAQ", "도움말 / FAQ", "ПОМОЩЬ / FAQ");
    private string CreditsTitle => Tr(Language, "CREDITS & LICENSES", "KATKIDA BULUNANLAR / LİSANSLAR", "CRÉDITOS Y LICENCIAS", "CRÉDITS ET LICENCES", "MITWIRKENDE & LIZENZEN", "제작진 / 라이선스", "АВТОРЫ И ЛИЦЕНЗИИ");
    private string CloseLabel => Tr(Language, "CLOSE", "KAPAT", "CERRAR", "FERMER", "SCHLIESSEN", "닫기", "ЗАКРЫТЬ");


    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString(value, out Color color);
        return color;
    }


    private static RectTransform Rect(Transform parent, string name)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }


    // One generated, reusable 9-slice sprite. No imported textures or font files.
    // Rounded cards/buttons are actual Unity UI geometry, not IMGUI boxes.
    private static Sprite RoundedSprite()
    {
        if (roundedSprite != null) return roundedSprite;
        const int side = 64;
        const float radius = 14f;
        Texture2D texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
        texture.name = "AtlasBoard_Runtime_Ui_Rounded_64";
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            float px = Mathf.Max(Mathf.Abs(x + 0.5f - 32f) - (32f - radius), 0f);
            float py = Mathf.Max(Mathf.Abs(y + 0.5f - 32f) - (32f - radius), 0f);
            float distance = Mathf.Sqrt(px * px + py * py);
            float alpha = Mathf.Clamp01(radius - distance + 0.5f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply(false, true);
        roundedSprite = Sprite.Create(texture,
            new Rect(0f, 0f, side, side), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(17f, 17f, 17f, 17f));
        roundedSprite.name = "AtlasBoard_Runtime_Ui_Rounded";
        return roundedSprite;
    }


    private static Image Background(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        if (rect.name != "AtlasBoard_Information_Modal" && rect.name != "AccentBar")
        {
            image.sprite = RoundedSprite();
            image.type = Image.Type.Sliced;
        }
        image.raycastTarget = true;
        return image;
    }


    private TextMeshProUGUI Text(RectTransform parent, string name,
        string value, int size, Color color, FontStyles weight = FontStyles.Normal,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        RectTransform child = Rect(parent, name);
        TextMeshProUGUI label = child.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = menuFont != null ? menuFont : TMP_Settings.defaultFontAsset;
        label.fontSize = size;
        label.fontStyle = weight;
        label.color = color;
        label.text = value;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }


    private TextMeshProUGUI Button(RectTransform parent, string name,
        Vector2 anchoredPosition, Vector2 size, Color color,
        string label, Action clicked, int textSize = 19)
    {
        RectTransform rect = Rect(parent, name);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image bg = Background(rect, color);
        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.75f, 0.9f, 1f, 0.16f);
        outline.effectDistance = new Vector2(1f, -1f);
        UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.83f, 0.93f, 1f, 1f);
        colors.pressedColor = new Color(0.68f, 0.87f, 0.95f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() => clicked());
        TextMeshProUGUI caption = Text(rect, "Label", label, textSize, Ink,
            FontStyles.Bold, TextAlignmentOptions.Center);
        caption.rectTransform.anchorMin = Vector2.zero;
        caption.rectTransform.anchorMax = Vector2.one;
        caption.rectTransform.offsetMin = new Vector2(10f, 4f);
        caption.rectTransform.offsetMax = new Vector2(-10f, -4f);
        caption.enableAutoSizing = true;
        caption.fontSizeMin = 12f;
        caption.fontSizeMax = textSize;
        return caption;
    }


    private void BuildUi()
    {
        TMP_Text sample = null;
        foreach (TMP_Text entry in menuRoot.GetComponentsInChildren<TMP_Text>(true))
            if (entry != null && entry.font != null) { sample = entry; break; }
        menuFont = sample != null ? sample.font : TMP_Settings.defaultFontAsset;


        helpButtonLabel = Button(menuRoot, "AtlasBoard_Help_Entry",
            new Vector2(20f, 18f), new Vector2(188f, 48f), Panel,
            "HELP / FAQ", () => Open(true));
        creditsButtonLabel = Button(menuRoot, "AtlasBoard_Credits_Entry",
            new Vector2(220f, 18f), new Vector2(228f, 48f), Panel,
            "CREDITS & LICENSES", () => Open(false), 18);


        overlay = Rect(menuRoot, "AtlasBoard_Information_Modal");
        overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero; overlay.offsetMax = Vector2.zero;
        Image dim = Background(overlay, new Color(0.02f, 0.055f, 0.11f, 0.83f));
        UnityEngine.UI.Button backdrop = overlay.gameObject.AddComponent<UnityEngine.UI.Button>();
        backdrop.targetGraphic = dim;
        backdrop.onClick.AddListener(Close);


        RectTransform panel = Rect(overlay, "InformationDialog");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(920f, 728f);
        Background(panel, Midnight);
        Shadow shadow = panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(0f, -12f);
        Outline border = panel.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(0.35f, 0.65f, 0.85f, 0.42f);
        border.effectDistance = new Vector2(2f, -2f);


        RectTransform accent = Rect(panel, "AccentBar");
        accent.anchorMin = new Vector2(0f, 1f);
        accent.anchorMax = new Vector2(1f, 1f);
        accent.pivot = new Vector2(0.5f, 1f);
        accent.sizeDelta = new Vector2(0f, 7f);
        Background(accent, Gold).raycastTarget = false;


        RectTransform titleArea = Rect(panel, "TitleArea");
        titleArea.anchorMin = new Vector2(0f, 1f);
        titleArea.anchorMax = new Vector2(1f, 1f);
        titleArea.pivot = new Vector2(0.5f, 1f);
        titleArea.anchoredPosition = new Vector2(0f, -29f);
        titleArea.sizeDelta = new Vector2(-60f, 82f);
        titleLabel = Text(titleArea, "Title", HelpTitle, 36, Ink, FontStyles.Bold);
        titleLabel.rectTransform.anchorMin = new Vector2(0f, 0.46f);
        titleLabel.rectTransform.anchorMax = new Vector2(0.86f, 1f);
        titleLabel.rectTransform.offsetMin = titleLabel.rectTransform.offsetMax = Vector2.zero;
        subtitleLabel = Text(titleArea, "Subtitle", "", 16, Secondary);
        subtitleLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
        subtitleLabel.rectTransform.anchorMax = new Vector2(1f, 0.43f);
        subtitleLabel.rectTransform.offsetMin = subtitleLabel.rectTransform.offsetMax = Vector2.zero;
        Button(titleArea, "CloseX", new Vector2(776f, 35f), new Vector2(62f, 44f), Card,
            "X", Close, 23);


        RectTransform tabs = Rect(panel, "Tabs");
        tabs.anchorMin = new Vector2(0f, 1f);
        tabs.anchorMax = new Vector2(1f, 1f);
        tabs.pivot = new Vector2(0.5f, 1f);
        tabs.sizeDelta = new Vector2(-60f, 50f);
        tabs.anchoredPosition = new Vector2(0f, -130f);
        helpTabLabel = Button(tabs, "HelpTab", new Vector2(0f, 0f), new Vector2(250f, 48f), Card,
            "HELP / FAQ", () => SelectTab(true), 17);
        creditsTabLabel = Button(tabs, "CreditsTab", new Vector2(262f, 0f), new Vector2(300f, 48f), Card,
            "CREDITS & LICENSES", () => SelectTab(false), 17);
        helpTabBackground = helpTabLabel.GetComponentInParent<Image>();
        creditsTabBackground = creditsTabLabel.GetComponentInParent<Image>();


        RectTransform viewport = Rect(panel, "ScrollViewport");
        viewport.anchorMin = new Vector2(0f, 0f);
        viewport.anchorMax = new Vector2(1f, 1f);
        viewport.offsetMin = new Vector2(30f, 90f);
        viewport.offsetMax = new Vector2(-30f, -194f);
        Background(viewport, Panel);
        viewport.gameObject.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 22f;
        contents = Rect(viewport, "ScrollContent");
        contents.anchorMin = new Vector2(0f, 1f);
        contents.anchorMax = new Vector2(1f, 1f);
        contents.pivot = new Vector2(0.5f, 1f);
        contents.sizeDelta = Vector2.zero;
        contents.anchoredPosition = Vector2.zero;
        VerticalLayoutGroup list = contents.gameObject.AddComponent<VerticalLayoutGroup>();
        list.padding = new RectOffset(16, 16, 16, 16);
        list.spacing = 12f;
        list.childAlignment = TextAnchor.UpperCenter;
        list.childControlWidth = true;
        list.childControlHeight = true;
        list.childForceExpandWidth = true;
        list.childForceExpandHeight = false;
        ContentSizeFitter fitter = contents.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contents;
        scroll.viewport = viewport;


        RectTransform foot = Rect(panel, "Footer");
        foot.anchorMin = new Vector2(0f, 0f);
        foot.anchorMax = new Vector2(1f, 0f);
        foot.pivot = new Vector2(0.5f, 0f);
        foot.sizeDelta = new Vector2(-60f, 72f);
        foot.anchoredPosition = new Vector2(0f, 12f);
        TextMeshProUGUI caption = Text(foot, "Note", "ATLAS BOARD  /  PLAYER INFORMATION", 14, Secondary);
        caption.rectTransform.anchorMin = new Vector2(0f, 0f);
        caption.rectTransform.anchorMax = new Vector2(0.67f, 1f);
        caption.rectTransform.offsetMin = caption.rectTransform.offsetMax = Vector2.zero;
        closeLabel = Button(foot, "Close", new Vector2(660f, 14f), new Vector2(195f, 44f),
            Hex("#1689B2"), CloseLabel, Close, 18);
        overlay.gameObject.SetActive(false);
        RefreshText();
    }


    private void RefreshText()
    {
        if (helpButtonLabel == null) return;
        helpButtonLabel.text = HelpTitle;
        creditsButtonLabel.text = CreditsTitle;
        helpTabLabel.text = HelpTitle;
        creditsTabLabel.text = CreditsTitle;
        closeLabel.text = CloseLabel;
        if (overlay != null && overlay.gameObject.activeSelf) RenderContent();
    }


    private void Open(bool help)
    {
        if (overlay == null) return;
        showingHelp = help;
        overlay.gameObject.SetActive(true);
        overlay.SetAsLastSibling();
        RenderContent();
    }


    private void SelectTab(bool help)
    {
        showingHelp = help;
        RenderContent();
    }


    private void Close()
    {
        if (overlay != null) overlay.gameObject.SetActive(false);
    }


    private void RenderContent()
    {
        if (contents == null) return;
        titleLabel.text = showingHelp ? HelpTitle : CreditsTitle;
        subtitleLabel.text = showingHelp
            ? Tr(Language,"Quick answers for online play, reconnect and AtlasBoard systems.","Oyun, bağlantı ve AtlasBoard sistemleri hakkında kısa yanıtlar.","Ayuda rápida para jugar y reconectar.","Conseils pour jouer et se reconnecter.","Kurze Hilfe zu Spielen und Verbindungen.","게임과 연결에 관한 빠른 도움말.","Ответы об игре и подключении.")
            : Tr(Language,"The creators and licenses behind Atlas Board.","Atlas Board'un arkasındaki üreticiler ve lisanslar.","Autores y licencias de Atlas Board.","Créateurs et licences d'Atlas Board.","Urheber und Lizenzen von Atlas Board.","제작진 및 라이선스.","Авторы и лицензии Atlas Board.");
        helpTabBackground.color = showingHelp ? Hex("#177EAA") : Card;
        creditsTabBackground.color = showingHelp ? Card : Hex("#177EAA");
        for (int i = contents.childCount - 1; i >= 0; i--)
        {
            Transform child = contents.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        if (showingHelp) DrawHelp(); else DrawCredits();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contents);
        Canvas.ForceUpdateCanvases();
        ScrollRect scroll = contents.GetComponentInParent<ScrollRect>();
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        else contents.anchoredPosition = Vector2.zero;
    }


    private void Entry(string question, string answer)
    {
        RectTransform card = Rect(contents, "InformationCard");
        Background(card, Card);
        VerticalLayoutGroup group = card.gameObject.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(20, 20, 17, 19);
        group.spacing = 7f;
        group.childControlWidth = true; group.childControlHeight = true;
        group.childForceExpandWidth = true; group.childForceExpandHeight = false;
        ContentSizeFitter fit = card.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TextMeshProUGUI heading = Text(card, "Heading", question, 22, Gold, FontStyles.Bold);
        heading.margin = Vector4.zero;
        heading.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TextMeshProUGUI paragraph = Text(card, "Details", answer, 18, Ink);
        paragraph.margin = Vector4.zero;
        paragraph.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }


    private void Note(string message)
    {
        Entry(Tr(Language,"IMPORTANT","ÖNEMLİ","IMPORTANTE","IMPORTANT","WICHTIG","중요","ВАЖНО"), message);
    }


    private void DrawHelp()
    {
        string l = Language;
        Entry(Tr(l,"How does Atlas World Journey work?","Atlas Dünya Yolculuğu nasıl çalışıyor?","¿Cómo funciona Viaje Mundial Atlas?","Comment fonctionne le Voyage Mondial Atlas ?","Wie funktioniert die Atlas-Weltreise?","아틀라스 세계 여행은 어떻게 진행되나요?","Как работает мировое путешествие Атласа?"),
            Tr(l,"Play verified online matches from November 3, 2026 through June 30, 2027. Complete eight missions, then open Store > Event > Rewards to claim each unlocked reward yourself.","3 Kasım 2026 – 30 Haziran 2027 arasında doğrulanmış çevrim içi maçlar oyna. Sekiz görevi tamamla; açılan ödüllerini Mağaza > Etkinlik > Ödüller bölümünden tek tek kendin al.","Juega partidas online verificadas entre el 3 de noviembre de 2026 y el 30 de junio de 2027. Reclama las recompensas en Tienda > Evento > Premios.","Jouez des parties en ligne vérifiées du 3 novembre 2026 au 30 juin 2027. Récupérez chaque récompense dans Boutique > Événement > Récompenses.","Spiele vom 3. November 2026 bis zum 30. Juni 2027 bestätigte Online-Partien. Hole Belohnungen unter Shop > Event > Belohnungen ab.","2026년 11월 3일부터 2027년 6월 30일까지 인증된 온라인 경기를 플레이하세요. 상점 > 이벤트 > 보상에서 직접 수령하세요.","Играйте проверенные онлайн-матчи с 3 ноября 2026 по 30 июня 2027 года. Забирайте награды в Магазин > События > Награды."));
        Entry(Tr(l,"How do I unlock the World Map and Atlas Horizon Board?","Dünya Haritası ve Atlas Ufuk Tahtası nasıl açılır?","¿Cómo desbloqueo el mapa mundial y el tablero Atlas?","Comment obtenir la carte du monde et le plateau Atlas ?","Wie schalte ich Weltkarte und Atlas-Horizont frei?","세계 지도와 아틀라스 보드는 어떻게 잠금 해제하나요?","Как открыть карту мира и доску Атласа?"),
            Tr(l,"Claim the first seven event rewards, then claim the World Map as reward eight. Claim all eight rewards to unlock the exclusive Atlas Horizon Board. Neither item is sold in the Shop; owned maps work in online and offline modes.","Önce ilk yedi etkinlik ödülünü al, ardından sekizinci ödül olarak Dünya Haritası'nı al. Sekiz ödülün tamamını aldıktan sonra özel Atlas Ufuk Tahtası'nı kazan. İkisi de mağazada satılmaz; sahip olduğun harita çevrim içi ve çevrim dışı oynanabilir.","Reclama los siete primeros premios y después el Mapa Mundial. Obtén los ocho para recibir el tablero Atlas exclusivo. No se venden en la tienda.","Récupérez sept récompenses puis la Carte du monde en huitième. Les huit récompenses débloquent le plateau Atlas exclusif, introuvable en boutique.","Hole zuerst sieben Belohnungen und danach die Weltkarte. Alle acht Belohnungen schalten das exklusive Atlas-Brett frei; beide sind nicht kaufbar.","처음 7개 보상을 받은 뒤 8번째 세계 지도를 받으세요. 보상 8개를 모두 받으면 한정 아틀라스 보드를 얻습니다. 상점에서는 판매하지 않습니다.","Заберите семь наград, затем карту мира восьмой. Все восемь наград открывают особую доску Атласа; они не продаются."));
        Entry(Tr(l,"Why didn't my mission reward arrive automatically?","Görev ödülüm neden otomatik gelmedi?","¿Por qué no recibí el premio automáticamente?","Pourquoi ma récompense n'arrive-t-elle pas automatiquement ?","Warum kam die Missionsbelohnung nicht automatisch?","임무 보상이 자동으로 지급되지 않은 이유는?","Почему награда за задание не выдана автоматически?"),
            Tr(l,"Finishing a mission only makes its reward claimable. Tap Claim in Store > Event > Rewards to receive it. Progress is account-specific and comes from verified online match results.","Görevi bitirmek ödülü yalnızca alınabilir yapar. Hesabına eklenmesi için Mağaza > Etkinlik > Ödüller kısmından ÖDÜLÜ AL'a bas. İlerleme doğrulanmış online maç sonuçlarından hesaplanır.","Completar una misión habilita el premio; debes pulsar Reclamar en Tienda > Evento > Premios.","Terminer une mission rend sa récompense disponible, mais il faut la récupérer dans Boutique > Événement > Récompenses.","Ein Missionsabschluss macht die Belohnung abholbereit. Hole sie im Shop > Event > Belohnungen ab.","임무를 완료하면 보상을 받을 수 있게 됩니다. 상점 > 이벤트 > 보상에서 직접 수령하세요.","Задание только открывает награду. Нажмите Получить в Магазин > События > Награды."));
        Entry(Tr(l,"How do promo codes work?","Promosyon kodları nasıl kullanılır?","¿Cómo funcionan los códigos promocionales?","Comment utiliser un code promotionnel ?","Wie löse ich einen Aktionscode ein?","프로모션 코드는 어떻게 사용하나요?","Как использовать промокод?"),
            Tr(l,"Open Store > Redeem Code and enter a valid active code. Each code can be used only once per account. Successful rewards and transaction IDs are stored under your account; invalid, already-used or unavailable codes do not grant rewards.","Mağaza > Kod Kullan bölümünden aktif kodu gir. Her kod hesap başına yalnız bir kez kullanılabilir. Başarılı ödül ve işlem kimliği hesabında saklanır; geçersiz, önceden kullanılmış veya erişilemeyen kodlar ödül vermez.","Introduce un código activo en Tienda > Canjear. Cada código se usa una vez por cuenta y el premio queda registrado.","Saisissez un code actif dans Boutique > Code. Un code ne peut être utilisé qu'une seule fois par compte et son historique est conservé.","Gib einen gültigen Code im Shop > Code ein. Jeder Code gilt nur einmal pro Konto; erfolgreiche Belohnungen werden erfasst.","상점 > 코드 사용에 활성 코드를 입력하세요. 코드는 계정당 한 번만 사용되며 보상과 거래 ID가 저장됩니다.","Введите активный код в Магазин > Промокод. Один код используется только один раз на аккаунт, награда записывается."));
        Entry(Tr(l,"How do I join a friend?","Arkadaşımın odasına nasıl katılırım?","¿Cómo me uno a un amigo?","Comment rejoindre un ami ?","Wie trete ich einem Freund bei?","친구 방에 어떻게 참가하나요?","Как присоединиться к другу?"),
            Tr(l,"Open Private Table and enter the six-digit room code, or browse Public Rooms.","Private Table'a girip altı haneli oda kodunu yaz veya Public Rooms'tan oda seç.","Abre Private Table y escribe el código de seis dígitos, o busca en Public Rooms.","Ouvrez Private Table et saisissez le code à six chiffres, ou utilisez Public Rooms.","Öffne Private Table und gib den sechsstelligen Raumcode ein, oder nutze Public Rooms.","Private Table에서 6자리 방 코드를 입력하거나 Public Rooms를 이용하세요.","Откройте Private Table и введите шестизначный код или выберите Public Rooms."));
        Entry(Tr(l,"What happens if I disconnect?","Bağlantım koparsa ne olur?","¿Qué pasa si pierdo la conexión?","Et si ma connexion est coupée ?","Was passiert bei Verbindungsabbruch?","연결이 끊기면 어떻게 되나요?","Что будет при обрыве связи?"),
            Tr(l,"Reconnect with the same account to recover your seat. A temporary bot may control your turn; a different account cannot claim that seat.","Aynı hesapla geri girerek koltuğunu kurtarabilirsin. Geçici bot sıranı oynayabilir; başka hesap o koltuğu alamaz.","Reconéctate con la misma cuenta. Un bot temporal puede jugar tu turno; otra cuenta no puede ocupar tu asiento.","Reconnectez-vous avec le même compte. Un bot temporaire peut jouer votre tour ; un autre compte ne récupère pas votre place.","Verbinde dich mit demselben Konto erneut. Ein temporärer Bot kann deinen Zug spielen; andere Konten übernehmen deinen Platz nicht.","동일한 계정으로 다시 접속하세요. 임시 봇이 턴을 대신할 수 있지만 다른 계정은 좌석을 차지할 수 없습니다.","Подключитесь с тем же аккаунтом. Временный бот может ходить вместо вас, но другой аккаунт не займёт ваше место."));
        Entry(Tr(l,"How do I rematch?","Tekrar maça nasıl başlarım?","¿Cómo vuelvo a jugar?","Comment rejouer ?","Wie starte ich ein Rematch?","재경기는 어떻게 시작하나요?","Как начать реванш?"),
            Tr(l,"After the result screen, choose Rematch. The existing lobby and authenticated player identities are preserved.","Sonuç ekranında Rematch'i seç. Oda ve doğrulanmış oyuncu kimlikleri korunur.","Tras el resultado, elige Rematch. Se conservan la sala y las identidades.","Après le résultat, choisissez Rematch. Le salon et les identités sont conservés.","Wähle nach dem Ergebnis Rematch. Raum und Spieleridentitäten bleiben erhalten.","결과 화면에서 Rematch를 선택하세요. 로비와 플레이어 신원은 유지됩니다.","После результата выберите Rematch. Комната и учётные записи сохраняются."));
        Entry(Tr(l,"Why can't I use a map or cosmetic?","Bir haritayı veya kozmetiği neden kullanamıyorum?","¿Por qué no puedo usar un mapa u objeto?","Pourquoi un objet est-il indisponible ?","Warum ist eine Karte oder Kosmetik gesperrt?","맵이나 꾸미기 아이템을 왜 못 쓰나요?","Почему карта или предмет недоступны?"),
            Tr(l,"Paid items require ownership on your signed-in account. Bots use the default dice. You cannot equip another player's items.","Ücretli içeriklerde hesabına ait sahiplik doğrulanır. Botlar varsayılan zarı kullanır; başkasının eşyası kuşanılamaz.","Los objetos de pago requieren propiedad verificada. Los bots usan el dado predeterminado.","Les objets payants nécessitent la propriété vérifiée. Les bots utilisent les dés par défaut.","Kostenpflichtige Gegenstände benötigen bestätigten Besitz. Bots verwenden Standardwürfel.","유료 아이템은 계정 소유권 확인이 필요합니다. 봇은 기본 주사위를 사용합니다.","Платные предметы требуют подтверждения владения. Боты используют стандартные кости."));
        Entry(Tr(l,"How do Career, XP and Gold work?","Career, XP ve Gold nasıl çalışıyor?","¿Cómo funcionan Career, XP y Gold?","Comment fonctionnent Career, XP et Gold ?","Wie funktionieren Career, XP und Gold?","Career, XP, Gold는 어떻게 작동하나요?","Как работают Career, XP и Gold?"),
            Tr(l,"Eligible completed online matches record Career progress and verified rewards. A pending result may synchronize after reconnect; rewards are not paid twice.","Uygun şekilde tamamlanan çevrim içi maçlar Career ve doğrulanmış ödülleri kaydeder. Sonuç bağlantı sonrası tamamlanabilir; ödüller iki kez verilmez.","Las partidas online válidas registran progreso y premios verificados sin duplicarlos.","Les parties en ligne éligibles enregistrent la progression et les récompenses, sans doublons.","Gültige abgeschlossene Online-Spiele speichern Karriere und Belohnungen ohne Doppelgutschrift.","조건을 충족하는 온라인 경기 결과는 기록되며 보상은 중복 지급되지 않습니다.","Подходящие завершённые онлайн-матчи сохраняют прогресс и награды без повторных выплат."));
        Entry(Tr(l,"Why is an update required?","Neden güncelleme gerekli?","¿Por qué debo actualizar?","Pourquoi une mise à jour ?","Warum ist ein Update erforderlich?","업데이트가 필요한 이유는?","Зачем нужно обновление?"),
            Tr(l,"Online rooms require compatible game, protocol, rules and content versions. Install the current build on every device.","Çevrim içi odalar uyumlu oyun, protokol, kural ve içerik sürümleri ister. Tüm cihazlara güncel sürümü kur.","Todos los dispositivos necesitan versiones compatibles de juego, protocolo, reglas y contenido.","Tous les appareils doivent utiliser des versions compatibles du jeu et du protocole.","Alle Geräte benötigen kompatible Spiel-, Protokoll-, Regel- und Inhaltsversionen.","모든 기기에 호환되는 게임 및 프로토콜 버전을 설치하세요.","Установите совместимые версии игры и протокола на всех устройствах."));
        Entry(Tr(l,"What do Pulse, Bank and Civic do?","Pulse, Bank ve Civic ne işe yarar?","¿Qué son Pulse, Bank y Civic?","À quoi servent Pulse, Bank et Civic ?","Was sind Pulse, Bank und Civic?","Pulse, Bank, Civic는 무엇인가요?","Что такое Pulse, Bank и Civic?"),
            Tr(l,"Pulse develops owned cities; Bank manages loans and mortgages; Civic projects offer separate investments through travel or events.","Pulse şehir geliştirmelerini, Bank kredi ve ipotekleri yönetir. Civic projeleri seyahat ve etkinliklerle ulaşılabilen ayrı yatırımlardır.","Pulse mejora ciudades; Bank gestiona préstamos; Civic ofrece inversiones por viajes y eventos.","Pulse développe les villes, Bank gère les prêts et Civic les projets spéciaux.","Pulse entwickelt Städte, Bank verwaltet Kredite und Civic bietet Sonderprojekte.","Pulse는 도시 개발, Bank는 대출, Civic은 별도 투자를 담당합니다.","Pulse развивает города, Bank управляет кредитами, Civic предлагает отдельные проекты."));
        Entry(Tr(l,"Can I play without creating an account?","Hesap oluşturmadan oynayabilir miyim?","¿Puedo jugar sin registrarme?","Puis-je jouer sans compte ?","Kann ich ohne Konto spielen?","계정 없이 플레이할 수 있나요?","Можно играть без регистрации?"),
            Tr(l,"You can start as a guest. To keep online rewards across devices, register or sign in to your account.","Misafir olarak başlayabilirsin. Çevrim içi ödüllerini cihazlar arasında korumak için hesabına kayıt ol veya giriş yap.","Puedes empezar como invitado; inicia sesión para conservar el progreso.","Vous pouvez commencer en invité ; connectez-vous pour garder votre progression.","Du kannst als Gast starten; melde dich an, um Fortschritte zu sichern.","게스트로 시작할 수 있습니다. 진행 상황을 유지하려면 로그인하세요.","Можно начать как гость; для сохранения прогресса войдите в аккаунт."));
        Entry(Tr(l,"What happens when I switch accounts?","Başka hesaba geçersem ne olur?","¿Qué pasa al cambiar de cuenta?","Et si je change de compte ?","Was passiert beim Kontowechsel?","계정을 바꾸면 어떻게 되나요?","Что будет при смене аккаунта?"),
            Tr(l,"Your Gold, Atlas Coin, inventory and Career belong to the account that earned them. Sign back in to see that account's data.","Gold, Atlas Coin, envanter ve Career bunları kazanan hesaba aittir. O hesabın bilgilerini görmek için tekrar giriş yap.","Cada cuenta mantiene su propio dinero, inventario y trayectoria.","Chaque compte conserve ses propres récompenses et sa carrière.","Jedes Konto hat sein eigenes Guthaben, Inventar und Karriere.","각 계정의 재화, 인벤토리 및 기록은 별도로 관리됩니다.","У каждого аккаунта собственные награды, инвентарь и карьера."));
        Entry(Tr(l,"How do I host a private game?","Özel oda nasıl oluşturulur?","¿Cómo creo una sala privada?","Comment créer une salle privée ?","Wie erstelle ich einen privaten Raum?","비공개 방은 어떻게 만드나요?","Как создать закрытую комнату?"),
            Tr(l,"Choose Private Table, select the game settings and share your room code with friends. Do not share your account password.","Private Table bölümünden maç ayarlarını seç ve oda kodunu arkadaşlarınla paylaş. Hesap şifreni paylaşma.","Crea una sala en Private Table y comparte solo el código.","Créez un salon Private Table et partagez son code.","Erstelle einen Private Table und teile nur den Raumcode.","Private Table에서 방을 만든 뒤 방 코드만 공유하세요.","Создайте Private Table и отправьте друзьям код комнаты."));
        Entry(Tr(l,"How do I change my dice or board?","Zarımı veya oyun tahtamı nasıl değiştiririm?","¿Cómo cambio mis dados o tablero?","Comment changer de dés ou de plateau ?","Wie ändere ich Würfel oder Spielbrett?","주사위와 보드를 어떻게 바꾸나요?","Как сменить кости или поле?"),
            Tr(l,"Open Shop or Career Inventory to inspect owned cosmetics and equip available items. Paid items remain tied to your account.","Sahip olduğun kozmetikleri Shop veya Career Envanter bölümünden görüp kuşanabilirsin. Satın alınan eşyalar hesabına bağlıdır.","Equipa objetos desde Shop o Career Inventory.","Équipez vos objets depuis Shop ou Career Inventory.","Rüste Gegenstände über Shop oder Career Inventory aus.","상점이나 Career 인벤토리에서 장착할 수 있습니다.","Снаряжение доступно в Shop и Career Inventory."));
        Entry(Tr(l,"Why is an online reward missing?","Çevrim içi ödülüm neden görünmüyor?","¿Por qué falta mi recompensa?","Pourquoi une récompense manque-t-elle ?","Warum fehlt eine Belohnung?","온라인 보상이 보이지 않아요.","Почему награда не появилась?"),
            Tr(l,"Wait for the completed match to synchronize, check your connection, and reopen Career. Use the same account; rewards are never duplicated.","Tamamlanan maçın eşitlenmesini bekle, bağlantını kontrol et ve Career bölümünü yeniden aç. Aynı hesapla giriş yap; ödüller ikinci kez verilmez.","Espera a la sincronización y revisa Career con la misma cuenta.","Attendez la synchronisation et vérifiez Career avec le même compte.","Warte auf die Synchronisierung und öffne Career erneut.","동기화를 기다린 뒤 같은 계정으로 Career를 확인하세요.","Дождитесь синхронизации и проверьте Career с тем же аккаунтом."));
        Entry(Tr(l,"Can I leave and return to a match?","Maçtan çıkıp geri dönebilir miyim?","¿Puedo volver a una partida?","Puis-je revenir dans une partie ?","Kann ich einem Spiel wieder beitreten?","나갔다가 다시 참여할 수 있나요?","Можно вернуться в матч?"),
            Tr(l,"A reconnect may restore your seat while the room is still available. Use the same identity and room code; room closure ends the session.","Oda açıksa aynı hesap ve oda koduyla yeniden bağlanarak koltuğunu geri alabilirsin. Oda kapandıysa oturum sona erer.","Vuelve con la misma cuenta y código mientras la sala siga abierta.","Revenez avec le même compte tant que le salon est ouvert.","Kehre mit demselben Konto zurück, solange der Raum offen ist.","방이 열려 있으면 동일 계정으로 재접속할 수 있습니다.","Пока комната открыта, вернитесь с той же учётной записью."));
        Entry(Tr(l,"Where can I report a problem?","Bir sorunu nereye bildirebilirim?","¿Dónde informo un problema?","Où signaler un problème ?","Wo melde ich ein Problem?","문제를 어디에 신고하나요?","Куда сообщить об ошибке?"),
            Tr(l,"Contact AtlasBoard support through the official store or support channel. Include the ATB error code and a short description, without private credentials.","Resmî mağaza veya destek kanalından AtlasBoard desteğine ulaş. ATB hata kodunu ve kısa açıklamayı paylaş; özel giriş bilgilerini ekleme.","Contacta soporte desde el canal oficial e incluye el código ATB.","Contactez l'assistance officielle et indiquez le code ATB.","Melde dich beim offiziellen Support und nenne den ATB-Code.","공식 지원 채널에 ATB 오류 코드를 알려 주세요.","Обратитесь в официальную поддержку и укажите код ATB."));
        Note(Tr(l,"Support: quote the ATB error code shown on screen; never share passwords or authentication tokens.","Destek ekibine ATB hata kodunu ve sorunun kısa açıklamasını ilet. Şifreni, giriş kodlarını veya hesap erişimini hiç kimseyle paylaşma; AtlasBoard desteği bunları istemez.","Para soporte, indica el código ATB; no compartas contraseñas.","Pour l'assistance, indiquez le code ATB, jamais vos identifiants.","Für Support den ATB-Code angeben, keine Passwörter teilen.","지원 시 ATB 오류 코드를 전달하고 비밀번호는 공유하지 마세요.","Для поддержки сообщите код ATB, но не пароли."));
    }


    private void DrawCredits()
    {
        string l = Language;
        Entry(Tr(l,"Atlas Board","Atlas Board","Atlas Board","Atlas Board","Atlas Board","Atlas Board","Atlas Board"),
            Tr(l,"Game development and original integration: AtlasBoard project. Third-party assets retain their creators' rights and licenses.","Oyun geliştirme ve özgün entegrasyon: AtlasBoard projesi. Üçüncü taraf varlıkların hakları ve lisansları sahiplerine aittir.","Desarrollo: proyecto AtlasBoard. Los derechos de terceros siguen vigentes.","Développement : projet AtlasBoard. Droits et licences des auteurs respectés.","Entwicklung: AtlasBoard-Projekt. Rechte Dritter bleiben bei ihren Urhebern.","게임 개발: AtlasBoard 프로젝트. 제3자 에셋 권리는 원저작자에게 있습니다.","Разработка: проект AtlasBoard. Права сторонних авторов сохраняются."));
        Entry("KENNEY — CC0", "Emotes Pack, UI Pack, City Kit Commercial, Nature Kit, Furniture Kit, Mini Characters, Mini Dungeon, Watercraft Kit, interface/audio packs. kenney.nl");
        Entry("POLY HAVEN — CC0", "Suburban Garden, Secluded Beach, Boma, Palermo Sidewalk environment assets. polyhaven.com");
        Entry("GOOGLE / NOTO — SIL OPEN FONT LICENSE 1.1", "Noto Sans and Noto Sans KR; multilingual UI font support. fonts.google.com/noto");
        Entry("UNITY ASSET STORE — LICENSED ASSETS", "Ferocious Industries — PBR Little Games Pack dice; SR Studios Kerala — Little Ghost; OlyPoly — Cartoon Cars; Low Poly Simple Urban City. Each is subject to its applicable Unity Asset Store EULA, not CC0.");
        Entry("OPEN SOURCE / ENGINE", "Unity Engine, TextMesh Pro, Firebase and other dependencies retain their respective licenses and trademarks.");
        Entry(Tr(l,"THIRD-PARTY COPYRIGHT","ÜÇÜNCÜ TARAF HAKLARI","DERECHOS DE TERCEROS","DROITS DES TIERS","RECHTE DRITTER","제3자 저작권","ПРАВА ТРЕТЬИХ ЛИЦ"),
            Tr(l,"Art, audio, fonts and licensed packages remain the intellectual property of their respective creators. Thank you to all contributors.","Görseller, sesler, yazı tipleri ve lisanslı paketlerin hakları ilgili üreticilerine aittir. Emeği geçen herkese teşekkür ederiz.","Los derechos pertenecen a sus respectivos creadores.","Les droits appartiennent à leurs créateurs respectifs.","Die Rechte verbleiben bei den jeweiligen Urhebern.","해당 저작물의 권리는 각 제작자에게 있습니다.","Права принадлежат соответствующим авторам."));
    }
}