using TMPro;
using UnityEngine;
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
    private bool overlayWasActive;
    private bool boardControlsWasActive;

    [Header("Temporary Local Profile")]
    [SerializeField] private string profileName = "PLAYER";
    [SerializeField] private int profileCash = 1500;
    [SerializeField] private int profileGold = 100;

    private string currentLobbyMode = "PLAY";

    private void Awake()
    {
        ResolveGameplayUIReferences();
        CaptureGameplayUIState();
        HideExistingGameUI();
        RefreshProfile();
        ShowMainMenu();
    }

    private void Update()
    {
        if (!WasEscapePressedThisFrame())
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
        currentLobbyMode = "PLAY";
        OpenLobby();
    }

    public void OpenPrivateLobby()
    {
        currentLobbyMode = "PRIVATE TABLE";
        OpenLobby();
    }

    public void OpenShop()
    {
        ShowModal(
            "SHOP",
            "Shop foundation is ready.\n" +
            "Items, cosmetics and progression can be added later.");
    }

    public void OpenSettings()
    {
        ShowModal(
            "SETTINGS",
            "Audio, graphics, camera and quality settings " +
            "will be connected in the Settings phase.");
    }

    public void OpenProfile()
    {
        ShowModal(
            "PROFILE",
            $"{profileName}\nCash: {profileCash:N0}\nGold: {profileGold:N0}");
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
        SetActive(mainMenuPanel, true);
        SetActive(lobbyPanel, false);
        SetActive(modalPanel, false);
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
                gameObject.SetActive(false);
                return;
            }
        }

        // v1.1 deliberately does NOT expose the legacy Match Setup screen.
        // If mapping ever fails, stay in the new lobby and show a clear error.
        ShowModal(
            "START MATCH",
            "The new lobby could not start the match automatically.\n" +
            "The legacy setup screen was kept hidden.\n" +
            "Check the Console mapping message.");

        Debug.LogWarning(
            "AtlasBoard Main Menu v1.1 blocked the legacy Match Setup " +
            "screen because automatic mapping/start did not complete.");
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
            lobbyTitleText.text =
                currentLobbyMode;
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
                profileName;
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
            GetDropdownText(
                mapDropdown,
                "Turkey");

        selection.PlayerCount =
            GetPlayerCount();

        selection.RoundLimit =
            ParseDropdownInt(
                roundLimitDropdown,
                20);

        selection.ThemeId =
            ThemeDisplayNameToId(
                GetDropdownText(
                    themeDropdown,
                    "Classic Table"));

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
                GetDropdownText(
                    player1TypeDropdown,
                    "Human"),
                GetDropdownText(
                    player2TypeDropdown,
                    "Bot"),
                GetDropdownText(
                    player3TypeDropdown,
                    "Bot"),
                GetDropdownText(
                    player4TypeDropdown,
                    "Bot")
            };

        return selection;
    }

    private static string ThemeDisplayNameToId(
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "classic_table";
        }

        string normalized =
            displayName.Trim()
                .ToLowerInvariant();

        switch (normalized)
        {
            case "garden":
                return "garden";

            case "beach":
                return "beach";

            case "pavilion":
                return "pavilion";

            case "street":
                return "street";

            default:
                return "classic_table";
        }
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

        overlayWasActive =
            existingGameplayOverlayCanvas != null &&
            existingGameplayOverlayCanvas.activeSelf;

        boardControlsWasActive =
            boardControlsCanvas != null &&
            boardControlsCanvas.activeSelf;

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
            existingGameplayOverlayCanvas.SetActive(
                gameplayUIStateCaptured
                    ? overlayWasActive
                    : true);
        }

        if (boardControlsCanvas != null)
        {
            boardControlsCanvas.SetActive(
                gameplayUIStateCaptured
                    ? boardControlsWasActive
                    : true);
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
