import {createHash} from "node:crypto";
import {getApps, initializeApp} from "firebase-admin/app";
import {getAuth} from "firebase-admin/auth";
import {
  FieldValue,
  getFirestore,
} from "firebase-admin/firestore";
import {logger} from "firebase-functions";
import {
  defineInt,
  defineSecret,
} from "firebase-functions/params";
import {
  HttpsError,
  onCall,
} from "firebase-functions/v2/https";

const REGION = "europe-west1";
const PROVIDER = "steam";
const SCHEMA_VERSION = 1;
const TICKET_IDENTITY = "atlasboard-account-link-v1";

const STEAM_APP_ID = defineInt(
  "STEAM_APP_ID",
  {
    default: 0,
  },
);

const STEAM_PUBLISHER_WEB_API_KEY = defineSecret(
  "STEAM_PUBLISHER_WEB_API_KEY",
);

if (getApps().length === 0) {
  initializeApp();
}

interface VerifiedSteamIdentity {
  steamId: string;
  ownerSteamId: string;
  appId: number;
  vacBanned: boolean;
  publisherBanned: boolean;
  verified: boolean;
  developmentOnly: boolean;
  verificationMode: string;
  proofHash: string;
}

interface LinkResult {
  linked: boolean;
  verified: boolean;
  developmentOnly: boolean;
  accountId: string;
  provider: string;
  steamId: string;
  ownerSteamId: string;
  appId: number;
  verificationMode: string;
  applied: boolean;
  idempotentReplay: boolean;
  upgradedToVerified: boolean;
  repaired: boolean;
}

interface LinkStatus {
  linked: boolean;
  verified: boolean;
  developmentOnly: boolean;
  accountId: string;
  provider: string;
  steamId: string;
  ownerSteamId: string;
  appId: number;
  verificationMode: string;
  integrityOk: boolean;
}

interface ReturningSignInMapping {
  accountId: string;
  steamId: string;
  ownerSteamId: string;
  appId: number;
  verified: boolean;
  developmentOnly: boolean;
  verificationMode: string;
}

interface SteamAuthPayload {
  response?: {
    params?: {
      result?: string;
      steamid?: string;
      ownersteamid?: string;
      vacbanned?: boolean;
      publisherbanned?: boolean;
    };
    error?: {
      errorcode?: number;
      errordesc?: string;
    };
  };
}

/**
 * Returns an authenticated Firebase UID.
 * @param {Object} request Minimal callable request auth shape.
 * @return {string} Authenticated account id.
 */
function requireAuthenticatedUid(
  request: {auth?: {uid: string}},
): string {
  if (!request.auth) {
    throw new HttpsError(
      "unauthenticated",
      "AUTH_REQUIRED",
      {
        errorKey: "account.error.authentication_required",
      },
    );
  }

  return request.auth.uid;
}

/**
 * Requires local Functions + Firestore emulators.
 */
function requireEmulatedFirestore(): void {
  if (
    process.env.FUNCTIONS_EMULATOR !== "true" ||
    !process.env.FIRESTORE_EMULATOR_HOST
  ) {
    throw new HttpsError(
      "failed-precondition",
      "EMULATOR_ONLY",
      {
        errorKey: "platform.error.emulator_only",
      },
    );
  }
}

/**
 * Requires the local Firebase Authentication emulator.
 */
function requireEmulatedAuth(): void {
  if (!process.env.FIREBASE_AUTH_EMULATOR_HOST) {
    throw new HttpsError(
      "failed-precondition",
      "AUTH_EMULATOR_ONLY",
      {
        errorKey: "platform.error.emulator_only",
      },
    );
  }
}

/**
 * Base64url-encodes UTF-8 text.
 * @param {string} value Input text.
 * @return {string} Encoded text.
 */
function encodeBase64Url(
  value: string,
): string {
  return Buffer
    .from(value, "utf8")
    .toString("base64url");
}

