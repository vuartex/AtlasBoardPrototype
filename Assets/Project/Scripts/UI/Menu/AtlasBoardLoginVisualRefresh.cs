using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phase19DF: non-invasive skin for the existing canonical Login view.
/// Auth/password/registration actions and callbacks remain owned by
/// AtlasBoardAccountAccessUI and AtlasBoardAccountFormQualityGate.
/// Never clones any account controller, modal, authentication service or form.
/// All illustrations are Unity UI primitives (no untrusted download/font glyph).
/// </summary>
[DefaultExecutionOrder(32025)]
public sealed class AtlasBoardLoginVisualRefresh : MonoBehaviour
{
    private static AtlasBoardLoginVisualRefresh instance;
    private const float ScanSeconds = 0.22f;
    private static readonly Color Navy = Parse("#111A3C");
    private static readonly Color PanelTint = Parse("#192654");
    private static readonly Color Violet = Parse("#35306E");
    private static readonly Color Gold = Parse("#F6C34B");
    private static readonly Color Muted = Parse("#B7C7DF");
    private static readonly Color Text = Parse("#F8F9FF");
    private static readonly Color Teal = Parse("#219FC4");
    private RectTransform window;
    private RectTransform visual;
    private Transform currentForm;
    private float nextScan;
    private bool loginLayout;
    private string currentLocale = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject host = new GameObject("AtlasBoard_Login_Visual_Refresh");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AtlasBoardLoginVisualRefresh>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    private static Color Parse(string html)
    {
        ColorUtility.TryParseHtmlString(html, out Color color);
        return color;
    }

    private static string L(string en, string tr, string es, string fr,
        string de, string ko, string ru) => AtlasBoardL.R(en, tr, es, fr, de, ko, ru);

    private static RectTransform FindWindow()
    {
        foreach (RectTransform rect in Resources.FindObjectsOfTypeAll<RectTransform>())
        {
            if (rect == null || rect.name != "AccountWindow" ||
                !rect.gameObject.scene.IsValid()) continue;
            return rect;
        }
        return null;
    }

    // The canonical account form becomes visible synchronously during OpenModal,
    // sometimes AFTER Update/LateUpdate. Canvas.willRenderCanvases is the last
    // non-destructive opportunity to skin the new form BEFORE its first frame.
    // Never hide the canonical authentication canvas or recreate its controls.
    private void OnEnable()
    {
        Canvas.willRenderCanvases += SynchronizeBeforeCanvasRender;
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= SynchronizeBeforeCanvasRender;
    }

    private void LateUpdate()
    {
        SynchronizeLoginLayout();
    }

    private void SynchronizeBeforeCanvasRender()
    {
        SynchronizeLoginLayout();
    }

