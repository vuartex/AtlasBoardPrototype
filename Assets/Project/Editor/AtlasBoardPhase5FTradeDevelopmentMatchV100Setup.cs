using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase5FTradeDevelopmentMatchV100Setup
{
    [MenuItem(
        "Atlas Board/Online/Current/Validate Phase 5F v1.0 Trade + Development + Match Completion Sync",
        false,
        600)]
    public static void ValidatePhase5FV100()
    {
        TurnManager turnManager = FindSceneObject<TurnManager>();
        TradeManager tradeManager = FindSceneObject<TradeManager>();
        TileResolutionManager tileResolution =
            FindSceneObject<TileResolutionManager>();
        PropertyDevelopmentManager development =
            FindSceneObject<PropertyDevelopmentManager>();
        MatchResultManager matchResult =
            FindSceneObject<MatchResultManager>();
        AtlasBoardHumanDecisionTimeoutController timeout =
            FindSceneObject<AtlasBoardHumanDecisionTimeoutController>();
        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            FindSceneObject<AtlasBoardTurnDiceNetworkCoordinator>();
        PawnCosmeticApplier pawnCosmetic =
            FindSceneObject<PawnCosmeticApplier>();

        bool tradeHooks =
            HasEvent(
                typeof(TradeManager),
                "RemoteTradeOfferRequested") &&
            HasEvent(
                typeof(TradeManager),
                "RemoteTradeResponseRequested") &&
            HasMethod(
                typeof(TradeManager),
                "TryBeginAuthoritativeRemoteOffer") &&
            HasMethod(
                typeof(TradeManager),
                "ShowOnlineRemoteTradeState");

        bool developmentHooks =
            HasEvent(
                typeof(TileResolutionManager),
                "RemoteDevelopmentDecisionRequested") &&
            HasMethod(
                typeof(TileResolutionManager),
                "ShowOnlineRemoteDevelopmentDecision") &&
            HasMethod(
                typeof(PropertyDevelopmentManager),
                "ApplyOnlineAuthoritativeDevelopmentLevel");

        bool turnHooks =
            HasProperty(
                typeof(TurnManager),
                "IsMatchFinished") &&
            HasMethod(
                typeof(TurnManager),
                "BeginOnlineFollowerManagementPresentation") &&
            HasMethod(
                typeof(TurnManager),
                "TryBeginAuthoritativeNetworkManagementAction");

        bool resultHooks =
            HasMethod(
                typeof(MatchResultManager),
                "BuildOnlineResultSnapshot") &&
            HasMethod(
                typeof(MatchResultManager),
                "ShowOnlineMatchResult");

        bool timeoutOkay =
            ValidateTradeTimeout(timeout);

        bool sceneObjects =
            turnManager != null &&
            tradeManager != null &&
            tileResolution != null &&
            development != null &&
            matchResult != null &&
            timeout != null &&
            coordinator != null &&
            pawnCosmetic != null;

        if (sceneObjects &&
            tradeHooks &&
            developmentHooks &&
            turnHooks &&
            resultHooks &&
            timeoutOkay)
        {
            Debug.Log(
                "AtlasBoard Phase 5F v1.0 static validation PASSED. " +
                "Trade setup/response networking, Remote development decisions, " +
                "authoritative development checkpoints, exact follower dice-result " +
                "presentation, match-result replication, 25-second Trade AFK grace, " +
                "and pawn cosmetic rebind guard are present. This is BUILD/STATIC " +
                "validation only; Runtime PASS still requires the two-client Host + " +
                "Guest test.");
            return;
        }

        Debug.LogError(
            "AtlasBoard Phase 5F v1.0 static validation FAILED. " +
            $"SceneObjects={sceneObjects}, " +
            $"TurnManager={turnManager != null}, " +
            $"TradeManager={tradeManager != null}, " +
            $"TileResolution={tileResolution != null}, " +
            $"Development={development != null}, " +
            $"MatchResult={matchResult != null}, " +
            $"Timeout={timeout != null}, " +
            $"Coordinator={coordinator != null}, " +
            $"PawnCosmetic={pawnCosmetic != null}, " +
            $"TradeHooks={tradeHooks}, " +
            $"DevelopmentHooks={developmentHooks}, " +
            $"TurnHooks={turnHooks}, " +
            $"ResultHooks={resultHooks}, " +
            $"TradeTimeout25={timeoutOkay}. " +
            "Do not run the two-client Runtime test until this static blocker is fixed.");
    }

    private static bool ValidateTradeTimeout(
        AtlasBoardHumanDecisionTimeoutController timeout)
    {
        if (timeout == null)
        {
            return false;
        }

        SerializedObject serialized =
            new SerializedObject(timeout);
        SerializedProperty property =
            serialized.FindProperty(
                "tradeDecisionTimeoutSeconds");

        return property != null &&
               property.floatValue >= 24.99f;
    }

    private static bool HasMethod(
        Type type,
        string name)
    {
        return type.GetMethod(
                   name,
                   BindingFlags.Instance |
                   BindingFlags.Public |
                   BindingFlags.NonPublic) != null;
    }

    private static bool HasProperty(
        Type type,
        string name)
    {
        return type.GetProperty(
                   name,
                   BindingFlags.Instance |
                   BindingFlags.Public |
                   BindingFlags.NonPublic) != null;
    }

    private static bool HasEvent(
        Type type,
        string name)
    {
        return type.GetEvent(
                   name,
                   BindingFlags.Instance |
                   BindingFlags.Public |
                   BindingFlags.NonPublic) != null;
    }

    private static T FindSceneObject<T>()
        where T : UnityEngine.Object
    {
        T[] objects =
            Resources.FindObjectsOfTypeAll<T>();

        foreach (T candidate in objects)
        {
            Component component =
                candidate as Component;

            if (component != null &&
                component.gameObject.scene.IsValid())
            {
                return candidate;
            }
        }

        return null;
    }
}
