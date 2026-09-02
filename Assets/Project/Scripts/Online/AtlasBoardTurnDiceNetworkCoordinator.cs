using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AtlasBoardLobbyRuntimeBridge))]
[RequireComponent(typeof(AtlasBoardMatchRuntimeBridge))]
public sealed class AtlasBoardTurnDiceNetworkCoordinator :
    MonoBehaviour, IAtlasBoardSessionExitHandler
{
    [Serializable]
    private sealed class TurnDiceFrame
    {
        // Schema 2 extends the already-working Phase 5B Turn/Dice frame with
        // Phase 5C authoritative pawn movement state. Schema 1 remains readable
        // by the follower during a transient old snapshot.
        public int schemaVersion = 7;
        public string phase = "starting";
        public int activeSlotIndex = -1;
        public int currentRound = 1;
        public int roundLimit = 20;
        public int diceSequence;
        public int dicePlayerSlotIndex = -1;
        public int dieOne;
        public int dieTwo;
        public int total;
        public bool startingOrderDice;

        public int movementSequence;
        public int movementPlayerSlotIndex = -1;
        public int movementStartTileIndex = -1;
        public int movementTargetTileIndex = -1;
        public int movementSteps;
        public bool movementInProgress;
        public bool movementPassedStart;
        public bool movementUsesSprint;

        // Stable-slot indexed authoritative position checkpoint. The follower
        // uses this only as a correction after visual movement; it never resolves
        // a tile or economy action from these values.
        public int[] pawnTileIndices =
        {
            -1, -1, -1, -1
        };

        // Phase 5D authoritative economy checkpoint. Money is indexed by
        // stable player slot. Tile ownership is indexed by stable tile index.
        // Remote clients apply these values as presentation/state mirrors only;
        // they never calculate purchases or rent locally.
        public int[] playerMoney =
        {
            -1, -1, -1, -1
        };

        public int[] tileOwnerSlotIndices =
            Array.Empty<int>();

        // Phase 5F authoritative development checkpoint by stable tile index.
        public int[] tileDevelopmentLevels =
            Array.Empty<int>();

        // Phase 5F final result snapshot. Host computes this once from the same
        // authoritative economy/development state and Remote only presents it.
        public MatchResultManager.OnlineResultSnapshot matchResult;

        // Phase 5E blocking decision mirror. Schema 5 extends the original
        // purchase-only payload with Travel, Event, Special and Auction state.
        public string decisionKind = string.Empty;
        public int decisionPlayerSlotIndex = -1;
        public int decisionAuxSlotIndex = -1;
        public int decisionTileIndex = -1;
        public int decisionValue0;
        public int decisionValue1;
        public int decisionValue2;
        public int decisionValue3;
        public bool decisionReady;
        public string decisionText0 = string.Empty;
        public string decisionText1 = string.Empty;
        public string decisionText2 = string.Empty;
        public string decisionText3 = string.Empty;

        // Match-session authoritative cosmetic ids by stable slot. RemoteHuman
        // slots are learned from their owning client instead of Host PlayerPrefs.
        public string[] pawnCosmeticIds =
        {
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty
        };
    }

    private sealed class FollowerMovementCommand
    {
        public int Sequence;
        public int PlayerSlotIndex;
        public int StartTileIndex;
        public int TargetTileIndex;
        public int Steps;
        public bool PassedStart;
        public bool UsesSprint;
    }

    [Serializable]
    private sealed class RollIntentPayload
    {
        public bool startingOrder;
    }

    [Serializable]
    private sealed class DecisionIntentPayload
    {
        public string kind = string.Empty;
        public string action = string.Empty;
        public int slotIndex = -1;
        public int tileIndex = -1;
        public int targetSlotIndex = -1;
        public int offeredTileIndex = -1;
        public int offeredCash;
        public int requestedTileIndex = -1;
        public int requestedCash;
        public string value = string.Empty;
    }

    [Header("Phase 5B Polling")]
    [SerializeField, Min(0.1f)]
    private float hostIntentPollSeconds = 0.25f;

    [SerializeField, Min(0.1f)]
    private float hostStatePublishCheckSeconds = 0.20f;

    private AtlasBoardLobbyRuntimeBridge lobbyBridge;
    private AtlasBoardMatchRuntimeBridge matchBridge;
    private TurnManager turnManager;
    private BoardPath boardPath;
    private TileResolutionManager tileResolutionManager;
    private SpecialTileManager specialTileManager;
    private EventCardManager eventCardManager;
    private AuctionManager auctionManager;
    private TradeManager tradeManager;
    private PropertyDevelopmentManager propertyDevelopmentManager;
    private MatchResultManager matchResultManager;
    private AtlasBoardHumanRollTimeoutController humanRollTimeoutController;
    private TabletUIManager tabletUIManager;

    public bool IsPreparedOnlineMatch =>
        prepared;

    public bool LocalIsHost =>
        prepared && localIsHost;

    public bool IsOnlineSessionActive =>
        prepared;

    private readonly List<int>
        locallyControlledHumanSlots =
            new List<int>();

    private readonly HashSet<int>
        submittedRemoteCosmeticSlots =
            new HashSet<int>();

    private string[] authoritativePawnCosmeticIds =
    {
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty
    };

    private bool remoteCosmeticSubmitInFlight;
    private float nextRemoteCosmeticSubmitAt;

    private AtlasLobbySnapshot startLobbySnapshot;
    private bool prepared;
    private bool localIsHost;
    private bool hostNetworkInitialized;
    private bool hostIntentPollInFlight;
    private bool hostPublishInFlight;
    private float nextIntentPollAt;
    private float nextPublishCheckAt;
    private int hostKnownRevision;
    private string lastPublishedFrameJson = string.Empty;

    private int diceSequence;
    private int dicePlayerSlotIndex = -1;
    private int lastDieOne;
    private int lastDieTwo;
    private int lastTotal;
    private bool lastDiceWasStartingOrder;

    // Phase 5C host movement telemetry. The Host remains the only gameplay
    // simulator; Remote clients consume these values for presentation only.
    private int movementSequence;
    private int movementPlayerSlotIndex = -1;
    private int movementStartTileIndex = -1;
    private int movementTargetTileIndex = -1;
    private int movementSteps;
    private bool movementInProgress;
    private bool movementPassedStart;
    private bool movementUsesSprint;

    private readonly List<PlayerPawnMover>
        subscribedPawns =
            new List<PlayerPawnMover>();

    private readonly Queue<FollowerMovementCommand>
        followerMovementQueue =
            new Queue<FollowerMovementCommand>();

    private int lastQueuedFollowerMovementSequence;
    private bool followerMovementVisualActive;
    private int[] latestFollowerPawnTileIndices =
    {
        -1, -1, -1, -1
    };

    private readonly string[] lastAppliedFollowerPawnCosmeticIds =
    {
        string.Empty, string.Empty, string.Empty, string.Empty
    };

    private readonly Dictionary<int, string> lastObservedControllerBySlot =
        new Dictionary<int, string>();
    private readonly Dictionary<int, string> lastObservedConnectionBySlot =
        new Dictionary<int, string>();

    private bool reconnectExpiryInFlight;
    private float nextReconnectExpiryCheckAt;
    private bool rematchRequestInFlight;
    private bool leaveRequestInFlight;
    private bool localAfkExitScheduled;
    private string preparedMatchId = string.Empty;
    private bool followerNeedsInitialCheckpointSnap;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDestroy()
    {
        UnsubscribeFromTurnManager();
        UnsubscribeFromPawnMovement();

        if (tileResolutionManager != null)
        {
            tileResolutionManager.RemotePurchaseDecisionRequested -=
                HandleRemotePurchaseDecisionRequested;
            tileResolutionManager.RemoteTravelDecisionRequested -=
                HandleRemoteTravelDecisionRequested;
            tileResolutionManager.RemoteDevelopmentDecisionRequested -=
                HandleRemoteDevelopmentDecisionRequested;
        }

        if (specialTileManager != null)
        {
            specialTileManager.RemoteContinueRequested -=
                HandleRemoteSpecialContinueRequested;
        }

        if (eventCardManager != null)
        {
            eventCardManager.RemoteContinueRequested -=
                HandleRemoteEventContinueRequested;
        }

        if (auctionManager != null)
        {
            auctionManager.RemoteAuctionDecisionRequested -=
                HandleRemoteAuctionDecisionRequested;
        }

        if (tradeManager != null)
        {
            tradeManager.RemoteTradeOfferRequested -=
                HandleRemoteTradeOfferRequested;
            tradeManager.RemoteTradeResponseRequested -=
                HandleRemoteTradeResponseRequested;
            tradeManager.RemoteTradeWindowChanged -=
                HandleRemoteTradeWindowChanged;
        }

        if (matchBridge != null)
        {
            matchBridge.SnapshotChanged -=
                HandleMatchSnapshotChanged;
        }

        if (lobbyBridge != null)
        {
            lobbyBridge.SnapshotChanged -=
                HandleLobbySnapshotChanged;
        }

        if (humanRollTimeoutController != null)
        {
            humanRollTimeoutController.AfkRemovalTriggered -=
                HandleHostAfkRemovalTriggered;
        }
    }

    private async void Update()
    {
        if (!prepared)
        {
            return;
        }

        ResolveTurnManager();
        ResolveTileResolutionManager();
        ResolveSpecialTileManager();
        ResolveEventCardManager();
        ResolveAuctionManager();
        ResolveTradeManager();
        ResolvePropertyDevelopmentManager();
        ResolveMatchResultManager();

        if (subscribedPawns.Count < 2)
        {
            SubscribeToPawnMovement();
        }

        if (turnManager == null ||
            !turnManager.IsMatchStarted)
        {
            return;
        }

        if (!localIsHost)
        {
            if (!remoteCosmeticSubmitInFlight &&
                Time.unscaledTime >= nextRemoteCosmeticSubmitAt)
            {
                await SubmitRemotePawnCosmeticsIfNeededAsync();
            }

            ProcessFollowerMovementQueue();
            ApplyFollowerPositionCorrections();
            return;
        }

        if (!hostNetworkInitialized)
        {
            await InitializeHostNetworkAsync();

            if (!hostNetworkInitialized)
            {
                return;
            }
        }

        if (!reconnectExpiryInFlight &&
            Time.unscaledTime >= nextReconnectExpiryCheckAt)
        {
            nextReconnectExpiryCheckAt =
                Time.unscaledTime + 5f;
            await ExpireReconnectReservationsAsync();
        }

        if (!hostIntentPollInFlight &&
            Time.unscaledTime >= nextIntentPollAt)
        {
            nextIntentPollAt =
                Time.unscaledTime +
                Mathf.Max(0.1f, hostIntentPollSeconds);

            await PollHostIntentsAsync();
        }

        if (!hostPublishInFlight &&
            Time.unscaledTime >= nextPublishCheckAt)
        {
            nextPublishCheckAt =
                Time.unscaledTime +
                Mathf.Max(
                    0.1f,
                    hostStatePublishCheckSeconds);

            await PublishHostStateIfChangedAsync();
        }
    }

    public void PrepareForAuthoritativeMatchStart(
        AtlasLobbySnapshot lobbySnapshot)
    {
        if (lobbySnapshot == null)
        {
            return;
        }

        ResolveReferences();
        ResolveTurnManager();
        ResolveTileResolutionManager();
        ResolveSpecialTileManager();
        ResolveEventCardManager();
        ResolveAuctionManager();
        ResolveTradeManager();
        ResolvePropertyDevelopmentManager();
        ResolveMatchResultManager();
        ResolveHumanRollTimeoutController();
        ResolveTabletUIManager();
        SubscribeToPawnMovement();

        string incomingMatchId =
            lobbySnapshot.MatchId ?? string.Empty;

        bool needsFreshSessionReset =
            !prepared ||
            !string.Equals(
                preparedMatchId,
                incomingMatchId,
                StringComparison.Ordinal);

        if (needsFreshSessionReset)
        {
            ResetRuntimePresentationForNewMatchSession(
                incomingMatchId);
        }

        preparedMatchId = incomingMatchId;

        startLobbySnapshot =
            lobbySnapshot;

        string localAccountId =
            lobbyBridge != null
                ? lobbyBridge.CurrentAccountId
                : string.Empty;

        localIsHost =
            !string.IsNullOrWhiteSpace(localAccountId) &&
            string.Equals(
                lobbySnapshot.HostAccountId,
                localAccountId,
                StringComparison.Ordinal);

        List<int> controlledSlots =
            BuildLocallyControlledHumanSlots(
                lobbySnapshot,
                localAccountId,
                localIsHost);

        locallyControlledHumanSlots.Clear();
        locallyControlledHumanSlots.AddRange(
            controlledSlots.Distinct());

        ApplyLobbyIdentitySnapshot(lobbySnapshot);

        if (tileResolutionManager != null)
        {
            tileResolutionManager
                .ConfigureOnlinePurchaseDecisionAuthority(
                    localIsHost,
                    controlledSlots);
        }

        specialTileManager?
            .ConfigureOnlineDecisionAuthority(
                localIsHost,
                controlledSlots);

        eventCardManager?
            .ConfigureOnlineDecisionAuthority(
                localIsHost,
                controlledSlots);

        auctionManager?
            .ConfigureOnlineDecisionAuthority(
                controlledSlots);

        tradeManager?
            .ConfigureOnlineTradeAuthority(
                localIsHost,
                controlledSlots);

        if (turnManager != null)
        {
            turnManager.ConfigureOnlineTurnAuthority(
                followerMode: !localIsHost,
                locallyControlledHumanSlots:
                    controlledSlots);

            SubscribeToTurnManager();
        }

        if (matchBridge != null)
        {
            matchBridge.SnapshotChanged -=
                HandleMatchSnapshotChanged;

            matchBridge.SnapshotChanged +=
                HandleMatchSnapshotChanged;
        }

        if (lobbyBridge != null)
        {
            lobbyBridge.SnapshotChanged -=
                HandleLobbySnapshotChanged;

            lobbyBridge.SnapshotChanged +=
                HandleLobbySnapshotChanged;
        }

        if (humanRollTimeoutController != null)
        {
            humanRollTimeoutController.AfkRemovalTriggered -=
                HandleHostAfkRemovalTriggered;

            if (localIsHost)
            {
                humanRollTimeoutController.AfkRemovalTriggered +=
                    HandleHostAfkRemovalTriggered;
            }
        }

        diceSequence = 0;
        dicePlayerSlotIndex = -1;
        lastDieOne = 0;
        lastDieTwo = 0;
        lastTotal = 0;
        lastDiceWasStartingOrder = false;

        movementSequence = 0;
        movementPlayerSlotIndex = -1;
        movementStartTileIndex = -1;
        movementTargetTileIndex = -1;
        movementSteps = 0;
        movementInProgress = false;
        movementPassedStart = false;
        movementUsesSprint = false;

        followerMovementQueue.Clear();
        lastQueuedFollowerMovementSequence = 0;
        followerMovementVisualActive = false;
        followerNeedsInitialCheckpointSnap =
            !localIsHost;
        latestFollowerPawnTileIndices =
            new[] { -1, -1, -1, -1 };

        for (int index = 0;
             index < lastAppliedFollowerPawnCosmeticIds.Length;
             index++)
        {
            lastAppliedFollowerPawnCosmeticIds[index] = string.Empty;
        }

        lastObservedControllerBySlot.Clear();
        lastObservedConnectionBySlot.Clear();
        reconnectExpiryInFlight = false;
        nextReconnectExpiryCheckAt = 0f;
        rematchRequestInFlight = false;
        leaveRequestInFlight = false;
        localAfkExitScheduled = false;

        submittedRemoteCosmeticSlots.Clear();
        remoteCosmeticSubmitInFlight = false;
        nextRemoteCosmeticSubmitAt = 0f;
        InitializeAuthoritativePawnCosmetics();

        hostKnownRevision = 0;
        hostNetworkInitialized = false;
        lastPublishedFrameJson = string.Empty;
        nextIntentPollAt = 0f;
        nextPublishCheckAt = 0f;
        prepared = true;

        Debug.Log(
            localIsHost
                ? "AtlasBoard Phase 5F v1.1 prepared HOST authoritative Turn/Dice + Movement + Economy + Trade/Development/Match Result simulation."
                : "AtlasBoard Phase 5F v1.1 prepared REMOTE follower presentation + owned Trade/Development decisions + Match Result.",
            this);
    }

    private void ResolveReferences()
    {
        if (lobbyBridge == null)
        {
            lobbyBridge =
                GetComponent<
                    AtlasBoardLobbyRuntimeBridge>();
        }

        if (matchBridge == null)
        {
            matchBridge =
                GetComponent<
                    AtlasBoardMatchRuntimeBridge>();
        }
    }

    private void ResolveTileResolutionManager()
    {
        if (tileResolutionManager != null)
        {
            return;
        }

        TileResolutionManager[] managers =
            Resources.FindObjectsOfTypeAll<
                TileResolutionManager>();

        foreach (TileResolutionManager manager
                 in managers)
        {
            if (manager == null ||
                !manager.gameObject.scene.IsValid())
            {
                continue;
            }

            tileResolutionManager = manager;

            tileResolutionManager.RemotePurchaseDecisionRequested -=
                HandleRemotePurchaseDecisionRequested;

            tileResolutionManager.RemotePurchaseDecisionRequested +=
                HandleRemotePurchaseDecisionRequested;

            tileResolutionManager.RemoteTravelDecisionRequested -=
                HandleRemoteTravelDecisionRequested;

            tileResolutionManager.RemoteTravelDecisionRequested +=
                HandleRemoteTravelDecisionRequested;

            tileResolutionManager.RemoteDevelopmentDecisionRequested -=
                HandleRemoteDevelopmentDecisionRequested;

            tileResolutionManager.RemoteDevelopmentDecisionRequested +=
                HandleRemoteDevelopmentDecisionRequested;

            if (prepared)
            {
                tileResolutionManager
                    .ConfigureOnlinePurchaseDecisionAuthority(
                        localIsHost,
                        locallyControlledHumanSlots);
            }

            return;
        }
    }

    private void ResolveSpecialTileManager()
    {
        if (specialTileManager != null)
        {
            return;
        }

        SpecialTileManager[] managers =
            Resources.FindObjectsOfTypeAll<
                SpecialTileManager>();

        foreach (SpecialTileManager manager in managers)
        {
            if (manager == null ||
                !manager.gameObject.scene.IsValid())
            {
                continue;
            }

            specialTileManager = manager;
            specialTileManager.RemoteContinueRequested -=
                HandleRemoteSpecialContinueRequested;
            specialTileManager.RemoteContinueRequested +=
                HandleRemoteSpecialContinueRequested;

            if (prepared)
            {
                specialTileManager
                    .ConfigureOnlineDecisionAuthority(
                        localIsHost,
                        locallyControlledHumanSlots);
            }

            return;
        }
    }

    private void ResolveEventCardManager()
    {
        if (eventCardManager != null)
        {
            return;
        }

        EventCardManager[] managers =
            Resources.FindObjectsOfTypeAll<
                EventCardManager>();

        foreach (EventCardManager manager in managers)
        {
            if (manager == null ||
                !manager.gameObject.scene.IsValid())
            {
                continue;
            }

            eventCardManager = manager;
            eventCardManager.RemoteContinueRequested -=
                HandleRemoteEventContinueRequested;
            eventCardManager.RemoteContinueRequested +=
                HandleRemoteEventContinueRequested;

            if (prepared)
            {
                eventCardManager
                    .ConfigureOnlineDecisionAuthority(
                        localIsHost,
                        locallyControlledHumanSlots);
            }

            return;
        }
    }

    private void ResolveAuctionManager()
    {
        if (auctionManager != null)
        {
            return;
        }

        AuctionManager[] managers =
            Resources.FindObjectsOfTypeAll<
                AuctionManager>();

        foreach (AuctionManager manager in managers)
        {
            if (manager == null ||
                !manager.gameObject.scene.IsValid())
            {
                continue;
            }

            auctionManager = manager;
            auctionManager.RemoteAuctionDecisionRequested -=
                HandleRemoteAuctionDecisionRequested;
            auctionManager.RemoteAuctionDecisionRequested +=
                HandleRemoteAuctionDecisionRequested;

            if (prepared)
            {
                auctionManager
                    .ConfigureOnlineDecisionAuthority(
                        locallyControlledHumanSlots);
            }

            return;
        }
    }

    private void ResolveTradeManager()
    {
        if (tradeManager != null)
        {
            return;
        }

        TradeManager[] managers =
            Resources.FindObjectsOfTypeAll<
                TradeManager>();

        foreach (TradeManager manager in managers)
        {
            if (manager == null ||
                !manager.gameObject.scene.IsValid())
            {
                continue;
            }

            tradeManager = manager;
            tradeManager.RemoteTradeOfferRequested -=
                HandleRemoteTradeOfferRequested;
            tradeManager.RemoteTradeOfferRequested +=
                HandleRemoteTradeOfferRequested;
            tradeManager.RemoteTradeResponseRequested -=
                HandleRemoteTradeResponseRequested;
            tradeManager.RemoteTradeResponseRequested +=
                HandleRemoteTradeResponseRequested;
            tradeManager.RemoteTradeWindowChanged -=
                HandleRemoteTradeWindowChanged;
            tradeManager.RemoteTradeWindowChanged +=
                HandleRemoteTradeWindowChanged;

            if (prepared)
            {
                tradeManager.ConfigureOnlineTradeAuthority(
                    localIsHost,
                    locallyControlledHumanSlots);
            }

            return;
        }
    }

    private void ResolvePropertyDevelopmentManager()
    {
        if (propertyDevelopmentManager != null)
        {
            return;
        }

        PropertyDevelopmentManager[] managers =
            Resources.FindObjectsOfTypeAll<
                PropertyDevelopmentManager>();

        foreach (PropertyDevelopmentManager manager in managers)
        {
            if (manager != null &&
                manager.gameObject.scene.IsValid())
            {
                propertyDevelopmentManager = manager;
                return;
            }
        }
    }

    private void ResolveMatchResultManager()
    {
        if (matchResultManager != null)
        {
            return;
        }

        MatchResultManager[] managers =
            Resources.FindObjectsOfTypeAll<
                MatchResultManager>();

        foreach (MatchResultManager manager in managers)
        {
            if (manager != null &&
                manager.gameObject.scene.IsValid())
            {
                matchResultManager = manager;
                return;
            }
        }
    }

    private void ResolveHumanRollTimeoutController()
    {
        if (humanRollTimeoutController != null)
        {
            return;
        }

        AtlasBoardHumanRollTimeoutController[] controllers =
            Resources.FindObjectsOfTypeAll<
                AtlasBoardHumanRollTimeoutController>();

        foreach (AtlasBoardHumanRollTimeoutController controller in controllers)
        {
            if (controller != null &&
                controller.gameObject.scene.IsValid())
            {
                humanRollTimeoutController = controller;
                return;
            }
        }
    }

    private void ResolveTabletUIManager()
    {
        if (tabletUIManager != null)
        {
            return;
        }

        TabletUIManager[] managers =
            Resources.FindObjectsOfTypeAll<TabletUIManager>();

        foreach (TabletUIManager manager in managers)
        {
            if (manager != null &&
                manager.gameObject.scene.IsValid())
            {
                tabletUIManager = manager;
                return;
            }
        }
    }

    private void ResolveTurnManager()
    {
        if (turnManager != null)
        {
            return;
        }

        TurnManager[] managers =
            Resources.FindObjectsOfTypeAll<
                TurnManager>();

        foreach (TurnManager manager
                 in managers)
        {
            if (manager != null &&
                manager.gameObject.scene.IsValid())
            {
                turnManager = manager;
                SubscribeToTurnManager();
                return;
            }
        }
    }

    private void SubscribeToTurnManager()
    {
        if (turnManager == null)
        {
            return;
        }

        turnManager.AuthoritativeDiceCommitted -=
            HandleAuthoritativeDiceCommitted;

        turnManager.AuthoritativeDiceCommitted +=
            HandleAuthoritativeDiceCommitted;

        turnManager.OnlineRollRequested -=
            HandleOnlineRollRequested;

        turnManager.OnlineTripleDoublePenaltyContinueRequested -=
            HandleOnlineTripleDoublePenaltyContinueRequested;

        turnManager.OnlineRollRequested +=
            HandleOnlineRollRequested;

        turnManager.OnlineTripleDoublePenaltyContinueRequested -=
            HandleOnlineTripleDoublePenaltyContinueRequested;

        turnManager.OnlineTripleDoublePenaltyContinueRequested +=
            HandleOnlineTripleDoublePenaltyContinueRequested;
    }

    private void UnsubscribeFromTurnManager()
    {
        if (turnManager == null)
        {
            return;
        }

        turnManager.AuthoritativeDiceCommitted -=
            HandleAuthoritativeDiceCommitted;

        turnManager.OnlineRollRequested -=
            HandleOnlineRollRequested;
    }

    private void SubscribeToPawnMovement()
    {
        for (int index =
                 subscribedPawns.Count - 1;
             index >= 0;
             index--)
        {
            if (subscribedPawns[index] == null)
            {
                subscribedPawns.RemoveAt(index);
            }
        }

        PlayerPawnMover[] pawns =
            Resources.FindObjectsOfTypeAll<
                PlayerPawnMover>();

        foreach (PlayerPawnMover pawn in pawns)
        {
            if (pawn == null ||
                !pawn.gameObject.scene.IsValid() ||
                subscribedPawns.Contains(pawn))
            {
                continue;
            }

            pawn.MovementStarted -=
                HandlePawnMovementStarted;
            pawn.MovementEnded -=
                HandlePawnMovementEnded;

            pawn.MovementStarted +=
                HandlePawnMovementStarted;
            pawn.MovementEnded +=
                HandlePawnMovementEnded;

            subscribedPawns.Add(pawn);
        }
    }

    private void UnsubscribeFromPawnMovement()
    {
        foreach (PlayerPawnMover pawn
                 in subscribedPawns)
        {
            if (pawn == null)
            {
                continue;
            }

            pawn.MovementStarted -=
                HandlePawnMovementStarted;
            pawn.MovementEnded -=
                HandlePawnMovementEnded;
        }

        subscribedPawns.Clear();
    }

    private void HandlePawnMovementStarted(
        PlayerPawnMover pawn)
    {
        if (!prepared ||
            !localIsHost ||
            pawn == null)
        {
            return;
        }

        PlayerGameState player =
            pawn.GetComponent<PlayerGameState>();

        if (player == null)
        {
            return;
        }

        movementSequence++;
        movementPlayerSlotIndex =
            player.PlayerSlotIndex;
        movementStartTileIndex =
            pawn.LastMovementStartTileIndex;
        movementTargetTileIndex =
            pawn.LastMovementTargetTileIndex;
        movementSteps =
            pawn.LastMovementStepCount;
        movementPassedStart =
            pawn.LastMovementPassedStart;
        movementUsesSprint =
            pawn.LastMovementUsedSprint;
        movementInProgress = true;

        nextPublishCheckAt = 0f;

        Debug.Log(
            $"Phase 5C Host movement start — " +
            $"P{movementPlayerSlotIndex + 1}: " +
            $"tile {movementStartTileIndex} -> " +
            $"{movementTargetTileIndex}, " +
            $"steps={movementSteps}, " +
            $"sequence={movementSequence}.",
            this);
    }

    private void HandlePawnMovementEnded(
        PlayerPawnMover pawn)
    {
        if (!prepared ||
            !localIsHost ||
            pawn == null)
        {
            return;
        }

        PlayerGameState player =
            pawn.GetComponent<PlayerGameState>();

        if (player == null ||
            player.PlayerSlotIndex !=
                movementPlayerSlotIndex)
        {
            return;
        }

        movementTargetTileIndex =
            pawn.CurrentTileIndex;
        movementInProgress = false;
        nextPublishCheckAt = 0f;

        Debug.Log(
            $"Phase 5C Host movement end — " +
            $"P{movementPlayerSlotIndex + 1}: " +
            $"tile {movementTargetTileIndex}, " +
            $"sequence={movementSequence}.",
            this);
    }

    private static List<int>
        BuildLocallyControlledHumanSlots(
            AtlasLobbySnapshot snapshot,
            string localAccountId,
            bool isHost)
    {
        List<int> result =
            new List<int>();

        if (snapshot == null ||
            snapshot.Members == null)
        {
            return result;
        }

        foreach (AtlasLobbyMemberSnapshot member
                 in snapshot.Members)
        {
            if (member == null ||
                !member.Active)
            {
                continue;
            }

            if (isHost &&
                (member.SeatMode ==
                     AtlasLobbySeatMode.HostLocal ||
                 member.SeatMode ==
                     AtlasLobbySeatMode.LocalHuman))
            {
                result.Add(member.SlotIndex);
                continue;
            }

            if (member.SeatMode ==
                    AtlasLobbySeatMode.RemoteHuman &&
                string.Equals(
                    member.AccountId,
                    localAccountId,
                    StringComparison.Ordinal))
            {
                result.Add(member.SlotIndex);
            }
        }

        return result.Distinct().ToList();
    }

    private void HandleAuthoritativeDiceCommitted(
        PlayerGameState player,
        int dieOne,
        int dieTwo,
        bool startingOrder)
    {
        if (!prepared ||
            !localIsHost ||
            player == null)
        {
            return;
        }

        diceSequence++;
        dicePlayerSlotIndex =
            player.PlayerSlotIndex;
        lastDieOne = dieOne;
        lastDieTwo = dieTwo;
        lastTotal = dieOne + dieTwo;
        lastDiceWasStartingOrder =
            startingOrder;

        // Force the next host update to publish the exact committed values.
        nextPublishCheckAt = 0f;
    }

    private async void HandleOnlineRollRequested(
        PlayerGameState player,
        bool startingOrder)
    {
        if (!prepared ||
            localIsHost ||
            player == null ||
            matchBridge == null)
        {
            return;
        }

        RollIntentPayload payload =
            new RollIntentPayload
            {
                startingOrder = startingOrder
            };

        string commandId =
            "roll-" +
            Guid.NewGuid().ToString("N");

        AtlasMatchNetworkResult result =
            await matchBridge.SubmitIntentAsync(
                "request_roll",
                JsonUtility.ToJson(payload),
                commandId);

        if (!result.Success)
        {
            turnManager?.
                NotifyOnlineRollRequestFailed();

            Debug.LogWarning(
                "AtlasBoard Phase 5B remote Roll intent failed: " +
                result.TechnicalMessage,
                this);
        }
    }

    private async Task InitializeHostNetworkAsync()
    {
        if (hostNetworkInitialized ||
            matchBridge == null)
        {
            return;
        }

        AtlasMatchNetworkResult result =
            await matchBridge.GetSnapshotAsync();

        if (!result.Success ||
            result.Snapshot == null)
        {
            return;
        }

        hostKnownRevision =
            result.Snapshot.Revision;

        hostNetworkInitialized = true;
        nextPublishCheckAt = 0f;
    }

    private async Task PollHostIntentsAsync()
    {
        if (hostIntentPollInFlight ||
            !localIsHost ||
            matchBridge == null ||
            turnManager == null)
        {
            return;
        }

        hostIntentPollInFlight = true;

        try
        {
            AtlasMatchNetworkResult result =
                await matchBridge
                    .HostListPendingIntentsAsync();

            if (!result.Success ||
                result.Intents == null ||
                result.Intents.Length == 0)
            {
                return;
            }

            List<string> consumed =
                new List<string>();

            foreach (AtlasMatchIntent intent
                     in result.Intents)
            {
                if (intent == null ||
                    string.IsNullOrWhiteSpace(
                        intent.IntentId))
                {
                    continue;
                }

                if (string.Equals(
                        intent.IntentType,
                        "request_roll",
                        StringComparison.Ordinal))
                {
                    int slotIndex =
                        ResolveSlotIndex(
                            intent.SeatId);

                    PlayerGameState player =
                        turnManager
                            .GetPlayerStateBySlotIndex(
                                slotIndex);

                    bool accepted =
                        player != null &&
                        turnManager
                            .TryRequestAuthoritativeNetworkRoll(
                                player);

                    Debug.Log(
                        accepted
                            ? $"Phase 5B Host accepted remote Roll for P{slotIndex + 1}."
                            : $"Phase 5B Host rejected stale/invalid remote Roll for P{slotIndex + 1}.",
                        this);
                }
                else if (string.Equals(
                             intent.IntentType,
                             "submit_decision",
                             StringComparison.Ordinal))
                {
                    HandleHostDecisionIntent(intent);
                }

                // Invalid/stale intents are also consumed so a client cannot
                // permanently poison the host queue with an old command.
                consumed.Add(
                    intent.IntentId);
            }

            if (consumed.Count > 0)
            {
                await matchBridge
                    .HostAcknowledgeIntentsAsync(
                        consumed);
            }
        }
        finally
        {
            hostIntentPollInFlight = false;
        }
    }

    private void HandleHostDecisionIntent(
        AtlasMatchIntent intent)
    {
        if (intent == null ||
            string.IsNullOrWhiteSpace(
                intent.PayloadJson))
        {
            return;
        }

        DecisionIntentPayload payload;

        try
        {
            payload =
                JsonUtility.FromJson<
                    DecisionIntentPayload>(
                        intent.PayloadJson);
        }
        catch
        {
            return;
        }

        if (payload == null)
        {
            return;
        }

        int authoritativeSlotIndex =
            ResolveSlotIndex(intent.SeatId);

        if (authoritativeSlotIndex < 0 ||
            payload.slotIndex !=
                authoritativeSlotIndex)
        {
            Debug.LogWarning(
                "Phase 5E rejected decision intent because the payload slot " +
                "does not match the authenticated match seat.",
                this);
            return;
        }

        if (string.Equals(
                payload.kind,
                "pawn_cosmetic",
                StringComparison.Ordinal))
        {
            TryAcceptRemotePawnCosmetic(
                authoritativeSlotIndex,
                payload.value);
            return;
        }

        if (string.Equals(
                payload.kind,
                "triple_double_penalty",
                StringComparison.Ordinal))
        {
            PlayerGameState penaltyPlayer =
                turnManager != null
                    ? turnManager.TripleDoublePenaltyPlayerState
                    : null;

            if (penaltyPlayer == null ||
                penaltyPlayer.PlayerSlotIndex != authoritativeSlotIndex ||
                !string.Equals(
                    payload.action,
                    "continue",
                    StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    "Phase 5F rejected stale/mismatched triple-double penalty decision.",
                    this);
                return;
            }

            turnManager.ContinueTripleDoublePenalty();
            nextPublishCheckAt = 0f;
            return;
        }

        if (string.Equals(
                payload.kind,
                "trade_hold",
                StringComparison.Ordinal))
        {
            ResolveHumanRollTimeoutController();

            PlayerGameState activePlayer =
                turnManager != null
                    ? turnManager.CurrentPlayerState
                    : null;

            if (activePlayer == null ||
                activePlayer.PlayerSlotIndex !=
                    authoritativeSlotIndex ||
                humanRollTimeoutController == null)
            {
                return;
            }

            bool open =
                string.Equals(
                    payload.action,
                    "open",
                    StringComparison.Ordinal);

            bool close =
                string.Equals(
                    payload.action,
                    "close",
                    StringComparison.Ordinal);

            if (!open && !close)
            {
                return;
            }

            humanRollTimeoutController
                .SetRemoteManagementHold(
                    authoritativeSlotIndex,
                    open,
                    30f);

            return;
        }

        ResolveTileResolutionManager();
        ResolveSpecialTileManager();
        ResolveEventCardManager();
        ResolveAuctionManager();
        ResolveTradeManager();

        if (string.Equals(
                payload.kind,
                "purchase",
                StringComparison.Ordinal))
        {
            if (tileResolutionManager == null ||
                tileResolutionManager.PendingPurchasePlayer == null ||
                tileResolutionManager.PendingPurchaseTile == null)
            {
                return;
            }

            PlayerGameState pendingPlayer =
                tileResolutionManager.PendingPurchasePlayer;

            BoardTile pendingTile =
                tileResolutionManager.PendingPurchaseTile;

            if (pendingPlayer.PlayerSlotIndex != authoritativeSlotIndex ||
                pendingTile.TileIndex != payload.tileIndex)
            {
                Debug.LogWarning(
                    "Phase 5E rejected stale/mismatched purchase decision.",
                    this);
                return;
            }

            if (string.Equals(
                    payload.action,
                    "buy",
                    StringComparison.Ordinal))
            {
                tileResolutionManager.BuyPendingTile();
            }
            else if (string.Equals(
                         payload.action,
                         "skip",
                         StringComparison.Ordinal))
            {
                tileResolutionManager.SkipPendingTile();
            }
            else
            {
                return;
            }

            nextPublishCheckAt = 0f;
            return;
        }

        if (string.Equals(
                payload.kind,
                "travel",
                StringComparison.Ordinal))
        {
            if (tileResolutionManager == null ||
                tileResolutionManager.PendingTravelPlayer == null ||
                tileResolutionManager.PendingTravelPlayer.PlayerSlotIndex !=
                    authoritativeSlotIndex ||
                tileResolutionManager.PendingTravelTargetIndex !=
                    payload.tileIndex)
            {
                Debug.LogWarning(
                    "Phase 5E rejected stale/mismatched Travel decision.",
                    this);
                return;
            }

            if (string.Equals(
                    payload.action,
                    "go",
                    StringComparison.Ordinal))
            {
                tileResolutionManager.TravelToNextEvent();
            }
            else if (string.Equals(
                         payload.action,
                         "stay",
                         StringComparison.Ordinal))
            {
                tileResolutionManager.StayOnTravelTile();
            }
            else
            {
                return;
            }

            nextPublishCheckAt = 0f;
            return;
        }

        if (string.Equals(
                payload.kind,
                "development",
                StringComparison.Ordinal))
        {
            if (tileResolutionManager == null ||
                tileResolutionManager.PendingDevelopmentPlayer == null ||
                tileResolutionManager.PendingDevelopmentTile == null ||
                tileResolutionManager.PendingDevelopmentPlayer.PlayerSlotIndex !=
                    authoritativeSlotIndex ||
                tileResolutionManager.PendingDevelopmentTile.TileIndex !=
                    payload.tileIndex)
            {
                Debug.LogWarning(
                    "Phase 5F rejected stale/mismatched Development decision.",
                    this);
                return;
            }

            if (string.Equals(
                    payload.action,
                    "develop",
                    StringComparison.Ordinal))
            {
                tileResolutionManager.DevelopPendingTile();
            }
            else if (string.Equals(
                         payload.action,
                         "skip",
                         StringComparison.Ordinal))
            {
                tileResolutionManager.SkipPendingDevelopment();
            }
            else
            {
                return;
            }

            nextPublishCheckAt = 0f;
            return;
        }

        if (string.Equals(
                payload.kind,
                "special",
                StringComparison.Ordinal))
        {
            PlayerGameState player =
                specialTileManager != null
                    ? specialTileManager.CurrentSpecialPlayer
                    : null;

            if (specialTileManager == null ||
                !specialTileManager.IsResolvingSpecialTile ||
                player == null ||
                player.PlayerSlotIndex != authoritativeSlotIndex ||
                !string.Equals(
                    payload.action,
                    "continue",
                    StringComparison.Ordinal))
            {
                return;
            }

            specialTileManager.ContinueAfterSpecialTile();
            nextPublishCheckAt = 0f;
            return;
        }

        if (string.Equals(
                payload.kind,
                "event",
                StringComparison.Ordinal))
        {
            PlayerGameState player =
                eventCardManager != null
                    ? eventCardManager.CurrentEventPlayer
                    : null;

            if (eventCardManager == null ||
                !eventCardManager.IsResolvingEvent ||
                !eventCardManager.EffectExecutionCompleted ||
                player == null ||
                player.PlayerSlotIndex != authoritativeSlotIndex ||
                !string.Equals(
                    payload.action,
                    "continue",
                    StringComparison.Ordinal))
            {
                return;
            }

            eventCardManager.ContinueAfterEvent();
            nextPublishCheckAt = 0f;
            return;
        }

        if (string.Equals(
                payload.kind,
                "trade",
                StringComparison.Ordinal))
        {
            if (tradeManager == null ||
                turnManager == null)
            {
                return;
            }

            if (string.Equals(
                    payload.action,
                    "offer",
                    StringComparison.Ordinal))
            {
                PlayerGameState initiator =
                    turnManager.GetPlayerStateBySlotIndex(
                        authoritativeSlotIndex);
                PlayerGameState targetPlayer =
                    turnManager.GetPlayerStateBySlotIndex(
                        payload.targetSlotIndex);
                BoardTile offeredTile =
                    payload.offeredTileIndex >= 0
                        ? GetBoardTile(payload.offeredTileIndex)
                        : null;
                BoardTile requestedTile =
                    payload.requestedTileIndex >= 0
                        ? GetBoardTile(payload.requestedTileIndex)
                        : null;

                bool accepted =
                    initiator != null &&
                    targetPlayer != null &&
                    tradeManager.TryBeginAuthoritativeRemoteOffer(
                        initiator,
                        targetPlayer,
                        offeredTile,
                        payload.offeredCash,
                        requestedTile,
                        payload.requestedCash);

                if (!accepted)
                {
                    Debug.LogWarning(
                        "Phase 5F rejected stale/invalid remote Trade offer.",
                        this);
                }

                nextPublishCheckAt = 0f;
                return;
            }

            if (string.Equals(
                    payload.action,
                    "accept",
                    StringComparison.Ordinal) ||
                string.Equals(
                    payload.action,
                    "reject",
                    StringComparison.Ordinal))
            {
                PlayerGameState targetPlayer =
                    tradeManager.TradeTarget;

                if (!tradeManager.IsAwaitingResponse ||
                    targetPlayer == null ||
                    targetPlayer.PlayerSlotIndex != authoritativeSlotIndex)
                {
                    Debug.LogWarning(
                        "Phase 5F rejected stale/mismatched Trade response.",
                        this);
                    return;
                }

                if (string.Equals(
                        payload.action,
                        "accept",
                        StringComparison.Ordinal))
                {
                    tradeManager.AcceptTradeOffer();
                }
                else
                {
                    tradeManager.RejectTradeOffer();
                }

                nextPublishCheckAt = 0f;
                return;
            }

            return;
        }

        if (string.Equals(
                payload.kind,
                "auction",
                StringComparison.Ordinal))
        {
            PlayerGameState bidder =
                auctionManager != null
                    ? auctionManager.CurrentBidder
                    : null;

            BoardTile property =
                auctionManager != null
                    ? auctionManager.AuctionProperty
                    : null;

            if (auctionManager == null ||
                !auctionManager.IsAuctionActive ||
                bidder == null ||
                bidder.PlayerSlotIndex != authoritativeSlotIndex ||
                property == null ||
                property.TileIndex != payload.tileIndex)
            {
                Debug.LogWarning(
                    "Phase 5E rejected stale/mismatched Auction decision.",
                    this);
                return;
            }

            if (string.Equals(
                    payload.action,
                    "bid_small",
                    StringComparison.Ordinal))
            {
                auctionManager.PlaceSmallBid();
            }
            else if (string.Equals(
                         payload.action,
                         "bid_large",
                         StringComparison.Ordinal))
            {
                auctionManager.PlaceLargeBid();
            }
            else if (string.Equals(
                         payload.action,
                         "pass",
                         StringComparison.Ordinal))
            {
                auctionManager.PassCurrentBidder();
            }
            else
            {
                return;
            }

            nextPublishCheckAt = 0f;
        }
    }

    private async Task PublishHostStateIfChangedAsync()
    {
        if (hostPublishInFlight ||
            !localIsHost ||
            matchBridge == null ||
            turnManager == null ||
            !turnManager.IsMatchStarted)
        {
            return;
        }

        TurnDiceFrame frame =
            BuildHostFrame();

        string json =
            JsonUtility.ToJson(frame);

        if (string.Equals(
                json,
                lastPublishedFrameJson,
                StringComparison.Ordinal))
        {
            return;
        }

        hostPublishInFlight = true;

        try
        {
            string turnSeatId =
                ResolveSeatId(
                    frame.activeSlotIndex);

            AtlasMatchNetworkResult result =
                await matchBridge
                    .HostPublishStateAsync(
                        hostKnownRevision,
                        frame.phase,
                        turnSeatId,
                        frame.diceSequence,
                        json);

            if (!result.Success)
            {
                // Most likely the local cached revision was stale. Refresh
                // once and let the next Update retry the same frame.
                AtlasMatchNetworkResult refreshed =
                    await matchBridge
                        .GetSnapshotAsync();

                if (refreshed.Success &&
                    refreshed.Snapshot != null)
                {
                    hostKnownRevision =
                        refreshed.Snapshot.Revision;
                }

                return;
            }

            hostKnownRevision =
                result.Revision;

            lastPublishedFrameJson =
                json;
        }
        finally
        {
            hostPublishInFlight = false;
        }
    }

    private TurnDiceFrame BuildHostFrame()
    {
        string phase =
            DetermineHostPhase();

        PlayerGameState activePlayer =
            turnManager.IsStartingOrderPhase
                ? turnManager.StartingOrderPlayerState
                : turnManager.CurrentPlayerState;

        ResolveTileResolutionManager();
        ResolveSpecialTileManager();
        ResolveEventCardManager();
        ResolveAuctionManager();
        ResolveTradeManager();
        ResolvePropertyDevelopmentManager();
        ResolveMatchResultManager();

        string decisionKind = string.Empty;
        int decisionPlayerSlotIndex = -1;
        int decisionAuxSlotIndex = -1;
        int decisionTileIndex = -1;
        int decisionValue0 = 0;
        int decisionValue1 = 0;
        int decisionValue2 = 0;
        int decisionValue3 = 0;
        bool decisionReady = false;
        string decisionText0 = string.Empty;
        string decisionText1 = string.Empty;
        string decisionText2 = string.Empty;
        string decisionText3 = string.Empty;

        if (turnManager != null &&
            turnManager.TripleDoublePenaltyPlayerState != null)
        {
            decisionKind = "triple_double_penalty";
            decisionPlayerSlotIndex =
                turnManager.TripleDoublePenaltyPlayerState.PlayerSlotIndex;
            decisionReady = true;
        }
        else if (tradeManager != null &&
            tradeManager.IsAwaitingResponse &&
            tradeManager.TradeInitiator != null &&
            tradeManager.TradeTarget != null)
        {
            decisionKind = "trade";
            decisionPlayerSlotIndex =
                tradeManager.TradeTarget.PlayerSlotIndex;
            decisionAuxSlotIndex =
                tradeManager.TradeInitiator.PlayerSlotIndex;
            decisionTileIndex =
                tradeManager.OfferedProperty != null
                    ? tradeManager.OfferedProperty.TileIndex
                    : -1;
            decisionValue0 =
                tradeManager.OfferedCash;
            decisionValue1 =
                tradeManager.RequestedProperty != null
                    ? tradeManager.RequestedProperty.TileIndex
                    : -1;
            decisionValue2 =
                tradeManager.RequestedCash;
            decisionReady = true;
        }
        else if (tileResolutionManager != null &&
            tileResolutionManager.PendingPurchasePlayer != null &&
            tileResolutionManager.PendingPurchaseTile != null)
        {
            decisionKind = "purchase";
            decisionPlayerSlotIndex =
                tileResolutionManager
                    .PendingPurchasePlayer
                    .PlayerSlotIndex;
            decisionTileIndex =
                tileResolutionManager
                    .PendingPurchaseTile
                    .TileIndex;
        }
        else if (tileResolutionManager != null &&
                 tileResolutionManager.PendingTravelPlayer != null &&
                 tileResolutionManager.PendingTravelTargetIndex >= 0)
        {
            decisionKind = "travel";
            decisionPlayerSlotIndex =
                tileResolutionManager
                    .PendingTravelPlayer
                    .PlayerSlotIndex;
            decisionTileIndex =
                tileResolutionManager
                    .PendingTravelTargetIndex;
            decisionValue0 =
                tileResolutionManager
                    .PendingTravelFee;
        }
        else if (tileResolutionManager != null &&
                 tileResolutionManager.PendingDevelopmentPlayer != null &&
                 tileResolutionManager.PendingDevelopmentTile != null)
        {
            decisionKind = "development";
            decisionPlayerSlotIndex =
                tileResolutionManager
                    .PendingDevelopmentPlayer
                    .PlayerSlotIndex;
            decisionTileIndex =
                tileResolutionManager
                    .PendingDevelopmentTile
                    .TileIndex;
            decisionReady = true;
        }
        else if (eventCardManager != null &&
                 eventCardManager.IsResolvingEvent &&
                 eventCardManager.CurrentEventPlayer != null &&
                 eventCardManager.CurrentCard != null)
        {
            decisionKind = "event";
            decisionPlayerSlotIndex =
                eventCardManager
                    .CurrentEventPlayer
                    .PlayerSlotIndex;
            decisionReady =
                eventCardManager
                    .EffectExecutionCompleted;
            decisionText0 =
                eventCardManager
                    .CurrentCard
                    .CardId;
            decisionText1 =
                eventCardManager
                    .OnlineResultKind;
            decisionText2 =
                eventCardManager
                    .OnlineResultTextValue;
            decisionValue0 =
                eventCardManager
                    .OnlineResultValue0;
            decisionValue1 =
                eventCardManager
                    .OnlineResultValue1;
            decisionValue2 =
                eventCardManager
                    .OnlineResultValue2;
        }
        else if (specialTileManager != null &&
                 specialTileManager.IsResolvingSpecialTile &&
                 specialTileManager.CurrentSpecialPlayer != null)
        {
            decisionKind = "special";
            decisionPlayerSlotIndex =
                specialTileManager
                    .CurrentSpecialPlayer
                    .PlayerSlotIndex;
            decisionReady = true;
            decisionText0 =
                specialTileManager
                    .OnlinePresentationKind;
            decisionText1 =
                specialTileManager
                    .OnlineFallbackTitle;
            decisionText2 =
                specialTileManager
                    .OnlineFallbackDescription;
            decisionText3 =
                specialTileManager
                    .OnlineFallbackResult;
            decisionValue0 =
                specialTileManager
                    .OnlineValue0;
            decisionValue1 =
                specialTileManager
                    .OnlineValue1;
            decisionValue2 =
                specialTileManager
                    .OnlineValue2;
        }
        else if (auctionManager != null &&
                 auctionManager.IsAuctionActive &&
                 auctionManager.AuctionProperty != null &&
                 auctionManager.CurrentBidder != null)
        {
            decisionKind = "auction";
            decisionPlayerSlotIndex =
                auctionManager
                    .CurrentBidder
                    .PlayerSlotIndex;
            decisionAuxSlotIndex =
                auctionManager.HighestBidder != null
                    ? auctionManager
                        .HighestBidder
                        .PlayerSlotIndex
                    : -1;
            decisionTileIndex =
                auctionManager
                    .AuctionProperty
                    .TileIndex;
            decisionValue0 =
                auctionManager.CurrentBid;
            decisionValue1 =
                auctionManager.MinimumBid;
            decisionValue2 =
                auctionManager.SmallBidStep;
            decisionValue3 =
                auctionManager.LargeBidStep;
            decisionReady = true;
        }

        return new TurnDiceFrame
        {
            phase = phase,
            activeSlotIndex =
                activePlayer != null
                    ? activePlayer.PlayerSlotIndex
                    : -1,
            currentRound =
                Mathf.Max(1, turnManager.CurrentRound),
            roundLimit =
                Mathf.Max(1, turnManager.RoundLimit),
            diceSequence = diceSequence,
            dicePlayerSlotIndex =
                dicePlayerSlotIndex,
            dieOne = lastDieOne,
            dieTwo = lastDieTwo,
            total = lastTotal,
            startingOrderDice =
                lastDiceWasStartingOrder,

            movementSequence =
                movementSequence,
            movementPlayerSlotIndex =
                movementPlayerSlotIndex,
            movementStartTileIndex =
                movementStartTileIndex,
            movementTargetTileIndex =
                movementTargetTileIndex,
            movementSteps =
                movementSteps,
            movementInProgress =
                movementInProgress,
            movementPassedStart =
                movementPassedStart,
            movementUsesSprint =
                movementUsesSprint,
            pawnTileIndices =
                BuildHostPawnTileIndices(),
            playerMoney =
                BuildHostPlayerMoney(),
            tileOwnerSlotIndices =
                BuildHostTileOwnerSlotIndices(),
            tileDevelopmentLevels =
                BuildHostTileDevelopmentLevels(),
            matchResult =
                turnManager.IsMatchFinished &&
                matchResultManager != null
                    ? matchResultManager
                        .BuildOnlineResultSnapshot()
                    : null,
            decisionKind =
                decisionKind,
            decisionPlayerSlotIndex =
                decisionPlayerSlotIndex,
            decisionAuxSlotIndex =
                decisionAuxSlotIndex,
            decisionTileIndex =
                decisionTileIndex,
            decisionValue0 =
                decisionValue0,
            decisionValue1 =
                decisionValue1,
            decisionValue2 =
                decisionValue2,
            decisionValue3 =
                decisionValue3,
            decisionReady =
                decisionReady,
            decisionText0 =
                decisionText0,
            decisionText1 =
                decisionText1,
            decisionText2 =
                decisionText2,
            decisionText3 =
                decisionText3,
            pawnCosmeticIds =
                BuildHostPawnCosmeticIds()
        };
    }

    private int[] BuildHostPawnTileIndices()
    {
        int[] result =
        {
            -1, -1, -1, -1
        };

        foreach (PlayerPawnMover pawn
                 in subscribedPawns)
        {
            if (pawn == null)
            {
                continue;
            }

            PlayerGameState player =
                pawn.GetComponent<PlayerGameState>();

            if (player == null ||
                player.PlayerSlotIndex < 0 ||
                player.PlayerSlotIndex >=
                    result.Length)
            {
                continue;
            }

            result[player.PlayerSlotIndex] =
                pawn.CurrentTileIndex;
        }

        return result;
    }

    private int[] BuildHostPlayerMoney()
    {
        int[] result =
        {
            -1, -1, -1, -1
        };

        if (turnManager == null)
        {
            return result;
        }

        for (int slotIndex = 0;
             slotIndex < result.Length;
             slotIndex++)
        {
            PlayerGameState player =
                turnManager
                    .GetPlayerStateBySlotIndex(
                        slotIndex);

            if (player == null ||
                !player.IsParticipating)
            {
                continue;
            }

            result[slotIndex] =
                Mathf.Max(0, player.CurrentMoney);
        }

        return result;
    }

    private int[] BuildHostTileOwnerSlotIndices()
    {
        ResolveBoardPath();

        if (boardPath == null ||
            boardPath.TileCount <= 0)
        {
            return Array.Empty<int>();
        }

        int[] result =
            new int[boardPath.TileCount];

        for (int index = 0;
             index < result.Length;
             index++)
        {
            result[index] = -1;
        }

        for (int index = 0;
             index < boardPath.TileCount;
             index++)
        {
            BoardTile tile =
                boardPath.GetTile(index);

            if (tile == null ||
                tile.TileIndex < 0 ||
                tile.TileIndex >= result.Length)
            {
                continue;
            }

            result[tile.TileIndex] =
                tile.OwnerPlayerIndex;
        }

        return result;
    }

    private int[] BuildHostTileDevelopmentLevels()
    {
        ResolveBoardPath();
        ResolvePropertyDevelopmentManager();

        if (boardPath == null ||
            boardPath.TileCount <= 0)
        {
            return Array.Empty<int>();
        }

        int[] result =
            new int[boardPath.TileCount];

        if (propertyDevelopmentManager == null)
        {
            return result;
        }

        for (int index = 0;
             index < boardPath.TileCount;
             index++)
        {
            BoardTile tile =
                boardPath.GetTile(index);

            if (tile == null ||
                tile.TileIndex < 0 ||
                tile.TileIndex >= result.Length)
            {
                continue;
            }

            result[tile.TileIndex] =
                propertyDevelopmentManager
                    .GetDevelopmentLevel(tile);
        }

        return result;
    }

    private string[] BuildHostPawnCosmeticIds()
    {
        RefreshHostControlledPawnCosmetics();

        string[] result =
        {
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty
        };

        for (int slotIndex = 0;
             slotIndex < result.Length &&
             slotIndex <
                authoritativePawnCosmeticIds.Length;
             slotIndex++)
        {
            result[slotIndex] =
                authoritativePawnCosmeticIds[slotIndex] ??
                string.Empty;
        }

        return result;
    }

    private void ResolveBoardPath()
    {
        if (boardPath != null)
        {
            return;
        }

        BoardPath[] paths =
            Resources.FindObjectsOfTypeAll<
                BoardPath>();

        foreach (BoardPath candidate
                 in paths)
        {
            if (candidate != null &&
                candidate.gameObject.scene.IsValid())
            {
                boardPath = candidate;
                return;
            }
        }
    }

    private string DetermineHostPhase()
    {
        if (turnManager.IsMatchFinished)
        {
            return "match_complete";
        }

        if (turnManager.IsStartingOrderPhase)
        {
            return turnManager.IsResolvingDiceVisual
                ? "dice_resolving"
                : "starting_order";
        }

        if (turnManager.IsPlayingPhase)
        {
            if (turnManager.IsResolvingDiceVisual)
            {
                return "dice_resolving";
            }

            ResolveTileResolutionManager();

            if (tileResolutionManager != null &&
                tileResolutionManager.PendingPurchasePlayer != null &&
                tileResolutionManager.PendingPurchaseTile != null)
            {
                return "awaiting_decision";
            }

            if (turnManager.IsWaitingForMovement)
            {
                return "movement";
            }

            return "awaiting_roll";
        }

        return "starting";
    }

    private void HandleMatchSnapshotChanged(
        AtlasMatchNetworkSnapshot snapshot)
    {
        if (!prepared ||
            snapshot == null ||
            turnManager == null)
        {
            return;
        }

        ApplyOnlineSeatMetadata(snapshot);

        if (localIsHost ||
            string.IsNullOrWhiteSpace(
                snapshot.SnapshotJson))
        {
            return;
        }

        TurnDiceFrame frame;

        try
        {
            frame =
                JsonUtility.FromJson<TurnDiceFrame>(
                    snapshot.SnapshotJson);
        }
        catch
        {
            return;
        }

        if (frame == null ||
            frame.schemaVersion < 1 ||
            frame.schemaVersion > 7)
        {
            return;
        }

        turnManager.ApplyOnlineFollowerTurnDiceFrame(
            frame.phase,
            frame.activeSlotIndex,
            frame.currentRound,
            frame.diceSequence,
            frame.dicePlayerSlotIndex,
            frame.dieOne,
            frame.dieTwo,
            frame.total,
            frame.startingOrderDice);

        if (frame.schemaVersion >= 2)
        {
            QueueFollowerMovementFrame(frame);
        }

        if (frame.schemaVersion >= 3)
        {
            ApplyFollowerEconomyFrame(frame);
        }

        if (frame.schemaVersion >= 4)
        {
            ApplyFollowerDecisionFrame(frame);
            ApplyFollowerPawnCosmetics(frame);
        }

        if (frame.schemaVersion >= 6)
        {
            ApplyFollowerDevelopmentFrame(frame);
            ApplyFollowerMatchResultFrame(frame);
        }
    }

    private void ApplyFollowerDecisionFrame(
        TurnDiceFrame frame)
    {
        ResolveTileResolutionManager();
        ResolveSpecialTileManager();
        ResolveEventCardManager();
        ResolveAuctionManager();
        ResolveTradeManager();
        ResolveBoardPath();

        if (frame == null)
        {
            return;
        }

        string kind =
            frame.decisionKind ?? string.Empty;

        bool locallyOwnedDecision =
            locallyControlledHumanSlots.Contains(
                frame.decisionPlayerSlotIndex);

        PlayerGameState player =
            turnManager != null
                ? turnManager
                    .GetPlayerStateBySlotIndex(
                        frame.decisionPlayerSlotIndex)
                : null;

        bool tripleDoublePenalty =
            string.Equals(
                kind,
                "triple_double_penalty",
                StringComparison.Ordinal) &&
            locallyOwnedDecision;

        turnManager?.ApplyOnlineFollowerTripleDoublePenalty(
            tripleDoublePenalty,
            frame.decisionPlayerSlotIndex);

        // PURCHASE ---------------------------------------------------------
        if (!string.Equals(
                kind,
                "purchase",
                StringComparison.Ordinal) ||
            !locallyOwnedDecision)
        {
            tileResolutionManager?
                .ClearOnlineRemotePurchaseDecision();
        }
        else if (tileResolutionManager != null)
        {
            BoardTile tile =
                GetBoardTile(frame.decisionTileIndex);

            if (player == null ||
                tile == null)
            {
                tileResolutionManager
                    .ClearOnlineRemotePurchaseDecision();
            }
            else
            {
                bool same =
                    tileResolutionManager
                        .IsRemotePurchasePresentation &&
                    tileResolutionManager
                        .PendingPurchasePlayer != null &&
                    tileResolutionManager
                        .PendingPurchaseTile != null &&
                    tileResolutionManager
                        .PendingPurchasePlayer
                        .PlayerSlotIndex ==
                            frame.decisionPlayerSlotIndex &&
                    tileResolutionManager
                        .PendingPurchaseTile
                        .TileIndex ==
                            frame.decisionTileIndex;

                if (!same)
                {
                    tileResolutionManager
                        .ShowOnlineRemotePurchaseDecision(
                            player,
                            tile);
                }
            }
        }

        // TRAVEL -----------------------------------------------------------
        if (!string.Equals(
                kind,
                "travel",
                StringComparison.Ordinal) ||
            !locallyOwnedDecision)
        {
            tileResolutionManager?
                .ClearOnlineRemoteTravelDecision();
        }
        else if (tileResolutionManager != null &&
                 player != null)
        {
            bool same =
                tileResolutionManager
                    .IsRemoteTravelPresentation &&
                tileResolutionManager
                    .PendingTravelPlayer != null &&
                tileResolutionManager
                    .PendingTravelPlayer
                    .PlayerSlotIndex ==
                        frame.decisionPlayerSlotIndex &&
                tileResolutionManager
                    .PendingTravelTargetIndex ==
                        frame.decisionTileIndex &&
                tileResolutionManager
                    .PendingTravelFee ==
                        frame.decisionValue0;

            if (!same)
            {
                tileResolutionManager
                    .ShowOnlineRemoteTravelDecision(
                        player,
                        frame.decisionTileIndex,
                        frame.decisionValue0);
            }
        }

        // DEVELOPMENT ------------------------------------------------------
        if (!string.Equals(
                kind,
                "development",
                StringComparison.Ordinal) ||
            !locallyOwnedDecision)
        {
            tileResolutionManager?
                .ClearOnlineRemoteDevelopmentDecision();
        }
        else if (tileResolutionManager != null &&
                 player != null)
        {
            BoardTile tile =
                GetBoardTile(frame.decisionTileIndex);

            bool same =
                tile != null &&
                tileResolutionManager
                    .IsRemoteDevelopmentPresentation &&
                tileResolutionManager
                    .PendingDevelopmentPlayer != null &&
                tileResolutionManager
                    .PendingDevelopmentTile != null &&
                tileResolutionManager
                    .PendingDevelopmentPlayer
                    .PlayerSlotIndex ==
                        frame.decisionPlayerSlotIndex &&
                tileResolutionManager
                    .PendingDevelopmentTile
                    .TileIndex ==
                        frame.decisionTileIndex;

            if (!same)
            {
                if (tile == null)
                {
                    tileResolutionManager
                        .ClearOnlineRemoteDevelopmentDecision();
                }
                else
                {
                    tileResolutionManager
                        .ShowOnlineRemoteDevelopmentDecision(
                            player,
                            tile);
                }
            }
        }

        // TRADE ------------------------------------------------------------
        bool localTradeParticipant =
            locallyControlledHumanSlots.Contains(
                frame.decisionPlayerSlotIndex) ||
            locallyControlledHumanSlots.Contains(
                frame.decisionAuxSlotIndex);

        if (!string.Equals(
                kind,
                "trade",
                StringComparison.Ordinal) ||
            !localTradeParticipant)
        {
            tradeManager?
                .ClearOnlineRemoteTradeState();
        }
        else if (tradeManager != null &&
                 turnManager != null)
        {
            PlayerGameState tradeTarget =
                turnManager.GetPlayerStateBySlotIndex(
                    frame.decisionPlayerSlotIndex);
            PlayerGameState tradeInitiator =
                turnManager.GetPlayerStateBySlotIndex(
                    frame.decisionAuxSlotIndex);
            BoardTile offeredTile =
                frame.decisionTileIndex >= 0
                    ? GetBoardTile(frame.decisionTileIndex)
                    : null;
            BoardTile requestedTile =
                frame.decisionValue1 >= 0
                    ? GetBoardTile(frame.decisionValue1)
                    : null;

            if (tradeTarget == null ||
                tradeInitiator == null)
            {
                tradeManager.ClearOnlineRemoteTradeState();
            }
            else
            {
                tradeManager.ShowOnlineRemoteTradeState(
                    tradeInitiator,
                    tradeTarget,
                    offeredTile,
                    frame.decisionValue0,
                    requestedTile,
                    frame.decisionValue2);
            }
        }

        // EVENT ------------------------------------------------------------
        if (!string.Equals(
                kind,
                "event",
                StringComparison.Ordinal) ||
            !locallyOwnedDecision)
        {
            eventCardManager?
                .ClearOnlineRemoteEventDecision();
        }
        else if (eventCardManager != null &&
                 player != null)
        {
            eventCardManager
                .ShowOnlineRemoteEventDecision(
                    player,
                    frame.decisionText0,
                    frame.decisionReady,
                    frame.decisionText1,
                    frame.decisionValue0,
                    frame.decisionValue1,
                    frame.decisionValue2,
                    frame.decisionText2);
        }

        // SPECIAL RESULT / CONTINUE ---------------------------------------
        if (!string.Equals(
                kind,
                "special",
                StringComparison.Ordinal) ||
            !locallyOwnedDecision)
        {
            specialTileManager?
                .ClearOnlineRemoteSpecialDecision();
        }
        else if (specialTileManager != null &&
                 player != null)
        {
            specialTileManager
                .ShowOnlineRemoteSpecialDecision(
                    player,
                    frame.decisionText0,
                    frame.decisionValue0,
                    frame.decisionValue1,
                    frame.decisionValue2,
                    frame.decisionText1,
                    frame.decisionText2,
                    frame.decisionText3);
        }

        // AUCTION is shared presentation. Every Remote client may watch it,
        // but AuctionManager only enables buttons when the current bidder is
        // one of this device's locally-owned Human seats.
        if (!string.Equals(
                kind,
                "auction",
                StringComparison.Ordinal))
        {
            auctionManager?
                .ClearOnlineRemoteAuctionState();
        }
        else if (auctionManager != null)
        {
            BoardTile property =
                GetBoardTile(frame.decisionTileIndex);

            PlayerGameState currentBidder =
                turnManager != null
                    ? turnManager
                        .GetPlayerStateBySlotIndex(
                            frame.decisionPlayerSlotIndex)
                    : null;

            PlayerGameState highestBidder =
                frame.decisionAuxSlotIndex >= 0 &&
                turnManager != null
                    ? turnManager
                        .GetPlayerStateBySlotIndex(
                            frame.decisionAuxSlotIndex)
                    : null;

            if (property == null ||
                currentBidder == null)
            {
                auctionManager
                    .ClearOnlineRemoteAuctionState();
            }
            else
            {
                auctionManager
                    .ShowOnlineRemoteAuctionState(
                        property,
                        currentBidder,
                        highestBidder,
                        frame.decisionValue0,
                        frame.decisionValue1,
                        frame.decisionValue2,
                        frame.decisionValue3);
            }
        }
    }

    private void ApplyFollowerDevelopmentFrame(
        TurnDiceFrame frame)
    {
        if (localIsHost ||
            frame == null ||
            frame.tileDevelopmentLevels == null)
        {
            return;
        }

        ResolveBoardPath();
        ResolvePropertyDevelopmentManager();

        if (boardPath == null ||
            propertyDevelopmentManager == null)
        {
            return;
        }

        int count =
            Mathf.Min(
                boardPath.TileCount,
                frame.tileDevelopmentLevels.Length);

        for (int index = 0;
             index < count;
             index++)
        {
            BoardTile tile =
                boardPath.GetTile(index);

            if (tile == null)
            {
                continue;
            }

            int authoritativeOwnerSlot =
                frame.tileOwnerSlotIndices != null &&
                index < frame.tileOwnerSlotIndices.Length
                    ? frame.tileOwnerSlotIndices[index]
                    : tile.OwnerPlayerIndex;

            PlayerGameState owner =
                authoritativeOwnerSlot >= 0 &&
                turnManager != null
                    ? turnManager
                        .GetPlayerStateBySlotIndex(
                            authoritativeOwnerSlot)
                    : null;

            propertyDevelopmentManager
                .ApplyOnlineAuthoritativeDevelopmentLevel(
                    tile,
                    frame.tileDevelopmentLevels[index],
                    owner);
        }
    }

    private void ApplyFollowerMatchResultFrame(
        TurnDiceFrame frame)
    {
        if (localIsHost ||
            frame == null ||
            frame.matchResult == null ||
            !frame.matchResult.valid)
        {
            return;
        }

        ResolveMatchResultManager();

        if (matchResultManager != null &&
            !matchResultManager.ResultShown)
        {
            matchResultManager.ShowOnlineMatchResult(
                frame.matchResult,
                localIsHost: false);
        }
    }

    private BoardTile GetBoardTile(
        int tileIndex)
    {
        return boardPath != null &&
               tileIndex >= 0 &&
               tileIndex < boardPath.TileCount
            ? boardPath.GetTile(tileIndex)
            : null;
    }

    private void ApplyFollowerPawnCosmetics(
        TurnDiceFrame frame)
    {
        if (frame == null ||
            frame.pawnCosmeticIds == null)
        {
            return;
        }

        int count =
            Mathf.Min(
                4,
                frame.pawnCosmeticIds.Length);

        for (int slotIndex = 0;
             slotIndex < count;
             slotIndex++)
        {
            string cosmeticId =
                frame.pawnCosmeticIds[slotIndex];

            if (string.IsNullOrWhiteSpace(
                    cosmeticId))
            {
                continue;
            }

            if (string.Equals(
                    lastAppliedFollowerPawnCosmeticIds[slotIndex],
                    cosmeticId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (ApplyPawnCosmeticToSlot(
                    slotIndex,
                    cosmeticId))
            {
                lastAppliedFollowerPawnCosmeticIds[slotIndex] =
                    cosmeticId;
            }
        }
    }

    private void ApplyFollowerEconomyFrame(
        TurnDiceFrame frame)
    {
        if (localIsHost ||
            frame == null ||
            turnManager == null)
        {
            return;
        }

        if (frame.playerMoney != null)
        {
            int playerCount =
                Mathf.Min(
                    4,
                    frame.playerMoney.Length);

            for (int slotIndex = 0;
                 slotIndex < playerCount;
                 slotIndex++)
            {
                int authoritativeMoney =
                    frame.playerMoney[slotIndex];

                if (authoritativeMoney < 0)
                {
                    continue;
                }

                PlayerGameState player =
                    turnManager
                        .GetPlayerStateBySlotIndex(
                            slotIndex);

                player?.ApplyOnlineAuthoritativeMoney(
                    authoritativeMoney);
            }
        }

        ResolveBoardPath();

        if (boardPath == null ||
            frame.tileOwnerSlotIndices == null ||
            frame.tileOwnerSlotIndices.Length == 0)
        {
            return;
        }

        int tileCount =
            Mathf.Min(
                boardPath.TileCount,
                frame.tileOwnerSlotIndices.Length);

        for (int tileIndex = 0;
             tileIndex < tileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile == null ||
                tile.TileIndex != tileIndex)
            {
                continue;
            }

            int ownerSlotIndex =
                frame.tileOwnerSlotIndices[tileIndex];

            Material ownerMaterial = null;

            if (ownerSlotIndex >= 0)
            {
                PlayerGameState owner =
                    turnManager
                        .GetPlayerStateBySlotIndex(
                            ownerSlotIndex);

                ownerMaterial =
                    owner != null
                        ? owner.OwnershipMaterial
                        : null;
            }

            tile.ApplyOnlineAuthoritativeOwner(
                ownerSlotIndex,
                ownerMaterial);
        }
    }

    private void QueueFollowerMovementFrame(
        TurnDiceFrame frame)
    {
        if (frame == null)
        {
            return;
        }

        if (frame.pawnTileIndices != null &&
            frame.pawnTileIndices.Length > 0)
        {
            latestFollowerPawnTileIndices =
                new int[frame.pawnTileIndices.Length];

            Array.Copy(
                frame.pawnTileIndices,
                latestFollowerPawnTileIndices,
                frame.pawnTileIndices.Length);
        }

        if (followerNeedsInitialCheckpointSnap)
        {
            followerNeedsInitialCheckpointSnap = false;
            lastQueuedFollowerMovementSequence =
                Mathf.Max(
                    lastQueuedFollowerMovementSequence,
                    frame.movementSequence);
            followerMovementQueue.Clear();
            followerMovementVisualActive = false;
            ApplyFollowerPositionCorrections();
            return;
        }

        if (frame.movementSequence <=
                lastQueuedFollowerMovementSequence ||
            frame.movementPlayerSlotIndex < 0 ||
            frame.movementSteps <= 0 ||
            frame.movementStartTileIndex < 0 ||
            frame.movementTargetTileIndex < 0)
        {
            return;
        }

        followerMovementQueue.Enqueue(
            new FollowerMovementCommand
            {
                Sequence =
                    frame.movementSequence,
                PlayerSlotIndex =
                    frame.movementPlayerSlotIndex,
                StartTileIndex =
                    frame.movementStartTileIndex,
                TargetTileIndex =
                    frame.movementTargetTileIndex,
                Steps =
                    frame.movementSteps,
                PassedStart =
                    frame.movementPassedStart,
                UsesSprint =
                    frame.movementUsesSprint
            });

        lastQueuedFollowerMovementSequence =
            frame.movementSequence;
    }

    private void ProcessFollowerMovementQueue()
    {
        if (localIsHost ||
            followerMovementVisualActive ||
            followerMovementQueue.Count == 0 ||
            (turnManager != null &&
             turnManager.IsResolvingDiceVisual))
        {
            return;
        }

        FollowerMovementCommand command =
            followerMovementQueue.Peek();

        PlayerPawnMover pawn =
            FindPawnBySlotIndex(
                command.PlayerSlotIndex);

        if (pawn == null ||
            pawn.IsMoving)
        {
            return;
        }

        followerMovementQueue.Dequeue();

        if (pawn.CurrentTileIndex ==
            command.TargetTileIndex)
        {
            ApplyFollowerPositionCorrections();
            return;
        }

        bool started =
            pawn.PlayOnlineFollowerMovement(
                command.StartTileIndex,
                command.Steps,
                command.TargetTileIndex,
                command.UsesSprint,
                completedPawn =>
                {
                    followerMovementVisualActive = false;
                    ApplyFollowerPositionCorrections();
                });

        if (!started)
        {
            // Put the command back at the front logically by rebuilding the
            // queue. This should be rare and only occurs if another local visual
            // animation momentarily owns the pawn.
            Queue<FollowerMovementCommand> rebuilt =
                new Queue<FollowerMovementCommand>();

            rebuilt.Enqueue(command);

            while (followerMovementQueue.Count > 0)
            {
                rebuilt.Enqueue(
                    followerMovementQueue.Dequeue());
            }

            while (rebuilt.Count > 0)
            {
                followerMovementQueue.Enqueue(
                    rebuilt.Dequeue());
            }

            return;
        }

        followerMovementVisualActive = true;

        Debug.Log(
            $"Phase 5C follower movement — " +
            $"P{command.PlayerSlotIndex + 1}: " +
            $"tile {command.StartTileIndex} -> " +
            $"{command.TargetTileIndex}, " +
            $"steps={command.Steps}, " +
            $"passedStart={command.PassedStart}, " +
            $"sequence={command.Sequence}.",
            this);
    }

    private void ApplyFollowerPositionCorrections()
    {
        if (localIsHost ||
            latestFollowerPawnTileIndices == null ||
            followerMovementVisualActive ||
            followerMovementQueue.Count > 0)
        {
            return;
        }

        int count =
            Mathf.Min(
                4,
                latestFollowerPawnTileIndices.Length);

        for (int slotIndex = 0;
             slotIndex < count;
             slotIndex++)
        {
            int tileIndex =
                latestFollowerPawnTileIndices[slotIndex];

            if (tileIndex < 0)
            {
                continue;
            }

            PlayerPawnMover pawn =
                FindPawnBySlotIndex(slotIndex);

            if (pawn == null ||
                pawn.IsMoving)
            {
                continue;
            }

            pawn.SyncOnlineFollowerTileIndex(
                tileIndex);
        }
    }

    private PlayerPawnMover FindPawnBySlotIndex(
        int stableSlotIndex)
    {
        foreach (PlayerPawnMover pawn
                 in subscribedPawns)
        {
            if (pawn == null)
            {
                continue;
            }

            PlayerGameState player =
                pawn.GetComponent<PlayerGameState>();

            if (player != null &&
                player.PlayerSlotIndex ==
                    stableSlotIndex)
            {
                return pawn;
            }
        }

        return null;
    }

    private void InitializeAuthoritativePawnCosmetics()
    {
        authoritativePawnCosmeticIds =
            new[]
            {
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty
            };

        if (!localIsHost)
        {
            return;
        }

        if (startLobbySnapshot != null &&
            startLobbySnapshot.Members != null)
        {
            foreach (AtlasLobbyMemberSnapshot member
                     in startLobbySnapshot.Members)
            {
                if (member == null ||
                    !member.Active ||
                    member.SlotIndex < 0 ||
                    member.SlotIndex >=
                        authoritativePawnCosmeticIds.Length ||
                    string.IsNullOrWhiteSpace(
                        member.PawnCosmeticId))
                {
                    continue;
                }

                authoritativePawnCosmeticIds[
                    member.SlotIndex] =
                        member.PawnCosmeticId;

                ApplyPawnCosmeticToSlot(
                    member.SlotIndex,
                    member.PawnCosmeticId);
            }
        }

        RefreshHostControlledPawnCosmetics();
    }

    private void RefreshHostControlledPawnCosmetics()
    {
        if (!localIsHost ||
            authoritativePawnCosmeticIds == null ||
            authoritativePawnCosmeticIds.Length < 4)
        {
            return;
        }

        for (int slotIndex = 0;
             slotIndex < 4;
             slotIndex++)
        {
            if (IsRemoteHumanSlot(slotIndex))
            {
                // The owning Remote client supplies this value through an
                // authenticated match intent. Never replace it with Host-local
                // PlayerPrefs for the same stable seat.
                continue;
            }

            string cosmeticId =
                GetCurrentPawnCosmeticId(
                    slotIndex);

            if (!string.IsNullOrWhiteSpace(
                    cosmeticId))
            {
                authoritativePawnCosmeticIds[
                    slotIndex] =
                        cosmeticId;
            }
        }
    }

    private bool IsRemoteHumanSlot(
        int slotIndex)
    {
        if (startLobbySnapshot == null ||
            startLobbySnapshot.Members == null)
        {
            return false;
        }

        AtlasLobbyMemberSnapshot member =
            startLobbySnapshot.Members
                .FirstOrDefault(
                    item =>
                        item != null &&
                        item.Active &&
                        item.SlotIndex ==
                            slotIndex);

        return member != null &&
               member.SeatMode ==
                   AtlasLobbySeatMode.RemoteHuman;
    }

    private string GetCurrentPawnCosmeticId(
        int slotIndex)
    {
        PlayerPawnMover pawn =
            FindPawnBySlotIndex(
                slotIndex);

        PawnCosmeticApplier applier =
            pawn != null
                ? pawn.GetComponent<
                    PawnCosmeticApplier>()
                : null;

        if (applier != null &&
            applier.CurrentCosmetic != null &&
            !string.IsNullOrWhiteSpace(
                applier.CurrentCosmetic.CosmeticId))
        {
            return applier
                .CurrentCosmetic
                .CosmeticId;
        }

        AtlasBoardPawnCosmeticService service =
            AtlasBoardPawnCosmeticService.Instance;

        PawnCosmeticDefinition fallback =
            service != null
                ? service.GetSelectedCosmetic(
                    slotIndex)
                : null;

        return fallback != null
            ? fallback.CosmeticId
            : string.Empty;
    }

    private bool ApplyPawnCosmeticToSlot(
        int slotIndex,
        string cosmeticId)
    {
        if (slotIndex < 0 ||
            slotIndex >= 4 ||
            string.IsNullOrWhiteSpace(
                cosmeticId))
        {
            return false;
        }

        PlayerPawnMover pawn =
            FindPawnBySlotIndex(
                slotIndex);

        PawnCosmeticApplier applier =
            pawn != null
                ? pawn.GetComponent<
                    PawnCosmeticApplier>()
                : null;

        return applier != null &&
               applier
                   .ApplyOnlineAuthoritativeCosmeticId(
                       cosmeticId);
    }

    private void TryAcceptRemotePawnCosmetic(
        int slotIndex,
        string cosmeticId)
    {
        if (!localIsHost ||
            slotIndex < 0 ||
            slotIndex >= 4 ||
            !IsRemoteHumanSlot(slotIndex) ||
            string.IsNullOrWhiteSpace(
                cosmeticId))
        {
            return;
        }

        AtlasBoardPawnCosmeticService service =
            AtlasBoardPawnCosmeticService.Instance;

        if (service == null ||
            service.Catalog == null ||
            service.Catalog.FindById(
                cosmeticId) == null)
        {
            Debug.LogWarning(
                $"Phase 5E rejected unknown pawn cosmetic '{cosmeticId}' " +
                $"for P{slotIndex + 1}.",
                this);

            return;
        }

        authoritativePawnCosmeticIds[
            slotIndex] =
                cosmeticId;

        ApplyPawnCosmeticToSlot(
            slotIndex,
            cosmeticId);

        nextPublishCheckAt = 0f;

        Debug.Log(
            $"Phase 5E Host accepted pawn cosmetic '{cosmeticId}' " +
            $"for Remote P{slotIndex + 1}.",
            this);
    }

    private async Task
        SubmitRemotePawnCosmeticsIfNeededAsync()
    {
        if (localIsHost ||
            remoteCosmeticSubmitInFlight ||
            matchBridge == null ||
            locallyControlledHumanSlots.Count == 0)
        {
            return;
        }

        int slotToSubmit = -1;

        foreach (int slotIndex
                 in locallyControlledHumanSlots)
        {
            if (!submittedRemoteCosmeticSlots
                    .Contains(slotIndex))
            {
                slotToSubmit = slotIndex;
                break;
            }
        }

        if (slotToSubmit < 0)
        {
            nextRemoteCosmeticSubmitAt =
                float.PositiveInfinity;

            return;
        }

        string cosmeticId =
            GetCurrentPawnCosmeticId(
                slotToSubmit);

        if (string.IsNullOrWhiteSpace(
                cosmeticId))
        {
            nextRemoteCosmeticSubmitAt =
                Time.unscaledTime + 1f;

            return;
        }

        remoteCosmeticSubmitInFlight = true;

        try
        {
            DecisionIntentPayload payload =
                new DecisionIntentPayload
                {
                    kind = "pawn_cosmetic",
                    action = "sync",
                    slotIndex =
                        slotToSubmit,
                    tileIndex = -1,
                    value = cosmeticId
                };

            AtlasMatchNetworkResult result =
                await matchBridge.SubmitIntentAsync(
                    "submit_decision",
                    JsonUtility.ToJson(payload),
                    "pawn-cosmetic-" +
                    Guid.NewGuid().ToString("N"));

            if (result.Success)
            {
                submittedRemoteCosmeticSlots.Add(
                    slotToSubmit);

                nextRemoteCosmeticSubmitAt = 0f;

                Debug.Log(
                    $"Phase 5E Remote submitted pawn cosmetic '{cosmeticId}' " +
                    $"for P{slotToSubmit + 1}.",
                    this);
            }
            else
            {
                nextRemoteCosmeticSubmitAt =
                    Time.unscaledTime + 1f;

                Debug.LogWarning(
                    "Phase 5E Remote pawn cosmetic sync failed: " +
                    result.TechnicalMessage,
                    this);
            }
        }
        finally
        {
            remoteCosmeticSubmitInFlight = false;
        }
    }

    private async void
        HandleRemotePurchaseDecisionRequested(
            int slotIndex,
            int tileIndex,
            bool buyProperty)
    {
        if (!prepared ||
            localIsHost ||
            matchBridge == null ||
            !locallyControlledHumanSlots
                .Contains(slotIndex))
        {
            tileResolutionManager?
                .NotifyOnlineRemotePurchaseSubmitFailed();

            return;
        }

        DecisionIntentPayload payload =
            new DecisionIntentPayload
            {
                kind = "purchase",
                action =
                    buyProperty
                        ? "buy"
                        : "skip",
                slotIndex =
                    slotIndex,
                tileIndex =
                    tileIndex,
                value =
                    string.Empty
            };

        AtlasMatchNetworkResult result =
            await matchBridge.SubmitIntentAsync(
                "submit_decision",
                JsonUtility.ToJson(payload),
                "purchase-" +
                Guid.NewGuid().ToString("N"));

        if (!result.Success)
        {
            tileResolutionManager?
                .NotifyOnlineRemotePurchaseSubmitFailed();

            Debug.LogWarning(
                "Phase 5E Remote purchase decision failed: " +
                result.TechnicalMessage,
                this);

            return;
        }

        Debug.Log(
            $"Phase 5E Remote submitted " +
            $"{(buyProperty ? "BUY" : "SKIP")} for " +
            $"P{slotIndex + 1}, tile {tileIndex}.",
            this);
    }

    private async void HandleRemoteTravelDecisionRequested(
        int slotIndex,
        int targetTileIndex,
        bool shouldTravel)
    {
        if (!CanSubmitRemoteDecision(slotIndex))
        {
            tileResolutionManager?
                .NotifyOnlineRemoteTravelSubmitFailed();
            return;
        }

        AtlasMatchNetworkResult result =
            await SubmitDecisionAsync(
                "travel",
                shouldTravel ? "go" : "stay",
                slotIndex,
                targetTileIndex,
                string.Empty,
                "travel");

        if (!result.Success)
        {
            tileResolutionManager?
                .NotifyOnlineRemoteTravelSubmitFailed();

            Debug.LogWarning(
                "Phase 5E Remote Travel decision failed: " +
                result.TechnicalMessage,
                this);
        }
    }

    private async void HandleRemoteSpecialContinueRequested(
        int slotIndex)
    {
        if (!CanSubmitRemoteDecision(slotIndex))
        {
            specialTileManager?
                .NotifyOnlineRemoteContinueSubmitFailed();
            return;
        }

        AtlasMatchNetworkResult result =
            await SubmitDecisionAsync(
                "special",
                "continue",
                slotIndex,
                -1,
                string.Empty,
                "special");

        if (!result.Success)
        {
            specialTileManager?
                .NotifyOnlineRemoteContinueSubmitFailed();

            Debug.LogWarning(
                "Phase 5E Remote Special Continue failed: " +
                result.TechnicalMessage,
                this);
        }
    }

    private async void HandleRemoteEventContinueRequested(
        int slotIndex)
    {
        if (!CanSubmitRemoteDecision(slotIndex))
        {
            eventCardManager?
                .NotifyOnlineRemoteContinueSubmitFailed();
            return;
        }

        AtlasMatchNetworkResult result =
            await SubmitDecisionAsync(
                "event",
                "continue",
                slotIndex,
                -1,
                string.Empty,
                "event");

        if (!result.Success)
        {
            eventCardManager?
                .NotifyOnlineRemoteContinueSubmitFailed();

            Debug.LogWarning(
                "Phase 5E Remote Event Continue failed: " +
                result.TechnicalMessage,
                this);
        }
    }

    private async void HandleRemoteAuctionDecisionRequested(
        int slotIndex,
        string action)
    {
        if (!CanSubmitRemoteDecision(slotIndex) ||
            auctionManager == null ||
            auctionManager.AuctionProperty == null)
        {
            auctionManager?
                .NotifyOnlineRemoteAuctionSubmitFailed();
            return;
        }

        int tileIndex =
            auctionManager.AuctionProperty.TileIndex;

        AtlasMatchNetworkResult result =
            await SubmitDecisionAsync(
                "auction",
                action,
                slotIndex,
                tileIndex,
                string.Empty,
                "auction");

        if (!result.Success)
        {
            auctionManager?
                .NotifyOnlineRemoteAuctionSubmitFailed();

            Debug.LogWarning(
                "Phase 5E Remote Auction decision failed: " +
                result.TechnicalMessage,
                this);
        }
    }

    private async void HandleRemoteDevelopmentDecisionRequested(
        int slotIndex,
        int tileIndex,
        bool shouldDevelop)
    {
        if (!CanSubmitRemoteDecision(slotIndex))
        {
            tileResolutionManager?
                .NotifyOnlineRemoteDevelopmentSubmitFailed();
            return;
        }

        AtlasMatchNetworkResult result =
            await SubmitDecisionAsync(
                "development",
                shouldDevelop ? "develop" : "skip",
                slotIndex,
                tileIndex,
                string.Empty,
                "development");

        if (!result.Success)
        {
            tileResolutionManager?
                .NotifyOnlineRemoteDevelopmentSubmitFailed();

            Debug.LogWarning(
                "Phase 5F Remote Development decision failed: " +
                result.TechnicalMessage,
                this);
        }
    }

    private async void HandleRemoteTradeWindowChanged(
        int slotIndex,
        bool open)
    {
        if (!CanSubmitRemoteDecision(slotIndex) ||
            matchBridge == null)
        {
            return;
        }

        AtlasMatchNetworkResult result =
            await SubmitDecisionAsync(
                "trade_hold",
                open ? "open" : "close",
                slotIndex,
                -1,
                string.Empty,
                "trade-hold");

        if (!result.Success)
        {
            Debug.LogWarning(
                "Phase 5F Remote Trade AFK hold update failed: " +
                result.TechnicalMessage,
                this);
        }
    }

    private async void HandleRemoteTradeOfferRequested(
        int initiatorSlotIndex,
        int targetSlotIndex,
        int offeredTileIndex,
        int offeredCash,
        int requestedTileIndex,
        int requestedCash)
    {
        if (!CanSubmitRemoteDecision(initiatorSlotIndex) ||
            tradeManager == null)
        {
            tradeManager?
                .NotifyOnlineRemoteTradeSubmitFailed();
            return;
        }

        DecisionIntentPayload payload =
            new DecisionIntentPayload
            {
                kind = "trade",
                action = "offer",
                slotIndex = initiatorSlotIndex,
                tileIndex = -1,
                targetSlotIndex = targetSlotIndex,
                offeredTileIndex = offeredTileIndex,
                offeredCash = Mathf.Max(0, offeredCash),
                requestedTileIndex = requestedTileIndex,
                requestedCash = Mathf.Max(0, requestedCash),
                value = string.Empty
            };

        AtlasMatchNetworkResult result =
            await matchBridge.SubmitIntentAsync(
                "submit_decision",
                JsonUtility.ToJson(payload),
                "trade-offer-" +
                Guid.NewGuid().ToString("N"));

        if (!result.Success)
        {
            tradeManager.NotifyOnlineRemoteTradeSubmitFailed();
            Debug.LogWarning(
                "Phase 5F Remote Trade offer failed: " +
                result.TechnicalMessage,
                this);
        }
    }

    private async void HandleRemoteTradeResponseRequested(
        int slotIndex,
        bool acceptOffer)
    {
        if (!CanSubmitRemoteDecision(slotIndex) ||
            tradeManager == null)
        {
            tradeManager?
                .NotifyOnlineRemoteTradeSubmitFailed();
            return;
        }

        AtlasMatchNetworkResult result =
            await SubmitDecisionAsync(
                "trade",
                acceptOffer ? "accept" : "reject",
                slotIndex,
                -1,
                string.Empty,
                "trade-response");

        if (!result.Success)
        {
            tradeManager.NotifyOnlineRemoteTradeSubmitFailed();
            Debug.LogWarning(
                "Phase 5F Remote Trade response failed: " +
                result.TechnicalMessage,
                this);
        }
    }

    private bool CanSubmitRemoteDecision(
        int slotIndex)
    {
        return prepared &&
               !localIsHost &&
               matchBridge != null &&
               locallyControlledHumanSlots.Contains(
                   slotIndex);
    }

    private Task<AtlasMatchNetworkResult> SubmitDecisionAsync(
        string kind,
        string action,
        int slotIndex,
        int tileIndex,
        string value,
        string commandPrefix)
    {
        DecisionIntentPayload payload =
            new DecisionIntentPayload
            {
                kind = kind ?? string.Empty,
                action = action ?? string.Empty,
                slotIndex = slotIndex,
                tileIndex = tileIndex,
                value = value ?? string.Empty
            };

        return matchBridge.SubmitIntentAsync(
            "submit_decision",
            JsonUtility.ToJson(payload),
            commandPrefix + "-" +
            Guid.NewGuid().ToString("N"));
    }

    public void RequestOnlineRematch()
    {
        if (!prepared ||
            !localIsHost ||
            rematchRequestInFlight ||
            matchBridge == null)
        {
            return;
        }

        RequestOnlineRematchAsync();
    }

    private async void RequestOnlineRematchAsync()
    {
        rematchRequestInFlight = true;

        try
        {
            AtlasMatchNetworkResult result =
                await matchBridge.HostPrepareRematchAsync();

            if (!result.Success)
            {
                ResolveMatchResultManager();
                matchResultManager?
                    .NotifyOnlineRematchRequestFailed();

                Debug.LogWarning(
                    "Phase 5F synchronized Rematch request failed: " +
                    result.TechnicalMessage,
                    this);
                return;
            }

            Debug.Log(
                "Phase 5F Host prepared the existing lobby for a synchronized rematch.",
                this);
        }
        finally
        {
            rematchRequestInFlight = false;
        }
    }

    public bool TryHandleLeaveMatch()
    {
        if (!prepared || leaveRequestInFlight)
        {
            return false;
        }

        if (localIsHost)
        {
            Debug.LogWarning(
                "Host Leave Match is blocked until Host Migration is implemented. " +
                "RemoteHuman clients can leave and receive a five-minute reclaim window.",
                this);
            return true;
        }

        LeaveActiveMatchAndReturnToMenuAsync();
        return true;
    }

    public bool TryHandleLeaveLobby()
    {
        if (prepared)
        {
            return TryHandleLeaveMatch();
        }

        return false;
    }

    public bool TryHandleQuitGame()
    {
        if (!prepared)
        {
            return false;
        }

        if (localIsHost)
        {
            Debug.LogWarning(
                "Host Quit is blocked while an online match is active until Host Migration is implemented.",
                this);
            return true;
        }

        LeaveActiveMatchAndQuitAsync();
        return true;
    }

    private async void LeaveActiveMatchAndReturnToMenuAsync()
    {
        if (leaveRequestInFlight || matchBridge == null)
        {
            return;
        }

        leaveRequestInFlight = true;

        try
        {
            AtlasMatchNetworkResult result =
                await matchBridge.LeaveActiveMatchAsync();

            if (!result.Success)
            {
                Debug.LogWarning(
                    "Phase 5F Leave Match request failed: " +
                    result.TechnicalMessage,
                    this);
                return;
            }

            DetachLocalClientFromActiveMatch();
        }
        finally
        {
            leaveRequestInFlight = false;
        }
    }

    private async void LeaveActiveMatchAndQuitAsync()
    {
        if (leaveRequestInFlight || matchBridge == null)
        {
            return;
        }

        leaveRequestInFlight = true;

        try
        {
            await matchBridge.LeaveActiveMatchAsync();
        }
        finally
        {
            leaveRequestInFlight = false;
            Application.Quit();
        }
    }

    private void DetachLocalClientFromActiveMatch()
    {
        prepared = false;
        hostNetworkInitialized = false;
        preparedMatchId = string.Empty;
        locallyControlledHumanSlots.Clear();
        matchBridge?.ResetForMatchSession(string.Empty);
        lastObservedControllerBySlot.Clear();
        lastObservedConnectionBySlot.Clear();

        AtlasBoardPrivateLobbyUIController privateLobby =
            FindSceneComponent<AtlasBoardPrivateLobbyUIController>();
        privateLobby?.NotifyActiveMatchClientDetached();

        AtlasBoardMainMenuController mainMenu =
            FindSceneComponent<AtlasBoardMainMenuController>();
        mainMenu?.ShowMainMenuAfterActiveMatchExit();
    }

    private void HandleLobbySnapshotChanged(
        AtlasLobbySnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        AtlasLobbySnapshot previous = startLobbySnapshot;
        startLobbySnapshot = snapshot;

        bool returnedForRematch =
            prepared &&
            previous != null &&
            (previous.LifecycleState == AtlasRoomLifecycleState.Starting ||
             previous.LifecycleState == AtlasRoomLifecycleState.InMatch) &&
            !string.IsNullOrWhiteSpace(previous.MatchId) &&
            snapshot.LifecycleState == AtlasRoomLifecycleState.Waiting &&
            string.IsNullOrWhiteSpace(snapshot.MatchId);

        if (returnedForRematch)
        {
            prepared = false;
            hostNetworkInitialized = false;
            lastPublishedFrameJson = string.Empty;
            preparedMatchId = string.Empty;

            ResetRuntimePresentationForNewMatchSession(
                string.Empty);

            // Waiting-lobby membership becomes authoritative again only after
            // the active match has been fully detached/reset.
            ApplyLobbyIdentitySnapshot(snapshot);

            AtlasBoardMainMenuController mainMenu =
                FindSceneComponent<AtlasBoardMainMenuController>();
            mainMenu?.ReturnOnlineMatchToLobby();
            return;
        }

        // CRITICAL: while a match is Starting/InMatch, lobby membership is not
        // the controller authority. matchGetSnapshot owns Human/TemporaryBot/
        // PermanentBot. Re-applying lobby controllerKind every poll was the
        // source of real Humans briefly becoming BOT and could cancel the
        // active Human turn during reconnect.
        if (!prepared ||
            snapshot.LifecycleState == AtlasRoomLifecycleState.Waiting)
        {
            ApplyLobbyIdentitySnapshot(snapshot);
        }
    }

    private void ResetRuntimePresentationForNewMatchSession(
        string matchId)
    {
        ResolveTurnManager();
        ResolveTradeManager();
        ResolveMatchResultManager();
        ResolveTabletUIManager();
        ResolveHumanRollTimeoutController();

        MatchSetupManager reusableSetup =
            FindSceneComponent<MatchSetupManager>();
        TileResolutionManager resolution =
            FindSceneComponent<TileResolutionManager>();
        SpecialTileManager special =
            FindSceneComponent<SpecialTileManager>();
        EventCardManager events =
            FindSceneComponent<EventCardManager>();
        AuctionManager auction =
            FindSceneComponent<AuctionManager>();
        PropertyDevelopmentManager development =
            FindSceneComponent<PropertyDevelopmentManager>();

        reusableSetup?.ResetForNewMatchSession();
        resolution?.ResetForNewMatchSession();
        special?.ResetForNewMatchSession();
        events?.ResetForNewMatchSession();
        auction?.ResetForNewMatchSession();
        development?.ResetAllDevelopmentsForNewMatch();

        turnManager?.ResetForOnlineLobbySession();
        tradeManager?.ResetForNewMatchSession();
        matchResultManager?.ResetForNewMatchSession();
        tabletUIManager?.ResetForNewMatchSession();
        humanRollTimeoutController?.ResetForNewMatchSession();

        if (matchBridge != null)
        {
            matchBridge.ResetForMatchSession(matchId);
        }

        followerMovementQueue.Clear();
        followerMovementVisualActive = false;
        latestFollowerPawnTileIndices =
            new[] { -1, -1, -1, -1 };
        lastObservedControllerBySlot.Clear();
        lastObservedConnectionBySlot.Clear();
        localAfkExitScheduled = false;
    }

    private void ApplyLobbyIdentitySnapshot(
        AtlasLobbySnapshot snapshot)
    {
        if (snapshot == null ||
            snapshot.Members == null ||
            turnManager == null)
        {
            return;
        }

        for (int slotIndex = 0; slotIndex < 4; slotIndex++)
        {
            PlayerGameState existing =
                turnManager.GetPlayerStateBySlotIndex(slotIndex);

            BotPlayerController existingBot =
                existing != null
                    ? existing.GetComponent<BotPlayerController>()
                    : null;

            existingBot?.SetBotEnabled(false);
            existing?.ClearOnlineSeatState();
        }

        foreach (AtlasLobbyMemberSnapshot member in snapshot.Members)
        {
            if (member == null || !member.Active)
            {
                continue;
            }

            PlayerGameState player =
                turnManager.GetPlayerStateBySlotIndex(member.SlotIndex);

            if (player == null)
            {
                continue;
            }

            string controllerWire =
                ResolveLobbyControllerWire(member);

            player.ApplyOnlineIdentityAndControlState(
                member.DisplayName,
                controllerWire,
                ConnectionStateToWire(member.ConnectionState),
                0L,
                member.ConnectionState == AtlasSeatConnectionState.AfkRemoved);

            BotPlayerController bot =
                player.GetComponent<BotPlayerController>();

            if (bot != null)
            {
                bool hostShouldSimulateBot =
                    localIsHost &&
                    IsBotControllerWire(controllerWire);

                bot.SetBotEnabled(hostShouldSimulateBot);
            }
        }
    }

    private void ApplyOnlineSeatMetadata(
        AtlasMatchNetworkSnapshot snapshot)
    {
        if (snapshot == null ||
            snapshot.Seats == null ||
            turnManager == null)
        {
            return;
        }

        ResolveTabletUIManager();

        foreach (AtlasMatchNetworkSeat seat in snapshot.Seats)
        {
            if (seat == null || seat.SlotIndex < 0 || seat.SlotIndex >= 4)
            {
                continue;
            }

            PlayerGameState player =
                turnManager.GetPlayerStateBySlotIndex(seat.SlotIndex);

            if (player == null)
            {
                continue;
            }

            string previousController =
                lastObservedControllerBySlot.TryGetValue(
                    seat.SlotIndex,
                    out string priorController)
                    ? priorController
                    : string.Empty;

            string previousConnection =
                lastObservedConnectionBySlot.TryGetValue(
                    seat.SlotIndex,
                    out string priorConnection)
                    ? priorConnection
                    : string.Empty;

            player.ApplyOnlineIdentityAndControlState(
                seat.DisplayName,
                seat.ControllerKind,
                seat.ConnectionState,
                seat.ReconnectExpiresAtEpochMs,
                seat.AfkLockedOut);

            BotPlayerController bot =
                player.GetComponent<BotPlayerController>();

            if (bot != null)
            {
                // Only Host simulates authoritative bot seats. Followers keep
                // every BotPlayerController disabled even when presenting a
                // Temporary/Permanent Bot in the HUD.
                bool botControlled =
                    localIsHost &&
                    IsBotControllerWire(seat.ControllerKind);
                bot.SetBotEnabled(botControlled);
            }

            bool firstObservation =
                string.IsNullOrWhiteSpace(previousController) &&
                string.IsNullOrWhiteSpace(previousConnection);

            if (!firstObservation &&
                (!string.Equals(
                     previousController,
                     seat.ControllerKind,
                     StringComparison.OrdinalIgnoreCase) ||
                 !string.Equals(
                     previousConnection,
                     seat.ConnectionState,
                     StringComparison.OrdinalIgnoreCase)))
            {
                ShowSeatLifecycleNotice(
                    seat,
                    previousController,
                    previousConnection);
            }

            lastObservedControllerBySlot[seat.SlotIndex] =
                seat.ControllerKind ?? string.Empty;
            lastObservedConnectionBySlot[seat.SlotIndex] =
                seat.ConnectionState ?? string.Empty;

            bool localSeat =
                !string.IsNullOrWhiteSpace(snapshot.LocalSeatId) &&
                string.Equals(
                    snapshot.LocalSeatId,
                    seat.SeatId,
                    StringComparison.Ordinal);

            if (localSeat &&
                seat.AfkLockedOut &&
                !localAfkExitScheduled)
            {
                localAfkExitScheduled = true;
                StartCoroutine(
                    RouteAfkRemovedLocalClientAfterNotice(
                        seat.DisplayName));
            }
        }

        turnManager.RefreshTurnPresentationForControlChange();
    }

    private void ShowSeatLifecycleNotice(
        AtlasMatchNetworkSeat seat,
        string previousController,
        string previousConnection)
    {
        if (seat == null || tabletUIManager == null)
        {
            return;
        }

        string name =
            string.IsNullOrWhiteSpace(seat.DisplayName)
                ? $"Player {seat.SlotIndex + 1}"
                : seat.DisplayName;

        string controller = seat.ControllerKind ?? string.Empty;
        string connection = seat.ConnectionState ?? string.Empty;
        string message = string.Empty;

        if (string.Equals(controller, "temporary_bot", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(connection, "reconnecting", StringComparison.OrdinalIgnoreCase))
        {
            message =
                AtlasBoardOnlineRuntimeText
                    .SeatLeftTemporaryBot(name);
        }
        else if (string.Equals(controller, "human", StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(connection, "connected", StringComparison.OrdinalIgnoreCase) &&
                 (string.Equals(previousController, "temporary_bot", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(previousConnection, "reconnecting", StringComparison.OrdinalIgnoreCase)))
        {
            message =
                AtlasBoardOnlineRuntimeText
                    .SeatRejoined(name);
        }
        else if (string.Equals(controller, "permanent_bot", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(connection, "afk_removed", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(connection, "reconnect_expired", StringComparison.OrdinalIgnoreCase))
        {
            bool afk =
                seat.AfkLockedOut ||
                string.Equals(connection, "afk_removed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(seat.RemovalReason, "afk", StringComparison.OrdinalIgnoreCase);

            message = afk
                ? AtlasBoardOnlineRuntimeText
                    .SeatAfkRemoved(name)
                : AtlasBoardOnlineRuntimeText
                    .SeatReconnectExpired(name);
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            tabletUIManager.ShowOnlineSeatNotice(message, 3f);
        }
    }

    private IEnumerator RouteAfkRemovedLocalClientAfterNotice(
        string displayName)
    {
        ResolveTabletUIManager();

        string name =
            string.IsNullOrWhiteSpace(displayName)
                ? "Player"
                : displayName;

        tabletUIManager?.ShowOnlineSeatNotice(
            AtlasBoardOnlineRuntimeText
                .LocalAfkRemoved(name),
            3f);

        yield return new WaitForSecondsRealtime(3f);

        AtlasBoardPrivateLobbyUIController privateLobby =
            FindSceneComponent<AtlasBoardPrivateLobbyUIController>();
        privateLobby?.NotifyActiveMatchClientDetached();

        AtlasBoardMainMenuController mainMenu =
            FindSceneComponent<AtlasBoardMainMenuController>();
        mainMenu?.ShowMainMenuAfterActiveMatchExit();

        if (!localIsHost)
        {
            prepared = false;
            hostNetworkInitialized = false;
            preparedMatchId = string.Empty;
            locallyControlledHumanSlots.Clear();
            matchBridge?.ResetForMatchSession(string.Empty);
            lastObservedControllerBySlot.Clear();
            lastObservedConnectionBySlot.Clear();
        }
    }

    private async void HandleHostAfkRemovalTriggered(
        PlayerGameState player,
        int resultingStreak)
    {
        if (!prepared ||
            !localIsHost ||
            player == null ||
            matchBridge == null)
        {
            return;
        }

        AtlasMatchNetworkResult result =
            await matchBridge.HostMarkAfkRemovedAsync(
                player.PlayerSlotIndex);

        if (!result.Success)
        {
            Debug.LogWarning(
                "Phase 5F failed to persist AFK removal for " +
                player.DisplayName + ": " + result.TechnicalMessage,
                this);
            return;
        }

        nextPublishCheckAt = 0f;
    }

    private async void HandleOnlineTripleDoublePenaltyContinueRequested(
        PlayerGameState player)
    {
        if (player == null ||
            !CanSubmitRemoteDecision(player.PlayerSlotIndex))
        {
            turnManager?.NotifyOnlineTripleDoublePenaltySubmitFailed();
            return;
        }

        AtlasMatchNetworkResult result =
            await SubmitDecisionAsync(
                "triple_double_penalty",
                "continue",
                player.PlayerSlotIndex,
                -1,
                string.Empty,
                "triple-double-penalty");

        if (!result.Success)
        {
            turnManager?.NotifyOnlineTripleDoublePenaltySubmitFailed();
            Debug.LogWarning(
                "Phase 5F Remote triple-double penalty Continue failed: " +
                result.TechnicalMessage,
                this);
        }
    }

    private async Task ExpireReconnectReservationsAsync()
    {
        if (!prepared ||
            !localIsHost ||
            matchBridge == null ||
            reconnectExpiryInFlight)
        {
            return;
        }

        reconnectExpiryInFlight = true;

        try
        {
            AtlasMatchNetworkResult result =
                await matchBridge.HostExpireReconnectsAsync();

            if (!result.Success)
            {
                Debug.LogWarning(
                    "Phase 5F reconnect-expiry sweep failed: " +
                    result.TechnicalMessage,
                    this);
            }
        }
        finally
        {
            reconnectExpiryInFlight = false;
        }
    }

    private static string ResolveLobbyControllerWire(
        AtlasLobbyMemberSnapshot member)
    {
        if (member == null)
        {
            return string.Empty;
        }

        string explicitController =
            ControllerKindToWire(member.ControllerKind);

        if (!string.IsNullOrWhiteSpace(explicitController))
        {
            return explicitController;
        }

        // Waiting-lobby snapshots may legitimately omit controllerKind. Seat
        // mode is the stable fallback; never interpret an omitted controller
        // kind as BOT for a Human seat.
        switch (member.SeatMode)
        {
            case AtlasLobbySeatMode.Bot:
                return "bot";

            case AtlasLobbySeatMode.HostLocal:
            case AtlasLobbySeatMode.LocalHuman:
            case AtlasLobbySeatMode.RemoteHuman:
            case AtlasLobbySeatMode.OpenOnline:
                return "human";

            default:
                return string.Empty;
        }
    }

    private static bool IsBotControllerWire(
        string controllerKind)
    {
        return string.Equals(
                   controllerKind,
                   "bot",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   controllerKind,
                   "temporary_bot",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   controllerKind,
                   "permanent_bot",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string ControllerKindToWire(
        AtlasSeatControllerKind kind)
    {
        switch (kind)
        {
            case AtlasSeatControllerKind.Human:
                return "human";
            case AtlasSeatControllerKind.TemporaryBot:
                return "temporary_bot";
            case AtlasSeatControllerKind.PermanentBot:
                return "permanent_bot";
            default:
                return string.Empty;
        }
    }

    private static string ConnectionStateToWire(
        AtlasSeatConnectionState state)
    {
        switch (state)
        {
            case AtlasSeatConnectionState.Connected:
                return "connected";
            case AtlasSeatConnectionState.Reconnecting:
                return "reconnecting";
            case AtlasSeatConnectionState.LeftVoluntarily:
                return "left_voluntarily";
            case AtlasSeatConnectionState.AfkRemoved:
                return "afk_removed";
            case AtlasSeatConnectionState.Kicked:
                return "kicked";
            default:
                return string.Empty;
        }
    }

    private static T FindSceneComponent<T>()
        where T : Component
    {
        T[] all = Resources.FindObjectsOfTypeAll<T>();

        foreach (T item in all)
        {
            if (item != null && item.gameObject.scene.IsValid())
            {
                return item;
            }
        }

        return null;
    }

    private int ResolveSlotIndex(
        string seatId)
    {
        if (matchBridge != null &&
            matchBridge.CurrentSnapshot != null &&
            matchBridge.CurrentSnapshot.Seats != null)
        {
            AtlasMatchNetworkSeat seat =
                matchBridge.CurrentSnapshot.Seats
                    .FirstOrDefault(
                        item =>
                            item != null &&
                            string.Equals(
                                item.SeatId,
                                seatId,
                                StringComparison.Ordinal));

            if (seat != null)
            {
                return seat.SlotIndex;
            }
        }

        if (startLobbySnapshot != null &&
            startLobbySnapshot.Members != null)
        {
            AtlasLobbyMemberSnapshot member =
                startLobbySnapshot.Members
                    .FirstOrDefault(
                        item =>
                            item != null &&
                            string.Equals(
                                item.SeatId,
                                seatId,
                                StringComparison.Ordinal));

            if (member != null)
            {
                return member.SlotIndex;
            }
        }

        return -1;
    }

    private string ResolveSeatId(
        int slotIndex)
    {
        if (startLobbySnapshot != null &&
            startLobbySnapshot.Members != null)
        {
            AtlasLobbyMemberSnapshot member =
                startLobbySnapshot.Members
                    .FirstOrDefault(
                        item =>
                            item != null &&
                            item.Active &&
                            item.SlotIndex == slotIndex);

            if (member != null &&
                !string.IsNullOrWhiteSpace(
                    member.SeatId))
            {
                return member.SeatId;
            }
        }

        if (matchBridge != null &&
            matchBridge.CurrentSnapshot != null &&
            matchBridge.CurrentSnapshot.Seats != null)
        {
            AtlasMatchNetworkSeat seat =
                matchBridge.CurrentSnapshot.Seats
                    .FirstOrDefault(
                        item =>
                            item != null &&
                            item.SlotIndex == slotIndex);

            if (seat != null)
            {
                return seat.SeatId ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
