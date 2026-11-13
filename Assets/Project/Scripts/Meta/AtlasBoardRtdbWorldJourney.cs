using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Spark-compatible World Journey. Verified Career claims are folded one at
/// a time into a rule-checked RTDB ledger; all prizes and the one-claim ledger
/// commit in the EXISTING ETag-protected metaEconomy account node.
/// </summary>
public static partial class AtlasBoardRtdbMetaEconomy
{
    // When Editor QA updates an account, an open Store can refresh in Play Mode
    // without closing and reopening the entire UI.
    public static int WorldQaRevision { get; private set; }
    private const string WorldEventId = "world_journey_2027_s1";
    private const long WorldStart = 1793664000000L;
    private const long WorldEnd = 1814400000000L;

    private sealed class WorldRewardSpec
    {
        public string Id, Metric, Type, Item;
        public int Target, Amount;
        public WorldRewardSpec(string id, string metric, int target,
            string type, int amount, string item = "")
        {
            Id = id; Metric = metric; Target = target;
            Type = type; Amount = amount; Item = item;
        }
    }

    private static readonly WorldRewardSpec[] WorldRewards = {
        new WorldRewardSpec("two_matches", "matches", 2, "gold", 500),
        new WorldRewardSpec("fifty_rolls", "diceRolls", 50, "xp", 500),
        new WorldRewardSpec("ten_developments", "developments", 10, "atlas_coin", 100),
        new WorldRewardSpec("three_wins", "wins", 3, "gold", 1000),
        new WorldRewardSpec("twenty_turns", "turns", 20, "xp", 1000),
        new WorldRewardSpec("three_maps", "mapCount", 3, "atlas_coin", 250),
        new WorldRewardSpec("networth_5000", "highestNetWorth", 5000, "gold", 2000),
        new WorldRewardSpec("world_explorer", "sevenClaims", 7,
            "inventory_item", 1, "map_world"),
        new WorldRewardSpec("completion_bonus", "eightClaims", 8,
            "inventory_item", 1, "board_atlas_horizon")
    };

    [Serializable]
    private sealed class WorldProof
    {
        public int schemaVersion = 1;
        public string proofHistory = "";
        public int matches, wins, diceRolls, developments, turns;
        public string maps = "";
        public int mapCount, highestNetWorth;
        public string lastMatchId = "";
        public long atMs;
        public bool qaMode;
    }

    private static string WorldPath(string uid) => "/users/" +
        UnityWebRequest.EscapeURL(uid) + "/worldJourney.json";
    private static string WorldHistoryPath(string uid) => "/users/" +
        UnityWebRequest.EscapeURL(uid) + "/worldJourneyHistory.json";
    private static int WInt(JToken node, string key) =>
        Math.Max(0, (int?)node?[key] ?? 0);
    private static string WString(JToken node, string key) =>
        (string)node?[key] ?? "";
    private static bool WorldRunning => Now() >= WorldStart && Now() < WorldEnd;
    // Never let a local-emulator scene silently write to the production RTDB URL.
    // This source uses the canonical live RTDB; emulator QA needs a separate
    // configured endpoint and is intentionally denied until one is supplied.
    private static bool WorldEmulatorSelected
    {
        get
        {
            var lobby = UnityEngine.Object.FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>();
            return lobby != null && lobby.UsingLocalEmulators;
        }
    }
    private static string WorldMarker(string id) => "|" + id + "|";
    private static bool WorldHas(string ledger, string id) =>
        (ledger ?? "").Contains(WorldMarker(id));

    private static async Task<WorldProof> WorldProgressAsync(Identity identity)
    {
        Response response = await RequestAsync(identity, "GET",
            WorldPath(identity.Uid), null, null);
        if ((response.Status == 401 || response.Status == 403) &&
            await RefreshRegisteredRestBearerAsync(identity))
            response = await RequestAsync(identity, "GET",
                WorldPath(identity.Uid), null, null);
        if (response.Status != 200)
            throw new InvalidOperationException("WORLD_PROOF_READ_HTTP_" + response.Status);
        return response.Body == "null" || string.IsNullOrWhiteSpace(response.Body)
            ? new WorldProof() : JsonUtility.FromJson<WorldProof>(response.Body);
    }

