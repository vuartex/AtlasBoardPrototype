#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPawnMovementNetworkV1Setup
{
    [MenuItem(
        "Atlas Board/Online/Previous Phase Validators/Phase 5C - Pawn Movement + Position Sync v1.0",
        false,
        540)]
    public static void ValidatePawnMovementNetworkV10()
    {
        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            FindSceneComponent<
                AtlasBoardTurnDiceNetworkCoordinator>();

        TurnManager turnManager =
            FindSceneComponent<TurnManager>();

        PlayerPawnMover[] pawns =
            FindSceneComponents<PlayerPawnMover>();

        bool hasMovementStartedEvent =
            typeof(PlayerPawnMover)
                .GetEvent("MovementStarted") != null;

        bool hasMovementEndedEvent =
            typeof(PlayerPawnMover)
                .GetEvent("MovementEnded") != null;

        bool hasFollowerMovementMethod =
            typeof(PlayerPawnMover)
                .GetMethod(
                    "PlayOnlineFollowerMovement") != null;

        bool hasFollowerSyncMethod =
            typeof(PlayerPawnMover)
                .GetMethod(
                    "SyncOnlineFollowerTileIndex") != null;

        int validPawnCount = 0;

        foreach (PlayerPawnMover pawn in pawns)
        {
            if (pawn != null &&
                pawn.GetComponent<PlayerGameState>() != null)
            {
                validPawnCount++;
            }
        }

        if (coordinator == null ||
            turnManager == null ||
            validPawnCount < 2 ||
            !hasMovementStartedEvent ||
            !hasMovementEndedEvent ||
            !hasFollowerMovementMethod ||
            !hasFollowerSyncMethod)
        {
            Debug.LogError(
                "Phase 5C v1.0 static validation FAILED. " +
                $"Coordinator={(coordinator != null)}, " +
                $"TurnManager={(turnManager != null)}, " +
                $"ValidPawns={validPawnCount}, " +
                $"MovementStarted={hasMovementStartedEvent}, " +
                $"MovementEnded={hasMovementEndedEvent}, " +
                $"FollowerMove={hasFollowerMovementMethod}, " +
                $"FollowerTileSync={hasFollowerSyncMethod}.");

            return;
        }

        Debug.Log(
            "AtlasBoard Phase 5C v1.0 Pawn Movement + Position Sync " +
            "static validation PASSED. Authoritative movement lifecycle and " +
            "visual-only follower APIs are present. This is BUILD/STATIC " +
            "validation only; Runtime PASS requires Host + Guest to show the " +
            "same pawn moving to the same final tile without Guest resolving " +
            "tile/economy gameplay independently.");
    }

    private static T FindSceneComponent<T>()
        where T : UnityEngine.Object
    {
        T[] items =
            Resources.FindObjectsOfTypeAll<T>();

        foreach (T item in items)
        {
            Component component =
                item as Component;

            if (component != null &&
                component.gameObject.scene.IsValid())
            {
                return item;
            }
        }

        return null;
    }

    private static T[] FindSceneComponents<T>()
        where T : Component
    {
        T[] all =
            Resources.FindObjectsOfTypeAll<T>();

        System.Collections.Generic.List<T> result =
            new System.Collections.Generic.List<T>();

        foreach (T item in all)
        {
            if (item != null &&
                item.gameObject.scene.IsValid())
            {
                result.Add(item);
            }
        }

        return result.ToArray();
    }
}
#endif
