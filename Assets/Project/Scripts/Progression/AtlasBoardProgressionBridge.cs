using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
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

        ProfileEnvelope envelope =
            await CallFunctionAsync<EmptyRequest, ProfileEnvelope>(
                "progressionGetProfile",
                new EmptyRequest());

        if (envelope == null ||
            envelope.result == null ||
            !envelope.result.ok)
        {
            return FailedSnapshot();
        }

        ProfileWire wire = envelope.result;
        AtlasBoardProgressionSnapshot snapshot =
            new AtlasBoardProgressionSnapshot
            {
                Success = true,
                AccountId = wire.accountId ?? string.Empty,
                DisplayName = wire.displayName ?? string.Empty,
                AvatarId = wire.avatarId ?? string.Empty,
                TotalXp = Mathf.Max(0, wire.totalXp),
                Level = Mathf.Max(1, wire.level),
                LevelStartXp = Mathf.Max(0, wire.levelStartXp),
                NextLevelXp = Mathf.Max(0, wire.nextLevelXp),
                MaxLevel = Mathf.Max(1, wire.maxLevel),
                HistoryRetentionHours = Mathf.Max(1, wire.historyRetentionHours),
                HistoryLimit = Mathf.Max(1, wire.historyLimit),
                Stats = wire.stats ?? new AtlasBoardCareerStats()
            };

        if (wire.mapStats != null)
        {
            snapshot.MapStats.AddRange(wire.mapStats);
        }

        if (wire.achievements != null)
        {
            snapshot.Achievements.AddRange(wire.achievements);
        }

        if (wire.recentMatches != null)
        {
            snapshot.RecentMatches.AddRange(wire.recentMatches);
        }

        ProfileChanged?.Invoke(snapshot);
        return snapshot;
    }

    public async Task<AtlasBoardProgressionRecordResult>
        RecordCompletedMatchAsync(
            string matchId,
            int completedTurns,
            IReadOnlyList<AtlasBoardProgressionSlotTelemetry> telemetry)
    {
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
    private sealed class EmptyRequest
    {
    }

    [Serializable]
    private sealed class RecordRequest
    {
        public string matchId;
        public int completedTurns;
        public AtlasBoardProgressionSlotTelemetry[] telemetry;
    }

    [Serializable]
    private sealed class ProfileEnvelope
    {
        public ProfileWire result;
    }

    [Serializable]
    private sealed class ProfileWire
    {
        public bool ok;
        public string accountId;
        public string displayName;
        public string avatarId;
        public int totalXp;
        public int level;
        public int levelStartXp;
        public int nextLevelXp;
        public int maxLevel;
        public AtlasBoardCareerStats stats;
        public AtlasBoardMapCareerStats[] mapStats;
        public AtlasBoardProgressionAchievement[] achievements;
        public AtlasBoardProgressionMatchSummary[] recentMatches;
        public int historyRetentionHours;
        public int historyLimit;
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
