using System;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Styles the existing shared tablet; the result-only exit lives in the same
// component so no second tablet/leave manager or Editor menu is introduced.
[DefaultExecutionOrder(14000)]
public class TabletShellStyler : MonoBehaviour
{
    private const string FrameName = "__UX_TabletFrame";
    private const string ScreenName = "__UX_TabletScreen";
    private const string HeaderName = "__UX_TabletHeader";
    private const string CameraDotName = "__UX_TabletCameraDot";
    private const string HeaderLineName = "__UX_TabletHeaderLine";
    private const string ResultExitButtonName = "__UX_MatchResultExitButton";
    private const string ResultExitConfirmName = "__UX_MatchResultExitConfirmation";

    [Header("Tablet")]
    [SerializeField] private GameObject backdropDim;
    [SerializeField] private GameObject tabletRoot;
    [SerializeField] private TMP_Text tabletTitleText;

    [Header("Style")]
    [SerializeField]
    private Color backdropColor = new Color(0f, 0f, 0f, 0.58f);

    [SerializeField]
    private Color frameColor = new Color(0.055f, 0.065f, 0.08f, 1f);

    [SerializeField]
    private Color screenColor = new Color(0.025f, 0.032f, 0.045f, 1f);

    [SerializeField]
    private Color headerColor = new Color(0.075f, 0.09f, 0.12f, 1f);

    private MatchResultManager resultManager;
    private RectTransform resultExitButton;
    private GameObject resultExitConfirmation;
    private TMP_Text resultExitQuestion;
    private TMP_Text resultExitConfirmLabel;
    private TMP_Text resultExitCancelLabel;
    private bool resultExitCloseInFlight;
    private bool resultExitCloseFailed;

    private void Awake()
    {
        // Older serialized scenes contain a nine-by-nine rectangular "camera
        // dot" centred in the tablet header. Remove it at runtime too: the
        // player must not have to regenerate or hand-edit the scene/prefab.
        RemoveLegacyCameraDot();
    }

    private void OnEnable()
    {
        AtlasBoardLocalizationManager.LanguageChanged += RefreshResultExitCopy;
    }

    private void OnDisable()
    {
        AtlasBoardLocalizationManager.LanguageChanged -= RefreshResultExitCopy;

        if (resultExitConfirmation != null)
            resultExitConfirmation.SetActive(false);
        if (resultExitButton != null)
            resultExitButton.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (tabletRoot == null)
            return;

        // A serialized camera dot can be re-created by an Editor style pass;
        // keep the actual runtime result/header clean without touching assets.
        RemoveLegacyCameraDot();

        if (resultManager == null)
            resultManager = FindAnyObjectByType<MatchResultManager>();

        bool isResultVisible =
            resultManager != null &&
            resultManager.ResultShown &&
            tabletRoot.activeInHierarchy &&
            tabletTitleText != null &&
            string.Equals(
                tabletTitleText.text,
                AtlasBoardL.T("tablet.match_result"),
                System.StringComparison.Ordinal);

        if (!isResultVisible)
        {
            if (resultExitButton != null && resultExitButton.gameObject.activeSelf)
                resultExitButton.gameObject.SetActive(false);
            if (resultExitConfirmation != null && resultExitConfirmation.activeSelf)
                resultExitConfirmation.SetActive(false);
            return;
        }

        EnsureResultExitControls();
        if (resultExitButton == null)
            return;

        if (!resultExitButton.gameObject.activeSelf)
            resultExitButton.gameObject.SetActive(true);

        // FinalMatchResultLayout moves its player cards to the last sibling in
        // its LateUpdate. This component runs after it, so confirmation stays
        // above all four cards and consumes clicks safely.
        if (resultExitConfirmation != null && resultExitConfirmation.activeSelf)
            resultExitConfirmation.transform.SetAsLastSibling();
        else
            resultExitButton.SetAsLastSibling();
    }

    private void RemoveLegacyCameraDot()
    {
        if (tabletRoot == null)
            return;

        Transform cameraDot = tabletRoot.transform.Find(CameraDotName);
        if (cameraDot == null)
            return;

        // Hide immediately, then destroy once. The old child exists in scenes
        // authored by prior versions of ApplyStyle().
        cameraDot.gameObject.SetActive(false);
        if (Application.isPlaying)
            Destroy(cameraDot.gameObject);
        else
            DestroyImmediate(cameraDot.gameObject);
    }

