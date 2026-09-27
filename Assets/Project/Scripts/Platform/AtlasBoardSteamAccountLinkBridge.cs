using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;

[DefaultExecutionOrder(12200)]
[DisallowMultipleComponent]
public sealed class AtlasBoardSteamAccountLinkBridge :
    MonoBehaviour
{
    public const string TicketIdentity =
        "atlasboard-account-link-v1";

    private const string ProjectId =
        "atlasboard-usa";

    private const string Region =
        "europe-west1";

    private const string EmulatorHost =
        "127.0.0.1";

    private const int FunctionsPort =
        5001;

    private const int AuthEmulatorPort =
        9099;

    private const float TicketTimeoutSeconds =
        12f;

    private static AtlasBoardSteamAccountLinkBridge
        instance;

    private AtlasBoardPlatformRuntime
        platformRuntime;

    private AtlasBoardLobbyRuntimeBridge
        lobbyBridge;

    private Callback<GetTicketForWebApiResponse_t>
        ticketCallback;

    private TaskCompletionSource<WebApiTicketResult>
        pendingTicketSource;

    private Task<AtlasBoardSteamReturningSignInResult>
        automaticReturningSignInTask;

    private HAuthTicket pendingTicket =
        HAuthTicket.Invalid;

    public static AtlasBoardSteamAccountLinkBridge Instance =>
        instance;

    public AtlasBoardSteamLinkStatus CurrentStatus
    {
        get;
        private set;
    } = AtlasBoardSteamLinkStatus.Unlinked();

    public AtlasBoardSteamReturningSignInResult
        LastReturningSignIn
    {
        get;
        private set;
    } =
        AtlasBoardSteamReturningSignInResult.Empty();

    public string LastErrorKey
    {
        get;
        private set;
    } = string.Empty;

    public string LastTechnicalMessage
    {
        get;
        private set;
    } = string.Empty;

    public bool UsingLocalEmulators
    {
        get
        {
            ResolveReferences();

            return lobbyBridge != null &&
                   lobbyBridge.UsingLocalEmulators;
        }
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntime()
    {
        if (instance != null)
        {
            return;
        }

        AtlasBoardSteamAccountLinkBridge existing =
            FindAnyObjectByType<
                AtlasBoardSteamAccountLinkBridge>(
                FindObjectsInactive.Include);

        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject root =
            new GameObject(
                "AtlasBoardSteamAccountLinkBridge");

        root.AddComponent<
            AtlasBoardSteamAccountLinkBridge>();
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
        DontDestroyOnLoad(gameObject);

        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        EnsureTicketCallback();
    }

    private void OnDestroy()
    {
        CancelPendingTicket();

        ticketCallback?.Dispose();
        ticketCallback = null;

        if (instance == this)
        {
            instance = null;
        }
    }

    public async Task<AtlasBoardSteamLinkStatus>
        RefreshStatusAsync()
    {
        if (!await EnsureAtlasIdentityAsync())
        {
            return FailedStatus();
        }

        StatusEnvelope envelope =
            await CallFunctionAsync<
                EmptyRequest,
                StatusEnvelope>(
                "platformSteamGetLinkStatus",
                new EmptyRequest());

        if (envelope?.result == null ||
            !envelope.result.ok)
        {
            return FailedStatus();
        }

        CurrentStatus =
            BuildStatus(
                envelope.result);

        return CurrentStatus;
    }

    public async Task<AtlasBoardSteamLinkStatus>
        DevLinkCurrentSteamAsync()
    {
        ResolveReferences();

        if (!UsingLocalEmulators)
        {
            SetError(
                "platform.error.emulator_only",
                "DEV Steam link proof requires local emulators.");

            return FailedStatus();
        }

        if (!IsSteamReady())
        {
            return FailedStatus();
        }

        LinkEnvelope envelope =
            await CallFunctionAsync<
                DevLinkRequest,
                LinkEnvelope>(
                "platformSteamDevLinkCurrentAccount",
                new DevLinkRequest
                {
                    steamId =
                        platformRuntime
                            .SteamId
                            .ToString(),
                    appId =
                        (int)platformRuntime
                            .SteamAppId
                });

        if (envelope?.result == null ||
            !envelope.result.ok)
        {
            return FailedStatus();
        }

        CurrentStatus =
            BuildStatus(
                envelope.result);

        return CurrentStatus;
    }

    public async Task<AtlasBoardSteamLinkStatus>
        VerifyAndLinkCurrentSteamAsync()
    {
        ResolveReferences();

        if (!IsSteamReady())
        {
            return FailedStatus();
        }

        if (!await EnsureAtlasIdentityAsync())
        {
            return FailedStatus();
        }

        WebApiTicketResult ticket =
            await RequestWebApiTicketAsync();

        if (!ticket.Success ||
            string.IsNullOrWhiteSpace(
                ticket.TicketHex))
        {
            SetError(
                "platform.error.steam_ticket_unavailable",
                ticket.TechnicalMessage);

            return FailedStatus();
        }

        try
        {
            LinkEnvelope envelope =
                await CallFunctionAsync<
                    VerifiedLinkRequest,
                    LinkEnvelope>(
                    "platformSteamLinkCurrentAccount",
                    new VerifiedLinkRequest
                    {
                        ticket =
                            ticket.TicketHex
                    });

            if (envelope?.result == null ||
                !envelope.result.ok)
            {
                return FailedStatus();
            }

            CurrentStatus =
                BuildStatus(
                    envelope.result);

            return CurrentStatus;
        }
        finally
        {
            CancelTicket(
                ticket.Handle);
        }
    }

    public async Task<AtlasBoardSteamReturningSignInResult>
        DevReturningSignInCurrentSteamAsync()
    {
        ResolveReferences();

        if (!UsingLocalEmulators)
        {
            return ReturningSignInFailure(
                "platform.error.emulator_only",
                "DEV returning Steam sign-in requires local emulators.");
        }

        if (!IsSteamReady())
        {
            return ReturningSignInFailure(
                LastErrorKey,
                LastTechnicalMessage);
        }

        ReturningEnvelope envelope =
            await CallUnauthenticatedFunctionAsync<
                DevReturningRequest,
                ReturningEnvelope>(
                "platformSteamDevReturningSignIn",
                new DevReturningRequest
                {
                    steamId =
                        platformRuntime
                            .SteamId
                            .ToString()
                });

        return await ApplyReturningEnvelopeAsync(
            envelope,
            true);
    }

    public async Task<AtlasBoardSteamReturningSignInResult>
        AutomaticReturningSignInCurrentSteamAsync()
    {
        if (automaticReturningSignInTask == null)
        {
            automaticReturningSignInTask =
                AutomaticReturningSignInCoreAsync();
        }

        return await automaticReturningSignInTask;
    }

    private async Task<AtlasBoardSteamReturningSignInResult>
        AutomaticReturningSignInCoreAsync()
    {
        ResolveReferences();

        AtlasBoardSteamReturningSignInResult result =
            UsingLocalEmulators
                ? await DevReturningSignInCurrentSteamAsync()
                : await ReturningSignInCurrentSteamAsync();

        if (!result.Success)
        {
            return result;
        }

        ResolveReferences();

        if (lobbyBridge != null)
        {
            bool rebound =
                await lobbyBridge
                    .RebindIdentityFromFirebaseAuthAsync();

            if (!rebound)
            {
                return ReturningSignInFailure(
                    "platform.error.identity_rebind_failed",
                    "Steam returning sign-in succeeded, but the lobby identity could not rebind to Firebase Auth.");
            }
        }

        return result;
    }

    public async Task<AtlasBoardSteamReturningSignInResult>
        ReturningSignInCurrentSteamAsync()
    {
        ResolveReferences();

        if (!IsSteamReady())
        {
            return ReturningSignInFailure(
                LastErrorKey,
                LastTechnicalMessage);
        }

        WebApiTicketResult ticket =
            await RequestWebApiTicketAsync();

        if (!ticket.Success ||
            string.IsNullOrWhiteSpace(
                ticket.TicketHex))
        {
            return ReturningSignInFailure(
                "platform.error.steam_ticket_unavailable",
                ticket.TechnicalMessage);
        }

        try
        {
            ReturningEnvelope envelope =
                await CallUnauthenticatedFunctionAsync<
                    VerifiedLinkRequest,
                    ReturningEnvelope>(
                    "platformSteamReturningSignIn",
                    new VerifiedLinkRequest
                    {
                        ticket =
                            ticket.TicketHex
                    });

            return await ApplyReturningEnvelopeAsync(
                envelope,
                false);
        }
        finally
        {
            CancelTicket(
                ticket.Handle);
        }
    }

    private bool IsSteamReady()
    {
        if (platformRuntime == null ||
            !platformRuntime.IsSteamProviderActive ||
            !platformRuntime.SteamInitialized ||
            platformRuntime.SteamId == 0)
        {
            SetError(
                "platform.error.steam_unavailable",
                "Steam provider is not initialized.");

            return false;
        }

        return true;
    }

    private async Task<bool> EnsureAtlasIdentityAsync()
    {
        ResolveReferences();

        if (lobbyBridge == null)
        {
            SetError(
                "platform.error.identity_unavailable",
                "AtlasBoardLobbyRuntimeBridge was not found.");

            return false;
        }

        bool ready =
            await lobbyBridge
                .EnsureOnlineIdentityAsync();

        if (!ready ||
            string.IsNullOrWhiteSpace(
                lobbyBridge.CurrentAccountId) ||
            string.IsNullOrWhiteSpace(
                lobbyBridge.AuthTokenForOnlineSubsystems))
        {
            SetError(
                "platform.error.identity_unavailable",
                "Authenticated Atlas account is unavailable.");

            return false;
        }

        return true;
    }

    private void ResolveReferences()
    {
        platformRuntime =
            platformRuntime != null
                ? platformRuntime
                : AtlasBoardPlatformRuntime.Instance;

        if (lobbyBridge != null)
        {
            return;
        }

        AtlasBoardLobbyRuntimeBridge[] bridges =
            Resources.FindObjectsOfTypeAll<
                AtlasBoardLobbyRuntimeBridge>();

        lobbyBridge =
            bridges.FirstOrDefault(
                item =>
                    item != null &&
                    item.gameObject.scene.IsValid());
    }

    private void EnsureTicketCallback()
    {
        if (ticketCallback != null ||
            platformRuntime == null ||
            !platformRuntime.SteamInitialized)
        {
            return;
        }

        ticketCallback =
            Callback<GetTicketForWebApiResponse_t>
                .Create(
                    HandleWebApiTicketResponse);
    }

    private async Task<WebApiTicketResult>
        RequestWebApiTicketAsync()
    {
        EnsureTicketCallback();

        if (ticketCallback == null)
        {
            return WebApiTicketResult.Fail(
                "Steam Web API ticket callback is unavailable.");
        }

        if (pendingTicketSource != null)
        {
            return WebApiTicketResult.Fail(
                "A Steam Web API ticket request is already pending.");
        }

        pendingTicketSource =
            new TaskCompletionSource<
                WebApiTicketResult>(
                    TaskCreationOptions
                        .RunContinuationsAsynchronously);

        pendingTicket =
            SteamUser.GetAuthTicketForWebApi(
                TicketIdentity);

        if (pendingTicket.m_HAuthTicket == 0)
        {
            pendingTicketSource = null;

            return WebApiTicketResult.Fail(
                "Steam rejected GetAuthTicketForWebApi.");
        }

        float deadline =
            Time.realtimeSinceStartup +
            TicketTimeoutSeconds;

        Task<WebApiTicketResult> task =
            pendingTicketSource.Task;

        while (!task.IsCompleted &&
               Time.realtimeSinceStartup < deadline)
        {
            await Task.Yield();
        }

        if (!task.IsCompleted)
        {
            HAuthTicket timedOutHandle =
                pendingTicket;

            pendingTicketSource = null;
            pendingTicket =
                HAuthTicket.Invalid;

            CancelTicket(
                timedOutHandle);

            return WebApiTicketResult.Fail(
                "Steam Web API ticket request timed out.");
        }

        return await task;
    }

    private void HandleWebApiTicketResponse(
        GetTicketForWebApiResponse_t response)
    {
        if (pendingTicketSource == null ||
            response.m_hAuthTicket.m_HAuthTicket !=
            pendingTicket.m_HAuthTicket)
        {
            return;
        }

        TaskCompletionSource<WebApiTicketResult> source =
            pendingTicketSource;

        HAuthTicket handle =
            pendingTicket;

        pendingTicketSource = null;
        pendingTicket =
            HAuthTicket.Invalid;

        if (response.m_eResult !=
            EResult.k_EResultOK)
        {
            source.TrySetResult(
                WebApiTicketResult.Fail(
                    "Steam ticket callback returned " +
                    response.m_eResult +
                    "."));

            CancelTicket(handle);
            return;
        }

        if (response.m_rgubTicket == null ||
            response.m_cubTicket <= 0 ||
            response.m_cubTicket >
            response.m_rgubTicket.Length)
        {
            source.TrySetResult(
                WebApiTicketResult.Fail(
                    "Steam returned an invalid ticket payload."));

            CancelTicket(handle);
            return;
        }

        StringBuilder hex =
            new StringBuilder(
                response.m_cubTicket * 2);

        for (int index = 0;
             index < response.m_cubTicket;
             index++)
        {
            hex.Append(
                response
                    .m_rgubTicket[index]
                    .ToString("x2"));
        }

        source.TrySetResult(
            WebApiTicketResult.Ok(
                handle,
                hex.ToString()));
    }

    private void CancelPendingTicket()
    {
        if (pendingTicket.m_HAuthTicket != 0)
        {
            CancelTicket(
                pendingTicket);
        }

        pendingTicket =
            HAuthTicket.Invalid;

        pendingTicketSource = null;
    }

    private static void CancelTicket(
        HAuthTicket handle)
    {
        if (handle.m_HAuthTicket == 0)
        {
            return;
        }

        try
        {
            SteamUser.CancelAuthTicket(
                handle);
        }
        catch
        {
            // Best effort; cancellation must not break account state.
        }
    }

    private async Task<TEnvelope>
        CallFunctionAsync<TRequest, TEnvelope>(
            string functionName,
            TRequest payload)
        where TEnvelope : class
    {
        ResetError();

        if (!await EnsureAtlasIdentityAsync())
        {
            return null;
        }

        string url =
            lobbyBridge.UsingLocalEmulators
                ? $"http://{EmulatorHost}:{FunctionsPort}/" +
                  $"{ProjectId}/{Region}/{functionName}"
                : $"https://{Region}-{ProjectId}" +
                  $".cloudfunctions.net/{functionName}";

        string requestJson =
            JsonUtility.ToJson(payload);

        string callableJson =
            "{\"data\":" +
            requestJson +
            "}";

        using UnityWebRequest request =
            new UnityWebRequest(
                url,
                UnityWebRequest.kHttpVerbPOST);

        request.uploadHandler =
            new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(
                    callableJson));

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json");

        request.SetRequestHeader(
            "Authorization",
            "Bearer " +
            lobbyBridge
                .AuthTokenForOnlineSubsystems);

        request.timeout = 20;

        await SendRequestAsync(
            request);

        string body =
            request.downloadHandler != null
                ? request.downloadHandler.text
                : string.Empty;

        if (request.result !=
            UnityWebRequest.Result.Success)
        {
            CallableErrorEnvelope error =
                SafeFromJson<
                    CallableErrorEnvelope>(
                        body);

            SetError(
                error?.error?.details?.errorKey ??
                "platform.error.service_unavailable",
                error?.error?.message ??
                $"HTTP {request.responseCode}: " +
                request.error);

            return null;
        }

        TEnvelope envelope =
            SafeFromJson<TEnvelope>(
                body);

        if (envelope == null)
        {
            SetError(
                "platform.error.service_unavailable",
                "Callable response could not be parsed.");
        }

        return envelope;
    }

    private async Task<TEnvelope>
        CallUnauthenticatedFunctionAsync<TRequest, TEnvelope>(
            string functionName,
            TRequest payload)
        where TEnvelope : class
    {
        ResetError();
        ResolveReferences();

        bool local =
            UsingLocalEmulators;

        string url =
            local
                ? $"http://{EmulatorHost}:{FunctionsPort}/" +
                  $"{ProjectId}/{Region}/{functionName}"
                : $"https://{Region}-{ProjectId}" +
                  $".cloudfunctions.net/{functionName}";

        string requestJson =
            JsonUtility.ToJson(payload);

        string callableJson =
            "{\"data\":" +
            requestJson +
            "}";

        using UnityWebRequest request =
            new UnityWebRequest(
                url,
                UnityWebRequest.kHttpVerbPOST);

        request.uploadHandler =
            new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(
                    callableJson));

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json");

        request.timeout = 20;

        await SendRequestAsync(
            request);

        string body =
            request.downloadHandler != null
                ? request.downloadHandler.text
                : string.Empty;

        if (request.result !=
            UnityWebRequest.Result.Success)
        {
            CallableErrorEnvelope error =
                SafeFromJson<
                    CallableErrorEnvelope>(
                        body);

            SetError(
                error?.error?.details?.errorKey ??
                "platform.error.service_unavailable",
                error?.error?.message ??
                $"HTTP {request.responseCode}: " +
                request.error);

            return null;
        }

        TEnvelope envelope =
            SafeFromJson<TEnvelope>(
                body);

        if (envelope == null)
        {
            SetError(
                "platform.error.service_unavailable",
                "Returning Steam sign-in response could not be parsed.");
        }

        return envelope;
    }

    private async Task<AtlasBoardSteamReturningSignInResult>
        ApplyReturningEnvelopeAsync(
            ReturningEnvelope envelope,
            bool useAuthEmulator)
    {
        if (envelope?.result == null ||
            !envelope.result.ok ||
            string.IsNullOrWhiteSpace(
                envelope.result.customToken) ||
            string.IsNullOrWhiteSpace(
                envelope.result.accountId))
        {
            return ReturningSignInFailure(
                string.IsNullOrWhiteSpace(
                    LastErrorKey)
                    ? "platform.error.returning_signin_failed"
                    : LastErrorKey,
                string.IsNullOrWhiteSpace(
                    LastTechnicalMessage)
                    ? "Returning Steam sign-in returned no valid token."
                    : LastTechnicalMessage);
        }

        try
        {
            FirebaseAuth auth =
                FirebaseAuth.DefaultInstance;

            if (useAuthEmulator)
            {
                auth.UseEmulator(
                    EmulatorHost,
                    AuthEmulatorPort);
            }

            AuthResult authResult =
                await auth
                    .SignInWithCustomTokenAsync(
                        envelope.result.customToken);

            string firebaseUid =
                authResult?.User != null
                    ? authResult.User.UserId
                    : string.Empty;

            if (!string.Equals(
                    firebaseUid,
                    envelope.result.accountId,
                    StringComparison.Ordinal))
            {
                auth.SignOut();

                return ReturningSignInFailure(
                    "platform.error.returning_signin_uid_mismatch",
                    "Firebase Auth UID did not match the linked Atlas account.");
            }

            LastReturningSignIn =
                new AtlasBoardSteamReturningSignInResult
                {
                    Success = true,
                    AccountId =
                        firebaseUid,
                    SteamId =
                        envelope.result.steamId ??
                        string.Empty,
                    Verified =
                        envelope.result.verified,
                    DevelopmentOnly =
                        envelope.result.developmentOnly,
                    VerificationMode =
                        envelope.result.verificationMode ??
                        string.Empty,
                    FirebaseAuthApplied = true
                };

            return LastReturningSignIn;
        }
        catch (Exception exception)
        {
            return ReturningSignInFailure(
                "platform.error.firebase_custom_token_signin_failed",
                exception.Message);
        }
    }

    private AtlasBoardSteamReturningSignInResult
        ReturningSignInFailure(
            string errorKey,
            string technicalMessage)
    {
        SetError(
            errorKey,
            technicalMessage);

        LastReturningSignIn =
            AtlasBoardSteamReturningSignInResult.Fail(
                errorKey,
                technicalMessage);

        return LastReturningSignIn;
    }

    private static AtlasBoardSteamLinkStatus BuildStatus(
        LinkWire wire)
    {
        if (wire == null)
        {
            return new AtlasBoardSteamLinkStatus
            {
                Success = false,
                ErrorKey =
                    "platform.error.service_unavailable",
                TechnicalMessage =
                    "Steam link response could not be decoded."
            };
        }

        return new AtlasBoardSteamLinkStatus
        {
            Success = wire.ok,
            Linked = wire.linked,
            Verified = wire.verified,
            DevelopmentOnly =
                wire.developmentOnly,
            AccountId =
                wire.accountId ??
                string.Empty,
            Provider =
                wire.provider ??
                "steam",
            SteamId =
                wire.steamId ??
                string.Empty,
            OwnerSteamId =
                wire.ownerSteamId ??
                string.Empty,
            AppId = wire.appId,
            VerificationMode =
                wire.verificationMode ??
                string.Empty,
            IntegrityOk =
                wire.integrityOk ||
                wire.linked,
            Applied = wire.applied,
            IdempotentReplay =
                wire.idempotentReplay,
            UpgradedToVerified =
                wire.upgradedToVerified,
            Repaired =
                wire.repaired
        };
    }

    private static async Task SendRequestAsync(
        UnityWebRequest request)
    {
        UnityWebRequestAsyncOperation operation =
            request.SendWebRequest();

        while (!operation.isDone)
        {
            await Task.Yield();
        }
    }

    private void ResetError()
    {
        LastErrorKey =
            string.Empty;

        LastTechnicalMessage =
            string.Empty;
    }

    private void SetError(
        string errorKey,
        string technicalMessage)
    {
        LastErrorKey =
            errorKey ?? string.Empty;

        LastTechnicalMessage =
            technicalMessage ?? string.Empty;
    }

    private AtlasBoardSteamLinkStatus FailedStatus()
    {
        return new AtlasBoardSteamLinkStatus
        {
            Success = false,
            ErrorKey =
                LastErrorKey,
            TechnicalMessage =
                LastTechnicalMessage
        };
    }

    private static T SafeFromJson<T>(
        string json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(
                json))
        {
            return null;
        }

        try
        {
            return JsonUtility
                .FromJson<T>(json);
        }
        catch
        {
            return null;
        }
    }

    [Serializable]
    private sealed class EmptyRequest
    {
    }

    [Serializable]
    private sealed class DevLinkRequest
    {
        public string steamId;
        public int appId;
    }

    [Serializable]
    private sealed class VerifiedLinkRequest
    {
        public string ticket;
    }

    [Serializable]
    private sealed class DevReturningRequest
    {
        public string steamId;
    }

    [Serializable]
    private sealed class ReturningEnvelope
    {
        public ReturningWire result;
    }

    [Serializable]
    private sealed class ReturningWire
    {
        public bool ok;
        public string accountId;
        public string provider;
        public string steamId;
        public string ownerSteamId;
        public int appId;
        public bool verified;
        public bool developmentOnly;
        public string verificationMode;
        public string customToken;
    }

    [Serializable]
    private sealed class StatusEnvelope
    {
        public LinkWire result;
    }

    [Serializable]
    private sealed class LinkEnvelope
    {
        public LinkWire result;
    }

    [Serializable]
    private sealed class LinkWire
    {
        public bool ok;
        public bool linked;
        public bool verified;
        public bool developmentOnly;
        public string accountId;
        public string provider;
        public string steamId;
        public string ownerSteamId;
        public int appId;
        public string verificationMode;
        public bool integrityOk;
        public bool applied;
        public bool idempotentReplay;
        public bool upgradedToVerified;
        public bool repaired;
    }

    [Serializable]
    private sealed class CallableErrorEnvelope
    {
        public CallableError error;
    }

    [Serializable]
    private sealed class CallableError
    {
        public string message;
        public CallableErrorDetails details;
    }

    [Serializable]
    private sealed class CallableErrorDetails
    {
        public string errorKey;
    }

    private sealed class WebApiTicketResult
    {
        public bool Success;
        public HAuthTicket Handle;
        public string TicketHex;
        public string TechnicalMessage;

        public static WebApiTicketResult Ok(
            HAuthTicket handle,
            string ticketHex)
        {
            return new WebApiTicketResult
            {
                Success = true,
                Handle = handle,
                TicketHex =
                    ticketHex ?? string.Empty,
                TechnicalMessage =
                    string.Empty
            };
        }

        public static WebApiTicketResult Fail(
            string technicalMessage)
        {
            return new WebApiTicketResult
            {
                Success = false,
                Handle =
                    HAuthTicket.Invalid,
                TicketHex =
                    string.Empty,
                TechnicalMessage =
                    technicalMessage ??
                    string.Empty
            };
        }
    }
}

