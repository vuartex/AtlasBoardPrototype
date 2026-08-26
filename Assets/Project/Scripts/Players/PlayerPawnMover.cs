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

    [Header("Safe Pawn Formation")]
    [SerializeField]
    private bool useSafeFormation = true;

    [Tooltip(
        "Horizontal separation of the 2x2 pawn formation, expressed as " +
        "a fraction of the tile half-width.")]
    [SerializeField, Range(0.05f, 0.80f)]
    private float formationSideRatio = 0.36f;

    [Tooltip(
        "Inward offset for player slots 1-2, expressed as a fraction " +
        "of the tile half-depth.")]
    [SerializeField, Range(0.05f, 0.90f)]
    private float formationNearInwardRatio = 0.28f;

    [Tooltip(
        "Inward offset for player slots 3-4, expressed as a fraction " +
        "of the tile half-depth.")]
    [SerializeField, Range(0.10f, 0.95f)]
    private float formationFarInwardRatio = 0.72f;

    [Tooltip(
        "Approximate XZ footprint radius used to keep pawns inside the " +
        "tile and away from development visuals.")]
    [SerializeField, Min(0.05f)]
    private float pawnFootprintRadius = 0.22f;

    [Tooltip(
        "Extra visual clearance around DevelopmentMarkers. This is a " +
        "logical board-game clearance check; development colliders remain disabled.")]
    [SerializeField, Min(0f)]
    private float developmentVisualClearance = 0.12f;

    private PlayerGameState playerState;
    private PawnMotionAnimator pawnMotionAnimator;
    private PawnCosmeticApplier pawnCosmeticApplier;
    private Renderer[] pawnRenderers;
    private Collider[] pawnColliders;

    private bool isMoving;
    private bool isPawnVisible = true;

    public int CurrentTileIndex =>
        currentTileIndex;

    public bool IsMoving =>
        isMoving;

    public bool IsPawnVisible =>
        isPawnVisible;

    public event Action<PlayerPawnMover>
        MovementCompleted;

    public event Action<PlayerPawnMover>
        PassedStart;

    private void Awake()
    {
        playerState =
            GetComponent<PlayerGameState>();

        pawnMotionAnimator =
            GetComponent<PawnMotionAnimator>();

        pawnCosmeticApplier =
            GetComponent<PawnCosmeticApplier>();

        pawnRenderers =
            GetComponentsInChildren<Renderer>(
                true);

        pawnColliders =
            GetComponentsInChildren<Collider>(
                true);

        if (playerState != null)
        {
            playerState.BankruptcyChanged +=
                HandleBankruptcyChanged;
        }
    }

    private void Start()
    {
        EnsureBoardPath();
        EnsureMotionAnimator();

        bool shouldBeVisible =
            playerState == null ||
            !playerState.IsBankrupt;

        SetPawnVisible(
            shouldBeVisible);

        if (shouldBeVisible)
        {
            SnapToCurrentTile();

            pawnMotionAnimator
                ?.SetLandedPose();
        }
    }

    private void OnDestroy()
    {
        if (playerState != null)
        {
            playerState.BankruptcyChanged -=
                HandleBankruptcyChanged;
        }
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
        if (isMoving ||
            steps <= 0 ||
            IsBankrupt())
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
                        completedPawn),
                false));

        return true;
    }

    public bool MoveForwardToTile(
        int targetTileIndex,
        Action<PlayerPawnMover> onCompleted)
    {
        if (isMoving ||
            IsBankrupt())
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
                onCompleted,
                true));

        return true;
    }

    [ContextMenu("Snap To Current Tile")]
    public void SnapToCurrentTile()
    {
        if (IsBankrupt())
        {
            return;
        }

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

    public void RefreshPawnVisibilityCache()
    {
        pawnRenderers =
            GetComponentsInChildren<Renderer>(
                true);

        pawnColliders =
            GetComponentsInChildren<Collider>(
                true);

        ApplyPawnVisibility();
    }

    public void SetPawnVisible(
        bool visible)
    {
        isPawnVisible = visible;

        ApplyPawnVisibility();
    }

    private void ApplyPawnVisibility()
    {
        if (pawnRenderers != null)
        {
            foreach (Renderer pawnRenderer
                     in pawnRenderers)
            {
                if (pawnRenderer != null)
                {
                    pawnRenderer.enabled =
                        isPawnVisible;
                }
            }
        }

        if (pawnColliders != null)
        {
            foreach (Collider pawnCollider
                     in pawnColliders)
            {
                if (pawnCollider != null)
                {
                    pawnCollider.enabled =
                        isPawnVisible;
                }
            }
        }

        if (pawnCosmeticApplier == null)
        {
            pawnCosmeticApplier =
                GetComponent<
                    PawnCosmeticApplier>();
        }

        pawnCosmeticApplier
            ?.EnforceVisibility(
                isPawnVisible);
    }

    private IEnumerator MoveStepsRoutine(
        int steps,
        Action<PlayerPawnMover> onCompleted,
        bool useSprintAnimation)
    {
        isMoving = true;

        EnsureMotionAnimator();

        pawnMotionAnimator
            ?.BeginMovement(
                useSprintAnimation);

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

            Vector3 startPosition =
                transform.position;

            Vector3 targetPosition =
                GetPawnPosition(targetTile);

            Vector3 moveDirection =
                targetPosition -
                startPosition;

            moveDirection.y = 0f;

            if (moveDirection.sqrMagnitude >
                0.0001f)
            {
                pawnMotionAnimator
                    ?.SetFacingDirection(
                        moveDirection);
            }

            float distance =
                Vector3.Distance(
                    startPosition,
                    targetPosition);

            float duration =
                distance > 0.0001f
                    ? distance /
                      Mathf.Max(
                          0.1f,
                          moveSpeed)
                    : 0f;

            if (duration > 0.0001f)
            {
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed +=
                        Time.deltaTime;

                    float normalized =
                        Mathf.Clamp01(
                            elapsed /
                            duration);

                    // SmoothStep preserves the exact tile-to-tile
                    // endpoints while removing the rigid sliding feel.
                    float eased =
                        normalized *
                        normalized *
                        (3f -
                         2f *
                         normalized);

                    transform.position =
                        Vector3.Lerp(
                            startPosition,
                            targetPosition,
                            eased);

                    yield return null;
                }
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

        pawnMotionAnimator
            ?.EndMovement();

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

    private void HandleBankruptcyChanged(
        PlayerGameState bankruptPlayer)
    {
        if (bankruptPlayer == null ||
            bankruptPlayer != playerState)
        {
            return;
        }

        pawnMotionAnimator
            ?.StopMotion();

        SetPawnVisible(false);

        Debug.Log(
            $"{bankruptPlayer.DisplayName}'s pawn " +
            "was hidden after bankruptcy.",
            this);
    }

    private bool IsBankrupt()
    {
        return playerState != null &&
               playerState.IsBankrupt;
    }

    private Vector3 GetPawnPosition(
        BoardTile tile)
    {
        if (tile == null)
        {
            return transform.position;
        }

        Vector3 position =
            tile.transform.position
            + Vector3.up *
              pawnHeightOffset;

        if (!useSafeFormation)
        {
            return position +
                   boardSlotOffset;
        }

        Vector3 outwardDirection =
            GetTileOutwardDirection(
                tile.TileIndex);

        Vector3 inwardDirection =
            -outwardDirection;

        Vector3 rowDirection =
            GetTileRowDirection(
                tile.TileIndex);

        Renderer tileRenderer =
            tile.GetComponent<Renderer>();

        float rowHalfExtent = 0.9f;
        float depthHalfExtent = 0.9f;

        Bounds tileBounds =
            new Bounds(
                tile.transform.position,
                new Vector3(
                    1.8f,
                    0.2f,
                    1.8f));

        if (tileRenderer != null)
        {
            tileBounds =
                tileRenderer.bounds;

            rowHalfExtent =
                GetBoundsExtentAlong(
                    tileBounds,
                    rowDirection);

            depthHalfExtent =
                GetBoundsExtentAlong(
                    tileBounds,
                    outwardDirection);
        }

        int slotIndex =
            playerState != null
                ? Mathf.Clamp(
                    playerState
                        .PlayerSlotIndex,
                    0,
                    3)
                : 0;

        float sideSign =
            slotIndex % 2 == 0
                ? -1f
                : 1f;

        bool useFarRow =
            slotIndex >= 2;

        // Kenney Mini Characters have a wider visible footprint than the
        // original prototype pawns. v1.0.2 used a compact 2x2 formation that
        // was mathematically separate but still visually overlapped. Enforce
        // a wider minimum side separation regardless of older serialized
        // inspector values left behind by the previous patch.
        float safeSideRatio =
            Mathf.Max(
                formationSideRatio,
                0.60f);

        float rowOffset =
            rowHalfExtent *
            safeSideRatio *
            sideSign;

        if (tile.TileType == TileType.City)
        {
            // City development is placed on the OUTER edge of the tile. Keep
            // both character rows on the center/inner side, but separate the
            // rows enough for full character meshes instead of tiny pawns.
            float nearInwardRatio =
                Mathf.Min(
                    formationNearInwardRatio,
                    0.08f);

            float farInwardRatio =
                Mathf.Max(
                    formationFarInwardRatio,
                    0.72f);

            float inwardRatio =
                useFarRow
                    ? farInwardRatio
                    : nearInwardRatio;

            position +=
                inwardDirection *
                (depthHalfExtent *
                 inwardRatio);
        }
        else
        {
            // Start and special tiles do not reserve their outer edge for
            // development. Center the four pawns as a true 2x2 group so all
            // four are clearly visible at match start.
            const float openTileDepthRatio =
                0.36f;

            float depthSign =
                useFarRow
                    ? 1f
                    : -1f;

            position +=
                inwardDirection *
                (depthHalfExtent *
                 openTileDepthRatio *
                 depthSign);
        }

        position +=
            rowDirection *
            rowOffset;

        // Safe formation is now authoritative. The old boardSlotOffset values
        // belonged to the prototype pawn meshes and can pull the larger
        // character models back toward each other, so they are intentionally
        // not applied while safe formation is enabled.

        position =
            ClampPawnToTile(
                position,
                tileBounds);

        position =
            AvoidDevelopmentVisuals(
                tile,
                position,
                inwardDirection,
                tileBounds);

        return position;
    }

    private Vector3 ClampPawnToTile(
        Vector3 position,
        Bounds tileBounds)
    {
        float margin =
            Mathf.Max(
                0.05f,
                pawnFootprintRadius);

        float minX =
            tileBounds.min.x +
            margin;

        float maxX =
            tileBounds.max.x -
            margin;

        float minZ =
            tileBounds.min.z +
            margin;

        float maxZ =
            tileBounds.max.z -
            margin;

        if (minX <= maxX)
        {
            position.x =
                Mathf.Clamp(
                    position.x,
                    minX,
                    maxX);
        }

        if (minZ <= maxZ)
        {
            position.z =
                Mathf.Clamp(
                    position.z,
                    minZ,
                    maxZ);
        }

        return position;
    }

    private Vector3 AvoidDevelopmentVisuals(
        BoardTile tile,
        Vector3 position,
        Vector3 inwardDirection,
        Bounds tileBounds)
    {
        Transform developmentRoot =
            tile.transform.Find(
                "DevelopmentMarkers");

        if (developmentRoot == null)
        {
            return position;
        }

        Renderer[] developmentRenderers =
            developmentRoot
                .GetComponentsInChildren<
                    Renderer>(
                        true);

        if (developmentRenderers == null ||
            developmentRenderers.Length == 0)
        {
            return position;
        }

        bool foundBounds = false;
        Bounds developmentBounds =
            new Bounds(
                developmentRoot.position,
                Vector3.zero);

        foreach (Renderer renderer
                 in developmentRenderers)
        {
            if (renderer == null ||
                !renderer.enabled)
            {
                continue;
            }

            if (!foundBounds)
            {
                developmentBounds =
                    renderer.bounds;

                foundBounds = true;
            }
            else
            {
                developmentBounds
                    .Encapsulate(
                        renderer.bounds);
            }
        }

        if (!foundBounds)
        {
            return position;
        }

        float clearance =
            pawnFootprintRadius +
            developmentVisualClearance;

        // Development visuals deliberately have their physics colliders
        // disabled. Use renderer bounds as a deterministic landing-clearance
        // check instead of turning physics back on and risking board regressions.
        for (int attempt = 0;
             attempt < 10 &&
             IsInsideExpandedXZ(
                 position,
                 developmentBounds,
                 clearance);
             attempt++)
        {
            position +=
                inwardDirection *
                0.08f;

            position =
                ClampPawnToTile(
                    position,
                    tileBounds);
        }

        return position;
    }

    private static bool IsInsideExpandedXZ(
        Vector3 point,
        Bounds bounds,
        float expansion)
    {
        return
            point.x >=
                bounds.min.x -
                expansion &&
            point.x <=
                bounds.max.x +
                expansion &&
            point.z >=
                bounds.min.z -
                expansion &&
            point.z <=
                bounds.max.z +
                expansion;
    }

    private static float GetBoundsExtentAlong(
        Bounds bounds,
        Vector3 axis)
    {
        Vector3 absAxis =
            new Vector3(
                Mathf.Abs(axis.x),
                Mathf.Abs(axis.y),
                Mathf.Abs(axis.z));

        return
            bounds.extents.x *
                absAxis.x +
            bounds.extents.y *
                absAxis.y +
            bounds.extents.z *
                absAxis.z;
    }

    private static Vector3 GetTileOutwardDirection(
        int tileIndex)
    {
        if (tileIndex <= 8)
        {
            return Vector3.back;
        }

        if (tileIndex <= 16)
        {
            return Vector3.right;
        }

        if (tileIndex <= 24)
        {
            return Vector3.forward;
        }

        return Vector3.left;
    }

    private static Vector3 GetTileRowDirection(
        int tileIndex)
    {
        if (tileIndex <= 8 ||
            (tileIndex >= 16 &&
             tileIndex <= 24))
        {
            return Vector3.right;
        }

        return Vector3.forward;
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
                FindAnyObjectByType<BoardPath>();
        }
    }

    private void EnsureMotionAnimator()
    {
        if (pawnMotionAnimator == null)
        {
            pawnMotionAnimator =
                GetComponent<PawnMotionAnimator>();
        }
    }
}
