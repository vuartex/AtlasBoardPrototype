import {HttpsError, onCall} from "firebase-functions/v2/https";
import {
  listPublicLobbies,
  LobbyVersionInfo,
} from "../lobby/lobby";
import {ATLAS_ENFORCE_APP_CHECK} from "./app-check-policy";
import {assertAccountRateLimit} from "./callable-abuse";

const REGION = "europe-west1";

/**
 * Keep the Phase 4 legacy public-discovery response contract intact while
 * placing the same shared read-budget guard in front of both browser APIs.
 *
 * The budget is per verified Firebase Auth UID (never caller-supplied data).
 * The 60/minute bound is an interim cost guard, NOT a global DDoS/edge limit.
 */
export const lobbyListPublicRooms = onCall(
  {
    region: REGION,
    maxInstances: 30,
    enforceAppCheck: ATLAS_ENFORCE_APP_CHECK,
  },
  async (request) => {
    const uid = request.auth?.uid;
    if (!uid) {
      throw new HttpsError(
        "unauthenticated",
        "AUTH_REQUIRED",
        {errorKey: "account.error.authentication_required"},
      );
    }

    await assertAccountRateLimit(
      uid,
      "lobby_discovery",
      60,
      60000,
      60000,
      "lobby.error.too_many_attempts",
    );

    const data = request.data ?? {};
    const versions: LobbyVersionInfo = {
      gameVersion: data.gameVersion as string,
      protocolVersion: data.protocolVersion as number,
      rulesVersion: data.rulesVersion as number,
      contentVersion: data.contentVersion as string,
      regionId: data.regionId as string,
    };

    const rooms = await listPublicLobbies({
      uid,
      versions,
      limit: data.limit as number,
    });

    return {ok: true, rooms};
  },
);
