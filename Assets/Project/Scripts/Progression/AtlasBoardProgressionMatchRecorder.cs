using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AtlasBoardProgressionMatchRecorder : MonoBehaviour
{
    private AtlasBoardProgressionBridge progressionBridge;
    private AtlasBoardLobbyRuntimeBridge lobbyBridge;
    private AtlasBoardMatchRuntimeBridge matchBridge;
    private AtlasBoardTurnDiceNetworkCoordinator coordinator;
    private TurnManager turnManager;
    private MatchResultManager matchResultManager;

    private readonly AtlasBoardProgressionSlotTelemetry[] telemetry =
    {
        new AtlasBoardProgressionSlotTelemetry { slotIndex = 0 },
        new AtlasBoardProgressionSlotTelemetry { slotIndex = 1 },
        new AtlasBoardProgressionSlotTelemetry { slotIndex = 2 },
        new AtlasBoardProgressionSlotTelemetry { slotIndex = 3 }
    };

    private string observedMatchId = string.Empty;
    private string recordedMatchId = string.Empty;
    private bool recordInFlight;
    private float nextResolveAt;
    private float nextRecordAttemptAt;

    private void Awake()
    {
        progressionBridge = GetComponent<AtlasBoardProgressionBridge>();
    }

    private void OnDestroy()
    {
        UnsubscribeTurnManager();
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextResolveAt)
        {
            nextResolveAt = Time.unscaledTime + 1f;
            ResolveReferences();
        }

        string matchId = ResolveCurrentMatchId();
        if (!string.Equals(matchId, observedMatchId, StringComparison.Ordinal))
        {
            BeginObservedMatch(matchId);
        }

        if (string.IsNullOrWhiteSpace(matchId) ||
            recordInFlight ||
            string.Equals(recordedMatchId, matchId, StringComparison.Ordinal) ||
            Time.unscaledTime < nextRecordAttemptAt)
        {
            return;
        }

        if (coordinator == null ||
            !coordinator.IsOnlineSessionActive ||
            !coordinator.LocalIsHost ||
            matchResultManager == null ||
            !matchResultManager.ResultShown)
        {
            return;
        }

        RecordCompletedMatchAsync(matchId);
    }

    private void ResolveReferences()
    {
        if (progressionBridge == null)
        {
            progressionBridge = GetComponent<AtlasBoardProgressionBridge>();
        }

        if (lobbyBridge == null)
        {
            lobbyBridge =
                FindSceneObject<AtlasBoardLobbyRuntimeBridge>();
        }

        if (matchBridge == null)
        {
            matchBridge =
                FindSceneObject<AtlasBoardMatchRuntimeBridge>();
        }

        if (coordinator == null)
        {
            coordinator =
                FindSceneObject<AtlasBoardTurnDiceNetworkCoordinator>();
        }

        TurnManager nextTurnManager = FindSceneObject<TurnManager>();
        if (nextTurnManager != turnManager)
        {
            UnsubscribeTurnManager();
            turnManager = nextTurnManager;
            SubscribeTurnManager();
        }

        if (matchResultManager == null)
        {
            matchResultManager = FindSceneObject<MatchResultManager>();
        }
    }

    private void SubscribeTurnManager()
    {
        if (turnManager == null)
        {
            return;
        }

        turnManager.AuthoritativeDiceCommitted -=
            HandleAuthoritativeDiceCommitted;
        turnManager.AuthoritativeDiceCommitted +=
            HandleAuthoritativeDiceCommitted;
    }

    private void UnsubscribeTurnManager()
    {
        if (turnManager != null)
        {
            turnManager.AuthoritativeDiceCommitted -=
                HandleAuthoritativeDiceCommitted;
        }
    }

    private void HandleAuthoritativeDiceCommitted(
        PlayerGameState player,
        int dieOne,
        int dieTwo,
        bool startingOrder)
    {
        if (startingOrder ||
            player == null ||
            coordinator == null ||
            !coordinator.LocalIsHost)
        {
            return;
        }

        int slot = Mathf.Clamp(player.PlayerSlotIndex, 0, 3);
        AtlasBoardProgressionSlotTelemetry item = telemetry[slot];
        int total = Mathf.Clamp(dieOne + dieTwo, 0, 12);
        item.diceRolls++;
        item.totalDiceValue += total;
        item.highestRoll = Mathf.Max(item.highestRoll, total);
        if (dieOne > 0 && dieOne == dieTwo)
        {
            item.doublesRolled++;
        }
    }

    private string ResolveCurrentMatchId()
    {
        if (matchBridge != null &&
            !string.IsNullOrWhiteSpace(matchBridge.CurrentMatchId))
        {
            return matchBridge.CurrentMatchId;
        }

        if (lobbyBridge != null &&
            lobbyBridge.CurrentSnapshot != null)
        {
            return lobbyBridge.CurrentSnapshot.MatchId ?? string.Empty;
        }

        return string.Empty;
    }

    private void BeginObservedMatch(string matchId)
    {
        observedMatchId = matchId ?? string.Empty;
        recordedMatchId = string.Empty;
        recordInFlight = false;
        nextRecordAttemptAt = 0f;

        for (int slot = 0; slot < telemetry.Length; slot++)
        {
            telemetry[slot].slotIndex = slot;
            telemetry[slot].diceRolls = 0;
            telemetry[slot].doublesRolled = 0;
            telemetry[slot].totalDiceValue = 0;
            telemetry[slot].highestRoll = 0;
        }
    }

    private async void RecordCompletedMatchAsync(string matchId)
    {
        if (progressionBridge == null)
        {
            nextRecordAttemptAt = Time.unscaledTime + 2f;
            return;
        }

        recordInFlight = true;
        try
        {
            List<AtlasBoardProgressionSlotTelemetry> copy =
                new List<AtlasBoardProgressionSlotTelemetry>();
            foreach (AtlasBoardProgressionSlotTelemetry item in telemetry)
            {
                copy.Add(
                    new AtlasBoardProgressionSlotTelemetry
                    {
                        slotIndex = item.slotIndex,
                        diceRolls = item.diceRolls,
                        doublesRolled = item.doublesRolled,
                        totalDiceValue = item.totalDiceValue,
                        highestRoll = item.highestRoll
                    });
            }

            int completedTurns =
                turnManager != null ? turnManager.CompletedTurns : 0;

            AtlasBoardProgressionRecordResult result =
                await progressionBridge.RecordCompletedMatchAsync(
                    matchId,
                    completedTurns,
                    copy);

            if (result.Success)
            {
                recordedMatchId = matchId;
                Debug.Log(
                    "AtlasBoard Phase 10 progression recorded completed match " +
                    $"{matchId}. Accounts={result.AffectedAccountCount}; " +
                    $"replay={result.IdempotentReplay}.",
                    this);

                await progressionBridge.GetProfileAsync();
                return;
            }

            nextRecordAttemptAt = Time.unscaledTime +
                (string.Equals(
                    result.ErrorKey,
                    "progression.error.match_not_final",
                    StringComparison.OrdinalIgnoreCase)
                    ? 1.5f
                    : 4f);

            Debug.LogWarning(
                "AtlasBoard Phase 10 match progression finalize retry. " +
                $"key={result.ErrorKey}; technical={result.TechnicalMessage}",
                this);
        }
        finally
        {
            recordInFlight = false;
        }
    }

    private static T FindSceneObject<T>()
        where T : UnityEngine.Object
    {
        T[] all = Resources.FindObjectsOfTypeAll<T>();
        foreach (T item in all)
        {
            if (item is Component component &&
                component.gameObject.scene.IsValid())
            {
                return item;
            }
        }

        return null;
    }
}
