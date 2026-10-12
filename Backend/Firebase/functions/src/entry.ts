// Phase 16I additive Functions entrypoint.
// Re-export every existing callable unchanged, then add the cursor-paginated
// public-lobby discovery callable. Keeping the legacy export preserves older
// client compatibility while the Unity browser moves to the new contract.
export * from "./index";
export {
  lobbyListPublicRoomsPage,
} from "./lobby/public-lobby-pagination-callable";
