using UnityEngine;

public class AtlasBoardHumanDecisionTimeoutController : MonoBehaviour
{
    private enum DecisionKind
    {
        None,
        TripleDoubleContinue,
        EventContinue,
        SpecialContinue,
        TradeResponse,
        Purchase,
        Travel,
        Development,
        Auction,
        TradeSetup
    }

    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private TileResolutionManager tileResolutionManager;

    [SerializeField]
    private TradeManager tradeManager;

    [SerializeField]
    private AuctionManager auctionManager;

    [SerializeField]
    private EventCardManager eventCardManager;

    [SerializeField]
    private SpecialTileManager specialTileManager;

    [SerializeField, Min(1f)]
    private float decisionTimeoutSeconds =
        AtlasOnlineDefaults.HumanRollTimeoutSeconds;

    [Header("Trade Grace Period")]
    [Tooltip(
        "Trade setup and incoming trade responses get a longer online grace " +
        "period so players have time to inspect properties and cash values.")]
    [SerializeField, Min(1f)]
    private float tradeDecisionTimeoutSeconds = 25f;

    private DecisionKind activeDecision =
        DecisionKind.None;

    private PlayerGameState activePlayer;
    private float elapsedSeconds;

    public bool IsDecisionCountdownRunning =>
        activeDecision != DecisionKind.None &&
        activePlayer != null;

    public float RemainingSeconds =>
        Mathf.Max(
            0f,
            ActiveTimeoutSeconds -
            elapsedSeconds);

    private float ActiveTimeoutSeconds =>
        activeDecision == DecisionKind.TradeResponse ||
        activeDecision == DecisionKind.TradeSetup
            ? Mathf.Max(1f, tradeDecisionTimeoutSeconds)
            : Mathf.Max(1f, decisionTimeoutSeconds);

    private void Awake()
    {
        EnsureReferences();
    }

    private void Update()
    {
        EnsureReferences();

        ResolvePendingDecision(
            out DecisionKind decision,
            out PlayerGameState player);

        if (decision == DecisionKind.None ||
            player == null ||
            IsBotControlled(player))
        {
            ResetWindow();
            return;
        }

        if (decision != activeDecision ||
            !IsSamePlayer(
                player,
                activePlayer))
        {
            activeDecision = decision;
            activePlayer = player;
            elapsedSeconds = 0f;
        }

        elapsedSeconds +=
            Time.deltaTime;

        if (elapsedSeconds <
            ActiveTimeoutSeconds)
        {
            return;
        }

        DecisionKind timedOutDecision =
            activeDecision;

        PlayerGameState timedOutPlayer =
            activePlayer;

        ResetWindow();

        if (!ResolveTimedOutDecision(
                timedOutDecision,
                timedOutPlayer))
        {
            return;
        }

        Debug.Log(
            $"{timedOutPlayer.DisplayName} [Slot " +
            $"{timedOutPlayer.PlayerSlotIndex}] did not respond " +
            $"to {Describe(timedOutDecision)} within " +
            $"{GetTimeoutSeconds(timedOutDecision):0.#} seconds. " +
            $"Atlas Board applied the safe AFK default: " +
            $"{DescribeSafeAction(timedOutDecision)}.",
            this);
    }

    private float GetTimeoutSeconds(
        DecisionKind decision)
    {
        return decision == DecisionKind.TradeResponse ||
               decision == DecisionKind.TradeSetup
            ? Mathf.Max(1f, tradeDecisionTimeoutSeconds)
            : Mathf.Max(1f, decisionTimeoutSeconds);
    }

