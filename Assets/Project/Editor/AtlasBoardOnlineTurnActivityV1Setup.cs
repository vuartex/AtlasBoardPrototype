#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardOnlineTurnActivityV1Setup
{
    private const string FoundationRootName =
        "OnlineSessionFoundation";

    [MenuItem(
        "Atlas Board/Online/Build Turn Activity + AFK v1")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "Exit Play Mode before building Turn Activity + AFK v1.");
            return;
        }

        TurnManager turnManager =
            FindSceneComponent<TurnManager>();

        if (turnManager == null)
        {
            Debug.LogError(
                "TurnManager was not found in the active scene. " +
                "Turn Activity + AFK v1 was not built.");
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

        AtlasBoardOnlineFoundation foundation =
            root.GetComponent<
                AtlasBoardOnlineFoundation>();

        if (foundation == null)
        {
            foundation =
                Undo.AddComponent<
                    AtlasBoardOnlineFoundation>(
                        root);
        }

        foundation.EditorConfigureTurnPolicy(
            AtlasOnlineDefaults.HumanRollTimeoutSeconds,
            AtlasOnlineDefaults.AfkConsecutiveAutoRollLimit);

        EditorUtility.SetDirty(
            foundation);

        AtlasBoardHumanRollTimeoutController controller =
            root.GetComponent<
                AtlasBoardHumanRollTimeoutController>();

        if (controller == null)
        {
            controller =
                Undo.AddComponent<
                    AtlasBoardHumanRollTimeoutController>(
                        root);
        }

        controller.EditorConfigure(
            turnManager,
            foundation,
            AtlasOnlineDefaults.HumanRollTimeoutSeconds,
            AtlasOnlineDefaults.AfkConsecutiveAutoRollLimit);

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
            "AtlasBoard Turn Activity + AFK v1 ready. Human roll timeout=10s. " +
            "The clock advances only while TurnManager.CanPlayerRequestRoll is true, " +
            "so dice animation, movement, turn-start work and management/modal blockers pause it. " +
            "Starting-order auto-rolls do not count toward AFK. Only the first roll of each " +
            "scheduled turn updates the AFK streak, so doubles extra rolls cannot double-count. " +
            "10 consecutive automatic scheduled-turn first rolls convert the seat to bot control; " +
            "real online AFK client routing back to Lobby remains a transport/session-adapter responsibility.");
    }

    [MenuItem(
        "Atlas Board/Online/Validate Turn Activity + AFK v1")]
    public static void Validate()
    {
        TurnManager turnManager =
            FindSceneComponent<TurnManager>();

        AtlasBoardOnlineFoundation foundation =
            FindSceneComponent<
                AtlasBoardOnlineFoundation>();

        AtlasBoardHumanRollTimeoutController controller =
            FindSceneComponent<
                AtlasBoardHumanRollTimeoutController>();

        bool valid =
            turnManager != null &&
            foundation != null &&
            controller != null &&
            Mathf.Approximately(
                foundation.HumanRollTimeoutSeconds,
                10f) &&
            foundation.AfkConsecutiveAutoRollLimit ==
                10;

        if (!valid)
        {
            Debug.LogError(
                "AtlasBoard Turn Activity + AFK v1 validation FAILED. " +
                "Run Build Turn Activity + AFK v1 and verify the OnlineSessionFoundation object.");
            return;
        }

        Debug.Log(
            "AtlasBoard Turn Activity + AFK v1 validation PASSED. " +
            "TurnManager, Online Foundation and timeout controller are connected; " +
            "policy is 10-second roll timeout + 10 consecutive scheduled-turn AFK limit.");
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
