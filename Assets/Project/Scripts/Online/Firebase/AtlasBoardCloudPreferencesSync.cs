using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DefaultExecutionOrder(250)]
[DisallowMultipleComponent]
public sealed class AtlasBoardCloudPreferencesSync : MonoBehaviour
{
    private const float AccountPollIntervalSeconds = 0.50f;
    private const float UploadDebounceSeconds = 0.75f;
    private const float FailedSyncRetrySeconds = 5.00f;

    private AtlasBoardAccountService accountService;

    private string syncedAccountId = string.Empty;
    private AtlasCloudPreferences cachedCloudPreferences;

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
        string.Equals(
            syncedAccountId,
            accountService.CurrentAccountId,
            StringComparison.Ordinal);

    public string PlatformKey => platformKey;

    private void Awake()
    {
        platformKey = ResolvePlatformKey();
        accountService =
            GetComponent<AtlasBoardAccountService>();

        if (accountService == null)
        {
            accountService =
                AtlasBoardAccountService.Instance;
        }
    }

    private void OnEnable()
    {
        AtlasBoardUserSettingsStore.SettingsSaved +=
            HandleLocalSettingsSaved;

        AtlasBoardAudioSettings.SettingsSaved +=
            HandleLocalSettingsSaved;
    }

    private void OnDisable()
    {
        AtlasBoardUserSettingsStore.SettingsSaved -=
            HandleLocalSettingsSaved;

        AtlasBoardAudioSettings.SettingsSaved -=
            HandleLocalSettingsSaved;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextAccountPollTime)
        {
            return;
        }

        nextAccountPollTime =
            Time.unscaledTime +
            AccountPollIntervalSeconds;

        if (accountService == null)
        {
            accountService =
                AtlasBoardAccountService.Instance;

            if (accountService == null)
            {
                return;
            }
        }

        if (!accountService.IsInitialized)
        {
            return;
        }

        if (!accountService.IsSignedIn)
        {
            ResetSignedInState();
            return;
        }

        string accountId =
            accountService.CurrentAccountId;

        if (string.IsNullOrWhiteSpace(accountId))
        {
            return;
        }

        if (!string.Equals(
                syncedAccountId,
                accountId,
                StringComparison.Ordinal))
        {
            if (!syncBusy &&
                Time.unscaledTime >=
                nextSyncRetryTime)
            {
                _ = SynchronizeAfterSignInAsync(
                    accountId);
            }

            return;
        }

