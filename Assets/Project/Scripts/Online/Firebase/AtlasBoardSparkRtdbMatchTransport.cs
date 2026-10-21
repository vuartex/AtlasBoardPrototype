using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Spark-only, client-hosted Casual match transport.
/// The host alone writes authoritative frames. Authenticated remote seats send
/// idempotently keyed intents. No Functions, Firestore, wallet, XP or rewards.
/// Not a trusted competitive/reward authority: the host controls its simulation.
/// </summary>
public static class AtlasBoardSparkRtdbMatchTransport
{
    private const string Database =
        "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";

    [Serializable]
    private sealed class MatchState
    {
        public string matchId, lobbyId, hostUid, status, phase, turnSeatId;
        public string snapshotJson, seatsJson;
        public int revision, eventSequence, authorityEpoch, networkSchemaVersion;
        public long createdAt, updatedAt, hostHeartbeatAt;
    }
    [Serializable]
    private sealed class IntentWire
    {
        public string uid, seatId, type, clientCommandId, payload;
        public long createdAt;
    }
    [Serializable]
    private sealed class AckWire
    {
        public bool accepted;
        public int revision;
        public long processedAt;
        public string errorCode;
    }
    [Serializable]
    private sealed class SeatWire
    {
        public string uid, ownerUid, seatId, mode, name, controller, connection;
        public int slotIndex;
        public bool host, afkLockedOut;
        public long reconnectExpiresAt;
        // Set when a Human first loses the presence lease. A reconnecting
        // client must issue a strictly NEW heartbeat to reclaim this seat.
        public long disconnectedAt;
        public string removalReason;
    }
    [Serializable]
    private sealed class SeatsWire
    {
        public SeatWire[] seats;
    }
    [Serializable] private sealed class PresenceWire
    {
        public bool online;
        public long lastSeenAt;
    }
    [Serializable]
    private sealed class RoomWire
    {
        public long revision, eventSequence, updatedAt;
        public string phase, snapshotJson;
    }
    [Serializable]
    private sealed class RoomMetaWire
    {
        public string hostUid, mode, mapId, themeId, passwordHash, inviteCode;
        public string activeMatchId;
        public long createdAt, updatedAt;
        public int maxPlayers, roundLimit, authorityEpoch;
        public bool closed;
    }
    private struct Response
    {
        public long Status;
        public string Body;
        public string ETag;
        public bool Ok => Status >= 200 && Status < 300;
    }

    private const long HostCrashLeaseMs = 12000L;
    private const long AuthorityCandidatePresenceMs = 10000L;
    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static readonly Dictionary<string,long> LastHostPresenceWrite =
        new Dictionary<string,long>(StringComparer.Ordinal);
    private static readonly Dictionary<string,long> LastMaintenanceCheck =
        new Dictionary<string,long>(StringComparer.Ordinal);
    private static readonly Dictionary<string,AtlasMatchNetworkResult> LastMaintenanceResult =
        new Dictionary<string,AtlasMatchNetworkResult>(StringComparer.Ordinal);
    private static readonly HashSet<string> AuthorityRepairCompleted =
        new HashSet<string>(StringComparer.Ordinal);
    private static string PathKey(string value) => UnityWebRequest.EscapeURL(value ?? "");
    private static string EscapeJson(string raw) => (raw ?? string.Empty)
        .Replace("\\", "\\\\").Replace("\"", "\\\"");
    private static string StatePath(string match) => "matchState/" + PathKey(match);
    private static string IntentRoot(string lobby) => "intents/" + PathKey(lobby);
    private static string AckRoot(string lobby) => "intentAcks/" + PathKey(lobby);
    private static string FailReason(Response r) =>
        r.Status == 401 || r.Status == 403 ? "permission_denied" :
        r.Status == 412 ? "stale_revision" :
        r.Status == 0 ? "connection_failed" : "http_" + r.Status;
    private static AtlasMatchNetworkResult Failure(string detail) =>
        AtlasMatchNetworkResult.Fail("match.error.service_unavailable", detail);

    private static string EncodeController(AtlasLobbyMemberSnapshot member)
    {
        if (member == null) return string.Empty;
        // The finalized seat mode, not a leftover lobby controller, decides
        // whether an unoccupied online slot is an authoritative host bot.
        if (member.SeatMode == AtlasLobbySeatMode.Bot)
            return "permanent_bot";
        switch (member.ControllerKind)
        {
            case AtlasSeatControllerKind.TemporaryBot:
                return "temporary_bot";
            case AtlasSeatControllerKind.PermanentBot:
                return "permanent_bot";
            case AtlasSeatControllerKind.Human:
                return "human";
        }
        if (member.SeatMode == AtlasLobbySeatMode.HostLocal ||
            member.SeatMode == AtlasLobbySeatMode.LocalHuman ||
            member.SeatMode == AtlasLobbySeatMode.RemoteHuman)
            return "human";
        return string.Empty;
    }

    private static string EncodeConnection(AtlasSeatConnectionState state)
    {
        switch (state)
        {
            case AtlasSeatConnectionState.Connected: return "connected";
            case AtlasSeatConnectionState.Reconnecting: return "reconnecting";
            case AtlasSeatConnectionState.LeftVoluntarily: return "left_voluntarily";
            case AtlasSeatConnectionState.AfkRemoved: return "afk_removed";
            case AtlasSeatConnectionState.Kicked: return "kicked";
            default: return string.Empty;
        }
    }

    private static string NormalizeController(string wire, string mode)
    {
        // Repair old RTDB match snapshots without changing existing matches
        // or asking the player to create a new room. Bot seat ownership
        // remains fixed by the finalized seat mode.
        if (string.Equals(mode, "bot", StringComparison.OrdinalIgnoreCase))
            return "permanent_bot";
        if (string.Equals(wire, "permanentbot", StringComparison.OrdinalIgnoreCase))
            return "permanent_bot";
        if (string.Equals(wire, "temporarybot", StringComparison.OrdinalIgnoreCase))
            return "temporary_bot";
        return wire ?? string.Empty;
    }

    public static bool ValidateSeatWireContract()
    {
        AtlasLobbyMemberSnapshot staleBot = new AtlasLobbyMemberSnapshot {
            SeatMode = AtlasLobbySeatMode.Bot,
            ControllerKind = AtlasSeatControllerKind.Human
        };
        AtlasLobbyMemberSnapshot normalHuman = new AtlasLobbyMemberSnapshot {
            SeatMode = AtlasLobbySeatMode.HostLocal,
            ControllerKind = AtlasSeatControllerKind.Human
        };
        return EncodeController(staleBot) == "permanent_bot" &&
            EncodeController(normalHuman) == "human" &&
            NormalizeController("permanentbot", "bot") == "permanent_bot" &&
            NormalizeController("permanentbot", "localhuman") == "permanent_bot" &&
            NormalizeController("human", "bot") == "permanent_bot" &&
            NormalizeController("human", "hostlocal") == "human" &&
            EncodeConnection(AtlasSeatConnectionState.Connected) == "connected";
    }

