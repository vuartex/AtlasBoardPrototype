#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardEconomyBalanceV1
{
    private const string RulesPath =
        "Assets/Project/Data/Rules";

    private const string MapsPath =
        "Assets/Project/Data/Maps";

    private const string BotProfilesPath =
        "Assets/Project/Data/Players/Bots";

    [MenuItem(
        "Atlas Board/Balance/Apply Economy Balance v1 (One-Time Baseline)")]
    public static void ApplyEconomyBalanceV1()
    {
        List<BoardEconomyProfile> profiles =
            LoadAssets<BoardEconomyProfile>(
                RulesPath);

        if (profiles.Count == 0)
        {
            Debug.LogError(
                "No BoardEconomyProfile asset was found under " +
                $"{RulesPath}.");

            return;
        }

        foreach (BoardEconomyProfile profile
                 in profiles)
        {
            profile.EditorApplyEconomyBalanceV1();
            EditorUtility.SetDirty(profile);
        }

        CreateMissingBotPersonalityPresets();
        SyncPropertyEconomyFromMapProfiles();
        AutoWireBotPersonalitiesInOpenScene();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Economy Balance v1 applied. " +
            "Profile values, property prices/rents/development costs, " +
            "and missing bot personality presets are ready.");
    }

    [MenuItem(
        "Atlas Board/Balance/Sync Property Economy From Map Profiles")]
    public static void SyncPropertyEconomyFromMapProfiles()
    {
        List<BoardMapDefinition> maps =
            LoadAssets<BoardMapDefinition>(
                MapsPath);

        int updatedMaps = 0;
        int updatedProperties = 0;

        foreach (BoardMapDefinition map
                 in maps)
        {
            if (map == null ||
                map.EconomyProfile == null)
            {
                Debug.LogWarning(
                    $"{map?.name ?? "Map"} has no Economy Profile.");

                continue;
            }

            List<BoardTileDefinition> properties =
                map.GetPropertyDefinitions()
                    .Where(
                        definition =>
                            definition != null)
                    .OrderBy(
                        definition =>
                            definition.TileIndex)
                    .ToList();

            IEnumerable<IGrouping<
                string,
                BoardTileDefinition>>
                groups =
                    properties
                        .Where(
                            definition =>
                                !string.IsNullOrWhiteSpace(
                                    definition.GroupId))
                        .GroupBy(
                            definition =>
                                definition.GroupId);

            bool mapChanged = false;

            foreach (IGrouping<
                         string,
                         BoardTileDefinition> group
                     in groups)
            {
                List<BoardTileDefinition>
                    orderedGroup =
                        group
                            .OrderBy(
                                definition =>
                                    definition.TileIndex)
                            .ToList();

                if (orderedGroup.Count != 3)
                {
                    Debug.LogWarning(
                        $"{map.DisplayName} / {group.Key} contains " +
                        $"{orderedGroup.Count} properties. Economy v1 " +
                        "is designed for 3-property groups; values will " +
                        "use the nearest available member slot.",
                        map);
                }

                for (int memberIndex = 0;
                     memberIndex <
                     orderedGroup.Count;
                     memberIndex++)
                {
                    if (!map.EconomyProfile
                            .TryGetPropertyEconomy(
                                group.Key,
                                memberIndex,
                                out int purchasePrice,
                                out int baseRent,
                                out int developmentCost))
                    {
                        Debug.LogWarning(
                            $"{map.EconomyProfile.name} has no " +
                            $"economy tier for {group.Key}.",
                            map.EconomyProfile);

                        continue;
                    }

                    orderedGroup[
                            memberIndex]
                        .EditorSetEconomy(
                            purchasePrice,
                            baseRent,
                            developmentCost);

                    updatedProperties++;
                    mapChanged = true;
                }
            }

            if (mapChanged)
            {
                EditorUtility.SetDirty(map);
                updatedMaps++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"Property economy synchronized. " +
            $"Maps: {updatedMaps}, properties: {updatedProperties}. " +
            "Names, board short names, group colors, descriptions, " +
            "special overrides and map identity were NOT changed.");
    }

    [MenuItem(
        "Atlas Board/Balance/Create Missing Bot Personality Presets")]
    public static void CreateMissingBotPersonalityPresets()
    {
        EnsureFolder(
            "Assets/Project/Data/Players");

        EnsureFolder(
            BotProfilesPath);

        CreatePersonalityIfMissing(
            "BotPersonality_Balanced.asset",
            "balanced",
            "Dengeli",
            "Standart nakit rezervi, yatırım ve risk dengesi.",
            1.00f,
            1.00f,
            1.00f,
            1.00f,
            1.00f,
            1.00f,
            1.00f,
            1.00f,
            0.00f);

        CreatePersonalityIfMissing(
            "BotPersonality_Safe.asset",
            "safe",
            "Temkinli",
            "Daha yüksek nakit rezervi tutar; ihale, geliştirme ve " +
            "takaslarda daha seçicidir.",
            1.35f,
            0.85f,
            0.85f,
            0.80f,
            0.65f,
            1.08f,
            1.05f,
            0.85f,
            0.10f);

        CreatePersonalityIfMissing(
            "BotPersonality_Aggressive.asset",
            "aggressive",
            "Agresif",
            "Daha düşük rezervle oynar; mülk, ihale, geliştirme " +
            "ve takasta daha fazla risk alır.",
            0.70f,
            1.18f,
            1.18f,
            1.22f,
            1.35f,
            0.93f,
            1.15f,
            1.10f,
            0.10f);

        CreatePersonalityIfMissing(
            "BotPersonality_Adaptive.asset",
            "adaptive",
            "Uyarlanabilir",
            "Nakit ve rakiplere göre risk seviyesini dinamik değiştirir; " +
            "gerideyken daha agresif, öndeyken daha korumacı olabilir.",
            1.00f,
            1.00f,
            1.00f,
            1.00f,
            1.00f,
            1.00f,
            1.08f,
            1.00f,
            1.00f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem(
        "Atlas Board/Balance/Auto-Wire Bot Personalities In Open Scene")]
    public static void AutoWireBotPersonalitiesInOpenScene()
    {
        BotPersonalityProfile balanced =
            LoadPersonality(
                "BotPersonality_Balanced.asset");

        BotPersonalityProfile safe =
            LoadPersonality(
                "BotPersonality_Safe.asset");

        BotPersonalityProfile aggressive =
            LoadPersonality(
                "BotPersonality_Aggressive.asset");

        BotPersonalityProfile adaptive =
            LoadPersonality(
                "BotPersonality_Adaptive.asset");

        if (balanced == null ||
            safe == null ||
            aggressive == null ||
            adaptive == null)
        {
            Debug.LogWarning(
                "One or more bot personality assets are missing. " +
                "Run Create Missing Bot Personality Presets first.");

            return;
        }

        BotPersonalityProfile[] profilesBySlot =
        {
            balanced,
            safe,
            aggressive,
            adaptive
        };

        BotPlayerController[] botControllers =
            Object.FindObjectsByType<
                BotPlayerController>();

        int wiredCount = 0;

        foreach (BotPlayerController bot
                 in botControllers)
        {
            if (bot == null)
            {
                continue;
            }

            PlayerGameState player =
                bot.GetComponent<
                    PlayerGameState>();

            if (player == null)
            {
                continue;
            }

            int slot =
                player.PlayerSlotIndex;

            if (slot < 0 ||
                slot >= profilesBySlot.Length)
            {
                continue;
            }

            SerializedObject serializedBot =
                new SerializedObject(bot);

            SerializedProperty personalityProperty =
                serializedBot.FindProperty(
                    "personalityProfile");

            if (personalityProperty == null)
            {
                Debug.LogWarning(
                    $"{bot.name} does not yet contain the " +
                    "personalityProfile field. Replace " +
                    "BotPlayerController.cs with the Economy v1 version.",
                    bot);

                continue;
            }

            personalityProperty.objectReferenceValue =
                profilesBySlot[slot];

            serializedBot.ApplyModifiedProperties();

            EditorUtility.SetDirty(bot);

            if (bot.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(
                    bot.gameObject.scene);
            }

            wiredCount++;
        }

        Debug.Log(
            $"Bot personality auto-wire complete. " +
            $"Controllers updated: {wiredCount}. " +
            "Slot mapping: P1 Balanced, P2 Safe, " +
            "P3 Aggressive, P4 Adaptive. Save the scene.");
    }

    private static BotPersonalityProfile
        LoadPersonality(
            string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<
            BotPersonalityProfile>(
                $"{BotProfilesPath}/{fileName}");
    }

    private static void CreatePersonalityIfMissing(
        string fileName,
        string personalityId,
        string displayName,
        string description,
        float reserve,
        float purchase,
        float auction,
        float development,
        float tradeFrequency,
        float tradeAcceptance,
        float groupFocus,
        float travel,
        float adaptive)
    {
        string path =
            $"{BotProfilesPath}/{fileName}";

        BotPersonalityProfile existing =
            AssetDatabase.LoadAssetAtPath<
                BotPersonalityProfile>(
                    path);

        if (existing != null)
        {
            return;
        }

        BotPersonalityProfile profile =
            ScriptableObject.CreateInstance<
                BotPersonalityProfile>();

        profile.EditorConfigure(
            personalityId,
            displayName,
            description,
            reserve,
            purchase,
            auction,
            development,
            tradeFrequency,
            tradeAcceptance,
            groupFocus,
            travel,
            adaptive);

        AssetDatabase.CreateAsset(
            profile,
            path);

        EditorUtility.SetDirty(profile);
    }

    private static List<T>
        LoadAssets<T>(
            string rootPath)
        where T : Object
    {
        string[] guids =
            AssetDatabase.FindAssets(
                $"t:{typeof(T).Name}",
                new[]
                {
                    rootPath
                });

        List<T> assets =
            new List<T>();

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase
                    .GUIDToAssetPath(guid);

            T asset =
                AssetDatabase
                    .LoadAssetAtPath<T>(
                        path);

            if (asset != null)
            {
                assets.Add(asset);
            }
        }

        return assets;
    }

    private static void EnsureFolder(
        string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent =
            System.IO.Path
                .GetDirectoryName(path)
                ?.Replace("\\", "/");

        string folder =
            System.IO.Path
                .GetFileName(path);

        if (!string.IsNullOrWhiteSpace(parent) &&
            !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(
            parent,
            folder);
    }
}
#endif
