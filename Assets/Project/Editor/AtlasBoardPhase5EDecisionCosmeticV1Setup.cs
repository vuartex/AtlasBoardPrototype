using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase5EDecisionCosmeticV1Setup
{
    [MenuItem(
        "Atlas Board/Online/Previous Phase Validators/Phase 5E - Remote Purchase + Pawn Cosmetic v1.0")]
    public static void ValidatePhase5EV10()
    {
        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            Object.FindAnyObjectByType<
                AtlasBoardTurnDiceNetworkCoordinator>();

        AtlasBoardMatchRuntimeBridge matchBridge =
            Object.FindAnyObjectByType<
                AtlasBoardMatchRuntimeBridge>();

        TileResolutionManager tileResolution =
            Object.FindAnyObjectByType<
                TileResolutionManager>();

        AtlasBoardPawnCosmeticService cosmeticService =
            Object.FindAnyObjectByType<
                AtlasBoardPawnCosmeticService>();

        PawnCosmeticApplier[] appliers =
            Object.FindObjectsByType<
                PawnCosmeticApplier>();

        bool valid =
            coordinator != null &&
            matchBridge != null &&
            tileResolution != null &&
            cosmeticService != null &&
            cosmeticService.Catalog != null &&
            appliers != null &&
            appliers.Count(
                item => item != null) >= 2;

        if (!valid)
        {
            Debug.LogError(
                "AtlasBoard Phase 5E v1.0 static validation FAILED. " +
                "Coordinator, MatchRuntimeBridge, TileResolutionManager, " +
                "PawnCosmeticService/catalog, or pawn cosmetic appliers are missing.");
            return;
        }

        Debug.Log(
            "AtlasBoard Phase 5E v1.0 Remote Purchase + Pawn Cosmetic Sync " +
            "static validation PASSED. Required decision/network/cosmetic " +
            "objects are present. This is BUILD/STATIC validation only; " +
            "Runtime PASS requires the two-client Guest BUY/SKIP and pawn " +
            "identity synchronization test.");
    }
}
