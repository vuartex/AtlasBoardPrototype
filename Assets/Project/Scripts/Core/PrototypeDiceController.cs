using UnityEngine;

public class PrototypeDiceController : MonoBehaviour
{
    [SerializeField] private PlayerPawnMover pawn;
    [SerializeField] private int lastRoll;

    public int LastRoll => lastRoll;

    [ContextMenu("Roll Dice And Move Pawn")]
    public void RollDiceAndMovePawn()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Enter Play Mode before rolling the dice.",
                this);

            return;
        }

        if (pawn == null)
        {
            pawn = FindAnyObjectByType<PlayerPawnMover>();
        }

        if (pawn == null)
        {
            Debug.LogError("No PlayerPawnMover was found.", this);
            return;
        }

        if (pawn.IsMoving)
        {
            Debug.LogWarning("The pawn is already moving.", this);
            return;
        }

        lastRoll = Random.Range(1, 7);

        Debug.Log($"Dice result: {lastRoll}", this);

        pawn.MoveBy(lastRoll);
    }
}