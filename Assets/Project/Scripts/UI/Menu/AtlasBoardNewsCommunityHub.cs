using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// News and community polls share one Main Menu modal. The existing Help/Credits
/// controller and its internal Credits tab remain the only Help authority.
/// News is bundled for offline reading; votes always require verified RTDB data.
/// </summary>
[DefaultExecutionOrder(32002)]
public sealed class AtlasBoardNewsCommunityHub : MonoBehaviour
{
    private const string Database =
        "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    private static AtlasBoardNewsCommunityHub instance;
    private static Sprite roundedSprite;
    private static readonly Color Ink = ColorOf("#F0F6FF");
    private static readonly Color Secondary = ColorOf("#A9BED5");
    private static readonly Color Midnight = ColorOf("#102239");
    private static readonly Color Panel = ColorOf("#162E49");
    private static readonly Color Card = ColorOf("#213C5A");
    private static readonly Color Gold = ColorOf("#F7B928");
    private static readonly Color Accent = ColorOf("#177EAA");
    // One poll, exactly seven *answer choices*. These are not category sub-polls.
    private const string CurrentPollId = "community_feature_priority_2026w46";
    private static readonly string[] Options =
        { "maps", "pawns", "avatars", "dice", "boards", "profile_frames", "emotes" };

    [Serializable]
    private sealed class NewsFeed { public NewsPost[] posts; }
    [Serializable]
    private sealed class NewsPost
    {
        public string id, date, titleTr, titleEn, bodyTr, bodyEn;
    }
    private sealed class Poll
    {
        public string id, questionTr, questionEn;
        public long startsAtMs, endsAtMs;
        public bool active;
        public readonly List<PollOption> options = new List<PollOption>();
    }
    private sealed class PollOption
    {
        public string id, labelTr, labelEn;
    }

    private AtlasBoardMainMenuController menu;
    private RectTransform menuRoot;
    private RectTransform overlay;
    private RectTransform scrollContent;
    private ScrollRect scroll;
    private TMP_Text newsEntryLabel;
    private TMP_Text helpEntryLabel;
    private TMP_Text titleLabel;
    private TMP_Text subtitleLabel;
    private TMP_Text newsTabLabel;
    private TMP_Text votingTabLabel;
    private TMP_Text closeLabel;
    private TMP_FontAsset menuFont;
    private Image newsTabBackground;
    private Image votingTabBackground;
    private Button footerNews;
    private Button footerHelp;
    private Button.ButtonClickedEvent savedOriginalHelpAction;
    private string currentLanguage = "";
    private bool showingNews = true;
    private int requestGeneration;
    private string pollsUid = "";
    private Poll livePoll;
    private string chosenOptionId = "";
    private string recordedOptionId = "";
    private string recordedAlias = "";
    private long? verifiedVoteTotal;
    private readonly Dictionary<string, long> optionVoteCounts =
        new Dictionary<string, long>(StringComparer.Ordinal);
    private TMP_Text totalVoteLabel;
    private float nextTotalRefreshAt;
    private bool totalRefreshInFlight;
    private string remoteState = "";
    private bool voteBusy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject go = new GameObject("AtlasBoard_News_Community_Hub");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AtlasBoardNewsCommunityHub>();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private string Language
    {
        get
        {
            AtlasBoardLocalizationManager language = AtlasBoardLocalizationManager.Instance;
            return language == null ? "en" :
                (language.CurrentLanguageCode ?? "en").ToLowerInvariant();
        }
    }

    private string L(string en, string tr, string es, string fr, string de,
        string ko, string ru)
    {
        switch (Language)
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
    private string NewsLabel => L("NEWS", "HABERLER", "NOTICIAS", "ACTUALITÉS",
        "NEUIGKEITEN", "소식", "НОВОСТИ");
    private string HelpLabel => L("HELP / FAQ", "YARDIM / SSS", "AYUDA / FAQ", "AIDE / FAQ",
        "HILFE / FAQ", "도움말 / FAQ", "ПОМОЩЬ / FAQ");
    private string VoteLabel => L("COMMUNITY VOTING", "TOPLULUK OYLAMASI", "VOTACIÓN",
        "VOTE COMMUNAUTAIRE", "COMMUNITY-ABSTIMMUNG", "커뮤니티 투표", "ГОЛОСОВАНИЕ");
    private string CloseLabel => L("CLOSE", "KAPAT", "CERRAR", "FERMER",
        "SCHLIESSEN", "닫기", "ЗАКРЫТЬ");

    private static Color ColorOf(string color)
    {
        ColorUtility.TryParseHtmlString(color, out Color result);
        return result;
    }

    private static RectTransform Rect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Sprite Rounded()
    {
        if (roundedSprite != null) return roundedSprite;
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            float dx = Mathf.Max(Mathf.Abs(x + 0.5f - 32) - 18f, 0f);
            float dy = Mathf.Max(Mathf.Abs(y + 0.5f - 32) - 18f, 0f);
            float alpha = Mathf.Clamp01(14f - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply(false, true);
        roundedSprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, 64, 64),
            new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
            new Vector4(17, 17, 17, 17));
        return roundedSprite;
    }

