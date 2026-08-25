#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardEnvironmentThemeV11Upgrade
{
    private const string DataRoot =
        "Assets/Project/Data/Environment";

    private const string ClassicPath =
        DataRoot + "/Theme_ClassicTable.asset";

    private const string GardenPath =
        DataRoot + "/Theme_Garden.asset";

    private const string BeachPath =
        DataRoot + "/Theme_Beach.asset";

    private const string PavilionPath =
        DataRoot + "/Theme_Pavilion.asset";

    private const string StreetPath =
        DataRoot + "/Theme_Street.asset";

    [MenuItem(
        "Atlas Board/Environment/Upgrade Theme System to v1.1")]
    public static void UpgradeThemeSystem()
    {
        GameObject environmentObject =
            GameObject.Find(
                "EnvironmentRoot");

        if (environmentObject == null)
        {
            Debug.LogError(
                "EnvironmentRoot was not found.");

            return;
        }

        Transform environmentRoot =
            environmentObject.transform;

        Renderer surfaceRenderer =
            FindRenderer(
                environmentRoot,
                "TableSurface");

        Renderer underlayRenderer =
            FindRenderer(
                environmentRoot,
                "TableUnderlay");

        if (surfaceRenderer == null ||
            underlayRenderer == null)
        {
            Debug.LogError(
                "TableSurface / TableUnderlay renderer is missing.");

            return;
        }

        Transform backgroundRoot =
            FindOrCreateChild(
                environmentRoot,
                "BackgroundRoot");

        Transform propsRoot =
            FindOrCreateChild(
                environmentRoot,
                "PropsRoot");

        Transform gardenProps =
            FindOrCreateChild(
                propsRoot,
                "PF_Theme_Garden_Props");

        Transform beachProps =
            FindOrCreateChild(
                propsRoot,
                "PF_Theme_Beach_Props");

        Transform pavilionProps =
            FindOrCreateChild(
                propsRoot,
                "PF_Theme_Pavilion_Props");

        Transform streetProps =
            FindOrCreateChild(
                propsRoot,
                "PF_Theme_Street_Props");

        Light directionalLight =
            Object.FindObjectsByType<Light>()
                .FirstOrDefault(
                    light =>
                        light != null &&
                        light.type ==
                        LightType.Directional);

        EnsureFolder(
            DataRoot);

        Material classicSkybox =
            RenderSettings.skybox;

        // If the user currently has one of the new theme HDRIs
        // temporarily assigned globally, do not accidentally save
        // that as the Classic theme.
        if (classicSkybox != null &&
            classicSkybox.name.StartsWith(
                "MAT_SKY_",
                System.StringComparison.OrdinalIgnoreCase))
        {
            classicSkybox = null;
        }

        Material gardenSkybox =
            FindMaterialExact(
                "MAT_SKY_Garden");

        Material beachSkybox =
            FindMaterialExact(
                "MAT_SKY_Beach");

        Material pavilionSkybox =
            FindMaterialExact(
                "MAT_SKY_Pavilion");

        Material streetSkybox =
            FindMaterialExact(
                "MAT_SKY_Street");

        EnvironmentThemeProfile classic =
            LoadOrCreateTheme(
                ClassicPath);

        EnvironmentThemeProfile garden =
            LoadOrCreateTheme(
                GardenPath);

        EnvironmentThemeProfile beach =
            LoadOrCreateTheme(
                BeachPath);

        EnvironmentThemeProfile pavilion =
            LoadOrCreateTheme(
                PavilionPath);

        EnvironmentThemeProfile street =
            LoadOrCreateTheme(
                StreetPath);

        classic.EditorConfigureV11(
            "classic_table",
            "Classic Table",
            "Clean default tabletop theme with no theme-specific props.",
            surfaceRenderer.sharedMaterial,
            underlayRenderer.sharedMaterial,
            classicSkybox,
            string.Empty,
            string.Empty,
            directionalLight);

        garden.EditorConfigureV11(
            "garden",
            "Garden",
            "Suburban Garden HDRI with tabletop nature decoration.",
            surfaceRenderer.sharedMaterial,
            underlayRenderer.sharedMaterial,
            gardenSkybox,
            gardenProps.name,
            string.Empty,
            directionalLight);

        beach.EditorConfigureV11(
            "beach",
            "Beach",
            "Secluded Beach HDRI. Props can be authored later.",
            surfaceRenderer.sharedMaterial,
            underlayRenderer.sharedMaterial,
            beachSkybox,
            beachProps.name,
            string.Empty,
            directionalLight);

        pavilion.EditorConfigureV11(
            "pavilion",
            "Pavilion",
            "Boma pavilion / hospitality environment.",
            surfaceRenderer.sharedMaterial,
            underlayRenderer.sharedMaterial,
            pavilionSkybox,
            pavilionProps.name,
            string.Empty,
            directionalLight);

        street.EditorConfigureV11(
            "street",
            "Street",
            "Palermo Sidewalk HDRI with tabletop city decoration.",
            surfaceRenderer.sharedMaterial,
            underlayRenderer.sharedMaterial,
            streetSkybox,
            streetProps.name,
            string.Empty,
            directionalLight);

        EnvironmentThemeProfile[] themes =
        {
            classic,
            garden,
            beach,
            pavilion,
            street
        };

        foreach (EnvironmentThemeProfile theme
                 in themes)
        {
            EditorUtility.SetDirty(
                theme);
        }

        EnvironmentThemeManager manager =
            environmentObject.GetComponent<
                EnvironmentThemeManager>();

        if (manager == null)
        {
            manager =
                environmentObject.AddComponent<
                    EnvironmentThemeManager>();
        }

        manager.EditorConfigureV11(
            environmentRoot,
            surfaceRenderer,
            underlayRenderer,
            backgroundRoot,
            propsRoot,
            directionalLight,
            classic,
            themes);

        // Default authoring state: Classic.
        // This makes every theme-specific prop group invisible
        // until that theme is explicitly selected.
        manager.ApplyTheme(
            classic);

        EditorUtility.SetDirty(
            manager);

        EditorUtility.SetDirty(
            environmentObject);

        if (environmentObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                environmentObject.scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject =
            classic;

        LogMaterialCheck(
            "Garden",
            gardenSkybox);

        LogMaterialCheck(
            "Beach",
            beachSkybox);

        LogMaterialCheck(
            "Pavilion",
            pavilionSkybox);

        LogMaterialCheck(
            "Street",
            streetSkybox);

        Debug.Log(
            "Environment Theme v1.1 ready. " +
            "Five themes are configured. Classic is active, so all " +
            "theme-specific prop roots are currently hidden. " +
            "Use Atlas Board > Environment > Themes to author/test themes. " +
            "Save the scene.");
    }

    [MenuItem(
        "Atlas Board/Environment/Themes/Apply Classic Table")]
    public static void ApplyClassic()
    {
        ApplyThemeAsset(
            ClassicPath);
    }

    [MenuItem(
        "Atlas Board/Environment/Themes/Apply Garden")]
    public static void ApplyGarden()
    {
        ApplyThemeAsset(
            GardenPath);
    }

    [MenuItem(
        "Atlas Board/Environment/Themes/Apply Beach")]
    public static void ApplyBeach()
    {
        ApplyThemeAsset(
            BeachPath);
    }

    [MenuItem(
        "Atlas Board/Environment/Themes/Apply Pavilion")]
    public static void ApplyPavilion()
    {
        ApplyThemeAsset(
            PavilionPath);
    }

    [MenuItem(
        "Atlas Board/Environment/Themes/Apply Street")]
    public static void ApplyStreet()
    {
        ApplyThemeAsset(
            StreetPath);
    }

    private static void ApplyThemeAsset(
        string path)
    {
        EnvironmentThemeManager manager =
            Object.FindAnyObjectByType<
                EnvironmentThemeManager>();

        EnvironmentThemeProfile theme =
            AssetDatabase.LoadAssetAtPath<
                EnvironmentThemeProfile>(
                    path);

        if (manager == null ||
            theme == null)
        {
            Debug.LogWarning(
                "Environment Theme v1.1 is not configured yet. " +
                "Run Upgrade Theme System to v1.1 first.");

            return;
        }

        manager.ApplyTheme(
            theme);

        EditorUtility.SetDirty(
            manager);

        if (manager.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                manager.gameObject.scene);
        }

        Selection.activeObject =
            theme;
    }

    private static EnvironmentThemeProfile
        LoadOrCreateTheme(
            string path)
    {
        EnvironmentThemeProfile theme =
            AssetDatabase.LoadAssetAtPath<
                EnvironmentThemeProfile>(
                    path);

        if (theme != null)
        {
            return theme;
        }

        theme =
            ScriptableObject.CreateInstance<
                EnvironmentThemeProfile>();

        AssetDatabase.CreateAsset(
            theme,
            path);

        return theme;
    }

    private static Renderer FindRenderer(
        Transform parent,
        string childName)
    {
        Transform child =
            parent.Find(
                childName);

        return child != null
            ? child.GetComponent<Renderer>()
            : null;
    }

    private static Transform FindOrCreateChild(
        Transform parent,
        string childName)
    {
        Transform existing =
            parent.Find(
                childName);

        if (existing != null)
        {
            return existing;
        }

        GameObject child =
            new GameObject(
                childName);

        child.transform.SetParent(
            parent,
            false);

        return child.transform;
    }

    private static Material FindMaterialExact(
        string materialName)
    {
        string[] guids =
            AssetDatabase.FindAssets(
                materialName + " t:Material");

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            Material material =
                AssetDatabase.LoadAssetAtPath<
                    Material>(
                        path);

            if (material != null &&
                material.name ==
                    materialName)
            {
                return material;
            }
        }

        return null;
    }

    private static void LogMaterialCheck(
        string themeName,
        Material material)
    {
        if (material != null)
        {
            Debug.Log(
                $"{themeName} skybox linked: {material.name}");
        }
        else
        {
            Debug.LogWarning(
                $"{themeName} skybox material was not found. " +
                $"Check that MAT_SKY_{themeName} exists.");
        }
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
            EnsureFolder(
                parent);
        }

        AssetDatabase.CreateFolder(
            parent,
            folder);
    }
}
#endif
