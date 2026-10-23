using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public enum AtlasBoardChatScopeType { Lobby = 0, Match = 1 }

[Serializable]
public sealed class AtlasBoardChatMessage
{
    public string messageId;
    public string accountId;
    public string displayName;
    public string body;
    public long createdAtEpochMs;
    public int senderUtcOffsetMinutes;
    public int schemaVersion;
}

public sealed class AtlasBoardChatScope
{
    public AtlasBoardChatScopeType ScopeType;
    public string ScopeId;
    public string StableKey => $"{ScopeType}:{ScopeId}";
}

public sealed class AtlasBoardChatListResult
{
    public bool Success;
    public string ErrorKey;
    public string TechnicalMessage;
    public List<AtlasBoardChatMessage> Messages = new List<AtlasBoardChatMessage>();
    public long ServerNowEpochMs;
    public int CooldownMs = 10000;
    public int MaxMessageLength = 120;
}

public sealed class AtlasBoardChatSendResult
{
    public bool Success;
    public string ErrorKey;
    public string TechnicalMessage;
    public AtlasBoardChatMessage Message;
    public long ServerNowEpochMs;
    public int CooldownMs = 10000;
    public int RetryAfterMs;
}

/// <summary>
/// Casual-only RTDB chat. No Cloud Functions/Blaze dependency.
/// Backend Rules MUST be deployed before this transport can send or read.
/// Moderation/report actions still have a separate release-security gate.
/// </summary>
public sealed class AtlasBoardChatRuntimeBridge : MonoBehaviour
{
    private const string DatabaseUrl = "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    private const int MaxMessageLength = 120;
    private const int CooldownMs = 10000;
    private AtlasBoardLobbyRuntimeBridge lobbyBridge;
    private static readonly System.Collections.Generic.HashSet<string> PendingRequests = new System.Collections.Generic.HashSet<string>();

    public string LocalAccountId => lobbyBridge != null ? lobbyBridge.CurrentAccountId : string.Empty;

    private void Awake() { ResolveLobbyBridge(); }

    public bool TryResolveActiveScope(out AtlasBoardChatScope scope)
    {
        scope = null;
        ResolveLobbyBridge();
        if (lobbyBridge == null || !lobbyBridge.HasLobby || lobbyBridge.CurrentSnapshot == null ||
            string.IsNullOrWhiteSpace(lobbyBridge.CurrentLobbyId)) return false;
        AtlasLobbySnapshot snapshot = lobbyBridge.CurrentSnapshot;
        bool match = !string.IsNullOrWhiteSpace(snapshot.MatchId) &&
                     snapshot.LifecycleState != AtlasRoomLifecycleState.Waiting;
        scope = new AtlasBoardChatScope {
            ScopeType = match ? AtlasBoardChatScopeType.Match : AtlasBoardChatScopeType.Lobby,
            ScopeId = match ? snapshot.MatchId : lobbyBridge.CurrentLobbyId
        };
        return !string.IsNullOrWhiteSpace(scope.ScopeId);
    }