/**
 * Creates an emulator-only custom token.
 *
 * The Firebase Auth emulator does not validate custom-token signatures.
 * This token must never be used outside emulator mode.
 * @param {string} uid Canonical Atlas account id.
 * @return {string} Emulator-only custom token.
 */
function createEmulatorCustomToken(
  uid: string,
): string {
  const now =
    Math.floor(Date.now() / 1000);

  const projectId =
    process.env.GCLOUD_PROJECT ??
    process.env.GOOGLE_CLOUD_PROJECT ??
    "atlasboard-usa";

  const issuer =
    `firebase-auth-emulator@${projectId}` +
    ".iam.gserviceaccount.com";

  const header =
    encodeBase64Url(
      JSON.stringify({
        alg: "RS256",
        typ: "JWT",
      }),
    );

  const payload =
    encodeBase64Url(
      JSON.stringify({
        iss: issuer,
        sub: issuer,
        aud:
          "https://identitytoolkit.googleapis.com/" +
          "google.identity.identitytoolkit.v1.IdentityToolkit",
        iat: now,
        exp: now + 3600,
        uid,
      }),
    );

  const signature =
    encodeBase64Url(
      "atlasboard-auth-emulator-only",
    );

  return `${header}.${payload}.${signature}`;
}

/**
 * Normalizes a Steam64 account id.
 * @param {unknown} value Candidate Steam id.
 * @return {string} Valid Steam64 id.
 */
function readSteamId(
  value: unknown,
): string {
  if (
    typeof value !== "string" ||
    !/^[0-9]{16,20}$/.test(value)
  ) {
    throw new HttpsError(
      "invalid-argument",
      "INVALID_STEAM_ID",
      {
        errorKey: "platform.error.invalid_steam_id",
      },
    );
  }

  return value;
}

/**
 * Validates a Steam Web API ticket encoded as hexadecimal text.
 * @param {unknown} value Candidate ticket.
 * @return {string} Normalized lowercase ticket.
 */
function readTicketHex(
  value: unknown,
): string {
  if (
    typeof value !== "string" ||
    value.length < 32 ||
    value.length > 5120 ||
    value.length % 2 !== 0 ||
    !/^[0-9a-fA-F]+$/.test(value)
  ) {
    throw new HttpsError(
      "invalid-argument",
      "INVALID_STEAM_TICKET",
      {
        errorKey: "platform.error.invalid_steam_ticket",
      },
    );
  }

  return value.toLowerCase();
}

/**
 * Computes a stable SHA-256 hexadecimal digest.
 * @param {string} value Input string.
 * @return {string} Digest.
 */
function sha256(
  value: string,
): string {
  return createHash("sha256")
    .update(value, "utf8")
    .digest("hex");
}

/**
 * Returns the provider lookup document id.
 * @param {string} steamId Verified Steam id.
 * @return {string} Firestore document id.
 */
function providerDocumentId(
  steamId: string,
): string {
  return `${PROVIDER}_${steamId}`;
}

/**
 * Returns the account lookup document id.
 * @param {string} uid Atlas account id.
 * @return {string} Firestore document id.
 */
function accountDocumentId(
  uid: string,
): string {
  return `${uid}_${PROVIDER}`;
}

/**
 * Returns an immutable link-event id.
 * @param {string} uid Atlas account id.
 * @param {string} steamId Steam id.
 * @param {string} mode Verification mode.
 * @return {string} Event id.
 */
function linkEventId(
  uid: string,
  steamId: string,
  mode: string,
): string {
  return sha256(
    `atlasboard:platform-link:${uid}:${steamId}:${mode}`,
  );
}

/**
 * Reads the current Steam link for an Atlas account.
 * @param {string} uid Atlas account id.
 * @return {Promise<LinkStatus>} Link status.
 */
