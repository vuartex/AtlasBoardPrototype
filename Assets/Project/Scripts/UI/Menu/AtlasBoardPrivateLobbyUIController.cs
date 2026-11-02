using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif








public class AtlasBoardPrivateLobbyUIController : MonoBehaviour
{
    [Header("Existing Screens")]
    [SerializeField] private GameObject mainMenuRoot;
    [SerializeField] private GameObject lobbyRoot;
    [SerializeField] private GameObject privateOnlineRoot;
    [SerializeField] private GameObject roomEntryOverlay;
    [SerializeField] private GameObject roomPanel;
    [SerializeField] private GameObject legacyStartButton;








    [Header("Entry Flow")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_InputField joinPasswordInput;
    [SerializeField] private Button joinRoomButton;
    [SerializeField] private Button cancelEntryButton;
    [SerializeField] private TMP_Text entryStatusText;








    [Header("Room Code")]
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text roomStateText;
    [SerializeField] private TMP_Text revisionText;
    [SerializeField] private Button codeVisibilityButton;
    [SerializeField] private TMP_Text codeVisibilityButtonText;
    [SerializeField] private Button copyCodeButton;
    [SerializeField] private TMP_Text copyCodeButtonText;








    [Header("Guest Ready")]
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;








    [Header("Lobby Safety Modal")]
    [SerializeField] private GameObject lobbySafetyOverlay;
    [SerializeField] private TMP_Text lobbySafetyTitleText;
    [SerializeField] private TMP_Text lobbySafetyBodyText;
    [SerializeField] private TMP_Text lobbySafetyCountdownText;
    [SerializeField] private Button lobbySafetyPrimaryButton;
    [SerializeField] private TMP_Text lobbySafetyPrimaryButtonText;
    [SerializeField] private Button lobbySafetySecondaryButton;
    [SerializeField] private TMP_Text lobbySafetySecondaryButtonText;








    [Header("Lobby Settings")]
    [SerializeField] private TMP_Dropdown playerCountDropdown;








    [Header("Game Settings Popup / Access")]
    [SerializeField] private GameObject gameSettingsOverlay;
    [SerializeField] private Button gameSettingsOpenButton;
    [SerializeField] private Button gameSettingsCloseButton;
    [SerializeField] private TMP_InputField lobbyPasswordInput;
    [SerializeField] private Button lobbyPasswordApplyButton;
    [SerializeField] private TMP_Text lobbyPasswordStateText;
    [SerializeField] private TMP_Text lobbyPasswordStatusText;
    [SerializeField] private TMP_Text lobbyPasswordValueText;
    [SerializeField] private Button lobbyPasswordVisibilityButton;
    [SerializeField] private TMP_Text lobbyPasswordVisibilityButtonText;
    [SerializeField] private Button lobbyPasswordCopyButton;
    [SerializeField] private TMP_Text lobbyPasswordCopyButtonText;
    [SerializeField] private TMP_Dropdown[] hostSettingsDropdowns;
    [SerializeField] private Toggle[] hostSettingsToggles;








    [Header("Existing Player Rows")]
    [SerializeField] private GameObject[] playerRows;
    [SerializeField] private GameObject[] legacyPlayerLabels;
    [SerializeField] private GameObject[] legacyPlayerStatuses;
    [SerializeField] private GameObject[] legacyPlayerDropdowns;








    [Header("Runtime Backend")]
    [SerializeField] private AtlasBoardLobbyRuntimeBridge runtimeBridge;








    [Header("Private Seat Presentation")]
    [SerializeField] private TMP_Text[] privateSeatNames;
    [SerializeField] private TMP_Text[] privateSeatStatuses;
    [SerializeField] private Button[] privateSeatAddButtons;
    [SerializeField] private GameObject[] privateSeatChoicePanels;
    [SerializeField] private Button[] privateSeatLocalButtons;
    [SerializeField] private Button[] privateSeatBotButtons;
    [SerializeField] private Button[] privateSeatRemoveButtons;








    private enum PrivateSeatMode
    {
        OpenOnline = 0,
        LocalHuman = 1,
        Bot = 2,
        RemoteHuman = 3
    }








    private enum LobbySafetyModalMode
    {
        None = 0,
        Notice = 1,
        KickConfirm = 2,
        KickedNotice = 3,
        StartCountdown = 4
    }








    private readonly PrivateSeatMode[] hostSeatModes =
    {
        PrivateSeatMode.LocalHuman,
        PrivateSeatMode.OpenOnline,
        PrivateSeatMode.OpenOnline,
        PrivateSeatMode.OpenOnline
    };








    private readonly int[] legacyControlValuesBeforePrivate =
        new int[4];








    private bool legacyControlValuesSaved;








    private readonly UnityEngine.Events.UnityAction[] seatAddActions =
        new UnityEngine.Events.UnityAction[4];
    private readonly UnityEngine.Events.UnityAction[] seatLocalActions =
        new UnityEngine.Events.UnityAction[4];
    private readonly UnityEngine.Events.UnityAction[] seatBotActions =
        new UnityEngine.Events.UnityAction[4];
    private readonly UnityEngine.Events.UnityAction[] seatRemoveActions =
        new UnityEngine.Events.UnityAction[4];








    private AtlasBoardMainMenuController mainMenuController;








    private bool eventsBound;
    private bool suppressSeatEvents;
    private bool privateMode;
    private bool roomActive;
    private bool localIsHost;
    private bool codeVisible;
    private bool lobbyPasswordVisible;
    private string lastKnownLobbyPassword = string.Empty;
    // Canonical lobby BoardThemeId: one persistent dropdown in the existing
    // More Settings panel. Never replicate Environment Theme or room creation.
    private string selectedBoardThemeId = "default_board";
    private AtlasBoardLobbyBoardThemePicker boardThemePicker;
    private bool backendBusy;
    private int navigationEpoch;


    // Presentation-only identity cache. Host migration can briefly project the
    // new host UID into both the old and authoritative host slots on one
    // client while the other client already has the canonical roster. Never
    // render the same host twice: preserve the last verified occupant of each
    // non-host slot until the backend provides its canonical identity again.
    private readonly string[] seatIdentityAccountCache = new string[4];
    private readonly string[] seatIdentityNameCache = new string[4];
    private string seatIdentityCacheLobbyId = string.Empty;
    private string lastDuplicateHostProjectionLog = string.Empty;


    public bool IsMainMenuTransitionSettled =>
        !backendBusy && !voluntaryLeaveInFlight && !hostCloseInFlight &&
        (runtimeBridge == null || !runtimeBridge.HasLobby) && !roomActive;


    public void InvalidatePendingRoomOperations()
    {
        navigationEpoch++;
        SetEntryButtonsInteractable(false);
        Debug.Log("ATB_LOBBY_NAV invalidated=true epoch=" + navigationEpoch, this);
    }








    // Host settings are edited optimistically in the local UI.
    // A short debounce batches rapid dropdown/toggle/seat edits into one
    // authoritative backend sync and prevents polling snapshots from
    // visually rolling the UI back while that sync is pending.
    private const float HostConfigurationDebounceSeconds = 0.45f;
    private bool hostConfigurationDirty;
    private bool hostConfigurationSyncQueued;
    private float hostConfigurationSyncAt;








    // Local transient UI state. Backend snapshots must not close this menu
    // unless the seat itself stopped being OpenOnline.
    private int openSeatChoiceIndex = -1;








    private string roomCode = string.Empty;
    private int settingsRevision = 1;
    private int readyForRevision;
    private int requiredHumanPlayers = 1;








    private AtlasLobbySnapshot backendSnapshot;
    private string backendLocalAccountId = string.Empty;








    private LobbySafetyModalMode lobbySafetyModalMode =
        LobbySafetyModalMode.None;








    private int pendingKickSlot = -1;
    private bool countdownActive;
    private Coroutine countdownCoroutine;
    private bool voluntaryLeaveInFlight;
    private bool hostCloseInFlight;
    private float nextGuestHostNameCheckAt;






    // Rematch can briefly surface a transport snapshot where the newly
    // promoted host UID is projected into both its real slot and the previous
    // host slot. The lobby must not trust that first frame. After returning
    // from a synchronized rematch we re-read the authoritative roster a few
    // times and rebuild the visible seat rows from the first stable/unique
    // snapshot. This is presentation recovery only; it does not change seat
    // ownership or write lobby state.
    private int rematchRosterRefreshEpoch;


    // After rematch verification succeeds, keep the verified account->name
    // mapping for the waiting lobby. The transport can briefly preserve the
    // correct AccountId while replaying the promoted host's DisplayName into
    // the old host seat. A later polling snapshot must not repaint that seat
    // as the local host again when the account identity itself did not change.
    private bool rematchVerifiedIdentityLockActive;
    private readonly string[] rematchVerifiedAccountBySlot = new string[4];
    private readonly string[] rematchVerifiedNameBySlot = new string[4];








    // The room-entry popup normally belongs to MainMenu. Public Rooms can
    // temporarily host the same popup so JOIN BY CODE behaves as an in-place
    // modal instead of navigating the player away from the browser.
    private Transform roomEntryHomeParent;
    private int roomEntryHomeSiblingIndex = -1;
    private GameObject roomEntryExternalRoot;








    private void Awake()
    {
        mainMenuController =
            GetComponent<AtlasBoardMainMenuController>();








        runtimeBridge =
            runtimeBridge != null
                ? runtimeBridge
                : GetComponent<AtlasBoardLobbyRuntimeBridge>();








        if (runtimeBridge == null)
        {
            runtimeBridge =
                gameObject.AddComponent<AtlasBoardLobbyRuntimeBridge>();
        }








        runtimeBridge.SnapshotChanged +=
            HandleBackendSnapshotChanged;








        runtimeBridge.LobbyAccessLost +=
            HandleLobbyAccessLost;








        ResolveExtendedOnlineUiReferences();
        BindRuntimeEvents();
        ResetPrivateState(false);
    }








    private void OnEnable()
    {
        BindRuntimeEvents();
    }








    private void OnDestroy()
    {
        if (runtimeBridge != null)
        {
            runtimeBridge.SnapshotChanged -=
                HandleBackendSnapshotChanged;








            runtimeBridge.LobbyAccessLost -=
                HandleLobbyAccessLost;
        }








        UnbindRuntimeEvents();
        AtlasBoardLocalizationManager.LanguageChanged -= RefreshLocalizedText;
    }








    private void Update()
    {
        // Cross-client guest label safety: only the host UID's member can
        // supply the host name. An unrelated local profile repaint must not
        // replace it with this device's Guest identity.
        if (roomActive && !localIsHost && backendSnapshot != null &&
            Time.unscaledTime >= nextGuestHostNameCheckAt)
        {
            nextGuestHostNameCheckAt = Time.unscaledTime + 1.0f;
            ReconcileGuestHostSeatName();
        }




        if (gameSettingsOverlay != null &&
            gameSettingsOverlay.activeSelf &&
            WasEscapePressedThisFrame())
        {
            CloseLobbyGameSettings();
            return;
        }








        if (roomEntryOverlay != null &&
            roomEntryOverlay.activeSelf &&
            WasEscapePressedThisFrame())
        {
            CancelRoomEntry();
            return;
        }








        if (roomActive &&
            lobbyRoot != null &&
            !lobbyRoot.activeSelf)
        {
            if (!localIsHost)
            {
                BeginRemoteVoluntaryLeave();
            }
            else
            {
                BeginHostCloseLobby();
            }








            return;
        }








        if (hostConfigurationSyncQueued &&
            roomActive &&
            localIsHost &&
            backendSnapshot != null &&
            !backendBusy &&
            Time.unscaledTime >= hostConfigurationSyncAt)
        {
            hostConfigurationSyncQueued = false;
            _ = SyncHostConfigurationToBackendAsync();
        }
    }








    public void EnterHostedPublicRoom(
        AtlasLobbyOperationResult result)
    {
        if (result == null ||
            !result.Success ||
            result.Snapshot == null)
        {
            return;
        }








        privateMode = true;
        roomActive = true;
        localIsHost = true;
        codeVisible = false;
        roomCode =
            result.RoomCode ?? string.Empty;
        backendLocalAccountId =
            runtimeBridge != null
                ? runtimeBridge.CurrentAccountId
                : string.Empty;








        hostConfigurationDirty = false;
        hostConfigurationSyncQueued = false;
        hostConfigurationSyncAt = 0f;
        openSeatChoiceIndex = -1;
        voluntaryLeaveInFlight = false;








        SetActive(
            roomEntryOverlay,
            false);








        mainMenuController ??=
            GetComponent<
                AtlasBoardMainMenuController>();








        mainMenuController
            ?.OpenPublicLobbyAfterRoomChoice();








        SetActive(
            privateOnlineRoot,
            true);








        SetActive(
            roomPanel,
            true);








        ApplyBackendSnapshot(
            result.Snapshot,
            result.RoomCode,
            backendLocalAccountId);








        Debug.Log(
            "AtlasBoard Phase 4B: public room entered through existing authoritative lobby UI. " +
            $"Lobby={result.Snapshot.LobbyId}, Code={result.RoomCode}.",
            this);
    }








    public void EnterJoinedPublicRoom(
        AtlasLobbyOperationResult result)
    {
        if (result == null || !result.Success || result.Snapshot == null)
        {
            return;
        }








        privateMode = true;
        roomActive = true;
        localIsHost = false;
        codeVisible = false;
        roomCode = result.RoomCode ?? string.Empty;
        backendLocalAccountId =
            runtimeBridge != null ? runtimeBridge.CurrentAccountId : string.Empty;
        hostConfigurationDirty = false;
        hostConfigurationSyncQueued = false;
        openSeatChoiceIndex = -1;








        SetActive(roomEntryOverlay, false);
        mainMenuController ??= GetComponent<AtlasBoardMainMenuController>();
        mainMenuController?.OpenPublicLobbyAfterRoomChoice();
        SetActive(privateOnlineRoot, true);
        SetActive(roomPanel, true);








        ApplyBackendSnapshot(
            result.Snapshot,
            result.RoomCode,
            backendLocalAccountId);
    }








    public async System.Threading.Tasks.Task EnterJoinedRoomFromExternalCodeAsync(
        AtlasLobbyOperationResult result)
    {
        if (result == null ||
            !result.Success ||
            result.Snapshot == null)
        {
            return;
        }








        privateMode = true;
        roomActive = true;
        localIsHost = false;
        codeVisible = false;
        roomCode =
            result.RoomCode ?? string.Empty;
        backendLocalAccountId =
            runtimeBridge != null
                ? runtimeBridge.CurrentAccountId
                : string.Empty;








        hostConfigurationDirty = false;
        hostConfigurationSyncQueued = false;
        hostConfigurationSyncAt = 0f;
        openSeatChoiceIndex = -1;








        SetActive(roomEntryOverlay, false);








        mainMenuController ??=
            GetComponent<AtlasBoardMainMenuController>();








        if (result.Snapshot.Visibility ==
            AtlasRoomVisibility.Public)
        {
            mainMenuController?
                .OpenPublicLobbyAfterRoomChoice();
        }
        else
        {
            mainMenuController?
                .OpenPrivateLobbyAfterRoomChoice();
        }








        SetActive(privateOnlineRoot, true);
        SetActive(roomPanel, true);








        ApplyBackendSnapshot(
            result.Snapshot,
            result.RoomCode,
            backendLocalAccountId);








        if (IsActiveMatchReconnectSnapshot(
                result.Snapshot))
        {
            await ResumeActiveMatchAfterReconnectAsync(
                result.Snapshot);
        }
    }








    public void ShowRoomEntryFromMainMenu()
    {
        RestoreRoomEntryHome();
        roomEntryExternalRoot = null;
        PrepareRoomEntryChooser();
    }








    public void ShowRoomEntryOverScreen(
        GameObject screenRoot)
    {
        if (roomEntryOverlay == null ||
            screenRoot == null)
        {
            ShowRoomEntryFromMainMenu();
            return;
        }








        CacheRoomEntryHome();








        roomEntryExternalRoot =
            screenRoot;








        roomEntryOverlay.transform.SetParent(
            screenRoot.transform,
            false);








        PrepareRoomEntryChooser();








        // The browser stays visible; this is a true modal over the current
        // screen, not a hidden navigation back to Main Menu.
        SetActive(screenRoot, true);
        roomEntryOverlay.transform.SetAsLastSibling();
    }








    private void PrepareRoomEntryChooser()
    {
        navigationEpoch++;
        rematchRosterRefreshEpoch++;
        ClearRematchVerifiedIdentityLock();
        privateMode = true;
        roomActive = false;
        localIsHost = false;
        backendSnapshot = null;
        backendLocalAccountId = string.Empty;
        hostConfigurationDirty = false;
        hostConfigurationSyncQueued = false;
        hostConfigurationSyncAt = 0f;
        openSeatChoiceIndex = -1;
        roomCode = string.Empty;
        codeVisible = false;
        lastKnownLobbyPassword = string.Empty;
        lobbyPasswordVisible = false;
        settingsRevision = 1;
        readyForRevision = 0;
        requiredHumanPlayers = 1;
        voluntaryLeaveInFlight = false;
        hostCloseInFlight = false;








        SetActive(gameSettingsOverlay, false);
        ResetLobbySafetyModalState();








        SaveLegacyControlValues();
        ResetHostSeatModes();








        SetActive(privateOnlineRoot, false);
        SetActive(roomPanel, false);
        SetActive(roomEntryOverlay, true);








        roomEntryOverlay?.transform.SetAsLastSibling();








        if (joinCodeInput != null)
        {
            joinCodeInput.text = string.Empty;
        }








        if (joinPasswordInput != null)
        {
            joinPasswordInput.text = string.Empty;
        }








        SetText(
            entryStatusText,
            T(
                "lobby.online.create_or_join",
                "Create a room or enter a 6-digit code."));


        // Main-menu soft reset intentionally invalidates and disables entry
        // controls while stale Create/Join callbacks drain. A fresh chooser
        // is a new navigation epoch, so explicitly re-arm only these entry
        // controls here. Without this, Create/Join/code/password stay disabled
        // after leaving a room even though Cancel/Local Offline still work.
        SetEntryButtonsInteractable(true);


        Debug.Log("ATB_PRIVATE_ENTRY rearmed=true epoch=" + navigationEpoch, this);


        RefreshLocalizedText();
    }








    private void CacheRoomEntryHome()
    {
        if (roomEntryOverlay == null ||
            roomEntryHomeParent != null)
        {
            return;
        }








        roomEntryHomeParent =
            roomEntryOverlay.transform.parent;








        roomEntryHomeSiblingIndex =
            roomEntryOverlay.transform.GetSiblingIndex();
    }








    private void RestoreRoomEntryHome()
    {
        if (roomEntryOverlay == null ||
            roomEntryHomeParent == null)
        {
            return;
        }








        roomEntryOverlay.transform.SetParent(
            roomEntryHomeParent,
            false);








        int sibling =
            Mathf.Clamp(
                roomEntryHomeSiblingIndex,
                0,
                Mathf.Max(
                    0,
                    roomEntryHomeParent.childCount - 1));








        roomEntryOverlay.transform.SetSiblingIndex(
            sibling);
    }








    private void LeaveExternalRoomEntryContext(
        bool hideExternalRoot)
    {
        GameObject external =
            roomEntryExternalRoot;








        roomEntryExternalRoot = null;








        RestoreRoomEntryHome();








        if (hideExternalRoot &&
            external != null)
        {
            SetActive(external, false);
        }
    }








    public void NotifyMainMenuShown()
    {
        if (roomEntryOverlay != null &&
            roomEntryOverlay.activeSelf)
        {
            // The chooser is intentionally allowed to stay when it was opened
            // from PRIVATE TABLE. ShowMainMenu can also be called from lobby
            // back/escape, in which case a room should be fully cleared.
            if (roomActive)
            {
                ResetPrivateState(false);
            }








            return;
        }








        if (roomActive && !localIsHost)
        {
            BeginRemoteVoluntaryLeave();
            return;
        }








        if (roomActive && localIsHost)
        {
            BeginHostCloseLobby();
            return;
        }








        if (roomActive || privateMode)
        {
            ResetPrivateState(false);
        }
    }








    public void NotifyActiveMatchClientDetached()
    {
        // The active-match backend already owns the seat transition. Do not call
        // lobbyLeave here: a started lobby is intentionally not leaveable through
        // the waiting-lobby endpoint. This only clears this client's lobby UI.
        ResetPrivateState(false);
    }








    private void ResolveExtendedOnlineUiReferences()
    {
        Transform[] all =
            GetComponentsInChildren<Transform>(true);








        Transform Find(string name) =>
            all.FirstOrDefault(
                item =>
                    item != null &&
                    item.name == name);








        if (joinPasswordInput == null)
        {
            joinPasswordInput =
                Find("Input_JoinPassword")
                    ?.GetComponent<TMP_InputField>();
        }








        if (gameSettingsOverlay == null)
        {
            gameSettingsOverlay =
                Find("LobbyGameSettingsOverlay")
                    ?.gameObject;
        }








        if (gameSettingsOpenButton == null)
        {
            gameSettingsOpenButton =
                Find("Button_LobbyGameSettingsOpen")
                    ?.GetComponent<Button>();
        }








        if (gameSettingsCloseButton == null)
        {
            gameSettingsCloseButton =
                Find("Button_CloseLobbyGameSettings")
                    ?.GetComponent<Button>();
        }








        if (lobbyPasswordInput == null)
        {
            lobbyPasswordInput =
                Find("Input_LobbyPassword")
                    ?.GetComponent<TMP_InputField>();
        }








        if (lobbyPasswordApplyButton == null)
        {
            lobbyPasswordApplyButton =
                Find("Button_ApplyLobbyPassword")
                    ?.GetComponent<Button>();
        }








        if (lobbyPasswordStateText == null)
        {
            lobbyPasswordStateText =
                Find("LobbyPasswordState")
                    ?.GetComponent<TMP_Text>();
        }








        if (lobbyPasswordStatusText == null)
        {
            lobbyPasswordStatusText =
                Find("LobbyPasswordStatus")
                    ?.GetComponent<TMP_Text>();
        }








        if (lobbyPasswordValueText == null)
        {
            lobbyPasswordValueText =
                Find("LobbyPasswordValue")
                    ?.GetComponent<TMP_Text>();
        }








        if (lobbyPasswordVisibilityButton == null)
        {
            lobbyPasswordVisibilityButton =
                Find("Button_LobbyPasswordVisibility")
                    ?.GetComponent<Button>();
        }








        if (lobbyPasswordVisibilityButtonText == null)
        {
            lobbyPasswordVisibilityButtonText =
                Find("Button_LobbyPasswordVisibility")
                    ?.GetComponentInChildren<TMP_Text>(true);
        }








        if (lobbyPasswordCopyButton == null)
        {
            lobbyPasswordCopyButton =
                Find("Button_CopyLobbyPassword")
                    ?.GetComponent<Button>();
        }








        if (lobbyPasswordCopyButtonText == null)
        {
            lobbyPasswordCopyButtonText =
                Find("Button_CopyLobbyPassword")
                    ?.GetComponentInChildren<TMP_Text>(true);
        }








        if (gameSettingsOverlay != null &&
            !roomActive)
        {
            gameSettingsOverlay.SetActive(false);
        }
    }








    private void BeginHostCloseLobby()
    {
        if (hostCloseInFlight)
        {
            return;
        }








        if (runtimeBridge == null || !runtimeBridge.HasLobby || !localIsHost)
        {
            ResetPrivateState(false);
            return;
        }








        hostCloseInFlight = true;
        roomActive = false;
        _ = CloseHostLobbyAndResetAsync();
    }








    private async System.Threading.Tasks.Task CloseHostLobbyAndResetAsync()
    {
        try
        {
            AtlasLobbyOperationResult result = await runtimeBridge.CloseLobbyAsync();
            if (!result.Success)
            {
                Debug.LogWarning(
                    "AtlasBoard host lobby close was not acknowledged: " +
                    result.TechnicalMessage,
                    this);
            }
        }
        finally
        {
            hostCloseInFlight = false;
            ResetPrivateState(false);
        }
    }








    private void EnsureBoardThemePicker()
    {
        if (gameSettingsOverlay == null) return;
        if (boardThemePicker == null)
        {
            boardThemePicker = gameSettingsOverlay.GetComponent<AtlasBoardLobbyBoardThemePicker>();
            if (boardThemePicker == null)
                boardThemePicker = gameSettingsOverlay.AddComponent<AtlasBoardLobbyBoardThemePicker>();
            boardThemePicker.Initialize(gameSettingsOverlay.transform,
                lobbyPasswordInput != null ? lobbyPasswordInput.transform : null,
                gameSettingsCloseButton, OnBoardThemeSelected);
        }
        boardThemePicker.SelectWithoutNotify(selectedBoardThemeId);
    }


    private async System.Threading.Tasks.Task RefreshBoardThemeOwnershipAsync()
    {
        string beforeUid = AtlasBoardRtdbMetaEconomy.CurrentUid;
        AtlasBoardMetaEconomySnapshot wallet = await AtlasBoardRtdbMetaEconomy.LoadAsync();
        if (boardThemePicker == null || gameSettingsOverlay == null ||
            !gameSettingsOverlay.activeInHierarchy ||
            beforeUid != AtlasBoardRtdbMetaEconomy.CurrentUid) return;
        bool ownAzure = false, ownMaple = false, ownAurora = false;
        if (wallet != null && wallet.WalletVerified)
            foreach (AtlasBoardStorefrontItem item in wallet.Items)
            {
                if (item.ItemType != "board_theme" || !item.Owned || item.PreviewOnly) continue;
                if (item.ItemId == "board_midnight_azure") ownAzure = true;
                if (item.ItemId == "board_ember_maple") ownMaple = true;
                if (item.ItemId == "board_arctic_aurora") ownAurora = true;
            }
        if (wallet != null && wallet.WalletVerified && localIsHost &&
            selectedBoardThemeId != "default_board" &&
            !(selectedBoardThemeId == "board_midnight_azure" && ownAzure) &&
            !(selectedBoardThemeId == "board_ember_maple" && ownMaple) &&
            !(selectedBoardThemeId == "board_arctic_aurora" && ownAurora))
        {
            selectedBoardThemeId = "default_board";
            AtlasBoardBoardThemeVisuals.ApplySelection(selectedBoardThemeId);
            QueueHostConfigurationSync();
        }
        boardThemePicker.Configure(localIsHost && wallet != null && wallet.WalletVerified,
            ownAzure, ownMaple, ownAurora, selectedBoardThemeId);
    }


    private void OnBoardThemeSelected(string requestedId)
    {
        if (!localIsHost || backendBusy || requestedId == selectedBoardThemeId) return;
        selectedBoardThemeId = requestedId;
        AtlasBoardBoardThemeVisuals.ApplySelection(requestedId);
        QueueHostConfigurationSync();
    }


    private void OpenLobbyGameSettings()
    {
        ResolveExtendedOnlineUiReferences();








        if (gameSettingsOverlay == null)
        {
            return;
        }








        gameSettingsOverlay.transform.SetAsLastSibling();








        if (lobbyPasswordInput != null)
        {
            lobbyPasswordInput.text = string.Empty;
            lobbyPasswordInput.interactable = localIsHost;
        }








        lobbyPasswordVisible = false;








        if (lobbyPasswordApplyButton != null)
        {
            lobbyPasswordApplyButton.gameObject.SetActive(localIsHost);
            lobbyPasswordApplyButton.interactable = localIsHost && !backendBusy;
        }








        SetText(lobbyPasswordStatusText, string.Empty);
        RefreshGameSettingsAccessUi();
        gameSettingsOverlay.SetActive(true);
        EnsureBoardThemePicker();
        _ = RefreshBoardThemeOwnershipAsync();
    }








    private void CloseLobbyGameSettings()
    {
        SetActive(gameSettingsOverlay, false);
    }








    private async void ApplyLobbyPassword()
    {
        if (!localIsHost || backendBusy || runtimeBridge == null || !runtimeBridge.HasLobby)
        {
            return;
        }








        string password = lobbyPasswordInput != null ? lobbyPasswordInput.text : string.Empty;
        backendBusy = true;
        if (lobbyPasswordApplyButton != null) lobbyPasswordApplyButton.interactable = false;
        SetText(
            lobbyPasswordStatusText,
            T("lobby.access.saving", "SAVING..."));








        try
        {
            AtlasLobbyOperationResult result =
                await runtimeBridge.UpdateLobbyPasswordAsync(password);








            if (!result.Success || result.Snapshot == null)
            {
                AtlasBoardFrontendErrorCodes.Report(
                    AtlasBoardFrontendErrorCodes.LobbyAccessUpdateFailed,
                    result.TechnicalMessage, "lobby_password");
                SetText(lobbyPasswordStatusText,
                    AtlasBoardFrontendErrorCodes.PublicMessage(
                        AtlasBoardFrontendErrorCodes.LobbyAccessUpdateFailed));
                return;
            }








            lastKnownLobbyPassword = password ?? string.Empty;
            lobbyPasswordVisible = false;








            ApplyBackendSnapshot(
                result.Snapshot,
                roomCode,
                runtimeBridge.CurrentAccountId);








            if (lobbyPasswordInput != null) lobbyPasswordInput.text = string.Empty;
            SetText(
                lobbyPasswordStatusText,
                T("lobby.access.saved", "ACCESS UPDATED"));
        }
        finally
        {
            backendBusy = false;
            if (lobbyPasswordApplyButton != null)
            {
                lobbyPasswordApplyButton.interactable = localIsHost;
            }
            RefreshHostStartAvailability();
        }
    }








    private void RefreshGameSettingsAccessUi()
    {
        bool locked = backendSnapshot != null && backendSnapshot.HasPassword;
        SetText(
            lobbyPasswordStateText,
            locked
                ? T("lobby.access.password_set", "PASSWORD PROTECTED")
                : T("lobby.access.open", "NO PASSWORD"));








        if (lobbyPasswordInput != null)
        {
            lobbyPasswordInput.interactable = localIsHost;
        }
        if (!locked)
        {
            lastKnownLobbyPassword = string.Empty;
            lobbyPasswordVisible = false;
        }








        if (lobbyPasswordApplyButton != null)
        {
            lobbyPasswordApplyButton.gameObject.SetActive(localIsHost);
        }








        RefreshLobbyPasswordDisplay();
    }








    private void RefreshLobbyPasswordDisplay()
    {
        bool locked = backendSnapshot != null && backendSnapshot.HasPassword;
        bool canReveal = localIsHost && locked && !string.IsNullOrWhiteSpace(lastKnownLobbyPassword);








        SetText(
            lobbyPasswordValueText,
            !locked
                ? T("lobby.access.open", "NO PASSWORD")
                : canReveal
                    ? (lobbyPasswordVisible
                        ? lastKnownLobbyPassword
                        : new string('•', Mathf.Max(6, lastKnownLobbyPassword.Length)))
                    : T("lobby.access.password_set", "PASSWORD PROTECTED"));








        SetActive(lobbyPasswordVisibilityButton != null ? lobbyPasswordVisibilityButton.gameObject : null, localIsHost && locked);
        SetActive(lobbyPasswordCopyButton != null ? lobbyPasswordCopyButton.gameObject : null, localIsHost && locked);








        if (lobbyPasswordVisibilityButton != null)
        {
            lobbyPasswordVisibilityButton.interactable = canReveal;
        }
        SetText(
            lobbyPasswordVisibilityButtonText,
            lobbyPasswordVisible
                ? T("lobby.online.hide", "HIDE")
                : T("lobby.online.show", "SHOW"));








        if (lobbyPasswordCopyButton != null)
        {
            lobbyPasswordCopyButton.interactable = canReveal;
        }
        SetText(lobbyPasswordCopyButtonText, T("lobby.online.copy", "COPY"));
    }








    private void ToggleLobbyPasswordVisibility()
    {
        if (!localIsHost || string.IsNullOrWhiteSpace(lastKnownLobbyPassword))
        {
            return;
        }








        lobbyPasswordVisible = !lobbyPasswordVisible;
        RefreshLobbyPasswordDisplay();
    }








    private void CopyLobbyPassword()
    {
        if (!localIsHost || string.IsNullOrWhiteSpace(lastKnownLobbyPassword))
        {
            return;
        }








        GUIUtility.systemCopyBuffer = lastKnownLobbyPassword;
        SetText(lobbyPasswordCopyButtonText, T("lobby.online.copied", "COPIED"));
        CancelInvoke(nameof(ResetLobbyPasswordCopyButtonText));
        Invoke(nameof(ResetLobbyPasswordCopyButtonText), 1.5f);
    }








    private void ResetLobbyPasswordCopyButtonText()
    {
        SetText(lobbyPasswordCopyButtonText, T("lobby.online.copy", "COPY"));
    }








    private void BeginRemoteVoluntaryLeave()
    {
        if (voluntaryLeaveInFlight)
        {
            return;
        }








        if (runtimeBridge == null ||
            !runtimeBridge.HasLobby ||
            localIsHost)
        {
            ResetPrivateState(false);
            return;
        }








        voluntaryLeaveInFlight = true;
        roomActive = false;
        _ = LeaveRemoteLobbyAndResetAsync();
    }








    private async System.Threading.Tasks.Task
        LeaveRemoteLobbyAndResetAsync()
    {
        try
        {
            AtlasLobbyOperationResult result =
                await runtimeBridge.LeaveLobbyAsync();








            if (!result.Success)
            {
                Debug.LogWarning(
                    "AtlasBoard voluntary lobby leave was not acknowledged: " +
                    result.TechnicalMessage,
                    this);
            }
        }
        finally
        {
            voluntaryLeaveInFlight = false;
            ResetPrivateState(false);
        }
    }








    private void CancelRoomEntry()
    {
        bool openedOverExternalScreen =
            roomEntryExternalRoot != null;








        SetActive(roomEntryOverlay, false);
        ResetPrivateState(false);








        if (openedOverExternalScreen)
        {
            LeaveExternalRoomEntryContext(
                false);
        }
    }








    private async void CreateRoomPreview()
    {
        if (!privateMode || backendBusy)
        {
            return;
        }








        int operationEpoch = navigationEpoch;
        backendBusy = true;
        SetEntryButtonsInteractable(false);








        SetText(
            entryStatusText,
            T(
                "lobby.online.backend_connecting",
                "CREATING ROOM ON BACKEND..."));








        try
        {
            AtlasBoardLobbySelection selection =
                GetCurrentLobbySelection();








            AtlasLobbyOperationResult result =
                await runtimeBridge.CreatePrivateRoomAsync(
                    selection);


            if (operationEpoch != navigationEpoch)
            {
                await DiscardStaleRoomResultAsync(result, true);
                Debug.Log("ATB_LOBBY_NAV stale_create_discarded=true", this);
                return;
            }








            if (!result.Success ||
                result.Snapshot == null)
            {
                if (result.ErrorLocalizationKey == "lobby.error.owner_room_limit")
                    ShowOwnedRoomLimitPopup();
                else
                    ShowEntryBackendError(result);
                return;
            }








            roomActive = true;
            localIsHost = true;
            codeVisible = false;
            roomCode = result.RoomCode ?? string.Empty;
            backendLocalAccountId = runtimeBridge.CurrentAccountId;








            SetActive(roomEntryOverlay, false);








            LeaveExternalRoomEntryContext(
                true);








            mainMenuController ??=
                GetComponent<AtlasBoardMainMenuController>();








            mainMenuController?.OpenPrivateLobbyAfterRoomChoice();








            SetActive(privateOnlineRoot, true);
            SetActive(roomPanel, true);








            ApplyBackendSnapshot(
                result.Snapshot,
                result.RoomCode,
                runtimeBridge.CurrentAccountId);








            Debug.Log(
                "AtlasBoard 3D.3B: real backend private room created. " +
                $"Lobby={result.Snapshot.LobbyId}, Code={result.RoomCode}, " +
                $"Account={runtimeBridge.CurrentAccountId}.",
                this);
        }
        finally
        {
            backendBusy = false;
            SetEntryButtonsInteractable(true);
        }
    }








    private async void JoinRoomPreview()
    {
        if (!privateMode || backendBusy)
        {
            return;
        }








        string code =
            joinCodeInput != null
                ? SanitizeCode(joinCodeInput.text)
                : string.Empty;








        if (code.Length != 6)
        {
            SetText(
                entryStatusText,
                T(
                    "lobby.online.invalid_code",
                    "Enter exactly 6 digits."));
            return;
        }








        int operationEpoch = navigationEpoch;
        backendBusy = true;
        SetEntryButtonsInteractable(false);








        SetText(
            entryStatusText,
            T(
                "lobby.online.backend_joining",
                "JOINING ROOM..."));








        try
        {
            string password =
                joinPasswordInput != null
                    ? joinPasswordInput.text
                    : string.Empty;








            AtlasLobbyOperationResult result =
                await runtimeBridge.JoinByCodeAsync(code, password);


            if (operationEpoch != navigationEpoch)
            {
                await DiscardStaleRoomResultAsync(result, false);
                Debug.Log("ATB_LOBBY_NAV stale_join_discarded=true", this);
                return;
            }








            if (!result.Success ||
                result.Snapshot == null)
            {
                ShowEntryBackendError(result);
                return;
            }








            roomActive = true;
            localIsHost = false;
            codeVisible = false;
            roomCode = code;
            backendLocalAccountId = runtimeBridge.CurrentAccountId;








            SetActive(roomEntryOverlay, false);








            LeaveExternalRoomEntryContext(
                true);








            mainMenuController ??=
                GetComponent<AtlasBoardMainMenuController>();








            mainMenuController?.OpenPrivateLobbyAfterRoomChoice();








            SetActive(privateOnlineRoot, true);
            SetActive(roomPanel, true);








            ApplyBackendSnapshot(
                result.Snapshot,
                code,
                runtimeBridge.CurrentAccountId);








            if (IsActiveMatchReconnectSnapshot(
                    result.Snapshot))
            {
                await ResumeActiveMatchAfterReconnectAsync(
                    result.Snapshot);
            }








            Debug.Log(
                "AtlasBoard 3D.3B: real backend room joined. " +
                $"Lobby={result.Snapshot.LobbyId}, Account={runtimeBridge.CurrentAccountId}.",
                this);
        }
        finally
        {
            backendBusy = false;
            SetEntryButtonsInteractable(true);
        }
    }








    private async System.Threading.Tasks.Task DiscardStaleRoomResultAsync(
        AtlasLobbyOperationResult result, bool createdByThisClient)
    {
        if (result == null || !result.Success || result.Snapshot == null ||
            runtimeBridge == null || !runtimeBridge.HasLobby) return;
        try
        {
            AtlasLobbyOperationResult cleanup = createdByThisClient
                ? await runtimeBridge.CloseLobbyAsync()
                : await runtimeBridge.LeaveLobbyAsync();
            if (cleanup == null || !cleanup.Success)
                Debug.LogWarning("ATB_LOBBY_NAV stale_cleanup_ack=false", this);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_LOBBY_NAV stale_cleanup=" + ex.GetType().Name, this);
        }
        finally
        {
            ResetPrivateState(false);
        }
    }






    private static bool IsActiveMatchReconnectSnapshot(
        AtlasLobbySnapshot snapshot)
    {
        return snapshot != null &&
               !string.IsNullOrWhiteSpace(snapshot.MatchId) &&
               (snapshot.LifecycleState ==
                    AtlasRoomLifecycleState.Starting ||
                snapshot.LifecycleState ==
                    AtlasRoomLifecycleState.InMatch);
    }








    private async System.Threading.Tasks.Task
        ResumeActiveMatchAfterReconnectAsync(
            AtlasLobbySnapshot snapshot)
    {
        if (snapshot == null ||
            string.IsNullOrWhiteSpace(snapshot.MatchId))
        {
            return;
        }








        PrepareLegacyControlsForOnlineMatchStart();








        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            GetComponent<
                AtlasBoardTurnDiceNetworkCoordinator>();








        coordinator?
            .PrepareForAuthoritativeMatchStart(
                snapshot);








        mainMenuController ??=
            GetComponent<
                AtlasBoardMainMenuController>();








        mainMenuController?
            .StartMatchAfterPrivateBackendAuthorization();








        AtlasBoardMatchRuntimeBridge matchRuntime =
            GetComponent<
                AtlasBoardMatchRuntimeBridge>();








        if (matchRuntime != null)
        {
            // PrepareForAuthoritativeMatchStart resets the stale local session
            // cache. Fetch once immediately so a returning player catches up to
            // the current round, pawn positions, money, ownership and decision
            // state instead of waiting on/being filtered by the old snapshot.
            AtlasMatchNetworkResult refresh =
                await matchRuntime
                    .RefreshSnapshotNowAsync(
                        snapshot.MatchId);








            if (!refresh.Success)
            {
                Debug.LogWarning(
                    "Active-match reclaim succeeded but the immediate " +
                    "authoritative catch-up snapshot failed: " +
                    refresh.TechnicalMessage,
                    this);
            }
        }








        SetText(
            roomStateText,
            T(
                "lobby.online.authoritative_start_ready",
                "START CONFIRMED"));
    }








    private void ConfigureHostLobbyPreview()
    {
        ShowPrivateSeatPresentation(true);








        if (backendSnapshot != null)
        {
            ApplyBackendSettingsToUi(backendSnapshot);
            LoadHostSeatModesFromBackend(backendSnapshot);
        }
        else
        {
            ResetHostSeatModes();
        }








        RestoreRowsFromPlayerCount();








        int seatChoiceToRestore =
            openSeatChoiceIndex;








        SyncHostSeatModesToLegacyControls();
        RefreshHostSeatRows();
        RestoreOpenSeatChoicePanelIfValid(
            seatChoiceToRestore);








        requiredHumanPlayers =
            backendSnapshot != null &&
            backendSnapshot.Settings != null
                ? backendSnapshot.Settings.RequiredHumanPlayers
                : CountResolvedLocalHumans();








        SetHostSettingsInteractable(
            backendSnapshot == null ||
            backendSnapshot.LifecycleState ==
            AtlasRoomLifecycleState.Waiting);








        SetActive(
            readyButton != null
                ? readyButton.gameObject
                : null,
            false);








        SetActive(legacyStartButton, true);
        RefreshHostStartAvailability();








        if (backendSnapshot == null)
        {
            SetText(
                roomStateText,
                T(
                    "lobby.online.preview_created",
                    "LOCAL UI PREVIEW • ROOM CREATED"));
        }
    }








    private void ConfigureGuestLobbyPreview()
    {
        ShowPrivateSeatPresentation(true);








        if (backendSnapshot != null)
        {
            ApplyBackendSettingsToUi(backendSnapshot);
        }








        SetHostSeatActionUIVisible(false);
        ForceAllPlayerRowsVisible();
        SetHostSettingsInteractable(false);








        SetActive(legacyStartButton, false);








        SetActive(
            readyButton != null
                ? readyButton.gameObject
                : null,
            true);








        SetText(
            roomStateText,
            T(
                "lobby.online.preview_joined",
                "LOCAL UI PREVIEW • JOINED AS GUEST"));
    }








    private void ToggleCodeVisibility()
    {
        if (!roomActive)
        {
            return;
        }








        codeVisible = !codeVisible;
        RefreshRoomHeader();
    }








    private void CopyRoomCode()
    {
        if (!roomActive ||
            string.IsNullOrWhiteSpace(roomCode))
        {
            return;
        }








        GUIUtility.systemCopyBuffer = roomCode;








        ConfigureRoomCodeActionText(
            copyCodeButtonText);








        SetText(
            copyCodeButtonText,
            T("lobby.online.copied", "COPIED"));








        CancelInvoke(nameof(ResetCopyButtonText));
        Invoke(nameof(ResetCopyButtonText), 1.5f);
    }








    private void ResetCopyButtonText()
    {
        ConfigureRoomCodeActionText(
            copyCodeButtonText);








        SetText(
            copyCodeButtonText,
            T("lobby.online.copy", "COPY"));
    }








    private static void ConfigureRoomCodeActionText(
        TMP_Text text)
    {
        if (text == null)
        {
            return;
        }








        // Some localized labels (for example Turkish "KOPYALA") are longer
        // than English "COPY". Keep the existing button size and let TMP fit
        // the label cleanly instead of overflowing.
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 18f;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;
        text.overflowMode =
            TextOverflowModes.Truncate;
    }








    private async void ToggleReady()
    {
        if (!roomActive ||
            localIsHost ||
            backendBusy ||
            runtimeBridge == null ||
            backendSnapshot == null)
        {
            return;
        }








        bool nextReady =
            readyForRevision != settingsRevision;








        backendBusy = true;








        if (readyButton != null)
        {
            readyButton.interactable = false;
        }








        try
        {
            AtlasLobbyOperationResult result =
                await runtimeBridge.SetReadyAsync(nextReady);








            if (!result.Success ||
                result.Snapshot == null)
            {
                ShowRoomBackendError(result);
                return;
            }








            ApplyBackendSnapshot(
                result.Snapshot,
                roomCode,
                runtimeBridge.CurrentAccountId);
        }
        finally
        {
            backendBusy = false;








            if (readyButton != null)
            {
                readyButton.interactable = true;
            }
        }
    }








    private void HandleHostSettingsDropdownChanged(int ignoredValue)
    {
        if (!roomActive ||
            !localIsHost ||
            suppressSeatEvents)
        {
            return;
        }








        int requestedPlayerCount =
            GetPlayerCount();








        if (backendSnapshot != null &&
            backendSnapshot.Settings != null &&
            requestedPlayerCount <
                backendSnapshot.Settings.MaxPlayers &&
            HasRemoteHumanInRemovedSlots(
                requestedPlayerCount))
        {
            suppressSeatEvents = true;








            try
            {
                SetDropdownByText(
                    playerCountDropdown,
                    backendSnapshot.Settings.MaxPlayers.ToString());
            }
            finally
            {
                suppressSeatEvents = false;
            }








            RestoreRowsFromPlayerCount();
            LoadHostSeatModesFromBackend(
                backendSnapshot);
            SyncHostSeatModesToLegacyControls();
            RefreshHostSeatRows();
            ShowPlayerCountBlockedModal();
            return;
        }








        // Paid country maps are host-only. A non-owned map may be visible
        // in the dropdown, but it must not become the active selection.
        if (hostSettingsDropdowns != null && hostSettingsDropdowns.Length > 0 &&
            hostSettingsDropdowns[0] != null)
        {
            int mapIndex = hostSettingsDropdowns[0].value;
            string requestedPremium = mapIndex == 3 ? "map_germany" :
                mapIndex == 4 ? "map_mexico" : string.Empty;
            if (requestedPremium.Length > 0 &&
                !AtlasBoardRtdbMetaEconomy.IsPremiumMapOwnedForCurrentUid(requestedPremium))
            {
                int priorIndex = 0;
                if (backendSnapshot != null && backendSnapshot.Settings != null)
                {
                    switch (backendSnapshot.Settings.MapId)
                    {
                        case "Colorado": priorIndex = 1; break;
                        case "USA": priorIndex = 2; break;
                        case "Germany": priorIndex = 3; break;
                        case "Mexico": priorIndex = 4; break;
                    }
                }
                suppressSeatEvents = true;
                try { SetDropdownByIndex(hostSettingsDropdowns[0], priorIndex); }
                finally { suppressSeatEvents = false; }
                mainMenuController ??= GetComponent<AtlasBoardMainMenuController>();
                mainMenuController?.ShowPremiumMapNotOwned(requestedPremium);
                Debug.LogWarning("ATB_MAP_OWNER_REJECT selected=" + requestedPremium +
                    " reason=not_owned_by_current_host popup=true", this);
                return;
            }
            // Preview an owned premium map while waiting for host settings
            // acknowledgement; the backend snapshot remains authoritative.
            if (requestedPremium.Length > 0)
                AtlasBoardPremiumMapDropdownLabels.PreviewMap(
                    mapIndex == 3 ? "Germany" : "Mexico");
        }


        RestoreRowsFromPlayerCount();
        ResetInactiveHostSeatModes();
        CloseAllSeatChoicePanels();
        SyncHostSeatModesToLegacyControls();
        RefreshHostSeatRows();








        requiredHumanPlayers =
            CountResolvedLocalHumans();








        QueueHostConfigurationSync();
    }








    private void HandleHostToggleChanged(bool ignoredValue)
    {
        if (suppressSeatEvents ||
            !roomActive ||
            !localIsHost)
        {
            return;
        }








        // Triple Double Penalty requires Doubles. If Doubles is turned OFF,
        // turn the dependent option OFF locally before the debounced backend
        // write so the whole settings update is not rejected.
        if (hostSettingsToggles != null &&
            hostSettingsToggles.Length > 2 &&
            hostSettingsToggles[1] != null &&
            hostSettingsToggles[2] != null &&
            !hostSettingsToggles[1].isOn &&
            hostSettingsToggles[2].isOn)
        {
            suppressSeatEvents = true;








            try
            {
                hostSettingsToggles[2]
                    .SetIsOnWithoutNotify(false);
            }
            finally
            {
                suppressSeatEvents = false;
            }
        }








        QueueHostConfigurationSync();
    }








    private void OpenSeatChoice(int index)
    {
        if (!roomActive ||
            !localIsHost ||
            index < 0 ||
            index >= GetPlayerCount() ||
            IsAuthoritativeHostSlot(index) ||
            hostSeatModes[index] != PrivateSeatMode.OpenOnline)
        {
            return;
        }


        CloseAllSeatChoicePanels();
        openSeatChoiceIndex = index;
        SetSeatTextVisible(index, false);
        SetActive(GetChoicePanel(index), true);


        Button add = GetAddButton(index);
        if (add != null)
        {
            add.gameObject.SetActive(false);
        }
    }


    private void AddLocalPlayerToSeat(int index)
    {
        ResolveSeat(
            index,
            PrivateSeatMode.LocalHuman);
    }








    private void AddBotToSeat(int index)
    {
        ResolveSeat(
            index,
            PrivateSeatMode.Bot);
    }








    private void RemoveLocalAssignmentFromSeat(int index)
    {
        if (!roomActive ||
            !localIsHost ||
            index < 0 ||
            index >= GetPlayerCount() ||
            IsAuthoritativeHostSlot(index))
        {
            return;
        }


        if (hostSeatModes[index] == PrivateSeatMode.RemoteHuman)
        {
            ShowKickConfirmation(index);
            return;
        }


        ResolveSeat(index, PrivateSeatMode.OpenOnline);
    }


    private void ResolveSeat(
        int index,
        PrivateSeatMode mode)
    {
        if (index < 0 ||
            index >= hostSeatModes.Length ||
            IsAuthoritativeHostSlot(index))
        {
            return;
        }


        hostSeatModes[index] = mode;
        CloseAllSeatChoicePanels();
        SyncHostSeatModesToLegacyControls();
        RefreshHostSeatRows();
        requiredHumanPlayers = CountResolvedLocalHumans();
        QueueHostConfigurationSync();
    }


    private void QueueHostConfigurationSync()
    {
        if (backendSnapshot == null)
        {
            AdvanceLocalSettingsRevision();
            RefreshHostStartAvailability();
            return;
        }








        if (!roomActive ||
            !localIsHost)
        {
            return;
        }








        hostConfigurationDirty = true;
        hostConfigurationSyncQueued = true;
        hostConfigurationSyncAt =
            Time.unscaledTime +
            HostConfigurationDebounceSeconds;








        // Do not allow Start while the visible host configuration has not yet
        // been acknowledged by the authoritative backend.
        RefreshHostStartAvailability();
    }








    private async System.Threading.Tasks.Task SyncHostConfigurationToBackendAsync()
    {
        if (backendBusy ||
            runtimeBridge == null ||
            backendSnapshot == null ||
            !localIsHost)
        {
            return;
        }








        backendBusy = true;
        SetHostSettingsInteractable(false);
        SetHostSeatActionUIVisible(false);








        try
        {
            AtlasBoardLobbySelection selection =
                GetCurrentLobbySelection();








            string[] policies =
                BuildBackendSeatPolicies(
                    selection.PlayerCount);








            AtlasLobbyOperationResult result =
                await runtimeBridge.SyncHostConfigurationAsync(
                    selection,
                    policies);








            if (!result.Success ||
                result.Snapshot == null)
            {
                ShowRoomBackendError(result);








                AtlasLobbyOperationResult refresh =
                    await runtimeBridge.GetSnapshotAsync();








                hostConfigurationDirty = false;
                hostConfigurationSyncQueued = false;








                if (refresh.Success &&
                    refresh.Snapshot != null)
                {
                    ApplyBackendSnapshot(
                        refresh.Snapshot,
                        roomCode,
                        runtimeBridge.CurrentAccountId);
                }








                return;
            }








            hostConfigurationDirty = false;
            hostConfigurationSyncQueued = false;








            ApplyBackendSnapshot(
                result.Snapshot,
                roomCode,
                runtimeBridge.CurrentAccountId);
        }
        finally
        {
            backendBusy = false;








            if (localIsHost &&
                backendSnapshot != null &&
                backendSnapshot.LifecycleState ==
                AtlasRoomLifecycleState.Waiting)
            {
                SetHostSettingsInteractable(true);








                int seatChoiceToRestore =
                    openSeatChoiceIndex;








                RefreshHostSeatRows();
                RestoreOpenSeatChoicePanelIfValid(
                    seatChoiceToRestore);
                RefreshHostStartAvailability();
            }
        }
    }








    public bool HandleHostStartRequested()
    {
        if (!roomActive ||
            !localIsHost ||
            backendSnapshot == null ||
            runtimeBridge == null)
        {
            return false;
        }








        if (!backendBusy)
        {
            _ = StartBackendMatchAsync();
        }








        return true;
    }








    private async System.Threading.Tasks.Task StartBackendMatchAsync()
    {
        backendBusy = true;








        Button start =
            legacyStartButton != null
                ? legacyStartButton.GetComponent<Button>()
                : null;








        if (start != null)
        {
            start.interactable = false;
        }








        SetText(
            roomStateText,
            T(
                "lobby.online.starting",
                "STARTING..."));








        try
        {
            AtlasLobbyOperationResult result =
                await runtimeBridge.StartMatchAsync();








            if (!result.Success ||
                result.Snapshot == null ||
                !result.Started)
            {
                ShowRoomBackendError(result);
                RefreshHostStartAvailability();
                return;
            }








            ApplyBackendSnapshot(
                result.Snapshot,
                roomCode,
                runtimeBridge.CurrentAccountId);








            Debug.Log(
                "AtlasBoard 3D.3B authoritative host Start accepted. " +
                $"MatchId={result.Snapshot.MatchId}, " +
                $"StartEventId={result.Snapshot.StartEventId}, " +
                $"Replay={result.IdempotentReplay}.",
                this);








            BeginAuthoritativeStartCountdown();
        }
        finally
        {
            backendBusy = false;
        }
    }








    private void HandleBackendSnapshotChanged(
        AtlasLobbySnapshot snapshot)
    {
        if (!roomActive ||
            snapshot == null)
        {
            return;
        }


        // A verified rematch roster can be correct for one poll and then have
        // only DisplayName regressed by a later transport projection while the
        // AccountId/slot stay correct. Normalize that presentation-only field
        // before equivalence checks or repainting.
        ApplyRematchVerifiedIdentityGuard(snapshot);


        if (snapshot.LifecycleState != AtlasRoomLifecycleState.Waiting &&
            rematchVerifiedIdentityLockActive)
        {
            ClearRematchVerifiedIdentityLock();
        }








        // The host's visible selection is temporarily authoritative while a
        // debounced write is pending/in flight. Polling is still allowed to
        // keep the bridge current, but an older backend snapshot must not
        // repaint Map / Player Count / Rules / seat choices over the edit the
        // host just made.
        if (localIsHost &&
            hostConfigurationDirty)
        {
            return;
        }








        // Polling is transport/state synchronization, not a reason to repaint
        // the UI every second. If nothing visible/authoritative changed, keep
        // the current local controls exactly as they are (including open
        // dropdowns and the + seat action menu).
        if (snapshot.Settings != null &&
            !string.Equals(selectedBoardThemeId,
                snapshot.Settings.BoardThemeId ?? "default_board", StringComparison.Ordinal))
        {
            selectedBoardThemeId = string.IsNullOrEmpty(snapshot.Settings.BoardThemeId)
                ? "default_board" : snapshot.Settings.BoardThemeId;
            AtlasBoardBoardThemeVisuals.ApplySelection(selectedBoardThemeId);
            if (boardThemePicker != null)
                boardThemePicker.SelectWithoutNotify(selectedBoardThemeId);
        }


        if (BackendSnapshotUiEquivalent(
                backendSnapshot,
                snapshot))
        {
            backendSnapshot = snapshot;
            return;
        }








        bool synchronizedRematchReturn =
            backendSnapshot != null &&
            (backendSnapshot.LifecycleState ==
                 AtlasRoomLifecycleState.Starting ||
             backendSnapshot.LifecycleState ==
                 AtlasRoomLifecycleState.InMatch) &&
            !string.IsNullOrWhiteSpace(backendSnapshot.MatchId) &&
            snapshot.LifecycleState ==
                AtlasRoomLifecycleState.Waiting &&
            string.IsNullOrWhiteSpace(snapshot.MatchId);








        ApplyBackendSnapshot(
            snapshot,
            roomCode,
            runtimeBridge != null
                ? runtimeBridge.CurrentAccountId
                : backendLocalAccountId);








        if (synchronizedRematchReturn)
        {
            mainMenuController ??=
                GetComponent<AtlasBoardMainMenuController>();








            mainMenuController?.ReturnOnlineMatchToLobby();


            // Do not trust the first rematch lobby projection. Host migration
            // can temporarily duplicate the promoted host UID into the old
            // host seat on one client. Re-read the backend roster after the
            // lobby has settled and rebuild all player rows from a stable,
            // unique authoritative snapshot.
            BeginRematchRosterVerification();
            return;
        }








        if (snapshot.LifecycleState ==
            AtlasRoomLifecycleState.Starting)
        {
            BeginAuthoritativeStartCountdown();
        }
    }








    private void BeginRematchRosterVerification()
    {
        ClearRematchVerifiedIdentityLock();
        int verificationEpoch = ++rematchRosterRefreshEpoch;
        int expectedNavigationEpoch = navigationEpoch;
        _ = VerifyAndRefreshRematchRosterAsync(
            verificationEpoch,
            expectedNavigationEpoch);
    }








    private async System.Threading.Tasks.Task VerifyAndRefreshRematchRosterAsync(
        int verificationEpoch,
        int expectedNavigationEpoch)
    {
        // Give the host-migration/rematch write enough time to settle, then
        // retry with progressively wider spacing. The first stable roster wins.
        int[] delaysMs = { 350, 650, 1000, 1500 };


        for (int attempt = 0; attempt < delaysMs.Length; attempt++)
        {
            await System.Threading.Tasks.Task.Delay(delaysMs[attempt]);


            if (verificationEpoch != rematchRosterRefreshEpoch ||
                expectedNavigationEpoch != navigationEpoch ||
                !roomActive ||
                runtimeBridge == null ||
                !runtimeBridge.HasLobby)
            {
                return;
            }


            AtlasLobbyOperationResult result =
                await runtimeBridge.GetSnapshotAsync();


            if (verificationEpoch != rematchRosterRefreshEpoch ||
                expectedNavigationEpoch != navigationEpoch ||
                !roomActive)
            {
                return;
            }


            if (result == null ||
                !result.Success ||
                result.Snapshot == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "ATB_REMATCH_ROSTER_REFRESH " +
                    "attempt=" + (attempt + 1) +
                    " stable=false reason=snapshot_failed " +
                    "detail=" + (result != null ? result.TechnicalMessage : "null_result"),
                    this);
#endif
                continue;
            }


            string stabilityReason;
            bool stable = IsAuthoritativeRosterStable(
                result.Snapshot,
                out stabilityReason);


#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                "ATB_REMATCH_ROSTER_REFRESH " +
                "attempt=" + (attempt + 1) +
                " stable=" + stable.ToString().ToLowerInvariant() +
                " reason=" + stabilityReason +
                " members=" +
                (result.Snapshot.Members != null
                    ? result.Snapshot.Members.Count.ToString()
                    : "0") +
                " host_slot=" + ResolveHostSlotIndex(result.Snapshot),
                this);
#endif


            if (!stable)
            {
                continue;
            }


            RebuildRosterFromAuthoritativeSnapshot(result.Snapshot);
            CaptureRematchVerifiedIdentityLock(result.Snapshot);
            return;
        }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning(
            "ATB_REMATCH_ROSTER_REFRESH exhausted=true " +
            "action=keep_last_rendered_roster",
            this);
#endif
    }








    private static bool IsAuthoritativeRosterStable(
        AtlasLobbySnapshot snapshot,
        out string reason)
    {
        if (snapshot == null)
        {
            reason = "snapshot_null";
            return false;
        }


        if (snapshot.LifecycleState != AtlasRoomLifecycleState.Waiting)
        {
            reason = "not_waiting";
            return false;
        }


        if (snapshot.Members == null)
        {
            reason = "members_null";
            return false;
        }


        HashSet<string> seenHumanAccounts =
            new HashSet<string>(StringComparer.Ordinal);


        int hostMatches = 0;
        string hostAccountId = snapshot.HostAccountId ?? string.Empty;


        foreach (AtlasLobbyMemberSnapshot member in snapshot.Members)
        {
            if (member == null ||
                member.SlotIndex < 0 ||
                member.SlotIndex >= 4 ||
                string.IsNullOrWhiteSpace(member.AccountId))
            {
                continue;
            }


            bool humanSeat =
                member.SeatMode == AtlasLobbySeatMode.HostLocal ||
                member.SeatMode == AtlasLobbySeatMode.LocalHuman ||
                member.SeatMode == AtlasLobbySeatMode.RemoteHuman;


            if (!humanSeat)
            {
                continue;
            }


            if (!seenHumanAccounts.Add(member.AccountId))
            {
                reason = "duplicate_human_uid";
                return false;
            }


            if (!string.IsNullOrWhiteSpace(hostAccountId) &&
                string.Equals(
                    member.AccountId,
                    hostAccountId,
                    StringComparison.Ordinal))
            {
                hostMatches++;
            }
        }


        if (!string.IsNullOrWhiteSpace(hostAccountId) &&
            hostMatches != 1)
        {
            reason = "host_projection_count_" + hostMatches;
            return false;
        }


        if (ResolveHostSlotIndex(snapshot) < 0)
        {
            reason = "host_slot_missing";
            return false;
        }


        reason = "unique_authoritative_roster";
        return true;
    }








    private void ClearRematchVerifiedIdentityLock()
    {
        rematchVerifiedIdentityLockActive = false;
        for (int i = 0; i < 4; i++)
        {
            rematchVerifiedAccountBySlot[i] = string.Empty;
            rematchVerifiedNameBySlot[i] = string.Empty;
        }
    }








    private void CaptureRematchVerifiedIdentityLock(
        AtlasLobbySnapshot snapshot)
    {
        ClearRematchVerifiedIdentityLock();


        if (snapshot == null ||
            snapshot.Members == null ||
            snapshot.LifecycleState != AtlasRoomLifecycleState.Waiting)
        {
            return;
        }


        foreach (AtlasLobbyMemberSnapshot member in snapshot.Members)
        {
            if (member == null ||
                !member.Active ||
                member.SlotIndex < 0 ||
                member.SlotIndex >= 4 ||
                string.IsNullOrWhiteSpace(member.AccountId))
            {
                continue;
            }


            bool humanSeat =
                member.SeatMode == AtlasLobbySeatMode.HostLocal ||
                member.SeatMode == AtlasLobbySeatMode.LocalHuman ||
                member.SeatMode == AtlasLobbySeatMode.RemoteHuman;


            if (!humanSeat)
            {
                continue;
            }


            rematchVerifiedAccountBySlot[member.SlotIndex] =
                member.AccountId;


            if (!string.IsNullOrWhiteSpace(member.DisplayName))
            {
                rematchVerifiedNameBySlot[member.SlotIndex] =
                    member.DisplayName.Trim();
            }
        }


        rematchVerifiedIdentityLockActive =
            rematchVerifiedAccountBySlot.Any(
                accountId => !string.IsNullOrWhiteSpace(accountId));


#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (rematchVerifiedIdentityLockActive)
        {
            Debug.Log(
                "ATB_REMATCH_ROSTER_IDENTITY_LOCK captured=true " +
                BuildRematchIdentityLockSummary(),
                this);
        }
#endif
    }








    private void ApplyRematchVerifiedIdentityGuard(
        AtlasLobbySnapshot snapshot)
    {
        if (!rematchVerifiedIdentityLockActive ||
            snapshot == null ||
            snapshot.Members == null ||
            snapshot.LifecycleState != AtlasRoomLifecycleState.Waiting)
        {
            return;
        }


        for (int slot = 0; slot < 4; slot++)
        {
            string lockedAccountId =
                rematchVerifiedAccountBySlot[slot];


            if (string.IsNullOrWhiteSpace(lockedAccountId))
            {
                continue;
            }


            AtlasLobbyMemberSnapshot member =
                snapshot.Members.FirstOrDefault(
                    candidate =>
                        candidate != null &&
                        candidate.Active &&
                        candidate.SlotIndex == slot);


            if (member == null)
            {
                continue;
            }


            if (!string.Equals(
                    member.AccountId,
                    lockedAccountId,
                    StringComparison.Ordinal))
            {
                int authoritativeHostSlot =
                    ResolveHostSlotIndex(snapshot);


                bool transientDuplicateHostProjection =
                    slot != authoritativeHostSlot &&
                    !string.IsNullOrWhiteSpace(snapshot.HostAccountId) &&
                    string.Equals(
                        member.AccountId,
                        snapshot.HostAccountId,
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        lockedAccountId,
                        snapshot.HostAccountId,
                        StringComparison.Ordinal);


                if (transientDuplicateHostProjection)
                {
                    // Host migration/rematch can replay the promoted host UID
                    // into the outgoing host's old slot for a later poll even
                    // after a correct Waiting roster was already observed.
                    // This is NOT a real occupant change. Keep the verified
                    // slot identity so presentation code cannot collapse two
                    // humans into the promoted host.
                    seatIdentityAccountCache[slot] = lockedAccountId;
                    if (!string.IsNullOrWhiteSpace(
                            rematchVerifiedNameBySlot[slot]))
                    {
                        seatIdentityNameCache[slot] =
                            rematchVerifiedNameBySlot[slot];
                    }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(
                        "ATB_REMATCH_ROSTER_ACCOUNT_GUARD preserved=true " +
                        "slot=" + slot +
                        " host_slot=" + authoritativeHostSlot +
                        " verified_account=" + lockedAccountId +
                        " transient_account=" +
                        (member.AccountId ?? string.Empty) +
                        " reason=duplicate_host_projection",
                        this);
#endif
                    continue;
                }


                // A genuinely different account now owns this slot. Stop
                // protecting the old identity rather than masking a real join.
                rematchVerifiedAccountBySlot[slot] = string.Empty;
                rematchVerifiedNameBySlot[slot] = string.Empty;
                continue;
            }


            string lockedName =
                rematchVerifiedNameBySlot[slot];


            if (string.IsNullOrWhiteSpace(lockedName))
            {
                if (!string.IsNullOrWhiteSpace(member.DisplayName))
                {
                    rematchVerifiedNameBySlot[slot] =
                        member.DisplayName.Trim();
                }


                continue;
            }


            if (string.Equals(
                    member.DisplayName,
                    lockedName,
                    StringComparison.Ordinal))
            {
                continue;
            }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "ATB_REMATCH_ROSTER_NAME_GUARD corrected=true " +
                "slot=" + slot +
                " account=" + lockedAccountId +
                " incoming_name=" + (member.DisplayName ?? string.Empty) +
                " verified_name=" + lockedName,
                this);
#endif


            // Presentation-only correction. AccountId, seat mode, readiness,
            // host ownership and connection state remain authoritative.
            member.DisplayName = lockedName;
        }


        bool anyLocked =
            rematchVerifiedAccountBySlot.Any(
                accountId => !string.IsNullOrWhiteSpace(accountId));


        if (!anyLocked)
        {
            ClearRematchVerifiedIdentityLock();
        }
    }








    private string BuildRematchIdentityLockSummary()
    {
        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();


        for (int slot = 0; slot < 4; slot++)
        {
            if (string.IsNullOrWhiteSpace(
                    rematchVerifiedAccountBySlot[slot]))
            {
                continue;
            }


            if (builder.Length > 0)
            {
                builder.Append(" | ");
            }


            builder.Append("P");
            builder.Append(slot + 1);
            builder.Append("=");
            builder.Append(rematchVerifiedNameBySlot[slot]);
            builder.Append("[");
            builder.Append(rematchVerifiedAccountBySlot[slot]);
            builder.Append("]");
        }


        return builder.ToString();
    }








    private void RebuildRosterFromAuthoritativeSnapshot(
        AtlasLobbySnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }


        // This refresh intentionally discards the rematch presentation cache.
        // At this point the snapshot has already passed the uniqueness checks,
        // so the backend roster is the source of truth for every visible seat.
        for (int i = 0; i < 4; i++)
        {
            seatIdentityAccountCache[i] = string.Empty;
            seatIdentityNameCache[i] = string.Empty;
        }


        seatIdentityCacheLobbyId = snapshot.LobbyId ?? string.Empty;
        lastDuplicateHostProjectionLog = string.Empty;


        backendSnapshot = null;


        ApplyBackendSnapshot(
            snapshot,
            roomCode,
            runtimeBridge != null
                ? runtimeBridge.CurrentAccountId
                : backendLocalAccountId);


        // ApplyBackendSnapshot already configures the correct host/guest view,
        // but repaint once more after the cache reset so no stale row survives
        // from the rematch transition frame.
        if (localIsHost)
        {
            LoadHostSeatModesFromBackend(snapshot);
            RefreshHostSeatRows();
            RefreshHostStartAvailability();
        }
        else
        {
            RefreshGuestSeats();
        }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            "ATB_REMATCH_ROSTER_REFRESH applied=true " +
            "host_slot=" + ResolveHostSlotIndex(snapshot) +
            " local_is_host=" + localIsHost.ToString().ToLowerInvariant(),
            this);
