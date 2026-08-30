using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

public sealed class AtlasBoardFirebaseConnectionTest : MonoBehaviour
{
    [SerializeField]
    private bool runOnStart = true;

    private async void Start()
    {
        if (runOnStart)
        {
            await RunConnectionTest();
        }
    }

    [ContextMenu("Run Firebase Connection Test")]
    public void RunFromContextMenu()
    {
        _ = RunConnectionTest();
    }

    private async Task RunConnectionTest()
    {
        Debug.Log(
            "[AtlasBoard Firebase] Starting connection test.",
            this);

        DependencyStatus dependencyStatus =
            await FirebaseApp.CheckAndFixDependenciesAsync();

        if (dependencyStatus != DependencyStatus.Available)
        {
            Debug.LogError(
                "[AtlasBoard Firebase] FAILED. " +
                $"Firebase dependencies are not available. " +
                $"Status: {dependencyStatus}",
                this);

            return;
        }

        FirebaseApp app =
            FirebaseApp.DefaultInstance;

        FirebaseAuth auth =
            FirebaseAuth.DefaultInstance;

        FirebaseFirestore firestore =
            FirebaseFirestore.DefaultInstance;

        bool appReady =
            app != null;

        bool authReady =
            auth != null;

        bool firestoreReady =
            firestore != null;

        string projectId =
            app != null &&
            app.Options != null
                ? app.Options.ProjectId
                : string.Empty;

        Debug.Log(
            "[AtlasBoard Firebase] " +
            $"Firebase App initialized: " +
            $"{(appReady ? "YES" : "NO")}",
            this);

        Debug.Log(
            "[AtlasBoard Firebase] " +
            $"Auth instance available: " +
            $"{(authReady ? "YES" : "NO")}",
            this);

        Debug.Log(
            "[AtlasBoard Firebase] " +
            $"Firestore instance available: " +
            $"{(firestoreReady ? "YES" : "NO")}",
            this);

        Debug.Log(
            "[AtlasBoard Firebase] " +
            $"Project ID: {projectId}",
            this);

        bool correctProject =
            projectId == "atlasboard-usa";

        if (appReady &&
            authReady &&
            firestoreReady &&
            correctProject)
        {
            Debug.Log(
                "AtlasBoard Firebase connection test PASSED. " +
                "Firebase App, Authentication and Firestore " +
                "initialized successfully. " +
                "Project ID = atlasboard-usa.",
                this);

            return;
        }

        Debug.LogError(
            "AtlasBoard Firebase connection test FAILED. " +
            "Expected App/Auth/Firestore to initialize and " +
            "Project ID to equal atlasboard-usa.",
            this);
    }
}