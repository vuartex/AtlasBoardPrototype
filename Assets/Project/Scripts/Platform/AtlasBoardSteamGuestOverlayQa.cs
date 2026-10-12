#if DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

/// <summary>
/// Phase 17B.2: opt-in, loopback-only Development Guest Steam overlay diagnostic.
/// Keeps the existing Guest Firebase emulator identity and production isolation.
/// IMPORTANT: SteamAPI.Init may return false even when Shift+Tab works for a
/// non-Steam shortcut. Opening a Steam dialog is NOT publisher/remote-join proof.
/// </summary>
[DefaultExecutionOrder(12010)]
[DisallowMultipleComponent]
public sealed class AtlasBoardSteamGuestOverlayQa : MonoBehaviour
{
    private const string LogTag = "ATB_PHASE17_STEAM_OVERLAY_QA_V2";
    private const string OptIn = "-atlasSteamOverlayQa";
    private const string StartButtonName = "Button_StartMatch";
    private const string QaButtonName = "Button_InviteSteamFriends_DevQa";
    private const uint DevelopmentAppId = 480;
    private const float ResolveEverySeconds = 0.25f;
    private const float OverlayConfirmWaitSeconds = 6f;

    private AtlasBoardLobbyRuntimeBridge lobby;
    private Button startButton;
    private Button inviteButton;
    private RectTransform inviteRect;
    private TMP_Text inviteLabel;
    private Callback<GameOverlayActivated_t> overlayCallback;
    private float nextResolveAt;
    private float startedAt;
    private float overlayConfirmDeadline;
    private bool steamInitializedHere;
    private bool steamInitFailed;
    private bool clickBusy;
    private bool warnedMissingUi;
    private bool overlayConfirmationPending;
    private bool overlayActivatedForRequest;
    private bool lastVisible;
    private int clickCount;
    private int requestCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!AtlasBoardAppCheckTokenSource.IsLocalEmulatorGuestSession ||
            !Environment.GetCommandLineArgs().Any(
                arg => string.Equals(arg, OptIn, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (FindAnyObjectByType<AtlasBoardSteamGuestOverlayQa>(
                FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject root = new GameObject("AtlasBoardSteamGuestOverlayQa_DEV_ONLY");
        DontDestroyOnLoad(root);
        root.AddComponent<AtlasBoardSteamGuestOverlayQa>();
        Log("BOOTSTRAP", "enabled=true mode=loopback_development " +
            "native_init=on_first_click identity_provider_unchanged=true");
    }

    private static void Log(string eventName, string details)
    {
        Debug.Log(LogTag + " event=" + eventName + " " + details);
    }

    private static void Warn(string eventName, string details)
    {
        Debug.LogWarning(LogTag + " event=" + eventName + " " + details);
    }

    private void Awake()
    {
        startedAt = Time.unscaledTime;
        PrintPreflight();
    }

    private void PrintPreflight()
    {
        string dataPath = Application.dataPath;
        string exeFolder = Path.GetDirectoryName(dataPath) ?? string.Empty;
        string workingDirectory = Environment.CurrentDirectory;
        string exeAppId = Path.Combine(exeFolder, "steam_appid.txt");
        string cwdAppId = Path.Combine(workingDirectory, "steam_appid.txt");
        string appIdStatusExe = ReadAppIdStatus(exeAppId);
        string appIdStatusCwd = ReadAppIdStatus(cwdAppId);
        bool steamRunning = IsSteamProcessRunning();
        bool nativeLibInRoot = File.Exists(Path.Combine(exeFolder, "steam_api64.dll"));
        bool nativeLibInPlugins = File.Exists(Path.Combine(
            dataPath, "Plugins", "x86_64", "steam_api64.dll"));

        Log("ENVIRONMENT", "steam_process_running=" + steamRunning +
            " appid_exe=" + appIdStatusExe +
            " appid_cwd=" + appIdStatusCwd +
            " cwd_equals_exe=" + PathsEqual(workingDirectory, exeFolder) +
            " steam_api64_exe=" + nativeLibInRoot +
            " steam_api64_plugins=" + nativeLibInPlugins +
            " emulator_guest=" + AtlasBoardAppCheckTokenSource.IsLocalEmulatorGuestSession +
            " note=dll_not_found_in_these_two_paths_is_not_conclusive");
        Log("ENV_PATHS", "exe_folder=" + SafePath(exeFolder) +
            " current_working_directory=" + SafePath(workingDirectory));
        if (appIdStatusExe != "480" || appIdStatusCwd != "480")
        {
            Warn("APPID_PREFLIGHT_WARNING", "One or both steam_appid.txt lookup paths " +
                "are not 480. Use Tools/Phase17B/SteamGuestPreflight.ps1; Steam may use CWD.");
        }
        if (!steamRunning)
        {
            Warn("STEAM_PROCESS_WARNING", "No steam.exe process found at preflight. " +
                "Ensure Steam desktop is running with same Windows user/elevation.");
        }
    }

    private static string ReadAppIdStatus(string path)
    {
        try
        {
            if (!File.Exists(path)) return "MISSING";
            string value = File.ReadAllText(path).Trim();
            if (value == "480") return "480";
            if (value.Length == 0) return "EMPTY";
            return "UNEXPECTED_VALUE"; // Do not log other AppIDs or file contents.
        }
        catch (Exception error)
        {
            return "READ_ERROR_" + error.GetType().Name;
        }
    }

    private static bool IsSteamProcessRunning()
    {
        try
        {
            Process[] processes = Process.GetProcessesByName("steam");
            bool exists = processes.Length > 0;
            foreach (Process process in processes) process.Dispose();
            return exists;
        }
        catch (Exception) { return false; }
    }

    private static bool PathsEqual(string first, string second)
    {
        try
        {
            return string.Equals(Path.GetFullPath(first).TrimEnd('\\', '/'),
                Path.GetFullPath(second).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) { return false; }
    }

    private static string SafePath(string path)
    {
        // Quoted and sanitized so paths cannot inject fake log fields or newlines.
        return "\"" + (path ?? string.Empty).Replace("\r", "_").Replace("\n", "_")
            .Replace("\"", "_") + "\"";
    }

    private void Update()
    {
        if (steamInitializedHere)
        {
            try { SteamAPI.RunCallbacks(); }
            catch (Exception error)
            {
                Warn("STEAM_CALLBACK_ERROR", "exception=" + error.GetType().Name);
            }
        }

        if (overlayConfirmationPending &&
            Time.unscaledTime >= overlayConfirmDeadline)
        {
            overlayConfirmationPending = false;
            if (!overlayActivatedForRequest)
            {
                Warn("OVERLAY_NOT_CONFIRMED", "request=" + requestCount +
                    " elapsed_s=" + OverlayConfirmWaitSeconds +
                    " note=Steam_invite_API_void_return_is_not_proof_dialog_opened");
            }
        }

        if (Time.unscaledTime >= nextResolveAt)
        {
            nextResolveAt = Time.unscaledTime + ResolveEverySeconds;
            ResolveReferences();
        }
        UpdateVisibility();
    }

    private void ResolveReferences()
    {
        if (lobby == null)
        {
            lobby = Resources.FindObjectsOfTypeAll<AtlasBoardLobbyRuntimeBridge>()
                .FirstOrDefault(x => x != null && x.gameObject.scene.IsValid());
        }
        if (startButton == null)
        {
            startButton = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(x => x != null && x.gameObject.scene.IsValid() &&
                    x.name == StartButtonName);
        }
        if (inviteButton == null && startButton != null) BuildInviteButton();
        AlignInviteButton();
        if (!warnedMissingUi && startButton == null &&
            Time.unscaledTime - startedAt > 10f)
        {
            warnedMissingUi = true;
            Warn("START_BUTTON_NOT_FOUND", "object_name=Button_StartMatch");
        }
    }

    private void BuildInviteButton()
    {
        if (startButton == null) return;
        RectTransform source = startButton.GetComponent<RectTransform>();
        Image sourceImage = startButton.GetComponent<Image>();
        TMP_Text sourceText = startButton.GetComponentInChildren<TMP_Text>(true);
        Transform parent = startButton.transform.parent;
        if (parent == null || source == null || sourceImage == null || sourceText == null)
        {
            Warn("BUTTON_BUILD_SKIPPED", "reason=template_missing_required_component");
            return;
        }

        GameObject go = new GameObject(QaButtonName, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        inviteRect = go.GetComponent<RectTransform>();
        Image image = go.GetComponent<Image>();
        image.sprite = sourceImage.sprite;
        image.overrideSprite = sourceImage.overrideSprite;
        image.material = sourceImage.material;
        image.color = sourceImage.color;
        image.type = sourceImage.type;
        image.preserveAspect = sourceImage.preserveAspect;
        inviteButton = go.GetComponent<Button>();
        inviteButton.targetGraphic = image;
        inviteButton.transition = startButton.transition;
        inviteButton.colors = startButton.colors;
        inviteButton.spriteState = startButton.spriteState;
        inviteButton.navigation = startButton.navigation;
        inviteButton.onClick.AddListener(OpenInvite);

        GameObject textGo = new GameObject("Text", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        inviteLabel = textGo.GetComponent<TextMeshProUGUI>();
        inviteLabel.font = sourceText.font;
        inviteLabel.fontSharedMaterial = sourceText.fontSharedMaterial;
        inviteLabel.color = sourceText.color;
        inviteLabel.fontStyle = sourceText.fontStyle;
        inviteLabel.alignment = sourceText.alignment;
        inviteLabel.enableAutoSizing = true;
        inviteLabel.fontSizeMin = 12f;
        inviteLabel.fontSizeMax = Mathf.Max(23f, sourceText.fontSize);
        inviteLabel.raycastTarget = false;
        RectTransform textRect = inviteLabel.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        UpdateLabel();
        AlignInviteButton();
        UpdateVisibility();
        Log("BUTTON_CREATED", "id=" + QaButtonName + " waiting_gate=true");
    }

    private void AlignInviteButton()
    {
        if (startButton == null || inviteRect == null) return;
        RectTransform source = startButton.GetComponent<RectTransform>();
        if (source == null) return;
        inviteRect.anchorMin = source.anchorMin;
        inviteRect.anchorMax = source.anchorMax;
        inviteRect.pivot = source.pivot;
        inviteRect.sizeDelta = source.sizeDelta;
        inviteRect.localScale = source.localScale;
        inviteRect.localRotation = source.localRotation;
        float gap = Mathf.Max(24f, source.sizeDelta.x * 0.06f);
        inviteRect.anchoredPosition = source.anchoredPosition +
            Vector2.left * (source.sizeDelta.x + gap);
    }

    private bool IsWaitingLobby()
    {
        return lobby != null && lobby.HasLobby && lobby.CurrentSnapshot != null &&
            lobby.CurrentSnapshot.LifecycleState == AtlasRoomLifecycleState.Waiting &&
            !string.IsNullOrWhiteSpace(lobby.CurrentRoomCode);
    }

    private void UpdateVisibility()
    {
        if (inviteButton == null) return;
        bool visible = IsWaitingLobby();
        if (visible != lastVisible)
        {
            lastVisible = visible;
            Log("BUTTON_VISIBILITY", "visible=" + visible +
                " lobby_present=" + (lobby != null && lobby.HasLobby) +
                " lifecycle_waiting=" + (lobby != null && lobby.CurrentSnapshot != null &&
                    lobby.CurrentSnapshot.LifecycleState == AtlasRoomLifecycleState.Waiting));
        }
        if (inviteButton.gameObject.activeSelf != visible)
            inviteButton.gameObject.SetActive(visible);
        inviteButton.interactable = visible && !clickBusy;
    }

    private void UpdateLabel()
    {
        if (inviteLabel == null) return;
        inviteLabel.text = steamInitFailed
            ? "STEAM INIT FAILED"
            : overlayConfirmationPending ? "INVITE REQUESTED" : "INVITE FRIENDS";
    }

    private void OnOverlayActivated(GameOverlayActivated_t callback)
    {
        bool active = callback.m_bActive != 0;
        Log("OVERLAY_CALLBACK", "active=" + active +
            " request=" + requestCount + " confirmation_pending=" + overlayConfirmationPending);
        if (active && overlayConfirmationPending)
        {
            overlayActivatedForRequest = true;
            overlayConfirmationPending = false;
            Log("OVERLAY_ACTIVATED", "request=" + requestCount +
                " note=overlay_opened_not_proof_friend_invite_sent");
            UpdateLabel();
        }
    }

    private void OpenInvite()
    {
        clickCount++;
        Log("CLICK_RECEIVED", "click=" + clickCount +
            " waiting=" + IsWaitingLobby() +
            " busy=" + clickBusy + " steam_initialized=" + steamInitializedHere +
            " init_failed_prior=" + steamInitFailed);
        if (clickBusy || !IsWaitingLobby())
        {
            Warn("CLICK_REJECTED", "click=" + clickCount + " reason=busy_or_lobby_not_waiting");
            return;
        }
        if (steamInitFailed)
        {
            Warn("CLICK_BLOCKED", "click=" + clickCount +
                " reason=prior_steam_api_init_failed retry_requires_relaunch");
            return;
        }

        clickBusy = true;
        UpdateVisibility();
        try
        {
            if (!steamInitializedHere)
            {
                PrintPreflight();
                Log("STEAM_INIT_BEGIN", "click=" + clickCount +
                    " appid_expected=480 steam_process_running=" + IsSteamProcessRunning());
                bool ok;
                try { ok = SteamAPI.Init(); }
                catch (Exception error)
                {
                    steamInitFailed = true;
                    Warn("STEAM_INIT_EXCEPTION", "click=" + clickCount +
                        " exception=" + error.GetType().Name +
                        " note=no_further_native_init_attempts_this_session");
                    return;
                }
                if (!ok)
                {
                    steamInitFailed = true;
                    Warn("STEAM_INIT_FAILED", "click=" + clickCount +
                        " reasons_to_check=appid_file_cwd_or_exe,steam_ownership,steam_client,elevation_mismatch " +
                        "note=shift_tab_in_non_steam_game_does_not_validate_steamworks_api " +
                        "retry_requires_relaunch=true");
                    return;
                }
                steamInitializedHere = true;
                uint appId = SteamUtils.GetAppID().m_AppId;
                bool overlayEnabled = SteamUtils.IsOverlayEnabled();
                Log("STEAM_INIT_OK", "click=" + clickCount +
                    " appid=" + appId + " overlay_enabled=" + overlayEnabled);
                if (appId != DevelopmentAppId)
                {
                    Warn("APPID_MISMATCH", "expected=480 actual=" + appId +
                        " action=shutdown_and_block");
                    steamInitFailed = true;
                    overlayCallback?.Dispose();
                    overlayCallback = null;
                    SteamAPI.Shutdown();
                    steamInitializedHere = false;
                    return;
                }
                overlayCallback = Callback<GameOverlayActivated_t>.Create(OnOverlayActivated);
                Log("OVERLAY_CALLBACK_REGISTERED", "callback=GameOverlayActivated_t");
            }

            string roomCode = lobby.CurrentRoomCode;
            string connect = AtlasBoardPlatformJoinPayload.BuildSteamConnectString(roomCode);
            if (string.IsNullOrWhiteSpace(connect))
            {
                Warn("CONNECT_STRING_INVALID", "click=" + clickCount +
                    " code_length=" + (roomCode == null ? 0 : roomCode.Length));
                return;
            }

            // Never print the 6-digit room access code or connect payload.
            requestCount++;
            overlayActivatedForRequest = false;
            overlayConfirmationPending = true;
            overlayConfirmDeadline = Time.unscaledTime + OverlayConfirmWaitSeconds;
            Log("INVITE_API_BEGIN", "request=" + requestCount +
                " connect_payload_valid=true room_code_redacted=true " +
                "overlay_enabled=" + SteamUtils.IsOverlayEnabled());
            SteamFriends.ActivateGameOverlayInviteDialogConnectString(connect);
            Log("INVITE_API_RETURNED", "request=" + requestCount +
                " api_return_type=void " +
                "note=wait_for_OVERLAY_ACTIVATED_callback_or_OVERLAY_NOT_CONFIRMED");
        }
        catch (Exception error)
        {
            overlayConfirmationPending = false;
            Warn("INVITE_EXCEPTION", "click=" + clickCount +
                " exception=" + error.GetType().Name);
        }
        finally
        {
            clickBusy = false;
            UpdateLabel();
            UpdateVisibility();
        }
    }

    private void OnDestroy()
    {
        Log("SHUTDOWN", "native_steam_initialized_here=" + steamInitializedHere +
            " total_clicks=" + clickCount + " total_invite_requests=" + requestCount);
        overlayCallback?.Dispose();
        overlayCallback = null;
        if (steamInitializedHere)
        {
            try { SteamAPI.Shutdown(); }
            catch (Exception error)
            {
                Warn("STEAM_SHUTDOWN_EXCEPTION", "exception=" + error.GetType().Name);
            }
            steamInitializedHere = false;
        }
        if (inviteButton != null) Destroy(inviteButton.gameObject);
    }
}
#endif
