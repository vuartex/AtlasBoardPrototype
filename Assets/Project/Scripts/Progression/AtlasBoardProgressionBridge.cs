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
        bool liveGuest = AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession;

        // A Guest is still an authenticated Firebase UID. Local observation is
        // only an immediate presentation fallback; durable Career recovery, XP,
        // achievements, history and match Gold use the same RTDB UID pipeline
        // as a registered player. Do not short-circuit the server read merely
        // because the provider is anonymous/REST-backed.
        string fallbackDisplayName = liveGuest
            ? AtlasBoardGuestIdentity.DisplayName
            : string.Empty;
        if (!liveGuest)
        {
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


        // An earlier account's asynchronous read must never be rendered after
        // a sign-out/account switch, even if its token was valid at request time.
        if (uid != lobbyBridge.CurrentAccountId ||
            AtlasBoardRtdbMetaEconomy.CurrentUid != uid)
            return FailedSnapshot();
        MergeLocalObservedMatches(snapshot, uid, liveGuest);
        lastErrorKey = string.Empty;
        lastTechnicalMessage = string.Empty;


        ProfileChanged?.Invoke(snapshot);
        return snapshot;
    }


    private void MergeLocalObservedMatches(AtlasBoardProgressionSnapshot verified,
        string uid, bool isGuest)
    {
        if (verified == null || !verified.Success ||
            string.IsNullOrWhiteSpace(uid) ||
            uid != lobbyBridge.CurrentAccountId) return;
        AtlasBoardProgressionSnapshot observed =
            ReadLocalGuestProfile(uid, isGuest);
        bool anyUnverified = false;
        foreach (AtlasBoardProgressionMatchSummary match in observed.RecentMatches)
        {
            if (match == null || verified.RecentMatches.Exists(m =>
                m != null && m.matchId == match.matchId)) continue;
            verified.RecentMatches.Add(match);
            anyUnverified = true;
        }
        verified.RecentMatches.Sort((a, b) =>
            (b != null ? b.finishedAtEpochMs : 0).CompareTo(
                a != null ? a.finishedAtEpochMs : 0));
        if (verified.RecentMatches.Count > 10)
            verified.RecentMatches.RemoveRange(10, verified.RecentMatches.Count - 10);
        if (!anyUnverified) return;
        // If the authoritative projection is still empty, present this UID's
        // bounded local observation consistently across History/Overview/XP.
        // It is provisional only: no metaEconomy Gold or Steam unlock is minted.
        bool serverHasVerifiedProgress = verified.TotalXp > 0 ||
            (verified.Stats != null && verified.Stats.matchesPlayed > 0);
        if (!serverHasVerifiedProgress)
        {
            verified.TotalXp = observed.TotalXp;
            verified.Level = observed.Level;
            verified.LevelStartXp = observed.LevelStartXp;
            verified.NextLevelXp = observed.NextLevelXp;
            verified.Stats = observed.Stats;
            verified.MapStats.Clear();
            verified.MapStats.AddRange(observed.MapStats);
            verified.Achievements.Clear();
            verified.Achievements.AddRange(observed.Achievements);
        }
        else if (verified.Achievements.Count == 0)
        {
            verified.Achievements.AddRange(observed.Achievements);
        }
        verified.TechnicalMessage = serverHasVerifiedProgress
            ? "unverified_local_match_history_plus_verified_rewards"
            : isGuest
                ? "local_guest_provisional_progress_server_recovery_pending"
                : "local_steam_provisional_progress_no_spendable_gold";
        Debug.Log((isGuest ? "ATB_GUEST_CAREER" : "ATB_STEAM_CAREER") +
            " local_history_visible=true provisional_xp=" +
            (!serverHasVerifiedProgress) +
            " account_uid_scoped=true server_recovery_enabled=true");
    }


    // No-Steam QA Guest: private, device-local, UID-scoped presentation only.
    // This is intentionally NOT a verified claim, Gold reward or Steam unlock.
    [Serializable]
    private sealed class GuestCareerLocalData
    {
        public AtlasBoardCareerStats stats = new AtlasBoardCareerStats();
        public int localXp;
        public List<AtlasBoardProgressionMatchSummary> history =
            new List<AtlasBoardProgressionMatchSummary>();
    }


    private static string GuestCareerKey(string uid) =>
        "ATB.GuestCareer.Local.v1." + uid;


    private static string ObservedSteamCareerKey(string uid) =>
        "ATB.SteamCareer.Observed.v1." + uid;


    public static void RecordLocalGuestMatch(string uid,
        AtlasBoardProgressionMatchSummary match)
    {
        RecordLocallyObservedMatch(uid, match, true);
    }


    public static void RecordLocalSteamObservation(string uid,
        AtlasBoardProgressionMatchSummary match)
    {
        RecordLocallyObservedMatch(uid, match, false);
    }


    private static void RecordLocallyObservedMatch(string uid,
        AtlasBoardProgressionMatchSummary match, bool isGuest)
    {
        if (isGuest != AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession ||
            string.IsNullOrWhiteSpace(uid) || match == null ||
            string.IsNullOrWhiteSpace(match.matchId)) return;
        var data = LoadLocalGuestCareer(uid, isGuest);
        if (data.history.Exists(m => m != null && m.matchId == match.matchId))
            return; // Same matchId may never double-count on restart/rematch.
        // Device-local provisional XP for both No-Steam Guest and Steam
        // observations. This makes the Career UI stable while a verified RTDB
        // projection is absent, but it never credits metaEconomy Gold or a
        // Steam achievement. A later verified matchId supersedes presentation
        // rather than being double-counted.
        match.xpAwarded = AtlasBoardRtdbCareerJournal.XpAward(
            match.result, Mathf.Clamp(match.completedRounds, 0, 100),
            Mathf.Clamp(match.propertyCount, 0, 100),
            Mathf.Clamp(match.developmentLevels, 0, 100));
        data.localXp = (int)Math.Min(int.MaxValue,
            (long)Math.Max(0, data.localXp) + match.xpAwarded);
        int guestLevel, levelStart, nextLevel;
        GuestLevelBoundaries(data.localXp, out guestLevel, out levelStart, out nextLevel);
        match.levelAfter = guestLevel;
        match.mode = isGuest ? "local_guest_unverified" :
            "local_steam_unverified";
        var st = data.stats;
        st.matchesPlayed++;
        if (match.result == "win")
        {
            st.wins++; st.currentWinStreak++;
            st.bestWinStreak = Mathf.Max(st.bestWinStreak, st.currentWinStreak);
        }
        else
        {
            st.currentWinStreak = 0;
            if (match.result == "tie") st.ties++;
            else st.losses++;
        }
        if (match.bankrupt) st.bankruptcies++;
        st.diceRolls += Mathf.Max(0, match.diceRolls);
        st.doublesRolled += Mathf.Max(0, match.doublesRolled);
        st.totalDiceValue += Mathf.Max(0, match.totalDiceValue);
        st.highestRoll = Mathf.Max(st.highestRoll, match.highestRoll);
        st.propertiesAtFinishTotal += Mathf.Max(0, match.propertyCount);
        st.developmentLevelsAtFinishTotal += Mathf.Max(0, match.developmentLevels);
        st.totalRounds += Mathf.Max(0, match.completedRounds);
        st.totalTurns += Mathf.Max(0, match.completedTurns);
        st.highestCash = Mathf.Max(st.highestCash, match.finalCash);
        st.highestNetWorth = Mathf.Max(st.highestNetWorth, match.netWorth);
        st.totalNetWorth = (int)Math.Min(int.MaxValue,
            (long)st.totalNetWorth + Math.Max(0, match.netWorth));
        string map = (match.mapId ?? "").ToLowerInvariant();
        if (map.Contains("turkey")) { st.mapTurkey++; if (match.result == "win") st.mapWinsTurkey++; }
        else if (map.Contains("colorado")) { st.mapColorado++; if (match.result == "win") st.mapWinsColorado++; }
        else if (map.Contains("usa")) { st.mapUsa++; if (match.result == "win") st.mapWinsUsa++; }
        else { st.mapOther++; if (match.result == "win") st.mapWinsOther++; }
        data.history.Insert(0, match);
        // History is deliberately bounded; aggregate stats are retained.
        long cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 72L * 3600000;
        data.history.RemoveAll(m => m == null || m.finishedAtEpochMs < cutoff);
        if (data.history.Count > 10) data.history.RemoveRange(10, data.history.Count - 10);
        PlayerPrefs.SetString(isGuest ? GuestCareerKey(uid) :
            ObservedSteamCareerKey(uid), JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Debug.Log("ATB_LOCAL_MATCH_OBSERVED saved=true verified=false account=" +
            (isGuest ? "guest" : "steam") + " matches=" + data.stats.matchesPlayed);
    }


    private static int GuestLevel(int totalXp)
    {
        int level, start, next;
        GuestLevelBoundaries(totalXp, out level, out start, out next);
        return level;
    }


    private static void GuestLevelBoundaries(int totalXp, out int level,
        out int threshold, out int next)
    {
        level = 1;
        threshold = 0;
        while (level < 100)
        {
            int needed = Mathf.RoundToInt(500f * Mathf.Pow(1.1f, level - 1));
            if ((long)totalXp < (long)threshold + needed) break;
            threshold = (int)Math.Min(int.MaxValue, (long)threshold + needed);
            level++;
        }
        next = level >= 100 ? threshold :
            (int)Math.Min(int.MaxValue,
                (long)threshold + Mathf.RoundToInt(500f * Mathf.Pow(1.1f, level - 1)));
    }


    private static GuestCareerLocalData LoadLocalGuestCareer(string uid,
        bool isGuest = true)
    {
        GuestCareerLocalData data = null;
        try
        {
            string json = PlayerPrefs.GetString(isGuest ? GuestCareerKey(uid) :
                ObservedSteamCareerKey(uid), "");
            if (!string.IsNullOrEmpty(json))
                data = JsonUtility.FromJson<GuestCareerLocalData>(json);
        }
        catch (Exception) { }
        if (data == null) data = new GuestCareerLocalData();
        if (data.stats == null) data.stats = new AtlasBoardCareerStats();
        if (data.history == null) data.history = new List<AtlasBoardProgressionMatchSummary>();
        return data;
    }


    private AtlasBoardProgressionSnapshot ReadLocalGuestProfile(string uid,
        bool isGuest = true)
    {
        GuestCareerLocalData data = LoadLocalGuestCareer(uid, isGuest);
        long cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 72L * 3600000;
        data.history.RemoveAll(m => m == null || m.finishedAtEpochMs < cutoff);
        if (data.history.Count > 10) data.history.RemoveRange(10, data.history.Count - 10);
        string name = "Guest";
        if (lobbyBridge != null && lobbyBridge.CurrentSnapshot != null &&
            lobbyBridge.CurrentSnapshot.Members != null)
        {
            AtlasLobbyMemberSnapshot seat = lobbyBridge.CurrentSnapshot.Members.Find(m =>
                m != null && string.Equals(m.AccountId, uid, StringComparison.Ordinal));
            if (seat != null && !string.IsNullOrWhiteSpace(seat.DisplayName))
                name = seat.DisplayName;
        }
        var snap = new AtlasBoardProgressionSnapshot
        {
            Success = true, AccountId = uid, DisplayName = name,
            Stats = data.stats, TotalXp = Math.Max(0, data.localXp),
            Level = GuestLevel(data.localXp), MaxLevel = 100,
            HistoryLimit = 10, HistoryRetentionHours = 72,
            TechnicalMessage = isGuest ? "local_guest_unverified_xp_no_spendable_gold" :
                "local_steam_provisional_progress_no_spendable_gold"
        };
        int localLevel, levelStartXp, nextLevelXp;
        GuestLevelBoundaries(data.localXp, out localLevel, out levelStartXp, out nextLevelXp);
        snap.Level = localLevel;
        snap.LevelStartXp = levelStartXp;
        snap.NextLevelXp = nextLevelXp;
        snap.RecentMatches.AddRange(data.history);
        // Local guest map filter must reflect saved matches, not just the
        // all-maps totals. Premium maps stay visible but still unowned.
        foreach (AtlasBoardProgressionMatchSummary match in data.history)
        {
            if (match == null || string.IsNullOrWhiteSpace(match.mapId)) continue;
            string canonical = (match.mapId ?? "").Trim().ToLowerInvariant();
            if (canonical.StartsWith("map_", StringComparison.Ordinal))
                canonical = canonical.Substring(4);
            AtlasBoardMapCareerStats map = snap.MapStats.Find(m => m != null &&
                string.Equals((m.mapId ?? "").ToLowerInvariant(), canonical,
                    StringComparison.Ordinal));
            if (map == null)
            {
                map = new AtlasBoardMapCareerStats { mapId = canonical };
                snap.MapStats.Add(map);
            }
            map.stats.matchesPlayed++;
            if (match.result == "win") map.stats.wins++;
            else if (match.result == "tie") map.stats.ties++;
            else map.stats.losses++;
            map.stats.diceRolls += match.diceRolls;
            map.stats.doublesRolled += match.doublesRolled;
            map.stats.totalRounds += match.completedRounds;
            map.stats.propertiesAtFinishTotal += match.propertyCount;
            map.stats.developmentLevelsAtFinishTotal += match.developmentLevels;
            map.stats.highestNetWorth = Mathf.Max(map.stats.highestNetWorth, match.netWorth);
        }
        AtlasBoardCareerStats st = data.stats;
        // Present useful local-only progress. Never call these Steam-verified
        // unlocks, and never write them to RTDB or metaEconomy.
        var provisional = new List<AtlasBoardProgressionAchievement>();
        AddGuestAchievement(provisional, "first_match", st.matchesPlayed);
        AddGuestAchievement(provisional, "first_win", st.wins);
        AddGuestAchievement(provisional, "win_streak_3", st.bestWinStreak);
        AddGuestAchievement(provisional, "matches_10", st.matchesPlayed);
        AddGuestAchievement(provisional, "wins_10", st.wins);
        AddGuestAchievement(provisional, "dice_100", st.diceRolls);
        AddGuestAchievement(provisional, "doubles_10", st.doublesRolled);
        AddGuestAchievement(provisional, "properties_25", st.propertiesAtFinishTotal);
        AddGuestAchievement(provisional, "builder_20", st.developmentLevelsAtFinishTotal);
        AddGuestAchievement(provisional, "networth_5000", st.highestNetWorth);
        AddGuestAchievement(provisional, "turkey_explorer", st.mapTurkey);
        AddGuestAchievement(provisional, "colorado_explorer", st.mapColorado);
        AddGuestAchievement(provisional, "usa_explorer", st.mapUsa);
        AddGuestAchievement(provisional, "atlas_traveler",
            (st.mapTurkey > 0 ? 1 : 0) + (st.mapColorado > 0 ? 1 : 0) +
            (st.mapUsa > 0 ? 1 : 0));
        snap.Achievements.AddRange(
            AtlasBoardCareerAchievementDisplay.MergeVerified(provisional));
        return snap;
    }


    private static void AddGuestAchievement(
        List<AtlasBoardProgressionAchievement> items, string id, int progress)
    {
        items.Add(new AtlasBoardProgressionAchievement
        {
            achievementId = id, progress = Mathf.Max(0, progress), unlocked = false
        });
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


        // Provider type is not reward authority. Registered, Firebase Anonymous
        // and the REST-backed Development Guest all use an exact authenticated
        // UID; RTDB participation/archive/attestation/claim rules decide whether
        // that UID earned persistent progression.

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