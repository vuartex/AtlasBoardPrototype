using System.Collections.Generic;
using UnityEngine;

public class StartRewardManager : MonoBehaviour
{
    [Header("Players")]
    [SerializeField]
    private PlayerPawnMover[] playerPawns;

    [Header("Start Reward")]
    [SerializeField, Min(0)]
    private int startPassReward = 200;

    private void Start()
    {
        if (!ValidateConfiguration())
        {
            enabled = false;
            return;
        }

        SubscribeToPlayers();
    }

    private void OnDestroy()
    {
        UnsubscribeFromPlayers();
    }

    private void HandlePassedStart(
        PlayerPawnMover pawn)
    {
        if (pawn == null)
        {
            return;
        }

        PlayerGameState player =
            pawn.GetComponent<PlayerGameState>();

        if (player == null)
        {
            Debug.LogError(
                "The pawn that passed Start does not have " +
                "a PlayerGameState component.",
                pawn);

            return;
        }

        player.AddMoney(startPassReward);

        Debug.Log(
            $"{player.DisplayName} [Slot {player.PlayerSlotIndex}] " +
            $"passed Start and received {startPassReward}.",
            this);
    }

    private bool ValidateConfiguration()
    {
        if (playerPawns == null ||
            playerPawns.Length == 0)
        {
            Debug.LogError(
                "StartRewardManager requires player pawns.",
                this);

            return false;
        }

        HashSet<int> usedStableSlots =
            new HashSet<int>();

        foreach (PlayerPawnMover pawn in playerPawns)
        {
            if (pawn == null)
            {
                Debug.LogError(
                    "StartRewardManager contains an empty pawn slot.",
                    this);

                return false;
            }

            PlayerGameState player =
                pawn.GetComponent<PlayerGameState>();

            if (player == null)
            {
                Debug.LogError(
                    $"{pawn.name} does not have a PlayerGameState.",
                    pawn);

                return false;
            }

            if (!usedStableSlots.Add(player.PlayerSlotIndex))
            {
                Debug.LogError(
                    $"Duplicate Player Slot Index detected: " +
                    $"{player.PlayerSlotIndex}.",
                    player);

                return false;
            }
        }

        return true;
    }

    private void SubscribeToPlayers()
    {
        foreach (PlayerPawnMover pawn in playerPawns)
        {
            pawn.PassedStart += HandlePassedStart;
        }
    }

    private void UnsubscribeFromPlayers()
    {
        if (playerPawns == null)
        {
            return;
        }

        foreach (PlayerPawnMover pawn in playerPawns)
        {
            if (pawn != null)
            {
                pawn.PassedStart -= HandlePassedStart;
            }
        }
    }
}
