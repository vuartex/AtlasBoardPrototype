import {createHash} from "node:crypto";
import {
  FieldValue,
  getFirestore,
} from "firebase-admin/firestore";
import {HttpsError} from "firebase-functions/v2/https";

const COLLECTION =
  "security_callable_rate_limits";

interface RateLimitResult {
  allowed: boolean;
  retryAfterEpochMs: number;
}

/**
 * Applies one server-owned fixed-window account rate limit.
 *
 * This helper is intentionally used only on abuse-sensitive, low-frequency
 * callables. High-frequency heartbeat/poll/snapshot endpoints must not use it.
 *
 * @param {string} uid Authenticated canonical Atlas account id.
 * @param {string} scope Stable server-selected operation scope.
 * @param {number} limit Maximum requests in one window.
 * @param {number} windowMs Fixed-window duration.
 * @param {number} blockMs Block duration after the limit is exceeded.
 * @param {string} errorKey Stable client localization key.
 */
export async function assertAccountRateLimit(
  uid: string,
  scope: string,
  limit: number,
  windowMs: number,
  blockMs: number,
  errorKey: string,
): Promise<void> {
  if (
    !uid ||
    !scope ||
    !Number.isSafeInteger(limit) ||
    limit < 1 ||
    !Number.isSafeInteger(windowMs) ||
    windowMs < 1000 ||
    !Number.isSafeInteger(blockMs) ||
    blockMs < 1000
  ) {
    throw new HttpsError(
      "internal",
      "RATE_LIMIT_CONFIGURATION_INVALID",
    );
  }

  const now =
    Date.now();

  const key =
    createHash("sha256")
      .update(
        `${scope}:${uid}`,
        "utf8",
      )
      .digest("hex");

  const ref =
    getFirestore()
      .collection(COLLECTION)
      .doc(key);

  const result =
    await getFirestore()
      .runTransaction(
        async (transaction): Promise<RateLimitResult> => {
          const snapshot =
            await transaction.get(ref);

          const data =
            snapshot.data() ?? {};

          const previousBlockedUntil =
            typeof data.blockedUntilEpochMs === "number" ?
              data.blockedUntilEpochMs :
              0;

          if (previousBlockedUntil > now) {
            return {
              allowed: false,
              retryAfterEpochMs:
                previousBlockedUntil,
            };
          }

          const previousWindowStart =
            typeof data.windowStartedAtEpochMs === "number" ?
              data.windowStartedAtEpochMs :
              now;

          const sameWindow =
            now - previousWindowStart <
            windowMs;

          const previousCount =
            typeof data.requestCount === "number" ?
              data.requestCount :
              0;

          const requestCount =
            sameWindow ?
              previousCount + 1 :
              1;

          const blocked =
            requestCount > limit;

          const blockedUntilEpochMs =
            blocked ?
              now + blockMs :
              0;

          transaction.set(
            ref,
            {
              uid,
              scope,
              requestCount,
              windowStartedAtEpochMs:
                sameWindow ?
                  previousWindowStart :
                  now,
              blockedUntilEpochMs,
              expiresAtEpochMs:
                Math.max(
                  blockedUntilEpochMs,
                  now + windowMs + blockMs,
                ),
              updatedAt:
                FieldValue.serverTimestamp(),
              schemaVersion:
                1,
            },
            {
              merge: false,
            },
          );

          return {
            allowed:
              !blocked,
            retryAfterEpochMs:
              blockedUntilEpochMs,
          };
        },
      );

  if (!result.allowed) {
    throw new HttpsError(
      "resource-exhausted",
      "CALLABLE_RATE_LIMITED",
      {
        errorKey,
        retryAfterEpochMs:
          result.retryAfterEpochMs,
        scope,
      },
    );
  }
}
