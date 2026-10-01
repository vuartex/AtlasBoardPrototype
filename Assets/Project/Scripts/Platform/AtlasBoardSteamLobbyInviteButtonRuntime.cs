using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(12000)]
[DisallowMultipleComponent]
public sealed class AtlasBoardSteamLobbyInviteButtonRuntime :
    MonoBehaviour
{
    private const string StartButtonName =
        "Button_StartMatch";

    private const string InviteButtonName =
        "Button_InviteSteamFriends";

    private const float ResolveIntervalSeconds =
        0.25f;

    private static AtlasBoardSteamLobbyInviteButtonRuntime
        instance;

    private Button startMatchButton;
    private Button inviteButton;
    private TMP_Text inviteButtonText;

    private AtlasBoardPlatformRuntime
        platformRuntime;

    private AtlasBoardLobbyRuntimeBridge
        lobbyBridge;

    private float nextResolveAt;
    private bool inviteInFlight;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntime()
    {
        if (instance != null)
        {
            return;
        }

        AtlasBoardSteamLobbyInviteButtonRuntime existing =
            FindAnyObjectByType<
                AtlasBoardSteamLobbyInviteButtonRuntime>(
                FindObjectsInactive.Include);

        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject root =
            new GameObject(
                "AtlasBoardSteamLobbyInviteButtonRuntime");

        root.AddComponent<
            AtlasBoardSteamLobbyInviteButtonRuntime>();
    }

    private void Awake()
    {
        if (instance != null &&
            instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        AtlasBoardLocalizationManager.LanguageChanged +=
            RefreshLocalizedText;
    }

    private void OnDestroy()
    {
        AtlasBoardLocalizationManager.LanguageChanged -=
            RefreshLocalizedText;

        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >=
            nextResolveAt)
        {
            nextResolveAt =
                Time.unscaledTime +
                ResolveIntervalSeconds;

            ResolveReferences();
        }

        RefreshVisibilityAndState();
    }

    private void ResolveReferences()
    {
        platformRuntime =
            platformRuntime != null
                ? platformRuntime
                : AtlasBoardPlatformRuntime.Instance;

        if (lobbyBridge == null)
        {
            AtlasBoardLobbyRuntimeBridge[] bridges =
                Resources.FindObjectsOfTypeAll<
                    AtlasBoardLobbyRuntimeBridge>();

            lobbyBridge =
                bridges.FirstOrDefault(
                    item =>
                        item != null &&
                        item.gameObject.scene.IsValid());
        }

        if (startMatchButton == null)
        {
            Button[] buttons =
                Resources.FindObjectsOfTypeAll<
                    Button>();

            startMatchButton =
                buttons.FirstOrDefault(
                    item =>
                        item != null &&
                        item.gameObject.scene.IsValid() &&
                        item.name == StartButtonName);
        }

        if (startMatchButton != null &&
            inviteButton == null)
        {
            BuildInviteButton();
        }

        AlignInviteButton();
    }

    private void BuildInviteButton()
    {
        if (startMatchButton == null)
        {
            return;
        }

        Transform parent =
            startMatchButton.transform.parent;

        RectTransform startRect =
            startMatchButton.GetComponent<
                RectTransform>();

        Image startImage =
            startMatchButton.GetComponent<Image>();

        TMP_Text startText =
            startMatchButton.GetComponentInChildren<
                TMP_Text>(true);

        if (parent == null ||
            startRect == null ||
            startImage == null ||
            startText == null)
        {
            return;
        }

        GameObject buttonObject =
            new GameObject(
                InviteButtonName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));

        buttonObject.transform.SetParent(
            parent,
            false);

        RectTransform inviteRect =
            buttonObject.GetComponent<
                RectTransform>();

        Image inviteImage =
            buttonObject.GetComponent<Image>();

        inviteImage.sprite =
            startImage.sprite;
        inviteImage.overrideSprite =
            startImage.overrideSprite;
        inviteImage.color =
            startImage.color;
        inviteImage.material =
            startImage.material;
        inviteImage.type =
            startImage.type;
        inviteImage.preserveAspect =
            startImage.preserveAspect;
        inviteImage.fillCenter =
            startImage.fillCenter;
        inviteImage.fillMethod =
            startImage.fillMethod;
        inviteImage.fillAmount =
            startImage.fillAmount;
        inviteImage.fillClockwise =
            startImage.fillClockwise;
        inviteImage.fillOrigin =
            startImage.fillOrigin;
        inviteImage.pixelsPerUnitMultiplier =
            startImage.pixelsPerUnitMultiplier;

        Button startButton =
            startMatchButton.GetComponent<Button>();

        inviteButton =
            buttonObject.GetComponent<Button>();

        inviteButton.targetGraphic =
            inviteImage;

        if (startButton != null)
        {
            inviteButton.transition =
                startButton.transition;
            inviteButton.colors =
                startButton.colors;
            inviteButton.spriteState =
                startButton.spriteState;
            inviteButton.navigation =
                startButton.navigation;
        }

        inviteButton.onClick.AddListener(
            HandleInviteClicked);

        GameObject textObject =
            new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        textObject.transform.SetParent(
            buttonObject.transform,
            false);

        inviteButtonText =
            textObject.GetComponent<
                TextMeshProUGUI>();

        RectTransform textRect =
            inviteButtonText.rectTransform;

        textRect.anchorMin =
            Vector2.zero;
        textRect.anchorMax =
            Vector2.one;
        textRect.offsetMin =
            Vector2.zero;
        textRect.offsetMax =
            Vector2.zero;

        inviteButtonText.font =
            startText.font;
        inviteButtonText.fontSharedMaterial =
            startText.fontSharedMaterial;
        inviteButtonText.color =
            startText.color;
        inviteButtonText.fontStyle =
            startText.fontStyle;
        inviteButtonText.alignment =
            startText.alignment;
        inviteButtonText.enableAutoSizing =
            true;
        inviteButtonText.fontSizeMin =
            15f;
        inviteButtonText.fontSizeMax =
            Mathf.Max(
                24f,
                startText.fontSize);
        inviteButtonText.raycastTarget =
            false;
        inviteButtonText.textWrappingMode = TextWrappingModes.NoWrap;
        inviteButtonText.overflowMode =
            TextOverflowModes.Truncate;

        inviteRect.anchorMin =
            startRect.anchorMin;
        inviteRect.anchorMax =
            startRect.anchorMax;
        inviteRect.pivot =
            startRect.pivot;
        inviteRect.sizeDelta =
            startRect.sizeDelta;
        inviteRect.localScale =
            startRect.localScale;
        inviteRect.localRotation =
            startRect.localRotation;

        AlignInviteButton();
        RefreshLocalizedText();
        RefreshVisibilityAndState();
    }

    private void AlignInviteButton()
    {
        if (inviteButton == null ||
            startMatchButton == null)
        {
            return;
        }

        RectTransform startRect =
            startMatchButton.GetComponent<
                RectTransform>();

        RectTransform inviteRect =
            inviteButton.GetComponent<
                RectTransform>();

        if (startRect == null ||
            inviteRect == null)
        {
            return;
        }

        inviteRect.anchorMin =
            startRect.anchorMin;
        inviteRect.anchorMax =
            startRect.anchorMax;
        inviteRect.pivot =
            startRect.pivot;
        inviteRect.sizeDelta =
            startRect.sizeDelta;

        float horizontalGap =
            Mathf.Max(
                24f,
                startRect.sizeDelta.x * 0.06f);

        inviteRect.anchoredPosition =
            startRect.anchoredPosition +
            Vector2.left *
            (startRect.sizeDelta.x +
             horizontalGap);

        inviteRect.localScale =
            startRect.localScale;
        inviteRect.localRotation =
            startRect.localRotation;
    }

    private void RefreshVisibilityAndState()
    {
        if (inviteButton == null)
        {
            return;
        }

        bool steamReady =
            platformRuntime != null &&
            platformRuntime.IsSteamProviderActive &&
            platformRuntime.SteamInitialized;

        bool waitingRoom =
            lobbyBridge != null &&
            lobbyBridge.HasLobby &&
            lobbyBridge.CurrentSnapshot != null &&
            lobbyBridge.CurrentSnapshot.LifecycleState ==
                AtlasRoomLifecycleState.Waiting;

        bool visible =
            steamReady &&
            waitingRoom;

        if (inviteButton.gameObject.activeSelf != visible)
        {
            inviteButton.gameObject.SetActive(
                visible);
        }

        inviteButton.interactable =
            visible &&
            !inviteInFlight;
    }

    private void RefreshLocalizedText()
    {
        if (inviteButtonText == null)
        {
            return;
        }

        string localized =
            AtlasBoardL.T(
                "platform.steam.invite_friends");

        inviteButtonText.text =
            string.IsNullOrWhiteSpace(localized) ||
            localized ==
                "platform.steam.invite_friends"
                ? "INVITE FRIENDS"
                : localized;

        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;

        if (localization != null)
        {
            inviteButtonText.font =
                localization.ResolveFont(
                    inviteButtonText.font);
        }
    }

    private void HandleInviteClicked()
    {
        _ = OpenInviteAsync();
    }

    private async Task OpenInviteAsync()
    {
        if (inviteInFlight ||
            platformRuntime == null)
        {
            return;
        }

        inviteInFlight = true;
        RefreshVisibilityAndState();

        try
        {
            bool opened =
                await platformRuntime
                    .ShowInviteUiAsync();

            if (!opened)
            {
                Debug.LogWarning(
                    "AtlasBoard Steam lobby invite button could not open " +
                    "the Steam invite dialog. Confirm Steam is initialized " +
                    "and the room is still in Waiting state.",
                    this);
            }
        }
        finally
        {
            inviteInFlight = false;
            RefreshVisibilityAndState();
        }
    }
}
