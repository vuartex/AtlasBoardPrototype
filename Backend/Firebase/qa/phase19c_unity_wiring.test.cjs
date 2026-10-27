'use strict';

// Source integration smoke: catches a secure RTDB backend being delivered
// without any of the Unity UI callers actually using it.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '../../..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');
const bridge = read('Assets/Project/Scripts/Meta/AtlasBoardMetaEconomyBridge.cs');
const ui = read('Assets/Project/Scripts/UI/Meta/AtlasBoardMetaEconomyUI.cs');
const inventory = read('Assets/Project/Scripts/UI/Progression/AtlasBoardProgressionUI.cs');
const menu = read('Assets/Project/Scripts/UI/Menu/AtlasBoardMainMenuController.cs');
const live = read('Assets/Project/Scripts/Meta/AtlasBoardRtdbMetaEconomy.cs');
const rules = JSON.parse(read('Backend/Firebase/database.rules.json'));
const authUi = read('Assets/Project/Scripts/Online/Firebase/AtlasBoardAccountAccessUI.cs');
const mirror = read('Assets/Project/Scripts/Online/Firebase/AtlasBoardRtdbUserProfileMirror.cs');
const editor = read('Assets/Project/Editor/AtlasBoardPhase19CRtdbMetaAudit.cs');
const header = read('Assets/Project/Scripts/UI/Menu/AtlasBoardCanonicalProfileHeader.cs');
const roadmap = read('Assets/Project/Documentation/MASTER_ROADMAP.md');
// Complete owner project has this scene; slim ZIP QA can run before overlay.
const sceneFile = path.join(root, 'Assets/Board_Prototype.unity');
const scene = fs.existsSync(sceneFile) ? fs.readFileSync(sceneFile, 'utf8') : null;

let assertions = 0;
function check(value, message) { assert.ok(value, message); assertions++; }

check(bridge.includes('return await AtlasBoardRtdbMetaEconomy.LoadAsync();'), 'Bridge must read live wallet');
check(bridge.includes('return await AtlasBoardRtdbMetaEconomy.GrantDevAsync();'), 'Bridge must grant dev via RTDB');
check(bridge.includes('return await AtlasBoardRtdbMetaEconomy.ClaimDailyAsync();'), 'Bridge must claim daily via RTDB');
check(bridge.includes('return await AtlasBoardRtdbMetaEconomy.BuyAsync(itemId, paymentMethod);'), 'Bridge must buy via RTDB');
check(bridge.includes('return await AtlasBoardRtdbMetaEconomy.ResetDevAsync();'), 'Bridge must reset via RTDB');
check(ui.includes('PollLiveWalletAsync();'), 'Store should poll the live wallet after sign-in');
check(ui.includes('snapshot.WalletVerified'), 'Store must not render unverified wallet');
check(ui.includes('AtlasBoardRtdbMetaEconomy.IsRegistered'), 'Registered accounts should access live commerce');
check(ui.includes('snapshot.IsDeveloperTester'), 'Dev wallet button must be hidden for normal users');
check(inventory.includes('await AtlasBoardRtdbMetaEconomy.EquipAsync('), 'Profile Apply must authorize server equip');
check(inventory.includes('read.WalletVerified'), 'Owned cosmetics must come from verified wallet');
check(menu.includes('private int profileCash = 0;') && menu.includes('private int profileGold = 0;'), 'Main menu initializes neutral zero wallet');
check(!menu.includes('private int profileCash = 1500;') && !menu.includes('private int profileGold = 100;'), 'Stale scene defaults must be removed');
check(live.includes('"X-Firebase-ETag", "true"') && live.includes('"if-match", etag'), 'Wallet mutation requires ETag concurrency checks');
check(rules.rules.users.$uid['.write'] === false, 'User root cannot directly write wallet');
check(rules.rules.users.$uid.metaEconomy['.write'].includes('devWalletTesters'), 'Dev writes need server-admin allowlist');

