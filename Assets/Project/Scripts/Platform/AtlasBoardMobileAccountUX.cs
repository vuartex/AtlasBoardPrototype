using System;
using System.Reflection;
using System.Threading.Tasks;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


/// <summary>
/// Mobile-only, additive account usability fixes. Does not change Windows/Steam,
/// auth persistence, Firebase rules, network transports or stored credentials.
/// All visual nodes are runtime-only and re-resolved after UI/scene rebuilds.
/// </summary>
[DefaultExecutionOrder(15100)]
public sealed class AtlasBoardMobileAccountUX : MonoBehaviour
{
    private static AtlasBoardMobileAccountUX instance;
    private float nextScan;
    private Canvas accountCanvas;
    private RectTransform accountWindow;
    private RectTransform focusBar;
    private TMP_Text focusText;
    private Button focusNext;
    private GameObject passwordOverlay;
    private TMP_InputField oldPassword;
    private TMP_InputField newPassword;
    private TMP_InputField confirmPassword;
    private TMP_Text passwordStatus;
    private Button passwordSubmit;
    private bool passwordBusy;
    private TMP_InputField lastFocusedInput;
    private static Sprite eyeVisibleSprite;
    private static Sprite eyeHiddenSprite;


    // Compile under Unity Editor for preflight; instantiate only on real mobile.
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;
        var host = new GameObject("AtlasBoard_Mobile_Account_UX");
        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardMobileAccountUX>();
    }
