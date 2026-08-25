using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TabletShellStyler : MonoBehaviour
{
    private const string FrameName =
        "__UX_TabletFrame";

    private const string ScreenName =
        "__UX_TabletScreen";

    private const string HeaderName =
        "__UX_TabletHeader";

    private const string CameraDotName =
        "__UX_TabletCameraDot";

    private const string HeaderLineName =
        "__UX_TabletHeaderLine";

    [Header("Tablet")]
    [SerializeField]
    private GameObject backdropDim;

    [SerializeField]
    private GameObject tabletRoot;

    [SerializeField]
    private TMP_Text tabletTitleText;

    [Header("Style")]
    [SerializeField]
    private Color backdropColor =
        new Color(
            0f,
            0f,
            0f,
            0.58f);

    [SerializeField]
    private Color frameColor =
        new Color(
            0.055f,
            0.065f,
            0.08f,
            1f);

    [SerializeField]
    private Color screenColor =
        new Color(
            0.025f,
            0.032f,
            0.045f,
            1f);

    [SerializeField]
    private Color headerColor =
        new Color(
            0.075f,
            0.09f,
            0.12f,
            1f);

    [ContextMenu("Apply Tablet Shell Style")]
    public void ApplyStyle()
    {
        if (tabletRoot == null)
        {
            Debug.LogWarning(
                "TabletShellStyler has no Tablet Root.",
                this);

            return;
        }

        RectTransform rootRect =
            tabletRoot
                .GetComponent<RectTransform>();

        if (rootRect == null)
        {
            Debug.LogWarning(
                "Tablet Root is not a RectTransform.",
                tabletRoot);

            return;
        }

        if (backdropDim != null)
        {
            Image backdropImage =
                backdropDim
                    .GetComponent<Image>();

            if (backdropImage != null)
            {
                backdropImage.color =
                    backdropColor;
            }
        }

        Image rootImage =
            tabletRoot
                .GetComponent<Image>();

        if (rootImage == null)
        {
            rootImage =
                tabletRoot
                    .AddComponent<Image>();
        }

        rootImage.color =
            frameColor;

        rootImage.raycastTarget =
            false;

        RemoveGeneratedChild(
            rootRect,
            FrameName);

        RemoveGeneratedChild(
            rootRect,
            ScreenName);

        RemoveGeneratedChild(
            rootRect,
            HeaderName);

        RemoveGeneratedChild(
            rootRect,
            CameraDotName);

        RemoveGeneratedChild(
            rootRect,
            HeaderLineName);

        Image frame =
            CreateStretchImage(
                FrameName,
                rootRect,
                -18f,
                frameColor);

        Outline outline =
            frame.gameObject
                .AddComponent<Outline>();

        outline.effectColor =
            new Color(
                0f,
                0f,
                0f,
                0.82f);

        outline.effectDistance =
            new Vector2(
                5f,
                -5f);

        frame.transform
            .SetSiblingIndex(0);

        Image screen =
            CreateStretchImage(
                ScreenName,
                rootRect,
                10f,
                screenColor);

        screen.transform
            .SetSiblingIndex(1);

        Image header =
            CreateTopBar(
                HeaderName,
                rootRect,
                64f,
                headerColor);

        header.transform
            .SetSiblingIndex(2);

        Image headerLine =
            CreateHeaderLine(
                HeaderLineName,
                rootRect);

        headerLine.transform
            .SetSiblingIndex(3);

        Image cameraDot =
            CreateCameraDot(
                CameraDotName,
                rootRect);

        cameraDot.transform
            .SetSiblingIndex(4);

        if (tabletTitleText != null)
        {
            tabletTitleText.fontStyle =
                FontStyles.Bold;

            tabletTitleText.fontSize =
                Mathf.Max(
                    26f,
                    tabletTitleText.fontSize);

            tabletTitleText.color =
                new Color(
                    0.94f,
                    0.96f,
                    1f,
                    1f);
        }

        Debug.Log(
            "Tablet shell style applied.",
            this);
    }

    private static Image CreateStretchImage(
        string objectName,
        RectTransform parent,
        float inset,
        Color color)
    {
        GameObject child =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        RectTransform rect =
            child.GetComponent<
                RectTransform>();

        rect.SetParent(
            parent,
            false);

        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            new Vector2(
                inset,
                inset);

        rect.offsetMax =
            new Vector2(
                -inset,
                -inset);

        Image image =
            child.GetComponent<Image>();

        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private static Image CreateTopBar(
        string objectName,
        RectTransform parent,
        float height,
        Color color)
    {
        GameObject child =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        RectTransform rect =
            child.GetComponent<
                RectTransform>();

        rect.SetParent(
            parent,
            false);

        rect.anchorMin =
            new Vector2(
                0f,
                1f);

        rect.anchorMax =
            new Vector2(
                1f,
                1f);

        rect.pivot =
            new Vector2(
                0.5f,
                1f);

        rect.offsetMin =
            new Vector2(
                12f,
                -height);

        rect.offsetMax =
            new Vector2(
                -12f,
                0f);

        Image image =
            child.GetComponent<Image>();

        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private static Image CreateHeaderLine(
        string objectName,
        RectTransform parent)
    {
        GameObject child =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        RectTransform rect =
            child.GetComponent<
                RectTransform>();

        rect.SetParent(
            parent,
            false);

        rect.anchorMin =
            new Vector2(
                0f,
                1f);

        rect.anchorMax =
            new Vector2(
                1f,
                1f);

        rect.pivot =
            new Vector2(
                0.5f,
                1f);

        rect.sizeDelta =
            new Vector2(
                -30f,
                2f);

        rect.anchoredPosition =
            new Vector2(
                0f,
                -64f);

        Image image =
            child.GetComponent<Image>();

        image.color =
            new Color(
                0.23f,
                0.35f,
                0.52f,
                0.65f);

        image.raycastTarget = false;

        return image;
    }

    private static Image CreateCameraDot(
        string objectName,
        RectTransform parent)
    {
        GameObject child =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        RectTransform rect =
            child.GetComponent<
                RectTransform>();

        rect.SetParent(
            parent,
            false);

        rect.anchorMin =
            new Vector2(
                0.5f,
                1f);

        rect.anchorMax =
            new Vector2(
                0.5f,
                1f);

        rect.pivot =
            new Vector2(
                0.5f,
                1f);

        rect.sizeDelta =
            new Vector2(
                9f,
                9f);

        rect.anchoredPosition =
            new Vector2(
                0f,
                -10f);

        Image image =
            child.GetComponent<Image>();

        image.color =
            new Color(
                0.12f,
                0.18f,
                0.27f,
                1f);

        image.raycastTarget = false;

        return image;
    }

    private static void RemoveGeneratedChild(
        RectTransform parent,
        string childName)
    {
        Transform existing =
            parent.Find(
                childName);

        if (existing == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(
                existing.gameObject);
        }
        else
        {
            DestroyImmediate(
                existing.gameObject);
        }
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        GameObject newBackdropDim,
        GameObject newTabletRoot,
        TMP_Text newTabletTitleText)
    {
        backdropDim = newBackdropDim;
        tabletRoot = newTabletRoot;
        tabletTitleText =
            newTabletTitleText;
    }
#endif
}
