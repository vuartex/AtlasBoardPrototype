import {getApps, initializeApp} from "firebase-admin/app";
import {FieldValue, getFirestore} from "firebase-admin/firestore";
import {logger} from "firebase-functions";
import {HttpsError, onCall} from "firebase-functions/v2/https";
import {setGlobalOptions} from "firebase-functions/v2/options";
import {
  applyWalletMutation,
  isWalletCurrencyId,
  WalletMutationInput,
} from "./economy/wallet";
import {
  applyInventoryMutation,
  InventoryMutationInput,
  isInventoryOperation,
} from "./economy/inventory";
import {
  applyCommercePurchase,
  applyCommerceRefund,
  CommercePurchaseInput,
  CommerceRefundInput,
} from "./economy/commerce";
import {
  applyPromoRedemption,
  PromoRedeemInput,
} from "./economy/promo";
import {
  configureLobbySeats,
  createPrivateLobby,
  getLobbySnapshot,
  joinLobbyByCode,
  joinPublicLobby,
  kickLobbyMember,
  leaveLobby,
  updateLobbyPassword,
  closeLobby,
  setLobbyReady,
  startLobbyMatch,
  updateLobbySettings,
  LobbySeatPolicy,
  LobbySettingsInput,
  LobbyVersionInfo,
  createPublicLobby,
  listPublicLobbies,
} from "./lobby/lobby";

const REGION = "europe-west1";
const PROJECT_ID = "atlasboard-usa";
const BACKEND_SCHEMA_VERSION = 1;
const PROTOCOL_VERSION = 1;
const TEST_MUTATION_LIMIT = 100_000;

setGlobalOptions({
  region: REGION,
  maxInstances: 10,
});

if (getApps().length === 0) {
  initializeApp();
}

/**
 * Returns the authenticated UID or throws a stable callable error.
 * @param {Object} request Minimal callable auth shape.
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
 * Blocks test mutations unless Functions and Firestore are both local.
 * @param {string} errorKey Stable localized error key.
 */
function requireEmulatedFirestore(
  errorKey = "economy.error.emulator_only",
): void {
  if (
    process.env.FUNCTIONS_EMULATOR !== "true" ||
    !process.env.FIRESTORE_EMULATOR_HOST
  ) {
    throw new HttpsError(
      "failed-precondition",
      "EMULATOR_ONLY",
      {
        errorKey,
      },
    );
  }
}

/**
 * Reads a bounded required string from callable request data.
 * @param {unknown} value Value to validate.
 * @param {string} fieldName Field name used in error details.
 * @param {number} minLength Minimum accepted length.
 * @param {number} maxLength Maximum accepted length.
 * @param {string} errorKey Stable localized error key.
 * @return {string} Validated string.
 */
function readRequiredString(
  value: unknown,
  fieldName: string,
  minLength: number,
  maxLength: number,
  errorKey = "economy.error.invalid_request",
): string {
  if (
    typeof value !== "string" ||
    value.length < minLength ||
    value.length > maxLength
  ) {
    throw new HttpsError(
      "invalid-argument",
      `INVALID_${fieldName.toUpperCase()}`,
      {
        errorKey,
        fieldName,
      },
    );
  }

  return value;
}

/**
 * Authenticated, read-only backend connectivity probe.
 *
 * This function intentionally performs no Firestore writes and no economy
 * mutations. It exists only to validate client -> Auth -> Callable Functions
 * wiring before wallet, inventory, commerce, promo, or lobby authority code
 * is introduced.
 */
export const economyHealthCheck = onCall(
  {
    region: REGION,
    maxInstances: 2,
    enforceAppCheck: false,
  },
  (request) => {
    const uid = requireAuthenticatedUid(request);

    const runtimeProjectId =
      process.env.GCLOUD_PROJECT ??
      process.env.GOOGLE_CLOUD_PROJECT ??
      PROJECT_ID;

    const runtimeMode =
      process.env.FUNCTIONS_EMULATOR === "true" ?
        "emulator" :
        "cloud";

    logger.info("AtlasBoard economyHealthCheck passed.", {
      accountId: uid,
      projectId: runtimeProjectId,
      region: REGION,
      runtimeMode,
    });

    return {
      ok: true,
      authenticated: true,
      accountId: uid,
      projectId: runtimeProjectId,
      region: REGION,
      backendSchemaVersion: BACKEND_SCHEMA_VERSION,
      protocolVersion: PROTOCOL_VERSION,
      service: "economy",
      mode: runtimeMode,
      serverTimeUtc: new Date().toISOString(),
    };
  },
);

