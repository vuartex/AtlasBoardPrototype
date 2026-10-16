using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared Career / Account presentation for ALL platforms.
/// Steam identity authentication is provider-specific; positioning, color,
/// profile HUD and navigation layout are common. Runtime UI only.
/// Does not write account, room, wallet or progression data.
/// </summary>
[DefaultExecutionOrder(16000)]
public sealed class AtlasBoardUniversalCareerTheme : MonoBehaviour
{
    private static AtlasBoardUniversalCareerTheme instance;
    private static readonly Color32 Violet = new Color32(91, 76, 201, 255);
    private static readonly Color32 VioletDark = new Color32(60, 48, 150, 255);
    private float nextScan;
    private float nextNavigationScan;
    private Button steamCareerBound;
    private Button steamContextBound;
    private RectTransform careerProfileHud;
    private TMP_Text hudName;
    private TMP_Text hudGold;
    private TMP_Text hudCoin;
    private TMP_Text hudInitial;
    private TMP_Text menuProfileName;
    private TMP_Text menuProfileGold;
    private TMP_Text menuProfileCoin;
    private TMP_Text menuAvatarInitial;
    private TMP_Text contextLabel;
    private Button settingsAccountTab;
    private Button careerAccountTab;
    private Button steamCardClickGuard;
    private Button cachedContextButton;
    private Button cachedControlsButton;
    private RectTransform cachedCareerRoot;
    private AtlasBoardPrivateLobbyUIController steamLobby;
    private TMP_Text steamHostSeatLabel;
    private PlayerHudPanel steamHostHud;
    private TMP_Text steamHostMatchLabel;
    private float nextSteamAliasResolveAt;
    private static readonly FieldInfo PrivateSeatNamesField =
        typeof(AtlasBoardPrivateLobbyUIController).GetField("privateSeatNames",
            BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo LocalIsHostField =
        typeof(AtlasBoardPrivateLobbyUIController).GetField("localIsHost",
            BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo LobbyRoomActiveField =
        typeof(AtlasBoardPrivateLobbyUIController).GetField("roomActive",
            BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo MatchHudNameField =
        typeof(PlayerHudPanel).GetField("playerNameText",
            BindingFlags.Instance | BindingFlags.NonPublic);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject host = new GameObject("AtlasBoard_Shared_Career_Account_UI");
        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardUniversalCareerTheme>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        if (now >= nextScan)
        {
            nextScan = now + 1.2f;
            ApplyCommonCareerColor();
            EnsureCareerProfileHud();
            FixAccountHeaderAndFooter();
            BindSteamProviderRoutes();
            RemoveLegacyCareerFootnote();
            ResolveProfileSource();
            EnsureStableProfileNames();
            ResolveSteamLocalSurfaces();
        }
        if (now >= nextNavigationScan)
        {
            nextNavigationScan = now + 0.4f;
            EnsureSharedAccountTabs();
        }
    }

    private void LateUpdate()
    {
        // Rebuilds can occur after our normal Update. Keep navigation complete
        // in the same frame instead of moving a floating button every 0.4 s.
        EnsureSharedAccountTabs();
        if (menuProfileName == null) ResolveProfileSource();
        if (menuProfileName != null &&
            menuProfileName.GetComponent<AtlasBoardStableProfileNameView>() == null)
            menuProfileName.gameObject.AddComponent<AtlasBoardStableProfileNameView>();
        CopyHudTexts();
        // Existing PlayerHudPanel/UnifiedProfilePresentation may refresh in Update.
        // Our child-only display does not mutate their authoritative identity data.
    }

    private static string L(string en, string tr, string es, string fr,
        string de, string ko, string ru)
    {
        return AtlasBoardL.R(en, tr, es, fr, de, ko, ru);
    }

    private static T FindScene<T>(string objectName) where T : Component
    {
        T[] items = Resources.FindObjectsOfTypeAll<T>();
        foreach (T item in items)
        {
            if (item != null && item.gameObject.scene.IsValid() &&
                string.Equals(item.name, objectName, StringComparison.Ordinal))
                return item;
        }
        return null;
    }

    private static Transform Descendant(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform child in root)
        {
            if (child.name == name) return child;
            Transform found = Descendant(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private static TMP_Text GetText(Transform root, string name)
    {
        Transform item = Descendant(root, name);
        return item != null ? item.GetComponent<TMP_Text>() : null;
    }

    private void ResolveProfileSource()
    {
        RectTransform source = FindScene<RectTransform>("ProfileCard");
        menuProfileName = source != null ? GetText(source, "ProfileName") : null;
        menuProfileGold = source != null ? GetText(source, "ProfileCash") : null;
        menuProfileCoin = source != null ? GetText(source, "ProfileGold") : null;
        menuAvatarInitial = source != null ? GetText(source, "AvatarInitial") : null;
    }

    private static void EnsureStableProfileNames()
    {
        // A second, separately constructed ProfileCard is used in Public
        // Lobby. Binding only the first card left its initial as the static
        // prefab "G" while its name correctly showed the Steam persona.
        // Reuse the identity scan that already runs at a bounded interval;
        // never add a second per-frame hierarchy search.
        string display = AtlasBoardCanonicalPlayerIdentity.CurrentDisplayName;
        string initial = string.IsNullOrWhiteSpace(display)
            ? string.Empty : display.Substring(0, 1).ToUpperInvariant();
        TMP_Text[] names = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (TMP_Text name in names)
        {
            if (name == null || !name.gameObject.scene.IsValid())
                continue;
            if (string.Equals(name.gameObject.name, "AvatarInitial", StringComparison.Ordinal))
            {
                // Only a local ProfileCard, not avatars belonging to remote
                // room members or bots, may use the local Steam/Auth initial.
                Transform parent = name.transform.parent;
                if (parent != null && parent.name == "ProfileCard" &&
                    !string.IsNullOrEmpty(initial) && name.text != initial)
                    name.text = initial;
                continue;
            }
            if (!string.Equals(name.gameObject.name, "ProfileName", StringComparison.Ordinal))
                continue;
            if (name.GetComponent<AtlasBoardStableProfileNameView>() == null)
                name.gameObject.AddComponent<AtlasBoardStableProfileNameView>();
        }
    }

    private static string WithoutLevelSuffix(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        const string suffix = " • Lv. ";
        int index = name.LastIndexOf(suffix, StringComparison.Ordinal);
        if (index < 0) return name;
        string remainder = name.Substring(index + suffix.Length).Trim();
        return int.TryParse(remainder, out int level) && level >= 1
            ? name.Substring(0, index)
            : name;
    }

    private void ApplyCommonCareerColor()
    {
        RectTransform ribbon = FindScene<RectTransform>("CareerRibbon");
        if (ribbon != null)
        {
            Image image = ribbon.GetComponent<Image>();
            if (image != null) image.color = Violet;
            Outline outline = ribbon.GetComponent<Outline>();
            if (outline != null) outline.effectColor = VioletDark;
        }
        RectTransform card = FindScene<RectTransform>("ProgressionCard");
        if (card == null) return;
        Transform label = Descendant(card, "Ribbon");
        if (label != null && label.TryGetComponent(out Image background))
            background.color = Violet;
        Transform plate = Descendant(card, "IllustrationPlate");
        if (plate != null && plate.TryGetComponent(out Image plateImage))
            plateImage.color = new Color(Violet.r / 255f, Violet.g / 255f,
                Violet.b / 255f, 0.18f);
        Transform glyph = Descendant(card, "IllustrationGlyph");
        if (glyph != null && glyph.TryGetComponent(out TMP_Text glyphText))
            glyphText.color = Violet;
    }

    private static RectTransform MakePanel(Transform parent, string name,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static TMP_Text MakeText(Transform parent, string name,
        Vector2 position, Vector2 size, float sizePt, FontStyles fontStyle,
        TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform),
            typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TMP_Text text = go.GetComponent<TMP_Text>();
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = sizePt;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private void EnsureCareerProfileHud()
    {
        RectTransform career = FindScene<RectTransform>("AtlasCareer");
        if (career == null)
        {
            careerProfileHud = null;
            hudName = hudGold = hudCoin = hudInitial = null;
            return;
        }

        if (careerProfileHud == null || !careerProfileHud.IsChildOf(career))
        {
            Transform previous = career.Find("CareerProfileHeader");
            if (previous != null)
            {
                Transform marker = previous.Find("CareerHudName");
                if (marker == null)
                    Destroy(previous.gameObject); // obsolete tiny right HUD
                else
                    careerProfileHud = previous as RectTransform;
            }
            if (careerProfileHud == null)
            {
                // New fixed-layout panel: same visual scale and ordering as
                // Main Menu, aligned to LEFT of Career on every platform.
                careerProfileHud = MakePanel(career, "CareerProfileHeader",
                    new Vector2(10f, -12f), new Vector2(365f, 122f),
                    new Color32(246, 241, 231, 255));
                RectTransform avatar = MakePanel(careerProfileHud, "CareerHudAvatar",
                    new Vector2(9f, -11f), new Vector2(96f, 96f),
                    new Color32(68, 153, 203, 255));
                hudInitial = MakeText(avatar, "CareerHudInitial",
                    Vector2.zero, new Vector2(96f, 96f), 43f,
                    FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
                hudName = MakeText(careerProfileHud, "CareerHudName",
                    new Vector2(120f, -10f), new Vector2(236f, 38f),
                    23f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft,
                    new Color32(61, 62, 66, 255));
                hudGold = MakeText(careerProfileHud, "CareerHudGold",
                    new Vector2(120f, -49f), new Vector2(236f, 30f),
                    19f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft,
                    new Color32(61, 62, 66, 255));
                hudCoin = MakeText(careerProfileHud, "CareerHudCoin",
                    new Vector2(120f, -85f), new Vector2(236f, 30f),
                    19f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft,
                    new Color32(61, 62, 66, 255));
            }
        }
        if (careerProfileHud == null) return;
        careerProfileHud.anchorMin = careerProfileHud.anchorMax = new Vector2(0f, 1f);
        careerProfileHud.pivot = new Vector2(0f, 1f);
        careerProfileHud.anchoredPosition = new Vector2(10f, -12f);
        careerProfileHud.sizeDelta = new Vector2(365f, 122f);
        careerProfileHud.localScale = Vector3.one;
        if (hudName == null) hudName = GetText(careerProfileHud, "CareerHudName");
        if (hudGold == null) hudGold = GetText(careerProfileHud, "CareerHudGold");
        if (hudCoin == null) hudCoin = GetText(careerProfileHud, "CareerHudCoin");
        if (hudInitial == null) hudInitial = GetText(careerProfileHud, "CareerHudInitial");
        RectTransform avatarRect = careerProfileHud.Find("CareerHudAvatar") as RectTransform;
        if (avatarRect != null)
        {
            avatarRect.anchoredPosition = new Vector2(9f, -11f);
            avatarRect.sizeDelta = new Vector2(96f, 96f);
        }
        AdjustHudText(hudName, new Vector2(120f, -10f), new Vector2(236f, 38f), 23f);
        AdjustHudText(hudGold, new Vector2(120f, -49f), new Vector2(236f, 30f), 19f);
        AdjustHudText(hudCoin, new Vector2(120f, -85f), new Vector2(236f, 30f), 19f);
        AdjustHudText(hudInitial, Vector2.zero, new Vector2(96f, 96f), 43f);
        CopyHudTexts();
    }

    private static void AdjustHudText(TMP_Text text, Vector2 position,
        Vector2 dimensions, float fontSize)
    {
        if (text == null) return;
        RectTransform rect = text.rectTransform;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        if (!Mathf.Approximately(text.fontSize, fontSize)) text.fontSize = fontSize;
    }

    private void CopyHudTexts()
    {
        if (careerProfileHud == null || !careerProfileHud.gameObject.activeInHierarchy)
            return;
        // Cached source fields: no hierarchy recursion or remote read per frame.
        if (menuProfileName == null) return;
        string steamName = AtlasBoardStableProfileNameView.ResolvedSteamPersona();
        string visibleName = string.IsNullOrEmpty(steamName)
            ? WithoutLevelSuffix(menuProfileName.text) : steamName;
        SetIfChanged(hudName, visibleName);
        if (!string.IsNullOrEmpty(steamName))
            SetIfChanged(hudInitial, steamName.Substring(0, 1).ToUpperInvariant());
        SetIfChanged(hudGold, menuProfileGold != null ? menuProfileGold.text : "Gold: 0");
        SetIfChanged(hudCoin, menuProfileCoin != null ? menuProfileCoin.text : "Atlas Coin: 0");
        if (string.IsNullOrEmpty(steamName))
            SetIfChanged(hudInitial, menuAvatarInitial != null ? menuAvatarInitial.text : "G");
    }

    // Provider-bound display aliases. We deliberately never modify PlayerGameState,
    // FirebaseAuth, RTDB, lobby snapshots, local save data or platform authority.
    // Only a locally controlled HOST seat gets this overlay. A remotely-owned
    // P1 must keep the authoritative remote player's name on this client.
    private void ResolveSteamLocalSurfaces()
    {
        if (string.IsNullOrEmpty(AtlasBoardStableProfileNameView.ResolvedSteamPersona()))
            return;
        if (Time.unscaledTime < nextSteamAliasResolveAt)
            return;
        nextSteamAliasResolveAt = Time.unscaledTime + 2f;

        if (steamLobby == null)
        {
            steamHostSeatLabel = null;
            steamLobby = UnityEngine.Object.FindAnyObjectByType<AtlasBoardPrivateLobbyUIController>(FindObjectsInactive.Include);
        }
        if (steamLobby != null && PrivateSeatNamesField != null &&
            LocalIsHostField != null && LobbyRoomActiveField != null &&
            LocalIsHostField.GetValue(steamLobby) is bool isHost && isHost &&
            LobbyRoomActiveField.GetValue(steamLobby) is bool roomActive && roomActive)
        {
            TMP_Text[] seatNames = PrivateSeatNamesField.GetValue(steamLobby) as TMP_Text[];
            TMP_Text firstSeat = seatNames != null && seatNames.Length > 0 ? seatNames[0] : null;
            if (firstSeat != null && firstSeat != steamHostSeatLabel)
            {
                steamHostSeatLabel = firstSeat;
                AtlasBoardStableProfileNameView guard =
                    firstSeat.GetComponent<AtlasBoardStableProfileNameView>();
                if (guard == null)
                    guard = firstSeat.gameObject.AddComponent<AtlasBoardStableProfileNameView>();
                guard.ConfigureAliasSurface(
                    AtlasBoardStableProfileNameView.AliasSurface.LocalHostLobbySeat);
            }
        }

        if (MatchHudNameField == null) return;
        if (steamHostHud == null || steamHostMatchLabel == null)
        {
            steamHostHud = null;
            steamHostMatchLabel = null;
            PlayerHudPanel[] panels = UnityEngine.Object.FindObjectsByType<PlayerHudPanel>(FindObjectsInactive.Include);
            foreach (PlayerHudPanel panel in panels)
            {
                PlayerGameState player = panel != null ? panel.Player : null;
                if (player == null || player.PlayerSlotIndex != 0 ||
                    !player.IsParticipating || player.IsOnlineBotControlled) continue;
                // When an online seat mirror exists, don't relabel a remote host.
                if (player.OnlineSeatStateActive) continue;
                steamHostHud = panel;
                steamHostMatchLabel = MatchHudNameField.GetValue(panel) as TMP_Text;
                break;
            }
        }
        if (steamHostMatchLabel != null &&
            steamHostMatchLabel.GetComponent<AtlasBoardStableProfileNameView>() == null)
        {
            AtlasBoardStableProfileNameView guard =
                steamHostMatchLabel.gameObject.AddComponent<AtlasBoardStableProfileNameView>();
            guard.ConfigureAliasSurface(
                AtlasBoardStableProfileNameView.AliasSurface.LocalHostMatchHud);
        }
    }

    private static void SetIfChanged(TMP_Text label, string value)
    {
        if (label != null && label.text != value) label.text = value;
    }

    private void FixAccountHeaderAndFooter()
    {
        RectTransform window = FindScene<RectTransform>("AccountWindow");
        if (window == null) return;
        Transform header = window.Find("Header");
        if (header != null)
        {
            if (header.GetSiblingIndex() != window.childCount - 1)
                header.SetAsLastSibling();
            RectTransform close = header.Find("Button_CloseAccount") as RectTransform;
            if (close != null)
            {
                close.anchorMin = close.anchorMax = new Vector2(1f, 1f);
                close.pivot = new Vector2(0.5f, 0.5f);
                close.anchoredPosition = new Vector2(-53f, -52f);
                close.sizeDelta = new Vector2(65f, 65f);
            }
        }
        Transform form = window.Find("FormRoot");
        if (form == null || form.Find("Button_ResetPassword") == null) return;
        TMP_Text status = GetText(window, "Status");
        if (status == null || status.transform.parent != window) return;
        if (status.rectTransform.anchoredPosition != new Vector2(0f, -303f))
            status.rectTransform.anchoredPosition = new Vector2(0f, -303f);
        if (status.rectTransform.sizeDelta != new Vector2(840f, 42f))
            status.rectTransform.sizeDelta = new Vector2(840f, 42f);
        if (!status.enableAutoSizing) status.enableAutoSizing = true;
        if (status.fontSizeMin != 15f) status.fontSizeMin = 15f;
        if (status.fontSizeMax != 19f) status.fontSizeMax = 19f;
    }

    private void EnsureSharedAccountTabs()
    {
        if (cachedContextButton == null)
            cachedContextButton = FindScene<Button>("Button_AccountContext");
        Button original = cachedContextButton;
        if (original != null)
        {
            // AccountAccessUI owns authentication behavior and can reactivate
            // this historical launcher. Hide its presentation permanently;
            // the native-look navigation tabs below invoke its action.
            CanvasGroup hidden = original.GetComponent<CanvasGroup>();
            if (hidden == null) hidden = original.gameObject.AddComponent<CanvasGroup>();
            if (hidden.alpha != 0f) hidden.alpha = 0f;
            if (hidden.interactable) hidden.interactable = false;
            if (hidden.blocksRaycasts) hidden.blocksRaycasts = false;
        }

        if (settingsAccountTab != null && careerAccountTab != null &&
            !settingsAccountTab.gameObject.activeInHierarchy &&
            !careerAccountTab.gameObject.activeInHierarchy)
            return;

        EnsureSettingsAccountTab(original);
        EnsureCareerAccountTab(original);
    }

    private void EnsureSettingsAccountTab(Button original)
    {
        if (cachedControlsButton == null)
            cachedControlsButton = FindScene<Button>("Tab_Controls");
        Button controls = cachedControlsButton;
        if (controls == null) return;
        Transform parent = controls.transform.parent;
        if (parent == null) return;
        string[] names = { "Tab_Audio", "Tab_Gameplay", "Tab_Graphics", "Tab_Controls" };
        for (int i = 0; i < names.Length; i++)
        {
            RectTransform tab = parent.Find(names[i]) as RectTransform;
            if (tab == null) return;
            // Six shared Settings tabs (Audio, Gameplay, Graphics, Controls,
            // Account, Server Status) always share the same banner geometry.
            Vector2 position = new Vector2(-550f + 220f * i, 325f);
            if (tab.anchoredPosition != position) tab.anchoredPosition = position;
            if (tab.sizeDelta != new Vector2(206f, 54f))
                tab.sizeDelta = new Vector2(206f, 54f);
        }
        if (settingsAccountTab == null || settingsAccountTab.transform.parent != parent)
        {
            Transform found = parent.Find("Tab_AccountAtlasBoard");
            settingsAccountTab = found != null ? found.GetComponent<Button>() : null;
            if (settingsAccountTab == null)
            {
                GameObject clone = Instantiate(controls.gameObject, parent, false);
                clone.name = "Tab_AccountAtlasBoard";
                settingsAccountTab = clone.GetComponent<Button>();
                settingsAccountTab.onClick.RemoveAllListeners();
                DisableClonedLocalization(clone);
                settingsAccountTab.onClick.AddListener(OpenSharedAccount);
            }
        }
        if (settingsAccountTab == null) return;
        RectTransform accountRect = settingsAccountTab.transform as RectTransform;
        if (accountRect.anchoredPosition != new Vector2(330f, 325f))
            accountRect.anchoredPosition = new Vector2(330f, 325f);
        if (accountRect.sizeDelta != new Vector2(206f, 54f))
            accountRect.sizeDelta = new Vector2(206f, 54f);
        TMP_Text label = settingsAccountTab.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            string desired = L("ACCOUNT", "HESAP", "CUENTA", "COMPTE", "KONTO", "계정", "АККАУНТ");
            if (label.text != desired) label.text = desired;
        }
        // A native tab must use exactly the same background/style as Controls.
        Image source = controls.GetComponent<Image>();
        Image target = settingsAccountTab.GetComponent<Image>();
        if (source != null && target != null)
        {
            if (target.sprite != source.sprite) target.sprite = source.sprite;
            if (target.type != source.type) target.type = source.type;
            // Inactive = brighter. Controls may be the selected (dark) tab.
            Color idleColor = source.color;
            foreach (string name in names)
            {
                Transform node = parent.Find(name);
                Image candidate = node != null ? node.GetComponent<Image>() : null;
                if (candidate != null &&
                    candidate.color.r + candidate.color.g + candidate.color.b >
                    idleColor.r + idleColor.g + idleColor.b)
                    idleColor = candidate.color;
            }
            if (target.color != idleColor) target.color = idleColor;
        }
    }

    private void EnsureCareerAccountTab(Button original)
    {
        if (cachedCareerRoot == null)
            cachedCareerRoot = FindScene<RectTransform>("AtlasCareer");
        RectTransform root = cachedCareerRoot;
        if (root == null) { careerAccountTab = null; return; }
        if (!root.gameObject.activeInHierarchy && careerAccountTab != null)
            return;
        RectTransform nav = root.Find("Navigation") as RectTransform;
        if (nav == null) return;
        // Reserve four equal slots in the SAME visible navigation banner.
        if (nav.sizeDelta.x != 1120f)
            nav.sizeDelta = new Vector2(1120f, nav.sizeDelta.y);
        // RebuildNavigation replaces its children using deferred Destroy.
        // The LAST three children are the freshly built page buttons.
        int count = nav.childCount;
        if (count < 3) return;
        RectTransform achievementTab = null;
        for (int i = 0; i < 3; i++)
        {
            RectTransform tab = nav.GetChild(count - 3 + i) as RectTransform;
            if (tab == null || tab.GetComponent<Button>() == null) return;
            Vector2 position = new Vector2(-390f + i * 260f, 0f);
            if (tab.anchoredPosition != position) tab.anchoredPosition = position;
            if (tab.sizeDelta != new Vector2(235f, 50f))
                tab.sizeDelta = new Vector2(235f, 50f);
            if (i == 2) achievementTab = tab;
        }
        // The legacy ProgressionUI destroys/rebuilds the three nav buttons on
        // every page change. Keep Account under ROOT (not Navigation) so it
        // survives, but position it INSIDE the Navigation banner visually.
        if (careerAccountTab == null || careerAccountTab.transform.parent != root)
        {
            Transform found = root.Find("Tab_CareerAccountAtlasBoard");
            careerAccountTab = found != null ? found.GetComponent<Button>() : null;
            if (careerAccountTab == null)
            {
                if (achievementTab == null) return;
                GameObject clone = Instantiate(achievementTab.gameObject, root, false);
                clone.name = "Tab_CareerAccountAtlasBoard";
                careerAccountTab = clone.GetComponent<Button>();
                careerAccountTab.onClick.RemoveAllListeners();
                DisableClonedLocalization(clone);
                careerAccountTab.onClick.AddListener(OpenSharedAccount);
            }
        }
        if (careerAccountTab == null) return;
        RectTransform rect = careerAccountTab.transform as RectTransform;
        if (rect.anchorMin != new Vector2(0.5f, 0.5f))
            rect.anchorMin = new Vector2(0.5f, 0.5f);
        if (rect.anchorMax != new Vector2(0.5f, 0.5f))
            rect.anchorMax = new Vector2(0.5f, 0.5f);
        if (rect.pivot != new Vector2(0.5f, 0.5f))
            rect.pivot = new Vector2(0.5f, 0.5f);
        if (rect.anchoredPosition != new Vector2(390f, 345f))
            rect.anchoredPosition = new Vector2(390f, 345f);
        if (rect.sizeDelta != new Vector2(235f, 50f))
            rect.sizeDelta = new Vector2(235f, 50f);
        TMP_Text label = careerAccountTab.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            string desired = L("ACCOUNT", "HESAP", "CUENTA", "COMPTE", "KONTO", "계정", "АККАУНТ");
            if (label.text != desired) label.text = desired;
        }
        // Match the ordinary unselected tabs, not a special purple button.
        Image image = careerAccountTab.GetComponent<Image>();
        if (image != null && image.color != new Color32(28, 157, 211, 255))
            image.color = new Color32(28, 157, 211, 255);
    }

    private static void DisableClonedLocalization(GameObject root)
    {
        // Do not let a cloned Controls/Achievements localization binding
        // overwrite ACCOUNT just before Canvas rendering.
        if (root == null) return;
        AtlasBoardLocalizedText[] bindings =
            root.GetComponentsInChildren<AtlasBoardLocalizedText>(true);
        foreach (AtlasBoardLocalizedText binding in bindings)
            if (binding != null) binding.enabled = false;
    }

    private void OpenSharedAccount()
    {
        if (IsSteamSelected())
        {
            ShowSteamAccountNotice();
            return;
        }
        Button source = FindScene<Button>("Button_AccountContext");
        if (source != null) source.onClick.Invoke();
    }

    private static bool IsSteamSelected()
    {
        AtlasBoardPlatformRuntime provider = AtlasBoardPlatformRuntime.Instance;
        return provider != null && provider.IsSteamProviderActive;
    }

    private void BindSteamProviderRoutes()
    {
        Button career = FindScene<Button>("ProgressionCard");
        if (career == null) return;
        Transform old = career.transform.Find("AtlasBoardSteamCareerClickGuard");
        if (old != null) steamCardClickGuard = old.GetComponent<Button>();
        if (!IsSteamSelected())
        {
            if (steamCardClickGuard != null)
                steamCardClickGuard.gameObject.SetActive(false);
            return;
        }
        if (steamCardClickGuard == null)
        {
            GameObject overlay = new GameObject("AtlasBoardSteamCareerClickGuard",
                typeof(RectTransform), typeof(Image), typeof(Button));
            overlay.transform.SetParent(career.transform, false);
            RectTransform rect = overlay.transform as RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image transparent = overlay.GetComponent<Image>();
            transparent.color = new Color(1f, 1f, 1f, 0f);
            transparent.raycastTarget = true;
            steamCardClickGuard = overlay.GetComponent<Button>();
            steamCardClickGuard.onClick.AddListener(OpenSteamCareer);
        }
        steamCardClickGuard.gameObject.SetActive(true);
        steamCardClickGuard.transform.SetAsLastSibling();
    }

    private void OpenSteamCareer()
    {
        AtlasBoardProgressionUI progression =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardProgressionUI>(
                FindObjectsInactive.Include);
        if (progression == null) return;
        MethodInfo method = typeof(AtlasBoardProgressionUI).GetMethod(
            "OpenCareer", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) return;
        try { method.Invoke(progression, null); }
        catch (Exception exception)
        {
            Debug.LogWarning("ATB_STEAM_CAREER_OPEN failedType=" +
                exception.GetType().Name);
        }
    }

    private static void RemoveLegacyCareerFootnote()
    {
        RectTransform career = FindScene<RectTransform>("AtlasCareer");
        if (career == null) return;
        Transform content = career.Find("ContentPanel");
        if (content == null) return;
        Transform oldSteam = content.Find("SteamCareerProviderNotice");
        if (oldSteam != null) Destroy(oldSteam.gameObject);
        Transform oldRtdb = content.Find("RtdbCareerDataNotice");
        if (oldRtdb != null) Destroy(oldRtdb.gameObject);
    }

    private void ShowSteamAccountNotice()
    {
        // The shared Account canvas (sortingOrder=1700) stays above Settings
        // and Career. The prior version attached this to an invisible/low-z
        // progression canvas and showed it behind Settings.
        Canvas canvas = FindScene<Canvas>("AtlasBoard_Account_Access_Canvas");
        if (canvas == null) return;
        Transform previous = canvas.transform.Find("SteamProviderAccountModal");
        if (previous != null) Destroy(previous.gameObject);
        RectTransform backdrop = MakePanel(canvas.transform,
            "SteamProviderAccountModal", Vector2.zero, Vector2.zero,
            new Color(0f, 0f, 0f, 0.65f));
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.pivot = new Vector2(0.5f, 0.5f);
        backdrop.offsetMin = Vector2.zero;
        backdrop.offsetMax = Vector2.zero;
        Image blocker = backdrop.GetComponent<Image>();
        blocker.raycastTarget = true;
        GameObject card = new GameObject("SteamAccountCard", typeof(RectTransform),
            typeof(Image));
        card.transform.SetParent(backdrop, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(920f, 440f);
        card.GetComponent<Image>().color = new Color32(247, 246, 241, 255);
        RectTransform header = MakePanel(card.transform, "Header",
            Vector2.zero, new Vector2(920f, 98f),
            new Color32(25, 101, 148, 255));
        header.anchorMin = header.anchorMax = new Vector2(0.5f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        TMP_Text title = MakeText(header, "SteamAccountTitle",
            new Vector2(100f, -15f), new Vector2(720f, 65f),
            36f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        title.text = L("STEAM ACCOUNT", "STEAM HESABI", "CUENTA DE STEAM",
            "COMPTE STEAM", "STEAM-KONTO", "STEAM 계정", "АККАУНТ STEAM");
        TMP_Text text = MakeText(card.transform, "SteamAccountDetails",
            new Vector2(65f, -135f), new Vector2(790f, 220f),
            24f, FontStyles.Normal, TextAlignmentOptions.Center,
            new Color32(54, 58, 72, 255));
        text.textWrappingMode = TextWrappingModes.Normal;
        text.text = L("Steam player", "Steam oyuncusu", "Jugador Steam",
            "Joueur Steam", "Steam-Spieler", "Steam 플레이어", "Игрок Steam") +
            ": " + (AtlasBoardPlatformRuntime.Instance != null
                ? AtlasBoardPlatformRuntime.Instance.SteamPersonaName : "") + "\n" +
            L("Steam sign-in is handled automatically by the provider when the trusted service is available. Email registration is not required. Offline play grants no cloud rewards.",
              "Güvenilir hizmet kullanılabilir olduğunda Steam girişi sağlayıcı tarafından otomatik gerçekleştirilir. E-posta kaydı gerekmez. Çevrimdışı oyun bulut ödülü vermez.",
              "Steam inicia sesión automáticamente con el servicio de confianza. No se requiere correo electrónico. El juego sin conexión no da recompensas.",
              "Steam se connecte automatiquement avec le service approuvé. Aucun e-mail requis. Le mode hors ligne n'accorde pas de récompenses.",
              "Steam meldet sich automatisch über den vertrauenswürdigen Dienst an. Keine E-Mail-Registrierung erforderlich. Offline gibt es keine Cloud-Belohnungen.",
              "신뢰할 수 있는 서비스에서 Steam 로그인이 자동으로 처리됩니다. 이메일 가입은 필요하지 않습니다. 오프라인에는 보상이 없습니다.",
              "Steam входит автоматически через доверенный сервис. Регистрация по почте не нужна. Офлайн-игра не дает наград.");
        GameObject close = new GameObject("CloseSteamAccount", typeof(RectTransform),
            typeof(Image), typeof(Button));
        close.transform.SetParent(card.transform, false);
        RectTransform closeRect = close.transform as RectTransform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0.5f);
        closeRect.anchoredPosition = new Vector2(0f, -175f);
        closeRect.sizeDelta = new Vector2(240f, 60f);
        close.GetComponent<Image>().color = Violet;
        Button button = close.GetComponent<Button>();
        button.targetGraphic = close.GetComponent<Image>();
        button.onClick.AddListener(() => Destroy(backdrop.gameObject));
        TMP_Text label = MakeText(close.transform, "CloseLabel",
            Vector2.zero, new Vector2(240f, 60f), 24f,
            FontStyles.Bold, TextAlignmentOptions.Center, Color.white);
        label.rectTransform.anchorMin = label.rectTransform.anchorMax =
            new Vector2(0.5f, 0.5f);
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.anchoredPosition = Vector2.zero;
        label.text = L("CLOSE", "KAPAT", "CERRAR", "FERMER", "SCHLIESSEN", "닫기", "ЗАКРЫТЬ");
        backdrop.SetAsLastSibling();
    }
}
