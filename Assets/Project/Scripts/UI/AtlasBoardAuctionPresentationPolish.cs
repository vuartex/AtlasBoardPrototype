using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(13100)]
[DisallowMultipleComponent]
public sealed class AtlasBoardAuctionPresentationPolish : MonoBehaviour
{
    private const float RefreshIntervalSeconds = 0.10f;

    private AuctionManager auctionManager;
    private TabletUIManager tabletManager;

    private GameObject auctionPanel;
    private RectTransform tabletRoot;

    private TMP_Text legacyTitle;
    private TMP_Text legacyProperty;
    private TMP_Text legacyStatus;

    private Button legacySmallButton;
    private Button legacyLargeButton;
    private Button legacyPassButton;

    private RectTransform polishRoot;

    private TMP_Text propertyNameText;
    private TMP_Text propertyMetaText;
    private TMP_Text bidLabelText;
    private TMP_Text bidValueText;

    private GameObject bidderCardRoot;
    private Image bidderAccent;
    private TMP_Text bidderHeaderText;
    private TMP_Text bidderNameText;
    private TMP_Text bidderBalanceText;

    private GameObject leaderCardRoot;
    private Image leaderAccent;
    private TMP_Text leaderHeaderText;
    private TMP_Text leaderNameText;
    private TMP_Text leaderBidText;

    private TMP_Text statusText;
    private GameObject resultCardRoot;
    private TMP_Text resultText;

    private Button smallButton;
    private Button largeButton;
    private Button passButton;

    private TMP_Text smallButtonText;
    private TMP_Text largeButtonText;
    private TMP_Text passButtonText;

    private bool legacyVisibilityCaptured;
    private bool legacyTitleWasActive;
    private bool legacyPropertyWasActive;
    private bool legacyStatusWasActive;
    private bool legacySmallWasActive;
    private bool legacyLargeWasActive;
    private bool legacyPassWasActive;

    private float nextRefreshAt;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (UnityEngine.Object.FindAnyObjectByType<
                BoardPath>() == null)
        {
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardAuctionPresentationPolish>() != null)
        {
            return;
        }

