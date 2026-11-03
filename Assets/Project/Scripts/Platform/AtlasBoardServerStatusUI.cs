using System;
using System.Globalization;
using System.Threading.Tasks;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Read-only foreground status board. A green result is never inferred from
/// a visible UI or a cached name: the signed-in UID must complete its own
/// network probe. Development REST Guest QA and native Firebase share this gate.
/// No user text exposes provider names, tokens, URLs or HTTP status codes.
/// </summary>
[DefaultExecutionOrder(16500)]
public sealed class AtlasBoardServerStatusUI : MonoBehaviour
{
    private enum Health { Checking, Ready, Limited, Offline }
    private static AtlasBoardServerStatusUI instance;
    private static readonly Color32 Background = new Color32(247, 246, 240, 255);
    private static readonly Color32 Ink = new Color32(51, 61, 70, 255);
    private static readonly Color32 Good = new Color32(41, 152, 92, 255);
    private static readonly Color32 Caution = new Color32(193, 142, 40, 255);
    private static readonly Color32 Bad = new Color32(184, 48, 70, 255);
    private static readonly Color32 IdleTab = new Color32(28, 157, 211, 255);
    private static readonly Color32 SelectedTab = new Color32(19, 113, 163, 255);
    private static readonly string[] Names =
        { "account", "bots", "online", "shop", "cloud", "career" };
    private static readonly string[] TabNames =
        { "Tab_Audio", "Tab_Gameplay", "Tab_Graphics", "Tab_Controls",
          "Tab_AccountAtlasBoard", "Tab_ServerStatusAtlasBoard" };

