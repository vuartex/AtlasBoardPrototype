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
    private bool isMatchFinished;
    private Coroutine beginTurnCoroutine;

    public int CurrentPlayerIndex => currentPlayerIndex;
    public int CurrentRound => currentRound;
    public int CompletedTurns => completedTurns;
    public int LastRoll => lastRoll;

    private void Start()
    {
        if (!ValidatePlayers())
        {
            enabled = false;
            return;
        }

        SubscribeToPlayers();
        BeginTurnOrderPhase();
    }

    private void OnDestroy()
    {
        UnsubscribeFromPlayers();
    }

    public void HandleRollButton()
    {
        if (isMatchFinished || resolvingTurnStart)
        {
            return;
        }

        if (gamePhase == GamePhase.DeterminingTurnOrder)
        {
            RollForStartingOrder();
            return;
        }

        if (gamePhase == GamePhase.Playing)
        {
            RollForActivePlayer();
        }
    }

    private void BeginTurnOrderPhase()
    {
        gamePhase = GamePhase.DeterminingTurnOrder;

        startingRolls = new int[players.Length];
        turnOrder = new int[players.Length];

        orderRollPlayerIndex = 0;
        currentTurnOrderIndex = 0;
        completedTurns = 0;
        currentRound = 1;

        waitingForMovement = false;
        resolvingTurnStart = false;
        isMatchFinished = false;

        if (rollButton != null)
        {
            rollButton.interactable = true;
        }

        UpdateStartingOrderUI();
    }

    private void RollForStartingOrder()
    {
        if (orderRollPlayerIndex >= players.Length)
        {
            return;
        }

        int roll = Random.Range(1, 7);

        startingRolls[orderRollPlayerIndex] = roll;

        PlayerGameState rollingPlayer =
            GetPlayerState(orderRollPlayerIndex);

        Debug.Log(
            $"Starting roll — {rollingPlayer.DisplayName} " +
            $"[Slot {rollingPlayer.PlayerSlotIndex}]: {roll}",
            this);

        orderRollPlayerIndex++;

        if (orderRollPlayerIndex < players.Length)
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
                    startingRolls[playerArrayIndex])
            .ThenBy(
                playerArrayIndex =>
                    GetPlayerState(playerArrayIndex).PlayerSlotIndex)
            .ToArray();

        string orderDescription =
            string.Join(
                " → ",
                turnOrder.Select(
                    playerArrayIndex =>
                    {
                        PlayerGameState player =
                            GetPlayerState(playerArrayIndex);

                        return
                            $"{player.DisplayName} " +
                            $"[Slot {player.PlayerSlotIndex}] " +
                            $"({startingRolls[playerArrayIndex]})";
                    }));

        Debug.Log(
            $"Starting order: {orderDescription}",
            this);

        gamePhase = GamePhase.Playing;
        currentTurnOrderIndex = 0;
        currentPlayerIndex = turnOrder[currentTurnOrderIndex];

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
            GetPlayerState(currentPlayerIndex);

        if (activePawn == null ||
            activePlayer == null ||
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
                $"{activePlayer.DisplayName} zar attı: {lastRoll}";
        }

        Debug.Log(
            $"{activePlayer.DisplayName} " +
            $"[Slot {activePlayer.PlayerSlotIndex}] dice result: " +
            $"{lastRoll}",
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
        if (gamePhase != GamePhase.Playing ||
            !waitingForMovement ||
            isMatchFinished)
        {
            return;
        }

        if (completedPlayer != players[currentPlayerIndex])
        {
            return;
        }

        BoardTile landedTile =
            completedPlayer.GetCurrentTile();

        PlayerGameState activePlayerState =
            completedPlayer.GetComponent<PlayerGameState>();

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

            // Critical: tile resolution completes the turn through
            // its callback. Do not finish the turn a second time here.
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

        currentRound = Mathf.Min(
            completedTurns / players.Length + 1,
            roundLimit);

        int requiredTurnCount =
            roundLimit * players.Length;

        if (completedTurns < requiredTurnCount)
        {
            return false;
        }

        EndMatch();
        return true;
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
            StopCoroutine(beginTurnCoroutine);
        }

        beginTurnCoroutine =
            StartCoroutine(BeginCurrentTurnRoutine());
    }

    private IEnumerator BeginCurrentTurnRoutine()
    {
        resolvingTurnStart = true;

        if (rollButton != null)
        {
            rollButton.interactable = false;
        }

        for (int checkedPlayers = 0;
             checkedPlayers < turnOrder.Length;
             checkedPlayers++)
        {
            if (isMatchFinished)
            {
                resolvingTurnStart = false;
                beginTurnCoroutine = null;
                yield break;
            }

            currentPlayerIndex =
                turnOrder[currentTurnOrderIndex];

            PlayerGameState playerState =
                GetPlayerState(currentPlayerIndex);

            bool skippedTurn =
                playerState != null &&
                playerState.ConsumeSkippedTurn();

            if (!skippedTurn)
            {
                resolvingTurnStart = false;
                beginTurnCoroutine = null;

                UpdateTurnUI();
                yield break;
            }

            Debug.Log(
                $"{playerState.DisplayName} skipped their turn. " +
                $"Remaining skipped turns: " +
                $"{playerState.TurnsToSkip}.",
                this);

            if (turnStatusText != null)
            {
                turnStatusText.text =
                    $"Tur {currentRound}/{roundLimit}\n" +
                    $"{playerState.DisplayName} bu turu atlıyor";
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

        currentPlayerIndex =
            turnOrder[currentTurnOrderIndex];

        resolvingTurnStart = false;
        beginTurnCoroutine = null;

        UpdateTurnUI();
    }

    private void EndMatch()
    {
        isMatchFinished = true;
        waitingForMovement = false;
        resolvingTurnStart = false;
        gamePhase = GamePhase.Finished;

        if (rollButton != null)
        {
            rollButton.interactable = false;
        }

        if (turnStatusText != null)
        {
            turnStatusText.text =
                $"Maç tamamlandı\n{roundLimit} tur";
        }

        Debug.Log(
            $"Match finished after {completedTurns} player turns.",
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

    private PlayerGameState GetPlayerState(
        int playerArrayIndex)
    {
        if (players == null ||
            playerArrayIndex < 0 ||
            playerArrayIndex >= players.Length)
        {
            return null;
        }

        PlayerPawnMover pawn =
            players[playerArrayIndex];

        if (pawn == null)
        {
            return null;
        }

        return pawn.GetComponent<PlayerGameState>();
    }

    private void UpdateStartingOrderUI()
    {
        PlayerGameState player =
            GetPlayerState(orderRollPlayerIndex);

        string playerName =
            player != null
                ? player.DisplayName
                : $"Oyuncu {orderRollPlayerIndex + 1}";

        if (turnStatusText != null)
        {
            turnStatusText.text =
                $"Başlangıç sırası: {playerName} zar atsın";
        }

        Debug.Log(
            $"Waiting for starting roll: {playerName}",
            this);
    }

    private void UpdateTurnUI()
    {
        if (isMatchFinished)
        {
            return;
        }

        PlayerGameState activePlayer =
            GetPlayerState(currentPlayerIndex);

        if (rollButton != null)
        {
            rollButton.interactable = true;
        }

        if (turnStatusText != null)
        {
            turnStatusText.text =
                $"Tur {currentRound}/{roundLimit}\n" +
                $"{activePlayer.DisplayName} sırası";
        }

        Debug.Log(
            $"Turn started: {activePlayer.DisplayName} " +
            $"[Slot {activePlayer.PlayerSlotIndex}]. " +
            $"Round: {currentRound}/{roundLimit}.",
            this);
    }

    private bool ValidatePlayers()
    {
        if (players == null || players.Length < 2)
        {
            Debug.LogError(
                "TurnManager requires at least two players.",
                this);

            return false;
        }

        HashSet<int> usedStableSlots = new HashSet<int>();

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
                players[index].GetComponent<PlayerGameState>();

            if (playerState == null)
            {
                Debug.LogError(
                    $"Player at array index {index} does not have " +
                    "a PlayerGameState component.",
                    players[index]);

                return false;
            }

            if (!usedStableSlots.Add(playerState.PlayerSlotIndex))
            {
                Debug.LogError(
                    $"Duplicate Player Slot Index detected: " +
                    $"{playerState.PlayerSlotIndex}.",
                    playerState);

                return false;
            }

            if (playerState.VisualProfile == null ||
                playerState.OwnershipMaterial == null)
            {
                Debug.LogError(
                    $"{playerState.DisplayName} has an incomplete " +
                    "visual profile.",
                    playerState);

                return false;
            }
        }

        return true;
    }

    private void SubscribeToPlayers()
    {
        foreach (PlayerPawnMover player in players)
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

        foreach (PlayerPawnMover player in players)
        {
            if (player != null)
            {
                player.MovementCompleted -=
                    HandleMovementCompleted;
            }
        }
    }
}
