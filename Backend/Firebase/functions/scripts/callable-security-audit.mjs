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
  "src/chat/chat.ts",
  "src/platform/steam-link.ts",
  "src/platform/app-check-steam.ts",
];

const authExempt =
  new Set([
    "platformSteamReturningSignIn",
    "platformSteamDevReturningSignIn",
    "platformSteamCreateAppCheckToken",
  ]);

function read(relativePath) {
  return fs.readFileSync(
    path.join(
      functionsDir,
      relativePath,
    ),
    "utf8",
  );
}

function fail(message) {
  console.error(
    "Phase 15G callable security audit FAIL: " +
    message,
  );

  process.exit(1);
}

function callableSegments(source) {
  const pattern =
    /export const\s+([A-Za-z0-9_]+)\s*=\s*onCall\(/g;

  const starts = [];
  let match;

  while ((match = pattern.exec(source)) !== null) {
    starts.push({
      name:
        match[1],
      index:
        match.index,
    });
  }

  return starts.map(
    (item, index) => {
      const end =
        index + 1 < starts.length ?
          starts[index + 1].index :
          source.length;

      return {
        name:
          item.name,
        text:
          source.slice(
            item.index,
            end,
          ),
      };
    },
  );
}

const discovered =
  new Map();

for (const relativePath of sourceFiles) {
  const source =
    read(relativePath);

  for (const callable of callableSegments(source)) {
    if (discovered.has(callable.name)) {
      fail(
        "duplicate callable " +
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

if (discovered.size !== 54) {
  fail(
    "expected 54 callables, found " +
    discovered.size +
    ".",
  );
}

let authRequiredCount =
  0;

for (const [name, info] of discovered) {
  if (authExempt.has(name)) {
    continue;
  }

  authRequiredCount++;

  const hasAuthGuard =
    info.text.includes(
      "requireAuthenticatedUid(request)",
    ) ||
    info.text.includes(
      "requireUid(request)",
    );

  if (!hasAuthGuard) {
    fail(
      name +
      " has no recognized Firebase Auth guard in " +
      info.file +
      ".",
    );
  }
}

if (authRequiredCount !== 51) {
  fail(
    "expected 51 auth-required callables, found " +
    authRequiredCount +
    ".",
  );
}

const indexSource =
  read(
    "src/index.ts",
  );

const steamSource =
  read(
    "src/platform/steam-link.ts",
  );

const requiredAbuseScopes = [
  [
    "lobbyCreatePrivateRoom",
    '"lobby_create"',
    indexSource,
  ],
  [
    "lobbyCreatePublicRoom",
    '"lobby_create"',
    indexSource,
  ],
  [
    "lobbyJoinPublicRoom",
    '"lobby_join"',
    indexSource,
  ],
  [
    "lobbyJoinByCode",
    '"lobby_join"',
    indexSource,
  ],
  [
    "platformSteamLinkCurrentAccount",
    '"steam_link"',
    steamSource,
  ],
];

for (const [
  callableName,
  scope,
  source,
] of requiredAbuseScopes) {
  const start =
    source.indexOf(
      `export const ${callableName} = onCall(`,
    );

  if (start < 0) {
    fail(
      "missing callable for abuse audit: " +
      callableName,
    );
  }

  const end =
    source.indexOf(
      "export const ",
      start + 20,
    );

  const segment =
    source.slice(
      start,
      end > start ?
        end :
        source.length,
    );

  if (
    !segment.includes(
      "assertAccountRateLimit(",
    ) ||
    !segment.includes(
      scope,
    )
  ) {
    fail(
      callableName +
      " is missing required abuse guard " +
      scope +
      ".",
    );
  }
}

const lobbySource =
  read(
    "src/lobby/lobby.ts",
  );

const chatSource =
  read(
    "src/chat/chat.ts",
  );

for (const marker of [
  "assertJoinAttemptAllowed",
  "JOIN_RATE_LIMITED",
]) {
  if (!lobbySource.includes(marker)) {
    fail(
      "existing lobby join-code abuse protection missing: " +
      marker,
    );
  }
}

for (const marker of [
  "CHAT_RATE_LIMIT",
  "CHAT_BURST_LIMIT",
  "CHAT_REPORT_RATE_LIMIT",
]) {
  if (!chatSource.includes(marker)) {
    fail(
      "existing chat abuse protection missing: " +
      marker,
    );
  }
}

console.log(
  "Phase 15G callable security audit PASS. " +
  "Callables=54, AuthRequired=51, AuthExempt=3, " +
  "NewAbuseGuards=5, ExistingJoinAndChatLimits=present.",
);
