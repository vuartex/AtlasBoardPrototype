#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardLobbyRuntimeBridgeV1Setup
{
    [MenuItem("Atlas Board/Online/Build Firebase Lobby Runtime Bridge v1")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "Exit Play Mode before building the Firebase Lobby Runtime Bridge.");
            return;
        }

        GameObject canvas =
            FindSceneObject("Canvas_MainMenu");

        if (canvas == null)
        {
            Debug.LogError(
                "AtlasBoard Lobby Runtime Bridge build FAILED: Canvas_MainMenu not found.");
            return;
        }

        AtlasBoardPrivateLobbyUIController privateLobby =
            canvas.GetComponent<AtlasBoardPrivateLobbyUIController>();

        if (privateLobby == null)
        {
            Debug.LogError(
                "AtlasBoard Lobby Runtime Bridge build FAILED: " +
                "AtlasBoardPrivateLobbyUIController is missing. Build the visible Private Lobby UI first.");
            return;
        }

        AtlasBoardLobbyRuntimeBridge bridge =
            canvas.GetComponent<AtlasBoardLobbyRuntimeBridge>();

        if (bridge == null)
        {
            bridge =
                Undo.AddComponent<AtlasBoardLobbyRuntimeBridge>(canvas);
        }

        bridge.EditorConfigureDefaults();
        EditorUtility.SetDirty(bridge);

        if (canvas.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(canvas.scene);
        }

        Selection.activeGameObject = canvas;

        Debug.Log(
            "AtlasBoard Firebase Lobby Runtime Bridge v1 installed on Canvas_MainMenu. " +
            "Editor mode uses localhost Auth/Firestore/Functions emulators; " +
            "production account data is not used by the editor bridge.");
    }

    [MenuItem("Atlas Board/Online/Validate Firebase Lobby Runtime Bridge v1")]
    public static void Validate()
    {
        GameObject canvas =
            FindSceneObject("Canvas_MainMenu");

        if (canvas == null)
        {
            Debug.LogError(
                "AtlasBoard Firebase Lobby Runtime Bridge v1 validation FAILED: Canvas_MainMenu not found.");
            return;
        }

        bool hasPrivateLobby =
            canvas.GetComponent<AtlasBoardPrivateLobbyUIController>() != null;

        bool hasBridge =
            canvas.GetComponent<AtlasBoardLobbyRuntimeBridge>() != null;

        if (!hasPrivateLobby || !hasBridge)
        {
            Debug.LogError(
                "AtlasBoard Firebase Lobby Runtime Bridge v1 validation FAILED. " +
                $"PrivateLobby={hasPrivateLobby}, RuntimeBridge={hasBridge}.");
            return;
        }

        Debug.Log(
            "AtlasBoard Firebase Lobby Runtime Bridge v1 static validation PASSED. " +
            "This proves only scene/component wiring. Real PASS still requires Play Mode + live local emulators.");
    }

    private static GameObject FindSceneObject(string name)
    {
        GameObject[] all =
            Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject item in all)
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
