#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Non-gameplay fixture: previews EXACTLY the Travel layout used at runtime.
/// No match creation, dice RNG, TileResolutionManager mutation or paid backend.
/// </summary>
public static class AtlasBoardTravelEditorVisualQA
{
    private const string RootName = "ATB_QA_TRAVEL_VISUAL_PREVIEW";

    [MenuItem("Atlas Board/QA & Diagnostics/QA/Travel/1 - Preview Modern Travel (Safe)")]
    private static void Preview()
    {
        Clear();
        GameObject canvasRoot = new GameObject(RootName,
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasRoot.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject tablet = new GameObject("Tablet_Wide_Fixture",
            typeof(RectTransform), typeof(Image));
        tablet.transform.SetParent(canvasRoot.transform, false);
        RectTransform stage = tablet.GetComponent<RectTransform>();
        stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
        stage.pivot = new Vector2(0.5f, 0.5f);
        stage.sizeDelta = new Vector2(1040f, 680f);
        stage.anchoredPosition = Vector2.zero;
        tablet.GetComponent<Image>().color = new Color32(6, 17, 25, 255);

        RectTransform travel = AtlasBoardTravelPresentationPolish.EditorCreatePreview(stage);
        Canvas.ForceUpdateCanvases();
        float ratio = travel.rect.width / Mathf.Max(1f, stage.rect.width);
        bool correctParent = travel.parent == stage;
        if (ratio < 0.75f || !correctParent)
            Debug.LogError("ATB_TRAVEL_LAYOUT_QA_FAIL widthRatio=" + ratio.ToString("F2") +
                " correctParent=" + correctParent + " expected >=0.75 and wide tablet root");
        else
            Debug.Log("ATB_TRAVEL_LAYOUT_QA_PREVIEW_READY widthRatio=" + ratio.ToString("F2") +
                " source=shared_runtime_builder gameplay_mutated=false");
        Selection.activeGameObject = canvasRoot;
        Debug.Log("AtlasBoard Travel visual QA: open Game view. Close preview using Travel > 2 - Close Preview.");
    }

    [MenuItem("Atlas Board/QA & Diagnostics/QA/Travel/2 - Close Preview")]
    private static void ClosePreview() => Clear();

    private static void Clear()
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject go in objects)
        {
            if (go != null && go.name == RootName && go.transform.parent == null)
                Object.DestroyImmediate(go);
        }
    }
}
#endif
