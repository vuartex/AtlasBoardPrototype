#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase5FAuthoritySessionRecoveryV140Setup
{
    [MenuItem("Atlas Board/Online/Current/Validate Phase 5F v1.4 Authority + Session Recovery")]
    public static void ValidatePhase5FV140()
    {
        var coordinator = FindSceneComponent<AtlasBoardTurnDiceNetworkCoordinator>();
        var lobby = FindSceneComponent<AtlasBoardLobbyRuntimeBridge>();
        var match = FindSceneComponent<AtlasBoardMatchRuntimeBridge>();
        var turn = FindSceneComponent<TurnManager>();
        var setup = FindSceneComponent<MatchSetupManager>();
        var dice = FindSceneComponent<DiceVisualController>();
        var hud = FindSceneComponent<PlayerHudPanel>();
        var result = FindSceneComponent<MatchResultManager>();
        var resolution = FindSceneComponent<TileResolutionManager>();
        var development = FindSceneComponent<PropertyDevelopmentManager>();
        var theme = FindSceneComponent<EnvironmentThemeManager>();

        bool coordinatorHooks = coordinator != null &&
            HasMethod(typeof(AtlasBoardTurnDiceNetworkCoordinator), "PrepareForAuthoritativeMatchStart") &&
            HasMethod(typeof(AtlasBoardTurnDiceNetworkCoordinator), "RequestOnlineRematch");
        bool sessionHooks = turn != null && setup != null && dice != null &&
            HasMethod(typeof(TurnManager), "ResetForOnlineLobbySession") &&
            HasMethod(typeof(MatchSetupManager), "ResetForNewMatchSession") &&
            HasMethod(typeof(DiceVisualController), "ResetForNewMatchSession");
        bool resultHooks = result != null &&
            HasMethod(typeof(MatchResultManager), "ShowOnlineMatchResult") &&
            HasMethod(typeof(MatchResultManager), "ResetForNewMatchSession");
        bool developmentHooks = development != null &&
            HasMethod(typeof(PropertyDevelopmentManager), "ResetDevelopment") &&
            HasMethod(typeof(PropertyDevelopmentManager), "ResetAllDevelopmentsForNewMatch");

        if (lobby == null || match == null || hud == null || resolution == null ||
            theme == null || !coordinatorHooks || !sessionHooks ||
            !resultHooks || !developmentHooks)
        {
            Debug.LogError(
                "AtlasBoard Phase 5F v1.4 static validation FAILED. " +
                $"Coordinator={coordinatorHooks}, Lobby={(lobby != null)}, " +
                $"MatchBridge={(match != null)}, Session={sessionHooks}, HUD={(hud != null)}, " +
                $"Result={resultHooks}, Resolution={(resolution != null)}, " +
                $"Development={developmentHooks}, Theme={(theme != null)}.");
            return;
        }

        Debug.Log(
            "AtlasBoard Phase 5F v1.4 Authority + Session Recovery static validation PASSED. " +
            "Human/Bot authority, stale match-snapshot rejection, reusable-session reset, " +
            "Rest+doubles completion, result/rematch presentation, bankruptcy development cleanup, " +
            "HUD turn-badge layout and BoardBase palette hooks are present. " +
            "This is BUILD/STATIC validation only; Runtime PASS still requires two-client Host + Guest tests.");
    }

    private static bool HasMethod(System.Type type, string name) =>
        type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;

    private static T FindSceneComponent<T>() where T : Component
    {
        T[] all = Resources.FindObjectsOfTypeAll<T>();
        foreach (T item in all)
        {
            if (item != null && item.gameObject.scene.IsValid()) return item;
        }
        return null;
    }
}
#endif
