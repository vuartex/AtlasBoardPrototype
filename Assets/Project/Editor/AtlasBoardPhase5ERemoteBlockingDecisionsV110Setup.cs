using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase5ERemoteBlockingDecisionsV110Setup
{
    [MenuItem(
        "Atlas Board/Online/Previous Phase Validators/Phase 5E - Remote Blocking Decisions v1.1")]
    public static void ValidatePhase5EV110()
    {
        // Phase 5E v1.1.2 validator hotfix:
        // decision/pawn UI objects may intentionally be inactive while the
        // lobby/gameplay modal is closed. FindAnyObjectByType() excludes those
        // objects and produced a false static failure. Use the same
        // include-inactive scene lookup pattern already used by Phase 5C/5D.
        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            FindSceneComponent<AtlasBoardTurnDiceNetworkCoordinator>();

        TileResolutionManager tileResolution =
            FindSceneComponent<TileResolutionManager>();

        SpecialTileManager specialTiles =
            FindSceneComponent<SpecialTileManager>();

        EventCardManager eventCards =
            FindSceneComponent<EventCardManager>();

        AuctionManager auctions =
            FindSceneComponent<AuctionManager>();

        AtlasBoardPawnCustomizationUI customizationUI =
            FindSceneComponent<AtlasBoardPawnCustomizationUI>();

        bool hasTravelPresentation =
            HasPublicInstanceMethod(
                typeof(TileResolutionManager),
                "ShowOnlineRemoteTravelDecision");

        bool hasSpecialPresentation =
            HasPublicInstanceMethod(
                typeof(SpecialTileManager),
                "ShowOnlineRemoteSpecialDecision");

        bool hasEventPresentation =
            HasPublicInstanceMethod(
                typeof(EventCardManager),
                "ShowOnlineRemoteEventDecision");

        bool hasAuctionPresentation =
            HasPublicInstanceMethod(
                typeof(AuctionManager),
                "ShowOnlineRemoteAuctionState");

        bool hasRemoteTravelEvent =
            typeof(TileResolutionManager).GetEvent(
                "RemoteTravelDecisionRequested",
                BindingFlags.Public |
                BindingFlags.Instance) != null;

        bool hasRemoteSpecialEvent =
            typeof(SpecialTileManager).GetEvent(
                "RemoteContinueRequested",
                BindingFlags.Public |
                BindingFlags.Instance) != null;

        bool hasRemoteEventCardEvent =
            typeof(EventCardManager).GetEvent(
                "RemoteContinueRequested",
                BindingFlags.Public |
                BindingFlags.Instance) != null;

        bool hasRemoteAuctionEvent =
            typeof(AuctionManager).GetEvent(
                "RemoteAuctionDecisionRequested",
                BindingFlags.Public |
                BindingFlags.Instance) != null;

        bool valid =
            coordinator != null &&
            tileResolution != null &&
            specialTiles != null &&
            eventCards != null &&
            auctions != null &&
            customizationUI != null &&
            hasTravelPresentation &&
            hasSpecialPresentation &&
            hasEventPresentation &&
            hasAuctionPresentation &&
            hasRemoteTravelEvent &&
            hasRemoteSpecialEvent &&
            hasRemoteEventCardEvent &&
            hasRemoteAuctionEvent;

        if (!valid)
        {
            Debug.LogError(
                "AtlasBoard Phase 5E v1.1.2 static validation FAILED. " +
                $"Coordinator={(coordinator != null)}, " +
                $"TileResolution={(tileResolution != null)}, " +
                $"SpecialTiles={(specialTiles != null)}, " +
                $"EventCards={(eventCards != null)}, " +
                $"Auction={(auctions != null)}, " +
                $"PawnCustomizationUI={(customizationUI != null)}, " +
                $"TravelPresentation={hasTravelPresentation}, " +
                $"SpecialPresentation={hasSpecialPresentation}, " +
                $"EventPresentation={hasEventPresentation}, " +
                $"AuctionPresentation={hasAuctionPresentation}, " +
                $"TravelIntentEvent={hasRemoteTravelEvent}, " +
                $"SpecialIntentEvent={hasRemoteSpecialEvent}, " +
                $"EventCardIntentEvent={hasRemoteEventCardEvent}, " +
                $"AuctionIntentEvent={hasRemoteAuctionEvent}. " +
                "Inactive scene objects are included by this validator, so any FALSE value now identifies the exact remaining wiring/source blocker.");
            return;
        }

        Debug.Log(
            "AtlasBoard Phase 5E v1.1.2 static validation PASSED. " +
            "Inactive scene objects are included; Remote Travel/Event/Special/Auction " +
            "presentation hooks, intent events, authoritative coordinator, and pawn " +
            "customization UI are present. This is BUILD/STATIC validation only; " +
            "Runtime PASS still requires the two-client Host + Guest decision test.");
    }

    private static bool HasPublicInstanceMethod(
        System.Type type,
        string methodName)
    {
        return type.GetMethod(
                   methodName,
                   BindingFlags.Public |
                   BindingFlags.Instance) != null;
    }

    private static T FindSceneComponent<T>()
        where T : UnityEngine.Object
    {
        T[] items = Resources.FindObjectsOfTypeAll<T>();

        foreach (T item in items)
        {
            Component component = item as Component;

            if (component != null &&
                component.gameObject.scene.IsValid())
            {
                return item;
            }
        }

        return null;
    }
}
