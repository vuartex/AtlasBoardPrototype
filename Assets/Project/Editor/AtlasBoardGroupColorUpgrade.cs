#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AtlasBoardGroupColorUpgrade
{
    [MenuItem(
        "Atlas Board/Data/Apply Group Colors To Existing Maps")]
    public static void ApplyGroupColors()
    {
        string[] mapGuids =
            AssetDatabase.FindAssets(
                "t:BoardMapDefinition",
                new[]
                {
                    "Assets/Project/Data/Maps"
                });

        int updatedMaps = 0;
        int updatedProperties = 0;

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

            bool mapChanged = false;

            foreach (BoardTileDefinition tile
                     in map.Tiles)
            {
                if (tile == null ||
                    tile.TileType !=
                        TileType.City)
                {
                    continue;
                }

                Color color =
                    GetGroupColor(
                        tile.GroupId);

                tile.EditorSetGroupColor(
                    color);

                updatedProperties++;
                mapChanged = true;
            }

            if (mapChanged)
            {
                EditorUtility.SetDirty(
                    map);

                updatedMaps++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Atlas Board group colors applied. " +
            $"Maps: {updatedMaps}, " +
            $"properties: {updatedProperties}. " +
            "Economy values were not changed.");
    }

    private static Color GetGroupColor(
        string groupId)
    {
        int groupNumber = 0;

        if (!string.IsNullOrWhiteSpace(
                groupId))
        {
            string digits =
                groupId.Replace(
                    "group_",
                    string.Empty);

            int.TryParse(
                digits,
                out groupNumber);
        }

        return groupNumber switch
        {
            1 => new Color(
                0.48f, 0.25f, 0.15f, 1f),
            2 => new Color(
                0.35f, 0.74f, 0.94f, 1f),
            3 => new Color(
                0.84f, 0.24f, 0.62f, 1f),
            4 => new Color(
                0.95f, 0.45f, 0.10f, 1f),
            5 => new Color(
                0.90f, 0.14f, 0.15f, 1f),
            6 => new Color(
                0.96f, 0.84f, 0.10f, 1f),
            7 => new Color(
                0.14f, 0.64f, 0.29f, 1f),
            8 => new Color(
                0.08f, 0.29f, 0.82f, 1f),
            _ => new Color(
                0.45f, 0.45f, 0.45f, 1f)
        };
    }
}
#endif
