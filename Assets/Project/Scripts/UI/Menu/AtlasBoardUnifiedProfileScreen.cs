using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Phase19DE: full-screen, provider-neutral Profile route replacing the
/// Career/Settings ACCOUNT tabs' old modal action. Account management
/// buttons reuse existing UID-bound rename/signout paths; password changes
/// use the provider's existing reset-email method. Does not mutate XP,
/// economy, Career, cosmetics, passwords or multiplayer state.
/// Weekly Mission / Museum / Passport tabs are presentation foundations until
/// their trusted RTDB manifests, receipts and verified gameplay feeds ship.
/// </summary>
[DefaultExecutionOrder(32020)]
public sealed class AtlasBoardUnifiedProfileScreen : MonoBehaviour
{
    private static AtlasBoardUnifiedProfileScreen instance;
    private const string DatabaseUrl =
        "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";

    private static readonly Color Navy = C("#121B45");
    private static readonly Color Deep = C("#0B1536");
    private static readonly Color Violet = C("#4B42A4");
    private static readonly Color Purple = C("#31275F");
    private static readonly Color Bright = C("#7562C9");
    private static readonly Color Gold = C("#F4BD43");
    private static readonly Color Muted = C("#B7C4E0");
    private static readonly Color TextColor = C("#F3F5FF");
    private static readonly Color Teal = C("#24A5CB");
    private static Sprite rounded;
    // Rasterized once at runtime: no unsupported TMP/Unicode icon codepoints.
    private static readonly Sprite[] glyphSprites = new Sprite[5];

