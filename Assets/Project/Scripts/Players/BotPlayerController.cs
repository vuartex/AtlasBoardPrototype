using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerGameState))]
public class BotPlayerController : MonoBehaviour
{
    [Header("Bot Control")]
    [SerializeField]
    private bool botEnabled = true;

    [SerializeField, Min(0f)]
    private float rollDecisionDelay = 0.8f;

    [SerializeField, Min(0f)]
    private float purchaseDecisionDelay = 0.9f;

    [SerializeField, Min(0f)]
    private float auctionDecisionDelay = 0.8f;

    [SerializeField, Min(0f)]
    private float travelDecisionDelay = 1f;

    [SerializeField, Min(0f)]
    private float developmentDecisionDelay = 0.9f;

    [SerializeField, Min(0f)]
    private float resultContinueDelay = 1.25f;

    [SerializeField, Min(0f)]
    private float tradeResponseDelay = 1.1f;

    [SerializeField, Min(0f)]
    private float ownTradeDecisionDelay = 0.9f;

    [Header("Strategy Profiles")]
    [Tooltip(
        "Per-bot character. Safe/Aggressive/Adaptive/Balanced profiles " +
        "are created by the Economy Balance editor tool.")]
    [SerializeField]
    private BotPersonalityProfile personalityProfile;

    [Tooltip(
        "Normally resolved automatically from the active map.")]
    [SerializeField]
    private BoardEconomyProfile economyProfile;

    [Header("Trade Strategy")]
    [Tooltip(
        "The bot accepts when the value it receives is at " +
        "least this fraction of the value it gives.")]
    [SerializeField, Range(0.5f, 1.5f)]
    private float minimumTradeValueRatio = 0.95f;

    [Tooltip(
        "Extra value assigned to a property that would " +
        "complete one of the bot's groups.")]
    [SerializeField, Range(0f, 1f)]
    private float groupCompletionBonus = 0.35f;

    [Tooltip(
        "Extra value assigned to a property the bot would " +
        "give away when it currently belongs to a complete group.")]
    [SerializeField, Range(0f, 1f)]
    private float completeGroupProtectionPremium = 0.40f;

    [Tooltip(
        "Chance that the bot considers creating one trade " +
        "offer before rolling on its turn.")]
    [SerializeField, Range(0f, 1f)]
    private float ownTradeAttemptChance = 0.11f;

    [Tooltip(
        "Cash premium over list value when the requested " +
        "property would complete the bot's group.")]
    [SerializeField, Range(0f, 1f)]
    private float groupCompletionOfferPremium = 0.20f;

    [Tooltip(
        "Cash multiplier for a property that does not " +
        "complete a group but still has strategic value.")]
    [SerializeField, Range(0.5f, 1.5f)]
    private float generalPropertyOfferMultiplier = 1.00f;

    [Header("Purchase Strategy")]
    [SerializeField, Min(0)]
    private int minimumCashReserve = 250;

    [SerializeField, Range(0.1f, 1f)]
    private float maximumPurchaseCashRatio = 0.45f;

    [Header("Auction Strategy")]
    [SerializeField, Range(0.5f, 2f)]
    private float auctionValueMultiplier = 0.90f;

    [SerializeField, Min(0)]
    private int largeBidSafetyMargin = 30;

    [Header("Travel Strategy")]
    [Tooltip(
        "If travelling does not cross Start, this is the " +
        "chance that the bot still chooses to travel.")]
    [SerializeField, Range(0f, 1f)]
    private float travelChanceWithoutStartReward = 0.60f;

    [Header("Development Strategy")]
    [Tooltip(
        "The bot develops only if the cost is no more than " +
        "this fraction of its current cash.")]
    [SerializeField, Range(0.05f, 1f)]
    private float maximumDevelopmentCashRatio = 0.30f;

    [Header("References")]
    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private TileResolutionManager tileResolutionManager;

    [SerializeField]
    private AuctionManager auctionManager;

    [SerializeField]
    private PropertyDevelopmentManager
        propertyDevelopmentManager;

    [SerializeField]
    private EventCardManager eventCardManager;

    [SerializeField]
    private SpecialTileManager specialTileManager;

    [SerializeField]
    private TradeManager tradeManager;

    [SerializeField]
    private BoardPath boardPath;

    private PlayerGameState playerState;
    private Coroutine decisionRoutine;

    private int lastOwnTradeAttemptCompletedTurns = -1;

    public bool BotEnabled =>
        botEnabled;

    public BotPersonalityProfile PersonalityProfile =>
        personalityProfile;

    public string PersonalityId =>
        personalityProfile != null
            ? personalityProfile.PersonalityId
            : "legacy_fallback";

    public void SetPersonalityProfile(
        BotPersonalityProfile profile)
    {
        personalityProfile = profile;

        Debug.Log(
            $"{name} bot personality = " +
            $"{(personalityProfile != null ? personalityProfile.DisplayName : "Legacy Fallback")}.",
            this);
    }