#endif
    }








    private AtlasBoardLobbySelection GetCurrentLobbySelection()
    {
        mainMenuController ??=
            GetComponent<AtlasBoardMainMenuController>();








        AtlasBoardLobbySelection selection =
            mainMenuController != null
                ? mainMenuController.GetCurrentLobbySelection()
                : new AtlasBoardLobbySelection
                {
                    Mode = "PRIVATE TABLE",
                    MapId = "Turkey",
                    PlayerCount = GetPlayerCount(),
                    RoundLimit = 20,
                    ThemeId = "classic_table",
                    BalancedDevelopment = true,
                    DoublesEnabled = true,
                    TripleDoublePenaltyEnabled = true
                };








        // The private/public lobby owns the visible rule toggles. The legacy
        // Main Menu still exposes ToggleProxy values, which can lag behind the
        // real controls during a backend poll/debounce boundary. Serialize the
        // exact visible host intent instead of a hidden/stale proxy value.
        if (hostSettingsToggles != null)
        {
            if (hostSettingsToggles.Length > 0 &&
                hostSettingsToggles[0] != null)
            {
                selection.BalancedDevelopment =
                    hostSettingsToggles[0].isOn;
            }








            if (hostSettingsToggles.Length > 1 &&
                hostSettingsToggles[1] != null)
            {
                selection.DoublesEnabled =
                    hostSettingsToggles[1].isOn;
            }








            if (hostSettingsToggles.Length > 2 &&
                hostSettingsToggles[2] != null)
            {
                selection.TripleDoublePenaltyEnabled =
                    hostSettingsToggles[2].isOn;
            }
        }








        selection.BoardThemeId = selectedBoardThemeId;
        return selection;
    }






    private string[] BuildBackendSeatPolicies(
        int playerCount)
    {
        string[] policies = { "inactive", "inactive", "inactive", "inactive" };
        int total = Mathf.Clamp(playerCount, 2, 4);
        int hostSlotIndex = GetAuthoritativeHostSlotIndex();


        for (int i = 0; i < 4; i++)
        {
            if (i >= total)
            {
                policies[i] = "inactive";
            }
            else if (i == hostSlotIndex)
            {
                policies[i] = "local_human";
            }
            else
            {
                policies[i] = hostSeatModes[i] switch
                {
                    PrivateSeatMode.LocalHuman => "local_human",
                    PrivateSeatMode.Bot => "bot",
                    PrivateSeatMode.RemoteHuman => "online",
                    _ => "online"
                };
            }
        }


        return policies;
    }


    private void SetEntryButtonsInteractable(
        bool interactable)
    {
        if (createRoomButton != null)
        {
            createRoomButton.interactable = interactable;
        }








        if (joinRoomButton != null)
        {
            joinRoomButton.interactable = interactable;
        }








        if (joinCodeInput != null)
        {
            joinCodeInput.interactable = interactable;
        }








        if (joinPasswordInput != null)
        {
            joinPasswordInput.interactable = interactable;
        }
    }








    private GameObject ownedRoomLimitPopup;
    private Button ownedRoomLimitOk;
    private TMP_Text ownedRoomLimitMessage;
    private bool ownedRoomQuotaCleanupBusy;


    // One shared popup for Private, Public and Public Browser Create paths.
    // The popup is placed under the active scene Canvas, not under a hidden
    // Private Lobby root (the browser may be open without that root).
    public void ShowOwnedRoomLimitPopup()
    {
        if (ownedRoomLimitPopup == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null && mainMenuController != null)
                canvas = mainMenuController.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("ATB_RTDB_QUOTA_POPUP missing_canvas=true");
                return;
            }
            ownedRoomLimitPopup = new GameObject("ATB_OwnedRoomLimitPopup",
                typeof(RectTransform), typeof(Image));
            ownedRoomLimitPopup.transform.SetParent(canvas.transform, false);
            RectTransform shade = (RectTransform)ownedRoomLimitPopup.transform;
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            ownedRoomLimitPopup.GetComponent<Image>().color = new Color(0f,0f,0f,.78f);
            GameObject panel = new GameObject("QuotaPanel",typeof(RectTransform),typeof(Image));
            panel.transform.SetParent(shade,false);
            RectTransform box = (RectTransform)panel.transform;
            box.anchorMin = box.anchorMax = new Vector2(.5f,.5f);
            box.sizeDelta = new Vector2(720,330);
            panel.GetComponent<Image>().color = new Color(.97f,.95f,.91f,1f);
            ownedRoomLimitMessage = MakeOwnedRoomQuotaText(panel.transform,"Message",
                23, new Vector2(650,175), new Vector2(0,50));
            GameObject button = new GameObject("ConfirmCleanup",typeof(RectTransform),
                typeof(Image),typeof(Button));
            button.transform.SetParent(panel.transform,false);
            RectTransform br=(RectTransform)button.transform;
            br.anchorMin=br.anchorMax=new Vector2(.5f,.5f);
            br.sizeDelta=new Vector2(250,64);
            br.anchoredPosition=new Vector2(0,-105);
            button.GetComponent<Image>().color=new Color(.28f,.43f,.03f,1f);
            ownedRoomLimitOk=button.GetComponent<Button>();
            ownedRoomLimitOk.onClick.AddListener(ConfirmOwnedRoomLimitCleanup);
            MakeOwnedRoomQuotaText(button.transform,"OK",27,
                new Vector2(235,55),Vector2.zero).text=T("common.ok","TAMAM");
        }
        ownedRoomLimitMessage.text = T("lobby.owner_rooms_limit_notice",
            "10’dan fazla açık odanız bulunuyor. Yeni oda oluşturmak için TAMAM’a basın. " +
            "TAMAM ile yaş sınırı olmadan size ait bekleyen ve tamamen bağlantısız odalar silinecek. " +
            "Halen oyuncu bağlı maçlar korunacak.");
        if (ownedRoomLimitOk != null)
            ownedRoomLimitOk.interactable = !ownedRoomQuotaCleanupBusy;
        ownedRoomLimitPopup.SetActive(true);
        ownedRoomLimitPopup.transform.SetAsLastSibling();
        Debug.Log("ATB_RTDB_QUOTA_POPUP shown=true three_create_paths=true");
    }


    private static TMP_Text MakeOwnedRoomQuotaText(Transform parent, string label,
        int size, Vector2 dimensions, Vector2 position)
    {
        GameObject node = new GameObject(label,typeof(RectTransform),
            typeof(TextMeshProUGUI));
        node.transform.SetParent(parent,false);
        RectTransform r=(RectTransform)node.transform;
        r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);
        r.sizeDelta=dimensions;
        r.anchoredPosition=position;
        TextMeshProUGUI text=node.GetComponent<TextMeshProUGUI>();
        text.fontSize=size;
        text.color = label == "OK" ? Color.white : new Color(.16f,.17f,.18f,1f);
        text.alignment=TextAlignmentOptions.Center;
        text.textWrappingMode=TextWrappingModes.Normal;
        text.raycastTarget=false;
        return text;
    }


    private async void ConfirmOwnedRoomLimitCleanup()
    {
        if (ownedRoomQuotaCleanupBusy || runtimeBridge == null) return;
        ownedRoomQuotaCleanupBusy=true;
        ownedRoomLimitOk.interactable=false;
        ownedRoomLimitMessage.text=T("lobby.owner_rooms_cleaning",
            "Size ait odalar kontrol edilip kapatılıyor. Lütfen bekleyin...");
        try
        {
            AtlasLobbyOperationResult result =
                await runtimeBridge.CloseOwnedWaitingRoomsOnConsentAsync();
            if (result.Success)
            {
                ownedRoomLimitPopup.SetActive(false);
                SetText(entryStatusText,T("lobby.owner_rooms_cleaned",
                    "Size ait eski odalar temizlendi. Yeni oda oluşturabilirsiniz."));
                Debug.Log("ATB_RTDB_QUOTA_POPUP cleanup_completed=true");
            }
            else
            {
                ownedRoomLimitMessage.text=T("lobby.owner_rooms_cleanup_incomplete",
                    "Bazı odalar kapatılamadı. Bağlı oyunculu maçlar veya yetki hatası olabilir; " +
                    "lütfen tekrar deneyin veya logu iletin.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_RTDB_QUOTA_POPUP cleanup_failed="+ex.GetType().Name);
            ownedRoomLimitMessage.text=T("lobby.owner_rooms_cleanup_failed",
                "Oda temizliği tamamlanamadı. Tekrar deneyin.");
        }
        finally
        {
            ownedRoomQuotaCleanupBusy=false;
            if (ownedRoomLimitOk != null) ownedRoomLimitOk.interactable=true;
        }
    }


    private void ShowEntryBackendError(
        AtlasLobbyOperationResult result)
    {
        string key =
            result != null
                ? result.ErrorLocalizationKey
                : "lobby.error.unknown";








        // Never expose Firebase/provider URLs, HTTP details or auth internals.
        // Internal diagnostics retain only sanitized technical context.
        bool authProblem = key.StartsWith("account.error.",
            StringComparison.OrdinalIgnoreCase);
        string code = authProblem
            ? AtlasBoardFrontendErrorCodes.SessionPreparationFailed
            : AtlasBoardFrontendErrorCodes.LobbyUnavailable;
        AtlasBoardFrontendErrorCodes.Report(code,
            result != null ? result.TechnicalMessage : "unknown", "private_entry");
        SetText(entryStatusText, AtlasBoardFrontendErrorCodes.PublicMessage(code));
    }








    private void ShowRoomBackendError(
        AtlasLobbyOperationResult result)
    {
        string key =
            result != null
                ? result.ErrorLocalizationKey
                : "lobby.error.unknown";








        string code = key.StartsWith("account.error.",
            StringComparison.OrdinalIgnoreCase)
            ? AtlasBoardFrontendErrorCodes.SessionPreparationFailed
            : AtlasBoardFrontendErrorCodes.LobbySyncFailed;
        AtlasBoardFrontendErrorCodes.Report(code,
            result != null ? result.TechnicalMessage : "unknown", "room_sync");
        SetText(roomStateText, AtlasBoardFrontendErrorCodes.PublicMessage(code));
    }








    private void AdvanceLocalSettingsRevision()
    {
        if (backendSnapshot != null)
        {
            return;
        }








        settingsRevision++;
        readyForRevision = 0;








        SetText(
            roomStateText,
            T(
                "lobby.online.settings_changed",
                "SETTINGS CHANGED • READY STATES RESET"));








        RefreshRoomHeader();
    }








    private void ApplyBackendSettingsToUi(
        AtlasLobbySnapshot snapshot)
    {
        if (snapshot == null ||
            snapshot.Settings == null)
        {
            return;
        }








        suppressSeatEvents = true;
        if (hostSettingsDropdowns != null && hostSettingsDropdowns.Length > 0)
            AtlasBoardPremiumMapDropdownLabels.EnsureOptions(hostSettingsDropdowns[0]);
        AtlasBoardPremiumMapDropdownLabels.PreviewMap(snapshot.Settings.MapId);








        try
        {
            if (hostSettingsDropdowns != null &&
                hostSettingsDropdowns.Length >= 4)
            {
                SetDropdownByIndex(
                    hostSettingsDropdowns[0],
                    snapshot.Settings.MapId switch
                    {
                        "Colorado" => 1,
                        "USA" => 2,
                        "Germany" => 3,
                        "Mexico" => 4,
                        _ => 0
                    });








                SetDropdownByText(
                    hostSettingsDropdowns[1],
                    snapshot.Settings.MaxPlayers.ToString());








                SetDropdownByText(
                    hostSettingsDropdowns[2],
                    snapshot.Settings.RoundLimit.ToString());








                SetDropdownByIndex(
                    hostSettingsDropdowns[3],
                    snapshot.Settings.ThemeId switch
                    {
                        "garden" => 1,
                        "beach" => 2,
                        "pavilion" => 3,
                        "street" => 4,
                        _ => 0
                    });
            }








            bool preservePendingHostRuleIntent =
                localIsHost &&
                (hostConfigurationDirty ||
                 hostConfigurationSyncQueued);








            if (hostSettingsToggles != null &&
                !preservePendingHostRuleIntent)
            {
                if (hostSettingsToggles.Length > 0 &&
                    hostSettingsToggles[0] != null)
                {
                    if (hostSettingsToggles[0].isOn !=
                        snapshot.Settings.BalancedDevelopment)
                    {
                        hostSettingsToggles[0].SetIsOnWithoutNotify(
                            snapshot.Settings.BalancedDevelopment);
                    }
                }








                if (hostSettingsToggles.Length > 1 &&
                    hostSettingsToggles[1] != null)
                {
                    if (hostSettingsToggles[1].isOn !=
                        snapshot.Settings.DoublesEnabled)
                    {
                        hostSettingsToggles[1].SetIsOnWithoutNotify(
                            snapshot.Settings.DoublesEnabled);
                    }
                }








                if (hostSettingsToggles.Length > 2 &&
                    hostSettingsToggles[2] != null)
                {
                    if (hostSettingsToggles[2].isOn !=
                        snapshot.Settings.TripleDoublePenaltyEnabled)
                    {
                        hostSettingsToggles[2].SetIsOnWithoutNotify(
                            snapshot.Settings.TripleDoublePenaltyEnabled);
                    }
                }
            }
        }
        finally
        {
            suppressSeatEvents = false;
        }
    }








    private void LoadHostSeatModesFromBackend(
        AtlasLobbySnapshot snapshot)
    {
        ResetHostSeatModes();








        if (snapshot == null ||
            snapshot.Members == null)
        {
            return;
        }








        foreach (AtlasLobbyMemberSnapshot member in snapshot.Members)
        {
            if (member == null ||
                member.SlotIndex < 0 ||
                member.SlotIndex >= hostSeatModes.Length)
            {
                continue;
            }








            hostSeatModes[member.SlotIndex] =
                member.SeatMode switch
                {
                    AtlasLobbySeatMode.HostLocal => PrivateSeatMode.LocalHuman,
                    AtlasLobbySeatMode.LocalHuman => PrivateSeatMode.LocalHuman,
                    AtlasLobbySeatMode.Bot => PrivateSeatMode.Bot,
                    AtlasLobbySeatMode.RemoteHuman => PrivateSeatMode.RemoteHuman,
                    _ => PrivateSeatMode.OpenOnline
                };
        }
    }








    private static void SetDropdownByIndex(
        TMP_Dropdown dropdown,
        int index)
    {
        if (dropdown == null ||
            dropdown.options == null ||
            dropdown.options.Count == 0)
        {
            return;
        }








        int safeIndex =
            Mathf.Clamp(
                index,
                0,
                dropdown.options.Count - 1);








        if (dropdown.value == safeIndex)
        {
            return;
        }








        dropdown.SetValueWithoutNotify(safeIndex);
        dropdown.RefreshShownValue();
    }








    private static void SetDropdownByText(
        TMP_Dropdown dropdown,
        string expected)
    {
        if (dropdown == null ||
            dropdown.options == null)
        {
            return;
        }








        for (int i = 0; i < dropdown.options.Count; i++)
        {
            if (string.Equals(
                    dropdown.options[i].text,
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (dropdown.value == i)
                {
                    return;
                }








                dropdown.SetValueWithoutNotify(i);
                dropdown.RefreshShownValue();
                return;
            }
        }
    }








    private void ResetHostSeatModes()
    {
        for (int i = 0; i < hostSeatModes.Length; i++)
        {
            hostSeatModes[i] = PrivateSeatMode.OpenOnline;
        }


        int hostSlotIndex = GetAuthoritativeHostSlotIndex();
        if (hostSlotIndex < 0 || hostSlotIndex >= hostSeatModes.Length)
        {
            hostSlotIndex = 0;
        }
        hostSeatModes[hostSlotIndex] = PrivateSeatMode.LocalHuman;
    }


    private void ResetInactiveHostSeatModes()
    {
        int total = GetPlayerCount();








        for (int i = total; i < hostSeatModes.Length; i++)
        {
            hostSeatModes[i] =
                PrivateSeatMode.OpenOnline;
        }
    }








    private void RefreshHostSeatRows()
    {
        int total = GetPlayerCount();
        int hostSlotIndex = GetAuthoritativeHostSlotIndex();
        ForceAllPlayerRowsVisible();


        for (int i = 0; i < 4; i++)
        {
            bool active = i < total;
            SetActive(playerRows != null && i < playerRows.Length ? playerRows[i] : null, active);


            if (!active)
            {
                SetHostSeatActionObjects(i, false, false, false);
                continue;
            }


            if (i == hostSlotIndex)
            {
                AtlasLobbyMemberSnapshot hostMember = FindBackendMemberBySlot(i);
                string hostName = hostMember != null && !string.IsNullOrWhiteSpace(hostMember.DisplayName)
                    ? hostMember.DisplayName + " (" + T("lobby.online.you_suffix", "YOU") + ")"
                    : T("lobby.online.you", "PLAYER (YOU)");
                SetSeat(i, hostName, T("lobby.online.host_local", "HOST • LOCAL"));
                SetHostSeatActionObjects(i, false, false, false);
                continue;
            }


            switch (hostSeatModes[i])
            {
                case PrivateSeatMode.LocalHuman:
                    SetSeat(i,
                        T("lobby.online.local_player_number", "LOCAL PLAYER {0}", i + 1),
                        T("lobby.online.no_ready_required", "LOCAL • NO READY REQUIRED"));
                    SetHostSeatActionObjects(i, false, false, true);
                    break;


                case PrivateSeatMode.Bot:
                    SetSeat(i,
                        T("lobby.online.local_bot_number", "LOCAL BOT {0}", i + 1),
                        T("lobby.online.bot_no_ready", "BOT • NO READY"));
                    SetHostSeatActionObjects(i, false, false, true);
                    break;


                case PrivateSeatMode.RemoteHuman:
                    AtlasLobbyMemberSnapshot remote = FindBackendMemberBySlot(i);
                    bool duplicateHostProjection =
                        remote != null &&
                        i != hostSlotIndex &&
                        backendSnapshot != null &&
                        !string.IsNullOrWhiteSpace(backendSnapshot.HostAccountId) &&
                        string.Equals(
                            remote.AccountId,
                            backendSnapshot.HostAccountId,
                            StringComparison.Ordinal);


                    string remoteName;
                    string remoteStatus;


                    if (duplicateHostProjection &&
                        !string.IsNullOrWhiteSpace(seatIdentityNameCache[i]) &&
                        !string.Equals(
                            seatIdentityAccountCache[i],
                            backendSnapshot.HostAccountId,
                            StringComparison.Ordinal))
                    {
                        remoteName = seatIdentityNameCache[i];
                        remoteStatus = T(
                            "lobby.online.reconnecting",
                            "RECONNECTING");


                        string signature =
                            (backendSnapshot.LobbyId ?? string.Empty) + ":" +
                            i + ":" +
                            (backendSnapshot.HostAccountId ?? string.Empty) + ":" +
                            seatIdentityAccountCache[i];


                        if (!string.Equals(
                                signature,
                                lastDuplicateHostProjectionLog,
                                StringComparison.Ordinal))
                        {
                            lastDuplicateHostProjectionLog = signature;
                            Debug.LogWarning(
                                "ATB_LOBBY_SLOT_RECONCILE " +
                                "duplicate_host_projection=true " +
                                "slot=" + i +
                                " host_slot=" + hostSlotIndex +
                                " cached_uid=" + seatIdentityAccountCache[i] +
                                " cached_name=" + seatIdentityNameCache[i],
                                this);
                        }
                    }
                    else
                    {
                        remoteName = remote != null &&
                                     !string.IsNullOrWhiteSpace(remote.DisplayName)
                            ? remote.DisplayName
                            : T("lobby.online.remote_player", "ONLINE PLAYER");


                        remoteStatus = remote != null &&
                                       remote.IsReadyFor(settingsRevision)
                            ? T(
                                "lobby.online.ready_for_revision",
                                "READY • REV {0}",
                                settingsRevision)
                            : remote != null &&
                              remote.ConnectionState ==
                                  AtlasSeatConnectionState.Reconnecting
                                ? T(
                                    "lobby.online.reconnecting",
                                    "RECONNECTING")
                                : T(
                                    "lobby.online.not_ready",
                                    "NOT READY");
                    }


                    SetSeat(i, remoteName, remoteStatus);
                    SetHostSeatActionObjects(i, false, false, true);
                    break;


                default:
                    SetSeat(i,
                        T("lobby.online.waiting_online", "WAITING FOR ONLINE PLAYER"),
                        T("lobby.online.open_online_seat", "OPEN ONLINE SEAT"));
                    SetHostSeatActionObjects(i, true, false, false);
                    break;
            }
        }
    }


    private void UpdateSeatIdentityPresentationCache(
        AtlasLobbySnapshot previousSnapshot,
        AtlasLobbySnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }


        string lobbyId = snapshot.LobbyId ?? string.Empty;
        bool sameLobby =
            previousSnapshot != null &&
            string.Equals(
                previousSnapshot.LobbyId ?? string.Empty,
                lobbyId,
                StringComparison.Ordinal);


        bool synchronizedRematchContinuity =
            previousSnapshot != null &&
            (previousSnapshot.LifecycleState ==
                 AtlasRoomLifecycleState.Starting ||
             previousSnapshot.LifecycleState ==
                 AtlasRoomLifecycleState.InMatch) &&
            !string.IsNullOrWhiteSpace(previousSnapshot.MatchId) &&
            snapshot.LifecycleState ==
                AtlasRoomLifecycleState.Waiting &&
            string.IsNullOrWhiteSpace(snapshot.MatchId);


        if (!string.Equals(
                seatIdentityCacheLobbyId,
                lobbyId,
                StringComparison.Ordinal))
        {
            // A synchronized rematch is the same human roster returning to the
            // lobby even when the backend rotates a lobby identifier. Preserve
            // the verified seat identities across that handoff. A genuinely
            // unrelated room still starts with a clean cache.
            if (!synchronizedRematchContinuity)
            {
                for (int i = 0; i < 4; i++)
                {
                    seatIdentityAccountCache[i] = string.Empty;
                    seatIdentityNameCache[i] = string.Empty;
                }
            }


            seatIdentityCacheLobbyId = lobbyId;
            lastDuplicateHostProjectionLog = string.Empty;
        }


        if (snapshot.Members == null)
        {
            return;
        }


        int hostSlot = ResolveHostSlotIndex(snapshot);
        string hostAccountId = snapshot.HostAccountId ?? string.Empty;
        bool canUsePreviousIdentity = sameLobby || synchronizedRematchContinuity;


        foreach (AtlasLobbyMemberSnapshot member in snapshot.Members)
        {
            if (member == null ||
                !member.Active ||
                member.SlotIndex < 0 ||
                member.SlotIndex >= 4 ||
                string.IsNullOrWhiteSpace(member.AccountId))
            {
                continue;
            }


            bool duplicateHostProjection =
                member.SlotIndex != hostSlot &&
                !string.IsNullOrWhiteSpace(hostAccountId) &&
                string.Equals(
                    member.AccountId,
                    hostAccountId,
                    StringComparison.Ordinal);


            if (duplicateHostProjection)
            {
                // During Host migration/rematch the outgoing host seat can
                // briefly be projected with the NEW host UID. Recover the
                // occupant from the immediately preceding authoritative
                // snapshot instead of allowing that transient projection to
                // poison the per-seat identity cache.
                AtlasLobbyMemberSnapshot previousMember =
                    canUsePreviousIdentity &&
                    previousSnapshot != null &&
                    previousSnapshot.Members != null
                        ? previousSnapshot.Members.FirstOrDefault(candidate =>
                            candidate != null &&
                            candidate.SlotIndex == member.SlotIndex &&
                            !string.IsNullOrWhiteSpace(candidate.AccountId) &&
                            !string.Equals(
                                candidate.AccountId,
                                hostAccountId,
                                StringComparison.Ordinal))
                        : null;


                if (previousMember != null)
                {
                    seatIdentityAccountCache[member.SlotIndex] =
                        previousMember.AccountId;


                    if (!string.IsNullOrWhiteSpace(previousMember.DisplayName))
                    {
                        seatIdentityNameCache[member.SlotIndex] =
                            previousMember.DisplayName.Trim();
                    }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(
                        "ATB_LOBBY_SLOT_IDENTITY_RECOVERED " +
                        "source=previous_snapshot " +
                        "slot=" + member.SlotIndex +
                        " host_slot=" + hostSlot +
                        " recovered_uid=" + previousMember.AccountId +
                        " recovered_name=" +
                        (previousMember.DisplayName ?? string.Empty),
                        this);
#endif
                }


                // Never overwrite a non-host seat with a duplicated host UID.
                continue;
            }


            seatIdentityAccountCache[member.SlotIndex] =
                member.AccountId;


            if (!string.IsNullOrWhiteSpace(member.DisplayName))
            {
                seatIdentityNameCache[member.SlotIndex] =
                    member.DisplayName.Trim();
            }
        }
    }


    private static int ResolveHostSlotIndex(
        AtlasLobbySnapshot snapshot)
    {
        if (snapshot == null || snapshot.Members == null)
        {
            return -1;
        }


        string hostAccountId = snapshot.HostAccountId ?? string.Empty;


        AtlasLobbyMemberSnapshot hostMember =
            snapshot.Members.FirstOrDefault(member =>
                member != null &&
                member.SlotIndex >= 0 &&
                member.SlotIndex < 4 &&
                member.SeatMode == AtlasLobbySeatMode.HostLocal &&
                !string.IsNullOrWhiteSpace(hostAccountId) &&
                string.Equals(
                    member.AccountId,
                    hostAccountId,
                    StringComparison.Ordinal));


        hostMember ??= snapshot.Members.FirstOrDefault(member =>
            member != null &&
            member.SlotIndex >= 0 &&
            member.SlotIndex < 4 &&
            member.IsHost &&
            !string.IsNullOrWhiteSpace(hostAccountId) &&
            string.Equals(
                member.AccountId,
                hostAccountId,
                StringComparison.Ordinal));


        hostMember ??= snapshot.Members.FirstOrDefault(member =>
            member != null &&
            member.SlotIndex >= 0 &&
            member.SlotIndex < 4 &&
            !string.IsNullOrWhiteSpace(hostAccountId) &&
            string.Equals(
                member.AccountId,
                hostAccountId,
                StringComparison.Ordinal));


        hostMember ??= snapshot.Members.FirstOrDefault(member =>
            member != null &&
            member.SlotIndex >= 0 &&
            member.SlotIndex < 4 &&
            member.IsHost);


        return hostMember != null
            ? hostMember.SlotIndex
            : -1;
    }


    private int GetAuthoritativeHostSlotIndex()
    {
        int resolved = ResolveHostSlotIndex(backendSnapshot);
        return resolved >= 0 ? resolved : 0;
    }




    private bool IsAuthoritativeHostSlot(int slotIndex)
    {
        return slotIndex == GetAuthoritativeHostSlotIndex();
    }


    private AtlasLobbyMemberSnapshot FindBackendMemberBySlot(
        int slotIndex)
    {
        if (backendSnapshot == null ||
            backendSnapshot.Members == null)
        {
            return null;
        }








        return backendSnapshot.Members.FirstOrDefault(
            member =>
                member != null &&
                member.SlotIndex == slotIndex);
    }








    private void SetHostSeatActionObjects(
        int index,
        bool showAdd,
        bool showChoice,
        bool showRemove)
    {
        Button add =
            GetAddButton(index);








        if (add != null)
        {
            add.gameObject.SetActive(showAdd);
        }








        SetActive(
            GetChoicePanel(index),
            showChoice);








        Button remove =
            GetRemoveButton(index);








        if (remove != null)
        {
            remove.gameObject.SetActive(showRemove);
        }
    }








    private void CloseAllSeatChoicePanels()
    {
        openSeatChoiceIndex = -1;








        if (privateSeatChoicePanels == null)
        {
            return;
        }








        for (int i = 0;
             i < privateSeatChoicePanels.Length;
             i++)
        {
            SetActive(
                privateSeatChoicePanels[i],
                false);








            SetSeatTextVisible(
                i,
                true);








            if (i > 0 &&
                i < GetPlayerCount() &&
                hostSeatModes[i] ==
                PrivateSeatMode.OpenOnline &&
                roomActive &&
                localIsHost)
            {
                Button add =
                    GetAddButton(i);








                if (add != null)
                {
                    add.gameObject.SetActive(true);
                }
            }
        }
    }








    private void RestoreOpenSeatChoicePanelIfValid(
        int index)
    {
        if (!roomActive ||
            !localIsHost ||
            index < 0 ||
            index >= GetPlayerCount() ||
            index >= hostSeatModes.Length ||
            IsAuthoritativeHostSlot(index) ||
            hostSeatModes[index] != PrivateSeatMode.OpenOnline)
        {
            openSeatChoiceIndex = -1;
            return;
        }


        GameObject panel = GetChoicePanel(index);
        Button add = GetAddButton(index);
        if (panel == null || add == null)
        {
            openSeatChoiceIndex = -1;
            return;
        }


        openSeatChoiceIndex = index;
        SetSeatTextVisible(index, false);
        SetActive(panel, true);
        add.gameObject.SetActive(false);
    }


    private void SetHostSeatActionUIVisible(
        bool visible)
    {
        if (visible)
        {
            RefreshHostSeatRows();
            return;
        }








        for (int i = 0; i < 4; i++)
        {
            SetHostSeatActionObjects(
                i,
                false,
                false,
                false);
        }
    }








    private Button GetAddButton(int index)
    {
        return GetButtonArrayItem(
            privateSeatAddButtons,
            index);
    }








    private Button GetRemoveButton(int index)
    {
        return GetButtonArrayItem(
            privateSeatRemoveButtons,
            index);
    }








    private GameObject GetChoicePanel(int index)
    {
        if (privateSeatChoicePanels == null ||
            index < 0 ||
            index >= privateSeatChoicePanels.Length)
        {
            return null;
        }








        return privateSeatChoicePanels[index];
    }








    private static Button GetButtonArrayItem(
        Button[] array,
        int index)
    {
        if (array == null ||
            index < 0 ||
            index >= array.Length)
        {
            return null;
        }








        return array[index];
    }








    private bool HasOpenOnlineSeat()
    {
        int total = GetPlayerCount();
        int hostSlotIndex = GetAuthoritativeHostSlotIndex();
        for (int i = 0; i < total; i++)
        {
            if (i != hostSlotIndex && hostSeatModes[i] == PrivateSeatMode.OpenOnline)
            {
                return true;
            }
        }
        return false;
    }


    private int CountResolvedLocalHumans()
    {
        int total = GetPlayerCount();
        int hostSlotIndex = GetAuthoritativeHostSlotIndex();
        int humans = 0;
        for (int i = 0; i < total; i++)
        {
            if (i == hostSlotIndex || hostSeatModes[i] == PrivateSeatMode.LocalHuman)
            {
                humans++;
            }
        }
        return Mathf.Clamp(humans, 1, total);
    }


    public void PrepareLegacyControlsForOnlineMatchStart()
    {
        if (backendSnapshot == null ||
            backendSnapshot.Members == null)
        {
            return;
        }








        int total =
            backendSnapshot.Settings != null
                ? Mathf.Clamp(
                    backendSnapshot.Settings.MaxPlayers,
                    2,
                    4)
                : GetPlayerCount();








        for (int slotIndex = 0;
             slotIndex < total;
             slotIndex++)
        {
            AtlasLobbyMemberSnapshot member =
                backendSnapshot.Members
                    .FirstOrDefault(
                        item =>
                            item != null &&
                            item.Active &&
                            item.SlotIndex == slotIndex);








            bool shouldBeBot =
                member != null &&
                member.SeatMode ==
                    AtlasLobbySeatMode.Bot;








            // Every real/local human remains a Human in the stable legacy
            // MatchSetup layer. Phase 5 Turn authority decides which client
            // is allowed to press Roll for that Human seat.
            SetPlayerType(
                slotIndex,
                shouldBeBot);
        }








        for (int slotIndex = total;
             slotIndex < 4;
             slotIndex++)
        {
            SetPlayerType(
                slotIndex,
                false);
        }
    }








    private void SyncHostSeatModesToLegacyControls()
    {
        int total = GetPlayerCount();
        int hostSlotIndex = GetAuthoritativeHostSlotIndex();


        for (int i = 0; i < total; i++)
        {
            bool bot = i != hostSlotIndex && hostSeatModes[i] == PrivateSeatMode.Bot;
            SetPlayerType(i, bot);
        }
    }


    private void SaveLegacyControlValues()
    {
        for (int i = 0; i < 4; i++)
        {
            TMP_Dropdown dropdown =
                GetPlayerTypeDropdown(i);








            legacyControlValuesBeforePrivate[i] =
                dropdown != null
                    ? dropdown.value
                    : 0;
        }








        legacyControlValuesSaved = true;
    }








    private void RestoreLegacyControlValues()
    {
        if (!legacyControlValuesSaved)
        {
            return;
        }








        for (int i = 0; i < 4; i++)
        {
            TMP_Dropdown dropdown =
                GetPlayerTypeDropdown(i);








            if (dropdown == null)
            {
                continue;
            }








            int value =
                Mathf.Clamp(
                    legacyControlValuesBeforePrivate[i],
                    0,
                    Mathf.Max(
                        0,
                        dropdown.options.Count - 1));








            dropdown.SetValueWithoutNotify(
                value);








            dropdown.RefreshShownValue();
        }








        legacyControlValuesSaved = false;
    }








    private void RefreshHostStartAvailability()
    {
        if (!roomActive ||
            !localIsHost ||
            legacyStartButton == null)
        {
            return;
        }








        Button start =
            legacyStartButton.GetComponent<Button>();








        if (start == null)
        {
            return;
        }








        bool canStart;








        if (hostConfigurationDirty ||
            hostConfigurationSyncQueued ||
            backendBusy)
        {
            canStart = false;
        }
        else if (backendSnapshot != null)
        {
            canStart =
                CanBackendHostStart(backendSnapshot);
        }
        else
        {
            // Local Humans and Bots never require network Ready.
            // OpenOnline seats may still start; the authoritative backend
            // converts unresolved open seats to Bots when Start is accepted.
            canStart = true;
        }








        start.interactable = canStart;








        if (canStart)
        {
            // Seat cards and Start-button state already communicate readiness.
            // Do not expose implementation notes such as "open seats become
            // bots" in the compact Room Code header.
            SetText(
                roomStateText,
                string.Empty);
        }
        else if (backendSnapshot == null)
        {
            SetText(
                roomStateText,
                string.Empty);
        }
    }








    private static bool CanBackendHostStart(
        AtlasLobbySnapshot snapshot)
    {
        if (snapshot == null ||
            snapshot.LifecycleState != AtlasRoomLifecycleState.Waiting ||
            snapshot.Members == null)
        {
            return false;
        }








        int revision = snapshot.SettingsRevision;








        foreach (AtlasLobbyMemberSnapshot member in snapshot.Members)
        {
            if (member == null ||
                !member.Active)
            {
                continue;
            }








            if (member.SeatMode ==
                    AtlasLobbySeatMode.RemoteHuman &&
                !member.IsReadyFor(revision))
            {
                return false;
            }
        }








        return true;
    }








    private void RefreshRoomHeader()
    {
        if (!roomActive)
        {
            return;
        }








        SetText(
            roomCodeText,
            codeVisible
                ? roomCode
                : new string('•', 6));








        SetText(
            codeVisibilityButtonText,
            codeVisible
                ? T("lobby.online.hide", "HIDE")
                : T("lobby.online.show", "SHOW"));








        // Settings revision is transport metadata, not customer-facing lobby
        // copy. Keep it available in logs/backend state but do not crowd the
        // Room Code header with engineering text.
        SetText(
            revisionText,
            string.Empty);








        if (localIsHost)
        {
            SetActive(
                readyButton != null
                    ? readyButton.gameObject
                    : null,
                false);








            SetActive(legacyStartButton, true);
            RefreshHostStartAvailability();
            return;
        }








        SetActive(legacyStartButton, false);








        SetActive(
            readyButton != null
                ? readyButton.gameObject
                : null,
            true);








        bool ready =
            readyForRevision == settingsRevision;








        SetText(
            readyButtonText,
            ready
                ? T("lobby.online.ready_checked", "READY ✓")
                : T("lobby.online.ready", "READY"));








        if (readyButton != null)
        {
            readyButton.interactable =
                backendSnapshot == null ||
                backendSnapshot.LifecycleState !=
                AtlasRoomLifecycleState.Starting;
        }
    }








    private void ReconcileGuestHostSeatName()
    {
        if (backendSnapshot == null ||
            backendSnapshot.Members == null ||
            privateSeatNames == null ||
            privateOnlineRoot == null ||
            !privateOnlineRoot.activeInHierarchy)
        {
            return;
        }

        int hostSlotIndex = ResolveHostSlotIndex(backendSnapshot);
        if (hostSlotIndex < 0 ||
            hostSlotIndex >= privateSeatNames.Length ||
            privateSeatNames[hostSlotIndex] == null)
        {
            return;
        }

        AtlasLobbyMemberSnapshot hostSeat =
            backendSnapshot.Members.FirstOrDefault(
                member =>
                    member != null &&
                    member.Active &&
                    member.SlotIndex == hostSlotIndex &&
                    string.Equals(
                        member.AccountId,
                        backendSnapshot.HostAccountId,
                        StringComparison.Ordinal));

        if (hostSeat == null ||
            string.IsNullOrWhiteSpace(hostSeat.DisplayName))
        {
            return;
        }

        string expected = hostSeat.DisplayName.Trim();
        if (string.Equals(
                privateSeatNames[hostSlotIndex].text,
                expected,
                StringComparison.Ordinal))
        {
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            "ATB_RTDB_LOBBY_HOST_LABEL corrected=true " +
            "source=snapshot_host_uid local_override=false host_slot=" +
            hostSlotIndex,
            this);
#endif
        // Host authority is NOT synonymous with P1. Only repaint the canonical
        // host seat. Writing privateSeatNames[0] here used to overwrite a
        // perfectly valid follower row after Host migration.
        SetText(privateSeatNames[hostSlotIndex], expected);
    }








    private void RefreshGuestSeats()
    {
        if (!roomActive ||
            localIsHost)
        {
            return;
        }








        ForceAllPlayerRowsVisible();








        if (backendSnapshot != null)
        {
            RefreshSeatsFromBackendSnapshot();
            return;
        }








        SetSeat(
            0,
            T("lobby.online.host_player", "HOST PLAYER"),
            T("lobby.online.host", "HOST"));








        // Never label an unverified fallback seat as YOU. The authenticated
        // backend snapshot is the only authority for local seat ownership.
        SetSeat(
            1,
            T("lobby.online.remote_player", "ONLINE PLAYER"),
            readyForRevision == settingsRevision
                ? T(
                    "lobby.online.ready_for_revision",
                    "READY • REV {0}",
                    settingsRevision)
                : T("lobby.online.not_ready", "NOT READY"));








        SetSeat(
            2,
            T("lobby.online.bot", "BOT"),
            T("lobby.online.bot_seat", "BOT SEAT"));








        SetSeat(
            3,
            T("lobby.online.bot", "BOT"),
            T("lobby.online.bot_seat", "BOT SEAT"));
    }








    private void RefreshSeatsFromBackendSnapshot()
    {
        int revision = backendSnapshot.SettingsRevision;
        int maxPlayers =
            backendSnapshot.Settings != null
                ? Mathf.Clamp(
                    backendSnapshot.Settings.MaxPlayers,
                    2,
                    4)
                : 4;








        for (int i = 0; i < 4; i++)
        {
            SetActive(
                playerRows != null &&
                i < playerRows.Length
                    ? playerRows[i]
                    : null,
                i < maxPlayers);








            if (i >= maxPlayers)
            {
                continue;
            }








            AtlasLobbyMemberSnapshot member =
                backendSnapshot.Members != null
                    ? backendSnapshot.Members.FirstOrDefault(
                        candidate =>
                            candidate != null &&
                            candidate.SlotIndex == i)
                    : null;








            if (member == null ||
                !member.Active)
            {
                SetSeat(
                    i,
                    T(
                        "lobby.online.waiting_online",
                        "WAITING FOR ONLINE PLAYER"),
                    T(
                        "lobby.online.open_online_seat",
                        "OPEN ONLINE SEAT"));
                continue;
            }








            switch (member.SeatMode)
            {
                case AtlasLobbySeatMode.HostLocal:
                    SetSeat(
                        i,
                        !string.IsNullOrWhiteSpace(member.DisplayName)
                            ? member.DisplayName
                            : T("lobby.online.host_player", "HOST PLAYER"),
                        T("lobby.online.host_local", "HOST • LOCAL"));
                    break;








                case AtlasLobbySeatMode.LocalHuman:
                    // Secondary local-human seats share the host device/UID.
                    // They are NOT a second copy of the host's Steam profile.
                    SetSeat(
                        i,
                        T("lobby.online.local_player_number",
                            "LOCAL PLAYER {0}", i + 1),
                        T(
                            "lobby.online.no_ready_required",
                            "LOCAL • NO READY REQUIRED"));
                    break;








                case AtlasLobbySeatMode.Bot:
                    SetSeat(
                        i,
                        T(
                            "lobby.online.local_bot_number",
                            "LOCAL BOT {0}",
                            i + 1),
                        T(
                            "lobby.online.bot_no_ready",
                            "BOT • NO READY"));
                    break;








                case AtlasLobbySeatMode.RemoteHuman:
                    bool isLocal =
                        !string.IsNullOrWhiteSpace(backendLocalAccountId) &&
                        string.Equals(
                            member.AccountId,
                            backendLocalAccountId,
                            StringComparison.Ordinal);








                    string name =
                        string.IsNullOrWhiteSpace(member.DisplayName)
                            ? T(
                                "lobby.online.remote_player",
                                "ONLINE PLAYER")
                            : member.DisplayName;








                    if (isLocal)
                    {
                        name += " (YOU)";
                    }








                    SetSeat(
                        i,
                        name,
                        member.IsReadyFor(revision)
                            ? T(
                                "lobby.online.ready_for_revision",
                                "READY • REV {0}",
                                revision)
                            : T(
                                "lobby.online.not_ready",
                                "NOT READY"));
                    break;








                case AtlasLobbySeatMode.OpenOnline:
                default:
                    SetSeat(
                        i,
                        T(
                            "lobby.online.waiting_online",
                            "WAITING FOR ONLINE PLAYER"),
                        T(
                            "lobby.online.open_online_seat",
                            "OPEN ONLINE SEAT"));
                    break;
            }
        }
    }








    private static bool BackendSnapshotUiEquivalent(
        AtlasLobbySnapshot left,
        AtlasLobbySnapshot right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }








        if (left == null ||
            right == null)
        {
            return false;
        }








        if (!string.Equals(
                left.LobbyId,
                right.LobbyId,
                StringComparison.Ordinal) ||
            left.LifecycleState != right.LifecycleState ||
            !string.Equals(
                left.HostAccountId,
                right.HostAccountId,
                StringComparison.Ordinal) ||
            left.SettingsRevision !=
                right.SettingsRevision ||
            !string.Equals(
                left.MatchId,
                right.MatchId,
                StringComparison.Ordinal) ||
            !string.Equals(
                left.StartEventId,
                right.StartEventId,
                StringComparison.Ordinal) ||
            left.StartCountdownEndsAtEpochMs !=
                right.StartCountdownEndsAtEpochMs ||
            !LobbySettingsEquivalent(
                left.Settings,
                right.Settings))
        {
            return false;
        }








        for (int slot = 0; slot < 4; slot++)
        {
            AtlasLobbyMemberSnapshot leftMember =
                FindSnapshotMemberBySlot(
                    left,
                    slot);








            AtlasLobbyMemberSnapshot rightMember =
                FindSnapshotMemberBySlot(
                    right,
                    slot);








            if (!LobbyMemberEquivalent(
                    leftMember,
                    rightMember))
            {
                return false;
            }
        }








        return true;
    }








    private static bool LobbySettingsEquivalent(
        AtlasLobbySettings left,
        AtlasLobbySettings right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }








        if (left == null ||
            right == null)
        {
            return false;
        }








        return
            string.Equals(
                left.MapId,
                right.MapId,
                StringComparison.Ordinal) &&
            string.Equals(
                left.ThemeId,
                right.ThemeId,
                StringComparison.Ordinal) &&
            left.RoundLimit == right.RoundLimit &&
            left.MaxPlayers == right.MaxPlayers &&
            left.RequiredHumanPlayers ==
                right.RequiredHumanPlayers &&
            left.LocalHumanCount ==
                right.LocalHumanCount &&
            left.RemoteHumanCount ==
                right.RemoteHumanCount &&
            left.RemoteReadyRequiredCount ==
                right.RemoteReadyRequiredCount &&
            left.OpenOnlineSeatCount ==
                right.OpenOnlineSeatCount &&
            left.BotCount == right.BotCount &&
            left.BalancedDevelopment ==
                right.BalancedDevelopment &&
            left.DoublesEnabled ==
                right.DoublesEnabled &&
            left.TripleDoublePenaltyEnabled ==
                right.TripleDoublePenaltyEnabled;
    }








    private static bool LobbyMemberEquivalent(
        AtlasLobbyMemberSnapshot left,
        AtlasLobbyMemberSnapshot right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }








        if (left == null ||
            right == null)
        {
            return false;
        }








        return
            left.SlotIndex == right.SlotIndex &&
            left.Active == right.Active &&
            left.SeatMode == right.SeatMode &&
            left.IsHumanSeat == right.IsHumanSeat &&
            string.Equals(
                left.AccountId,
                right.AccountId,
                StringComparison.Ordinal) &&
            string.Equals(
                left.LocalOwnerAccountId,
                right.LocalOwnerAccountId,
                StringComparison.Ordinal) &&
            string.Equals(
                left.DisplayName,
                right.DisplayName,
                StringComparison.Ordinal) &&
            left.IsHost == right.IsHost &&
            left.ControllerKind ==
                right.ControllerKind &&
            left.ConnectionState ==
                right.ConnectionState &&
            left.ReadyForRevision ==
                right.ReadyForRevision &&
            left.RequiresReady ==
                right.RequiresReady;
    }








    private static AtlasLobbyMemberSnapshot FindSnapshotMemberBySlot(
        AtlasLobbySnapshot snapshot,
        int slotIndex)
    {
        if (snapshot == null ||
            snapshot.Members == null)
        {
            return null;
        }








        for (int i = 0;
             i < snapshot.Members.Count;
             i++)
        {
            AtlasLobbyMemberSnapshot member =
                snapshot.Members[i];








            if (member != null &&
                member.SlotIndex == slotIndex)
            {
                return member;
            }
        }








        return null;
    }








    public void ApplyBackendSnapshot(
        AtlasLobbySnapshot snapshot,
        string rawRoomCode,
        string localAccountId)
    {
        if (snapshot == null)
        {
            return;
        }








        privateMode = true;
        roomActive = true;
        if (snapshot != null && snapshot.Settings != null &&
            !(localIsHost && hostConfigurationDirty))
        {
            selectedBoardThemeId = string.IsNullOrEmpty(snapshot.Settings.BoardThemeId)
                ? "default_board" : snapshot.Settings.BoardThemeId;
            if (boardThemePicker != null)
                boardThemePicker.SelectWithoutNotify(selectedBoardThemeId);
            AtlasBoardBoardThemeVisuals.ApplySelection(selectedBoardThemeId);
        }


        AtlasLobbySnapshot previousSnapshot = backendSnapshot;
        backendSnapshot = snapshot;
        backendLocalAccountId =
            localAccountId ?? string.Empty;


        UpdateSeatIdentityPresentationCache(
            previousSnapshot,
            snapshot);








        if (!string.IsNullOrWhiteSpace(rawRoomCode))
        {
            string sanitized = SanitizeCode(rawRoomCode);
            if (sanitized.Length == 6)
            {
                roomCode = sanitized;
            }
        }








        settingsRevision =
            Mathf.Max(1, snapshot.SettingsRevision);








        requiredHumanPlayers =
            snapshot.Settings != null
                ? Mathf.Clamp(
                    snapshot.Settings.RequiredHumanPlayers,
                    1,
                    4)
                : 1;








        localIsHost =
            !string.IsNullOrWhiteSpace(backendLocalAccountId) &&
            string.Equals(
                snapshot.HostAccountId,
                backendLocalAccountId,
                StringComparison.Ordinal);








        AtlasLobbyMemberSnapshot localMember =
            snapshot.Members != null
                ? snapshot.Members.FirstOrDefault(
                    member =>
                        member != null &&
                        string.Equals(
                            member.AccountId,
                            backendLocalAccountId,
                            StringComparison.Ordinal))
                : null;








        readyForRevision =
            localMember != null
                ? localMember.ReadyForRevision
                : 0;








        SetActive(roomEntryOverlay, false);
        SetActive(privateOnlineRoot, true);
        SetActive(roomPanel, true);








        if (localIsHost)
        {
            ConfigureHostLobbyPreview();
        }
        else
        {
            ConfigureGuestLobbyPreview();
            RefreshGuestSeats();
        }








        SetText(
            roomStateText,
            snapshot.LifecycleState ==
            AtlasRoomLifecycleState.Starting
                ? T("lobby.online.starting", "STARTING...")
                : string.Empty);








        RefreshRoomHeader();
        RefreshGameSettingsAccessUi();
    }








    public void RefreshLocalizedText()
    {
        if (!privateMode)
        {
            return;
        }








        ResetCopyButtonText();








        if (roomActive)
        {
            RefreshRoomHeader();








            if (localIsHost)
            {
                RefreshHostSeatRows();
                RefreshHostStartAvailability();
            }
            else
            {
                RefreshGuestSeats();
            }
        }
    }








    private bool HasRemoteHumanInRemovedSlots(
        int requestedPlayerCount)
    {
        if (backendSnapshot == null ||
            backendSnapshot.Members == null)
        {
            return false;
        }








        foreach (AtlasLobbyMemberSnapshot member in
                 backendSnapshot.Members)
        {
            if (member == null ||
                !member.Active ||
                member.SeatMode !=
                    AtlasLobbySeatMode.RemoteHuman)
            {
                continue;
            }








            if (member.SlotIndex >=
                requestedPlayerCount)
            {
                return true;
            }
        }








        return false;
    }








    private void ShowPlayerCountBlockedModal()
    {
        ShowLobbySafetyModal(
            LobbySafetyModalMode.Notice,
            T(
                "lobby.online.player_count_blocked_title",
                "PLAYER COUNT CANNOT BE REDUCED"),
            T(
                "lobby.online.player_count_blocked_body",
                "An online player is using a slot that would be removed. Remove that player from the lobby first."),
            T(
                "common.ok",
                "OK"),
            string.Empty);
    }








    private void ShowKickConfirmation(int slotIndex)
    {
        AtlasLobbyMemberSnapshot member =
            FindBackendMemberBySlot(
                slotIndex);








        if (member == null ||
            member.SeatMode !=
                AtlasLobbySeatMode.RemoteHuman)
        {
            return;
        }








        pendingKickSlot =
            slotIndex;








        string playerName =
            string.IsNullOrWhiteSpace(
                member.DisplayName)
                ? T(
                    "lobby.online.remote_player",
                    "ONLINE PLAYER")
                : member.DisplayName;








        ShowLobbySafetyModal(
            LobbySafetyModalMode.KickConfirm,
            T(
                "lobby.online.kick_title",
                "REMOVE PLAYER?"),
            T(
                "lobby.online.kick_body",
                "Remove {0} from this lobby?",
                playerName),
            T(
                "lobby.online.remove_player",
                "REMOVE"),
            T(
                "common.cancel",
                "CANCEL"));
    }








    private async System.Threading.Tasks.Task
        KickRemotePlayerAsync(int slotIndex)
    {
        if (!roomActive ||
            !localIsHost ||
            runtimeBridge == null ||
            backendBusy)
        {
            return;
        }








        backendBusy = true;








        if (lobbySafetyPrimaryButton != null)
        {
            lobbySafetyPrimaryButton.interactable =
                false;
        }








        if (lobbySafetySecondaryButton != null)
        {
            lobbySafetySecondaryButton.interactable =
                false;
        }








        SetText(
            lobbySafetyBodyText,
            T(
                "lobby.online.removing_player",
                "REMOVING PLAYER..."));








        try
        {
            AtlasLobbyOperationResult result =
                await runtimeBridge.KickMemberAsync(
                    slotIndex);








            if (!result.Success ||
                result.Snapshot == null)
            {
                SetText(
                    lobbySafetyBodyText,
                    AtlasBoardFrontendErrorCodes.PublicMessage(
                        AtlasBoardFrontendErrorCodes.LobbySeatConfigurationFailed));
                AtlasBoardFrontendErrorCodes.Report(
                    AtlasBoardFrontendErrorCodes.LobbySeatConfigurationFailed,
                    result.TechnicalMessage, "lobby_kick");








                if (lobbySafetyPrimaryButton != null)
                {
                    lobbySafetyPrimaryButton.interactable =
                        true;
                }








                SetText(
                    lobbySafetyPrimaryButtonText,
                    T(
                        "common.ok",
                        "OK"));








                lobbySafetyModalMode =
                    LobbySafetyModalMode.Notice;
                return;
            }








            pendingKickSlot = -1;
            HideLobbySafetyModal();








            ApplyBackendSnapshot(
                result.Snapshot,
                roomCode,
                runtimeBridge.CurrentAccountId);








            RefreshHostStartAvailability();
        }
        finally
        {
            backendBusy = false;








            if (lobbySafetySecondaryButton != null)
            {
                lobbySafetySecondaryButton.interactable =
                    true;
            }
        }
    }








    private void HandleLobbyAccessLost(
        string errorLocalizationKey)
    {
        if (!roomActive ||
            string.IsNullOrWhiteSpace(
                errorLocalizationKey))
        {
            return;
        }




        if (string.Equals(errorLocalizationKey,
                "lobby.error.closed", StringComparison.OrdinalIgnoreCase))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("ATB_RTDB_GUEST_ROOM_CLOSED menu_return=true " +
                "source=verified_backend_close", this);
#endif
            ResetPrivateState(false);
            mainMenuController ??= GetComponent<AtlasBoardMainMenuController>();
            mainMenuController?.ShowMainMenu();
            return;
        }




        if (!string.Equals(
                errorLocalizationKey,
                "lobby.error.kicked",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }








        backendBusy = false;
        hostConfigurationDirty = false;
        hostConfigurationSyncQueued = false;
        roomActive = false;








        ShowLobbySafetyModal(
            LobbySafetyModalMode.KickedNotice,
            T(
                "lobby.online.kicked_title",
                "REMOVED FROM LOBBY"),
            T(
                "lobby.online.kicked_body",
                "The host removed you from this lobby."),
            T(
                "common.ok",
                "OK"),
            string.Empty);
    }








    private void ApplyLobbySafetyModalLayout(
        bool hasSecondaryButton)
    {
        if (lobbySafetyTitleText != null)
        {
            lobbySafetyTitleText.alignment =
                TextAlignmentOptions.Center;
            lobbySafetyTitleText.rectTransform.anchoredPosition =
                new Vector2(0f, 112f);
            lobbySafetyTitleText.rectTransform.sizeDelta =
                new Vector2(610f, 58f);
        }








        if (lobbySafetyBodyText != null)
        {
            lobbySafetyBodyText.alignment =
                TextAlignmentOptions.Center;
            lobbySafetyBodyText.rectTransform.anchoredPosition =
                new Vector2(0f, 28f);
            lobbySafetyBodyText.rectTransform.sizeDelta =
                new Vector2(610f, 115f);
        }








        if (lobbySafetyPrimaryButton != null)
        {
            RectTransform primaryRect =
                lobbySafetyPrimaryButton.GetComponent<RectTransform>();








            if (primaryRect != null)
            {
                primaryRect.anchoredPosition =
                    new Vector2(
                        hasSecondaryButton ? -105f : 0f,
                        -125f);
            }
        }








        if (lobbySafetySecondaryButton != null)
        {
            RectTransform secondaryRect =
                lobbySafetySecondaryButton.GetComponent<RectTransform>();








            if (secondaryRect != null)
            {
                secondaryRect.anchoredPosition =
                    new Vector2(105f, -125f);
            }
        }
    }








    private void ShowLobbySafetyModal(
        LobbySafetyModalMode mode,
        string title,
        string body,
        string primary,
        string secondary)
    {
        lobbySafetyModalMode =
            mode;








        SetText(
            lobbySafetyTitleText,
            title);








        SetText(
            lobbySafetyBodyText,
            body);








        SetText(
            lobbySafetyCountdownText,
            string.Empty);








        SetText(
            lobbySafetyPrimaryButtonText,
            primary);








        SetText(
            lobbySafetySecondaryButtonText,
            secondary);








        ApplyLobbySafetyModalLayout(
            !string.IsNullOrWhiteSpace(secondary));








        SetActive(
            lobbySafetyPrimaryButton != null
                ? lobbySafetyPrimaryButton.gameObject
                : null,
            !string.IsNullOrWhiteSpace(primary));








        SetActive(
            lobbySafetySecondaryButton != null
                ? lobbySafetySecondaryButton.gameObject
                : null,
            !string.IsNullOrWhiteSpace(secondary));








        if (lobbySafetyPrimaryButton != null)
        {
            lobbySafetyPrimaryButton.interactable =
                true;
        }








        if (lobbySafetySecondaryButton != null)
        {
            lobbySafetySecondaryButton.interactable =
                true;
        }








        SetActive(
            lobbySafetyOverlay,
            true);








        if (lobbySafetyOverlay != null)
        {
            lobbySafetyOverlay.transform.SetAsLastSibling();
        }
    }








    private void HideLobbySafetyModal()
    {
        if (lobbySafetyModalMode ==
            LobbySafetyModalMode.StartCountdown)
        {
            return;
        }








        lobbySafetyModalMode =
            LobbySafetyModalMode.None;








        pendingKickSlot = -1;








        SetActive(
            lobbySafetyOverlay,
            false);
    }








    private void HandleLobbySafetyPrimary()
    {
        switch (lobbySafetyModalMode)
        {
            case LobbySafetyModalMode.KickConfirm:
                int slot =
                    pendingKickSlot;








                if (slot >= 0)
                {
                    _ = KickRemotePlayerAsync(slot);
                }
                break;








            case LobbySafetyModalMode.KickedNotice:
                SetActive(
                    lobbySafetyOverlay,
                    false);








                lobbySafetyModalMode =
                    LobbySafetyModalMode.None;








                runtimeBridge?.ClearLocalLobbyState();








                mainMenuController ??=
                    GetComponent<
                        AtlasBoardMainMenuController>();








                mainMenuController?.ShowMainMenu();
                break;








            case LobbySafetyModalMode.Notice:
            default:
                HideLobbySafetyModal();
                break;
        }
    }








    private void HandleLobbySafetySecondary()
    {
        if (lobbySafetyModalMode ==
            LobbySafetyModalMode.KickConfirm)
        {
            HideLobbySafetyModal();
        }
    }








    private void BeginAuthoritativeStartCountdown()
    {
        if (countdownActive ||
            backendSnapshot == null ||
            backendSnapshot.LifecycleState !=
                AtlasRoomLifecycleState.Starting)
        {
            return;
        }








        countdownActive = true;








        if (countdownCoroutine != null)
        {
            StopCoroutine(
                countdownCoroutine);
        }








        countdownCoroutine =
            StartCoroutine(
                AuthoritativeStartCountdownRoutine());
    }








    private IEnumerator
        AuthoritativeStartCountdownRoutine()
    {
        lobbySafetyModalMode =
            LobbySafetyModalMode.StartCountdown;








        SetActive(
            lobbySafetyOverlay,
            true);








        if (lobbySafetyOverlay != null)
        {
            lobbySafetyOverlay.transform.SetAsLastSibling();
        }








        SetText(
            lobbySafetyTitleText,
            T(
                "lobby.online.match_starting_title",
                "MATCH STARTING"));








        SetText(
            lobbySafetyBodyText,
            T(
                "lobby.online.match_starting_body",
                "Lobby locked. Remaining open seats are filled by bots."));








        SetActive(
            lobbySafetyPrimaryButton != null
                ? lobbySafetyPrimaryButton.gameObject
                : null,
            false);








        SetActive(
            lobbySafetySecondaryButton != null
                ? lobbySafetySecondaryButton.gameObject
                : null,
            false);








        SetHostSettingsInteractable(false);
        SetHostSeatActionUIVisible(false);








        Button start =
            legacyStartButton != null
                ? legacyStartButton.GetComponent<Button>()
                : null;








        if (start != null)
        {
            start.interactable = false;
        }








        long countdownEndsAtEpochMs =
            backendSnapshot != null
                ? backendSnapshot.StartCountdownEndsAtEpochMs
                : 0L;








        if (countdownEndsAtEpochMs <= 0L)
        {
            countdownEndsAtEpochMs =
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() +
                5000L;
        }








        int lastDisplayedSecond = -1;








        while (true)
        {
            long remainingMs =
                countdownEndsAtEpochMs -
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();








            if (remainingMs <= 0L)
            {
                break;
            }








            int seconds =
                Mathf.Clamp(
                    Mathf.CeilToInt(
                        remainingMs / 1000f),
                    1,
                    5);








            if (seconds != lastDisplayedSecond)
            {
                lastDisplayedSecond = seconds;
                SetText(
                    lobbySafetyCountdownText,
                    seconds.ToString());
            }








            yield return new WaitForSecondsRealtime(
                0.1f);
        }








        SetText(
            lobbySafetyCountdownText,
            string.Empty);








        SetActive(
            lobbySafetyOverlay,
            false);








        lobbySafetyModalMode =
            LobbySafetyModalMode.None;
        countdownActive = false;
        countdownCoroutine = null;








        PrepareLegacyControlsForOnlineMatchStart();








        AtlasBoardTurnDiceNetworkCoordinator networkCoordinator =
            GetComponent<
                AtlasBoardTurnDiceNetworkCoordinator>();








        if (networkCoordinator != null)
        {
            networkCoordinator
                .PrepareForAuthoritativeMatchStart(
                    backendSnapshot);
        }








        mainMenuController ??=
            GetComponent<
                AtlasBoardMainMenuController>();








        mainMenuController?.
            StartMatchAfterPrivateBackendAuthorization();








        SetText(
            roomStateText,
            T(
                "lobby.online.authoritative_start_ready",
                "START CONFIRMED"));








        Debug.Log(
            "AtlasBoard Phase 5B authoritative online Start confirmed. " +
            "Host drives Turn/Dice simulation; remote clients enter follower presentation.",
            this);
    }








    private void ResetLobbySafetyModalState()
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(
                countdownCoroutine);








            countdownCoroutine = null;
        }








        countdownActive = false;
        lobbySafetyModalMode =
            LobbySafetyModalMode.None;
        pendingKickSlot = -1;








        SetActive(
            lobbySafetyOverlay,
            false);
    }








    private void ResetPrivateState(bool preserveEntry)
    {
        privateMode = preserveEntry;
        roomActive = false;
        localIsHost = false;
        backendSnapshot = null;
        backendLocalAccountId = string.Empty;
        selectedBoardThemeId = "default_board";
        AtlasBoardBoardThemeVisuals.ApplySelection("default_board");
        hostConfigurationDirty = false;
        hostConfigurationSyncQueued = false;
        hostConfigurationSyncAt = 0f;
        openSeatChoiceIndex = -1;
        roomCode = string.Empty;
        codeVisible = false;
        lastKnownLobbyPassword = string.Empty;
        lobbyPasswordVisible = false;
        settingsRevision = 1;
        readyForRevision = 0;
        requiredHumanPlayers = 1;
        voluntaryLeaveInFlight = false;
        nextGuestHostNameCheckAt = 0f;








        ResetLobbySafetyModalState();








        if (!preserveEntry &&
            runtimeBridge != null)
        {
            runtimeBridge.ClearLocalLobbyState();
        }








        SetActive(privateOnlineRoot, false);
        SetActive(roomPanel, false);








        if (!preserveEntry)
        {
            SetActive(roomEntryOverlay, false);
        }








        SetActive(
            readyButton != null
                ? readyButton.gameObject
                : null,
            false);








        SetActive(legacyStartButton, true);








        SetHostSeatActionUIVisible(false);
        ShowPrivateSeatPresentation(false);
        RestoreLegacyControlValues();
        RestoreRowsFromPlayerCount();
        SetHostSettingsInteractable(true);
        SetPlayerTypeInteractable(0, true);








        if (!preserveEntry &&
            roomEntryExternalRoot == null)
        {
            RestoreRoomEntryHome();
        }
    }








    private void BindRuntimeEvents()
    {
        // The visible Game Lobby dropdown is shared with Main Menu, but some
        // scenes configure it only after opening the private/public lobby.
        if (hostSettingsDropdowns != null && hostSettingsDropdowns.Length > 0)
            AtlasBoardPremiumMapDropdownLabels.EnsureOptions(hostSettingsDropdowns[0]);
        if (eventsBound)
        {
            return;
        }








        AddButton(createRoomButton, CreateRoomPreview);
        AddButton(joinRoomButton, JoinRoomPreview);
        AddButton(cancelEntryButton, CancelRoomEntry);
        AddButton(codeVisibilityButton, ToggleCodeVisibility);
        AddButton(copyCodeButton, CopyRoomCode);
        AddButton(readyButton, ToggleReady);
        AddButton(gameSettingsOpenButton, OpenLobbyGameSettings);
        AddButton(gameSettingsCloseButton, CloseLobbyGameSettings);
        AddButton(lobbyPasswordApplyButton, ApplyLobbyPassword);
        AddButton(lobbyPasswordVisibilityButton, ToggleLobbyPasswordVisibility);
        AddButton(lobbyPasswordCopyButton, CopyLobbyPassword);
        AddButton(
            lobbySafetyPrimaryButton,
            HandleLobbySafetyPrimary);
        AddButton(
            lobbySafetySecondaryButton,
            HandleLobbySafetySecondary);








        if (hostSettingsDropdowns != null)
        {
            foreach (TMP_Dropdown dropdown in hostSettingsDropdowns)
            {
                if (dropdown != null)
                {
                    dropdown.onValueChanged.AddListener(
                        HandleHostSettingsDropdownChanged);
                }
            }
        }








        if (hostSettingsToggles != null)
        {
            foreach (Toggle toggle in hostSettingsToggles)
            {
                if (toggle != null)
                {
                    toggle.onValueChanged.AddListener(
                        HandleHostToggleChanged);
                }
            }
        }








        for (int i = 0; i < 4; i++)
        {
            int captured = i;








            seatAddActions[i] =
                () => OpenSeatChoice(captured);








            seatLocalActions[i] =
                () => AddLocalPlayerToSeat(captured);








            seatBotActions[i] =
                () => AddBotToSeat(captured);








            seatRemoveActions[i] =
                () => RemoveLocalAssignmentFromSeat(captured);








            AddButton(
                GetButtonArrayItem(
                    privateSeatAddButtons,
                    i),
                seatAddActions[i]);








            AddButton(
                GetButtonArrayItem(
                    privateSeatLocalButtons,
                    i),
                seatLocalActions[i]);








            AddButton(
                GetButtonArrayItem(
                    privateSeatBotButtons,
                    i),
                seatBotActions[i]);








            AddButton(
                GetButtonArrayItem(
                    privateSeatRemoveButtons,
                    i),
                seatRemoveActions[i]);
        }








        AtlasBoardLocalizationManager.LanguageChanged -= RefreshLocalizedText;
        AtlasBoardLocalizationManager.LanguageChanged += RefreshLocalizedText;








        eventsBound = true;
    }








    private void UnbindRuntimeEvents()
    {
        if (!eventsBound)
        {
            return;
        }








        RemoveButton(createRoomButton, CreateRoomPreview);
        RemoveButton(joinRoomButton, JoinRoomPreview);
        RemoveButton(cancelEntryButton, CancelRoomEntry);
        RemoveButton(codeVisibilityButton, ToggleCodeVisibility);
        RemoveButton(copyCodeButton, CopyRoomCode);
        RemoveButton(readyButton, ToggleReady);
        RemoveButton(gameSettingsOpenButton, OpenLobbyGameSettings);
        RemoveButton(gameSettingsCloseButton, CloseLobbyGameSettings);
        RemoveButton(lobbyPasswordApplyButton, ApplyLobbyPassword);
        RemoveButton(lobbyPasswordVisibilityButton, ToggleLobbyPasswordVisibility);
        RemoveButton(lobbyPasswordCopyButton, CopyLobbyPassword);
        RemoveButton(
            lobbySafetyPrimaryButton,
            HandleLobbySafetyPrimary);
        RemoveButton(
            lobbySafetySecondaryButton,
            HandleLobbySafetySecondary);








        if (hostSettingsDropdowns != null)
        {
            foreach (TMP_Dropdown dropdown in hostSettingsDropdowns)
            {
                if (dropdown != null)
                {
                    dropdown.onValueChanged.RemoveListener(
                        HandleHostSettingsDropdownChanged);
                }
            }
        }








        if (hostSettingsToggles != null)
        {
            foreach (Toggle toggle in hostSettingsToggles)
            {
                if (toggle != null)
                {
                    toggle.onValueChanged.RemoveListener(
                        HandleHostToggleChanged);
                }
            }
        }








        for (int i = 0; i < 4; i++)
        {
            RemoveButton(
                GetButtonArrayItem(
                    privateSeatAddButtons,
                    i),
                seatAddActions[i]);








            RemoveButton(
                GetButtonArrayItem(
                    privateSeatLocalButtons,
                    i),
                seatLocalActions[i]);








            RemoveButton(
                GetButtonArrayItem(
                    privateSeatBotButtons,
                    i),
                seatBotActions[i]);








            RemoveButton(
                GetButtonArrayItem(
                    privateSeatRemoveButtons,
                    i),
                seatRemoveActions[i]);
        }








        eventsBound = false;
    }








    private void ShowPrivateSeatPresentation(bool privatePresentation)
    {
        SetObjectsActive(
            legacyPlayerLabels,
            !privatePresentation);








        SetObjectsActive(
            legacyPlayerStatuses,
            !privatePresentation);








        SetObjectsActive(
            legacyPlayerDropdowns,
            !privatePresentation);








        if (privateSeatNames != null)
        {
            foreach (TMP_Text text in privateSeatNames)
            {
                if (text != null)
                {
                    text.gameObject.SetActive(privatePresentation);
                }
            }
        }








        if (privateSeatStatuses != null)
        {
            foreach (TMP_Text text in privateSeatStatuses)
            {
                if (text != null)
                {
                    text.gameObject.SetActive(privatePresentation);
                }
            }
        }








        if (!privatePresentation)
        {
            SetHostSeatActionUIVisible(false);
        }
    }








    private void SetHostSettingsInteractable(bool value)
    {
        if (hostSettingsDropdowns != null)
        {
            foreach (TMP_Dropdown dropdown in hostSettingsDropdowns)
            {
                if (dropdown != null)
                {
                    dropdown.interactable = value;
                }
            }
        }








        if (hostSettingsToggles != null)
        {
            foreach (Toggle toggle in hostSettingsToggles)
            {
                if (toggle != null)
                {
                    toggle.interactable = value;
                }
            }
        }
    }








    private int GetPlayerCount()
    {
        if (playerCountDropdown == null ||
            playerCountDropdown.options == null ||
            playerCountDropdown.options.Count == 0)
        {
            return 2;
        }








        string text =
            playerCountDropdown.options[
                Mathf.Clamp(
                    playerCountDropdown.value,
                    0,
                    playerCountDropdown.options.Count - 1)]
            .text;








        return int.TryParse(text, out int parsed)
            ? Mathf.Clamp(parsed, 2, 4)
            : 2;
    }








    private bool GetPlayerTypeIsBot(int index)
    {
        TMP_Dropdown dropdown = GetPlayerTypeDropdown(index);








        if (dropdown == null)
        {
            return index > 0;
        }








        return dropdown.value == 1;
    }








    private void SetPlayerType(int index, bool bot)
    {
        TMP_Dropdown dropdown = GetPlayerTypeDropdown(index);








        if (dropdown == null)
        {
            return;
        }








        int desired = bot ? 1 : 0;








        if (dropdown.value != desired)
        {
            dropdown.SetValueWithoutNotify(desired);
            dropdown.RefreshShownValue();
        }
    }








    private void SetPlayerTypeInteractable(int index, bool value)
    {
        TMP_Dropdown dropdown = GetPlayerTypeDropdown(index);








        if (dropdown != null)
        {
            dropdown.interactable = value;
        }
    }








    private TMP_Dropdown GetPlayerTypeDropdown(int index)
    {
        if (legacyPlayerDropdowns == null ||
            index < 0 ||
            index >= legacyPlayerDropdowns.Length ||
            legacyPlayerDropdowns[index] == null)
        {
            return null;
        }








        return legacyPlayerDropdowns[index].GetComponent<TMP_Dropdown>();
    }








    private void RestoreRowsFromPlayerCount()
    {
        if (playerRows == null)
        {
            return;
        }








        int count = GetPlayerCount();








        for (int i = 0; i < playerRows.Length; i++)
        {
            SetActive(playerRows[i], i < count);
        }
    }








    private void ForceAllPlayerRowsVisible()
    {
        if (playerRows == null)
        {
            return;
        }








        foreach (GameObject row in playerRows)
        {
            SetActive(row, true);
        }
    }








    private void SetSeatTextVisible(
        int index,
        bool visible)
    {
        if (privateSeatNames != null &&
            index >= 0 &&
            index < privateSeatNames.Length &&
            privateSeatNames[index] != null)
        {
            privateSeatNames[index].gameObject.SetActive(
                visible);
        }








        if (privateSeatStatuses != null &&
            index >= 0 &&
            index < privateSeatStatuses.Length &&
            privateSeatStatuses[index] != null)
        {
            privateSeatStatuses[index].gameObject.SetActive(
                visible);
        }
    }








    private void SetSeat(int index, string name, string status)
    {
        if (privateSeatNames != null &&
            index >= 0 &&
            index < privateSeatNames.Length)
        {
            SetText(privateSeatNames[index], name);
        }








        if (privateSeatStatuses != null &&
            index >= 0 &&
            index < privateSeatStatuses.Length)
        {
            SetText(privateSeatStatuses[index], status);
        }
    }








    private static void SetObjectsActive(GameObject[] objects, bool value)
    {
        if (objects == null)
        {
            return;
        }








        foreach (GameObject target in objects)
        {
            SetActive(target, value);
        }
    }








    private static void AddButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }








    private static void RemoveButton(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }








    private static void SetActive(GameObject target, bool value)
    {
        if (target != null &&
            target.activeSelf != value)
        {
            target.SetActive(value);
        }
    }








    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value ?? string.Empty;
        }
    }








    private static string SanitizeCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }








        return new string(
            value.Where(char.IsDigit)
                .Take(6)
                .ToArray());
    }








    private static string T(
        string key,
        string fallback,
        params object[] args)
    {
        string value = AtlasBoardL.T(key, args);








        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(
                value,
                key,
                StringComparison.Ordinal))
        {
            return args != null && args.Length > 0
                ? string.Format(fallback, args)
                : fallback;
        }








        return value;
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








