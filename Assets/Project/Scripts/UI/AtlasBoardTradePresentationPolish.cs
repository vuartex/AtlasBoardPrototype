using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(13250)]
[DisallowMultipleComponent]
public sealed class AtlasBoardTradePresentationPolish : MonoBehaviour
{
    private const float RefreshIntervalSeconds = 0.10f;

    private TradeManager tradeManager;
    private TabletUIManager tabletManager;

    private GameObject tradePanel;
    private RectTransform tabletRoot;
    private RectTransform polishRoot;

    private TMP_Text legacyTitle;
    private TMP_Text legacySummary;

    private GameObject legacyTargetLabel;
    private GameObject legacyOfferHeader;
    private GameObject legacyRequestHeader;

    private bool legacyTargetWasActive;
    private bool legacyOfferHeaderWasActive;
    private bool legacyRequestHeaderWasActive;
    private bool legacyStaticCaptured;

    private TMP_Dropdown targetDropdown;
    private TMP_Dropdown offeredPropertyDropdown;
    private TMP_InputField offeredCashInput;
    private TMP_Dropdown requestedPropertyDropdown;
    private TMP_InputField requestedCashInput;

    private Button sendButton;
    private Button cancelButton;
    private Button acceptButton;
    private Button rejectButton;

    private TMP_Text playerWithLabel;
    private TMP_Text fromNameText;
    private TMP_Text toNameText;
    private Image fromAccent;
    private Image toAccent;

    private TMP_Text offerHeaderText;
    private TMP_Text offerPropertyLabelText;
    private TMP_Text offerCashLabelText;
    private TMP_Text offerStaticPropertyText;
    private TMP_Text offerStaticCashText;

    private TMP_Text requestHeaderText;
    private TMP_Text requestPropertyLabelText;
    private TMP_Text requestCashLabelText;
    private TMP_Text requestStaticPropertyText;
    private TMP_Text requestStaticCashText;

    private TMP_Text setupHintText;

    private TMP_Text resultTitleText;
    private GameObject resultCard;
    private TMP_Text resultBodyText;

    private bool legacyTextCaptured;
    private bool legacyTitleWasActive;
    private bool legacySummaryWasActive;

    private float nextRefreshAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (UnityEngine.Object.FindAnyObjectByType<BoardPath>() == null)
        {
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType<AtlasBoardTradePresentationPolish>() != null)
        {
            return;
        }

