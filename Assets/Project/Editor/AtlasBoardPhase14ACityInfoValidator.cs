#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase14ACityInfoValidator
{
    private const string MapsFolder =
        "Assets/Project/Data/Maps";

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14A - City Information/Validate City Information Panel Data")]
    public static void ValidateCityInformationPanelData()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:BoardMapDefinition",
                new[]
                {
                    MapsFolder
                });

        int mapCount = 0;
        int cityCount = 0;
        int describedCityCount = 0;
        int errors = 0;

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            BoardMapDefinition map =
                AssetDatabase.LoadAssetAtPath<
                    BoardMapDefinition>(path);

            if (map == null)
            {
                continue;
            }

            mapCount++;

            HashSet<string> propertyIds =
                new HashSet<string>();

            foreach (BoardTileDefinition tile
                     in map.GetPropertyDefinitions())
            {
                if (tile == null)
                {
                    continue;
                }

                cityCount++;

                if (!string.IsNullOrWhiteSpace(
                        tile.Description))
                {
                    describedCityCount++;
                }

                if (string.IsNullOrWhiteSpace(
                        tile.DisplayName))
                {
                    Debug.LogError(
                        $"Phase 14A: {map.name} has a City tile " +
                        $"without DisplayName at index {tile.TileIndex}.",
                        map);
                    errors++;
                }

                if (string.IsNullOrWhiteSpace(
                        tile.PropertyId))
                {
                    Debug.LogError(
                        $"Phase 14A: {map.name} / " +
                        $"{tile.DisplayName} is missing PropertyId.",
                        map);
                    errors++;
                }
                else if (!propertyIds.Add(
                             tile.PropertyId))
                {
                    Debug.LogError(
                        $"Phase 14A: {map.name} contains duplicate " +
                        $"PropertyId '{tile.PropertyId}'.",
                        map);
                    errors++;
                }

                if (tile.PurchasePrice <= 0 ||
                    tile.BaseRent <= 0 ||
                    tile.DevelopmentCost <= 0)
                {
                    Debug.LogError(
                        $"Phase 14A: {map.name} / " +
                        $"{tile.DisplayName} has incomplete property economy data.",
                        map);
                    errors++;
                }
            }
        }

        if (errors > 0)
        {
            Debug.LogError(
                $"Phase 14A validation FAILED. Maps: {mapCount}, " +
                $"City properties: {cityCount}, Errors: {errors}.");
            return;
        }

        Debug.Log(
            $"Phase 14A validation PASS. Maps: {mapCount}, " +
            $"City properties: {cityCount}. Custom city descriptions present: " +
            $"{describedCityCount}/{cityCount}. Cities without a custom " +
            "description use the localized fallback information sentence.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14A - City Information/Open First City Panel (Play Mode)")]
    public static void OpenFirstCityPanelInPlayMode()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Enter Play Mode before using the Phase 14A city-panel smoke command.");
            return;
        }

        AtlasBoardCityInformationPanel panel =
            Object.FindAnyObjectByType<
                AtlasBoardCityInformationPanel>();

        if (panel == null)
        {
            Debug.LogError(
                "Phase 14A city information panel runtime controller was not found.");
            return;
        }

        BoardTile[] tiles =
            Object.FindObjectsByType<
                BoardTile>();

        foreach (BoardTile tile in tiles)
        {
            if (tile != null &&
                tile.TileType == TileType.City)
            {
                panel.ShowTile(tile);
                Debug.Log(
                    $"Phase 14A smoke: opened city info for {tile.DisplayName}.");
                return;
            }
        }

        Debug.LogError(
            "Phase 14A smoke could not find a City BoardTile.");
    }
}
#endif
