using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

// Security Core 06 v2. One bounded Firestore document per supported platform.
// Root preferences retain language, audio, crossplay and global gameplay;
// legacy root graphics / platform gameplay are read as migration input only.
[DefaultExecutionOrder(250)]
[DisallowMultipleComponent]
public sealed class AtlasBoardCloudPreferencesSync : MonoBehaviour
{
    private const float AccountPollIntervalSeconds = 0.50f;
    private const float UploadDebounceSeconds = 0.75f;
    private const float FailedSyncRetrySeconds = 5.00f;

    private AtlasBoardAccountService accountService;
    private string syncedAccountId = string.Empty;
    private bool applyingCloudToLocal;
    private bool syncBusy;
    private bool uploadPending;
    private float nextAccountPollTime;
    private float nextUploadTime;
    private float nextSyncRetryTime;
    private string platformKey = "unknown";

    public bool IsSynchronized =>
        !string.IsNullOrWhiteSpace(syncedAccountId) &&
        accountService != null &&
        accountService.IsSignedIn &&
        string.Equals(syncedAccountId, accountService.CurrentAccountId,
            StringComparison.Ordinal);

    public string PlatformKey => platformKey;

    private void Awake()
    {
        platformKey = ResolvePlatformKey();
        accountService = GetComponent<AtlasBoardAccountService>() ??
            AtlasBoardAccountService.Instance;
    }

    private void OnEnable()
    {
        AtlasBoardUserSettingsStore.SettingsSaved += HandleLocalSettingsSaved;
        AtlasBoardAudioSettings.SettingsSaved += HandleLocalSettingsSaved;
    }

    private void OnDisable()
    {
        AtlasBoardUserSettingsStore.SettingsSaved -= HandleLocalSettingsSaved;
        AtlasBoardAudioSettings.SettingsSaved -= HandleLocalSettingsSaved;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextAccountPollTime) return;
        nextAccountPollTime = Time.unscaledTime + AccountPollIntervalSeconds;

        if (accountService == null)
            accountService = AtlasBoardAccountService.Instance;
        if (accountService == null || !accountService.IsInitialized) return;
        if (!accountService.IsSignedIn)
        {
            ResetSignedInState();
            return;
        }

