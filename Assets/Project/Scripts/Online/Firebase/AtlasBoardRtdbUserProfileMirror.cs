using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Low-frequency authenticated RTDB mirror for general AtlasBoard account and
/// device/platform metadata. Credentials and valuable progression never enter
/// this branch.
///
/// Canonical private path:
/// /users/{uid}
///   userId, playerId, email, displayName, createdAt, updatedAt,
///   lastDisplayNameChangeAt
///   general/{schemaVersion,accountType,language,countryCode,lastSeenAt}
///   platforms/{platform}/devices/{deviceId}/{...}
///
/// Passwords remain exclusively inside Firebase Authentication.
/// Wallet/XP/purchases/entitlements/verification remain trusted elsewhere.
/// </summary>
public static class AtlasBoardRtdbUserProfileMirror
{
    private const string FallbackDatabaseUrl =
        "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";

    private const string DeviceIdPrefsKey =
        "atlasboard.identity.deviceId.v1";

    private static readonly object SyncGate = new object();
    private static Task<bool> syncTask;
    private static string lastSyncedUid = string.Empty;
    private static long lastSyncAtMs;

    [Serializable]
    private sealed class ExistingUser
    {
        public string userId;
        public string playerId;
        public string email;
        public string displayName;
        public long createdAt;
        public long updatedAt;
        public long lastDisplayNameChangeAt;
        public GeneralDto general;
    }

    [Serializable]
    private sealed class GeneralDto
    {
        public int schemaVersion = 1;
        public string accountType = "registered";
        public string language = "en";
        public string countryCode = "ZZ";
        public long lastSeenAt;
    }

    [Serializable]
    private sealed class UserDto
    {
        public string userId;
        public string playerId;
        public string email;
        public string displayName;
        public long createdAt;
        public long updatedAt;
        public long lastDisplayNameChangeAt;
        public GeneralDto general;
    }

    [Serializable]
    private sealed class DeviceDto
    {
        public string deviceId;
        public string deviceModel;
        public string operatingSystem;
        public string appVersion;
        public int settingsVersion = 1;
        public long firstSeenAt;
        public long lastSeenAt;
    }

    public static Task<bool> SyncCurrentUserAsync(
        string canonicalDisplayName,
        bool force = false,
        string countryCodeOverride = null,
        string languageCodeOverride = null)
    {
        lock (SyncGate)
        {
            // Account bootstrap and the background mirror can arrive in the
            // same frame. They must await one authoritative sync instead of
            // racing and letting AccountService read /users/{uid} too early.
            if (syncTask != null && !syncTask.IsCompleted)
            {
                return syncTask;
            }

            syncTask = SyncCurrentUserCoreAsync(
                canonicalDisplayName,
                force,
                countryCodeOverride,
                languageCodeOverride);

            return syncTask;
        }
    }

