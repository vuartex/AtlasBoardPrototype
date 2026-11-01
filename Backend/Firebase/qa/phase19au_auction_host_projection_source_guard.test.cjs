const fs = require('fs');
const assert = require('assert');

const auction = fs.readFileSync('Assets/Project/Scripts/UI/AtlasBoardAuctionPresentationPolish.cs','utf8');
const lobby = fs.readFileSync('Assets/Project/Scripts/UI/Menu/AtlasBoardPrivateLobbyUIController.cs','utf8');

const checks = [
  ['auction canonical parent', auction.includes('auctionPanel.transform as RectTransform')],
  ['detached modern roots removed', auction.includes('ATB_AUCTION_MODERN detached_roots_removed=')],
  ['binding diagnostics', auction.includes('ATB_AUCTION_BIND ')],
  ['canonical failure diagnostic', auction.includes('canonical_parent=Panel_Auction')],
  ['legacy graphics disabled', auction.includes('graphic.enabled = false')],
  ['modern graphics preserved', auction.includes('graphic.transform.IsChildOf(polishRoot)')],
  ['legacy button state preserved', auction.includes('Do NOT change legacy Button.interactable here')],
  ['viewport fills canonical panel', auction.includes('polishRoot.anchorMin = new Vector2(0f, 0f)') && auction.includes('polishRoot.anchorMax = new Vector2(1f, 1f)')],
  ['seat identity cache', lobby.includes('seatIdentityAccountCache') && lobby.includes('seatIdentityNameCache')],
  ['duplicate host suppression', lobby.includes('duplicate_host_projection=true')],
  ['host slot resolver', lobby.includes('ResolveHostSlotIndex')],
  ['cache update on snapshot', lobby.includes('UpdateSeatIdentityPresentationCache(snapshot)')],
];
for (const [name, ok] of checks) assert.ok(ok, name);
console.log(`ATB_PHASE19AU_SOURCE_GUARD PASS checks=${checks.length} LIVE_UNITY=NOT_RUN TWO_CLIENT=NOT_RUN`);
