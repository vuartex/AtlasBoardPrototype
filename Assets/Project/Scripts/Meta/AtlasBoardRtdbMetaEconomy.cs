using System;
using System.Text;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Firebase.Auth;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;


/// <summary>
/// Firebase RTDB meta wallet: all Auth identities get a zero wallet; only
/// registered or administrator-approved test identities may mutate balances.
/// Purchases, daily claims, equipment and developer resets are one ETag-guarded
/// PUT of /users/{uid}/metaEconomy. The matching RTDB rules verify every
/// balance and entitlement transition. Clients never receive direct write
/// permission to an unconstrained currency field.
/// </summary>
public static class AtlasBoardRtdbMetaEconomy
{
    private static readonly System.Collections.Generic.HashSet<string>
        ConfirmedMatchGoldKeys = new System.Collections.Generic.HashSet<string>(
            StringComparer.Ordinal);

    private const string Database =
        "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    private const long DayMs = 86400000L;
    private const int DailyGold = 50;
    private const int DevGold = 5000;
    private const int DevCoin = 500;


    private const string Halloween = "frame_halloween";
    private const string NewYear = "frame_new_year";
    private const string Turkiye = "frame_turkiye";
    private const string Usa = "frame_usa";
    private const string Midnight = "board_midnight_azure";
    private const string Maple = "board_ember_maple";
    private const string Aurora = "board_arctic_aurora";


    [Serializable]
    private sealed class Wallet
    {
        public int gold;
        public int atlasCoin;
        public bool starterGrantClaimed;
    }


    [Serializable]
    private sealed class Frames
    {
        public bool frame_halloween;
        public bool frame_new_year;
        public bool frame_turkiye;
        public bool frame_usa;
        public bool frame_neon_circuit;
        public bool frame_royal_crown;
        public bool frame_ocean_tide;
        public bool frame_sakura_blossom;
        public bool frame_cosmic_nova;
        public bool frame_forest_spirit;


        public bool IsOwned(string id)
        {
            switch (id)
            {
                case Halloween: return frame_halloween;
                case NewYear: return frame_new_year;
                case Turkiye: return frame_turkiye;
                case Usa: return frame_usa;
                case "frame_neon_circuit": return frame_neon_circuit;
                case "frame_royal_crown": return frame_royal_crown;
                case "frame_ocean_tide": return frame_ocean_tide;
                case "frame_sakura_blossom": return frame_sakura_blossom;
                case "frame_cosmic_nova": return frame_cosmic_nova;
                case "frame_forest_spirit": return frame_forest_spirit;
                default: return false;
            }
        }


        public void SetOwned(string id, bool owned)
        {
            switch (id)
            {
                case Halloween: frame_halloween = owned; break;
                case NewYear: frame_new_year = owned; break;
                case Turkiye: frame_turkiye = owned; break;
                case Usa: frame_usa = owned; break;
                case "frame_neon_circuit": frame_neon_circuit = owned; break;
                case "frame_royal_crown": frame_royal_crown = owned; break;
                case "frame_ocean_tide": frame_ocean_tide = owned; break;
                case "frame_sakura_blossom": frame_sakura_blossom = owned; break;
                case "frame_cosmic_nova": frame_cosmic_nova = owned; break;
                case "frame_forest_spirit": frame_forest_spirit = owned; break;
            }
        }


        public int RefundAmount()
        {
            return (frame_halloween ? 350 : 0) +
                   (frame_new_year ? 350 : 0) +
                   (frame_turkiye ? 400 : 0) +
                   (frame_usa ? 400 : 0) +
                   (frame_neon_circuit ? 450 : 0) +
                   (frame_royal_crown ? 500 : 0) +
                   (frame_ocean_tide ? 450 : 0) +
                   (frame_sakura_blossom ? 475 : 0) +
                   (frame_cosmic_nova ? 550 : 0) +
                   (frame_forest_spirit ? 475 : 0);
        }
    }


    [Serializable]
    private sealed class Boards
    {
        public bool board_midnight_azure;
        public bool board_ember_maple;
        public bool board_arctic_aurora;
        public bool board_atlas_horizon;
        public bool IsOwned(string id) =>
            id == Midnight && board_midnight_azure ||
            id == Maple && board_ember_maple ||
            id == Aurora && board_arctic_aurora ||
            id == "board_atlas_horizon" && board_atlas_horizon;
        public void SetOwned(string id, bool value)
        {
            if (id == Midnight) board_midnight_azure = value;
            if (id == Maple) board_ember_maple = value;
            if (id == Aurora) board_arctic_aurora = value;
            if (id == "board_atlas_horizon") board_atlas_horizon = value;
        }
        public int RefundAmount() =>
            (board_midnight_azure ? 1200 : 0) +
            (board_ember_maple ? 1500 : 0) +
            (board_arctic_aurora ? 1700 : 0);
    }


    [Serializable]
    private sealed class Maps
    {
        public bool map_germany;
        public bool map_mexico;
        public bool map_world;
        public bool IsOwned(string id) =>
            id == "map_germany" && map_germany || id == "map_mexico" && map_mexico ||
            id == "map_world" && map_world;
        public void SetOwned(string id, bool owned)
        {
            if (id == "map_germany") map_germany = owned;
            else if (id == "map_mexico") map_mexico = owned;
            else if (id == "map_world") map_world = owned;
        }
        public int RefundAmount() => (map_germany ? 30000 : 0) + (map_mexico ? 25000 : 0);
    }


    // Exact, audited RTDB dice catalog; paid cosmetic state stays in the
    // existing per-UID metaEconomy ETag transaction (never a second wallet).
    [Serializable]
    private sealed class Dice
    {
        public bool dice_ruby_red;
        public bool dice_ocean_blue;
        public bool dice_midnight_black;
        public bool dice_marble;
        public bool IsOwned(string id) =>
            id == "dice_ruby_red" && dice_ruby_red ||
            id == "dice_ocean_blue" && dice_ocean_blue ||
            id == "dice_midnight_black" && dice_midnight_black ||
            id == "dice_marble" && dice_marble;
        public void SetOwned(string id, bool owned)
        {
            if (id == "dice_ruby_red") dice_ruby_red = owned;
            else if (id == "dice_ocean_blue") dice_ocean_blue = owned;
            else if (id == "dice_midnight_black") dice_midnight_black = owned;
            else if (id == "dice_marble") dice_marble = owned;
        }
        public int RefundAmount() =>
            (dice_ruby_red ? 350 : 0) + (dice_ocean_blue ? 350 : 0) +
            (dice_midnight_black ? 400 : 0) + (dice_marble ? 400 : 0);
    }


    [Serializable]
    private sealed class Pawns
    {
        public bool ghost_01;
        public bool ghost_02;
        public bool ghost_03;
        public bool ghost_04;
        public bool ghost_05;
        public bool ghost_06;
        public bool ghost_07;
        public bool vehicle_police;
        public bool vehicle_taxi;
        public bool vehicle_pickup;
        public bool IsOwned(string id) =>
            id == "ghost_01" && ghost_01 ||
            id == "ghost_02" && ghost_02 ||
            id == "ghost_03" && ghost_03 ||
            id == "ghost_04" && ghost_04 ||
            id == "ghost_05" && ghost_05 ||
            id == "ghost_06" && ghost_06 ||
            id == "ghost_07" && ghost_07 ||
            id == "vehicle_police" && vehicle_police ||
            id == "vehicle_taxi" && vehicle_taxi ||
            id == "vehicle_pickup" && vehicle_pickup;
        public void SetOwned(string id, bool owned)
        {
            if (id == "ghost_01") ghost_01 = owned;
            if (id == "ghost_02") ghost_02 = owned;
            if (id == "ghost_03") ghost_03 = owned;
            if (id == "ghost_04") ghost_04 = owned;
            if (id == "ghost_05") ghost_05 = owned;
            if (id == "ghost_06") ghost_06 = owned;
            if (id == "ghost_07") ghost_07 = owned;
            if (id == "vehicle_police") vehicle_police = owned;
            if (id == "vehicle_taxi") vehicle_taxi = owned;
            if (id == "vehicle_pickup") vehicle_pickup = owned;
        }
        public int RefundAmount() =>
            (ghost_01 ? 300 : 0) +
            (ghost_02 ? 300 : 0) +
            (ghost_03 ? 300 : 0) +
            (ghost_04 ? 300 : 0) +
            (ghost_05 ? 300 : 0) +
            (ghost_06 ? 300 : 0) +
            (ghost_07 ? 300 : 0) +
            (vehicle_police ? 450 : 0) +
            (vehicle_taxi ? 400 : 0) +
            (vehicle_pickup ? 425 : 0);
    }


