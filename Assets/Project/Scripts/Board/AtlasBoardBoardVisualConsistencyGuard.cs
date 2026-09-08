using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10000)]
public sealed class AtlasBoardBoardVisualConsistencyGuard :
    MonoBehaviour
{
    private const string VisualRootObjectName =
        "__TileVisualRoot";

    private const string LegacyLabelObjectName =
        "__TileLabel";

    private const string LegacyBandObjectName =
        "__GroupBand";

    private const float ScanIntervalSeconds =
        0.25f;

    private float nextScanAt;
    private int cleanedVisualCount;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeGuard()
    {
        AtlasBoardBoardVisualConsistencyGuard existing =
            FindAnyObjectByType<
                AtlasBoardBoardVisualConsistencyGuard>();

        if (existing != null)
        {
            return;
        }

        GameObject root =
            new GameObject(
                "AtlasBoardBoardVisualConsistencyGuard");

        root.AddComponent<
            AtlasBoardBoardVisualConsistencyGuard>();
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime < nextScanAt)
        {
            return;
        }

        nextScanAt =
            Time.unscaledTime +
            ScanIntervalSeconds;

        CleanDuplicateGeneratedVisuals();
    }

    private void CleanDuplicateGeneratedVisuals()
    {
        BoardTile[] tiles =
            FindObjectsByType<BoardTile>(
                FindObjectsInactive.Include);

        int cleanedThisPass = 0;

        foreach (BoardTile tile in tiles)
        {
            if (tile == null ||
                !tile.gameObject.scene.IsValid())
            {
                continue;
            }

            cleanedThisPass +=
                CleanTileVisuals(
                    tile.transform);
        }

        if (cleanedThisPass <= 0)
        {
            return;
        }

        cleanedVisualCount +=
            cleanedThisPass;

        Debug.Log(
            "AtlasBoard board visual consistency guard removed " +
            $"{cleanedThisPass} stale/duplicate tile visual(s). " +
            $"Session total: {cleanedVisualCount}.",
            this);
    }

    private static int CleanTileVisuals(
        Transform tileTransform)
    {
        if (tileTransform == null)
        {
            return 0;
        }

        List<Transform> generatedRoots =
            new List<Transform>();

        List<Transform> legacyDirectVisuals =
            new List<Transform>();

        for (int index = 0;
             index < tileTransform.childCount;
             index++)
        {
            Transform child =
                tileTransform.GetChild(index);

            if (child == null)
            {
                continue;
            }

            if (child.name ==
                VisualRootObjectName)
            {
                generatedRoots.Add(child);
                continue;
            }

            if (child.name ==
                    LegacyLabelObjectName ||
                child.name ==
                    LegacyBandObjectName)
            {
                legacyDirectVisuals.Add(child);
            }
        }

        int cleaned = 0;

        // Runtime map application can rebuild the same tile more than once in
        // a frame. Unity Destroy() is deferred, so an older implementation
        // could leave both the previous-map root and the newest-map root alive.
        // The newest generated root is appended last; keep only that root.
        for (int index = 0;
             index < generatedRoots.Count - 1;
             index++)
        {
            DisableAndDestroy(
                generatedRoots[index]);

            cleaned++;
        }

        // Current presentation nests label/band under __TileVisualRoot.
        // Any direct label/band is legacy generated state and can be removed.
        foreach (Transform legacy in legacyDirectVisuals)
        {
            DisableAndDestroy(legacy);
            cleaned++;
        }

        return cleaned;
    }

    private static void DisableAndDestroy(
        Transform target)
    {
        if (target == null)
        {
            return;
        }

        GameObject targetObject =
            target.gameObject;

        // Hide immediately; Destroy() itself completes at end-of-frame.
        targetObject.SetActive(false);
        Destroy(targetObject);
    }
}
