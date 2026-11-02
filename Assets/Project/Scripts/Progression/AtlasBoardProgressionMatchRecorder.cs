using System;
using System.Collections.Generic;
using UnityEngine;


[DisallowMultipleComponent]
public sealed class AtlasBoardProgressionMatchRecorder : MonoBehaviour
{
    private static AtlasBoardProgressionMatchRecorder currentRecorder;
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
    private float nextParticipationAttemptAt;
    private string registeredParticipationKey = string.Empty;
    private bool participationInFlight;


    // Mirror already-committed host totals while a follower. On a legitimate
    // host migration the new host resumes counters instead of losing the
    // predecessor's dice and doubles achievement progress.
    public static void HydrateCareerDiceCounts(int[] rolls, int[] doubles)
    {
        var recorder = currentRecorder;
        if (recorder == null || recorder.coordinator == null ||
            recorder.coordinator.LocalIsHost || rolls == null || doubles == null)
            return;
        for (int i = 0; i < 4 && i < rolls.Length && i < doubles.Length; i++)
        {
            recorder.telemetry[i].diceRolls = Mathf.Max(recorder.telemetry[i].diceRolls,
                Mathf.Clamp(rolls[i], 0, 10000));
            recorder.telemetry[i].doublesRolled = Mathf.Max(recorder.telemetry[i].doublesRolled,
                Mathf.Clamp(doubles[i], 0, 10000));
        }
    }


    public static int[] HostDiceCounts(bool doubles)
    {
        var result = new int[4];
        var recorder = currentRecorder;
        if (recorder == null || recorder.coordinator == null ||
            !recorder.coordinator.LocalIsHost) return result;
        for (int i = 0; i < 4; i++)
            result[i] = Mathf.Clamp(doubles ? recorder.telemetry[i].doublesRolled :
                recorder.telemetry[i].diceRolls, 0, 10000);
        return result;
    }


    private void Awake()
    {
        currentRecorder = this;
        progressionBridge = GetComponent<AtlasBoardProgressionBridge>();
    }


    private void OnDestroy()
    {
        UnsubscribeTurnManager();
        if (currentRecorder == this) currentRecorder = null;
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


        if (!string.IsNullOrWhiteSpace(matchId) && coordinator != null &&
            coordinator.IsOnlineSessionActive &&
            Time.unscaledTime >= nextParticipationAttemptAt &&
            !participationInFlight && lobbyBridge != null &&
            !lobbyBridge.UsingLocalEmulators)
        {
            RegisterParticipationAsync(matchId);
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
            matchResultManager == null ||
            !matchResultManager.ResultShown)
        {
            return;
        }


        // Every authenticated Firebase UID, including the persistent REST-only
        // Windows Guest, enters the same durable Career/Gold pipeline. Local
        // history is still saved first so UI/navigation cannot erase the match.

        // Non-currency, two-human Career claims. A transient remote-human
        // disconnect must not permanently close our recorder. It still cannot
        // award XP without an eligible, independently signed RTDB claim.
        if (lobbyBridge == null || !lobbyBridge.UsingLocalEmulators)
        {
            // Persist this account's completed observation before the remote
            // claim. A denied/delayed peer-attested claim must not make the
            // match disappear from Steam Career on the next UI refresh.
            SaveLocallyObservedMatch(
                matchId, AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession);
            RecordLiveRtdbMatchAsync(matchId);
            return;
        }
        // Retain the legacy emulator-only verification path for QA.
        if (coordinator.LocalIsHost) RecordCompletedMatchAsync(matchId);
    }


