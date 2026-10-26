using System;
using System.Collections;
using System.Globalization;
using System.Threading.Tasks;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Shared Settings Server Status tab, with bounded, foreground-only checks.
/// Health indicates actual auth/RTDB reachability, not a claim that the
/// undeployed Cloud Functions lobby or permanent economy is operational.
/// Maximum two private RTDB GETs + one Firebase Auth token refresh per 60 sec
/// while the status tab is visible. Never logs tokens, passwords, emails or URLs.
/// </summary>
[DefaultExecutionOrder(16500)]
public sealed class AtlasBoardServerStatusUI : MonoBehaviour
{
    private enum Health { Unknown, Online, Limited, Offline }
    private static AtlasBoardServerStatusUI instance;
    private static readonly Color32 Background = new Color32(247, 246, 240, 255);
    private static readonly Color32 TextColor = new Color32(51, 61, 70, 255);
    private static readonly Color32 Healthy = new Color32(41, 152, 92, 255);
    private static readonly Color32 Caution = new Color32(193, 142, 40, 255);
    private static readonly Color32 Error = new Color32(184, 48, 70, 255);
    private static readonly Color32 IdleTab = new Color32(28, 157, 211, 255);
    private static readonly Color32 ActiveTab = new Color32(19, 113, 163, 255);
    private static readonly string[] LabelKeys = {
        "account", "bots", "online", "shop", "rtdb", "career" };
    private long lastUserReadHttp;
    private long lastCareerReadHttp;
    private double lastVerifiedMatchSampleAt = -1d;
    private int verifiedMatchSampleCount;
    private AtlasBoardMatchRuntimeBridge observedMatchBridge;

