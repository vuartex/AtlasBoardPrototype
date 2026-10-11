const projectId =
  process.env.ATLAS_FIREBASE_PROJECT_ID ??
  "atlasboard-usa";

const region =
  process.env.ATLAS_FIREBASE_REGION ??
  "europe-west1";

const authHost =
  process.env.FIREBASE_AUTH_EMULATOR_HOST ??
  "127.0.0.1:9099";

const functionsHost =
  process.env.ATLAS_FUNCTIONS_EMULATOR_HOST ??
  "127.0.0.1:5001";

function functionUrl(name) {
  return `http://${functionsHost}/${projectId}/${region}/${name}`;
}

function assert(
  condition,
  message,
) {
  if (!condition) {
    throw new Error(
      message,
    );
  }
}

async function createUser(label) {
  const stamp =
    `${Date.now()}-` +
    Math.floor(
      Math.random() * 100000,
    );

  const response =
    await fetch(
      `http://${authHost}/identitytoolkit.googleapis.com/v1/` +
      "accounts:signUp?key=phase15g-local",
      {
        method:
          "POST",
        headers: {
          "Content-Type":
            "application/json",
        },
        body:
          JSON.stringify({
            email:
              `phase15g-${label}-${stamp}@example.test`,
            password:
              "Phase15G-Local-Only-123!",
            returnSecureToken:
              true,
          }),
      },
    );

  const body =
    await response.json();

  assert(
    response.ok &&
    typeof body.idToken === "string",
    "Could not create Auth Emulator user: " +
    JSON.stringify(body),
  );

  return {
    token:
      body.idToken,
    uid:
      body.localId,
  };
}

async function call(
  name,
  token,
  data,
) {
  const headers = {
    "Content-Type":
      "application/json",
    "X-Firebase-AppCheck":
      "phase15g-local-emulator-token",
  };

  if (token) {
    headers.Authorization =
      `Bearer ${token}`;
  }

  const response =
    await fetch(
      functionUrl(name),
      {
        method:
          "POST",
        headers,
        body:
          JSON.stringify({
            data,
          }),
      },
    );

  let json = null;

  try {
    json =
      await response.json();
  } catch {
    // Keep null for diagnostics.
  }

  return {
    response,
    json,
  };
}

function result(callResult) {
  return callResult.json?.result ??
    null;
}

function errorCode(callResult) {
  return callResult.json?.error?.status ??
    callResult.json?.error?.message ??
    "";
}

async function ensureAccount(
  user,
  displayName,
) {
  const response =
    await call(
      "lobbyDevEnsureAccount",
      user.token,
      {
        displayName,
      },
    );

  assert(
    response.response.ok,
    "lobbyDevEnsureAccount failed: " +
    JSON.stringify(response.json),
  );
}

