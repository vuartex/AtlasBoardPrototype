using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Spark RTDB casual-room transport (Firebase Auth token, REST, no Functions).
/// Room state is host-authored and carries NO persistent rewards or wallets.
/// This is a lobby transport, NOT a completed synchronized-match backend.
/// </summary>
public static class AtlasBoardRtdbCasualLobbyTransport
{
    private const string Database =
        "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    private static readonly System.Random CodeRandom = new System.Random();
    // Room codes are also persisted at /roomMeta/{roomId}/inviteCode.
    // These caches only avoid redundant REST reads; they are never the authority.
    private static readonly Dictionary<string, string> ActiveCode =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, long> RoomCreatedAt =
        new Dictionary<string, long>(StringComparer.Ordinal);
    private static readonly Dictionary<string, long> CodeRenewAfter =
        new Dictionary<string, long>(StringComparer.Ordinal);
    private static readonly object RoomWriterGateSync = new object();
    private static readonly Dictionary<string, SemaphoreSlim> RoomWriterGates =
        new Dictionary<string, SemaphoreSlim>(StringComparer.Ordinal);

    [Serializable] private sealed class Request
    {
        public string lobbyId, roomCode, password, mapId, themeId, boardThemeId, pawnCosmeticId;
        public string[] seatPolicies;
        public int slotIndex, maxPlayers, roundLimit, expectedSettingsRevision;
        public bool balancedDevelopment, doublesEnabled, tripleDoublePenaltyEnabled, ready;
    }
    [Serializable] private sealed class Meta
    {
        public string hostUid, mode, mapId, themeId, passwordHash, inviteCode;
        public long createdAt, updatedAt;
        public string activeMatchId;
        public int authorityEpoch;
        public bool closed;
        public int maxPlayers, roundLimit;
    }
    [Serializable] private sealed class Code
    {
        public string roomId, hostUid;
        public long expiresAt;
    }
    [Serializable] private sealed class Member
    {
        public string uid, role, displayName;
        public bool active;
        public long joinedAt;
    }
    [Serializable] private sealed class Join
    {
        public string uid, displayName, passwordHash;
        public long requestedAt;
    }
    [Serializable] private sealed class Ready
    {
        public string uid;
        public int revision;
        public bool ready;
    }
    [Serializable] private sealed class State
    {
        public long revision, eventSequence, updatedAt;
        public string phase, snapshotJson;
    }
    [Serializable] private sealed class Entry
    {
        public string roomId, hostDisplayName, mapId, themeId, regionId, gameVersion, contentVersion;
        public int roundLimit, maxPlayers, occupiedPlayers, openOnlineSeatCount, protocolVersion, rulesVersion, settingsRevision;
        public long createdAt, updatedAt;
        public bool joinable, hasPassword;
    }
    private struct Response
    {
        public long Code;
        public string Body;
        public string ETag;
        public bool Ok => Code >= 200 && Code < 300;
    }

    private enum StoreSnapshotFailureKind
    {
        None,
        StaleRevision,
        Collision,
        Transport
    }

    private sealed class StoreSnapshotResult
    {
        public bool Success;
        public StoreSnapshotFailureKind FailureKind;
        public long ExpectedRevision;
        public long ActualRevision;
        public long HttpCode;
    }

    private static SemaphoreSlim GetRoomWriterGate(string room)
    {
        lock (RoomWriterGateSync)
        {
            SemaphoreSlim gate;
            if (!RoomWriterGates.TryGetValue(room, out gate))
            {
                gate = new SemaphoreSlim(1, 1);
                RoomWriterGates[room] = gate;
            }
            return gate;
        }
    }

    private static bool RequiresRoomWriterGate(string operation, string room)
    {
        if (string.IsNullOrWhiteSpace(room)) return false;
        switch (operation)
        {
            // Snapshot/heartbeat are write-capable for the host while Waiting because they
            // admit pending joins/readiness. Serialize every operation that can observe or
            // mutate the same room lifecycle so Start cannot race its own host poll.
            case "lobbyGetSnapshot":
            case "lobbyHostHeartbeat":
            case "lobbyUpdatePassword":
            case "lobbyUpdateSettings":
            case "lobbyConfigureSeats":
            case "lobbySetPawnCosmetic":
            case "lobbyKickMember":
            case "lobbyStartMatch":
            case "lobbySetReady":
            case "lobbyLeaveRoom":
            case "lobbyCloseRoom":
                return true;
            default:
                return false;
        }
    }

