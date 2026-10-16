#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Non-gameplay RTDB integration smoke. Creates a throwaway private room,
/// starts a match, publishes and reads a dummy frame, returns to lobby,
/// and closes the room. No scene matchmaking, roll, XP or rewards.
/// </summary>
public static class AtlasBoardSparkMatchSmokeQA
{
    [MenuItem("Atlas Board/QA & Diagnostics/RTDB/Verify Bot Seat Wire Contract (No Network)")]
    private static void VerifyBotSeatWireContract()
    {
        bool correct = AtlasBoardSparkRtdbMatchTransport.ValidateSeatWireContract();
        if (correct)
            Debug.Log("ATB_SPARK_BOT_WIRE_QA PASS mode=local_no_network legacy_permanentbot=normalized bot_mode=permanent_bot host_mode=human");
        else
            Debug.LogError("ATB_SPARK_BOT_WIRE_QA FAIL reason=bot_seat_controller_contract");
    }

    [MenuItem("Atlas Board/QA & Diagnostics/RTDB/Measure Live Match Read Latency (Play Mode)")]
    private static async void MeasureLiveReadLatency()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("ATB_RTDB_LATENCY FAIL reason=enter_Play_Mode_first");
            return;
        }
        AtlasBoardLobbyRuntimeBridge lobby = UnityEngine.Object.FindAnyObjectByType<
            AtlasBoardLobbyRuntimeBridge>(FindObjectsInactive.Include);
        AtlasBoardMatchRuntimeBridge match = UnityEngine.Object.FindAnyObjectByType<
            AtlasBoardMatchRuntimeBridge>(FindObjectsInactive.Include);
        if (lobby == null || match == null || lobby.UsingLocalEmulators ||
            string.IsNullOrWhiteSpace(match.CurrentMatchId))
        {
            Debug.LogError("ATB_RTDB_LATENCY FAIL reason=start_a_real_Live_Spark_online_match_first");
            return;
        }
        // Read-only live probe: no room creation, no dummy frame, no gameplay
        // state mutation. Do not confuse this with the three-rematch smoke test.
        string matchId = match.CurrentMatchId;
        System.Collections.Generic.List<long> latencyMs =
            new System.Collections.Generic.List<long>();
        int failures = 0;
        for (int i = 0; i < 8; i++)
        {
            if (!string.Equals(match.CurrentMatchId, matchId, StringComparison.Ordinal))
            {
                Debug.LogWarning("ATB_RTDB_LATENCY INTERRUPTED reason=match_changed");
                return;
            }
            System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
            AtlasMatchNetworkResult result = await match.GetSnapshotAsync(matchId);
            clock.Stop();
            if (result.Success && result.Snapshot != null)
                latencyMs.Add(clock.ElapsedMilliseconds);
            else
                failures++;
            await System.Threading.Tasks.Task.Delay(120);
        }
        if (latencyMs.Count == 0)
        {
            Debug.LogError("ATB_RTDB_LATENCY FAIL reason=all_reads_failed samples=8");
            return;
        }
        latencyMs.Sort();
        long median = latencyMs[latencyMs.Count / 2];
        long p95 = latencyMs[Mathf.Min(latencyMs.Count - 1,
            Mathf.CeilToInt(latencyMs.Count * 0.95f) - 1)];
        Debug.Log("ATB_RTDB_LATENCY RESULT transport=live_spark_rtdb" +
            " mode=read_only real_match=true samples=" + latencyMs.Count +
            " failed=" + failures + " median_ms=" + median +
            " p95_ms=" + p95 + " max_ms=" + latencyMs[latencyMs.Count - 1] +
            " metric=snapshot_roundtrip not_dice_decision_elapsed");
    }

    [MenuItem("Atlas Board/QA & Diagnostics/RTDB/Smoke Test Casual Match Roundtrip (Play Mode)")]
    private static async void Run()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("ATB_SPARK_MATCH_QA FAIL stage=editor_mode reason=enter_Play_Mode_first");
            return;
        }
        AtlasBoardLobbyRuntimeBridge lobby = UnityEngine.Object.FindAnyObjectByType<
            AtlasBoardLobbyRuntimeBridge>(FindObjectsInactive.Include);
        AtlasBoardMatchRuntimeBridge match = UnityEngine.Object.FindAnyObjectByType<
            AtlasBoardMatchRuntimeBridge>(FindObjectsInactive.Include);
        if (lobby == null || match == null || lobby.HasLobby || lobby.UsingLocalEmulators)
        {
            Debug.LogError("ATB_SPARK_MATCH_QA FAIL stage=precondition reason=requires_idle_live_spark_lobby_and_match_bridges");
            return;
        }
        bool created = false;
        bool passed = false;
        string stage = "create";
        try
        {
            AtlasLobbyOperationResult room = await lobby.CreatePrivateRoomAsync(
                new AtlasBoardLobbySelection {
                    Mode = "PRIVATE TABLE",
                    MapId = "Turkey",
                    PlayerCount = 2,
                    RoundLimit = 20,
                    ThemeId = "classic_table",
                    BalancedDevelopment = true,
                    DoublesEnabled = true,
                    TripleDoublePenaltyEnabled = true
                });
            if (!room.Success || room.Snapshot == null)
                throw new InvalidOperationException("create: " + room.TechnicalMessage);
            created = true;
            for (int round = 1; round <= 3; round++)
            {
                stage = "start_" + round;
                AtlasLobbyOperationResult started = await lobby.StartMatchAsync();
                if (!started.Success || !started.Started || started.Snapshot == null)
                    throw new InvalidOperationException("start: " + started.TechnicalMessage);
                string id = started.Snapshot.MatchId;
                if (string.IsNullOrEmpty(id)) throw new InvalidOperationException("Start returned no MatchId");
                if (started.Snapshot.Settings.BotCount != 1 ||
                    started.Snapshot.Settings.LocalHumanCount != 1)
                    throw new InvalidOperationException("Open seat was not finalized as a Bot");
                match.ResetForMatchSession(id);
                stage = "read_" + round;
                AtlasMatchNetworkResult first = await match.GetSnapshotAsync(id);
                if (!first.Success || first.Snapshot == null || !first.Snapshot.LocalIsHost)
                    throw new InvalidOperationException("initial match snapshot unavailable");
                // BotCount in the LOBBY is not enough: the ACTUAL match
                // snapshot must use the canonical bot controller wire value.
                // Otherwise the bot does not roll until the 10s AFK fallback.
                stage = "verify_bot_seat_" + round;
                AtlasMatchNetworkSeat botSeat = null;
                AtlasMatchNetworkSeat hostSeat = null;
                if (first.Snapshot.Seats != null)
                {
                    foreach (AtlasMatchNetworkSeat seat in first.Snapshot.Seats)
                    {
                        if (seat == null) continue;
                        if (seat.SlotIndex == 1) botSeat = seat;
                        if (seat.SlotIndex == 0) hostSeat = seat;
                    }
                }
                if (botSeat == null ||
                    !string.Equals(botSeat.SeatMode, "bot", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(botSeat.ControllerKind, "permanent_bot", StringComparison.OrdinalIgnoreCase) ||
                    hostSeat == null ||
                    !string.Equals(hostSeat.ControllerKind, "human", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "match bot wire mismatch: botMode=" +
                        (botSeat != null ? botSeat.SeatMode : "missing") +
                        " botController=" +
                        (botSeat != null ? botSeat.ControllerKind : "missing") +
                        " hostController=" +
                        (hostSeat != null ? hostSeat.ControllerKind : "missing"));
                Debug.Log("ATB_SPARK_BOT_SEAT PASS round=" + round +
                    " bot_wire=permanent_bot host_wire=human");
                stage = "publish_" + round;
                AtlasMatchNetworkResult published = await match.HostPublishStateAsync(
                    first.Snapshot.Revision, "awaiting_roll", "seat_0", 1,
                    "{\"schemaVersion\":1,\"qaOnly\":true}", id);
                if (!published.Success || published.Revision <= first.Snapshot.Revision)
                    throw new InvalidOperationException("host revision publish failed: " + published.TechnicalMessage);
                stage = "verify_" + round;
                AtlasMatchNetworkResult verified = await match.GetSnapshotAsync(id);
                if (!verified.Success || verified.Snapshot == null ||
                    verified.Snapshot.Revision != published.Revision ||
                    verified.Snapshot.Phase != "awaiting_roll")
                    throw new InvalidOperationException("published frame not readable");
                stage = "rematch_" + round;
                AtlasMatchNetworkResult rematch = await match.HostPrepareRematchAsync(id);
                if (!rematch.Success)
                    throw new InvalidOperationException("rematch reset failed: " + rematch.TechnicalMessage);
                match.ResetForMatchSession("");
                AtlasLobbyOperationResult refreshed = await lobby.GetSnapshotAsync();
                if (!refreshed.Success || refreshed.Snapshot == null ||
                    refreshed.Snapshot.LifecycleState != AtlasRoomLifecycleState.Waiting)
                    throw new InvalidOperationException("lobby did not return to Waiting");
                if (refreshed.Snapshot.TransportRevision <=
                    started.Snapshot.TransportRevision)
                    throw new InvalidOperationException(
                        "rematch did not advance roomState transport revision: " +
                        "started=" + started.Snapshot.TransportRevision +
                        " waiting=" + refreshed.Snapshot.TransportRevision);
                if (lobby.CurrentSnapshot == null ||
                    lobby.CurrentSnapshot.LifecycleState != AtlasRoomLifecycleState.Waiting ||
                    !string.IsNullOrEmpty(lobby.CurrentSnapshot.MatchId) ||
                    lobby.CurrentSnapshot.TransportRevision !=
                        refreshed.Snapshot.TransportRevision ||
                    lobby.CurrentSnapshot.SettingsRevision !=
                        refreshed.Snapshot.SettingsRevision)
                    throw new InvalidOperationException(
                        "rematch lobby cache mismatch: " +
                        "remoteTransport=" + refreshed.Snapshot.TransportRevision +
                        " cachedTransport=" +
                            (lobby.CurrentSnapshot != null
                                ? lobby.CurrentSnapshot.TransportRevision : -1L) +
                        " remoteSettings=" + refreshed.Snapshot.SettingsRevision +
                        " cachedSettings=" +
                            (lobby.CurrentSnapshot != null
                                ? lobby.CurrentSnapshot.SettingsRevision : -1) +
                        " cachedLifecycle=" +
                            (lobby.CurrentSnapshot != null
                                ? lobby.CurrentSnapshot.LifecycleState.ToString()
                                : "null") +
                        " cachedMatchIdPresent=" +
                            (lobby.CurrentSnapshot != null &&
                                !string.IsNullOrEmpty(lobby.CurrentSnapshot.MatchId)));
                // Observe the authoritative revision for the NEXT start,
                // not just the old match's snapshot. The bridge now updates
                // its local cache as part of this read.
                Debug.Log("ATB_SPARK_MATCH_QA rematch_ready=" + round +
                    " settingsRevision=" +
                    refreshed.Snapshot.SettingsRevision +
                    " transportRevision=" +
                    refreshed.Snapshot.TransportRevision +
                    " next_start=" + (round + 1));
            }
            stage = "close";
            AtlasLobbyOperationResult closed = await lobby.CloseLobbyAsync();
            if (!closed.Success) throw new InvalidOperationException("close: " + closed.TechnicalMessage);
            created = false;
            passed = true;
            Debug.Log("ATB_SPARK_MATCH_QA PASS stages=create_3x_start_read_publish_rematch_close bots=PASS " +
                "remote_join=UNTESTED remote_gameplay=UNTESTED rewards=OFF");
        }
        catch (Exception error)
        {
            Debug.LogError("ATB_SPARK_MATCH_QA FAIL stage=" + stage +
                " cause=" + error.GetType().Name + " detail=" + error.Message);
        }
        finally
        {
            match.ResetForMatchSession("");
            if (created && lobby.HasLobby)
            {
                AtlasLobbyOperationResult cleanup = await lobby.CloseLobbyAsync();
                if (!cleanup.Success)
                    Debug.LogWarning("ATB_SPARK_MATCH_QA cleanup_pending=true reason=" + cleanup.TechnicalMessage);
            }
            if (!passed)
                Debug.LogWarning("ATB_SPARK_MATCH_QA done=false do_not_build_APK_yet");
        }
    }
}
#endif