    private Image Background(RectTransform rect, Color color, bool rounded = true)
    {
        Image img = rect.gameObject.AddComponent<Image>();
        img.color = color;
        if (rounded) { img.sprite = Rounded(); img.type = Image.Type.Sliced; }
        return img;
    }

    private TextMeshProUGUI Text(RectTransform parent, string name, string value,
        int size, Color color, FontStyles style = FontStyles.Normal,
        TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        RectTransform rect = Rect(parent, name);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = menuFont != null ? menuFont : TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.text = value;
        text.alignment = align;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private TMP_Text ButtonText(RectTransform parent, string name,
        Vector2 pos, Vector2 size, Color color, string caption,
        Action action, out Button button, int sizePt = 18)
    {
        RectTransform rect = Rect(parent, name);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 0);
        rect.pivot = new Vector2(0, 0);
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        Image img = Background(rect, color);
        button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(() => action());
        TextMeshProUGUI label = Text(rect, "Caption", caption, sizePt, Ink,
            FontStyles.Bold, TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(8, 4);
        label.rectTransform.offsetMax = new Vector2(-8, -4);
        label.enableAutoSizing = true;
        label.fontSizeMin = 12;
        label.fontSizeMax = sizePt;
        return label;
    }

    private void LateUpdate()
    {
        if (menu == null || menuRoot == null || !menuRoot)
        {
            menu = FindAnyObjectByType<AtlasBoardMainMenuController>();
            menuRoot = null;
            footerNews = null;
            footerHelp = null;
            overlay = null;
            if (menu == null) return;
            foreach (RectTransform item in menu.GetComponentsInChildren<RectTransform>(true))
                if (item.name == "MainMenu") { menuRoot = item; break; }
            if (menuRoot == null) return;
        }
        if (footerNews == null || footerHelp == null) BindExistingFooter();
        if (overlay == null && footerNews != null && footerHelp != null) BuildUi();
        if (overlay != null && !menuRoot.gameObject.activeInHierarchy) Close();
        if (pollsUid != AtlasBoardRtdbMetaEconomy.CurrentUid)
        {
            pollsUid = AtlasBoardRtdbMetaEconomy.CurrentUid;
            recordedOptionId = "";
            recordedAlias = "";
            chosenOptionId = "";
            livePoll = null;
            verifiedVoteTotal = null;
            optionVoteCounts.Clear();
            totalVoteLabel = null;
            nextTotalRefreshAt = 0f;
            requestGeneration++;
            if (overlay != null && overlay.gameObject.activeSelf && !showingNews)
            {
                RenderContent();
                StartCoroutine(LoadPolls(requestGeneration));
            }
        }
        if (overlay != null && overlay.gameObject.activeInHierarchy && !showingNews &&
            !totalRefreshInFlight && Time.unscaledTime >= nextTotalRefreshAt &&
            !string.IsNullOrWhiteSpace(pollsUid))
        {
            nextTotalRefreshAt = Time.unscaledTime + 15f;
            StartCoroutine(RefreshTotal(requestGeneration, pollsUid));
        }
        if (currentLanguage != Language)
        {
            currentLanguage = Language;
            RefreshLabels();
        }
    }

    private void BindExistingFooter()
    {
        Transform news = menuRoot.Find("AtlasBoard_Help_Entry");
        Transform help = menuRoot.Find("AtlasBoard_Credits_Entry");
        if (news == null || help == null) return;
        Button n = news.GetComponent<Button>();
        Button h = help.GetComponent<Button>();
        if (n == null || h == null) return;
        if (footerNews == n && footerHelp == h) return;
        savedOriginalHelpAction = n.onClick;
        n.onClick = new Button.ButtonClickedEvent();
        n.onClick.AddListener(OpenNews);
        h.onClick = new Button.ButtonClickedEvent();
        h.onClick.AddListener(() =>
        {
            Close();
            if (savedOriginalHelpAction != null) savedOriginalHelpAction.Invoke();
        });
        footerNews = n;
        footerHelp = h;
        newsEntryLabel = n.GetComponentInChildren<TMP_Text>(true);
        helpEntryLabel = h.GetComponentInChildren<TMP_Text>(true);
        RefreshLabels();
    }

    private void BuildUi()
    {
        foreach (TMP_Text t in menuRoot.GetComponentsInChildren<TMP_Text>(true))
            if (t != null && t.font != null) { menuFont = t.font; break; }
        overlay = Rect(menuRoot, "AtlasBoard_NewsCommunity_Modal");
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = overlay.offsetMax = Vector2.zero;
        Image dim = Background(overlay, new Color(0.02f, 0.055f, 0.11f, 0.83f), false);
        Button back = overlay.gameObject.AddComponent<Button>();
        back.targetGraphic = dim;
        back.onClick.AddListener(Close);
        overlay.gameObject.AddComponent<AtlasBoardEscapeBlocker>();

        RectTransform panel = Rect(overlay, "NewsCommunityDialog");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(920, 728);
        Image panelImage = Background(panel, Midnight);
        // Stop pointer clicks on empty dialog space from reaching the dimmed
        // backdrop's Close button. Child tabs/options remain independently usable.
        Button panelClickBlocker = panel.gameObject.AddComponent<Button>();
        panelClickBlocker.targetGraphic = panelImage;
        panelClickBlocker.transition = Selectable.Transition.None;
        Shadow shadow = panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.48f);
        shadow.effectDistance = new Vector2(0, -11);
        Outline border = panel.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(0.35f, 0.65f, 0.85f, 0.35f);
        border.effectDistance = new Vector2(2, -2);
        RectTransform accent = Rect(panel, "AccentBar");
        accent.anchorMin = new Vector2(0, 1);
        accent.anchorMax = new Vector2(1, 1);
        accent.pivot = new Vector2(0.5f, 1);
        accent.sizeDelta = new Vector2(0, 7);
        Background(accent, Gold, false).raycastTarget = false;

        RectTransform titleBar = Rect(panel, "TitleArea");
        titleBar.anchorMin = new Vector2(0, 1);
        titleBar.anchorMax = new Vector2(1, 1);
        titleBar.pivot = new Vector2(0.5f, 1);
        titleBar.anchoredPosition = new Vector2(0, -29);
        titleBar.sizeDelta = new Vector2(-60, 82);
        titleLabel = Text(titleBar, "Title", NewsLabel, 36, Ink, FontStyles.Bold);
        titleLabel.rectTransform.anchorMin = new Vector2(0, 0.46f);
        titleLabel.rectTransform.anchorMax = new Vector2(0.84f, 1);
        titleLabel.rectTransform.offsetMin = titleLabel.rectTransform.offsetMax = Vector2.zero;
        subtitleLabel = Text(titleBar, "Subtitle", "", 16, Secondary);
        subtitleLabel.rectTransform.anchorMin = new Vector2(0, 0);
        subtitleLabel.rectTransform.anchorMax = new Vector2(1, 0.43f);
        subtitleLabel.rectTransform.offsetMin = subtitleLabel.rectTransform.offsetMax = Vector2.zero;
        Button x;
        ButtonText(titleBar, "CloseX", new Vector2(776, 35), new Vector2(62, 44),
            Card, "X", Close, out x, 23);

        RectTransform tabs = Rect(panel, "NewsTabs");
        tabs.anchorMin = new Vector2(0, 1);
        tabs.anchorMax = new Vector2(1, 1);
        tabs.pivot = new Vector2(0.5f, 1);
        tabs.anchoredPosition = new Vector2(0, -130);
        tabs.sizeDelta = new Vector2(-60, 50);
        Button nb, vb;
        newsTabLabel = ButtonText(tabs, "NewsTab", Vector2.zero,
            new Vector2(250, 48), Card, NewsLabel, () => SelectTab(true), out nb, 18);
        votingTabLabel = ButtonText(tabs, "VotingTab", new Vector2(262, 0),
            new Vector2(310, 48), Card, VoteLabel, () => SelectTab(false), out vb, 17);
        newsTabBackground = nb.GetComponent<Image>();
        votingTabBackground = vb.GetComponent<Image>();

        RectTransform viewport = Rect(panel, "ScrollViewport");
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(30, 90);
        viewport.offsetMax = new Vector2(-30, -194);
        Background(viewport, Panel);
        viewport.gameObject.AddComponent<RectMask2D>();
        scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 22;
        scrollContent = Rect(viewport, "NewsScrollContent");
        scrollContent.anchorMin = new Vector2(0, 1);
        scrollContent.anchorMax = new Vector2(1, 1);
        scrollContent.pivot = new Vector2(0.5f, 1);
        scrollContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = scrollContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 12;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        scrollContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = scrollContent;

        RectTransform footer = Rect(panel, "Footer");
        footer.anchorMin = new Vector2(0, 0);
        footer.anchorMax = new Vector2(1, 0);
        footer.pivot = new Vector2(0.5f, 0);
        footer.anchoredPosition = new Vector2(0, 12);
        footer.sizeDelta = new Vector2(-60, 72);
        TMP_Text note = Text(footer, "Note", "ATLAS BOARD  /  COMMUNITY", 14, Secondary);
        note.rectTransform.anchorMin = new Vector2(0, 0);
        note.rectTransform.anchorMax = new Vector2(0.7f, 1);
        note.rectTransform.offsetMin = note.rectTransform.offsetMax = Vector2.zero;
        Button close;
        closeLabel = ButtonText(footer, "Close", new Vector2(660, 14),
            new Vector2(195, 44), Accent, CloseLabel, Close, out close, 18);
        overlay.gameObject.SetActive(false);
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (newsEntryLabel != null) newsEntryLabel.text = "NEWS";
        if (helpEntryLabel != null) helpEntryLabel.text = HelpLabel;
        if (newsTabLabel != null) newsTabLabel.text = NewsLabel;
        if (votingTabLabel != null) votingTabLabel.text = VoteLabel;
        if (closeLabel != null) closeLabel.text = CloseLabel;
        if (overlay != null && overlay.gameObject.activeSelf) RenderContent();
    }

