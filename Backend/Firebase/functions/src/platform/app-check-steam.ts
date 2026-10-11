import {getApps, initializeApp} from "firebase-admin/app";
import {getAppCheck} from "firebase-admin/app-check";
import {logger} from "firebase-functions";
import {
  defineInt,
  defineSecret,
  defineString,
} from "firebase-functions/params";
import {
  HttpsError,
  onCall,
} from "firebase-functions/v2/https";

import {assertSteamVerifierEnvironment} from "../security/steam-environment";

const REGION = "europe-west1";

const STEAM_TICKET_IDENTITY =
  "atlasboard-appcheck-v1";

const TOKEN_TTL_MS =
  60 * 60 * 1000;

const DEFAULT_FIREBASE_APP_ID =
  "1:962084427661:android:3e8f20b1ddfe0da4d68fc4";

const STEAM_APP_ID = defineInt(
  "STEAM_APP_ID",
  {
    default: 0,
  },
);

const STEAM_PUBLISHER_WEB_API_KEY = defineSecret(
  "STEAM_PUBLISHER_WEB_API_KEY",
);

const ATLAS_FIREBASE_APP_CHECK_APP_ID = defineString(
  "ATLAS_FIREBASE_APP_CHECK_APP_ID",
  {
    default: DEFAULT_FIREBASE_APP_ID,
  },
);

if (getApps().length === 0) {
  initializeApp();
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

interface VerifiedSteamProof {
  steamId: string;
  steamAppId: number;
}

/**
 * Validates a Steam Web API ticket encoded as hex.
 *
 * @param {unknown} value Candidate ticket.
 * @return {string} Normalized ticket.
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
      "INVALID_STEAM_APP_CHECK_TICKET",
    );
  }

  return value.toLowerCase();
}

/**
 * Verifies a Steam proof against Valve for the configured AtlasBoard AppID.
 *
 * Raw tickets are never persisted or logged.
 *
 * @param {string} ticketHex Steam Web API ticket.
 * @return {Promise<VerifiedSteamProof>} Verified proof.
 */
async function verifySteamAppCheckProof(
  ticketHex: string,
): Promise<VerifiedSteamProof> {
  const steamAppId =
    STEAM_APP_ID.value();

  if (
    !Number.isSafeInteger(steamAppId) ||
    steamAppId <= 0
  ) {
    throw new HttpsError(
      "failed-precondition",
      "STEAM_APP_ID_NOT_CONFIGURED",
    );
  }

  assertSteamVerifierEnvironment(steamAppId);

  const publisherKey =
    STEAM_PUBLISHER_WEB_API_KEY
      .value()
      .trim();

  if (!publisherKey) {
    throw new HttpsError(
      "failed-precondition",
      "STEAM_PUBLISHER_KEY_NOT_CONFIGURED",
    );
  }

  const query =
    new URLSearchParams({
      key: publisherKey,
      appid: String(steamAppId),
      ticket: ticketHex,
      identity: STEAM_TICKET_IDENTITY,
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
    response =
      await fetch(
        "https://partner.steam-api.com/" +
        "ISteamUserAuth/AuthenticateUserTicket/v1/?" +
        query.toString(),
        {
          method: "GET",
          signal: controller.signal,
        },
      );
  } catch {
    logger.error(
      "AtlasBoard Steam App Check verification request failed.",
      {failureClass: "steam_app_check_transport"},
    );

    throw new HttpsError(
      "unavailable",
      "STEAM_APP_CHECK_VERIFICATION_UNAVAILABLE",
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
      "STEAM_APP_CHECK_INVALID_RESPONSE",
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
      "AtlasBoard Steam App Check proof rejected.",
      {
        httpStatus: response.status,
        failureClass: "steam_publisher_rejected",
      },
    );

    throw new HttpsError(
      "permission-denied",
      "STEAM_APP_CHECK_TICKET_REJECTED",
    );
  }

  if (params.publisherbanned === true) {
    throw new HttpsError(
      "permission-denied",
      "STEAM_PUBLISHER_BANNED",
    );
  }

  if (!/^[0-9]{16,20}$/.test(params.steamid)) {
    throw new HttpsError(
      "permission-denied",
      "STEAM_APP_CHECK_IDENTITY_INVALID",
    );
  }

  return {
    steamId: params.steamid,
    steamAppId,
  };
}

/**
 * Phase 15E token-acquisition endpoint for AtlasBoard desktop/Steam.
 *
 * This callable intentionally does NOT require Firebase Auth or App Check:
 * it is the bootstrap endpoint that turns verified Steam proof into an
 * App Check token. Authenticity comes from Valve verification.
 */
export const platformSteamCreateAppCheckToken = onCall(
  {
    region: REGION,
    maxInstances: 20,
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

    const verified =
      await verifySteamAppCheckProof(
        ticketHex,
      );

    const firebaseAppId =
      ATLAS_FIREBASE_APP_CHECK_APP_ID
        .value()
        .trim();

    if (!firebaseAppId) {
      throw new HttpsError(
        "failed-precondition",
        "FIREBASE_APP_CHECK_APP_ID_NOT_CONFIGURED",
      );
    }

    let minted;

    try {
      minted =
        await getAppCheck()
          .createToken(
            firebaseAppId,
            {
              ttlMillis: TOKEN_TTL_MS,
            },
          );
    } catch {
      logger.error(
        "AtlasBoard could not mint a custom App Check token.",
        {failureClass: "app_check_token_mint"},
      );

      throw new HttpsError(
        "internal",
        "APP_CHECK_TOKEN_MINT_FAILED",
      );
    }

    const expiresAtEpochSeconds =
      Math.floor(
        (Date.now() + TOKEN_TTL_MS) /
        1000,
      );

    logger.info(
      "AtlasBoard Steam custom App Check token issued.",
      {
        steamAppId:
          verified.steamAppId,
        verificationMode:
          "steam_webapi_ticket_v1",
        ttlSeconds:
          TOKEN_TTL_MS / 1000,
      },
    );

    return {
      ok: true,
      token:
        minted.token,
      ttlMillis:
        TOKEN_TTL_MS,
      expiresAtEpochSeconds,
      appId:
        firebaseAppId,
      steamAppId:
        verified.steamAppId,
      steamId:
        verified.steamId,
      verificationMode:
        "steam_webapi_ticket_v1",
    };
  },
);
