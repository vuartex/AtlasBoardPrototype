#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AtlasBoardPhase14IDevelopmentTool
{
    private static bool previewActive;
    private static float previousTimeScale = 1f;

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14I - Development Polish/Show Development Preview (Freeze Gameplay)")]
    public static void ShowPreview()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Phase 14I Development preview requires Play Mode.");
            return;
        }

        TileResolutionManager tileResolution =
            Object.FindAnyObjectByType<
                TileResolutionManager>();

        BoardPath board =
            Object.FindAnyObjectByType<
                BoardPath>();

        TurnManager turns =
            Object.FindAnyObjectByType<
                TurnManager>();

        if (tileResolution == null ||
            board == null ||
            turns == null)
        {
            Debug.LogError(
                "Phase 14I preview FAILED: TileResolutionManager, BoardPath or TurnManager missing.");
            return;
        }

        PlayerGameState player =
            FindPlayer(
                turns);

        BoardTile tile =
            FindCity(
                board);

        if (player == null ||
            tile == null)
        {
            Debug.LogError(
                "Phase 14I preview FAILED: no player or City property found.");
            return;
        }

        tileResolution
            .ShowOnlineRemoteDevelopmentDecision(
                player,
                tile);

        AtlasBoardDevelopmentPresentationPolish polish =
            Object.FindAnyObjectByType<
                AtlasBoardDevelopmentPresentationPolish>();

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
            "Phase 14I Development visual preview opened. " +
            "Gameplay is frozen; the preview does not develop the property.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14I - Development Polish/Close Development Preview & Resume")]
    public static void ClosePreview()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        TileResolutionManager tileResolution =
            Object.FindAnyObjectByType<
                TileResolutionManager>();

        tileResolution
            ?.ClearOnlineRemoteDevelopmentDecision();

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
            "Phase 14I Development preview closed and gameplay resumed.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14I - Development Polish/Validate Development Presentation")]
    public static void Validate()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Phase 14I validation requires Play Mode.");
            return;
        }

        AtlasBoardDevelopmentPresentationPolish polish =
            Object.FindAnyObjectByType<
                AtlasBoardDevelopmentPresentationPolish>();

        if (polish == null ||
            polish.TabletRoot == null ||
            polish.PolishRoot == null ||
            !polish.PolishRoot.gameObject.activeInHierarchy)
        {
            Debug.LogError(
                "Phase 14I validation FAILED: open Development preview first.");
            return;
        }

        Rect tablet =
            GetScreenRect(
                polish.TabletRoot);

        Rect root =
            GetScreenRect(
                polish.PolishRoot);

        int errors = 0;

        if (tablet.width < 400f ||
            tablet.height < 300f)
        {
            Debug.LogError(
                $"Phase 14I: TabletRoot is unexpectedly small " +
                $"({tablet.width:0.#}x{tablet.height:0.#}).");
            errors++;
        }

        if (root.width <
                tablet.width * 0.95f ||
            root.height <
                tablet.height * 0.95f)
        {
            Debug.LogError(
                $"Phase 14I: Development polish is not using TabletRoot correctly. " +
                $"Polish={root.width:0.#}x{root.height:0.#}, " +
                $"Tablet={tablet.width:0.#}x{tablet.height:0.#}.");
            errors++;
        }

        Button[] buttons =
        {
            polish.DevelopButton,
            polish.SkipButton
        };

        foreach (Button button
                 in buttons)
        {
            if (button == null)
            {
                Debug.LogError(
                    "Phase 14I: one or more authoritative action buttons are missing.");
                errors++;
                continue;
            }

            Rect rect =
                GetScreenRect(
                    button.transform as
                        RectTransform);

            if (rect.width <
                    tablet.width * 0.24f ||
                rect.height <
                    tablet.height * 0.08f)
            {
                Debug.LogError(
                    $"Phase 14I: {button.name} is too small " +
                    $"({rect.width:0.#}x{rect.height:0.#}).");
                errors++;
            }
        }

        if (errors > 0)
        {
            Debug.LogError(
                $"Phase 14I Development presentation validation FAILED with {errors} issue(s).");
            return;
        }

        Debug.Log(
            $"Phase 14I Development presentation validation PASS. " +
            $"Tablet={tablet.width:0.#}x{tablet.height:0.#}. " +
            "Development presentation is anchored to TabletUIManager.tabletRoot.");
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

    private static PlayerGameState FindPlayer(
        TurnManager turns)
    {
        if (turns == null)
        {
            return null;
        }

        for (int slot = 0;
             slot < 4;
             slot++)
        {
            PlayerGameState player =
                turns.GetPlayerStateBySlotIndex(
                    slot);

            if (player != null &&
                !player.IsBankrupt)
            {
                return player;
            }
        }

        return null;
    }

    private static BoardTile FindCity(
        BoardPath board)
    {
        if (board == null)
        {
            return null;
        }

        for (int index = 0;
             index < board.TileCount;
             index++)
        {
            BoardTile tile =
                board.GetTile(
                    index);

            if (tile != null &&
                tile.TileType ==
                    TileType.City)
            {
                return tile;
            }
        }

        return null;
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
