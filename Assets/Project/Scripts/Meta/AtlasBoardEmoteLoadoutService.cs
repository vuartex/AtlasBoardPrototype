using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;

// Canonical UID-scoped emote loadout. Never stores wallet or grants cosmetics.
// Firebase RTDB rules remain responsible for UID ownership and paid entitlements.
public static class AtlasBoardEmoteLoadoutService
{
    private const string Database =
        "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    private const int MaxActiveEmotes = 6;
    private const int MinActiveEmotes = 1;

    [Serializable]
    private sealed class Wire
    {
        public string slot1 = "", slot2 = "", slot3 = "", slot4 = "", slot5 = "", slot6 = "";
        public string[] ToArray() => new[] { slot1, slot2, slot3, slot4, slot5, slot6 };
        public static Wire From(IList<string> ids)
        {
            return new Wire
            {
                slot1 = ids.Count > 0 ? ids[0] : "",
                slot2 = ids.Count > 1 ? ids[1] : "",
                slot3 = ids.Count > 2 ? ids[2] : "",
                slot4 = ids.Count > 3 ? ids[3] : "",
                slot5 = ids.Count > 4 ? ids[4] : "",
                slot6 = ids.Count > 5 ? ids[5] : ""
            };
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
            AtlasBoardLobbyRuntimeBridge bridge =
                UnityEngine.Object.FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>();
            string uid = bridge != null && bridge.HasLobby
                ? bridge.CurrentAccountId : AtlasBoardRtdbMetaEconomy.CurrentUid;
            return !string.IsNullOrEmpty(uid) && uid == cacheUid && cache != null
                ? (IReadOnlyList<string>)cache : AtlasBoardEmotes.FreeIds;
        }
    }

    public static void Invalidate()
    {
        cacheUid = "";
        cache = null;
        cacheTag = "null_etag";
    }

