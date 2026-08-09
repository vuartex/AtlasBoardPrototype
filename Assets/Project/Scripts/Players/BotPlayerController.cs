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
    private float ownTradeAttemptChance = 0.35f;

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
    private int minimumCashReserve = 350;

    [SerializeField, Range(0.1f, 1f)]
    private float maximumPurchaseCashRatio = 0.45f;

    [Header("Auction Strategy")]
    [SerializeField, Range(0.5f, 2f)]
    private float auctionValueMultiplier = 1.10f;

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
            ownTradeAttemptChance)
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
                rollDecisionDelay);
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
                purchaseDecisionDelay);
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
                auctionDecisionDelay);
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
                travelDecisionDelay);
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
                developmentDecisionDelay);
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
                ownTradeDecisionDelay);
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
                tradeResponseDelay);
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
            minimumCashReserve;

        bool accept =
            cashReserveSafe &&
            receivedValue >=
            Mathf.RoundToInt(
                givenValue *
                minimumTradeValueRatio);

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
                resultContinueDelay);
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
                resultContinueDelay);
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

        int moneyAfterPurchase =
            playerState.CurrentMoney - price;

        if (moneyAfterPurchase <
            minimumCashReserve)
        {
            return false;
        }

        float priceToCashRatio =
            price /
            Mathf.Max(
                1f,
                playerState.CurrentMoney);

        return priceToCashRatio <=
               maximumPurchaseCashRatio;
    }

    private BotAuctionAction ChooseAuctionAction()
    {
        BoardTile property =
            auctionManager.AuctionProperty;

        if (property == null)
        {
            return BotAuctionAction.Pass;
        }

        int cashBudget =
            Mathf.Max(
                0,
                playerState.CurrentMoney -
                minimumCashReserve);

        int valueBudget =
            Mathf.RoundToInt(
                property.PurchasePrice *
                auctionValueMultiplier);

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

        bool canBidLarge =
            largeBid <= maximumBid &&
            largeBid <= playerState.CurrentMoney &&
            maximumBid - largeBid >=
            largeBidSafetyMargin;

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

        return Random.value <=
               travelChanceWithoutStartReward;
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

        if (moneyAfterDevelopment <
            minimumCashReserve)
        {
            return false;
        }

        float costToCashRatio =
            cost /
            Mathf.Max(
                1f,
                playerState.CurrentMoney);

        return costToCashRatio <=
               maximumDevelopmentCashRatio;
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
                minimumCashReserve);

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
                    completesGroup
                        ? 1f +
                          groupCompletionOfferPremium
                        : generalPropertyOfferMultiplier;

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
                propertyValue *=
                    1f + groupCompletionBonus;
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

            if (propertyDevelopmentManager != null &&
                propertyDevelopmentManager
                    .HasCompleteGroup(
                        playerState.PlayerSlotIndex,
                        propertyDevelopmentManager
                            .GetGroupIndex(property)))
            {
                propertyValue *=
                    1f +
                    completeGroupProtectionPremium;
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
        if (candidateProperty == null ||
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
                playerState.PlayerSlotIndex)
            {
                return false;
            }
        }

        return groupCityCount >= 2;
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
