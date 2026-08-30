#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardOnlineFoundationV1Setup
{
    private const string RootName = "OnlineSessionFoundation";

    [MenuItem("Atlas Board/Online/Build Online Foundation v1")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Exit Play Mode before building Online Foundation v1.");
            return;
        }

        GameObject root = FindSceneObject(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build AtlasBoard Online Foundation v1");
        }

        AtlasBoardOnlineFoundation foundation =
            root.GetComponent<AtlasBoardOnlineFoundation>();

        if (foundation == null)
        {
            foundation = Undo.AddComponent<AtlasBoardOnlineFoundation>(root);
        }

        foundation.EditorConfigureDefaults();
        EditorUtility.SetDirty(foundation);

        if (root.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        Selection.activeGameObject = root;

        Debug.Log(
            "AtlasBoard Online Foundation v1 ready. Provider-neutral session/identity/invite/transport contracts are installed. " +
            "Player seats are separate from platform identity, reconnect reservation defaults to 5 minutes, human roll timeout policy is 10 seconds, " +
            "AFK removal threshold is 10 consecutive auto-rolled scheduled turns, AFK-removed accounts are match-blocked, and crossplay is enabled by default. " +
            "No Steam/mobile SDK or existing gameplay system was modified.");
    }

    [MenuItem("Atlas Board/Online/Validate Online Foundation v1")]
    public static void Validate()
    {
        RunStateMachineValidation();
    }

    private static void RunStateMachineValidation()
    {
        AtlasRoomDescriptor room = new AtlasRoomDescriptor
        {
            SessionId = "local_validation_session",
            RoomCode = "482731",
            RoomName = "Foundation Validation",
            MaxPlayers = 4,
            CrossplayMode = AtlasCrossplayMode.CrossPlatform
        };

        AtlasSessionStateMachine state = new AtlasSessionStateMachine
        {
            ReconnectWindowSeconds = 300,
            AfkAutoRollLimit = 10
        };
        state.Initialize(room);

        long now = DateTime.UtcNow.Ticks;
        AtlasPlayerIdentity steamHost = Identity("account_host", "Host", AtlasPlatformKind.Steam, "steam_1");
        AtlasPlayerIdentity androidGuest = Identity("account_android", "Android Guest", AtlasPlatformKind.Android, "android_1");
        AtlasPlayerIdentity iosGuest = Identity("account_ios", "iOS Guest", AtlasPlatformKind.IOS, "ios_1");

        Require(state.TryAssignHuman(0, steamHost, true, out string error1), error1);
        Require(state.TryAssignHuman(1, androidGuest, false, out string error2), error2);
        Require(state.TryAssignHuman(2, iosGuest, false, out string error3), error3);
        Require(state.TryAssignPermanentBot(3), "P4 bot assignment failed.");
        Require(state.CountConnectedHumans() == 3, "Expected 3 connected humans.");

        // Disconnect -> temporary bot -> same account reclaims same seat.
        Require(state.HandleDisconnect(androidGuest.AccountId, now), "Disconnect handoff failed.");
        AtlasPlayerSeat disconnected = state.FindSeatByAccount(androidGuest.AccountId);
        Require(disconnected.ControllerKind == AtlasSeatControllerKind.TemporaryBot, "Disconnect did not hand control to TemporaryBot.");
        Require(!state.ShouldCloseAfterDisconnects(now), "Room closed while humans/reconnect reservations still exist.");
        Require(state.TryReconnect(androidGuest, now + TimeSpan.FromSeconds(20).Ticks, out AtlasPlayerSeat restored, out string reconnectError), reconnectError);
        Require(restored.PlayerSlotIndex == 1, "Reconnect did not restore the same player slot.");
        Require(restored.ControllerKind == AtlasSeatControllerKind.Human, "Reconnect did not return control to Human.");

        // One human leaves from a multi-human match: room remains because humans remain.
        Require(state.HandleVoluntaryLeave(androidGuest.AccountId, now), "Voluntary leave handoff failed.");
        Require(!state.ShouldCloseAfterExplicitHumanRemoval(), "Room closed even though other humans remain.");

        // AFK: 10 consecutive scheduled turns auto-rolled => permanent bot + match lockout.
        for (int i = 0; i < 9; i++)
        {
            Require(state.RegisterScheduledTurnRoll(iosGuest.AccountId, false, out bool earlyAfk), "AFK counter update failed.");
            Require(!earlyAfk, "AFK triggered before 10 consecutive auto-roll turns.");
        }

        Require(state.RegisterScheduledTurnRoll(iosGuest.AccountId, false, out bool afkReached), "10th AFK counter update failed.");
        Require(afkReached, "AFK did not trigger on the 10th consecutive auto-roll turn.");
        Require(state.HandleAfkRemoval(iosGuest.AccountId), "AFK removal failed.");
        Require(state.IsAccountBlocked(iosGuest.AccountId), "AFK-removed account was not match-blocked.");
        Require(!state.TryReconnect(iosGuest, now + TimeSpan.FromSeconds(10).Ticks, out _, out _), "AFK-removed player was incorrectly allowed to reconnect.");

        // Last connected human explicitly leaves: session may close.
        Require(state.HandleVoluntaryLeave(steamHost.AccountId, now), "Host voluntary leave handoff failed.");
        Require(state.ShouldCloseAfterExplicitHumanRemoval(), "Session did not become closable after the last connected human left.");

        // A last-player disconnect is different: keep reservation alive for reconnect.
        AtlasSessionStateMachine disconnectOnly = new AtlasSessionStateMachine();
        disconnectOnly.Initialize(new AtlasRoomDescriptor { SessionId = "disconnect_test", MaxPlayers = 2 });
        Require(disconnectOnly.TryAssignHuman(0, steamHost, true, out string singleError), singleError);
        Require(disconnectOnly.TryAssignPermanentBot(1), "Single-player P2 bot assignment failed.");
        Require(disconnectOnly.HandleDisconnect(steamHost.AccountId, now), "Single-human disconnect failed.");
        Require(!disconnectOnly.ShouldCloseAfterDisconnects(now), "Disconnected last human lost the reconnect grace period.");
        disconnectOnly.ExpireReconnectReservations(now + TimeSpan.FromSeconds(301).Ticks);
        Require(disconnectOnly.ShouldCloseAfterDisconnects(now + TimeSpan.FromSeconds(301).Ticks), "Session remained alive after the last reconnect reservation expired.");

        Debug.Log(
            "AtlasBoard Online Foundation v1 validation PASSED. Cross-platform seats, TemporaryBot handoff, same-seat reconnect, 5-minute reservation, " +
            "multi-human leave behavior, last-human session close policy, AFK=10 lockout, and last-player disconnect grace were validated locally without a network SDK.");
    }

    private static AtlasPlayerIdentity Identity(
        string accountId,
        string displayName,
        AtlasPlatformKind platform,
        string platformUserId)
    {
        return new AtlasPlayerIdentity
        {
            AccountId = accountId,
            DisplayName = displayName,
            Platform = platform,
            PlatformUserId = platformUserId
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                "AtlasBoard Online Foundation v1 validation FAILED: " + message);
        }
    }

    private static GameObject FindSceneObject(string objectName)
    {
        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
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
