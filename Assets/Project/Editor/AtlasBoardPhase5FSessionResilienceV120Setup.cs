#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase5FSessionResilienceV120Setup
{
    [MenuItem("Atlas Board/Online/Previous Phase Validators/Validate Phase 5F v1.2 Session + Reconnect + Rematch Hotfix")]
    public static void ValidatePhase5FV120()
    {
        TurnManager turn = FindSceneComponent<TurnManager>();
        TradeManager trade = FindSceneComponent<TradeManager>();
        AtlasBoardHumanRollTimeoutController rollTimeout =
            FindSceneComponent<AtlasBoardHumanRollTimeoutController>();
        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            FindSceneComponent<AtlasBoardTurnDiceNetworkCoordinator>();
        AtlasBoardMatchRuntimeBridge matchBridge =
            FindSceneComponent<AtlasBoardMatchRuntimeBridge>();
        AtlasBoardPrivateLobbyUIController privateLobby =
            FindSceneComponent<AtlasBoardPrivateLobbyUIController>();
        MatchResultManager result = FindSceneComponent<MatchResultManager>();
        TabletUIManager tablet = FindSceneComponent<TabletUIManager>();
        DiceVisualController dice = FindSceneComponent<DiceVisualController>();
        PlayerHudPanel hud = FindSceneComponent<PlayerHudPanel>();
        BankruptcyManager bankruptcy = FindSceneComponent<BankruptcyManager>();

        bool turnHooks =
            HasMethod(typeof(TurnManager), "ResetForOnlineLobbySession");

        bool tradeHooks =
            HasMember(typeof(TradeManager), "RemoteTradeWindowChanged") &&
            HasMethod(typeof(TradeManager), "ResetForNewMatchSession");

        bool timeoutHooks =
            HasMethod(typeof(AtlasBoardHumanRollTimeoutController), "SetRemoteManagementHold") &&
            HasMethod(typeof(AtlasBoardHumanRollTimeoutController), "ResetForNewMatchSession");

        bool bridgeHooks =
            HasMethod(typeof(AtlasBoardMatchRuntimeBridge), "ResetForMatchSession") &&
            HasMethod(typeof(AtlasBoardMatchRuntimeBridge), "RefreshSnapshotNowAsync");

        bool uiHooks =
            HasMethod(typeof(MatchResultManager), "ResetForNewMatchSession") &&
            HasMethod(typeof(MatchResultManager), "NotifyOnlineRematchRequestFailed") &&
            HasMethod(typeof(TabletUIManager), "ResetForNewMatchSession") &&
            HasMethod(typeof(DiceVisualController), "ResetForNewMatchSession");

        bool playerHooks =
            HasMember(typeof(PlayerGameState), "IsOnlineBotControlled");

        bool objects =
            turn != null &&
            trade != null &&
            rollTimeout != null &&
            coordinator != null &&
            matchBridge != null &&
            privateLobby != null &&
            result != null &&
            tablet != null &&
            dice != null &&
            hud != null &&
            bankruptcy != null;

        bool passed =
            objects &&
            turnHooks &&
            tradeHooks &&
            timeoutHooks &&
            bridgeHooks &&
            uiHooks &&
            playerHooks;

        if (passed)
        {
            Debug.Log(
                "AtlasBoard Phase 5F v1.2 Session + Reconnect + Rematch Hotfix static validation PASSED. " +
                "Trade AFK hold, clean reusable-session reset, live reconnect snapshot catch-up, Host/Guest result actions, " +
                "dice reset, authoritative bot HUD state and bankruptcy-development cleanup hooks are present. " +
                "This is BUILD/STATIC validation only; Runtime PASS still requires the two-client checklist.");
            return;
        }

        Debug.LogError(
            "AtlasBoard Phase 5F v1.2 static validation FAILED. " +
            $"Objects={objects}, TurnHooks={turnHooks}, TradeHooks={tradeHooks}, TimeoutHooks={timeoutHooks}, " +
            $"BridgeHooks={bridgeHooks}, UIHooks={uiHooks}, PlayerHooks={playerHooks}. " +
            $"Turn={turn != null}, Trade={trade != null}, RollTimeout={rollTimeout != null}, Coordinator={coordinator != null}, " +
            $"MatchBridge={matchBridge != null}, PrivateLobby={privateLobby != null}, Result={result != null}, " +
            $"Tablet={tablet != null}, Dice={dice != null}, HUD={hud != null}, Bankruptcy={bankruptcy != null}.");
    }

    private static bool HasMethod(Type type, string name)
    {
        return type.GetMethods(
                BindingFlags.Instance |
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .Any(method =>
                string.Equals(
                    method.Name,
                    name,
                    StringComparison.Ordinal));
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

    private static T FindSceneComponent<T>()
        where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(item =>
                item != null &&
                item.gameObject != null &&
                item.gameObject.scene.IsValid());
    }
}
#endif
