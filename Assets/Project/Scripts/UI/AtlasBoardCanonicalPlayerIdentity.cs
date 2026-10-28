using System;
using System.Collections;
using System.Reflection;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Single display-only identity resolver for LOCAL player-facing surfaces.
/// Steam persona is not authentication proof, and no online player UID, seat
/// ownership, bot identity, or Firebase profile is mutated by this helper.
/// The menu Profile modal is rewritten on click, not polled/rebuilt per frame.
/// </summary>
[DisallowMultipleComponent]
public sealed class AtlasBoardCanonicalPlayerIdentity : MonoBehaviour
{
    private static string currentNameCache = string.Empty;
    public static string CurrentDisplayName => !string.IsNullOrWhiteSpace(currentNameCache)
        ? currentNameCache : Resolve();

    public static string ValidSteamPersona()
    {
        AtlasBoardPlatformRuntime runtime = AtlasBoardPlatformRuntime.Instance;
        return runtime != null && runtime.IsSteamProviderActive &&
            runtime.SteamInitialized && runtime.SteamId != 0 &&
            !string.IsNullOrWhiteSpace(runtime.SteamPersonaName)
            ? runtime.SteamPersonaName.Trim() : string.Empty;
    }

    private static readonly BindingFlags PrivateFields =
        BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo MenuNameField = typeof(AtlasBoardMainMenuController)
        .GetField("profileName", PrivateFields);
    private static readonly FieldInfo ModalBodyField = typeof(AtlasBoardMainMenuController)
        .GetField("modalBody", PrivateFields);
    private static readonly FieldInfo ProfileHeaderNameField = typeof(AtlasBoardCanonicalProfileHeader)
        .GetField("appliedDisplayName", PrivateFields);
    private static readonly FieldInfo ProfileHeaderAccountField = typeof(AtlasBoardCanonicalProfileHeader)
        .GetField("observedAccountId", PrivateFields);