    private void SaveLocallyObservedMatch(string matchId, bool isGuest)
    {
        if (lobbyBridge == null || coordinator == null ||
            matchResultManager == null ||
            !matchResultManager.ResultShown) return;
        string uid = lobbyBridge.CurrentAccountId;
        if (string.IsNullOrEmpty(uid)) return;
        var result = matchResultManager.BuildOnlineResultSnapshot();
        if (result == null || !result.valid) return;
        int slot = -1;
        for (int i = 0; i < 4; i++)
            if (coordinator.IsLocallyControlledHumanSlot(i)) { slot = i; break; }
        if (slot < 0 || result.participating == null ||
            slot >= result.participating.Length || !result.participating[slot])
            return;
        bool win = result.winnerSlots != null &&
            Array.Exists(result.winnerSlots, x => x == slot);
        int winnerCount = result.winnerSlots != null ? result.winnerSlots.Length : 0;
        string kind = win ? winnerCount > 1 ? "tie" : "win" : "loss";
        var settings = lobbyBridge.CurrentSnapshot != null ?
            lobbyBridge.CurrentSnapshot.Settings : null;
        var summary = new AtlasBoardProgressionMatchSummary
        {
            matchId = matchId,
            lobbyId = lobbyBridge.CurrentLobbyId,
            mode = "local_guest_unverified",
            mapId = settings != null ? settings.MapId : "unknown",
            themeId = settings != null ? settings.ThemeId : "classic_table",
            roundLimit = settings != null ? settings.RoundLimit : 0,
            result = kind, place = kind == "loss" ? 2 : 1,
            playerCount = result.participating != null ?
                Array.FindAll(result.participating, participating => participating).Length : 0,
            localSlotIndex = slot,
            bankrupt = result.bankrupt != null && slot < result.bankrupt.Length &&
                result.bankrupt[slot],
            finalCash = result.cash[slot],
            propertyCount = result.propertyCount[slot],
            propertyValue = result.propertyValue[slot],
            developmentLevels = result.developmentLevels[slot],
            developmentValue = result.developmentValue[slot],
            netWorth = result.netWorth[slot],
            highestMatchNetWorth = result.highestNetWorth,
            completedTurns = turnManager != null ? turnManager.CompletedTurns : 0,
            completedRounds = turnManager != null ? turnManager.CurrentRound : 0,
            diceRolls = telemetry[slot].diceRolls,
            doublesRolled = telemetry[slot].doublesRolled,
            totalDiceValue = telemetry[slot].totalDiceValue,
            highestRoll = telemetry[slot].highestRoll,
            finishedAtEpochMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            xpAwarded = 0,
            goldAwarded = 0,
            levelAfter = 1
        };
        if (isGuest)
            AtlasBoardProgressionBridge.RecordLocalGuestMatch(uid, summary);
        else
            AtlasBoardProgressionBridge.RecordLocalSteamObservation(uid, summary);
        if (progressionBridge != null) _ = progressionBridge.GetProfileAsync();
    }