        new GameObject("AtlasBoard_Phase14H_TradePresentation")
            .AddComponent<AtlasBoardTradePresentationPolish>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        AtlasBoardLocalizationManager.LanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        AtlasBoardLocalizationManager.LanguageChanged -= HandleLanguageChanged;
        RestoreLegacyText();
        RestoreLegacyStaticLabels();
        HidePolish();
    }

    private void OnDestroy()
    {
        RestoreLegacyText();
        RestoreLegacyStaticLabels();
    }

    private void HandleLanguageChanged()
    {
        nextRefreshAt = 0f;
    }

    private void LateUpdate()
    {
        ResolveReferences();

        bool visible =
            tradePanel != null &&
            tradePanel.activeSelf &&
            tabletRoot != null &&
            tabletRoot.gameObject.activeInHierarchy;

        if (!visible)
        {
            RestoreLegacyText();
            HidePolish();
            return;
        }

        EnsurePresentation();

        HideLegacyText();
        HideLegacyStaticLabels();

        if (polishRoot != null)
        {
            polishRoot.SetAsLastSibling();

            if (!polishRoot.gameObject.activeSelf)
            {
                polishRoot.gameObject.SetActive(true);
            }
        }

        string state = GetPanelStateName();
        ApplyStateVisibility(state);

        if (Time.unscaledTime >= nextRefreshAt)
        {
            nextRefreshAt =
                Time.unscaledTime + RefreshIntervalSeconds;

            RefreshPresentation(state);
        }
    }

    public void RefreshNow()
    {
        ResolveReferences();
        EnsurePresentation();

        HideLegacyText();
        HideLegacyStaticLabels();

        string state = GetPanelStateName();
        ApplyStateVisibility(state);
        RefreshPresentation(state);
    }

    public RectTransform TabletRoot => tabletRoot;
    public RectTransform PolishRoot => polishRoot;

    public RectTransform LeftTradeCard =>
        polishRoot != null
            ? polishRoot.Find("OfferCard") as RectTransform
            : null;

    public RectTransform RightTradeCard =>
        polishRoot != null
            ? polishRoot.Find("RequestCard") as RectTransform
            : null;

    private void ResolveReferences()
    {
        if (tradeManager == null)
        {
            tradeManager =
                UnityEngine.Object.FindAnyObjectByType<TradeManager>();
        }

        if (tabletManager == null)
        {
            tabletManager =
                UnityEngine.Object.FindAnyObjectByType<TabletUIManager>();
        }

        if (tradeManager != null)
        {
            tradePanel =
                ReadPrivateField<GameObject>(
                    tradeManager,
                    "tradePanel");

            legacyTitle =
                ReadPrivateField<TMP_Text>(
                    tradeManager,
                    "tradeTitleText");

            legacySummary =
                ReadPrivateField<TMP_Text>(
                    tradeManager,
                    "tradeSummaryText");

            targetDropdown =
                ReadPrivateField<TMP_Dropdown>(
                    tradeManager,
                    "targetPlayerDropdown");

            offeredPropertyDropdown =
                ReadPrivateField<TMP_Dropdown>(
                    tradeManager,
                    "offeredPropertyDropdown");

            offeredCashInput =
                ReadPrivateField<TMP_InputField>(
                    tradeManager,
                    "offeredCashInput");

            requestedPropertyDropdown =
                ReadPrivateField<TMP_Dropdown>(
                    tradeManager,
                    "requestedPropertyDropdown");

            requestedCashInput =
                ReadPrivateField<TMP_InputField>(
                    tradeManager,
                    "requestedCashInput");

            sendButton =
                ReadPrivateField<Button>(
                    tradeManager,
                    "sendOfferButton");

            cancelButton =
                ReadPrivateField<Button>(
                    tradeManager,
                    "cancelTradeButton");

            acceptButton =
                ReadPrivateField<Button>(
                    tradeManager,
                    "acceptTradeButton");

            rejectButton =
                ReadPrivateField<Button>(
                    tradeManager,
                    "rejectTradeButton");

            ResolveLegacyStaticLabels();
        }

        if (tabletManager != null)
        {
            GameObject root =
                ReadPrivateField<GameObject>(
                    tabletManager,
                    "tabletRoot");

            tabletRoot =
                root != null
                    ? root.transform as RectTransform
                    : null;
        }
    }

    private void ResolveLegacyStaticLabels()
    {
        if (tradePanel == null)
        {
            return;
        }

        if (legacyTargetLabel == null)
        {
            Transform found =
                FindChildRecursive(
                    tradePanel.transform,
                    "Text_TradeTargetLabel");

            legacyTargetLabel =
                found != null
                    ? found.gameObject
                    : null;
        }

        if (legacyOfferHeader == null)
        {
            Transform found =
                FindChildRecursive(
                    tradePanel.transform,
                    "Text_TradeOfferHeader");

            legacyOfferHeader =
                found != null
                    ? found.gameObject
                    : null;
        }

        if (legacyRequestHeader == null)
        {
            Transform found =
                FindChildRecursive(
                    tradePanel.transform,
                    "Text_TradeRequestHeader");

            legacyRequestHeader =
                found != null
                    ? found.gameObject
                    : null;
        }
    }

    private void EnsurePresentation()
    {
        if (tabletRoot == null)
        {
            return;
        }

        if (polishRoot == null)
        {
            Transform existing =
                tabletRoot.Find(
                    "Phase14H_TradePolish_Final");

            if (existing != null)
            {
                polishRoot =
                    existing as RectTransform;

                ResolveExistingPresentation();
            }
            else
            {
                BuildPresentation();
            }
        }

        ReparentAuthoritativeControls();
    }

    private void BuildPresentation()
    {
        GameObject root =
            new GameObject(
                "Phase14H_TradePolish_Final",
                typeof(RectTransform),
                typeof(LayoutElement));

        root.transform.SetParent(
            tabletRoot,
            false);

        polishRoot =
            root.GetComponent<RectTransform>();

        root.GetComponent<LayoutElement>()
            .ignoreLayout = true;

        Stretch(
            polishRoot);

        CreatePlayerPicker();
        CreatePlayerStrip();
        CreateTradeCards();
        CreateSetupHint();
        CreateResultPresentation();

        polishRoot.gameObject.SetActive(
            false);
    }

    private void CreatePlayerPicker()
    {
        playerWithLabel =
            CreateText(
                "PlayerWithLabel",
                polishRoot,
                9f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

        Box(
            playerWithLabel.rectTransform,
            0.06f,
            0.775f,
            0.28f,
            0.805f);

        Auto(
            playerWithLabel,
            7f,
            10f,
            TextWrappingModes.NoWrap);

        playerWithLabel.color =
            new Color(
                0.72f,
                0.79f,
                0.87f,
                1f);
    }

    private void CreatePlayerStrip()
    {
        GameObject fromCard =
            CreatePanel(
                "FromCard",
                polishRoot);

        Box(
            fromCard.GetComponent<RectTransform>(),
            0.06f,
            0.615f,
            0.45f,
            0.715f);

        fromAccent =
            AddAccent(
                fromCard.transform,
                true,
                new Color(
                    0.18f,
                    0.56f,
                    0.88f,
                    1f));

        TMP_Text fromLabel =
            CreateText(
                "FromLabel",
                fromCard.transform,
                8f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

        Box(
            fromLabel.rectTransform,
            0.05f,
            0.59f,
            0.95f,
            0.90f);

        Auto(
            fromLabel,
            6f,
            9f,
            TextWrappingModes.NoWrap);

        fromLabel.text =
            FromLabel();

        fromLabel.color =
            new Color(
                0.67f,
                0.74f,
                0.83f,
                1f);

        fromNameText =
            CreateText(
                "FromName",
                fromCard.transform,
                15f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

        Box(
            fromNameText.rectTransform,
            0.05f,
            0.13f,
            0.95f,
            0.62f);

        Auto(
            fromNameText,
            9f,
            16f,
            TextWrappingModes.NoWrap);

        TMP_Text arrow =
            CreateText(
                "TradeArrow",
                polishRoot,
                20f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            arrow.rectTransform,
            0.46f,
            0.625f,
            0.54f,
            0.705f);

        Auto(
            arrow,
            12f,
            20f,
            TextWrappingModes.NoWrap);

        arrow.text =
            "⇄";

        arrow.color =
            new Color(
                0.96f,
                0.72f,
                0.16f,
                1f);

        GameObject toCard =
            CreatePanel(
                "ToCard",
                polishRoot);

        Box(
            toCard.GetComponent<RectTransform>(),
            0.55f,
            0.615f,
            0.94f,
            0.715f);

        toAccent =
            AddAccent(
                toCard.transform,
                false,
                new Color(
                    0.78f,
                    0.39f,
                    0.76f,
                    1f));

        TMP_Text toLabel =
            CreateText(
                "ToLabel",
                toCard.transform,
                8f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineRight);

        Box(
            toLabel.rectTransform,
            0.05f,
            0.59f,
            0.95f,
            0.90f);

        Auto(
            toLabel,
            6f,
            9f,
            TextWrappingModes.NoWrap);

        toLabel.text =
            ToLabel();

        toLabel.color =
            new Color(
                0.67f,
                0.74f,
                0.83f,
                1f);

        toNameText =
            CreateText(
                "ToName",
                toCard.transform,
                15f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineRight);

        Box(
            toNameText.rectTransform,
            0.05f,
            0.13f,
            0.95f,
            0.62f);

        Auto(
            toNameText,
            9f,
            16f,
            TextWrappingModes.NoWrap);
    }

    private void CreateTradeCards()
    {
        GameObject offerCard =
            CreatePanel(
                "OfferCard",
                polishRoot);

        Box(
            offerCard.GetComponent<RectTransform>(),
            0.05f,
            0.285f,
            0.475f,
            0.565f);

        AddAccent(
            offerCard.transform,
            true,
            new Color(
                0.18f,
                0.56f,
                0.88f,
                1f));

        offerHeaderText =
            CreateText(
                "Header",
                offerCard.transform,
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            offerHeaderText.rectTransform,
            0.08f,
            0.83f,
            0.92f,
            0.96f);

        Auto(
            offerHeaderText,
            10f,
            16f,
            TextWrappingModes.NoWrap);

        offerPropertyLabelText =
            CreateText(
                "PropertyLabel",
                offerCard.transform,
                8f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

        Box(
            offerPropertyLabelText.rectTransform,
            0.10f,
            0.67f,
            0.90f,
            0.75f);

        Auto(
            offerPropertyLabelText,
            6f,
            8.5f,
            TextWrappingModes.NoWrap);

        offerPropertyLabelText.color =
            new Color(
                0.72f,
                0.79f,
                0.87f,
                1f);

        offerCashLabelText =
            CreateText(
                "CashLabel",
                offerCard.transform,
                8f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

        Box(
            offerCashLabelText.rectTransform,
            0.10f,
            0.33f,
            0.90f,
            0.41f);

        Auto(
            offerCashLabelText,
            6f,
            8.5f,
            TextWrappingModes.NoWrap);

        offerCashLabelText.color =
            new Color(
                0.72f,
                0.79f,
                0.87f,
                1f);

        offerStaticPropertyText =
            CreateText(
                "StaticProperty",
                offerCard.transform,
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            offerStaticPropertyText.rectTransform,
            0.10f,
            0.47f,
            0.90f,
            0.63f);

        Auto(
            offerStaticPropertyText,
            9f,
            15f,
            TextWrappingModes.Normal);

        offerStaticCashText =
            CreateText(
                "StaticCash",
                offerCard.transform,
                18f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            offerStaticCashText.rectTransform,
            0.10f,
            0.10f,
            0.90f,
            0.29f);

        Auto(
            offerStaticCashText,
            10f,
            20f,
            TextWrappingModes.NoWrap);

        offerStaticCashText.color =
            new Color(
                0.96f,
                0.82f,
                0.40f,
                1f);

        GameObject requestCard =
            CreatePanel(
                "RequestCard",
                polishRoot);

        Box(
            requestCard.GetComponent<RectTransform>(),
            0.525f,
            0.285f,
            0.95f,
            0.565f);

        AddAccent(
            requestCard.transform,
            false,
            new Color(
                0.78f,
                0.39f,
                0.76f,
                1f));

        requestHeaderText =
            CreateText(
                "Header",
                requestCard.transform,
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            requestHeaderText.rectTransform,
            0.08f,
            0.83f,
            0.92f,
            0.96f);

        Auto(
            requestHeaderText,
            10f,
            16f,
            TextWrappingModes.NoWrap);

        requestPropertyLabelText =
            CreateText(
                "PropertyLabel",
                requestCard.transform,
                8f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

        Box(
            requestPropertyLabelText.rectTransform,
            0.10f,
            0.67f,
            0.90f,
            0.75f);

        Auto(
            requestPropertyLabelText,
            6f,
            8.5f,
            TextWrappingModes.NoWrap);

        requestPropertyLabelText.color =
            new Color(
                0.72f,
                0.79f,
                0.87f,
                1f);

        requestCashLabelText =
            CreateText(
                "CashLabel",
                requestCard.transform,
                8f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

        Box(
            requestCashLabelText.rectTransform,
            0.10f,
            0.33f,
            0.90f,
            0.41f);

        Auto(
            requestCashLabelText,
            6f,
            8.5f,
            TextWrappingModes.NoWrap);

        requestCashLabelText.color =
            new Color(
                0.72f,
                0.79f,
                0.87f,
                1f);

        requestStaticPropertyText =
            CreateText(
                "StaticProperty",
                requestCard.transform,
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            requestStaticPropertyText.rectTransform,
            0.10f,
            0.47f,
            0.90f,
            0.63f);

        Auto(
            requestStaticPropertyText,
            9f,
            15f,
            TextWrappingModes.Normal);

        requestStaticCashText =
            CreateText(
                "StaticCash",
                requestCard.transform,
                18f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            requestStaticCashText.rectTransform,
            0.10f,
            0.10f,
            0.90f,
            0.29f);

        Auto(
            requestStaticCashText,
            10f,
            20f,
            TextWrappingModes.NoWrap);

        requestStaticCashText.color =
            new Color(
                0.96f,
                0.82f,
                0.40f,
                1f);
    }

    private void CreateSetupHint()
    {
        setupHintText =
            CreateText(
                "SetupHint",
                polishRoot,
                9f,
                FontStyles.Normal,
                TextAlignmentOptions.Center);

        Box(
            setupHintText.rectTransform,
            0.12f,
            0.215f,
            0.88f,
            0.255f);

        Auto(
            setupHintText,
            6.5f,
            10f,
            TextWrappingModes.NoWrap);

        setupHintText.color =
            new Color(
                0.72f,
                0.79f,
                0.87f,
                1f);
    }

    private void CreateResultPresentation()
    {
        resultTitleText =
            CreateText(
                "ResultTitle",
                polishRoot,
                27f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            resultTitleText.rectTransform,
            0.18f,
            0.765f,
            0.82f,
            0.855f);

        Auto(
            resultTitleText,
            17f,
            30f,
            TextWrappingModes.NoWrap);

        resultTitleText.color =
            new Color(
                0.96f,
                0.82f,
                0.40f,
                1f);

        resultCard =
            CreatePanel(
                "TradeResultCard",
                polishRoot);

        Box(
            resultCard.GetComponent<RectTransform>(),
            0.14f,
            0.245f,
            0.86f,
            0.555f);

        resultBodyText =
            CreateText(
                "ResultBody",
                resultCard.transform,
                20f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            resultBodyText.rectTransform,
            0.08f,
            0.10f,
            0.92f,
            0.90f);

        Auto(
            resultBodyText,
            12f,
            22f,
            TextWrappingModes.Normal);

        resultBodyText.color =
            new Color(
                1f,
                0.88f,
                0.48f,
                1f);

        resultTitleText.gameObject.SetActive(
            false);

        resultCard.SetActive(
            false);
    }

    private void ReparentAuthoritativeControls()
    {
        if (polishRoot == null)
        {
            return;
        }

        RectTransform offerCard =
            LeftTradeCard;

        RectTransform requestCard =
            RightTradeCard;

        Reparent(
            targetDropdown,
            polishRoot);

        Reparent(
            offeredPropertyDropdown,
            offerCard);

        Reparent(
            offeredCashInput,
            offerCard);

        Reparent(
            requestedPropertyDropdown,
            requestCard);

        Reparent(
            requestedCashInput,
            requestCard);

        Reparent(
            sendButton,
            polishRoot);

        Reparent(
            cancelButton,
            polishRoot);

        Reparent(
            acceptButton,
            polishRoot);

        Reparent(
            rejectButton,
            polishRoot);

        Box(
            targetDropdown != null
                ? targetDropdown.transform as RectTransform
                : null,
            0.06f,
            0.720f,
            0.30f,
            0.770f);

        Box(
            offeredPropertyDropdown != null
                ? offeredPropertyDropdown.transform as RectTransform
                : null,
            0.10f,
            0.47f,
            0.90f,
            0.63f);

        Box(
            offeredCashInput != null
                ? offeredCashInput.transform as RectTransform
                : null,
            0.10f,
            0.10f,
            0.90f,
            0.29f);

        Box(
            requestedPropertyDropdown != null
                ? requestedPropertyDropdown.transform as RectTransform
                : null,
            0.10f,
            0.47f,
            0.90f,
            0.63f);

        Box(
            requestedCashInput != null
                ? requestedCashInput.transform as RectTransform
                : null,
            0.10f,
            0.10f,
            0.90f,
            0.29f);

        StyleDropdown(
            targetDropdown);

        StyleDropdown(
            offeredPropertyDropdown);

        StyleDropdown(
            requestedPropertyDropdown);

        StyleInput(
            offeredCashInput,
            CashAmountLabel());

        StyleInput(
            requestedCashInput,
            CashAmountLabel());

        StyleButton(
            sendButton,
            new Color(
                0.15f,
                0.64f,
                0.45f,
                1f));

        StyleButton(
            cancelButton,
            new Color(
                0.46f,
                0.49f,
                0.57f,
                1f));

        StyleButton(
            acceptButton,
            new Color(
                0.15f,
                0.64f,
                0.45f,
                1f));

        StyleButton(
            rejectButton,
            new Color(
                0.73f,
                0.25f,
                0.29f,
                1f));

        Box(
            sendButton != null
                ? sendButton.transform as RectTransform
                : null,
            0.17f,
            0.075f,
            0.47f,
            0.18f);

        Box(
            cancelButton != null
                ? cancelButton.transform as RectTransform
                : null,
            0.53f,
            0.075f,
            0.83f,
            0.18f);

        Box(
            acceptButton != null
                ? acceptButton.transform as RectTransform
                : null,
            0.17f,
            0.075f,
            0.47f,
            0.18f);

        Box(
            rejectButton != null
                ? rejectButton.transform as RectTransform
                : null,
            0.53f,
            0.075f,
            0.83f,
            0.18f);
    }

    private void ApplyStateVisibility(
        string state)
    {
        bool building =
            string.Equals(
                state,
                "BuildingOffer",
                StringComparison.Ordinal);

        bool awaiting =
            string.Equals(
                state,
                "AwaitingResponse",
                StringComparison.Ordinal);

        bool showingResult =
            string.Equals(
                state,
                "ShowingResult",
                StringComparison.Ordinal);

        Active(
            playerWithLabel,
            building);

        Active(
            targetDropdown,
            building);

        Active(
            offeredPropertyDropdown,
            building);

        Active(
            offeredCashInput,
            building);

        Active(
            requestedPropertyDropdown,
            building);

        Active(
            requestedCashInput,
            building);

        Active(
            offerStaticPropertyText,
            awaiting);

        Active(
            offerStaticCashText,
            awaiting);

        Active(
            requestStaticPropertyText,
            awaiting);

        Active(
            requestStaticCashText,
            awaiting);

        Active(
            setupHintText,
            building);

        RectTransform leftCard =
            LeftTradeCard;

        RectTransform rightCard =
            RightTradeCard;

        if (leftCard != null)
        {
            leftCard.gameObject.SetActive(
                !showingResult);
        }

        if (rightCard != null)
        {
            rightCard.gameObject.SetActive(
                !showingResult);
        }

        Active(
            resultTitleText,
            showingResult);

        if (resultCard != null)
        {
            resultCard.SetActive(
                showingResult);
        }
    }

    private void RefreshPresentation(
        string state)
    {
        if (tradeManager == null ||
            polishRoot == null)
        {
            return;
        }

        PlayerGameState initiator =
            tradeManager.TradeInitiator;

        PlayerGameState target =
            tradeManager.TradeTarget;

        ResolveFont(
            playerWithLabel);

        if (playerWithLabel != null)
        {
            playerWithLabel.text =
                PlayerWithLabel();
        }

        ResolveFont(
            fromNameText);

        if (fromNameText != null)
        {
            fromNameText.text =
                initiator != null
                    ? AtlasBoardL.PlayerName(
                        initiator)
                    : PlayerLabel();
        }

        ResolveFont(
            toNameText);

        if (toNameText != null)
        {
            toNameText.text =
                target != null
                    ? AtlasBoardL.PlayerName(
                        target)
                    : SelectPlayerLabel();
        }

        if (fromAccent != null)
        {
            fromAccent.color =
                initiator != null
                    ? initiator.UIColor
                    : new Color(
                        0.18f,
                        0.56f,
                        0.88f,
                        1f);
        }

        if (toAccent != null)
        {
            toAccent.color =
                target != null
                    ? target.UIColor
                    : new Color(
                        0.78f,
                        0.39f,
                        0.76f,
                        1f);
        }

        ResolveFont(
            offerHeaderText);

        if (offerHeaderText != null)
        {
            offerHeaderText.text =
                YouGiveLabel();
        }

        ResolveFont(
            requestHeaderText);

        if (requestHeaderText != null)
        {
            requestHeaderText.text =
                OtherPlayerGivesLabel();
        }

        ResolveFont(
            offerPropertyLabelText);

        if (offerPropertyLabelText != null)
        {
            offerPropertyLabelText.text =
                PropertyLabel();
        }

        ResolveFont(
            offerCashLabelText);

        if (offerCashLabelText != null)
        {
            offerCashLabelText.text =
                CashLabel();
        }

        ResolveFont(
            requestPropertyLabelText);

        if (requestPropertyLabelText != null)
        {
            requestPropertyLabelText.text =
                PropertyLabel();
        }

        ResolveFont(
            requestCashLabelText);

        if (requestCashLabelText != null)
        {
            requestCashLabelText.text =
                CashLabel();
        }

        ResolveFont(
            offerStaticPropertyText);

        if (offerStaticPropertyText != null)
        {
            offerStaticPropertyText.text =
                tradeManager.OfferedProperty != null
                    ? tradeManager.OfferedProperty.DisplayName
                    : NoPropertyLabel();
        }

        ResolveFont(
            offerStaticCashText);

        if (offerStaticCashText != null)
        {
            offerStaticCashText.text =
                $"{tradeManager.OfferedCash:N0} ₵";
        }

        ResolveFont(
            requestStaticPropertyText);

        if (requestStaticPropertyText != null)
        {
            requestStaticPropertyText.text =
                tradeManager.RequestedProperty != null
                    ? tradeManager.RequestedProperty.DisplayName
                    : NoPropertyLabel();
        }

        ResolveFont(
            requestStaticCashText);

        if (requestStaticCashText != null)
        {
            requestStaticCashText.text =
                $"{tradeManager.RequestedCash:N0} ₵";
        }

        ResolveFont(
            setupHintText);

        if (setupHintText != null)
        {
            setupHintText.text =
                BuildSetupHint(
                    initiator,
                    target);
        }

        if (string.Equals(
                state,
                "ShowingResult",
                StringComparison.Ordinal))
        {
            SplitResultSummary(
                legacySummary != null
                    ? legacySummary.text
                    : string.Empty,
                out string heading,
                out string body);

            ResolveFont(
                resultTitleText);

            resultTitleText.text =
                string.IsNullOrWhiteSpace(
                    heading)
                    ? ResultLabel()
                    : heading;

            ResolveFont(
                resultBodyText);

            resultBodyText.text =
                body;
        }

        SetButtonLabel(
            sendButton,
            SendOfferLabel());

        SetButtonLabel(
            cancelButton,
            CancelLabel());

        SetButtonLabel(
            acceptButton,
            AcceptLabel());

        SetButtonLabel(
            rejectButton,
            RejectLabel());
    }

    private void HideLegacyStaticLabels()
    {
        ResolveLegacyStaticLabels();

        if (!legacyStaticCaptured)
        {
            legacyTargetWasActive =
                legacyTargetLabel != null &&
                legacyTargetLabel.activeSelf;

            legacyOfferHeaderWasActive =
                legacyOfferHeader != null &&
                legacyOfferHeader.activeSelf;

            legacyRequestHeaderWasActive =
                legacyRequestHeader != null &&
                legacyRequestHeader.activeSelf;

            legacyStaticCaptured =
                true;
        }

        SetGameObjectActive(
            legacyTargetLabel,
            false);

        SetGameObjectActive(
            legacyOfferHeader,
            false);

        SetGameObjectActive(
            legacyRequestHeader,
            false);
    }

    private void RestoreLegacyStaticLabels()
    {
        if (!legacyStaticCaptured)
        {
            return;
        }

        SetGameObjectActive(
            legacyTargetLabel,
            legacyTargetWasActive);

        SetGameObjectActive(
            legacyOfferHeader,
            legacyOfferHeaderWasActive);

        SetGameObjectActive(
            legacyRequestHeader,
            legacyRequestHeaderWasActive);

        legacyStaticCaptured =
            false;
    }

    private void HideLegacyText()
    {
        if (!legacyTextCaptured)
        {
            legacyTitleWasActive =
                legacyTitle != null &&
                legacyTitle.gameObject.activeSelf;

            legacySummaryWasActive =
                legacySummary != null &&
                legacySummary.gameObject.activeSelf;

            legacyTextCaptured =
                true;
        }

        Active(
            legacyTitle,
            false);

        Active(
            legacySummary,
            false);
    }

    private void RestoreLegacyText()
    {
        if (!legacyTextCaptured)
        {
            return;
        }

        Active(
            legacyTitle,
            legacyTitleWasActive);

        Active(
            legacySummary,
            legacySummaryWasActive);

        legacyTextCaptured =
            false;
    }

    private void HidePolish()
    {
        if (polishRoot != null)
        {
            polishRoot.gameObject.SetActive(
                false);
        }
    }

    private string GetPanelStateName()
    {
        object value =
            ReadPrivateFieldObject(
                tradeManager,
                "panelState");

        return value != null
            ? value.ToString()
            : "Closed";
    }

    private void ResolveExistingPresentation()
    {
        playerWithLabel =
            FindText(
                "PlayerWithLabel");

        fromNameText =
            FindText(
                "FromCard/FromName");

        toNameText =
            FindText(
                "ToCard/ToName");

        fromAccent =
            FindImage(
                "FromCard/Accent");

        toAccent =
            FindImage(
                "ToCard/Accent");

        offerHeaderText =
            FindText(
                "OfferCard/Header");

        offerPropertyLabelText =
            FindText(
                "OfferCard/PropertyLabel");

        offerCashLabelText =
            FindText(
                "OfferCard/CashLabel");

        offerStaticPropertyText =
            FindText(
                "OfferCard/StaticProperty");

        offerStaticCashText =
            FindText(
                "OfferCard/StaticCash");

        requestHeaderText =
            FindText(
                "RequestCard/Header");

        requestPropertyLabelText =
            FindText(
                "RequestCard/PropertyLabel");

        requestCashLabelText =
            FindText(
                "RequestCard/CashLabel");

        requestStaticPropertyText =
            FindText(
                "RequestCard/StaticProperty");

        requestStaticCashText =
            FindText(
                "RequestCard/StaticCash");

        setupHintText =
            FindText(
                "SetupHint");

        resultTitleText =
            FindText(
                "ResultTitle");

        resultBodyText =
            FindText(
                "TradeResultCard/ResultBody");

        Transform result =
            polishRoot.Find(
                "TradeResultCard");

        resultCard =
            result != null
                ? result.gameObject
                : null;
    }

    private TMP_Text FindText(
        string path)
    {
        Transform child =
            polishRoot != null
                ? polishRoot.Find(
                    path)
                : null;

        return child != null
            ? child.GetComponent<
                TMP_Text>()
            : null;
    }

    private Image FindImage(
        string path)
    {
        Transform child =
            polishRoot != null
                ? polishRoot.Find(
                    path)
                : null;

        return child != null
            ? child.GetComponent<
                Image>()
            : null;
    }

    private static Transform FindChildRecursive(
        Transform root,
        string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (string.Equals(
                root.name,
                objectName,
                StringComparison.Ordinal))
        {
            return root;
        }

        for (int index = 0;
             index < root.childCount;
             index++)
        {
            Transform found =
                FindChildRecursive(
                    root.GetChild(
                        index),
                    objectName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void Reparent(
        Component component,
        Transform parent)
    {
        if (component == null ||
            parent == null ||
            component.transform.parent ==
                parent)
        {
            return;
        }

        component.transform.SetParent(
            parent,
            false);
    }

    private static GameObject CreatePanel(
        string name,
        Transform parent)
    {
        GameObject panel =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Shadow));

        panel.transform.SetParent(
            parent,
            false);

        Image image =
            panel.GetComponent<
                Image>();

        image.color =
            new Color(
                0.07f,
                0.08f,
                0.14f,
                0.985f);

        image.raycastTarget =
            false;

        Shadow shadow =
            panel.GetComponent<
                Shadow>();

        shadow.effectColor =
            new Color(
                0f,
                0f,
                0f,
                0.25f);

        shadow.effectDistance =
            new Vector2(
                0f,
                -2f);

        return panel;
    }

    private static Image AddAccent(
        Transform parent,
        bool leftSide,
        Color color)
    {
        GameObject go =
            new GameObject(
                "Accent",
                typeof(RectTransform),
                typeof(Image));

        go.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            go.GetComponent<
                RectTransform>();

        rect.anchorMin =
            leftSide
                ? new Vector2(
                    0f,
                    0f)
                : new Vector2(
                    1f,
                    0f);

        rect.anchorMax =
            leftSide
                ? new Vector2(
                    0f,
                    1f)
                : new Vector2(
                    1f,
                    1f);

        rect.pivot =
            leftSide
                ? new Vector2(
                    0f,
                    0.5f)
                : new Vector2(
                    1f,
                    0.5f);

        rect.sizeDelta =
            new Vector2(
                4f,
                0f);

        Image image =
            go.GetComponent<
                Image>();

        image.color =
            color;

        image.raycastTarget =
            false;

        return image;
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject go =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));

        go.transform.SetParent(
            parent,
            false);

        TMP_Text text =
            go.GetComponent<
                TMP_Text>();

        text.fontSize =
            fontSize;

        text.fontStyle =
            style;

        text.alignment =
            alignment;

        text.color =
            Color.white;

        text.raycastTarget =
            false;

        text.richText =
            true;

        ResolveFont(
            text);

        return text;
    }

    private static void Auto(
        TMP_Text text,
        float min,
        float max,
        TextWrappingModes wrapping)
    {
        if (text == null)
        {
            return;
        }

        text.enableAutoSizing =
            true;

        text.fontSizeMin =
            min;

        text.fontSizeMax =
            max;

        text.textWrappingMode =
            wrapping;

        text.overflowMode =
            TextOverflowModes.Truncate;
    }

    private static void ResolveFont(
        TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        AtlasBoardLocalizationManager manager =
            AtlasBoardLocalizationManager.Instance;

        if (manager == null)
        {
            if (text.font == null &&
                TMP_Settings.defaultFontAsset != null)
            {
                text.font =
                    TMP_Settings.defaultFontAsset;
            }

            return;
        }

        TMP_FontAsset current =
            text.font != null
                ? text.font
                : TMP_Settings.defaultFontAsset;

        TMP_FontAsset resolved =
            manager.ResolveFont(
                current);

        if (resolved != null)
        {
            text.font =
                resolved;
        }
    }

    private static void StyleDropdown(
        TMP_Dropdown dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        Image image =
            dropdown.GetComponent<
                Image>();

        if (image != null)
        {
            image.color =
                new Color(
                    0.12f,
                    0.15f,
                    0.22f,
                    0.98f);
        }

        if (dropdown.captionText != null)
        {
            ResolveFont(
                dropdown.captionText);

            Auto(
                dropdown.captionText,
                8f,
                12f,
                TextWrappingModes.NoWrap);

            dropdown.captionText.color =
                Color.white;

            dropdown.captionText.fontStyle =
                FontStyles.Normal;
        }
    }

    private static void StyleInput(
        TMP_InputField input,
        string placeholder)
    {
        if (input == null)
        {
            return;
        }

        Image image =
            input.GetComponent<
                Image>();

        if (image != null)
        {
            image.color =
                new Color(
                    0.12f,
                    0.15f,
                    0.22f,
                    0.98f);
        }

        if (input.textComponent != null)
        {
            ResolveFont(
                input.textComponent);

            Auto(
                input.textComponent,
                8f,
                13f,
                TextWrappingModes.NoWrap);

            input.textComponent.alignment =
                TextAlignmentOptions.Center;

            input.textComponent.color =
                Color.white;

            input.textComponent.fontStyle =
                FontStyles.Normal;
        }

        TMP_Text placeholderText =
            input.placeholder as TMP_Text;

        if (placeholderText != null)
        {
            ResolveFont(
                placeholderText);

            placeholderText.text =
                placeholder;

            Auto(
                placeholderText,
                7f,
                11f,
                TextWrappingModes.NoWrap);

            placeholderText.alignment =
                TextAlignmentOptions.Center;

            placeholderText.color =
                new Color(
                    0.78f,
                    0.82f,
                    0.88f,
                    1f);

            placeholderText.fontStyle =
                FontStyles.Normal;
        }
    }

    private static void StyleButton(
        Button button,
        Color color)
    {
        if (button == null)
        {
            return;
        }

        Image image =
            button.GetComponent<
                Image>();

        if (image != null)
        {
            image.color =
                color;
        }

        TMP_Text label =
            button.GetComponentInChildren<
                TMP_Text>(
                true);

        if (label != null)
        {
            ResolveFont(
                label);

            Auto(
                label,
                8f,
                15f,
                TextWrappingModes.NoWrap);

            label.fontStyle =
                FontStyles.Bold;

            label.alignment =
                TextAlignmentOptions.Center;
        }
    }

    private static void SetButtonLabel(
        Button button,
        string value)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text label =
            button.GetComponentInChildren<
                TMP_Text>(
                true);

        if (label != null)
        {
            ResolveFont(
                label);

            label.text =
                value;
        }
    }

    private static void Box(
        RectTransform rect,
        float xMin,
        float yMin,
        float xMax,
        float yMax)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin =
            new Vector2(
                xMin,
                yMin);

        rect.anchorMax =
            new Vector2(
                xMax,
                yMax);

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;

        rect.localScale =
            Vector3.one;
    }

    private static void Stretch(
        RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;

        rect.localScale =
            Vector3.one;
    }

    private static void Active(
        Component component,
        bool active)
    {
        if (component != null)
        {
            component.gameObject.SetActive(
                active);
        }
    }

    private static void SetGameObjectActive(
        GameObject gameObject,
        bool active)
    {
        if (gameObject != null)
        {
            gameObject.SetActive(
                active);
        }
    }

    private static T ReadPrivateField<T>(
        object target,
        string fieldName)
        where T : class
    {
        if (target == null)
        {
            return null;
        }

        FieldInfo field =
            target.GetType()
                .GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

        return field != null
            ? field.GetValue(
                target) as T
            : null;
    }

    private static object ReadPrivateFieldObject(
        object target,
        string fieldName)
    {
        if (target == null)
        {
            return null;
        }

        FieldInfo field =
            target.GetType()
                .GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

        return field != null
            ? field.GetValue(
                target)
            : null;
    }

    private static void SplitResultSummary(
        string summary,
        out string heading,
        out string body)
    {
        heading =
            string.Empty;

        body =
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                summary))
        {
            return;
        }

        string normalized =
            summary
                .Replace(
                    "\r\n",
                    "\n")
                .Replace(
                    "\r",
                    "\n")
                .Trim();

        string[] lines =
            normalized.Split(
                new[] { '\n' },
                StringSplitOptions.None);

        int headingIndex =
            -1;

        for (int index = 0;
             index < lines.Length;
             index++)
        {
            if (!string.IsNullOrWhiteSpace(
                    lines[index]))
            {
                heading =
                    lines[index].Trim();

                headingIndex =
                    index;

                break;
            }
        }

        if (headingIndex < 0)
        {
            return;
        }

        System.Text.StringBuilder builder =
            new System.Text.StringBuilder();

        for (int index =
                 headingIndex + 1;
             index < lines.Length;
             index++)
        {
            string line =
                lines[index].TrimEnd();

            if (builder.Length > 0)
            {
                builder.Append(
                    '\n');
            }

            builder.Append(
                line);
        }

        body =
            builder
                .ToString()
                .Trim();

        if (string.IsNullOrWhiteSpace(
                body))
        {
            body =
                heading;

            heading =
                ResultLabel();
        }
    }

    private static string BuildSetupHint(
        PlayerGameState initiator,
        PlayerGameState target)
    {
        string from =
            initiator != null
                ? AtlasBoardL.PlayerName(
                    initiator)
                : PlayerLabel();

        string to =
            target != null
                ? AtlasBoardL.PlayerName(
                    target)
                : SelectPlayerLabel();

        return AtlasBoardL.R(
            $"{from} → {to} • Build the offer, then send it.",
            $"{from} → {to} • Teklifi oluştur, sonra gönder.",
            $"{from} → {to} • Prepara la oferta y envíala.",
            $"{from} → {to} • Composez l’offre, puis envoyez-la.",
            $"{from} → {to} • Angebot erstellen und senden.",
            $"{from} → {to} • 제안을 구성한 뒤 보내세요.",
            $"{from} → {to} • Составьте предложение и отправьте его.");
    }

    private static string ResultLabel() =>
        AtlasBoardL.R(
            "TRADE RESULT",
            "TAKAS SONUCU",
            "RESULTADO DEL INTERCAMBIO",
            "RÉSULTAT DE L’ÉCHANGE",
            "HANDELSERGEBNIS",
            "거래 결과",
            "РЕЗУЛЬТАТ ОБМЕНА");

    private static string PlayerWithLabel() =>
        AtlasBoardL.R(
            "PLAYER WITH",
            "OYUNCU",
            "JUGADOR",
            "JOUEUR",
            "SPIELER",
            "상대 플레이어",
            "ИГРОК");

    private static string FromLabel() =>
        AtlasBoardL.R(
            "FROM",
            "GÖNDEREN",
            "DE",
            "DE",
            "VON",
            "보내는 사람",
            "ОТ");

    private static string ToLabel() =>
        AtlasBoardL.R(
            "TO",
            "ALAN",
            "PARA",
            "À",
            "AN",
            "받는 사람",
            "КОМУ");

    private static string YouGiveLabel() =>
        AtlasBoardL.R(
            "YOU GIVE",
            "SEN VERİYORSUN",
            "TÚ OFRECES",
            "VOUS DONNEZ",
            "DU GIBST",
            "내가 주는 것",
            "ВЫ ДАЁТЕ");

    private static string OtherPlayerGivesLabel() =>
        AtlasBoardL.R(
            "OTHER PLAYER GIVES",
            "DİĞER OYUNCU VERİYOR",
            "EL OTRO JUGADOR OFRECE",
            "L’AUTRE JOUEUR DONNE",
            "ANDERER SPIELER GIBT",
            "상대가 주는 것",
            "ДРУГОЙ ИГРОК ДАЁТ");

    private static string PropertyLabel() =>
        AtlasBoardL.R(
            "PROPERTY",
            "MÜLK",
            "PROPIEDAD",
            "PROPRIÉTÉ",
            "GRUNDSTÜCK",
            "부동산",
            "СОБСТВЕННОСТЬ");

    private static string CashLabel() =>
        AtlasBoardL.R(
            "CASH",
            "NAKİT",
            "EFECTIVO",
            "ARGENT",
            "BARGELD",
            "현금",
            "НАЛИЧНЫЕ");

    private static string CashAmountLabel() =>
        AtlasBoardL.R(
            "Cash amount",
            "Nakit miktarı",
            "Cantidad de efectivo",
            "Montant en espèces",
            "Bargeldbetrag",
            "현금 금액",
            "Сумма наличных");

    private static string NoPropertyLabel() =>
        AtlasBoardL.R(
            "No property",
            "Mülk yok",
            "Sin propiedad",
            "Aucune propriété",
            "Kein Grundstück",
            "부동산 없음",
            "Без собственности");

    private static string SelectPlayerLabel() =>
        AtlasBoardL.R(
            "Select player",
            "Oyuncu seç",
            "Seleccionar jugador",
            "Choisir un joueur",
            "Spieler wählen",
            "플레이어 선택",
            "Выберите игрока");

    private static string PlayerLabel() =>
        AtlasBoardL.R(
            "Player",
            "Oyuncu",
            "Jugador",
            "Joueur",
            "Spieler",
            "플레이어",
            "Игрок");

    private static string SendOfferLabel() =>
        AtlasBoardL.R(
            "SEND OFFER",
            "TEKLİF GÖNDER",
            "ENVIAR OFERTA",
            "ENVOYER L’OFFRE",
            "ANGEBOT SENDEN",
            "제안 보내기",
            "ОТПРАВИТЬ");

    private static string CancelLabel() =>
        AtlasBoardL.R(
            "CANCEL",
            "İPTAL",
            "CANCELAR",
            "ANNULER",
            "ABBRECHEN",
            "취소",
            "ОТМЕНА");

    private static string AcceptLabel() =>
        AtlasBoardL.R(
            "ACCEPT",
            "KABUL ET",
            "ACEPTAR",
            "ACCEPTER",
            "ANNEHMEN",
            "수락",
            "ПРИНЯТЬ");

    private static string RejectLabel() =>
        AtlasBoardL.R(
            "REJECT",
            "REDDET",
            "RECHAZAR",
            "REFUSER",
            "ABLEHNEN",
            "거절",
            "ОТКЛОНИТЬ");
}
