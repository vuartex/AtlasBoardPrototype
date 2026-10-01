#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase14F2StabilityInstaller
{
    private const string CoordinatorPath =
        "Assets/Project/Scripts/Online/" +
        "AtlasBoardTurnDiceNetworkCoordinator.cs";

    private const string LifecyclePath =
        "Backend/Firebase/functions/src/match/lifecycle.ts";

    private const string ProfileHeaderPath =
        "Assets/Project/Scripts/UI/Menu/" +
        "AtlasBoardCanonicalProfileHeader.cs";

    private const string RetryMarker =
        "IsTransientHostMigrationNotSafe";

    private const string NoSuccessorMarker =
        "voluntary_leave_no_successor";

    private const string QuietOfflineMarker =
        "expectedOfflineFailure";

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14F - Result & Exit/1 - Apply 14F.2 Stability Fix")]
    public static void Apply()
    {
        string[] required =
        {
            CoordinatorPath,
            LifecyclePath,
            ProfileHeaderPath
        };

        foreach (string path in required)
        {
            if (!File.Exists(path))
            {
                Debug.LogError(
                    $"Phase 14F.2 ABORTED: missing required source {path}");
                return;
            }
        }

        string coordinator =
            Normalize(
                File.ReadAllText(
                    CoordinatorPath));

        string lifecycle =
            Normalize(
                File.ReadAllText(
                    LifecyclePath));

        string profile =
            Normalize(
                File.ReadAllText(
                    ProfileHeaderPath));

        bool coordinatorChanged = false;
        bool backendChanged = false;
        bool profileChanged = false;

        try
        {
            if (!coordinator.Contains(
                    "private static void QuitApplicationNow()",
                    StringComparison.Ordinal))
            {
                coordinator =
                    PatchQuitRouting(
                        coordinator);

                coordinatorChanged =
                    true;
            }

            if (!coordinator.Contains(
                    RetryMarker,
                    StringComparison.Ordinal))
            {
                coordinator =
                    PatchMigrationRetry(
                        coordinator);

                coordinatorChanged =
                    true;
            }

            if (!lifecycle.Contains(
                    NoSuccessorMarker,
                    StringComparison.Ordinal))
            {
                lifecycle =
                    PatchNoSuccessorBackend(
                        lifecycle);

                backendChanged =
                    true;
            }

            if (!profile.Contains(
                    QuietOfflineMarker,
                    StringComparison.Ordinal))
            {
                profile =
                    PatchProfileOfflineNoise(
                        profile);

                profileChanged =
                    true;
            }
        }
        catch (InvalidOperationException exception)
        {
            Debug.LogError(
                "Phase 14F.2 ABORTED before writing: " +
                exception.Message);
            return;
        }

        string backupRoot =
            Path.Combine(
                "Library",
                "AtlasBoardPhase14F2Backup_" +
                DateTime.Now.ToString(
                    "yyyyMMdd_HHmmss"));

        Directory.CreateDirectory(
            backupRoot);

        if (coordinatorChanged)
        {
            Backup(
                CoordinatorPath,
                backupRoot);

            File.WriteAllText(
                CoordinatorPath,
                coordinator,
                new UTF8Encoding(false));
        }

        if (backendChanged)
        {
            Backup(
                LifecyclePath,
                backupRoot);

            File.WriteAllText(
                LifecyclePath,
                lifecycle,
                new UTF8Encoding(false));
        }

        if (profileChanged)
        {
            Backup(
                ProfileHeaderPath,
                backupRoot);

            File.WriteAllText(
                ProfileHeaderPath,
                profile,
                new UTF8Encoding(false));
        }

        AssetDatabase.Refresh();

        Debug.Log(
            "Phase 14F.2 Stability fix APPLIED. " +
            $"CoordinatorChanged={coordinatorChanged}, " +
            $"BackendChanged={backendChanged}, " +
            $"ProfileHeaderChanged={profileChanged}. " +
            $"Backup={backupRoot}. " +
            (backendChanged
                ? "Backend changed: run npm run build and restart the Firebase emulator."
                : "Backend already contained the no-successor fix; no Functions rebuild is required for this 14F.2 pass."));
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14F - Result & Exit/2 - Validate 14F.2 Stability Fix")]
    public static void Validate()
    {
        string coordinator =
            File.Exists(CoordinatorPath)
                ? File.ReadAllText(
                    CoordinatorPath)
                : string.Empty;

        string lifecycle =
            File.Exists(LifecyclePath)
                ? File.ReadAllText(
                    LifecyclePath)
                : string.Empty;

        string profile =
            File.Exists(ProfileHeaderPath)
                ? File.ReadAllText(
                    ProfileHeaderPath)
                : string.Empty;

        bool quitReady =
            coordinator.Contains(
                "private static void QuitApplicationNow()",
                StringComparison.Ordinal) &&
            coordinator.Contains(
                "UnityEditor.EditorApplication.isPlaying = false;",
                StringComparison.Ordinal);

        bool retryReady =
            coordinator.Contains(
                RetryMarker,
                StringComparison.Ordinal) &&
            coordinator.Contains(
                "HOST_MIGRATION_NOT_SAFE_YET",
                StringComparison.Ordinal) &&
            coordinator.Contains(
                "lastPublishedFrameJson = string.Empty;",
                StringComparison.Ordinal);

        bool backendReady =
            lifecycle.Contains(
                NoSuccessorMarker,
                StringComparison.Ordinal);

        bool profileReady =
            profile.Contains(
                QuietOfflineMarker,
                StringComparison.Ordinal);

        if (!quitReady ||
            !retryReady ||
            !backendReady ||
            !profileReady)
        {
            Debug.LogError(
                "Phase 14F.2 validation FAILED. " +
                $"QuitReady={quitReady}, " +
                $"RetryReady={retryReady}, " +
                $"BackendReady={backendReady}, " +
                $"ProfileQuietOfflineReady={profileReady}.");
            return;
        }

        Debug.Log(
            "Phase 14F.2 stability validation PASS. " +
            "Host leave retries transient unsafe backend snapshots, " +
            "no-successor Host leave is supported, Editor/build quit routing is correct, " +
            "and expected Phase 14C offline profile failures are quiet/rate-limited.");
    }

    private static string PatchMigrationRetry(
        string source)
    {
        const string oldBlock =
@"                if (!result.Success)
                {
                    Debug.LogWarning(
                        ""AtlasBoard Phase 11G Host Migration request failed: "" +
                        result.TechnicalMessage,
                        this);
                    return;
                }
";

        const string newBlock =
@"                if (!result.Success)
                {
                    if (IsTransientHostMigrationNotSafe(
                            result))
                    {
                        // Local state already reached a safe checkpoint, but
                        // the server can still be one snapshot behind.
                        // Force the exact safe frame to publish again, then
                        // retry instead of abandoning the user's Leave request.
                        lastPublishedFrameJson =
                            string.Empty;

                        nextPublishCheckAt = 0f;

                        await Task.Delay(
                            150);

                        continue;
                    }

                    Debug.LogWarning(
                        ""AtlasBoard Phase 11G Host Migration request failed: "" +
                        result.TechnicalMessage,
                        this);
                    return;
                }
";

        source =
            ReplaceExactlyOnce(
                source,
                oldBlock,
                newBlock,
                "Host Migration transient retry block");

        const string insertionMarker =
            "    private bool IsSafeHostMigrationCheckpoint()\n";

        int index =
            source.IndexOf(
                insertionMarker,
                StringComparison.Ordinal);

        if (index < 0)
        {
            throw new InvalidOperationException(
                "Could not locate IsSafeHostMigrationCheckpoint.");
        }

        const string helper =
@"    private static bool IsTransientHostMigrationNotSafe(
        AtlasMatchNetworkResult result)
    {
        if (result == null)
        {
            return false;
        }

        if (string.Equals(
                result.ErrorLocalizationKey,
                ""match.error.host_migration_not_safe"",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(
                   result.TechnicalMessage) &&
               result.TechnicalMessage.IndexOf(
                   ""HOST_MIGRATION_NOT_SAFE_YET"",
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

";

        return
            source.Substring(
                0,
                index) +
            helper +
            source.Substring(
                index);
    }

    private static string PatchQuitRouting(
        string source)
    {
        int count =
            CountOccurrences(
                source,
                "Application.Quit();");

        if (count != 2)
        {
            throw new InvalidOperationException(
                "Quit routing expected exactly 2 Application.Quit() calls " +
                $"before patch; found {count}.");
        }

        source =
            source.Replace(
                "Application.Quit();",
                "QuitApplicationNow();");

        const string insertionMarker =
            "    private void DetachLocalClientFromActiveMatch()\n";

        int index =
            source.IndexOf(
                insertionMarker,
                StringComparison.Ordinal);

        if (index < 0)
        {
            throw new InvalidOperationException(
                "Could not locate DetachLocalClientFromActiveMatch for quit helper.");
        }

        const string helper =
@"    private static void QuitApplicationNow()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

";

        return
            source.Substring(
                0,
                index) +
            helper +
            source.Substring(
                index);
    }

    private static string PatchProfileOfflineNoise(
        string source)
    {
        const string oldBlock =
@"        catch (Exception exception)
        {
            Debug.LogWarning(
                ""Phase 14C could not load the canonical profile "" +
                ""identity yet. Existing header presentation was kept. "" +
                exception.Message,
                this);

            nextCheckAt =
                Time.unscaledTime +
                RetryAfterFailureSeconds;
        }
";

        const string newBlock =
@"        catch (Exception exception)
        {
            string failureMessage =
                exception != null
                    ? exception.Message ?? string.Empty
                    : string.Empty;

            bool expectedOfflineFailure =
                failureMessage.IndexOf(
                    ""client is offline"",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                failureMessage.IndexOf(
                    ""network is unreachable"",
                    StringComparison.OrdinalIgnoreCase) >= 0;

            // Firebase being temporarily offline is expected during local
            // development/emulator transitions. Keep the existing header and
            // retry quietly instead of spamming the Console every few seconds.
            if (!expectedOfflineFailure)
            {
                Debug.LogWarning(
                    ""Phase 14C could not load the canonical profile "" +
                    ""identity yet. Existing header presentation was kept. "" +
                    failureMessage,
                    this);
            }

            nextCheckAt =
                Time.unscaledTime +
                (expectedOfflineFailure
                    ? 20f
                    : RetryAfterFailureSeconds);
        }
";

        return ReplaceExactlyOnce(
            source,
            oldBlock,
            newBlock,
            "Phase 14C expected-offline warning suppression");
    }

    private static string PatchNoSuccessorBackend(
        string source)
    {
        const string oldBlock =
@"    const candidate = candidates[0];
    if (!candidate) {
      throw new HttpsError(
        ""failed-precondition"",
        ""HOST_MIGRATION_NO_CANDIDATE"",
        {errorKey: ""match.error.host_migration_no_candidate""},
      );
    }

    const candidateData = candidate.data() ?? {};
    const nextHostAccountId =
      typeof candidateData.accountId === ""string"" ?
        candidateData.accountId :
        """";

    if (!nextHostAccountId) {
      throw new HttpsError(
        ""failed-precondition"",
        ""HOST_MIGRATION_NO_CANDIDATE"",
        {errorKey: ""match.error.host_migration_no_candidate""},
      );
    }

    const oldHostSeat = seats.find((seat) => {
      if (!seat.exists) return false;
      const data = seat.data() ?? {};
      const accountId =
        typeof data.accountId === ""string"" ? data.accountId : """";
      const localOwnerAccountId =
        typeof data.localOwnerAccountId === ""string"" ?
          data.localOwnerAccountId :
          """";
      return data.isHost === true &&
        (accountId === uid || localOwnerAccountId === uid);
    }) ?? seats.find((seat) => {
      const data = seat.data() ?? {};
      return data.accountId === uid;
    });

    if (!oldHostSeat) {
      throw new HttpsError(
        ""failed-precondition"",
        ""MATCH_SEAT_REQUIRED"",
        {errorKey: ""match.error.seat_required""},
      );
    }
";

        const string newBlock =
@"    const oldHostSeat = seats.find((seat) => {
      if (!seat.exists) return false;
      const data = seat.data() ?? {};
      const accountId =
        typeof data.accountId === ""string"" ? data.accountId : """";
      const localOwnerAccountId =
        typeof data.localOwnerAccountId === ""string"" ?
          data.localOwnerAccountId :
          """";
      return data.isHost === true &&
        (accountId === uid || localOwnerAccountId === uid);
    }) ?? seats.find((seat) => {
      const data = seat.data() ?? {};
      return data.accountId === uid;
    });

    if (!oldHostSeat) {
      throw new HttpsError(
        ""failed-precondition"",
        ""MATCH_SEAT_REQUIRED"",
        {errorKey: ""match.error.seat_required""},
      );
    }

    const candidate = candidates[0];

    if (!candidate) {
      const serverTimestamp = FieldValue.serverTimestamp();
      const oldHostData = oldHostSeat.data() ?? {};
      const oldHostSlotIndex =
        typeof oldHostData.slotIndex === ""number"" ?
          oldHostData.slotIndex :
          -1;
      const lobby = lobbySnap.data() ?? {};
      const codeHash =
        typeof lobby.joinCodeHash === ""string"" ?
          lobby.joinCodeHash :
          """";

      transaction.set(
        matchRef,
        {
          hostAccountId: """",
          hostHeartbeatAtEpochMs: 0,
          status: ""complete"",
          lastHostMigrationReason: ""voluntary_leave_no_successor"",
          lastHostMigrationAtEpochMs: now,
          updatedAt: serverTimestamp,
        },
        {merge: true},
      );

      transaction.set(
        stateRef,
        {
          phase: ""match_complete"",
          authorityHostAccountId: """",
          authorityHandoffReason: ""voluntary_leave_no_successor"",
          authorityHandoffAtEpochMs: now,
          updatedAt: serverTimestamp,
        },
        {merge: true},
      );

      transaction.set(
        lobbyRef,
        {
          lifecycleState: ""closed"",
          matchId: """",
          hostAccountId: """",
          hostHeartbeatAtEpochMs: 0,
          updatedAt: serverTimestamp,
        },
        {merge: true},
      );

      transaction.set(
        oldHostSeat.ref,
        {
          isHost: false,
          localOwnerAccountId: """",
          controllerKind: ""permanent_bot"",
          connectionState: ""HOST_LEFT_NO_SUCCESSOR"",
          reconnectExpiresAtEpochMs: 0,
          afkLockedOut: true,
          removalReason: ""voluntary_leave_no_successor"",
          updatedAt: serverTimestamp,
        },
        {merge: true},
      );

      if (
        oldHostSlotIndex >= 0 &&
        oldHostSlotIndex < MAX_PLAYERS
      ) {
        transaction.set(
          lobbyRef
            .collection(""members"")
            .doc(`seat_${oldHostSlotIndex + 1}`),
          {
            seatMode: ""bot"",
            seatType: ""bot"",
            accountId: """",
            localOwnerAccountId: """",
            isHost: false,
            controllerKind: ""bot"",
            connectionState: ""HOST_LEFT_NO_SUCCESSOR"",
            readyForRevision: 0,
            updatedAt: serverTimestamp,
          },
          {merge: true},
        );
      }

      if (codeHash) {
        transaction.set(
          db.collection(""join_codes"").doc(codeHash),
          {
            active: false,
            lookupActive: false,
            joinOpen: false,
            matchId: """",
            lifecycleState: ""closed"",
            updatedAt: serverTimestamp,
          },
          {merge: true},
        );
      }

      transaction.delete(
        db.collection(""lobby_discovery"").doc(lobbyId),
      );

      return;
    }

    const candidateData = candidate.data() ?? {};
    const nextHostAccountId =
      typeof candidateData.accountId === ""string"" ?
        candidateData.accountId :
        """";

    if (!nextHostAccountId) {
      throw new HttpsError(
        ""failed-precondition"",
        ""HOST_MIGRATION_NO_CANDIDATE"",
        {errorKey: ""match.error.host_migration_no_candidate""},
      );
    }
";

        return ReplaceExactlyOnce(
            source,
            oldBlock,
            newBlock,
            "Host no-successor backend fallback");
    }

    private static string ReplaceExactlyOnce(
        string source,
        string oldValue,
        string newValue,
        string label)
    {
        int first =
            source.IndexOf(
                oldValue,
                StringComparison.Ordinal);

        if (first < 0)
        {
            throw new InvalidOperationException(
                $"Could not find expected source block for {label}.");
        }

        int second =
            source.IndexOf(
                oldValue,
                first +
                oldValue.Length,
                StringComparison.Ordinal);

        if (second >= 0)
        {
            throw new InvalidOperationException(
                $"Expected one source block for {label}, found multiple.");
        }

        return
            source.Substring(
                0,
                first) +
            newValue +
            source.Substring(
                first +
                oldValue.Length);
    }

    private static int CountOccurrences(
        string source,
        string value)
    {
        int count = 0;
        int index = 0;

        while (true)
        {
            index =
                source.IndexOf(
                    value,
                    index,
                    StringComparison.Ordinal);

            if (index < 0)
            {
                return count;
            }

            count++;
            index +=
                value.Length;
        }
    }

    private static void Backup(
        string sourcePath,
        string backupRoot)
    {
        string safeName =
            sourcePath
                .Replace(
                    '/',
                    '_')
                .Replace(
                    '\\',
                    '_');

        File.Copy(
            sourcePath,
            Path.Combine(
                backupRoot,
                safeName),
            overwrite: true);
    }

    private static string Normalize(
        string source)
    {
        return
            (source ?? string.Empty)
                .Replace(
                    "\r\n",
                    "\n")
                .Replace(
                    "\r",
                    "\n");
    }
}
#endif
