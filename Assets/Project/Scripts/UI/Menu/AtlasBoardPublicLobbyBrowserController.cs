using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class AtlasBoardPublicLobbyBrowserController : MonoBehaviour
{
    [Serializable]
    private sealed class RoomRow
    {
        public GameObject Root;
        public Button RowButton;
        public TMP_Text HostText;
        public TMP_Text MapText;
        public TMP_Text PlayersText;
        public TMP_Text RoundsText;
        public TMP_Text RegionText;
        public TMP_Text AccessText;
        public Button JoinButton;
    }

    [Header("Roots")]
    [SerializeField] private GameObject mainMenuRoot;
    [SerializeField] private GameObject browserRoot;

    [Header("Filters")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private TMP_Text searchPlaceholderText;
    [SerializeField] private TMP_Dropdown mapFilter;
    [SerializeField] private TMP_Dropdown playersFilter;
    [SerializeField] private TMP_Dropdown roundFilter;
    [SerializeField] private TMP_Dropdown passwordFilter;

    [Header("Browser")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject emptyStateRoot;
    [SerializeField] private Button backButton;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button createPublicRoomButton;
    [SerializeField] private RoomRow[] rows = Array.Empty<RoomRow>();

    [Header("Password Prompt")]
    [SerializeField] private GameObject passwordPromptRoot;
    [SerializeField] private TMP_Text passwordPromptBody;
    [SerializeField] private TMP_InputField passwordPromptInput;
    [SerializeField] private TMP_Text passwordPromptStatus;
    [SerializeField] private Button passwordPromptJoinButton;
    [SerializeField] private Button passwordPromptCancelButton;

    [Header("Runtime")]
    [SerializeField] private AtlasBoardLobbyRuntimeBridge runtimeBridge;
    [SerializeField] private AtlasBoardMainMenuController mainMenuController;
    [SerializeField] private AtlasBoardPrivateLobbyUIController lobbyUiController;

    private readonly List<AtlasPublicLobbyCard> allRooms =
        new List<AtlasPublicLobbyCard>();

    private UnityEngine.Events.UnityAction[] rowClickActions =
        Array.Empty<UnityEngine.Events.UnityAction>();

    private UnityEngine.Events.UnityAction[] rowJoinActions =
        Array.Empty<UnityEngine.Events.UnityAction>();

    private bool busy;
    private bool hooked;
    private AtlasPublicLobbyCard pendingJoinRoom;
    private int lastClickedRow = -1;
    private float lastRowClickAt = -10f;

    private const float DoubleClickSeconds = 0.38f;

    private void Awake()
    {
        ResolveReferences();
        HookControls();
        SetActive(browserRoot, false);
        SetActive(passwordPromptRoot, false);
        AtlasBoardLocalizationManager.LanguageChanged += HandleLanguageChanged;
    }

    private void OnDestroy()
    {
        AtlasBoardLocalizationManager.LanguageChanged -= HandleLanguageChanged;
        UnhookControls();
    }

    private void Update()
    {
        if (passwordPromptRoot != null &&
            passwordPromptRoot.activeSelf &&
            WasEscapePressedThisFrame())
        {
            HidePasswordPrompt();
            return;
        }

        if (browserRoot == null ||
            !browserRoot.activeSelf ||
            !WasEscapePressedThisFrame())
        {
            return;
        }

        BackToMainMenu();
    }

    public void ShowBrowser()
    {
        if (busy)
        {
            return;
        }

        ResolveReferences();
        SetActive(mainMenuRoot, false);
        SetActive(browserRoot, true);
        SetActive(passwordPromptRoot, false);
        RebuildFilterLabels();
        ClearRows();
        SetText(statusText, T("public_browser.loading", "Loading public rooms..."));
        _ = RefreshRoomsAsync();
    }

    public void HostPublicRoomFromMainMenu()
    {
        if (busy)
        {
            return;
        }

        // Use the same online-room screen/style while the public room is
        // created. On success the authoritative lobby opens immediately.
        ResolveReferences();
        SetActive(mainMenuRoot, false);
        SetActive(browserRoot, true);
        RebuildFilterLabels();
        _ = CreatePublicRoomAsync();
    }

    public void BackToMainMenu()
    {
        if (busy)
        {
            return;
        }

        SetActive(passwordPromptRoot, false);
        SetActive(browserRoot, false);
        SetActive(mainMenuRoot, true);
    }

    private void ResolveReferences()
    {
        runtimeBridge ??= GetComponent<AtlasBoardLobbyRuntimeBridge>();
        mainMenuController ??= GetComponent<AtlasBoardMainMenuController>();
        lobbyUiController ??= GetComponent<AtlasBoardPrivateLobbyUIController>();
    }

    private void HookControls()
    {
        if (hooked)
        {
            return;
        }

        AddClick(backButton, BackToMainMenu);
        AddClick(refreshButton, RefreshRooms);
        AddClick(createPublicRoomButton, CreatePublicRoom);
        AddClick(passwordPromptJoinButton, ConfirmPasswordJoin);
        AddClick(passwordPromptCancelButton, HidePasswordPrompt);

        if (searchInput != null)
        {
            searchInput.onValueChanged.AddListener(HandleSearchChanged);
        }

        AddDropdown(mapFilter, HandleFilterChanged);
        AddDropdown(playersFilter, HandleFilterChanged);
        AddDropdown(roundFilter, HandleFilterChanged);
        AddDropdown(passwordFilter, HandleFilterChanged);

        rowClickActions = new UnityEngine.Events.UnityAction[rows?.Length ?? 0];
        rowJoinActions = new UnityEngine.Events.UnityAction[rows?.Length ?? 0];

        for (int i = 0; i < rowClickActions.Length; i++)
        {
            int captured = i;
            rowClickActions[i] = () => HandleRowClick(captured);
            rowJoinActions[i] = () => JoinRow(captured);
            AddClick(rows[i]?.RowButton, rowClickActions[i]);
            AddClick(rows[i]?.JoinButton, rowJoinActions[i]);
        }

        hooked = true;
    }

    private void UnhookControls()
    {
        if (!hooked)
        {
            return;
        }

        RemoveClick(backButton, BackToMainMenu);
        RemoveClick(refreshButton, RefreshRooms);
        RemoveClick(createPublicRoomButton, CreatePublicRoom);
        RemoveClick(passwordPromptJoinButton, ConfirmPasswordJoin);
        RemoveClick(passwordPromptCancelButton, HidePasswordPrompt);

        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveListener(HandleSearchChanged);
        }

        RemoveDropdown(mapFilter, HandleFilterChanged);
        RemoveDropdown(playersFilter, HandleFilterChanged);
        RemoveDropdown(roundFilter, HandleFilterChanged);
        RemoveDropdown(passwordFilter, HandleFilterChanged);

        for (int i = 0; i < rowClickActions.Length && i < rows.Length; i++)
        {
            RemoveClick(rows[i]?.RowButton, rowClickActions[i]);
            RemoveClick(rows[i]?.JoinButton, rowJoinActions[i]);
        }

        hooked = false;
    }

    private void RefreshRooms()
    {
        if (!busy)
        {
            _ = RefreshRoomsAsync();
        }
    }

    private async Task RefreshRoomsAsync()
    {
        if (busy)
        {
            return;
        }

        busy = true;
        SetControlsInteractable(false);

        try
        {
            SetText(statusText, T("public_browser.loading", "Loading public rooms..."));

            AtlasPublicLobbyListResult result =
                await runtimeBridge.ListPublicRoomsAsync(20);

            if (!result.Success)
            {
                allRooms.Clear();
                ClearRows();
                SetText(
                    statusText,
                    TranslateError(result.ErrorLocalizationKey,
                        "Could not load public rooms."));
                return;
            }

            allRooms.Clear();
            if (result.Rooms != null)
            {
                allRooms.AddRange(result.Rooms.Where(room => room != null));
            }

            ApplyFilters();
        }
        finally
        {
            busy = false;
            SetControlsInteractable(true);
        }
    }

    private async void CreatePublicRoom()
    {
        await CreatePublicRoomAsync();
    }

    private async Task CreatePublicRoomAsync()
    {
        if (busy)
        {
            return;
        }

        ResolveReferences();
        if (runtimeBridge == null || mainMenuController == null || lobbyUiController == null)
        {
            SetText(statusText, T("public_browser.unavailable", "Public room service is unavailable."));
            return;
        }

        busy = true;
        SetControlsInteractable(false);

        try
        {
            SetText(statusText, T("public_browser.creating", "Creating public room..."));
            AtlasBoardLobbySelection selection = mainMenuController.GetCurrentLobbySelection();
            AtlasLobbyOperationResult result =
                await runtimeBridge.CreatePublicRoomAsync(selection);

            if (!result.Success || result.Snapshot == null)
            {
                SetText(
                    statusText,
                    TranslateError(result.ErrorLocalizationKey,
                        "Could not create public room."));
                return;
            }

            SetActive(browserRoot, false);
            lobbyUiController.EnterHostedPublicRoom(result);
        }
        finally
        {
            busy = false;
            SetControlsInteractable(true);
        }
    }

    private void HandleSearchChanged(string _)
    {
        if (!busy)
        {
            ApplyFilters();
        }
    }

    private void HandleFilterChanged(int _)
    {
        if (!busy)
        {
            ApplyFilters();
        }
    }

    private void ApplyFilters()
    {
        string search = searchInput != null
            ? (searchInput.text ?? string.Empty).Trim()
            : string.Empty;

        int mapIndex = mapFilter != null ? mapFilter.value : 0;
        int playersIndex = playersFilter != null ? playersFilter.value : 0;
        int roundIndex = roundFilter != null ? roundFilter.value : 0;
        int passwordIndex = passwordFilter != null ? passwordFilter.value : 0;

        IEnumerable<AtlasPublicLobbyCard> filtered = allRooms;

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(room =>
                Contains(room.HostDisplayName, search) ||
                Contains(room.MapId, search));
        }

        string selectedMap = mapIndex switch
        {
            1 => "Turkey",
            2 => "Colorado",
            3 => "USA",
            _ => string.Empty
        };

        if (!string.IsNullOrEmpty(selectedMap))
        {
            filtered = filtered.Where(room =>
                string.Equals(room.MapId, selectedMap, StringComparison.OrdinalIgnoreCase));
        }

        int selectedCapacity = playersIndex switch
        {
            1 => 2,
            2 => 3,
            3 => 4,
            _ => 0
        };

        if (selectedCapacity > 0)
        {
            filtered = filtered.Where(room => room.MaxPlayers == selectedCapacity);
        }

        int selectedRounds = roundIndex switch
        {
            1 => 10,
            2 => 20,
            3 => 30,
            _ => 0
        };

        if (selectedRounds > 0)
        {
            filtered = filtered.Where(room => room.RoundLimit == selectedRounds);
        }

        if (passwordIndex == 1)
        {
            filtered = filtered.Where(room => !room.HasPassword);
        }
        else if (passwordIndex == 2)
        {
            filtered = filtered.Where(room => room.HasPassword);
        }

        RenderRooms(filtered.Take(rows.Length).ToList());
    }

    private void RenderRooms(List<AtlasPublicLobbyCard> rooms)
    {
        ClearRows();
        int count = rooms != null ? Mathf.Min(rooms.Count, rows.Length) : 0;

        for (int i = 0; i < count; i++)
        {
            AtlasPublicLobbyCard room = rooms[i];
            RoomRow row = rows[i];
            if (room == null || row == null)
            {
                continue;
            }

            row.Root.name = $"RoomRow_{i + 1}_{room.LobbyId}";
            SetActive(row.Root, true);
            SetText(row.HostText,
                string.IsNullOrWhiteSpace(room.HostDisplayName)
                    ? T("public_browser.unknown_host", "Unknown")
                    : room.HostDisplayName);
            SetText(row.MapText, room.MapId);
            SetText(row.PlayersText, $"{room.OccupiedPlayers}/{room.MaxPlayers}");
            SetText(row.RoundsText, room.RoundLimit.ToString());
            SetText(row.RegionText, DisplayRegion(room.RegionId));
            SetText(row.AccessText,
                room.HasPassword
                    ? T("public_browser.password_required_short", "PASSWORD")
                    : T("public_browser.open_short", "OPEN"));

            row.Root.GetComponent<AtlasBoardPublicLobbyRowIdentity>()
                ?.SetRoom(room);
        }

        bool empty = count == 0;
        SetActive(emptyStateRoot, empty);
        SetText(
            statusText,
            empty
                ? T("public_browser.empty", "No joinable public rooms found.")
                : T("public_browser.found", "{0} public room(s) found.", count));
    }

    private void ClearRows()
    {
        if (rows != null)
        {
            foreach (RoomRow row in rows)
            {
                if (row != null)
                {
                    row.Root?.GetComponent<AtlasBoardPublicLobbyRowIdentity>()?.ClearRoom();
                    SetActive(row.Root, false);
                }
            }
        }

        SetActive(emptyStateRoot, false);
    }

    private AtlasPublicLobbyCard RoomAtRow(int index)
    {
        if (index < 0 || index >= rows.Length || rows[index]?.Root == null)
        {
            return null;
        }

        return rows[index].Root
            .GetComponent<AtlasBoardPublicLobbyRowIdentity>()
            ?.Room;
    }

    private void HandleRowClick(int index)
    {
        float now = Time.unscaledTime;
        bool doubleClick = index == lastClickedRow &&
                           now - lastRowClickAt <= DoubleClickSeconds;
        lastClickedRow = index;
        lastRowClickAt = now;

        if (doubleClick)
        {
            JoinRow(index);
        }
    }

    private void JoinRow(int index)
    {
        if (busy)
        {
            return;
        }

        AtlasPublicLobbyCard room = RoomAtRow(index);
        if (room == null)
        {
            return;
        }

        if (room.HasPassword)
        {
            ShowPasswordPrompt(room);
            return;
        }

        _ = JoinPublicRoomAsync(room, string.Empty);
    }

    private void ShowPasswordPrompt(AtlasPublicLobbyCard room)
    {
        pendingJoinRoom = room;
        SetText(
            passwordPromptBody,
            T("public_browser.password_prompt_body",
                "Enter the password for {0}.",
                string.IsNullOrWhiteSpace(room.HostDisplayName)
                    ? room.MapId
                    : room.HostDisplayName));
        SetText(passwordPromptStatus, string.Empty);
        if (passwordPromptInput != null)
        {
            passwordPromptInput.text = string.Empty;
            passwordPromptInput.ActivateInputField();
        }
        SetActive(passwordPromptRoot, true);
    }

    private void HidePasswordPrompt()
    {
        if (busy)
        {
            return;
        }

        pendingJoinRoom = null;
        SetActive(passwordPromptRoot, false);
    }

    private void ConfirmPasswordJoin()
    {
        if (pendingJoinRoom == null || busy)
        {
            return;
        }

        string password = passwordPromptInput != null
            ? passwordPromptInput.text
            : string.Empty;

        _ = JoinPublicRoomAsync(pendingJoinRoom, password);
    }

    private async Task JoinPublicRoomAsync(
        AtlasPublicLobbyCard room,
        string password)
    {
        if (room == null || busy)
        {
            return;
        }

        busy = true;
        SetControlsInteractable(false);
        SetText(statusText, T("public_browser.joining", "Joining room..."));
        SetText(passwordPromptStatus, T("public_browser.joining", "Joining room..."));

        try
        {
            AtlasLobbyOperationResult result =
                await runtimeBridge.JoinPublicRoomAsync(room.LobbyId, password);

            if (!result.Success || result.Snapshot == null)
            {
                string error = TranslateError(
                    result.ErrorLocalizationKey,
                    "Could not join this room.");

                if (string.Equals(
                        result.ErrorLocalizationKey,
                        "lobby.error.password_required",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        result.ErrorLocalizationKey,
                        "lobby.error.password_incorrect",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (passwordPromptRoot == null || !passwordPromptRoot.activeSelf)
                    {
                        ShowPasswordPrompt(room);
                    }
                    SetText(passwordPromptStatus, error);
                }
                else
                {
                    SetText(statusText, error);
                }

                // Card may have become full/closed/passworded after the last
                // discovery refresh. Re-fetch canonical discovery state.
                _ = RefreshRoomsAfterJoinFailureAsync();
                return;
            }

            pendingJoinRoom = null;
            SetActive(passwordPromptRoot, false);
            SetActive(browserRoot, false);
            lobbyUiController.EnterJoinedPublicRoom(result);
        }
        finally
        {
            busy = false;
            SetControlsInteractable(true);
        }
    }

    private async Task RefreshRoomsAfterJoinFailureAsync()
    {
        await Task.Yield();
        if (!busy && browserRoot != null && browserRoot.activeSelf)
        {
            await RefreshRoomsAsync();
        }
    }

    private void HandleLanguageChanged()
    {
        RebuildFilterLabels();
        ApplyFilters();
    }

    private void RebuildFilterLabels()
    {
        SetText(searchPlaceholderText,
            T("public_browser.search_placeholder", "Search host or map..."));

        SetOptions(mapFilter, new[]
        {
            T("public_browser.all_maps", "ALL MAPS"),
            "Turkey", "Colorado", "USA"
        });

        SetOptions(playersFilter, new[]
        {
            T("public_browser.any_players", "ANY PLAYERS"),
            "2", "3", "4"
        });

        SetOptions(roundFilter, new[]
        {
            T("public_browser.any_rounds", "ANY ROUNDS"),
            "10", "20", "30"
        });

        SetOptions(passwordFilter, new[]
        {
            T("public_browser.any_access", "ANY ACCESS"),
            T("public_browser.open_short", "OPEN"),
            T("public_browser.password_required_short", "PASSWORD")
        });
    }

    private void SetControlsInteractable(bool interactable)
    {
        if (backButton != null) backButton.interactable = interactable;
        if (refreshButton != null) refreshButton.interactable = interactable;
        if (createPublicRoomButton != null) createPublicRoomButton.interactable = interactable;
        if (searchInput != null) searchInput.interactable = interactable;
        if (mapFilter != null) mapFilter.interactable = interactable;
        if (playersFilter != null) playersFilter.interactable = interactable;
        if (roundFilter != null) roundFilter.interactable = interactable;
        if (passwordFilter != null) passwordFilter.interactable = interactable;
        if (passwordPromptJoinButton != null) passwordPromptJoinButton.interactable = interactable;
        if (passwordPromptCancelButton != null) passwordPromptCancelButton.interactable = interactable;

        if (rows != null)
        {
            foreach (RoomRow row in rows)
            {
                if (row?.JoinButton != null) row.JoinButton.interactable = interactable;
                if (row?.RowButton != null) row.RowButton.interactable = interactable;
            }
        }
    }

    private static bool Contains(string source, string value)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string DisplayRegion(string regionId)
    {
        return string.Equals(regionId, "eur3", StringComparison.OrdinalIgnoreCase)
            ? "EU"
            : string.IsNullOrWhiteSpace(regionId)
                ? "-"
                : regionId.ToUpperInvariant();
    }

    private static string TranslateError(string key, string fallback)
    {
        string value = AtlasBoardL.T(key);
        return string.IsNullOrWhiteSpace(value) || value == key ? fallback : value;
    }

    private static string T(string key, string fallback, params object[] args)
    {
        string value = AtlasBoardL.T(key, args);
        return string.IsNullOrWhiteSpace(value) || value == key
            ? (args != null && args.Length > 0
                ? string.Format(fallback, args)
                : fallback)
            : value;
    }

    private static void SetOptions(TMP_Dropdown dropdown, string[] values)
    {
        if (dropdown == null)
        {
            return;
        }

        int previous = Mathf.Clamp(dropdown.value, 0, Mathf.Max(0, values.Length - 1));
        dropdown.ClearOptions();
        dropdown.AddOptions(values.ToList());
        dropdown.SetValueWithoutNotify(previous);
        dropdown.RefreshShownValue();
    }

    private static void AddDropdown(TMP_Dropdown dropdown, UnityEngine.Events.UnityAction<int> action)
    {
        if (dropdown == null) return;
        dropdown.onValueChanged.RemoveListener(action);
        dropdown.onValueChanged.AddListener(action);
    }

    private static void RemoveDropdown(TMP_Dropdown dropdown, UnityEngine.Events.UnityAction<int> action)
    {
        if (dropdown != null) dropdown.onValueChanged.RemoveListener(action);
    }

    private static void AddClick(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void RemoveClick(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.RemoveListener(action);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value ?? string.Empty;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }

    private static bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        GameObject newMainMenuRoot,
        GameObject newBrowserRoot,
        TMP_InputField newSearchInput,
        TMP_Text newSearchPlaceholderText,
        TMP_Dropdown newMapFilter,
        TMP_Dropdown newPlayersFilter,
        TMP_Dropdown newRoundFilter,
        TMP_Dropdown newPasswordFilter,
        TMP_Text newStatusText,
        GameObject newEmptyStateRoot,
        Button newBackButton,
        Button newRefreshButton,
        Button newCreatePublicRoomButton,
        GameObject newPasswordPromptRoot,
        TMP_Text newPasswordPromptBody,
        TMP_InputField newPasswordPromptInput,
        TMP_Text newPasswordPromptStatus,
        Button newPasswordPromptJoinButton,
        Button newPasswordPromptCancelButton,
        GameObject[] rowRoots,
        Button[] rowButtons,
        TMP_Text[] rowHostTexts,
        TMP_Text[] rowMapTexts,
        TMP_Text[] rowPlayersTexts,
        TMP_Text[] rowRoundsTexts,
        TMP_Text[] rowRegionTexts,
        TMP_Text[] rowAccessTexts,
        Button[] rowJoinButtons,
        AtlasBoardLobbyRuntimeBridge newRuntimeBridge,
        AtlasBoardMainMenuController newMainMenuController,
        AtlasBoardPrivateLobbyUIController newLobbyUiController)
    {
        mainMenuRoot = newMainMenuRoot;
        browserRoot = newBrowserRoot;
        searchInput = newSearchInput;
        searchPlaceholderText = newSearchPlaceholderText;
        mapFilter = newMapFilter;
        playersFilter = newPlayersFilter;
        roundFilter = newRoundFilter;
        passwordFilter = newPasswordFilter;
        statusText = newStatusText;
        emptyStateRoot = newEmptyStateRoot;
        backButton = newBackButton;
        refreshButton = newRefreshButton;
        createPublicRoomButton = newCreatePublicRoomButton;
        passwordPromptRoot = newPasswordPromptRoot;
        passwordPromptBody = newPasswordPromptBody;
        passwordPromptInput = newPasswordPromptInput;
        passwordPromptStatus = newPasswordPromptStatus;
        passwordPromptJoinButton = newPasswordPromptJoinButton;
        passwordPromptCancelButton = newPasswordPromptCancelButton;
        runtimeBridge = newRuntimeBridge;
        mainMenuController = newMainMenuController;
        lobbyUiController = newLobbyUiController;

        int count = rowRoots?.Length ?? 0;
        rows = new RoomRow[count];
        for (int i = 0; i < count; i++)
        {
            rows[i] = new RoomRow
            {
                Root = rowRoots[i],
                RowButton = rowButtons[i],
                HostText = rowHostTexts[i],
                MapText = rowMapTexts[i],
                PlayersText = rowPlayersTexts[i],
                RoundsText = rowRoundsTexts[i],
                RegionText = rowRegionTexts[i],
                AccessText = rowAccessTexts[i],
                JoinButton = rowJoinButtons[i]
            };
        }
    }
#endif
}

// Keeps the current sanitized discovery card attached to a visible row without
// exposing it through Unity serialization.
public sealed class AtlasBoardPublicLobbyRowIdentity : MonoBehaviour
{
    public AtlasPublicLobbyCard Room { get; private set; }
    public void SetRoom(AtlasPublicLobbyCard room) => Room = room;
    public void ClearRoom() => Room = null;
}
