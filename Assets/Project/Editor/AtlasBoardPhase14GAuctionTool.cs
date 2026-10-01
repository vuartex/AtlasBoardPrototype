#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class AtlasBoardPhase14GAuctionTool
{
    private static bool previewActive;
    private static float previousTimeScale = 1f;

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14G - Auction Polish/Show Auction Preview (Freeze Gameplay)")]
    public static void ShowPreview()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Phase 14G Auction preview requires Play Mode.");
            return;
        }

        AuctionManager auction =
            Object.FindAnyObjectByType<
                AuctionManager>();

        BoardPath board =
            Object.FindAnyObjectByType<
                BoardPath>();

        TurnManager turns =
            Object.FindAnyObjectByType<
                TurnManager>();

        if (auction == null ||
            board == null ||
            turns == null)
        {
            Debug.LogError(
                "Phase 14G preview FAILED: AuctionManager, BoardPath, or TurnManager missing.");
            return;
        }

        BoardTile property =
            FindPreviewProperty(
                board);

        PlayerGameState bidder =
            FindPreviewPlayer(
                turns,
                0);

        PlayerGameState leader =
            FindPreviewPlayer(
                turns,
                1);

        if (property == null ||
            bidder == null)
        {
            Debug.LogError(
                "Phase 14G preview FAILED: no City property or player available.");
            return;
        }

        int minimumBid = 10;
        int currentBid =
            Mathf.Max(
                minimumBid,
                Mathf.Min(
                    150,
                    Mathf.Max(
                        minimumBid,
                        bidder.CurrentMoney / 5)));

        auction.ShowOnlineRemoteAuctionState(
            property,
            bidder,
            leader,
            currentBid,
            minimumBid,
            10,
            50);

        AtlasBoardAuctionPresentationPolish polish =
            Object.FindAnyObjectByType<
                AtlasBoardAuctionPresentationPolish>();

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
            "Phase 14G Auction visual preview opened. Gameplay is frozen; " +
            "this preview does not submit a bid or mutate property ownership.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14G - Auction Polish/Close Auction Preview & Resume")]
    public static void ClosePreview()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        AuctionManager auction =
            Object.FindAnyObjectByType<
                AuctionManager>();

        auction?.ClearOnlineRemoteAuctionState();

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
            "Phase 14G Auction preview closed and gameplay resumed.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14G - Auction Polish/Validate Auction Presentation")]
    public static void Validate()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Phase 14G validation requires Play Mode.");
            return;
        }

        AtlasBoardAuctionPresentationPolish polish =
            Object.FindAnyObjectByType<
                AtlasBoardAuctionPresentationPolish>();

        if (polish == null ||
            polish.TabletRoot == null ||
            polish.PolishRoot == null ||
            !polish.PolishRoot.gameObject.activeInHierarchy)
        {
            Debug.LogError(
                "Phase 14G validation FAILED: open Auction Preview first.");
            return;
        }

        Rect tabletRect =
            GetScreenRect(
                polish.TabletRoot);

        Rect polishRect =
            GetScreenRect(
                polish.PolishRoot);

        int errors = 0;

        if (tabletRect.width < 400f ||
            tabletRect.height < 300f)
        {
            Debug.LogError(
                $"Phase 14G: TabletRoot is unexpectedly small " +
                $"({tabletRect.width:0.#}x{tabletRect.height:0.#}).");
            errors++;
        }

        if (polishRect.width <
                tabletRect.width * 0.95f ||
            polishRect.height <
                tabletRect.height * 0.95f)
        {
            Debug.LogError(
                $"Phase 14G: polish root is not using the real TabletRoot area. " +
                $"Polish={polishRect.width:0.#}x{polishRect.height:0.#}, " +
                $"Tablet={tabletRect.width:0.#}x{tabletRect.height:0.#}.");
            errors++;
        }

        Button[] buttons =
        {
            polish.SmallBidButton,
            polish.LargeBidButton,
            polish.PassButton
        };

        foreach (Button button
                 in buttons)
        {
            if (button == null)
            {
                Debug.LogError(
                    "Phase 14G: one or more polished action buttons are missing.");
                errors++;
                continue;
            }

            Rect rect =
                GetScreenRect(
                    button.transform as
                        RectTransform);

            if (rect.width <
                    tabletRect.width * 0.20f ||
                rect.height <
                    tabletRect.height * 0.08f)
            {
                Debug.LogError(
                    $"Phase 14G: {button.name} is too small " +
                    $"({rect.width:0.#}x{rect.height:0.#}).",
                    button);
                errors++;
            }
        }

        if (errors > 0)
        {
            Debug.LogError(
                $"Phase 14G Auction presentation validation FAILED with {errors} issue(s).");
            return;
        }

        Debug.Log(
            $"Phase 14G Auction presentation validation PASS. " +
            $"Tablet={tabletRect.width:0.#}x{tabletRect.height:0.#}. " +
            "Auction polish is anchored to the real TabletRoot.");
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

    private static BoardTile FindPreviewProperty(
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

    private static PlayerGameState FindPreviewPlayer(
        TurnManager turns,
        int preferredSlot)
    {
        if (turns == null)
        {
            return null;
        }

        PlayerGameState preferred =
            turns.GetPlayerStateBySlotIndex(
                preferredSlot);

        if (preferred != null)
        {
            return preferred;
        }

        for (int slot = 0;
             slot < 4;
             slot++)
        {
            PlayerGameState player =
                turns.GetPlayerStateBySlotIndex(
                    slot);

            if (player != null)
            {
                return player;
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
