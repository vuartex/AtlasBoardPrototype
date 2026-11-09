using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;


/// <summary>
/// RTDB-only, owner-UID-bound peer-attested Career journal. This is not an
/// independent authoritative match replay. Never award Gold or ranked rewards
/// from a peer-attested result; the final host frame can still be manipulated.
/// </summary>
public static class AtlasBoardRtdbCareerJournal
{
    private const string Database = "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    private const int MaxLevel = 100;


    public sealed class RecordStatus
    {
        public bool Applied;
        public string Reason;
    }


    private static async Task<string> RequestAsync(string path, string token, string body = null)
    {
        string url = Database + path + ".json?auth=" + UnityWebRequest.EscapeURL(token);
        using (UnityWebRequest request = body == null ? UnityWebRequest.Get(url) :
            new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT))
        {
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 12;
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);
            var completion = new TaskCompletionSource<bool>();
            var operation = request.SendWebRequest();
            operation.completed += _ => completion.TrySetResult(true);
            if (operation.isDone) completion.TrySetResult(true);
            await completion.Task;
            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException("RTDB_HTTP_" + request.responseCode);
            return request.downloadHandler.text ?? "null";
        }
    }


    private static JObject ObjectOrNull(string json)
    {
        try { return JToken.Parse(json) as JObject; }
        catch { return null; }
    }


    private static string Hash(string source)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(source)))
                .Replace("-", "").ToLowerInvariant();
    }


    // Final-frame observation is persisted BEFORE any network await. If the
    // player presses Leave/Rematch or closes the process immediately after the
    // result appears, the next Career read can resume the exact attestation.
    // No Firebase token, password or other credential is stored here.
    private static string PendingObservationKey(string uid, string matchId) =>
        "ATB.Career.PendingFinalObservation.v1." + (uid ?? string.Empty) +
        "." + (matchId ?? string.Empty);

    private static void PersistPendingObservation(
        string uid, string matchId, string digest, int revision)
    {
        if (string.IsNullOrWhiteSpace(uid) ||
            string.IsNullOrWhiteSpace(matchId) ||
            string.IsNullOrWhiteSpace(digest) || revision < 0) return;
        PlayerPrefs.SetString(
            PendingObservationKey(uid, matchId),
            revision + "|" + digest);
        PlayerPrefs.Save();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("ATB_CAREER_FINAL_OBSERVATION persisted=true uid=" +
            uid.Substring(0, Mathf.Min(7, uid.Length)) +
            " revision=" + revision + " match=" + matchId);
#endif
    }

    private static bool TryReadPendingObservation(
        string uid, string matchId, out string digest, out int revision)
    {
        digest = string.Empty;
        revision = -1;
        string raw = PlayerPrefs.GetString(
            PendingObservationKey(uid, matchId), string.Empty);
        if (string.IsNullOrWhiteSpace(raw)) return false;
        int split = raw.IndexOf('|');
        if (split <= 0 || split >= raw.Length - 1 ||
            !int.TryParse(raw.Substring(0, split), out revision))
            return false;
        digest = raw.Substring(split + 1);
        return digest.Length == 64 && revision >= 0;
    }

    private static void ClearPendingObservation(string uid, string matchId)
    {
        PlayerPrefs.DeleteKey(PendingObservationKey(uid, matchId));
        PlayerPrefs.Save();
    }


    private static int Int(JToken token, string field, int max)
    {
        return Mathf.Clamp((int?)token?[field] ?? 0, 0, max);
    }


    private static string String(JToken token, string field)
    {
        return (string)token?[field] ?? string.Empty;
    }


    private static JToken IndexedToken(JToken token, string field, int slot)
    {
        if (token == null || slot < 0) return null;
        JToken collection = token[field];
        if (collection is JArray array)
            return slot < array.Count ? array[slot] : null;
        if (collection is JObject map)
            return map[slot.ToString()];
        return null;
    }


    private static JObject IndexedObject(JToken token, string field, int slot)
    {
        return IndexedToken(token, field, slot) as JObject;
    }


    private static int Indexed(JToken token, string field, int slot, int max)
    {
        return Mathf.Clamp((int?)IndexedToken(token, field, slot) ?? 0, 0, max);
    }


    private static bool IndexedBool(JToken token, string field, int slot)
    {
        return (bool?)IndexedToken(token, field, slot) ?? false;
    }


    public static int XpAward(string result, int rounds, int properties, int developments)
    {
        return 25 + (result == "win" ? 50 : result == "tie" ? 20 : 0) +
            Mathf.Min(100, Mathf.Clamp(rounds, 0, 100) * 3) +
            Mathf.Min(50, Mathf.Clamp(properties, 0, 100) * 5) +
            Mathf.Min(75, Mathf.Clamp(developments, 0, 100) * 3);
    }


    /// <summary>
    /// Owner-approved match Gold schedule: one Gold per completed round plus
    /// a result bonus of 25/15/10 for win/tie/loss. The combined award is
    /// hard-capped at 50 Gold per player per match. Property/development
    /// performance remains XP-only.
    /// </summary>
    public static int GoldAward(string result, int rounds,
        int properties, int developments)
    {
        if (result != "win" && result != "tie" && result != "loss")
            return 0;

        int roundBonus = Mathf.Max(0, rounds);
        int outcome = result == "win" ? 25 : result == "tie" ? 15 : 10;
        return Mathf.Min(50, roundBonus + outcome);
    }

    private static string Safe(string text) => UnityWebRequest.EscapeURL(text ?? string.Empty);


    private sealed class CareerIdentity
    {
        public string Uid;
        public string Token;
    }


    private static async Task<CareerIdentity> ResolveIdentityAsync(
        AtlasBoardLobbyRuntimeBridge bridge = null)
    {
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
        {
            string uid = bridge != null ? bridge.CurrentAccountId : string.Empty;
            string token = bridge != null ? bridge.AuthTokenForOnlineSubsystems : string.Empty;
            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(token))
                AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(
                    out uid, out token);
            return string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(token)
                ? null
                : new CareerIdentity { Uid = uid, Token = token };
        }

        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null || string.IsNullOrWhiteSpace(user.UserId)) return null;
            string token = await user.TokenAsync(false);
            return string.IsNullOrWhiteSpace(token)
                ? null
                : new CareerIdentity { Uid = user.UserId, Token = token };
        }
        catch { return null; }
    }


    private static bool CurrentIdentity(string uid,
        AtlasBoardLobbyRuntimeBridge bridge = null)
    {
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
        {
            string current = bridge != null ? bridge.CurrentAccountId : string.Empty;
            if (string.IsNullOrWhiteSpace(current))
                AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(
                    out current, out _);
            return string.Equals(current, uid, StringComparison.Ordinal);
        }

        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            return user != null && user.UserId == uid;
        }
        catch { return false; }
    }


    // Recorded during actual online play, before any final-result publication.
    // An authenticated participant signs only their own immutable UID/slot.
    public static async Task<bool> RegisterParticipationAsync(string matchId,
        AtlasBoardLobbyRuntimeBridge bridge,
        AtlasBoardTurnDiceNetworkCoordinator coordinator)
    {
        if (bridge == null || coordinator == null || !coordinator.IsOnlineSessionActive ||
            string.IsNullOrWhiteSpace(matchId)) return false;
        CareerIdentity identity = await ResolveIdentityAsync(bridge);
        if (identity == null || identity.Uid != bridge.CurrentAccountId ||
            !await AtlasBoardRtdbMetaEconomy.IsCareerRewardEligibleAsync())
            return false;
        string uid = identity.Uid;
        string token = identity.Token;
        if (string.IsNullOrEmpty(token) || !CurrentIdentity(uid, bridge)) return false;
        string path = "/careerParticipation/" + Safe(uid) + "/" + Safe(matchId);
        if (ObjectOrNull(await RequestAsync(path, token)) != null) return true;
        JObject state = ObjectOrNull(await RequestAsync("/matchState/" + Safe(matchId), token));
        if (state == null || String(state, "status") != "active" ||
            String(state, "phase") == "match_complete") return false;
        string room = String(state, "lobbyId");
        JObject members = ObjectOrNull(await RequestAsync("/roomMembers/" + Safe(room), token));
        if (members == null || (bool?)members[uid]?["active"] != true) return false;
        string role = String(members[uid], "role");
        if (role != "host" && role != "remoteHuman") return false;
        int slot = -1;
        for (int i = 0; i < 4; i++)
            if (coordinator.IsLocallyControlledHumanSlot(i)) { slot = i; break; }
        if (slot < 0) return false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        JObject receipt = new JObject {
            ["uid"] = uid, ["matchId"] = matchId, ["lobbyId"] = room,
            ["slotIndex"] = slot, ["role"] = role, ["atMs"] = now
        };
        if (!CurrentIdentity(uid, bridge)) return false;
        try { await RequestAsync(path, token, receipt.ToString(Newtonsoft.Json.Formatting.None)); }
        catch (InvalidOperationException) { /* another attempt or server rejection */ }
        return ObjectOrNull(await RequestAsync(path, token)) != null;
    }



    private static async Task<JObject> WaitForCompletedArchiveAsync(
        string matchId, string token, int attempts = 16)
    {
        JObject archive = null;
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            archive = ObjectOrNull(await RequestAsync(
                "/completedMatchArchives/" + Safe(matchId), token));
            if (archive != null) return archive;
            await Task.Delay(250);
        }
        return null;
    }


    private static async Task<bool> WriteArchiveAttestationAsync(
        string matchId, string uid, string token, JObject archive)
    {
        if (archive == null || !CurrentIdentity(uid)) return false;
        string digest = String(archive, "digest");
        int revision = Int(archive, "revision", 10000000);
        string path = "/careerAttestations/" + Safe(matchId) + "/" + Safe(uid);
        JObject prior = ObjectOrNull(await RequestAsync(path, token));
        if (prior != null)
            return String(prior, "digest") == digest &&
                Int(prior, "revision", 10000000) == revision;

        JObject attestation = new JObject
        {
            ["uid"] = uid,
            ["matchId"] = matchId,
            ["lobbyId"] = String(archive, "lobbyId"),
            ["digest"] = digest,
            ["revision"] = revision,
            ["atMs"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        try
        {
            await RequestAsync(path, token,
                attestation.ToString(Newtonsoft.Json.Formatting.None));
        }
        catch (InvalidOperationException) { }
        prior = ObjectOrNull(await RequestAsync(path, token));
        bool saved = prior != null && String(prior, "digest") == digest &&
            Int(prior, "revision", 10000000) == revision;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("ATB_CAREER_ARCHIVE_ATTEST saved=" +
            saved.ToString().ToLowerInvariant() + " uid=" +
            uid.Substring(0, Mathf.Min(7, uid.Length)) + " match=" + matchId);
#endif
        return saved;
    }


    // Called from either Host or follower snapshot handling as soon as the
    // immutable final match frame is observed. This is intentionally earlier than Result UI,
    // Leave Match and Rematch so a player cannot lose their result merely by
    // navigating away after the match has already completed.
    public static async Task ObserveCompletedMatchAsync(
        string matchId,
        AtlasBoardLobbyRuntimeBridge bridge,
        AtlasMatchNetworkSnapshot observed)
    {
        if (bridge == null || observed == null ||
            string.IsNullOrWhiteSpace(matchId) ||
            !string.Equals(observed.MatchId, matchId, StringComparison.Ordinal) ||
            !string.Equals(observed.Phase, "match_complete", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(observed.SnapshotJson))
            return;

        string localUid = bridge.CurrentAccountId ?? string.Empty;
        string observedDigest = Hash(observed.SnapshotJson);
        PersistPendingObservation(
            localUid, matchId, observedDigest, observed.Revision);

        CareerIdentity identity = await ResolveIdentityAsync(bridge);
        if (identity == null || identity.Uid != localUid ||
            !await AtlasBoardRtdbMetaEconomy.IsCareerRewardEligibleAsync())
            return;

        JObject participation = ObjectOrNull(await RequestAsync(
            "/careerParticipation/" + Safe(identity.Uid) + "/" + Safe(matchId),
            identity.Token));
        if (participation == null) return;

        JObject archive = await WaitForCompletedArchiveAsync(matchId,
            identity.Token, 12);
        if (archive == null) return;
        string digest = observedDigest;
        if (!string.Equals(String(archive, "digest"), digest,
                StringComparison.Ordinal) ||
            Int(archive, "revision", 10000000) != observed.Revision)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("ATB_CAREER_ARCHIVE_ATTEST saved=false " +
                "reason=observed_frame_archive_mismatch match=" + matchId);
#endif
            return;
        }

        if (await WriteArchiveAttestationAsync(
                matchId, identity.Uid, identity.Token, archive))
            ClearPendingObservation(identity.Uid, matchId);
    }


    private static async Task<bool> EnsureLocalArchiveAttestationAsync(
        string matchId, string uid, string token,
        AtlasBoardTurnDiceNetworkCoordinator coordinator,
        MatchResultManager resultManager)
    {
        if (coordinator == null || resultManager == null) return false;
        JObject own = ObjectOrNull(await RequestAsync(
            "/careerParticipation/" + Safe(uid) + "/" + Safe(matchId), token));
        if (own == null) return false;
        int slot = Int(own, "slotIndex", 3);
        JObject archive = await WaitForCompletedArchiveAsync(matchId, token, 16);
        if (archive == null) return false;
        JObject frame = ObjectOrNull(String(archive, "snapshotJson"));
        JToken result = frame?["matchResult"];
        MatchResultManager.OnlineResultSnapshot local =
            resultManager.BuildOnlineResultSnapshot();
        if (result == null || local == null || !local.valid ||
            !coordinator.IsLocallyControlledHumanSlot(slot) ||
            local.participating == null || slot >= local.participating.Length ||
            !local.participating[slot]) return false;
        JArray winners = result["winnerSlots"] as JArray;
        bool localWon = local.winnerSlots != null &&
            local.winnerSlots.Contains(slot);
        bool archiveWon = winners != null && winners.Any(x => (int?)x == slot);
        bool verified = localWon == archiveWon &&
            Indexed(result, "netWorth", slot, int.MaxValue) ==
                Mathf.Max(0, local.netWorth[slot]) &&
            Indexed(result, "cash", slot, int.MaxValue) ==
                Mathf.Max(0, local.cash[slot]) &&
            Indexed(result, "propertyCount", slot, 100) ==
                Mathf.Clamp(local.propertyCount[slot], 0, 100) &&
            Indexed(result, "developmentLevels", slot, 100) ==
                Mathf.Clamp(local.developmentLevels[slot], 0, 100);
        if (!verified) return false;
        PersistPendingObservation(
            uid, matchId, String(archive, "digest"),
            Int(archive, "revision", 10000000));
        bool saved = await WriteArchiveAttestationAsync(
            matchId, uid, token, archive);
        if (saved) ClearPendingObservation(uid, matchId);
        return saved;
    }


    private static async Task<bool> EnsureFinalProofAsync(
        string matchId,
        string uid,
        string token,
        AtlasBoardLobbyRuntimeBridge bridge,
        int archiveAttempts = 16)
    {
        string path = "/careerFinalProofs/" + Safe(matchId);
        JObject existing = ObjectOrNull(await RequestAsync(path, token));
        if (existing != null)
            return true;

        JObject ownParticipation = ObjectOrNull(await RequestAsync(
            "/careerParticipation/" + Safe(uid) + "/" + Safe(matchId),
            token));

        if (ownParticipation == null ||
            String(ownParticipation, "uid") != uid)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "ATB_CAREER_FINAL_PROOF stage=publisher_participation_missing " +
                "match=" + matchId);
#endif
            return false;
        }

        // Completed-match evidence is independent of Rematch and independent
        // of reward eligibility. The authoritative Host transport writes this
        // immutable archive as soon as match_complete is published and again
        // verifies it before deleting matchState during Rematch.
        JObject archive = await WaitForCompletedArchiveAsync(
            matchId, token, Mathf.Clamp(archiveAttempts, 1, 16));

        if (archive == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Recovery may inspect historical pre-archive matches. Do not emit
            // one line per polling attempt; only a live finalization timeout is
            // actionable during a match test.
            if (archiveAttempts > 1)
                Debug.LogWarning(
                    "ATB_CAREER_FINAL_PROOF stage=archive_timeout match=" +
                    matchId);
#endif
            return false;
        }

        string finalHostUid = String(archive, "hostUid");
        if (!string.Equals(finalHostUid, uid, StringComparison.Ordinal))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                "ATB_CAREER_FINAL_PROOF stage=publisher_not_final_host " +
                "publisher=" + uid.Substring(0, Mathf.Min(7, uid.Length)) +
                " final_host=" +
                finalHostUid.Substring(0, Mathf.Min(7, finalHostUid.Length)) +
                " match=" + matchId);
