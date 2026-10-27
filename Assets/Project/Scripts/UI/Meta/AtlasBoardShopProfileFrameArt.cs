using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AtlasBoard original procedural UI artwork. No third-party images, fonts,
// textures or Unity Asset Store binaries are bundled with this renderer.
// Visual preview only: this component never equips an item or grants ownership.
public static class AtlasBoardShopProfileFrameArt
{
    public static bool IsSupported(string itemId)
    {
        return itemId == "frame_halloween" ||
               itemId == "frame_new_year" ||
               itemId == "frame_turkiye" ||
               itemId == "frame_usa";
    }

    public static GameObject Create(
        Transform parent, string itemId, float size, string initial)
    {
        if (!IsSupported(itemId) || parent == null)
            return null;

        Color baseColor;
        Color accent;
        Color trim;
        switch (itemId)
        {
            case "frame_halloween":
                baseColor = new Color32(48, 26, 77, 255);
                accent = new Color32(241, 122, 36, 255);
                trim = new Color32(222, 180, 95, 255);
                break;
            case "frame_new_year":
                baseColor = new Color32(16, 53, 93, 255);
                accent = new Color32(129, 231, 242, 255);
                trim = new Color32(238, 204, 111, 255);
                break;
            case "frame_turkiye":
                baseColor = new Color32(188, 21, 45, 255);
                accent = Color.white;
                trim = new Color32(232, 192, 99, 255);
                break;
            default:
                baseColor = new Color32(21, 49, 99, 255);
                accent = Color.white;
                trim = new Color32(199, 52, 61, 255);
                break;
        }

        GameObject root = Panel(parent, "FrameArt_" + itemId,
            Vector2.zero, new Vector2(size, size), baseColor);
        float edge = Mathf.Max(4f, size * .085f);
        // Inset border, corners and badge are all Canvas geometry.
        Panel(root.transform, "OuterTop", new Vector2(0, size * .46f),
            new Vector2(size, edge), trim);
        Panel(root.transform, "OuterBottom", new Vector2(0, -size * .46f),
            new Vector2(size, edge), trim);
        Panel(root.transform, "OuterLeft", new Vector2(-size * .46f, 0),
            new Vector2(edge, size), accent);
        Panel(root.transform, "OuterRight", new Vector2(size * .46f, 0),
            new Vector2(edge, size), accent);
        Panel(root.transform, "AvatarInset", Vector2.zero,
            new Vector2(size * .69f, size * .69f),
            new Color32(27, 41, 62, 255));
        string letter = string.IsNullOrWhiteSpace(initial) ? "A" :
            initial.Trim().Substring(0, 1).ToUpperInvariant();
        Label(root.transform, "SampleInitial", letter, Vector2.zero,
            new Vector2(size * .55f, size * .50f),
            Mathf.Clamp(size * .32f, 17f, 74f), Color.white);

        if (itemId == "frame_halloween")
        {
            Panel(root.transform, "PumpkinLeft",
                new Vector2(-size * .34f, size * .30f),
                new Vector2(edge * 1.6f, edge * 1.6f), accent);
            Panel(root.transform, "PumpkinRight",
                new Vector2(size * .34f, -size * .30f),
                new Vector2(edge * 1.6f, edge * 1.6f), accent);
            Label(root.transform, "NightBadge", "*",
                new Vector2(size * .33f, size * .29f),
                new Vector2(size * .20f, size * .20f),
                Mathf.Max(10f, size * .13f), trim);
        }
        else if (itemId == "frame_new_year")
        {
            Label(root.transform, "Stars", "*  *  *",
                new Vector2(0, size * .37f),
                new Vector2(size * .65f, size * .16f),
                Mathf.Max(8f, size * .085f), Color.white);
            Label(root.transform, "Celebration", "*",
                new Vector2(-size * .33f, -size * .31f),
                new Vector2(size * .18f, size * .18f),
                Mathf.Max(10f, size * .12f), trim);
        }
        else if (itemId == "frame_turkiye")
        {
            Label(root.transform, "FlagSymbol", "TR",
                new Vector2(0, size * .36f),
                new Vector2(size * .53f, size * .16f),
                Mathf.Max(9f, size * .09f), Color.white);
            Panel(root.transform, "NationalRedLine",
                new Vector2(0, -size * .37f),
                new Vector2(size * .62f, edge * .6f),
                new Color32(226, 34, 52, 255));
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                Panel(root.transform, "USAStripe_" + i,
                    new Vector2(size * .19f, size * (.30f - i * .085f)),
                    new Vector2(size * .31f, edge * .40f), trim);
            }
            Label(root.transform, "Stars", "*",
                new Vector2(-size * .33f, size * .29f),
                new Vector2(size * .2f, size * .2f),
                Mathf.Max(11f, size * .12f), Color.white);
        }
        return root;
    }

    private static GameObject Panel(
        Transform parent, string name, Vector2 pos,
        Vector2 size, Color color)
    {
        GameObject o = new GameObject(name,
            typeof(RectTransform), typeof(Image));
        o.transform.SetParent(parent, false);
        RectTransform rect = o.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Image image = o.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return o;
    }

    private static void Label(Transform parent, string name,
        string value, Vector2 pos, Vector2 size,
        float fontSize, Color color)
    {
        GameObject o = new GameObject(name,
            typeof(RectTransform), typeof(TextMeshProUGUI));
        o.transform.SetParent(parent, false);
        RectTransform rect = o.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        TMP_Text label = o.GetComponent<TMP_Text>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = color;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(7f, fontSize * .65f);
        label.fontSizeMax = fontSize;
        label.raycastTarget = false;
        label.text = value;
    }
}
