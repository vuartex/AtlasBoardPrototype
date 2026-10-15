using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cross-platform modern presentation for the existing Travel decision.
/// TileResolutionManager remains the sole owner of travel state, pricing,
/// permissions and the original wired actions. This class only mirrors those
/// fields and delegates button clicks to the existing UnityEvents.
/// </summary>
[DefaultExecutionOrder(14500)]
[DisallowMultipleComponent]
public sealed class AtlasBoardTravelPresentationPolish : MonoBehaviour
{
    private const float RefreshInterval = 0.20f;
    private static readonly BindingFlags PrivateInstance =
        BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly FieldInfo PanelField =
        typeof(TileResolutionManager).GetField("travelPanel", PrivateInstance);
    private static readonly FieldInfo InfoField =
        typeof(TileResolutionManager).GetField("travelInfoText", PrivateInstance);
    private static readonly FieldInfo GoField =
        typeof(TileResolutionManager).GetField("travelGoButton", PrivateInstance);
    private static readonly FieldInfo StayField =
        typeof(TileResolutionManager).GetField("travelStayButton", PrivateInstance);

    private static readonly Color Back = new Color32(10, 20, 29, 255);
    private static readonly Color Card = new Color32(20, 35, 48, 255);
    private static readonly Color Outline = new Color32(44, 137, 184, 255);
    private static readonly Color Label = new Color32(190, 212, 224, 255);
    private static readonly Color Gold = new Color32(255, 187, 51, 255);
    private static readonly Color Go = new Color32(28, 168, 121, 255);
    private static readonly Color Stay = new Color32(92, 103, 121, 255);

    private TileResolutionManager resolution;
    private GameObject travelPanel;
    private TabletUIManager tabletManager;
    private RectTransform tabletRoot;
    private static readonly FieldInfo TabletRootField = typeof(TabletUIManager).GetField(
        "tabletRoot", BindingFlags.NonPublic | BindingFlags.Instance);
    private TMP_Text sourceInfo;
    private Button sourceGo;
    private Button sourceStay;

