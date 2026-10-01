#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase14FMatchResultFinalTool
{
    private static bool ownsPause;
    private static float oldTimeScale = 1f;

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14F - Match Result/Show Preview (Freeze Gameplay)")]
    public static void ShowPreview()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Phase 14F Match Result preview requires Play Mode.");
            return;
        }

        MatchResultManager result =
            Object.FindAnyObjectByType<
                MatchResultManager>();

        if (result == null)
        {
            Debug.LogError(
                "Phase 14F: MatchResultManager not found.");
            return;
        }

        result.ResetForNewMatchSession();
        result.ShowMatchResult();

        AtlasBoardMatchResultFinalLayout layout =
            Object.FindAnyObjectByType<
                AtlasBoardMatchResultFinalLayout>();

        layout?.RefreshNow();

        if (!ownsPause)
        {
            oldTimeScale =
                Time.timeScale > 0f
                    ? Time.timeScale
                    : 1f;

            Time.timeScale = 0f;
            ownsPause = true;
        }

        Debug.Log(
            "Phase 14F Match Result preview opened and gameplay frozen.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14F - Match Result/Close Preview & Resume")]
    public static void ClosePreview()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        MatchResultManager result =
            Object.FindAnyObjectByType<
                MatchResultManager>();

        result?.ResetForNewMatchSession();

        if (ownsPause)
        {
            Time.timeScale =
                oldTimeScale > 0f
                    ? oldTimeScale
                    : 1f;

            ownsPause = false;
            oldTimeScale = 1f;
        }

        Debug.Log(
            "Phase 14F Match Result preview closed and gameplay resumed.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14F - Match Result/Validate FINAL Layout")]
    public static void Validate()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Phase 14F final validation requires Play Mode.");
            return;
        }

        AtlasBoardMatchResultFinalLayout layout =
            Object.FindAnyObjectByType<
                AtlasBoardMatchResultFinalLayout>();

        if (layout == null ||
            layout.TabletRoot == null ||
            layout.CardsRoot == null ||
            !layout.CardsRoot.gameObject.activeInHierarchy)
        {
            Debug.LogError(
                "Phase 14F FINAL validation FAILED: open the Result preview first.");
            return;
        }

        RectTransform tablet =
            layout.TabletRoot;

        Vector2 tabletSize =
            GetScreenSize(
                tablet);

        if (tabletSize.x < 400f ||
            tabletSize.y < 300f)
        {
            Debug.LogError(
                $"Phase 14F FINAL validation FAILED: tablet visual bounds " +
                $"are unexpectedly small ({tabletSize.x:0.#}x{tabletSize.y:0.#}).",
                tablet);
            return;
        }

        int errors = 0;
        int visibleCards = 0;

        Rect[] cardRects =
            new Rect[4];

        for (int slot = 0;
             slot < 4;
             slot++)
        {
            RectTransform card =
                layout.GetCard(
                    slot);

            if (card == null ||
                !card.gameObject.activeInHierarchy)
            {
                continue;
            }

            visibleCards++;

            cardRects[slot] =
                GetScreenRect(
                    card);

            float widthRatio =
                cardRects[slot].width /
                tabletSize.x;

            float heightRatio =
                cardRects[slot].height /
                tabletSize.y;

            if (widthRatio < 0.34f ||
                heightRatio < 0.11f)
            {
                Debug.LogError(
                    $"Phase 14F FINAL: P{slot + 1} card is too small. " +
                    $"Card={cardRects[slot].width:0.#}x{cardRects[slot].height:0.#}, " +
                    $"Tablet={tabletSize.x:0.#}x{tabletSize.y:0.#}.",
                    card);
                errors++;
            }
        }

        if (visibleCards >= 4)
        {
            if (cardRects[0].Overlaps(cardRects[1]) ||
                cardRects[0].Overlaps(cardRects[2]) ||
                cardRects[1].Overlaps(cardRects[3]) ||
                cardRects[2].Overlaps(cardRects[3]))
            {
                Debug.LogError(
                    "Phase 14F FINAL: player cards overlap in screen space.");
                errors++;
            }

            float topGap =
                Mathf.Max(
                    0f,
                    cardRects[1].xMin -
                    cardRects[0].xMax);

            float bottomGap =
                Mathf.Max(
                    0f,
                    cardRects[3].xMin -
                    cardRects[2].xMax);

            if (topGap < tabletSize.x * 0.04f ||
                bottomGap < tabletSize.x * 0.04f)
            {
                Debug.LogError(
                    $"Phase 14F FINAL: horizontal whitespace is too small. " +
                    $"TopGap={topGap:0.#}, BottomGap={bottomGap:0.#}.");
                errors++;
            }
        }

        if (visibleCards < 2)
        {
            Debug.LogError(
                $"Phase 14F FINAL: only {visibleCards} participant card(s) visible.");
            errors++;
        }

        if (errors > 0)
        {
            Debug.LogError(
                $"Phase 14F FINAL validation FAILED with {errors} issue(s).");
            return;
        }

        Debug.Log(
            $"Phase 14F FINAL validation PASS. " +
            $"Tablet={tabletSize.x:0.#}x{tabletSize.y:0.#}, " +
            $"VisibleCards={visibleCards}. " +
            "Cards are sized and spaced against the real TabletRoot.");
    }

    [InitializeOnLoadMethod]
    private static void RegisterPlayModeCleanup()
    {
        EditorApplication.playModeStateChanged -=
            OnPlayModeStateChanged;

        EditorApplication.playModeStateChanged +=
            OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(
        PlayModeStateChange state)
    {
        if (state ==
            PlayModeStateChange.ExitingPlayMode)
        {
            ownsPause = false;
            oldTimeScale = 1f;
        }
    }

    private static Vector2 GetScreenSize(
        RectTransform rect)
    {
        Rect screenRect =
            GetScreenRect(
                rect);

        return new Vector2(
            screenRect.width,
            screenRect.height);
    }

    private static Rect GetScreenRect(
        RectTransform rect)
    {
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

        Vector2 bl =
            RectTransformUtility.WorldToScreenPoint(
                camera,
                corners[0]);

        Vector2 tr =
            RectTransformUtility.WorldToScreenPoint(
                camera,
                corners[2]);

        float xMin =
            Mathf.Min(
                bl.x,
                tr.x);

        float xMax =
            Mathf.Max(
                bl.x,
                tr.x);

        float yMin =
            Mathf.Min(
                bl.y,
                tr.y);

        float yMax =
            Mathf.Max(
                bl.y,
                tr.y);

        return Rect.MinMaxRect(
            xMin,
            yMin,
            xMax,
            yMax);
    }
}
#endif
