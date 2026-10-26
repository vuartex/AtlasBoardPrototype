using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BoardMap_New",
    menuName = "Atlas Board/Maps/Board Map Definition")]
public class BoardMapDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string mapId = "map_new";

    [SerializeField]
    private string displayName = "New Map";

    [Header("Presentation")]
    [SerializeField]
    private string propertyNoun = "Konum";

    [SerializeField]
    private string groupNoun = "Bölge";

    [Header("Economy")]
    [SerializeField]
    private BoardEconomyProfile economyProfile;

    [Header("Board")]
    [SerializeField]
    private BoardTileDefinition[] tiles =
        new BoardTileDefinition[32];

    public string MapId => mapId;
    public string DisplayName => displayName;
    public string PropertyNoun => propertyNoun;
    public string GroupNoun => groupNoun;
    public BoardEconomyProfile EconomyProfile =>
        economyProfile;
    public IReadOnlyList<BoardTileDefinition> Tiles =>
        tiles;

    public BoardTileDefinition GetTileDefinition(
        int tileIndex)
    {
        if (tiles == null)
        {
            return null;
        }

        for (int index = 0;
             index < tiles.Length;
             index++)
        {
            BoardTileDefinition definition =
                tiles[index];

            if (definition != null &&
                definition.TileIndex == tileIndex)
            {
                return definition;
            }
        }

        return null;
    }

    public IEnumerable<BoardTileDefinition>
        GetPropertyDefinitions()
    {
        if (tiles == null)
        {
            return Enumerable
                .Empty<BoardTileDefinition>();
        }

        return tiles.Where(
            definition =>
                definition != null &&
                definition.TileType ==
                    TileType.City);
    }

    public IEnumerable<BoardTileDefinition>
        GetGroupDefinitions(
            string groupId)
    {
        if (string.IsNullOrWhiteSpace(
                groupId))
        {
            return Enumerable
                .Empty<BoardTileDefinition>();
        }

        return GetPropertyDefinitions()
            .Where(
                definition =>
                    string.Equals(
                        definition.GroupId,
                        groupId,
                        StringComparison.Ordinal));
    }

#if UNITY_EDITOR
    public void EditorSetIdentity(
        string newMapId,
        string newDisplayName,
        string newPropertyNoun,
        string newGroupNoun)
    {
        mapId = newMapId;
        displayName = newDisplayName;
        propertyNoun = newPropertyNoun;
        groupNoun = newGroupNoun;
    }

    public void EditorSetEconomyProfile(
        BoardEconomyProfile profile)
    {
        economyProfile = profile;
    }

    public void EditorSetTiles(
        BoardTileDefinition[] definitions)
    {
        tiles = definitions;
    }
#endif

    // Phase 18B: authored gameplay balance baselines for all 24 cities on
    // each existing map. The array index matches the stable property_01..24
    // already defined in the map assets (which also contain the prices).
    // These are illustrative local economy scores, NOT official statistics.
    // Intentionally fixed per location/price identity: no session reroll.
    // Sources used for qualitative ranking: TÜİK provincial GDP (2024),
    // US BEA GDP by state and Colorado population/economic prominence.
    private static readonly int[,] Pulse_turkey = new int[,]
    {
        { 38, 42, 55, 76 },
        { 46, 48, 64, 73 },
        { 43, 51, 68, 74 },
        { 53, 58, 70, 71 },
        { 63, 67, 78, 70 },
        { 77, 82, 89, 72 },
        { 56, 62, 66, 73 },
        { 51, 58, 64, 74 },
        { 85, 84, 86, 68 },
        { 49, 53, 60, 73 },
        { 65, 67, 70, 70 },
        { 79, 81, 86, 69 },
        { 57, 61, 71, 76 },
        { 60, 62, 70, 74 },
        { 85, 88, 85, 75 },
        { 57, 60, 73, 67 },
        { 42, 47, 60, 76 },
        { 59, 63, 70, 70 },
        { 66, 72, 83, 65 },
        { 72, 78, 88, 66 },
        { 71, 74, 80, 67 },
        { 50, 55, 61, 66 },
        { 47, 50, 65, 70 },
        { 39, 45, 52, 69 },
    };

    private static readonly int[,] Pulse_colorado = new int[,]
    {
        { 38, 43, 51, 80 },
        { 43, 50, 62, 77 },
        { 65, 67, 77, 74 },
        { 60, 63, 71, 81 },
        { 68, 73, 79, 78 },
        { 72, 73, 75, 83 },
        { 73, 79, 76, 75 },
        { 67, 72, 82, 72 },
        { 58, 67, 74, 79 },
        { 78, 80, 86, 73 },
        { 89, 88, 89, 71 },
        { 81, 80, 85, 77 },
        { 59, 60, 69, 67 },
        { 44, 49, 55, 72 },
        { 37, 43, 49, 68 },
        { 57, 62, 66, 73 },
        { 48, 54, 59, 73 },
        { 60, 66, 65, 75 },
        { 63, 73, 64, 79 },
        { 56, 68, 62, 79 },
        { 69, 82, 67, 76 },
        { 74, 85, 77, 78 },
        { 83, 88, 74, 79 },
        { 61, 71, 70, 82 },
    };

    private static readonly int[,] Pulse_usa = new int[,]
    {
        { 52, 60, 55, 81 },
        { 42, 54, 48, 84 },
        { 55, 62, 57, 82 },
        { 74, 84, 83, 81 },
        { 78, 81, 88, 72 },
        { 70, 73, 80, 73 },
        { 72, 75, 81, 70 },
        { 82, 80, 87, 69 },
        { 88, 83, 87, 70 },
        { 71, 74, 81, 71 },
        { 73, 73, 82, 69 },
        { 84, 84, 89, 69 },
        { 48, 53, 68, 71 },
        { 55, 57, 75, 66 },
        { 89, 88, 90, 70 },
        { 70, 75, 83, 65 },
        { 65, 67, 79, 72 },
        { 72, 76, 77, 76 },
        { 69, 76, 78, 78 },
        { 83, 86, 87, 72 },
        { 70, 72, 85, 65 },
        { 48, 52, 50, 63 },
        { 73, 78, 76, 73 },
        { 90, 88, 90, 70 },
    };

    public int GetPulseBaseline(string propertyId, int category)
    {
        if (category < 0 || category >= 4 || string.IsNullOrWhiteSpace(propertyId) ||
            !propertyId.StartsWith("property_", StringComparison.Ordinal) ||
            !int.TryParse(propertyId.Substring(9), out int number) ||
            number < 1 || number > 24)
            return 50;

        string id = (MapId ?? string.Empty).ToLowerInvariant();
        int[,] profile = id == "map_turkey" ? Pulse_turkey :
                         id == "map_colorado" ? Pulse_colorado :
                         id == "map_usa" ? Pulse_usa : null;
        return profile != null ? profile[number - 1, category] : 50;
    }

    private void OnValidate()
    {
        if (tiles == null)
        {
            return;
        }

        HashSet<int> usedIndexes =
            new HashSet<int>();

        foreach (BoardTileDefinition definition
                 in tiles)
        {
            if (definition == null)
            {
                continue;
            }

            if (!usedIndexes.Add(
                    definition.TileIndex))
            {
                Debug.LogWarning(
                    $"{name}: duplicate tile index " +
                    $"{definition.TileIndex}.",
                    this);
            }
        }
    }
}
