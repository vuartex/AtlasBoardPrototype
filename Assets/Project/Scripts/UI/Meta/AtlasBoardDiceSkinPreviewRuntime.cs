using UnityEngine;
using UnityEngine.UI;

// Tied to each UI preview object, so Store close/rebuild and language switching
// release the temporary camera/texture automatically. No persistent scene copy.
public sealed class AtlasBoardDiceSkinPreviewRuntime : MonoBehaviour
{
    private GameObject stage;
    private RenderTexture target;
    private Camera previewCamera;

    public void Initialize(GameObject modelPrefab, float side, Material surface)
    {
        if (modelPrefab == null) return;
        int layer = 30;
        Vector3 center = new Vector3(3200f, 3200f, 3200f);
        stage = new GameObject("ATB_DicePreview3DStage");
        GameObject model = Object.Instantiate(modelPrefab, stage.transform, false);
        model.name = "ImportedDicePreview";
        model.transform.position = center;
        model.transform.rotation = Quaternion.Euler(18f, 32f, -10f);
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (surface != null) DiceVisualController.BindImportedSurface(renderers, surface);
        if (renderers.Length == 0)
        {
            Object.Destroy(stage);
            stage = null;
            return;
        }
        foreach (Transform transform in model.GetComponentsInChildren<Transform>(true))
            transform.gameObject.layer = layer;
        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        if (bounds.size.magnitude > .00001f)
        {
            model.transform.localScale *= Mathf.Clamp(1.6f / bounds.size.magnitude, .002f, 120f);
            Bounds scaled = renderers[0].bounds;
            foreach (Renderer renderer in renderers) scaled.Encapsulate(renderer.bounds);
            model.transform.position += center - scaled.center;
        }

        GameObject cameraObject = new GameObject("ATB_DicePreviewCamera");
        cameraObject.transform.SetParent(stage.transform, true);
        cameraObject.transform.position = center + new Vector3(0f, 0f, 4f);
        cameraObject.transform.LookAt(center);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 1.15f;
        camera.nearClipPlane = .02f;
        camera.farClipPlane = 20f;
        camera.cullingMask = 1 << layer;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(224, 235, 241, 255);
        camera.depth = -100f;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        target = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
        target.name = "ATB_DicePreviewTexture";
        target.Create();
        camera.targetTexture = target;
        previewCamera = camera;

        GameObject lightObject = new GameObject("ATB_DicePreviewLight");
        lightObject.transform.SetParent(stage.transform, false);
        lightObject.transform.position = center + new Vector3(-3f, 5f, 4f);
        lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        light.cullingMask = 1 << layer;
        light.shadows = LightShadows.None;

        GameObject raw = new GameObject("ActualImported3DModel", typeof(RectTransform), typeof(RawImage));
        raw.transform.SetParent(transform, false);
        RectTransform rect = raw.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(side * .95f, side * .95f);
        RawImage image = raw.GetComponent<RawImage>();
        image.texture = target;
        image.raycastTarget = false;
    }

    private void OnEnable()
    {
        if (previewCamera != null) previewCamera.enabled = true;
    }

    private void OnDisable()
    {
        if (previewCamera != null) previewCamera.enabled = false;
    }

    private void OnDestroy()
    {
        if (stage != null) Object.Destroy(stage);
        if (target != null)
        {
            target.Release();
            Object.Destroy(target);
        }
    }
}
