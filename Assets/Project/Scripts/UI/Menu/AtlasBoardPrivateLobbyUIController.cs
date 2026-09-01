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
    private bool backendBusy;

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

    public void ShowRoomEntryFromMainMenu()
    {
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

        RefreshLocalizedText();
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
                string message = AtlasBoardL.T(result.ErrorLocalizationKey);
                if (string.IsNullOrWhiteSpace(message) || message == result.ErrorLocalizationKey)
                {
                    message = result.TechnicalMessage;
                }
                SetText(lobbyPasswordStatusText, message);
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
        SetActive(roomEntryOverlay, false);
        ResetPrivateState(false);
    }

    private async void CreateRoomPreview()
    {
        if (!privateMode || backendBusy)
        {
            return;
        }

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

            if (!result.Success ||
                result.Snapshot == null)
            {
                ShowEntryBackendError(result);
                return;
            }

            roomActive = true;
            localIsHost = true;
            codeVisible = false;
            roomCode = result.RoomCode ?? string.Empty;
            backendLocalAccountId = runtimeBridge.CurrentAccountId;

            SetActive(roomEntryOverlay, false);

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

            mainMenuController ??=
                GetComponent<AtlasBoardMainMenuController>();

            mainMenuController?.OpenPrivateLobbyAfterRoomChoice();

            SetActive(privateOnlineRoot, true);
            SetActive(roomPanel, true);

            ApplyBackendSnapshot(
                result.Snapshot,
                code,
                runtimeBridge.CurrentAccountId);

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
            TextOverflowModes.Ellipsis;
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
        if (!roomActive ||
            !localIsHost)
        {
            return;
        }

        QueueHostConfigurationSync();
    }

    private void OpenSeatChoice(int index)
    {
        if (!roomActive ||
            !localIsHost ||
            index <= 0 ||
            index >= GetPlayerCount() ||
            hostSeatModes[index] != PrivateSeatMode.OpenOnline)
        {
            return;
        }

        CloseAllSeatChoicePanels();

        openSeatChoiceIndex =
            index;

        SetSeatTextVisible(
            index,
            false);

        SetActive(
            GetChoicePanel(index),
            true);

        Button add =
            GetAddButton(index);

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
            index <= 0 ||
            index >= GetPlayerCount())
        {
            return;
        }

        if (hostSeatModes[index] ==
            PrivateSeatMode.RemoteHuman)
        {
            ShowKickConfirmation(index);
            return;
        }

        ResolveSeat(
            index,
            PrivateSeatMode.OpenOnline);
    }

    private void ResolveSeat(
        int index,
        PrivateSeatMode mode)
    {
        if (index <= 0 ||
            index >= hostSeatModes.Length)
        {
            return;
        }

        hostSeatModes[index] = mode;

        CloseAllSeatChoicePanels();
        SyncHostSeatModesToLegacyControls();
        RefreshHostSeatRows();

        requiredHumanPlayers =
            CountResolvedLocalHumans();

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
        if (BackendSnapshotUiEquivalent(
                backendSnapshot,
                snapshot))
        {
            backendSnapshot = snapshot;
            return;
        }

        ApplyBackendSnapshot(
            snapshot,
            roomCode,
            runtimeBridge != null
                ? runtimeBridge.CurrentAccountId
                : backendLocalAccountId);

        if (snapshot.LifecycleState ==
            AtlasRoomLifecycleState.Starting)
        {
            BeginAuthoritativeStartCountdown();
        }
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

        return selection;
    }

    private string[] BuildBackendSeatPolicies(
        int playerCount)
    {
        string[] policies =
        {
            "local_human",
            "inactive",
            "inactive",
            "inactive"
        };

        int total =
            Mathf.Clamp(playerCount, 2, 4);

        for (int i = 1; i < 4; i++)
        {
            if (i >= total)
            {
                policies[i] = "inactive";
                continue;
            }

            policies[i] =
                hostSeatModes[i] switch
                {
                    PrivateSeatMode.LocalHuman => "local_human",
                    PrivateSeatMode.Bot => "bot",
                    PrivateSeatMode.RemoteHuman => "online",
                    _ => "online"
                };
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

    private void ShowEntryBackendError(
        AtlasLobbyOperationResult result)
    {
        string key =
            result != null
                ? result.ErrorLocalizationKey
                : "lobby.error.unknown";

        string localized =
            AtlasBoardL.T(key);

        if (string.IsNullOrWhiteSpace(localized) ||
            localized == key)
        {
            string technical =
                result != null
                    ? result.TechnicalMessage
                    : string.Empty;

            if (string.Equals(
                    technical,
                    "INVALID_ROOM_CODE",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    key,
                    "lobby.error.invalid_room_code",
                    StringComparison.OrdinalIgnoreCase))
            {
                localized =
                    T(
                        "lobby.error.invalid_room_code",
                        "Invalid room code.");
            }
            else
            {
                localized =
                    !string.IsNullOrWhiteSpace(technical)
                        ? technical
                        : "Lobby backend request failed.";
            }
        }

        SetText(entryStatusText, localized);

        Debug.LogWarning(
            "AtlasBoard lobby backend request failed: " +
            (result != null ? result.TechnicalMessage : "unknown"),
            this);
    }

    private void ShowRoomBackendError(
        AtlasLobbyOperationResult result)
    {
        string key =
            result != null
                ? result.ErrorLocalizationKey
                : "lobby.error.unknown";

        string localized =
            AtlasBoardL.T(key);

        if (string.IsNullOrWhiteSpace(localized) ||
            localized == key)
        {
            localized =
                result != null &&
                !string.IsNullOrWhiteSpace(result.TechnicalMessage)
                    ? result.TechnicalMessage
                    : "Lobby backend request failed.";
        }

        SetText(roomStateText, localized);

        Debug.LogWarning(
            "AtlasBoard lobby backend request failed: " +
            (result != null ? result.TechnicalMessage : "unknown"),
            this);
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

            if (hostSettingsToggles != null)
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
        hostSeatModes[0] =
            PrivateSeatMode.LocalHuman;

        for (int i = 1; i < hostSeatModes.Length; i++)
        {
            hostSeatModes[i] =
                PrivateSeatMode.OpenOnline;
        }
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

        ForceAllPlayerRowsVisible();

        for (int i = 0; i < 4; i++)
        {
            bool active =
                i < total;

            SetActive(
                playerRows != null &&
                i < playerRows.Length
                    ? playerRows[i]
                    : null,
                active);

            if (!active)
            {
                SetHostSeatActionObjects(
                    i,
                    false,
                    false,
                    false);
                continue;
            }

            if (i == 0)
            {
                SetSeat(
                    i,
                    T(
                        "lobby.online.you",
                        "PLAYER (YOU)"),
                    T(
                        "lobby.online.host_local",
                        "HOST • LOCAL"));

                SetHostSeatActionObjects(
                    i,
                    false,
                    false,
                    false);
                continue;
            }

            switch (hostSeatModes[i])
            {
                case PrivateSeatMode.LocalHuman:
                    SetSeat(
                        i,
                        T(
                            "lobby.online.local_player_number",
                            "LOCAL PLAYER {0}",
                            i + 1),
                        T(
                            "lobby.online.no_ready_required",
                            "LOCAL • NO READY REQUIRED"));

                    SetHostSeatActionObjects(
                        i,
                        false,
                        false,
                        true);
                    break;

                case PrivateSeatMode.Bot:
                    SetSeat(
                        i,
                        T(
                            "lobby.online.local_bot_number",
                            "LOCAL BOT {0}",
                            i + 1),
                        T(
                            "lobby.online.bot_no_ready",
                            "BOT • NO READY"));

                    SetHostSeatActionObjects(
                        i,
                        false,
                        false,
                        true);
                    break;

                case PrivateSeatMode.RemoteHuman:
                    AtlasLobbyMemberSnapshot remote =
                        FindBackendMemberBySlot(i);

                    string remoteName =
                        remote != null &&
                        !string.IsNullOrWhiteSpace(remote.DisplayName)
                            ? remote.DisplayName
                            : T(
                                "lobby.online.remote_player",
                                "ONLINE PLAYER");

                    string remoteStatus =
                        remote != null &&
                        remote.IsReadyFor(settingsRevision)
                            ? T(
                                "lobby.online.ready_for_revision",
                                "READY • REV {0}",
                                settingsRevision)
                            : T(
                                "lobby.online.not_ready",
                                "NOT READY");

                    SetSeat(
                        i,
                        remoteName,
                        remoteStatus);

                    SetHostSeatActionObjects(
                        i,
                        false,
                        false,
                        true);
                    break;

                default:
                    SetSeat(
                        i,
                        T(
                            "lobby.online.waiting_online",
                            "WAITING FOR ONLINE PLAYER"),
                        T(
                            "lobby.online.open_online_seat",
                            "OPEN ONLINE SEAT"));

                    SetHostSeatActionObjects(
                        i,
                        true,
                        false,
                        false);
                    break;
            }
        }
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
            index <= 0 ||
            index >= GetPlayerCount() ||
            index >= hostSeatModes.Length ||
            hostSeatModes[index] !=
                PrivateSeatMode.OpenOnline)
        {
            openSeatChoiceIndex = -1;
            return;
        }

        GameObject panel =
            GetChoicePanel(index);

        Button add =
            GetAddButton(index);

        if (panel == null ||
            add == null)
        {
            openSeatChoiceIndex = -1;
            return;
        }

        openSeatChoiceIndex =
            index;

        SetSeatTextVisible(
            index,
            false);

        panel.SetActive(true);
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

        for (int i = 1; i < total; i++)
        {
            if (hostSeatModes[i] ==
                PrivateSeatMode.OpenOnline)
            {
                return true;
            }
        }

        return false;
    }

    private int CountResolvedLocalHumans()
    {
        int total = GetPlayerCount();
        int humans = 1;

        for (int i = 1; i < total; i++)
        {
            if (hostSeatModes[i] ==
                PrivateSeatMode.LocalHuman)
            {
                humans++;
            }
        }

        return Mathf.Clamp(
            humans,
            1,
            total);
    }

    private void SyncHostSeatModesToLegacyControls()
    {
        int total = GetPlayerCount();

        // Existing MatchSetupManager still consumes:
        // Human = 0, Bot = 1.
        SetPlayerType(
            0,
            false);

        for (int i = 1; i < total; i++)
        {
            bool bot =
                hostSeatModes[i] ==
                PrivateSeatMode.Bot;

            // OpenOnline is kept as a Human placeholder only in the hidden
            // legacy MatchSetup dropdown. START MATCH stays disabled while
            // any OpenOnline seat is unresolved, so this placeholder can
            // never incorrectly start a local player.
            SetPlayerType(
                i,
                bot);
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
            bool hasOpenSeats =
                backendSnapshot != null &&
                backendSnapshot.Settings != null &&
                backendSnapshot.Settings.OpenOnlineSeatCount > 0;

            SetText(
                roomStateText,
                backendSnapshot == null
                    ? T(
                        "lobby.online.roster_resolved",
                        "ROSTER READY • START AVAILABLE")
                    : hasOpenSeats
                        ? T(
                            "lobby.online.host_can_start_with_open",
                            "HOST CAN START • OPEN SEATS BECOME BOTS")
                        : T(
                            "lobby.online.host_can_start",
                            "HOST CAN START • REMOTE HUMANS READY"));
        }

        else if (backendSnapshot == null)
        {
            SetText(
                roomStateText,
                T(
                    "lobby.online.waiting_online_or_add",
                    "WAITING FOR ONLINE PLAYER • OR ADD LOCAL/BOT"));
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

        SetText(
            revisionText,
            T(
                "lobby.online.revision_format",
                "SETTINGS REV {0}",
                settingsRevision));

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

        SetSeat(
            1,
            T("lobby.online.you", "PLAYER (YOU)"),
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
                    SetSeat(
                        i,
                        !string.IsNullOrWhiteSpace(member.DisplayName)
                            ? member.DisplayName
                            : T(
                                "lobby.online.local_player_number",
                                "LOCAL PLAYER {0}",
                                i + 1),
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
        backendSnapshot = snapshot;
        backendLocalAccountId =
            localAccountId ?? string.Empty;

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
                : T(
                    "lobby.online.backend_connected",
                    "BACKEND SNAPSHOT CONNECTED"));

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
                    T(
                        result.ErrorLocalizationKey,
                        string.IsNullOrWhiteSpace(
                            result.TechnicalMessage)
                            ? "Unable to remove player."
                            : result.TechnicalMessage));

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

        bool localOnly =
            localIsHost &&
            backendSnapshot != null &&
            !backendSnapshot.HasRemoteHumans;

        if (localOnly)
        {
            mainMenuController ??=
                GetComponent<
                    AtlasBoardMainMenuController>();

            mainMenuController?.
                StartMatchAfterPrivateBackendAuthorization();

            yield break;
        }

        SetText(
            roomStateText,
            T(
                "lobby.online.authoritative_start_ready",
                "START CONFIRMED"));

        Debug.Log(
            "AtlasBoard authoritative online Start confirmed. " +
            "Full remote board-state networking is intentionally deferred to Phase 5.",
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
    }

    private void BindRuntimeEvents()
    {
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

        for (int i = 1; i < 4; i++)
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

        for (int i = 1; i < 4; i++)
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
