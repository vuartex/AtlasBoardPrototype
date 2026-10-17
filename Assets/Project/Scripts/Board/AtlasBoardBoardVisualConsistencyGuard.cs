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
        2f;

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

        int cleanedActive = 0;

        // Keep the newest active generated root. Inactive roots are normally
        // already queued for deferred Destroy() by the presenter and should
        // not inflate the duplicate counter or warning noise.
        Transform rootToKeep = null;

        for (int index =
                 generatedRoots.Count - 1;
             index >= 0;
             index--)
        {
            Transform candidate =
                generatedRoots[index];

            if (candidate != null &&
                candidate.gameObject.activeSelf)
            {
                rootToKeep = candidate;
                break;
            }
        }

        if (rootToKeep == null &&
            generatedRoots.Count > 0)
        {
            rootToKeep =
                generatedRoots[
                    generatedRoots.Count - 1];
        }

        foreach (Transform generatedRoot
                 in generatedRoots)
        {
            if (generatedRoot == null ||
                generatedRoot == rootToKeep)
            {
                continue;
            }

            if (generatedRoot.gameObject.activeSelf)
            {
                cleanedActive++;
            }

            DisableAndDestroy(generatedRoot);
        }

        // Current presentation nests label/band under __TileVisualRoot.
        // Any direct label/band is legacy generated state and can be removed.
        foreach (Transform legacy in legacyDirectVisuals)
        {
            if (legacy != null &&
                legacy.gameObject.activeSelf)
            {
                cleanedActive++;
            }

            DisableAndDestroy(legacy);
        }

        return cleanedActive;
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