    public void SetBotEnabled(
        bool enabledValue)
    {
        botEnabled = enabledValue;

        if (!botEnabled &&
            decisionRoutine != null)
        {
            StopCoroutine(decisionRoutine);
            decisionRoutine = null;
        }

        Debug.Log(
            $"{name} control mode changed to " +
            $"{(botEnabled ? "BOT" : "HUMAN")}.",
            this);
    }

    private void Awake()
    {
        playerState =
            GetComponent<PlayerGameState>();
    }

    private void Start()
    {
        EnsureReferences();
    }

    private void OnDisable()
    {
        if (decisionRoutine != null)
        {
            StopCoroutine(decisionRoutine);
            decisionRoutine = null;
        }
    }

    private void Update()
    {
        if (!botEnabled ||
            playerState == null ||
            decisionRoutine != null)
        {
            return;
        }

        EnsureReferences();

        if (TryStartTradeResponse())
        {
            return;
        }

        // A bankrupt bot may still need to close the result panel
        // that announced its bankruptcy.
        if (TryStartEventContinue())
        {
            return;
        }

        if (TryStartSpecialContinue())
        {
            return;
        }

        if (playerState.IsBankrupt)
        {
            return;
        }

        if (TryStartPurchaseDecision())
        {
            return;
        }

        if (TryStartAuctionDecision())
        {
            return;
        }

        if (TryStartTravelDecision())
        {
            return;
        }

        if (TryStartDevelopmentDecision())
        {
            return;
        }

        if (TryStartOwnTradeDecision())
        {
            return;
        }

        TryStartRollDecision();
    }

    private bool TryStartTradeResponse()
    {
        if (tradeManager == null ||
            !tradeManager.HasPendingOfferFor(
                playerState))
        {
            return false;
        }

        decisionRoutine =
            StartCoroutine(
                ResolveTradeResponseAfterDelay());

        return true;
    }

    private bool TryStartEventContinue()
    {
        if (eventCardManager == null ||
            !eventCardManager
                .HasPendingEventFor(playerState))
        {
            return false;
        }

        decisionRoutine =
            StartCoroutine(
                ContinueEventAfterDelay());

        return true;
    }

    private bool TryStartSpecialContinue()
    {
        if (specialTileManager == null ||
            !specialTileManager
                .HasPendingSpecialFor(playerState))
        {
            return false;
        }

        decisionRoutine =
            StartCoroutine(
                ContinueSpecialAfterDelay());

        return true;
    }

    private bool TryStartPurchaseDecision()
    {
        if (tileResolutionManager == null ||
            !tileResolutionManager
                .HasPendingPurchaseFor(playerState))
        {
            return false;
        }

        decisionRoutine =
            StartCoroutine(
                ResolvePurchaseAfterDelay());

        return true;
    }

    private bool TryStartAuctionDecision()
    {
        if (auctionManager == null ||
            !auctionManager.IsAuctionActive ||
            !auctionManager.IsCurrentBidder(
                playerState))
        {
            return false;
        }

        decisionRoutine =
            StartCoroutine(
                ResolveAuctionAfterDelay());

        return true;
    }

    private bool TryStartTravelDecision()
    {
        if (tileResolutionManager == null ||
            !tileResolutionManager
                .HasPendingTravelFor(playerState))
        {
            return false;
        }

        decisionRoutine =
            StartCoroutine(
                ResolveTravelAfterDelay());

        return true;
    }

    private bool TryStartDevelopmentDecision()
    {
        if (tileResolutionManager == null ||
            !tileResolutionManager
                .HasPendingDevelopmentFor(playerState))
        {
            return false;
        }

        decisionRoutine =
            StartCoroutine(
                ResolveDevelopmentAfterDelay());

        return true;
    }

    private bool TryStartOwnTradeDecision()
    {
        if (tradeManager == null ||
            turnManager == null ||
            !turnManager.IsPlayingPhase ||
            !tradeManager.IsTradeClosed ||
            !turnManager.CanPlayerRequestRoll(
                playerState))
        {
            return false;
        }

        int currentTurnToken =
            turnManager.CompletedTurns;

        if (lastOwnTradeAttemptCompletedTurns ==
            currentTurnToken)
        {
            return false;
        }

        lastOwnTradeAttemptCompletedTurns =
            currentTurnToken;

        if (Random.value >
            GetTradeAttemptChance())
        {
            return false;
        }

        if (!TryBuildOwnTradeOffer(
                out PlayerGameState targetPlayer,
                out BoardTile requestedProperty,
                out int cashOffer))
        {
            return false;
        }

        decisionRoutine =
            StartCoroutine(
                BeginOwnTradeAfterDelay(
                    targetPlayer,
                    requestedProperty,
                    cashOffer));

        return true;
    }

    private void TryStartRollDecision()
    {
        if (turnManager == null ||
            !turnManager.CanPlayerRequestRoll(
                playerState))
        {
            return;
        }

        decisionRoutine =
            StartCoroutine(
                RollAfterDelay());
    }

    private IEnumerator RollAfterDelay()
    {
        if (rollDecisionDelay > 0f)
        {
            yield return new WaitForSeconds(
                AtlasBoardUserSettingsRuntime.ScaleBotDelay(
                    rollDecisionDelay));
        }

        EnsureReferences();

        if (CanStillPlay() &&
            turnManager != null)
        {
            bool rolled =
                turnManager.TryRequestRoll(
                    playerState);

            if (rolled)
            {
                Debug.Log(
                    $"{playerState.DisplayName} [BOT] " +
                    "requested a dice roll.",
                    this);
            }
        }

        decisionRoutine = null;
    }

