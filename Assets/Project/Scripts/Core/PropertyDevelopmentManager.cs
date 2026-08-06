using System.Collections.Generic;
using UnityEngine;

public class PropertyDevelopmentManager : MonoBehaviour
{
    [Header("Board")]
    [SerializeField]
    private BoardPath boardPath;

    [Header("Prototype Development Rules")]
    [SerializeField, Range(1, 4)]
    private int maximumDevelopmentLevel = 4;

    [SerializeField, Min(0.1f)]
    private float developmentCostRatio = 0.5f;

    [SerializeField, Min(1)]
    private int minimumDevelopmentCost = 50;

    [SerializeField]
    private int[] rentMultipliers = { 1, 2, 3, 5, 8 };

    [Header("Prototype Building Visuals")]
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
               player.CurrentMoney >=
               GetDevelopmentCost(tile);
    }

    public bool TryDevelop(
        PlayerGameState player,
        BoardTile tile)
    {
        if (!CanAffordDevelopment(
                player,
                tile))
        {
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

        return tile.TileIndex / 4;
    }

    public string GetGroupName(
        BoardTile tile)
    {
        int groupIndex =
            GetGroupIndex(tile);

        return groupIndex >= 0
            ? $"Bölge {groupIndex + 1}"
            : "Bilinmeyen Bölge";
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

        Debug.Log(
            $"{GetDebugOwnerName()} now owns all " +
            $"cities in Bölge {debugGroupIndex + 1}.",
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
            "All prototype development levels were reset.",
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
        if (rentMultipliers == null ||
            rentMultipliers.Length == 0)
        {
            return Mathf.Max(1, level + 1);
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
                rootObject.transform,
                false);

            marker.transform.localScale =
                markerScale;

            marker.transform.localPosition =
                new Vector3(
                    -totalWidth * 0.5f +
                    markerIndex * markerSpacing,
                    markerHeight,
                    0f);

            Collider markerCollider =
                marker.GetComponent<Collider>();

            if (markerCollider != null)
            {
                markerCollider.enabled = false;

                if (Application.isPlaying)
                {
                    Destroy(markerCollider);
                }
                else
                {
                    DestroyImmediate(
                        markerCollider);
                }
            }

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
}
