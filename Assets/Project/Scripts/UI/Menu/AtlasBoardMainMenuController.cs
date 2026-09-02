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
    [SerializeField] private int profileCash = 1500;
    [SerializeField] private int profileGold = 100;

    private string currentLobbyMode = "PLAY";

    private void Awake()
    {
        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;

        ResolveGameplayUIReferences();
        CaptureGameplayUIState();
        HideExistingGameUI();
        RefreshProfile();
        ShowMainMenu();
    }

    private void OnDestroy()
    {
        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;
    }

    private void HandleLanguageChanged()
    {
        RefreshProfile();

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

        currentLobbyMode = "PUBLIC TABLE";
        OpenLobby();
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

        // Safety fallback if the private-lobby extension is missing.
        OpenLobby();
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
                profileCash,
                profileGold));
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
        SetMainMenuCanvasRendering(true);

        SetActive(mainMenuPanel, true);
        SetActive(lobbyPanel, false);
        SetActive(modalPanel, false);

        AtlasBoardPrivateLobbyUIController privateLobby =
            GetComponent<AtlasBoardPrivateLobbyUIController>();

        if (privateLobby != null)
        {
            privateLobby.NotifyMainMenuShown();
        }
    }

    public void ReturnOnlineMatchToLobby()
    {
        ResolveGameplayUIReferences();
        ResetReusableGameplaySession();
        SetMainMenuCanvasRendering(true);

        SetActive(mainMenuPanel, false);
        SetActive(lobbyPanel, true);
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
        ResolveGameplayUIReferences();
        ResetReusableGameplaySession();
        SetMainMenuCanvasRendering(true);

        SetActive(mainMenuPanel, true);
        SetActive(lobbyPanel, false);
        SetActive(modalPanel, false);

        SetActive(existingTabletCanvas, false);
        SetActive(existingGameplayOverlayCanvas, false);
        SetActive(boardControlsCanvas, false);
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

    private void OpenLobby()
    {
        SetActive(mainMenuPanel, false);
        SetActive(modalPanel, false);
        SetActive(lobbyPanel, true);

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
            profileCashText.text =
                $"$ {profileCash:N0}";
        }

        if (profileGoldText != null)
        {
            profileGoldText.text =
                $"G {profileGold:N0}";
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

    public bool BalancedDevelopment;
    public bool DoublesEnabled;
    public bool TripleDoublePenaltyEnabled;

    public string[] PlayerTypes;
}