    private readonly Health[] state = new Health[6];
    private readonly string[] note = new string[6];
    private readonly TMP_Text[] label = new TMP_Text[6];
    private readonly TMP_Text[] detail = new TMP_Text[6];
    private readonly Image[] light = new Image[6];
    private readonly Button[] tabs = new Button[6];
    private RectTransform settingsWindow;
    private RectTransform panel;
    private TMP_Text heading;
    private TMP_Text checkedAt;
    private Button tab;
    private bool selected, checking, bound, wasVisible;
    private float bindAt, checkAt, noticeAt;
    private DateTime lastCheck;
    private GameObject toast;
    private TMP_Text toastLabel;
    private float toastUntil;
    private AtlasBoardMatchRuntimeBridge matchBridge;
    private double lastMatchSample = -1d;
    private int matchSamples;
    private string lastAccountSuffix = "";

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
        for (int i = 0; i < state.Length; i++) state[i] = Health.Checking;
        state[1] = Health.Ready;
        note[0] = "checking"; note[1] = "bots_ready";
        note[2] = "checking"; note[3] = "checking";
        note[4] = "checking"; note[5] = "checking";
    }
    private void OnEnable()
    {
        AtlasBoardFrontendErrorCodes.PublicCodeRaised += OnNetworkError;
        AtlasBoardNetworkTelemetry.SampleReported += OnNetworkSample;
        AtlasBoardLocalizationManager.LanguageChanged += Refresh;
    }
    private void OnDisable()
    {
        AtlasBoardFrontendErrorCodes.PublicCodeRaised -= OnNetworkError;
        AtlasBoardNetworkTelemetry.SampleReported -= OnNetworkSample;
        AtlasBoardLocalizationManager.LanguageChanged -= Refresh;
    }
    private void OnDestroy() { if (instance == this) instance = null; }

    private void OnNetworkError(string code, string message)
    {
        if (code != "ATB110" && code != "ATB202") return;
        if (Time.unscaledTime < noticeAt) return;
        noticeAt = Time.unscaledTime + 90f;
        ShowNotice();
    }
    private void OnNetworkSample(AtlasBoardNetworkTelemetrySample sample)
    {
        if (!sample.RequestWasSent || !sample.Success ||
            !string.Equals(sample.FunctionName, "matchRTDB", StringComparison.Ordinal)) return;
        if (matchBridge == null)
            matchBridge = UnityEngine.Object.FindAnyObjectByType<AtlasBoardMatchRuntimeBridge>();
        if (matchBridge == null || string.IsNullOrEmpty(matchBridge.CurrentMatchId)) return;
        lastMatchSample = sample.RealtimeAt;
        matchSamples = Math.Min(20, matchSamples + 1);
        if (selected && state[2] == Health.Limited && HasLiveMatch())
        {
            state[2] = Health.Ready;
            note[2] = "match_verified";
            Refresh();
        }
    }
    private bool HasLiveMatch()
    {
        if (matchBridge == null)
            matchBridge = UnityEngine.Object.FindAnyObjectByType<AtlasBoardMatchRuntimeBridge>();
        return matchBridge != null && !string.IsNullOrEmpty(matchBridge.CurrentMatchId) &&
            matchSamples >= 2 && lastMatchSample >= 0 &&
            Time.realtimeSinceStartupAsDouble - lastMatchSample < 12d;
    }
    private void Update()
    {
        float now = Time.unscaledTime;
        if (now >= bindAt) { bindAt = now + 1f; EnsureUI(); }
        if (toast != null && toast.activeSelf && now >= toastUntil) toast.SetActive(false);
        bool visible = settingsWindow != null && settingsWindow.gameObject.activeInHierarchy;
        if (!visible && wasVisible) Leave();
        wasVisible = visible;
        if (selected && visible && panel != null && panel.gameObject.activeInHierarchy &&
            !checking && now >= checkAt)
        {
            checkAt = now + 60f;
            _ = CheckAsync();
        }
    }
    private void LateUpdate()
    {
        if (settingsWindow == null || !settingsWindow.gameObject.activeInHierarchy) return;
        LayOutTabs();
        if (panel != null && panel.gameObject.activeSelf != selected) panel.gameObject.SetActive(selected);
    }
    private static Button FindButton(string name)
    {
        foreach (Button b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
            if (b != null && b.gameObject.scene.IsValid() && b.name == name) return b;
        return null;
    }
    private void EnsureUI()
    {
        Button controls = FindButton("Tab_Controls");
        if (controls == null) return;
        RectTransform window = controls.transform.parent as RectTransform;
        if (window == null) return;
        if (settingsWindow != window)
        {
            settingsWindow = window;
            tab = null; panel = null; selected = false; bound = false;
            for (int i = 0; i < tabs.Length; i++) tabs[i] = null;
        }
        if (tab == null)
        {
            Button account = FindButton("Tab_AccountAtlasBoard");
            if (account == null) return;
            Transform existing = settingsWindow.Find("Tab_ServerStatusAtlasBoard");
            tab = existing != null ? existing.GetComponent<Button>() : null;
            if (tab == null)
            {
                GameObject cloned = Instantiate(account.gameObject, settingsWindow, false);
                cloned.name = "Tab_ServerStatusAtlasBoard";
                tab = cloned.GetComponent<Button>();
                tab.onClick.RemoveAllListeners();
                foreach (AtlasBoardLocalizedText bind in cloned.GetComponentsInChildren<AtlasBoardLocalizedText>(true))
                    bind.enabled = false;
                tab.onClick.AddListener(Select);
            }
            else
            {
                // This singleton owns the cloned tab regardless of whether
                // an earlier screen instance already created it.
                tab.onClick.RemoveAllListeners();
                tab.onClick.AddListener(Select);
            }
        }
        if (panel == null) BuildPanel();
        if (!bound)
        {
            for (int i = 0; i < 5; i++)
            {
                Button other = FindButton(TabNames[i]);
                if (other != null) other.onClick.AddListener(Leave);
            }
            bound = true;
        }
        for (int i = 0; i < tabs.Length; i++)
        {
            Transform tr = settingsWindow.Find(TabNames[i]);
            tabs[i] = tr != null ? tr.GetComponent<Button>() : null;
        }
        LayOutTabs();
    }
    private void Select()
    {
        selected = true;
        if (panel != null) panel.gameObject.SetActive(true);
        // Reopening Settings must not evade the per-minute network budget.
        double since = lastCheck == default(DateTime)
            ? 60d : (DateTime.UtcNow - lastCheck).TotalSeconds;
        if (since >= 60d)
        {
            state[0] = state[2] = state[3] = state[4] = state[5] = Health.Checking;
            note[0] = note[2] = note[3] = note[4] = note[5] = "checking";
            checkAt = 0f;
        }
        else checkAt = Time.unscaledTime + (float)(60d - Math.Max(0d, since));
        Refresh();
    }
    private void Leave()
    {
        selected = false;
        if (panel != null) panel.gameObject.SetActive(false);
    }
    private void LayOutTabs()
    {
        Image refImage = tabs[3] != null ? tabs[3].GetComponent<Image>() : null;
        for (int i = 0; i < tabs.Length; i++)
        {
            Button b = tabs[i];
            if (b == null) continue;
            RectTransform r = b.transform as RectTransform;
            if (r == null) continue;
            Vector2 pos = new Vector2(-515f + i * 206f, 325f);
            if (r.anchoredPosition != pos) r.anchoredPosition = pos;
            if (r.sizeDelta != new Vector2(194f, 54f)) r.sizeDelta = new Vector2(194f, 54f);
            if (i == 5)
            {
                Image image = b.GetComponent<Image>();
                if (image != null)
                {
                    if (refImage != null) image.sprite = refImage.sprite;
                    image.color = selected ? SelectedTab : IdleTab;
                }
                TMP_Text title = b.GetComponentInChildren<TMP_Text>(true);
                if (title != null)
                {
                    title.text = Word("tab");
                    title.enableAutoSizing = true;
                    title.fontSizeMin = 9f; title.fontSizeMax = 16f;
                }
            }
        }
    }
    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = position; r.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color; image.raycastTarget = false;
        return r;
    }
    private static TMP_Text Label(Transform parent, string name, string value, Vector2 position,
        Vector2 size, int fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text t = go.GetComponent<TMP_Text>();
        if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;
        t.color = Ink; t.text = value; t.fontSize = fontSize; t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false;
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = position; r.sizeDelta = size;
        return t;
    }
    private void BuildPanel()
    {
        panel = Rect(settingsWindow, "AtlasBoardServerStatusPanel", new Vector2(0f,-35f),
            new Vector2(1240f,620f), Background);
        panel.GetComponent<Image>().raycastTarget = true;
        heading = Label(panel,"Heading",Word("heading"),new Vector2(0f,260f),
            new Vector2(1080f,44f),27,TextAlignmentOptions.Center);
        for (int i = 0; i < 6; i++)
        {
            RectTransform line = Rect(panel,"StatusRow"+i,new Vector2(0f,193f-i*72f),
                new Vector2(1100f,64f),i%2==0?
                    new Color32(228,239,246,255):new Color32(238,241,243,255));
            RectTransform dot = Rect(line,"Indicator",new Vector2(-517f,0f),
                new Vector2(22f,22f),Caution);
            light[i] = dot.GetComponent<Image>();
            label[i] = Label(line,"ServiceName",Word(Names[i]),new Vector2(-330f,0f),
                new Vector2(330f,36f),18,TextAlignmentOptions.MidlineLeft);
            detail[i] = Label(line,"ServiceDetails",Word("checking"),new Vector2(180f,0f),
                new Vector2(620f,50f),15,TextAlignmentOptions.MidlineLeft);
            label[i].enableAutoSizing = true; label[i].fontSizeMin = 11; label[i].fontSizeMax = 18;
            detail[i].enableAutoSizing = true; detail[i].fontSizeMin = 11; detail[i].fontSizeMax = 15;
        }
        checkedAt = Label(panel,"LastChecked",Word("unchecked"),new Vector2(0f,-275f),
            new Vector2(1100f,34f),16,TextAlignmentOptions.Center);
        panel.gameObject.SetActive(false); Refresh();
    }
    private void Refresh()
    {
        for (int i = 0; i < Names.Length; i++)
        {
            if (label[i] != null) label[i].text = Word(Names[i]);
            if (detail[i] != null) detail[i].text = Word(note[i]);
            if (light[i] != null)
                light[i].color = state[i] == Health.Ready ? Good :
                    state[i] == Health.Offline ? Bad : Caution;
        }
        if (heading != null) heading.text = Word("heading");
        if (checkedAt != null)
            checkedAt.text = lastCheck == default(DateTime) ? Word("unchecked") :
                Word("last")+": "+lastCheck.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture)+" · "+Word("cadence");
    }

    private async Task CheckAsync()
    {
        if (checking) return;
        checking = true;
        try
        {
            state[1] = Health.Ready; note[1] = "bots_ready";
            for (int i = 0; i < 6; i++)
                if (i != 1) { state[i] = Health.Checking; note[i] = "checking"; }
            Refresh();
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                for (int i = 0; i < 6; i++)
                    if (i != 1) { state[i] = Health.Offline; note[i] = "no_network"; }
                return;
            }
            string uid = "", token = "";
            bool rest = AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession;
            if (rest)
            {
                if (!AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(out uid,out token) ||
                    string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(token))
                {
                    AccountUnavailable(); return;
                }
            }
            else
            {
                FirebaseUser user = null;
                try { user = FirebaseAuth.DefaultInstance.CurrentUser; } catch { }
                if (user == null || string.IsNullOrWhiteSpace(user.UserId)) { AccountUnavailable(); return; }
                uid = user.UserId;
                try { token = await user.TokenAsync(true); } catch { AccountUnavailable(); return; }
                if (string.IsNullOrWhiteSpace(token)) { AccountUnavailable(); return; }
            }
            if (!string.Equals(uid, AtlasBoardRtdbMetaEconomy.CurrentUid,
                StringComparison.Ordinal)) { AccountUnavailable(); return; }
            // The rest of this method never changes a profile, gold or account.
            if (lastAccountSuffix != uid)
            {
                matchSamples = 0; lastMatchSample = -1d; lastAccountSuffix = uid;
            }
            string baseUrl = "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
            string escaped = UnityWebRequest.EscapeURL(uid);
            string auth = "?auth=" + UnityWebRequest.EscapeURL(token);
            long accountCode = await Probe(baseUrl + "/users/" + escaped + "/general.json" + auth);
            if (accountCode == 200)
            {
                state[0] = Health.Ready; note[0] = "account_ready";
                state[4] = Health.Ready; note[4] = "cloud_ready";
            }
            else
            {
                state[0] = accountCode == 401 || accountCode == 403 ? Health.Limited : Health.Offline;
                note[0] = accountCode == 401 || accountCode == 403 ? "account_expired" : "account_failed";
                state[4] = Health.Limited; note[4] = "cloud_unverified";
            }
            // Public room probe is real but is not proof of live match sync.
            long roomCode = await Probe(baseUrl + "/publicRoomDirectory.json" + auth +
                "&orderBy=%22updatedAt%22&limitToLast=1");
            state[2] = roomCode == 200
                ? (HasLiveMatch() ? Health.Ready : Health.Limited) : Health.Offline;
            note[2] = roomCode == 200
                ? (HasLiveMatch() ? "match_verified" : "rooms_ready") : "rooms_failed";
            long careerCode = await Probe(baseUrl + "/careerProfiles/" + escaped + ".json" + auth);
            state[5] = careerCode == 200 ? Health.Ready : Health.Limited;
            note[5] = careerCode == 200 ? "career_ready" : "career_unverified";
            // A verified account and readable wallet identify a safe shop
            // session; they DO NOT certify that purchases are currently writable.
            bool walletUid = string.Equals(uid, AtlasBoardRtdbMetaEconomy.CurrentUid,
                StringComparison.Ordinal);
            state[3] = accountCode == 200 && walletUid ? Health.Limited : Health.Offline;
            note[3] = accountCode == 200 && walletUid ? "shop_browse" : "shop_unverified";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("ATB_SERVER_PROBE account="+accountCode+" rooms="+roomCode+
                " career="+careerCode+" rest="+rest+" uid_suffix="+
                uid.Substring(Math.Max(0,uid.Length-5)));
#endif
        }
        catch (Exception e)
        {
            for (int i = 0; i < state.Length; i++)
                if (i != 1 && state[i] == Health.Checking)
                { state[i] = Health.Limited; note[i] = "unverified"; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("ATB_SERVER_PROBE code=ATB955 stage=exception type="+e.GetType().Name);
#endif
        }
        finally
        {
            lastCheck = DateTime.UtcNow;
            checking = false; Refresh();
            if (state[2] == Health.Offline && Time.unscaledTime >= noticeAt)
            { noticeAt = Time.unscaledTime+240f; ShowNotice(); }
        }
    }
    private void AccountUnavailable()
    {
        state[0] = Health.Limited; note[0] = "login_required";
        state[3] = Health.Limited; note[3] = "shop_unverified";
        state[4] = Health.Limited; note[4] = "login_required";
        state[5] = Health.Limited; note[5] = "login_required";
        state[2] = Health.Limited; note[2] = "rooms_unchecked";
    }
    private static async Task<long> Probe(string url)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 8;
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            return request.result == UnityWebRequest.Result.ConnectionError ? 0L : request.responseCode;
        }
    }
    private static string Word(string key)
    {
        // Seven-language compatibility; status keys are internal-only.
        switch(key)
        {
            case "tab": case "heading": return AtlasBoardL.R("SERVER STATUS","SUNUCU DURUMU","ESTADO DEL SERVIDOR","ÉTAT DU SERVEUR","SERVERSTATUS","서버 상태","СОСТОЯНИЕ СЕРВЕРА");
            case "account": return AtlasBoardL.R("ACCOUNT","HESAP / GİRİŞ","CUENTA","COMPTE","KONTO","계정","АККАУНТ");
            case "bots": return AtlasBoardL.R("OFFLINE BOTS","YEREL BOTLAR","BOTS SIN CONEXIÓN","BOTS LOCAUX","OFFLINE-BOTS","로컬 봇","ЛОКАЛЬНЫЕ БОТЫ");
            case "online": return AtlasBoardL.R("ONLINE MATCHES","ÇEVRİMİÇİ MAÇLAR","PARTIDAS EN LÍNEA","PARTIES EN LIGNE","ONLINE-PARTIEN","온라인 경기","ОНЛАЙН-МАТЧИ");
            case "shop": return AtlasBoardL.R("SHOP","MAĞAZA","TIENDA","BOUTIQUE","SHOP","상점","МАГАЗИН");
            case "cloud": return AtlasBoardL.R("CLOUD DATA","BULUT VERİLERİ","DATOS EN LA NUBE","DONNÉES EN LIGNE","CLOUD-DATEN","클라우드 데이터","ОБЛАЧНЫЕ ДАННЫЕ");
            case "career": return AtlasBoardL.R("CAREER SYNC","KARİYER EŞİTLEME","SINCRONIZACIÓN","SYNCHRONISATION","KARRIERE-SYNC","커리어 동기화","СИНХРОНИЗАЦИЯ");
            case "checking": return AtlasBoardL.R("Checking connection…","Bağlantı kontrol ediliyor…","Comprobando…","Vérification…","Prüfung läuft…","연결 확인 중…","Проверка связи…");
            case "bots_ready": return AtlasBoardL.R("Ready; no connection needed","Hazır; internet gerekmiyor","Disponible sin internet","Disponible hors ligne","Ohne Internet bereit","인터넷 없이 사용 가능","Доступно без интернета");
            case "no_network": return AtlasBoardL.R("No internet connection","İnternet bağlantısı yok","Sin conexión","Pas de connexion","Keine Internetverbindung","인터넷 연결 없음","Нет подключения");
            case "account_ready": return AtlasBoardL.R("Account verified","Hesap doğrulandı","Cuenta verificada","Compte vérifié","Konto bestätigt","계정 확인됨","Аккаунт подтверждён");
            case "account_expired": return AtlasBoardL.R("Sign in again","Yeniden giriş yapman gerekiyor","Inicia sesión de nuevo","Reconnectez-vous","Erneut anmelden","다시 로그인하세요","Войдите повторно");
            case "account_failed": return AtlasBoardL.R("Unable to check account","Hesap kontrol edilemedi","No se pudo verificar","Vérification indisponible","Konto nicht geprüft","계정 확인 불가","Проверка недоступна");
            case "cloud_ready": return AtlasBoardL.R("Player data accessible","Oyuncu verilerine erişiliyor","Datos disponibles","Données accessibles","Spielerdaten erreichbar","플레이어 데이터 접근 가능","Данные игрока доступны");
            case "cloud_unverified": return AtlasBoardL.R("Data access not verified","Veri erişimi doğrulanamadı","Acceso no verificado","Accès non vérifié","Datenzugriff ungeprüft","데이터 접근 미확인","Доступ не проверен");
            case "login_required": return AtlasBoardL.R("Sign in to verify","Kontrol için giriş yap","Inicia sesión","Connectez-vous","Bitte anmelden","로그인 필요","Требуется вход");
            case "rooms_ready": return AtlasBoardL.R("Rooms reachable; match not tested","Odalar erişilebilir; maç henüz sınanmadı","Salas disponibles","Salons disponibles","Räume erreichbar","방 연결 가능","Комнаты доступны");
            case "rooms_failed": return AtlasBoardL.R("Unable to reach online rooms","Çevrimiçi odalara ulaşılamıyor","Salas no disponibles","Salons indisponibles","Räume nicht erreichbar","온라인 방 연결 불가","Комнаты недоступны");
            case "rooms_unchecked": return AtlasBoardL.R("Online rooms not checked","Çevrimiçi odalar henüz kontrol edilmedi","Salas sin comprobar","Salons non vérifiés","Räume nicht geprüft","온라인 방 미확인","Комнаты не проверены");
            case "match_verified": return AtlasBoardL.R("Active match connection verified","Aktif maç bağlantısı doğrulandı","Partida verificada","Partie vérifiée","Aktives Spiel bestätigt","진행 중인 경기 확인됨","Матч подтверждён");
            case "career_ready": return AtlasBoardL.R("Career data readable","Kariyer verilerine erişiliyor","Carrera disponible","Carrière accessible","Karriere erreichbar","커리어 데이터 사용 가능","Карьера доступна");
            case "career_unverified": return AtlasBoardL.R("Career not verified","Kariyer doğrulanamadı","Carrera no verificada","Carrière non vérifiée","Karriere ungeprüft","커리어 미확인","Карьера не проверена");
            case "shop_browse": return AtlasBoardL.R("Account ready; purchases not tested","Hesap hazır; satın alma sınanmadı","Compras sin verificar","Achats non vérifiés","Käufe nicht getestet","구매 확인 안 됨","Покупки не проверены");
            case "shop_unverified": return AtlasBoardL.R("Shop transactions not verified","Mağaza işlemleri doğrulanamadı","Tienda no verificada","Boutique non vérifiée","Shop nicht bestätigt","상점 미확인","Магазин не проверен");
            case "unverified": return AtlasBoardL.R("Check incomplete; retry later","Kontrol tamamlanamadı; tekrar dene","Comprobación incompleta","Vérification incomplète","Prüfung unvollständig","확인 미완료","Проверка не завершена");
            case "unchecked": return AtlasBoardL.R("Not checked yet","Henüz kontrol edilmedi","Sin comprobar","Non vérifié","Noch nicht geprüft","미확인","Не проверено");
            case "last": return AtlasBoardL.R("Last check","Son kontrol","Última comprobación","Dernier contrôle","Letzte Prüfung","마지막 확인","Последняя проверка");
            case "cadence": return AtlasBoardL.R("once a minute while open","açıkken dakikada en fazla bir kez","cada minuto","chaque minute","jede Minute","분당 한 번","раз в минуту");
            default: return string.Empty;
        }
    }
    private void ShowNotice()
    {
        if (toast == null)
        {
            GameObject canvasObject = new GameObject("AtlasBoardServerStatusToastCanvas",
                typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform,false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1200;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080);
            RectTransform background = Rect(canvasObject.transform,"StatusNotice",Vector2.zero,
                new Vector2(560f,132f),new Color32(25,39,56,242));
            background.anchorMin = background.anchorMax = new Vector2(1f,1f);
            background.pivot = new Vector2(1f,1f);
            background.anchoredPosition = new Vector2(-56f,-135f);
            toast = background.gameObject;
            toastLabel = Label(background,"NoticeText",Word("heading"),Vector2.zero,
                new Vector2(520f,110f),19,TextAlignmentOptions.Center);
            toastLabel.color = Color.white;
        }
        toastLabel.text = Word("heading") + "\n" +
            Word(note[0]) + " · " + Word(note[2]);
        toast.SetActive(true);
        toastUntil = Time.unscaledTime + 4f;
    }
}
