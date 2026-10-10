using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Firebase;
using Firebase.AppCheck;
using Steamworks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class AtlasBoardSteamAppCheckProviderFactory :
    IAppCheckProviderFactory
{
    public IAppCheckProvider CreateProvider(
        FirebaseApp app)
    {
        return new AtlasBoardSteamAppCheckProvider(
            app);
    }
}

public sealed class AtlasBoardSteamAppCheckProvider :
    IAppCheckProvider
{
    private const string ProjectId =
        "atlasboard-usa";

    private const string Region =
        "europe-west1";

    private const string FunctionName =
        "platformSteamCreateAppCheckToken";

    private const string TicketIdentity =
        "atlasboard-appcheck-v1";

    private const float RuntimeReadyTimeoutSeconds =
        20f;

    private const float TicketTimeoutSeconds =
        12f;

    private readonly FirebaseApp firebaseApp;

    private readonly SemaphoreSlim exchangeGate =
        new SemaphoreSlim(
            1,
            1);

    public AtlasBoardSteamAppCheckProvider(
        FirebaseApp app)
    {
        firebaseApp =
            app ??
            throw new ArgumentNullException(
                nameof(app));
    }

    public Task<AppCheckToken> GetLimitedUseTokenAsync()
    {
        // Firebase Unity 13.9+ requires custom providers to implement this
        // method. AtlasBoard does not currently need replay-protected
        // limited-use tokens, so the official fallback is to return a
        // standard App Check token.
        return GetTokenAsync();
    }

    public async Task<AppCheckToken> GetTokenAsync()
    {
        await exchangeGate.WaitAsync();

        try
        {
            AtlasBoardPlatformRuntime runtime =
                await WaitForSteamRuntimeAsync();

            SteamTicketLease ticket =
                await AcquireSteamTicketAsync(
                    runtime);

            try
            {
                return await ExchangeTicketAsync(
                    runtime,
                    ticket.TicketHex);
            }
            finally
            {
                CancelTicket(
                    ticket.Handle);
            }
        }
        finally
        {
            exchangeGate.Release();
        }
    }

    private static async Task<AtlasBoardPlatformRuntime>
        WaitForSteamRuntimeAsync()
    {
        float deadline =
            Time.realtimeSinceStartup +
            RuntimeReadyTimeoutSeconds;

        while (Time.realtimeSinceStartup <
               deadline)
        {
            AtlasBoardPlatformRuntime runtime =
                AtlasBoardPlatformRuntime.Instance;

            if (runtime != null &&
                runtime.IsSteamProviderActive &&
                runtime.SteamInitialized &&
                runtime.SteamId != 0 &&
                runtime.SteamAppId != 0)
            {
                return runtime;
            }

            await Task.Yield();
        }

        throw new InvalidOperationException(
            "Steam App Check attestation could not start because the " +
            "AtlasBoard Steam platform provider did not become ready.");
    }

    private static async Task<SteamTicketLease>
        AcquireSteamTicketAsync(
            AtlasBoardPlatformRuntime runtime)
    {
        if (runtime == null ||
            !runtime.SteamInitialized ||
            runtime.SteamId == 0)
        {
            throw new InvalidOperationException(
                "Steam runtime is unavailable for App Check attestation.");
        }

        TaskCompletionSource<SteamTicketLease> source =
            new TaskCompletionSource<SteamTicketLease>(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        HAuthTicket requestedHandle =
            HAuthTicket.Invalid;

        using Callback<GetTicketForWebApiResponse_t> callback =
            Callback<GetTicketForWebApiResponse_t>
                .Create(
                    response =>
                    {
                        if (requestedHandle.m_HAuthTicket == 0 ||
                            response.m_hAuthTicket.m_HAuthTicket !=
                            requestedHandle.m_HAuthTicket)
                        {
                            return;
                        }

                        if (response.m_eResult !=
                            EResult.k_EResultOK)
                        {
                            source.TrySetException(
                                new InvalidOperationException(
                                    "Steam App Check ticket callback returned " +
                                    response.m_eResult +
                                    "."));

                            return;
                        }

                        if (response.m_rgubTicket == null ||
                            response.m_cubTicket <= 0 ||
                            response.m_cubTicket >
                            response.m_rgubTicket.Length)
                        {
                            source.TrySetException(
                                new InvalidOperationException(
                                    "Steam returned an invalid App Check " +
                                    "ticket payload."));

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
                            new SteamTicketLease(
                                requestedHandle,
                                hex.ToString()));
                    });

        requestedHandle =
            SteamUser.GetAuthTicketForWebApi(
                TicketIdentity);

        if (requestedHandle.m_HAuthTicket == 0)
        {
            throw new InvalidOperationException(
                "Steam rejected GetAuthTicketForWebApi for App Check.");
        }

        float deadline =
            Time.realtimeSinceStartup +
            TicketTimeoutSeconds;

        try
        {
            while (!source.Task.IsCompleted &&
                   Time.realtimeSinceStartup <
                   deadline)
            {
                SteamAPI.RunCallbacks();
                await Task.Yield();
            }

            if (!source.Task.IsCompleted)
            {
                throw new TimeoutException(
                    "Steam App Check Web API ticket request timed out.");
            }

            return await source.Task;
        }
        catch
        {
            CancelTicket(
                requestedHandle);

            throw;
        }
    }

    private async Task<AppCheckToken>
        ExchangeTicketAsync(
            AtlasBoardPlatformRuntime runtime,
            string ticketHex)
    {
        if (string.IsNullOrWhiteSpace(
                ticketHex))
        {
            throw new InvalidOperationException(
                "Steam App Check ticket was empty.");
        }

        string url =
            $"https://{Region}-{ProjectId}" +
            $".cloudfunctions.net/{FunctionName}";

        SteamExchangeRequest payload =
            new SteamExchangeRequest
            {
                ticket =
                    ticketHex
            };

        string callableJson =
            "{\"data\":" +
            JsonUtility.ToJson(
                payload) +
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

        request.timeout =
            20;

        UnityWebRequestAsyncOperation operation =
            request.SendWebRequest();

        while (!operation.isDone)
        {
            await Task.Yield();
        }

        string responseBody =
            request.downloadHandler != null
                ? request.downloadHandler.text
                : string.Empty;

        if (request.result !=
            UnityWebRequest.Result.Success)
        {
            CallableErrorEnvelope error =
                SafeFromJson<CallableErrorEnvelope>(
                    responseBody);

            string message =
                error?.error?.message;

            if (string.IsNullOrWhiteSpace(
                    message))
            {
                message =
                    $"HTTP {request.responseCode}: " +
                    request.error;
            }

            throw new InvalidOperationException(
                "Steam App Check token exchange failed. " +
                message);
        }

        SteamExchangeEnvelope envelope =
            SafeFromJson<SteamExchangeEnvelope>(
                responseBody);

        SteamExchangeWire result =
            envelope?.result;

        if (result == null ||
            !result.ok ||
            string.IsNullOrWhiteSpace(
                result.token) ||
            result.expiresAtEpochSeconds <= 0)
        {
            throw new InvalidOperationException(
                "Steam App Check token exchange returned an invalid result.");
        }

        string expectedFirebaseAppId =
            firebaseApp.Options != null
                ? firebaseApp.Options.AppId
                : string.Empty;

        if (!string.Equals(
                result.appId,
                expectedFirebaseAppId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Steam App Check Firebase App ID mismatch.");
        }

        if (result.steamAppId !=
            (int)runtime.SteamAppId)
        {
            throw new InvalidOperationException(
                "Steam App Check Steam AppID mismatch.");
        }

        if (!string.Equals(
                result.steamId,
                runtime.SteamId.ToString(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Steam App Check Steam identity mismatch.");
        }

        DateTime expireTime =
            DateTimeOffset
                .FromUnixTimeSeconds(
                    result.expiresAtEpochSeconds)
                .UtcDateTime;

        if (expireTime <=
            DateTime.UtcNow)
        {
            throw new InvalidOperationException(
                "Steam App Check token was already expired.");
        }

        Debug.Log(
            "Phase 15E Steam custom App Check attestation PASS. " +
            $"SteamAppID={result.steamAppId}, " +
            $"TTL={result.ttlMillis / 1000}s. " +
            "Token and Steam ticket were not logged.");

        return new AppCheckToken
        {
            Token =
                result.token,
            ExpireTime =
                expireTime
        };
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
            // Best effort. Never expose or retain a proof ticket longer than
            // required for the exchange.
        }
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
                .FromJson<T>(
                    json);
        }
        catch
        {
            return null;
        }
    }

    [Serializable]
    private sealed class SteamExchangeRequest
    {
        public string ticket;
    }

    [Serializable]
    private sealed class SteamExchangeEnvelope
    {
        public SteamExchangeWire result;
    }

    [Serializable]
    private sealed class SteamExchangeWire
    {
        public bool ok;
        public string token;
        public long ttlMillis;
        public long expiresAtEpochSeconds;
        public string appId;
        public int steamAppId;
        public string steamId;
        public string verificationMode;
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
    }

    private sealed class SteamTicketLease
    {
        public HAuthTicket Handle { get; }
        public string TicketHex { get; }

        public SteamTicketLease(
            HAuthTicket handle,
            string ticketHex)
        {
            Handle =
                handle;

            TicketHex =
                ticketHex ??
                string.Empty;
        }
    }
}