    public async Task<AtlasBoardChatListResult> ListMessagesAsync(AtlasBoardChatScope scope, long afterEpochMs)
    {
        if (!ValidScope(scope)) return ListFail("chat.error.invalid_scope", "Missing or invalid chat scope.");
        if (!await EnsureIdentityAsync()) return ListFail("chat.error.authentication_required", "Firebase Auth identity unavailable.");
        string token = lobbyBridge.AuthTokenForOnlineSubsystems;
        if (string.IsNullOrEmpty(token)) return ListFail("chat.error.authentication_required", "Firebase Auth token missing.");
        string path = ChatPath(scope);
        string query = "orderBy=%22createdAtEpochMs%22&limitToLast=50";
        using (UnityWebRequest request = await RequestAsync("GET", path, token, null, query))
        {
            if (request.result != UnityWebRequest.Result.Success)
                return ListFail("chat.error.service_unavailable", "RTDB chat list HTTP " + request.responseCode);
            List<AtlasBoardChatMessage> parsed = ParseMessageMap(request.downloadHandler.text);
            parsed.Sort((a, b) => {
                int c = a.createdAtEpochMs.CompareTo(b.createdAtEpochMs);
                return c != 0 ? c : string.CompareOrdinal(a.messageId, b.messageId);
            });
            AtlasBoardChatListResult result = new AtlasBoardChatListResult { Success = true,
                ServerNowEpochMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            foreach (AtlasBoardChatMessage message in parsed)
                if (message.createdAtEpochMs >= afterEpochMs) result.Messages.Add(message);
            return result;
        }
    }

    public async Task<AtlasBoardChatSendResult> SendMessageAsync(
        AtlasBoardChatScope scope, string body, int senderUtcOffsetMinutes)
    {
        if (!ValidScope(scope)) return SendFail("chat.error.invalid_scope", "Missing chat scope.");
        string text = (body ?? string.Empty).Trim();
        // Prevent basic TMP rich-text markup from casual chat UI content.
        text = text.Replace("<", "‹").Replace(">", "›");
        if (text.Length == 0 || text.Length > MaxMessageLength)
            return SendFail(text.Length == 0 ? "chat.error.empty_message" : "chat.error.message_too_long", "Body length must be 1..120.");
        if (!await EnsureIdentityAsync())
            return SendFail("chat.error.authentication_required", "Firebase identity not ready.");
        string token = lobbyBridge.AuthTokenForOnlineSubsystems;
        string uid = lobbyBridge.CurrentAccountId;
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(uid))
            return SendFail("chat.error.authentication_required", "Missing authenticated UID/token.");
        string name = GetLocalMemberDisplayName(uid);
        if (string.IsNullOrWhiteSpace(name))
            return SendFail("chat.error.invalid_scope", "Current UID not present in room snapshot.");
        string kind = ScopeKind(scope);
        string throttlePath = "chatThrottle/" + kind + "/" + scope.ScopeId + "/" + uid;
        string id = Guid.NewGuid().ToString("N");
        string gate = scope.StableKey + ":" + uid;
        lock (PendingRequests)
        {
            if (!PendingRequests.Add(gate)) return SendFail("chat.error.rate_limited", "Chat send already in flight.", CooldownMs);
        }
        try
        {
            string throttleJson = "{\"token\":\"" + id + "\",\"lastSentAt\":{\".sv\":\"timestamp\"}}";
            using (UnityWebRequest throttle = await RequestAsync("PUT", throttlePath, token, throttleJson))
            {
                if (throttle.result != UnityWebRequest.Result.Success)
                {
                    bool denied = throttle.responseCode == 401 || throttle.responseCode == 403;
                    return SendFail(denied ? "chat.error.rate_limited" : "chat.error.service_unavailable",
                        "RTDB throttle HTTP " + throttle.responseCode, denied ? CooldownMs : 0);
                }
            }
            int offset = Mathf.Clamp(senderUtcOffsetMinutes, -840, 840);
            AtlasBoardChatMessage message = new AtlasBoardChatMessage {
                accountId = uid, messageId = id, displayName = name, body = text,
                createdAtEpochMs = 0, senderUtcOffsetMinutes = offset, schemaVersion = 1
            };
            string json = JsonUtility.ToJson(message).Replace("\"createdAtEpochMs\":0",
                "\"createdAtEpochMs\":{\".sv\":\"timestamp\"}");
            if (!json.Contains("\".sv\":\"timestamp\""))
                return SendFail("chat.error.service_unavailable", "Server timestamp serialization did not match.");
            using (UnityWebRequest send = await RequestAsync("PUT", ChatPath(scope) + "/" + id, token, json))
            {
                if (send.result != UnityWebRequest.Result.Success)
                    return SendFail("chat.error.service_unavailable", "RTDB message HTTP " + send.responseCode);
            }
            // Server timestamp is authoritative. If the post-write read fails,
            // let the next polling cycle render it rather than showing 1970.
            AtlasBoardChatMessage stored = null;
            using (UnityWebRequest read = await RequestAsync("GET", ChatPath(scope) + "/" + id, token, null))
            {
                if (read.result == UnityWebRequest.Result.Success)
                {
                    AtlasBoardChatMessage saved = SafeFromJson<AtlasBoardChatMessage>(read.downloadHandler.text);
                    if (saved != null && saved.createdAtEpochMs > 0) stored = saved;
                }
            }
            Debug.Log("ATB_RTDB_CHAT_SEND success=true scope=" + kind + " backend=spark_rtdb payload_logged=false");
            return new AtlasBoardChatSendResult { Success = true, Message = stored,
                ServerNowEpochMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        }
        finally { lock (PendingRequests) { PendingRequests.Remove(gate); } }
    }

    private string GetLocalMemberDisplayName(string uid)
    {
        AtlasLobbySnapshot snapshot = lobbyBridge != null ? lobbyBridge.CurrentSnapshot : null;
        if (snapshot != null && snapshot.Members != null)
            foreach (AtlasLobbyMemberSnapshot member in snapshot.Members)
                if (member != null && member.Active && string.Equals(uid, member.AccountId,
                    StringComparison.Ordinal)) return member.DisplayName;
        return string.Empty;
    }

    private static bool ValidScope(AtlasBoardChatScope scope)
    {
        if (scope == null || string.IsNullOrWhiteSpace(scope.ScopeId) || scope.ScopeId.Length > 80) return false;
        foreach (char c in scope.ScopeId)
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-')) return false;
        return true;
    }
    private static string ScopeKind(AtlasBoardChatScope s) =>
        s.ScopeType == AtlasBoardChatScopeType.Match ? "match" : "lobby";
    private static string ChatPath(AtlasBoardChatScope s) => "chatMessages/" + ScopeKind(s) + "/" + s.ScopeId;

    private async Task<UnityWebRequest> RequestAsync(string method, string path, string token,
        string json, string parameters = null)
    {
        ResolveLobbyBridge();
        string baseUrl = lobbyBridge.UsingLocalEmulators
            ? "http://" + lobbyBridge.EmulatorHostForOnlineSubsystems + ":9000"
            : DatabaseUrl;
        string url = baseUrl + "/" + path + ".json?auth=" + UnityWebRequest.EscapeURL(token);
        if (!string.IsNullOrEmpty(parameters)) url += "&" + parameters;
        UnityWebRequest request = new UnityWebRequest(url, method);
        request.downloadHandler = new DownloadHandlerBuffer();
        if (json != null)
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.SetRequestHeader("Content-Type", "application/json");
        }
        request.timeout = 20;
        await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);
        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        while (!operation.isDone) await Task.Yield();
        return request;
    }

    // RTDB REST returns an object keyed by push/message IDs, not a JSON array.
    // The existing UI and JsonUtility expect a list. Extract only complete top-level
    // object values; quoted braces / escaped quotes do not terminate the scan.
    private static List<AtlasBoardChatMessage> ParseMessageMap(string json)
    {
        List<AtlasBoardChatMessage> list = new List<AtlasBoardChatMessage>();
        if (string.IsNullOrWhiteSpace(json) || json == "null") return list;
        int i = 0;
        while (i < json.Length && json[i] != '{') i++;
        if (i >= json.Length) return list;
        i++;
        while (i < json.Length)
        {
            SkipWhitespace(json, ref i);
            if (i >= json.Length || json[i] == '}') break;
            string key = ReadQuoted(json, ref i);
            if (key == null) break;
            SkipWhitespace(json, ref i);
            if (i >= json.Length || json[i++] != ':') break;
            SkipWhitespace(json, ref i);
            if (i >= json.Length || json[i] != '{') break;
            int start = i, depth = 0;
            bool quoted = false, escaped = false;
            for (; i < json.Length; i++)
            {
                char c = json[i];
                if (quoted)
                {
                    if (escaped) { escaped = false; continue; }
                    if (c == '\\') { escaped = true; continue; }
                    if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == '{') depth++;
                else if (c == '}' && --depth == 0) { i++; break; }
            }
            if (depth != 0) break;
            AtlasBoardChatMessage item = SafeFromJson<AtlasBoardChatMessage>(json.Substring(start, i - start));
            if (item != null && !string.IsNullOrWhiteSpace(item.body) && item.createdAtEpochMs > 0)
            {
                if (string.IsNullOrEmpty(item.messageId)) item.messageId = key;
                list.Add(item);
            }
            SkipWhitespace(json, ref i);
            if (i < json.Length && json[i] == ',') i++;
        }
        return list;
    }
    private static void SkipWhitespace(string s, ref int i)
    { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
    private static string ReadQuoted(string s, ref int i)
    {
        if (i >= s.Length || s[i++] != '"') return null;
        int start = i;
        bool escaped = false;
        for (; i < s.Length; i++)
        {
            char c = s[i];
            if (escaped) { escaped = false; continue; }
            if (c == '\\') { escaped = true; continue; }
            if (c == '"') { string result = s.Substring(start, i - start); i++; return result; }
        }
        return null;
    }
    private static T SafeFromJson<T>(string json) where T : class
    {
        try { return JsonUtility.FromJson<T>(json); } catch { return null; }
    }
    private async Task<bool> EnsureIdentityAsync()
    {
        ResolveLobbyBridge();
        return lobbyBridge != null && await lobbyBridge.EnsureOnlineIdentityAsync();
    }
    private void ResolveLobbyBridge()
    {
        if (lobbyBridge != null) return;
        foreach (AtlasBoardLobbyRuntimeBridge item in Resources.FindObjectsOfTypeAll<AtlasBoardLobbyRuntimeBridge>())
            if (item != null && item.gameObject.scene.IsValid()) { lobbyBridge = item; break; }
    }
    private static AtlasBoardChatListResult ListFail(string key, string message) =>
        new AtlasBoardChatListResult { Success = false, ErrorKey = key, TechnicalMessage = message };
    private static AtlasBoardChatSendResult SendFail(string key, string message, int retry = 0) =>
        new AtlasBoardChatSendResult { Success = false, ErrorKey = key, TechnicalMessage = message, RetryAfterMs = retry };

    public static bool ValidateWireParserContract()
    {
        string sample = "{\"a\":{\"messageId\":\"a\",\"accountId\":\"u\",\"displayName\":\"User\",\"body\":\"Hello { \\\"ok\\\" }\",\"createdAtEpochMs\":1234,\"schemaVersion\":1},\"b\":{\"messageId\":\"b\",\"accountId\":\"u\",\"displayName\":\"User\",\"body\":\"Two\",\"createdAtEpochMs\":1235,\"schemaVersion\":1}}";
        List<AtlasBoardChatMessage> x = ParseMessageMap(sample);
        return x.Count == 2 && x[0].body == "Hello { \"ok\" }" && x[1].body == "Two";
    }
}
