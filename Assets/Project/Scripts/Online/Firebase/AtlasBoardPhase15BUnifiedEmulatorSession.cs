#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

[DefaultExecutionOrder(15050)]
[DisallowMultipleComponent]
public sealed class AtlasBoardPhase15BUnifiedEmulatorSession :
    MonoBehaviour
{
    private const int AuthEmulatorPort = 9099;
    private const int FirestoreEmulatorPort = 8080;
    private const string DevelopmentNameSwitch =
        "-atlasDevName";

    private static AtlasBoardPhase15BUnifiedEmulatorSession
        instance;

    private AtlasBoardLobbyRuntimeBridge lobbyBridge;
    private AtlasBoardAccountService accountService;
    private Task initializationTask;

    public bool IsReady { get; private set; }
    public bool IsRunning { get; private set; }
    public string LastError { get; private set; } = string.Empty;
    public string BoundAccountId { get; private set; } = string.Empty;
    public string BoundDisplayName { get; private set; } = string.Empty;
    public string FirestoreEndpoint { get; private set; } = string.Empty;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        AtlasBoardPhase15BUnifiedEmulatorSession existing =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardPhase15BUnifiedEmulatorSession>();

        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject root =
            new GameObject(
                "AtlasBoard_Phase15B_UnifiedEmulatorSession");

        DontDestroyOnLoad(root);

        instance =
            root.AddComponent<
                AtlasBoardPhase15BUnifiedEmulatorSession>();
    }

    private void Awake()
    {
        if (instance != null &&
            instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private async void Start()
    {
        await EnsureReadyAsync();
    }

    public Task EnsureReadyAsync()
    {
        if (initializationTask == null ||
            initializationTask.IsFaulted ||
            initializationTask.IsCanceled)
        {
            initializationTask = InitializeAsync();
        }

        return initializationTask;
    }

    public async Task RetryAsync()
    {
        IsReady = false;
        LastError = string.Empty;
        initializationTask = InitializeAsync();
        await initializationTask;
    }

    private async Task InitializeAsync()
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;

        try
        {
            ResolveReferences();

            if (lobbyBridge == null ||
                accountService == null)
            {
                LastError =
                    "LobbyRuntimeBridge or AccountService is missing.";

                Debug.LogWarning(
                    "Phase 15B unified emulator session is waiting for " +
                    "AtlasBoardLobbyRuntimeBridge + AtlasBoardAccountService.",
                    this);

                return;
            }

            if (!lobbyBridge.UsingLocalEmulators)
            {
                // Release/production identity remains untouched.
                LastError = string.Empty;
                return;
            }

            await accountService.EnsureInitializedAsync();

            string host =
                lobbyBridge.EmulatorHostForOnlineSubsystems;

            if (string.IsNullOrWhiteSpace(host))
            {
                host = "127.0.0.1";
            }

            FirebaseAuth auth =
                FirebaseAuth.DefaultInstance;

            FirebaseFirestore firestore =
                FirebaseFirestore.DefaultInstance;

            if (auth == null ||
                firestore == null)
            {
                LastError =
                    "Firebase Auth/Firestore SDK instances are unavailable.";
                return;
            }

            // Official Firebase Unity emulator APIs. The account SDK now talks
            // to the same local services as AtlasBoard's manual lobby bridge.
            auth.UseEmulator(
                host,
                AuthEmulatorPort);

            firestore.Settings.Host =
                $"{host}:{FirestoreEmulatorPort}";

            firestore.Settings.SslEnabled =
                false;

            FirestoreEndpoint =
                firestore.Settings.Host;

            // Never carry a persisted production Auth SDK user into a local
            // emulator session.
            if (auth.CurrentUser != null)
            {
                auth.SignOut();
            }

            bool lobbyIdentityReady =
                await lobbyBridge.EnsureOnlineIdentityAsync();

            if (!lobbyIdentityReady ||
                string.IsNullOrWhiteSpace(
                    lobbyBridge.CurrentAccountId))
            {
                LastError =
                    "Lobby emulator identity could not be established.";
                return;
            }

            string displayName =
                ResolveDevelopmentDisplayName(
                    lobbyBridge);

            string identityKey =
                BuildDeterministicDevelopmentIdentityKey(
                    displayName);

            string email =
                $"atlasboard.dev.{identityKey}@example.com";

            string password =
                $"AtlasBoardDev!{identityKey}";

            AuthResult authResult =
                await auth.SignInWithEmailAndPasswordAsync(
                    email,
                    password);

            FirebaseUser user =
                authResult != null
                    ? authResult.User
                    : null;

            if (user == null ||
                string.IsNullOrWhiteSpace(
                    user.UserId))
            {
                LastError =
                    "FirebaseAuth SDK emulator sign-in returned no user.";
                return;
            }

            if (!string.Equals(
                    user.UserId,
                    lobbyBridge.CurrentAccountId,
                    StringComparison.Ordinal))
            {
                LastError =
                    "Auth SDK and Lobby bridge resolved different emulator UIDs. " +
                    $"SDK={user.UserId}, Lobby={lobbyBridge.CurrentAccountId}.";

                auth.SignOut();
                return;
            }

            await EnsurePreferencesDocumentAsync(
                firestore,
                user.UserId);

            AtlasAccountSnapshot snapshot =
                await accountService.LoadCurrentAccountAsync();

            if (snapshot == null ||
                string.IsNullOrWhiteSpace(
                    snapshot.DisplayName))
            {
                LastError =
                    "Unified emulator Auth succeeded, but canonical account " +
                    "snapshot/profile could not be loaded.";
                return;
            }

            BoundAccountId = user.UserId;
            BoundDisplayName =
                snapshot.DisplayName.Trim();

            AtlasBoardCanonicalProfileHeader profileHeader =
                UnityEngine.Object.FindAnyObjectByType<
                    AtlasBoardCanonicalProfileHeader>();

            profileHeader?.RefreshNow();

            IsReady = true;
            LastError = string.Empty;

            Debug.Log(
                "Phase 15B unified emulator Auth session READY. " +
                $"UID={BoundAccountId}, " +
                $"DisplayName={BoundDisplayName}, " +
                $"Auth={host}:{AuthEmulatorPort}, " +
                $"Firestore={FirestoreEndpoint}. " +
                "Lobby/manual HTTP identity and FirebaseAuth SDK identity " +
                "now share the same emulator account.",
                this);
        }
        catch (Exception exception)
        {
            IsReady = false;
            LastError =
                exception != null
                    ? exception.ToString()
                    : "Unknown Phase 15B emulator session error.";

            Debug.LogWarning(
                "Phase 15B unified emulator Auth session failed. " +
                LastError,
                this);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void ResolveReferences()
    {
        if (lobbyBridge == null)
        {
            lobbyBridge =
                UnityEngine.Object.FindAnyObjectByType<
                    AtlasBoardLobbyRuntimeBridge>();
        }

        if (accountService == null)
        {
            accountService =
                AtlasBoardAccountService.Instance;

            if (accountService == null)
            {
                accountService =
                    UnityEngine.Object.FindAnyObjectByType<
                        AtlasBoardAccountService>();
            }
        }
    }

    private static async Task EnsurePreferencesDocumentAsync(
        FirebaseFirestore firestore,
        string uid)
    {
        DocumentReference reference =
            firestore.Collection("preferences")
                .Document(uid);

        DocumentSnapshot snapshot =
            await reference.GetSnapshotAsync();

        if (snapshot.Exists)
        {
            return;
        }

        string language =
            AtlasBoardLocalizationManager.Instance != null
                ? AtlasBoardLocalizationManager.Instance
                    .CurrentLanguageCode
                : "en";

        if (string.IsNullOrWhiteSpace(language) ||
            !AtlasBoardAccountConstants
                .SupportedLanguageCodes
                .Contains(language))
        {
            language = "en";
        }

        Dictionary<string, object> data =
            new Dictionary<string, object>
            {
                { "language", language.ToLowerInvariant() },
                { "audio", new Dictionary<string, object>() },
                { "graphics", new Dictionary<string, object>() },
                { "controls", new Dictionary<string, object>() },
                { "gameplay", new Dictionary<string, object>() },
                {
                    "crossplayPreference",
                    AtlasBoardAccountConstants
                        .DefaultCrossplayPreference
                },
                { "updatedAt", FieldValue.ServerTimestamp }
            };

        await reference.SetAsync(data);

        Debug.Log(
            "Phase 15B created missing emulator cloud preferences " +
            "through the authenticated FirebaseAuth SDK session.");
    }

    private static string ResolveDevelopmentDisplayName(
        AtlasBoardLobbyRuntimeBridge bridge)
    {
        string commandLine =
            ReadCommandLineValue(
                DevelopmentNameSwitch);

        if (!string.IsNullOrWhiteSpace(
                commandLine))
        {
            return commandLine.Trim();
        }

        if (bridge != null)
        {
            System.Reflection.FieldInfo field =
                typeof(AtlasBoardLobbyRuntimeBridge)
                    .GetField(
                        "developmentDisplayName",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);

            string configured =
                field != null
                    ? field.GetValue(bridge) as string
                    : string.Empty;

            if (!string.IsNullOrWhiteSpace(
                    configured))
            {
                return configured.Trim();
            }
        }

        return Application.isEditor
            ? "Unity Editor Player"
            : "Unity Development Player";
    }

    private static string BuildDeterministicDevelopmentIdentityKey(
        string displayName)
    {
        string normalized =
            string.IsNullOrWhiteSpace(displayName)
                ? "atlasboard-development-player"
                : displayName.Trim().ToLowerInvariant();

        using SHA256 sha256 =
            SHA256.Create();

        byte[] bytes =
            sha256.ComputeHash(
                Encoding.UTF8.GetBytes(
                    "atlasboard-local-emulator|" +
                    normalized));

        StringBuilder builder =
            new StringBuilder(24);

        for (int index = 0;
             index < 12 &&
             index < bytes.Length;
             index++)
        {
            builder.Append(
                bytes[index].ToString("x2"));
        }

        return builder.ToString();
    }

    private static string ReadCommandLineValue(
        string switchName)
    {
        string[] args =
            Environment.GetCommandLineArgs();

        string prefix =
            switchName + "=";

        for (int index = 0;
             index < args.Length;
             index++)
        {
            string argument =
                args[index] ?? string.Empty;

            if (argument.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return argument.Substring(
                    prefix.Length);
            }

            if (string.Equals(
                    argument,
                    switchName,
                    StringComparison.OrdinalIgnoreCase) &&
                index + 1 < args.Length)
            {
                return args[index + 1] ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
#endif
