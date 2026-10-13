import {HttpsError, onCall} from "firebase-functions/v2/https";
import {ATLAS_ENFORCE_APP_CHECK} from "../security/app-check-policy";
import {assertAccountRateLimit} from "../security/callable-abuse";
import {
  listPublicLobbyPage,
  PublicLobbyVersionInfo,
} from "./public-lobby-pagination";

const REGION = "europe-west1";

/**
 * Returns the canonical Firebase Auth UID after rejecting anonymous calls.
 * @param {Object} request Firebase callable request authentication envelope.
 * @return {string} Authenticated canonical account ID.
 */
function requireAuthenticatedUid(
  request: {auth?: {uid: string}},
): string {
  if (!request.auth?.uid) {
    throw new HttpsError(
      "unauthenticated",
      "AUTH_REQUIRED",
      {errorKey: "account.error.authentication_required"},
    );
  }

  return request.auth.uid;
}

/**
 * Phase 16I cursor-based discovery; Security Core 04 bounded read budget.
 * Legacy and cursor endpoints share one per-account budget so attackers
 * cannot bypass throttling by switching between the callable names.
 */
export const lobbyListPublicRoomsPage = onCall(
  {
    region: REGION,
    maxInstances: 30,
    enforceAppCheck: ATLAS_ENFORCE_APP_CHECK,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    const data = request.data ?? {};

    await assertAccountRateLimit(
      uid,
      "lobby_discovery",
      60,
      60000,
      60000,
      "lobby.error.too_many_attempts",
    );

    // Reject non-string cursor inputs rather than silently treating them as a
    // request for the first page. Empty string remains the valid first page.
    if (
      data.cursor !== undefined &&
      typeof data.cursor !== "string"
    ) {
      throw new HttpsError(
        "invalid-argument",
        "INVALID_LOBBY_REQUEST",
        {
          errorKey: "lobby.error.invalid_request",
          fieldName: "cursor",
        },
      );
    }

    const versions: PublicLobbyVersionInfo = {
      gameVersion: data.gameVersion as string,
      protocolVersion: data.protocolVersion as number,
      rulesVersion: data.rulesVersion as number,
      contentVersion: data.contentVersion as string,
      regionId: data.regionId as string,
    };

    const result = await listPublicLobbyPage({
      uid,
      versions,
      limit: data.limit as number,
      cursor: data.cursor as string | undefined,
    });

    return {
      ok: true,
      rooms: result.rooms,
      nextCursor: result.nextCursor,
      hasMore: result.hasMore,
      diagnostics: result.diagnostics,
    };
  },
);