    private RectTransform settingsWindow;
    private Button tab;
    private RectTransform panel;
    private TMP_Text heading;
    private TMP_Text updated;
    private readonly TMP_Text[] rows = new TMP_Text[6];
    private readonly TMP_Text[] details = new TMP_Text[6];
    private readonly Image[] lights = new Image[6];
    private readonly Health[] states = new Health[6];
    private readonly Button[] settingsTabs = new Button[6];
    private readonly string[] notes = new string[6];
    private bool selected;
    private bool checking;
    private bool initialized;
    private bool wasSettingsVisible;
    private float nextBindAt;
    private float nextCheckAt;
    private float nextStatusToastAt;
    private DateTime lastCheckUtc;
    private GameObject toast;
    private TMP_Text toastText;
    private float toastUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject host = new GameObject("AtlasBoard_Server_Status_Runtime");
        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardServerStatusUI>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        states[0] = Health.Unknown;
        states[1] = Health.Online;
        states[2] = Health.Offline;
        states[3] = Health.Online;
        states[4] = Health.Unknown;
        states[5] = Health.Unknown;
        notes[0] = "auth_checking";
        notes[1] = "bots_ready";
        notes[2] = "online_missing";
        notes[3] = "shop_unverified";
        notes[4] = "not_checked";
        notes[5] = "career_not_checked";
    }

    private void OnEnable()
    {
        AtlasBoardFrontendErrorCodes.PublicCodeRaised += HandleNetworkFailure;
        AtlasBoardNetworkTelemetry.SampleReported += HandleMatchNetworkSample;
        AtlasBoardLocalizationManager.LanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        AtlasBoardFrontendErrorCodes.PublicCodeRaised -= HandleNetworkFailure;
        AtlasBoardNetworkTelemetry.SampleReported -= HandleMatchNetworkSample;
        AtlasBoardLocalizationManager.LanguageChanged -= HandleLanguageChanged;
    }

    private void HandleMatchNetworkSample(AtlasBoardNetworkTelemetrySample sample)
    {
        if (!sample.RequestWasSent || !sample.Success ||
            !string.Equals(sample.FunctionName, "matchRTDB", StringComparison.Ordinal))
            return;
        // A completed/closed match must not make the next lobby look green.
        if (observedMatchBridge == null)
            observedMatchBridge = UnityEngine.Object.FindAnyObjectByType<AtlasBoardMatchRuntimeBridge>();
        var bridge = observedMatchBridge;
        if (bridge == null || string.IsNullOrWhiteSpace(bridge.CurrentMatchId))
            return;
        lastVerifiedMatchSampleAt = sample.RealtimeAt;
        verifiedMatchSampleCount = Math.Min(verifiedMatchSampleCount + 1, 20);
        if (selected && states[2] == Health.Limited)
        {
            states[2] = Health.Online;
            notes[2] = "online_verified_match";
            RefreshLabels();
        }
    }

    private bool HasRecentVerifiedMatch()
    {
        if (observedMatchBridge == null)
            observedMatchBridge = UnityEngine.Object.FindAnyObjectByType<AtlasBoardMatchRuntimeBridge>();
        var bridge = observedMatchBridge;
        return bridge != null && !string.IsNullOrWhiteSpace(bridge.CurrentMatchId) &&
            verifiedMatchSampleCount >= 2 && lastVerifiedMatchSampleAt >= 0d &&
            Time.realtimeSinceStartupAsDouble - lastVerifiedMatchSampleAt < 12d;
    }

    private void HandleLanguageChanged()
    {
        RefreshLabels();
        if (toast != null && toast.activeSelf) UpdateToastText();
    }

    private void OnDestroy()
    {
        AtlasBoardFrontendErrorCodes.PublicCodeRaised -= HandleNetworkFailure;
        if (instance == this) instance = null;
    }

    private void HandleNetworkFailure(string code, string message)
    {
        // Show a brief, non-blocking summary when a real frontend network
        // failure occurs. Never start extra 404 polling or expose error detail.
        if (code != "ATB110" && code != "ATB202") return;
        if (Time.unscaledTime < nextStatusToastAt) return;
        nextStatusToastAt = Time.unscaledTime + 90f;
        ShowTransientToast();
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        if (now >= nextBindAt)
        {
            nextBindAt = now + 1f;
            EnsureUi();
        }
        if (toast != null && toast.activeSelf && now >= toastUntil)
            toast.SetActive(false);
        bool settingsVisible = settingsWindow != null &&
            settingsWindow.gameObject.activeInHierarchy;
        if (!settingsVisible && wasSettingsVisible) LeaveServerTab();
        wasSettingsVisible = settingsVisible;
        if (selected && panel != null && panel.gameObject.activeInHierarchy &&
            !checking && now >= nextCheckAt)
        {
            nextCheckAt = now + 60f;
            _ = CheckServicesAsync();
        }
    }

    private void LateUpdate()
    {
        // All six tabs use the same canonical banner positions; no floating
        // lower-left control, no visual style override by platform.
        if (settingsWindow == null || !settingsWindow.gameObject.activeInHierarchy) return;
        ApplySixTabLayout();
        if (panel != null && panel.gameObject.activeSelf != selected)
            panel.gameObject.SetActive(selected);
    }

    private static Button FindButton(string name)
    {
        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include);
        foreach (Button b in buttons)
            if (b != null && b.gameObject.scene.IsValid() && b.name == name)
                return b;
        return null;
    }

    private void EnsureUi()
    {
        Button controls = FindButton("Tab_Controls");
        if (controls == null || controls.transform.parent == null) return;
        RectTransform window = controls.transform.parent as RectTransform;
        if (window == null) return;
        if (settingsWindow != window)
        {
            settingsWindow = window;
            tab = null;
            panel = null;
            selected = false;
            initialized = false;
            for (int i = 0; i < settingsTabs.Length; i++) settingsTabs[i] = null;
        }
        Button account = FindButton("Tab_AccountAtlasBoard");
        if (account == null) return;
        if (tab == null)
        {
            Transform found = window.Find("Tab_ServerStatusAtlasBoard");
            tab = found != null ? found.GetComponent<Button>() : null;
            if (tab == null)
            {
                GameObject clone = Instantiate(account.gameObject, window, false);
                clone.name = "Tab_ServerStatusAtlasBoard";
                tab = clone.GetComponent<Button>();
                tab.onClick.RemoveAllListeners();
                foreach (AtlasBoardLocalizedText localized in
                    clone.GetComponentsInChildren<AtlasBoardLocalizedText>(true))
                    localized.enabled = false;
                tab.onClick.AddListener(SelectServerTab);
            }
            TMP_Text label = tab.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = T("server_tab");
                label.enableAutoSizing = true;
                label.fontSizeMin = 12f;
                label.fontSizeMax = 18f;
            }
        }
        if (panel == null) BuildPanel(window);
        if (!initialized)
        {
            BindOtherTabs();
            initialized = true;
        }
        // Cache same-scene native tabs; NEVER scan all scene Buttons per frame.
        string[] names = { "Tab_Audio", "Tab_Gameplay", "Tab_Graphics",
            "Tab_Controls", "Tab_AccountAtlasBoard", "Tab_ServerStatusAtlasBoard" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform item = window.Find(names[i]);
            settingsTabs[i] = item != null ? item.GetComponent<Button>() : null;
        }
        ApplySixTabLayout();
    }

    private void BindOtherTabs()
    {
        string[] names = { "Tab_Audio", "Tab_Gameplay", "Tab_Graphics",
            "Tab_Controls", "Tab_AccountAtlasBoard" };
        foreach (string name in names)
        {
            Button button = FindButton(name);
            if (button != null) button.onClick.AddListener(LeaveServerTab);
        }
    }

    private void SelectServerTab()
    {
        selected = true;
        if (panel != null) panel.gameObject.SetActive(true);
        RefreshLabels();
        nextCheckAt = 0f;
    }

    private void LeaveServerTab()
    {
        selected = false;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    private void ApplySixTabLayout()
    {
        string[] names = { "Tab_Audio", "Tab_Gameplay", "Tab_Graphics",
            "Tab_Controls", "Tab_AccountAtlasBoard", "Tab_ServerStatusAtlasBoard" };
        Image reference = settingsTabs[3] != null
            ? settingsTabs[3].GetComponent<Image>() : null;
        for (int i = 0; i < names.Length; i++)
        {
            Button native = settingsTabs[i];
            if (native == null) continue;
            Transform tr = native.transform;
            RectTransform rect = tr as RectTransform;
            if (rect == null) continue;
            Vector2 position = new Vector2(-550f + i * 220f, 325f);
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
            if (rect.sizeDelta != new Vector2(206f, 54f))
                rect.sizeDelta = new Vector2(206f, 54f);
            if (i == 5)
            {
                Image image = tr.GetComponent<Image>();
                if (image != null)
                {
                    if (reference != null && image.sprite != reference.sprite)
                        image.sprite = reference.sprite;
                    Color wanted = selected ? ActiveTab : IdleTab;
                    if (image.color != wanted) image.color = wanted;
                }
            }
        }
    }

    private RectTransform NewPanel(Transform parent, string name,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform r = go.transform as RectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = position;
        r.sizeDelta = size;
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return r;
    }

    private TMP_Text NewText(Transform parent, string name, string value,
        Vector2 position, Vector2 size, int font, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text t = go.GetComponent<TMP_Text>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.text = value;
        t.color = color;
        t.fontSize = font;
        t.alignment = alignment;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = position;
        r.sizeDelta = size;
        return t;
    }

    private void BuildPanel(Transform window)
    {
        panel = NewPanel(window, "AtlasBoardServerStatusPanel",
            new Vector2(0f, -35f), new Vector2(1240f, 620f), Background);
        // Block the hidden sliders/dropdowns beneath this new content page.
        panel.GetComponent<Image>().raycastTarget = true;
        heading = NewText(panel, "Heading", T("server_heading"),
            new Vector2(0f, 260f), new Vector2(1080f, 42f), 28,
            TextColor, TextAlignmentOptions.Center);
        for (int i = 0; i < 6; i++)
        {
            float y = 193f - i * 72f;
            RectTransform background = NewPanel(panel, "StatusRow" + i,
                new Vector2(0f, y), new Vector2(1100f, 64f),
                i % 2 == 0 ? new Color32(228, 239, 246, 255) : new Color32(238, 241, 243, 255));
            RectTransform indicator = NewPanel(background, "Indicator",
                new Vector2(-517f, 0f), new Vector2(22f, 22f), Caution);
            lights[i] = indicator.GetComponent<Image>();
            rows[i] = NewText(background, "ServiceName", T(LabelKeys[i]),
                new Vector2(-330f, 0f), new Vector2(330f, 30f), 18,
                TextColor, TextAlignmentOptions.MidlineLeft);
            rows[i].enableAutoSizing = true;
            rows[i].fontSizeMin = 11f;
            rows[i].fontSizeMax = 18f;
            details[i] = NewText(background, "ServiceDetails", T("auth_checking"),
                new Vector2(180f, 0f), new Vector2(620f, 44f), 16,
                TextColor, TextAlignmentOptions.MidlineLeft);
            details[i].enableAutoSizing = true;
            details[i].fontSizeMin = 12f;
            details[i].fontSizeMax = 16f;
        }
        updated = NewText(panel, "LastChecked", T("not_yet_checked"),
            new Vector2(0f, -275f), new Vector2(1100f, 38f), 16,
            TextColor, TextAlignmentOptions.Center);
        panel.gameObject.SetActive(false);
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        for (int i = 0; i < 6; i++)
        {
            if (rows[i] != null) rows[i].text = T(LabelKeys[i]);
            if (details[i] != null)
            {
                string note = T(notes[i] ?? "not_checked");
                if (i == 4 && notes[i] == "rtdb_read_error") note += " (HTTP " + lastUserReadHttp + ")";
                if (i == 5 && notes[i] == "career_read_error") note += " (HTTP " + lastCareerReadHttp + ")";
                if (details[i].text != note) details[i].text = note;
            }
            if (lights[i] != null)
            {
                Color wanted = states[i] == Health.Online ? Healthy :
                    states[i] == Health.Offline ? Error : Caution;
                if (lights[i].color != wanted) lights[i].color = wanted;
            }
        }
        if (heading != null) heading.text = T("server_heading");
        if (tab != null)
        {
            TMP_Text tabLabel = tab.GetComponentInChildren<TMP_Text>(true);
            if (tabLabel != null) tabLabel.text = T("server_tab");
        }
        if (updated != null)
            updated.text = lastCheckUtc == default(DateTime)
                ? T("not_yet_checked")
                : T("last_check") + ": " + lastCheckUtc.ToLocalTime().ToString(
                    "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                  " · " + T("every_minute");
    }

    private async Task CheckServicesAsync()
    {
        if (checking) return;
        checking = true;
        try
        {
            states[1] = Health.Online;
            states[3] = Health.Online;
            // A missing Firebase user is not proof that Spark RTDB is down.
            states[2] = Health.Unknown;
            notes[2] = "online_sign_in_required";
            notes[3] = "shop_unverified";
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                states[0] = states[4] = states[5] = Health.Offline;
                states[2] = Health.Offline;
                notes[2] = "internet_unavailable";
                notes[0] = "internet_unavailable";
                notes[4] = "network_unavailable";
                notes[5] = "career_offline";
                return;
            }
            FirebaseUser user = null;
            try { user = FirebaseAuth.DefaultInstance.CurrentUser; }
            catch { }
            if (user == null)
            {
                states[0] = Health.Limited;
                states[4] = states[5] = Health.Unknown;
                notes[0] = "signed_out";
                notes[4] = "rtdb_sign_in";
                notes[5] = "career_guest";
                return;
            }
            string token;
            try
            {
                token = await user.TokenAsync(true); // actual Auth network check
            }
            catch
            {
                states[0] = states[4] = states[5] = Health.Offline;
                notes[0] = "token_failed";
                notes[4] = "not_tested_auth";
                notes[5] = "not_tested_auth";
                return;
            }
            if (string.IsNullOrEmpty(token))
            {
                states[0] = Health.Offline;
                notes[0] = "token_missing";
                return;
            }
            states[0] = Health.Online;
            notes[0] = "auth_online";
            string db = "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
            string uid = UnityWebRequest.EscapeURL(user.UserId);
            string query = "?auth=" + UnityWebRequest.EscapeURL(token);
            // Probe only the small account metadata leaf, not every user
            // preference, device and profile record on each status check.
            long accountCode = await SafeGetStatusAsync(
                db + "/users/" + uid + "/general.json" + query);
            states[4] = accountCode == 200 ? Health.Online : Health.Offline;
            lastUserReadHttp = accountCode;
            notes[4] = accountCode == 200 ? "rtdb_reachable" : "rtdb_read_error";
            // Probe the actual Spark room directory instead of displaying
            // the obsolete Functions/404 state as a hard-coded status.
            // Existence/permission probe; never download the full public
            // room directory merely to paint the status indicator.
            long roomsCode = await SafeGetStatusAsync(
                db + "/publicRoomDirectory.json" + query +
                "&orderBy=%22updatedAt%22&limitToLast=1");
            bool verifiedMatch = roomsCode == 200 && HasRecentVerifiedMatch();
            states[2] = roomsCode == 200
                ? (verifiedMatch ? Health.Online : Health.Limited)
                : Health.Offline;
            notes[2] = roomsCode == 200
                ? (verifiedMatch ? "online_verified_match" : "online_lobby_only")
                : "online_rtdb_unavailable";
            long careerCode = await SafeGetStatusAsync(db + "/careerProfiles/" + uid + ".json" + query);
            states[5] = careerCode == 200 ? Health.Limited : Health.Offline;
            lastCareerReadHttp = careerCode;
            notes[5] = careerCode == 200 ? "career_reachable" : "career_read_error";
        }
        finally
        {
            lastCheckUtc = DateTime.UtcNow;
            checking = false;
            RefreshLabels();
            if (states[2] == Health.Offline && Time.unscaledTime >= nextStatusToastAt)
            {
                nextStatusToastAt = Time.unscaledTime + 240f;
                ShowTransientToast();
            }
        }
    }

    private static async Task<long> SafeGetStatusAsync(string url)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 8;
            // Same App Check path as room transport; never bypass
            // enforcement or mislabel a missing header as RTDB outage.
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);
            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            // Never log the URL because it contains an ephemeral Firebase ID token.
            if (request.result == UnityWebRequest.Result.ConnectionError)
                return 0;
            return request.responseCode;
        }
    }

    // One game language at a time. All service states remain language-neutral
    // identifiers, so changing language does not trigger a backend request.
    private static string T(string key)
    {
        switch (key)
        {
            case "server_tab": return AtlasBoardL.R(
                "SERVER STATUS",
                "SUNUCU DURUMU",
                "ESTADO DEL SERVIDOR",
                "ÉTAT DU SERVEUR",
                "SERVERSTATUS",
                "서버 상태",
                "СОСТОЯНИЕ СЕРВЕРА");
            case "server_heading": return AtlasBoardL.R(
                "SERVER STATUS",
                "SUNUCU DURUMU",
                "ESTADO DEL SERVIDOR",
                "ÉTAT DU SERVEUR",
                "SERVERSTATUS",
                "서버 상태",
                "СОСТОЯНИЕ СЕРВЕРА");
            case "account": return AtlasBoardL.R(
                "ACCOUNT / AUTH",
                "HESAP / GİRİŞ",
                "CUENTA / ACCESO",
                "COMPTE / CONNEXION",
                "KONTO / ANMELDUNG",
                "계정 / 로그인",
                "АККАУНТ / ВХОД");
            case "bots": return AtlasBoardL.R(
                "LOCAL BOTS",
                "YEREL BOTLAR",
                "BOTS LOCALES",
                "BOTS LOCAUX",
                "LOKALE BOTS",
                "로컬 봇",
                "ЛОКАЛЬНЫЕ БОТЫ");
            case "online": return AtlasBoardL.R(
                "ONLINE MATCHES",
                "ÇEVRİMİÇİ MAÇLAR",
                "PARTIDAS EN LÍNEA",
                "PARTIES EN LIGNE",
                "ONLINE-PARTIEN",
                "온라인 경기",
                "ОНЛАЙН-МАТЧИ");
            case "shop": return AtlasBoardL.R(
                "SHOP / CATALOG",
                "MAĞAZA / KATALOG",
                "TIENDA / CATÁLOGO",
                "BOUTIQUE / CATALOGUE",
                "SHOP / KATALOG",
                "상점 / 카탈로그",
                "МАГАЗИН / КАТАЛОГ");
            case "rtdb": return AtlasBoardL.R(
                "RTDB CONNECTION",
                "RTDB BAĞLANTISI",
                "CONEXIÓN RTDB",
                "CONNEXION RTDB",
                "RTDB-VERBINDUNG",
                "RTDB 연결",
                "СОЕДИНЕНИЕ RTDB");
            case "career": return AtlasBoardL.R(
                "CAREER SYNC",
                "KARİYER EŞİTLEME",
                "SINCRONIZACIÓN DE CARRERA",
                "SYNCHRONISATION CARRIÈRE",
                "KARRIERE-SYNC",
                "커리어 동기화",
                "СИНХРОНИЗАЦИЯ КАРЬЕРЫ");
            case "auth_checking": return AtlasBoardL.R(
                "Checking authentication",
                "Hesap doğrulaması kontrol ediliyor",
                "Comprobando autenticación",
                "Vérification de connexion",
                "Anmeldung wird geprüft",
                "인증 확인 중",
                "Проверка входа");
            case "bots_ready": return AtlasBoardL.R(
                "Offline bots available; no internet needed",
                "Çevrimdışı botlar hazır; internet gerekmiyor",
                "Bots sin conexión; no requieren internet",
                "Bots hors ligne; sans internet",
                "Offline-Bots verfügbar; kein Internet nötig",
                "오프라인 봇 사용 가능",
                "Боты доступны без интернета");
            case "online_sign_in_required": return AtlasBoardL.R(
                "Sign in or choose an online room to check RTDB",
                "RTDB kontrolü için giriş yap veya çevrimiçi oda seç",
                "Inicia sesión o elige una sala en línea para comprobar RTDB",
                "Connectez-vous ou choisissez un salon pour tester RTDB",
                "Zum Prüfen von RTDB anmelden oder Online-Raum wählen",
                "RTDB 확인은 로그인 또는 온라인 방 선택 후 가능",
                "Войдите или выберите онлайн-комнату для проверки RTDB");
            case "online_missing": return AtlasBoardL.R(
                "Online match service unavailable (404)",
                "Çevrimiçi maç servisi kullanılamıyor (404)",
                "Partidas en línea no disponibles (404)",
                "Parties en ligne indisponibles (404)",
                "Online-Partien nicht verfügbar (404)",
                "온라인 경기 사용 불가 (404)",
                "Онлайн-матчи недоступны (404)");
            case "online_verified_match": return AtlasBoardL.R(
                "Live match RTDB requests verified on this client",
                "Bu cihazda canlı maç RTDB istekleri doğrulandı",
                "Solicitudes RTDB de partida verificadas en este dispositivo",
                "Requêtes RTDB du match vérifiées sur cet appareil",
                "Live-Match-RTDB auf diesem Gerät bestätigt",
                "이 기기의 실시간 매치 RTDB 요청 확인됨",
                "RTDB-запросы матча проверены на устройстве");
            case "online_lobby_only": return AtlasBoardL.R(
                "RTDB rooms reachable; synchronized matches not yet verified",
                "RTDB odaları erişilebilir; eşzamanlı maçlar henüz doğrulanmadı",
                "Salas RTDB accesibles; partidas no verificadas",
                "Salons RTDB accessibles; matchs non vérifiés",
                "RTDB-Räume erreichbar; Spiele noch ungeprüft",
                "RTDB 방 연결됨; 동기화 경기 미검증",
                "Комнаты RTDB доступны; матчи не проверены");
            case "online_rtdb_unavailable": return AtlasBoardL.R(
                "RTDB room directory unavailable",
                "RTDB oda dizinine ulaşılamıyor",
                "Directorio de salas RTDB no disponible",
                "Répertoire des salons RTDB indisponible",
                "RTDB-Raumverzeichnis nicht verfügbar",
                "RTDB 방 목록 사용 불가",
                "Список комнат RTDB недоступен");
            case "shop_unverified": return AtlasBoardL.R(
                "Shop UI ready; online purchases unverified",
                "Mağaza arayüzü hazır; çevrimiçi işlemler doğrulanmadı",
                "Tienda disponible; compras sin verificar",
                "Boutique disponible; achats non vérifiés",
                "Shop verfügbar; Käufe ungeprüft",
                "상점 UI 사용 가능; 결제 미검증",
                "Магазин доступен; покупки не проверены");
            case "not_checked": return AtlasBoardL.R(
                "Not checked",
                "Kontrol edilmedi",
                "Sin comprobar",
                "Non vérifié",
                "Nicht geprüft",
                "확인 안 됨",
                "Не проверено");
            case "career_not_checked": return AtlasBoardL.R(
                "Server-verified rewards not available yet",
                "Sunucu doğrulamalı ödüller henüz kullanılamıyor",
                "Premios verificados aún no disponibles",
                "Récompenses vérifiées indisponibles",
                "Verifizierte Belohnungen noch nicht verfügbar",
                "검증된 보상 아직 사용 불가",
                "Награды с проверкой пока недоступны");
            case "internet_unavailable": return AtlasBoardL.R(
                "No internet; offline play remains available",
                "İnternet yok; çevrimdışı oyun kullanılabilir",
                "Sin internet; juego local disponible",
                "Pas d’internet; jeu local disponible",
                "Kein Internet; Offline-Spiel verfügbar",
                "인터넷 없음; 오프라인 플레이 가능",
                "Нет интернета; локальная игра доступна");
            case "network_unavailable": return AtlasBoardL.R(
                "No network connection",
                "Ağ bağlantısı yok",
                "Sin conexión de red",
                "Connexion réseau absente",
                "Keine Netzwerkverbindung",
                "네트워크 연결 없음",
                "Нет подключения к сети");
            case "career_offline": return AtlasBoardL.R(
                "No verified XP while offline",
                "Çevrimdışıyken doğrulanmış XP verilmez",
                "Sin XP verificado sin conexión",
                "Pas d’XP vérifiée hors ligne",
                "Offline keine verifizierten XP",
                "오프라인에서 검증 XP 없음",
                "Оффлайн XP не начисляются");
            case "signed_out": return AtlasBoardL.R(
                "Signed out; use Account to sign in",
                "Çıkış yapılmış; Hesap bölümünden giriş yapın",
                "Sesión cerrada; entra en Cuenta",
                "Déconnecté ; ouvrir Compte",
                "Abgemeldet; unter Konto anmelden",
                "로그아웃됨; 계정에서 로그인",
                "Выход выполнен; войдите в аккаунт");
            case "rtdb_sign_in": return AtlasBoardL.R(
                "Sign in to check private RTDB data",
                "Özel RTDB verileri için giriş yapın",
                "Inicia sesión para consultar RTDB",
                "Connectez-vous pour vérifier RTDB",
                "Für private RTDB-Daten anmelden",
                "비공개 RTDB 확인 시 로그인 필요",
                "Войдите для проверки RTDB");
            case "career_guest": return AtlasBoardL.R(
                "Guest preview only",
                "Yalnızca misafir önizlemesi",
                "Solo vista previa de invitado",
                "Aperçu invité uniquement",
                "Nur Gastvorschau",
                "게스트 미리보기 전용",
                "Только гостевой просмотр");
            case "token_failed": return AtlasBoardL.R(
                "Authentication check failed",
                "Hesap doğrulaması başarısız",
                "Error de autenticación",
                "Échec de vérification du compte",
                "Anmeldeprüfung fehlgeschlagen",
                "인증 확인 실패",
                "Ошибка проверки входа");
            case "not_tested_auth": return AtlasBoardL.R(
                "Not tested; login unavailable",
                "Kontrol edilemedi; giriş kullanılamıyor",
                "No comprobado; acceso no disponible",
                "Non vérifié ; connexion indisponible",
                "Nicht geprüft; Anmeldung nicht verfügbar",
                "확인 불가; 로그인 사용 불가",
                "Не проверено; вход недоступен");
            case "token_missing": return AtlasBoardL.R(
                "Authentication token unavailable",
                "Giriş doğrulama anahtarı alınamadı",
                "Token de acceso no disponible",
                "Jeton de connexion indisponible",
                "Anmeldetoken nicht verfügbar",
                "인증 토큰 사용 불가",
                "Токен входа недоступен");
            case "auth_online": return AtlasBoardL.R(
                "Account connection verified",
                "Hesap bağlantısı doğrulandı",
                "Conexión de cuenta verificada",
                "Connexion au compte vérifiée",
                "Kontoverbindung bestätigt",
                "계정 연결 확인됨",
                "Подключение аккаунта проверено");
            case "rtdb_reachable": return AtlasBoardL.R(
                "RTDB user data accessible",
                "RTDB kullanıcı verilerine erişiliyor",
                "Datos de usuario RTDB accesibles",
                "Données utilisateur RTDB accessibles",
                "RTDB-Nutzerdaten erreichbar",
                "RTDB 사용자 데이터 접근 가능",
                "Данные пользователя RTDB доступны");
            case "rtdb_read_error": return AtlasBoardL.R(
                "RTDB access failed",
                "RTDB erişimi başarısız",
                "Error de acceso a RTDB",
                "Accès RTDB échoué",
                "RTDB-Zugriff fehlgeschlagen",
                "RTDB 접근 실패",
                "Ошибка доступа к RTDB");
            case "career_reachable": return AtlasBoardL.R(
                "Career readable; rewards not verified",
                "Kariyer okunuyor; ödüller doğrulanmıyor",
                "Carrera accesible; premios sin verificar",
                "Carrière lisible ; gains non vérifiés",
                "Karriere lesbar; Belohnungen ungeprüft",
                "커리어 읽기 가능; 보상 미검증",
                "Карьеру можно читать; награды не проверены");
            case "career_read_error": return AtlasBoardL.R(
                "Career data inaccessible; no XP fallback",
                "Kariyer verisine erişilemiyor; XP üretilmez",
                "Carrera inaccesible; sin XP alternativo",
                "Carrière inaccessible ; pas d’XP de secours",
                "Karrieredaten nicht erreichbar; keine Ersatz-XP",
                "커리어 접근 불가; XP 대체 지급 없음",
                "Карьера недоступна; XP не начисляются");
            case "not_yet_checked": return AtlasBoardL.R(
                "Last check: not yet checked",
                "Son kontrol: henüz yapılmadı",
                "Última comprobación: pendiente",
                "Dernière vérification : en attente",
                "Letzte Prüfung: noch nicht erfolgt",
                "마지막 확인: 아직 없음",
                "Последняя проверка: ещё не было");
            case "last_check": return AtlasBoardL.R(
                "Last check",
                "Son kontrol",
                "Última comprobación",
                "Dernière vérification",
                "Letzte Prüfung",
                "마지막 확인",
                "Последняя проверка");
            case "every_minute": return AtlasBoardL.R(
                "checked at most once a minute while open",
                "açıkken en fazla dakikada bir kontrol",
                "máximo una vez por minuto",
                "une fois par minute au plus",
                "höchstens einmal pro Minute",
                "열려 있는 동안 분당 최대 한 번",
                "не чаще раза в минуту");
            case "connected": return AtlasBoardL.R(
                "connected",
                "bağlı",
                "conectado",
                "connecté",
                "verbunden",
                "연결됨",
                "подключено");
            case "limited": return AtlasBoardL.R(
                "limited",
                "sınırlı",
                "limitado",
                "limité",
                "eingeschränkt",
                "제한됨",
                "ограничено");
            case "available": return AtlasBoardL.R(
                "available",
                "kullanılabilir",
                "disponible",
                "disponible",
                "verfügbar",
                "사용 가능",
                "доступно");
            case "unavailable": return AtlasBoardL.R(
                "unavailable",
                "bağlantı yok",
                "no disponible",
                "indisponible",
                "nicht verfügbar",
                "사용 불가",
                "недоступно");
            case "ui_available": return AtlasBoardL.R(
                "UI available",
                "arayüz hazır",
                "interfaz disponible",
                "interface disponible",
                "Oberfläche verfügbar",
                "UI 사용 가능",
                "интерфейс доступен");
            default: return key ?? string.Empty;
        }
    }

    private void UpdateToastText()
    {
        if (toastText == null) return;
        string account = states[0] == Health.Online ? T("connected") : T("limited");
        toastText.text = T("server_heading") + "\n" +
            "● " + T("bots") + ": " + T("available") + "    ● " + T("account") + ": " + account + "\n" +
            "● " + T("online") + ": " + T("unavailable") + "    ● " + T("shop") + ": " + T("ui_available");
    }

    private void ShowTransientToast()
    {
        if (toast == null)
        {
            GameObject canvasObject = new GameObject("AtlasBoardServerStatusToastCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas c = canvasObject.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 1200;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            RectTransform container = NewPanel(canvasObject.transform,
                "StatusToast", Vector2.zero, new Vector2(560f, 132f),
                new Color32(25, 39, 56, 242));
            container.anchorMin = container.anchorMax = new Vector2(1f, 1f);
            container.pivot = new Vector2(1f, 1f);
            container.anchoredPosition = new Vector2(-56f, -135f);
            toast = container.gameObject;
            toastText = NewText(container, "ToastText", "",
                Vector2.zero, new Vector2(520f, 110f), 19,
                Color.white, TextAlignmentOptions.Center);
            toastText.enableAutoSizing = true;
            toastText.fontSizeMin = 12f;
            toastText.fontSizeMax = 19f;
        }
        UpdateToastText();
        toast.SetActive(true);
        toastUntil = Time.unscaledTime + 4f;
    }
}