/**
 * Emulator-only wallet mutation probe for Phase 3C.4B.
 *
 * This endpoint refuses to run unless BOTH the Functions Emulator and the
 * Firestore Emulator are active. It must not be used as a production grant
 * or debit API. Production commerce/admin mutation entry points are separate
 * later phases and will apply their own authorization rules.
 */
export const walletTestMutation = onCall(
  {
    region: REGION,
    maxInstances: 2,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    requireEmulatedFirestore();

    const data = request.data ?? {};

    if (!isWalletCurrencyId(data.currencyId)) {
      throw new HttpsError(
        "invalid-argument",
        "INVALID_CURRENCY",
        {
          errorKey: "economy.error.invalid_currency",
        },
      );
    }

    if (
      typeof data.delta !== "number" ||
      !Number.isSafeInteger(data.delta) ||
      data.delta === 0 ||
      Math.abs(data.delta) > TEST_MUTATION_LIMIT
    ) {
      throw new HttpsError(
        "invalid-argument",
        "INVALID_DELTA",
        {
          errorKey: "economy.error.invalid_delta",
        },
      );
    }

    const reason = readRequiredString(
      data.reason,
      "reason",
      3,
      64,
    );
    const transactionId = readRequiredString(
      data.transactionId,
      "transactionId",
      8,
      128,
    );
    const idempotencyKey = readRequiredString(
      data.idempotencyKey,
      "idempotencyKey",
      8,
      128,
    );

    const input: WalletMutationInput = {
      uid,
      currencyId: data.currencyId,
      delta: data.delta,
      reason,
      transactionId,
      idempotencyKey,
      source: "phase_3c_4b_emulator_test",
    };

    const result = await applyWalletMutation(input);

    logger.info("AtlasBoard walletTestMutation completed.", {
      accountId: uid,
      currencyId: result.currencyId,
      delta: result.delta,
      balanceAfter: result.balanceAfter,
      applied: result.applied,
      idempotentReplay: result.idempotentReplay,
      ledgerEntryId: result.ledgerEntryId,
    });

    return {
      ok: true,
      accountId: uid,
      projectId: PROJECT_ID,
      region: REGION,
      backendSchemaVersion: BACKEND_SCHEMA_VERSION,
      protocolVersion: PROTOCOL_VERSION,
      ...result,
    };
  },
);

/**
 * Emulator-only inventory entitlement probe for Phase 3C.4C.
 *
 * This endpoint validates catalog-backed entitlement grant/revoke behavior,
 * duplicate ownership protection, idempotency, and immutable item events.
 * It refuses to run outside the local Functions + Firestore emulators.
 */
export const inventoryTestMutation = onCall(
  {
    region: REGION,
    maxInstances: 2,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    requireEmulatedFirestore("inventory.error.emulator_only");

    const data = request.data ?? {};

    if (!isInventoryOperation(data.operation)) {
      throw new HttpsError(
        "invalid-argument",
        "INVALID_INVENTORY_OPERATION",
        {
          errorKey: "inventory.error.invalid_operation",
        },
      );
    }

    const itemId = readRequiredString(
      data.itemId,
      "itemId",
      3,
      96,
      "inventory.error.invalid_request",
    );
    const reason = readRequiredString(
      data.reason,
      "reason",
      3,
      64,
      "inventory.error.invalid_request",
    );
    const transactionId = readRequiredString(
      data.transactionId,
      "transactionId",
      8,
      128,
      "inventory.error.invalid_request",
    );
    const idempotencyKey = readRequiredString(
      data.idempotencyKey,
      "idempotencyKey",
      8,
      128,
      "inventory.error.invalid_request",
    );

    const input: InventoryMutationInput = {
      uid,
      itemId,
      operation: data.operation,
      reason,
      transactionId,
      idempotencyKey,
      source: "phase_3c_4c_emulator_test",
    };

    const result = await applyInventoryMutation(input);

    logger.info("AtlasBoard inventoryTestMutation completed.", {
      accountId: uid,
      itemId: result.itemId,
      itemType: result.itemType,
      operation: result.operation,
      ownedAfter: result.ownedAfter,
      applied: result.applied,
      idempotentReplay: result.idempotentReplay,
      eventId: result.eventId,
    });

    return {
      ok: true,
      accountId: uid,
      projectId: PROJECT_ID,
      region: REGION,
      backendSchemaVersion: BACKEND_SCHEMA_VERSION,
      protocolVersion: PROTOCOL_VERSION,
      ...result,
    };
  },
);

