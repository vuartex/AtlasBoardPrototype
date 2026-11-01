const fs = require('fs');
const assert = require('assert');

const auction = fs.readFileSync('Assets/Project/Scripts/UI/AtlasBoardAuctionPresentationPolish.cs','utf8');

const checks = [
  ['property name resolves through card', auction.includes('"PropertyCard/PropertyName"')],
  ['property meta resolves through card', auction.includes('"PropertyCard/PropertyMeta"')],
  ['bid label resolves through hero', auction.includes('"CurrentBidHero/CurrentBidLabel"')],
  ['bid value resolves through hero', auction.includes('"CurrentBidHero/CurrentBidValue"')],
  ['old direct property name binding removed', !auction.includes('polishRoot,\n                "PropertyName"')],
  ['old direct property meta binding removed', !auction.includes('polishRoot,\n                "PropertyMeta"')],
  ['old direct bid label binding removed', !auction.includes('polishRoot,\n                "CurrentBidLabel"')],
  ['old direct bid value binding removed', !auction.includes('polishRoot,\n                "CurrentBidValue"')],
  ['incomplete bind diagnostic retained', auction.includes('incomplete_after_rebuild')],
  ['canonical panel retained', auction.includes('canonical_parent=Panel_Auction')],
];
for (const [name, ok] of checks) assert.ok(ok, name);
console.log(`ATB_PHASE19AV_AUCTION_BINDING_PATH PASS checks=${checks.length} LIVE_UNITY=NOT_RUN TWO_CLIENT=NOT_RUN`);
