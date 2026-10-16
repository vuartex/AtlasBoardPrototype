#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Explicit choice for Editor integration tests. Never changes Android/Release
/// backend selection, SDK credentials, or deployed Firebase rules.
/// </summary>
public static class AtlasBoardSparkRtdbEditorQa
{
    // Editor-only QA setting. Kept local to Editor code so an older runtime
    // bridge API cannot break editor compilation. Must match the runtime key.
    internal const string BackendPreferenceKey = "AtlasBoard.RTDB.EditorBackend.v1";

    [MenuItem("Atlas Board/QA & Diagnostics/RTDB/Use Live Spark RTDB in Editor")]
    private static void SelectLive()
    {
        SetEmulators(false);
    }

    [MenuItem("Atlas Board/QA & Diagnostics/RTDB/Use Local Emulators in Editor")]
    private static void SelectEmulators()
    {
        SetEmulators(true);
    }

    private static void SetEmulators(bool useEmulators)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("ATB_RTDB_EDITOR_QA Stop Play Mode before changing backend.");
            return;
        }
        // Persist a QA-only project-local editor preference. Newly created
        // bridge components and reloaded scenes will honor the same choice.
        EditorPrefs.SetInt(
            BackendPreferenceKey,
            useEmulators ? 1 : 0);
        AtlasBoardLobbyRuntimeBridge bridge =
            Object.FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>(
                FindObjectsInactive.Include);
        if (bridge == null)
        {
            // Some scenes create this bridge only in PrivateLobby.Awake.
            // Create the same component in Edit Mode so its Editor-only
            // emulator selector can be saved with the scene for QA.
            AtlasBoardPrivateLobbyUIController lobby =
                Object.FindAnyObjectByType<AtlasBoardPrivateLobbyUIController>(
                    FindObjectsInactive.Include);
            if (lobby != null && lobby.gameObject.scene.IsValid())
                bridge = Undo.AddComponent<AtlasBoardLobbyRuntimeBridge>(
                    lobby.gameObject);
        }
        if (bridge == null)
        {
            Debug.Log("ATB_RTDB_EDITOR_QA backend=" +
                      (useEmulators ? "local_emulators" : "live_spark_rtdb") +
                      " saved_in_EditorPrefs=true " +
                      "bridge=not_present_applies_on_next_play " +
                      "deploy=false release_untouched=true");
            return;
        }
        Undo.RecordObject(bridge, "Select AtlasBoard Editor Firebase backend");
        SerializedObject serialized = new SerializedObject(bridge);
        SerializedProperty property =
            serialized.FindProperty("useLocalEmulatorsInEditor");
        if (property == null)
        {
            Debug.LogError("ATB_RTDB_EDITOR_QA emulator selection field missing.");
            return;
        }
        property.boolValue = useEmulators;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(bridge);
        if (bridge.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(bridge.gameObject.scene);
        Debug.Log("ATB_RTDB_EDITOR_QA backend=" +
                  (useEmulators ? "local_emulators" : "live_spark_rtdb") +
                  " resolved=" + (bridge.UsingLocalEmulators
                      ? "local_emulators" : "live_spark_rtdb") +
                  " saved_in_EditorPrefs=true " +
                  "deploy=false release_untouched=true");
    }
}
#endif

#if UNITY_EDITOR
public static class AtlasBoardSparkRtdbRoomSmoke
{
    [MenuItem("Atlas Board/QA & Diagnostics/RTDB/Smoke Test Create - Read - Close")]
    private static async void TestRoomRoundTrip()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("ATB_RTDB_SMOKE Start Play Mode first.");
            return;
        }
        AtlasBoardLobbyRuntimeBridge bridge =
            Object.FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>(
                FindObjectsInactive.Include);
        if (bridge == null)
        {
            Debug.LogError("ATB_RTDB_SMOKE bridge=missing");
            return;
        }
        if (bridge.HasLobby)
        {
            Debug.LogWarning("ATB_RTDB_SMOKE Already in a room. " +
                "Leave it normally before smoke testing; no room overwritten.");
            return;
        }
        Debug.Log("ATB_RTDB_SMOKE stage=create backend=" +
                  (bridge.UsingLocalEmulators ? "emulator" : "Spark") +
                  " mode_choice=" + EditorPrefs.GetInt(
                      AtlasBoardSparkRtdbEditorQa.BackendPreferenceKey,
                      -1) +
                  " project=atlasboard-usa");
        AtlasBoardLobbySelection selection = new AtlasBoardLobbySelection
        {
            Mode = "PRIVATE TABLE",
            MapId = "Turkey",
            PlayerCount = 2,
            RoundLimit = 20,
            ThemeId = "classic_table",
            BalancedDevelopment = true,
            DoublesEnabled = true,
            TripleDoublePenaltyEnabled = true,
            PlayerTypes = new[] { "Human", "Bot", "Bot", "Bot" }
        };
        AtlasLobbyOperationResult created =
            await bridge.CreatePrivateRoomAsync(selection);
        if (!created.Success || created.Snapshot == null)
        {
            Debug.LogError("ATB_RTDB_SMOKE FAIL create error=" +
                created.ErrorLocalizationKey + " technical=" +
                AtlasBoardFrontendErrorCodes.SanitizeForLog(created.TechnicalMessage));
            return;
        }
        string room = created.Snapshot.LobbyId;
        string code = created.RoomCode ?? string.Empty;
        bool validCode = code.Length == 6;
        for (int i = 0; i < code.Length; i++)
            validCode &= code[i] >= '0' && code[i] <= '9';
        AtlasLobbyOperationResult read = await bridge.GetSnapshotAsync();
        bool sameRoom = read.Success && read.Snapshot != null &&
            read.Snapshot.LobbyId == room;
        Debug.Log("ATB_RTDB_SMOKE check room_created=true " +
                  "code_six_digits=" + validCode +
                  "snapshot_roundtrip=" + sameRoom +
                  " no_match_started=true");
        AtlasLobbyOperationResult closed = await bridge.CloseLobbyAsync();
        if (!closed.Success) bridge.ClearLocalLobbyState();
        Debug.Log("ATB_RTDB_SMOKE " +
                  (validCode && sameRoom && closed.Success ? "PASS" : "FAIL") +
                  " stages=create_read_close " +
                  "closed=" + closed.Success +
                  " remote_player_join=UNTESTED gameplay=UNTESTED");
    }
}
#endif
