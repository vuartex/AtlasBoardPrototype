#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Repair imported FBX references on the EXISTING DiceVisualController.
/// The generated ID for an FBX GameObject must come from Unity's importer;
/// a manually written 100100000 is not a reliable imported GameObject ID.
/// This code is Editor-only. Builds use serialized scene references.
/// </summary>
[InitializeOnLoad]
public static class AtlasBoardPhase19NDiceModelBinding
{
    private const string ScenePath = "Assets/Board_Prototype.unity";
    private const string ModelsPath = "Assets/SubstanceAssets/LittleGamesPack/Models/GamePieces/";
    private const string GamePiecesMaterialPath =
        "Assets/SubstanceAssets/LittleGamesPack/Materials/GamePieces.mat";
    private const string RenderMaterialFolder = "Assets/Project/Materials";
    private const string RenderMaterialPath =
        "Assets/Project/Materials/ATB_Dice_GamePieces_URP_Unlit.mat";
    // The package shader generated a magenta material in the owner project.
    // Use our serialized source shader instead; do not resolve by name.
    private const string DiceAtlasShaderPath =
        "Assets/Project/Shaders/AtlasBoardDiceAtlasUnlit.shader";
    private static readonly string[] Properties =
    {
        "classicDiceModel", "rubyDiceModel", "oceanDiceModel", "midnightDiceModel", "marbleDiceModel"
    };
    private static readonly string[] ModelFiles =
    {
        "Dice_White.fbx", "Dice_Red.fbx", "Dice_Blue.fbx", "Dice_Black.fbx", "Dice_Marble.fbx"
    };
    private static bool repairing;