        string accountId = accountService.CurrentAccountId;
        if (string.IsNullOrWhiteSpace(accountId)) return;
        if (!string.Equals(syncedAccountId, accountId,
                StringComparison.Ordinal))
        {
            if (!syncBusy && Time.unscaledTime >= nextSyncRetryTime)
                _ = SynchronizeAfterSignInAsync(accountId);
            return;
        }
        if (uploadPending && !syncBusy && Time.unscaledTime >= nextUploadTime)
            _ = UploadCurrentLocalPreferencesAsync();
    }

    private void HandleLocalSettingsSaved()
    {
        if (applyingCloudToLocal || accountService == null ||
            !accountService.IsInitialized || !accountService.IsSignedIn)
            return;
        uploadPending = true;
        nextUploadTime = Time.unscaledTime + UploadDebounceSeconds;
    }

    private bool SupportedPlatform =>
        platformKey == "android" || platformKey == "ios" ||
        platformKey == "windows" || platformKey == "macos" ||
        platformKey == "linux";

    private DocumentReference PlatformDocument(string uid)
    {
        return FirebaseFirestore.DefaultInstance.Collection("preferences")
            .Document(uid).Collection("platforms").Document(platformKey);
    }

    private async Task SynchronizeAfterSignInAsync(string accountId)
    {
        if (syncBusy || accountService == null) return;
        syncBusy = true;
        try
        {
            if (!SupportedPlatform)
                throw new InvalidOperationException(
                    "Unsupported Settings platform: " + platformKey);

            AtlasAccountSnapshot snapshot = await accountService.LoadCurrentAccountAsync();
            if (snapshot == null || !string.Equals(snapshot.AccountId, accountId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException("Account documents not ready.");

            // Only the CURRENT platform is read. No cross-device collection
            // scans or wholesale nested map rewrites are performed.
            DocumentSnapshot platformSnapshot =
                await PlatformDocument(accountId).GetSnapshotAsync();
            AtlasCloudPreferences legacy = snapshot.Preferences ??
                new AtlasCloudPreferences();
            bool missing = MergeCloudIntoLocal(legacy,
                platformSnapshot.Exists ? platformSnapshot.ToDictionary() : null);

            if (missing || !platformSnapshot.Exists)
                await PersistCurrentSettingsAsync(accountId, legacy);

            syncedAccountId = accountId;
            uploadPending = false;
            nextSyncRetryTime = 0f;
            await SyncUserLocaleAsync(snapshot);
            Debug.Log("AtlasBoard Cloud Preferences v2 ready. Platform=" +
                platformKey + "; root shared + isolated platform document.", this);
        }
        catch (Exception exception)
        {
            nextSyncRetryTime = Time.unscaledTime + FailedSyncRetrySeconds;
            Debug.LogWarning("AtlasBoard Cloud Preferences v2 sign-in sync failed. " +
                "Local settings remain active; retry scheduled. " +
                exception.Message, this);
        }
        finally { syncBusy = false; }
    }

    private async Task UploadCurrentLocalPreferencesAsync()
    {
        if (syncBusy || accountService == null || !accountService.IsSignedIn)
            return;
        syncBusy = true;
        try
        {
            string uid = accountService.CurrentAccountId;
            if (!SupportedPlatform || string.IsNullOrWhiteSpace(uid))
                throw new InvalidOperationException("Unsupported or unsigned platform.");

            // AccountSnapshot is used only for shared metadata and locale.
            // The new platform write never includes other platform documents.
            AtlasAccountSnapshot snapshot =
                await accountService.LoadCurrentAccountAsync();
            if (snapshot == null || snapshot.AccountId != uid)
                throw new InvalidOperationException("Account snapshot unavailable.");
            await PersistCurrentSettingsAsync(uid,
                snapshot.Preferences ?? new AtlasCloudPreferences());
            uploadPending = false;
            await SyncUserLocaleAsync(snapshot);
        }
        catch (Exception exception)
        {
            uploadPending = true;
            nextUploadTime = Time.unscaledTime + FailedSyncRetrySeconds;
            Debug.LogWarning("AtlasBoard Cloud Preferences v2 upload failed. " +
                "Local Settings retained; retry scheduled. " +
                exception.Message, this);
        }
        finally { syncBusy = false; }
    }

    private async Task PersistCurrentSettingsAsync(string uid,
        AtlasCloudPreferences existing)
    {
        AtlasBoardUserSettingsValues user = AtlasBoardUserSettingsStore.Load();
        AtlasBoardAudioSettingsValues audio = AtlasBoardAudioSettings.Load();

        // MergeAll for root preserves legacy graphics and other platforms'
        // historic gameplay maps. Rules permit only global gameplay changes.
        Dictionary<string, object> root = new Dictionary<string, object>
        {
            { "language", AtlasBoardLocalizationLanguages.Normalize(user.LanguageCode) },
            { "audio", BuildAudioMap(audio, user.AudioMuted) },
            { "gameplay", new Dictionary<string, object>
                { { "global", BuildGlobalGameplayMap(user.Gameplay) } } },
            { "updatedAt", FieldValue.ServerTimestamp }
        };

        // Keep the user's saved crossplay choice. This component doesn't
        // currently expose its own crossplay toggle.
        if (existing != null &&
            !string.IsNullOrWhiteSpace(existing.CrossplayPreference))
            root["crossplayPreference"] = existing.CrossplayPreference;

        Dictionary<string, object> platform = new Dictionary<string, object>
        {
            { "schemaVersion", 1 },
            { "graphics", BuildGraphicsMap(user.Graphics) },
            { "gameplay", BuildPlatformGameplayMap(user.Gameplay) },
            { "updatedAt", FieldValue.ServerTimestamp }
        };

        FirebaseFirestore db = FirebaseFirestore.DefaultInstance;
        WriteBatch batch = db.StartBatch();
        batch.Set(db.Collection("preferences").Document(uid),
            root, SetOptions.MergeAll);
        batch.Set(PlatformDocument(uid), platform, SetOptions.Overwrite);
        await batch.CommitAsync();
    }

    private async Task SyncUserLocaleAsync(AtlasAccountSnapshot snapshot)
    {
        if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.CountryCode))
            return;
        string language = AtlasBoardLocalizationLanguages.Normalize(
            AtlasBoardUserSettingsStore.Load().LanguageCode);
        if (!string.Equals(snapshot.PreferredLanguage, language,
                StringComparison.OrdinalIgnoreCase))
            await accountService.UpdateAccountLocaleAsync(
                snapshot.CountryCode, language);
    }

    private bool MergeCloudIntoLocal(AtlasCloudPreferences root,
        Dictionary<string, object> platformDoc)
    {
        AtlasBoardUserSettingsValues user = AtlasBoardUserSettingsStore.Load();
        AtlasBoardAudioSettingsValues audio = AtlasBoardAudioSettings.Load();
        bool missing = false;

        if (root != null && AtlasBoardAccountConstants.SupportedLanguageCodes
            .Contains(root.Language))
            user.LanguageCode = AtlasBoardLocalizationLanguages.Normalize(root.Language);
        else missing = true;

        if (root != null && HasAudioSection(root.Audio))
            ApplyAudioMap(root.Audio, ref audio, ref user.AudioMuted);
        else missing = true;

        Dictionary<string, object> global;
        if (root != null && TryGetNestedMap(root.Gameplay, "global", out global)
            && global.Count > 0)
            ApplyGlobalGameplayMap(global, ref user.Gameplay);
        else missing = true;

        // New v2 platform doc wins. If missing, migrate the v1 ROOT platform
        // map for this OS, or seed from this device's existing PlayerPrefs.
        Dictionary<string, object> graphics;
        if (TryGetNestedMap(platformDoc, "graphics", out graphics)
            && graphics.Count > 0)
            ApplyGraphicsMap(graphics, ref user.Graphics);
        else
        {
            missing = true;
            if (root != null && TryGetNestedMap(root.Graphics, platformKey,
                    out graphics) && graphics.Count > 0)
                ApplyGraphicsMap(graphics, ref user.Graphics);
        }

        Dictionary<string, object> gameplay;
        if (TryGetNestedMap(platformDoc, "gameplay", out gameplay)
            && gameplay.Count > 0)
            ApplyPlatformGameplayMap(gameplay, ref user.Gameplay);
        else
        {
            missing = true;
            if (root != null && TryGetNestedMap(root.Gameplay, platformKey,
                    out gameplay) && gameplay.Count > 0)
                ApplyPlatformGameplayMap(gameplay, ref user.Gameplay);
        }

        user = AtlasBoardUserSettingsStore.Clamp(user);
        audio = AtlasBoardAudioSettings.Clamp(audio);
        applyingCloudToLocal = true;
        try
        {
            AtlasBoardAudioSettings.Save(audio);
            AtlasBoardUserSettingsStore.Save(user);
            ApplyPersistedLocalSettingsToRuntime(user, audio);
        }
        finally { applyingCloudToLocal = false; }
        return missing;
    }

    private static Dictionary<string, object> BuildAudioMap(
        AtlasBoardAudioSettingsValues audio, bool muted)
    {
        return new Dictionary<string, object> {
            { "master", audio.Master }, { "mainMusic", audio.MainMusic },
            { "theme", audio.Theme }, { "dice", audio.Dice },
            { "effects", audio.Effects }, { "muted", muted }
        };
    }

    private static Dictionary<string, object> BuildGraphicsMap(
        AtlasBoardGraphicsSettingsValues graphics)
    {
        return new Dictionary<string, object> {
            { "resolutionWidth", graphics.ResolutionWidth },
            { "resolutionHeight", graphics.ResolutionHeight },
            { "refreshRate", graphics.RefreshRate },
            { "displayMode", graphics.DisplayMode },
            { "qualityPreset", graphics.QualityPreset },
            { "vsync", graphics.VSync },
            { "fpsLimit", graphics.FpsLimit },
            { "shadowQuality", graphics.ShadowQuality },
            { "antiAliasing", graphics.AntiAliasing },
            { "showFps", graphics.ShowFps }
        };
    }

    private static Dictionary<string, object> BuildGlobalGameplayMap(
        AtlasBoardGameplaySettingsValues gameplay)
    {
        return new Dictionary<string, object> {
            { "botTurnSpeed", gameplay.BotTurnSpeed },
            { "uiHints", gameplay.UiHints },
            { "gameplayConfirmations", gameplay.GameplayConfirmations }
        };
    }

    private static Dictionary<string, object> BuildPlatformGameplayMap(
        AtlasBoardGameplaySettingsValues gameplay)
    {
        return new Dictionary<string, object> {
            { "cameraSensitivity", gameplay.CameraSensitivity },
            { "cameraZoomSensitivity", gameplay.CameraZoomSensitivity },
            { "cameraPanSensitivity", gameplay.CameraPanSensitivity },
            { "reduceCameraMotion", gameplay.ReduceCameraMotion }
        };
    }

    private static bool HasAudioSection(Dictionary<string, object> section)
    {
        return section != null && section.Count > 0 &&
            (section.ContainsKey("master") ||
             section.ContainsKey("mainMusic") || section.ContainsKey("effects"));
    }

    private static bool TryGetNestedMap(Dictionary<string, object> parent,
        string key, out Dictionary<string, object> result)
    {
        result = null;
        if (parent == null || !parent.TryGetValue(key, out object value) ||
            value == null) return false;
        if (value is Dictionary<string, object> known)
        {
            result = known;
            return true;
        }
        if (value is IDictionary<string, object> other)
        {
            result = new Dictionary<string, object>(other);
            return true;
        }
        return false;
    }

    private static int GetInt(Dictionary<string, object> section,
        string key, int fallback)
    {
        if (section == null || !section.TryGetValue(key, out object value) ||
            value == null) return fallback;
        try { return Convert.ToInt32(value); }
        catch { return fallback; }
    }

    private static float GetFloat(Dictionary<string, object> section,
        string key, float fallback)
    {
        if (section == null || !section.TryGetValue(key, out object value) ||
            value == null) return fallback;
        try { return Convert.ToSingle(value); }
        catch { return fallback; }
    }

    private static bool GetBool(Dictionary<string, object> section,
        string key, bool fallback)
    {
        if (section == null || !section.TryGetValue(key, out object value) ||
            value == null) return fallback;
        if (value is bool b) return b;
        try { return Convert.ToInt32(value) != 0; }
        catch
        {
            return bool.TryParse(value.ToString(), out bool parsed)
                ? parsed : fallback;
        }
    }

    private static void ApplyAudioMap(Dictionary<string, object> m,
        ref AtlasBoardAudioSettingsValues a, ref bool muted)
    {
        a.Master = GetFloat(m, "master", a.Master);
        a.MainMusic = GetFloat(m, "mainMusic", a.MainMusic);
        a.Theme = GetFloat(m, "theme", a.Theme);
        a.Dice = GetFloat(m, "dice", a.Dice);
        a.Effects = GetFloat(m, "effects", a.Effects);
        muted = GetBool(m, "muted", muted);
    }

    private static void ApplyGraphicsMap(Dictionary<string, object> m,
        ref AtlasBoardGraphicsSettingsValues g)
    {
        g.ResolutionWidth = GetInt(m, "resolutionWidth", g.ResolutionWidth);
        g.ResolutionHeight = GetInt(m, "resolutionHeight", g.ResolutionHeight);
        g.RefreshRate = GetInt(m, "refreshRate", g.RefreshRate);
        g.DisplayMode = GetInt(m, "displayMode", g.DisplayMode);
        g.QualityPreset = GetInt(m, "qualityPreset", g.QualityPreset);
        g.VSync = GetBool(m, "vsync", g.VSync);
        g.FpsLimit = GetInt(m, "fpsLimit", g.FpsLimit);
        g.ShadowQuality = GetInt(m, "shadowQuality", g.ShadowQuality);
        g.AntiAliasing = GetInt(m, "antiAliasing", g.AntiAliasing);
        g.ShowFps = GetBool(m, "showFps", g.ShowFps);
    }

    private static void ApplyGlobalGameplayMap(Dictionary<string, object> m,
        ref AtlasBoardGameplaySettingsValues g)
    {
        g.BotTurnSpeed = GetInt(m, "botTurnSpeed", g.BotTurnSpeed);
        g.UiHints = GetBool(m, "uiHints", g.UiHints);
        g.GameplayConfirmations = GetBool(m, "gameplayConfirmations",
            g.GameplayConfirmations);
    }

    private static void ApplyPlatformGameplayMap(Dictionary<string, object> m,
        ref AtlasBoardGameplaySettingsValues g)
    {
        g.CameraSensitivity = GetInt(m, "cameraSensitivity", g.CameraSensitivity);
        g.CameraZoomSensitivity = GetInt(m, "cameraZoomSensitivity",
            g.CameraZoomSensitivity);
        g.CameraPanSensitivity = GetInt(m, "cameraPanSensitivity",
            g.CameraPanSensitivity);
        g.ReduceCameraMotion = GetBool(m, "reduceCameraMotion",
            g.ReduceCameraMotion);
    }

    private void ApplyPersistedLocalSettingsToRuntime(
        AtlasBoardUserSettingsValues user, AtlasBoardAudioSettingsValues audio)
    {
        AtlasBoardSettingsV2Controller controller =
            AtlasBoardSettingsV2Controller.Instance;
        if (controller != null)
        {
            controller.ReloadAndApplyPersistedSettingsFromExternalSync();
            return;
        }
        AtlasBoardUserSettingsRuntime.SetCurrent(user);
        AtlasBoardLocalizationManager.Instance?.SetLanguage(user.LanguageCode);
        AtlasBoardAudioManager.Instance?.ApplySettings(audio);
        AudioListener.volume = user.AudioMuted ? 0f : 1f;
    }

    private void ResetSignedInState()
    {
        syncedAccountId = string.Empty;
        uploadPending = false;
        nextSyncRetryTime = 0f;
    }

    private static string ResolvePlatformKey()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.WindowsEditor:
            case RuntimePlatform.WindowsPlayer: return "windows";
            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.OSXPlayer: return "macos";
            case RuntimePlatform.LinuxEditor:
            case RuntimePlatform.LinuxPlayer: return "linux";
            case RuntimePlatform.Android: return "android";
            case RuntimePlatform.IPhonePlayer: return "ios";
            default: return "unknown"; // fail closed pending schema review
        }
    }
}
