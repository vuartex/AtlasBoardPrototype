using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;

public sealed class AtlasBoardBackendHealthCheckTest : MonoBehaviour
{
    private const string EmulatorAppName =
        "AtlasBoardBackendEmulatorTest";

    private const string ExpectedProjectId =
        "atlasboard-usa";

    private const string Region =
        "europe-west1";

    private const string FunctionName =
        "economyHealthCheck";

    private const string EmulatorHost =
        "127.0.0.1";

    private const int AuthEmulatorPort = 9099;
    private const int FunctionsEmulatorPort = 5001;

    private bool testRunning;

    public bool TestRunning =>
        testRunning;

    [ContextMenu("Run Backend Health Check v1")]
    public void RunFromContextMenu()
    {
        RunFromEditorMenu();
    }

    public async void RunFromEditorMenu()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError(
                "AtlasBoard Backend Health Check v1 must be run " +
                "while the Unity Editor is in Play Mode.",
                this);

            return;
        }

        if (testRunning)
        {
            Debug.LogWarning(
                "AtlasBoard Backend Health Check v1 is already running.",
                this);

            return;
        }

        testRunning = true;

        try
        {
            await RunHealthCheckAsync();
        }
        finally
        {
            testRunning = false;
        }
    }

    private async Task RunHealthCheckAsync()
    {
        Debug.Log(
            "[AtlasBoard Backend Test] Starting isolated local " +
            "Auth + direct callable HTTP emulator test.",
            this);

        DependencyStatus dependencyStatus =
            await FirebaseApp.CheckAndFixDependenciesAsync();

        if (dependencyStatus != DependencyStatus.Available)
        {
            Debug.LogError(
                "[AtlasBoard Backend Test] FAILED. Firebase " +
                $"dependencies unavailable: {dependencyStatus}",
                this);

            return;
        }

        FirebaseApp defaultApp =
            FirebaseApp.DefaultInstance;

        if (defaultApp == null ||
            defaultApp.Options == null ||
            defaultApp.Options.ProjectId != ExpectedProjectId)
        {
            Debug.LogError(
                "[AtlasBoard Backend Test] FAILED. Expected the " +
                "default Firebase project to be atlasboard-usa.",
                this);

            return;
        }

        FirebaseApp emulatorApp = null;
        FirebaseAuth emulatorAuth = null;
        FirebaseUser temporaryUser = null;

        try
        {
            FirebaseApp existingApp =
                FirebaseApp.GetInstance(
                    EmulatorAppName);

            if (existingApp != null)
            {
                existingApp.Dispose();
            }

            AppOptions options =
                CopyOptions(
                    defaultApp.Options);

            emulatorApp =
                FirebaseApp.Create(
                    options,
                    EmulatorAppName);

            emulatorAuth =
                FirebaseAuth.GetAuth(
                    emulatorApp);

            emulatorAuth.UseEmulator(
                EmulatorHost,
                AuthEmulatorPort);

            string uniquePart =
                DateTime.UtcNow
                    .ToString(
                        "yyyyMMddHHmmssfff");

            string testEmail =
                $"atlasboard.backend.{uniquePart}@example.com";

            string testPassword =
                $"AtlasBoardBackend!{uniquePart}";

            Debug.Log(
                "[AtlasBoard Backend Test] Creating a temporary " +
                "LOCAL Auth Emulator account.",
                this);

            AuthResult authResult =
                await emulatorAuth
                    .CreateUserWithEmailAndPasswordAsync(
                        testEmail,
                        testPassword);

            temporaryUser =
                authResult.User;

            if (temporaryUser == null ||
                string.IsNullOrWhiteSpace(
                    temporaryUser.UserId))
            {
                throw new InvalidOperationException(
                    "Auth Emulator did not return a valid user.");
            }

            string idToken =
                await temporaryUser.TokenAsync(
                    false);

            if (string.IsNullOrWhiteSpace(
                    idToken))
            {
                throw new InvalidOperationException(
                    "Auth Emulator did not return an ID token.");
            }

            string callableUrl =
                $"http://{EmulatorHost}:{FunctionsEmulatorPort}/" +
                $"{ExpectedProjectId}/{Region}/{FunctionName}";

            string requestJson =
                "{\"data\":{" +
                "\"probe\":\"unity-editor\"," +
                "\"clientProtocolVersion\":1" +
                "}}";

            Debug.Log(
                "[AtlasBoard Backend Test] Calling " +
                $"{FunctionName} through the exact local callable URL: " +
                callableUrl,
                this);

            using UnityWebRequest request =
                new UnityWebRequest(
                    callableUrl,
                    UnityWebRequest.kHttpVerbPOST);

            byte[] body =
                Encoding.UTF8.GetBytes(
                    requestJson);

            request.uploadHandler =
                new UploadHandlerRaw(
                    body);

            request.downloadHandler =
                new DownloadHandlerBuffer();

            request.SetRequestHeader(
                "Content-Type",
                "application/json");

            request.SetRequestHeader(
                "Authorization",
                $"Bearer {idToken}");

            request.timeout = 20;

            await SendRequestAsync(
                request);

            string responseText =
                request.downloadHandler != null
                    ? request.downloadHandler.text
                    : string.Empty;

            if (request.result !=
                UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    "Callable HTTP request failed. " +
                    $"HTTP={request.responseCode}, " +
                    $"UnityResult={request.result}, " +
                    $"Error={request.error}, " +
                    $"Body={responseText}");
            }

            CallableEnvelope response =
                JsonUtility.FromJson<CallableEnvelope>(
                    responseText);

            if (response == null ||
                response.result == null)
            {
                throw new InvalidOperationException(
                    "Callable response did not contain a valid result. " +
                    $"Body={responseText}");
            }

            EconomyHealthResult result =
                response.result;

            bool passed =
                result.ok &&
                result.authenticated &&
                result.accountId == temporaryUser.UserId &&
                result.projectId == ExpectedProjectId &&
                result.region == Region &&
                result.mode == "emulator" &&
                result.backendSchemaVersion == 1 &&
                result.protocolVersion == 1;

            if (!passed)
            {
                Debug.LogError(
                    "[AtlasBoard Backend Test] FAILED response " +
                    $"validation. ok={result.ok}, " +
                    $"auth={result.authenticated}, " +
                    $"project={result.projectId}, " +
                    $"region={result.region}, " +
                    $"mode={result.mode}, " +
                    $"schema={result.backendSchemaVersion}, " +
                    $"protocol={result.protocolVersion}, " +
                    $"body={responseText}.",
                    this);

                return;
            }

            Debug.Log(
                "AtlasBoard Secure Backend Health Check v1 LOCAL E2E PASSED. " +
                "Unity created an Auth Emulator identity, obtained its ID " +
                "token, reached the exact europe-west1 callable Functions " +
                "emulator URL, the backend recognized the same account ID, " +
                "and atlasboard-usa/schema v1/protocol v1 were verified. " +
                "No Firestore, wallet, inventory or commerce data was written.",
                this);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[AtlasBoard Backend Test] FAILED.\n" +
                exception,
                this);
        }
        finally
        {
            if (emulatorAuth != null)
            {
                emulatorAuth.SignOut();
            }

            // The Auth Emulator is disposable local state and is cleared when
            // the emulator restarts. We intentionally do not call
            // FirebaseUser.DeleteAsync here because the desktop beta Auth SDK
            // produced a secondary cleanup error in v1.0.0 after a transport
            // timeout. Keeping the temporary local user cannot affect
            // production Firebase data.

            if (emulatorApp != null)
            {
                emulatorApp.Dispose();
            }
        }
    }

    private static async Task SendRequestAsync(
        UnityWebRequest request)
    {
        UnityWebRequestAsyncOperation operation =
            request.SendWebRequest();

        TaskCompletionSource<bool> completion =
            new TaskCompletionSource<bool>();

        operation.completed +=
            _ => completion.TrySetResult(true);

        await completion.Task;
    }

    private static AppOptions CopyOptions(
        AppOptions source)
    {
        return new AppOptions
        {
            ApiKey = source.ApiKey,
            AppId = source.AppId,
            MessageSenderId = source.MessageSenderId,
            ProjectId = source.ProjectId,
            StorageBucket = source.StorageBucket
        };
    }

    [Serializable]
    private sealed class CallableEnvelope
    {
        public EconomyHealthResult result;
    }

    [Serializable]
    private sealed class EconomyHealthResult
    {
        public bool ok;
        public bool authenticated;
        public string accountId;
        public string projectId;
        public string region;
        public int backendSchemaVersion;
        public int protocolVersion;
        public string service;
        public string mode;
        public string serverTimeUtc;
    }
}
