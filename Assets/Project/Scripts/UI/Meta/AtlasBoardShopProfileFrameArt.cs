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
               itemId == "frame_usa" ||
               itemId == "frame_neon_circuit" ||
               itemId == "frame_royal_crown" ||
               itemId == "frame_ocean_tide" ||
               itemId == "frame_sakura_blossom" ||
               itemId == "frame_cosmic_nova" ||
               itemId == "frame_forest_spirit";
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
            case "frame_neon_circuit":
                baseColor = new Color32(11, 29, 46, 255);
                accent = new Color32(29, 242, 224, 255);
                trim = new Color32(96, 136, 255, 255);
                break;
            case "frame_royal_crown":
                baseColor = new Color32(61, 27, 88, 255);
                accent = new Color32(241, 195, 77, 255);
                trim = new Color32(241, 231, 171, 255);
                break;
            case "frame_ocean_tide":
                baseColor = new Color32(8, 64, 111, 255);
                accent = new Color32(76, 209, 237, 255);
                trim = new Color32(216, 249, 249, 255);
                break;
            case "frame_sakura_blossom":
                baseColor = new Color32(106, 40, 81, 255);
                accent = new Color32(255, 155, 192, 255);
                trim = new Color32(255, 220, 229, 255);
                break;
            case "frame_cosmic_nova":
                baseColor = new Color32(25, 20, 67, 255);
                accent = new Color32(177, 112, 255, 255);
                trim = new Color32(252, 204, 104, 255);
                break;
            case "frame_forest_spirit":
                baseColor = new Color32(20, 70, 49, 255);
                accent = new Color32(142, 219, 99, 255);
                trim = new Color32(204, 180, 103, 255);
                break;
            case "frame_usa":
                baseColor = new Color32(21, 49, 99, 255);
                accent = Color.white;
                trim = new Color32(199, 52, 61, 255);
                break;
            default:
                return null;
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
        else if (itemId == "frame_usa")
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
        else if (itemId == "frame_neon_circuit")
        {
            for (int i = 0; i < 3; i++)
            {
                Panel(root.transform, "CircuitTrace_" + i,
                    new Vector2(-size * .35f + i * size * .12f, size * .35f),
                    new Vector2(edge * .55f, edge * .55f), trim);
                Panel(root.transform, "CircuitDot_" + i,
                    new Vector2(size * .35f, -size * .35f + i * size * .12f),
                    new Vector2(edge * .50f, edge * .50f), accent);
            }
            Panel(root.transform, "CircuitLink", new Vector2(-size * .34f, 0),
                new Vector2(edge * .42f, size * .34f), accent);
        }
        else if (itemId == "frame_royal_crown")
        {
            Panel(root.transform, "CrownBand", new Vector2(0, size * .33f),
                new Vector2(size * .45f, edge * .80f), accent);
            for (int i = -1; i <= 1; i++)
            {
                Panel(root.transform, "CrownPoint_" + i,
                    new Vector2(i * size * .17f, size * .40f),
                    new Vector2(edge * .72f, size * (i == 0 ? .15f : .11f)), trim);
            }
            Panel(root.transform, "CrownGem", new Vector2(0, -size * .38f),
                new Vector2(edge * .82f, edge * .82f), trim);
        }
        else if (itemId == "frame_ocean_tide")
        {
            for (int i = 0; i < 3; i++)
            {
                Panel(root.transform, "Wave_" + i,
                    new Vector2((i - 1) * size * .28f, -size * (.36f - i * .025f)),
                    new Vector2(size * .23f, edge * .65f), trim);
            }
            Panel(root.transform, "FoamLeft", new Vector2(-size * .35f, size * .28f),
                new Vector2(edge * .75f, edge * .75f), accent);
            Panel(root.transform, "FoamRight", new Vector2(size * .34f, size * .33f),
                new Vector2(edge * .50f, edge * .50f), Color.white);
        }
        else if (itemId == "frame_sakura_blossom")
        {
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * size * .36f;
                float y = (i < 2 ? 1f : -1f) * size * .32f;
                GameObject petal = Panel(root.transform, "SakuraPetal_" + i,
                    new Vector2(x, y), new Vector2(edge * 1.35f, edge * .86f), trim);
                petal.transform.localEulerAngles = new Vector3(0, 0, 45f);
            }
            Panel(root.transform, "SakuraHeart", new Vector2(size * .36f, size * .32f),
                new Vector2(edge * .56f, edge * .56f), accent);
        }
        else if (itemId == "frame_cosmic_nova")
        {
            Label(root.transform, "Constellation", "*   *",
                new Vector2(0, size * .37f), new Vector2(size * .72f, size * .16f),
                Mathf.Max(10f, size * .14f), trim);
            Label(root.transform, "NovaSpark", "*",
                new Vector2(-size * .35f, -size * .33f),
                new Vector2(size * .25f, size * .25f),
                Mathf.Max(12f, size * .17f), accent);
            Panel(root.transform, "Orbit", new Vector2(size * .34f, -size * .34f),
                new Vector2(edge * .65f, edge * .65f), Color.white);
        }
        else if (itemId == "frame_forest_spirit")
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject leaf = Panel(root.transform, "Leaf_" + i,
                    new Vector2(-size * .34f + i * size * .34f,
                        (i == 1 ? size * .36f : -size * .36f)),
                    new Vector2(edge * .75f, edge * 1.35f), accent);
                leaf.transform.localEulerAngles = new Vector3(0, 0,
                    i == 1 ? 45f : -45f);
            }
            Panel(root.transform, "Branch", new Vector2(size * .34f, size * .13f),
                new Vector2(edge * .45f, size * .36f), trim);
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
