using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// Provider-neutral AtlasBoard account entry surface.
///
/// Unsigned players may keep playing Local Offline as a persistent GuestxxxxxX.
/// Career and Settings expose account access without forcing authentication at
/// boot. Credentials are handled only by Firebase Authentication: passwords are
/// never written to Firestore, RTDB, PlayerPrefs or AtlasBoard logs.
/// </summary>
[DefaultExecutionOrder(14500)]
public sealed class AtlasBoardAccountAccessUI : MonoBehaviour
{
    private enum AuthView
    {
        Login,
        Register,
        Account
    }


    private const long RenameCooldownMilliseconds =
        24L * 60L * 60L * 1000L;


    private static AtlasBoardAccountAccessUI instance;


    private AtlasBoardAccountService accountService;
    private AtlasBoardProgressionUI progressionUi;
    private AtlasBoardSettingsV2Controller settingsController;


    private Canvas canvas;
    private GameObject modalBackdrop;
    private RectTransform modalWindow;
    private TMP_Text modalTitle;
    private TMP_Text statusText;
    private TMP_Text busyText;
    private GameObject formRoot;
    private Button contextButton;
    private TMP_Text contextButtonLabel;


    private TMP_InputField loginEmail;
    private TMP_InputField loginPassword;


    private TMP_InputField registerName;
    private TMP_InputField registerEmail;
    private TMP_InputField registerPassword;
    private TMP_InputField registerPasswordRepeat;
    private TMP_InputField captchaAnswer;
    private TMP_Text captchaQuestion;


    private TMP_InputField renameInput;


    private readonly List<Button> activeFormButtons =
        new List<Button>();


    private Button boundCareerButton;
    private float nextScanAt;
    private float nextIdentityApplyAt;
    private bool busy;
    private float busyAnimationAt;
    private int busyDots;
    private bool openCareerAfterAuthentication;
    private AuthView currentView;


    private int captchaA;
    private int captchaB;
    private bool captchaAddition;
    private int captchaExpected;


    private string loadedUid = string.Empty;
    private string canonicalDisplayName = string.Empty;
    private bool identityLoadInFlight;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;