        new GameObject(
                "AtlasBoard_Phase14G_AuctionPresentation")
            .AddComponent<
                AtlasBoardAuctionPresentationPolish>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;
    }

    private void OnDisable()
    {
        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;

        RestoreLegacyPresentation();
        HidePolish();
    }

    private void OnDestroy()
    {
        RestoreLegacyPresentation();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        bool visible =
            auctionPanel != null &&
            auctionPanel.activeSelf &&
            tabletRoot != null &&
            tabletRoot.gameObject.activeInHierarchy;

        if (!visible)
        {
            RestoreLegacyPresentation();
            HidePolish();
            return;
        }

        EnsurePolish();
        HideLegacyPresentation();

        if (polishRoot != null)
        {
            polishRoot.SetAsLastSibling();

            if (!polishRoot.gameObject.activeSelf)
            {
                polishRoot.gameObject.SetActive(
                    true);
            }
        }

        MirrorButtonState();

        if (Time.unscaledTime >=
            nextRefreshAt)
        {
            nextRefreshAt =
                Time.unscaledTime +
                RefreshIntervalSeconds;

            RefreshPresentation();
        }
    }

    private void HandleLanguageChanged()
    {
        nextRefreshAt = 0f;
        RefreshPresentation();
    }

    public RectTransform TabletRoot =>
        tabletRoot;

    public RectTransform PolishRoot =>
        polishRoot;

    public Button SmallBidButton =>
        smallButton;

    public Button LargeBidButton =>
        largeButton;

    public Button PassButton =>
        passButton;

    public void RefreshNow()
    {
        ResolveReferences();
        EnsurePolish();
        HideLegacyPresentation();
        MirrorButtonState();
        RefreshPresentation();
    }

    private void ResolveReferences()
    {
        if (auctionManager == null)
        {
            auctionManager =
                UnityEngine.Object.FindAnyObjectByType<
                    AuctionManager>();
        }

        if (tabletManager == null)
        {
            tabletManager =
                UnityEngine.Object.FindAnyObjectByType<
                    TabletUIManager>();
        }

        if (auctionManager != null)
        {
            auctionPanel =
                ReadPrivateField<GameObject>(
                    auctionManager,
                    "auctionPanel");

            legacyTitle =
                ReadPrivateField<TMP_Text>(
                    auctionManager,
                    "auctionTitleText");

            legacyProperty =
                ReadPrivateField<TMP_Text>(
                    auctionManager,
                    "auctionPropertyText");

            legacyStatus =
                ReadPrivateField<TMP_Text>(
                    auctionManager,
                    "auctionStatusText");

            legacySmallButton =
                ReadPrivateField<Button>(
                    auctionManager,
                    "bidSmallButton");

            legacyLargeButton =
                ReadPrivateField<Button>(
                    auctionManager,
                    "bidLargeButton");

            legacyPassButton =
                ReadPrivateField<Button>(
                    auctionManager,
                    "passButton");
        }

        if (tabletManager != null)
        {
            GameObject root =
                ReadPrivateField<GameObject>(
                    tabletManager,
                    "tabletRoot");

            tabletRoot =
                root != null
                    ? root.transform as
                        RectTransform
                    : null;
        }
    }

    private void EnsurePolish()
    {
        if (tabletRoot == null ||
            polishRoot != null)
        {
            return;
        }

        Transform existing =
            tabletRoot.Find(
                "Phase14G_AuctionPolish");

        if (existing != null)
        {
            polishRoot =
                existing as RectTransform;

            ResolveExistingPolish();
            return;
        }

        GameObject rootObject =
            new GameObject(
                "Phase14G_AuctionPolish",
                typeof(RectTransform),
                typeof(LayoutElement));

        rootObject.transform.SetParent(
            tabletRoot,
            false);

        polishRoot =
            rootObject.GetComponent<
                RectTransform>();

        LayoutElement rootLayout =
            rootObject.GetComponent<
                LayoutElement>();

        rootLayout.ignoreLayout =
            true;

        polishRoot.anchorMin =
            Vector2.zero;

        polishRoot.anchorMax =
            Vector2.one;

        polishRoot.offsetMin =
            Vector2.zero;

        polishRoot.offsetMax =
            Vector2.zero;

        CreatePropertyCard();
        CreateBidHero();
        CreateBidderCards();
        CreateStatusLine();
        CreateActionButtons();

        polishRoot.gameObject.SetActive(
            false);
    }

    private void CreatePropertyCard()
    {
        GameObject card =
            CreatePanel(
                "PropertyCard",
                polishRoot,
                new Color(
                    0.055f,
                    0.085f,
                    0.12f,
                    0.985f));

        SetAnchorBox(
            card.GetComponent<RectTransform>(),
            0.07f,
            0.675f,
            0.93f,
            0.79f);

        AddAccent(
            card.transform,
            leftSide: true,
            new Color(
                0.96f,
                0.72f,
                0.16f,
                1f));

        propertyNameText =
            CreateText(
                "PropertyName",
                card.transform,
                18f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

        SetAnchorBox(
            propertyNameText.rectTransform,
            0.04f,
            0.47f,
            0.96f,
            0.94f);

        ConfigureAutoText(
            propertyNameText,
            11f,
            19f,
            TextWrappingModes.NoWrap);

        propertyNameText.color =
            new Color(
                0.98f,
                0.88f,
                0.50f,
                1f);

        propertyMetaText =
            CreateText(
                "PropertyMeta",
                card.transform,
                11f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);

        SetAnchorBox(
            propertyMetaText.rectTransform,
            0.04f,
            0.08f,
            0.96f,
            0.47f);

        ConfigureAutoText(
            propertyMetaText,
            7.5f,
            12f,
            TextWrappingModes.NoWrap);

        propertyMetaText.color =
            new Color(
                0.76f,
                0.82f,
                0.90f,
                1f);
    }

    private void CreateBidHero()
    {
        GameObject hero =
            CreatePanel(
                "CurrentBidHero",
                polishRoot,
                new Color(
                    0.035f,
                    0.060f,
                    0.085f,
                    0.99f));

        SetAnchorBox(
            hero.GetComponent<RectTransform>(),
            0.31f,
            0.515f,
            0.69f,
            0.655f);

        Outline outline =
            hero.AddComponent<
                Outline>();

        outline.effectColor =
            new Color(
                0.96f,
                0.72f,
                0.16f,
                0.55f);

        outline.effectDistance =
            new Vector2(
                1f,
                -1f);

        bidLabelText =
            CreateText(
                "CurrentBidLabel",
                hero.transform,
                10f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        SetAnchorBox(
            bidLabelText.rectTransform,
            0.07f,
            0.57f,
            0.93f,
            0.91f);

        ConfigureAutoText(
            bidLabelText,
            7f,
            11f,
            TextWrappingModes.NoWrap);

        bidLabelText.color =
            new Color(
                0.68f,
                0.75f,
                0.84f,
                1f);

        bidValueText =
            CreateText(
                "CurrentBidValue",
                hero.transform,
                25f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        SetAnchorBox(
            bidValueText.rectTransform,
            0.05f,
            0.08f,
            0.95f,
            0.60f);

        ConfigureAutoText(
            bidValueText,
            15f,
            28f,
            TextWrappingModes.NoWrap);

        bidValueText.color =
            new Color(
                1f,
                0.83f,
                0.28f,
                1f);
    }

    private void CreateBidderCards()
    {
        GameObject bidderCard =
            CreatePanel(
                "CurrentBidderCard",
                polishRoot,
                new Color(
                    0.070f,
                    0.095f,
                    0.135f,
                    0.985f));

        SetAnchorBox(
            bidderCard.GetComponent<RectTransform>(),
            0.07f,
            0.335f,
            0.47f,
            0.485f);

        bidderCardRoot =
            bidderCard;

        bidderAccent =
            AddAccent(
                bidderCard.transform,
                leftSide: true,
                new Color(
                    0.20f,
                    0.60f,
                    0.90f,
                    1f));

        bidderHeaderText =
            CreateText(
                "Header",
                bidderCard.transform,
                9f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

        SetAnchorBox(
            bidderHeaderText.rectTransform,
            0.05f,
            0.69f,
            0.95f,
            0.96f);

        ConfigureAutoText(
            bidderHeaderText,
            6.5f,
            9.5f,
            TextWrappingModes.NoWrap);

        bidderHeaderText.color =
            new Color(
                0.65f,
                0.73f,
                0.82f,
                1f);

        bidderNameText =
            CreateText(
                "PlayerName",
                bidderCard.transform,
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

        SetAnchorBox(
            bidderNameText.rectTransform,
            0.05f,
            0.34f,
            0.95f,
            0.70f);

        ConfigureAutoText(
            bidderNameText,
            8f,
            15f,
            TextWrappingModes.NoWrap);

        bidderBalanceText =
            CreateText(
                "Balance",
                bidderCard.transform,
                10f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);

        SetAnchorBox(
            bidderBalanceText.rectTransform,
            0.05f,
            0.05f,
            0.95f,
            0.35f);

        ConfigureAutoText(
            bidderBalanceText,
            6.5f,
            10.5f,
            TextWrappingModes.NoWrap);

        bidderBalanceText.color =
            new Color(
                0.78f,
                0.83f,
                0.90f,
                1f);

        GameObject leaderCard =
            CreatePanel(
                "HighestBidderCard",
                polishRoot,
                new Color(
                    0.070f,
                    0.095f,
                    0.135f,
                    0.985f));

        SetAnchorBox(
            leaderCard.GetComponent<RectTransform>(),
            0.53f,
            0.335f,
            0.93f,
            0.485f);

        leaderCardRoot =
            leaderCard;

        leaderAccent =
            AddAccent(
                leaderCard.transform,
                leftSide: false,
                new Color(
                    0.96f,
                    0.72f,
                    0.16f,
                    1f));

        leaderHeaderText =
            CreateText(
                "Header",
                leaderCard.transform,
                9f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineRight);

        SetAnchorBox(
            leaderHeaderText.rectTransform,
            0.05f,
            0.69f,
            0.95f,
            0.96f);

        ConfigureAutoText(
            leaderHeaderText,
            6.5f,
            9.5f,
            TextWrappingModes.NoWrap);

        leaderHeaderText.color =
            new Color(
                0.65f,
                0.73f,
                0.82f,
                1f);

        leaderNameText =
            CreateText(
                "PlayerName",
                leaderCard.transform,
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineRight);

        SetAnchorBox(
            leaderNameText.rectTransform,
            0.05f,
            0.34f,
            0.95f,
            0.70f);

        ConfigureAutoText(
            leaderNameText,
            8f,
            15f,
            TextWrappingModes.NoWrap);

        leaderBidText =
            CreateText(
                "Bid",
                leaderCard.transform,
                10f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineRight);

        SetAnchorBox(
            leaderBidText.rectTransform,
            0.05f,
            0.05f,
            0.95f,
            0.35f);

        ConfigureAutoText(
            leaderBidText,
            6.5f,
            10.5f,
            TextWrappingModes.NoWrap);

        leaderBidText.color =
            new Color(
                0.78f,
                0.83f,
                0.90f,
                1f);
    }

    private void CreateStatusLine()
    {
        statusText =
            CreateText(
                "AuctionStatus",
                polishRoot,
                11f,
                FontStyles.Normal,
                TextAlignmentOptions.Center);

        SetAnchorBox(
            statusText.rectTransform,
            0.08f,
            0.275f,
            0.92f,
            0.325f);

        ConfigureAutoText(
            statusText,
            7f,
            11.5f,
            TextWrappingModes.NoWrap);

        statusText.color =
            new Color(
                0.76f,
                0.82f,
                0.90f,
                1f);

        resultCardRoot =
            CreatePanel(
                "AuctionResultCard",
                polishRoot,
                new Color(
                    0.075f,
                    0.095f,
                    0.125f,
                    0.99f));

        SetAnchorBox(
            resultCardRoot.GetComponent<RectTransform>(),
            0.13f,
            0.155f,
            0.87f,
            0.485f);

        Outline outline =
            resultCardRoot.AddComponent<Outline>();

        outline.effectColor =
            new Color(
                0.96f,
                0.72f,
                0.16f,
                0.70f);

        outline.effectDistance =
            new Vector2(
                1f,
                -1f);

        resultText =
            CreateText(
                "AuctionResultText",
                resultCardRoot.transform,
                22f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        SetAnchorBox(
            resultText.rectTransform,
            0.07f,
            0.10f,
            0.93f,
            0.90f);

        ConfigureAutoText(
            resultText,
            13f,
            24f,
            TextWrappingModes.Normal);

        resultText.overflowMode =
            TextOverflowModes.Truncate;

        resultText.color =
            new Color(
                1f,
                0.88f,
                0.48f,
                1f);

        resultCardRoot.SetActive(
            false);
    }

    private void CreateActionButtons()
    {
        smallButton =
            CreateActionButton(
                "Button_BidSmall",
                polishRoot,
                new Vector2(
                    0.07f,
                    0.105f),
                new Vector2(
                    0.345f,
                    0.235f),
                new Color(
                    0.13f,
                    0.48f,
                    0.76f,
                    1f),
                auctionManager != null
                    ? auctionManager.PlaceSmallBid
                    : null,
                out smallButtonText);

        largeButton =
            CreateActionButton(
                "Button_BidLarge",
                polishRoot,
                new Vector2(
                    0.365f,
                    0.105f),
                new Vector2(
                    0.64f,
                    0.235f),
                new Color(
                    0.12f,
                    0.60f,
                    0.43f,
                    1f),
                auctionManager != null
                    ? auctionManager.PlaceLargeBid
                    : null,
                out largeButtonText);

        passButton =
            CreateActionButton(
                "Button_Pass",
                polishRoot,
                new Vector2(
                    0.66f,
                    0.105f),
                new Vector2(
                    0.93f,
                    0.235f),
                new Color(
                    0.37f,
                    0.41f,
                    0.48f,
                    1f),
                auctionManager != null
                    ? auctionManager.PassCurrentBidder
                    : null,
                out passButtonText);
    }

    private void RefreshPresentation()
    {
        if (auctionManager == null ||
            polishRoot == null)
        {
            return;
        }

        BoardTile property =
            auctionManager.AuctionProperty;

        if (propertyNameText != null)
        {
            ResolveFont(
                propertyNameText);

            propertyNameText.text =
                property != null
                    ? property.DisplayName
                    : AuctionLabel();
        }

        if (propertyMetaText != null)
        {
            ResolveFont(
                propertyMetaText);

            propertyMetaText.text =
                property != null
                    ? $"{PurchaseLabel()} {property.PurchasePrice:N0} ₵   •   " +
                      $"{BaseRentLabel()} {property.BaseRent:N0} ₵"
                    : string.Empty;
        }

        if (bidLabelText != null)
        {
            ResolveFont(
                bidLabelText);

            bidLabelText.text =
                CurrentBidLabel();
        }

        if (bidValueText != null)
        {
            ResolveFont(
                bidValueText);

            bidValueText.text =
                $"{auctionManager.CurrentBid:N0} ₵";
        }

        PlayerGameState bidder =
            auctionManager.CurrentBidder;

        PlayerGameState leader =
            auctionManager.HighestBidder;

        if (bidderHeaderText != null)
        {
            ResolveFont(
                bidderHeaderText);

            bidderHeaderText.text =
                CurrentPlayerLabel();
        }

        if (bidderNameText != null)
        {
            ResolveFont(
                bidderNameText);

            bidderNameText.text =
                bidder != null
                    ? AtlasBoardL.PlayerName(
                        bidder)
                    : NoneLabel();
        }

        if (bidderBalanceText != null)
        {
            ResolveFont(
                bidderBalanceText);

            bidderBalanceText.text =
                bidder != null
                    ? $"{BalanceLabel()} {bidder.CurrentMoney:N0} ₵"
                    : string.Empty;
        }

        if (bidderAccent != null)
        {
            bidderAccent.color =
                bidder != null
                    ? bidder.UIColor
                    : new Color(
                        0.20f,
                        0.60f,
                        0.90f,
                        1f);
        }

        if (leaderHeaderText != null)
        {
            ResolveFont(
                leaderHeaderText);

            leaderHeaderText.text =
                HighestBidderLabel();
        }

        if (leaderNameText != null)
        {
            ResolveFont(
                leaderNameText);

            leaderNameText.text =
                leader != null
                    ? AtlasBoardL.PlayerName(
                        leader)
                    : NoneLabel();
        }

        if (leaderBidText != null)
        {
            ResolveFont(
                leaderBidText);

            leaderBidText.text =
                leader != null
                    ? $"{WinningBidLabel()} {auctionManager.CurrentBid:N0} ₵"
                    : NoBidYetLabel();
        }

        if (leaderAccent != null)
        {
            leaderAccent.color =
                leader != null
                    ? leader.UIColor
                    : new Color(
                        0.96f,
                        0.72f,
                        0.16f,
                        1f);
        }

        bool resolutionDisplay =
            auctionPanel != null &&
            auctionPanel.activeSelf &&
            !auctionManager.IsAuctionActive;

        if (statusText != null)
        {
            ResolveFont(
                statusText);

            if (!resolutionDisplay &&
                bidder != null)
            {
                statusText.text =
                    $"{AtlasBoardL.PlayerName(bidder)}  •  " +
                    $"{NextBidLabel()} {auctionManager.NextSmallBidAmount:N0} ₵";
            }
            else
            {
                statusText.text =
                    string.Empty;
            }
        }

        if (resultText != null)
        {
            ResolveFont(
                resultText);

            resultText.text =
                resolutionDisplay &&
                legacyStatus != null &&
                !string.IsNullOrWhiteSpace(
                    legacyStatus.text)
                    ? legacyStatus.text
                    : string.Empty;
        }

        ApplyResolutionPresentation(
            resolutionDisplay);

        if (smallButtonText != null)
        {
            ResolveFont(
                smallButtonText);

            smallButtonText.text =
                $"{BidLabel()}  {auctionManager.NextSmallBidAmount:N0} ₵";
        }

        if (largeButtonText != null)
        {
            ResolveFont(
                largeButtonText);

            largeButtonText.text =
                $"{BidLabel()}  {auctionManager.NextLargeBidAmount:N0} ₵";
        }

        if (passButtonText != null)
        {
            ResolveFont(
                passButtonText);

            passButtonText.text =
                PassLabel();
        }
    }

    private void ApplyResolutionPresentation(
        bool resolutionDisplay)
    {
        if (resultCardRoot != null)
        {
            resultCardRoot.SetActive(
                resolutionDisplay);
        }

        if (statusText != null)
        {
            statusText.gameObject.SetActive(
                !resolutionDisplay);
        }

        if (bidderCardRoot != null)
        {
            bidderCardRoot.SetActive(
                !resolutionDisplay);
        }

        if (leaderCardRoot != null)
        {
            leaderCardRoot.SetActive(
                !resolutionDisplay);
        }

        if (smallButton != null)
        {
            smallButton.gameObject.SetActive(
                !resolutionDisplay);
        }

        if (largeButton != null)
        {
            largeButton.gameObject.SetActive(
                !resolutionDisplay);
        }

        if (passButton != null)
        {
            passButton.gameObject.SetActive(
                !resolutionDisplay);
        }
    }

    private void MirrorButtonState()
    {
        if (smallButton != null)
        {
            smallButton.interactable =
                legacySmallButton != null &&
                legacySmallButton.interactable;

            ApplyButtonVisualState(
                smallButton,
                new Color(
                    0.13f,
                    0.48f,
                    0.76f,
                    1f));
        }

        if (largeButton != null)
        {
            largeButton.interactable =
                legacyLargeButton != null &&
                legacyLargeButton.interactable;

            ApplyButtonVisualState(
                largeButton,
                new Color(
                    0.12f,
                    0.60f,
                    0.43f,
                    1f));
        }

        if (passButton != null)
        {
            passButton.interactable =
                legacyPassButton != null &&
                legacyPassButton.interactable;

            ApplyButtonVisualState(
                passButton,
                new Color(
                    0.37f,
                    0.41f,
                    0.48f,
                    1f));
        }
    }

    private void HideLegacyPresentation()
    {
        if (!legacyVisibilityCaptured)
        {
            legacyTitleWasActive =
                IsActive(
                    legacyTitle);

            legacyPropertyWasActive =
                IsActive(
                    legacyProperty);

            legacyStatusWasActive =
                IsActive(
                    legacyStatus);

            legacySmallWasActive =
                IsActive(
                    legacySmallButton);

            legacyLargeWasActive =
                IsActive(
                    legacyLargeButton);

            legacyPassWasActive =
                IsActive(
                    legacyPassButton);

            legacyVisibilityCaptured =
                true;
        }

        SetActive(
            legacyTitle,
            false);

        SetActive(
            legacyProperty,
            false);

        SetActive(
            legacyStatus,
            false);

        SetActive(
            legacySmallButton,
            false);

        SetActive(
            legacyLargeButton,
            false);

        SetActive(
            legacyPassButton,
            false);
    }

    private void RestoreLegacyPresentation()
    {
        if (!legacyVisibilityCaptured)
        {
            return;
        }

        SetActive(
            legacyTitle,
            legacyTitleWasActive);

        SetActive(
            legacyProperty,
            legacyPropertyWasActive);

        SetActive(
            legacyStatus,
            legacyStatusWasActive);

        SetActive(
            legacySmallButton,
            legacySmallWasActive);

        SetActive(
            legacyLargeButton,
            legacyLargeWasActive);

        SetActive(
            legacyPassButton,
            legacyPassWasActive);

        legacyVisibilityCaptured =
            false;
    }

    private void HidePolish()
    {
        if (polishRoot != null &&
            polishRoot.gameObject.activeSelf)
        {
            polishRoot.gameObject.SetActive(
                false);
        }
    }

    private void ResolveExistingPolish()
    {
        if (polishRoot == null)
        {
            return;
        }

        propertyNameText =
            FindText(
                polishRoot,
                "PropertyName");

        propertyMetaText =
            FindText(
                polishRoot,
                "PropertyMeta");

        bidLabelText =
            FindText(
                polishRoot,
                "CurrentBidLabel");

        bidValueText =
            FindText(
                polishRoot,
                "CurrentBidValue");

        Transform bidderCardTransform =
            polishRoot.Find(
                "CurrentBidderCard");

        bidderCardRoot =
            bidderCardTransform != null
                ? bidderCardTransform.gameObject
                : null;

        bidderHeaderText =
            FindText(
                polishRoot,
                "CurrentBidderCard/Header");

        bidderNameText =
            FindText(
                polishRoot,
                "CurrentBidderCard/PlayerName");

        bidderBalanceText =
            FindText(
                polishRoot,
                "CurrentBidderCard/Balance");

        bidderAccent =
            FindImage(
                polishRoot,
                "CurrentBidderCard/Accent");

        Transform leaderCardTransform =
            polishRoot.Find(
                "HighestBidderCard");

        leaderCardRoot =
            leaderCardTransform != null
                ? leaderCardTransform.gameObject
                : null;

        leaderHeaderText =
            FindText(
                polishRoot,
                "HighestBidderCard/Header");

        leaderNameText =
            FindText(
                polishRoot,
                "HighestBidderCard/PlayerName");

        leaderBidText =
            FindText(
                polishRoot,
                "HighestBidderCard/Bid");

        leaderAccent =
            FindImage(
                polishRoot,
                "HighestBidderCard/Accent");

        statusText =
            FindText(
                polishRoot,
                "AuctionStatus");

        Transform resultCardTransform =
            polishRoot.Find(
                "AuctionResultCard");

        resultCardRoot =
            resultCardTransform != null
                ? resultCardTransform.gameObject
                : null;

        resultText =
            FindText(
                polishRoot,
                "AuctionResultCard/AuctionResultText");

        smallButton =
            FindButton(
                polishRoot,
                "Button_BidSmall");

        largeButton =
            FindButton(
                polishRoot,
                "Button_BidLarge");

        passButton =
            FindButton(
                polishRoot,
                "Button_Pass");

        smallButtonText =
            smallButton != null
                ? smallButton.GetComponentInChildren<
                    TMP_Text>(
                    true)
                : null;

        largeButtonText =
            largeButton != null
                ? largeButton.GetComponentInChildren<
                    TMP_Text>(
                    true)
                : null;

        passButtonText =
            passButton != null
                ? passButton.GetComponentInChildren<
                    TMP_Text>(
                    true)
                : null;
    }

    private static GameObject CreatePanel(
        string name,
        Transform parent,
        Color color)
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
            panel.GetComponent<Image>();

        image.color =
            color;

        image.raycastTarget =
            false;

        Shadow shadow =
            panel.GetComponent<Shadow>();

        shadow.effectColor =
            new Color(
                0f,
                0f,
                0f,
                0.28f);

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
        GameObject accentObject =
            new GameObject(
                "Accent",
                typeof(RectTransform),
                typeof(Image));

        accentObject.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            accentObject.GetComponent<
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
            accentObject.GetComponent<
                Image>();

        image.color =
            color;

        image.raycastTarget =
            false;

        return image;
    }

    private static Button CreateActionButton(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color baseColor,
        UnityEngine.Events.UnityAction action,
        out TMP_Text label)
    {
        GameObject buttonObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(Shadow));

        buttonObject.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            buttonObject.GetComponent<
                RectTransform>();

        rect.anchorMin =
            anchorMin;

        rect.anchorMax =
            anchorMax;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;

        Image image =
            buttonObject.GetComponent<
                Image>();

        image.color =
            baseColor;

        Button button =
            buttonObject.GetComponent<
                Button>();

        if (action != null)
        {
            button.onClick.AddListener(
                action);
        }

        Shadow shadow =
            buttonObject.GetComponent<
                Shadow>();

        shadow.effectColor =
            new Color(
                0f,
                0f,
                0f,
                0.30f);

        shadow.effectDistance =
            new Vector2(
                0f,
                -2f);

        label =
            CreateText(
                "Label",
                buttonObject.transform,
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Stretch(
            label.rectTransform,
            5f);

        ConfigureAutoText(
            label,
            8f,
            15f,
            TextWrappingModes.NoWrap);

        return button;
    }

    private static void ApplyButtonVisualState(
        Button button,
        Color baseColor)
    {
        if (button == null)
        {
            return;
        }

        Image image =
            button.GetComponent<
                Image>();

        if (image == null)
        {
            return;
        }

        image.color =
            button.interactable
                ? baseColor
                : new Color(
                    0.18f,
                    0.21f,
                    0.26f,
                    0.82f);
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        float fontSize,
        FontStyles fontStyle,
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
            fontStyle;

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

    private static void ConfigureAutoText(
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

    private static bool IsActive(
        Component component)
    {
        return component != null &&
               component.gameObject.activeSelf;
    }

    private static void SetActive(
        Component component,
        bool active)
    {
        if (component != null)
        {
            component.gameObject.SetActive(
                active);
        }
    }

    private static void SetAnchorBox(
        RectTransform rect,
        float xMin,
        float yMin,
        float xMax,
        float yMax)
    {
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
    }

    private static void Stretch(
        RectTransform rect,
        float inset)
    {
        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            new Vector2(
                inset,
                inset);

        rect.offsetMax =
            new Vector2(
                -inset,
                -inset);
    }

    private static TMP_Text FindText(
        Transform root,
        string path)
    {
        Transform child =
            root != null
                ? root.Find(
                    path)
                : null;

        return child != null
            ? child.GetComponent<
                TMP_Text>()
            : null;
    }

    private static Image FindImage(
        Transform root,
        string path)
    {
        Transform child =
            root != null
                ? root.Find(
                    path)
                : null;

        return child != null
            ? child.GetComponent<
                Image>()
            : null;
    }

    private static Button FindButton(
        Transform root,
        string path)
    {
        Transform child =
            root != null
                ? root.Find(
                    path)
                : null;

        return child != null
            ? child.GetComponent<
                Button>()
            : null;
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

    private static string AuctionLabel() =>
        AtlasBoardL.R(
            "AUCTION",
            "AÇIK ARTIRMA",
            "SUBASTA",
            "ENCHÈRES",
            "AUKTION",
            "경매",
            "АУКЦИОН");

    private static string PurchaseLabel() =>
        AtlasBoardL.R(
            "PURCHASE",
            "SATIN ALMA",
            "COMPRA",
            "ACHAT",
            "KAUFPREIS",
            "구매",
            "ПОКУПКА");

    private static string BaseRentLabel() =>
        AtlasBoardL.R(
            "BASE RENT",
            "TABAN KİRA",
            "RENTA BASE",
            "LOYER DE BASE",
            "GRUNDMIETE",
            "기본 임대료",
            "БАЗОВАЯ АРЕНДА");

    private static string CurrentBidLabel() =>
        AtlasBoardL.R(
            "CURRENT BID",
            "GÜNCEL TEKLİF",
            "PUJA ACTUAL",
            "ENCHÈRE ACTUELLE",
            "AKTUELLES GEBOT",
            "현재 입찰가",
            "ТЕКУЩАЯ СТАВКА");

    private static string CurrentPlayerLabel() =>
        AtlasBoardL.R(
            "CURRENT BIDDER",
            "SIRADAKİ OYUNCU",
            "POSTOR ACTUAL",
            "ENCHÉRISSEUR ACTUEL",
            "AKTUELLER BIETER",
            "현재 입찰자",
            "ТЕКУЩИЙ УЧАСТНИК");

    private static string HighestBidderLabel() =>
        AtlasBoardL.R(
            "HIGHEST BIDDER",
            "EN YÜKSEK TEKLİF",
            "MEJOR POSTOR",
            "MEILLEUR ENCHÉRISSEUR",
            "HÖCHSTBIETENDER",
            "최고 입찰자",
            "ЛУЧШИЙ УЧАСТНИК");

    private static string BalanceLabel() =>
        AtlasBoardL.R(
            "BALANCE",
            "BAKİYE",
            "SALDO",
            "SOLDE",
            "GUTHABEN",
            "잔액",
            "БАЛАНС");

    private static string WinningBidLabel() =>
        AtlasBoardL.R(
            "LEADING BID",
            "ÖNDEKİ TEKLİF",
            "PUJA LÍDER",
            "MEILLEURE OFFRE",
            "FÜHRENDES GEBOT",
            "선두 입찰가",
            "ЛИДИРУЮЩАЯ СТАВКА");

    private static string NoBidYetLabel() =>
        AtlasBoardL.R(
            "NO BID YET",
            "HENÜZ TEKLİF YOK",
            "SIN PUJAS",
            "AUCUNE OFFRE",
            "NOCH KEIN GEBOT",
            "아직 입찰 없음",
            "СТАВОК ПОКА НЕТ");

    private static string NextBidLabel() =>
        AtlasBoardL.R(
            "NEXT BID",
            "SONRAKİ TEKLİF",
            "SIGUIENTE PUJA",
            "PROCHAINE OFFRE",
            "NÄCHSTES GEBOT",
            "다음 입찰",
            "СЛЕДУЮЩАЯ СТАВКА");

    private static string BidLabel() =>
        AtlasBoardL.R(
            "BID",
            "TEKLİF",
            "PUJAR",
            "ENCHÉRIR",
            "BIETEN",
            "입찰",
            "СТАВКА");

    private static string PassLabel() =>
        AtlasBoardL.R(
            "PASS",
            "PAS",
            "PASAR",
            "PASSER",
            "PASSEN",
            "패스",
            "ПАС");

    private static string NoneLabel() =>
        AtlasBoardL.R(
            "NONE",
            "YOK",
            "NINGUNO",
            "AUCUN",
            "KEINER",
            "없음",
            "НЕТ");
}
