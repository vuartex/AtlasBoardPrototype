const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '../../..');
const progression = fs.readFileSync(path.join(root,
  'Assets/Project/Scripts/UI/Progression/AtlasBoardProgressionUI.cs'), 'utf8');
const store = fs.readFileSync(path.join(root,
  'Assets/Project/Scripts/UI/Meta/AtlasBoardMetaEconomyUI.cs'), 'utf8');
const roadmap = fs.readFileSync(path.join(root,
  'Assets/Project/Documentation/MASTER_ROADMAP.md'), 'utf8');

let checks = 0;
function check(condition, message) {
  checks++;
  if (!condition) throw new Error('FAIL: ' + message);
}

check(progression.includes('new Vector2(-50f, 32f)') &&
      progression.includes('new Vector2(190f, 30f)'),
  'player identity region must sit to the right of the avatar');
check(progression.includes('new Vector2(-50f, 8f)') &&
      progression.includes('new Vector2(190f, 22f)'),
  'player role region must align with the repaired identity region');
check(progression.includes('new Vector2(135f, 32f)') &&
      progression.includes('new Vector2(160f, 30f)'),
  'localized result region must have dedicated 160px right column');
check(progression.includes('result.fontSizeMin = 7f;') &&
      progression.includes('result.fontSizeMax = 13f;') &&
      progression.includes('TextWrappingModes.NoWrap'),
  'localized result must autosize on one line');
check(!store.includes('$"  •  catalog v{Mathf.Max(1, item.CatalogVersion)}"'),
  'customer store row must not expose catalog version');
check(store.includes('type.text = ItemTypeDescriptor(item.ItemType);'),
  'store row must use customer-facing descriptor');
check(store.includes('type.enableAutoSizing = true;') && store.includes('type.fontSizeMin = 9f;') && store.includes('type.textWrappingMode = TextWrappingModes.NoWrap;'),
  'store customer metadata must remain localization-safe');
check(store.includes('"HARİTA • ÜLKE TAHTASI"') &&
      store.includes('"KARTE • LÄNDERSPIELBRETT"') &&
      store.includes('"КАРТА • ПОЛЕ СТРАНЫ"'),
  'map descriptor must be resolved customer copy in multiple locales');
check(store.includes('"PİYON • KOZMETİK"') &&
      store.includes('"PEÓN • COSMÉTICO"') &&
      store.includes('"PION • COSMÉTIQUE"') &&
      store.includes('"말 • 꾸미기"'),
  'pawn descriptor must cover supported locales');
check(store.includes('FindHistoryCatalogItem(itemId)') &&
      store.includes('InferHistoryItemType(itemId)'),
  'purchase history must resolve product and category from stable ItemId');
check(store.includes('int paid = Mathf.Abs(item.Amount);'),
  'purchase history must display positive paid amount');
check(store.includes('CurrencyLabel(item.CurrencyId)') &&
      !store.includes('$"{item.Amount:N0} " +\n                   $"{CurrencyLabel(item.CurrencyId)}  |  {item.Status}"'),
  'purchase history must show one charged currency without status clutter');
const goldPos = store.indexOf('storeGoldText.rectTransform,\n            new Vector2(70f, 36f)');
const coinPos = store.indexOf('storeCoinText.rectTransform,\n            new Vector2(70f, 3f)');
check(goldPos >= 0 && coinPos >= 0,
  'Store HUD must render Gold above Atlas Coin');
check(roadmap.includes('Main Menu footer no longer shows `Prototype • Main Menu v1`') &&
      roadmap.includes('Room Code header cleanup is accepted'),
  'owner-accepted bb items must be closed in roadmap');
check(roadmap.includes('v5.49.12bc') &&
      roadmap.includes('Atlas Store customer-facing item metadata'),
  'roadmap must record bc presentation checkpoint');

console.log('ATB_PHASE19BH_PLAYER_CARD_STORE_POLISH_SOURCE PASS checks=' + checks +
  ' playerCardColumns=true locales=7 storeCustomerCopy=true purchaseHistory=true hudOrder=true');