    static AtlasBoardPhase19NDiceModelBinding()
    {
        EditorApplication.delayCall += RepairLoadedSceneReferences;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == ScenePath)
            EditorApplication.delayCall += RepairLoadedSceneReferences;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            RepairLoadedSceneReferences();
    }

    [MenuItem("Atlas Board/QA & Diagnostics/Repair Imported Dice Model References")]
    public static void RepairLoadedSceneReferences()
    {
        if (repairing || EditorApplication.isPlaying) return;
        repairing = true;
        try
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.path == ScenePath)
                    RepairScene(scene);
            }
        }
        finally { repairing = false; }
    }

    // Serialize a material referencing our own URP-compatible source shader.
    // The previous package Unlit material itself rendered magenta in Inspector.
    // The asset references the original game's imported atlas texture.
    private static Material EnsurePersistentUrpDiceMaterial(Material source)
    {
        if (source == null) return null;
        Texture albedo = source.HasProperty("_BaseMap")
            ? source.GetTexture("_BaseMap") : null;
        if (albedo == null && source.HasProperty("_MainTex"))
            albedo = source.GetTexture("_MainTex");
        if (albedo == null) return null;

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(DiceAtlasShaderPath);
        if (shader == null || !shader.isSupported)
        {
            Debug.LogError("ATB_DICE_RENDER_MATERIAL_BIND status=shader_asset_unavailable" +
                " path=" + DiceAtlasShaderPath + " action=inspect_shader_compile_errors");
            return null;
        }

        Material surface = AssetDatabase.LoadAssetAtPath<Material>(RenderMaterialPath);
        bool changed = false;
        if (surface == null)
        {
            if (!AssetDatabase.IsValidFolder(RenderMaterialFolder))
                AssetDatabase.CreateFolder("Assets/Project", "Materials");
            surface = new Material(shader);
            surface.name = "ATB_Dice_GamePieces_URP_Unlit";
            surface.SetTexture("_BaseMap", albedo);
            surface.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(surface, RenderMaterialPath);
            changed = true;
        }
        if (surface.shader != shader)
        {
            surface.shader = shader;
            changed = true;
        }
        if (surface.HasProperty("_BaseMap") && surface.GetTexture("_BaseMap") != albedo)
        {
            surface.SetTexture("_BaseMap", albedo);
            changed = true;
        }
        if (surface.HasProperty("_BaseMap") &&
            surface.GetTextureScale("_BaseMap") != Vector2.one)
        {
            surface.SetTextureScale("_BaseMap", Vector2.one);
            changed = true;
        }
        if (surface.HasProperty("_BaseMap") &&
            surface.GetTextureOffset("_BaseMap") != Vector2.zero)
        {
            surface.SetTextureOffset("_BaseMap", Vector2.zero);
            changed = true;
        }
        if (surface.HasProperty("_BaseColor") && surface.GetColor("_BaseColor") != Color.white)
        {
            surface.SetColor("_BaseColor", Color.white);
            changed = true;
        }
        if (changed)
        {
            EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssets();
            Debug.Log("ATB_DICE_RENDER_MATERIAL_BIND status=asset_ready shader=" +
                shader.name + " albedo=true path=" + RenderMaterialPath +
                " phase=19Q action=inspect_material_sphere_before_play");
        }
        return surface;
    }

    private static void RepairScene(Scene scene)
    {
        // The owner's other unsaved modifications must never be auto-saved.
        bool dirtyBefore = scene.isDirty;
        int changed = 0;
        int missing = 0;
        int controllers = 0;
        var modelIds = new List<string>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (DiceVisualController dice in root.GetComponentsInChildren<DiceVisualController>(true))
            {
                if (dice == null) continue;
                controllers++;
                var serialized = new SerializedObject(dice);
                serialized.Update();
                bool touched = false;
                for (int k = 0; k < Properties.Length; k++)
                {
                    SerializedProperty property = serialized.FindProperty(Properties[k]);
                    if (property == null)
                    {
                        Debug.LogError("ATB_DICE_MODEL_BIND status=missing_property property=" + Properties[k], dice);
                        missing++;
                        continue;
                    }
                    if (property.objectReferenceValue != null) continue; // Preserve custom assets.
                    string path = ModelsPath + ModelFiles[k];
                    GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (imported == null)
                    {
                        Debug.LogWarning("ATB_DICE_MODEL_BIND status=asset_missing path=" + path, dice);
                        missing++;
                        continue;
                    }
                    // Unity supplies the actual model root GameObject and its fileID.
                    property.objectReferenceValue = imported;
                    string guid;
                    long fileId;
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(imported, out guid, out fileId))
                        modelIds.Add(Properties[k] + ":" + guid + "/" + fileId);
                    changed++;
                    touched = true;
                }
                // Fix the missing texture atlas, not just the FBX references.
                // GamePieces.mat already references the proper 4K dice UV atlas.
                SerializedProperty atlasProperty = serialized.FindProperty("importedDiceAtlasMaterial");
                if (atlasProperty != null && atlasProperty.objectReferenceValue == null)
                {
                    Material atlas = AssetDatabase.LoadAssetAtPath<Material>(GamePiecesMaterialPath);
                    if (atlas != null)
                    {
                        atlasProperty.objectReferenceValue = atlas;
                        changed++;
                        touched = true;
                        Debug.Log("ATB_DICE_MATERIAL_BIND status=resolved asset=" +
                                  GamePiecesMaterialPath, dice);
                    }
                    else
                    {
                        missing++;
                        Debug.LogWarning("ATB_DICE_MATERIAL_BIND status=asset_missing path=" +
                                         GamePiecesMaterialPath, dice);
                    }
                }
                // Preserve the PBR pack's imported atlas, and replace the broken
                // URP package shader on the ONE EXISTING material asset.
                // No FBX reimport, temporary runtime shader or extra manager.
                Material source = atlasProperty != null
                    ? atlasProperty.objectReferenceValue as Material : null;
                Material rendered = EnsurePersistentUrpDiceMaterial(source);
                SerializedProperty renderedProperty = serialized.FindProperty("importedDiceRenderMaterial");
                if (renderedProperty != null && rendered != null &&
                    renderedProperty.objectReferenceValue != rendered)
                {
                    renderedProperty.objectReferenceValue = rendered;
                    changed++;
                    touched = true;
                    Debug.Log("ATB_DICE_RENDER_MATERIAL_BIND status=resolved shader=" +
                              rendered.shader.name + " savedAsset=" + RenderMaterialPath, dice);
                }
                else if (renderedProperty == null)
                {
                    missing++;
                    Debug.LogError("ATB_DICE_RENDER_MATERIAL_BIND status=missing_property", dice);
                }
                else if (rendered == null)
                {
                    missing++;
                    Debug.LogWarning("ATB_DICE_RENDER_MATERIAL_BIND status=missing_compatible_material" +
                        " action=verify_URP_and_GamePieces_atlas", dice);
                }
                if (touched)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(dice);
                }
            }
        }
        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            // Save automatically only when the scene had no pre-existing edits.
            bool saved = !dirtyBefore && !EditorApplication.isPlayingOrWillChangePlaymode &&
                         EditorSceneManager.SaveScene(scene);
            Debug.Log("ATB_DICE_MODEL_BIND status=resolved components=" + controllers +
                      " references=" + changed + " saved=" + saved +
                      " models=" + string.Join(",", modelIds.ToArray()) +
                      (saved ? "" : " action=save_scene_CtrlS"));
        }
        else if (controllers > 0 && missing == 0)
        {
            Debug.Log("ATB_DICE_MODEL_BIND status=already_valid components=" + controllers);
        }
        if (controllers == 0)
            Debug.LogWarning("ATB_DICE_MODEL_BIND status=no_dice_controller scene=" + ScenePath);
        if (missing > 0)
            Debug.LogWarning("ATB_DICE_MODEL_BIND status=incomplete missing=" + missing +
                             " action=verify_PBR_Little_Games_Pack_import");
    }
}
#endif
