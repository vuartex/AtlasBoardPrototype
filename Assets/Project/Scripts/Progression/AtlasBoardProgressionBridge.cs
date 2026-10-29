using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
public sealed class AtlasBoardProgressionBridge : MonoBehaviour
{
    private const string ProjectId = "atlasboard-usa";
    private const string Region = "europe-west1";
    private const int FunctionsPort = 5001;

    private AtlasBoardLobbyRuntimeBridge lobbyBridge;
    private string lastErrorKey = string.Empty;
    private string lastTechnicalMessage = string.Empty;

    public string CurrentAccountId
    {
        get
        {
            ResolveLobbyBridge();
            return lobbyBridge != null
                ? lobbyBridge.CurrentAccountId
                : string.Empty;
        }
    }

    public event Action<AtlasBoardProgressionSnapshot> ProfileChanged;

    private void Awake()
    {
        ResolveLobbyBridge();
    }

    public async Task<AtlasBoardProgressionSnapshot> GetProfileAsync()
    {
        if (!await EnsureIdentityAsync())
        {
            return FailedSnapshot();
        }

        string uid = lobbyBridge.CurrentAccountId;
        string token = lobbyBridge.AuthTokenForOnlineSubsystems;

        string fallbackDisplayName = string.Empty;
        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user != null)
                fallbackDisplayName = user.DisplayName ?? string.Empty;
        }
        catch
        {
            fallbackDisplayName = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(fallbackDisplayName))
        {
            AtlasBoardPlatformRuntime platform = AtlasBoardPlatformRuntime.Instance;
            if (platform != null &&
                platform.IsSteamProviderActive &&
                platform.SteamInitialized)
            {
                fallbackDisplayName =
                    platform.SteamPersonaName ?? string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(fallbackDisplayName))
            fallbackDisplayName = "Atlas Player";

        AtlasBoardProgressionSnapshot snapshot =
            await AtlasBoardRtdbCareerReadModel.ReadProfileAsync(
                uid,
                token,
                fallbackDisplayName);

        if (snapshot == null || !snapshot.Success)
        {
            lastErrorKey =
                snapshot != null &&
                !string.IsNullOrWhiteSpace(snapshot.ErrorKey)
                    ? snapshot.ErrorKey
                    : "progression.error.service_unavailable";
            lastTechnicalMessage =
                snapshot != null
                    ? snapshot.TechnicalMessage ?? string.Empty
                    : "RTDB career profile returned no snapshot.";
            return FailedSnapshot();
        }

        lastErrorKey = string.Empty;
        lastTechnicalMessage = string.Empty;

        ProfileChanged?.Invoke(snapshot);
        return snapshot;
    }

    public async Task<AtlasBoardProgressionRecordResult>
        RecordCompletedMatchAsync(
            string matchId,
            int completedTurns,
            IReadOnlyList<AtlasBoardProgressionSlotTelemetry> telemetry)
    {
        // The legacy callable belongs to the old hosted backend. Live Spark
        // matches do not have a trusted verifier and may NEVER grant XP on the
        // authority of a client/host payload. Keep the local emulator QA path.
        ResolveLobbyBridge();
        if (lobbyBridge == null || !lobbyBridge.UsingLocalEmulators)
        {
            lastErrorKey = "progression.error.trusted_verifier_required";
            lastTechnicalMessage = "Live Spark RTDB has no trusted completed-match verifier.";
            return FailedRecordResult(matchId);
        }
        if (!await EnsureIdentityAsync())
        {
            return FailedRecordResult(matchId);
        }

        List<AtlasBoardProgressionSlotTelemetry> rows =
            new List<AtlasBoardProgressionSlotTelemetry>();
        if (telemetry != null)
        {
            for (int index = 0; index < telemetry.Count; index++)
            {
                AtlasBoardProgressionSlotTelemetry item = telemetry[index];
                if (item != null)
                {
                    rows.Add(item);
                }
            }
        }

        RecordEnvelope envelope =
            await CallFunctionAsync<RecordRequest, RecordEnvelope>(
                "progressionRecordCompletedMatch",
                new RecordRequest
                {
                    matchId = matchId ?? string.Empty,
                    completedTurns = Mathf.Max(0, completedTurns),
                    telemetry = rows.ToArray()
                });

        if (envelope == null ||
            envelope.result == null ||
            !envelope.result.ok)
        {
            return FailedRecordResult(matchId);
        }

        return new AtlasBoardProgressionRecordResult
        {
            Success = true,
            MatchId = envelope.result.matchId ?? matchId ?? string.Empty,
            Applied = envelope.result.applied,
            IdempotentReplay = envelope.result.idempotentReplay,
            AffectedAccountCount = envelope.result.affectedAccountCount
        };
    }

    private async Task<bool> EnsureIdentityAsync()
    {
        ResolveLobbyBridge();
        if (lobbyBridge == null)
        {
            lastErrorKey = "progression.error.identity_unavailable";
            lastTechnicalMessage =
                "AtlasBoardLobbyRuntimeBridge was not found.";
            return false;
        }

        bool ready = await lobbyBridge.EnsureOnlineIdentityAsync();
        if (!ready ||
            string.IsNullOrWhiteSpace(lobbyBridge.CurrentAccountId) ||
            string.IsNullOrWhiteSpace(
                lobbyBridge.AuthTokenForOnlineSubsystems))
        {
            lastErrorKey = "progression.error.identity_unavailable";
            lastTechnicalMessage =
                "Authenticated Atlas Board identity is unavailable.";
            return false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // Firebase Anonymous proves an RTDB room UID, NOT a trusted
        // XP/Gold/Steam reward. Server-side security acceptance stays OPEN.
        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null || user.IsAnonymous)
            {
                lastErrorKey = "progression.error.identity_unavailable";
                lastTechnicalMessage =
                    "Android Casual is not eligible for trusted progression.";
                return false;
            }
        }
        catch (Exception)
        {
            lastErrorKey = "progression.error.identity_unavailable";
            lastTechnicalMessage = "Android identity validation unavailable.";
            return false;
        }
