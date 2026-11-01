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
/// RTDB-only, owner-UID-bound peer-attested Career journal. This is not an
/// independent authoritative match replay. Never award Gold or ranked rewards
/// from a peer-attested result; the final host frame can still be manipulated.
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
            var completion = new TaskCompletionSource<bool>();
            var operation = request.SendWebRequest();
            operation.completed += _ => completion.TrySetResult(true);
            if (operation.isDone) completion.TrySetResult(true);
            await completion.Task;
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

    private static int Int(JToken token, string field, int max)
    {
        return Mathf.Clamp((int?)token?[field] ?? 0, 0, max);
    }

    private static string String(JToken token, string field)
    {
        return (string)token?[field] ?? string.Empty;
    }

    private static int Indexed(JToken token, string field, int slot, int max)
    {
        JArray items = token?[field] as JArray;
        return items != null && slot >= 0 && slot < items.Count
            ? Mathf.Clamp((int?)items[slot] ?? 0, 0, max) : 0;
    }

    private static bool IndexedBool(JToken token, string field, int slot)
    {
        JArray items = token?[field] as JArray;
        return items != null && slot >= 0 && slot < items.Count &&
               ((bool?)items[slot] ?? false);
    }

    public static int XpAward(string result, int rounds, int properties, int developments)
    {
        return 25 + (result == "win" ? 50 : result == "tie" ? 20 : 0) +
            Mathf.Min(100, Mathf.Clamp(rounds, 0, 100) * 3) +
            Mathf.Min(50, Mathf.Clamp(properties, 0, 100) * 5) +
            Mathf.Min(75, Mathf.Clamp(developments, 0, 100) * 3);
    }

    /// <summary>
    /// Proposed match Gold schedule (max 120). Pure calculation only.
    /// This value MUST NOT be treated as spendable or written to metaEconomy
    /// until the existing RTDB wallet transaction and security rules support
    /// an idempotent, UID-bound MATCH_REWARD operation. Peer attestation alone
    /// cannot independently prove a completed match against colluding clients.
    /// </summary>
    public static int GoldAward(string result, int rounds,
        int properties, int developments)
    {
        if (result != "win" && result != "tie" && result != "loss")
            return 0;

        int outcome = result == "win" ? 50 : result == "tie" ? 20 : 5;
        int roundBonus = Mathf.Min(30, Mathf.Max(0, rounds));
        int propertyBonus = Mathf.Min(20, Mathf.Max(0, properties) * 2);
        int developmentBonus = Mathf.Min(20, Mathf.Max(0, developments) * 2);
        return Mathf.Min(120, outcome + roundBonus + propertyBonus +
            developmentBonus);
    }

    private static string Safe(string text) => UnityWebRequest.EscapeURL(text ?? string.Empty);

    private static bool CurrentIdentity(string uid)
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        return user != null && user.UserId == uid;
    }

    // Recorded during actual online play, before any final-result publication.
    // An authenticated participant signs only their own immutable UID/slot.
    public static async Task<bool> RegisterParticipationAsync(string matchId,
        AtlasBoardLobbyRuntimeBridge bridge,
        AtlasBoardTurnDiceNetworkCoordinator coordinator)
    {
        if (bridge == null || coordinator == null || !coordinator.IsOnlineSessionActive ||
            string.IsNullOrWhiteSpace(matchId)) return false;
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null || user.UserId != bridge.CurrentAccountId ||
            !await AtlasBoardRtdbMetaEconomy.IsCareerRewardEligibleAsync())
            return false;
        string uid = user.UserId;
        string token = await user.TokenAsync(false);
        if (string.IsNullOrEmpty(token) || !CurrentIdentity(uid)) return false;
        string path = "/careerParticipation/" + Safe(uid) + "/" + Safe(matchId);
        if (ObjectOrNull(await RequestAsync(path, token)) != null) return true;
        JObject state = ObjectOrNull(await RequestAsync("/matchState/" + Safe(matchId), token));
        if (state == null || String(state, "status") != "active" ||
            String(state, "phase") == "match_complete") return false;
        string room = String(state, "lobbyId");
        JObject members = ObjectOrNull(await RequestAsync("/roomMembers/" + Safe(room), token));
        if (members == null || (bool?)members[uid]?["active"] != true) return false;
        string role = String(members[uid], "role");
        if (role != "host" && role != "remoteHuman") return false;
        int slot = -1;
        for (int i = 0; i < 4; i++)
            if (coordinator.IsLocallyControlledHumanSlot(i)) { slot = i; break; }
        if (slot < 0) return false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        JObject receipt = new JObject {
            ["uid"] = uid, ["matchId"] = matchId, ["lobbyId"] = room,
            ["slotIndex"] = slot, ["role"] = role, ["atMs"] = now
        };
        if (!CurrentIdentity(uid)) return false;
        try { await RequestAsync(path, token, receipt.ToString(Newtonsoft.Json.Formatting.None)); }
        catch (InvalidOperationException) { /* another attempt or server rejection */ }
        return ObjectOrNull(await RequestAsync(path, token)) != null;
    }

    private static async Task<bool> EnsureFinalProofAsync(string matchId,
        string uid, string token, AtlasBoardLobbyRuntimeBridge bridge)
    {
        string path = "/careerFinalProofs/" + Safe(matchId);
        if (ObjectOrNull(await RequestAsync(path, token)) != null) return true;
        JObject state = ObjectOrNull(await RequestAsync("/matchState/" + Safe(matchId), token));
        if (state == null || String(state, "status") != "complete" ||
            String(state, "phase") != "match_complete" || String(state, "hostUid") != uid)
            return false;
        string room = String(state, "lobbyId");
        JObject members = ObjectOrNull(await RequestAsync("/roomMembers/" + Safe(room), token));
        string witness = string.Empty;
        if (members != null)
            foreach (JProperty member in members.Properties())
            {
                if (member.Name == uid || String(member.Value,"role") != "remoteHuman")
                    continue;
                // Only another human who signed before finality may witness.
                JObject signed = ObjectOrNull(await RequestAsync(
                    "/careerParticipation/" + Safe(member.Name) + "/" +
                    Safe(matchId), token));
                if (signed == null) continue;
                witness = member.Name;
                break;
            }
        if (string.IsNullOrWhiteSpace(witness)) return false;
        string snapshotJson = String(state, "snapshotJson");
        JObject frame = ObjectOrNull(snapshotJson);
        if (frame == null || (bool?)frame["matchResult"]?["valid"] != true ||
            String(frame, "phase") != "match_complete") return false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        JToken finalResult = frame["matchResult"];
        JArray winnerSlots = finalResult?["winnerSlots"] as JArray;
        int completedRounds = Int(frame, "currentRound", 100);
        int completedTurns = Int(frame, "completedTurns", 10000);
        JObject seats = new JObject();
        for (int index = 0; index < 4; index++)
        {
            bool won = winnerSlots != null && winnerSlots.Any(x => (int?)x == index);
            string kind = won ? winnerSlots.Count > 1 ? "tie" : "win" : "loss";
            seats[index.ToString()] = new JObject {
                ["result"] = kind,
                ["participating"] = IndexedBool(finalResult,"participating",index),
                ["properties"] = Indexed(finalResult,"propertyCount",index,100),
                ["developments"] = Indexed(finalResult,"developmentLevels",index,100),
                ["cash"] = Indexed(finalResult,"cash",index,100000000),
                ["netWorth"] = Indexed(finalResult,"netWorth",index,100000000),
                ["diceRolls"] = Indexed(frame,"careerDiceRolls",index,10000),
                ["doublesRolled"] = Indexed(frame,"careerDoublesRolled",index,10000)
            };
        }
        AtlasLobbySettings settings = bridge.CurrentSnapshot?.Settings;
        string mapId = settings != null ? settings.MapId : "unknown";
        string themeId = settings != null ? settings.ThemeId : "classic_table";
        if (string.IsNullOrWhiteSpace(mapId)) mapId = "unknown";
        if (string.IsNullOrWhiteSpace(themeId)) themeId = "classic_table";
        JObject proof = new JObject {
            ["seats"] = seats, ["rounds"] = completedRounds, ["turns"] = completedTurns,
            ["playerCount"] = (finalResult?["participating"] as JArray)?.Count(x => (bool?)x == true) ?? 0,
            ["mapId"] = mapId.Substring(0, Mathf.Min(64,mapId.Length)),
            ["themeId"] = themeId.Substring(0, Mathf.Min(64,themeId.Length)),
            ["roundLimit"] = settings != null ? Mathf.Clamp(settings.RoundLimit,1,100) :
                Mathf.Clamp(Int(frame,"roundLimit",100),1,100),
            ["matchId"] = matchId, ["lobbyId"] = room, ["hostUid"] = uid,
            ["witnessUid"] = witness, ["revision"] = Int(state, "revision", 10000000),
            ["digest"] = Hash(snapshotJson), ["snapshotJson"] = snapshotJson,
            ["atMs"] = now
        };
        if (!CurrentIdentity(uid) || bridge.CurrentAccountId != uid) return false;
        try { await RequestAsync(path, token, proof.ToString(Newtonsoft.Json.Formatting.None)); }
        catch (InvalidOperationException) { }
        return ObjectOrNull(await RequestAsync(path, token)) != null;
    }

    public static async Task<RecordStatus> RecordAsync(
        string matchId,
        AtlasBoardLobbyRuntimeBridge bridge,
        AtlasBoardTurnDiceNetworkCoordinator coordinator,
        MatchResultManager resultManager,
        TurnManager turnManager,
        AtlasBoardProgressionSlotTelemetry[] telemetry)
    {
        var outcome = new RecordStatus { Reason = "not_eligible" };
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null || bridge == null || coordinator == null ||
            !coordinator.IsOnlineSessionActive || string.IsNullOrWhiteSpace(matchId) ||
            user.UserId != bridge.CurrentAccountId ||
            !await AtlasBoardRtdbMetaEconomy.IsCareerRewardEligibleAsync()) return outcome;
        string token = await user.TokenAsync(false);
        if (string.IsNullOrWhiteSpace(token)) return outcome;
        string uid = user.UserId;
        if (coordinator.LocalIsHost)
            await EnsureFinalProofAsync(matchId, uid, token, bridge);
        return await ClaimFromFinalAsync(matchId, uid, token, bridge,
            coordinator, resultManager, turnManager, false);
    }

    // Called when a player opens Career after disconnect, app restart or
    // delayed network availability. The original signed participation survives
    // even when the player's local match objects are gone.
    public static async Task RecoverPendingAsync(string uid, string token)
    {
        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(token) ||
            !CurrentIdentity(uid)) return;
        JObject receipts = ObjectOrNull(await RequestAsync(
            "/careerParticipation/" + Safe(uid), token));
        if (receipts == null) return;
        int inspected = 0;
        foreach (JProperty entry in receipts.Properties().Reverse())
        {
            if (++inspected > 30 || !CurrentIdentity(uid)) break;
            try
            {
                if (String(entry.Value, "uid") != uid) continue;
                await ClaimFromFinalAsync(entry.Name, uid, token, null, null, null, null, true);
                JObject claim = ObjectOrNull(await RequestAsync(
                    "/careerClaims/" + Safe(uid) + "/" + Safe(entry.Name), token));
                if (claim != null && Int(claim, "goldAwarded", 120) > 0)
                    await AtlasBoardRtdbMetaEconomy.ClaimMatchGoldAsync(entry.Name);
            }
            catch (Exception e)
            {
                Debug.LogWarning("ATB_CAREER_RECOVERY pending=" + e.GetType().Name);
            }
        }
    }

    private static async Task<RecordStatus> ClaimFromFinalAsync(
        string matchId, string uid, string token,
        AtlasBoardLobbyRuntimeBridge bridge,
        AtlasBoardTurnDiceNetworkCoordinator coordinator,
        MatchResultManager resultManager,
        TurnManager turnManager,
        bool allowRecovery)
    {
        var outcome = new RecordStatus { Reason = "final_proof_pending" };
        if (!CurrentIdentity(uid)) { outcome.Reason = "identity_changed"; return outcome; }
        JObject own = ObjectOrNull(await RequestAsync("/careerParticipation/" +
            Safe(uid) + "/" + Safe(matchId), token));
        if (own == null || String(own, "uid") != uid)
        { outcome.Reason = "participation_missing"; return outcome; }
        JObject proof = ObjectOrNull(await RequestAsync(
            "/careerFinalProofs/" + Safe(matchId), token));
        if (proof == null) return outcome;
        string snapshotJson = String(proof, "snapshotJson");
        JObject frame = ObjectOrNull(snapshotJson);
        JToken result = frame?["matchResult"];
        if (frame == null || result == null || (bool?)result["valid"] != true ||
            String(frame, "phase") != "match_complete" ||
            String(proof, "digest") != Hash(snapshotJson))
        { outcome.Reason = "invalid_final_proof"; return outcome; }
        string host = String(proof, "hostUid");
        string witness = uid == host ? String(proof, "witnessUid") : host;
        int slot = Int(own, "slotIndex", 3);
        if (witness == uid || string.IsNullOrWhiteSpace(witness) ||
            !IndexedBool(result, "participating", slot))
        { outcome.Reason = "second_human_required"; return outcome; }

        string claimPath = "/careerClaims/" + Safe(uid) + "/" + Safe(matchId);
        if (ObjectOrNull(await RequestAsync(claimPath, token)) != null)
        {
            outcome.Applied = true; outcome.Reason = "already_recorded";
            await AtlasBoardRtdbMetaEconomy.ClaimMatchGoldAsync(matchId);
            return outcome;
        }
        bool localVerified = false;
        if (!allowRecovery && coordinator != null && resultManager != null)
        {
            var local = resultManager.BuildOnlineResultSnapshot();
            JArray winning = result["winnerSlots"] as JArray;
            bool localWon = local?.winnerSlots != null && local.winnerSlots.Contains(slot);
            bool proofWon = winning != null && winning.Any(x => (int?)x == slot);
            localVerified = local != null && local.valid &&
                coordinator.IsLocallyControlledHumanSlot(slot) &&
                local.participating[slot] && localWon == proofWon &&
                Indexed(result, "netWorth", slot, int.MaxValue) ==
                    Mathf.Max(0, local.netWorth[slot]) &&
                Indexed(result, "cash", slot, int.MaxValue) ==
                    Mathf.Max(0, local.cash[slot]) &&
                Indexed(result, "propertyCount", slot, 100) ==
                    Mathf.Clamp(local.propertyCount[slot], 0, 100) &&
                Indexed(result, "developmentLevels", slot, 100) ==
                    Mathf.Clamp(local.developmentLevels[slot], 0, 100);
            if (!localVerified) { outcome.Reason = "result_mismatch"; return outcome; }
        }
        string digest = String(proof, "digest");
        int revision = Int(proof, "revision", 10000000);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (localVerified)
        {
            string ownAttPath = "/careerAttestations/" + Safe(matchId) + "/" + Safe(uid);
            JObject prior = ObjectOrNull(await RequestAsync(ownAttPath, token));
            if (prior == null)
            {
                JObject att = new JObject {
                    ["uid"] = uid, ["matchId"] = matchId,
                    ["lobbyId"] = String(proof,"lobbyId"), ["digest"] = digest,
                    ["revision"] = revision, ["atMs"] = now
                };
                if (!CurrentIdentity(uid)) return outcome;
                await RequestAsync(ownAttPath, token, att.ToString(Newtonsoft.Json.Formatting.None));
            }
            else if (String(prior, "digest") != digest)
            { outcome.Reason = "changed_result"; return outcome; }
        }
        JObject witnessAtt = ObjectOrNull(await RequestAsync(
            "/careerAttestations/" + Safe(matchId) + "/" + Safe(witness), token));
        if (witnessAtt == null || String(witnessAtt, "digest") != digest ||
            Int(witnessAtt, "revision", 10000000) != revision)
        {
            // A signed, eligible pre-finish participant still earns 1 XP
            // once an immutable final proof exists, even if they disconnect.
            await SaveParticipationCreditAsync(uid, matchId, witness, proof, token);
            outcome.Reason = "waiting_for_peer";
            return outcome;
        }
        JObject seat = proof["seats"]?[slot.ToString()] as JObject;
        if (seat == null || (bool?)seat["participating"] != true)
        { outcome.Reason = "missing_final_seat"; return outcome; }
        int rounds = Int(proof, "rounds", 100);
        int properties = Int(seat, "properties", 100);
        int developments = Int(seat, "developments", 100);
        string kind = String(seat, "result");
        int diceRolls = Int(seat, "diceRolls", 10000);
        int doubles = Int(seat, "doublesRolled", 10000);
        int turns = Int(proof, "turns", 10000);
        if (doubles > diceRolls || diceRolls > (turns * 3 + 10))
        { outcome.Reason = "dice_telemetry_invalid"; return outcome; }
        string mapId = String(proof, "mapId");
        string themeId = String(proof, "themeId");
        JObject claim = new JObject {
            ["uid"] = uid, ["matchId"] = matchId, ["lobbyId"] = String(proof,"lobbyId"),
            ["witnessUid"] = witness, ["digest"] = digest, ["revision"] = revision,
            ["atMs"] = now, ["result"] = kind, ["slotIndex"] = slot,
            ["rounds"] = rounds, ["turns"] = turns,
            ["properties"] = properties, ["developments"] = developments,
            ["cash"] = Int(seat, "cash", 100000000),
            ["netWorth"] = Int(seat, "netWorth", 100000000),
            ["playerCount"] = Int(proof, "playerCount", 4),
            ["mapId"] = mapId.Substring(0, Mathf.Min(64, mapId.Length)),
            ["themeId"] = themeId.Substring(0, Mathf.Min(64, themeId.Length)),
            ["roundLimit"] = Int(proof, "roundLimit", 100),
            ["hostAccountId"] = host, ["diceRolls"] = diceRolls,
            ["doublesRolled"] = doubles, ["recovered"] = !localVerified,
            ["xpAwarded"] = XpAward(kind, rounds, properties, developments),
            ["goldAwarded"] = GoldAward(kind, rounds, properties, developments)
        };
        if (!CurrentIdentity(uid) || (bridge != null && bridge.CurrentAccountId != uid))
        { outcome.Reason = "identity_changed"; return outcome; }
        try { await RequestAsync(claimPath, token, claim.ToString(Newtonsoft.Json.Formatting.None)); }
        catch (InvalidOperationException)
        {
            if (ObjectOrNull(await RequestAsync(claimPath, token)) == null)
            { outcome.Reason = "claim_denied"; return outcome; }
        }
        outcome.Applied = true;
        bool paid = await AtlasBoardRtdbMetaEconomy.ClaimMatchGoldAsync(matchId);
        outcome.Reason = paid ? "career_and_gold_saved" : "xp_saved_gold_pending";
        Debug.Log("ATB_CAREER_RTDB_CLAIM status=" + outcome.Reason +
            " xp=" + claim["xpAwarded"] + " gold=" + claim["goldAwarded"]);
        return outcome;
    }

    private static async Task SaveParticipationCreditAsync(string uid, string matchId,
        string witness, JObject proof, string token)
    {
        string path = "/careerParticipationCredits/" + Safe(uid) + "/" + Safe(matchId);
        if (ObjectOrNull(await RequestAsync(path, token)) != null) return;
        JObject credit = new JObject {
            ["uid"] = uid, ["matchId"] = matchId, ["witnessUid"] = witness,
            ["digest"] = String(proof,"digest"),
            ["xpAwarded"] = 1, ["atMs"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        if (!CurrentIdentity(uid)) return;
        try { await RequestAsync(path, token, credit.ToString(Newtonsoft.Json.Formatting.None)); }
        catch (InvalidOperationException) { }
    }

    private static void AddProvisionalAchievement(AtlasBoardProgressionSnapshot snapshot,
        string id, int target, int progress)
    {
        snapshot.Achievements.Add(new AtlasBoardProgressionAchievement {
            achievementId = id,
            target = target,
            progress = Mathf.Max(0, progress),
            unlocked = progress >= target
        });
    }

    public static async Task<AtlasBoardProgressionSnapshot> MergeAsync(
        AtlasBoardProgressionSnapshot snapshot, string uid, string token)
    {
        if (snapshot == null || !snapshot.Success ||
            string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(token))
            return snapshot;
        JObject claims = ObjectOrNull(await RequestAsync(
            "/careerClaims/" + UnityWebRequest.EscapeURL(uid), token));
        JObject participationCredits = ObjectOrNull(await RequestAsync(
            "/careerParticipationCredits/" + Safe(uid), token));
        if ((claims == null || claims.Count == 0) &&
            (participationCredits == null || participationCredits.Count == 0))
            return snapshot;
        // Server projection takes precedence. Never double-add provisional XP.
        if (snapshot.TechnicalMessage != "empty_authoritative_profile") return snapshot;
        List<JObject> entries = (claims ?? new JObject()).Properties().Select(p => p.Value as JObject)
            .Where(p => p != null && String(p, "uid") == uid &&
                XpAward(String(p, "result"), Int(p,"rounds",100),
                    Int(p,"properties",100), Int(p,"developments",100)) ==
                    Int(p,"xpAwarded",300))
            .OrderByDescending(x => (long?)x["atMs"] ?? 0).ToList();
        var completeMatchIds = new HashSet<string>(entries.Select(x => String(x,"matchId")),
            StringComparer.Ordinal);
        List<JObject> partial = (participationCredits ?? new JObject()).Properties()
            .Select(p => p.Value as JObject)
            .Where(x => x != null && String(x,"uid") == uid &&
                !completeMatchIds.Contains(String(x,"matchId")) &&
                Int(x,"xpAwarded",1) == 1)
            .OrderByDescending(x => (long?)x["atMs"] ?? 0).ToList();
        var stats = new AtlasBoardCareerStats();
        int totalXp = partial.Count; // one XP per distinct eligible unfinished attestation

        int streak = 0;
        foreach (JObject item in entries.OrderBy(x => (long?)x["atMs"] ?? 0))
        {
            totalXp = Mathf.Min(100000000, totalXp + Int(item, "xpAwarded", 300));
            stats.matchesPlayed++;
            string kind = String(item, "result");
            if (kind == "win")
            {
                stats.wins++;
                streak++;
                stats.bestWinStreak = Mathf.Max(stats.bestWinStreak, streak);
            }
            else
            {
                if (kind == "tie") stats.ties++;
                else stats.losses++;
                streak = 0;
            }
            stats.diceRolls = (int)Math.Min(int.MaxValue,
                (long)stats.diceRolls + Int(item,"diceRolls",10000));
            stats.doublesRolled = (int)Math.Min(int.MaxValue,
                (long)stats.doublesRolled + Int(item,"doublesRolled",10000));
            stats.totalRounds += Int(item, "rounds", 100);
            stats.totalTurns += Int(item, "turns", 10000);
            stats.propertiesAtFinishTotal += Int(item, "properties", 100);
            stats.developmentLevelsAtFinishTotal += Int(item, "developments", 100);
            stats.highestCash = Mathf.Max(stats.highestCash, Int(item, "cash", 100000000));
            stats.highestNetWorth = Mathf.Max(stats.highestNetWorth, Int(item, "netWorth", 100000000));
            stats.totalNetWorth = (int)Math.Min(int.MaxValue,
                (long)stats.totalNetWorth + Int(item, "netWorth", 100000000));
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
        snapshot.NextLevelXp = level >= MaxLevel ? threshold :
            threshold + Mathf.RoundToInt(500f * Mathf.Pow(1.1f, level - 1));
        snapshot.HistoryLimit = 10;
        snapshot.HistoryRetentionHours = 72;
        // These fourteen are CLIENT-SIDE PROVISIONAL indicators for this
        // peer-attested ledger. They are NOT Steam achievements or verified
        // backend unlocks. Dice/doubles cannot be inferred from old receipts.
        AddProvisionalAchievement(snapshot, "first_match", 1, stats.matchesPlayed);
        AddProvisionalAchievement(snapshot, "first_win", 1, stats.wins);
        AddProvisionalAchievement(snapshot, "win_streak_3", 3, stats.bestWinStreak);
        AddProvisionalAchievement(snapshot, "matches_10", 10, stats.matchesPlayed);
        AddProvisionalAchievement(snapshot, "wins_10", 10, stats.wins);
        AddProvisionalAchievement(snapshot, "dice_100", 100, stats.diceRolls);
        AddProvisionalAchievement(snapshot, "doubles_10", 10, stats.doublesRolled);
        AddProvisionalAchievement(snapshot, "properties_25", 25, stats.propertiesAtFinishTotal);
        AddProvisionalAchievement(snapshot, "builder_20", 20, stats.developmentLevelsAtFinishTotal);
        AddProvisionalAchievement(snapshot, "networth_5000", 5000, stats.highestNetWorth);
        AddProvisionalAchievement(snapshot, "turkey_explorer", 1, stats.mapTurkey);
        AddProvisionalAchievement(snapshot, "colorado_explorer", 1, stats.mapColorado);
        AddProvisionalAchievement(snapshot, "usa_explorer", 1, stats.mapUsa);
        AddProvisionalAchievement(snapshot, "atlas_traveler", 3,
            (stats.mapTurkey > 0 ? 1 : 0) +
            (stats.mapColorado > 0 ? 1 : 0) +
            (stats.mapUsa > 0 ? 1 : 0));
        long cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 72L * 3600000L;
        foreach (JObject item in partial.Where(x => ((long?)x["atMs"] ?? 0L) >= cutoff))
        {
            snapshot.RecentMatches.Add(new AtlasBoardProgressionMatchSummary {
                matchId = String(item,"matchId"), mode = "signed_participation_only",
                result = "participation", xpAwarded = 1,
                finishedAtEpochMs = (long?)item["atMs"] ?? 0L
            });
        }
        foreach (JObject item in entries.Where(x => ((long?)x["atMs"] ?? 0L) >= cutoff))
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
                diceRolls = Int(item,"diceRolls",10000), doublesRolled = Int(item,"doublesRolled",10000),
                xpAwarded = Int(item,"xpAwarded",300), levelAfter = level,
                finishedAtEpochMs = (long?)item["atMs"] ?? 0
            });
        }
        snapshot.RecentMatches = snapshot.RecentMatches
            .OrderByDescending(x => x.finishedAtEpochMs).Take(10).ToList();
        snapshot.TechnicalMessage = "peer_attested_not_server_verified";
        return snapshot;
    }
}
