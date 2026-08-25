using UnityEngine;

[CreateAssetMenu(
    fileName = "Theme_New",
    menuName = "Atlas Board/Environment/Theme Profile")]
public class EnvironmentThemeProfile : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string themeId = "classic_table";

    [SerializeField]
    private string displayName = "Classic Table";

    [SerializeField, TextArea(2, 4)]
    private string description =
        "Atlas Board environment theme.";

    [Header("Table Materials")]
    [SerializeField]
    private Material tableSurfaceMaterial;

    [SerializeField]
    private Material tableUnderlayMaterial;

    [Header("Skybox")]
    [Tooltip(
        "Panoramic Skybox material for this theme. " +
        "Can be empty for a theme with no skybox override.")]
    [SerializeField]
    private Material skyboxMaterial;

    [Header("Scene Theme Roots")]
    [Tooltip(
        "Child object name under PropsRoot. Only this theme's " +
        "scene prop root is enabled when the theme is active.")]
    [SerializeField]
    private string scenePropsRootName;

    [Tooltip(
        "Optional child object name under BackgroundRoot. " +
        "Useful later for non-HDRI background geometry.")]
    [SerializeField]
    private string sceneBackgroundRootName;

    [Header("Optional Prefab Content (Legacy / Future)")]
    [SerializeField]
    private GameObject backgroundPrefab;

    [SerializeField]
    private GameObject propsPrefab;

    [Header("Lighting Override")]
    [SerializeField]
    private bool overrideDirectionalLight;

    [SerializeField]
    private Color directionalLightColor =
        Color.white;

    [SerializeField, Min(0f)]
    private float directionalLightIntensity = 1f;

    [SerializeField]
    private Vector3 directionalLightEuler =
        new Vector3(50f, -35f, 0f);

    [Header("Ambient Override")]
    [SerializeField]
    private bool overrideAmbientLight;

    [SerializeField]
    private Color ambientLightColor =
        new Color(
            0.55f,
            0.55f,
            0.55f,
            1f);

    public string ThemeId => themeId;
    public string DisplayName => displayName;
    public string Description => description;

    public Material TableSurfaceMaterial =>
        tableSurfaceMaterial;

    public Material TableUnderlayMaterial =>
        tableUnderlayMaterial;

    public Material SkyboxMaterial =>
        skyboxMaterial;

    public string ScenePropsRootName =>
        scenePropsRootName;

    public string SceneBackgroundRootName =>
        sceneBackgroundRootName;

    public GameObject BackgroundPrefab =>
        backgroundPrefab;

    public GameObject PropsPrefab =>
        propsPrefab;

    public bool OverrideDirectionalLight =>
        overrideDirectionalLight;

    public Color DirectionalLightColor =>
        directionalLightColor;

    public float DirectionalLightIntensity =>
        directionalLightIntensity;

    public Vector3 DirectionalLightEuler =>
        directionalLightEuler;

    public bool OverrideAmbientLight =>
        overrideAmbientLight;

    public Color AmbientLightColor =>
        ambientLightColor;

#if UNITY_EDITOR
    // Compatibility with Environment Theme Foundation v1.
    public void EditorConfigureClassicTable(
        Material surfaceMaterial,
        Material underlayMaterial,
        Light directionalLight)
    {
        themeId = "classic_table";
        displayName = "Classic Table";
        description =
            "Current Atlas Board tabletop baseline.";

        tableSurfaceMaterial =
            surfaceMaterial;

        tableUnderlayMaterial =
            underlayMaterial;

        if (directionalLight != null)
        {
            overrideDirectionalLight = true;
            directionalLightColor =
                directionalLight.color;

            directionalLightIntensity =
                directionalLight.intensity;

            directionalLightEuler =
                directionalLight
                    .transform
                    .eulerAngles;
        }
    }

    public void EditorConfigureV11(
        string newThemeId,
        string newDisplayName,
        string newDescription,
        Material surfaceMaterial,
        Material underlayMaterial,
        Material newSkyboxMaterial,
        string newScenePropsRootName,
        string newSceneBackgroundRootName,
        Light sourceDirectionalLight)
    {
        themeId = newThemeId;
        displayName = newDisplayName;
        description = newDescription;

        tableSurfaceMaterial =
            surfaceMaterial;

        tableUnderlayMaterial =
            underlayMaterial;

        skyboxMaterial =
            newSkyboxMaterial;

        scenePropsRootName =
            newScenePropsRootName;

        sceneBackgroundRootName =
            newSceneBackgroundRootName;

        backgroundPrefab = null;
        propsPrefab = null;

        if (sourceDirectionalLight != null)
        {
            overrideDirectionalLight = true;
            directionalLightColor =
                sourceDirectionalLight.color;

            directionalLightIntensity =
                sourceDirectionalLight.intensity;

            directionalLightEuler =
                sourceDirectionalLight
                    .transform
                    .eulerAngles;
        }
        else
        {
            overrideDirectionalLight = false;
        }

        overrideAmbientLight = false;
    }
#endif
}