    private void SynchronizeLoginLayout()
    {
        // Only the expensive scene lookup is throttled. Layout for an EXISTING
        // window is reconciled every frame and before rendering, preventing
        // the old white login from flashing or a later status/guest override.
        if (window == null)
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + ScanSeconds;
            window = FindWindow();
            currentForm = null;
        }
        if (window == null) return;
        Transform form = window.Find("FormRoot");
        bool onLogin = window.gameObject.activeInHierarchy && form != null &&
            form.Find("LoginEmail") != null && form.Find("LoginPassword") != null &&
            form.Find("Button_SignIn") != null;
        if (!onLogin)
        {
            if (loginLayout) ResetOriginalLayout();
            return;
        }
        string language = AtlasBoardLocalizationManager.Instance != null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";
        if (!loginLayout || currentForm != form || currentLocale != language ||
            visual == null || !visual.gameObject.activeSelf)
        {
            currentForm = form;
            currentLocale = language;
            ApplyLoginVisuals(form);
            loginLayout = true;
        }
        KeepLoginFooterAligned(form);
    }

    private void KeepLoginFooterAligned(Transform form)
    {
        // The older AccountFormQualityGate.StabilizeStatusText() still runs in
        // this project. It overwrites the guest button and status rectangle on
        // every Update/LateUpdate, so *both* must be reconciled just before
        // canvas rendering (not once per 0.22s scan).
        Move(form, "Button_ContinueAsGuest", 155f, -348f, 470f, 60f);
        TMP_Text status = window.Find("Status")?.GetComponent<TMP_Text>();
        if (status == null) return;
        RectTransform rect = status.rectTransform;
        Vector2 centerAnchor = new Vector2(.5f, .5f);
        if (rect.anchorMin != centerAnchor) rect.anchorMin = centerAnchor;
        if (rect.anchorMax != centerAnchor) rect.anchorMax = centerAnchor;
        if (rect.pivot != centerAnchor) rect.pivot = centerAnchor;
        Vector2 position = new Vector2(161f, -330f);
        if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        Vector2 size = new Vector2(610f, 44f);
        if (rect.sizeDelta != size) rect.sizeDelta = size;
        if (status.enableAutoSizing) status.enableAutoSizing = false;
        if (!Mathf.Approximately(status.fontSize, 15f)) status.fontSize = 15f;
        if (status.textWrappingMode != TextWrappingModes.Normal)
            status.textWrappingMode = TextWrappingModes.Normal;
        if (status.overflowMode != TextOverflowModes.Ellipsis)
            status.overflowMode = TextOverflowModes.Ellipsis;
        if (status.alignment != TextAlignmentOptions.Center)
            status.alignment = TextAlignmentOptions.Center;
        Color tone = Parse("#E6C1B7");
        if (status.color != tone) status.color = tone;
    }

    private void ResetOriginalLayout()
    {
        loginLayout = false;
        currentForm = null;
        if (visual != null) visual.gameObject.SetActive(false);
        if (window == null) return;
        window.sizeDelta = new Vector2(980f, 900f);
        Image background = window.GetComponent<Image>();
        if (background != null) background.color = new Color32(247, 246, 241, 255);
        Transform header = window.Find("Header");
        if (header != null && header.TryGetComponent(out Image image))
            image.color = new Color32(25, 101, 148, 255);
        TMP_Text originalTitle = header != null
            ? header.Find("AccountTitle")?.GetComponent<TMP_Text>() : null;
        if (originalTitle != null)
        {
            originalTitle.color = Color.white;
            originalTitle.fontSize = 34f;
        }
        TMP_Text originalStatus = window.Find("Status")?.GetComponent<TMP_Text>();
        if (originalStatus != null)
        {
            originalStatus.rectTransform.anchoredPosition = new Vector2(0f, -282f);
            originalStatus.rectTransform.sizeDelta = new Vector2(820f, 84f);
        }
        // FormRoot is destroyed/rebuilt by canonical Account UI when moving
        // between Login/Register/Reset. No other form geometry is modified.
    }

    private void ApplyLoginVisuals(Transform form)
    {
        window.sizeDelta = new Vector2(1220f, 900f);
        Image background = window.GetComponent<Image>();
        if (background != null) background.color = Navy;
        Transform header = window.Find("Header");
        if (header != null && header.TryGetComponent(out Image image))
            image.color = Parse("#202B62");
        TMP_Text title = header != null ? header.Find("AccountTitle")?.GetComponent<TMP_Text>() : null;
        if (title != null)
        {
            title.color = Gold;
            title.fontSize = 37f;
        }
        if (visual == null || visual.parent != window)
        {
            Transform existing = window.Find("AtlasLoginIllustration");
            visual = existing as RectTransform;
            if (visual == null) BuildVisuals();
        }
        if (visual != null)
        {
            visual.gameObject.SetActive(true);
            visual.SetAsFirstSibling();
            TMP_Text caption = visual.Find("IllustrationTagline")?.GetComponent<TMP_Text>();
            if (caption != null) caption.text = L(
                "YOUR JOURNEY STARTS HERE", "YOLCULUĞUN BURADA BAŞLIYOR",
                "TU VIAJE EMPIEZA AQUÍ", "VOTRE VOYAGE COMMENCE ICI",
                "DEINE REISE BEGINNT HIER", "여기에서 여정을 시작하세요", "ТВОЙ ПУТЬ НАЧИНАЕТСЯ ЗДЕСЬ");
            TMP_Text detail = visual.Find("IllustrationDetail")?.GetComponent<TMP_Text>();
            if (detail != null) detail.text = L(
                "Build your empire, play with friends, and collect your journey.",
                "Şehirlerini geliştir, arkadaşlarınla oyna ve yolculuğunu biriktir.",
                "Construye tu ciudad y juega.", "Bâtissez et jouez ensemble.",
                "Baue Städte und spiele gemeinsam.", "도시를 건설하고 함께 플레이하세요.",
                "Строй города и играй с друзьями.");
        }
        // Keep EVERY original button callback intact. Only geometry/color
        // changes are made, with no second auth view or duplicate input.
        Move(form, "EmailLabel", 161f, 222f, 590f, 38f);
        Move(form, "LoginEmail", 161f, 156f, 604f, 72f);
        Move(form, "PasswordLabel", 161f, 77f, 590f, 38f);
        Move(form, "LoginPassword", 161f, 12f, 604f, 72f);
        Move(form, "Button_SignIn", -4f, -115f, 290f, 68f);
        Move(form, "Button_OpenRegister", 325f, -115f, 308f, 68f);
        Move(form, "Button_ResetPassword", 161f, -204f, 402f, 54f);
        Move(form, "Button_ContinueAsGuest", 155f, -348f, 470f, 60f);
        foreach (string name in new[] { "LoginEmail", "LoginPassword" })
        {
            Transform field = form.Find(name);
            if (field == null) continue;
            Image fieldImage = field.GetComponent<Image>();
            if (fieldImage != null) fieldImage.color = Parse("#F6F8FF");
            TMP_InputField input = field.GetComponent<TMP_InputField>();
            if (input != null && input.textComponent != null)
                input.textComponent.color = Parse("#243459");
        }
        foreach (string labelName in new[] { "EmailLabel", "PasswordLabel" })
        {
            TMP_Text label = form.Find(labelName)?.GetComponent<TMP_Text>();
            if (label != null) label.color = Muted;
        }
        SetButtonColor(form, "Button_SignIn", Teal);
        SetButtonColor(form, "Button_OpenRegister", Parse("#6253CB"));
        SetButtonColor(form, "Button_ResetPassword", Parse("#27396D"));
        SetButtonColor(form, "Button_ContinueAsGuest", Parse("#3D377D"));
        TMP_Text status = window.Find("Status")?.GetComponent<TMP_Text>();
        if (status != null)
        {
            status.color = Parse("#E6C1B7");
            // KeepLoginFooterAligned reasserts final placement before render;
            // we retain the original text and its localization verbatim.
        }
    }

    private static void Move(Transform parent, string name, float x, float y, float w, float h)
    {
        RectTransform rect = parent.Find(name) as RectTransform;
        if (rect == null) return;
        Vector2 center = new Vector2(.5f, .5f);
        if (rect.anchorMin != center) rect.anchorMin = center;
        if (rect.anchorMax != center) rect.anchorMax = center;
        if (rect.pivot != center) rect.pivot = center;
        Vector2 position = new Vector2(x, y);
        Vector2 size = new Vector2(w, h);
        if (rect.anchoredPosition != position) rect.anchoredPosition = position;
        if (rect.sizeDelta != size) rect.sizeDelta = size;
    }

    private static void SetButtonColor(Transform parent, string name, Color color)
    {
        Image image = parent.Find(name)?.GetComponent<Image>();
        if (image != null) image.color = color;
    }

    private static RectTransform Panel(Transform parent, string name,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static TMP_Text Label(Transform parent, string name, string value,
        Vector2 position, Vector2 size, float fontSize, Color color, bool bold = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 15;
        text.fontSizeMax = fontSize;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    private void BuildVisuals()
    {
        visual = Panel(window, "AtlasLoginIllustration", Vector2.zero,
            new Vector2(1220, 780), Color.clear);
        Panel(visual, "VerticalSplit", new Vector2(-210, -28),
            new Vector2(2, 610), Parse("#3B487D"));
        RectTransform left = Panel(visual, "BoardIllustration", new Vector2(-398, -26),
            new Vector2(362, 664), PanelTint);
        Outline outline = left.gameObject.AddComponent<Outline>();
        outline.effectColor = Parse("#564FA4");
        outline.effectDistance = new Vector2(3, -3);
        Label(left, "ArtBrand", "ATLAS BOARD", new Vector2(0, 257),
            new Vector2(330, 60), 34, Gold, true);
        Label(left, "ArtMini", "THE BOARD IS YOUR WORLD", new Vector2(0, 207),
            new Vector2(310, 36), 15, Muted);
        // Board-game path artwork: standalone UI meshes, NO font symbols,
        // NO fetched assets and no additional material allocations per frame.
        RectTransform board = Panel(left, "BoardWorld", new Vector2(0, 15),
            new Vector2(304, 315), Parse("#2A3475"));
        Outline boardOutline = board.gameObject.AddComponent<Outline>();
        boardOutline.effectColor = Gold;
        boardOutline.effectDistance = new Vector2(3, -3);
        for (int row = 0; row < 4; row++)
        for (int col = 0; col < 4; col++)
        {
            // A golden path winds around violet inner cities.
            bool route = row == 0 || col == 3 || (row == 2 && col <= 2) ||
                (row == 3 && col == 0);
            Color tile = route ? Parse("#D6AB4E") : Parse("#424392");
            RectTransform cell = Panel(board, "RouteTile_" + row + "_" + col,
                new Vector2(-111 + col * 74, 111 - row * 74),
                new Vector2(65, 65), tile);
            RectTransform inset = Panel(cell, "TileFace", Vector2.zero,
                new Vector2(55, 55), route ? Parse("#5C4E96") : Parse("#35356C"));
            if ((row + col) % 3 == 0)
                Panel(inset, "GoldHouse", new Vector2(0, -7),
                    new Vector2(23, 23), route ? Gold : Teal);
            if ((row + col) % 5 == 0)
                Panel(inset, "Roof", new Vector2(0, 10),
                    new Vector2(32, 9), Parse("#C1E5ED"));
        }
        RectTransform dice = Panel(left, "GoldenDice", new Vector2(101, -145),
            new Vector2(83, 83), Gold);
        dice.localRotation = Quaternion.Euler(0, 0, -14f);
        foreach (Vector2 dot in new[] {
            new Vector2(-17, 17), new Vector2(17, 17), Vector2.zero,
            new Vector2(-17, -17), new Vector2(17, -17) })
            Panel(dice, "Pip", dot, new Vector2(11, 11), Navy);
        Panel(left, "Coin", new Vector2(-105, -158), new Vector2(59, 59), Teal);
        Panel(left, "CoinCore", new Vector2(-105, -158), new Vector2(35, 35), Gold);
        Label(visual, "IllustrationTagline", "", new Vector2(-398, -244),
            new Vector2(350, 56), 21, Text, true);
        Label(visual, "IllustrationDetail", "", new Vector2(-398, -310),
            new Vector2(335, 66), 17, Muted);
        Panel(visual, "RightFormSurface", new Vector2(171, -27),
            new Vector2(682, 665), Parse("#202B5D"));
        Label(visual, "RightHeadline", "WELCOME TO ATLAS BOARD", new Vector2(161, 282),
            new Vector2(638, 42), 23, Gold, true);
    }
}