        if (uploadPending &&
            !syncBusy &&
            Time.unscaledTime >=
            nextUploadTime)
        {
            _ = UploadCurrentLocalPreferencesAsync();
        }
    }

    private void HandleLocalSettingsSaved()
    {
        if (applyingCloudToLocal)
        {
            return;
        }

        if (accountService == null ||
            !accountService.IsInitialized ||
            !accountService.IsSignedIn)
        {
            return;
        }

        uploadPending = true;
        nextUploadTime =
            Time.unscaledTime +
            UploadDebounceSeconds;
    }

    private async Task SynchronizeAfterSignInAsync(
        string accountId)
    {
        if (syncBusy ||
            accountService == null)
        {
            return;
        }

        syncBusy = true;

        try
        {
            AtlasAccountSnapshot snapshot =
                await accountService
                    .LoadCurrentAccountAsync();

            if (snapshot == null ||
                !string.Equals(
                    snapshot.AccountId,
                    accountId,
                    StringComparison.Ordinal))
            {
                nextSyncRetryTime =
                    Time.unscaledTime +
                    FailedSyncRetrySeconds;

                Debug.LogWarning(
                    "AtlasBoard Cloud Preferences could not load the " +
                    "signed-in account yet. Local PlayerPrefs remain " +
                    "active and cloud sync will retry automatically.",
                    this);

                return;
            }

            AtlasCloudPreferences remote =
                snapshot.Preferences ??
                new AtlasCloudPreferences();

            bool cloudWasCompletedFromLocal =
                MergeRemoteIntoLocal(
                    remote);

            cachedCloudPreferences =
                BuildCloudPreferencesFromCurrentLocal(
                    remote);

            if (cloudWasCompletedFromLocal)
            {
                AtlasAccountOperationResult seedResult =
                    await accountService
                        .SaveCloudPreferencesAsync(
                            cachedCloudPreferences);

                if (!seedResult.Success)
                {
                    Debug.LogWarning(
                        "AtlasBoard Cloud Preferences loaded successfully, " +
                        "but missing local sections could not be seeded to " +
                        "Firestore yet. " +
                        seedResult.TechnicalMessage,
                        this);
                }
            }

            string localLanguage =
                AtlasBoardLocalizationLanguages.Normalize(
                    AtlasBoardUserSettingsStore
                        .Load()
                        .LanguageCode);

            if (!string.Equals(
                    snapshot.PreferredLanguage,
                    localLanguage,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(
                    snapshot.CountryCode))
            {
                await accountService.UpdateAccountLocaleAsync(
                    snapshot.CountryCode,
                    localLanguage);
            }

            syncedAccountId = accountId;
            uploadPending = false;
            nextSyncRetryTime = 0f;

            Debug.Log(
                "AtlasBoard Cloud Preferences sync ready. " +
                $"UID={accountId}; platform={platformKey}. " +
                "Language/audio/global gameplay use shared cloud values; " +
                "graphics and camera controls are platform-scoped. " +
                "Missing platform sections are seeded from existing " +
                "PlayerPrefs without moving or replacing the Settings UI.",
                this);
        }
        catch (Exception exception)
        {
            nextSyncRetryTime =
                Time.unscaledTime +
                FailedSyncRetrySeconds;

            Debug.LogWarning(
                "AtlasBoard Cloud Preferences initial sync failed. " +
                "Local settings remain active; retry scheduled. " +
                exception.Message,
                this);
        }
        finally
        {
            syncBusy = false;
        }
    }

    private bool MergeRemoteIntoLocal(
        AtlasCloudPreferences remote)
    {
        AtlasBoardUserSettingsValues localUser =
            AtlasBoardUserSettingsStore.Load();

        AtlasBoardAudioSettingsValues localAudio =
            AtlasBoardAudioSettings.Load();

        bool seededAnyMissingSection = false;

        if (remote != null &&
            AtlasBoardAccountConstants
                .SupportedLanguageCodes
                .Contains(remote.Language))
        {
            localUser.LanguageCode =
                AtlasBoardLocalizationLanguages.Normalize(
                    remote.Language);
        }
        else
        {
            seededAnyMissingSection = true;
        }

        if (remote != null &&
            HasAudioSection(remote.Audio))
        {
            ApplyAudioMap(
                remote.Audio,
                ref localAudio,
                ref localUser.AudioMuted);
        }
        else
        {
            seededAnyMissingSection = true;
        }

        Dictionary<string, object> remoteGraphics =
            remote != null
                ? remote.Graphics
                : null;

        if (TryGetNestedMap(
                remoteGraphics,
                platformKey,
                out Dictionary<string, object>
                    platformGraphics) &&
            platformGraphics.Count > 0)
        {
            ApplyGraphicsMap(
                platformGraphics,
                ref localUser.Graphics);
        }
        else
        {
            seededAnyMissingSection = true;
        }

        Dictionary<string, object> remoteGameplay =
            remote != null
                ? remote.Gameplay
                : null;

        if (TryGetNestedMap(
                remoteGameplay,
                "global",
                out Dictionary<string, object>
                    globalGameplay) &&
            globalGameplay.Count > 0)
        {
            ApplyGlobalGameplayMap(
                globalGameplay,
                ref localUser.Gameplay);
        }
        else
        {
            seededAnyMissingSection = true;
        }

        if (TryGetNestedMap(
                remoteGameplay,
                platformKey,
                out Dictionary<string, object>
                    platformGameplay) &&
            platformGameplay.Count > 0)
        {
            ApplyPlatformGameplayMap(
                platformGameplay,
                ref localUser.Gameplay);
        }
        else
        {
            seededAnyMissingSection = true;
        }

        localUser =
            AtlasBoardUserSettingsStore.Clamp(
                localUser);

        localAudio =
            AtlasBoardAudioSettings.Clamp(
                localAudio);

        applyingCloudToLocal = true;

        try
        {
            AtlasBoardAudioSettings.Save(
                localAudio);

            AtlasBoardUserSettingsStore.Save(
                localUser);

            ApplyPersistedLocalSettingsToRuntime(
                localUser,
                localAudio);
        }
        finally
        {
            applyingCloudToLocal = false;
        }

        return seededAnyMissingSection;
    }

    private async Task UploadCurrentLocalPreferencesAsync()
    {
        if (syncBusy ||
            accountService == null ||
            !accountService.IsSignedIn)
        {
            return;
        }

        syncBusy = true;

        try
        {
            // Re-read the newest remote document before every write so a
            // Windows client never erases Android/iOS platform sections and
            // vice versa.
            AtlasAccountSnapshot latestSnapshot =
                await accountService
                    .LoadCurrentAccountAsync();

            AtlasCloudPreferences basis =
                latestSnapshot != null &&
                latestSnapshot.Preferences != null
                    ? latestSnapshot.Preferences
                    : cachedCloudPreferences ??
                      new AtlasCloudPreferences();

            AtlasCloudPreferences outgoing =
                BuildCloudPreferencesFromCurrentLocal(
                    basis);

            AtlasAccountOperationResult result =
                await accountService
                    .SaveCloudPreferencesAsync(
                        outgoing);

            if (!result.Success)
            {
                uploadPending = true;
                nextUploadTime =
                    Time.unscaledTime +
                    FailedSyncRetrySeconds;

                Debug.LogWarning(
                    "AtlasBoard Cloud Preferences upload failed. " +
                    "Local PlayerPrefs were already saved; cloud retry " +
                    "will occur automatically. " +
                    result.TechnicalMessage,
                    this);

                return;
            }

            cachedCloudPreferences = outgoing;
            uploadPending = false;

            if (latestSnapshot != null)
            {
                string localLanguage =
                    AtlasBoardLocalizationLanguages.Normalize(
                        outgoing.Language);

                if (!string.Equals(
                        latestSnapshot.PreferredLanguage,
                        localLanguage,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(
                        latestSnapshot.CountryCode))
                {
                    await accountService.UpdateAccountLocaleAsync(
                        latestSnapshot.CountryCode,
                        localLanguage);
                }
            }

            Debug.Log(
                "AtlasBoard Cloud Preferences updated from local " +
                $"Settings. UID={accountService.CurrentAccountId}; " +
                $"platform={platformKey}.",
                this);
        }
        catch (Exception exception)
        {
            uploadPending = true;
            nextUploadTime =
                Time.unscaledTime +
                FailedSyncRetrySeconds;

            Debug.LogWarning(
                "AtlasBoard Cloud Preferences upload encountered a " +
                "temporary failure. Local PlayerPrefs remain authoritative " +
                "for this device until retry. " +
                exception.Message,
                this);
        }
        finally
        {
            syncBusy = false;
        }
    }

    private AtlasCloudPreferences
        BuildCloudPreferencesFromCurrentLocal(
            AtlasCloudPreferences basis)
    {
        AtlasBoardUserSettingsValues user =
            AtlasBoardUserSettingsStore.Load();

        AtlasBoardAudioSettingsValues audio =
            AtlasBoardAudioSettings.Load();

        AtlasCloudPreferences result =
            basis ??
            new AtlasCloudPreferences();

        result.Language =
            AtlasBoardLocalizationLanguages.Normalize(
                user.LanguageCode);

        result.Audio =
            BuildAudioMap(
                audio,
                user.AudioMuted);

        result.Graphics =
            CloneMap(result.Graphics);

        result.Graphics[platformKey] =
            BuildGraphicsMap(
                user.Graphics);

        result.Gameplay =
            CloneMap(result.Gameplay);

        result.Gameplay["global"] =
            BuildGlobalGameplayMap(
                user.Gameplay);

        result.Gameplay[platformKey] =
            BuildPlatformGameplayMap(
                user.Gameplay);

        // Controls do not yet have a shipping Atlas Board Settings model.
        // Preserve any future/mobile/Steam data instead of replacing it.
        result.Controls =
            CloneMap(result.Controls);

        if (string.IsNullOrWhiteSpace(
                result.CrossplayPreference))
        {
            result.CrossplayPreference =
                AtlasBoardAccountConstants
                    .DefaultCrossplayPreference;
        }

        return result;
    }

    private static Dictionary<string, object>
        BuildAudioMap(
            AtlasBoardAudioSettingsValues audio,
            bool muted)
    {
        return new Dictionary<string, object>
        {
            { "master", audio.Master },
            { "mainMusic", audio.MainMusic },
            { "theme", audio.Theme },
            { "dice", audio.Dice },
            { "effects", audio.Effects },
            { "muted", muted }
        };
    }

    private static Dictionary<string, object>
        BuildGraphicsMap(
            AtlasBoardGraphicsSettingsValues graphics)
    {
        return new Dictionary<string, object>
        {
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

    private static Dictionary<string, object>
        BuildGlobalGameplayMap(
            AtlasBoardGameplaySettingsValues gameplay)
    {
        return new Dictionary<string, object>
        {
            { "botTurnSpeed", gameplay.BotTurnSpeed },
            { "uiHints", gameplay.UiHints },
            {
                "gameplayConfirmations",
                gameplay.GameplayConfirmations
            }
        };
    }

    private static Dictionary<string, object>
        BuildPlatformGameplayMap(
            AtlasBoardGameplaySettingsValues gameplay)
    {
        return new Dictionary<string, object>
        {
            {
                "cameraSensitivity",
                gameplay.CameraSensitivity
            },
            {
                "cameraZoomSensitivity",
                gameplay.CameraZoomSensitivity
            },
            {
                "cameraPanSensitivity",
                gameplay.CameraPanSensitivity
            },
            {
                "reduceCameraMotion",
                gameplay.ReduceCameraMotion
            }
        };
    }

    private static void ApplyAudioMap(
        Dictionary<string, object> map,
        ref AtlasBoardAudioSettingsValues audio,
        ref bool muted)
    {
        audio.Master =
            GetFloat(map, "master", audio.Master);

        audio.MainMusic =
            GetFloat(map, "mainMusic", audio.MainMusic);

        audio.Theme =
            GetFloat(map, "theme", audio.Theme);

        audio.Dice =
            GetFloat(map, "dice", audio.Dice);

        audio.Effects =
            GetFloat(map, "effects", audio.Effects);

        muted =
            GetBool(map, "muted", muted);
    }

    private static void ApplyGraphicsMap(
        Dictionary<string, object> map,
        ref AtlasBoardGraphicsSettingsValues graphics)
    {
        graphics.ResolutionWidth =
            GetInt(
                map,
                "resolutionWidth",
                graphics.ResolutionWidth);

        graphics.ResolutionHeight =
            GetInt(
                map,
                "resolutionHeight",
                graphics.ResolutionHeight);

        graphics.RefreshRate =
            GetInt(
                map,
                "refreshRate",
                graphics.RefreshRate);

        graphics.DisplayMode =
            GetInt(
                map,
                "displayMode",
                graphics.DisplayMode);

        graphics.QualityPreset =
            GetInt(
                map,
                "qualityPreset",
                graphics.QualityPreset);

        graphics.VSync =
            GetBool(
                map,
                "vsync",
                graphics.VSync);

        graphics.FpsLimit =
            GetInt(
                map,
                "fpsLimit",
                graphics.FpsLimit);

        graphics.ShadowQuality =
            GetInt(
                map,
                "shadowQuality",
                graphics.ShadowQuality);

        graphics.AntiAliasing =
            GetInt(
                map,
                "antiAliasing",
                graphics.AntiAliasing);

        graphics.ShowFps =
            GetBool(
                map,
                "showFps",
                graphics.ShowFps);
    }

    private static void ApplyGlobalGameplayMap(
        Dictionary<string, object> map,
        ref AtlasBoardGameplaySettingsValues gameplay)
    {
        gameplay.BotTurnSpeed =
            GetInt(
                map,
                "botTurnSpeed",
                gameplay.BotTurnSpeed);

        gameplay.UiHints =
            GetBool(
                map,
                "uiHints",
                gameplay.UiHints);

        gameplay.GameplayConfirmations =
            GetBool(
                map,
                "gameplayConfirmations",
                gameplay.GameplayConfirmations);
    }

    private static void ApplyPlatformGameplayMap(
        Dictionary<string, object> map,
        ref AtlasBoardGameplaySettingsValues gameplay)
    {
        gameplay.CameraSensitivity =
            GetInt(
                map,
                "cameraSensitivity",
                gameplay.CameraSensitivity);

        gameplay.CameraZoomSensitivity =
            GetInt(
                map,
                "cameraZoomSensitivity",
                gameplay.CameraZoomSensitivity);

        gameplay.CameraPanSensitivity =
            GetInt(
                map,
                "cameraPanSensitivity",
                gameplay.CameraPanSensitivity);

        gameplay.ReduceCameraMotion =
            GetBool(
                map,
                "reduceCameraMotion",
                gameplay.ReduceCameraMotion);
    }

    private static bool HasAudioSection(
        Dictionary<string, object> map)
    {
        return map != null &&
               map.Count > 0 &&
               (map.ContainsKey("master") ||
                map.ContainsKey("mainMusic") ||
                map.ContainsKey("effects"));
    }

    private static bool TryGetNestedMap(
        Dictionary<string, object> parent,
        string key,
        out Dictionary<string, object> map)
    {
        map = null;

        if (parent == null ||
            !parent.TryGetValue(
                key,
                out object value) ||
            value == null)
        {
            return false;
        }

        if (value is Dictionary<string, object>
            dictionary)
        {
            map = dictionary;
            return true;
        }

        if (value is IDictionary<string, object>
            interfaceMap)
        {
            map =
                new Dictionary<string, object>(
                    interfaceMap);

            return true;
        }

        return false;
    }

    private static Dictionary<string, object> CloneMap(
        Dictionary<string, object> source)
    {
        return source == null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(source);
    }

    private static int GetInt(
        Dictionary<string, object> map,
        string key,
        int fallback)
    {
        if (map == null ||
            !map.TryGetValue(key, out object value) ||
            value == null)
        {
            return fallback;
        }

        try
        {
            return Convert.ToInt32(value);
        }
        catch
        {
            return fallback;
        }
    }

    private static float GetFloat(
        Dictionary<string, object> map,
        string key,
        float fallback)
    {
        if (map == null ||
            !map.TryGetValue(key, out object value) ||
            value == null)
        {
            return fallback;
        }

        try
        {
            return Convert.ToSingle(value);
        }
        catch
        {
            return fallback;
        }
    }

    private static bool GetBool(
        Dictionary<string, object> map,
        string key,
        bool fallback)
    {
        if (map == null ||
            !map.TryGetValue(key, out object value) ||
            value == null)
        {
            return fallback;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        try
        {
            return Convert.ToInt32(value) != 0;
        }
        catch
        {
            if (bool.TryParse(
                    value.ToString(),
                    out bool parsed))
            {
                return parsed;
            }

            return fallback;
        }
    }

    private void ApplyPersistedLocalSettingsToRuntime(
        AtlasBoardUserSettingsValues user,
        AtlasBoardAudioSettingsValues audio)
    {
        AtlasBoardSettingsV2Controller controller =
            AtlasBoardSettingsV2Controller.Instance;

        if (controller != null)
        {
            controller
                .ReloadAndApplyPersistedSettingsFromExternalSync();

            return;
        }

        AtlasBoardUserSettingsRuntime.SetCurrent(
            user);

        AtlasBoardLocalizationManager.Instance
            ?.SetLanguage(
                user.LanguageCode);

        AtlasBoardAudioManager.Instance
            ?.ApplySettings(
                audio);

        AudioListener.volume =
            user.AudioMuted ? 0f : 1f;
    }

    private void ResetSignedInState()
    {
        if (string.IsNullOrWhiteSpace(
                syncedAccountId) &&
            cachedCloudPreferences == null &&
            !uploadPending)
        {
            return;
        }

        syncedAccountId = string.Empty;
        cachedCloudPreferences = null;
        uploadPending = false;
        nextSyncRetryTime = 0f;
    }

    private static string ResolvePlatformKey()
    {
        switch (Application.platform)
        {
            case RuntimePlatform.WindowsEditor:
            case RuntimePlatform.WindowsPlayer:
                return "windows";

            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.OSXPlayer:
                return "macos";

            case RuntimePlatform.LinuxEditor:
            case RuntimePlatform.LinuxPlayer:
                return "linux";

            case RuntimePlatform.Android:
                return "android";

            case RuntimePlatform.IPhonePlayer:
                return "ios";

            default:
                return Application.platform
                    .ToString()
                    .ToLowerInvariant();
        }
    }
}
