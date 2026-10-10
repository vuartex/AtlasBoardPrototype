const PROJECT_ID = "atlasboard-usa";
const AUTH_HOST =
  process.env.FIREBASE_AUTH_EMULATOR_HOST ??
  "127.0.0.1:9099";
const FIRESTORE_HOST =
  process.env.FIRESTORE_EMULATOR_HOST ??
  "127.0.0.1:8080";
const API_KEY =
  "atlasboard-local-emulator-only";

const authBase =
  `http://${AUTH_HOST}/identitytoolkit.googleapis.com/v1`;
const firestoreBase =
  `http://${FIRESTORE_HOST}/v1/projects/${PROJECT_ID}` +
  "/databases/(default)/documents";

let passed = 0;
let failed = 0;

function timestampValue(
  value = new Date().toISOString(),
) {
  return {
    timestampValue:
      value,
  };
}

function stringValue(value) {
  return {
    stringValue: value,
  };
}

function integerValue(value) {
  return {
    integerValue: String(value),
  };
}

function mapValue(fields = {}) {
  return {
    mapValue: {
      fields,
    },
  };
}

function userFields(uid, {
  countryCode = "US",
  preferredLanguage = "en",
  membershipTier = "normal",
  accountStatus = "active",
  createdAt =
    new Date().toISOString(),
} = {}) {
  return {
    uid: stringValue(uid),
    schemaVersion: integerValue(1),
    accountStatus: stringValue(accountStatus),
    membershipTier: stringValue(membershipTier),
    countryCode: stringValue(countryCode),
    preferredLanguage: stringValue(preferredLanguage),
    createdAt: timestampValue(
      createdAt,
    ),
  };
}

function profileFields(displayName = "Atlas Tester") {
  return {
    displayName: stringValue(displayName),
    avatarId: stringValue(""),
    profileFrameId: stringValue(""),
    updatedAt: timestampValue(),
  };
}

function preferenceFields(language = "en") {
  return {
    language: stringValue(language),
    audio: mapValue(),
    graphics: mapValue(),
    controls: mapValue(),
    gameplay: mapValue(),
    crossplayPreference: stringValue("enabled"),
    updatedAt: timestampValue(),
  };
}

async function createAuthUser(label) {
  const suffix =
    `${Date.now()}-${Math.random().toString(16).slice(2)}`;

  const response =
    await fetch(
      `${authBase}/accounts:signUp?key=${API_KEY}`,
      {
        method: "POST",
        headers: {
          "content-type": "application/json",
        },
        body: JSON.stringify({
          email:
            `phase15a-${label}-${suffix}@atlasboard.local`,
          password:
            "AtlasBoard-Phase15A-Local!",
          returnSecureToken: true,
        }),
      },
    );

  const payload =
    await response.json();

  if (!response.ok ||
      !payload.localId ||
      !payload.idToken) {
    throw new Error(
      `Auth emulator user creation failed: ` +
      `${response.status} ${JSON.stringify(payload)}`,
    );
  }

  return {
    uid: payload.localId,
    token: payload.idToken,
  };
}

async function firestoreRequest(
  method,
  path,
  token = "",
  fields = null,
) {
  const headers = {
    "content-type": "application/json",
  };

  if (token) {
    headers.authorization =
      `Bearer ${token}`;
  }

  const options = {
    method,
    headers,
  };

  if (fields !== null) {
    options.body =
      JSON.stringify({
        fields,
      });
  }

  return fetch(
    `${firestoreBase}/${path}`,
    options,
  );
}

async function expectStatus(
  label,
  expected,
  operation,
) {
  try {
    const response =
      await operation();

    if (response.status !== expected) {
      const body =
        await response.text();

      failed++;

      console.error(
        `FAIL ${label}: expected HTTP ${expected}, ` +
        `got ${response.status}. ${body}`,
      );

      return;
    }

    passed++;

    console.log(
      `PASS ${label}: HTTP ${expected}`,
    );
  } catch (error) {
    failed++;

    console.error(
      `FAIL ${label}: ${error instanceof Error ?
        error.message :
        String(error)}`,
    );
  }
}