    private IEnumerator ResolvePurchaseAfterDelay()
    {
        if (purchaseDecisionDelay > 0f)
        {
            yield return new WaitForSeconds(
                AtlasBoardUserSettingsRuntime.ScaleBotDelay(
                    purchaseDecisionDelay));
        }

        EnsureReferences();

        if (!CanStillPlay() ||
            tileResolutionManager == null ||
            !tileResolutionManager
                .HasPendingPurchaseFor(playerState))
        {
            decisionRoutine = null;
            yield break;
        }

        BoardTile property =
            tileResolutionManager.PendingPurchaseTile;

        bool shouldBuy =
            ShouldBuyProperty(property);

        Debug.Log(
            $"{playerState.DisplayName} [BOT] purchase decision " +
            $"for {property?.DisplayName ?? "unknown"}: " +
            $"{(shouldBuy ? "BUY" : "PASS")}.",
            this);

        tileResolutionManager.TryResolveBotPurchase(
            playerState,
            shouldBuy);

        decisionRoutine = null;
    }

    private IEnumerator ResolveAuctionAfterDelay()
    {
        if (auctionDecisionDelay > 0f)
        {
            yield return new WaitForSeconds(
                AtlasBoardUserSettingsRuntime.ScaleBotDelay(
                    auctionDecisionDelay));
        }

        EnsureReferences();

        if (!CanStillPlay() ||
            auctionManager == null ||
            !auctionManager.IsAuctionActive ||
            !auctionManager.IsCurrentBidder(
                playerState))
        {
            decisionRoutine = null;
            yield break;
        }

        BotAuctionAction action =
            ChooseAuctionAction();

        Debug.Log(
            $"{playerState.DisplayName} [BOT] auction decision " +
            $"for {auctionManager.AuctionProperty?.DisplayName ?? "unknown"}: " +
            $"{action}.",
            this);

        auctionManager.TryResolveBotAction(
            playerState,
            action);

        decisionRoutine = null;
    }

    private IEnumerator ResolveTravelAfterDelay()
    {
        if (travelDecisionDelay > 0f)
        {
            yield return new WaitForSeconds(
                AtlasBoardUserSettingsRuntime.ScaleBotDelay(
                    travelDecisionDelay));
        }

        EnsureReferences();

        if (!CanStillPlay() ||
            tileResolutionManager == null ||
            !tileResolutionManager
                .HasPendingTravelFor(playerState))
        {
            decisionRoutine = null;
            yield break;
        }

        bool shouldTravel =
            ShouldTravel();

        Debug.Log(
            $"{playerState.DisplayName} [BOT] travel decision: " +
            $"{(shouldTravel ? "TRAVEL" : "STAY")}.",
            this);

        tileResolutionManager.TryResolveBotTravel(
            playerState,
            shouldTravel);

        decisionRoutine = null;
    }

    private IEnumerator ResolveDevelopmentAfterDelay()
    {
        if (developmentDecisionDelay > 0f)
        {
            yield return new WaitForSeconds(
                AtlasBoardUserSettingsRuntime.ScaleBotDelay(
                    developmentDecisionDelay));
        }

        EnsureReferences();

        if (!CanStillPlay() ||
            tileResolutionManager == null ||
            !tileResolutionManager
                .HasPendingDevelopmentFor(playerState))
        {
            decisionRoutine = null;
            yield break;
        }

        BoardTile property =
            tileResolutionManager.PendingDevelopmentTile;

        bool shouldDevelop =
            ShouldDevelop(property);

        Debug.Log(
            $"{playerState.DisplayName} [BOT] development decision " +
            $"for {property?.DisplayName ?? "unknown"}: " +
            $"{(shouldDevelop ? "DEVELOP" : "SKIP")}.",
            this);

        tileResolutionManager.TryResolveBotDevelopment(
            playerState,
            shouldDevelop);

        decisionRoutine = null;
    }

    private IEnumerator BeginOwnTradeAfterDelay(
        PlayerGameState targetPlayer,
        BoardTile requestedProperty,
        int cashOffer)
    {
        if (ownTradeDecisionDelay > 0f)
        {
            yield return new WaitForSeconds(
                AtlasBoardUserSettingsRuntime.ScaleBotDelay(
                    ownTradeDecisionDelay));
        }

        EnsureReferences();

        if (!CanStillPlay() ||
            tradeManager == null ||
            turnManager == null ||
            !tradeManager.IsTradeClosed ||
            !turnManager.CanPlayerRequestRoll(
                playerState))
        {
            decisionRoutine = null;
            yield break;
        }

        bool started =
            tradeManager.TryBeginBotOffer(
                playerState,
                targetPlayer,
                propertyOffered: null,
                cashOffered: cashOffer,
                propertyRequested:
                    requestedProperty,
                cashRequested: 0);

        if (started)
        {
            Debug.Log(
                $"{playerState.DisplayName} [BOT] offered " +
                $"{cashOffer} ₵ to {targetPlayer.DisplayName} " +
                $"for {requestedProperty.DisplayName}.",
                this);
        }

        decisionRoutine = null;
    }

