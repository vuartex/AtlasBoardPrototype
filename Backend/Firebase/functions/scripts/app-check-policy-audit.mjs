import fs from "node:fs";
import path from "node:path";
import {fileURLToPath} from "node:url";

const scriptDir =
  path.dirname(
    fileURLToPath(import.meta.url),
  );

const functionsDir =
  path.resolve(
    scriptDir,
    "..",
  );

const sourceFiles = [
  "src/index.ts",
  "src/lobby/public-lobby-pagination-callable.ts",
  "src/chat/chat.ts",
  "src/platform/steam-link.ts",
  "src/platform/app-check-steam.ts",
];

const protectedCallables =
  new Set(["economyHealthCheck", "metaDailyRewardClaim", "metaSeasonalGetActive", "metaSeasonalGetOverview", "metaSeasonalClaimChallenge", "metaSeasonalClaimTrackTier", "metaSeasonalPurchaseLimitedItem", "progressionGetProfile", "progressionRecordCompletedMatch", "lobbyCreatePrivateRoom", "lobbyCreatePublicRoom", "lobbyListPublicRooms", "lobbyListPublicRoomsPage", "lobbyJoinPublicRoom", "lobbyJoinByCode", "lobbyUpdatePassword", "lobbyCloseRoom", "lobbyHostHeartbeat", "lobbyUpdateSettings", "lobbyConfigureSeats", "lobbySetPawnCosmetic", "lobbySetReady", "lobbyKickMember", "lobbyLeaveRoom", "lobbyStartMatch", "lobbyGetSnapshot", "matchGetSnapshot", "matchTouchPresence", "matchHostSweepDisconnected", "matchTryRecoverCrashedHost", "matchLeaveActive", "matchHostMarkAfkRemoved", "matchHostExpireReconnects", "matchHostPrepareRematch", "matchSubmitIntent", "matchHostListPendingIntents", "matchHostPublishState", "matchHostAcknowledgeIntents", "lobbyChatSend", "lobbyChatList", "matchChatSend", "matchChatList", "platformSteamGetLinkStatus", "platformSteamLinkCurrentAccount", "platformSteamReturningSignIn"]);

const devExemptCallables =
  new Set(["walletTestMutation", "inventoryTestMutation", "commerceTestPurchase", "commerceTestRefund", "promoTestRedeem", "metaDevResetPurchasedEntitlements", "lobbyDevEnsureAccount", "platformSteamDevLinkCurrentAccount", "platformSteamDevReturningSignIn"]);

const bootstrapExemptCallables =
  new Set(["platformSteamCreateAppCheckToken"]);

function fail(message) {
  console.error(
    "Phase 15F policy audit FAIL: " +
    message,
  );
  process.exit(1);
}

function read(relativePath) {
  return fs.readFileSync(
    path.join(
      functionsDir,
      relativePath,
    ),
    "utf8",
  );
}

function extractCallables(source) {
  const pattern =
    /export const\s+([A-Za-z0-9_]+)\s*=\s*onCall\(\s*\{([\s\S]*?)\}\s*,/g;

  const callables = [];
  let match;

  while ((match = pattern.exec(source)) !== null) {
    const enforcement =
      /enforceAppCheck:\s*([^,\n]+)/.exec(
        match[2],
      );

    callables.push({
      name: match[1],
      enforcement:
        enforcement ?
          enforcement[1].trim() :
          "<missing>",
    });
  }

  return callables;
}

const policySource =
  read(
    "src/security/app-check-policy.ts",
  );

if (
  policySource.includes(
    "defineBoolean",
  ) ||
  !policySource.includes(
    'process.env.FUNCTIONS_EMULATOR === "true"',
  ) ||
  !policySource.includes(
    "process.env.ATLAS_ENFORCE_APP_CHECK",
  ) ||
  !policySource.includes(
    "isFunctionsEmulator &&",
  )
) {
  fail(
    "local emulator-only plain-boolean enforcement policy is missing.",
  );
}

const discovered =
  new Map();

for (const relativePath of sourceFiles) {
  for (const callable of extractCallables(
    read(relativePath),
  )) {
    if (discovered.has(
        callable.name)) {
      fail(
        "duplicate callable export " +
        callable.name,
      );
    }

    discovered.set(
      callable.name,
      {
        ...callable,
        file:
          relativePath,
      },
    );
  }
}

if (discovered.size !== 55) {
  fail(
    "callable inventory drift. Expected 55, found " +
    discovered.size +
    ".",
  );
}

for (const [name, info] of discovered) {
  const memberships =
    Number(
      protectedCallables.has(name),
    ) +
    Number(
      devExemptCallables.has(name),
    ) +
    Number(
      bootstrapExemptCallables.has(name),
    );

  if (memberships !== 1) {
    fail(
      name +
      " must belong to exactly one App Check class.",
    );
  }

  if (
    protectedCallables.has(name) &&
    info.enforcement !==
      "ATLAS_ENFORCE_APP_CHECK"
  ) {
    fail(
      name +
      " must use ATLAS_ENFORCE_APP_CHECK, found " +
      info.enforcement +
      ".",
    );
  }

  if (
    (
      devExemptCallables.has(name) ||
      bootstrapExemptCallables.has(name)
    ) &&
    info.enforcement !==
      "false"
  ) {
    fail(
      name +
      " must remain enforceAppCheck:false, found " +
      info.enforcement +
      ".",
    );
  }
}

console.log(
  "Phase 15F App Check policy audit PASS. " +
  "Protected=45, DevExempt=9, BootstrapExempt=1, Total=55. " +
  "Enforcement source=Functions Emulator .env.local only; production remains OFF.",
);
