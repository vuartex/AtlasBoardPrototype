using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Firebase RTDB/Spark-only weekly missions. CareerClaims already require a
/// witnessed final-match proof. All four proofs are checked AGAIN by RTDB Rules
/// at reward claim; this class never writes earned progress, Gold or XP.
/// One global operator-published week schedule determines token weeks.
/// </summary>
[DefaultExecutionOrder(31000)]
public sealed class AtlasBoardWeeklyMissions : MonoBehaviour
{
    private const string Base = "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    private const int MaxClaimsPerWeek = 256;
    public static AtlasBoardWeeklyMissions Instance { get; private set; }
    public event Action Changed;

    [Serializable] public sealed class Mission { public string type; public int target; }
    [Serializable] public sealed class Variant { public Mission s0, s1, s2, s3; }
    [Serializable] public sealed class Variants { public Variant a, b, c, d; }
    [Serializable] public sealed class Reward { public string kind; public int amount; }
    [Serializable] public sealed class RewardCatalog { public Reward s0, s1, s2, s3;
        public Reward[] AsArray() => new[] { s0, s1, s2, s3 }; }
    [Serializable] private sealed class StringWrapper { public string value; }
    [Serializable] private sealed class MissionRewardReceipt
    {
        public string weekId, slot, matchId, kind;
        public int amount;
        public long claimedAt;
    }
    [Serializable] public sealed class WeekCatalog
    {
        public long startUtcMs, endUtcMs;
        public bool hasTokenReward;
        public string tokenId;
        public Variants variants;
    }
    [Serializable] private sealed class Assignment
    {
        public string weekId, variant;
        public long startUtcMs, endUtcMs, assignedAt;
    }
    [Serializable] private sealed class Claim
    {
        public string uid, matchId, result, mapId;
        public long atMs;
        public int playerCount, diceRolls, doublesRolled, rounds, turns, properties,
            developments, cash, netWorth;
    }
    [Serializable] private sealed class RewardRecord
    {
        public string tokenId, weekId;
        public long claimedAt;
    }
    [Serializable] private sealed class OwnershipRecord
    {
        public string tokenId, weekId;
        public long acquiredAt;
    }

    public sealed class View
    {
        public string uid, weekId, variant, tokenId, message;
        public Mission[] missions = new Mission[4];
        public string[] proofs = new string[4];
        public bool[] complete = new bool[4];
        public bool[] paid = new bool[4];
        public Reward[] rewards = new Reward[4];
        public bool scheduleMissing;
        public int PayableCount {
            get { int n=0; for(int i=0;i<4;i++) if(complete[i] && !paid[i]) n++; return n; }
        }
        public bool CanCollect => loaded && !busy && PayableCount > 0;
        public int PaidCount {
            get { int n=0; for(int i=0;i<4;i++) if(paid[i]) n++; return n; }
        }
        public bool loaded, hasTokenReward, claimed, tokenOwned, busy;
        public long startUtcMs, endUtcMs;
        public int FinishedCount
        {
            get { int n = 0; for (int i = 0; i < 4; i++) if (complete[i]) n++; return n; }
        }
        public bool AllCompleted => loaded && FinishedCount == 4;
        public bool CanClaim => loaded && hasTokenReward && AllCompleted && !claimed &&
            !busy && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < endUtcMs;
    }

    public View Current { get; private set; } = new View { message = "Not loaded" };
    private bool inFlight;
    private string observedUid = "";
    private long nextRefreshEpochMs;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("AtlasBoard_WeeklyMissions_RTDB");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AtlasBoardWeeklyMissions>();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
    private void Update()
    {
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid ?? "";
        if (uid == observedUid)
        {
            // A profile left open across Monday must drop the previous week
            // immediately; its immutable history stays in RTDB.
            if (Current.loaded && Current.weekId != WeekId(DateTimeOffset.UtcNow))
            {
                Current = new View { uid = uid, message = "New UTC week: refreshing assignments" };
                nextRefreshEpochMs = 0;
                Changed?.Invoke();
            }
            return;
        }
        observedUid = uid;
        nextRefreshEpochMs = 0;
        Current = new View { uid = uid, message = "Waiting for verified session" };
        Changed?.Invoke();
    }

