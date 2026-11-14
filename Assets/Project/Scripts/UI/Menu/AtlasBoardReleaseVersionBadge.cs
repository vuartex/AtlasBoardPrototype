using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Existing Atlas Board version badge and cooperative RTDB game-version admission.
/// This component is attached to the existing main menu; it is not a second
/// networking manager. It uses the Spark/free RTDB REST endpoint only.
/// IMPORTANT: a client-only gate cannot authenticate an unmodified binary.
/// Older distributed builds require a separately QA'd server admission policy.
/// </summary>
[DisallowMultipleComponent]
public sealed class AtlasBoardReleaseVersionBadge : MonoBehaviour
{
    public const string ReleaseVersion = "0.9.1";
    public const string ContentVersion = "1";

    // Historical RELEASE LABELS, not blindly accepted versions. Firebase decides
    // which TWO active release families are permitted at any given moment.
    // Keep this adjacent to the policy for traceability. Add the next build here
    // only when actually shipping it; old 0.1/0.5 clients must never be accepted.
    private static readonly string[] HistoricalReleases =
    {
        "0.1.0", "0.5.0", "0.9.0-beta.1", "0.9.0", "0.9.1"
    };

    // Intentional migration alias: the previously shipped beta is the v.0.9.0
    // release family. Do not generalize to arbitrary prerelease suffixes.
    private const string PreviousBetaAlias = "0.9.0-beta.1";
    private const string PreviousReleaseFamily = "0.9.0";

    private const string PolicyUrl =
        "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app/gameversion.json";
    private const string BadgeObjectName = "AtlasBoardReleaseVersion_BottomRight";
    private const float CheckIntervalSeconds = 60f;

    [Serializable]
    private sealed class GameVersionValues
    {
        public string currentgameversion;
        public string lastgameversion;
    }

    private enum VersionDecision { Checking, Current, Previous, Unsupported, Offline }

    private AtlasBoardLobbyRuntimeBridge bridge;
    private AtlasBoardMainMenuController menu;
    private TMP_Text badge;
    private bool incompatibilityHandling;
    private string lastMismatch = string.Empty;
    private GameVersionValues policy;
    private VersionDecision decision = VersionDecision.Checking;
    private float nextCheckAt;
    private bool isChecking;
    private int requestGeneration;
    private bool lastFocus = true;
    private string previousNoticeDismissedFor = string.Empty;

    // Runtime-only modal. Always recreate from existing menu theme; never add
    // a serialized Editor menu, secondary manager or prefab dependency.
    private GameObject versionOverlay;
    private TMP_Text noticeTitle;
    private TMP_Text noticeBody;
    private Button noticePrimary;
    private Button noticeSecondary;
    private TMP_Text noticePrimaryLabel;
    private TMP_Text noticeSecondaryLabel;
    private Button noticeClose;
    private TMP_Text noticeCloseLabel;
    private TMP_Text noticeBrand;
    private VersionDecision shownDecision = VersionDecision.Checking;

    // A full-screen overlay alone is insufficient when keyboard navigation or
    // an old menu callback can open a room behind it. Suspend the existing
    // menu canvas as well; never disable the independent overlay's canvas.
    private CanvasGroup menuInputGroup;
    private bool menuGroupAdded;
    private bool menuInputsSuspended;
    private bool previousMenuInteractable;
    private bool previousMenuBlocksRaycasts;


    // Phase 19EF: menu-only idle wallpaper. This is part of the existing
    // version/menu component; there is no additional identity or RTDB manager.
    private const float IdleAfterSeconds = 300f;
    private const float IdleWallpaperChangeSeconds = 180f;
    // Player-supplied, language-specific wallpapers live directly alongside
    // AtlasBoard/Events (not below Events/AFK). Resources path excludes Assets,
    // Project, Resources and the .png suffix. No duplicate art is generated.
    // Names: AtlasBoard_AFK_01_TR.png ... _06_TR.png; 01..04 for other locales.
    private const string IdleResourceBase = "AtlasBoard/AFK/AtlasBoard_AFK_";
    private const int IdleWallpapersPerLanguage = 4;
    private const int IdleTurkishWallpaperCount = 6;
    private string idleDisplayedLanguage = string.Empty;
    private bool idleMissingArtworkLogged;
    private GameObject idleOverlay;
    private RawImage idleWallpaper;
    private TMP_Text idleExitText;
    private RectTransform idleExitRect;
    private RectTransform idleMainMenuRect;
    private UnityEngine.Object idleLoadedResource;
    private float lastUserActionAt;
    private float idleNextWallpaperAt;
    private float idleNextUiScanAt;
    private int idleWallpaperIndex;
    private int idleWakeGeneration;
    private bool idleActive;
    private bool idleWakeInProgress;

    // With no room, the existing RTDB transport uses short REST requests and
    // returns from its lobby polling Update. No persistent socket is assumed;
    // never call FirebaseDatabase.GoOffline and risk breaking another system.
    // During idle, this component also stops its own version-policy polling.