    [ContextMenu("Apply Tablet Shell Style")]
    public void ApplyStyle()
    {
        if (tabletRoot == null)
        {
            Debug.LogWarning("TabletShellStyler has no Tablet Root.", this);
            return;
        }

        RectTransform rootRect = tabletRoot.GetComponent<RectTransform>();
        if (rootRect == null)
        {
            Debug.LogWarning("Tablet Root is not a RectTransform.", tabletRoot);
            return;
        }

        if (backdropDim != null)
        {
            Image backdropImage = backdropDim.GetComponent<Image>();
            if (backdropImage != null)
                backdropImage.color = backdropColor;
        }

        Image rootImage = tabletRoot.GetComponent<Image>();
        if (rootImage == null)
            rootImage = tabletRoot.AddComponent<Image>();
        rootImage.color = frameColor;
        rootImage.raycastTarget = false;

        RemoveGeneratedChild(rootRect, FrameName);
        RemoveGeneratedChild(rootRect, ScreenName);
        RemoveGeneratedChild(rootRect, HeaderName);
        RemoveGeneratedChild(rootRect, CameraDotName);
        RemoveGeneratedChild(rootRect, HeaderLineName);

        Image frame = CreateStretchImage(FrameName, rootRect, -18f, frameColor);
        Outline outline = frame.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
        outline.effectDistance = new Vector2(5f, -5f);
        frame.transform.SetSiblingIndex(0);

        Image screen = CreateStretchImage(ScreenName, rootRect, 10f, screenColor);
        screen.transform.SetSiblingIndex(1);

        Image header = CreateTopBar(HeaderName, rootRect, 64f, headerColor);
        header.transform.SetSiblingIndex(2);

        Image headerLine = CreateHeaderLine(HeaderLineName, rootRect);
        headerLine.transform.SetSiblingIndex(3);

        // The old CameraDotName child is deliberately NOT reconstructed.
        // It was the visible blue 9x9 square beside MAÇ SONUCU.
        if (tabletTitleText != null)
        {
            tabletTitleText.fontStyle = FontStyles.Bold;
            tabletTitleText.fontSize = Mathf.Max(26f, tabletTitleText.fontSize);
            tabletTitleText.color = new Color(0.94f, 0.96f, 1f, 1f);
        }

        Debug.Log("Tablet shell style applied.", this);
    }

    private void EnsureResultExitControls()
    {
        RectTransform parent = tabletRoot != null
            ? tabletRoot.GetComponent<RectTransform>()
            : null;
        if (parent == null)
            return;

        if (resultExitButton == null)
        {
            Transform existing = parent.Find(ResultExitButtonName);
            if (existing != null)
                resultExitButton = existing as RectTransform;

            if (resultExitButton == null)
            {
                GameObject buttonRoot = new GameObject(
                    ResultExitButtonName,
                    typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Image), typeof(Button));
                resultExitButton = buttonRoot.GetComponent<RectTransform>();
                resultExitButton.SetParent(parent, false);
                resultExitButton.anchorMin = new Vector2(1f, 1f);
                resultExitButton.anchorMax = new Vector2(1f, 1f);
                resultExitButton.pivot = new Vector2(1f, 1f);
                resultExitButton.sizeDelta = new Vector2(44f, 44f);
                resultExitButton.anchoredPosition = new Vector2(-26f, -12f);

                Image background = buttonRoot.GetComponent<Image>();
                background.color = new Color(0.17f, 0.22f, 0.29f, 0.95f);
                Button button = buttonRoot.GetComponent<Button>();
                button.targetGraphic = background;
                button.transition = Selectable.Transition.ColorTint;
                button.onClick.AddListener(OpenResultExitConfirmation);
                CreateText("X", resultExitButton, "X", 27f,
                    TextAlignmentOptions.Center, Color.white);
            }
        }