#endif


    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }


    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }


    private void Update()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.70f;
            RepairAccountUi();
        }
        UpdateKeyboardFocus();
    }


    private static T Find<T>(string objectName) where T : Component
    {
        T[] items = Resources.FindObjectsOfTypeAll<T>();
        foreach (T item in items)
            if (item != null && item.gameObject.scene.IsValid() &&
                string.Equals(item.gameObject.name, objectName, StringComparison.Ordinal))
                return item;
        return null;
    }


    private static TMP_InputField FindInput(Transform root, string name)
    {
        if (root == null)
            return null;
        TMP_InputField[] items = root.GetComponentsInChildren<TMP_InputField>(true);
        foreach (TMP_InputField field in items)
            if (field != null && field.name == name)
                return field;
        return null;
    }


    private void RepairAccountUi()
    {
        Canvas canvas = Find<Canvas>("AtlasBoard_Account_Access_Canvas");
        RectTransform window = Find<RectTransform>("AccountWindow");
        if (canvas == null || window == null)
            return;
        accountCanvas = canvas;
        if (accountWindow != window)
        {
            accountWindow = window;
            focusBar = null;
            focusText = null;
            focusNext = null;
            passwordOverlay = null;
            oldPassword = newPassword = confirmPassword = null;
        }


        Transform form = accountWindow.Find("FormRoot");
        if (form != null)
        {
            SetupPasswordVisibility(FindInput(form, "LoginPassword"));
            SetupPasswordVisibility(FindInput(form, "RegisterPassword"));
            SetupPasswordVisibility(FindInput(form, "RegisterPasswordRepeat"));
            bool register = FindInput(form, "RegisterPasswordRepeat") != null;
            RepairFormStatus(register);
            EnsureChangePasswordButton(form);
        }
        // Shared Account tab layout is owned by AtlasBoardUniversalCareerTheme
        // on every platform, never by this mobile-only helper.
        RepairCareerPlaceholder();
        EnsureFocusBar();
    }


    private static void SetupPasswordVisibility(TMP_InputField field)
    {
        if (field == null)
            return;


        if (field.placeholder != null)
        {
            TMP_Text placeholder = field.placeholder as TMP_Text;
            if (placeholder != null && placeholder.text != string.Empty)
                placeholder.text = string.Empty;
        }
        if (field.transform.Find("MobilePasswordEye") != null)
            return;


        // Keep the editable text clear of the eye target.
        if (field.textViewport != null)
        {
            var offset = field.textViewport.offsetMax;
            field.textViewport.offsetMax = new Vector2(Mathf.Min(offset.x, -108f), offset.y);
        }


        Button button = MakeButton(field.transform, "MobilePasswordEye", "SHOW",
            new Vector2(1f, 0.5f), new Vector2(-8f, 0f),
            new Vector2(98f, 48f), new Color32(38, 112, 157, 255));
        var rect = button.transform as RectTransform;
        rect.pivot = new Vector2(1f, 0.5f);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.fontSize = 14f;
            label.rectTransform.anchoredPosition = new Vector2(20f, 0f);
            label.rectTransform.sizeDelta = new Vector2(52f, 44f);
        }
        GameObject iconObject = new GameObject("EyeGlyph", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(button.transform, false);
        RectTransform iconRect = iconObject.transform as RectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(38f, 28f);
        iconRect.anchoredPosition = new Vector2(-25f, 0f);
        Image glyph = iconObject.GetComponent<Image>();
        glyph.sprite = GetEyeSprite(false);
        glyph.raycastTarget = false;
        button.onClick.AddListener(() =>
        {
            if (field == null)
                return;
            bool reveal = field.contentType == TMP_InputField.ContentType.Password;
            field.contentType = reveal
                ? TMP_InputField.ContentType.Standard
                : TMP_InputField.ContentType.Password;
            field.ForceLabelUpdate();
            if (label != null)
                label.text = reveal ? "HIDE" : "SHOW";
            if (glyph != null) glyph.sprite = GetEyeSprite(reveal);
            // Never log or persist input text.
        });
    }


    private static Sprite GetEyeSprite(bool visible)
    {
        if (visible && eyeVisibleSprite != null) return eyeVisibleSprite;
        if (!visible && eyeHiddenSprite != null) return eyeHiddenSprite;
        const int width = 48, height = 32;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float dx = (x - 23.5f) / 22f;
                float top = 16f + 10f * Mathf.Sqrt(Mathf.Max(0f, 1f - dx * dx));
                float bottom = 16f - 10f * Mathf.Sqrt(Mathf.Max(0f, 1f - dx * dx));
                bool contour = Mathf.Abs(dx) <= 1f &&
                    (Mathf.Abs(y - top) < 1.5f || Mathf.Abs(y - bottom) < 1.5f);
                float px = x - 23.5f, py = y - 16f;
                bool pupil = px * px + py * py < 23f;
                bool slash = !visible && Mathf.Abs(y - (30f - x * 0.61f)) < 1.8f;
                pixels[y * width + x] = contour || pupil || slash
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture,
            new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f));
        if (visible) eyeVisibleSprite = sprite;
        else eyeHiddenSprite = sprite;
        return sprite;
    }


    private void RepairFormStatus(bool register)
    {
        TMP_Text status = null;
        TMP_Text busy = null;
        foreach (TMP_Text text in accountWindow.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.transform.parent != accountWindow)
                continue;
            if (text.name == "Status") status = text;
            if (text.name == "Busy") busy = text;
        }
        if (status != null)
        {
            RectTransform rect = status.rectTransform;
            rect.anchoredPosition = new Vector2(0f, -303f);
            rect.sizeDelta = new Vector2(840f, 42f);
            status.fontSize = register ? 18f : 19f;
            status.enableAutoSizing = true;
            status.fontSizeMin = 15f;
            status.fontSizeMax = register ? 18f : 19f;
            status.textWrappingMode = TextWrappingModes.Normal;
        }
        if (busy != null)
        {
            busy.rectTransform.anchoredPosition = new Vector2(0f, register ? -246f : -243f);
            busy.rectTransform.sizeDelta = new Vector2(550f, 40f);
        }
    }


    private void EnsureChangePasswordButton(Transform form)
    {
        Button rename = Find<Button>("Button_Rename");
        if (rename == null || rename.transform.parent != form ||
            form.Find("Button_ChangePassword") != null)
            return;


        Button button = Instantiate(rename, form, false);
        button.name = "Button_ChangePassword";
        RectTransform rect = button.transform as RectTransform;
        rect.anchoredPosition = new Vector2(0f, -221f);
        rect.sizeDelta = new Vector2(360f, 63f);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = AtlasBoardL.R("CHANGE PASSWORD", "ŞİFRE DEĞİŞTİR", "CAMBIAR CONTRASEÑA", "CHANGER LE MOT DE PASSE", "PASSWORT ÄNDERN", "비밀번호 변경", "СМЕНИТЬ ПАРОЛЬ");
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(OpenPasswordOverlay);


        Button signOut = Find<Button>("Button_SignOut");
        if (signOut != null && signOut.transform.parent == form)
        {
            var signOutRect = signOut.transform as RectTransform;
            if (signOutRect != null)
                signOutRect.anchoredPosition = new Vector2(0f, -335f);
        }
    }


    private void OpenPasswordOverlay()
    {
        if (passwordBusy || accountWindow == null)
            return;
        if (passwordOverlay != null)
            Destroy(passwordOverlay);


        GameObject root = new GameObject("MobileChangePasswordOverlay",
            typeof(RectTransform), typeof(Image));
        root.transform.SetParent(accountWindow, false);
        RectTransform panel = root.transform as RectTransform;
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = new Vector2(30f, 30f);
        panel.offsetMax = new Vector2(-30f, -110f);
        root.GetComponent<Image>().color = new Color32(247, 246, 241, 255);
        passwordOverlay = root;


        MakeText(root.transform, "PasswordChangeHeading", AtlasBoardL.R("CHANGE PASSWORD", "ŞİFRE DEĞİŞTİR", "CAMBIAR CONTRASEÑA", "CHANGER DE MOT DE PASSE", "PASSWORT ÄNDERN", "비밀번호 변경", "СМЕНИТЬ ПАРОЛЬ"), 27f,
            new Vector2(0f, 250f), new Vector2(700f, 45f));
        MakeText(root.transform, "OldPasswordLabel", AtlasBoardL.R("CURRENT PASSWORD", "MEVCUT ŞİFRE", "CONTRASEÑA ACTUAL", "MOT DE PASSE ACTUEL", "AKTUELLES PASSWORT", "현재 비밀번호", "ТЕКУЩИЙ ПАРОЛЬ"), 18f,
            new Vector2(0f, 186f), new Vector2(700f, 34f));
        oldPassword = MakePasswordField(root.transform, "ChangeOldPassword", 135f);
        MakeText(root.transform, "NewPasswordLabel", AtlasBoardL.R("NEW PASSWORD", "YENİ ŞİFRE", "NUEVA CONTRASEÑA", "NOUVEAU MOT DE PASSE", "NEUES PASSWORT", "새 비밀번호", "НОВЫЙ ПАРОЛЬ"), 18f,
            new Vector2(0f, 75f), new Vector2(700f, 34f));
        newPassword = MakePasswordField(root.transform, "ChangeNewPassword", 23f);
        MakeText(root.transform, "ConfirmPasswordLabel", AtlasBoardL.R("CONFIRM NEW PASSWORD", "YENİ ŞİFRE TEKRAR", "CONFIRMAR CONTRASEÑA", "CONFIRMER LE MOT DE PASSE", "PASSWORT BESTÄTIGEN", "새 비밀번호 확인", "ПОВТОРИТЕ НОВЫЙ ПАРОЛЬ"), 18f,
            new Vector2(0f, -37f), new Vector2(700f, 34f));
        confirmPassword = MakePasswordField(root.transform, "ChangeConfirmPassword", -89f);


        passwordStatus = MakeText(root.transform, "PasswordChangeStatus", "", 17f,
            new Vector2(0f, -165f), new Vector2(770f, 55f));
        passwordSubmit = MakeButton(root.transform, "Button_SubmitPasswordChange", AtlasBoardL.R("SAVE PASSWORD", "ŞİFREYİ KAYDET", "GUARDAR CONTRASEÑA", "ENREGISTRER", "PASSWORT SPEICHERN", "비밀번호 저장", "СОХРАНИТЬ ПАРОЛЬ"),
            new Vector2(0.5f, 0.5f), new Vector2(-185f, -251f),
            new Vector2(320f, 64f), new Color32(32, 132, 180, 255));
        passwordSubmit.onClick.AddListener(() => _ = UpdatePasswordAsync());
        Button cancel = MakeButton(root.transform, "Button_CancelPasswordChange", AtlasBoardL.R("CANCEL", "İPTAL", "CANCELAR", "ANNULER", "ABBRECHEN", "취소", "ОТМЕНА"),
            new Vector2(0.5f, 0.5f), new Vector2(196f, -251f),
            new Vector2(280f, 64f), new Color32(98, 106, 117, 255));
        cancel.onClick.AddListener(() => { if (!passwordBusy && passwordOverlay != null) Destroy(passwordOverlay); });
        SetupPasswordVisibility(oldPassword);
        SetupPasswordVisibility(newPassword);
        SetupPasswordVisibility(confirmPassword);
    }


    private async Task UpdatePasswordAsync()
    {
        if (passwordBusy || oldPassword == null || newPassword == null || confirmPassword == null)
            return;
        string oldText = oldPassword.text;
        string newText = newPassword.text;
        string repeat = confirmPassword.text;
        if (string.IsNullOrEmpty(oldText) || newText.Length < 6 || newText != repeat)
        {
            SetPasswordStatus("Check current password, new password (6+ characters), and confirmation.");
            return;
        }
        passwordBusy = true;
        if (passwordSubmit != null) passwordSubmit.interactable = false;
        SetPasswordStatus("Updating password...");
        try
        {
            FirebaseAuth auth = FirebaseAuth.DefaultInstance;
            FirebaseUser user = auth != null ? auth.CurrentUser : null;
            if (user == null || string.IsNullOrEmpty(user.Email))
            {
                SetPasswordStatus("Sign in with your email account first.");
                return;
            }
            Credential credential = EmailAuthProvider.GetCredential(user.Email, oldText);
            await user.ReauthenticateAsync(credential);
            await user.UpdatePasswordAsync(newText);
            SetPasswordStatus("Password updated successfully.");
            if (oldPassword != null) oldPassword.text = string.Empty;
            if (newPassword != null) newPassword.text = string.Empty;
            if (confirmPassword != null) confirmPassword.text = string.Empty;
            Debug.Log("ATB_ACCOUNT_PASSWORD_CHANGE success=true secretLogged=false");
        }
        catch (Exception exception)
        {
            Debug.LogWarning("ATB_ACCOUNT_PASSWORD_CHANGE failedType=" + exception.GetType().Name);
            SetPasswordStatus("Could not change password. Verify your current password and connection.");
        }
        finally
        {
            oldText = newText = repeat = null;
            passwordBusy = false;
            if (passwordSubmit != null) passwordSubmit.interactable = true;
        }
    }


    private void SetPasswordStatus(string text)
    {
        if (passwordStatus != null) passwordStatus.text = text;
    }


    private void RepairCareerPlaceholder()
    {
        // Phase17F shares Career empty-state and verified RTDB read model
        // across ALL platforms. Do not draw a second mobile-only warning
        // over the common data status, or clear its signed-in snapshot.
        RectTransform career = Find<RectTransform>("AtlasCareer");
        if (career == null)
            return;
        Transform content = career.Find("ContentPanel");
        Transform old = content != null ? content.Find("MobileCareerDataNotice") : null;
        if (old != null && old.gameObject.activeSelf)
            old.gameObject.SetActive(false);
    }


    private void EnsureFocusBar()
    {
        if (accountCanvas == null || focusBar != null)
            return;
        GameObject host = new GameObject("MobileKeyboardFocusBar", typeof(RectTransform), typeof(Image));
        host.transform.SetParent(accountCanvas.transform, false);
        RectTransform rect = host.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -22f);
        rect.sizeDelta = new Vector2(960f, 76f);
        host.GetComponent<Image>().color = new Color32(23, 39, 54, 252);
        focusBar = rect;
        focusText = MakeText(host.transform, "MobileKeyboardFocusLabel", "", 21f,
            new Vector2(-82f, 0f), new Vector2(750f, 68f));
        focusText.color = Color.white;
        focusText.alignment = TextAlignmentOptions.MidlineLeft;
        focusNext = MakeButton(host.transform, "MobileKeyboardNext", "NEXT",
            new Vector2(0.5f, 0.5f), new Vector2(407f, 0f),
            new Vector2(130f, 56f), new Color32(39, 120, 174, 255));
        focusNext.onClick.AddListener(FocusNextField);
        host.SetActive(false);
    }


    private static readonly string[] RegisterFields = {
        "RegisterName", "RegisterEmail", "RegisterPassword", "RegisterPasswordRepeat", "CaptchaAnswer"
    };
    private static readonly string[] LoginFields = { "LoginEmail", "LoginPassword" };
    private static readonly string[] ChangeFields = { "ChangeOldPassword", "ChangeNewPassword", "ChangeConfirmPassword" };


    private void UpdateKeyboardFocus()
    {
        if (focusBar == null || accountWindow == null)
            return;
        EventSystem eventSystem = EventSystem.current;
        TMP_InputField input = eventSystem != null && eventSystem.currentSelectedGameObject != null
            ? eventSystem.currentSelectedGameObject.GetComponent<TMP_InputField>()
            : null;
        bool inside = input != null && input.transform.IsChildOf(accountWindow) &&
            accountWindow.gameObject.activeInHierarchy;
        if (focusBar.gameObject.activeSelf != inside)
            focusBar.gameObject.SetActive(inside);
        if (!inside)
            return;
        if (focusBar.GetSiblingIndex() != focusBar.parent.childCount - 1)
            focusBar.SetAsLastSibling();
        lastFocusedInput = input;


        string fieldName = input.gameObject.name;
        string friendly = fieldName.Replace("Register", "").Replace("Login", "")
            .Replace("Change", "").Replace("PasswordRepeat", "Confirm password")
            .Replace("CaptchaAnswer", "Math answer");
        bool isSecret = fieldName.IndexOf("Password", StringComparison.OrdinalIgnoreCase) >= 0;
        string value = input.text ?? string.Empty;
        if (isSecret && input.contentType == TMP_InputField.ContentType.Password)
            value = new string('•', Mathf.Min(30, value.Length));
        if (value.Length > 40) value = value.Substring(value.Length - 40);
        if (focusText != null)
            focusText.text = "EDITING: " + friendly + "   " + value;
        if (focusNext != null)
            focusNext.interactable = NextField(input) != null;
    }


    private TMP_InputField NextField(TMP_InputField selected)
    {
        if (selected == null || accountWindow == null)
            return null;
        string[] list = Array.IndexOf(RegisterFields, selected.name) >= 0
            ? RegisterFields
            : Array.IndexOf(LoginFields, selected.name) >= 0
                ? LoginFields
                : ChangeFields;
        int index = Array.IndexOf(list, selected.name);
        return index < 0 || index + 1 >= list.Length
            ? null
            : FindInput(accountWindow, list[index + 1]);
    }


    private void FocusNextField()
    {
        TMP_InputField input = lastFocusedInput;
        TMP_InputField next = NextField(input);
        if (next == null) return;
        if (input != null) input.DeactivateInputField();
        next.Select();
        next.ActivateInputField();
    }


    private static TMP_Text MakeText(Transform parent, string name, string content, float size,
        Vector2 position, Vector2 dimensions)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = go.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = position;
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.text = content;
        text.color = new Color32(45, 50, 60, 255);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }


    private static Button MakeButton(Transform parent, string name, string content,
        Vector2 anchor, Vector2 position, Vector2 dimensions, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        Image bg = go.GetComponent<Image>();
        bg.color = color;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = bg;
        TMP_Text title = MakeText(go.transform, "Label", content, 17f, Vector2.zero, dimensions);
        title.color = Color.white;
        title.fontStyle = FontStyles.Bold;
        title.enableAutoSizing = true;
        title.fontSizeMin = 12f;
        title.fontSizeMax = 19f;
        return button;
    }


    private static TMP_InputField MakePasswordField(Transform parent, string name, float y)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.transform as RectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(720f, 65f);
        go.GetComponent<Image>().color = Color.white;
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(go.transform, false);
        var viewRect = viewport.transform as RectTransform;
        viewRect.anchorMin = Vector2.zero; viewRect.anchorMax = Vector2.one;
        viewRect.offsetMin = new Vector2(14f, 8f);
        viewRect.offsetMax = new Vector2(-14f, -8f);
        TMP_Text text = MakeText(viewport.transform, "Text", "", 22f, Vector2.zero, Vector2.zero);
        var tr = text.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        TMP_InputField input = go.GetComponent<TMP_InputField>();
        input.targetGraphic = go.GetComponent<Image>();
        input.textViewport = viewRect;
        input.textComponent = text;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Password;
        return input;
    }
}