    internal static async Task<bool> CreateFromLobbyAsync(
        AtlasLobbySnapshot lobby, string token)
    {
        if (lobby == null || string.IsNullOrWhiteSpace(lobby.MatchId) ||
            string.IsNullOrWhiteSpace(lobby.LobbyId) ||
            string.IsNullOrWhiteSpace(token)) return false;

        List<SeatWire> seats = new List<SeatWire>();
        if (lobby.Members != null)
        {
            foreach (AtlasLobbyMemberSnapshot member in lobby.Members)
            {
                if (member == null || !member.Active) continue;
                seats.Add(new SeatWire {
                    uid = member.AccountId ?? "",
                    ownerUid = member.LocalOwnerAccountId ?? "",
                    seatId = string.IsNullOrEmpty(member.SeatId)
                        ? "seat_" + member.SlotIndex : member.SeatId,
                    slotIndex = member.SlotIndex,
                    mode = member.SeatMode.ToString().ToLowerInvariant(),
                    name = member.DisplayName ?? "",
                    host = member.IsHost,
                    // CRITICAL: C# enum name PermanentBot lowercased is
                    // "permanentbot", but the gameplay coordinator recognizes
                    // "permanent_bot". An incorrect wire value disables the
                    // BotPlayerController until the 10-second human AFK timer.
                    // SeatMode.Bot is the authority even if a stale lobby
                    // controller field still says Human.
                    controller = EncodeController(member),
                    connection = EncodeConnection(member.ConnectionState),
                    afkLockedOut = false, removalReason = "", reconnectExpiresAt = 0
                });
            }
        }
        long now = Now;
        int initialAuthorityEpoch = 1;
        Response metaRead = await Send("GET",
            "roomMeta/" + PathKey(lobby.LobbyId), token);
        RoomMetaWire existingMeta = Decode<RoomMetaWire>(metaRead.Body);
        if (metaRead.Ok && existingMeta != null)
            initialAuthorityEpoch = Math.Max(1, existingMeta.authorityEpoch + 1);

        MatchState initial = new MatchState {
            matchId = lobby.MatchId,
            lobbyId = lobby.LobbyId,
            hostUid = lobby.HostAccountId,
            status = "active",
            phase = "starting",
            turnSeatId = "",
            snapshotJson = "",
            seatsJson = JsonUtility.ToJson(new SeatsWire { seats = seats.ToArray() }),
            revision = 0,
            eventSequence = 0,
            authorityEpoch = initialAuthorityEpoch,
            networkSchemaVersion = 1,
            createdAt = now,
            updatedAt = now,
            hostHeartbeatAt = now
        };
        Response result = await Send("PUT", StatePath(lobby.MatchId), token,
            JsonUtility.ToJson(initial), "null_etag");
        if (!result.Ok)
        {
            // The first matchState is created with an atomic null ETag. The
            // matching RTDB .read rule must permit the *absent* node to be
            // checked, while .write still authorizes only the room host.
            // Never log the Firebase ID token or the response URL.
            string cause = FailReason(result);
            Debug.LogWarning("ATB_SPARK_MATCH_INIT failed=true stage=matchState_create" +
                " http=" + result.Status + " cause=" + cause +
                " rule_hint=check_deployed_matchState_absent_read_and_host_write");
            if (result.Status == 401 || result.Status == 403)
            {
                Response host = await Send("GET", "roomMeta/" + PathKey(lobby.LobbyId) +
                    "/hostUid", token);
                Response membership = await Send("GET", "roomMembers/" +
                    PathKey(lobby.LobbyId) + "/" + PathKey(lobby.HostAccountId) +
                    "/active", token);
                bool hostMatches = host.Ok &&
                    string.Equals(host.Body != null ? host.Body.Trim(' ', '\n', '\r', '\t', '"') : "",
                        lobby.HostAccountId, StringComparison.Ordinal);
                bool memberActive = membership.Ok &&
                    string.Equals(membership.Body != null ? membership.Body.Trim() : "",
                        "true", StringComparison.OrdinalIgnoreCase);
                Debug.LogWarning("ATB_SPARK_MATCH_PERMISSIONS" +
                    " room_host_read_http=" + host.Status +
                    " room_host_matches=" + hostMatches +
                    " membership_read_http=" + membership.Status +
                    " host_member_active=" + memberActive +
                    " check=deployed_rules_if_both_true");
            }
        }
        if (result.Ok)
        {
            // roomMeta is the single authority lock for both gameplay and lobby.
            // Older deployed rules may reject these optional fields; gameplay
            // still starts, but crash takeover stays disabled until rules update.
            Response authorityAnchor = await Send("PATCH",
                "roomMeta/" + PathKey(lobby.LobbyId), token,
                "{\"activeMatchId\":\"" + EscapeJson(lobby.MatchId) +
                "\",\"authorityEpoch\":" + initialAuthorityEpoch +
                ",\"updatedAt\":" + now + "}");
            if (!authorityAnchor.Ok)
                Debug.LogWarning("ATB_RTDB_AUTHORITY_ANCHOR ready=false http=" +
                    authorityAnchor.Status + " migration_disabled_for_match=true");
            else
                Debug.Log("ATB_RTDB_AUTHORITY_ANCHOR ready=true epoch=" +
                    initialAuthorityEpoch);
        }
        return result.Ok;
    }

    internal static async Task<bool> DeleteCreatedMatchAsync(string matchId, string token)
    {
        if (string.IsNullOrWhiteSpace(matchId)) return true;
        return (await Send("DELETE", StatePath(matchId), token)).Ok;
    }

    internal static async Task<AtlasMatchNetworkResult> SnapshotAsync(
        string matchId, string accountId, string token)
    {
        Response r = await Send("GET", StatePath(matchId), token);
        if (!r.Ok) return Failure("RTDB match snapshot: " + FailReason(r));
        MatchState state = Decode<MatchState>(r.Body);
        if (state == null || state.matchId != matchId)
            return AtlasMatchNetworkResult.Fail("match.error.not_found", "No RTDB match state exists.");
        return new AtlasMatchNetworkResult {
            Success = true,
            Snapshot = ToSnapshot(state, accountId)
        };
    }

    internal static async Task<AtlasMatchNetworkResult> PublishAsync(
        string matchId, string uid, string token, int expectedRevision,
        string phase, string turnSeatId, int eventSequence, string snapshotJson)
    {
        string path = StatePath(matchId);
        Response r = await Send("GET", path, token, null, null, true);
        if (!r.Ok) return Failure("Read before publish: " + FailReason(r));
        MatchState state = Decode<MatchState>(r.Body);
        if (state == null || state.hostUid != uid)
            return AtlasMatchNetworkResult.Fail("match.error.access_denied", "Only the match host may publish.");
        if (state.revision != expectedRevision)
            return AtlasMatchNetworkResult.Fail("match.error.stale_client_state", "Authoritative revision changed.");
        if (string.IsNullOrEmpty(r.ETag))
            return Failure("RTDB did not supply the expected-state ETag.");
        long now = Now;
        state.revision++;
        state.eventSequence = Mathf.Max(eventSequence, state.eventSequence);
        state.phase = string.IsNullOrEmpty(phase) ? "awaiting_roll" : phase;
        state.turnSeatId = turnSeatId ?? "";
        state.snapshotJson = snapshotJson ?? "{}";
        state.updatedAt = now;
        state.hostHeartbeatAt = now;
        if (state.phase == "match_complete") state.status = "complete";
        Response written = await Send("PUT", path, token,
            JsonUtility.ToJson(state), r.ETag);
        if (!written.Ok)
            return AtlasMatchNetworkResult.Fail(
                "match.error.stale_client_state", "RTDB publish rejected: " + FailReason(written));
        return new AtlasMatchNetworkResult { Success = true, Revision = state.revision };
    }

    internal static async Task<AtlasMatchNetworkResult> SubmitAsync(
        string matchId, string uid, string token, string commandId,
        string intentType, string payload, int observedRevision)
    {
        if (string.IsNullOrWhiteSpace(commandId) || commandId.Length > 128 ||
            string.IsNullOrWhiteSpace(intentType) || intentType.Length > 64)
            return Failure("Invalid command identity or intent type.");
        if (payload != null && payload.Length > 16000)
            return Failure("Intent is larger than the Casual RTDB payload limit.");
        AtlasMatchNetworkResult current = await SnapshotAsync(matchId, uid, token);
        if (!current.Success || current.Snapshot == null) return current;
        AtlasMatchNetworkSnapshot frame = current.Snapshot;
        string seat = frame.LocalSeatId;
        if (string.IsNullOrEmpty(seat))
            return AtlasMatchNetworkResult.Fail("match.error.access_denied",
                "No local seat in this match.");
        // A former remote Human may retain a reclaimable UID after expiry.
        // Membership is not authority to issue Human decisions.
        AtlasMatchNetworkSeat local = frame.Seats != null
            ? frame.Seats.Find(s => s != null && s.SeatId == seat) : null;
        if (local == null || local.AfkLockedOut ||
            !string.Equals(local.ControllerKind,"human",StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(local.ConnectionState,"connected",StringComparison.OrdinalIgnoreCase))
            return AtlasMatchNetworkResult.Fail("match.error.access_denied",
                "This seat is not currently controlled by a connected Human.");
        string id = Digest(uid + ":" + matchId + ":" + commandId).Substring(0, 32);
        string room = frame.LobbyId;
        string ackPath = AckRoot(room) + "/" + PathKey(uid) + "/" + PathKey(id);
        // A retry can arrive after the host has advanced its frame. Check the
        // server ACK before rejecting an OLD revision, never replay a decision.
        if (frame.Revision != observedRevision)
        {
            Response staleAck = await Send("GET", ackPath, token);
            if (staleAck.Ok && staleAck.Body != "null")
                return new AtlasMatchNetworkResult { Success = true,
                    IntentId = id, IdempotentReplay = true };
            return AtlasMatchNetworkResult.Fail("match.error.stale_client_state",
                "Remote command refers to an old frame.");
        }
        string path = IntentRoot(room) + "/" + PathKey(uid) + "/" + PathKey(id);
        IntentWire intent = new IntentWire {
            uid = uid, seatId = seat, type = intentType,
            payload = payload ?? "{}", clientCommandId = commandId,
            createdAt = Now
        };
        // The CAS write itself is the idempotency check for the common path.
        // Avoid an unnecessary ACK round trip on EVERY time-critical roll,
        // purchase and trade action. Server/host ACK checks remain authoritative.
        Response result = await Send("PUT", path, token,
            JsonUtility.ToJson(intent), "null_etag");
        if (result.Ok)
            return new AtlasMatchNetworkResult { Success = true, IntentId = id };
        if (result.Status == 412)
        {
            Response existingAck = await Send("GET", ackPath, token);
            if (existingAck.Ok && existingAck.Body != "null")
                return new AtlasMatchNetworkResult { Success = true,
                    IntentId = id, IdempotentReplay = true };
            // The existing pending intent is not lost; the host may not have
            // committed its ACK yet. Do not write or execute it a second time.
            return new AtlasMatchNetworkResult { Success = true,
                IntentId = id, IdempotentReplay = true };
        }
        return Failure("RTDB intent rejected: " + FailReason(result));
    }

    internal static async Task<AtlasMatchNetworkResult> PendingAsync(
        string matchId, string uid, string token, string verifiedLobbyId = "")
    {
        string room = verifiedLobbyId;
        if (string.IsNullOrWhiteSpace(room))
        {
            AtlasMatchNetworkResult current = await SnapshotAsync(matchId, uid, token);
            if (!current.Success || current.Snapshot == null) return current;
            if (!current.Snapshot.LocalIsHost)
                return AtlasMatchNetworkResult.Fail("match.error.access_denied", "Only host can list commands.");
            room = current.Snapshot.LobbyId;
        }
        // The server's intents .read rule, not the caller-provided room name,
        // determines whether this authenticated UID is its current host.
        Response data = await Send("GET", IntentRoot(room), token);
        if (!data.Ok) return Failure("RTDB intents read: " + FailReason(data));
        List<AtlasMatchIntent> list = new List<AtlasMatchIntent>();
        foreach (KeyValuePair<string,string> owner in ObjectMembers(data.Body))
        {
            foreach (KeyValuePair<string,string> child in ObjectMembers(owner.Value))
            {
                IntentWire i = Decode<IntentWire>(child.Value);
                if (i == null || i.uid != owner.Key) continue;
                list.Add(new AtlasMatchIntent {
                    IntentId = child.Key,
                    ClientCommandId = i.clientCommandId,
                    AccountId = i.uid,
                    SeatId = i.seatId,
                    IntentType = i.type,
                    PayloadJson = i.payload,
                    CreatedAtEpochMs = i.createdAt
                });
            }
        }
        if (list.Count > 0)
        {
            // The host never trusts a caller-supplied seatId. Resolve its
            // owner against the immutable match seat assignment before the
            // gameplay coordinator is allowed to receive a remote command.
            Response authoritative = await Send("GET", StatePath(matchId), token);
            MatchState raw = Decode<MatchState>(authoritative.Body);
            SeatsWire assigned = raw != null ? Decode<SeatsWire>(raw.seatsJson) : null;
            if (!authoritative.Ok || assigned == null || assigned.seats == null)
                return Failure("Match seat authorization data is unavailable.");
            list.RemoveAll(intent =>
            {
                foreach (SeatWire seat in assigned.seats)
                    if (seat != null && seat.uid == intent.AccountId &&
                        seat.seatId == intent.SeatId &&
                        seat.mode == "remotehuman" &&
                        seat.controller == "human" &&
                        seat.connection == "connected" &&
                        !seat.afkLockedOut)
                        return false;
                return true;
            });
        }
        if (list.Count > 0)
        {
            // ACK is persisted before the intent is removed. If deletion lost
            // connectivity, never execute that same gameplay command twice.
            Response acknowledged = await Send("GET", AckRoot(room), token);
            if (!acknowledged.Ok)
                return Failure("RTDB ACK safety read: " + FailReason(acknowledged));
            HashSet<string> done = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string,string> owner in ObjectMembers(acknowledged.Body))
                foreach (KeyValuePair<string,string> child in ObjectMembers(owner.Value))
                    done.Add(owner.Key + ":" + child.Key);
            list.RemoveAll(i => done.Contains(i.AccountId + ":" + i.IntentId));
        }
        list.Sort((a, b) => a.CreatedAtEpochMs.CompareTo(b.CreatedAtEpochMs));
        return new AtlasMatchNetworkResult { Success = true, Intents = list.ToArray() };
    }

