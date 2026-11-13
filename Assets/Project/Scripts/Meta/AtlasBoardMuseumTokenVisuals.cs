using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presentation only. Reuses the existing Profile > Atlas Museum inventory and
/// its authenticated AtlasBoardMuseumCollection ownership and ETag APIs.
/// Adds an on-panel slot chooser, round minted tokens and owner-only hover text.
/// No new scene, navigation tab, manager, grant path or economy state.
/// </summary>
public sealed class AtlasBoardMuseumTokenVisuals : MonoBehaviour
{
    private AtlasBoardMuseumCollection museum;
    private RectTransform inventory;
    private RectTransform display;
    private TMP_Text hoverText;
    private GameObject hoverPanel;
    private GameObject chooser;
    private TMP_Text pageText;
    private Button equip;
    private Button remove;
    private string selection = "";
    private string activeUid = "";
    private string lastVisualLanguage = "";
    private bool selectingRemoval;
    private readonly Button[] inventoryButtons = new Button[12];
    private readonly Button[] slotButtons = new Button[3];
    private readonly Image[] coinImages = new Image[3];
    private readonly TMP_Text[] coinLabels = new TMP_Text[3];
    // Read-only General Profile preview of the SAME three authenticated Museum slots.
    // Never cache token ownership across UIDs or persist a second loadout.
    private RectTransform overviewPreview;
    private readonly Image[] overviewRims = new Image[3];
    private readonly Image[] overviewInners = new Image[3];
    private readonly TMP_Text[] overviewMarks = new TMP_Text[3];
    private TMP_Text overviewInfo;
    private GameObject overviewTooltip;
    private TMP_Text overviewTooltipText;
    private float nextOverviewCheckAt;
    private float nextOverviewLoadAt;
    // Cache the existing Profile chrome instead of repeatedly searching the scene.
    // UnifiedProfileScreen updates the mission preview in Update(); this component
    // normalizes the visible prefix in LateUpdate() before rendering that frame.
    private float nextProfileLocaleAt;
    private GameObject profileChromeRoot;
    private TMP_Text profileChromeHeading;
    private Transform profileChromeWeekly;
    private readonly TMP_Text[] profileChromeMissionLabels = new TMP_Text[4];
    private static Sprite coinSprite;

    private static readonly Color Gold = new Color32(246, 195, 73, 255);
    private static readonly Color Panel = new Color32(19, 29, 73, 250);
    private static readonly Color Muted = new Color32(183, 196, 226, 255);
    private static readonly Color Teal = new Color32(39, 178, 204, 255);
    private static readonly Color Rose = new Color32(201, 113, 170, 255);

    private void Awake() { museum = GetComponent<AtlasBoardMuseumCollection>(); }

    private void LateUpdate()
    {
        if (museum == null) return;
        string language = AtlasBoardLocalizationManager.Instance != null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";
        if (!string.Equals(language, lastVisualLanguage, StringComparison.OrdinalIgnoreCase))
        {
            lastVisualLanguage = language;
            CloseChooser();
            HideHover();
            HideOverviewHover();
            ApplyFont(hoverText);
            ApplyFont(overviewTooltipText);
            foreach (var mark in overviewMarks) ApplyFont(mark);
            foreach (var mark in coinLabels) ApplyFont(mark);
        }
        if (!string.Equals(activeUid, museum.Uid, StringComparison.Ordinal))
        {
            activeUid = museum.Uid;
            selection = "";
            CloseChooser();
            HideHover();
            HideOverviewHover();
            // Preserve existing EventTrigger bindings if the General panel is
            // still present; re-binding would duplicate hover callbacks.
            nextOverviewLoadAt = 0;
        }
        RefreshProfileChromeLocale();
        // The General Profile uses CollectionSlot_0..2, which are separate
        // visual placeholders. Link them to Museum's existing UID-owned Slots.
        RefreshOverviewPreview();
        GameObject activeInventory = GameObject.Find("MuseumInventory");
        if (activeInventory == null)
        {
            inventory = null;
            display = null;
            chooser = null;
            hoverPanel = null;
            return;
        }
        RectTransform next = activeInventory.GetComponent<RectTransform>();
        if (next == null) return;
        if (inventory != next)
        {
            inventory = next;
            display = GameObject.Find("MuseumDisplay")?.GetComponent<RectTransform>();
            if (display == null) return;
            BindCurrentPanels();
        }
        if (display != null) RefreshVisualState();
    }

