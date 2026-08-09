using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TurnManager : MonoBehaviour
{
    private enum GamePhase
    {
        DeterminingTurnOrder,
        Playing,
        Finished
    }

    [Header("Players")]
    [SerializeField]
    private PlayerPawnMover[] players;

    [Header("UI")]
    [SerializeField]
    private Button rollButton;

    [SerializeField]
    private TMP_Text turnStatusText;

    [Header("Dice Visuals")]
    [SerializeField]
    private DiceVisualController diceVisualController;

    [Header("Tile Resolution")]
    [SerializeField]
    private TileResolutionManager tileResolutionManager;

    [Header("Match Result")]
    [SerializeField]
    private MatchResultManager matchResultManager;

    [Header("Match Startup")]
    [SerializeField]
    private bool startMatchAutomatically = true;

    [Header("Match Rules")]
    [SerializeField, Min(1)]
    private int roundLimit = 20;

    [SerializeField]
    private bool enableDoublesExtraRollRule = true;

    [SerializeField]
    private bool enableTripleDoublePenalty = true;

    [Header("Current Match State")]
    [SerializeField]
    private GamePhase gamePhase;

    [SerializeField, Min(0)]
    private int currentPlayerIndex;

    [SerializeField, Min(0)]
    private int completedTurns;

    [SerializeField, Min(1)]
    private int currentRound = 1;

    [SerializeField]
    private int lastRoll;

    [SerializeField]
    private int lastDieOne;

    [SerializeField]
    private int lastDieTwo;

    [Header("Starting Order")]
    [SerializeField]
    private int orderRollPlayerIndex;

    [SerializeField]
    private int[] startingRolls;

    [SerializeField]
    private int[] turnOrder;

    [SerializeField]
    private int[] participatingPlayerIndexes;

    private List<int>[] startingRollHistories;

    private List<int> startingRollQueue =
        new List<int>();

    private bool resolvingStartingOrderTie;

    [Header("Turn Start Presentation")]
    [SerializeField, Min(0f)]
    private float skippedTurnMessageDuration = 1f;

    [SerializeField, Min(0f)]
    private float tripleDoublePenaltyMessageDuration = 1.25f;

    [Header("Doubles Rule")]
    [Tooltip(
        "Number of consecutive doubles in the current player's " +
        "turn. Resets when the turn passes to another player.")]
    [SerializeField, Min(0)]
    private int consecutiveDoublesThisTurn;

    [Header("Doubles Penalty UI")]
    [SerializeField]
    private GameObject tripleDoublePenaltyPanel;

    [SerializeField]
    private TMP_Text tripleDoublePenaltyText;

    [SerializeField]
    private Button tripleDoublePenaltyContinueButton;

    [Header("Starting Order Debug")]
    [Tooltip(
        "Test only. Forces every participating player to " +
        "roll the same value on their FIRST starting-order " +
        "roll. Tie rerolls remain random.")]
    [SerializeField]
    private bool debugForceInitialStartingTie;

    [SerializeField, Range(2, 12)]
    private int debugInitialStartingTieRoll = 7;

    [Header("Doubles Debug")]
    [Tooltip(
        "Test only. Forces both normal-turn dice to the same " +
        "value so the extra-roll and three-doubles penalty can " +
        "be tested quickly.")]
    [SerializeField]
    private bool debugForceDoubleRoll;

    [SerializeField, Range(1, 6)]
    private int debugForcedDoubleValue = 3;

    [Header("Debug")]
    [Tooltip(
        "0 = random 2d6. 1–12 forces the movement total for " +
        "testing. Values below 2 are debug-only and cannot " +
        "occur in a real 2d6 roll. When Doubles Debug is ON, " +
        "the forced-double setting takes priority.")]
    [SerializeField, Range(0, 12)]
    private int debugForcedRoll;

    private int currentTurnOrderIndex;
    private bool waitingForMovement;
    private bool resolvingDiceVisual;
    private bool resolvingTurnStart;
    private bool resolvingManagementAction;
    private bool isMatchFinished;
    private bool isMatchStarted;
    private Coroutine beginTurnCoroutine;
    private Coroutine tripleDoublePenaltyCoroutine;
    private PlayerGameState tripleDoublePenaltyPlayer;

    private readonly HashSet<int>
        completedActiveSlotsThisRound =
            new HashSet<int>();

    public int CurrentPlayerIndex =>
        currentPlayerIndex;

    public int CurrentRound =>
        currentRound;

    public int RoundLimit =>
        roundLimit;

    public bool DoublesExtraRollRuleEnabled =>
        enableDoublesExtraRollRule;

    public bool TripleDoublePenaltyEnabled =>
        enableDoublesExtraRollRule &&
        enableTripleDoublePenalty;

    public int CompletedTurns =>
        completedTurns;

    public int LastRoll =>
        lastRoll;

    public int LastDieOne =>
        lastDieOne;

    public int LastDieTwo =>
        lastDieTwo;

    public bool IsMatchStarted =>
        isMatchStarted;

    public PlayerGameState CurrentPlayerState =>
        GetPlayerState(currentPlayerIndex);

    public PlayerGameState StartingOrderPlayerState
    {
        get
        {
            if (gamePhase != GamePhase.DeterminingTurnOrder ||
                startingRollQueue == null ||
                orderRollPlayerIndex < 0 ||
                orderRollPlayerIndex >=
                startingRollQueue.Count)
            {
                return null;
            }

            return GetPlayerState(
                startingRollQueue[
                    orderRollPlayerIndex]);
        }
    }

    public bool CanStartManagementAction
    {
        get
        {
            PlayerGameState activePlayer =
                CurrentPlayerState;

            return isMatchStarted &&
                   gamePhase == GamePhase.Playing &&
                   !isMatchFinished &&
                   !waitingForMovement &&
                   !resolvingDiceVisual &&
                   !resolvingTurnStart &&
                   !resolvingManagementAction &&
                   activePlayer != null &&
                   activePlayer.IsParticipating &&
                   !activePlayer.IsBankrupt &&
                   !IsBotPlayer(activePlayer);
        }
    }

    private void Start()
    {
        ResetTripleDoublePenaltyUI();

        if (!ValidatePlayers())
        {
            enabled = false;
            return;
        }

        SubscribeToPlayers();

        if (startMatchAutomatically)
        {
            BeginMatch();
        }
        else
        {
            PrepareForMatchSetup();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromPlayers();
    }

    public void SetRoundLimit(
        int value)
    {
        if (isMatchStarted)
        {
            Debug.LogWarning(
                "Round limit cannot be changed after the " +
                "match has started.",
                this);

            return;
        }

        roundLimit =
            Mathf.Max(
                1,
                value);
    }

    public void SetDoublesExtraRollRule(
        bool enabledValue)
    {
        if (isMatchStarted)
        {
            Debug.LogWarning(
                "The doubles rule cannot be changed after " +
                "the match has started.",
                this);

            return;
        }

        enableDoublesExtraRollRule =
            enabledValue;

        if (!enableDoublesExtraRollRule)
        {
            consecutiveDoublesThisTurn = 0;
        }
    }

    public void SetTripleDoublePenaltyRule(
        bool enabledValue)
    {
        if (isMatchStarted)
        {
            Debug.LogWarning(
                "The triple-double penalty cannot be changed " +
                "after the match has started.",
                this);

            return;
        }

        enableTripleDoublePenalty =
            enabledValue;
    }

    public bool BeginMatch()
    {
        if (isMatchStarted)
        {
            Debug.LogWarning(
                "The match has already started.",
                this);

            return false;
        }

        if (!ValidatePlayers())
        {
            return false;
        }

        int participatingCount =
            GetParticipatingPlayerIndexes()
                .Length;

        if (participatingCount < 2)
        {
            Debug.LogError(
                "A match requires at least two " +
                "participating players.",
                this);

            return false;
        }

        isMatchStarted = true;

        BeginTurnOrderPhase();

        Debug.Log(
            "Match started from the current player setup.",
            this);

        return true;
    }

    private void PrepareForMatchSetup()
    {
        isMatchStarted = false;
        isMatchFinished = false;
        waitingForMovement = false;
        resolvingDiceVisual = false;
        resolvingTurnStart = false;
        resolvingManagementAction = false;

        if (rollButton != null)
        {
            rollButton.interactable = false;
        }

        if (turnStatusText != null)
        {
            turnStatusText.text =
                "Oyuncu ayarları bekleniyor";
        }
    }

    public void HandleRollButton()
    {
        if (!isMatchStarted ||
            isMatchFinished ||
            resolvingDiceVisual ||
            resolvingTurnStart ||
            resolvingManagementAction)
        {
            return;
        }

        if (gamePhase ==
            GamePhase.DeterminingTurnOrder)
        {
            PlayerGameState startingPlayer =
                StartingOrderPlayerState;

            if (IsBotPlayer(startingPlayer))
            {
                return;
            }

            RollForStartingOrder();
            return;
        }

        if (gamePhase == GamePhase.Playing)
        {
            PlayerGameState activePlayer =
                CurrentPlayerState;

            if (IsBotPlayer(activePlayer))
            {
                return;
            }

            RollForActivePlayer();
        }
    }

    public bool CanPlayerRequestRoll(
        PlayerGameState player)
    {
        if (!isMatchStarted ||
            player == null ||
            !player.IsParticipating ||
            player.IsBankrupt ||
            isMatchFinished ||
            resolvingDiceVisual ||
            resolvingTurnStart ||
            resolvingManagementAction ||
            waitingForMovement)
        {
            return false;
        }

        if (gamePhase ==
            GamePhase.DeterminingTurnOrder)
        {
            return IsSamePlayer(
                StartingOrderPlayerState,
                player);
        }

        if (gamePhase == GamePhase.Playing)
        {
            PlayerGameState activePlayer =
                CurrentPlayerState;

            if (!IsSamePlayer(
                    activePlayer,
                    player))
            {
                return false;
            }

            PlayerPawnMover pawn =
                players[currentPlayerIndex];

            return pawn != null &&
                   !pawn.IsMoving;
        }

        return false;
    }

    public bool TryRequestRoll(
        PlayerGameState player)
    {
        if (!CanPlayerRequestRoll(player))
        {
            return false;
        }

        if (gamePhase ==
            GamePhase.DeterminingTurnOrder)
        {
            RollForStartingOrder();
            return true;
        }

        if (gamePhase == GamePhase.Playing)
        {
            RollForActivePlayer();
            return true;
        }

        return false;
    }

    public bool TryBeginBotManagementAction(
        PlayerGameState botPlayer)
    {
        if (!isMatchStarted ||
            botPlayer == null ||
            !IsBotPlayer(botPlayer) ||
            gamePhase != GamePhase.Playing ||
            isMatchFinished ||
            waitingForMovement ||
            resolvingDiceVisual ||
            resolvingTurnStart ||
            resolvingManagementAction ||
            !botPlayer.IsParticipating ||
            botPlayer.IsBankrupt ||
            !IsSamePlayer(
                CurrentPlayerState,
                botPlayer))
        {
            return false;
        }

        resolvingManagementAction = true;

        if (rollButton != null)
        {
            rollButton.interactable = false;
        }

        Debug.Log(
            $"{botPlayer.DisplayName} [BOT] opened a " +
            "management action.",
            this);

        return true;
    }

    public bool TryBeginManagementAction()
    {
        if (!CanStartManagementAction)
        {
            return false;
        }

        resolvingManagementAction = true;

        if (rollButton != null)
        {
            rollButton.interactable = false;
        }

        Debug.Log(
            $"{CurrentPlayerState.DisplayName} opened a " +
            "management action.",
            this);

        return true;
    }

    public void CompleteManagementAction()
    {
        if (!resolvingManagementAction)
        {
            return;
        }

        resolvingManagementAction = false;

        if (gamePhase == GamePhase.Playing &&
            !isMatchFinished)
        {
            UpdateTurnUI();
        }
    }

    public void NotifyPlayerBankrupt(
        PlayerGameState player)
    {
        if (player == null)
        {
            return;
        }

        completedActiveSlotsThisRound.Remove(
            player.PlayerSlotIndex);

        Debug.Log(
            $"{player.DisplayName} [Slot " +
            $"{player.PlayerSlotIndex}] was removed " +
            "from active turn participation.",
            this);
    }

    public List<PlayerGameState>
        GetPlayersInTurnOrderFrom(
            PlayerGameState referencePlayer,
            bool includeReferencePlayer)
    {
        List<PlayerGameState> orderedPlayers =
            new List<PlayerGameState>();

        if (referencePlayer == null ||
            players == null ||
            players.Length == 0)
        {
            return orderedPlayers;
        }

        int[] activeOrder =
            turnOrder != null &&
            turnOrder.Length > 0
                ? turnOrder
                : GetParticipatingPlayerIndexes();

        int referenceOrderPosition = -1;

        for (int orderPosition = 0;
             orderPosition < activeOrder.Length;
             orderPosition++)
        {
            PlayerGameState candidate =
                GetPlayerState(
                    activeOrder[orderPosition]);

            if (candidate == null)
            {
                continue;
            }

            if (candidate == referencePlayer ||
                candidate.PlayerSlotIndex ==
                referencePlayer.PlayerSlotIndex)
            {
                referenceOrderPosition =
                    orderPosition;

                break;
            }
        }

        if (referenceOrderPosition < 0)
        {
            Debug.LogWarning(
                $"{referencePlayer.DisplayName} could not " +
                "be found in the current turn order.",
                this);

            return orderedPlayers;
        }

        int firstOffset =
            includeReferencePlayer ? 0 : 1;

        int positionsToInspect =
            includeReferencePlayer
                ? activeOrder.Length
                : Mathf.Max(
                    0,
                    activeOrder.Length - 1);

        for (int offset = 0;
             offset < positionsToInspect;
             offset++)
        {
            int orderPosition =
                (referenceOrderPosition +
                 firstOffset +
                 offset) %
                activeOrder.Length;

            PlayerGameState player =
                GetPlayerState(
                    activeOrder[orderPosition]);

            if (player != null &&
                player.IsParticipating &&
                !player.IsBankrupt)
            {
                orderedPlayers.Add(player);
            }
        }

        return orderedPlayers;
    }

    private void BeginTurnOrderPhase()
    {
        gamePhase =
            GamePhase.DeterminingTurnOrder;

        participatingPlayerIndexes =
            GetParticipatingPlayerIndexes();

        startingRolls =
            new int[players.Length];

        startingRollHistories =
            new List<int>[players.Length];

        foreach (int playerIndex
                 in participatingPlayerIndexes)
        {
            startingRollHistories[
                playerIndex] =
                    new List<int>();
        }

        startingRollQueue =
            new List<int>(
                participatingPlayerIndexes);

        turnOrder =
            new int[
                participatingPlayerIndexes.Length];

        orderRollPlayerIndex = 0;
        resolvingStartingOrderTie = false;
        currentTurnOrderIndex = 0;
        completedTurns = 0;
        currentRound = 1;

        completedActiveSlotsThisRound.Clear();

        waitingForMovement = false;
        resolvingDiceVisual = false;
        resolvingTurnStart = false;
        resolvingManagementAction = false;
        isMatchFinished = false;

        if (rollButton != null)
        {
            rollButton.interactable = true;
        }

        UpdateStartingOrderUI();
    }

    private void RollForStartingOrder()
    {
        if (resolvingDiceVisual ||
            startingRollQueue == null ||
            orderRollPlayerIndex < 0 ||
            orderRollPlayerIndex >=
            startingRollQueue.Count)
        {
            return;
        }

        int playerArrayIndex =
            startingRollQueue[
                orderRollPlayerIndex];

        if (startingRollHistories[
                playerArrayIndex] == null)
        {
            startingRollHistories[
                playerArrayIndex] =
                    new List<int>();
        }

        bool isFirstStartingRoll =
            startingRollHistories[
                playerArrayIndex].Count == 0;

        int dieOne;
        int dieTwo;
        int roll;

        if (debugForceInitialStartingTie &&
            isFirstStartingRoll)
        {
            roll =
                debugInitialStartingTieRoll;

            BuildDiceForForcedTotal(
                roll,
                out dieOne,
                out dieTwo);
        }
        else
        {
            dieOne =
                Random.Range(1, 7);

            dieTwo =
                Random.Range(1, 7);

            roll =
                dieOne + dieTwo;
        }

        resolvingDiceVisual = true;

        if (rollButton != null)
        {
            rollButton.interactable = false;
        }

        if (turnStatusText != null)
        {
            string playerName =
                GetPlayerState(
                    playerArrayIndex)
                ?.DisplayName ??
                "Oyuncu";

            turnStatusText.text =
                $"{playerName} zar atıyor...";
        }

        PlayDiceVisual(
            dieOne,
            dieTwo,
            () =>
                CompleteStartingOrderRoll(
                    playerArrayIndex,
                    dieOne,
                    dieTwo,
                    roll));
    }

    private void CompleteStartingOrderRoll(
        int playerArrayIndex,
        int dieOne,
        int dieTwo,
        int roll)
    {
        resolvingDiceVisual = false;

        startingRolls[
            playerArrayIndex] = roll;

        startingRollHistories[
            playerArrayIndex].Add(roll);

        PlayerGameState rollingPlayer =
            GetPlayerState(
                playerArrayIndex);

        string rollType =
            resolvingStartingOrderTie
                ? "Starting tie reroll"
                : "Starting roll";

        Debug.Log(
            $"{rollType} — " +
            $"{rollingPlayer.DisplayName} " +
            $"[Slot {rollingPlayer.PlayerSlotIndex}]: " +
            $"{dieOne} + {dieTwo} = {roll}",
            this);

        orderRollPlayerIndex++;

        if (orderRollPlayerIndex <
            startingRollQueue.Count)
        {
            UpdateStartingOrderUI();
            return;
        }

        ContinueStartingOrderResolution();
    }

    private void ContinueStartingOrderResolution()
    {
        List<int> tiedPlayers =
            GetStillTiedStartingPlayers();

        if (tiedPlayers.Count > 0)
        {
            startingRollQueue =
                tiedPlayers;

            orderRollPlayerIndex = 0;
            resolvingStartingOrderTie = true;

            string tiedNames =
                string.Join(
                    ", ",
                    tiedPlayers.Select(
                        playerIndex =>
                        {
                            PlayerGameState player =
                                GetPlayerState(
                                    playerIndex);

                            return player != null
                                ? player.DisplayName
                                : $"Slot {playerIndex}";
                        }));

            Debug.Log(
                $"Starting-order tie detected: " +
                $"{tiedNames}. Reroll required.",
                this);

            UpdateStartingOrderUI();
            return;
        }

        resolvingStartingOrderTie = false;
        DetermineTurnOrder();
    }

    private List<int>
        GetStillTiedStartingPlayers()
    {
        List<int> tiedPlayers =
            new List<int>();

        if (participatingPlayerIndexes == null ||
            startingRollHistories == null)
        {
            return tiedPlayers;
        }

        for (int firstPosition = 0;
             firstPosition <
             participatingPlayerIndexes.Length;
             firstPosition++)
        {
            int firstPlayer =
                participatingPlayerIndexes[
                    firstPosition];

            for (int secondPosition =
                     firstPosition + 1;
                 secondPosition <
                 participatingPlayerIndexes.Length;
                 secondPosition++)
            {
                int secondPlayer =
                    participatingPlayerIndexes[
                        secondPosition];

                if (!HaveEqualStartingRollHistory(
                        firstPlayer,
                        secondPlayer))
                {
                    continue;
                }

                if (!tiedPlayers.Contains(
                        firstPlayer))
                {
                    tiedPlayers.Add(
                        firstPlayer);
                }

                if (!tiedPlayers.Contains(
                        secondPlayer))
                {
                    tiedPlayers.Add(
                        secondPlayer);
                }
            }
        }

        return tiedPlayers;
    }

    private bool HaveEqualStartingRollHistory(
        int firstPlayer,
        int secondPlayer)
    {
        List<int> firstHistory =
            startingRollHistories[
                firstPlayer];

        List<int> secondHistory =
            startingRollHistories[
                secondPlayer];

        if (firstHistory == null ||
            secondHistory == null ||
            firstHistory.Count !=
            secondHistory.Count)
        {
            return false;
        }

        for (int index = 0;
             index < firstHistory.Count;
             index++)
        {
            if (firstHistory[index] !=
                secondHistory[index])
            {
                return false;
            }
        }

        return true;
    }

    private void DetermineTurnOrder()
    {
        turnOrder =
            participatingPlayerIndexes
            .OrderBy(
                playerArrayIndex =>
                    playerArrayIndex,
                Comparer<int>.Create(
                    CompareStartingRollHistories))
            .ToArray();

        string orderDescription =
            string.Join(
                " → ",
                turnOrder.Select(
                    playerArrayIndex =>
                    {
                        PlayerGameState player =
                            GetPlayerState(
                                playerArrayIndex);

                        string rollHistory =
                            startingRollHistories != null &&
                            startingRollHistories[
                                playerArrayIndex] != null
                                ? string.Join(
                                    "/",
                                    startingRollHistories[
                                        playerArrayIndex])
                                : startingRolls[
                                    playerArrayIndex]
                                    .ToString();

                        return
                            $"{player.DisplayName} " +
                            $"[Slot {player.PlayerSlotIndex}] " +
                            $"({rollHistory})";
                    }));

        Debug.Log(
            $"Starting order: {orderDescription}",
            this);

        gamePhase =
            GamePhase.Playing;

        currentTurnOrderIndex = 0;

        currentPlayerIndex =
            turnOrder[currentTurnOrderIndex];

        BeginCurrentTurn();
    }

    private int CompareStartingRollHistories(
        int firstPlayer,
        int secondPlayer)
    {
        List<int> firstHistory =
            startingRollHistories[
                firstPlayer];

        List<int> secondHistory =
            startingRollHistories[
                secondPlayer];

        int comparisonLength =
            Mathf.Min(
                firstHistory.Count,
                secondHistory.Count);

        for (int index = 0;
             index < comparisonLength;
             index++)
        {
            if (firstHistory[index] ==
                secondHistory[index])
            {
                continue;
            }

            // Higher roll comes first.
            return secondHistory[index]
                .CompareTo(
                    firstHistory[index]);
        }

        // This is only a deterministic safety fallback.
        // Normal equal histories are rerolled before this point.
        PlayerGameState firstState =
            GetPlayerState(firstPlayer);

        PlayerGameState secondState =
            GetPlayerState(secondPlayer);

        int firstSlot =
            firstState != null
                ? firstState.PlayerSlotIndex
                : firstPlayer;

        int secondSlot =
            secondState != null
                ? secondState.PlayerSlotIndex
                : secondPlayer;

        return firstSlot.CompareTo(
            secondSlot);
    }

    private void RollForActivePlayer()
    {
        if (waitingForMovement ||
            resolvingDiceVisual ||
            resolvingTurnStart ||
            isMatchFinished)
        {
            return;
        }

        PlayerPawnMover activePawn =
            players[currentPlayerIndex];

        PlayerGameState activePlayer =
            GetPlayerState(
                currentPlayerIndex);

        if (activePawn == null ||
            activePlayer == null ||
            !activePlayer.IsParticipating ||
            activePlayer.IsBankrupt ||
            activePawn.IsMoving)
        {
            return;
        }

        if (debugForceDoubleRoll)
        {
            lastDieOne =
                debugForcedDoubleValue;

            lastDieTwo =
                debugForcedDoubleValue;

            lastRoll =
                lastDieOne +
                lastDieTwo;
        }
        else if (debugForcedRoll > 0)
        {
            lastRoll =
                debugForcedRoll;

            BuildDiceForForcedTotal(
                lastRoll,
                out lastDieOne,
                out lastDieTwo);
        }
        else
        {
            lastDieOne =
                Random.Range(1, 7);

            lastDieTwo =
                Random.Range(1, 7);

            lastRoll =
                lastDieOne +
                lastDieTwo;
        }

        bool rolledDouble =
            IsCurrentRollDouble();

        if (enableDoublesExtraRollRule &&
            rolledDouble)
        {
            consecutiveDoublesThisTurn++;
        }
        else
        {
            consecutiveDoublesThisTurn = 0;
        }

        waitingForMovement = true;
        resolvingDiceVisual = true;

        if (rollButton != null)
        {
            rollButton.interactable = false;
        }

        if (turnStatusText != null)
        {
            turnStatusText.text =
                $"Tur {currentRound}/{roundLimit}\n" +
                $"{activePlayer.DisplayName} zar atıyor...";
        }

        int resolvedDieOne =
            lastDieOne;

        int resolvedDieTwo =
            lastDieTwo;

        int resolvedTotal =
            lastRoll;

        bool resolvedDouble =
            rolledDouble;

        PlayDiceVisual(
            resolvedDieOne,
            resolvedDieTwo,
            () =>
                CompleteActiveRollAfterVisual(
                    activePawn,
                    activePlayer,
                    resolvedDieOne,
                    resolvedDieTwo,
                    resolvedTotal,
                    resolvedDouble));
    }

    private void CompleteActiveRollAfterVisual(
        PlayerPawnMover activePawn,
        PlayerGameState activePlayer,
        int dieOne,
        int dieTwo,
        int total,
        bool rolledDouble)
    {
        resolvingDiceVisual = false;

        // Keep public last-roll state synchronized with the exact
        // numbers that were shown by the dice animation.
        lastDieOne = dieOne;
        lastDieTwo = dieTwo;
        lastRoll = total;

        if (turnStatusText != null)
        {
            string diceText =
                !debugForceDoubleRoll &&
                debugForcedRoll > 0 &&
                debugForcedRoll < 2
                    ? $"DEBUG toplam: {lastRoll}"
                    : $"{lastDieOne} + {lastDieTwo} = " +
                      $"{lastRoll}";

            turnStatusText.text =
                $"Tur {currentRound}/{roundLimit}\n" +
                $"{activePlayer.DisplayName} zar attı: " +
                $"{diceText}";
        }

        string debugDiceDescription =
            !debugForceDoubleRoll &&
            debugForcedRoll > 0 &&
            debugForcedRoll < 2
                ? $"DEBUG total {lastRoll}"
                : $"{lastDieOne} + {lastDieTwo} = " +
                  $"{lastRoll}";

        Debug.Log(
            $"{activePlayer.DisplayName} " +
            $"[Slot {activePlayer.PlayerSlotIndex}] " +
            $"dice result: {debugDiceDescription}. " +
            $"Consecutive doubles: " +
            $"{consecutiveDoublesThisTurn}",
            this);

        if (enableDoublesExtraRollRule &&
            enableTripleDoublePenalty &&
            rolledDouble &&
            consecutiveDoublesThisTurn >= 3)
        {
            ApplyTripleDoublePenalty(
                activePlayer);

            return;
        }

        if (!activePawn.MoveBy(lastRoll))
        {
            waitingForMovement = false;
            UpdateTurnUI();
        }
    }

    private void PlayDiceVisual(
        int dieOne,
        int dieTwo,
        System.Action onCompleted)
    {
        if (diceVisualController == null)
        {
            onCompleted?.Invoke();
            return;
        }

        diceVisualController.PlayRoll(
            dieOne,
            dieTwo,
            onCompleted);
    }

    private void BuildDiceForForcedTotal(
        int forcedTotal,
        out int dieOne,
        out int dieTwo)
    {
        if (forcedTotal < 2)
        {
            // Preserve legacy one-step debug tests. A real 2d6
            // roll can never produce this total.
            dieOne = forcedTotal;
            dieTwo = 0;
            return;
        }

        int clampedTotal =
            Mathf.Clamp(
                forcedTotal,
                2,
                12);

        List<int> validDieOneValues =
            new List<int>();

        int minimumDieOne =
            Mathf.Max(
                1,
                clampedTotal - 6);

        int maximumDieOne =
            Mathf.Min(
                6,
                clampedTotal - 1);

        for (int value = minimumDieOne;
             value <= maximumDieOne;
             value++)
        {
            int otherValue =
                clampedTotal - value;

            // A forced total should not accidentally trigger the
            // doubles rule when a non-double representation exists.
            if (value == otherValue &&
                clampedTotal != 2 &&
                clampedTotal != 12)
            {
                continue;
            }

            validDieOneValues.Add(value);
        }

        if (validDieOneValues.Count == 0)
        {
            dieOne =
                clampedTotal / 2;

            dieTwo =
                clampedTotal - dieOne;

            return;
        }

        dieOne =
            validDieOneValues[
                Random.Range(
                    0,
                    validDieOneValues.Count)];

        dieTwo =
            clampedTotal -
            dieOne;
    }

    private bool IsCurrentRollDouble()
    {
        return lastDieOne >= 1 &&
               lastDieOne <= 6 &&
               lastDieTwo >= 1 &&
               lastDieTwo <= 6 &&
               lastDieOne == lastDieTwo;
    }

    private void ApplyTripleDoublePenalty(
        PlayerGameState activePlayer)
    {
        if (activePlayer == null)
        {
            FinishCurrentTurn();
            return;
        }

        // Atlas Board currently has no jail tile. The third
        // consecutive double cancels that movement and causes
        // the player to miss their next scheduled turn.
        activePlayer.AddTurnsToSkip(1);

        tripleDoublePenaltyPlayer =
            activePlayer;

        if (tripleDoublePenaltyText != null)
        {
            tripleDoublePenaltyText.text =
                $"{activePlayer.DisplayName}, " +
                "3 kez üst üste çift attı!\n\n" +
                "Bu üçüncü atışta hareket yok.\n" +
                "Bir sonraki turunda zar atamazsın.";
        }

        if (tripleDoublePenaltyPanel != null)
        {
            tripleDoublePenaltyPanel.SetActive(
                true);
        }

        bool isBot =
            IsBotPlayer(activePlayer);

        if (tripleDoublePenaltyContinueButton != null)
        {
            tripleDoublePenaltyContinueButton
                .interactable =
                    !isBot;
        }

        Debug.Log(
            $"{activePlayer.DisplayName} " +
            $"[Slot {activePlayer.PlayerSlotIndex}] rolled " +
            "three consecutive doubles. Third movement was " +
            "cancelled and 1 skipped turn was applied.",
            this);

        bool hasInteractivePenaltyUI =
            tripleDoublePenaltyPanel != null &&
            tripleDoublePenaltyContinueButton != null;

        // Bots continue automatically. If the UI was not wired,
        // humans also fall back to the timed continuation so the
        // match can never deadlock.
        if (isBot ||
            !hasInteractivePenaltyUI)
        {
            StartTripleDoublePenaltyAutoContinue();
        }
    }

    public void ContinueTripleDoublePenalty()
    {
        if (tripleDoublePenaltyPlayer == null)
        {
            return;
        }

        if (tripleDoublePenaltyCoroutine != null)
        {
            StopCoroutine(
                tripleDoublePenaltyCoroutine);

            tripleDoublePenaltyCoroutine = null;
        }

        ResetTripleDoublePenaltyUI();

        tripleDoublePenaltyPlayer = null;

        FinishCurrentTurn();
    }

    private void StartTripleDoublePenaltyAutoContinue()
    {
        if (tripleDoublePenaltyCoroutine != null)
        {
            StopCoroutine(
                tripleDoublePenaltyCoroutine);
        }

        tripleDoublePenaltyCoroutine =
            StartCoroutine(
                TripleDoublePenaltyRoutine());
    }

    private IEnumerator TripleDoublePenaltyRoutine()
    {
        if (tripleDoublePenaltyMessageDuration > 0f)
        {
            yield return new WaitForSeconds(
                tripleDoublePenaltyMessageDuration);
        }

        tripleDoublePenaltyCoroutine = null;

        ContinueTripleDoublePenalty();
    }

    private void ResetTripleDoublePenaltyUI()
    {
        if (tripleDoublePenaltyPanel != null)
        {
            tripleDoublePenaltyPanel.SetActive(
                false);
        }

        if (tripleDoublePenaltyContinueButton != null)
        {
            tripleDoublePenaltyContinueButton
                .interactable = true;
        }
    }

    private void HandleMovementCompleted(
        PlayerPawnMover completedPlayer)
    {
        if (gamePhase !=
                GamePhase.Playing ||
            !waitingForMovement ||
            isMatchFinished)
        {
            return;
        }

        if (completedPlayer !=
            players[currentPlayerIndex])
        {
            return;
        }

        BoardTile landedTile =
            completedPlayer.GetCurrentTile();

        PlayerGameState activePlayerState =
            completedPlayer.GetComponent<
                PlayerGameState>();

        if (activePlayerState == null)
        {
            Debug.LogError(
                "The active pawn does not have a " +
                "PlayerGameState component.",
                completedPlayer);

            FinishCurrentTurn();
            return;
        }

        if (tileResolutionManager != null)
        {
            tileResolutionManager.ResolveTile(
                activePlayerState,
                landedTile,
                HandleResolvedLanding);

            return;
        }

        Debug.LogWarning(
            "TileResolutionManager is not connected. " +
            "Turn will continue without resolving the tile.",
            this);

        HandleResolvedLanding();
    }

    private void HandleResolvedLanding()
    {
        if (isMatchFinished)
        {
            return;
        }

        waitingForMovement = false;

        PlayerGameState activePlayer =
            GetPlayerState(
                currentPlayerIndex);

        bool earnedExtraRoll =
            enableDoublesExtraRollRule &&
            activePlayer != null &&
            activePlayer.IsParticipating &&
            !activePlayer.IsBankrupt &&
            IsCurrentRollDouble() &&
            consecutiveDoublesThisTurn > 0 &&
            (!enableTripleDoublePenalty ||
             consecutiveDoublesThisTurn < 3);

        if (!earnedExtraRoll)
        {
            FinishCurrentTurn();
            return;
        }

        if (turnStatusText != null)
        {
            turnStatusText.text =
                $"Tur {currentRound}/{roundLimit}\n" +
                $"{activePlayer.DisplayName}: " +
                $"çift zar ({lastDieOne}+{lastDieTwo}) — " +
                "tekrar zar at!";
        }

        Debug.Log(
            $"{activePlayer.DisplayName} " +
            $"[Slot {activePlayer.PlayerSlotIndex}] earned " +
            $"an extra roll from doubles. Streak: " +
            $"{consecutiveDoublesThisTurn}/3.",
            this);

        UpdateTurnUI();
    }

    private void FinishCurrentTurn()
    {
        if (isMatchFinished)
        {
            return;
        }

        waitingForMovement = false;
        consecutiveDoublesThisTurn = 0;

        if (RegisterCompletedTurn())
        {
            return;
        }

        AdvanceToNextPlayer();
        BeginCurrentTurn();
    }

    private bool RegisterCompletedTurn()
    {
        completedTurns++;

        PlayerGameState completedPlayer =
            GetPlayerState(
                currentPlayerIndex);

        if (completedPlayer != null &&
            !completedPlayer.IsBankrupt)
        {
            completedActiveSlotsThisRound.Add(
                completedPlayer.PlayerSlotIndex);
        }

        List<PlayerGameState> activePlayers =
            GetActivePlayers();

        if (activePlayers.Count <= 1)
        {
            EndMatch();
            return true;
        }

        bool allActivePlayersCompleted =
            activePlayers.All(
                player =>
                    completedActiveSlotsThisRound
                        .Contains(
                            player.PlayerSlotIndex));

        if (!allActivePlayersCompleted)
        {
            return false;
        }

        if (currentRound >= roundLimit)
        {
            EndMatch();
            return true;
        }

        currentRound++;
        completedActiveSlotsThisRound.Clear();

        return false;
    }

    private void AdvanceToNextPlayer()
    {
        currentTurnOrderIndex =
            (currentTurnOrderIndex + 1) %
            turnOrder.Length;

        currentPlayerIndex =
            turnOrder[currentTurnOrderIndex];
    }

    private void BeginCurrentTurn()
    {
        if (isMatchFinished)
        {
            return;
        }

        if (beginTurnCoroutine != null)
        {
            StopCoroutine(
                beginTurnCoroutine);
        }

        beginTurnCoroutine =
            StartCoroutine(
                BeginCurrentTurnRoutine());
    }

    private IEnumerator BeginCurrentTurnRoutine()
    {
        resolvingTurnStart = true;

        if (rollButton != null)
        {
            rollButton.interactable = false;
        }

        if (GetActivePlayers().Count <= 1)
        {
            resolvingTurnStart = false;
            beginTurnCoroutine = null;

            EndMatch();
            yield break;
        }

        for (int checkedPlayers = 0;
             checkedPlayers < turnOrder.Length;
             checkedPlayers++)
        {
            currentPlayerIndex =
                turnOrder[currentTurnOrderIndex];

            PlayerGameState playerState =
                GetPlayerState(
                    currentPlayerIndex);

            if (playerState == null ||
                !playerState.IsParticipating ||
                playerState.IsBankrupt)
            {
                AdvanceToNextPlayer();
                continue;
            }

            bool skippedTurn =
                playerState.ConsumeSkippedTurn();

            if (!skippedTurn)
            {
                consecutiveDoublesThisTurn = 0;

                resolvingTurnStart = false;
                beginTurnCoroutine = null;

                UpdateTurnUI();
                yield break;
            }

            Debug.Log(
                $"{playerState.DisplayName} " +
                "skipped their turn. " +
                $"Remaining skipped turns: " +
                $"{playerState.TurnsToSkip}.",
                this);

            if (turnStatusText != null)
            {
                turnStatusText.text =
                    $"Tur {currentRound}/{roundLimit}\n" +
                    $"{playerState.DisplayName} " +
                    "bu turu atlıyor";
            }

            if (skippedTurnMessageDuration > 0f)
            {
                yield return new WaitForSeconds(
                    skippedTurnMessageDuration);
            }

            if (RegisterCompletedTurn())
            {
                resolvingTurnStart = false;
                beginTurnCoroutine = null;
                yield break;
            }

            AdvanceToNextPlayer();
        }

        resolvingTurnStart = false;
        beginTurnCoroutine = null;

        EndMatch();
    }

    private void EndMatch()
    {
        if (isMatchFinished)
        {
            return;
        }

        isMatchFinished = true;
        waitingForMovement = false;
        resolvingDiceVisual = false;
        consecutiveDoublesThisTurn = 0;
        resolvingTurnStart = false;

        if (tripleDoublePenaltyCoroutine != null)
        {
            StopCoroutine(
                tripleDoublePenaltyCoroutine);

            tripleDoublePenaltyCoroutine = null;
        }

        tripleDoublePenaltyPlayer = null;
        ResetTripleDoublePenaltyUI();
        resolvingManagementAction = false;

        gamePhase =
            GamePhase.Finished;

        if (rollButton != null)
        {
            rollButton.interactable = false;
        }

        if (turnStatusText != null)
        {
            turnStatusText.text =
                $"Maç tamamlandı\n" +
                $"Tur {currentRound}";
        }

        Debug.Log(
            $"Match finished after " +
            $"{completedTurns} player turns. " +
            $"Active players remaining: " +
            $"{GetActivePlayers().Count}.",
            this);

        if (matchResultManager != null)
        {
            matchResultManager.ShowMatchResult();
        }
        else
        {
            Debug.LogWarning(
                "MatchResultManager is not connected.",
                this);
        }
    }

    private int[]
        GetParticipatingPlayerIndexes()
    {
        if (players == null)
        {
            return new int[0];
        }

        return Enumerable
            .Range(0, players.Length)
            .Where(
                index =>
                {
                    PlayerGameState player =
                        GetPlayerState(index);

                    return player != null &&
                           player.IsParticipating;
                })
            .ToArray();
    }

    private List<PlayerGameState>
        GetActivePlayers()
    {
        List<PlayerGameState> activePlayers =
            new List<PlayerGameState>();

        if (players == null)
        {
            return activePlayers;
        }

        foreach (PlayerPawnMover pawn in players)
        {
            if (pawn == null)
            {
                continue;
            }

            PlayerGameState player =
                pawn.GetComponent<
                    PlayerGameState>();

            if (player != null &&
                player.IsParticipating &&
                !player.IsBankrupt)
            {
                activePlayers.Add(player);
            }
        }

        return activePlayers;
    }

    private PlayerGameState GetPlayerState(
        int playerArrayIndex)
    {
        if (players == null ||
            playerArrayIndex < 0 ||
            playerArrayIndex >=
            players.Length)
        {
            return null;
        }

        PlayerPawnMover pawn =
            players[playerArrayIndex];

        if (pawn == null)
        {
            return null;
        }

        return pawn.GetComponent<
            PlayerGameState>();
    }

    private void UpdateStartingOrderUI()
    {
        int playerArrayIndex =
            startingRollQueue != null &&
            orderRollPlayerIndex >= 0 &&
            orderRollPlayerIndex <
            startingRollQueue.Count
                ? startingRollQueue[
                    orderRollPlayerIndex]
                : -1;

        PlayerGameState player =
            GetPlayerState(
                playerArrayIndex);

        string playerName =
            player != null
                ? player.DisplayName
                : "Oyuncu";

        if (rollButton != null)
        {
            rollButton.interactable =
                player != null &&
                !IsBotPlayer(player);
        }

        if (turnStatusText != null)
        {
            string controlSuffix =
                IsBotPlayer(player)
                    ? " (BOT)"
                    : string.Empty;

            turnStatusText.text =
                resolvingStartingOrderTie
                    ? $"Eşitlik: {playerName}" +
                      $"{controlSuffix} tekrar zar atsın"
                    : $"Başlangıç sırası: " +
                      $"{playerName}{controlSuffix} zar atsın";
        }

        Debug.Log(
            resolvingStartingOrderTie
                ? $"Waiting for starting-order tie reroll: " +
                  $"{playerName}"
                : $"Waiting for starting roll: " +
                  $"{playerName}",
            this);
    }

    private void UpdateTurnUI()
    {
        if (isMatchFinished)
        {
            return;
        }

        PlayerGameState activePlayer =
            GetPlayerState(
                currentPlayerIndex);

        if (activePlayer == null ||
            !activePlayer.IsParticipating ||
            activePlayer.IsBankrupt)
        {
            BeginCurrentTurn();
            return;
        }

        bool isBotTurn =
            IsBotPlayer(activePlayer);

        if (rollButton != null)
        {
            rollButton.interactable =
                !resolvingManagementAction &&
                !isBotTurn;
        }

        if (turnStatusText != null)
        {
            string controlSuffix =
                isBotTurn
                    ? " (BOT)"
                    : string.Empty;

            turnStatusText.text =
                $"Tur {currentRound}/{roundLimit}\n" +
                $"{activePlayer.DisplayName}{controlSuffix} sırası";
        }

        Debug.Log(
            $"Turn started: " +
            $"{activePlayer.DisplayName} " +
            $"[Slot {activePlayer.PlayerSlotIndex}]. " +
            $"Round: {currentRound}/{roundLimit}.",
            this);
    }

    private bool IsBotPlayer(
        PlayerGameState player)
    {
        if (player == null)
        {
            return false;
        }

        BotPlayerController botController =
            player.GetComponent<BotPlayerController>();

        return botController != null &&
               botController.BotEnabled;
    }

    private bool IsSamePlayer(
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

    private bool ValidatePlayers()
    {
        if (players == null ||
            players.Length < 2)
        {
            Debug.LogError(
                "TurnManager requires at least " +
                "two players.",
                this);

            return false;
        }

        HashSet<int> usedStableSlots =
            new HashSet<int>();

        for (int index = 0;
             index < players.Length;
             index++)
        {
            if (players[index] == null)
            {
                Debug.LogError(
                    $"Player array index {index} is empty.",
                    this);

                return false;
            }

            PlayerGameState playerState =
                players[index]
                    .GetComponent<
                        PlayerGameState>();

            if (playerState == null)
            {
                Debug.LogError(
                    $"Player at array index {index} " +
                    "does not have a PlayerGameState " +
                    "component.",
                    players[index]);

                return false;
            }

            if (!usedStableSlots.Add(
                    playerState.PlayerSlotIndex))
            {
                Debug.LogError(
                    "Duplicate Player Slot Index " +
                    $"detected: " +
                    $"{playerState.PlayerSlotIndex}.",
                    playerState);

                return false;
            }

            if (playerState.VisualProfile == null ||
                playerState.OwnershipMaterial == null)
            {
                Debug.LogError(
                    $"{playerState.DisplayName} has an " +
                    "incomplete visual profile.",
                    playerState);

                return false;
            }
        }

        return true;
    }

    private void SubscribeToPlayers()
    {
        foreach (PlayerPawnMover player
                 in players)
        {
            player.MovementCompleted +=
                HandleMovementCompleted;
        }
    }

    private void UnsubscribeFromPlayers()
    {
        if (players == null)
        {
            return;
        }

        foreach (PlayerPawnMover player
                 in players)
        {
            if (player != null)
            {
                player.MovementCompleted -=
                    HandleMovementCompleted;
            }
        }
    }
}
