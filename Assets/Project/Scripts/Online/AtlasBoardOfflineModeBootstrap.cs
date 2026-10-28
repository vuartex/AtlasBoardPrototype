using System;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps AtlasBoard local/private play genuinely offline when there is no
/// authenticated Atlas identity.
///
/// Local Offline contract:
/// - PRIVATE TABLE uses the modern seat UI (+ -> Local Player / Bot).
/// - Selected open seats become local Bots at Start; they never silently become
///   extra local Humans.
/// - Start has a visible five-second local countdown.
/// - No gameplay Functions / Firestore / RTDB traffic is required.
/// - No permanent XP, wins, achievements, wallet currency or cloud history is
///   minted from this unverified local session.
/// </summary>
public sealed class AtlasBoardOfflineModeBootstrap : MonoBehaviour
{
    private static AtlasBoardOfflineModeBootstrap instance;

    private bool offlineStartupPending;
    private bool offlineStartupActivated;
    private bool privateLocalRouteLogged;
    private float nextScanAt;

    private AtlasBoardPrivateLobbyUIController offlinePrivateLobby;
    private AtlasBoardMainMenuController offlineMainMenu;
    private Button offlineStartButton;
    private bool offlineCountdownActive;
    private Coroutine offlineCountdownRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        GameObject host =
            new GameObject("AtlasBoard_Offline_Mode_Bootstrap");

        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardOfflineModeBootstrap>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        AtlasBoardFrontendErrorCodes.PublicCodeRaised -=
            HandlePublicCodeRaised;
        AtlasBoardFrontendErrorCodes.PublicCodeRaised +=
            HandlePublicCodeRaised;
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        AtlasBoardFrontendErrorCodes.PublicCodeRaised -=
            HandlePublicCodeRaised;

        if (offlineCountdownRoutine != null)
            StopCoroutine(offlineCountdownRoutine);