    private void OpenNews()
    {
        if (overlay == null) return;
        showingNews = true;
        overlay.gameObject.SetActive(true);
        overlay.SetAsLastSibling();
        RenderContent();
    }
    private void SelectTab(bool news)
    {
        if (showingNews == news) return;
        showingNews = news;
        RenderContent();
        if (!news)
        {
            requestGeneration++;
            verifiedVoteTotal = null;
            optionVoteCounts.Clear();
            nextTotalRefreshAt = Time.unscaledTime + 15f;
            StartCoroutine(LoadPolls(requestGeneration));
        }
    }
    private void Close()
    {
        requestGeneration++;
        if (overlay != null) overlay.gameObject.SetActive(false);
    }
    private void Update()
    {
        if (overlay == null || !overlay.gameObject.activeInHierarchy) return;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) Close();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
#endif
    }

    private void RenderContent()
    {
        if (scrollContent == null || overlay == null || !overlay.gameObject.activeSelf) return;
        titleLabel.text = showingNews ? NewsLabel : VoteLabel;
        subtitleLabel.text = showingNews
            ? L("Development updates from Atlas Board", "Atlas Board geliştirme günlüğü",
                "Novedades de desarrollo", "Journal de développement", "Entwicklungsnotizen",
                "개발 소식", "Журнал разработки")
            : L("One vote. Choose what we should add next.", "Tek oy hakkın var. Sırada ne geliştirelim?",
                "Un voto para la próxima mejora.", "Un vote pour la prochaine nouveauté.",
                "Eine Stimme für die nächste Neuerung.", "다음 업데이트를 하나 선택하세요.",
                "Один голос за следующее улучшение.");
        newsTabBackground.color = showingNews ? Accent : Card;
        votingTabBackground.color = showingNews ? Card : Accent;
        for (int i = scrollContent.childCount - 1; i >= 0; i--)
        {
            Transform child = scrollContent.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        if (showingNews) DrawNews(); else DrawPolls();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
        Canvas.ForceUpdateCanvases();
        if (scroll != null) scroll.verticalNormalizedPosition = 1;
    }

    private RectTransform CardRect(string name)
    {
        RectTransform card = Rect(scrollContent, name);
        Background(card, Card);
        VerticalLayoutGroup group = card.gameObject.AddComponent<VerticalLayoutGroup>();
        group.padding = new RectOffset(19, 19, 14, 16);
        group.spacing = 7;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
        return card;
    }

    private TMP_Text Paragraph(RectTransform parent, string name, string value,
        int size, Color color, bool bold = false)
    {
        TextMeshProUGUI t = Text(parent, name, value, size, color,
            bold ? FontStyles.Bold : FontStyles.Normal);
        t.margin = Vector4.zero;
        t.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
        return t;
    }

    private void DrawNews()
    {
        TextAsset source = Resources.Load<TextAsset>("AtlasBoardNewsPosts");
        NewsFeed feed = source != null ? JsonUtility.FromJson<NewsFeed>(source.text) : null;
        if (feed == null || feed.posts == null)
        {
            RectTransform card = CardRect("NewsMissing");
            Paragraph(card, "Status", L("News could not be loaded.", "Haberler yüklenemedi.",
                "No se cargaron noticias.", "Actualités indisponibles.", "News nicht verfügbar.",
                "소식을 불러올 수 없습니다.", "Новости недоступны."), 19, Secondary);
            return;
        }
        List<NewsPost> fresh = new List<NewsPost>();
        DateTime today = DateTime.UtcNow.Date;
        DateTime cutoff = today.AddMonths(-20);
        foreach (NewsPost post in feed.posts)
        {
            if (post == null || string.IsNullOrWhiteSpace(post.date)) continue;
            if (!DateTime.TryParseExact(post.date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime published)) continue;
            if (published.Date < cutoff || published.Date > today) continue;
            fresh.Add(post);
        }
        fresh.Sort((a, b) => string.CompareOrdinal(b.date, a.date));
        int count = Mathf.Min(20, fresh.Count);
        for (int i = 0; i < count; i++)
        {
            NewsPost post = fresh[i];
            RectTransform card = CardRect("News_" + post.id);
            Paragraph(card, "Date", post.date, 16, Gold, true);
            Paragraph(card, "Heading", Language == "tr" ? post.titleTr : post.titleEn,
                22, Ink, true);
            Paragraph(card, "Content", Language == "tr" ? post.bodyTr : post.bodyEn,
                18, Ink);
        }
        if (count == 0)
        {
            RectTransform card = CardRect("NewsEmpty");
            Paragraph(card, "Status", L("No recent news.", "Güncel haber yok.",
                "Sin noticias recientes.", "Pas d'actualités récentes.", "Keine aktuellen News.",
                "최근 소식이 없습니다.", "Свежих новостей нет."), 19, Secondary);
        }
    }

    private string CategoryName(string key)
    {
        switch (key)
        {
            case "maps": return L("MAPS", "HARİTALAR", "MAPAS", "CARTES", "KARTEN", "지도", "КАРТЫ");
            case "pawns": return L("PAWNS", "PİYONLAR", "FICHAS", "PIONS", "SPIELFIGUREN", "말", "ФИШКИ");
            case "avatars": return L("AVATARS", "PROFİL FOTOĞRAFI", "AVATARES", "AVATARS", "AVATARE", "아바타", "АВАТАРЫ");
            case "dice": return L("DICE", "ZARLAR", "DADOS", "DÉS", "WÜRFEL", "주사위", "КУБИКИ");
            case "boards": return L("BOARDS", "TAHTALAR", "TABLEROS", "PLATEAUX", "SPIELBRETTER", "보드", "ПОЛЯ");
            case "profile_frames": return L("PROFILE FRAMES", "PROFİL ÇERÇEVELERİ", "MARCOS",
                "CADRES", "PROFILRAHMEN", "프로필 테두리", "РАМКИ ПРОФИЛЯ");
            default: return L("EMOTES", "EMOTE", "GESTOS", "ÉMOTES", "EMOTES", "이모트", "ЭМОЦИИ");
        }
    }

    private void DrawPolls()
    {
        // The user chooses ONE of these seven options in ONE poll. Never draw
        // per-category child polls or unverified nested options.
        totalVoteLabel = null;
        RectTransform card = CardRect("CommunityPriorityPoll");
        string question = livePoll != null
            ? (Language == "tr" ? livePoll.questionTr : livePoll.questionEn)
            : L("Which feature should we develop next?", "Sırada hangi özelliği geliştirelim?",
                "¿Qué función añadimos después?", "Quelle fonction ensuite ?",
                "Welche Funktion als Nächstes?", "다음에 어떤 기능을 추가할까요?",
                "Что нам разработать следующим?");
        Paragraph(card, "Question", question, 22, Gold, true);
        bool registered = AtlasBoardRtdbMetaEconomy.IsRegistered &&
            !string.IsNullOrWhiteSpace(AtlasBoardRtdbMetaEconomy.CurrentUid);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bool active = livePoll != null && livePoll.active &&
            now >= livePoll.startsAtMs && now < livePoll.endsAtMs;
        bool voted = !string.IsNullOrEmpty(recordedOptionId);
        bool canSelect = registered && active && !voted && !voteBusy;
        foreach (string optionId in Options)
        {
            string captured = optionId;
            bool isSelected = voted
                ? string.Equals(recordedOptionId, captured, StringComparison.Ordinal)
                : string.Equals(chosenOptionId, captured, StringComparison.Ordinal);
            RectTransform row = Rect(card, "Choice_" + captured);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
            Image background = Background(row, isSelected ? Accent : Panel);
            Button choice = row.gameObject.AddComponent<Button>();
            choice.targetGraphic = background;
            choice.interactable = canSelect;
            choice.onClick.AddListener(() =>
            {
                chosenOptionId = captured;
                // Selection is local. The Firebase write happens ONLY on OY VER.
                RenderContent();
            });
            string countSuffix = (voted || !active) && optionVoteCounts.TryGetValue(captured,
                out long counted) ? "     " + counted.ToString(CultureInfo.InvariantCulture) : "";
            TMP_Text caption = Text(row, "ChoiceLabel",
                (isSelected ? "[ X ]   " : "[   ]   ") + CategoryName(captured) + countSuffix,
                17, Ink, isSelected ? FontStyles.Bold : FontStyles.Normal);
            caption.rectTransform.anchorMin = Vector2.zero;
            caption.rectTransform.anchorMax = Vector2.one;
            caption.rectTransform.offsetMin = new Vector2(15, 5);
            caption.rectTransform.offsetMax = new Vector2(-15, -5);
        }
        if (canSelect)
        {
            // Explicit vote confirmation prevents a mistaken tap from
            // consuming a lifetime/one-poll vote.
            RectTransform voteRow = Rect(card, "SubmitChoice");
            voteRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 52;
            Image background = Background(voteRow, Accent);
            Button vote = voteRow.gameObject.AddComponent<Button>();
            vote.targetGraphic = background;
            vote.interactable = !string.IsNullOrEmpty(chosenOptionId);
            vote.onClick.AddListener(() =>
            {
                if (voteBusy || string.IsNullOrEmpty(chosenOptionId)) return;
                StartCoroutine(SubmitVote(CurrentPollId, chosenOptionId, requestGeneration));
            });
            TMP_Text label = Text(voteRow, "VoteCaption",
                L("SUBMIT VOTE", "OY VER", "VOTAR", "VOTER", "ABSTIMMEN",
                    "투표하기", "ПРОГОЛОСОВАТЬ"), 19, Ink,
                FontStyles.Bold, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8, 3);
            label.rectTransform.offsetMax = new Vector2(-8, -3);
        }
        totalVoteLabel = Paragraph(card, "LiveVoteTotal", TotalText(), 17, Secondary, true);
        if (voted)
            Paragraph(card, "Voted", L("YOUR VOTE WAS RECORDED", "OYUN KAYDEDİLDİ",
                "VOTO REGISTRADO", "VOTE ENREGISTRÉ", "STIMME GESPEICHERT",
                "투표 완료", "ГОЛОС УЧТЁН"), 16, Gold, true);
        else if (!registered)
            Paragraph(card, "SignIn", L("Sign in with a registered account to vote.",
                "Oy kullanmak için kayıtlı hesabınla giriş yap.", "Inicia sesión para votar.",
                "Connectez-vous pour voter.", "Melde dich zum Abstimmen an.",
                "로그인 후 투표하세요.", "Войдите для голосования."), 16, Secondary);
        else if (livePoll == null)
            Paragraph(card, "Pending", L("Poll is loading or not yet published.",
                "Oylama yükleniyor veya henüz yayınlanmadı.", "Sondeo aún no publicado.",
                "Sondage non publié.", "Umfrage noch nicht veröffentlicht.",
                "투표가 아직 게시되지 않았습니다.", "Опрос ещё не опубликован."), 16, Secondary);
        else if (!active)
            Paragraph(card, "Inactive", L("This vote is closed.", "Bu oylama kapalı.",
                "Votación cerrada.", "Vote clos.", "Abstimmung geschlossen.",
                "투표가 종료되었습니다.", "Голосование закрыто."), 16, Secondary);
        else if (voteBusy)
            Paragraph(card, "Busy", L("Saving your vote...", "Oyun kaydediliyor...",
                "Guardando voto...", "Enregistrement du vote...", "Stimme wird gespeichert...",
                "투표 저장 중...", "Сохраняем голос..."), 16, Secondary);
        if (!string.IsNullOrEmpty(remoteState))
            Paragraph(CardRect("VoteStatus"), "Message", remoteState, 17, Secondary);
    }

    private string TotalText()
    {
        string prefix = L("TOTAL VOTES: ", "TOPLAM OY: ", "VOTOS TOTALES: ",
            "TOTAL DES VOTES : ", "STIMMEN GESAMT: ", "총 투표 수: ", "ВСЕГО ГОЛОСОВ: ");
        return prefix + (verifiedVoteTotal.HasValue
            ? verifiedVoteTotal.Value.ToString(CultureInfo.InvariantCulture)
            : "—");
    }

    private static long ReadLong(JToken token, string field)
    {
        if (token == null) return 0;
        return long.TryParse((string)token[field], NumberStyles.Integer,
            CultureInfo.InvariantCulture, out long value) ? value : 0;
    }

    private bool IsRequestCurrent(int generation, string uid)
    {
        return generation == requestGeneration &&
            string.Equals(uid, AtlasBoardRtdbMetaEconomy.CurrentUid, StringComparison.Ordinal) &&
            overlay != null && overlay.gameObject.activeInHierarchy && !showingNews;
    }

    private IEnumerator LoadPolls(int generation)
    {
        if (showingNews || overlay == null || !overlay.gameObject.activeInHierarchy) yield break;
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid;
        if (string.IsNullOrWhiteSpace(uid))
        {
            remoteState = "";
            livePoll = null;
            verifiedVoteTotal = null;
            RenderContent();
            yield break;
        }
        Task<string> tokenTask = AtlasBoardRtdbMetaEconomy.GetCurrentCallableTokenAsync();
        while (!tokenTask.IsCompleted) yield return null;
        if (!IsRequestCurrent(generation, uid)) yield break;
        if (tokenTask.IsCanceled || tokenTask.IsFaulted || string.IsNullOrEmpty(tokenTask.Result))
        {
            remoteState = L("Could not validate your session.", "Oturumun doğrulanamadı.",
                "Sesión no verificada.", "Session non vérifiée.", "Sitzung nicht verifiziert.",
                "세션 확인 실패.", "Сеанс не подтверждён.");
            RenderContent();
            yield break;
        }
        string token = tokenTask.Result;
        string endpoint = Database + "/communityPolls/" + CurrentPollId +
            ".json?auth=" + UnityWebRequest.EscapeURL(token);
        using (UnityWebRequest req = UnityWebRequest.Get(endpoint))
        {
            req.timeout = 15;
            yield return req.SendWebRequest();
            if (!IsRequestCurrent(generation, uid)) yield break;
            if (req.result != UnityWebRequest.Result.Success)
            {
                remoteState = L("Voting server unavailable.", "Oylama sunucusuna ulaşılamadı.",
                    "Servidor no disponible.", "Serveur indisponible.", "Server nicht erreichbar.",
                    "서버 연결 실패.", "Сервер недоступен.");
                livePoll = null;
                RenderContent();
                yield break;
            }
            try
            {
                JToken value = JToken.Parse(req.downloadHandler.text);
                if (!(value is JObject)) throw new FormatException("No active poll manifest");
                JObject opts = value["options"] as JObject;
                if (opts == null || opts.Count != Options.Length)
                    throw new FormatException("Not a seven-choice poll");
                foreach (string option in Options)
                    if (!(opts[option] is JObject)) throw new FormatException("Unknown option manifest");
                livePoll = new Poll
                {
                    id = CurrentPollId,
                    questionTr = (string)value["questionTr"] ?? "",
                    questionEn = (string)value["questionEn"] ?? "",
                    startsAtMs = ReadLong(value, "startsAtMs"),
                    endsAtMs = ReadLong(value, "endsAtMs"),
                    active = (bool?)value["active"] == true
                };
                remoteState = "";
            }
            catch (Exception)
            {
                livePoll = null;
                remoteState = L("Poll configuration is not published.",
                    "Oylama yapılandırması henüz yayınlanmadı.",
                    "Encuesta no publicada.", "Sondage non publié.",
                    "Umfrage nicht veröffentlicht.", "설정이 게시되지 않았습니다.",
                    "Опрос ещё не опубликован.");
            }
        }
        if (livePoll == null)
        {
            RenderContent();
            yield break;
        }
        // Only this session's own receipt is read; all other voters stay private.
        if (AtlasBoardRtdbMetaEconomy.IsRegistered)
        {
            string receiptUrl = Database + "/communityVotes/" + CurrentPollId + "/" +
                UnityWebRequest.EscapeURL(uid) + ".json?auth=" +
                UnityWebRequest.EscapeURL(token);
            using (UnityWebRequest req = UnityWebRequest.Get(receiptUrl))
            {
                req.timeout = 12;
                yield return req.SendWebRequest();
                if (!IsRequestCurrent(generation, uid)) yield break;
                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        JToken receipt = JToken.Parse(req.downloadHandler.text);
                        string option = (string)receipt?["optionId"];
                        string alias = (string)receipt?["publicAlias"];
                        if (Array.IndexOf(Options, option) >= 0 && IsValidAlias(alias))
                        { recordedOptionId = option; recordedAlias = alias; }
                    }
                    catch (Exception) { }
                }
            }
        }
        if (IsRequestCurrent(generation, uid))
        {
            RenderContent();
            // Recover a saved vote whose counter update was interrupted.
            if (!string.IsNullOrEmpty(recordedOptionId))
                yield return PublishOwnAnonymousVote(generation, uid, token);
            yield return FetchTotal(generation, uid, token);
        }
    }

    // The public alias contains no UID. It is permanently bound to the
    // authenticated private receipt by RTDB Rules. There is NO shared
    // client-writable counter or cross-user marker to corrupt.
    private static bool IsValidAlias(string alias)
    {
        if (string.IsNullOrEmpty(alias) || alias.Length != 32) return false;
        foreach (char c in alias)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        return true;
    }

    private IEnumerator PublishOwnAnonymousVote(int generation, string uid, string token)
    {
        if (!IsRequestCurrent(generation, uid) ||
            !IsValidAlias(recordedAlias) ||
            Array.IndexOf(Options, recordedOptionId) < 0) yield break;
        string url = Database + "/communityPublicVotes/" + CurrentPollId + "/" +
            recordedAlias + ".json?auth=" + UnityWebRequest.EscapeURL(token);
        string payload = "{\"optionId\":\"" + recordedOptionId + "\"}";
        using (UnityWebRequest req = new UnityWebRequest(url, "PUT"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("If-Match", "null_etag");
            req.timeout = 12;
            yield return req.SendWebRequest();
            // Already published is safe. If there was a network interruption,
            // reopening the vote view can retry the same immutable alias.
        }
    }

    private IEnumerator FetchTotal(int generation, string uid, string token)
    {
        string url = Database + "/communityPublicVotes/" + CurrentPollId +
            ".json?auth=" + UnityWebRequest.EscapeURL(token);
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 15;
            yield return req.SendWebRequest();
            if (!IsRequestCurrent(generation, uid)) yield break;
            if (req.result != UnityWebRequest.Result.Success) yield break;
            try
            {
                JToken parsed = JToken.Parse(req.downloadHandler.text);
                if (parsed.Type != JTokenType.Null && !(parsed is JObject))
                    throw new FormatException("Unexpected public poll result");
                var counts = new Dictionary<string, long>(StringComparer.Ordinal);
                foreach (string option in Options) counts[option] = 0;
                long total = 0;
                if (parsed is JObject anonymousVotes)
                {
                    foreach (JProperty item in anonymousVotes.Properties())
                    {
                        if (!IsValidAlias(item.Name)) continue;
                        string option = (string)item.Value?["optionId"];
                        if (option == null || !counts.ContainsKey(option)) continue;
                        counts[option]++;
                        total++;
                    }
                }
                verifiedVoteTotal = total;
                bool changed = optionVoteCounts.Count != counts.Count;
                foreach (var pair in counts)
                    if (!optionVoteCounts.TryGetValue(pair.Key, out long old) ||
                        old != pair.Value) changed = true;
                if (changed)
                {
                    optionVoteCounts.Clear();
                    foreach (var pair in counts) optionVoteCounts[pair.Key] = pair.Value;
                    if (!string.IsNullOrEmpty(recordedOptionId) && !voteBusy)
                        RenderContent();
                }
                if (totalVoteLabel != null) totalVoteLabel.text = TotalText();
            }
            catch (Exception)
            {
                // Never paint an unverified/partial response as a real total.
            }
        }
    }

    private IEnumerator RefreshTotal(int generation, string uid)
    {
        if (totalRefreshInFlight || livePoll == null) yield break;
        totalRefreshInFlight = true;
        Task<string> task = AtlasBoardRtdbMetaEconomy.GetCurrentCallableTokenAsync();
        while (!task.IsCompleted) yield return null;
        if (IsRequestCurrent(generation, uid) && !task.IsFaulted &&
            !task.IsCanceled && !string.IsNullOrEmpty(task.Result))
            yield return FetchTotal(generation, uid, task.Result);
        totalRefreshInFlight = false;
    }

    private IEnumerator SubmitVote(string pollId, string optionId, int generation)
    {
        if (voteBusy || showingNews || livePoll == null ||
            !AtlasBoardRtdbMetaEconomy.IsRegistered ||
            !string.Equals(pollId, CurrentPollId, StringComparison.Ordinal) ||
            Array.IndexOf(Options, optionId) < 0 || !string.IsNullOrEmpty(recordedOptionId))
            yield break;
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid;
        if (string.IsNullOrWhiteSpace(uid)) yield break;
        voteBusy = true;
        RenderContent();
        Task<string> tokenTask = AtlasBoardRtdbMetaEconomy.GetCurrentCallableTokenAsync();
        while (!tokenTask.IsCompleted) yield return null;
        if (tokenTask.IsFaulted || tokenTask.IsCanceled || !IsRequestCurrent(generation, uid) ||
            string.IsNullOrEmpty(tokenTask.Result))
        {
            voteBusy = false;
            yield break;
        }
        string token = tokenTask.Result;
        string url = Database + "/communityVotes/" + pollId + "/" +
            UnityWebRequest.EscapeURL(uid) + ".json?auth=" +
            UnityWebRequest.EscapeURL(token);
        string newAlias = Guid.NewGuid().ToString("N");
        string payload = "{\"uid\":\"" + uid + "\",\"pollId\":\"" + pollId +
            "\",\"optionId\":\"" + optionId +
            "\",\"publicAlias\":\"" + newAlias +
            "\",\"createdAt\":{\".sv\":\"timestamp\"}}";
        bool committed = false;
        using (UnityWebRequest req = new UnityWebRequest(url, "PUT"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("If-Match", "null_etag");
            req.timeout = 15;
            yield return req.SendWebRequest();
            if (!IsRequestCurrent(generation, uid)) { voteBusy = false; yield break; }
            committed = req.result == UnityWebRequest.Result.Success;
        }
        if (!committed)
        {
            // A timeout can happen after the server committed. Never report a
            // second vote: read the exact UID receipt and recover its choice.
            using (UnityWebRequest verify = UnityWebRequest.Get(url))
            {
                verify.timeout = 12;
                yield return verify.SendWebRequest();
                if (!IsRequestCurrent(generation, uid)) { voteBusy = false; yield break; }
                if (verify.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        JToken receipt = JToken.Parse(verify.downloadHandler.text);
                        string stored = (string)receipt?["optionId"];
                        string alias = (string)receipt?["publicAlias"];
                        if (Array.IndexOf(Options, stored) >= 0 && IsValidAlias(alias))
                        { recordedOptionId = stored; recordedAlias = alias; committed = true; }
                    }
                    catch (Exception) { }
                }
            }
        }
        else { recordedOptionId = optionId; recordedAlias = newAlias; }
        if (committed)
        {
            remoteState = L("Your vote was saved.", "Oyun kaydedildi.", "Voto guardado.",
                "Vote enregistré.", "Stimme gespeichert.", "투표가 기록되었습니다.",
                "Ваш голос сохранён.");
            yield return PublishOwnAnonymousVote(generation, uid, token);
            yield return FetchTotal(generation, uid, token);
        }
        else
            remoteState = L("Vote could not be verified. Try again.",
                "Oy doğrulanamadı. Tekrar deneyebilirsin.", "Voto no verificado.",
                "Vote non vérifié.", "Stimme nicht bestätigt.", "투표 확인 실패.",
                "Голос не подтверждён.");
        voteBusy = false;
        if (IsRequestCurrent(generation, uid)) RenderContent();
    }
}
