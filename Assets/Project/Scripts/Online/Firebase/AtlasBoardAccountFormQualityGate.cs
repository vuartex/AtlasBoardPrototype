using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Improves the single existing Account form (no duplicate login screen).
/// Captures invalid email addresses before they reach Auth, creates clear
/// typing focus/caret, and offers explicit password reveal buttons.
/// When the native Firebase principal is anonymous, upgrades that SAME UID
/// rather than creating a different wallet owner.
/// </summary>
[DefaultExecutionOrder(32500)]
public sealed class AtlasBoardAccountFormQualityGate : MonoBehaviour
{
    private static AtlasBoardAccountFormQualityGate instance;
    private static readonly Regex EmailFormat = new Regex(
        @"^[^\s@]+@[^\s@.]+(?:\.[A-Za-z]{2,63})+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly HashSet<TMP_InputField> configuredInputs = new HashSet<TMP_InputField>();
    private readonly HashSet<Button> configuredActions = new HashSet<Button>();
    private static Sprite roundedSprite;
    private static Sprite openEyeSprite;
    private static Sprite closedEyeSprite;
    private readonly Dictionary<TMP_InputField, Image> caretMarkers =
        new Dictionary<TMP_InputField, Image>();
    private float nextScan;
    private bool upgrading;
    private bool restGuestBusy;
    private bool guestInitializationInFlight;
    private string registeredLiveUid = string.Empty;
    private string registeredLiveDisplayName = string.Empty;
    private string registeredLiveEmail = string.Empty;
    private bool registeredLiveVerified;
    private Button registeredLiveContextButton;
    private float nextSessionAudit;
    private float preventImmediateAccountReopenUntil;
    private Button registeredLiveCareerButton;
    private GameObject authErrorDialog;
    private bool liveAccountProbeRunning;
    private float nextLiveAccountProbe;
    private string lastProbedLiveUid = string.Empty;
    private float nextGuestInitialization;
    private TMP_InputField lastFocusedField;
    private GameObject accountNoticeDialog;
    private GameObject passwordResetDialog;
    private bool passwordResetBusy;
    private float nextRegisteredHeaderRefresh;
    private bool restAccountBusy;
    private GameObject registeredAccountPanel;


    // Single verified REST identity contract for all UI consumers. Never use
    // a saved nickname as proof of authentication or read native Editor Auth
    // inside the separate Windows Live Guest QA process.
    public static bool TryGetVerifiedRegisteredRestPrincipal(out string uid)
    {
        uid = string.Empty;
        if (!AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession ||
            instance == null || !instance.registeredLiveVerified ||
            string.IsNullOrWhiteSpace(instance.registeredLiveUid)) return false;
        if (!AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(
                out string liveUid, out string liveToken) ||
            string.IsNullOrWhiteSpace(liveToken) ||
            !string.Equals(liveUid, instance.registeredLiveUid,
                StringComparison.Ordinal)) return false;
        uid = liveUid;
        return true;
    }


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject host = new GameObject("AtlasBoard_Account_Form_Quality_Gate");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<AtlasBoardAccountFormQualityGate>();
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void LateUpdate()
    {
        // UI can rebuild during Update; the last layout pass must remain fixed.
        StabilizeStatusText();
        KeepRegisteredAccountLabel();
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession &&
            preventImmediateAccountReopenUntil > Time.unscaledTime)
        {
            var ui = FindAnyObjectByType<AtlasBoardAccountAccessUI>(FindObjectsInactive.Include);
            var modal = ui != null ? GetField<GameObject>(ui, "modalBackdrop") : null;
            if (modal != null && modal.activeSelf) modal.SetActive(false);
        }
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextGuestInitialization &&
            !guestInitializationInFlight)
        {
            nextGuestInitialization = Time.unscaledTime + 8f;
            _ = EnsureNativeGuestAsync();
        }
        TrackNewlyFocusedInput();
        UpdateVisibleCarets();
        HandleKeyboardTab();
        MaintainRegisteredLiveGuestPresentation();
        MaybeProbeRestoredRegisteredGuest();
        BindRegisteredAccountContext();
        EmitSessionAudit();
        StabilizeStatusText();
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 0.25f;
        AtlasBoardAccountAccessUI ui = FindAnyObjectByType<AtlasBoardAccountAccessUI>(FindObjectsInactive.Include);
        if (ui == null) return;
        TMP_InputField[] inputs = ui.GetComponentsInChildren<TMP_InputField>(true);
        foreach (TMP_InputField input in inputs)
        {
            if (input == null || !configuredInputs.Add(input)) continue;
            input.customCaretColor = true;
            input.caretColor = new Color32(20, 72, 129, 255);
            input.caretWidth = 4;
            input.caretBlinkRate = 0.55f;
            ConfigureInputSurface(input);
            input.selectionColor = new Color32(72, 145, 221, 120);
            input.onSelect.AddListener(selectedField =>
            {
                SetFieldFocus(input, true);
                // SelectInput can change TMP's caret again later in the frame.
                // Only reposition when entering a different field; never
                // fight explicit mouse positioning inside an already focused field.
                _ = MoveCaretToEndAfterFocusAsync(input);
            });
            input.onDeselect.AddListener(_ => SetFieldFocus(input, false));
            InstallVisibleCaret(input);
            if (input.contentType == TMP_InputField.ContentType.Password ||
                input.gameObject.name.IndexOf("Password", StringComparison.OrdinalIgnoreCase) >= 0)
                AddPasswordVisibilityToggle(input);
        }

        // Layout is owned by StabilizeStatusText: one invariant placement on
        // every frame, not conflicting positions in the initial scan and UI.
        StabilizeStatusText();

