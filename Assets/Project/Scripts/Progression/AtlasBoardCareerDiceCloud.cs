using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// UID-scoped, append-only, *observational* dice telemetry. Stored separately
/// from verified match results, gold and XP. Firebase rules demand a matching
/// career claim or completed-match participation; values are not reward proof.
/// Each match is written at most once (null_etag + !data.exists()).
/// </summary>
public static class AtlasBoardCareerDiceCloud
{
    private const string Database =
        "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    private const string BasePath = "/careerDiceObservations/";

    [Serializable]
    private sealed class LocalHistory
    {
        public List<AtlasBoardProgressionMatchSummary> history =
            new List<AtlasBoardProgressionMatchSummary>();
    }

    public sealed class Aggregate
    {
        public string Uid;
        public int RollCount;
        public long RollSum;
        public int HighestRoll;
        public int StoredMatches;
    }

    private sealed class Principal
    {
        public string Uid;
        public string Token;
        public bool RegisteredRest;
    }

    private sealed class Response
    {
        public long Status;
        public string Body;
    }

    public static async Task<Aggregate> SynchronizeAsync(
        List<AtlasBoardProgressionMatchSummary> recentMatches)
    {
        Principal principal = await ResolvePrincipalAsync();
        if (principal == null) return null;
        Aggregate result = new Aggregate { Uid = principal.Uid };
        try
        {
            string rootPath = BasePath + UnityWebRequest.EscapeURL(principal.Uid);
            Response remote = await SendAsync(principal, "GET", rootPath + ".json", null, null);
            if (remote.Status != 200) return null;
            JObject records = ParseMap(remote.Body);
            if (records == null) return null;

            HashSet<string> recent = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, AtlasBoardProgressionMatchSummary> serverMatches =
                new Dictionary<string, AtlasBoardProgressionMatchSummary>(StringComparer.Ordinal);
            if (recentMatches != null)
                foreach (AtlasBoardProgressionMatchSummary match in recentMatches)
                {
                    if (match == null || !ValidMatchId(match.matchId)) continue;
                    recent.Add(match.matchId);
                    if (!serverMatches.ContainsKey(match.matchId))
                        serverMatches.Add(match.matchId, match);
                }

            // The account's local complete-match observations are a source for
            // INITIAL telemetry. Never copy another UID's PlayerPrefs history.
            string prefKey = (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession
                ? "ATB.GuestCareer.Local.v1."
                : "ATB.SteamCareer.Observed.v1.") + principal.Uid;
            if (PlayerPrefs.HasKey(prefKey))
            {
                LocalHistory local = null;
                try { local = JsonUtility.FromJson<LocalHistory>(PlayerPrefs.GetString(prefKey)); }
                catch (Exception) { /* corrupt local cache must not block cloud */ }
                if (local != null && local.history != null)
                {
                    HashSet<string> attempted = new HashSet<string>(StringComparer.Ordinal);
                    int uploaded = 0;
                    foreach (AtlasBoardProgressionMatchSummary match in local.history)
                    {
                        if (match == null || !attempted.Add(match.matchId) ||
                            !ValidObservation(match) ||
                            records.Property(match.matchId) != null) continue;
                        // Some completed games predate the current server's
                        // short RecentMatches window. Those observations are
                        // still eligible if RTDB itself verifies participation
                        // and a finalized archive before accepting the insert.
                        AtlasBoardProgressionMatchSummary known;
                        if (serverMatches.TryGetValue(match.matchId, out known) &&
                            known.diceRolls != match.diceRolls) continue;

                        JObject payload = new JObject
                        {
                            ["uid"] = principal.Uid,
                            ["matchId"] = match.matchId,
                            ["diceRolls"] = match.diceRolls,
                            ["totalDiceValue"] = match.totalDiceValue,
                            ["highestRoll"] = match.highestRoll,
                            ["recordedAt"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                        };
                        Response write = await SendAsync(principal, "PUT",
                            rootPath + "/" + match.matchId + ".json",
                            payload.ToString(Newtonsoft.Json.Formatting.None), "null_etag");
                        if (write.Status >= 200 && write.Status < 300)
                            uploaded++;
                        else if (write.Status != 412)
                            Debug.Log("ATB_CAREER_DICE_CLOUD stage=observation_pending " +
                                "http=" + write.Status + " uid_suffix=" + Suffix(principal.Uid));
                    }
                    if (uploaded > 0)
                    {
                        remote = await SendAsync(principal, "GET", rootPath + ".json", null, null);
                        if (remote.Status == 200) records = ParseMap(remote.Body) ?? records;
                    }
                }
            }

            // Sum all the owner's immutable, per-match observations, including
            // other devices. Never aggregate any other account's records.
            foreach (JProperty property in records.Properties())
            {
                JObject entry = property.Value as JObject;
                if (entry == null || !ValidMatchId(property.Name) ||
                    (string)entry["uid"] != principal.Uid ||
                    (string)entry["matchId"] != property.Name) continue;
                int n = (int?)entry["diceRolls"] ?? 0;
                int sum = (int?)entry["totalDiceValue"] ?? 0;
                int high = (int?)entry["highestRoll"] ?? 0;
                if (!ValidNumbers(n, sum, high)) continue;
                if ((long)result.RollCount + n > int.MaxValue ||
                    result.RollSum + sum > int.MaxValue) break;
                result.RollCount += n;
                result.RollSum += sum;
                result.HighestRoll = Mathf.Max(result.HighestRoll, high);
                result.StoredMatches++;
            }
            Debug.Log("ATB_CAREER_DICE_CLOUD stage=loaded stored_matches=" +
                result.StoredMatches + " rolls=" + result.RollCount +
                " uid_suffix=" + Suffix(principal.Uid) + " reward_authority=false");
            return result;
        }
        catch (Exception error)
        {
            Debug.LogWarning("ATB_CAREER_DICE_CLOUD stage=unavailable type=" +
                error.GetType().Name + " uid_suffix=" + Suffix(principal.Uid));
            return null;
        }
    }

    private static bool ValidMatchId(string id)
    {
        if (id == null || id.Length != 32) return false;
        foreach (char c in id)
            if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
        return true;
    }

    private static bool ValidNumbers(int n, int sum, int high)
    {
        return n > 0 && n <= 5000 &&
               sum >= n * 2 && sum <= n * 12 &&
               high >= 2 && high <= 12 &&
               sum >= high + (n - 1) * 2 && sum <= high * n;
    }

    private static bool ValidObservation(AtlasBoardProgressionMatchSummary m)
    {
        return ValidMatchId(m.matchId) &&
               ValidNumbers(m.diceRolls, m.totalDiceValue, m.highestRoll);
    }

    private static JObject ParseMap(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Trim() == "null")
            return new JObject();
        return JObject.Parse(body);
    }