    internal static async Task<AtlasMatchNetworkResult> AcknowledgeAsync(
        string matchId, string uid, string token, IEnumerable<string> ids)
    {
        AtlasMatchNetworkResult current = await SnapshotAsync(matchId, uid, token);
        if (!current.Success || current.Snapshot == null) return current;
        if (!current.Snapshot.LocalIsHost)
            return AtlasMatchNetworkResult.Fail("match.error.access_denied", "Only host may ACK.");
        string room = current.Snapshot.LobbyId;
        Response pending = await Send("GET", IntentRoot(room), token);
        if (!pending.Ok) return Failure("RTDB ACK queue read: " + FailReason(pending));
        Dictionary<string,string> ownerByIntent = new Dictionary<string,string>();
        foreach (KeyValuePair<string,string> owner in ObjectMembers(pending.Body))
            foreach (KeyValuePair<string,string> child in ObjectMembers(owner.Value))
                ownerByIntent[child.Key] = owner.Key;
        int processed = 0;
        if (ids != null)
        foreach (string id in ids)
        {
            if (string.IsNullOrEmpty(id) || !ownerByIntent.TryGetValue(id, out string owner)) continue;
            string ack = AckRoot(room) + "/" + PathKey(owner) + "/" + PathKey(id);
            Response set = await Send("PUT", ack, token,
                JsonUtility.ToJson(new AckWire {
                    accepted = true, revision = current.Snapshot.Revision,
                    processedAt = Now, errorCode = ""
                }));
            if (!set.Ok) continue;
            Response removed = await Send("DELETE", IntentRoot(room) + "/" +
                PathKey(owner) + "/" + PathKey(id), token);
            if (removed.Ok) processed++;
        }
        return new AtlasMatchNetworkResult { Success = true, Acknowledged = processed };
    }

    internal static async Task<AtlasMatchNetworkResult> TouchAsync(
        string matchId, string uid, string token)
    {
        AtlasMatchNetworkResult state = await SnapshotAsync(matchId, uid, token);
        if (!state.Success || state.Snapshot == null) return state;
        string path = "matchPresence/" + PathKey(matchId) + "/" + PathKey(uid);
        string value = "{\"online\":true,\"lastSeenAt\":" + Now + "}";
        Response result = await Send("PUT", path, token, value);
        if (!result.Ok) return Failure("RTDB presence: " + FailReason(result));
        return state;
    }

    internal static async Task<AtlasMatchNetworkResult> LeaveAsync(
        string matchId, string uid, string token)
    {
        AtlasMatchNetworkResult state = await SnapshotAsync(matchId, uid, token);
        if (!state.Success || state.Snapshot == null) return state;

        if (state.Snapshot.LocalIsHost)
        {
            // The old Host must never move roomMeta authority and then attempt
            // the matchState transfer with its now-stale token. Instead it
            // publishes one final safe checkpoint with an expired host lease.
            // The connected successor wins the existing authorityEpoch CAS
            // using its own identity, so rules reject every stale Host writer.
            return await PrepareGracefulHostDepartureAsync(
                matchId, uid, token);
        }

        Response result = await Send("PUT", "matchPresence/" + PathKey(matchId) +
            "/" + PathKey(uid), token,
            "{\"online\":false,\"lastSeenAt\":" + Now + "}");
        return result.Ok ? state : Failure("RTDB departure: " + FailReason(result));
    }

    private static async Task<AtlasMatchNetworkResult>
        PrepareGracefulHostDepartureAsync(
            string matchId, string uid, string token)
    {
        long now = Now;
        string matchPath = StatePath(matchId);

        Response matchRead = await Send(
            "GET", matchPath, token, null, null, true);

        MatchState state = Decode<MatchState>(matchRead.Body);
        if (!matchRead.Ok ||
            state == null ||
            string.IsNullOrEmpty(matchRead.ETag))
        {
            return Failure(
                "RTDB graceful handoff match read: " +
                FailReason(matchRead));
        }

        if (!string.Equals(
                state.hostUid,
                uid,
                StringComparison.Ordinal))
        {
            return AtlasMatchNetworkResult.Fail(
                "match.error.access_denied",
                "Only the current Host can arm graceful handoff.");
        }

        SeatsWire roster = Decode<SeatsWire>(state.seatsJson);
        Dictionary<string, PresenceWire> presence =
            await ReadPresenceAsync(matchId, token);

        SeatWire successor =
            FindEligibleAuthoritySeat(
                roster,
                null,
                presence,
                now);

        if (successor == null)
        {
            return AtlasMatchNetworkResult.Fail(
                "match.error.host_migration_not_safe",
                "No connected Remote Human is eligible to become Host.");
        }

        // This write is still performed while the old Host owns roomMeta.
        // Only the lease is retired here; authority itself is NOT transferred.
        // The selected follower must claim roomMeta + authorityEpoch through
        // RecoverCrashedHostAsync with its own authenticated token.
        state.revision++;
        state.updatedAt = now;
        state.hostHeartbeatAt =
            now - HostCrashLeaseMs - 1000L;

        Response armed = await Send(
            "PUT",
            matchPath,
            token,
            JsonUtility.ToJson(state),
            matchRead.ETag);

        if (!armed.Ok)
        {
            if (armed.Status == 412)
            {
                return AtlasMatchNetworkResult.Fail(
                    "match.error.host_migration_not_safe",
                    "The safe checkpoint changed while graceful handoff was arming.");
            }

            return Failure(
                "RTDB graceful handoff checkpoint: " +
                FailReason(armed));
        }

        Response presenceWrite = await Send(
            "PUT",
            "matchPresence/" + PathKey(matchId) +
                "/" + PathKey(uid),
            token,
            "{\"online\":false,\"lastSeenAt\":" + now + "}");

        if (!presenceWrite.Ok)
        {
            Debug.LogWarning(
                "ATB_RTDB_AUTHORITY_HANDOFF armed=true " +
                "host_presence_offline=false cause=" +
                FailReason(presenceWrite));
        }

        string maintenanceKey = matchId + ":" + uid;
        LastMaintenanceCheck.Remove(maintenanceKey);
        LastMaintenanceResult.Remove(maintenanceKey);
        LastHostPresenceWrite.Remove(maintenanceKey);

        Debug.Log(
            "ATB_RTDB_AUTHORITY_HANDOFF armed=true " +
            "successor_slot=" + successor.slotIndex +
            " epoch=" + state.authorityEpoch +
            " takeover=pending_successor_cas");

        return new AtlasMatchNetworkResult
        {
            Success = true,
            Snapshot = ToSnapshot(state, uid),
            Revision = state.revision
        };
    }