export async function getSteamLinkStatus(
  uid: string,
): Promise<LinkStatus> {
  const db = getFirestore();

  const accountRef = db
    .collection("account_platform_links")
    .doc(accountDocumentId(uid));

  const accountSnapshot =
    await accountRef.get();

  if (!accountSnapshot.exists) {
    return {
      linked: false,
      verified: false,
      developmentOnly: false,
      accountId: uid,
      provider: PROVIDER,
      steamId: "",
      ownerSteamId: "",
      appId: 0,
      verificationMode: "",
      integrityOk: true,
    };
  }

  const accountData =
    accountSnapshot.data() ?? {};

  const steamId =
    readSteamId(accountData.providerUserId);

  const providerRef = db
    .collection("platform_identity_links")
    .doc(providerDocumentId(steamId));

  const providerSnapshot =
    await providerRef.get();

  if (!providerSnapshot.exists) {
    throw new HttpsError(
      "internal",
      "PLATFORM_LINK_INTEGRITY_ERROR",
      {
        errorKey: "platform.error.link_integrity",
      },
    );
  }

  const providerData =
    providerSnapshot.data() ?? {};

  if (
    providerData.accountId !== uid ||
    providerData.providerUserId !== steamId
  ) {
    throw new HttpsError(
      "internal",
      "PLATFORM_LINK_INTEGRITY_ERROR",
      {
        errorKey: "platform.error.link_integrity",
      },
    );
  }

  return {
    linked: true,
    verified:
      accountData.verified === true &&
      providerData.verified === true,
    developmentOnly:
      accountData.developmentOnly === true ||
      providerData.developmentOnly === true,
    accountId: uid,
    provider: PROVIDER,
    steamId,
    ownerSteamId:
      typeof accountData.ownerProviderUserId === "string" ?
        accountData.ownerProviderUserId :
        steamId,
    appId:
      typeof accountData.appId === "number" ?
        accountData.appId :
        0,
    verificationMode:
      typeof accountData.verificationMode === "string" ?
        accountData.verificationMode :
        "",
    integrityOk: true,
  };
}

/**
 * Applies a verified or emulator-development Steam identity link.
 * @param {string} uid Atlas account id.
 * @param {VerifiedSteamIdentity} identity Verified identity.
 * @return {Promise<LinkResult>} Applied link result.
 */
