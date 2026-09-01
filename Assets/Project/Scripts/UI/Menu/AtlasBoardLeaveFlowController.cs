using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-5000)]
[DisallowMultipleComponent]
public class AtlasBoardLeaveFlowController : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private UXKeyboardShortcutController gameplayShortcutController;

    [Header("Menu Flow")]
    [SerializeField]
    private GameObject mainMenuCanvas;

    [SerializeField]
    private GameObject mainMenuRoot;

    [SerializeField]
    private GameObject lobbyRoot;

    [Header("Leave Flow UI")]
    [SerializeField]
    private GameObject pauseRoot;

    [SerializeField]
    private GameObject leaveConfirmationRoot;

    [Header("In-Match Room Code")]
    [SerializeField]
    private GameObject roomCodeSectionRoot;

    [SerializeField]
    private TMP_Text roomCodeValueText;

    [SerializeField]
    private Button roomCodeShowHideButton;

    [SerializeField]
    private TMP_Text roomCodeShowHideButtonText;

    [SerializeField]
    private Button roomCodeCopyButton;

    [SerializeField]
    private TMP_Text roomCodeCopyButtonText;

    [Header("Buttons")]
    [SerializeField]
    private Button resumeButton;

    [SerializeField]
    private Button settingsButton;

    [SerializeField]
    private Button leaveMatchButton;

    [SerializeField]
    private Button quitGameButton;

    [SerializeField]
    private Button cancelLeaveButton;

    [SerializeField]
    private Button confirmLeaveButton;

    [SerializeField]
    private Button leaveLobbyButton;

    private bool controlsHooked;
    private bool shortcutStateCaptured;
    private bool gameplayShortcutWasEnabled;
    private bool pauseOwnsTimeScale;
    private float previousTimeScale = 1f;
    private bool resumeFromEscapeRequested;

    private AtlasBoardLobbyRuntimeBridge lobbyRuntimeBridge;
    private bool roomCodeRevealed;
    private string currentPauseRoomCode = string.Empty;
    private Coroutine copyFeedbackCoroutine;

    private void Awake()
    {
        ResolveReferences();
        HookControls();
        HideLeaveFlowImmediate();
    }

    private void OnEnable()
    {
        ResolveReferences();
        HookControls();
    }

    private void OnDisable()
    {
        RestoreGameplayState();
    }

    private void OnDestroy()
    {
        UnhookControls();
        RestoreGameplayState();
    }

    private void Update()
    {
        ResolveReferences();

        if (!IsActiveGameplayMatch())
        {
            if (IsPauseVisible() || IsConfirmationVisible())
            {
                HideLeaveFlowImmediate();
                RestoreGameplayState();
            }

            return;
        }

        // Settings owns Escape while its own window is visible.
        // The pause blocker remains active behind it so gameplay does not resume.
        if (IsSettingsOpen())
        {
            KeepGameplayShortcutDisabled();
            return;
        }

        if (IsConfirmationVisible())
        {
            KeepGameplayShortcutDisabled();

            if (WasEscapePressedThisFrame())
            {
                CancelLeaveMatch();
            }

            return;
        }

        if (IsPauseVisible())
        {
            KeepGameplayShortcutDisabled();

            if (WasEscapePressedThisFrame())
            {
                // Do not hide the blocker during Update. SettingsV2 runs later in
                // the same frame and must still see Escape as blocked.
                resumeFromEscapeRequested = true;
            }

            return;
        }

        if (!WasEscapePressedThisFrame())
        {
            return;
        }

        // Existing trade / auction / purchase / tablet modals keep first priority.
        if (HasBlockingGameplayModal())
        {
            return;
        }

        OpenPauseMenu();
    }

    private void LateUpdate()
    {
        if (!resumeFromEscapeRequested)
        {
            return;
        }

        resumeFromEscapeRequested = false;
        ResumeMatch();
    }

    public void OpenPauseMenu()
    {
        if (!IsActiveGameplayMatch())
        {
            return;
        }

        CaptureGameplayState();
        KeepGameplayShortcutDisabled();
        RefreshPauseRoomCodeSection(
            resetVisibility: true);

        if (leaveConfirmationRoot != null)
        {
            leaveConfirmationRoot.SetActive(false);
        }

        if (pauseRoot != null)
        {
            pauseRoot.SetActive(true);
        }

        AtlasBoardAudioManager.Instance?.PlayUiOpen();
    }

    public void ResumeMatch()
    {
        resumeFromEscapeRequested = false;

        if (leaveConfirmationRoot != null)
        {
            leaveConfirmationRoot.SetActive(false);
        }

        if (pauseRoot != null)
        {
            pauseRoot.SetActive(false);
        }

        RestoreGameplayState();
    }

    public void OpenSettingsFromPause()
    {
        AtlasBoardSettingsV2Controller controller =
            AtlasBoardSettingsV2Controller.Instance;

        if (controller == null)
        {
            controller = FindIncludingInactive<AtlasBoardSettingsV2Controller>();
        }

        if (controller == null)
        {
            Debug.LogWarning(
                "Leave Flow could not open Settings because AtlasBoardSettingsV2Controller was not found.",
                this);

            return;
        }

        KeepGameplayShortcutDisabled();
        controller.gameObject.SetActive(true);
        controller.OpenSettings();
    }

    public void ShowLeaveMatchConfirmation()
    {
        if (!IsActiveGameplayMatch())
        {
            return;
        }

        CaptureGameplayState();
        KeepGameplayShortcutDisabled();

        if (pauseRoot != null)
        {
            pauseRoot.SetActive(true);
        }

        if (leaveConfirmationRoot != null)
        {
            leaveConfirmationRoot.SetActive(true);
        }

        AtlasBoardAudioManager.Instance?.PlayUiOpen();
    }

    public void CancelLeaveMatch()
    {
        if (leaveConfirmationRoot != null)
        {
            leaveConfirmationRoot.SetActive(false);
        }

        KeepGameplayShortcutDisabled();
    }

    public void ConfirmLeaveMatch()
    {
        resumeFromEscapeRequested = false;

        IAtlasBoardSessionExitHandler onlineHandler =
            FindActiveOnlineSessionExitHandler();

        if (onlineHandler != null)
        {
            RestoreGameplayState();

            if (onlineHandler.TryHandleLeaveMatch())
            {
                HideLeaveFlowImmediate();
                return;
            }

            Debug.LogWarning(
                "AtlasBoard Leave Flow: an online session is active, but its session exit handler did not accept Leave Match. " +
                "The local scene will NOT be reloaded because doing so could orphan or desynchronize the online seat.",
                this);

            return;
        }

        if (pauseOwnsTimeScale)
        {
            Time.timeScale = 1f;
            pauseOwnsTimeScale = false;
        }

        // Local/offline fallback. Reloading the current scene intentionally gives
        // us a clean local session without manually resetting ownership, money,
        // auctions, trades, events, development, bots, turn order, dice or pawn state.
        Scene activeScene = SceneManager.GetActiveScene();

        if (!activeScene.IsValid() || string.IsNullOrWhiteSpace(activeScene.name))
        {
            Debug.LogError(
                "Leave Flow could not return to Main Menu because the active scene is invalid.",
                this);

            return;
        }

        Debug.Log(
            $"AtlasBoard Leave Flow: leaving local match and reloading scene '{activeScene.name}' for a clean Main Menu session.",
            this);

        SceneManager.LoadScene(activeScene.name, LoadSceneMode.Single);
    }

    public void LeaveLobby()
    {
        ResolveReferences();

        IAtlasBoardSessionExitHandler onlineHandler =
            FindActiveOnlineSessionExitHandler();

        if (onlineHandler != null)
        {
            if (!onlineHandler.TryHandleLeaveLobby())
            {
                Debug.LogWarning(
                    "AtlasBoard Leave Flow: an online session is active, but its session exit handler did not accept Leave Lobby. " +
                    "Local lobby state was left untouched to avoid desynchronizing the online session.",
                    this);
            }

            return;
        }

        // Local/offline lobby fallback. Use the existing Main Menu controller first
        // so its internal lobby state remains authoritative.
        if (mainMenuCanvas != null)
        {
            mainMenuCanvas.SendMessage(
                "BackFromLobby",
                SendMessageOptions.DontRequireReceiver);
        }

        // Safe visual fallback if the current controller implementation did not
        // receive the message for any reason.
        if (mainMenuCanvas != null && !mainMenuCanvas.activeSelf)
        {
            mainMenuCanvas.SetActive(true);
        }

        if (mainMenuRoot != null)
        {
            mainMenuRoot.SetActive(true);
        }

        if (lobbyRoot != null)
        {
            lobbyRoot.SetActive(false);
        }

        AtlasBoardAudioManager.Instance?.PlayUiClick();
    }

    public void QuitGame()
    {
        resumeFromEscapeRequested = false;

        IAtlasBoardSessionExitHandler onlineHandler =
            FindActiveOnlineSessionExitHandler();

        if (onlineHandler != null)
        {
            // Online quit must be owned by the session layer so it can notify
            // peers/host, preserve or release the seat according to reconnect
            // policy, and only then close the application safely.
            RestoreGameplayState();

            if (onlineHandler.TryHandleQuitGame())
            {
                HideLeaveFlowImmediate();
                return;
            }

            Debug.LogWarning(
                "AtlasBoard Leave Flow: an online session is active, but its session exit handler did not accept Quit Game. " +
                "The application will remain open to avoid orphaning or desynchronizing the online seat.",
                this);

            return;
        }

        if (pauseOwnsTimeScale)
        {
            Time.timeScale = 1f;
            pauseOwnsTimeScale = false;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private bool IsActiveGameplayMatch()
    {
        return turnManager != null &&
               turnManager.IsMatchStarted &&
               !IsMenuFlowVisible();
    }

    private bool IsMenuFlowVisible()
    {
        return mainMenuCanvas != null &&
               mainMenuCanvas.activeInHierarchy;
    }

    private bool IsPauseVisible()
    {
        return pauseRoot != null &&
               pauseRoot.activeInHierarchy;
    }

    private bool IsConfirmationVisible()
    {
        return leaveConfirmationRoot != null &&
               leaveConfirmationRoot.activeInHierarchy;
    }

    private static bool IsSettingsOpen()
    {
        GameObject settingsRoot = FindSceneObject("SettingsRoot");

        return settingsRoot != null &&
               settingsRoot.activeInHierarchy;
    }

    private bool HasBlockingGameplayModal()
    {
        AtlasBoardEscapeBlocker[] blockers =
            UnityEngine.Object.FindObjectsByType<AtlasBoardEscapeBlocker>(
                FindObjectsInactive.Include);

        foreach (AtlasBoardEscapeBlocker blocker in blockers)
        {
            if (blocker == null || !blocker.IsBlocking)
            {
                continue;
            }

            if (pauseRoot != null &&
                blocker.transform.IsChildOf(pauseRoot.transform))
            {
                continue;
            }

            if (leaveConfirmationRoot != null &&
                blocker.transform.IsChildOf(leaveConfirmationRoot.transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void CaptureGameplayState()
    {
        if (!shortcutStateCaptured && gameplayShortcutController != null)
        {
            gameplayShortcutWasEnabled = gameplayShortcutController.enabled;
            shortcutStateCaptured = true;
        }

        // Pause is a local UI state. It may freeze a single-human offline match,
        // but it must never freeze an online session or a match with multiple
        // participating Human seats. This keeps the pause foundation compatible
        // with future Steam sessions where each client can open its own menu.
        if (!pauseOwnsTimeScale &&
            ShouldFreezeSimulationForPause())
        {
            previousTimeScale = Time.timeScale > 0f
                ? Time.timeScale
                : 1f;

            Time.timeScale = 0f;
            pauseOwnsTimeScale = true;
        }
    }

    private void KeepGameplayShortcutDisabled()
    {
        if (!shortcutStateCaptured && gameplayShortcutController != null)
        {
            gameplayShortcutWasEnabled = gameplayShortcutController.enabled;
            shortcutStateCaptured = true;
        }

        if (gameplayShortcutController != null &&
            gameplayShortcutController.enabled)
        {
            gameplayShortcutController.enabled = false;
        }
    }

    private void RestoreGameplayState()
    {
        if (pauseOwnsTimeScale)
        {
            Time.timeScale = previousTimeScale > 0f
                ? previousTimeScale
                : 1f;

            pauseOwnsTimeScale = false;
        }

        if (shortcutStateCaptured && gameplayShortcutController != null)
        {
            gameplayShortcutController.enabled = gameplayShortcutWasEnabled;
        }

        shortcutStateCaptured = false;
    }

    private void HideLeaveFlowImmediate()
    {
        resumeFromEscapeRequested = false;
        roomCodeRevealed = false;
        currentPauseRoomCode = string.Empty;

        if (copyFeedbackCoroutine != null)
        {
            StopCoroutine(
                copyFeedbackCoroutine);

            copyFeedbackCoroutine = null;
        }

        if (roomCodeSectionRoot != null)
        {
            roomCodeSectionRoot.SetActive(false);
        }

        if (leaveConfirmationRoot != null)
        {
            leaveConfirmationRoot.SetActive(false);
        }

        if (pauseRoot != null)
        {
            pauseRoot.SetActive(false);
        }
    }

    private void HookControls()
    {
        if (controlsHooked)
        {
            return;
        }

        AddClick(resumeButton, ResumeMatch);
        AddClick(settingsButton, OpenSettingsFromPause);
        AddClick(leaveMatchButton, ShowLeaveMatchConfirmation);
        AddClick(quitGameButton, QuitGame);
        AddClick(cancelLeaveButton, CancelLeaveMatch);
        AddClick(confirmLeaveButton, ConfirmLeaveMatch);
        AddClick(leaveLobbyButton, LeaveLobby);
        AddClick(
            roomCodeShowHideButton,
            TogglePauseRoomCodeVisibility);
        AddClick(
            roomCodeCopyButton,
            CopyPauseRoomCode);

        controlsHooked = true;
    }

    private void UnhookControls()
    {
        if (!controlsHooked)
        {
            return;
        }

        RemoveClick(resumeButton, ResumeMatch);
        RemoveClick(settingsButton, OpenSettingsFromPause);
        RemoveClick(leaveMatchButton, ShowLeaveMatchConfirmation);
        RemoveClick(quitGameButton, QuitGame);
        RemoveClick(cancelLeaveButton, CancelLeaveMatch);
        RemoveClick(confirmLeaveButton, ConfirmLeaveMatch);
        RemoveClick(leaveLobbyButton, LeaveLobby);
        RemoveClick(
            roomCodeShowHideButton,
            TogglePauseRoomCodeVisibility);
        RemoveClick(
            roomCodeCopyButton,
            CopyPauseRoomCode);

        controlsHooked = false;
    }

    private void ResolveReferences()
    {
        if (turnManager == null)
        {
            turnManager = FindIncludingInactive<TurnManager>();
        }

        if (gameplayShortcutController == null)
        {
            gameplayShortcutController =
                FindIncludingInactive<UXKeyboardShortcutController>();
        }

        if (mainMenuCanvas == null)
        {
            mainMenuCanvas = FindSceneObject("Canvas_MainMenu");
        }

        if (mainMenuRoot == null && mainMenuCanvas != null)
        {
            mainMenuRoot = FindChildRecursive(mainMenuCanvas.transform, "MainMenu")?.gameObject;
        }

        if (lobbyRoot == null && mainMenuCanvas != null)
        {
            lobbyRoot = FindChildRecursive(mainMenuCanvas.transform, "Lobby")?.gameObject;
        }

        if (lobbyRuntimeBridge == null)
        {
            lobbyRuntimeBridge =
                FindIncludingInactive<
                    AtlasBoardLobbyRuntimeBridge>();
        }
    }

    private void RefreshPauseRoomCodeSection(
        bool resetVisibility)
    {
        ResolveReferences();

        string code =
            lobbyRuntimeBridge != null
                ? lobbyRuntimeBridge.CurrentRoomCode
                : string.Empty;

        code =
            string.IsNullOrWhiteSpace(code)
                ? string.Empty
                : code.Trim();

        bool hasCode =
            IsSixDigitRoomCode(code);

        currentPauseRoomCode =
            hasCode
                ? code
                : string.Empty;

        if (resetVisibility)
        {
            roomCodeRevealed = false;
        }

        if (roomCodeSectionRoot != null)
        {
            roomCodeSectionRoot.SetActive(
                hasCode);
        }

        if (!hasCode)
        {
            return;
        }

        RefreshPauseRoomCodeValue();
        ResetPauseRoomCodeActionLabels();
    }

    private void TogglePauseRoomCodeVisibility()
    {
        if (string.IsNullOrWhiteSpace(
                currentPauseRoomCode))
        {
            RefreshPauseRoomCodeSection(
                resetVisibility: true);

            if (string.IsNullOrWhiteSpace(
                    currentPauseRoomCode))
            {
                return;
            }
        }

        roomCodeRevealed =
            !roomCodeRevealed;

        RefreshPauseRoomCodeValue();
        UpdatePauseRoomCodeShowHideLabel();
        AtlasBoardAudioManager.Instance?.PlayUiClick();
    }

    private void CopyPauseRoomCode()
    {
        if (string.IsNullOrWhiteSpace(
                currentPauseRoomCode))
        {
            RefreshPauseRoomCodeSection(
                resetVisibility: false);

            if (string.IsNullOrWhiteSpace(
                    currentPauseRoomCode))
            {
                return;
            }
        }

        GUIUtility.systemCopyBuffer =
            currentPauseRoomCode;

        SetText(
            roomCodeCopyButtonText,
            Localize(
                "leaveflow.pause.room_code_copied",
                "COPIED"));

        if (copyFeedbackCoroutine != null)
        {
            StopCoroutine(
                copyFeedbackCoroutine);
        }

        copyFeedbackCoroutine =
            StartCoroutine(
                RestorePauseCopyLabelAfterDelay());

        AtlasBoardAudioManager.Instance?.PlayUiClick();
    }

    private IEnumerator RestorePauseCopyLabelAfterDelay()
    {
        yield return new WaitForSecondsRealtime(
            1.25f);

        copyFeedbackCoroutine = null;

        SetText(
            roomCodeCopyButtonText,
            Localize(
                "leaveflow.pause.room_code_copy",
                "COPY"));
    }

    private void RefreshPauseRoomCodeValue()
    {
        SetText(
            roomCodeValueText,
            roomCodeRevealed
                ? currentPauseRoomCode
                : "••••••");

        UpdatePauseRoomCodeShowHideLabel();
    }

    private void UpdatePauseRoomCodeShowHideLabel()
    {
        SetText(
            roomCodeShowHideButtonText,
            roomCodeRevealed
                ? Localize(
                    "leaveflow.pause.room_code_hide",
                    "HIDE")
                : Localize(
                    "leaveflow.pause.room_code_show",
                    "SHOW"));
    }

    private void ResetPauseRoomCodeActionLabels()
    {
        UpdatePauseRoomCodeShowHideLabel();

        SetText(
            roomCodeCopyButtonText,
            Localize(
                "leaveflow.pause.room_code_copy",
                "COPY"));
    }

    private static bool IsSixDigitRoomCode(
        string code)
    {
        if (string.IsNullOrWhiteSpace(code) ||
            code.Length != 6)
        {
            return false;
        }

        for (int i = 0;
             i < code.Length;
             i++)
        {
            if (!char.IsDigit(code[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static void SetText(
        TMP_Text text,
        string value)
    {
        if (text != null)
        {
            text.text =
                value ?? string.Empty;
        }
    }

    private static string Localize(
        string key,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback ?? string.Empty;
        }

        string value =
            AtlasBoardL.T(
                key);

        return string.IsNullOrWhiteSpace(value) ||
               string.Equals(
                   value,
                   key,
                   StringComparison.OrdinalIgnoreCase)
            ? fallback ?? string.Empty
            : value;
    }


    private static bool ShouldFreezeSimulationForPause()
    {
        // Any active online session owns simulation time. A local pause menu on
        // one client must never stall the other connected players.
        if (FindActiveOnlineSessionExitHandler() != null)
        {
            return false;
        }

        // Keep classic pause behavior for a single-Human offline match with bots,
        // but do not freeze shared simulation when 2+ Human seats participate.
        return CountParticipatingHumanPlayers() <= 1;
    }

    private static int CountParticipatingHumanPlayers()
    {
        PlayerGameState[] players =
            UnityEngine.Object.FindObjectsByType<PlayerGameState>(
                FindObjectsInactive.Include);

        int humanCount = 0;

        foreach (PlayerGameState player in players)
        {
            if (player == null ||
                !player.gameObject.scene.IsValid() ||
                !player.IsParticipating)
            {
                continue;
            }

            BotPlayerController botController =
                player.GetComponent<BotPlayerController>();

            bool isBot =
                botController != null &&
                botController.BotEnabled;

            if (!isBot)
            {
                humanCount++;
            }
        }

        return humanCount;
    }

    private static IAtlasBoardSessionExitHandler FindActiveOnlineSessionExitHandler()
    {
        MonoBehaviour[] behaviours =
            Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null ||
                !behaviour.gameObject.scene.IsValid() ||
                behaviour is not IAtlasBoardSessionExitHandler handler ||
                !handler.IsOnlineSessionActive)
            {
                continue;
            }

            return handler;
        }

        return null;
    }

    private static T FindIncludingInactive<T>() where T : UnityEngine.Object
    {
        T[] items = Resources.FindObjectsOfTypeAll<T>();

        foreach (T item in items)
        {
            if (item == null)
            {
                continue;
            }

            Component component = item as Component;

            if (component != null && component.gameObject.scene.IsValid())
            {
                return item;
            }
        }

        return null;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();

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

    private static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), objectName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void AddClick(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private static void RemoveClick(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }

    private static bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        TurnManager newTurnManager,
        UXKeyboardShortcutController newGameplayShortcutController,
        GameObject newMainMenuCanvas,
        GameObject newMainMenuRoot,
        GameObject newLobbyRoot,
        GameObject newPauseRoot,
        GameObject newLeaveConfirmationRoot,
        GameObject newRoomCodeSectionRoot,
        TMP_Text newRoomCodeValueText,
        Button newRoomCodeShowHideButton,
        TMP_Text newRoomCodeShowHideButtonText,
        Button newRoomCodeCopyButton,
        TMP_Text newRoomCodeCopyButtonText,
        Button newResumeButton,
        Button newSettingsButton,
        Button newLeaveMatchButton,
        Button newQuitGameButton,
        Button newCancelLeaveButton,
        Button newConfirmLeaveButton,
        Button newLeaveLobbyButton)
    {
        turnManager = newTurnManager;
        gameplayShortcutController = newGameplayShortcutController;
        mainMenuCanvas = newMainMenuCanvas;
        mainMenuRoot = newMainMenuRoot;
        lobbyRoot = newLobbyRoot;
        pauseRoot = newPauseRoot;
        leaveConfirmationRoot = newLeaveConfirmationRoot;
        roomCodeSectionRoot = newRoomCodeSectionRoot;
        roomCodeValueText = newRoomCodeValueText;
        roomCodeShowHideButton = newRoomCodeShowHideButton;
        roomCodeShowHideButtonText =
            newRoomCodeShowHideButtonText;
        roomCodeCopyButton = newRoomCodeCopyButton;
        roomCodeCopyButtonText =
            newRoomCodeCopyButtonText;
        resumeButton = newResumeButton;
        settingsButton = newSettingsButton;
        leaveMatchButton = newLeaveMatchButton;
        quitGameButton = newQuitGameButton;
        cancelLeaveButton = newCancelLeaveButton;
        confirmLeaveButton = newConfirmLeaveButton;
        leaveLobbyButton = newLeaveLobbyButton;
    }
#endif
}