    internal static async Task<AtlasMatchNetworkResult> RecoverCrashedHostAsync(
        string matchId, string uid, string token)
    {
        return await TransferAuthorityAsync(matchId, uid, token);
    }

    private static SeatWire FindEligibleAuthoritySeat(
        SeatsWire roster, string uid, Dictionary<string, PresenceWire> presence, long now)
    {
        if (roster == null || roster.seats == null || presence == null) return null;
        SeatWire best = null;
        foreach (SeatWire seat in roster.seats)
        {
            // Presence alone never restores authority to an expired/temporary
            // bot or a disconnected Human. Host maintenance owns reconnection.
            if (seat == null || seat.host || seat.afkLockedOut ||
                seat.mode != "remotehuman" ||
                seat.controller != "human" || seat.connection != "connected" ||
                string.IsNullOrEmpty(seat.uid))
                continue;
            if (!presence.TryGetValue(seat.uid, out PresenceWire p))
                continue;
            bool recent = p != null && p.online &&
                now - p.lastSeenAt >= 0 &&
                now - p.lastSeenAt <= AuthorityCandidatePresenceMs;
            if (!recent) continue;
            if (best == null || seat.slotIndex < best.slotIndex)
                best = seat;
        }
        // Evaluate the WHOLE eligible roster before checking requester UID.
        // Previously every candidate filtered to itself and raced the RTDB CAS:
        // a higher seat could win instead of the deterministic lowest seat.
        return best != null && (uid == null ||
            string.Equals(best.uid, uid, StringComparison.Ordinal)) ? best : null;
    }

    public static bool ValidateHostMigrationElectionContract()
    {
        const long now = 2000000L;
        SeatsWire roster = new SeatsWire { seats = new[] {
            new SeatWire { uid = "host", host = true, slotIndex = 0,
                mode = "hostlocal", controller = "human", connection = "connected" },
            // Intentionally out of seat order to validate deterministic election.
            new SeatWire { uid = "b", slotIndex = 2, mode = "remotehuman",
                controller = "human", connection = "connected" },
            new SeatWire { uid = "expired", slotIndex = -2, mode = "remotehuman",
                controller = "permanent_bot", connection = "reconnect_expired" },
            new SeatWire { uid = "temporary", slotIndex = -1, mode = "remotehuman",
                controller = "temporary_bot", connection = "reconnecting" },
            new SeatWire { uid = "a", slotIndex = 1, mode = "remotehuman",
                controller = "human", connection = "connected" }
        }};
        Dictionary<string, PresenceWire> online = new Dictionary<string, PresenceWire> {
            { "a", new PresenceWire { online = true, lastSeenAt = now - 500 } },
            { "b", new PresenceWire { online = true, lastSeenAt = now - 1000 } },
            { "expired", new PresenceWire { online = true, lastSeenAt = now } },
            { "temporary", new PresenceWire { online = true, lastSeenAt = now } }
        };
        SeatWire elected = FindEligibleAuthoritySeat(roster, null, online, now);
        if (elected == null || elected.uid != "a" ||
            FindEligibleAuthoritySeat(roster, "a", online, now) == null ||
            FindEligibleAuthoritySeat(roster, "b", online, now) != null)
            return false;
        online["a"].online = false;
        elected = FindEligibleAuthoritySeat(roster, null, online, now);
        if (elected == null || elected.uid != "b" ||
            FindEligibleAuthoritySeat(roster, "a", online, now) != null)
            return false;
        // Stale heartbeat, retired bot, AFK and reconnecting seats cannot win.
        online["b"].lastSeenAt = now - AuthorityCandidatePresenceMs - 1;
        if (FindEligibleAuthoritySeat(roster, null, online, now) != null)
            return false;
        online["b"].lastSeenAt = now;
        roster.seats[1].afkLockedOut = true;
        if (FindEligibleAuthoritySeat(roster, null, online, now) != null)
            return false;
        roster.seats[1].afkLockedOut = false;
        roster.seats[1].connection = "reconnecting";
        return FindEligibleAuthoritySeat(roster, null, online, now) == null;
    }

    // Owner-safe, no-network regression: genuine second successor selection.
    public static bool ValidateMultiHopAuthorityContract()
    {
        long now = 4000000L;
        SeatsWire roster = new SeatsWire { seats = new[] {
            new SeatWire { uid="old", seatId="seat_0", slotIndex=0, host=true,
                mode="hostlocal", controller="human", connection="connected" },
            new SeatWire { uid="second", seatId="seat_1", slotIndex=1,
                mode="remotehuman", controller="human", connection="connected" },
            new SeatWire { uid="third", seatId="seat_2", slotIndex=2,
                mode="remotehuman", controller="human", connection="connected" }
        }};
        var presence = new Dictionary<string, PresenceWire> {
            { "second", new PresenceWire { online=true, lastSeenAt=now } },
            { "third", new PresenceWire { online=true, lastSeenAt=now } }
        };
        SeatWire first = FindEligibleAuthoritySeat(roster, null, presence, now);
        if (first == null || first.uid != "second") return false;
        ApplyAuthoritySeatTransfer(roster, "old", first.uid, now);
        if (!roster.seats[1].host || roster.seats[0].controller != "temporary_bot" ||
            roster.seats[0].seatId != "seat_0") return false;
        // Rejoining an old Host cannot steal authority back while the new one
        // remains alive, and reconnecting Temporary Bots cannot self-elect.
        if (FindEligibleAuthoritySeat(roster, "old", presence, now) != null) return false;
        presence["second"].online = false;
        SeatWire next = FindEligibleAuthoritySeat(roster, null, presence, now);
        if (next == null || next.uid != "third") return false;
        ApplyAuthoritySeatTransfer(roster, "second", next.uid, now+3000);
        int hostCount = 0;
        foreach (SeatWire seat in roster.seats) if (seat != null && seat.host) hostCount++;
        return hostCount == 1 && roster.seats[2].host &&
            roster.seats[1].controller == "temporary_bot" &&
            roster.seats[2].seatId == "seat_2" &&
            FindEligibleAuthoritySeat(roster, null, new Dictionary<string, PresenceWire>(), now) == null;
    }

