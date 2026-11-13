using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Atlas Passport: a single Passport feature component on the pre-existing
/// Museum/Profile runtime object. No second Profile, Account, Career, or wallet manager.
/// All ownership is backed by immutable, witnessed careerClaims and checked again
/// by RTDB Rules before the receipt is issued. Visuals are generated locally.
/// </summary>
[DisallowMultipleComponent]
public sealed class AtlasBoardPassportFeature : MonoBehaviour
{
    private const string Database = "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    private const int StampCount = 21;
    private const int TotalPages = 18;
    private const int ListPageSize = 7;
    // Read-only bridge to the identity ALREADY resolved by the active Profile.
    // Never borrow the Steam persona, player ID or cached data from another UID.
    private static readonly BindingFlags ProfileFields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo ProfileUidField = typeof(AtlasBoardUnifiedProfileScreen).GetField("currentUid", ProfileFields);
    private static readonly FieldInfo ProfileNameField = typeof(AtlasBoardUnifiedProfileScreen).GetField("displayName", ProfileFields);
    private static readonly FieldInfo ProfilePlayerIdField = typeof(AtlasBoardUnifiedProfileScreen).GetField("loadedPlayerId", ProfileFields);
    private static readonly Color Ink = ColorOf("#8A3558");
    private static readonly Color Navy = ColorOf("#16224B");
    private static readonly Color Paper = ColorOf("#E7D6B2");
    private static readonly Color Gold = ColorOf("#E9B74F");

    private sealed class Stamp
    {
        public string id, metric, map, title, description;
        public int min;
        public bool win;
        public JObject titles, descriptions;
    }
    private sealed class HttpReply
    {
        public long code;
        public string body, etag;
    }
    private readonly List<Stamp> stamps = new List<Stamp>();
    private readonly HashSet<string> owned = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> proofIds = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<int, string[]> pages = new Dictionary<int, string[]>();
    // Old v5.49.12dx page records and v5.49.12ea single-week receipts stay readable.
    // Each owned stamp gets its OWN immutable weekly press and removal receipt.
    // Removing a stamp from a previous week and pressing it again are allowed
    // in the same current week; other stamps never consume this stamp's quota.
    private sealed class Placement
    {
        public long week, atMs;
        public string stampId;
        public int page, slot;
        public bool remove;
    }
    private readonly List<Placement> placementHistory = new List<Placement>();
    private readonly Dictionary<string,int> bestProgress =
        new Dictionary<string,int>(StringComparer.Ordinal);
    private int mutationRevision;
    private const long WeekMilliseconds = 604800000L;
    private const long MondayEpochOffset = 345600000L;

    private GameObject profileObject;
    private RectTransform stage, uiRoot, book, inventory;
    private TMP_Text messageLabel, pageLabel, listPageLabel;
    private RectTransform tooltipRoot, tooltipFill, dialogRoot;
    private TMP_Text tooltipTitle, tooltipObjective, tooltipProgress;
    private string hoveredId = "";
    private int pendingLongPress = -1;
    private float longPressStarted, tooltipHideAt;
    private Vector2 pendingPressPosition;
    private Button claimButton, placeButton, removeButton, previousPage, nextPage, rotateButton;
    private readonly Button[] listButtons = new Button[ListPageSize];
    private readonly Button[] slotButtons = new Button[2];
    private string uid = "", selectedId = "", language = "en", displayName = "", playerId = "";
    private string message = "";
    private int generation, page, listPage, selectedSlot = -1;
    private bool portrait, loaded, busy, refreshing, viewSaving, viewDirty;
    private float nextRefresh, nextUiCheck;

    private void OnEnable() { AtlasBoardLocalizationManager.LanguageChanged += OnLanguageChanged; }
    private void OnDisable() { AtlasBoardLocalizationManager.LanguageChanged -= OnLanguageChanged; }
    private void OnLanguageChanged() { language = Language(); RebuildWhenVisible(); }
    private static Color ColorOf(string html) { ColorUtility.TryParseHtmlString(html, out Color c); return c; }
    private static string Local(string en, string tr, string es, string fr, string de, string ko, string ru) =>
        AtlasBoardL.R(en, tr, es, fr, de, ko, ru);
    private static string Language()
    {
        var lm = AtlasBoardLocalizationManager.Instance;
        string code = lm != null ? lm.CurrentLanguageCode : "en";
        switch ((code ?? "en").ToLowerInvariant())
        {
            case "tr": case "es": case "fr": case "de": case "ko": case "ru": return code.ToLowerInvariant();
            default: return "en";
        }
    }
    private static string Esc(string text) => UnityWebRequest.EscapeURL(text ?? "");
    private bool Same(int g, string id) => g == generation && id == uid &&
        !string.IsNullOrWhiteSpace(id) && id == (AtlasBoardRtdbMetaEconomy.CurrentUid ?? "");
    private void ResetAccount(string id)
    {
        generation++;
        uid = id ?? "";
        stamps.Clear(); owned.Clear(); proofIds.Clear(); pages.Clear();
        bestProgress.Clear(); placementHistory.Clear(); mutationRevision++;
        hoveredId = ""; pendingLongPress = -1; tooltipHideAt = 0;
        tooltipRoot = dialogRoot = null; tooltipFill = null;
        page = listPage = 0; selectedSlot = -1; selectedId = "";
        displayName = playerId = ""; message = "";
        loaded = refreshing = busy = viewSaving = viewDirty = false; portrait = false;
        nextRefresh = 0;
        if (uiRoot != null) { Destroy(uiRoot.gameObject); uiRoot = null; }
        stage = null;
    }
    // Profile owns the canonical identity labels and UID-specific cache. The
    // Passport only reads them; it never updates the Profile or Firebase user.
    private bool SyncCurrentProfileIdentity()
    {
        if (profileObject == null || string.IsNullOrWhiteSpace(uid) ||
            ProfileUidField == null || ProfileNameField == null || ProfilePlayerIdField == null)
            return false;
        var profile = profileObject.GetComponent<AtlasBoardUnifiedProfileScreen>();
        if (profile == null ||
            !string.Equals(ProfileUidField.GetValue(profile) as string, uid, StringComparison.Ordinal) ||
            !string.Equals(AtlasBoardRtdbMetaEconomy.CurrentUid ?? "", uid, StringComparison.Ordinal))
            return false;
        bool changed = false;
        string name = (ProfileNameField.GetValue(profile) as string ?? "").Trim();
        string id = (ProfilePlayerIdField.GetValue(profile) as string ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(name) && !string.Equals(displayName, name, StringComparison.Ordinal))
        {
            displayName = name;
            changed = true;
        }
        if (!string.IsNullOrWhiteSpace(id) && !string.Equals(playerId, id, StringComparison.Ordinal))
        {
            playerId = id;
            changed = true;
        }
        return changed;
    }

    private bool BookPortrait => page > 0 && portrait;

