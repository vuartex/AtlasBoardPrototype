/**
 * AtlasBoard Firebase Functions deployment entrypoint.
 *
 * Explicitly enumerate the complete callable surface. A wildcard re-export
 * of index.ts causes ESLint import/export duplicate-name diagnostics when a
 * Security Core replacement retains the same public callable name.
 *
 * The guarded Security Core 01, 03 and 04 implementations replace only the
 * affected callables. Every other Phase 15/16 function remains exported with
 * its original public name and callable contract.
 */
export {
  matchHostPublishState,
  progressionRecordCompletedMatch,
} from "./security/match-integrity-gates";

export {metaSeasonalClaimTrackTier} from "./security/seasonal-track-integrity";
export {lobbyListPublicRooms} from "./security/legacy-public-discovery-gate";

// Unmodified canonical callables from index.ts: keep the public API stable.
export {
  economyHealthCheck,
  walletTestMutation,
  inventoryTestMutation,
  commerceTestPurchase,
  commerceTestRefund,
  promoTestRedeem,
  metaDailyRewardClaim,
  metaSeasonalGetActive,
  metaSeasonalGetOverview,
  metaSeasonalClaimChallenge,
  metaSeasonalPurchaseLimitedItem,
  progressionGetProfile,
  metaDevResetPurchasedEntitlements,
  lobbyDevEnsureAccount,
  lobbyCreatePrivateRoom,
  lobbyCreatePublicRoom,
  lobbyJoinPublicRoom,
  lobbyJoinByCode,
  lobbyUpdatePassword,
  lobbyCloseRoom,
  lobbyHostHeartbeat,
  lobbyUpdateSettings,
  lobbyConfigureSeats,
  lobbySetPawnCosmetic,
  lobbySetReady,
  lobbyKickMember,
  lobbyLeaveRoom,
  lobbyStartMatch,
  lobbyGetSnapshot,
  matchGetSnapshot,
  matchTouchPresence,
  matchHostSweepDisconnected,
  matchTryRecoverCrashedHost,
  matchLeaveActive,
  matchHostMarkAfkRemoved,
  matchHostExpireReconnects,
  matchHostPrepareRematch,
  matchSubmitIntent,
  matchHostListPendingIntents,
  matchHostAcknowledgeIntents,
  lobbyChatSend,
  lobbyChatList,
  matchChatSend,
  matchChatList,
  platformSteamDevLinkCurrentAccount,
  platformSteamDevReturningSignIn,
  platformSteamGetLinkStatus,
  platformSteamLinkCurrentAccount,
  platformSteamReturningSignIn,
  platformSteamCreateAppCheckToken,
} from "./index";

export {
  lobbyListPublicRoomsPage,
} from "./lobby/public-lobby-pagination-callable";
