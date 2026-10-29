using System;
using System.Text;
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
        public bool IsOwned(string id) =>
            id == Midnight && board_midnight_azure ||
            id == Maple && board_ember_maple ||
            id == Aurora && board_arctic_aurora;
        public void SetOwned(string id, bool value)
        {
            if (id == Midnight) board_midnight_azure = value;
            if (id == Maple) board_ember_maple = value;
            if (id == Aurora) board_arctic_aurora = value;
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
        public bool IsOwned(string id) =>
            id == "map_germany" && map_germany || id == "map_mexico" && map_mexico;
        public void SetOwned(string id, bool owned)
        {
            if (id == "map_germany") map_germany = owned;
            else if (id == "map_mexico") map_mexico = owned;
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
        public Wallet wallet = new Wallet();
        public Inventory inventory = new Inventory();
        public Equipped equipped = new Equipped();
        public DailyReward dailyReward = new DailyReward();
        public DevCounters dev = new DevCounters();
        public Operation operation = new Operation();
    }

    private sealed class Identity
    {
        public FirebaseUser User;
        public string Uid;
        public string Token;
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
    }

    public static bool IsRegistered
    {
        get
        {
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
            AtlasBoardPlatformRuntime steam = AtlasBoardPlatformRuntime.Instance;
            return steam != null && steam.IsSteamProviderActive &&
                   steam.SteamInitialized && steam.SteamId != 0;
        }
    }

    public static string CurrentUid
    {
        get
        {
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
            snapshot.Success = true;
            snapshot.IsGuest = !IsSteamSession;
            snapshot.IsSteamAccount = IsSteamSession;
            snapshot.Gold = 0;
            snapshot.AtlasCoin = 0;
            snapshot.WalletVerified = false;
            return snapshot;
        }

        try
        {
            if (!await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync(
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
            snapshot.IsGuest = identity.User.IsAnonymous && !snapshot.IsSteamAccount;
            snapshot.IsDeveloperTester = await IsDeveloperTesterAsync(identity);
            bool permitted = !identity.User.IsAnonymous || snapshot.IsDeveloperTester;
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
            cachedPawnOwnerUid = identity.Uid;
            CachedOwnedPawnIds.Clear();
            cachedEmoteUid = identity.Uid;
            CachedPaidEmotes.Clear();
            foreach (var emote in AtlasBoardEmotes.All)
                if (emote.Gold > 0 && document.inventory.emotes.IsOwned(emote.Id))
                    CachedPaidEmotes.Add(emote.Id);
            foreach (string pawnId in new [] { "ghost_01", "ghost_02", "ghost_03", "ghost_04", "ghost_05", "ghost_06", "ghost_07", "vehicle_police", "vehicle_taxi", "vehicle_pickup" })
                if (document.inventory.pawns.IsOwned(pawnId)) CachedOwnedPawnIds.Add(pawnId);
            // Presentation cache only; RTDB remains the owner of this value.
            if (identity.Uid == CurrentUid)
                CurrentEquippedDiceId = snapshot.EquippedDiceId;
            snapshot.DailyClaimedAtMs = document.dailyReward.lastClaimAt;

            // Read-only display of immutable, owner-scoped receipts. The
            // main wallet remains the single ETag-authoritative economy.
            if (document.operation != null &&
                (document.operation.type == "PURCHASE" ||
                 document.operation.type == "DAILY" ||
                 document.operation.type == "DEV_GRANT" ||
                 document.operation.type == "RESET_PURCHASES"))
                await TryWriteReceiptAsync(identity, document.operation);
            try { await ReadHistoryAsync(identity, snapshot); }
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
    public static bool IsPremiumMap(string id) => id == "map_germany" || id == "map_mexico";
    public static bool IsPremiumMapOwnedForCurrentUid(string id) =>
        !IsPremiumMap(id) || (cachedMapUid == CurrentUid && CachedOwnedMaps.Contains(id));
    public static bool IsPremiumRoomMap(string mapId) => mapId == "Germany" || mapId == "Mexico";
    public static string MapEntitlementId(string mapId) => mapId == "Germany" ? "map_germany" : mapId == "Mexico" ? "map_mexico" : "";

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
        itemId == Midnight || itemId == Maple || itemId == Aurora;

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
        return await MutateAsync("EQUIP", itemId ?? "", false);
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

    [Serializable]
    private sealed class PublicDiceWire { public string diceSkinId; }

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
            if (!await AtlasBoardRtdbUserProfileMirror.SyncCurrentUserAsync("", false))
                return Failure("meta.error.identity_unavailable");

            // The UID allowlist is ADMIN managed. This exception supports
            // the owner-approved anonymous Steam test identity only.
            if ((identity.User.IsAnonymous || kind == "DEV_GRANT" ||
                 kind == "RESET_PURCHASES") &&
                !await IsDeveloperTesterAsync(identity))
                return Failure("meta.error.dev_not_allowed");

            for (int attempt = 0; attempt < 5; attempt++)
            {
                ReadState state = await ReadAsync(identity);
                if (state.Document == null)
                {
                    await InitializeAsync(identity, state.ETag);
                    continue;
                }

                Economy doc = state.Document;
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
                        doc.inventory.profile_frames = new Frames();
                        doc.inventory.boards = new Boards();
                        doc.inventory.maps = new Maps();
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

                Response written = await RequestAsync(identity, "PUT",
                    EconomyPath(identity.Uid), JsonUtility.ToJson(doc), state.ETag);
                if (written.Status == 412) continue; // conflicting device: re-read
                if (written.Status < 200 || written.Status >= 300)
                    throw new InvalidOperationException("RTDB_HTTP_" + written.Status);

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
            Debug.LogWarning("ATB_PHASE19C_RTDB_OP type=" + kind +
                             " result=failed " + Sanitize(exception.Message));
            return Failure("meta.error.refresh_failed", exception.Message);
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
            Midnight, Maple, Aurora, "map_germany", "map_mexico", "dice_ruby_red", "dice_ocean_blue", "dice_midnight_black", "dice_marble",
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

    private static async Task<Identity> ResolveIdentityAsync()
    {
        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null || string.IsNullOrWhiteSpace(user.UserId))
                return null;

            string token = await user.TokenAsync(false);
            if (string.IsNullOrWhiteSpace(token)) return null;
            return new Identity { User = user, Uid = user.UserId, Token = token };
        }
        catch { return null; }
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
        if (response.Status != 200)
            throw new InvalidOperationException("RTDB_HTTP_" + response.Status);
        Economy doc = string.IsNullOrWhiteSpace(response.Body) ||
                      response.Body.Trim() == "null"
            ? null : JsonUtility.FromJson<Economy>(response.Body);
        // In-place schema-1 extension. Existing v5.49.10 profile purchases
        // remain intact; legacy wallets without boards are upgraded only on
        // their next otherwise-authorized ETag-protected transaction.
        if (doc != null)
        {
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
            Document = doc
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
        Response response = await RequestAsync(identity, "PUT",
            EconomyPath(identity.Uid), JsonUtility.ToJson(initial),
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
        items.Sort((a, b) => b.atMs.CompareTo(a.atMs));
        for (int i = 0; i < items.Count && i < 100; i++)
        {
            var receipt = items[i];
            int amount = receipt.type == "PURCHASE" ? -Price(receipt.itemId) :
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