    private static async Task<bool> SyncCurrentUserCoreAsync(
        string canonicalDisplayName,
        bool force,
        string countryCodeOverride,
        string languageCodeOverride)
    {
        FirebaseAuth auth;
        FirebaseUser user;

        try
        {
            auth = FirebaseAuth.DefaultInstance;
            user = auth != null ? auth.CurrentUser : null;
        }
        catch
        {
            return false;
        }

        if (user == null || string.IsNullOrWhiteSpace(user.UserId))
            return false;

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (!force &&
            string.Equals(lastSyncedUid, user.UserId, StringComparison.Ordinal) &&
            now - lastSyncAtMs < 30000L)
        {
            return true;
        }

        try
        {
            string baseUrl = ResolveDatabaseUrl();
            string uid = user.UserId;
            string encodedUid = UnityWebRequest.EscapeURL(uid);
            string userPath = "/users/" + encodedUid + ".json";

            ExistingUser existing = null;
            string currentJson = await SendAuthenticatedAsync(
                user,
                "GET",
                baseUrl,
                userPath,
                null);

            if (!string.IsNullOrWhiteSpace(currentJson) &&
                !string.Equals(currentJson.Trim(), "null", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    existing = JsonUtility.FromJson<ExistingUser>(currentJson);
                }
                catch
                {
                    existing = null;
                }
            }

            string displayName = !string.IsNullOrWhiteSpace(canonicalDisplayName)
                ? canonicalDisplayName.Trim()
                : existing != null && !string.IsNullOrWhiteSpace(existing.displayName)
                    ? existing.displayName.Trim()
                    : (user.DisplayName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(displayName))
            {
                AtlasBoardPlatformRuntime platformRuntime =
                    AtlasBoardPlatformRuntime.Instance;

                if (platformRuntime != null &&
                    platformRuntime.IsSteamProviderActive &&
                    platformRuntime.SteamInitialized &&
                    !string.IsNullOrWhiteSpace(platformRuntime.SteamPersonaName))
                {
                    displayName = platformRuntime.SteamPersonaName.Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = AtlasBoardGuestIdentity.DisplayName;

            string languageCode = NormalizeLanguageCode(languageCodeOverride);
            if (string.IsNullOrEmpty(languageCode) &&
                existing != null &&
                existing.general != null)
            {
                languageCode = NormalizeLanguageCode(existing.general.language);
            }
            if (string.IsNullOrEmpty(languageCode))
                languageCode = ResolveLanguageCode();

            string countryCode = NormalizeCountryCode(countryCodeOverride);
            if (string.IsNullOrEmpty(countryCode) &&
                existing != null &&
                existing.general != null)
            {
                countryCode = NormalizeCountryCode(existing.general.countryCode);
            }
            if (string.IsNullOrEmpty(countryCode))
                countryCode = "ZZ";

            string email = (user.Email ?? string.Empty).Trim();
            long createdAt = existing != null && existing.createdAt > 0
                ? existing.createdAt
                : now;

            string playerId = existing != null && !string.IsNullOrWhiteSpace(existing.playerId)
                ? existing.playerId
                : BuildPlayerId(uid);

            bool nameChanged = existing != null &&
                               !string.IsNullOrWhiteSpace(existing.displayName) &&
                               !string.Equals(existing.displayName, displayName, StringComparison.Ordinal);

            long lastDisplayNameChangeAt = existing != null && existing.lastDisplayNameChangeAt > 0
                ? existing.lastDisplayNameChangeAt
                : createdAt;

            if (nameChanged)
                lastDisplayNameChangeAt = now;

            UserDto payload = new UserDto
            {
                userId = uid,
                playerId = playerId,
                email = email,
                displayName = displayName,
                createdAt = createdAt,
                updatedAt = now,
                lastDisplayNameChangeAt = lastDisplayNameChangeAt,
                general = new GeneralDto
                {
                    schemaVersion = 1,
                    accountType = "registered",
                    language = languageCode,
                    countryCode = countryCode,
                    lastSeenAt = now
                }
            };

            await SendAuthenticatedAsync(
                user,
                "PATCH",
                baseUrl,
                userPath,
                JsonUtility.ToJson(payload));

            string platform = ResolvePlatformKey();
            string deviceId = GetOrCreateDeviceId();
            string devicePath =
                "/users/" + encodedUid +
                "/platforms/" + UnityWebRequest.EscapeURL(platform) +
                "/devices/" + UnityWebRequest.EscapeURL(deviceId) +
                ".json";

            long existingFirstSeen = ReadLocalFirstSeen(uid, platform, deviceId);
            if (existingFirstSeen <= 0)
            {
                existingFirstSeen = now;
                WriteLocalFirstSeen(uid, platform, deviceId, existingFirstSeen);
            }

            DeviceDto device = new DeviceDto
            {
                deviceId = deviceId,
                deviceModel = Truncate(SystemInfo.deviceModel, 96),
                operatingSystem = Truncate(SystemInfo.operatingSystem, 128),
                appVersion = Truncate(Application.version, 32),
                settingsVersion = 1,
                firstSeenAt = existingFirstSeen,
                lastSeenAt = now
            };

            await SendAuthenticatedAsync(
                user,
                "PUT",
                baseUrl,
                devicePath,
                JsonUtility.ToJson(device));

            lastSyncedUid = uid;
            lastSyncAtMs = now;

            Debug.Log(
                "ATB_RTDB_USER_SYNC success=true uid=" + Short(uid) +
                " playerId=" + playerId +
                " platform=" + platform +
                " device=" + Short(deviceId) +
                " credentialsStored=false");

            return true;
        }
        catch (Exception error)
        {
            Debug.LogWarning(
                "ATB_RTDB_USER_SYNC success=false type=" +
                error.GetType().Name +
                " emailPresent=" +
                (!string.IsNullOrWhiteSpace(user != null ? user.Email : null)
                    ? "true"
                    : "false") +
                " message=" + Sanitize(error.Message));

            return false;
        }
    }

    private static async Task<string> SendAuthenticatedAsync(
        FirebaseUser user,
        string method,
        string baseUrl,
        string relativePath,
        string json)
    {
        if (user == null)
            throw new InvalidOperationException("Firebase user is unavailable.");

        string token = await user.TokenAsync(false);
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Firebase ID token is unavailable.");

        try
        {
            return await SendAsync(
                method,
                BuildAuthenticatedUrl(baseUrl, relativePath, token),
                json,
                false);
        }
        catch (RtdbHttpException error) when (error.ResponseCode == 401)
        {
            // Firebase ID/App Check tokens may cross an expiry boundary while
            // the Editor is already signed in. Refresh both once; never loop.
            string refreshedToken = await user.TokenAsync(true);
            if (string.IsNullOrWhiteSpace(refreshedToken))
                throw;

            Debug.Log(
                "ATB_RTDB_USER_SYNC auth_retry=true reason=http_401 once=true " +
                "appcheck_force_refresh=false");

            // RTDB 401 can be a Security Rules denial. Refreshing App Check
            // on every permission denial previously caused a debug-token
            // exchange storm without changing the rule outcome.
            return await SendAsync(
                method,
                BuildAuthenticatedUrl(baseUrl, relativePath, refreshedToken),
                json,
                false);
        }
    }

    private static string BuildAuthenticatedUrl(
        string baseUrl,
        string relativePath,
        string token)
    {
        return baseUrl.TrimEnd('/') +
               relativePath +
               "?auth=" + UnityWebRequest.EscapeURL(token);
    }

    private sealed class RtdbHttpException : InvalidOperationException
    {
        public long ResponseCode { get; }

        public RtdbHttpException(
            long responseCode,
            string message)
            : base(message)
        {
            ResponseCode = responseCode;
        }
    }

    private static async Task<string> SendAsync(
        string method,
        string url,
        string json,
        bool forceAppCheckRefresh)
    {
        using (UnityWebRequest request = new UnityWebRequest(url, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();

            if (json != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bytes);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.timeout = 15;
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(
                request,
                forceAppCheckRefresh);

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            operation.completed += _ => completion.TrySetResult(true);
            await completion.Task;

            if (request.result != UnityWebRequest.Result.Success)
            {
                string responseBody = request.downloadHandler != null
                    ? request.downloadHandler.text
                    : string.Empty;

                throw new RtdbHttpException(
                    request.responseCode,
                    "method=" + method +
                    " HTTP " + request.responseCode + " " +
                    (request.error ?? "request failed") +
                    (string.IsNullOrWhiteSpace(responseBody)
                        ? string.Empty
                        : " body=" + Truncate(Sanitize(responseBody), 180)));
            }

            return request.downloadHandler != null
                ? request.downloadHandler.text
                : string.Empty;
        }
    }

    private static string ResolveDatabaseUrl()
    {
        try
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;
            object options = null;

            if (app != null)
            {
                PropertyInfo optionsProperty = app.GetType().GetProperty(
                    "Options",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                options = optionsProperty != null
                    ? optionsProperty.GetValue(app)
                    : null;
            }

            if (options != null)
            {
                PropertyInfo property = options.GetType().GetProperty(
                    "DatabaseUrl",
                    BindingFlags.Instance | BindingFlags.Public);

                object value = property != null
                    ? property.GetValue(options)
                    : null;

                string resolved = value != null ? value.ToString() : string.Empty;
                if (!string.IsNullOrWhiteSpace(resolved))
                    return resolved.TrimEnd('/');
            }
        }
        catch
        {
            // Fall back to the known atlasboard-usa Belgium RTDB endpoint.
        }

        return FallbackDatabaseUrl;
    }

    private static string ResolvePlatformKey()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.Android:
                return "android";
            case RuntimePlatform.IPhonePlayer:
                return "ios";
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.WindowsEditor:
                return "windows";
            case RuntimePlatform.OSXPlayer:
            case RuntimePlatform.OSXEditor:
                return "macos";
            case RuntimePlatform.LinuxPlayer:
            case RuntimePlatform.LinuxEditor:
                return "linux";
            default:
                return "other";
        }
    }

    private static string NormalizeLanguageCode(string raw)
    {
        string normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return AtlasBoardAccountConstants.SupportedLanguageCodes.Contains(normalized)
            ? normalized
            : string.Empty;
    }

    private static string NormalizeCountryCode(string raw)
    {
        string normalized = (raw ?? string.Empty).Trim().ToUpperInvariant();
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

    private static string ResolveLanguageCode()
    {
        AtlasBoardLocalizationManager manager = AtlasBoardLocalizationManager.Instance;
        if (manager != null && !string.IsNullOrWhiteSpace(manager.CurrentLanguageCode))
            return manager.CurrentLanguageCode;

        switch (Application.systemLanguage)
        {
            case SystemLanguage.Turkish: return "tr";
            case SystemLanguage.Spanish: return "es";
            case SystemLanguage.French: return "fr";
            case SystemLanguage.German: return "de";
            case SystemLanguage.Korean: return "ko";
            case SystemLanguage.Russian: return "ru";
            default: return "en";
        }
    }

    private static string BuildPlayerId(string uid)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(uid ?? string.Empty));
            StringBuilder builder = new StringBuilder("ATB-");

            for (int i = 0; i < 10 && i < bytes.Length; i++)
                builder.Append(bytes[i].ToString("X2"));

            return builder.ToString();
        }
    }

    private static string GetOrCreateDeviceId()
    {
        string existing = PlayerPrefs.GetString(DeviceIdPrefsKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(existing))
            return existing;

        string created = "D-" + Guid.NewGuid().ToString("N").Substring(0, 20).ToUpperInvariant();
        PlayerPrefs.SetString(DeviceIdPrefsKey, created);
        PlayerPrefs.Save();
        return created;
    }

    private static long ReadLocalFirstSeen(string uid, string platform, string deviceId)
    {
        string key = "atlasboard.identity.deviceFirstSeen." + uid + "." + platform + "." + deviceId;
        string raw = PlayerPrefs.GetString(key, "0");
        return long.TryParse(raw, out long value) ? value : 0L;
    }

    private static void WriteLocalFirstSeen(string uid, string platform, string deviceId, long value)
    {
        string key = "atlasboard.identity.deviceFirstSeen." + uid + "." + platform + "." + deviceId;
        PlayerPrefs.SetString(key, value.ToString());
        PlayerPrefs.Save();
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Length <= max ? value : value.Substring(0, max);
    }

    private static string Short(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "none";
        return value.Length <= 8 ? value : value.Substring(0, 8);
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";
        string cleaned = value.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return cleaned.Length <= 140 ? cleaned : cleaned.Substring(0, 140);
    }
}
