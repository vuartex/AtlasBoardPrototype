using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(13300)]
[DisallowMultipleComponent]
public sealed class AtlasBoardDevelopmentPresentationPolish :
    MonoBehaviour
{
    private const float RefreshIntervalSeconds = 0.10f;

    private TileResolutionManager tileResolutionManager;
    private PropertyDevelopmentManager developmentManager;
    private TabletUIManager tabletManager;

    private GameObject developmentPanel;
    private RectTransform tabletRoot;
    private TMP_Text legacyInfoText;

    private Button developButton;
    private Button skipButton;

    private RectTransform polishRoot;

    private TMP_Text propertyNameText;
    private TMP_Text groupNameText;
    private TMP_Text playerNameText;
    private TMP_Text playerBalanceText;
    private Image playerAccent;

    private TMP_Text levelLabelText;
    private TMP_Text levelValueText;
    private TMP_Text groupLevelsLabelText;
    private TMP_Text groupLevelsValueText;
    private TMP_Text ruleBadgeText;

    private TMP_Text rentLabelText;
    private TMP_Text currentRentText;
    private TMP_Text rentArrowText;
    private TMP_Text nextRentText;

    private TMP_Text costLabelText;
    private TMP_Text costValueText;
    private TMP_Text balanceLabelText;
    private TMP_Text balanceValueText;

    private GameObject statusCard;
    private Image statusCardImage;
    private TMP_Text statusHeadingText;
    private TMP_Text statusReasonText;

    private bool legacyCaptured;
    private bool legacyInfoWasActive;
    private bool authoritativeButtonsStyled;

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
                AtlasBoardDevelopmentPresentationPolish>() != null)
        {
            return;
        }

        new GameObject(
                "AtlasBoard_Phase14I_DevelopmentPresentation")
            .AddComponent<
                AtlasBoardDevelopmentPresentationPolish>();
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

        RestoreLegacyInfo();
        HidePolish();
    }

    private void HandleLanguageChanged()
    {
        nextRefreshAt = 0f;
    }

    private void LateUpdate()
    {
        ResolveReferences();

        bool visible =
            developmentPanel != null &&
            developmentPanel.activeSelf &&
            tabletRoot != null &&
            tabletRoot.gameObject.activeInHierarchy;

        if (!visible)
        {
            RestoreLegacyInfo();
            HidePolish();
            return;
        }

        EnsurePresentation();
        HideLegacyInfo();

        if (polishRoot != null)
        {
            polishRoot.SetAsLastSibling();

            if (!polishRoot.gameObject.activeSelf)
            {
                polishRoot.gameObject.SetActive(
                    true);
            }
        }

        RefreshAuthoritativeButtonVisuals();

        if (Time.unscaledTime >=
            nextRefreshAt)
        {
            nextRefreshAt =
                Time.unscaledTime +
                RefreshIntervalSeconds;

            RefreshPresentation();
        }
    }

    public RectTransform TabletRoot =>
        tabletRoot;

    public RectTransform PolishRoot =>
        polishRoot;

    public Button DevelopButton =>
        developButton;

    public Button SkipButton =>
        skipButton;

    public void RefreshNow()
    {
        ResolveReferences();
        EnsurePresentation();
        HideLegacyInfo();
        RefreshPresentation();
    }

    private void ResolveReferences()
    {
        if (tileResolutionManager == null)
        {
            tileResolutionManager =
                UnityEngine.Object.FindAnyObjectByType<
                    TileResolutionManager>();
        }

        if (developmentManager == null)
        {
            developmentManager =
                UnityEngine.Object.FindAnyObjectByType<
                    PropertyDevelopmentManager>();
        }

        if (tabletManager == null)
        {
            tabletManager =
                UnityEngine.Object.FindAnyObjectByType<
                    TabletUIManager>();
        }

        if (tileResolutionManager != null)
        {
            developmentPanel =
                ReadPrivateField<GameObject>(
                    tileResolutionManager,
                    "developmentPanel");

            legacyInfoText =
                ReadPrivateField<TMP_Text>(
                    tileResolutionManager,
                    "developmentInfoText");

            developButton =
                ReadPrivateField<Button>(
                    tileResolutionManager,
                    "developButton");

            skipButton =
                ReadPrivateField<Button>(
                    tileResolutionManager,
                    "skipDevelopmentButton");
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
                    "Phase14I_DevelopmentPolish");

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

        ReparentAuthoritativeButtons();
    }

    private void BuildPresentation()
    {
        GameObject root =
            new GameObject(
                "Phase14I_DevelopmentPolish",
                typeof(RectTransform),
                typeof(LayoutElement));

        root.transform.SetParent(
            tabletRoot,
            false);

        polishRoot =
            root.GetComponent<
                RectTransform>();

        root.GetComponent<
                LayoutElement>()
            .ignoreLayout = true;

        Stretch(
            polishRoot);

        CreatePropertyHero();
        CreateLevelCard();
        CreateRentCard();
        CreateCostCard();
        CreateStatusCard();

        ReparentAuthoritativeButtons();

        polishRoot.gameObject.SetActive(
            false);
    }

    private void CreatePropertyHero()
    {
        GameObject hero =
            CreatePanel(
                "PropertyHero",
                polishRoot,
                new Color(
                    0.060f,
                    0.085f,
                    0.120f,
                    0.99f));

        Box(
            hero.GetComponent<
                RectTransform>(),
            0.07f,
            0.69f,
            0.93f,
            0.82f);

        playerAccent =
            AddAccent(
                hero.transform,
                true,
                new Color(
                    0.18f,
                    0.56f,
                    0.88f,
                    1f));

        propertyNameText =
            CreateText(
                "PropertyName",
                hero.transform,
                19f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

        Box(
            propertyNameText.rectTransform,
            0.04f,
            0.49f,
            0.66f,
            0.93f);

        Auto(
            propertyNameText,
            11f,
            21f,
            TextWrappingModes.NoWrap);

        propertyNameText.color =
            new Color(
                0.98f,
                0.88f,
                0.50f,
                1f);

        groupNameText =
            CreateText(
                "GroupName",
                hero.transform,
                10f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);

        Box(
            groupNameText.rectTransform,
            0.04f,
            0.10f,
            0.66f,
            0.49f);

        Auto(
            groupNameText,
            7f,
            11f,
            TextWrappingModes.NoWrap);

        groupNameText.color =
            new Color(
                0.73f,
                0.80f,
                0.89f,
                1f);

        playerNameText =
            CreateText(
                "PlayerName",
                hero.transform,
                13f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineRight);

        Box(
            playerNameText.rectTransform,
            0.68f,
            0.50f,
            0.96f,
            0.92f);

        Auto(
            playerNameText,
            8f,
            14f,
            TextWrappingModes.NoWrap);

        playerBalanceText =
            CreateText(
                "PlayerBalance",
                hero.transform,
                10f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineRight);

        Box(
            playerBalanceText.rectTransform,
            0.68f,
            0.12f,
            0.96f,
            0.50f);

        Auto(
            playerBalanceText,
            7f,
            11f,
            TextWrappingModes.NoWrap);

        playerBalanceText.color =
            new Color(
                0.73f,
                0.80f,
                0.89f,
                1f);
    }

    private void CreateLevelCard()
    {
        GameObject card =
            CreatePanel(
                "LevelCard",
                polishRoot,
                new Color(
                    0.070f,
                    0.095f,
                    0.135f,
                    0.99f));

        Box(
            card.GetComponent<
                RectTransform>(),
            0.06f,
            0.40f,
            0.37f,
            0.64f);

        levelLabelText =
            CreateText(
                "LevelLabel",
                card.transform,
                9f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            levelLabelText.rectTransform,
            0.08f,
            0.77f,
            0.92f,
            0.93f);

        Auto(
            levelLabelText,
            6.5f,
            10f,
            TextWrappingModes.NoWrap);

        levelLabelText.color =
            new Color(
                0.68f,
                0.75f,
                0.84f,
                1f);

        levelValueText =
            CreateText(
                "LevelValue",
                card.transform,
                29f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            levelValueText.rectTransform,
            0.08f,
            0.46f,
            0.92f,
            0.78f);

        Auto(
            levelValueText,
            18f,
            31f,
            TextWrappingModes.NoWrap);

        levelValueText.color =
            new Color(
                1f,
                0.83f,
                0.28f,
                1f);

        groupLevelsLabelText =
            CreateText(
                "GroupLevelsLabel",
                card.transform,
                8f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            groupLevelsLabelText.rectTransform,
            0.08f,
            0.29f,
            0.92f,
            0.44f);

        Auto(
            groupLevelsLabelText,
            6f,
            8.5f,
            TextWrappingModes.NoWrap);

        groupLevelsLabelText.color =
            new Color(
                0.68f,
                0.75f,
                0.84f,
                1f);

        groupLevelsValueText =
            CreateText(
                "GroupLevelsValue",
                card.transform,
                12f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            groupLevelsValueText.rectTransform,
            0.08f,
            0.12f,
            0.92f,
            0.31f);

        Auto(
            groupLevelsValueText,
            7f,
            13f,
            TextWrappingModes.NoWrap);

        ruleBadgeText =
            CreateText(
                "RuleBadge",
                card.transform,
                7.5f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            ruleBadgeText.rectTransform,
            0.15f,
            0.01f,
            0.85f,
            0.13f);

        Auto(
            ruleBadgeText,
            5.5f,
            8f,
            TextWrappingModes.NoWrap);

        ruleBadgeText.color =
            new Color(
                0.65f,
                0.80f,
                0.92f,
                1f);
    }

    private void CreateRentCard()
    {
        GameObject card =
            CreatePanel(
                "RentCard",
                polishRoot,
                new Color(
                    0.070f,
                    0.095f,
                    0.135f,
                    0.99f));

        Box(
            card.GetComponent<
                RectTransform>(),
            0.385f,
            0.40f,
            0.68f,
            0.64f);

        rentLabelText =
            CreateText(
                "RentLabel",
                card.transform,
                9f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            rentLabelText.rectTransform,
            0.08f,
            0.77f,
            0.92f,
            0.93f);

        Auto(
            rentLabelText,
            6.5f,
            10f,
            TextWrappingModes.NoWrap);

        rentLabelText.color =
            new Color(
                0.68f,
                0.75f,
                0.84f,
                1f);

        currentRentText =
            CreateText(
                "CurrentRent",
                card.transform,
                18f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            currentRentText.rectTransform,
            0.05f,
            0.38f,
            0.40f,
            0.72f);

        Auto(
            currentRentText,
            10f,
            20f,
            TextWrappingModes.NoWrap);

        rentArrowText =
            CreateText(
                "Arrow",
                card.transform,
                18f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            rentArrowText.rectTransform,
            0.40f,
            0.38f,
            0.60f,
            0.72f);

        rentArrowText.text =
            "→";

        rentArrowText.color =
            new Color(
                0.96f,
                0.72f,
                0.16f,
                1f);

        nextRentText =
            CreateText(
                "NextRent",
                card.transform,
                18f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            nextRentText.rectTransform,
            0.60f,
            0.38f,
            0.95f,
            0.72f);

        Auto(
            nextRentText,
            10f,
            20f,
            TextWrappingModes.NoWrap);

        nextRentText.color =
            new Color(
                0.41f,
                0.86f,
                0.58f,
                1f);

        TMP_Text currentCaption =
            CreateText(
                "CurrentCaption",
                card.transform,
                7f,
                FontStyles.Normal,
                TextAlignmentOptions.Center);

        Box(
            currentCaption.rectTransform,
            0.05f,
            0.17f,
            0.40f,
            0.37f);

        Auto(
            currentCaption,
            5.5f,
            7.5f,
            TextWrappingModes.NoWrap);

        currentCaption.text =
            CurrentLabel();

        currentCaption.color =
            new Color(
                0.68f,
                0.75f,
                0.84f,
                1f);

        TMP_Text nextCaption =
            CreateText(
                "NextCaption",
                card.transform,
                7f,
                FontStyles.Normal,
                TextAlignmentOptions.Center);

        Box(
            nextCaption.rectTransform,
            0.60f,
            0.17f,
            0.95f,
            0.37f);

        Auto(
            nextCaption,
            5.5f,
            7.5f,
            TextWrappingModes.NoWrap);

        nextCaption.text =
            NextLabel();

        nextCaption.color =
            new Color(
                0.68f,
                0.75f,
                0.84f,
                1f);
    }

    private void CreateCostCard()
    {
        GameObject card =
            CreatePanel(
                "CostCard",
                polishRoot,
                new Color(
                    0.070f,
                    0.095f,
                    0.135f,
                    0.99f));

        Box(
            card.GetComponent<
                RectTransform>(),
            0.695f,
            0.40f,
            0.94f,
            0.64f);

        costLabelText =
            CreateText(
                "CostLabel",
                card.transform,
                9f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            costLabelText.rectTransform,
            0.08f,
            0.77f,
            0.92f,
            0.93f);

        Auto(
            costLabelText,
            6.5f,
            10f,
            TextWrappingModes.NoWrap);

        costLabelText.color =
            new Color(
                0.68f,
                0.75f,
                0.84f,
                1f);

        costValueText =
            CreateText(
                "CostValue",
                card.transform,
                22f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            costValueText.rectTransform,
            0.08f,
            0.48f,
            0.92f,
            0.78f);

        Auto(
            costValueText,
            12f,
            24f,
            TextWrappingModes.NoWrap);

        costValueText.color =
            new Color(
                1f,
                0.83f,
                0.28f,
                1f);

        balanceLabelText =
            CreateText(
                "BalanceLabel",
                card.transform,
                8f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            balanceLabelText.rectTransform,
            0.08f,
            0.29f,
            0.92f,
            0.45f);

        Auto(
            balanceLabelText,
            6f,
            8.5f,
            TextWrappingModes.NoWrap);

        balanceLabelText.color =
            new Color(
                0.68f,
                0.75f,
                0.84f,
                1f);

        balanceValueText =
            CreateText(
                "BalanceValue",
                card.transform,
                15f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            balanceValueText.rectTransform,
            0.08f,
            0.08f,
            0.92f,
            0.31f);

        Auto(
            balanceValueText,
            9f,
            17f,
            TextWrappingModes.NoWrap);
    }

    private void CreateStatusCard()
    {
        statusCard =
            CreatePanel(
                "StatusCard",
                polishRoot,
                new Color(
                    0.08f,
                    0.14f,
                    0.11f,
                    0.99f));

        Box(
            statusCard.GetComponent<
                RectTransform>(),
            0.10f,
            0.245f,
            0.90f,
            0.36f);

        statusCardImage =
            statusCard.GetComponent<
                Image>();

        statusHeadingText =
            CreateText(
                "StatusHeading",
                statusCard.transform,
                13f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            statusHeadingText.rectTransform,
            0.06f,
            0.54f,
            0.94f,
            0.90f);

        Auto(
            statusHeadingText,
            8f,
            14f,
            TextWrappingModes.NoWrap);

        statusReasonText =
            CreateText(
                "StatusReason",
                statusCard.transform,
                9f,
                FontStyles.Normal,
                TextAlignmentOptions.Center);

        Box(
            statusReasonText.rectTransform,
            0.06f,
            0.10f,
            0.94f,
            0.54f);

        Auto(
            statusReasonText,
            6.5f,
            10f,
            TextWrappingModes.Normal);

        statusReasonText.color =
            new Color(
                0.82f,
                0.86f,
                0.91f,
                1f);
    }

    private void ReparentAuthoritativeButtons()
    {
        if (polishRoot == null)
        {
            return;
        }

        Reparent(
            developButton,
            polishRoot);

        Reparent(
            skipButton,
            polishRoot);

        if (!authoritativeButtonsStyled)
        {
            StyleButton(
                developButton,
                new Color(
                    0.15f,
                    0.64f,
                    0.45f,
                    1f));

            StyleButton(
                skipButton,
                new Color(
                    0.46f,
                    0.49f,
                    0.57f,
                    1f));

            authoritativeButtonsStyled =
                true;
        }

        Box(
            developButton != null
                ? developButton.transform as
                    RectTransform
                : null,
            0.17f,
            0.075f,
            0.47f,
            0.18f);

        Box(
            skipButton != null
                ? skipButton.transform as
                    RectTransform
                : null,
            0.53f,
            0.075f,
            0.83f,
            0.18f);
    }

    private void RefreshAuthoritativeButtonVisuals()
    {
        ApplyButtonVisualState(
            developButton,
            new Color(
                0.15f,
                0.64f,
                0.45f,
                1f));

        ApplyButtonVisualState(
            skipButton,
            new Color(
                0.46f,
                0.49f,
                0.57f,
                1f));
    }

    private void RefreshPresentation()
    {
        if (tileResolutionManager == null ||
            developmentManager == null ||
            polishRoot == null)
        {
            return;
        }

        PlayerGameState player =
            tileResolutionManager
                .PendingDevelopmentPlayer;

        BoardTile tile =
            tileResolutionManager
                .PendingDevelopmentTile;

        if (player == null ||
            tile == null)
        {
            return;
        }

        int currentLevel =
            developmentManager
                .GetDevelopmentLevel(
                    tile);

        int maxLevel =
            developmentManager
                .MaximumDevelopmentLevel;

        int cost =
            developmentManager
                .GetDevelopmentCost(
                    tile);

        int currentRent =
            developmentManager
                .GetEffectiveRent(
                    tile);

        int nextRent =
            developmentManager
                .GetProjectedRentAtNextLevel(
                    tile);

        string groupName =
            developmentManager
                .GetGroupName(
                    tile);

        string groupLevels =
            developmentManager
                .GetGroupDevelopmentSummary(
                    tile);

        bool eligible =
            developmentManager
                .IsEligibleForDevelopment(
                    player,
                    tile);

        bool balanced =
            developmentManager
                .CanDevelopEvenly(
                    player,
                    tile);

        bool affordable =
            player.CurrentMoney >=
            cost;

        bool ready =
            eligible &&
            balanced &&
            affordable &&
            currentLevel < maxLevel;

        string blockReason =
            developmentManager
                .GetDevelopmentBlockReason(
                    player,
                    tile);

        if (string.IsNullOrWhiteSpace(
                blockReason) &&
            !ready)
        {
            blockReason =
                BuildFallbackBlockReason(
                    player,
                    tile,
                    currentLevel,
                    maxLevel);
        }

        ResolveFont(
            propertyNameText);

        propertyNameText.text =
            tile.DisplayName;

        ResolveFont(
            groupNameText);

        groupNameText.text =
            $"{GroupLabel()}  •  {groupName}";

        ResolveFont(
            playerNameText);

        playerNameText.text =
            AtlasBoardL.PlayerName(
                player);

        ResolveFont(
            playerBalanceText);

        playerBalanceText.text =
            $"{BalanceLabel()}  {player.CurrentMoney:N0} ₵";

        if (playerAccent != null)
        {
            playerAccent.color =
                player.UIColor;
        }

        ResolveFont(
            levelLabelText);

        levelLabelText.text =
            CurrentLevelLabel();

        ResolveFont(
            levelValueText);

        levelValueText.text =
            $"{currentLevel} / {maxLevel}";

        ResolveFont(
            groupLevelsLabelText);

        groupLevelsLabelText.text =
            GroupLevelsLabel();

        ResolveFont(
            groupLevelsValueText);

        groupLevelsValueText.text =
            groupLevels;

        ResolveFont(
            ruleBadgeText);

        ruleBadgeText.text =
            developmentManager
                .RequireBalancedGroupDevelopment
                ? BalancedRuleOnLabel()
                : BalancedRuleOffLabel();

        ResolveFont(
            rentLabelText);

        rentLabelText.text =
            RentProgressLabel();

        ResolveFont(
            currentRentText);

        currentRentText.text =
            $"{currentRent:N0} ₵";

        ResolveFont(
            nextRentText);

        nextRentText.text =
            $"{nextRent:N0} ₵";

        ResolveFont(
            costLabelText);

        costLabelText.text =
            DevelopmentCostLabel();

        ResolveFont(
            costValueText);

        costValueText.text =
            $"{cost:N0} ₵";

        ResolveFont(
            balanceLabelText);

        balanceLabelText.text =
            BalanceLabel();

        ResolveFont(
            balanceValueText);

        balanceValueText.text =
            $"{player.CurrentMoney:N0} ₵";

        balanceValueText.color =
            affordable
                ? new Color(
                    0.41f,
                    0.86f,
                    0.58f,
                    1f)
                : new Color(
                    0.95f,
                    0.42f,
                    0.42f,
                    1f);

        ApplyStatusPresentation(
            ready,
            blockReason);

        RefreshButtonLabels(
            cost);
    }

    private string BuildFallbackBlockReason(
        PlayerGameState player,
        BoardTile tile,
        int currentLevel,
        int maxLevel)
    {
        if (currentLevel >=
            maxLevel)
        {
            return MaxLevelReason();
        }

        if (!tile.IsOwned ||
            tile.OwnerPlayerIndex !=
                player.PlayerSlotIndex)
        {
            return OwnershipReason();
        }

        int groupIndex =
            developmentManager
                .GetGroupIndex(
                    tile);

        if (!developmentManager
                .HasCompleteGroup(
                    player.PlayerSlotIndex,
                    groupIndex))
        {
            return CompleteGroupReason();
        }

        if (player.CurrentMoney <
            developmentManager
                .GetDevelopmentCost(
                    tile))
        {
            return InsufficientBalanceReason();
        }

        return NotAvailableReason();
    }

    private void ApplyStatusPresentation(
        bool ready,
        string reason)
    {
        if (statusCardImage != null)
        {
            statusCardImage.color =
                ready
                    ? new Color(
                        0.07f,
                        0.19f,
                        0.13f,
                        0.99f)
                    : new Color(
                        0.20f,
                        0.10f,
                        0.11f,
                        0.99f);
        }

        ResolveFont(
            statusHeadingText);

        statusHeadingText.text =
            ready
                ? ReadyLabel()
                : BlockedLabel();

        statusHeadingText.color =
            ready
                ? new Color(
                    0.41f,
                    0.86f,
                    0.58f,
                    1f)
                : new Color(
                    0.95f,
                    0.42f,
                    0.42f,
                    1f);

        ResolveFont(
            statusReasonText);

        statusReasonText.text =
            ready
                ? ReadyReason()
                : reason;
    }

    private void RefreshButtonLabels(
        int cost)
    {
        SetButtonLabel(
            developButton,
            $"{DevelopLabel()}  •  {cost:N0} ₵");

        SetButtonLabel(
            skipButton,
            SkipLabel());

        RefreshAuthoritativeButtonVisuals();
    }

    private void HideLegacyInfo()
    {
        if (legacyInfoText == null)
        {
            return;
        }

        if (!legacyCaptured)
        {
            legacyInfoWasActive =
                legacyInfoText
                    .gameObject
                    .activeSelf;

            legacyCaptured =
                true;
        }

        legacyInfoText
            .gameObject
            .SetActive(
                false);
    }

    private void RestoreLegacyInfo()
    {
        if (!legacyCaptured ||
            legacyInfoText == null)
        {
            return;
        }

        legacyInfoText
            .gameObject
            .SetActive(
                legacyInfoWasActive);

        legacyCaptured =
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

    private void ResolveExistingPresentation()
    {
        propertyNameText =
            FindText(
                "PropertyHero/PropertyName");

        groupNameText =
            FindText(
                "PropertyHero/GroupName");

        playerNameText =
            FindText(
                "PropertyHero/PlayerName");

        playerBalanceText =
            FindText(
                "PropertyHero/PlayerBalance");

        playerAccent =
            FindImage(
                "PropertyHero/Accent");

        levelLabelText =
            FindText(
                "LevelCard/LevelLabel");

        levelValueText =
            FindText(
                "LevelCard/LevelValue");

        groupLevelsLabelText =
            FindText(
                "LevelCard/GroupLevelsLabel");

        groupLevelsValueText =
            FindText(
                "LevelCard/GroupLevelsValue");

        ruleBadgeText =
            FindText(
                "LevelCard/RuleBadge");

        rentLabelText =
            FindText(
                "RentCard/RentLabel");

        currentRentText =
            FindText(
                "RentCard/CurrentRent");

        rentArrowText =
            FindText(
                "RentCard/Arrow");

        nextRentText =
            FindText(
                "RentCard/NextRent");

        costLabelText =
            FindText(
                "CostCard/CostLabel");

        costValueText =
            FindText(
                "CostCard/CostValue");

        balanceLabelText =
            FindText(
                "CostCard/BalanceLabel");

        balanceValueText =
            FindText(
                "CostCard/BalanceValue");

        Transform status =
            polishRoot.Find(
                "StatusCard");

        statusCard =
            status != null
                ? status.gameObject
                : null;

        statusCardImage =
            statusCard != null
                ? statusCard.GetComponent<
                    Image>()
                : null;

        statusHeadingText =
            FindText(
                "StatusCard/StatusHeading");

        statusReasonText =
            FindText(
                "StatusCard/StatusReason");
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
            panel.GetComponent<
                Image>();

        image.color =
            color;

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
                    0.19f,
                    0.22f,
                    0.27f,
                    0.90f);
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

    private static string GroupLabel() =>
        AtlasBoardL.R(
            "GROUP",
            "GRUP",
            "GRUPO",
            "GROUPE",
            "GRUPPE",
            "그룹",
            "ГРУППА");

    private static string BalanceLabel() =>
        AtlasBoardL.R(
            "BALANCE",
            "BAKİYE",
            "SALDO",
            "SOLDE",
            "GUTHABEN",
            "잔액",
            "БАЛАНС");

    private static string CurrentLevelLabel() =>
        AtlasBoardL.R(
            "CURRENT LEVEL",
            "MEVCUT SEVİYE",
            "NIVEL ACTUAL",
            "NIVEAU ACTUEL",
            "AKTUELLE STUFE",
            "현재 레벨",
            "ТЕКУЩИЙ УРОВЕНЬ");

    private static string GroupLevelsLabel() =>
        AtlasBoardL.R(
            "GROUP LEVELS",
            "GRUP SEVİYELERİ",
            "NIVELES DEL GRUPO",
            "NIVEAUX DU GROUPE",
            "GRUPPENSTUFEN",
            "그룹 레벨",
            "УРОВНИ ГРУППЫ");

    private static string BalancedRuleOnLabel() =>
        AtlasBoardL.R(
            "BALANCED RULE • ON",
            "DENGELİ KURAL • AÇIK",
            "REGLA EQUILIBRADA • ACTIVA",
            "RÈGLE ÉQUILIBRÉE • ACTIVE",
            "AUSGLEICHSREGEL • AN",
            "균형 규칙 • 켜짐",
            "ПРАВИЛО БАЛАНСА • ВКЛ");

    private static string BalancedRuleOffLabel() =>
        AtlasBoardL.R(
            "BALANCED RULE • OFF",
            "DENGELİ KURAL • KAPALI",
            "REGLA EQUILIBRADA • INACTIVA",
            "RÈGLE ÉQUILIBRÉE • INACTIVE",
            "AUSGLEICHSREGEL • AUS",
            "균형 규칙 • 꺼짐",
            "ПРАВИЛО БАЛАНСА • ВЫКЛ");

    private static string RentProgressLabel() =>
        AtlasBoardL.R(
            "RENT PROGRESSION",
            "KİRA ARTIŞI",
            "PROGRESIÓN DE RENTA",
            "PROGRESSION DU LOYER",
            "MIETENTWICKLUNG",
            "임대료 변화",
            "РОСТ АРЕНДЫ");

    private static string CurrentLabel() =>
        AtlasBoardL.R(
            "CURRENT",
            "MEVCUT",
            "ACTUAL",
            "ACTUEL",
            "AKTUELL",
            "현재",
            "СЕЙЧАС");

    private static string NextLabel() =>
        AtlasBoardL.R(
            "NEXT LEVEL",
            "SONRAKİ SEVİYE",
            "SIGUIENTE NIVEL",
            "NIVEAU SUIVANT",
            "NÄCHSTE STUFE",
            "다음 레벨",
            "СЛЕД. УРОВЕНЬ");

    private static string DevelopmentCostLabel() =>
        AtlasBoardL.R(
            "DEVELOPMENT COST",
            "GELİŞTİRME MALİYETİ",
            "COSTE DE DESARROLLO",
            "COÛT DE DÉVELOPPEMENT",
            "AUSBAUKOSTEN",
            "개발 비용",
            "СТОИМОСТЬ РАЗВИТИЯ");

    private static string ReadyLabel() =>
        AtlasBoardL.R(
            "READY TO DEVELOP",
            "GELİŞTİRMEYE HAZIR",
            "LISTO PARA DESARROLLAR",
            "PRÊT À DÉVELOPPER",
            "AUSBAU BEREIT",
            "개발 가능",
            "ГОТОВО К РАЗВИТИЮ");

    private static string ReadyReason() =>
        AtlasBoardL.R(
            "This property meets the current development rules.",
            "Bu mülk mevcut geliştirme kurallarını karşılıyor.",
            "Esta propiedad cumple las reglas actuales de desarrollo.",
            "Cette propriété respecte les règles actuelles de développement.",
            "Dieses Grundstück erfüllt die aktuellen Ausbauregeln.",
            "이 부동산은 현재 개발 규칙을 충족합니다.",
            "Этот объект соответствует текущим правилам развития.");

    private static string BlockedLabel() =>
        AtlasBoardL.R(
            "DEVELOPMENT BLOCKED",
            "GELİŞTİRME ENGELLENDİ",
            "DESARROLLO BLOQUEADO",
            "DÉVELOPPEMENT BLOQUÉ",
            "AUSBAU BLOCKIERT",
            "개발 불가",
            "РАЗВИТИЕ ЗАБЛОКИРОВАНО");

    private static string MaxLevelReason() =>
        AtlasBoardL.R(
            "This property is already at the maximum development level.",
            "Bu mülk zaten maksimum geliştirme seviyesinde.",
            "Esta propiedad ya está en el nivel máximo de desarrollo.",
            "Cette propriété est déjà au niveau maximal de développement.",
            "Dieses Grundstück hat bereits die maximale Ausbaustufe.",
            "이 부동산은 이미 최대 개발 레벨입니다.",
            "Этот объект уже на максимальном уровне развития.");

    private static string OwnershipReason() =>
        AtlasBoardL.R(
            "You do not own this property.",
            "Bu mülk sana ait değil.",
            "No eres propietario de esta propiedad.",
            "Vous ne possédez pas cette propriété.",
            "Dieses Grundstück gehört dir nicht.",
            "이 부동산을 소유하고 있지 않습니다.",
            "Этот объект вам не принадлежит.");

    private static string CompleteGroupReason() =>
        AtlasBoardL.R(
            "Own the complete property group before developing.",
            "Geliştirmeden önce grubun tamamına sahip olmalısın.",
            "Debes poseer todo el grupo antes de desarrollar.",
            "Vous devez posséder tout le groupe avant de développer.",
            "Besitze zuerst die vollständige Gruppe.",
            "개발하려면 먼저 그룹 전체를 소유해야 합니다.",
            "Перед развитием нужно владеть всей группой.");

    private static string InsufficientBalanceReason() =>
        AtlasBoardL.R(
            "Not enough balance for this development.",
            "Bu geliştirme için yeterli bakiye yok.",
            "Saldo insuficiente para este desarrollo.",
            "Solde insuffisant pour ce développement.",
            "Nicht genug Guthaben für diesen Ausbau.",
            "개발에 필요한 잔액이 부족합니다.",
            "Недостаточно средств для развития.");

    private static string NotAvailableReason() =>
        AtlasBoardL.R(
            "Development is not currently available.",
            "Geliştirme şu anda kullanılamıyor.",
            "El desarrollo no está disponible actualmente.",
            "Le développement n’est pas disponible actuellement.",
            "Ausbau ist derzeit nicht verfügbar.",
            "현재 개발할 수 없습니다.",
            "Развитие сейчас недоступно.");

    private static string DevelopLabel() =>
        AtlasBoardL.R(
            "DEVELOP",
            "GELİŞTİR",
            "DESARROLLAR",
            "DÉVELOPPER",
            "AUSBAUEN",
            "개발",
            "РАЗВИТЬ");

    private static string SkipLabel() =>
        AtlasBoardL.R(
            "SKIP",
            "ATLA",
            "OMITIR",
            "PASSER",
            "ÜBERSPRINGEN",
            "건너뛰기",
            "ПРОПУСТИТЬ");
}