/**
 * Emulator-only wallet-backed commerce purchase probe for Phase 3C.4D.
 *
 * The caller supplies only the item and wallet payment method. Price is read
 * from the trusted catalog document on the backend. Wallet debit, immutable
 * ledger, entitlement grant, purchase history, and commerce event are committed
 * in one Firestore transaction.
 */
export const commerceTestPurchase = onCall(
  {
    region: REGION,
    maxInstances: 2,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    requireEmulatedFirestore("commerce.error.emulator_only");

    const data = request.data ?? {};

    const itemId = readRequiredString(
      data.itemId,
      "itemId",
      3,
      96,
      "commerce.error.invalid_request",
    );

    if (!isWalletCurrencyId(data.paymentMethod)) {
      throw new HttpsError(
        "invalid-argument",
        "INVALID_PAYMENT_METHOD",
        {
          errorKey: "commerce.error.invalid_payment_method",
        },
      );
    }

    const idempotencyKey = readRequiredString(
      data.idempotencyKey,
      "idempotencyKey",
      8,
      128,
      "commerce.error.invalid_request",
    );

    const input: CommercePurchaseInput = {
      uid,
      itemId,
      paymentMethod: data.paymentMethod,
      idempotencyKey,
      source: "phase_3c_4d_emulator_test",
    };

    const result = await applyCommercePurchase(input);

    logger.info("AtlasBoard commerceTestPurchase completed.", {
      accountId: uid,
      transactionId: result.transactionId,
      itemId: result.itemId,
      paymentMethod: result.paymentMethod,
      amount: result.amount,
      status: result.status,
      entitlementStatus: result.entitlementStatus,
      applied: result.applied,
      idempotentReplay: result.idempotentReplay,
    });

    if (result.status === "failed") {
      const errorCode =
        result.failureReason === "already_owned" ?
          "already-exists" :
          "failed-precondition";

      throw new HttpsError(
        errorCode,
        "COMMERCE_PURCHASE_FAILED",
        {
          errorKey:
            result.failureErrorKey ??
            "commerce.error.purchase_failed",
          transactionId: result.transactionId,
          failureReason: result.failureReason,
        },
      );
    }

    return {
      ok: true,
      accountId: uid,
      projectId: PROJECT_ID,
      region: REGION,
      backendSchemaVersion: BACKEND_SCHEMA_VERSION,
      protocolVersion: PROTOCOL_VERSION,
      ...result,
    };
  },
);

/**
 * Emulator-only refund probe for Phase 3C.4D.
 *
 * Refund credit, immutable wallet ledger, entitlement revoke, commerce status
 * transition, and immutable refund event are committed atomically.
 */
export const commerceTestRefund = onCall(
  {
    region: REGION,
    maxInstances: 2,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    requireEmulatedFirestore("commerce.error.emulator_only");

    const data = request.data ?? {};

    const transactionId = readRequiredString(
      data.transactionId,
      "transactionId",
      32,
      128,
      "commerce.error.invalid_request",
    );
    const idempotencyKey = readRequiredString(
      data.idempotencyKey,
      "idempotencyKey",
      8,
      128,
      "commerce.error.invalid_request",
    );

    const input: CommerceRefundInput = {
      uid,
      transactionId,
      idempotencyKey,
      source: "phase_3c_4d_emulator_test",
    };

    const result = await applyCommerceRefund(input);

    logger.info("AtlasBoard commerceTestRefund completed.", {
      accountId: uid,
      transactionId: result.transactionId,
      itemId: result.itemId,
      amount: result.amount,
      status: result.status,
      entitlementStatus: result.entitlementStatus,
      applied: result.applied,
      idempotentReplay: result.idempotentReplay,
    });

    return {
      ok: true,
      accountId: uid,
      projectId: PROJECT_ID,
      region: REGION,
      backendSchemaVersion: BACKEND_SCHEMA_VERSION,
      protocolVersion: PROTOCOL_VERSION,
      ...result,
    };
  },
);

