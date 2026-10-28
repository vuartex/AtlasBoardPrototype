using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One Board Theme dropdown in the existing LobbyGameSettingsDialog.
/// No second settings overlay, wallet, or room settings manager is created.
/// </summary>
public sealed class AtlasBoardLobbyBoardThemePicker : MonoBehaviour
{
    private TMP_Dropdown dropdown;
    private Action<string> onSelect;
    private bool setupDone;
    private string[] ids = { "default_board" };
    private string current = "default_board";
    private static readonly Color Black = new Color(0.10f, 0.12f, 0.16f, 1f);
    private static readonly Color White = Color.white;
    private RectTransform rowRoot;

    private static GameObject Make(string name, Transform parent, params Type[] components)
    {
        var go = new GameObject(name, components);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    private static TMP_Text Label(Transform parent, string name, string value,
        float font, float x, float y, float w, float h)
    {
        var t = Make(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        t.text = value;
        t.fontSize = font;
        t.color = Black;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        // Only the Dropdown/Image/Toggle should receive pointer events.
        // Decorative TMP labels must not intercept the authored Apply button.
        t.raycastTarget = false;
        Place(t.rectTransform, x, y, w, h);
        return t;
    }

    // Preserve the scene-authored password, Show/Copy, and Done objects and
    // their listeners. Reflow at most once per original 520-high dialog.
    private static void ReflowExistingDialog(RectTransform panel, RectTransform done, bool expand)
    {
        const float extraHeight = 140f;
        const float topShift = 58f;
        const float doneShift = -63f;
        if (expand)
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, panel.sizeDelta.y + extraHeight);
        for (int i = 0; i < panel.childCount; i++)
        {
            RectTransform child = panel.GetChild(i) as RectTransform;
            if (child == null || child.name == "BoardThemeRow") continue;
            Vector2 position = child.anchoredPosition;
            position.y += done != null && child == done ? doneShift : topShift;
            child.anchoredPosition = position;
        }
    }

    private static void PlaceDropdownTemplateBelow(RectTransform rt, RectTransform dropdownRect)
    {
        if (rt == null || dropdownRect == null) return;
        // TMP_Dropdown duplicates this inactive template during Show().
        // Anchor to the BOTTOM of its owning dropdown, with a TOP pivot.
        // This removes the old +116 centre offset that covered Apply Password.
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -2f);
        rt.sizeDelta = new Vector2(0f, 172f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        Image background = rt.GetComponent<Image>();
        if (background != null) background.color = White;
        // TMP may flip automatically only when the list would leave the root canvas.
        // On a normal Game Settings canvas this template has space below.
    }

    private static void MakeTextNonBlocking(Transform root)
    {
        if (root == null) return;
        foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true))
            if (t != null) t.raycastTarget = false;
    }

    private void BindSelectionOnce()
    {
        if (dropdown == null) return;
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(index =>
        {
            if (index >= 0 && index < ids.Length)
                onSelect?.Invoke(ids[index]);
        });
    }

    public void Initialize(Transform overlay, Transform passwordField, Button close)
    {
        Initialize(overlay, passwordField, close, null);
    }

    public void Initialize(Transform overlay, Transform passwordField, Button close,
        Action<string> selected)
    {
        if (selected != null) onSelect = selected;
        if (setupDone) return;

        RectTransform panel = passwordField != null
            ? passwordField.parent as RectTransform : null;
        if (panel == null || panel.name != "LobbyGameSettingsDialog")
            panel = overlay != null
                ? overlay.Find("LobbyGameSettingsDialog") as RectTransform : null;
        if (panel == null)
        {
            Debug.LogWarning("ATB_BOARD_THEME_LAYOUT: canonical LobbyGameSettingsDialog missing; duplicate avoided.");
            return;
        }

        RectTransform done = close != null ? close.transform as RectTransform
            : panel.Find("Button_CloseLobbyGameSettings") as RectTransform;
        bool needsExpansion = panel.rect.height < 640f;
        bool needsReflow = needsExpansion ||
            (done != null && done.anchoredPosition.y > -248f);
        if (needsReflow)
            ReflowExistingDialog(panel, done, needsExpansion);

        RectTransform existingRow = panel.Find("BoardThemeRow") as RectTransform;
        if (existingRow != null)
        {
            rowRoot = existingRow;
            Place(rowRoot, -26f, -184f, 610f, 52f);
            dropdown = rowRoot.GetComponentInChildren<TMP_Dropdown>(true);
            if (dropdown == null)
            {
                Debug.LogWarning("ATB_BOARD_THEME_LAYOUT: row exists without its dropdown; duplicate avoided.");
                return;
            }
            RectTransform dropRect = dropdown.transform as RectTransform;
            Place(dropRect, 131f, 0f, 244f, 44f);
            RectTransform oldTemplate = dropdown.template != null
                ? dropdown.template : dropdown.transform.Find("Template") as RectTransform;
            PlaceDropdownTemplateBelow(oldTemplate, dropRect);
            MakeTextNonBlocking(rowRoot);
            setupDone = true;
            BindSelectionOnce();
            Configure(false, false, false, false, "default_board");
            return;
        }

        rowRoot = Make("BoardThemeRow", panel, typeof(RectTransform))
            .GetComponent<RectTransform>();
        Place(rowRoot, -26f, -184f, 610f, 52f);
        Label(rowRoot, "BoardThemeLabel", LocalizedBoardLabel(), 19f,
            -152f, 0f, 230f, 44f);

        var drop = Make("BoardThemeDropdown", rowRoot,
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(TMP_Dropdown));
        RectTransform dropRectNew = (RectTransform)drop.transform;
        Place(dropRectNew, 131f, 0f, 244f, 44f);
        drop.GetComponent<Image>().color = White;
        dropdown = drop.GetComponent<TMP_Dropdown>();
        dropdown.targetGraphic = drop.GetComponent<Image>();
        dropdown.captionText = Label(drop.transform, "Caption", "Classic / Default",
            18f, -11f, 0f, 196f, 36f);
        Label(drop.transform, "Arrow", "v", 18f, 101f, 0f, 24f, 32f);

        var template = Make("Template", drop.transform,
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(ScrollRect));
        RectTransform templateRect = (RectTransform)template.transform;
        PlaceDropdownTemplateBelow(templateRect, dropRectNew);
        var scroll = template.GetComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;

        var viewport = Make("Viewport", template.transform,
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Mask));
        RectTransform vr = (RectTransform)viewport.transform;
        vr.anchorMin = Vector2.zero;
        vr.anchorMax = Vector2.one;
        vr.offsetMin = vr.offsetMax = Vector2.zero;
        viewport.GetComponent<Image>().color = White;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = Make("Content", viewport.transform, typeof(RectTransform));
        RectTransform cr = (RectTransform)content.transform;
        cr.anchorMin = new Vector2(0f, 1f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.anchoredPosition = Vector2.zero;
        cr.sizeDelta = new Vector2(0f, 42f);
        scroll.viewport = vr;
        scroll.content = cr;

        var item = Make("Item", content.transform,
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Toggle));
        RectTransform ir = (RectTransform)item.transform;
        ir.anchorMin = new Vector2(0f, 1f);
        ir.anchorMax = new Vector2(1f, 1f);
        ir.pivot = new Vector2(0.5f, 1f);
        ir.anchoredPosition = Vector2.zero;
        ir.sizeDelta = new Vector2(0f, 42f);
        item.GetComponent<Image>().color = White;
        var toggle = item.GetComponent<Toggle>();
        toggle.targetGraphic = item.GetComponent<Image>();
        TMP_Text itemText = Label(item.transform, "ItemLabel",
            "Classic / Default", 17f, 0f, 0f, 226f, 38f);
        dropdown.itemText = itemText;
        dropdown.template = templateRect;
        template.SetActive(false);
        setupDone = true;
        BindSelectionOnce();
        Configure(false, false, false, false, "default_board");
    }

    public void SelectWithoutNotify(string id)
    {
        current = string.IsNullOrEmpty(id) ? "default_board" : id;
        if (dropdown == null) return;
        for (int i = 0; i < ids.Length; i++)
        {
            if (ids[i] != current) continue;
            dropdown.SetValueWithoutNotify(i);
            dropdown.RefreshShownValue();
            return;
        }
    }

    private static string DefaultName(string language)
    {
        switch (AtlasBoardLocalizationLanguages.Normalize(language))
        {
            case "tr": return "Klasik / Varsayılan";
            case "es": return "Clásico / Predeterminado";
            case "fr": return "Classique / Défaut";
            case "de": return "Klassisch / Standard";
            case "ko": return "기본 / 클래식";
            case "ru": return "Классическая / По умолчанию";
            default: return "Classic / Default";
        }
    }

    private static string LocalizedBoardLabel()
    {
        string lang = AtlasBoardLocalizationManager.Instance != null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";
        switch (AtlasBoardLocalizationLanguages.Normalize(lang))
        {
            case "tr": return "TAHTA TEMASI";
            case "es": return "TEMA DEL TABLERO";
            case "fr": return "THÈME DU PLATEAU";
            case "de": return "SPIELBRETT-DESIGN";
            case "ko": return "보드 테마";
            case "ru": return "ТЕМА ПОЛЯ";
            default: return "BOARD THEME";
        }
    }

    public void Configure(bool isHost, bool azure, bool maple, bool aurora,
        string selected)
    {
        if (dropdown == null) return;
        var options = new List<TMP_Dropdown.OptionData>();
        var available = new List<string>();
        Action<string, string, bool> add = (id, name, allowed) =>
        {
            if (!allowed) return;
            available.Add(id);
            options.Add(new TMP_Dropdown.OptionData(name));
        };
        string lang = AtlasBoardLocalizationManager.Instance != null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";
        add("default_board", DefaultName(lang), true);
        add("board_midnight_azure",
            AtlasBoardShopContentDraft.LocalizedName("board_midnight_azure", lang),
            azure || selected == "board_midnight_azure");
        add("board_ember_maple",
            AtlasBoardShopContentDraft.LocalizedName("board_ember_maple", lang),
            maple || selected == "board_ember_maple");
        add("board_arctic_aurora",
            AtlasBoardShopContentDraft.LocalizedName("board_arctic_aurora", lang),
            aurora || selected == "board_arctic_aurora");
        TMP_Text label = rowRoot != null
            ? rowRoot.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null) label.text = LocalizedBoardLabel();
        ids = available.ToArray();
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.interactable = isHost;
        SelectWithoutNotify(selected);
    }
}
