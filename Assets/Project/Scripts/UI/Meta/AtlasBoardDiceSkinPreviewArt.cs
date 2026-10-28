using UnityEngine;
using UnityEngine.UI;

// Lightweight, inspectable Store/Career die samples. Actual licensed FBX
// meshes are assigned to DiceVisualController in Board_Prototype.unity and
// rendered during rolls. This UI art has NO authority over item ownership.
public static class AtlasBoardDiceSkinPreviewArt
{
    public static bool IsSupported(string id) =>
        id == "default_dice" || AtlasBoardRtdbMetaEconomy.IsDice(id);

    public static GameObject Create(Transform parent, string id, float side)
    {
        if (parent == null || !IsSupported(id) || side < 36f) return null;
        Color face, edge, pip;
        switch (id)
        {
            case "dice_ruby_red":
                face = new Color32(200, 43, 49, 255);
                edge = new Color32(106, 23, 35, 255);
                pip = Color.white;
                break;
            case "dice_ocean_blue":
                face = new Color32(35, 114, 210, 255);
                edge = new Color32(14, 52, 102, 255);
                pip = Color.white;
                break;
            case "dice_midnight_black":
                face = new Color32(38, 39, 45, 255);
                edge = new Color32(18, 19, 25, 255);
                pip = Color.white;
                break;
            case "dice_marble":
                face = new Color32(188, 193, 194, 255);
                edge = new Color32(99, 106, 115, 255);
                pip = new Color32(26, 31, 35, 255);
                break;
            default:
                face = new Color32(241, 240, 234, 255);
                edge = new Color32(180, 183, 183, 255);
                pip = new Color32(26, 27, 30, 255);
                break;
        }
        GameObject root = Panel(parent, "DicePreview_" + id,
            Vector2.zero, new Vector2(side, side), Color.clear);
        // The offset dark silhouette makes the sample legible at 80 and 250 px.
        Panel(root.transform, "DiceShadow", new Vector2(side * .06f, -side * .07f),
            new Vector2(side * .69f, side * .69f), edge);
        Panel(root.transform, "DiceFace", Vector2.zero,
            new Vector2(side * .71f, side * .71f), face);
        float d = side * .20f;
        float dot = side * .087f;
        for (int i = 0; i < 5; i++)
        {
            float x = i == 4 ? 0f : (i % 2 == 0 ? -d : d);
            float y = i == 4 ? 0f : (i < 2 ? d : -d);
            Panel(root.transform, "Pip_" + i, new Vector2(x, y),
                new Vector2(dot, dot), pip);
        }
        // For larger inspect/Inventory previews, overlay a live 3D render of
        // the *actual* imported FBX. Thumbnails retain lightweight 2D art.
        if (side >= 150f)
        {
            DiceVisualController dice = Object.FindAnyObjectByType<DiceVisualController>(
                FindObjectsInactive.Include);
            GameObject prefab = dice != null ? dice.GetModelForPreview(id) : null;
            // Classic uses the original pipped model in matches, not the
            // imported White FBX. Keep its 2D inventory sample authoritative.
            // If the atlas cannot load, retain the legible 2D preview.
            Material surface = dice != null && id != "default_dice"
                ? dice.GetImportedDiceSurfaceMaterial() : null;
            if (prefab != null && surface != null)
                root.AddComponent<AtlasBoardDiceSkinPreviewRuntime>().Initialize(
                    prefab, side, surface);
        }
        return root;
    }

    private static GameObject Panel(Transform parent, string name,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return go;
    }
}

