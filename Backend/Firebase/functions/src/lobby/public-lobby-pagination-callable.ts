import {HttpsError, onCall} from "firebase-functions/v2/https";
import {ATLAS_ENFORCE_APP_CHECK} from "../security/app-check-policy";
import {
  listPublicLobbyPage,
  PublicLobbyVersionInfo,
} from "./public-lobby-pagination";

const REGION = "europe-west1";

function requireAuthenticatedUid(
  request: {auth?: {uid: string}},
): string {
  if (!request.auth) {
    throw new HttpsError(
      "unauthenticated",
      "AUTH_REQUIRED",
      {errorKey: "account.error.authentication_required"},
    );
  }

  return request.auth.uid;
}

/**
 * Phase 16I cursor-based discovery endpoint.
 * The legacy lobbyListPublicRooms export remains available for older clients.
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
      cursor:
        typeof data.cursor === "string" ?
          data.cursor :
          undefined,
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