    // The canonical weekly preview's receipt status is written by the existing
    // UnifiedProfileScreen.Update() every ~0.6s. Normalize only its PRESENTATION
    // in LateUpdate on the same rendered frame; waiting 0.25s made the English
    // [PAID] and localized [REÇU] prefixes visibly alternate.
    // No changes to paid/ready flags, Firebase reads, claims, wallet, or receipts.
    private void RefreshProfileChromeLocale()
    {
        // Only scan for the existing root while it is absent; when the screen
        // is open the cached references keep this path cheap on mobile devices.
        if (profileChromeRoot == null || !profileChromeRoot.activeInHierarchy)
        {
            profileChromeRoot = null;
            profileChromeHeading = null;
            profileChromeWeekly = null;
            for (int i = 0; i < profileChromeMissionLabels.Length; ++i)
                profileChromeMissionLabels[i] = null;
            if (Time.unscaledTime < nextProfileLocaleAt) return;
            nextProfileLocaleAt = Time.unscaledTime + 0.25f;
            profileChromeRoot = GameObject.Find("AtlasProfile");
            if (profileChromeRoot == null) return;
            nextProfileLocaleAt = 0f;
        }

        if (profileChromeHeading == null)
        {
            Transform title = profileChromeRoot.transform.Find("Title");
            if (title != null) title.TryGetComponent(out profileChromeHeading);
        }
        if (profileChromeHeading != null)
        {
            string translated = AtlasBoardL.R("PROFILE", "PROFİL", "PERFIL", "PROFIL",
                "PROFIL", "프로필", "ПРОФИЛЬ");
            if (!string.Equals(profileChromeHeading.text, translated, StringComparison.Ordinal))
            {
                profileChromeHeading.text = translated;
                ApplyFont(profileChromeHeading);
            }
        }

        // Unity destroys and recreates the WeeklyPreview on route switches.
        // UnityEngine.Object's null comparison detects the retired transform.
        if (profileChromeWeekly == null)
        {
            if (Time.unscaledTime < nextProfileLocaleAt) return;
            nextProfileLocaleAt = Time.unscaledTime + 0.25f;
            profileChromeWeekly = profileChromeRoot.transform.Find("ProfileStage/WeeklyPreview");
            if (profileChromeWeekly == null) return;
            for (int i = 0; i < profileChromeMissionLabels.Length; ++i)
                profileChromeMissionLabels[i] = null;
        }

        string paid = AtlasBoardL.R("PAID", "ALINDI", "COBRADO", "REÇU",
            "ERHALTEN", "수령", "ПОЛУЧЕНО");
        string ready = AtlasBoardL.R("READY", "HAZIR", "LISTO", "PRÊT",
            "BEREIT", "준비", "ГОТОВО");
        for (int i = 0; i < profileChromeMissionLabels.Length; i++)
        {
            TMP_Text label = profileChromeMissionLabels[i];
            if (label == null)
            {
                Transform node = profileChromeWeekly.Find("MissionPreview_" + i + "/Mission");
                if (node != null) node.TryGetComponent(out label);
                profileChromeMissionLabels[i] = label;
            }
            if (label == null) continue;
            string value = label.text;
            int end = value.IndexOf("] ", StringComparison.Ordinal);
            if (end < 0 || !value.StartsWith("[", StringComparison.Ordinal)) continue;
            string prefix = value.Substring(0, end + 2);
            bool received = prefix == "[PAID] " || prefix == "[ALINDI] " ||
                prefix == "[COBRADO] " || prefix == "[REÇU] " ||
                prefix == "[ERHALTEN] " || prefix == "[수령] " || prefix == "[ПОЛУЧЕНО] ";
            bool completed = prefix == "[READY] " || prefix == "[HAZIR] " ||
                prefix == "[LISTO] " || prefix == "[PRÊT] " ||
                prefix == "[BEREIT] " || prefix == "[준비] " || prefix == "[ГОТОВО] ";
            if (!received && !completed) continue;
            string localizedPrefix = "[" + (received ? paid : ready) + "] ";
            if (prefix == localizedPrefix) continue;
            label.text = localizedPrefix + value.Substring(end + 2);
            ApplyFont(label);
        }
    }

