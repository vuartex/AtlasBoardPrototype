using System;
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
        ApplyClassicBoardPalette(theme);
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
    private void ApplyClassicBoardPalette(
        EnvironmentThemeProfile theme)
    {
        if (theme == null)
        {
            return;
        }

        // The board itself is intentionally theme-invariant. Theme changes
        // may alter skybox/props/table environment, but not the physical game
        // board's walnut edge or warm center playing surface.
        Color centerColor =
            new Color32(214, 209, 184, 255);
        Color edgeColor =
            new Color32(74, 56, 42, 255);

        Renderer boardBaseRenderer =
            FindBoardBaseRenderer();

        if (boardBaseRenderer == null)
        {
            Debug.LogWarning(
                "EnvironmentThemeManager could not find BoardBase. " +
                "Board-only palette was not applied.",
                this);
            return;
        }

        SetRendererColor(
            boardBaseRenderer,
            edgeColor);

        Renderer centerRenderer =
            FindBoardCenterRenderer(
                boardBaseRenderer);

        if (centerRenderer != null)
        {
            SetRendererColor(
                centerRenderer,
                centerColor);
        }

        Debug.Log(
            "EnvironmentThemeManager board-only palette applied. " +
            $"Theme={theme.ThemeId}, " +
            $"Edge={boardBaseRenderer.gameObject.name}, " +
            $"Center={(centerRenderer != null ? centerRenderer.gameObject.name : "<not found>")}.",
            this);
    }

    private static Renderer FindBoardBaseRenderer()
    {
        Renderer[] renderers =
            Resources.FindObjectsOfTypeAll<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null ||
                !renderer.gameObject.scene.IsValid())
            {
                continue;
            }

            string objectName =
                renderer.gameObject.name;
            string materialName =
                GetCombinedMaterialName(renderer);

            if (string.Equals(
                    objectName,
                    "BoardBase",
                    StringComparison.OrdinalIgnoreCase) ||
                materialName.Contains(
                    "m_boardbase"))
            {
                return renderer;
            }
        }

        return null;
    }

    private static Renderer FindBoardCenterRenderer(
        Renderer boardBaseRenderer)
    {
        if (boardBaseRenderer == null)
        {
            return null;
        }

        Bounds boardBounds =
            boardBaseRenderer.bounds;
        float boardFootprint =
            Mathf.Max(
                0.01f,
                boardBounds.size.x *
                boardBounds.size.z);

        Renderer best = null;
        float bestScore = float.MinValue;

        Renderer[] renderers =
            Resources.FindObjectsOfTypeAll<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null ||
                renderer == boardBaseRenderer ||
                !renderer.gameObject.scene.IsValid() ||
                IsTileOrPawnRenderer(renderer))
            {
                continue;
            }

            Bounds candidate =
                renderer.bounds;

            if (!IsInsideBoardFootprint(
                    candidate,
                    boardBounds))
            {
                continue;
            }

            float candidateFootprint =
                candidate.size.x *
                candidate.size.z;
            float footprintRatio =
                candidateFootprint /
                boardFootprint;

            // Center surface should cover a meaningful fraction of the board,
            // but should not be a larger surrounding table/environment plane.
            if (footprintRatio < 0.20f ||
                footprintRatio > 0.92f)
            {
                continue;
            }

            float yDistance =
                Mathf.Abs(
                    candidate.max.y -
                    boardBounds.max.y);

            if (yDistance >
                Mathf.Max(
                    1.25f,
                    boardBounds.size.y * 2.5f))
            {
                continue;
            }

            string objectName =
                renderer.gameObject.name
                    .ToLowerInvariant();
            string materialName =
                GetCombinedMaterialName(renderer);

            float score =
                footprintRatio * 10f -
                yDistance;

            if (ContainsAny(
                    objectName,
                    "boardcenter",
                    "board_center",
                    "centerboard",
                    "centersurface",
                    "boardsurface",
                    "playingsurface",
                    "playsurface") ||
                ContainsAny(
                    materialName,
                    "boardcenter",
                    "boardsurface",
                    "playingsurface",
                    "playsurface",
                    "felt"))
            {
                score += 25f;
            }

            if (TryGetRepresentativeColor(
                    renderer,
                    out Color currentColor) &&
                currentColor.g >
                    currentColor.r + 0.05f &&
                currentColor.g >
                    currentColor.b + 0.05f)
            {
                score += 8f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = renderer;
            }
        }

        return best;
    }

    private static bool IsInsideBoardFootprint(
        Bounds candidate,
        Bounds board)
    {
        const float tolerance = 0.35f;

        return candidate.min.x >=
                   board.min.x - tolerance &&
               candidate.max.x <=
                   board.max.x + tolerance &&
               candidate.min.z >=
                   board.min.z - tolerance &&
               candidate.max.z <=
                   board.max.z + tolerance;
    }

    private static bool IsTileOrPawnRenderer(
        Renderer renderer)
    {
        if (renderer == null)
        {
            return true;
        }

        Transform current =
            renderer.transform;

        while (current != null)
        {
            string name =
                current.name
                    .ToLowerInvariant();

            if (ContainsAny(
                    name,
                    "tile_",
                    "tile ",
                    "boardtile",
                    "pawn",
                    "playerpawn",
                    "building",
                    "house",
                    "hotel"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static string GetCombinedMaterialName(
        Renderer renderer)
    {
        if (renderer == null ||
            renderer.sharedMaterials == null)
        {
            return string.Empty;
        }

        string combined =
            string.Empty;

        foreach (Material material in renderer.sharedMaterials)
        {
            if (material == null)
            {
                continue;
            }

            if (combined.Length > 0)
            {
                combined += "|";
            }

            combined += material.name
                .ToLowerInvariant();
        }

        return combined;
    }

    private static bool ContainsAny(
        string source,
        params string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(source) ||
            keywords == null)
        {
            return false;
        }

        foreach (string keyword in keywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword) &&
                source.Contains(keyword))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetRepresentativeColor(
        Renderer renderer,
        out Color color)
    {
        color = Color.black;

        if (renderer == null ||
            renderer.sharedMaterials == null)
        {
            return false;
        }

        foreach (Material material in renderer.sharedMaterials)
        {
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty("_BaseColor"))
            {
                color = material.GetColor("_BaseColor");
                return true;
            }

            if (material.HasProperty("_Color"))
            {
                color = material.GetColor("_Color");
                return true;
            }
        }

        return false;
    }

    private static void SetRendererColor(
        Renderer target,
        Color color)
    {
        if (target == null)
        {
            return;
        }

        Material[] materials =
            target.materials;

        if (materials == null ||
            materials.Length == 0)
        {
            return;
        }

        foreach (Material material in materials)
        {
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }
    }

}