#endif
            return false;
        }

        string snapshotJson = String(archive, "snapshotJson");
        string archiveDigest = String(archive, "digest");
        JObject frame = ObjectOrNull(snapshotJson);
        JToken finalResult = frame?["matchResult"];

        if (frame == null ||
            finalResult == null ||
            (bool?)finalResult["valid"] != true ||
            String(frame, "phase") != "match_complete" ||
            string.IsNullOrWhiteSpace(archiveDigest) ||
            !string.Equals(
                archiveDigest,
                Hash(snapshotJson),
                StringComparison.Ordinal))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "ATB_CAREER_FINAL_PROOF stage=invalid_archive_frame match=" +
                matchId);
#endif
            return false;
        }

        // Do not depend on mutable roomMembers after the result screen. Parse
        // the immutable match-seat roster and look for another participant who
        // signed before completion. If that peer later leaves the lobby, their
        // receipt remains available for recovery.
        string witness = string.Empty;
        JObject archivedSeats = ObjectOrNull(String(archive, "seatsJson"));
        JArray seatRows = archivedSeats?["seats"] as JArray;

        if (seatRows != null)
        {
            foreach (JToken seatRow in seatRows)
            {
                string candidateUid = String(seatRow, "uid");
                if (string.IsNullOrWhiteSpace(candidateUid) ||
                    string.Equals(candidateUid, uid, StringComparison.Ordinal))
                {
                    continue;
                }

                JObject signed = ObjectOrNull(await RequestAsync(
                    "/careerParticipation/" + Safe(candidateUid) + "/" +
                    Safe(matchId), token));
                JObject attested = ObjectOrNull(await RequestAsync(
                    "/careerAttestations/" + Safe(matchId) + "/" +
                    Safe(candidateUid), token));

                if (signed == null || String(signed, "uid") != candidateUid ||
                    attested == null ||
                    String(attested, "digest") != archiveDigest ||
                    Int(attested, "revision", 10000000) !=
                        Int(archive, "revision", 10000000))
                    continue;

                witness = candidateUid;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(witness))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "ATB_CAREER_FINAL_PROOF stage=witness_missing " +
                "source=completed_match_archive match=" + matchId);