    private void RefreshOverviewPreview()
    {
        if (Time.unscaledTime < nextOverviewCheckAt) return;
        nextOverviewCheckAt = Time.unscaledTime + 0.30f;
        GameObject candidate = GameObject.Find("MuseumPreview");
        if (candidate == null)
        {
            overviewPreview = null;
            overviewTooltip = null;
            overviewInfo = null;
            overviewTooltipText = null;
            return;
        }
        RectTransform panel = candidate.GetComponent<RectTransform>();
        if (panel == null) return;
        if (overviewPreview != panel) BindOverviewPreview(panel);

        // The old General route did not request Museum data until Museum was
        // opened. Fetch once, with throttled retry, so profile-first works.
        if (!museum.Loaded && !museum.Busy &&
            !string.IsNullOrEmpty(museum.Uid) &&
            Time.unscaledTime >= nextOverviewLoadAt)
        {
            nextOverviewLoadAt = Time.unscaledTime + 8f;
            _ = museum.RefreshAsync();
        }
        for (int i = 0; i < 3; i++)
        {
            Image rim = overviewRims[i];
            TMP_Text mark = overviewMarks[i];
            if (rim == null || mark == null) continue;
            var token = museum.Loaded ? OwnedById(museum.Slots[i]) : null;
            // Reuse the actual minted round sprite, not a square placeholder.
            rim.sprite = CoinSprite();
            rim.type = Image.Type.Simple;
            rim.preserveAspect = true;
            rim.color = token == null ? new Color32(94, 107, 166, 190) : Color.white;
            if (overviewInners[i] != null) overviewInners[i].color = Color.clear;
            mark.text = token == null ? new[] { "I", "II", "III" }[i] : Monogram(token.Title);
            mark.color = token == null ? Muted : ThemeColor(token.Theme);
        }
    }

