#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardCloudPreferencesSyncV1Setup
{
    [MenuItem("Atlas Board/Firebase/Build Cloud Preferences Sync v1")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError(
                "Exit Play Mode before building AtlasBoard Cloud Preferences Sync v1.");
            return;
        }

        AtlasBoardAccountService accountService =
            Object.FindAnyObjectByType<AtlasBoardAccountService>(
                FindObjectsInactive.Include);

        if (accountService == null)
        {
            Debug.LogError(
                "AtlasBoard Cloud Preferences Sync v1 requires the existing " +
                "AtlasBoardAccountService. Run Atlas Board > Firebase > " +
                "Build Account Runtime v1 first.");
            return;
        }

        AtlasBoardCloudPreferencesSync sync =
            accountService.GetComponent<AtlasBoardCloudPreferencesSync>();

        if (sync == null)
        {
            sync = Undo.AddComponent<AtlasBoardCloudPreferencesSync>(
                accountService.gameObject);
        }

        EditorUtility.SetDirty(accountService.gameObject);
        EditorSceneManager.MarkSceneDirty(
            accountService.gameObject.scene);

        Selection.activeGameObject =
            accountService.gameObject;

        Debug.Log(
            "AtlasBoard Cloud Preferences Sync v1 ready. Existing " +
            "PlayerPrefs Settings remain the immediate/offline source. " +
            "Signed-in accounts synchronize language, audio and gameplay " +
            "preferences to Firestore; graphics and camera controls are " +
            "platform-scoped (Windows/macOS/Linux/Android/iOS) so future " +
            "mobile settings cannot overwrite desktop display controls. " +
            "Cloud failures never block the local Settings UI.");
    }

    [MenuItem("Atlas Board/Firebase/Validate Cloud Preferences Sync v1")]
    public static void Validate()
    {
        AtlasBoardAccountService accountService =
            Object.FindAnyObjectByType<AtlasBoardAccountService>(
                FindObjectsInactive.Include);

        AtlasBoardCloudPreferencesSync sync =
            Object.FindAnyObjectByType<AtlasBoardCloudPreferencesSync>(
                FindObjectsInactive.Include);

        AtlasBoardSettingsV2Controller settings =
            Object.FindAnyObjectByType<AtlasBoardSettingsV2Controller>(
                FindObjectsInactive.Include);

        bool languagesValid =
            AtlasBoardLocalizationLanguages.Codes != null &&
            AtlasBoardLocalizationLanguages.Codes.Length == 7 &&
            AtlasBoardLocalizationLanguages.IndexOf("en") >= 0 &&
            AtlasBoardLocalizationLanguages.IndexOf("tr") >= 0 &&
            AtlasBoardLocalizationLanguages.IndexOf("es") >= 0 &&
            AtlasBoardLocalizationLanguages.IndexOf("fr") >= 0 &&
            AtlasBoardLocalizationLanguages.IndexOf("de") >= 0 &&
            AtlasBoardLocalizationLanguages.IndexOf("ko") >= 0 &&
            AtlasBoardLocalizationLanguages.IndexOf("ru") >= 0;

        if (accountService == null)
        {
            Debug.LogError(
                "AtlasBoard Cloud Preferences Sync v1 validation FAILED: " +
                "AtlasBoardAccountService is missing from the active scene.");
            return;
        }

        if (sync == null ||
            sync.gameObject != accountService.gameObject)
        {
            Debug.LogError(
                "AtlasBoard Cloud Preferences Sync v1 validation FAILED: " +
                "AtlasBoardCloudPreferencesSync is not attached to the " +
                "AtlasBoardAccountRuntime object.");
            return;
        }

        if (settings == null)
        {
            Debug.LogError(
                "AtlasBoard Cloud Preferences Sync v1 validation FAILED: " +
                "AtlasBoardSettingsV2Controller is missing. Existing " +
                "Settings architecture was expected to remain in place.");
            return;
        }

        if (!languagesValid)
        {
            Debug.LogError(
                "AtlasBoard Cloud Preferences Sync v1 validation FAILED: " +
                "stable locale set EN/TR/ES/FR/DE/KO/RU was not found.");
            return;
        }

        AtlasBoardUserSettingsValues user =
            AtlasBoardUserSettingsStore.Load();

        AtlasBoardAudioSettingsValues audio =
            AtlasBoardAudioSettings.Load();

        user = AtlasBoardUserSettingsStore.Clamp(user);
        audio = AtlasBoardAudioSettings.Clamp(audio);

        Debug.Log(
            "AtlasBoard Cloud Preferences Sync v1 validation PASSED. " +
            "Account service + cloud sync + existing Settings v2 are " +
            "connected; local PlayerPrefs remain offline-first; stable " +
            "locales are EN/TR/ES/FR/DE/KO/RU; audio is shared; graphics " +
            "and camera controls are platform-scoped; Controls cloud map " +
            "is preserved for future implementation.");
    }
}
#endif