    private readonly string[] routes = {"general", "weekly", "museum", "passport"};
    private Canvas canvas;
    private RectTransform screen;
    private RectTransform stage;
    private RectTransform leftNav;
    private TextMeshProUGUI headerText;
    private TextMeshProUGUI subtitleText;
    private TextMeshProUGUI nameLabel;
    private TextMeshProUGUI avatarInitial;
    private TextMeshProUGUI idLabel;
    private TextMeshProUGUI levelLabel;
    private TextMeshProUGUI xpLabel;
    private TextMeshProUGUI steamLabel;
    private TextMeshProUGUI lockedName;
    private TextMeshProUGUI lockedEmail;
    private Button renameAction;
    private Button passwordAction;
    private Button signOutAction;
    private RectTransform accountDialog;
    private RectTransform accountDialogBackdrop;
    private TextMeshProUGUI footerText;
    private TextMeshProUGUI breadcrumbLabel;
    private bool careerReadComplete;
    private Image xpFill;
    private TMP_FontAsset font;
    private GameObject previousParent;
    private Button careerButton;
    private Button settingsButton;
    private bool openedFromSettings;
    private int dataGeneration;
    private string currentUid = "";
    private string loadedPlayerId = "";
    private string loadedAccountEmail = "";
    private string confirmedName = "";
    private string confirmedNameUid = "";
    private TMP_InputField profileRenameInput;
    private Image renameCaretGuide;
    private Coroutine renameFocusCoroutine;
    private bool profileAccountOperationBusy;
    private AtlasBoardProgressionSnapshot currentSnapshot;
    private string selectedRoute = "general";
    private string currentLanguage = "";
    private float scanAt;
    private float refreshAt;
    private string displayName = "";
    private Coroutine restoreCoroutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject obj = new GameObject("AtlasBoard_FullScreen_Profile_Route");
        DontDestroyOnLoad(obj);
        instance = obj.AddComponent<AtlasBoardUnifiedProfileScreen>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        BuildScreen();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (Time.unscaledTime >= scanAt)
        {
            scanAt = Time.unscaledTime + 0.25f;
            BindAccountRoutes();
        }
        if (screen == null || !screen.gameObject.activeSelf) return;
        KeepRenameCaretVisible();

        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid ?? "";
        if (!string.Equals(uid, currentUid, StringComparison.Ordinal))
        {
            ++dataGeneration;
            currentUid = uid;
            loadedPlayerId = "";
            loadedAccountEmail = "";
            confirmedName = confirmedNameUid = "";
            DismissAccountDialog();
            currentSnapshot = null;
            careerReadComplete = false;
            RefreshVisibleIdentity();
            _ = LoadAccountDataAsync(dataGeneration, uid);
        }
        if (Time.unscaledTime >= refreshAt)
        {
            refreshAt = Time.unscaledTime + 2.5f;
            RefreshVisibleIdentity();
            string language = Lang;
            if (language != currentLanguage)
            {
                currentLanguage = language;
                RenderRoute();
            }
        }
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseScreen();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape)) CloseScreen();
#endif
    }

    private string Lang
    {
        get
        {
            var lm = AtlasBoardLocalizationManager.Instance;
            return lm != null && !string.IsNullOrWhiteSpace(lm.CurrentLanguageCode)
                ? lm.CurrentLanguageCode.ToLowerInvariant() : "en";
        }
    }

    private static string L(string en, string tr, string es, string fr,
        string de, string ko, string ru) => AtlasBoardL.R(en, tr, es, fr, de, ko, ru);

    private static Color C(string html)
    {
        ColorUtility.TryParseHtmlString(html, out Color value);
        return value;
    }

    private static Button FindButton(string name)
    {
        foreach (Button b in Resources.FindObjectsOfTypeAll<Button>())
            if (b != null && b.name == name && b.gameObject.scene.IsValid())
                return b;
        return null;
    }

    private static RectTransform FindRoot(string name)
    {
        foreach (RectTransform r in Resources.FindObjectsOfTypeAll<RectTransform>())
            if (r != null && r.name == name && r.gameObject.scene.IsValid())
                return r;
        return null;
    }

    private void BindAccountRoutes()
    {
        // The existing UniversalCareerTheme creates both native ACCOUNT tabs.
        // Replace their click ACTION only, preserving their design and position.
        // The original hidden context button remains untouched for Store sign-in.
        Button currentCareer = FindButton("Tab_CareerAccountAtlasBoard");
        if (currentCareer != null && currentCareer != careerButton)
        {
            careerButton = currentCareer;
            careerButton.onClick = new Button.ButtonClickedEvent();
            careerButton.onClick.AddListener(() => OpenScreen(false));
        }
        Button currentSettings = FindButton("Tab_AccountAtlasBoard");
        if (currentSettings != null && currentSettings != settingsButton)
        {
            settingsButton = currentSettings;
            settingsButton.onClick = new Button.ButtonClickedEvent();
            settingsButton.onClick.AddListener(() => OpenScreen(true));
        }
    }

    private void OpenScreen(bool fromSettings)
    {
        if (screen == null) return;
        if (restoreCoroutine != null)
        {
            StopCoroutine(restoreCoroutine);
            restoreCoroutine = null;
        }
        // This is a separate full-screen destination, not a modal over a tab.
        RectTransform parent = FindRoot(fromSettings ? "SettingsRoot" : "AtlasCareer");
        if (parent == null && fromSettings)
        {
            var settings = AtlasBoardSettingsV2Controller.Instance;
            FieldInfo field = typeof(AtlasBoardSettingsV2Controller).GetField(
                "settingsRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            parent = settings != null && field != null
                ? (field.GetValue(settings) as GameObject)?.GetComponent<RectTransform>() : null;
        }
        // The canonical Career root has an AtlasBoardEscapeBlocker; while the
        // profile is shown, only our own full-screen blocker remains active.
        previousParent = parent != null && parent.gameObject.activeInHierarchy
            ? parent.gameObject : null;
        openedFromSettings = fromSettings;
        currentUid = AtlasBoardRtdbMetaEconomy.CurrentUid ?? "";
        currentSnapshot = null;
        careerReadComplete = false;
        loadedPlayerId = "";
        loadedAccountEmail = "";
        confirmedName = confirmedNameUid = "";
        ++dataGeneration;
        selectedRoute = "general";
        currentLanguage = Lang;
        screen.gameObject.SetActive(true);
        if (previousParent != null) previousParent.SetActive(false);
        RenderRoute();
        RefreshVisibleIdentity();
        _ = LoadAccountDataAsync(dataGeneration, currentUid);
        Debug.Log("ATB_PROFILE_ROUTE opened=true source=" +
                  (fromSettings ? "settings" : "career") + " uidScoped=true");
    }

    private void CloseScreen()
    {
        if (screen == null || !screen.gameObject.activeSelf) return;
        // Escape/X dismisses an active confirmation before leaving Profile.
        if (accountDialog != null)
        {
            DismissAccountDialog();
            return;
        }
        ++dataGeneration;
        screen.gameObject.SetActive(false);
        // Delay restoring Settings/Career by one frame to avoid interpreting
        // the same Escape key a second time in the underlying controller.
        if (restoreCoroutine != null) StopCoroutine(restoreCoroutine);
        restoreCoroutine = StartCoroutine(RestoreOriginNextFrame());
    }

    private IEnumerator RestoreOriginNextFrame()
    {
        yield return null;
        if (previousParent != null) previousParent.SetActive(true);
        previousParent = null;
        restoreCoroutine = null;
    }

    private async Task LoadAccountDataAsync(int generation, string uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) return;
        var career = UnityEngine.Object.FindAnyObjectByType<AtlasBoardProgressionUI>(
            FindObjectsInactive.Include);
        var bridge = UnityEngine.Object.FindAnyObjectByType<AtlasBoardProgressionBridge>(
            FindObjectsInactive.Include);
        if (career != null)
        {
            FieldInfo field = typeof(AtlasBoardProgressionUI).GetField(
                "snapshot", BindingFlags.NonPublic | BindingFlags.Instance);
            var snapshot = field?.GetValue(career) as AtlasBoardProgressionSnapshot;
            if (IsCurrent(generation, uid) && IsSnapshotForUser(snapshot, uid))
            {
                currentSnapshot = snapshot;
                RefreshVisibleIdentity();
            }
        }
        // The same canonical Career Bridge provides the Overview's XP/level.
        // Profile NEVER computes XP, level, achievement or rewards itself.
        if (bridge != null)
        {
            try
            {
                AtlasBoardProgressionSnapshot updated = await bridge.GetProfileAsync();
                if (IsCurrent(generation, uid) && IsSnapshotForUser(updated, uid))
                {
                    currentSnapshot = updated;
                    RefreshVisibleIdentity();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("ATB_PROFILE_CAREER_READ error=" + ex.GetType().Name);
            }
        }
        if (IsCurrent(generation, uid))
        {
            careerReadComplete = true;
            RefreshVisibleIdentity();
        }
        try
        {
            string token = await AtlasBoardRtdbMetaEconomy.GetCurrentCallableTokenAsync();
            if (string.IsNullOrWhiteSpace(token) &&
                !AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
            {
                FirebaseUser current = null;
                try { current = FirebaseAuth.DefaultInstance.CurrentUser; }
                catch { /* Auth can be uninitialized. */ }
                if (current != null && current.UserId == uid)
                    token = await current.TokenAsync(false);
            }
            if (!IsCurrent(generation, uid) || string.IsNullOrWhiteSpace(token)) return;
            // PlayerID and email are two independent read-only RTDB fields.
            // A missing/malformed PlayerID must never suppress email loading.
            string id = await ReadOwnRtdbStringAsync(uid, token, "playerId");
            if (IsCurrent(generation, uid) && !string.IsNullOrWhiteSpace(id) &&
                id.Length <= 80)
            {
                loadedPlayerId = id;
                RefreshVisibleIdentity();
            }
            // Email in /users/{uid}/email is stored on this registered REST
            // identity, but never exposed via native FirebaseAuth.CurrentUser.
            // Read only this authenticated UID's record, never a public profile.
            if (IsCurrent(generation, uid) && CanManageEmailAccount())
            {
                string storedEmail = await ReadOwnRtdbStringAsync(uid, token, "email");
                if (IsCurrent(generation, uid) && LooksLikeEmail(storedEmail))
                {
                    loadedAccountEmail = storedEmail;
                    RefreshVisibleIdentity();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_PROFILE_ID_READ error=" + ex.GetType().Name);
        }
    }

    private async Task<string> ReadOwnRtdbStringAsync(
        string uid, string token, string key)
    {
        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(token))
            return string.Empty;
        string endpoint = DatabaseUrl + "/users/" + UnityWebRequest.EscapeURL(uid) +
            "/" + UnityWebRequest.EscapeURL(key) + ".json?auth=" +
            UnityWebRequest.EscapeURL(token);
        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            request.timeout = 12;
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);
            TaskCompletionSource<bool> completed = new TaskCompletionSource<bool>();
            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            op.completed += _ => completed.TrySetResult(true);
            if (op.isDone) completed.TrySetResult(true);
            await completed.Task;
            if (request.result != UnityWebRequest.Result.Success) return string.Empty;
            string json = request.downloadHandler != null
                ? (request.downloadHandler.text ?? "").Trim() : "";
            if (json.Length < 3 || json[0] != '"') return string.Empty;
            try { return Newtonsoft.Json.JsonConvert.DeserializeObject<string>(json) ?? ""; }
            catch { return string.Empty; }
        }
    }

    private static bool LooksLikeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254) return false;
        int at = email.IndexOf('@');
        return at > 0 && at < email.Length - 3 && email.IndexOf('.', at + 2) > at + 1 &&
            email.IndexOf('\r') < 0 && email.IndexOf('\n') < 0;
    }

    private bool IsCurrent(int gen, string uid) =>
        this != null && screen != null && screen.gameObject.activeSelf &&
        gen == dataGeneration &&
        string.Equals(uid, currentUid, StringComparison.Ordinal) &&
        string.Equals(uid, AtlasBoardRtdbMetaEconomy.CurrentUid ?? "", StringComparison.Ordinal);

    private static bool IsSnapshotForUser(AtlasBoardProgressionSnapshot s, string uid) =>
        s != null && s.Success && !string.IsNullOrWhiteSpace(uid) &&
        string.Equals(s.AccountId, uid, StringComparison.Ordinal);

    private string ResolveName()
    {
        if (!string.IsNullOrWhiteSpace(currentUid) &&
            string.Equals(confirmedNameUid, currentUid, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(confirmedName))
            return confirmedName;
        // Never borrow a previous account's cached global display name after
        // A -> B switching. Use only a verified UID-bound Career/header source.
        if (IsSnapshotForUser(currentSnapshot, currentUid) &&
            !string.IsNullOrWhiteSpace(currentSnapshot.DisplayName))
            return currentSnapshot.DisplayName.Trim();
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
            return AtlasBoardGuestIdentity.DisplayName ?? "";
        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user != null && user.UserId == currentUid &&
                !string.IsNullOrWhiteSpace(user.DisplayName))
                return user.DisplayName.Trim();
        }
        catch { /* Native provider may not be available on REST builds. */ }
        AtlasBoardCanonicalProfileHeader header =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardCanonicalProfileHeader>(
                FindObjectsInactive.Include);
        if (header != null && !string.IsNullOrWhiteSpace(currentUid))
        {
            FieldInfo idField = typeof(AtlasBoardCanonicalProfileHeader).GetField(
                "observedAccountId", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo nameField = typeof(AtlasBoardCanonicalProfileHeader).GetField(
                "appliedDisplayName", BindingFlags.Instance | BindingFlags.NonPublic);
            string observedUid = idField != null ? idField.GetValue(header) as string : null;
            string value = nameField != null ? nameField.GetValue(header) as string : null;
            if (string.Equals(observedUid, currentUid, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        AtlasBoardPlatformRuntime platform = AtlasBoardPlatformRuntime.Instance;
        if (platform != null && platform.IsSteamProviderActive &&
            platform.SteamInitialized && platform.SteamId != 0)
            return platform.SteamPersonaName ?? "";
        return string.Empty;
    }

    private string SteamStatus()
    {
        var platform = AtlasBoardPlatformRuntime.Instance;
        bool session = platform != null && platform.IsSteamProviderActive &&
            platform.SteamInitialized && platform.SteamId != 0;
        if (!session)
            return L("Steam Connection: Not active", "Steam Bağlantısı: Aktif değil",
                "Steam: Sin conexión", "Steam : Non actif", "Steam: Nicht aktiv",
                "Steam 연결: 비활성", "Steam: Не активно");
        AtlasBoardSteamAccountLinkBridge bridge = AtlasBoardSteamAccountLinkBridge.Instance;
        AtlasBoardSteamLinkStatus status = bridge != null ? bridge.CurrentStatus : null;
        bool linked = status != null && status.Success && status.Linked &&
            status.Verified && string.Equals(status.AccountId, currentUid,
                StringComparison.Ordinal);
        return linked
            ? L("Steam Connection: Verified", "Steam Bağlantısı: Doğrulandı",
                "Steam: Verificado", "Steam : Vérifié", "Steam: Verifiziert",
                "Steam 연결: 확인됨", "Steam: Подтверждено")
            : L("Steam session active · Link not verified",
                "Steam oturumu açık · Hesap bağı doğrulanmadı",
                "Steam activo · Enlace sin verificar", "Steam actif · Lien non vérifié",
                "Steam aktiv · Verknüpfung ungeprüft", "Steam 활성 · 연결 미확인",
                "Steam активен · Связь не проверена");
    }

    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    // A selected Steam provider owns the session even if its trusted UID link
    // has not completed; do not expose native account mutation or sign-out.
    private static bool IsSteamSession()
    {
        AtlasBoardPlatformRuntime platform = AtlasBoardPlatformRuntime.Instance;
        return platform != null && platform.IsSteamProviderActive;
    }

    private bool CanManageEmailAccount()
    {
        if (IsSteamSession() || string.IsNullOrWhiteSpace(currentUid)) return false;
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
        {
            return AtlasBoardAccountFormQualityGate
                .TryGetVerifiedRegisteredRestPrincipal(out string restUid) &&
                string.Equals(restUid, currentUid, StringComparison.Ordinal);
        }
        AtlasBoardAccountService account = AtlasBoardAccountService.Instance;
        if (account == null || !account.IsSignedIn ||
            !string.Equals(account.CurrentAccountId, currentUid, StringComparison.Ordinal))
            return false;
        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            return user != null && !user.IsAnonymous &&
                string.Equals(user.UserId, currentUid, StringComparison.Ordinal);
        }
        catch { return false; }
    }

    private void RefreshAccountActions()
    {
        bool allowed = CanManageEmailAccount();
        if (renameAction != null) renameAction.interactable = allowed;
        if (passwordAction != null) passwordAction.interactable = allowed;
        // Steam sessions cannot be disconnected using the Atlas account UI.
        if (signOutAction != null && signOutAction.gameObject.activeSelf != allowed)
            signOutAction.gameObject.SetActive(allowed);
    }

    private AtlasBoardAccountAccessUI FindAccountUI() =>
        UnityEngine.Object.FindAnyObjectByType<AtlasBoardAccountAccessUI>(
            FindObjectsInactive.Include);

    private void OpenRenameAccountManager()
    {
        if (!CanManageEmailAccount() || profileAccountOperationBusy) return;
        DismissAccountDialog();
        BuildAccountDialogShell(1010, 470);
        Text(accountDialog, "RenameTitle", L("CHANGE PLAYER NAME", "OYUNCU ADINI DEĞİŞTİR",
            "CAMBIAR NOMBRE", "CHANGER LE NOM", "SPIELERNAMEN ÄNDERN", "이름 변경", "ИЗМЕНИТЬ ИМЯ"),
            new Vector2(0, 162), new Vector2(880, 60), 33, Gold, bold: true);
        Text(accountDialog, "RenameHint", L(
            "You can change your name once every 24 hours (2–32 characters).",
            "Oyuncu adını 24 saatte bir değiştirebilirsin (2–32 karakter).",
            "Una vez cada 24 horas.", "Une fois par 24 heures.",
            "Einmal in 24 Stunden.", "24시간마다 변경 가능합니다.", "Раз в 24 часа."),
            new Vector2(0, 89), new Vector2(880, 47), 19, Muted);
        RectTransform field = Rect(accountDialog, "ProfileRenameField",
            new Vector2(0, -6), new Vector2(790, 80), C("#26356A"));
        Image background = field.GetComponent<Image>();
        background.raycastTarget = true;
        profileRenameInput = field.gameObject.AddComponent<TMP_InputField>();
        RectTransform viewport = Rect(field, "Viewport", Vector2.zero,
            new Vector2(734, 64), new Color(0, 0, 0, 0), false);
        viewport.GetComponent<Image>().raycastTarget = false;
        viewport.gameObject.AddComponent<RectMask2D>();
        TextMeshProUGUI edit = Text(viewport, "Text", "", Vector2.zero,
            new Vector2(724, 56), 28, TextColor, TextAlignmentOptions.MidlineLeft);
        edit.textWrappingMode = TextWrappingModes.NoWrap;
        edit.enableAutoSizing = false;
        TextMeshProUGUI placeholder = Text(viewport, "Placeholder", "", Vector2.zero,
            new Vector2(724, 56), 23, Muted, TextAlignmentOptions.MidlineLeft);
        TMP_InputField input = profileRenameInput;
        input.textViewport = viewport;
        input.textComponent = edit;
        input.placeholder = placeholder;
        input.targetGraphic = background;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.characterLimit = 32;
        input.customCaretColor = true;
        input.caretColor = Gold;
        input.caretWidth = 4;
        input.caretBlinkRate = 0.75f;
        input.selectionColor = new Color(0.41f, 0.73f, 0.98f, 0.46f);
        input.text = ResolveName();
        // A font-independent caret indicator follows the ACTUAL TMP caret
        // index, including mouse clicks between characters. The native caret
        // can be obscured by the masked viewport on some editor builds.
        RectTransform guide = Rect(viewport, "RenameCaretGuide", Vector2.zero,
            new Vector2(4f, 39f), Gold, false);
        renameCaretGuide = guide.GetComponent<Image>();
        renameCaretGuide.raycastTarget = false;
        renameCaretGuide.gameObject.SetActive(false);
        string uidAtOpen = currentUid;
        Btn(accountDialog, "CancelRename", L("CANCEL", "VAZGEÇ", "CANCELAR", "ANNULER",
            "ABBRECHEN", "취소", "ОТМЕНА"), new Vector2(-174, -156),
            new Vector2(275, 63), Bright, DismissAccountDialog, 21);
        Btn(accountDialog, "ConfirmRename", L("SAVE NAME", "ADI KAYDET", "GUARDAR",
            "ENREGISTRER", "SPEICHERN", "저장", "СОХРАНИТЬ"),
            new Vector2(174, -156), new Vector2(300, 63), Teal,
            () => _ = ApplyRenameFromProfileAsync(uidAtOpen, input), 21);
        input.Select();
        input.ActivateInputField();
        // OnClick's EventSystem processing may overwrite selection after this
        // callback; reselect once when the newly constructed dialog is ready.
        if (renameFocusCoroutine != null) StopCoroutine(renameFocusCoroutine);
        renameFocusCoroutine = StartCoroutine(FocusRenameInputNextFrame(input));
    }

    private IEnumerator FocusRenameInputNextFrame(TMP_InputField input)
    {
        yield return null;
        renameFocusCoroutine = null;
        if (input == null || profileRenameInput != input ||
            accountDialog == null || !accountDialog.gameObject.activeInHierarchy)
            yield break;
        input.Select();
        input.ActivateInputField();
        int end = input.text != null ? input.text.Length : 0;
        input.caretPosition = end;
        input.selectionAnchorPosition = end;
        input.selectionFocusPosition = end;
        input.ForceLabelUpdate();
        KeepRenameCaretVisible();
    }

    private void KeepRenameCaretVisible()
    {
        TMP_InputField input = profileRenameInput;
        if (input == null || renameCaretGuide == null) return;
        bool focused = input.isFocused && input.gameObject.activeInHierarchy;
        if (renameCaretGuide.gameObject.activeSelf != focused)
            renameCaretGuide.gameObject.SetActive(focused);
        if (!focused || input.textComponent == null) return;
        TMP_Text edit = input.textComponent;
        edit.ForceMeshUpdate();
        TMP_TextInfo info = edit.textInfo;
        int count = info != null ? info.characterCount : 0;
        int index = Mathf.Clamp(input.caretPosition, 0, count);
        float x = edit.rectTransform.rect.xMin + 4f;
        if (count > 0)
            x = index == count ? info.characterInfo[count - 1].xAdvance :
                info.characterInfo[index].origin;
        RectTransform viewport = input.textViewport;
        float half = viewport != null ? viewport.rect.width * 0.5f - 10f : 355f;
        RectTransform caret = renameCaretGuide.rectTransform;
        caret.anchoredPosition = new Vector2(Mathf.Clamp(x, -half, half), 0f);
        if (caret.GetSiblingIndex() != caret.parent.childCount - 1)
            caret.SetAsLastSibling();
    }

    private async Task ApplyRenameFromProfileAsync(string uidAtOpen, TMP_InputField input)
    {
        if (profileAccountOperationBusy || input == null || !CanManageEmailAccount() ||
            !string.Equals(uidAtOpen, currentUid, StringComparison.Ordinal)) return;
        string newName = (input.text ?? "").Trim();
        if (newName.Length < 2 || newName.Length > 32 ||
            newName.IndexOf('\n') >= 0 || newName.IndexOf('\r') >= 0)
        {
            ShowAccountNotice(L("Name must be 2–32 characters.", "Oyuncu adı 2–32 karakter olmalı.",
                "Nombre de 2–32 caracteres.", "Nom de 2 à 32 caractères.",
                "Name: 2–32 Zeichen.", "이름은 2~32자여야 합니다.", "Имя: 2–32 символа."));
            return;
        }
        if (string.Equals(newName, ResolveName(), StringComparison.Ordinal))
        {
            ShowAccountNotice(L("This is already your name.", "Oyuncu adın zaten bu.",
                "Este es tu nombre.", "C'est déjà votre nom.", "Name unverändert.",
                "이미 같은 이름입니다.", "Имя не изменилось."));
            return;
        }
        profileAccountOperationBusy = true;
        try
        {
            bool changed = false;
            if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
            {
                // Call the SAME 24h RTDB rule-backed rename operation, but
                // never instantiate OpenRegisteredRestAccountPanel.
                var gate = UnityEngine.Object.FindAnyObjectByType<AtlasBoardAccountFormQualityGate>(
                    FindObjectsInactive.Include);
                var ui = FindAccountUI();
                MethodInfo rename = typeof(AtlasBoardAccountFormQualityGate).GetMethod(
                    "RenameRegisteredRestAccountAsync", PrivateInstance);
                if (gate != null && ui != null && rename != null)
                {
                    Task task = rename.Invoke(gate, new object[] { uidAtOpen, newName, ui }) as Task;
                    if (task != null) await task;
                    FieldInfo liveName = typeof(AtlasBoardAccountFormQualityGate).GetField(
                        "registeredLiveDisplayName", PrivateInstance);
                    string verified = liveName != null ? liveName.GetValue(gate) as string : null;
                    changed = string.Equals(verified, newName, StringComparison.Ordinal) &&
                        CanManageEmailAccount() && currentUid == uidAtOpen;
                    RemoveUnusedLegacyNotices();
                }
            }
            else
            {
                var ui = FindAccountUI();
                MethodInfo rename = typeof(AtlasBoardAccountAccessUI).GetMethod(
                    "RenameAsync", PrivateInstance);
                FieldInfo field = typeof(AtlasBoardAccountAccessUI).GetField(
                    "renameInput", PrivateInstance);
                FieldInfo nameField = typeof(AtlasBoardAccountAccessUI).GetField(
                    "canonicalDisplayName", PrivateInstance);
                if (ui != null && field != null && rename != null)
                {
                    object previous = field.GetValue(ui);
                    try
                    {
                        field.SetValue(ui, input);
                        Task task = rename.Invoke(ui, null) as Task;
                        if (task != null) await task;
                    }
                    finally { if (ui != null) field.SetValue(ui, previous); }
                    changed = currentUid == uidAtOpen && CanManageEmailAccount() &&
                        nameField != null && string.Equals(
                            nameField.GetValue(ui) as string, newName, StringComparison.Ordinal);
                }
            }
            if (screen == null || !screen.gameObject.activeSelf ||
                !string.Equals(uidAtOpen, currentUid, StringComparison.Ordinal)) return;
            if (changed)
            {
                confirmedName = newName;
                confirmedNameUid = uidAtOpen;
                DismissAccountDialog();
                RefreshVisibleIdentity();
                ShowAccountNotice(L("Your player name has been updated.",
                    "Oyuncu adın güncellendi.", "Nombre actualizado.", "Nom modifié.",
                    "Name aktualisiert.", "이름이 변경되었습니다.", "Имя обновлено."));
            }
            else
            {
                ShowAccountNotice(L("Name could not be changed. Check the 24-hour cooldown.",
                    "Ad değiştirilemedi. Son değişiklikten itibaren 24 saat geçmiş olmalı.",
                    "No se pudo cambiar el nombre.", "Impossible de changer le nom.",
                    "Änderung nicht möglich (24-Stunden-Frist).", "이름 변경 불가 (24시간 제한).",
                    "Не удалось изменить имя (24 часа)."));
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_PROFILE_RENAME failed=" + ex.GetType().Name);
            if (screen != null && screen.gameObject.activeSelf && currentUid == uidAtOpen)
                ShowAccountNotice(L("Name update unavailable. Try again later.",
                    "Ad güncellenemedi. Daha sonra tekrar dene.", "Error al actualizar.",
                    "Modification indisponible.", "Änderung fehlgeschlagen.",
                    "이름을 변경할 수 없습니다.", "Ошибка изменения имени."));
        }
        finally { profileAccountOperationBusy = false; }
    }

    // The existing REST account service uses a secondary canvas for result
    // notices. After executing its trusted backend method, render status only
    // on this Profile screen; do not expose that historical secondary panel.
    private static void RemoveUnusedLegacyNotices()
    {
        foreach (RectTransform r in Resources.FindObjectsOfTypeAll<RectTransform>())
            if (r != null && r.gameObject.scene.IsValid() &&
                r.name == "ATB_Account_Success") Destroy(r.gameObject);
    }

    private void BuildAccountDialogShell(int width, int height)
    {
        accountDialogBackdrop = Rect(screen, "ProfileAccountBackdrop", Vector2.zero,
            new Vector2(1920, 1080), new Color(0, 0, 0, .76f), false);
        accountDialogBackdrop.anchorMin = Vector2.zero;
        accountDialogBackdrop.anchorMax = Vector2.one;
        accountDialogBackdrop.offsetMin = accountDialogBackdrop.offsetMax = Vector2.zero;
        accountDialogBackdrop.GetComponent<Image>().raycastTarget = true;
        accountDialog = Rect(accountDialogBackdrop, "ProfileAccountDialog", Vector2.zero,
            new Vector2(width, height), C("#34306F"));
        Rect(accountDialog, "TopAccent", new Vector2(0, height / 2f - 9),
            new Vector2(width - 36, 7), Gold, false);
        accountDialogBackdrop.SetAsLastSibling();
    }

    private void OpenPasswordResetConfirm()
    {
        if (!CanManageEmailAccount()) return;
        string email = GetVerifiedCurrentEmail();
        if (string.IsNullOrWhiteSpace(email))
        {
            ShowAccountNotice(L("This account has no verified recovery email.",
                "Bu hesap için doğrulanmış kurtarma e-postası bulunamadı.",
                "No hay correo verificado.", "Aucun e-mail vérifié.",
                "Keine verifizierte E-Mail.", "확인된 이메일이 없습니다.",
                "Подтверждённая почта отсутствует."));
            return;
        }
        string verifiedUidAtPrompt = currentUid;
        ShowAccountConfirmation(
            L("Send a password reset link to the email on this account? Check spam or junk too.",
              "Bu hesabın e-posta adresine şifre sıfırlama bağlantısı gönderilsin mi? Spam/Gereksiz klasörünü de kontrol et.",
              "¿Enviar un enlace para restablecer la contraseña?", 
              "Envoyer un lien de réinitialisation ?",
              "Passwort-Zurücksetzungslink senden?",
              "비밀번호 재설정 링크를 전송할까요?",
              "Отправить ссылку для сброса пароля?"),
            L("SEND LINK", "BAĞLANTI GÖNDER", "ENVIAR", "ENVOYER",
              "SENDEN", "전송", "ОТПРАВИТЬ"),
            () => _ = SendPasswordResetForCurrentUidAsync(verifiedUidAtPrompt, email));
    }

    private string GetVerifiedCurrentEmail()
    {
        if (!CanManageEmailAccount()) return string.Empty;
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
        {
            var gate = UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardAccountFormQualityGate>(FindObjectsInactive.Include);
            var field = typeof(AtlasBoardAccountFormQualityGate).GetField(
                "registeredLiveEmail", PrivateInstance);
            string email = gate != null && field != null
                ? (field.GetValue(gate) as string ?? string.Empty).Trim()
                : string.Empty;
            return LooksLikeEmail(email) ? email :
                (LooksLikeEmail(loadedAccountEmail) ? loadedAccountEmail : string.Empty);
        }
        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            string email = user != null && user.UserId == currentUid ? user.Email ?? "" : "";
            return LooksLikeEmail(email) ? email :
                (LooksLikeEmail(loadedAccountEmail) ? loadedAccountEmail : string.Empty);
        }
        catch { return ""; }
    }

    private async Task SendPasswordResetForCurrentUidAsync(string uid, string email)
    {
        if (!CanManageEmailAccount() || !string.Equals(uid, currentUid,
                StringComparison.Ordinal) || string.IsNullOrWhiteSpace(email)) return;
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
        {
            // Reuse the REST-only guest build's established reset sender and
            // generic (non-account-enumerating) confirmation/error handling.
            var gate = UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardAccountFormQualityGate>(FindObjectsInactive.Include);
            var ui = FindAccountUI();
            var method = typeof(AtlasBoardAccountFormQualityGate).GetMethod(
                "SendPasswordResetRestAsync", PrivateInstance);
            if (gate == null || ui == null || method == null) return;
            ++dataGeneration;
            screen.gameObject.SetActive(false);
            if (restoreCoroutine != null) StopCoroutine(restoreCoroutine);
            restoreCoroutine = StartCoroutine(RestoreOriginNextFrame());
            try
            {
                Task sending = method.Invoke(gate, new object[] { ui, email, null }) as Task;
                if (sending != null) await sending;
            }
            catch (Exception error)
            {
                Debug.LogWarning("ATB_PROFILE_PASSWORD_RESET rest_error=" +
                    error.GetType().Name);
            }
            return;
        }
        int generation = dataGeneration;
        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null || user.IsAnonymous || user.UserId != uid ||
                !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
                return;
            await FirebaseAuth.DefaultInstance.SendPasswordResetEmailAsync(email);
            if (IsCurrent(generation, uid))
                ShowAccountNotice(L("If an account is associated with this email, a reset link was sent. Check your inbox and spam folder.",
                    "Bu e-postaya bağlı bir hesap varsa sıfırlama bağlantısı gönderildi. Gelen kutunu ve Spam/Gereksiz klasörünü kontrol et.",
                    "Revisa el correo y spam.", "Vérifiez votre courrier et les indésirables.",
                    "Posteingang und Spam prüfen.", "메일함과 스팸함을 확인하세요.",
                    "Проверьте почту и папку спама."));
        }
        catch (Exception error)
        {
            Debug.LogWarning("ATB_PROFILE_PASSWORD_RESET native_error=" +
                error.GetType().Name);
            if (IsCurrent(generation, uid))
                ShowAccountNotice(L("Password reset could not be sent. Please try again later.",
                    "Şifre sıfırlama isteği gönderilemedi. Daha sonra tekrar dene.",
                    "No se pudo enviar.", "Envoi impossible.", "Senden fehlgeschlagen.",
                    "전송에 실패했습니다.", "Не удалось отправить."));
        }
    }

    private void OpenSignOutConfirm()
    {
        if (!CanManageEmailAccount() || IsSteamSession() || profileAccountOperationBusy)
            return;
        string verifiedUidAtPrompt = currentUid;
        ShowAccountConfirmation(
            L("Sign out? Your Gold, XP and purchases remain on this account. A new guest account will be used on the standalone client.",
              "Hesaptan çıkılsın mı? Gold, XP ve satın alımların bu hesapta kalır. Bağımsız istemcide yeni bir misafir hesabı açılır.",
              "¿Cerrar sesión? Tus progresos se conservarán.",
              "Se déconnecter ? Votre progression est conservée.",
              "Abmelden? Dein Fortschritt bleibt gespeichert.",
              "로그아웃하시겠습니까? 진행도는 보존됩니다.",
              "Выйти? Прогресс сохранится."),
            L("SIGN OUT", "ÇIKIŞ YAP", "SALIR", "SE DÉCONNECTER",
                "ABMELDEN", "로그아웃", "ВЫЙТИ"),
            () => _ = ConfirmSignOutForCurrentUidAsync(verifiedUidAtPrompt));
    }

    private async Task ConfirmSignOutForCurrentUidAsync(string uid)
    {
        if (profileAccountOperationBusy || !CanManageEmailAccount() || IsSteamSession() ||
            !string.Equals(uid, currentUid, StringComparison.Ordinal)) return;
        var ui = FindAccountUI();
        if (ui == null) return;
        profileAccountOperationBusy = true;
        try
        {
            if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
            {
                var gate = UnityEngine.Object.FindAnyObjectByType<AtlasBoardAccountFormQualityGate>(
                    FindObjectsInactive.Include);
                // Important: bypass BOTH OpenRegisteredRestAccountPanel and
                // ConfirmRegisteredRestSignOut. They build the old ATLAS HESABI
                // popup and were the source of duplicate close/confirm actions.
                MethodInfo direct = typeof(AtlasBoardAccountFormQualityGate).GetMethod(
                    "SignOutRestToNewGuestAsync", PrivateInstance);
                if (gate == null || direct == null)
                {
                    ShowAccountNotice(L("Sign-out is unavailable.", "Çıkış işlemi kullanılamıyor.",
                        "No disponible.", "Indisponible.", "Abmeldung nicht verfügbar.",
                        "로그아웃 불가.", "Выход недоступен."));
                    return;
                }
                // The existing handler destroys its dialog after a successful
                // session switch. A private non-interactive sentinel is used,
                // not a cloned legacy confirmation or a duplicate popup.
                GameObject sentinel = new GameObject("ProfileSignOutState");
                Task task = direct.Invoke(gate, new object[] { ui, sentinel }) as Task;
                if (task != null) await task;
                if (sentinel != null) Destroy(sentinel);
                bool switched = !string.Equals(uid, AtlasBoardRtdbMetaEconomy.CurrentUid,
                    StringComparison.Ordinal) &&
                    !AtlasBoardAccountFormQualityGate.TryGetVerifiedRegisteredRestPrincipal(out _);
                RemoveUnusedLegacyNotices();
                if (switched)
                {
                    ExitToMainMenuAfterSignOut();
                    return;
                }
                if (screen != null && screen.gameObject.activeSelf)
                    ShowAccountNotice(L("Could not sign out. Your account remains active.",
                        "Çıkış yapılamadı. Mevcut hesabın açık kalıyor.", "Error de salida.",
                        "Échec de la déconnexion.", "Abmeldung fehlgeschlagen.",
                        "로그아웃 실패.", "Не удалось выйти."));
                return;
            }
            MethodInfo signOut = typeof(AtlasBoardAccountAccessUI).GetMethod(
                "SignOut", PrivateInstance);
            if (signOut == null) return;
            signOut.Invoke(ui, null);
            if (!CanManageEmailAccount()) ExitToMainMenuAfterSignOut();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_PROFILE_SIGNOUT failed=" + ex.GetType().Name);
            if (screen != null && screen.gameObject.activeSelf)
                ShowAccountNotice(L("Could not sign out.", "Çıkış yapılamadı.",
                    "Error de salida.", "Déconnexion impossible.", "Abmeldung fehlgeschlagen.",
                    "로그아웃 실패.", "Не удалось выйти."));
        }
        finally { profileAccountOperationBusy = false; }
    }

    private void ExitToMainMenuAfterSignOut()
    {
        DismissAccountDialog();
        ++dataGeneration;
        loadedAccountEmail = loadedPlayerId = confirmedName = confirmedNameUid = "";
        currentSnapshot = null;
        screen.gameObject.SetActive(false);
        if (restoreCoroutine != null) { StopCoroutine(restoreCoroutine); restoreCoroutine = null; }
        if (previousParent != null) previousParent.SetActive(false);
        previousParent = null;
        AtlasBoardMainMenuController menu = UnityEngine.Object.FindAnyObjectByType<
            AtlasBoardMainMenuController>(FindObjectsInactive.Include);
        if (menu != null) menu.ShowMainMenu();
    }

    private void ShowAccountNotice(string message)
    {
        if (screen == null || !screen.gameObject.activeSelf) return;
        DismissAccountDialog();
        accountDialogBackdrop = Rect(screen, "ProfileAccountBackdrop", Vector2.zero,
            new Vector2(1920, 1080), new Color(0f, 0f, 0f, .72f), false);
        accountDialogBackdrop.anchorMin = Vector2.zero;
        accountDialogBackdrop.anchorMax = Vector2.one;
        accountDialogBackdrop.offsetMin = accountDialogBackdrop.offsetMax = Vector2.zero;
        accountDialogBackdrop.GetComponent<Image>().raycastTarget = true;
        accountDialog = Rect(accountDialogBackdrop, "ProfileAccountDialog", Vector2.zero,
            new Vector2(980, 370), C("#34306F"));
        accountDialogBackdrop.SetAsLastSibling();
        Text(accountDialog, "Notice", message, new Vector2(0, 42),
            new Vector2(900, 160), 25, TextColor);
        Btn(accountDialog, "Dismiss", L("OK", "TAMAM", "VALE", "OK",
            "OK", "확인", "ОК"), new Vector2(0, -115),
            new Vector2(260, 58), Bright, DismissAccountDialog, 21);
    }

    private void ShowAccountConfirmation(string message, string confirmLabel,
        Action onConfirm)
    {
        if (screen == null || !screen.gameObject.activeSelf) return;
        DismissAccountDialog();
        accountDialogBackdrop = Rect(screen, "ProfileAccountBackdrop", Vector2.zero,
            new Vector2(1920, 1080), new Color(0f, 0f, 0f, .72f), false);
        accountDialogBackdrop.anchorMin = Vector2.zero;
        accountDialogBackdrop.anchorMax = Vector2.one;
        accountDialogBackdrop.offsetMin = accountDialogBackdrop.offsetMax = Vector2.zero;
        accountDialogBackdrop.GetComponent<Image>().raycastTarget = true;
        accountDialog = Rect(accountDialogBackdrop, "ProfileAccountDialog", Vector2.zero,
            new Vector2(980, 385), C("#34306F"));
        accountDialogBackdrop.SetAsLastSibling();
        Text(accountDialog, "Question", message, new Vector2(0, 48),
            new Vector2(890, 155), 24, TextColor);
        Btn(accountDialog, "Cancel", L("CANCEL", "VAZGEÇ", "CANCELAR",
            "ANNULER", "ABBRECHEN", "취소", "ОТМЕНА"),
            new Vector2(-170, -123), new Vector2(280, 58), Bright,
            DismissAccountDialog, 20);
        Btn(accountDialog, "Confirm", confirmLabel,
            new Vector2(170, -123), new Vector2(300, 58), Teal,
            () => { DismissAccountDialog(); onConfirm?.Invoke(); }, 19);
    }

    private void DismissAccountDialog()
    {
        if (renameFocusCoroutine != null)
        {
            StopCoroutine(renameFocusCoroutine);
            renameFocusCoroutine = null;
        }
        if (accountDialogBackdrop != null) Destroy(accountDialogBackdrop.gameObject);
        profileRenameInput = null;
        renameCaretGuide = null;
        accountDialogBackdrop = null;
        accountDialog = null;
    }

    private static string MaskEmail(string email)
    {
        int at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1) return "••••••";
        string local = email.Substring(0, at);
        string prefix = local.Length > 1 ? local.Substring(0, 2) : local;
        return prefix + "••••@" + email.Substring(at + 1);
    }

    private void RefreshVisibleIdentity()
    {
        if (screen == null || !screen.gameObject.activeSelf) return;
        displayName = ResolveName();
        if (nameLabel != null) nameLabel.text = string.IsNullOrEmpty(displayName) ? "—" : displayName;
        if (lockedName != null) lockedName.text =
            string.IsNullOrEmpty(displayName) ? "—" : displayName;
        if (lockedEmail != null)
        {
            // Native Auth is absent in the REST-only registered Guest build.
            // The same verified REST principal plus /users/{uid}/email owns it.
            // Never fall back to a different UID or publicly cached alias.
            string ownEmail = GetVerifiedCurrentEmail();
            lockedEmail.text = LooksLikeEmail(ownEmail) ? MaskEmail(ownEmail) : "—";
        }
        if (avatarInitial != null)
        {
            string first = string.IsNullOrEmpty(displayName) ? "" :
                StringInfo.GetNextTextElement(displayName.Trim());
            avatarInitial.text = string.IsNullOrEmpty(first) ? "?" : first.ToUpperInvariant();
        }
        if (idLabel != null)
            idLabel.text = L("Player ID: ", "Oyuncu ID: ", "ID jugador: ",
                "ID joueur : ", "Spieler-ID: ", "플레이어 ID: ", "ID игрока: ") +
                (string.IsNullOrWhiteSpace(loadedPlayerId) ? "—" : loadedPlayerId);
        if (steamLabel != null) steamLabel.text = SteamStatus();
        RefreshAccountActions();
        if (currentSnapshot != null && IsSnapshotForUser(currentSnapshot, currentUid))
        {
            int level = Math.Max(1, currentSnapshot.Level);
            long progress = Math.Max(0L,
                (long)currentSnapshot.TotalXp - currentSnapshot.LevelStartXp);
            long requirement = Math.Max(1L,
                (long)currentSnapshot.NextLevelXp - currentSnapshot.LevelStartXp);
            if (levelLabel != null) levelLabel.text = "Lv. " + level;
            if (xpLabel != null) xpLabel.text = progress.ToString("N0") + " / " +
                requirement.ToString("N0") + " XP";
            if (xpFill != null)
            {
                RectTransform rect = xpFill.rectTransform;
                rect.anchorMax = new Vector2(Mathf.Clamp01((float)progress / requirement), 1f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
        }
        else
        {
            if (levelLabel != null) levelLabel.text = "Lv. —";
            if (xpLabel != null) xpLabel.text = careerReadComplete
                ? L("Career unavailable", "Kariyer verisi alınamadı",
                    "Carrera no disponible", "Carrière indisponible",
                    "Karriere nicht verfügbar", "커리어 사용 불가", "Карьера недоступна")
                : L("Career loading", "Kariyer yükleniyor",
                    "Cargando carrera", "Carrière en chargement", "Karriere wird geladen",
                    "커리어 불러오는 중", "Загрузка карьеры");
            if (xpFill != null) xpFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        }
    }

    private static Sprite Rounded()
    {
        if (rounded != null) return rounded;
        const int side = 64;
        var texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            float dx = Mathf.Max(Mathf.Abs(x + .5f - 32f) - 19f, 0f);
            float dy = Mathf.Max(Mathf.Abs(y + .5f - 32f) - 19f, 0f);
            float alpha = Mathf.Clamp01(13.5f - Mathf.Sqrt(dx * dx + dy * dy));
            texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
        }
        texture.Apply(false, true);
        rounded = Sprite.Create(texture, new Rect(0, 0, side, side),
            new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(14, 14, 14, 14));
        return rounded;
    }


    private static bool InBox(int x, int y, int x0, int y0, int x1, int y1) =>
        x >= x0 && x <= x1 && y >= y0 && y <= y1;

    private static bool InCircle(int x, int y, int cx, int cy, int radius)
    {
        int dx = x - cx;
        int dy = y - cy;
        return dx * dx + dy * dy <= radius * radius;
    }

    // 0: player, 1: calendar, 2: museum, 3: passport, 4: padlock.
    // All outlines are actual Unity images, never character symbols.
    private static Sprite GlyphSprite(int kind)
    {
        if (kind < 0 || kind >= glyphSprites.Length) return null;
        if (glyphSprites[kind] != null) return glyphSprites[kind];
        const int side = 48;
        Texture2D texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < side; ++y)
        for (int x = 0; x < side; ++x)
        {
            bool mark = false;
            switch (kind)
            {
                case 0:
                    mark = InCircle(x, y, 24, 32, 8) ||
                        (InBox(x, y, 10, 9, 38, 21) &&
                         InCircle(x, y, 24, 5, 22));
                    break;
                case 1:
                    mark = (InBox(x, y, 9, 10, 39, 38) &&
                        (!InBox(x, y, 13, 14, 35, 34) || InBox(x, y, 13, 29, 35, 32))) ||
                        InBox(x, y, 17, 34, 21, 44) || InBox(x, y, 28, 34, 32, 44) ||
                        InBox(x, y, 17, 18, 21, 22) || InBox(x, y, 27, 18, 31, 22);
                    break;
                case 2:
                    mark = (y >= 38 && y <= 44 && Math.Abs(x - 24) <= (45 - y) * 3) ||
                        InBox(x, y, 7, 34, 41, 37) ||
                        InBox(x, y, 11, 11, 37, 14) ||
                        InBox(x, y, 14, 16, 18, 33) ||
                        InBox(x, y, 23, 16, 27, 33) ||
                        InBox(x, y, 32, 16, 36, 33);
                    break;
                case 3:
                    mark = (InBox(x, y, 12, 7, 36, 42) &&
                        (!InBox(x, y, 16, 11, 32, 38) || InBox(x, y, 18, 11, 20, 38))) ||
                        (InCircle(x, y, 26, 25, 7) && !InCircle(x, y, 26, 25, 5)) ||
                        InBox(x, y, 20, 24, 32, 26) || InBox(x, y, 25, 18, 27, 32);
                    break;
                case 4:
                    mark = (y >= 23 && y <= 42 &&
                        InCircle(x, y, 24, 30, 10) &&
                        !InCircle(x, y, 24, 30, 6)) ||
                        InBox(x, y, 13, 9, 35, 28);
                    break;
            }
            texture.SetPixel(x, y, mark ? Color.white : Color.clear);
        }
        texture.Apply(false, true);
        glyphSprites[kind] = Sprite.Create(texture, new UnityEngine.Rect(0, 0, side, side),
            new Vector2(.5f, .5f), 100f);
        return glyphSprites[kind];
    }

    private static Image AddGlyph(Transform parent, string name, int kind,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image icon = obj.GetComponent<Image>();
        icon.sprite = GlyphSprite(kind);
        icon.color = color;
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        return icon;
    }

    private static RectTransform Rect(Transform parent, string name,
        Vector2 pos, Vector2 size, Color color, bool round = true)
    {
        GameObject o = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform r = o.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
        r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        Image image = o.GetComponent<Image>();
        image.color = color;
        if (round)
        {
            image.sprite = Rounded();
            image.type = Image.Type.Sliced;
        }
        image.raycastTarget = false;
        return r;
    }

    private TextMeshProUGUI Text(Transform parent, string name, string value,
        Vector2 pos, Vector2 size, int pts, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center,
        bool bold = false)
    {
        GameObject o = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform r = o.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        TextMeshProUGUI t = o.GetComponent<TextMeshProUGUI>();
        t.font = font != null ? font : TMP_Settings.defaultFontAsset;
        t.fontSize = pts;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.enableAutoSizing = true;
        t.fontSizeMin = Mathf.Max(11, pts - 7);
        t.fontSizeMax = pts;
        t.color = color;
        t.alignment = alignment;
        t.raycastTarget = false;
        t.text = value;
        return t;
    }

    private Button Btn(Transform parent, string name, string label,
        Vector2 pos, Vector2 size, Color bg, Action click,
        int pts = 20)
    {
        RectTransform r = Rect(parent, name, pos, size, bg);
        Image image = r.GetComponent<Image>();
        image.raycastTarget = true;
        Button b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = image;
        b.onClick.AddListener(() => click());
        ColorBlock colors = b.colors;
        colors.highlightedColor = new Color(.82f, .88f, 1f, 1f);
        colors.pressedColor = new Color(.68f, .78f, .93f, 1f);
        b.colors = colors;
        Text(r, "Label", label, Vector2.zero, size - new Vector2(12, 4), pts,
            TextColor, TextAlignmentOptions.Center, true);
        return b;
    }

    private void BuildScreen()
    {
        TMP_Text found = null;
        foreach (TMP_Text t in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (t != null && t.font != null && t.gameObject.scene.IsValid())
            { found = t; break; }
        }
        font = found != null ? found.font : TMP_Settings.defaultFontAsset;
        GameObject obj = new GameObject("Canvas_AtlasProfileFullScreen",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        obj.transform.SetParent(transform, false);
        canvas = obj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1800;
        CanvasScaler scaler = obj.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        screen = Rect(obj.transform, "AtlasProfile", Vector2.zero,
            new Vector2(1920, 1080), Deep, false);
        screen.anchorMin = Vector2.zero;
        screen.anchorMax = Vector2.one;
        screen.offsetMin = screen.offsetMax = Vector2.zero;
        // Block pointer clicks from reaching the hidden Career/Settings
        // origin or any still-visible Main Menu controls below this page.
        screen.GetComponent<Image>().raycastTarget = true;
        screen.gameObject.AddComponent<AtlasBoardEscapeBlocker>();
        // Permanent opaque background: visually and functionally a page,
        // never a translucent account modal.
        Rect(screen, "UpperGlow", new Vector2(0, 465),
            new Vector2(1920, 170), Violet, false);
        Rect(screen, "AccentBorder", new Vector2(0, 0),
            new Vector2(1870, 968), Bright);
        Rect(screen, "Body", new Vector2(0, 0),
            new Vector2(1862, 960), Navy);
        Text(screen, "Brand", "ATLAS BOARD", new Vector2(-767, 435),
            new Vector2(285, 42), 27, Gold, TextAlignmentOptions.Left, true);
        breadcrumbLabel = Text(screen, "ProfileBreadcrumb", "", new Vector2(-425, 400),
            new Vector2(355, 31), 17, Muted, TextAlignmentOptions.Left);
        Text(screen, "Title", "PROFİL", new Vector2(0, 417),
            new Vector2(460, 62), 49, TextColor, TextAlignmentOptions.Center, true);
        Btn(screen, "CloseProfile", "X", new Vector2(843, 417),
            new Vector2(76, 60), C("#B62969"), CloseScreen, 29);

        leftNav = Rect(screen, "LeftMenu", new Vector2(-777, -36),
            new Vector2(294, 790), Purple);
        stage = Rect(screen, "ProfileStage", new Vector2(150, -36),
            new Vector2(1538, 790), C("#1B2456"));
        // Dedicated section heading: Overview cards start below this area.
        Rect(screen, "SectionAccent", new Vector2(-530, 354),
            new Vector2(133, 4), Gold, false);
        headerText = Text(screen, "RouteHeader", "", new Vector2(115, 322),
            new Vector2(1280, 40), 28, Gold, TextAlignmentOptions.Left, true);
        subtitleText = Text(screen, "RouteSubtitle", "", new Vector2(115, 281),
            new Vector2(1280, 26), 17, Muted, TextAlignmentOptions.Left);
        Rect(screen, "FooterStrip", new Vector2(0, -503),
            new Vector2(1920, 73), Purple, false);
        footerText = Text(screen, "Footer", "ESC  ·  Geri      /      Sol menü  ·  Sekmeler",
            new Vector2(-470, -503), new Vector2(880, 45), 19, Muted,
            TextAlignmentOptions.Left);
        screen.gameObject.SetActive(false);
    }

    private static void Clear(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; --i)
        {
            Transform child = root.GetChild(i);
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private void RenderRoute()
    {
        if (screen == null || !screen.gameObject.activeSelf) return;
        Clear(leftNav);
        Clear(stage);
        nameLabel = avatarInitial = idLabel = levelLabel = xpLabel = steamLabel =
            lockedName = lockedEmail = null;
        renameAction = passwordAction = signOutAction = null;
        xpFill = null;
        if (breadcrumbLabel != null)
            breadcrumbLabel.text = openedFromSettings
                ? L("SETTINGS > PROFILE", "AYARLAR > PROFİL", "AJUSTES > PERFIL",
                    "PARAMÈTRES > PROFIL", "EINSTELLUNGEN > PROFIL", "설정 > 프로필",
                    "НАСТРОЙКИ > ПРОФИЛЬ")
                : L("CAREER > PROFILE", "KARİYER > PROFİL", "CARRERA > PERFIL",
                    "CARRIÈRE > PROFIL", "KARRIERE > PROFIL", "커리어 > 프로필",
                    "КАРЬЕРА > ПРОФИЛЬ");
        if (footerText != null) footerText.text = L(
            "ESC · Back    /    Side navigation · Tabs", "ESC · Geri    /    Sol menü · Sekmeler",
            "ESC · Atrás", "ESC · Retour", "ESC · Zurück", "ESC · 뒤로", "ESC · Назад");
        string[] names = {
            L("GENERAL PROFILE", "GENEL PROFİL", "PERFIL", "PROFIL", "PROFIL", "프로필", "ПРОФИЛЬ"),
            L("WEEKLY MISSIONS", "HAFTALIK GÖREVLER", "MISIONES", "MISSIONS", "WOCHENAUFGABEN", "주간 임무", "ЗАДАНИЯ"),
            L("ATLAS MUSEUM", "ATLAS MÜZESİ", "MUSEO ATLAS", "MUSÉE ATLAS", "ATLAS-MUSEUM", "아틀라스 박물관", "МУЗЕЙ АТЛАС"),
            L("ATLAS PASSPORT", "ATLAS PASAPORT", "PASAPORTE ATLAS", "PASSEPORT ATLAS", "ATLAS-PASS", "아틀라스 여권", "ПАСПОРТ АТЛАС")
        };
        for (int i = 0; i < names.Length; i++)
        {
            string captured = routes[i];
            bool isSelected = selectedRoute == captured;
            Button tab = Btn(leftNav, "ProfileTab_" + captured, names[i],
                new Vector2(0, 290 - 128 * i), new Vector2(282, 105),
                isSelected ? Violet : C("#26346B"),
                () => { selectedRoute = captured; RenderRoute(); }, 22);
            TextMeshProUGUI label = tab.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.rectTransform.anchoredPosition = new Vector2(15, 0);
                label.rectTransform.sizeDelta = new Vector2(213, 88);
                label.alignment = TextAlignmentOptions.MidlineLeft;
            }
            AddGlyph(tab.transform, "TabIcon", i, new Vector2(-114, 0),
                new Vector2(35, 35), isSelected ? Gold : Muted);
        }
        Text(leftNav, "MenuHint", L("Personal collection", "Kişisel koleksiyon",
            "Colección personal", "Collection personnelle", "Persönliche Sammlung",
            "개인 컬렉션", "Личная коллекция"),
            new Vector2(0, -345), new Vector2(265, 55), 16, Muted);

        switch (selectedRoute)
        {
            case "weekly":
                headerText.text = names[1];
                subtitleText.text = L("Four UID-specific missions · Weekly UTC reset",
                    "Dört kişisel görev · Haftalık UTC sıfırlama", "Cuatro misiones semanales",
                    "Quatre missions hebdomadaires", "Vier wöchentliche Aufgaben",
                    "주간 개인 임무 4개", "Четыре еженедельных задания");
                RenderWeekly(); break;
            case "museum":
                headerText.text = names[2];
                subtitleText.text = L("Three collectible display slots",
                    "Üç koleksiyon jetonu sergileme yuvası", "Tres espacios de colección",
                    "Trois emplacements", "Drei Sammelplätze", "전시 슬롯 세 개", "Три слота коллекции");
                RenderMuseum(); break;
            case "passport":
                headerText.text = names[3];
                subtitleText.text = L("Keep your journey and stamps together",
                    "Yolculuğun ve damgaların tek yerde", "Tus viajes y sellos",
                    "Vos voyages et tampons", "Reisen und Stempel", "여행과 스탬프", "Путешествия и штампы");
                RenderPassport(); break;
            default:
                headerText.text = names[0];
                subtitleText.text = L("Career progress · Account · Collection",
                    "Kariyer ilerlemesi · Hesap · Koleksiyon", "Progreso y cuenta",
                    "Progression et compte", "Karriere und Konto", "커리어와 계정", "Карьера и аккаунт");
                RenderOverview(); break;
        }
        RefreshVisibleIdentity();
    }

    private void RenderOverview()
    {
        // Shared canonical XP and level are read from ProgressionSnapshot.
        RectTransform identity = Rect(stage, "IdentityCard", new Vector2(-378, 202),
            new Vector2(736, 172), C("#283B81"));
        RectTransform avatar = Rect(identity, "AvatarInitialCircle", new Vector2(-288, 0),
            new Vector2(134, 134), Violet);
        avatarInitial = Text(avatar, "AvatarInitial", "A", Vector2.zero,
            new Vector2(127, 127), 72, Gold, TextAlignmentOptions.Center, true);
        nameLabel = Text(identity, "DisplayName", "", new Vector2(49, 51),
            new Vector2(467, 42), 37, TextColor, TextAlignmentOptions.Left, true);
        idLabel = Text(identity, "PlayerId", "", new Vector2(48, 11),
            new Vector2(465, 27), 19, Muted, TextAlignmentOptions.Left);
        levelLabel = Text(identity, "Level", "Lv. —", new Vector2(-139, -44),
            new Vector2(150, 38), 26, Gold, TextAlignmentOptions.Center, true);
        RectTransform xpBack = Rect(identity, "XpProgressBack", new Vector2(105, -43),
            new Vector2(388, 22), C("#141C47"));
        xpFill = Rect(xpBack, "XpProgressFill", Vector2.zero,
            Vector2.zero, Gold).GetComponent<Image>();
        xpFill.rectTransform.anchorMin = Vector2.zero;
        xpFill.rectTransform.anchorMax = Vector2.zero;
        xpFill.rectTransform.offsetMin = xpFill.rectTransform.offsetMax = Vector2.zero;
        xpLabel = Text(identity, "XpNumber", "", new Vector2(106, -72),
            new Vector2(377, 22), 16, Muted);

        RectTransform account = Rect(stage, "ReadOnlyIdentity", new Vector2(378, 202),
            new Vector2(710, 172), C("#2E366D"));
        lockedName = ReadOnlyRow(account, 50, L("Name", "İsim", "Nombre", "Nom", "Name", "이름", "Имя"),
            L("Account managed", "Hesap tarafından yönetilir", "Cuenta administrada",
                "Géré par compte", "Konto verwaltet", "계정 관리", "Управляется аккаунтом"));
        lockedEmail = ReadOnlyRow(account, 5, L("Email", "E-posta", "Correo", "E-mail", "E-Mail", "이메일", "Почта"), "••••••••");
        renameAction = Btn(account, "RenameAccount", L("CHANGE NAME", "ADI DEĞİŞTİR",
            "CAMBIAR NOMBRE", "CHANGER LE NOM", "NAME ÄNDERN", "이름 변경", "СМЕНИТЬ ИМЯ"),
            new Vector2(-225, -57), new Vector2(200, 43), Bright,
            OpenRenameAccountManager, 16);
        passwordAction = Btn(account, "ChangePassword", L("CHANGE PASSWORD", "ŞİFRE DEĞİŞTİR",
            "CAMBIAR CLAVE", "CHANGER LE CODE", "PASSWORT ÄNDERN", "비밀번호 변경", "СМЕНИТЬ ПАРОЛЬ"),
            new Vector2(-4, -57), new Vector2(212, 43), Teal,
            OpenPasswordResetConfirm, 15);
        signOutAction = Btn(account, "SignOutAccount", L("SIGN OUT", "ÇIKIŞ YAP",
            "SALIR", "DÉCONNEXION", "ABMELDEN", "로그아웃", "ВЫЙТИ"),
            new Vector2(225, -57), new Vector2(200, 43), C("#A8245B"),
            OpenSignOutConfirm, 16);

        RectTransform steam = Rect(stage, "SteamLine", new Vector2(0, 78),
            new Vector2(1460, 52), C("#342A6E"));
        steamLabel = Text(steam, "SteamConnection", "", new Vector2(-185, 0),
            new Vector2(1050, 42), 21, TextColor, TextAlignmentOptions.Left, true);
        Text(steam, "SteamReadOnly", L("Read-only", "Salt okunur", "Solo lectura",
            "Lecture seule", "Schreibgeschützt", "읽기 전용", "Только просмотр"),
            new Vector2(600, 0), new Vector2(200, 39), 16, Muted);

        RectTransform weekly = Rect(stage, "WeeklyPreview", new Vector2(-492, -156),
            new Vector2(456, 390), C("#33245F"));
        Text(weekly, "Heading", L("WEEKLY MISSIONS", "HAFTALIK GÖREVLER",
            "MISIONES", "MISSIONS", "WOCHENAUFGABEN", "주간 임무", "ЗАДАНИЯ"),
            new Vector2(0, 166), new Vector2(436, 48), 25, Gold,
            TextAlignmentOptions.Center, true);
        for (int i = 0; i < 4; ++i)
        {
            RectTransform item = Rect(weekly, "MissionPreview_" + i,
                new Vector2(0, 102 - i * 58), new Vector2(425, 49), C("#26346B"));
            Text(item, "Mission", L("Personal mission · Pending", "Kişisel görev · Bekleniyor",
                "Misión pendiente", "Mission en attente", "Aufgabe ausstehend",
                "개인 임무 대기", "Задание ожидается"),
                Vector2.zero, new Vector2(400, 39), 17, Muted);
        }
        Btn(weekly, "OpenWeekly", L("VIEW MISSIONS", "GÖREVLERİ AÇ",
            "VER MISIONES", "VOIR MISSIONS", "AUFGABEN ÖFFNEN",
            "임무 보기", "ОТКРЫТЬ"), new Vector2(0, -163),
            new Vector2(418, 43), Bright,
            () => { selectedRoute = "weekly"; RenderRoute(); }, 18);

        RectTransform museum = Rect(stage, "MuseumPreview", new Vector2(0, -156),
            new Vector2(454, 390), C("#33245F"));
        Text(museum, "Heading", L("ATLAS MUSEUM", "ATLAS MÜZESİ",
            "MUSEO ATLAS", "MUSÉE ATLAS", "ATLAS MUSEUM", "아틀라스 박물관", "МУЗЕЙ АТЛАС"),
            new Vector2(0, 166), new Vector2(428, 48), 25, Gold,
            TextAlignmentOptions.Center, true);
        DrawMuseumCoins(museum, 12, false);
        Text(museum, "Info", L("Show up to 3 owned collection tokens.",
            "En fazla 3 sahip olunan jetonu sergile.", "Muestra hasta 3 fichas.",
            "Exposez jusqu'à 3 jetons.", "Bis zu 3 Jetons ausstellen.",
            "최대 토큰 3개 전시", "Выставьте до 3 жетонов."),
            new Vector2(0, -114), new Vector2(425, 45), 18, Muted);
        Btn(museum, "OpenMuseum", L("OPEN MUSEUM", "MÜZEYİ AÇ", "ABRIR MUSEO",
            "OUVRIR MUSÉE", "MUSEUM ÖFFNEN", "박물관 보기", "ОТКРЫТЬ МУЗЕЙ"),
            new Vector2(0, -163), new Vector2(418, 43), Bright,
            () => { selectedRoute = "museum"; RenderRoute(); }, 18);

        RectTransform passport = Rect(stage, "PassportPreview", new Vector2(492, -156),
            new Vector2(456, 390), C("#233F78"));
        Text(passport, "Heading", L("ATLAS PASSPORT", "ATLAS PASAPORT",
            "PASAPORTE ATLAS", "PASSEPORT ATLAS", "ATLAS-PASS", "아틀라스 여권", "ПАСПОРТ АТЛАС"),
            new Vector2(0, 166), new Vector2(436, 48), 25, Gold,
            TextAlignmentOptions.Center, true);
        RectTransform book = Rect(passport, "PassportBook", new Vector2(0, 11),
            new Vector2(224, 236), C("#7D335A"));
        Text(book, "Cover", "ATLAS\nPASSPORT", new Vector2(0, 38),
            new Vector2(205, 100), 28, Gold, TextAlignmentOptions.Center, true);
        AddGlyph(book, "PassportMark", 3, new Vector2(0, -65),
            new Vector2(48, 48), Gold);
        Btn(passport, "OpenPassport", L("OPEN PASSPORT", "PASAPORTU AÇ",
            "ABRIR PASAPORTE", "OUVRIR PASSEPORT", "PASS ÖFFNEN",
            "여권 보기", "ОТКРЫТЬ ПАСПОРТ"),
            new Vector2(0, -163), new Vector2(418, 43), Teal,
            () => { selectedRoute = "passport"; RenderRoute(); }, 18);
        RefreshVisibleIdentity();
    }

    private TextMeshProUGUI ReadOnlyRow(Transform parent, float y, string name, string value)
    {
        Text(parent, "FieldLabel", name, new Vector2(-237, y),
            new Vector2(165, 35), 20, TextColor, TextAlignmentOptions.Left, true);
        RectTransform field = Rect(parent, "LockedField", new Vector2(101, y),
            new Vector2(435, 40), C("#252B5A"));
        TextMeshProUGUI text = Text(field, "Value", value,
            new Vector2(-16, 0), new Vector2(359, 31), 17, Muted,
            TextAlignmentOptions.Left);
        AddGlyph(field, "LockIcon", 4, new Vector2(184, 0),
            new Vector2(26, 26), Muted);
        return text;
    }

    private void DrawMuseumCoins(Transform parent, int yOffset, bool big)
    {
        int diameter = big ? 180 : 126;
        int gap = big ? 280 : 146;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            RectTransform rim = Rect(parent, "CollectionSlot_" + idx,
                new Vector2((i - 1) * gap, yOffset + (big ? 35 : 0)),
                new Vector2(diameter + 12, diameter + 12), Gold);
            RectTransform inner = Rect(rim, "EmptyCoin", Vector2.zero,
                new Vector2(diameter, diameter), C("#392C75"));
            Text(inner, "Slot", new[] {"I", "II", "III"}[i], Vector2.zero,
                new Vector2(diameter - 20, 68), big ? 48 : 35,
                Muted, TextAlignmentOptions.Center, true);
            if (big)
                Text(parent, "SlotStatus" + idx, L("Empty slot", "Boş yuva",
                    "Vacío", "Vide", "Leer", "비어 있음", "Пусто"),
                    new Vector2((i - 1) * gap, -124),
                    new Vector2(220, 42), 19, Muted);
        }
    }

    private void RenderWeekly()
    {
        for (int i = 0; i < 4; ++i)
        {
            RectTransform line = Rect(stage, "WeeklyMissionSlot_" + i,
                new Vector2(0, 250 - i * 121), new Vector2(1440, 102), C("#293B80"));
            Text(line, "Number", (i + 1).ToString("00"), new Vector2(-639, 0),
                new Vector2(100, 59), 34, Gold, TextAlignmentOptions.Center, true);
            Text(line, "Pending", L("Waiting for an account-specific verified mission",
                "Hesaba özel doğrulanmış görev bekleniyor", "Esperando misión verificada",
                "Mission vérifiée en attente", "Verifizierte Aufgabe ausstehend",
                "확인된 개인 임무 대기", "Ожидается проверенное задание"),
                new Vector2(20, 0), new Vector2(1070, 75), 24, TextColor,
                TextAlignmentOptions.Left);
        }
        Text(stage, "WeeklyPolicy", L(
            "Weekly missions will only advance after trusted online match results. Tokens are not granted by this screen.",
            "Haftalık görevler yalnızca doğrulanmış online maçlarla ilerleyecek. Bu ekran jeton vermez.",
            "Solo cuentan partidas verificadas.", "Seules les parties vérifiées comptent.",
            "Nur bestätigte Online-Partien zählen.", "검증된 온라인 게임만 적용됩니다.",
            "Учитываются только подтверждённые онлайн-матчи."),
            new Vector2(0, -279), new Vector2(1375, 98), 20, Muted);
    }

    private void RenderMuseum()
    {
        RectTransform display = Rect(stage, "MuseumDisplay", new Vector2(0, 43),
            new Vector2(1410, 520), C("#302B68"));
        Text(display, "MuseumTitle", L("YOUR DISPLAY CASE", "KOLEKSİYON VİTRİNİN",
            "TU COLECCIÓN", "VOTRE COLLECTION", "DEINE SAMMLUNG",
            "내 컬렉션", "ВАША КОЛЛЕКЦИЯ"), new Vector2(0, 205),
            new Vector2(1250, 55), 32, Gold, TextAlignmentOptions.Center, true);
        DrawMuseumCoins(display, 0, true);
        Text(display, "Notice", L("Token art and equipping will be activated after server ownership verification.",
            "Jeton görselleri ve kuşanma, sunucu sahiplik doğrulamasından sonra etkinleştirilecek.",
            "La propiedad se verificará en el servidor.", "Propriété vérifiée par le serveur.",
            "Besitz wird serverseitig geprüft.", "서버 소유권 확인 후 활성화됩니다.",
            "Владение проверяется сервером."),
            new Vector2(0, -196), new Vector2(1280, 62), 21, Muted);
    }

    private void RenderPassport()
    {
        RectTransform book = Rect(stage, "PassportCover", new Vector2(-285, 1),
            new Vector2(410, 560), C("#8C355D"));
        Rect(book, "CoverBorder", Vector2.zero, new Vector2(385, 535), C("#5B2648"));
        Text(book, "CoverTitle", "ATLAS\nPASSPORT", new Vector2(0, 83),
            new Vector2(345, 210), 50, Gold, TextAlignmentOptions.Center, true);
        AddGlyph(book, "PassportMark", 3, new Vector2(0, -116),
            new Vector2(78, 78), Gold);
        RectTransform details = Rect(stage, "PassportDetails", new Vector2(385, 1),
            new Vector2(720, 560), C("#293B80"));
        Text(details, "Explanation", L("Your Atlas Passport will open here. Travel stamps will be added only from verified matches and manually claimed by the player.",
            "Atlas Pasaport bu sekmeden açılacak. Damgalar yalnızca doğrulanmış maçlardan kazanılacak ve oyuncu tarafından elle basılacak.",
            "Las estampillas se obtienen en partidas verificadas.",
            "Les tampons proviennent de parties vérifiées.",
            "Stempel werden in bestätigten Partien verdient.",
            "검증된 경기에서만 스탬프를 받을 수 있습니다.",
            "Штампы выдаются за подтверждённые матчи."),
            new Vector2(0, 68), new Vector2(630, 210), 26, TextColor);
        Text(details, "Status", L("Stamps: not enabled yet", "Damgalar: henüz etkin değil",
            "Sellos no disponibles", "Tampons indisponibles", "Stempel noch inaktiv",
            "스탬프 미활성", "Штампы пока не включены"),
            new Vector2(0, -150), new Vector2(610, 60), 24, Gold);
    }
}
