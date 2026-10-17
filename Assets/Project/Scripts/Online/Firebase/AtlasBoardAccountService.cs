using System;
using System.Text;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;

public sealed class AtlasBoardAccountService : MonoBehaviour
{
    private const string Database =
        "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";

    public static AtlasBoardAccountService Instance { get; private set; }

    public bool IsInitialized { get; private set; }
    public string CurrentAccountId =>
        auth != null && auth.CurrentUser != null
            ? auth.CurrentUser.UserId
            : string.Empty;

    public bool IsSignedIn =>
        auth != null && auth.CurrentUser != null;

    private FirebaseAuth auth;
    private Task initializationTask;

    [Serializable]
    private sealed class GeneralWire
    {
        public int schemaVersion = 1;
        public string accountType = "registered";
        public string language = "en";
        public string countryCode = "ZZ";
        public long lastSeenAt;
    }

    [Serializable]
    private sealed class SettingsWire
    {
        public int schemaVersion = 1;
        public string language = "en";
        public string crossplayPreference =
            AtlasBoardAccountConstants.DefaultCrossplayPreference;
    }

    [Serializable]
    private sealed class UserWire
    {
        public string userId;
        public string playerId;
        public string email;
        public string displayName;
        public long createdAt;
        public long updatedAt;
        public long lastDisplayNameChangeAt;
        public string accountStatus;
        public string membershipTier;
        public GeneralWire general;
        public SettingsWire settings;
    }

    [Serializable]
    private sealed class PublicProfileWire
    {
        public string displayName;
        public string avatarId;
        public string profileFrameId;
        public long updatedAt;
    }

    [Serializable]
    private sealed class SettingsPatchWire
    {
        public int schemaVersion = 1;
        public string language;
        public string crossplayPreference;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        initializationTask = InitializeAsync();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public Task EnsureInitializedAsync()
    {
        if (initializationTask == null)
            initializationTask = InitializeAsync();

        return initializationTask;
    }

    private async Task InitializeAsync()
    {
        if (IsInitialized)
            return;

        DependencyStatus dependencyStatus =
            await FirebaseApp.CheckAndFixDependenciesAsync();

        if (dependencyStatus != DependencyStatus.Available)
        {
            Debug.LogError(
                "AtlasBoard Account Runtime could not initialize Firebase. " +
                "Dependency status: " + dependencyStatus + ".",
                this);
            return;
        }

        FirebaseApp app = FirebaseApp.DefaultInstance;
        auth = FirebaseAuth.DefaultInstance;

        string projectId =
            app != null && app.Options != null
                ? app.Options.ProjectId
                : string.Empty;

        if (projectId != "atlasboard-usa")
        {
            Debug.LogError(
                "AtlasBoard Account Runtime connected to an unexpected " +
                "Firebase project. Expected atlasboard-usa, got '" +
                projectId + "'.",
                this);
            return;
        }

        IsInitialized = auth != null;

        if (IsInitialized)
        {
            Debug.Log(
                "AtlasBoard Account Runtime ready. Firebase Auth + Spark RTDB " +
                "own account/profile/preferences runtime state; Firestore is " +
                "not required for normal account operation.",
                this);
        }
    }

    public async Task<AtlasAccountOperationResult>
        RegisterWithEmailPasswordAsync(
            string email,
            string password,
            string displayName,
            string countryCode,
            string preferredLanguage)
    {
        await EnsureInitializedAsync();

        if (!IsInitialized)
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.NotInitialized,
                "account.error.service_unavailable",
                "Firebase account service is not initialized.");
        }

        AtlasAccountOperationResult validation =
            ValidateRegistrationInput(
                email,
                password,
                displayName,
                countryCode,
                preferredLanguage);

        if (!validation.Success)
            return validation;

        FirebaseUser createdUser = null;

