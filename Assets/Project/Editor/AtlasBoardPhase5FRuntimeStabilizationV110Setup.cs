#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase5FRuntimeStabilizationV110Setup
{
    [MenuItem("Atlas Board/Online/Current/Validate Phase 5F v1.1 Runtime Stabilization + Resilience")]
    public static void ValidatePhase5FV110()
    {
        TurnManager turnManager = FindSceneComponent<TurnManager>();
        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            FindSceneComponent<AtlasBoardTurnDiceNetworkCoordinator>();
        AtlasBoardHumanRollTimeoutController rollTimeout =
            FindSceneComponent<AtlasBoardHumanRollTimeoutController>();
        AtlasBoardMatchRuntimeBridge matchBridge =
            FindSceneComponent<AtlasBoardMatchRuntimeBridge>();
        AtlasBoardLobbyRuntimeBridge lobbyBridge =
            FindSceneComponent<AtlasBoardLobbyRuntimeBridge>();
        PlayerHudPanel hud = FindSceneComponent<PlayerHudPanel>();
        TabletUIManager tablet = FindSceneComponent<TabletUIManager>();
        MatchResultManager result = FindSceneComponent<MatchResultManager>();
        PropertyDevelopmentManager development =
            FindSceneComponent<PropertyDevelopmentManager>();

        bool turnHooks =
            HasMember(typeof(TurnManager), "IsOnlineAuthoritativeHost") &&
            HasMethod(typeof(TurnManager), "TryRequestHostAuthoritativeAutomaticHumanRoll") &&
            HasMethod(typeof(TurnManager), "ApplyOnlineFollowerTripleDoublePenalty");

        bool coordinatorHooks =
            HasMethod(typeof(AtlasBoardTurnDiceNetworkCoordinator), "RequestOnlineRematch") &&
            HasMethod(typeof(AtlasBoardTurnDiceNetworkCoordinator), "TryHandleLeaveMatch");

        bool bridgeHooks =
            HasMethod(typeof(AtlasBoardMatchRuntimeBridge), "LeaveActiveMatchAsync") &&
            HasMethod(typeof(AtlasBoardMatchRuntimeBridge), "HostMarkAfkRemovedAsync") &&
            HasMethod(typeof(AtlasBoardMatchRuntimeBridge), "HostExpireReconnectsAsync") &&
            HasMethod(typeof(AtlasBoardMatchRuntimeBridge), "HostPrepareRematchAsync");

        bool playerHooks =
            HasMember(typeof(PlayerGameState), "IsOnlineTemporaryBot") &&
            HasMethod(typeof(PlayerGameState), "ApplyOnlineIdentityAndControlState");

        bool uiHooks =
            HasMethod(typeof(TabletUIManager), "ShowOnlineSeatNotice") &&
            HasMethod(typeof(MatchResultManager), "ShowOnlineMatchResult") &&
            HasMethod(typeof(PropertyDevelopmentManager), "ApplyOnlineAuthoritativeDevelopmentLevel");

        bool objectsPresent =
            turnManager != null &&
            coordinator != null &&
            rollTimeout != null &&
            matchBridge != null &&
            lobbyBridge != null &&
            hud != null &&
            tablet != null &&
            result != null &&
            development != null;

        bool passed = objectsPresent && turnHooks && coordinatorHooks && bridgeHooks && playerHooks && uiHooks;

        if (passed)
        {
            Debug.Log(
                "AtlasBoard Phase 5F v1.1 Runtime Stabilization + Resilience static validation PASSED. " +
                "Required inactive/active scene objects and AFK/reconnect/rematch/result/development hooks are present. " +
                "This is BUILD/STATIC validation only; Runtime PASS still requires the two-client Host + Guest checklist.");
            return;
        }

        Debug.LogError(
            "AtlasBoard Phase 5F v1.1 static validation FAILED. " +
            $"Objects={objectsPresent}, TurnHooks={turnHooks}, CoordinatorHooks={coordinatorHooks}, " +
            $"BridgeHooks={bridgeHooks}, PlayerHooks={playerHooks}, UIHooks={uiHooks}. " +
            $"TurnManager={turnManager != null}, Coordinator={coordinator != null}, RollTimeout={rollTimeout != null}, " +
            $"MatchBridge={matchBridge != null}, LobbyBridge={lobbyBridge != null}, HUD={hud != null}, " +
            $"Tablet={tablet != null}, MatchResult={result != null}, Development={development != null}. " +
            "Do not proceed to Runtime PASS until this validator is green.");
    }

    private static bool HasMethod(Type type, string name)
    {
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(method => string.Equals(method.Name, name, StringComparison.Ordinal));
    }

    private static bool HasMember(Type type, string name)
    {
        return type.GetProperty(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) != null ||
               type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) != null;
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(item =>
                item != null &&
                item.gameObject != null &&
                item.gameObject.scene.IsValid());
    }
}
#endif