    public static async Task<AtlasLobbyOperationResult> ExecuteAsync(
        string operation, string requestJson, string uid, string token,
        string activeRoom, AtlasLobbySnapshot current, string displayName)
    {
        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(token))
            return Failure("account.error.authentication_required", "Sign in for online rooms.");
        Request request = Parse<Request>(requestJson) ?? new Request();
        string room = !string.IsNullOrWhiteSpace(request.lobbyId)
            ? request.lobbyId : activeRoom;
        SemaphoreSlim roomWriterGate = RequiresRoomWriterGate(operation, room)
            ? GetRoomWriterGate(room)
            : null;
        if (roomWriterGate != null)
            await roomWriterGate.WaitAsync();
        try
        {
            switch (operation)
            {
                case "lobbyCreatePrivateRoom":
                    return await CreateAsync(request, uid, token, displayName, false);
                case "lobbyCreatePublicRoom":
                    return await CreateAsync(request, uid, token, displayName, true);
                case "lobbyCloseOwnedWaitingRooms":
                    return await CloseOwnedWaitingRoomsOnConsentAsync(uid, token, activeRoom);
                case "lobbyJoinByCode":
                {
                    Response code = await Send("GET", "joinCodes/" + Safe(request.roomCode), token);
                    Code index = Parse<Code>(code.Body);
                    if (!code.Ok || index == null || index.expiresAt <= Now())
                        return Failure("lobby.error.not_joinable", "Room code unavailable or expired.");
                    return await JoinAsync(index.roomId, request.password, uid, token, displayName);
                }
                case "lobbyJoinPublicRoom":
                    return await JoinAsync(room, request.password, uid, token, displayName);
                case "lobbyGetSnapshot":
                    return await SnapshotAsync(room, uid, token, displayName);
                case "lobbyHostHeartbeat":
                {
                    AtlasLobbyOperationResult heartbeat = await SnapshotAsync(room, uid, token, displayName);
                    if (heartbeat.Success && heartbeat.Snapshot != null &&
                        heartbeat.Snapshot.HostAccountId == uid)
                    {
                        await Write("PATCH", "roomMeta/" + Safe(room), token,
                            "{\"updatedAt\":" + Now() + "}");
                        // Room codes remain usable for the lifetime of a live
                        // private/public Casual room. Avoid a write on every
                        // heartbeat: renew at most every 30 minutes.
                        await RenewInviteCodeIfNeeded(room, uid, token);
                        if (heartbeat.Snapshot.Visibility == AtlasRoomVisibility.Public)
                            await StoreIndex(room, heartbeat.Snapshot, token, Now());
                    }
                    return heartbeat;
                }
                case "lobbyUpdatePassword":
                    return await UpdatePasswordAsync(room, request.password, uid, token);
                case "lobbyUpdateSettings":
                case "lobbyConfigureSeats":
                case "lobbySetPawnCosmetic":
                case "lobbyKickMember":
                case "lobbyStartMatch":
                    return await HostChangeAsync(operation, room, request, uid, token);
                case "lobbySetReady":
                    return await SetReadyAsync(room, request.ready, uid, token);
                case "lobbyLeaveRoom":
                    return await LeaveAsync(room, uid, token);
                case "lobbyCloseRoom":
                    return await CloseAsync(room, uid, token);
                default:
                    return Failure("lobby.error.service_unavailable",
                        "RTDB room operation not yet implemented: " + operation);
            }
        }
        catch (Exception error)
        {
            Debug.LogWarning("ATB_RTDB_LOBBY operation=" + operation +
                " result=failed category=" + error.GetType().Name);
            return Failure("lobby.error.service_unavailable", "RTDB operation failed.");
        }
        finally
        {
            if (roomWriterGate != null)
                roomWriterGate.Release();
        }
    }

    public static async Task<AtlasPublicLobbyListResult> PublicRoomsAsync(string token, int limit)
    {
        try
        {
            // Query ONLY the latest 50 rooms; the original unbounded GET would
            // download the whole directory and waste the Spark quota.
            Response response = await Send("GET", "publicRoomDirectory", token,
                null, null, "orderBy=%22updatedAt%22&limitToLast=50");
            if (!response.Ok)
                return AtlasPublicLobbyListResult.Fail("lobby.error.service_unavailable",
                    "RTDB directory HTTP " + response.Code);
            List<AtlasPublicLobbyCard> cards = new List<AtlasPublicLobbyCard>();
            foreach (KeyValuePair<string, string> child in JsonObjectMembers(response.Body))
            {
                Entry entry = Parse<Entry>(child.Value);
                if (entry == null || !entry.joinable ||
                    Now() - entry.updatedAt > 600000L) continue;
                cards.Add(new AtlasPublicLobbyCard
                {
                    LobbyId = entry.roomId,
                    HostDisplayName = entry.hostDisplayName,
                    MapId = entry.mapId,
                    ThemeId = entry.themeId,
                    RoundLimit = entry.roundLimit,
                    MaxPlayers = entry.maxPlayers,
                    OccupiedPlayers = entry.occupiedPlayers,
                    OpenOnlineSeatCount = entry.openOnlineSeatCount,
                    RegionId = entry.regionId,
                    GameVersion = entry.gameVersion,
                    ProtocolVersion = entry.protocolVersion,
                    RulesVersion = entry.rulesVersion,
                    ContentVersion = entry.contentVersion,
                    SettingsRevision = entry.settingsRevision,
                    HasPassword = entry.hasPassword,
                    CreatedAtEpochMs = entry.createdAt
                });
            }
            cards.Sort((a,b) => b.CreatedAtEpochMs.CompareTo(a.CreatedAtEpochMs));
            if (cards.Count > Mathf.Clamp(limit, 1, 50))
                cards.RemoveRange(Mathf.Clamp(limit, 1, 50), cards.Count - Mathf.Clamp(limit, 1, 50));
            Debug.Log("ATB_RTDB_PUBLIC_DIRECTORY success=true rooms=" + cards.Count);
            return AtlasPublicLobbyListResult.Ok(cards);
        }
        catch (Exception error)
        {
            return AtlasPublicLobbyListResult.Fail("lobby.error.service_unavailable",
                "RTDB directory unavailable: " + error.GetType().Name);
        }
    }

    // Phase 18G: reserve the six-digit code FIRST, before creating room records.
    // If a subsequent RTDB write fails, remove only this newly-created room.
    // Conditional PUT + null_etag prevents two hosts from claiming one code.
    private const int MaxPriorHostRooms = 10;
    private const int MaxOwnedRoomScan = MaxPriorHostRooms + 1;
    private const long DormantRoomAgeMs = 3L * 60L * 60L * 1000L;
    // Guard an owner's current match + one new room. This is client-side flow
    // control, NOT a server-enforced global quota; malicious API clients need
    // an authenticated backend admission service for a hard abuse bound.
    // Quota checks fetch at most 11 records; full owner cleanup deliberately
    // enumerates ALL records with this UID via the indexed, UID-restricted rule.
    // A limitToFirst=11 cleanup query could never see stale rows behind 11
    // fresh/closed rows; it MUST NOT be reused for full enumeration.
    private static async Task<(bool success, List<KeyValuePair<string,Meta>> rooms)>
        ReadOwnedRoomMetadataAsync(string uid, string token, bool fullOwnerScan = false)
    {
        var result = new List<KeyValuePair<string,Meta>>(MaxOwnedRoomScan);
        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(token))
            return (false, result);
        string query = "orderBy=%22hostUid%22&equalTo=%22" + Safe(uid) + "%22" +
            (fullOwnerScan ? string.Empty : "&limitToFirst=" + MaxOwnedRoomScan);
        Response response = await Send("GET", "roomMeta", token, query: query);
        if (!response.Ok)
        {
            Debug.LogWarning("ATB_RTDB_OWNER_SCAN success=false http=" + response.Code +
                " full_owner_scan=" + fullOwnerScan);
            return (false, result);
        }
        foreach (KeyValuePair<string,string> node in JsonObjectMembers(response.Body))
        {
            Meta meta = Parse<Meta>(node.Value);
            if (meta == null || !string.Equals(meta.hostUid, uid,
                    StringComparison.Ordinal)) continue;
            result.Add(new KeyValuePair<string,Meta>(node.Key, meta));
        }
        return (true, result);
    }

    // A 3-hour room is NOT automatically an orphan. We independently check
    // host UID, last activity, authoritative lobby, and (for started matches)
    // the match heartbeat + every saved Human heartbeat before deleting it.
    // Failed/ambiguous reads are always SKIPPED, never interpreted as offline.
    [Serializable] private sealed class MatchLease
    {
        public string matchId, lobbyId, hostUid, status;
        public long createdAt, updatedAt, hostHeartbeatAt;
        public int authorityEpoch;
    }
    [Serializable] private sealed class PresenceLease
    {
        public bool online;
        public long lastSeenAt;
    }

    private static bool IsIdleForThreeHours(MatchLease match, Meta meta,
        string room, string uid, long now)
    {
        return match != null && meta != null &&
            match.matchId == meta.activeMatchId && match.lobbyId == room &&
            string.Equals(match.hostUid, uid, StringComparison.Ordinal) &&
            match.authorityEpoch == meta.authorityEpoch &&
            match.createdAt > 0 && match.updatedAt > 0 &&
            match.hostHeartbeatAt > 0 &&
            match.createdAt <= now - DormantRoomAgeMs &&
            match.updatedAt <= now - DormantRoomAgeMs &&
            match.hostHeartbeatAt <= now - DormantRoomAgeMs;
    }

    // Owner-only automatic collection. After THREE hours with no recorded
    // activity, abandoned pre-start and disconnected matches become eligible.
    // Any HTTP/identity ambiguity and any recent activity remain fail-closed.
    public static async Task<(bool scanned, int found, int deleted)>
        CleanupOwnedDormantRoomsAsync(string uid, string token,
            string activeRoom, int maxDeletes = 0)
    {
        var scan = await ReadOwnedRoomMetadataAsync(uid, token, true);
        if (!scan.success) return (false, 0, 0);
        int deleted = 0, eligible = 0, failed = 0, protectedRooms = 0;
        int max = maxDeletes <= 0 ? int.MaxValue : Mathf.Clamp(maxDeletes, 1, MaxPriorHostRooms);
        scan.rooms.Sort((a,b) => a.Value.createdAt.CompareTo(b.Value.createdAt));
        foreach (var entry in scan.rooms)
        {
            if (deleted >= max) break;
            string room = entry.Key;
            if (string.IsNullOrWhiteSpace(room) || room == activeRoom ||
                !IsDormantCandidate(entry.Value, uid, Now()))
            {
                protectedRooms++;
                LogRoomCleanupDecision("startup_3h", room,
                    CleanupProtected(room == activeRoom ? "current_room" : "not_3h_dormant_candidate"),
                    entry.Value, null);
                continue;
            }
            SemaphoreSlim gate = GetRoomWriterGate(room);
            await gate.WaitAsync();
            try
            {
                Response metaRead = await Send("GET", "roomMeta/" + Safe(room), token);
                Meta latest = metaRead.Ok ? Parse<Meta>(metaRead.Body) : null;
                if (!IsDormantCandidate(latest, uid, Now()))
                {
                    protectedRooms++;
                    LogRoomCleanupDecision("startup_3h", room,
                        CleanupUnknown(metaRead.Ok ? "metadata_changed_since_scan" :
                            "room_meta_http_" + metaRead.Code), entry.Value, null);
                    continue;
                }
                Response stateRead = await Send("GET", "roomState/" + Safe(room), token);
                if (!stateRead.Ok)
                {
                    failed++;
                    LogRoomCleanupDecision("startup_3h", room,
                        CleanupUnknown("room_state_http_" + stateRead.Code), latest, null);
                    continue;
                }
                State wire = Parse<State>(stateRead.Body);
                AtlasLobbySnapshot snap = wire == null ? null : Parse<AtlasLobbySnapshot>(wire.snapshotJson);
                RoomCleanupDecision decision = string.IsNullOrWhiteSpace(latest.activeMatchId)
                    ? (snap != null && !string.IsNullOrWhiteSpace(snap.MatchId)
                        ? await VerifyUnanchoredStartingMatchAsync(latest, wire, snap,
                            room, uid, token, DormantRoomAgeMs)
                        : EvaluateStalePreMatchRoom(latest, wire, snap, uid,
                            Now(), DormantRoomAgeMs))
                    : await VerifyMatchInactiveAsync(latest, snap, room, uid, token,
                        DormantRoomAgeMs, DormantRoomAgeMs);
                LogRoomCleanupDecision("startup_3h", room, decision, latest, snap);
                if (!decision.Eligible)
                {
                    protectedRooms++;
                    continue;
                }
                eligible++;
                AtlasLobbyOperationResult result = await CloseAsync(room, uid, token,
                    true, latest.updatedAt, DormantRoomAgeMs);
                if (result.Success) deleted++; else failed++;
            }
            catch (Exception ex)
            {
                failed++;
                Debug.LogWarning("ATB_RTDB_ORPHAN_CLEANUP failed=true category=" +
                    ex.GetType().Name);
            }
            finally { gate.Release(); }
            await Task.Yield();
        }
        Debug.Log("ATB_RTDB_ORPHAN_CLEANUP scanned=true owner_only=true " +
            "full_scan=true found=" + scan.rooms.Count + " eligible=" + eligible +
            " deleted=" + deleted + " failed=" + failed +
            " protected=" + protectedRooms + " age_min_hours=3 " +
            "remaining_eligible=" + Math.Max(0, eligible - deleted) +
            " active_match_protected_by_presence=true");
        return (true, scan.rooms.Count, deleted);
    }

    // The ONLY opt-in path. No room creation-age gate, but at least ten
    // minutes without authoritative activity is required before closure.
    // A started match is eligible only after all its authoritative leases have
    // been idle for the entire reconnect window, not simply because its owner
    // pressed OK. Startup orphan collection retains its separate 3h policy.
    // Intentional: the game seat-reconnect deadline remains five minutes.
    // Consent cleanup requires TEN minutes of inactivity, not a shortened reconnect deadline.
    private const long ConsentMatchOfflineGraceMs = 10L * 60L * 1000L;

    // Diagnostics only: a protected room is NOT automatically a live match.
    // The fingerprint remains stable across restarts without printing room codes,
    // tokens, account UIDs or actual lobby identifiers.
    private readonly struct RoomCleanupDecision
    {
        internal readonly bool Eligible;
        internal readonly bool Unknown;
        internal readonly string Reason;
        internal RoomCleanupDecision(bool eligible, bool unknown, string reason)
        {
            Eligible = eligible;
            Unknown = unknown;
            Reason = reason;
        }
    }

    private static RoomCleanupDecision CleanupEligible(string reason) =>
        new RoomCleanupDecision(true, false, reason);
    private static RoomCleanupDecision CleanupProtected(string reason) =>
        new RoomCleanupDecision(false, false, reason);
    private static RoomCleanupDecision CleanupUnknown(string reason) =>
        new RoomCleanupDecision(false, true, reason);

    private static string RoomDiagnosticReference(string room)
    {
        // Collision-resistant reference; intentionally not an invite code.
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(room ?? string.Empty));
            var label = new StringBuilder(12);
            for (int i = 0; i < 6; i++) label.Append(bytes[i].ToString("x2"));
            return label.ToString();
        }
    }

    private static long RoomDiagnosticAgeSeconds(long timestampMs, long nowMs)
    {
        return timestampMs <= 0 ? -1 : Math.Max(0, (nowMs - timestampMs) / 1000L);
    }

    private static void LogRoomCleanupDecision(string scope, string room,
        RoomCleanupDecision decision, Meta m, AtlasLobbySnapshot snapshot)
    {
        long now = Now();
        string phase = snapshot == null ? "unknown" : snapshot.LifecycleState.ToString();
        // No room ID/UID/invite code, no Firebase URLs, no raw backend JSON.
        Debug.Log("ATB_RTDB_ROOM_DIAG scope=" + scope +
            " room_ref=" + RoomDiagnosticReference(room) +
            " action=" + (decision.Eligible ? "candidate" : "protected") +
            " category=" + (decision.Unknown ? "unknown" : "verified") +
            " reason=" + decision.Reason +
            " phase=" + phase +
            " closed=" + (m != null && m.closed) +
            " started=" + (m != null && !string.IsNullOrWhiteSpace(m.activeMatchId)) +
            " created_age_s=" + (m == null ? -1 : RoomDiagnosticAgeSeconds(m.createdAt, now)) +
            " updated_age_s=" + (m == null ? -1 : RoomDiagnosticAgeSeconds(m.updatedAt, now)));
    }

    private static RoomCleanupDecision EvaluateConsentMatchLease(
        MatchLease lease, Meta m, string room, string uid, long now)
    {
        if (lease == null) return CleanupUnknown("match_state_missing_or_invalid");
        if (m == null) return CleanupUnknown("room_meta_missing");
        if (lease.matchId != m.activeMatchId || lease.lobbyId != room)
            return CleanupUnknown("match_anchor_mismatch");
        if (!string.Equals(lease.hostUid, uid, StringComparison.Ordinal))
            return CleanupUnknown("match_host_mismatch");
        if (lease.authorityEpoch != m.authorityEpoch)
            return CleanupUnknown("authority_epoch_mismatch");
        if (lease.updatedAt <= 0 || lease.hostHeartbeatAt <= 0)
            return CleanupUnknown("match_lease_timestamp_missing");
        if (lease.updatedAt > now - ConsentMatchOfflineGraceMs)
            return CleanupProtected("match_updated_recent_age_s_" +
                RoomDiagnosticAgeSeconds(lease.updatedAt, now));
        if (lease.hostHeartbeatAt > now - ConsentMatchOfflineGraceMs)
            return CleanupProtected("host_heartbeat_recent_age_s_" +
                RoomDiagnosticAgeSeconds(lease.hostHeartbeatAt, now));
        return CleanupEligible("match_lease_idle_over_10m");
    }

    private static bool IsTimestampIdle(long timestamp, long now, long idleMs)
    {
        return timestamp > 0 && timestamp <= now - idleMs;
    }

    // A legacy room may be stranded in Starting with no matchId, or its
    // roomState may be missing entirely. Neither condition proves a live game.
    // Only owner+idle metadata, idle roomState, and NO match anchor can release it.
    private static RoomCleanupDecision EvaluateStalePreMatchRoom(
        Meta m, State wire, AtlasLobbySnapshot snap, string uid, long now, long idleMs)
    {
        if (m == null || m.hostUid != uid)
            return CleanupUnknown("metadata_owner_missing_or_changed");
        if (!string.IsNullOrWhiteSpace(m.activeMatchId))
            return CleanupUnknown("active_match_anchor_present");
        if (!IsTimestampIdle(m.createdAt, now, idleMs))
            return CleanupProtected("room_created_recent");
        if (!m.closed && !IsTimestampIdle(m.updatedAt, now, idleMs))
            return CleanupProtected("room_meta_updated_recent_age_s_" +
                RoomDiagnosticAgeSeconds(m.updatedAt, now));
        if (wire != null && !IsTimestampIdle(wire.updatedAt, now, idleMs))
            return CleanupProtected("room_state_updated_recent_age_s_" +
                RoomDiagnosticAgeSeconds(wire.updatedAt, now));
        if (snap != null)
        {
            if (snap.HostAccountId != uid)
                return CleanupUnknown("snapshot_host_mismatch");
            if (!string.IsNullOrWhiteSpace(snap.MatchId))
                return CleanupUnknown("snapshot_match_anchor_present");
            if (snap.LifecycleState != AtlasRoomLifecycleState.Waiting &&
                snap.LifecycleState != AtlasRoomLifecycleState.Starting)
                return CleanupUnknown("unrecognized_prematch_lifecycle");
        }
        return CleanupEligible(snap == null ? "stale_missing_snapshot_repair" :
            snap.LifecycleState == AtlasRoomLifecycleState.Starting
                ? "stale_starting_without_match" : "idle_waiting_room");
    }

    // Never equate a stale roomMeta timestamp with a stale match. A started
    // match also requires stale host writes and *all* human presence records.
    private static async Task<RoomCleanupDecision> VerifyMatchInactiveAsync(
        Meta m, AtlasLobbySnapshot snap, string room, string uid, string token,
        long hostIdleMs, long presenceIdleMs)
    {
        if (m == null || m.hostUid != uid ||
            string.IsNullOrWhiteSpace(m.activeMatchId))
            return CleanupUnknown("invalid_match_owner_or_anchor");
        if (snap == null || snap.HostAccountId != uid ||
            snap.MatchId != m.activeMatchId)
            return CleanupUnknown("snapshot_match_anchor_mismatch");
        Response matchRead = await Send("GET", "matchState/" + Safe(m.activeMatchId), token);
        if (!matchRead.Ok)
            return CleanupUnknown("match_state_http_" + matchRead.Code);
        MatchLease lease = Parse<MatchLease>(matchRead.Body);
        if (lease == null)
            return CleanupUnknown("match_state_missing_or_invalid");
        if (lease.lobbyId != room || lease.matchId != m.activeMatchId ||
            lease.hostUid != uid || lease.authorityEpoch != m.authorityEpoch)
            return CleanupUnknown("match_authority_or_anchor_mismatch");
        long now = Now();
        if (!IsTimestampIdle(lease.updatedAt, now, hostIdleMs) ||
            !IsTimestampIdle(lease.hostHeartbeatAt, now, hostIdleMs))
            return CleanupProtected("match_host_updated_recent_or_invalid");
        Response presence = await Send("GET", "matchPresence/" + Safe(m.activeMatchId), token);
        if (!presence.Ok)
            return CleanupUnknown("match_presence_http_" + presence.Code);
        foreach (var node in JsonObjectMembers(presence.Body))
        {
            PresenceLease member = Parse<PresenceLease>(node.Value);
            if (member == null || member.lastSeenAt <= 0)
                return CleanupUnknown("human_presence_timestamp_missing");
            if (!IsTimestampIdle(member.lastSeenAt, Now(), presenceIdleMs))
                return CleanupProtected("human_presence_recent_age_s_" +
                    RoomDiagnosticAgeSeconds(member.lastSeenAt, Now()));
        }
        return CleanupEligible("host_and_all_humans_inactive");
    }

    private static async Task<AtlasLobbyOperationResult>
        CloseOwnedWaitingRoomsOnConsentAsync(string uid, string token, string activeRoom)
    {
        var scan = await ReadOwnedRoomMetadataAsync(uid, token, true);
        if (!scan.success)
            return Failure("lobby.error.service_unavailable", "Owner-only RTDB room listing failed.");
        int closed = 0, failed = 0, protectedRooms = 0, selected = 0;
        int unreadable = 0, recentActivity = 0, unknown = 0;
        foreach (var entry in scan.rooms)
        {
            string room = entry.Key;
            if (string.IsNullOrWhiteSpace(room) || room == activeRoom)
            {
                protectedRooms++;
                LogRoomCleanupDecision("consent", room,
                    CleanupProtected(room == activeRoom ? "current_room" : "invalid_room_key"),
                    entry.Value, null);
                continue;
            }
            SemaphoreSlim gate = GetRoomWriterGate(room);
            await gate.WaitAsync();
            try
            {
                Response currentMeta = await Send("GET", "roomMeta/" + Safe(room), token);
                Meta m = currentMeta.Ok ? Parse<Meta>(currentMeta.Body) : null;
                if (!currentMeta.Ok || m == null)
                {
                    protectedRooms++; unreadable++; unknown++;
                    LogRoomCleanupDecision("consent", room,
                        CleanupUnknown(currentMeta.Ok ? "room_meta_missing" :
                            "room_meta_http_" + currentMeta.Code), entry.Value, null);
                    continue;
                }
                if (m.hostUid != uid)
                {
                    protectedRooms++; unknown++;
                    LogRoomCleanupDecision("consent", room,
                        CleanupUnknown("owner_changed_since_scan"), m, null);
                    continue;
                }
                Response roomState = await Send("GET", "roomState/" + Safe(room), token);
                State wire = roomState.Ok ? Parse<State>(roomState.Body) : null;
                AtlasLobbySnapshot snap = wire == null ? null :
                    Parse<AtlasLobbySnapshot>(wire.snapshotJson);
                if (!roomState.Ok && !m.closed)
                {
                    protectedRooms++; unreadable++; unknown++;
                    LogRoomCleanupDecision("consent", room,
                        CleanupUnknown("room_state_http_" + roomState.Code), m, null);
                    continue;
                }
                if (snap != null && snap.HostAccountId != uid)
                {
                    protectedRooms++; unknown++;
                    LogRoomCleanupDecision("consent", room,
                        CleanupUnknown("snapshot_host_mismatch"), m, snap);
                    continue;
                }
                // "OK" grants deletion of the owner's INACTIVE rooms, but
                // not live matches. Ten minutes is an activity fence, NOT a room age.
                RoomCleanupDecision decision = string.IsNullOrWhiteSpace(m.activeMatchId)
                    ? (snap != null && !string.IsNullOrWhiteSpace(snap.MatchId)
                        ? await VerifyUnanchoredStartingMatchAsync(m, wire, snap,
                            room, uid, token, ConsentMatchOfflineGraceMs)
                        : EvaluateStalePreMatchRoom(m, wire, snap, uid,
                            Now(), ConsentMatchOfflineGraceMs))
                    : await VerifyMatchInactiveAsync(m, snap, room, uid, token,
                        ConsentMatchOfflineGraceMs, ConsentMatchOfflineGraceMs);
                LogRoomCleanupDecision("consent", room, decision, m, snap);
                if (!decision.Eligible)
                {
                    protectedRooms++;
                    if (decision.Unknown) unknown++;
                    else recentActivity++;
                    continue;
                }
                selected++;
                AtlasLobbyOperationResult result = await CloseAsync(room, uid, token,
                    true, m.updatedAt, ConsentMatchOfflineGraceMs);
                if (result.Success) closed++;
                else
                {
                    failed++;
                    LogRoomCleanupDecision("consent_close", room,
                        CleanupUnknown("delete_failed_see_cleanup_stage_logs"), m, snap);
                }
            }
            catch (Exception e)
            {
                failed++;
                LogRoomCleanupDecision("consent", room,
                    CleanupUnknown("exception_" + e.GetType().Name), entry.Value, null);
            }
            finally { gate.Release(); }
            await Task.Yield();
        }
        var remaining = await ReadOwnedRoomMetadataAsync(uid, token, true);
        int remainingOpen = 0, remainingRecords = -1;
        if (remaining.success)
        {
            remainingRecords = remaining.rooms.Count;
            foreach (var item in remaining.rooms)
                if (!item.Value.closed) remainingOpen++;
        }
        Debug.Log("ATB_RTDB_QUOTA_CONSENT owner_only=true explicitly_approved=true " +
            "age_min_hours=0 scanned=" + scan.rooms.Count + " selected=" + selected +
            " closed=" + closed + " failed=" + failed + " protected=" + protectedRooms +
            " protected_recent=" + recentActivity + " protected_unknown=" + unknown + " unreadable=" + unreadable +
            " remaining_records=" + remainingRecords + " remaining_open=" + remainingOpen +
            " active_matches_preserved=true");
        int permittedCurrent = string.IsNullOrWhiteSpace(activeRoom) ? 0 : 1;
        if (!remaining.success || remainingRecords > permittedCurrent || failed > 0)
            return Failure("lobby.error.owner_room_limit",
                "Owner cleanup incomplete; some rooms are active, unreadable or restricted.");
        return AtlasLobbyOperationResult.Ok(null);
    }

    private static bool IsConsentCleanupCandidate(Meta m, AtlasLobbySnapshot snap,
        string uid, string room, string activeRoom)
    {
        if (m == null || uid == null || room == activeRoom ||
            m.hostUid != uid || string.IsNullOrWhiteSpace(room)) return false;
        if (m.closed && snap == null &&
            string.IsNullOrWhiteSpace(m.activeMatchId)) return true; // retry a Waiting partial close
        return snap != null && snap.HostAccountId == uid &&
            string.IsNullOrWhiteSpace(m.activeMatchId) &&
            string.IsNullOrWhiteSpace(snap.MatchId) &&
            snap.LifecycleState == AtlasRoomLifecycleState.Waiting;
    }

    private static bool IsConsentMatchLeaseInactive(MatchLease lease, Meta m,
        string room, string uid, long now)
    {
        // No room-age gate. This is a ten-minute *activity* safety fence only.
        // A closed room may be retried after at least one reconnect window;
        // never skip the fence for a started match that might have live humans.
        return lease != null && m != null &&
            lease.matchId == m.activeMatchId && lease.lobbyId == room &&
            lease.hostUid == uid && lease.authorityEpoch == m.authorityEpoch &&
            lease.updatedAt > 0 && lease.hostHeartbeatAt > 0 &&
            lease.updatedAt <= now - ConsentMatchOfflineGraceMs &&
            lease.hostHeartbeatAt <= now - ConsentMatchOfflineGraceMs;
    }

    private static async Task<RoomCleanupDecision> VerifyConsentMatchInactiveAsync(
        Meta m, AtlasLobbySnapshot snap, string room, string uid, string token)
    {
        // A partial Waiting-room closure is safe to retry without a snapshot.
        if (m.closed && string.IsNullOrWhiteSpace(m.activeMatchId) &&
            (snap == null || string.IsNullOrWhiteSpace(snap.MatchId)))
            return CleanupEligible("partial_waiting_room_retry");
        if (string.IsNullOrWhiteSpace(m.activeMatchId))
            return CleanupUnknown("unclassified_room_phase");
        if (snap != null && snap.MatchId != m.activeMatchId)
            return CleanupUnknown("snapshot_match_anchor_mismatch");
        Response matchRead = await Send("GET", "matchState/" + Safe(m.activeMatchId), token);
        if (!matchRead.Ok)
            return CleanupUnknown("match_state_http_" + matchRead.Code);
        MatchLease lease = Parse<MatchLease>(matchRead.Body);
        RoomCleanupDecision leaseDecision = EvaluateConsentMatchLease(lease, m, room, uid, Now());
        if (!leaseDecision.Eligible) return leaseDecision;
        Response presence = await Send("GET", "matchPresence/" + Safe(m.activeMatchId), token);
        if (!presence.Ok)
            return CleanupUnknown("match_presence_http_" + presence.Code);
        foreach (var node in JsonObjectMembers(presence.Body))
        {
            PresenceLease player = Parse<PresenceLease>(node.Value);
            if (player == null || player.lastSeenAt <= 0)
                return CleanupUnknown("presence_timestamp_missing_or_invalid");
            if (player.lastSeenAt > Now() - ConsentMatchOfflineGraceMs)
                return CleanupProtected("human_presence_recent_age_s_" +
                    RoomDiagnosticAgeSeconds(player.lastSeenAt, Now()));
        }
        return CleanupEligible("match_and_all_human_leases_idle_over_10m");
    }

    // V1 legacy rooms can have neither roomMeta.activeMatchId nor
    // roomMeta.authorityEpoch, even though roomState.snapshotJson.MatchId and
    // matchState/{matchId} exist. The missing integer deserializes to zero.
    // A match starts at authorityEpoch=1. This ONE schema transition is
    // compatible only for an unchanged owner and an exact match/lobby anchor.
    // Never tolerate 1->2, 2->3, host changes, or a modern activeMatchId.
    private static bool IsUnanchoredStartingAuthorityConsistent(
        Meta m, AtlasLobbySnapshot snap, MatchLease lease, string room, string uid)
    {
        if (m == null || snap == null || lease == null ||
            string.IsNullOrWhiteSpace(room) || string.IsNullOrWhiteSpace(uid) ||
            !string.IsNullOrWhiteSpace(m.activeMatchId) ||
            !string.Equals(m.hostUid, uid, StringComparison.Ordinal) ||
            !string.Equals(snap.HostAccountId, uid, StringComparison.Ordinal) ||
            snap.LifecycleState != AtlasRoomLifecycleState.Starting ||
            string.IsNullOrWhiteSpace(snap.MatchId) ||
            !string.Equals(lease.matchId, snap.MatchId, StringComparison.Ordinal) ||
            !string.Equals(lease.lobbyId, room, StringComparison.Ordinal) ||
            !string.Equals(lease.hostUid, uid, StringComparison.Ordinal))
            return false;
        return (m.authorityEpoch > 0 && lease.authorityEpoch == m.authorityEpoch) ||
            (m.authorityEpoch == 0 && lease.authorityEpoch == 1);
    }

    // Regression guard: real legacy RTDB export contained 14 old Starting
    // rooms with meta epoch absent (0), match epoch 1 and exact owned anchors.
    // This tests identity/epoch compatibility only; live safety ALSO requires
    // the unchanged 3h/10m metadata, match-heartbeat and presence checks.
    private static bool ValidateUnanchoredStartingEpochContract()
    {
        var meta = new Meta { hostUid = "qa_owner", activeMatchId = "", authorityEpoch = 0 };
        var snap = new AtlasLobbySnapshot {
            HostAccountId = "qa_owner", MatchId = "qa_match",
            LifecycleState = AtlasRoomLifecycleState.Starting };
        var lease = new MatchLease { hostUid = "qa_owner", lobbyId = "qa_room",
            matchId = "qa_match", authorityEpoch = 1 };
        if (!IsUnanchoredStartingAuthorityConsistent(meta, snap, lease,
                "qa_room", "qa_owner")) return false;
        lease.authorityEpoch = 2; // modern migration mismatch MUST stay blocked
        if (IsUnanchoredStartingAuthorityConsistent(meta, snap, lease,
                "qa_room", "qa_owner")) return false;
        lease.authorityEpoch = 1;
        lease.hostUid = "other_host";
        if (IsUnanchoredStartingAuthorityConsistent(meta, snap, lease,
                "qa_room", "qa_owner")) return false;
        lease.hostUid = "qa_owner";
        lease.lobbyId = "different_room";
        if (IsUnanchoredStartingAuthorityConsistent(meta, snap, lease,
                "qa_room", "qa_owner")) return false;
        lease.lobbyId = "qa_room";
        snap.MatchId = "different_match";
        if (IsUnanchoredStartingAuthorityConsistent(meta, snap, lease,
                "qa_room", "qa_owner")) return false;
        snap.MatchId = "qa_match";
        meta.activeMatchId = "qa_match";
        if (IsUnanchoredStartingAuthorityConsistent(meta, snap, lease,
                "qa_room", "qa_owner")) return false;
        meta.activeMatchId = "";
        meta.authorityEpoch = 3;
        lease.authorityEpoch = 3;
        if (!IsUnanchoredStartingAuthorityConsistent(meta, snap, lease,
                "qa_room", "qa_owner")) return false;
        lease.authorityEpoch = 4;
        return !IsUnanchoredStartingAuthorityConsistent(meta, snap, lease,
            "qa_room", "qa_owner");
    }

    // Legacy crash window: matchState may have been created and roomState
    // switched to Starting, but roomMeta.activeMatchId was never committed.
    // Never treat that orphan as a fresh Waiting room. Check BOTH real lease
    // and human presence; missing matchState means it cannot host a live match.
    private static async Task<RoomCleanupDecision> VerifyUnanchoredStartingMatchAsync(
        Meta m, State wire, AtlasLobbySnapshot snap, string room, string uid,
        string token, long idleMs)
    {
        if (m == null || m.hostUid != uid || snap == null ||
            snap.HostAccountId != uid ||
            snap.LifecycleState != AtlasRoomLifecycleState.Starting ||
            !string.IsNullOrWhiteSpace(m.activeMatchId) ||
            string.IsNullOrWhiteSpace(snap.MatchId))
            return CleanupUnknown("starting_match_ownership_or_phase_mismatch");
        long now = Now();
        if (!IsTimestampIdle(m.createdAt, now, idleMs) ||
            (!m.closed && !IsTimestampIdle(m.updatedAt, now, idleMs)) ||
            wire == null || !IsTimestampIdle(wire.updatedAt, now, idleMs))
            return CleanupProtected("starting_room_recent_activity");
        Response matchRead = await Send("GET", "matchState/" + Safe(snap.MatchId), token);
        if (!matchRead.Ok)
            return CleanupUnknown("starting_match_state_http_" + matchRead.Code);
        MatchLease lease = Parse<MatchLease>(matchRead.Body);
        if (lease == null)
            return CleanupEligible("stale_starting_match_state_absent");
        if (!IsUnanchoredStartingAuthorityConsistent(m, snap, lease, room, uid))
            return CleanupUnknown("starting_match_authority_mismatch");
        if (!IsTimestampIdle(lease.updatedAt, now, idleMs) ||
            !IsTimestampIdle(lease.hostHeartbeatAt, now, idleMs))
            return CleanupProtected("starting_match_lease_recent");
        Response presence = await Send("GET", "matchPresence/" + Safe(snap.MatchId), token);
        if (!presence.Ok)
            return CleanupUnknown("starting_presence_http_" + presence.Code);
        foreach (var node in JsonObjectMembers(presence.Body))
        {
            PresenceLease member = Parse<PresenceLease>(node.Value);
            if (member == null || member.lastSeenAt <= 0)
                return CleanupUnknown("starting_presence_timestamp_invalid");
            if (!IsTimestampIdle(member.lastSeenAt, Now(), idleMs))
                return CleanupProtected("starting_presence_recent_age_s_" +
                    RoomDiagnosticAgeSeconds(member.lastSeenAt, Now()));
        }
        return CleanupEligible(m.authorityEpoch == 0 && lease.authorityEpoch == 1
            ? "stale_legacy_starting_epoch0_to_epoch1_all_players_inactive"
            : "stale_starting_match_and_players_inactive");
    }

    public static bool ValidateOwnerConsentCleanupContract()
    {
        long now = 1000000000L;
        Meta m = new Meta { hostUid="owner", activeMatchId="", closed=false };
        AtlasLobbySnapshot waiting = new AtlasLobbySnapshot {
            HostAccountId="owner", MatchId="", LifecycleState=AtlasRoomLifecycleState.Waiting };
        if (!IsConsentCleanupCandidate(m, waiting, "owner", "old", "active")) return false;
        if (IsConsentCleanupCandidate(m, waiting, "stranger", "old", "active") ||
            IsConsentCleanupCandidate(m, waiting, "owner", "active", "active")) return false;
        // A partially deleted closed room is retryable without its roomState.
        m.closed=true;
        if (!IsConsentCleanupCandidate(m, null, "owner", "old", "active")) return false;
        m.closed=false;
        m.activeMatchId="match_A";
        waiting.MatchId="match_A";
        if (IsConsentCleanupCandidate(m, waiting, "owner", "old", "active")) return false;
        MatchLease idle = new MatchLease { matchId="match_A", lobbyId="old",
            hostUid="owner", authorityEpoch=1,
            updatedAt=now-ConsentMatchOfflineGraceMs-1,
            hostHeartbeatAt=now-ConsentMatchOfflineGraceMs-1 };
        m.authorityEpoch=1;
        if (!IsConsentMatchLeaseInactive(idle, m, "old", "owner", now)) return false;
        idle.hostHeartbeatAt=now-ConsentMatchOfflineGraceMs+1;
        if (IsConsentMatchLeaseInactive(idle, m, "old", "owner", now)) return false;
        if (EvaluateConsentMatchLease(null, m, "old", "owner", now).Reason !=
            "match_state_missing_or_invalid") return false;
        idle.hostHeartbeatAt=now-ConsentMatchOfflineGraceMs-1;
        if (!EvaluateConsentMatchLease(idle, m, "old", "owner", now).Eligible) return false;
        idle.authorityEpoch=2;
        if (EvaluateConsentMatchLease(idle, m, "old", "owner", now).Reason !=
            "authority_epoch_mismatch") return false;
        m.activeMatchId = "";
        m.createdAt = now - ConsentMatchOfflineGraceMs - 1;
        m.updatedAt = now - ConsentMatchOfflineGraceMs - 1;
        AtlasLobbySnapshot starting = new AtlasLobbySnapshot {
            HostAccountId = "owner", MatchId = "", LifecycleState = AtlasRoomLifecycleState.Starting };
        State wire = new State { updatedAt = now - ConsentMatchOfflineGraceMs - 1 };
        if (!EvaluateStalePreMatchRoom(m, wire, starting, "owner", now,
                ConsentMatchOfflineGraceMs).Eligible) return false;
        if (!EvaluateStalePreMatchRoom(m, null, null, "owner", now,
                ConsentMatchOfflineGraceMs).Eligible) return false;
        wire.updatedAt = now - ConsentMatchOfflineGraceMs + 1;
        if (EvaluateStalePreMatchRoom(m, wire, starting, "owner", now,
                ConsentMatchOfflineGraceMs).Eligible) return false;
        wire.updatedAt = now - ConsentMatchOfflineGraceMs - 1;
        starting.MatchId = "real_match";
        if (EvaluateStalePreMatchRoom(m, wire, starting, "owner", now,
                ConsentMatchOfflineGraceMs).Eligible) return false;
        starting.MatchId = "";
        m.updatedAt = now - ConsentMatchOfflineGraceMs + 1;
        if (EvaluateStalePreMatchRoom(m, wire, starting, "owner", now,
                ConsentMatchOfflineGraceMs).Eligible) return false;
        m.updatedAt = now - ConsentMatchOfflineGraceMs - 1;
        if (EvaluateStalePreMatchRoom(m, wire, starting, "intruder", now,
                ConsentMatchOfflineGraceMs).Eligible) return false;
        return ValidateUnanchoredStartingEpochContract();
    }

    private static bool IsDormantCandidate(Meta data, string uid, long now)
    {
        // A failed close marks the room closed and updates updatedAt=now.
        // That is NOT new player activity: retry after creation is 3h old.
        // An open room needs BOTH timestamps older than 3h.
        return data != null &&
            string.Equals(data.hostUid, uid, StringComparison.Ordinal) &&
            data.createdAt > 0 && data.updatedAt > 0 &&
            data.createdAt <= now - DormantRoomAgeMs &&
            (data.closed || data.updatedAt <= now - DormantRoomAgeMs);
    }

    // Uses the identical candidate function as the live owner-cleanup path.
    public static bool ValidateDormantCleanupGuardContract()
    {
        long now = 1000000000L;
        Meta m = new Meta { hostUid = "qa_owner", createdAt = now - DormantRoomAgeMs - 1,
            updatedAt = now - DormantRoomAgeMs - 1, closed = false };
        if (!IsDormantCandidate(m, "qa_owner", now) ||
            IsDormantCandidate(m, "another_uid", now)) return false;
        m.updatedAt = now - DormantRoomAgeMs + 1;
        if (IsDormantCandidate(m, "qa_owner", now)) return false;
        m.updatedAt = now - DormantRoomAgeMs - 1;
        m.createdAt = now - DormantRoomAgeMs + 1;
        if (IsDormantCandidate(m, "qa_owner", now)) return false;
        m.createdAt = now - DormantRoomAgeMs - 1;
        m.closed = true; // Failed closures are re-eligible for cleanup retries.
        m.updatedAt = now - 100; // A failed close updates activity to now.
        if (!IsDormantCandidate(m, "qa_owner", now)) return false;
        m.activeMatchId = "match_A";
        m.authorityEpoch = 3;
        MatchLease lease = new MatchLease { matchId = "match_A", lobbyId = "room_A",
            hostUid = "qa_owner", authorityEpoch = 3,
            createdAt = now - DormantRoomAgeMs - 100,
            updatedAt = now - DormantRoomAgeMs - 100,
            hostHeartbeatAt = now - DormantRoomAgeMs - 100 };
        if (!IsIdleForThreeHours(lease, m, "room_A", "qa_owner", now)) return false;
        lease.hostHeartbeatAt = now - DormantRoomAgeMs + 100;
        return !IsIdleForThreeHours(lease, m, "room_A", "qa_owner", now) &&
            MaxPriorHostRooms == 10 && MaxOwnedRoomScan == 11 &&
            ValidateUnanchoredStartingEpochContract();
    }

    private static async Task<AtlasLobbyOperationResult> CreateAsync(
        Request req, string uid, string token, string name, bool isPublic)
    {
        var owned = await ReadOwnedRoomMetadataAsync(uid, token, true);
        if (!owned.success)
            return Failure("lobby.error.service_unavailable",
                "Cannot verify host room quota; check deployed RTDB owner-query rules.");
        int openRoomCount = 0;
        foreach (var record in owned.rooms)
            if (!record.Value.closed) openRoomCount++;
        if (openRoomCount >= MaxOwnedRoomScan)
        {
            // Bounded owner-only retry for rooms stranded before startup cleanup.
            await CleanupOwnedDormantRoomsAsync(uid, token, "", MaxPriorHostRooms);
            owned = await ReadOwnedRoomMetadataAsync(uid, token, true);
            if (!owned.success)
                return Failure("lobby.error.service_unavailable",
                    "Cannot verify owner room quota after cleanup.");
            openRoomCount = 0;
            foreach (var record in owned.rooms)
                if (!record.Value.closed) openRoomCount++;
            if (openRoomCount >= MaxOwnedRoomScan)
                return Failure("lobby.error.owner_room_limit",
                    "OWNED_ROOM_LIMIT_10_PLUS_1");
        }
        string room = Guid.NewGuid().ToString("N") +
            Guid.NewGuid().ToString("N").Substring(0, 4);
        long now = Now();
        string roomCode = string.Empty;
        long reservationHttp = 0;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            int codeNum;
            lock (CodeRandom)
                codeNum = CodeRandom.Next(100000, 1000000);

            string candidate = codeNum.ToString();
            Code index = new Code
            {
                roomId = room,
                hostUid = uid,
                expiresAt = now + 7200000L
            };
            Response reserved = await Send(
                "PUT", "joinCodes/" + candidate, token,
                JsonUtility.ToJson(index), "null_etag");

            reservationHttp = reserved.Code;
            if (reserved.Ok)
            {
                roomCode = candidate;
                break;
            }

            // 412 means a real collision: select another six-digit code.
            // 401/403 means permissions/auth, 0 means network; retries
            // with other random codes cannot fix these failures.
            if (reserved.Code != 412)
            {
                Debug.LogWarning(
                    "ATB_RTDB_CODE_RESERVATION success=false http=" +
                    reserved.Code + " reason=" +
                    (reserved.Code == 401 || reserved.Code == 403
                        ? "permission_denied_or_token_invalid"
                        : reserved.Code == 0 ? "network_unavailable"
                        : "unexpected_response") +
                    " check=deployed_joinCodes_rules");
                return Failure(
                    "lobby.error.service_unavailable",
                    "RTDB invite code reservation failed (HTTP " +
                    reserved.Code + "). Check the deployed joinCodes rules " +
                    "and the Firebase Auth session.");
            }
        }

        if (roomCode.Length != 6)
        {
            Debug.LogWarning("ATB_RTDB_CODE_RESERVATION success=false http=" +
                reservationHttp + " reason=ten_code_collisions");
            return Failure("lobby.error.service_unavailable",
                "RTDB invite codes were occupied (HTTP 412). Try again.");
        }

        int playerCount = Mathf.Clamp(req.maxPlayers, 2, 4);
        Meta meta = new Meta
        {
            hostUid = uid,
            mode = isPublic ? "publicCasualOnline" : "privateCasualOnline",
            createdAt = now, updatedAt = now, closed = false,
            maxPlayers = playerCount,
            mapId = string.IsNullOrEmpty(req.mapId) ? "Turkey" : req.mapId,
            themeId = string.IsNullOrEmpty(req.themeId)
                ? "classic_table" : req.themeId,
            roundLimit = Mathf.Clamp(req.roundLimit, 1, 100),
            passwordHash = string.Empty,
            inviteCode = roomCode
        };
        Member host = new Member
        {
            uid = uid, role = "host", displayName = CleanName(name),
            active = true, joinedAt = now
        };

        // Never orphan partial rooms, and never touch any other host's room.
        if (!await Write("PUT", "roomMeta/" + Safe(room), token,
                JsonUtility.ToJson(meta)))
        {
            await AbortCreateAsync(room, roomCode, uid, token);
            return Failure("lobby.error.service_unavailable",
                "RTDB invite code was reserved but room metadata write failed.");
        }
        if (!await Write("PUT", "roomMembers/" + Safe(room) +
                "/" + Safe(uid), token, JsonUtility.ToJson(host)))
        {
            await AbortCreateAsync(room, roomCode, uid, token);
            return Failure("lobby.error.service_unavailable",
                "RTDB host membership write failed.");
        }

        AtlasLobbySnapshot snapshot =
            NewSnapshot(room, uid, host.displayName, meta, isPublic);
        if (!await StoreSnapshot(room, snapshot, token, 1L))
        {
            await AbortCreateAsync(room, roomCode, uid, token);
            return Failure("lobby.error.service_unavailable",
                "RTDB initial room snapshot write failed.");
        }

        ActiveCode[room] = roomCode;
        RoomCreatedAt[room] = now;
        CodeRenewAfter[room] = now + 30L * 60L * 1000L;
        if (isPublic)
            await StoreIndex(room, snapshot, token, now);

        Debug.Log("ATB_RTDB_ROOM_CREATED visibility=" +
            (isPublic ? "public" : "private") +
            " transport=RTDB code_length=6");
        return AtlasLobbyOperationResult.Ok(snapshot, roomCode, true);
    }

    private static async Task AbortCreateAsync(
        string room, string roomCode, string uid, string token)
    {
        // Unique GUID room and conditional invite code were allocated by
        // THIS CreateAsync only. Do not delete any pre-existing room.
        await Send("DELETE", "roomState/" + Safe(room), token);
        await Send("DELETE", "roomMembers/" + Safe(room) +
            "/" + Safe(uid), token);
        await Send("DELETE", "roomMeta/" + Safe(room), token);
        await Send("DELETE", "joinCodes/" + Safe(roomCode), token);
        ActiveCode.Remove(room);
        RoomCreatedAt.Remove(room);
        CodeRenewAfter.Remove(room);
    }

    private static AtlasLobbySnapshot NewSnapshot(
        string room,string uid,string name,Meta meta,bool isPublic)
    {
        AtlasLobbySettings settings = new AtlasLobbySettings
        {
            MapId=meta.mapId,ThemeId=meta.themeId, BoardThemeId="default_board", RoundLimit=meta.roundLimit,
            MaxPlayers=meta.maxPlayers,RequiredHumanPlayers=1,LocalHumanCount=1,
            RemoteHumanCount=0,RemoteReadyRequiredCount=0, OpenOnlineSeatCount=meta.maxPlayers-1,
            BotCount=0
        };
        AtlasLobbySnapshot snap=new AtlasLobbySnapshot
        {
            LobbyId=room, LifecycleState=AtlasRoomLifecycleState.Waiting,
            Visibility=isPublic?AtlasRoomVisibility.Public:AtlasRoomVisibility.Private,
            HostAccountId=uid, SettingsRevision=1,Settings=settings,
            GameVersion=Application.version,ProtocolVersion=AtlasOnlineDefaults.ProtocolVersion,
            RulesVersion=AtlasOnlineDefaults.RulesVersion, ContentVersion="1",RegionId="eur3",
            Members=new List<AtlasLobbyMemberSnapshot>()
        };
        for(int i=0;i<meta.maxPlayers;i++)
        {
            bool host=i==0;
            snap.Members.Add(new AtlasLobbyMemberSnapshot
            {
                SeatId="seat_"+i,SlotIndex=i,Active=host,
                SeatMode=host?AtlasLobbySeatMode.HostLocal:AtlasLobbySeatMode.OpenOnline,
                IsHumanSeat=host,AccountId=host?uid:"",LocalOwnerAccountId=host?uid:"",
                DisplayName=host?name:"",IsHost=host,
                ControllerKind=host?AtlasSeatControllerKind.Human:AtlasSeatControllerKind.None,
                ConnectionState=host?AtlasSeatConnectionState.Connected:AtlasSeatConnectionState.Empty,
                ReadyForRevision=host?1:0,RequiresReady=false
            });
        }
        return snap;
    }

    private static async Task<AtlasLobbyOperationResult> JoinAsync(
        string room,string password,string uid,string token,string name)
    {
        if (string.IsNullOrWhiteSpace(room))
            return Failure("lobby.error.not_joinable","Missing room ID.");
        // Private members may not read roomMeta until admitted. Join using a code
        // whose RTDB rule limits code reads to authenticated players.
        Join request=new Join {uid=uid,displayName=CleanName(name),
            passwordHash=Digest(room+":"+(password??"")),requestedAt=Now()};
        if(!await Write("PUT","joinRequests/"+Safe(room)+"/"+Safe(uid),token,
            JsonUtility.ToJson(request)))
            return Failure("lobby.error.not_joinable","RTDB join request denied.");
        Debug.Log("ATB_RTDB_REMOTE_QA event=join_requested transport=live_spark_rtdb");
        // Host accepts the request in its normal snapshot poll. This is bounded;
        // no cloud function and no infinite request loop.
        for(int attempt=0;attempt<24;attempt++)
        {
            await Task.Delay(600);
            Response memberResponse=await Send("GET","roomMembers/"+Safe(room)+"/"+Safe(uid),token);
            Member member=Parse<Member>(memberResponse.Body);
            if(!memberResponse.Ok || member==null || !member.active) continue;
            Response stateResponse=await Send("GET","roomState/"+Safe(room),token);
            State state=Parse<State>(stateResponse.Body);
            AtlasLobbySnapshot snap=state!=null?Parse<AtlasLobbySnapshot>(state.snapshotJson):null;
            // Joining with the Host's own Firebase UID is never a second Human.
            // Preserve the host seat instead of presenting a false remote join.
            if(snap!=null && string.Equals(snap.HostAccountId,uid,StringComparison.Ordinal))
            {
                Debug.LogWarning("ATB_RTDB_REMOTE_QA event=join_rejected " +
                    "reason=account_already_hosts_room");
                return Failure("lobby.error.not_joinable",
                    "A second, distinct Firebase account is required for Remote Human.");
            }
            // Membership may become visible before the host has published the
            // new snapshot. Never return a stale lobby with an empty seat.
            if(snap!=null && snap.Members!=null &&
                snap.Members.Exists(seat=>seat!=null && seat.Active &&
                    string.Equals(seat.AccountId,uid,StringComparison.Ordinal)))
            {
                Response joinedMeta=await Send("GET","roomMeta/"+Safe(room),token);
                Meta admittedMeta=Parse<Meta>(joinedMeta.Body);
                if(admittedMeta!=null && !admittedMeta.closed)
                {
                    if(!string.IsNullOrWhiteSpace(admittedMeta.inviteCode))
                        ActiveCode[room] = admittedMeta.inviteCode;
                    Debug.Log("ATB_RTDB_REMOTE_QA event=join_confirmed " +
                        "member_in_snapshot=true settings_revision=" + snap.SettingsRevision);
                    return AtlasLobbyOperationResult.Ok(snap,
                        admittedMeta.inviteCode??"",true);
                }
            }
        }
        Debug.LogWarning("ATB_RTDB_REMOTE_QA event=join_timeout " +
            "admission_confirmed=false attempts=24");
        return Failure("lobby.error.service_unavailable","RTDB host has not approved the join request.");
    }

    private static async Task<AtlasLobbyOperationResult> SnapshotAsync(
        string room,string uid,string token,string name)
    {
        if(string.IsNullOrWhiteSpace(room)) return Failure("lobby.error.not_joinable","Missing room ID.");
        Response response=await Send("GET","roomState/"+Safe(room),token);
        State state=Parse<State>(response.Body);
        AtlasLobbySnapshot snap=state!=null?Parse<AtlasLobbySnapshot>(state.snapshotJson):null;
        if (!response.Ok || snap == null)
        {
            // REST 200/null is a definitive removed room, not a transient
            // network timeout. When RTDB rules hide roomState after membership
            // cleanup, verify the caller's own member record before reporting
            // a close; a 401 or temporary HTTP failure alone is NOT enough.
            if (response.Ok && snap == null &&
                (string.IsNullOrWhiteSpace(response.Body) ||
                 response.Body.Trim() == "null"))
                return Failure("lobby.error.closed", "RTDB room state removed.");
            if (response.Code == 403 || response.Code == 401)
            {
                Response ownSeat = await Send("GET", "roomMembers/" + Safe(room) +
                    "/" + Safe(uid), token);
                if (ownSeat.Ok && (string.IsNullOrWhiteSpace(ownSeat.Body) ||
                    ownSeat.Body.Trim() == "null"))
                    return Failure("lobby.error.closed", "RTDB room membership removed.");
            }
            return Failure("lobby.error.not_joinable", "RTDB room state unavailable.");
        }
        if (snap.HostAccountId != uid)
        {
            // The host marks metadata closed before removing dependent
            // match/lobby records. This lets a follower leave cleanly even
            // if deletion is still in progress (or partially retryable).
            Response metadata = await Send("GET", "roomMeta/" + Safe(room), token);
            Meta liveMeta = metadata.Ok ? Parse<Meta>(metadata.Body) : null;
            if (liveMeta != null && liveMeta.closed)
                return Failure("lobby.error.closed", "RTDB host closed the room.");

            // A host's identity is bound to its UID, never to this client's
            // device-local Guest nickname. Repair only a suspect host label
            // from the existing authoritative roomMembers record; this is
            // presentation-only and does not write the host's snapshot.
            AtlasLobbyMemberSnapshot hostSeat = snap.Members != null
                ? snap.Members.Find(seat => seat != null && seat.Active &&
                    string.Equals(seat.AccountId, snap.HostAccountId,
                        StringComparison.Ordinal)) : null;
            if (hostSeat != null &&
                (string.IsNullOrWhiteSpace(hostSeat.DisplayName) ||
                 string.Equals(hostSeat.DisplayName, CleanName(name),
                     StringComparison.OrdinalIgnoreCase)))
            {
                Response hostResponse = await Send("GET", "roomMembers/" +
                    Safe(room) + "/" + Safe(snap.HostAccountId), token);
                Member host = hostResponse.Ok ? Parse<Member>(hostResponse.Body) : null;
                if (host != null && host.active &&
                    string.Equals(host.uid, snap.HostAccountId,
                        StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(host.displayName))
                {
                    hostSeat.DisplayName = CleanName(host.displayName);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log("ATB_RTDB_LOBBY_HOST_NAME verified=true " +
                        "source=host_uid_membership local_override=false");
#endif
                }
            }
        }
        // The canonical state revision is separate from the room settings
        // revision. A rematch may change Starting->Waiting with no new rules.
        snap.TransportRevision = Math.Max(snap.TransportRevision, state.revision);
        if(snap.HostAccountId==uid && snap.LifecycleState==AtlasRoomLifecycleState.Waiting)
        {
            // Defer deletion of join requests until the authoritative room
            // snapshot commit succeeds, so a failed write can be retried.
            List<string> accepted = new List<string>();
            bool seatsChanged=await AcceptPendingJoins(room,uid,token,snap,accepted);
            if(seatsChanged) snap.SettingsRevision+=1;
            bool readyChanged=await AcceptReadyUpdates(room,token,snap);
            if(seatsChanged || readyChanged)
            {
                Recount(snap);
                if (!await StoreSnapshot(room,snap,token,state.revision+1))
                    return Failure("lobby.error.service_unavailable",
                        "Host RTDB snapshot commit failed; join requests retained.");
                if(snap.Visibility==AtlasRoomVisibility.Public)
                    await StoreIndex(room,snap,token,Now());
            }
            // Delete already-committed duplicate joins even when nothing changed.
            // New admissions only reach this point after their snapshot commit.
            foreach (string acceptedUid in accepted)
                await Send("DELETE", "joinRequests/" + Safe(room) + "/" + Safe(acceptedUid), token);
        }
        string persistedCode = ActiveCode.TryGetValue(room, out string cached)
            ? cached : string.Empty;
        // A host that resumed its RTDB room can still retrieve the code even
        // after its in-memory cache was discarded.
        if (string.IsNullOrEmpty(persistedCode) && snap.HostAccountId == uid)
            persistedCode = await GetPersistedRoomCode(room, token);
        return AtlasLobbyOperationResult.Ok(snap, persistedCode);
    }

    private static async Task<bool> AcceptPendingJoins(string room,string host,string token,
        AtlasLobbySnapshot snap, List<string> accepted)
    {
        Response r=await Send("GET","joinRequests/"+Safe(room),token);
        if(!r.Ok) return false;
        bool changed=false;
        Response mr=await Send("GET","roomMeta/"+Safe(room),token);
        Meta meta=Parse<Meta>(mr.Body);
        if(meta==null||meta.closed)return false;
        foreach(var pair in JsonObjectMembers(r.Body))
        {
            Join join=Parse<Join>(pair.Value);
            if (join == null || string.IsNullOrEmpty(join.uid) ||
                !string.Equals(pair.Key, join.uid, StringComparison.Ordinal))
            {
                Debug.LogWarning("ATB_RTDB_JOIN rejected=true reason=uid_key_mismatch");
                continue;
            }
            if (Now() - join.requestedAt > 120000L ||
                (!string.IsNullOrEmpty(meta.passwordHash) && join.passwordHash != meta.passwordHash))
            {
                // Wrong-password and stale requests must not accumulate on Spark.
                await Send("DELETE", "joinRequests/" + Safe(room) + "/" + Safe(join.uid), token);
                continue;
            }
            // The request is authenticated by RTDB rules as its own UID.
            AtlasLobbyMemberSnapshot seat=null;
            foreach(var member in snap.Members)if(member!=null && member.Active && member.AccountId==join.uid){seat=member;break;}
            if(seat!=null)
            {
                // An existing member (including the Host from a second client)
                // must NEVER be re-admitted into another role or reset readiness.
                // The earlier committed snapshot already owns this identity.
                accepted.Add(join.uid);
                continue;
            }
            foreach(var member in snap.Members)
                if(member!=null && !member.Active && member.SeatMode==AtlasLobbySeatMode.OpenOnline)
                { seat=member; break; }
            if(seat==null)continue;
            // Never accept a bare client-provided Guest nickname as proof of
            // identity. Bind Guest display names to the authenticated UID in
            // the host's admission path and reject a room-local name conflict.
            string requestedName = CleanName(join.displayName);
            string admittedName = AtlasBoardGuestIdentity.LooksLikeGuestName(requestedName)
                ? AtlasBoardGuestIdentity.NameForAuthenticatedUid(join.uid)
                : requestedName;
            bool nameCollision = snap.Members.Exists(existing =>
                existing != null && existing.Active &&
                !string.Equals(existing.AccountId, join.uid, StringComparison.Ordinal) &&
                string.Equals(existing.DisplayName, admittedName,
                    StringComparison.OrdinalIgnoreCase));
            if (nameCollision)
            {
                Debug.LogWarning("ATB_RTDB_JOIN rejected=true reason=display_name_collision " +
                    "different_authenticated_uid=true");
                await Send("DELETE", "joinRequests/" + Safe(room) + "/" + Safe(join.uid), token);
                continue;
            }
            Member admitted=new Member{uid=join.uid,role="remoteHuman",displayName=admittedName,active=true,joinedAt=Now()};
            if(!await Write("PUT","roomMembers/"+Safe(room)+"/"+Safe(join.uid),token,
                    JsonUtility.ToJson(admitted))) continue;
            seat.Active=true;
            seat.SeatMode=AtlasLobbySeatMode.RemoteHuman;
            seat.IsHumanSeat=true;
            seat.AccountId=join.uid;
            seat.LocalOwnerAccountId=join.uid;
            seat.DisplayName=admitted.displayName;
            seat.ControllerKind=AtlasSeatControllerKind.Human;
            seat.ConnectionState=AtlasSeatConnectionState.Connected;
            seat.RequiresReady=true;
            seat.ReadyForRevision=0;
            changed=true;
            accepted.Add(join.uid);
            Debug.Log("ATB_RTDB_REMOTE_QA event=seat_admitted " +
                "slot=" + seat.SlotIndex + " awaiting_ready=true");
        }
        return changed;
    }

    private static async Task<bool> AcceptReadyUpdates(string room,string token,AtlasLobbySnapshot snap)
    {
        bool changed=false;
        foreach(var seat in snap.Members)
        {
            if(seat==null||!seat.Active||seat.SeatMode!=AtlasLobbySeatMode.RemoteHuman)continue;
            Response r=await Send("GET","roomReady/"+Safe(room)+"/"+Safe(seat.AccountId),token);
            Ready ready=Parse<Ready>(r.Body);
            if(ApplyRemoteReady(seat,ready,snap.SettingsRevision))
            {
                changed=true;
                Debug.Log("ATB_RTDB_REMOTE_QA event=ready_changed " +
                    "slot=" + seat.SlotIndex +
                    " ready=" + seat.IsReadyFor(snap.SettingsRevision) +
                    " settings_revision=" + snap.SettingsRevision);
            }
        }
        return changed;
    }

    // One canonical transition for remote READY and NOT READY. The previous
    // implementation only accepted true, allowing Start after an unready click.
    private static bool ApplyRemoteReady(
        AtlasLobbyMemberSnapshot seat, Ready request, int settingsRevision)
    {
        if(seat==null || !seat.Active || seat.SeatMode!=AtlasLobbySeatMode.RemoteHuman ||
            request==null || request.revision!=settingsRevision) return false;
        int desired=request.ready ? settingsRevision : 0;
        if(seat.ReadyForRevision==desired) return false;
        seat.ReadyForRevision=desired;
        return true;
    }

    // No-network contract QA only; not a second-player live RTDB PASS.
    public static bool ValidateRemoteReadyTransitionContract()
    {
        var seat=new AtlasLobbyMemberSnapshot {
            SeatMode=AtlasLobbySeatMode.RemoteHuman,Active=true,
            AccountId="qa_remote_uid",ReadyForRevision=0
        };
        var ready=new Ready {revision=4,ready=true};
        if(!ApplyRemoteReady(seat,ready,4) || !seat.IsReadyFor(4)) return false;
        if(ApplyRemoteReady(seat,ready,4)) return false;
        ready.ready=false;
        if(!ApplyRemoteReady(seat,ready,4) || seat.IsReadyFor(4)) return false;
        if(ApplyRemoteReady(seat,ready,4)) return false;
        ready.ready=true;
        ready.revision=3;
        if(ApplyRemoteReady(seat,ready,4) || seat.IsReadyFor(4)) return false;
        seat.Active=false;
        ready.revision=4;
        return !ApplyRemoteReady(seat,ready,4);
    }

    private static async Task<AtlasLobbyOperationResult> SetReadyAsync(
        string room,bool ready,string uid,string token)
    {
        AtlasLobbyOperationResult state=await SnapshotAsync(room,uid,token,"");
        if(!state.Success)return state;
        Ready update=new Ready{uid=uid,revision=state.Snapshot.SettingsRevision,ready=ready};
        if(!await Write("PUT","roomReady/"+Safe(room)+"/"+Safe(uid),token,JsonUtility.ToJson(update)))
            return Failure("lobby.error.service_unavailable","RTDB ready update rejected.");
        Debug.Log("ATB_RTDB_REMOTE_QA event=ready_submitted ready=" + ready +
            " settings_revision=" + update.revision +
            " authoritative_host_ack=pending");
        return state;
    }

    private static async Task<AtlasLobbyOperationResult> HostChangeAsync(
        string operation,string room,Request req,string uid,string token)
    {
        AtlasLobbyOperationResult result=await SnapshotAsync(room,uid,token,"");
        if(!result.Success || result.Snapshot==null)return result;
        AtlasLobbySnapshot snap=result.Snapshot;
        if(snap.HostAccountId!=uid)return Failure("lobby.error.access_denied","Only the host can change this room.");
        if(operation=="lobbyStartMatch")
            return await StartCasualMatchAsync(room, snap, uid, token,
                req.expectedSettingsRevision);

        // A delayed lobby UI callback must never mutate game configuration
        // while a match is Starting/InMatch. Such writes increment the room
        // revision and can invalidate the very next start after rematch.
        bool changesGameSetup = operation=="lobbyUpdateSettings" ||
            operation=="lobbyConfigureSeats" ||
            operation=="lobbySetPawnCosmetic";
        if(changesGameSetup &&
            snap.LifecycleState != AtlasRoomLifecycleState.Waiting)
            return Failure("lobby.error.not_joinable",
                "Game setup can only be edited while the room is Waiting.");

        if(operation=="lobbyUpdateSettings")
        {
            // The authenticated host cannot select a cosmetic they do not
            // own. The room member may view it but only the owner can apply.
            string boardId = string.IsNullOrEmpty(req.boardThemeId)
                ? "default_board" : req.boardThemeId;
            if (!AtlasBoardBoardThemeVisuals.IsValid(boardId))
                return Failure("lobby.error.invalid_settings", "Unknown BoardThemeId.");
            if (boardId != "default_board")
            {
                Response entitlement = await Send("GET",
                    "users/" + Safe(uid) + "/metaEconomy/inventory/boards/" + Safe(boardId), token);
                if (!entitlement.Ok || entitlement.Body == null ||
                    entitlement.Body.Trim() != "true")
                    return Failure("lobby.error.access_denied",
                        "Board theme is not owned by the authenticated host.");
            }
            // Idempotent host settings saves avoid consuming Spark writes or
            // incrementing SettingsRevision just because the UI re-opened.
            if (string.Equals(snap.Settings.MapId, req.mapId, StringComparison.Ordinal) &&
                string.Equals(snap.Settings.ThemeId, req.themeId, StringComparison.Ordinal) &&
                string.Equals(snap.Settings.BoardThemeId ?? "default_board",
                    string.IsNullOrEmpty(req.boardThemeId) ? "default_board" : req.boardThemeId,
                    StringComparison.Ordinal) &&
                snap.Settings.RoundLimit == Mathf.Clamp(req.roundLimit,1,100) &&
                snap.Settings.BalancedDevelopment == req.balancedDevelopment &&
                snap.Settings.DoublesEnabled == req.doublesEnabled &&
                snap.Settings.TripleDoublePenaltyEnabled == req.tripleDoublePenaltyEnabled)
            {
                return result;
            }
            snap.Settings.MapId=req.mapId;
            snap.Settings.ThemeId=req.themeId;
            snap.Settings.BoardThemeId=string.IsNullOrEmpty(req.boardThemeId) ? "default_board" : req.boardThemeId;
            snap.Settings.RoundLimit=Mathf.Clamp(req.roundLimit,1,100);
            snap.Settings.BalancedDevelopment=req.balancedDevelopment;
            snap.Settings.DoublesEnabled=req.doublesEnabled;
            snap.Settings.TripleDoublePenaltyEnabled=req.tripleDoublePenaltyEnabled;
        }
        // A repeated seat payload is common when returning to the lobby.
        // Compare the final authoritative state before increasing revision.
        string previousSeatConfiguration = operation=="lobbyConfigureSeats"
            ? JsonUtility.ToJson(snap) : string.Empty;
        if(operation=="lobbyConfigureSeats")
        {
            int newCount = Mathf.Clamp(req.maxPlayers, 2, 4);
            for (int i = newCount; i < snap.Members.Count; i++)
                if (snap.Members[i] != null && snap.Members[i].Active)
                    return Failure("lobby.error.not_joinable", "Occupied seats cannot be removed.");
            while (snap.Members.Count > newCount)
                snap.Members.RemoveAt(snap.Members.Count - 1);
            while (snap.Members.Count < newCount)
            {
                int index = snap.Members.Count;
                snap.Members.Add(new AtlasLobbyMemberSnapshot {
                    SeatId = "seat_" + index, SlotIndex = index, Active = false,
                    SeatMode = AtlasLobbySeatMode.OpenOnline,
                    ControllerKind = AtlasSeatControllerKind.None,
                    ConnectionState = AtlasSeatConnectionState.Empty
                });
            }
            snap.Settings.MaxPlayers = newCount;
            if(req.seatPolicies!=null && req.seatPolicies.Length>0)
            {
                // Existing assignments remain untouched; never reclassify
                // a remotely controlled human as a local human.
                for(int i=1;i<snap.Members.Count && i<req.seatPolicies.Length;i++)
                {
                    var seat=snap.Members[i];
                    if(seat==null || seat.Active)continue;
                    string policy=req.seatPolicies[i]??"";
                    bool localHuman = policy.IndexOf("local", StringComparison.OrdinalIgnoreCase)>=0;
                    bool bot = policy.IndexOf("bot", StringComparison.OrdinalIgnoreCase)>=0;
                    seat.SeatMode = localHuman ? AtlasLobbySeatMode.LocalHuman :
                        bot ? AtlasLobbySeatMode.Bot : AtlasLobbySeatMode.OpenOnline;
                    seat.Active = localHuman || bot;
                    seat.IsHumanSeat = localHuman;
                    seat.AccountId = localHuman ? uid : string.Empty;
                    seat.LocalOwnerAccountId = localHuman ? uid : string.Empty;
                    seat.DisplayName = localHuman ? "Local Player" : bot ? "BOT" : string.Empty;
                    seat.ControllerKind = bot ? AtlasSeatControllerKind.PermanentBot :
                        localHuman ? AtlasSeatControllerKind.Human : AtlasSeatControllerKind.None;
                    seat.ReadyForRevision = localHuman ? snap.SettingsRevision+1 : 0;
                    seat.ConnectionState = seat.Active ? AtlasSeatConnectionState.Connected : AtlasSeatConnectionState.Empty;
                    seat.RequiresReady = false;
                }
            }
        }
        if (operation=="lobbyConfigureSeats" &&
            string.Equals(previousSeatConfiguration, JsonUtility.ToJson(snap),
                StringComparison.Ordinal))
            return result;
        if(operation=="lobbySetPawnCosmetic")
        {
            if(req.slotIndex<0 || req.slotIndex>=snap.Members.Count ||
                snap.Members[req.slotIndex]==null)
                return Failure("lobby.error.not_joinable", "Invalid cosmetic seat.");
            if (string.Equals(snap.Members[req.slotIndex].PawnCosmeticId,
                req.pawnCosmeticId, StringComparison.Ordinal))
                return result;
            snap.Members[req.slotIndex].PawnCosmeticId=req.pawnCosmeticId;
        }
        if(operation=="lobbyKickMember")
        {
            if(req.slotIndex<=0||req.slotIndex>=snap.Members.Count)
                return Failure("lobby.error.not_joinable","Invalid kick seat.");
            var member=snap.Members[req.slotIndex];
            if(member!=null && member.Active && !string.IsNullOrEmpty(member.AccountId))
            {
                await Send("PATCH","roomMembers/"+Safe(room)+"/"+Safe(member.AccountId),token,"{\"active\":false}");
                member.Active=false;member.SeatMode=AtlasLobbySeatMode.OpenOnline;
                member.ConnectionState=AtlasSeatConnectionState.Kicked;
            }
        }
        snap.SettingsRevision++;
        Recount(snap);
        if (operation == "lobbyUpdateSettings" || operation == "lobbyConfigureSeats")
        {
            // Keep metadata and the authoritative lobby snapshot consistent.
            string metaJson = "{\"mapId\":\"" + EscapeJson(snap.Settings.MapId) +
                "\",\"roundLimit\":" + snap.Settings.RoundLimit +
                ",\"maxPlayers\":" + snap.Settings.MaxPlayers +
                ",\"updatedAt\":" + Now() + "}";
            if (!await Write("PATCH", "roomMeta/" + Safe(room), token, metaJson))
                return Failure("lobby.error.service_unavailable", "RTDB room metadata update rejected.");
        }
        if(!await StoreSnapshot(room,snap,token,Now()))
            return Failure("lobby.error.service_unavailable","RTDB room update rejected.");
        if(snap.Visibility==AtlasRoomVisibility.Public)await StoreIndex(room,snap,token,Now());
        string persistentCode = await GetPersistedRoomCode(room,token);
        return AtlasLobbyOperationResult.Ok(snap,persistentCode,true);
    }

    private static async Task<AtlasLobbySnapshot> ReadRoomSnapshotOnlyAsync(
        string room, string token, int maxAttempts)
    {
        int attempts = Math.Max(1, Math.Min(maxAttempts, 3));
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Response response = await Send("GET", "roomState/" + Safe(room), token);
            State state = response.Ok ? Parse<State>(response.Body) : null;
            AtlasLobbySnapshot snapshot = state != null
                ? Parse<AtlasLobbySnapshot>(state.snapshotJson)
                : null;
            if (snapshot != null)
            {
                snapshot.TransportRevision = Math.Max(snapshot.TransportRevision, state.revision);
                return snapshot;
            }

            if (attempt + 1 < attempts)
                await Task.Delay(75);
        }
        return null;
    }

    private static async Task<bool> RollbackCreatedMatchAsync(string matchId, string token)
    {
        // DELETE is idempotent and matchId is generated uniquely by this Start attempt.
        // A second bounded try repairs a transient response failure without touching any
        // other room or match.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (await AtlasBoardSparkRtdbMatchTransport.DeleteCreatedMatchAsync(matchId, token))
                return true;
            if (attempt == 0)
                await Task.Delay(75);
        }
        return false;
    }

    // Called only on the authenticated room host. All unselected OPEN seats
    // become bots at Start, never implicit additional local humans. Explicit
    // LocalHuman/Bot seats and occupied remote humans remain untouched.
    private static async Task<AtlasLobbyOperationResult> StartCasualMatchAsync(
        string room, AtlasLobbySnapshot snap, string uid, string token,
        int expectedSettingsRevision, bool allowTransportRebase = true)
    {
        int stableSettingsRevision = expectedSettingsRevision > 0
            ? expectedSettingsRevision
            : snap != null ? snap.SettingsRevision : 0;

        if (snap != null && expectedSettingsRevision > 0 &&
            expectedSettingsRevision != snap.SettingsRevision)
        {
            Debug.LogWarning("ATB_RTDB_MATCH_REVISION_STALE expected=" +
                expectedSettingsRevision + " actual=" + snap.SettingsRevision +
                " stage=before_match_write");
            return Failure("lobby.error.stale_settings",
                "Room settings changed; refresh before starting.");
        }
        if (snap == null || snap.Settings == null || snap.Members == null ||
            snap.Members.Count < 2 || snap.Members.Count > 4)
            return Failure("lobby.error.not_joinable", "Invalid lobby seat configuration.");
        if (snap.LifecycleState != AtlasRoomLifecycleState.Waiting)
            return Failure("lobby.error.not_joinable", "Lobby is not waiting for a new match.");
        if (!string.Equals(snap.HostAccountId, uid, StringComparison.Ordinal))
            return Failure("lobby.error.access_denied", "Only the current room host can start this match.");

        for (int i = 1; i < snap.Members.Count; i++)
        {
            AtlasLobbyMemberSnapshot seat = snap.Members[i];
            if (seat == null) continue;
            if (seat.Active && seat.SeatMode == AtlasLobbySeatMode.RemoteHuman &&
                !seat.IsReadyFor(snap.SettingsRevision))
                return Failure("lobby.error.not_ready", "A remote player has not readied the current settings.");
            if (seat.Active && seat.SeatMode == AtlasLobbySeatMode.RemoteHuman)
                continue;
            if (seat.SeatMode == AtlasLobbySeatMode.LocalHuman ||
                seat.SeatMode == AtlasLobbySeatMode.HostLocal)
            {
                seat.Active = true;
                seat.IsHumanSeat = true;
                seat.AccountId = uid;
                seat.LocalOwnerAccountId = uid;
                seat.ControllerKind = AtlasSeatControllerKind.Human;
                seat.ConnectionState = AtlasSeatConnectionState.Connected;
                if (string.IsNullOrWhiteSpace(seat.DisplayName))
                    seat.DisplayName = "Local Player " + (i + 1);
                seat.RequiresReady = false;
                seat.ReadyForRevision = snap.SettingsRevision;
                continue;
            }

            // A selected Bot or any unoccupied OpenOnline/RemoteHuman seat becomes a
            // normal BOT. Do not infer temporary/permanent lifecycle history here.
            seat.Active = true;
            seat.IsHumanSeat = false;
            seat.SeatMode = AtlasLobbySeatMode.Bot;
            seat.AccountId = "";
            seat.LocalOwnerAccountId = "";
            seat.ControllerKind = AtlasSeatControllerKind.PermanentBot;
            seat.ConnectionState = AtlasSeatConnectionState.Connected;
            seat.ReadyForRevision = 0;
            seat.RequiresReady = false;
            if (string.IsNullOrWhiteSpace(seat.DisplayName) ||
                seat.DisplayName.StartsWith("Local Player", StringComparison.OrdinalIgnoreCase))
                seat.DisplayName = "BOT " + (i + 1);
        }

        Recount(snap);
        string newMatch = Guid.NewGuid().ToString("N");
        snap.MatchId = newMatch;
        snap.StartEventId = Guid.NewGuid().ToString("N");
        snap.LifecycleState = AtlasRoomLifecycleState.Starting;
        snap.StartCountdownEndsAtEpochMs = Now() + 5000L;

        // Create match state first. CAS on roomState is still the second line of defense
        // against duplicate host processes. SettingsRevision is gameplay configuration;
        // TransportRevision is only the authoritative room-state write sequence.
        bool created = await AtlasBoardSparkRtdbMatchTransport.CreateFromLobbyAsync(snap, token);
        if (!created)
            return Failure("lobby.error.service_unavailable",
                "RTDB match state creation failed. Check matchState rules.");

        long attemptedTransportRevision = snap.TransportRevision;
        StoreSnapshotResult saved = await StoreSnapshotDetailed(room, snap, token, Now());
        if (!saved.Success)
        {
            bool definitelyNotCommitted =
                saved.FailureKind == StoreSnapshotFailureKind.StaleRevision ||
                saved.FailureKind == StoreSnapshotFailureKind.Collision;

            if (definitelyNotCommitted)
            {
                bool rolledBack = await RollbackCreatedMatchAsync(newMatch, token);
                if (!rolledBack)
                {
                    Debug.LogWarning("ATB_RTDB_MATCH_START_ROLLBACK success=false match=" +
                        newMatch + " reason=created_match_delete_failed");
                    return Failure("lobby.error.service_unavailable",
                        "RTDB match start did not commit and rollback could not be verified.");
                }

                AtlasLobbySnapshot latest = await ReadRoomSnapshotOnlyAsync(room, token, 3);
                if (latest == null)
                    return Failure("lobby.error.service_unavailable",
                        "RTDB lobby changed during match start and could not be refreshed.");

                if (latest.SettingsRevision != stableSettingsRevision)
                {
                    Debug.LogWarning("ATB_RTDB_MATCH_REVISION_STALE expected=" +
                        stableSettingsRevision + " actual=" + latest.SettingsRevision +
                        " stage=cas_rebase");
                    return Failure("lobby.error.stale_settings",
                        "Room settings changed; refresh before starting.");
                }

                if (allowTransportRebase &&
                    latest.LifecycleState == AtlasRoomLifecycleState.Waiting &&
                    string.Equals(latest.HostAccountId, uid, StringComparison.Ordinal))
                {
                    Debug.Log("ATB_RTDB_LOBBY_CAS_REBASE retry=true transport_old=" +
                        attemptedTransportRevision + " transport_new=" +
                        latest.TransportRevision + " settings_revision=" +
                        stableSettingsRevision);
                    return await StartCasualMatchAsync(
                        room, latest, uid, token, stableSettingsRevision, false);
                }

                return Failure("lobby.error.service_unavailable",
                    "RTDB match start lost the room-state CAS and was rolled back safely.");
            }

            // A timeout/non-CAS HTTP failure can be ambiguous: the Firebase PUT may have
            // committed even if its response was lost. Verify roomState before deciding
            // whether deleting the freshly-created match is safe.
            AtlasLobbySnapshot verified = await ReadRoomSnapshotOnlyAsync(room, token, 3);
            if (verified != null &&
                string.Equals(verified.HostAccountId, uid, StringComparison.Ordinal) &&
                string.Equals(verified.MatchId, newMatch, StringComparison.Ordinal) &&
                verified.LifecycleState != AtlasRoomLifecycleState.Waiting)
            {
                snap = verified;
                Debug.Log("ATB_RTDB_MATCH_START_OUTCOME verified=true committed=true match=" +
                    newMatch);
            }
            else if (verified != null)
            {
                bool rolledBack = await RollbackCreatedMatchAsync(newMatch, token);
                if (!rolledBack)
                    Debug.LogWarning("ATB_RTDB_MATCH_START_ROLLBACK success=false match=" +
                        newMatch + " reason=verified_uncommitted_delete_failed");
                return Failure("lobby.error.service_unavailable",
                    rolledBack
                        ? "RTDB match start did not commit; the created match was rolled back."
                        : "RTDB match start did not commit and rollback could not be verified.");
            }
            else
            {
                // Do not delete on an unknowable outcome: doing so could leave canonical
                // roomState pointing at a match that this client just destroyed.
                Debug.LogWarning("ATB_RTDB_MATCH_START_OUTCOME verified=false " +
                    "rollback_deferred=true match=" + newMatch);
                return Failure("lobby.error.service_unavailable",
                    "RTDB match start outcome could not be verified; reconnect before retrying.");
            }
        }

        if (snap.Visibility == AtlasRoomVisibility.Public)
            await StoreIndex(room, snap, token, Now());
        string code = await GetPersistedRoomCode(room, token);
        Debug.Log("ATB_SPARK_MATCH_START committed=true transport=RTDB " +
            "countdown=5 local_humans=" + snap.Settings.LocalHumanCount +
            " remote_humans=" + snap.Settings.RemoteHumanCount +
            " bots=" + snap.Settings.BotCount + " rewarded=false");
        return AtlasLobbyOperationResult.Ok(snap, code, true, true);
    }

    private static async Task<AtlasLobbyOperationResult> UpdatePasswordAsync(
        string room,string password,string uid,string token)
    {
        AtlasLobbyOperationResult result=await SnapshotAsync(room,uid,token,"");
        if(!result.Success||result.Snapshot.HostAccountId!=uid)return Failure("lobby.error.access_denied","Host required.");
        string hash=string.IsNullOrEmpty(password)?"":Digest(room+":"+password);
        if(!await Write("PATCH","roomMeta/"+Safe(room),token,
            "{\"passwordHash\":\""+hash+"\",\"updatedAt\":"+Now()+"}"))
            return Failure("lobby.error.service_unavailable","Password change failed.");
        result.Snapshot.HasPassword=hash.Length>0;
        await StoreSnapshot(room,result.Snapshot,token,Now());
        if (result.Snapshot.Visibility == AtlasRoomVisibility.Public)
            await StoreIndex(room,result.Snapshot,token,Now());
        return result;
    }

    private static async Task<AtlasLobbyOperationResult> LeaveAsync(string room,string uid,string token)
    {
        AtlasLobbyOperationResult state=await SnapshotAsync(room,uid,token,"");
        if(!state.Success)return state;
        if(state.Snapshot.HostAccountId==uid)return await CloseAsync(room,uid,token);
        await Send("PATCH","roomMembers/"+Safe(room)+"/"+Safe(uid),token,"{\"active\":false}");
        return AtlasLobbyOperationResult.Ok(null);
    }

    private static async Task<AtlasLobbyOperationResult> CloseAsync(
        string room, string uid, string token, bool verifiedIdleCleanup = false,
        long expectedUpdatedAt = 0L, long idleThresholdMs = 0L)
    {
        // Read current owner first. Legacy partial deletes may have no
        // roomState, and SnapshotAsync intentionally rejects missing records.
        // This cleanup endpoint must still be able to finish those CLOSED
        // records without accepting a different owner's room.
        Response ownerRead = await Send("GET", "roomMeta/" + Safe(room), token);
        Meta ownerMeta = ownerRead.Ok ? Parse<Meta>(ownerRead.Body) : null;
        if (ownerMeta == null || !string.Equals(ownerMeta.hostUid, uid, StringComparison.Ordinal))
            return Failure("lobby.error.access_denied", "Only current host can close room.");
        Response wireRead = await Send("GET", "roomState/" + Safe(room), token);
        State stateWire = wireRead.Ok ? Parse<State>(wireRead.Body) : null;
        AtlasLobbySnapshot snapshot = stateWire != null
            ? Parse<AtlasLobbySnapshot>(stateWire.snapshotJson) : null;
        if (snapshot != null && !string.Equals(snapshot.HostAccountId, uid,
                StringComparison.Ordinal))
            return Failure("lobby.error.access_denied", "Room host snapshot mismatch.");
        if (verifiedIdleCleanup)
        {
            // Fail-closed if a host refreshed the metadata or roomState since
            // the scan. Never repair a newly started match with stale evidence.
            if (ownerMeta.updatedAt != expectedUpdatedAt)
                return Failure("lobby.error.service_unavailable",
                    "Room metadata changed since cleanup verification.");
            if (string.IsNullOrWhiteSpace(ownerMeta.activeMatchId))
            {
                RoomCleanupDecision recheck = snapshot != null &&
                    !string.IsNullOrWhiteSpace(snapshot.MatchId)
                    ? await VerifyUnanchoredStartingMatchAsync(ownerMeta, stateWire,
                        snapshot, room, uid, token, idleThresholdMs)
                    : EvaluateStalePreMatchRoom(ownerMeta, stateWire, snapshot,
                        uid, Now(), idleThresholdMs);
                if (!recheck.Eligible)
                    return Failure("lobby.error.service_unavailable",
                        "Room activity or match state changed since cleanup verification.");
            }
            else
            {
                RoomCleanupDecision matchRecheck = await VerifyMatchInactiveAsync(
                    ownerMeta, snapshot, room, uid, token,
                    idleThresholdMs, idleThresholdMs);
                if (!matchRecheck.Eligible)
                    return Failure("lobby.error.service_unavailable",
                        "Match or human presence changed since cleanup verification.");
            }
        }
        if (snapshot == null && !ownerMeta.closed &&
            !(verifiedIdleCleanup && string.IsNullOrWhiteSpace(ownerMeta.activeMatchId)))
            return Failure("lobby.error.service_unavailable",
                "Unclosed room has no readable authoritative snapshot.");
        // A prior partial close is repairable even if its snapshot was removed.
        if (snapshot == null)
            snapshot = new AtlasLobbySnapshot {
                HostAccountId = uid,
                MatchId = ownerMeta.activeMatchId ?? string.Empty,
                Visibility = ownerMeta.mode == "publicCasualOnline"
                    ? AtlasRoomVisibility.Public : AtlasRoomVisibility.Private,
                Members = new List<AtlasLobbyMemberSnapshot>()
            };

        // Close the join gate first, so no new players can be admitted while
        // the host removes its own room data. Do not touch other hosts' rooms.
        Response closed = await Send("PATCH", "roomMeta/" + Safe(room), token,
            "{\"closed\":true,\"updatedAt\":" + Now() + "}");
        if (!closed.Ok)
            return Failure("lobby.error.service_unavailable",
                "Could not mark RTDB room closed (HTTP " + closed.Code + ").");

        bool codeDeleted = true;
        // NEVER delete a reused six-digit code that now belongs to another
        // room. A cached/stale code is not authority; verify its roomId.
        string code = ownerMeta.inviteCode;
        if (!string.IsNullOrWhiteSpace(code))
        {
            Response lookup = await Send("GET", "joinCodes/" + Safe(code), token);
            Code index = lookup.Ok ? Parse<Code>(lookup.Body) : null;
            if (!lookup.Ok)
            {
                codeDeleted = false;
                Debug.LogWarning("ATB_RTDB_ROOM_CLEANUP stage=invite_code_lookup http=" +
                    lookup.Code);
            }
            else if (index == null)
            {
                Debug.Log("ATB_RTDB_ROOM_CLEANUP stage=invite_code already_absent=true");
            }
            else if (index.roomId != room)
            {
                // Safe: deleting the metadata of THIS room must not remove a
                // different room's currently valid invite-code claim.
                Debug.Log("ATB_RTDB_ROOM_CLEANUP stage=invite_code " +
                    "stale_reference=true other_room_preserved=true");
            }
            else
            {
                Response removed = await Send("DELETE", "joinCodes/" + Safe(code), token);
                codeDeleted = removed.Ok;
                if (!removed.Ok)
                    Debug.LogWarning("ATB_RTDB_ROOM_CLEANUP stage=invite_code http=" +
                        removed.Code + " record_matches_room=true " +
                        "record_original_host=" + (index.hostUid == uid) +
                        " verify_deployed_rules=true");
            }
        }
        // A Public Directory record must disappear before metadata is removed;
        // its write permission is derived from roomMeta.hostUid/mode.
        bool directoryDeletedOk = true;
        if (snapshot.Visibility == AtlasRoomVisibility.Public)
        {
            Response directoryDeleted = await Send(
                "DELETE", "publicRoomDirectory/" + Safe(room), token);
            directoryDeletedOk = directoryDeleted.Ok;
            if (!directoryDeleted.Ok)
                Debug.LogWarning("ATB_RTDB_ROOM_CLEANUP stage=public_directory http=" +
                                 directoryDeleted.Code);
        }

        // Match-dependent branches must be deleted while room membership and
        // host metadata are still valid. Only this host's own match is touched.
        bool matchDeleted = true;
        string match = snapshot.MatchId ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(match))
        {
            Response matchProbe = verifiedIdleCleanup
                ? await Send("GET", "matchState/" + Safe(match), token)
                : new Response { Code = 200, Body = "match_state_not_probed" };
            if (verifiedIdleCleanup && matchProbe.Ok &&
                Parse<MatchLease>(matchProbe.Body) == null)
            {
                // A missing matchState cannot authorize matchPresence DELETE
                // under current Spark rules. Log possible unreachable legacy
                // presence data rather than lying about full-root cleanup.
                Debug.LogWarning("ATB_RTDB_ROOM_CLEANUP stage=match " +
                    "missing_match_state=true orphan_presence_unverified=true");
            }
            else
            {
                Response presence = await Send("DELETE", "matchPresence/" + Safe(match), token);
                Response matchState = await Send("DELETE", "matchState/" + Safe(match), token);
                matchDeleted = presence.Ok && matchState.Ok;
                if (!matchDeleted)
                    Debug.LogWarning("ATB_RTDB_ROOM_CLEANUP stage=match http=" +
                        presence.Code + "/" + matchState.Code);
            }
        }
        Response intents = await Send("DELETE", "intents/" + Safe(room), token);
        Response acks = await Send("DELETE", "intentAcks/" + Safe(room), token);
        Response joins = await Send("DELETE", "joinRequests/" + Safe(room), token);
        Response ready = await Send("DELETE", "roomReady/" + Safe(room), token);
        bool ephemeralDeleted = intents.Ok && acks.Ok && joins.Ok && ready.Ok;
        if (!ephemeralDeleted)
            Debug.LogWarning("ATB_RTDB_ROOM_CLEANUP stage=ephemeral http=" +
                intents.Code + "/" + acks.Code + "/" + joins.Code + "/" + ready.Code);

        // Preserve the host's ability to retry partial cleanup: do NOT remove
        // the last readable roomState/membership when any dependent cleanup
        // failed. roomMeta.closed=true already prevents new admissions.
        if (!matchDeleted || !ephemeralDeleted || !codeDeleted || !directoryDeletedOk)
        {
            Debug.LogWarning("ATB_RTDB_ROOM_CLEANUP retryable=true " +
                "dependents_failed=true host_membership_preserved=true");
            return Failure("lobby.error.service_unavailable",
                "Room closed to new joins; dependent cleanup failed and is retryable.");
        }

        // roomState and individual roomMembers are protected by RTDB rules
        // referencing roomMeta. Delete these BEFORE roomMeta. This prevents
        // the old implementation from leaving one large snapshot and several
        // dead host membership records after every smoke test or room exit.
        Response stateDeleted = await Send("DELETE", "roomState/" + Safe(room), token);
        bool membersDeleted = true;
        // Enumerate the actual server record, not a stale roomState seat list:
        // deleting a seat that was already removed can return 401 under the
        // current roomMembers validation and needlessly block retries.
        Response memberIndex = await Send("GET", "roomMembers/" + Safe(room), token);
        if (!memberIndex.Ok) membersDeleted = false;
        else foreach (var member in JsonObjectMembers(memberIndex.Body))
        {
            if (string.IsNullOrWhiteSpace(member.Key)) continue;
            Response erased = await Send("DELETE", "roomMembers/" + Safe(room) +
                "/" + Safe(member.Key), token);
            if (!erased.Ok)
            {
                membersDeleted = false;
                Debug.LogWarning("ATB_RTDB_ROOM_CLEANUP stage=member http=" + erased.Code);
            }
        }

        // If any dependent write failed, retain closed metadata so a host can
        // still repair its own abandoned records (rather than orphaning them
        // behind a deleted access-control root).
        bool metadataDeleted = false;
        if (stateDeleted.Ok && membersDeleted && matchDeleted && ephemeralDeleted &&
            codeDeleted && directoryDeletedOk)
        {
            Response metaDeleted = await Send("DELETE", "roomMeta/" + Safe(room), token);
            metadataDeleted = metaDeleted.Ok;
            if (!metaDeleted.Ok)
                Debug.LogWarning("ATB_RTDB_ROOM_CLEANUP stage=metadata http=" + metaDeleted.Code);
        }
        else
        {
            Debug.LogWarning("ATB_RTDB_ROOM_CLEANUP pending=true room_state=" +
                             stateDeleted.Code + " members_deleted=" + membersDeleted);
        }
        ActiveCode.Remove(room);
        RoomCreatedAt.Remove(room);
        CodeRenewAfter.Remove(room);
        bool completed = stateDeleted.Ok && membersDeleted &&
            matchDeleted && ephemeralDeleted && metadataDeleted &&
            codeDeleted && directoryDeletedOk;
        Debug.Log("ATB_RTDB_ROOM_CLOSED data_cleanup_attempted=true " +
                  "complete=" + completed + " no_other_rooms_touched=true");
        if (!completed)
            return Failure("lobby.error.service_unavailable",
                "Room marked closed, but owned-record cleanup is incomplete; " +
                "verify remaining RTDB records before treating it as removed.");
        return AtlasLobbyOperationResult.Ok(null);
    }

    private static void Recount(AtlasLobbySnapshot s)
    {
        int remote=0,bot=0,local=0,open=0;
        foreach(var seat in s.Members)
        {
            if(seat==null)continue;
            if(seat.Active && seat.SeatMode==AtlasLobbySeatMode.RemoteHuman)remote++;
            else if(seat.Active && (seat.SeatMode==AtlasLobbySeatMode.LocalHuman||seat.SeatMode==AtlasLobbySeatMode.HostLocal))local++;
            else if(seat.SeatMode==AtlasLobbySeatMode.Bot)bot++;
            else if(!seat.Active && seat.SeatMode==AtlasLobbySeatMode.OpenOnline)open++;
        }
        s.Settings.RemoteHumanCount=remote;
        s.Settings.LocalHumanCount=local;
        s.Settings.BotCount=bot;
        s.Settings.OpenOnlineSeatCount=open;
        s.Settings.RemoteReadyRequiredCount=remote;
        s.Settings.RequiredHumanPlayers=remote+local;
    }

    private static async Task<bool> StoreSnapshot(
        string room, AtlasLobbySnapshot snap, string token, long revision)
    {
        StoreSnapshotResult result =
            await StoreSnapshotDetailed(room, snap, token, revision);
        return result.Success;
    }

    private static async Task<StoreSnapshotResult> StoreSnapshotDetailed(
        string room, AtlasLobbySnapshot snap, string token, long revision)
    {
        StoreSnapshotResult result = new StoreSnapshotResult
        {
            Success = false,
            FailureKind = StoreSnapshotFailureKind.Transport,
            ExpectedRevision = snap != null ? snap.TransportRevision : 0L
        };
        if (snap == null || snap.Settings == null) return result;

        string path = "roomState/" + Safe(room);
        // SettingsRevision tracks gameplay configuration/readiness compatibility.
        // TransportRevision + Firebase ETag guard the serialized room-state write stream.
        Response current = await Send("GET", path, token, null, null, null, true);
        result.HttpCode = current.Code;
        if (!current.Ok) return result;

        State existing = Parse<State>(current.Body);
        long actual = existing != null ? existing.revision : 0L;
        long expected = snap.TransportRevision;
        result.ExpectedRevision = expected;
        result.ActualRevision = actual;
        if (actual != expected)
        {
            result.FailureKind = StoreSnapshotFailureKind.StaleRevision;
            Debug.LogWarning("ATB_RTDB_LOBBY_CAS stale=true expected=" +
                expected + " actual=" + actual);
            return result;
        }

        string etag = !string.IsNullOrEmpty(current.ETag) ? current.ETag :
            existing == null ? "null_etag" : string.Empty;
        if (string.IsNullOrEmpty(etag)) return result;

        long nextRevision = Math.Max(Math.Max(revision, actual + 1L), Now());
        // Do not leave the caller advanced when the conditional PUT loses the race.
        snap.TransportRevision = nextRevision;
        string phase = snap.LifecycleState == AtlasRoomLifecycleState.Waiting
            ? "waiting" : snap.LifecycleState == AtlasRoomLifecycleState.Starting
                ? "starting" : "in_match";
        State state = new State
        {
            revision = nextRevision,
            eventSequence = nextRevision,
            updatedAt = Now(),
            phase = phase,
            snapshotJson = JsonUtility.ToJson(snap)
        };

        Response saved = await Send("PUT", path, token,
            JsonUtility.ToJson(state), etag);
        result.HttpCode = saved.Code;
        if (saved.Ok)
        {
            result.Success = true;
            result.FailureKind = StoreSnapshotFailureKind.None;
            result.ActualRevision = nextRevision;
            return result;
        }

        snap.TransportRevision = expected;
        if (saved.Code == 412)
        {
            result.FailureKind = StoreSnapshotFailureKind.Collision;
            Debug.LogWarning("ATB_RTDB_LOBBY_CAS collision=true room_unchanged=true");
        }
        return result;
    }

    private static async Task StoreIndex(string room,AtlasLobbySnapshot s,string token,long now)
    {
        if (!RoomCreatedAt.TryGetValue(room, out long originalCreatedAt))
        {
            // Once per resumed room only: keep directory order stable across
            // host heartbeats and seat/setting changes.
            Response existing = await Send("GET", "roomMeta/" + Safe(room), token);
            Meta meta = Parse<Meta>(existing.Body);
            originalCreatedAt = meta != null && meta.createdAt > 0 ? meta.createdAt : now;
            RoomCreatedAt[room] = originalCreatedAt;
        }
        Entry entry=new Entry{
            roomId=room,hostDisplayName=s.Members.Count>0?s.Members[0].DisplayName:"Host",
            mapId=s.Settings.MapId,themeId=s.Settings.ThemeId,roundLimit=s.Settings.RoundLimit,
            maxPlayers=s.Settings.MaxPlayers,occupiedPlayers=s.Settings.LocalHumanCount+s.Settings.RemoteHumanCount,
            openOnlineSeatCount=s.Settings.OpenOnlineSeatCount,
            regionId=s.RegionId,gameVersion=s.GameVersion,protocolVersion=s.ProtocolVersion,
            rulesVersion=s.RulesVersion,contentVersion=s.ContentVersion,
            settingsRevision=s.SettingsRevision,createdAt=originalCreatedAt,updatedAt=now,
            joinable=s.LifecycleState==AtlasRoomLifecycleState.Waiting&&s.Settings.OpenOnlineSeatCount>0,
            hasPassword=s.HasPassword
        };
        await Write("PUT","publicRoomDirectory/"+Safe(room),token,JsonUtility.ToJson(entry));
    }

    private static async Task<string> GetPersistedRoomCode(string room, string token)
    {
        if (ActiveCode.TryGetValue(room, out string known) && known.Length == 6)
            return known;
        Response response = await Send("GET", "roomMeta/" + Safe(room), token);
        Meta meta = response.Ok ? Parse<Meta>(response.Body) : null;
        string code = meta != null ? meta.inviteCode : string.Empty;
        if (!string.IsNullOrEmpty(code) && code.Length == 6)
        {
            ActiveCode[room] = code;
            return code;
        }
        return string.Empty;
    }

    private static async Task RenewInviteCodeIfNeeded(
        string room, string uid, string token)
    {
        long now = Now();
        if (CodeRenewAfter.TryGetValue(room, out long after) && now < after)
            return;
        Response response = await Send("GET", "roomMeta/" + Safe(room), token);
        Meta meta = response.Ok ? Parse<Meta>(response.Body) : null;
        if (meta == null || meta.closed || meta.hostUid != uid ||
            string.IsNullOrWhiteSpace(meta.inviteCode)) return;
        string code = meta.inviteCode;
        ActiveCode[room] = code;
        Response lookup = await Send("GET", "joinCodes/" + Safe(code), token);
        Code index = lookup.Ok ? Parse<Code>(lookup.Body) : null;
        if (index != null && index.hostUid == uid && index.roomId == room &&
            index.expiresAt <= now + 45L * 60L * 1000L)
        {
            Code updated = new Code { roomId = room, hostUid = uid,
                expiresAt = now + 7200000L };
            await Write("PUT", "joinCodes/" + Safe(code), token,
                JsonUtility.ToJson(updated));
        }
        // Refresh no more than every 30 minutes on a healthy room.
        CodeRenewAfter[room] = now + 30L * 60L * 1000L;
    }

    private static Task<bool> Write(string verb,string path,string token,string json) =>
        WriteCore(verb,path,token,json);
    private static async Task<bool> WriteCore(string verb,string path,string token,string json) =>
        (await Send(verb,path,token,json)).Ok;

    private static async Task<Response> Send(string verb,string path,string token,
        string json=null,string ifMatch=null,string query=null,bool wantEtag=false)
    {
        string url=Database+"/"+path+".json?auth="+UnityWebRequest.EscapeURL(token);
        if (!string.IsNullOrEmpty(query)) url += "&" + query;
        using(UnityWebRequest req=new UnityWebRequest(url,verb))
        {
            req.downloadHandler=new DownloadHandlerBuffer();
            if(json!=null){req.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type","application/json");}
            if(ifMatch!=null)req.SetRequestHeader("if-match",ifMatch);
            if(wantEtag)req.SetRequestHeader("X-Firebase-ETag","true");
            req.timeout=15;
            var operation=req.SendWebRequest();
            var done=new TaskCompletionSource<bool>();
            operation.completed+=_=>done.TrySetResult(true);
            if(operation.isDone)done.TrySetResult(true);
            await done.Task;
            return new Response{Code=req.responseCode,
                Body=req.downloadHandler!=null?req.downloadHandler.text:"",
                ETag=req.GetResponseHeader("ETag")};
        }
    }

    private static T Parse<T>(string json) where T:class
    {
        try {return string.IsNullOrWhiteSpace(json)||json.Trim()=="null" ? null:JsonUtility.FromJson<T>(json);}
        catch {return null;}
    }
    private static string Safe(string value) =>UnityWebRequest.EscapeURL(value??"");
    private static long Now()=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static string CleanName(string name)=>string.IsNullOrWhiteSpace(name)?"Player":
        name.Trim().Substring(0,Math.Min(32,name.Trim().Length));
    private static AtlasLobbyOperationResult Failure(string key,string message)=>
        AtlasLobbyOperationResult.Fail(key,message);
    private static string EscapeJson(string raw) => (raw ?? string.Empty)
        .Replace("\\", "\\\\").Replace("\"", "\\\"");
    private static string Digest(string raw)
    {
        using(SHA256 sha=SHA256.Create())
        {
            byte[] bytes=sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            StringBuilder b=new StringBuilder(bytes.Length*2);
            foreach(byte value in bytes)b.Append(value.ToString("x2"));
            return b.ToString();
        }
    }

    // Enumerate object members without Newtonsoft or an SDK-specific JSON module.
    // Values remain raw JSON and are parsed with Unity JsonUtility.
    private static IEnumerable<KeyValuePair<string,string>> JsonObjectMembers(string json)
    {
        if(string.IsNullOrWhiteSpace(json)||json.Trim()=="null")yield break;
        int i=0,n=json.Length;
        while(i<n&&char.IsWhiteSpace(json[i]))i++;
        if(i>=n||json[i++]!='{')yield break;
        while(i<n)
        {
            while(i<n&&(char.IsWhiteSpace(json[i])||json[i]==','))i++;
            if(i>=n||json[i]=='}')yield break;
            if(json[i++]!='"')yield break;
            StringBuilder key=new StringBuilder();
            bool escape=false;
            while(i<n)
            {
                char ch=json[i++];
                if(escape){key.Append(ch);escape=false;continue;}
                if(ch=='\\'){escape=true;continue;}
                if(ch=='"')break;
                key.Append(ch);
            }
            while(i<n&&char.IsWhiteSpace(json[i]))i++;
            if(i>=n||json[i++]!=':')yield break;
            while(i<n&&char.IsWhiteSpace(json[i]))i++;
            int start=i,depth=0;bool inString=false,slash=false;
            while(i<n)
            {
                char ch=json[i];
                if(inString)
                {
                    if(slash)slash=false;
                    else if(ch=='\\')slash=true;
                    else if(ch=='"')inString=false;
                    i++;continue;
                }
                if(ch=='"'){inString=true;i++;continue;}
                if(ch=='{'||ch=='[')depth++;
                else if(ch=='}'||ch==']')
                {if(depth==0)break;depth--;}
                if(ch==','&&depth==0)break;
                i++;
            }
            yield return new KeyValuePair<string,string>(key.ToString(),
                json.Substring(start,i-start));
        }
    }
}