    // Read-only authority proof for the two-client QA. Read the RTDB room lock
    // and match frame separately, not only the client's displayed HUD role.
    internal static async Task<AtlasMatchNetworkResult> InspectAuthorityChainAsync(
        string matchId, string uid, string token)
    {
        if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(uid))
            return Failure("Missing authenticated match for authority proof.");
        Response read = await Send("GET", StatePath(matchId), token);
        MatchState match = read.Ok ? Decode<MatchState>(read.Body) : null;
        if (match == null || !read.Ok || match.matchId != matchId)
            return Failure("Authority proof match state unavailable.");
        Response anchor = await Send("GET", "roomMeta/" + PathKey(match.lobbyId), token);
        RoomMetaWire meta = anchor.Ok ? Decode<RoomMetaWire>(anchor.Body) : null;
        if (!anchor.Ok || meta == null)
            return Failure("Authority proof roomMeta unavailable.");
        SeatsWire seats = Decode<SeatsWire>(match.seatsJson);
        int hostSeats = 0, hostSlot = -1;
        bool hostSlotsAreValid = true;
        if (seats != null && seats.seats != null)
            foreach (SeatWire seat in seats.seats)
                if (seat != null && seat.host)
                {
                    hostSeats++; hostSlot = seat.slotIndex;
                    if (seat.uid != match.hostUid) hostSlotsAreValid = false;
                }
        bool epochOk = match.authorityEpoch > 0 &&
            match.authorityEpoch == meta.authorityEpoch;
        bool ownerOk = string.Equals(match.hostUid, meta.hostUid, StringComparison.Ordinal);
        bool matchOk = string.Equals(meta.activeMatchId, matchId, StringComparison.Ordinal);
        bool seatsOk = hostSeats == 1 && hostSlotsAreValid;
        Response roomRead = await Send("GET", "roomState/" + PathKey(match.lobbyId), token);
        RoomWire lobbyWire = roomRead.Ok ? Decode<RoomWire>(roomRead.Body) : null;
        AtlasLobbySnapshot lobby = lobbyWire != null
            ? Decode<AtlasLobbySnapshot>(lobbyWire.snapshotJson) : null;
        bool lobbyOk = lobby != null &&
            string.Equals(lobby.HostAccountId, match.hostUid, StringComparison.Ordinal);
        bool okay = epochOk && ownerOk && matchOk && seatsOk && lobbyOk && !meta.closed;
        return new AtlasMatchNetworkResult {
            Success = okay,
            Snapshot = ToSnapshot(match, uid),
            Revision = match.revision,
            TechnicalMessage = "epoch=" + match.authorityEpoch +
                " host_slot=" + hostSlot +
                " epoch_consistent=" + epochOk + " host_uid_consistent=" + ownerOk +
                " match_anchor_consistent=" + matchOk + " single_host_seat=" + seatsOk +
                " lobby_host_consistent=" + lobbyOk + " closed=" + meta.closed,
            ErrorLocalizationKey = okay ? "" : "match.error.host_migration_not_safe"
        };
    }

    private static async Task<Dictionary<string, PresenceWire>> ReadPresenceAsync(
        string matchId, string token)
    {
        Dictionary<string, PresenceWire> result =
            new Dictionary<string, PresenceWire>(StringComparer.Ordinal);
        Response read = await Send("GET",
            "matchPresence/" + PathKey(matchId), token);
        if (!read.Ok) return result;
        foreach (KeyValuePair<string,string> item in ObjectMembers(read.Body))
        {
            PresenceWire p = Decode<PresenceWire>(item.Value);
            if (p != null) result[item.Key] = p;
        }
        return result;
    }

    private static void ApplyAuthoritySeatTransfer(
        SeatsWire roster, string oldHostUid, string newHostUid, long now)
    {
        if (roster == null || roster.seats == null) return;
        foreach (SeatWire seat in roster.seats)
        {
            if (seat == null) continue;
            bool wasHost = seat.host || seat.uid == oldHostUid;
            bool becomesHost = seat.uid == newHostUid;
            if (wasHost && !becomesHost)
            {
                // The old human keeps the exact seat/economy/property state.
                // Only input ownership changes to a temporary bot for 5 minutes.
                seat.host = false;
                seat.mode = "remotehuman";
                seat.controller = "temporary_bot";
                seat.connection = "reconnecting";
                seat.disconnectedAt = now;
                seat.reconnectExpiresAt = now + ReconnectWindowMs;
                seat.removalReason = "host_disconnected";
            }
            if (becomesHost)
            {
                seat.host = true;
                seat.mode = "hostlocal";
                seat.controller = "human";
                seat.connection = "connected";
                seat.afkLockedOut = false;
                seat.disconnectedAt = 0;
                seat.reconnectExpiresAt = 0;
                seat.removalReason = "";
            }
        }
    }

    private static async Task<bool> RepairLobbyAuthorityAsync(
        MatchState state, string oldHostUid, string newHostUid, string token)
    {
        if (state == null || string.IsNullOrEmpty(state.lobbyId)) return false;

        // Roles are presentation/lobby metadata. The room authority lock has
        // already changed, so only the new host is allowed to repair them.
        if (string.IsNullOrEmpty(oldHostUid))
        {
            SeatsWire migrated = Decode<SeatsWire>(state.seatsJson);
            if (migrated != null && migrated.seats != null)
                foreach (SeatWire seat in migrated.seats)
                    if (seat != null && seat.uid != newHostUid &&
                        seat.removalReason == "host_disconnected")
                    {
                        oldHostUid = seat.uid;
                        break;
                    }
        }
        if (!string.IsNullOrEmpty(oldHostUid) && oldHostUid != newHostUid)
            await Send("PATCH", "roomMembers/" + PathKey(state.lobbyId) +
                "/" + PathKey(oldHostUid), token, "{\"role\":\"remoteHuman\"}");
        await Send("PATCH", "roomMembers/" + PathKey(state.lobbyId) +
            "/" + PathKey(newHostUid), token, "{\"role\":\"host\"}");

        Response roomRead = await Send("GET",
            "roomState/" + PathKey(state.lobbyId), token, null, null, true);
        RoomWire wire = Decode<RoomWire>(roomRead.Body);
        if (!roomRead.Ok || wire == null || string.IsNullOrEmpty(roomRead.ETag))
            return false;
        AtlasLobbySnapshot lobby = Decode<AtlasLobbySnapshot>(wire.snapshotJson);
        if (lobby == null) return false;
        bool lobbyChanged = !string.Equals(
            lobby.HostAccountId, newHostUid, StringComparison.Ordinal);
        lobby.HostAccountId = newHostUid;
        if (lobby.Members != null)
        {
            foreach (AtlasLobbyMemberSnapshot member in lobby.Members)
            {
                if (member == null) continue;
                bool isNewHost = string.Equals(
                    member.AccountId, newHostUid, StringComparison.Ordinal);
                bool isOldHost = string.Equals(
                    member.AccountId, oldHostUid, StringComparison.Ordinal);
                if (member.IsHost != isNewHost)
                    lobbyChanged = true;
                member.IsHost = isNewHost;
                if (isNewHost)
                {
                    if (member.SeatMode != AtlasLobbySeatMode.HostLocal ||
                        member.ControllerKind != AtlasSeatControllerKind.Human ||
                        member.ConnectionState != AtlasSeatConnectionState.Connected)
                        lobbyChanged = true;
                    member.SeatMode = AtlasLobbySeatMode.HostLocal;
                    member.LocalOwnerAccountId = newHostUid;
                    member.ControllerKind = AtlasSeatControllerKind.Human;
                    member.ConnectionState = AtlasSeatConnectionState.Connected;
                }
                else if (isOldHost)
                {
                    if (member.SeatMode != AtlasLobbySeatMode.RemoteHuman ||
                        member.ControllerKind != AtlasSeatControllerKind.TemporaryBot ||
                        member.ConnectionState != AtlasSeatConnectionState.Reconnecting)
                        lobbyChanged = true;
                    member.SeatMode = AtlasLobbySeatMode.RemoteHuman;
                    member.ControllerKind = AtlasSeatControllerKind.TemporaryBot;
                    member.ConnectionState = AtlasSeatConnectionState.Reconnecting;
                }
            }
        }
        if (!lobbyChanged)
            return true;
        wire.revision = Math.Max(wire.revision + 1, Now);
        wire.eventSequence = Math.Max(wire.eventSequence, wire.revision);
        wire.updatedAt = Now;
        lobby.TransportRevision = wire.revision;
        wire.snapshotJson = JsonUtility.ToJson(lobby);
        Response saved = await Send("PUT",
            "roomState/" + PathKey(state.lobbyId), token,
            JsonUtility.ToJson(wire), roomRead.ETag);
        return saved.Ok;
    }

    private static async Task<AtlasMatchNetworkResult> TransferAuthorityAsync(
        string matchId, string requesterUid, string token)
    {
        long now = Now;
        string matchPath = StatePath(matchId);
        Response matchRead = await Send("GET", matchPath, token, null, null, true);
        MatchState state = Decode<MatchState>(matchRead.Body);
        if (!matchRead.Ok || state == null || string.IsNullOrEmpty(matchRead.ETag))
            return Failure("RTDB authority match read: " + FailReason(matchRead));

        string oldHostUid = state.hostUid ?? "";
        if (oldHostUid == requesterUid)
        {
            await RepairLobbyAuthorityAsync(state, "", requesterUid, token);
            return new AtlasMatchNetworkResult {
                Success = true, Snapshot = ToSnapshot(state, requesterUid),
                Revision = state.revision
            };
        }
        SeatsWire roster = Decode<SeatsWire>(state.seatsJson);
        Dictionary<string, PresenceWire> presence =
            await ReadPresenceAsync(matchId, token);

        SeatWire successor =
            FindEligibleAuthoritySeat(
                roster,
                requesterUid,
                presence,
                now);
        if (successor == null)
            return AtlasMatchNetworkResult.Fail(
                "match.error.host_migration_not_safe",
                "No connected Remote Human is eligible to become host.");

        string newHostUid = successor.uid;
        Response metaRead = await Send("GET",
            "roomMeta/" + PathKey(state.lobbyId), token, null, null, true);
        RoomMetaWire meta = Decode<RoomMetaWire>(metaRead.Body);
        if (!metaRead.Ok || meta == null || string.IsNullOrEmpty(metaRead.ETag))
            return Failure("RTDB authority room read: " + FailReason(metaRead));
        if (meta.closed ||
            !string.Equals(meta.activeMatchId, matchId, StringComparison.Ordinal))
            return AtlasMatchNetworkResult.Fail(
                "match.error.host_migration_not_safe",
                "Room authority anchor no longer matches this match.");

        bool resumePendingSync =
            meta.hostUid == requesterUid &&
            state.hostUid != requesterUid &&
            meta.authorityEpoch == state.authorityEpoch + 1;

        int nextEpoch;
        if (resumePendingSync)
        {
            nextEpoch = meta.authorityEpoch;
        }
        else
        {
            if (meta.hostUid != oldHostUid ||
                meta.authorityEpoch != state.authorityEpoch)
                return AtlasMatchNetworkResult.Fail(
                    "match.error.host_migration_not_safe",
                    "Another client already changed room authority.");

            if (now - state.hostHeartbeatAt < HostCrashLeaseMs)
                return AtlasMatchNetworkResult.Fail(
                    "match.error.host_migration_not_safe",
                    "Host heartbeat lease is still valid.");

            nextEpoch = Math.Max(1, state.authorityEpoch + 1);
            meta.hostUid = newHostUid;
            meta.authorityEpoch = nextEpoch;
            meta.updatedAt = now;
            Response metaWrite = await Send("PUT",
                "roomMeta/" + PathKey(state.lobbyId), token,
                JsonUtility.ToJson(meta), metaRead.ETag);
            if (!metaWrite.Ok)
            {
                if (metaWrite.Status == 412 || metaWrite.Status == 401 ||
                    metaWrite.Status == 403)
                    return AtlasMatchNetworkResult.Fail(
                        "match.error.host_migration_not_safe",
                        "Another client won the authority CAS.");
                return Failure("RTDB authority lock write: " + FailReason(metaWrite));
            }
        }

        // roomMeta is the authority lock. Once it moves, rules immediately
        // reject stale old-host publishes, intents cleanup and lobby writes.
        ApplyAuthoritySeatTransfer(roster, oldHostUid, newHostUid, now);
        state.hostUid = newHostUid;
        state.authorityEpoch = nextEpoch;
        state.seatsJson = JsonUtility.ToJson(roster);
        state.revision++;
        state.updatedAt = now;
        state.hostHeartbeatAt = now;
        Response matchWrite = await Send("PUT", matchPath, token,
            JsonUtility.ToJson(state), matchRead.ETag);
        if (!matchWrite.Ok)
        {
            // Never roll the lock back to the stale host. The elected client
            // retries this exact synchronization using the same room epoch.
            Debug.LogWarning("ATB_RTDB_AUTHORITY_SYNC pending=true epoch=" +
                nextEpoch + " cause=" + FailReason(matchWrite));
            return AtlasMatchNetworkResult.Fail(
                "match.error.host_migration_not_safe",
                "Authority lock moved, but match synchronization must retry.");
        }

        await Send("PUT", "matchPresence/" + PathKey(matchId) +
            "/" + PathKey(newHostUid), token,
            "{\"online\":true,\"lastSeenAt\":" + now + "}");

        if (requesterUid == newHostUid &&
            await RepairLobbyAuthorityAsync(state, oldHostUid, newHostUid, token))
            AuthorityRepairCompleted.Add(matchId + ":" + nextEpoch);

        string oldKey = matchId + ":" + oldHostUid;
        string newKey = matchId + ":" + newHostUid;
        LastMaintenanceCheck.Remove(oldKey);
        LastMaintenanceResult.Remove(oldKey);
        LastHostPresenceWrite.Remove(oldKey);
        LastMaintenanceCheck.Remove(newKey);
        LastMaintenanceResult.Remove(newKey);
        LastHostPresenceWrite.Remove(newKey);

        Debug.Log(
            "ATB_RTDB_AUTHORITY_TRANSFER committed=true mode=takeover " +
            "epoch=" + nextEpoch);
        return new AtlasMatchNetworkResult {
            Success = true,
            Snapshot = ToSnapshot(state, requesterUid),
            Revision = state.revision
        };
    }