async function main() {
  console.log(
    "AtlasBoard Phase 15A Firestore Security Rules E2E",
  );

  console.log(
    `Auth emulator: ${AUTH_HOST}`,
  );

  console.log(
    `Firestore emulator: ${FIRESTORE_HOST}`,
  );

  const accountA =
    await createAuthUser("a");

  const accountB =
    await createAuthUser("b");

  const createdAtA =
    new Date().toISOString();

  const userAPath =
    `users/${accountA.uid}`;

  const profileAPath =
    `public_profiles/${accountA.uid}`;

  const preferencesAPath =
    `preferences/${accountA.uid}`;

  await expectStatus(
    "unauthenticated private user read is denied",
    403,
    () => firestoreRequest(
      "GET",
      userAPath,
    ),
  );

  await expectStatus(
    "owner can create canonical default user",
    200,
    () => firestoreRequest(
      "PATCH",
      userAPath,
      accountA.token,
      userFields(
        accountA.uid,
        {
          createdAt: createdAtA,
        },
      ),
    ),
  );

  await expectStatus(
    "owner can read private user",
    200,
    () => firestoreRequest(
      "GET",
      userAPath,
      accountA.token,
    ),
  );

  await expectStatus(
    "other account cannot read private user",
    403,
    () => firestoreRequest(
      "GET",
      userAPath,
      accountB.token,
    ),
  );

  await expectStatus(
    "owner can update locale only",
    200,
    () => firestoreRequest(
      "PATCH",
      userAPath,
      accountA.token,
      userFields(
        accountA.uid,
        {
          countryCode: "TR",
          preferredLanguage: "tr",
          createdAt: createdAtA,
        },
      ),
    ),
  );

  await expectStatus(
    "owner cannot self-promote membership tier",
    403,
    () => firestoreRequest(
      "PATCH",
      userAPath,
      accountA.token,
      userFields(
        accountA.uid,
        {
          countryCode: "TR",
          preferredLanguage: "tr",
          membershipTier: "premium",
          createdAt: createdAtA,
        },
      ),
    ),
  );

  await expectStatus(
    "other account cannot overwrite private user",
    403,
    () => firestoreRequest(
      "PATCH",
      userAPath,
      accountB.token,
      userFields(
        accountA.uid,
        {
          createdAt: createdAtA,
        },
      ),
    ),
  );

  await expectStatus(
    "client cannot delete canonical user",
    403,
    () => firestoreRequest(
      "DELETE",
      userAPath,
      accountA.token,
    ),
  );

  await expectStatus(
    "owner can create public profile",
    200,
    () => firestoreRequest(
      "PATCH",
      profileAPath,
      accountA.token,
      profileFields(),
    ),
  );

  await expectStatus(
    "authenticated peer can read public profile",
    200,
    () => firestoreRequest(
      "GET",
      profileAPath,
      accountB.token,
    ),
  );

  await expectStatus(
    "anonymous caller cannot read public profile",
    403,
    () => firestoreRequest(
      "GET",
      profileAPath,
    ),
  );

  await expectStatus(
    "peer cannot overwrite another public profile",
    403,
    () => firestoreRequest(
      "PATCH",
      profileAPath,
      accountB.token,
      profileFields("Attacker"),
    ),
  );

  await expectStatus(
    "owner can write cloud preferences",
    200,
    () => firestoreRequest(
      "PATCH",
      preferencesAPath,
      accountA.token,
      preferenceFields("en"),
    ),
  );

  await expectStatus(
    "peer cannot read private preferences",
    403,
    () => firestoreRequest(
      "GET",
      preferencesAPath,
      accountB.token,
    ),
  );

  await expectStatus(
    "unsupported language is denied",
    403,
    () => firestoreRequest(
      "PATCH",
      preferencesAPath,
      accountA.token,
      preferenceFields("xx"),
    ),
  );

  await expectStatus(
    "direct wallet write is denied",
    403,
    () => firestoreRequest(
      "PATCH",
      `wallets/${accountA.uid}`,
      accountA.token,
      {
        hacked: stringValue("true"),
      },
    ),
  );

  await expectStatus(
    "direct authoritative lobby read is denied",
    403,
    () => firestoreRequest(
      "GET",
      "lobbies/not-a-real-lobby",
      accountA.token,
    ),
  );

  console.log();
  console.log(
    `Phase 15A result: ${passed} passed, ${failed} failed.`,
  );

  if (failed > 0) {
    process.exitCode = 1;
    return;
  }

  console.log(
    "PASS: direct Firestore access is deny-by-default and " +
    "Atlas client-owned documents retain least-privilege access.",
  );
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
