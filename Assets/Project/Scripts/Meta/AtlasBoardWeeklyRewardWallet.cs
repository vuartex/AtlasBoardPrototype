using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Weekly reward settlement into the EXISTING canonical ETag wallet. No second
/// XP/Gold ledger, no client-awarded balance, no new Account/Store manager.
/// RTDB Rules independently enforce the signed proof, exact reward amount,
/// append-only idempotency marker and all untouched economy fields.
/// </summary>
public static class AtlasBoardWeeklyRewardWallet
{
    private const string Database = "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    private const int MaxAttempts = 6;

    private static bool SameUser(string uid) =>
        !string.IsNullOrEmpty(uid) && uid == AtlasBoardRtdbMetaEconomy.CurrentUid;

    private static string Url(string uid, string token) => Database + "/users/" +
        UnityWebRequest.EscapeURL(uid) + "/metaEconomy.json?auth=" +
        UnityWebRequest.EscapeURL(token);

    private static async Task<(long status, string body, string etag)> SendAsync(
        string uid, string token, string method, string json = null, string ifMatch = null)
    {
        using (var req = new UnityWebRequest(Url(uid, token), method))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            if (method == "GET") req.SetRequestHeader("X-Firebase-ETag", "true");
            if (ifMatch != null) req.SetRequestHeader("if-match", ifMatch);
            if (json != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.timeout = 18;
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(req);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            return (req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : "",
                req.GetResponseHeader("ETag") ?? "");
        }
    }

    public static async Task<bool> CreditAsync(string uid, string token,
        string rewardId, string kind, int amount)
    {
        if (!SameUser(uid) || string.IsNullOrEmpty(token) || rewardId == null ||
            !System.Text.RegularExpressions.Regex.IsMatch(rewardId, "^[0-9]{8}s[0-3]$") ||
            (kind != "xp" && kind != "gold") || amount < 1 || amount > 350)
            return false;
        string marker = "|" + rewardId + "|";
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (!SameUser(uid)) return false;
            var current = await SendAsync(uid, token, "GET");
            if (!SameUser(uid)) return false;
            if (current.status != 200 || string.IsNullOrEmpty(current.etag)) return false;
            if (string.IsNullOrWhiteSpace(current.body) || current.body.Trim() == "null")
            {
                // Only the existing Economy bootstrap is permitted to initialize
                // a fresh canonical wallet; a weekly feature never forges INIT.
                try { await AtlasBoardRtdbMetaEconomy.LoadAsync(); }
                catch { return false; }
                continue;
            }
            JObject wallet;
            try { wallet = JObject.Parse(current.body); }
            catch { return false; }
            string history = (string)wallet["rewardHistory"] ?? "";
            if (history.Contains(marker)) return true;
            if (history.Length + marker.Length > 1000000) return false;
            JObject balance = wallet["wallet"] as JObject;
            JObject operation = wallet["operation"] as JObject;
            if (balance == null || operation == null) return false;
            long oldOperationAt = (long?)operation["atMs"] ?? 0;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (now <= oldOperationAt) now = oldOperationAt + 1;
            int currentGold = (int?)balance["gold"] ?? 0;
            int currentXp = (int?)wallet["promoXp"] ?? 0;
            if ((kind == "gold" && currentGold > 10000000 - amount) ||
                (kind == "xp" && currentXp > 100000000 - amount)) return false;
            if (kind == "gold") balance["gold"] = currentGold + amount;
            else wallet["promoXp"] = currentXp + amount;
            if (wallet["promoXp"] == null) wallet["promoXp"] = currentXp;
            // Both existing legacy and newly created wallets must carry a
            // valid promoXp value in the canonical WEEKLY_REWARD transaction.
            // Legacy absent promoXp is not materialized by Gold rewards.
            wallet["rewardHistory"] = history + marker;
            wallet["operation"] = new JObject
            {
                ["id"] = Guid.NewGuid().ToString("N"),
                ["type"] = "WEEKLY_REWARD",
                ["itemId"] = rewardId,
                ["atMs"] = now
            };
            if (!SameUser(uid)) return false;
            var written = await SendAsync(uid, token, "PUT",
                wallet.ToString(Newtonsoft.Json.Formatting.None), current.etag);
            if (!SameUser(uid)) return false;
            if (written.status == 412) continue; // concurrent wallet write: re-read
            if (written.status < 200 || written.status >= 300)
            {
                Debug.LogWarning("ATB_WEEKLY_WALLET status=not_credited http=" + written.status);
                return false;
            }
            // Refresh the original wallet presentation. Career continues to
            // include bonus XP through its existing metaEconomy/promoXp reader.
            try { await AtlasBoardRtdbMetaEconomy.LoadAsync(); }
            catch { /* Wallet transaction already succeeded on server. */ }
            Debug.Log("ATB_WEEKLY_WALLET status=credited kind=" + kind +
                " amount=" + amount + " account_uid_scoped=true");
            return SameUser(uid);
        }
        return false;
    }
}
