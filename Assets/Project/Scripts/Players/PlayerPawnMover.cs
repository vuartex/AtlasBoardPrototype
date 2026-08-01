using System;
using System.Collections;
using UnityEngine;

public class PlayerPawnMover : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private BoardPath boardPath;

    [Header("Current State")]
    [SerializeField, Min(0)]
    private int currentTileIndex;

    [Header("Movement")]
    [SerializeField, Min(0.1f)]
    private float moveSpeed = 5f;

    [SerializeField, Min(0f)]
    private float pawnHeightOffset = 0.7f;

    [SerializeField, Min(0f)]
    private float delayBetweenTiles = 0.08f;

    [Header("Board Position Offset")]
    [SerializeField]
    private Vector3 boardSlotOffset;

    private bool isMoving;

    public int CurrentTileIndex =>
        currentTileIndex;

    public bool IsMoving =>
        isMoving;

    public event Action<PlayerPawnMover>
        MovementCompleted;

    public event Action<PlayerPawnMover>
        PassedStart;

    private void Start()
    {
        EnsureBoardPath();
        SnapToCurrentTile();
    }

    public BoardTile GetCurrentTile()
    {
        EnsureBoardPath();

        if (boardPath == null)
        {
            return null;
        }

        return boardPath.GetTile(
            currentTileIndex);
    }

    public bool MoveBy(int steps)
    {
        if (isMoving || steps <= 0)
        {
            return false;
        }

        EnsureBoardPath();

        if (boardPath == null ||
            boardPath.TileCount == 0)
        {
            Debug.LogError(
                "Pawn cannot move because " +
                "BoardPath is unavailable.",
                this);

            return false;
        }

        StartCoroutine(
            MoveStepsRoutine(
                steps,
                completedPawn =>
                    MovementCompleted?.Invoke(
                        completedPawn)));

        return true;
    }

    public bool MoveForwardToTile(
        int targetTileIndex,
        Action<PlayerPawnMover> onCompleted)
    {
        if (isMoving)
        {
            return false;
        }

        EnsureBoardPath();

        if (boardPath == null ||
            boardPath.TileCount == 0)
        {
            Debug.LogError(
                "Pawn cannot perform special movement " +
                "because BoardPath is unavailable.",
                this);

            return false;
        }

        int wrappedTargetIndex =
            WrapTileIndex(targetTileIndex);

        int steps =
            (wrappedTargetIndex -
             currentTileIndex +
             boardPath.TileCount) %
            boardPath.TileCount;

        if (steps == 0)
        {
            onCompleted?.Invoke(this);
            return true;
        }

        Debug.Log(
            $"{gameObject.name} begins special movement " +
            $"from tile {currentTileIndex} " +
            $"to tile {wrappedTargetIndex}.",
            this);

        StartCoroutine(
            MoveStepsRoutine(
                steps,
                onCompleted));

        return true;
    }

    [ContextMenu("Snap To Current Tile")]
    public void SnapToCurrentTile()
    {
        EnsureBoardPath();

        if (boardPath == null)
        {
            return;
        }

        BoardTile tile =
            boardPath.GetTile(
                currentTileIndex);

        if (tile == null)
        {
            return;
        }

        transform.position =
            GetPawnPosition(tile);
    }

    private IEnumerator MoveStepsRoutine(
        int steps,
        Action<PlayerPawnMover> onCompleted)
    {
        isMoving = true;

        for (int step = 0;
             step < steps;
             step++)
        {
            int nextTileIndex =
                (currentTileIndex + 1) %
                boardPath.TileCount;

            bool passedStartThisStep =
                nextTileIndex == 0;

            currentTileIndex =
                nextTileIndex;

            BoardTile targetTile =
                boardPath.GetTile(
                    currentTileIndex);

            if (targetTile == null)
            {
                Debug.LogError(
                    $"Tile {currentTileIndex} " +
                    "could not be found.",
                    this);

                break;
            }

            Vector3 targetPosition =
                GetPawnPosition(targetTile);

            while (
                Vector3.Distance(
                    transform.position,
                    targetPosition) > 0.01f)
            {
                transform.position =
                    Vector3.MoveTowards(
                        transform.position,
                        targetPosition,
                        moveSpeed *
                        Time.deltaTime);

                yield return null;
            }

            transform.position =
                targetPosition;

            if (passedStartThisStep)
            {
                PassedStart?.Invoke(this);
            }

            if (delayBetweenTiles > 0f)
            {
                yield return
                    new WaitForSeconds(
                        delayBetweenTiles);
            }
        }

        isMoving = false;

        BoardTile landedTile =
            GetCurrentTile();

        if (landedTile != null)
        {
            Debug.Log(
                $"{gameObject.name} landed on tile " +
                $"{currentTileIndex}: " +
                $"{landedTile.DisplayName}",
                this);
        }

        onCompleted?.Invoke(this);
    }

    private Vector3 GetPawnPosition(
        BoardTile tile)
    {
        return tile.transform.position
               + Vector3.up *
               pawnHeightOffset
               + boardSlotOffset;
    }

    private int WrapTileIndex(
        int tileIndex)
    {
        return
            ((tileIndex %
              boardPath.TileCount) +
             boardPath.TileCount) %
            boardPath.TileCount;
    }

    private void EnsureBoardPath()
    {
        if (boardPath == null)
        {
            boardPath =
                FindFirstObjectByType<
                    BoardPath>();
        }
    }
}