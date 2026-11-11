using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Three UID-private Museum display slots. The inventory is READ ONLY and
/// derived exclusively from RTDB collectibleTokens ownership receipts.
/// Slot writes use an ETag + Firebase security rules that independently check
/// ownership and prevent any token from appearing in multiple display slots.
/// No economy grant, login manager, or token issuance path exists here.
/// </summary>
[DefaultExecutionOrder(31005)]
public sealed class AtlasBoardMuseumCollection : MonoBehaviour
{
    private const string Database = "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    public static AtlasBoardMuseumCollection Instance { get; private set; }
    public event Action Changed;
    public sealed class Token
    {
        public string Id;
        public string Title;
        public string WeekId;
    }

    public readonly List<Token> Owned = new List<Token>();
    public readonly string[] Slots = { "", "", "" };
    public string Uid { get; private set; } = "";
    public string Message { get; private set; } = "";
    public bool Loaded { get; private set; }
    public bool Busy { get; private set; }
    public int Revision { get; private set; }
    private bool reading;
    private float nextPollAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("AtlasBoard_Museum_Collection_RTDB");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AtlasBoardMuseumCollection>();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void Notify() { ++Revision; Changed?.Invoke(); }
    private void ResetUid(string uid)
    {
        Uid = uid ?? "";
        Owned.Clear();
        for (int i = 0; i < 3; i++) Slots[i] = "";
        Loaded = false;
        Message = "";
        nextPollAt = 0;
        Notify();
    }
    private bool SameUid(string uid) => !string.IsNullOrEmpty(uid) &&
        uid == Uid && uid == (AtlasBoardRtdbMetaEconomy.CurrentUid ?? "");
    private void Update()
    {
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid ?? "";
        if (!string.Equals(uid, Uid, StringComparison.Ordinal)) ResetUid(uid);
        // The museum is refreshed when entered; modest retry only for a
        // temporarily unavailable account/token, without polling each frame.
    }
    private static string Esc(string value) => UnityWebRequest.EscapeURL(value);
    private static async Task<string> TokenAsync(string uid)
    {
        string token = await AtlasBoardRtdbMetaEconomy.GetCurrentCallableTokenAsync();
        if (string.IsNullOrWhiteSpace(token) && !AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
        {
            try
            {
                FirebaseUser principal = FirebaseAuth.DefaultInstance.CurrentUser;
                if (principal != null && principal.UserId == uid) token = await principal.TokenAsync(false);
            }
            catch { /* native auth not ready */ }
        }
        return token;
    }
    private static async Task<(long status, string body, string etag)> RequestAsync(
        string uid, string token, string path, string method, string body = null,
        string etag = null, bool readEtag = false)
    {
        using (var req = new UnityWebRequest(Database + "/" + path + ".json?auth=" + Esc(token), method))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            if (readEtag) req.SetRequestHeader("X-Firebase-ETag", "true");
            if (!string.IsNullOrEmpty(etag)) req.SetRequestHeader("if-match", etag);
            if (body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.timeout = 18;
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(req);
            var operation = req.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            return (req.responseCode, req.downloadHandler != null ? req.downloadHandler.text : "",
                req.GetResponseHeader("ETag") ?? "");
        }
    }
    private static string Clean(string text, int max = 60)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return text.Length <= max ? text : text.Substring(0, max);
    }
    private static string DisplayTitle(string id, string catalogName)
    {
        if (!string.IsNullOrWhiteSpace(catalogName)) return Clean(catalogName);
        return "Atlas " + (id.StartsWith("weekly_", StringComparison.Ordinal) ? id.Substring(7) : Clean(id, 20));
    }
    private static void ReadDisplay(string body, string[] result)
    {
        for (int i = 0; i < 3; i++) result[i] = "";
        if (string.IsNullOrWhiteSpace(body) || body.Trim() == "null") return;
        try
        {
            var json = JObject.Parse(body);
            for (int i = 0; i < 3; i++) result[i] = (string)json["s" + i] ?? "";
        }
        catch { /* reject malformed data by showing empty slots */ }
    }
    private bool HasOwned(string id) => Owned.Exists(t => t.Id == id);

