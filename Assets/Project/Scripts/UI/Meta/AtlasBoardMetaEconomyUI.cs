using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AtlasBoardMetaEconomyUI : MonoBehaviour
{
    private enum StorePage
    {
        Home,
        Items,
        Event,
        Redeem,
        History
    }

    private static readonly Color BackgroundBlue =
        new Color32(151, 205, 232, 255);
    private static readonly Color Blue =
        new Color32(28, 157, 211, 255);
    private static readonly Color DarkBlue =
        new Color32(19, 113, 163, 255);
    private static readonly Color Green =
        new Color32(139, 170, 16, 255);
    private static readonly Color Orange =
        new Color32(248, 175, 0, 255);
    private static readonly Color Red =
        new Color32(226, 39, 90, 255);
    private static readonly Color Cream =
        new Color32(246, 241, 231, 255);
    private static readonly Color TextDark =
        new Color32(61, 62, 66, 255);
    private static readonly Color PanelBlue =
        new Color32(226, 239, 245, 255);

    private const float OperationCooldownSeconds = 1.5f;

    private AtlasBoardMetaEconomyBridge bridge;
    private Canvas canvas;
    private GameObject storeRoot;
    private GameObject confirmRoot;
    private GameObject resultRoot;
    private RectTransform contentRoot;
    private TMP_Text titleText;
    private TMP_Text storeProfileNameText;
    private TMP_Text storeGoldText;
    private TMP_Text storeCoinText;
    private TMP_Text storeAvatarInitialText;
    private TMP_Text confirmTitleText;
    private TMP_Text confirmBodyText;
    private TMP_Text confirmBuyText;
    private TMP_Text confirmCancelText;
    private TMP_Text resultTitleText;
    private TMP_Text resultBodyText;
    private TMP_Text resultOkText;
    private TMP_InputField redeemInput;
    private RectTransform redeemHistoryContent;
    private RectTransform itemContent;
    private RectTransform historyContent;
    private readonly List<Button> tabButtons = new List<Button>();
    private readonly Dictionary<string, string> operationKeys =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private AtlasBoardMetaEconomySnapshot snapshot;
    private AtlasBoardSeasonalOverview seasonalOverview;
    private AtlasBoardStorefrontItem pendingItem;
    private string pendingCurrency = string.Empty;
    private string activeCategory = "all";
    private StorePage currentPage = StorePage.Home;
    private bool busy;
    private float nextOperationAllowedAt;
    private bool shopCardBound;
    private float nextBindAttemptAt;

    private void Awake()
    {
        bridge = GetComponent<AtlasBoardMetaEconomyBridge>();
        BuildUi();
        storeRoot.SetActive(false);
        confirmRoot.SetActive(false);
        resultRoot.SetActive(false);
        AtlasBoardLocalizationManager.LanguageChanged += HandleLanguageChanged;
    }

    private async void Start()
    {
        if (bridge == null)
        {
            return;
        }

        AtlasBoardMetaEconomySnapshot initial =
            await bridge.RefreshAsync();

        if (initial != null && initial.Success)
        {
            snapshot = initial;
            UpdateWalletText();
        }
    }

    private void OnDestroy()
    {
        AtlasBoardLocalizationManager.LanguageChanged -= HandleLanguageChanged;
    }

    private void Update()
    {
        if (!shopCardBound && Time.unscaledTime >= nextBindAttemptAt)
        {
            nextBindAttemptAt = Time.unscaledTime + 1f;
            TryBindExistingShopCard();
        }

        if (storeRoot != null && storeRoot.activeSelf)
        {
            CloseLegacyShopModal();
        }
    }

    private void TryBindExistingShopCard()
    {
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (Button button in buttons)
        {
            if (button == null ||
                !button.gameObject.scene.IsValid() ||
                !string.Equals(
                    button.gameObject.name,
                    "ShopCard",
                    StringComparison.Ordinal))
            {
                continue;
            }

            button.onClick.AddListener(OpenFromMainMenu);
            shopCardBound = true;
            Debug.Log(
                "AtlasBoard Phase 9 Store v2 bound to the existing Main Menu ShopCard. " +
                "No duplicate Shop launcher was created.",
                this);
            return;
        }
    }

    private void OpenFromMainMenu()
    {
        CloseLegacyShopModal();
        storeRoot.SetActive(true);
        confirmRoot.SetActive(false);
        resultRoot.SetActive(false);
        currentPage = StorePage.Home;
        RefreshLanguageAndPage();
        RefreshAsync();
    }

    private void CloseStore()
    {
        if (busy)
        {
            ShowMessagePopup(
                "store.popup.info_title",
                T("store.response.wait"));
            return;
        }

        confirmRoot.SetActive(false);
        resultRoot.SetActive(false);
        storeRoot.SetActive(false);
    }

    private static void CloseLegacyShopModal()
    {
        AtlasBoardMainMenuController controller =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardMainMenuController>();
        controller?.CloseModal();
    }

    private void HandleLanguageChanged()
    {
        ApplyResolvedStoreFonts();
        UpdateWalletText();

        if (storeRoot != null && storeRoot.activeSelf)
        {
            RefreshLanguageAndPage();
            ApplyResolvedStoreFonts();
        }
    }

    private void RefreshLanguageAndPage()
    {
        titleText.text = T("store.title");
        RebuildNavigation();
        RebuildCurrentPage();
        UpdateWalletText();
        if (confirmBuyText != null)
        {
            confirmBuyText.text = T("store.purchase.buy");
        }
        if (confirmCancelText != null)
        {
            confirmCancelText.text = T("store.common.cancel");
        }
        if (resultOkText != null)
        {
            resultOkText.text = T("store.purchase.result_ok");
        }

        ApplyResolvedStoreFonts();
    }

    private bool CanStartOperation()
    {
        if (busy)
        {
            ShowMessagePopup(
                "store.popup.info_title",
                T("store.response.wait"));
            return false;
        }

        if (Time.unscaledTime < nextOperationAllowedAt)
        {
            ShowMessagePopup(
                "store.popup.info_title",
                T("store.response.cooldown"));
            return false;
        }

        return true;
    }

    private void BeginOperation(string statusKey)
    {
        _ = statusKey;
        busy = true;
        SetUiInteractable(false);
    }

    private void EndOperation()
    {
        busy = false;
        nextOperationAllowedAt =
            Time.unscaledTime + OperationCooldownSeconds;
        SetUiInteractable(true);
    }

    private void SetUiInteractable(bool value)
    {
        if (storeRoot == null)
        {
            return;
        }

        Button[] buttons = storeRoot.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button != null)
            {
                button.interactable = value;
            }
        }

        if (redeemInput != null)
        {
            redeemInput.interactable = value;
        }
    }

    private async void RefreshAsync()
    {
        if (!CanStartOperation() || bridge == null)
        {
            return;
        }

        BeginOperation("store.response.loading");
        AtlasBoardMetaEconomySnapshot result = await bridge.RefreshAsync();
        AtlasBoardSeasonalOverview seasonal = result.Success
            ? await bridge.GetSeasonalOverviewAsync()
            : null;
        EndOperation();

        if (!result.Success)
        {
            ShowMessagePopup(
                "store.popup.error_title",
                LocalizeError(result.ErrorKey));
            return;
        }

        snapshot = result;
        if (seasonal != null)
        {
            seasonalOverview = seasonal;
        }
        UpdateWalletText();
        RebuildCurrentPage();
    }

    private async void SeedWalletAsync()
    {
        if (!CanStartOperation() || bridge == null)
        {
            return;
        }

        BeginOperation("store.response.processing");
        AtlasBoardMetaEconomyOperationResult result =
            await bridge.SeedDevelopmentWalletAsync();
        EndOperation();

        if (!result.Success)
        {
            ShowMessagePopup(
                "store.popup.error_title",
                LocalizeError(result.ErrorKey));
            return;
        }

        ShowMessagePopup(
            "store.popup.dev_title",
            T("store.response.dev_wallet_done"));
        RefreshAfterOperationAsync();
    }

    private async void ResetDevelopmentPurchasesAsync()
    {
        if (!CanStartOperation() || bridge == null)
        {
            return;
        }

        BeginOperation("store.response.processing");
        AtlasBoardMetaEconomyOperationResult result =
            await bridge.ResetDevelopmentPurchasesAsync();
        EndOperation();

        if (!result.Success)
        {
            ShowMessagePopup(
                "store.popup.error_title",
                LocalizeError(result.ErrorKey));
            return;
        }

        ShowMessagePopup(
            "store.popup.dev_title",
            T(
                "store.response.dev_reset_done",
                result.Detail ?? "0"));
        RefreshAfterOperationAsync();
    }

    private async void ClaimDailyAsync()
    {
        if (!CanStartOperation() || bridge == null)
        {
            return;
        }

        BeginOperation("store.response.processing");
        AtlasBoardMetaEconomyOperationResult result =
            await bridge.ClaimDailyDevelopmentRewardAsync();
        EndOperation();

        if (!result.Success)
        {
            ShowMessagePopup(
                "store.popup.error_title",
                LocalizeError(result.ErrorKey));
            return;
        }

        ShowMessagePopup(
            "store.popup.daily_title",
            result.AlreadyClaimed
                ? T("store.response.daily_already")
                : T("store.response.daily_claimed"));
        RefreshAfterOperationAsync();
    }

    private async void RefreshAfterOperationAsync()
    {
        await System.Threading.Tasks.Task.Delay(1600);
        RefreshAsync();
    }

    private void AskPurchase(
        AtlasBoardStorefrontItem item,
        string currency)
    {
        if (!CanStartOperation() || item == null || item.Owned)
        {
            return;
        }

        pendingItem = item;
        pendingCurrency = currency;
        int price = currency == "gold"
            ? item.GoldPrice
            : item.AtlasCoinPrice;

        confirmTitleText.text = T("store.purchase.confirm_title");
        confirmBodyText.text =
            $"<b>{Escape(LocalizeItemName(item))}</b>\n\n" +
            $"{price:N0} {CurrencyLabel(currency)}\n\n" +
            T("store.purchase.confirm_body");
        confirmRoot.SetActive(true);
    }

    private async void ConfirmPurchaseAsync()
    {
        if (!CanStartOperation() ||
            pendingItem == null ||
            bridge == null)
        {
            return;
        }

        string operationName =
            $"purchase:{pendingItem.ItemId}:{pendingCurrency}";
        string operationId = GetOrCreateOperationKey(operationName);

        confirmRoot.SetActive(false);
        BeginOperation("store.response.processing");
        AtlasBoardStorefrontItem attemptedItem = pendingItem;
        string attemptedCurrency = pendingCurrency;
        int attemptedPrice = attemptedCurrency == "gold"
            ? attemptedItem.GoldPrice
            : attemptedItem.AtlasCoinPrice;

        AtlasBoardMetaEconomyOperationResult result =
            await bridge.PurchaseAsync(
                attemptedItem.ItemId,
                attemptedCurrency,
                operationId);
        EndOperation();

        if (!result.Success)
        {
            string localizedError = LocalizeError(result.ErrorKey);

            if (IsDeterministicBusinessFailure(result.ErrorKey))
            {
                operationKeys.Remove(operationName);
            }

            ShowPurchaseResult(
                false,
                attemptedItem,
                attemptedCurrency,
                attemptedPrice,
                -1,
                localizedError);

            pendingItem = null;
            pendingCurrency = string.Empty;
            return;
        }

        operationKeys.Remove(operationName);
        int balanceAfter = attemptedCurrency == "gold"
            ? result.Gold
            : result.AtlasCoin;

        ShowPurchaseResult(
            true,
            attemptedItem,
            attemptedCurrency,
            attemptedPrice,
            balanceAfter,
            string.Empty);

        pendingItem = null;
        pendingCurrency = string.Empty;
        RefreshAfterOperationAsync();
    }

    private async void RedeemAsync()
    {
        if (!CanStartOperation() || bridge == null || redeemInput == null)
        {
            return;
        }

        string code = (redeemInput.text ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length < 4)
        {
            ShowMessagePopup(
                "store.popup.error_title",
                T("store.error.promo_invalid"));
            return;
        }

        string operationName = "redeem:" + code;
        string operationId = GetOrCreateOperationKey(operationName);

        BeginOperation("store.response.processing");
        AtlasBoardMetaEconomyOperationResult result =
            await bridge.RedeemCodeAsync(code, operationId);
        EndOperation();

        if (!result.Success)
        {
            ShowMessagePopup(
                "store.popup.error_title",
                LocalizeError(result.ErrorKey));
            return;
        }

        operationKeys.Remove(operationName);
        redeemInput.text = string.Empty;
        ShowMessagePopup(
            "store.popup.redeem_title",
            T(
                "store.response.redeem_done",
                DescribeOperationRewards(result.Rewards)));
        RefreshAfterOperationAsync();
    }

    private string GetOrCreateOperationKey(string operationName)
    {
        if (!operationKeys.TryGetValue(operationName, out string value) ||
            string.IsNullOrWhiteSpace(value))
        {
            value = $"phase9-ui-v2-{Guid.NewGuid():N}";
            operationKeys[operationName] = value;
        }

        return value;
    }

    private void ChangePage(StorePage page)
    {
        if (busy)
        {
            return;
        }

        currentPage = page;
        RebuildNavigation();
        RebuildCurrentPage();
    }

    private void ChangeCategory(string category)
    {
        activeCategory = category;
        RebuildItemsPage();
    }

    private void RebuildCurrentPage()
    {
        if (contentRoot == null)
        {
            return;
        }

        ClearChildren(contentRoot);
        itemContent = null;
        historyContent = null;
        redeemHistoryContent = null;
        redeemInput = null;

        switch (currentPage)
        {
            case StorePage.Items:
                RebuildItemsPage();
                break;
            case StorePage.Event:
                RebuildEventPage();
                break;
            case StorePage.Redeem:
                RebuildRedeemPage();
                break;
            case StorePage.History:
                RebuildHistoryPage();
                break;
            default:
                RebuildHomePage();
                break;
        }
    }

    private void RebuildHomePage()
    {
        CreateSectionTitle(contentRoot, T("store.home.title"));

        GameObject daily =
            CreateCard(
                contentRoot,
                "DailyRewardCard",
                new Vector2(-285f, 115f),
                new Vector2(500f, 225f),
                Cream);
        AddCardTitle(daily.transform, T("store.home.daily_title"), Green);
        AddCardBody(daily.transform, T("store.home.daily_body"));
        Button claim = CreateWideButton(
            daily.transform,
            T("store.home.daily_claim"),
            Green,
            new Vector2(0f, -62f));
        claim.onClick.AddListener(ClaimDailyAsync);

        GameObject featured =
            CreateCard(
                contentRoot,
                "SeasonalCard",
                new Vector2(285f, 115f),
                new Vector2(500f, 225f),
                Cream);
        AddCardTitle(featured.transform, T("seasonal.home.title"), Orange);
        AddCardBody(
            featured.transform,
            seasonalOverview != null &&
            seasonalOverview.Event != null &&
            seasonalOverview.Event.Active
                ? T(seasonalOverview.Event.SubtitleKey)
                : T("seasonal.home.inactive"));
        Button browse = CreateWideButton(
            featured.transform,
            T("seasonal.home.open"),
            Orange,
            new Vector2(0f, -62f));
        browse.onClick.AddListener(() => ChangePage(StorePage.Event));

        GameObject redeem =
            CreateCard(
                contentRoot,
                "RedeemCard",
                new Vector2(-285f, -145f),
                new Vector2(500f, 225f),
                Cream);
        AddCardTitle(redeem.transform, T("store.home.redeem_title"), Blue);
        AddCardBody(redeem.transform, T("store.home.redeem_body"));
        Button redeemButton = CreateWideButton(
            redeem.transform,
            T("store.home.redeem_open"),
            Blue,
            new Vector2(0f, -62f));
        redeemButton.onClick.AddListener(() => ChangePage(StorePage.Redeem));

        GameObject dev =
            CreateCard(
                contentRoot,
                "DevCard",
                new Vector2(285f, -145f),
                new Vector2(500f, 225f),
                PanelBlue);
        AddCardTitle(dev.transform, T("store.home.dev_title"), DarkBlue);
        AddCardBody(dev.transform, T("store.home.dev_body"));
        Button devWallet = CreateSmallButton(
            dev.transform,
            T("store.home.dev_wallet"),
            DarkBlue,
            new Vector2(-120f, -62f),
            new Vector2(220f, 52f));
        devWallet.onClick.AddListener(SeedWalletAsync);

        Button resetPurchases = CreateSmallButton(
            dev.transform,
            T("store.home.dev_reset"),
            Red,
            new Vector2(120f, -62f),
            new Vector2(220f, 52f));
        resetPurchases.onClick.AddListener(
            ResetDevelopmentPurchasesAsync);

    }

    private void RebuildItemsPage()
    {
        ClearChildren(contentRoot);
        CreateSectionTitle(contentRoot, T("store.items.title"));

        string[] categories =
        {
            "all", "pawn", "dice_skin", "board_theme",
            "profile_frame", "emote", "animation_pack"
        };

        string[] keys =
        {
            "store.category.all", "store.category.pawns",
            "store.category.dice", "store.category.boards",
            "store.category.profile", "store.category.emotes",
            "store.category.motion"
        };

        float startX = -486f;
        for (int i = 0; i < categories.Length; i++)
        {
            string captured = categories[i];
            Button button = CreateSmallButton(
                contentRoot,
                T(keys[i]),
                captured == activeCategory ? DarkBlue : Blue,
                new Vector2(startX + i * 160f, 190f),
                new Vector2(148f, 44f));
            button.onClick.AddListener(() => ChangeCategory(captured));
        }

        itemContent = BuildScrollList(
            contentRoot,
            "StoreItems",
            new Vector2(0f, -35f),
            new Vector2(1110f, 390f),
            new Color32(235, 240, 243, 245));

        if (snapshot == null)
        {
            AddInfoRow(itemContent, T("store.response.loading"));
            return;
        }

        IEnumerable<AtlasBoardStorefrontItem> items = snapshot.Items;
        if (activeCategory != "all")
        {
            items = items.Where(item =>
                string.Equals(
                    item.ItemType,
                    activeCategory,
                    StringComparison.OrdinalIgnoreCase));
        }

        List<AtlasBoardStorefrontItem> list = items.ToList();
        if (list.Count == 0)
        {
            AddInfoRow(itemContent, T("store.items.empty"));
            return;
        }

        foreach (AtlasBoardStorefrontItem item in list)
        {
            AddStoreItemRow(itemContent, item);
        }
    }

    private void AddStoreItemRow(
        Transform parent,
        AtlasBoardStorefrontItem item)
    {
        GameObject row = CreateListRow(parent, 108f, Cream);

        CreatePanel(
            row.transform,
            "ItemAccent",
            new Vector2(-515f, 0f),
            new Vector2(8f, 92f),
            ItemAccentColor(item.ItemType));

        TMP_Text name = CreateText(
            row.transform,
            19f,
            FontStyles.Bold,
            TextAlignmentOptions.TopLeft,
            TextDark);
        SetAnchored(
            name.rectTransform,
            new Vector2(-300f, 20f),
            new Vector2(410f, 34f));
        name.enableAutoSizing = true;
        name.fontSizeMin = 14f;
        name.fontSizeMax = 19f;
        name.text = LocalizeItemName(item);

        TMP_Text type = CreateText(
            row.transform,
            12f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft,
            new Color32(100, 104, 110, 255));
        SetAnchored(
            type.rectTransform,
            new Vector2(-300f, -23f),
            new Vector2(410f, 30f));
        type.text =
            T(ItemTypeKey(item.ItemType)) +
            $"  •  catalog v{Mathf.Max(1, item.CatalogVersion)}";

        if (item.Owned)
        {
            GameObject ownedPlate = CreatePanel(
                row.transform,
                "OwnedPlate",
                new Vector2(360f, 0f),
                new Vector2(300f, 54f),
                new Color32(234, 242, 213, 255));

            TMP_Text owned = CreateText(
                ownedPlate.transform,
                16f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                Green);
            Stretch(owned.rectTransform, 6f);
            owned.text = T("store.items.owned");
            return;
        }

        Button gold = CreateSmallButton(
            row.transform,
            T("store.items.buy_gold", item.GoldPrice),
            Green,
            new Vector2(225f, 0f),
            new Vector2(190f, 54f));
        gold.interactable = item.GoldPrice > 0;
        gold.onClick.AddListener(() => AskPurchase(item, "gold"));

        Button coin = CreateSmallButton(
            row.transform,
            T("store.items.buy_coin", item.AtlasCoinPrice),
            Orange,
            new Vector2(430f, 0f),
            new Vector2(190f, 54f));
        coin.interactable = item.AtlasCoinPrice > 0;
        coin.onClick.AddListener(() => AskPurchase(item, "atlas_coin"));
    }

    private async void ClaimSeasonalChallengeAsync(string challengeId)
    {
        if (!CanStartOperation() || bridge == null) return;
        BeginOperation("store.response.processing");
        AtlasBoardMetaEconomyOperationResult result =
            await bridge.ClaimSeasonalChallengeAsync(challengeId);
        EndOperation();
        if (!result.Success)
        {
            ShowMessagePopup("store.popup.error_title", LocalizeError(result.ErrorKey));
            return;
        }
        ShowMessagePopup("seasonal.popup.challenge_title", T("seasonal.response.challenge_claimed"));
        RefreshAfterOperationAsync();
    }

    private async void ClaimSeasonalTrackAsync(string tierId)
    {
        if (!CanStartOperation() || bridge == null) return;
        BeginOperation("store.response.processing");
        AtlasBoardMetaEconomyOperationResult result =
            await bridge.ClaimSeasonalTrackTierAsync(tierId);
        EndOperation();
        if (!result.Success)
        {
            ShowMessagePopup("store.popup.error_title", LocalizeError(result.ErrorKey));
            return;
        }
        ShowMessagePopup("seasonal.popup.track_title", T("seasonal.response.track_claimed"));
        RefreshAfterOperationAsync();
    }

    private async void BuySeasonalItemAsync(AtlasBoardSeasonalLimitedItem item)
    {
        if (!CanStartOperation() || bridge == null || item == null) return;
        string operationName = "seasonal-buy:" + item.ItemId;
        string operationId = GetOrCreateOperationKey(operationName);
        BeginOperation("store.response.processing");
        AtlasBoardMetaEconomyOperationResult result =
            await bridge.PurchaseSeasonalLimitedItemAsync(item.ItemId, operationId);
        EndOperation();
        if (!result.Success)
        {
            if (IsDeterministicBusinessFailure(result.ErrorKey))
            {
                operationKeys.Remove(operationName);
            }
            ShowMessagePopup("store.popup.error_title", LocalizeError(result.ErrorKey));
            return;
        }
        operationKeys.Remove(operationName);
        ShowMessagePopup(
            "seasonal.popup.purchase_title",
            T("seasonal.response.purchase_done", item.DisplayName));
        RefreshAfterOperationAsync();
    }

    private void RebuildEventPage()
    {
        CreateSectionTitle(contentRoot, T("seasonal.page.title"));
        if (seasonalOverview == null || !seasonalOverview.Success)
        {
            AddInfoRow(contentRoot, T("seasonal.response.loading"));
            return;
        }
        if (seasonalOverview.Event == null || !seasonalOverview.Event.Active)
        {
            AddInfoRow(contentRoot, T("seasonal.home.inactive"));
            return;
        }

        GameObject summary = CreateCard(
            contentRoot,
            "SeasonalSummary",
            new Vector2(0f, 168f),
            new Vector2(1110f, 130f),
            Cream);
        AddCardTitle(summary.transform, T(seasonalOverview.Event.TitleKey), Orange, 33f);
        TMP_Text summaryText = CreateText(
            summary.transform, 15f, FontStyles.Normal,
            TextAlignmentOptions.Center, TextDark);
        SetAnchored(summaryText.rectTransform, new Vector2(0f, -24f), new Vector2(1035f, 62f));
        summaryText.enableAutoSizing = true;
        summaryText.fontSizeMin = 11f;
        summaryText.fontSizeMax = 15f;
        summaryText.text = T(
            "seasonal.summary",
            seasonalOverview.TicketBalance,
            seasonalOverview.EventXp,
            FormatSeasonalEnd(seasonalOverview.Event.EndsAtEpochMs));

        BuildSeasonalChallenges();
        BuildSeasonalTrack();
        BuildSeasonalLimitedItems();
    }

    private void BuildSeasonalChallenges()
    {
        GameObject card = CreateCard(
            contentRoot, "SeasonalChallenges",
            new Vector2(-375f, -105f), new Vector2(350f, 390f), Cream);
        AddCardTitle(card.transform, T("seasonal.challenges.title"), Green, 160f);
        RectTransform list = BuildScrollList(
            card.transform, "ChallengeList", new Vector2(0f, -30f),
            new Vector2(310f, 285f), PanelBlue);
        if (seasonalOverview.Challenges.Count == 0)
        {
            AddInfoRow(list, T("seasonal.challenges.empty"));
            return;
        }
        foreach (AtlasBoardSeasonalChallenge challenge in seasonalOverview.Challenges)
        {
            GameObject row = CreateListRow(list, 132f, Cream);
            TMP_Text text = CreateText(
                row.transform, 12f, FontStyles.Normal,
                TextAlignmentOptions.TopLeft, TextDark);
            SetAnchored(text.rectTransform, new Vector2(-62f, 14f), new Vector2(150f, 92f));
            text.enableAutoSizing = true;
            text.fontSizeMin = 9f;
            text.fontSizeMax = 12f;
            text.text =
                $"<b>{T(challenge.TitleKey)}</b>\n" +
                $"{challenge.Progress}/{challenge.Target}\n" +
                T("seasonal.challenge.reward", challenge.RewardTickets, challenge.RewardXp);
            Button claim = CreateSmallButton(
                row.transform,
                challenge.Claimed ? T("seasonal.claimed") : T("seasonal.claim"),
                challenge.Claimed ? Green : Blue,
                new Vector2(88f, -24f), new Vector2(82f, 36f));
            claim.interactable = challenge.Claimable;
            string captured = challenge.ChallengeId;
            claim.onClick.AddListener(() => ClaimSeasonalChallengeAsync(captured));
        }
    }

    private void BuildSeasonalTrack()
    {
        GameObject card = CreateCard(
            contentRoot, "SeasonalTrack",
            new Vector2(0f, -105f), new Vector2(350f, 390f), Cream);
        AddCardTitle(card.transform, T("seasonal.track.title"), Blue, 160f);
        RectTransform list = BuildScrollList(
            card.transform, "TrackList", new Vector2(0f, -30f),
            new Vector2(310f, 285f), PanelBlue);
        foreach (AtlasBoardSeasonalTrackTier tier in seasonalOverview.TrackTiers)
        {
            GameObject row = CreateListRow(list, 118f, Cream);
            TMP_Text text = CreateText(
                row.transform, 12f, FontStyles.Normal,
                TextAlignmentOptions.TopLeft, TextDark);
            SetAnchored(text.rectTransform, new Vector2(-62f, 12f), new Vector2(150f, 84f));
            text.enableAutoSizing = true;
            text.fontSizeMin = 9f;
            text.fontSizeMax = 12f;
            text.text =
                $"<b>{T(tier.TitleKey)}</b>\n" +
                T("seasonal.track.requirement", tier.RequiredXp) + "\n" +
                T("seasonal.track.reward", tier.RewardAmount, SeasonalRewardLabel(tier.RewardType));
            Button claim = CreateSmallButton(
                row.transform,
                tier.Claimed ? T("seasonal.claimed") : T("seasonal.claim"),
                tier.Claimed ? Green : Blue,
                new Vector2(88f, -20f), new Vector2(82f, 36f));
            claim.interactable = tier.Claimable;
            string captured = tier.TierId;
            claim.onClick.AddListener(() => ClaimSeasonalTrackAsync(captured));
        }
    }

    private void BuildSeasonalLimitedItems()
    {
        GameObject card = CreateCard(
            contentRoot, "SeasonalLimitedItems",
            new Vector2(375f, -105f), new Vector2(350f, 390f), Cream);
        AddCardTitle(card.transform, T("seasonal.items.title"), Orange, 160f);
        RectTransform list = BuildScrollList(
            card.transform, "LimitedItemList", new Vector2(0f, -30f),
            new Vector2(310f, 285f), PanelBlue);
        foreach (AtlasBoardSeasonalLimitedItem item in seasonalOverview.LimitedItems)
        {
            GameObject row = CreateListRow(list, 118f, Cream);
            TMP_Text text = CreateText(
                row.transform, 12f, FontStyles.Normal,
                TextAlignmentOptions.TopLeft, TextDark);
            SetAnchored(text.rectTransform, new Vector2(-62f, 12f), new Vector2(150f, 84f));
            text.enableAutoSizing = true;
            text.fontSizeMin = 9f;
            text.fontSizeMax = 12f;
            text.text = $"<b>{Escape(LocalizeSeasonalItemName(item))}</b>\n" +
                T("seasonal.item.price", item.TicketPrice);
            Button buy = CreateSmallButton(
                row.transform,
                item.Owned ? T("store.items.owned") : T("seasonal.item.buy"),
                item.Owned ? Green : Orange,
                new Vector2(88f, -20f), new Vector2(82f, 36f));
            buy.interactable = !item.Owned;
            AtlasBoardSeasonalLimitedItem captured = item;
            buy.onClick.AddListener(() => BuySeasonalItemAsync(captured));
        }
    }

    private static string LocalizeSeasonalItemName(
        AtlasBoardSeasonalLimitedItem item)
    {
        if (item == null) return string.Empty;
        string key = "store.item." + item.ItemId;
        string localized = T(key);
        return string.Equals(localized, key, StringComparison.Ordinal)
            ? item.DisplayName
            : localized;
    }

    private string SeasonalRewardLabel(string rewardType)
    {
        switch (rewardType)
        {
            case "gold": return T("store.wallet.gold");
            case "atlas_coin": return T("store.wallet.coin");
            default: return T("seasonal.ticket");
        }
    }

    private static string FormatSeasonalEnd(long epochMs)
    {
        if (epochMs <= 0L) return "-";
        try
        {
            return DateTimeOffset
                .FromUnixTimeMilliseconds(epochMs)
                .ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture);
        }
        catch
        {
            return "-";
        }
    }

    private void RebuildRedeemPage()
    {
        CreateSectionTitle(contentRoot, T("store.redeem.title"));

        GameObject left = CreateCard(
            contentRoot,
            "RedeemEntry",
            new Vector2(-270f, 30f),
            new Vector2(515f, 415f),
            Cream);
        AddCardTitle(
            left.transform,
            T("store.redeem.enter_title"),
            Blue,
            165f);
        AddCardBody(
            left.transform,
            T("store.redeem.enter_body"),
            new Vector2(0f, 82f));

        redeemInput = CreateInput(
            left.transform,
            T("store.redeem.placeholder"),
            new Vector2(0f, -8f),
            new Vector2(430f, 58f));
        redeemInput.characterLimit = 32;
        redeemInput.contentType = TMP_InputField.ContentType.Standard;

        Button redeem = CreateWideButton(
            left.transform,
            T("store.redeem.button"),
            Blue,
            new Vector2(0f, -90f),
            new Vector2(300f, 58f));
        redeem.onClick.AddListener(RedeemAsync);

        TMP_Text safety = CreateText(
            left.transform,
            12f,
            FontStyles.Italic,
            TextAlignmentOptions.Center,
            TextDark);
        SetAnchored(
            safety.rectTransform,
            new Vector2(0f, -158f),
            new Vector2(430f, 52f));
        safety.text = T("store.redeem.audit_note");

        GameObject right = CreateCard(
            contentRoot,
            "RedeemHistory",
            new Vector2(270f, 30f),
            new Vector2(515f, 415f),
            Cream);
        AddCardTitle(
            right.transform,
            T("store.redeem.history_title"),
            Orange,
            165f);

        redeemHistoryContent = BuildScrollList(
            right.transform,
            "RedeemHistoryList",
            new Vector2(0f, -35f),
            new Vector2(455f, 300f),
            new Color32(241, 236, 225, 255));

        List<AtlasBoardMetaHistoryItem> redeemed =
            snapshot != null
                ? snapshot.History
                    .Where(item => item.Type == "REDEEM_CODE")
                    .ToList()
                : new List<AtlasBoardMetaHistoryItem>();

        if (redeemed.Count == 0)
        {
            AddInfoRow(redeemHistoryContent, T("store.redeem.history_empty"));
            return;
        }

        foreach (AtlasBoardMetaHistoryItem item in redeemed)
        {
            AddHistoryRow(redeemHistoryContent, item, true);
        }
    }

    private void RebuildHistoryPage()
    {
        CreateSectionTitle(contentRoot, T("store.history.title"));

        TMP_Text note = CreateText(
            contentRoot,
            13f,
            FontStyles.Normal,
            TextAlignmentOptions.Center,
            TextDark);
        SetAnchored(note.rectTransform, new Vector2(0f, 175f), new Vector2(980f, 48f));
        note.text = T("store.history.audit_note");

        historyContent = BuildScrollList(
            contentRoot,
            "UnifiedHistory",
            new Vector2(0f, -55f),
            new Vector2(1110f, 430f),
            new Color32(235, 240, 243, 245));

        if (snapshot == null || snapshot.History.Count == 0)
        {
            AddInfoRow(historyContent, T("store.history.empty"));
            return;
        }

        foreach (AtlasBoardMetaHistoryItem item in snapshot.History)
        {
            AddHistoryRow(historyContent, item, false);
        }
    }

    private void AddHistoryRow(
        Transform parent,
        AtlasBoardMetaHistoryItem item,
        bool compact)
    {
        float rowHeight = compact ? 122f : 104f;
        float textWidth = compact ? 390f : 930f;
        float headerY = compact ? 37f : 22f;
        float detailY = compact ? 3f : -13f;
        float idY = compact ? -39f : -40f;

        GameObject row = CreateListRow(parent, rowHeight, Cream);

        TMP_Text header = CreateText(
            row.transform,
            compact ? 13f : 15f,
            FontStyles.Bold,
            TextAlignmentOptions.TopLeft,
            TextDark);
        SetAnchored(
            header.rectTransform,
            new Vector2(-5f, headerY),
            new Vector2(textWidth, 30f));
        header.enableAutoSizing = compact;
        header.fontSizeMin = 10f;
        header.fontSizeMax = compact ? 13f : 15f;
        header.text =
            HistoryTypeLabel(item.Type) +
            "  |  " + FormatServerTime(item.CreatedAtUtc);

        TMP_Text detail = CreateText(
            row.transform,
            compact ? 12f : 13f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft,
            TextDark);
        SetAnchored(
            detail.rectTransform,
            new Vector2(-5f, detailY),
            new Vector2(textWidth, compact ? 44f : 28f));
        detail.enableAutoSizing = compact;
        detail.fontSizeMin = 9f;
        detail.fontSizeMax = compact ? 12f : 13f;
        detail.text = HistoryDetail(item);

        TMP_Text id = CreateText(
            row.transform,
            compact ? 8.5f : 10f,
            FontStyles.Normal,
            TextAlignmentOptions.BottomLeft,
            new Color32(90, 96, 105, 255));
        SetAnchored(
            id.rectTransform,
            new Vector2(-5f, idY),
            new Vector2(textWidth, compact ? 28f : 24f));
        id.enableAutoSizing = compact;
        id.fontSizeMin = 7f;
        id.fontSizeMax = compact ? 8.5f : 10f;
        id.text =
            T("store.history.transaction_id") + ": " +
            (item.TransactionId ?? string.Empty);
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject(
            "Canvas_AtlasStoreV2",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 560;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        storeRoot = CreatePanel(
            canvasObject.transform,
            "AtlasStore",
            Vector2.zero,
            new Vector2(1920f, 1080f),
            BackgroundBlue);
        Stretch(storeRoot.GetComponent<RectTransform>());
        storeRoot.AddComponent<AtlasBoardEscapeBlocker>();

        GameObject ribbon = CreatePanel(
            storeRoot.transform,
            "StoreRibbon",
            new Vector2(0f, 468f),
            new Vector2(780f, 96f),
            Orange);
        Outline outline = ribbon.AddComponent<Outline>();
        outline.effectColor = new Color32(169, 112, 0, 255);
        outline.effectDistance = new Vector2(4f, -4f);

        titleText = CreateText(
            ribbon.transform,
            36f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white);
        Stretch(titleText.rectTransform, 8f);

        GameObject profileCard = CreateCard(
            storeRoot.transform,
            "ProfileCard",
            new Vector2(-720f, 460f),
            new Vector2(455f, 120f),
            Cream);

        GameObject avatar = CreatePanel(
            profileCard.transform,
            "Avatar",
            new Vector2(-160f, 0f),
            new Vector2(88f, 88f),
            new Color32(68, 153, 203, 255));

        storeAvatarInitialText = CreateText(
            avatar.transform,
            40f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white);
        storeAvatarInitialText.gameObject.name = "AvatarInitial";
        Stretch(storeAvatarInitialText.rectTransform, 4f);

        storeProfileNameText = CreateText(
            profileCard.transform,
            22f,
            FontStyles.Bold,
            TextAlignmentOptions.Left,
            TextDark);
        storeProfileNameText.gameObject.name = "ProfileName";
        SetAnchored(
            storeProfileNameText.rectTransform,
            new Vector2(70f, -34f),
            new Vector2(235f, 30f));

        storeGoldText = CreateText(
            profileCard.transform,
            16f,
            FontStyles.Normal,
            TextAlignmentOptions.Left,
            TextDark);
        storeGoldText.gameObject.name = "ProfileCash";
        SetAnchored(
            storeGoldText.rectTransform,
            new Vector2(70f, 3f),
            new Vector2(235f, 26f));

        storeCoinText = CreateText(
            profileCard.transform,
            16f,
            FontStyles.Normal,
            TextAlignmentOptions.Left,
            TextDark);
        storeCoinText.gameObject.name = "ProfileGold";
        SetAnchored(
            storeCoinText.rectTransform,
            new Vector2(70f, 36f),
            new Vector2(235f, 26f));

        Button close = CreateSmallButton(
            storeRoot.transform,
            "X",
            Red,
            new Vector2(850f, 468f),
            new Vector2(82f, 70f));
        close.onClick.AddListener(CloseStore);

        GameObject nav = CreatePanel(
            storeRoot.transform,
            "Navigation",
            new Vector2(0f, 325f),
            new Vector2(1180f, 72f),
            new Color32(233, 233, 239, 245));

        BuildNavigationButtons(nav.transform);

        GameObject content = CreatePanel(
            storeRoot.transform,
            "ContentPanel",
            new Vector2(0f, -45f),
            new Vector2(1240f, 650f),
            new Color32(233, 233, 239, 245));
        contentRoot = content.GetComponent<RectTransform>();


        BuildConfirmation(storeRoot.transform);
        BuildResultPopup(storeRoot.transform);
        ApplyResolvedStoreFonts();
    }

    private void BuildNavigationButtons(Transform parent)
    {
        tabButtons.Clear();
        StorePage[] pages =
        {
            StorePage.Home,
            StorePage.Items,
            StorePage.Event,
            StorePage.Redeem,
            StorePage.History
        };
        string[] keys =
        {
            "store.tab.home",
            "store.tab.items",
            "seasonal.tab.event",
            "store.tab.redeem",
            "store.tab.history"
        };

        for (int i = 0; i < pages.Length; i++)
        {
            StorePage captured = pages[i];
            Button button = CreateSmallButton(
                parent,
                T(keys[i]),
                pages[i] == currentPage ? DarkBlue : Blue,
                new Vector2(-440f + i * 220f, 0f),
                new Vector2(205f, 50f));
            button.onClick.AddListener(() => ChangePage(captured));
            tabButtons.Add(button);
        }
    }

    private void RebuildNavigation()
    {
        GameObject nav = storeRoot != null
            ? FindDirectChild(storeRoot.transform, "Navigation")
            : null;
        if (nav == null)
        {
            return;
        }

        ClearChildren(nav.transform);
        BuildNavigationButtons(nav.transform);
    }

    private void BuildConfirmation(Transform parent)
    {
        confirmRoot = CreatePanel(
            parent,
            "PurchaseConfirmation",
            Vector2.zero,
            new Vector2(610f, 370f),
            Cream);
        confirmRoot.AddComponent<AtlasBoardEscapeBlocker>();
        Outline outline = confirmRoot.AddComponent<Outline>();
        outline.effectColor = Orange;
        outline.effectDistance = new Vector2(3f, -3f);

        confirmTitleText = CreateText(
            confirmRoot.transform,
            25f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            TextDark);
        SetAnchored(confirmTitleText.rectTransform, new Vector2(0f, 120f), new Vector2(520f, 48f));

        confirmBodyText = CreateText(
            confirmRoot.transform,
            17f,
            FontStyles.Normal,
            TextAlignmentOptions.Center,
            TextDark);
        SetAnchored(confirmBodyText.rectTransform, new Vector2(0f, 28f), new Vector2(520f, 140f));
        confirmBodyText.overflowMode = TextOverflowModes.Overflow;

        Button buy = CreateSmallButton(
            confirmRoot.transform,
            T("store.purchase.buy"),
            Green,
            new Vector2(-140f, -120f),
            new Vector2(220f, 58f));
        confirmBuyText = buy.GetComponentInChildren<TMP_Text>();
        buy.onClick.AddListener(ConfirmPurchaseAsync);

        Button cancel = CreateSmallButton(
            confirmRoot.transform,
            T("store.common.cancel"),
            Blue,
            new Vector2(140f, -120f),
            new Vector2(220f, 58f));
        confirmCancelText = cancel.GetComponentInChildren<TMP_Text>();
        cancel.onClick.AddListener(() => confirmRoot.SetActive(false));
    }

    private void BuildResultPopup(Transform parent)
    {
        resultRoot = CreatePanel(
            parent,
            "PurchaseResult",
            Vector2.zero,
            new Vector2(700f, 390f),
            Cream);
        resultRoot.AddComponent<AtlasBoardEscapeBlocker>();

        Outline outline = resultRoot.AddComponent<Outline>();
        outline.effectColor = Blue;
        outline.effectDistance = new Vector2(3f, -3f);

        resultTitleText = CreateText(
            resultRoot.transform,
            27f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            TextDark);
        SetAnchored(
            resultTitleText.rectTransform,
            new Vector2(0f, 125f),
            new Vector2(620f, 52f));

        resultBodyText = CreateText(
            resultRoot.transform,
            17f,
            FontStyles.Normal,
            TextAlignmentOptions.Center,
            TextDark);
        SetAnchored(
            resultBodyText.rectTransform,
            new Vector2(0f, 15f),
            new Vector2(610f, 160f));
        resultBodyText.overflowMode = TextOverflowModes.Overflow;

        Button ok = CreateSmallButton(
            resultRoot.transform,
            T("store.purchase.result_ok"),
            Blue,
            new Vector2(0f, -130f),
            new Vector2(250f, 58f));
        resultOkText = ok.GetComponentInChildren<TMP_Text>(true);
        ok.onClick.AddListener(() => resultRoot.SetActive(false));
    }

    private void ShowPurchaseResult(
        bool success,
        AtlasBoardStorefrontItem item,
        string currency,
        int price,
        int balanceAfter,
        string failureReason)
    {
        if (resultRoot == null || resultTitleText == null ||
            resultBodyText == null)
        {
            return;
        }

        string itemName = LocalizeItemName(item);
        string currencyLabel = CurrencyLabel(currency);

        if (success)
        {
            resultTitleText.text = T("store.purchase.success_title");
            string body = T(
                "store.purchase.success_body",
                itemName,
                price,
                currencyLabel);

            if (balanceAfter >= 0)
            {
                body += "\n\n" +
                    T(
                        "store.purchase.success_balance",
                        balanceAfter,
                        currencyLabel);
            }

            resultBodyText.text = body;
        }
        else
        {
            resultTitleText.text = T("store.purchase.failed_title");
            resultBodyText.text = T(
                "store.purchase.failed_body",
                itemName,
                failureReason) +
                "\n\n" + T("store.purchase.no_charge");
        }

        if (resultOkText != null)
        {
            resultOkText.text = T("store.purchase.result_ok");
        }

        resultRoot.SetActive(true);
        resultRoot.transform.SetAsLastSibling();
    }

    private void UpdateWalletText()
    {
        string displayName =
            snapshot != null &&
            !string.IsNullOrWhiteSpace(snapshot.DisplayName)
                ? snapshot.DisplayName.Trim()
                : T("menu.player");

        int gold = snapshot != null ? snapshot.Gold : 0;
        int coin = snapshot != null ? snapshot.AtlasCoin : 0;
        string initial = GetProfileInitial(displayName);

        if (storeProfileNameText != null)
        {
            storeProfileNameText.text = displayName;
        }

        if (storeGoldText != null)
        {
            storeGoldText.text =
                $"{T("store.wallet.gold")}: {gold:N0}";
        }

        if (storeCoinText != null)
        {
            storeCoinText.text =
                $"{T("store.wallet.coin")}: {coin:N0}";
        }

        if (storeAvatarInitialText != null)
        {
            storeAvatarInitialText.text = initial;
        }

        SyncSharedProfileCards(
            displayName,
            gold,
            coin,
            initial);
    }

    private static void SyncSharedProfileCards(
        string displayName,
        int gold,
        int coin,
        string initial)
    {
        AtlasBoardMainMenuController menuController =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardMainMenuController>();

        if (menuController != null)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;

            Type controllerType = menuController.GetType();
            controllerType.GetField("profileName", flags)
                ?.SetValue(menuController, displayName);
            controllerType.GetField("profileCash", flags)
                ?.SetValue(menuController, gold);
            controllerType.GetField("profileGold", flags)
                ?.SetValue(menuController, coin);
        }

        TMP_Text[] texts =
            Resources.FindObjectsOfTypeAll<TMP_Text>();

        foreach (TMP_Text text in texts)
        {
            if (text == null ||
                !text.gameObject.scene.IsValid())
            {
                continue;
            }

            switch (text.gameObject.name)
            {
                case "ProfileName":
                    text.text = displayName;
                    break;
                case "ProfileCash":
                    text.text =
                        $"{T("store.wallet.gold")}: {gold:N0}";
                    break;
                case "ProfileGold":
                    text.text =
                        $"{T("store.wallet.coin")}: {coin:N0}";
                    break;
                case "AvatarInitial":
                    text.text = initial;
                    break;
            }
        }
    }

    private static string GetProfileInitial(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "A";
        }

        string trimmed = displayName.Trim();
        return trimmed.Length > 0
            ? trimmed.Substring(0, 1).ToUpperInvariant()
            : "A";
    }

    private void ShowMessagePopup(
        string titleKey,
        string body)
    {
        if (resultRoot == null ||
            resultTitleText == null ||
            resultBodyText == null)
        {
            return;
        }

        resultTitleText.text = T(titleKey);
        resultBodyText.text = body ?? string.Empty;

        if (resultOkText != null)
        {
            resultOkText.text = T("store.purchase.result_ok");
        }

        resultRoot.SetActive(true);
        resultRoot.transform.SetAsLastSibling();
    }

    private string LocalizeItemName(AtlasBoardStorefrontItem item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        string key = "store.item." + item.ItemId;
        string localized = T(key);
        return string.Equals(localized, key, StringComparison.Ordinal)
            ? item.DisplayName ?? item.ItemId
            : localized;
    }

    private static string ItemTypeKey(string itemType)
    {
        switch (itemType)
        {
            case "pawn": return "store.category.pawns";
            case "dice_skin": return "store.category.dice";
            case "board_theme": return "store.category.boards";
            case "profile_frame": return "store.category.profile";
            case "emote": return "store.category.emotes";
            case "animation_pack": return "store.category.motion";
            default: return "store.category.items";
        }
    }

    private static Color ItemAccentColor(string itemType)
    {
        switch (itemType)
        {
            case "pawn": return Blue;
            case "dice_skin": return DarkBlue;
            case "board_theme": return Orange;
            case "profile_frame": return Green;
            case "emote": return new Color32(188, 94, 167, 255);
            case "animation_pack": return new Color32(66, 168, 150, 255);
            default: return Blue;
        }
    }

    private static string CurrencyLabel(string currency)
    {
        return currency == "atlas_coin"
            ? T("store.wallet.coin")
            : T("store.wallet.gold");
    }

    private static string HistoryTypeLabel(string type)
    {
        switch (type)
        {
            case "REDEEM_CODE": return T("store.history.type_redeem");
            case "DAILY_REWARD": return T("store.history.type_daily");
            default: return T("store.history.type_purchase");
        }
    }

    private string HistoryDetail(AtlasBoardMetaHistoryItem item)
    {
        if (item.Type == "PURCHASE")
        {
            string itemName = item.Detail;
            AtlasBoardStorefrontItem catalogItem =
                snapshot != null
                    ? snapshot.Items.FirstOrDefault(entry =>
                        string.Equals(
                            entry.ItemId,
                            item.Detail,
                            StringComparison.Ordinal))
                    : null;
            if (catalogItem != null)
            {
                itemName = LocalizeItemName(catalogItem);
            }

            return $"{itemName}  |  {item.Amount:N0} " +
                   $"{CurrencyLabel(item.CurrencyId)}  |  {item.Status}";
        }

        return item.Detail ?? string.Empty;
    }

    private static string FormatServerTime(string utc)
    {
        if (!DateTime.TryParse(
                utc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime parsed))
        {
            return T("store.history.time_pending");
        }

        DateTime local = parsed.ToLocalTime();
        TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(local);
        string sign = offset < TimeSpan.Zero ? "-" : "+";
        TimeSpan absolute = offset.Duration();
        string offsetText = absolute.Minutes == 0
            ? $"UTC{sign}{absolute.Hours}"
            : $"UTC{sign}{absolute.Hours}:{absolute.Minutes:00}";

        return local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) +
               $" ({offsetText})";
    }

    private static string DescribeOperationRewards(
        List<AtlasBoardMetaRewardLine> rewards)
    {
        if (rewards == null || rewards.Count == 0)
        {
            return T("store.redeem.reward_generic");
        }

        List<string> output = new List<string>();
        foreach (AtlasBoardMetaRewardLine reward in rewards)
        {
            if (!string.IsNullOrWhiteSpace(reward.ItemId))
            {
                output.Add(reward.ItemId);
            }
            else if (reward.Amount > 0)
            {
                output.Add(
                    $"{reward.Amount:N0} " +
                    (reward.CurrencyId == "atlas_coin"
                        ? T("store.wallet.coin")
                        : reward.CurrencyId == "gold"
                            ? T("store.wallet.gold")
                            : reward.CurrencyId));
            }
        }

        return output.Count > 0
            ? string.Join(", ", output)
            : T("store.redeem.reward_generic");
    }

    private static bool IsDeterministicBusinessFailure(string key)
    {
        return key == "commerce.error.insufficient_funds" ||
               key == "commerce.error.already_owned" ||
               key == "commerce.error.catalog_item_not_found" ||
               key == "commerce.error.catalog_item_invalid" ||
               key == "commerce.error.price_missing" ||
               key == "commerce.error.price_invalid";
    }

    private static string LocalizeError(string key)
    {
        switch (key)
        {
            case "commerce.error.insufficient_funds":
                return T("store.error.insufficient");
            case "commerce.error.already_owned":
                return T("store.error.already_owned");
            case "commerce.error.catalog_item_not_found":
            case "commerce.error.catalog_item_invalid":
            case "commerce.error.price_missing":
            case "commerce.error.price_invalid":
                return T("store.error.catalog");
            case "promo.error.invalid_code":
                return T("store.error.promo_invalid");
            case "promo.error.not_found":
                return T("store.error.promo_not_found");
            case "promo.error.disabled":
                return T("store.error.promo_disabled");
            case "promo.error.not_started":
                return T("store.error.promo_not_started");
            case "promo.error.expired":
                return T("store.error.promo_expired");
            case "promo.error.global_limit_reached":
                return T("store.error.promo_global_limit");
            case "promo.error.account_limit_reached":
                return T("store.error.promo_account_limit");
            case "meta.error.emulator_only":
                return T("store.error.emulator_only");
            case "meta.error.identity_unavailable":
                return T("store.error.identity");
            case "meta.error.invalid_purchase":
                return T("store.error.invalid_purchase");
            case "meta.error.dev_reset_failed":
                return T("store.error.dev_reset");
            case "meta.error.refresh_failed":
                return T("store.error.refresh");
            case "seasonal.error.event_inactive":
                return T("seasonal.error.event_inactive");
            case "seasonal.error.challenge_not_complete":
                return T("seasonal.error.challenge_not_complete");
            case "seasonal.error.challenge_not_found":
            case "seasonal.error.challenge_inactive":
                return T("seasonal.error.challenge_unavailable");
            case "seasonal.error.track_locked":
                return T("seasonal.error.track_locked");
            case "seasonal.error.item_unavailable":
            case "seasonal.error.item_not_found":
                return T("seasonal.error.item_unavailable");
            case "seasonal.error.insufficient_tickets":
                return T("seasonal.error.insufficient_tickets");
            case "seasonal.error.already_owned":
                return T("store.error.already_owned");
            case "seasonal.error.idempotency_conflict":
                return T("store.error.idempotency_conflict");
            default:
                return T("store.error.generic");
        }
    }

    private static string T(string key, params object[] args)
    {
        return AtlasBoardL.T(key, args);
    }

    private static GameObject CreatePanel(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        root.GetComponent<Image>().color = color;
        return root;
    }

    private static GameObject CreateCard(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject card = CreatePanel(parent, name, position, size, color);
        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = new Color32(210, 210, 215, 255);
        outline.effectDistance = new Vector2(2f, -2f);
        return card;
    }

    private static void CreateSectionTitle(Transform parent, string value)
    {
        TMP_Text title = CreateText(
            parent,
            28f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            TextDark);
        SetAnchored(title.rectTransform, new Vector2(0f, 260f), new Vector2(900f, 52f));
        title.text = value;
    }

    private static void AddCardTitle(
        Transform parent,
        string value,
        Color accent,
        float y = 82f)
    {
        float width = 500f;
        RectTransform parentRect = parent as RectTransform;
        if (parentRect != null)
        {
            float measuredWidth = parentRect.rect.width;
            if (measuredWidth > 0f)
            {
                width = Mathf.Max(180f, measuredWidth - 24f);
            }
        }

        GameObject bar = CreatePanel(
            parent,
            "CardHeader",
            new Vector2(0f, y),
            new Vector2(width, 58f),
            accent);
        TMP_Text text = CreateText(
            bar.transform,
            20f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white);
        Stretch(text.rectTransform, 8f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 20f;
        text.text = value;
    }

    private static void AddCardBody(
        Transform parent,
        string value,
        Vector2? position = null)
    {
        TMP_Text text = CreateText(
            parent,
            14f,
            FontStyles.Normal,
            TextAlignmentOptions.Center,
            TextDark);
        SetAnchored(
            text.rectTransform,
            position ?? new Vector2(0f, 18f),
            new Vector2(430f, 78f));
        text.overflowMode = TextOverflowModes.Overflow;
        text.text = value;
    }

    private static Button CreateWideButton(
        Transform parent,
        string label,
        Color color,
        Vector2 position,
        Vector2? size = null)
    {
        return CreateSmallButton(
            parent,
            label,
            color,
            position,
            size ?? new Vector2(320f, 52f));
    }

    private static Button CreateSmallButton(
        Transform parent,
        string label,
        Color color,
        Vector2 position,
        Vector2 size)
    {
        GameObject root = new GameObject(
            "Button_" + label,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        root.GetComponent<Image>().color = color;

        Button button = root.GetComponent<Button>();
        TMP_Text text = CreateText(
            root.transform,
            14f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white);
        Stretch(text.rectTransform, 5f);
        text.text = label;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = 16f;
        return button;
    }

    private static TMP_InputField CreateInput(
        Transform parent,
        string placeholder,
        Vector2 position,
        Vector2 size)
    {
        GameObject root = new GameObject(
            "RedeemCodeInput",
            typeof(RectTransform),
            typeof(Image),
            typeof(TMP_InputField));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        root.GetComponent<Image>().color = Color.white;

        TMP_Text text = CreateText(
            root.transform,
            18f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft,
            TextDark);
        Stretch(text.rectTransform, 14f);

        TMP_Text hint = CreateText(
            root.transform,
            16f,
            FontStyles.Italic,
            TextAlignmentOptions.MidlineLeft,
            new Color32(130, 135, 140, 255));
        Stretch(hint.rectTransform, 14f);
        hint.text = placeholder;

        TMP_InputField input = root.GetComponent<TMP_InputField>();
        input.textComponent = text;
        input.placeholder = hint;
        return input;
    }

    private static RectTransform BuildScrollList(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = position;
        rootRect.sizeDelta = size;
        root.GetComponent<Image>().color = color;

        GameObject viewport = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));
        viewport.transform.SetParent(root.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewport.GetComponent<Image>().color = Color.clear;

        GameObject content = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(8f, 0f);
        contentRect.offsetMax = new Vector2(-8f, 0f);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 7f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = root.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        return contentRect;
    }

    private static GameObject CreateListRow(
        Transform parent,
        float height,
        Color color)
    {
        GameObject row = new GameObject(
            "StoreRow",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = color;
        LayoutElement layout = row.GetComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        return row;
    }

    private static void AddInfoRow(Transform parent, string value)
    {
        GameObject row = CreateListRow(parent, 64f, Cream);
        TMP_Text text = CreateText(
            row.transform,
            14f,
            FontStyles.Italic,
            TextAlignmentOptions.Center,
            TextDark);
        Stretch(text.rectTransform, 8f);
        text.text = value;
    }

    private static TMP_Text CreateText(
        Transform parent,
        float size,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject root = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>();

        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;
        if (localization != null)
        {
            text.font = localization.ResolveFont(text.font);
        }

        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private void ApplyResolvedStoreFonts()
    {
        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;

        if (localization == null || canvas == null)
        {
            return;
        }

        TMP_Text[] texts =
            canvas.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text == null)
            {
                continue;
            }

            TMP_FontAsset resolved =
                localization.ResolveFont(text.font);

            if (resolved != null && text.font != resolved)
            {
                text.font = resolved;
            }
        }
    }

    private static void SetAnchored(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    private static GameObject FindDirectChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == name)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
