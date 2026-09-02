using System;
using System.Collections.Generic;
using UnityEngine;

public class AtlasBoardHumanRollTimeoutController : MonoBehaviour
{
    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private AtlasBoardOnlineFoundation onlineFoundation;

    [SerializeField]
    private TradeManager tradeManager;

    [SerializeField, Min(1f)]
    private float humanRollTimeoutSeconds =
        AtlasOnlineDefaults.HumanRollTimeoutSeconds;

    [SerializeField, Min(1)]
    private int afkConsecutiveAutoRollLimit =
        AtlasOnlineDefaults.AfkConsecutiveAutoRollLimit;

    private PlayerGameState windowPlayer;
    private float elapsedEligibleSeconds;
    private bool windowStarted;
    private int remoteManagementHoldSlot = -1;
    private float remoteManagementHoldUntilUnscaledTime;

    private int countedScheduledTurnCompletedTurns = -1;
    private int countedScheduledTurnSlot = -1;

    private readonly Dictionary<int, int>
        localAfkStreakBySlot =
            new Dictionary<int, int>();

    public bool IsCountdownRunning =>
        windowStarted &&
        windowPlayer != null;

    public float RemainingSeconds =>
        Mathf.Max(
            0f,
            humanRollTimeoutSeconds -
            elapsedEligibleSeconds);

    public event Action<
        PlayerGameState,
        int> AfkRemovalTriggered;

    public event Action SessionCloseSuggested;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();

