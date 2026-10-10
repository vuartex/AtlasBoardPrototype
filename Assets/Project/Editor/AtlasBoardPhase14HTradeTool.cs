#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase14HTradeTool
{
    private static bool previewActive;
    private static float previousTimeScale = 1f;

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14H - Trade Polish/Show Trade Setup Preview (Freeze Gameplay)")]
    public static void ShowPreview()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Phase 14H Trade preview requires Play Mode.");
            return;
        }

        TradeManager trade =
            Object.FindAnyObjectByType<
                TradeManager>();

        if (trade == null)
        {
            Debug.LogError(
                "Phase 14H preview FAILED: TradeManager not found.");
            return;
        }

        if (!trade.IsTradeClosed)
        {
            trade.CancelTrade();
        }

        trade.OpenTradePanel();

        if (trade.IsTradeClosed)
        {
            Debug.LogWarning(
                "Phase 14H preview could not open Trade. " +
                "Trade must be available before rolling and at least one valid target player must exist.");
            return;
        }

        AtlasBoardTradePresentationPolish polish =
            Object.FindAnyObjectByType<
                AtlasBoardTradePresentationPolish>();

        polish?.RefreshNow();

        if (!previewActive)
        {
            previousTimeScale =
                Time.timeScale > 0f
                    ? Time.timeScale
                    : 1f;

            Time.timeScale = 0f;
            previewActive = true;
        }

        Debug.Log(
            "Phase 14H Trade setup preview opened and gameplay frozen. " +
            "No trade offer has been submitted.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14H - Trade Polish/Close Trade Preview & Resume")]
    public static void ClosePreview()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        TradeManager trade =
            Object.FindAnyObjectByType<
                TradeManager>();

        if (trade != null &&
            !trade.IsTradeClosed)
        {
            trade.CancelTrade();
        }

        if (previewActive)
        {
            Time.timeScale =
                previousTimeScale > 0f
                    ? previousTimeScale
                    : 1f;

            previewActive = false;
            previousTimeScale = 1f;
        }

        Debug.Log(
            "Phase 14H Trade preview closed and gameplay resumed.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14H - Trade Polish/Validate Trade Presentation")]
    public static void Validate()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Phase 14H validation requires Play Mode.");
            return;
        }

        AtlasBoardTradePresentationPolish polish =
            Object.FindAnyObjectByType<
                AtlasBoardTradePresentationPolish>();

        if (polish == null ||
            polish.TabletRoot == null ||
            polish.PolishRoot == null ||
            !polish.PolishRoot.gameObject.activeInHierarchy)
        {
            Debug.LogError(
                "Phase 14H validation FAILED: open Trade preview first.");
            return;
        }

        Rect tablet =
            GetScreenRect(
                polish.TabletRoot);

        Rect root =
            GetScreenRect(
                polish.PolishRoot);

        Rect left =
            GetScreenRect(
                polish.LeftTradeCard);

        Rect right =
            GetScreenRect(
                polish.RightTradeCard);

        int errors = 0;

        if (tablet.width < 400f ||
            tablet.height < 300f)
        {
            Debug.LogError(
                $"Phase 14H: TabletRoot is unexpectedly small " +
                $"({tablet.width:0.#}x{tablet.height:0.#}).");
            errors++;
        }

        if (root.width <
                tablet.width * 0.95f ||
            root.height <
                tablet.height * 0.95f)
        {
            Debug.LogError(
                $"Phase 14H: Trade polish is not using the real TabletRoot area. " +
                $"Polish={root.width:0.#}x{root.height:0.#}, " +
                $"Tablet={tablet.width:0.#}x{tablet.height:0.#}.");
            errors++;
        }

        if (left.width <
                tablet.width * 0.34f ||
            right.width <
                tablet.width * 0.34f)
        {
            Debug.LogError(
                $"Phase 14H: Trade side cards are too narrow. " +
                $"Left={left.width:0.#}, Right={right.width:0.#}, Tablet={tablet.width:0.#}.");
            errors++;
        }

        if (left.Overlaps(
                right))
        {
            Debug.LogError(
                "Phase 14H: Offer and Request cards overlap.");
            errors++;
        }

        float middleGap =
            Mathf.Max(
                0f,
                right.xMin -
                left.xMax);

        if (middleGap <
            tablet.width * 0.04f)
        {
            Debug.LogError(
                $"Phase 14H: Offer/Request card gap is too small ({middleGap:0.#}).");
            errors++;
        }

        if (errors > 0)
        {
            Debug.LogError(
                $"Phase 14H Trade presentation validation FAILED with {errors} issue(s).");
            return;
        }

        Debug.Log(
            $"Phase 14H Trade presentation validation PASS. " +
            $"Tablet={tablet.width:0.#}x{tablet.height:0.#}, " +
            $"OfferCard={left.width:0.#}x{left.height:0.#}, " +
            $"RequestCard={right.width:0.#}x{right.height:0.#}. " +
            "Trade presentation is anchored to TabletUIManager.tabletRoot.");
    }

    [InitializeOnLoadMethod]
    private static void RegisterCleanup()
    {
        EditorApplication.playModeStateChanged -=
            HandlePlayModeStateChanged;

        EditorApplication.playModeStateChanged +=
            HandlePlayModeStateChanged;
    }

    private static void HandlePlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (state ==
            PlayModeStateChange.ExitingPlayMode)
        {
            previewActive = false;
            previousTimeScale = 1f;
        }
    }

    private static Rect GetScreenRect(
        RectTransform rect)
    {
        if (rect == null)
        {
            return default;
        }

        Vector3[] corners =
            new Vector3[4];

        rect.GetWorldCorners(
            corners);

        Canvas canvas =
            rect.GetComponentInParent<
                Canvas>();

        Canvas root =
            canvas != null
                ? canvas.rootCanvas
                : null;

        Camera camera =
            root == null ||
            root.renderMode ==
                RenderMode.ScreenSpaceOverlay
                ? null
                : root.worldCamera;

        Vector2 bottomLeft =
            RectTransformUtility.WorldToScreenPoint(
                camera,
                corners[0]);

        Vector2 topRight =
            RectTransformUtility.WorldToScreenPoint(
                camera,
                corners[2]);

        return Rect.MinMaxRect(
            Mathf.Min(
                bottomLeft.x,
                topRight.x),
            Mathf.Min(
                bottomLeft.y,
                topRight.y),
            Mathf.Max(
                bottomLeft.x,
                topRight.x),
            Mathf.Max(
                bottomLeft.y,
                topRight.y));
    }
}
#endif