[Serializable]
public sealed class AtlasBoardSteamLinkStatus
{
    public bool Success;
    public bool Linked;
    public bool Verified;
    public bool DevelopmentOnly;
    public string AccountId =
        string.Empty;
    public string Provider =
        "steam";
    public string SteamId =
        string.Empty;
    public string OwnerSteamId =
        string.Empty;
    public int AppId;
    public string VerificationMode =
        string.Empty;
    public bool IntegrityOk;
    public bool Applied;
    public bool IdempotentReplay;
    public bool UpgradedToVerified;
    public bool Repaired;
    public string ErrorKey =
        string.Empty;
    public string TechnicalMessage =
        string.Empty;

    public static AtlasBoardSteamLinkStatus Unlinked()
    {
        return new AtlasBoardSteamLinkStatus
        {
            Success = true,
            Linked = false,
            Verified = false,
            DevelopmentOnly = false,
            IntegrityOk = true
        };
    }


}

[Serializable]
public sealed class AtlasBoardSteamReturningSignInResult
{
    public bool Success;
    public string AccountId =
        string.Empty;
    public string SteamId =
        string.Empty;
    public bool Verified;
    public bool DevelopmentOnly;
    public string VerificationMode =
        string.Empty;
    public bool FirebaseAuthApplied;
    public string ErrorKey =
        string.Empty;
    public string TechnicalMessage =
        string.Empty;

    public static AtlasBoardSteamReturningSignInResult Empty()
    {
        return new AtlasBoardSteamReturningSignInResult();
    }

    public static AtlasBoardSteamReturningSignInResult Fail(
        string errorKey,
        string technicalMessage)
    {
        return new AtlasBoardSteamReturningSignInResult
        {
            Success = false,
            ErrorKey =
                errorKey ??
                string.Empty,
            TechnicalMessage =
                technicalMessage ??
                string.Empty
        };
    }
}