    public static bool IsOnlineEntryAllowed
    {
        get
        {
            foreach (AtlasBoardReleaseVersionBadge instance in
                UnityEngine.Object.FindObjectsByType<AtlasBoardReleaseVersionBadge>(FindObjectsInactive.Include))
                if (instance != null && instance.gameObject.scene.IsValid())
                    return instance.decision == VersionDecision.Current ||
                        instance.decision == VersionDecision.Previous;
            // Fail closed if the existing menu component has not checked RTDB.
            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallOnExistingMenu()
    {
        AtlasBoardMainMenuController[] menus =
            UnityEngine.Object.FindObjectsByType<AtlasBoardMainMenuController>(FindObjectsInactive.Include);
        foreach (AtlasBoardMainMenuController existing in menus)
        {
            if (existing == null || !existing.gameObject.scene.IsValid()) continue;
            if (existing.GetComponent<AtlasBoardReleaseVersionBadge>() == null)
                existing.gameObject.AddComponent<AtlasBoardReleaseVersionBadge>();
        }
    }

    private void Awake()
    {
        menu = GetComponent<AtlasBoardMainMenuController>();
        bridge = GetComponent<AtlasBoardLobbyRuntimeBridge>();
        if (bridge != null) bridge.SnapshotChanged += OnLobbySnapshotChanged;
        AtlasBoardLocalizationManager.LanguageChanged += RefreshLocalizedNotice;
        InstallBadge();
        lastUserActionAt = Time.unscaledTime;
    }

    private void Start()
    {
        InstallBadge();
        if (bridge != null && bridge.CurrentSnapshot != null)
            OnLobbySnapshotChanged(bridge.CurrentSnapshot);
        if (!string.Equals(Application.version, ReleaseVersion, StringComparison.Ordinal))
            Debug.LogWarning("ATB_VERSION_BUILD_CONFIG mismatch=true project=" +
                Application.version + " expected=" + ReleaseVersion +
                " action=check_PlayerSettings_bundleVersion", this);
        // Block main-menu interaction until a fresh server policy is known.
        ShowPolicyNotice(VersionDecision.Checking);
        _ = RefreshPolicyAsync();
    }

    private void OnDestroy()
    {
        requestGeneration++;
        if (bridge != null) bridge.SnapshotChanged -= OnLobbySnapshotChanged;
        AtlasBoardLocalizationManager.LanguageChanged -= RefreshLocalizedNotice;
        RestoreMenuInputs();
        if (menuGroupAdded && menuInputGroup != null) Destroy(menuInputGroup);
        if (versionOverlay != null) Destroy(versionOverlay);
        idleWakeGeneration++;
        if (idleOverlay != null) Destroy(idleOverlay);
        // Only the AFK RawImage owns this dynamically loaded texture.
        if (idleLoadedResource != null)
        {
            if (idleWallpaper != null) idleWallpaper.texture = null;
            Resources.UnloadAsset(idleLoadedResource);
            idleLoadedResource = null;
        }
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused && !lastFocus && !idleActive)
        {
            nextCheckAt = 0f;
            _ = RefreshPolicyAsync();
        }
        lastFocus = focused;
    }

    private void LateUpdate()
    {
        if (badge == null) InstallBadge();
        if (bridge == null)
        {
            bridge = GetComponent<AtlasBoardLobbyRuntimeBridge>();
            if (bridge != null)
            {
                bridge.SnapshotChanged += OnLobbySnapshotChanged;
                if (bridge.CurrentSnapshot != null)
                    OnLobbySnapshotChanged(bridge.CurrentSnapshot);
            }
        }
        // Poll once a minute, never every frame. A manual change in the
        // Firebase Console therefore takes effect without restarting Unity.
        if (!idleActive && Time.unscaledTime >= nextCheckAt && !isChecking)
            _ = RefreshPolicyAsync();
        UpdateMenuIdle();
        // The serialized menu may be created after this component's Start.
        // Respect version gates AND the intentional menu-only idle pause.
        SetMenuInputsSuspended(idleActive || !IsInteractivelyAllowed());
    }

    private void InstallBadge()
    {
        if (badge != null) return;
        RectTransform mainMenu = null;
        foreach (RectTransform child in GetComponentsInChildren<RectTransform>(true))
            if (child != null && child.gameObject.name == "MainMenu")
            {
                mainMenu = child;
                break;
            }
        if (mainMenu == null) return;

        Transform found = mainMenu.Find(BadgeObjectName);
        GameObject labelObject = found != null ? found.gameObject :
            new GameObject(BadgeObjectName, typeof(RectTransform));
        if (found == null) labelObject.transform.SetParent(mainMenu, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-18f, 17f);
        rect.sizeDelta = new Vector2(220f, 32f);
        rect.SetAsLastSibling();

        badge = labelObject.GetComponent<TMP_Text>();
        if (badge == null) badge = labelObject.AddComponent<TextMeshProUGUI>();
        foreach (TMP_Text oldLabel in mainMenu.GetComponentsInChildren<TMP_Text>(true))
        {
            if (oldLabel == null || oldLabel == badge || oldLabel.font == null) continue;
            badge.font = oldLabel.font;
            break;
        }
        badge.fontSize = 20;
        badge.fontStyle = FontStyles.Bold;
        badge.enableAutoSizing = false;
        badge.alignment = TextAlignmentOptions.BottomRight;
        badge.textWrappingMode = TextWrappingModes.NoWrap;
        badge.overflowMode = TextOverflowModes.Overflow;
        badge.raycastTarget = false;
        RefreshBadge();
    }

    private void RefreshBadge()
    {
        if (badge == null) return;
        bool blocked = !string.IsNullOrEmpty(lastMismatch) || decision == VersionDecision.Unsupported;
        badge.text = blocked ? "v" + Application.version + "  |  " + LocalizedMismatchLabel()
            : "v" + Application.version;
        badge.color = blocked ? new Color(0.56f, 0.06f, 0.06f, 1f)
            : new Color(0.06f, 0.09f, 0.13f, 1f);
        badge.rectTransform.sizeDelta = blocked ? new Vector2(345f, 32f)
            : new Vector2(220f, 32f);
    }

    private static string Language()
    {
        string language = AtlasBoardLocalizationManager.Instance != null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";
        return (language ?? "en").ToLowerInvariant();
    }

    private static string LocalizedMismatchLabel()
    {
        switch (Language())
        {
            case "tr": return "GÜNCELLEME GEREKLİ";
            case "es": return "ACTUALIZACIÓN NECESARIA";
            case "fr": return "MISE À JOUR REQUISE";
            case "de": return "UPDATE ERFORDERLICH";
            case "ko": return "업데이트 필요";
            case "ru": return "НУЖНО ОБНОВЛЕНИЕ";
            default: return "UPDATE REQUIRED";
        }
    }

    // Version values in RTDB intentionally include the user's v0.9.1 and
    // v.0.9.0 spellings. Normalize only those prefixes; never use numerical
    // comparisons that accidentally admit unlisted/broken builds.
    public static string NormalizeVersion(string version)
    {
        string value = (version ?? string.Empty).Trim();
        if (value.StartsWith("v.", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(2);
        else if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            value = value.Substring(1);
        if (string.Equals(value, PreviousBetaAlias, StringComparison.OrdinalIgnoreCase))
            return PreviousReleaseFamily;
        return value;
    }

    private static bool IsHistoricalVersion(string local)
    {
        foreach (string known in HistoricalReleases)
            if (string.Equals(local, known, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static VersionDecision Evaluate(string local, GameVersionValues remote)
    {
        if (remote == null || string.IsNullOrWhiteSpace(remote.currentgameversion) ||
            string.IsNullOrWhiteSpace(remote.lastgameversion)) return VersionDecision.Offline;
        // Only signed-off binaries from our history are allowed. The two
        // Firebase values govern which of those are currently admitted.
        if (!IsHistoricalVersion(local)) return VersionDecision.Unsupported;
        string v = NormalizeVersion(local);
        string current = NormalizeVersion(remote.currentgameversion);
        string last = NormalizeVersion(remote.lastgameversion);
        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(last) || current == last)
            return VersionDecision.Offline;
        if (string.Equals(v, current, StringComparison.OrdinalIgnoreCase))
            return VersionDecision.Current;
        if (string.Equals(v, last, StringComparison.OrdinalIgnoreCase))
            return VersionDecision.Previous;
        return VersionDecision.Unsupported;
    }

    private async Task RefreshPolicyAsync()
    {
        if (isChecking || idleActive) return;
        isChecking = true;
        int generation = ++requestGeneration;
        nextCheckAt = Time.unscaledTime + CheckIntervalSeconds;
        try
        {
            using (UnityWebRequest request = UnityWebRequest.Get(PolicyUrl))
            {
                request.timeout = 8;
                UnityWebRequestAsyncOperation op = request.SendWebRequest();
                while (!op.isDone) await Task.Yield();
                if (this == null || generation != requestGeneration) return;
                if (request.result != UnityWebRequest.Result.Success ||
                    string.IsNullOrWhiteSpace(request.downloadHandler.text))
                {
                    decision = VersionDecision.Offline;
                    Debug.LogWarning("ATB_VERSION_POLICY read_failed=true http=" + request.responseCode);
                }
                else
                {
                    GameVersionValues candidate = null;
                    try { candidate = JsonUtility.FromJson<GameVersionValues>(request.downloadHandler.text); }
                    catch (Exception) { /* A malformed policy is fail-closed. */ }
                    policy = candidate;
                    decision = Evaluate(Application.version, policy);
                    Debug.Log("ATB_VERSION_POLICY decision=" + decision +
                        " client=" + Application.version + " current=" +
                        (policy == null ? "missing" : policy.currentgameversion) +
                        " previous=" + (policy == null ? "missing" : policy.lastgameversion));
                }
            }
            if (this != null && generation == requestGeneration)
            {
                UpdatePolicyPresentation();
                if (bridge != null && bridge.CurrentSnapshot != null &&
                    !IsCompatible(bridge.CurrentSnapshot))
                    OnLobbySnapshotChanged(bridge.CurrentSnapshot);
            }
        }
        catch (Exception ex)
        {
            if (this == null || generation != requestGeneration) return;
            decision = VersionDecision.Offline;
            Debug.LogWarning("ATB_VERSION_POLICY exception=" + ex.GetType().Name);
            UpdatePolicyPresentation();
        }
        finally { isChecking = false; }
    }

    private void UpdatePolicyPresentation()
    {
        // Do not keep the previous room mismatch warning after the user has
        // returned to a valid policy. Old rooms are still checked separately.
        if (decision == VersionDecision.Current || decision == VersionDecision.Previous)
            lastMismatch = string.Empty;
        RefreshBadge();
        if (decision == VersionDecision.Current)
        {
            HidePolicyNotice();
            return;
        }
        if (decision == VersionDecision.Previous)
        {
            string key = (policy == null ? "" : policy.currentgameversion + "|" + policy.lastgameversion);
            if (key == previousNoticeDismissedFor)
                HidePolicyNotice();
            else ShowPolicyNotice(VersionDecision.Previous);
            return;
        }
        // Unsupported, offline and checking all block entry. There is no
        // dismiss/ESC path in the overlay for those conditions.
        ShowPolicyNotice(decision);
    }

    private bool IsInteractivelyAllowed()
    {
        bool supported = decision == VersionDecision.Current ||
            decision == VersionDecision.Previous;
        // Even a permitted previous release must dismiss the warning first.
        return supported && !idleActive && (versionOverlay == null || !versionOverlay.activeSelf);
    }

    private void SetMenuInputsSuspended(bool suspend)
    {
        if (menuInputGroup == null)
        {
            Transform target = null;
            foreach (RectTransform rect in GetComponentsInChildren<RectTransform>(true))
                if (rect != null && rect.gameObject.name == "MainMenu")
                {
                    target = rect;
                    break;
                }
            if (target == null) return;
            menuInputGroup = target.GetComponent<CanvasGroup>();
            if (menuInputGroup == null)
            {
                menuInputGroup = target.gameObject.AddComponent<CanvasGroup>();
                menuGroupAdded = true;
            }
        }
        if (suspend)
        {
            if (!menuInputsSuspended)
            {
                previousMenuInteractable = menuInputGroup.interactable;
                previousMenuBlocksRaycasts = menuInputGroup.blocksRaycasts;
                menuInputsSuspended = true;
            }
            menuInputGroup.interactable = false;
            menuInputGroup.blocksRaycasts = false;
        }
        else RestoreMenuInputs();
    }

    private void RestoreMenuInputs()
    {
        if (!menuInputsSuspended || menuInputGroup == null) return;
        menuInputGroup.interactable = previousMenuInteractable;
        menuInputGroup.blocksRaycasts = previousMenuBlocksRaycasts;
        menuInputsSuspended = false;
    }

    public static bool IsCompatible(AtlasLobbySnapshot snapshot)
    {
        // Room protocol, rules and content MUST still agree even when both
        // GameVersion strings belong to the two accepted release families.
        return snapshot != null &&
            snapshot.ProtocolVersion == AtlasOnlineDefaults.ProtocolVersion &&
            snapshot.RulesVersion == AtlasOnlineDefaults.RulesVersion &&
            string.Equals(snapshot.ContentVersion, ContentVersion, StringComparison.Ordinal) &&
            // Remote versions are accepted only while this local process has
            // validated the two-value policy. Never trust room labels alone.
            IsCompatiblePeerVersion(snapshot.GameVersion);
    }

    private static bool IsCompatiblePeerVersion(string remoteVersion)
    {
        foreach (AtlasBoardReleaseVersionBadge badge in
            UnityEngine.Object.FindObjectsByType<AtlasBoardReleaseVersionBadge>(FindObjectsInactive.Include))
        {
            if (badge == null || badge.policy == null ||
                (badge.decision != VersionDecision.Current && badge.decision != VersionDecision.Previous))
                continue;
            string remote = NormalizeVersion(remoteVersion);
            return string.Equals(remote, NormalizeVersion(badge.policy.currentgameversion), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(remote, NormalizeVersion(badge.policy.lastgameversion), StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private void OnLobbySnapshotChanged(AtlasLobbySnapshot snapshot)
    {
        if (snapshot == null || incompatibilityHandling) return;
        // The policy may not be loaded yet. Do not close existing rooms solely
        // because the first network read is still in progress.
        if (decision == VersionDecision.Checking || decision == VersionDecision.Offline)
            return;
        if (IsCompatible(snapshot)) return;
        incompatibilityHandling = true;
        lastMismatch = "remote=" + (snapshot.GameVersion ?? "missing") +
            "/" + snapshot.ProtocolVersion + "/" + snapshot.RulesVersion +
            "/" + (snapshot.ContentVersion ?? "missing");
        RefreshBadge();
        Debug.LogWarning("ATB_VERSION_COMPATIBLE false local=" + Application.version +
            " " + lastMismatch + " action=leave_incompatible_lobby", this);
        _ = ExitIncompatibleRoomAsync(snapshot);
    }

    private async Task ExitIncompatibleRoomAsync(AtlasLobbySnapshot detected)
    {
        if (bridge == null) return;
        try
        {
            if (bridge.HasLobby)
            {
                bool localHost = string.Equals(detected.HostAccountId,
                    bridge.CurrentAccountId, StringComparison.Ordinal);
                AtlasLobbyOperationResult result = localHost
                    ? await bridge.CloseLobbyAsync()
                    : await bridge.LeaveLobbyAsync();
                if (result == null || !result.Success)
                    Debug.LogWarning("ATB_VERSION_ROOM_EXIT server_cleanup_pending=true", this);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_VERSION_ROOM_EXIT exception=" + ex.GetType().Name, this);
        }
        finally
        {
            if (bridge != null) bridge.ClearLocalLobbyState();
            if (menu != null) menu.ShowMainMenu();
            incompatibilityHandling = false;
        }
    }

    private void RefreshLocalizedNotice()
    {
        if (versionOverlay != null && versionOverlay.activeSelf)
            ShowPolicyNotice(shownDecision);
        if (idleExitText != null)
            idleExitText.text = Label("quit");
        // Language changes must also update the wallpaper immediately. Never
        // leave a Turkish bitmap on-screen after selecting another language.
        if (idleActive && idleWallpaper != null)
        {
            idleWallpaperIndex = -1;
            idleDisplayedLanguage = string.Empty;
            NextIdleWallpaper();
            idleNextWallpaperAt = Time.unscaledTime + IdleWallpaperChangeSeconds;
        }
    }

    private static string Label(string key)
    {
        string locale = Language();
        switch (key)
        {
            case "loading":
                switch(locale) { case "tr": return "Sürüm kontrol ediliyor"; case "es": return "Comprobando la versión"; case "fr": return "Vérification de la version"; case "de": return "Spielversion wird geprüft"; case "ko": return "게임 버전 확인 중"; case "ru": return "Проверка версии игры"; default: return "Checking game version"; }
            case "previous_title":
                switch(locale) { case "tr": return "YENİ SÜRÜM MEVCUT"; case "es": return "ACTUALIZACIÓN DISPONIBLE"; case "fr": return "MISE À JOUR DISPONIBLE"; case "de": return "UPDATE VERFÜGBAR"; case "ko": return "새 버전 사용 가능"; case "ru": return "ДОСТУПНО ОБНОВЛЕНИЕ"; default: return "UPDATE AVAILABLE"; }
            case "blocked_title": return LocalizedMismatchLabel();
            case "offline_title":
                switch(locale) { case "tr": return "BAĞLANTI KONTROLÜ"; case "es": return "COMPROBACIÓN DE CONEXIÓN"; case "fr": return "VÉRIFICATION DE CONNEXION"; case "de": return "VERBINDUNGSPRÜFUNG"; case "ko": return "연결 확인"; case "ru": return "ПРОВЕРКА СОЕДИНЕНИЯ"; default: return "CONNECTION CHECK"; }
            case "later":
                switch(locale) { case "tr": return "DAHA SONRA"; case "es": return "MÁS TARDE"; case "fr": return "PLUS TARD"; case "de": return "SPÄTER"; case "ko": return "나중에"; case "ru": return "ПОЗЖЕ"; default: return "LATER"; }
            case "quit":
                switch(locale) { case "tr": return "OYUNDAN ÇIK"; case "es": return "SALIR DEL JUEGO"; case "fr": return "QUITTER LE JEU"; case "de": return "SPIEL BEENDEN"; case "ko": return "게임 종료"; case "ru": return "ВЫЙТИ ИЗ ИГРЫ"; default: return "EXIT GAME"; }
            case "retry":
                switch(locale) { case "tr": return "TEKRAR DENE"; case "es": return "REINTENTAR"; case "fr": return "RÉESSAYER"; case "de": return "ERNEUT PRÜFEN"; case "ko": return "다시 시도"; case "ru": return "ПОВТОРИТЬ"; default: return "RETRY"; }
            case "previous_body":
                switch(locale) { case "tr": return "Atlas Board'un yeni sürümü mevcut. Güncellemeni öneriyoruz; şimdilik bu sürümle devam edebilirsin."; case "es": return "Hay una nueva versión de Atlas Board. Puedes actualizar ahora o continuar por el momento."; case "fr": return "Une nouvelle version d'Atlas Board est disponible. Vous pouvez jouer maintenant et mettre à jour plus tard."; case "de": return "Eine neue Atlas-Board-Version ist verfügbar. Du kannst vorerst weiterspielen und später aktualisieren."; case "ko": return "새 Atlas Board 버전이 있습니다. 지금 계속 플레이하고 나중에 업데이트할 수 있습니다."; case "ru": return "Доступна новая версия Atlas Board. Можно продолжить игру и обновиться позже."; default: return "A new Atlas Board version is available. You may continue playing and update later."; }
            case "blocked_body":
                switch(locale) { case "tr": return "Bu oyun sürümü artık desteklenmiyor. Devam etmek için lütfen Atlas Board'u güncelleyin."; case "es": return "Esta versión ya no es compatible. Actualiza Atlas Board para continuar."; case "fr": return "Cette version n'est plus prise en charge. Mettez à jour Atlas Board pour continuer."; case "de": return "Diese Version wird nicht mehr unterstützt. Bitte aktualisiere Atlas Board."; case "ko": return "이 버전은 더 이상 지원되지 않습니다. Atlas Board를 업데이트하세요."; case "ru": return "Эта версия больше не поддерживается. Обновите Atlas Board."; default: return "This version is no longer supported. Update Atlas Board to continue."; }
            case "offline_body":
                switch(locale) { case "tr": return "Firebase sürüm bilgisi alınamadı. İnternet bağlantını kontrol edip yeniden dene."; case "es": return "No se pudo verificar la versión. Comprueba la conexión e inténtalo de nuevo."; case "fr": return "Impossible de vérifier la version. Vérifiez votre connexion et réessayez."; case "de": return "Die Version konnte nicht geprüft werden. Prüfe die Verbindung und versuche es erneut."; case "ko": return "버전을 확인할 수 없습니다. 연결을 확인하고 다시 시도하세요."; case "ru": return "Не удалось проверить версию. Проверьте подключение и повторите попытку."; default: return "Could not verify the game version. Check your connection and retry."; }
            default: return key;
        }
    }

    private static Image MakeImage(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>(); img.color = color;
        return img;
    }

    private TMP_Text MakeText(Transform parent, string name, Vector2 min, Vector2 max, int size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min; rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        TMP_Text tx = go.GetComponent<TMP_Text>();
        tx.fontSize = size;
        tx.enableAutoSizing = true;
        tx.fontSizeMin = Mathf.Max(12, size - 8);
        tx.fontSizeMax = size;
        tx.alignment = TextAlignmentOptions.Center;
        tx.color = new Color(0.96f, 0.96f, 1f);
        tx.textWrappingMode = TextWrappingModes.Normal;
        if (badge != null && badge.font != null) tx.font = badge.font;
        return tx;
    }

    private Button MakeButton(Transform parent, string name, Vector2 min, Vector2 max,
        Color bg, out TMP_Text label)
    {
        Image img = MakeImage(parent, name, min, max, bg);
        Button button = img.gameObject.AddComponent<Button>();
        label = MakeText(img.transform, "Text", Vector2.zero, Vector2.one, 22);
        return button;
    }

    private void EnsurePolicyOverlay()
    {
        if (versionOverlay != null) return;
        // IMPORTANT: This must be a TOP-LEVEL ScreenSpaceOverlay canvas, not a
        // child of the scaled main-menu panel. The old nested Canvas had a
        // zero-width RectTransform: every sentence collapsed into a narrow
        // vertical strip and underlying buttons remained clickable.
        versionOverlay = new GameObject("AtlasBoardVersionNotice", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        RectTransform canvasRect = versionOverlay.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
        Canvas c = versionOverlay.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.overrideSorting = true;
        c.sortingOrder = 32000;
        CanvasScaler scaler = versionOverlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = versionOverlay.transform;

        Image dim = MakeImage(root, "FullScreenInputBlocker", Vector2.zero, Vector2.one,
            new Color(0.025f, 0.035f, 0.085f, 0.88f));
        dim.raycastTarget = true; // full-screen mouse and mobile interception
        Image outline = MakeImage(root, "VersionDialogBorder", new Vector2(0.18f, 0.23f),
            new Vector2(0.82f, 0.77f), new Color(0.84f, 0.61f, 0.23f));
        Image panel = MakeImage(outline.transform, "VersionDialog", new Vector2(0.006f, 0.012f),
            new Vector2(0.994f, 0.988f), new Color(0.115f, 0.15f, 0.31f));
        MakeImage(panel.transform, "TopAccent", new Vector2(0.045f, 0.89f),
            new Vector2(0.955f, 0.903f), new Color(0.96f, 0.72f, 0.25f));

        noticeBrand = MakeText(panel.transform, "Brand", new Vector2(0.06f, 0.91f),
            new Vector2(0.94f, 0.985f), 18);
        noticeBrand.text = "ATLAS BOARD";
        noticeBrand.color = new Color(0.97f, 0.73f, 0.28f);
        noticeTitle = MakeText(panel.transform, "VersionTitle", new Vector2(0.08f, 0.71f),
            new Vector2(0.92f, 0.87f), 36);
        noticeTitle.fontSizeMin = 27;
        noticeBody = MakeText(panel.transform, "VersionExplanation", new Vector2(0.085f, 0.30f),
            new Vector2(0.915f, 0.69f), 23);
        noticeBody.fontSizeMin = 18;
        noticeBody.fontSizeMax = 23;
        noticeBody.lineSpacing = 7f;

        noticePrimary = MakeButton(panel.transform, "RetryPolicy", new Vector2(0.12f, 0.085f),
            new Vector2(0.48f, 0.22f), new Color(0.12f, 0.62f, 0.77f), out noticePrimaryLabel);
        noticeSecondary = MakeButton(panel.transform, "QuitOrContinue", new Vector2(0.52f, 0.085f),
            new Vector2(0.88f, 0.22f), new Color(0.45f, 0.29f, 0.64f), out noticeSecondaryLabel);
        noticeClose = MakeButton(panel.transform, "DismissPreviousOnly", new Vector2(0.935f, 0.905f),
            new Vector2(0.985f, 0.978f), new Color(0.65f, 0.12f, 0.32f), out noticeCloseLabel);
        noticeCloseLabel.text = "X";
        noticeClose.onClick.AddListener(DismissPreviousNotice);
        noticePrimary.onClick.AddListener(() => { nextCheckAt = 0; _ = RefreshPolicyAsync(); });
        noticeSecondary.onClick.AddListener(() =>
        {
            if (decision == VersionDecision.Previous)
                DismissPreviousNotice();
            else if (decision == VersionDecision.Unsupported || decision == VersionDecision.Offline)
                QuitFromVersionDialog();
        });
    }

    private void DismissPreviousNotice()
    {
        if (decision != VersionDecision.Previous) return;
        previousNoticeDismissedFor = policy == null ? "" :
            policy.currentgameversion + "|" + policy.lastgameversion;
        HidePolicyNotice();
    }

    private static void QuitFromVersionDialog()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static string VersionDetailLabel(bool installed)
    {
        switch (Language())
        {
            case "tr": return installed ? "Oyundaki sürüm: " : "Güncel sürüm: ";
            case "es": return installed ? "Versión instalada: " : "Versión actual: ";
            case "fr": return installed ? "Version installée : " : "Version actuelle : ";
            case "de": return installed ? "Installierte Version: " : "Aktuelle Version: ";
            case "ko": return installed ? "설치된 버전: " : "현재 버전: ";
            case "ru": return installed ? "Установленная версия: " : "Текущая версия: ";
            default: return installed ? "Installed: " : "Current: ";
        }
    }

    private void ShowPolicyNotice(VersionDecision show)
    {
        EnsurePolicyOverlay();
        if (versionOverlay == null) return;
        shownDecision = show;
        versionOverlay.SetActive(true);
        string detail = "\n\n" + VersionDetailLabel(true) +
            "v" + Application.version + "\n" + VersionDetailLabel(false) +
            (policy == null ? "—" : policy.currentgameversion);
        if (policy != null && show == VersionDecision.Unsupported)
            detail += "\n" + VersionDetailLabelPrevious() + policy.lastgameversion;
        noticeTitle.text = show == VersionDecision.Previous ? Label("previous_title") :
            show == VersionDecision.Unsupported ? Label("blocked_title") :
            show == VersionDecision.Offline ? Label("offline_title") : Label("loading");
        noticeBody.text = (show == VersionDecision.Previous ? Label("previous_body") :
            show == VersionDecision.Unsupported ? Label("blocked_body") :
            show == VersionDecision.Offline ? Label("offline_body") : Label("loading")) +
            (show == VersionDecision.Checking ? "" : detail);
        bool pending = show == VersionDecision.Checking;
        noticePrimary.gameObject.SetActive(!pending && show != VersionDecision.Previous);
        noticeSecondary.gameObject.SetActive(!pending);
        noticeClose.gameObject.SetActive(show == VersionDecision.Previous);
        noticePrimaryLabel.text = Label("retry");
        noticeSecondaryLabel.text = show == VersionDecision.Previous ? Label("later") : Label("quit");
        SetMenuInputsSuspended(true);
    }

    private static string VersionDetailLabelPrevious()
    {
        switch (Language())
        {
            case "tr": return "İzin verilen önceki sürüm: ";
            case "es": return "Versión anterior permitida: ";
            case "fr": return "Version précédente autorisée : ";
            case "de": return "Erlaubte vorherige Version: ";
            case "ko": return "허용되는 이전 버전: ";
            case "ru": return "Разрешённая предыдущая версия: ";
            default: return "Allowed previous version: ";
        }
    }

    private void HidePolicyNotice()
    {
        if (versionOverlay != null) versionOverlay.SetActive(false);
        SetMenuInputsSuspended(!IsInteractivelyAllowed());
    }


    // ===== MAIN MENU ONLY: AFK idle presentation (Phase 19EF) =====
    // No new singleton, RTDB database manager, Editor menu or second roadmap.
    private bool IsEligibleForMenuIdle()
    {
        // A routine minute-level gameversion refresh must NOT reset this
        // five-minute timer. It only postpones entry while in flight.
        if (!IsInPureMainMenu()) return false;
        return (decision == VersionDecision.Current ||
                decision == VersionDecision.Previous) &&
               !isChecking && !idleWakeInProgress &&
               (versionOverlay == null || !versionOverlay.activeSelf);
    }

    private bool IsInPureMainMenu()
    {
        if (menu == null || !menu.isActiveAndEnabled) return false;
        if (bridge != null && bridge.HasLobby) return false;

        // Exactly the existing active main-menu panel, not the room or match.
        if (idleMainMenuRect == null)
        {
            foreach (RectTransform r in GetComponentsInChildren<RectTransform>(true))
                if (r != null && r.gameObject.name == "MainMenu")
                { idleMainMenuRect = r; break; }
        }
        if (idleMainMenuRect == null || !idleMainMenuRect.gameObject.activeInHierarchy)
            return false;

        AtlasBoardPrivateLobbyUIController lobby =
            GetComponent<AtlasBoardPrivateLobbyUIController>();
        if (lobby != null && !lobby.IsMainMenuTransitionSettled) return false;

        // Never steal keyboard/touch focus from an existing modal or tablet.
        foreach (AtlasBoardEscapeBlocker blocker in
            UnityEngine.Object.FindObjectsByType<AtlasBoardEscapeBlocker>(
                FindObjectsInactive.Include))
            if (blocker != null && blocker.IsBlocking) return false;
        foreach (RectTransform r in GetComponentsInChildren<RectTransform>(true))
            if (r != null && r.gameObject.activeInHierarchy &&
                (string.Equals(r.gameObject.name, "Modal", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(r.gameObject.name, "ModalPanel", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(r.gameObject.name, "SettingsPanel", StringComparison.OrdinalIgnoreCase)))
                return false;
        return true;
    }

    // Deliberate user actions, not cursor movements, animation, UI polling or
    // background REST responses. Compile for both legacy and new Input System.
    private static bool WasIdleInputPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null &&
            (mouse.leftButton.wasPressedThisFrame ||
             mouse.rightButton.wasPressedThisFrame ||
             mouse.middleButton.wasPressedThisFrame)) return true;
        var touch = UnityEngine.InputSystem.Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame ||
             pad.buttonNorth.wasPressedThisFrame ||
             pad.buttonWest.wasPressedThisFrame ||
             pad.buttonEast.wasPressedThisFrame ||
             pad.startButton.wasPressedThisFrame ||
             pad.selectButton.wasPressedThisFrame ||
             pad.dpad.up.wasPressedThisFrame ||
             pad.dpad.down.wasPressedThisFrame ||
             pad.dpad.left.wasPressedThisFrame ||
             pad.dpad.right.wasPressedThisFrame)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) ||
            Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) return true;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            return true;
#endif
        return false;
    }

    private bool IsIdleExitPointerPressed()
    {
        if (idleExitRect == null) return false;
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
            RectTransformUtility.RectangleContainsScreenPoint(
                idleExitRect, mouse.position.ReadValue(), null)) return true;
        var touch = UnityEngine.InputSystem.Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame &&
            RectTransformUtility.RectangleContainsScreenPoint(
                idleExitRect, touch.primaryTouch.position.ReadValue(), null)) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(0) &&
            RectTransformUtility.RectangleContainsScreenPoint(
                idleExitRect, Input.mousePosition, null)) return true;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began &&
            RectTransformUtility.RectangleContainsScreenPoint(
                idleExitRect, Input.GetTouch(0).position, null)) return true;
#endif
        return false;
    }

    private void UpdateMenuIdle()
    {
        if (idleActive)
        {
            // The exit button is a real Unity control. Let it receive the
            // pointer event, rather than destroying the overlay on mouse-down.
            if (WasIdleInputPressed() && !IsIdleExitPointerPressed() &&
                !idleWakeInProgress)
            {
                _ = WakeFromMenuIdleAsync();
                return;
            }
            if (Time.unscaledTime >= idleNextWallpaperAt)
            {
                NextIdleWallpaper();
                idleNextWallpaperAt = Time.unscaledTime + IdleWallpaperChangeSeconds;
            }
            return;
        }

        if (WasIdleInputPressed()) lastUserActionAt = Time.unscaledTime;
        // Scene/modal checks need not run at 60 FPS.
        if (Time.unscaledTime < idleNextUiScanAt) return;
        idleNextUiScanAt = Time.unscaledTime + 0.5f;
        if (!IsInPureMainMenu())
        {
            lastUserActionAt = Time.unscaledTime;
            return;
        }
        // The version-policy refresh may take several seconds, but that is
        // not a player action; do not quietly restart the AFK countdown.
        if (!IsEligibleForMenuIdle()) return;
        if (Time.unscaledTime - lastUserActionAt < IdleAfterSeconds) return;
        EnterMenuIdle();
    }

    private void EnterMenuIdle()
    {
        if (idleActive || !IsEligibleForMenuIdle()) return;
        idleActive = true;
        // This existing UI/REST component must not keep contacting RTDB
        // while idle; there is no HasLobby, therefore no room heartbeat or
        // snapshot poll to tear down. No Firebase Auth sign-out or UID purge.
        nextCheckAt = float.PositiveInfinity;
        EnsureMenuIdleOverlay();
        if (idleOverlay == null)
        {
            idleActive = false;
            lastUserActionAt = Time.unscaledTime;
            return;
        }
        idleOverlay.SetActive(true);
        idleWallpaperIndex = -1;
        idleDisplayedLanguage = string.Empty;
        NextIdleWallpaper();
        idleNextWallpaperAt = Time.unscaledTime + IdleWallpaperChangeSeconds;
        SetMenuInputsSuspended(true);
        Debug.Log("ATB_MENU_IDLE active=true threshold_s=300 mode=main_menu_no_lobby " +
            "transport=rest version_poll=paused uid_unchanged=true");
    }

    private static string IdleLanguageCode()
    {
        // Fallback locale is EN, not the user's old Turkish-only thumbnails.
        switch (Language())
        {
            case "tr": return "TR";
            case "en": return "EN";
            case "es": return "ES";
            case "fr": return "FR";
            case "de": return "DE";
            case "ko": return "KO";
            case "ru": return "RU";
            default: return "EN";
        }
    }

    private static int IdleWallpaperCount(string language)
    {
        return language == "TR" ? IdleTurkishWallpaperCount :
            IdleWallpapersPerLanguage;
    }

    private void NextIdleWallpaper()
    {
        if (idleWallpaper == null) return;
        string requestedLanguage = IdleLanguageCode();
        if (!string.Equals(requestedLanguage, idleDisplayedLanguage,
            StringComparison.Ordinal))
        {
            idleWallpaperIndex = -1;
            idleDisplayedLanguage = requestedLanguage;
            idleMissingArtworkLogged = false;
        }

        Texture2D nextTexture = null;
        UnityEngine.Object loaded = null;
        int selected = -1;
        int count = IdleWallpaperCount(requestedLanguage);
        for (int n = 1; n <= count; n++)
        {
            int index = (idleWallpaperIndex + n + count) % count;
            string filename = IdleResourceBase + (index + 1).ToString("00") +
                "_" + requestedLanguage;
            loaded = LoadWallpaperResource(filename, out nextTexture);
            if (nextTexture == null) continue;
            selected = index;
            break;
        }

        // A missing localization must not produce a broken/empty screen.
        // Only fall back to real EN assets, never synthesize or duplicate an
        // image file, and do not pretend this is a translated wallpaper.
        if (nextTexture == null && requestedLanguage != "EN")
        {
            for (int n = 1; n <= IdleWallpapersPerLanguage; n++)
            {
                string english = IdleResourceBase + n.ToString("00") + "_EN";
                loaded = LoadWallpaperResource(english, out nextTexture);
                if (nextTexture != null) break;
            }
        }
        if (nextTexture == null)
        {
            if (!idleMissingArtworkLogged)
            {
                idleMissingArtworkLogged = true;
                Debug.LogWarning("ATB_MENU_IDLE wallpaper_missing=true language=" +
                    requestedLanguage + " path=" + IdleResourceBase +
                    " using=solid_colour_fallback");
            }
            return;
        }
        if (selected < 0 && !idleMissingArtworkLogged)
        {
            idleMissingArtworkLogged = true;
            Debug.LogWarning("ATB_MENU_IDLE missing_language_art=" +
                requestedLanguage + " fallback=EN");
        }

        UnityEngine.Object previous = idleLoadedResource;
        idleWallpaper.texture = nextTexture;
        idleLoadedResource = loaded;
        if (selected >= 0) idleWallpaperIndex = selected;
        // All new wallpapers are clean (no embedded Exit or version labels).
        // Do not crop the bottom of the user's actual 01..06 locale art.
        idleWallpaper.uvRect = new Rect(0f, 0f, 1f, 1f);
        AspectRatioFitter fitter = idleWallpaper.GetComponent<AspectRatioFitter>();
        if (fitter != null)
            fitter.aspectRatio = (float)nextTexture.width / nextTexture.height;
        if (previous != null && previous != loaded)
            Resources.UnloadAsset(previous);
    }

    private static UnityEngine.Object LoadWallpaperResource(string resourcePath,
        out Texture2D texture)
    {
        // Resource PNGs can be imported either as Default textures or as
        // Sprite (2D and UI). Support both without requiring Editor import
        // changes or a second image folder.
        texture = Resources.Load<Texture2D>(resourcePath);
        if (texture != null) return texture;
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
        {
            texture = sprite.texture;
            return sprite;
        }
        return null;
    }

    private void EnsureMenuIdleOverlay()
    {
        if (idleOverlay != null) return;
        idleOverlay = new GameObject("AtlasBoardMainMenuIdleOverlay",
            typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = idleOverlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 31900; // Version modal = 32000, always wins.
        CanvasScaler scaler = idleOverlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = idleOverlay.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;

        Image backdrop = MakeImage(root, "BackgroundFallback", Vector2.zero,
            Vector2.one, new Color(0.015f, 0.055f, 0.13f, 1f));
        backdrop.raycastTarget = true;
        GameObject art = new GameObject("IdleWallpaper_3MinuteRotation",
            typeof(RectTransform), typeof(RawImage));
        art.transform.SetParent(root, false);
        RectTransform artRect = art.GetComponent<RectTransform>();
        artRect.anchorMin = Vector2.zero; artRect.anchorMax = Vector2.one;
        artRect.offsetMin = artRect.offsetMax = Vector2.zero;
        idleWallpaper = art.GetComponent<RawImage>();
        AspectRatioFitter wallpaperFitter = art.AddComponent<AspectRatioFitter>();
        wallpaperFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        wallpaperFitter.aspectRatio = 16f / 9f;
        idleWallpaper.raycastTarget = true;
        idleWallpaper.color = Color.white;

        // Only the quit control is a true clickable button. All other words
        // belong to the wallpaper and are decorative; no duplicate hot spots.
        TMP_Text label;
        Button quit = MakeButton(root, "IdleExitActualButton",
            new Vector2(0.39f, 0.018f), new Vector2(0.61f, 0.090f),
            new Color(0.47f, 0.11f, 0.24f, 0.97f), out label);
        idleExitRect = quit.GetComponent<RectTransform>();
        idleExitText = label;
        idleExitText.text = Label("quit");
        quit.onClick.AddListener(QuitFromVersionDialog);
        idleOverlay.SetActive(false);
    }

#if UNITY_EDITOR
    // Invoked by the existing Design > UI & UX > UX Editor command.
    // Simulate only the elapsed duration; retain the same menu/lobby/version
    // guard as the real timer. Never bypass a live online session.
    public bool EditorSimulateFiveMinutesOfMenuIdle()
    {
        if (!Application.isPlaying || !IsEligibleForMenuIdle())
        {
            Debug.LogWarning("ATB_MENU_IDLE_QA rejected=true " +
                "reason=not_in_idle_eligible_main_menu", this);
            return false;
        }
        lastUserActionAt = Time.unscaledTime - IdleAfterSeconds - 1f;
        EnterMenuIdle();
        Debug.Log("ATB_MENU_IDLE_QA simulated=300s entered=" + idleActive +
            " active_lobby=false", this);
        return idleActive;
    }
#endif

    private async Task WakeFromMenuIdleAsync()
    {
        if (!idleActive || idleWakeInProgress) return;
        idleWakeInProgress = true;
        int generation = ++idleWakeGeneration;
        // Keep the account and credentials untouched. REST has no permanent
        // socket to reconnect; revalidate /gameversion before enabling UI.
        idleActive = false;
        if (idleOverlay != null) idleOverlay.SetActive(false);
        lastUserActionAt = Time.unscaledTime;
        idleNextUiScanAt = Time.unscaledTime + 1f;
        decision = VersionDecision.Checking;
        ShowPolicyNotice(VersionDecision.Checking);
        nextCheckAt = 0f;
        try
        {
            await RefreshPolicyAsync();
            if (this == null || generation != idleWakeGeneration) return;
            // Use existing authentication on first subsequent online request.
            // Never create another Guest UID or sign the current user out.
            Debug.Log("ATB_MENU_IDLE resumed=true version=" + decision +
                " auth_uid_preserved=true " +
                "room_join_requires_existing_policy=true");
        }
        finally
        {
            if (this != null) idleWakeInProgress = false;
        }
    }

}
