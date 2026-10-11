using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phase 16H.3 loading/bootstrap UX.
///
/// This component is self-bootstrapping and does not require a scene edit.
/// It performs real startup work, rotates localized tips, logs phase timings,
/// exposes only ATBxxx support codes to players, and covers the lobby->match
/// transition until the first authoritative match snapshot arrives.
///
/// It does not change snapshot cadence, reconnect, host migration, authority,
/// gameplay rules or backend security.
/// </summary>
[DefaultExecutionOrder(-31000)]
[DisallowMultipleComponent]
public sealed class AtlasBoardLoadingExperience : MonoBehaviour
{
    private enum LoadingMode
    {
        None = 0,
        Startup = 1,
        Match = 2
    }

    private sealed class LocalizedLine
    {
        public readonly string En;
        public readonly string Tr;
        public readonly string Es;
        public readonly string Fr;
        public readonly string De;
        public readonly string Ko;
        public readonly string Ru;

        public LocalizedLine(
            string en,
            string tr,
            string es,
            string fr,
            string de,
            string ko,
            string ru)
        {
            En = en;
            Tr = tr;
            Es = es;
            Fr = fr;
            De = de;
            Ko = ko;
            Ru = ru;
        }

        public string Get()
        {
            string language =
                AtlasBoardLocalizationManager.Instance != null
                    ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
                    : "en";

            return (language ?? "en").Trim().ToLowerInvariant() switch
            {
                "tr" => Tr,
                "es" => Es,
                "fr" => Fr,
                "de" => De,
                "ko" => Ko,
                "ru" => Ru,
                _ => En
            };
        }
    }

    private static AtlasBoardLoadingExperience instance;

    private readonly List<LocalizedLine> tips =
        new List<LocalizedLine>();

    private Canvas canvas;
    private GameObject blocker;
    private Image progressFill;
    private TMP_Text titleText;
    private TMP_Text statusText;
    private TMP_Text tipText;
    private TMP_Text codeText;
    private TMP_Text retryText;
    private TMP_Text continueText;
    private Button retryButton;
    private Button continueButton;
    private GameObject toastRoot;
    private TMP_Text toastText;

    private AtlasBoardLobbyRuntimeBridge lobbyBridge;
    private AtlasBoardMatchRuntimeBridge matchBridge;
    private AtlasBoardProgressionBridge progressionBridge;
    private AtlasBoardLobbyRuntimeBridge subscribedLobbyBridge;
    private AtlasBoardMatchRuntimeBridge subscribedMatchBridge;

    private LoadingMode mode;
    private float modeStartedAt;
    private int lastDelayBucket;
    private int tipIndex;
    private float nextTipAt;
    private float toastHideAt;
    private float nextConnectivityCheckAt;
    private bool connectivityCodeVisible;
    private bool startupRunning;
    private bool startupComplete;
    private int startupGeneration;
    private string trackedMatchId = string.Empty;
    private int matchGeneration;
    private bool matchCompletionSignaled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("AtlasBoard_Loading_Experience");
        DontDestroyOnLoad(root);
        root.AddComponent<AtlasBoardLoadingExperience>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        BuildTips();
        BuildUi();

        AtlasBoardLocalizationManager.LanguageChanged -= HandleLanguageChanged;
        AtlasBoardLocalizationManager.LanguageChanged += HandleLanguageChanged;

        AtlasBoardFrontendErrorCodes.PublicCodeRaised -= HandlePublicCodeRaised;
        AtlasBoardFrontendErrorCodes.PublicCodeRaised += HandlePublicCodeRaised;

        BeginLoadingMode(LoadingMode.Startup);
        SetProgress(0.08f);
        SetStatus(L(
            "Preparing Atlas Board...",
            "Atlas Board hazırlanıyor...",
            "Preparando Atlas Board...",
            "Préparation d'Atlas Board...",
            "Atlas Board wird vorbereitet...",
            "Atlas Board를 준비하는 중...",
            "Подготовка Atlas Board..."));

