using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Android/iOS-only gameplay usability and performance layer.
///
/// Desktop/Steam input, saved graphics settings and desktop presentation are
/// deliberately not modified. Mobile receives:
/// - large Roll / Trade actions with safe HUD separation;
/// - one-finger rotate + two-finger pinch/pan;
/// - larger tablet/trade/city touch targets;
/// - scene-reload self-healing for Phase14 presentation components;
/// - a mobile render ceiling that is not persisted to PlayerPrefs/cloud;
/// - measured adaptive render fallback for sustained low FPS.
/// </summary>
[DefaultExecutionOrder(14000)]
public sealed class AtlasBoardMobileGameplayUX : MonoBehaviour
{
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    private const float TabletScale = 1.30f;
    private const float InitialRenderScale = 0.75f;
    private const float EmergencyRenderScale = 0.62f;

    private static AtlasBoardMobileGameplayUX instance;

    private TurnManager turnManager;
    private TradeManager tradeManager;
    private TabletUIManager tabletManager;
    private BoardCameraController boardCamera;
    private AtlasBoardCityInformationPanel cityInformation;
    private AtlasBoardTradePresentationPolish tradePresentation;

    private Canvas actionCanvas;
    private RectTransform safeRoot;
    private Button rollButton;
    private Button tradeButton;
    private TMP_Text rollLabel;
    private TMP_Text tradeLabel;

    private readonly HashSet<RectTransform> enlargedTabletRoots =
        new HashSet<RectTransform>();
    private readonly HashSet<RectTransform> enlargedCityPanels =
        new HashSet<RectTransform>();
    private readonly HashSet<RectTransform> enlargedTradeRoots =
        new HashSet<RectTransform>();
    private readonly HashSet<GameObject> recoloredCareerCards =
        new HashSet<GameObject>();

    private float nextResolveAt;
    private float nextLayoutAt;
    private float nextRenderReassertAt;
    private Rect lastSafeArea;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private ulong observedSceneHandle = ulong.MaxValue;

    private float perfWindowStartedAt;
    private int perfFrames;
    private float perfAccumulatedDelta;
    private float perfWorstFrameMs;
    private int consecutiveLowFpsWindows;
    private bool emergencyProfileApplied;
    private bool touchCameraLogged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject host =
            new GameObject("AtlasBoard_Mobile_Gameplay_UX");

        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardMobileGameplayUX>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        BuildActionCanvas();
        AtlasBoardLocalizationManager.LanguageChanged += RefreshLabels;
        AtlasBoardUserSettingsStore.SettingsSaved += HandleSettingsSaved;

        ApplyMobilePerformanceProfile(
            emergency: false,
            reason: "startup");

        ResetPerformanceWindow();