    public static string WeekId(DateTimeOffset utc)
    {
        DateTime date = utc.UtcDateTime.Date;
        int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    }
    public static string VariantForUid(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return "d";
        char c = uid[0];
        if ((c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f') || (c >= '0' && c <= '3')) return "a";
        if ((c >= 'G' && c <= 'M') || (c >= 'g' && c <= 'm') || (c >= '4' && c <= '6')) return "b";
        if ((c >= 'N' && c <= 'T') || (c >= 'n' && c <= 't') || (c >= '7' && c <= '8')) return "c";
        return "d";
    }
    public static Mission[] GetMissions(WeekCatalog catalog, string variant)
    {
        if (catalog == null || catalog.variants == null) return null;
        Variant x = variant == "a" ? catalog.variants.a :
            variant == "b" ? catalog.variants.b :
            variant == "c" ? catalog.variants.c : catalog.variants.d;
        if (x == null || x.s0 == null || x.s1 == null || x.s2 == null || x.s3 == null) return null;
        return new[] { x.s0, x.s1, x.s2, x.s3 };
    }
    private static bool Qualifies(Claim c, Mission m, long start, long end)
    {
        if (c == null || m == null || string.IsNullOrEmpty(c.matchId) ||
            c.playerCount < 2 || c.atMs < start || c.atMs >= end) return false;
        switch (m.type)
        {
            case "match": return true;
            case "win": return c.result == "win";
            case "winOrTie": return c.result == "win" || c.result == "tie";
            case "dice": return c.diceRolls >= m.target;
            case "doubles": return c.doublesRolled >= m.target;
            case "rounds": return c.rounds >= m.target;
            case "turns": return c.turns >= m.target;
            case "properties": return c.properties >= m.target;
            case "developments": return c.developments >= m.target;
            case "cash": return c.cash >= m.target;
            case "networth": return c.netWorth >= m.target;
            default: return false;
        }
    }

    private static string Localize(string language, string en, string tr,
        string es, string fr, string de, string ko, string ru)
    {
        switch (language)
        {
            case "tr": return tr;
            case "es": return es;
            case "fr": return fr;
            case "de": return de;
            case "ko": return ko;
            case "ru": return ru;
            default: return en;
        }
    }
    public static string MissionLabel(Mission m, string language)
    {
        if (m == null) return Localize(language,"Mission pending","Görev bekleniyor",
            "Misión pendiente","Mission en attente","Aufgabe ausstehend","임무 대기","Задание ожидается");
        string k = m.target.ToString(CultureInfo.InvariantCulture);
        switch (m.type)
        {
            case "rounds": return Localize(language,
                "Complete an online match with " + k + "+ rounds",
                k + "+ turluk online maç tamamla", "Completa partida de " + k + "+ rondas",
                "Terminez une partie de " + k + "+ tours",
                "Beende ein Spiel mit " + k + "+ Runden",
                k + "라운드 이상 온라인 경기 완료", "Заверши матч из " + k + "+ раундов");
            case "win": return Localize(language,
                "Win a verified online match", "Doğrulanmış online maç kazan",
                "Gana una partida verificada", "Gagnez une partie vérifiée",
                "Gewinne ein bestätigtes Online-Spiel", "검증된 온라인 경기 승리",
                "Победи в подтверждённом матче");
            case "winOrTie": return Localize(language,
                "Win or tie a verified online match", "Online maçı kazan veya berabere bitir",
                "Gana o empata una partida", "Gagnez ou faites match nul",
                "Gewinne oder spiele unentschieden", "온라인 경기 승리 또는 무승부",
                "Победи или сыграй вничью");
            case "dice": return Localize(language,
                "Roll dice " + k + "+ times in one online match", "Bir maçta " + k + "+ kez zar at",
                "Lanza dados " + k + "+ veces", "Lancez les dés " + k + "+ fois",
                "Würfle " + k + "+ Mal in einem Spiel", "한 경기에서 주사위 " + k + "회 이상",
                "Брось кубики " + k + "+ раз за матч");
            case "properties": return Localize(language,
                "Finish one online match with " + k + "+ properties",
                "Bir online maçı " + k + "+ mülkle bitir", "Termina con " + k + "+ propiedades",
                "Finissez avec " + k + "+ propriétés", "Beende mit " + k + "+ Grundstücken",
                "한 경기에서 부동산 " + k + "개 이상 보유", "Заверши матч с " + k + "+ объектами");
            default: return Localize(language,"Unknown mission","Tanımsız görev",
                "Misión desconocida","Mission inconnue","Unbekannte Aufgabe",
                "알 수 없는 임무","Неизвестное задание");
        }
    }

    private static string Url(string path, string token)
    {
        return Base + "/" + path + ".json?auth=" + UnityWebRequest.EscapeURL(token);
    }
    private static async Task<string> TokenAsync(string uid)
    {
        string token = await AtlasBoardRtdbMetaEconomy.GetCurrentCallableTokenAsync();
        if (string.IsNullOrEmpty(token) && !AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
        {
            try
            {
                FirebaseUser p = FirebaseAuth.DefaultInstance.CurrentUser;
                if (p != null && p.UserId == uid) token = await p.TokenAsync(false);
            }
            catch { /* Auth startup not finished */ }
        }
        return token;
    }
    private static async Task<(long status, string json)> RequestAsync(
        string uid, string token, string path, string method, string body = null,
        bool createOnly = false)
    {
        string url = Url(path, token);
        using (var r = new UnityWebRequest(url, method))
        {
            r.downloadHandler = new DownloadHandlerBuffer();
            if (body != null)
            {
                r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                r.SetRequestHeader("Content-Type", "application/json");
            }
            if (createOnly) r.SetRequestHeader("if-match", "null_etag");
            r.timeout = 18;
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(r);
            var send = r.SendWebRequest();
            while (!send.isDone) await Task.Yield();
            // UID change is handled by the caller, never log tokens/response PII.
            return (r.responseCode, r.downloadHandler != null ? r.downloadHandler.text : "");
        }
    }
    private static bool Okay(long status) => status >= 200 && status < 300;
    private bool SameUid(string uid) =>
        uid == (AtlasBoardRtdbMetaEconomy.CurrentUid ?? "") && uid == observedUid;

    private static readonly Regex ClaimRegex = new Regex(
        "\\\"(?<id>[a-f0-9]{32})\\\"\\s*:\\s*(?<body>\\{[^{}]*\\})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static List<Claim> ParseClaims(string json, string uid, long weekStart, long weekEnd)
    {
        var result = new List<Claim>();
        if (string.IsNullOrEmpty(json) || json == "null") return result;
        foreach (Match match in ClaimRegex.Matches(json))
        {
            // Do not stop after arbitrary historical matches. Those keys are
            // lexicographically sorted GUIDs, not chronological order.
            try
            {
                Claim entry = JsonUtility.FromJson<Claim>(match.Groups["body"].Value);
                if (entry != null && entry.uid == uid &&
                    entry.matchId == match.Groups["id"].Value &&
                    entry.playerCount >= 2 && entry.atMs >= weekStart &&
                    entry.atMs < weekEnd && result.Count < MaxClaimsPerWeek)
                    result.Add(entry);
            }
            catch { /* Invalid cached proof is never accepted */ }
        }
        return result;
    }

    public async Task RefreshAsync(bool force = false)
    {
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid ?? "";
        if (string.IsNullOrEmpty(uid))
        {
            Current = new View { message = "Sign in to load missions" };
            Changed?.Invoke();
            return;
        }
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (inFlight || (!force && Current.loaded && Current.uid == uid && now < nextRefreshEpochMs)) return;
        inFlight = true;
        observedUid = uid;
        var model = new View { uid = uid, message = "Loading verified weekly missions" };
        Current = model;
        Changed?.Invoke();
        try
        {
            string token = await TokenAsync(uid);
            if (!SameUid(uid) || string.IsNullOrEmpty(token)) return;
            string weekId = WeekId(DateTimeOffset.UtcNow);
            model.weekId = weekId;
            var catalogResponse = await RequestAsync(uid, token, "weeklyMissionCatalog/" + weekId, "GET");
            if (!SameUid(uid)) return;
            if (!Okay(catalogResponse.status) || catalogResponse.json == "null")
            {
                model.scheduleMissing = true;
                model.message = "Yeni haftalık görev bulunamadı";
                return;
            }
            var rewardResponse = await RequestAsync(uid, token, "weeklyRewardCatalog/" + weekId, "GET");
            if (!SameUid(uid)) return;
            if (!Okay(rewardResponse.status) || rewardResponse.json == "null")
            {
                model.scheduleMissing = true;
                model.message = "Yeni haftalık görev bulunamadı";
                return;
            }
            RewardCatalog rewardWeek = null;
            try { rewardWeek = JsonUtility.FromJson<RewardCatalog>(rewardResponse.json); }
            catch { /* No fabricated rewards */ }
            Reward[] publishedRewards = rewardWeek != null ? rewardWeek.AsArray() : null;
            if (publishedRewards == null || publishedRewards.Length != 4)
            { model.message = "Invalid reward schedule"; return; }
            for (int i = 0; i < 4; i++)
            {
                Reward r = publishedRewards[i];
                if (r == null || r.kind != (i == 3 ? "gold" : "xp") ||
                    r.amount <= 0 || r.amount > 350)
                { model.message = "Invalid weekly reward definition"; return; }
            }
            model.rewards = publishedRewards;
            WeekCatalog cat = JsonUtility.FromJson<WeekCatalog>(catalogResponse.json);
            if (cat == null || cat.startUtcMs > now || cat.endUtcMs <= now ||
                cat.endUtcMs - cat.startUtcMs != 604800000L)
            {
                model.message = "Server week differs from device time; retry when online";
                return;
            }
            string variant = VariantForUid(uid);
            Mission[] missions = GetMissions(cat, variant);
            if (missions == null) { model.message = "Weekly mission manifest invalid"; return; }
            model.startUtcMs = cat.startUtcMs;
            model.endUtcMs = cat.endUtcMs;
            model.hasTokenReward = cat.hasTokenReward;
            model.tokenId = cat.tokenId ?? "";
            model.variant = variant;
            model.missions = missions;
            string assignmentPath = "users/" + uid + "/weeklyMissions/" + weekId + "/assignment";
            var assigned = await RequestAsync(uid, token, assignmentPath, "GET");
            if (!SameUid(uid)) return;
            if (Okay(assigned.status) && assigned.json == "null")
            {
                string payload = "{\"weekId\":\"" + weekId + "\",\"variant\":\"" + variant +
                    "\",\"startUtcMs\":" + cat.startUtcMs + ",\"endUtcMs\":" + cat.endUtcMs +
                    ",\"assignedAt\":{\".sv\":\"timestamp\"}}";
                // Only initial creation, never a reroll or reset. The Rules
                // independently derive the variant from Firebase UID.
                var created = await RequestAsync(uid, token, assignmentPath, "PUT", payload, true);
                if (!SameUid(uid)) return;
                if (!Okay(created.status) && created.status != 412)
                { model.message = "Could not secure weekly assignment"; return; }
                assigned = await RequestAsync(uid, token, assignmentPath, "GET");
            }
            if (!Okay(assigned.status) || assigned.json == "null")
            { model.message = "Verified assignment unavailable"; return; }
            Assignment a = JsonUtility.FromJson<Assignment>(assigned.json);
            if (a == null || a.weekId != weekId || a.variant != variant ||
                a.startUtcMs != cat.startUtcMs || a.endUtcMs != cat.endUtcMs)
            { model.message = "Assignment integrity mismatch"; return; }
            // Only on meaningful screen refresh/reconnect, not per frame.
            // Rules validate actual Firebase server timestamp (now).
            await RequestAsync(uid, token,
                "users/" + uid + "/weeklyMissions/" + weekId + "/lastSeenServerAt",
                "PUT", "{\".sv\":\"timestamp\"}");
            if (!SameUid(uid)) return;
            var claims = await RequestAsync(uid, token, "careerClaims/" + uid, "GET");
            if (!SameUid(uid) || !Okay(claims.status))
            { model.message = "Verified match history unavailable"; return; }
            List<Claim> matches = ParseClaims(claims.json, uid, cat.startUtcMs, cat.endUtcMs);
            for (int i = 0; i < 4; i++)
            {
                foreach (Claim item in matches)
                {
                    if (!Qualifies(item, missions[i], cat.startUtcMs, cat.endUtcMs)) continue;
                    model.complete[i] = true;
                    model.proofs[i] = item.matchId;
                    break;
                }
            }
            var receipt = await RequestAsync(uid, token,
                "users/" + uid + "/weeklyMissions/" + weekId + "/claim", "GET");
            if (!SameUid(uid)) return;
            if (Okay(receipt.status) && receipt.json != "null")
            {
                RewardRecord reward = JsonUtility.FromJson<RewardRecord>(receipt.json);
                model.claimed = reward != null && reward.tokenId == cat.tokenId;
            }
            // The canonical wallet ledger (not an uncredited receipt) is the
            // payout authority. A failed network transaction is retryable.
            var walletResponse = await RequestAsync(uid, token,
                "users/" + uid + "/metaEconomy/rewardHistory", "GET");
            if (!SameUid(uid)) return;
            if (!Okay(walletResponse.status))
            { model.message = "Wallet ledger unavailable"; return; }
            string ledger = "";
            try { ledger = JsonUtility.FromJson<StringWrapper>(
                "{\"value\":" + walletResponse.json + "}").value ?? ""; }
            catch { /* null and legacy empty receipt are valid */ }
            for (int i = 0; i < 4; i++)
                model.paid[i] = ledger.Contains("|" + weekId + "s" + i + "|");
            if (model.hasTokenReward && !string.IsNullOrEmpty(model.tokenId))
            {
                var owned = await RequestAsync(uid, token,
                    "users/" + uid + "/collectibleTokens/" + model.tokenId, "GET");
                if (!SameUid(uid)) return;
                if (Okay(owned.status) && owned.json != "null") model.tokenOwned = true;
            }
            model.loaded = true;
            model.message = model.PaidCount == 4 ? "All mission rewards collected" :
                model.PayableCount > 0 ? "Mission rewards available" :
                "Verified mission progress loaded";
            nextRefreshEpochMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 30000;
            if (model.claimed && !model.tokenOwned) _ = PublishOwnershipAsync(uid, token, model);
        }
        catch (Exception e)
        {
            if (SameUid(uid)) model.message = "Weekly network error: " + e.GetType().Name;
        }
        finally
        {
            inFlight = false;
            if (SameUid(uid)) Changed?.Invoke();
        }
    }

    private async Task PublishOwnershipAsync(string uid, string token, View model)
    {
        if (!SameUid(uid) || !model.claimed || !model.hasTokenReward ||
            string.IsNullOrEmpty(model.tokenId)) return;
        string path = "users/" + uid + "/collectibleTokens/" + model.tokenId;
        string payload = "{\"weekId\":\"" + model.weekId + "\",\"tokenId\":\"" + model.tokenId +
            "\",\"acquiredAt\":{\".sv\":\"timestamp\"},\"source\":\"weeklyMissions\"}";
        var r = await RequestAsync(uid, token, path, "PUT", payload, true);
        if (!SameUid(uid)) return;
        if (Okay(r.status) || r.status == 412)
        {
            var check = await RequestAsync(uid, token, path, "GET");
            if (SameUid(uid) && Okay(check.status) && check.json != "null")
            {
                try
                {
                    OwnershipRecord owned = JsonUtility.FromJson<OwnershipRecord>(check.json);
                    model.tokenOwned = owned != null && owned.weekId == model.weekId && owned.tokenId == model.tokenId;
                }
                catch { /* do not assert ownership */ }
                Changed?.Invoke();
            }
        }
    }

    public async Task ClaimAsync()
    {
        View v = Current;
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid ?? "";
        if (v.uid != uid || !v.CanClaim) return;
        v.busy = true;
        v.message = "Verifying all four signed match proofs";
        Changed?.Invoke();
        try
        {
            string token = await TokenAsync(uid);
            if (!SameUid(uid) || string.IsNullOrEmpty(token)) return;
            // The server independently checks every match ID, each mission
            // definition, the one-per-UID claim and the UTC-week deadline.
            string payload = "{\"proofs\":{\"s0\":\"" + v.proofs[0] +
                "\",\"s1\":\"" + v.proofs[1] + "\",\"s2\":\"" + v.proofs[2] +
                "\",\"s3\":\"" + v.proofs[3] + "\"},\"tokenId\":\"" + v.tokenId +
                "\",\"claimedAt\":{\".sv\":\"timestamp\"}}";
            string path = "users/" + uid + "/weeklyMissions/" + v.weekId + "/claim";
            var result = await RequestAsync(uid, token, path, "PUT", payload, true);
            if (!SameUid(uid) || Current != v) return;
            if (!Okay(result.status) && result.status != 412)
            {
                v.message = "Claim not accepted by Firebase security rules";
                return;
            }
            var verified = await RequestAsync(uid, token, path, "GET");
            if (!SameUid(uid) || Current != v || !Okay(verified.status)) return;
            RewardRecord receipt = null;
            try { receipt = JsonUtility.FromJson<RewardRecord>(verified.json); }
            catch { /* fail closed */ }
            // The claim record is deliberately NOT a client-provided UID, and
            // contains only four signed proofs, token ID and server timestamp.
            if (receipt != null && receipt.tokenId == v.tokenId)
            {
                v.claimed = true;
                v.message = "Claim accepted. Synchronizing permanent token ownership";
                await PublishOwnershipAsync(uid, token, v);
                if (v.tokenOwned) v.message = "Collectible token acquired";
            }
        }
        catch (Exception e) { if (SameUid(uid)) v.message = "Claim error: " + e.GetType().Name; }
        finally
        {
            if (SameUid(uid) && Current == v) { v.busy = false; Changed?.Invoke(); }
        }
    }

    /// <summary>
    /// One player gesture collects all eligible mission payouts. The server
    /// verifies each signed Career proof; canonical ETag wallet ledger pays at
    /// most once per UID/week/slot. Receipt-only is never displayed as paid.
    /// </summary>
    public async Task CollectRewardsAsync()
    {
        View v = Current;
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid ?? "";
        if (v == null || v.uid != uid || !v.CanCollect) return;
        v.busy = true;
        v.message = "Verifying mission rewards";
        Changed?.Invoke();
        try
        {
            string token = await TokenAsync(uid);
            if (string.IsNullOrEmpty(token) || !SameUid(uid) || Current != v) return;
            int paidNow = 0;
            for (int i = 0; i < 4; ++i)
            {
                if (!SameUid(uid) || Current != v) return;
                if (!v.complete[i] || v.paid[i] || string.IsNullOrEmpty(v.proofs[i])) continue;
                Reward reward = v.rewards[i];
                string slot = "s" + i;
                string rid = v.weekId + slot;
                string receiptPath = "users/" + uid + "/weeklyRewardReceipts/" + rid;
                string payload = "{\"weekId\":\"" + v.weekId + "\",\"slot\":\"" + slot +
                    "\",\"matchId\":\"" + v.proofs[i] + "\",\"kind\":\"" + reward.kind +
                    "\",\"amount\":" + reward.amount +
                    ",\"claimedAt\":{\".sv\":\"timestamp\"}}";
                var created = await RequestAsync(uid, token, receiptPath, "PUT", payload, true);
                if (!SameUid(uid) || Current != v) return;
                if (!Okay(created.status) && created.status != 412)
                {
                    v.message = "Reward proof rejected (HTTP " + created.status + ")";
                    continue;
                }
                var check = await RequestAsync(uid, token, receiptPath, "GET");
                if (!Okay(check.status) || check.json == "null" || !SameUid(uid)) continue;
                MissionRewardReceipt receipt = null;
                try { receipt = JsonUtility.FromJson<MissionRewardReceipt>(check.json); }
                catch { /* fail closed */ }
                if (receipt == null || receipt.weekId != v.weekId || receipt.slot != slot ||
                    receipt.kind != reward.kind || receipt.amount != reward.amount) continue;
                bool credited = await AtlasBoardWeeklyRewardWallet.CreditAsync(
                    uid, token, rid, reward.kind, reward.amount);
                if (!SameUid(uid) || Current != v) return;
                if (!credited) { v.message = "Wallet sync pending; retry rewards"; continue; }
                v.paid[i] = true;
                paidNow++;
                Changed?.Invoke();
            }
            if (!SameUid(uid) || Current != v) return;
            if (paidNow > 0)
                AtlasBoardUnifiedProfileScreen.RefreshCareerAfterWeeklyPayout();
            v.message = v.PayableCount == 0 ? "Available rewards collected" :
                "Pending wallet synchronization; retry later";
        }
        catch (Exception e)
        {
            if (SameUid(uid) && Current == v) v.message = "Rewards unavailable: " + e.GetType().Name;
        }
        finally
        {
            if (SameUid(uid) && Current == v)
            {
                v.busy = false;
                Changed?.Invoke();
            }
        }
        // Bonus collectible is optional and never replaces XP/Gold; claim it
        // only on token weeks after ALL 4 verified objectives are complete.
        if (SameUid(uid) && Current == v && v.hasTokenReward &&
            v.AllCompleted && v.PaidCount == 4 && v.CanClaim)
            await ClaimAsync();
    }

}