export async function applySteamIdentityLink(
  uid: string,
  identity: VerifiedSteamIdentity,
): Promise<LinkResult> {
  const db = getFirestore();

  const providerRef = db
    .collection("platform_identity_links")
    .doc(providerDocumentId(identity.steamId));

  const accountRef = db
    .collection("account_platform_links")
    .doc(accountDocumentId(uid));

  const eventRef = db
    .collection("platform_link_events")
    .doc(
      linkEventId(
        uid,
        identity.steamId,
        identity.verificationMode,
      ),
    );

  return db.runTransaction(
    async (transaction) => {
      const [
        providerSnapshot,
        accountSnapshot,
      ] = await Promise.all([
        transaction.get(providerRef),
        transaction.get(accountRef),
      ]);

      const providerData =
        providerSnapshot.exists ?
          providerSnapshot.data() ?? {} :
          null;

      const accountData =
        accountSnapshot.exists ?
          accountSnapshot.data() ?? {} :
          null;

      if (
        providerData &&
        providerData.accountId !== uid
      ) {
        throw new HttpsError(
          "already-exists",
          "STEAM_IDENTITY_ALREADY_LINKED",
          {
            errorKey:
              "platform.error.steam_already_linked",
          },
        );
      }

      if (
        accountData &&
        accountData.providerUserId !== identity.steamId
      ) {
        throw new HttpsError(
          "failed-precondition",
          "ATLAS_ACCOUNT_ALREADY_HAS_STEAM",
          {
            errorKey:
              "platform.error.account_has_other_steam",
          },
        );
      }

      const providerVerified =
        providerData?.verified === true;

      const accountVerified =
        accountData?.verified === true;

      const alreadyVerified =
        providerVerified &&
        accountVerified;

      const bothExist =
        providerSnapshot.exists &&
        accountSnapshot.exists;

      const upgradeToVerified =
        identity.verified &&
        !alreadyVerified;

      const repaired =
        providerSnapshot.exists !==
        accountSnapshot.exists;

      const shouldWrite =
        !bothExist ||
        repaired ||
        upgradeToVerified;

      if (!shouldWrite) {
        return {
          linked: true,
          verified: alreadyVerified,
          developmentOnly:
            accountData?.developmentOnly === true,
          accountId: uid,
          provider: PROVIDER,
          steamId: identity.steamId,
          ownerSteamId:
            typeof accountData?.ownerProviderUserId === "string" ?
              accountData.ownerProviderUserId :
              identity.ownerSteamId,
          appId:
            typeof accountData?.appId === "number" ?
              accountData.appId :
              identity.appId,
          verificationMode:
            typeof accountData?.verificationMode === "string" ?
              accountData.verificationMode :
              identity.verificationMode,
          applied: false,
          idempotentReplay: true,
          upgradedToVerified: false,
          repaired: false,
        };
      }

      const now =
        FieldValue.serverTimestamp();

      const common = {
        schemaVersion: SCHEMA_VERSION,
        provider: PROVIDER,
        providerUserId: identity.steamId,
        ownerProviderUserId:
          identity.ownerSteamId,
        accountId: uid,
        appId: identity.appId,
        verified:
          identity.verified ||
          alreadyVerified,
        developmentOnly:
          identity.verified ?
            false :
            identity.developmentOnly,
        verificationMode:
          identity.verified ?
            identity.verificationMode :
            accountData?.verificationMode ??
            identity.verificationMode,
        vacBanned: identity.vacBanned,
        publisherBanned:
          identity.publisherBanned,
        updatedAt: now,
      };

      transaction.set(
        providerRef,
        {
          ...common,
          ...(providerSnapshot.exists ?
            {} :
            {createdAt: now}),
          ...(identity.verified ?
            {lastVerifiedAt: now} :
            {}),
        },
        {merge: true},
      );

      transaction.set(
        accountRef,
        {
          ...common,
          ...(accountSnapshot.exists ?
            {} :
            {createdAt: now}),
          ...(identity.verified ?
            {lastVerifiedAt: now} :
            {}),
        },
        {merge: true},
      );

      transaction.set(
        eventRef,
        {
          schemaVersion: SCHEMA_VERSION,
          eventType:
            identity.verified ?
              "steam_verified_link" :
              "steam_emulator_dev_link",
          provider: PROVIDER,
          providerUserId: identity.steamId,
          ownerProviderUserId:
            identity.ownerSteamId,
          accountId: uid,
          appId: identity.appId,
          verified: identity.verified,
          developmentOnly:
            identity.developmentOnly,
          verificationMode:
            identity.verificationMode,
          proofHash: identity.proofHash,
          serverTime: now,
        },
        {merge: false},
      );

      return {
        linked: true,
        verified:
          identity.verified ||
          alreadyVerified,
        developmentOnly:
          identity.verified ?
            false :
            identity.developmentOnly,
        accountId: uid,
        provider: PROVIDER,
        steamId: identity.steamId,
        ownerSteamId:
          identity.ownerSteamId,
        appId: identity.appId,
        verificationMode:
          identity.verificationMode,
        applied: true,
        idempotentReplay: false,
        upgradedToVerified:
          upgradeToVerified &&
          bothExist,
        repaired,
      };
    },
  );
}

/**
 * Resolves Steam -> Atlas mapping and verifies both lookup directions.
 * @param {string} steamId Steam identity.
 * @param {boolean} requireVerified Whether production verification is required.
 * @return {Promise<ReturningSignInMapping>} Canonical mapping.
 */