    private static async Task<Principal> ResolvePrincipalAsync()
    {
        string expected = AtlasBoardRtdbMetaEconomy.CurrentUid;
        if (string.IsNullOrWhiteSpace(expected)) return null;
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
        {
            if (!AtlasBoardAccountFormQualityGate.TryGetVerifiedRegisteredRestPrincipal(
                    out string accountUid) || accountUid != expected ||
                !AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(
                    out string liveUid, out string liveToken) ||
                liveUid != expected || string.IsNullOrWhiteSpace(liveToken)) return null;
            return new Principal { Uid = expected, Token = liveToken,
                RegisteredRest = true };
        }
        try
        {
            FirebaseUser native = FirebaseAuth.DefaultInstance.CurrentUser;
            if (native == null || native.UserId != expected || native.IsAnonymous)
                return null;
            string token = await native.TokenAsync(false);
            return string.IsNullOrWhiteSpace(token) ? null :
                new Principal { Uid = expected, Token = token };
        }
        catch (Exception) { return null; }
    }

    private static async Task<Response> SendAsync(
        Principal principal, string method, string path, string json, string etag,
        bool allowRefresh = true)
    {
        if (principal == null || string.IsNullOrWhiteSpace(principal.Token) ||
            !string.Equals(principal.Uid, AtlasBoardRtdbMetaEconomy.CurrentUid,
                StringComparison.Ordinal)) return new Response { Status = 0, Body = "" };
        string url = Database + path + (path.Contains("?") ? "&" : "?") +
                     "auth=" + UnityWebRequest.EscapeURL(principal.Token);
        Response result;
        using (UnityWebRequest request = new UnityWebRequest(url, method))
        {
            request.timeout = 15;
            request.downloadHandler = new DownloadHandlerBuffer();
            if (json != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (!string.IsNullOrWhiteSpace(etag))
                request.SetRequestHeader("if-match", etag);
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);
            TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            if (operation.isDone) done.TrySetResult(true);
            else operation.completed += _ => done.TrySetResult(true);
            await done.Task;
            result = new Response
            {
                Status = request.responseCode,
                Body = request.downloadHandler != null ? request.downloadHandler.text : ""
            };
        }
        // Ordinary RTDB permission denials remain denials. For existing
        // registered REST sessions only, retry once with the same-UID token.
        if (allowRefresh && principal.RegisteredRest &&
            (result.Status == 401 || result.Status == 403))
        {
            string refreshed = await AtlasBoardRtdbMetaEconomy
                .RefreshRegisteredRestTokenForCurrentUidAsync(principal.Uid);
            if (!string.IsNullOrWhiteSpace(refreshed))
            {
                principal.Token = refreshed;
                return await SendAsync(principal, method, path, json, etag, false);
            }
        }
        return result;
    }

    private static string Suffix(string uid)
    {
        return string.IsNullOrEmpty(uid) ? "none" :
            uid.Substring(Math.Max(0, uid.Length - 5));
    }
}