    private void ResolvePendingDecision(
        out DecisionKind decision,
        out PlayerGameState player)
    {
        decision = DecisionKind.None;
        player = null;

        if (turnManager == null ||
            !turnManager.IsMatchStarted)
        {
            return;
        }

        PlayerGameState current =
            turnManager.CurrentPlayerState;

        // Acknowledgement-only tablet. No economic choice is made.
        if (current != null &&
            turnManager
                .HasPendingTripleDoublePenaltyFor(
                    current))
        {
            decision =
                DecisionKind.TripleDoubleContinue;

            player = current;
            return;
        }

        // Event timer starts only after the card effect itself is fully
        // executed. Movement/effect animation time is never charged.
        if (current != null &&
            eventCardManager != null &&
            CanContinueEventSafely(
                current))
        {
            decision =
                DecisionKind.EventContinue;

            player = current;
            return;
        }

        if (current != null &&
            specialTileManager != null &&
            specialTileManager
                .HasPendingSpecialFor(
                    current))
        {
            decision =
                DecisionKind.SpecialContinue;

            player = current;
            return;
        }

        // Incoming trade response may target a player who is not the
        // current turn owner, so use TradeTarget instead of current.
        if (tradeManager != null &&
            tradeManager.IsAwaitingResponse &&
            tradeManager.TradeTarget != null)
        {
            decision =
                DecisionKind.TradeResponse;

            player =
                tradeManager.TradeTarget;

            return;
        }

        if (tileResolutionManager != null &&
            tileResolutionManager.PendingPurchasePlayer != null &&
            tileResolutionManager.HasPendingPurchaseFor(
                tileResolutionManager.PendingPurchasePlayer))
        {
            decision =
                DecisionKind.Purchase;

            player =
                tileResolutionManager.PendingPurchasePlayer;

            return;
        }

        if (tileResolutionManager != null &&
            tileResolutionManager.PendingTravelPlayer != null &&
            tileResolutionManager.HasPendingTravelFor(
                tileResolutionManager.PendingTravelPlayer))
        {
            decision =
                DecisionKind.Travel;

            player =
                tileResolutionManager.PendingTravelPlayer;

            return;
        }

        if (tileResolutionManager != null &&
            tileResolutionManager.PendingDevelopmentPlayer != null &&
            tileResolutionManager.HasPendingDevelopmentFor(
                tileResolutionManager.PendingDevelopmentPlayer))
        {
            decision =
                DecisionKind.Development;

            player =
                tileResolutionManager.PendingDevelopmentPlayer;

            return;
        }

        if (auctionManager != null &&
            auctionManager.IsAuctionActive &&
            auctionManager.CurrentBidder != null)
        {
            decision =
                DecisionKind.Auction;

            player =
                auctionManager.CurrentBidder;

            return;
        }

        // A Human may open the trade-builder before rolling and then
        // walk away. Cancel it after the same grace period so the turn
        // cannot be held forever.
        if (tradeManager != null &&
            !tradeManager.IsTradeClosed &&
            !tradeManager.IsAwaitingResponse &&
            tradeManager.TradeInitiator != null)
        {
            decision =
                DecisionKind.TradeSetup;

            player =
                tradeManager.TradeInitiator;
        }
    }

    private bool ResolveTimedOutDecision(
        DecisionKind decision,
        PlayerGameState player)
    {
        if (player == null ||
            IsBotControlled(player))
        {
            return false;
        }

        switch (decision)
        {
            case DecisionKind.TripleDoubleContinue:
                if (turnManager == null ||
                    !turnManager
                        .HasPendingTripleDoublePenaltyFor(
                            player))
                {
                    return false;
                }

                turnManager
                    .ContinueTripleDoublePenalty();

                return true;

            case DecisionKind.EventContinue:
                if (eventCardManager == null ||
                    !CanContinueEventSafely(
                        player))
                {
                    return false;
                }

                eventCardManager
                    .ContinueAfterEvent();

                // ContinueAfterEvent is intentionally a no-op until the
                // EventCardManager has finished applying its effect. If an
                // unexpected timing edge case occurs, do not claim success;
                // the controller will simply open a fresh timeout window.
                return !eventCardManager
                    .HasPendingEventFor(
                        player);

            case DecisionKind.SpecialContinue:
                if (specialTileManager == null ||
                    !specialTileManager
                        .HasPendingSpecialFor(
                            player))
                {
                    return false;
                }

                specialTileManager
                    .ContinueAfterSpecialTile();

                return true;

            case DecisionKind.TradeResponse:
                if (tradeManager == null ||
                    !tradeManager
                        .HasPendingOfferFor(
                            player))
                {
                    return false;
                }

                // AFK never accepts an economic transfer.
                tradeManager
                    .RejectTradeOffer();

                return true;

            case DecisionKind.Purchase:
                if (tileResolutionManager == null ||
                    !tileResolutionManager
                        .HasPendingPurchaseFor(
                            player))
                {
                    return false;
                }

                // AFK never spends money or acquires property.
                tileResolutionManager
                    .SkipPendingTile();

                return true;

            case DecisionKind.Travel:
                if (tileResolutionManager == null ||
                    !tileResolutionManager
                        .HasPendingTravelFor(
                            player))
                {
                    return false;
                }

                // AFK never spends travel money or chooses movement.
                tileResolutionManager
                    .StayOnTravelTile();

                return true;

            case DecisionKind.Development:
                if (tileResolutionManager == null ||
                    !tileResolutionManager
                        .HasPendingDevelopmentFor(
                            player))
                {
                    return false;
                }

                // AFK never spends money on development.
                tileResolutionManager
                    .SkipPendingDevelopment();

                return true;

            case DecisionKind.Auction:
                if (auctionManager == null ||
                    !auctionManager.IsAuctionActive ||
                    !auctionManager
                        .IsCurrentBidder(
                            player))
                {
                    return false;
                }

                // AFK never bids.
                auctionManager
                    .PassCurrentBidder();

                return true;

            case DecisionKind.TradeSetup:
                if (tradeManager == null ||
                    tradeManager.IsTradeClosed ||
                    tradeManager.IsAwaitingResponse ||
                    !IsSamePlayer(
                        tradeManager.TradeInitiator,
                        player))
                {
                    return false;
                }

                tradeManager
                    .CancelTrade();

                return true;

            default:
                return false;
        }
    }