    private RectTransform modernRoot;
    private TMP_Text modernInfo;
    private TMP_Text goCaption;
    private TMP_Text stayCaption;
    private Button modernGo;
    private Button modernStay;
    private CanvasGroup oldInfoGroup;
    private CanvasGroup oldGoGroup;
    private CanvasGroup oldStayGroup;
    private float nextBindAt;
    private float nextRefreshAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (UnityEngine.Object.FindAnyObjectByType<BoardPath>(FindObjectsInactive.Include) == null ||
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardTravelPresentationPolish>(FindObjectsInactive.Include) != null)
        {
            return;
        }
        new GameObject("AtlasBoard_Phase17F_TravelPresentation")
            .AddComponent<AtlasBoardTravelPresentationPolish>();
    }

    private void OnEnable()
    {
        AtlasBoardLocalizationManager.LanguageChanged += HandleLanguageChanged;
        nextBindAt = 0f;
    }

    private void OnDisable()
    {
        AtlasBoardLocalizationManager.LanguageChanged -= HandleLanguageChanged;
        RestoreLegacyVisuals();
    }

    private void OnDestroy()
    {
        RestoreLegacyVisuals();
    }

    private void HandleLanguageChanged()
    {
        nextRefreshAt = 0f;
    }

    private void LateUpdate()
    {
        // Stop expensive scene searches after a successful bind.  Rebind only
        // when Unity destroys the cached objects during scene replacement.
        if (Time.unscaledTime >= nextBindAt &&
            (resolution == null || travelPanel == null || tabletRoot == null || sourceInfo == null ||
             sourceGo == null || sourceStay == null))
        {
            nextBindAt = Time.unscaledTime + 0.75f;
            ResolveReferences();
        }

        if (travelPanel == null || !travelPanel.activeInHierarchy)
        {
            return;
        }

        if (modernRoot == null)
        {
            BuildPresentation();
        }
        if (modernRoot == null)
        {
            return;
        }

        if (!modernRoot.gameObject.activeSelf)
            modernRoot.gameObject.SetActive(true);

        // Other managers can change ordering when they update the tablet.
        // No SetAsLastSibling or text/graphics mutation is needed per-frame
        // after this root has become the final sibling.
        if (modernRoot.GetSiblingIndex() != modernRoot.parent.childCount - 1)
            modernRoot.SetAsLastSibling();

        if (Time.unscaledTime >= nextRefreshAt)
        {
            nextRefreshAt = Time.unscaledTime + RefreshInterval;
            RefreshPresentation();
        }
    }

    private void ResolveReferences()
    {
        TileResolutionManager resolved = UnityEngine.Object.FindAnyObjectByType<TileResolutionManager>(
            FindObjectsInactive.Include);
        if (resolved == null)
            return;

        GameObject resolvedPanel = PanelField != null
            ? PanelField.GetValue(resolved) as GameObject
            : null;
        TabletUIManager manager = UnityEngine.Object.FindAnyObjectByType<TabletUIManager>(
            FindObjectsInactive.Include);
        RectTransform wideTablet = manager != null && TabletRootField != null
            ? (TabletRootField.GetValue(manager) as GameObject)?.GetComponent<RectTransform>()
            : null;
        if (resolved != resolution || resolvedPanel != travelPanel || wideTablet != tabletRoot)
        {
            RestoreLegacyVisuals();
            if (modernRoot != null && modernRoot.parent != wideTablet)
                Destroy(modernRoot.gameObject);
            resolution = resolved;
            travelPanel = resolvedPanel;
            tabletManager = manager;
            tabletRoot = wideTablet;
            modernRoot = null;
            modernInfo = null;
            modernGo = null;
            modernStay = null;
            oldInfoGroup = null;
            oldGoGroup = null;
            oldStayGroup = null;
        }

        sourceInfo = InfoField != null ? InfoField.GetValue(resolution) as TMP_Text : null;
        sourceGo = GoField != null ? GoField.GetValue(resolution) as Button : null;
        sourceStay = StayField != null ? StayField.GetValue(resolution) as Button : null;
    }

    private void BuildPresentation()
    {
        if (travelPanel == null || sourceGo == null || sourceStay == null ||
            sourceInfo == null || tabletRoot == null)
        {
            return;
        }

        // v5.30.9 mistakenly parented the visual to a narrow legacy field.
        // Never use travelPanel as the layout host; its RectTransform may be
        // only a few pixels wide depending on the existing scene LayoutGroup.
        Transform obsolete = travelPanel.transform.Find("Phase17F_TravelPolish");
        if (obsolete != null) Destroy(obsolete.gameObject);
        Transform existing = tabletRoot.Find("Phase17F_TravelPolish");
        if (existing != null)
        {
            // A previous helper may have survived scene navigation. Reuse it
            // instead of duplicating controls or creating a second overlay.
            modernRoot = existing as RectTransform;
            if (modernRoot != null)
            {
                modernInfo = FindText(modernRoot, "TravelHero/TravelInfo");
                goCaption = FindText(modernRoot, "TravelAction/Caption");
                stayCaption = FindText(modernRoot, "StayAction/Caption");
                modernGo = FindButton(modernRoot, "TravelAction");
                modernStay = FindButton(modernRoot, "StayAction");
                if (modernGo != null)
                {
                    modernGo.onClick.RemoveAllListeners();
                    modernGo.onClick.AddListener(ConfirmTravel);
                }
                if (modernStay != null)
                {
                    modernStay.onClick.RemoveAllListeners();
                    modernStay.onClick.AddListener(ConfirmStay);
                }
            }
        }
        else
        {
            TravelVisual visual = BuildVisual(tabletRoot, string.Empty);
            modernRoot = visual.Root;
            modernInfo = visual.Info;
            modernGo = visual.GoButton;
            modernStay = visual.StayButton;
            goCaption = visual.GoCaption;
            stayCaption = visual.StayCaption;
            modernGo.onClick.AddListener(ConfirmTravel);
            modernStay.onClick.AddListener(ConfirmStay);
        }

        if (modernRoot != null)
        {
            modernRoot.SetAsLastSibling();
            // Do not SetActive(false) on originals: the rules/online handlers
            // still own their interactable flags and serialized button events.
            oldInfoGroup = HideSource(sourceInfo.gameObject);
            oldGoGroup = HideSource(sourceGo.gameObject);
            oldStayGroup = HideSource(sourceStay.gameObject);
            RefreshPresentation();
            Debug.Log("ATB_MODERN_TRAVEL_UI bound=true shared_platform=true authoritative_buttons_preserved=true");
        }
    }

    private struct TravelVisual
    {
        public RectTransform Root;
        public TMP_Text Info;
        public TMP_Text GoCaption;
        public TMP_Text StayCaption;
        public Button GoButton;
        public Button StayButton;
    }

    // The exact SAME composition routine is used by runtime and QA preview.
    // tabletRoot, not the narrow travelPanel, owns the full-size layout.
    private static TravelVisual BuildVisual(RectTransform host, string info)
    {
        GameObject root = CreateGraphic("Phase17F_TravelPolish", host,
            new Vector2(0.055f, 0.085f), new Vector2(0.945f, 0.85f), Back);
        var layout = root.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
        var rect = root.GetComponent<RectTransform>();
        GameObject top = CreateGraphic("TravelHero", rect,
            new Vector2(0.055f, 0.35f), new Vector2(0.945f, 0.96f), Card);
        CreateGraphic("HeroAccent", top.transform,
            new Vector2(0f, 0f), new Vector2(0.009f, 1f), Gold);
        CreateText("TravelHeading", top.transform, "TRAVEL HUB", 27f,
            FontStyles.Bold, Gold,
            new Vector2(0.05f, 0.79f), new Vector2(0.95f, 0.96f),
            TextAlignmentOptions.Center);
        TMP_Text body = CreateText("TravelInfo", top.transform, info, 21f,
            FontStyles.Normal, Color.white,
            new Vector2(0.085f, 0.09f), new Vector2(0.915f, 0.77f),
            TextAlignmentOptions.Center);
        body.fontSizeMin = 16f;
        body.overflowMode = TextOverflowModes.Overflow;
        CreateGraphic("FooterLine", rect,
            new Vector2(0.06f, 0.315f), new Vector2(0.94f, 0.32f), Outline);
        TMP_Text goCaption, stayCaption;
        Button go = CreateAction("TravelAction", rect, Go,
            new Vector2(0.095f, 0.075f), new Vector2(0.485f, 0.25f), out goCaption);
        Button stay = CreateAction("StayAction", rect, Stay,
            new Vector2(0.515f, 0.075f), new Vector2(0.905f, 0.25f), out stayCaption);
        return new TravelVisual { Root = rect, Info = body,
            GoButton = go, StayButton = stay,
            GoCaption = goCaption, StayCaption = stayCaption };
    }