    private void Update()
    {
        string nowUid = AtlasBoardRtdbMetaEconomy.CurrentUid ?? "";
        if (!string.Equals(uid, nowUid, StringComparison.Ordinal)) ResetAccount(nowUid);
        if (Time.unscaledTime < nextUiCheck) return;
        nextUiCheck = Time.unscaledTime + 0.16f;
        if (pendingLongPress >= 0 && Time.unscaledTime - longPressStarted >= 2f)
        {
            int stampIndex = pendingLongPress;
            pendingLongPress = -1;
            if (stampIndex < stamps.Count && stampIndex >= 0)
                ShowTooltip(stamps[stampIndex].id, pendingPressPosition);
        }
        if (tooltipRoot != null && tooltipRoot.gameObject.activeSelf &&
            tooltipHideAt > 0 && Time.unscaledTime >= tooltipHideAt)
            HideTooltip();
        string newLang = Language();
        if (newLang != language) { language = newLang; RebuildWhenVisible(); }
        // Attach only to the EXISTING Atlas Passport route. The Profile owns
        // the navigation and its placeholder; we hide, never duplicate them.
        // The existing Profile is DontDestroyOnLoad. Cache once rather than
        // performing a global scene search every refresh interval on Android.
        if (profileObject == null)
            profileObject = GameObject.Find("AtlasBoard_FullScreen_Profile_Route");
        var profile = profileObject;
        if (profile == null || !profile.activeInHierarchy) return;
        bool identityChanged = SyncCurrentProfileIdentity();
        if (identityChanged && page == 0 && uiRoot != null) PaintPage();
        RectTransform root = FindStage(profile.transform);
        if (root == null || !root.gameObject.activeInHierarchy) return;
        Transform placeholder = root.Find("PassportCover");
        if (placeholder == null) { if (stage != null && stage != root) stage = null; return; }
        if (uiRoot == null || stage != root || uiRoot.parent != root)
        {
            stage = root;
            var details = stage.Find("PassportDetails");
            placeholder.gameObject.SetActive(false);
            if (details != null) details.gameObject.SetActive(false);
            Build();
            if (!string.IsNullOrEmpty(uid) && !refreshing) _ = RefreshAsync();
        }
        if (loaded && !busy && !viewSaving && !refreshing && Time.unscaledTime >= nextRefresh)
            _ = RefreshAsync();
    }
    private static RectTransform FindStage(Transform container)
    {
        var all = container.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < all.Length; i++) if (all[i].name == "ProfileStage") return all[i];
        return null;
    }
    private void RebuildWhenVisible()
    {
        if (uiRoot == null || stage == null) return;
        uiRoot.gameObject.SetActive(false);
        Destroy(uiRoot.gameObject);
        uiRoot = null;
        Build();
    }
    private static async Task<string> AuthTokenAsync(string forUid)
    {
        string token = await AtlasBoardRtdbMetaEconomy.GetCurrentCallableTokenAsync();
        if (string.IsNullOrEmpty(token) && !AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
        {
            try
            {
                FirebaseUser u = FirebaseAuth.DefaultInstance.CurrentUser;
                if (u != null && u.UserId == forUid) token = await u.TokenAsync(false);
            }
            catch { /* Steam/REST providers may not have a native auth user. */ }
        }
        return token;
    }
    private static async Task<HttpReply> RequestAsync(string token, string path, string method,
        string body = null, string ifMatch = null, bool readEtag = false)
    {
        using (var req = new UnityWebRequest(Database + "/" + path + ".json?auth=" + Esc(token), method))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            if (readEtag) req.SetRequestHeader("X-Firebase-ETag", "true");
            if (!string.IsNullOrEmpty(ifMatch)) req.SetRequestHeader("if-match", ifMatch);
            if (body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.timeout = 20;
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(req);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            return new HttpReply { code = req.responseCode,
                body = req.downloadHandler != null ? req.downloadHandler.text : "",
                etag = req.GetResponseHeader("ETag") ?? "" };
        }
    }
    private static JObject ParseObject(string text)
    {
        try { return string.IsNullOrWhiteSpace(text) || text.Trim() == "null" ? new JObject() : JObject.Parse(text); }
        catch { return new JObject(); }
    }
    private static bool IsProofId(string id) => id != null && id.Length == 32 &&
        id.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
    private static int Number(JToken node) { try { return Math.Max(0, (int?)node ?? 0); } catch { return 0; } }
    private static string Word(JToken node) => (string)node ?? "";
    private bool IsQualified(Stamp stamp, JObject claim)
    {
        return claim != null && Number(claim[stamp.metric]) >= stamp.min &&
            (stamp.map == "*" || stamp.map == Word(claim["mapId"])) &&
            (!stamp.win || Word(claim["result"]) == "win");
    }
    private void AssignCatalog(JObject data)
    {
        stamps.Clear();
        foreach (var p in data.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (!(p.Value is JObject node)) continue;
            string metric = Word(node["metric"]), map = Word(node["map"]);
            if (metric != "rounds" && metric != "turns" && metric != "properties" &&
                metric != "developments" && metric != "diceRolls" && metric != "doublesRolled" &&
                metric != "netWorth" && metric != "cash") continue;
            var item = new Stamp { id = p.Name, metric = metric, map = map,
                min = Number(node["min"]), win = (bool?)node["win"] == true,
                title = Word(node["title"]), titles = node["titles"] as JObject,
                descriptions = node["descriptions"] as JObject };
            if (item.min >= 1 && item.id.Length <= 60) stamps.Add(item);
        }
    }
    private void AssignClaims(JObject claims)
    {
        proofIds.Clear(); bestProgress.Clear();
        // Exact UID and immutable witnessed careerClaims are the only source;
        // this local check is merely a preview. RTDB Rules independently
        // validates the actual claim at the one-time ownership write.
        foreach (var entry in claims.Properties().OrderByDescending(p => p.Name, StringComparer.Ordinal))
        {
            if (!IsProofId(entry.Name) || !(entry.Value is JObject proof) ||
                Word(proof["uid"]) != uid || Word(proof["matchId"]) != entry.Name) continue;
            foreach (Stamp stamp in stamps)
            {
                if (stamp.map != "*" && stamp.map != Word(proof["mapId"])) continue;
                if (stamp.win && Word(proof["result"]) != "win") continue;
                int progress = Number(proof[stamp.metric]);
                if (!bestProgress.TryGetValue(stamp.id, out int best) || progress > best)
                    bestProgress[stamp.id] = progress;
                if (!proofIds.ContainsKey(stamp.id) && IsQualified(stamp, proof))
                    proofIds[stamp.id] = entry.Name;
            }
        }
    }
    private void AssignPassport(JObject data, bool applyRemoteView)
    {
        owned.Clear(); pages.Clear(); placementHistory.Clear();
        if (data["owned"] is JObject ownedEntries)
            foreach (var p in ownedEntries.Properties())
                if (p.Value is JObject && stamps.Exists(s => s.id == p.Name)) owned.Add(p.Name);
        if (data["pages"] is JObject savedPages)
            foreach (var property in savedPages.Properties())
            {
                if (!int.TryParse(property.Name, out int index) || index < 1 || index >= TotalPages) continue;
                if (!(property.Value is JObject slots)) continue;
                var values = new[] { Word(slots["s0"]), Word(slots["s1"]) };
                for (int i = 0; i < values.Length; i++) if (!owned.Contains(values[i])) values[i] = "";
                pages[index] = values;
            }
        if (data["placementHistory"] is JObject history)
            foreach (var weekEntry in history.Properties())
            {
                // The original week-only receipt is immutable. New receipts
                // live at <week>/<stampId>/p or r alongside any old i/p/s/t.
                if (!long.TryParse(weekEntry.Name, NumberStyles.None,
                    CultureInfo.InvariantCulture, out long week) ||
                    !(weekEntry.Value is JObject weekNode)) continue;
                if (weekNode["i"] != null)
                    ReadPlacement(week, weekNode, null);
                foreach (var stampEntry in weekNode.Properties())
                {
                    if (!(stampEntry.Value is JObject actions) ||
                        !stamps.Exists(x => x.id == stampEntry.Name)) continue;
                    if (actions["r"] is JObject removed)
                        ReadPlacement(week, removed, stampEntry.Name);
                    if (actions["p"] is JObject pressed)
                        ReadPlacement(week, pressed, stampEntry.Name);
                }
            }
        // UTC receipt timestamps order a remove followed by a new press.
        // For old records without timestamps, the week start is the only
        // known time. A tie replays removal before placement.
        foreach (Placement placement in placementHistory
            .OrderBy(x => x.atMs > 0 ? x.atMs : x.week)
            .ThenBy(x => x.remove ? 0 : 1))
        {
            var slots = PageSlots(placement.page);
            if (placement.remove)
            {
                if (slots[placement.slot] == placement.stampId) slots[placement.slot] = "";
            }
            else
            {
                // A defensive read guard; the server grants NO new ownership.
                if (!pages.Values.Any(a => a[0] == placement.stampId || a[1] == placement.stampId))
                    slots[placement.slot] = placement.stampId;
            }
            pages[placement.page] = slots;
        }
        // Background polling must not jump the user back to an older saved page
        // while they are browsing or saving their current orientation.
        if (applyRemoteView && data["view"] is JObject view)
        {
            page = Mathf.Clamp(Number(view["page"]), 0, TotalPages - 1);
            portrait = Word(view["orientation"]) == "portrait";
        }
    }
    private void ReadPlacement(long week, JObject node, string expectedStamp)
    {
        int placedPage = Number(node["p"]);
        int encodedSlot = (int?)node["s"] ?? 2;
        bool removing = encodedSlot < 0;
        int placedSlot = removing ? -encodedSlot - 1 : encodedSlot;
        string stampId = Word(node["i"]);
        if (expectedStamp != null && stampId != expectedStamp) return;
        if (placedPage < 1 || placedPage >= TotalPages || placedSlot < 0 ||
            placedSlot > 1 || !owned.Contains(stampId)) return;
        placementHistory.Add(new Placement { week=week, stampId=stampId,
            atMs=(long?)node["t"] ?? 0L, page=placedPage, slot=placedSlot,
            remove=removing });
    }
    private static long CurrentWeekStart()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return now - (now - MondayEpochOffset) % WeekMilliseconds;
    }
    // A stamp pressed THIS week cannot be moved/removed before next Monday.
    // A removal from a previous week's position leaves this week's press
    // available, allowing a remove-then-replace flow for that stamp.
    private bool StampActionUsedThisWeek(string stampId, bool removing)
    {
        if (string.IsNullOrEmpty(stampId)) return false;
        long week = CurrentWeekStart();
        return placementHistory.Any(x => x.week == week && x.stampId == stampId &&
            (!x.remove || removing));
    }
    private string NextStampDate()
    {
        var next = DateTimeOffset.FromUnixTimeMilliseconds(CurrentWeekStart() + WeekMilliseconds);
        return next.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) + " UTC";
    }
    private string WeeklyActionHint(string stampId)
    {
        var stamp = stamps.Find(x => x.id == stampId);
        string name = stamp != null ? Title(stamp) : stampId;
        return name + "\n" + Local(
            "This stamp was already pressed this week. You can move or press THIS stamp again on ",
            "Bu damgayı bu hafta zaten bastın. Yalnız bu damgayı yeniden basma veya taşıma tarihi: ",
            "Ya estampaste este sello esta semana. Podrás moverlo de nuevo el ",
            "Ce tampon a déjà été apposé cette semaine. Prochain changement le ",
            "Dieser Stempel wurde diese Woche gesetzt. Nächste Änderung am ",
            "이번 주에 이미 찍은 도장입니다. 다시 찍을 수 있는 날짜: ",
            "Этот штамп уже поставлен на этой неделе. Следующая дата: ") + NextStampDate();
    }

    private async Task RefreshAsync()
    {
        if (refreshing || busy || string.IsNullOrEmpty(uid)) return;
        int g = generation; string id = uid;
        int startedRevision = mutationRevision;
        refreshing = true;
        if (!loaded) message = Local("Loading verified stamps...", "Doğrulanmış damgalar yükleniyor...",
            "Cargando sellos...", "Chargement des tampons...", "Stempel werden geladen...", "스탬프 불러오는 중...", "Загрузка штампов...");
        if (!loaded) Draw();
        try
        {
            string auth = await AuthTokenAsync(id);
            if (!Same(g, id) || string.IsNullOrWhiteSpace(auth)) return;
            var catalogReply = await RequestAsync(auth, "passportStampCatalog", "GET");
            if (!Same(g, id)) return;
            if (catalogReply.code != 200) { message = Local("Unable to load the stamp catalogue",
                "Damga kataloğu yüklenemedi", "No se puede cargar el catálogo", "Catalogue indisponible",
                "Stempelkatalog nicht verfügbar", "스탬프 목록을 불러올 수 없습니다", "Не удалось загрузить каталог"); return; }
            JObject catalog = ParseObject(catalogReply.body);
            if (catalog.Properties().Count() != StampCount) { message = Local("New passport stamps are not published yet",
                "Yeni pasaport damgaları henüz yayımlanmadı", "Los sellos aún no están disponibles",
                "Les nouveaux tampons ne sont pas encore publiés", "Neue Passstempel noch nicht veröffentlicht",
                "새 여권 스탬프가 아직 공개되지 않았습니다", "Новые штампы ещё не опубликованы"); return; }
            var ownedReply = await RequestAsync(auth, "users/" + Esc(id) + "/atlasPassport", "GET");
            if (!Same(g, id)) return;
            if (ownedReply.code != 200) { message = "PASSPORT_READ_" + ownedReply.code; return; }
            var profileReply = await RequestAsync(auth, "users/" + Esc(id), "GET");
            if (!Same(g, id)) return;
            if (profileReply.code == 200)
            {
                var identity = ParseObject(profileReply.body);
                // Fallback for a valid UID without a ready Profile label.
                // Never overwrite the canonical Profile identity with an empty
                // or outdated value returned by the broader RTDB user read.
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = Word(identity["displayName"]).Trim();
                if (string.IsNullOrWhiteSpace(playerId))
                    playerId = Word(identity["playerId"]).Trim();
            }
            var claimsReply = await RequestAsync(auth, "careerClaims/" + Esc(id), "GET");
            if (!Same(g, id)) return;
            if (claimsReply.code != 200) { message = "VERIFIED_CLAIMS_READ_" + claimsReply.code; return; }
            // A claim or placement completed while this network read was in
            // flight. Never let an earlier snapshot hide a freshly stamped seal.
            if (startedRevision != mutationRevision) { nextRefresh = 0; return; }
            SyncCurrentProfileIdentity();
            AssignCatalog(catalog);
            bool previousBookPortrait = BookPortrait;
            AssignPassport(ParseObject(ownedReply.body), !loaded && !viewDirty && !viewSaving);
            if (previousBookPortrait != BookPortrait && uiRoot != null) RebuildWhenVisible();
            AssignClaims(ParseObject(claimsReply.body));
            loaded = true;
            nextRefresh = Time.unscaledTime + 35f;
            message = Local("Verified passport ready", "Doğrulanmış pasaport hazır", "Pasaporte verificado listo",
                "Passeport vérifié prêt", "Bestätigter Pass bereit", "인증된 여권 준비 완료", "Подтверждённый паспорт готов");
        }
        catch (Exception e) { if (Same(g, id)) message = "PASSPORT_NETWORK_" + e.GetType().Name; }
        finally
        {
            if (Same(g, id))
            {
                refreshing = false;
                // Network/catalogue failures must never cause a request loop
                // on every UI tick. Keep the last verified page visible.
                if (nextRefresh <= Time.unscaledTime) nextRefresh = Time.unscaledTime + 10f;
                Draw();
            }
        }
    }
    private async Task ClaimAsync()
    {
        Stamp selected = stamps.Find(s => s.id == selectedId);
        if (selected == null || !loaded || busy || !proofIds.TryGetValue(selectedId, out string proofId) || owned.Contains(selectedId)) return;
        int g = generation; string id = uid; busy = true; Draw();
        try
        {
            string auth = await AuthTokenAsync(id); if (!Same(g,id) || string.IsNullOrEmpty(auth)) return;
            var payload = new JObject { ["proofId"] = proofId }.ToString(Formatting.None);
            var result = await RequestAsync(auth, "users/"+Esc(id)+"/atlasPassport/owned/"+Esc(selectedId), "PUT", payload);
            if (!Same(g,id)) return;
            if (result.code == 200) { owned.Add(selectedId); mutationRevision++; message = Local("Stamp earned! Choose a page slot.",
                "Damga kazanıldı! Sayfa yuvası seç.", "¡Sello ganado! Elige un espacio.", "Tampon gagné ! Choisissez une case.",
                "Stempel verdient! Wähle ein Feld.", "스탬프 획득! 칸을 선택하세요.", "Штамп получен! Выберите место.");
                OpenDialog(Local("STAMP EARNED", "DAMGA KAZANILDI", "SELLO CONSEGUIDO",
                    "TAMPON GAGNÉ", "STEMPEL ERHALTEN", "스탬프 획득", "ШТАМП ПОЛУЧЕН"),
                    Title(selected) + "\n" + Local("This collectible has been added to your passport inventory.",
                    "Bu damga pasaport envanterine eklendi.", "El sello está en tu inventario.",
                    "Ce tampon est dans votre inventaire.", "Der Stempel ist jetzt im Inventar.",
                    "스탬프가 여권 보관함에 추가되었습니다.", "Штамп добавлен в инвентарь."), null);
            }
            else { message = result.code == 401 || result.code == 403 ?
                Local("Claim rejected: check verified match and rules", "Damga reddedildi: doğrulanmış maç ve kuralları kontrol et",
                    "Reclamo rechazado", "Réclamation refusée", "Anspruch abgelehnt", "획득이 거부됐습니다", "Получение отклонено") :
                "STAMP_CLAIM_HTTP_" + result.code; }
        }
        catch (Exception e) { if (Same(g,id)) message = "STAMP_CLAIM_" + e.GetType().Name; }
        finally { if (Same(g,id)) { busy = false; Draw(); } }
    }
    private string[] PageSlots(int current)
    {
        if (!pages.TryGetValue(current, out var values)) return new[] { "", "" };
        return new[] { values[0], values[1] };
    }
    private bool PlacedAnywhere(string stampId)
    {
        return pages.Values.Any(a => a[0] == stampId || a[1] == stampId);
    }
    private void ConfirmSlotAction(bool remove)
    {
        if (!loaded || busy || refreshing || selectedSlot < 0 || selectedSlot > 1 ||
            page < 1 || page >= TotalPages) return;
        string stamp = remove ? PageSlots(page)[selectedSlot] : selectedId;
        if (string.IsNullOrEmpty(stamp)) return;
        if (StampActionUsedThisWeek(stamp, remove))
        {
            OpenDialog(Local("WEEKLY LIMIT", "HAFTALIK SINIR", "LÍMITE SEMANAL",
                "LIMITE HEBDOMADAIRE", "WOCHENLIMIT", "주간 제한", "НЕДЕЛЬНЫЙ ЛИМИТ"),
                WeeklyActionHint(stamp), null);
            return;
        }
        if (!remove && (!owned.Contains(stamp) || PlacedAnywhere(stamp)))
        {
            message = Local("Stamp already placed or not owned", "Damga zaten basılı veya kazanılmamış",
                "Sello colocado o no adquirido", "Tampon déjà posé ou indisponible",
                "Stempel bereits platziert oder nicht erworben", "이미 찍었거나 소유하지 않은 스탬프",
                "Штамп уже размещён или не получен"); Draw(); return;
        }
        int confirmedPage = page, confirmedSlot = selectedSlot;
        string confirmedStamp = stamp;
        OpenDialog(remove ? Local("REMOVE STAMP?", "DAMGA KALDIRILSIN MI?", "¿QUITAR SELLO?",
                "RETIRER LE TAMPON ?", "STEMPEL ENTFERNEN?", "스탬프 삭제?", "УДАЛИТЬ ШТАМП?") :
            Local("PRESS STAMP?", "DAMGA BASILSIN MI?", "¿PONER SELLO?",
                "APPLIQUER LE TAMPON ?", "STEMPEL AUFDRÜCKEN?", "스탬프 찍기?", "ПОСТАВИТЬ ШТАМП?"),
            Title(stamps.Find(s => s.id == confirmedStamp) ?? new Stamp { title=confirmedStamp }) + "\n" +
            Local("Each stamp can be pressed once per UTC week. Other stamps are free to place. A stamp pressed this week cannot be removed until next week.",
                "Her damgayı bir UTC haftasında bir kez basabilirsin. Diğer damgaları hemen basabilirsin. Bu hafta bastığın damgayı gelecek hafta kaldırabilirsin.",
                "Cada sello se puede estampar una vez por semana UTC. Los demás sellos no tienen restricciones.",
                "Chaque tampon peut être apposé une fois par semaine UTC. Les autres restent disponibles.",
                "Jeder Stempel kann einmal pro UTC-Woche gesetzt werden. Andere Stempel bleiben verfügbar.",
                "도장마다 UTC 기준 주 1회 찍을 수 있습니다. 다른 도장은 자유롭게 찍을 수 있습니다.",
                "Каждый штамп можно поставить раз в неделю UTC. Остальные штампы доступны."),
            () => _ = SetSlotAsync(remove, confirmedPage, confirmedSlot, confirmedStamp));
    }
    private async Task SetSlotAsync(bool remove, int targetPage, int targetSlot, string stamp)
    {
        if (!loaded || busy || refreshing || StampActionUsedThisWeek(stamp, remove) ||
            page != targetPage || selectedSlot != targetSlot ||
            targetPage < 1 || targetPage >= TotalPages || targetSlot < 0 || targetSlot > 1 ||
            !owned.Contains(stamp)) return;
        string[] slots = PageSlots(targetPage);
        if (remove ? slots[targetSlot] != stamp : PlacedAnywhere(stamp)) return;
        long week = CurrentWeekStart();
        int g = generation; string id = uid;
        busy = true; Draw();
        try
        {
            string auth = await AuthTokenAsync(id);
            if (!Same(g,id) || string.IsNullOrEmpty(auth)) return;
            // Existing week-only receipts remain immutable and readable.
            // Distinct stamp-owned paths prevent one stamp from blocking another.
            // A same-week removal may be followed by exactly one new press.
            string path = "users/" + Esc(id) + "/atlasPassport/placementHistory/" +
                week.ToString(CultureInfo.InvariantCulture) + "/" + Esc(stamp) +
                (remove ? "/r" : "/p");
            var data = new JObject { ["i"] = stamp, ["p"] = targetPage,
                ["s"] = remove ? -(targetSlot + 1) : targetSlot,
                ["t"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }.ToString(Formatting.None);
            var result = await RequestAsync(auth, path, "PUT", data, ifMatch:"null_etag");
            if (!Same(g,id)) return;
            if (result.code == 412 || result.code == 401 || result.code == 403)
            {
                // A concurrent device may have changed THIS stamp's weekly state.
                // Refresh the server history; do not block unrelated stamps.
                message = Local("This stamp's weekly action was used or rejected by the server",
                    "Bu damgaya ait haftalık işlem kullanılmış veya sunucu reddetmiş",
                    "La acción semanal de este sello fue rechazada",
                    "Action hebdomadaire de ce tampon refusée",
                    "Wochenaktion für diesen Stempel verweigert",
                    "이 도장의 주간 작업이 거부되었습니다",
                    "Недельное действие этого штампа отклонено");
                nextRefresh = 0;
                return;
            }
            if (result.code != 200) { message = "PASSPORT_STAMP_HTTP_" + result.code; return; }
            placementHistory.Add(new Placement { week=week, stampId=stamp,
                page=targetPage, slot=targetSlot, remove=remove,
                atMs=(long?)JObject.Parse(data)["t"] ?? 0L });
            slots[targetSlot] = remove ? "" : stamp;
            pages[targetPage] = slots;
            mutationRevision++;
            message = remove ? Local("Stamp removed; you may re-stamp it this week",
                "Damga kaldırıldı; bu hafta yeniden basabilirsin",
                "Sello quitado; puedes volver a estamparlo", "Tampon retiré : vous pouvez le reposer",
                "Stempel entfernt; erneut setzen möglich", "도장을 제거했습니다. 이번 주 다시 찍을 수 있습니다",
                "Штамп снят; можно поставить снова") :
                Local("Stamp pressed. Other stamps remain available",
                    "Damga basıldı. Diğer damgaları basabilirsin",
                    "Sello puesto. Otros sellos disponibles", "Tampon apposé. Autres tampons disponibles",
                    "Stempel gesetzt. Andere Stempel verfügbar", "도장을 찍었습니다. 다른 도장도 찍을 수 있습니다",
                    "Штамп поставлен. Другие штампы доступны");
        }
        catch (Exception e)
        {
            if (Same(g,id))
            {
                message = "PASSPORT_STAMP_" + e.GetType().Name;
                // An ambiguous network failure may still have committed the
                // immutable server receipt. Re-read before any further action.
                nextRefresh = 0;
            }
        }
        finally { if (Same(g,id)) { busy = false; Draw(); } }
    }
    private void GoToPage(int amount)
    {
        if (busy) return;
        int previousPage = page;
        page = Mathf.Clamp(page + amount, 0, TotalPages - 1);
        if (page == previousPage) return;
        selectedSlot = -1;
        // Page zero is always landscape, even if the stamp pages were rotated.
        // Rebuild on the 0/1 boundary to restore the preferred stamp view.
        if (portrait && (previousPage == 0 || page == 0)) RebuildWhenVisible();
        else Draw();
        QueueViewSave();
    }
    private void FlipOrientation()
    {
        if (busy || page == 0) return;
        portrait = !portrait;
        RebuildWhenVisible(); QueueViewSave();
    }
    private void QueueViewSave()
    {
        viewDirty = true;
        if (!viewSaving && loaded) _ = SaveViewAsync();
    }
    private async Task SaveViewAsync()
    {
        if (viewSaving || !loaded || string.IsNullOrEmpty(uid)) return;
        int g = generation; string id = uid; viewSaving = true;
        try
        {
            string auth = await AuthTokenAsync(id);
            if (!Same(g,id) || string.IsNullOrWhiteSpace(auth)) return;
            // Serialize preference updates; an older page request cannot win
            // after a newer click has already been queued locally.
            while (viewDirty && Same(g,id))
            {
                viewDirty = false;
                var data = new JObject { ["page"] = page,
                    ["orientation"] = portrait ? "portrait" : "landscape" };
                var result = await RequestAsync(auth,"users/"+Esc(id)+"/atlasPassport/view",
                    "PUT",data.ToString(Formatting.None));
                if (result.code != 200 || !Same(g,id)) return;
            }
        }
        catch { /* preference-only; navigation remains usable without connectivity */ }
        finally { if (Same(g,id)) viewSaving = false; }
    }
    private string Title(Stamp stamp) => StampLocalized(stamp.titles, stamp.title);
    private string Description(Stamp stamp) => StampLocalized(stamp.descriptions, "");
    private string StampLocalized(JObject translations, string fallback)
    {
        string local = Word(translations?[language]);
        if (!string.IsNullOrWhiteSpace(local)) return local;
        string en = Word(translations?["en"]);
        return string.IsNullOrWhiteSpace(en) ? fallback : en;
    }
    private RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size, Color color, bool raycast=false)
    {
        var go = new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
        rect.pivot = new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
        var image=go.GetComponent<Image>(); image.color=color;image.raycastTarget=raycast;
        return rect;
    }
    private TMP_Text Label(Transform parent,string name,string value,Vector2 pos,Vector2 size,int fontSize,Color color,
        TextAlignmentOptions alignment=TextAlignmentOptions.Center,bool bold=false)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));
        var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);
        rect.anchoredPosition=pos;rect.sizeDelta=size;
        var t=go.GetComponent<TextMeshProUGUI>();t.text=value;t.fontSize=fontSize;t.color=color;
        t.alignment=alignment;t.enableAutoSizing=true;t.fontSizeMin=12;t.fontSizeMax=fontSize;
        t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Ellipsis;
        t.raycastTarget=false;if(bold)t.fontStyle=FontStyles.Bold;
        return t;
    }
    private Button Push(Transform parent,string name,string title,Vector2 position,Vector2 size,Color color,Action onclick,int font=18)
    {
        var rect=Box(parent,name,position,size,color,true);var button=rect.gameObject.AddComponent<Button>();
        button.targetGraphic=rect.GetComponent<Image>();button.onClick.AddListener(()=>onclick?.Invoke());
        Label(rect,"Text",title,Vector2.zero,size-new Vector2(10f,6f),font,Color.white);
        return button;
    }
    private static void AddEvent(EventTrigger trigger, EventTriggerType type,
        UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }
    private void RegisterStampTooltip(Button button, int listIndex, bool absoluteIndex = false)
    {
        var trigger = button.gameObject.AddComponent<EventTrigger>();
        trigger.triggers = new List<EventTrigger.Entry>();
        AddEvent(trigger, EventTriggerType.PointerEnter, e =>
        {
            if (Application.isMobilePlatform) return;
            int index = absoluteIndex ? listIndex : listPage*ListPageSize + listIndex;
            if (index < stamps.Count && e is PointerEventData pointer)
                ShowTooltip(stamps[index].id, pointer.position);
        });
        AddEvent(trigger, EventTriggerType.PointerExit, e =>
        {
            if (!Application.isMobilePlatform) HideTooltip();
            pendingLongPress = -1;
        });
        AddEvent(trigger, EventTriggerType.PointerDown, e =>
        {
            if (!Application.isMobilePlatform || !(e is PointerEventData pointer)) return;
            int index = absoluteIndex ? listIndex : listPage*ListPageSize + listIndex;
            if (index >= stamps.Count) return;
            pendingLongPress = index;
            longPressStarted = Time.unscaledTime;
            pendingPressPosition = pointer.position;
        });
        AddEvent(trigger, EventTriggerType.PointerUp, e =>
        {
            pendingLongPress = -1;
            if (tooltipRoot != null && tooltipRoot.gameObject.activeSelf)
                tooltipHideAt = Time.unscaledTime + 2.5f;
        });
    }
    private void ShowTooltip(string stampId, Vector2 screenPosition)
    {
        if (tooltipRoot == null || uiRoot == null) return;
        Stamp stamp = stamps.Find(s => s.id == stampId);
        if (stamp == null) return;
        hoveredId = stampId;
        tooltipHideAt = 0;
        tooltipRoot.gameObject.SetActive(true);
        tooltipRoot.SetAsLastSibling();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(uiRoot, screenPosition,
            uiRoot.GetComponentInParent<Canvas>()?.worldCamera, out Vector2 localPoint);
        float x = Mathf.Clamp(localPoint.x - 170f, -uiRoot.sizeDelta.x/2f + 258f,
            uiRoot.sizeDelta.x/2f - 258f);
        float y = Mathf.Clamp(localPoint.y + 135f, -uiRoot.sizeDelta.y/2f + 122f,
            uiRoot.sizeDelta.y/2f - 122f);
        tooltipRoot.anchoredPosition = new Vector2(x, y);
        tooltipTitle.text = Title(stamp);
        tooltipObjective.text = Objective(stamp);
        int progress = owned.Contains(stampId) ? stamp.min :
            (bestProgress.TryGetValue(stampId, out int reached) ? reached : 0);
        float fraction = stamp.min > 0 ? Mathf.Clamp01((float)progress / stamp.min) : 0f;
        tooltipProgress.text = Math.Min(progress, stamp.min) + " / " + stamp.min +
            (owned.Contains(stampId) ? "  " + Local("OWNED", "SAHİP", "TUYO", "ACQUIS",
                "BESITZ", "보유", "ЕСТЬ") : "");
        float width = 430f * fraction;
        tooltipFill.sizeDelta = new Vector2(Math.Max(3f, width), 12f);
        tooltipFill.anchoredPosition = new Vector2(-215f + width/2f, -88f);
    }
    private void HideTooltip()
    {
        hoveredId = "";
        pendingLongPress = -1;
        tooltipHideAt = 0f;
        if (tooltipRoot != null) tooltipRoot.gameObject.SetActive(false);
    }
    private void OpenDialog(string title, string body, Action confirmed)
    {
        HideTooltip();
        if (uiRoot == null) return;
        if (dialogRoot != null) { dialogRoot.gameObject.SetActive(false); Destroy(dialogRoot.gameObject); }
        dialogRoot = Box(uiRoot, "PassportConfirmationOverlay", Vector2.zero,
            new Vector2(1490,755), new Color(.03f,.05f,.13f,.78f), true);
        dialogRoot.SetAsLastSibling();
        var panel = Box(dialogRoot, "ConfirmPanel", Vector2.zero,
            new Vector2(850,320), ColorOf("#26386F"));
        Box(panel, "ConfirmAccent", new Vector2(0,145), new Vector2(830,7), Gold);
        Label(panel, "ConfirmTitle", title, new Vector2(0,100), new Vector2(800,52),
            27, Gold, bold:true);
        Label(panel, "ConfirmBody", body, new Vector2(0,15), new Vector2(760,125),
            21, Color.white);
        if (confirmed != null)
            Push(panel, "ConfirmCancel", Local("CANCEL", "VAZGEÇ", "CANCELAR", "ANNULER",
                "ABBRECHEN", "취소", "ОТМЕНА"),new Vector2(-172,-116),
                new Vector2(245,54),ColorOf("#6952BB"), CloseDialog, 20);
        Push(panel, "ConfirmOK", confirmed == null ? "OK" : Local("CONFIRM", "ONAYLA",
            "CONFIRMAR", "CONFIRMER", "BESTÄTIGEN", "확인", "ПОДТВЕРДИТЬ"),
            new Vector2(confirmed == null ? 0 : 172,-116),new Vector2(260,54),
            ColorOf("#1E9CB7"),() => { CloseDialog(); confirmed?.Invoke(); },20);
    }
    private void CloseDialog()
    {
        if (dialogRoot != null)
        {
            dialogRoot.gameObject.SetActive(false);
            Destroy(dialogRoot.gameObject);
            dialogRoot = null;
        }
    }

    private void Build()
    {
        if(stage==null)return;
        uiRoot=Box(stage,"AtlasPassportLiveRoot",Vector2.zero,new Vector2(1490,755),ColorOf("#1B2456"));
        SyncCurrentProfileIdentity();
        bool bookPortrait = BookPortrait;
        float bookX=bookPortrait?-340f:-302f;
        // Keep BOTH orientations below the Profile route subtitle. The old
        // height/position put the top edge through the breadcrumb area.
        var bw=bookPortrait?690f:845f;var bh=565f;
        book=Box(uiRoot,"PassportBook",new Vector2(bookX,-24),new Vector2(bw,bh),Ink);
        Box(book,"PagePaper",Vector2.zero,new Vector2(bw-25,bh-25),Paper);
        Label(book,"BookHeading","ATLAS PASSPORT",new Vector2(0,bh/2-46),new Vector2(bw-65,44),27,Ink,bold:true);
        float invX=bookPortrait?365f:419f;
        float invW=bookPortrait?570f:590f;
        inventory=Box(uiRoot,"PassportStampInventory",new Vector2(invX,-52),new Vector2(invW,665),ColorOf("#27396F"));
        Label(inventory,"InventoryHeading",Local("EARNED STAMPS & OBJECTIVES","DAMGALAR VE GÖREVLER",
            "SELLOS Y OBJETIVOS","TAMPONS ET OBJECTIFS","STEMPEL UND ZIELE","스탬프와 목표","ШТАМПЫ И ЗАДАНИЯ"),
            new Vector2(0,266),new Vector2(invW-28,48),23,Gold,bold:true);
        for(int i=0;i<ListPageSize;i++)
        {
            int n=i;
            listButtons[i]=Push(inventory,"StampChoice_"+i,"",new Vector2(0,209-i*56),new Vector2(invW-42,49),
                ColorOf("#394982"),()=>{int index=listPage*ListPageSize+n;if(index<stamps.Count){selectedId=stamps[index].id;Draw();}},17);
            RegisterStampTooltip(listButtons[i], n);
        }
        listPageLabel=Label(inventory,"ListPage","",new Vector2(-110,-201),new Vector2(140,30),17,Color.white);
        Push(inventory,"PreviousList","<",new Vector2(-211,-201),new Vector2(54,32),ColorOf("#6952BB"),()=>{listPage=Math.Max(0,listPage-1);Draw();},20);
        Push(inventory,"NextList",">",new Vector2(-9,-201),new Vector2(54,32),ColorOf("#6952BB"),()=>{listPage=Math.Min(2,listPage+1);Draw();},20);
        // The old 3-line objective footer collided with buttons. Objectives and
        // verified progress are now a non-modal hover / long-press tooltip.
        messageLabel=Label(inventory,"PassportMessage","",new Vector2(0,-253),
            new Vector2(invW-35,46),15,Gold);
        claimButton=Push(inventory,"ClaimStamp",Local("CLAIM","KAZAN","COBRAR","RÉCLAMER","ABHOLEN","획득","ПОЛУЧИТЬ"),
            new Vector2(-196,-309),new Vector2(166,44),ColorOf("#1E9CB7"),()=>_ = ClaimAsync(),16);
        placeButton=Push(inventory,"PlaceStamp",Local("PRESS STAMP","DAMGA BAS","SELLAR","TAMPONNER","STEMPELN","도장 찍기","ПОСТАВИТЬ"),
            new Vector2(0,-309),new Vector2(184,44),ColorOf("#1E9CB7"),()=>ConfirmSlotAction(false),16);
        removeButton=Push(inventory,"RemoveStamp",Local("REMOVE","KALDIR","QUITAR","RETIRER","ENTFERNEN","지우기","УБРАТЬ"),
            new Vector2(197,-309),new Vector2(160,44),ColorOf("#6755B0"),()=>ConfirmSlotAction(true),16);
        previousPage=Push(uiRoot,"PreviousPage","<",new Vector2(bookX-183,-319),new Vector2(58,40),ColorOf("#6755B0"),()=>GoToPage(-1),22);
        pageLabel=Label(uiRoot,"PageIndicator","",new Vector2(bookX,-319),new Vector2(235,36),19,Color.white);
        nextPage=Push(uiRoot,"NextPage",">",new Vector2(bookX+183,-319),new Vector2(58,40),ColorOf("#6755B0"),()=>GoToPage(1),22);
        rotateButton=Push(uiRoot,"RotatePassport",Local("ROTATE PASSPORT","PASAPORTU DÖNDÜR","GIRAR","TOURNER","DREHEN","회전","ПОВЕРНУТЬ"),
            new Vector2(bookX,-360),new Vector2(290,36),ColorOf("#745EB7"),FlipOrientation,16);
        tooltipRoot = Box(uiRoot,"PassportStampTooltip",Vector2.zero,
            new Vector2(548,212),ColorOf("#132047"));
        tooltipRoot.GetComponent<Image>().raycastTarget=false;
        tooltipTitle=Label(tooltipRoot,"TooltipName","",new Vector2(0,75),
            new Vector2(506,42),24,Gold,bold:true);
        tooltipObjective=Label(tooltipRoot,"TooltipObjective","",new Vector2(0,15),
            new Vector2(500,95),17,Color.white);
        Box(tooltipRoot,"TooltipProgressTrack",new Vector2(0,-88),
            new Vector2(430,12),ColorOf("#344470"));
        tooltipFill=Box(tooltipRoot,"TooltipProgressFill",new Vector2(-215,-88),
            new Vector2(3,12),Gold);
        tooltipProgress=Label(tooltipRoot,"TooltipProgress","",new Vector2(0,-65),
            new Vector2(460,27),17,Gold);
        tooltipRoot.gameObject.SetActive(false);
        Draw();
    }
    private void PaintPage()
    {
        if(book==null)return;
        bool bookPortrait = BookPortrait;
        Transform old=book.Find("DynamicPage");if(old!=null){old.gameObject.SetActive(false);Destroy(old.gameObject);}
        float bw=book.sizeDelta.x,bh=book.sizeDelta.y;
        var content=Box(book,"DynamicPage",new Vector2(0,-24),new Vector2(bw-55,bh-120),new Color(0,0,0,0));
        if(page==0)
        {
            var avatar=Box(content,"AtlasAvatar",new Vector2(-bw*.32f,30),new Vector2(132,150),ColorOf("#B79E80"));
            string letter=string.IsNullOrWhiteSpace(displayName)?"?":
                StringInfo.GetNextTextElement(displayName.Trim()).ToUpperInvariant();
            Label(avatar,"AvatarLetter",letter,Vector2.zero,new Vector2(120,110),61,Ink,bold:true);
            Label(content,"IdentityLabel",Local("GAME PASSPORT - NOT A TRAVEL DOCUMENT","OYUN PASAPORTU - SEYAHAT BELGESİ DEĞİLDİR",
                "PASAPORTE DE JUEGO - NO OFICIAL","PASSEPORT DE JEU - NON OFFICIEL",
                "SPIELPASS - KEIN REISEDOKUMENT","게임용 여권 - 실제 문서 아님","ИГРОВОЙ ПАСПОРТ - НЕ ДОКУМЕНТ"),
                new Vector2(45,bh*.29f),new Vector2(bw-98,48),19,Ink,bold:true);
            float lineX=bookPortrait?70:118;
            Label(content,"PlayerName",Local("PLAYER NAME","OYUNCU ADI","NOMBRE","NOM DU JOUEUR","SPIELERNAME","플레이어 이름","ИМЯ ИГРОКА")+": "+
                (string.IsNullOrWhiteSpace(displayName)?"—":displayName),new Vector2(lineX,72),new Vector2(bw*.58f,62),19,Ink,TextAlignmentOptions.Left);
            Label(content,"PlayerIdentifier",Local("PLAYER ID", "OYUNCU KİMLİĞİ", "ID DE JUGADOR", "ID JOUEUR", "SPIELER-ID", "플레이어 ID", "ID ИГРОКА")+": "+
                (string.IsNullOrWhiteSpace(playerId)?"—":playerId),
                new Vector2(lineX,2),new Vector2(bw*.58f,60),17,Ink,TextAlignmentOptions.Left);
            Label(content,"DocumentIssuer",Local("ISSUER: ATLAS BOARD","DÜZENLEYEN: ATLAS BOARD","EMISOR: ATLAS BOARD",
                "ÉMETTEUR : ATLAS BOARD","AUSSTELLER: ATLAS BOARD","발행: ATLAS BOARD","ВЫДАЛ: ATLAS BOARD"),
                new Vector2(lineX,-68),new Vector2(bw*.58f,54),17,Ink,TextAlignmentOptions.Left);
            Label(content,"PageZeroNote",Local("Collect stamps by completing verified game objectives.",
                "Doğrulanmış oyun görevlerini tamamlayarak damga kazan.","Consigue sellos en partidas verificadas.",
                "Gagnez des tampons dans les parties vérifiées.","Sammle Stempel in bestätigten Spielen.",
                "검증된 게임 목표를 완료해 스탬프를 모으세요.","Получайте штампы за подтверждённые игры."),
                new Vector2(0,-bh*.31f),new Vector2(bw-65,52),18,Ink);
            return;
        }
        var values=PageSlots(page);
        for (int i=0;i<2;i++)
        {
            int slot=i;
            float x=bookPortrait?0:(i==0?-bw*.22f:bw*.22f);
            float y=bookPortrait?(i==0?bh*.14f:-bh*.18f):0;
            var btn=Push(content,"PassportStampSlot_"+i,"",new Vector2(x,y),
                new Vector2(bookPortrait?bw*.73f:bw*.40f,bookPortrait?bh*.27f:bh*.63f),
                selectedSlot==i?ColorOf("#D1B1AA"):ColorOf("#DAC8A3"),()=>{selectedSlot=slot;Draw();});
            slotButtons[i]=btn;
            if (string.IsNullOrEmpty(values[i]))
            {
                Label(btn.transform,"EmptyStampLabel",Local("EMPTY STAMP SPACE","BOŞ DAMGA ALANI","ESPACIO LIBRE",
                    "EMPLACEMENT VIDE","LEERER STEMPELPLATZ","빈 스탬프 칸","ПУСТОЕ МЕСТО"),
                    Vector2.zero,new Vector2(btn.GetComponent<RectTransform>().sizeDelta.x-20,75),17,Ink);
            }
            else
            {
                Stamp st=stamps.Find(s=>s.id==values[i]);
                int stampedIndex=stamps.FindIndex(s=>s.id==values[i]);
                if (stampedIndex >= 0) RegisterStampTooltip(btn, stampedIndex, true);
                string name=st!=null?Title(st):values[i];
                // Every stamp has its own locally generated mark. No artwork,
                // personal identity image, or official crest is stored in RTDB.
                RenderStampImprint(btn.transform, st, values[i], name, page, i, bookPortrait);
            }
        }
    }
    private enum SealKind { Round, Rectangle, Oval, Hexagon, Diamond, Ticket }
    private static readonly Dictionary<int, Sprite> sealSprites = new Dictionary<int, Sprite>();
    private static readonly Color[] SealInks = {
        ColorOf("#8A3558"), ColorOf("#315879"), ColorOf("#5C6350"),
        ColorOf("#72513A"), ColorOf("#5B4380")
    };
    private static int SealIndex(string id)
    {
        switch (id)
        {
            case "tr_horizon": return 0; case "co_trail": return 1;
            case "us_estate": return 2; case "de_arch": return 3;
            case "mx_sun": return 4; case "world_route": return 5;
            case "first_victory": return 6; case "long_voyage": return 7;
            case "property_archivist": return 8; case "dice_compass": return 9;
            case "builder_stamp": return 10; case "patient_walker": return 11;
            case "grand_ledger": return 12; case "double_crossing": return 13;
            case "tr_champion": return 14; case "co_champion": return 15;
            case "us_builder": return 16; case "de_master": return 17;
            case "mx_master": return 18; case "world_ambassador": return 19;
            case "final_harbor": return 20;
            default: return 0;
        }
    }
    // Twenty-one distinct, stable designs: six outline families, five ink
    // treatments and three types of edge wear. No official symbols are used.
    private static Sprite SealArt(int index)
    {
        if (sealSprites.TryGetValue(index, out Sprite cached)) return cached;
        const int size = 192;
        SealKind family=(SealKind)(index%6);
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
        texture.wrapMode=TextureWrapMode.Clamp;
        for (int y=0;y<size;y++) for(int x=0;x<size;x++)
        {
            float px=(x+0.5f-size*.5f)/(size*.5f);
            float py=(y+0.5f-size*.5f)/(size*.5f);
            float ax=Mathf.Abs(px),ay=Mathf.Abs(py);
            float distance;
            switch (family)
            {
                case SealKind.Rectangle: distance=Mathf.Max(ax/.94f,ay/.73f);break;
                case SealKind.Oval: distance=Mathf.Sqrt(px*px/0.91f/0.91f+py*py/.72f/.72f);break;
                case SealKind.Hexagon: distance=Mathf.Max(ax*.88f+ay*.52f,ay);break;
                case SealKind.Diamond: distance=(ax+ay*.94f)/1.16f;break;
                case SealKind.Ticket: distance=Mathf.Pow(Mathf.Pow(ax,5f)+Mathf.Pow(ay/.79f,5f),.2f);break;
                default: distance=Mathf.Sqrt(px*px+py*py);break;
            }
            float outer=Mathf.Abs(distance-.90f);
            float inner=Mathf.Abs(distance-(index%3==0?.74f:.78f));
            float body=outer<.033f?1f:(inner<.017f?.72f:0f);
            // Irregular ink wear is deterministic and affects edge pigment only.
            uint noise=(uint)(x*73856093 ^ y*19349663 ^ index*83492791);
            float wear=((noise>>4)&15)/15f;
            float opacity=body*(wear<.065f?.58f:.90f);
            texture.SetPixel(x,y,new Color(1f,1f,1f,opacity));
        }
        texture.Apply(false,true);
        Sprite sprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100f);
        sealSprites[index]=sprite;
        return sprite;
    }
    // Full name stays available on hover. Interior legends are shortened to
    // keep all glyphs INSIDE the outline, even in small portrait slots.
    private static string InteriorLegend(string name, bool round)
    {
        string value=(name??"").Trim();
        if (value.Length>40) value=value.Substring(0,40).TrimEnd();
        string[] parts=value.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
        if (!round || value.Length<=13) return WrapStampName(value);
        if (parts.Length>=2) return parts[0]+"\n"+string.Join(" ",parts.Skip(1).Take(2).ToArray());
        return value;
    }
    private string ImprintDate(int stampPage,int stampSlot,string id)
    {
        Placement latest=placementHistory.OrderBy(x=>x.atMs>0?x.atMs:x.week)
            .LastOrDefault(x=>!x.remove &&
            x.page==stampPage&&x.slot==stampSlot&&x.stampId==id);
        // Historical pre-weekly stamps have no authenticated issuance date.
        if (latest==null) return Local("EARLIER ISSUE", "ESKİ BASIM", "SELLO ANTIGUO",
            "ANCIEN TAMPON", "ÄLTERER STEMPEL", "이전 발급", "СТАРЫЙ ШТАМП");
        if (latest.atMs <= 0) return Local("EARLIER ISSUE", "ESKİ BASIM",
            "SELLO ANTIGUO", "ANCIEN TAMPON", "ÄLTERER STEMPEL", "이전 발급", "СТАРЫЙ ШТАМП");
        long timestamp=latest.atMs;
        string label=Local("STAMPED ON", "BASIM TARİHİ", "FECHA DE SELLO",
            "DATE D’APPOSITION", "STEMPELDATUM", "날인 날짜", "ДАТА ШТАМПА");
        try {return label+": "+DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.ToString("dd.MM.yyyy",CultureInfo.InvariantCulture);}
        catch {return Local("EARLIER ISSUE","ESKİ BASIM","SELLO ANTIGUO","ANCIEN TAMPON",
            "ÄLTERER STEMPEL","이전 발급","СТАРЫЙ ШТАМП");}
    }
    private void RenderStampImprint(Transform slot,Stamp stamp,string id,string fullTitle,
        int sheet,int side,bool bookPortrait)
    {
        int index=SealIndex(id), code=StableHash(id+"/"+sheet+"/"+side);
        SealKind family=(SealKind)(index%6);
        bool narrow=family==SealKind.Round||family==SealKind.Oval||
            family==SealKind.Diamond||family==SealKind.Hexagon;
        float width=bookPortrait?(narrow?124f:210f):(narrow?180f:254f);
        float height=bookPortrait?(narrow?118f:103f):(narrow?173f:151f);
        Color ink=SealInks[(index/6+index%3)%SealInks.Length];
        var impression=Box(slot,"Imprint",new Vector2((code%11)-5,bookPortrait?9:19),
            new Vector2(width,height),new Color(0,0,0,0));
        impression.localRotation=Quaternion.Euler(0,0,(code%13)-6f);
        var border=Box(impression,"AtlasSealOutline",Vector2.zero,
            new Vector2(width,height),new Color(0,0,0,0));
        border.GetComponent<Image>().sprite=SealArt(index);
        border.GetComponent<Image>().color=ink;
        float top=height*.30f;
        float middle=height*.045f;
        float bottom=-height*.265f;
        Label(impression,"StampBrand","ATLAS",new Vector2(0,top),
            new Vector2(width*(narrow?.66f:.83f),height*.19f),bookPortrait?18:23,ink,bold:true);
        Label(impression,"StampRegion",RegionCode(stamp!=null?stamp.map:""),
            new Vector2(0,middle),new Vector2(width*.55f,height*.17f),bookPortrait?16:21,ink,bold:true);
        // Properly separated three-level stack; circular lines NEVER reach
        // the lower border. Name remains legible with two wrapped rows.
        var legend=InteriorLegend(fullTitle,narrow);
        var text=Label(impression,"StampName",legend,new Vector2(0,bottom),
            new Vector2(width*(narrow?.70f:.81f),height*.31f),bookPortrait?16:19,ink,bold:true);
        text.fontSizeMin=bookPortrait?12:14;
        Label(slot,"StampImprintDate",ImprintDate(sheet,side,id),
            new Vector2(0,bookPortrait?-60f:-122f),
            new Vector2(bookPortrait?230f:310f,25f),bookPortrait?12:15,ink,bold:true);
    }
    private static string RegionCode(string map)
    {
        switch(map)
        {
            case "Turkey": return "TR";
            case "Germany": return "DE";
            case "Mexico": return "MX";
            case "USA": return "US";
            case "Colorado": return "CO";
            case "World": return "WR";
            default: return "AT";
        }
    }
    private static string WrapStampName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length<19 || !name.Contains(" ")) return name;
        int mid=name.Length/2, split=-1, best=int.MaxValue;
        for(int i=1;i<name.Length-1;i++)if(name[i]==' ' && Math.Abs(i-mid)<best)
        { best=Math.Abs(i-mid);split=i; }
        return split>0?name.Substring(0,split)+"\n"+name.Substring(split+1):name;
    }
    private static int StableHash(string s)
    {
        unchecked { uint hash=2166136261u;for(int i=0;i<s.Length;i++)hash=(hash^(uint)s[i])*16777619u;
            return (int)(hash&0x7FFFFFFF); }
    }
    private void Draw()
    {
        if(uiRoot==null)return;
        if(pageLabel!=null)pageLabel.text=Local("PAGE","SAYFA","PÁGINA","PAGE","SEITE","페이지","СТРАНИЦА")+" "+(page+1)+" / "+TotalPages;
        if(messageLabel!=null)messageLabel.text=message;
        if(listPageLabel!=null)listPageLabel.text=(listPage+1)+" / 3";
        for (int i=0;i<ListPageSize;i++)
        {
            var btn=listButtons[i];if(btn==null)continue;
            int at=listPage*ListPageSize+i;bool available=at<stamps.Count;
            btn.gameObject.SetActive(available);
            if(!available)continue;
            Stamp s=stamps[at];bool have=owned.Contains(s.id),ready=proofIds.ContainsKey(s.id);
            TMP_Text caption=btn.GetComponentInChildren<TMP_Text>(true);
            if(caption!=null)caption.text=Title(s)+"  ["+(have?Local("OWNED","SAHİP","TUYO","ACQUIS","BESITZ","보유","ЕСТЬ"):
                ready?Local("READY","HAZIR","LISTO","PRÊT","BEREIT","준비","ГОТОВО"):
                Local("LOCKED","KİLİTLİ","BLOQUEADO","VERROUILLÉ","GESPERRT","잠김","ЗАКРЫТО"))+"]";
            btn.GetComponent<Image>().color=selectedId==s.id?ColorOf("#6654B2"):ColorOf("#394982");
        }
        Stamp selected=stamps.Find(s=>s.id==selectedId);
        if(claimButton!=null)claimButton.interactable=loaded&&!busy&&!refreshing&&selected!=null&&
            !owned.Contains(selectedId)&&proofIds.ContainsKey(selectedId);
        bool selectedSpace = loaded && !busy && !refreshing && page > 0 && selectedSlot >= 0;
        // Keep eligible controls clickable after the weekly action is spent.
        // ConfirmSlotAction shows the exact next UTC Monday instead of a silent disabled button.
        if(placeButton!=null)placeButton.interactable=selectedSpace&&owned.Contains(selectedId)&&
            !PlacedAnywhere(selectedId) && PageSlots(page)[selectedSlot]=="";
        if(removeButton!=null)removeButton.interactable=selectedSpace&&
            PageSlots(page)[selectedSlot]!="";
        if(messageLabel!=null && loaded && !busy)
        {
            if (selected!=null && StampActionUsedThisWeek(selected.id, false))
                messageLabel.text=WeeklyActionHint(selected.id);
            else if(selected!=null && owned.Contains(selectedId) && !PlacedAnywhere(selectedId))
                messageLabel.text=Local("You own this stamp. Select an empty page space, then press STAMP.",
                    "Bu damga sende. Boş sayfa alanını seç, ardından DAMGA BAS'a tıkla.",
                    "Tienes este sello. Selecciona un espacio vacío y pulsa SELLAR.",
                    "Ce tampon est à vous. Choisissez une case vide, puis apposez-le.",
                    "Du besitzt den Stempel. Wähle ein leeres Feld und drücke STEMPELN.",
                    "보유한 스탬프입니다. 빈 칸을 선택하고 찍으세요.",
                    "Штамп уже ваш. Выберите пустую ячейку и поставьте штамп.");
            else if(selected!=null && owned.Contains(selectedId) && PlacedAnywhere(selectedId))
                messageLabel.text=Local("Already stamped. Select its filled space to remove it.",
                    "Bu damga zaten basılı. Kaldırmak için dolu alanı seç.",
                    "Ya colocado. Elige el espacio ocupado para quitarlo.",
                    "Déjà apposé. Choisissez la case occupée pour retirer.",
                    "Bereits platziert. Zum Entfernen das belegte Feld wählen.",
                    "이미 찍힌 스탬프입니다. 제거하려면 해당 칸을 선택하세요.",
                    "Штамп уже стоит. Для удаления выберите занятую ячейку.");
            else messageLabel.text=message;
        }
        if(hoveredId.Length>0 && tooltipRoot!=null && tooltipRoot.gameObject.activeSelf)
        {
            // Retain the floating hint through status-refresh repaints.
            Stamp hovered=stamps.Find(t=>t.id==hoveredId);
            if(hovered==null) HideTooltip();
        }
        if(previousPage!=null)previousPage.interactable=page>0;
        if(nextPage!=null)nextPage.interactable=page<TotalPages-1;
        if(rotateButton!=null)rotateButton.interactable=page>0&&!busy;
        PaintPage();
    }
    private string LocalMap(string id)
    {
        switch (id)
        {
            case "Turkey": return Local("Turkey","Türkiye","Turquía","Turquie","Türkei","튀르키예","Турция");
            case "Germany": return Local("Germany","Almanya","Alemania","Allemagne","Deutschland","독일","Германия");
            case "Mexico": return Local("Mexico","Meksika","México","Mexique","Mexiko","멕시코","Мексика");
            case "USA": return Local("United States","ABD","Estados Unidos","États-Unis","USA","미국","США");
            case "Colorado": return "Colorado";
            case "World": return Local("World","Dünya","Mundo","Monde","Welt","세계","Мир");
            default: return Local("any map","herhangi bir harita","cualquier mapa","toute carte",
                "beliebige Karte","모든 맵","любая карта");
        }
    }
    private string LocalMetric(string metric)
    {
        switch (metric)
        {
            case "rounds": return Local("rounds","tur","rondas","tours","Runden","라운드","раундов");
            case "turns": return Local("turns","hamle","turnos","tours joués","Züge","턴","ходов");
            case "properties": return Local("properties","mülk","propiedades","propriétés","Immobilien","부동산","объектов");
            case "developments": return Local("developments","geliştirme","mejoras","améliorations","Ausbauten","개발","улучшений");
            case "diceRolls": return Local("dice rolls","zar atışı","tiradas","lancers de dés","Würfe","주사위 굴림","бросков кубиков");
            case "doublesRolled": return Local("doubles","çift zar","dobles","doubles","Pasche","더블","дублей");
            case "netWorth": return Local("net worth","net servet","valor neto","valeur nette","Nettovermögen","순자산","капитал");
            case "cash": return Local("cash","nakit","efectivo","liquidités","Bargeld","현금","наличные");
            default: return metric;
        }
    }
    // A single witnessed match must satisfy the objective. A progress bar
    // is the highest SINGLE eligible match, not an additive/currency counter.
    private string Objective(Stamp s)
    {
        string mapName = LocalMap(s.map);
        string metricText;
        string amount = s.min.ToString("N0", CultureInfo.InvariantCulture);
        switch (s.metric)
        {
            case "rounds": metricText = Local("finish at least "+amount+" rounds",
                "en az "+amount+" tur tamamla", "completa al menos "+amount+" rondas",
                "terminez au moins "+amount+" manches", "schließe mindestens "+amount+" Runden ab",
                "최소 "+amount+"라운드를 완료하세요", "заверши не менее "+amount+" раундов"); break;
            case "turns": metricText = Local("play at least "+amount+" turns",
                "en az "+amount+" hamle yap", "realiza al menos "+amount+" turnos",
                "jouez au moins "+amount+" tours", "spiele mindestens "+amount+" Züge",
                "최소 "+amount+"턴을 플레이하세요", "сыграй не менее "+amount+" ходов"); break;
            case "properties": metricText = Local("finish with at least "+amount+" properties",
                "maçı en az "+amount+" mülkle bitir", "termina con al menos "+amount+" propiedades",
                "terminez avec au moins "+amount+" propriétés", "beende mit mindestens "+amount+" Immobilien",
                "부동산 "+amount+"개 이상으로 종료하세요", "заверши с минимум "+amount+" объектами"); break;
            case "developments": metricText = Local("make at least "+amount+" developments",
                "en az "+amount+" geliştirme yap", "realiza al menos "+amount+" mejoras",
                "effectuez au moins "+amount+" améliorations", "führe mindestens "+amount+" Ausbauten durch",
                "최소 "+amount+"회 개발하세요", "выполни не менее "+amount+" улучшений"); break;
            case "diceRolls": metricText = Local("roll the dice at least "+amount+" times",
                "zarı en az "+amount+" kez at", "lanza los dados al menos "+amount+" veces",
                "lancez les dés au moins "+amount+" fois", "würfle mindestens "+amount+" Mal",
                "주사위를 최소 "+amount+"번 굴리세요", "брось кубики минимум "+amount+" раз"); break;
            case "doublesRolled": metricText = Local("roll doubles at least "+amount+" times",
                "en az "+amount+" kez çift zar at", "obtén dobles al menos "+amount+" veces",
                "obtenez au moins "+amount+" doubles", "würfle mindestens "+amount+" Pasche",
                "더블을 최소 "+amount+"번 굴리세요", "выброси дубль минимум "+amount+" раз"); break;
            case "netWorth": metricText = Local("reach a net worth of at least "+amount,
                "en az "+amount+" net servete ulaş", "alcanza un patrimonio de "+amount,
                "atteignez une fortune nette de "+amount, "erreiche ein Nettovermögen von "+amount,
                "순자산 "+amount+" 이상 달성하세요", "достигни капитала не менее "+amount); break;
            case "cash": metricText = Local("finish with at least "+amount+" cash",
                "maçı en az "+amount+" nakitle bitir", "termina con al menos "+amount+" en efectivo",
                "terminez avec au moins "+amount+" en espèces", "beende mit mindestens "+amount+" Bargeld",
                "현금 "+amount+" 이상으로 종료하세요", "заверши с наличными не менее "+amount); break;
            default: metricText = Local("complete the required challenge", "görevi tamamla",
                "completa el objetivo", "terminez l'objectif", "schließe die Aufgabe ab",
                "목표를 완료하세요", "выполни задачу"); break;
        }
        string intro = s.map=="*" ? Local("In one verified online match, ",
            "Doğrulanmış tek bir online maçta ", "En una partida online verificada, ",
            "Dans une seule partie en ligne vérifiée, ", "In einem bestätigten Online-Spiel: ",
            "검증된 온라인 경기 한 판에서 ", "В одном подтверждённом сетевом матче ") :
            Local("On the "+mapName+" map, in one verified online match, ",
                mapName+" haritasında doğrulanmış tek bir online maçta ",
                "En el mapa "+mapName+", en una partida online verificada, ",
                "Sur la carte "+mapName+", dans une partie vérifiée, ",
                "Auf der Karte "+mapName+" in einem bestätigten Online-Spiel: ",
                mapName+" 맵의 검증된 온라인 경기 한 판에서 ",
                "На карте "+mapName+" в одном подтверждённом матче ");
        string result = intro + metricText + ".";
        if (s.win) result += " " + Local("You must also win that match.",
            "Ayrıca o maçı kazanmalısın.", "Además, debes ganar esa partida.",
            "Vous devez aussi gagner cette partie.", "Du musst das Spiel außerdem gewinnen.",
            "그 경기에서 승리해야 합니다.", "Кроме того, нужно победить в этом матче.");
        return result;
    }
}
