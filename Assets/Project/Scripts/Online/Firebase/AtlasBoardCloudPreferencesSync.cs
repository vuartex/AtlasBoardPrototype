using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Phase 18A. This is the ORIGINAL CloudPreferencesSync component rewritten
/// to use the Spark RTDB at /users/{uid}/settings, without extra helpers.
/// Historic Firestore preferences are READ ONCE only if RTDB settings do not
/// exist, then migrated to RTDB. No new Firestore preference writes.
/// Offline/unsigned: PlayerPrefs remains available; no cloud operation.
/// Passwords, bearer tokens, and player authentication data are never stored.
/// </summary>
[DefaultExecutionOrder(250)]
[DisallowMultipleComponent]
public sealed class AtlasBoardCloudPreferencesSync : MonoBehaviour
{
    private const string DatabaseFallback =
        "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    private const float AccountCheckSeconds = 1f;
    private const float DebounceSeconds = 1.2f;
    private const float RetrySeconds = 15f;

    private AtlasBoardAccountService account;
    private string synchronizedUid = string.Empty;
    private bool busy;
    private bool applyingRemote;
    private bool uploadPending;
    private float nextAccountCheckAt;
    private float uploadAt;
    private float retryAt;
    private string lastSharedFingerprint = string.Empty;
    private string lastPlatformFingerprint = string.Empty;
    private string platform = "other";

    public bool IsSynchronized => !string.IsNullOrWhiteSpace(synchronizedUid) &&
        StillSignedInAs(synchronizedUid);
    public string PlatformKey => platform;

    [Serializable]
    private sealed class SharedData
    {
        public int schemaVersion = 1;
        public string language = "en";
        public string audioJson = "";
        public string gameplayJson = "";
    }

    [Serializable]
    private sealed class PlatformData
    {
        public string graphicsJson = "";
        public string gameplayJson = "";
    }

    [Serializable]
    private sealed class SettingsData
    {
        public int schemaVersion;
        public string language;
        public string audioJson;
        public string gameplayJson;
        public PlatformsData platforms;
    }

    [Serializable]
    private sealed class PlatformsData
    {
        public PlatformData android;
        public PlatformData ios;
        public PlatformData windows;
        public PlatformData macos;
        public PlatformData linux;
    }

    [Serializable]
    private sealed class AudioData
    {
        public AtlasBoardAudioSettingsValues values;
        public bool muted;
    }

    [Serializable]
    private sealed class GlobalGameplayData
    {
        public int botTurnSpeed;
        public bool uiHints;
        public bool gameplayConfirmations;
    }

    [Serializable]
    private sealed class PlatformGameplayData
    {
        public int cameraSensitivity;
        public int cameraZoomSensitivity;
        public int cameraPanSensitivity;
        public bool reduceCameraMotion;
    }

    [Serializable]
    private sealed class GraphicsData
    {
        public AtlasBoardGraphicsSettingsValues values;
    }

    private void Awake()
    {
        platform = ResolvePlatform();
        account = GetComponent<AtlasBoardAccountService>() ??
            AtlasBoardAccountService.Instance;
    }

    private void OnEnable()
    {
        AtlasBoardUserSettingsStore.SettingsSaved += OnLocalSettingsSaved;
        AtlasBoardAudioSettings.SettingsSaved += OnLocalSettingsSaved;
    }

    private void OnDisable()
    {
        AtlasBoardUserSettingsStore.SettingsSaved -= OnLocalSettingsSaved;
        AtlasBoardAudioSettings.SettingsSaved -= OnLocalSettingsSaved;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextAccountCheckAt) return;
        nextAccountCheckAt = Time.unscaledTime + AccountCheckSeconds;
        if (account == null) account = AtlasBoardAccountService.Instance;
        if (account == null || !account.IsInitialized || platform == "other") return;
        if (!account.IsSignedIn || string.IsNullOrWhiteSpace(account.CurrentAccountId))
        {
            synchronizedUid = string.Empty;
            lastSharedFingerprint = string.Empty;
            lastPlatformFingerprint = string.Empty;
            uploadPending = false;
            return;
        }

        string uid = account.CurrentAccountId;
        if (!string.Equals(synchronizedUid, uid, StringComparison.Ordinal))
        {
            if (!busy && Time.unscaledTime >= retryAt)
                _ = LoadOrMigrateAsync(uid);
            return;
        }

