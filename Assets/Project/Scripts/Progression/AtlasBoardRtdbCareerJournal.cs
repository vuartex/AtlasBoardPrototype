using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Spark-only peer-attested Career journal. NOT a server-replay anti-cheat verdict:
/// two distinct authenticated human accounts must approve an immutable match
/// digest before either can claim bounded, non-currency Career XP.
/// </summary>
public static class AtlasBoardRtdbCareerJournal
{
    private const string Database = "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    private const int MaxLevel = 100;

    public sealed class RecordStatus
    {
        public bool Applied;
        public string Reason;
    }

    private static async Task<string> RequestAsync(string path, string token, string body = null)
    {
        string url = Database + path + ".json?auth=" + UnityWebRequest.EscapeURL(token);
        using (UnityWebRequest request = body == null ? UnityWebRequest.Get(url) :
            new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT))
        {
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 12;
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);
            var tcs = new TaskCompletionSource<bool>();
            var op = request.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);
            if (op.isDone) tcs.TrySetResult(true);
            await tcs.Task;
            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException("RTDB_HTTP_" + request.responseCode);
            return request.downloadHandler.text ?? "null";
        }
    }

    private static JObject ObjectOrNull(string json)
    {
        try { return JToken.Parse(json) as JObject; }
        catch { return null; }
    }

    private static string Hash(string source)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(source)))
                .Replace("-", "").ToLowerInvariant();
    }

    private static int Int(JToken token, string name, int max)
    {
        return Mathf.Clamp((int?)token?[name] ?? 0, 0, max);
    }

    private static string String(JToken token, string name) => (string)token?[name] ?? string.Empty;

    private static int Indexed(JToken token, string field, int slot, int max)
    {
        JArray array = token?[field] as JArray;
        return array != null && slot >= 0 && slot < array.Count
            ? Mathf.Clamp((int?)array[slot] ?? 0, 0, max) : 0;
    }

    private static bool IndexedBool(JToken token, string field, int slot)
    {
        JArray array = token?[field] as JArray;
        return array != null && slot >= 0 && slot < array.Count &&
               ((bool?)array[slot] ?? false);
    }

    public static int XpAward(string result, int rounds, int properties, int developments)
    {
        return 25 + (result == "win" ? 50 : result == "tie" ? 20 : 0) +
            Mathf.Min(100, Mathf.Clamp(rounds, 0, 100) * 3) +
            Mathf.Min(50, Mathf.Clamp(properties, 0, 100) * 5) +
            Mathf.Min(75, Mathf.Clamp(developments, 0, 100) * 3);
    }

    public static async Task<RecordStatus> RecordAsync(
        string matchId, AtlasBoardLobbyRuntimeBridge bridge,
        AtlasBoardTurnDiceNetworkCoordinator coordinator,
        MatchResultManager resultManager, TurnManager turnManager,
        AtlasBoardProgressionSlotTelemetry[] telemetry)
    {
        var outcome = new RecordStatus { Reason = "not_eligible" };
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null || user.IsAnonymous || bridge == null || coordinator == null ||
            resultManager == null || !coordinator.IsOnlineSessionActive ||
            string.IsNullOrEmpty(matchId) || user.UserId != bridge.CurrentAccountId)
            return outcome;
        string token = await user.TokenAsync(false);
        if (string.IsNullOrEmpty(token)) return outcome;
        string uid = user.UserId;
        JObject state = ObjectOrNull(await RequestAsync("/matchState/" + UnityWebRequest.EscapeURL(matchId), token));
        if (state == null || String(state, "phase") != "match_complete" ||
            String(state, "status") != "complete")
        { outcome.Reason = "match_not_final"; return outcome; }
        string room = String(state, "lobbyId");
        string snapshotJson = String(state, "snapshotJson");
        JObject frame = ObjectOrNull(snapshotJson);
        JToken result = frame?["matchResult"];
        if (string.IsNullOrEmpty(room) || result == null || (bool?)result["valid"] != true ||
            String(frame, "phase") != "match_complete")
        { outcome.Reason = "invalid_host_frame"; return outcome; }
        JObject members = ObjectOrNull(await RequestAsync("/roomMembers/" + UnityWebRequest.EscapeURL(room), token));
        if (members == null || (bool?)members[uid]?["active"] != true)
            return outcome;
        string host = String(state, "hostUid");
        string witness = uid == host
            ? members.Properties().FirstOrDefault(p => p.Name != uid &&
                (bool?)p.Value["active"] == true && String(p.Value, "role") == "remoteHuman")?.Name
            : host;
        if (string.IsNullOrWhiteSpace(witness) || witness == uid ||
            (bool?)members[witness]?["active"] != true ||
            (uid != host && String(members[uid], "role") != "remoteHuman"))
        { outcome.Reason = "second_human_required"; return outcome; }
        int slot = -1;
        for (int i = 0; i < 4; i++)
            if (coordinator.IsLocallyControlledHumanSlot(i) && IndexedBool(result, "participating", i))
            { slot = i; break; }
        if (slot < 0) { outcome.Reason = "local_human_missing"; return outcome; }
        // Compare local result to host's canonical result. These two clients may
        // collude, so this is peer-attestation, never an authoritative verifier.
        var localResult = resultManager.BuildOnlineResultSnapshot();
        if (!localResult.valid || !localResult.participating[slot] ||
            Indexed(result, "netWorth", slot, int.MaxValue) != Mathf.Max(0, localResult.netWorth[slot]))
        { outcome.Reason = "result_mismatch"; return outcome; }
        string digest = Hash(snapshotJson);
        int revision = Int(state, "revision", 10000000);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        JObject attestation = new JObject {
            ["uid"] = uid, ["matchId"] = matchId, ["lobbyId"] = room,
            ["digest"] = digest, ["revision"] = revision, ["atMs"] = now
        };
        // A repeat write is denied. First read the immutable receipt to allow
        // retries and reconnection without mutating an earlier attestation.
        string ownAttPath = "/careerAttestations/" + UnityWebRequest.EscapeURL(matchId) + "/" + UnityWebRequest.EscapeURL(uid);
        JObject oldAtt = ObjectOrNull(await RequestAsync(ownAttPath, token));
        if (oldAtt == null) await RequestAsync(ownAttPath, token, attestation.ToString(Newtonsoft.Json.Formatting.None));
        else if (String(oldAtt, "digest") != digest)
        { outcome.Reason = "changed_result"; return outcome; }
        JObject witnessAtt = ObjectOrNull(await RequestAsync(
            "/careerAttestations/" + UnityWebRequest.EscapeURL(matchId) + "/" + UnityWebRequest.EscapeURL(witness), token));
        if (witnessAtt == null || String(witnessAtt, "digest") != digest || Int(witnessAtt, "revision", 10000000) != revision)
        { outcome.Reason = "waiting_for_peer"; return outcome; }
        int rounds = Int(frame, "currentRound", 100);
        int properties = Indexed(result, "propertyCount", slot, 100);
        int developments = Indexed(result, "developmentLevels", slot, 100);
        JArray winners = result["winnerSlots"] as JArray;
        bool won = winners != null && winners.Any(x => (int?)x == slot);
        string kind = won ? winners.Count > 1 ? "tie" : "win" : "loss";
        int xp = XpAward(kind, rounds, properties, developments);
        int playerCount = (result["participating"] as JArray)?.Count(x => (bool?)x == true) ?? 0;
        AtlasLobbySettings settings = bridge.CurrentSnapshot != null
            ? bridge.CurrentSnapshot.Settings : null;
        string mapId = settings != null ? settings.MapId : String(frame, "mapId");
        string themeId = settings != null ? settings.ThemeId : "classic_table";
        int roundLimit = settings != null ? Mathf.Clamp(settings.RoundLimit, 1, 100) : 20;
        JObject claim = new JObject {
            ["uid"] = uid, ["matchId"] = matchId, ["lobbyId"] = room,
            ["witnessUid"] = witness, ["digest"] = digest, ["revision"] = revision,
            ["atMs"] = now, ["result"] = kind, ["slotIndex"] = slot,
            ["rounds"] = rounds, ["turns"] = turnManager != null ? Mathf.Clamp(turnManager.CompletedTurns, 0, 10000) : 0,
            ["properties"] = properties, ["developments"] = developments,
            ["cash"] = Indexed(result, "cash", slot, 100000000),
            ["netWorth"] = Indexed(result, "netWorth", slot, 100000000),
            ["playerCount"] = Mathf.Clamp(playerCount, 2, 4),
            ["mapId"] = string.IsNullOrWhiteSpace(mapId) ? "unknown" : mapId.Substring(0, Mathf.Min(64, mapId.Length)),
            ["themeId"] = string.IsNullOrWhiteSpace(themeId) ? "classic_table" : themeId.Substring(0, Mathf.Min(64, themeId.Length)),
            ["roundLimit"] = roundLimit, ["hostAccountId"] = host,
            ["xpAwarded"] = xp
        };
        string claimPath = "/careerClaims/" + UnityWebRequest.EscapeURL(uid) + "/" + UnityWebRequest.EscapeURL(matchId);
        if (ObjectOrNull(await RequestAsync(claimPath, token)) != null)
        { outcome.Applied = true; outcome.Reason = "already_recorded"; return outcome; }
        await RequestAsync(claimPath, token, claim.ToString(Newtonsoft.Json.Formatting.None));
        outcome.Applied = true;
        outcome.Reason = "peer_attested_saved";
        Debug.Log("ATB_CAREER_RTDB_CLAIM status=saved xp=" + xp + " match=" + matchId);
        return outcome;
    }

    public static async Task<AtlasBoardProgressionSnapshot> MergeAsync(
        AtlasBoardProgressionSnapshot snapshot, string uid, string token)
    {
        if (snapshot == null || !snapshot.Success || string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(token))
            return snapshot;
        JObject claims = ObjectOrNull(await RequestAsync("/careerClaims/" + UnityWebRequest.EscapeURL(uid), token));
        if (claims == null || claims.Count == 0) return snapshot;
        // Only merge peer-attested provisional data when no protected server
        // Career projection exists. This avoids double counting a future writer.
        if (snapshot.TechnicalMessage != "empty_authoritative_profile") return snapshot;
        List<JObject> entries = claims.Properties().Select(p => p.Value as JObject)
            .Where(p => p != null && String(p, "uid") == uid &&
                XpAward(String(p, "result"), Int(p,"rounds",100),Int(p,"properties",100),Int(p,"developments",100)) == Int(p,"xpAwarded",300))
            .OrderByDescending(x => (long?)x["atMs"] ?? 0).ToList();
        var stats = new AtlasBoardCareerStats();
        int totalXp = 0;
        int streak = 0;
        foreach (JObject item in entries.OrderBy(x => (long?)x["atMs"] ?? 0))
        {
            totalXp = Mathf.Min(100000000, totalXp + Int(item, "xpAwarded", 300));
            stats.matchesPlayed++;
            string kind = String(item, "result");
            if (kind == "win") { stats.wins++; streak++; stats.bestWinStreak = Mathf.Max(stats.bestWinStreak, streak); }
            else { if (kind == "tie") stats.ties++; else stats.losses++; streak = 0; }
            stats.totalRounds += Int(item, "rounds", 100);
            stats.totalTurns += Int(item, "turns", 10000);
            stats.propertiesAtFinishTotal += Int(item, "properties", 100);
            stats.developmentLevelsAtFinishTotal += Int(item, "developments", 100);
            stats.highestCash = Mathf.Max(stats.highestCash, Int(item, "cash", 100000000));
            stats.highestNetWorth = Mathf.Max(stats.highestNetWorth, Int(item, "netWorth", 100000000));
            stats.totalNetWorth += Int(item, "netWorth", 100000000);
            string map = String(item, "mapId").ToLowerInvariant();
            if (map.Contains("turkey")) { stats.mapTurkey++; if (kind == "win") stats.mapWinsTurkey++; }
            else if (map.Contains("colorado")) { stats.mapColorado++; if (kind == "win") stats.mapWinsColorado++; }
            else if (map.Contains("usa")) { stats.mapUsa++; if (kind == "win") stats.mapWinsUsa++; }
            else { stats.mapOther++; if (kind == "win") stats.mapWinsOther++; }
        }
        stats.currentWinStreak = streak;
        int level = 1, threshold = 0;
        while (level < MaxLevel)
        {
            int requirement = Mathf.RoundToInt(500f * Mathf.Pow(1.1f, level - 1));
            if (totalXp < threshold + requirement) break;
            threshold += requirement;
            level++;
        }
        snapshot.Stats = stats;
        snapshot.TotalXp = totalXp;
        snapshot.Level = level;
        snapshot.MaxLevel = MaxLevel;
        snapshot.LevelStartXp = threshold;
        snapshot.NextLevelXp = level >= MaxLevel ? threshold : threshold + Mathf.RoundToInt(500f * Mathf.Pow(1.1f, level - 1));
        snapshot.HistoryLimit = 10;
        snapshot.HistoryRetentionHours = 72;
        long cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 72L*3600000L;
        foreach (JObject item in entries.Where(x => ((long?)x["atMs"] ?? 0L) >= cutoff).Take(10))
        {
            snapshot.RecentMatches.Add(new AtlasBoardProgressionMatchSummary {
                matchId = String(item,"matchId"), lobbyId = String(item,"lobbyId"),
                mapId = String(item,"mapId"), mode = "peer_attested_online",
                hostAccountId = String(item,"hostAccountId"),
                themeId = String(item,"themeId"), roundLimit = Int(item,"roundLimit",100),
                result = String(item,"result"), place = String(item,"result") == "win" ? 1 : 2,
                playerCount = Int(item,"playerCount",4), localSlotIndex = Int(item,"slotIndex",3),
                propertyCount = Int(item,"properties",100), developmentLevels = Int(item,"developments",100),
                finalCash = Int(item,"cash",100000000), netWorth = Int(item,"netWorth",100000000),
                completedRounds = Int(item,"rounds",100), completedTurns = Int(item,"turns",10000),
                xpAwarded = Int(item,"xpAwarded",300), levelAfter = level,
                finishedAtEpochMs = (long?)item["atMs"] ?? 0
            });
        }
        snapshot.TechnicalMessage = "peer_attested_not_server_verified";
        return snapshot;
    }
}
