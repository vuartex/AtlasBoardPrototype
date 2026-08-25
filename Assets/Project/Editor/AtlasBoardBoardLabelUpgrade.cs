#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardBoardLabelUpgrade
{
    private static readonly Dictionary<
        string,
        string> ShortNames =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                // Turkey / future Turkey labels
                { "Afyonkarahisar", "Afyon" },

                // Colorado
                { "Colorado Springs", "C. Springs" },
                { "Glenwood Springs", "Glenwood" },
                { "Steamboat Springs", "Steamboat" },
                { "Grand Junction", "G. Junction" },
                { "Fort Collins", "Ft. Collins" },
                { "Fort Morgan", "Ft. Morgan" },

                // USA
                { "New Hampshire", "N. Hampshire" },
                { "North Carolina", "N. Carolina" }
            };

    [MenuItem(
        "Atlas Board/Data/Apply Board Short Names")]
    public static void ApplyBoardShortNames()
    {
        string[] mapGuids =
            AssetDatabase.FindAssets(
                "t:BoardMapDefinition",
                new[]
                {
                    "Assets/Project/Data/Maps"
                });

        int updatedMaps = 0;
        int updatedTiles = 0;

        foreach (string guid
                 in mapGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            BoardMapDefinition map =
                AssetDatabase.LoadAssetAtPath<
                    BoardMapDefinition>(
                        path);

            if (map == null ||
                map.Tiles == null)
            {
                continue;
            }

            bool changed = false;

            foreach (BoardTileDefinition tile
                     in map.Tiles)
            {
                if (tile == null)
                {
                    continue;
                }

                string boardName =
                    GetBoardName(
                        tile.DisplayName,
                        tile.TileType);

                tile.EditorSetBoardDisplayName(
                    boardName);

                updatedTiles++;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(
                    map);

                updatedMaps++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Atlas Board short board labels applied. " +
            $"Maps: {updatedMaps}, tiles: {updatedTiles}. " +
            "Full DisplayName values were NOT changed.");
    }

    private static string GetBoardName(
        string fullName,
        TileType tileType)
    {
        if (string.IsNullOrWhiteSpace(
                fullName))
        {
            return string.Empty;
        }

        if (tileType != TileType.City)
        {
            return fullName;
        }

        if (ShortNames.TryGetValue(
                fullName,
                out string shortName))
        {
            return shortName;
        }

        return fullName;
    }
}
#endif
