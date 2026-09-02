using System.Collections.Generic;
using UnityEngine;

public class PropertyDevelopmentManager : MonoBehaviour
{
    [Header("Board")]
    [SerializeField]
    private BoardPath boardPath;

    [SerializeField]
    private BoardEconomyProfile economyProfile;

    [Header("Prototype Development Rules")]
    [SerializeField, Range(1, 4)]
    private int maximumDevelopmentLevel = 4;

    [SerializeField, Min(0.1f)]
    private float developmentCostRatio = 0.5f;

    [SerializeField, Min(1)]
    private int minimumDevelopmentCost = 50;

    [SerializeField]
    private int[] rentMultipliers = { 1, 2, 3, 5, 8 };

    [Header("Group Development Rule")]
    [Tooltip(
        "When enabled, a property may only be developed if it " +
        "is currently at the minimum development level in its " +
        "completed group. This keeps group development balanced.")]
    [SerializeField]
    private bool requireBalancedGroupDevelopment = true;

    [Header("Development Visuals")]
    [Tooltip(
        "Optional prefabs for development levels 1 through 4. " +
        "The selected level prefab is repeated side-by-side " +
        "1/2/3/4 times in a Monopoly-style row.")]
    [SerializeField]
    private GameObject[] developmentLevelPrefabs =
        new GameObject[4];

    [Tooltip(
        "Extra uniform/manual scale applied before automatic " +
        "fit-to-tile scaling. Keep X/Y/Z equal.")]
    [SerializeField]
    private Vector3 developmentPrefabLocalScale =
        Vector3.one;

    [Header("Monopoly-Style Placement")]
    [Tooltip(
        "Places development visuals on the OUTER edge of each " +
        "property tile instead of the tile center.")]
    [SerializeField]
    private bool placeOnOuterTileEdge = true;

    [Tooltip(
        "Keeps development visuals away from the outer title/header strip by " +
        "placing them on the inner board-facing edge. Recommended for Atlas Board.")]
    [SerializeField]
    private bool avoidOuterTitleHeader = true;

    [Tooltip(
        "Distance from the outside edge of the tile in world units.")]
    [SerializeField, Min(0f)]
    private float outerEdgeInset = 0.08f;

    [Tooltip(
        "Small vertical gap above the tile surface.")]
    [SerializeField, Min(0f)]
    private float tileSurfaceGap = 0.015f;

    [Tooltip(
        "Gap between side-by-side buildings in world units.")]
    [SerializeField, Min(0f)]
    private float buildingGap = 0.05f;

    [Tooltip(
        "Maximum total row width as a fraction of the tile width.")]
    [SerializeField, Range(0.25f, 0.95f)]
    private float maximumRowWidthRatio = 0.82f;

    [Tooltip(
        "Maximum building height as a fraction of the tile world width. " +
        "Keeps imported city assets small enough for a board game.")]
    [SerializeField, Range(0.1f, 1.5f)]
    private float maximumBuildingHeightRatio = 0.38f;

    [Tooltip(
        "Uses clean board-side rotations (0/90/180/270 degrees) " +
        "instead of pointing each building diagonally toward center.")]
    [SerializeField]
    private bool alignToBoardSide = true;

    [Tooltip(
        "Use this only if the imported prefab faces the wrong way. " +
        "Typical values: 0, 90, 180, -90.")]
    [SerializeField]
    private float boardSideYawOffset;

    [Tooltip(
        "Normally leave this OFF so building prefabs keep their " +
        "original materials. The property tile itself already " +
        "shows the player's ownership color.")]
    [SerializeField]
    private bool applyOwnerMaterialToDevelopmentVisual;

    [Header("Fallback Marker Visuals")]
    [SerializeField]
    private Vector3 markerScale =
        new Vector3(0.16f, 0.22f, 0.16f);

    [SerializeField]
    private float markerHeight = 0.48f;

    [SerializeField]
    private float markerSpacing = 0.19f;

    [Header("Debug")]
    [SerializeField]
    private PlayerGameState debugOwner;

    [SerializeField, Range(0, 7)]
    private int debugGroupIndex;