#if UNITY_EDITOR
    // Explicit Live Spark QA only. Runs the SAME expiry transition used by
    // HostMaintenanceAsync, but advances time for ONE disconnected test seat.
    // Never alters the production 5-minute lease or other users' rooms.
    public static async Task<AtlasMatchNetworkResult>
        EditorExpireSingleTemporaryHumanForQaAsync(
            string matchId, string uid, string token)
    {
        if (string.IsNullOrWhiteSpace(matchId) ||
            string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(token))
            return Failure("qa_identity_or_match_missing");

        string matchPath = StatePath(matchId);
        Response read = await Send("GET", matchPath, token, null, null, true);
        MatchState state = Decode<MatchState>(read.Body);
        if (!read.Ok || state == null || string.IsNullOrEmpty(read.ETag) ||
            state.matchId != matchId || state.hostUid != uid ||
            state.status != "active")
            return Failure("qa_requires_current_live_match_host");

        Response roomRead = await Send("GET",
            "roomMeta/" + PathKey(state.lobbyId), token);
        RoomMetaWire room = Decode<RoomMetaWire>(roomRead.Body);
        if (!roomRead.Ok || room == null || room.closed ||
            room.hostUid != uid || room.activeMatchId != matchId ||
            room.authorityEpoch != state.authorityEpoch)
            return Failure("qa_authority_epoch_anchor_mismatch");

        SeatsWire roster = Decode<SeatsWire>(state.seatsJson);
        if (roster == null || roster.seats == null)
            return Failure("qa_roster_not_available");
        SeatWire candidate = null;
        int pending = 0;
        long now = Now;
        foreach (SeatWire seat in roster.seats)
        {
            if (seat == null || seat.host || seat.afkLockedOut ||
                seat.mode != "remotehuman" || seat.controller != "temporary_bot" ||
                seat.connection != "reconnecting" ||
                seat.disconnectedAt <= 0 || seat.reconnectExpiresAt <= now ||
                string.IsNullOrEmpty(seat.uid)) continue;
            candidate = seat;
            pending++;
        }
        // Never guess which Human to expire in a 3- or 4-player match.
        if (pending != 1)
            return Failure("qa_requires_exactly_one_unexpired_temporary_human");

        Response presenceRead = await Send("GET",
            "matchPresence/" + PathKey(matchId) + "/" +
            PathKey(candidate.uid), token);
        if (!presenceRead.Ok)
            return Failure("qa_presence_check_failed");
        PresenceWire presence = Decode<PresenceWire>(presenceRead.Body);
        if (presence != null && presence.online &&
            presence.lastSeenAt > candidate.disconnectedAt &&
            now >= presence.lastSeenAt &&
            now - presence.lastSeenAt <= RemotePresenceTimeoutMs)
            return Failure("qa_remote_human_already_reconnected");

        long simulatedNow = candidate.reconnectExpiresAt + 1L;
        if (!AdvanceRemoteSeat(candidate, null, simulatedNow, state.createdAt) ||
            candidate.controller != "permanent_bot" ||
            candidate.connection != "reconnect_expired")
            return Failure("qa_canonical_expiry_transition_rejected");

        state.seatsJson = JsonUtility.ToJson(roster);
        state.revision++;
        state.updatedAt = now;
        state.hostHeartbeatAt = now;
        Response committed = await Send("PUT", matchPath, token,
            JsonUtility.ToJson(state), read.ETag);
        if (!committed.Ok)
            return Failure("qa_match_cas_rejected_" + FailReason(committed));
        // This is a simulated-deadline test, not proof of a 5-minute wall-clock wait.
        return new AtlasMatchNetworkResult {
            Success = true,
            Snapshot = ToSnapshot(state, uid),
            Revision = state.revision
        };
    }