check(ui.includes('frameBuy.gameObject.name = "ProfileBuy_"'), 'Profile frame must expose visible Buy button even for Guest');
check(ui.includes('if (guestAccount) claim.onClick.AddListener(OpenStoreSignIn);'), 'Guest daily card should lead to registration');
check(!ui.includes('AtlasBoardShopContentDraft.PreviewNotice('), 'Do not display misleading Purchases disabled banner in purchasable items');
check(ui.includes('bool payWithGold = item.GoldPrice > 0;'), 'Paid row picks Gold when the catalog provides a Gold price');
check(ui.includes('bool payWithCoin = !payWithGold && item.AtlasCoinPrice > 0;'), 'Atlas Coin is only selected when there is no Gold price');
check(ui.includes('buy.gameObject.name = "ItemBuy_" + item.ItemId;'), 'Standard item has one Buy action');
check(!ui.includes('Button coin = CreateSmallButton('), 'Second zero-cost Atlas Coin Buy must be removed');
check(inventory.includes('AtlasBoardRtdbMetaEconomy.CurrentUid == uid'), 'Career inventory uses the current authenticated RTDB UID');
check(!inventory.includes('AtlasBoardRtdbMetaEconomy.IsRegistered &&'), 'Career inventory must not reject admin-approved Steam RTDB wallets');
check(inventory.includes('item.Owned && !item.PreviewOnly'), 'Career only lists server-verified owned entitlements');
check(authUi.includes('public static bool OpenLoginForStore()'), 'Store must link to canonical account modal');
check(menu.includes('Mathf.Max(0, profileCash).ToString("N0")') && menu.includes('Mathf.Max(0, profileGold).ToString("N0")'), 'Settings and Main Menu render zero, never dashes');
check(live.includes("public static bool IsSteamSession"), "Steamworks provider detected separately from Firebase Auth");
check(live.includes("snapshot.IsSteamAccount = IsSteamSession;"), "Steam wallet carries provider identity");
check(live.includes("snapshot.IsGuest = identity.User.IsAnonymous && !snapshot.IsSteamAccount;"), "Steam is not presented as email Guest");
check(mirror.includes("user.IsAnonymous ? \"guest\" : \"registered\"".replaceAll("\\\"", "\"")), "Mirror writes Auth-correct account type");
check(editor.includes("ATB_PHASE19E_IDENTITY"), "Editor prints Steam and Firebase Auth separately");
check(menu.includes("private int profileCash = 0;"), "Initial currency is zero");
check(ui.includes("bool steamAccount = AtlasBoardRtdbMetaEconomy.IsSteamSession;"), "Daily UI recognizes Steam provider");
check(rules.rules.users.$uid.metaEconomy[".write"].includes("newData.child('operation/type').val() === 'INIT'"), "Anonymous zero-only init security exception");
check(header.includes('AtlasBoardShopProfileFrameArt.Create(\n                avatarTile,'), 'Equipped art must anchor to the full avatar tile, not avatar text or profile card');
check(!header.includes('avatarInitialText.transform.parent, frameId, 76f'), 'Legacy misplaced Main Menu art parent must be removed');
check(header.includes('FrameOverlayPrefix') && header.includes('hasCurrent'), 'One equipped frame per UI avatar, no duplicate decorations');
check(header.includes('initial.gameObject.name == "CareerHudInitial"'), 'Career-specific header avatar must receive same frame');
check(header.includes('initial.gameObject.name == "AvatarInitial"') && header.includes('parent.name == "ProfileCard"'), 'Store/Menu/Lobby profile banners use same frame');
check(header.includes('Resources.FindObjectsOfTypeAll<TMP_Text>()'), 'Later-created profile cards are reconciled');
check(header.includes('graphic.raycastTarget = false'), 'Equipped decoration does not block UI input');
check(header.includes('accountService.CurrentAccountId, ownerUid') && inventory.includes('header.NotifyVerifiedProfileFrameEquipped('), 'Successful Career Apply immediately updates current UID frame');
check(roadmap.includes('Board Theme — More Settings') && roadmap.includes('Kalıcı AtlasBoard proje kuralları'), 'Roadmap preserves Board setting request and owner invariants');
check(header.includes('child.gameObject.SetActive(false)') && header.includes('Destroy(child.gameObject)'), 'Old frame is removed on switch/default');
check(scene === null || (scene.includes('m_SizeDelta: {x: 118, y: 118}') && scene.includes('m_SizeDelta: {x: 95, y: 95}')), 'Menu/Lobby scene (when present) has full-size Avatar Image and smaller sibling AvatarInitial');
check(header.includes('parent.Find("Avatar")') && header.includes('parent.name == "CareerHudAvatar"'), 'Menu/Lobby, Store and Career resolve existing avatar Image geometry');
check(header.includes('float side = Mathf.Min(Mathf.Abs(rect.width), Mathf.Abs(rect.height));') && !header.includes('Mathf.Clamp(side - 2f, 36f, 88f)'), 'Artwork uses full avatar tile width, not legacy 88px upper cap');
check(header.includes('if (defaultAvatar != null) defaultAvatar.enabled = false;') && header.includes('text.enabled = false;'), 'Equipped frame fully replaces default blue tile and original initial');
check(header.includes('if (defaultAvatar != null) defaultAvatar.enabled = true;') && header.includes('text.enabled = true;'), 'Default/reset restores original avatar image and V text');
check(header.includes('RemoveTextAnchoredLegacyOverlays(text.rectTransform)') && header.includes('graphic.raycastTarget = false'), 'Legacy inset art is removed and full-size art never blocks input');
check(roadmap.includes('v5.49.9') && roadmap.includes('tam avatar'), 'Roadmap records full-tile default-avatar replacement and owner acceptance');
process.stdout.write(`ATB_PHASE19H_UNITY_WIRING_QA PASS assertions=${assertions} compile=UNITY_EDITOR_REQUIRED\n`);
