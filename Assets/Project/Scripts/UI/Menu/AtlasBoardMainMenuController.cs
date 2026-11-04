using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


public class AtlasBoardMainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject modalPanel;


    [Header("Modal")]
    [SerializeField] private TMP_Text modalTitle;
    [SerializeField] private TMP_Text modalBody;


    [Header("Profile")]
    [SerializeField] private TMP_Text profileNameText;
    [SerializeField] private TMP_Text profileCashText;
    [SerializeField] private TMP_Text profileGoldText;


    [Header("Lobby")]
    [SerializeField] private TMP_Text lobbyTitleText;
    [SerializeField] private TMP_Dropdown mapDropdown;
    [SerializeField] private TMP_Dropdown playerCountDropdown;
    [SerializeField] private TMP_Dropdown roundLimitDropdown;
    [SerializeField] private TMP_Dropdown themeDropdown;
    [SerializeField] private ToggleProxy balancedDevelopmentToggle;
    [SerializeField] private ToggleProxy doublesToggle;
    [SerializeField] private ToggleProxy tripleDoublePenaltyToggle;


    [SerializeField] private TMP_Dropdown player1TypeDropdown;
    [SerializeField] private TMP_Dropdown player2TypeDropdown;
    [SerializeField] private TMP_Dropdown player3TypeDropdown;
    [SerializeField] private TMP_Dropdown player4TypeDropdown;


    [SerializeField] private GameObject player3Row;
    [SerializeField] private GameObject player4Row;


    [Header("Existing Game UI")]
    [SerializeField] private GameObject existingMatchSetupCanvas;
    [SerializeField] private GameObject existingTabletCanvas;
    [SerializeField] private GameObject existingGameplayOverlayCanvas;


    [Header("Bridge")]
    [SerializeField] private AtlasBoardMatchSetupBridge matchSetupBridge;


    private GameObject boardControlsCanvas;
    private bool gameplayUIStateCaptured;
    private bool tabletWasActive;


    [Header("Temporary Local Profile")]
    [SerializeField] private string profileName = "PLAYER";
    // Meta wallet values must only come from an authenticated RTDB read.
    // Render zero during loading; only a verified RTDB wallet can replace it.
    [SerializeField] private int profileCash = 0;
    [SerializeField] private int profileGold = 0;


    private string currentLobbyMode = "PLAY";
    private Coroutine mainMenuReturnRoutine;
    private int navigationGeneration;

    // Customer-facing footer. The serialized scene still carries an old
    // prototype/version label; runtime presentation replaces it with a small
    // localized Atlas Board tip and a localized Main Menu caption.
    private TMP_Text mainMenuFooterText;
    private int mainMenuFooterTipIndex = -1;


    public int NavigationGeneration => navigationGeneration;
    public bool IsNavigationGenerationCurrent(int generation) =>
        generation == navigationGeneration;


    public void ApplyVerifiedMetaWallet(int gold, int atlasCoin)
    {
        if (gold < 0 || atlasCoin < 0) return;
        profileCash = gold;
        profileGold = atlasCoin;
        RefreshProfile();
    }


    public void ClearMetaWallet()
    {
        profileCash = 0;
        profileGold = 0;
        RefreshProfile();
    }


    private void Awake()
    {
        // Serialized scenes may still contain the old 1500/100 placeholders.
        // Explicitly discard them before the first profile render.
        profileCash = 0;
        profileGold = 0;
        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;


        ResolveGameplayUIReferences();
        // The runtime lobby dropdown may be recreated from an old 3-map scene.
        // Always register the premium entries before any room interaction.
        if (AtlasBoardPremiumMapDropdownLabels.EnsureOptions(mapDropdown))
            Debug.Log("ATB_MAP_CHOICES status=ready count=" + mapDropdown.options.Count, this);
        CaptureGameplayUIState();
        HideExistingGameUI();
        RefreshProfile();
        ShowMainMenuImmediate();
    }


    private void OnDestroy()
    {
        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;
    }


    private void HandleLanguageChanged()
    {
        AtlasBoardPremiumMapDropdownLabels.EnsureOptions(mapDropdown);
        RefreshProfile();
        RefreshMainMenuFooter(false);


        if (lobbyPanel != null &&
            lobbyPanel.activeSelf &&
            lobbyTitleText != null)
        {
            if (currentLobbyMode ==
                "PRIVATE TABLE")
            {
                lobbyTitleText.text =
                    AtlasBoardL.T(
                        "menu.private_table");
            }
            else if (currentLobbyMode ==
                     "PUBLIC TABLE")
            {
                lobbyTitleText.text =
                    AtlasBoardL.T(
                        "menu.public_table");
            }
            else
            {
                lobbyTitleText.text =
                    AtlasBoardL.T(
                        "menu.play");
            }
        }
    }


    private void Update()
    {
        Canvas ownerCanvas =
            GetComponent<Canvas>();


        if (ownerCanvas != null &&
            !ownerCanvas.enabled)
        {
            return;
        }


        if (!WasEscapePressedThisFrame())
        {
            return;
        }


        if (HasActiveEscapeBlocker())
        {
            return;
        }


        if (modalPanel != null &&
            modalPanel.activeSelf)
        {
            CloseModal();
            return;
        }


        if (lobbyPanel != null &&
            lobbyPanel.activeSelf)
        {
            ShowMainMenu();
        }
    }


    private static bool HasActiveEscapeBlocker()
    {
        AtlasBoardEscapeBlocker[] blockers =
            UnityEngine.Object.FindObjectsByType<AtlasBoardEscapeBlocker>(
                FindObjectsInactive.Include);


        foreach (AtlasBoardEscapeBlocker blocker in blockers)
        {
            if (blocker != null && blocker.gameObject.activeInHierarchy)
            {
                return true;
            }
        }


        return false;
    }


    private static bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }


    public void OpenPlayLobby()
    {
        AtlasBoardPublicLobbyBrowserController publicBrowser =
            GetComponent<AtlasBoardPublicLobbyBrowserController>();


        if (publicBrowser != null)
        {
            publicBrowser.HostPublicRoomFromMainMenu();
            return;
        }


        // Do not open the pre-networking 4-local-player lobby as a fallback.
        // A missing public-room controller is a real setup error, not an
        // invitation to silently enter obsolete local UI.
        Debug.LogError("ATB_ONLINE_LOBBY_BLOCKED public_controller_missing=true legacy_fallback=false");
        ShowModal("ONLINE ROOM UNAVAILABLE",
            "The online lobby could not be opened. Please restart and try again.");
    }


    public void OpenPrivateLobby()
    {
        currentLobbyMode = "PRIVATE TABLE";


        AtlasBoardPrivateLobbyUIController privateLobby =
            GetComponent<AtlasBoardPrivateLobbyUIController>();


        if (privateLobby != null)
        {
            privateLobby.ShowRoomEntryFromMainMenu();
            return;
        }


        // Never expose the obsolete local-player lobby on an online flow.
        Debug.LogError("ATB_ONLINE_LOBBY_BLOCKED private_controller_missing=true legacy_fallback=false");
        ShowModal("ONLINE ROOM UNAVAILABLE",
            "The private online lobby is unavailable. Please restart and try again.");
    }


    public void OpenPrivateLobbyAfterRoomChoice()
    {
        currentLobbyMode = "PRIVATE TABLE";
        OpenLobby();
    }


    public void OpenPublicLobbyAfterRoomChoice()
    {
        currentLobbyMode = "PUBLIC TABLE";
        OpenLobby();
    }


    public void OpenShop()
    {
        ShowModal(
            AtlasBoardL.T(
                "menu.shop"),
            AtlasBoardL.T(
                "menu.shop_body"));
    }


    public void OpenSettings()
    {
        ShowModal(
            AtlasBoardL.T(
                "settings.title"),
            AtlasBoardL.T(
                "settings.audio_title"));
    }


    public void OpenProfile()
    {
        string visibleProfileName =
            string.Equals(
                profileName,
                "PLAYER",
                System.StringComparison.OrdinalIgnoreCase)
                    ? AtlasBoardL.T(
                        "menu.player")
                    : profileName;


        ShowModal(
            AtlasBoardL.T(
                "menu.profile"),
            AtlasBoardL.T(
                "menu.profile_body",
                visibleProfileName,
                Mathf.Max(0, profileCash).ToString("N0"),
                Mathf.Max(0, profileGold).ToString("N0")));
    }


    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }


    public void ShowMainMenu()
    {
        BeginMainMenuReturn(false);
    }


    private void ShowMainMenuImmediate()
    {
        SetMainMenuCanvasRendering(true);
        SetActive(mainMenuPanel, true);
        SetActive(lobbyPanel, false);
        SetActive(modalPanel, false);
        RefreshMainMenuFooter(true);
    }


    private void BeginMainMenuReturn(bool resetGameplay)
    {
        // A second Back/Escape/menu click while a room leave/close is still in
        // flight used to let stale async callbacks reopen serialized lobby UI.
        // Keep one canonical transition and cover it with the existing loading
        // blocker until the online controllers have settled.
        if (mainMenuReturnRoutine != null) return;


        navigationGeneration++;
        int generation = navigationGeneration;
        AtlasBoardLoadingExperience.BeginMenuReturnReset();
        mainMenuReturnRoutine = StartCoroutine(
            MainMenuReturnRoutine(resetGameplay, generation));
    }


    private IEnumerator MainMenuReturnRoutine(bool resetGameplay, int generation)
    {
        AtlasBoardPrivateLobbyUIController privateLobby =
            GetComponent<AtlasBoardPrivateLobbyUIController>();
        AtlasBoardPublicLobbyBrowserController publicBrowser =
            GetComponent<AtlasBoardPublicLobbyBrowserController>();


        publicBrowser?.InvalidateForMainMenuReturn();
        privateLobby?.InvalidatePendingRoomOperations();
        privateLobby?.NotifyMainMenuShown();


        if (resetGameplay)
        {
            ResolveGameplayUIReferences();
            ResetReusableGameplaySession();
            SetActive(existingTabletCanvas, false);
            SetActive(existingGameplayOverlayCanvas, false);
            SetActive(boardControlsCanvas, false);
        }


        const float timeoutSeconds = 8f;
        float started = Time.unscaledTime;
        while (privateLobby != null &&
               !privateLobby.IsMainMenuTransitionSettled &&
               Time.unscaledTime - started < timeoutSeconds)
        {
            float elapsed = Time.unscaledTime - started;
            AtlasBoardLoadingExperience.SetMenuReturnResetProgress(
                Mathf.Lerp(0.18f, 0.88f, Mathf.Clamp01(elapsed / timeoutSeconds)));
            yield return null;
        }


        if (privateLobby != null && !privateLobby.IsMainMenuTransitionSettled)
        {
            Debug.LogWarning(
                "ATB_MENU_RESET timeout=true lobby_cleanup_pending=true generation=" +
                generation);
        }


        // Only the latest transition may reveal Main Menu. Old async lobby
        // callbacks carry an older generation and are discarded by their
        // controllers instead of reopening room UI.
        if (generation == navigationGeneration)
        {
            ShowMainMenuImmediate();
            Debug.Log("ATB_MENU_RESET stale_callbacks_blocked=true generation=" + generation);
        }


        AtlasBoardLoadingExperience.CompleteMenuReturnReset();
        mainMenuReturnRoutine = null;
    }


    public void ReturnOnlineMatchToLobby()
    {
        ResolveGameplayUIReferences();
        ResetReusableGameplaySession();
        SetMainMenuCanvasRendering(true);


        SetActive(mainMenuPanel, false);
        if (!HasModernLobbyScaffold())
        {
            Debug.LogError("ATB_MODERN_LOBBY missing_scaffold=true rematch_lobby_aborted=true");
            ShowMainMenuAfterActiveMatchExit();
            return;
        }
        SetActive(lobbyPanel, true);
        RestoreOnlineLobbyScaffold();
        SetActive(modalPanel, false);


        SetActive(existingTabletCanvas, false);
        SetActive(existingGameplayOverlayCanvas, false);
        SetActive(boardControlsCanvas, false);


        Debug.Log(
            "AtlasBoard synchronized online rematch returned this client " +
            "to the existing lobby without reloading the scene.",
            this);
    }


    public void ShowMainMenuAfterActiveMatchExit()
    {
        BeginMainMenuReturn(true);
    }


    private static void ResetReusableGameplaySession()
    {
        MatchSetupManager setup =
            FindSceneComponentIncludingInactive<MatchSetupManager>();
        setup?.ResetForNewMatchSession();


        TurnManager turn =
            FindSceneComponentIncludingInactive<TurnManager>();
        turn?.ResetForOnlineLobbySession();


        DiceVisualController dice =
            FindSceneComponentIncludingInactive<DiceVisualController>();
        dice?.ResetForNewMatchSession();


        PlayerHudPanel[] hudPanels =
            Resources.FindObjectsOfTypeAll<PlayerHudPanel>();
        foreach (PlayerHudPanel hud in hudPanels)
        {
            if (hud != null && hud.gameObject.scene.IsValid())
            {
                hud.Refresh(false);
            }
        }


        TileResolutionManager resolution =
            FindSceneComponentIncludingInactive<TileResolutionManager>();
        resolution?.ResetForNewMatchSession();


        SpecialTileManager special =
            FindSceneComponentIncludingInactive<SpecialTileManager>();
        special?.ResetForNewMatchSession();


        EventCardManager events =
            FindSceneComponentIncludingInactive<EventCardManager>();
        events?.ResetForNewMatchSession();


        AuctionManager auction =
            FindSceneComponentIncludingInactive<AuctionManager>();
        auction?.ResetForNewMatchSession();


        TradeManager trade =
            FindSceneComponentIncludingInactive<TradeManager>();
        trade?.ResetForNewMatchSession();


        MatchResultManager result =
            FindSceneComponentIncludingInactive<MatchResultManager>();
        result?.ResetForNewMatchSession();


        TabletUIManager tablet =
            FindSceneComponentIncludingInactive<TabletUIManager>();
        tablet?.ResetForNewMatchSession();


        PropertyDevelopmentManager development =
            FindSceneComponentIncludingInactive<PropertyDevelopmentManager>();
        development?.ResetAllDevelopmentsForNewMatch();
    }


    private static T FindSceneComponentIncludingInactive<T>()
        where T : Component
    {
        T[] all =
            Resources.FindObjectsOfTypeAll<T>();


        foreach (T item in all)
        {
            if (item != null &&
                item.gameObject.scene.IsValid())
            {
                return item;
            }
        }


        return null;
    }


    public void BackFromLobby()
    {
        ShowMainMenu();
    }


    public void CloseModal()
    {
        SetActive(modalPanel, false);
    }


    public void OnPlayerCountChanged(int _)
    {
        RefreshPlayerRows();
    }


    public void StartMatch()
    {
        if (currentLobbyMode == "PRIVATE TABLE" ||
            currentLobbyMode == "PUBLIC TABLE")
        {
            AtlasBoardPrivateLobbyUIController privateLobby =
                GetComponent<AtlasBoardPrivateLobbyUIController>();


            if (privateLobby != null &&
                privateLobby.HandleHostStartRequested())
            {
                return;
            }
        }


        StartMatchCore();
    }


    public void StartMatchAfterPrivateBackendAuthorization()
    {
        StartMatchCore();
    }


    public AtlasBoardLobbySelection GetCurrentLobbySelection()
    {
        return ReadLobbySelection();
    }


    private void StartMatchCore()
    {
        AtlasBoardLobbySelection selection =
            ReadLobbySelection();


        string premiumId = AtlasBoardRtdbMetaEconomy.MapEntitlementId(selection.MapId);
        // Joined remote humans may play the host-owned map without owning it.
        AtlasBoardLobbyRuntimeBridge mapBridge = GetComponent<AtlasBoardLobbyRuntimeBridge>();
        bool remoteParticipant = mapBridge != null && mapBridge.HasLobby &&
            mapBridge.CurrentSnapshot != null &&
            !string.Equals(mapBridge.CurrentSnapshot.HostAccountId,
                mapBridge.CurrentAccountId, System.StringComparison.Ordinal);
        if (!remoteParticipant && premiumId.Length > 0 &&
            !AtlasBoardRtdbMetaEconomy.IsPremiumMapOwnedForCurrentUid(premiumId))
        {
            ShowModal("MAP NOT OWNED", "Only the host who owns this country map can start it. Purchase the map in Atlas Store.");
            Debug.LogWarning("ATB_MAP_OWNER_REJECT map=" + selection.MapId + " ownerUidNotVerified=true");
            return;
        }


        ApplySelectedTheme(
            selection.ThemeId);


        Debug.Log(
            "Lobby selection: " +
            $"Mode={selection.Mode}, " +
            $"Map={selection.MapId}, " +
            $"Players={selection.PlayerCount}, " +
            $"Rounds={selection.RoundLimit}, " +
            $"Theme={selection.ThemeId}, " +
            $"Balanced={selection.BalancedDevelopment}, " +
            $"Doubles={selection.DoublesEnabled}, " +
            $"TriplePenalty={selection.TripleDoublePenaltyEnabled}");


        if (matchSetupBridge != null)
        {
            bool started =
                matchSetupBridge.TryStartExistingMatch(
                    selection);


            if (started)
            {
                RestoreGameplayUIForMatch();


                // Phase 5 networking lives on Canvas_MainMenu components.
                // Hide rendering/raycasting but keep the GameObject active so
                // Firebase lobby/match bridges and the online coordinator can
                // continue polling during gameplay.
                SetMainMenuCanvasRendering(false);
                return;
            }
        }


        // v1.1 deliberately does NOT expose the legacy Match Setup screen.
        // If mapping ever fails, stay in the new lobby and show a clear error.
        ShowModal(
            AtlasBoardL.T(
                "menu.start_match_error_title"),
            AtlasBoardL.T(
                "menu.start_match_error_body"));


        Debug.LogWarning(
            "AtlasBoard Main Menu v1.1 blocked the legacy Match Setup " +
            "screen because automatic mapping/start did not complete.");
    }


    private void SetMainMenuCanvasRendering(
        bool visible)
    {
        Canvas canvas =
            GetComponent<Canvas>();


        if (canvas != null)
        {
            canvas.enabled = visible;
        }


        GraphicRaycaster raycaster =
            GetComponent<GraphicRaycaster>();


        if (raycaster != null)
        {
            raycaster.enabled = visible;
        }
    }


    private void ApplySelectedTheme(
        string themeId)
    {
        EnvironmentThemeManager manager =
            FindAnyObjectByType<
                EnvironmentThemeManager>();


        if (manager == null)
        {
            Debug.LogWarning(
                "EnvironmentThemeManager was not found. " +
                "The match will start without applying the selected theme.");


            return;
        }


        if (!manager.ApplyThemeById(themeId))
        {
            Debug.LogWarning(
                $"Could not apply environment theme '{themeId}'.");
        }
    }


    // PrivateOnlineRoot owns the room code/online controls, but the actual
    // online settings and player cards are serialized siblings beneath Lobby.
    // Never disable all siblings: doing so leaves a blank room with only the
    // code and start buttons. Entry is authorized by the online controllers;
    // legacy local-player fallback is blocked at OpenPrivateLobby/OpenPlayLobby.
    private bool HasModernLobbyScaffold()
    {
        return lobbyPanel != null &&
            lobbyPanel.transform.Find("PrivateOnlineRoot") != null &&
            lobbyPanel.transform.Find("Panel_GameSettings") != null &&
            lobbyPanel.transform.Find("Panel_Players") != null;
    }


    private void RestoreOnlineLobbyScaffold()
    {
        if (!HasModernLobbyScaffold())
        {
            Debug.LogError("ATB_MODERN_LOBBY missing_scaffold=true legacy_fallback=false");
            return;
        }


        // These were deactivated by the previous blanket-hiding regression.
        // They are the SAME live online room widgets (used by the private-room
        // controller), not a separate legacy lobby.
        string[] requiredChildren =
        {
            "LobbyModeTitle", "Panel_GameSettings", "Panel_Players",
            "Button_Back", "Button_StartMatch", "PrivateOnlineRoot"
        };
        foreach (string childName in requiredChildren)
        {
            Transform child = lobbyPanel.transform.Find(childName);
            if (child != null && !child.gameObject.activeSelf)
                child.gameObject.SetActive(true);
        }


        Debug.Log("ATB_MODERN_LOBBY scaffold_restored=true settings=true players=true legacy_fallback=false");
    }


    private void OpenLobby()
    {
        // Do not expose an obsolete local lobby if required online UI is missing.
        if (!HasModernLobbyScaffold())
        {
            Debug.LogError("ATB_MODERN_LOBBY missing_scaffold=true legacy_fallback=false");
            ShowModal("ONLINE ROOM UNAVAILABLE",
                "The online room layout is incomplete. Please reopen the game.");
            return;
        }
        // New rooms start on a free map, never inherit previous owner's paid map.
        if (mapDropdown != null) { mapDropdown.SetValueWithoutNotify(0); mapDropdown.RefreshShownValue(); }
        SetActive(mainMenuPanel, false);
        SetActive(modalPanel, false);
        SetActive(lobbyPanel, true);
        RestoreOnlineLobbyScaffold();


        if (lobbyTitleText != null)
        {
            if (currentLobbyMode == "PRIVATE TABLE")
            {
                lobbyTitleText.text = AtlasBoardL.T("menu.private_table");
            }
            else if (currentLobbyMode == "PUBLIC TABLE")
            {
                lobbyTitleText.text = AtlasBoardL.T("menu.public_table");
            }
            else
            {
                lobbyTitleText.text = AtlasBoardL.T("menu.play");
            }
        }


        RefreshPlayerRows();
    }


    // Shared account-entitlement notice for offline, private and public
    // selectors. Do not silently discard a premium map selection.
    public void ShowPremiumMapNotOwned(string mapId)
    {
        string locale = AtlasBoardLocalizationManager.Instance != null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";
        string mapName = AtlasBoardShopContentDraft.LocalizedName(
            mapId == "Mexico" || mapId == "map_mexico" ? "map_mexico" :
            mapId == "World" || mapId == "map_world" ? "map_world" :
            "map_germany", locale);
        string title;
        string message;
        switch ((locale ?? "en").ToLowerInvariant())
        {
            case "tr": title = "HARİTA SATIN ALINMAMIŞ";
                message = mapName + " haritasına sahip değilsiniz. Bu haritada oda kurmak için Atlas Mağaza'dan satın alın."; break;
            case "es": title = "MAPA NO ADQUIRIDO";
                message = "No tienes el mapa " + mapName + ". Cómpralo en la Tienda Atlas para crear una partida."; break;
            case "fr": title = "CARTE NON ACHETÉE";
                message = "Vous ne possédez pas la carte " + mapName + ". Achetez-la dans la boutique Atlas pour créer une partie."; break;
            case "de": title = "KARTE NICHT GEKAUFT";
                message = "Du besitzt die Karte " + mapName + " nicht. Kaufe sie im Atlas-Shop, um ein Spiel zu erstellen."; break;
            case "ko": title = "보유하지 않은 지도";
                message = mapName + " 지도를 보유하고 있지 않습니다. Atlas 상점에서 구매한 뒤 방을 만드세요."; break;
            case "ru": title = "КАРТА НЕ КУПЛЕНА";
                message = "У вас нет карты " + mapName + ". Купите её в магазине Atlas, чтобы создать игру."; break;
            default: title = "MAP NOT OWNED";
                message = "You don't own " + mapName + ". Purchase this map in Atlas Store before creating a game."; break;
        }
        if (mapId == "World" || mapId == "map_world")
        {
            title = AtlasBoardShopContentDraft.WorldText("locked", locale);
            switch (AtlasBoardLocalizationLanguages.Normalize(locale))
            {
                case "tr": message = "Dünya haritası hesabınızda bulunmuyor. Atlas Dünya Yolculuğu etkinliğindeki 8 ödülü tamamlayarak kazanabilirsiniz."; break;
                case "es": message = "No tienes el mapa Mundo. Desbloquéalo completando las ocho recompensas del evento Atlas."; break;
                case "fr": message = "Vous ne possédez pas la carte Monde. Débloquez-la en terminant les huit récompenses Atlas."; break;
                case "de": message = "Du besitzt die Weltkarte nicht. Schalte sie mit allen acht Atlas-Eventbelohnungen frei."; break;
                case "ko": message = "세계 지도가 없습니다. 아틀라스 이벤트 보상 8개를 모두 완료하면 잠금 해제됩니다."; break;
                case "ru": message = "У вас нет карты Мира. Получите её за восемь наград события Атлас."; break;
                default: message = "You don't own the World map. Unlock it by claiming all eight Atlas World Journey event rewards."; break;
            }
        }
        ShowModal(title, message);
        if (modalPanel != null) modalPanel.transform.SetAsLastSibling();
    }


    private void ShowModal(
        string title,
        string body)
    {
        if (modalTitle != null)
        {
            modalTitle.text = title;
        }


        if (modalBody != null)
        {
            modalBody.text = body;
        }


        SetActive(modalPanel, true);
    }


    private void RefreshMainMenuFooter(bool advanceTip)
    {
        if (mainMenuFooterText == null)
        {
            TMP_Text[] labels = mainMenuPanel != null
                ? mainMenuPanel.GetComponentsInChildren<TMP_Text>(true)
                : GetComponentsInChildren<TMP_Text>(true);

            foreach (TMP_Text label in labels)
            {
                if (label != null &&
                    string.Equals(
                        label.gameObject.name,
                        "FooterVersion",
                        System.StringComparison.Ordinal))
                {
                    mainMenuFooterText = label;
                    break;
                }
            }

            if (mainMenuFooterText == null && mainMenuPanel != null)
            {
                foreach (TMP_Text label in
                         GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label != null &&
                        string.Equals(
                            label.gameObject.name,
                            "FooterVersion",
                            System.StringComparison.Ordinal))
                    {
                        mainMenuFooterText = label;
                        break;
                    }
                }
            }
        }

        if (mainMenuFooterText == null)
        {
            return;
        }

        if (advanceTip || mainMenuFooterTipIndex < 0)
        {
            mainMenuFooterTipIndex =
                (mainMenuFooterTipIndex + 1) % 4;
        }

        string tip =
            LocalizedMainMenuTip(mainMenuFooterTipIndex);
        string caption =
            LocalizedMainMenuCaption();

        mainMenuFooterText.text =
            tip + "\n<b>" + caption + "</b>";
        mainMenuFooterText.enableAutoSizing = true;
        mainMenuFooterText.fontSizeMin = 11f;
        mainMenuFooterText.fontSizeMax = 18f;
        mainMenuFooterText.textWrappingMode =
            TextWrappingModes.Normal;
        mainMenuFooterText.overflowMode =
            TextOverflowModes.Truncate;
        mainMenuFooterText.alignment =
            TextAlignmentOptions.Center;

        RectTransform footerRect =
            mainMenuFooterText.rectTransform;
        if (footerRect != null)
        {
            footerRect.sizeDelta =
                new Vector2(
                    Mathf.Max(
                        footerRect.sizeDelta.x,
                        760f),
                    68f);
        }
    }

    private static string LocalizedMainMenuCaption()
    {
        string language =
            AtlasBoardLocalizationManager.Instance != null
                ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
                : "en";

        switch ((language ?? "en").ToLowerInvariant())
        {
            case "tr": return "ANA MENÜ";
            case "es": return "MENÚ PRINCIPAL";
            case "fr": return "MENU PRINCIPAL";
            case "de": return "HAUPTMENÜ";
            case "ko": return "메인 메뉴";
            case "ru": return "ГЛАВНОЕ МЕНЮ";
            default: return "MAIN MENU";
        }
    }

    private static string LocalizedMainMenuTip(int index)
    {
        int tipIndex = Mathf.Abs(index) % 4;
        string language =
            AtlasBoardLocalizationManager.Instance != null
                ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
                : "en";

        switch ((language ?? "en").ToLowerInvariant())
        {
            case "tr":
                return tipIndex switch
                {
                    0 => "Şehirlerini geliştir, ekonomini büyüt ve en yüksek net değere ulaş.",
                    1 => "Açık artırmalar doğru zamanda güçlü bir yatırıma dönüşebilir.",
                    2 => "Dengeli geliştirme, aynı mülk grubunda planlı büyümeyi ödüllendirir.",
                    _ => "Etkinlikler, ticaret ve geliştirmeler her maçı farklı bir stratejiye dönüştürür."
                };
            case "es":
                return tipIndex switch
                {
                    0 => "Desarrolla tus ciudades, haz crecer tu economía y termina con el mayor patrimonio.",
                    1 => "Una subasta en el momento adecuado puede convertirse en tu mejor inversión.",
                    2 => "El desarrollo equilibrado premia el crecimiento planificado de un grupo de propiedades.",
                    _ => "Eventos, intercambios y mejoras pueden cambiar por completo la estrategia de cada partida."
                };
            case "fr":
                return tipIndex switch
                {
                    0 => "Développez vos villes, renforcez votre économie et terminez avec la plus grande valeur nette.",
                    1 => "Une enchère au bon moment peut devenir votre meilleur investissement.",
                    2 => "Le développement équilibré récompense une croissance planifiée de tout un groupe de propriétés.",
                    _ => "Événements, échanges et améliorations peuvent transformer la stratégie de chaque partie."
                };
            case "de":
                return tipIndex switch
                {
                    0 => "Baue deine Städte aus, stärke deine Wirtschaft und erreiche das höchste Nettovermögen.",
                    1 => "Eine Auktion zum richtigen Zeitpunkt kann deine beste Investition werden.",
                    2 => "Ausgewogene Entwicklung belohnt geplantes Wachstum innerhalb einer Grundstücksgruppe.",
                    _ => "Ereignisse, Handel und Ausbauten können die Strategie jeder Partie verändern."
                };
            case "ko":
                return tipIndex switch
                {
                    0 => "도시를 개발하고 경제를 키워 가장 높은 순자산으로 경기를 마치세요.",
                    1 => "적절한 타이밍의 경매는 최고의 투자가 될 수 있습니다.",
                    2 => "균형 개발은 같은 부동산 그룹을 계획적으로 성장시킬 때 보상합니다.",
                    _ => "이벤트, 거래, 개발은 매 경기의 전략을 새롭게 바꿀 수 있습니다."
                };
            case "ru":
                return tipIndex switch
                {
                    0 => "Развивайте города, укрепляйте экономику и завершите матч с самым высоким капиталом.",
                    1 => "Аукцион в нужный момент может стать вашей лучшей инвестицией.",
                    2 => "Сбалансированное развитие поощряет планомерный рост всей группы собственности.",
                    _ => "События, сделки и улучшения могут полностью изменить стратегию каждого матча."
                };
            default:
                return tipIndex switch
                {
                    0 => "Develop your cities, grow your economy, and finish with the highest net worth.",
                    1 => "A well-timed auction can become your strongest investment.",
                    2 => "Balanced development rewards planned growth across a property group.",
                    _ => "Events, trades, and upgrades can reshape the strategy of every match."
                };
        }
    }

    private void RefreshProfile()
    {
        if (profileNameText != null)
        {
            profileNameText.text =
                string.Equals(
                    profileName,
                    "PLAYER",
                    System.StringComparison.OrdinalIgnoreCase)
                        ? AtlasBoardL.T(
                            "menu.player")
                        : profileName;
        }


        if (profileCashText != null)
        {
            profileCashText.text = $"Gold: {Mathf.Max(0, profileCash):N0}";
        }


        if (profileGoldText != null)
        {
            profileGoldText.text = $"Atlas Coin: {Mathf.Max(0, profileGold):N0}";
        }
    }


    private void RefreshPlayerRows()
    {
        int count =
            GetPlayerCount();


        SetActive(
            player3Row,
            count >= 3);


        SetActive(
            player4Row,
            count >= 4);
    }


    private AtlasBoardLobbySelection
        ReadLobbySelection()
    {
        AtlasBoardLobbySelection selection =
            new AtlasBoardLobbySelection();


        selection.Mode =
            currentLobbyMode;


        selection.MapId =
            GetMapIdByIndex(
                mapDropdown);


        selection.PlayerCount =
            GetPlayerCount();


        selection.RoundLimit =
            ParseDropdownInt(
                roundLimitDropdown,
                20);


        selection.ThemeId =
            GetThemeIdByIndex(
                themeDropdown);


        selection.BalancedDevelopment =
            balancedDevelopmentToggle == null ||
            balancedDevelopmentToggle.IsOn;


        selection.DoublesEnabled =
            doublesToggle == null ||
            doublesToggle.IsOn;


        selection.TripleDoublePenaltyEnabled =
            tripleDoublePenaltyToggle == null ||
            tripleDoublePenaltyToggle.IsOn;


        selection.PlayerTypes =
            new[]
            {
                GetPlayerTypeByIndex(
                    player1TypeDropdown,
                    "Human"),
                GetPlayerTypeByIndex(
                    player2TypeDropdown,
                    "Bot"),
                GetPlayerTypeByIndex(
                    player3TypeDropdown,
                    "Bot"),
                GetPlayerTypeByIndex(
                    player4TypeDropdown,
                    "Bot")
            };


        return selection;
    }


    private static string GetMapIdByIndex(
        TMP_Dropdown dropdown)
    {
        int index =
            dropdown != null
                ? dropdown.value
                : 0;


        return index switch
        {
            1 => "Colorado",
            2 => "USA",
            3 => "Germany",
            4 => "Mexico",
            5 => "World",
            _ => "Turkey"
        };
    }


    private static string GetThemeIdByIndex(
        TMP_Dropdown dropdown)
    {
        int index =
            dropdown != null
                ? dropdown.value
                : 0;


        return index switch
        {
            1 => "garden",
            2 => "beach",
            3 => "pavilion",
            4 => "street",
            _ => "classic_table"
        };
    }


    private static string GetPlayerTypeByIndex(
        TMP_Dropdown dropdown,
        string fallback)
    {
        if (dropdown == null)
        {
            return fallback;
        }


        return dropdown.value == 1
            ? "Bot"
            : "Human";
    }


    private int GetPlayerCount()
    {
        return ParseDropdownInt(
            playerCountDropdown,
            2);
    }


    private static string GetDropdownText(
        TMP_Dropdown dropdown,
        string fallback)
    {
        if (dropdown == null ||
            dropdown.options == null ||
            dropdown.options.Count == 0 ||
            dropdown.value < 0 ||
            dropdown.value >= dropdown.options.Count)
        {
            return fallback;
        }


        return dropdown.options[
            dropdown.value].text;
    }


    private static int ParseDropdownInt(
        TMP_Dropdown dropdown,
        int fallback)
    {
        string value =
            GetDropdownText(
                dropdown,
                fallback.ToString());


        return int.TryParse(
            value,
            out int result)
                ? result
                : fallback;
    }


    private void ResolveGameplayUIReferences()
    {
        if (existingTabletCanvas == null)
        {
            existingTabletCanvas =
                FindSceneObjectByName(
                    "Canvas_TabletUI");
        }


        if (existingGameplayOverlayCanvas == null)
        {
            existingGameplayOverlayCanvas =
                FindSceneObjectByName(
                    "Canvas_UXOverlay");
        }


        if (boardControlsCanvas == null)
        {
            boardControlsCanvas =
                FindSceneObjectByName(
                    "Canvas_BoardControls");
        }
    }


    private void CaptureGameplayUIState()
    {
        if (gameplayUIStateCaptured)
        {
            return;
        }


        tabletWasActive =
            existingTabletCanvas != null &&
            existingTabletCanvas.activeSelf;


        gameplayUIStateCaptured = true;
    }


    private void RestoreGameplayUIForMatch()
    {
        ResolveGameplayUIReferences();


        if (existingTabletCanvas != null)
        {
            existingTabletCanvas.SetActive(
                gameplayUIStateCaptured
                    ? tabletWasActive
                    : true);
        }


        if (existingGameplayOverlayCanvas != null)
        {
            // Gameplay overlays are required once the match has started.
            // Do not restore the intentionally-hidden menu-time state.
            existingGameplayOverlayCanvas.SetActive(true);
        }


        if (boardControlsCanvas != null)
        {
            // ROLL / TRADE / SET live here. Match startup must win over
            // the FALSE state captured while the Main Menu was visible.
            boardControlsCanvas.SetActive(true);
        }


        Debug.Log(
            "Gameplay UI restored after Main Menu: " +
            $"Tablet={(existingTabletCanvas != null && existingTabletCanvas.activeSelf)}, " +
            $"UXOverlay={(existingGameplayOverlayCanvas != null && existingGameplayOverlayCanvas.activeSelf)}, " +
            $"BoardControls={(boardControlsCanvas != null && boardControlsCanvas.activeSelf)}.");
    }


    private static GameObject FindSceneObjectByName(
        string objectName)
    {
        GameObject[] all =
            Resources.FindObjectsOfTypeAll<
                GameObject>();


        foreach (GameObject item in all)
        {
            if (item == null ||
                !item.scene.IsValid() ||
                item.name != objectName)
            {
                continue;
            }


            return item;
        }


        return null;
    }


    private void HideExistingGameUI()
    {
        SetActive(
            existingMatchSetupCanvas,
            false);


        SetActive(
            existingTabletCanvas,
            false);


        SetActive(
            existingGameplayOverlayCanvas,
            false);
    }


    private static void SetActive(
        GameObject target,
        bool value)
    {
        if (target != null &&
            target.activeSelf != value)
        {
            target.SetActive(value);
        }
    }