    private IEnumerator ResolveTradeResponseAfterDelay()
    {
        if (tradeResponseDelay > 0f)
        {
            yield return new WaitForSeconds(
                AtlasBoardUserSettingsRuntime.ScaleBotDelay(
                    tradeResponseDelay));
        }

        EnsureReferences();

        if (tradeManager == null ||
            !tradeManager.HasPendingOfferFor(
                playerState) ||
            playerState == null ||
            playerState.IsBankrupt)
        {
            decisionRoutine = null;
            yield break;
        }

        int receivedValue =
            EvaluateIncomingTradeValue();

        int givenValue =
            EvaluateOutgoingTradeValue();

        bool cashReserveSafe =
            tradeManager.RequestedCash <= 0 ||
            playerState.CurrentMoney -
            tradeManager.RequestedCash >=
            GetPurchaseCashReserve(false);

        bool accept =
            cashReserveSafe &&
            receivedValue >=
            Mathf.RoundToInt(
                givenValue *
                GetMinimumTradeValueRatio());

        Debug.Log(
            $"{playerState.DisplayName} [BOT] trade response: " +
            $"{(accept ? "ACCEPT" : "REJECT")}. " +
            $"Receives value: {receivedValue}, " +
            $"gives value: {givenValue}, " +
            $"cash reserve safe: {cashReserveSafe}.",
            this);

        tradeManager.TryResolveBotResponse(
            playerState,
            accept);

        decisionRoutine = null;
    }

    private IEnumerator ContinueEventAfterDelay()
    {
        if (resultContinueDelay > 0f)
        {
            yield return new WaitForSeconds(
                AtlasBoardUserSettingsRuntime.ScaleBotDelay(
                    resultContinueDelay));
        }

        EnsureReferences();

        if (eventCardManager != null &&
            eventCardManager
                .HasPendingEventFor(playerState))
        {
            eventCardManager.TryResolveBotContinue(
                playerState);

            Debug.Log(
                $"{playerState.DisplayName} [BOT] " +
                "continued the event result.",
                this);
        }

        decisionRoutine = null;
    }

    private IEnumerator ContinueSpecialAfterDelay()
    {
        if (resultContinueDelay > 0f)
        {
            yield return new WaitForSeconds(
                AtlasBoardUserSettingsRuntime.ScaleBotDelay(
                    resultContinueDelay));
        }

        EnsureReferences();

        if (specialTileManager != null &&
            specialTileManager
                .HasPendingSpecialFor(playerState))
        {
            specialTileManager.TryResolveBotContinue(
                playerState);

            Debug.Log(
                $"{playerState.DisplayName} [BOT] " +
                "continued the special result.",
                this);
        }

        decisionRoutine = null;
    }

    private bool ShouldBuyProperty(
        BoardTile property)
    {
        if (property == null ||
            property.IsOwned ||
            !property.Purchasable)
        {
            return false;
        }

        int price =
            property.PurchasePrice;

        if (price <= 0 ||
            playerState.CurrentMoney < price)
        {
            return false;
        }

        bool completesGroup =
            WouldCompleteGroupWith(property);

        bool hasGroupInterest =
            completesGroup ||
            OwnsAnotherPropertyInGroup(property);

        int requiredReserve =
            GetPurchaseCashReserve(
                completesGroup);

        int moneyAfterPurchase =
            playerState.CurrentMoney - price;

        if (moneyAfterPurchase <
            requiredReserve)
        {
            return false;
        }

        BoardEconomyProfile economy =
            ResolveEconomyProfile();

        float maxRatio =
            economy != null
                ? economy.BotMaximumPurchaseCashRatio
                : maximumPurchaseCashRatio;

        if (economy != null)
        {
            if (completesGroup)
            {
                maxRatio +=
                    economy.BotGroupCompletionPurchaseBonus *
                    GetGroupCompletionFocus();
            }
            else if (hasGroupInterest)
            {
                maxRatio +=
                    economy.BotGroupInterestPurchaseBonus *
                    GetGroupCompletionFocus();
            }
        }

        maxRatio *=
            GetPurchaseWillingness();

        maxRatio =
            Mathf.Clamp(
                maxRatio,
                0.10f,
                0.95f);

        float priceToCashRatio =
            price /
            Mathf.Max(
                1f,
                playerState.CurrentMoney);

        return priceToCashRatio <=
               maxRatio;
    }