    private static async Task<(string uid, string token)> Identify()
    {
        // In Live Spark Windows QA the signed-in Firebase UID is verified by
        // the REST bridge, not FirebaseAuth.DefaultInstance. HasLobby is false
        // in Career/Store, so it must never be used as the only REST gate.
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
        {
            if (AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(
                    out string guestUid, out string guestToken) &&
                !string.IsNullOrWhiteSpace(guestUid) &&
                !string.IsNullOrWhiteSpace(guestToken))
            {
                string walletUid = AtlasBoardRtdbMetaEconomy.CurrentUid;
                if (!string.IsNullOrWhiteSpace(walletUid) &&
                    !string.Equals(walletUid, guestUid, StringComparison.Ordinal))
                {
                    Debug.LogWarning("ATB_EMOTE_LOADOUT identity_mismatch=true code=ATB831");
                    return ("", "");
                }
                return (guestUid, guestToken);
            }
            return ("", "");
        }

        AtlasBoardLobbyRuntimeBridge bridge =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>();
        if (bridge != null && bridge.HasLobby)
        {
            string owner = bridge.CurrentAccountId;
            string token = bridge.AuthTokenForOnlineSubsystems;
            return !string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(token)
                ? (owner, token) : ("", "");
        }
        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null || string.IsNullOrWhiteSpace(user.UserId)) return ("", "");
            string token = await user.TokenAsync(false);
            return (user.UserId, token ?? "");
        }
        catch { return ("", ""); }
    }

    private static async Task<Response> Request(
        string method, string path, string token, string body = null, string etag = null)
    {
        string url = Database + path + ".json?auth=" + UnityWebRequest.EscapeURL(token);
        using (UnityWebRequest request = method == "GET"
            ? UnityWebRequest.Get(url) : UnityWebRequest.Put(url, body ?? "{}"))
        {
            request.method = method;
            request.timeout = 15;
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);
            if (method == "GET") request.SetRequestHeader("X-Firebase-ETag", "true");
            if (method != "GET") request.SetRequestHeader("Content-Type", "application/json");
            if (etag != null) request.SetRequestHeader("if-match", etag);
            var done = new TaskCompletionSource<bool>();
            var requestOperation = request.SendWebRequest();
            requestOperation.completed += _ => done.TrySetResult(true);
            if (requestOperation.isDone) done.TrySetResult(true);
            await done.Task;
            return new Response
            {
                Code = request.responseCode,
                Body = request.downloadHandler != null ? request.downloadHandler.text : "",
                ETag = request.GetResponseHeader("ETag")
            };
        }
    }

    private static string Path(string uid) =>
        "/users/" + UnityWebRequest.EscapeURL(uid) + "/emoteLoadout";

    private static List<string> Defaults()
    {
        // At least one bundled free emote remains available without purchases.
        return new List<string>(AtlasBoardEmotes.FreeIds.Take(MaxActiveEmotes));
    }

    public static async Task<IReadOnlyList<string>> LoadAsync()
    {
        var (uid, token) = await Identify();
        if (uid.Length == 0 || token.Length == 0)
        {
            Invalidate();
            Debug.LogWarning("ATB_EMOTE_LOADOUT load_blocked=true code=ATB831");
            return AtlasBoardEmotes.FreeIds;
        }
        try
        {
            Response result = await Request("GET", Path(uid), token);
            if (result.Code != 200)
                throw new InvalidOperationException("LOAD_HTTP_" + result.Code);
            Wire wire = string.IsNullOrWhiteSpace(result.Body) ||
                result.Body.Trim() == "null" ? null : JsonUtility.FromJson<Wire>(result.Body);
            var parsed = wire == null ? Defaults() : wire.ToArray()
                .Where(s => !string.IsNullOrEmpty(s) && AtlasBoardEmotes.IsEmote(s))
                .Distinct(StringComparer.Ordinal).Take(MaxActiveEmotes).ToList();
            bool repair = wire == null || parsed.Count < MinActiveEmotes;
            if (parsed.Count < MinActiveEmotes) parsed = Defaults();
            cache = parsed;
            cacheUid = uid;
            cacheTag = string.IsNullOrWhiteSpace(result.ETag) ? "null_etag" : result.ETag;
            if (repair && !await SaveAsync(parsed))
                Debug.LogWarning("ATB_EMOTE_LOADOUT status=seed_pending code=ATB953");
            Debug.Log("ATB_EMOTE_LOADOUT status=loaded count=" + cache.Count +
                " uid_suffix=" + uid.Substring(Math.Max(0, uid.Length - 5)));
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_EMOTE_LOADOUT load_failed=true code=ATB952 type=" +
                e.GetType().Name);
            cacheUid = uid;
            cache = Defaults();
        }
        return Active;
    }

    public static async Task<bool> SaveAsync(IList<string> ids)
    {
        var (uid, token) = await Identify();
        if (uid.Length == 0 || token.Length == 0 || ids == null ||
            ids.Count < MinActiveEmotes || ids.Count > MaxActiveEmotes ||
            ids.Distinct(StringComparer.Ordinal).Count() != ids.Count ||
            ids.Any(s => !AtlasBoardEmotes.IsEmote(s) ||
                AtlasBoardEmotes.IsPaid(s) &&
                !AtlasBoardRtdbMetaEconomy.IsEmoteOwnedForCurrentUid(s)))
        {
            Debug.LogWarning("ATB_EMOTE_LOADOUT save_rejected=true code=ATB951");
            return false;
        }
        try
        {
            // Fresh ETag prevents overwriting another device's loadout edit.
            Response state = await Request("GET", Path(uid), token);
            if (state.Code != 200) return false;
            Response written = await Request("PUT", Path(uid), token,
                JsonUtility.ToJson(Wire.From(ids)),
                string.IsNullOrWhiteSpace(state.ETag) ? "null_etag" : state.ETag);
            if (written.Code < 200 || written.Code >= 300)
            {
                Debug.LogWarning("ATB_EMOTE_LOADOUT save_denied=true code=ATB952 http=" + written.Code);
                return false;
            }
            cacheUid = uid;
            cache = new List<string>(ids);
            cacheTag = written.ETag;
            Debug.Log("ATB_EMOTE_LOADOUT status=saved count=" + cache.Count +
                " uid_suffix=" + uid.Substring(Math.Max(0, uid.Length - 5)));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_EMOTE_LOADOUT save_failed=true code=ATB952 type=" +
                e.GetType().Name);
            return false;
        }
    }
}
