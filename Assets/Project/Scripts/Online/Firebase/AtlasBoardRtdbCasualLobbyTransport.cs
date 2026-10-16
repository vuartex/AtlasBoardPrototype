using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
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

    [Serializable] private sealed class Request
    {
        public string lobbyId, roomCode, password, mapId, themeId, pawnCosmeticId;
        public string[] seatPolicies;
        public int slotIndex, maxPlayers, roundLimit, expectedSettingsRevision;
        public bool balancedDevelopment, doublesEnabled, tripleDoublePenaltyEnabled, ready;
    }
    [Serializable] private sealed class Meta
    {
        public string hostUid, mode, mapId, themeId, passwordHash, inviteCode;
        public long createdAt, updatedAt;
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
        public bool Ok => Code >= 200 && Code < 300;
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
        try
        {
            switch (operation)
            {
                case "lobbyCreatePrivateRoom":
                    return await CreateAsync(request, uid, token, displayName, false);
                case "lobbyCreatePublicRoom":
                    return await CreateAsync(request, uid, token, displayName, true);
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
    private static async Task<AtlasLobbyOperationResult> CreateAsync(
        Request req, string uid, string token, string name, bool isPublic)
    {
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
            MapId=meta.mapId,ThemeId=meta.themeId, RoundLimit=meta.roundLimit,
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
                    return AtlasLobbyOperationResult.Ok(snap,
                        admittedMeta.inviteCode??"",true);
                }
            }
        }
        return Failure("lobby.error.service_unavailable","RTDB host has not approved the join request.");
    }

    private static async Task<AtlasLobbyOperationResult> SnapshotAsync(
        string room,string uid,string token,string name)
    {
        if(string.IsNullOrWhiteSpace(room)) return Failure("lobby.error.not_joinable","Missing room ID.");
        Response response=await Send("GET","roomState/"+Safe(room),token);
        State state=Parse<State>(response.Body);
        AtlasLobbySnapshot snap=state!=null?Parse<AtlasLobbySnapshot>(state.snapshotJson):null;
        if(!response.Ok||snap==null) return Failure("lobby.error.not_joinable","RTDB room state unavailable.");
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
                foreach (string acceptedUid in accepted)
                    await Send("DELETE", "joinRequests/" + Safe(room) + "/" + Safe(acceptedUid), token);
                if(snap.Visibility==AtlasRoomVisibility.Public)
                    await StoreIndex(room,snap,token,Now());
            }
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
            if (join == null || string.IsNullOrEmpty(join.uid)) continue;
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
            if(seat==null)
                foreach(var member in snap.Members)if(member!=null && !member.Active && member.SeatMode==AtlasLobbySeatMode.OpenOnline){seat=member;break;}
            if(seat==null)continue;
            Member admitted=new Member{uid=join.uid,role="remoteHuman",displayName=CleanName(join.displayName),active=true,joinedAt=Now()};
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
            if(ready!=null && ready.ready && ready.revision==snap.SettingsRevision &&
                seat.ReadyForRevision!=ready.revision)
            {seat.ReadyForRevision=ready.revision;changed=true;}
        }
        return changed;
    }

    private static async Task<AtlasLobbyOperationResult> SetReadyAsync(
        string room,bool ready,string uid,string token)
    {
        AtlasLobbyOperationResult state=await SnapshotAsync(room,uid,token,"");
        if(!state.Success)return state;
        Ready update=new Ready{uid=uid,revision=state.Snapshot.SettingsRevision,ready=ready};
        if(!await Write("PUT","roomReady/"+Safe(room)+"/"+Safe(uid),token,JsonUtility.ToJson(update)))
            return Failure("lobby.error.service_unavailable","RTDB ready update rejected.");
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
        {
            // Do NOT launch a network match using the obsolete Functions-based
            // AtlasBoardMatchRuntimeBridge. Room creation/join are real RTDB,
            // synchronized gameplay is not yet implemented on Spark.
            return Failure("lobby.error.service_unavailable",
                "RTDB lobby is ready, but match-state transport has not been migrated.");
        }
        if(operation=="lobbyUpdateSettings")
        {
            snap.Settings.MapId=req.mapId;
            snap.Settings.ThemeId=req.themeId;
            snap.Settings.RoundLimit=Mathf.Clamp(req.roundLimit,1,100);
            snap.Settings.BalancedDevelopment=req.balancedDevelopment;
            snap.Settings.DoublesEnabled=req.doublesEnabled;
            snap.Settings.TripleDoublePenaltyEnabled=req.tripleDoublePenaltyEnabled;
        }
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
        if(operation=="lobbySetPawnCosmetic")
        {
            if(req.slotIndex>=0 && req.slotIndex<snap.Members.Count && snap.Members[req.slotIndex]!=null)
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
        string room, string uid, string token)
    {
        AtlasLobbyOperationResult state = await SnapshotAsync(room, uid, token, "");
        if (!state.Success || state.Snapshot == null) return state;
        if (!string.Equals(state.Snapshot.HostAccountId, uid, StringComparison.Ordinal))
            return Failure("lobby.error.access_denied", "Only host can close room.");

        // Close the join gate first, so no new players can be admitted while
        // the host removes its own room data. Do not touch other hosts' rooms.
        Response closed = await Send("PATCH", "roomMeta/" + Safe(room), token,
            "{\"closed\":true,\"updatedAt\":" + Now() + "}");
        if (!closed.Ok)
            return Failure("lobby.error.service_unavailable",
                "Could not mark RTDB room closed (HTTP " + closed.Code + ").");

        string code = await GetPersistedRoomCode(room, token);
        if (!string.IsNullOrEmpty(code))
        {
            Response codeDeleted = await Send("DELETE", "joinCodes/" + Safe(code), token);
            if (!codeDeleted.Ok)
                Debug.LogWarning("ATB_RTDB_ROOM_CLEANUP stage=invite_code http=" + codeDeleted.Code);
        }
        // A Public Directory record must disappear before metadata is removed;
        // its write permission is derived from roomMeta.hostUid/mode.
        if (state.Snapshot.Visibility == AtlasRoomVisibility.Public)
        {
            Response directoryDeleted = await Send(
                "DELETE", "publicRoomDirectory/" + Safe(room), token);
            if (!directoryDeleted.Ok)
                Debug.LogWarning("ATB_RTDB_ROOM_CLEANUP stage=public_directory http=" +
                                 directoryDeleted.Code);
        }

        // roomState and individual roomMembers are protected by RTDB rules
        // referencing roomMeta. Delete these BEFORE roomMeta. This prevents
        // the old implementation from leaving one large snapshot and several
        // dead host membership records after every smoke test or room exit.
        Response stateDeleted = await Send("DELETE", "roomState/" + Safe(room), token);
        bool membersDeleted = true;
        HashSet<string> members = new HashSet<string>(StringComparer.Ordinal);
        foreach (AtlasLobbyMemberSnapshot seat in state.Snapshot.Members)
        {
            if (seat == null || string.IsNullOrWhiteSpace(seat.AccountId) ||
                !members.Add(seat.AccountId)) continue;
            Response deleted = await Send("DELETE", "roomMembers/" + Safe(room) +
                "/" + Safe(seat.AccountId), token);
            if (!deleted.Ok)
            {
                membersDeleted = false;
                Debug.LogWarning("ATB_RTDB_ROOM_CLEANUP stage=member http=" + deleted.Code);
            }
        }
        if (!members.Contains(uid))
        {
            Response deleted = await Send("DELETE", "roomMembers/" + Safe(room) +
                "/" + Safe(uid), token);
            membersDeleted &= deleted.Ok;
        }

        // If any dependent write failed, retain closed metadata so a host can
        // still repair its own abandoned records (rather than orphaning them
        // behind a deleted access-control root).
        if (stateDeleted.Ok && membersDeleted)
        {
            Response metaDeleted = await Send("DELETE", "roomMeta/" + Safe(room), token);
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
        Debug.Log("ATB_RTDB_ROOM_CLOSED data_cleanup_attempted=true " +
                  "no_other_rooms_touched=true");
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

    private static async Task<bool> StoreSnapshot(string room,AtlasLobbySnapshot snap,string token,long revision)
    {
        State state=new State{
            revision=revision,eventSequence=revision,updatedAt=Now(),
            phase=snap.LifecycleState==AtlasRoomLifecycleState.Waiting?"waiting":"starting",
            snapshotJson=JsonUtility.ToJson(snap)
        };
        return await Write("PUT","roomState/"+Safe(room),token,JsonUtility.ToJson(state));
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
        string json=null,string ifMatch=null,string query=null)
    {
        string url=Database+"/"+path+".json?auth="+UnityWebRequest.EscapeURL(token);
        if (!string.IsNullOrEmpty(query)) url += "&" + query;
        using(UnityWebRequest req=new UnityWebRequest(url,verb))
        {
            req.downloadHandler=new DownloadHandlerBuffer();
            if(json!=null){req.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type","application/json");}
            if(ifMatch!=null)req.SetRequestHeader("if-match",ifMatch);
            req.timeout=15;
            var operation=req.SendWebRequest();
            var done=new TaskCompletionSource<bool>();
            operation.completed+=_=>done.TrySetResult(true);
            await done.Task;
            return new Response{Code=req.responseCode,
                Body=req.downloadHandler!=null?req.downloadHandler.text:""};
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