#if UNITY_EDITOR
    public void EditorConfigure(
        GameObject newMainMenuRoot,
        GameObject newLobbyRoot,
        GameObject newPrivateOnlineRoot,
        GameObject newRoomEntryOverlay,
        GameObject newRoomPanel,
        GameObject newLegacyStartButton,
        Button newCreateRoomButton,
        TMP_InputField newJoinCodeInput,
        Button newJoinRoomButton,
        Button newCancelEntryButton,
        TMP_Text newEntryStatusText,
        TMP_Text newRoomCodeText,
        TMP_Text newRoomStateText,
        TMP_Text newRevisionText,
        Button newCodeVisibilityButton,
        TMP_Text newCodeVisibilityButtonText,
        Button newCopyCodeButton,
        TMP_Text newCopyCodeButtonText,
        Button newReadyButton,
        TMP_Text newReadyButtonText,
        TMP_Dropdown newPlayerCountDropdown,
        TMP_Dropdown[] newHostSettingsDropdowns,
        Toggle[] newHostSettingsToggles,
        GameObject[] newPlayerRows,
        GameObject[] newLegacyPlayerLabels,
        GameObject[] newLegacyPlayerStatuses,
        GameObject[] newLegacyPlayerDropdowns,
        TMP_Text[] newPrivateSeatNames,
        TMP_Text[] newPrivateSeatStatuses,
        Button[] newPrivateSeatAddButtons,
        GameObject[] newPrivateSeatChoicePanels,
        Button[] newPrivateSeatLocalButtons,
        Button[] newPrivateSeatBotButtons,
        Button[] newPrivateSeatRemoveButtons)
    {
        mainMenuRoot = newMainMenuRoot;
        lobbyRoot = newLobbyRoot;
        privateOnlineRoot = newPrivateOnlineRoot;
        roomEntryOverlay = newRoomEntryOverlay;
        roomPanel = newRoomPanel;
        legacyStartButton = newLegacyStartButton;








        createRoomButton = newCreateRoomButton;
        joinCodeInput = newJoinCodeInput;
        joinRoomButton = newJoinRoomButton;
        cancelEntryButton = newCancelEntryButton;
        entryStatusText = newEntryStatusText;








        roomCodeText = newRoomCodeText;
        roomStateText = newRoomStateText;
        revisionText = newRevisionText;
        codeVisibilityButton = newCodeVisibilityButton;
        codeVisibilityButtonText = newCodeVisibilityButtonText;
        copyCodeButton = newCopyCodeButton;
        copyCodeButtonText = newCopyCodeButtonText;








        readyButton = newReadyButton;
        readyButtonText = newReadyButtonText;








        playerCountDropdown = newPlayerCountDropdown;
        hostSettingsDropdowns =
            newHostSettingsDropdowns ?? Array.Empty<TMP_Dropdown>();
        hostSettingsToggles =
            newHostSettingsToggles ?? Array.Empty<Toggle>();








        playerRows =
            newPlayerRows ?? Array.Empty<GameObject>();
        legacyPlayerLabels =
            newLegacyPlayerLabels ?? Array.Empty<GameObject>();
        legacyPlayerStatuses =
            newLegacyPlayerStatuses ?? Array.Empty<GameObject>();
        legacyPlayerDropdowns =
            newLegacyPlayerDropdowns ?? Array.Empty<GameObject>();
        privateSeatNames =
            newPrivateSeatNames ?? Array.Empty<TMP_Text>();
        privateSeatStatuses =
            newPrivateSeatStatuses ?? Array.Empty<TMP_Text>();








        privateSeatAddButtons =
            newPrivateSeatAddButtons ?? Array.Empty<Button>();
        privateSeatChoicePanels =
            newPrivateSeatChoicePanels ?? Array.Empty<GameObject>();
        privateSeatLocalButtons =
            newPrivateSeatLocalButtons ?? Array.Empty<Button>();
        privateSeatBotButtons =
            newPrivateSeatBotButtons ?? Array.Empty<Button>();
        privateSeatRemoveButtons =
            newPrivateSeatRemoveButtons ?? Array.Empty<Button>();
    }








    public void EditorConfigureSafetyUX(
        GameObject newLobbySafetyOverlay,
        TMP_Text newLobbySafetyTitleText,
        TMP_Text newLobbySafetyBodyText,
        TMP_Text newLobbySafetyCountdownText,
        Button newLobbySafetyPrimaryButton,
        TMP_Text newLobbySafetyPrimaryButtonText,
        Button newLobbySafetySecondaryButton,
        TMP_Text newLobbySafetySecondaryButtonText)
    {
        lobbySafetyOverlay =
            newLobbySafetyOverlay;
        lobbySafetyTitleText =
            newLobbySafetyTitleText;
        lobbySafetyBodyText =
            newLobbySafetyBodyText;
        lobbySafetyCountdownText =
            newLobbySafetyCountdownText;
        lobbySafetyPrimaryButton =
            newLobbySafetyPrimaryButton;
        lobbySafetyPrimaryButtonText =
            newLobbySafetyPrimaryButtonText;
        lobbySafetySecondaryButton =
            newLobbySafetySecondaryButton;
        lobbySafetySecondaryButtonText =
            newLobbySafetySecondaryButtonText;
    }
#endif
}