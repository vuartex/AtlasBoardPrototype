using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class BoardTileVisualPresenter : MonoBehaviour
{
    private const string VisualRootObjectName =
        "__TileVisualRoot";

    private const string LabelObjectName =
        "__TileLabel";

    private const string BandObjectName =
        "__GroupBand";

    [Header("Label")]
    [SerializeField]
    private bool showPropertyPrice = false;

    [SerializeField]
    private Color labelColor =
        new Color(
            0.03f,
            0.03f,
            0.03f,
            1f);

    [SerializeField]
    private Color labelOutlineColor =
        new Color(
            1f,
            1f,
            1f,
            0.95f);

    [SerializeField, Range(0f, 0.5f)]
    private float labelOutlineWidth = 0.12f;

    [SerializeField, Min(0.001f)]
    private float labelLift = 0.035f;

    [SerializeField]
    private float labelInwardOffset = 0.20f;

    [SerializeField, Range(0.3f, 0.99f)]
    private float labelWidthRatio = 0.96f;

    [SerializeField, Range(0.2f, 0.98f)]
    private float labelDepthRatio = 0.82f;

    [Header("Property Group Band")]
    [SerializeField, Range(0.4f, 1f)]
    private float groupBandWidthRatio = 0.92f;

    [SerializeField, Range(0.05f, 0.35f)]
    private float groupBandDepthRatio = 0.16f;

    [SerializeField, Min(0.005f)]
    private float groupBandHeight = 0.055f;

    [SerializeField, Min(0f)]
    private float groupBandOuterInset = 0.055f;

    [SerializeField, Min(0f)]
    private float groupBandLift = 0.018f;

    private BoardTile tile;
    private Renderer tileRenderer;
    private Transform visualRoot;

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;
    }

    private void OnDisable()
    {
        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;
    }

    private void HandleLanguageChanged()
    {
        RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        ResolveReferences();

        if (tile == null ||
            tileRenderer == null)
        {
            return;
        }

        // Clean both the current generated root AND legacy
        // direct children left by earlier readability versions.
        // Those old objects can otherwise render a second, blurry
        // label on top of the current one.
        RemoveGeneratedVisual(
            VisualRootObjectName);

        RemoveGeneratedVisual(
            LabelObjectName);

        RemoveGeneratedVisual(
            BandObjectName);

        CreateNeutralVisualRoot();

        CreateTileLabel();

        if (tile.TileType ==
            TileType.City)
        {
            CreateGroupBand();
        }
    }

    [ContextMenu("Clean Legacy Tile Visuals")]
    private void CleanLegacyTileVisuals()
    {
        RemoveGeneratedVisual(
            VisualRootObjectName);

        RemoveGeneratedVisual(
            LabelObjectName);

        RemoveGeneratedVisual(
            BandObjectName);

        Debug.Log(
            $"Cleaned generated tile visuals on {name}.",
            this);
    }

    [ContextMenu("Refresh Tile Visuals")]
    private void RefreshFromContextMenu()
    {
        RefreshVisuals();
    }

    private void ResolveReferences()
    {
        if (tile == null)
        {
            tile =
                GetComponent<BoardTile>();
        }

        if (tileRenderer == null)
        {
            tileRenderer =
                GetComponent<Renderer>();
        }
    }

    private void CreateNeutralVisualRoot()
    {
        GameObject rootObject =
            new GameObject(
                VisualRootObjectName);

        visualRoot =
            rootObject.transform;

        visualRoot.SetParent(
            transform,
            false);

        visualRoot.localPosition =
            Vector3.zero;

        visualRoot.localRotation =
            Quaternion.identity;

        // BoardTile cubes are intentionally non-uniformly scaled
        // (wide X/Z, very thin Y). A neutral root cancels that scale
        // BEFORE label/band rotation, preventing distorted/tiny TMP.
        visualRoot.localScale =
            InverseLossyScale(
                transform.lossyScale);
    }

    private void CreateTileLabel()
    {
        GameObject canvasObject =
            new GameObject(
                LabelObjectName,
                typeof(RectTransform),
                typeof(Canvas));

        RectTransform canvasRect =
            canvasObject
                .GetComponent<RectTransform>();

        canvasRect.SetParent(
            visualRoot,
            false);

        Canvas canvas =
            canvasObject
                .GetComponent<Canvas>();

        canvas.renderMode =
            RenderMode.WorldSpace;

        canvas.overrideSorting = true;
        canvas.sortingOrder = 50;

        GameObject textObject =
            new GameObject(
                "Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        RectTransform textRect =
            textObject
                .GetComponent<RectTransform>();

        textRect.SetParent(
            canvasRect,
            false);

        textRect.anchorMin =
            Vector2.zero;

        textRect.anchorMax =
            Vector2.one;

        textRect.offsetMin =
            Vector2.zero;

        textRect.offsetMax =
            Vector2.zero;

        TextMeshProUGUI text =
            textObject
                .GetComponent<TextMeshProUGUI>();

        TMP_FontAsset resolvedFont =
            TMP_Settings.defaultFontAsset;

        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;

        if (localization != null)
        {
            resolvedFont =
                localization.ResolveFont(
                    resolvedFont);
        }

        if (resolvedFont != null)
        {
            text.font =
                resolvedFont;
        }

        text.text =
            BuildLabelText();

        text.color =
            labelColor;

        text.alignment =
            TextAlignmentOptions.Center;

        text.enableAutoSizing = true;
        text.fontSize = 110f;
        text.fontSizeMax = 110f;
        text.fontSizeMin = 42f;

        text.fontStyle =
            FontStyles.Bold;

        text.textWrappingMode =
            TextWrappingModes.Normal;

        text.richText = true;
        text.lineSpacing = -12f;
        text.characterSpacing = 0f;

        text.outlineColor =
            labelOutlineColor;

        text.outlineWidth =
            labelOutlineWidth;

        text.margin =
            new Vector4(
                8f,
                5f,
                8f,
                5f);

        Vector3 outward =
            GetOutwardDirection(
                tile.TileIndex);

        Vector3 inward =
            -outward;

        bool horizontalSide =
            IsHorizontalSide(
                tile.TileIndex);

        float worldWidth =
            horizontalSide
                ? tileRenderer.bounds.size.x
                : tileRenderer.bounds.size.z;

        float worldDepth =
            horizontalSide
                ? tileRenderer.bounds.size.z
                : tileRenderer.bounds.size.x;

        float targetWorldWidth =
            worldWidth *
            labelWidthRatio;

        float targetWorldDepth =
            worldDepth *
            labelDepthRatio;

        // Match the internal Canvas aspect ratio to the ACTUAL
        // tile label area. Earlier 700x360 labels behaved like a
        // horizontal strip even on deep portrait-style properties.
        const float canvasPixelWidth = 700f;

        float safeWorldWidth =
            Mathf.Max(
                0.01f,
                targetWorldWidth);

        float canvasPixelHeight =
            canvasPixelWidth *
            (targetWorldDepth /
             safeWorldWidth);

        canvasPixelHeight =
            Mathf.Clamp(
                canvasPixelHeight,
                420f,
                1400f);

        canvasRect.sizeDelta =
            new Vector2(
                canvasPixelWidth,
                canvasPixelHeight);

        float uniformWorldScale =
            targetWorldWidth /
            canvasPixelWidth;

        canvasRect.localScale =
            Vector3.one *
            uniformWorldScale;

        Vector3 worldPosition =
            tileRenderer.bounds.center +
            inward *
            labelInwardOffset;

        worldPosition.y =
            tileRenderer.bounds.max.y +
            labelLift;

        canvasRect.position =
            worldPosition;

        // Unity UI is viewed from the Canvas FRONT side, which is
        // opposite local +Z. Therefore local +Z must point DOWN so
        // the visible text face points UP toward the board camera.
        // This fixes the mirror/back-face appearance.
        // Local +Y still points toward the board center, giving
        // clean 0/90/180/270 tabletop orientation on all four sides.
        canvasRect.rotation =
            Quaternion.LookRotation(
                Vector3.down,
                inward);
    }

    private string BuildLabelText()
    {
        if (tile == null)
        {
            return string.Empty;
        }

        string readableName =
            FormatTileName(
                tile.BoardDisplayName,
                tile.TileType);

        if (tile.TileType ==
            TileType.City)
        {
            if (!showPropertyPrice)
            {
                return readableName;
            }

            return
                $"{readableName}\n" +
                $"<size=62%>₵{tile.PurchasePrice}</size>";
        }

        return readableName;
    }

    private string FormatTileName(
        string rawName,
        TileType type)
    {
        if (string.IsNullOrWhiteSpace(
                rawName))
        {
            return string.Empty;
        }

        string normalized =
            type == TileType.City
                ? rawName.Trim()
                : AtlasBoardL.TileName(
                    type,
                    rawName)
                    .Trim();

        // For long board-only labels, split near the middle at a
        // whitespace so the font stays large instead of shrinking.
        if (normalized.Length <= 12 ||
            !normalized.Contains(" "))
        {
            return normalized;
        }

        string[] words =
            normalized.Split(' ');

        if (words.Length == 2)
        {
            return
                $"{words[0]}\n{words[1]}";
        }

        int midpoint =
            normalized.Length / 2;

        int bestBreak = -1;
        int bestDistance =
            int.MaxValue;

        for (int index = 0;
             index < normalized.Length;
             index++)
        {
            if (normalized[index] != ' ')
            {
                continue;
            }

            int distance =
                Mathf.Abs(
                    index - midpoint);

            if (distance <
                bestDistance)
            {
                bestDistance =
                    distance;
                bestBreak =
                    index;
            }
        }

        if (bestBreak > 0)
        {
            return
                normalized.Substring(
                    0,
                    bestBreak) +
                "\n" +
                normalized.Substring(
                    bestBreak + 1);
        }

        return normalized;
    }

    private void CreateGroupBand()
    {
        Color color =
            ResolveGroupColor();

        GameObject band =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube);

        band.name =
            BandObjectName;

        band.transform.SetParent(
            visualRoot,
            false);

        Collider bandCollider =
            band.GetComponent<Collider>();

        if (bandCollider != null)
        {
            DestroyGeneratedObject(
                bandCollider);
        }

        Renderer bandRenderer =
            band.GetComponent<Renderer>();

        if (bandRenderer == null)
        {
            return;
        }

        // Reuse the tile's already-working URP material/shader,
        // then override only the visible band color.
        if (tileRenderer.sharedMaterial != null)
        {
            bandRenderer.sharedMaterial =
                tileRenderer.sharedMaterial;
        }

        MaterialPropertyBlock propertyBlock =
            new MaterialPropertyBlock();

        bandRenderer.GetPropertyBlock(
            propertyBlock);

        propertyBlock.SetColor(
            "_BaseColor",
            color);

        propertyBlock.SetColor(
            "_Color",
            color);

        bandRenderer.SetPropertyBlock(
            propertyBlock);

        bool horizontalSide =
            IsHorizontalSide(
                tile.TileIndex);

        float worldTileWidth =
            horizontalSide
                ? tileRenderer.bounds.size.x
                : tileRenderer.bounds.size.z;

        float worldTileDepth =
            horizontalSide
                ? tileRenderer.bounds.size.z
                : tileRenderer.bounds.size.x;

        float bandWidth =
            worldTileWidth *
            groupBandWidthRatio;

        float bandDepth =
            worldTileDepth *
            groupBandDepthRatio;

        Vector3 desiredWorldScale =
            horizontalSide
                ? new Vector3(
                    bandWidth,
                    groupBandHeight,
                    bandDepth)
                : new Vector3(
                    bandDepth,
                    groupBandHeight,
                    bandWidth);

        band.transform.localScale =
            desiredWorldScale;

        Vector3 outward =
            GetOutwardDirection(
                tile.TileIndex);

        float halfTileDepth =
            worldTileDepth *
            0.5f;

        Vector3 worldPosition =
            tileRenderer.bounds.center +
            outward *
            (halfTileDepth -
             groupBandOuterInset -
             bandDepth * 0.5f);

        worldPosition.y =
            tileRenderer.bounds.max.y +
            groupBandLift +
            groupBandHeight * 0.5f;

        band.transform.position =
            worldPosition;

        band.transform.rotation =
            Quaternion.identity;
    }

    private Color ResolveGroupColor()
    {
        if (tile != null &&
            tile.GroupColor.a > 0.01f)
        {
            return tile.GroupColor;
        }

        // Safe fallback for old map assets that have not yet
        // received explicit data-driven group colors.
        return GetFallbackGroupColor(
            tile != null
                ? tile.GroupId
                : string.Empty);
    }

    private static Color GetFallbackGroupColor(
        string groupId)
    {
        int groupNumber = 0;

        if (!string.IsNullOrWhiteSpace(
                groupId))
        {
            string digits =
                groupId.Replace(
                    "group_",
                    string.Empty);

            int.TryParse(
                digits,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out groupNumber);
        }

        return groupNumber switch
        {
            1 => new Color(
                0.48f, 0.25f, 0.15f, 1f),
            2 => new Color(
                0.35f, 0.74f, 0.94f, 1f),
            3 => new Color(
                0.84f, 0.24f, 0.62f, 1f),
            4 => new Color(
                0.95f, 0.45f, 0.10f, 1f),
            5 => new Color(
                0.90f, 0.14f, 0.15f, 1f),
            6 => new Color(
                0.96f, 0.84f, 0.10f, 1f),
            7 => new Color(
                0.14f, 0.64f, 0.29f, 1f),
            8 => new Color(
                0.08f, 0.29f, 0.82f, 1f),
            _ => new Color(
                0.45f, 0.45f, 0.45f, 1f)
        };
    }

    private static bool IsHorizontalSide(
        int tileIndex)
    {
        return tileIndex <= 8 ||
               (tileIndex >= 16 &&
                tileIndex <= 24);
    }

    private static Vector3 GetOutwardDirection(
        int tileIndex)
    {
        if (tileIndex <= 8)
        {
            return Vector3.back;
        }

        if (tileIndex <= 16)
        {
            return Vector3.right;
        }

        if (tileIndex <= 24)
        {
            return Vector3.forward;
        }

        return Vector3.left;
    }

    private static Vector3 InverseLossyScale(
        Vector3 lossyScale)
    {
        return new Vector3(
            SafeInverse(
                lossyScale.x),
            SafeInverse(
                lossyScale.y),
            SafeInverse(
                lossyScale.z));
    }

    private static float SafeInverse(
        float value)
    {
        return Mathf.Abs(value) >
               0.0001f
            ? 1f / value
            : 1f;
    }

    private void RemoveGeneratedVisual(
        string childName)
    {
        Transform child =
            transform.Find(
                childName);

        if (child == null)
        {
            return;
        }

        if (childName ==
            VisualRootObjectName)
        {
            visualRoot = null;
        }

        DestroyGeneratedObject(
            child.gameObject);
    }

    private static void DestroyGeneratedObject(
        Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