        Debug.Log("ATB_BOOTSTRAP event=startup_begin");
    }

    private IEnumerator Start()
    {
        yield return null;
        ResolveRuntimeBridges();
        _ = RunStartupAsync();
    }

    private void Update()
    {
        UpdateTipRotation();
        UpdateDelayThresholds();
        UpdateToast();
        UpdateConnectivitySignal();

        if (startupComplete)
        {
            ResolveRuntimeBridges();
            ObserveMatchTransition();
        }
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        AtlasBoardLocalizationManager.LanguageChanged -= HandleLanguageChanged;
        AtlasBoardFrontendErrorCodes.PublicCodeRaised -= HandlePublicCodeRaised;
        UnbindBridges();
        instance = null;
    }

    private async Task RunStartupAsync()
    {
        if (startupRunning)
            return;

        startupRunning = true;
        int generation = ++startupGeneration;
        BeginLoadingMode(LoadingMode.Startup);

        float totalStartedAt = Time.realtimeSinceStartup;

        try
        {
            SetProgress(0.12f);
            SetStatus(L(
                "Connecting to online services...",
                "Çevrimiçi hizmetlere bağlanılıyor...",
                "Conectando con los servicios en línea...",
                "Connexion aux services en ligne...",
                "Verbindung mit Onlinediensten...",
                "온라인 서비스에 연결하는 중...",
                "Подключение к онлайн-сервисам..."));

            AtlasBoardLobbyRuntimeBridge lobby =
                await WaitForSceneComponentAsync<AtlasBoardLobbyRuntimeBridge>(6f);

            if (generation != startupGeneration)
                return;

            if (lobby == null)
            {
                FailStartup(
                    AtlasBoardFrontendErrorCodes.StartupUnavailable,
                    "lobby_bridge_missing",
                    totalStartedAt);
                return;
            }

            lobbyBridge = lobby;
            BindBridges();

            float identityStartedAt = Time.realtimeSinceStartup;
            bool identityCompleted = false;
            bool identityReady = false;

            try
            {
                Task<bool> identityTask = lobbyBridge.EnsureOnlineIdentityAsync();
                Task winner = await Task.WhenAny(identityTask, Task.Delay(25000));
                identityCompleted = winner == identityTask;

                if (identityCompleted)
                    identityReady = await identityTask;
            }
            catch (Exception exception)
            {
                LogPhase(
                    "identity",
                    false,
                    identityStartedAt,
                    AtlasBoardFrontendErrorCodes.SessionPreparationFailed,
                    exception.GetType().Name);
            }

            if (generation != startupGeneration)
                return;

            if (!identityCompleted)
            {
                LogPhase(
                    "identity",
                    false,
                    identityStartedAt,
                    AtlasBoardFrontendErrorCodes.RequestTimedOut,
                    "startup_identity_timeout");

                FailStartup(
                    AtlasBoardFrontendErrorCodes.RequestTimedOut,
                    "startup_identity_timeout",
                    totalStartedAt);
                return;
            }

            if (!identityReady)
            {
                LogPhase(
                    "identity",
                    false,
                    identityStartedAt,
                    AtlasBoardFrontendErrorCodes.SessionPreparationFailed,
                    "identity_ready_false");

                FailStartup(
                    AtlasBoardFrontendErrorCodes.SessionPreparationFailed,
                    "identity_ready_false",
                    totalStartedAt);
                return;
            }

            LogPhase("identity", true, identityStartedAt, "", "");

            SetProgress(0.46f);
            SetStatus(L(
                "Preparing your player profile...",
                "Oyuncu profiliniz hazırlanıyor...",
                "Preparando tu perfil de jugador...",
                "Préparation de votre profil joueur...",
                "Spielerprofil wird vorbereitet...",
                "플레이어 프로필을 준비하는 중...",
                "Подготовка профиля игрока..."));

            ResolveRuntimeBridges();

            Task profileTask = WarmProfileAsync(generation);
            Task discoveryTask = WarmLobbyDiscoveryAsync(generation);
            await Task.WhenAll(profileTask, discoveryTask);

            if (generation != startupGeneration)
                return;

            SetProgress(0.86f);
            SetStatus(L(
                "Preparing game data...",
                "Oyun verileri hazırlanıyor...",
                "Preparando los datos del juego...",
                "Préparation des données de jeu...",
                "Spieldaten werden vorbereitet...",
                "게임 데이터를 준비하는 중...",
                "Подготовка игровых данных..."));

            await Task.Delay(120);

            if (generation != startupGeneration)
                return;

            SetProgress(1f);
            SetStatus(L(
                "Ready",
                "Hazır",
                "Listo",
                "Prêt",
                "Bereit",
                "준비 완료",
                "Готово"));

            float totalMs =
                (Time.realtimeSinceStartup - totalStartedAt) * 1000f;

            Debug.Log(
                "ATB_BOOTSTRAP event=startup_ready totalMs=" +
                totalMs.ToString("F0") +
                " identityReady=true");

            startupComplete = true;
            await Task.Delay(180);

            if (generation == startupGeneration)
                HideBlockingOverlay();
        }
        catch (Exception exception)
        {
            if (generation == startupGeneration)
            {
                FailStartup(
                    AtlasBoardFrontendErrorCodes.UnexpectedClientFailure,
                    exception.GetType().Name,
                    totalStartedAt);
            }
        }
        finally
        {
            if (generation == startupGeneration)
                startupRunning = false;
        }
    }

    private async Task WarmProfileAsync(int generation)
    {
        ResolveRuntimeBridges();

        if (progressionBridge == null)
        {
            Debug.Log(
                "ATB_BOOTSTRAP phase=profile status=skipped reason=bridge_unavailable");
            return;
        }

        float startedAt = Time.realtimeSinceStartup;

        try
        {
            Task<AtlasBoardProgressionSnapshot> task =
                progressionBridge.GetProfileAsync();

            Task winner = await Task.WhenAny(task, Task.Delay(12000));

            if (generation != startupGeneration)
                return;

            if (winner != task)
            {
                LogPhase(
                    "profile",
                    false,
                    startedAt,
                    AtlasBoardFrontendErrorCodes.StartupProfileFailed,
                    "optional_profile_timeout");
                return;
            }

            AtlasBoardProgressionSnapshot snapshot = await task;

            if (snapshot == null || !snapshot.Success)
            {
                LogPhase(
                    "profile",
                    false,
                    startedAt,
                    AtlasBoardFrontendErrorCodes.StartupProfileFailed,
                    "optional_profile_not_ready");
                return;
            }

            LogPhase("profile", true, startedAt, "", "");
        }
        catch (Exception exception)
        {
            LogPhase(
                "profile",
                false,
                startedAt,
                AtlasBoardFrontendErrorCodes.StartupProfileFailed,
                exception.GetType().Name);
        }
    }

    private async Task WarmLobbyDiscoveryAsync(int generation)
    {
        // Only Development/Editor pays a speculative public-lobby read to warm
        // the local QA path. Release builds do not add this extra request.
        if (!Debug.isDebugBuild && !Application.isEditor)
        {
            Debug.Log(
                "ATB_BOOTSTRAP phase=lobby_prefetch status=skipped " +
                "reason=release_no_speculative_call");
            return;
        }

        if (lobbyBridge == null)
            return;

        float startedAt = Time.realtimeSinceStartup;

        try
        {
            Task<AtlasPublicLobbyListResult> task =
                lobbyBridge.ListPublicRoomsAsync(6);

            Task winner = await Task.WhenAny(task, Task.Delay(12000));

            if (generation != startupGeneration)
                return;

            if (winner != task)
            {
                LogPhase(
                    "lobby_prefetch",
                    false,
                    startedAt,
                    AtlasBoardFrontendErrorCodes.StartupOnlineDataFailed,
                    "optional_lobby_prefetch_timeout");
                return;
            }

            AtlasPublicLobbyListResult result = await task;

            if (result == null || !result.Success)
            {
                LogPhase(
                    "lobby_prefetch",
                    false,
                    startedAt,
                    AtlasBoardFrontendErrorCodes.StartupOnlineDataFailed,
                    "optional_lobby_prefetch_not_ready");
                return;
            }

            LogPhase(
                "lobby_prefetch",
                true,
                startedAt,
                "",
                "rooms=" + (result.Rooms != null ? result.Rooms.Count.ToString() : "0"));
        }
        catch (Exception exception)
        {
            LogPhase(
                "lobby_prefetch",
                false,
                startedAt,
                AtlasBoardFrontendErrorCodes.StartupOnlineDataFailed,
                exception.GetType().Name);
        }
    }

    private void ObserveMatchTransition()
    {
        if (lobbyBridge == null)
            return;

        AtlasLobbySnapshot lobby = lobbyBridge.CurrentSnapshot;

        if (lobby == null || string.IsNullOrWhiteSpace(lobby.MatchId))
            return;

        bool transition =
            lobby.LifecycleState == AtlasRoomLifecycleState.Starting ||
            lobby.LifecycleState == AtlasRoomLifecycleState.InMatch;

        if (!transition)
            return;

        AtlasMatchNetworkSnapshot match =
            matchBridge != null ? matchBridge.CurrentSnapshot : null;

        bool authoritativeReady =
            match != null &&
            string.Equals(match.MatchId, lobby.MatchId, StringComparison.Ordinal);

        if (authoritativeReady)
        {
            if (mode == LoadingMode.Match &&
                string.Equals(trackedMatchId, lobby.MatchId, StringComparison.Ordinal))
            {
                CompleteMatchLoading(match);
            }
            return;
        }

        if (mode != LoadingMode.Match ||
            !string.Equals(trackedMatchId, lobby.MatchId, StringComparison.Ordinal))
        {
            BeginMatchLoading(lobby.MatchId);
        }
    }

    private void HandleLobbySnapshotChanged(AtlasLobbySnapshot snapshot)
    {
        if (!startupComplete || snapshot == null)
            return;

        ObserveMatchTransition();
    }

    private void HandleMatchSnapshotChanged(AtlasMatchNetworkSnapshot snapshot)
    {
        if (mode != LoadingMode.Match ||
            snapshot == null ||
            string.IsNullOrWhiteSpace(trackedMatchId) ||
            !string.Equals(trackedMatchId, snapshot.MatchId, StringComparison.Ordinal))
        {
            return;
        }

        CompleteMatchLoading(snapshot);
    }

    private void BeginMatchLoading(string matchId)
    {
        if (string.IsNullOrWhiteSpace(matchId))
            return;

        trackedMatchId = matchId;
        matchGeneration++;
        matchCompletionSignaled = false;

        BeginLoadingMode(LoadingMode.Match);
        SetProgress(0.28f);
        SetStatus(L(
            "Synchronizing players...",
            "Oyuncular senkronize ediliyor...",
            "Sincronizando jugadores...",
            "Synchronisation des joueurs...",
            "Spieler werden synchronisiert...",
            "플레이어를 동기화하는 중...",
            "Синхронизация игроков..."));

        Debug.Log("ATB_MATCH_LOAD event=begin");
    }

    private async void CompleteMatchLoading(AtlasMatchNetworkSnapshot snapshot)
    {
        if (mode != LoadingMode.Match ||
            matchCompletionSignaled)
        {
            return;
        }

        // SnapshotChanged can fire several times during the short overlay-close
        // delay. Completion is a presentation event, so accept only the first
        // authoritative snapshot for this loading cycle. This does not alter
        // snapshot polling, networking, reconnect or host-migration behavior.
        matchCompletionSignaled = true;

        int generation = matchGeneration;
        float elapsedMs = (Time.realtimeSinceStartup - modeStartedAt) * 1000f;

        SetProgress(1f);
        SetStatus(L(
            "Match ready",
            "Maç hazır",
            "Partida lista",
            "Partie prête",
            "Spiel bereit",
            "경기 준비 완료",
            "Матч готов"));

        Debug.Log(
            "ATB_MATCH_LOAD event=authoritative_ready elapsedMs=" +
            elapsedMs.ToString("F0") +
            " revision=" + snapshot.Revision);

        await Task.Delay(160);

        if (this != null &&
            generation == matchGeneration &&
            mode == LoadingMode.Match)
        {
            HideBlockingOverlay();
        }
    }

    private async void RetryCurrent()
    {
        if (retryButton != null)
            retryButton.interactable = false;

        try
        {
            if (mode == LoadingMode.Startup)
            {
                startupRunning = false;
                _ = RunStartupAsync();
                return;
            }

            if (mode == LoadingMode.Match)
            {
                ResolveRuntimeBridges();

                if (matchBridge == null ||
                    string.IsNullOrWhiteSpace(trackedMatchId))
                {
                    AtlasBoardFrontendErrorCodes.Report(
                        AtlasBoardFrontendErrorCodes.MatchInitializationFailed,
                        "retry_match_bridge_unavailable",
                        "match_retry");
                    return;
                }

                float startedAt = Time.realtimeSinceStartup;

                AtlasMatchNetworkResult result =
                    await matchBridge.RefreshSnapshotNowAsync(trackedMatchId);

                LogPhase(
                    "match_retry_snapshot",
                    result != null && result.Success,
                    startedAt,
                    result != null && result.Success
                        ? ""
                        : AtlasBoardFrontendErrorCodes.MatchSnapshotFailed,
                    result != null
                        ? result.ErrorLocalizationKey
                        : "null_result");
            }
        }
        catch (Exception exception)
        {
            AtlasBoardFrontendErrorCodes.Report(
                mode == LoadingMode.Match
                    ? AtlasBoardFrontendErrorCodes.MatchSnapshotFailed
                    : AtlasBoardFrontendErrorCodes.StartupUnavailable,
                exception.GetType().Name,
                "loading_retry");
        }
        finally
        {
            if (retryButton != null)
                retryButton.interactable = true;
        }
    }

    private void ContinueWithoutBlocking()
    {
        Debug.LogWarning(
            "ATB_LOADING event=user_continue mode=" + mode +
            " elapsedMs=" +
            ((Time.realtimeSinceStartup - modeStartedAt) * 1000f).ToString("F0"));

        HideBlockingOverlay();
    }

    private void FailStartup(string code, string detail, float totalStartedAt)
    {
        float elapsedMs =
            (Time.realtimeSinceStartup - totalStartedAt) * 1000f;

        AtlasBoardFrontendErrorCodes.Report(
            code,
            detail,
            "startup",
            AtlasBoardFrontendErrorSeverity.Blocking);

        SetCode(code);
        SetStatus(AtlasBoardFrontendErrorCodes.PublicMessage(code));

        retryButton.gameObject.SetActive(true);
        continueButton.gameObject.SetActive(true);

        Debug.LogWarning(
            "ATB_BOOTSTRAP event=startup_failed totalMs=" +
            elapsedMs.ToString("F0") +
            " code=" + code);
    }

    private void UpdateDelayThresholds()
    {
        if (mode == LoadingMode.None ||
            blocker == null ||
            !blocker.activeSelf)
        {
            return;
        }

        float elapsed = Time.realtimeSinceStartup - modeStartedAt;
        int bucket = Mathf.FloorToInt(elapsed / 10f);

        if (bucket <= 0 || bucket == lastDelayBucket)
            return;

        lastDelayBucket = bucket;
        string code;

        if (mode == LoadingMode.Startup)
        {
            code = bucket == 1
                ? AtlasBoardFrontendErrorCodes.StartupDelay10
                : bucket == 2
                    ? AtlasBoardFrontendErrorCodes.StartupDelay20
                    : AtlasBoardFrontendErrorCodes.StartupDelay30;
        }
        else
        {
            code = bucket == 1
                ? AtlasBoardFrontendErrorCodes.MatchInitialSyncDelay
                : bucket == 2
                    ? AtlasBoardFrontendErrorCodes.MatchExtendedSyncDelay
                    : AtlasBoardFrontendErrorCodes.MatchRecoveryDelay;
        }

        SetCode(code);

        Debug.LogWarning(
            "ATB_LOADING_DELAY mode=" + mode +
            " bucket10s=" + bucket +
            " elapsedMs=" + (elapsed * 1000f).ToString("F0") +
            " code=" + code);

        if (bucket >= 2)
            retryButton.gameObject.SetActive(true);

        if (bucket >= 3)
            continueButton.gameObject.SetActive(true);
    }

    private void UpdateTipRotation()
    {
        if (blocker == null || !blocker.activeSelf || tips.Count == 0)
            return;

        if (Time.unscaledTime < nextTipAt)
            return;

        nextTipAt = Time.unscaledTime + 3f;
        tipIndex = (tipIndex + 7) % tips.Count;

        tipText.text = tips[tipIndex].Get();
        ApplyLocalizedFont(tipText);
    }

    private void UpdateToast()
    {
        if (toastRoot != null &&
            toastRoot.activeSelf &&
            Time.unscaledTime >= toastHideAt)
        {
            toastRoot.SetActive(false);
        }
    }

    private void UpdateConnectivitySignal()
    {
        if (!startupComplete || Time.unscaledTime < nextConnectivityCheckAt)
            return;

        nextConnectivityCheckAt = Time.unscaledTime + 2.5f;

        bool unreachable =
            Application.internetReachability == NetworkReachability.NotReachable;

        if (unreachable && !connectivityCodeVisible)
        {
            connectivityCodeVisible = true;
            AtlasBoardFrontendErrorCodes.Report(
                AtlasBoardFrontendErrorCodes.NetworkUnavailable,
                "os_reachability_not_reachable",
                "runtime_connectivity");
        }
        else if (!unreachable && connectivityCodeVisible)
        {
            connectivityCodeVisible = false;
            Debug.Log("ATB_CONNECTIVITY event=reachability_recovered");
        }
    }

    private void HandlePublicCodeRaised(string code, string message)
    {
        if (toastRoot == null || toastText == null)
            return;

        toastText.text = message ?? code ?? "ATB990";
        ApplyLocalizedFont(toastText);
        toastRoot.SetActive(true);
        toastHideAt = Time.unscaledTime + 8f;
    }

    private void HandleLanguageChanged()
    {
        RefreshStaticLocalizedUi();

        if (tips.Count > 0 && tipText != null)
            tipText.text = tips[tipIndex % tips.Count].Get();

        ApplyLocalizedFont(titleText);
        ApplyLocalizedFont(statusText);
        ApplyLocalizedFont(tipText);
        ApplyLocalizedFont(codeText);
        ApplyLocalizedFont(toastText);
        ApplyLocalizedFont(retryText);
        ApplyLocalizedFont(continueText);
    }

    private void RefreshStaticLocalizedUi()
    {
        if (retryText != null)
        {
            retryText.text = L(
                "Retry",
                "Tekrar Dene",
                "Reintentar",
                "Réessayer",
                "Erneut versuchen",
                "다시 시도",
                "Повторить");
        }

        if (continueText != null)
        {
            continueText.text = L(
                "Continue",
                "Devam Et",
                "Continuar",
                "Continuer",
                "Fortfahren",
                "계속",
                "Продолжить");
        }

        if (titleText != null)
        {
            titleText.text = mode == LoadingMode.Match
                ? L(
                    "Starting Match",
                    "Maç Başlatılıyor",
                    "Iniciando partida",
                    "Démarrage de la partie",
                    "Spiel wird gestartet",
                    "경기 시작 중",
                    "Запуск матча")
                : "Atlas Board";
        }
    }

    private void BeginLoadingMode(LoadingMode newMode)
    {
        mode = newMode;
        modeStartedAt = Time.realtimeSinceStartup;
        lastDelayBucket = 0;

        tipIndex = UnityEngine.Random.Range(0, Mathf.Max(1, tips.Count));
        nextTipAt = 0f;

        SetCode(string.Empty);

        if (retryButton != null)
            retryButton.gameObject.SetActive(false);

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

        if (blocker != null)
            blocker.SetActive(true);

        RefreshStaticLocalizedUi();
        UpdateTipRotation();
    }

    private void HideBlockingOverlay()
    {
        if (blocker != null)
            blocker.SetActive(false);

        mode = LoadingMode.None;
        matchCompletionSignaled = false;
        SetCode(string.Empty);
    }

    private void SetStatus(string value)
    {
        if (statusText == null)
            return;

        statusText.text = value ?? string.Empty;
        ApplyLocalizedFont(statusText);
    }

    private void SetCode(string code)
    {
        if (codeText == null)
            return;

        codeText.text =
            string.IsNullOrWhiteSpace(code)
                ? string.Empty
                : "CODE " + code;

        codeText.gameObject.SetActive(!string.IsNullOrWhiteSpace(code));
    }

    private void SetProgress(float progress)
    {
        if (progressFill != null)
            progressFill.fillAmount = Mathf.Clamp01(progress);
    }

    private void ResolveRuntimeBridges()
    {
        if (lobbyBridge == null)
            lobbyBridge = FindSceneComponent<AtlasBoardLobbyRuntimeBridge>();

        if (matchBridge == null)
            matchBridge = FindSceneComponent<AtlasBoardMatchRuntimeBridge>();

        if (progressionBridge == null)
            progressionBridge = FindSceneComponent<AtlasBoardProgressionBridge>();

        BindBridges();
    }

    private void BindBridges()
    {
        if (subscribedLobbyBridge != lobbyBridge)
        {
            if (subscribedLobbyBridge != null)
                subscribedLobbyBridge.SnapshotChanged -= HandleLobbySnapshotChanged;

            subscribedLobbyBridge = lobbyBridge;

            if (subscribedLobbyBridge != null)
                subscribedLobbyBridge.SnapshotChanged += HandleLobbySnapshotChanged;
        }

        if (subscribedMatchBridge != matchBridge)
        {
            if (subscribedMatchBridge != null)
                subscribedMatchBridge.SnapshotChanged -= HandleMatchSnapshotChanged;

            subscribedMatchBridge = matchBridge;

            if (subscribedMatchBridge != null)
                subscribedMatchBridge.SnapshotChanged += HandleMatchSnapshotChanged;
        }
    }

    private void UnbindBridges()
    {
        if (subscribedLobbyBridge != null)
            subscribedLobbyBridge.SnapshotChanged -= HandleLobbySnapshotChanged;

        if (subscribedMatchBridge != null)
            subscribedMatchBridge.SnapshotChanged -= HandleMatchSnapshotChanged;

        subscribedLobbyBridge = null;
        subscribedMatchBridge = null;
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        T[] candidates = Resources.FindObjectsOfTypeAll<T>();

        foreach (T candidate in candidates)
        {
            if (candidate != null &&
                candidate.gameObject != null &&
                candidate.gameObject.scene.IsValid())
            {
                return candidate;
            }
        }

        return null;
    }

    private static async Task<T> WaitForSceneComponentAsync<T>(
        float timeoutSeconds)
        where T : Component
    {
        float startedAt = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup - startedAt < timeoutSeconds)
        {
            T candidate = FindSceneComponent<T>();
            if (candidate != null)
                return candidate;

            await Task.Delay(80);
        }

        return FindSceneComponent<T>();
    }

    private static void LogPhase(
        string phase,
        bool success,
        float startedAt,
        string code,
        string note)
    {
        float durationMs =
            (Time.realtimeSinceStartup - startedAt) * 1000f;

        string safeNote =
            AtlasBoardFrontendErrorCodes.SanitizeForLog(note);

        string line =
            "ATB_BOOTSTRAP phase=" + phase +
            " durationMs=" + durationMs.ToString("F0") +
            " success=" + success.ToString().ToLowerInvariant() +
            (string.IsNullOrWhiteSpace(code) ? "" : " code=" + code) +
            (string.IsNullOrWhiteSpace(safeNote) ? "" : " note=" + safeNote);

        if (success)
            Debug.Log(line);
        else
            Debug.LogWarning(line);
    }

    private static string L(
        string en,
        string tr,
        string es,
        string fr,
        string de,
        string ko,
        string ru)
    {
        return AtlasBoardL.R(en, tr, es, fr, de, ko, ru);
    }

    private static void ApplyLocalizedFont(TMP_Text text)
    {
        if (text == null)
            return;

        AtlasBoardLocalizationManager manager =
            AtlasBoardLocalizationManager.Instance;

        if (manager == null)
            return;

        TMP_FontAsset resolved = manager.ResolveFont(text.font);
        if (resolved != null)
            text.font = resolved;
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject(
            "AtlasBoard_LoadingCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        blocker = CreatePanel(
            canvasObject.transform,
            "LoadingBlocker",
            new Color(0.035f, 0.045f, 0.065f, 0.985f));

        StretchFull(blocker.GetComponent<RectTransform>());

        GameObject center = new GameObject("Center", typeof(RectTransform));
        center.transform.SetParent(blocker.transform, false);

        RectTransform centerRect = center.GetComponent<RectTransform>();
        centerRect.anchorMin = new Vector2(0.5f, 0.5f);
        centerRect.anchorMax = new Vector2(0.5f, 0.5f);
        centerRect.pivot = new Vector2(0.5f, 0.5f);
        centerRect.sizeDelta = new Vector2(900f, 520f);
        centerRect.anchoredPosition = Vector2.zero;

        titleText = CreateText(center.transform, "Title", 48f, TextAlignmentOptions.Center);
        SetRect(titleText.rectTransform, new Vector2(0f, 155f), new Vector2(900f, 80f));

        statusText = CreateText(center.transform, "Status", 27f, TextAlignmentOptions.Center);
        SetRect(statusText.rectTransform, new Vector2(0f, 70f), new Vector2(900f, 60f));

        GameObject progressBack = CreatePanel(
            center.transform,
            "ProgressBack",
            new Color(1f, 1f, 1f, 0.14f));

        SetRect(
            progressBack.GetComponent<RectTransform>(),
            new Vector2(0f, 12f),
            new Vector2(650f, 12f));

        GameObject progress = CreatePanel(
            progressBack.transform,
            "ProgressFill",
            new Color(0.84f, 0.69f, 0.25f, 1f));

        StretchFull(progress.GetComponent<RectTransform>());
        progressFill = progress.GetComponent<Image>();
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Horizontal;
        progressFill.fillOrigin = 0;
        progressFill.fillAmount = 0f;

        tipText = CreateText(center.transform, "Tip", 21f, TextAlignmentOptions.Center);
        tipText.fontStyle = FontStyles.Italic;
        tipText.color = new Color(0.84f, 0.86f, 0.90f, 1f);
        SetRect(tipText.rectTransform, new Vector2(0f, -78f), new Vector2(820f, 90f));

        codeText = CreateText(center.transform, "SupportCode", 18f, TextAlignmentOptions.Center);
        codeText.color = new Color(1f, 0.79f, 0.34f, 1f);
        SetRect(codeText.rectTransform, new Vector2(0f, -152f), new Vector2(820f, 45f));

        retryButton = CreateButton(center.transform, "RetryButton", out retryText);
        SetRect(
            retryButton.GetComponent<RectTransform>(),
            new Vector2(-125f, -215f),
            new Vector2(220f, 52f));
        retryButton.onClick.AddListener(RetryCurrent);

        continueButton = CreateButton(center.transform, "ContinueButton", out continueText);
        SetRect(
            continueButton.GetComponent<RectTransform>(),
            new Vector2(125f, -215f),
            new Vector2(220f, 52f));
        continueButton.onClick.AddListener(ContinueWithoutBlocking);

        retryButton.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(false);

        toastRoot = CreatePanel(
            canvasObject.transform,
            "ErrorCodeToast",
            new Color(0.08f, 0.09f, 0.12f, 0.96f));

        RectTransform toastRect = toastRoot.GetComponent<RectTransform>();
        toastRect.anchorMin = new Vector2(1f, 1f);
        toastRect.anchorMax = new Vector2(1f, 1f);
        toastRect.pivot = new Vector2(1f, 1f);
        toastRect.sizeDelta = new Vector2(500f, 105f);
        toastRect.anchoredPosition = new Vector2(-30f, -30f);

        toastText = CreateText(
            toastRoot.transform,
            "ToastText",
            18f,
            TextAlignmentOptions.Left);

        StretchFull(toastText.rectTransform);
        toastText.rectTransform.offsetMin = new Vector2(20f, 14f);
        toastText.rectTransform.offsetMax = new Vector2(-20f, -14f);

        toastRoot.SetActive(false);
        RefreshStaticLocalizedUi();
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image));

        panel.transform.SetParent(parent, false);

        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject item = new GameObject(
            name,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));

        item.transform.SetParent(parent, false);

        TextMeshProUGUI text = item.GetComponent<TextMeshProUGUI>();
        text.text = string.Empty;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        return text;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        out TMP_Text label)
    {
        GameObject buttonObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));

        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.17f, 0.19f, 0.24f, 1f);

        Button button = buttonObject.GetComponent<Button>();

        label = CreateText(
            buttonObject.transform,
            "Label",
            19f,
            TextAlignmentOptions.Center);

        StretchFull(label.rectTransform);
        return button;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void BuildTips()
    {
        tips.Clear();

        AddTip(
            "Complete regions are easier to develop efficiently.",
            "Tam bölgeler daha verimli geliştirilebilir.",
            "Las regiones completas se desarrollan con mayor eficiencia.",
            "Les régions complètes sont plus faciles à développer efficacement.",
            "Vollständige Regionen lassen sich effizienter entwickeln.",
            "완성된 지역은 더 효율적으로 개발할 수 있습니다.",
            "Полные регионы проще развивать эффективно.");
        AddTip(
            "Keep enough cash available for unexpected payments.",
            "Beklenmedik ödemeler için yeterli nakit tutun.",
            "Mantén efectivo disponible para pagos inesperados.",
            "Gardez assez de liquidités pour les paiements imprévus.",
            "Halte genug Bargeld für unerwartete Zahlungen bereit.",
            "예상치 못한 지출에 대비해 충분한 현금을 남겨 두세요.",
            "Оставляйте достаточно наличных на непредвиденные платежи.");
        AddTip(
            "A strong trade can be more valuable than a lucky roll.",
            "İyi bir takas, şanslı bir zardan daha değerli olabilir.",
            "Un buen intercambio puede valer más que una tirada afortunada.",
            "Un bon échange peut valoir plus qu'un lancer chanceux.",
            "Ein guter Handel kann wertvoller sein als ein glücklicher Wurf.",
            "좋은 거래는 운 좋은 주사위보다 더 가치 있을 수 있습니다.",
            "Удачная сделка может быть ценнее удачного броска.");
        AddTip(
            "Watch every player's cash before making an aggressive offer.",
            "Agresif teklif vermeden önce herkesin nakdini kontrol edin.",
            "Observa el efectivo de cada jugador antes de hacer una oferta agresiva.",
            "Observez l'argent de chaque joueur avant une offre agressive.",
            "Prüfe vor einem aggressiven Angebot das Bargeld aller Spieler.",
            "공격적인 제안을 하기 전에 모든 플레이어의 현금을 확인하세요.",
            "Перед агрессивным предложением проверьте деньги всех игроков.");
        AddTip(
            "Doubles may give you another roll when that rule is enabled.",
            "Kural açıksa çift zar size bir atış daha verebilir.",
            "Los dobles pueden darte otra tirada si la regla está activada.",
            "Un double peut vous donner un autre lancer si la règle est active.",
            "Ein Pasch kann einen weiteren Wurf geben, wenn die Regel aktiv ist.",
            "규칙이 켜져 있으면 더블은 추가 주사위 기회를 줄 수 있습니다.",
            "Дубль может дать ещё один бросок, если правило включено.");
        AddTip(
            "Three consecutive doubles can be risky when the penalty rule is active.",
            "Üç ardışık çift, ceza kuralı açıksa risklidir.",
            "Tres dobles seguidos pueden ser arriesgados con la penalización activa.",
            "Trois doubles consécutifs peuvent être risqués si la pénalité est active.",
            "Drei Pasche in Folge können bei aktiver Strafregel riskant sein.",
            "연속 세 번 더블은 페널티 규칙이 켜져 있으면 위험할 수 있습니다.",
            "Три дубля подряд опасны, если включено правило штрафа.");
        AddTip(
            "Do not spend all your cash just because a property is available.",
            "Bir mülk boş diye tüm paranızı harcamayın.",
            "No gastes todo tu efectivo solo porque una propiedad esté disponible.",
            "Ne dépensez pas tout votre argent simplement parce qu'une propriété est libre.",
            "Gib nicht dein gesamtes Geld aus, nur weil ein Grundstück verfügbar ist.",
            "매물이 있다고 해서 현금을 전부 쓰지는 마세요.",
            "Не тратьте все деньги только потому, что объект доступен.");
        AddTip(
            "A balanced portfolio can survive bad turns better than a fragile one.",
            "Dengeli bir portföy kötü turlara daha dayanıklıdır.",
            "Una cartera equilibrada resiste mejor las malas rondas.",
            "Un portefeuille équilibré résiste mieux aux mauvais tours.",
            "Ein ausgewogenes Portfolio übersteht schlechte Runden besser.",
            "균형 잡힌 자산은 나쁜 턴을 더 잘 버틸 수 있습니다.",
            "Сбалансированный портфель лучше переживает неудачные ходы.");
        AddTip(
            "Check the board before accepting a trade that completes an opponent's region.",
            "Rakibin bölgesini tamamlayan bir takası kabul etmeden önce tahtayı kontrol edin.",
            "Revisa el tablero antes de aceptar un intercambio que complete una región rival.",
            "Vérifiez le plateau avant d'accepter un échange qui complète une région adverse.",
            "Prüfe das Brett, bevor du einen Handel annimmst, der eine gegnerische Region vervollständigt.",
            "상대의 지역을 완성해 주는 거래를 수락하기 전에 보드를 확인하세요.",
            "Проверьте поле перед сделкой, которая завершает регион соперника.");
        AddTip(
            "Auctions can create opportunities even when you skip a direct purchase.",
            "Doğrudan satın almayı geçseniz bile açık artırmalar fırsat yaratabilir.",
            "Las subastas pueden crear oportunidades aunque omitas una compra directa.",
            "Les enchères peuvent créer des opportunités même sans achat direct.",
            "Auktionen können Chancen bieten, auch wenn du den Direktkauf auslässt.",
            "직접 구매를 건너뛰어도 경매에서 기회가 생길 수 있습니다.",
            "Аукцион может дать шанс даже после отказа от прямой покупки.");
        AddTip(
            "Development is safest when your cash reserve can absorb a bad landing.",
            "Geliştirme, nakit rezerviniz kötü bir inişi karşılayabildiğinde daha güvenlidir.",
            "Desarrolla cuando tu reserva pueda soportar una mala caída.",
            "Développez lorsque votre réserve peut absorber une mauvaise case.",
            "Entwickle, wenn deine Geldreserve eine schlechte Landung verkraften kann.",
            "불리한 칸에 도착해도 버틸 현금이 있을 때 개발하는 것이 안전합니다.",
            "Развивайтесь, когда запас денег выдержит неудачное попадание.");
        AddTip(
            "A short-term cash advantage is not always a long-term board advantage.",
            "Kısa vadeli nakit üstünlüğü her zaman uzun vadeli tahta üstünlüğü değildir.",
            "Una ventaja de efectivo a corto plazo no siempre es una ventaja a largo plazo.",
            "Un avantage de trésorerie à court terme n'est pas toujours durable.",
            "Ein kurzfristiger Geldvorteil ist nicht immer ein langfristiger Brettvorteil.",
            "단기 현금 우위가 항상 장기 보드 우위는 아닙니다.",
            "Краткосрочный денежный перевес не всегда означает долгосрочное преимущество.");
        AddTip(
            "Pay attention to turn order before making a risky investment.",
            "Riskli yatırım yapmadan önce sıra düzenine dikkat edin.",
            "Mira el orden de turnos antes de hacer una inversión arriesgada.",
            "Tenez compte de l'ordre des tours avant un investissement risqué.",
            "Beachte die Zugreihenfolge vor einer riskanten Investition.",
            "위험한 투자 전에는 턴 순서를 확인하세요.",
            "Перед рискованной инвестицией учитывайте порядок ходов.");
        AddTip(
            "Use the city information panel to review a property before committing.",
            "Karar vermeden önce şehir bilgi panelinden mülkü inceleyin.",
            "Usa el panel de información de la ciudad antes de decidir.",
            "Utilisez le panneau d'information de la ville avant de décider.",
            "Nutze die Stadtinformation, bevor du dich entscheidest.",
            "결정하기 전에 도시 정보 패널을 확인하세요.",
            "Перед решением изучите объект в панели информации о городе.");
        AddTip(
            "A trade should improve your position, not only make the numbers look larger.",
            "Takas yalnız rakamları değil, konumunuzu da iyileştirmeli.",
            "Un intercambio debe mejorar tu posición, no solo aumentar los números.",
            "Un échange doit améliorer votre position, pas seulement les chiffres.",
            "Ein Handel sollte deine Position verbessern, nicht nur die Zahlen erhöhen.",
            "거래는 숫자만 키우는 것이 아니라 실제 위치를 개선해야 합니다.",
            "Сделка должна улучшать позицию, а не только увеличивать цифры.");
        AddTip(
            "Leaving some flexibility can be stronger than maximizing every turn.",
            "Biraz esneklik bırakmak her turu maksimuma zorlamaktan daha güçlü olabilir.",
            "Mantener flexibilidad puede ser mejor que maximizar cada turno.",
            "Garder de la flexibilité peut être meilleur que tout maximiser à chaque tour.",
            "Flexibilität kann stärker sein als jeden Zug maximal auszureizen.",
            "매 턴 최대치를 노리기보다 유연성을 남기는 것이 더 강할 수 있습니다.",
            "Запас гибкости иногда полезнее максимума в каждом ходу.");
        AddTip(
            "Think about what the next player can do after your turn ends.",
            "Turunuz bittikten sonra sıradaki oyuncunun ne yapabileceğini düşünün.",
            "Piensa en lo que puede hacer el siguiente jugador después de tu turno.",
            "Pensez à ce que le joueur suivant pourra faire après votre tour.",
            "Denke daran, was der nächste Spieler nach deinem Zug tun kann.",
            "내 턴이 끝난 뒤 다음 플레이어가 무엇을 할 수 있는지 생각하세요.",
            "Думайте о том, что сможет сделать следующий игрок после вашего хода.");
        AddTip(
            "Do not judge a deal only by current cash; future board access matters too.",
            "Bir anlaşmayı yalnız mevcut nakitle değerlendirmeyin; gelecekteki tahta erişimi de önemlidir.",
            "No valores un trato solo por el efectivo actual; el acceso futuro al tablero también importa.",
            "Ne jugez pas un accord uniquement sur l'argent actuel ; l'accès futur compte aussi.",
            "Bewerte einen Handel nicht nur nach Bargeld; zukünftiger Brettzugang zählt ebenfalls.",
            "거래를 현재 현금만으로 판단하지 마세요. 이후 보드 접근성도 중요합니다.",
            "Оценивайте сделку не только по деньгам: будущий доступ к полю тоже важен.");
        AddTip(
            "A safe reserve gives you more choices on your next turn.",
            "Güvenli bir nakit rezervi sonraki turda daha fazla seçenek sunar.",
            "Una reserva segura te da más opciones en tu próximo turno.",
            "Une réserve sûre vous donne plus d'options au prochain tour.",
            "Eine sichere Reserve gibt dir im nächsten Zug mehr Möglichkeiten.",
            "안전한 현금 보유는 다음 턴에 더 많은 선택지를 줍니다.",
            "Денежный резерв даёт больше вариантов на следующем ходу.");
        AddTip(
            "Observe which regions are becoming contested before you commit resources.",
            "Kaynak ayırmadan önce hangi bölgelerin rekabete açıldığını izleyin.",
            "Observa qué regiones se están disputando antes de comprometer recursos.",
            "Observez les régions disputées avant d'engager vos ressources.",
            "Beobachte umkämpfte Regionen, bevor du Ressourcen bindest.",
            "자원을 투입하기 전에 경쟁이 심해지는 지역을 살펴보세요.",
            "Следите за спорными регионами, прежде чем вкладывать ресурсы.");
        AddTip(
            "A defensive trade can be useful if it prevents a stronger opponent combination.",
            "Güçlü bir rakip kombinasyonunu engelliyorsa savunmacı takas işe yarayabilir.",
            "Un intercambio defensivo puede servir si evita una combinación rival fuerte.",
            "Un échange défensif peut empêcher une combinaison adverse plus forte.",
            "Ein defensiver Handel kann eine starke gegnerische Kombination verhindern.",
            "강한 상대 조합을 막는다면 방어적인 거래도 유용할 수 있습니다.",
            "Защитная сделка полезна, если мешает сопернику собрать сильную комбинацию.");
        AddTip(
            "Use pauses between decisions to scan the whole board, not only your own properties.",
            "Kararlar arasında yalnız kendi mülklerinizi değil tüm tahtayı tarayın.",
            "Entre decisiones, revisa todo el tablero, no solo tus propiedades.",
            "Entre deux décisions, regardez tout le plateau, pas seulement vos biens.",
            "Nutze Pausen, um das ganze Brett zu prüfen, nicht nur deine Grundstücke.",
            "결정 사이에는 내 자산뿐 아니라 전체 보드를 살펴보세요.",
            "Между решениями смотрите на всё поле, а не только на свои объекты.");
        AddTip(
            "A player with less cash can still be dangerous if their board position is strong.",
            "Daha az nakdi olan oyuncu güçlü tahta konumuyla hâlâ tehlikeli olabilir.",
            "Un jugador con menos efectivo aún puede ser peligroso con una buena posición.",
            "Un joueur avec moins d'argent peut rester dangereux avec une bonne position.",
            "Ein Spieler mit weniger Geld kann durch eine starke Brettposition gefährlich bleiben.",
            "현금이 적어도 보드 위치가 좋으면 여전히 위협적일 수 있습니다.",
            "Игрок с меньшим запасом денег всё ещё опасен при сильной позиции на поле.");
        AddTip(
            "Avoid making several risky commitments in the same turn unless the payoff is clear.",
            "Getirisi açık değilse aynı turda birden fazla riskli taahhüt vermeyin.",
            "Evita varios compromisos arriesgados en el mismo turno sin una recompensa clara.",
            "Évitez plusieurs engagements risqués dans le même tour sans bénéfice clair.",
            "Vermeide mehrere riskante Verpflichtungen im selben Zug ohne klaren Nutzen.",
            "보상이 분명하지 않다면 한 턴에 여러 위험한 결정을 겹치지 마세요.",
            "Не делайте несколько рискованных вложений за один ход без ясной выгоды.");
        AddTip(
            "When the board changes quickly, information is often more valuable than speed.",
            "Tahta hızlı değişirken bilgi çoğu zaman hızdan daha değerlidir.",
            "Cuando el tablero cambia rápido, la información suele valer más que la velocidad.",
            "Quand le plateau change vite, l'information vaut souvent plus que la vitesse.",
            "Wenn sich das Brett schnell ändert, ist Information oft wertvoller als Tempo.",
            "보드 상황이 빠르게 변할수록 속도보다 정보가 더 중요할 수 있습니다.",
            "Когда поле быстро меняется, информация часто важнее скорости.");
        AddTip(
            "A region close to completion may influence every player's trade decisions.",
            "Tamamlanmaya yakın bir bölge herkesin takas kararlarını etkileyebilir.",
            "Una región casi completa puede influir en las decisiones de todos.",
            "Une région presque complète peut influencer les échanges de tous les joueurs.",
            "Eine fast vollständige Region kann die Handelsentscheidungen aller beeinflussen.",
            "완성 직전의 지역은 모든 플레이어의 거래 판단에 영향을 줄 수 있습니다.",
            "Почти собранный регион может влиять на решения всех игроков.");
        AddTip(
            "Your best move may be to wait if the current price is too high.",
            "Mevcut fiyat çok yüksekse en iyi hamle beklemek olabilir.",
            "Tu mejor jugada puede ser esperar si el precio actual es demasiado alto.",
            "Votre meilleur choix peut être d'attendre si le prix est trop élevé.",
            "Warten kann der beste Zug sein, wenn der aktuelle Preis zu hoch ist.",
            "현재 가격이 너무 높다면 기다리는 것이 최선일 수 있습니다.",
            "Если цена слишком высока, лучшим ходом может быть ожидание.");
        AddTip(
            "Plan for both the best roll and the worst affordable outcome.",
            "Hem en iyi zarı hem karşılayabileceğiniz en kötü sonucu planlayın.",
            "Planifica tanto la mejor tirada como el peor resultado que puedas pagar.",
            "Prévoyez le meilleur lancer et le pire résultat que vous pouvez supporter.",
            "Plane sowohl den besten Wurf als auch das schlechteste bezahlbare Ergebnis.",
            "최고의 주사위와 감당 가능한 최악의 결과를 모두 고려하세요.",
            "Планируйте и лучший бросок, и худший результат, который сможете оплатить.");
        AddTip(
            "A small advantage repeated over many turns can become decisive.",
            "Birçok tur tekrarlanan küçük avantaj belirleyici olabilir.",
            "Una pequeña ventaja repetida durante muchos turnos puede ser decisiva.",
            "Un petit avantage répété sur plusieurs tours peut devenir décisif.",
            "Ein kleiner Vorteil über viele Züge kann entscheidend werden.",
            "작은 이점도 여러 턴 반복되면 결정적일 수 있습니다.",
            "Небольшое преимущество, повторяемое много ходов, может стать решающим.");
        AddTip(
            "Check who benefits most before agreeing to a sequence of trades.",
            "Arka arkaya takaslarda en çok kimin kazandığını kontrol edin.",
            "Comprueba quién se beneficia más antes de una secuencia de intercambios.",
            "Vérifiez qui profite le plus avant une série d'échanges.",
            "Prüfe vor mehreren Handelszügen, wer am meisten profitiert.",
            "연속 거래를 하기 전에 누가 가장 큰 이득을 얻는지 확인하세요.",
            "Перед серией сделок проверьте, кто получит наибольшую выгоду.");
        AddTip(
            "A quiet turn can be strategic if it preserves your options.",
            "Seçeneklerinizi koruyorsa sakin bir tur stratejik olabilir.",
            "Un turno tranquilo puede ser estratégico si conserva tus opciones.",
            "Un tour calme peut être stratégique s'il préserve vos options.",
            "Ein ruhiger Zug kann strategisch sein, wenn er Optionen offenhält.",
            "선택지를 유지할 수 있다면 조용한 턴도 전략적일 수 있습니다.",
            "Спокойный ход может быть стратегическим, если сохраняет варианты.");
        AddTip(
            "Use the table state, not emotion, to decide whether to bid again.",
            "Tekrar teklif verip vermeyeceğinize duyguyla değil tahta durumuyla karar verin.",
            "Decide si pujar de nuevo por el estado de la mesa, no por emoción.",
            "Décidez de surenchérir selon l'état du plateau, pas selon l'émotion.",
            "Entscheide anhand der Spielsituation, nicht aus Emotion, ob du weiterbietest.",
            "다시 입찰할지는 감정이 아니라 보드 상황으로 판단하세요.",
            "Решайте, повышать ли ставку, по ситуации на поле, а не по эмоциям.");
        AddTip(
            "If another player is close to bankruptcy, cash flexibility becomes more valuable.",
            "Bir oyuncu iflasa yakınsa nakit esnekliği daha değerli hale gelir.",
            "Si otro jugador está cerca de la bancarrota, la flexibilidad de efectivo gana valor.",
            "Si un joueur approche de la faillite, la flexibilité financière devient plus précieuse.",
            "Ist ein Spieler nahe am Bankrott, wird finanzielle Flexibilität wertvoller.",
            "다른 플레이어가 파산에 가까우면 현금 유연성이 더 중요해집니다.",
            "Если игрок близок к банкротству, гибкость в деньгах становится особенно ценной.");
        AddTip(
            "Do not ignore low-cost properties; they can complete valuable combinations.",
            "Düşük maliyetli mülkleri küçümsemeyin; değerli kombinasyonları tamamlayabilirler.",
            "No ignores propiedades baratas; pueden completar combinaciones valiosas.",
            "N'ignorez pas les propriétés peu coûteuses ; elles peuvent compléter de bonnes combinaisons.",
            "Ignoriere günstige Grundstücke nicht; sie können wertvolle Kombinationen vervollständigen.",
            "저렴한 자산도 무시하지 마세요. 중요한 조합을 완성할 수 있습니다.",
            "Не игнорируйте дешёвые объекты: они могут завершить ценную комбинацию.");
        AddTip(
            "A good defensive position can be as important as aggressive growth.",
            "İyi savunma konumu agresif büyüme kadar önemli olabilir.",
            "Una buena posición defensiva puede ser tan importante como crecer rápido.",
            "Une bonne position défensive peut être aussi importante qu'une croissance agressive.",
            "Eine gute defensive Position kann so wichtig sein wie aggressives Wachstum.",
            "좋은 방어적 위치는 공격적인 성장만큼 중요할 수 있습니다.",
            "Хорошая защитная позиция может быть не менее важна, чем агрессивный рост.");
        AddTip(
            "Review the rules selected for this table; they can change the value of a move.",
            "Bu masa için seçilen kuralları kontrol edin; hamlelerin değerini değiştirebilirler.",
            "Revisa las reglas de la mesa; pueden cambiar el valor de una jugada.",
            "Vérifiez les règles de la table ; elles peuvent changer la valeur d'un choix.",
            "Prüfe die Tischregeln; sie können den Wert eines Zuges verändern.",
            "이 테이블의 규칙을 확인하세요. 같은 행동의 가치가 달라질 수 있습니다.",
            "Проверьте правила стола: они могут изменить ценность хода.");
        AddTip(
            "Reconnecting players keep their seat state; give the table a moment to resynchronize.",
            "Yeniden bağlanan oyuncular koltuk durumunu korur; masanın senkronize olması için kısa süre verin.",
            "Los jugadores que vuelven conservan su asiento; deja un momento para resincronizar.",
            "Les joueurs reconnectés conservent leur siège ; laissez un instant à la synchronisation.",
            "Wiederverbundene Spieler behalten ihren Sitz; gib der Synchronisierung kurz Zeit.",
            "재접속한 플레이어는 좌석 상태를 유지합니다. 동기화될 시간을 잠시 주세요.",
            "Вернувшийся игрок сохраняет состояние места; дайте столу немного времени на синхронизацию.");
        AddTip(
            "Stable decisions usually beat rushed decisions over a long match.",
            "Uzun maçta istikrarlı kararlar acele kararlardan genellikle daha iyidir.",
            "En una partida larga, las decisiones estables suelen superar a las apresuradas.",
            "Sur une longue partie, les décisions stables battent souvent les décisions précipitées.",
            "In langen Spielen sind stabile Entscheidungen meist besser als überstürzte.",
            "긴 경기에서는 안정적인 판단이 성급한 판단보다 대체로 좋습니다.",
            "В долгом матче взвешенные решения обычно лучше поспешных.");
        AddTip(
            "If a deal feels too good, check what it completes for the other player.",
            "Bir anlaşma fazla iyi görünüyorsa rakip için neyi tamamladığını kontrol edin.",
            "Si un trato parece demasiado bueno, mira qué completa para el rival.",
            "Si un accord semble trop avantageux, vérifiez ce qu'il complète pour l'autre joueur.",
            "Wirkt ein Handel zu gut, prüfe, was er dem Gegner vervollständigt.",
            "거래가 너무 좋아 보인다면 상대에게 무엇을 완성해 주는지 확인하세요.",
            "Если сделка кажется слишком выгодной, проверьте, что она завершает сопернику.");
        AddTip(
            "The best strategy changes as player count and round limit change.",
            "En iyi strateji oyuncu sayısı ve tur sınırı değiştikçe değişir.",
            "La mejor estrategia cambia con el número de jugadores y el límite de rondas.",
            "La meilleure stratégie change selon le nombre de joueurs et la limite de tours.",
            "Die beste Strategie ändert sich mit Spielerzahl und Rundenlimit.",
            "플레이어 수와 라운드 제한에 따라 최적 전략도 달라집니다.",
            "Лучшая стратегия меняется вместе с числом игроков и лимитом раундов.");
        AddTip(
            "Use loading time to plan your opening priorities.",
            "Yükleme süresini açılış önceliklerinizi planlamak için kullanın.",
            "Usa el tiempo de carga para planear tus primeras prioridades.",
            "Profitez du chargement pour planifier vos premières priorités.",
            "Nutze die Ladezeit, um deine ersten Prioritäten zu planen.",
            "로딩 시간에 초반 우선순위를 계획해 보세요.",
            "Используйте время загрузки, чтобы спланировать первые приоритеты.");
    }

    private void AddTip(
        string en,
        string tr,
        string es,
        string fr,
        string de,
        string ko,
        string ru)
    {
        tips.Add(new LocalizedLine(en, tr, es, fr, de, ko, ru));
    }
}
