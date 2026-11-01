const fs = require('fs');
const assert = require('assert');
const auction = fs.readFileSync('Assets/Project/Scripts/UI/AtlasBoardAuctionPresentationPolish.cs','utf8');
const bot = fs.readFileSync('Assets/Project/Scripts/Players/BotPlayerController.cs','utf8');

const checks = [];
function ok(name, cond) { assert.ok(cond, name); checks.push(name); }

ok('auction canonical parent is TabletRoot', auction.includes('rootObject.transform.SetParent(\n            tabletRoot,\n            false);'));
ok('auction full TabletRoot stretch restored', auction.includes('polishRoot.anchorMin =\n            Vector2.zero;') && auction.includes('polishRoot.anchorMax =\n            Vector2.one;') && auction.includes('NormalizeModernGeometry();'));
ok('nested property binding preserved', auction.includes('PropertyCard/PropertyName') && auction.includes('PropertyCard/PropertyMeta'));
ok('nested bid binding preserved', auction.includes('CurrentBidHero/CurrentBidLabel') && auction.includes('CurrentBidHero/CurrentBidValue'));
ok('legacy auction graphics are hidden while modern view owns presentation', auction.includes('private void HideLegacyPresentation()') && auction.includes('SetActive(\n            legacyTitle,\n            false);') && auction.includes('SetActive(\n            legacyPassButton,\n            false);'));
ok('modern geometry diagnostic retained', auction.includes('ATB_AUCTION_LAYOUT stage='));

ok('bot watchdog threshold present', bot.includes('DecisionRoutineWatchdogSeconds = 8f'));
ok('bot watchdog recovery present', bot.includes('ATB_BOT_DECISION_WATCHDOG recovered=true'));
ok('bot decisions are tracked', ['purchase','auction','event_continue','special_continue','roll'].every(k => bot.includes('"'+k+'"')));
ok('event continue readiness gate used', bot.includes('.CanBotContinueEventFor(playerState)'));
ok('purchase rejection diagnostic present', bot.includes('ATB_BOT_DECISION_RETRY kind=purchase'));
ok('watchdog does not directly force resolution', !/RecoverStalledDecisionRoutine[\s\S]*?\{[\s\S]*?(CompleteResolution|PassCurrentBidder|BuyPendingTile|SkipPendingTile)\s*\(/.test(bot));

console.log(`ATB_PHASE19AW_SOURCE_GUARD PASS checks=${checks.length}/${checks.length} live_unity=NOT_RUN two_client=NOT_RUN`);
