#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Additive editor authoring for the EXISTING pawn catalog.
/// This does not build/replace the lobby modal, change existing Kenney
/// definitions, install a second pawn manager or change board movement.
/// </summary>
public static class AtlasBoardPhase19TPawnAssetExtension
{
    private const string CatalogPath =
        "Assets/Project/Data/Players/PawnCosmetics/PawnCosmeticCatalog_Default.asset";
    private const string DataRoot =
        "Assets/Project/Data/Players/PawnCosmetics";

    private struct Candidate
    {
        public string Id, Label, PrefabPath;
        public float Height, Footprint;
        public Vector3 Rotation;

        public Candidate(string id, string label, string prefab,
            float height, float footprint, Vector3 rotation)
        {
            Id = id; Label = label; PrefabPath = prefab;
            Height = height; Footprint = footprint; Rotation = rotation;
        }
    }

    [MenuItem("Atlas Board/Design/Board & Presentation/Pawns/Extend Existing Catalog - Ghosts and Cars")]
    public static void ExtendExistingCatalog()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("ATB_PAWN_IMPORT exit Play Mode before editing pawn assets.");
            return;
        }

        PawnCosmeticCatalog catalog =
            AssetDatabase.LoadAssetAtPath<PawnCosmeticCatalog>(CatalogPath);
        if (catalog == null || catalog.Count < 8)
        {
            Debug.LogError("ATB_PAWN_IMPORT stopped: existing 8-pawn catalog is missing. " +
                "Do not reconstruct it or replace the existing lobby. " + CatalogPath);
            return;
        }

        List<PawnCosmeticDefinition> preserved =
            new List<PawnCosmeticDefinition>(catalog.Cosmetics);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PawnCosmeticDefinition existing in preserved)
            if (existing != null && !string.IsNullOrWhiteSpace(existing.CosmeticId))
                ids.Add(existing.CosmeticId);

        int added = 0, refreshed = 0, missing = 0;
        foreach (Candidate candidate in GetCandidates())
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(candidate.PrefabPath);
            if (model == null)
            {
                missing++;
                Debug.LogWarning("ATB_PAWN_IMPORT missing prefab: " + candidate.PrefabPath);
                continue;
            }

            string definitionPath = DataRoot + "/Pawn_ATB_" + candidate.Id + ".asset";
            PawnCosmeticDefinition definition =
                AssetDatabase.LoadAssetAtPath<PawnCosmeticDefinition>(definitionPath);
            if (definition == null)
            {
                // A previous manual import may already have placed the same ID
                // somewhere else. Reuse it rather than producing duplicate IDs.
                foreach (PawnCosmeticDefinition existing in preserved)
                {
                    if (existing != null && string.Equals(existing.CosmeticId,
                        candidate.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        definition = existing;
                        break;
                    }
                }
            }
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<PawnCosmeticDefinition>();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            definition.EditorConfigure(candidate.Id, candidate.Label, model,
                candidate.Height, candidate.Rotation);
            definition.EditorSetMaxWorldFootprint(candidate.Footprint);
            // Ghosts float via PawnMotionAnimator; cars drive without leg-walk
            // animation. Do not bind Kenney character motion clips to them.
            definition.EditorSetDefaultMotionSet(null);
            if (candidate.Id.StartsWith("vehicle_", StringComparison.Ordinal))
                definition.EditorSetOverrideMaterial(EnsureCarMaterial());
            else
                definition.EditorSetOverrideMaterial(null);
            EditorUtility.SetDirty(definition);

            if (ids.Add(candidate.Id))
            {
                preserved.Add(definition);
                added++;
            }
            else
                refreshed++;
        }

        if (added > 0)
        {
            // Preserve exact original ordering, slot defaults, prefab refs and
            // Unity asset GUIDs for all the older pawns.
            catalog.EditorReplace(preserved);
            EditorUtility.SetDirty(catalog);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("ATB_PAWN_IMPORT status=" + (missing == 0 ? "ready" : "partial") +
            " original_preserved=true existing=" + (catalog.Count - added) +
            " appended=" + added + " existing_new=" + refreshed +
            " missing=" + missing + " catalog_total=" + catalog.Count +
            " popup=existing_only scene_components_added=0");
    }

    // Repair only AtlasBoard's own material, not third-party prefabs.
    // Standard is deliberately chosen because the owner-verified Dice material
    // renders with this pipeline; the imported car shader rendered magenta.
    private const string CarMaterialPath =
        "Assets/Project/Materials/ATB_PawnCar_ColorPalette_Standard.mat";
    private const string CarTexturePath =
        "Assets/Pack_FREE_Cars/Materials/Textures/ColorPalette.png";

    private static Material EnsureCarMaterial()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CarTexturePath);
        if (texture == null)
        {
            Debug.LogError("ATB_PAWN_MATERIAL missing palette " + CarTexturePath);
            return null;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Project/Materials"))
            AssetDatabase.CreateFolder("Assets/Project", "Materials");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(CarMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null || !shader.isSupported)
                shader = Shader.Find("Unlit/Texture");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("ATB_PAWN_MATERIAL missing supported shader");
                return null;
            }
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, CarMaterialPath);
        }
        else if (material.shader == null || !material.shader.isSupported)
        {
            Shader shader = Shader.Find("Standard");
            if (shader != null && shader.isSupported) material.shader = shader;
        }
        material.color = Color.white;
        material.mainTexture = texture;
        EditorUtility.SetDirty(material);
        Debug.Log("ATB_PAWN_MATERIAL status=ready texture=true shader=" +
            material.shader.name + " supported=" + material.shader.isSupported);
        return material;
    }

    private static IEnumerable<Candidate> GetCandidates()
    {
        // Phase 19V OWNER scale request: relative to 19U definitions,
        // multiply BOTH desired height and footprint caps by the same factor.
        // Ghost = x1.30 (1.32/0.53 -> 1.716/0.689).
        // Vehicle = x1.70 (0.95/0.56 -> 1.615/0.952).
        // Do not touch the original Kenney eight, their motion, formation,
        // or the lobby's player-seat ownership checks.
        const string ghostRoot = "Assets/Little_GhostLP(FREE)/Prefabs/";
        for (int i = 1; i <= 7; ++i)
        {
            string index = i.ToString("00");
            yield return new Candidate("ghost_" + index,
                "Little Ghost " + i,
                ghostRoot + "LittleGhost_LP (" + i + ").prefab",
                1.716f, 0.689f, Vector3.zero);
        }
        const string carRoot = "Assets/Pack_FREE_Cars/Prefabs/";
        yield return new Candidate("vehicle_police", "Police Car",
            carRoot + "Police.prefab", 1.615f, 0.952f, Vector3.zero);
        yield return new Candidate("vehicle_taxi", "Taxi",
            carRoot + "Taxi.prefab", 1.615f, 0.952f, Vector3.zero);
        yield return new Candidate("vehicle_pickup", "Pickup",
            carRoot + "Pickup.prefab", 1.615f, 0.952f, Vector3.zero);
    }
}
#endif