    [Serializable]
    private sealed class Emotes
    {
        public bool emote_alert;
        public bool emote_anger;
        public bool emote_bars;
        public bool emote_cash;
        public bool emote_circle;
        public bool emote_cloud;
        public bool emote_cross;
        public bool emote_dots1;
        public bool emote_dots2;
        public bool emote_dots3;
        public bool emote_drop;
        public bool emote_drops;
        public bool emote_exclamation;
        public bool emote_exclamations;
        public bool emote_heart;
        public bool emote_heartBroken;
        public bool emote_hearts;
        public bool emote_idea;
        public bool emote_laugh;
        public bool emote_music;
        public bool emote_question;
        public bool emote_sleep;
        public bool emote_sleeps;
        public bool emote_star;
        public bool emote_stars;
        public bool emote_swirl;
        public bool IsOwned(string id)
        {
            switch (id)
            {
                case "emote_alert": return emote_alert;
                case "emote_anger": return emote_anger;
                case "emote_bars": return emote_bars;
                case "emote_cash": return emote_cash;
                case "emote_circle": return emote_circle;
                case "emote_cloud": return emote_cloud;
                case "emote_cross": return emote_cross;
                case "emote_dots1": return emote_dots1;
                case "emote_dots2": return emote_dots2;
                case "emote_dots3": return emote_dots3;
                case "emote_drop": return emote_drop;
                case "emote_drops": return emote_drops;
                case "emote_exclamation": return emote_exclamation;
                case "emote_exclamations": return emote_exclamations;
                case "emote_heart": return emote_heart;
                case "emote_heartBroken": return emote_heartBroken;
                case "emote_hearts": return emote_hearts;
                case "emote_idea": return emote_idea;
                case "emote_laugh": return emote_laugh;
                case "emote_music": return emote_music;
                case "emote_question": return emote_question;
                case "emote_sleep": return emote_sleep;
                case "emote_sleeps": return emote_sleeps;
                case "emote_star": return emote_star;
                case "emote_stars": return emote_stars;
                case "emote_swirl": return emote_swirl;
                default: return false;
            }
        }
        public void SetOwned(string id, bool owned)
        {
            switch (id)
            {
                case "emote_alert": emote_alert = owned; break;
                case "emote_anger": emote_anger = owned; break;
                case "emote_bars": emote_bars = owned; break;
                case "emote_cash": emote_cash = owned; break;
                case "emote_circle": emote_circle = owned; break;
                case "emote_cloud": emote_cloud = owned; break;
                case "emote_cross": emote_cross = owned; break;
                case "emote_dots1": emote_dots1 = owned; break;
                case "emote_dots2": emote_dots2 = owned; break;
                case "emote_dots3": emote_dots3 = owned; break;
                case "emote_drop": emote_drop = owned; break;
                case "emote_drops": emote_drops = owned; break;
                case "emote_exclamation": emote_exclamation = owned; break;
                case "emote_exclamations": emote_exclamations = owned; break;
                case "emote_heart": emote_heart = owned; break;
                case "emote_heartBroken": emote_heartBroken = owned; break;
                case "emote_hearts": emote_hearts = owned; break;
                case "emote_idea": emote_idea = owned; break;
                case "emote_laugh": emote_laugh = owned; break;
                case "emote_music": emote_music = owned; break;
                case "emote_question": emote_question = owned; break;
                case "emote_sleep": emote_sleep = owned; break;
                case "emote_sleeps": emote_sleeps = owned; break;
                case "emote_star": emote_star = owned; break;
                case "emote_stars": emote_stars = owned; break;
                case "emote_swirl": emote_swirl = owned; break;
            }
        }
        public int RefundAmount() =>
            (emote_alert ? 150 : 0) +
            (emote_anger ? 150 : 0) +
            (emote_bars ? 150 : 0) +
            (emote_cash ? 190 : 0) +
            (emote_circle ? 150 : 0) +
            (emote_cloud ? 150 : 0) +
            (emote_cross ? 150 : 0) +
            (emote_dots1 ? 150 : 0) +
            (emote_dots2 ? 150 : 0) +
            (emote_dots3 ? 150 : 0) +
            (emote_drop ? 150 : 0) +
            (emote_drops ? 150 : 0) +
            (emote_exclamation ? 150 : 0) +
            (emote_exclamations ? 150 : 0) +
            (emote_heart ? 180 : 0) +
            (emote_heartBroken ? 180 : 0) +
            (emote_hearts ? 150 : 0) +
            (emote_idea ? 165 : 0) +
            (emote_laugh ? 165 : 0) +
            (emote_music ? 175 : 0) +
            (emote_question ? 150 : 0) +
            (emote_sleep ? 150 : 0) +
            (emote_sleeps ? 150 : 0) +
            (emote_star ? 175 : 0) +
            (emote_stars ? 180 : 0) +
            (emote_swirl ? 150 : 0);
    }


    [Serializable]
    private sealed class Inventory
    {
        public Frames profile_frames = new Frames();
        public Boards boards = new Boards();
        public Maps maps = new Maps();
        public Dice dice = new Dice();
        public Pawns pawns = new Pawns();
        public Emotes emotes = new Emotes();
    }


    [Serializable]
    private sealed class Equipped { public string profileFrameId = ""; public string boardId = ""; public string diceId = ""; }


    [Serializable]
    private sealed class DailyReward { public long lastClaimAt; }


    [Serializable]
    private sealed class DevCounters
    {
        public int grantCount;
        public int resetCount;
    }


    [Serializable]
    private sealed class Operation
    {
        public string id = "";
        public string type = "";
        public string itemId = "";
        public long atMs;
    }


    [Serializable]
    private sealed class Economy
    {
        public int schemaVersion = 1;
        // Atomic source of bonus XP from public promo redemptions.
        public int promoXp;
        public Wallet wallet = new Wallet();
        public Inventory inventory = new Inventory();
        public Equipped equipped = new Equipped();
        public DailyReward dailyReward = new DailyReward();
        public DevCounters dev = new DevCounters();
        public Operation operation = new Operation();
        // Append-only idempotency ledger; a server rule permits only suffix append.
        public string rewardHistory = "";
    }


    private sealed class Identity
    {
        public FirebaseUser User;
        public string Uid;
        public string Token;
        public bool IsAnonymous;
        public bool IsRestGuest;
    }


    private sealed class Response
    {
        public long Status;
        public string Body;
        public string ETag;
    }


    private sealed class ReadState
    {
        public Economy Document;
        public string ETag;
        public JObject Raw;
    }


    public static bool IsRegistered
    {
        get
        {
            // Development Live Guest authenticates via a separate HTTPS/REST UID.
            // Never reuse the native Firebase SDK principal (possibly cached
            // from the owner's Editor session) for a Guest wallet.
            if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
                return AtlasBoardAccountFormQualityGate
                    .TryGetVerifiedRegisteredRestPrincipal(out _);
            try
            {
                FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
                return user != null && !user.IsAnonymous &&
                       !string.IsNullOrEmpty(user.UserId);
            }
            catch { return false; }
        }
    }


    // Steamworks session is not proof of a verified Firebase Steam link.
    // RTDB rules, not client Steam flags, authorize every balance mutation.
    public static bool IsSteamSession
    {
        get
        {
            if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
                return false;
            AtlasBoardPlatformRuntime steam = AtlasBoardPlatformRuntime.Instance;
            return steam != null && steam.IsSteamProviderActive &&
                   steam.SteamInitialized && steam.SteamId != 0;
        }
    }


    public static string CurrentUid
    {
        get
        {
            // The Live Guest transport does NOT own native FirebaseAuth.
            // Returning a native UID here would expose another account's
            // wallet, inventory and equipped cosmetic caches in this process.
            if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
            {
                return AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(
                    out string guestUid, out _) ? guestUid : string.Empty;
            }
            try
            {
                FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
                return user != null ? user.UserId ?? "" : "";
            }
            catch { return ""; }
        }
    }


    public static int Price(string itemId)
    {
        switch (itemId)
        {
            case Halloween:
            case NewYear: return 350;
            case Turkiye:
            case Usa: return 400;
            case "frame_neon_circuit": return 450;
            case "frame_royal_crown": return 500;
            case "frame_ocean_tide": return 450;
            case "frame_sakura_blossom": return 475;
            case "frame_cosmic_nova": return 550;
            case "frame_forest_spirit": return 475;
            case "map_germany": return 30000;
            case "map_mexico": return 25000;
            case Midnight: return 1200;
            case Maple: return 1500;
            case Aurora: return 1700;
            case "dice_ruby_red":
            case "dice_ocean_blue": return 350;
            case "dice_midnight_black":
            case "dice_marble": return 400;
            case "ghost_01": return 300;
            case "ghost_02": return 300;
            case "ghost_03": return 300;
            case "ghost_04": return 300;
            case "ghost_05": return 300;
            case "ghost_06": return 300;
            case "ghost_07": return 300;
            case "vehicle_police": return 450;
            case "vehicle_taxi": return 400;
            case "vehicle_pickup": return 425;
            default: return AtlasBoardEmotes.Price(itemId);
        }
    }