        if (turnManager != null)
        {
            turnManager.HumanRollCommitted +=
                HandleHumanRollCommitted;
        }
    }

    private void OnDisable()
    {
        if (turnManager != null)
        {
            turnManager.HumanRollCommitted -=
                HandleHumanRollCommitted;
        }

        ResetRollWindow();
    }

    private void Update()
    {
        if (turnManager == null)
        {
            EnsureReferences();

            if (turnManager == null)
            {
                return;
            }
        }

        // A Human who deliberately opens Trade is actively using their turn.
        // The 10-second roll AFK clock must not continue in the background while
        // the 25-second Trade decision window is active. Closing Trade starts a
        // completely fresh roll window.
        if (tradeManager == null)
        {
            tradeManager =
                FindSceneComponent<TradeManager>();
        }

        if (tradeManager != null &&
            !tradeManager.IsTradeClosed)
        {
            ResetRollWindow();
            return;
        }

        PlayerGameState candidate =
            ResolveHumanRollCandidate();

        if (remoteManagementHoldSlot >= 0 &&
            Time.unscaledTime >=
                remoteManagementHoldUntilUnscaledTime)
        {
            remoteManagementHoldSlot = -1;
            remoteManagementHoldUntilUnscaledTime = 0f;
        }

        if (candidate != null &&
            candidate.PlayerSlotIndex ==
                remoteManagementHoldSlot)
        {
            ResetRollWindow();
            return;
        }

        if (candidate == null ||
            turnManager.IsPlayerBot(candidate))
        {
            ResetRollWindow();
            return;
        }

        if (windowPlayer != candidate)
        {
            ResetRollWindow();
            windowPlayer = candidate;
        }

        // In an online authoritative Host session the timeout policy must
        // cover RemoteHuman seats too. Local input authority is intentionally
        // NOT required for a Host-generated AFK roll. Followers never generate
        // gameplay RNG.
        bool canTimeoutRoll =
            turnManager.IsOnlineAuthoritativeHost
                ? turnManager.CanHostAuthoritativelyAutoRollHuman(candidate)
                : turnManager.CanPlayerRequestRoll(candidate);

        if (!canTimeoutRoll)
        {
            return;
        }

        if (!windowStarted)
        {
            windowStarted = true;
            elapsedEligibleSeconds = 0f;
        }

        // Time.deltaTime intentionally pauses in a true single-Human
        // local pause (Time.timeScale = 0). Multiplayer/online pause
        // does not freeze Time.timeScale, so its authoritative clock
        // keeps running.
        elapsedEligibleSeconds +=
            Time.deltaTime;

        if (elapsedEligibleSeconds <
            Mathf.Max(1f, humanRollTimeoutSeconds))
        {
            return;
        }

        PlayerGameState timedOutPlayer =
            candidate;

        // Mark the current window consumed before requesting the roll.
        // TurnManager will synchronously raise HumanRollCommitted if
        // the automatic request is accepted.
        ResetRollWindow();

        bool automaticRollAccepted =
            turnManager.IsOnlineAuthoritativeHost
                ? turnManager
                    .TryRequestHostAuthoritativeAutomaticHumanRoll(
                        timedOutPlayer)
                : turnManager.TryRequestAutomaticHumanRoll(
                    timedOutPlayer);

        if (!automaticRollAccepted)
        {
            // Eligibility may have changed in this exact frame.
            // A future eligible frame starts a fresh window.
            return;
        }

        Debug.Log(
            $"{timedOutPlayer.DisplayName} [Slot " +
            $"{timedOutPlayer.PlayerSlotIndex}] did not roll " +
            $"within {humanRollTimeoutSeconds:0.#} seconds. " +
            "Atlas Board performed the roll automatically.",
            this);
    }

    private PlayerGameState ResolveHumanRollCandidate()
    {
        if (turnManager == null ||
            !turnManager.IsMatchStarted)
        {
            return null;
        }

        if (turnManager.IsStartingOrderPhase)
        {
            return turnManager.StartingOrderPlayerState;
        }

        if (turnManager.IsPlayingPhase)
        {
            return turnManager.CurrentPlayerState;
        }

        return null;
    }

    private void HandleHumanRollCommitted(
        PlayerGameState player,
        bool automaticHumanRoll,
        bool isStartingOrder)
    {
        ResetRollWindow();

        if (player == null ||
            isStartingOrder)
        {
            // Starting-order rolls may time out, but they never count
            // toward the 10 scheduled-turn AFK streak.
            return;
        }

        int slot =
            player.PlayerSlotIndex;

        int completedTurnsToken =
            turnManager != null
                ? turnManager.CompletedTurns
                : -1;

        // Doubles can create extra rolls before CompletedTurns changes.
        // Only the first roll opportunity of a scheduled turn is used
        // as the AFK activity sample.
        if (countedScheduledTurnCompletedTurns ==
                completedTurnsToken &&
            countedScheduledTurnSlot == slot)
        {
            return;
        }

        countedScheduledTurnCompletedTurns =
            completedTurnsToken;

        countedScheduledTurnSlot =
            slot;

        bool rolledManually =
            !automaticHumanRoll;

        bool afkLimitReached =
            RegisterSeatActivity(
                player,
                rolledManually,
                out int resultingStreak);

        Debug.Log(
            $"Turn activity: {player.DisplayName} [Slot {slot}] " +
            $"{(rolledManually ? "manual" : "automatic")} first roll. " +
            $"AFK streak={resultingStreak}/" +
            $"{afkConsecutiveAutoRollLimit}.",
            this);

        if (!afkLimitReached)
        {
            return;
        }

        ApplyAfkRemoval(
            player,
            resultingStreak);
    }

    private bool RegisterSeatActivity(
        PlayerGameState player,
        bool rolledManually,
        out int resultingStreak)
    {
        resultingStreak = 0;

        AtlasSessionStateMachine state =
            onlineFoundation != null
                ? onlineFoundation.SessionState
                : null;

        AtlasPlayerSeat seat =
            state != null
                ? state.FindSeatBySlot(
                    player.PlayerSlotIndex)
                : null;

        // Once a real session model exists, it is authoritative for
        // AFK counting. Reconnecting/TemporaryBot seats never reach
        // this Human-roll callback, so disconnects are not mislabeled AFK.
        if (seat != null &&
            seat.HasIdentity &&
            seat.ControllerKind ==
                AtlasSeatControllerKind.Human &&
            seat.ConnectionState ==
                AtlasSeatConnectionState.Connected)
        {
            if (!state.RegisterScheduledTurnRoll(
                    seat.Identity.AccountId,
                    rolledManually,
                    out bool afkReached))
            {
                return false;
            }

            resultingStreak =
                seat.ConsecutiveAutoRollTurns;

            return afkReached;
        }

        // Local/editor fallback lets the policy be tested before a
        // real account/session provider has created online identities.
        int slot =
            player.PlayerSlotIndex;

        if (rolledManually)
        {
            localAfkStreakBySlot[slot] = 0;
            resultingStreak = 0;
            return false;
        }

        localAfkStreakBySlot.TryGetValue(
            slot,
            out int streak);

        streak++;
        localAfkStreakBySlot[slot] = streak;
        resultingStreak = streak;

        return streak >=
               Mathf.Max(
                   1,
                   afkConsecutiveAutoRollLimit);
    }

    private void ApplyAfkRemoval(
        PlayerGameState player,
        int resultingStreak)
    {
        AtlasSessionStateMachine state =
            onlineFoundation != null
                ? onlineFoundation.SessionState
                : null;

        AtlasPlayerSeat seat =
            state != null
                ? state.FindSeatBySlot(
                    player.PlayerSlotIndex)
                : null;

        if (seat != null &&
            seat.HasIdentity &&
            !seat.AfkLockedOut)
        {
            state.HandleAfkRemoval(
                seat.Identity.AccountId);
        }

        BotPlayerController bot =
            player.GetComponent<
                BotPlayerController>();

        if (bot != null)
        {
            bot.SetBotEnabled(true);
        }

        if (turnManager != null)
        {
            turnManager
                .RefreshTurnPresentationForControlChange();
        }

        Debug.LogWarning(
            $"{player.DisplayName} [Slot " +
            $"{player.PlayerSlotIndex}] reached " +
            $"{resultingStreak} consecutive scheduled turns " +
            "whose first roll was automatic and was removed for " +
            "inactivity. The seat is now bot-controlled. " +
            "In a real online session the removed account must be " +
            "blocked from this match and its client routed back to Lobby.",
            this);

        AfkRemovalTriggered?.Invoke(
            player,
            resultingStreak);

        if (state != null &&
            state.ShouldCloseAfterExplicitHumanRemoval())
        {
            SessionCloseSuggested?.Invoke();
        }
    }

    public void SetRemoteManagementHold(
        int playerSlotIndex,
        bool active,
        float safetySeconds = 30f)
    {
        if (!active)
        {
            if (remoteManagementHoldSlot ==
                playerSlotIndex)
            {
                remoteManagementHoldSlot = -1;
                remoteManagementHoldUntilUnscaledTime = 0f;
            }

            ResetRollWindow();
            return;
        }

        remoteManagementHoldSlot =
            Mathf.Clamp(playerSlotIndex, 0, 3);
        remoteManagementHoldUntilUnscaledTime =
            Time.unscaledTime +
            Mathf.Max(5f, safetySeconds);

        ResetRollWindow();
    }

    public void ResetForNewMatchSession()
    {
        remoteManagementHoldSlot = -1;
        remoteManagementHoldUntilUnscaledTime = 0f;
        countedScheduledTurnCompletedTurns = -1;
        countedScheduledTurnSlot = -1;
        localAfkStreakBySlot.Clear();
        ResetRollWindow();
    }

    private void ResetRollWindow()
    {
        windowPlayer = null;
        elapsedEligibleSeconds = 0f;
        windowStarted = false;
    }

    private void EnsureReferences()
    {
        if (turnManager == null)
        {
            turnManager =
                FindSceneComponent<TurnManager>();
        }

        if (onlineFoundation == null)
        {
            onlineFoundation =
                FindSceneComponent<
                    AtlasBoardOnlineFoundation>();
        }

        if (tradeManager == null)
        {
            tradeManager =
                FindSceneComponent<TradeManager>();
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
        AtlasBoardOnlineFoundation newFoundation,
        float newTimeoutSeconds,
        int newAfkLimit)
    {
        turnManager = newTurnManager;
        onlineFoundation = newFoundation;
        humanRollTimeoutSeconds =
            Mathf.Max(1f, newTimeoutSeconds);
        afkConsecutiveAutoRollLimit =
            Mathf.Max(1, newAfkLimit);
    }
#endif
}
