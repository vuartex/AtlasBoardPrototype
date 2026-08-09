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
