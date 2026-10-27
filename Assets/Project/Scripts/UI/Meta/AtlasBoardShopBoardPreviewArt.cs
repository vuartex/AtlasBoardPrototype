using UnityEngine;
using UnityEngine.UI;

// Original AtlasBoard procedural 2D preview of board finishes. This is never
// the physical game board, a purchased entitlement, or a host room setting.
// All three remain preview-only until the RTDB owner/host protocol is wired.
public static class AtlasBoardShopBoardPreviewArt
{
    public static bool IsSupported(string id)
    {
        return id == "board_midnight_azure" ||
               id == "board_ember_maple" ||
               id == "board_arctic_aurora";
    }

    public static GameObject Create(Transform parent, string id, float side)
    {
        if (parent == null || !IsSupported(id) || side < 24f) return null;

        Color edge;
        Color field;
        Color tileA;
        Color tileB;
        Color glow;
        switch (id)
        {
            case "board_midnight_azure":
                edge = new Color32(20, 32, 83, 255);
                field = new Color32(36, 67, 128, 255);
                tileA = new Color32(70, 105, 185, 255);
                tileB = new Color32(31, 49, 105, 255);
                glow = new Color32(161, 195, 255, 255);
                break;
            case "board_ember_maple":
                edge = new Color32(95, 39, 26, 255);
                field = new Color32(154, 89, 52, 255);
                tileA = new Color32(215, 149, 80, 255);
                tileB = new Color32(111, 58, 34, 255);
                glow = new Color32(255, 206, 113, 255);
                break;
            default:
                edge = new Color32(24, 73, 96, 255);
                field = new Color32(93, 164, 173, 255);
                tileA = new Color32(177, 226, 225, 255);
                tileB = new Color32(49, 120, 141, 255);
                glow = new Color32(222, 255, 249, 255);
                break;
        }

        GameObject root = Panel(parent, "BoardPreview_" + id,
            Vector2.zero, new Vector2(side, side), edge);
        Panel(root.transform, "PlayableSurface", Vector2.zero,
            new Vector2(side * .87f, side * .87f), field);

        // Symbolic 32 perimeter tiles, leaving the central playing surface
        // untouched. This preview never modifies 3D topology or colliders.
        float pitch = side * .098f;
        float start = -pitch * 3.5f;
        float lane = side * .415f;
        for (int i = 0; i < 8; i++)
        {
            Color color = i % 2 == 0 ? tileA : tileB;
            float x = start + pitch * i;
            Panel(root.transform, "NorthTile_" + i, new Vector2(x, lane),
                new Vector2(pitch * .92f, side * .10f), color);
            Panel(root.transform, "SouthTile_" + i, new Vector2(x, -lane),
                new Vector2(pitch * .92f, side * .10f), color);
            Panel(root.transform, "WestTile_" + i, new Vector2(-lane, x),
                new Vector2(side * .10f, pitch * .92f), color);
            Panel(root.transform, "EastTile_" + i, new Vector2(lane, x),
                new Vector2(side * .10f, pitch * .92f), color);
        }

        // A theme-specific mark stays on this store sample only.
        if (id == "board_midnight_azure")
        {
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * side * .11f;
                float y = (i % 2 == 0 ? .15f : -.10f) * side;
                Panel(root.transform, "AzureStar_" + i,
                    new Vector2(x, y),
                    new Vector2(side * .026f, side * .026f), glow);
            }
        }
        else if (id == "board_ember_maple")
        {
            for (int i = -1; i <= 1; i++)
            {
                GameObject ember = Panel(root.transform, "MapleEmber_" + i,
                    new Vector2(i * side * .15f, i * side * .11f),
                    new Vector2(side * .065f, side * .065f), glow);
                ember.transform.localEulerAngles = new Vector3(0, 0, 45f);
            }
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject aurora = Panel(root.transform, "AuroraRibbon_" + i,
                    new Vector2((i - 1) * side * .10f, (i - 1) * side * .12f),
                    new Vector2(side * .38f, side * .020f), glow);
                aurora.transform.localEulerAngles = new Vector3(0, 0, 18f);
            }
        }
        return root;
    }

    private static GameObject Panel(Transform parent, string name,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name,
            typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }
}