#if UNITY_EDITOR
    public void EditorConfigure(
        GameObject newMainMenuPanel,
        GameObject newLobbyPanel,
        GameObject newModalPanel,
        TMP_Text newModalTitle,
        TMP_Text newModalBody,
        TMP_Text newProfileNameText,
        TMP_Text newProfileCashText,
        TMP_Text newProfileGoldText,
        TMP_Text newLobbyTitleText,
        TMP_Dropdown newMapDropdown,
        TMP_Dropdown newPlayerCountDropdown,
        TMP_Dropdown newRoundLimitDropdown,
        TMP_Dropdown newThemeDropdown,
        ToggleProxy newBalancedDevelopmentToggle,
        ToggleProxy newDoublesToggle,
        ToggleProxy newTripleDoublePenaltyToggle,
        TMP_Dropdown newPlayer1TypeDropdown,
        TMP_Dropdown newPlayer2TypeDropdown,
        TMP_Dropdown newPlayer3TypeDropdown,
        TMP_Dropdown newPlayer4TypeDropdown,
        GameObject newPlayer3Row,
        GameObject newPlayer4Row,
        GameObject newExistingMatchSetupCanvas,
        GameObject newExistingTabletCanvas,
        GameObject newExistingGameplayOverlayCanvas,
        AtlasBoardMatchSetupBridge newBridge)
    {
        mainMenuPanel =
            newMainMenuPanel;


        lobbyPanel =
            newLobbyPanel;


        modalPanel =
            newModalPanel;


        modalTitle =
            newModalTitle;


        modalBody =
            newModalBody;


        profileNameText =
            newProfileNameText;


        profileCashText =
            newProfileCashText;


        profileGoldText =
            newProfileGoldText;


        lobbyTitleText =
            newLobbyTitleText;


        mapDropdown =
            newMapDropdown;


        playerCountDropdown =
            newPlayerCountDropdown;


        roundLimitDropdown =
            newRoundLimitDropdown;


        themeDropdown =
            newThemeDropdown;


        balancedDevelopmentToggle =
            newBalancedDevelopmentToggle;


        doublesToggle =
            newDoublesToggle;


        tripleDoublePenaltyToggle =
            newTripleDoublePenaltyToggle;


        player1TypeDropdown =
            newPlayer1TypeDropdown;


        player2TypeDropdown =
            newPlayer2TypeDropdown;


        player3TypeDropdown =
            newPlayer3TypeDropdown;


        player4TypeDropdown =
            newPlayer4TypeDropdown;


        player3Row =
            newPlayer3Row;


        player4Row =
            newPlayer4Row;


        existingMatchSetupCanvas =
            newExistingMatchSetupCanvas;


        existingTabletCanvas =
            newExistingTabletCanvas;


        existingGameplayOverlayCanvas =
            newExistingGameplayOverlayCanvas;


        matchSetupBridge =
            newBridge;
    }
#endif
}


[System.Serializable]
public class AtlasBoardLobbySelection
{
    public string Mode;
    public string MapId;
    public int PlayerCount;
    public int RoundLimit;
    public string ThemeId;
    public string BoardThemeId = "default_board";


    public bool BalancedDevelopment;
    public bool DoublesEnabled;
    public bool TripleDoublePenaltyEnabled;


    public string[] PlayerTypes;
}