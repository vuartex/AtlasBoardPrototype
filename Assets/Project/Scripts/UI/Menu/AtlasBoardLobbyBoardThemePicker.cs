using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One persistent dropdown in the ORIGINAL More Settings overlay.
/// Constructed once per overlay instance; never one per match, and never a clone
/// of the Environment Theme dropdown. Board choice is a separate room setting.
/// </summary>
public sealed class AtlasBoardLobbyBoardThemePicker : MonoBehaviour
{
    private TMP_Dropdown dropdown;
    private Action<string> onSelect;
    private bool setupDone;
    private string[] ids = { "default_board" };
    private string current = "default_board";
    private static readonly Color Black = new Color(0.10f,0.12f,0.16f,1f);
    private static readonly Color White = Color.white;
    private RectTransform rowRoot;

    private static GameObject Make(string name, Transform parent, params Type[] components)
    {
        var go = new GameObject(name, components);
        go.transform.SetParent(parent,false);
        return go;
    }
    private static RectTransform Place(RectTransform rt,float x,float y,float w,float h)
    {
        rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);
        rt.pivot=new Vector2(.5f,.5f);
        rt.anchoredPosition=new Vector2(x,y);
        rt.sizeDelta=new Vector2(w,h);
        return rt;
    }
    private static TMP_Text Label(Transform parent,string name,string value,float font,float x,float y,float w,float h)
    {
        var t=Make(name,parent,typeof(RectTransform),typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        t.text=value;t.fontSize=font;t.color=Black;
        t.alignment=TextAlignmentOptions.Center;t.textWrappingMode=TextWrappingModes.NoWrap;
        Place(t.rectTransform,x,y,w,h);
        return t;
    }

    // Layout measured against the authored Board_Prototype.unity dialog:
    // LobbyGameSettingsDialog 760x520, password APPLY at y=-102,
    // status at -156 and DONE at -218. The earlier board row at -135
    // overlapped APPLY. Expand the real dialog and reflow ONCE instead.
    private static void ReflowExistingDialog(RectTransform panel, RectTransform done, bool expand)
    {
        // The original card has centered anchors on each of its direct children.
        // Use the existing absolute scene positions as the source of truth. Do
        // not clone or reparent password controls; their listeners must persist.
        const float extraHeight = 140f;
        const float topShift = 58f;
        const float doneShift = -63f;
        if (expand) panel.sizeDelta = new Vector2(panel.sizeDelta.x, panel.sizeDelta.y + extraHeight);
        for (int i = 0; i < panel.childCount; i++)
        {
            RectTransform child = panel.GetChild(i) as RectTransform;
            if (child == null || child.name == "BoardThemeRow") continue;
            Vector2 position = child.anchoredPosition;
            position.y += done != null && child == done
                ? doneShift : topShift;
            child.anchoredPosition = position;
        }
    }

    private void BindSelectionOnce()
    {
        if (dropdown == null) return;
        // This dropdown belongs exclusively to this picker. Do not accumulate
        // runtime listeners after a domain reload or returning to the lobby.
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(index=>
        {
            if(index>=0&&index<ids.Length) onSelect?.Invoke(ids[index]);
        });
    }

    public void Initialize(Transform overlay,Transform passwordField,Button close)
    {
        Initialize(overlay,passwordField,close,null);
    }
    public void Initialize(Transform overlay,Transform passwordField,Button close,Action<string> selected)
    {
        if (selected!=null) onSelect=selected;
        if (setupDone) return;

        // The password field is a direct child of LobbyGameSettingsDialog in
        // the canonical scene. Never pick an arbitrary size-matching ancestor:
        // that can resize the dimmer instead of the real dialog.
        RectTransform panel=passwordField!=null
            ? passwordField.parent as RectTransform : null;
        if (panel == null || panel.name != "LobbyGameSettingsDialog")
            panel=overlay!=null ? overlay.Find("LobbyGameSettingsDialog") as RectTransform : null;
        if (panel==null)
        {
            Debug.LogWarning("ATB_BOARD_THEME_LAYOUT: canonical LobbyGameSettingsDialog missing; no duplicate UI created.");
            return;
        }

        // Migrate authored original 520-height scene once, and never resize a
        // second time on later lobby openings, rematches or host handovers.
        // If an owner-edited scene already provides >=640 height, respect it.
        RectTransform done = close != null ? close.transform as RectTransform
            : panel.Find("Button_CloseLobbyGameSettings") as RectTransform;
        bool needsExpansion = panel.rect.height < 640f;
        bool needsReflow = needsExpansion ||
            (done != null && done.anchoredPosition.y > -248f);
        if (needsReflow)
            ReflowExistingDialog(panel, done, needsExpansion);

        // Recover one existing dynamically-created row after Unity script/domain
        // reload. Never create a second dropdown on top of the old one.
        RectTransform existingRow = panel.Find("BoardThemeRow") as RectTransform;
        if (existingRow != null)
        {
            rowRoot=existingRow;
            Place(rowRoot,0,-184f,610f,52f);
            dropdown=rowRoot.GetComponentInChildren<TMP_Dropdown>(true);
            if (dropdown == null)
            {
                Debug.LogWarning("ATB_BOARD_THEME_LAYOUT: existing row has no dropdown; duplicate avoided.");
                return;
            }
            Place(dropdown.transform as RectTransform,131f,0,244f,44f);
            RectTransform oldTemplate=dropdown.transform.Find("Template") as RectTransform;
            if(oldTemplate!=null)Place(oldTemplate,0,116f,244f,172f);
            setupDone=true;
            BindSelectionOnce();
            Configure(false,false,false,false,"default_board");
            return;
        }
        setupDone=true;
        rowRoot=Make("BoardThemeRow",panel,typeof(RectTransform)).GetComponent<RectTransform>();
        // Centered under the original password status, clear of Apply Password
        // and above the DONE button; no overlap at default canvas scale.
        Place(rowRoot,0,-184f,610f,52f);
        Label(rowRoot,"BoardThemeLabel",LocalizedBoardLabel(),19f,-152f,0,230f,44f);
        var drop=Make("BoardThemeDropdown",rowRoot,typeof(RectTransform),typeof(CanvasRenderer),
            typeof(Image),typeof(TMP_Dropdown));
        Place((RectTransform)drop.transform,131f,0,244f,44f);
        drop.GetComponent<Image>().color=White;
        dropdown=drop.GetComponent<TMP_Dropdown>();
        dropdown.targetGraphic=drop.GetComponent<Image>();
        dropdown.captionText=Label(drop.transform,"Caption","Classic / Default",18f,-11,0,196,36);
        Label(drop.transform,"Arrow","v",18f,101,0,24,32);

        // Popup opens UPWARD to avoid covering DONE or leaving the dialog.
        // A temporary TMP dropdown popup is normal; the picker itself is
        // constructed just once and reused across all lobby lifecycle events.
        var template=Make("Template",drop.transform,typeof(RectTransform),typeof(CanvasRenderer),
            typeof(Image),typeof(ScrollRect));
        Place((RectTransform)template.transform,0,116,244,172);
        template.GetComponent<Image>().color=White;
        var scroll=template.GetComponent<ScrollRect>();
        scroll.horizontal=false;scroll.vertical=true;
        var viewport=Make("Viewport",template.transform,typeof(RectTransform),typeof(CanvasRenderer),
            typeof(Image),typeof(Mask));
        var vr=(RectTransform)viewport.transform;
        vr.anchorMin=Vector2.zero;vr.anchorMax=Vector2.one;vr.offsetMin=vr.offsetMax=Vector2.zero;
        viewport.GetComponent<Image>().color=White;
        viewport.GetComponent<Mask>().showMaskGraphic=false;
        var content=Make("Content",viewport.transform,typeof(RectTransform));
        var cr=(RectTransform)content.transform;
        cr.anchorMin=new Vector2(0,1);cr.anchorMax=new Vector2(1,1);cr.pivot=new Vector2(.5f,1);
        cr.anchoredPosition=Vector2.zero;cr.sizeDelta=new Vector2(0,42);
        scroll.viewport=vr;scroll.content=cr;
        var item=Make("Item",content.transform,typeof(RectTransform),typeof(CanvasRenderer),
            typeof(Image),typeof(Toggle));
        var ir=(RectTransform)item.transform;
        ir.anchorMin=new Vector2(0,1);ir.anchorMax=new Vector2(1,1);
        ir.pivot=new Vector2(.5f,1);ir.anchoredPosition=new Vector2(0,0);
        ir.sizeDelta=new Vector2(0,42);
        item.GetComponent<Image>().color=White;
        var toggle=item.GetComponent<Toggle>();toggle.targetGraphic=item.GetComponent<Image>();
        var itemText=Label(item.transform,"ItemLabel","Classic / Default",17f,0,0,226,38);
        dropdown.itemText=itemText;dropdown.template=(RectTransform)template.transform;
        template.SetActive(false);
        BindSelectionOnce();
        Configure(false,false,false,false,"default_board");
    }

    public void SelectWithoutNotify(string id)
    {
        current=string.IsNullOrEmpty(id)?"default_board":id;
        if(dropdown==null) return;
        for(int i=0;i<ids.Length;i++) if(ids[i]==current)
        {
            dropdown.SetValueWithoutNotify(i);dropdown.RefreshShownValue();return;
        }
    }

    private static string DefaultName(string language)
    {
        switch(AtlasBoardLocalizationLanguages.Normalize(language))
        {
            case "tr":return "Klasik / Varsayılan";
            case "es":return "Clásico / Predeterminado";
            case "fr":return "Classique / Défaut";
            case "de":return "Klassisch / Standard";
            case "ko":return "기본 / 클래식";
            case "ru":return "Классическая / По умолчанию";
            default:return "Classic / Default";
        }
    }
    private static string LocalizedBoardLabel()
    {
        string lang=AtlasBoardLocalizationManager.Instance!=null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";
        switch(AtlasBoardLocalizationLanguages.Normalize(lang))
        {
            case "tr":return "TAHTA TEMASI";
            case "es":return "TEMA DEL TABLERO";
            case "fr":return "THÈME DU PLATEAU";
            case "de":return "SPIELBRETT-DESIGN";
            case "ko":return "보드 테마";
            case "ru":return "ТЕМА ПОЛЯ";
            default:return "BOARD THEME";
        }
    }

    public void Configure(bool isHost,bool azure,bool maple,bool aurora,string selected)
    {
        if(dropdown==null)return;
        var options=new List<TMP_Dropdown.OptionData>();
        var available=new List<string>();
        Action<string,string,bool> add=(id,name,allowed)=>
        {
            if(!allowed)return;
            available.Add(id);options.Add(new TMP_Dropdown.OptionData(name));
        };
        string lang = AtlasBoardLocalizationManager.Instance != null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";
        add("default_board",DefaultName(lang),true);
        add("board_midnight_azure",AtlasBoardShopContentDraft.LocalizedName("board_midnight_azure",lang),azure||selected=="board_midnight_azure");
        add("board_ember_maple",AtlasBoardShopContentDraft.LocalizedName("board_ember_maple",lang),maple||selected=="board_ember_maple");
        add("board_arctic_aurora",AtlasBoardShopContentDraft.LocalizedName("board_arctic_aurora",lang),aurora||selected=="board_arctic_aurora");
        TMP_Text label=rowRoot!=null ? rowRoot.GetComponentInChildren<TMP_Text>(true) : null;
        if(label!=null)label.text=LocalizedBoardLabel();
        ids=available.ToArray();dropdown.ClearOptions();dropdown.AddOptions(options);
        dropdown.interactable=isHost;
        SelectWithoutNotify(selected);
    }
}
