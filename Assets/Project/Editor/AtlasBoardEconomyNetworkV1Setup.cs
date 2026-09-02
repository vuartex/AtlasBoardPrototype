#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardEconomyNetworkV1Setup
{
    [MenuItem(
        "Atlas Board/Online/Previous Phase Validators/Phase 5D - Economy + Ownership + Rent Sync v1.0",
        false,
        550)]
    public static void ValidateEconomyNetworkV10()
    {
        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            FindSceneComponent<AtlasBoardTurnDiceNetworkCoordinator>();

        TurnManager turnManager =
            FindSceneComponent<TurnManager>();

        BoardPath boardPath =
            FindSceneComponent<BoardPath>();

        PlayerGameState[] players =
            FindSceneComponents<PlayerGameState>();

        bool hasMoneyApply =
            typeof(PlayerGameState)
                .GetMethod(
                    "ApplyOnlineAuthoritativeMoney",
                    BindingFlags.Instance |
                    BindingFlags.Public) != null;

        bool hasOwnerApply =
            typeof(BoardTile)
                .GetMethod(
                    "ApplyOnlineAuthoritativeOwner",
                    BindingFlags.Instance |
                    BindingFlags.Public) != null;

        int participatingCapablePlayers = 0;

        foreach (PlayerGameState player in players)
        {
            if (player != null &&
                player.PlayerSlotIndex >= 0 &&
                player.PlayerSlotIndex < 4)
            {
                participatingCapablePlayers++;
            }
        }

        int tileCount =
            boardPath != null
                ? boardPath.TileCount
                : 0;

        if (coordinator == null ||
            turnManager == null ||
            boardPath == null ||
            participatingCapablePlayers < 2 ||
            tileCount <= 0 ||
            !hasMoneyApply ||
            !hasOwnerApply)
        {
            Debug.LogError(
                "Phase 5D v1.0 static validation FAILED. " +
                $"Coordinator={(coordinator != null)}, " +
                $"TurnManager={(turnManager != null)}, " +
                $"BoardPath={(boardPath != null)}, " +
                $"Players={participatingCapablePlayers}, " +
                $"Tiles={tileCount}, " +
                $"MoneyApply={hasMoneyApply}, " +
                $"OwnerApply={hasOwnerApply}.");

            return;
        }

        Debug.Log(
            "AtlasBoard Phase 5D v1.0 Economy + Ownership + Rent Sync " +
            "static validation PASSED. Host economy checkpoint, Remote exact-money " +
            "mirror, and authoritative tile ownership visual/state APIs are present. " +
            "This is BUILD/STATIC validation only; Runtime PASS requires the two-client " +
            "purchase/rent/balance/ownership tests. Buy/Skip Remote decision UI remains Phase 5E.");
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