    private static AtlasBoardCanonicalPlayerIdentity instance;
    private AtlasBoardMainMenuController menu;
    private Button profileButton;
    private TMP_Text mainCardName;
    private TMP_Text mainCardInitial;
    private TMP_Text lobbyCardName;
    private TMP_Text lobbyCardInitial;
    private TMP_Text modalBody;
    private AtlasBoardCanonicalProfileHeader canonicalHeader;
    private string lastApplied = string.Empty;
    private Coroutine watcher;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject root = new GameObject("AtlasBoard_Canonical_Local_Identity");
        DontDestroyOnLoad(root);
        root.AddComponent<AtlasBoardCanonicalPlayerIdentity>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        currentNameCache = string.Empty;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AtlasBoardLocalizationManager.LanguageChanged += OnLanguageChanged;
    }

    private void Start() => watcher = StartCoroutine(WatchIdentity());

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        AtlasBoardLocalizationManager.LanguageChanged -= OnLanguageChanged;
        if (watcher != null) StopCoroutine(watcher);
        if (profileButton != null) profileButton.onClick.RemoveListener(OnProfileClicked);
        if (instance == this) { instance = null; currentNameCache = string.Empty; }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Unbind();
        canonicalHeader = null;
    }

    private void OnLanguageChanged() => ApplyNameIfChanged(true);

    /// <summary>One cached profile value. No scene traversal occurs here.</summary>
    public static string Resolve()
    {
        AtlasBoardPlatformRuntime platform = AtlasBoardPlatformRuntime.Instance;
        if (platform != null && platform.IsSteamProviderActive)
        {
            string steam = ValidSteamPersona();
            if (!string.IsNullOrWhiteSpace(steam)) return steam;

            // SteamAPI.Init can fail in the Editor while the RTDB account is
            // already signed in and its server profile name is known. Do NOT
            // invent a Steam persona or replace that verified name with the
            // generic "Steam Player" placeholder. This is display-only:
            // Steam authentication state remains false until Steamworks works.
            AtlasBoardAccountService steamPendingAccount = AtlasBoardAccountService.Instance;
            if (steamPendingAccount != null && steamPendingAccount.IsSignedIn)
            {
                string canonical = instance != null
                    ? instance.ReadCanonicalHeaderName() : string.Empty;
                if (!string.IsNullOrWhiteSpace(canonical)) return canonical;
            }
            string pending = AtlasBoardL.T("menu.player");
            return string.IsNullOrWhiteSpace(pending) || pending == "menu.player"
                ? "Player" : pending;
        }
        AtlasBoardAccountService account = AtlasBoardAccountService.Instance;
        if (account != null && account.IsSignedIn)
        {
            try
            {
                FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
                // An Android anonymous Firebase UID is the sole Casual Guest
                // display-name source. A pre-existing Firebase displayName or
                // stale menu cache must not impersonate an admitted room seat.
                if (Application.platform == RuntimePlatform.Android &&
                    user != null && user.IsAnonymous &&
                    !string.IsNullOrWhiteSpace(user.UserId))
                    return AtlasBoardGuestIdentity.NameForAuthenticatedUid(user.UserId);
                if (user != null && !string.IsNullOrWhiteSpace(user.DisplayName))
                    return user.DisplayName.Trim();
                if (user != null && user.IsAnonymous &&
                    !string.IsNullOrWhiteSpace(user.UserId))
                    return AtlasBoardGuestIdentity.NameForAuthenticatedUid(user.UserId);
            }
            catch { /* Firebase SDK may be initializing. */ }
            // A signed-in Firebase UID is not the Guest account.
            // The existing profile header will load the verified display name.
            AtlasBoardCanonicalPlayerIdentity current = instance;
            string canonical = current != null ? current.ReadCanonicalHeaderName() : string.Empty;
            if (!string.IsNullOrWhiteSpace(canonical)) return canonical;
            return "Player";
        }
        return AtlasBoardGuestIdentity.DisplayName;
    }

    private string ReadCanonicalHeaderName()
    {
        AtlasBoardAccountService account = AtlasBoardAccountService.Instance;
        if (account == null || !account.IsSignedIn ||
            string.IsNullOrWhiteSpace(account.CurrentAccountId))
            return string.Empty;
        if (canonicalHeader == null)
            canonicalHeader = UnityEngine.Object.FindAnyObjectByType<AtlasBoardCanonicalProfileHeader>();
        if (canonicalHeader == null || ProfileHeaderNameField == null ||
            ProfileHeaderAccountField == null)
            return string.Empty;

        // A profile loaded for a prior Firebase UID must never be displayed
        // during an account change; the canonical header already tracks this.
        string observedUid = ProfileHeaderAccountField.GetValue(canonicalHeader) as string;
        if (!string.Equals(observedUid, account.CurrentAccountId, StringComparison.Ordinal))
            return string.Empty;
        return (ProfileHeaderNameField.GetValue(canonicalHeader) as string ?? string.Empty).Trim();
    }

    private IEnumerator WatchIdentity()
    {
        // Only the first missing controller triggers discovery; when bound,
        // reading Steam/Auth state and a cached TMP reference costs no searches.
        var delay = new WaitForSecondsRealtime(1f);
        while (true)
        {
            if (menu == null || profileButton == null) TryBind();
            currentNameCache = Resolve();
            ApplyNameIfChanged(false);
            yield return delay;
        }
    }

    private void TryBind()
    {
        var found = UnityEngine.Object.FindAnyObjectByType<AtlasBoardMainMenuController>(
            FindObjectsInactive.Include);
        if (found == null) return;
        if (menu != found) Unbind();
        menu = found;
        Transform mainMenu = menu.transform.Find("MainMenu");
        Transform card = mainMenu != null ? mainMenu.Find("ProfileCard") : null;
        if (card == null) return;
        Button button = card.GetComponent<Button>();
        if (button == null) return;
        if (profileButton != button)
        {
            if (profileButton != null) profileButton.onClick.RemoveListener(OnProfileClicked);
            profileButton = button;
            // Persistent OpenProfile() runs first, then correct its cached name.
            profileButton.onClick.AddListener(OnProfileClicked);
        }
        Transform text = card.Find("ProfileName");
        mainCardName = text != null ? text.GetComponent<TMP_Text>() : null;
        Transform avatar = card.Find("AvatarInitial");
        mainCardInitial = avatar != null ? avatar.GetComponent<TMP_Text>() : null;
        Transform lobby = menu.transform.Find("Lobby");
        Transform lobbyCard = lobby != null ? lobby.Find("ProfileCard") : null;
        Transform lobbyName = lobbyCard != null ? lobbyCard.Find("ProfileName") : null;
        Transform lobbyInitial = lobbyCard != null ? lobbyCard.Find("AvatarInitial") : null;
        lobbyCardName = lobbyName != null ? lobbyName.GetComponent<TMP_Text>() : null;
        lobbyCardInitial = lobbyInitial != null ? lobbyInitial.GetComponent<TMP_Text>() : null;
        modalBody = ModalBodyField != null ? ModalBodyField.GetValue(menu) as TMP_Text : null;
        lastApplied = string.Empty;
    }

    private void Unbind()
    {
        if (profileButton != null) profileButton.onClick.RemoveListener(OnProfileClicked);
        profileButton = null;
        mainCardName = null;
        mainCardInitial = null;
        lobbyCardName = null;
        lobbyCardInitial = null;
        modalBody = null;
        menu = null;
        lastApplied = string.Empty;
    }

    private void ApplyNameIfChanged(bool force)
    {
        if (menu == null) return;
        string name = CurrentDisplayName;
        if (string.IsNullOrWhiteSpace(name)) return;
        // Keep the four cached graphic references consistent with ONE alias.
        // Other legacy UI owners may repaint initials after an identity change;
        // repair only actual drift, without reloading the panel or scene.
        string initial = FirstInitial(name);
        // The existing shared profile presenter reads the canonical header's
        // presentation field every LateUpdate. Make it consume the same Steam
        // persona rather than continuously repainting a stale Guest initial.
        // This private field is PRESENTATION ONLY; Firebase Auth/RTDB data is
        // never modified here.
        if (!string.IsNullOrWhiteSpace(ValidSteamPersona()))
        {
            if (canonicalHeader == null)
                canonicalHeader = UnityEngine.Object.FindAnyObjectByType<AtlasBoardCanonicalProfileHeader>();
            if (canonicalHeader != null && ProfileHeaderNameField != null)
            {
                string shown = ProfileHeaderNameField.GetValue(canonicalHeader) as string;
                if (!string.Equals(shown, name, StringComparison.Ordinal))
                    ProfileHeaderNameField.SetValue(canonicalHeader, name);
            }
        }
        bool nameChanged = !string.Equals(lastApplied, name, StringComparison.Ordinal);
        if (nameChanged || force)
        {
            lastApplied = name;
            if (MenuNameField != null) MenuNameField.SetValue(menu, name);
        }
        SetIfDifferent(mainCardName, name);
        SetIfDifferent(mainCardInitial, initial);
        SetIfDifferent(lobbyCardName, name);
        SetIfDifferent(lobbyCardInitial, initial);
    }

    private static void SetIfDifferent(TMP_Text target, string value)
    {
        if (target != null && !string.Equals(target.text, value, StringComparison.Ordinal))
            target.text = value;
    }

    private static string FirstInitial(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "?";
        string trimmed = value.Trim();
        // Avoid splitting a multi-code-unit initial in localized Steam names.
        string element = System.Globalization.StringInfo.GetNextTextElement(trimmed);
        return element.ToUpperInvariant();
    }

    private void OnProfileClicked()
    {
        // Never rebuild Settings, Shop, or Profile UI here. Keep wallet values
        // and localization from the original modal, replace only first-line name.
        currentNameCache = Resolve();
        ApplyNameIfChanged(true);
        if (modalBody == null) return;
        string original = modalBody.text ?? string.Empty;
        int firstLine = original.IndexOf('\n');
        string name = CurrentDisplayName;
        string expected = firstLine >= 0
            ? name + original.Substring(firstLine)
            : name;
        if (!string.Equals(original, expected, StringComparison.Ordinal))
            modalBody.text = expected;
        Debug.Log("ATB_PROFILE_MODAL_IDENTITY resolved=true source=canonical_local_identity");
    }
}