#endif
            return false;
        }

        JArray winnerSlots = finalResult?["winnerSlots"] as JArray;
        int completedRounds = Int(frame, "currentRound", 100);
        int completedTurns = Int(frame, "completedTurns", 10000);
        JObject seats = new JObject();

        for (int index = 0; index < 4; index++)
        {
            bool won =
                winnerSlots != null &&
                winnerSlots.Any(x => (int?)x == index);

            string kind =
                won
                    ? winnerSlots.Count > 1 ? "tie" : "win"
                    : "loss";

            seats[index.ToString()] = new JObject
            {
                ["result"] = kind,
                ["participating"] =
                    IndexedBool(finalResult, "participating", index),
                ["properties"] =
                    Indexed(finalResult, "propertyCount", index, 100),
                ["developments"] =
                    Indexed(finalResult, "developmentLevels", index, 100),
                ["cash"] =
                    Indexed(finalResult, "cash", index, 100000000),
                ["netWorth"] =
                    Indexed(finalResult, "netWorth", index, 100000000),
                ["diceRolls"] =
                    Indexed(frame, "careerDiceRolls", index, 10000),
                ["doublesRolled"] =
                    Indexed(frame, "careerDoublesRolled", index, 10000)
            };
        }

        string mapId = String(archive, "mapId");
        string themeId = String(archive, "themeId");
        int roundLimit = Int(archive, "roundLimit", 100);

        if (string.IsNullOrWhiteSpace(mapId))
            mapId = "unknown";
        if (string.IsNullOrWhiteSpace(themeId))
            themeId = "classic_table";
        if (roundLimit < 1)
            roundLimit = Mathf.Clamp(Int(frame, "roundLimit", 100), 1, 100);

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        JObject proof = new JObject
        {
            ["seats"] = seats,
            ["rounds"] = completedRounds,
            ["turns"] = completedTurns,
            ["playerCount"] =
                (finalResult?["participating"] as JArray)?
                    .Count(x => (bool?)x == true) ?? 0,
            ["mapId"] =
                mapId.Substring(0, Mathf.Min(64, mapId.Length)),
            ["themeId"] =
                themeId.Substring(0, Mathf.Min(64, themeId.Length)),
            ["roundLimit"] = Mathf.Clamp(roundLimit, 1, 100),
            ["matchId"] = matchId,
            ["lobbyId"] = String(archive, "lobbyId"),
            ["hostUid"] = finalHostUid,
            ["witnessUid"] = witness,
            ["revision"] = Int(archive, "revision", 10000000),
            ["digest"] = archiveDigest,
            ["snapshotJson"] = snapshotJson,
            ["atMs"] = now
        };

        if (!CurrentIdentity(uid) ||
            (bridge != null && bridge.CurrentAccountId != uid))
        {
            return false;
        }

        try
        {
            await RequestAsync(
                path,
                token,
                proof.ToString(Newtonsoft.Json.Formatting.None));
        }
        catch (InvalidOperationException exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "ATB_CAREER_FINAL_PROOF stage=write_denied detail=" +
                exception.Message +
                " match=" + matchId);