#if UNITY_EDITOR
    /// <summary>Safe editor visual fixture; no board/turn manager is touched.</summary>
    public static RectTransform EditorCreatePreview(RectTransform host)
    {
        TravelVisual view = BuildVisual(host,
            "Would you like to travel to the nearest Event space?\n" +
            "Target: EVENT\nTravel fee: 50 ₵\n" +
            "If you pass Start, you earn +200 ₵.");
        view.GoButton.interactable = false;
        view.StayButton.interactable = false;
        return view.Root;
    }
#endif

    private void RefreshPresentation()
    {
        if (modernInfo == null || sourceInfo == null)
            return;

        string info = (sourceInfo.text ?? string.Empty).Trim();
        if (!string.Equals(modernInfo.text, info, StringComparison.Ordinal))
            modernInfo.text = info;

        UpdateAction(modernGo, goCaption, sourceGo, "TRAVEL");
        UpdateAction(modernStay, stayCaption, sourceStay, "STAY");
    }

    private static void UpdateAction(Button target, TMP_Text caption, Button source, string fallback)
    {
        if (target == null || source == null)
            return;

        if (target.interactable != source.interactable)
            target.interactable = source.interactable;

        string label = fallback;
        TMP_Text original = source.GetComponentInChildren<TMP_Text>(true);
        if (original != null && !string.IsNullOrWhiteSpace(original.text))
            label = original.text.Trim();
        if (caption != null && !string.Equals(caption.text, label, StringComparison.Ordinal))
            caption.text = label;
    }

    private void ConfirmTravel()
    {
        if (sourceGo != null && sourceGo.interactable)
            sourceGo.onClick.Invoke();
    }

    private void ConfirmStay()
    {
        if (sourceStay != null && sourceStay.interactable)
            sourceStay.onClick.Invoke();
    }

    private static CanvasGroup HideSource(GameObject original)
    {
        if (original == null)
            return null;
        CanvasGroup group = original.GetComponent<CanvasGroup>();
        if (group == null) group = original.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        return group;
    }

    private void RestoreLegacyVisuals()
    {
        RestoreGroup(oldInfoGroup);
        RestoreGroup(oldGoGroup);
        RestoreGroup(oldStayGroup);
    }

    private static void RestoreGroup(CanvasGroup group)
    {
        if (group == null) return;
        group.alpha = 1f;
        group.blocksRaycasts = true;
    }

    private static GameObject CreateGraphic(string name, Transform parent,
        Vector2 min, Vector2 max, Color color)
    {
        var result = new GameObject(name, typeof(RectTransform), typeof(Image));
        result.transform.SetParent(parent, false);
        var rect = result.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = result.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = name == "Phase17F_TravelPolish";
        return result;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value,
        float size, FontStyles style, Color color, Vector2 min,
        Vector2 max, TextAlignmentOptions alignment)
    {
        GameObject holder = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        holder.transform.SetParent(parent, false);
        RectTransform rect = holder.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = holder.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(10f, size * 0.55f);
        text.fontSizeMax = size;
        text.alignment = alignment;
        text.fontStyle = style;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateAction(string name, Transform parent, Color background,
        Vector2 min, Vector2 max, out TMP_Text caption)
    {
        GameObject root = CreateGraphic(name, parent, min, max, background);
        Image image = root.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = root.AddComponent<Button>();
        button.targetGraphic = image;
        caption = CreateText("Caption", root.transform, name == "TravelAction" ? "TRAVEL" : "STAY",
            25f, FontStyles.Bold, Color.white,
            new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.94f),
            TextAlignmentOptions.Center);
        return button;
    }

    private static TMP_Text FindText(RectTransform root, string path)
    {
        Transform element = root.Find(path);
        return element != null ? element.GetComponent<TMP_Text>() : null;
    }

    private static Button FindButton(RectTransform root, string path)
    {
        Transform element = root.Find(path);
        return element != null ? element.GetComponent<Button>() : null;
    }
}