async function resolveSteamReturningMapping(
  steamId: string,
  requireVerified: boolean,
): Promise<ReturningSignInMapping> {
  const db =
    getFirestore();

  const providerRef =
    db
      .collection("platform_identity_links")
      .doc(providerDocumentId(steamId));

  const providerSnapshot =
    await providerRef.get();

  if (!providerSnapshot.exists) {
    throw new HttpsError(
      "not-found",
      "STEAM_ACCOUNT_NOT_LINKED",
      {
        errorKey:
          "platform.error.steam_not_linked",
      },
    );
  }

  const providerData =
    providerSnapshot.data() ?? {};

  const accountId =
    typeof providerData.accountId === "string" ?
      providerData.accountId :
      "";

  if (!accountId) {
    throw new HttpsError(
      "internal",
      "PLATFORM_LINK_INTEGRITY_ERROR",
      {
        errorKey:
          "platform.error.link_integrity",
      },
    );
  }

  const accountRef =
    db
      .collection("account_platform_links")
      .doc(accountDocumentId(accountId));

  const accountSnapshot =
    await accountRef.get();

  if (!accountSnapshot.exists) {
    throw new HttpsError(
      "internal",
      "PLATFORM_LINK_INTEGRITY_ERROR",
      {
        errorKey:
          "platform.error.link_integrity",
      },
    );
  }

  const accountData =
    accountSnapshot.data() ?? {};

  if (
    accountData.accountId !== accountId ||
    accountData.providerUserId !== steamId ||
    providerData.providerUserId !== steamId
  ) {
    throw new HttpsError(
      "internal",
      "PLATFORM_LINK_INTEGRITY_ERROR",
      {
        errorKey:
          "platform.error.link_integrity",
      },
    );
  }

  const verified =
    providerData.verified === true &&
    accountData.verified === true;

  const developmentOnly =
    providerData.developmentOnly === true ||
    accountData.developmentOnly === true;

  if (
    requireVerified &&
    (!verified || developmentOnly)
  ) {
    throw new HttpsError(
      "permission-denied",
      "STEAM_LINK_NOT_VERIFIED",
      {
        errorKey:
          "platform.error.steam_link_not_verified",
      },
    );
  }

  return {
    accountId,
    steamId,
    ownerSteamId:
      typeof accountData.ownerProviderUserId === "string" ?
        accountData.ownerProviderUserId :
        steamId,
    appId:
      typeof accountData.appId === "number" ?
        accountData.appId :
        0,
    verified,
    developmentOnly,
    verificationMode:
      typeof accountData.verificationMode === "string" ?
        accountData.verificationMode :
        "",
  };
}

/**
 * Verifies a Steam Web API ticket through Valve's secure publisher endpoint.
 * @param {string} ticketHex Hex-encoded Web API ticket.
 * @return {Promise<VerifiedSteamIdentity>} Verified identity.
 */