async function main() {
  const host =
    await createUser(
      "host",
    );

  const guest =
    await createUser(
      "guest",
    );

  const outsider =
    await createUser(
      "outsider",
    );

  await ensureAccount(
    host,
    "Phase15G Host",
  );

  await ensureAccount(
    guest,
    "Phase15G Guest",
  );

  await ensureAccount(
    outsider,
    "Phase15G Outsider",
  );

  let checks =
    0;

  const noAuth =
    await call(
      "economyHealthCheck",
      "",
      {},
    );

  assert(
    noAuth.response.status === 401,
    "Unauthenticated protected callable should be HTTP 401. " +
    JSON.stringify(noAuth.json),
  );

  checks++;

  const versions = {
    gameVersion:
      "0.1.0-dev",
    protocolVersion:
      1,
    rulesVersion:
      1,
    contentVersion:
      "1",
    regionId:
      "eur3",
  };

  const created =
    await call(
      "lobbyCreatePrivateRoom",
      host.token,
      {
        mapId:
          "Turkey",
        themeId:
          "Classic",
        roundLimit:
          20,
        maxPlayers:
          2,
        balancedDevelopment:
          true,
        doublesEnabled:
          true,
        tripleDoublePenaltyEnabled:
          true,
        ...versions,
      },
    );

  assert(
    created.response.ok &&
    result(created)?.roomCode &&
    result(created)?.snapshot?.lobbyId,
    "Host lobby create failed: " +
    JSON.stringify(created.json),
  );

  const lobbyId =
    result(created)
      .snapshot
      .lobbyId;

  const roomCode =
    result(created)
      .roomCode;

  const joined =
    await call(
      "lobbyJoinByCode",
      guest.token,
      {
        roomCode,
        password:
          "",
        idempotencyKey:
          `phase15g-join-${Date.now()}`,
        ...versions,
      },
    );

  assert(
    joined.response.ok,
    "Guest join failed: " +
    JSON.stringify(joined.json),
  );

  const revision =
    result(joined)
      .snapshot
      .settingsRevision;

  const guestHostMutation =
    await call(
      "lobbyUpdateSettings",
      guest.token,
      {
        lobbyId,
        expectedSettingsRevision:
          revision,
        mapId:
          "Turkey",
        themeId:
          "Classic",
        // Use a different but VALID lobby round limit so the request
        // reaches the Host-authorization check instead of failing request
        // validation first. AtlasBoard accepts 10/15/20/30.
        roundLimit:
          30,
        balancedDevelopment:
          true,
        doublesEnabled:
          true,
        tripleDoublePenaltyEnabled:
          true,
      },
    );

  assert(
    guestHostMutation.response.status === 403 ||
    errorCode(guestHostMutation) === "PERMISSION_DENIED",
    "Guest host-only mutation was not rejected: " +
    JSON.stringify(guestHostMutation.json),
  );

  checks++;

  const outsiderRead =
    await call(
      "lobbyGetSnapshot",
      outsider.token,
      {
        lobbyId,
      },
    );

  assert(
    outsiderRead.response.status === 403 ||
    errorCode(outsiderRead) === "PERMISSION_DENIED",
    "Non-member lobby snapshot was not rejected: " +
    JSON.stringify(outsiderRead.json),
  );

  checks++;

  const guestStart =
    await call(
      "lobbyStartMatch",
      guest.token,
      {
        lobbyId,
        expectedSettingsRevision:
          revision,
      },
    );

  assert(
    guestStart.response.status === 403 ||
    errorCode(guestStart) === "PERMISSION_DENIED",
    "Guest start-match attempt was not rejected: " +
    JSON.stringify(guestStart.json),
  );

  checks++;

  const flood =
    await createUser(
      "flood",
    );

  await ensureAccount(
    flood,
    "Phase15G Flood",
  );

  let lobbyRateLimited =
    false;

  for (let i = 0; i < 7; i++) {
    const attempt =
      await call(
        "lobbyCreatePrivateRoom",
        flood.token,
        {
          mapId:
            "Turkey",
          themeId:
            "Classic",
          roundLimit:
            20,
          maxPlayers:
            2,
          balancedDevelopment:
            true,
          doublesEnabled:
            true,
          tripleDoublePenaltyEnabled:
            true,
          ...versions,
        },
      );

    if (
      attempt.response.status === 429 ||
      errorCode(attempt) ===
        "RESOURCE_EXHAUSTED"
    ) {
      lobbyRateLimited =
        true;
      break;
    }

    assert(
      attempt.response.ok,
      "Unexpected lobby-create failure before rate limit: " +
      JSON.stringify(attempt.json),
    );
  }

  assert(
    lobbyRateLimited,
    "Lobby creation abuse limiter did not trigger.",
  );

  checks++;

  const steamUser =
    await createUser(
      "steam",
    );

  await ensureAccount(
    steamUser,
    "Phase15G Steam",
  );

  let steamRateLimited =
    false;

  for (let i = 0; i < 7; i++) {
    const attempt =
      await call(
        "platformSteamLinkCurrentAccount",
        steamUser.token,
        {
          ticket:
            "deadbeef",
        },
      );

    if (
      attempt.response.status === 429 ||
      errorCode(attempt) ===
        "RESOURCE_EXHAUSTED"
    ) {
      steamRateLimited =
        true;
      break;
    }

    assert(
      attempt.response.status === 400,
      "Expected invalid Steam ticket before limiter threshold. " +
      JSON.stringify(attempt.json),
    );
  }

  assert(
    steamRateLimited,
    "Steam-link abuse limiter did not trigger.",
  );

  checks++;

  console.log(
    "Phase 15G authorization/abuse E2E PASS. " +
    `Checks=${checks}/6. ` +
    "Unauthenticated, non-member, and non-host requests were rejected; " +
    "lobby-create and Steam-link abuse limits triggered.",
  );
}

main().catch(
  (error) => {
    console.error(
      "Phase 15G authorization/abuse E2E FAIL.",
      error,
    );

    process.exit(1);
  },
);