    public static async Task<AtlasBoardMetaEconomySnapshot> LoadAsync()
    {
        AtlasBoardMetaEconomySnapshot snapshot = NewSnapshot();
        Identity identity = await ResolveIdentityAsync();
        if (identity == null)
        {
            cachedMapUid = ""; CachedOwnedMaps.Clear();
            cachedPawnOwnerUid = "";
            CachedOwnedPawnIds.Clear();
            cachedDiceId = ""; currentDiceUid = "";
            cachedEmoteUid = ""; CachedPaidEmotes.Clear();
            snapshot.Success = true;
            snapshot.IsGuest = !IsSteamSession;
            snapshot.IsSteamAccount = IsSteamSession;
            snapshot.Gold = 0;
            snapshot.AtlasCoin = 0;
            snapshot.WalletVerified = false;
            if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
            {
                // The Guest's real REST UID is valid for lobby/turn actions,
                // but never authorizes reading a native SDK wallet.
                snapshot.IsGuest = true;
                snapshot.IsSteamAccount = false;
                snapshot.DisplayName = AtlasBoardGuestIdentity.DisplayName;
                Debug.Log("ATB_LIVE_GUEST_WALLET isolated=true " +
                    "gold=0 coin=0 walletVerified=false " +
                    "native_sdk_wallet=not_read rewards=false");
            }
            return snapshot;
        }


        try
        {
            if (!identity.IsRestGuest &&
                !await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync(
                    "", false))
                throw new InvalidOperationException("USER_PROFILE_NOT_READY");


            ReadState state = await ReadAsync(identity);
            if (state.Document == null)
            {
                await InitializeAsync(identity, state.ETag);
                state = await ReadAsync(identity);
            }


            if (state.Document == null)
                throw new InvalidOperationException("WALLET_NOT_READY");


            Economy document = state.Document;
            snapshot.Success = true;
            snapshot.WalletVerified = true;
            snapshot.IsSteamAccount = IsSteamSession;
            snapshot.IsGuest = identity.IsAnonymous && !snapshot.IsSteamAccount;
            snapshot.IsDeveloperTester = await IsDeveloperTesterAsync(identity);
            bool permitted = !identity.IsAnonymous || snapshot.IsDeveloperTester;
            foreach (AtlasBoardStorefrontItem item in snapshot.Items)
                item.PreviewOnly = !permitted;
            snapshot.Gold = document.wallet.gold;
            snapshot.AtlasCoin = document.wallet.atlasCoin;
            snapshot.EquippedFrameId = document.equipped.profileFrameId ?? "";
            snapshot.EquippedBoardId = document.equipped.boardId ?? "";
            snapshot.EquippedDiceId = document.equipped.diceId ?? "";
            cachedMapUid = identity.Uid;
            CachedOwnedMaps.Clear();
            if (document.inventory.maps.IsOwned("map_germany")) CachedOwnedMaps.Add("map_germany");
            if (document.inventory.maps.IsOwned("map_mexico")) CachedOwnedMaps.Add("map_mexico");
            if (document.inventory.maps.IsOwned("map_world")) CachedOwnedMaps.Add("map_world");
            cachedPawnOwnerUid = identity.Uid;
            CachedOwnedPawnIds.Clear();
            cachedEmoteUid = identity.Uid;
            CachedPaidEmotes.Clear();
            foreach (var emote in AtlasBoardEmotes.All)
                if (emote.Gold > 0 && document.inventory.emotes.IsOwned(emote.Id))
                    CachedPaidEmotes.Add(emote.Id);
            // Read-only, UID-scoped inventory evidence for support diagnostics.
            // No item IDs, tokens or personally identifying account data logged.
            Debug.Log("ATB_EMOTE_INVENTORY source=canonical_wallet paid_owned=" +
                CachedPaidEmotes.Count + " account_uid_match=" +
                string.Equals(identity.Uid, CurrentUid, StringComparison.Ordinal) +
                " wallet_verified=true");
            foreach (string pawnId in new [] { "ghost_01", "ghost_02", "ghost_03", "ghost_04", "ghost_05", "ghost_06", "ghost_07", "vehicle_police", "vehicle_taxi", "vehicle_pickup" })
                if (document.inventory.pawns.IsOwned(pawnId)) CachedOwnedPawnIds.Add(pawnId);
            // Presentation cache only; RTDB remains the owner of this value.
            if (identity.Uid == CurrentUid)
                CurrentEquippedDiceId = snapshot.EquippedDiceId;
            // Repair a missing/stale *public* dice projection after a prior
            // successful verified equip. This never grants an item, spends
            // Gold, or reads any other UID's private wallet.
            string diceSyncKey = identity.Uid + ":" + snapshot.EquippedDiceId;
            if (identity.Uid == CurrentUid &&
                !string.IsNullOrWhiteSpace(snapshot.EquippedDiceId) &&
                lastPublicDiceProjectionReconciledKey != diceSyncKey)
            {
                string publicDiceId = await ReadPublicDiceAsync(identity.Uid);
                if (publicDiceId == snapshot.EquippedDiceId ||
                    await PublishEquippedDiceAsync(snapshot.EquippedDiceId))
                {
                    lastPublicDiceProjectionReconciledKey = diceSyncKey;
                    Debug.Log("ATB_DICE_PUBLIC_PROJECTION reconciled=true");
                }
                else
                    Debug.LogWarning("ATB_DICE_PUBLIC_PROJECTION pending=true");
            }
            snapshot.DailyClaimedAtMs = document.dailyReward.lastClaimAt;


            // Ordinary wallet refresh is strictly read-only. Receipts are
            // written once at the mutation commit sites below; replaying the
            // latest operation here caused unrelated 401 receipt attempts in
            // the middle of gameplay whenever the wallet UI refreshed.
            try
            {
                await ReadHistoryAsync(identity, snapshot);
                await ReadPromoHistoryAsync(identity, snapshot);
                // Legacy wallets may have a daily receipt but no lastClaimAt
                // subnode. The receipt is immutable, so reuse its timestamp in
                // the UI instead of advertising another award prematurely.
                if (state.Raw?["dailyReward"]?["lastClaimAt"] == null)
                {
                    long historicalClaim = LastDailyReceiptAtMs(snapshot);
                    snapshot.DailyClaimedAtMs = Math.Max(
                        snapshot.DailyClaimedAtMs, historicalClaim);
                    Debug.Log("ATB_DAILY_LEGACY stage=read missing=true " +
                        "historical_receipt_found=" + (historicalClaim > 0));
                }
            }
            catch (Exception historyError)
            {
                Debug.LogWarning("ATB_HISTORY_READ unavailable=" +
                    Sanitize(historyError.Message));
            }


            foreach (AtlasBoardStorefrontItem item in snapshot.Items)
            {
                item.Owned = AtlasBoardEmotes.IsPaid(item.ItemId)
                    ? document.inventory.emotes.IsOwned(item.ItemId)
                    : IsPremiumMap(item.ItemId)
                    ? document.inventory.maps.IsOwned(item.ItemId)
                    : IsPaidPawn(item.ItemId)
                    ? document.inventory.pawns.IsOwned(item.ItemId)
                    : IsBoard(item.ItemId)
                    ? document.inventory.boards.IsOwned(item.ItemId)
                    : IsDice(item.ItemId)
                        ? document.inventory.dice.IsOwned(item.ItemId)
                        : document.inventory.profile_frames.IsOwned(item.ItemId);
            }


            Debug.Log("ATB_PHASE19E_RTDB_WALLET ready=true guest=" +
                      snapshot.IsGuest + " steam=" + snapshot.IsSteamAccount +
                      " tester=" + snapshot.IsDeveloperTester + " " +
                      "gold=" + snapshot.Gold + " coin=" + snapshot.AtlasCoin +
                      " uid=" + Short(identity.Uid));
        }
        catch (Exception exception)
        {
            snapshot.Success = false;
            snapshot.WalletVerified = false;
            snapshot.ErrorKey = "meta.error.refresh_failed";
            snapshot.TechnicalMessage = exception.Message;
            Debug.LogWarning("ATB_PHASE19C_RTDB_WALLET ready=false " +
                             Sanitize(exception.Message));
        }
        return snapshot;
    }


    public static async Task<AtlasBoardMetaEconomyOperationResult> GrantDevAsync()
    {
        return await MutateAsync("DEV_GRANT", "", false);
    }


    public static async Task<AtlasBoardMetaEconomyOperationResult> ClaimDailyAsync()
    {
        return await MutateAsync("DAILY", "", false);
    }


    // Public promo redemption uses the same ETag-protected canonical wallet as
    // daily grants and purchases. No Cloud Functions, Owner QA or DEV allowlist.
    // rewardHistory is the atomic one-use ledger; the separate receipt is display-only.
    public static async Task<AtlasBoardMetaEconomyOperationResult> RedeemPromoAsync(
        string requestedCode)
    {
        string code = (requestedCode ?? "").Trim().ToUpperInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(code,
            @"^ATLAS-(G|XP|AC|IT)-[0-9A-F]{18}$"))
            return Failure("promo.error.invalid_code");