    private readonly Dictionary<BoardTile, int>
        developmentLevels =
            new Dictionary<BoardTile, int>();

    private readonly Dictionary<BoardTile, Transform>
        markerRoots =
            new Dictionary<BoardTile, Transform>();

    private bool initialized;

    public int MaximumDevelopmentLevel =>
        maximumDevelopmentLevel;

    public bool RequireBalancedGroupDevelopment =>
        requireBalancedGroupDevelopment;

    public void SetRequireBalancedGroupDevelopment(
        bool required)
    {
        requireBalancedGroupDevelopment =
            required;

        Debug.Log(
            $"Balanced group development rule: " +
            $"{(required ? "ON" : "OFF")}.",
            this);
    }

    private void Start()
    {
        EnsureInitialized();
    }

    public bool IsEligibleForDevelopment(
        PlayerGameState player,
        BoardTile tile)
    {
        if (player == null ||
            tile == null ||
            player.IsBankrupt ||
            tile.TileType != TileType.City ||
            !tile.IsOwned ||
            tile.OwnerPlayerIndex !=
            player.PlayerSlotIndex)
        {
            return false;
        }

        EnsureInitialized();

        if (!HasCompleteGroup(
                player.PlayerSlotIndex,
                GetGroupIndex(tile)))
        {
            return false;
        }

        return GetDevelopmentLevel(tile) <
               maximumDevelopmentLevel;
    }

    public bool CanAffordDevelopment(
        PlayerGameState player,
        BoardTile tile)
    {
        return IsEligibleForDevelopment(
                   player,
                   tile) &&
               CanDevelopEvenly(
                   player,
                   tile) &&
               player.CurrentMoney >=
               GetDevelopmentCost(tile);
    }

    public bool CanDevelopEvenly(
        PlayerGameState player,
        BoardTile tile)
    {
        if (!IsEligibleForDevelopment(
                player,
                tile))
        {
            return false;
        }

        if (!requireBalancedGroupDevelopment)
        {
            return true;
        }

        int minimumLevel =
            GetGroupMinimumDevelopmentLevel(
                GetGroupIndex(tile));

        if (minimumLevel < 0)
        {
            return false;
        }

        return GetDevelopmentLevel(tile) ==
               minimumLevel;
    }

    public int GetGroupMinimumDevelopmentLevel(
        int groupIndex)
    {
        EnsureInitialized();

        if (boardPath == null ||
            groupIndex < 0)
        {
            return -1;
        }

        int minimumLevel =
            int.MaxValue;

        bool foundCity = false;

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile groupTile =
                boardPath.GetTile(tileIndex);

            if (groupTile == null ||
                groupTile.TileType != TileType.City ||
                GetGroupIndex(groupTile) !=
                groupIndex)
            {
                continue;
            }

            foundCity = true;

            minimumLevel =
                Mathf.Min(
                    minimumLevel,
                    GetDevelopmentLevel(
                        groupTile));
        }