/**
 * Emulator-only promo redemption probe for Phase 3C.4E.
 *
 * The raw code is normalized and hashed for lookup; the client never chooses
 * reward amounts. Promo window, enabled state, global/account limits, wallet
 * rewards, event-specific currencies/tickets, inventory grants, immutable
 * ledgers, and redemption audit events are enforced atomically on the backend.
 */
export const promoTestRedeem = onCall(
  {
    region: REGION,
    maxInstances: 2,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    requireEmulatedFirestore("promo.error.emulator_only");

    const data = request.data ?? {};

    const code = readRequiredString(
      data.code,
      "code",
      4,
      32,
      "promo.error.invalid_code",
    );
    const idempotencyKey = readRequiredString(
      data.idempotencyKey,
      "idempotencyKey",
      8,
      128,
      "promo.error.invalid_request",
    );

    const input: PromoRedeemInput = {
      uid,
      code,
      idempotencyKey,
      source: "phase_3c_4e_emulator_test",
    };

    const result = await applyPromoRedemption(input);

    logger.info("AtlasBoard promoTestRedeem completed.", {
      accountId: uid,
      promoId: result.promoId,
      redemptionId: result.redemptionId,
      redemptionCount: result.redemptionCount,
      globalRedemptionCount: result.globalRedemptionCount,
      rewardTypes: result.rewards.map((reward) => reward.type),
      applied: result.applied,
      idempotentReplay: result.idempotentReplay,
    });

    return {
      ok: true,
      accountId: uid,
      projectId: PROJECT_ID,
      region: REGION,
      backendSchemaVersion: BACKEND_SCHEMA_VERSION,
      protocolVersion: PROTOCOL_VERSION,
      ...result,
    };
  },
);


/**
 * Emulator-only account bootstrap for Unity lobby integration tests.
 *
 * The Auth Emulator creates the temporary identity. This callable only ensures
 * the minimal canonical account/profile documents required by lobby authority.
 * It refuses to run without BOTH Functions and Firestore emulators.
 */
export const lobbyDevEnsureAccount = onCall(
  {
    region: REGION,
    maxInstances: 2,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    requireEmulatedFirestore("lobby.error.emulator_only");
    const data = request.data ?? {};

    const displayName = readRequiredString(
      data.displayName,
      "displayName",
      1,
      40,
      "lobby.error.invalid_request",
    ).trim();

    const db = getFirestore();
    const timestamp = FieldValue.serverTimestamp();

    await db.runTransaction(async (transaction) => {
      transaction.set(
        db.collection("users").doc(uid),
        {
          accountId: uid,
          accountStatus: "active",
          countryCode: "US",
          preferredLanguage: "EN",
          schemaVersion: 1,
          createdAt: timestamp,
          updatedAt: timestamp,
        },
        {merge: true},
      );

      transaction.set(
        db.collection("public_profiles").doc(uid),
        {
          displayName,
          schemaVersion: 1,
          createdAt: timestamp,
          updatedAt: timestamp,
        },
        {merge: true},
      );
    });

    logger.info("AtlasBoard lobby dev account ensured.", {
      accountId: uid,
      displayName,
    });

    return {
      ok: true,
      accountId: uid,
      projectId: PROJECT_ID,
      region: REGION,
      backendSchemaVersion: BACKEND_SCHEMA_VERSION,
      protocolVersion: PROTOCOL_VERSION,
    };
  },
);

/**
 * Production-intended private-lobby create entry point for Phase 3D.
 *
 * The raw six-digit room code is returned only to the creating client. The
 * backend persists only an HMAC-protected lookup document. In production this
 * function refuses code allocation until ATLAS_JOIN_CODE_PEPPER is configured.
 */