#endif
        return true;
    }

    private void ResolveLobbyBridge()
    {
        if (lobbyBridge != null)
        {
            return;
        }

        AtlasBoardLobbyRuntimeBridge[] candidates =
            Resources.FindObjectsOfTypeAll<AtlasBoardLobbyRuntimeBridge>();
        foreach (AtlasBoardLobbyRuntimeBridge candidate in candidates)
        {
            if (candidate != null && candidate.gameObject.scene.IsValid())
            {
                lobbyBridge = candidate;
                return;
            }
        }
    }

    private async Task<TEnvelope> CallFunctionAsync<TRequest, TEnvelope>(
        string functionName,
        TRequest payload)
        where TEnvelope : class
    {
        lastErrorKey = string.Empty;
        lastTechnicalMessage = string.Empty;

        string url = lobbyBridge.UsingLocalEmulators
            ? $"http://{lobbyBridge.EmulatorHostForOnlineSubsystems}:{FunctionsPort}/" +
              $"{ProjectId}/{Region}/{functionName}"
            : $"https://{Region}-{ProjectId}.cloudfunctions.net/" +
              functionName;

        string requestJson = JsonUtility.ToJson(payload);
        string callableJson = "{\"data\":" + requestJson + "}";

        using UnityWebRequest request =
            new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler =
            new UploadHandlerRaw(Encoding.UTF8.GetBytes(callableJson));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader(
            "Authorization",
            "Bearer " + lobbyBridge.AuthTokenForOnlineSubsystems);
        request.timeout = 20;

        await SendRequestAsync(request);
        string body = request.downloadHandler != null
            ? request.downloadHandler.text
            : string.Empty;

        if (request.result != UnityWebRequest.Result.Success)
        {
            CallableErrorEnvelope error =
                SafeFromJson<CallableErrorEnvelope>(body);
            lastErrorKey =
                error != null &&
                error.error != null &&
                error.error.details != null &&
                !string.IsNullOrWhiteSpace(error.error.details.errorKey)
                    ? error.error.details.errorKey
                    : "progression.error.service_unavailable";
            lastTechnicalMessage =
                error != null &&
                error.error != null &&
                !string.IsNullOrWhiteSpace(error.error.message)
                    ? error.error.message
                    : $"HTTP {request.responseCode}: {request.error}";
            return null;
        }

        TEnvelope envelope = SafeFromJson<TEnvelope>(body);
        if (envelope == null)
        {
            lastErrorKey = "progression.error.service_unavailable";
            lastTechnicalMessage = "Callable response could not be parsed.";
        }

        return envelope;
    }

    private AtlasBoardProgressionSnapshot FailedSnapshot()
    {
        return new AtlasBoardProgressionSnapshot
        {
            Success = false,
            ErrorKey = string.IsNullOrWhiteSpace(lastErrorKey)
                ? "progression.error.service_unavailable"
                : lastErrorKey,
            TechnicalMessage = lastTechnicalMessage ?? string.Empty
        };
    }

    private AtlasBoardProgressionRecordResult FailedRecordResult(
        string matchId)
    {
        return new AtlasBoardProgressionRecordResult
        {
            Success = false,
            MatchId = matchId ?? string.Empty,
            ErrorKey = string.IsNullOrWhiteSpace(lastErrorKey)
                ? "progression.error.service_unavailable"
                : lastErrorKey,
            TechnicalMessage = lastTechnicalMessage ?? string.Empty
        };
    }

    private static async Task SendRequestAsync(UnityWebRequest request)
    {
        await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);
        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            await Task.Yield();
        }
    }

    private static T SafeFromJson<T>(string json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch
        {
            return null;
        }
    }


    [Serializable]
    private sealed class RecordRequest
    {
        public string matchId;
        public int completedTurns;
        public AtlasBoardProgressionSlotTelemetry[] telemetry;
    }


    [Serializable]
    private sealed class RecordEnvelope
    {
        public RecordWire result;
    }

    [Serializable]
    private sealed class RecordWire
    {
        public bool ok;
        public string matchId;
        public bool applied;
        public bool idempotentReplay;
        public int affectedAccountCount;
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
}
