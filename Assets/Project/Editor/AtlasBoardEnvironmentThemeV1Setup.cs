#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardEnvironmentThemeV1Setup
{
    private const string DataRoot =
        "Assets/Project/Data/Environment";

    private const string ClassicThemePath =
        DataRoot + "/Theme_ClassicTable.asset";

    [MenuItem(
        "Atlas Board/Environment/Create Theme Foundation v1")]
    public static void CreateThemeFoundation()
    {
        GameObject environmentRootObject =
            GameObject.Find(
                "EnvironmentRoot");

        if (environmentRootObject == null)
        {
            Debug.LogError(
                "EnvironmentRoot was not found in the open scene.");

            return;
        }

        Transform environmentRoot =
            environmentRootObject.transform;

        Transform tableSurface =
            environmentRoot.Find(
                "TableSurface");

        Transform tableUnderlay =
            environmentRoot.Find(
                "TableUnderlay");

        if (tableSurface == null ||
            tableUnderlay == null)
        {
            Debug.LogError(
                "EnvironmentRoot must contain TableSurface and " +
                "TableUnderlay before creating the theme foundation.");

            return;
        }

        Renderer surfaceRenderer =
            tableSurface.GetComponent<Renderer>();

        Renderer underlayRenderer =
            tableUnderlay.GetComponent<Renderer>();

        if (surfaceRenderer == null ||
            underlayRenderer == null)
        {
            Debug.LogError(
                "TableSurface and TableUnderlay must have Renderer components.");

            return;
        }

        Transform backgroundRoot =
            FindOrCreateRoot(
                environmentRoot,
                "BackgroundRoot");

        Transform propsRoot =
            FindOrCreateRoot(
                environmentRoot,
                "PropsRoot");

        Light directionalLight =
            Object.FindObjectsByType<
                    Light>()
                .FirstOrDefault(
                    light =>
                        light != null &&
                        light.type ==
                        LightType.Directional);

        EnsureFolder(
            DataRoot);

        EnvironmentThemeProfile classicTheme =
            AssetDatabase.LoadAssetAtPath<
                EnvironmentThemeProfile>(
                    ClassicThemePath);

        if (classicTheme == null)
        {
            classicTheme =
                ScriptableObject.CreateInstance<
                    EnvironmentThemeProfile>();

            AssetDatabase.CreateAsset(
                classicTheme,
                ClassicThemePath);
        }

        classicTheme.EditorConfigureClassicTable(
            surfaceRenderer.sharedMaterial,
            underlayRenderer.sharedMaterial,
            directionalLight);

        EditorUtility.SetDirty(
            classicTheme);

        EnvironmentThemeManager manager =
            environmentRootObject.GetComponent<
                EnvironmentThemeManager>();

        if (manager == null)
        {
            manager =
                environmentRootObject.AddComponent<
                    EnvironmentThemeManager>();
        }

        manager.EditorConfigure(
            environmentRoot,
            surfaceRenderer,
            underlayRenderer,
            backgroundRoot,
            propsRoot,
            directionalLight,
            classicTheme,
            new[]
            {
                classicTheme
            });

        EditorUtility.SetDirty(
            manager);

        EditorUtility.SetDirty(
            environmentRootObject);

        if (environmentRootObject
                .scene
                .IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                environmentRootObject.scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject =
            classicTheme;

        Debug.Log(
            "Environment Theme Foundation v1 created. " +
            "Classic Table is now the default theme. " +
            "BackgroundRoot and PropsRoot are ready for " +
            "future Beach/Street/etc. themes. Save the scene.");
    }

    [MenuItem(
        "Atlas Board/Environment/Apply Classic Table Theme")]
    public static void ApplyClassicTableTheme()
    {
        EnvironmentThemeManager manager =
            Object.FindAnyObjectByType<
                EnvironmentThemeManager>();

        EnvironmentThemeProfile theme =
            AssetDatabase.LoadAssetAtPath<
                EnvironmentThemeProfile>(
                    ClassicThemePath);

        if (manager == null ||
            theme == null)
        {
            Debug.LogWarning(
                "Theme foundation is missing. Run " +
                "Create Theme Foundation v1 first.");

            return;
        }

        manager.ApplyTheme(theme);

        EditorUtility.SetDirty(
            manager);

        if (manager.gameObject
                .scene
                .IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                manager.gameObject.scene);
        }

        Debug.Log(
            "Classic Table theme applied.");
    }

    private static Transform FindOrCreateRoot(
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