export const lobbyCreatePrivateRoom = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    const data = request.data ?? {};

    const settings: LobbySettingsInput = {
      mapId: data.mapId as string,
      themeId: data.themeId as string,
      roundLimit: data.roundLimit as number,
      maxPlayers: data.maxPlayers as number,
      balancedDevelopment: data.balancedDevelopment as boolean,
      doublesEnabled: data.doublesEnabled as boolean,
      tripleDoublePenaltyEnabled:
        data.tripleDoublePenaltyEnabled as boolean,
    };

    const versions: LobbyVersionInfo = {
      gameVersion: data.gameVersion as string,
      protocolVersion: data.protocolVersion as number,
      rulesVersion: data.rulesVersion as number,
      contentVersion: data.contentVersion as string,
      regionId: data.regionId as string,
    };

    const result = await createPrivateLobby({
      uid,
      settings,
      versions,
    });

    logger.info("AtlasBoard private lobby created.", {
      accountId: uid,
      lobbyId: result.snapshot.lobbyId,
      settingsRevision: result.snapshot.settingsRevision,
      openOnlineSeatCount:
        result.snapshot.openOnlineSeatCount,
      maxPlayers: result.snapshot.maxPlayers,
    });

    return {
      ok: true,
      projectId: PROJECT_ID,
      region: REGION,
      backendSchemaVersion: BACKEND_SCHEMA_VERSION,
      protocolVersion: PROTOCOL_VERSION,
      roomCode: result.roomCode,
      snapshot: result.snapshot,
    };
  },
);

/**
 * Creates a PUBLIC waiting lobby and its sanitized discovery projection.
 * The host still receives a protected six-digit room code for invite/reconnect
 * UX, but that code is never placed in browser cards.
 */
export const lobbyCreatePublicRoom = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    const data = request.data ?? {};

    const settings: LobbySettingsInput = {
      mapId: data.mapId as string,
      themeId: data.themeId as string,
      roundLimit: data.roundLimit as number,
      maxPlayers: data.maxPlayers as number,
      balancedDevelopment: data.balancedDevelopment as boolean,
      doublesEnabled: data.doublesEnabled as boolean,
      tripleDoublePenaltyEnabled:
        data.tripleDoublePenaltyEnabled as boolean,
    };

    const versions: LobbyVersionInfo = {
      gameVersion: data.gameVersion as string,
      protocolVersion: data.protocolVersion as number,
      rulesVersion: data.rulesVersion as number,
      contentVersion: data.contentVersion as string,
      regionId: data.regionId as string,
    };

    const result = await createPublicLobby({
      uid,
      settings,
      versions,
    });

    logger.info("AtlasBoard public lobby created.", {
      accountId: uid,
      lobbyId: result.snapshot.lobbyId,
      settingsRevision: result.snapshot.settingsRevision,
      openOnlineSeatCount: result.snapshot.openOnlineSeatCount,
      maxPlayers: result.snapshot.maxPlayers,
    });

    return {
      ok: true,
      projectId: PROJECT_ID,
      region: REGION,
      backendSchemaVersion: BACKEND_SCHEMA_VERSION,
      protocolVersion: PROTOCOL_VERSION,
      roomCode: result.roomCode,
      snapshot: result.snapshot,
    };
  },
);

/**
 * Lists sanitized joinable public lobby cards for the browser.
 * No room code, account id, join-code hash, Ready state, or member details are
 * returned. Discovery data is not authoritative for a future Join.
 */
export const lobbyListPublicRooms = onCall(
  {
    region: REGION,
    maxInstances: 30,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
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

    return {
      ok: true,
      rooms,
    };
  },
);

/**
 * Joins a public lobby directly from a sanitized browser card. The canonical
 * lobby is re-read transactionally; the browser row is never treated as
 * authority for capacity or access.
 */
export const lobbyJoinPublicRoom = onCall(
  {
    region: REGION,
    maxInstances: 20,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    const data = request.data ?? {};
    const versions: LobbyVersionInfo = {
      gameVersion: data.gameVersion as string,
      protocolVersion: data.protocolVersion as number,
      rulesVersion: data.rulesVersion as number,
      contentVersion: data.contentVersion as string,
      regionId: data.regionId as string,
    };

    const result = await joinPublicLobby({
      uid,
      lobbyId: data.lobbyId as string,
      password: data.password as string,
      idempotencyKey: data.idempotencyKey as string,
      versions,
    });

    logger.info("AtlasBoard public lobby join completed.", {
      accountId: uid,
      lobbyId: result.snapshot.lobbyId,
      idempotentReplay: result.idempotentReplay,
    });

    return {
      ok: true,
      idempotentReplay: result.idempotentReplay,
      roomCode: result.roomCode,
      snapshot: result.snapshot,
    };
  },
);

