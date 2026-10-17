using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Spark RTDB Casual match bridge; preserves the Phase 12-16 public API used
/// by AtlasBoardTurnDiceNetworkCoordinator and the existing gameplay drivers.
/// Local Offline does not enter this network bridge. No Cloud Functions calls.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AtlasBoardLobbyRuntimeBridge))]
public sealed class AtlasBoardMatchRuntimeBridge : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float snapshotPollSeconds = 0.75f;
    [SerializeField] private bool autoPollAfterMatchBootstrap = true;

    private AtlasBoardLobbyRuntimeBridge lobbyBridge;
    private AtlasMatchNetworkSnapshot currentSnapshot;
    private bool locallyDetachedFromMatch = true;
    private string expectedMatchId = string.Empty;
    private bool pollInFlight;
    private float nextPollAt;
    private int sessionGeneration;
    private int criticalIntentInFlightCount;
    private float nextTokenRefreshAt;
    private string pendingCachedMatchId = string.Empty;
    private float emptyIntentCacheUntil;
    private AtlasMatchNetworkResult cachedEmptyIntents;
    private Task<AtlasMatchNetworkResult> activeRead;
    private string activeReadMatchId = string.Empty;

    public event Action<AtlasMatchNetworkSnapshot> SnapshotChanged;
    public AtlasMatchNetworkSnapshot CurrentSnapshot => currentSnapshot;
    public bool HasCriticalIntentInFlight => criticalIntentInFlightCount > 0;
    public string CurrentMatchId => lobbyBridge != null &&
        lobbyBridge.CurrentSnapshot != null
        ? lobbyBridge.CurrentSnapshot.MatchId ?? string.Empty : string.Empty;

    private void Awake() { ResolveReferences(); }
    private void ResolveReferences()
    {
        if (lobbyBridge == null)
            lobbyBridge = GetComponent<AtlasBoardLobbyRuntimeBridge>();
    }
    private string ResolveMatchId(string explicitId)
    {
        return string.IsNullOrWhiteSpace(explicitId) ? CurrentMatchId : explicitId.Trim();
    }
    private async Task<bool> IdentityAsync()
    {
        ResolveReferences();
        return lobbyBridge != null && !lobbyBridge.UsingLocalEmulators &&
            await lobbyBridge.EnsureOnlineIdentityAsync() &&
            !string.IsNullOrWhiteSpace(lobbyBridge.CurrentAccountId) &&
            !string.IsNullOrWhiteSpace(lobbyBridge.AuthTokenForOnlineSubsystems);
    }
    private async Task<AtlasMatchNetworkResult> WithIdentity(
        Func<string,string,Task<AtlasMatchNetworkResult>> operation)
    {
        if (!await IdentityAsync())
            return AtlasMatchNetworkResult.Fail("match.error.service_unavailable",
                "Firebase identity is not ready for Spark RTDB Casual gameplay.");
        // Firebase ID tokens expire; refresh the cached RTDB REST token on a
        // bounded cadence, never on each 0.75-second snapshot poll.
        if (Time.unscaledTime >= nextTokenRefreshAt)
        {
            await lobbyBridge.RebindIdentityFromFirebaseAuthAsync();
            nextTokenRefreshAt = Time.unscaledTime + 2400f;
        }
        float requestStartedAt = Time.realtimeSinceStartup;
        AtlasMatchNetworkResult result = await operation(lobbyBridge.CurrentAccountId,
            lobbyBridge.AuthTokenForOnlineSubsystems);
        if (!result.Success && result.TechnicalMessage != null &&
            (result.TechnicalMessage.IndexOf("permission_denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
             result.TechnicalMessage.IndexOf("http_401", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            if (await lobbyBridge.RebindIdentityFromFirebaseAuthAsync())
            {
                nextTokenRefreshAt = Time.unscaledTime + 2400f;
                result = await operation(lobbyBridge.CurrentAccountId,
                    lobbyBridge.AuthTokenForOnlineSubsystems);
            }
        }
        AtlasBoardNetworkTelemetry.Report(
            "matchRTDB", result.Success,
            Mathf.Max(0f, (Time.realtimeSinceStartup - requestStartedAt) * 1000f));
        return result;
    }
    private async void Update()
    {
        if (!autoPollAfterMatchBootstrap || HasCriticalIntentInFlight ||
            pollInFlight || Time.unscaledTime < nextPollAt || locallyDetachedFromMatch)
            return;
        string match = CurrentMatchId;
        if (string.IsNullOrWhiteSpace(match)) return;
        // The host already owns the authoritative turn state. A solo
        // Host+Bots session has no remote intents, so repeatedly downloading
        // the same full RTDB frame adds latency, bandwidth and HUD churn.
        // Explicit snapshot reads (bootstrap/reconnect/QA) still work.
        if (IsSoloHostMatch(match)) return;
        nextPollAt = Time.unscaledTime + Mathf.Max(0.5f, snapshotPollSeconds);
        pollInFlight = true;
        try
        {
            // GetSnapshotAsync already accepts the response for this session.
            // Do not dispatch the same snapshot twice on each REST poll.
            await GetSnapshotAsync(match);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_RTDB_MATCH_POLL failure=" + ex.GetType().Name);
        }
        finally { pollInFlight = false; }
    }

    public void ResetForMatchSession(string matchId)
    {
        sessionGeneration++;
        expectedMatchId = matchId ?? string.Empty;
        locallyDetachedFromMatch = string.IsNullOrWhiteSpace(expectedMatchId);
        currentSnapshot = null;
        nextPollAt = 0f;
        activeRead = null;
        activeReadMatchId = string.Empty;
        pendingCachedMatchId = string.Empty;
        cachedEmptyIntents = null;
        emptyIntentCacheUntil = 0f;
        // Never restore stale previous-match UI after a scene or lobby return.
    }

    public async Task<AtlasMatchNetworkResult> GetSnapshotAsync(string matchId = "")
    {
        string match = ResolveMatchId(matchId);
        if (string.IsNullOrEmpty(match))
            return AtlasMatchNetworkResult.Fail("match.error.not_found", "No active match.");
        if (activeRead != null && !activeRead.IsCompleted &&
            string.Equals(activeReadMatchId, match, StringComparison.Ordinal))
            return await activeRead;
        int generation = sessionGeneration;
        activeReadMatchId = match;
        Task<AtlasMatchNetworkResult> task = WithIdentity((uid, token) =>
            AtlasBoardSparkRtdbMatchTransport.SnapshotAsync(match, uid, token));
        activeRead = task;
        AtlasMatchNetworkResult result = await task;
        if (generation == sessionGeneration && result.Success)
            AcceptSnapshot(result.Snapshot);
        if (ReferenceEquals(activeRead, task))
        {
            activeRead = null;
            activeReadMatchId = string.Empty;
        }
        return result;
    }

    public async Task<AtlasMatchNetworkResult> RefreshSnapshotNowAsync(string matchId = "")
    {
        string match = ResolveMatchId(matchId);
        // Join the in-flight request before scheduling a single fresh read.
        Task<AtlasMatchNetworkResult> previous = activeRead;
        if (previous != null && !previous.IsCompleted) await previous;
        return await GetSnapshotAsync(match);
    }

    public Task<AtlasMatchNetworkResult> SubmitIntentAsync(
        string intentType, string payloadJson, string clientCommandId, string matchId = "")
    {
        return SubmitCoreAsync(intentType, payloadJson, clientCommandId,
            ResolveMatchId(matchId));
    }
    private async Task<AtlasMatchNetworkResult> SubmitCoreAsync(
        string type, string payload, string commandId, string match)
    {
        if (currentSnapshot == null || currentSnapshot.MatchId != match)
        {
            AtlasMatchNetworkResult fresh = await GetSnapshotAsync(match);
            if (!fresh.Success || fresh.Snapshot == null) return fresh;
        }
        int revision = currentSnapshot != null ? currentSnapshot.Revision : 0;
        return await WithIdentity((uid, token) =>
            AtlasBoardSparkRtdbMatchTransport.SubmitAsync(
                match, uid, token, commandId, type, payload, revision));
    }

    public async Task<AtlasMatchNetworkResult> SubmitCriticalIntentAsync(
        string intentType, string payloadJson, string clientCommandId, string matchId = "")
    {
        criticalIntentInFlightCount++;
        try
        {
            string match = ResolveMatchId(matchId);
            AtlasMatchNetworkResult result = await SubmitCoreAsync(
                intentType, payloadJson, clientCommandId, match);
            // Preserve idempotent command identity if a transport response is lost.
            if (!result.Success && result.TechnicalMessage != null &&
                result.TechnicalMessage.IndexOf("connection_failed", StringComparison.OrdinalIgnoreCase) >= 0)
                result = await SubmitCoreAsync(intentType, payloadJson, clientCommandId, match);
            return result;
        }
        finally { criticalIntentInFlightCount = Mathf.Max(0, criticalIntentInFlightCount - 1); }
    }

    public Task<AtlasMatchNetworkResult> LeaveActiveMatchAsync(string matchId = "")
    {
        string match = ResolveMatchId(matchId);
        ResetForMatchSession("");
        return WithIdentity((uid, token) =>
            AtlasBoardSparkRtdbMatchTransport.LeaveAsync(match, uid, token));
    }
    public Task<AtlasMatchNetworkResult> TouchPresenceAsync(string matchId = "")
    {
        string match = ResolveMatchId(matchId);
        return WithIdentity((uid, token) =>
            AtlasBoardSparkRtdbMatchTransport.TouchAsync(match, uid, token));
    }
    public async Task<AtlasMatchNetworkResult> HostSweepDisconnectedAsync(string matchId = "")
    {
        string match = ResolveMatchId(matchId);
        int generation = sessionGeneration;
        AtlasMatchNetworkResult result = await WithIdentity((uid, token) =>
            AtlasBoardSparkRtdbMatchTransport.HostMaintenanceAsync(match, uid, token));
        if (generation == sessionGeneration && result.Success)
            AcceptSnapshot(result.Snapshot);
        return result;
    }
    public async Task<AtlasMatchNetworkResult> TryRecoverCrashedHostAsync(string matchId = "")
    {
        string match = ResolveMatchId(matchId);
        int generation = sessionGeneration;
        AtlasMatchNetworkResult result = await WithIdentity((uid, token) =>
            AtlasBoardSparkRtdbMatchTransport.RecoverCrashedHostAsync(
                match, uid, token));
        if (generation == sessionGeneration && result.Success &&
            result.Snapshot != null)
            AcceptSnapshot(result.Snapshot);
        return result;
    }
    public Task<AtlasMatchNetworkResult> HostMarkAfkRemovedAsync(
        int slotIndex, string matchId = "")
    {
        string match = ResolveMatchId(matchId);
        return WithIdentity((uid, token) =>
            AtlasBoardSparkRtdbMatchTransport.HostRemoveAfkAsync(
                match, uid, token, slotIndex));
    }
    public async Task<AtlasMatchNetworkResult> HostExpireReconnectsAsync(string matchId = "")
    {
        string match = ResolveMatchId(matchId);
        int generation = sessionGeneration;
        AtlasMatchNetworkResult result = await WithIdentity((uid, token) =>
            AtlasBoardSparkRtdbMatchTransport.HostMaintenanceAsync(match, uid, token));
        if (generation == sessionGeneration && result.Success)
            AcceptSnapshot(result.Snapshot);
        return result;
    }
    public Task<AtlasMatchNetworkResult> HostPrepareRematchAsync(string matchId = "")
    {
        string match = ResolveMatchId(matchId);
        return WithIdentity((uid, token) =>
            AtlasBoardSparkRtdbMatchTransport.HostRematchAsync(match, uid, token));
    }
    private bool IsSoloHostMatch(string match)
    {
        AtlasMatchNetworkSnapshot snapshot = currentSnapshot;
        if (snapshot == null || !snapshot.LocalIsHost ||
            !string.Equals(snapshot.MatchId, match, StringComparison.Ordinal) ||
            snapshot.Seats == null || snapshot.Seats.Count == 0)
            return false;
        foreach (AtlasMatchNetworkSeat seat in snapshot.Seats)
        {
            if (seat == null) continue;
            // Only a human controlled by another account can issue a remote
            // intent. Local human seats and every bot are host-owned gameplay.
            // Temporary bots can still become Human within the five-minute
            // reconnect window; keep their remote intent polling active.
            // An expired/AFK-removed permanent bot cannot send Human intents.
            if (string.Equals(seat.SeatMode, "remotehuman",
                    StringComparison.OrdinalIgnoreCase) &&
                !seat.AfkLockedOut &&
                !string.Equals(seat.ControllerKind,"permanent_bot",
                    StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    public async Task<AtlasMatchNetworkResult> HostListPendingIntentsAsync(string matchId = "")
    {
        string match = ResolveMatchId(matchId);
        if (IsSoloHostMatch(match))
            return new AtlasMatchNetworkResult { Success = true,
                Intents = Array.Empty<AtlasMatchIntent>() };
        if (match == pendingCachedMatchId && cachedEmptyIntents != null &&
            Time.unscaledTime < emptyIntentCacheUntil)
            return cachedEmptyIntents;
        string verifiedRoom = currentSnapshot != null &&
            currentSnapshot.LocalIsHost && currentSnapshot.MatchId == match
            ? currentSnapshot.LobbyId : string.Empty;
        AtlasMatchNetworkResult result = await WithIdentity((uid, token) =>
            AtlasBoardSparkRtdbMatchTransport.PendingAsync(match, uid, token, verifiedRoom));
        pendingCachedMatchId = match;
        if (result.Success && result.Intents != null && result.Intents.Length == 0)
        {
            // Legacy coordinator polls very frequently. One empty RTDB read
            // covers a 450ms window; a real new intent is delayed at most 0.45s.
            cachedEmptyIntents = result;
            emptyIntentCacheUntil = Time.unscaledTime + 0.45f;
        }
        else
        {
            cachedEmptyIntents = null;
            emptyIntentCacheUntil = 0f;
        }
        return result;
    }
    public Task<AtlasMatchNetworkResult> HostPublishStateAsync(
        int expectedRevision, string phase, string turnSeatId,
        int eventSequence, string snapshotJson, string matchId = "")
    {
        string match = ResolveMatchId(matchId);
        return WithIdentity((uid, token) =>
            AtlasBoardSparkRtdbMatchTransport.PublishAsync(
                match, uid, token, expectedRevision, phase,
                turnSeatId, eventSequence, snapshotJson));
    }
    public Task<AtlasMatchNetworkResult> HostAcknowledgeIntentsAsync(
        IEnumerable<string> intentIds, string matchId = "")
    {
        string match = ResolveMatchId(matchId);
        return WithIdentity((uid, token) =>
            AtlasBoardSparkRtdbMatchTransport.AcknowledgeAsync(
                match, uid, token, intentIds));
    }

    private void AcceptSnapshot(AtlasMatchNetworkSnapshot next)
    {
        if (next == null || locallyDetachedFromMatch) return;
        string required = string.IsNullOrEmpty(expectedMatchId)
            ? CurrentMatchId : expectedMatchId;
        if (string.IsNullOrEmpty(required) || next.MatchId != required) return;
        AtlasMatchNetworkSnapshot old = currentSnapshot;
        // Remote responses may arrive out of order. An older frame must not
        // roll back a restored Human seat, the turn, or a newer HUD state.
        if (old != null && old.MatchId == next.MatchId &&
            (next.Revision < old.Revision ||
             (next.Revision == old.Revision &&
              next.EventSequence < old.EventSequence)))
            return;
        if (old != null && old.MatchId == next.MatchId &&
            old.Revision == next.Revision && old.EventSequence == next.EventSequence &&
            old.Phase == next.Phase && old.SnapshotJson == next.SnapshotJson &&
            old.AuthorityEpoch == next.AuthorityEpoch)
        {
            old.HostHeartbeatAtEpochMs = next.HostHeartbeatAtEpochMs;
            old.UpdatedAtEpochMs = next.UpdatedAtEpochMs;
            return;
        }
        currentSnapshot = next;
        SnapshotChanged?.Invoke(next);
    }
}
