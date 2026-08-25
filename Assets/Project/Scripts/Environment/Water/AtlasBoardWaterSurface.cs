using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class AtlasBoardWaterSurface : MonoBehaviour
{
    [SerializeField] private Material waterMaterial;

    public Material WaterMaterial => waterMaterial;

#if UNITY_EDITOR
    public void EditorConfigure(Material material)
    {
        waterMaterial = material;

        MeshRenderer renderer =
            GetComponent<MeshRenderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }
#endif
}
