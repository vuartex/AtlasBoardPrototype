#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Development-only two-client QA shortcut. It never calls EndMatch directly:
/// F8 moves the authoritative host to the configured final round; F9 also
/// pre-completes the other active slots so the normal current-turn completion
/// flows through TurnManager's ordinary end-of-match path.
/// Compiled out of non-development players.
/// </summary>
[DefaultExecutionOrder(31000)]
public sealed class AtlasBoardDevelopmentMatchFastForwardQA : MonoBehaviour
{
    private const BindingFlags InstancePrivate =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private static AtlasBoardDevelopmentMatchFastForwardQA instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject host = new GameObject("AtlasBoard_Development_Match_FastForward_QA");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AtlasBoardDevelopmentMatchFastForwardQA>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (PressedF9()) ApplyFinalRoundState(true);
        else if (PressedF8()) ApplyFinalRoundState(false);
    }

    private static bool PressedF8()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F8);
#else
        return false;
#endif
    }

    private static bool PressedF9()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F9);
#else
        return false;
#endif
    }

    private static void ApplyFinalRoundState(bool finishAfterCurrentTurn)
    {
        TurnManager turnManager =
            UnityEngine.Object.FindAnyObjectByType<TurnManager>();

        if (turnManager == null)
        {
            Debug.LogWarning("ATB_DEV_MATCH_FAST_FORWARD rejected=true reason=turn_manager_missing");
            return;
        }

        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardTurnDiceNetworkCoordinator>();

        if (coordinator != null && coordinator.IsPreparedOnlineMatch &&
            !coordinator.LocalIsHost)
        {
            Debug.LogWarning(
                "ATB_DEV_MATCH_FAST_FORWARD rejected=true " +
                "reason=online_follower use_on_current_host=true");
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

        if (currentRoundField == null || currentPlayerIndexField == null ||
            playersField == null || completedSlotsField == null)
        {
            Debug.LogError(
                "ATB_DEV_MATCH_FAST_FORWARD rejected=true reason=turn_manager_contract_changed");
            return;
        }

        int roundLimit = Mathf.Max(1, turnManager.RoundLimit);
        currentRoundField.SetValue(turnManager, roundLimit);
        int currentPlayerIndex = (int)currentPlayerIndexField.GetValue(turnManager);
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
                    "ATB_DEV_MATCH_FAST_FORWARD rejected=true reason=turn_state_missing");
                return;
            }

            completedSlots.Clear();
            int currentSlot = -1;
            if (currentPlayerIndex >= 0 && currentPlayerIndex < pawns.Length &&
                pawns[currentPlayerIndex] != null)
            {
                PlayerGameState currentState =
                    pawns[currentPlayerIndex].GetComponent<PlayerGameState>();
                if (currentState != null) currentSlot = currentState.PlayerSlotIndex;
            }

            foreach (PlayerPawnMover pawn in pawns)
            {
                if (pawn == null) continue;
                PlayerGameState state = pawn.GetComponent<PlayerGameState>();
                if (state == null || state.IsBankrupt ||
                    state.PlayerSlotIndex == currentSlot) continue;
                completedSlots.Add(state.PlayerSlotIndex);
            }
            preCompleted = completedSlots.Count;
        }

        updateTurnUi?.Invoke(turnManager, null);
        Debug.Log(
            "ATB_DEV_MATCH_FAST_FORWARD applied=true mode=" +
            (finishAfterCurrentTurn ? "finish_after_current_turn" : "final_round") +
            " round=" + roundLimit + "/" + roundLimit +
            " current_player_index=" + currentPlayerIndex +
            " precompleted_other_slots=" + preCompleted +
            " online_host=" +
            (coordinator != null && coordinator.IsPreparedOnlineMatch
                ? coordinator.LocalIsHost.ToString().ToLowerInvariant()
                : "offline") +
            " hotkey=" + (finishAfterCurrentTurn ? "F9" : "F8"));
    }
}
#endif