        Identity identity = await ResolveIdentityAsync();
        if (identity == null)
            return Failure("account.error.authentication_required");
        try
        {
            if (!identity.IsRestGuest &&
                !await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync("", false))
                return Failure("promo.error.account_not_ready");

            Response catalogResponse = await RequestAsync(identity, "GET",
                "/promoCodes/" + UnityWebRequest.EscapeURL(code) + ".json",
                null, null);
            if (catalogResponse.Status == 401 || catalogResponse.Status == 403)
                return Failure("promo.error.account_not_ready",
                    "PROMO_RULES_READ_HTTP_" + catalogResponse.Status);
            if (catalogResponse.Status != 200)
                return Failure("meta.error.service_unavailable",
                    "PROMO_CATALOG_HTTP_" + catalogResponse.Status);
            if (string.IsNullOrWhiteSpace(catalogResponse.Body) ||
                catalogResponse.Body.Trim() == "null")
                return Failure("promo.error.not_found");

            JObject definition = JObject.Parse(catalogResponse.Body);
            if ((bool?)definition["active"] != true ||
                (string)definition["scope"] != "public")
                return Failure("promo.error.disabled");
            long now = Now();
            if (now < ((long?)definition["startsAtEpochMs"] ?? 0L))
                return Failure("promo.error.not_started");
            if (now >= ((long?)definition["endsAtEpochMs"] ?? 0L))
                return Failure("promo.error.expired");
            JObject reward = definition["reward"] as JObject;
            string type = (string)reward?["type"] ?? "";
            string itemId = (string)reward?["itemId"] ?? "";
            int amount = (int?)reward?["amount"] ?? 0;
            if ((string)definition["code"] != code ||
                (int?)definition["perAccountLimit"] != 1 ||
                (int?)definition["quantity"] != amount ||
                amount < 1 || amount > 5000 ||
                (type != "gold" && type != "atlas_coin" && type != "xp" &&
                 type != "inventory_item") ||
                (type == "inventory_item" && (amount != 1 ||
                    string.IsNullOrWhiteSpace(itemId) ||
                    itemId == "map_world" || itemId == "board_atlas_horizon")) ||
                (type != "inventory_item" && !string.IsNullOrEmpty(itemId)))
                return Failure("promo.error.invalid_reward");

            string digest = PromoTransactionHash(identity.Uid + ":" + code);
            for (int attempt = 0; attempt < 7; attempt++)
            {
                // Fetch the ETag again after every conflict. No stale wallet overwrite.
                ReadState state = await ReadAsync(identity);
                if (state.Document == null)
                {
                    await InitializeAsync(identity, state.ETag);
                    continue;
                }
                Economy wallet = state.Document;
                string marker = "|" + code + "|";
                string ledger = wallet.rewardHistory ?? "";
                if (ledger.Contains(marker))
                {
                    await WritePromoReceiptBestEffortAsync(identity, code,
                        digest, type, itemId, amount, Now());
                    return Failure("promo.error.already_redeemed");
                }
                if (ledger.Length + marker.Length > 1000000)
                    return Failure("promo.error.limit_reached");
                if (type == "inventory_item")
                {
                    bool supported = IsPremiumMap(itemId) && itemId != "map_world" ||
                        AtlasBoardEmotes.IsPaid(itemId) || IsPaidPawn(itemId) ||
                        IsBoard(itemId) && itemId != "board_atlas_horizon" ||
                        IsDice(itemId) || itemId.StartsWith("frame_",
                            StringComparison.Ordinal);
                    if (!supported) return Failure("promo.error.invalid_reward");
                    bool owned = IsPremiumMap(itemId) ? wallet.inventory.maps.IsOwned(itemId) :
                        AtlasBoardEmotes.IsPaid(itemId) ? wallet.inventory.emotes.IsOwned(itemId) :
                        IsPaidPawn(itemId) ? wallet.inventory.pawns.IsOwned(itemId) :
                        IsBoard(itemId) ? wallet.inventory.boards.IsOwned(itemId) :
                        IsDice(itemId) ? wallet.inventory.dice.IsOwned(itemId) :
                        wallet.inventory.profile_frames.IsOwned(itemId);
                    if (owned) return Failure("promo.error.item_already_owned");
                    if (IsPremiumMap(itemId)) wallet.inventory.maps.SetOwned(itemId, true);
                    else if (AtlasBoardEmotes.IsPaid(itemId)) wallet.inventory.emotes.SetOwned(itemId, true);
                    else if (IsPaidPawn(itemId)) wallet.inventory.pawns.SetOwned(itemId, true);
                    else if (IsBoard(itemId)) wallet.inventory.boards.SetOwned(itemId, true);
                    else if (IsDice(itemId)) wallet.inventory.dice.SetOwned(itemId, true);
                    else wallet.inventory.profile_frames.SetOwned(itemId, true);
                }
                else if (type == "gold")
                {
                    if (wallet.wallet.gold > 10000000 - amount)
                        return Failure("promo.error.limit_reached");
                    wallet.wallet.gold += amount;
                }
                else if (type == "atlas_coin")
                {
                    if (wallet.wallet.atlasCoin > 1000000 - amount)
                        return Failure("promo.error.limit_reached");
                    wallet.wallet.atlasCoin += amount;
                }
                else
                {
                    if (wallet.promoXp > 100000000 - amount)
                        return Failure("promo.error.limit_reached");
                    wallet.promoXp += amount;
                }

                long timestamp = Now();
                if (wallet.operation != null &&
                    wallet.operation.atMs >= timestamp)
                    timestamp = wallet.operation.atMs + 1;
                wallet.rewardHistory = ledger + marker;
                wallet.operation = new Operation {
                    id = Guid.NewGuid().ToString("N"), type = "PROMO",
                    itemId = code, atMs = timestamp
                };
                JObject body = JObject.Parse(JsonUtility.ToJson(wallet));
                if (state.Raw?["dailyReward"]?["lastClaimAt"] == null)
                    body.Remove("dailyReward");
                if (identity.Uid != CurrentUid)
                    return Failure("promo.error.account_not_ready");
                Response applied = await RequestAsync(identity, "PUT",
                    EconomyPath(identity.Uid),
                    body.ToString(Newtonsoft.Json.Formatting.None), state.ETag);
                if (applied.Status == 412) continue;
                if (applied.Status == 401 || applied.Status == 403)
                {
                    if (await RefreshRegisteredRestBearerAsync(identity))
                        continue;
                    return Failure("promo.error.account_not_ready",
                        "PROMO_WALLET_HTTP_" + applied.Status);
                }
                if (applied.Status < 200 || applied.Status >= 300)
                    return Failure("meta.error.service_unavailable",
                        "PROMO_WALLET_HTTP_" + applied.Status);

                if (type == "inventory_item")
                {
                    if (IsPremiumMap(itemId)) {
                        cachedMapUid = identity.Uid; CachedOwnedMaps.Add(itemId);
                    }
                    if (IsPaidPawn(itemId)) {
                        cachedPawnOwnerUid = identity.Uid; CachedOwnedPawnIds.Add(itemId);
                    }
                    if (AtlasBoardEmotes.IsPaid(itemId)) {
                        cachedEmoteUid = identity.Uid; CachedPaidEmotes.Add(itemId);
                    }
                }
                await WritePromoReceiptBestEffortAsync(identity, code,
                    digest, type, itemId, amount, timestamp);
                var result = new AtlasBoardMetaEconomyOperationResult {
                    Success = true, Type = "REDEEM_CODE", TransactionId = digest,
                    ReferenceId = code, Detail = code,
                    Gold = wallet.wallet.gold, AtlasCoin = wallet.wallet.atlasCoin
                };
                result.Rewards.Add(new AtlasBoardMetaRewardLine {
                    Type = type, CurrencyId = type,
                    ItemId = itemId, Amount = amount
                });
                Debug.Log("ATB_PROMO_RTDB result=credited type=" + type +
                    " uid_suffix=" + ShortUid(identity.Uid));
                return result;
            }
            return Failure("meta.error.concurrent_write");
        }
        catch (Exception error)
        {
            Debug.LogWarning("ATB_PROMO_RTDB result=failed reason=" +
                Sanitize(error.Message));
            return Failure("meta.error.service_unavailable", error.Message);
        }
    }

    private static string PromoTransactionHash(string data)
    {
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(data)))
                .Replace("-", "").ToLowerInvariant();
    }

    private static async Task WritePromoReceiptBestEffortAsync(
        Identity identity, string code, string transactionId, string type,
        string itemId, int amount, long claimedAt)
    {
        try
        {
            JObject receipt = new JObject {
                ["code"] = code, ["transactionId"] = transactionId,
                ["claimedAt"] = claimedAt, ["rewardType"] = type,
                ["rewardAmount"] = amount, ["rewardItemId"] = itemId,
                ["status"] = "succeeded"
            };
            Response existing = await RequestAsync(identity, "GET",
                "/users/" + UnityWebRequest.EscapeURL(identity.Uid) +
                "/promoRedemptions/" + UnityWebRequest.EscapeURL(code) +
                ".json", null, null);
            if (existing.Status == 200 && existing.Body != "null")
                return;
            Response written = await RequestAsync(identity, "PUT",
                "/users/" + UnityWebRequest.EscapeURL(identity.Uid) +
                "/promoRedemptions/" + UnityWebRequest.EscapeURL(code) +
                ".json", receipt.ToString(Newtonsoft.Json.Formatting.None),
                "null_etag");
            if (written.Status < 200 || written.Status >= 300)
                Debug.LogWarning("ATB_PROMO_RECEIPT pending=true http=" +
                    written.Status);
        }
        catch (Exception error)
        {
            Debug.LogWarning("ATB_PROMO_RECEIPT pending=true type=" +
                error.GetType().Name);
        }
    }

    private static string currentDiceUid = "";
    private static string cachedDiceId = "";
    public static bool HasCurrentDiceCache =>
        !string.IsNullOrEmpty(currentDiceUid) && currentDiceUid == CurrentUid;
    public static string CurrentEquippedDiceId
    {
        get => CurrentUid == currentDiceUid ? cachedDiceId : "";
        private set { currentDiceUid = CurrentUid; cachedDiceId = value ?? ""; }
    }


    private static readonly System.Collections.Generic.HashSet<string> CachedOwnedMaps =
        new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
    private static string cachedMapUid = "";
    public static bool IsPremiumMap(string id) => id == "map_germany" || id == "map_mexico" || id == "map_world";
    public static bool IsPremiumMapOwnedForCurrentUid(string id) =>
        !IsPremiumMap(id) || (cachedMapUid == CurrentUid && CachedOwnedMaps.Contains(id));
    public static bool IsPremiumRoomMap(string mapId) => mapId == "Germany" || mapId == "Mexico" || mapId == "World";
    public static string MapEntitlementId(string mapId) => mapId == "Germany" ? "map_germany" : mapId == "Mexico" ? "map_mexico" :
        mapId == "World" ? "map_world" : "";


    private static readonly System.Collections.Generic.HashSet<string> CachedOwnedPawnIds =
        new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
    private static string cachedPawnOwnerUid = "";
    public static bool IsPaidPawn(string id) =>
        !string.IsNullOrEmpty(id) &&
        (id.StartsWith("ghost_", StringComparison.Ordinal) ||
         id == "vehicle_police" || id == "vehicle_taxi" || id == "vehicle_pickup") &&
        Price(id) > 0;
    public static bool IsPawnOwnedOnThisAccount(string id) =>
        !IsPaidPawn(id) ||
        (cachedPawnOwnerUid == CurrentUid && CachedOwnedPawnIds.Contains(id));
    private static readonly System.Collections.Generic.HashSet<string> CachedPaidEmotes =
        new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
    private static string cachedEmoteUid = "";
    public static bool IsEmoteOwnedForCurrentUid(string id) =>
        AtlasBoardEmotes.Find(id) != null &&
        (AtlasBoardEmotes.Find(id).IsFree ||
         cachedEmoteUid == CurrentUid && CachedPaidEmotes.Contains(id));


    public static bool IsDice(string id) =>
        id == "dice_ruby_red" || id == "dice_ocean_blue" ||
        id == "dice_midnight_black" || id == "dice_marble";


    public static bool IsBoard(string itemId) =>
        itemId == Midnight || itemId == Maple || itemId == Aurora ||
        itemId == "board_atlas_horizon";


    public static async Task<AtlasBoardMetaEconomyOperationResult> BuyAsync(
        string itemId, string paymentMethod)
    {
        if (paymentMethod != "gold" || Price(itemId) <= 0)
            return Failure("meta.error.invalid_purchase");
        return await MutateAsync("PURCHASE", itemId, false);
    }


    public static async Task<AtlasBoardMetaEconomyOperationResult> EquipAsync(
        string itemId)
    {
        if (!string.IsNullOrEmpty(itemId) && Price(itemId) <= 0)
            return Failure("meta.error.invalid_purchase");
        var result = await MutateAsync("EQUIP", itemId ?? "", false);
        if (result != null && result.Success)
        {
            // Keep the existing public profile projection in step with the
            // verified wallet. Rules accept this ID only after the owner has
            // equipped it in metaEconomy; purchases are entirely unaffected.
            if (!await PublishEquippedFrameAsync(itemId ?? ""))
                Debug.LogWarning("ATB_FRAME_PUBLIC_PROJECTION pending=true");
        }
        return result;
    }


    public static async Task<AtlasBoardMetaEconomyOperationResult> EquipBoardAsync(string itemId)
    {
        if (!string.IsNullOrEmpty(itemId) && !IsBoard(itemId))
            return Failure("meta.error.invalid_purchase");
        return await MutateAsync("EQUIP_BOARD", itemId ?? "", false);
    }


    public static async Task<AtlasBoardMetaEconomyOperationResult> EquipDiceAsync(string itemId)
    {
        if (!string.IsNullOrEmpty(itemId) && !IsDice(itemId))
            return Failure("meta.error.invalid_purchase");
        var result = await MutateAsync("EQUIP_DICE", itemId ?? "", false);
        if (result != null && result.Success)
        {
            CurrentEquippedDiceId = itemId ?? "";
            // RTDB-backed public projection validated against the equipped UID.
            // Failure does NOT mint an item or rollback a legitimate ETag equip.
            if (!await PublishEquippedDiceAsync(CurrentEquippedDiceId))
                Debug.LogWarning("ATB_PHASE19K_DICE public_projection_retry_needed");
        }
        return result;
    }


    private static string lastPublicDiceProjectionReconciledKey = "";


    [Serializable]
    private sealed class PublicDiceWire { public string diceSkinId; }

    [Serializable]
    private sealed class PublicFrameWire { public string profileFrameId; }

    private static async Task<bool> PublishEquippedFrameAsync(string id)
    {
        Identity identity = await ResolveIdentityAsync();
        if (identity == null || !string.Equals(identity.Uid, CurrentUid,
                StringComparison.Ordinal)) return false;
        string path = "/publicProfiles/" +
            UnityWebRequest.EscapeURL(identity.Uid) + ".json";
        Response response = await RequestAsync(identity, "PATCH", path,
            JsonUtility.ToJson(new PublicFrameWire { profileFrameId = id ?? "" }), null);
        if (response.Status >= 200 && response.Status < 300) return true;

        // Older REST-registered accounts can have a verified metaEconomy
        // wallet but no publicProfiles node at all. A single-field PATCH
        // cannot pass that node's required-field validation. Create ONLY a
        // missing profile with the owner's existing RTDB display name; never
        // replace a profile another device has already populated.
        Response existing = await RequestAsync(identity, "GET", path, null, null);
        if (existing.Status != 200 ||
            !string.Equals(existing.Body != null ? existing.Body.Trim() : "",
                "null", StringComparison.Ordinal)) return false;
        Response name = await RequestAsync(identity, "GET",
            "/users/" + UnityWebRequest.EscapeURL(identity.Uid) +
            "/displayName.json", null, null);
        if (name.Status != 200 || string.IsNullOrWhiteSpace(name.Body))
            return false;
        string displayName;
        try
        {
            JToken nameToken = JToken.Parse(name.Body);
            if (nameToken.Type != JTokenType.String) return false;
            displayName = (string)nameToken;
        }
        catch { return false; }
        if (string.IsNullOrWhiteSpace(displayName) ||
            displayName.Trim().Length < 2 || displayName.Trim().Length > 32)
            return false;
        JObject profile = new JObject
        {
            ["displayName"] = displayName.Trim(),
            ["avatarId"] = "",
            ["profileFrameId"] = id ?? "",
            ["updatedAt"] = Now()
        };
        Response created = await RequestAsync(identity, "PUT", path,
            profile.ToString(Newtonsoft.Json.Formatting.None), "null_etag");
        if (created.Status >= 200 && created.Status < 300) return true;
        if (created.Status != 412) return false;
        // Another device won the create race. Re-apply only the equipped field;
        // do not overwrite its display name, avatar or other public data.
        Response retry = await RequestAsync(identity, "PATCH", path,
            JsonUtility.ToJson(new PublicFrameWire { profileFrameId = id ?? "" }), null);
        return retry.Status >= 200 && retry.Status < 300;
    }

    private static async Task<bool> PublishEquippedDiceAsync(string id)
    {
        Identity identity = await ResolveIdentityAsync();
        if (identity == null) return false;
        Response response = await RequestAsync(identity, "PATCH",
            "/publicProfiles/" + UnityWebRequest.EscapeURL(identity.Uid) + ".json",
            JsonUtility.ToJson(new PublicDiceWire { diceSkinId = id ?? "" }), null);
        return response.Status >= 200 && response.Status < 300;
    }


    // Read-only public projection: Firebase rules allow an owner to publish
    // only the server-verified equipped item; never accept an unowned client ID.
    public static async Task<string> ReadPublicDiceAsync(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return "";
        // No-Steam Guest is a *different authenticated RTDB principal*.
        // Its lobby REST token may read another player's public cosmetic
        // projection, but it must never open the paid wallet of that UID.
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
        {
            AtlasBoardLobbyRuntimeBridge lobby =
                UnityEngine.Object.FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>();
            if (lobby == null || string.IsNullOrWhiteSpace(lobby.CurrentAccountId) ||
                string.IsNullOrWhiteSpace(lobby.AuthTokenForOnlineSubsystems))
                return "";
            try
            {
                string endpoint = Database + "/publicProfiles/" +
                    UnityWebRequest.EscapeURL(uid) + ".json?auth=" +
                    UnityWebRequest.EscapeURL(lobby.AuthTokenForOnlineSubsystems);
                using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
                {
                    request.timeout = 10;
                    await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);
                    var tcs = new TaskCompletionSource<bool>();
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    operation.completed += _ => tcs.TrySetResult(true);
                    if (operation.isDone) tcs.TrySetResult(true);
                    await tcs.Task;
                    if (request.result != UnityWebRequest.Result.Success) return "";
                    PublicDiceWire read = JsonUtility.FromJson<PublicDiceWire>(
                        request.downloadHandler.text ?? "{}");
                    return read != null && IsDice(read.diceSkinId) ? read.diceSkinId : "";
                }
            }
            catch (Exception) { return ""; }
        }
        Identity identity = await ResolveIdentityAsync();
        if (identity == null) return "";
        try
        {
            Response response = await RequestAsync(identity, "GET",
                "/publicProfiles/" + UnityWebRequest.EscapeURL(uid) + ".json",
                null, null);
            if (response.Status != 200 || string.IsNullOrEmpty(response.Body) ||
                response.Body.Trim() == "null") return "";
            PublicDiceWire profile = JsonUtility.FromJson<PublicDiceWire>(response.Body);
            return profile != null && IsDice(profile.diceSkinId) ? profile.diceSkinId : "";
        }
        catch { return ""; }
    }


    /// <summary>
    /// Applies a previously RTDB-validated, peer-attested completed match claim
    /// to the canonical ETag wallet. One immutable rewardLedger child per match
    /// lives INSIDE the wallet's atomic PUT; retry and concurrent clients are
    /// idempotent. No AtlasCoin/entitlement is granted.
    /// </summary>
    public static async Task<bool> IsCareerRewardEligibleAsync()
    {
        Identity identity = await ResolveIdentityAsync();
        // Anonymous Firebase Guests are real persistent UIDs in AtlasBoard.
        // Reward authorization comes from immutable participation/attestation
        // rules, not from the Auth provider label.
        return identity != null;
    }


    public static async Task<bool> ClaimMatchGoldAsync(string matchId)
    {
        if (string.IsNullOrWhiteSpace(matchId) || matchId.Length > 64) return false;
        Identity identity = await ResolveIdentityAsync();
        if (identity == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("ATB_MATCH_GOLD_FLOW stage=eligibility eligible=false match=" + matchId);
#endif
            return false;
        }

        string confirmationKey = identity.Uid + "|" + matchId;
        if (ConfirmedMatchGoldKeys.Contains(confirmationKey))
            return true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("ATB_MATCH_GOLD_FLOW stage=start match=" + matchId +
            " uid=" + ShortUid(identity.Uid));
#endif
        try
        {
            Response proof = await RequestAsync(identity, "GET",
                "/careerClaims/" + UnityWebRequest.EscapeURL(identity.Uid) + "/" +
                UnityWebRequest.EscapeURL(matchId) + ".json", null, null);
            if (proof.Status != 200 || string.IsNullOrEmpty(proof.Body) ||
                proof.Body.Trim() == "null")
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("ATB_MATCH_GOLD_FLOW stage=claim_read ready=false http=" +
                    proof.Status + " match=" + matchId);
#endif
                return false;
            }

            JObject claim = JObject.Parse(proof.Body);
            int award = (int?)claim["goldAwarded"] ?? 0;
            if ((string)claim["matchId"] != matchId ||
                (string)claim["uid"] != identity.Uid || award < 1 || award > 50)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("ATB_MATCH_GOLD_FLOW stage=claim_validate valid=false award=" +
                    award + " match=" + matchId);
#endif
                return false;
            }

            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (CurrentUid != identity.Uid) return false;
                ReadState state = await ReadAsync(identity);
                if (state.Document == null)
                {
                    await InitializeAsync(identity, state.ETag);
                    continue;
                }

                string ledger = state.Document.rewardHistory ?? string.Empty;
                string marker = "|" + matchId + "|";
                if (ledger.Contains(marker))
                {
                    ConfirmedMatchGoldKeys.Add(confirmationKey);
                    return true;
                }

                Economy doc = state.Document;
                int beforeGold = doc.wallet.gold;
                if (doc.wallet.gold > 10000000 - award) return false;
                doc.wallet.gold += award;
                long now = Now();
                if (doc.operation != null && doc.operation.atMs >= now)
                    now = doc.operation.atMs + 1;
                doc.operation = new Operation {
                    id = Guid.NewGuid().ToString("N"), type = "MATCH_REWARD",
                    itemId = matchId, atMs = now
                };
                if (ledger.Length + marker.Length > 1000000) return false;
                doc.rewardHistory = ledger + marker;
                JObject body = JObject.Parse(JsonUtility.ToJson(doc));
                if (CurrentUid != identity.Uid) return false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("ATB_MATCH_GOLD_FLOW stage=wallet_put attempt=" +
                    (attempt + 1) + " before=" + beforeGold + " award=" + award +
                    " after=" + doc.wallet.gold + " match=" + matchId);
