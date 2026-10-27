using System;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
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
    private sealed class Inventory
    {
        public Frames profile_frames = new Frames();
        public Boards boards = new Boards();
    }

    [Serializable]
    private sealed class Equipped { public string profileFrameId = ""; public string boardId = ""; }

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
            case Midnight: return 1200;
            case Maple: return 1500;
            case Aurora: return 1700;
            default: return 0;
        }
    }

    public static async Task<AtlasBoardMetaEconomySnapshot> LoadAsync()
    {
        AtlasBoardMetaEconomySnapshot snapshot = NewSnapshot();
        Identity identity = await ResolveIdentityAsync();
        if (identity == null)
        {
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
            snapshot.DailyClaimedAtMs = document.dailyReward.lastClaimAt;

            foreach (AtlasBoardStorefrontItem item in snapshot.Items)
            {
                item.Owned = IsBoard(item.ItemId)
                    ? document.inventory.boards.IsOwned(item.ItemId)
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
                        if (IsBoard(itemId) ? doc.inventory.boards.IsOwned(itemId) : doc.inventory.profile_frames.IsOwned(itemId))
                            return Failure("meta.error.already_owned");
                        if (doc.wallet.gold < Price(itemId))
                            return Failure("meta.error.insufficient_funds");
                        doc.wallet.gold -= Price(itemId);
                        if (IsBoard(itemId)) doc.inventory.boards.SetOwned(itemId, true);
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
                    case "RESET_PURCHASES":
                        doc.wallet.gold += doc.inventory.profile_frames.RefundAmount() + doc.inventory.boards.RefundAmount();
                        doc.inventory.profile_frames = new Frames();
                        doc.inventory.boards = new Boards();
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
                    await ClearPublicFrameBestEffortAsync();

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
            Midnight, Maple, Aurora };
        foreach (string id in ids)
        {
            result.Items.Add(new AtlasBoardStorefrontItem
            {
                ItemId = id, ItemType = IsBoard(id) ? "board_theme" : "profile_frame",
                DisplayName = AtlasBoardShopContentDraft.LocalizedName(id, "en"),
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
            if (doc.equipped == null) doc.equipped = new Equipped();
            if (doc.equipped.boardId == null) doc.equipped.boardId = "";
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
        string url = Database + path + "?auth=" +
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
