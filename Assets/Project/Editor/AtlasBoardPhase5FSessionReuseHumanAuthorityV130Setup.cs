#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase5FSessionReuseHumanAuthorityV130Setup
{
    [MenuItem("Atlas Board/Online/Current/Validate Phase 5F v1.3 Human Authority + Session Reuse")]
    public static void ValidatePhase5FV130()
    {
        BotPlayerController bot = FindSceneComponent<BotPlayerController>();
        PlayerHudPanel hud = FindSceneComponent<PlayerHudPanel>();
        MatchSetupManager setup = FindSceneComponent<MatchSetupManager>();
        PlayerPawnMover pawn = FindSceneComponent<PlayerPawnMover>();
        TurnManager turn = FindSceneComponent<TurnManager>();
        TileResolutionManager resolution = FindSceneComponent<TileResolutionManager>();
        SpecialTileManager special = FindSceneComponent<SpecialTileManager>();
        EventCardManager events = FindSceneComponent<EventCardManager>();
        AuctionManager auction = FindSceneComponent<AuctionManager>();
        MatchResultManager result = FindSceneComponent<MatchResultManager>();
        PropertyDevelopmentManager development = FindSceneComponent<PropertyDevelopmentManager>();
        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            FindSceneComponent<AtlasBoardTurnDiceNetworkCoordinator>();
        AtlasBoardPrivateLobbyUIController privateLobby =
            FindSceneComponent<AtlasBoardPrivateLobbyUIController>();
        EnvironmentThemeManager environment = FindSceneComponent<EnvironmentThemeManager>();

        bool sessionHooks =
            HasMethod(typeof(MatchSetupManager), "ResetForNewMatchSession") &&
            HasMethod(typeof(PlayerPawnMover), "ResetForNewMatchSession") &&
            HasMethod(typeof(TileResolutionManager), "ResetForNewMatchSession") &&
            HasMethod(typeof(SpecialTileManager), "ResetForNewMatchSession") &&
            HasMethod(typeof(EventCardManager), "ResetForNewMatchSession") &&
            HasMethod(typeof(AuctionManager), "ResetForNewMatchSession") &&
            HasMethod(typeof(PropertyDevelopmentManager), "ResetAllDevelopmentsForNewMatch");

        bool restHooks =
            HasMethod(typeof(TurnManager), "SuppressExtraRollForCurrentTurn");

        bool resultHooks =
            HasMethod(typeof(MatchResultManager), "ResetForNewMatchSession") &&
            HasMethod(typeof(MatchResultManager), "NotifyOnlineRematchRequestFailed");

        bool humanAuthorityHooks =
            HasMember(typeof(PlayerGameState), "OnlineSeatStateActive") &&
            HasMember(typeof(PlayerGameState), "IsOnlineBotControlled");

        bool objects =
            bot != null && hud != null && setup != null && pawn != null &&
            turn != null && resolution != null && special != null &&
            events != null && auction != null && result != null &&
            development != null && coordinator != null && privateLobby != null &&
            environment != null;

        bool passed =
            objects && sessionHooks && restHooks && resultHooks &&
            humanAuthorityHooks;

        if (passed)
        {
            Debug.Log(
                "AtlasBoard Phase 5F v1.3 Human Authority + Session Reuse static validation PASSED. " +
                "Authoritative Human/Bot guards, reusable match reset hooks, Rest/doubles suppression, " +
                "result/rematch hooks and classic-table visual manager are present. " +
                "This is BUILD/STATIC validation only; Runtime PASS still requires the two-client checklist.");
            return;
        }

        Debug.LogError(
            "AtlasBoard Phase 5F v1.3 static validation FAILED. " +
            $"Objects={objects}, SessionHooks={sessionHooks}, RestHooks={restHooks}, " +
            $"ResultHooks={resultHooks}, HumanAuthorityHooks={humanAuthorityHooks}. " +
            $"Bot={bot != null}, HUD={hud != null}, Setup={setup != null}, Pawn={pawn != null}, " +
            $"Turn={turn != null}, Resolution={resolution != null}, Special={special != null}, " +
            $"Event={events != null}, Auction={auction != null}, Result={result != null}, " +
            $"Development={development != null}, Coordinator={coordinator != null}, " +
            $"PrivateLobby={privateLobby != null}, Environment={environment != null}.");
    }

    private static bool HasMethod(Type type, string name)
    {
        return type.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .Any(method =>
                string.Equals(method.Name, name, StringComparison.Ordinal));
    }

    private static bool HasMember(Type type, string name)
    {
        return type.GetProperty(
                   name,
                   BindingFlags.Instance |
                   BindingFlags.Static |
                   BindingFlags.Public |
                   BindingFlags.NonPublic) != null ||
               type.GetField(
                   name,
                   BindingFlags.Instance |
                   BindingFlags.Static |
                   BindingFlags.Public |
                   BindingFlags.NonPublic) != null ||
               type.GetEvent(
                   name,
                   BindingFlags.Instance |
                   BindingFlags.Static |
                   BindingFlags.Public |
                   BindingFlags.NonPublic) != null;
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