        if (uploadPending && !busy && Time.unscaledTime >= uploadAt &&
            Time.unscaledTime >= retryAt)
            _ = UploadAsync(uid);
    }

    private void OnLocalSettingsSaved()
    {
        if (applyingRemote || account == null || !account.IsSignedIn) return;
        uploadPending = true;
        uploadAt = Time.unscaledTime + DebounceSeconds;
    }

    private bool StillSignedInAs(string uid)
    {
        return account != null && account.IsSignedIn &&
            string.Equals(account.CurrentAccountId, uid, StringComparison.Ordinal);
    }

    private async Task LoadOrMigrateAsync(string uid)
    {
        if (busy || !StillSignedInAs(uid)) return;
        busy = true;
        try
        {
            // The existing account creation/sign-in flow owns /users/{uid}.
            // Do not call the user profile mirror with an empty name here: it
            // could replace a real canonical name with a Guest fallback.
            string token = await GetTokenAsync(uid);
            if (string.IsNullOrEmpty(token)) throw new InvalidOperationException("AUTH_UNAVAILABLE");
            string url = SettingsUrl(uid, token);
            string raw = await RequestAsync("GET", url, null);
            if (!StillSignedInAs(uid)) return;

            if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "null")
            {
                bool imported = await ImportLegacyFirestoreOnceAsync(uid);
                if (!StillSignedInAs(uid)) return;
                Debug.Log("ATB_SETTINGS_MIGRATION source=" +
                    (imported ? "legacy_firestore_read_only" : "local_device") +
                    " target=RTDB");
                uploadPending = true;
                uploadAt = 0f;
                synchronizedUid = uid;
                lastSharedFingerprint = string.Empty;
            lastPlatformFingerprint = string.Empty;
            }
            else
            {
                SettingsData dto = JsonUtility.FromJson<SettingsData>(raw);
                if (dto == null || dto.schemaVersion != 1)
                    throw new InvalidOperationException("UNSUPPORTED_RTDB_SETTINGS_SCHEMA");
                ApplyFromRtdb(dto);
                synchronizedUid = uid;
                uploadPending = false;
                lastSharedFingerprint = CurrentSharedFingerprint();
                lastPlatformFingerprint = CurrentPlatformFingerprint();
                Debug.Log("ATB_SETTINGS_SYNC source=RTDB destination=local platform=" + platform);
            }
            retryAt = 0f;
        }
        catch (Exception ex)
        {
            // No fallback write to Firestore. Saved local settings survive.
            retryAt = Time.unscaledTime + RetrySeconds;
            Debug.LogWarning("ATB_SETTINGS_RTDB_READ failed type=" +
                FailureTag(ex) + " local_settings_preserved=true");
        }
        finally
        {
            busy = false;
        }
    }

    private async Task UploadAsync(string uid)
    {
        if (busy || !StillSignedInAs(uid)) return;
        busy = true;
        try
        {
            AtlasBoardUserSettingsValues user = AtlasBoardUserSettingsStore.Load();
            AtlasBoardAudioSettingsValues audio = AtlasBoardAudioSettings.Load();
            string sharedFingerprint = JsonUtility.ToJson(BuildShared(user, audio));
            string platformFingerprint = JsonUtility.ToJson(BuildPlatform(user));
            bool sharedChanged = sharedFingerprint != lastSharedFingerprint;
            bool platformChanged = platformFingerprint != lastPlatformFingerprint;
            if (!sharedChanged && !platformChanged)
            {
                uploadPending = false;
                return;
            }
            string token = await GetTokenAsync(uid);
            if (string.IsNullOrEmpty(token) || !StillSignedInAs(uid)) return;
            string settingsUrl = SettingsUrl(uid, token);

            // Write only the portion that actually changed. In particular,
            // editing Audio does NOT resend the platform Graphics document.
            if (sharedChanged)
            {
                await RequestAsync("PATCH", settingsUrl, sharedFingerprint);
                if (!StillSignedInAs(uid)) return;
                lastSharedFingerprint = sharedFingerprint;
            }
            if (platformChanged)
            {
                string platformUrl = settingsUrl.Replace(
                    ".json?auth=", "/platforms/" + platform + ".json?auth=");
                await RequestAsync("PATCH", platformUrl, platformFingerprint);
                if (!StillSignedInAs(uid)) return;
                lastPlatformFingerprint = platformFingerprint;
            }

            // Avoid dropping a concurrent local Save that occurred while awaiting.
            uploadPending = CurrentSharedFingerprint() != lastSharedFingerprint ||
                            CurrentPlatformFingerprint() != lastPlatformFingerprint;
            if (uploadPending) uploadAt = Time.unscaledTime + DebounceSeconds;
            retryAt = 0f;
            Debug.Log("ATB_SETTINGS_RTDB_WRITE success=true platform=" + platform +
                " firestore_writes=0");
        }
        catch (Exception ex)
        {
            retryAt = Time.unscaledTime + RetrySeconds;
            uploadPending = true;
            Debug.LogWarning("ATB_SETTINGS_RTDB_WRITE failed type=" +
                FailureTag(ex) + " local_settings_preserved=true");
        }
        finally { busy = false; }
    }

    private static SharedData BuildShared(
        AtlasBoardUserSettingsValues user, AtlasBoardAudioSettingsValues audio)
    {
        return new SharedData
        {
            schemaVersion = 1,
            language = AtlasBoardLocalizationLanguages.Normalize(user.LanguageCode),
            audioJson = JsonUtility.ToJson(new AudioData { values = audio, muted = user.AudioMuted }),
            gameplayJson = JsonUtility.ToJson(new GlobalGameplayData
            {
                botTurnSpeed = user.Gameplay.BotTurnSpeed,
                uiHints = user.Gameplay.UiHints,
                gameplayConfirmations = user.Gameplay.GameplayConfirmations
            })
        };
    }

    private static PlatformData BuildPlatform(AtlasBoardUserSettingsValues user)
    {
        return new PlatformData
        {
            graphicsJson = JsonUtility.ToJson(new GraphicsData { values = user.Graphics }),
            gameplayJson = JsonUtility.ToJson(new PlatformGameplayData
            {
                cameraSensitivity = user.Gameplay.CameraSensitivity,
                cameraZoomSensitivity = user.Gameplay.CameraZoomSensitivity,
                cameraPanSensitivity = user.Gameplay.CameraPanSensitivity,
                reduceCameraMotion = user.Gameplay.ReduceCameraMotion
            })
        };
    }

    private static string CurrentSharedFingerprint()
    {
        return JsonUtility.ToJson(BuildShared(
            AtlasBoardUserSettingsStore.Load(), AtlasBoardAudioSettings.Load()));
    }

    private static string CurrentPlatformFingerprint()
    {
        return JsonUtility.ToJson(BuildPlatform(AtlasBoardUserSettingsStore.Load()));
    }

    private void ApplyFromRtdb(SettingsData dto)
    {
        var user = AtlasBoardUserSettingsStore.Load();
        var audio = AtlasBoardAudioSettings.Load();
        if (AtlasBoardAccountConstants.SupportedLanguageCodes.Contains(dto.language ?? ""))
            user.LanguageCode = dto.language;
        if (!string.IsNullOrWhiteSpace(dto.audioJson))
        {
            var a = JsonUtility.FromJson<AudioData>(dto.audioJson);
            if (a != null) { audio = a.values; user.AudioMuted = a.muted; }
        }
        if (!string.IsNullOrWhiteSpace(dto.gameplayJson))
        {
            var g = JsonUtility.FromJson<GlobalGameplayData>(dto.gameplayJson);
            if (g != null)
            {
                user.Gameplay.BotTurnSpeed = g.botTurnSpeed;
                user.Gameplay.UiHints = g.uiHints;
                user.Gameplay.GameplayConfirmations = g.gameplayConfirmations;
            }
        }
        PlatformData p = GetPlatform(dto.platforms);
        if (p != null)
        {
            if (!string.IsNullOrWhiteSpace(p.graphicsJson))
            {
                var g = JsonUtility.FromJson<GraphicsData>(p.graphicsJson);
                if (g != null) user.Graphics = g.values;
            }
            if (!string.IsNullOrWhiteSpace(p.gameplayJson))
            {
                var g = JsonUtility.FromJson<PlatformGameplayData>(p.gameplayJson);
                if (g != null)
                {
                    user.Gameplay.CameraSensitivity = g.cameraSensitivity;
                    user.Gameplay.CameraZoomSensitivity = g.cameraZoomSensitivity;
                    user.Gameplay.CameraPanSensitivity = g.cameraPanSensitivity;
                    user.Gameplay.ReduceCameraMotion = g.reduceCameraMotion;
                }
            }
        }
        ApplySettingsToLocal(user, audio);
    }

    private PlatformData GetPlatform(PlatformsData p)
    {
        if (p == null) return null;
        switch (platform)
        {
            case "android": return p.android;
            case "ios": return p.ios;
            case "windows": return p.windows;
            case "macos": return p.macos;
            case "linux": return p.linux;
            default: return null;
        }
    }

    private void ApplySettingsToLocal(
        AtlasBoardUserSettingsValues user, AtlasBoardAudioSettingsValues audio)
    {
        applyingRemote = true;
        try
        {
            user = AtlasBoardUserSettingsStore.Clamp(user);
            audio = AtlasBoardAudioSettings.Clamp(audio);
            AtlasBoardUserSettingsStore.Save(user);
            AtlasBoardAudioSettings.Save(audio);
            var settings = AtlasBoardSettingsV2Controller.Instance;
            if (settings != null)
                settings.ReloadAndApplyPersistedSettingsFromExternalSync();
            else
            {
                AtlasBoardUserSettingsRuntime.SetCurrent(user);
                AtlasBoardLocalizationManager.Instance?.SetLanguage(user.LanguageCode);
                AtlasBoardAudioManager.Instance?.ApplySettings(audio);
                AudioListener.volume = user.AudioMuted ? 0f : 1f;
            }
        }
        finally { applyingRemote = false; }
    }

    private async Task<bool> ImportLegacyFirestoreOnceAsync(string uid)
    {
        // This method NEVER writes Firestore. It executes only if the RTDB
        // settings subtree was absent. Two bounded legacy document reads.
        try
        {
            FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
            if (db == null) return false;
            var root = await db.Collection("preferences").Document(uid).GetSnapshotAsync();
            var plat = await db.Collection("preferences").Document(uid)
                .Collection("platforms").Document(platform).GetSnapshotAsync();
            if (!StillSignedInAs(uid) || (!root.Exists && !plat.Exists)) return false;

            var u = AtlasBoardUserSettingsStore.Load();
            var a = AtlasBoardAudioSettings.Load();
            var data = root.Exists ? root.ToDictionary() : null;
            var platformData = plat.Exists ? plat.ToDictionary() : null;
            string language = ReadString(data, "language", u.LanguageCode);
            if (AtlasBoardAccountConstants.SupportedLanguageCodes.Contains(language))
                u.LanguageCode = language;
            var aud = ReadMap(data, "audio");
            if (aud != null)
            {
                a.Master = ReadFloat(aud, "master", a.Master);
                a.MainMusic = ReadFloat(aud, "mainMusic", a.MainMusic);
                a.Theme = ReadFloat(aud, "theme", a.Theme);
                a.Dice = ReadFloat(aud, "dice", a.Dice);
                a.Effects = ReadFloat(aud, "effects", a.Effects);
                u.AudioMuted = ReadBool(aud, "muted", u.AudioMuted);
            }
            var global = ReadMap(ReadMap(data, "gameplay"), "global");
            if (global != null)
            {
                u.Gameplay.BotTurnSpeed = ReadInt(global, "botTurnSpeed", u.Gameplay.BotTurnSpeed);
                u.Gameplay.UiHints = ReadBool(global, "uiHints", u.Gameplay.UiHints);
                u.Gameplay.GameplayConfirmations = ReadBool(global, "gameplayConfirmations", u.Gameplay.GameplayConfirmations);
            }
            var graphics = ReadMap(platformData, "graphics") ??
                ReadMap(ReadMap(data, "graphics"), platform);
            if (graphics != null)
            {
                u.Graphics.ResolutionWidth = ReadInt(graphics, "resolutionWidth", u.Graphics.ResolutionWidth);
                u.Graphics.ResolutionHeight = ReadInt(graphics, "resolutionHeight", u.Graphics.ResolutionHeight);
                u.Graphics.RefreshRate = ReadInt(graphics, "refreshRate", u.Graphics.RefreshRate);
                u.Graphics.DisplayMode = ReadInt(graphics, "displayMode", u.Graphics.DisplayMode);
                u.Graphics.QualityPreset = ReadInt(graphics, "qualityPreset", u.Graphics.QualityPreset);
                u.Graphics.VSync = ReadBool(graphics, "vsync", u.Graphics.VSync);
                u.Graphics.FpsLimit = ReadInt(graphics, "fpsLimit", u.Graphics.FpsLimit);
                u.Graphics.ShadowQuality = ReadInt(graphics, "shadowQuality", u.Graphics.ShadowQuality);
                u.Graphics.AntiAliasing = ReadInt(graphics, "antiAliasing", u.Graphics.AntiAliasing);
                u.Graphics.ShowFps = ReadBool(graphics, "showFps", u.Graphics.ShowFps);
            }
            var gp = ReadMap(platformData, "gameplay") ??
                ReadMap(ReadMap(data, "gameplay"), platform);
            if (gp != null)
            {
                u.Gameplay.CameraSensitivity = ReadInt(gp, "cameraSensitivity", u.Gameplay.CameraSensitivity);
                u.Gameplay.CameraZoomSensitivity = ReadInt(gp, "cameraZoomSensitivity", u.Gameplay.CameraZoomSensitivity);
                u.Gameplay.CameraPanSensitivity = ReadInt(gp, "cameraPanSensitivity", u.Gameplay.CameraPanSensitivity);
                u.Gameplay.ReduceCameraMotion = ReadBool(gp, "reduceCameraMotion", u.Gameplay.ReduceCameraMotion);
            }
            ApplySettingsToLocal(u, a);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_SETTINGS_LEGACY_IMPORT failed type=" +
                ex.GetType().Name + " preserving_local=true");
            return false;
        }
    }

    private static Dictionary<string, object> ReadMap(
        Dictionary<string, object> parent, string key)
    {
        if (parent == null || !parent.TryGetValue(key, out object value)) return null;
        if (value is Dictionary<string, object> dict) return dict;
        if (value is IDictionary<string, object> other)
            return new Dictionary<string, object>(other);
        return null;
    }
    private static int ReadInt(Dictionary<string, object> d, string key, int fallback)
    {
        if (d == null || !d.TryGetValue(key, out object v)) return fallback;
        try { return Convert.ToInt32(v); } catch { return fallback; }
    }
    private static float ReadFloat(Dictionary<string, object> d, string key, float fallback)
    {
        if (d == null || !d.TryGetValue(key, out object v)) return fallback;
        try { return Convert.ToSingle(v); } catch { return fallback; }
    }
    private static bool ReadBool(Dictionary<string, object> d, string key, bool fallback)
    {
        if (d == null || !d.TryGetValue(key, out object v)) return fallback;
        if (v is bool flag) return flag;
        try { return Convert.ToInt32(v) != 0; } catch { return fallback; }
    }
    private static string ReadString(Dictionary<string, object> d, string key, string fallback)
    {
        return d != null && d.TryGetValue(key, out object v) && v is string s ? s : fallback;
    }

    private static async Task<string> GetTokenAsync(string uid)
    {
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        FirebaseUser user = auth != null ? auth.CurrentUser : null;
        if (user == null || user.UserId != uid) return string.Empty;
        return await user.TokenAsync(false);
    }

    private static string SettingsUrl(string uid, string token)
    {
        // The project has one RTDB instance, already created in Belgium.
        // Fixing the URL also avoids platform-specific FirebaseAppOptions API
        // availability differences in Unity Firebase SDK builds.
        return DatabaseFallback + "/users/" + UnityWebRequest.EscapeURL(uid) +
            "/settings.json?auth=" + UnityWebRequest.EscapeURL(token);
    }

    private static async Task<string> RequestAsync(string method, string url, string body)
    {
        using (var req = new UnityWebRequest(url, method))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 12;
            if (body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            var completion = new TaskCompletionSource<bool>();
            var op = req.SendWebRequest();
            if (op.isDone) completion.TrySetResult(true);
            else op.completed += _ => completion.TrySetResult(true);
            await completion.Task;
            if (req.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException("RTDB_HTTP_" + req.responseCode);
            return req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
        }
    }

    private static string FailureTag(Exception error)
    {
        string message = error != null ? error.Message ?? "" : "";
        // Never expose a request URL, identity token, or secret to logs.
        if (message.StartsWith("RTDB_HTTP_", StringComparison.Ordinal))
            return message;
        return error != null ? error.GetType().Name : "Unknown";
    }

    private static string ResolvePlatform()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.Android: return "android";
            case RuntimePlatform.IPhonePlayer: return "ios";
            case RuntimePlatform.WindowsEditor:
            case RuntimePlatform.WindowsPlayer: return "windows";
            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.OSXPlayer: return "macos";
            case RuntimePlatform.LinuxEditor:
            case RuntimePlatform.LinuxPlayer: return "linux";
            default: return "other";
        }
    }
}
