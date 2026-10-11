import {HttpsError} from "firebase-functions/v2/https";

/**
 * Prevent an accidental deployed Steam verifier from trusting the shared
 * Spacewar development AppID. This is a safety floor, NOT evidence that the
 * configured real publisher secret/AppID was provisioned or accepted.
 *
 * @param {number} appId Configured Steam application id.
 */
export function assertSteamVerifierEnvironment(appId: number): void {
  if (!Number.isSafeInteger(appId) || appId <= 0) {
    throw new HttpsError(
      "failed-precondition",
      "STEAM_APP_ID_NOT_CONFIGURED",
    );
  }

  if (
    appId === 480 &&
    process.env.FUNCTIONS_EMULATOR !== "true"
  ) {
    throw new HttpsError(
      "failed-precondition",
      "STEAM_DEVELOPMENT_APP_ID_FORBIDDEN",
    );
  }
}
