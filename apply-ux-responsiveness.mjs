import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const root = process.cwd();
const mode = process.argv[2] === '--check' ? '--check' :
             process.argv[2] === '--apply' ? '--apply' : null;
if (!mode) {
  console.error('Usage: node apply-ux-responsiveness.mjs --check | --apply');
  process.exit(2);
}

const files = [
  {
    "path": "Assets/Project/Scripts/Online/Firebase/AtlasBoardMatchRuntimeBridge.cs",
    "edits": [
      {
        "old": "        if (string.IsNullOrWhiteSpace(matchId))\n        {\n            return;\n        }\n",
        "replacement": "        // The lobby may still carry its previous MatchId after this client\n        // has locally detached. Never poll an inactive match or revive its UI.\n        if (string.IsNullOrWhiteSpace(matchId) ||\n            string.IsNullOrWhiteSpace(expectedMatchId) ||\n            !string.Equals(matchId, expectedMatchId, StringComparison.Ordinal))\n        {\n            return;\n        }\n"
      },
      {
        "old": "        if (!string.IsNullOrWhiteSpace(expectedMatchId))\n        {\n            if (!string.Equals(\n                    expectedMatchId,\n                    snapshot.MatchId,\n                    StringComparison.Ordinal))\n            {\n                return;\n            }\n        }\n        else\n        {\n            // ResetForMatchSession(\"\") intentionally means there is no\n            // active match. An HTTP poll started before that reset may finish\n            // later; never let its old snapshot resurrect dice/HUD/result or\n            // controller state in the lobby or the next match.\n            string lobbyMatchId = CurrentMatchId;\n\n            if (string.IsNullOrWhiteSpace(lobbyMatchId) ||\n                !string.Equals(\n                    lobbyMatchId,\n                    snapshot.MatchId,\n                    StringComparison.Ordinal))\n            {\n                return;\n            }\n        }\n",
        "replacement": "        // A response from a session that has already been left must never\n        // resurrect the match just because the lobby has a stale MatchId.\n        if (string.IsNullOrWhiteSpace(expectedMatchId) ||\n            !string.Equals(\n                expectedMatchId,\n                snapshot.MatchId,\n                StringComparison.Ordinal))\n        {\n            return;\n        }\n"
      }
    ]
  },
  {
    "path": "Assets/Project/Scripts/UI/Menu/AtlasBoardPublicLobbyBrowserController.cs",
    "edits": [
      {
        "old": "    private bool busy;\n    private bool hooked;\n",
        "replacement": "    private bool busy;\n    private bool refreshingRooms;\n    private int browserGeneration;\n    private bool hooked;\n"
      },
      {
        "old": "        RebuildFilterLabels();\n        ClearRows();\n        SetText(statusText, T(\"public_browser.loading\", \"Loading public rooms...\"));\n        _ = RefreshRoomsAsync();\n",
        "replacement": "        RebuildFilterLabels();\n        // Show cached results immediately on a repeat visit. Refresh without\n        // making the whole browser a blocking loading screen.\n        if (allRooms.Count > 0)\n        {\n            ApplyFilters();\n        }\n        else\n        {\n            ClearRows();\n            SetText(statusText, T(\"public_browser.loading\", \"Loading public rooms...\"));\n        }\n        _ = RefreshRoomsAsync();\n"
      },
      {
        "old": "    public void BackToMainMenu()\n    {\n        if (busy)\n        {\n            return;\n        }\n\n        SetActive(passwordPromptRoot, false);\n",
        "replacement": "    public void BackToMainMenu()\n    {\n        // A room creation/join operation must not be orphaned, but an ordinary\n        // read-only refresh must NEVER imprison the player on this screen.\n        if (busy && !refreshingRooms)\n        {\n            return;\n        }\n\n        browserGeneration++;\n        SetActive(passwordPromptRoot, false);\n"
      },
      {
        "old": "    private async Task RefreshRoomsAsync()\n    {\n        if (busy)\n        {\n            return;\n        }\n\n        busy = true;\n        SetControlsInteractable(false);\n\n        try\n        {\n            SetText(statusText, T(\"public_browser.loading\", \"Loading public rooms...\"));\n\n            AtlasPublicLobbyListResult result =\n                await runtimeBridge.ListPublicRoomsAsync(20);\n\n            if (!result.Success)\n",
        "replacement": "    private async Task RefreshRoomsAsync()\n    {\n        if (busy)\n        {\n            return;\n        }\n\n        int requestedGeneration = browserGeneration;\n        busy = true;\n        refreshingRooms = true;\n        SetControlsInteractable(false);\n\n        try\n        {\n            if (allRooms.Count == 0)\n            {\n                SetText(statusText, T(\"public_browser.loading\", \"Loading public rooms...\"));\n            }\n\n            AtlasPublicLobbyListResult result =\n                await runtimeBridge.ListPublicRoomsAsync(20);\n\n            // Ignore a response that arrived after the player went Back.\n            if (requestedGeneration != browserGeneration ||\n                browserRoot == null || !browserRoot.activeSelf)\n            {\n                return;\n            }\n\n            if (!result.Success)\n"
      },
      {
        "old": "            ApplyFilters();\n        }\n        finally\n        {\n            busy = false;\n            SetControlsInteractable(true);\n        }\n",
        "replacement": "            ApplyFilters();\n        }\n        finally\n        {\n            refreshingRooms = false;\n            busy = false;\n            SetControlsInteractable(true);\n        }\n"
      },
      {
        "old": "        if (backButton != null) backButton.interactable = interactable;\n",
        "replacement": "        if (backButton != null) backButton.interactable = interactable || refreshingRooms;\n"
      }
    ]
  },
  {
    "path": "Assets/Project/Scripts/Online/AtlasBoardTurnDiceNetworkCoordinator.cs",
    "edits": [
      {
        "old": "    public bool TryHandleLeaveMatch()\n    {\n        if (!prepared || leaveRequestInFlight)\n        {\n            return false;\n        }\n",
        "replacement": "    public bool TryHandleLeaveMatch()\n    {\n        if (!prepared)\n        {\n            return false;\n        }\n\n        // Repeated clicks during an accepted leave are already handled.\n        if (leaveRequestInFlight || hostMigrationQueued)\n        {\n            return true;\n        }\n"
      },
      {
        "old": "            while (prepared &&\n                   localIsHost)\n            {\n                if (!IsSafeHostMigrationCheckpoint() ||\n",
        "replacement": "            // Graceful host transfer gets a short real-time window. If an\n            // animation/decision never reaches a safe checkpoint, the host\n            // must still be able to leave; server recovery then applies.\n            float migrationDeadline = Time.realtimeSinceStartup + 5f;\n            while (prepared &&\n                   localIsHost)\n            {\n                if (Time.realtimeSinceStartup >= migrationDeadline)\n                {\n                    Debug.LogWarning(\n                        \"AtlasBoard host migration timed out. Leaving locally; \" +\n                        \"remote seats will use the existing disconnect/recovery policy.\",\n                        this);\n                    bool quit = hostMigrationQuitAfterHandoff;\n                    DetachLocalClientFromActiveMatch();\n                    if (quit)\n                    {\n                        QuitApplicationNow();\n                    }\n                    return;\n                }\n\n                if (!IsSafeHostMigrationCheckpoint() ||\n"
      },
      {
        "old": "            Debug.LogWarning(\n                \"AtlasBoard Host no-successor leave request failed: \" +\n                result.TechnicalMessage,\n                this);\n\n            return true;\n",
        "replacement": "            Debug.LogWarning(\n                \"AtlasBoard Host no-successor leave request failed: \" +\n                result.TechnicalMessage +\n                \". Returning to menu locally; server session will require recovery.\",\n                this);\n\n            // No connected remote human depends on this host. An error must\n            // not leave the sole human indefinitely stuck in the match.\n            bool quitAfterFailure = hostMigrationQuitAfterHandoff;\n            DetachLocalClientFromActiveMatch();\n            if (quitAfterFailure)\n            {\n                QuitApplicationNow();\n            }\n            return true;\n"
      },
      {
        "old": "    private async void LeaveActiveMatchAndReturnToMenuAsync()\n    {\n        if (leaveRequestInFlight || matchBridge == null)\n        {\n            return;\n        }\n\n        leaveRequestInFlight = true;\n\n        try\n        {\n            AtlasMatchNetworkResult result =\n                await matchBridge.LeaveActiveMatchAsync();\n\n            if (!result.Success)\n            {\n                Debug.LogWarning(\n                    \"Phase 5F Leave Match request failed: \" +\n                    result.TechnicalMessage,\n                    this);\n                return;\n            }\n\n            DetachLocalClientFromActiveMatch();\n        }\n        finally\n        {\n            leaveRequestInFlight = false;\n        }\n    }\n",
        "replacement": "    private async void LeaveActiveMatchAndReturnToMenuAsync()\n    {\n        if (leaveRequestInFlight || matchBridge == null)\n        {\n            return;\n        }\n\n        leaveRequestInFlight = true;\n        AtlasBoardMatchRuntimeBridge departingBridge = matchBridge;\n        string departingMatchId = preparedMatchId;\n\n        try\n        {\n            // Start the authoritative server cleanup, then return to the\n            // local menu without waiting for networking, retries or timeouts.\n            Task<AtlasMatchNetworkResult> leave =\n                departingBridge.LeaveActiveMatchAsync(departingMatchId);\n            DetachLocalClientFromActiveMatch();\n\n            AtlasMatchNetworkResult result = await leave;\n            if (!result.Success)\n            {\n                Debug.LogWarning(\n                    \"AtlasBoard remote seat leave was not acknowledged: \" +\n                    result.TechnicalMessage +\n                    \". The server disconnect/reconnect policy remains responsible for cleanup.\",\n                    this);\n            }\n        }\n        catch (Exception exception)\n        {\n            Debug.LogWarning(\n                \"AtlasBoard remote seat leave request failed after local exit: \" +\n                exception.Message,\n                this);\n        }\n        finally\n        {\n            leaveRequestInFlight = false;\n        }\n    }\n"
      },
      {
        "old": "            if (!HasOtherConnectedRemoteHumanSeat())\n            {\n                bool handled =\n                    await TryFastCloseHostSessionWithoutSuccessorAsync();\n\n                if (handled)\n                {\n                    return;\n                }\n",
        "replacement": "            if (!HasOtherConnectedRemoteHumanSeat())\n            {\n                Task<bool> closeAttempt =\n                    TryFastCloseHostSessionWithoutSuccessorAsync();\n                Task finished = await Task.WhenAny(\n                    closeAttempt, Task.Delay(2000));\n                if (finished != closeAttempt)\n                {\n                    // Backend is unavailable/slow. A bot-only host must not\n                    // be trapped while a background request times out.\n                    Debug.LogWarning(\n                        \"AtlasBoard bot-only host leave exceeded 2 seconds. \" +\n                        \"Detaching locally; backend recovery is still required.\",\n                        this);\n                    bool quitOnTimeout = hostMigrationQuitAfterHandoff;\n                    DetachLocalClientFromActiveMatch();\n                    if (quitOnTimeout) QuitApplicationNow();\n                    return;\n                }\n                bool handled = await closeAttempt;\n\n                if (handled)\n                {\n                    return;\n                }\n"
      },
      {
        "old": "        AtlasMatchNetworkResult result =\n            await matchBridge\n                .LeaveActiveMatchAsync();\n\n        if (!result.Success)\n",
        "replacement": "        AtlasMatchNetworkResult result =\n            await matchBridge\n                .LeaveActiveMatchAsync();\n\n        // An earlier two-second timeout may already have returned this host\n        // to the menu; do not detach or quit a second time.\n        if (!prepared)\n        {\n            return true;\n        }\n\n        if (!result.Success)\n"
      },
      {
        "old": "    private async void LeaveActiveMatchAndQuitAsync()\n    {\n        if (leaveRequestInFlight || matchBridge == null)\n        {\n            return;\n        }\n\n        leaveRequestInFlight = true;\n\n        try\n        {\n            await matchBridge.LeaveActiveMatchAsync();\n        }\n        finally\n        {\n            leaveRequestInFlight = false;\n            QuitApplicationNow();\n        }\n    }\n",
        "replacement": "    private async void LeaveActiveMatchAndQuitAsync()\n    {\n        if (leaveRequestInFlight || matchBridge == null)\n        {\n            return;\n        }\n\n        leaveRequestInFlight = true;\n\n        try\n        {\n            // Best-effort server notification. Never require a successful\n            // network round-trip before the player may close the application.\n            Task<AtlasMatchNetworkResult> leave =\n                matchBridge.LeaveActiveMatchAsync(preparedMatchId);\n            await Task.WhenAny(leave, Task.Delay(1500));\n        }\n        catch (Exception exception)\n        {\n            Debug.LogWarning(\n                \"AtlasBoard quit leave notification failed: \" +\n                exception.Message, this);\n        }\n        finally\n        {\n            leaveRequestInFlight = false;\n            QuitApplicationNow();\n        }\n    }\n"
      }
    ]
  },
  {
    "path": "Assets/Project/Scripts/Online/Firebase/AtlasBoardLobbyRuntimeBridge.cs",
    "edits": [
      {
        "old": "    public async Task<AtlasPublicLobbyListResult>\n        ListPublicRoomsAsync(\n            int limit = 6)\n    {\n        AtlasLobbyOperationResult init =\n            await EnsureIdentityAsync();\n",
        "replacement": "    public async Task<AtlasPublicLobbyListResult>\n        ListPublicRoomsAsync(\n            int limit = 6)\n    {\n#if UNITY_EDITOR || DEVELOPMENT_BUILD\n        float listStartedAt = Time.realtimeSinceStartup;\n#endif\n        AtlasLobbyOperationResult init =\n            await EnsureIdentityAsync();\n#if UNITY_EDITOR || DEVELOPMENT_BUILD\n        float identityMilliseconds =\n            (Time.realtimeSinceStartup - listStartedAt) * 1000f;\n#endif\n"
      },
      {
        "old": "        webRequest.timeout = 20;\n\n        await AtlasBoardAppCheckRequestSecurity.ApplyAsync(\n            webRequest);\n        await SendRequestAsync(webRequest);\n\n        string body =\n",
        "replacement": "        webRequest.timeout = 20;\n\n        await AtlasBoardAppCheckRequestSecurity.ApplyAsync(\n            webRequest);\n        await SendRequestAsync(webRequest);\n#if UNITY_EDITOR || DEVELOPMENT_BUILD\n        Debug.Log(\n            $\"AtlasBoard lobby list timing: identity={identityMilliseconds:F0}ms, \" +\n            $\"total={(Time.realtimeSinceStartup - listStartedAt) * 1000f:F0}ms, \" +\n            $\"HTTP={webRequest.responseCode}.\", this);\n#endif\n\n        string body =\n"
      }
    ]
  }
];
const pending = [];
let errors = 0;
for (const item of files) {
  const location = path.join(root, ...item.path.split('/'));
  if (!fs.existsSync(location)) {
    console.error('MISSING', item.path);
    errors++;
    continue;
  }
  const buf = fs.readFileSync(location);
  const hasBom = buf.length >= 3 && buf[0] === 0xef && buf[1] === 0xbb && buf[2] === 0xbf;
  const input = buf.toString('utf8');
  const normalized = input.replace(/^\uFEFF/, '').replace(/\r\n/g, '\n');
  let result = normalized;
  for (const {old, replacement} of item.edits) {
    const first = result.indexOf(old);
    if (first < 0) {
      if (result.includes(replacement)) {
        continue; // already applied
      }
      console.error('SOURCE MISMATCH', item.path, 'at anchor:', JSON.stringify(old.slice(0,110)));
      errors++;
      continue;
    }
    if (result.indexOf(old, first+1) !== -1) {
      console.error('AMBIGUOUS SOURCE', item.path, 'at anchor:', JSON.stringify(old.slice(0,110)));
      errors++;
      continue;
    }
    result = result.replace(old, replacement);
  }
  const eol = normalized !== input.replace(/^\uFEFF/, '') ? '\r\n' : '\n';
  const output = (hasBom ? '\uFEFF' : '') + result.replace(/\n/g, eol);
  pending.push({location, item, before:buf, after:output});
  console.log((output === input ? 'ALREADY' : 'READY') + ' ' + item.path);
}
if (errors) {
  console.error('ABORT: '+errors+' errors, no project file modified.');
  process.exit(1);
}
if (mode === '--check') {
  console.log('AtlasBoard UX preflight PASS ('+pending.length+' files).');
  process.exit(0);
}
const backupDir = fs.mkdtempSync(path.join(os.tmpdir(), 'atlasboard-ux-backup-'));
const applied = [];
try {
  for (const [i,file] of pending.entries()) {
    fs.writeFileSync(path.join(backupDir, String(i)+'.bak'), file.before);
    fs.writeFileSync(file.location, file.after);
    applied.push(file);
    console.log('UPDATED '+file.item.path);
  }
} catch (error) {
  console.error('ERROR; RESTORING backups:',error.message);
  for (const f of applied.reverse()) fs.writeFileSync(f.location, f.before);
  process.exit(1);
}
console.log('AtlasBoard UX fix APPLIED. Backup files in: '+backupDir);
console.log('No commits, pushes or backend changes.');