        if (resultExitConfirmation == null)
        {
            Transform existing = parent.Find(ResultExitConfirmName);
            if (existing != null)
                resultExitConfirmation = existing.gameObject;

            if (resultExitConfirmation == null)
            {
                RectTransform overlay = CreateImage(
                    ResultExitConfirmName, parent,
                    new Color(0f, 0.015f, 0.025f, 0.87f));
                overlay.anchorMin = Vector2.zero;
                overlay.anchorMax = Vector2.one;
                overlay.offsetMin = Vector2.zero;
                overlay.offsetMax = Vector2.zero;
                overlay.GetComponent<Image>().raycastTarget = true;
                overlay.gameObject.AddComponent<AtlasBoardEscapeBlocker>();
                resultExitConfirmation = overlay.gameObject;

                RectTransform dialog = CreateImage(
                    "ConfirmationCard", overlay,
                    new Color(0.085f, 0.10f, 0.14f, 1f));
                dialog.anchorMin = new Vector2(0.5f, 0.5f);
                dialog.anchorMax = new Vector2(0.5f, 0.5f);
                dialog.pivot = new Vector2(0.5f, 0.5f);
                dialog.sizeDelta = new Vector2(570f, 235f);
                dialog.anchoredPosition = Vector2.zero;
                dialog.GetComponent<Image>().raycastTarget = true;

                RectTransform titleBox = CreateRect("Question", dialog,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(520f, 90f),
                    new Vector2(0f, 44f));
                resultExitQuestion = CreateText("QuestionText", titleBox,
                    string.Empty, 26f, TextAlignmentOptions.Center,
                    new Color(0.94f, 0.96f, 1f, 1f));

                RectTransform cancelRoot = CreateActionButton(
                    "Cancel", dialog, new Vector2(-129f, -67f),
                    new Color(0.20f, 0.25f, 0.34f, 1f),
                    CancelResultExit);
                resultExitCancelLabel = CreateText(
                    "CancelText", cancelRoot, string.Empty, 21f,
                    TextAlignmentOptions.Center, Color.white);

                RectTransform confirmRoot = CreateActionButton(
                    "Confirm", dialog, new Vector2(129f, -67f),
                    new Color(0.12f, 0.37f, 0.42f, 1f),
                    ConfirmResultExit);
                resultExitConfirmLabel = CreateText(
                    "ConfirmText", confirmRoot, string.Empty, 21f,
                    TextAlignmentOptions.Center, Color.white);
                resultExitConfirmation.SetActive(false);
            }
            else
            {
                Transform card = resultExitConfirmation.transform.Find("ConfirmationCard");
                resultExitQuestion = card != null
                    ? card.Find("Question")?.GetComponentInChildren<TMP_Text>(true)
                    : null;
                resultExitCancelLabel = card != null
                    ? card.Find("Cancel")?.GetComponentInChildren<TMP_Text>(true)
                    : null;
                resultExitConfirmLabel = card != null
                    ? card.Find("Confirm")?.GetComponentInChildren<TMP_Text>(true)
                    : null;
            }
        }