    private BotAuctionAction ChooseAuctionAction()
    {
        BoardTile property =
            auctionManager.AuctionProperty;

        if (property == null)
        {
            return BotAuctionAction.Pass;
        }

        bool completesGroup =
            WouldCompleteGroupWith(property);

        bool groupInterest =
            completesGroup ||
            OwnsAnotherPropertyInGroup(property);

        BoardEconomyProfile economy =
            ResolveEconomyProfile();

        int reserve =
            GetPurchaseCashReserve(
                completesGroup);

        int cashBudget =
            Mathf.Max(
                0,
                playerState.CurrentMoney -
                reserve);

        float valueMultiplier;

        if (economy != null)
        {
            valueMultiplier =
                completesGroup
                    ? economy
                        .BotAuctionGroupCompletionValueMultiplier
                    : groupInterest
                        ? economy
                            .BotAuctionGroupInterestValueMultiplier
                        : economy
                            .BotAuctionNormalValueMultiplier;
        }
        else
        {
            valueMultiplier =
                auctionValueMultiplier;
        }

        if (completesGroup)
        {
            valueMultiplier =
                1f +
                (valueMultiplier - 1f) *
                GetGroupCompletionFocus();
        }

        valueMultiplier *=
            GetAuctionWillingness();

        int valueBudget =
            Mathf.RoundToInt(
                property.PurchasePrice *
                Mathf.Max(
                    0.1f,
                    valueMultiplier));

        int maximumBid =
            Mathf.Min(
                cashBudget,
                valueBudget);

        int smallBid =
            auctionManager.NextSmallBidAmount;

        if (smallBid > maximumBid ||
            smallBid > playerState.CurrentMoney)
        {
            return BotAuctionAction.Pass;
        }

        int largeBid =
            auctionManager.NextLargeBidAmount;

        int safetyMargin =
            economy != null
                ? economy.BotLargeBidSafetyMargin
                : largeBidSafetyMargin;

        bool canBidLarge =
            largeBid <= maximumBid &&
            largeBid <= playerState.CurrentMoney &&
            maximumBid - largeBid >=
            safetyMargin;

        return canBidLarge
            ? BotAuctionAction.BidLarge
            : BotAuctionAction.BidSmall;
    }

    private bool ShouldTravel()
    {
        int targetIndex =
            tileResolutionManager
                .PendingTravelTargetIndex;

        PlayerPawnMover pawn =
            GetComponent<PlayerPawnMover>();

        if (pawn == null ||
            targetIndex < 0)
        {
            return false;
        }

        bool crossesStart =
            targetIndex <=
            pawn.CurrentTileIndex;

        if (crossesStart)
        {
            return true;
        }

        BoardEconomyProfile economy =
            ResolveEconomyProfile();

        float baseChance =
            economy != null
                ? economy
                    .BotTravelChanceWithoutStartReward
                : travelChanceWithoutStartReward;

        float chance =
            Mathf.Clamp01(
                baseChance *
                GetTravelWillingness());

        return Random.value <= chance;
    }

    private bool ShouldDevelop(
        BoardTile property)
    {
        if (property == null ||
            propertyDevelopmentManager == null ||
            !propertyDevelopmentManager
                .CanAffordDevelopment(
                    playerState,
                    property))
        {
            return false;
        }

        int cost =
            propertyDevelopmentManager
                .GetDevelopmentCost(property);

        int moneyAfterDevelopment =
            playerState.CurrentMoney - cost;

        int requiredReserve =
            GetDevelopmentCashReserve();

        if (moneyAfterDevelopment <
            requiredReserve)
        {
            return false;
        }

        BoardEconomyProfile economy =
            ResolveEconomyProfile();

        float maxRatio =
            economy != null
                ? economy
                    .BotMaximumDevelopmentCashRatio
                : maximumDevelopmentCashRatio;

        maxRatio *=
            GetDevelopmentWillingness();

        maxRatio =
            Mathf.Clamp(
                maxRatio,
                0.05f,
                0.90f);

        float costToCashRatio =
            cost /
            Mathf.Max(
                1f,
                playerState.CurrentMoney);

        return costToCashRatio <=
               maxRatio;
    }

    private bool TryBuildOwnTradeOffer(
        out PlayerGameState bestTarget,
        out BoardTile bestProperty,
        out int bestCashOffer)
    {
        bestTarget = null;
        bestProperty = null;
        bestCashOffer = 0;

        EnsureReferences();

        if (turnManager == null ||
            boardPath == null ||
            propertyDevelopmentManager == null)
        {
            return false;
        }

        int availableCash =
            Mathf.Max(
                0,
                playerState.CurrentMoney -
                GetPurchaseCashReserve(false));

        if (availableCash <= 0)
        {
            return false;
        }

        int bestScore =
            int.MinValue;

        var candidateTargets =
            turnManager.GetPlayersInTurnOrderFrom(
                playerState,
                includeReferencePlayer: false);

        foreach (PlayerGameState candidateTarget
                 in candidateTargets)
        {
            if (candidateTarget == null ||
                candidateTarget.IsBankrupt)
            {
                continue;
            }

            for (int tileIndex = 0;
                 tileIndex < boardPath.TileCount;
                 tileIndex++)
            {
                BoardTile property =
                    boardPath.GetTile(tileIndex);

                if (!IsTradablePropertyOwnedBy(
                        property,
                        candidateTarget))
                {
                    continue;
                }

                bool completesGroup =
                    WouldCompleteGroupWith(
                        property);

                bool hasGroupInterest =
                    completesGroup ||
                    OwnsAnotherPropertyInGroup(
                        property);

                float offerMultiplier =
                    GetOutgoingTradeOfferMultiplier(
                        completesGroup);

                int offer =
                    RoundCashOffer(
                        property.PurchasePrice *
                        offerMultiplier);

                if (offer <= 0 ||
                    offer > availableCash)
                {
                    continue;
                }

                // Avoid trying to buy a completed monopoly away
                // from another player with a basic cash-only offer.
                bool targetOwnsCompleteGroup =
                    propertyDevelopmentManager
                        .HasCompleteGroup(
                            candidateTarget.PlayerSlotIndex,
                            propertyDevelopmentManager
                                .GetGroupIndex(property));

                if (targetOwnsCompleteGroup)
                {
                    continue;
                }

                int score = 0;

                if (completesGroup)
                {
                    score += 10000;
                }
                else if (hasGroupInterest)
                {
                    score += 5000;
                }
                else
                {
                    score += 1000;
                }

                score += property.PurchasePrice;
                score -= offer / 10;

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestTarget = candidateTarget;
                bestProperty = property;
                bestCashOffer = offer;
            }
        }

        return bestTarget != null &&
               bestProperty != null &&
               bestCashOffer > 0;
    }