        Debug.Log(
            "ATB_MOBILE_GAMEPLAY_UX_READY targetFps=" +
            Application.targetFrameRate +
            " quality=" +
            QualitySettings.names[QualitySettings.GetQualityLevel()] +
            " aa=" + QualitySettings.antiAliasing +
            " vsync=" + QualitySettings.vSyncCount +
            " renderScale=" + GetRenderScale().ToString("F2"));
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        AtlasBoardLocalizationManager.LanguageChanged -= RefreshLabels;
        AtlasBoardUserSettingsStore.SettingsSaved -= HandleSettingsSaved;
        instance = null;
    }

    private void HandleSettingsSaved()
    {
        // Settings remain the source of player intent. Mobile then reapplies
        // its non-persistent hardware ceiling; Windows/Steam are untouched.
        nextRenderReassertAt = 0f;
    }

    private void Update()
    {
        DetectSceneChange();

        if (Time.unscaledTime >= nextResolveAt)
        {
            nextResolveAt = Time.unscaledTime + 1.00f;
            EnsureModernScenePresentation();
            ResolveReferences();
            ApplyCareerMobileAccent();
        }

        if (Time.unscaledTime >= nextRenderReassertAt)
        {
            nextRenderReassertAt = Time.unscaledTime + 8.0f;
            ApplyMobilePerformanceProfile(
                emergencyProfileApplied,
                "periodic_guard");
        }

        UpdateSafeArea();
        UpdateActionButtons();
        HandleTouchCamera();
        UpdateMobilePanels();
        UpdatePerformanceTelemetry();
    }

    private void DetectSceneChange()
    {
        Scene active = SceneManager.GetActiveScene();
        ulong handle = active.IsValid()
            ? active.handle.GetRawData()
            : ulong.MaxValue;

        if (handle == observedSceneHandle)
            return;

        observedSceneHandle = handle;

        turnManager = null;
        tradeManager = null;
        tabletManager = null;
        boardCamera = null;
        cityInformation = null;
        tradePresentation = null;

        enlargedTabletRoots.Clear();
        enlargedCityPanels.Clear();
        enlargedTradeRoots.Clear();
        recoloredCareerCards.Clear();

        nextResolveAt = 0f;
        nextLayoutAt = 0f;
        nextRenderReassertAt = 0f;
        touchCameraLogged = false;

        Debug.Log(
            "ATB_MOBILE_SCENE_REBIND scene=" +
            (active.IsValid() ? active.name : "invalid") +
            " presentation_self_heal=true");
    }

    private void EnsureModernScenePresentation()
    {
        BoardPath boardPath =
            UnityEngine.Object.FindAnyObjectByType<BoardPath>(
                FindObjectsInactive.Include);

        if (boardPath == null)
            return;

        EnsureSceneComponent<AtlasBoardPurchasePresentationPolish>(
            "AtlasBoard_Phase14K_PurchasePresentation");
        EnsureSceneComponent<AtlasBoardAuctionPresentationPolish>(
            "AtlasBoard_Phase14G_AuctionPresentation");
        EnsureSceneComponent<AtlasBoardTradePresentationPolish>(
            "AtlasBoard_Phase14H_TradePresentation");
        EnsureSceneComponent<AtlasBoardDevelopmentPresentationPolish>(
            "AtlasBoard_Phase14I_DevelopmentPresentation");
        EnsureSceneComponent<AtlasBoardMatchResultFinalLayout>(
            "AtlasBoard_Phase14F_FinalMatchResult");
        EnsureSceneComponent<AtlasBoardCityInformationPanel>(
            "AtlasBoard_Phase14A_CityInformationPanel");
        EnsureSceneComponent<AtlasBoardCityInfoCameraInputGuard>(
            "AtlasBoard_CityInfo_Camera_Input_Guard");
        EnsureSceneComponent<AtlasBoardTabletWorldInteractionGuard>(
            "AtlasBoard_Tablet_World_Input_Guard");
    }

    private static T EnsureSceneComponent<T>(
        string objectName)
        where T : Component
    {
        T existing =
            UnityEngine.Object.FindAnyObjectByType<T>(
                FindObjectsInactive.Include);

        if (existing != null)
            return existing;

        GameObject host =
            new GameObject(objectName);

        T created = host.AddComponent<T>();

        Debug.Log(
            "ATB_PRESENTATION_SELF_HEAL component=" +
            typeof(T).Name +
            " action=recreated_after_scene_load");

        return created;
    }

    private void ResolveReferences()
    {
        // Unity's destroyed scene objects retain managed wrappers. Always use
        // Unity null checks so a Board_Prototype reload cannot retain stale
        // references from the previous match.
        if (turnManager == null)
        {
            turnManager =
                UnityEngine.Object.FindAnyObjectByType<TurnManager>(
                    FindObjectsInactive.Include);
        }

        if (tradeManager == null)
        {
            tradeManager =
                UnityEngine.Object.FindAnyObjectByType<TradeManager>(
                    FindObjectsInactive.Include);
        }

        if (tabletManager == null)
        {
            tabletManager =
                UnityEngine.Object.FindAnyObjectByType<TabletUIManager>(
                    FindObjectsInactive.Include);
        }

        if (boardCamera == null)
        {
            boardCamera =
                UnityEngine.Object.FindAnyObjectByType<BoardCameraController>(
                    FindObjectsInactive.Include);
        }

        if (cityInformation == null)
        {
            cityInformation =
                UnityEngine.Object.FindAnyObjectByType<
                    AtlasBoardCityInformationPanel>(
                        FindObjectsInactive.Include);
        }

        if (tradePresentation == null)
        {
            tradePresentation =
                UnityEngine.Object.FindAnyObjectByType<
                    AtlasBoardTradePresentationPolish>(
                        FindObjectsInactive.Include);
        }
    }

    private void BuildActionCanvas()
    {
        GameObject canvasObject =
            new GameObject(
                "AtlasBoard_Mobile_Gameplay_Actions",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(transform, false);

        actionCanvas = canvasObject.GetComponent<Canvas>();
        actionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        actionCanvas.sortingOrder = 920;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        GameObject safeObject =
            new GameObject("SafeArea", typeof(RectTransform));
        safeObject.transform.SetParent(canvasObject.transform, false);
        safeRoot = safeObject.GetComponent<RectTransform>();
        safeRoot.offsetMin = Vector2.zero;
        safeRoot.offsetMax = Vector2.zero;

        rollButton = CreateActionButton(
            safeRoot,
            "Button_MobileRollDice",
            new Color32(26, 143, 196, 245),
            true,
            out rollLabel);

        tradeButton = CreateActionButton(
            safeRoot,
            "Button_MobileTrade",
            new Color32(58, 128, 41, 245),
            false,
            out tradeLabel);

        rollButton.onClick.AddListener(HandleRollPressed);
        tradeButton.onClick.AddListener(HandleTradePressed);

        RefreshLabels();
        UpdateSafeArea(force: true);
    }

    private static Button CreateActionButton(
        RectTransform parent,
        string name,
        Color color,
        bool leftSide,
        out TMP_Text label)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(Outline));

        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = leftSide
            ? new Vector2(0f, 0f)
            : new Vector2(1f, 0f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = leftSide
            ? new Vector2(0f, 0f)
            : new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(230f, 76f);

        // P1 Roll placement was accepted. Move Trade substantially farther
        // left so it cannot overlap the lower-right P2 HUD on 20:9 phones.
        rect.anchoredPosition =
            leftSide
                ? new Vector2(300f, 28f)
                : new Vector2(-610f, 28f);

        Image image = root.GetComponent<Image>();
        image.color = color;

        Outline outline = root.GetComponent<Outline>();
        outline.effectColor = new Color(0.02f, 0.08f, 0.12f, 0.80f);
        outline.effectDistance = new Vector2(2f, -2f);

        Button button = root.GetComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
        colors.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.65f);
        button.colors = colors;

        GameObject labelObject =
            new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 6f);
        labelRect.offsetMax = new Vector2(-10f, -6f);

        label = labelObject.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.fontSize = 28f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 20f;
        label.fontSizeMax = 30f;
        label.color = Color.white;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        return button;
    }

    private void RefreshLabels()
    {
        if (rollLabel != null)
        {
            rollLabel.text = AtlasBoardL.R(
                "ROLL DICE",
                "ZAR AT",
                "LANZAR DADOS",
                "LANCER LES DÉS",
                "WÜRFELN",
                "주사위 굴리기",
                "БРОСИТЬ КОСТИ");
        }

        if (tradeLabel != null)
        {
            tradeLabel.text = AtlasBoardL.R(
                "TRADE",
                "TAKAS YAP",
                "INTERCAMBIAR",
                "ÉCHANGER",
                "HANDELN",
                "거래",
                "ОБМЕН");
        }
    }

    private void UpdateSafeArea(bool force = false)
    {
        if (safeRoot == null)
            return;

        Rect safe = Screen.safeArea;

        if (!force &&
            safe == lastSafeArea &&
            lastScreenWidth == Screen.width &&
            lastScreenHeight == Screen.height)
        {
            return;
        }

        lastSafeArea = safe;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        float width = Mathf.Max(1f, Screen.width);
        float height = Mathf.Max(1f, Screen.height);

        safeRoot.anchorMin = new Vector2(
            safe.xMin / width,
            safe.yMin / height);
        safeRoot.anchorMax = new Vector2(
            safe.xMax / width,
            safe.yMax / height);
        safeRoot.offsetMin = Vector2.zero;
        safeRoot.offsetMax = Vector2.zero;
    }

    private void UpdateActionButtons()
    {
        bool matchVisible =
            turnManager != null &&
            turnManager.IsMatchStarted &&
            !turnManager.IsMatchFinished &&
            !IsTabletVisible();

        if (actionCanvas != null &&
            actionCanvas.gameObject.activeSelf != matchVisible)
        {
            actionCanvas.gameObject.SetActive(matchVisible);
        }

        if (!matchVisible)
            return;

        PlayerGameState rollPlayer =
            turnManager.IsStartingOrderPhase
                ? turnManager.StartingOrderPlayerState
                : turnManager.CurrentPlayerState;

        if (rollButton != null)
        {
            rollButton.interactable =
                rollPlayer != null &&
                turnManager.CanPlayerRequestRoll(rollPlayer);
        }

        if (tradeButton != null)
        {
            tradeButton.interactable =
                tradeManager != null &&
                turnManager.CanStartManagementAction;
        }
    }

    private void HandleRollPressed()
    {
        if (turnManager == null)
            return;

        Debug.Log("ATB_MOBILE_ACTION action=roll_dice");
        turnManager.HandleRollButton();
    }

    private void HandleTradePressed()
    {
        if (tradeManager == null ||
            turnManager == null ||
            !turnManager.CanStartManagementAction)
        {
            return;
        }

        Debug.Log("ATB_MOBILE_ACTION action=trade");
        tradeManager.OpenTradePanel();
    }

    private bool IsTabletVisible()
    {
        if (tabletManager == null)
            return false;

        GameObject tabletRoot =
            ReadPrivateField<GameObject>(
                tabletManager,
                "tabletRoot");

        return tabletRoot != null &&
               tabletRoot.activeInHierarchy;
    }

    private void HandleTouchCamera()
    {
#if ENABLE_INPUT_SYSTEM
        if (boardCamera == null ||
            turnManager == null ||
            !turnManager.IsMatchStarted ||
            IsTabletVisible() ||
            (cityInformation != null && cityInformation.IsOpen) ||
            Touchscreen.current == null)
        {
            return;
        }

        TouchControl first = null;
        TouchControl second = null;

        foreach (TouchControl touch in Touchscreen.current.touches)
        {
            if (!touch.press.isPressed)
                continue;

            if (first == null)
                first = touch;
            else
            {
                second = touch;
                break;
            }
        }

        if (first == null ||
            IsTouchOverUi(first))
        {
            return;
        }

        if (second == null)
        {
            Vector2 delta = first.delta.ReadValue();
            if (delta.sqrMagnitude < 0.5f)
                return;

            float yaw =
                ReadPrivateField<float>(
                    boardCamera,
                    "targetYaw");
            float sensitivity =
                ReadPrivateField<float>(
                    boardCamera,
                    "rotationSensitivity");
            float user =
                ReadPrivateField<float>(
                    boardCamera,
                    "userRotationMultiplier");

            SetPrivateField(
                boardCamera,
                "targetYaw",
                yaw + delta.x * sensitivity * user);

            LogTouchCameraOnce("one_finger_rotate");
            return;
        }

        if (IsTouchOverUi(second))
            return;

        Vector2 firstPosition = first.position.ReadValue();
        Vector2 secondPosition = second.position.ReadValue();
        Vector2 firstDelta = first.delta.ReadValue();
        Vector2 secondDelta = second.delta.ReadValue();

        Vector2 previousFirst = firstPosition - firstDelta;
        Vector2 previousSecond = secondPosition - secondDelta;

        float currentSpan =
            Vector2.Distance(firstPosition, secondPosition);
        float previousSpan =
            Vector2.Distance(previousFirst, previousSecond);
        float pinchDelta = currentSpan - previousSpan;

        float targetDistance =
            ReadPrivateField<float>(
                boardCamera,
                "targetDistance");
        float minimumDistance =
            ReadPrivateField<float>(
                boardCamera,
                "minimumDistance");
        float maximumDistance =
            ReadPrivateField<float>(
                boardCamera,
                "maximumDistance");
        float zoomUser =
            ReadPrivateField<float>(
                boardCamera,
                "userZoomMultiplier");

        targetDistance -=
            pinchDelta * 0.018f * zoomUser;
        targetDistance = Mathf.Clamp(
            targetDistance,
            minimumDistance,
            maximumDistance);

        SetPrivateField(
            boardCamera,
            "targetDistance",
            targetDistance);

        Vector2 averageDelta =
            (firstDelta + secondDelta) * 0.5f;

        if (averageDelta.sqrMagnitude > 0.25f)
        {
            Vector3 cameraRight = boardCamera.transform.right;
            cameraRight.y = 0f;
            if (cameraRight.sqrMagnitude > 0.0001f)
                cameraRight.Normalize();

            Vector3 cameraForward = boardCamera.transform.forward;
            cameraForward.y = 0f;
            if (cameraForward.sqrMagnitude > 0.0001f)
                cameraForward.Normalize();

            float panSensitivity =
                ReadPrivateField<float>(
                    boardCamera,
                    "panSensitivity");
            float panUser =
                ReadPrivateField<float>(
                    boardCamera,
                    "userPanMultiplier");
            float maximumPanDistance =
                ReadPrivateField<float>(
                    boardCamera,
                    "maximumPanDistance");

            Vector3 targetPan =
                ReadPrivateField<Vector3>(
                    boardCamera,
                    "targetPanOffset");

            targetPan +=
                (-cameraRight * averageDelta.x -
                 cameraForward * averageDelta.y) *
                panSensitivity * panUser;

            if (maximumPanDistance > 0f &&
                targetPan.magnitude > maximumPanDistance)
            {
                targetPan =
                    targetPan.normalized * maximumPanDistance;
            }

            SetPrivateField(
                boardCamera,
                "targetPanOffset",
                targetPan);
        }

        LogTouchCameraOnce("two_finger_pinch_pan");
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static bool IsTouchOverUi(TouchControl touch)
    {
        if (EventSystem.current == null ||
            touch == null)
        {
            return false;
        }

        return EventSystem.current.IsPointerOverGameObject(
            touch.touchId.ReadValue());
    }
#endif

    private void LogTouchCameraOnce(string gesture)
    {
        if (touchCameraLogged)
            return;

        touchCameraLogged = true;

        Debug.Log(
            "ATB_MOBILE_CAMERA_TOUCH enabled=true gesture=" + gesture +
            " controls=one_finger_rotate,two_finger_pinch_pan");
    }

    private void UpdateMobilePanels()
    {
        if (Time.unscaledTime < nextLayoutAt)
            return;

        nextLayoutAt = Time.unscaledTime + 0.35f;

        EnlargeTabletIfNeeded();
        EnlargeTradePresentationIfNeeded();
        EnlargeCityPanelIfNeeded();
    }

    private void EnlargeTabletIfNeeded()
    {
        if (tabletManager == null)
            return;

        GameObject root =
            ReadPrivateField<GameObject>(
                tabletManager,
                "tabletRoot");

        RectTransform rect =
            root != null
                ? root.transform as RectTransform
                : null;

        if (rect == null ||
            enlargedTabletRoots.Contains(rect))
        {
            return;
        }

        rect.localScale =
            new Vector3(
                rect.localScale.x * TabletScale,
                rect.localScale.y * TabletScale,
                rect.localScale.z);

        enlargedTabletRoots.Add(rect);

        TMP_Text[] texts =
            rect.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text == null)
                continue;

            if (text.enableAutoSizing)
            {
                text.fontSizeMin = Mathf.Max(text.fontSizeMin, 15f);
                text.fontSizeMax = Mathf.Max(text.fontSizeMax, 27f);
            }
        }

        Debug.Log(
            "ATB_MOBILE_TABLET_SCALE scale=" +
            TabletScale.ToString("F2") +
            " touch_targets=larger");
    }

    private void EnlargeTradePresentationIfNeeded()
    {
        if (tradePresentation == null)
            return;

        RectTransform root = tradePresentation.PolishRoot;
        if (root == null ||
            enlargedTradeRoots.Contains(root))
        {
            return;
        }

        TMP_Text[] texts =
            root.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text == null)
                continue;

            if (text.enableAutoSizing)
            {
                text.fontSizeMin = Mathf.Max(text.fontSizeMin, 17f);
                text.fontSizeMax = Mathf.Max(text.fontSizeMax, 32f);
            }
            else if (text.fontSize < 19f)
            {
                text.fontSize = 19f;
            }
        }

        TMP_Dropdown[] dropdowns =
            root.GetComponentsInChildren<TMP_Dropdown>(true);
        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            if (dropdown == null)
                continue;

            RectTransform rect =
                dropdown.transform as RectTransform;
            IncreaseFixedHeight(rect, 66f);
        }

        TMP_InputField[] inputs =
            root.GetComponentsInChildren<TMP_InputField>(true);
        foreach (TMP_InputField input in inputs)
        {
            if (input == null)
                continue;

            RectTransform rect =
                input.transform as RectTransform;
            IncreaseFixedHeight(rect, 66f);
        }

        Button[] buttons =
            root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            RectTransform rect =
                button.transform as RectTransform;
            IncreaseFixedHeight(rect, 72f);
        }

        enlargedTradeRoots.Add(root);

        Debug.Log(
            "ATB_MOBILE_TRADE_TOUCH_UI fontMin=17 controlsMinHeight=66");
    }

    private static void IncreaseFixedHeight(
        RectTransform rect,
        float minimum)
    {
        if (rect == null)
            return;

        // Stretch-anchored controls use offsets and should not have their
        // geometry rewritten here. Fixed-anchor controls can safely grow.
        if (!Mathf.Approximately(
                rect.anchorMin.y,
                rect.anchorMax.y))
        {
            return;
        }

        Vector2 size = rect.sizeDelta;
        if (size.y >= minimum)
            return;

        size.y = minimum;
        rect.sizeDelta = size;
    }

    private void EnlargeCityPanelIfNeeded()
    {
        if (cityInformation == null)
            return;

        RectTransform panel =
            ReadPrivateField<RectTransform>(
                cityInformation,
                "panelRect");

        if (panel == null ||
            enlargedCityPanels.Contains(panel))
        {
            return;
        }

        panel.sizeDelta = new Vector2(600f, 720f);
        panel.anchoredPosition = new Vector2(-30f, 0f);

        TMP_Text body =
            ReadPrivateField<TMP_Text>(
                cityInformation,
                "bodyText");

        if (body != null)
        {
            body.fontSize = Mathf.Max(body.fontSize, 19f);
            body.lineSpacing = Mathf.Max(body.lineSpacing, 10f);
        }

        Button close =
            ReadPrivateField<Button>(
                cityInformation,
                "closeButton");

        if (close != null &&
            close.transform is RectTransform closeRect)
        {
            closeRect.sizeDelta = new Vector2(58f, 58f);
        }

        ScrollRect scroll =
            panel.GetComponentInChildren<ScrollRect>(true);

        if (scroll != null)
        {
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 68f;
        }

        enlargedCityPanels.Add(panel);

        Debug.Log(
            "ATB_MOBILE_CITY_INFO size=600x720 touch_scroll=true");
    }

    private void ApplyCareerMobileAccent()
    {
        GameObject card = FindSceneObject("ProgressionCard");
        if (card == null ||
            recoloredCareerCards.Contains(card))
        {
            return;
        }

        Color indigo = new Color32(92, 82, 214, 255);

        Transform ribbon =
            FindChildRecursive(card.transform, "Ribbon");
        Transform plate =
            FindChildRecursive(card.transform, "IllustrationPlate");
        Transform glyph =
            FindChildRecursive(card.transform, "IllustrationGlyph");

        if (ribbon != null &&
            ribbon.TryGetComponent(out Image ribbonImage))
        {
            ribbonImage.color = indigo;
        }

        if (plate != null &&
            plate.TryGetComponent(out Image plateImage))
        {
            plateImage.color =
                new Color(
                    indigo.r,
                    indigo.g,
                    indigo.b,
                    0.18f);
        }

        if (glyph != null &&
            glyph.TryGetComponent(out TMP_Text glyphText))
        {
            glyphText.color = indigo;
        }

        recoloredCareerCards.Add(card);

        Debug.Log(
            "ATB_MOBILE_MENU_ACCENT card=career accent=indigo shop=orange");
    }

    private void ApplyMobilePerformanceProfile(
        bool emergency,
        string reason)
    {
        int requestedTarget =
            AtlasBoardUserSettingsRuntime.Current.Graphics.FpsLimit;

        int target =
            requestedTarget <= 0
                ? 60
                : Mathf.Clamp(requestedTarget, 30, 60);

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = target;

        QualitySettings.pixelLightCount =
            Mathf.Min(QualitySettings.pixelLightCount, 1);

        QualitySettings.antiAliasing =
            emergency
                ? 0
                : Mathf.Min(QualitySettings.antiAliasing, 2);

        QualitySettings.shadows = ShadowQuality.Disable;

        QualitySettings.shadowResolution =
            ShadowResolution.Low;

        QualitySettings.shadowDistance = 0f;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.softParticles = false;
        QualitySettings.lodBias = Mathf.Min(QualitySettings.lodBias, 0.80f);

        ApplyRenderScale(
            emergency
                ? EmergencyRenderScale
                : InitialRenderScale);

        Camera[] cameras =
            UnityEngine.Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include);

        foreach (Camera camera in cameras)
        {
            if (camera == null ||
                !camera.gameObject.scene.IsValid())
            {
                continue;
            }

            camera.allowHDR = false;
            camera.allowMSAA =
                QualitySettings.antiAliasing > 0;

            DisableUrpPostProcessing(camera);
        }

        if (!string.Equals(
                reason,
                "periodic_guard",
                StringComparison.Ordinal))
        {
            Debug.Log(
                "ATB_MOBILE_RENDER_PROFILE emergency=" + emergency +
                " reason=" + reason +
                " targetFps=" + target +
                " vsync=" + QualitySettings.vSyncCount +
                " aa=" + QualitySettings.antiAliasing +
                " shadows=" + QualitySettings.shadows +
                " shadowDistance=" +
                QualitySettings.shadowDistance.ToString("F1") +
                " renderScale=" + GetRenderScale().ToString("F2") +
                " hdr=false postProcess=false");
        }
    }

    private static void DisableUrpPostProcessing(
        Camera camera)
    {
        if (camera == null)
            return;

        Component[] components =
            camera.GetComponents<Component>();

        foreach (Component component in components)
        {
            if (component == null)
                continue;

            Type type = component.GetType();
            string fullName = type.FullName ?? string.Empty;

            if (fullName.IndexOf(
                    "UniversalAdditionalCameraData",
                    StringComparison.Ordinal) < 0)
            {
                continue;
            }

            PropertyInfo property =
                type.GetProperty(
                    "renderPostProcessing",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            if (property != null &&
                property.CanWrite &&
                property.PropertyType == typeof(bool))
            {
                try
                {
                    property.SetValue(component, false);
                }
                catch
                {
                    // A render-pipeline package API mismatch is non-fatal.
                }
            }
        }
    }

    private static void ApplyRenderScale(float value)
    {
        UnityEngine.Rendering.RenderPipelineAsset pipeline =
            UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;

        if (pipeline == null)
            return;

        PropertyInfo property =
            pipeline.GetType().GetProperty(
                "renderScale",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        if (property == null ||
            !property.CanWrite ||
            property.PropertyType != typeof(float))
        {
            return;
        }

        try
        {
            property.SetValue(
                pipeline,
                Mathf.Clamp(value, 0.60f, 1f));
        }
        catch
        {
            // Keep gameplay functional even if the active pipeline does not
            // expose a writable renderScale in a future Unity version.
        }
    }

    private static float GetRenderScale()
    {
        UnityEngine.Rendering.RenderPipelineAsset pipeline =
            UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;

        if (pipeline == null)
            return 1f;

        PropertyInfo property =
            pipeline.GetType().GetProperty(
                "renderScale",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        if (property == null ||
            !property.CanRead ||
            property.PropertyType != typeof(float))
        {
            return 1f;
        }

        try
        {
            object value = property.GetValue(pipeline);
            return value is float number
                ? number
                : 1f;
        }
        catch
        {
            return 1f;
        }
    }

    private void UpdatePerformanceTelemetry()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f)
            return;

        perfFrames++;
        perfAccumulatedDelta += dt;
        perfWorstFrameMs =
            Mathf.Max(
                perfWorstFrameMs,
                dt * 1000f);

        float elapsed =
            Time.unscaledTime - perfWindowStartedAt;

        if (elapsed < 10f)
            return;

        float averageFps =
            perfAccumulatedDelta > 0f
                ? perfFrames / perfAccumulatedDelta
                : 0f;

        long memoryMb =
            Profiler.GetTotalAllocatedMemoryLong() /
            (1024L * 1024L);

        Debug.Log(
            "ATB_MOBILE_PERF avgFps=" + averageFps.ToString("F1") +
            " worstFrameMs=" + perfWorstFrameMs.ToString("F1") +
            " targetFps=" + Application.targetFrameRate +
            " vsync=" + QualitySettings.vSyncCount +
            " aa=" + QualitySettings.antiAliasing +
            " shadows=" + QualitySettings.shadows +
            " renderScale=" + GetRenderScale().ToString("F2") +
            " pixelLights=" + QualitySettings.pixelLightCount +
            " memoryMB=" + memoryMb +
            " resolution=" + Screen.width + "x" + Screen.height +
            " devBuild=" + Debug.isDebugBuild);

        if (averageFps < 45f)
            consecutiveLowFpsWindows++;
        else
            consecutiveLowFpsWindows = 0;

        if (!emergencyProfileApplied &&
            consecutiveLowFpsWindows >= 2)
        {
            emergencyProfileApplied = true;
            consecutiveLowFpsWindows = 0;

            ApplyMobilePerformanceProfile(
                emergency: true,
                reason: "sustained_low_fps");

            Debug.LogWarning(
                "ATB_MOBILE_PERF_ADAPT action=emergency_mobile_profile " +
                "renderScale=" + GetRenderScale().ToString("F2") +
                " aa=" + QualitySettings.antiAliasing +
                " shadows=" + QualitySettings.shadows +
                " reason=sustained_low_fps");
        }

        ResetPerformanceWindow();
    }

    private void ResetPerformanceWindow()
    {
        perfWindowStartedAt = Time.unscaledTime;
        perfFrames = 0;
        perfAccumulatedDelta = 0f;
        perfWorstFrameMs = 0f;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        GameObject[] objects =
            Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject item in objects)
        {
            if (item != null &&
                item.scene.IsValid() &&
                string.Equals(
                    item.name,
                    objectName,
                    StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(
        Transform root,
        string name)
    {
        if (root == null)
            return null;

        for (int index = 0;
             index < root.childCount;
             index++)
        {
            Transform child = root.GetChild(index);

            if (string.Equals(
                    child.name,
                    name,
                    StringComparison.Ordinal))
            {
                return child;
            }

            Transform nested =
                FindChildRecursive(
                    child,
                    name);

            if (nested != null)
                return nested;
        }

        return null;
    }

    private static T ReadPrivateField<T>(
        object owner,
        string fieldName)
    {
        if (owner == null)
            return default;

        FieldInfo field = owner.GetType().GetField(
            fieldName,
            BindingFlags.Instance |
            BindingFlags.NonPublic);

        if (field == null)
            return default;

        object value = field.GetValue(owner);
        return value is T typed
            ? typed
            : default;
    }

    private static void SetPrivateField<T>(
        object owner,
        string fieldName,
        T value)
    {
        if (owner == null)
            return;

        FieldInfo field = owner.GetType().GetField(
            fieldName,
            BindingFlags.Instance |
            BindingFlags.NonPublic);

        if (field != null)
            field.SetValue(owner, value);
    }
#endif
}