    private void ResolveReferences()
    {
        if (progressionBridge == null)
        {
            progressionBridge = GetComponent<AtlasBoardProgressionBridge>();
        }


        if (lobbyBridge == null)
        {
            lobbyBridge = FindSceneObject<AtlasBoardLobbyRuntimeBridge>();
        }


        if (matchBridge == null)
        {
            matchBridge = FindSceneObject<AtlasBoardMatchRuntimeBridge>();
        }


        if (coordinator == null)
        {
            coordinator = FindSceneObject<AtlasBoardTurnDiceNetworkCoordinator>();
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


        turnManager.AuthoritativeDiceCommitted -= HandleAuthoritativeDiceCommitted;
        turnManager.AuthoritativeDiceCommitted += HandleAuthoritativeDiceCommitted;
    }


    private void UnsubscribeTurnManager()
    {
        if (turnManager != null)
        {
            turnManager.AuthoritativeDiceCommitted -= HandleAuthoritativeDiceCommitted;
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
        nextParticipationAttemptAt = 0f;
        registeredParticipationKey = string.Empty;
        participationInFlight = false;


        for (int slot = 0; slot < telemetry.Length; slot++)
        {
            telemetry[slot].slotIndex = slot;
            telemetry[slot].diceRolls = 0;
            telemetry[slot].doublesRolled = 0;
            telemetry[slot].totalDiceValue = 0;
            telemetry[slot].highestRoll = 0;
        }
    }


    private async void RegisterParticipationAsync(string matchId)
    {
        string account = lobbyBridge != null ? lobbyBridge.CurrentAccountId : "";
        string key = matchId + ":" + account;
        if (string.Equals(registeredParticipationKey, key, StringComparison.Ordinal))
            return;
        participationInFlight = true;
        nextParticipationAttemptAt = Time.unscaledTime + 2f;
        try
        {
            bool ok = await AtlasBoardRtdbCareerJournal.RegisterParticipationAsync(
                matchId, lobbyBridge, coordinator);
            if (ok && string.Equals(matchId, observedMatchId, StringComparison.Ordinal) &&
                lobbyBridge != null && lobbyBridge.CurrentAccountId == account)
                registeredParticipationKey = key;
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_CAREER_PARTICIPATION pending=" + e.GetType().Name);
        }
        finally { participationInFlight = false; }
    }


    private async void RecordLiveRtdbMatchAsync(string matchId)
    {
        recordInFlight = true;
        try
        {
            AtlasBoardRtdbCareerJournal.RecordStatus result =
                await AtlasBoardRtdbCareerJournal.RecordAsync(matchId,
                    lobbyBridge, coordinator, matchResultManager, turnManager,
                    telemetry);
            if (result.Applied &&
                string.Equals(result.Reason,
                    "career_and_gold_saved",
                    StringComparison.Ordinal))
            {
                recordedMatchId = matchId;
                Debug.Log("ATB_CAREER_MATCH_FINALIZED career=true gold=true match=" +
                    matchId);
                if (progressionBridge != null)
                    await progressionBridge.GetProfileAsync();
            }
            else if (result.Applied)
            {
                // Defensive compatibility: an Applied result that is not the
                // complete Career+Gold terminal state must remain retryable.
                SaveLocallyObservedMatch(matchId, AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession);
                nextRecordAttemptAt = Time.unscaledTime + 0.75f;
                Debug.Log("ATB_CAREER_RTDB_CLAIM retry=true reason=" +
                    result.Reason + " match=" + matchId);
            }
            else if (result.Reason == "not_eligible")
            {
                // Still display an actual, completed match as UNVERIFIED
                // local Steam history. Never mint server XP or Gold here.
                SaveLocallyObservedMatch(matchId, AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession);
                recordedMatchId = matchId;
                Debug.Log("ATB_CAREER_RTDB_CLAIM ineligible=" + result.Reason +
                    " xp=0 match=" + matchId);
            }
            else
            {
                // A second human can disconnect briefly immediately before
                // the finish. Retry instead of marking the match permanently
                // recorded. The RTDB rules still decide whether a claim can
                // ever be rewarded; no local/host-only XP fallback is granted.
                SaveLocallyObservedMatch(matchId, AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession);
                float retryDelay =
                    result.Reason == "final_proof_pending" ||
                    result.Reason == "local_attestation_pending" ||
                    result.Reason == "waiting_for_peer" ||
                    result.Reason == "xp_saved_gold_pending"
                        ? 0.75f
                        : 3f;
                nextRecordAttemptAt = Time.unscaledTime + retryDelay;
                Debug.Log("ATB_CAREER_RTDB_CLAIM pending=" + result.Reason +
                    " retry_seconds=" + retryDelay.ToString("0.00") +
                    " match=" + matchId);
            }
        }
        catch (Exception ex)
        {
            nextRecordAttemptAt = Time.unscaledTime + 1.5f;
            Debug.LogWarning("ATB_CAREER_RTDB_CLAIM error=" + ex.Message +
                " retry_seconds=1.50 match=" + matchId);
        }
        finally { recordInFlight = false; }
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
                copy.Add(new AtlasBoardProgressionSlotTelemetry
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