async function verifySteamWebApiTicket(
  ticketHex: string,
): Promise<VerifiedSteamIdentity> {
  const appId =
    STEAM_APP_ID.value();

  if (!Number.isSafeInteger(appId) ||
      appId <= 0) {
    throw new HttpsError(
      "failed-precondition",
      "STEAM_APP_ID_NOT_CONFIGURED",
      {
        errorKey:
          "platform.error.steam_verifier_not_configured",
      },
    );
  }

  const publisherKey =
    STEAM_PUBLISHER_WEB_API_KEY
      .value()
      .trim();

  if (!publisherKey) {
    throw new HttpsError(
      "failed-precondition",
      "STEAM_PUBLISHER_KEY_NOT_CONFIGURED",
      {
        errorKey:
          "platform.error.steam_verifier_not_configured",
      },
    );
  }

  const query =
    new URLSearchParams({
      key: publisherKey,
      appid: String(appId),
      ticket: ticketHex,
      identity: TICKET_IDENTITY,
    });

  const controller =
    new AbortController();

  const timeout =
    setTimeout(
      () => controller.abort(),
      10000,
    );

  let response: Response;

  try {
    response = await fetch(
      "https://partner.steam-api.com/" +
      "ISteamUserAuth/AuthenticateUserTicket/v1/?" +
      query.toString(),
      {
        method: "GET",
        signal: controller.signal,
      },
    );
  } catch (error) {
    logger.error(
      "AtlasBoard Steam ticket verification request failed.",
      {
        error:
          error instanceof Error ?
            error.message :
            String(error),
      },
    );

    throw new HttpsError(
      "unavailable",
      "STEAM_VERIFICATION_UNAVAILABLE",
      {
        errorKey:
          "platform.error.steam_verification_unavailable",
      },
    );
  } finally {
    clearTimeout(timeout);
  }

  let payload: SteamAuthPayload;

  try {
    payload =
      await response.json() as SteamAuthPayload;
  } catch {
    throw new HttpsError(
      "unavailable",
      "STEAM_VERIFICATION_INVALID_RESPONSE",
      {
        errorKey:
          "platform.error.steam_verification_unavailable",
      },
    );
  }

  const params =
    payload.response?.params;

  if (
    !response.ok ||
    params?.result !== "OK" ||
    !params.steamid
  ) {
    logger.warn(
      "AtlasBoard Steam ticket rejected.",
      {
        httpStatus: response.status,
        steamError:
          payload.response?.error?.errordesc ??
          "",
      },
    );

    throw new HttpsError(
      "permission-denied",
      "STEAM_TICKET_REJECTED",
      {
        errorKey:
          "platform.error.steam_ticket_rejected",
      },
    );
  }

  const steamId =
    readSteamId(params.steamid);

  const ownerSteamId =
    params.ownersteamid ?
      readSteamId(params.ownersteamid) :
      steamId;

  return {
    steamId,
    ownerSteamId,
    appId,
    vacBanned:
      params.vacbanned === true,
    publisherBanned:
      params.publisherbanned === true,
    verified: true,
    developmentOnly: false,
    verificationMode:
      "steam_webapi_ticket_v1",
    proofHash: sha256(ticketHex),
  };
}

/**
 * Returns Steam link status for the authenticated Atlas account.
 */
export const platformSteamGetLinkStatus = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid =
      requireAuthenticatedUid(request);

    const status =
      await getSteamLinkStatus(uid);

    return {
      ok: true,
      ...status,
    };
  },
);

/**
 * Verifies a Steam ticket server-side and links it to the current
 * Atlas account.
 */
export const platformSteamLinkCurrentAccount = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
    secrets: [
      STEAM_PUBLISHER_WEB_API_KEY,
    ],
  },
  async (request) => {
    const uid =
      requireAuthenticatedUid(request);

    const ticketHex =
      readTicketHex(
        request.data?.ticket,
      );

    const identity =
      await verifySteamWebApiTicket(
        ticketHex,
      );

    const result =
      await applySteamIdentityLink(
        uid,
        identity,
      );

    logger.info(
      "AtlasBoard verified Steam account link completed.",
      {
        accountId: uid,
        provider: PROVIDER,
        steamId: identity.steamId,
        appId: identity.appId,
        applied: result.applied,
        idempotentReplay:
          result.idempotentReplay,
        upgradedToVerified:
          result.upgradedToVerified,
      },
    );

    return {
      ok: true,
      ...result,
    };
  },
);

/**
 * Emulator-only proof for link-state/conflict testing without publisher
 * secrets.
 */