    private void BindOverviewPreview(RectTransform panel)
    {
        overviewPreview = panel;
        overviewInfo = ChildComponent<TMP_Text>(panel, "Info");
        for (int i = 0; i < 3; i++)
        {
            int slot = i;
            Transform node = panel.Find("CollectionSlot_" + i);
            overviewRims[i] = node == null ? null : node.GetComponent<Image>();
            Transform center = node == null ? null : node.Find("EmptyCoin");
            overviewInners[i] = center == null ? null : center.GetComponent<Image>();
            overviewMarks[i] = ChildComponent<TMP_Text>(center, "Slot");
            if (node == null) continue;
            Image rim = overviewRims[i];
            if (rim != null) rim.raycastTarget = true;
            EventTrigger trigger = node.GetComponent<EventTrigger>();
            if (trigger == null) trigger = node.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowOverviewHover(slot));
            trigger.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => HideOverviewHover());
            trigger.triggers.Add(exit);
        }
        // One tooltip inside the EXISTING general Museum card; no extra panel
        // route, new manager, or second inventory is introduced.
        Transform existing = panel.Find("MuseumOverviewHoverCard");
        RectTransform tip = existing == null
            ? MakeRect(panel, "MuseumOverviewHoverCard", new Vector2(0, -84),
                new Vector2(432, 92), Panel)
            : existing.GetComponent<RectTransform>();
        if (tip != null)
        {
            tip.GetComponent<Image>().raycastTarget = false;
            overviewTooltipText = tip.GetComponentInChildren<TMP_Text>(true);
            if (overviewTooltipText == null) overviewTooltipText = MakeText(tip, "", 15, Muted);
            overviewTooltipText.enableAutoSizing = true;
            overviewTooltipText.fontSizeMin = 11;
            overviewTooltipText.fontSizeMax = 15;
            overviewTooltip = tip.gameObject;
            overviewTooltip.SetActive(false);
        }
    }

    private void ShowOverviewHover(int slot)
    {
        if (museum == null || !museum.Loaded || slot < 0 || slot > 2 ||
            overviewTooltip == null || overviewTooltipText == null) return;
        var token = OwnedById(museum.Slots[slot]);
        if (token == null) { HideOverviewHover(); return; }
        overviewTooltipText.text = token.Title + "\n" + token.Description;
        overviewTooltip.SetActive(true);
        overviewTooltip.transform.SetAsLastSibling();
        if (overviewInfo != null) overviewInfo.gameObject.SetActive(false);
    }

    private void HideOverviewHover()
    {
        if (overviewTooltip != null) overviewTooltip.SetActive(false);
        if (overviewInfo != null) overviewInfo.gameObject.SetActive(true);
    }

    private static T ChildComponent<T>(Transform parent, string name) where T : Component
    {
        if (parent == null) return null;
        Transform t = parent.Find(name);
        return t == null ? null : t.GetComponent<T>();
    }

    private void BindCurrentPanels()
    {
        pageText = ChildComponent<TMP_Text>(inventory, "MuseumPage");
        equip = ChildComponent<Button>(inventory, "MuseumEquip");
        remove = ChildComponent<Button>(inventory, "MuseumRemove");
        // Only rebind the two existing Museum buttons, never the Store, Profile
        // navigation or the three slot selection buttons.
        if (equip != null)
        {
            equip.onClick.RemoveAllListeners();
            equip.onClick.AddListener(() => OpenChooser(false));
        }
        if (remove != null)
        {
            remove.onClick.RemoveAllListeners();
            remove.onClick.AddListener(() => OpenChooser(true));
        }
        for (int i = 0; i < 12; i++)
        {
            int localIndex = i;
            Button button = ChildComponent<Button>(inventory, "OwnedToken_" + i);
            inventoryButtons[i] = button;
            if (button == null) continue;
            // The existing click handler still updates the Profile's own selection.
            button.onClick.AddListener(() => SelectToken(localIndex));
            AddHover(button.gameObject, () => InventoryToken(localIndex));
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { label.enableAutoSizing = true; label.fontSizeMin = 10; label.fontSizeMax = 16; }
        }
        for (int i = 0; i < 3; i++)
        {
            int slotIndex = i;
            Button button = ChildComponent<Button>(display, "MuseumSlot_" + i);
            slotButtons[i] = button;
            if (button == null) continue;
            AddHover(button.gameObject, () => OwnedById(museum.Slots[slotIndex]));
            Transform originalFrame = button.transform.Find("GoldFrame");
            if (originalFrame != null && originalFrame.TryGetComponent(out Image frameImage))
                frameImage.color = Color.clear;
            RectTransform imageRect = MakeRect(button.transform, "MintedMuseumCoin", Vector2.zero,
                new Vector2(138, 138), Color.clear);
            imageRect.SetAsLastSibling();
            Image image = imageRect.GetComponent<Image>();
            image.sprite = CoinSprite();
            image.color = Color.white;
            image.raycastTarget = false;
            image.preserveAspect = true;
            coinImages[i] = image;
            RectTransform emblem = MakeRect(imageRect, "MintedCoinMark", new Vector2(0, 5),
                new Vector2(119, 55), Color.clear);
            emblem.GetComponent<Image>().raycastTarget = false;
            coinLabels[i] = MakeText(emblem, "AT", 31, Gold, true);
            var status = ChildComponent<TMP_Text>(display, "MuseumSlotStatus_" + i);
            if (status != null) { status.enableAutoSizing = true; status.fontSizeMin = 12; status.fontSizeMax = 16; }
        }
        CreateHoverCard();
        chooser = null;
    }

    private int PageIndex()
    {
        if (pageText == null || string.IsNullOrEmpty(pageText.text)) return 0;
        string head = pageText.text.Split('/')[0].Trim();
        return int.TryParse(head, out int page) ? Math.Max(0, page - 1) : 0;
    }

    private AtlasBoardMuseumCollection.Token InventoryToken(int localIndex)
    {
        int index = PageIndex() * 12 + localIndex;
        return index >= 0 && index < museum.Owned.Count ? museum.Owned[index] : null;
    }

    private AtlasBoardMuseumCollection.Token OwnedById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return museum.Owned.Find(t => t.Id == id);
    }

    private void SelectToken(int localIndex)
    {
        var item = InventoryToken(localIndex);
        if (item == null) return;
        selection = item.Id;
        CloseChooser();
        ShowHover(item);
    }

    private static RectTransform MakeRect(Transform parent, string name, Vector2 position,
        Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static void ApplyFont(TMP_Text label)
    {
        var manager = AtlasBoardLocalizationManager.Instance;
        if (label != null && manager != null) label.font = manager.ResolveFont(label.font);
    }
    private static TMP_Text MakeText(Transform parent, string content, float size,
        Color color, bool bold = false)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform r = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(6, 3);
        r.offsetMax = new Vector2(-6, -3);
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = size;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.color = color;
        t.enableAutoSizing = true;
        t.fontSizeMin = 12;
        t.fontSizeMax = size;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        ApplyFont(t);
        return t;
    }

    private static Button MakeButton(Transform parent, string name, string label,
        Vector2 position, Vector2 size, Color color, Action action)
    {
        RectTransform rect = MakeRect(parent, name, position, size, color);
        Image image = rect.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        MakeText(rect, label, 19, Color.white, true);
        button.onClick.AddListener(() => action());
        return button;
    }

    private void CreateHoverCard()
    {
        if (inventory == null) return;
        RectTransform card = MakeRect(inventory, "MuseumTokenHoverCard", new Vector2(0, 95),
            new Vector2(1220, 75), Panel);
        card.SetAsLastSibling();
        card.GetComponent<Image>().raycastTarget = false;
        hoverText = MakeText(card, "", 17, Muted);
        hoverText.alignment = TextAlignmentOptions.Center;
        hoverText.fontSizeMin = 13;
        hoverPanel = card.gameObject;
        hoverPanel.SetActive(false);
    }

    private void AddHover(GameObject go, Func<AtlasBoardMuseumCollection.Token> resolve)
    {
        EventTrigger trigger = go.GetComponent<EventTrigger>();
        if (trigger == null) trigger = go.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowHover(resolve()));
        trigger.triggers.Add(enter);
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => HideHover());
        trigger.triggers.Add(exit);
    }

    private void ShowHover(AtlasBoardMuseumCollection.Token token)
    {
        if (hoverPanel == null || token == null || hoverText == null) return;
        string description = string.IsNullOrWhiteSpace(token.Description)
            ? AtlasBoardL.R("Collectible from week ", "Haftalık koleksiyon: ", "Colección semanal: ",
                "Collection de la semaine ", "Wochensammlung: ", "주간 컬렉션: ", "Коллекция недели: ") + token.WeekId
            : token.Description;
        hoverText.text = token.Title + "\n" + description;
        hoverPanel.SetActive(true);
        hoverPanel.transform.SetAsLastSibling();
    }

    private void HideHover() { if (hoverPanel != null) hoverPanel.SetActive(false); }
    private void CloseChooser() { if (chooser != null) { Destroy(chooser); chooser = null; } }

    private void OpenChooser(bool removing)
    {
        if (inventory == null || museum == null || !museum.Loaded || museum.Busy) return;
        if (!removing && OwnedById(selection) == null) return;
        bool hasEquipped = false;
        for (int i = 0; i < 3; i++) hasEquipped |= !string.IsNullOrEmpty(museum.Slots[i]);
        if (removing && !hasEquipped) return;
        selectingRemoval = removing;
        HideHover();
        CloseChooser();
        RectTransform box = MakeRect(inventory, "MuseumChooseDisplaySlot", new Vector2(0, -2),
            new Vector2(715, 219), Panel);
        box.GetComponent<Image>().raycastTarget = true;
        box.SetAsLastSibling();
        chooser = box.gameObject;
        RectTransform header = MakeRect(box, "ChooseHeader", new Vector2(0, 74),
            new Vector2(665, 37), Color.clear);
        MakeText(header, removing
            ? AtlasBoardL.R("REMOVE TOKEN FROM WHICH SLOT?", "JETON HANGİ YUVADAN ÇIKARILSIN?",
                "¿DE QUÉ ESPACIO RETIRAR?", "DE QUEL EMPLACEMENT RETIRER ?",
                "AUS WELCHEM PLATZ ENTFERNEN?", "어느 슬롯에서 제거할까요?", "ИЗ КАКОЙ ЯЧЕЙКИ УБРАТЬ?")
            : AtlasBoardL.R("WHICH SLOT SHOULD DISPLAY THIS TOKEN?", "JETON HANGİ YUVADA SERGİLENSİN?",
                "¿EN QUÉ ESPACIO EXPONER?", "DANS QUEL EMPLACEMENT EXPOSER ?",
                "IN WELCHEM PLATZ AUSSTELLEN?", "어느 슬롯에 전시할까요?", "В КАКОЙ ЯЧЕЙКЕ РАЗМЕСТИТЬ?"),
            20, Gold, true);
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            bool usable = !removing || !string.IsNullOrEmpty(museum.Slots[i]);
            var button = MakeButton(box, "ChooseSlot_" + i, new[] {"I", "II", "III"}[i],
                new Vector2((i - 1) * 170, 5), new Vector2(127, 64),
                usable ? Teal : new Color32(61, 65, 98, 255), () => ApplyToSlot(index));
            button.interactable = usable;
        }
        MakeButton(box, "CancelSelection", AtlasBoardL.R("CANCEL", "İPTAL", "CANCELAR",
            "ANNULER", "ABBRECHEN", "취소", "ОТМЕНА"), new Vector2(0, -76),
            new Vector2(190, 38), new Color32(82, 71, 156, 255), CloseChooser);
    }

    private async void ApplyToSlot(int slot)
    {
        if (museum == null || museum.Busy) return;
        bool removing = selectingRemoval;
        string target = removing ? "" : selection;
        if (!removing && OwnedById(target) == null) return;
        CloseChooser();
        await museum.EquipAsync(slot, target);
        // Existing Profile screen listens to the museum's Changed event and
        // redraws original slot labels. Round visuals are updated in LateUpdate.
        if (!string.IsNullOrEmpty(museum.Message) &&
            (museum.Message.IndexOf("Firebase", StringComparison.OrdinalIgnoreCase) >= 0 ||
             museum.Message.IndexOf("HTTP", StringComparison.OrdinalIgnoreCase) >= 0))
            Debug.LogWarning("ATB_MUSEUM_SLOT " + museum.Message);
    }

    private void RefreshVisualState()
    {
        if (museum == null) return;
        // Existing UI requires a slot to be selected before enabling Equip.
        // Our on-panel chooser makes that prerequisite unnecessary.
        if (equip != null) equip.interactable = museum.Loaded && !museum.Busy &&
            !string.IsNullOrEmpty(selection) && OwnedById(selection) != null;
        if (remove != null) remove.interactable = museum.Loaded && !museum.Busy &&
            (!string.IsNullOrEmpty(museum.Slots[0]) ||
             !string.IsNullOrEmpty(museum.Slots[1]) ||
             !string.IsNullOrEmpty(museum.Slots[2]));
        for (int i = 0; i < 3; i++)
        {
            if (slotButtons[i] == null) continue;
            var background = slotButtons[i].GetComponent<Image>();
            if (background != null) background.color = Color.clear;
            Transform frame = slotButtons[i].transform.Find("GoldFrame");
            if (frame != null && frame.TryGetComponent(out Image originalFrame))
                originalFrame.color = Color.clear;
            var token = OwnedById(museum.Slots[i]);
            if (coinImages[i] != null)
            {
                coinImages[i].sprite = CoinSprite();
                coinImages[i].color = token == null ? new Color32(92, 103, 162, 155) : Color.white;
            }
            if (coinLabels[i] != null)
            {
                coinLabels[i].text = token == null ? new[] { "I", "II", "III" }[i] : Monogram(token.Title);
                coinLabels[i].color = token == null ? Muted : ThemeColor(token.Theme);
            }
        }
    }

    private static string Monogram(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "AT";
        string[] parts = title.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1) return parts[0].Substring(0, 1).ToUpperInvariant() +
            parts[1].Substring(0, 1).ToUpperInvariant();
        return title.Substring(0, Math.Min(2, title.Length)).ToUpperInvariant();
    }

    private static Color ThemeColor(string theme)
    {
        switch (theme)
        {
            case "city": return new Color32(152, 239, 255, 255);
            case "name": return new Color32(229, 177, 255, 255);
            case "object": return Gold;
            case "holiday": return new Color32(255, 188, 207, 255);
            default: return Muted;
        }
    }

    private static Sprite CoinSprite()
    {
        if (coinSprite != null) return coinSprite;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float dx = x + .5f - size / 2f, dy = y + .5f - size / 2f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float shine = Mathf.Clamp01((dx + dy + 87f) / 165f);
            Color c = Color.clear;
            if (d <= 60f)
            {
                if (d > 55f) c = Color.Lerp(new Color32(166, 107, 36, 255),
                    new Color32(255, 221, 125, 255), shine);
                else if (d > 50f) c = new Color32(77, 56, 41, 255);
                else if (d > 46f) c = Color.Lerp(new Color32(202, 152, 67, 255),
                    new Color32(255, 218, 125, 255), shine);
                else c = Color.Lerp(new Color32(36, 51, 91, 255),
                    new Color32(64, 87, 143, 255), shine);
                // Soft anti-aliasing only on the outer circular edge.
                c.a *= Mathf.Clamp01((60.5f - d) / 1.25f);
            }
            tex.SetPixel(x, y, c);
        }
        tex.Apply(false, true);
        coinSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
        return coinSprite;
    }
}