        GameObject host =
            new GameObject("AtlasBoard_Account_Access_UI");


        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardAccountAccessUI>();
    }


    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }


        instance = this;
        AtlasBoardGuestIdentity.EnsureCreated();


        BuildUi();
        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;
    }


    private void OnDestroy()
    {
        if (instance != this)
            return;


        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;
        instance = null;
    }


    private void Update()
    {
        if (busy &&
            Time.unscaledTime >= busyAnimationAt)
        {
            busyAnimationAt = Time.unscaledTime + 0.35f;
            busyDots = (busyDots + 1) % 4;


            if (busyText != null)
            {
                busyText.text =
                    T(
                        "WORKING",
                        "İŞLENİYOR",
                        "PROCESANDO",
                        "TRAITEMENT",
                        "WIRD VERARBEITET",
                        "처리 중",
                        "ОБРАБОТКА") +
                    new string('.', busyDots);
            }
        }


        if (Time.unscaledTime >= nextScanAt)
        {
            nextScanAt = Time.unscaledTime + 0.85f;


            ResolveRuntimeReferences();
            BindCareerButton();
            UpdateContextButton();
            _ = ObserveAuthenticationAsync();
        }


        if (Time.unscaledTime >= nextIdentityApplyAt)
        {
            nextIdentityApplyAt = Time.unscaledTime + 1.25f;
            ApplyIdentityPresentation();
        }
    }


    private void HandleLanguageChanged()
    {
        UpdateContextButton();


        if (modalBackdrop != null &&
            modalBackdrop.activeSelf)
        {
            RebuildCurrentView();
        }
    }


    private void ResolveRuntimeReferences()
    {
        if (accountService == null)
        {
            accountService = AtlasBoardAccountService.Instance;


            if (accountService == null)
            {
                accountService =
                    UnityEngine.Object.FindAnyObjectByType<
                        AtlasBoardAccountService>(
                            FindObjectsInactive.Include);
            }
        }


        if (progressionUi == null)
        {
            progressionUi =
                UnityEngine.Object.FindAnyObjectByType<
                    AtlasBoardProgressionUI>(
                        FindObjectsInactive.Include);
        }


        if (settingsController == null)
        {
            settingsController =
                AtlasBoardSettingsV2Controller.Instance;


            if (settingsController == null)
            {
                settingsController =
                    UnityEngine.Object.FindAnyObjectByType<
                        AtlasBoardSettingsV2Controller>(
                            FindObjectsInactive.Include);
            }
        }
    }


    private void BindCareerButton()
    {
        Button button = FindSceneButton("ProgressionCard");
        if (button == null ||
            button == boundCareerButton)
        {
            return;
        }


        boundCareerButton = button;


        // One canonical Career entry path: unsigned users get the account
        // gate, signed users invoke the existing Career implementation.
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(HandleCareerPressed);


        Debug.Log(
            "ATB_ACCOUNT_UI career_entry_bound=true signedIn=" +
            IsSignedIn());
    }


    // Store uses the existing canonical Firebase account modal. Anonymous
    // Firebase Auth sessions must be upgraded via an explicit registration or
    // email/password login before a private RTDB economy can be initialized.
    public static bool OpenLoginForStore()
    {
        if (instance == null) return false;
        instance.openCareerAfterAuthentication = false;
        instance.OpenModal(AuthView.Login);
        instance.SetStatus(T(
            "Sign in or create an account before purchasing cosmetics.",
            "Kozmetik satın almak için giriş yap veya hesap oluştur.",
            "Inicia sesión o crea una cuenta para comprar cosméticos.",
            "Connectez-vous ou créez un compte avant l'achat.",
            "Bitte anmelden oder ein Konto zum Kauf erstellen.",
            "아이템을 구매하려면 로그인하거나 계정을 만드세요.",
            "Для покупки войдите или создайте учётную запись."));
        return true;
    }

    private void HandleCareerPressed()
    {
        if (IsSignedIn())
        {
            InvokeProgressionOpenCareer();
            return;
        }


        openCareerAfterAuthentication = true;
        OpenModal(AuthView.Login);


        SetStatus(
            T(
                "Sign in to use cloud Career. Offline play stays available.",
                "Bulut Kariyer'i kullanmak için giriş yap. Çevrimdışı oyun kullanılabilir kalır.",
                "Inicia sesión para usar Carrera en la nube. El juego sin conexión sigue disponible.",
                "Connectez-vous pour utiliser la carrière cloud. Le jeu hors ligne reste disponible.",
                "Melde dich für die Cloud-Karriere an. Offline-Spiel bleibt verfügbar.",
                "클라우드 커리어를 사용하려면 로그인하세요. 오프라인 플레이는 계속 가능합니다.",
                "Войдите, чтобы использовать облачную карьеру. Офлайн-игра останется доступной."));
    }


    private void InvokeProgressionOpenCareer()
    {
        ResolveRuntimeReferences();


        if (progressionUi == null)
            return;


        MethodInfo method =
            progressionUi.GetType().GetMethod(
                "OpenCareer",
                BindingFlags.Instance |
                BindingFlags.NonPublic);


        if (method == null)
            return;


        try
        {
            method.Invoke(progressionUi, null);
        }
        catch (Exception error)
        {
            Debug.LogWarning(
                "ATB_ACCOUNT_UI career_open_failed type=" +
                error.GetType().Name);
        }
    }


    private void UpdateContextButton()
    {
        if (contextButton == null)
            return;


        bool settingsVisible =
            IsPrivateRootVisible(
                settingsController,
                "settingsRoot");


        bool careerVisible =
            IsPrivateRootVisible(
                progressionUi,
                "root");


        bool show = settingsVisible || careerVisible;


        if (contextButton.gameObject.activeSelf != show)
            contextButton.gameObject.SetActive(show);


        if (!show || contextButtonLabel == null)
            return;


        contextButtonLabel.text =
            IsSignedIn()
                ? T(
                    "ACCOUNT",
                    "HESAP",
                    "CUENTA",
                    "COMPTE",
                    "KONTO",
                    "계정",
                    "АККАУНТ")
                : T(
                    "SIGN IN",
                    "GİRİŞ YAP",
                    "INICIAR SESIÓN",
                    "SE CONNECTER",
                    "ANMELDEN",
                    "로그인",
                    "ВОЙТИ");
    }


    private async Task ObserveAuthenticationAsync()
    {
        if (identityLoadInFlight)
            return;


        ResolveRuntimeReferences();


        if (accountService == null)
        {
            loadedUid = string.Empty;
            canonicalDisplayName = string.Empty;
            return;
        }


        if (!accountService.IsSignedIn)
        {
            if (!string.IsNullOrWhiteSpace(loadedUid) ||
                !string.IsNullOrWhiteSpace(canonicalDisplayName))
            {
                loadedUid = string.Empty;
                canonicalDisplayName = string.Empty;
                ApplyIdentityPresentation();
            }


            return;
        }


        string uid = accountService.CurrentAccountId ?? string.Empty;


        if (string.IsNullOrWhiteSpace(uid) ||
            (string.Equals(
                 uid,
                 loadedUid,
                 StringComparison.Ordinal) &&
             !string.IsNullOrWhiteSpace(canonicalDisplayName)))
        {
            return;
        }


        identityLoadInFlight = true;


        try
        {
            AtlasAccountSnapshot snapshot =
                await accountService.LoadCurrentAccountAsync();


            if (snapshot == null ||
                string.IsNullOrWhiteSpace(snapshot.DisplayName))
            {
                return;
            }


            loadedUid = snapshot.AccountId ?? uid;
            canonicalDisplayName = snapshot.DisplayName.Trim();
            ApplyIdentityPresentation();


            await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync(
                canonicalDisplayName);


            Debug.Log(
                "ATB_ACCOUNT_IDENTITY canonical_loaded=true uid=" +
                ShortUid(loadedUid));
        }
        catch (Exception error)
        {
            Debug.LogWarning(
                "ATB_ACCOUNT_IDENTITY load_failed type=" +
                error.GetType().Name);
        }
        finally
        {
            identityLoadInFlight = false;
        }
    }


    private void ApplyIdentityPresentation()
    {
        string name =
            IsSignedIn() &&
            !string.IsNullOrWhiteSpace(canonicalDisplayName)
                ? canonicalDisplayName
                : AtlasBoardGuestIdentity.DisplayName;


        AtlasBoardMainMenuController menu =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardMainMenuController>(
                    FindObjectsInactive.Include);


        if (menu != null)
        {
            SetPrivateField(menu, "profileName", name);


            TMP_Text profileNameText =
                ReadPrivateField<TMP_Text>(
                    menu,
                    "profileNameText");


            if (profileNameText != null &&
                !string.Equals(
                    profileNameText.text,
                    name,
                    StringComparison.Ordinal))
            {
                profileNameText.text = name;
            }


            Transform avatar =
                FindChildRecursive(
                    menu.transform,
                    "AvatarInitial");


            if (avatar != null &&
                avatar.TryGetComponent(out TMP_Text avatarText))
            {
                avatarText.text = GetInitial(name);
            }
        }


        // There may be more than one ProfileName presentation (Main Menu and
        // lobby header). Keep them synchronized from the same canonical value.
        TMP_Text[] allText =
            Resources.FindObjectsOfTypeAll<TMP_Text>();


        foreach (TMP_Text text in allText)
        {
            if (text == null ||
                !text.gameObject.scene.IsValid() ||
                !string.Equals(
                    text.gameObject.name,
                    "ProfileName",
                    StringComparison.Ordinal))
            {
                continue;
            }


            text.text = name;
        }


        PlayerGameState[] players =
            UnityEngine.Object.FindObjectsByType<PlayerGameState>(
                FindObjectsInactive.Include);


        foreach (PlayerGameState player in players)
        {
            if (player == null ||
                player.PlayerSlotIndex != 0 ||
                player.OnlineSeatStateActive)
            {
                continue;
            }


            string current =
                ReadPrivateField<string>(
                    player,
                    "displayName");


            if (string.Equals(
                    current,
                    name,
                    StringComparison.Ordinal))
            {
                continue;
            }


            SetPrivateField(
                player,
                "displayName",
                name);


            FieldInfo eventField =
                typeof(PlayerGameState).GetField(
                    "IdentityChanged",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);


            if (eventField?.GetValue(player) is Action<PlayerGameState> action)
                action.Invoke(player);
        }


        // Online lobby seat labels belong exclusively to the authoritative
        // lobby snapshot renderer, NOT the local account/profile UI.
        // On a remote Guest client slot 0 is another user's Host seat.
        // Writing the local Guest alias there caused the observed 1.25-second
        // Guest/Host name oscillation. AtlasBoardPrivateLobbyUIController and
        // AtlasBoardUnifiedProfilePresentation own seat-aware labels.
    }


    private void BuildUi()
    {
        GameObject canvasObject =
            new GameObject(
                "AtlasBoard_Account_Access_Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);


        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1700;


        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;


        contextButton = CreateButton(
            canvasObject.transform,
            "Button_AccountContext",
            string.Empty,
            new Color32(40, 112, 165, 248),
            new Vector2(150f, 72f),
            new Vector2(240f, 68f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f));
        contextButtonLabel =
            contextButton.GetComponentInChildren<TMP_Text>(true);
        contextButton.onClick.AddListener(
            () => OpenModal(
                IsSignedIn()
                    ? AuthView.Account
                    : AuthView.Login));
        contextButton.gameObject.SetActive(false);


        modalBackdrop = CreatePanel(
            canvasObject.transform,
            "AccountModalBackdrop",
            new Color(0f, 0f, 0f, 0.66f));


        modalWindow = CreatePanel(
                modalBackdrop.transform,
                "AccountWindow",
                new Color32(247, 246, 241, 255))
            .GetComponent<RectTransform>();
        modalWindow.anchorMin = new Vector2(0.5f, 0.5f);
        modalWindow.anchorMax = new Vector2(0.5f, 0.5f);
        modalWindow.pivot = new Vector2(0.5f, 0.5f);
        modalWindow.sizeDelta = new Vector2(980f, 900f);
        modalWindow.anchoredPosition = Vector2.zero;


        GameObject header = CreatePanel(
            modalWindow,
            "Header",
            new Color32(25, 101, 148, 255));
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 104f);
        headerRect.anchoredPosition = Vector2.zero;


        modalTitle = CreateText(
            header.transform,
            "AccountTitle",
            34f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white);
        Stretch(modalTitle.rectTransform, 95f, 18f);


        Button close = CreateButton(
            header.transform,
            "Button_CloseAccount",
            "×",
            new Color32(190, 38, 76, 255),
            new Vector2(-28f, -52f),
            new Vector2(66f, 66f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f));
        close.onClick.AddListener(CloseModal);


        statusText = CreateText(
            modalWindow,
            "Status",
            20f,
            FontStyles.Normal,
            TextAlignmentOptions.Center,
            new Color32(50, 58, 68, 255));
        SetAnchored(
            statusText.rectTransform,
            new Vector2(0f, -282f),
            new Vector2(820f, 84f));
        statusText.textWrappingMode = TextWrappingModes.Normal;


        busyText = CreateText(
            modalWindow,
            "Busy",
            18f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            new Color32(25, 101, 148, 255));
        SetAnchored(
            busyText.rectTransform,
            new Vector2(0f, -238f),
            new Vector2(500f, 42f));
        busyText.gameObject.SetActive(false);


        modalBackdrop.SetActive(false);
    }


    private void OpenModal(AuthView view)
    {
        ResolveRuntimeReferences();


        currentView = view;
        modalBackdrop.SetActive(true);
        modalBackdrop.transform.SetAsLastSibling();
        SetStatus(string.Empty);
        RebuildCurrentView();
    }


    private void CloseModal()
    {
        if (busy)
            return;


        openCareerAfterAuthentication = false;
        modalBackdrop.SetActive(false);
    }


    private void RebuildCurrentView()
    {
        if (formRoot != null)
            Destroy(formRoot);


        activeFormButtons.Clear();


        formRoot =
            new GameObject(
                "FormRoot",
                typeof(RectTransform));
        formRoot.transform.SetParent(modalWindow, false);


        RectTransform formRect =
            formRoot.GetComponent<RectTransform>();
        formRect.anchorMin = Vector2.zero;
        formRect.anchorMax = Vector2.one;
        formRect.offsetMin = Vector2.zero;
        formRect.offsetMax = new Vector2(0f, -104f);


        switch (currentView)
        {
            case AuthView.Register:
                BuildRegisterView();
                break;
            case AuthView.Account:
                BuildAccountView();
                break;
            default:
                BuildLoginView();
                break;
        }


        if (statusText != null)
        {
            // Keep the canonical Guest action separate from error/status copy.
            RectTransform statusRect = statusText.rectTransform;
            statusRect.anchoredPosition = new Vector2(0f,
                currentView == AuthView.Login ? -385f : -282f);
        }
        if (busy)
            ApplyBusyState(true);
    }


    private void BuildLoginView()
    {
        modalTitle.text = T(
            "ATLAS ACCOUNT",
            "ATLAS HESABI",
            "CUENTA ATLAS",
            "COMPTE ATLAS",
            "ATLAS-KONTO",
            "ATLAS 계정",
            "АККАУНТ ATLAS");


        CreateFieldLabel("EmailLabel", T("Email", "E-posta", "Correo", "E-mail", "E-Mail", "이메일", "Эл. почта"), 250f);
        loginEmail = CreateInput("LoginEmail", 190f, false, true);


        CreateFieldLabel("PasswordLabel", T("Password", "Şifre", "Contraseña", "Mot de passe", "Passwort", "비밀번호", "Пароль"), 92f);
        loginPassword = CreateInput("LoginPassword", 32f, true, false);


        Button signIn = CreateFormButton(
            "Button_SignIn",
            T("SIGN IN", "GİRİŞ YAP", "INICIAR SESIÓN", "SE CONNECTER", "ANMELDEN", "로그인", "ВОЙТИ"),
            new Color32(30, 127, 184, 255),
            new Vector2(-190f, -104f),
            new Vector2(330f, 70f));
        signIn.onClick.AddListener(
            () => _ = SignInAsync());


        Button register = CreateFormButton(
            "Button_OpenRegister",
            T("CREATE ACCOUNT", "HESAP OLUŞTUR", "CREAR CUENTA", "CRÉER UN COMPTE", "KONTO ERSTELLEN", "계정 만들기", "СОЗДАТЬ АККАУНТ"),
            new Color32(92, 82, 214, 255),
            new Vector2(190f, -104f),
            new Vector2(330f, 70f));
        register.onClick.AddListener(
            () =>
            {
                currentView = AuthView.Register;
                SetStatus(string.Empty);
                RebuildCurrentView();
            });


        Button reset = CreateFormButton(
            "Button_ResetPassword",
            T("FORGOT PASSWORD?", "ŞİFREMİ UNUTTUM", "¿OLVIDASTE TU CONTRASEÑA?", "MOT DE PASSE OUBLIÉ ?", "PASSWORT VERGESSEN?", "비밀번호 재설정", "ЗАБЫЛИ ПАРОЛЬ?"),
            new Color32(91, 101, 112, 255),
            new Vector2(0f, -196f),
            new Vector2(410f, 58f));
        reset.onClick.AddListener(
            () => _ = SendPasswordResetAsync());

        // The canonical Account modal owns this action on every platform.
        // A Guest never needs Firebase credentials just to preview Career or
        // return to offline play; no button cloning or mobile-only callbacks.
        Button continueGuest = CreateFormButton(
            "Button_ContinueAsGuest",
            T("CONTINUE AS GUEST", "MİSAFİR OLARAK DEVAM ET",
              "CONTINUAR COMO INVITADO", "CONTINUER COMME INVITÉ",
              "ALS GAST FORTFAHREN", "게스트로 계속", "ПРОДОЛЖИТЬ КАК ГОСТЬ"),
            new Color32(82, 67, 170, 255),
            new Vector2(0f, -292f),
            new Vector2(430f, 60f));
        continueGuest.onClick.AddListener(ContinueAsGuest);
    }

    private void ContinueAsGuest()
    {
        if (busy)
            return;
        bool openCareer = openCareerAfterAuthentication;
        openCareerAfterAuthentication = false;
        modalBackdrop.SetActive(false);
        if (!openCareer)
            return;

        ResolveRuntimeReferences();
        if (progressionUi == null)
            return;
        // Placeholder is only a local Guest preview, NEVER a cloud Career or
        // an authoritative reward/statistic.
        FieldInfo field = progressionUi.GetType().GetField("snapshot",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null && field.GetValue(progressionUi) == null)
            field.SetValue(progressionUi, new AtlasBoardProgressionSnapshot
            {
                Success = false,
                DisplayName = AtlasBoardGuestIdentity.DisplayName,
                Level = 1
            });
        InvokeProgressionOpenCareer();
        Debug.Log("ATB_GUEST_CAREER preview=local_only rewards=false " +
                  "account_sign_in_required=false");
    }

    private void BuildRegisterView()
    {
        modalTitle.text = T(
            "CREATE ATLAS ACCOUNT",
            "ATLAS HESABI OLUŞTUR",
            "CREAR CUENTA ATLAS",
            "CRÉER UN COMPTE ATLAS",
            "ATLAS-KONTO ERSTELLEN",
            "ATLAS 계정 만들기",
            "СОЗДАТЬ АККАУНТ ATLAS");


        CreateFieldLabel("NameLabel", T("Display name", "Oyuncu adı", "Nombre", "Nom", "Anzeigename", "표시 이름", "Имя игрока"), 292f);
        registerName = CreateInput("RegisterName", 240f, false, false);
        registerName.text = AtlasBoardGuestIdentity.DisplayName;


        CreateFieldLabel("EmailLabel", T("Email", "E-posta", "Correo", "E-mail", "E-Mail", "이메일", "Эл. почта"), 165f);
        registerEmail = CreateInput("RegisterEmail", 113f, false, true);


        CreateFieldLabel("PasswordLabel", T("Password", "Şifre", "Contraseña", "Mot de passe", "Passwort", "비밀번호", "Пароль"), 38f);
        registerPassword = CreateInput("RegisterPassword", -14f, true, false);


        CreateFieldLabel("RepeatLabel", T("Repeat password", "Şifre tekrar", "Repetir contraseña", "Répéter le mot de passe", "Passwort wiederholen", "비밀번호 확인", "Повторите пароль"), -89f);
        registerPasswordRepeat = CreateInput("RegisterPasswordRepeat", -141f, true, false);


        captchaQuestion = CreateText(
            formRoot.transform,
            "CaptchaQuestion",
            21f,
            FontStyles.Bold,
            TextAlignmentOptions.Left,
            new Color32(45, 52, 61, 255));
        SetAnchored(
            captchaQuestion.rectTransform,
            new Vector2(-230f, -222f),
            new Vector2(260f, 52f));


        captchaAnswer = CreateInput("CaptchaAnswer", -222f, false, false);
        RectTransform captchaRect = captchaAnswer.transform as RectTransform;
        if (captchaRect != null)
        {
            captchaRect.sizeDelta = new Vector2(300f, 64f);
            captchaRect.anchoredPosition = new Vector2(175f, -222f);
        }


        GenerateCaptcha();


        Button create = CreateFormButton(
            "Button_CreateAccount",
            T("CREATE ACCOUNT", "HESAP OLUŞTUR", "CREAR CUENTA", "CRÉER LE COMPTE", "KONTO ERSTELLEN", "계정 만들기", "СОЗДАТЬ АККАУНТ"),
            new Color32(92, 82, 214, 255),
            new Vector2(-180f, -350f),
            new Vector2(350f, 70f));
        create.onClick.AddListener(
            () => _ = RegisterAsync());


        Button back = CreateFormButton(
            "Button_BackToLogin",
            T("BACK", "GERİ", "ATRÁS", "RETOUR", "ZURÜCK", "뒤로", "НАЗАД"),
            new Color32(91, 101, 112, 255),
            new Vector2(220f, -350f),
            new Vector2(260f, 70f));
        back.onClick.AddListener(
            () =>
            {
                currentView = AuthView.Login;
                SetStatus(string.Empty);
                RebuildCurrentView();
            });
    }


    private void BuildAccountView()
    {
        modalTitle.text = T(
            "ACCOUNT",
            "HESAP",
            "CUENTA",
            "COMPTE",
            "KONTO",
            "계정",
            "АККАУНТ");


        string visibleName =
            !string.IsNullOrWhiteSpace(canonicalDisplayName)
                ? canonicalDisplayName
                : AtlasBoardGuestIdentity.DisplayName;


        string email = string.Empty;
        FirebaseUser user =
            FirebaseAuth.DefaultInstance != null
                ? FirebaseAuth.DefaultInstance.CurrentUser
                : null;


        if (user != null)
            email = user.Email ?? string.Empty;


        TMP_Text identity = CreateText(
            formRoot.transform,
            "IdentitySummary",
            25f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            new Color32(42, 48, 58, 255));
        SetAnchored(
            identity.rectTransform,
            new Vector2(0f, 245f),
            new Vector2(760f, 110f));
        identity.text = visibleName + "\n" + email;


        CreateFieldLabel("RenameLabel", T("Change display name", "Oyuncu adını değiştir", "Cambiar nombre", "Changer le nom", "Anzeigenamen ändern", "표시 이름 변경", "Изменить имя"), 105f);
        renameInput = CreateInput("RenameInput", 45f, false, false);
        renameInput.text = visibleName;


        Button rename = CreateFormButton(
            "Button_Rename",
            T("CHANGE NAME", "ADI DEĞİŞTİR", "CAMBIAR NOMBRE", "CHANGER LE NOM", "NAMEN ÄNDERN", "이름 변경", "ИЗМЕНИТЬ ИМЯ"),
            new Color32(30, 127, 184, 255),
            new Vector2(0f, -70f),
            new Vector2(360f, 70f));
        rename.onClick.AddListener(
            () => _ = RenameAsync());


        TMP_Text cooldown = CreateText(
            formRoot.transform,
            "RenameCooldown",
            17f,
            FontStyles.Normal,
            TextAlignmentOptions.Center,
            new Color32(85, 92, 102, 255));
        SetAnchored(
            cooldown.rectTransform,
            new Vector2(0f, -145f),
            new Vector2(700f, 48f));
        cooldown.text = BuildRenameCooldownText();


        Button signOut = CreateFormButton(
            "Button_SignOut",
            T("SIGN OUT", "ÇIKIŞ YAP", "CERRAR SESIÓN", "SE DÉCONNECTER", "ABMELDEN", "로그아웃", "ВЫЙТИ"),
            new Color32(190, 38, 76, 255),
            new Vector2(0f, -225f),
            new Vector2(300f, 66f));
        signOut.onClick.AddListener(SignOut);
    }


    private async Task SignInAsync()
    {
        if (busy)
            return;


        ResolveRuntimeReferences();


        if (accountService == null)
        {
            SetStatus(ServiceUnavailable());
            return;
        }


        string email = loginEmail?.text?.Trim() ?? string.Empty;
        string password = loginPassword?.text ?? string.Empty;


        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrEmpty(password))
        {
            SetStatus(T("Enter email and password.", "E-posta ve şifre gir.", "Introduce correo y contraseña.", "Saisissez l’e-mail et le mot de passe.", "E-Mail und Passwort eingeben.", "이메일과 비밀번호를 입력하세요.", "Введите эл. почту и пароль."));
            return;
        }


        ApplyBusyState(true);


        try
        {
            AtlasAccountOperationResult result =
                await accountService.SignInWithEmailPasswordAsync(
                    email,
                    password);


            if (!result.Success)
            {
                SetStatus(FriendlyError(result));
                return;
            }


            loadedUid = string.Empty;
            canonicalDisplayName = string.Empty;
            await ForceLoadCanonicalIdentityAsync();


            SetStatus(T("Signed in.", "Giriş yapıldı.", "Sesión iniciada.", "Connexion réussie.", "Angemeldet.", "로그인했습니다.", "Вход выполнен."));


            Debug.Log(
                "ATB_ACCOUNT_AUTH action=signin success=true uid=" +
                ShortUid(result.AccountId));


            bool career = openCareerAfterAuthentication;
            openCareerAfterAuthentication = false;
            modalBackdrop.SetActive(false);


            if (career)
                InvokeProgressionOpenCareer();
        }
        finally
        {
            ApplyBusyState(false);
        }
    }


    private async Task RegisterAsync()
    {
        if (busy)
            return;


        ResolveRuntimeReferences();


        if (accountService == null)
        {
            SetStatus(ServiceUnavailable());
            return;
        }


        string displayName = registerName?.text?.Trim() ?? string.Empty;
        string email = registerEmail?.text?.Trim() ?? string.Empty;
        string password = registerPassword?.text ?? string.Empty;
        string repeat = registerPasswordRepeat?.text ?? string.Empty;


        if (password != repeat)
        {
            SetStatus(T("Passwords do not match.", "Şifreler eşleşmiyor.", "Las contraseñas no coinciden.", "Les mots de passe ne correspondent pas.", "Passwörter stimmen nicht überein.", "비밀번호가 일치하지 않습니다.", "Пароли не совпадают."));
            return;
        }


        if (!int.TryParse(
                captchaAnswer?.text?.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int answer) ||
            answer != captchaExpected)
        {
            SetStatus(T("Math check is incorrect.", "Matematik kontrolü yanlış.", "La comprobación matemática es incorrecta.", "Le calcul est incorrect.", "Die Rechenprüfung ist falsch.", "수학 확인 답이 올바르지 않습니다.", "Ответ на математическую проверку неверен."));
            GenerateCaptcha();
            if (captchaAnswer != null)
                captchaAnswer.text = string.Empty;
            return;
        }


        string language =
            AtlasBoardLocalizationManager.Instance != null
                ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
                : "en";


        ApplyBusyState(true);


        try
        {
            AtlasAccountOperationResult result =
                await accountService.RegisterWithEmailPasswordAsync(
                    email,
                    password,
                    displayName,
                    "ZZ",
                    language);


            if (!result.Success)
            {
                SetStatus(FriendlyError(result));
                GenerateCaptcha();
                return;
            }


            loadedUid = string.Empty;
            canonicalDisplayName = string.Empty;
            await ForceLoadCanonicalIdentityAsync();


            Debug.Log(
                "ATB_ACCOUNT_AUTH action=register success=true uid=" +
                ShortUid(result.AccountId) +
                " passwordStoredByAtlasBoard=false");


            bool career = openCareerAfterAuthentication;
            openCareerAfterAuthentication = false;
            modalBackdrop.SetActive(false);


            if (career)
                InvokeProgressionOpenCareer();
        }
        finally
        {
            ApplyBusyState(false);
        }
    }


    private async Task SendPasswordResetAsync()
    {
        if (busy)
            return;


        ResolveRuntimeReferences();


        string email = loginEmail?.text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
        {
            SetStatus(T("Enter your email first.", "Önce e-posta adresini gir.", "Introduce primero tu correo.", "Saisissez d’abord votre e-mail.", "Zuerst E-Mail eingeben.", "먼저 이메일을 입력하세요.", "Сначала введите эл. почту."));
            return;
        }


        if (accountService == null)
        {
            SetStatus(ServiceUnavailable());
            return;
        }


        ApplyBusyState(true);


        try
        {
            await accountService.EnsureInitializedAsync();


            FirebaseAuth auth = FirebaseAuth.DefaultInstance;
            if (auth == null)
            {
                SetStatus(ServiceUnavailable());
                return;
            }


            await auth.SendPasswordResetEmailAsync(email);


            SetStatus(T("Password reset email sent.", "Şifre sıfırlama e-postası gönderildi.", "Correo de restablecimiento enviado.", "E-mail de réinitialisation envoyé.", "E-Mail zum Zurücksetzen wurde gesendet.", "비밀번호 재설정 이메일을 보냈습니다.", "Письмо для сброса пароля отправлено."));


            Debug.Log(
                "ATB_ACCOUNT_AUTH action=password_reset requested=true");
        }
        catch (Exception error)
        {
            Debug.LogWarning(
                "ATB_ACCOUNT_AUTH action=password_reset success=false type=" +
                error.GetType().Name);


            SetStatus(T("Could not send the reset email. Check the address and connection.", "Sıfırlama e-postası gönderilemedi. Adresi ve bağlantıyı kontrol et.", "No se pudo enviar el correo. Comprueba la dirección y la conexión.", "Impossible d’envoyer l’e-mail. Vérifiez l’adresse et la connexion.", "E-Mail konnte nicht gesendet werden. Adresse und Verbindung prüfen.", "재설정 이메일을 보낼 수 없습니다. 주소와 연결을 확인하세요.", "Не удалось отправить письмо. Проверьте адрес и соединение."));
        }
        finally
        {
            ApplyBusyState(false);
        }
    }


    private async Task RenameAsync()
    {
        if (busy ||
            accountService == null ||
            !accountService.IsSignedIn)
        {
            return;
        }


        string uid = accountService.CurrentAccountId ?? string.Empty;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long nextAllowed = ReadRenameNextAllowed(uid);


        if (nextAllowed > now)
        {
            SetStatus(BuildRenameCooldownText());
            return;
        }


        string name = renameInput?.text?.Trim() ?? string.Empty;
        if (name.Length < 2 || name.Length > 32)
        {
            SetStatus(T("Name must be 2–32 characters.", "Ad 2–32 karakter olmalı.", "El nombre debe tener 2–32 caracteres.", "Le nom doit comporter 2 à 32 caractères.", "Der Name muss 2–32 Zeichen lang sein.", "이름은 2~32자여야 합니다.", "Имя должно содержать 2–32 символа."));
            return;
        }


        ApplyBusyState(true);


        try
        {
            AtlasAccountSnapshot snapshot =
                await accountService.LoadCurrentAccountAsync();


            if (snapshot == null)
            {
                SetStatus(ServiceUnavailable());
                return;
            }


            AtlasAccountOperationResult result =
                await accountService.UpdatePublicProfileAsync(
                    name,
                    snapshot.AvatarId,
                    snapshot.ProfileFrameId);


            if (!result.Success)
            {
                SetStatus(
                    result.ErrorCode == AtlasAccountErrorCode.PermissionDenied
                        ? T("Display name can be changed once every 24 hours.", "Oyuncu adı 24 saatte bir değiştirilebilir.", "El nombre se puede cambiar una vez cada 24 horas.", "Le nom peut être modifié une fois toutes les 24 heures.", "Der Name kann einmal alle 24 Stunden geändert werden.", "표시 이름은 24시간에 한 번 변경할 수 있습니다.", "Имя можно менять один раз в 24 часа.")
                        : FriendlyError(result));
                return;
            }


            WriteRenameNextAllowed(
                uid,
                now + RenameCooldownMilliseconds);


            canonicalDisplayName = name;
            loadedUid = uid;
            ApplyIdentityPresentation();


            await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync(
                canonicalDisplayName,
                force: true);


            AtlasBoardCanonicalProfileHeader header =
                UnityEngine.Object.FindAnyObjectByType<
                    AtlasBoardCanonicalProfileHeader>(
                        FindObjectsInactive.Include);
            header?.RefreshNow();


            SetStatus(T("Display name updated.", "Oyuncu adı güncellendi.", "Nombre actualizado.", "Nom mis à jour.", "Name aktualisiert.", "표시 이름이 변경되었습니다.", "Имя обновлено."));


            Debug.Log(
                "ATB_ACCOUNT_PROFILE rename=success cooldownHours=24 uid=" +
                ShortUid(uid));


            RebuildCurrentView();
        }
        finally
        {
            ApplyBusyState(false);
        }
    }


    private void SignOut()
    {
        if (busy || accountService == null)
            return;


        accountService.SignOut();
        loadedUid = string.Empty;
        canonicalDisplayName = string.Empty;
        ApplyIdentityPresentation();
        modalBackdrop.SetActive(false);


        Debug.Log(
            "ATB_ACCOUNT_AUTH action=signout success=true guest=" +
            AtlasBoardGuestIdentity.DisplayName);
    }


    private async Task ForceLoadCanonicalIdentityAsync()
    {
        if (accountService == null ||
            !accountService.IsSignedIn)
        {
            return;
        }


        AtlasAccountSnapshot snapshot =
            await accountService.LoadCurrentAccountAsync();


        if (snapshot == null ||
            string.IsNullOrWhiteSpace(snapshot.DisplayName))
        {
            return;
        }


        loadedUid = snapshot.AccountId ?? accountService.CurrentAccountId;
        canonicalDisplayName = snapshot.DisplayName.Trim();
        ApplyIdentityPresentation();


        await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync(
            canonicalDisplayName,
            force: true);
    }


    private void GenerateCaptcha()
    {
        captchaA = UnityEngine.Random.Range(2, 10);
        captchaB = UnityEngine.Random.Range(1, 10);
        captchaAddition = UnityEngine.Random.value >= 0.28f;


        if (!captchaAddition && captchaB > captchaA)
        {
            int temp = captchaA;
            captchaA = captchaB;
            captchaB = temp;
        }


        captchaExpected =
            captchaAddition
                ? captchaA + captchaB
                : captchaA - captchaB;


        if (captchaQuestion != null)
        {
            captchaQuestion.text =
                captchaA +
                (captchaAddition ? " + " : " − ") +
                captchaB +
                " = ?";
        }
    }


    private void ApplyBusyState(bool value)
    {
        busy = value;
        busyDots = 0;
        busyAnimationAt = 0f;


        if (busyText != null)
            busyText.gameObject.SetActive(value);


        foreach (Button button in activeFormButtons)
        {
            if (button != null)
                button.interactable = !value;
        }
    }


    private bool IsSignedIn()
    {
        return accountService != null &&
               accountService.IsSignedIn;
    }


    private string FriendlyError(
        AtlasAccountOperationResult result)
    {
        if (result == null)
            return ServiceUnavailable();


        switch (result.ErrorCode)
        {
            case AtlasAccountErrorCode.EmailAlreadyInUse:
                return T("That email is already in use.", "Bu e-posta zaten kullanılıyor.", "Ese correo ya está en uso.", "Cet e-mail est déjà utilisé.", "Diese E-Mail wird bereits verwendet.", "이미 사용 중인 이메일입니다.", "Эта эл. почта уже используется.");
            case AtlasAccountErrorCode.WeakPassword:
                return T("Use a stronger password (at least 6 characters).", "Daha güçlü bir şifre kullan (en az 6 karakter).", "Usa una contraseña más segura (mínimo 6 caracteres).", "Utilisez un mot de passe plus fort (au moins 6 caractères).", "Verwende ein stärkeres Passwort (mindestens 6 Zeichen).", "더 강한 비밀번호를 사용하세요(최소 6자).", "Используйте более надежный пароль (минимум 6 символов).");
            case AtlasAccountErrorCode.InvalidCredentials:
                return T("Email or password is incorrect.", "E-posta veya şifre yanlış.", "Correo o contraseña incorrectos.", "E-mail ou mot de passe incorrect.", "E-Mail oder Passwort ist falsch.", "이메일 또는 비밀번호가 올바르지 않습니다.", "Неверная эл. почта или пароль.");
            case AtlasAccountErrorCode.TooManyRequests:
                return T("Too many attempts. Try again later.", "Çok fazla deneme yapıldı. Daha sonra tekrar dene.", "Demasiados intentos. Inténtalo más tarde.", "Trop de tentatives. Réessayez plus tard.", "Zu viele Versuche. Später erneut versuchen.", "시도가 너무 많습니다. 나중에 다시 시도하세요.", "Слишком много попыток. Повторите позже.");
            case AtlasAccountErrorCode.Network:
                return T("Network connection is unavailable.", "Ağ bağlantısı kullanılamıyor.", "La conexión de red no está disponible.", "La connexion réseau est indisponible.", "Netzwerkverbindung nicht verfügbar.", "네트워크 연결을 사용할 수 없습니다.", "Сетевое соединение недоступно.");
            case AtlasAccountErrorCode.PermissionDenied:
                return T("This change is not allowed right now.", "Bu değişikliğe şu anda izin verilmiyor.", "Este cambio no está permitido ahora.", "Cette modification n’est pas autorisée pour le moment.", "Diese Änderung ist derzeit nicht erlaubt.", "지금은 이 변경이 허용되지 않습니다.", "Это изменение сейчас недоступно.");
            default:
                return T("Account request could not be completed.", "Hesap işlemi tamamlanamadı.", "No se pudo completar la solicitud de cuenta.", "La demande de compte n’a pas pu être effectuée.", "Kontoanfrage konnte nicht abgeschlossen werden.", "계정 요청을 완료할 수 없습니다.", "Не удалось выполнить запрос аккаунта.");
        }
    }


    private string ServiceUnavailable()
    {
        return T("Account service is not ready. Try again shortly.", "Hesap servisi hazır değil. Kısa süre sonra tekrar dene.", "El servicio de cuenta no está listo. Inténtalo de nuevo en breve.", "Le service de compte n’est pas prêt. Réessayez bientôt.", "Kontodienst ist noch nicht bereit. Bitte gleich erneut versuchen.", "계정 서비스가 아직 준비되지 않았습니다. 잠시 후 다시 시도하세요.", "Сервис аккаунтов еще не готов. Повторите чуть позже.");
    }


    private string BuildRenameCooldownText()
    {
        if (accountService == null ||
            !accountService.IsSignedIn)
        {
            return string.Empty;
        }


        long next =
            ReadRenameNextAllowed(
                accountService.CurrentAccountId);
        long now =
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();


        if (next <= now)
        {
            return T("Display name can be changed once every 24 hours.", "Oyuncu adı 24 saatte bir değiştirilebilir.", "El nombre se puede cambiar una vez cada 24 horas.", "Le nom peut être modifié une fois toutes les 24 heures.", "Der Name kann einmal alle 24 Stunden geändert werden.", "표시 이름은 24시간에 한 번 변경할 수 있습니다.", "Имя можно менять один раз в 24 часа.");
        }


        TimeSpan remaining =
            TimeSpan.FromMilliseconds(next - now);


        int hours = Mathf.Max(0, Mathf.CeilToInt((float)remaining.TotalHours));


        return T("Name change available in {0}h.", "Ad değişikliği {0} saat sonra kullanılabilir.", "Cambio de nombre disponible en {0} h.", "Changement de nom disponible dans {0} h.", "Namensänderung in {0} Std. verfügbar.", "이름 변경은 {0}시간 후 가능합니다.", "Смена имени будет доступна через {0} ч.", hours);
    }


    private static long ReadRenameNextAllowed(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return 0L;


        string raw = PlayerPrefs.GetString(
            "atlasboard.account.rename.next." + uid,
            "0");


        return long.TryParse(
                   raw,
                   NumberStyles.Integer,
                   CultureInfo.InvariantCulture,
                   out long value)
            ? value
            : 0L;
    }


    private static void WriteRenameNextAllowed(
        string uid,
        long value)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return;


        PlayerPrefs.SetString(
            "atlasboard.account.rename.next." + uid,
            value.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }


    private void SetStatus(string value)
    {
        if (statusText != null)
            statusText.text = value ?? string.Empty;
    }


    private void CreateFieldLabel(
        string name,
        string value,
        float y)
    {
        TMP_Text label = CreateText(
            formRoot.transform,
            name,
            18f,
            FontStyles.Bold,
            TextAlignmentOptions.Left,
            new Color32(55, 62, 72, 255));
        SetAnchored(
            label.rectTransform,
            new Vector2(0f, y),
            new Vector2(700f, 34f));
        label.text = value;
    }


    private TMP_InputField CreateInput(
        string name,
        float y,
        bool password,
        bool email)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(TMP_InputField));
        root.transform.SetParent(formRoot.transform, false);


        RectTransform rect = root.GetComponent<RectTransform>();
        SetAnchored(
            rect,
            new Vector2(0f, y),
            new Vector2(700f, 64f));


        Image image = root.GetComponent<Image>();
        image.color = Color.white;


        GameObject viewport =
            new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(RectMask2D));
        viewport.transform.SetParent(root.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect, 14f, 8f);


        TMP_Text text = CreateText(
            viewport.transform,
            "Text",
            22f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft,
            new Color32(35, 40, 48, 255));
        Stretch(text.rectTransform, 0f, 0f);
        text.textWrappingMode = TextWrappingModes.NoWrap;


        TMP_Text placeholder = CreateText(
            viewport.transform,
            "Placeholder",
            20f,
            FontStyles.Italic,
            TextAlignmentOptions.MidlineLeft,
            new Color32(130, 135, 143, 255));
        Stretch(placeholder.rectTransform, 0f, 0f);
        placeholder.text = email
            ? "name@example.com"
            : password
                ? "••••••••"
                : string.Empty;


        TMP_InputField input = root.GetComponent<TMP_InputField>();
        input.targetGraphic = image;
        input.textViewport = viewportRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = password
            ? TMP_InputField.ContentType.Password
            : email
                ? TMP_InputField.ContentType.EmailAddress
                : TMP_InputField.ContentType.Standard;


        return input;
    }


    private Button CreateFormButton(
        string name,
        string label,
        Color color,
        Vector2 position,
        Vector2 size)
    {
        Button button = CreateButton(
            formRoot.transform,
            name,
            label,
            color,
            position,
            size,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f));


        activeFormButtons.Add(button);
        return button;
    }


    private static GameObject CreatePanel(
        Transform parent,
        string name,
        Color color)
    {
        GameObject panel =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));
        panel.transform.SetParent(parent, false);


        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;


        panel.GetComponent<Image>().color = color;
        return panel;
    }


    private static TMP_Text CreateText(
        Transform parent,
        string name,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject item =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
        item.transform.SetParent(parent, false);


        TMP_Text text = item.GetComponent<TMP_Text>();
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;


        return text;
    }


    private static Button CreateButton(
        Transform parent,
        string name,
        string label,
        Color color,
        Vector2 position,
        Vector2 size,
        Vector2 anchor,
        Vector2 pivot)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
        root.transform.SetParent(parent, false);


        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;


        Image image = root.GetComponent<Image>();
        image.color = color;


        Button button = root.GetComponent<Button>();
        button.targetGraphic = image;


        TMP_Text text = CreateText(
            root.transform,
            "Label",
            22f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white);
        Stretch(text.rectTransform, 8f, 6f);
        text.text = label;
        text.enableAutoSizing = true;
        text.fontSizeMin = 13f;
        text.fontSizeMax = 24f;


        return button;
    }


    private static void SetAnchored(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }


    private static void Stretch(
        RectTransform rect,
        float horizontal,
        float vertical)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
    }


    private static Button FindSceneButton(string name)
    {
        Button[] all = Resources.FindObjectsOfTypeAll<Button>();


        foreach (Button button in all)
        {
            if (button != null &&
                button.gameObject.scene.IsValid() &&
                string.Equals(
                    button.gameObject.name,
                    name,
                    StringComparison.Ordinal))
            {
                return button;
            }
        }


        return null;
    }


    private static bool IsPrivateRootVisible(
        object owner,
        string fieldName)
    {
        GameObject root =
            ReadPrivateField<GameObject>(
                owner,
                fieldName);


        return root != null &&
               root.activeInHierarchy;
    }


    private static string GetInitial(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "G";


        string element =
            StringInfo.GetNextTextElement(
                value.Trim());


        return string.IsNullOrWhiteSpace(element)
            ? "G"
            : element.ToUpperInvariant();
    }


    private static string ShortUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return "none";


        return uid.Length <= 6
            ? uid
            : uid.Substring(0, 6) + "…";
    }


    private static Transform FindChildRecursive(
        Transform root,
        string childName)
    {
        if (root == null)
            return null;


        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);


            if (string.Equals(
                    child.name,
                    childName,
                    StringComparison.Ordinal))
            {
                return child;
            }


            Transform nested =
                FindChildRecursive(
                    child,
                    childName);


            if (nested != null)
                return nested;
        }


        return null;
    }


    private static T ReadPrivateField<T>(
        object owner,
        string fieldName)
    {
        if (owner == null)
            return default;


        FieldInfo field = owner.GetType().GetField(
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


        FieldInfo field = owner.GetType().GetField(
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
                "ATB_ACCOUNT_UI field_write_failed field=" +
                fieldName +
                " type=" + error.GetType().Name);
        }
    }


    private static string T(
        string en,
        string tr,
        string es,
        string fr,
        string de,
        string ko,
        string ru,
        params object[] args)
    {
        return AtlasBoardL.R(
            en,
            tr,
            es,
            fr,
            de,
            ko,
            ru,
            args);
    }
}

