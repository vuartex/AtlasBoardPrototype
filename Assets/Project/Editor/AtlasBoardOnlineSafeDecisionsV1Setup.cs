#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardOnlineSafeDecisionsV1Setup
{
    private const string FoundationRootName =
        "OnlineSessionFoundation";

    [MenuItem(
        "Atlas Board/Online/Build AFK Safe Decisions v1")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "Exit Play Mode before building AFK Safe Decisions v1.");
            return;
        }

        TurnManager turnManager =
            FindSceneComponent<TurnManager>();

        TileResolutionManager tileResolutionManager =
            FindSceneComponent<
                TileResolutionManager>();

        TradeManager tradeManager =
            FindSceneComponent<
                TradeManager>();

        AuctionManager auctionManager =
            FindSceneComponent<
                AuctionManager>();

        EventCardManager eventCardManager =
            FindSceneComponent<
                EventCardManager>();

        SpecialTileManager specialTileManager =
            FindSceneComponent<
                SpecialTileManager>();

        if (turnManager == null)
        {
            Debug.LogError(
                "TurnManager was not found. AFK Safe Decisions v1 was not built.");
            return;
        }

        GameObject root =
            FindSceneObject(
                FoundationRootName);

        if (root == null)
        {
            root =
                new GameObject(
                    FoundationRootName);

            Undo.RegisterCreatedObjectUndo(
                root,
                "Create AtlasBoard Online Foundation Root");
        }

        AtlasBoardHumanDecisionTimeoutController
            controller =
                root.GetComponent<
                    AtlasBoardHumanDecisionTimeoutController>();

        if (controller == null)
        {
            controller =
                Undo.AddComponent<
                    AtlasBoardHumanDecisionTimeoutController>(
                        root);
        }

        controller.EditorConfigure(
            turnManager,
            tileResolutionManager,
            tradeManager,
            auctionManager,
            eventCardManager,
            specialTileManager,
            AtlasOnlineDefaults
                .HumanRollTimeoutSeconds);

        EditorUtility.SetDirty(
            controller);

        if (root.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                root.scene);
        }

        Selection.activeGameObject =
            root;

        Debug.Log(
            "AtlasBoard AFK Safe Decisions v1 ready. " +
            "Human gameplay decisions use the same 10-second grace window. " +
            "Safe defaults: purchase=SKIP, incoming trade=REJECT, outgoing trade setup=CANCEL, " +
            "auction=PASS, travel=STAY, development=SKIP, " +
            "event/special/triple-double acknowledgements=CONTINUE. " +
            "These automatic tablet decisions do NOT increment the 10-turn AFK streak; " +
            "the streak remains based only on automatic first rolls of scheduled turns.");
    }

    [MenuItem(
        "Atlas Board/Online/Validate AFK Safe Decisions v1")]
    public static void Validate()
    {
        AtlasBoardHumanDecisionTimeoutController
            controller =
                FindSceneComponent<
                    AtlasBoardHumanDecisionTimeoutController>();

        TurnManager turnManager =
            FindSceneComponent<TurnManager>();

        EventCardManager eventCardManager =
            FindSceneComponent<
                EventCardManager>();

        bool valid =
            controller != null &&
            turnManager != null &&
            eventCardManager != null;

        if (!valid)
        {
            Debug.LogError(
                "AtlasBoard AFK Safe Decisions v1 validation FAILED. " +
                "Run Build AFK Safe Decisions v1 and verify the active scene.");
            return;
        }

        Debug.Log(
            "AtlasBoard AFK Safe Decisions v1 validation PASSED. " +
            "The authority-side fallback controller is installed and can safely resolve " +
            "blocking Human tablet decisions without spending money or accepting trades.");
    }

    private static T FindSceneComponent<T>()
        where T : Component
    {
        T[] all =
            Resources.FindObjectsOfTypeAll<T>();

        foreach (T item in all)
        {
            if (item != null &&
                item.gameObject.scene.IsValid())
            {
                return item;
            }
        }

        return null;
    }

    private static GameObject FindSceneObject(
        string objectName)
    {
        GameObject[] all =
            Resources.FindObjectsOfTypeAll<
                GameObject>();

        foreach (GameObject item in all)
        {
            if (item != null &&
                item.scene.IsValid() &&
                item.name == objectName)
            {
                return item;
            }
        }

        return null;
    }
}
#endif
