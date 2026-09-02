using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase5EAuthorityHotfixV101Setup
{
    [MenuItem(
        "Atlas Board/Online/Previous Phase Validators/Phase 5E - Purchase Authority + Pawn Sync v1.0.1")]
    public static void ValidatePhase5EV101()
    {
        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            Object.FindAnyObjectByType<
                AtlasBoardTurnDiceNetworkCoordinator>();

        AtlasBoardLobbyRuntimeBridge lobbyBridge =
            Object.FindAnyObjectByType<
                AtlasBoardLobbyRuntimeBridge>();

        TileResolutionManager tileResolution =
            Object.FindAnyObjectByType<
                TileResolutionManager>();

        AtlasBoardPawnCustomizationUI customizationUI =
            Object.FindAnyObjectByType<
                AtlasBoardPawnCustomizationUI>();

        bool hasLobbyCosmeticField =
            typeof(AtlasLobbyMemberSnapshot).GetField(
                "PawnCosmeticId",
                BindingFlags.Public |
                BindingFlags.Instance) != null;

        bool hasLobbyCosmeticCallable =
            typeof(AtlasBoardLobbyRuntimeBridge).GetMethod(
                "SetPawnCosmeticAsync",
                BindingFlags.Public |
                BindingFlags.Instance) != null;

        bool hasPurchaseAuthorityConfiguration =
            typeof(TileResolutionManager).GetMethod(
                "ConfigureOnlinePurchaseDecisionAuthority",
                BindingFlags.Public |
                BindingFlags.Instance) != null;

        bool valid =
            coordinator != null &&
            lobbyBridge != null &&
            tileResolution != null &&
            customizationUI != null &&
            hasLobbyCosmeticField &&
            hasLobbyCosmeticCallable &&
            hasPurchaseAuthorityConfiguration;

        if (!valid)
        {
            Debug.LogError(
                "AtlasBoard Phase 5E v1.0.1 static validation FAILED. " +
                "Lobby cosmetic snapshot/callable, purchase authority " +
                "configuration, or required scene objects are missing.");
            return;
        }

        Debug.Log(
            "AtlasBoard Phase 5E v1.0.1 Purchase Authority + Pawn Sync " +
            "Hotfix static validation PASSED. Remote-owned purchase UI can " +
            "be separated from Host authority and pawn cosmetic identity is " +
            "available in the lobby snapshot. This is BUILD/STATIC validation " +
            "only; Runtime PASS requires Host + Guest validation.");
    }
}
