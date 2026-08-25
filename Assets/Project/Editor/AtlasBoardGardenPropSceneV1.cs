#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardGardenPropSceneV1
{
    private const string PropsRootName =
        "PF_Theme_Garden_Props";

    private const string GeneratedRootName =
        "__Generated_GardenDecor";

    // Kenney source objects are already fairly small in the user's scene.
    // This multiplier keeps the per-placement variation but makes the final
    // tabletop props visibly substantial.
    private const float GlobalScaleMultiplier =
        2.5f;

    private struct Placement
    {
        public string NameContains;
        public Vector3 Position;
        public float Yaw;
        public float Scale;

        public Placement(
            string nameContains,
            Vector3 position,
            float yaw,
            float scale)
        {
            NameContains = nameContains;
            Position = position;
            Yaw = yaw;
            Scale = scale;
        }
    }

    private const string GardenAssetRoot =
        "Assets/Project/Art/Environment/Props/Garden";

    [MenuItem(
        "Atlas Board/Environment/Debug/List Garden Model Assets")]
    public static void ListGardenModelAssets()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                string.Empty,
                new[]
                {
                    GardenAssetRoot
                });

        int count = 0;

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            string extension =
                System.IO.Path
                    .GetExtension(path)
                    .ToLowerInvariant();

            if (extension != ".fbx" &&
                extension != ".prefab" &&
                extension != ".obj" &&
                extension != ".dae")
            {
                continue;
            }

            count++;

            Debug.Log(
                $"Garden model asset: {path}");
        }

        Debug.Log(
            $"Garden model scan complete. " +
            $"Model-like assets found: {count}. " +
            $"Root: {GardenAssetRoot}");
    }

    [MenuItem(
        "Atlas Board/Environment/Build Garden Prop Scene v1")]
    public static void BuildGardenPropScene()
    {
        GameObject propsRoot =
            GameObject.Find(
                "PropsRoot");

        if (propsRoot == null)
        {
            Debug.LogError(
                "Could not find 'PropsRoot' in the open scene.");

            return;
        }

        Transform oldThemeRoot =
            propsRoot.transform.Find(
                PropsRootName);

        if (oldThemeRoot != null)
        {
            Undo.DestroyObjectImmediate(
                oldThemeRoot.gameObject);
        }

        GameObject themeRoot =
            new GameObject(
                PropsRootName);

        Undo.RegisterCreatedObjectUndo(
            themeRoot,
            "Build Garden Prop Scene v1");

        themeRoot.transform.SetParent(
            propsRoot.transform,
            false);

        themeRoot.transform.localPosition =
            Vector3.zero;

        themeRoot.transform.localRotation =
            Quaternion.identity;

        themeRoot.transform.localScale =
            Vector3.one;

        GameObject generatedRoot =
            new GameObject(
                GeneratedRootName);

        Undo.RegisterCreatedObjectUndo(
            generatedRoot,
            "Build Garden Prop Scene v1");

        generatedRoot.transform.SetParent(
            themeRoot.transform,
            false);

        generatedRoot.transform.localPosition =
            Vector3.zero;

        generatedRoot.transform.localRotation =
            Quaternion.identity;

        generatedRoot.transform.localScale =
            Vector3.one;

        Placement[] placements =
            BuildPlacements();

        int createdCount = 0;
        int missingCount = 0;

        foreach (Placement placement
                 in placements)
        {
            GameObject source =
                FindGardenAsset(
                    placement.NameContains);

            if (source == null)
            {
                source =
                    FindGardenAsset(
                        GetFallbackToken(
                            placement.NameContains));
            }

            if (source == null)
            {
                Debug.LogWarning(
                    $"Garden source asset containing " +
                    $"'{placement.NameContains}' was not found " +
                    $"under {GardenAssetRoot}.");

                missingCount++;
                continue;
            }

            GameObject clone =
                PrefabUtility.InstantiatePrefab(
                    source,
                    generatedRoot.transform)
                as GameObject;

            if (clone == null)
            {
                clone =
                    UnityEngine.Object.Instantiate(
                        source,
                        generatedRoot.transform);
            }

            Undo.RegisterCreatedObjectUndo(
                clone,
                "Duplicate Garden Prop");

            clone.name =
                $"{source.name}_decor_{createdCount + 1:00}";

            clone.SetActive(true);

            clone.transform.localPosition =
                placement.Position;

            clone.transform.localRotation =
                Quaternion.Euler(
                    0f,
                    placement.Yaw,
                    0f);

            clone.transform.localScale =
                source.transform.localScale *
                placement.Scale *
                GlobalScaleMultiplier;

            DisableColliders(
                clone);

            createdCount++;
        }

        EditorUtility.SetDirty(
            themeRoot);

        EditorUtility.SetDirty(
            generatedRoot);

        if (themeRoot.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                themeRoot.scene);
        }

        Selection.activeGameObject =
            generatedRoot;

        Debug.Log(
            $"Garden prop scene built from project assets. " +
            $"Generated: {createdCount}, missing asset lookups: {missingCount}. " +
            $"The tool no longer depends on hidden source props in the scene.",
            generatedRoot);
    }

    private static GameObject FindGardenAsset(
        string token)
    {
        string[] guids =
            AssetDatabase.FindAssets(
                string.Empty,
                new[]
                {
                    GardenAssetRoot
                });

        int inspectedAssetCount = 0;

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string extension =
                System.IO.Path
                    .GetExtension(path)
                    .ToLowerInvariant();

            if (extension != ".fbx" &&
                extension != ".prefab" &&
                extension != ".obj" &&
                extension != ".dae")
            {
                continue;
            }

            inspectedAssetCount++;

            string fileName =
                System.IO.Path
                    .GetFileNameWithoutExtension(
                        path);

            bool nameMatches =
                fileName.IndexOf(
                    token,
                    StringComparison.OrdinalIgnoreCase) >= 0;

            if (!nameMatches)
            {
                continue;
            }

            UnityEngine.Object mainAsset =
                AssetDatabase.LoadMainAssetAtPath(
                    path);

            GameObject gameObject =
                mainAsset as GameObject;

            if (gameObject != null)
            {
                return gameObject;
            }

            GameObject loadedAsGameObject =
                AssetDatabase.LoadAssetAtPath<
                    GameObject>(
                        path);

            if (loadedAsGameObject != null)
            {
                return loadedAsGameObject;
            }
        }

        Debug.LogWarning(
            $"No usable model asset matching '{token}' was found under " +
            $"{GardenAssetRoot}. Model-like assets inspected: " +
            $"{inspectedAssetCount}.");

        return null;
    }

    [MenuItem(
        "Atlas Board/Environment/Restore Garden Prop Palette")]
    public static void RestoreGardenPalette()
    {
        GameObject propsRoot =
            GameObject.Find(
                "PropsRoot");

        if (propsRoot == null)
        {
            Debug.LogWarning(
                "PropsRoot was not found.");

            return;
        }

        Transform themeRoot =
            propsRoot.transform.Find(
                PropsRootName);

        if (themeRoot != null)
        {
            Undo.DestroyObjectImmediate(
                themeRoot.gameObject);
        }

        if (propsRoot.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                propsRoot.scene);
        }

        Debug.Log(
            "Garden theme scene objects removed. " +
            "Source assets remain safely in the Project folder.");
    }

    [MenuItem(
        "Atlas Board/Environment/Garden Decor Size/Increase +20%")]
    public static void IncreaseGardenDecorSize()
    {
        ScaleGeneratedDecor(
            1.20f,
            "Garden decor size increased by 20%.");
    }

    [MenuItem(
        "Atlas Board/Environment/Garden Decor Size/Decrease -20%")]
    public static void DecreaseGardenDecorSize()
    {
        ScaleGeneratedDecor(
            0.80f,
            "Garden decor size decreased by 20%.");
    }

    private static void ScaleGeneratedDecor(
        float multiplier,
        string message)
    {
        GameObject propsRoot =
            GameObject.Find(
                "PropsRoot");

        if (propsRoot == null)
        {
            Debug.LogWarning(
                "PropsRoot was not found.");

            return;
        }

        Transform themeRoot =
            propsRoot.transform.Find(
                PropsRootName);

        if (themeRoot == null)
        {
            Debug.LogWarning(
                $"{PropsRootName} was not found.");

            return;
        }

        Transform generatedRoot =
            themeRoot.Find(
                GeneratedRootName);

        if (generatedRoot == null)
        {
            Debug.LogWarning(
                "Generated garden decor was not found. " +
                "Run Build Garden Prop Scene v1 first.");

            return;
        }

        foreach (Transform child
                 in generatedRoot)
        {
            if (child == null)
            {
                continue;
            }

            Undo.RecordObject(
                child,
                "Scale Garden Decor");

            child.localScale *=
                multiplier;
        }

        EditorUtility.SetDirty(
            generatedRoot);

        if (generatedRoot.gameObject
                .scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                generatedRoot.gameObject.scene);
        }

        Debug.Log(
            message,
            generatedRoot);
    }

    private static string GetFallbackToken(
        string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        string lower =
            token.ToLowerInvariant();

        if (lower.Contains("tree"))
        {
            return "tree";
        }

        if (lower.Contains("stone") ||
            lower.Contains("rock"))
        {
            return "rock";
        }

        if (lower.Contains("plant") ||
            lower.Contains("bush"))
        {
            return "plant";
        }

        if (lower.Contains("flower"))
        {
            return "flower";
        }

        if (lower.Contains("grass"))
        {
            return "grass";
        }

        if (lower.Contains("path"))
        {
            return "path";
        }

        if (lower.Contains("fence"))
        {
            return "fence";
        }

        if (lower.Contains("campfire"))
        {
            return "campfire";
        }

        if (lower.Contains("corn") ||
            lower.Contains("crop"))
        {
            return "corn";
        }

        if (lower.Contains("statue"))
        {
            return "statue";
        }

        return token;
    }

    private static Placement[]
        BuildPlacements()
    {
        // Designed for a ~19x19 board on a larger tabletop.
        // Y is intentionally close to zero because the user's palette root
        // is expected to be aligned to tabletop height. Fine-tune the root
        // Y once if the imported kit pivots differ.
        return new[]
        {
            // Upper-left garden cluster
            new Placement(
                "tree_blocks",
                new Vector3(-13.5f, 0f, 11.5f),
                18f,
                0.55f),
            new Placement(
                "plant_bushTriangle",
                new Vector3(-11.8f, 0f, 12.8f),
                55f,
                0.60f),
            new Placement(
                "stone_tallA",
                new Vector3(-15.0f, 0f, 9.6f),
                80f,
                0.48f),
            new Placement(
                "flower_redC",
                new Vector3(-12.6f, 0f, 9.9f),
                5f,
                0.72f),
            new Placement(
                "grass_large",
                new Vector3(-14.5f, 0f, 13.3f),
                95f,
                0.58f),

            // Upper-right garden cluster
            new Placement(
                "tree_thin_fall",
                new Vector3(13.3f, 0f, 11.2f),
                -28f,
                0.58f),
            new Placement(
                "plant_flatTall",
                new Vector3(11.7f, 0f, 12.8f),
                25f,
                0.62f),
            new Placement(
                "rock_tallF",
                new Vector3(14.8f, 0f, 9.4f),
                120f,
                0.50f),
            new Placement(
                "flower_redC",
                new Vector3(12.3f, 0f, 9.8f),
                -18f,
                0.68f),
            new Placement(
                "grass_large",
                new Vector3(14.4f, 0f, 13.2f),
                40f,
                0.55f),

            // Lower-left garden cluster
            new Placement(
                "tree_thin_fall",
                new Vector3(-13.4f, 0f, -11.2f),
                165f,
                0.55f),
            new Placement(
                "plant_flatTall",
                new Vector3(-11.7f, 0f, -12.6f),
                205f,
                0.61f),
            new Placement(
                "stone_tallH",
                new Vector3(-14.8f, 0f, -9.6f),
                235f,
                0.47f),
            new Placement(
                "flower_redC",
                new Vector3(-12.4f, 0f, -9.7f),
                195f,
                0.68f),

            // Lower-right garden cluster
            new Placement(
                "tree_blocks",
                new Vector3(13.6f, 0f, -11.5f),
                215f,
                0.56f),
            new Placement(
                "plant_bushTriangle",
                new Vector3(11.8f, 0f, -12.7f),
                250f,
                0.62f),
            new Placement(
                "rock_tallF",
                new Vector3(14.8f, 0f, -9.5f),
                280f,
                0.49f),
            new Placement(
                "grass_large",
                new Vector3(14.5f, 0f, -13.2f),
                295f,
                0.56f),

            // Small themed details on far table edge
            new Placement(
                "statue_columnDamaged",
                new Vector3(-6.5f, 0f, 15.0f),
                12f,
                0.42f),
            new Placement(
                "campfire_stones",
                new Vector3(0f, 0f, 15.2f),
                0f,
                0.54f),
            new Placement(
                "crops_cornStageC",
                new Vector3(6.6f, 0f, 15.0f),
                -8f,
                0.50f),

            // Stone-path hints around the outside of the board
            new Placement(
                "path_stone",
                new Vector3(-5.5f, 0f, -14.8f),
                90f,
                0.72f),
            new Placement(
                "path_stone",
                new Vector3(-2.7f, 0f, -14.8f),
                90f,
                0.72f),
            new Placement(
                "path_stone",
                new Vector3(2.7f, 0f, -14.8f),
                90f,
                0.72f),
            new Placement(
                "path_stone",
                new Vector3(5.5f, 0f, -14.8f),
                90f,
                0.72f),

            // Small fence accents at left/right sides
            new Placement(
                "fence_simpleDiagonalCenter",
                new Vector3(-15.2f, 0f, 1.7f),
                0f,
                0.66f),
            new Placement(
                "fence_simpleDiagonalCenter",
                new Vector3(-15.2f, 0f, -1.7f),
                0f,
                0.66f),
            new Placement(
                "fence_simpleDiagonalCenter",
                new Vector3(15.2f, 0f, 1.7f),
                180f,
                0.66f),
            new Placement(
                "fence_simpleDiagonalCenter",
                new Vector3(15.2f, 0f, -1.7f),
                180f,
                0.66f)
        };
    }

    private static void DisableColliders(
        GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Collider[] colliders =
            root.GetComponentsInChildren<
                Collider>(true);

        foreach (Collider collider
                 in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            collider.enabled = false;
        }
    }
}
#endif