#endif

    private const long RemotePresenceTimeoutMs = 20000L;
    private const long ReconnectWindowMs = 5L * 60L * 1000L;

    // A single canonical transition helper is used by live host maintenance
    // AND Editor regression QA. No GameObject or second bot controller here.
    private static bool AdvanceRemoteSeat(SeatWire seat, PresenceWire presence,
        long now, long matchCreatedAt)
    {
        if (seat == null || seat.host || seat.mode != "remotehuman" ||
            seat.afkLockedOut || string.IsNullOrEmpty(seat.uid)) return false;
        bool recent = presence != null && presence.online &&
            presence.lastSeenAt > 0 &&
            now - Math.Min(presence.lastSeenAt, now) <= RemotePresenceTimeoutMs;
        if (seat.controller == "human" && seat.connection == "connected")
        {
            // Give newly admitted Humans a grace period to send their first
            // heartbeat after the host starts the match.
            if (recent || now - matchCreatedAt < RemotePresenceTimeoutMs)
                return false;
            seat.controller = "temporary_bot";
            seat.connection = "reconnecting";
            seat.disconnectedAt = now;
            seat.reconnectExpiresAt = now + ReconnectWindowMs;
            seat.removalReason = "connection_lost";
            return true;
        }
        if (seat.controller != "temporary_bot" ||
            seat.connection != "reconnecting") return false;
        // Expiry wins even if a reconnect arrives too late.
        if (seat.reconnectExpiresAt > 0 && now >= seat.reconnectExpiresAt)
        {
            seat.controller = "permanent_bot";
            seat.connection = "reconnect_expired";
            seat.reconnectExpiresAt = 0;
            seat.removalReason = "reconnect_expired";
            return true;
        }
        // A stale 'online:true' recorded before the disconnection MUST NOT
        // reclaim the Human seat. The old owner must send a new presence.
        if (recent && seat.disconnectedAt > 0 &&
            presence.lastSeenAt > seat.disconnectedAt)
        {
            seat.controller = "human";
            seat.connection = "connected";
            seat.disconnectedAt = 0;
            seat.reconnectExpiresAt = 0;
            seat.removalReason = "";
            return true;
        }
        return false;
    }

    public static bool ValidatePresenceTransitionContract()
    {
        const long now = 1000000L;
        SeatWire test = new SeatWire {
            uid="remote",mode="remotehuman",controller="human",
            connection="connected",slotIndex=1
        };
        if (AdvanceRemoteSeat(test,new PresenceWire {online=true,
                lastSeenAt=now-1000L},now,now-60000L)) return false;
        if (!AdvanceRemoteSeat(test,new PresenceWire {online=false,
                lastSeenAt=now},now,now-60000L) ||
            test.controller!="temporary_bot" ||
            test.reconnectExpiresAt!=now+ReconnectWindowMs) return false;
        if (AdvanceRemoteSeat(test,new PresenceWire {online=true,
                lastSeenAt=now},now+2000L,now-60000L)) return false;
        if (!AdvanceRemoteSeat(test,new PresenceWire {online=true,
                lastSeenAt=now+3000L},now+4000L,now-60000L) ||
            test.controller!="human") return false;
        if (!AdvanceRemoteSeat(test,new PresenceWire {online=false,
                lastSeenAt=now+5000L},now+6000L,now-60000L)) return false;
        if (!AdvanceRemoteSeat(test,null,now+6000L+ReconnectWindowMs,
                now-60000L) || test.controller!="permanent_bot") return false;
        if (AdvanceRemoteSeat(test,new PresenceWire {online=true,
                lastSeenAt=now+6000L+ReconnectWindowMs+1},
                now+6000L+ReconnectWindowMs+10,now-60000L)) return false;
        return true;
    }

    internal static async Task<AtlasMatchNetworkResult> HostMaintenanceAsync(
        string matchId, string uid, string token)
    {
        string key = matchId + ":" + uid;
        long now = Now;
        // Both legacy sweep entrypoints converge here, with a bounded cache.
        if (LastMaintenanceCheck.TryGetValue(key, out long checkedAt) &&
            now-checkedAt < 4000L &&
            LastMaintenanceResult.TryGetValue(key, out AtlasMatchNetworkResult cached) &&
            cached != null && cached.Success) return cached;

        string path = StatePath(matchId);
        Response read = await Send("GET",path,token,null,null,true);
        MatchState state = Decode<MatchState>(read.Body);
        if (!read.Ok || state == null || string.IsNullOrEmpty(read.ETag))
            return Failure("RTDB maintenance read: " + FailReason(read));
        if (state.hostUid != uid)
            return AtlasMatchNetworkResult.Fail("match.error.access_denied","Host required.");

        // Backfill/repair the room authority anchor. This makes matches that
        // started before the migration-capable client safe after rules update.
        Response metaRead = await Send("GET",
            "roomMeta/" + PathKey(state.lobbyId), token);
        RoomMetaWire meta = Decode<RoomMetaWire>(metaRead.Body);
        if (metaRead.Ok && meta != null &&
            (meta.hostUid != uid ||
             meta.authorityEpoch != state.authorityEpoch ||
             !string.Equals(meta.activeMatchId, matchId, StringComparison.Ordinal)))
        {
            if (meta.hostUid == uid)
            {
                Response anchored = await Send("PATCH",
                    "roomMeta/" + PathKey(state.lobbyId), token,
                    "{\"activeMatchId\":\"" + EscapeJson(matchId) +
                    "\",\"authorityEpoch\":" + state.authorityEpoch +
                    ",\"updatedAt\":" + now + "}");
                if (anchored.Ok)
                    meta.activeMatchId = matchId;
            }
        }
        string repairKey = matchId + ":" + state.authorityEpoch;
        if (!AuthorityRepairCompleted.Contains(repairKey))
        {
            if (await RepairLobbyAuthorityAsync(state, "", uid, token))
                AuthorityRepairCompleted.Add(repairKey);
        }

        SeatsWire roster = Decode<SeatsWire>(state.seatsJson);
        if (roster == null || roster.seats == null)
            return Failure("RTDB maintenance seats are missing.");
        bool hasRemote = false;
        foreach (SeatWire seat in roster.seats)
            hasRemote |= seat != null && seat.mode == "remotehuman" &&
                !seat.afkLockedOut && seat.controller != "permanent_bot";

        Dictionary<string,PresenceWire> presenceByUid =
            new Dictionary<string,PresenceWire>(StringComparer.Ordinal);
        if (hasRemote)
        {
            Response presence = await Send("GET","matchPresence/"+PathKey(matchId),token);
            if (!presence.Ok) return Failure("RTDB remote presence read: " + FailReason(presence));
            foreach (KeyValuePair<string,string> item in ObjectMembers(presence.Body))
            {
                PresenceWire value = Decode<PresenceWire>(item.Value);
                if (value != null) presenceByUid[item.Key] = value;
            }
        }
        bool changed = false;
        foreach (SeatWire seat in roster.seats)
        {
            if (seat == null || !hasRemote) continue;
            presenceByUid.TryGetValue(seat.uid ?? "",out PresenceWire presence);
            changed |= AdvanceRemoteSeat(seat,presence,now,state.createdAt);
        }
        if (changed)
        {
            state.seatsJson = JsonUtility.ToJson(roster);
            state.revision++;
            state.updatedAt = now;
            state.hostHeartbeatAt = now;
            Response commit = await Send("PUT",path,token,
                JsonUtility.ToJson(state),read.ETag);
            if (!commit.Ok)
                return AtlasMatchNetworkResult.Fail("match.error.stale_client_state",
                    "RTDB seat transition CAS lost; retry next maintenance pass: " +
                    FailReason(commit));
        }
        // A single host heartbeat for two maintenance callers. Never rewrite
        // the full match frame when only the lease must be extended.
        if (!LastHostPresenceWrite.TryGetValue(key,out long previous) ||
            now-previous >= 10000L)
        {
            Response own = await Send("PUT","matchPresence/"+PathKey(matchId)+
                "/"+PathKey(uid),token,
                "{\"online\":true,\"lastSeenAt\":"+now+"}");
            if (!own.Ok) return Failure("RTDB host presence write: " + FailReason(own));
            if (!changed)
            {
                Response heartbeat = await Send("PATCH",path,token,
                    "{\"hostHeartbeatAt\":"+now+",\"updatedAt\":"+now+"}");
                if (!heartbeat.Ok)
                    return Failure("RTDB host heartbeat write: " + FailReason(heartbeat));
            }
            LastHostPresenceWrite[key] = now;
        }
        AtlasMatchNetworkResult result = new AtlasMatchNetworkResult {
            Success=true, Snapshot=ToSnapshot(state,uid)
        };
        LastMaintenanceCheck[key]=now;
        LastMaintenanceResult[key]=result;
        return result;
    }

    internal static async Task<AtlasMatchNetworkResult> HostRemoveAfkAsync(
        string matchId, string uid, string token, int slotIndex)
    {
        string path = StatePath(matchId);
        Response read = await Send("GET", path, token, null, null, true);
        MatchState state = Decode<MatchState>(read.Body);
        if (!read.Ok || state == null) return Failure("RTDB AFK read: " + FailReason(read));
        if (state.hostUid != uid)
            return AtlasMatchNetworkResult.Fail("match.error.access_denied", "Host required for AFK removal.");
        SeatsWire seats = Decode<SeatsWire>(state.seatsJson);
        if (seats == null || seats.seats == null)
            return Failure("No match seats for AFK removal.");
        bool changed = false;
        foreach (SeatWire seat in seats.seats)
        {
            if (seat == null || seat.slotIndex != slotIndex || seat.host ||
                (seat.mode != "remotehuman" && seat.mode != "localhuman") ||
                seat.afkLockedOut) continue;
            // Closing the Human input path alone leaves a seat that cannot
            // roll. Atomically switch both the wire controller and lock-out.
            seat.afkLockedOut = true;
            seat.controller = "permanent_bot";
            seat.removalReason = "afk";
            seat.connection = "afk_removed";
            seat.reconnectExpiresAt = 0;
            seat.disconnectedAt = Now;
            changed = true;
        }
        if (!changed)
            return new AtlasMatchNetworkResult { Success = true, Snapshot = ToSnapshot(state, uid) };
        state.seatsJson = JsonUtility.ToJson(seats);
        state.revision++;
        state.updatedAt = Now;
        Response updated = await Send("PUT", path, token,
            JsonUtility.ToJson(state), read.ETag);
        return updated.Ok
            ? new AtlasMatchNetworkResult { Success = true, Snapshot = ToSnapshot(state, uid) }
            : Failure("RTDB AFK CAS rejected: " + FailReason(updated));
    }

    internal static async Task<AtlasMatchNetworkResult> HostRematchAsync(
        string matchId, string uid, string token)
    {
        AtlasMatchNetworkResult current = await SnapshotAsync(matchId, uid, token);
        if (!current.Success || current.Snapshot == null) return current;
        if (!current.Snapshot.LocalIsHost)
            return AtlasMatchNetworkResult.Fail("match.error.access_denied", "Host required.");
        if (!string.Equals(current.Snapshot.Phase,"match_complete",StringComparison.Ordinal) ||
            !string.Equals(current.Snapshot.Status,"complete",StringComparison.Ordinal))
            return AtlasMatchNetworkResult.Fail("match.error.not_complete",
                "Cannot reset an active match to Waiting before match completion.");
        // Read the room ETag for an atomic rematch transition. A delayed
        // concurrent host update must not overwrite a newer room state.
        Response read = await Send("GET", "roomState/" + PathKey(current.Snapshot.LobbyId), token,
            null, null, true);
        RoomWire state = Decode<RoomWire>(read.Body);
        if (!read.Ok || state == null)
            return Failure("RTDB lobby was not available for rematch.");
        AtlasLobbySnapshot lobby = Decode<AtlasLobbySnapshot>(state.snapshotJson);
        bool retryCleanup = lobby != null &&
            lobby.LifecycleState == AtlasRoomLifecycleState.Waiting &&
            string.IsNullOrEmpty(lobby.MatchId);
        if (lobby == null || (!retryCleanup && lobby.MatchId != matchId))
            return Failure("The room has already moved to a different match.");
        if (!retryCleanup)
        {
            lobby.LifecycleState = AtlasRoomLifecycleState.Waiting;
            lobby.MatchId = string.Empty;
            lobby.StartEventId = string.Empty;
            lobby.StartCountdownEndsAtEpochMs = 0;
            foreach (AtlasLobbyMemberSnapshot member in lobby.Members)
            {
                if (member == null || member.SeatMode != AtlasLobbySeatMode.RemoteHuman)
                    continue;
                member.ReadyForRevision = 0;
            }
            state.revision = Math.Max(state.revision + 1, Now);
            state.eventSequence = Math.Max(state.eventSequence, state.revision);
            state.updatedAt = Now;
            state.phase = "waiting";
            // Waiting must carry the SAME higher transport revision as RTDB.
            lobby.TransportRevision = state.revision;
            state.snapshotJson = JsonUtility.ToJson(lobby);
            Response saved = await Send("PUT", "roomState/" + PathKey(lobby.LobbyId), token,
                JsonUtility.ToJson(state), read.ETag);
            if (!saved.Ok) return Failure("RTDB rematch lobby reset: " + FailReason(saved));
        }
        // On a partial cleanup failure the old completed match remains.
        // A retry can finish deleting its records without mutating the new
        // Waiting lobby revision a second time.
        // Once the canonical lobby has returned to Waiting, clean ONLY its
        // previous ephemeral match session. This room's invite code survives.
        Response presenceDeleted = await Send("DELETE", "matchPresence/" + PathKey(matchId), token);
        Response intentsDeleted = await Send("DELETE", IntentRoot(lobby.LobbyId), token);
        Response acksDeleted = await Send("DELETE", AckRoot(lobby.LobbyId), token);
        // Delete the parent match last: presence cleanup rules depend on it.
        Response matchDeleted = presenceDeleted.Ok && intentsDeleted.Ok && acksDeleted.Ok
            ? await Send("DELETE", "matchState/" + PathKey(matchId), token)
            : new Response { Status = 0 };
        bool clean = presenceDeleted.Ok && intentsDeleted.Ok && acksDeleted.Ok && matchDeleted.Ok;
        if (!clean)
            Debug.LogWarning("ATB_RTDB_REMATCH_CLEANUP pending=true presence=" +
                presenceDeleted.Status + " intents=" + intentsDeleted.Status +
                " acks=" + acksDeleted.Status + " match=" + matchDeleted.Status);
        Response authorityCleared = await Send("PATCH",
            "roomMeta/" + PathKey(lobby.LobbyId), token,
            "{\"activeMatchId\":\"\",\"updatedAt\":" + Now + "}");
        if (!authorityCleared.Ok)
            Debug.LogWarning("ATB_RTDB_REMATCH_CLEANUP stage=authority_anchor http=" +
                authorityCleared.Status);
        clean = clean && authorityCleared.Ok;
        string maintenanceKey = matchId + ":" + uid;
        LastMaintenanceCheck.Remove(maintenanceKey);
        LastMaintenanceResult.Remove(maintenanceKey);
        LastHostPresenceWrite.Remove(maintenanceKey);
        AuthorityRepairCompleted.Remove(matchId + ":" + current.Snapshot.AuthorityEpoch);
        if (!clean)
            return Failure("Rematch lobby is Waiting, but cleanup of its old match " +
                "failed. Do not start another match until cleanup succeeds.");
        return current;
    }

    private static AtlasMatchNetworkSnapshot ToSnapshot(MatchState state, string uid)
    {
        AtlasMatchNetworkSnapshot result = new AtlasMatchNetworkSnapshot {
            MatchId = state.matchId,
            LobbyId = state.lobbyId,
            Status = state.status,
            Revision = state.revision,
            Phase = state.phase,
            TurnSeatId = state.turnSeatId,
            EventSequence = state.eventSequence,
            SnapshotJson = state.snapshotJson ?? "",
            UpdatedAtEpochMs = state.updatedAt,
            HostHeartbeatAtEpochMs = state.hostHeartbeatAt,
            AuthorityEpoch = state.authorityEpoch,
            AuthorityHandoffReason = "",
            NetworkSchemaVersion = state.networkSchemaVersion,
            LocalIsHost = string.Equals(state.hostUid, uid, StringComparison.Ordinal)
        };
        SeatsWire data = Decode<SeatsWire>(state.seatsJson);
        if (data != null && data.seats != null)
        {
            foreach (SeatWire s in data.seats)
            {
                if (s == null) continue;
                AtlasMatchNetworkSeat seat = new AtlasMatchNetworkSeat {
                    SeatId = s.seatId,
                    SlotIndex = s.slotIndex,
                    SeatMode = s.mode,
                    DisplayName = s.name,
                    IsHost = s.host,
                    ControllerKind = NormalizeController(s.controller, s.mode),
                    ConnectionState = s.connection,
                    ReconnectExpiresAtEpochMs = s.reconnectExpiresAt,
                    AfkLockedOut = s.afkLockedOut,
                    RemovalReason = s.removalReason ?? ""
                };
                result.Seats.Add(seat);
                if (!string.IsNullOrEmpty(uid) &&
                    (s.uid == uid || s.ownerUid == uid) &&
                    (string.IsNullOrEmpty(result.LocalSeatId) || s.host))
                    result.LocalSeatId = s.seatId;
            }
        }
        return result;
    }

    private static async Task<Response> Send(string verb, string path, string token,
        string json = null, string ifMatch = null, bool wantEtag = false)
    {
        string url = Database + "/" + path + ".json?auth=" + UnityWebRequest.EscapeURL(token);
        using (UnityWebRequest req = new UnityWebRequest(url, verb))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            if (json != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            if (!string.IsNullOrEmpty(ifMatch)) req.SetRequestHeader("if-match", ifMatch);
            if (wantEtag) req.SetRequestHeader("X-Firebase-ETag", "true");
            req.timeout = 12;
            UnityWebRequestAsyncOperation op = req.SendWebRequest();
            TaskCompletionSource<bool> completed = new TaskCompletionSource<bool>();
            op.completed += _ => completed.TrySetResult(true);
            if (op.isDone) completed.TrySetResult(true);
            await completed.Task;
            return new Response {
                Status = req.responseCode,
                Body = req.downloadHandler != null ? req.downloadHandler.text : "",
                ETag = req.GetResponseHeader("ETag")
            };
        }
    }

    private static T Decode<T>(string json) where T:class
    {
        try { return string.IsNullOrWhiteSpace(json) || json.Trim() == "null"
            ? null : JsonUtility.FromJson<T>(json); }
        catch { return null; }
    }
    private static string Digest(string data)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(data));
            StringBuilder result = new StringBuilder(64);
            foreach (byte b in bytes) result.Append(b.ToString("x2"));
            return result.ToString();
        }
    }
    // Streaming JSON-member extraction without adding a Newtonsoft dependency.
    private static IEnumerable<KeyValuePair<string,string>> ObjectMembers(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null") yield break;
        int i = 0, n = json.Length;
        while (i < n && char.IsWhiteSpace(json[i])) i++;
        if (i >= n || json[i++] != '{') yield break;
        while (i < n)
        {
            while (i < n && (char.IsWhiteSpace(json[i]) || json[i] == ',')) i++;
            if (i >= n || json[i] == '}') yield break;
            if (json[i++] != '"') yield break;
            StringBuilder key = new StringBuilder(); bool escaped = false;
            while (i < n)
            {
                char c = json[i++];
                if (escaped) { key.Append(c); escaped = false; continue; }
                if (c == '\\') { escaped = true; continue; }
                if (c == '"') break;
                key.Append(c);
            }
            while (i < n && char.IsWhiteSpace(json[i])) i++;
            if (i >= n || json[i++] != ':') yield break;
            while (i < n && char.IsWhiteSpace(json[i])) i++;
            int start = i, depth = 0; bool inString = false, slash = false;
            while (i < n)
            {
                char c = json[i];
                if (inString)
                {
                    if (slash) slash = false;
                    else if (c == '\\') slash = true;
                    else if (c == '"') inString = false;
                    i++; continue;
                }
                if (c == '"') { inString = true; i++; continue; }
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') { if (depth == 0) break; depth--; }
                if (c == ',' && depth == 0) break;
                i++;
            }
            yield return new KeyValuePair<string,string>(key.ToString(), json.Substring(start,i-start));
        }
    }
}
