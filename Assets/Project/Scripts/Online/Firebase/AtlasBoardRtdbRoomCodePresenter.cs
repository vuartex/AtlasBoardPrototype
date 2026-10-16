using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Once-per-room UI binding for the six-digit invite code. Existing private
/// lobby Show/Copy actions remain the owner of the actual UI. This component
/// does not inspect every frame, alter online account IDs, or create codes for
/// a LocalOffline match.
/// </summary>
[DisallowMultipleComponent]
public sealed class AtlasBoardRtdbRoomCodePresenter : MonoBehaviour
{
    private static AtlasBoardRtdbRoomCodePresenter instance;
    private AtlasBoardLobbyRuntimeBridge bridge;
    private string shownLobbyId = string.Empty;
    private Coroutine showRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject root = new GameObject("AtlasBoard_RTDB_RoomCodePresentation");
        DontDestroyOnLoad(root);
        root.AddComponent<AtlasBoardRtdbRoomCodePresenter>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindScene();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unbind();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        shownLobbyId = string.Empty;
        BindScene();
    }

    private void BindScene()
    {
        AtlasBoardLobbyRuntimeBridge next =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>(
                FindObjectsInactive.Include);
        if (next == bridge) return;
        Unbind();
        bridge = next;
        if (bridge != null)
        {
            bridge.SnapshotChanged += OnSnapshotChanged;
            OnSnapshotChanged(bridge.CurrentSnapshot);
        }
    }

    private void Unbind()
    {
        if (bridge != null) bridge.SnapshotChanged -= OnSnapshotChanged;
        bridge = null;
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }
    }

    private void OnSnapshotChanged(AtlasLobbySnapshot snapshot)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.LobbyId) ||
            bridge == null)
            return;
        string code = bridge.CurrentRoomCode;
        if (code == null || code.Length != AtlasOnlineDefaults.RoomCodeDigits)
            return;
        for (int i = 0; i < code.Length; i++)
            if (code[i] < '0' || code[i] > '9') return;
        if (string.Equals(shownLobbyId, snapshot.LobbyId, StringComparison.Ordinal))
            return;
        shownLobbyId = snapshot.LobbyId;
        if (showRoutine != null) StopCoroutine(showRoutine);
        // The lobby controller may set codeVisible=false AFTER the RTDB
        // snapshot event. Allow it to finish its own create/join callback.
        showRoutine = StartCoroutine(RevealOnceAfterLobbyUiSettles(snapshot.LobbyId));
    }

    private IEnumerator RevealOnceAfterLobbyUiSettles(string lobbyId)
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        showRoutine = null;
        if (bridge == null || !bridge.HasLobby ||
            bridge.CurrentLobbyId != lobbyId ||
            string.IsNullOrWhiteSpace(bridge.CurrentRoomCode))
            yield break;

        AtlasBoardPrivateLobbyUIController ui =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardPrivateLobbyUIController>(
                FindObjectsInactive.Include);
        if (ui == null) yield break;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo active = typeof(AtlasBoardPrivateLobbyUIController)
            .GetField("roomActive", flags);
        FieldInfo textCode = typeof(AtlasBoardPrivateLobbyUIController)
            .GetField("roomCode", flags);
        FieldInfo visible = typeof(AtlasBoardPrivateLobbyUIController)
            .GetField("codeVisible", flags);
        MethodInfo refresh = typeof(AtlasBoardPrivateLobbyUIController)
            .GetMethod("RefreshRoomHeader", flags);
        if (active == null || textCode == null || visible == null || refresh == null)
        {
            shownLobbyId = string.Empty;
            yield break;
        }
        // Wait a bounded number of frames for the existing lobby controller's
        // async Create/Join continuation. Never poll the whole scene every frame.
        bool isActive = false;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            isActive = active.GetValue(ui) is bool value && value;
            if (isActive) break;
            yield return null;
        }
        if (!isActive)
        {
            shownLobbyId = string.Empty;
            yield break;
        }
        try
        {
            textCode.SetValue(ui, bridge.CurrentRoomCode);
            visible.SetValue(ui, true);
            refresh.Invoke(ui, null);
            Debug.Log("ATB_RTDB_INVITE_CODE visible=true length=6 scope=room action=once");
        }
        catch (Exception error)
        {
            Debug.LogWarning("ATB_RTDB_INVITE_CODE result=ui_bind_failed type=" +
                error.GetType().Name);
        }
    }
}
