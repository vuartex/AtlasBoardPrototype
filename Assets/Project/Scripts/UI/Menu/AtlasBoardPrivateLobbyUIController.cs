using System;
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

    [Header("Lobby Settings")]
    [SerializeField] private TMP_Dropdown playerCountDropdown;
    [SerializeField] private TMP_Dropdown[] hostSettingsDropdowns;
    [SerializeField] private Toggle[] hostSettingsToggles;

    [Header("Existing Player Rows")]
    [SerializeField] private GameObject[] playerRows;
    [SerializeField] private GameObject[] legacyPlayerLabels;
    [SerializeField] private GameObject[] legacyPlayerStatuses;
    [SerializeField] private GameObject[] legacyPlayerDropdowns;

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
        Bot = 2
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

    private string roomCode = string.Empty;
    private int settingsRevision = 1;
    private int readyForRevision;
    private int requiredHumanPlayers = 1;

    private AtlasLobbySnapshot backendSnapshot;
    private string backendLocalAccountId = string.Empty;

    private void Awake()
    {
        mainMenuController =
            GetComponent<AtlasBoardMainMenuController>();

        BindRuntimeEvents();
        ResetPrivateState(false);
    }

    private void OnEnable()
    {
        BindRuntimeEvents();
    }

    private void OnDestroy()
    {
        UnbindRuntimeEvents();
        AtlasBoardLocalizationManager.LanguageChanged -= RefreshLocalizedText;
    }

    private void Update()
    {
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
            ResetPrivateState(false);
        }
    }

    public void ShowRoomEntryFromMainMenu()
    {
        privateMode = true;
        roomActive = false;
        localIsHost = false;
        backendSnapshot = null;
        backendLocalAccountId = string.Empty;
        roomCode = string.Empty;
        codeVisible = false;
        settingsRevision = 1;
        readyForRevision = 0;
        requiredHumanPlayers = 1;

        SaveLegacyControlValues();
        ResetHostSeatModes();

        SetActive(privateOnlineRoot, false);
        SetActive(roomPanel, false);
        SetActive(roomEntryOverlay, true);

        if (joinCodeInput != null)
        {
            joinCodeInput.text = string.Empty;
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

        if (roomActive || privateMode)
        {
            ResetPrivateState(false);
        }
    }

    private void CancelRoomEntry()
    {
        SetActive(roomEntryOverlay, false);
        ResetPrivateState(false);
    }

    private void CreateRoomPreview()
    {
        if (!privateMode)
        {
            return;
        }

        roomCode =
            UnityEngine.Random.Range(0, 1000000)
                .ToString("D6");

        roomActive = true;
        localIsHost = true;
        codeVisible = false;
        settingsRevision = 1;
        readyForRevision = 0;
        backendSnapshot = null;
        backendLocalAccountId = string.Empty;

        SetActive(roomEntryOverlay, false);

        mainMenuController ??=
            GetComponent<AtlasBoardMainMenuController>();

        mainMenuController?.OpenPrivateLobbyAfterRoomChoice();

        SetActive(privateOnlineRoot, true);
        SetActive(roomPanel, true);

        ConfigureHostLobbyPreview();
        RefreshLocalizedText();
        RefreshRoomHeader();
    }

    private void JoinRoomPreview()
    {
        if (!privateMode)
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

        roomCode = code;
        roomActive = true;
        localIsHost = false;
        codeVisible = false;
        settingsRevision = 1;
        readyForRevision = 0;
        backendSnapshot = null;
        backendLocalAccountId = string.Empty;
        requiredHumanPlayers = 2;

        SetActive(roomEntryOverlay, false);

        mainMenuController ??=
            GetComponent<AtlasBoardMainMenuController>();

        mainMenuController?.OpenPrivateLobbyAfterRoomChoice();

        SetActive(privateOnlineRoot, true);
        SetActive(roomPanel, true);

        ConfigureGuestLobbyPreview();
        RefreshLocalizedText();
        RefreshRoomHeader();
        RefreshGuestSeats();
    }

    private void ConfigureHostLobbyPreview()
    {
        ShowPrivateSeatPresentation(true);
        RestoreRowsFromPlayerCount();
        ResetHostSeatModes();
        CloseAllSeatChoicePanels();
        SyncHostSeatModesToLegacyControls();
        RefreshHostSeatRows();

        requiredHumanPlayers =
            CountResolvedLocalHumans();

        SetHostSettingsInteractable(true);

        SetActive(
            readyButton != null
                ? readyButton.gameObject
                : null,
            false);

        SetActive(legacyStartButton, true);
        RefreshHostStartAvailability();

        SetText(
            roomStateText,
            T(
                "lobby.online.preview_created",
                "LOCAL UI PREVIEW • ROOM CREATED"));
    }

    private void ConfigureGuestLobbyPreview()
    {
        ShowPrivateSeatPresentation(true);
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

        SetText(
            copyCodeButtonText,
            T("lobby.online.copied", "COPIED"));

        CancelInvoke(nameof(ResetCopyButtonText));
        Invoke(nameof(ResetCopyButtonText), 1.5f);
    }

    private void ResetCopyButtonText()
    {
        SetText(
            copyCodeButtonText,
            T("lobby.online.copy", "COPY"));
    }

    private void ToggleReady()
    {
        if (!roomActive ||
            localIsHost)
        {
            return;
        }

        readyForRevision =
            readyForRevision == settingsRevision
                ? 0
                : settingsRevision;

        RefreshRoomHeader();
        RefreshGuestSeats();
    }

    private void HandleHostSettingsDropdownChanged(int _)
    {
        if (!roomActive ||
            !localIsHost ||
            suppressSeatEvents)
        {
            return;
        }

        RestoreRowsFromPlayerCount();
        ResetInactiveHostSeatModes();
        CloseAllSeatChoicePanels();
        SyncHostSeatModesToLegacyControls();
        RefreshHostSeatRows();

        requiredHumanPlayers =
            CountResolvedLocalHumans();

        AdvanceLocalSettingsRevision();
        RefreshHostStartAvailability();
    }

    private void HandleHostToggleChanged(bool _)
    {
        if (!roomActive ||
            !localIsHost)
        {
            return;
        }

        AdvanceLocalSettingsRevision();
        RefreshHostStartAvailability();
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

        AdvanceLocalSettingsRevision();
        RefreshHostStartAvailability();
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

        if (backendSnapshot != null)
        {
            canStart =
                CanBackendHostStart(backendSnapshot);
        }
        else
        {
            // Local Humans and Bots never require network Ready.
            // An OpenOnline seat means the host is intentionally waiting for
            // a remote account, so local preview must not start yet.
            canStart =
                !HasOpenOnlineSeat();
        }

        start.interactable = canStart;

        if (canStart)
        {
            SetText(
                roomStateText,
                backendSnapshot == null
                    ? T(
                        "lobby.online.roster_resolved",
                        "ROSTER READY • START AVAILABLE")
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
            snapshot.LifecycleState != AtlasRoomLifecycleState.Waiting)
        {
            return false;
        }

        int revision = snapshot.SettingsRevision;

        foreach (AtlasLobbyMemberSnapshot member in snapshot.Members)
        {
            if (member == null ||
                !member.IsHumanSeat ||
                member.IsHost)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(member.AccountId) ||
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
            "👁 " +
            (codeVisible
                ? T("lobby.online.hide", "HIDE")
                : T("lobby.online.show", "SHOW")));

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

        for (int i = 0; i < 4; i++)
        {
            int slot = i + 1;

            AtlasLobbyMemberSnapshot member =
                backendSnapshot.Members != null
                    ? backendSnapshot.Members.FirstOrDefault(
                        candidate =>
                            candidate != null &&
                            candidate.SlotIndex == slot)
                    : null;

            if (member == null)
            {
                SetSeat(
                    i,
                    T("lobby.online.waiting_player", "WAITING FOR PLAYER"),
                    T("lobby.online.open_human_seat", "OPEN HUMAN SEAT"));
                continue;
            }

            if (!member.IsHumanSeat ||
                member.ControllerKind ==
                AtlasSeatControllerKind.PermanentBot)
            {
                SetSeat(
                    i,
                    T("lobby.online.bot", "BOT"),
                    T("lobby.online.bot_seat", "BOT SEAT"));
                continue;
            }

            bool isLocal =
                !string.IsNullOrWhiteSpace(backendLocalAccountId) &&
                string.Equals(
                    member.AccountId,
                    backendLocalAccountId,
                    StringComparison.Ordinal);

            string name =
                string.IsNullOrWhiteSpace(member.DisplayName)
                    ? T("lobby.online.waiting_player", "WAITING FOR PLAYER")
                    : member.DisplayName;

            if (isLocal)
            {
                name += " (YOU)";
            }

            string status;

            if (member.IsHost)
            {
                status = T("lobby.online.host", "HOST");
            }
            else if (member.IsReadyFor(revision))
            {
                status =
                    T(
                        "lobby.online.ready_for_revision",
                        "READY • REV {0}",
                        revision);
            }
            else
            {
                status = T("lobby.online.not_ready", "NOT READY");
            }

            SetSeat(i, name, status);
        }
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

    private void ResetPrivateState(bool preserveEntry)
    {
        privateMode = preserveEntry;
        roomActive = false;
        localIsHost = false;
        backendSnapshot = null;
        backendLocalAccountId = string.Empty;
        roomCode = string.Empty;
        codeVisible = false;
        settingsRevision = 1;
        readyForRevision = 0;
        requiredHumanPlayers = 1;

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
#endif
}
