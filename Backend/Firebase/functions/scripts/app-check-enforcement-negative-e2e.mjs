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

async function callable(
  name,
  data,
  idToken,
  appCheckToken,
) {
  const headers = {
    "Content-Type": "application/json",
  };

  if (idToken) {
    headers.Authorization =
      `Bearer ${idToken}`;
  }

  if (appCheckToken !== undefined) {
    headers["X-Firebase-AppCheck"] =
      appCheckToken;
  }

  const response =
    await fetch(
      functionUrl(name),
      {
        method: "POST",
        headers,
        body:
          JSON.stringify({
            data,
          }),
      },
    );

  return {
    status:
      response.status,
    body:
      await response.text(),
  };
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

async function createAuthEmulatorUser() {
  const stamp =
    `${Date.now()}-` +
    Math.floor(
      Math.random() * 100000,
    );

  const response =
    await fetch(
      `http://${authHost}/identitytoolkit.googleapis.com/v1/` +
      "accounts:signUp?key=phase15f-local",
      {
        method: "POST",
        headers: {
          "Content-Type":
            "application/json",
        },
        body:
          JSON.stringify({
            email:
              `phase15f-${stamp}@example.test`,
            password:
              "Phase15F-Local-Only-123!",
            returnSecureToken:
              true,
          }),
      },
    );

  const body =
    await response.json();

  assert(
    response.ok &&
    typeof body.idToken === "string" &&
    body.idToken.length > 20,
    "Could not create Auth Emulator test identity. " +
    JSON.stringify(body),
  );

  return body.idToken;
}

async function main() {
  const idToken =
    await createAuthEmulatorUser();

  let strictChecksPassed =
    0;

  const missing =
    await callable(
      "economyHealthCheck",
      {},
      idToken,
      undefined,
    );

  assert(
    missing.status === 401,
    "Protected callable without App Check should be HTTP 401. " +
    `Got ${missing.status}: ${missing.body}. ` +
    "Confirm ATLAS_ENFORCE_APP_CHECK=true and restart emulators.",
  );

  strictChecksPassed++;

  const invalid =
    await callable(
      "economyHealthCheck",
      {},
      idToken,
      "phase15f-intentionally-invalid-token",
    );

  let invalidResult;

  if (invalid.status === 401) {
    invalidResult =
      "REJECTED_STRICTLY";
  } else if (invalid.status === 200) {
    invalidResult =
      "FUNCTIONS_EMULATOR_ACCEPTED_PRESENT_TOKEN";

    console.warn(
      "Phase 15F NOTE: the local Functions Emulator accepted the " +
      "intentionally invalid non-empty App Check header and reported it as " +
      "VALID. Local emulator execution is therefore not authoritative for " +
      "cryptographic invalid-token rejection. Production/deployed App Check " +
      "verification must cover this final negative case later.",
    );
  } else {
    throw new Error(
      "Unexpected response for intentionally invalid App Check token. " +
      `Got ${invalid.status}: ${invalid.body}`,
    );
  }

  const devExempt =
    await callable(
      "lobbyDevEnsureAccount",
      {
        displayName:
          "Phase 15F Local Test",
      },
      idToken,
      undefined,
    );

  assert(
    devExempt.status === 200,
    "Emulator-only dev callable must remain usable without App Check. " +
    `Got ${devExempt.status}: ${devExempt.body}.`,
  );

  strictChecksPassed++;

  const bootstrap =
    await callable(
      "platformSteamCreateAppCheckToken",
      {
        ticket:
          "deadbeef",
      },
      "",
      undefined,
    );

  assert(
    bootstrap.status !== 401 &&
    bootstrap.body.includes(
      "INVALID_STEAM_APP_CHECK_TICKET",
    ),
    "Steam App Check bootstrap must remain reachable without a pre-existing " +
    `App Check token. Got ${bootstrap.status}: ${bootstrap.body}.`,
  );

  strictChecksPassed++;

  console.log(
    "Phase 15F local enforcement E2E PASS. " +
    `StrictChecks=${strictChecksPassed}/3, ` +
    `InvalidToken=${invalidResult}. ` +
    "Missing App Check was rejected; emulator-only dev and Steam bootstrap " +
    "exceptions remained reachable.",
  );

  if (
    invalidResult ===
    "FUNCTIONS_EMULATOR_ACCEPTED_PRESENT_TOKEN"
  ) {
    console.log(
      "Phase 15F deferred negative gate: cryptographic invalid-token rejection " +
      "must be proven against a deployed App Check-enforced callable before " +
      "production security close.",
    );
  }
}

main().catch(
  (error) => {
    console.error(
      "Phase 15F local enforcement E2E FAIL.",
      error,
    );

    process.exit(1);
  },
);