        RefreshResultExitCopy();
    }

    private void RefreshResultExitCopy()
    {
        if (resultExitQuestion != null)
        {
            if (resultExitCloseInFlight)
            {
                resultExitQuestion.text = AtlasBoardL.R(
                    "Closing the room...",
                    "Oda kapatılıyor...",
                    "Cerrando la sala...",
                    "Fermeture du salon...",
                    "Raum wird geschlossen...",
                    "방을 종료하는 중...",
                    "Закрытие комнаты...");
            }
            else if (resultExitCloseFailed)
            {
                resultExitQuestion.text = AtlasBoardL.R(
                    "The room could not be closed. Try again?",
                    "Oda kapatılamadı. Tekrar denemek ister misin?",
                    "No se pudo cerrar la sala. ¿Reintentar?",
                    "Impossible de fermer le salon. Réessayer ?",
                    "Raum konnte nicht geschlossen werden. Erneut versuchen?",
                    "방을 종료하지 못했습니다. 다시 시도하시겠습니까?",
                    "Не удалось закрыть комнату. Повторить?");
            }
            else
            {
                resultExitQuestion.text = AtlasBoardL.R(
                    "Return to the main menu?",
                    "Ana menüye dönmek istiyor musun?",
                    "¿Quieres volver al menú principal?",
                    "Voulez-vous revenir au menu principal ?",
                    "Zum Hauptmenü zurückkehren?",
                    "메인 메뉴로 돌아가시겠습니까?",
                    "Вернуться в главное меню?");
            }
        }

        if (resultExitCancelLabel != null)
        {
            resultExitCancelLabel.text = AtlasBoardL.R(
                "CANCEL", "İPTAL", "CANCELAR", "ANNULER",
                "ABBRECHEN", "취소", "ОТМЕНА");
        }

        if (resultExitConfirmLabel != null)
        {
            resultExitConfirmLabel.text = resultExitCloseFailed
                ? AtlasBoardL.R(
                    "RETRY", "TEKRAR DENE", "REINTENTAR", "RÉESSAYER",
                    "ERNEUT VERSUCHEN", "다시 시도", "ПОВТОРИТЬ")
                : AtlasBoardL.R(
                    "MAIN MENU", "ANA MENÜYE DÖN", "MENÚ PRINCIPAL",
                    "MENU PRINCIPAL", "HAUPTMENÜ", "메인 메뉴", "ГЛАВНОЕ МЕНЮ");
        }
    }

    private void OpenResultExitConfirmation()
    {
        if (resultExitCloseInFlight ||
            resultManager == null || !resultManager.ResultShown)
            return;

        EnsureResultExitControls();
        if (resultExitConfirmation == null)
            return;

        resultExitCloseFailed = false;
        RefreshResultExitCopy();
        resultExitConfirmation.SetActive(true);
        resultExitConfirmation.transform.SetAsLastSibling();
        AtlasBoardAudioManager.Instance?.PlayUiOpen();
    }

    private void CancelResultExit()
    {
        // Once the Host's network close has started, this dialog cannot be
        // cancelled: an acknowledged server close must not look undoable.
        if (resultExitCloseInFlight)
            return;

        resultExitCloseFailed = false;
        if (resultExitConfirmation != null)
            resultExitConfirmation.SetActive(false);
        AtlasBoardAudioManager.Instance?.PlayUiClick();
    }

    private void ConfirmResultExit()
    {
        if (resultExitCloseInFlight)
            return;

        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            FindAnyObjectByType<AtlasBoardTurnDiceNetworkCoordinator>();

        // The normal Leave Match handler intentionally starts Host Migration.
        // The result-tablet X has different semantics: Host explicitly closes
        // its own completed room. The existing RTDB close first publishes
        // roomMeta.closed=true and removes the host-owned room, which tells
        // the follower to detach and return to the Main Menu. Do not migrate
        // the result Host to a player who may still be viewing the result.
        if (coordinator != null && coordinator.IsPreparedOnlineMatch &&
            coordinator.LocalIsHost)
        {
            _ = CloseFinishedHostRoomAsync(coordinator);
            return;
        }

        // The remote-player X route is already owner-accepted. Do not change
        // its membership leave / online seat cleanup or the Host REMATCH path.
        if (resultExitConfirmation != null)
            resultExitConfirmation.SetActive(false);

        AtlasBoardLeaveFlowController leaveFlow =
            FindAnyObjectByType<AtlasBoardLeaveFlowController>();
        if (leaveFlow != null && leaveFlow.isActiveAndEnabled)
        {
            leaveFlow.ConfirmLeaveMatch();
            return;
        }

        if (coordinator != null && coordinator.IsPreparedOnlineMatch)
        {
            if (!coordinator.TryHandleLeaveMatch())
                Debug.LogWarning(
                    "Result exit: online leave was not accepted; scene unchanged.",
                    this);
            return;
        }

        // Local/offline games keep the existing scene-reload fallback.
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && !string.IsNullOrWhiteSpace(activeScene.name))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(activeScene.name, LoadSceneMode.Single);
        }
    }

    private async Task CloseFinishedHostRoomAsync(
        AtlasBoardTurnDiceNetworkCoordinator coordinator)
    {
        if (resultExitCloseInFlight)
            return;

        AtlasBoardLobbyRuntimeBridge lobbyBridge =
            FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>();
        AtlasBoardMainMenuController mainMenu =
            FindAnyObjectByType<AtlasBoardMainMenuController>();

        // Reuse precisely the established coordinator cleanup (which clears
        // prepared, heartbeat, presence and match bridge state). Keeping
        // prepared=true after a room close is another way to strand a client.
        // Resolve this existing method BEFORE deleting any remote room.
        MethodInfo detachMethod =
            typeof(AtlasBoardTurnDiceNetworkCoordinator).GetMethod(
                "DetachLocalClientFromActiveMatch",
                BindingFlags.Instance | BindingFlags.NonPublic);
        if (lobbyBridge == null || !lobbyBridge.HasLobby ||
            mainMenu == null || detachMethod == null ||
            coordinator == null || !coordinator.IsPreparedOnlineMatch ||
            !coordinator.LocalIsHost)
        {
            Debug.LogWarning(
                "ATB_RESULT_HOST_CLOSE preflight_failed=true " +
                "room_was_not_modified=true", this);
            ShowHostRoomCloseError();
            return;
        }

        resultExitCloseInFlight = true;
        resultExitCloseFailed = false;
        SetResultExitDialogInteractable(false);
        RefreshResultExitCopy();

        bool roomClosed = false;
        try
        {
            // Existing authenticated, current-host-only RTDB operation:
            // first closes roomMeta to admission, then removes this room's
            // join code, match, state, members and ancillary records.
            // No raw client write, Firebase rules change or other-room delete.
            AtlasLobbyOperationResult closeResult =
                await lobbyBridge.CloseLobbyAsync();
            roomClosed = closeResult != null && closeResult.Success;
            if (!roomClosed)
            {
                Debug.LogWarning(
                    "ATB_RESULT_HOST_CLOSE acknowledged=false " +
                    "retry_available=true", this);
            }
        }
        catch (Exception error)
        {
            Debug.LogWarning(
                "ATB_RESULT_HOST_CLOSE failed=" + error.GetType().Name +
                " retry_available=true", this);
        }
        finally
        {
            resultExitCloseInFlight = false;
        }

        if (!roomClosed)
        {
            ShowHostRoomCloseError();
            return;
        }

        if (resultExitConfirmation != null)
            resultExitConfirmation.SetActive(false);
        Debug.Log(
            "ATB_RESULT_HOST_CLOSE room_closed=true " +
            "follower_exit_via_closed_room_poll=expected", this);

        try
        {
            // The coordinator's established detach also invokes the
            // canonical ShowMainMenuAfterActiveMatchExit() reset sequence.
            // Do not trigger Host Migration or an unsafe scene reload.
            if (coordinator != null && coordinator.IsPreparedOnlineMatch)
                detachMethod.Invoke(coordinator, null);
            else
                mainMenu.ShowMainMenuAfterActiveMatchExit();
        }
        catch (Exception error)
        {
            Debug.LogError(
                "ATB_RESULT_HOST_CLOSE room_closed=true " +
                "local_detach_failed=" + error.GetType().Name, this);
            // Server already closed the room; do not leave a stranded result
            // tablet on the Host due only to a local presentation failure.
            if (mainMenu != null)
                mainMenu.ShowMainMenuAfterActiveMatchExit();
        }
    }

    private void SetResultExitDialogInteractable(bool interactable)
    {
        if (resultExitConfirmation == null)
            return;
        Transform card = resultExitConfirmation.transform.Find("ConfirmationCard");
        if (card == null)
            return;
        Button cancel = card.Find("Cancel")?.GetComponent<Button>();
        Button confirm = card.Find("Confirm")?.GetComponent<Button>();
        if (cancel != null)
            cancel.interactable = interactable;
        if (confirm != null)
            confirm.interactable = interactable;
    }

    private void ShowHostRoomCloseError()
    {
        resultExitCloseFailed = true;
        SetResultExitDialogInteractable(true);
        RefreshResultExitCopy();
        if (resultExitConfirmation != null)
        {
            resultExitConfirmation.SetActive(true);
            resultExitConfirmation.transform.SetAsLastSibling();
        }
    }

    private RectTransform CreateActionButton(
        string objectName, RectTransform parent, Vector2 position,
        Color backgroundColor, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = CreateImage(objectName, parent, backgroundColor);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(222f, 55f);
        rect.anchoredPosition = position;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(action);
        return rect;
    }

    private TMP_Text CreateText(
        string objectName, RectTransform parent, string value,
        float size, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = new GameObject(objectName,
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(5f, 2f);
        rect.offsetMax = new Vector2(-5f, -2f);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (tabletTitleText != null && tabletTitleText.font != null)
            tmp.font = tabletTitleText.font;
        tmp.text = value;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static RectTransform CreateRect(
        string objectName, RectTransform parent,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 size, Vector2 position)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static RectTransform CreateImage(
        string objectName, RectTransform parent, Color color)
    {
        GameObject go = new GameObject(objectName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        return rect;
    }

    private static Image CreateStretchImage(
        string objectName, RectTransform parent, float inset, Color color)
    {
        RectTransform rect = CreateImage(objectName, parent, color);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        Image image = rect.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateTopBar(
        string objectName, RectTransform parent, float height, Color color)
    {
        RectTransform rect = CreateImage(objectName, parent, color);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(12f, -height);
        rect.offsetMax = new Vector2(-12f, 0f);
        Image image = rect.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateHeaderLine(string objectName, RectTransform parent)
    {
        RectTransform rect = CreateImage(objectName, parent, Color.white);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-30f, 2f);
        rect.anchoredPosition = new Vector2(0f, -64f);
        Image image = rect.GetComponent<Image>();
        image.color = new Color(0.23f, 0.35f, 0.52f, 0.65f);
        image.raycastTarget = false;
        return image;
    }

    private static void RemoveGeneratedChild(
        RectTransform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing == null)
            return;
        if (Application.isPlaying)
        {
            existing.gameObject.SetActive(false);
            Destroy(existing.gameObject);
        }
        else
            DestroyImmediate(existing.gameObject);
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        GameObject newBackdropDim,
        GameObject newTabletRoot,
        TMP_Text newTabletTitleText)
    {
        backdropDim = newBackdropDim;
        tabletRoot = newTabletRoot;
        tabletTitleText = newTabletTitleText;
    }
#endif
}
