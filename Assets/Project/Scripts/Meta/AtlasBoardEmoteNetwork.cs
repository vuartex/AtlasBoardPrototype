using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Auth;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

// Single atomic RTDB sender slot per room + UID. Server rules verify actual
// membership, loadout, 3s spacing, rolling 20/180s and 3-minute mute.
public static class AtlasBoardEmoteNetwork
{
    private const string Database = "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    public sealed class Event
    {
        public string emoteId;
        public long atMs, seq, windowStart, count, muteUntil;
    }
    private sealed class Reply { public long code; public string body, etag; }
    private static Task<(string uid,string token)> Identity()
    {
        // Every emote MUST use the exact identity that owns the live lobby
        // seat. Native FirebaseAuth can be a different cached login in the
        // Steam Editor / No-Steam QA arrangement.
        AtlasBoardLobbyRuntimeBridge bridge =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>();
        string uid = bridge != null ? bridge.CurrentAccountId : "";
        string token = bridge != null ? bridge.AuthTokenForOnlineSubsystems : "";
        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(token))
            return Task.FromResult(("", ""));
        return Task.FromResult((uid, token));
    }
    private static async Task<Reply> Call(string method, string path, string token, string body = null, string etag = null)
    {
        string url = Database + path + ".json?auth=" + UnityWebRequest.EscapeURL(token);
        using (UnityWebRequest req = method == "GET" ? UnityWebRequest.Get(url) : UnityWebRequest.Put(url, body ?? "{}"))
        {
            req.method = method;
            if (method == "GET") req.SetRequestHeader("X-Firebase-ETag", "true");
            else req.SetRequestHeader("Content-Type", "application/json");
            if (etag != null) req.SetRequestHeader("if-match", etag);
            var done = new TaskCompletionSource<bool>();
            req.SendWebRequest().completed += _ => done.TrySetResult(true);
            await done.Task;
            return new Reply { code=req.responseCode,
                body=req.downloadHandler != null ? req.downloadHandler.text : "",
                etag=req.GetResponseHeader("ETag") };
        }
    }
    private static string Path(string room, string uid = null) => "/emoteEvents/" +
        UnityWebRequest.EscapeURL(room) + (uid != null ? "/" + UnityWebRequest.EscapeURL(uid) : "");
    public static async Task<string> SendAsync(string roomId, string id)
    {
        if (!AtlasBoardEmotes.IsEmote(id) || string.IsNullOrWhiteSpace(roomId))
            return "not_allowed";

        var (uid, token) = await Identity();
        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(token))
            return "not_signed_in";

        bool isBundledFreeEmote = AtlasBoardEmotes.FreeIds.Contains(id);

        try
        {
            // The three bundled face emotes are always available to an
            // authenticated room member, including the anonymous No-Steam QA
            // Guest. They do not require a persisted emoteLoadout record.
            // Paid/custom emotes still require the authoritative server-side
            // loadout and its ownership validation.
            if (!isBundledFreeEmote)
            {
                await AtlasBoardEmoteLoadoutService.LoadAsync();
                if (!AtlasBoardEmoteLoadoutService.Active.Contains(id))
                    return "not_equipped";

                Reply serverLoadout = await Call("GET", "/users/" +
                    UnityWebRequest.EscapeURL(uid) + "/emoteLoadout", token);
                if (serverLoadout.code != 200)
                {
                    Debug.LogWarning("ATB_EMOTE_SEND stage=loadout_read http=" +
                        serverLoadout.code);
                    return "loadout_unavailable";
                }

                if (string.IsNullOrWhiteSpace(serverLoadout.body) ||
                    serverLoadout.body.Trim() == "null")
                    return "not_equipped";

                JObject equipped = JObject.Parse(serverLoadout.body);
                bool serverHasEmote = false;
                for (int slot = 1; slot <= 6; slot++)
                {
                    if (string.Equals((string)equipped["slot" + slot], id,
                        StringComparison.Ordinal))
                    {
                        serverHasEmote = true;
                        break;
                    }
                }

                if (!serverHasEmote)
                {
                    Debug.LogWarning("ATB_EMOTE_SEND stage=loadout_check equipped=false");
                    return "not_equipped";
                }
            }
            else if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
            {
                Debug.Log("ATB_EMOTE_GUEST_FREE path=direct_server_validated");
            }

            Reply previous = await Call("GET", Path(roomId, uid), token);
            if (previous.code != 200)
            {
                Debug.LogWarning("ATB_EMOTE_SEND stage=read http=" + previous.code);
                return previous.code == 401 || previous.code == 403
                    ? "denied"
                    : "network_error";
            }

            Event state = null;
            if (previous.body != "null" && !string.IsNullOrWhiteSpace(previous.body))
                state = JObject.Parse(previous.body).ToObject<Event>();

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (state != null && now < state.muteUntil)
                return "muted";
            if (state != null && now - state.atMs < 3000)
                return "cooldown";

            bool newWindow = state == null || now - state.windowStart >= 180000;
            if (state != null && !newWindow && state.count >= 20)
                return "muted";

            long count = newWindow ? 1 : state.count + 1;
            Event next = new Event
            {
                emoteId = id,
                atMs = now,
                seq = state == null ? 1 : state.seq + 1,
                windowStart = newWindow ? now : state.windowStart,
                count = count,
                muteUntil = count == 20
                    ? now + 180000
                    : newWindow ? 0 : state.muteUntil
            };

            string payload = Newtonsoft.Json.JsonConvert.SerializeObject(next);
            Reply sent = await Call("PUT", Path(roomId, uid), token, payload,
                string.IsNullOrWhiteSpace(previous.etag) ? "null_etag" : previous.etag);

            if (sent.code == 412)
                return "retry";

            if (sent.code < 200 || sent.code >= 300)
            {
                Debug.LogWarning("ATB_EMOTE_SEND stage=write http=" + sent.code +
                    " room_member_uid_matched=true");
                return "denied";
            }

            return "ok";
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_EMOTE_SEND " + e.GetType().Name);
            return "network_error";
        }
    }
    public static async Task<Dictionary<string,Event>> ReadRoomAsync(string roomId)
    {
        var found = new Dictionary<string,Event>(StringComparer.Ordinal);
        var (uid,token) = await Identity();
        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(token) ||
            string.IsNullOrEmpty(roomId)) return found;
        try
        {
            Reply r=await Call("GET",Path(roomId),token);
            if (r.code!=200 || string.IsNullOrWhiteSpace(r.body) || r.body=="null") return found;
            JObject obj=JObject.Parse(r.body);
            foreach (JProperty field in obj.Properties())
            {
                if (!(field.Value is JObject)) continue;
                var value=field.Value.ToObject<Event>();
                if (value!=null && AtlasBoardEmotes.IsEmote(value.emoteId)) found[field.Name]=value;
            }
        }
        catch (Exception e) { Debug.LogWarning("ATB_EMOTE_RECEIVE " + e.GetType().Name); }
        return found;
    }
}
