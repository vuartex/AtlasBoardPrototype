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
        public string Description;
        public string Theme;
        // Cached public catalog translations; these never affect ownership or writes.
        public Dictionary<string, string> LocalTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> LocalDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
    private string displayLanguage = "en";
    private string statusKey = "";
    private string statusDetail = "";

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
        // Presentation-only child component on the existing Museum instance;
        // no second manager, bootstrap, menu or ownership writer.
        if (GetComponent<AtlasBoardMuseumTokenVisuals>() == null)
            gameObject.AddComponent<AtlasBoardMuseumTokenVisuals>();
        // Passport is a separate collection type but shares this existing Profile runtime.
        if (GetComponent<AtlasBoardPassportFeature>() == null)
            gameObject.AddComponent<AtlasBoardPassportFeature>();
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
        statusKey = statusDetail = "";
        nextPollAt = 0;
        Notify();
    }
    private bool SameUid(string uid) => !string.IsNullOrEmpty(uid) &&
        uid == Uid && uid == (AtlasBoardRtdbMetaEconomy.CurrentUid ?? "");
    private void Update()
    {
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid ?? "";
        if (!string.Equals(uid, Uid, StringComparison.Ordinal)) ResetUid(uid);
        // Language switches only reformat cached verified catalog metadata.
        // No extra RTDB reads, local grants, or slot writes are performed.
        var language = ActiveLanguage();
        if (language != displayLanguage)
        {
            displayLanguage = language;
            for (int i = 0; i < Owned.Count; ++i) Localize(Owned[i], language);
            Message = Status(statusKey, statusDetail);
            Notify();
        }
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
        if (!string.IsNullOrWhiteSpace(catalogName)) return Clean(catalogName, 110);
        return "Atlas " + (id.StartsWith("weekly_", StringComparison.Ordinal) ? id.Substring(7) : Clean(id, 20));
    }
    private static string ActiveLanguage()
    {
        var manager = AtlasBoardLocalizationManager.Instance;
        string code = manager != null ? manager.CurrentLanguageCode : "en";
        switch ((code ?? "en").ToLowerInvariant())
        {
            case "tr": case "es": case "fr": case "de": case "ko": case "ru": return code.ToLowerInvariant();
            default: return "en";
        }
    }
    private static Dictionary<string, string> LocalFields(JToken entry, string plural, string legacy)
    {
        var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string fallback = (string)entry?[legacy] ?? "";
        if (!string.IsNullOrWhiteSpace(fallback)) texts["en"] = fallback;
        if (entry?[plural] is JObject localized)
            foreach (var p in localized.Properties())
                if (!string.IsNullOrWhiteSpace((string)p.Value)) texts[p.Name] = (string)p.Value;
        return texts;
    }
    private static string Choose(Dictionary<string, string> texts, string lang)
    {
        if (texts == null) return "";
        if (texts.TryGetValue(lang, out string localized) && !string.IsNullOrWhiteSpace(localized)) return localized;
        return texts.TryGetValue("en", out string fallback) ? fallback : "";
    }
    private static void Localize(Token token, string lang)
    {
        token.Title = DisplayTitle(token.Id, Choose(token.LocalTitles, lang));
        token.Description = Clean(Choose(token.LocalDescriptions, lang), 420);
    }
    private void SetStatus(string code, string detail = "")
    {
        statusKey = code;
        statusDetail = detail;
        Message = Status(code, detail);
    }
    private static string Status(string code, string detail)
    {
        string message;
        switch (code)
        {
            case "signIn": message = AtlasBoardL.R("Sign in to view your collection", "Koleksiyon için giriş yap", "Inicia sesión para ver la colección", "Connectez-vous pour voir la collection", "Melde dich an, um deine Sammlung zu sehen", "컬렉션을 보려면 로그인하세요", "Войдите, чтобы увидеть коллекцию"); break;
            case "loading": message = AtlasBoardL.R("Loading verified collection", "Doğrulanmış koleksiyon yükleniyor", "Cargando colección verificada", "Chargement de la collection vérifiée", "Verifizierte Sammlung wird geladen", "인증된 컬렉션을 불러오는 중", "Загрузка подтверждённой коллекции"); break;
            case "readOwnership": message = AtlasBoardL.R("Could not read collectible ownership", "Jeton sahipliği okunamadı", "No se pudo leer la colección", "Lecture de la collection impossible", "Sammlung konnte nicht gelesen werden", "보유 토큰 정보를 읽을 수 없습니다", "Не удалось прочитать данные о жетонах"); break;
            case "readSlots": message = AtlasBoardL.R("Could not load display slots", "Vitrin yuvaları yüklenemedi", "No se pudieron cargar las vitrinas", "Impossible de charger les emplacements", "Ausstellungsplätze konnten nicht geladen werden", "전시 슬롯을 불러올 수 없습니다", "Не удалось загрузить ячейки витрины"); break;
            case "empty": message = AtlasBoardL.R("No collectible tokens owned yet", "Henüz koleksiyon jetonun yok", "Aún no tienes fichas", "Vous ne possédez pas encore de jetons", "Noch keine Sammel-Token", "아직 보유한 토큰이 없습니다", "Пока нет коллекционных жетонов"); break;
            case "ready": message = AtlasBoardL.R("Select an owned token, then select a display slot", "Bir jeton seç ve vitrin yuvasını belirle", "Elige una ficha y un espacio", "Choisissez un jeton puis un emplacement", "Wähle einen Token und einen Platz", "토큰과 전시 슬롯을 선택하세요", "Выберите жетон, затем ячейку витрины"); break;
            case "unavailable": message = AtlasBoardL.R("Collection unavailable", "Koleksiyon kullanılamıyor", "Colección no disponible", "Collection indisponible", "Sammlung nicht verfügbar", "컬렉션을 이용할 수 없습니다", "Коллекция недоступна"); break;
            case "checking": message = AtlasBoardL.R("Verifying slot change", "Yuva değişikliği doğrulanıyor", "Verificando el cambio", "Vérification de l’emplacement", "Platzänderung wird geprüft", "슬롯 변경을 확인하는 중", "Проверка изменения ячейки"); break;
            case "slotUnavailable": message = AtlasBoardL.R("Slot state unavailable. Refresh and retry", "Yuva durumu alınamadı, yenileyip tekrar dene", "Estado no disponible. Actualiza y reintenta", "État indisponible. Actualisez et réessayez", "Platzstatus fehlt. Aktualisieren und erneut versuchen", "슬롯 상태가 없습니다. 새로고침 후 다시 시도하세요", "Данные ячейки недоступны. Обновите и повторите"); break;
            case "ownershipChanged": message = AtlasBoardL.R("Ownership changed; refresh required", "Sahiplik değişti, yenileme gerekli", "La propiedad cambió; actualiza", "Collection modifiée ; actualisez", "Besitz geändert; bitte aktualisieren", "보유 정보가 바뀌었습니다. 새로고침하세요", "Состав коллекции изменился; обновите"); break;
            case "conflict": message = AtlasBoardL.R("Collection changed on another device; refresh", "Koleksiyon başka cihazda değişti, yenile", "La colección cambió en otro dispositivo", "Collection modifiée sur un autre appareil", "Sammlung auf anderem Gerät geändert", "다른 기기에서 컬렉션이 변경되었습니다", "Коллекция изменена на другом устройстве"); break;
            case "rejected": message = AtlasBoardL.R("Slot change rejected by Firebase", "Firebase yuva değişikliğini reddetti", "Firebase rechazó el cambio", "Modification refusée par Firebase", "Firebase hat die Änderung abgelehnt", "Firebase가 슬롯 변경을 거부했습니다", "Firebase отклонил изменение ячейки"); break;
            case "pending": message = AtlasBoardL.R("Slot saved; waiting to verify", "Yuva kaydedildi, doğrulama bekleniyor", "Guardado; esperando verificación", "Enregistré ; vérification en attente", "Gespeichert; warte auf Prüfung", "슬롯 저장됨, 확인 대기 중", "Сохранено; ожидается проверка"); break;
            case "sync": message = AtlasBoardL.R("Display sync pending; refresh", "Vitrin eşitleniyor, yenile", "Sincronización pendiente; actualiza", "Synchronisation en attente ; actualisez", "Synchronisierung ausstehend; aktualisieren", "전시 동기화 중입니다. 새로고침하세요", "Синхронизация витрины; обновите"); break;
            case "removed": message = AtlasBoardL.R("Token removed from display", "Jeton vitrinden çıkarıldı", "Ficha retirada", "Jeton retiré", "Token entfernt", "토큰이 전시에서 제거되었습니다", "Жетон убран из витрины"); break;
            case "equipped": message = AtlasBoardL.R("Token placed in display", "Jeton vitrine yerleştirildi", "Ficha colocada", "Jeton exposé", "Token ausgestellt", "토큰이 전시되었습니다", "Жетон размещён в витрине"); break;
            case "failed": message = AtlasBoardL.R("Slot change failed", "Yuva değiştirilemedi", "Error al cambiar espacio", "Échec de la modification", "Platzänderung fehlgeschlagen", "슬롯 변경에 실패했습니다", "Не удалось изменить ячейку"); break;
            default: return "";
        }
        return string.IsNullOrWhiteSpace(detail) ? message : message + " (" + detail + ")";
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
        if (string.IsNullOrEmpty(uid)) { SetStatus("signIn"); Notify(); return; }
        if (reading || Busy || (!force && Loaded && Time.unscaledTime < nextPollAt)) return;
        reading = true;
        SetStatus("loading");
        Notify();
        try
        {
            string token = await TokenAsync(uid);
            if (!SameUid(uid) || string.IsNullOrWhiteSpace(token)) return;
            var response = await RequestAsync(uid, token, "users/" + Esc(uid) + "/collectibleTokens", "GET");
            if (!SameUid(uid)) return;
            if (response.status != 200) { SetStatus("readOwnership"); return; }
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
                    // Only server-verified claims or administrator-seeded Museum QA
                    // ownership are displayed; clients cannot grant either source.
                    string tokenId = entry.Name;
                    if (!tokenId.StartsWith("weekly_", StringComparison.Ordinal) ||
                        !(entry.Value is JObject owned) || (string)owned["tokenId"] != tokenId ||
                        ((string)owned["source"] != "weeklyMissions" &&
                         (string)owned["source"] != "museumQa")) continue;
                    string weekId = (string)owned["weekId"] ?? "";
                    if (tokenId != "weekly_" + weekId || weekId.Length != 8) continue;
                    var publicEntry = catalog?[tokenId];
                    var item = new Token { Id = tokenId, WeekId = weekId,
                        LocalTitles = LocalFields(publicEntry, "titles", "title"),
                        LocalDescriptions = LocalFields(publicEntry, "descriptions", "description"),
                        Theme = Clean((string)publicEntry?["theme"], 24) };
                    Localize(item, ActiveLanguage());
                    items.Add(item);
                }
            }
            var display = await RequestAsync(uid, token, "users/" + Esc(uid) + "/collectibleDisplay", "GET");
            if (!SameUid(uid)) return;
            if (display.status != 200) { SetStatus("readSlots"); return; }
            items.Sort((a, b) => string.CompareOrdinal(b.WeekId, a.WeekId));
            Owned.Clear();
            Owned.AddRange(items);
            string[] saved = new string[3];
            ReadDisplay(display.body, saved);
            for (int i = 0; i < 3; i++)
                Slots[i] = string.IsNullOrEmpty(saved[i]) || HasOwned(saved[i]) ? saved[i] : "";
            Loaded = true;
            SetStatus(items.Count == 0 ? "empty" : "ready");
            nextPollAt = Time.unscaledTime + 20f;
        }
        catch (Exception e)
        {
            if (SameUid(uid)) SetStatus("unavailable", e.GetType().Name);
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
        SetStatus("checking");
        Notify();
        try
        {
            string token = await TokenAsync(uid);
            if (!SameUid(uid) || string.IsNullOrWhiteSpace(token)) return;
            string path = "users/" + Esc(uid) + "/collectibleDisplay";
            var old = await RequestAsync(uid, token, path, "GET", null, null, true);
            if (!SameUid(uid)) return;
            if (old.status != 200 || string.IsNullOrEmpty(old.etag))
            { SetStatus("slotUnavailable"); return; }
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
            { SetStatus("ownershipChanged"); return; }
            var payload = new JObject {
                ["s0"] = next[0], ["s1"] = next[1], ["s2"] = next[2]
            }.ToString(Newtonsoft.Json.Formatting.None);
            var changed = await RequestAsync(uid, token, path, "PUT", payload, old.etag);
            if (!SameUid(uid)) return;
            if (changed.status == 412) { SetStatus("conflict"); return; }
            if (changed.status < 200 || changed.status >= 300)
            { SetStatus("rejected", "HTTP " + changed.status); return; }
            var verified = await RequestAsync(uid, token, path, "GET");
            if (!SameUid(uid) || verified.status != 200)
            { SetStatus("pending"); return; }
            var check = new string[3];
            ReadDisplay(verified.body, check);
            if (!check.SequenceEqual(next)) { SetStatus("sync"); return; }
            for (int i = 0; i < 3; i++) Slots[i] = next[i];
            SetStatus(string.IsNullOrEmpty(tokenId) ? "removed" : "equipped");
            Notify();
        }
        catch (Exception e)
        {
            if (SameUid(uid)) SetStatus("failed", e.GetType().Name);
        }
        finally
        {
            if (SameUid(uid)) { Busy = false; Notify(); }
        }
    }
}