        return foundCity
            ? minimumLevel
            : -1;
    }

    public string GetGroupDevelopmentSummary(
        BoardTile tile)
    {
        EnsureInitialized();

        if (tile == null ||
            boardPath == null)
        {
            return "-";
        }

        int groupIndex =
            GetGroupIndex(tile);

        List<string> levels =
            new List<string>();

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile groupTile =
                boardPath.GetTile(tileIndex);

            if (groupTile == null ||
                groupTile.TileType != TileType.City ||
                GetGroupIndex(groupTile) !=
                groupIndex)
            {
                continue;
            }

            levels.Add(
                $"L{GetDevelopmentLevel(groupTile)}");
        }

        return levels.Count > 0
            ? string.Join(" / ", levels)
            : "-";
    }

    public string GetDevelopmentBlockReason(
        PlayerGameState player,
        BoardTile tile)
    {
        if (!IsEligibleForDevelopment(
                player,
                tile))
        {
            return string.Empty;
        }

        if (requireBalancedGroupDevelopment &&
            !CanDevelopEvenly(
                player,
                tile))
        {
            int minimumLevel =
                GetGroupMinimumDevelopmentLevel(
                    GetGroupIndex(tile));

            return
                AtlasBoardL.T(
                    "development.balanced_rule",
                    minimumLevel);
        }

        if (player.CurrentMoney <
            GetDevelopmentCost(tile))
        {
            return
                AtlasBoardL.T(
                    "development.insufficient_balance");
        }

        return string.Empty;
    }

    public bool TryDevelop(
        PlayerGameState player,
        BoardTile tile)
    {
        if (!CanAffordDevelopment(
                player,
                tile))
        {
            string blockReason =
                GetDevelopmentBlockReason(
                    player,
                    tile);

            if (!string.IsNullOrEmpty(
                    blockReason))
            {
                Debug.Log(
                    $"{player?.DisplayName ?? "Player"} could not " +
                    $"develop {tile?.DisplayName ?? "property"}: " +
                    $"{blockReason}",
                    this);
            }

            return false;
        }

        int cost =
            GetDevelopmentCost(tile);

        if (!player.TrySpend(cost))
        {
            return false;
        }

        int newLevel =
            GetDevelopmentLevel(tile) + 1;

        developmentLevels[tile] =
            newLevel;

        RebuildDevelopmentVisual(
            tile,
            player.OwnershipMaterial);

        Debug.Log(
            $"{player.DisplayName} developed " +
            $"{tile.DisplayName} to level " +
            $"{newLevel} for {cost}. " +
            $"New rent: {GetEffectiveRent(tile)}.",
            this);

        return true;
    }

    public int GetDevelopmentLevel(
        BoardTile tile)
    {
        EnsureInitialized();

        if (tile == null ||
            !developmentLevels.TryGetValue(
                tile,
                out int level))
        {
            return 0;
        }

        return level;
    }

    public int GetDevelopmentCost(
        BoardTile tile)
    {
        if (tile == null)
        {
            return minimumDevelopmentCost;
        }

        if (tile.DevelopmentCost > 0)
        {
            return tile.DevelopmentCost;
        }

        // Legacy fallback for prototype/old scene data.
        int rawCost =
            Mathf.Max(
                minimumDevelopmentCost,
                Mathf.RoundToInt(
                    tile.PurchasePrice *
                    developmentCostRatio));

        return Mathf.CeilToInt(
                   rawCost / 10f) * 10;
    }

    public int GetEffectiveRent(
        BoardTile tile)
    {
        if (tile == null)
        {
            return 0;
        }

        int level =
            Mathf.Clamp(
                GetDevelopmentLevel(tile),
                0,
                maximumDevelopmentLevel);

        int multiplier =
            GetRentMultiplier(level);

        return tile.BaseRent * multiplier;
    }

    public int GetProjectedRentAtNextLevel(
        BoardTile tile)
    {
        if (tile == null)
        {
            return 0;
        }

        int nextLevel =
            Mathf.Clamp(
                GetDevelopmentLevel(tile) + 1,
                0,
                maximumDevelopmentLevel);

        return tile.BaseRent *
               GetRentMultiplier(nextLevel);
    }

    public int GetGroupIndex(
        BoardTile tile)
    {
        if (tile == null)
        {
            return -1;
        }

        if (boardPath == null)
        {
            boardPath =
                FindAnyObjectByType<
                    BoardPath>();
        }

        if (boardPath != null &&
            !string.IsNullOrWhiteSpace(
                tile.GroupId))
        {
            List<string> groupIds =
                new List<string>();

            for (int tileIndex = 0;
                 tileIndex <
                 boardPath.TileCount;
                 tileIndex++)
            {
                BoardTile candidate =
                    boardPath.GetTile(
                        tileIndex);

                if (candidate == null ||
                    candidate.TileType !=
                        TileType.City ||
                    string.IsNullOrWhiteSpace(
                        candidate.GroupId))
                {
                    continue;
                }

                if (!groupIds.Contains(
                        candidate.GroupId))
                {
                    groupIds.Add(
                        candidate.GroupId);
                }

                if (candidate == tile ||
                    candidate.TileIndex ==
                        tile.TileIndex)
                {
                    return groupIds.IndexOf(
                        candidate.GroupId);
                }
            }

            int dataGroupIndex =
                groupIds.IndexOf(
                    tile.GroupId);

            if (dataGroupIndex >= 0)
            {
                return dataGroupIndex;
            }
        }

        // Legacy fallback for old prototype boards.
        return tile.TileIndex / 4;
    }

    public string GetGroupName(
        BoardTile tile)
    {
        if (tile == null)
        {
            return AtlasBoardL.T(
                "development.unknown_group");
        }

        if (!string.IsNullOrWhiteSpace(
                tile.GroupDisplayName))
        {
            return tile.GroupDisplayName;
        }

        int groupIndex =
            GetGroupIndex(tile);

        return groupIndex >= 0
            ? AtlasBoardL.T(
                "development.group_number",
                groupIndex + 1)
            : AtlasBoardL.T(
                "development.unknown_group");
    }

    public bool HasCompleteGroup(
        int playerSlotIndex,
        int groupIndex)
    {
        EnsureInitialized();

        if (boardPath == null ||
            groupIndex < 0)
        {
            return false;
        }

        int groupCityCount = 0;

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile == null ||
                tile.TileType != TileType.City ||
                GetGroupIndex(tile) != groupIndex)
            {
                continue;
            }

            groupCityCount++;

            if (!tile.IsOwned ||
                tile.OwnerPlayerIndex !=
                playerSlotIndex)
            {
                return false;
            }
        }

        return groupCityCount >= 2;
    }

    public void ApplyOnlineAuthoritativeDevelopmentLevel(
        BoardTile tile,
        int authoritativeLevel,
        PlayerGameState owner)
    {
        if (tile == null)
        {
            return;
        }

        EnsureInitialized();

        int clampedLevel =
            Mathf.Clamp(
                authoritativeLevel,
                0,
                maximumDevelopmentLevel);

        int currentLevel =
            GetDevelopmentLevel(tile);

        bool hasVisual =
            markerRoots.TryGetValue(
                tile,
                out Transform markerRoot) &&
            markerRoot != null &&
            markerRoot.gameObject.activeSelf &&
            markerRoot.childCount > 0;

        if (currentLevel == clampedLevel &&
            ((clampedLevel == 0 && !hasVisual) ||
             (clampedLevel > 0 && hasVisual)))
        {
            return;
        }

        developmentLevels[tile] =
            clampedLevel;

        if (clampedLevel <= 0)
        {
            RemoveMarkerRoot(tile);
            return;
        }

        Material material =
            owner != null
                ? owner.OwnershipMaterial
                : null;

        RebuildDevelopmentVisual(
            tile,
            material);
    }

    public void RefreshDevelopmentVisual(
        BoardTile tile,
        PlayerGameState owner)
    {
        if (tile == null)
        {
            return;
        }

        Material material =
            owner != null
                ? owner.OwnershipMaterial
                : null;

        RebuildDevelopmentVisual(
            tile,
            material);
    }

    public void ResetDevelopment(
        BoardTile tile)
    {
        if (tile == null)
        {
            return;
        }

        EnsureInitialized();

        developmentLevels[tile] = 0;
        RemoveMarkerRoot(tile);
    }

    public int GetDevelopmentInvestmentValue(
        int playerSlotIndex)
    {
        EnsureInitialized();

        int totalValue = 0;

        foreach (KeyValuePair<BoardTile, int> pair
                 in developmentLevels)
        {
            BoardTile tile = pair.Key;
            int level = pair.Value;

            if (tile == null ||
                !tile.IsOwned ||
                tile.OwnerPlayerIndex !=
                playerSlotIndex)
            {
                continue;
            }

            totalValue +=
                GetDevelopmentCost(tile) *
                level;
        }

        return totalValue;
    }

    public int GetTotalDevelopmentLevels(
        int playerSlotIndex)
    {
        EnsureInitialized();

        int totalLevels = 0;

        foreach (KeyValuePair<BoardTile, int> pair
                 in developmentLevels)
        {
            BoardTile tile = pair.Key;

            if (tile == null ||
                !tile.IsOwned ||
                tile.OwnerPlayerIndex !=
                playerSlotIndex)
            {
                continue;
            }

            totalLevels += pair.Value;
        }

        return totalLevels;
    }

    [ContextMenu("Debug/Assign Selected Group To Debug Owner")]
    private void DebugAssignSelectedGroup()
    {
        EnsureInitialized();

        if (debugOwner == null ||
            boardPath == null)
        {
            Debug.LogWarning(
                "Assign Debug Owner before using " +
                "the group assignment command.",
                this);

            return;
        }

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile == null ||
                tile.TileType != TileType.City ||
                GetGroupIndex(tile) !=
                debugGroupIndex)
            {
                continue;
            }

            tile.ClearOwner();
            ResetDevelopment(tile);

            if (tile.TrySetOwner(
                    debugOwner.PlayerSlotIndex))
            {
                tile.ApplyOwnerMaterial(
                    debugOwner.OwnershipMaterial);
            }
        }

        string debugGroupName =
            $"Bölge {debugGroupIndex + 1}";

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile != null &&
                tile.TileType == TileType.City &&
                GetGroupIndex(tile) ==
                    debugGroupIndex)
            {
                debugGroupName =
                    GetGroupName(tile);

                break;
            }
        }

        Debug.Log(
            $"{GetDebugOwnerName()} now owns all " +
            $"properties in {debugGroupName}.",
            this);
    }

    [ContextMenu("Debug/Reset All Developments")]
    private void DebugResetAllDevelopments()
    {
        EnsureInitialized();

        List<BoardTile> tiles =
            new List<BoardTile>(
                developmentLevels.Keys);

        foreach (BoardTile tile in tiles)
        {
            ResetDevelopment(tile);
        }

        Debug.Log(
            "All development levels were reset.",
            this);
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        if (boardPath == null)
        {
            boardPath =
                FindAnyObjectByType<BoardPath>();
        }

        if (boardPath == null)
        {
            return;
        }

        developmentLevels.Clear();
        markerRoots.Clear();

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile == null ||
                tile.TileType != TileType.City)
            {
                continue;
            }

            developmentLevels[tile] = 0;

            Transform existingRoot =
                tile.transform.Find(
                    "DevelopmentMarkers");

            if (existingRoot != null)
            {
                markerRoots[tile] =
                    existingRoot;

                RemoveMarkerRoot(tile);
            }
        }

        initialized = true;
    }

    private int GetRentMultiplier(
        int level)
    {
        BoardEconomyProfile activeEconomy =
            ResolveEconomyProfile();

        if (activeEconomy != null)
        {
            return activeEconomy
                .GetRentMultiplier(level);
        }

        if (rentMultipliers == null ||
            rentMultipliers.Length == 0)
        {
            return Mathf.Max(
                1,
                level + 1);
        }

        int safeIndex =
            Mathf.Clamp(
                level,
                0,
                rentMultipliers.Length - 1);

        return Mathf.Max(
            1,
            rentMultipliers[safeIndex]);
    }

    private BoardEconomyProfile
        ResolveEconomyProfile()
    {
        if (economyProfile != null)
        {
            return economyProfile;
        }

        BoardGenerator generator =
            FindAnyObjectByType<
                BoardGenerator>();

        if (generator != null &&
            generator.ActiveEconomyProfile != null)
        {
            economyProfile =
                generator.ActiveEconomyProfile;
        }

        return economyProfile;
    }

    private void RebuildDevelopmentVisual(
        BoardTile tile,
        Material ownershipMaterial)
    {
        EnsureInitialized();
        RemoveMarkerRoot(tile);

        int level =
            GetDevelopmentLevel(tile);

        if (level <= 0)
        {
            return;
        }

        GameObject rootObject =
            new GameObject(
                "DevelopmentMarkers");

        rootObject.transform.SetParent(
            tile.transform,
            false);

        markerRoots[tile] =
            rootObject.transform;

        GameObject levelPrefab =
            GetDevelopmentPrefab(level);

        if (levelPrefab != null)
        {
            BuildPrefabDevelopmentVisual(
                tile,
                rootObject.transform,
                levelPrefab,
                level,
                ownershipMaterial);

            return;
        }

        BuildFallbackMarkerVisuals(
            rootObject.transform,
            level,
            ownershipMaterial);
    }

    private GameObject GetDevelopmentPrefab(
        int level)
    {
        if (developmentLevelPrefabs == null ||
            developmentLevelPrefabs.Length == 0)
        {
            return null;
        }

        int prefabIndex =
            level - 1;

        if (prefabIndex < 0 ||
            prefabIndex >=
                developmentLevelPrefabs.Length)
        {
            return null;
        }

        return developmentLevelPrefabs[
            prefabIndex];
    }

    private void BuildPrefabDevelopmentVisual(
        BoardTile tile,
        Transform visualRoot,
        GameObject levelPrefab,
        int level,
        Material ownershipMaterial)
    {
        int buildingCount =
            Mathf.Clamp(
                level,
                1,
                maximumDevelopmentLevel);

        Renderer tileRenderer =
            tile.GetComponent<Renderer>();

        if (tileRenderer == null)
        {
            BuildFallbackMarkerVisuals(
                visualRoot,
                level,
                ownershipMaterial);

            return;
        }

        List<GameObject> instances =
            new List<GameObject>();

        for (int index = 0;
             index < buildingCount;
             index++)
        {
            GameObject instance =
                Instantiate(
                    levelPrefab,
                    visualRoot);

            instance.name =
                $"Development_Level_{level}_{index + 1}";

            DisableDevelopmentColliders(
                instance);

            if (applyOwnerMaterialToDevelopmentVisual &&
                ownershipMaterial != null)
            {
                ApplyMaterialToDevelopmentVisual(
                    instance,
                    ownershipMaterial);
            }

            instances.Add(instance);
        }

        Quaternion worldRotation =
            GetDevelopmentWorldRotation(
                tile);

        foreach (GameObject instance
                 in instances)
        {
            Transform instanceTransform =
                instance.transform;

            Vector3 authoredPrefabScale =
                instanceTransform.localScale;

            Vector3 desiredWorldScale =
                Vector3.Scale(
                    authoredPrefabScale,
                    developmentPrefabLocalScale);

            Vector3 parentLossyScale =
                visualRoot.lossyScale;

            instanceTransform.localScale =
                new Vector3(
                    SafeScaleDivide(
                        desiredWorldScale.x,
                        parentLossyScale.x),
                    SafeScaleDivide(
                        desiredWorldScale.y,
                        parentLossyScale.y),
                    SafeScaleDivide(
                        desiredWorldScale.z,
                        parentLossyScale.z));

            instanceTransform.rotation =
                worldRotation;
        }

        Bounds firstBounds =
            GetCombinedRendererBounds(
                instances[0]);

        if (firstBounds.size.sqrMagnitude <=
            0.000001f)
        {
            BuildFallbackMarkerVisuals(
                visualRoot,
                level,
                ownershipMaterial);

            foreach (GameObject instance
                     in instances)
            {
                Destroy(instance);
            }

            return;
        }

        bool rowRunsAlongWorldX =
            IsRowAlongWorldX(
                tile.TileIndex);

        float tileRowWidth =
            rowRunsAlongWorldX
                ? tileRenderer.bounds.size.x
                : tileRenderer.bounds.size.z;

        float tileDepth =
            rowRunsAlongWorldX
                ? tileRenderer.bounds.size.z
                : tileRenderer.bounds.size.x;

        float buildingRowWidth =
            rowRunsAlongWorldX
                ? firstBounds.size.x
                : firstBounds.size.z;

        float buildingDepth =
            rowRunsAlongWorldX
                ? firstBounds.size.z
                : firstBounds.size.x;

        float maximumRowWidth =
            tileRowWidth *
            maximumRowWidthRatio;

        float currentRowWidth =
            buildingRowWidth *
            buildingCount +
            buildingGap *
            Mathf.Max(
                0,
                buildingCount - 1);

        float maximumBuildingHeight =
            tileRowWidth *
            maximumBuildingHeightRatio;

        float scaleForRow =
            currentRowWidth > 0.0001f
                ? maximumRowWidth /
                  currentRowWidth
                : 1f;

        float scaleForHeight =
            firstBounds.size.y > 0.0001f
                ? maximumBuildingHeight /
                  firstBounds.size.y
                : 1f;

        // Never enlarge imported models automatically. Only shrink
        // them enough to fit the board-game tile.
        float autoScale =
            Mathf.Min(
                1f,
                scaleForRow,
                scaleForHeight);

        foreach (GameObject instance
                 in instances)
        {
            instance.transform.localScale *=
                autoScale;
        }

        // Recalculate bounds after auto-fit scaling.
        firstBounds =
            GetCombinedRendererBounds(
                instances[0]);

        buildingRowWidth =
            rowRunsAlongWorldX
                ? firstBounds.size.x
                : firstBounds.size.z;

        buildingDepth =
            rowRunsAlongWorldX
                ? firstBounds.size.z
                : firstBounds.size.x;

        Vector3 outwardDirection =
            GetTileOutwardDirection(
                tile.TileIndex);

        Vector3 rowDirection =
            GetTileRowDirection(
                tile.TileIndex);

        float outerHalfDepth =
            rowRunsAlongWorldX
                ? tileRenderer.bounds.extents.z
                : tileRenderer.bounds.extents.x;

        float rowStartOffset =
            -0.5f *
            ((buildingCount - 1) *
             (buildingRowWidth +
              buildingGap));

        Vector3 tileCenter =
            tileRenderer.bounds.center;

        Vector3 rowBaseCenter =
            tileCenter;

        float edgeOffset =
            outerHalfDepth -
            outerEdgeInset -
            buildingDepth * 0.5f;

        if (avoidOuterTitleHeader)
        {
            rowBaseCenter -=
                outwardDirection *
                edgeOffset;
        }
        else if (placeOnOuterTileEdge)
        {
            rowBaseCenter +=
                outwardDirection *
                edgeOffset;
        }

        float tileTopY =
            tileRenderer.bounds.max.y;

        for (int index = 0;
             index < instances.Count;
             index++)
        {
            GameObject instance =
                instances[index];

            Bounds bounds =
                GetCombinedRendererBounds(
                    instance);

            float rowOffset =
                rowStartOffset +
                index *
                (buildingRowWidth +
                 buildingGap);

            Vector3 targetPosition =
                rowBaseCenter +
                rowDirection *
                rowOffset;

            // First place horizontally, then lift the model so its
            // renderer bottom sits exactly on the tile surface.
            Vector3 currentPosition =
                instance.transform.position;

            instance.transform.position =
                new Vector3(
                    targetPosition.x,
                    currentPosition.y,
                    targetPosition.z);

            bounds =
                GetCombinedRendererBounds(
                    instance);

            float verticalCorrection =
                tileTopY +
                tileSurfaceGap -
                bounds.min.y;

            instance.transform.position +=
                Vector3.up *
                verticalCorrection;
        }
    }

    private Quaternion GetDevelopmentWorldRotation(
        BoardTile tile)
    {
        if (tile == null)
        {
            return Quaternion.identity;
        }

        if (!alignToBoardSide)
        {
            return Quaternion.Euler(
                0f,
                boardSideYawOffset,
                0f);
        }

        float baseYaw =
            GetBoardSideYaw(
                tile.TileIndex);

        return Quaternion.Euler(
            0f,
            baseYaw +
            boardSideYawOffset,
            0f);
    }

    private float GetBoardSideYaw(
        int tileIndex)
    {
        // Clean cardinal rotations:
        // bottom faces center (+Z),
        // right faces center (-X),
        // top faces center (-Z),
        // left faces center (+X).
        if (tileIndex <= 8)
        {
            return 0f;
        }

        if (tileIndex <= 16)
        {
            return -90f;
        }

        if (tileIndex <= 24)
        {
            return 180f;
        }

        return 90f;
    }

    private bool IsRowAlongWorldX(
        int tileIndex)
    {
        return tileIndex <= 8 ||
               (tileIndex >= 16 &&
                tileIndex <= 24);
    }

    private Vector3 GetTileOutwardDirection(
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

    private Vector3 GetTileRowDirection(
        int tileIndex)
    {
        if (IsRowAlongWorldX(
                tileIndex))
        {
            return Vector3.right;
        }

        return Vector3.forward;
    }

    private Bounds GetCombinedRendererBounds(
        GameObject visualRoot)
    {
        Renderer[] renderers =
            visualRoot.GetComponentsInChildren<
                Renderer>(true);

        if (renderers == null ||
            renderers.Length == 0)
        {
            return new Bounds(
                visualRoot.transform.position,
                Vector3.zero);
        }

        Bounds bounds =
            renderers[0].bounds;

        for (int index = 1;
             index < renderers.Length;
             index++)
        {
            bounds.Encapsulate(
                renderers[index].bounds);
        }

        return bounds;
    }

    private void BuildFallbackMarkerVisuals(
        Transform visualRoot,
        int level,
        Material ownershipMaterial)
    {
        float totalWidth =
            (level - 1) * markerSpacing;

        for (int markerIndex = 0;
             markerIndex < level;
             markerIndex++)
        {
            GameObject marker =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            marker.name =
                $"Development_{markerIndex + 1}";

            marker.transform.SetParent(
                visualRoot,
                false);

            marker.transform.localScale =
                markerScale;

            marker.transform.localPosition =
                new Vector3(
                    -totalWidth * 0.5f +
                    markerIndex * markerSpacing,
                    markerHeight,
                    0f);

            DisableDevelopmentColliders(
                marker);

            Renderer markerRenderer =
                marker.GetComponent<Renderer>();

            if (markerRenderer != null &&
                ownershipMaterial != null)
            {
                markerRenderer.sharedMaterial =
                    ownershipMaterial;
            }
        }
    }

    private float SafeScaleDivide(
        float value,
        float divisor)
    {
        if (Mathf.Abs(divisor) <
            0.0001f)
        {
            return value;
        }

        return value / divisor;
    }

    private void DisableDevelopmentColliders(
        GameObject visualRoot)
    {
        if (visualRoot == null)
        {
            return;
        }

        Collider[] colliders =
            visualRoot.GetComponentsInChildren<
                Collider>(true);

        foreach (Collider visualCollider
                 in colliders)
        {
            if (visualCollider == null)
            {
                continue;
            }

            visualCollider.enabled = false;
        }
    }

    private void ApplyMaterialToDevelopmentVisual(
        GameObject visualRoot,
        Material ownershipMaterial)
    {
        if (visualRoot == null ||
            ownershipMaterial == null)
        {
            return;
        }

        Renderer[] renderers =
            visualRoot.GetComponentsInChildren<
                Renderer>(true);

        foreach (Renderer visualRenderer
                 in renderers)
        {
            if (visualRenderer == null)
            {
                continue;
            }

            Material[] materials =
                visualRenderer.sharedMaterials;

            for (int materialIndex = 0;
                 materialIndex < materials.Length;
                 materialIndex++)
            {
                materials[materialIndex] =
                    ownershipMaterial;
            }

            visualRenderer.sharedMaterials =
                materials;
        }
    }

    private void RemoveMarkerRoot(
        BoardTile tile)
    {
        if (tile == null)
        {
            return;
        }

        Transform markerRoot = null;

        if (markerRoots.TryGetValue(
                tile,
                out Transform storedRoot))
        {
            markerRoot = storedRoot;
        }
        else
        {
            markerRoot =
                tile.transform.Find(
                    "DevelopmentMarkers");
        }

        markerRoots.Remove(tile);

        if (markerRoot == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(markerRoot.gameObject);
        }
        else
        {
            DestroyImmediate(
                markerRoot.gameObject);
        }
    }

    private string GetDebugOwnerName()
    {
        return debugOwner != null
            ? debugOwner.DisplayName
            : "Unknown player";
    }
    public void ResetAllDevelopmentsForNewMatch()
    {
        EnsureInitialized();

        List<BoardTile> tiles =
            new List<BoardTile>(developmentLevels.Keys);

        foreach (BoardTile tile in tiles)
        {
            if (tile == null)
            {
                continue;
            }

            developmentLevels[tile] = 0;
            RemoveMarkerRoot(tile);
        }
    }

}
