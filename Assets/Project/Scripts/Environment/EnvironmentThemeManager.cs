using System.Collections.Generic;
using UnityEngine;

public class EnvironmentThemeManager : MonoBehaviour
{
    [Header("Environment References")]
    [SerializeField]
    private Transform environmentRoot;

    [SerializeField]
    private Renderer tableSurfaceRenderer;

    [SerializeField]
    private Renderer tableUnderlayRenderer;

    [SerializeField]
    private Transform backgroundRoot;

    [SerializeField]
    private Transform propsRoot;

    [SerializeField]
    private Light directionalLight;

    [Header("Themes")]
    [SerializeField]
    private EnvironmentThemeProfile defaultTheme;

    [SerializeField]
    private EnvironmentThemeProfile[]
        availableThemes;

    [Header("Runtime")]
    [SerializeField]
    private bool applyDefaultThemeOnStart = true;

    [SerializeField]
    private EnvironmentThemeProfile activeTheme;

    private GameObject spawnedBackground;
    private GameObject spawnedProps;

    public EnvironmentThemeProfile ActiveTheme =>
        activeTheme;

    public IReadOnlyList<EnvironmentThemeProfile>
        AvailableThemes =>
            availableThemes;

    private void Start()
    {
        EnsureReferences();

        if (applyDefaultThemeOnStart &&
            defaultTheme != null)
        {
            ApplyTheme(defaultTheme);
        }
    }

