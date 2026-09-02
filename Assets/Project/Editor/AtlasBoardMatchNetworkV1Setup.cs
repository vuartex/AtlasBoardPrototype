#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardMatchNetworkV1Setup
{
    [MenuItem(
        "Atlas Board/Online/Build Match Network Foundation v1",
        false,
        520)]
    public static void Build()
    {
        GameObject canvas =
            FindSceneObject(
                "Canvas_MainMenu");

        if (canvas == null)
        {
            Debug.LogError(
                "Phase 5A build FAILED: Canvas_MainMenu was not found.");

            return;
        }

        AtlasBoardLobbyRuntimeBridge lobbyBridge =
            canvas.GetComponent<
                AtlasBoardLobbyRuntimeBridge>();

        if (lobbyBridge == null)
        {
            Debug.LogError(
                "Phase 5A build FAILED: AtlasBoardLobbyRuntimeBridge is missing.");

            return;
        }

        AtlasBoardMatchRuntimeBridge matchBridge =
            canvas.GetComponent<
                AtlasBoardMatchRuntimeBridge>();

        if (matchBridge == null)
        {
            matchBridge =
                Undo.AddComponent<
                    AtlasBoardMatchRuntimeBridge>(
                        canvas);
        }

        EditorUtility.SetDirty(
            matchBridge);

        EditorUtility.SetDirty(
            canvas);

        EditorSceneManager
            .MarkAllScenesDirty();

        Debug.Log(
            "AtlasBoard Phase 5A Match Network Foundation v1 installed. " +
            "This adds authoritative host-state transport and remote intent " +
            "plumbing only; Turn/Dice gameplay synchronization begins in 5B.");
    }

    [MenuItem(
        "Atlas Board/Online/Validate Match Network Foundation v1",
        false,
        521)]
    public static void Validate()
    {
        GameObject canvas =
            FindSceneObject(
                "Canvas_MainMenu");

        if (canvas == null)
        {
            Debug.LogError(
                "Phase 5A validation FAILED: Canvas_MainMenu missing.");

            return;
        }

        bool hasLobby =
            canvas.GetComponent<
                AtlasBoardLobbyRuntimeBridge>() != null;

        bool hasMatch =
            canvas.GetComponent<
                AtlasBoardMatchRuntimeBridge>() != null;

        if (!hasLobby ||
            !hasMatch)
        {
            Debug.LogError(
                "Phase 5A validation FAILED: runtime bridge wiring incomplete.");

            return;
        }

        Debug.Log(
            "AtlasBoard Phase 5A Match Network Foundation v1 static " +
            "validation PASSED. This proves component wiring only. " +
            "Backend Local E2E and later two-client Turn/Dice tests are " +
            "separate validation levels.");
    }

    private static GameObject FindSceneObject(
        string name)
    {
        GameObject[] objects =
            Resources.FindObjectsOfTypeAll<
                GameObject>();

        foreach (GameObject item
                 in objects)
        {
            if (item != null &&
                item.scene.IsValid() &&
                item.name == name)
            {
                return item;
            }
        }

        return null;
    }
}
#endif