    private bool CanContinueEventSafely(
        PlayerGameState player)
    {
        if (eventCardManager == null ||
            player == null ||
            !eventCardManager
                .HasPendingEventFor(
                    player))
        {
            return false;
        }

        // Atlas Board's current Event Deck effects are synchronous money /
        // skip-turn changes or pawn movement effects. For movement cards,
        // wait until the pawn has fully stopped before starting the human
        // acknowledgement timeout. This keeps animation time out of the
        // player's decision window without requiring any new API on the
        // long-standing EventCardManager class.
        PlayerPawnMover pawn =
            player.GetComponent<
                PlayerPawnMover>();

        return pawn == null ||
               !pawn.IsMoving;
    }

    private bool IsBotControlled(
        PlayerGameState player)
    {
        return turnManager != null &&
               turnManager.IsPlayerBot(
                   player);
    }

    private static bool IsSamePlayer(
        PlayerGameState first,
        PlayerGameState second)
    {
        if (first == null ||
            second == null)
        {
            return false;
        }

        return first == second ||
               first.PlayerSlotIndex ==
               second.PlayerSlotIndex;
    }

    private static string Describe(
        DecisionKind decision)
    {
        switch (decision)
        {
            case DecisionKind.TripleDoubleContinue:
                return "triple-double acknowledgement";

            case DecisionKind.EventContinue:
                return "event acknowledgement";

            case DecisionKind.SpecialContinue:
                return "special-tile acknowledgement";

            case DecisionKind.TradeResponse:
                return "incoming trade offer";

            case DecisionKind.Purchase:
                return "property purchase";

            case DecisionKind.Travel:
                return "travel choice";

            case DecisionKind.Development:
                return "property development";

            case DecisionKind.Auction:
                return "auction turn";

            case DecisionKind.TradeSetup:
                return "trade setup";

            default:
                return "gameplay decision";
        }
    }

    private static string DescribeSafeAction(
        DecisionKind decision)
    {
        switch (decision)
        {
            case DecisionKind.TripleDoubleContinue:
            case DecisionKind.EventContinue:
            case DecisionKind.SpecialContinue:
                return "CONTINUE";

            case DecisionKind.TradeResponse:
                return "REJECT";

            case DecisionKind.Purchase:
                return "SKIP PURCHASE";

            case DecisionKind.Travel:
                return "STAY";

            case DecisionKind.Development:
                return "SKIP DEVELOPMENT";

            case DecisionKind.Auction:
                return "PASS";

            case DecisionKind.TradeSetup:
                return "CANCEL TRADE";

            default:
                return "SAFE DEFAULT";
        }
    }

    private void ResetWindow()
    {
        activeDecision =
            DecisionKind.None;

        activePlayer = null;
        elapsedSeconds = 0f;
    }

    private void EnsureReferences()
    {
        if (turnManager == null)
        {
            turnManager =
                FindSceneComponent<TurnManager>();
        }

        if (tileResolutionManager == null)
        {
            tileResolutionManager =
                FindSceneComponent<
                    TileResolutionManager>();
        }

        if (tradeManager == null)
        {
            tradeManager =
                FindSceneComponent<
                    TradeManager>();
        }

        if (auctionManager == null)
        {
            auctionManager =
                FindSceneComponent<
                    AuctionManager>();
        }

        if (eventCardManager == null)
        {
            eventCardManager =
                FindSceneComponent<
                    EventCardManager>();
        }

        if (specialTileManager == null)
        {
            specialTileManager =
                FindSceneComponent<
                    SpecialTileManager>();
        }
    }

    private static T FindSceneComponent<T>()
        where T : Component
    {
        T[] all =
            Resources.FindObjectsOfTypeAll<T>();

        foreach (T item in all)
        {
            if (item != null &&
                item.gameObject.scene.IsValid())
            {
                return item;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        TurnManager newTurnManager,
        TileResolutionManager newTileResolutionManager,
        TradeManager newTradeManager,
        AuctionManager newAuctionManager,
        EventCardManager newEventCardManager,
        SpecialTileManager newSpecialTileManager,
        float newDecisionTimeoutSeconds)
    {
        turnManager = newTurnManager;
        tileResolutionManager =
            newTileResolutionManager;
        tradeManager = newTradeManager;
        auctionManager = newAuctionManager;
        eventCardManager = newEventCardManager;
        specialTileManager =
            newSpecialTileManager;

        decisionTimeoutSeconds =
            Mathf.Max(
                1f,
                newDecisionTimeoutSeconds);
    }
#endif
}
