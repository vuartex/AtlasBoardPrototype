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
    private GameObject framePreviewRoot;
    private RectTransform framePreviewArtSlot;
    private TMP_Text framePreviewTitle;
    private TMP_Text framePreviewPrice;
    private Button framePreviewClose;
    private string framePreviewItemId = string.Empty;
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
    private int worldEventTab;
    private bool busy;
    private float nextOperationAllowedAt;
    private bool shopCardBound;
    private float nextBindAttemptAt;
    private bool liveWalletPolling;
    private string lastLiveWalletUid = string.Empty;
    private float nextLiveWalletPollAt;
    private float nextDailyButtonVisualAt;
    private Button dailyClaimButton;
    private TMP_Text dailyClaimButtonText;
    private readonly Dictionary<Button, bool> preOperationButtonStates =
        new Dictionary<Button, bool>();
    private bool preOperationRedeemInteractable;
    private bool hasPreOperationRedeemState;



    private void Awake()
    {
        bridge = GetComponent<AtlasBoardMetaEconomyBridge>();
        BuildUi();
        storeRoot.SetActive(false);
        confirmRoot.SetActive(false);
        resultRoot.SetActive(false);
        framePreviewRoot.SetActive(false);
        AtlasBoardLocalizationManager.LanguageChanged += HandleLanguageChanged;
    }


    private async void Start()
    {
        if (bridge == null)
        {
            return;
        }


        string requestedUid = AtlasBoardRtdbMetaEconomy.CurrentUid;
        AtlasBoardMetaEconomySnapshot initial = await bridge.RefreshAsync();
        // Do not promote a wallet loaded for a former UID during sign-out/in.
        snapshot = requestedUid == AtlasBoardRtdbMetaEconomy.CurrentUid &&
            initial != null && initial.Success ? initial : null;
        lastLiveWalletUid = requestedUid;
        nextLiveWalletPollAt = Time.unscaledTime + 15f;
        UpdateWalletText();
    }


    private void OnDestroy()
    {
        AtlasBoardLocalizationManager.LanguageChanged -= HandleLanguageChanged;
    }


    private void Update()
    {
        if (bridge != null && !bridge.IsLocalEmulatorMode && !busy &&
            !liveWalletPolling &&
            (lastLiveWalletUid != AtlasBoardRtdbMetaEconomy.CurrentUid ||
             Time.unscaledTime >= nextLiveWalletPollAt))
            PollLiveWalletAsync();


        if (!shopCardBound && Time.unscaledTime >= nextBindAttemptAt)
        {
            nextBindAttemptAt = Time.unscaledTime + 1f;
            TryBindExistingShopCard();
        }


        if (storeRoot != null && storeRoot.activeSelf)
        {
            CloseLegacyShopModal();
            if (currentPage == StorePage.Home && !busy &&
                Time.unscaledTime >= nextDailyButtonVisualAt)
            {
                nextDailyButtonVisualAt = Time.unscaledTime + 1f;
                UpdateDailyClaimButtonState();
            }
        }
    }


    private async void PollLiveWalletAsync()
    {
        liveWalletPolling = true;
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid;
        if (uid != lastLiveWalletUid)
        {
            // Never display the previous UID's wallet or daily entitlement.
            snapshot = null;
            dailyClaimButton = null;
            dailyClaimButtonText = null;
            UpdateWalletText();
            if (storeRoot != null && storeRoot.activeSelf && !busy)
                RebuildCurrentPage();
        }
        lastLiveWalletUid = uid;
        nextLiveWalletPollAt = Time.unscaledTime + 15f;
        try
        {
            AtlasBoardMetaEconomySnapshot fresh = await bridge.RefreshAsync();
            if (uid == AtlasBoardRtdbMetaEconomy.CurrentUid &&
                fresh != null && fresh.Success)
            {
                bool changed = HasMaterialStoreChange(snapshot, fresh);
                snapshot = fresh;
                UpdateWalletText();
                // Reading a wallet does not justify destroying and recreating
                // a disabled daily button (or a purchase/history list).
                if (changed && storeRoot != null && storeRoot.activeSelf && !busy)
                    RebuildCurrentPage();
            }
            // A transient read failure never turns a verified same-UID claim
            // back into an actionable button. A UID change was cleared above.
        }
        catch (Exception exception)
        {
            Debug.LogWarning("ATB_PHASE19C_LIVE_POLL " + exception.GetType().Name);
        }
        finally { liveWalletPolling = false; }
    }


    // Do not rebuild the entire store on every 15-second wallet poll.
    // Material changes still refresh owned inventory, currency, and history.
    private static bool HasMaterialStoreChange(
        AtlasBoardMetaEconomySnapshot before, AtlasBoardMetaEconomySnapshot after)
    {
        if (before == null || after == null) return before != after;
        if (before.Success != after.Success ||
            before.WalletVerified != after.WalletVerified ||
            before.IsGuest != after.IsGuest ||
            before.IsSteamAccount != after.IsSteamAccount ||
            before.IsDeveloperTester != after.IsDeveloperTester ||
            before.Gold != after.Gold || before.AtlasCoin != after.AtlasCoin ||
            before.DailyClaimedAtMs != after.DailyClaimedAtMs ||
            !string.Equals(before.EquippedFrameId, after.EquippedFrameId, StringComparison.Ordinal) ||
            !string.Equals(before.EquippedBoardId, after.EquippedBoardId, StringComparison.Ordinal) ||
            !string.Equals(before.EquippedDiceId, after.EquippedDiceId, StringComparison.Ordinal) ||
            !string.Equals(before.DisplayName, after.DisplayName, StringComparison.Ordinal) ||
            before.Items.Count != after.Items.Count ||
            before.History.Count != after.History.Count)
            return true;

        for (int i = 0; i < before.Items.Count; i++)
        {
            AtlasBoardStorefrontItem a = before.Items[i];
            AtlasBoardStorefrontItem b = after.Items[i];
            if (a == null || b == null) { if (a != b) return true; continue; }
            if (!string.Equals(a.ItemId, b.ItemId, StringComparison.Ordinal) ||
                !string.Equals(a.ItemType, b.ItemType, StringComparison.Ordinal) ||
                !string.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) ||
                a.Owned != b.Owned || a.PreviewOnly != b.PreviewOnly ||
                a.GoldPrice != b.GoldPrice || a.AtlasCoinPrice != b.AtlasCoinPrice ||
                a.SortOrder != b.SortOrder || a.CatalogVersion != b.CatalogVersion)
                return true;
        }
        for (int i = 0; i < before.History.Count; i++)
        {
            AtlasBoardMetaHistoryItem a = before.History[i];
            AtlasBoardMetaHistoryItem b = after.History[i];
            if (a == null || b == null) { if (a != b) return true; continue; }
            if (!string.Equals(a.TransactionId, b.TransactionId, StringComparison.Ordinal) ||
                !string.Equals(a.Type, b.Type, StringComparison.Ordinal) ||
                !string.Equals(a.Status, b.Status, StringComparison.Ordinal) ||
                a.Amount != b.Amount ||
                !string.Equals(a.ReferenceId, b.ReferenceId, StringComparison.Ordinal) ||
                !string.Equals(a.CurrencyId, b.CurrencyId, StringComparison.Ordinal) ||
                !string.Equals(a.Detail, b.Detail, StringComparison.Ordinal) ||
                !string.Equals(a.CreatedAtUtc, b.CreatedAtUtc, StringComparison.Ordinal))
                return true;
        }
        return false;
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
        framePreviewRoot.SetActive(false);
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
        framePreviewRoot.SetActive(false);
        storeRoot.SetActive(false);
        AtlasBoardMainMenuController menu =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardMainMenuController>();
        menu?.ShowMainMenu();
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


        RefreshFramePreviewLanguage();
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


        if (!value)
        {
            preOperationButtonStates.Clear();
            Button[] buttons = storeRoot.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button == null) continue;
                // Preserve the *original* disabled state, especially for an
                // already claimed daily reward or already owned product.
                preOperationButtonStates[button] = button.interactable;
                button.interactable = false;
            }
            hasPreOperationRedeemState = redeemInput != null;
            if (hasPreOperationRedeemState)
            {
                preOperationRedeemInteractable = redeemInput.interactable;
                redeemInput.interactable = false;
            }
            return;
        }

        foreach (KeyValuePair<Button, bool> state in preOperationButtonStates)
        {
            if (state.Key != null) state.Key.interactable = state.Value;
        }
        preOperationButtonStates.Clear();
        if (hasPreOperationRedeemState && redeemInput != null)
            redeemInput.interactable = preOperationRedeemInteractable;
        hasPreOperationRedeemState = false;
    }


    private async void RefreshAsync()
    {
        if (!CanStartOperation() || bridge == null)
        {
            return;
        }
        BeginOperation("store.response.loading");
        string requestedUid = AtlasBoardRtdbMetaEconomy.CurrentUid;
        AtlasBoardMetaEconomySnapshot result = await bridge.RefreshAsync();
        AtlasBoardSeasonalOverview seasonal = result != null && result.Success
            ? await bridge.GetSeasonalOverviewAsync()
            : null;
        EndOperation();


        if (requestedUid != AtlasBoardRtdbMetaEconomy.CurrentUid)
        {
            snapshot = null;
            dailyClaimButton = null;
            dailyClaimButtonText = null;
            UpdateWalletText();
            RebuildCurrentPage();
            return;
        }
        lastLiveWalletUid = requestedUid;
        if (result == null || !result.Success)
        {
            // A failed refresh must not erase a previously verified claim or
            // make an already claimed daily reward look actionable.
            UpdateWalletText();
            if (snapshot == null) RebuildCurrentPage();
            ShowMessagePopup("store.popup.error_title",
                LocalizeError(result != null ? result.ErrorKey : "meta.error.refresh_failed"));
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


    // RTDB authorization belongs to the Firebase UID, not the Steam overlay.
    // The administrator allowlists exact tester UIDs in /devWalletTesters.
    // A SteamAPI.Init failure must not block an ALREADY verified/allowlisted
    // UID, nor may a cached tester flag grant access after an account switch.
    private bool HasVerifiedApprovedTestWallet()
    {
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid;
        return bridge != null && !bridge.IsLocalEmulatorMode &&
            !string.IsNullOrWhiteSpace(uid) &&
            string.Equals(lastLiveWalletUid, uid, StringComparison.Ordinal) &&
            snapshot != null && snapshot.Success && snapshot.WalletVerified &&
            snapshot.IsDeveloperTester;
    }


    private bool RequireEmulatorForDevelopmentEconomy()
    {
        if (bridge != null && bridge.IsLocalEmulatorMode) return true;
        if (AtlasBoardRtdbMetaEconomy.IsRegistered) return true;
        if (HasVerifiedApprovedTestWallet()) return true;
        if (AtlasBoardRtdbMetaEconomy.IsSteamSession)
        {
            ShowMessagePopup("store.popup.info_title", StoreAccountText(
                "Steam is connected. Cloud rewards need verified Steam identity; an administrator-approved test UID can use this RTDB test wallet.",
                "Steam bağlı. Bulut ödülleri için doğrulanmış Steam kimliği gerekir; yönetici onaylı UID test cüzdanını kullanabilir.",
                "Steam conectado. Recompensas tras verificación.",
                "Steam connecté. Récompenses après vérification.",
                "Steam verbunden. Belohnungen nach Verifizierung.",
                "Steam 연결됨. 보상은 인증 후 이용할 수 있습니다.",
                "Steam подключён. Награды после подтверждения."));
            return false;
        }
        string lang = AtlasBoardLocalizationManager.Instance != null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";
        string message;
        switch (AtlasBoardLocalizationLanguages.Normalize(lang))
        {
            case "tr": message = "Bu işlem yalnızca kayıtlı hesaplarda kullanılabilir. Guest hesabıyla günlük ödül veya Gold alınamaz."; break;
            case "es": message = "Se necesita una cuenta registrada. Los invitados no pueden obtener recompensas."; break;
            case "fr": message = "Un compte enregistré est nécessaire. Les invités ne peuvent pas obtenir de récompenses."; break;
            case "de": message = "Ein registriertes Konto ist erforderlich. Gäste erhalten keine Belohnungen."; break;
            case "ko": message = "등록된 계정이 필요합니다. 게스트는 보상을 받을 수 없습니다."; break;
            case "ru": message = "Требуется зарегистрированная учётная запись. Гостям награды недоступны."; break;
            default: message = "Sign in with a registered account. Guests cannot claim rewards or spend Gold."; break;
        }
        ShowMessagePopup("store.popup.info_title", message);
        return false;
    }


    private async void SeedWalletAsync()
    {
        if (!RequireEmulatorForDevelopmentEconomy()) return;
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
        if (!RequireEmulatorForDevelopmentEconomy()) return;
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
        if (!RequireEmulatorForDevelopmentEconomy()) return;
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


        if (!result.AlreadyClaimed && snapshot != null &&
            snapshot.WalletVerified &&
            string.Equals(lastLiveWalletUid,
                AtlasBoardRtdbMetaEconomy.CurrentUid, StringComparison.Ordinal))
        {
            // Only after the server confirms the transaction. The final
            // refresh remains authoritative and loads the immutable receipt.
            snapshot.DailyClaimedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (result.Gold >= 0) snapshot.Gold = result.Gold;
            UpdateWalletText();
            UpdateDailyClaimButtonState();
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
        if (!RequireEmulatorForDevelopmentEconomy()) return;
        if (item == null || item.PreviewOnly) return;
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
        if (code.Length < 4 || code.Length > 48)
        {
            ShowMessagePopup(WorldText("promo_failed_title"),
                PromoFeedback("promo.error.invalid_code"), true);
            return;
        }


        string operationName = "redeem:" + code;
        string operationId = GetOrCreateOperationKey(operationName);


        BeginOperation("store.response.processing");
        AtlasBoardMetaEconomyOperationResult result =
            await bridge.RedeemCodeAsync(code, operationId);
        EndOperation();


        if (result == null || !result.Success)
        {
            string failureKey = result != null ? result.ErrorKey :
                "meta.error.service_unavailable";
            ShowMessagePopup(WorldText("promo_failed_title"),
                PromoFeedback(failureKey), true);
            return; // keep typed code for retry; no success receipt was verified.
        }

        operationKeys.Remove(operationName);
        redeemInput.text = string.Empty;
        string rewardSummary = DescribeOperationRewards(result.Rewards);
        ShowMessagePopup(WorldText("promo_success_title"),
            WorldText("promo_success_body") + "\n" + rewardSummary +
            "\n" + WorldText("transaction") + ": " +
            (result.TransactionId ?? ""), true);
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
        dailyClaimButton = null;
        dailyClaimButtonText = null;
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


    private static bool DailyAwardAlreadyClaimed(AtlasBoardMetaEconomySnapshot state)
    {
        if (state == null || !state.WalletVerified || state.DailyClaimedAtMs <= 0)
            return false;
        long since = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - state.DailyClaimedAtMs;
        return since < 86400000L;
    }

    private void UpdateDailyClaimButtonState()
    {
        if (dailyClaimButton == null || bridge == null || busy)
            return;
        bool steamAccount = AtlasBoardRtdbMetaEconomy.IsSteamSession;
        bool approvedTestWallet = HasVerifiedApprovedTestWallet();
        bool steamTester = steamAccount && approvedTestWallet;
        bool guestAccount = !bridge.IsLocalEmulatorMode &&
            !steamAccount && !AtlasBoardRtdbMetaEconomy.IsRegistered &&
            !approvedTestWallet;
        bool claimed = !guestAccount && DailyAwardAlreadyClaimed(snapshot);
        bool eligible = guestAccount || bridge.IsLocalEmulatorMode ||
            snapshot != null && snapshot.WalletVerified &&
            (!snapshot.IsGuest || approvedTestWallet) &&
            (!steamAccount || AtlasBoardRtdbMetaEconomy.IsRegistered || steamTester);
        string label = guestAccount
            ? StoreAccountText("SIGN IN / REGISTER", "GİRİŞ / KAYIT",
                "INICIAR SESIÓN", "SE CONNECTER", "ANMELDEN",
                "로그인 / 가입", "ВОЙТИ / РЕГИСТРАЦИЯ")
            : claimed
                ? StoreAccountText("DAILY REWARD CLAIMED", "GÜNLÜK ÖDÜL ALINDI",
                    "RECOMPENSA DIARIA RECLAMADA", "RÉCOMPENSE QUOTIDIENNE RÉCUPÉRÉE",
                    "TAGESBELOHNUNG ABGEHOLT", "일일 보상 수령 완료", "ЕЖЕДНЕВНАЯ НАГРАДА ПОЛУЧЕНА")
                : T("store.home.daily_claim");
        if (dailyClaimButtonText != null && dailyClaimButtonText.text != label)
            dailyClaimButtonText.text = label;
        bool canClick = eligible && !claimed;
        if (dailyClaimButton.interactable != canClick)
            dailyClaimButton.interactable = canClick;
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
        bool steamAccount = AtlasBoardRtdbMetaEconomy.IsSteamSession;
        bool approvedTestWallet = HasVerifiedApprovedTestWallet();
        bool steamTester = steamAccount && approvedTestWallet;
        bool guestAccount = bridge != null && !bridge.IsLocalEmulatorMode &&
            !steamAccount && !AtlasBoardRtdbMetaEconomy.IsRegistered &&
            !approvedTestWallet;
        AddCardBody(daily.transform,
            approvedTestWallet && !steamAccount && !AtlasBoardRtdbMetaEconomy.IsRegistered
                ? StoreAccountText(
                    "Approved test wallet: claim 50 Gold every 24 hours.",
                    "Onaylı test cüzdanı: 24 saatte bir 50 Gold al.",
                    "Cartera de prueba autorizada: 50 Gold cada 24 horas.",
                    "Portefeuille de test approuvé : 50 Gold toutes les 24 h.",
                    "Freigegebene Test-Wallet: 50 Gold alle 24 Stunden.",
                    "승인된 테스트 지갑: 24시간마다 50 Gold.",
                    "Одобренный тестовый кошелёк: 50 Gold раз в 24 часа.")
                : steamAccount && !AtlasBoardRtdbMetaEconomy.IsRegistered
                ? steamTester
                    ? StoreAccountText(
                        "Approved Steam test account: claim 50 Gold every 24 hours.",
                        "Steam test hesabın onaylı: 24 saatte bir 50 Gold al.",
                        "Steam aprobado: 50 Gold cada 24 horas.",
                        "Steam approuvé : 50 Gold par 24 heures.",
                        "Steam Testkonto genehmigt: 50 Gold pro Tag.",
                        "Steam 테스트 계정 승인됨: 24시간마다 50 Gold.",
                        "Тестовый Steam аккаунт одобрен: 50 Gold за 24 часа.")
                    : StoreAccountText(
                        "Steam connected. Cloud rewards require verified Steam identity.",
                        "Steam bağlı. Bulut ödülleri için Steam kimliğinin doğrulanması gerekiyor.",
                        "Steam conectado. Es necesaria la verificación.",
                        "Steam connecté. Vérification nécessaire.",
                        "Steam verbunden. Verifizierung erforderlich.",
                        "Steam 연결됨. 보상을 받으려면 인증이 필요합니다.",
                        "Steam подключён. Нужна проверка для наград.")
                : guestAccount
                ? StoreAccountText(
                    "Guest mode: sign in or register for 50 Gold every 24 hours.",
                    "Guest modu: 24 saatte bir 50 Gold için giriş yap veya kayıt ol.",
                    "Modo invitado: inicia sesión para recibir 50 Gold cada 24 horas.",
                    "Mode invité : connectez-vous pour obtenir 50 Gold toutes les 24 h.",
                    "Gastmodus: Für 50 Gold alle 24 Std. anmelden.",
                    "게스트 모드: 로그인하면 24시간마다 50 Gold를 받을 수 있습니다.",
                    "Гостевой режим: войдите для 50 Gold каждые 24 часа.")
                : StoreAccountText(
                    "Registered account: claim 50 Gold every 24 hours.",
                    "Kayıtlı hesap: 24 saatte bir 50 Gold kazan.",
                    "Cuenta registrada: 50 Gold cada 24 horas.",
                    "Compte enregistré : 50 Gold toutes les 24 heures.",
                    "Registriert: 50 Gold alle 24 Stunden.",
                    "등록 계정: 24시간마다 50 Gold를 받으세요.",
                    "Зарегистрированный аккаунт: 50 Gold каждые 24 часа."));
        Button claim = CreateWideButton(
            daily.transform,
            guestAccount
                ? StoreAccountText("SIGN IN / REGISTER", "GİRİŞ / KAYIT",
                    "INICIAR SESIÓN", "SE CONNECTER", "ANMELDEN",
                    "로그인 / 가입", "ВОЙТИ / РЕГИСТРАЦИЯ")
                : T("store.home.daily_claim"),
            Green,
            new Vector2(0f, -62f));
        if (guestAccount) claim.onClick.AddListener(OpenStoreSignIn);
        else claim.onClick.AddListener(ClaimDailyAsync);
        dailyClaimButton = claim;
        dailyClaimButtonText = claim.GetComponentInChildren<TMP_Text>(true);
        UpdateDailyClaimButtonState();


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
                ? (seasonalOverview.IsWorldJourney
                    ? WorldText("subtitle") : T(seasonalOverview.Event.SubtitleKey))
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
        AddCardBody(dev.transform,
            bridge != null && (bridge.IsLocalEmulatorMode ||
                snapshot != null && snapshot.IsDeveloperTester)
                ? T("store.home.dev_body")
                : StoreAccountText(
                    "Only a registered, administrator-approved developer test account can use this wallet.",
                    "Dev Wallet yalnızca yönetici tarafından onaylanan kayıtlı test hesaplarında açılır.",
                    "Solo para cuentas de prueba registradas y autorizadas.",
                    "Réservé aux comptes de test enregistrés et autorisés.",
                    "Nur für registrierte und freigegebene Testkonten.",
                    "관리자가 승인한 등록된 테스트 계정에서만 사용할 수 있습니다.",
                    "Только для зарегистрированных и разрешённых тестовых аккаунтов."));
        Button devWallet = CreateSmallButton(
            dev.transform,
            T("store.home.dev_wallet"),
            DarkBlue,
            new Vector2(-120f, -62f),
            new Vector2(220f, 52f));
        devWallet.onClick.AddListener(SeedWalletAsync);
        devWallet.interactable = bridge != null &&
            (bridge.IsLocalEmulatorMode || snapshot != null && snapshot.IsDeveloperTester);


        Button resetPurchases = CreateSmallButton(
            dev.transform,
            T("store.home.dev_reset"),
            Red,
            new Vector2(120f, -62f),
            new Vector2(220f, 52f));
        resetPurchases.onClick.AddListener(
            ResetDevelopmentPurchasesAsync);
        resetPurchases.interactable = devWallet.interactable;


    }


    private static string StoreAccountText(
        string en, string tr, string es, string fr,
        string de, string ko, string ru)
    {
        AtlasBoardLocalizationManager localization = AtlasBoardLocalizationManager.Instance;
        string language = AtlasBoardLocalizationLanguages.Normalize(
            localization != null ? localization.CurrentLanguageCode : "en");
        switch (language)
        {
            case "tr": return tr;
            case "es": return es;
            case "fr": return fr;
            case "de": return de;
            case "ko": return ko;
            case "ru": return ru;
            default: return en;
        }
    }


    private void OpenStoreSignIn()
    {
        if (!AtlasBoardAccountAccessUI.OpenLoginForStore())
        {
            ShowMessagePopup("store.popup.info_title", StoreAccountText(
                "Use Settings or Career to sign in or create an account.",
                "Giriş yapmak veya hesap oluşturmak için Ayarlar ya da Kariyer'i aç.",
                "Usa Configuración o Carrera para iniciar sesión.",
                "Utilisez Paramètres ou Carrière pour vous connecter.",
                "Melde dich über Einstellungen oder Karriere an.",
                "설정 또는 커리어에서 로그인하세요.",
                "Войдите через Настройки или Карьеру."));
            return;
        }
        confirmRoot.SetActive(false);
        framePreviewRoot.SetActive(false);
        storeRoot.SetActive(false);
    }


    private void RebuildItemsPage()
    {
        ClearChildren(contentRoot);
        CreateSectionTitle(contentRoot, T("store.items.title"));


        string[] categories =
        {
            "all", "pawn", "dice_skin", "board_theme",
            "profile_frame", "emote", "map"
        };


        string[] keys =
        {
            "store.category.all", "store.category.pawns",
            "store.category.dice", "store.category.boards",
            "store.category.profile", "store.category.emotes",
            "store.category.maps"
        };


        float startX = -486f;
        for (int i = 0; i < categories.Length; i++)
        {
            string captured = categories[i];
            Button button = CreateSmallButton(
                contentRoot,
                captured == "map" ? StoreAccountText("MAPS", "HARİTALAR", "MAPAS", "CARTES", "KARTEN", "지도", "КАРТЫ") : T(keys[i]),
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


        // Never hide unshipped drafts merely because an unrelated backend
        // catalog entry exists. The backend wins each identical ItemId.
        List<AtlasBoardStorefrontItem> combined =
            AtlasBoardShopContentDraft.MergeForDisplay(
                snapshot != null ? snapshot.Items : null);
        IEnumerable<AtlasBoardStorefrontItem> items = combined;
        // Rows already identify preview-only draft cosmetics individually.
        // A page-wide "Purchases disabled" banner is misleading whenever
        // verified profile frames can be purchased in the same list.


        if (activeCategory != "all")
        {
            items = items.Where(item =>
                string.Equals(
                    item.ItemType,
                    activeCategory,
                    StringComparison.OrdinalIgnoreCase));
        }


        // Exclusive World Journey unlocks are inventory items, never shop products.
        List<AtlasBoardStorefrontItem> list = items.Where(item =>
            item.ItemId != "map_world" &&
            item.ItemId != "board_atlas_horizon").ToList();
        if (list.Count == 0)
        {
            AddInfoRow(itemContent, activeCategory == "map"
                ? StoreAccountText("Germany · 30,000 Gold / Mexico · 25,000 Gold — Coming soon. Host-owned playable maps are in development.",
                    "Almanya · 30.000 Gold / Meksika · 25.000 Gold — Yakında. Host'a ait oynanabilir haritalar geliştiriliyor.",
                    "Alemania y México — Próximamente.", "Allemagne et Mexique — Bientôt.",
                    "Deutschland und Mexiko — Demnächst.", "독일 및 멕시코 — 준비 중.", "Германия и Мексика — Скоро.")
                : T("store.items.empty"));
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
        type.enableAutoSizing = true;
        type.fontSizeMin = 9f;
        type.fontSizeMax = 12f;
        type.textWrappingMode = TextWrappingModes.NoWrap;
        type.overflowMode = TextOverflowModes.Ellipsis;
        type.text = ItemTypeDescriptor(item.ItemType);


        // Profile art remains a dedicated inspect action; purchasing uses a
        // separate visible button on the same row.
        if (AtlasBoardShopProfileFrameArt.IsSupported(item.ItemId) &&
            string.Equals(item.ItemType, "profile_frame",
                StringComparison.Ordinal))
        {
            Button inspect = CreateSmallButton(
                row.transform, string.Empty, DarkBlue,
                new Vector2(100f, 0f), new Vector2(88f, 88f));
            inspect.gameObject.name = "PreviewFrame_" + item.ItemId;
            AtlasBoardShopProfileFrameArt.Create(
                inspect.transform, item.ItemId, 78f,
                GetProfileInitial(snapshot != null ? snapshot.DisplayName : null));
            inspect.onClick.AddListener(() => ShowFramePreview(item.ItemId));
        }
        else if (item.ItemType == "dice_skin" &&
                 AtlasBoardDiceSkinPreviewArt.IsSupported(item.ItemId))
        {
            Button inspect = CreateSmallButton(
                row.transform, string.Empty, DarkBlue,
                new Vector2(100f, 0f), new Vector2(88f, 88f));
            inspect.gameObject.name = "PreviewDice_" + item.ItemId;
            AtlasBoardDiceSkinPreviewArt.Create(inspect.transform, item.ItemId, 80f);
            inspect.onClick.AddListener(() => ShowFramePreview(item.ItemId));
        }
        else if (item.ItemType == "emote" && AtlasBoardEmotes.IsEmote(item.ItemId))
        {
            Button inspect = CreateSmallButton(row.transform, string.Empty, DarkBlue,
                new Vector2(100f, 0f), new Vector2(88f, 88f));
            inspect.gameObject.name = "PreviewEmote_" + item.ItemId;
            AtlasBoardEmotePreviewArt.Create(inspect.transform, item.ItemId, 73f);
            inspect.onClick.AddListener(() => ShowFramePreview(item.ItemId));
        }
        else if (item.ItemType == "pawn" &&
                 AtlasBoardPawnShopPreviewArt.IsSupported(item.ItemId))
        {
            Button inspect = CreateSmallButton(
                row.transform, StoreAccountText("VIEW", "GÖR", "VER", "VOIR",
                    "ANSEHEN", "보기", "СМОТРЕТЬ"), DarkBlue,
                new Vector2(100f, 0f), new Vector2(88f, 88f));
            inspect.gameObject.name = "PreviewPawn_" + item.ItemId;
            inspect.onClick.AddListener(() => ShowFramePreview(item.ItemId));
        }
        else if (item.ItemType == "map" &&
                 AtlasBoardRtdbMetaEconomy.IsPremiumMap(item.ItemId))
        {
            Button inspect = CreateSmallButton(row.transform,
                StoreAccountText("VIEW", "GÖR", "VER", "VOIR", "ANSEHEN", "보기", "СМОТРЕТЬ"),
                DarkBlue, new Vector2(100f, 0f), new Vector2(88f, 88f));
            inspect.gameObject.name = "PreviewMap_" + item.ItemId;
            inspect.onClick.AddListener(() => ShowFramePreview(item.ItemId));
        }
        else if (item.ItemType == "board_theme" &&
                 AtlasBoardShopBoardPreviewArt.IsSupported(item.ItemId))
        {
            // Board cosmetics are still PREVIEW ONLY. Use the existing
            // Store inspect popup; no second modal or fake Buy/Equip action.
            Button inspect = CreateSmallButton(
                row.transform, string.Empty, DarkBlue,
                new Vector2(100f, 0f), new Vector2(88f, 88f));
            inspect.gameObject.name = "PreviewBoard_" + item.ItemId;
            AtlasBoardShopBoardPreviewArt.Create(
                inspect.transform, item.ItemId, 78f);
            inspect.onClick.AddListener(() => ShowFramePreview(item.ItemId));
        }


        if (item.PreviewOnly)
        {
            bool isFrame = item.ItemType == "profile_frame" &&
                AtlasBoardShopProfileFrameArt.IsSupported(item.ItemId);
            bool isDice = item.ItemType == "dice_skin" &&
                AtlasBoardDiceSkinPreviewArt.IsSupported(item.ItemId);
            bool isPawn = item.ItemType == "pawn" &&
                AtlasBoardPawnShopPreviewArt.IsSupported(item.ItemId);
            bool isEmote = item.ItemType == "emote" && AtlasBoardEmotes.IsPaid(item.ItemId);
            if (isFrame || isDice || isPawn || isEmote ||
                item.ItemType == "board_theme" || item.ItemType == "map")
            {
                // Show a single Gold Buy beside inspect. Preview-only frames
                // never reach the checkout; Guest opens canonical login.
                Button frameBuy = CreateSmallButton(
                    row.transform, T("store.items.buy_gold", item.GoldPrice),
                    Green, new Vector2(360f, 0f), new Vector2(258f, 54f));
                frameBuy.gameObject.name = (isFrame ? "ProfileBuy_" : isDice ? "DiceBuy_" : isPawn ? "PawnBuy_" : item.ItemType == "board_theme" ? "BoardBuy_" : item.ItemType == "map" ? "MapBuy_" : "EmoteBuy_") + item.ItemId;
                frameBuy.onClick.AddListener(() =>
                {
                    if (!AtlasBoardRtdbMetaEconomy.IsRegistered &&
                        !HasVerifiedApprovedTestWallet())
                    {
                        if (!AtlasBoardRtdbMetaEconomy.IsSteamSession)
                            OpenStoreSignIn();
                        else ShowMessagePopup("store.popup.info_title", StoreAccountText(
                            "Steam connected: verified commerce or admin test approval required.",
                            "Steam bağlı: satın alma için doğrulama veya yönetici test onayı gerekli.",
                            "Steam conectado. Se necesita aprobación.",
                            "Steam connecté. Autorisation requise.",
                            "Steam verbunden. Freigabe erforderlich.",
                            "Steam 연결됨. 승인 후 구매 가능합니다.",
                            "Steam подключён. Требуется разрешение."));
                        return;
                    }
                    ShowMessagePopup("store.popup.info_title", StoreAccountText(
                        "Wallet is not verified yet. Please retry after sign-in.",
                        "Cüzdan henüz doğrulanmadı. Giriş yaptıktan sonra yeniden dene.",
                        "La cartera no está verificada. Inténtalo tras iniciar sesión.",
                        "Portefeuille non vérifié. Réessayez après connexion.",
                        "Wallet nicht bestätigt. Nach Anmeldung erneut versuchen.",
                        "지갑을 확인할 수 없습니다. 로그인 후 다시 시도하세요.",
                        "Кошелёк не проверен. Повторите после входа."));
                });
                return;
            }
            // Unimplemented 3D cosmetics remain visual-only draft items.
            TMP_Text previewLabel = CreateText(
                row.transform, 14f, FontStyles.Bold,
                TextAlignmentOptions.Center, DarkBlue);
            SetAnchored(previewLabel.rectTransform,
                new Vector2(355f, 0f), new Vector2(300f, 66f));
            string language = AtlasBoardLocalizationManager.Instance != null
                ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
                : "en";
            previewLabel.text = AtlasBoardShopContentDraft.PreviewLabel(language);
            return;
        }


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


        // One checkout button per row. Prefer the Gold price when available;
        // Atlas Coin is used only for catalog entries that have no Gold price.
        // An absent/zero price is never presented as a free purchase.
        bool payWithGold = item.GoldPrice > 0;
        bool payWithCoin = !payWithGold && item.AtlasCoinPrice > 0;
        if (payWithGold || payWithCoin)
        {
            string currencyId = payWithGold ? "gold" : "atlas_coin";
            string label = payWithGold
                ? T("store.items.buy_gold", item.GoldPrice)
                : T("store.items.buy_coin", item.AtlasCoinPrice);
            Button buy = CreateSmallButton(
                row.transform, label, payWithGold ? Green : Orange,
                new Vector2(360f, 0f), new Vector2(258f, 54f));
            buy.gameObject.name = "ItemBuy_" + item.ItemId;
            buy.onClick.AddListener(() => AskPurchase(item, currencyId));
        }
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
        if (seasonalOverview != null && seasonalOverview.Success &&
            seasonalOverview.IsWorldJourney &&
            seasonalOverview.Event != null &&
            (seasonalOverview.Event.Active || seasonalOverview.Upcoming))
        {
            RebuildWorldJourneyPage();
            return;
        }
        CreateSectionTitle(contentRoot, T("seasonal.page.title"));
        if (seasonalOverview == null || !seasonalOverview.Success ||
            seasonalOverview.Event == null || !seasonalOverview.Event.Active)
        {
            BuildEventComingSoonPanel(seasonalOverview != null && seasonalOverview.Success);
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



    private static string WorldText(string key)
    {
        AtlasBoardLocalizationManager loc = AtlasBoardLocalizationManager.Instance;
        return AtlasBoardShopContentDraft.WorldText(key,
            loc != null ? loc.CurrentLanguageCode : "en");
    }

    private string WorldRewardText(string rewardType, int amount, string itemId)
    {
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            if (string.Equals(itemId, "map_world", StringComparison.Ordinal))
                return WorldText("world_map_label");
            AtlasBoardLocalizationManager loc = AtlasBoardLocalizationManager.Instance;
            return AtlasBoardShopContentDraft.LocalizedName(itemId,
                loc != null ? loc.CurrentLanguageCode : "en");
        }
        return amount.ToString("N0", CultureInfo.InvariantCulture) + " " +
            WorldText(rewardType);
    }

    private async void ClaimWorldRewardAsync(string rewardId)
    {
        // Preview is UI-only. Never initiate reward requests against an absent
        // backend; existing claim permissions and server authority remain intact.
        if (seasonalOverview == null || seasonalOverview.WorldPreviewOnly) return;
        if (!CanStartOperation() || bridge == null) return;
        BeginOperation("store.response.processing");
        AtlasBoardMetaEconomyOperationResult result =
            await bridge.ClaimSeasonalChallengeAsync(rewardId);
        EndOperation();
        if (result == null || !result.Success)
        {
            ShowMessagePopup("store.popup.error_title", LocalizeError(
                result != null ? result.ErrorKey : "meta.error.service_unavailable"));
            return;
        }
        string rewardDescription = WorldRewardText("inventory_item", 1,
            rewardId == "completion_bonus" ? "board_atlas_horizon" :
            rewardId == "world_explorer" ? "map_world" : "");
        foreach (AtlasBoardWorldMission mission in seasonalOverview.WorldMissions)
            if (mission.MissionId == rewardId)
            {
                rewardDescription = WorldRewardText(mission.RewardType,
                    mission.RewardAmount, mission.RewardItemId);
                break;
            }
        ShowMessagePopup("seasonal.popup.track_title", WorldText("claimed") +
            "\n" + rewardDescription + "\n" + WorldText("transaction") +
            ": " + result.TransactionId);
        RefreshAfterOperationAsync();
    }

    private void RebuildWorldJourneyPage()
    {
        // Uses the existing Store canvas and layout primitives; no extra scene or UI manager.
        AtlasBoardSeasonalEvent e = seasonalOverview.Event;
        // Single continuous game-themed inset. Avoid the former blank strip
        // between the Store event heading and event content.
        CreatePanel(contentRoot, "WorldJourneyBackdrop",
            new Vector2(0f, -14f), new Vector2(1120f, 552f),
            new Color32(222, 238, 250, 255));
        GameObject banner = CreateCard(contentRoot, "WorldJourneyHero",
            new Vector2(0f, 192f), new Vector2(1100f, 132f),
            new Color32(14, 48, 88, 255));
        Image bannerImage = banner.GetComponent<Image>();
        Sprite heroSprite = Resources.Load<Sprite>("ATB_WorldJourneyHero");
        if (bannerImage != null && heroSprite != null)
        {
            bannerImage.sprite = heroSprite;
            bannerImage.type = Image.Type.Simple;
            bannerImage.color = Color.white;
        }
        // One continuous semi-transparent midnight layer across the full hero.
        // The old 714px left panel + 296px right panel left an 84px bright seam.
        // Keep the globe illustration visible THROUGH the overlay; no split panels.
        CreatePanel(banner.transform, "WorldJourneyUnifiedContrast",
            Vector2.zero, new Vector2(1088f, 124f),
            new Color32(7, 27, 60, 172));
        CreatePanel(banner.transform, "WorldJourneyAccent",
            new Vector2(-526f, 0f), new Vector2(7f, 98f), Orange);
        TMP_Text title = CreateText(banner.transform, 34f, FontStyles.Bold,
            TextAlignmentOptions.Center, Color.white);
        SetAnchored(title.rectTransform, new Vector2(-194f, 17f),
            new Vector2(675f, 53f));
        title.enableAutoSizing = true;
        title.fontSizeMin = 23f;
        title.fontSizeMax = 34f;
        title.overflowMode = TextOverflowModes.Ellipsis;
        title.text = WorldText("title");
        TMP_Text subtitle = CreateText(banner.transform, 15f, FontStyles.Normal,
            TextAlignmentOptions.Center, new Color32(218, 235, 255, 255));
        SetAnchored(subtitle.rectTransform, new Vector2(-194f, -30f),
            new Vector2(648f, 45f));
        subtitle.enableAutoSizing = true;
        subtitle.fontSizeMin = 11f;
        subtitle.fontSizeMax = 15f;
        subtitle.text = WorldText("subtitle");
        TMP_Text end = CreateText(banner.transform, 17f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Color32(255, 223, 130, 255));
        SetAnchored(end.rectTransform, new Vector2(394f, 0f),
            new Vector2(272f, 106f));
        end.enableAutoSizing = true;
        end.fontSizeMin = 12f;
        end.fontSizeMax = 17f;
        end.text = (seasonalOverview.Upcoming ? WorldText("starts") + ":\n" +
            FormatSeasonalEnd(e.StartsAtEpochMs) + "\n" : "") +
            WorldText("ends") + ":\n" + FormatSeasonalEnd(e.EndsAtEpochMs) +
            (seasonalOverview.Upcoming ? "\n" + WorldText("status_upcoming") : "");

        // Clearly separated navigation: no tab overlaps the hero or content cards.
        string[] tabs = {"overview", "missions", "rewards", "history"};
        for (int i = 0; i < tabs.Length; i++)
        {
            int next = i;
            Button tab = CreateEventPill(contentRoot, WorldText(tabs[i]),
                worldEventTab == i ? new Color32(20, 67, 117, 255) : Blue,
                new Vector2(-399f + i * 266f, 84f),
                new Vector2(238f, 43f), true);
            tab.onClick.AddListener(() => {
                worldEventTab = next;
                RebuildCurrentPage();
            });
        }
        if (worldEventTab == 0) BuildWorldOverview();
        else if (worldEventTab == 1) BuildWorldMissions();
        else if (worldEventTab == 2) BuildWorldRewards();
        else BuildWorldHistory();
    }

    private void BuildWorldOverview()
    {
        GameObject progress = CreateCard(contentRoot, "WorldProgress",
            new Vector2(-275f, -126f), new Vector2(530f, 306f), Cream);
        AddCardTitle(progress.transform, WorldText("progress"), DarkBlue, 117f);
        TMP_Text main = CreateText(progress.transform, 29f, FontStyles.Bold,
            TextAlignmentOptions.Center, TextDark);
        SetAnchored(main.rectTransform, new Vector2(0f, 16f),
            new Vector2(485f, 110f));
        main.text = WorldText("progress") + "  " +
            (seasonalOverview.WorldPreviewOnly ? "0" :
                seasonalOverview.CompletedMissions.ToString()) + " / 8\n" +
            WorldText("claimed_progress") + "  " +
            (seasonalOverview.WorldPreviewOnly ? "0" :
                seasonalOverview.ClaimedRewards.ToString()) + " / 8";
        if (seasonalOverview.WorldPreviewOnly)
        {
            TMP_Text notice = CreateText(progress.transform, 13f,
                FontStyles.Normal, TextAlignmentOptions.Center, DarkBlue);
            SetAnchored(notice.rectTransform, new Vector2(0f, -120f),
                new Vector2(470f, 36f));
            notice.text = WorldText("progress_unavailable");
        }
        GameObject barBack = CreatePanel(progress.transform, "ProgressTrack",
            new Vector2(0f, -80f), new Vector2(440f, 19f), PanelBlue);
        CreatePanel(barBack.transform, "ProgressFill",
            new Vector2(-220f + 220f * Mathf.Clamp01(seasonalOverview.CompletedMissions / 8f), 0f),
            new Vector2(440f * Mathf.Clamp01(seasonalOverview.CompletedMissions / 8f), 17f), Green);
        GameObject awards = CreateCard(contentRoot, "WorldExclusiveRewards",
            new Vector2(275f, -126f), new Vector2(530f, 306f), Cream);
        AddCardTitle(awards.transform, WorldText("world_reward"), Orange, 117f);
        TMP_Text preview = CreateText(awards.transform, 21f, FontStyles.Bold,
            TextAlignmentOptions.Center, DarkBlue);
        SetAnchored(preview.rectTransform, new Vector2(0f, 25f),
            new Vector2(480f, 115f));
        preview.text = WorldRewardText("inventory_item", 1, "map_world") +
            "\n" + WorldRewardText("inventory_item", 1, "board_atlas_horizon");
        TMP_Text footer = CreateText(awards.transform, 12f, FontStyles.Normal,
            TextAlignmentOptions.Center, TextDark);
        SetAnchored(footer.rectTransform, new Vector2(0f, -95f),
            new Vector2(465f, 92f));
        footer.text = WorldText("bonus_desc");
    }

    private void BuildWorldMissions()
    {
        RectTransform list = BuildScrollList(contentRoot, "WorldMissions",
            new Vector2(0f, -130f), new Vector2(1080f, 340f), PanelBlue);
        foreach (AtlasBoardWorldMission mission in seasonalOverview.WorldMissions)
        {
            GameObject row = CreateListRow(list, 100f, Cream);
            TMP_Text t = CreateText(row.transform, 16f, FontStyles.Bold,
                TextAlignmentOptions.Left, TextDark);
            SetAnchored(t.rectTransform, new Vector2(-230f, 12f),
                new Vector2(520f, 55f));
            t.text = WorldText("mission." + mission.MissionId) +
                "\n" + (seasonalOverview.WorldPreviewOnly ? "0" :
                mission.Progress.ToString()) + "/" + mission.Target;
            TMP_Text reward = CreateText(row.transform, 14f, FontStyles.Bold,
                TextAlignmentOptions.Center, Orange);
            SetAnchored(reward.rectTransform, new Vector2(287f, 0f),
                new Vector2(365f, 70f));
            reward.text = WorldRewardText(mission.RewardType,
                mission.RewardAmount, mission.RewardItemId) + "\n" +
                (mission.Claimed ? WorldText("claimed") :
                 mission.Claimable ? WorldText("ready") : WorldText("locked"));
            float ratio = mission.Target > 0 && !seasonalOverview.WorldPreviewOnly
                ? Mathf.Clamp01(mission.Progress / (float)mission.Target) : 0f;
            GameObject track = CreatePanel(row.transform, "MissionProgressTrack",
                new Vector2(-231f, -34f), new Vector2(488f, 8f),
                new Color32(211, 221, 228, 255));
            if (ratio > 0f) CreatePanel(track.transform, "MissionProgressFill",
                new Vector2(-244f + 244f * ratio, 0f),
                new Vector2(488f * ratio, 8f), Green);
        }
    }

    private void BuildWorldRewards()
    {
        RectTransform list = BuildScrollList(contentRoot, "WorldRewards",
            new Vector2(0f, -130f), new Vector2(1080f, 340f), PanelBlue);
        foreach (AtlasBoardWorldMission mission in seasonalOverview.WorldMissions)
        {
            GameObject row = CreateListRow(list, 95f, Cream);
            TMP_Text t = CreateText(row.transform, 16f, FontStyles.Bold,
                TextAlignmentOptions.Left, TextDark);
            SetAnchored(t.rectTransform, new Vector2(-235f, 3f),
                new Vector2(525f, 74f));
            t.text = WorldText("mission." + mission.MissionId) + "\n" +
                WorldRewardText(mission.RewardType,
                    mission.RewardAmount, mission.RewardItemId);
            Button claim = CreateEventPill(row.transform,
                mission.Claimed ? WorldText("claimed") :
                mission.Claimable ? WorldText("claim") : WorldText("locked"),
                mission.Claimed ? Green : mission.Claimable ? Orange :
                    new Color32(105, 126, 153, 255),
                new Vector2(355f, 0f), new Vector2(260f, 49f));
            claim.interactable = mission.Claimable &&
                !seasonalOverview.WorldPreviewOnly && seasonalOverview.Event.Active;
            string rewardId = mission.MissionId;
            claim.onClick.AddListener(() => ClaimWorldRewardAsync(rewardId));
        }
        GameObject bonus = CreateListRow(list, 146f, new Color32(226, 221, 245, 255));
        TMP_Text b = CreateText(bonus.transform, 16f, FontStyles.Bold,
            TextAlignmentOptions.Left, DarkBlue);
        SetAnchored(b.rectTransform, new Vector2(-225f, 27f),
            new Vector2(560f, 74f));
        b.text = WorldText("bonus") + "\n" +
            WorldRewardText("inventory_item", 1, "board_atlas_horizon");
        TMP_Text bonusDetails = CreateText(bonus.transform, 12f,
            FontStyles.Normal, TextAlignmentOptions.Left, TextDark);
        SetAnchored(bonusDetails.rectTransform, new Vector2(-225f, -41f),
            new Vector2(565f, 66f));
        bonusDetails.text = WorldText("bonus_desc");
        Button take = CreateEventPill(bonus.transform,
            seasonalOverview.WorldBonus.Claimed ? WorldText("claimed") :
            seasonalOverview.WorldBonus.Claimable ? WorldText("claim") : WorldText("locked"),
            seasonalOverview.WorldBonus.Claimed ? Green :
                seasonalOverview.WorldBonus.Claimable ? Orange :
                new Color32(105, 126, 153, 255),
            new Vector2(355f, 0f), new Vector2(260f, 49f));
        take.interactable = seasonalOverview.WorldBonus.Claimable &&
            !seasonalOverview.WorldPreviewOnly && seasonalOverview.Event.Active;
        take.onClick.AddListener(() => ClaimWorldRewardAsync("completion_bonus"));
    }

    private void BuildWorldHistory()
    {
        RectTransform list = BuildScrollList(contentRoot, "WorldRewardHistory",
            new Vector2(0f, -130f), new Vector2(1080f, 340f), PanelBlue);
        if (seasonalOverview.WorldHistory.Count == 0)
        {
            GameObject empty = CreateListRow(list, 290f,
                new Color32(240, 246, 252, 255));
            TMP_Text header = CreateText(empty.transform, 25f,
                FontStyles.Bold, TextAlignmentOptions.Center, DarkBlue);
            SetAnchored(header.rectTransform, new Vector2(0f, 49f),
                new Vector2(940f, 58f));
            header.text = WorldText(seasonalOverview.WorldPreviewOnly ?
                "history_unavailable" : "no_history");
            TMP_Text detail = CreateText(empty.transform, 17f,
                FontStyles.Normal, TextAlignmentOptions.Center, TextDark);
            SetAnchored(detail.rectTransform, new Vector2(0f, -25f),
                new Vector2(925f, 85f));
            detail.text = WorldText(seasonalOverview.WorldPreviewOnly ?
                "history_unavailable_body" : "no_history_body");
            CreatePanel(empty.transform, "HistoryAccent",
                new Vector2(0f, -93f), new Vector2(290f, 5f), Orange);
            return;
        }
        foreach (AtlasBoardWorldRewardHistory receipt in seasonalOverview.WorldHistory)
        {
            GameObject row = CreateListRow(list, 96f, Cream);
            TMP_Text text = CreateText(row.transform, 14f, FontStyles.Normal,
                TextAlignmentOptions.Left, TextDark);
            SetAnchored(text.rectTransform, Vector2.zero, new Vector2(1020f, 87f));
            string when = receipt.ClaimedAt > 0
                ? FormatSeasonalEnd(receipt.ClaimedAt) : "-";
            text.text = "<b>" + WorldText("title") + "</b>  ·  " + when +
                "\n" + WorldRewardText(receipt.RewardType,
                    receipt.RewardAmount, receipt.RewardItemId) +
                "  ·  " + receipt.Status +
                "\n" + WorldText("transaction") + ": " +
                receipt.TransactionId;
        }
    }

    // Calm, seven-language showcase rather than one isolated "service unavailable" row.
    // The existing LIVE event path below remains unchanged if an event is active.
    private void BuildEventComingSoonPanel(bool statusKnown)
    {
        GameObject card = CreateCard(contentRoot, "EventsComingSoon",
            new Vector2(0f, 24f), new Vector2(970f, 345f), Cream);
        GameObject ribbon = CreatePanel(card.transform, "EventHeader",
            new Vector2(0f, 119f), new Vector2(936f, 65f), Blue);
        TMP_Text header = CreateText(ribbon.transform, 27f, FontStyles.Bold,
            TextAlignmentOptions.Center, Color.white);
        Stretch(header.rectTransform, 12f);
        header.text = AtlasBoardReleaseUiText.EventsComingSoonTitle();


        TMP_Text symbol = CreateText(card.transform, 46f, FontStyles.Bold,
            TextAlignmentOptions.Center, Orange);
        SetAnchored(symbol.rectTransform, new Vector2(0f, 44f), new Vector2(200f, 65f));
        symbol.text = "★";


        TMP_Text body = CreateText(card.transform, 20f, FontStyles.Normal,
            TextAlignmentOptions.Center, TextDark);
        SetAnchored(body.rectTransform, new Vector2(0f, -35f), new Vector2(838f, 85f));
        body.enableAutoSizing = true;
        body.fontSizeMin = 16f;
        body.fontSizeMax = 20f;
        body.text = AtlasBoardReleaseUiText.EventsComingSoonBody();


        TMP_Text footer = CreateText(card.transform, 12f, FontStyles.Normal,
            TextAlignmentOptions.Center, TextDark);
        SetAnchored(footer.rectTransform, new Vector2(0f, -128f),
            new Vector2(850f, 32f));
        footer.text = statusKnown ? AtlasBoardReleaseUiText.EventsComingSoonFooter() :
            AtlasBoardReleaseUiText.EventsStatusPendingFooter();
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
        redeemInput.characterLimit = 48;
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
            AddInfoRow(historyContent, AtlasBoardReleaseUiText.NoTransactions());
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
            new Vector2(70f, 36f),
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
            new Vector2(70f, 3f),
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
        BuildFramePreview(storeRoot.transform);
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


    private void BuildFramePreview(Transform parent)
    {
        framePreviewRoot = CreatePanel(
            parent, "ProfileFramePreviewOverlay", Vector2.zero,
            new Vector2(1920f, 1080f),
            new Color(0.04f, 0.10f, 0.17f, 0.88f));
        // The overlay is modal: block clicks to underlying shop controls.
        GameObject card = CreateCard(framePreviewRoot.transform,
            "FramePreviewCard", Vector2.zero,
            new Vector2(640f, 630f), Cream);
        framePreviewTitle = CreateText(card.transform,
            26f, FontStyles.Bold, TextAlignmentOptions.Center, TextDark);
        SetAnchored(framePreviewTitle.rectTransform,
            new Vector2(0f, 255f), new Vector2(570f, 70f));
        framePreviewTitle.enableAutoSizing = true;
        framePreviewTitle.fontSizeMin = 17f;
        framePreviewTitle.fontSizeMax = 26f;


        GameObject previewWell = CreatePanel(card.transform,
            "FramePreviewWell", new Vector2(0f, 16f),
            new Vector2(290f, 290f), PanelBlue);
        framePreviewArtSlot =
            previewWell.GetComponent<RectTransform>();


        framePreviewPrice = CreateText(card.transform,
            16f, FontStyles.Bold, TextAlignmentOptions.Center, DarkBlue);
        SetAnchored(framePreviewPrice.rectTransform,
            new Vector2(0f, -184f), new Vector2(550f, 52f));
        framePreviewPrice.enableAutoSizing = true;
        framePreviewPrice.fontSizeMin = 11f;
        framePreviewPrice.fontSizeMax = 16f;


        framePreviewClose = CreateSmallButton(card.transform,
            T("store.common.cancel"), Blue,
            new Vector2(0f, -255f), new Vector2(255f, 55f));
        framePreviewClose.onClick.AddListener(() =>
            framePreviewRoot.SetActive(false));
    }


    private void ShowFramePreview(string itemId)
    {
        bool isProfile = AtlasBoardShopProfileFrameArt.IsSupported(itemId);
        bool isBoard = AtlasBoardShopBoardPreviewArt.IsSupported(itemId);
        bool isDice = AtlasBoardDiceSkinPreviewArt.IsSupported(itemId);
        bool isPawn = AtlasBoardPawnShopPreviewArt.IsSupported(itemId);
        bool isEmote = AtlasBoardEmotes.IsEmote(itemId);
        bool isMap = AtlasBoardRtdbMetaEconomy.IsPremiumMap(itemId);
        if ((!isProfile && !isBoard && !isDice && !isPawn && !isEmote && !isMap) ||
            framePreviewRoot == null || framePreviewArtSlot == null)
            return;
        framePreviewItemId = itemId;
        // The artwork is created only on explicit user interaction.
        for (int i = framePreviewArtSlot.childCount - 1; i >= 0; --i)
            Destroy(framePreviewArtSlot.GetChild(i).gameObject);
        if (isMap)
            AtlasBoardPremiumMapPreviewArt.Create(framePreviewArtSlot, itemId);
        else if (isProfile)
            AtlasBoardShopProfileFrameArt.Create(framePreviewArtSlot,
                itemId, 250f,
                GetProfileInitial(snapshot != null ? snapshot.DisplayName : null));
        else if (isBoard)
            AtlasBoardShopBoardPreviewArt.Create(framePreviewArtSlot,
                itemId, 250f);
        else if (isPawn)
            AtlasBoardPawnShopPreviewArt.Create(framePreviewArtSlot, itemId, 235f);
        else if (isEmote)
            AtlasBoardEmotePreviewArt.Create(framePreviewArtSlot, itemId, 225f);
        else
            AtlasBoardDiceSkinPreviewArt.Create(framePreviewArtSlot, itemId, 240f);
        RefreshFramePreviewLanguage();
        framePreviewRoot.SetActive(true);
        framePreviewRoot.transform.SetAsLastSibling();
    }


    private void RefreshFramePreviewLanguage()
    {
        if (framePreviewRoot == null ||
            string.IsNullOrWhiteSpace(framePreviewItemId)) return;
        string lang = AtlasBoardLocalizationManager.Instance != null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
            : "en";
        if (framePreviewTitle != null)
            framePreviewTitle.text =
                AtlasBoardShopContentDraft.LocalizedName(framePreviewItemId, lang);
        AtlasBoardStorefrontItem item =
            AtlasBoardShopContentDraft.CreatePreview()
                .FirstOrDefault(x => x.ItemId == framePreviewItemId);
        if (framePreviewPrice != null)
            framePreviewPrice.text = AtlasBoardEmotes.IsPaid(framePreviewItemId)
                ? AtlasBoardEmotes.Price(framePreviewItemId).ToString("N0") + " Gold"
                : item == null
                ? AtlasBoardShopContentDraft.PreviewLabel(lang)
                : item.GoldPrice.ToString("N0") + " Gold" +
                  (AtlasBoardRtdbMetaEconomy.Price(framePreviewItemId) > 0 &&
                   snapshot != null && snapshot.WalletVerified
                      ? "" : "  •  " + AtlasBoardShopContentDraft.PreviewLabel(lang));
        if (framePreviewClose != null)
        {
            TMP_Text label = framePreviewClose.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = T("store.common.cancel");
        }
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


        bool walletVerified = snapshot != null && snapshot.Success &&
            ((bridge != null && bridge.IsLocalEmulatorMode) || snapshot.WalletVerified);
        int gold = walletVerified ? snapshot.Gold : 0;
        int coin = walletVerified ? snapshot.AtlasCoin : 0;
        string initial = GetProfileInitial(displayName);


        if (storeProfileNameText != null)
        {
            storeProfileNameText.text = displayName;
        }


        if (storeGoldText != null)
        {
            storeGoldText.text = $"{T("store.wallet.gold")}: {gold:N0}";
        }


        if (storeCoinText != null)
        {
            storeCoinText.text = $"{T("store.wallet.coin")}: {coin:N0}";
        }


        if (storeAvatarInitialText != null)
        {
            storeAvatarInitialText.text = initial;
        }


        // Verified balances are shared immediately. During auth/network
        // readiness, every menu uses the neutral initial zero display.
        // Purchases still require snapshot.WalletVerified.
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
            menuController.ApplyVerifiedMetaWallet(gold, coin);
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
        string body,
        bool localizedTitle = false)
    {
        if (resultRoot == null ||
            resultTitleText == null ||
            resultBodyText == null)
        {
            return;
        }


        resultTitleText.text = localizedTitle ? (titleKey ?? string.Empty) : T(titleKey);
        resultBodyText.text = body ?? string.Empty;


        if (resultOkText != null)
        {
            resultOkText.text = T("store.purchase.result_ok");
        }


        resultRoot.SetActive(true);
        resultRoot.transform.SetAsLastSibling();
    }


    // Stable ATB codes are user-facing UI categories, not raw backend details.
    private static string PromoFeedback(string key)
    {
        switch (key ?? "")
        {
            case "promo.error.invalid_code": return "ATB-430 · " + WorldText("promo_invalid");
            case "promo.error.already_redeemed": return "ATB-434 · " + WorldText("promo_used");
            case "promo.error.item_already_owned": return "ATB-435 · " + WorldText("promo_owned");
            case "promo.error.not_found": return "ATB-433 · " + WorldText("promo_not_found");
            case "promo.error.disabled": return "ATB-432 · " + WorldText("promo_disabled");
            case "promo.error.not_started": return "ATB-437 · " + WorldText("promo_not_started");
            case "promo.error.expired": return "ATB-438 · " + WorldText("promo_expired");
            case "promo.error.qa_denied": return "ATB-441 · " + WorldText("promo_qa_denied");
            case "account.error.authentication_required": return "ATB-431 · " + WorldText("promo_sign_in");
            case "promo.error.account_not_ready": return "ATB-440 · " + WorldText("promo_account_not_ready");
            case "promo.error.limit_reached": return "ATB-439 · " + WorldText("promo_limit");
            case "promo.error.invalid_reward": return "ATB-436 · " + WorldText("promo_invalid_reward");
            default: return "ATB-424 · " + WorldText("promo_unavailable");
        }
    }

    private string LocalizeItemName(AtlasBoardStorefrontItem item)
    {
        if (item == null)
        {
            return string.Empty;
        }


        if (item.PreviewOnly)
        {
            string language = AtlasBoardLocalizationManager.Instance != null
                ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
                : "en";
            return AtlasBoardShopContentDraft.LocalizedName(
                item.ItemId, language);
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
            case "map": return "store.category.maps";
            default: return "store.category.items";
        }
    }


    // Customer-facing store metadata must never expose catalog revisions or
    // localization/backend keys. Keep the descriptor short enough for every
    // supported language and useful enough to identify the product family.
    private static string ItemTypeDescriptor(string itemType)
    {
        switch (itemType)
        {
            case "pawn":
                return StoreAccountText(
                    "PAWN • COSMETIC", "PİYON • KOZMETİK",
                    "PEÓN • COSMÉTICO", "PION • COSMÉTIQUE",
                    "SPIELFIGUR • KOSMETIK", "말 • 꾸미기",
                    "ФИШКА • КОСМЕТИКА");
            case "dice_skin":
                return StoreAccountText(
                    "DICE • COSMETIC", "ZAR • KOZMETİK",
                    "DADOS • COSMÉTICO", "DÉS • COSMÉTIQUE",
                    "WÜRFEL • KOSMETIK", "주사위 • 꾸미기",
                    "КУБИКИ • КОСМЕТИКА");
            case "board_theme":
                return StoreAccountText(
                    "BOARD • THEME", "TAHTA • TEMA",
                    "TABLERO • TEMA", "PLATEAU • THÈME",
                    "SPIELBRETT • THEMA", "보드 • 테마",
                    "ПОЛЕ • ТЕМА");
            case "profile_frame":
                return StoreAccountText(
                    "PROFILE • FRAME", "PROFİL • ÇERÇEVE",
                    "PERFIL • MARCO", "PROFIL • CADRE",
                    "PROFIL • RAHMEN", "프로필 • 프레임",
                    "ПРОФИЛЬ • РАМКА");
            case "emote":
                return StoreAccountText(
                    "EMOTE • EXPRESSION", "EMOTE • İFADE",
                    "EMOTE • EXPRESIÓN", "EMOTE • EXPRESSION",
                    "EMOTE • AUSDRUCK", "이모트 • 표현",
                    "ЭМОЦИЯ • ВЫРАЖЕНИЕ");
            case "map":
                return StoreAccountText(
                    "MAP • COUNTRY BOARD", "HARİTA • ÜLKE TAHTASI",
                    "MAPA • TABLERO DE PAÍS", "CARTE • PLATEAU PAYS",
                    "KARTE • LÄNDERSPIELBRETT", "지도 • 국가 보드",
                    "КАРТА • ПОЛЕ СТРАНЫ");
            default:
                return StoreAccountText(
                    "ITEM", "ÜRÜN", "ARTÍCULO", "ARTICLE",
                    "ARTIKEL", "아이템", "ПРЕДМЕТ");
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
            case "map": return new Color32(66, 168, 150, 255);
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
            case "DAILY_REWARD":
            case "DAILY": return T("store.history.type_daily");
            case "DEV_GRANT": return "Developer Wallet";
            case "RESET_PURCHASES": return "Test Purchase Reset";
            default: return T("store.history.type_purchase");
        }
    }


    private AtlasBoardStorefrontItem FindHistoryCatalogItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        AtlasBoardStorefrontItem serverItem = snapshot != null
            ? snapshot.Items.FirstOrDefault(entry =>
                entry != null &&
                string.Equals(entry.ItemId, itemId, StringComparison.Ordinal))
            : null;
        if (serverItem != null)
            return serverItem;

        return AtlasBoardShopContentDraft.CreatePreview().FirstOrDefault(entry =>
            entry != null &&
            string.Equals(entry.ItemId, itemId, StringComparison.Ordinal));
    }


    private static string InferHistoryItemType(string itemId)
    {
        if (AtlasBoardRtdbMetaEconomy.IsPremiumMap(itemId)) return "map";
        if (AtlasBoardEmotes.IsEmote(itemId)) return "emote";
        if (AtlasBoardRtdbMetaEconomy.IsPaidPawn(itemId)) return "pawn";
        if (AtlasBoardRtdbMetaEconomy.IsDice(itemId)) return "dice_skin";
        if (AtlasBoardRtdbMetaEconomy.IsBoard(itemId)) return "board_theme";
        if (!string.IsNullOrWhiteSpace(itemId) &&
            itemId.StartsWith("frame_", StringComparison.Ordinal))
            return "profile_frame";
        return string.Empty;
    }


    private string HistoryDetail(AtlasBoardMetaHistoryItem item)
    {
        if (item.Type == "PURCHASE")
        {
            string itemId = !string.IsNullOrWhiteSpace(item.ReferenceId)
                ? item.ReferenceId
                : item.Detail;
            AtlasBoardStorefrontItem catalogItem = FindHistoryCatalogItem(itemId);
            string itemName = catalogItem != null
                ? LocalizeItemName(catalogItem)
                : StoreAccountText(
                    "Purchased item", "Satın alınan ürün",
                    "Artículo comprado", "Article acheté",
                    "Gekaufter Artikel", "구매한 아이템",
                    "Купленный предмет");
            string itemType = catalogItem != null
                ? catalogItem.ItemType
                : InferHistoryItemType(itemId);
            int paid = Mathf.Abs(item.Amount);


            return ItemTypeDescriptor(itemType) +
                   "  •  " + itemName +
                   "  •  " + paid.ToString("N0") + " " +
                   CurrencyLabel(item.CurrencyId);
        }


        if (item.Type == "REDEEM_CODE")
        {
            // The RTDB receipt keeps the entered code in ReferenceId.
            // Show it in both Store > Redeem > Used Codes and Store > History;
            // transaction IDs remain in their existing separate audit row.
            string code = !string.IsNullOrWhiteSpace(item.ReferenceId)
                ? item.ReferenceId
                : (item.Detail ?? string.Empty);
            string codeLabel = StoreAccountText(
                "Code", "Kod", "Código", "Code", "Code", "코드", "Код");
            string reward = string.Empty;
            if (string.Equals(item.CurrencyId, "inventory_item",
                    StringComparison.OrdinalIgnoreCase))
            {
                // Cosmetic receipts carry the granted inventory ID in Detail.
                reward = item.Detail ?? string.Empty;
            }
            else if (item.Amount > 0)
            {
                string currency = string.Equals(item.CurrencyId, "xp",
                    StringComparison.OrdinalIgnoreCase)
                    ? "XP"
                    : CurrencyLabel(item.CurrencyId);
                reward = "+" + item.Amount.ToString("N0") + " " + currency;
            }
            else if (!string.IsNullOrWhiteSpace(item.Detail) &&
                     !string.Equals(item.Detail, code, StringComparison.Ordinal))
            {
                reward = item.Detail;
            }
            return codeLabel + ": " + code +
                (string.IsNullOrWhiteSpace(reward) ? string.Empty : "  •  " + reward);
        }

        if (item.Type == "DAILY" || item.Type == "DEV_GRANT")
            return "+" + item.Amount.ToString("N0") + " " + CurrencyLabel(item.CurrencyId);
        if (item.Type == "RESET_PURCHASES")
            return "Development test inventory reset";
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


    private static Sprite eventButtonSprite;

    private static Sprite GetEventButtonSprite()
    {
        if (eventButtonSprite != null) return eventButtonSprite;
        const int side = 48;
        const float radius = 13f;
        Texture2D texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[side * side];
        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            float dx = Mathf.Max(radius - x - .5f, 0f);
            dx = Mathf.Max(dx, x - (side - radius - .5f));
            float dy = Mathf.Max(radius - y - .5f, 0f);
            dy = Mathf.Max(dy, y - (side - radius - .5f));
            float a = Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy));
            pixels[y * side + x] = new Color(1f, 1f, 1f, a);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        eventButtonSprite = Sprite.Create(texture,
            new Rect(0f, 0f, side, side), new Vector2(.5f, .5f),
            100f, 0, SpriteMeshType.FullRect,
            new Vector4(15f, 15f, 15f, 15f));
        eventButtonSprite.name = "AtlasBoardEventRoundedButton";
        return eventButtonSprite;
    }

    private static Button CreateEventPill(Transform parent, string label,
        Color color, Vector2 position, Vector2 size, bool selected = false)
    {
        Button button = CreateSmallButton(parent, label, color, position, size);
        Image image = button.GetComponent<Image>();
        image.sprite = GetEventButtonSprite();
        image.type = Image.Type.Sliced;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock palette = button.colors;
        palette.normalColor = Color.white;
        palette.highlightedColor = new Color(.92f, .97f, 1f, 1f);
        palette.pressedColor = new Color(.80f, .89f, 1f, 1f);
        palette.selectedColor = Color.white;
        palette.disabledColor = Color.white; // keep lock styling readable
        palette.colorMultiplier = 1f;
        button.colors = palette;
        Outline shadow = button.gameObject.AddComponent<Outline>();
        shadow.effectColor = new Color(0.04f, 0.10f, 0.22f, selected ? .50f : .20f);
        shadow.effectDistance = new Vector2(1.8f, -2.4f);
        return button;
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
        text.overflowMode = TextOverflowModes.Truncate;
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