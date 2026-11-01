#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardRematchFastForwardQA
{
    private const BindingFlags InstancePrivate =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Atlas Board/QA Diagnostics/Match/Set Current Match To Final Round", priority = 905)]
    public static void SetCurrentMatchToFinalRound()
    {
        ApplyFinalRoundState(finishAfterCurrentTurn: false);
    }

    [MenuItem("Atlas Board/QA Diagnostics/Match/Finish After Current Turn For Rematch QA", priority = 906)]
    public static void FinishAfterCurrentTurnForRematchQa()
    {
        ApplyFinalRoundState(finishAfterCurrentTurn: true);
    }

    [MenuItem("Atlas Board/QA Diagnostics/Match/Set Current Match To Final Round", true)]
    [MenuItem("Atlas Board/QA Diagnostics/Match/Finish After Current Turn For Rematch QA", true)]
    private static bool ValidateCommands()
    {
        return EditorApplication.isPlaying;
    }

    private static void ApplyFinalRoundState(bool finishAfterCurrentTurn)
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("ATB_REMATCH_QA_FAST_FORWARD rejected=true reason=not_in_play_mode");
            return;
        }

        TurnManager turnManager =
            UnityEngine.Object.FindFirstObjectByType<TurnManager>();

        if (turnManager == null)
        {
            Debug.LogWarning("ATB_REMATCH_QA_FAST_FORWARD rejected=true reason=turn_manager_missing");
            return;
        }

        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            UnityEngine.Object.FindFirstObjectByType<AtlasBoardTurnDiceNetworkCoordinator>();

        if (coordinator != null &&
            coordinator.IsPreparedOnlineMatch &&
            !coordinator.LocalIsHost)
        {
            Debug.LogWarning(
                "ATB_REMATCH_QA_FAST_FORWARD rejected=true " +
                "reason=editor_is_online_follower use_on_current_host=true");
            return;
        }

        FieldInfo currentRoundField =
            typeof(TurnManager).GetField("currentRound", InstancePrivate);
        FieldInfo currentPlayerIndexField =
            typeof(TurnManager).GetField("currentPlayerIndex", InstancePrivate);
        FieldInfo playersField =
            typeof(TurnManager).GetField("players", InstancePrivate);
        FieldInfo completedSlotsField =
            typeof(TurnManager).GetField("completedActiveSlotsThisRound", InstancePrivate);
        MethodInfo updateTurnUi =
            typeof(TurnManager).GetMethod("UpdateTurnUI", InstancePrivate);

        if (currentRoundField == null ||
            currentPlayerIndexField == null ||
            playersField == null ||
            completedSlotsField == null)
        {
            Debug.LogError(
                "ATB_REMATCH_QA_FAST_FORWARD rejected=true reason=turn_manager_contract_changed");
            return;
        }

        int roundLimit = Mathf.Max(1, turnManager.RoundLimit);
        currentRoundField.SetValue(turnManager, roundLimit);

        int currentPlayerIndex =
            (int)currentPlayerIndexField.GetValue(turnManager);

        int preCompleted = 0;

        if (finishAfterCurrentTurn)
        {
            HashSet<int> completedSlots =
                completedSlotsField.GetValue(turnManager) as HashSet<int>;
            PlayerPawnMover[] pawns =
                playersField.GetValue(turnManager) as PlayerPawnMover[];

            if (completedSlots == null || pawns == null)
            {
                Debug.LogError(
                    "ATB_REMATCH_QA_FAST_FORWARD rejected=true reason=turn_state_missing");
                return;
            }

            completedSlots.Clear();

            int currentSlot = -1;
            if (currentPlayerIndex >= 0 && currentPlayerIndex < pawns.Length &&
                pawns[currentPlayerIndex] != null)
            {
                PlayerGameState currentState =
                    pawns[currentPlayerIndex].GetComponent<PlayerGameState>();
                if (currentState != null)
                {
                    currentSlot = currentState.PlayerSlotIndex;
                }
            }

            foreach (PlayerPawnMover pawn in pawns)
            {
                if (pawn == null)
                {
                    continue;
                }

                PlayerGameState state = pawn.GetComponent<PlayerGameState>();
                if (state == null || state.IsBankrupt)
                {
                    continue;
                }

                if (state.PlayerSlotIndex == currentSlot)
                {
                    continue;
                }

                completedSlots.Add(state.PlayerSlotIndex);
            }

            preCompleted = completedSlots.Count;
        }

        updateTurnUi?.Invoke(turnManager, null);
        EditorUtility.SetDirty(turnManager);

        Debug.Log(
            "ATB_REMATCH_QA_FAST_FORWARD applied=true " +
            "mode=" + (finishAfterCurrentTurn ? "finish_after_current_turn" : "final_round") +
            " round=" + roundLimit + "/" + roundLimit +
            " current_player_index=" + currentPlayerIndex +
            " precompleted_other_slots=" + preCompleted +
            " online_host=" +
            (coordinator != null && coordinator.IsPreparedOnlineMatch
                ? coordinator.LocalIsHost.ToString().ToLowerInvariant()
                : "offline"));
    }
}
#endif
