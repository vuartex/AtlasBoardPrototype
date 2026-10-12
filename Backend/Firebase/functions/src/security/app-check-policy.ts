/**
 * Phase 15F local staged App Check enforcement switch.
 *
 * Why this is a plain boolean rather than a Firebase BooleanParam:
 * firebase-functions evaluates callable `enforceAppCheck` while discovering
 * functions. Passing a BooleanParam here causes repeated deployment/discovery
 * `.value()` warnings and can stall local Functions Emulator discovery.
 *
 * Phase 15F therefore enables enforcement ONLY inside the Functions Emulator,
 * using `.env.local`:
 *
 *   ATLAS_ENFORCE_APP_CHECK=true
 *
 * Outside the Functions Emulator this value is always false. Production
 * enforcement remains deliberately disabled until the real Steam provider
 * acceptance / production rollout gate.
 */
const isFunctionsEmulator =
  process.env.FUNCTIONS_EMULATOR === "true";

const localEnforcementRequested =
  (process.env.ATLAS_ENFORCE_APP_CHECK ?? "")
    .trim()
    .toLowerCase() === "true";

export const ATLAS_ENFORCE_APP_CHECK =
  isFunctionsEmulator &&
  localEnforcementRequested;

export const APP_CHECK_PROTECTED_CALLABLES = [
  "economyHealthCheck",
  "metaDailyRewardClaim",
  "metaSeasonalGetActive",
  "metaSeasonalGetOverview",
  "metaSeasonalClaimChallenge",
  "metaSeasonalClaimTrackTier",
  "metaSeasonalPurchaseLimitedItem",
  "progressionGetProfile",
  "progressionRecordCompletedMatch",
  "lobbyCreatePrivateRoom",
  "lobbyCreatePublicRoom",
  "lobbyListPublicRooms",
  "lobbyListPublicRoomsPage",
  "lobbyJoinPublicRoom",
  "lobbyJoinByCode",
  "lobbyUpdatePassword",
  "lobbyCloseRoom",
  "lobbyHostHeartbeat",
  "lobbyUpdateSettings",
  "lobbyConfigureSeats",
  "lobbySetPawnCosmetic",
  "lobbySetReady",
  "lobbyKickMember",
  "lobbyLeaveRoom",
  "lobbyStartMatch",
  "lobbyGetSnapshot",
  "matchGetSnapshot",
  "matchTouchPresence",
  "matchHostSweepDisconnected",
  "matchTryRecoverCrashedHost",
  "matchLeaveActive",
  "matchHostMarkAfkRemoved",
  "matchHostExpireReconnects",
  "matchHostPrepareRematch",
  "matchSubmitIntent",
  "matchHostListPendingIntents",
  "matchHostPublishState",
  "matchHostAcknowledgeIntents",
  "lobbyChatSend",
  "lobbyChatList",
  "matchChatSend",
  "matchChatList",
  "platformSteamGetLinkStatus",
  "platformSteamLinkCurrentAccount",
  "platformSteamReturningSignIn",
] as const;

export const APP_CHECK_DEV_EXEMPT_CALLABLES = [
  "walletTestMutation",
  "inventoryTestMutation",
  "commerceTestPurchase",
  "commerceTestRefund",
  "promoTestRedeem",
  "metaDevResetPurchasedEntitlements",
  "lobbyDevEnsureAccount",
  "platformSteamDevLinkCurrentAccount",
  "platformSteamDevReturningSignIn",
] as const;

export const APP_CHECK_BOOTSTRAP_EXEMPT_CALLABLES = [
  "platformSteamCreateAppCheckToken",
] as const;
