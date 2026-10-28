using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;

// Emotes extend the existing RTDB UID / metaEconomy; no second wallet.
// RTDB rules enforce 6 distinct owned IDs; this cache is presentation-only.
public static class AtlasBoardEmoteLoadoutService
{
    private const string Database = "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    [Serializable] private sealed class Wire
    {
        public string slot1 = "", slot2 = "", slot3 = "", slot4 = "", slot5 = "", slot6 = "";
        public string[] ToArray() => new[] { slot1, slot2, slot3, slot4, slot5, slot6 };
        public static Wire From(IList<string> ids)
        {
            var w = new Wire();
            w.slot1 = ids.Count > 0 ? ids[0] : "";
            w.slot2 = ids.Count > 1 ? ids[1] : "";
            w.slot3 = ids.Count > 2 ? ids[2] : "";
            w.slot4 = ids.Count > 3 ? ids[3] : "";
            w.slot5 = ids.Count > 4 ? ids[4] : "";
            w.slot6 = ids.Count > 5 ? ids[5] : "";
            return w;
        }
    }
    private sealed class Response { public long Code; public string Body; public string ETag; }
    private static string cacheUid = "";
    private static string cacheTag = "null_etag";
    private static List<string> cache;
    public static IReadOnlyList<string> Active
    {
        get
        {
            string uid = AtlasBoardRtdbMetaEconomy.CurrentUid;
            return uid.Length > 0 && uid == cacheUid && cache != null
                ? (IReadOnlyList<string>)cache : AtlasBoardEmotes.FreeIds;
        }
    }
    public static void Invalidate() { cacheUid = ""; cache = null; cacheTag = "null_etag"; }
    private static async Task<(string uid, string token)> Identify()
    {
        try
        {
            FirebaseUser u = FirebaseAuth.DefaultInstance.CurrentUser;
            if (u == null || string.IsNullOrWhiteSpace(u.UserId)) return ("", "");
            string token = await u.TokenAsync(false);
            return (u.UserId, token ?? "");
        }
        catch { return ("", ""); }
    }
    private static async Task<Response> Request(string method, string path, string token, string body = null, string etag = null)
    {
        string url = Database + path + ".json?auth=" + UnityWebRequest.EscapeURL(token);
        using (UnityWebRequest request = method == "GET" ? UnityWebRequest.Get(url) : UnityWebRequest.Put(url, body ?? "{}"))
        {
            request.method = method;
            if (method == "GET") request.SetRequestHeader("X-Firebase-ETag", "true");
            if (method != "GET") request.SetRequestHeader("Content-Type", "application/json");
            if (etag != null) request.SetRequestHeader("if-match", etag);
            var done = new TaskCompletionSource<bool>();
            request.SendWebRequest().completed += _ => done.TrySetResult(true);
            await done.Task;
            return new Response { Code = request.responseCode, Body = request.downloadHandler != null ? request.downloadHandler.text : "",
                ETag = request.GetResponseHeader("ETag") };
        }
    }
    private static string Path(string uid) => "/users/" + UnityWebRequest.EscapeURL(uid) + "/emoteLoadout";
    public static async Task<IReadOnlyList<string>> LoadAsync()
    {
        var (uid, token) = await Identify();
        if (uid.Length == 0 || token.Length == 0) { Invalidate(); return AtlasBoardEmotes.FreeIds; }
        try
        {
            Response r = await Request("GET", Path(uid), token);
            if (r.Code != 200) throw new Exception("HTTP " + r.Code);
            Wire w = string.IsNullOrWhiteSpace(r.Body) || r.Body.Trim() == "null" ? null : JsonUtility.FromJson<Wire>(r.Body);
            cache = w == null ? new List<string>(AtlasBoardEmotes.FreeIds) : w.ToArray()
                .Where(s => !string.IsNullOrEmpty(s) && AtlasBoardEmotes.IsEmote(s)).Distinct().Take(6).ToList();
            cacheUid = uid;
            cacheTag = string.IsNullOrWhiteSpace(r.ETag) ? "null_etag" : r.ETag;
            if (w == null) await SaveAsync(cache); // deterministic three free slots for new UID
        }
        catch (Exception e) { Debug.LogWarning("ATB_EMOTE_LOAD failed " + e.Message); cacheUid = uid; cache = new List<string>(AtlasBoardEmotes.FreeIds); }
        return Active;
    }
    public static async Task<bool> SaveAsync(IList<string> ids)
    {
        var (uid, token) = await Identify();
        if (uid.Length == 0 || token.Length == 0 || ids == null || ids.Count > 6 ||
            ids.Distinct().Count() != ids.Count || ids.Any(s => !AtlasBoardRtdbMetaEconomy.IsEmoteOwnedForCurrentUid(s))) return false;
        try
        {
            // Always read current ETag, avoiding overwrite of another device's latest selection.
            Response state = await Request("GET", Path(uid), token);
            if (state.Code != 200) return false;
            Response written = await Request("PUT", Path(uid), token,
                JsonUtility.ToJson(Wire.From(ids)), string.IsNullOrWhiteSpace(state.ETag) ? "null_etag" : state.ETag);
            if (written.Code < 200 || written.Code >= 300) return false;
            cacheUid = uid; cache = new List<string>(ids); cacheTag = written.ETag;
            Debug.Log("ATB_EMOTE_LOADOUT status=saved count=" + cache.Count);
            return true;
        }
        catch (Exception e) { Debug.LogWarning("ATB_EMOTE_LOADOUT failed " + e.GetType().Name); return false; }
    }
}
