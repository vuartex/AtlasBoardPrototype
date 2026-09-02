#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardTurnDiceNetworkV1Setup
{
    [InitializeOnLoadMethod]
    private static void EnsureRunInBackgroundV102()
    {
        if (!PlayerSettings.runInBackground)
        {
            PlayerSettings.runInBackground = true;

            Debug.Log(
                "AtlasBoard Phase 5B v1.0.2 enabled PlayerSettings.runInBackground " +
                "for Host + Guest local two-client testing.");
        }
    }

    [MenuItem(
        "Atlas Board/Online/Build Turn + Dice Networking v1",
        false,
        530)]
    public static void Build()
    {
        GameObject canvas =
            FindSceneObject(
                "Canvas_MainMenu");

        if (canvas == null)
        {
            Debug.LogError(
                "Phase 5B build FAILED: Canvas_MainMenu was not found.");

            return;
        }

        AtlasBoardLobbyRuntimeBridge lobbyBridge =
            canvas.GetComponent<
                AtlasBoardLobbyRuntimeBridge>();

        AtlasBoardMatchRuntimeBridge matchBridge =
            canvas.GetComponent<
                AtlasBoardMatchRuntimeBridge>();

        if (lobbyBridge == null ||
            matchBridge == null)
        {
            Debug.LogError(
                "Phase 5B build FAILED: Phase 5A Lobby/Match runtime bridges are missing.");

            return;
        }

        SerializedObject matchBridgeSerialized =
            new SerializedObject(matchBridge);

        SerializedProperty pollProperty =
            matchBridgeSerialized.FindProperty(
                "snapshotPollSeconds");

        if (pollProperty != null)
        {
            pollProperty.floatValue = 0.25f;
            matchBridgeSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            canvas.GetComponent<
                AtlasBoardTurnDiceNetworkCoordinator>();

        if (coordinator == null)
        {
            coordinator =
                Undo.AddComponent<
                    AtlasBoardTurnDiceNetworkCoordinator>(
                        canvas);
        }

        EditorUtility.SetDirty(coordinator);
        EditorUtility.SetDirty(canvas);
        EditorSceneManager.MarkAllScenesDirty();

        Debug.Log(
            "AtlasBoard Phase 5B Turn + Dice Networking v1 installed. " +
            "Online Host remains authoritative; Remote clients use Roll intents " +
            "and follower dice/turn presentation. Movement remains Phase 5C.");
    }

    [MenuItem(
        "Atlas Board/Online/Previous Phase Validators/Phase 5B - Validate Turn + Dice Networking v1",
        false,
        531)]
    public static void Validate()
    {
        GameObject canvas =
            FindSceneObject(
                "Canvas_MainMenu");

        if (canvas == null)
        {
            Debug.LogError(
                "Phase 5B validation FAILED: Canvas_MainMenu missing.");

            return;
        }

        bool hasLobby =
            canvas.GetComponent<
                AtlasBoardLobbyRuntimeBridge>() != null;

        bool hasMatch =
            canvas.GetComponent<
                AtlasBoardMatchRuntimeBridge>() != null;

        bool hasCoordinator =
            canvas.GetComponent<
                AtlasBoardTurnDiceNetworkCoordinator>() != null;

        TurnManager turnManager =
            FindSceneComponent<TurnManager>();

        DiceVisualController dice =
            FindSceneComponent<DiceVisualController>();

        if (!hasLobby ||
            !hasMatch ||
            !hasCoordinator ||
            turnManager == null ||
            dice == null)
        {
            Debug.LogError(
                "Phase 5B validation FAILED: required runtime wiring is incomplete. " +
                $"LobbyBridge={hasLobby}, MatchBridge={hasMatch}, " +
                $"Coordinator={hasCoordinator}, TurnManager={(turnManager != null)}, " +
                $"DiceVisual={(dice != null)}.");

            return;
        }

        Debug.Log(
            "AtlasBoard Phase 5B Turn + Dice Networking v1 static validation PASSED. " +
            "This proves component/source wiring only. Real PASS requires Editor Host + " +
            "standalone Guest against the same local Firebase emulators and matching " +
            "starting-order/turn/dice results.");
    }

    [MenuItem(
        "Atlas Board/Online/Previous Phase Validators/Phase 5B - Runtime UI + Input Hotfix v1.0.1",
        false,
        532)]
    public static void ValidateRuntimeUIInputHotfixV101()
    {
        GameObject canvasObject =
            FindSceneObject(
                "Canvas_MainMenu");

        Canvas menuCanvas =
            canvasObject != null
                ? canvasObject.GetComponent<Canvas>()
                : null;

        GameObject boardControls =
            FindSceneObject(
                "Canvas_BoardControls");

        GameObject uxOverlay =
            FindSceneObject(
                "Canvas_UXOverlay");

        AtlasBoardLeaveFlowController leaveFlow =
            FindSceneComponent<AtlasBoardLeaveFlowController>();

        UXKeyboardShortcutController shortcuts =
            FindSceneComponent<UXKeyboardShortcutController>();

        TurnManager turnManager =
            FindSceneComponent<TurnManager>();

        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            canvasObject != null
                ? canvasObject.GetComponent<
                    AtlasBoardTurnDiceNetworkCoordinator>()
                : null;

        if (canvasObject == null ||
            menuCanvas == null ||
            boardControls == null ||
            uxOverlay == null ||
            leaveFlow == null ||
            shortcuts == null ||
            turnManager == null ||
            coordinator == null)
        {
            Debug.LogError(
                "Phase 5B v1.0.1 hotfix static validation FAILED. " +
                $"MainMenu={(canvasObject != null)}, " +
                $"MainMenuCanvas={(menuCanvas != null)}, " +
                $"BoardControls={(boardControls != null)}, " +
                $"UXOverlay={(uxOverlay != null)}, " +
                $"LeaveFlow={(leaveFlow != null)}, " +
                $"KeyboardShortcuts={(shortcuts != null)}, " +
                $"TurnManager={(turnManager != null)}, " +
                $"Coordinator={(coordinator != null)}.");

            return;
        }

        Debug.Log(
            "AtlasBoard Phase 5B v1.0.1 Runtime UI + Input Hotfix " +
            "static validation PASSED. Required menu/gameplay/input/network " +
            "objects are present. This is BUILD/STATIC validation only; " +
            "Runtime PASS still requires the two-client Host + Guest test.");
    }

    [MenuItem(
        "Atlas Board/Online/Previous Phase Validators/Phase 5B - Run In Background v1.0.2",
        false,
        533)]
    public static void ValidateRunInBackgroundV102()
    {
        if (!PlayerSettings.runInBackground)
        {
            Debug.LogError(
                "Phase 5B v1.0.2 static validation FAILED: " +
                "PlayerSettings.runInBackground is disabled.");

            return;
        }

        Debug.Log(
            "AtlasBoard Phase 5B v1.0.2 Run In Background / Alt+Tab local-E2E " +
            "static validation PASSED. PlayerSettings.runInBackground is enabled " +
            "and the runtime bootstrap will force Application.runInBackground=true " +
            "before scene load. Runtime PASS still requires the two-client Alt+Tab test.");
    }

    private static T FindSceneComponent<T>()
        where T : Object
    {
        T[] items =
            Resources.FindObjectsOfTypeAll<T>();

        foreach (T item in items)
        {
            Component component =
                item as Component;

            if (component != null &&
                component.gameObject.scene.IsValid())
            {
                return item;
            }
        }

        return null;
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