#endif
        }

        bool written =
            ObjectOrNull(await RequestAsync(path, token)) != null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            "ATB_CAREER_FINAL_PROOF stage=write_result saved=" +
            written.ToString().ToLowerInvariant() +
            " source=completed_match_archive witness=" +
            witness.Substring(0, Mathf.Min(7, witness.Length)) +
            " match=" + matchId);
#endif

        return written;
    }


    public static async Task<RecordStatus> RecordAsync(
        string matchId,
        AtlasBoardLobbyRuntimeBridge bridge,
        AtlasBoardTurnDiceNetworkCoordinator coordinator,
        MatchResultManager resultManager,
        TurnManager turnManager,
        AtlasBoardProgressionSlotTelemetry[] telemetry)
    {
        var outcome = new RecordStatus { Reason = "not_eligible" };
        CareerIdentity identity = await ResolveIdentityAsync(bridge);
        if (identity == null || bridge == null || coordinator == null ||
            !coordinator.IsOnlineSessionActive || string.IsNullOrWhiteSpace(matchId) ||
            identity.Uid != bridge.CurrentAccountId ||
            !await AtlasBoardRtdbMetaEconomy.IsCareerRewardEligibleAsync()) return outcome;
        string token = identity.Token;
        if (string.IsNullOrWhiteSpace(token)) return outcome;
        string uid = identity.Uid;

        // Commit this client's independent observation BEFORE any final proof
        // depends on it. Host/follower order and post-result navigation no
        // longer decide whether the witness exists.
        await EnsureLocalArchiveAttestationAsync(
            matchId, uid, token, coordinator, resultManager);
        if (coordinator.LocalIsHost)
            await EnsureFinalProofAsync(matchId, uid, token, bridge);
        return await ClaimFromFinalAsync(matchId, uid, token, bridge,
            coordinator, resultManager, turnManager, false);
    }


    // Called when a player opens Career after disconnect, app restart or
    // delayed network availability. The original signed participation survives
    // even when the player's local match objects are gone.
    public static async Task RecoverPendingAsync(string uid, string token)
    {
        if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(token) ||
            !CurrentIdentity(uid)) return;
        JObject receipts = ObjectOrNull(await RequestAsync(
            "/careerParticipation/" + Safe(uid), token));
        if (receipts == null) return;
        int inspected = 0;
        foreach (JProperty entry in receipts.Properties().Reverse())
        {
            if (++inspected > 30 || !CurrentIdentity(uid)) break;
            try
            {
                if (String(entry.Value, "uid") != uid) continue;

                JObject existingClaim = ObjectOrNull(await RequestAsync(
                    "/careerClaims/" + Safe(uid) + "/" + Safe(entry.Name), token));
                if (existingClaim != null)
                {
                    if (Int(existingClaim, "goldAwarded", 50) > 0)
                        await AtlasBoardRtdbMetaEconomy.ClaimMatchGoldAsync(entry.Name);
                    continue;
                }

                // Resume an exact final-frame observation that was persisted
                // before an immediate Leave/Rematch/process close interrupted
                // the network attestation. Recovery performs one archive read
                // per historical match instead of starting a 4-second poll.
                if (TryReadPendingObservation(
                        uid, entry.Name, out string observedDigest,
                        out int observedRevision))
                {
                    JObject archive = await WaitForCompletedArchiveAsync(
                        entry.Name, token, 1);
                    if (archive != null &&
                        String(archive, "digest") == observedDigest &&
                        Int(archive, "revision", 10000000) == observedRevision &&
                        await WriteArchiveAttestationAsync(
                            entry.Name, uid, token, archive))
                    {
                        ClearPendingObservation(uid, entry.Name);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log(
                            "ATB_CAREER_RECOVERY attestation_resumed=true match=" +
                            entry.Name);
#endif
                    }
                }

                // The original final Host may have left the lobby before the
                // Career write completed. The immutable completion archive
                // survives Rematch/room cleanup, so recovery can publish the
                // missing final proof later without needing live roomMembers.
                await EnsureFinalProofAsync(
                    entry.Name,
                    uid,
                    token,
                    null,
                    1);

                await ClaimFromFinalAsync(
                    entry.Name,
                    uid,
                    token,
                    null,
                    null,
                    null,
                    null,
                    true);

                JObject claim = ObjectOrNull(await RequestAsync(
                    "/careerClaims/" + Safe(uid) + "/" + Safe(entry.Name), token));
                if (claim != null && Int(claim, "goldAwarded", 50) > 0)
                    await AtlasBoardRtdbMetaEconomy.ClaimMatchGoldAsync(entry.Name);
            }
            catch (Exception e)
            {
                Debug.LogWarning("ATB_CAREER_RECOVERY pending=" + e.GetType().Name);
            }
        }
    }


    private static async Task<RecordStatus> ClaimFromFinalAsync(
        string matchId, string uid, string token,
        AtlasBoardLobbyRuntimeBridge bridge,
        AtlasBoardTurnDiceNetworkCoordinator coordinator,
        MatchResultManager resultManager,
        TurnManager turnManager,
        bool allowRecovery)
    {
        var outcome = new RecordStatus { Reason = "final_proof_pending" };
        if (!CurrentIdentity(uid)) { outcome.Reason = "identity_changed"; return outcome; }
        JObject own = ObjectOrNull(await RequestAsync("/careerParticipation/" +
            Safe(uid) + "/" + Safe(matchId), token));
        if (own == null || String(own, "uid") != uid)
        { outcome.Reason = "participation_missing"; return outcome; }
        JObject proof = ObjectOrNull(await RequestAsync(
            "/careerFinalProofs/" + Safe(matchId), token));
        if (proof == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!allowRecovery)
                Debug.Log("ATB_CAREER_CLAIM_FLOW stage=proof_pending match=" + matchId);
#endif
            return outcome;
        }
        string snapshotJson = String(proof, "snapshotJson");
        JObject frame = ObjectOrNull(snapshotJson);
        JToken result = frame?["matchResult"];
        if (frame == null || result == null || (bool?)result["valid"] != true ||
            String(frame, "phase") != "match_complete" ||
            String(proof, "digest") != Hash(snapshotJson))
        { outcome.Reason = "invalid_final_proof"; return outcome; }
        string host = String(proof, "hostUid");
        string witness = uid == host ? String(proof, "witnessUid") : host;
        int slot = Int(own, "slotIndex", 3);
        if (witness == uid || string.IsNullOrWhiteSpace(witness) ||
            !IndexedBool(result, "participating", slot))
        { outcome.Reason = "second_human_required"; return outcome; }


        string claimPath = "/careerClaims/" + Safe(uid) + "/" + Safe(matchId);
        if (ObjectOrNull(await RequestAsync(claimPath, token)) != null)
        {
            bool alreadyPaid =
                await AtlasBoardRtdbMetaEconomy.ClaimMatchGoldAsync(matchId);
            outcome.Applied = alreadyPaid;
            outcome.Reason = alreadyPaid
                ? "career_and_gold_saved"
                : "xp_saved_gold_pending";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("ATB_CAREER_CLAIM_FLOW stage=existing_claim gold_paid=" +
                alreadyPaid.ToString().ToLowerInvariant() +
                " match=" + matchId);
#endif
            return outcome;
        }
        bool localVerified = false;
        if (!allowRecovery && coordinator != null && resultManager != null)
        {
            var local = resultManager.BuildOnlineResultSnapshot();
            JArray winning = result["winnerSlots"] as JArray;
            bool localWon = local?.winnerSlots != null && local.winnerSlots.Contains(slot);
            bool proofWon = winning != null && winning.Any(x => (int?)x == slot);
            localVerified = local != null && local.valid &&
                coordinator.IsLocallyControlledHumanSlot(slot) &&
                local.participating[slot] && localWon == proofWon &&
                Indexed(result, "netWorth", slot, int.MaxValue) ==
                    Mathf.Max(0, local.netWorth[slot]) &&
                Indexed(result, "cash", slot, int.MaxValue) ==
                    Mathf.Max(0, local.cash[slot]) &&
                Indexed(result, "propertyCount", slot, 100) ==
                    Mathf.Clamp(local.propertyCount[slot], 0, 100) &&
                Indexed(result, "developmentLevels", slot, 100) ==
                    Mathf.Clamp(local.developmentLevels[slot], 0, 100);
            if (!localVerified)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "ATB_CAREER_CLAIM_FLOW stage=local_crosscheck_mismatch " +
                    "fallback=immutable_final_proof match=" + matchId +
                    " slot=" + slot);