        try
        {
            AuthResult authResult =
                await auth.CreateUserWithEmailAndPasswordAsync(
                    email.Trim(),
                    password);

            createdUser = authResult.User;

            if (createdUser == null ||
                string.IsNullOrWhiteSpace(createdUser.UserId))
            {
                throw new InvalidOperationException(
                    "Firebase Auth did not return a valid user.");
            }

            string normalizedDisplayName = displayName.Trim();
            string normalizedCountry = NormalizeCountryCode(countryCode);
            string normalizedLanguage = NormalizeLanguageCode(preferredLanguage);

            await createdUser.UpdateUserProfileAsync(
                new UserProfile
                {
                    DisplayName = normalizedDisplayName
                });

            bool mirrored =
                await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync(
                    normalizedDisplayName,
                    true,
                    normalizedCountry,
                    normalizedLanguage);

            if (!mirrored)
            {
                throw new InvalidOperationException(
                    "RTDB_ACCOUNT_BOOTSTRAP_FAILED");
            }

            string token = await createdUser.TokenAsync(false);
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("AUTH_TOKEN_UNAVAILABLE");

            await WritePublicProfileAsync(
                createdUser.UserId,
                token,
                normalizedDisplayName,
                string.Empty,
                string.Empty);

            await WriteSettingsAsync(
                createdUser.UserId,
                token,
                normalizedLanguage,
                AtlasBoardAccountConstants.DefaultCrossplayPreference);

            Debug.Log(
                "ATB_ACCOUNT_REGISTER success=true storage=RTDB " +
                "firestore_writes=0 uid=" + Short(createdUser.UserId),
                this);

            return AtlasAccountOperationResult.Ok(createdUser.UserId);
        }
        catch (Exception exception)
        {
            if (createdUser != null)
            {
                try
                {
                    await createdUser.DeleteAsync();
                }
                catch (Exception cleanupException)
                {
                    Debug.LogWarning(
                        "AtlasBoard registration rollback could not delete " +
                        "the newly-created Auth user. " +
                        cleanupException.GetType().Name,
                        this);
                }
            }

            return MapException(exception);
        }
    }

    public async Task<AtlasAccountOperationResult>
        SignInWithEmailPasswordAsync(
            string email,
            string password)
    {
        await EnsureInitializedAsync();

        if (!IsInitialized)
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.NotInitialized,
                "account.error.service_unavailable",
                "Firebase account service is not initialized.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.InvalidEmail,
                "account.error.invalid_email",
                "Email is empty.");
        }

        if (string.IsNullOrEmpty(password))
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.InvalidCredentials,
                "account.error.invalid_credentials",
                "Password is empty.");
        }

        try
        {
            AuthResult result =
                await auth.SignInWithEmailAndPasswordAsync(
                    email.Trim(),
                    password);

            string uid =
                result.User != null
                    ? result.User.UserId
                    : string.Empty;

            if (string.IsNullOrWhiteSpace(uid))
                throw new InvalidOperationException(
                    "Firebase Auth returned no account after sign-in.");

            // Account authentication must not be rejected by a transient profile
            // mirror failure. LoadCurrentAccountAsync will retry the RTDB bootstrap.
            bool mirrored =
                await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync(
                    string.Empty,
                    true);

            Debug.Log(
                "ATB_ACCOUNT_SIGN_IN success=true storage=Auth+RTDB " +
                "profile_sync=" + mirrored.ToString().ToLowerInvariant() +
                " uid=" + Short(uid),
                this);

            return AtlasAccountOperationResult.Ok(uid);
        }
        catch (Exception exception)
        {
            return MapException(exception);
        }
    }

    public void SignOut()
    {
        if (auth == null)
            return;

        auth.SignOut();
        Debug.Log("AtlasBoard account signed out.", this);
    }

    public async Task<string> GetCurrentIdTokenAsync(
        bool forceRefresh = false)
    {
        await EnsureInitializedAsync();

        if (!IsInitialized ||
            auth == null ||
            auth.CurrentUser == null)
        {
            return string.Empty;
        }

        return await auth.CurrentUser.TokenAsync(forceRefresh);
    }

    public async Task<AtlasAccountSnapshot> LoadCurrentAccountAsync()
    {
        await EnsureInitializedAsync();

        if (!IsInitialized || !IsSignedIn)
            return null;

        FirebaseUser user = auth.CurrentUser;
        string uid = user.UserId;

        // Existing Auth-only/legacy-Firestore accounts are lazily materialized
        // into the canonical RTDB profile. Existing RTDB displayName wins.
        bool mirrored =
            await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync(
                string.Empty,
                false);

        if (!mirrored)
        {
            mirrored =
                await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync(
                    string.Empty,
                    true);
        }

        string token = await user.TokenAsync(false);
        if (string.IsNullOrWhiteSpace(token))
            return null;

        UserWire userWire = await ReadJsonAsync<UserWire>(
            UserUrl(uid, token));

        if (userWire == null)
        {
            await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync(
                string.Empty,
                true);

            userWire = await ReadJsonAsync<UserWire>(
                UserUrl(uid, token));
        }

        if (userWire == null)
        {
            Debug.LogWarning(
                "ATB_ACCOUNT_RTDB_LOAD success=false reason=user_missing " +
                "uid=" + Short(uid),
                this);
            return null;
        }

        PublicProfileWire publicProfile =
            await ReadJsonAsync<PublicProfileWire>(
                PublicProfileUrl(uid, token));

        string canonicalName =
            !string.IsNullOrWhiteSpace(publicProfile?.displayName)
                ? publicProfile.displayName
                : !string.IsNullOrWhiteSpace(userWire.displayName)
                    ? userWire.displayName
                    : ResolveCanonicalDisplayName(user);

        if (publicProfile == null)
        {
            try
            {
                await WritePublicProfileAsync(
                    uid,
                    token,
                    canonicalName,
                    string.Empty,
                    string.Empty);

                publicProfile = new PublicProfileWire
                {
                    displayName = canonicalName,
                    avatarId = string.Empty,
                    profileFrameId = string.Empty,
                    updatedAt = Now()
                };
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "ATB_PUBLIC_PROFILE_BOOTSTRAP success=false type=" +
                    exception.GetType().Name);
            }
        }

        string language =
            NormalizeLanguageCode(userWire.settings?.language);
        if (string.IsNullOrEmpty(language))
            language = NormalizeLanguageCode(userWire.general?.language);
        if (string.IsNullOrEmpty(language))
            language = "en";

        string country =
            NormalizeCountryCode(userWire.general?.countryCode);
        if (string.IsNullOrEmpty(country))
            country = "ZZ";

        string crossplay =
            userWire.settings != null &&
            !string.IsNullOrWhiteSpace(userWire.settings.crossplayPreference)
                ? userWire.settings.crossplayPreference
                : AtlasBoardAccountConstants.DefaultCrossplayPreference;

        AtlasAccountSnapshot snapshot =
            new AtlasAccountSnapshot
            {
                AccountId = uid,
                AccountStatus =
                    string.IsNullOrWhiteSpace(userWire.accountStatus)
                        ? AtlasBoardAccountConstants.DefaultAccountStatus
                        : userWire.accountStatus,
                MembershipTier =
                    string.IsNullOrWhiteSpace(userWire.membershipTier)
                        ? AtlasBoardAccountConstants.DefaultMembershipTier
                        : userWire.membershipTier,
                CountryCode = country,
                PreferredLanguage = language,
                SchemaVersion =
                    userWire.general != null
                        ? Mathf.Max(1, userWire.general.schemaVersion)
                        : AtlasBoardAccountConstants.SchemaVersion,
                DisplayName = canonicalName,
                AvatarId = publicProfile?.avatarId ?? string.Empty,
                ProfileFrameId = publicProfile?.profileFrameId ?? string.Empty,
                Preferences = new AtlasCloudPreferences
                {
                    Language = language,
                    CrossplayPreference = crossplay
                }
            };

        Debug.Log(
            "ATB_ACCOUNT_RTDB_LOAD success=true firestore_reads=0 uid=" +
            Short(uid),
            this);

        return snapshot;
    }

    public async Task<AtlasAccountOperationResult>
        UpdateAccountLocaleAsync(
            string countryCode,
            string preferredLanguage)
    {
        await EnsureInitializedAsync();

        if (!IsInitialized || !IsSignedIn)
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.NotAuthenticated,
                "account.error.not_signed_in",
                "No authenticated Atlas Board account.");
        }

        string normalizedCountry = NormalizeCountryCode(countryCode);
        string normalizedLanguage = NormalizeLanguageCode(preferredLanguage);

        if (normalizedCountry.Length != 2)
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.InvalidCountryCode,
                "account.error.invalid_country",
                "Country code must be a two-letter code.");
        }

        if (string.IsNullOrEmpty(normalizedLanguage))
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.UnsupportedLanguage,
                "account.error.unsupported_language",
                "Unsupported Atlas Board language code.");
        }

        try
        {
            bool success =
                await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync(
                    string.Empty,
                    true,
                    normalizedCountry,
                    normalizedLanguage);

            if (!success)
                throw new InvalidOperationException("RTDB_LOCALE_WRITE_FAILED");

            return AtlasAccountOperationResult.Ok(CurrentAccountId);
        }
        catch (Exception exception)
        {
            return MapException(exception);
        }
    }

    public async Task<AtlasAccountOperationResult>
        UpdatePublicProfileAsync(
            string displayName,
            string avatarId,
            string profileFrameId)
    {
        await EnsureInitializedAsync();

        if (!IsInitialized || !IsSignedIn)
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.NotAuthenticated,
                "account.error.not_signed_in",
                "No authenticated Atlas Board account.");
        }

        string normalizedDisplayName =
            displayName != null
                ? displayName.Trim()
                : string.Empty;

        if (normalizedDisplayName.Length < 2 ||
            normalizedDisplayName.Length > 32)
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.InvalidDisplayName,
                "account.error.invalid_display_name",
                "Display name must be 2-32 characters.");
        }

        try
        {
            bool privateProfileUpdated =
                await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync(
                    normalizedDisplayName,
                    true);

            if (!privateProfileUpdated)
                throw new InvalidOperationException(
                    "RTDB_PRIVATE_PROFILE_WRITE_FAILED");

            FirebaseUser user = auth.CurrentUser;
            string token = await user.TokenAsync(false);

            await WritePublicProfileAsync(
                user.UserId,
                token,
                normalizedDisplayName,
                avatarId ?? string.Empty,
                profileFrameId ?? string.Empty);

            if (!string.Equals(
                    user.DisplayName,
                    normalizedDisplayName,
                    StringComparison.Ordinal))
            {
                await user.UpdateUserProfileAsync(
                    new UserProfile
                    {
                        DisplayName = normalizedDisplayName
                    });
            }

            return AtlasAccountOperationResult.Ok(CurrentAccountId);
        }
        catch (Exception exception)
        {
            return MapException(exception);
        }
    }

    public async Task<AtlasAccountOperationResult>
        SaveCloudPreferencesAsync(
            AtlasCloudPreferences preferences)
    {
        await EnsureInitializedAsync();

        if (!IsInitialized || !IsSignedIn)
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.NotAuthenticated,
                "account.error.not_signed_in",
                "No authenticated Atlas Board account.");
        }

        if (preferences == null)
            preferences = new AtlasCloudPreferences();

        string language = NormalizeLanguageCode(preferences.Language);
        if (string.IsNullOrEmpty(language))
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.UnsupportedLanguage,
                "account.error.unsupported_language",
                "Unsupported Atlas Board language code.");
        }

        string crossplay =
            string.Equals(
                preferences.CrossplayPreference,
                "disabled",
                StringComparison.OrdinalIgnoreCase)
                ? "disabled"
                : AtlasBoardAccountConstants.DefaultCrossplayPreference;

        try
        {
            FirebaseUser user = auth.CurrentUser;
            string token = await user.TokenAsync(false);
            await WriteSettingsAsync(
                user.UserId,
                token,
                language,
                crossplay);

            Debug.Log(
                "ATB_ACCOUNT_SETTINGS_WRITE success=true storage=RTDB " +
                "firestore_writes=0",
                this);

            return AtlasAccountOperationResult.Ok(CurrentAccountId);
        }
        catch (Exception exception)
        {
            return MapException(exception);
        }
    }

    private static async Task WritePublicProfileAsync(
        string uid,
        string token,
        string displayName,
        string avatarId,
        string profileFrameId)
    {
        PublicProfileWire payload =
            new PublicProfileWire
            {
                displayName = displayName ?? string.Empty,
                avatarId = avatarId ?? string.Empty,
                profileFrameId = profileFrameId ?? string.Empty,
                updatedAt = Now()
            };

        await RequestAsync(
            "PUT",
            PublicProfileUrl(uid, token),
            JsonUtility.ToJson(payload));
    }

    private static async Task WriteSettingsAsync(
        string uid,
        string token,
        string language,
        string crossplay)
    {
        SettingsPatchWire payload =
            new SettingsPatchWire
            {
                schemaVersion = 1,
                language = language,
                crossplayPreference = crossplay
            };

        await RequestAsync(
            "PATCH",
            SettingsUrl(uid, token),
            JsonUtility.ToJson(payload));
    }

    private static async Task<T> ReadJsonAsync<T>(string url)
        where T : class
    {
        string body = await RequestAsync("GET", url, null);
        if (string.IsNullOrWhiteSpace(body) ||
            string.Equals(
                body.Trim(),
                "null",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(body);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> RequestAsync(
        string method,
        string url,
        string json)
    {
        using (UnityWebRequest request =
               new UnityWebRequest(url, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 15;

            if (json != null)
            {
                request.uploadHandler =
                    new UploadHandlerRaw(
                        Encoding.UTF8.GetBytes(json));
                request.SetRequestHeader(
                    "Content-Type",
                    "application/json");
            }

            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);

            TaskCompletionSource<bool> completion =
                new TaskCompletionSource<bool>();

            UnityWebRequestAsyncOperation operation =
                request.SendWebRequest();

            if (operation.isDone)
                completion.TrySetResult(true);
            else
                operation.completed +=
                    _ => completion.TrySetResult(true);

            await completion.Task;

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    "RTDB_HTTP_" + request.responseCode);
            }

            return request.downloadHandler != null
                ? request.downloadHandler.text
                : string.Empty;
        }
    }

    private static string UserUrl(string uid, string token)
    {
        return Database +
            "/users/" + UnityWebRequest.EscapeURL(uid ?? string.Empty) +
            ".json?auth=" + UnityWebRequest.EscapeURL(token ?? string.Empty);
    }

    private static string PublicProfileUrl(string uid, string token)
    {
        return Database +
            "/publicProfiles/" +
            UnityWebRequest.EscapeURL(uid ?? string.Empty) +
            ".json?auth=" + UnityWebRequest.EscapeURL(token ?? string.Empty);
    }

    private static string SettingsUrl(string uid, string token)
    {
        return Database +
            "/users/" + UnityWebRequest.EscapeURL(uid ?? string.Empty) +
            "/settings.json?auth=" +
            UnityWebRequest.EscapeURL(token ?? string.Empty);
    }

    private static string ResolveCanonicalDisplayName(FirebaseUser user)
    {
        if (user != null &&
            !string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName.Trim();
        }

        AtlasBoardPlatformRuntime platform =
            AtlasBoardPlatformRuntime.Instance;

        if (platform != null &&
            platform.IsSteamProviderActive &&
            platform.SteamInitialized &&
            !string.IsNullOrWhiteSpace(platform.SteamPersonaName))
        {
            return platform.SteamPersonaName.Trim();
        }

        return AtlasBoardGuestIdentity.DisplayName;
    }

    private static AtlasAccountOperationResult ValidateRegistrationInput(
        string email,
        string password,
        string displayName,
        string countryCode,
        string preferredLanguage)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            !email.Contains("@"))
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.InvalidEmail,
                "account.error.invalid_email",
                "Email is invalid.");
        }

        if (string.IsNullOrEmpty(password) ||
            password.Length < 6)
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.WeakPassword,
                "account.error.weak_password",
                "Password must contain at least 6 characters.");
        }

        string normalizedDisplayName =
            displayName != null
                ? displayName.Trim()
                : string.Empty;

        if (normalizedDisplayName.Length < 2 ||
            normalizedDisplayName.Length > 32)
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.InvalidDisplayName,
                "account.error.invalid_display_name",
                "Display name must be 2-32 characters.");
        }

        if (NormalizeCountryCode(countryCode).Length != 2)
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.InvalidCountryCode,
                "account.error.invalid_country",
                "Country code must be a two-letter code.");
        }

        if (string.IsNullOrEmpty(
                NormalizeLanguageCode(preferredLanguage)))
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.UnsupportedLanguage,
                "account.error.unsupported_language",
                "Unsupported Atlas Board language code.");
        }

        return AtlasAccountOperationResult.Ok(string.Empty);
    }

    private static string NormalizeCountryCode(string countryCode)
    {
        string normalized =
            countryCode != null
                ? countryCode.Trim().ToUpperInvariant()
                : string.Empty;

        if (normalized.Length != 2)
            return string.Empty;

        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[i];
            if (c < 'A' || c > 'Z')
                return string.Empty;
        }

        return normalized;
    }

    private static string NormalizeLanguageCode(string languageCode)
    {
        string normalized =
            languageCode != null
                ? languageCode.Trim().ToLowerInvariant()
                : string.Empty;

        return AtlasBoardAccountConstants.SupportedLanguageCodes.Contains(
                normalized)
            ? normalized
            : string.Empty;
    }

    private static AtlasAccountOperationResult MapException(
        Exception exception)
    {
        FirebaseException firebaseException =
            exception as FirebaseException;

        if (firebaseException != null)
        {
            AuthError authError =
                (AuthError)firebaseException.ErrorCode;

            switch (authError)
            {
                case AuthError.InvalidEmail:
                    return AtlasAccountOperationResult.Fail(
                        AtlasAccountErrorCode.InvalidEmail,
                        "account.error.invalid_email",
                        exception.Message);

                case AuthError.WeakPassword:
                    return AtlasAccountOperationResult.Fail(
                        AtlasAccountErrorCode.WeakPassword,
                        "account.error.weak_password",
                        exception.Message);

                case AuthError.EmailAlreadyInUse:
                    return AtlasAccountOperationResult.Fail(
                        AtlasAccountErrorCode.EmailAlreadyInUse,
                        "account.error.email_in_use",
                        exception.Message);

                case AuthError.WrongPassword:
                case AuthError.UserNotFound:
                case AuthError.InvalidCredential:
                    return AtlasAccountOperationResult.Fail(
                        AtlasAccountErrorCode.InvalidCredentials,
                        "account.error.invalid_credentials",
                        exception.Message);

                case AuthError.UserDisabled:
                    return AtlasAccountOperationResult.Fail(
                        AtlasAccountErrorCode.UserDisabled,
                        "account.error.user_disabled",
                        exception.Message);

                case AuthError.TooManyRequests:
                    return AtlasAccountOperationResult.Fail(
                        AtlasAccountErrorCode.TooManyRequests,
                        "account.error.too_many_requests",
                        exception.Message);

                case AuthError.NetworkRequestFailed:
                    return AtlasAccountOperationResult.Fail(
                        AtlasAccountErrorCode.Network,
                        "account.error.network",
                        exception.Message);
            }
        }

        string message = exception != null
            ? exception.Message ?? string.Empty
            : string.Empty;

        if (message.StartsWith(
                "RTDB_HTTP_401",
                StringComparison.Ordinal) ||
            message.StartsWith(
                "RTDB_HTTP_403",
                StringComparison.Ordinal))
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.PermissionDenied,
                "account.error.permission_denied",
                message);
        }

        if (message.StartsWith(
                "RTDB_HTTP_",
                StringComparison.Ordinal))
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.Network,
                "account.error.network",
                message);
        }

        return AtlasAccountOperationResult.Fail(
            AtlasAccountErrorCode.Unknown,
            "account.error.unknown",
            message);
    }

    private static long Now()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static string Short(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "none";

        return value.Length <= 7
            ? value
            : value.Substring(0, 7);
    }
}