    public bool ApplyThemeById(
        string themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId) ||
            availableThemes == null)
        {
            return false;
        }

        foreach (EnvironmentThemeProfile theme
                 in availableThemes)
        {
            if (theme == null ||
                theme.ThemeId != themeId)
            {
                continue;
            }

            ApplyTheme(theme);
            return true;
        }

        Debug.LogWarning(
            $"Environment theme '{themeId}' was not found.",
            this);

        return false;
    }

    public bool ApplyThemeByIndex(
        int index)
    {
        if (availableThemes == null ||
            index < 0 ||
            index >= availableThemes.Length ||
            availableThemes[index] == null)
        {
            return false;
        }

        ApplyTheme(
            availableThemes[index]);

        return true;
    }

    public void ApplyTheme(
        EnvironmentThemeProfile theme)
    {
        if (theme == null)
        {
            Debug.LogWarning(
                "EnvironmentThemeManager received an empty theme.",
                this);

            return;
        }

        EnsureReferences();

        ApplyTableMaterials(theme);
        ApplySkybox(theme);
        ApplySceneThemeRoots(theme);
        ApplyOptionalPrefabContent(theme);
        ApplyLighting(theme);

        activeTheme = theme;

        Debug.Log(
            $"Environment theme applied: " +
            $"{theme.DisplayName} ({theme.ThemeId}).",
            this);
    }

    private void ApplyTableMaterials(
        EnvironmentThemeProfile theme)
    {
        if (tableSurfaceRenderer != null &&
            theme.TableSurfaceMaterial != null)
        {
            tableSurfaceRenderer.sharedMaterial =
                theme.TableSurfaceMaterial;
        }

        if (tableUnderlayRenderer != null &&
            theme.TableUnderlayMaterial != null)
        {
            tableUnderlayRenderer.sharedMaterial =
                theme.TableUnderlayMaterial;
        }
    }

    private void ApplySkybox(
        EnvironmentThemeProfile theme)
    {
        // Assignment is intentional even when null:
        // switching back to Classic must remove the previous HDRI.
        RenderSettings.skybox =
            theme.SkyboxMaterial;

        DynamicGI.UpdateEnvironment();
    }

    private void ApplySceneThemeRoots(
        EnvironmentThemeProfile theme)
    {
        SetOnlyThemeRootActive(
            propsRoot,
            theme.ScenePropsRootName,
            "PF_Theme_");

        SetOnlyThemeRootActive(
            backgroundRoot,
            theme.SceneBackgroundRootName,
            "BG_Theme_");
    }

    private static void SetOnlyThemeRootActive(
        Transform parent,
        string activeChildName,
        string themePrefix)
    {
        if (parent == null)
        {
            return;
        }

        foreach (Transform child
                 in parent)
        {
            if (child == null ||
                !child.name.StartsWith(
                    themePrefix,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool shouldBeActive =
                !string.IsNullOrWhiteSpace(
                    activeChildName) &&
                string.Equals(
                    child.name,
                    activeChildName,
                    System.StringComparison.OrdinalIgnoreCase);

            if (child.gameObject.activeSelf !=
                shouldBeActive)
            {
                child.gameObject.SetActive(
                    shouldBeActive);
            }
        }
    }

    private void ApplyOptionalPrefabContent(
        EnvironmentThemeProfile theme)
    {
        // Scene roots are preferred for hand-authored theme decoration.
        // Prefab spawning remains available for future themes.
        GameObject backgroundPrefab =
            string.IsNullOrWhiteSpace(
                theme.SceneBackgroundRootName)
                ? theme.BackgroundPrefab
                : null;

        GameObject propsPrefab =
            string.IsNullOrWhiteSpace(
                theme.ScenePropsRootName)
                ? theme.PropsPrefab
                : null;

        ReplaceOptionalPrefab(
            ref spawnedBackground,
            backgroundPrefab,
            backgroundRoot,
            "Background");

        ReplaceOptionalPrefab(
            ref spawnedProps,
            propsPrefab,
            propsRoot,
            "Props");
    }

    private void ApplyLighting(
        EnvironmentThemeProfile theme)
    {
        if (theme.OverrideDirectionalLight &&
            directionalLight != null)
        {
            directionalLight.color =
                theme.DirectionalLightColor;

            directionalLight.intensity =
                theme.DirectionalLightIntensity;

            directionalLight.transform.rotation =
                Quaternion.Euler(
                    theme.DirectionalLightEuler);
        }

        if (theme.OverrideAmbientLight)
        {
            RenderSettings.ambientLight =
                theme.AmbientLightColor;
        }
    }

    private void ReplaceOptionalPrefab(
        ref GameObject currentInstance,
        GameObject prefab,
        Transform parent,
        string label)
    {
        if (currentInstance != null)
        {
            if (Application.isPlaying)
            {
                Destroy(currentInstance);
            }
            else
            {
                DestroyImmediate(
                    currentInstance);
            }

            currentInstance = null;
        }

        if (prefab == null ||
            parent == null)
        {
            return;
        }

        currentInstance =
            Instantiate(
                prefab,
                parent);

        currentInstance.name =
            $"{label}_{prefab.name}";
    }

    private void EnsureReferences()
    {
        if (environmentRoot == null)
        {
            environmentRoot = transform;
        }

        if (tableSurfaceRenderer == null)
        {
            Transform surface =
                environmentRoot.Find(
                    "TableSurface");

            if (surface != null)
            {
                tableSurfaceRenderer =
                    surface.GetComponent<
                        Renderer>();
            }
        }

        if (tableUnderlayRenderer == null)
        {
            Transform underlay =
                environmentRoot.Find(
                    "TableUnderlay");

            if (underlay != null)
            {
                tableUnderlayRenderer =
                    underlay.GetComponent<
                        Renderer>();
            }
        }

        if (backgroundRoot == null)
        {
            backgroundRoot =
                environmentRoot.Find(
                    "BackgroundRoot");
        }

        if (propsRoot == null)
        {
            propsRoot =
                environmentRoot.Find(
                    "PropsRoot");
        }

        if (directionalLight == null)
        {
            Light[] lights =
                FindObjectsByType<Light>();

            foreach (Light light
                     in lights)
            {
                if (light != null &&
                    light.type ==
                    LightType.Directional)
                {
                    directionalLight = light;
                    break;
                }
            }
        }
    }

#if UNITY_EDITOR
    // Compatibility with Foundation v1 setup.
    public void EditorConfigure(
        Transform newEnvironmentRoot,
        Renderer newTableSurfaceRenderer,
        Renderer newTableUnderlayRenderer,
        Transform newBackgroundRoot,
        Transform newPropsRoot,
        Light newDirectionalLight,
        EnvironmentThemeProfile
            newDefaultTheme,
        EnvironmentThemeProfile[]
            newAvailableThemes)
    {
        environmentRoot =
            newEnvironmentRoot;

        tableSurfaceRenderer =
            newTableSurfaceRenderer;

        tableUnderlayRenderer =
            newTableUnderlayRenderer;

        backgroundRoot =
            newBackgroundRoot;

        propsRoot =
            newPropsRoot;

        directionalLight =
            newDirectionalLight;

        defaultTheme =
            newDefaultTheme;

        availableThemes =
            newAvailableThemes;

        activeTheme =
            newDefaultTheme;
    }

    public void EditorConfigureV11(
        Transform newEnvironmentRoot,
        Renderer newTableSurfaceRenderer,
        Renderer newTableUnderlayRenderer,
        Transform newBackgroundRoot,
        Transform newPropsRoot,
        Light newDirectionalLight,
        EnvironmentThemeProfile
            newDefaultTheme,
        EnvironmentThemeProfile[]
            newAvailableThemes)
    {
        EditorConfigure(
            newEnvironmentRoot,
            newTableSurfaceRenderer,
            newTableUnderlayRenderer,
            newBackgroundRoot,
            newPropsRoot,
            newDirectionalLight,
            newDefaultTheme,
            newAvailableThemes);
    }
#endif
}