#endif
                // Result UI objects can already be resetting for Rematch/Leave.
                // Security is enforced by participation + immutable archive +
                // matching peer attestations + RTDB claim rules, not by a
                // mutable scene mirror surviving long enough.
            }
        }
        string digest = String(proof, "digest");
        int revision = Int(proof, "revision", 10000000);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        JObject ownAtt = ObjectOrNull(await RequestAsync(
            "/careerAttestations/" + Safe(matchId) + "/" + Safe(uid), token));
        if (ownAtt == null || String(ownAtt, "digest") != digest ||
            Int(ownAtt, "revision", 10000000) != revision)
        {
            outcome.Reason = "local_attestation_pending";
            return outcome;
        }
        JObject witnessAtt = ObjectOrNull(await RequestAsync(
            "/careerAttestations/" + Safe(matchId) + "/" + Safe(witness), token));
        if (witnessAtt == null || String(witnessAtt, "digest") != digest ||
            Int(witnessAtt, "revision", 10000000) != revision)
        {
            // A signed, eligible pre-finish participant still earns 1 XP
            // once an immutable final proof exists, even if they disconnect.
            await SaveParticipationCreditAsync(uid, matchId, witness, proof, token);
            outcome.Reason = "waiting_for_peer";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!allowRecovery)
                Debug.Log("ATB_CAREER_CLAIM_FLOW stage=waiting_for_peer match=" +
                    matchId + " witness=" +
                    witness.Substring(0, Mathf.Min(7, witness.Length)));