#endif
                Response saved = await RequestAsync(identity, "PUT", EconomyPath(identity.Uid),
                    body.ToString(Newtonsoft.Json.Formatting.None), state.ETag);
                if (saved.Status == 412)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("ATB_MATCH_GOLD_FLOW stage=etag_retry attempt=" +
                        (attempt + 1) + " match=" + matchId);
#endif
                    continue;
                }
                if (saved.Status < 200 || saved.Status >= 300)
                    throw new InvalidOperationException("MATCH_REWARD_HTTP_" + saved.Status);

                await TryWriteReceiptAsync(identity, doc.operation);
                ConfirmedMatchGoldKeys.Add(confirmationKey);
                Debug.Log("ATB_CAREER_GOLD status=credited amount=" + award +
                    " before=" + beforeGold + " after=" + doc.wallet.gold +
                    " match=" + matchId);
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_CAREER_GOLD pending=" + e.GetType().Name +
                " detail=" + e.Message + " match=" + matchId);
        }
        return false;
    }

    private static string ShortUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) return "-";
        return uid.Substring(0, Mathf.Min(7, uid.Length));
    }


    public static async Task<AtlasBoardMetaEconomyOperationResult> ResetDevAsync()
    {
        return await MutateAsync("RESET_PURCHASES", "", true);
    }


    private static async Task<AtlasBoardMetaEconomyOperationResult> MutateAsync(
        string kind, string itemId, bool clearPublicFrame)
    {
        Identity identity = await ResolveIdentityAsync();
        if (identity == null) return Failure("meta.error.registered_only");


        try
        {
            if (identity.IsRestGuest)
            {
                // REST-only QA builds intentionally remove native Firebase.
                // Do not require a native SDK user or create a second UID.
                // Require the SAME verified token to see a registered profile.
                if (identity.IsAnonymous || !await RegisteredRestProfileExistsAsync(identity))
                {
                    Debug.LogWarning("ATB_STORE_SESSION result=blocked reason=" +
                        "rest_registered_profile_not_verified uid=" + Short(identity.Uid));
                    return Failure("meta.error.registered_only");
                }
            }
            else if (!await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync("", false))
                return Failure("meta.error.identity_unavailable");


            // The UID allowlist is ADMIN managed. This exception supports
            // the owner-approved anonymous Steam test identity only.
            if ((identity.IsAnonymous || kind == "DEV_GRANT" ||
                 kind == "RESET_PURCHASES") &&
                !await IsDeveloperTesterAsync(identity))
                return Failure("meta.error.dev_not_allowed");

            // A password login can outlive the ID token cached by the REST
            // transport. Refresh before ETag mutations, preserving the SAME UID.
            // Server-side rules still validate every wallet operation.
            if (identity.IsRestGuest && !identity.IsAnonymous)
            {
                bool refreshed = await RefreshRegisteredRestBearerAsync(identity);
                Debug.Log("ATB_STORE_MUTATION stage=identity_refresh kind=" + kind +
                    " result=" + (refreshed ? "success" : "reuse_existing_token") +
                    " uid_suffix=" + ShortUid(identity.Uid));
            }
            bool permissionRetryUsed = false;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                ReadState state = await ReadAsync(identity);
                if (state.Document == null)
                {
                    await InitializeAsync(identity, state.ETag);
                    continue;
                }


                Economy doc = state.Document;
                // Existing wallets written before dailyReward was mandatory
                // are repaired as part of the NEXT normal ETag transaction.
                // Purchases do not require daily participation or admin access.
                // Look up immutable receipts so an existing DAILY award does
                // not become claimable again merely because this field vanished.
                bool dailyFieldMissing = state.Raw?["dailyReward"]?["lastClaimAt"] == null;
                if (dailyFieldMissing && kind == "DAILY")
                {
                    // Only a daily claim needs immutable daily-history proof.
                    // Ordinary purchases, equipping and developer test grants
                    // MUST NOT depend on dailyReward or receipt availability.
                    long receiptAt = await ReadLastDailyReceiptAtMsAsync(identity,
                        requireHistory: true);
                    doc.dailyReward.lastClaimAt = Math.Max(
                        doc.dailyReward.lastClaimAt, receiptAt);
                    Debug.Log("ATB_DAILY_LEGACY stage=claim missing=true " +
                        "receipt_found=" + (receiptAt > 0) +
                        " uid_suffix=" + ShortUid(identity.Uid));
                }
                long now = Now();
                if (doc.operation != null && doc.operation.atMs >= now)
                    now = doc.operation.atMs + 1;


                bool alreadyClaimed = false;
                switch (kind)
                {
                    case "DEV_GRANT":
                        doc.wallet.gold += DevGold;
                        doc.wallet.atlasCoin += DevCoin;
                        doc.dev.grantCount++;
                        break;
                    case "DAILY":
                        alreadyClaimed = doc.dailyReward.lastClaimAt > now - DayMs;
                        if (!alreadyClaimed)
                        {
                            doc.dailyReward.lastClaimAt = now;
                            doc.wallet.gold += DailyGold;
                        }
                        break;
                    case "PURCHASE":
                        if (IsPremiumMap(itemId) ? doc.inventory.maps.IsOwned(itemId) : AtlasBoardEmotes.IsPaid(itemId) ? doc.inventory.emotes.IsOwned(itemId) : IsPaidPawn(itemId) ? doc.inventory.pawns.IsOwned(itemId) : IsBoard(itemId) ? doc.inventory.boards.IsOwned(itemId) : IsDice(itemId) ? doc.inventory.dice.IsOwned(itemId) : doc.inventory.profile_frames.IsOwned(itemId))
                            return Failure("meta.error.already_owned");
                        if (doc.wallet.gold < Price(itemId))
                            return Failure("meta.error.insufficient_funds");
                        doc.wallet.gold -= Price(itemId);
                        if (IsPremiumMap(itemId)) doc.inventory.maps.SetOwned(itemId, true);
                        else if (AtlasBoardEmotes.IsPaid(itemId)) doc.inventory.emotes.SetOwned(itemId, true);
                        else if (IsPaidPawn(itemId)) doc.inventory.pawns.SetOwned(itemId, true);
                        else if (IsBoard(itemId)) doc.inventory.boards.SetOwned(itemId, true);
                        else if (IsDice(itemId)) doc.inventory.dice.SetOwned(itemId, true);
                        else doc.inventory.profile_frames.SetOwned(itemId, true);
                        break;
                    case "EQUIP":
                        if (!string.IsNullOrEmpty(itemId) &&
                            !doc.inventory.profile_frames.IsOwned(itemId))
                            return Failure("meta.error.not_owned");
                        if (doc.equipped.profileFrameId == itemId)
                            return new AtlasBoardMetaEconomyOperationResult
                            {
                                Success = true, IdempotentReplay = true,
                                ItemId = itemId, Type = "EQUIP"
                            };
                        doc.equipped.profileFrameId = itemId;
                        break;
                    case "EQUIP_BOARD":
                        if (!string.IsNullOrEmpty(itemId) && !doc.inventory.boards.IsOwned(itemId))
                            return Failure("meta.error.not_owned");
                        if (doc.equipped.boardId == itemId)
                            return new AtlasBoardMetaEconomyOperationResult
                            { Success = true, IdempotentReplay = true,
                              ItemId = itemId, Type = "EQUIP_BOARD" };
                        doc.equipped.boardId = itemId;
                        break;
                    case "EQUIP_DICE":
                        if (!string.IsNullOrEmpty(itemId) && !doc.inventory.dice.IsOwned(itemId))
                            return Failure("meta.error.not_owned");
                        if (doc.equipped.diceId == itemId)
                            return new AtlasBoardMetaEconomyOperationResult
                            { Success = true, IdempotentReplay = true,
                              ItemId = itemId, Type = "EQUIP_DICE" };
                        doc.equipped.diceId = itemId;
                        break;
                    case "RESET_PURCHASES":
                        doc.wallet.gold += doc.inventory.profile_frames.RefundAmount() + doc.inventory.boards.RefundAmount() + doc.inventory.dice.RefundAmount() + doc.inventory.pawns.RefundAmount() + doc.inventory.emotes.RefundAmount() + doc.inventory.maps.RefundAmount();
                        // RESET PURCHASES never revokes event-only unlocks.
                        bool worldOwned = doc.inventory.maps.IsOwned("map_world");
                        bool horizonOwned = doc.inventory.boards.IsOwned("board_atlas_horizon");
                        doc.inventory.profile_frames = new Frames();
                        doc.inventory.boards = new Boards();
                        doc.inventory.maps = new Maps();
                        doc.inventory.maps.map_world = worldOwned;
                        doc.inventory.boards.board_atlas_horizon = horizonOwned;
                        doc.inventory.dice = new Dice();
                        doc.inventory.pawns = new Pawns();
                        doc.inventory.emotes = new Emotes();
                        doc.equipped.diceId = "";
                        CurrentEquippedDiceId = "";
                        doc.equipped.boardId = "";
                        doc.equipped.profileFrameId = "";
                        doc.dev.resetCount++;
                        break;
                    default: return Failure("meta.error.invalid_purchase");
                }


                if (alreadyClaimed)
                {
                    return new AtlasBoardMetaEconomyOperationResult
                    {
                        Success = true, Type = kind, AlreadyClaimed = true,
                        IdempotentReplay = true, Gold = doc.wallet.gold,
                        AtlasCoin = doc.wallet.atlasCoin
                    };
                }


                doc.operation = new Operation
                {
                    id = Guid.NewGuid().ToString("N"),
                    type = kind,
                    itemId = itemId,
                    atMs = now
                };


                // Old economy operations must keep the append-only rewardHistory
                // string byte-for-byte (enforced again by RTDB Rules).
                JObject documentJson = JObject.Parse(JsonUtility.ToJson(doc));
                // Daily history is OPTIONAL for unrelated actions. Preserve
                // a legacy absent field through purchases/equips so the
                // customer's ability to shop never depends on a daily claim.
                if (dailyFieldMissing && kind != "DAILY")
                    documentJson.Remove("dailyReward");
                else
                {
                    // New accounts and first-time DAILY claims always write
                    // an explicit timestamp (zero is legitimate on INIT).
                    JObject daily = documentJson["dailyReward"] as JObject;
                    if (daily == null)
                    {
                        daily = new JObject();
                        documentJson["dailyReward"] = daily;
                    }
                    daily["lastClaimAt"] = doc.dailyReward.lastClaimAt;
                }
                Response written = await RequestAsync(identity, "PUT",
                    EconomyPath(identity.Uid), documentJson.ToString(Newtonsoft.Json.Formatting.None), state.ETag);
                if (written.Status == 412) continue; // conflicting device: re-read
                if ((written.Status == 401 || written.Status == 403) &&
                    !permissionRetryUsed)
                {
                    permissionRetryUsed = true;
                    if (await RefreshRegisteredRestBearerAsync(identity))
                    {
                        Debug.Log("ATB_STORE_MUTATION stage=permission_retry kind=" +
                            kind + " original_http=" + written.Status +
                            " uid_unchanged=true");
                        continue; // new ETag read; never reuse a stale PUT body
                    }
                }
                if (written.Status < 200 || written.Status >= 300)
                {
                    Debug.LogWarning("ATB_STORE_MUTATION stage=wallet_put_failed kind=" +
                        kind + " http=" + written.Status + " attempt=" + (attempt + 1) +
                        " uid_suffix=" + ShortUid(identity.Uid));
                    throw new InvalidOperationException("RTDB_HTTP_" + written.Status);
                }


                if (clearPublicFrame)
                {
                    CurrentEquippedDiceId = "";
                    await ClearPublicFrameBestEffortAsync();
                    await PublishEquippedDiceAsync("");
                }


                if (kind == "PURCHASE" && IsPremiumMap(itemId))
                {
                    cachedMapUid = identity.Uid;
                    CachedOwnedMaps.Add(itemId);
                }
                if (kind == "PURCHASE" && AtlasBoardEmotes.IsPaid(itemId))
                {
                    cachedEmoteUid = identity.Uid;
                    CachedPaidEmotes.Add(itemId);
                }
                if (kind == "PURCHASE" && IsPaidPawn(itemId))
                {
                    cachedPawnOwnerUid = identity.Uid;
                    CachedOwnedPawnIds.Add(itemId);
                }
                if (kind == "RESET_PURCHASES")
                {
                    cachedMapUid = identity.Uid; CachedOwnedMaps.Clear();
                    cachedPawnOwnerUid = identity.Uid;
                    CachedOwnedPawnIds.Clear();
                    cachedEmoteUid = identity.Uid;
                    CachedPaidEmotes.Clear();
                }


                // Ledger is a second, immutable receipt write validated against
                // this operation in Firebase rules. It does NOT award items,
                // restore Gold, or claim atomicity with the wallet commit.
                if (kind == "PURCHASE" || kind == "DAILY" ||
                    kind == "DEV_GRANT" || kind == "RESET_PURCHASES")
                    await TryWriteReceiptAsync(identity, doc.operation);


                Debug.Log("ATB_PHASE19C_RTDB_OP type=" + kind +
                          " result=success uid=" + Short(identity.Uid));
                return new AtlasBoardMetaEconomyOperationResult
                {
                    Success = true, Type = kind, ItemId = itemId,
                    TransactionId = doc.operation.id,
                    Gold = doc.wallet.gold, AtlasCoin = doc.wallet.atlasCoin,
                    Detail = kind == "DAILY" ? "+50 Gold" : ""
                };
            }
            return Failure("meta.error.concurrent_write");
        }
        catch (Exception exception)
        {
            string safeReason = Sanitize(exception.Message);
            Debug.LogWarning("ATB_STORE_MUTATION stage=failed kind=" + kind +
                " reason=" + safeReason + " uid_suffix=" + ShortUid(identity.Uid));
            // Buying an ordinary item / collecting a daily award has nothing
            // to do with owner QA. Never show a development-identity error to
            // a registered player for a rules or wallet-migration failure.
            bool privileged = kind == "DEV_GRANT" || kind == "RESET_PURCHASES";
            return Failure(privileged && exception.Message != null &&
                exception.Message.Contains("RTDB_HTTP_401")
                ? "meta.error.identity_unavailable" : "meta.error.refresh_failed",
                exception.Message);
        }
    }


    private static async Task ClearPublicFrameBestEffortAsync()
    {
        try
        {
            AtlasBoardAccountService service = AtlasBoardAccountService.Instance;
            if (service == null) return;
            AtlasAccountSnapshot account = await service.LoadCurrentAccountAsync();
            if (account != null && !string.IsNullOrEmpty(account.ProfileFrameId))
                await service.UpdatePublicProfileAsync(
                    account.DisplayName, account.AvatarId, "");
        }
        catch (Exception exception)
        {
            Debug.LogWarning("ATB_PHASE19C_FRAME_RESET public_profile_retry_needed " +
                             Sanitize(exception.Message));
        }
    }


    private static AtlasBoardMetaEconomySnapshot NewSnapshot()
    {
        AtlasBoardMetaEconomySnapshot result = new AtlasBoardMetaEconomySnapshot();
        result.DisplayName = AtlasBoardCanonicalPlayerIdentity.CurrentDisplayName;
        string[] ids = { Halloween, NewYear, Turkiye, Usa,
            "frame_neon_circuit", "frame_royal_crown", "frame_ocean_tide",
            "frame_sakura_blossom", "frame_cosmic_nova", "frame_forest_spirit",
            Midnight, Maple, Aurora, "board_atlas_horizon", "map_germany", "map_mexico", "map_world", "dice_ruby_red", "dice_ocean_blue", "dice_midnight_black", "dice_marble",
            "ghost_01", "ghost_02", "ghost_03", "ghost_04", "ghost_05", "ghost_06", "ghost_07", "vehicle_police", "vehicle_taxi", "vehicle_pickup" };
        var catalogIds = new System.Collections.Generic.List<string>(ids);
        foreach (var emote in AtlasBoardEmotes.All)
            if (emote.Gold > 0) catalogIds.Add(emote.Id);
        foreach (string id in catalogIds)
        {
            result.Items.Add(new AtlasBoardStorefrontItem
            {
                ItemId = id, ItemType = IsPremiumMap(id) ? "map" : AtlasBoardEmotes.IsPaid(id) ? "emote" : IsPaidPawn(id) ? "pawn" : IsBoard(id) ? "board_theme" : IsDice(id) ? "dice_skin" : "profile_frame",
                DisplayName = AtlasBoardEmotes.IsPaid(id) ? AtlasBoardEmotes.Name(id, "en") : AtlasBoardShopContentDraft.LocalizedName(id, "en"),
                GoldPrice = Price(id), AtlasCoinPrice = 0,
                SortOrder = 200, CatalogVersion = 19,
                PreviewOnly = true, Owned = false
            });
        }
        return result;
    }


    // Refresh an EXISTING verified REST password session without ever creating
    // an anonymous UID or changing wallet ownership. The Lobby bridge owns the
    // encrypted refresh credential and enforces the UID-continuity contract.
    // A client refresh cannot override any database rule or admin permission.
    private static async Task<bool> RefreshRegisteredRestBearerAsync(Identity identity)
    {
        if (identity == null || !identity.IsRestGuest || identity.IsAnonymous ||
            !AtlasBoardAccountFormQualityGate.TryGetVerifiedRegisteredRestPrincipal(
                out string registeredUid) ||
            !string.Equals(registeredUid, identity.Uid, StringComparison.Ordinal))
            return false;
        try
        {
            AtlasBoardLobbyRuntimeBridge bridge =
                UnityEngine.Object.FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>();
            if (bridge == null) return false;
            MethodInfo method = typeof(AtlasBoardLobbyRuntimeBridge).GetMethod(
                "RefreshLiveGuestQaIdentityAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Task<AtlasLobbyOperationResult> refresh = method?.Invoke(bridge, null)
                as Task<AtlasLobbyOperationResult>;
            if (refresh == null || !(await refresh).Success) return false;
            if (!AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(
                    out string liveUid, out string liveToken) ||
                !string.Equals(liveUid, identity.Uid, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(liveToken) ||
                !string.Equals(CurrentUid, identity.Uid, StringComparison.Ordinal))
                return false;
            identity.Token = liveToken;
            Debug.Log("ATB_STORE_AUTH_REFRESH result=success uid_unchanged=true");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_STORE_AUTH_REFRESH result=failed type=" +
                ex.GetType().Name);
            return false;
        }
    }

    // Share the existing UID-preserving REST token refresh with the
    // observational Career dice uploader. This does not grant any currency,
    // privilege or access to a different account.
    /// <summary>Returns only this session's UID-bound Firebase bearer for trusted callables.</summary>
    public static async Task<string> GetCurrentCallableTokenAsync()
    {
        Identity identity = await ResolveIdentityAsync();
        if (identity == null || !string.Equals(identity.Uid, CurrentUid,
                StringComparison.Ordinal)) return "";
        if (identity.IsRestGuest && !identity.IsAnonymous)
            await RefreshRegisteredRestBearerAsync(identity);
        return string.Equals(identity.Uid, CurrentUid, StringComparison.Ordinal)
            ? identity.Token : "";
    }

    public static async Task<string> RefreshRegisteredRestTokenForCurrentUidAsync(
        string uid)
    {
        if (string.IsNullOrWhiteSpace(uid) ||
            !string.Equals(uid, CurrentUid, StringComparison.Ordinal)) return "";
        Identity identity = await ResolveIdentityAsync();
        if (identity == null || !string.Equals(identity.Uid, uid,
                StringComparison.Ordinal) ||
            !await RefreshRegisteredRestBearerAsync(identity)) return "";
        return string.Equals(uid, CurrentUid, StringComparison.Ordinal)
            ? identity.Token : "";
    }

    private static async Task<Identity> ResolveIdentityAsync()
    {
        // The standalone two-client Guest owns a persistent anonymous Firebase
        // UID through the lobby REST Auth flow. Reuse that exact identity for
        // the SAME canonical wallet; never fall back to the Editor/native UID.
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
        {
            if (AtlasBoardLobbyRuntimeBridge.TryGetLiveGuestRewardIdentity(
                    out string guestUid, out string guestToken))
            {
                // The QA client must not reinterpret a verified password
                // account as anonymous just because it authenticates via REST.
                // Firebase's server-side token and RTDB rules remain authority.
                bool registered = AtlasBoardAccountFormQualityGate
                    .TryGetVerifiedRegisteredRestPrincipal(out string verifiedUid) &&
                    string.Equals(guestUid, verifiedUid, StringComparison.Ordinal);
                return new Identity
                {
                    User = null,
                    Uid = guestUid,
                    Token = guestToken,
                    IsAnonymous = !registered,
                    IsRestGuest = true
                };
            }
            return null;
        }

        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null || string.IsNullOrWhiteSpace(user.UserId))
                return null;

            string token = await user.TokenAsync(false);
            if (string.IsNullOrWhiteSpace(token)) return null;
            return new Identity
            {
                User = user,
                Uid = user.UserId,
                Token = token,
                IsAnonymous = user.IsAnonymous,
                IsRestGuest = false
            };
        }
        catch { return null; }
    }


    private static async Task<bool> RegisteredRestProfileExistsAsync(Identity identity)
    {
        try
        {
            // Both the REST UID and its bearer token are held by Identity.
            // Permission to read this node is enforced by RTDB Security Rules.
            string accountPath = "/users/" + UnityWebRequest.EscapeURL(identity.Uid) +
                "/general/accountType.json";
            Response result = await RequestAsync(identity, "GET", accountPath, null, null);
            if ((result.Status == 401 || result.Status == 403) &&
                await RefreshRegisteredRestBearerAsync(identity))
                result = await RequestAsync(identity, "GET", accountPath, null, null);
            bool registered = result.Status == 200 &&
                string.Equals((result.Body ?? "").Trim(), "\"registered\"",
                    StringComparison.Ordinal);
            Debug.Log("ATB_STORE_SESSION uid_suffix=" + ShortUid(identity.Uid) +
                " rest_verified=true account_type_registered=" + registered +
                " http=" + result.Status + " native_auth_required=false");
            return registered;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_STORE_SESSION result=profile_read_failed type=" +
                ex.GetType().Name);
            return false;
        }
    }

    private static async Task<bool> IsDeveloperTesterAsync(Identity identity)
    {
        try
        {
            Response r = await RequestAsync(identity, "GET",
                "/devWalletTesters/" + UnityWebRequest.EscapeURL(identity.Uid) +
                ".json", null, null);
            return r.Status == 200 && r.Body != null &&
                   r.Body.Trim() == "true";
        }
        catch { return false; }
    }


    private static async Task<ReadState> ReadAsync(Identity identity)
    {
        Response response = await RequestAsync(identity, "GET",
            EconomyPath(identity.Uid), null, null, true);
        if ((response.Status == 401 || response.Status == 403) &&
            await RefreshRegisteredRestBearerAsync(identity))
            response = await RequestAsync(identity, "GET",
                EconomyPath(identity.Uid), null, null, true);
        if (response.Status != 200)
        {
            Debug.LogWarning("ATB_STORE_WALLET_READ result=denied_or_unavailable http=" +
                response.Status + " uid_suffix=" + ShortUid(identity.Uid));
            throw new InvalidOperationException("RTDB_HTTP_" + response.Status);
        }
        Economy doc = string.IsNullOrWhiteSpace(response.Body) ||
                      response.Body.Trim() == "null"
            ? null : JsonUtility.FromJson<Economy>(response.Body);
        // In-place schema-1 extension. Existing v5.49.10 profile purchases
        // remain intact; legacy wallets without boards are upgraded only on
        // their next otherwise-authorized ETag-protected transaction.
        if (doc != null)
        {
            // An old RTDB wallet can omit dailyReward. Never let a missing
            // first-claim field break unrelated store purchases/equipment.
            if (doc.dailyReward == null) doc.dailyReward = new DailyReward();
            if (doc.inventory == null) doc.inventory = new Inventory();
            if (doc.inventory.profile_frames == null)
                doc.inventory.profile_frames = new Frames();
            if (doc.inventory.boards == null) doc.inventory.boards = new Boards();
            if (doc.inventory.maps == null) doc.inventory.maps = new Maps();
            if (doc.inventory.dice == null) doc.inventory.dice = new Dice();
            if (doc.inventory.pawns == null) doc.inventory.pawns = new Pawns();
            if (doc.inventory.emotes == null) doc.inventory.emotes = new Emotes();
            if (doc.equipped == null) doc.equipped = new Equipped();
            if (doc.equipped.boardId == null) doc.equipped.boardId = "";
            if (doc.equipped.diceId == null) doc.equipped.diceId = "";
            if (doc.equipped.profileFrameId == null) doc.equipped.profileFrameId = "";
        }
        return new ReadState
        {
            ETag = string.IsNullOrEmpty(response.ETag) ? "null_etag" : response.ETag,
            Document = doc,
            Raw = string.IsNullOrWhiteSpace(response.Body) || response.Body.Trim() == "null"
                ? new JObject() : JObject.Parse(response.Body)
        };
    }


    private static async Task InitializeAsync(Identity identity, string etag)
    {
        Economy initial = new Economy();
        initial.operation = new Operation
        {
            id = Guid.NewGuid().ToString("N"),
            type = "INIT", itemId = "", atMs = Now()
        };
        JObject initialJson = JObject.Parse(JsonUtility.ToJson(initial));
        initialJson["dailyReward"] = new JObject { ["lastClaimAt"] = 0L };
        Response response = await RequestAsync(identity, "PUT",
            EconomyPath(identity.Uid),
            initialJson.ToString(Newtonsoft.Json.Formatting.None),
            string.IsNullOrEmpty(etag) ? "null_etag" : etag);
        // Another device may have initialized the same UID at the same time.
        if (response.Status != 412 &&
            (response.Status < 200 || response.Status >= 300))
            throw new InvalidOperationException("INIT_HTTP_" + response.Status);
    }


    private static async Task<Response> RequestAsync(
        Identity identity, string method, string path, string body,
        string etag, bool fetchEtag = false)
    {
        string url = Database + path + (path.Contains("?") ? "&auth=" : "?auth=") +
                     UnityWebRequest.EscapeURL(identity.Token);
        using (UnityWebRequest request = new UnityWebRequest(url, method))
        {
            request.timeout = 15;
            request.downloadHandler = new DownloadHandlerBuffer();
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(
                    Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (!string.IsNullOrEmpty(etag))
                request.SetRequestHeader("if-match", etag);
            if (fetchEtag)
                request.SetRequestHeader("X-Firebase-ETag", "true");


            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);
            TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();
            UnityWebRequestAsyncOperation op = request.SendWebRequest();
            if (op.isDone) done.TrySetResult(true);
            else op.completed += _ => done.TrySetResult(true);
            await done.Task;


            return new Response
            {
                Status = request.responseCode,
                Body = request.downloadHandler != null
                    ? request.downloadHandler.text : "",
                ETag = request.GetResponseHeader("ETag")
            };
        }
    }


    [Serializable]
    private sealed class HistoryReceipt
    {
        public string id;
        public string type;
        public string itemId;
        public long atMs;
    }


    private static string HistoryPath(string uid)
    {
        return "/users/" + UnityWebRequest.EscapeURL(uid) + "/metaHistory";
    }


    private static async Task TryWriteReceiptAsync(Identity identity, Operation operation)
    {
        if (identity == null || operation == null ||
            string.IsNullOrEmpty(operation.id)) return;
        try
        {
            var receipt = new HistoryReceipt
            {
                id = operation.id,
                type = operation.type,
                itemId = operation.itemId ?? "",
                atMs = operation.atMs
            };
            Response response = await RequestAsync(identity, "PUT",
                HistoryPath(identity.Uid) + "/" + operation.id + ".json",
                JsonUtility.ToJson(receipt), null);
            if (response.Status < 200 || response.Status >= 300)
                Debug.LogWarning("ATB_HISTORY_RECEIPT pending=true status=" +
                    response.Status + " (wallet remains committed)");
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_HISTORY_RECEIPT pending=true type=" +
                e.GetType().Name + " (wallet remains committed)");
        }
    }


    private static async Task ReadPromoHistoryAsync(
        Identity identity, AtlasBoardMetaEconomySnapshot snapshot)
    {
        Response response = await RequestAsync(identity, "GET",
            "/users/" + UnityWebRequest.EscapeURL(identity.Uid) +
            "/promoRedemptions.json", null, null);
        if (response.Status != 200 || string.IsNullOrWhiteSpace(response.Body) ||
            response.Body.Trim() == "null") return;
        JObject entries = JObject.Parse(response.Body);
        foreach (JProperty entry in entries.Properties())
        {
            JObject receipt = entry.Value as JObject;
            if (receipt == null) continue;
            string id = (string)receipt["transactionId"] ?? "";
            long at = (long?)receipt["claimedAt"] ?? 0;
            if (id.Length != 64 || at <= 0) continue;
            string rewardType = (string)receipt["rewardType"] ?? "";
            snapshot.History.Add(new AtlasBoardMetaHistoryItem {
                Type = "REDEEM_CODE", TransactionId = id,
                ReferenceId = entry.Name, Status = "completed",
                CurrencyId = rewardType,
                Amount = (int?)receipt["rewardAmount"] ?? 0,
                Detail = (string)receipt["rewardItemId"] ?? "",
                CreatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(at)
                    .UtcDateTime.ToString("o")
            });
        }
    }

    private static async Task ReadHistoryAsync(
        Identity identity, AtlasBoardMetaEconomySnapshot snapshot)
    {
        Response response = await RequestAsync(identity, "GET",
            HistoryPath(identity.Uid) +
                ".json?orderBy=%22atMs%22&limitToLast=100", null, null);
        if (response.Status < 200 || response.Status >= 300)
            throw new InvalidOperationException("HISTORY_HTTP_" + response.Status);
        if (string.IsNullOrWhiteSpace(response.Body) ||
            response.Body.Trim() == "null") return;
        JObject raw = JObject.Parse(response.Body);
        var items = new System.Collections.Generic.List<HistoryReceipt>();
        foreach (JProperty entry in raw.Properties())
        {
            if (!(entry.Value is JObject)) continue;
            HistoryReceipt value;
            try { value = entry.Value.ToObject<HistoryReceipt>(); }
            catch { continue; }
            if (value == null || !string.Equals(entry.Name, value.id,
                    StringComparison.Ordinal)) continue;
            items.Add(value);
        }
        JObject matchClaimsForHistory = null;
        if (items.Exists(x => x.type == "MATCH_REWARD"))
        {
            Response claims = await RequestAsync(identity, "GET", "/careerClaims/" +
                UnityWebRequest.EscapeURL(identity.Uid) + ".json", null, null);
            if (claims.Status == 200 && claims.Body != "null")
                matchClaimsForHistory = JObject.Parse(claims.Body);
        }
        items.Sort((a, b) => b.atMs.CompareTo(a.atMs));
        for (int i = 0; i < items.Count && i < 100; i++)
        {
            var receipt = items[i];
            int amount = receipt.type == "MATCH_REWARD" ?
                Mathf.Clamp((int?)matchClaimsForHistory?[receipt.itemId]?["goldAwarded"] ?? 0, 0, 50) :
                receipt.type == "PURCHASE" ? -Price(receipt.itemId) :
                receipt.type == "DAILY" ? DailyGold :
                receipt.type == "DEV_GRANT" ? DevGold : 0;
            snapshot.History.Add(new AtlasBoardMetaHistoryItem
            {
                Type = receipt.type,
                TransactionId = receipt.id,
                ReferenceId = receipt.itemId ?? "",
                Status = "completed",
                CurrencyId = "gold",
                Amount = amount,
                Detail = receipt.itemId ?? "",
                CreatedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(
                    receipt.atMs).UtcDateTime.ToString("o")
            });
        }
        Debug.Log("ATB_HISTORY_READ source=RTDB receipts=" + items.Count);
    }


    // Immutable receipt scan (read-only): a legacy account may have received
    // +50 Gold before its dailyReward subtree became mandatory. Recover the
    // latest claim from history, not from the currently displayed Gold amount.
    private static long LastDailyReceiptAtMs(AtlasBoardMetaEconomySnapshot snapshot)
    {
        long latest = 0;
        if (snapshot == null || snapshot.History == null) return latest;
        foreach (AtlasBoardMetaHistoryItem item in snapshot.History)
        {
            if (item == null || item.Type != "DAILY" ||
                string.IsNullOrWhiteSpace(item.CreatedAtUtc)) continue;
            if (DateTimeOffset.TryParse(item.CreatedAtUtc, out DateTimeOffset date))
                latest = Math.Max(latest, date.ToUnixTimeMilliseconds());
        }
        return latest;
    }

    private static async Task<long> ReadLastDailyReceiptAtMsAsync(
        Identity identity, bool requireHistory)
    {
        try
        {
            // No arbitrary dailyReward state required to shop: this is only
            // migration evidence, and non-daily operations may continue if
            // history is temporarily unavailable.
            Response response = await RequestAsync(identity, "GET",
                HistoryPath(identity.Uid) + ".json", null, null);
            if (response.Status != 200)
                throw new InvalidOperationException("HISTORY_HTTP_" + response.Status);
            if (string.IsNullOrWhiteSpace(response.Body) ||
                response.Body.Trim() == "null") return 0;
            JObject receipts = JObject.Parse(response.Body);
            long latest = 0;
            foreach (JProperty entry in receipts.Properties())
            {
                JObject receipt = entry.Value as JObject;
                if (receipt == null || (string)receipt["type"] != "DAILY" ||
                    (string)receipt["id"] != entry.Name) continue;
                long value = (long?)receipt["atMs"] ?? 0;
                if (value > 0 && value <= Now() + 30000)
                    latest = Math.Max(latest, value);
            }
            return latest;
        }
        catch (Exception error)
        {
            Debug.LogWarning("ATB_DAILY_LEGACY history_unavailable=true type=" +
                error.GetType().Name);
            if (requireHistory)
                throw new InvalidOperationException("DAILY_HISTORY_UNAVAILABLE");
            return 0;
        }
    }

    private static string EconomyPath(string uid)
    {
        return "/users/" + UnityWebRequest.EscapeURL(uid) +
               "/metaEconomy.json";
    }


    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();


    private static AtlasBoardMetaEconomyOperationResult Failure(
        string key, string detail = "")
    {
        return new AtlasBoardMetaEconomyOperationResult
        {
            Success = false, ErrorKey = key, TechnicalMessage = Sanitize(detail)
        };
    }


    private static string Short(string uid)
    {
        return string.IsNullOrEmpty(uid) ? "unknown" :
            uid.Substring(0, Math.Min(6, uid.Length));
    }


    private static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Length > 100 ? input.Substring(0, 100) : input;
    }
}