    // Every increment is checked by database.rules.json against careerClaims.
    // The processed-match string prevents counting the same signed match twice.
    private static async Task<WorldProof> FoldCareerProofsAsync(Identity identity)
    {
        WorldProof progress = await WorldProgressAsync(identity);
        if (!WorldRunning) return progress;
        Response response = await RequestAsync(identity, "GET",
            "/careerClaims/" + UnityWebRequest.EscapeURL(identity.Uid) + ".json",
            null, null);
        if (response.Status != 200)
            throw new InvalidOperationException("WORLD_CAREER_READ_HTTP_" + response.Status);
        if (string.IsNullOrWhiteSpace(response.Body) || response.Body == "null")
            return progress;
        JObject claims = JObject.Parse(response.Body);
        foreach (JProperty entry in claims.Properties().OrderBy(x => x.Name))
        {
            string id = entry.Name;
            JObject claim = entry.Value as JObject;
            if (claim == null || id.Length != 32 ||
                WString(claim, "uid") != identity.Uid ||
                WString(claim, "matchId") != id ||
                !string.Equals(id, id.ToLowerInvariant(), StringComparison.Ordinal) ||
                WorldHas(progress.proofHistory, id)) continue;
            long at = (long?)claim["atMs"] ?? 0;
            if (at < WorldStart || at >= WorldEnd || at > Now()) continue;
            string map = WString(claim, "mapId");
            if (map != "Turkey" && map != "USA" && map != "Colorado" &&
                map != "Germany" && map != "Mexico" && map != "World") continue;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (identity.Uid != CurrentUid)
                    throw new InvalidOperationException("WORLD_ACCOUNT_CHANGED");
                Response read = await RequestAsync(identity, "GET",
                    WorldPath(identity.Uid), null, null, true);
                if (read.Status != 200)
                    throw new InvalidOperationException("WORLD_PROOF_ETAG_HTTP_" + read.Status);
                WorldProof next = read.Body == "null" || string.IsNullOrWhiteSpace(read.Body)
                    ? new WorldProof() : JsonUtility.FromJson<WorldProof>(read.Body);
                if (WorldHas(next.proofHistory, id)) { progress = next; break; }
                if ((next.proofHistory ?? "").Length + 34 > 200000) break;
                next.proofHistory = (next.proofHistory ?? "") + WorldMarker(id);
                next.matches += 1;
                if (WString(claim, "result") == "win") next.wins++;
                next.diceRolls += WInt(claim, "diceRolls");
                next.developments += WInt(claim, "developments");
                next.turns += WInt(claim, "turns");
                if (!WorldHas(next.maps, map))
                {
                    next.maps = (next.maps ?? "") + WorldMarker(map);
                    next.mapCount++;
                }
                next.highestNetWorth = Math.Max(next.highestNetWorth,
                    WInt(claim, "netWorth"));
                next.lastMatchId = id;
                next.atMs = Now();
                Response save = await RequestAsync(identity, "PUT",
                    WorldPath(identity.Uid), JsonUtility.ToJson(next),
                    read.ETag ?? "null_etag");
                if (save.Status == 412) continue;
                if (save.Status == 401 || save.Status == 403)
                    throw new InvalidOperationException("WORLD_PROOF_RULE_DENIED_" + id);
                if (save.Status < 200 || save.Status >= 300)
                    throw new InvalidOperationException("WORLD_PROOF_WRITE_HTTP_" + save.Status);
                progress = next;
                break;
            }
        }
        return await WorldProgressAsync(identity);
    }

    private static int WorldProgressValue(WorldRewardSpec item, WorldProof proof,
        string claimed)
    {
        switch (item.Metric)
        {
            case "matches": return proof.matches;
            case "diceRolls": return proof.diceRolls;
            case "developments": return proof.developments;
            case "wins": return proof.wins;
            case "turns": return proof.turns;
            case "mapCount": return proof.mapCount;
            case "highestNetWorth": return proof.highestNetWorth;
            case "sevenClaims": return WorldRewards.Take(7)
                .Count(m => WorldHas(claimed, m.Id));
            case "eightClaims": return WorldRewards.Take(8)
                .Count(m => WorldHas(claimed, m.Id));
            default: return 0;
        }
    }

    public static async Task<AtlasBoardSeasonalOverview> ReadWorldJourneyOverviewAsync()
    {
        var view = new AtlasBoardSeasonalOverview
        {
            IsWorldJourney = true,
            Event = new AtlasBoardSeasonalEvent
            {
                EventId = WorldEventId, TitleKey = "world.event.title",
                SubtitleKey = "world.event.subtitle",
                StartsAtEpochMs = WorldStart, EndsAtEpochMs = WorldEnd - 1,
                Active = WorldRunning
            },
            WorldBonus = new AtlasBoardWorldBonus {
                RewardType = "inventory_item", RewardItemId = "board_atlas_horizon" }
        };
        try
        {
            if (WorldEmulatorSelected)
                throw new InvalidOperationException("WORLD_EMULATOR_ENDPOINT_NOT_CONFIGURED");
            Identity identity = await ResolveIdentityAsync();
            if (identity == null) throw new InvalidOperationException("WORLD_AUTH_REQUIRED");
            if (!identity.IsRestGuest &&
                !await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync("", false))
                throw new InvalidOperationException("WORLD_PROFILE_NOT_READY");
            WorldProof proof = await FoldCareerProofsAsync(identity);
            ReadState wallet = await ReadAsync(identity);
            if (wallet.Document == null)
            {
                await InitializeAsync(identity, wallet.ETag);
                wallet = await ReadAsync(identity);
            }
            if (wallet.Document == null) throw new InvalidOperationException("WORLD_WALLET_MISSING");
            string claimed = wallet.Document.worldClaims ?? "";
            foreach (WorldRewardSpec reward in WorldRewards.Take(8))
            {
                int current = Math.Min(reward.Target,
                    WorldProgressValue(reward, proof, claimed));
                bool done = WorldHas(claimed, reward.Id);
                view.WorldMissions.Add(new AtlasBoardWorldMission
                {
                    MissionId = reward.Id, TitleKey = "world.mission." + reward.Id,
                    DescriptionKey = "world.mission." + reward.Id + ".body",
                    Target = reward.Target, Progress = current,
                    RewardType = reward.Type, RewardAmount = reward.Amount,
                    RewardItemId = reward.Item, Claimed = done,
                    Claimable = WorldRunning && !done && current >= reward.Target
                });
            }
            view.CompletedMissions = view.WorldMissions.Count(x => x.Progress >= x.Target);
            view.ClaimedRewards = view.WorldMissions.Count(x => x.Claimed);
            view.WorldBonus.Claimed = WorldHas(claimed, "completion_bonus");
            view.WorldBonus.Claimable = WorldRunning && !view.WorldBonus.Claimed &&
                view.ClaimedRewards == 8;
            Response history = await RequestAsync(identity, "GET",
                WorldHistoryPath(identity.Uid), null, null);
            if (history.Status != 200)
                throw new InvalidOperationException("WORLD_HISTORY_READ_HTTP_" + history.Status);
            if (!string.IsNullOrWhiteSpace(history.Body) && history.Body != "null")
            {
                JObject receipts = JObject.Parse(history.Body);
                foreach (JProperty receipt in receipts.Properties())
                {
                    JObject info = receipt.Value as JObject;
                    string receiptRewardId = WString(info, "rewardId");
                    if (info == null || !WorldHas(claimed, receiptRewardId)) continue;
                    view.WorldHistory.Add(new AtlasBoardWorldRewardHistory
                    {
                        EventId = WorldEventId, EventName = "Atlas World Journey",
                        RewardId = receiptRewardId, RewardType = WString(info, "rewardType"),
                        RewardItemId = WString(info, "rewardItemId"),
                        RewardAmount = WInt(info, "rewardAmount"),
                        TransactionId = WString(info, "transactionId"),
                        Status = WString(info, "status"), Source = WString(info, "source"),
                        ClaimedAt = (long?)info["claimedAt"] ?? 0
                    });
                }
                view.WorldHistory.Sort((a,b) => b.ClaimedAt.CompareTo(a.ClaimedAt));
            }
            // If a receipt was interrupted after the atomic wallet grant,
            // show the authoritative claimed state; do not grant again.
            foreach (WorldRewardSpec reward in WorldRewards)
                if (WorldHas(claimed, reward.Id) &&
                    view.WorldHistory.All(h => h.RewardId != reward.Id))
                    view.WorldHistory.Add(new AtlasBoardWorldRewardHistory {
                        EventId = WorldEventId, EventName = "Atlas World Journey",
                        RewardId = reward.Id, RewardType = reward.Type,
                        RewardAmount = reward.Amount, RewardItemId = reward.Item,
                        Status = "succeeded", Source = "world_journey"
                    });
            view.Success = true;
            return view;
        }
        catch (Exception error)
        {
            view.Success = false;
            view.ErrorKey = "seasonal.error.unavailable";
            view.TechnicalMessage = error.Message;
            Debug.LogWarning("ATB_WORLD_RTDB overview=" + error.Message);
            return view;
        }
    }

    public static async Task<AtlasBoardMetaEconomyOperationResult> ClaimWorldJourneyAsync(string rewardId)
    {
        WorldRewardSpec reward = WorldRewards.FirstOrDefault(x => x.Id == rewardId);
        if (reward == null) return Failure("seasonal.error.reward_not_found");
        if (!WorldRunning) return Failure("seasonal.error.unavailable", "EVENT_NOT_ACTIVE");
        if (WorldEmulatorSelected) return Failure("seasonal.error.unavailable",
            "WORLD_EMULATOR_ENDPOINT_NOT_CONFIGURED");
        try
        {
            Identity identity = await ResolveIdentityAsync();
            if (identity == null) return Failure("account.error.authentication_required");
            // Refresh server-checkable career proof before attempting an award.
            await FoldCareerProofsAsync(identity);
            for (int attempt = 0; attempt < 7; attempt++)
            {
                if (identity.Uid != CurrentUid)
                    return Failure("account.error.authentication_required");
                WorldProof progress = await WorldProgressAsync(identity);
                ReadState state = await ReadAsync(identity);
                if (state.Document == null)
                {
                    await InitializeAsync(identity, state.ETag);
                    continue;
                }
                Economy doc = state.Document;
                string claimed = doc.worldClaims ?? "";
                if (WorldHas(claimed, rewardId))
                    return Failure("seasonal.error.already_claimed", "WORLD_ALREADY_CLAIMED");
                if (WorldProgressValue(reward, progress, claimed) < reward.Target)
                    return Failure("seasonal.error.reward_locked", "WORLD_REQUIREMENT_NOT_MET");
                // Preserve all other wallet fields as-is; a reward is a single
                // server-validated ETag transition (never two separate grants).
                JObject raw = (JObject)state.Raw.DeepClone();
                if (raw["promoXp"] == null) raw["promoXp"] = doc.promoXp;
                int compensationGold = 0;
                if (reward.Type == "gold")
                {
                    int amount = doc.wallet.gold + reward.Amount;
                    if (amount > 10000000) return Failure("meta.error.service_unavailable");
                    raw["wallet"]["gold"] = amount;
                }
                else if (reward.Type == "atlas_coin")
                {
                    int amount = doc.wallet.atlasCoin + reward.Amount;
                    if (amount > 1000000) return Failure("meta.error.service_unavailable");
                    raw["wallet"]["atlasCoin"] = amount;
                }
                else if (reward.Type == "xp")
                {
                    int amount = doc.promoXp + reward.Amount;
                    if (amount > 100000000) return Failure("meta.error.service_unavailable");
                    raw["promoXp"] = amount;
                }
                else
                {
                    string category = reward.Item == "map_world" ? "maps" : "boards";
                    JObject inventory = raw["inventory"] as JObject;
                    JObject owned = inventory?[category] as JObject;
                    if (owned == null) return Failure("seasonal.error.unavailable", "WORLD_INVENTORY_MISSING");
                    if ((bool?)owned[reward.Item] == true)
                    {
                        compensationGold = AtlasBoardDuplicateRewardGold.ForItemId(reward.Item);
                        if (compensationGold <= 0)
                            return Failure("seasonal.error.unavailable", "WORLD_REWARD_BAD_COMPENSATION");
                        if (doc.wallet.gold > 10000000 - compensationGold)
                            return Failure("meta.error.service_unavailable", "WORLD_GOLD_CAP");
                        raw["wallet"]["gold"] = doc.wallet.gold + compensationGold;
                    }
                    else owned[reward.Item] = true;
                }
                raw["worldClaims"] = claimed + WorldMarker(rewardId);
                long now = Now();
                if (doc.operation != null && doc.operation.atMs >= now)
                    now = doc.operation.atMs + 1;
                string transactionId = Guid.NewGuid().ToString("N");
                raw["operation"] = JObject.FromObject(new {
                    id = transactionId, type = "EVENT_CLAIM", itemId = rewardId, atMs = now
                });
                Response saved = await RequestAsync(identity, "PUT",
                    EconomyPath(identity.Uid),
                    raw.ToString(Newtonsoft.Json.Formatting.None), state.ETag);
                if (saved.Status == 412) continue;
                if (saved.Status == 401 || saved.Status == 403)
                    return Failure("seasonal.error.unavailable", "WORLD_RULE_DENIED_" + saved.Status);
                if (saved.Status < 200 || saved.Status >= 300)
                    return Failure("seasonal.error.unavailable", "WORLD_HTTP_" + saved.Status);
                // Receipt is append-only, display-only; award was already
                // atomic and is not rolled back if the receipt request fails.
                await WorldReceiptBestEffortAsync(identity, reward, transactionId,
                    progress.qaMode, now, compensationGold);
                var result = new AtlasBoardMetaEconomyOperationResult
                {
                    Success = true, Type = "WORLD_JOURNEY_CLAIM",
                    ReferenceId = rewardId, TransactionId = transactionId,
                    Detail = compensationGold > 0 ? "duplicate_item_gold:" +
                        compensationGold.ToString(CultureInfo.InvariantCulture) :
                        reward.Type + (reward.Item != "" ? ":" + reward.Item :
                        ":" + reward.Amount.ToString(CultureInfo.InvariantCulture))
                };
                result.Rewards.Add(new AtlasBoardMetaRewardLine {
                    Type = compensationGold > 0 ? "gold" : reward.Type,
                    CurrencyId = compensationGold > 0 ? "gold" : reward.Type,
                    ItemId = compensationGold > 0 ? "" : reward.Item,
                    Amount = compensationGold > 0 ? compensationGold : reward.Amount
                });
                return result;
            }
            return Failure("seasonal.error.unavailable", "WORLD_ETAG_RETRIES_EXHAUSTED");
        }
        catch (Exception error)
        {
            Debug.LogWarning("ATB_WORLD_RTDB claim=" + error.Message);
            return Failure("seasonal.error.unavailable", error.Message);
        }
    }

    private static async Task WorldReceiptBestEffortAsync(Identity identity,
        WorldRewardSpec spec, string transactionId, bool qa, long claimedAt,
        int compensationGold)
    {
        try
        {
            var data = new JObject
            {
                ["eventId"] = WorldEventId, ["rewardId"] = spec.Id,
                ["rewardType"] = compensationGold > 0 ? "gold" : spec.Type,
                ["rewardAmount"] = compensationGold > 0 ? compensationGold : spec.Amount,
                ["rewardItemId"] = compensationGold > 0 ? "" : spec.Item,
                ["transactionId"] = transactionId,
                ["claimedAt"] = claimedAt, ["status"] = "succeeded",
                ["source"] = qa ? "world_journey_qa" : "world_journey"
            };
            Response receipt = await RequestAsync(identity, "PUT",
                "/users/" + UnityWebRequest.EscapeURL(identity.Uid) +
                "/worldJourneyHistory/" + spec.Id + "_" + transactionId + ".json",
                data.ToString(Newtonsoft.Json.Formatting.None), "null_etag");
            if (receipt.Status != 200)
                Debug.LogWarning("ATB_WORLD_RTDB receipt_http=" + receipt.Status);
        }
        catch (Exception e) { Debug.LogWarning("ATB_WORLD_RTDB receipt=" + e.Message); }
    }