#endif
            return outcome;
        }
        JObject seat = IndexedObject(proof, "seats", slot);
        if (seat == null || (bool?)seat["participating"] != true)
        { outcome.Reason = "missing_final_seat"; return outcome; }
        int rounds = Int(proof, "rounds", 100);
        int properties = Int(seat, "properties", 100);
        int developments = Int(seat, "developments", 100);
        string kind = String(seat, "result");
        int diceRolls = Int(seat, "diceRolls", 10000);
        int doubles = Int(seat, "doublesRolled", 10000);
        int turns = Int(proof, "turns", 10000);
        if (doubles > diceRolls || diceRolls > (turns * 3 + 10))
        { outcome.Reason = "dice_telemetry_invalid"; return outcome; }
        string mapId = String(proof, "mapId");
        string themeId = String(proof, "themeId");
        JObject claim = new JObject {
            ["uid"] = uid, ["matchId"] = matchId, ["lobbyId"] = String(proof,"lobbyId"),
            ["witnessUid"] = witness, ["digest"] = digest, ["revision"] = revision,
            ["atMs"] = now, ["result"] = kind, ["slotIndex"] = slot,
            ["rounds"] = rounds, ["turns"] = turns,
            ["properties"] = properties, ["developments"] = developments,
            ["cash"] = Int(seat, "cash", 100000000),
            ["netWorth"] = Int(seat, "netWorth", 100000000),
            ["playerCount"] = Int(proof, "playerCount", 4),
            ["mapId"] = mapId.Substring(0, Mathf.Min(64, mapId.Length)),
            ["themeId"] = themeId.Substring(0, Mathf.Min(64, themeId.Length)),
            ["roundLimit"] = Int(proof, "roundLimit", 100),
            ["hostAccountId"] = host, ["diceRolls"] = diceRolls,
            ["doublesRolled"] = doubles, ["recovered"] = !localVerified,
            ["xpAwarded"] = XpAward(kind, rounds, properties, developments),
            ["goldAwarded"] = GoldAward(kind, rounds, properties, developments)
        };
        if (!CurrentIdentity(uid) || (bridge != null && bridge.CurrentAccountId != uid))
        { outcome.Reason = "identity_changed"; return outcome; }
        try { await RequestAsync(claimPath, token, claim.ToString(Newtonsoft.Json.Formatting.None)); }
        catch (InvalidOperationException)
        {
            if (ObjectOrNull(await RequestAsync(claimPath, token)) == null)
            { outcome.Reason = "claim_denied"; return outcome; }
        }
        outcome.Applied = true;
        bool paid = await AtlasBoardRtdbMetaEconomy.ClaimMatchGoldAsync(matchId);
        outcome.Reason = paid ? "career_and_gold_saved" : "xp_saved_gold_pending";
        Debug.Log("ATB_CAREER_RTDB_CLAIM status=" + outcome.Reason +
            " xp=" + claim["xpAwarded"] + " gold=" + claim["goldAwarded"]);
        return outcome;
    }


    private static async Task SaveParticipationCreditAsync(string uid, string matchId,
        string witness, JObject proof, string token)
    {
        string path = "/careerParticipationCredits/" + Safe(uid) + "/" + Safe(matchId);
        if (ObjectOrNull(await RequestAsync(path, token)) != null) return;
        JObject credit = new JObject {
            ["uid"] = uid, ["matchId"] = matchId, ["witnessUid"] = witness,
            ["digest"] = String(proof,"digest"),
            ["xpAwarded"] = 1, ["atMs"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        if (!CurrentIdentity(uid)) return;
        try { await RequestAsync(path, token, credit.ToString(Newtonsoft.Json.Formatting.None)); }
        catch (InvalidOperationException) { }
    }


    private static void AddProvisionalAchievement(AtlasBoardProgressionSnapshot snapshot,
        string id, int target, int progress)
    {
        snapshot.Achievements.Add(new AtlasBoardProgressionAchievement {
            achievementId = id,
            target = target,
            progress = Mathf.Max(0, progress),
            unlocked = progress >= target
        });
    }


    private static bool IsCompleteResult(string value)
    {
        return value == "win" || value == "tie" || value == "loss";
    }


    private static void ApplyClaimDisplayFields(
        AtlasBoardProgressionSnapshot snapshot, JObject claims, string uid)
    {
        if (snapshot == null || claims == null || snapshot.RecentMatches == null)
            return;

        foreach (AtlasBoardProgressionMatchSummary match in snapshot.RecentMatches)
        {
            if (match == null || string.IsNullOrWhiteSpace(match.matchId))
                continue;
            JObject claim = claims[match.matchId] as JObject;
            if (claim == null || String(claim, "uid") != uid) continue;

            match.goldAwarded = Int(claim, "goldAwarded", 50);
            match.localSlotIndex = Int(claim, "slotIndex", 3);
            string result = String(claim, "result");
            if (IsCompleteResult(result))
            {
                match.result = result;
                if (result == "win" || result == "tie") match.place = 1;
                else if (match.place < 2) match.place = 2;
            }
        }
    }


    private static async Task RefreshMatchResultFromProofAsync(
        AtlasBoardProgressionMatchSummary match, string token)
    {
        if (match == null || string.IsNullOrWhiteSpace(match.matchId) ||
            match.localSlotIndex < 0 || match.localSlotIndex > 3) return;
        try
        {
            JObject proof = ObjectOrNull(await RequestAsync(
                "/careerFinalProofs/" + Safe(match.matchId), token));
            if (proof == null || String(proof, "matchId") != match.matchId) return;

            JObject seat = IndexedObject(proof, "seats", match.localSlotIndex);
            if (seat == null || (bool?)seat["participating"] != true) return;
            string result = String(seat, "result");
            if (IsCompleteResult(result))
            {
                match.result = result;
                match.place = result == "loss" ? Mathf.Max(2, match.place) : 1;
            }
            match.finalCash = Int(seat, "cash", 100000000);
            match.propertyCount = Int(seat, "properties", 100);
            match.developmentLevels = Int(seat, "developments", 100);
            match.netWorth = Int(seat, "netWorth", 100000000);
            match.completedRounds = Int(proof, "rounds", 100);
            match.completedTurns = Int(proof, "turns", 10000);
            match.playerCount = Int(proof, "playerCount", 4);
            if (!string.IsNullOrWhiteSpace(String(proof, "mapId")))
                match.mapId = String(proof, "mapId");
            if (!string.IsNullOrWhiteSpace(String(proof, "themeId")))
                match.themeId = String(proof, "themeId");
            if (Int(proof, "roundLimit", 100) > 0)
                match.roundLimit = Int(proof, "roundLimit", 100);
        }
        catch (Exception)
        {
            // Historical proof enrichment is presentation-only. One expired or
            // inaccessible old proof must not make the whole Career page fail.
        }
    }


    private static async Task RefreshRecentMatchFinalResultsAsync(
        AtlasBoardProgressionSnapshot snapshot, string token)
    {
        if (snapshot == null || snapshot.RecentMatches == null ||
            string.IsNullOrWhiteSpace(token)) return;
        List<Task> tasks = new List<Task>();
        foreach (AtlasBoardProgressionMatchSummary match in
                 snapshot.RecentMatches.Where(x => x != null &&
                    !string.Equals(x.result, "participation", StringComparison.OrdinalIgnoreCase))
                    .Take(10))
            tasks.Add(RefreshMatchResultFromProofAsync(match, token));
        if (tasks.Count > 0) await Task.WhenAll(tasks);
    }


    public static async Task<bool> PopulateMatchDetailsAsync(
        AtlasBoardProgressionMatchSummary match,
        AtlasBoardLobbyRuntimeBridge bridge = null)
    {
        if (match == null || string.IsNullOrWhiteSpace(match.matchId))
            return false;
        CareerIdentity identity = await ResolveIdentityAsync(bridge);
        if (identity == null || string.IsNullOrWhiteSpace(identity.Token))
            return false;

        JObject proof = ObjectOrNull(await RequestAsync(
            "/careerFinalProofs/" + Safe(match.matchId), identity.Token));
        JObject archive = ObjectOrNull(await RequestAsync(
            "/completedMatchArchives/" + Safe(match.matchId), identity.Token));
        if (proof == null || archive == null ||
            String(proof, "matchId") != match.matchId ||
            String(archive, "matchId") != match.matchId ||
            String(proof, "digest") != String(archive, "digest") ||
            Int(proof, "revision", 10000000) !=
                Int(archive, "revision", 10000000))
            return false;

        JObject archivedSeats = ObjectOrNull(String(archive, "seatsJson"));
        JArray seatRows = archivedSeats?["seats"] as JArray;
        JObject frame = ObjectOrNull(String(proof, "snapshotJson"));
        JToken finalResult = frame?["matchResult"];
        string finalHostUid = String(proof, "hostUid");
        var participants = new List<AtlasBoardProgressionParticipant>();

        for (int slot = 0; slot < 4; slot++)
        {
            JObject seat = IndexedObject(proof, "seats", slot);
            if (seat == null || (bool?)seat["participating"] != true) continue;

            JToken rosterSeat = seatRows?.FirstOrDefault(row =>
                ((int?)row?["slotIndex"] ?? -1) == slot);
            string displayName = String(rosterSeat, "name");
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "Player " + (slot + 1);
            string accountId = String(rosterSeat, "uid");
            string result = String(seat, "result");

            participants.Add(new AtlasBoardProgressionParticipant
            {
                slotIndex = slot,
                displayName = displayName,
                controllerKind = String(rosterSeat, "controller"),
                isHost = (bool?)rosterSeat?["host"] == true ||
                    (!string.IsNullOrWhiteSpace(accountId) &&
                     accountId == finalHostUid),
                bankrupt = IndexedBool(finalResult, "bankrupt", slot),
                place = 0,
                result = IsCompleteResult(result) ? result : "loss",
                cash = Int(seat, "cash", 100000000),
                propertyCount = Int(seat, "properties", 100),
                propertyValue = Indexed(finalResult, "propertyValue", slot, 100000000),
                developmentLevels = Int(seat, "developments", 100),
                developmentValue = Indexed(finalResult, "developmentValue", slot, 100000000),
                netWorth = Int(seat, "netWorth", 100000000)
            });
        }

        List<AtlasBoardProgressionParticipant> winners = participants
            .Where(p => p.result == "win" || p.result == "tie")
            .OrderBy(p => p.slotIndex).ToList();
        foreach (AtlasBoardProgressionParticipant winner in winners)
            winner.place = 1;
        int nextPlace = Mathf.Max(1, winners.Count) + 1;
        foreach (AtlasBoardProgressionParticipant loser in participants
                     .Where(p => p.result == "loss")
                     .OrderByDescending(p => p.netWorth)
                     .ThenByDescending(p => p.cash)
                     .ThenBy(p => p.slotIndex))
            loser.place = nextPlace++;

        match.participants = participants.OrderBy(p => p.slotIndex).ToArray();
        match.completedRounds = Int(proof, "rounds", 100);
        match.completedTurns = Int(proof, "turns", 10000);
        match.playerCount = participants.Count;
        match.hostAccountId = finalHostUid;
        if (!string.IsNullOrWhiteSpace(String(proof, "mapId")))
            match.mapId = String(proof, "mapId");
        if (!string.IsNullOrWhiteSpace(String(proof, "themeId")))
            match.themeId = String(proof, "themeId");
        if (Int(proof, "roundLimit", 100) > 0)
            match.roundLimit = Int(proof, "roundLimit", 100);

        AtlasBoardProgressionParticipant local = participants.FirstOrDefault(
            p => p.slotIndex == match.localSlotIndex);
        if (local != null)
        {
            match.result = local.result;
            match.place = local.place;
            match.bankrupt = local.bankrupt;
            match.finalCash = local.cash;
            match.propertyCount = local.propertyCount;
            match.propertyValue = local.propertyValue;
            match.developmentLevels = local.developmentLevels;
            match.developmentValue = local.developmentValue;
            match.netWorth = local.netWorth;
        }
        return participants.Count > 0;
    }


    private static async Task<int> VerifiedBonusXpAsync(string uid, string token)
    {
        // Two independently rule-protected sources. Promo XP is credited in
        // the same ETag wallet commit as its one-use code marker, not from a
        // client-authored match result or an unverified receipt.
        int seasonal = 0;
        int promo = 0;
        try
        {
            string raw = await RequestAsync(
                "/users/" + Safe(uid) + "/seasonalRewards/xp", token);
            JToken value = JToken.Parse(raw);
            if (value.Type == JTokenType.Integer)
                seasonal = Mathf.Clamp((int?)value ?? 0, 0, 100000000);
        }
        catch { } // Legacy user with no seasonal rewards.
        try
        {
            string raw = await RequestAsync(
                "/users/" + Safe(uid) + "/metaEconomy/promoXp", token);
            JToken value = JToken.Parse(raw);
            if (value.Type == JTokenType.Integer)
                promo = Mathf.Clamp((int?)value ?? 0, 0, 100000000);
        }
        catch { } // Legacy user with no promotional XP.
        return (int)Math.Min(100000000L, (long)seasonal + promo);
    }

    private static void ApplyBonusLevel(AtlasBoardProgressionSnapshot snapshot,
        int bonusXp)
    {
        if (snapshot == null || bonusXp <= 0) return;
        snapshot.TotalXp = (int)Math.Min(100000000L,
            (long)Math.Max(0, snapshot.TotalXp) + bonusXp);
        int level = 1, start = 0;
        while (level < MaxLevel)
        {
            int needed = Mathf.RoundToInt(500f * Mathf.Pow(1.1f, level - 1));
            if (snapshot.TotalXp < start + needed) break;
            start += needed; level++;
        }
        snapshot.Level = level;
        snapshot.LevelStartXp = start;
        snapshot.NextLevelXp = level >= MaxLevel ? start :
            start + Mathf.RoundToInt(500f * Mathf.Pow(1.1f, level - 1));
    }

    public static async Task<AtlasBoardProgressionSnapshot> MergeAsync(
        AtlasBoardProgressionSnapshot snapshot, string uid, string token)
    {
        if (snapshot == null || !snapshot.Success ||
            string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(token))
            return snapshot;
        int bonusXp = await VerifiedBonusXpAsync(uid, token);
        JObject claims = ObjectOrNull(await RequestAsync(
            "/careerClaims/" + UnityWebRequest.EscapeURL(uid), token));
        JObject participationCredits = ObjectOrNull(await RequestAsync(
            "/careerParticipationCredits/" + Safe(uid), token));
        if ((claims == null || claims.Count == 0) &&
            (participationCredits == null || participationCredits.Count == 0) &&
            bonusXp == 0)
            return snapshot;
        // Server projection takes precedence for totals. Claims may still
        // enrich user-facing result/Gold fields without double-adding XP.
        if (snapshot.TechnicalMessage != "empty_authoritative_profile")
        {
            ApplyClaimDisplayFields(snapshot, claims, uid);
            ApplyBonusLevel(snapshot, bonusXp);
            await RefreshRecentMatchFinalResultsAsync(snapshot, token);
            return snapshot;
        }
        List<JObject> entries = (claims ?? new JObject()).Properties().Select(p => p.Value as JObject)
            .Where(p => p != null && String(p, "uid") == uid &&
                XpAward(String(p, "result"), Int(p,"rounds",100),
                    Int(p,"properties",100), Int(p,"developments",100)) ==
                    Int(p,"xpAwarded",300))
            .OrderByDescending(x => (long?)x["atMs"] ?? 0).ToList();
        var completeMatchIds = new HashSet<string>(entries.Select(x => String(x,"matchId")),
            StringComparer.Ordinal);
        List<JObject> partial = (participationCredits ?? new JObject()).Properties()
            .Select(p => p.Value as JObject)
            .Where(x => x != null && String(x,"uid") == uid &&
                !completeMatchIds.Contains(String(x,"matchId")) &&
                Int(x,"xpAwarded",1) == 1)
            .OrderByDescending(x => (long?)x["atMs"] ?? 0).ToList();
        var stats = new AtlasBoardCareerStats();
        int totalXp = bonusXp + partial.Count; // server bonus + distinct eligible unfinished attestation


        int streak = 0;
        foreach (JObject item in entries.OrderBy(x => (long?)x["atMs"] ?? 0))
        {
            totalXp = Mathf.Min(100000000, totalXp + Int(item, "xpAwarded", 300));
            stats.matchesPlayed++;
            string kind = String(item, "result");
            if (kind == "win")
            {
                stats.wins++;
                streak++;
                stats.bestWinStreak = Mathf.Max(stats.bestWinStreak, streak);
            }
            else
            {
                if (kind == "tie") stats.ties++;
                else stats.losses++;
                streak = 0;
            }
            stats.diceRolls = (int)Math.Min(int.MaxValue,
                (long)stats.diceRolls + Int(item,"diceRolls",10000));
            stats.doublesRolled = (int)Math.Min(int.MaxValue,
                (long)stats.doublesRolled + Int(item,"doublesRolled",10000));
            stats.totalRounds += Int(item, "rounds", 100);
            stats.totalTurns += Int(item, "turns", 10000);
            stats.propertiesAtFinishTotal += Int(item, "properties", 100);
            stats.developmentLevelsAtFinishTotal += Int(item, "developments", 100);
            stats.highestCash = Mathf.Max(stats.highestCash, Int(item, "cash", 100000000));
            stats.highestNetWorth = Mathf.Max(stats.highestNetWorth, Int(item, "netWorth", 100000000));
            stats.totalNetWorth = (int)Math.Min(int.MaxValue,
                (long)stats.totalNetWorth + Int(item, "netWorth", 100000000));
            string map = String(item, "mapId").ToLowerInvariant();
            if (map.Contains("turkey")) { stats.mapTurkey++; if (kind == "win") stats.mapWinsTurkey++; }
            else if (map.Contains("colorado")) { stats.mapColorado++; if (kind == "win") stats.mapWinsColorado++; }
            else if (map.Contains("usa")) { stats.mapUsa++; if (kind == "win") stats.mapWinsUsa++; }
            else { stats.mapOther++; if (kind == "win") stats.mapWinsOther++; }
        }
        stats.currentWinStreak = streak;
        int level = 1, threshold = 0;
        while (level < MaxLevel)
        {
            int requirement = Mathf.RoundToInt(500f * Mathf.Pow(1.1f, level - 1));
            if (totalXp < threshold + requirement) break;
            threshold += requirement;
            level++;
        }
        snapshot.Stats = stats;
        snapshot.TotalXp = totalXp;
        snapshot.Level = level;
        snapshot.MaxLevel = MaxLevel;
        snapshot.LevelStartXp = threshold;
        snapshot.NextLevelXp = level >= MaxLevel ? threshold :
            threshold + Mathf.RoundToInt(500f * Mathf.Pow(1.1f, level - 1));
        snapshot.HistoryLimit = 10;
        snapshot.HistoryRetentionHours = 72;
        // These fourteen are CLIENT-SIDE PROVISIONAL indicators for this
        // peer-attested ledger. They are NOT Steam achievements or verified
        // backend unlocks. Dice/doubles cannot be inferred from old receipts.
        AddProvisionalAchievement(snapshot, "first_match", 1, stats.matchesPlayed);
        AddProvisionalAchievement(snapshot, "first_win", 1, stats.wins);
        AddProvisionalAchievement(snapshot, "win_streak_3", 3, stats.bestWinStreak);
        AddProvisionalAchievement(snapshot, "matches_10", 10, stats.matchesPlayed);
        AddProvisionalAchievement(snapshot, "wins_10", 10, stats.wins);
        AddProvisionalAchievement(snapshot, "dice_100", 100, stats.diceRolls);
        AddProvisionalAchievement(snapshot, "doubles_10", 10, stats.doublesRolled);
        AddProvisionalAchievement(snapshot, "properties_25", 25, stats.propertiesAtFinishTotal);
        AddProvisionalAchievement(snapshot, "builder_20", 20, stats.developmentLevelsAtFinishTotal);
        AddProvisionalAchievement(snapshot, "networth_5000", 5000, stats.highestNetWorth);
        AddProvisionalAchievement(snapshot, "turkey_explorer", 1, stats.mapTurkey);
        AddProvisionalAchievement(snapshot, "colorado_explorer", 1, stats.mapColorado);
        AddProvisionalAchievement(snapshot, "usa_explorer", 1, stats.mapUsa);
        AddProvisionalAchievement(snapshot, "atlas_traveler", 3,
            (stats.mapTurkey > 0 ? 1 : 0) +
            (stats.mapColorado > 0 ? 1 : 0) +
            (stats.mapUsa > 0 ? 1 : 0));
        long cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 72L * 3600000L;
        foreach (JObject item in partial.Where(x => ((long?)x["atMs"] ?? 0L) >= cutoff))
        {
            snapshot.RecentMatches.Add(new AtlasBoardProgressionMatchSummary {
                matchId = String(item,"matchId"), mode = "signed_participation_only",
                result = "participation", xpAwarded = 1,
                finishedAtEpochMs = (long?)item["atMs"] ?? 0L
            });
        }
        foreach (JObject item in entries.Where(x => ((long?)x["atMs"] ?? 0L) >= cutoff))
        {
            snapshot.RecentMatches.Add(new AtlasBoardProgressionMatchSummary {
                matchId = String(item,"matchId"), lobbyId = String(item,"lobbyId"),
                mapId = String(item,"mapId"), mode = "peer_attested_online",
                hostAccountId = String(item,"hostAccountId"),
                themeId = String(item,"themeId"), roundLimit = Int(item,"roundLimit",100),
                result = String(item,"result"),
                place = String(item,"result") == "loss" ? 2 : 1,
                playerCount = Int(item,"playerCount",4), localSlotIndex = Int(item,"slotIndex",3),
                propertyCount = Int(item,"properties",100), developmentLevels = Int(item,"developments",100),
                finalCash = Int(item,"cash",100000000), netWorth = Int(item,"netWorth",100000000),
                completedRounds = Int(item,"rounds",100), completedTurns = Int(item,"turns",10000),
                diceRolls = Int(item,"diceRolls",10000), doublesRolled = Int(item,"doublesRolled",10000),
                xpAwarded = Int(item,"xpAwarded",300), goldAwarded = Int(item,"goldAwarded",50),
                levelAfter = level,
                finishedAtEpochMs = (long?)item["atMs"] ?? 0
            });
        }
        snapshot.RecentMatches = snapshot.RecentMatches
            .OrderByDescending(x => x.finishedAtEpochMs).Take(10).ToList();
        ApplyClaimDisplayFields(snapshot, claims, uid);
        await RefreshRecentMatchFinalResultsAsync(snapshot, token);
        snapshot.TechnicalMessage = "peer_attested_not_server_verified";
        return snapshot;
    }
}