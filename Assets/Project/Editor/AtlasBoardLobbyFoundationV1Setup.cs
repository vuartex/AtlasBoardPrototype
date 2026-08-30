#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardLobbyFoundationV1Setup
{
    [MenuItem("Atlas Board/Online/Validate Lobby Revision + Ready v1")]
    public static void Validate()
    {
        AtlasRoomDescriptor room =
            new AtlasRoomDescriptor
            {
                SessionId = "lobby_revision_validation",
                RoomName = "Lobby Revision Validation",
                HostAccountId = string.Empty,
                MaxPlayers = 4,
                RequiredHumanPlayers = 3,
                SettingsRevision =
                    AtlasOnlineDefaults.InitialLobbySettingsRevision,
                LifecycleState =
                    AtlasRoomLifecycleState.Waiting
            };

        AtlasSessionStateMachine state =
            new AtlasSessionStateMachine();

        state.Initialize(room);

        AtlasPlayerIdentity host =
            Identity(
                "host_account",
                "Host");

        AtlasPlayerIdentity guest1 =
            Identity(
                "guest_one",
                "Guest One");

        AtlasPlayerIdentity guest2 =
            Identity(
                "guest_two",
                "Guest Two");

        Require(
            state.TryAssignHuman(
                0,
                host,
                true,
                out string hostError),
            hostError);

        Require(
            state.TryAssignHuman(
                1,
                guest1,
                false,
                out string guest1Error),
            guest1Error);

        Require(
            state.TryAssignHuman(
                2,
                guest2,
                false,
                out string guest2Error),
            guest2Error);

        Require(
            state.TryAssignPermanentBot(3),
            "P4 bot assignment failed.");

        Require(
            room.SettingsRevision == 1,
            "Expected initial settingsRevision=1.");

        Require(
            state.TrySetLobbyReady(
                host.AccountId,
                true,
                1,
                out string hostReadyError),
            hostReadyError);

        Require(
            state.TrySetLobbyReady(
                guest1.AccountId,
                true,
                1,
                out string guest1ReadyError),
            guest1ReadyError);

        Require(
            !state.AreAllRequiredHumansReady(),
            "Lobby became ready before all required humans were ready.");

        Require(
            state.TryAdvanceSettingsRevision(
                host.AccountId,
                out int revision2,
                out string revisionError),
            revisionError);

        Require(
            revision2 == 2,
            "Host settings change did not advance revision to 2.");

        Require(
            !state.AreAllRequiredHumansReady(),
            "Old ready states incorrectly survived settings revision change.");

        Require(
            !state.TryAdvanceSettingsRevision(
                guest1.AccountId,
                out _,
                out _),
            "Non-host player changed lobby settings revision.");

        Require(
            state.TrySetLobbyReady(
                host.AccountId,
                true,
                revision2,
                out string hostReady2Error),
            hostReady2Error);

        Require(
            state.TrySetLobbyReady(
                guest1.AccountId,
                true,
                revision2,
                out string guest1Ready2Error),
            guest1Ready2Error);

        Require(
            state.TrySetLobbyReady(
                guest2.AccountId,
                true,
                revision2,
                out string guest2Ready2Error),
            guest2Ready2Error);

        Require(
            state.AreAllRequiredHumansReady(),
            "All three human seats should be ready for revision 2.");

        Require(
            state.TryTransitionLobbyToStarting(
                out string matchId,
                out string startEventId,
                out string startError),
            startError);

        Require(
            room.LifecycleState ==
            AtlasRoomLifecycleState.Starting,
            "Lobby did not transition Waiting -> Starting.");

        Require(
            !string.IsNullOrWhiteSpace(matchId) &&
            !string.IsNullOrWhiteSpace(startEventId),
            "Authoritative start identifiers were not created.");

        string originalMatchId =
            matchId;

        string originalStartEventId =
            startEventId;

        Require(
            state.TryTransitionLobbyToStarting(
                out string replayMatchId,
                out string replayStartEventId,
                out string replayError),
            replayError);

        Require(
            replayMatchId == originalMatchId &&
            replayStartEventId == originalStartEventId,
            "Starting replay produced a second authoritative start.");

        Debug.Log(
            "AtlasBoard Lobby Revision + Ready v1 static validation PASSED. " +
            "Validated required-human seats, readyForRevision, host-only " +
            "settings revision changes, stale-ready invalidation, all-ready " +
            "gating, and a single Waiting -> Starting start identity. " +
            "This is a local domain/static validation only; backend Local E2E " +
            "must still pass separately.");
    }

    private static AtlasPlayerIdentity Identity(
        string accountId,
        string displayName)
    {
        return new AtlasPlayerIdentity
        {
            AccountId = accountId,
            DisplayName = displayName,
            Platform = AtlasPlatformKind.Windows,
            PlatformUserId = accountId
        };
    }

    private static void Require(
        bool condition,
        string message)
    {
        if (condition)
        {
            return;
        }

        throw new InvalidOperationException(
            "AtlasBoard Lobby Revision + Ready v1 validation FAILED: " +
            message);
    }
}
#endif
