#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardBackendHealthCheckV1Setup
{
    private const string ObjectName =
        "AtlasBoardBackendHealthCheckTest";

    [MenuItem(
        "Atlas Board/Firebase/Build Backend Health Check v1")]
    public static void Build()
    {
        AtlasBoardBackendHealthCheckTest tester =
            Object.FindAnyObjectByType<
                AtlasBoardBackendHealthCheckTest>();

        if (tester == null)
        {
            GameObject root =
                GameObject.Find(
                    ObjectName);

            if (root == null)
            {
                root =
                    new GameObject(
                        ObjectName);

                Undo.RegisterCreatedObjectUndo(
                    root,
                    "Create AtlasBoard Backend Health Check");
            }

            tester =
                Undo.AddComponent<
                    AtlasBoardBackendHealthCheckTest>(
                        root);
        }

        EditorUtility.SetDirty(
            tester);

        EditorSceneManager.MarkSceneDirty(
            tester.gameObject.scene);

        Debug.Log(
            "AtlasBoard Backend Health Check v1 ready. The test " +
            "uses an isolated named FirebaseApp connected only " +
            "to local Auth (127.0.0.1:9099) and Functions " +
            "(127.0.0.1:5001) emulators. Production Auth, " +
            "Firestore, wallet, inventory and commerce data are " +
            "not modified.");
    }

    [MenuItem(
        "Atlas Board/Firebase/Validate Backend Health Check v1")]
    public static void Validate()
    {
        AtlasBoardBackendHealthCheckTest tester =
            Object.FindAnyObjectByType<
                AtlasBoardBackendHealthCheckTest>();

        if (tester == null)
        {
            Debug.LogError(
                "AtlasBoard Backend Health Check v1 validation " +
                "FAILED: tester component is not present in the " +
                "active scene.");

            return;
        }

        Debug.Log(
            "AtlasBoard Backend Health Check v1 validation PASSED. " +
            "Local emulator tester is installed; expected project " +
            "is atlasboard-usa, function region is europe-west1, " +
            "Auth emulator port is 9099 and Functions emulator " +
            "port is 5001.");
    }

    [MenuItem(
        "Atlas Board/Firebase/Run Backend Health Check v1")]
    public static void Run()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError(
                "Enter Play Mode before running AtlasBoard " +
                "Backend Health Check v1.");

            return;
        }

        AtlasBoardBackendHealthCheckTest tester =
            Object.FindAnyObjectByType<
                AtlasBoardBackendHealthCheckTest>();

        if (tester == null)
        {
            Debug.LogError(
                "Backend Health Check tester is missing. Run " +
                "Atlas Board > Firebase > Build Backend Health " +
                "Check v1 first.");

            return;
        }

        tester.RunFromEditorMenu();
    }
}
#endif