        instance = null;
    }

    private void HandlePublicCodeRaised(
        string code,
        string message)
    {
        if (code == AtlasBoardFrontendErrorCodes.SessionPreparationFailed ||
            code == AtlasBoardFrontendErrorCodes.RequestTimedOut)
        {
            offlineStartupPending = true;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScanAt)
            return;

        nextScanAt = Time.unscaledTime + 0.50f;

        bool identityReady = HasOnlineIdentity();

        if (identityReady)
        {
            if (offlineStartupActivated)
            {
                offlineStartupActivated = false;
                RestoreConnectivityChecks();

                Debug.Log(
                    "ATB_SESSION_MODE online_identity_restored=true");
            }

            return;
        }

        if (offlineStartupPending)
            ActivateOfflineStartup();

        RoutePrivateTableToLocalLobby();
    }

    private static bool HasOnlineIdentity()
    {
        AtlasBoardLobbyRuntimeBridge bridge =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardLobbyRuntimeBridge>(
                    FindObjectsInactive.Include);

        return bridge != null &&
               !string.IsNullOrWhiteSpace(
                   bridge.CurrentAccountId);
    }

    private void ActivateOfflineStartup()
    {
        AtlasBoardLoadingExperience loading =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardLoadingExperience>(
                    FindObjectsInactive.Include);

        if (loading == null)
            return;

        offlineStartupPending = false;
        offlineStartupActivated = true;

        InvokeNoArgs(
            loading,
            "ContinueWithoutBlocking");

        SetPrivateField(
            loading,
            "startupComplete",
            true);

        // Offline/private-local mode is intentional. Do not immediately turn
        // the absence of internet into another startup toast.
        SetPrivateField(
            loading,
            "nextConnectivityCheckAt",
            float.PositiveInfinity);

        Debug.Log(
            "ATB_SESSION_MODE mode=LocalOffline " +
            "startup_nonblocking=true rewards=false cloud_history=false");
    }

    private static void RestoreConnectivityChecks()
    {
        AtlasBoardLoadingExperience loading =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardLoadingExperience>(
                    FindObjectsInactive.Include);

        if (loading == null)
            return;

        SetPrivateField(
            loading,
            "nextConnectivityCheckAt",
            Time.unscaledTime);
    }

    private void RoutePrivateTableToLocalLobby()
    {
        AtlasBoardPrivateLobbyUIController privateLobby =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardPrivateLobbyUIController>(
                    FindObjectsInactive.Include);

        AtlasBoardMainMenuController mainMenu =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardMainMenuController>(
                    FindObjectsInactive.Include);

        if (privateLobby == null ||
            mainMenu == null ||
            !PrivateGameObjectIsActive(
                privateLobby,
                "roomEntryOverlay"))
        {
            return;
        }

        // Prefer the direct v5.29+ local-offline entry point when present.
        // It owns the canonical + -> Local Player / Bot seat state, open-seat
        // -> Bot finalization and the 5..1 countdown. The reflection fallback
        // below remains for older local project states.
        if (InvokeNoArgs(
                privateLobby,
                "EnterLocalOfflinePrivateLobby"))
        {
            if (!privateLocalRouteLogged)
            {
                privateLocalRouteLogged = true;
                Debug.Log(
                    "ATB_SESSION_MODE mode=LocalOffline source=PRIVATE_TABLE " +
                    "transport=local_only presentation=modern_seat_actions " +
                    "route=direct_v529 open_seat_policy=local_bot rewards=false");
            }

            return;
        }

        // Fallback for project copies that have not yet received the direct
        // PrivateLobby entry method. Route to the same modern presentation.
        InvokeNoArgs(
            privateLobby,
            "CancelRoomEntry");

        SetPrivateField(privateLobby, "privateMode", true);
        SetPrivateField(privateLobby, "roomActive", true);
        SetPrivateField(privateLobby, "localIsHost", true);
        SetPrivateField(privateLobby, "backendSnapshot", null);
        SetPrivateField(privateLobby, "backendLocalAccountId", string.Empty);
        SetPrivateField(privateLobby, "roomCode", string.Empty);
        SetPrivateField(privateLobby, "codeVisible", false);
        SetPrivateField(privateLobby, "settingsRevision", 1);
        SetPrivateField(privateLobby, "readyForRevision", 0);
        SetPrivateField(privateLobby, "requiredHumanPlayers", 1);

        mainMenu.OpenPrivateLobbyAfterRoomChoice();

        SetPrivateGameObjectActive(
            privateLobby,
            "privateOnlineRoot",
            true);

        SetPrivateGameObjectActive(
            privateLobby,
            "roomPanel",
            true);

        InvokeNoArgs(
            privateLobby,
            "ConfigureHostLobbyPreview");

        InstallOfflineStartGate(
            privateLobby,
            mainMenu);

        if (!privateLocalRouteLogged)
        {
            privateLocalRouteLogged = true;

            Debug.Log(
                "ATB_SESSION_MODE mode=LocalOffline source=PRIVATE_TABLE " +
                "transport=local_only presentation=modern_seat_actions " +
                "open_seat_policy=local_bot rewards=false " +
                "anti_cheat=not_required");
        }
    }

    private void InstallOfflineStartGate(
        AtlasBoardPrivateLobbyUIController privateLobby,
        AtlasBoardMainMenuController mainMenu)
    {
        GameObject startObject =
            ReadPrivateField<GameObject>(
                privateLobby,
                "legacyStartButton");

        Button start =
            startObject != null
                ? startObject.GetComponent<Button>()
                : null;

        if (start == null)
            return;

        if (offlineStartButton == start &&
            offlinePrivateLobby == privateLobby &&
            offlineMainMenu == mainMenu)
        {
            return;
        }

        offlinePrivateLobby = privateLobby;
        offlineMainMenu = mainMenu;
        offlineStartButton = start;
        offlineCountdownActive = false;

        // This scene instance is an explicit LocalOffline lobby. Replace the
        // legacy persistent Start listener so there is exactly one launch path.
        // A scene reload restores the original serialized event for online play.
        start.onClick = new Button.ButtonClickedEvent();
        start.onClick.AddListener(BeginOfflineStartCountdown);

        Debug.Log(
            "ATB_LOCAL_START_GATE installed=true countdownSeconds=5 " +
            "open_selected_seats_fill=bot");
    }

    private void BeginOfflineStartCountdown()
    {
        if (offlineCountdownActive ||
            offlinePrivateLobby == null ||
            offlineMainMenu == null)
        {
            return;
        }

        if (offlineCountdownRoutine != null)
            StopCoroutine(offlineCountdownRoutine);

        offlineCountdownRoutine =
            StartCoroutine(
                OfflineStartCountdownRoutine());
    }

    private IEnumerator OfflineStartCountdownRoutine()
    {
        offlineCountdownActive = true;

        int autoFilled =
            ConvertOpenSelectedSeatsToBots(
                offlinePrivateLobby);

        InvokeOneArg(
            offlinePrivateLobby,
            "SetHostSettingsInteractable",
            false);

        InvokeOneArg(
            offlinePrivateLobby,
            "SetHostSeatActionUIVisible",
            false);

        if (offlineStartButton != null)
            offlineStartButton.interactable = false;

        GameObject overlay =
            ReadPrivateField<GameObject>(
                offlinePrivateLobby,
                "lobbySafetyOverlay");

        TMP_Text title =
            ReadPrivateField<TMP_Text>(
                offlinePrivateLobby,
                "lobbySafetyTitleText");

        TMP_Text body =
            ReadPrivateField<TMP_Text>(
                offlinePrivateLobby,
                "lobbySafetyBodyText");

        TMP_Text countdown =
            ReadPrivateField<TMP_Text>(
                offlinePrivateLobby,
                "lobbySafetyCountdownText");

        Button primary =
            ReadPrivateField<Button>(
                offlinePrivateLobby,
                "lobbySafetyPrimaryButton");

        Button secondary =
            ReadPrivateField<Button>(
                offlinePrivateLobby,
                "lobbySafetySecondaryButton");

        if (overlay != null)
        {
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();
        }

        if (primary != null)
            primary.gameObject.SetActive(false);

        if (secondary != null)
            secondary.gameObject.SetActive(false);

        if (title != null)
        {
            title.text = AtlasBoardL.R(
                "MATCH STARTING",
                "MAÇ BAŞLIYOR",
                "LA PARTIDA COMIENZA",
                "DÉBUT DE LA PARTIE",
                "SPIEL STARTET",
                "게임 시작",
                "МАТЧ НАЧИНАЕТСЯ");
        }

        if (body != null)
        {
            body.text = AtlasBoardL.R(
                "Remaining open seats are filled by local bots.",
                "Kalan boş koltuklar yerel botlarla dolduruluyor.",
                "Los asientos vacíos se llenan con bots locales.",
                "Les places libres sont remplies par des bots locaux.",
                "Freie Plätze werden mit lokalen Bots gefüllt.",
                "남은 빈 자리는 로컬 봇으로 채워집니다.",
                "Свободные места заполняются локальными ботами.");
        }

        Debug.Log(
            "ATB_LOCAL_START_GATE event=countdown_begin seconds=5 " +
            "open_seats_to_bots=" + autoFilled);

        for (int second = 5; second >= 1; second--)
        {
            if (countdown != null)
                countdown.text = second.ToString();

            yield return new WaitForSecondsRealtime(1f);
        }

        if (countdown != null)
            countdown.text = string.Empty;

        if (overlay != null)
            overlay.SetActive(false);

        // The modern seat model is the canonical source. Only now mirror its
        // final LocalHuman/Bot decisions into legacy MatchSetup controls,
        // immediately before the unchanged gameplay Start path reads them.
        InvokeNoArgs(
            offlinePrivateLobby,
            "PrepareLegacyControlsForOnlineMatchStart");

        Debug.Log(
            "ATB_LOCAL_START_GATE event=countdown_complete " +
            "launch=canonical_legacy_bridge");

        // Same scene may have hosted an online room immediately beforehand.
        // Reset only the existing dice presentation to the local UID's equipped
        // model; otherwise online slot skin IDs leak into the local match.
        DiceVisualController dice = UnityEngine.Object.FindAnyObjectByType<DiceVisualController>(
            FindObjectsInactive.Include);
        dice?.BeginLocalOfflineDiceSession();

        offlineMainMenu.StartMatchAfterPrivateBackendAuthorization();

        offlineCountdownActive = false;
        offlineCountdownRoutine = null;
    }

    private static int ConvertOpenSelectedSeatsToBots(
        AtlasBoardPrivateLobbyUIController privateLobby)
    {
        if (privateLobby == null)
            return 0;

        object countValue =
            InvokeNoArgsWithResult(
                privateLobby,
                "GetPlayerCount");

        int selectedPlayerCount =
            countValue is int value
                ? Mathf.Clamp(value, 2, 4)
                : 2;

        Array modes =
            ReadPrivateField<Array>(
                privateLobby,
                "hostSeatModes");

        if (modes == null ||
            modes.Length < 4)
        {
            return 0;
        }

        Type enumType =
            modes.GetType().GetElementType();

        if (enumType == null ||
            !enumType.IsEnum)
        {
            return 0;
        }

        int changed = 0;

        // Slot 0 is always the local host. For selected active slots, only a
        // still-open seat is converted. Explicit LocalHuman and explicit Bot
        // selections are preserved exactly.
        for (int index = 1;
             index < selectedPlayerCount && index < modes.Length;
             index++)
        {
            object mode = modes.GetValue(index);
            int numeric =
                mode != null
                    ? Convert.ToInt32(mode)
                    : -1;

            const int OpenOnline = 0;
            const int Bot = 2;

            if (numeric != OpenOnline)
                continue;

            modes.SetValue(
                Enum.ToObject(enumType, Bot),
                index);

            changed++;
        }

        InvokeNoArgs(
            privateLobby,
            "SyncHostSeatModesToLegacyControls");

        InvokeNoArgs(
            privateLobby,
            "RefreshHostSeatRows");

        return changed;
    }

    private static void SetPrivateGameObjectActive(
        object owner,
        string fieldName,
        bool active)
    {
        GameObject value =
            ReadPrivateField<GameObject>(
                owner,
                fieldName);

        if (value != null)
            value.SetActive(active);
    }

    private static bool PrivateGameObjectIsActive(
        object owner,
        string fieldName)
    {
        GameObject value =
            ReadPrivateField<GameObject>(
                owner,
                fieldName);

        return value != null &&
               value.activeInHierarchy;
    }

    private static bool InvokeNoArgs(
        object owner,
        string methodName)
    {
        return InvokeNoArgsWithResult(
                   owner,
                   methodName,
                   out bool found) != null ||
               found;
    }

    private static object InvokeNoArgsWithResult(
        object owner,
        string methodName)
    {
        return InvokeNoArgsWithResult(
            owner,
            methodName,
            out _);
    }

    private static object InvokeNoArgsWithResult(
        object owner,
        string methodName,
        out bool found)
    {
        found = false;

        if (owner == null)
            return null;

        MethodInfo method =
            owner.GetType().GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

        if (method == null)
            return null;

        found = true;

        try
        {
            return method.Invoke(owner, null);
        }
        catch (Exception error)
        {
            Debug.LogWarning(
                "ATB_OFFLINE_ROUTE_FAILED method=" +
                methodName +
                " type=" + error.GetType().Name);

            return null;
        }
    }

    private static bool InvokeOneArg(
        object owner,
        string methodName,
        object value)
    {
        if (owner == null)
            return false;

        MethodInfo[] methods =
            owner.GetType().GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        foreach (MethodInfo method in methods)
        {
            if (!string.Equals(
                    method.Name,
                    methodName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            ParameterInfo[] parameters =
                method.GetParameters();

            if (parameters.Length != 1)
                continue;

            try
            {
                method.Invoke(
                    owner,
                    new[] { value });

                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning(
                    "ATB_OFFLINE_ROUTE_FAILED method=" +
                    methodName +
                    " type=" + error.GetType().Name);

                return false;
            }
        }

        return false;
    }

    private static T ReadPrivateField<T>(
        object owner,
        string fieldName)
    {
        if (owner == null)
            return default;

        FieldInfo field =
            owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (field == null)
            return default;

        object value = field.GetValue(owner);
        return value is T typed
            ? typed
            : default;
    }

    private static void SetPrivateField(
        object owner,
        string fieldName,
        object value)
    {
        if (owner == null)
            return;

        FieldInfo field =
            owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);

        if (field == null)
            return;

        try
        {
            field.SetValue(owner, value);
        }
        catch (Exception error)
        {
            Debug.LogWarning(
                "ATB_OFFLINE_FIELD_WRITE_FAILED field=" +
                fieldName +
                " type=" + error.GetType().Name);
        }
    }
}