/**
 * Resolves a protected six-digit room code and atomically reserves a seat.
 */
export const lobbyJoinByCode = onCall(
  {
    region: REGION,
    maxInstances: 20,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    const data = request.data ?? {};

    const versions: LobbyVersionInfo = {
      gameVersion: data.gameVersion as string,
      protocolVersion: data.protocolVersion as number,
      rulesVersion: data.rulesVersion as number,
      contentVersion: data.contentVersion as string,
      regionId: data.regionId as string,
    };

    const result = await joinLobbyByCode({
      uid,
      roomCode: data.roomCode as string,
      password: data.password as string,
      idempotencyKey: data.idempotencyKey as string,
      versions,
    });

    logger.info("AtlasBoard private lobby join completed.", {
      accountId: uid,
      lobbyId: result.snapshot.lobbyId,
      idempotentReplay: result.idempotentReplay,
    });

    return {
      ok: true,
      projectId: PROJECT_ID,
      region: REGION,
      backendSchemaVersion: BACKEND_SCHEMA_VERSION,
      protocolVersion: PROTOCOL_VERSION,
      idempotentReplay: result.idempotentReplay,
      snapshot: result.snapshot,
    };
  },
);

/**
 * Host-only room password/access update shared by Private and Public rooms.
 */
export const lobbyUpdatePassword = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    const data = request.data ?? {};
    const result = await updateLobbyPassword({
      uid,
      lobbyId: data.lobbyId as string,
      expectedSettingsRevision: data.expectedSettingsRevision as number,
      password: data.password as string,
    });

    return {ok: true, applied: result.applied, snapshot: result.snapshot};
  },
);

/**
 * Host closes an unstarted lobby when returning to the Main Menu.
 */
export const lobbyCloseRoom = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    const data = request.data ?? {};
    const result = await closeLobby({
      uid,
      lobbyId: data.lobbyId as string,
    });

    logger.info("AtlasBoard lobby closed by host.", {
      accountId: uid,
      lobbyId: result.snapshot.lobbyId,
      applied: result.applied,
    });

    return {ok: true, applied: result.applied, snapshot: result.snapshot};
  },
);

/**
 * Host-only rule update. Every real change advances settingsRevision.
 */
export const lobbyUpdateSettings = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    const data = request.data ?? {};

    const result = await updateLobbySettings({
      uid,
      lobbyId: data.lobbyId as string,
      expectedSettingsRevision:
        data.expectedSettingsRevision as number,
      settings: {
        mapId: data.mapId as string,
        themeId: data.themeId as string,
        roundLimit: data.roundLimit as number,
        balancedDevelopment: data.balancedDevelopment as boolean,
        doublesEnabled: data.doublesEnabled as boolean,
        tripleDoublePenaltyEnabled:
          data.tripleDoublePenaltyEnabled as boolean,
      },
    });

    logger.info("AtlasBoard lobby settings update completed.", {
      accountId: uid,
      lobbyId: result.snapshot.lobbyId,
      settingsRevision: result.snapshot.settingsRevision,
      applied: result.applied,
    });

    return {
      ok: true,
      applied: result.applied,
      snapshot: result.snapshot,
    };
  },
);

/**
 * Host-only fixed-slot policy update.
 *
 * P1 is always Host/Local. P2-P4 policies are Online, Local Human, Bot, or
 * Inactive when outside maxPlayers. Existing connected Remote Humans are
 * preserved when the requested policy remains Online.
 */
export const lobbyConfigureSeats = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    const data = request.data ?? {};

    const seatPolicies =
      Array.isArray(data.seatPolicies) ?
        data.seatPolicies as LobbySeatPolicy[] :
        [];

    const result = await configureLobbySeats({
      uid,
      lobbyId: data.lobbyId as string,
      expectedSettingsRevision:
        data.expectedSettingsRevision as number,
      maxPlayers: data.maxPlayers as number,
      seatPolicies,
    });

    logger.info("AtlasBoard lobby seat configuration completed.", {
      accountId: uid,
      lobbyId: result.snapshot.lobbyId,
      settingsRevision: result.snapshot.settingsRevision,
      maxPlayers: result.snapshot.maxPlayers,
      openOnlineSeatCount: result.snapshot.openOnlineSeatCount,
      localHumanCount: result.snapshot.localHumanCount,
      botCount: result.snapshot.botCount,
      applied: result.applied,
    });

    return {
      ok: true,
      applied: result.applied,
      snapshot: result.snapshot,
    };
  },
);