    public async Task RefreshAsync(bool force = false)
    {
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid ?? "";
        if (uid != Uid) ResetUid(uid);
        if (string.IsNullOrEmpty(uid)) { Message = "Sign in to view your collection"; Notify(); return; }
        if (reading || Busy || (!force && Loaded && Time.unscaledTime < nextPollAt)) return;
        reading = true;
        Message = "Loading verified collection";
        Notify();
        try
        {
            string token = await TokenAsync(uid);
            if (!SameUid(uid) || string.IsNullOrWhiteSpace(token)) return;
            var response = await RequestAsync(uid, token, "users/" + Esc(uid) + "/collectibleTokens", "GET");
            if (!SameUid(uid)) return;
            if (response.status != 200) { Message = "Could not read collectible ownership"; return; }
            var catalogResponse = await RequestAsync(uid, token, "collectibleTokenCatalog", "GET");
            if (!SameUid(uid)) return;
            JObject catalog = null;
            try { if (catalogResponse.status == 200 && catalogResponse.body != "null") catalog = JObject.Parse(catalogResponse.body); }
            catch { /* ownership is still authoritative */ }
            var items = new List<Token>();
            if (!string.IsNullOrEmpty(response.body) && response.body != "null")
            {
                JObject ownership = JObject.Parse(response.body);
                foreach (var entry in ownership.Properties())
                {
                    // Only genuine prior server-verified ownership is displayed.
                    string tokenId = entry.Name;
                    if (!tokenId.StartsWith("weekly_", StringComparison.Ordinal) ||
                        !(entry.Value is JObject owned) || (string)owned["tokenId"] != tokenId ||
                        (string)owned["source"] != "weeklyMissions") continue;
                    string weekId = (string)owned["weekId"] ?? "";
                    if (tokenId != "weekly_" + weekId || weekId.Length != 8) continue;
                    items.Add(new Token { Id = tokenId, WeekId = weekId,
                        Title = DisplayTitle(tokenId, (string)catalog?[tokenId]?["title"]) });
                }
            }
            var display = await RequestAsync(uid, token, "users/" + Esc(uid) + "/collectibleDisplay", "GET");
            if (!SameUid(uid)) return;
            if (display.status != 200) { Message = "Could not load display slots"; return; }
            items.Sort((a, b) => string.CompareOrdinal(b.WeekId, a.WeekId));
            Owned.Clear();
            Owned.AddRange(items);
            string[] saved = new string[3];
            ReadDisplay(display.body, saved);
            for (int i = 0; i < 3; i++)
                Slots[i] = string.IsNullOrEmpty(saved[i]) || HasOwned(saved[i]) ? saved[i] : "";
            Loaded = true;
            Message = items.Count == 0 ? "No collectible tokens owned yet" : "Select an owned token, then select a display slot";
            nextPollAt = Time.unscaledTime + 20f;
        }
        catch (Exception e)
        {
            if (SameUid(uid)) Message = "Collection unavailable: " + e.GetType().Name;
        }
        finally
        {
            reading = false;
            if (SameUid(uid)) Notify();
        }
    }

    /// <param name="tokenId">Empty means remove a token from the selected slot.</param>
    public async Task EquipAsync(int slot, string tokenId)
    {
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid ?? "";
        tokenId = tokenId ?? "";
        if (slot < 0 || slot > 2 || !Loaded || Busy || reading || !SameUid(uid) ||
            (!string.IsNullOrEmpty(tokenId) && !HasOwned(tokenId))) return;
        Busy = true;
        Message = "Verifying slot change";
        Notify();
        try
        {
            string token = await TokenAsync(uid);
            if (!SameUid(uid) || string.IsNullOrWhiteSpace(token)) return;
            string path = "users/" + Esc(uid) + "/collectibleDisplay";
            var old = await RequestAsync(uid, token, path, "GET", null, null, true);
            if (!SameUid(uid)) return;
            if (old.status != 200 || string.IsNullOrEmpty(old.etag))
            { Message = "Slot state unavailable. Refresh and retry"; return; }
            var next = new string[3];
            ReadDisplay(old.body, next);
            // Moving an already displayed token automatically empties its
            // original slot; a single atomic PUT prevents duplicate displays.
            if (!string.IsNullOrEmpty(tokenId))
                for (int i = 0; i < 3; i++)
                    if (next[i] == tokenId) next[i] = "";
            next[slot] = tokenId;
            if (next[0] == next[1] && next[0] != "" ||
                next[0] == next[2] && next[0] != "" ||
                next[1] == next[2] && next[1] != "") return;
            if (next.Any(id => !string.IsNullOrEmpty(id) && !HasOwned(id)))
            { Message = "Ownership changed; refresh required"; return; }
            var payload = new JObject {
                ["s0"] = next[0], ["s1"] = next[1], ["s2"] = next[2]
            }.ToString(Newtonsoft.Json.Formatting.None);
            var changed = await RequestAsync(uid, token, path, "PUT", payload, old.etag);
            if (!SameUid(uid)) return;
            if (changed.status == 412) { Message = "Collection changed on another device; refresh"; return; }
            if (changed.status < 200 || changed.status >= 300)
            { Message = "Slot change rejected by Firebase (HTTP " + changed.status + ")"; return; }
            var verified = await RequestAsync(uid, token, path, "GET");
            if (!SameUid(uid) || verified.status != 200)
            { Message = "Slot saved; waiting to verify"; return; }
            var check = new string[3];
            ReadDisplay(verified.body, check);
            if (!check.SequenceEqual(next)) { Message = "Display sync pending; refresh"; return; }
            for (int i = 0; i < 3; i++) Slots[i] = next[i];
            Message = string.IsNullOrEmpty(tokenId) ? "Token removed from display" : "Token placed in display";
            Notify();
        }
        catch (Exception e)
        {
            if (SameUid(uid)) Message = "Slot change failed: " + e.GetType().Name;
        }
        finally
        {
            if (SameUid(uid)) { Busy = false; Notify(); }
        }
    }
}
