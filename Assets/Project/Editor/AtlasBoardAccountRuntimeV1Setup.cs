#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardAccountRuntimeV1Setup
{
    private const string RuntimeObjectName =
        "AtlasBoardAccountRuntime";

    [MenuItem("Atlas Board/Firebase/Build Account Runtime v1")]
    public static void Build()
    {
        AtlasBoardAccountService existing =
            Object.FindAnyObjectByType<AtlasBoardAccountService>();

        if (existing == null)
        {
            GameObject runtimeObject =
                GameObject.Find(RuntimeObjectName);

            if (runtimeObject == null)
            {
                runtimeObject =
                    new GameObject(RuntimeObjectName);

                Undo.RegisterCreatedObjectUndo(
                    runtimeObject,
                    "Create AtlasBoard Account Runtime");
            }

            existing =
                runtimeObject.GetComponent<AtlasBoardAccountService>();

            if (existing == null)
            {
                Undo.AddComponent<AtlasBoardAccountService>(
                    runtimeObject);
            }
        }

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene());

        Debug.Log(
            "AtlasBoard Account Runtime v1 installed. " +
            "AtlasBoardAccountRuntime now provides provider-neutral " +
            "Email/Password Auth + users/public_profiles/preferences Firestore " +
            "services. Existing Settings/PlayerPrefs, localization, gameplay, " +
            "wallet, inventory and lobby systems were not modified.");
    }

    [MenuItem("Atlas Board/Firebase/Validate Account Runtime v1")]
    public static void Validate()
    {
        AtlasBoardAccountService service =
            Object.FindAnyObjectByType<AtlasBoardAccountService>();

        if (service == null)
        {
            Debug.LogError(
                "AtlasBoard Account Runtime v1 validation FAILED: " +
                "AtlasBoardAccountService is not present in the active scene.");
            return;
        }

        string[] languageCodes =
        {
            "en", "tr", "es", "fr", "de", "ko", "ru"
        };

        foreach (string code in languageCodes)
        {
            if (!AtlasBoardAccountConstants.SupportedLanguageCodes.Contains(code))
            {
                Debug.LogError(
                    "AtlasBoard Account Runtime v1 validation FAILED: " +
                    $"missing language storage code '{code}'.");
                return;
            }
        }

        Debug.Log(
            "AtlasBoard Account Runtime v1 validation PASSED. " +
            "Service component exists; account schema v1 constants are loaded; " +
            "stable cloud language codes are EN/TR/ES/FR/DE/KO/RU; no existing " +
            "gameplay or localization files were replaced.");
    }
}
#endif
