using UnityEngine;
using UnityEngine.UI;

// Reuses the imported Kenney Sprite references; never clones a 3D preview world.
public static class AtlasBoardEmotePreviewArt
{
    public static GameObject Create(Transform parent, string id, float size)
    {
        Sprite sprite = AtlasBoardEmotes.Sprite(id);
        if (parent == null || sprite == null) return null;
        GameObject obj = new GameObject("EmoteIcon_" + id, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
        rect.pivot = new Vector2(.5f,.5f);
        rect.sizeDelta = new Vector2(size,size);
        rect.anchoredPosition = Vector2.zero;
        Image image = obj.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return obj;
    }
}