    private bool IsTradablePropertyOwnedBy(
        BoardTile property,
        PlayerGameState owner)
    {
        if (property == null ||
            owner == null ||
            property.TileType != TileType.City ||
            !property.IsOwned ||
            property.OwnerPlayerIndex !=
            owner.PlayerSlotIndex)
        {
            return false;
        }

        int developmentLevel =
            propertyDevelopmentManager != null
                ? propertyDevelopmentManager
                    .GetDevelopmentLevel(property)
                : 0;

        return developmentLevel == 0;
    }

    private bool OwnsAnotherPropertyInGroup(
        BoardTile candidateProperty)
    {
        if (candidateProperty == null ||
            boardPath == null ||
            propertyDevelopmentManager == null)
        {
            return false;
        }

        int groupIndex =
            propertyDevelopmentManager
                .GetGroupIndex(candidateProperty);

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile == null ||
                tile == candidateProperty ||
                tile.TileType != TileType.City ||
                propertyDevelopmentManager
                    .GetGroupIndex(tile) !=
                groupIndex)
            {
                continue;
            }

            if (tile.IsOwned &&
                tile.OwnerPlayerIndex ==
                playerState.PlayerSlotIndex)
            {
                return true;
            }
        }

        return false;
    }

    private int RoundCashOffer(
        float rawOffer)
    {
        return Mathf.Max(
            10,
            Mathf.RoundToInt(
                rawOffer / 10f) * 10);
    }

    private int EvaluateIncomingTradeValue()
    {
        if (tradeManager == null)
        {
            return 0;
        }

        int value =
            Mathf.Max(
                0,
                tradeManager.OfferedCash);

        BoardTile property =
            tradeManager.OfferedProperty;

        if (property != null)
        {
            float propertyValue =
                property.PurchasePrice;

            if (WouldCompleteGroupWith(property))
            {
                BoardEconomyProfile economy =
                    ResolveEconomyProfile();

                float multiplier =
                    economy != null
                        ? economy
                            .BotIncomingGroupCompletionValueMultiplier
                        : 1f + groupCompletionBonus;

                propertyValue *=
                    1f +
                    (multiplier - 1f) *
                    GetGroupCompletionFocus();
            }

            value +=
                Mathf.RoundToInt(
                    propertyValue);
        }

        return value;
    }

    private int EvaluateOutgoingTradeValue()
    {
        if (tradeManager == null)
        {
            return 0;
        }

        int value =
            Mathf.Max(
                0,
                tradeManager.RequestedCash);

        BoardTile property =
            tradeManager.RequestedProperty;

        if (property != null)
        {
            float propertyValue =
                property.PurchasePrice;

            BoardEconomyProfile economy =
                ResolveEconomyProfile();

            if (propertyDevelopmentManager != null &&
                propertyDevelopmentManager
                    .HasCompleteGroup(
                        playerState.PlayerSlotIndex,
                        propertyDevelopmentManager
                            .GetGroupIndex(property)))
            {
                float protectionMultiplier =
                    economy != null
                        ? economy
                            .BotCompleteGroupProtectionValueMultiplier
                        : 1f +
                          completeGroupProtectionPremium;

                propertyValue *=
                    protectionMultiplier;
            }

            PlayerGameState receivingPlayer =
                tradeManager.TradeInitiator;

            if (receivingPlayer != null &&
                WouldCompleteGroupFor(
                    receivingPlayer,
                    property))
            {
                float opponentCompletionMultiplier =
                    economy != null
                        ? economy
                            .BotOpponentGroupCompletionProtectionValueMultiplier
                        : 1.30f;

                propertyValue *=
                    1f +
                    (opponentCompletionMultiplier - 1f) *
                    GetGroupCompletionFocus();
            }

            value +=
                Mathf.RoundToInt(
                    propertyValue);
        }

        return value;
    }

    private bool WouldCompleteGroupWith(
        BoardTile candidateProperty)
    {
        return WouldCompleteGroupFor(
            playerState,
            candidateProperty);
    }

    private bool WouldCompleteGroupFor(
        PlayerGameState player,
        BoardTile candidateProperty)
    {
        if (player == null ||
            candidateProperty == null ||
            propertyDevelopmentManager == null)
        {
            return false;
        }

        EnsureReferences();

        if (boardPath == null)
        {
            return false;
        }

        int groupIndex =
            propertyDevelopmentManager
                .GetGroupIndex(
                    candidateProperty);

        int groupCityCount = 0;

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile == null ||
                tile.TileType != TileType.City ||
                propertyDevelopmentManager
                    .GetGroupIndex(tile) !=
                groupIndex)
            {
                continue;
            }

            groupCityCount++;

            if (tile == candidateProperty)
            {
                continue;
            }

            if (!tile.IsOwned ||
                tile.OwnerPlayerIndex !=
                player.PlayerSlotIndex)
            {
                return false;
            }
        }

        return groupCityCount >= 2;
    }

    private BoardEconomyProfile ResolveEconomyProfile()
    {
        if (economyProfile != null)
        {
            return economyProfile;
        }

        BoardGenerator generator =
            FindAnyObjectByType<BoardGenerator>();

        if (generator != null)
        {
            economyProfile =
                generator.ActiveEconomyProfile;
        }

        return economyProfile;
    }

    private float GetAdaptiveAggressionFactor()
    {
        if (personalityProfile == null ||
            personalityProfile.AdaptiveStrength <= 0f ||
            playerState == null)
        {
            return 1f;
        }

        BoardEconomyProfile economy =
            ResolveEconomyProfile();

        int startingCash =
            economy != null
                ? economy.StartingMoney
                : 1500;

        float factor = 1f;

        float cashRatio =
            playerState.CurrentMoney /
            Mathf.Max(
                1f,
                startingCash);

        if (cashRatio < 0.45f)
        {
            factor *= 0.82f;
        }
        else if (cashRatio > 1.15f)
        {
            factor *= 1.10f;
        }

        float opponentAverage =
            GetOpponentAverageNetWorth();

        if (opponentAverage > 0f)
        {
            float ownNetWorth =
                GetApproximateNetWorth(
                    playerState);

            if (ownNetWorth <
                opponentAverage * 0.85f)
            {
                factor *= 1.12f;
            }
            else if (ownNetWorth >
                     opponentAverage * 1.20f)
            {
                factor *= 0.90f;
            }
        }

        return Mathf.Lerp(
            1f,
            Mathf.Clamp(
                factor,
                0.70f,
                1.30f),
            personalityProfile.AdaptiveStrength);
    }

    private float GetOpponentAverageNetWorth()
    {
        if (turnManager == null ||
            playerState == null)
        {
            return 0f;
        }

        var opponents =
            turnManager.GetPlayersInTurnOrderFrom(
                playerState,
                includeReferencePlayer: false);

        if (opponents == null ||
            opponents.Count == 0)
        {
            return 0f;
        }

        float total = 0f;
        int count = 0;

        foreach (PlayerGameState opponent
                 in opponents)
        {
            if (opponent == null ||
                opponent.IsBankrupt)
            {
                continue;
            }

            total +=
                GetApproximateNetWorth(
                    opponent);

            count++;
        }

        return count > 0
            ? total / count
            : 0f;
    }

    private float GetApproximateNetWorth(
        PlayerGameState player)
    {
        if (player == null)
        {
            return 0f;
        }

        float total =
            player.CurrentMoney;

        if (boardPath != null)
        {
            for (int tileIndex = 0;
                 tileIndex < boardPath.TileCount;
                 tileIndex++)
            {
                BoardTile tile =
                    boardPath.GetTile(tileIndex);

                if (tile == null ||
                    !tile.IsOwned ||
                    tile.OwnerPlayerIndex !=
                    player.PlayerSlotIndex)
                {
                    continue;
                }

                total +=
                    tile.PurchasePrice;
            }
        }

        if (propertyDevelopmentManager != null)
        {
            total +=
                propertyDevelopmentManager
                    .GetDevelopmentInvestmentValue(
                        player.PlayerSlotIndex);
        }

        return total;
    }

    private float GetCashReserveMultiplier()
    {
        float personalityMultiplier =
            personalityProfile != null
                ? personalityProfile
                    .CashReserveMultiplier
                : 1f;

        float adaptive =
            GetAdaptiveAggressionFactor();

        return Mathf.Clamp(
            personalityMultiplier /
            Mathf.Max(0.5f, adaptive),
            0.40f,
            1.80f);
    }

    private int GetPurchaseCashReserve(
        bool groupCompletion)
    {
        BoardEconomyProfile economy =
            ResolveEconomyProfile();

        int baseReserve;

        if (economy != null)
        {
            baseReserve =
                groupCompletion
                    ? economy
                        .BotGroupCompletionCashReserve
                    : economy
                        .BotSafeCashReserve;
        }
        else
        {
            baseReserve =
                minimumCashReserve;
        }

        return Mathf.Max(
            0,
            Mathf.RoundToInt(
                baseReserve *
                GetCashReserveMultiplier()));
    }

    private int GetDevelopmentCashReserve()
    {
        BoardEconomyProfile economy =
            ResolveEconomyProfile();

        int baseReserve =
            economy != null
                ? economy
                    .BotDevelopmentCashReserve
                : minimumCashReserve;

        float developmentWillingness =
            GetDevelopmentWillingness();

        return Mathf.Max(
            0,
            Mathf.RoundToInt(
                baseReserve *
                GetCashReserveMultiplier() /
                Mathf.Max(
                    0.5f,
                    developmentWillingness)));
    }

    private float GetPurchaseWillingness()
    {
        float personality =
            personalityProfile != null
                ? personalityProfile
                    .PurchaseWillingness
                : 1f;

        return Mathf.Clamp(
            personality *
            GetAdaptiveAggressionFactor(),
            0.50f,
            1.50f);
    }

    private float GetAuctionWillingness()
    {
        float personality =
            personalityProfile != null
                ? personalityProfile
                    .AuctionWillingness
                : 1f;

        return Mathf.Clamp(
            personality *
            GetAdaptiveAggressionFactor(),
            0.50f,
            1.60f);
    }

    private float GetDevelopmentWillingness()
    {
        float personality =
            personalityProfile != null
                ? personalityProfile
                    .DevelopmentWillingness
                : 1f;

        return Mathf.Clamp(
            personality *
            GetAdaptiveAggressionFactor(),
            0.50f,
            1.60f);
    }

    private float GetTradeAttemptChance()
    {
        BoardEconomyProfile economy =
            ResolveEconomyProfile();

        float baseChance =
            economy != null
                ? economy.BotTradeAttemptChance
                : ownTradeAttemptChance;

        float personality =
            personalityProfile != null
                ? personalityProfile
                    .TradeFrequencyMultiplier
                : 1f;

        return Mathf.Clamp01(
            baseChance *
            personality *
            GetAdaptiveAggressionFactor());
    }

    private float GetMinimumTradeValueRatio()
    {
        BoardEconomyProfile economy =
            ResolveEconomyProfile();

        float baseRatio =
            economy != null
                ? economy.BotMinimumTradeValueRatio
                : minimumTradeValueRatio;

        float personality =
            personalityProfile != null
                ? personalityProfile
                    .TradeAcceptanceRatioMultiplier
                : 1f;

        float adaptive =
            GetAdaptiveAggressionFactor();

        return Mathf.Clamp(
            baseRatio *
            personality /
            Mathf.Sqrt(
                Mathf.Max(0.5f, adaptive)),
            0.65f,
            1.40f);
    }

    private float GetGroupCompletionFocus()
    {
        return personalityProfile != null
            ? personalityProfile.GroupCompletionFocus
            : 1f;
    }

    private float GetOutgoingTradeOfferMultiplier(
        bool completesGroup)
    {
        BoardEconomyProfile economy =
            ResolveEconomyProfile();

        float baseMultiplier;

        if (economy != null)
        {
            baseMultiplier =
                completesGroup
                    ? economy
                        .BotGroupCompletionOfferMultiplier
                    : economy
                        .BotGeneralPropertyOfferMultiplier;
        }
        else
        {
            baseMultiplier =
                completesGroup
                    ? 1f +
                      groupCompletionOfferPremium
                    : generalPropertyOfferMultiplier;
        }

        if (completesGroup)
        {
            baseMultiplier =
                1f +
                (baseMultiplier - 1f) *
                GetGroupCompletionFocus();
        }

        return Mathf.Clamp(
            baseMultiplier *
            Mathf.Lerp(
                1f,
                GetPurchaseWillingness(),
                0.35f),
            0.50f,
            1.75f);
    }

    private float GetTravelWillingness()
    {
        float personality =
            personalityProfile != null
                ? personalityProfile
                    .TravelWillingness
                : 1f;

        return Mathf.Clamp(
            personality *
            Mathf.Lerp(
                1f,
                GetAdaptiveAggressionFactor(),
                0.35f),
            0.50f,
            1.50f);
    }

    private bool CanStillPlay()
    {
        return botEnabled &&
               playerState != null &&
               !playerState.IsBankrupt;
    }

    private void EnsureReferences()
    {
        if (turnManager == null)
        {
            turnManager =
                FindAnyObjectByType<TurnManager>();
        }

        if (tileResolutionManager == null)
        {
            tileResolutionManager =
                FindAnyObjectByType<
                    TileResolutionManager>();
        }

        if (auctionManager == null)
        {
            auctionManager =
                FindAnyObjectByType<AuctionManager>();
        }

        if (propertyDevelopmentManager == null)
        {
            propertyDevelopmentManager =
                FindAnyObjectByType<
                    PropertyDevelopmentManager>();
        }

        if (eventCardManager == null)
        {
            eventCardManager =
                FindAnyObjectByType<
                    EventCardManager>();
        }

        if (specialTileManager == null)
        {
            specialTileManager =
                FindAnyObjectByType<
                    SpecialTileManager>();
        }

        if (tradeManager == null)
        {
            tradeManager =
                FindAnyObjectByType<TradeManager>();
        }

        if (boardPath == null)
        {
            boardPath =
                FindAnyObjectByType<BoardPath>();
        }
    }
}
