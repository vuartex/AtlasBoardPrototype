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
    private struct Response
    {
        public long Status;
        public string Body;
        public string ETag;
        public bool Ok => Status >= 200 && Status < 300;
    }

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static readonly Dictionary<string,long> LastHostPresenceWrite =
        new Dictionary<string,long>(StringComparer.Ordinal);
    private static readonly Dictionary<string,long> LastMaintenanceCheck =
        new Dictionary<string,long>(StringComparer.Ordinal);
    private static readonly Dictionary<string,AtlasMatchNetworkResult> LastMaintenanceResult =
        new Dictionary<string,AtlasMatchNetworkResult>(StringComparer.Ordinal);
    private static string PathKey(string value) => UnityWebRequest.EscapeURL(value ?? "");
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
            authorityEpoch = 1,
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
        Response result = await Send("PUT", "matchPresence/" + PathKey(matchId) +
            "/" + PathKey(uid), token, "{\"online\":false,\"lastSeenAt\":"+Now+"}");
        return result.Ok ? state : Failure("RTDB departure: " + FailReason(result));
    }

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
        string maintenanceKey = matchId + ":" + uid;
        LastMaintenanceCheck.Remove(maintenanceKey);
        LastMaintenanceResult.Remove(maintenanceKey);
        LastHostPresenceWrite.Remove(maintenanceKey);
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
