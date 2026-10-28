using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-shot non-rotating 3D preview inside the EXISTING Store preview card.
/// Does not duplicate Pawn Customization, spawn gameplay pawns or use new UI overlays.
/// </summary>
public static class AtlasBoardPawnShopPreviewArt
{
    public static bool IsSupported(string id) => AtlasBoardRtdbMetaEconomy.IsPaidPawn(id);

    public static void Create(Transform parent, string cosmeticId, float size)
    {
        if (parent == null || !IsSupported(cosmeticId)) return;
        // Keep exactly one pawn preview in this existing UI container.
        // Destroy() is deferred to the end of the frame: disable immediately,
        // and dispose its offscreen render stage before spawning its replacement.
        for (int i = parent.childCount - 1; i >= 0; --i)
        {
            Transform child = parent.GetChild(i);
            AtlasBoardPawnShopPreviewDriver previous =
                child.GetComponent<AtlasBoardPawnShopPreviewDriver>();
            if (previous == null) continue;
            previous.DisposePreview();
            child.gameObject.SetActive(false);
            Object.Destroy(child.gameObject);
        }
        GameObject go = new GameObject("PawnShopPreview_" + cosmeticId,
            typeof(RectTransform), typeof(RawImage), typeof(AtlasBoardPawnShopPreviewDriver));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
        AtlasBoardPawnShopPreviewDriver driver = go.GetComponent<AtlasBoardPawnShopPreviewDriver>();
        driver.Setup(cosmeticId);
    }
}

[DisallowMultipleComponent]
public sealed class AtlasBoardPawnShopPreviewDriver : MonoBehaviour
{
    private string pawnId;
    private RenderTexture target;
    private GameObject stage;
    private Camera previewCamera;
    private bool rendered;
    private bool disposed;
    // Cameras must never see a previously previewed pawn, even when a
    // hidden Store panel and a Career panel coexist during one frame.
    // The original implementation put EVERY camera/model at (0,-8700,0).
    private static int nextStageSlot;

    public void Setup(string id)
    {
        pawnId = id;
        Render();
    }

    private void Start()
    {
        if (!rendered) Render();
    }

    private void Render()
    {
        if (rendered || disposed) return;
        AtlasBoardPawnCosmeticService service = AtlasBoardPawnCosmeticService.Instance;
        PawnCosmeticDefinition cosmetic = service != null && service.Catalog != null
            ? service.Catalog.FindById(pawnId) : null;
        if (cosmetic == null || cosmetic.Prefab == null) return;

        stage = new GameObject("PawnShopPreviewStage");
        stage.hideFlags = HideFlags.HideAndDontSave;
        int stageSlot = nextStageSlot++ & 4095;
        stage.transform.position = new Vector3(stageSlot * 64f, -8700f, 0f);
        GameObject model = Instantiate(cosmetic.Prefab, stage.transform, false);
        model.transform.localRotation = Quaternion.Euler(cosmetic.RotationOffset + new Vector3(0f, 135f, 0f));
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        foreach (Animator a in model.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
        { body.isKinematic = true; body.useGravity = false; }
        if (cosmetic.OverrideMaterial != null)
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = cosmetic.OverrideMaterial;
                r.sharedMaterials = slots;
            }

        Renderer[] meshes = model.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(model.transform.position, Vector3.one * 0.01f);
        bool found = false;
        foreach (Renderer r in meshes)
        {
            if (r == null) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        if (!found) { Destroy(stage); stage = null; return; }
        float scale = Mathf.Min(1.9f / Mathf.Max(0.001f, bounds.size.y),
            2.3f / Mathf.Max(0.001f, bounds.size.x, bounds.size.z));
        model.transform.localScale *= scale;
        bounds = new Bounds(model.transform.position, Vector3.one * 0.01f);
        found = false;
        foreach (Renderer r in meshes)
        {
            if (r == null) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        model.transform.position += stage.transform.position + new Vector3(0f, 0.9f, 0f) - bounds.center;

        GameObject cam = new GameObject("PreviewCamera");
        cam.transform.SetParent(stage.transform, false);
        cam.transform.localPosition = new Vector3(0f, 1.25f, -4.0f);
        cam.transform.LookAt(stage.transform.position + new Vector3(0f, 0.9f, 0f));
        previewCamera = cam.AddComponent<Camera>();
        previewCamera.enabled = false;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.06f, 0.09f, 0.13f, 1f);
        previewCamera.fieldOfView = 35f;
        previewCamera.nearClipPlane = 0.05f;
        previewCamera.farClipPlane = 20f;
        target = new RenderTexture(384, 384, 16, RenderTextureFormat.ARGB32);
        target.Create();
        previewCamera.targetTexture = target;
        GameObject lightObject = new GameObject("PreviewLight");
        lightObject.transform.SetParent(stage.transform, false);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.6f;
        lightObject.transform.localRotation = Quaternion.Euler(40f, -35f, 0f);
        previewCamera.Render();
        GetComponent<RawImage>().texture = target;
        rendered = true;
    }

    public void DisposePreview()
    {
        if (disposed) return;
        disposed = true;
        rendered = false;
        // Prevent one-frame overlap while Unity defers destruction.
        RawImage image = GetComponent<RawImage>();
        if (image != null) image.texture = null;
        if (previewCamera != null)
        {
            previewCamera.enabled = false;
            previewCamera.targetTexture = null;
        }
        if (target != null)
        {
            target.Release();
            Destroy(target);
            target = null;
        }
        if (stage != null)
        {
            stage.SetActive(false);
            Destroy(stage);
            stage = null;
        }
    }

    private void OnDestroy() => DisposePreview();
}
