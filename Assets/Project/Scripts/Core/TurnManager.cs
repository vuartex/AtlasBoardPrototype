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

    [Header("Starting Order")]
    [SerializeField]
    private int orderRollPlayerIndex;

    [SerializeField]
    private int[] startingRolls;

    [SerializeField]
    private int[] turnOrder;

    [Header("Turn Start Presentation")]
    [SerializeField, Min(0f)]
    private float skippedTurnMessageDuration = 1f;

    [Header("Debug")]
    [Tooltip("0 = random. 1–6 forces the normal-turn dice result.")]
    [SerializeField, Range(0, 6)]
    private int debugForcedRoll;

    private int currentTurnOrderIndex;
    private bool waitingForMovement;
    private bool resolvingTurnStart;
    private bool resolvingManagementAction;
    private bool isMatchFinished;
    private bool isMatchStarted;
    private Coroutine beginTurnCoroutine;

    private readonly HashSet<int>
        completedActiveSlotsThisRound =
            new HashSet<int>();

    public int CurrentPlayerIndex =>
        currentPlayerIndex;

    public int CurrentRound =>
        currentRound;

    public int CompletedTurns =>
        completedTurns;

    public int LastRoll =>
        lastRoll;

    public bool IsMatchStarted =>
        isMatchStarted;

    public PlayerGameState CurrentPlayerState =>
        GetPlayerState(currentPlayerIndex);

    public PlayerGameState StartingOrderPlayerState
    {
        get
        {
            if (gamePhase != GamePhase.DeterminingTurnOrder ||
                orderRollPlayerIndex < 0 ||
                orderRollPlayerIndex >= players.Length)
            {
                return null;
            }

            return GetPlayerState(orderRollPlayerIndex);
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
                   !resolvingTurnStart &&
                   !resolvingManagementAction &&
                   activePlayer != null &&
                   !activePlayer.IsBankrupt &&
                   !IsBotPlayer(activePlayer);
        }
    }

    private void Start()
    {
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
            player.IsBankrupt ||
            isMatchFinished ||
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
            resolvingTurnStart ||
            resolvingManagementAction ||
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
            turnOrder.Length == players.Length
                ? turnOrder
                : Enumerable
                    .Range(0, players.Length)
                    .ToArray();

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

        startingRolls =
            new int[players.Length];

        turnOrder =
            new int[players.Length];

        orderRollPlayerIndex = 0;
        currentTurnOrderIndex = 0;
        completedTurns = 0;
        currentRound = 1;

        completedActiveSlotsThisRound.Clear();

        waitingForMovement = false;
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
        if (orderRollPlayerIndex >=
            players.Length)
        {
            return;
        }

        int roll =
            Random.Range(1, 7);

        startingRolls[
            orderRollPlayerIndex] = roll;

        PlayerGameState rollingPlayer =
            GetPlayerState(
                orderRollPlayerIndex);

        Debug.Log(
            $"Starting roll — " +
            $"{rollingPlayer.DisplayName} " +
            $"[Slot {rollingPlayer.PlayerSlotIndex}]: " +
            $"{roll}",
            this);

        orderRollPlayerIndex++;

        if (orderRollPlayerIndex <
            players.Length)
        {
            UpdateStartingOrderUI();
            return;
        }

        DetermineTurnOrder();
    }

    private void DetermineTurnOrder()
    {
        turnOrder = Enumerable
            .Range(0, players.Length)
            .OrderByDescending(
                playerArrayIndex =>
                    startingRolls[
                        playerArrayIndex])
            .ThenBy(
                playerArrayIndex =>
                    GetPlayerState(
                        playerArrayIndex)
                    .PlayerSlotIndex)
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

                        int startingRoll =
                            startingRolls[playerArrayIndex];

                        return
                            $"{player.DisplayName} " +
                            $"[Slot {player.PlayerSlotIndex}] " +
                            $"({startingRoll})";
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

    private void RollForActivePlayer()
    {
        if (waitingForMovement ||
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
            activePlayer.IsBankrupt ||
            activePawn.IsMoving)
        {
            return;
        }

        lastRoll =
            debugForcedRoll > 0
                ? debugForcedRoll
                : Random.Range(1, 7);

        waitingForMovement = true;

        if (rollButton != null)
        {
            rollButton.interactable = false;
        }

        if (turnStatusText != null)
        {
            turnStatusText.text =
                $"Tur {currentRound}/{roundLimit}\n" +
                $"{activePlayer.DisplayName} " +
                $"zar attı: {lastRoll}";
        }

        Debug.Log(
            $"{activePlayer.DisplayName} " +
            $"[Slot {activePlayer.PlayerSlotIndex}] " +
            $"dice result: {lastRoll}",
            this);

        if (!activePawn.MoveBy(lastRoll))
        {
            waitingForMovement = false;
            UpdateTurnUI();
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
                FinishCurrentTurn);

            return;
        }

        Debug.LogWarning(
            "TileResolutionManager is not connected. " +
            "Turn will continue without resolving the tile.",
            this);

        FinishCurrentTurn();
    }

    private void FinishCurrentTurn()
    {
        if (isMatchFinished)
        {
            return;
        }

        waitingForMovement = false;

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
                playerState.IsBankrupt)
            {
                AdvanceToNextPlayer();
                continue;
            }

            bool skippedTurn =
                playerState.ConsumeSkippedTurn();

            if (!skippedTurn)
            {
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
        resolvingTurnStart = false;
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
        PlayerGameState player =
            GetPlayerState(
                orderRollPlayerIndex);

        string playerName =
            player != null
                ? player.DisplayName
                : $"Oyuncu " +
                  $"{orderRollPlayerIndex + 1}";

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
                $"Başlangıç sırası: " +
                $"{playerName}{controlSuffix} zar atsın";
        }

        Debug.Log(
            $"Waiting for starting roll: " +
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
