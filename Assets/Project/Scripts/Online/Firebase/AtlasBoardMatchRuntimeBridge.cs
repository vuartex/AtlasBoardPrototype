using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
[RequireComponent(typeof(AtlasBoardLobbyRuntimeBridge))]
public sealed class AtlasBoardMatchRuntimeBridge :
    MonoBehaviour
{
    private const string ProjectId =
        "atlasboard-usa";

    private const string Region =
        "europe-west1";

    private const string EmulatorHost =
        "127.0.0.1";

    private const int FunctionsEmulatorPort =
        5001;

    [SerializeField, Min(0.25f)]
    private float snapshotPollSeconds =
        0.5f;

    [SerializeField]
    private bool autoPollAfterMatchBootstrap =
        true;

    private AtlasBoardLobbyRuntimeBridge
        lobbyBridge;

    private AtlasMatchNetworkSnapshot
        currentSnapshot;

    private bool pollInFlight;
    private float nextPollAt;
    private string expectedMatchId = string.Empty;

    public event Action<AtlasMatchNetworkSnapshot>
        SnapshotChanged;

    public AtlasMatchNetworkSnapshot
        CurrentSnapshot =>
            currentSnapshot;

    public string CurrentMatchId =>
        lobbyBridge != null &&
        lobbyBridge.CurrentSnapshot != null
            ? lobbyBridge.CurrentSnapshot.MatchId
            : string.Empty;

    private void Awake()
    {
        ResolveReferences();
    }

    private async void Update()
    {
        if (!autoPollAfterMatchBootstrap ||
            pollInFlight ||
            Time.unscaledTime < nextPollAt)
        {
            return;
        }

        string matchId =
            CurrentMatchId;

        if (string.IsNullOrWhiteSpace(matchId))
        {
            return;
        }

        nextPollAt =
            Time.unscaledTime +
            Mathf.Max(
                0.25f,
                snapshotPollSeconds);

        pollInFlight = true;

        try
        {
            AtlasMatchNetworkResult result =
                await GetSnapshotAsync(
                    matchId);

            if (result.Success &&
                result.Snapshot != null)
            {
                AcceptSnapshot(
                    result.Snapshot);
            }
        }
        finally
        {
            pollInFlight = false;
        }
    }

    public async Task<AtlasMatchNetworkResult>
        GetSnapshotAsync(
            string matchId = "")
    {
        string resolvedMatchId =
            ResolveMatchId(matchId);

        if (string.IsNullOrWhiteSpace(
                resolvedMatchId))
        {
            return AtlasMatchNetworkResult.Fail(
                "match.error.not_found",
                "No active matchId is available.");
        }

        return await CallAsync(
            "matchGetSnapshot",
            new MatchIdRequest
            {
                matchId =
                    resolvedMatchId
            },
            ParseSnapshotEnvelope);
    }

    public void ResetForMatchSession(
        string matchId)
    {
        currentSnapshot = null;
        pollInFlight = false;
        nextPollAt = 0f;
        expectedMatchId =
            matchId ?? string.Empty;

        Debug.Log(
            "AtlasBoard match snapshot cache reset for session " +
            (matchId ?? string.Empty) + ".",
            this);
    }

    public async Task<AtlasMatchNetworkResult>
        RefreshSnapshotNowAsync(
            string matchId = "")
    {
        AtlasMatchNetworkResult result =
            await GetSnapshotAsync(matchId);

        if (result.Success &&
            result.Snapshot != null)
        {
            AcceptSnapshot(result.Snapshot);
        }

        return result;
    }

    public async Task<AtlasMatchNetworkResult>
        SubmitIntentAsync(
            string intentType,
            string payloadJson,
            string clientCommandId,
            string matchId = "")
    {
        string resolvedMatchId =
            ResolveMatchId(matchId);

        return await CallAsync(
            "matchSubmitIntent",
            new SubmitIntentRequest
            {
                matchId =
                    resolvedMatchId,
                intentType =
                    intentType ?? string.Empty,
                payloadJson =
                    string.IsNullOrWhiteSpace(
                        payloadJson)
                        ? "{}"
                        : payloadJson,
                clientCommandId =
                    clientCommandId ?? string.Empty
            },
            ParseIntentResultEnvelope);
    }

    public async Task<AtlasMatchNetworkResult>
        LeaveActiveMatchAsync(
            string matchId = "")
    {
        string resolvedMatchId =
            ResolveMatchId(matchId);

        return await CallAsync(
            "matchLeaveActive",
            new MatchIdRequest
            {
                matchId = resolvedMatchId
            },
            ParseSnapshotEnvelope);
    }

    public async Task<AtlasMatchNetworkResult>
        HostMarkAfkRemovedAsync(
            int slotIndex,
            string matchId = "")
    {
        string resolvedMatchId =
            ResolveMatchId(matchId);

        return await CallAsync(
            "matchHostMarkAfkRemoved",
            new MatchSeatSlotRequest
            {
                matchId = resolvedMatchId,
                slotIndex = slotIndex
            },
            ParseSnapshotEnvelope);
    }

    public async Task<AtlasMatchNetworkResult>
        HostExpireReconnectsAsync(
            string matchId = "")
    {
        string resolvedMatchId =
            ResolveMatchId(matchId);

        return await CallAsync(
            "matchHostExpireReconnects",
            new MatchIdRequest
            {
                matchId = resolvedMatchId
            },
            ParseSnapshotEnvelope);
    }

    public async Task<AtlasMatchNetworkResult>
        HostPrepareRematchAsync(
            string matchId = "")
    {
        string resolvedMatchId =
            ResolveMatchId(matchId);

        return await CallAsync(
            "matchHostPrepareRematch",
            new MatchIdRequest
            {
                matchId = resolvedMatchId
            },
            ParseSnapshotEnvelope);
    }

    public async Task<AtlasMatchNetworkResult>
        HostListPendingIntentsAsync(
            string matchId = "")
    {
        string resolvedMatchId =
            ResolveMatchId(matchId);

        return await CallAsync(
            "matchHostListPendingIntents",
            new MatchIdRequest
            {
                matchId =
                    resolvedMatchId
            },
            ParseIntentListEnvelope);
    }

    public async Task<AtlasMatchNetworkResult>
        HostPublishStateAsync(
            int expectedRevision,
            string phase,
            string turnSeatId,
            int eventSequence,
            string snapshotJson,
            string matchId = "")
    {
        string resolvedMatchId =
            ResolveMatchId(matchId);

        return await CallAsync(
            "matchHostPublishState",
            new PublishStateRequest
            {
                matchId =
                    resolvedMatchId,
                expectedRevision =
                    expectedRevision,
                phase =
                    phase ?? string.Empty,
                turnSeatId =
                    turnSeatId ?? string.Empty,
                eventSequence =
                    eventSequence,
                snapshotJson =
                    string.IsNullOrWhiteSpace(
                        snapshotJson)
                        ? "{}"
                        : snapshotJson
            },
            ParsePublishEnvelope);
    }

    public async Task<AtlasMatchNetworkResult>
        HostAcknowledgeIntentsAsync(
            IEnumerable<string> intentIds,
            string matchId = "")
    {
        string resolvedMatchId =
            ResolveMatchId(matchId);

        List<string> ids =
            intentIds != null
                ? new List<string>(intentIds)
                : new List<string>();

        return await CallAsync(
            "matchHostAcknowledgeIntents",
            new AcknowledgeIntentRequest
            {
                matchId =
                    resolvedMatchId,
                intentIds =
                    ids.ToArray()
            },
            ParseAcknowledgeEnvelope);
    }

    private void ResolveReferences()
    {
        if (lobbyBridge == null)
        {
            lobbyBridge =
                GetComponent<
                    AtlasBoardLobbyRuntimeBridge>();
        }
    }

    private string ResolveMatchId(
        string explicitMatchId)
    {
        if (!string.IsNullOrWhiteSpace(
                explicitMatchId))
        {
            return explicitMatchId.Trim();
        }

        return CurrentMatchId;
    }

    private void AcceptSnapshot(
        AtlasMatchNetworkSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(expectedMatchId))
        {
            if (!string.Equals(
                    expectedMatchId,
                    snapshot.MatchId,
                    StringComparison.Ordinal))
            {
                return;
            }
        }
        else
        {
            // ResetForMatchSession("") intentionally means there is no
            // active match. An HTTP poll started before that reset may finish
            // later; never let its old snapshot resurrect dice/HUD/result or
            // controller state in the lobby or the next match.
            string lobbyMatchId = CurrentMatchId;

            if (string.IsNullOrWhiteSpace(lobbyMatchId) ||
                !string.Equals(
                    lobbyMatchId,
                    snapshot.MatchId,
                    StringComparison.Ordinal))
            {
                return;
            }
        }

        if (currentSnapshot != null &&
            string.Equals(
                currentSnapshot.MatchId,
                snapshot.MatchId,
                StringComparison.Ordinal) &&
            string.Equals(
                currentSnapshot.LobbyId,
                snapshot.LobbyId,
                StringComparison.Ordinal) &&
            currentSnapshot.Revision ==
                snapshot.Revision &&
            string.Equals(
                currentSnapshot.Phase,
                snapshot.Phase,
                StringComparison.Ordinal) &&
            currentSnapshot.EventSequence ==
                snapshot.EventSequence &&
            string.Equals(
                currentSnapshot.TurnSeatId,
                snapshot.TurnSeatId,
                StringComparison.Ordinal) &&
            string.Equals(
                currentSnapshot.SnapshotJson,
                snapshot.SnapshotJson,
                StringComparison.Ordinal) &&
            SeatSnapshotsEquivalent(
                currentSnapshot.Seats,
                snapshot.Seats))
        {
            return;
        }

        currentSnapshot =
            snapshot;

        SnapshotChanged?.Invoke(
            currentSnapshot);
    }

    private static bool SeatSnapshotsEquivalent(
        List<AtlasMatchNetworkSeat> left,
        List<AtlasMatchNetworkSeat> right)
    {
        int leftCount = left != null ? left.Count : 0;
        int rightCount = right != null ? right.Count : 0;

        if (leftCount != rightCount)
        {
            return false;
        }

        for (int index = 0; index < leftCount; index++)
        {
            AtlasMatchNetworkSeat a = left[index];
            AtlasMatchNetworkSeat b = right[index];

            if (a == null || b == null)
            {
                if (a != b)
                {
                    return false;
                }

                continue;
            }

            if (a.SlotIndex != b.SlotIndex ||
                a.IsHost != b.IsHost ||
                a.ReconnectExpiresAtEpochMs != b.ReconnectExpiresAtEpochMs ||
                a.AfkLockedOut != b.AfkLockedOut ||
                !string.Equals(a.SeatId, b.SeatId, StringComparison.Ordinal) ||
                !string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) ||
                !string.Equals(a.ControllerKind, b.ControllerKind, StringComparison.Ordinal) ||
                !string.Equals(a.ConnectionState, b.ConnectionState, StringComparison.Ordinal) ||
                !string.Equals(a.RemovalReason, b.RemovalReason, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<AtlasMatchNetworkResult>
        CallAsync<TRequest>(
            string functionName,
            TRequest requestData,
            Func<string, AtlasMatchNetworkResult>
                parser)
    {
        ResolveReferences();

        if (lobbyBridge == null)
        {
            return AtlasMatchNetworkResult.Fail(
                "match.error.service_unavailable",
                "Lobby runtime bridge is missing.");
        }

        bool identityReady =
            await lobbyBridge
                .EnsureOnlineIdentityAsync();

        if (!identityReady ||
            string.IsNullOrWhiteSpace(
                lobbyBridge
                    .AuthTokenForOnlineSubsystems))
        {
            return AtlasMatchNetworkResult.Fail(
                "account.error.authentication_required",
                "Online identity is unavailable.");
        }

        string url =
            lobbyBridge.UsingLocalEmulators
                ? $"http://{EmulatorHost}:" +
                  $"{FunctionsEmulatorPort}/" +
                  $"{ProjectId}/{Region}/" +
                  functionName
                : $"https://{Region}-" +
                  $"{ProjectId}.cloudfunctions.net/" +
                  functionName;

        string dataJson =
            JsonUtility.ToJson(
                requestData);

        string callableJson =
            "{\"data\":" +
            dataJson +
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

        UnityWebRequestAsyncOperation operation =
            request.SendWebRequest();

        TaskCompletionSource<bool> completion =
            new TaskCompletionSource<bool>();

        operation.completed +=
            _ => completion.TrySetResult(true);

        await completion.Task;

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

            string key =
                error != null &&
                error.error != null &&
                error.error.details != null &&
                !string.IsNullOrWhiteSpace(
                    error.error.details.errorKey)
                    ? error.error.details.errorKey
                    : "match.error.unknown";

            string technical =
                error != null &&
                error.error != null &&
                !string.IsNullOrWhiteSpace(
                    error.error.message)
                    ? error.error.message
                    : $"HTTP {request.responseCode}: " +
                      request.error;

            return AtlasMatchNetworkResult.Fail(
                key,
                technical);
        }

        AtlasMatchNetworkResult result =
            parser(body);

        if (result.Success &&
            result.Snapshot != null)
        {
            AcceptSnapshot(
                result.Snapshot);
        }

        return result;
    }

    private static AtlasMatchNetworkResult
        ParseSnapshotEnvelope(
            string body)
    {
        SnapshotEnvelope envelope =
            SafeFromJson<
                SnapshotEnvelope>(
                    body);

        if (envelope == null ||
            envelope.result == null ||
            !envelope.result.ok ||
            envelope.result.snapshot == null)
        {
            return AtlasMatchNetworkResult.Fail(
                "match.error.invalid_response",
                "Invalid match snapshot response.");
        }

        return new AtlasMatchNetworkResult
        {
            Success = true,
            Snapshot =
                ConvertSnapshot(
                    envelope.result.snapshot)
        };
    }

    private static AtlasMatchNetworkResult
        ParseIntentResultEnvelope(
            string body)
    {
        IntentResultEnvelope envelope =
            SafeFromJson<
                IntentResultEnvelope>(
                    body);

        if (envelope == null ||
            envelope.result == null ||
            !envelope.result.ok)
        {
            return AtlasMatchNetworkResult.Fail(
                "match.error.invalid_response",
                "Invalid match intent response.");
        }

        return new AtlasMatchNetworkResult
        {
            Success = true,
            IntentId =
                envelope.result.intentId ??
                string.Empty,
            IdempotentReplay =
                envelope.result
                    .idempotentReplay
        };
    }

    private static AtlasMatchNetworkResult
        ParseIntentListEnvelope(
            string body)
    {
        IntentListEnvelope envelope =
            SafeFromJson<
                IntentListEnvelope>(
                    body);

        if (envelope == null ||
            envelope.result == null ||
            !envelope.result.ok)
        {
            return AtlasMatchNetworkResult.Fail(
                "match.error.invalid_response",
                "Invalid pending-intent response.");
        }

        WireIntent[] wires =
            envelope.result.intents ??
            Array.Empty<WireIntent>();

        AtlasMatchIntent[] intents =
            new AtlasMatchIntent[
                wires.Length];

        for (int i = 0;
             i < wires.Length;
             i++)
        {
            WireIntent wire =
                wires[i];

            intents[i] =
                new AtlasMatchIntent
                {
                    IntentId =
                        wire.intentId ??
                        string.Empty,
                    ClientCommandId =
                        wire.clientCommandId ??
                        string.Empty,
                    AccountId =
                        wire.accountId ??
                        string.Empty,
                    SeatId =
                        wire.seatId ??
                        string.Empty,
                    IntentType =
                        wire.intentType ??
                        string.Empty,
                    PayloadJson =
                        wire.payloadJson ??
                        "{}",
                    CreatedAtEpochMs =
                        wire.createdAtEpochMs
                };
        }

        return new AtlasMatchNetworkResult
        {
            Success = true,
            Intents = intents
        };
    }

    private static AtlasMatchNetworkResult
        ParsePublishEnvelope(
            string body)
    {
        PublishEnvelope envelope =
            SafeFromJson<
                PublishEnvelope>(
                    body);

        if (envelope == null ||
            envelope.result == null ||
            !envelope.result.ok ||
            envelope.result.state == null)
        {
            return AtlasMatchNetworkResult.Fail(
                "match.error.invalid_response",
                "Invalid match publish response.");
        }

        return new AtlasMatchNetworkResult
        {
            Success = true,
            Revision =
                envelope.result.state.revision
        };
    }

    private static AtlasMatchNetworkResult
        ParseAcknowledgeEnvelope(
            string body)
    {
        AcknowledgeEnvelope envelope =
            SafeFromJson<
                AcknowledgeEnvelope>(
                    body);

        if (envelope == null ||
            envelope.result == null ||
            !envelope.result.ok)
        {
            return AtlasMatchNetworkResult.Fail(
                "match.error.invalid_response",
                "Invalid match ACK response.");
        }

        return new AtlasMatchNetworkResult
        {
            Success = true,
            Acknowledged =
                envelope.result.acknowledged
        };
    }

    private static AtlasMatchNetworkSnapshot
        ConvertSnapshot(
            WireSnapshot wire)
    {
        AtlasMatchNetworkSnapshot snapshot =
            new AtlasMatchNetworkSnapshot
            {
                MatchId =
                    wire.matchId ??
                    string.Empty,
                LobbyId =
                    wire.lobbyId ??
                    string.Empty,
                Status =
                    wire.status ??
                    string.Empty,
                LocalSeatId =
                    wire.localSeatId ??
                    string.Empty,
                LocalIsHost =
                    wire.localIsHost,
                Revision =
                    wire.revision,
                Phase =
                    wire.phase ??
                    string.Empty,
                TurnSeatId =
                    wire.turnSeatId ??
                    string.Empty,
                EventSequence =
                    wire.eventSequence,
                SnapshotJson =
                    wire.snapshotJson ??
                    "{}",
                UpdatedAtEpochMs =
                    wire.updatedAtEpochMs,
                NetworkSchemaVersion =
                    wire.networkSchemaVersion
            };

        if (wire.seats != null)
        {
            foreach (WireSeat seat
                     in wire.seats)
            {
                if (seat == null)
                {
                    continue;
                }

                snapshot.Seats.Add(
                    new AtlasMatchNetworkSeat
                    {
                        SeatId =
                            seat.seatId ??
                            string.Empty,
                        SlotIndex =
                            seat.slotIndex,
                        SeatMode =
                            seat.seatMode ??
                            string.Empty,
                        DisplayName =
                            seat.displayName ??
                            string.Empty,
                        IsHost =
                            seat.isHost,
                        ControllerKind =
                            seat.controllerKind ??
                            string.Empty,
                        ConnectionState =
                            seat.connectionState ??
                            string.Empty,
                        ReconnectExpiresAtEpochMs =
                            seat.reconnectExpiresAtEpochMs,
                        AfkLockedOut =
                            seat.afkLockedOut,
                        RemovalReason =
                            seat.removalReason ??
                            string.Empty
                    });
            }
        }

        return snapshot;
    }

    private static T SafeFromJson<T>(
        string json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(
                json);
        }
        catch
        {
            return null;
        }
    }

    [Serializable]
    private sealed class MatchIdRequest
    {
        public string matchId;
    }

    [Serializable]
    private sealed class MatchSeatSlotRequest
    {
        public string matchId;
        public int slotIndex;
    }

    [Serializable]
    private sealed class SubmitIntentRequest
    {
        public string matchId;
        public string clientCommandId;
        public string intentType;
        public string payloadJson;
    }

    [Serializable]
    private sealed class PublishStateRequest
    {
        public string matchId;
        public int expectedRevision;
        public string phase;
        public string turnSeatId;
        public int eventSequence;
        public string snapshotJson;
    }

    [Serializable]
    private sealed class AcknowledgeIntentRequest
    {
        public string matchId;
        public string[] intentIds;
    }

    [Serializable]
    private sealed class CallableErrorDetails
    {
        public string errorKey;
    }

    [Serializable]
    private sealed class CallableError
    {
        public string message;
        public CallableErrorDetails details;
    }

    [Serializable]
    private sealed class CallableErrorEnvelope
    {
        public CallableError error;
    }

    [Serializable]
    private sealed class WireSeat
    {
        public string seatId;
        public int slotIndex;
        public string seatMode;
        public string displayName;
        public bool isHost;
        public string controllerKind;
        public string connectionState;
        public long reconnectExpiresAtEpochMs;
        public bool afkLockedOut;
        public string removalReason;
    }

    [Serializable]
    private sealed class WireSnapshot
    {
        public string matchId;
        public string lobbyId;
        public string status;
        public string localSeatId;
        public bool localIsHost;
        public int revision;
        public string phase;
        public string turnSeatId;
        public int eventSequence;
        public string snapshotJson;
        public long updatedAtEpochMs;
        public int networkSchemaVersion;
        public WireSeat[] seats;
    }

    [Serializable]
    private sealed class SnapshotResult
    {
        public bool ok;
        public WireSnapshot snapshot;
    }

    [Serializable]
    private sealed class SnapshotEnvelope
    {
        public SnapshotResult result;
    }

    [Serializable]
    private sealed class IntentResult
    {
        public bool ok;
        public string intentId;
        public bool idempotentReplay;
    }

    [Serializable]
    private sealed class IntentResultEnvelope
    {
        public IntentResult result;
    }

    [Serializable]
    private sealed class WireIntent
    {
        public string intentId;
        public string clientCommandId;
        public string accountId;
        public string seatId;
        public string intentType;
        public string payloadJson;
        public long createdAtEpochMs;
    }

    [Serializable]
    private sealed class IntentListResult
    {
        public bool ok;
        public WireIntent[] intents;
    }

    [Serializable]
    private sealed class IntentListEnvelope
    {
        public IntentListResult result;
    }

    [Serializable]
    private sealed class WirePublishedState
    {
        public int revision;
    }

    [Serializable]
    private sealed class PublishResult
    {
        public bool ok;
        public WirePublishedState state;
    }

    [Serializable]
    private sealed class PublishEnvelope
    {
        public PublishResult result;
    }

    [Serializable]
    private sealed class AcknowledgeResult
    {
        public bool ok;
        public int acknowledged;
    }

    [Serializable]
    private sealed class AcknowledgeEnvelope
    {
        public AcknowledgeResult result;
    }
}