export const platformSteamDevLinkCurrentAccount = onCall(
  {
    region: REGION,
    maxInstances: 2,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid =
      requireAuthenticatedUid(request);

    requireEmulatedFirestore();

    const steamId =
      readSteamId(
        request.data?.steamId,
      );

    const requestedAppId =
      request.data?.appId;

    const appId =
      typeof requestedAppId === "number" &&
      Number.isSafeInteger(requestedAppId) &&
      requestedAppId > 0 ?
        requestedAppId :
        480;

    const identity: VerifiedSteamIdentity = {
      steamId,
      ownerSteamId: steamId,
      appId,
      vacBanned: false,
      publisherBanned: false,
      verified: false,
      developmentOnly: true,
      verificationMode:
        "emulator_dev_proof",
      proofHash:
        sha256(
          `emulator:${uid}:${steamId}:${appId}`,
        ),
    };

    const result =
      await applySteamIdentityLink(
        uid,
        identity,
      );

    logger.info(
      "AtlasBoard emulator Steam link proof completed.",
      {
        accountId: uid,
        provider: PROVIDER,
        steamId,
        appId,
        applied: result.applied,
        idempotentReplay:
          result.idempotentReplay,
      },
    );

    return {
      ok: true,
      ...result,
    };
  },
);
/**
 * Verifies Steam server-side and returns a Firebase custom token for an
 * already-linked canonical Atlas account.
 */
export const platformSteamReturningSignIn = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
    secrets: [
      STEAM_PUBLISHER_WEB_API_KEY,
    ],
  },
  async (request) => {
    const ticketHex =
      readTicketHex(
        request.data?.ticket,
      );

    const identity =
      await verifySteamWebApiTicket(
        ticketHex,
      );

    const mapping =
      await resolveSteamReturningMapping(
        identity.steamId,
        true,
      );

    if (
      mapping.appId > 0 &&
      mapping.appId !== identity.appId
    ) {
      throw new HttpsError(
        "permission-denied",
        "STEAM_APP_ID_MISMATCH",
        {
          errorKey:
            "platform.error.steam_app_mismatch",
        },
      );
    }

    const customToken =
      await getAuth().createCustomToken(
        mapping.accountId,
        {
          atlasProvider: PROVIDER,
          atlasSteamId: identity.steamId,
        },
      );

    logger.info(
      "AtlasBoard returning Steam sign-in token issued.",
      {
        accountId: mapping.accountId,
        provider: PROVIDER,
        steamId: identity.steamId,
        appId: identity.appId,
        verified: true,
      },
    );

    return {
      ok: true,
      accountId: mapping.accountId,
      provider: PROVIDER,
      steamId: identity.steamId,
      ownerSteamId: mapping.ownerSteamId,
      appId: identity.appId,
      verified: true,
      developmentOnly: false,
      verificationMode:
        "steam_webapi_ticket_v1",
      customToken,
    };
  },
);

/**
 * Emulator-only returning-user proof.
 *
 * This callable is intentionally unauthenticated because its purpose is to
 * restore the already-linked Atlas account. It is hard-gated to local
 * Functions + Firestore + Auth emulators.
 */
export const platformSteamDevReturningSignIn = onCall(
  {
    region: REGION,
    maxInstances: 2,
    enforceAppCheck: false,
  },
  async (request) => {
    requireEmulatedFirestore();
    requireEmulatedAuth();

    const steamId =
      readSteamId(
        request.data?.steamId,
      );

    const mapping =
      await resolveSteamReturningMapping(
        steamId,
        false,
      );

    const customToken =
      createEmulatorCustomToken(
        mapping.accountId,
      );

    logger.info(
      "AtlasBoard emulator returning Steam sign-in issued.",
      {
        accountId: mapping.accountId,
        provider: PROVIDER,
        steamId,
        verified: mapping.verified,
        developmentOnly:
          mapping.developmentOnly,
      },
    );

    return {
      ok: true,
      accountId: mapping.accountId,
      provider: PROVIDER,
      steamId,
      ownerSteamId: mapping.ownerSteamId,
      appId: mapping.appId,
      verified: mapping.verified,
      developmentOnly: true,
      verificationMode:
        "emulator_returning_signin",
      customToken,
    };
  },
);