        foreach (Button button in ui.GetComponentsInChildren<Button>(true))
        {
            if (button == null || !configuredActions.Add(button)) continue;
            ModernizeButton(button);
            if (button.name == "Button_ResetPassword")
            {
                // The existing native Account UI already sends reset emails.
                // The REST Guest EXE does not initialize native Firebase Auth:
                // intercept only that specific build, using a dedicated email dialog.
                if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
                {
                    button.onClick = new Button.ButtonClickedEvent();
                    button.onClick.AddListener(() => ShowPasswordResetDialog(ui));
                }
                continue;
            }
            if (button.name != "Button_CreateAccount" && button.name != "Button_SignIn") continue;
            Button.ButtonClickedEvent original = button.onClick;
            button.onClick = new Button.ButtonClickedEvent();
            bool register = button.name == "Button_CreateAccount";
            button.onClick.AddListener(() =>
            {
                string email = GetText(ui, register ? "registerEmail" : "loginEmail");
                if (!EmailFormat.IsMatch((email ?? "").Trim()))
                {
                    ShowStatus(ui, "Geçerli bir e-posta adresi gir (örnek: name@example.com). / Enter a valid email address.");
                    return;
                }
                if (!register && string.IsNullOrWhiteSpace(GetText(ui, "loginPassword")))
                {
                    ShowAuthErrorPopup(ui,
                        "Şifre alanı boş bırakılamaz. Destek kodu: ATB739");
                    return;
                }
                if (register)
                {
                    if (upgrading || restGuestBusy) return;
                    string name = GetText(ui, "registerName").Trim();
                    if (name.Length < 2 || name.Length > 32)
                    {
                        ShowStatus(ui, "Oyuncu adı 2–32 karakter olmalı. / Display name must be 2–32 characters.");
                        return;
                    }
                    if (!ValidateNewPassword(ui)) return;
                    if (!ValidateCaptcha(ui)) return;
                }
                // The canonical live Guest already owns an anonymous REST UID.
                // Upgrade that very principal instead of starting native Auth
                // or creating another Firebase UID/wallet.
                if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
                {
                    _ = AuthenticateLiveGuestRestAsync(ui, register, email.Trim());
                    return;
                }
                if (register)
                {
                    FirebaseUser principal = GetPrincipal();
                    if (principal != null && principal.IsAnonymous)
                    {
                        _ = UpgradeGuestAsync(ui, principal, email.Trim());
                        return;
                    }
                }
                if (AtlasBoardAccountService.Instance == null)
                {
                    _ = InvokeAfterServiceReadyAsync(ui, original);
                    return;
                }
                original.Invoke();
            });
        }
    }

    // Password contract: at least six characters, one letter, and at least
    // one digit OR symbol. This is independent of Firebase's minimum length.
    private static bool IsStrongPassword(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 6) return false;
        bool letter = false, other = false;
        foreach (char ch in value)
        {
            if (char.IsWhiteSpace(ch)) return false;
            if (char.IsLetter(ch)) letter = true;
            else if (char.IsDigit(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch))
                other = true;
        }
        return letter && other;
    }

    private static bool ValidateNewPassword(AtlasBoardAccountAccessUI ui)
    {
        string password = GetText(ui, "registerPassword");
        if (!IsStrongPassword(password))
        {
            ShowStatus(ui, "Şifre en az 6 karakter, bir harf ve bir sayı veya sembol içermeli. / Use 6+ characters, a letter and a number or symbol.");
            return false;
        }
        if (!string.Equals(password, GetText(ui, "registerPasswordRepeat"), StringComparison.Ordinal))
        {
            ShowStatus(ui, "Şifre tekrarı eşleşmiyor. / Passwords do not match.");
            return false;
        }
        return true;
    }

    private static bool ValidateCaptcha(AtlasBoardAccountAccessUI ui)
    {
        TMP_InputField answer = GetField<TMP_InputField>(ui, "captchaAnswer");
        if (!int.TryParse(answer != null ? answer.text : "", out int actual) ||
            actual != GetField<int>(ui, "captchaExpected"))
        {
            ShowStatus(ui, "Matematik doğrulamasını tamamla. / Complete the math check.");
            return false;
        }
        return true;
    }

    [Serializable]
    private sealed class RestGuestLinkRequest
    {
        public string idToken;
        public string email;
        public string password;
        public bool returnSecureToken = true;
    }

    [Serializable]
    private sealed class RestGuestLoginRequest
    {
        public string email;
        public string password;
        public bool returnSecureToken = true;
    }

    [Serializable]
    private sealed class RestAuthResponse
    {
        public string localId;
        public string idToken;
        public string refreshToken;
        public string email;
    }

    [Serializable]
    private sealed class RestLookupRequest { public string idToken; }
    [Serializable]
    private sealed class RestLookupResponse { public RestLookupUser[] users; }
    [Serializable]
    private sealed class RestLookupUser { public string email; }
    [Serializable]
    private sealed class RestAuthErrorEnvelope { public RestAuthError error; }
    [Serializable]
    private sealed class RestAuthError { public string message; }

    private const string LiveGuestWebApiKey =
        "AIzaSyAOnxprB0-59IZ2D5uvWM4xBtixzA_cRtU";
    private const string LiveGuestDatabase =
        "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";

    // This is the SAME launch flow and REST Firebase identity used by the
    // canonical Two-Client Guest QA EXE. No native Firebase SDK is started.
    private async Task AuthenticateLiveGuestRestAsync(
        AtlasBoardAccountAccessUI ui, bool register, string email)
    {
        if (restGuestBusy) return;
        restGuestBusy = true;
        try
        {
            AtlasBoardLobbyRuntimeBridge bridge =
                FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>(FindObjectsInactive.Include);
            if (bridge == null || !await bridge.EnsureOnlineIdentityAsync())
            {
                ShowStatus(ui, "Oturum hazırlanamadı. Bağlantını kontrol et. Destek kodu: ATB731");
                return;
            }
            string oldUid = string.Empty, token = string.Empty;
            if (!AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(out oldUid, out token))
            {
                ShowStatus(ui, "Guest oturumu doğrulanamadı. Veriler değiştirilmedi.");
                return;
            }
            // A Live Guest may remain open for hours. Always request a fresh
            // server-issued ID token for the same UID before linking the
            // email credential. A refresh never creates another Firebase user.
            if (register)
            {
                MethodInfo refresh = typeof(AtlasBoardLobbyRuntimeBridge).GetMethod(
                    "RefreshLiveGuestQaIdentityAsync", BindingFlags.Instance | BindingFlags.NonPublic);
                Task<AtlasLobbyOperationResult> refreshTask =
                    refresh?.Invoke(bridge, null) as Task<AtlasLobbyOperationResult>;
                if (refreshTask == null || !(await refreshTask).Success ||
                    !AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(
                        out string refreshedUid, out string refreshedToken) ||
                    !string.Equals(oldUid, refreshedUid, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(refreshedToken))
                {
                    ShowStatus(ui, "Oturum yenilenemedi. Verilerin korundu. Destek kodu: ATB734");
                    return;
                }
                token = refreshedToken;
            }
            if (register && !await IsStillAnonymousLiveGuestAsync(token))
            {
                ShowStatus(ui, "Bu oturum zaten kayıtlı olabilir veya kontrol edilemedi. Yeni hesap açılmadı. GİRİŞ YAP seçeneğini kullan.");
                return;
            }
            string password = GetText(ui, register ? "registerPassword" : "loginPassword");
            string requestedDisplayName = register ? GetText(ui, "registerName").Trim() : string.Empty;
            string endpoint = "https://identitytoolkit.googleapis.com/v1/accounts:" +
                (register ? "signUp" : "signInWithPassword") + "?key=" +
                UnityWebRequest.EscapeURL(LiveGuestWebApiKey);
            string payload = register
                ? JsonUtility.ToJson(new RestGuestLinkRequest {
                    idToken = token, email = email, password = password,
                    returnSecureToken = true })
                : JsonUtility.ToJson(new RestGuestLoginRequest {
                    email = email, password = password, returnSecureToken = true });

            using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 20;
                await SendWebRequestAsync(request);
                if (request.result != UnityWebRequest.Result.Success ||
                    request.responseCode < 200 || request.responseCode >= 300)
                {
                    string category = "AUTH_UNAVAILABLE";
                    try {
                        var error = JsonUtility.FromJson<RestAuthErrorEnvelope>(request.downloadHandler.text);
                        category = error?.error?.message ?? category;
                    } catch { /* error response is untrusted */ }
                    string safeCategory = SafeFirebaseErrorCode(category);
                    ShowAuthErrorPopup(ui, PlayerAuthErrorCode(safeCategory, register));
                    // Never log the payload, password, ID token or full response.
                    Debug.LogWarning("ATB_GUEST_AUTH account_action=" + (register ? "link" : "signin") +
                                     " success=false http=" + request.responseCode +
                                     " firebase_code=" + safeCategory +
                                     " uid_unchanged=true");
                    return;
                }
                RestAuthResponse response = null;
                try { response = JsonUtility.FromJson<RestAuthResponse>(request.downloadHandler.text); }
                catch { /* malformed response must fail closed */ }
                if (response == null || string.IsNullOrWhiteSpace(response.localId) ||
                    string.IsNullOrWhiteSpace(response.idToken) ||
                    string.IsNullOrWhiteSpace(response.refreshToken) ||
                    (register && !string.Equals(oldUid, response.localId, StringComparison.Ordinal)))
                {
                    ShowStatus(ui, "Hesap doğrulanamadı. Verilerin korundu. Destek kodu: ATB734");
                    Debug.LogError("ATB_GUEST_AUTH response_uid_contract_failed=true");
                    return;
                }
                // The bridge owns Windows-DPAPI persistence. Confirm the token
                // is stored before switching any in-memory identity.
                MethodInfo saver = typeof(AtlasBoardLobbyRuntimeBridge).GetMethod(
                    "TrySaveLiveGuestCredentials", BindingFlags.NonPublic | BindingFlags.Static);
                bool saved = saver != null && (bool)saver.Invoke(null,
                    new object[] { response.localId, response.refreshToken });
                if (!saved)
                {
                    ShowStatus(ui, "Giriş bilgileri güvenle saklanamadı. Hesap verilerini silme; yeniden giriş yap.");
                    Debug.LogError("ATB_GUEST_AUTH secure_session_save_failed=true");
                    return;
                }
                SetBridgeField(bridge, "currentAccountId", response.localId);
                SetBridgeField(bridge, "idToken", response.idToken);
                SetBridgeField(bridge, "liveGuestRefreshToken", response.refreshToken);
                SetBridgeField(bridge, "initialized", true);
                SetBridgeField(bridge, "nextIdentityRefreshAt", Time.unscaledTime + 2700f);
                bridge.ClearLocalLobbyState();
                AtlasBoardGuestIdentity.BindLiveAuthenticatedUid(response.localId);
                if (register)
                {
                    // Refresh after changing the Auth provider to password.
                    // The new ID token must carry the new provider claims for
                    // RTDB accountType validation; don't assume an older token
                    // still represents the registered identity.
                    try
                    {
                        MethodInfo refresh = typeof(AtlasBoardLobbyRuntimeBridge).GetMethod(
                            "RefreshLiveGuestQaIdentityAsync", BindingFlags.Instance | BindingFlags.NonPublic);
                        Task<AtlasLobbyOperationResult> resultTask = refresh?.Invoke(bridge, null)
                            as Task<AtlasLobbyOperationResult>;
                        if (resultTask != null && (await resultTask).Success &&
                            AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(
                                out string refreshedUid, out string refreshedToken) &&
                            string.Equals(refreshedUid, response.localId, StringComparison.Ordinal))
                            response.idToken = refreshedToken;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("ATB_GUEST_AUTH post_link_refresh_pending=" + ex.GetType().Name);
                    }
                }
                if (register)
                {
                    try
                    {
                        await TrySaveRegisteredDisplayNameInFirebaseAuthAsync(
                            response.localId, response.idToken, requestedDisplayName);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("ATB_GUEST_AUTH optional_name_write_pending=" + ex.GetType().Name);
                    }
                    BindRegisteredLiveGuest(response.localId, requestedDisplayName);
                    registeredLiveEmail = email;
                    // Updating RTDB displayName is subject to the existing 24h
                    // change cooldown. Never discard a successful UID upgrade.
                    _ = TryUpdateRtdbDisplayNameAsync(
                        response.localId, response.idToken, requestedDisplayName);
                }
                else
                {
                    // IMPORTANT: registeredLiveUid is the sign-in state used by
                    // Career. It must be bound at successful credential validation,
                    // NOT after asynchronous nickname lookup. The old code closed
                    // Login while leaving Career stuck in the unsigned state.
                    string restoredName = PlayerPrefs.GetString(
                        "AtlasBoard.RegisteredGuestName." + response.localId, string.Empty);
                    if (string.IsNullOrWhiteSpace(restoredName))
                        restoredName = AtlasBoardGuestIdentity.NameForAuthenticatedUid(response.localId);
                    BindRegisteredLiveGuest(response.localId, restoredName);
                    registeredLiveEmail = email;
                    // Auth profile is authoritative for the permanent display
                    // name. Lookup can finish later without losing sign-in state.
                    _ = LoadRegisteredGuestNameAsync(response.localId, response.idToken);
                }
                // On *every* verified password login, reconcile the existing
                // /users/{uid} profile with Firebase Authentication. A session
                // that was linked before the profile write succeeded may still
                // be marked guest on the RTDB node. Never copy any wallet.
                bool profileUpdated = await ApplyRegisteredGuestProfileAsync(
                    response.localId, response.idToken, email);
                Debug.Log("ATB_ACCOUNT_PROFILE_SYNC stage=signin completed=" +
                    profileUpdated + " uid_suffix=" + SafeUidSuffix(response.localId) +
                    " wallet_transfer=false");
                if (!profileUpdated)
                {
                    Debug.LogWarning("ATB_GUEST_AUTH verified=true profile_update_pending=true " +
                        "code=ATB738 uid_unchanged=true");
                }
                // The confirmed Firebase token is already bound to its UID.
                // Wallet/profile refresh is best-effort; it must not turn a
                // successful Auth sign-in into a silent half-signed-in state.
                try { await AtlasBoardRtdbMetaEconomy.LoadAsync(); }
                catch (Exception walletError)
                {
                    Debug.LogWarning("ATB_GUEST_AUTH wallet_reload_pending type=" +
                        walletError.GetType().Name);
                }
                AtlasBoardCanonicalProfileHeader header =
                    FindAnyObjectByType<AtlasBoardCanonicalProfileHeader>(FindObjectsInactive.Include);
                if (header != null)
                {
                    header.RefreshNow();
                    ApplyRegisteredHeaderIdentity();
                }
                bool openCareerAfterLogin = GetField<bool>(ui, "openCareerAfterAuthentication");
                FieldInfo openFlag = typeof(AtlasBoardAccountAccessUI).GetField(
                    "openCareerAfterAuthentication", BindingFlags.Instance | BindingFlags.NonPublic);
                openFlag?.SetValue(ui, false);
                GameObject modal = GetField<GameObject>(ui, "modalBackdrop");
                if (modal != null) modal.SetActive(false);
                preventImmediateAccountReopenUntil = Time.unscaledTime + 3f;
                // The REST Guest build deliberately has no native FirebaseAuth
                // service. The existing Account UI would otherwise interpret
                // the successful login as unsigned and redisplay the gate.
                BindRegisteredCareerEntry();
                ShowGlobalAccountNotice(ui, register
                    ? "Hesabın oluşturuldu ve giriş yapıldı. İlerlemen korundu."
                    : "Giriş başarılı. Hesabın ve kayıtlı ilerlemen yüklendi.");
                if (openCareerAfterLogin)
                    OpenCareerAfterRestAuthentication();
                Debug.Log("ATB_GUEST_AUTH mode=" + (register ? "uid_link" : "email_signin") +
                          " success=true uid_preserved=" + (register ? "true" : "n/a") +
                          " wallet_transfer=false");
            }
        }
        catch (Exception e)
        {
            ShowStatus(ui, "Bağlantı kurulamadı. Verilerin korundu. Destek kodu: ATB731");
            Debug.LogWarning("ATB_GUEST_AUTH exception=" + e.GetType().Name);
        }
        finally { restGuestBusy = false; }
    }

    [Serializable]
    private sealed class RestDisplayNameRequest
    {
        public string idToken;
        public string displayName;
        public bool returnSecureToken = true;
    }
    [Serializable]
    private sealed class RestLookupUserProfile
    {
        public string localId;
        public string displayName;
        public string email;
    }
    [Serializable]
    private sealed class RestLookupProfile
    {
        public RestLookupUserProfile[] users;
    }

    private static async Task TrySaveRegisteredDisplayNameInFirebaseAuthAsync(
        string uid, string token, string displayName)
    {
        if (displayName.Length < 2 || displayName.Length > 32) return;
        string url = "https://identitytoolkit.googleapis.com/v1/accounts:update?key=" +
            UnityWebRequest.EscapeURL(LiveGuestWebApiKey);
        using (UnityWebRequest r = new UnityWebRequest(url, "POST"))
        {
            r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(new RestDisplayNameRequest {
                    idToken = token, displayName = displayName, returnSecureToken = true
                })));
            r.downloadHandler = new DownloadHandlerBuffer();
            r.SetRequestHeader("Content-Type", "application/json");
            r.timeout = 12;
            await SendWebRequestAsync(r);
            if (r.responseCode != 200)
                Debug.LogWarning("ATB_GUEST_AUTH auth_display_name_pending=true http=" + r.responseCode);
        }
    }

    private static async Task TryUpdateRtdbDisplayNameAsync(
        string uid, string token, string displayName)
    {
        if (displayName.Length < 2 || displayName.Length > 32) return;
        string url = LiveGuestDatabase + "/users/" +
            UnityWebRequest.EscapeURL(uid) + "/displayName.json?auth=" +
            UnityWebRequest.EscapeURL(token);
        using (UnityWebRequest r = new UnityWebRequest(url, "PUT"))
        {
            r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
                "\"" + JsonEscape(displayName) + "\""));
            r.downloadHandler = new DownloadHandlerBuffer();
            r.SetRequestHeader("Content-Type", "application/json");
            r.timeout = 12;
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(r);
            await SendWebRequestAsync(r);
            if (r.responseCode != 200)
                Debug.LogWarning("ATB_GUEST_AUTH rtdb_name_cooldown_or_denial=true http=" + r.responseCode);
        }
    }

    private async Task LoadRegisteredGuestNameAsync(string uid, string token)
    {
        string url = "https://identitytoolkit.googleapis.com/v1/accounts:lookup?key=" +
            UnityWebRequest.EscapeURL(LiveGuestWebApiKey);
        using (UnityWebRequest r = new UnityWebRequest(url, "POST"))
        {
            r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(new RestLookupRequest { idToken = token })));
            r.downloadHandler = new DownloadHandlerBuffer();
            r.SetRequestHeader("Content-Type", "application/json");
            r.timeout = 12;
            await SendWebRequestAsync(r);
            if (r.responseCode != 200) return;
            RestLookupProfile profile = null;
            try { profile = JsonUtility.FromJson<RestLookupProfile>(r.downloadHandler.text); }
            catch { return; }
            if (profile?.users != null && profile.users.Length == 1 &&
                string.Equals(profile.users[0].localId, uid, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(profile.users[0].displayName))
                BindRegisteredLiveGuest(uid, profile.users[0].displayName);
        }
    }

    private void MaybeProbeRestoredRegisteredGuest()
    {
        if (!AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession || liveAccountProbeRunning ||
            Time.unscaledTime < nextLiveAccountProbe) return;
        nextLiveAccountProbe = Time.unscaledTime + 8f;
        if (!AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(out string uid,
                out string token) || string.IsNullOrWhiteSpace(uid) ||
            string.Equals(uid, registeredLiveUid, StringComparison.Ordinal)) return;
        if (uid != lastProbedLiveUid)
        {
            lastProbedLiveUid = uid;
            registeredLiveVerified = false;
            registeredLiveUid = string.Empty;
            registeredLiveDisplayName = string.Empty;
            registeredLiveEmail = string.Empty;
            registeredLiveCareerButton = null;
            registeredLiveContextButton = null;
        }
        if (!registeredLiveVerified)
            _ = ProbeRegisteredLiveGuestAsync(uid, token);
    }

    private async Task ProbeRegisteredLiveGuestAsync(string uid, string token)
    {
        if (liveAccountProbeRunning) return;
        liveAccountProbeRunning = true;
        try
        {
            string url = "https://identitytoolkit.googleapis.com/v1/accounts:lookup?key=" +
                UnityWebRequest.EscapeURL(LiveGuestWebApiKey);
            using (UnityWebRequest r = new UnityWebRequest(url, "POST"))
            {
                r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
                    JsonUtility.ToJson(new RestLookupRequest { idToken = token })));
                r.downloadHandler = new DownloadHandlerBuffer();
                r.SetRequestHeader("Content-Type", "application/json");
                r.timeout = 12;
                await SendWebRequestAsync(r);
                if (r.responseCode != 200) return;
                RestLookupProfile profile = null;
                try { profile = JsonUtility.FromJson<RestLookupProfile>(r.downloadHandler.text); }
                catch { return; }
                if (profile?.users == null || profile.users.Length != 1 ||
                    !string.Equals(profile.users[0].localId, uid, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(profile.users[0].email)) return;
                string name = profile.users[0].displayName;
                if (string.IsNullOrWhiteSpace(name))
                    name = PlayerPrefs.GetString("AtlasBoard.RegisteredGuestName." + uid,
                        string.Empty);
                if (string.IsNullOrWhiteSpace(name))
                    name = AtlasBoardGuestIdentity.NameForAuthenticatedUid(uid);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    BindRegisteredLiveGuest(uid, name);
                    registeredLiveEmail = profile.users[0].email;
                    bool profileReady = await ApplyRegisteredGuestProfileAsync(
                        uid, token, registeredLiveEmail);
                    Debug.Log("ATB_ACCOUNT_PROFILE_SYNC stage=restore completed=" +
                        profileReady + " uid_suffix=" + SafeUidSuffix(uid) +
                        " wallet_transfer=false");
                    Debug.Log("ATB_SESSION_GATE screen=bootstrap state=registered_verified source=auth_lookup uid_suffix=" +
                        SafeUidSuffix(uid));
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_LIVE_REGISTERED_SESSION restore_pending=" + e.GetType().Name);
        }
        finally { liveAccountProbeRunning = false; }
    }

    private void BindRegisteredLiveGuest(string uid, string displayName)
    {
        if (!AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession ||
            string.IsNullOrWhiteSpace(uid) || displayName.Length < 2 || displayName.Length > 32)
            return;
        registeredLiveUid = uid;
        registeredLiveDisplayName = displayName;
        registeredLiveVerified = true;
        // This value is display-only. The RTDB UID remains the authority.
        PlayerPrefs.SetString("AtlasBoard.RegisteredGuestName." + uid, displayName);
        PlayerPrefs.Save();
        AtlasBoardGuestIdentity.BindLiveAuthenticatedUid(uid);
        FieldInfo liveName = typeof(AtlasBoardGuestIdentity).GetField(
            "liveDisplayName", BindingFlags.Static | BindingFlags.NonPublic);
        if (liveName != null) liveName.SetValue(null, displayName);
        AtlasBoardCanonicalProfileHeader header =
            FindAnyObjectByType<AtlasBoardCanonicalProfileHeader>(FindObjectsInactive.Include);
        MethodInfo apply = typeof(AtlasBoardCanonicalProfileHeader).GetMethod(
            "ApplyCanonicalIdentity", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (header != null && apply != null)
        {
            try { apply.Invoke(header, new object[] { displayName }); }
            catch (Exception ex)
            {
                Debug.LogWarning("ATB_GUEST_AUTH optional_header_pending=" + ex.GetType().Name);
            }
        }
        Debug.Log("ATB_GUEST_REGISTERED_PRESENTATION bound=true uid_preserved=true name_length=" +
            displayName.Length);
    }

    private void MaintainRegisteredLiveGuestPresentation()
    {
        if (!AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession ||
            registeredLiveUid.Length == 0) return;
        if (!AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(out string uid, out _)
            || !string.Equals(uid, registeredLiveUid, StringComparison.Ordinal))
        {
            registeredLiveUid = string.Empty;
            registeredLiveDisplayName = string.Empty;
            registeredLiveEmail = string.Empty;
            registeredLiveVerified = false;
            registeredLiveCareerButton = null;
            registeredLiveContextButton = null;
            return;
        }
        FieldInfo liveName = typeof(AtlasBoardGuestIdentity).GetField(
            "liveDisplayName", BindingFlags.Static | BindingFlags.NonPublic);
        if (liveName != null && !string.Equals(liveName.GetValue(null) as string,
                registeredLiveDisplayName, StringComparison.Ordinal))
            liveName.SetValue(null, registeredLiveDisplayName);
        BindRegisteredCareerEntry();
        // The RTDB Guest name may still be under its 24h rename cooldown.
        // Only presentation is corrected, never ownership/currency records.
        if (Time.unscaledTime >= nextRegisteredHeaderRefresh)
        {
            nextRegisteredHeaderRefresh = Time.unscaledTime + 1.25f;
            ApplyRegisteredHeaderIdentity();
        }
    }

    private void ApplyRegisteredHeaderIdentity()
    {
        if (registeredLiveUid.Length == 0 ||
            string.IsNullOrWhiteSpace(registeredLiveDisplayName)) return;
        AtlasBoardCanonicalProfileHeader header = FindAnyObjectByType<
            AtlasBoardCanonicalProfileHeader>(FindObjectsInactive.Include);
        // Avoid logging/re-applying an unchanged name every 1.25 seconds.
        TMP_Text visible = GetField<TMP_Text>(header, "profileNameText");
        if (visible != null && string.Equals(
            (visible.text ?? string.Empty).Trim(), registeredLiveDisplayName.Trim(),
            StringComparison.Ordinal)) return;
        MethodInfo apply = header != null ? typeof(AtlasBoardCanonicalProfileHeader).GetMethod(
            "ApplyCanonicalIdentity", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) : null;
        if (apply != null)
        {
            try { apply.Invoke(header, new object[] { registeredLiveDisplayName }); }
            catch (Exception e)
            {
                Debug.LogWarning("ATB_GUEST_AUTH header_pending=" + e.GetType().Name);
            }
        }
    }

    private void OpenCareerAfterRestAuthentication()
    {
        if (registeredLiveUid.Length == 0) return;
        AtlasBoardProgressionUI progression = FindAnyObjectByType<
            AtlasBoardProgressionUI>(FindObjectsInactive.Include);
        MethodInfo open = progression != null ? typeof(AtlasBoardProgressionUI).GetMethod(
            "OpenCareer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) : null;
        if (open != null)
        {
            try { open.Invoke(progression, null); }
            catch (Exception ex)
            {
                Debug.LogWarning("ATB_GUEST_AUTH career_open_pending=" + ex.GetType().Name);
            }
        }
    }

    private void BindRegisteredCareerEntry()
    {
        if (registeredLiveUid.Length == 0) return;
        var ui = FindAnyObjectByType<AtlasBoardAccountAccessUI>(FindObjectsInactive.Include);
        if (ui == null) return;
        Button card = null;
        foreach (Button b in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (b != null && b.gameObject.scene.IsValid() && b.name == "ProgressionCard")
            { card = b; break; }
        }
        if (card == null || registeredLiveCareerButton == card) return;
        registeredLiveCareerButton = card;
        card.onClick = new Button.ButtonClickedEvent();
        card.onClick.AddListener(() =>
        {
            var progression = FindAnyObjectByType<AtlasBoardProgressionUI>(FindObjectsInactive.Include);
            MethodInfo open = progression != null ? typeof(AtlasBoardProgressionUI).GetMethod(
                "OpenCareer", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) : null;
            if (open != null) open.Invoke(progression, null);
        });
    }

    private static string SafeUidSuffix(string uid)
    {
        return string.IsNullOrWhiteSpace(uid) ? "none" :
            uid.Substring(Mathf.Max(0, uid.Length - 5));
    }

    private void BindRegisteredAccountContext()
    {
        if (!TryGetVerifiedRegisteredRestPrincipal(out string uid)) return;
        AtlasBoardAccountAccessUI ui = FindAnyObjectByType<
            AtlasBoardAccountAccessUI>(FindObjectsInactive.Include);
        if (ui == null) return;
        Button context = GetField<Button>(ui, "contextButton");
        if (context == null || registeredLiveContextButton == context) return;
        registeredLiveContextButton = context;
        context.onClick = new Button.ButtonClickedEvent();
        context.onClick.AddListener(() =>
        {
            if (!TryGetVerifiedRegisteredRestPrincipal(out string activeUid))
            {
                registeredLiveContextButton = null;
                Debug.LogWarning("ATB_SESSION_GATE screen=account state=not_verified");
                return;
            }
            Debug.Log("ATB_SESSION_GATE screen=account state=registered_verified uid_suffix=" +
                SafeUidSuffix(activeUid));
            string safeName = string.IsNullOrWhiteSpace(registeredLiveDisplayName)
                ? "AtlasBoard" : registeredLiveDisplayName;
            string summary = "Kayıtlı hesap: " + safeName +
                (string.IsNullOrWhiteSpace(registeredLiveEmail)
                    ? "" : "\nE-posta: " + registeredLiveEmail) +
                "\nHesabın ve ilerlemen bu oyuncuya bağlı.";
            OpenRegisteredRestAccountPanel(ui);
        });
        Debug.Log("ATB_SESSION_GATE screen=settings_account state=callback_bound uid_suffix=" +
            SafeUidSuffix(uid));
    }

    // The REST-only Guest QA client must NOT show the native account form.
    // The native Firebase Account view still works normally for Steam/mobile.
    private void OpenRegisteredRestAccountPanel(AtlasBoardAccountAccessUI ui)
    {
        if (restAccountBusy || !TryGetVerifiedRegisteredRestPrincipal(out string uid)) return;
        var modal = GetField<GameObject>(ui, "modalBackdrop");
        if (modal == null) return;
        if (registeredAccountPanel != null) Destroy(registeredAccountPanel);
        registeredAccountPanel = CreateNoticePanel(modal.transform.parent,
            "ATB_Registered_Account_Panel", "", new Color32(23, 47, 72, 255));
        var root = (RectTransform)registeredAccountPanel.transform;
        root.sizeDelta = new Vector2(750f, 620f);
        var title = registeredAccountPanel.transform.Find("Title")?.GetComponent<TMP_Text>();
        if (title != null)
        {
            title.text = "ATLAS HESABI";
            title.rectTransform.anchoredPosition = new Vector2(0f, 248f);
            title.rectTransform.sizeDelta = new Vector2(670f, 52f);
        }
        var message = registeredAccountPanel.transform.Find("Message")?.GetComponent<TMP_Text>();
        if (message != null)
        {
            message.text = "Oyuncu: " + registeredLiveDisplayName +
                (string.IsNullOrEmpty(registeredLiveEmail) ? "" :
                    "\nE-posta: " + registeredLiveEmail) +
                "\nİlerlemen bu hesapta saklanıyor.";
            message.rectTransform.anchoredPosition = new Vector2(0f, 169f);
            message.rectTransform.sizeDelta = new Vector2(660f, 98f);
            message.fontSize = 20f;
        }
        var close = registeredAccountPanel.transform.Find("Close") as RectTransform;
        if (close != null)
        {
            close.anchoredPosition = new Vector2(0f, -259f);
            var label = close.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null) label.text = "KAPAT";
        }
        NewDialogText(registeredAccountPanel.transform, "RenameDescription",
            "Oyuncu adını değiştir (24 saatte bir)", 18f,
            new Vector2(0f, 95f), new Vector2(650f, 35f));
        TMP_InputField newName = NewAccountTextInput(registeredAccountPanel.transform,
            "RenameText", registeredLiveDisplayName, new Vector2(0f, 38f));
        NewAccountAction(registeredAccountPanel.transform, "RenameAction", "ADI DEĞİŞTİR",
            new Vector2(0f, -25f), new Vector2(278f, 51f),
            new Color32(34, 141, 185, 255),
            () => _ = RenameRegisteredRestAccountAsync(uid, newName.text, ui));
        NewAccountAction(registeredAccountPanel.transform, "ChangePassword", "ŞİFREYİ DEĞİŞTİR",
            new Vector2(0f, -104f), new Vector2(332f, 51f),
            new Color32(77, 81, 201, 255),
            () => _ = SendPasswordResetRestAsync(ui, registeredLiveEmail, null));
        NewAccountAction(registeredAccountPanel.transform, "LogOut", "HESAPTAN ÇIKIŞ YAP",
            new Vector2(0f, -187f), new Vector2(343f, 52f),
            new Color32(181, 37, 75, 255),
            () => ConfirmRegisteredRestSignOut(ui));
        Debug.Log("ATB_SESSION_GATE screen=account_management action=open auth=registered_rest_verified uid_suffix=" +
            SafeUidSuffix(uid));
    }

    private static TMP_InputField NewAccountTextInput(Transform parent,
        string name, string value, Vector2 position)
    {
        GameObject container = new GameObject(name,
            typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        container.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)container.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(555f, 58f);
        rect.anchoredPosition = position;
        var image = container.GetComponent<Image>();
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        var input = container.GetComponent<TMP_InputField>();
        var text = (TextMeshProUGUI)NewDialogText(container.transform,
            "Text", value, 23f, Vector2.zero, new Vector2(510f, 51f));
        text.alignment = TextAlignmentOptions.Left;
        text.color = new Color32(45, 55, 65, 255);
        input.textComponent = text;
        input.textViewport = rect;
        input.characterLimit = 32;
        input.text = value;
        input.customCaretColor = true;
        input.caretColor = new Color32(15, 76, 131, 255);
        input.caretWidth = 3;
        return input;
    }

    private static Button NewAccountAction(Transform parent, string name,
        string label, Vector2 position, Vector2 size, Color32 color,
        UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = go.GetComponent<Image>();
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        Button result = go.GetComponent<Button>();
        result.onClick.AddListener(action);
        var text = NewDialogText(go.transform, "Label", label, 20f,
            Vector2.zero, size);
        text.fontStyle = FontStyles.Bold;
        return result;
    }

    // Rules enforce 24h server cooldown. Never update the Auth nickname when
    // RTDB rejects the change; this avoids two divergent profile identities.
    private async Task RenameRegisteredRestAccountAsync(
        string requestedUid, string newName, AtlasBoardAccountAccessUI ui)
    {
        if (restAccountBusy) return;
        newName = (newName ?? string.Empty).Trim();
        if (newName.Length < 2 || newName.Length > 32)
        {
            ShowGlobalAccountNotice(ui,
                "Oyuncu adı 2–32 karakter olmalı. Destek kodu: ATB743");
            return;
        }
        if (!TryGetVerifiedRegisteredRestPrincipal(out string currentUid) ||
            !string.Equals(requestedUid, currentUid, StringComparison.Ordinal) ||
            !AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(
                out _, out string token)) return;
        if (string.Equals(newName, registeredLiveDisplayName, StringComparison.Ordinal))
        {
            ShowGlobalAccountNotice(ui, "Oyuncu adın zaten bu şekilde kayıtlı.");
            return;
        }
        restAccountBusy = true;
        try
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string payload = "{\"displayName\":\"" + JsonEscape(newName) +
                "\",\"lastDisplayNameChangeAt\":" + now +
                ",\"updatedAt\":" + now + "}";
            string url = LiveGuestDatabase + "/users/" +
                UnityWebRequest.EscapeURL(currentUid) + ".json?auth=" +
                UnityWebRequest.EscapeURL(token);
            using (UnityWebRequest req = new UnityWebRequest(url, "PATCH"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 15;
                await AtlasBoardAppCheckRequestSecurity.ApplyAsync(req);
                await SendWebRequestAsync(req);
                if (req.responseCode < 200 || req.responseCode >= 300)
                {
                    Debug.LogWarning("ATB_ACCOUNT_RENAME status=denied_or_cooldown http=" +
                        req.responseCode + " uid_suffix=" + SafeUidSuffix(currentUid));
                    ShowGlobalAccountNotice(ui,
                        "Oyuncu adın şu anda değiştirilemiyor. Son değişiklikten sonra 24 saat geçmeli. Destek kodu: ATB743");
                    return;
                }
            }
            await TrySaveRegisteredDisplayNameInFirebaseAuthAsync(currentUid, token, newName);
            if (TryGetVerifiedRegisteredRestPrincipal(out string stillUid) &&
                stillUid == currentUid)
            {
                BindRegisteredLiveGuest(currentUid, newName);
                ApplyRegisteredHeaderIdentity();
                if (registeredAccountPanel != null) Destroy(registeredAccountPanel);
                ShowGlobalAccountNotice(ui, "Oyuncu adın değiştirildi.");
                Debug.Log("ATB_ACCOUNT_RENAME status=success uid_suffix=" + SafeUidSuffix(currentUid));
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_ACCOUNT_RENAME status=error type=" + ex.GetType().Name);
            ShowGlobalAccountNotice(ui,
                "Ad değiştirilemedi. Tekrar dene. Destek kodu: ATB743");
        }
        finally { restAccountBusy = false; }
    }

    private void ConfirmRegisteredRestSignOut(AtlasBoardAccountAccessUI ui)
    {
        if (restAccountBusy || !TryGetVerifiedRegisteredRestPrincipal(out _)) return;
        Transform parent = registeredAccountPanel != null ?
            registeredAccountPanel.transform.parent : null;
        if (parent == null) return;
        GameObject dialog = CreateNoticePanel(parent, "ATB_Signout_Confirm",
            "Hesabından çıkınca yeni bir misafir hesabı açılacak. " +
            "Eski hesabının Gold, XP ve satın alımları korunacak; " +
            "yeni misafirin kazançları eski hesabına eklenmeyecek.",
            new Color32(23, 47, 72, 255));
        var cancel = dialog.transform.Find("Close")?.GetComponent<Button>();
        if (cancel != null)
        {
            var lbl = cancel.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (lbl != null) lbl.text = "VAZGEÇ";
        }
        NewAccountAction(dialog.transform, "ConfirmSignout", "ÇIKIŞ YAP",
            new Vector2(151f, -127f), new Vector2(225f, 49f),
            new Color32(181, 37, 75, 255),
            () => _ = SignOutRestToNewGuestAsync(ui, dialog));
        var panelRect = (RectTransform)dialog.transform;
        panelRect.sizeDelta = new Vector2(670f, 345f);
        var msg = dialog.transform.Find("Message")?.GetComponent<TMP_Text>();
        if (msg != null)
        {
            msg.rectTransform.sizeDelta = new Vector2(600f, 128f);
            msg.fontSize = 17f;
            msg.rectTransform.anchoredPosition = new Vector2(0f, 19f);
            msg.enableAutoSizing = false;
        }
        var cr = dialog.transform.Find("Close") as RectTransform;
        if (cr != null) cr.anchoredPosition = new Vector2(-151f, -127f);
    }

    private static void RestoreGuestAccountAndCareerButtons(AtlasBoardAccountAccessUI ui)
    {
        if (ui == null) return;
        // The signed-in QA overlay temporarily replaces the Career listener.
        // Reinstall the canonical unsigned gate after logging out; otherwise
        // a fresh anonymous Guest could still bypass the sign-in prompt.
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            FieldInfo bound = typeof(AtlasBoardAccountAccessUI).GetField(
                "boundCareerButton", flags);
            bound?.SetValue(ui, null);
            MethodInfo bind = typeof(AtlasBoardAccountAccessUI).GetMethod(
                "BindCareerButton", flags);
            bind?.Invoke(ui, null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_ACCOUNT_SIGNOUT career_rebind_pending=" +
                ex.GetType().Name);
        }
        var context = GetField<Button>(ui, "contextButton");
        if (context != null)
        {
            context.onClick = new Button.ButtonClickedEvent();
            context.onClick.AddListener(() =>
            {
                try
                {
                    Type viewType = typeof(AtlasBoardAccountAccessUI).GetNestedType(
                        "AuthView", BindingFlags.NonPublic);
                    MethodInfo open = typeof(AtlasBoardAccountAccessUI).GetMethod(
                        "OpenModal", flags);
                    if (viewType == null || open == null) return;
                    object login = Enum.Parse(viewType, "Login");
                    open.Invoke(ui, new object[] { login });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("ATB_ACCOUNT_SIGNOUT login_gate_pending=" +
                        ex.GetType().Name);
                }
            });
        }
        Debug.Log("ATB_SESSION_GATE screen=logout listeners=guest_restored");
    }

    [Serializable]
    private sealed class FreshAnonymousAccountRequest
    {
        public bool returnSecureToken = true;
    }

    private async Task SignOutRestToNewGuestAsync(
        AtlasBoardAccountAccessUI ui, GameObject confirmDialog)
    {
        if (restAccountBusy || !TryGetVerifiedRegisteredRestPrincipal(out string oldUid)) return;
        restAccountBusy = true;
        try
        {
            // A fresh anonymous account is created by Firebase Auth itself.
            // Never delete the old registered user or copy/merge wallet data.
            string url = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" +
                UnityWebRequest.EscapeURL(LiveGuestWebApiKey);
            using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
                    JsonUtility.ToJson(new FreshAnonymousAccountRequest())));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 20;
                await SendWebRequestAsync(req);
                if (req.responseCode < 200 || req.responseCode >= 300)
                {
                    ShowGlobalAccountNotice(ui,
                        "Çıkış işlemi tamamlanamadı. Eski hesabın açık. Destek kodu: ATB744");
                    return;
                }
                RestAuthResponse account = JsonUtility.FromJson<RestAuthResponse>(
                    req.downloadHandler.text);
                if (account == null || string.IsNullOrWhiteSpace(account.localId) ||
                    string.IsNullOrWhiteSpace(account.idToken) ||
                    string.IsNullOrWhiteSpace(account.refreshToken) ||
                    string.Equals(account.localId, oldUid, StringComparison.Ordinal))
                    throw new InvalidOperationException("ANONYMOUS_UID_CONTRACT");
                // Persist the new principal FIRST. On failure the old session
                // stays active; a new anonymous account is not linked to it.
                var bridge = FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>(
                    FindObjectsInactive.Include);
                MethodInfo saver = typeof(AtlasBoardLobbyRuntimeBridge).GetMethod(
                    "TrySaveLiveGuestCredentials", BindingFlags.NonPublic | BindingFlags.Static);
                if (bridge == null || saver == null || !(bool)saver.Invoke(null,
                        new object[] { account.localId, account.refreshToken }))
                    throw new InvalidOperationException("NEW_GUEST_PERSISTENCE");
                SetBridgeField(bridge, "currentAccountId", account.localId);
                SetBridgeField(bridge, "idToken", account.idToken);
                SetBridgeField(bridge, "liveGuestRefreshToken", account.refreshToken);
                SetBridgeField(bridge, "initialized", true);
                SetBridgeField(bridge, "nextIdentityRefreshAt", Time.unscaledTime + 2700f);
                bridge.ClearLocalLobbyState();
                // Drop previous UID's emote selection when leaving a registered
                // account. The fresh anonymous UID starts with free defaults.
                AtlasBoardEmoteLoadoutService.Invalidate();
                registeredLiveUid = registeredLiveDisplayName = registeredLiveEmail = string.Empty;
                registeredLiveVerified = false;
                registeredLiveCareerButton = null;
                registeredLiveContextButton = null;
                lastProbedLiveUid = string.Empty;
                RestoreGuestAccountAndCareerButtons(ui);
                AtlasBoardGuestIdentity.BindLiveAuthenticatedUid(account.localId);
                // Clear the canonical wallet presentation immediately; the
                // previous registered snapshot must not appear on Guest UI.
                try { await AtlasBoardRtdbMetaEconomy.LoadAsync(); }
                catch (Exception e)
                {
                    Debug.LogWarning("ATB_ACCOUNT_SIGNOUT new_guest_wallet_refresh_pending=" +
                        e.GetType().Name);
                }
                var profileHeader = FindAnyObjectByType<AtlasBoardCanonicalProfileHeader>(
                    FindObjectsInactive.Include);
                if (profileHeader != null) profileHeader.RefreshNow();
                if (registeredAccountPanel != null) Destroy(registeredAccountPanel);
                if (confirmDialog != null) Destroy(confirmDialog);
                var accountUi = FindAnyObjectByType<AtlasBoardAccountAccessUI>(
                    FindObjectsInactive.Include);
                var contextButton = GetField<Button>(accountUi, "contextButton");
                if (contextButton != null) contextButton.gameObject.SetActive(false);
                Debug.Log("ATB_ACCOUNT_SIGNOUT result=new_anonymous uid_changed=true " +
                    "old_suffix=" + SafeUidSuffix(oldUid) +
                    " new_suffix=" + SafeUidSuffix(account.localId) + " wallet_transfer=false");
                ShowGlobalAccountNotice(ui,
                    "Hesabından çıkış yapıldı. Yeni bir misafir hesabı açıldı. " +
                    "Eski hesabına tekrar giriş yapabilirsin; misafir ödülleri aktarılmaz.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_ACCOUNT_SIGNOUT status=failed type=" + ex.GetType().Name);
            ShowGlobalAccountNotice(ui,
                "Çıkış işlemi tamamlanamadı. Destek kodu: ATB744");
        }
        finally { restAccountBusy = false; }
    }

    private void KeepRegisteredAccountLabel()
    {
        if (!TryGetVerifiedRegisteredRestPrincipal(out _)) return;
        AtlasBoardAccountAccessUI ui = FindAnyObjectByType<
            AtlasBoardAccountAccessUI>(FindObjectsInactive.Include);
        TMP_Text label = GetField<TMP_Text>(ui, "contextButtonLabel");
        if (label == null) return;
        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;
        string language = AtlasBoardLocalizationLanguages.Normalize(
            localization != null ? localization.CurrentLanguageCode : "tr");
        string expected = language == "tr" ? "HESAP" :
            language == "es" ? "CUENTA" :
            language == "fr" ? "COMPTE" :
            language == "de" ? "KONTO" :
            language == "ko" ? "계정" :
            language == "ru" ? "АККАУНТ" : "ACCOUNT";
        if (label.text != expected) label.text = expected;
    }

    private void EmitSessionAudit()
    {
        if (!AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession ||
            Time.unscaledTime < nextSessionAudit) return;
        nextSessionAudit = Time.unscaledTime + 20f;
        bool verified = TryGetVerifiedRegisteredRestPrincipal(out string uid);
        bool economyGate = AtlasBoardRtdbMetaEconomy.IsRegistered;
        Debug.Log("ATB_SESSION_GATE screen=global auth=" +
            (verified ? "registered_rest_verified" : "guest_or_pending") +
            " uid_suffix=" + SafeUidSuffix(uid) +
            " store_native_registered_gate=" + (economyGate ? "true" : "false") +
            " native_auth_not_authoritative=true");
    }

    private void ShowAuthErrorPopup(AtlasBoardAccountAccessUI ui, string text)
    {
        // Show the error only once, inside the dedicated popup.
        ShowStatus(ui, string.Empty);
        GameObject modal = GetField<GameObject>(ui, "modalBackdrop");
        if (modal == null) return;
        if (authErrorDialog != null) UnityEngine.Object.Destroy(authErrorDialog);
        authErrorDialog = new GameObject("ATB_Account_Error_Overlay", typeof(RectTransform), typeof(Image));
        authErrorDialog.transform.SetParent(modal.transform, false);
        RectTransform rect = (RectTransform)authErrorDialog.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(590f, 250f);
        Image background = authErrorDialog.GetComponent<Image>();
        background.sprite = RoundedSprite();
        background.type = Image.Type.Sliced;
        background.color = new Color32(23, 47, 72, 255);
        TMP_Text title = NewDialogText(authErrorDialog.transform, "Title", "ATLAS BOARD", 27,
            new Vector2(0f, 76f), new Vector2(530f, 46f));
        title.fontStyle = FontStyles.Bold;
        NewDialogText(authErrorDialog.transform, "Message", text, 19,
            new Vector2(0f, 10f), new Vector2(530f, 90f));
        GameObject close = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
        close.transform.SetParent(authErrorDialog.transform, false);
        RectTransform cr = (RectTransform)close.transform;
        cr.anchorMin = cr.anchorMax = new Vector2(.5f, .5f);
        cr.sizeDelta = new Vector2(200f, 43f);
        cr.anchoredPosition = new Vector2(0f, -89f);
        Image ci = close.GetComponent<Image>();
        ci.sprite = RoundedSprite();
        ci.type = Image.Type.Sliced;
        ci.color = new Color32(33, 137, 189, 255);
        close.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (authErrorDialog != null) UnityEngine.Object.Destroy(authErrorDialog);
            authErrorDialog = null;
        });
        NewDialogText(close.transform, "Label", "KAPAT", 20, Vector2.zero,
            new Vector2(196f, 42f)).fontStyle = FontStyles.Bold;
    }

    // Success confirmation lives outside the Account modal because the modal
    // is intentionally closed after a successful sign-in. This makes both
    // success and failure visible without exposing provider internals.
    private void ShowGlobalAccountNotice(AtlasBoardAccountAccessUI ui, string text)
    {
        GameObject modal = GetField<GameObject>(ui, "modalBackdrop");
        Transform parent = modal != null ? modal.transform.parent : null;
        if (parent == null) return;
        if (accountNoticeDialog != null) Destroy(accountNoticeDialog);
        accountNoticeDialog = CreateNoticePanel(parent, "ATB_Account_Success", text,
            new Color32(22, 79, 82, 255));
    }

    private GameObject CreateNoticePanel(Transform parent, string name, string message,
        Color32 color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform root = (RectTransform)panel.transform;
        root.anchorMin = root.anchorMax = new Vector2(.5f, .5f);
        root.pivot = new Vector2(.5f, .5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = new Vector2(620f, 270f);
        Image image = panel.GetComponent<Image>();
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        TMP_Text title = NewDialogText(panel.transform, "Title", "ATLAS BOARD",
            27f, new Vector2(0f, 91f), new Vector2(560f, 48f));
        title.fontStyle = FontStyles.Bold;
        NewDialogText(panel.transform, "Message", message,
            19f, new Vector2(0f, 27f), new Vector2(560f, 83f));
        GameObject close = new GameObject("Close", typeof(RectTransform),
            typeof(Image), typeof(Button));
        close.transform.SetParent(panel.transform, false);
        RectTransform cr = (RectTransform)close.transform;
        cr.anchorMin = cr.anchorMax = new Vector2(.5f, .5f);
        cr.sizeDelta = new Vector2(206f, 46f);
        cr.anchoredPosition = new Vector2(0f, -92f);
        Image bg = close.GetComponent<Image>();
        bg.sprite = RoundedSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color32(33, 137, 189, 255);
        close.GetComponent<Button>().onClick.AddListener(() =>
        {
            if (panel != null) Destroy(panel);
        });
        NewDialogText(close.transform, "Label", "KAPAT", 19,
            Vector2.zero, new Vector2(200f, 43f)).fontStyle = FontStyles.Bold;
        panel.transform.SetAsLastSibling();
        return panel;
    }

    [Serializable]
    private sealed class PasswordResetEmailRequest
    {
        public string requestType = "PASSWORD_RESET";
        public string email;
    }

    // Both Email/Password and Anonymous are already enabled by the owner.
    // In the REST-only Windows Guest EXE, password reset must use the same
    // Firebase project without waking the native SDK or creating any UID.
    private void ShowPasswordResetDialog(AtlasBoardAccountAccessUI ui)
    {
        if (passwordResetBusy) return;
        GameObject modal = GetField<GameObject>(ui, "modalBackdrop");
        if (modal == null) return;
        if (passwordResetDialog != null) Destroy(passwordResetDialog);
        passwordResetDialog = CreateNoticePanel(modal.transform,
            "ATB_Password_Reset", "E-posta adresini gir. Şifre sıfırlama bağlantısını göndereceğiz.",
            new Color32(23, 47, 72, 255));
        RectTransform root = passwordResetDialog.transform as RectTransform;
        root.sizeDelta = new Vector2(650f, 410f);
        TMP_Text title = passwordResetDialog.transform.Find("Title")?
            .GetComponent<TMP_Text>();
        if (title != null)
        {
            title.rectTransform.anchoredPosition = new Vector2(0f, 145f);
            title.rectTransform.sizeDelta = new Vector2(560f, 44f);
        }
        TMP_Text message = passwordResetDialog.transform.Find("Message")?
            .GetComponent<TMP_Text>();
        if (message != null)
        {
            message.rectTransform.anchoredPosition = new Vector2(0f, 71f);
            message.rectTransform.sizeDelta = new Vector2(560f, 55f);
            message.fontSize = 17f;
            message.enableAutoSizing = false;
            message.alignment = TextAlignmentOptions.Center;
        }
        RectTransform closeRect = passwordResetDialog.transform.Find("Close") as RectTransform;
        if (closeRect != null)
        {
            closeRect.anchoredPosition = new Vector2(155f, -151f);
            TextMeshProUGUI label = closeRect.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null) label.text = "VAZGEÇ";
        }
        GameObject entry = new GameObject("ResetEmail", typeof(RectTransform),
            typeof(Image), typeof(TMP_InputField));
        entry.transform.SetParent(passwordResetDialog.transform, false);
        RectTransform er = entry.transform as RectTransform;
        er.anchorMin = er.anchorMax = new Vector2(.5f, .5f);
        er.sizeDelta = new Vector2(545f, 65f);
        er.anchoredPosition = new Vector2(0f, -12f);
        Image entryBackground = entry.GetComponent<Image>();
        entryBackground.sprite = RoundedSprite();
        entryBackground.type = Image.Type.Sliced;
        entryBackground.color = Color.white;
        TMP_InputField emailInput = entry.GetComponent<TMP_InputField>();
        emailInput.textViewport = er;
        TextMeshProUGUI text = (TextMeshProUGUI)NewDialogText(entry.transform,
            "Text", string.Empty, 22f, new Vector2(0f, 0f), new Vector2(510f, 48f));
        text.alignment = TextAlignmentOptions.Left;
        text.color = new Color32(42, 58, 72, 255);
        emailInput.textComponent = text;
        emailInput.contentType = TMP_InputField.ContentType.EmailAddress;
        emailInput.customCaretColor = true;
        emailInput.caretColor = new Color32(20, 72, 129, 255);
        emailInput.caretWidth = 3;
        emailInput.characterLimit = 254;
        string existing = GetText(ui, "loginEmail").Trim();
        emailInput.text = existing;
        GameObject send = new GameObject("SendReset", typeof(RectTransform),
            typeof(Image), typeof(Button));
        send.transform.SetParent(passwordResetDialog.transform, false);
        RectTransform sr = send.transform as RectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(.5f, .5f);
        sr.sizeDelta = new Vector2(245f, 48f);
        sr.anchoredPosition = new Vector2(-155f, -151f);
        Image sbg = send.GetComponent<Image>();
        sbg.sprite = RoundedSprite();
        sbg.type = Image.Type.Sliced;
        sbg.color = new Color32(33, 137, 189, 255);
        Button sendButton = send.GetComponent<Button>();
        sendButton.onClick.AddListener(() => _ = SendPasswordResetRestAsync(
            ui, emailInput.text, sendButton));
        NewDialogText(send.transform, "Label", "E-POSTA GÖNDER", 19,
            Vector2.zero, new Vector2(235f, 44f)).fontStyle = FontStyles.Bold;
        emailInput.Select();
        emailInput.ActivateInputField();
    }

    private async Task SendPasswordResetRestAsync(AtlasBoardAccountAccessUI ui,
        string enteredEmail, Button sendButton)
    {
        if (passwordResetBusy) return;
        string email = (enteredEmail ?? string.Empty).Trim();
        if (!EmailFormat.IsMatch(email))
        {
            ShowAuthErrorPopup(ui,
                "Geçerli bir e-posta adresi gir. Destek kodu: ATB741");
            return;
        }
        passwordResetBusy = true;
        if (sendButton != null) sendButton.interactable = false;
        try
        {
            string url = "https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key=" +
                UnityWebRequest.EscapeURL(LiveGuestWebApiKey);
            using (UnityWebRequest r = new UnityWebRequest(url, "POST"))
            {
                r.downloadHandler = new DownloadHandlerBuffer();
                r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
                    JsonUtility.ToJson(new PasswordResetEmailRequest { email = email })));
                r.SetRequestHeader("Content-Type", "application/json");
                r.timeout = 20;
                await SendWebRequestAsync(r);
                if (r.result != UnityWebRequest.Result.Success || r.responseCode < 200 ||
                    r.responseCode >= 300)
                {
                    // No account enumeration: don't disclose if the email exists.
                    string code = "UNKNOWN";
                    try
                    {
                        RestAuthErrorEnvelope failure = JsonUtility.FromJson<
                            RestAuthErrorEnvelope>(r.downloadHandler.text);
                        code = SafeFirebaseErrorCode(failure?.error?.message ?? "UNKNOWN");
                    }
                    catch { /* No raw response or personal data in logs. */ }
                    Debug.LogWarning("ATB_PASSWORD_RESET result=failed http=" + r.responseCode +
                        " provider_code=" + code + " email_redacted=true");
                    if (code == "EMAIL_NOT_FOUND" || code == "INVALID_EMAIL")
                    {
                        FinishPasswordResetNotice(ui);
                        return;
                    }
                    ShowAuthErrorPopup(ui,
                        "Sıfırlama isteği tamamlanamadı. Daha sonra dene. Destek kodu: ATB742");
                    return;
                }
                Debug.Log("ATB_PASSWORD_RESET request_accepted=true email_redacted=true");
                FinishPasswordResetNotice(ui);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_PASSWORD_RESET transport_failed type=" + e.GetType().Name);
            ShowAuthErrorPopup(ui,
                "Bağlantı kurulamadı. Daha sonra tekrar dene. Destek kodu: ATB742");
        }
        finally
        {
            passwordResetBusy = false;
            if (sendButton != null) sendButton.interactable = true;
        }
    }

    private void FinishPasswordResetNotice(AtlasBoardAccountAccessUI ui)
    {
        if (passwordResetDialog != null)
        {
            Destroy(passwordResetDialog);
            passwordResetDialog = null;
        }
        // Generic confirmation avoids leaking registered email addresses.
        GameObject modal = GetField<GameObject>(ui, "modalBackdrop");
        if (modal != null) modal.SetActive(false);
        ShowGlobalAccountNotice(ui,
            "Bu adrese bağlı bir hesap varsa şifre sıfırlama bağlantısı gönderildi. " +
            "E-postanı ve Spam/Gereksiz klasörünü kontrol et; " +
            "ardından yeni şifrenle giriş yap.");
    }

    private static TMP_Text NewDialogText(Transform parent, string name, string value,
        float size, Vector2 position, Vector2 dimensions)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
        r.sizeDelta = dimensions;
        r.anchoredPosition = position;
        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        t.text = value;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        return t;
    }

    private static string SafeFirebaseErrorCode(string raw)
    {
        // Firebase errors can contain attacker-controlled or identifying text.
        // Restrict diagnostics to uppercase API error labels only.
        if (string.IsNullOrWhiteSpace(raw)) return "AUTH_UNAVAILABLE";
        string code = raw.Trim().Split(' ', ':')[0];
        if (code.Length < 1 || code.Length > 70) return "AUTH_UNAVAILABLE";
        foreach (char c in code)
            if (!(c >= 'A' && c <= 'Z') && c != '_' && !(c >= '0' && c <= '9'))
                return "AUTH_UNAVAILABLE";
        return code;
    }

    private static async Task<bool> IsStillAnonymousLiveGuestAsync(string token)
    {
        string url = "https://identitytoolkit.googleapis.com/v1/accounts:lookup?key=" +
            UnityWebRequest.EscapeURL(LiveGuestWebApiKey);
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(new RestLookupRequest { idToken = token })));
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 15;
            await SendWebRequestAsync(request);
            if (request.result != UnityWebRequest.Result.Success ||
                request.responseCode != 200) return false;
            RestLookupResponse response = null;
            try { response = JsonUtility.FromJson<RestLookupResponse>(request.downloadHandler.text); }
            catch { return false; }
            return response?.users != null && response.users.Length == 1 &&
                string.IsNullOrWhiteSpace(response.users[0]?.email);
        }
    }

    private static void SetBridgeField(object bridge, string name, object value)
    {
        FieldInfo f = bridge.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (f == null) throw new MissingFieldException(name);
        f.SetValue(bridge, value);
    }

    private static async Task<bool> ApplyRegisteredGuestProfileAsync(string uid, string token, string email)
    {
        // Atomic RTDB multi-path update preserves wallet, inventory and
        // immutable profile identity. No new UID, gold, XP or rewards are minted.
        string url = LiveGuestDatabase + "/users/" + UnityWebRequest.EscapeURL(uid) +
            ".json?auth=" + UnityWebRequest.EscapeURL(token);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string payload = "{\"email\":\"" + JsonEscape(email) + "\",\"general/accountType\":\"registered\"," +
            "\"general/lastSeenAt\":" + now + ",\"updatedAt\":" + now + "}";
        using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 15;
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);
            await SendWebRequestAsync(request);
            bool accepted = request.result == UnityWebRequest.Result.Success &&
                request.responseCode >= 200 && request.responseCode < 300;
            if (!accepted)
                Debug.LogWarning("ATB_GUEST_PROFILE_UPGRADE rtdb_write=false http=" +
                    request.responseCode + " uid_unchanged=true email_redacted=true");
            return accepted;
        }
    }

    // Public UI contract: no provider/backend names or raw errors. Internal
    // categories stay in the Development diagnostics and error codebook.
    private static string PlayerAuthErrorCode(string providerCode, bool registering)
    {
        // Do not expose Identity Toolkit endpoint, operation or provider error
        // strings in the UI. A support code is enough for internal diagnostics.
        switch (providerCode)
        {
            case "EMAIL_EXISTS":
                return "Bu e-posta zaten kayıtlı. Giriş yapmayı dene. Destek kodu: ATB732";
            case "INVALID_PASSWORD":
            case "INVALID_LOGIN_CREDENTIALS":
            case "EMAIL_NOT_FOUND":
                return "E-posta veya şifre hatalı. Destek kodu: ATB733";
            case "INVALID_ID_TOKEN":
            case "TOKEN_EXPIRED":
                return "Oturumun süresi doldu. Oyunu yeniden açıp dene. Destek kodu: ATB734";
            case "OPERATION_NOT_ALLOWED":
                return "Hesap işlemi şu anda kullanılamıyor. Destek kodu: ATB735";
            case "TOO_MANY_ATTEMPTS_TRY_LATER":
                return "Çok fazla deneme yapıldı. Bir süre sonra tekrar dene. Destek kodu: ATB736";
            case "WEAK_PASSWORD":
                return "Daha güçlü bir şifre belirle. Destek kodu: ATB737";
            default:
                return registering
                    ? "Hesap oluşturulamadı. Bağlantını kontrol et. Destek kodu: ATB731"
                    : "Giriş tamamlanamadı. Bağlantını kontrol et. Destek kodu: ATB731";
        }
    }

    private static string JsonEscape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static async Task SendWebRequestAsync(UnityWebRequest request)
    {
        var operation = request.SendWebRequest();
        while (!operation.isDone) await Task.Yield();
    }

    private static void StabilizeStatusText()
    {
        AtlasBoardAccountAccessUI ui =
            FindAnyObjectByType<AtlasBoardAccountAccessUI>(FindObjectsInactive.Include);
        if (ui == null) return;
        GameObject modal = GetField<GameObject>(ui, "modalBackdrop");
        if (modal == null || !modal.activeInHierarchy) return;
        TMP_Text status = GetField<TMP_Text>(ui, "statusText");
        if (status == null) return;
        bool register = FindChildButton(ui, "Button_CreateAccount")?.gameObject.activeInHierarchy == true;
        bool login = FindChildButton(ui, "Button_SignIn")?.gameObject.activeInHierarchy == true;
        if (!register && !login) return;
        status.enableAutoSizing = false;
        status.fontSize = 16f;
        status.textWrappingMode = TextWrappingModes.NoWrap;
        status.overflowMode = TextOverflowModes.Ellipsis;
        // Top alignment prevents short and long messages from shifting their
        // baseline within an unchanged RectTransform. Never scale the label.
        status.alignment = TextAlignmentOptions.Top;
        status.color = new Color32(137, 57, 64, 255);
        RectTransform rect = status.rectTransform;
        Vector2 target = new Vector2(0f, register ? -337f : -296f);
        if ((rect.anchoredPosition - target).sqrMagnitude > .01f)
            rect.anchoredPosition = target;
        Vector2 size = new Vector2(790f, 40f);
        if ((rect.sizeDelta - size).sqrMagnitude > .01f)
            rect.sizeDelta = size;
        if (login)
        {
            Button guestButton = FindChildButton(ui, "Button_ContinueAsGuest");
            if (guestButton != null)
            {
                RectTransform guestRect = guestButton.transform as RectTransform;
                Vector2 guestTarget = new Vector2(0f, -337f);
                if (guestRect != null &&
                    (guestRect.anchoredPosition - guestTarget).sqrMagnitude > .01f)
                    guestRect.anchoredPosition = guestTarget;
            }
        }
    }

    private void HandleKeyboardTab()
    {
        bool tab = false, reverse = false;
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            tab = keyboard.tabKey.wasPressedThisFrame;
            reverse = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        tab = Input.GetKeyDown(KeyCode.Tab);
        reverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
        if (!tab) return;
        AtlasBoardAccountAccessUI ui = FindAnyObjectByType<AtlasBoardAccountAccessUI>(FindObjectsInactive.Include);
        if (ui == null || !ui.gameObject.activeInHierarchy) return;
        GameObject modal = GetField<GameObject>(ui, "modalBackdrop");
        if (modal == null || !modal.activeInHierarchy) return;
        bool register = FindChildButton(ui, "Button_CreateAccount")?.gameObject.activeInHierarchy == true;
        string[] names = register
            ? new[] {"RegisterName", "RegisterEmail", "RegisterPassword", "RegisterPasswordRepeat",
                     "CaptchaAnswer", "Button_CreateAccount", "Button_BackToLogin"}
            : new[] {"LoginEmail", "LoginPassword", "Button_SignIn", "Button_OpenRegister",
                     "Button_ResetPassword", "Button_ContinueAsGuest"};
        List<Selectable> items = new List<Selectable>();
        foreach (string name in names)
            foreach (Selectable s in ui.GetComponentsInChildren<Selectable>(true))
                if (s != null && s.name == name && s.gameObject.activeInHierarchy && s.IsInteractable())
                { items.Add(s); break; }
        if (items.Count == 0) return;
        GameObject selected = EventSystem.current?.currentSelectedGameObject;
        int at = items.FindIndex(s => s.gameObject == selected);
        int next = at < 0 ? (reverse ? items.Count - 1 : 0) :
            (at + (reverse ? -1 : 1) + items.Count) % items.Count;
        Selectable target = items[next];
        EventSystem.current?.SetSelectedGameObject(target.gameObject);
        if (target is TMP_InputField field)
        {
            field.Select();
            field.ActivateInputField();
        }
        else target.Select();
    }

    private void TrackNewlyFocusedInput()
    {
        TMP_InputField now = null;
        foreach (TMP_InputField field in configuredInputs)
        {
            if (field != null && field.isFocused && field.gameObject.activeInHierarchy)
            { now = field; break; }
        }
        if (now == lastFocusedField) return;
        lastFocusedField = now;
        if (now == null || string.IsNullOrEmpty(now.text)) return;
        // SelectInput may reset caret AFTER onSelect. Run at end of frame.
        _ = MoveCaretToEndAfterFocusAsync(now);
    }

    private static async Task MoveCaretToEndAfterFocusAsync(TMP_InputField input)
    {
        await Task.Yield();
        await Task.Yield();
        if (input == null || !input.isFocused || string.IsNullOrEmpty(input.text)) return;
        int end = input.text.Length;
        input.caretPosition = end;
        input.selectionAnchorPosition = end;
        input.selectionFocusPosition = end;
        input.ForceLabelUpdate();
    }

    private static FirebaseUser GetPrincipal()
    {
        try { return FirebaseAuth.DefaultInstance.CurrentUser; }
        catch { return null; }
    }

    private static void SetFieldFocus(TMP_InputField input, bool focused)
    {
        if (input == null) return;
        Image image = input.GetComponent<Image>();
        if (image != null) image.color = focused
            ? new Color32(224, 243, 253, 255)
            : Color.white;
    }

    private static Sprite RoundedSprite()
    {
        if (roundedSprite != null) return roundedSprite;
        const int dimension = 64;
        const float radius = 15f;
        Texture2D texture = new Texture2D(dimension, dimension, TextureFormat.RGBA32, false);
        texture.name = "ATB_Account_RoundedNineSlice";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[dimension * dimension];
        for (int y = 0; y < dimension; y++)
        for (int x = 0; x < dimension; x++)
        {
            float dx = Mathf.Max(radius - x - 0.5f, 0f);
            dx = Mathf.Max(dx, x + 0.5f - (dimension - radius));
            float dy = Mathf.Max(radius - y - 0.5f, 0f);
            dy = Mathf.Max(dy, y + 0.5f - (dimension - radius));
            float coverage = Mathf.Clamp01(radius + 0.65f - Mathf.Sqrt(dx * dx + dy * dy));
            pixels[y * dimension + x] = new Color32(255, 255, 255, (byte)(coverage * 255));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        roundedSprite = Sprite.Create(texture, new Rect(0, 0, dimension, dimension),
            new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(18f, 18f, 18f, 18f));
        return roundedSprite;
    }

    private static void ConfigureInputSurface(TMP_InputField input)
    {
        Image field = input.GetComponent<Image>();
        if (field == null) return;
        field.sprite = RoundedSprite();
        field.type = Image.Type.Sliced;
        field.color = Color.white;
    }

    private static void ModernizeButton(Button button)
    {
        if (button.name == "PasswordVisibility") return;
        Image image = button.GetComponent<Image>();
        if (image == null) return;
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        ColorBlock cb = button.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 1f, 1f, .94f);
        cb.pressedColor = new Color(.85f, .90f, 1f, 1f);
        cb.selectedColor = Color.white;
        cb.colorMultiplier = 1f;
        button.colors = cb;
        Shadow shadow = button.GetComponent<Shadow>();
        if (shadow == null) shadow = button.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, .09f, .2f, .18f);
        shadow.effectDistance = new Vector2(0f, -3f);
        shadow.useGraphicAlpha = true;
    }

    private void InstallVisibleCaret(TMP_InputField input)
    {
        if (input.textViewport == null || caretMarkers.ContainsKey(input)) return;
        GameObject go = new GameObject("AtlasBoard_VisibleInputCaret", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(input.textViewport, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(.5f, .5f);
        rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(3.5f, 32f);
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = new Color32(19, 87, 152, 255);
        go.SetActive(false);
        caretMarkers[input] = image;
    }

    private void UpdateVisibleCarets()
    {
        foreach (var item in caretMarkers)
        {
            TMP_InputField input = item.Key;
            Image marker = item.Value;
            if (input == null || marker == null) continue;
            bool active = input.isFocused && input.gameObject.activeInHierarchy &&
                input.textViewport != null && input.textComponent != null;
            marker.gameObject.SetActive(active);
            if (!active) continue;
            TMP_Text text = input.textComponent;
            text.ForceMeshUpdate();
            TMP_TextInfo info = text.textInfo;
            int insertion = Mathf.Clamp(input.caretPosition, 0, info.characterCount);
            float x = text.rectTransform.rect.xMin + 2f;
            if (insertion > 0 && info.characterCount > 0)
                x = info.characterInfo[insertion - 1].topRight.x + 1f;
            Vector3 world = text.rectTransform.TransformPoint(new Vector3(x, 0f, 0f));
            RectTransform view = input.textViewport;
            Vector3 local = view.InverseTransformPoint(world);
            float half = view.rect.width * .5f - 5f;
            ((RectTransform)marker.transform).anchoredPosition =
                new Vector2(Mathf.Clamp(local.x, -half, half), 0f);
        }
    }

    private static Sprite EyeSprite(bool visible)
    {
        if (visible && openEyeSprite != null) return openEyeSprite;
        if (!visible && closedEyeSprite != null) return closedEyeSprite;
        const int width = 64, height = 40;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] colors = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float dx = x - 31.5f, dy = y - 19.5f;
            float t = Mathf.Clamp01(1f - Mathf.Abs(dx) / 26f);
            float curve = 11f * Mathf.Sin(t * Mathf.PI * .5f);
            bool rim = Mathf.Abs(dx) <= 26f &&
                (Mathf.Abs(dy - curve) < 1.9f || Mathf.Abs(dy + curve) < 1.9f);
            bool pupil = dx * dx + dy * dy < 40f;
            bool slash = !visible && Mathf.Abs(dy - dx * .45f) < 2.1f &&
                Mathf.Abs(dx) < 27f;
            colors[y * width + x] = new Color32(255, 255, 255,
                (byte)((rim || (visible && pupil) || slash) ? 255 : 0));
        }
        texture.SetPixels32(colors);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
            new Vector2(.5f, .5f), 100f);
        if (visible) openEyeSprite = sprite; else closedEyeSprite = sprite;
        return sprite;
    }

    private static void AddPasswordVisibilityToggle(TMP_InputField input)
    {
        RectTransform host = input.transform as RectTransform;
        if (host == null || host.Find("PasswordVisibility") != null) return;
        if (input.textViewport != null)
        {
            RectTransform viewport = input.textViewport;
            viewport.offsetMax = new Vector2(-72f, viewport.offsetMax.y);
        }
        GameObject go = new GameObject("PasswordVisibility", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(input.transform, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(1f, .5f);
        rect.anchorMax = new Vector2(1f, .5f);
        rect.pivot = new Vector2(1f, .5f);
        rect.sizeDelta = new Vector2(58f, 48f);
        rect.anchoredPosition = new Vector2(-8f, 0f);
        Image bg = go.GetComponent<Image>();
        bg.sprite = RoundedSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color32(231, 239, 247, 255);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        GameObject iconGo = new GameObject("EyeIcon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(go.transform, false);
        RectTransform iconRect = (RectTransform)iconGo.transform;
        iconRect.anchorMin = new Vector2(.5f, .5f);
        iconRect.anchorMax = new Vector2(.5f, .5f);
        iconRect.pivot = new Vector2(.5f, .5f);
        iconRect.sizeDelta = new Vector2(34f, 24f);
        Image icon = iconGo.GetComponent<Image>();
        icon.raycastTarget = false;
        icon.sprite = EyeSprite(false);
        icon.color = new Color32(40, 83, 121, 255);
        bool showing = false;
        button.onClick.AddListener(() =>
        {
            showing = !showing;
            int caret = input.caretPosition;
            input.contentType = showing ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            input.ForceLabelUpdate();
            input.caretPosition = Math.Min(caret, input.text.Length);
            icon.sprite = EyeSprite(showing);
            bg.color = showing ? new Color32(207, 232, 246, 255) : new Color32(231, 239, 247, 255);
            input.Select();
            input.ActivateInputField();
        });
    }

    private async Task EnsureNativeGuestAsync()
    {
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession ||
            AtlasBoardAppCheckTokenSource.IsLocalEmulatorGuestSession) return;
        AtlasBoardPlatformRuntime platform = AtlasBoardPlatformRuntime.Instance;
        // Steam identity restoration has precedence. Never create a random
        // anonymous principal while a verified Steam account is loading.
        if (platform != null && platform.IsSteamProviderActive) return;
        if (Application.internetReachability == NetworkReachability.NotReachable) return;
        guestInitializationInFlight = true;
        try
        {
            AtlasBoardAccountService service = AtlasBoardAccountService.Instance;
            if (service == null) return;
            await service.EnsureInitializedAsync();
            if (!service.IsInitialized) return;
            FirebaseAuth auth = FirebaseAuth.DefaultInstance;
            if (auth == null || auth.CurrentUser != null) return;
            AuthResult guest = await auth.SignInAnonymouslyAsync();
            if (guest?.User == null || !guest.User.IsAnonymous) return;
            await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync(
                AtlasBoardGuestIdentity.NameForAuthenticatedUid(guest.User.UserId), true);
            Debug.Log("ATB_NATIVE_GUEST_BOOTSTRAP anonymous_uid_created=true source=firebase_auth");
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_NATIVE_GUEST_BOOTSTRAP pending=true type=" + e.GetType().Name);
        }
        finally { guestInitializationInFlight = false; }
    }

    private static async Task InvokeAfterServiceReadyAsync(
        AtlasBoardAccountAccessUI ui, Button.ButtonClickedEvent original)
    {
        if (AtlasBoardAppCheckTokenSource.IsLocalEmulatorGuestSession)
        {
            ShowStatus(ui, "Local Emulator Guest testinde gerçek hesap kaydı kapalıdır.");
            return;
        }
        AtlasBoardAccountService service = AtlasBoardAccountService.Instance;
        if (service == null)
        {
            GameObject host = new GameObject("AtlasBoard_Account_Auth_Runtime");
            DontDestroyOnLoad(host);
            service = host.AddComponent<AtlasBoardAccountService>();
        }
        await service.EnsureInitializedAsync();
        if (!service.IsInitialized)
        {
            ShowStatus(ui, "Hesap hizmetine ulaşılamadı. Bağlantını kontrol et. Destek kodu: ATB731");
            return;
        }
        original.Invoke();
    }

    private async Task UpgradeGuestAsync(AtlasBoardAccountAccessUI ui, FirebaseUser anonymous, string email)
    {
        upgrading = true;
        string originalUid = anonymous.UserId;
        try
        {
            string password = GetText(ui, "registerPassword");
            string confirm = GetText(ui, "registerPasswordRepeat");
            if (password != confirm || !IsStrongPassword(password))
            {
                ShowStatus(ui, "Şifreler eşleşmeli; en az 6 karakter, bir harf ve sayı/sembol içermeli.");
                return;
            }
            TMP_InputField captcha = GetField<TMP_InputField>(ui, "captchaAnswer");
            int expected = GetField<int>(ui, "captchaExpected");
            if (!int.TryParse(captcha != null ? captcha.text : "", out int answer) || answer != expected)
            {
                ShowStatus(ui, "Matematik doğrulamasını tamamla. / Complete the math check.");
                return;
            }
            Credential credential = EmailAuthProvider.GetCredential(email, password);
            AuthResult result = await anonymous.LinkWithCredentialAsync(credential);
            if (result == null || result.User == null ||
                !string.Equals(result.User.UserId, originalUid, StringComparison.Ordinal))
                throw new InvalidOperationException("GUEST_UID_UPGRADE_CONTRACT_FAILED");
            // Account linkage preserves original /users/{uid}, Career, inventory
            // and wallet. Do not transfer or award balances on the client.
            // The existing RTDB profile may have a display-name change cooldown;
            // preserve its existing name rather than rejecting the UID upgrade.
            string requestedName = GetText(ui, "registerName").Trim();
            if (requestedName.Length >= 2 && requestedName.Length <= 32)
            {
                try
                {
                    await result.User.UpdateUserProfileAsync(
                        new UserProfile { DisplayName = requestedName });
                }
                catch (Exception e)
                {
                    Debug.LogWarning("ATB_GUEST_UPGRADE auth_name_pending=" + e.GetType().Name);
                }
            }
            bool synced = await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync(
                requestedName, true);
            if (!synced)
            {
                // A recently-created Guest name may be under the canonical
                // 24-hour name-change cooldown. This must not undo a successful
                // Firebase UID upgrade or erase rewards. Preserve old name.
                synced = await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync("", true);
            }
            if (!synced)
                Debug.LogWarning("ATB_GUEST_UPGRADE linked=true profile_sync=pending uid_preserved=true");
            AtlasBoardAccountService account = AtlasBoardAccountService.Instance;
            if (account != null) await account.LoadCurrentAccountAsync();
            // Never leave a successfully linked account on the Register view:
            // clicking CREATE ACCOUNT again could create a second Auth UID.
            FieldInfo loaded = typeof(AtlasBoardAccountAccessUI).GetField(
                "loadedUid", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo canonical = typeof(AtlasBoardAccountAccessUI).GetField(
                "canonicalDisplayName", BindingFlags.Instance | BindingFlags.NonPublic);
            loaded?.SetValue(ui, string.Empty);
            canonical?.SetValue(ui, string.Empty);
            MethodInfo forceReload = typeof(AtlasBoardAccountAccessUI).GetMethod(
                "ForceLoadCanonicalIdentityAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            if (forceReload != null && forceReload.Invoke(ui, null) is Task reloadTask)
                await reloadTask;
            GameObject modal = GetField<GameObject>(ui, "modalBackdrop");
            if (modal != null) modal.SetActive(false);
            Debug.Log("ATB_GUEST_UPGRADE uid_preserved=true balances_cloned=false registration_closed=true");
        }
        catch (Exception ex)
        {
            ShowStatus(ui, "Kayıt tamamlanamadı; mevcut Guest verileri silinmedi. / Registration failed; guest data was preserved.");
            Debug.LogWarning("ATB_GUEST_UPGRADE status=failed category=" + ex.GetType().Name);
        }
        finally { upgrading = false; }
    }

    private static Button FindChildButton(AtlasBoardAccountAccessUI ui, string name)
    {
        foreach (Button button in ui.GetComponentsInChildren<Button>(true))
            if (button != null && button.name == name) return button;
        return null;
    }

    private static string GetText(object owner, string name)
    {
        TMP_InputField input = GetField<TMP_InputField>(owner, name);
        return input != null ? input.text : "";
    }

    private static T GetField<T>(object owner, string name)
    {
        if (owner == null) return default;
        FieldInfo f = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (f == null) return default;
        object value = f.GetValue(owner);
        return value is T typed ? typed : default;
    }

    private static void ShowStatus(AtlasBoardAccountAccessUI ui, string message)
    {
        TMP_Text status = GetField<TMP_Text>(ui, "statusText");
        if (status != null) status.text = message;
    }
}