#if UNITY_EDITOR
    // Test-only: this changes progress, NEVER a reward. Server rules require
    // devWalletTesters/{uid}==true. No method is built in release players.
    public static async Task<string> WorldEditorSetProgressAsync(bool complete)
    {
        if (!Application.isPlaying) return "Enter Unity Play Mode first.";
        if (WorldEmulatorSelected) return "Switch off Local Emulator mode; QA here uses authenticated live Spark RTDB.";
        if (!WorldRunning) return "World Journey is not active.";
        Identity identity = await ResolveIdentityAsync();
        if (identity == null) return "Sign in with a test account.";
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (identity.Uid != CurrentUid) return "Account switched.";
            Response read = await RequestAsync(identity, "GET",
                WorldPath(identity.Uid), null, null, true);
            if (read.Status != 200) return "Read failed HTTP " + read.Status;
            WorldProof value = read.Body == "null" || string.IsNullOrWhiteSpace(read.Body)
                ? new WorldProof() : JsonUtility.FromJson<WorldProof>(read.Body);
            value.matches = complete ? 3 : 0;
            value.wins = complete ? 3 : 0;
            value.diceRolls = complete ? 50 : 0;
            value.developments = complete ? 10 : 0;
            value.turns = complete ? 20 : 0;
            value.highestNetWorth = complete ? 5000 : 0;
            value.maps = complete ? "|Turkey||USA||Colorado|" : "";
            value.mapCount = complete ? 3 : 0;
            value.lastMatchId = "editor_qa";
            value.atMs = Now();
            value.qaMode = true;
            Response write = await RequestAsync(identity, "PUT",
                WorldPath(identity.Uid), JsonUtility.ToJson(value),
                read.ETag ?? "null_etag");
            if (write.Status == 412) continue;
            if (write.Status == 200)
            {
                WorldQaRevision++;
                return complete ? "7/7 missions ready; Store > Event refreshes automatically." :
                    "QA mission counters cleared. Reward claims unchanged.";
            }
            return "QA denied HTTP " + write.Status +
                " (requires devWalletTesters/<current UID> = true).";
        }
        return "Concurrent update; retry.";
    }

    // Allows a whitelisted developer test account to reclaim the SAME event
    // rewards without deleting Gold, XP, entitlement or receipt history.
    // Never compiled into player builds; RTDB rules independently gate the
    // exact worldClaims child write to allowlisted UID + active QA proof.
    public static async Task<string> WorldEditorResetClaimsAsync()
    {
        if (!Application.isPlaying) return "Enter Unity Play Mode first.";
        if (WorldEmulatorSelected) return "Local emulator path is not configured.";
        if (!WorldRunning) return "World Journey is not active.";
        Identity identity = await ResolveIdentityAsync();
        if (identity == null) return "Sign in with your QA test account.";
        Response allow = await RequestAsync(identity, "GET",
            "/devWalletTesters/" + UnityWebRequest.EscapeURL(identity.Uid) + ".json",
            null, null);
        if (allow.Status != 200 || allow.Body.Trim() != "true")
            return "QA denied: UID not on devWalletTesters allowlist.";
        WorldProof progress = await WorldProgressAsync(identity);
        if (!progress.qaMode) return "Use Complete All Missions first to enter QA mode.";
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (identity.Uid != CurrentUid) return "Account switched.";
            Response read = await RequestAsync(identity, "GET",
                "/users/" + UnityWebRequest.EscapeURL(identity.Uid) +
                "/metaEconomy/worldClaims.json", null, null, true);
            if (read.Status != 200)
                return "Claims read failed HTTP " + read.Status;
            // The backend permits clearing this child only, not any wallet
            // field. ETag protects concurrent claims on the same UID.
            Response write = await RequestAsync(identity, "PUT",
                "/users/" + UnityWebRequest.EscapeURL(identity.Uid) +
                "/metaEconomy/worldClaims.json", "\"\"", read.ETag);
            if (write.Status == 412) continue;
            if (write.Status != 200)
                return "Claim reset denied HTTP " + write.Status;
            WorldQaRevision++;
            return "QA claims reset. Mission progress and earned inventory/Gold/XP preserved. " +
                "Reclaim in Store > Event > Rewards to test owned-item Gold fallback.";
        }
        return "Concurrent claim update; retry.";
    }
#endif
}
