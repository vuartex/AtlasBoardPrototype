#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardWaterV1Setup
{
    private const string MaterialFolder =
        "Assets/Project/Art/Environment/Water/Materials";

    private const string MeshFolder =
        "Assets/Project/Art/Environment/Water/Meshes";

    private const string MaterialPath =
        MaterialFolder + "/MAT_Water_Beach.mat";

    private const string MeshPath =
        MeshFolder + "/MESH_Water_Beach.asset";

    private const string ShaderName =
        "AtlasBoard/Stylized Water BuiltIn";

    [MenuItem(
        "Atlas Board/Environment/Water/Create or Refresh Water v1")]
    public static void CreateOrRefresh()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "Exit Play Mode before creating Water v1.");

            return;
        }

        EnsureFolder(MaterialFolder);
        EnsureFolder(MeshFolder);

        Shader shader =
            Shader.Find(ShaderName);

        if (shader == null)
        {
            Debug.LogError(
                $"Shader '{ShaderName}' was not found. " +
                "Check AtlasBoardStylizedWater.shader.");

            return;
        }

        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(
                MaterialPath);

        if (material == null)
        {
            material =
                new Material(shader);

            material.name =
                "MAT_Water_Beach";

            AssetDatabase.CreateAsset(
                material,
                MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        ConfigureMaterial(material);

        Mesh mesh =
            AssetDatabase.LoadAssetAtPath<Mesh>(
                MeshPath);

        Mesh generated =
            BuildGridMesh(
                18f,
                18f,
                48,
                48);

        if (mesh == null)
        {
            mesh = generated;
            mesh.name = "MESH_Water_Beach";

            AssetDatabase.CreateAsset(
                mesh,
                MeshPath);
        }
        else
        {
            EditorUtility.CopySerialized(
                generated,
                mesh);

            mesh.name = "MESH_Water_Beach";

            Object.DestroyImmediate(
                generated);
        }

        Transform beachRoot =
            EnsureBeachThemeRoot();

        Transform existing =
            beachRoot.Find(
                "WaterSurface");

        GameObject water;

        if (existing == null)
        {
            water =
                new GameObject(
                    "WaterSurface");

            Undo.RegisterCreatedObjectUndo(
                water,
                "Create AtlasBoard Water");

            water.transform.SetParent(
                beachRoot,
                false);
        }
        else
        {
            water = existing.gameObject;
        }

        water.transform.localPosition =
            Vector3.zero;

        water.transform.localRotation =
            Quaternion.identity;

        water.transform.localScale =
            Vector3.one;

        MeshFilter filter =
            GetOrAdd<MeshFilter>(
                water);

        MeshRenderer renderer =
            GetOrAdd<MeshRenderer>(
                water);

        AtlasBoardWaterSurface surface =
            GetOrAdd<AtlasBoardWaterSurface>(
                water);

        filter.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        surface.EditorConfigure(material);

        Collider collider =
            water.GetComponent<Collider>();

        if (collider != null)
        {
            Undo.DestroyObjectImmediate(
                collider);
        }

        EditorUtility.SetDirty(material);
        EditorUtility.SetDirty(mesh);
        EditorUtility.SetDirty(water);

        if (water.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                water.scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeGameObject = water;

        Debug.Log(
            "AtlasBoard Water v1 created/refreshed. " +
            "WaterSurface is under PF_Theme_Beach_Props. " +
            "Press Play to see animated waves.",
            water);
    }

    [MenuItem(
        "Atlas Board/Environment/Water/Add Bob To Selected")]
    public static void AddBobToSelected()
    {
        GameObject selected =
            Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogWarning(
                "Select a boat or floating prop first.");

            return;
        }

        if (selected.GetComponent<AtlasBoardWaterBob>() == null)
        {
            Undo.AddComponent<AtlasBoardWaterBob>(
                selected);
        }

        EditorUtility.SetDirty(selected);

        Debug.Log(
            $"Water bob added to '{selected.name}'.",
            selected);
    }

    private static void ConfigureMaterial(
        Material material)
    {
        material.SetColor(
            "_ShallowColor",
            new Color(
                0.08f,
                0.62f,
                0.74f,
                1f));

        material.SetColor(
            "_DeepColor",
            new Color(
                0.015f,
                0.16f,
                0.30f,
                1f));

        material.SetFloat("_Opacity", 0.78f);
        material.SetFloat("_Smoothness", 0.82f);
        material.SetFloat("_Metallic", 0f);

        material.SetFloat("_WaveHeight", 0.10f);
        material.SetFloat("_WaveScale", 0.80f);
        material.SetFloat("_WaveSpeed", 0.55f);

        material.SetFloat("_WaveHeight2", 0.045f);
        material.SetFloat("_WaveScale2", 1.55f);
        material.SetFloat("_WaveSpeed2", 0.38f);

        material.SetFloat("_FresnelStrength", 0.72f);
        material.SetFloat("_FresnelPower", 2.25f);
        material.SetFloat("_ColorVariation", 0.20f);
    }

    private static Mesh BuildGridMesh(
        float width,
        float length,
        int xSegments,
        int zSegments)
    {
        int vx = xSegments + 1;
        int vz = zSegments + 1;

        Vector3[] vertices =
            new Vector3[vx * vz];

        Vector3[] normals =
            new Vector3[vertices.Length];

        Vector2[] uvs =
            new Vector2[vertices.Length];

        int[] triangles =
            new int[
                xSegments *
                zSegments *
                6];

        for (int z = 0; z < vz; z++)
        {
            float z01 =
                z / (float)zSegments;

            float zp =
                Mathf.Lerp(
                    -length * 0.5f,
                    length * 0.5f,
                    z01);

            for (int x = 0; x < vx; x++)
            {
                float x01 =
                    x / (float)xSegments;

                float xp =
                    Mathf.Lerp(
                        -width * 0.5f,
                        width * 0.5f,
                        x01);

                int index =
                    z * vx + x;

                vertices[index] =
                    new Vector3(
                        xp,
                        0f,
                        zp);

                normals[index] =
                    Vector3.up;

                uvs[index] =
                    new Vector2(
                        x01,
                        z01);
            }
        }

        int ti = 0;

        for (int z = 0; z < zSegments; z++)
        {
            for (int x = 0; x < xSegments; x++)
            {
                int bl =
                    z * vx + x;

                int br = bl + 1;
                int tl = bl + vx;
                int tr = tl + 1;

                triangles[ti++] = bl;
                triangles[ti++] = tl;
                triangles[ti++] = tr;

                triangles[ti++] = bl;
                triangles[ti++] = tr;
                triangles[ti++] = br;
            }
        }

        Mesh mesh = new Mesh();

        mesh.name = "MESH_Water_Beach";
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    private static Transform EnsureBeachThemeRoot()
    {
        GameObject environment =
            GameObject.Find(
                "EnvironmentRoot");

        if (environment == null)
        {
            environment =
                new GameObject(
                    "EnvironmentRoot");

            Undo.RegisterCreatedObjectUndo(
                environment,
                "Create EnvironmentRoot");
        }

        Transform propsRoot =
            environment.transform.Find(
                "PropsRoot");

        if (propsRoot == null)
        {
            GameObject props =
                new GameObject(
                    "PropsRoot");

            Undo.RegisterCreatedObjectUndo(
                props,
                "Create PropsRoot");

            props.transform.SetParent(
                environment.transform,
                false);

            propsRoot =
                props.transform;
        }

        Transform beachRoot =
            propsRoot.Find(
                "PF_Theme_Beach_Props");

        if (beachRoot == null)
        {
            GameObject beach =
                new GameObject(
                    "PF_Theme_Beach_Props");

            Undo.RegisterCreatedObjectUndo(
                beach,
                "Create Beach Theme Root");

            beach.transform.SetParent(
                propsRoot,
                false);

            beachRoot =
                beach.transform;
        }

        return beachRoot;
    }

    private static T GetOrAdd<T>(
        GameObject target)
        where T : Component
    {
        T component =
            target.GetComponent<T>();

        if (component == null)
        {
            component =
                Undo.AddComponent<T>(
                    target);
        }

        return component;
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