/**
 * Sets Ready for a connected Remote Human only.
 *
 * Host, Local Humans, and Bots never Ready. Ready never auto-starts a match;
 * only the host-owned lobbyStartMatch callable may transition Waiting ->
 * Starting.
 */
export const lobbySetReady = onCall(
  {
    region: REGION,
    maxInstances: 20,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    const data = request.data ?? {};

    const result = await setLobbyReady({
      uid,
      lobbyId: data.lobbyId as string,
      expectedSettingsRevision:
        data.expectedSettingsRevision as number,
      ready: data.ready as boolean,
    });

    logger.info("AtlasBoard lobby ready update completed.", {
      accountId: uid,
      lobbyId: result.snapshot.lobbyId,
      settingsRevision: result.snapshot.settingsRevision,
      lifecycleState: result.snapshot.lifecycleState,
      applied: result.applied,
    });

    return {
      ok: true,
      applied: result.applied,
      snapshot: result.snapshot,
    };
  },
);

/**
 * Host-only remote player removal.
 *
 * The backend resets the concrete seat to OpenOnline, records a lobby-specific
 * kick so the removed account cannot immediately rejoin with the same room
 * code, and increments settingsRevision.
 */
export const lobbyKickMember = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    const data = request.data ?? {};

    const result = await kickLobbyMember({
      uid,
      lobbyId: data.lobbyId as string,
      expectedSettingsRevision:
        data.expectedSettingsRevision as number,
      slotIndex: data.slotIndex as number,
    });

    logger.info("AtlasBoard lobby remote member removed.", {
      accountId: uid,
      lobbyId: result.snapshot.lobbyId,
      settingsRevision: result.snapshot.settingsRevision,
      slotIndex: data.slotIndex,
      applied: result.applied,
    });

    return {
      ok: true,
      applied: result.applied,
      snapshot: result.snapshot,
    };
  },
);

/**
 * Remote member voluntary leave.
 */
export const lobbyLeaveRoom = onCall(
  {
    region: REGION,
    maxInstances: 20,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    const data = request.data ?? {};

    const result = await leaveLobby({
      uid,
      lobbyId: data.lobbyId as string,
    });

    logger.info("AtlasBoard lobby remote member left voluntarily.", {
      accountId: uid,
      lobbyId: result.snapshot.lobbyId,
      settingsRevision: result.snapshot.settingsRevision,
      applied: result.applied,
    });

    return {
      ok: true,
      applied: result.applied,
      snapshot: result.snapshot,
    };
  },
);

/**
 * Host-only authoritative start.
 *
 * The backend requires every connected Remote Human to be Ready for the
 * current settingsRevision. Local Humans and Bots never participate in the
 * Ready gate. Any unresolved OpenOnline seat is atomically converted to a Bot
 * when Start is accepted.
 */
export const lobbyStartMatch = onCall(
  {
    region: REGION,
    maxInstances: 10,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    const data = request.data ?? {};

    const result = await startLobbyMatch({
      uid,
      lobbyId: data.lobbyId as string,
      expectedSettingsRevision:
        data.expectedSettingsRevision as number,
    });

    logger.info("AtlasBoard lobby host start completed.", {
      accountId: uid,
      lobbyId: result.snapshot.lobbyId,
      lifecycleState: result.snapshot.lifecycleState,
      started: result.started,
      idempotentReplay: result.idempotentReplay,
      matchId: result.snapshot.matchId,
      startEventId: result.snapshot.startEventId,
    });

    return {
      ok: true,
      started: result.started,
      idempotentReplay: result.idempotentReplay,
      snapshot: result.snapshot,
    };
  },
);

/**
 * Returns a member-authorized server snapshot. Direct Firestore client lobby
 * reads remain closed in the current security rules.
 */
export const lobbyGetSnapshot = onCall(
  {
    region: REGION,
    maxInstances: 20,
    enforceAppCheck: false,
  },
  async (request) => {
    const uid = requireAuthenticatedUid(request);
    const data = request.data ?? {};

    const snapshot = await getLobbySnapshot({
      uid,
      lobbyId: data.lobbyId as string,
    });

    return {
      ok: true,
      snapshot,
    };
  },
);
