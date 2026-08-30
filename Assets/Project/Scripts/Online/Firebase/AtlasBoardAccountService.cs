using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

public sealed class AtlasBoardAccountService : MonoBehaviour
{
    public static AtlasBoardAccountService Instance { get; private set; }

    public bool IsInitialized { get; private set; }
    public string CurrentAccountId =>
        auth != null && auth.CurrentUser != null
            ? auth.CurrentUser.UserId
            : string.Empty;

    public bool IsSignedIn =>
        auth != null && auth.CurrentUser != null;

    private FirebaseAuth auth;
    private FirebaseFirestore firestore;
    private Task initializationTask;

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
        {
            Instance = null;
        }
    }

    public Task EnsureInitializedAsync()
    {
        if (initializationTask == null)
        {
            initializationTask = InitializeAsync();
        }

        return initializationTask;
    }

    private async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        DependencyStatus dependencyStatus =
            await FirebaseApp.CheckAndFixDependenciesAsync();

        if (dependencyStatus != DependencyStatus.Available)
        {
            Debug.LogError(
                "AtlasBoard Account Runtime v1 could not initialize Firebase. " +
                $"Dependency status: {dependencyStatus}.",
                this);
            return;
        }

        FirebaseApp app = FirebaseApp.DefaultInstance;
        auth = FirebaseAuth.DefaultInstance;
        firestore = FirebaseFirestore.DefaultInstance;

        string projectId =
            app != null && app.Options != null
                ? app.Options.ProjectId
                : string.Empty;

        if (projectId != "atlasboard-usa")
        {
            Debug.LogError(
                "AtlasBoard Account Runtime v1 connected to an unexpected " +
                $"Firebase project. Expected atlasboard-usa, got '{projectId}'.",
                this);
            return;
        }

        IsInitialized =
            auth != null &&
            firestore != null;

        if (IsInitialized)
        {
            Debug.Log(
                "AtlasBoard Account Runtime v1 ready. Firebase project=" +
                "atlasboard-usa; Email/Password account, profile and cloud " +
                "preferences services are available. No login/register UI was " +
                "created and existing PlayerPrefs settings were not modified.",
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
        {
            return validation;
        }

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

            string uid = createdUser.UserId;
            string normalizedCountry =
                NormalizeCountryCode(countryCode);
            string normalizedLanguage =
                NormalizeLanguageCode(preferredLanguage);

            DocumentReference userRef =
                firestore.Collection("users").Document(uid);

            DocumentReference publicProfileRef =
                firestore.Collection("public_profiles").Document(uid);

            DocumentReference preferencesRef =
                firestore.Collection("preferences").Document(uid);

            Dictionary<string, object> userData =
                BuildNewUserDocument(
                    uid,
                    normalizedCountry,
                    normalizedLanguage);

            Dictionary<string, object> publicProfileData =
                BuildNewPublicProfileDocument(displayName.Trim());

            Dictionary<string, object> preferencesData =
                BuildDefaultPreferencesDocument(normalizedLanguage);

            WriteBatch batch = firestore.StartBatch();
            batch.Set(userRef, userData);
            batch.Set(publicProfileRef, publicProfileData);
            batch.Set(preferencesRef, preferencesData);

            await batch.CommitAsync();

            Debug.Log(
                $"AtlasBoard account created. UID={uid}. " +
                "users/public_profiles/preferences were committed atomically.",
                this);

            return AtlasAccountOperationResult.Ok(uid);
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
                        "AtlasBoard account registration rollback could not " +
                        "delete the newly-created Firebase Auth user. " +
                        cleanupException.Message,
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
            {
                throw new InvalidOperationException(
                    "Firebase Auth returned no account after sign-in.");
            }

            Debug.Log(
                $"AtlasBoard account signed in. UID={uid}.",
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
        {
            return;
        }

        auth.SignOut();
        Debug.Log("AtlasBoard account signed out.", this);
    }

    public async Task<AtlasAccountSnapshot> LoadCurrentAccountAsync()
    {
        await EnsureInitializedAsync();

        if (!IsInitialized || !IsSignedIn)
        {
            return null;
        }

        string uid = CurrentAccountId;

        DocumentSnapshot userSnapshot =
            await firestore.Collection("users")
                .Document(uid)
                .GetSnapshotAsync();

        DocumentSnapshot profileSnapshot =
            await firestore.Collection("public_profiles")
                .Document(uid)
                .GetSnapshotAsync();

        DocumentSnapshot preferencesSnapshot =
            await firestore.Collection("preferences")
                .Document(uid)
                .GetSnapshotAsync();

        if (!userSnapshot.Exists ||
            !profileSnapshot.Exists ||
            !preferencesSnapshot.Exists)
        {
            Debug.LogWarning(
                $"AtlasBoard account UID={uid} is authenticated but one or " +
                "more required Firestore documents are missing.",
                this);
            return null;
        }

        Dictionary<string, object> userData =
            userSnapshot.ToDictionary();
        Dictionary<string, object> profileData =
            profileSnapshot.ToDictionary();
        Dictionary<string, object> preferencesData =
            preferencesSnapshot.ToDictionary();

        AtlasAccountSnapshot snapshot =
            new AtlasAccountSnapshot
            {
                AccountId = uid,
                AccountStatus = GetString(userData, "accountStatus"),
                MembershipTier = GetString(userData, "membershipTier"),
                CountryCode = GetString(userData, "countryCode"),
                PreferredLanguage = GetString(userData, "preferredLanguage"),
                SchemaVersion = GetInt(userData, "schemaVersion", 1),
                DisplayName = GetString(profileData, "displayName"),
                AvatarId = GetString(profileData, "avatarId"),
                ProfileFrameId = GetString(profileData, "profileFrameId"),
                Preferences = BuildPreferencesFromDictionary(preferencesData)
            };

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

        string normalizedCountry =
            NormalizeCountryCode(countryCode);
        string normalizedLanguage =
            NormalizeLanguageCode(preferredLanguage);

        if (normalizedCountry.Length != 2)
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.InvalidCountryCode,
                "account.error.invalid_country",
                "Country code must be a two-letter code.");
        }

        if (!AtlasBoardAccountConstants.SupportedLanguageCodes.Contains(
                normalizedLanguage))
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.UnsupportedLanguage,
                "account.error.unsupported_language",
                "Unsupported Atlas Board language code.");
        }

        try
        {
            Dictionary<string, object> updates =
                new Dictionary<string, object>
                {
                    { "countryCode", normalizedCountry },
                    { "preferredLanguage", normalizedLanguage }
                };

            await firestore.Collection("users")
                .Document(CurrentAccountId)
                .UpdateAsync(updates);

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
            displayName != null ? displayName.Trim() : string.Empty;

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
            Dictionary<string, object> data =
                new Dictionary<string, object>
                {
                    { "displayName", normalizedDisplayName },
                    { "avatarId", avatarId ?? string.Empty },
                    { "profileFrameId", profileFrameId ?? string.Empty },
                    { "updatedAt", FieldValue.ServerTimestamp }
                };

            await firestore.Collection("public_profiles")
                .Document(CurrentAccountId)
                .SetAsync(data);

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
        {
            preferences = new AtlasCloudPreferences();
        }

        string language = NormalizeLanguageCode(preferences.Language);

        if (!AtlasBoardAccountConstants.SupportedLanguageCodes.Contains(
                language))
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.UnsupportedLanguage,
                "account.error.unsupported_language",
                "Unsupported Atlas Board language code.");
        }

        try
        {
            Dictionary<string, object> data =
                BuildPreferencesDocument(preferences, language);

            await firestore.Collection("preferences")
                .Document(CurrentAccountId)
                .SetAsync(data);

            return AtlasAccountOperationResult.Ok(CurrentAccountId);
        }
        catch (Exception exception)
        {
            return MapException(exception);
        }
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
            displayName != null ? displayName.Trim() : string.Empty;

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

        string language = NormalizeLanguageCode(preferredLanguage);

        if (!AtlasBoardAccountConstants.SupportedLanguageCodes.Contains(
                language))
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.UnsupportedLanguage,
                "account.error.unsupported_language",
                "Unsupported Atlas Board language code.");
        }

        return AtlasAccountOperationResult.Ok(string.Empty);
    }

    private static Dictionary<string, object> BuildNewUserDocument(
        string uid,
        string countryCode,
        string preferredLanguage)
    {
        return new Dictionary<string, object>
        {
            { "uid", uid },
            { "schemaVersion", AtlasBoardAccountConstants.SchemaVersion },
            { "accountStatus", AtlasBoardAccountConstants.DefaultAccountStatus },
            { "membershipTier", AtlasBoardAccountConstants.DefaultMembershipTier },
            { "countryCode", countryCode },
            { "preferredLanguage", preferredLanguage },
            { "createdAt", FieldValue.ServerTimestamp }
        };
    }

    private static Dictionary<string, object>
        BuildNewPublicProfileDocument(string displayName)
    {
        return new Dictionary<string, object>
        {
            { "displayName", displayName },
            { "avatarId", string.Empty },
            { "profileFrameId", string.Empty },
            { "updatedAt", FieldValue.ServerTimestamp }
        };
    }

    private static Dictionary<string, object>
        BuildDefaultPreferencesDocument(string language)
    {
        return new Dictionary<string, object>
        {
            { "language", language },
            { "audio", new Dictionary<string, object>() },
            { "graphics", new Dictionary<string, object>() },
            { "controls", new Dictionary<string, object>() },
            { "gameplay", new Dictionary<string, object>() },
            { "crossplayPreference", AtlasBoardAccountConstants.DefaultCrossplayPreference },
            { "updatedAt", FieldValue.ServerTimestamp }
        };
    }

    private static Dictionary<string, object> BuildPreferencesDocument(
        AtlasCloudPreferences preferences,
        string language)
    {
        return new Dictionary<string, object>
        {
            { "language", language },
            { "audio", preferences.Audio ?? new Dictionary<string, object>() },
            { "graphics", preferences.Graphics ?? new Dictionary<string, object>() },
            { "controls", preferences.Controls ?? new Dictionary<string, object>() },
            { "gameplay", preferences.Gameplay ?? new Dictionary<string, object>() },
            {
                "crossplayPreference",
                string.IsNullOrWhiteSpace(preferences.CrossplayPreference)
                    ? AtlasBoardAccountConstants.DefaultCrossplayPreference
                    : preferences.CrossplayPreference
            },
            { "updatedAt", FieldValue.ServerTimestamp }
        };
    }

    private static AtlasCloudPreferences BuildPreferencesFromDictionary(
        Dictionary<string, object> data)
    {
        AtlasCloudPreferences preferences =
            new AtlasCloudPreferences
            {
                Language = GetString(data, "language", "en"),
                CrossplayPreference = GetString(
                    data,
                    "crossplayPreference",
                    AtlasBoardAccountConstants.DefaultCrossplayPreference),
                Audio = GetMap(data, "audio"),
                Graphics = GetMap(data, "graphics"),
                Controls = GetMap(data, "controls"),
                Gameplay = GetMap(data, "gameplay")
            };

        return preferences;
    }

    private static string NormalizeCountryCode(string countryCode)
    {
        return string.IsNullOrWhiteSpace(countryCode)
            ? string.Empty
            : countryCode.Trim().ToUpperInvariant();
    }

    private static string NormalizeLanguageCode(string language)
    {
        return string.IsNullOrWhiteSpace(language)
            ? "en"
            : language.Trim().ToLowerInvariant();
    }

    private static string GetString(
        Dictionary<string, object> data,
        string key,
        string fallback = "")
    {
        if (data != null &&
            data.TryGetValue(key, out object value) &&
            value != null)
        {
            return value.ToString();
        }

        return fallback;
    }

    private static int GetInt(
        Dictionary<string, object> data,
        string key,
        int fallback)
    {
        if (data != null &&
            data.TryGetValue(key, out object value) &&
            value != null)
        {
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        return fallback;
    }

    private static Dictionary<string, object> GetMap(
        Dictionary<string, object> data,
        string key)
    {
        if (data != null &&
            data.TryGetValue(key, out object value) &&
            value is Dictionary<string, object> map)
        {
            return map;
        }

        return new Dictionary<string, object>();
    }

    private static AtlasAccountOperationResult MapException(Exception exception)
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

        string technical = exception != null
            ? exception.ToString()
            : "Unknown account service error.";

        if (technical.IndexOf(
                "permission",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return AtlasAccountOperationResult.Fail(
                AtlasAccountErrorCode.PermissionDenied,
                "account.error.permission_denied",
                technical);
        }

        return AtlasAccountOperationResult.Fail(
            AtlasAccountErrorCode.Unknown,
            "account.error.unknown",
            technical);
    }
}
