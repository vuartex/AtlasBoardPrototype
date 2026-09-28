using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class AtlasBoardMetaEconomyBridge : MonoBehaviour
{
    private const string ProjectId = "atlasboard-usa";
    private const string Region = "europe-west1";
    private const int FunctionsPort = 5001;
    private const int FirestorePort = 8080;

    private AtlasBoardLobbyRuntimeBridge lobbyBridge;
    private string lastErrorKey = string.Empty;
    private string lastTechnicalMessage = string.Empty;

    public bool IsLocalEmulatorMode
    {
        get
        {
            ResolveLobbyBridge();
            return lobbyBridge != null && lobbyBridge.UsingLocalEmulators;
        }
    }

    public string LocalAccountId
    {
        get
        {
            ResolveLobbyBridge();
            return lobbyBridge != null
                ? lobbyBridge.CurrentAccountId
                : string.Empty;
        }
    }

    private void Awake()
    {
        ResolveLobbyBridge();
    }

    public async Task<AtlasBoardMetaEconomySnapshot> RefreshAsync()
    {
        if (!await EnsureDevIdentityAsync())
        {
            return FailSnapshot();
        }

        string uid = lobbyBridge.CurrentAccountId;
        string token = lobbyBridge.AuthTokenForOnlineSubsystems;
        AtlasBoardMetaEconomySnapshot snapshot =
            new AtlasBoardMetaEconomySnapshot();

        try
        {
            snapshot.Gold = await ReadWalletBalanceAsync(uid, "gold", token);
            snapshot.AtlasCoin =
                await ReadWalletBalanceAsync(uid, "atlas_coin", token);

            await ReadPublicProfileAsync(uid, token, snapshot);

            HashSet<string> owned =
                await ReadOwnedItemIdsAsync(uid, token);

            List<AtlasBoardStorefrontItem> catalog =
                await ReadCatalogAsync(token);

            foreach (AtlasBoardStorefrontItem item in catalog)
            {
                item.Owned = owned.Contains(item.ItemId);
                snapshot.Items.Add(item);
            }

            List<AtlasBoardMetaHistoryItem> history =
                await ReadUnifiedHistoryAsync(uid, token);
            snapshot.History.AddRange(history);
            snapshot.Success = true;
            return snapshot;
        }
        catch (Exception exception)
        {
            snapshot.Success = false;
            snapshot.ErrorKey = "meta.error.refresh_failed";
            snapshot.TechnicalMessage = exception.Message;
            return snapshot;
        }
    }

    public async Task<AtlasBoardMetaEconomyOperationResult>
        SeedDevelopmentWalletAsync()
    {
        if (!await EnsureDevIdentityAsync())
        {
            return FailOperation();
        }

        string uid = lobbyBridge.CurrentAccountId;

        WalletMutationEnvelope gold =
            await CallFunctionAsync<WalletMutationRequest, WalletMutationEnvelope>(
                "walletTestMutation",
                new WalletMutationRequest
                {
                    currencyId = "gold",
                    delta = 5000,
                    reason = "phase9_dev_seed",
                    transactionId = "phase9-dev-wallet-gold-v2",
                    idempotencyKey = $"phase9-dev-wallet-gold-v2-{uid}"
                });

        if (gold == null || gold.result == null || !gold.result.ok)
        {
            return FailOperation();
        }

        WalletMutationEnvelope coin =
            await CallFunctionAsync<WalletMutationRequest, WalletMutationEnvelope>(
                "walletTestMutation",
                new WalletMutationRequest
                {
                    currencyId = "atlas_coin",
                    delta = 500,
                    reason = "phase9_dev_seed",
                    transactionId = "phase9-dev-wallet-coin-v2",
                    idempotencyKey = $"phase9-dev-wallet-coin-v2-{uid}"
                });

        if (coin == null || coin.result == null || !coin.result.ok)
        {
            return FailOperation();
        }

        return new AtlasBoardMetaEconomyOperationResult
        {
            Success = true,
            Type = "DEV_WALLET",
            TransactionId = "phase9-dev-wallet-v2",
            Gold = gold.result.balanceAfter,
            AtlasCoin = coin.result.balanceAfter
        };
    }

    public async Task<AtlasBoardMetaEconomyOperationResult>
        ClaimDailyDevelopmentRewardAsync()
    {
        if (!await EnsureDevIdentityAsync())
        {
            return FailOperation();
        }

        WalletMutationEnvelope envelope =
            await CallFunctionAsync<
                MetaDailyRewardRequest,
                WalletMutationEnvelope>(
                "metaDailyRewardClaim",
                new MetaDailyRewardRequest());

        if (envelope == null || envelope.result == null || !envelope.result.ok)
        {
            return FailOperation();
        }

        return new AtlasBoardMetaEconomyOperationResult
        {
            Success = true,
            Type = "DAILY_REWARD",
            TransactionId = envelope.result.transactionId ?? string.Empty,
            IdempotentReplay = envelope.result.idempotentReplay,
            AlreadyClaimed = envelope.result.idempotentReplay,
            Gold = envelope.result.balanceAfter,
            Detail = $"+{Mathf.Max(0, envelope.result.rewardGold)} Gold"
        };
    }

    public async Task<AtlasBoardMetaEconomyOperationResult> PurchaseAsync(
        string itemId,
        string paymentMethod,
        string idempotencyKey)
    {
        if (!await EnsureDevIdentityAsync())
        {
            return FailOperation();
        }

        if (string.IsNullOrWhiteSpace(itemId) ||
            string.IsNullOrWhiteSpace(idempotencyKey) ||
            (paymentMethod != "gold" && paymentMethod != "atlas_coin"))
        {
            return new AtlasBoardMetaEconomyOperationResult
            {
                Success = false,
                ErrorKey = "meta.error.invalid_purchase",
                TechnicalMessage = "Invalid item, payment method, or operation id."
            };
        }

        CommercePurchaseEnvelope envelope =
            await CallFunctionAsync<CommercePurchaseRequest, CommercePurchaseEnvelope>(
                "commerceTestPurchase",
                new CommercePurchaseRequest
                {
                    itemId = itemId,
                    paymentMethod = paymentMethod,
                    idempotencyKey = idempotencyKey
                });

        if (envelope == null || envelope.result == null || !envelope.result.ok)
        {
            return FailOperation();
        }

        return new AtlasBoardMetaEconomyOperationResult
        {
            Success = true,
            Type = "PURCHASE",
            ItemId = envelope.result.itemId,
            TransactionId = envelope.result.transactionId,
            ReferenceId = envelope.result.transactionId,
            IdempotentReplay = envelope.result.idempotentReplay,
            Gold = paymentMethod == "gold"
                ? envelope.result.balanceAfter
                : -1,
            AtlasCoin = paymentMethod == "atlas_coin"
                ? envelope.result.balanceAfter
                : -1
        };
    }

    public async Task<AtlasBoardMetaEconomyOperationResult> RedeemCodeAsync(
        string code,
        string idempotencyKey)
    {
        if (!await EnsureDevIdentityAsync())
        {
            return FailOperation();
        }

        string normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (normalized.Length < 4 || normalized.Length > 32 ||
            string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return new AtlasBoardMetaEconomyOperationResult
            {
                Success = false,
                ErrorKey = "promo.error.invalid_code",
                TechnicalMessage = "Redeem code format is invalid."
            };
        }

        PromoRedeemEnvelope envelope =
            await CallFunctionAsync<PromoRedeemRequest, PromoRedeemEnvelope>(
                "promoTestRedeem",
                new PromoRedeemRequest
                {
                    code = normalized,
                    idempotencyKey = idempotencyKey
                });

        if (envelope == null || envelope.result == null || !envelope.result.ok)
        {
            return FailOperation();
        }

        AtlasBoardMetaEconomyOperationResult result =
            new AtlasBoardMetaEconomyOperationResult
            {
                Success = true,
                Type = "REDEEM_CODE",
                TransactionId = envelope.result.redemptionEventId,
                ReferenceId = envelope.result.redemptionId,
                IdempotentReplay = envelope.result.idempotentReplay,
                Detail = normalized
            };

        if (envelope.result.rewards != null)
        {
            foreach (PromoRewardWire reward in envelope.result.rewards)
            {
                if (reward == null)
                {
                    continue;
                }

                result.Rewards.Add(
                    new AtlasBoardMetaRewardLine
                    {
                        Type = reward.type ?? string.Empty,
                        CurrencyId = reward.currencyId ?? string.Empty,
                        ItemId = reward.itemId ?? string.Empty,
                        Amount = reward.amount
                    });
            }
        }

        return result;
    }

    public async Task<AtlasBoardSeasonalOverview>
        GetSeasonalOverviewAsync()
    {
        if (!await EnsureDevIdentityAsync())
        {
            return new AtlasBoardSeasonalOverview
            {
                Success = false,
                ErrorKey = lastErrorKey,
                TechnicalMessage = lastTechnicalMessage
            };
        }

        SeasonalOverviewEnvelope envelope =
            await CallFunctionAsync<
                SeasonalEmptyRequest,
                SeasonalOverviewEnvelope>(
                "metaSeasonalGetOverview",
                new SeasonalEmptyRequest());

        if (envelope == null || envelope.result == null || !envelope.result.ok)
        {
            return new AtlasBoardSeasonalOverview
            {
                Success = false,
                ErrorKey = lastErrorKey,
                TechnicalMessage = lastTechnicalMessage
            };
        }

        SeasonalOverviewWire wire = envelope.result;
        AtlasBoardSeasonalOverview result = new AtlasBoardSeasonalOverview
        {
            Success = true,
            TicketBalance = wire.ticketBalance,
            EventXp = wire.@eventXp,
            Event = new AtlasBoardSeasonalEvent
            {
                Active = wire.@event != null && wire.@event.active,
                EventId = wire.@event != null ? wire.@event.eventId : string.Empty,
                TitleKey = wire.@event != null ? wire.@event.titleKey : string.Empty,
                SubtitleKey = wire.@event != null ? wire.@event.subtitleKey : string.Empty,
                StartsAtEpochMs = wire.@event != null ? wire.@event.startsAtEpochMs : 0L,
                EndsAtEpochMs = wire.@event != null ? wire.@event.endsAtEpochMs : 0L,
                TicketCurrencyId = wire.@event != null ? wire.@event.ticketCurrencyId : string.Empty
            }
        };

        if (wire.challenges != null)
        {
            foreach (SeasonalChallengeWire item in wire.challenges)
            {
                if (item == null) continue;
                result.Challenges.Add(new AtlasBoardSeasonalChallenge
                {
                    ChallengeId = item.challengeId ?? string.Empty,
                    TitleKey = item.titleKey ?? string.Empty,
                    DescriptionKey = item.descriptionKey ?? string.Empty,
                    Cadence = item.cadence ?? string.Empty,
                    Target = item.target,
                    Progress = item.progress,
                    RewardTickets = item.rewardTickets,
                    RewardXp = item.rewardXp,
                    Claimed = item.claimed,
                    Claimable = item.claimable,
                    PeriodKey = item.periodKey ?? string.Empty,
                    SortOrder = item.sortOrder
                });
            }
        }

        if (wire.trackTiers != null)
        {
            foreach (SeasonalTrackTierWire item in wire.trackTiers)
            {
                if (item == null) continue;
                result.TrackTiers.Add(new AtlasBoardSeasonalTrackTier
                {
                    TierId = item.tierId ?? string.Empty,
                    TitleKey = item.titleKey ?? string.Empty,
                    RequiredXp = item.requiredXp,
                    RewardType = item.rewardType ?? string.Empty,
                    RewardAmount = item.rewardAmount,
                    Claimed = item.claimed,
                    Claimable = item.claimable,
                    SortOrder = item.sortOrder
                });
            }
        }

        if (wire.limitedItems != null)
        {
            foreach (SeasonalLimitedItemWire item in wire.limitedItems)
            {
                if (item == null) continue;
                result.LimitedItems.Add(new AtlasBoardSeasonalLimitedItem
                {
                    ItemId = item.itemId ?? string.Empty,
                    ItemType = item.itemType ?? string.Empty,
                    DisplayName = item.displayName ?? string.Empty,
                    TicketPrice = item.ticketPrice,
                    LimitedUntilEpochMs = item.limitedUntilEpochMs,
                    Owned = item.owned,
                    SortOrder = item.sortOrder
                });
            }
        }

        return result;
    }

    public async Task<AtlasBoardMetaEconomyOperationResult>
        ClaimSeasonalChallengeAsync(string challengeId)
    {
        if (!await EnsureDevIdentityAsync())
        {
            return FailOperation();
        }

        SeasonalClaimEnvelope envelope =
            await CallFunctionAsync<
                SeasonalChallengeRequest,
                SeasonalClaimEnvelope>(
                "metaSeasonalClaimChallenge",
                new SeasonalChallengeRequest
                {
                    challengeId = challengeId ?? string.Empty
                });

        if (envelope == null || envelope.result == null || !envelope.result.ok)
        {
            return FailOperation();
        }

        return new AtlasBoardMetaEconomyOperationResult
        {
            Success = true,
            Type = "SEASONAL_CHALLENGE",
            TransactionId = envelope.result.transactionId ?? string.Empty,
            ReferenceId = envelope.result.referenceId ?? string.Empty,
            IdempotentReplay = envelope.result.idempotentReplay,
            Detail = envelope.result.ticketBalance.ToString(CultureInfo.InvariantCulture)
        };
    }

    public async Task<AtlasBoardMetaEconomyOperationResult>
        ClaimSeasonalTrackTierAsync(string tierId)
    {
        if (!await EnsureDevIdentityAsync())
        {
            return FailOperation();
        }

        SeasonalClaimEnvelope envelope =
            await CallFunctionAsync<
                SeasonalTrackRequest,
                SeasonalClaimEnvelope>(
                "metaSeasonalClaimTrackTier",
                new SeasonalTrackRequest
                {
                    tierId = tierId ?? string.Empty
                });

        if (envelope == null || envelope.result == null || !envelope.result.ok)
        {
            return FailOperation();
        }

        return new AtlasBoardMetaEconomyOperationResult
        {
            Success = true,
            Type = "SEASONAL_TRACK",
            TransactionId = envelope.result.transactionId ?? string.Empty,
            ReferenceId = envelope.result.referenceId ?? string.Empty,
            IdempotentReplay = envelope.result.idempotentReplay,
            Detail = $"{envelope.result.rewardType}:{envelope.result.rewardAmount}"
        };
    }

    public async Task<AtlasBoardMetaEconomyOperationResult>
        PurchaseSeasonalLimitedItemAsync(
            string itemId,
            string idempotencyKey)
    {
        if (!await EnsureDevIdentityAsync())
        {
            return FailOperation();
        }

        SeasonalPurchaseEnvelope envelope =
            await CallFunctionAsync<
                SeasonalPurchaseRequest,
                SeasonalPurchaseEnvelope>(
                "metaSeasonalPurchaseLimitedItem",
                new SeasonalPurchaseRequest
                {
                    itemId = itemId ?? string.Empty,
                    idempotencyKey = idempotencyKey ?? string.Empty
                });

        if (envelope == null || envelope.result == null || !envelope.result.ok)
        {
            return FailOperation();
        }

        return new AtlasBoardMetaEconomyOperationResult
        {
            Success = true,
            Type = "SEASONAL_PURCHASE",
            ItemId = envelope.result.itemId ?? string.Empty,
            TransactionId = envelope.result.transactionId ?? string.Empty,
            ReferenceId = envelope.result.eventId ?? string.Empty,
            IdempotentReplay = envelope.result.idempotentReplay,
            Detail = envelope.result.ticketBalance.ToString(CultureInfo.InvariantCulture)
        };
    }

    public async Task<AtlasBoardMetaEconomyOperationResult>
        ResetDevelopmentPurchasesAsync()
    {
        if (!await EnsureDevIdentityAsync())
        {
            return FailOperation();
        }

        MetaDevResetEnvelope envelope =
            await CallFunctionAsync<
                MetaDevResetRequest,
                MetaDevResetEnvelope>(
                "metaDevResetPurchasedEntitlements",
                new MetaDevResetRequest
                {
                    operationId =
                        $"phase9-dev-reset-{Guid.NewGuid():N}"
                });

        if (envelope == null ||
            envelope.result == null ||
            !envelope.result.ok)
        {
            return FailOperation();
        }

        return new AtlasBoardMetaEconomyOperationResult
        {
            Success = true,
            Type = "DEV_RESET",
            TransactionId = envelope.result.auditId,
            Detail = envelope.result.resetCount.ToString(
                CultureInfo.InvariantCulture)
        };
    }

    private async Task<bool> EnsureDevIdentityAsync()
    {
        ResolveLobbyBridge();

        if (lobbyBridge == null)
        {
            lastErrorKey = "meta.error.identity_unavailable";
            lastTechnicalMessage = "AtlasBoardLobbyRuntimeBridge was not found.";
            return false;
        }

        if (!lobbyBridge.UsingLocalEmulators)
        {
            lastErrorKey = "meta.error.emulator_only";
            lastTechnicalMessage =
                "Phase 9 storefront v2 is intentionally local-emulator only.";
            return false;
        }

        bool ready = await lobbyBridge.EnsureOnlineIdentityAsync();
        if (!ready ||
            string.IsNullOrWhiteSpace(lobbyBridge.CurrentAccountId) ||
            string.IsNullOrWhiteSpace(lobbyBridge.AuthTokenForOnlineSubsystems))
        {
            lastErrorKey = "meta.error.identity_unavailable";
            lastTechnicalMessage = "Online development identity is unavailable.";
            return false;
        }

        return true;
    }

    private async Task ReadPublicProfileAsync(
        string uid,
        string token,
        AtlasBoardMetaEconomySnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        FirestoreDocument profile =
            await GetFirestoreDocumentAsync(
                $"public_profiles/{Uri.EscapeDataString(uid)}",
                token,
                true);

        if (profile == null || profile.fields == null)
        {
            snapshot.DisplayName = string.Empty;
            snapshot.AvatarId = string.Empty;
            return;
        }

        snapshot.DisplayName =
            ReadString(profile.fields.displayName);
        snapshot.AvatarId =
            ReadString(profile.fields.avatarId);
    }

    private async Task<int> ReadWalletBalanceAsync(
        string uid,
        string currencyId,
        string token)
    {
        string path =
            $"wallets/{Uri.EscapeDataString(uid)}/balances/" +
            Uri.EscapeDataString(currencyId);

        FirestoreDocument document =
            await GetFirestoreDocumentAsync(path, token, true);

        if (document == null || document.fields == null ||
            document.fields.amount == null)
        {
            return 0;
        }

        return ReadInteger(document.fields.amount);
    }

    private async Task<HashSet<string>> ReadOwnedItemIdsAsync(
        string uid,
        string token)
    {
        FirestoreListEnvelope list =
            await ListFirestoreDocumentsAsync(
                $"inventories/{Uri.EscapeDataString(uid)}/items",
                token);

        HashSet<string> owned = new HashSet<string>();
        if (list == null || list.documents == null)
        {
            return owned;
        }

        foreach (FirestoreDocument document in list.documents)
        {
            if (document == null || document.fields == null ||
                document.fields.owned == null ||
                !document.fields.owned.booleanValue)
            {
                continue;
            }

            string itemId = ReadString(document.fields.itemId);
            if (string.IsNullOrWhiteSpace(itemId))
            {
                itemId = LastDocumentSegment(document.name);
            }

            if (!string.IsNullOrWhiteSpace(itemId))
            {
                owned.Add(itemId);
            }
        }

        return owned;
    }

    private async Task<List<AtlasBoardStorefrontItem>> ReadCatalogAsync(
        string token)
    {
        FirestoreListEnvelope list =
            await ListFirestoreDocumentsAsync("item_catalog", token);

        List<AtlasBoardStorefrontItem> output =
            new List<AtlasBoardStorefrontItem>();

        if (list == null || list.documents == null)
        {
            return output;
        }

        foreach (FirestoreDocument document in list.documents)
        {
            FirestoreFields fields = document != null
                ? document.fields
                : null;

            if (fields == null || fields.active == null ||
                !fields.active.booleanValue)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(
                    ReadString(fields.seasonalEventId)))
            {
                continue;
            }

            string itemId = ReadString(fields.itemId);
            if (string.IsNullOrWhiteSpace(itemId))
            {
                itemId = LastDocumentSegment(document.name);
            }

            FirestoreMapFields prices =
                fields.prices != null &&
                fields.prices.mapValue != null
                    ? fields.prices.mapValue.fields
                    : null;

            output.Add(
                new AtlasBoardStorefrontItem
                {
                    ItemId = itemId,
                    ItemType = ReadString(fields.itemType),
                    DisplayName = string.IsNullOrWhiteSpace(
                        ReadString(fields.displayName))
                            ? itemId
                            : ReadString(fields.displayName),
                    GoldPrice = prices != null
                        ? ReadInteger(prices.gold)
                        : 0,
                    AtlasCoinPrice = prices != null
                        ? ReadInteger(prices.atlas_coin)
                        : 0,
                    SortOrder = ReadInteger(fields.sortOrder),
                    CatalogVersion = ReadInteger(fields.catalogVersion)
                });
        }

        return output
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.DisplayName)
            .ToList();
    }

    private async Task<List<AtlasBoardMetaHistoryItem>>
        ReadUnifiedHistoryAsync(string uid, string token)
    {
        List<AtlasBoardMetaHistoryItem> output =
            new List<AtlasBoardMetaHistoryItem>();

        await ReadPurchaseHistoryAsync(uid, token, output);
        await ReadRedeemHistoryAsync(uid, token, output);
        await ReadDailyRewardHistoryAsync(uid, token, output);

        return output
            .OrderByDescending(item => ParseUtcSort(item.CreatedAtUtc))
            .Take(40)
            .ToList();
    }

    private async Task ReadPurchaseHistoryAsync(
        string uid,
        string token,
        List<AtlasBoardMetaHistoryItem> output)
    {
        FirestoreListEnvelope list =
            await ListFirestoreDocumentsAsync(
                "commerce_transactions",
                token);

        if (list == null || list.documents == null)
        {
            return;
        }

        foreach (FirestoreDocument document in list.documents)
        {
            FirestoreFields fields = document != null
                ? document.fields
                : null;

            if (fields == null ||
                !string.Equals(
                    ReadString(fields.uid),
                    uid,
                    StringComparison.Ordinal))
            {
                continue;
            }

            string transactionId = ReadString(fields.transactionId);
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                transactionId = LastDocumentSegment(document.name);
            }

            output.Add(
                new AtlasBoardMetaHistoryItem
                {
                    Type = "PURCHASE",
                    TransactionId = transactionId,
                    ReferenceId = ReadString(fields.itemId),
                    Status = ReadString(fields.status),
                    CurrencyId = ReadString(fields.currencyId),
                    Amount = ReadInteger(fields.amount),
                    Detail = ReadString(fields.itemId),
                    CreatedAtUtc = ReadTimestamp(fields.createdAt)
                });
        }
    }

    private async Task ReadRedeemHistoryAsync(
        string uid,
        string token,
        List<AtlasBoardMetaHistoryItem> output)
    {
        FirestoreListEnvelope redemptions =
            await ListFirestoreDocumentsAsync(
                "promo_redemptions",
                token);

        if (redemptions == null || redemptions.documents == null)
        {
            return;
        }

        foreach (FirestoreDocument redemption in redemptions.documents)
        {
            FirestoreFields rootFields = redemption != null
                ? redemption.fields
                : null;

            if (rootFields == null ||
                !string.Equals(
                    ReadString(rootFields.uid),
                    uid,
                    StringComparison.Ordinal))
            {
                continue;
            }

            string redemptionId =
                ReadString(rootFields.redemptionId);
            if (string.IsNullOrWhiteSpace(redemptionId))
            {
                redemptionId = LastDocumentSegment(redemption.name);
            }

            string promoId = ReadString(rootFields.promoId);
            string displayCode =
                await ReadPromoDisplayCodeAsync(
                    promoId,
                    token);

            List<FirestoreDocument> eventDocuments =
                new List<FirestoreDocument>();

            try
            {
                FirestoreListEnvelope events =
                    await ListFirestoreDocumentsAsync(
                        $"promo_redemptions/" +
                        $"{Uri.EscapeDataString(redemptionId)}/events",
                        token);

                if (events != null && events.documents != null)
                {
                    eventDocuments.AddRange(
                        events.documents.Where(item => item != null));
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Redeem history nested-list fallback activated. " +
                    exception.Message,
                    this);
            }

            if (eventDocuments.Count == 0)
            {
                string lastEventId =
                    ReadString(rootFields.lastRedemptionEventId);

                if (!string.IsNullOrWhiteSpace(lastEventId))
                {
                    FirestoreDocument exact =
                        await GetFirestoreDocumentAsync(
                            $"promo_redemptions/" +
                            $"{Uri.EscapeDataString(redemptionId)}/events/" +
                            $"{Uri.EscapeDataString(lastEventId)}",
                            token,
                            true);

                    if (exact != null)
                    {
                        eventDocuments.Add(exact);
                    }
                }
            }

            if (eventDocuments.Count == 0)
            {
                string fallbackTime =
                    ReadTimestamp(rootFields.lastRedeemedAt);
                if (string.IsNullOrWhiteSpace(fallbackTime))
                {
                    fallbackTime =
                        ReadTimestamp(rootFields.firstRedeemedAt);
                }

                output.Add(
                    new AtlasBoardMetaHistoryItem
                    {
                        Type = "REDEEM_CODE",
                        TransactionId =
                            ReadString(rootFields.lastRedemptionEventId),
                        ReferenceId = redemptionId,
                        Status = "succeeded",
                        CurrencyId = string.Empty,
                        Amount = 0,
                        Detail =
                            string.IsNullOrWhiteSpace(displayCode)
                                ? ShortId(promoId)
                                : displayCode,
                        CreatedAtUtc = fallbackTime
                    });

                continue;
            }

            foreach (FirestoreDocument eventDocument in eventDocuments)
            {
                FirestoreFields fields = eventDocument.fields;
                if (fields == null)
                {
                    continue;
                }

                string eventId =
                    ReadString(fields.redemptionEventId);
                if (string.IsNullOrWhiteSpace(eventId))
                {
                    eventId =
                        LastDocumentSegment(eventDocument.name);
                }

                output.Add(
                    new AtlasBoardMetaHistoryItem
                    {
                        Type = "REDEEM_CODE",
                        TransactionId = eventId,
                        ReferenceId = redemptionId,
                        Status = "succeeded",
                        CurrencyId = string.Empty,
                        Amount = 0,
                        Detail =
                            string.IsNullOrWhiteSpace(displayCode)
                                ? ShortId(promoId) + " | " +
                                  DescribeRewards(fields.rewards)
                                : displayCode + " | " +
                                  DescribeRewards(fields.rewards),
                        CreatedAtUtc =
                            ReadTimestamp(fields.createdAt)
                    });
            }
        }
    }

    private async Task ReadDailyRewardHistoryAsync(
        string uid,
        string token,
        List<AtlasBoardMetaHistoryItem> output)
    {
        FirestoreListEnvelope list =
            await ListFirestoreDocumentsAsync(
                "wallet_ledger",
                token);

        if (list == null || list.documents == null)
        {
            return;
        }

        foreach (FirestoreDocument document in list.documents)
        {
            FirestoreFields fields = document != null
                ? document.fields
                : null;

            if (fields == null ||
                !string.Equals(ReadString(fields.uid), uid, StringComparison.Ordinal) ||
                !string.Equals(
                    ReadString(fields.reason),
                    "phase9_daily_reward",
                    StringComparison.Ordinal))
            {
                continue;
            }

            output.Add(
                new AtlasBoardMetaHistoryItem
                {
                    Type = "DAILY_REWARD",
                    TransactionId = ReadString(fields.transactionId),
                    ReferenceId = LastDocumentSegment(document.name),
                    Status = "succeeded",
                    CurrencyId = ReadString(fields.currencyId),
                    Amount = Math.Abs(ReadInteger(fields.delta)),
                    Detail = "+" + Math.Abs(ReadInteger(fields.delta)) + " Gold",
                    CreatedAtUtc = ReadTimestamp(fields.createdAt)
                });
        }
    }

    private async Task<string> ReadPromoDisplayCodeAsync(
        string promoId,
        string token)
    {
        if (string.IsNullOrWhiteSpace(promoId))
        {
            return string.Empty;
        }

        FirestoreDocument document =
            await GetFirestoreDocumentAsync(
                $"promo_codes/{Uri.EscapeDataString(promoId)}",
                token,
                true);

        return document != null && document.fields != null
            ? ReadString(document.fields.displayCode)
            : string.Empty;
    }

    private static string DescribeRewards(FirestoreArrayValue value)
    {
        if (value == null || value.arrayValue == null ||
            value.arrayValue.values == null ||
            value.arrayValue.values.Length == 0)
        {
            return "reward";
        }

        List<string> parts = new List<string>();
        foreach (FirestoreMapValue entry in value.arrayValue.values)
        {
            FirestoreMapFields fields =
                entry != null && entry.mapValue != null
                    ? entry.mapValue.fields
                    : null;

            if (fields == null)
            {
                continue;
            }

            string type = ReadString(fields.type);
            string itemId = ReadString(fields.itemId);
            string currencyId = ReadString(fields.currencyId);
            int amount = ReadInteger(fields.amount);

            if (!string.IsNullOrWhiteSpace(itemId))
            {
                parts.Add(itemId);
            }
            else if (amount > 0)
            {
                parts.Add($"{amount} {currencyId}");
            }
            else if (!string.IsNullOrWhiteSpace(type))
            {
                parts.Add(type);
            }
        }

        return parts.Count > 0
            ? string.Join(", ", parts)
            : "reward";
    }

    private async Task<TEnvelope> CallFunctionAsync<TRequest, TEnvelope>(
        string functionName,
        TRequest requestBody)
        where TEnvelope : class
    {
        lastErrorKey = string.Empty;
        lastTechnicalMessage = string.Empty;

        string token = lobbyBridge.AuthTokenForOnlineSubsystems;
        string url =
            $"http://{lobbyBridge.EmulatorHostForOnlineSubsystems}:{FunctionsPort}/" +
            $"{ProjectId}/{Region}/{functionName}";
        string callableJson =
            "{\"data\":" + JsonUtility.ToJson(requestBody) + "}";

        using UnityWebRequest request =
            new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler =
            new UploadHandlerRaw(Encoding.UTF8.GetBytes(callableJson));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {token}");
        request.timeout = 20;

        await SendRequestAsync(request);
        string body = request.downloadHandler != null
            ? request.downloadHandler.text
            : string.Empty;

        if (request.result != UnityWebRequest.Result.Success)
        {
            CallableErrorEnvelope error =
                SafeFromJson<CallableErrorEnvelope>(body);
            lastErrorKey =
                error != null && error.error != null &&
                error.error.details != null &&
                !string.IsNullOrWhiteSpace(error.error.details.errorKey)
                    ? error.error.details.errorKey
                    : "meta.error.service_unavailable";
            lastTechnicalMessage =
                error != null && error.error != null &&
                !string.IsNullOrWhiteSpace(error.error.message)
                    ? error.error.message
                    : $"HTTP {request.responseCode}: {request.error}";
            return null;
        }

        TEnvelope envelope = SafeFromJson<TEnvelope>(body);
        if (envelope == null)
        {
            lastErrorKey = "meta.error.service_unavailable";
            lastTechnicalMessage = "Callable response could not be parsed.";
        }

        return envelope;
    }

    private async Task<FirestoreDocument> GetFirestoreDocumentAsync(
        string documentPath,
        string token,
        bool allowMissing)
    {
        string url = FirestoreDocumentUrl(documentPath);
        using UnityWebRequest request = UnityWebRequest.Get(url);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.SetRequestHeader("Authorization", $"Bearer {token}");
        }
        request.timeout = 10;
        await SendRequestAsync(request);

        if (request.responseCode == 404 && allowMissing)
        {
            return null;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            throw new InvalidOperationException(
                $"Firestore GET failed ({request.responseCode}): {request.error}");
        }

        return SafeFromJson<FirestoreDocument>(
            request.downloadHandler.text);
    }

    private async Task<FirestoreListEnvelope> ListFirestoreDocumentsAsync(
        string collectionPath,
        string token)
    {
        string url =
            $"http://{lobbyBridge.EmulatorHostForOnlineSubsystems}:{FirestorePort}/v1/projects/" +
            $"{ProjectId}/databases/(default)/documents/" +
            $"{collectionPath}?pageSize=100";

        using UnityWebRequest request = UnityWebRequest.Get(url);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.SetRequestHeader("Authorization", $"Bearer {token}");
        }
        request.timeout = 10;
        await SendRequestAsync(request);

        if (request.responseCode == 404)
        {
            return new FirestoreListEnvelope();
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            throw new InvalidOperationException(
                $"Firestore LIST failed ({request.responseCode}): {request.error}");
        }

        return SafeFromJson<FirestoreListEnvelope>(
                   request.downloadHandler.text) ??
               new FirestoreListEnvelope();
    }

    private string FirestoreDocumentUrl(string documentPath)
    {
        return $"http://{lobbyBridge.EmulatorHostForOnlineSubsystems}:{FirestorePort}/v1/projects/" +
               $"{ProjectId}/databases/(default)/documents/{documentPath}";
    }

    private void ResolveLobbyBridge()
    {
        if (lobbyBridge != null)
        {
            return;
        }

        AtlasBoardLobbyRuntimeBridge[] all =
            Resources.FindObjectsOfTypeAll<AtlasBoardLobbyRuntimeBridge>();
        foreach (AtlasBoardLobbyRuntimeBridge item in all)
        {
            if (item != null && item.gameObject.scene.IsValid())
            {
                lobbyBridge = item;
                break;
            }
        }
    }

    private AtlasBoardMetaEconomySnapshot FailSnapshot()
    {
        return new AtlasBoardMetaEconomySnapshot
        {
            Success = false,
            ErrorKey = string.IsNullOrWhiteSpace(lastErrorKey)
                ? "meta.error.service_unavailable"
                : lastErrorKey,
            TechnicalMessage = lastTechnicalMessage
        };
    }

    private AtlasBoardMetaEconomyOperationResult FailOperation()
    {
        return new AtlasBoardMetaEconomyOperationResult
        {
            Success = false,
            ErrorKey = string.IsNullOrWhiteSpace(lastErrorKey)
                ? "meta.error.service_unavailable"
                : lastErrorKey,
            TechnicalMessage = lastTechnicalMessage
        };
    }

    private static int ReadInteger(FirestoreInteger value)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.integerValue))
        {
            return 0;
        }

        return int.TryParse(
            value.integerValue,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int parsed)
                ? parsed
                : 0;
    }

    private static string ReadString(FirestoreString value)
    {
        return value != null
            ? value.stringValue ?? string.Empty
            : string.Empty;
    }

    private static string ReadTimestamp(FirestoreTimestamp value)
    {
        return value != null
            ? value.timestampValue ?? string.Empty
            : string.Empty;
    }

    private static DateTime ParseUtcSort(string value)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out DateTime parsed)
                ? parsed.ToUniversalTime()
                : DateTime.MinValue;
    }

    private static string LastDocumentSegment(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        int index = name.LastIndexOf('/');
        return index >= 0 && index + 1 < name.Length
            ? name.Substring(index + 1)
            : name;
    }

    private static string ShortId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= 10
            ? value
            : value.Substring(0, 10);
    }

    private static async Task SendRequestAsync(UnityWebRequest request)
    {
        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            await Task.Yield();
        }
    }

    private static T SafeFromJson<T>(string json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch
        {
            return null;
        }
    }

    [Serializable]
    private sealed class MetaDailyRewardRequest
    {
    }

    [Serializable]
    private sealed class WalletMutationRequest
    {
        public string currencyId;
        public int delta;
        public string reason;
        public string transactionId;
        public string idempotencyKey;
    }

    [Serializable]
    private sealed class WalletMutationEnvelope
    {
        public WalletMutationWire result;
    }

    [Serializable]
    private sealed class WalletMutationWire
    {
        public bool ok;
        public int balanceAfter;
        public bool idempotentReplay;
        public string transactionId;
        public string serverDayUtc;
        public int rewardGold;
    }

    [Serializable]
    private sealed class CommercePurchaseRequest
    {
        public string itemId;
        public string paymentMethod;
        public string idempotencyKey;
    }

    [Serializable]
    private sealed class CommercePurchaseEnvelope
    {
        public CommercePurchaseWire result;
    }

    [Serializable]
    private sealed class CommercePurchaseWire
    {
        public bool ok;
        public string itemId;
        public string transactionId;
        public string status;
        public int balanceAfter;
        public bool idempotentReplay;
    }

    [Serializable]
    private sealed class PromoRedeemRequest
    {
        public string code;
        public string idempotencyKey;
    }

    [Serializable]
    private sealed class PromoRedeemEnvelope
    {
        public PromoRedeemWire result;
    }

    [Serializable]
    private sealed class PromoRedeemWire
    {
        public bool ok;
        public bool applied;
        public bool idempotentReplay;
        public string promoId;
        public string redemptionId;
        public string redemptionEventId;
        public PromoRewardWire[] rewards;
    }

    [Serializable]
    private sealed class PromoRewardWire
    {
        public string type;
        public string currencyId;
        public string itemId;
        public int amount;
    }

    [Serializable]
    private sealed class SeasonalEmptyRequest
    {
    }

    [Serializable]
    private sealed class SeasonalChallengeRequest
    {
        public string challengeId;
    }

    [Serializable]
    private sealed class SeasonalTrackRequest
    {
        public string tierId;
    }

    [Serializable]
    private sealed class SeasonalPurchaseRequest
    {
        public string itemId;
        public string idempotencyKey;
    }

    [Serializable]
    private sealed class SeasonalOverviewEnvelope
    {
        public SeasonalOverviewWire result;
    }

    [Serializable]
    private sealed class SeasonalOverviewWire
    {
        public bool ok;
        public SeasonalEventWire @event;
        public int ticketBalance;
        public int eventXp;
        public SeasonalChallengeWire[] challenges;
        public SeasonalTrackTierWire[] trackTiers;
        public SeasonalLimitedItemWire[] limitedItems;
    }

    [Serializable]
    private sealed class SeasonalEventWire
    {
        public bool active;
        public string eventId;
        public string titleKey;
        public string subtitleKey;
        public long startsAtEpochMs;
        public long endsAtEpochMs;
        public string ticketCurrencyId;
    }

    [Serializable]
    private sealed class SeasonalChallengeWire
    {
        public string challengeId;
        public string titleKey;
        public string descriptionKey;
        public string cadence;
        public int target;
        public int progress;
        public int rewardTickets;
        public int rewardXp;
        public bool claimed;
        public bool claimable;
        public string periodKey;
        public int sortOrder;
    }

    [Serializable]
    private sealed class SeasonalTrackTierWire
    {
        public string tierId;
        public string titleKey;
        public int requiredXp;
        public string rewardType;
        public int rewardAmount;
        public bool claimed;
        public bool claimable;
        public int sortOrder;
    }

    [Serializable]
    private sealed class SeasonalLimitedItemWire
    {
        public string itemId;
        public string itemType;
        public string displayName;
        public int ticketPrice;
        public long limitedUntilEpochMs;
        public bool owned;
        public int sortOrder;
    }

    [Serializable]
    private sealed class SeasonalClaimEnvelope
    {
        public SeasonalClaimWire result;
    }

    [Serializable]
    private sealed class SeasonalClaimWire
    {
        public bool ok;
        public bool applied;
        public bool idempotentReplay;
        public string eventId;
        public string referenceId;
        public string transactionId;
        public int ticketBalance;
        public int eventXp;
        public string rewardType;
        public int rewardAmount;
    }

    [Serializable]
    private sealed class SeasonalPurchaseEnvelope
    {
        public SeasonalPurchaseWire result;
    }

    [Serializable]
    private sealed class SeasonalPurchaseWire
    {
        public bool ok;
        public bool applied;
        public bool idempotentReplay;
        public string eventId;
        public string itemId;
        public string transactionId;
        public int ticketPrice;
        public int ticketBalance;
        public bool owned;
    }

    [Serializable]
    private sealed class MetaDevResetRequest
    {
        public string operationId;
    }

    [Serializable]
    private sealed class MetaDevResetEnvelope
    {
        public MetaDevResetWire result;
    }

    [Serializable]
    private sealed class MetaDevResetWire
    {
        public bool ok;
        public int resetCount;
        public string auditId;
    }

    [Serializable]
    private sealed class CallableErrorEnvelope
    {
        public CallableError error;
    }

    [Serializable]
    private sealed class CallableError
    {
        public string message;
        public CallableErrorDetails details;
    }

    [Serializable]
    private sealed class CallableErrorDetails
    {
        public string errorKey;
    }

    [Serializable]
    private sealed class FirestoreListEnvelope
    {
        public FirestoreDocument[] documents;
    }

    [Serializable]
    private sealed class FirestoreDocument
    {
        public string name;
        public FirestoreFields fields;
    }

    [Serializable]
    private sealed class FirestoreFields
    {
        public FirestoreString uid;
        public FirestoreString itemId;
        public FirestoreString itemType;
        public FirestoreString displayName;
        public FirestoreString avatarId;
        public FirestoreString displayCode;
        public FirestoreString status;
        public FirestoreString currencyId;
        public FirestoreString transactionId;
        public FirestoreString reason;
        public FirestoreString promoId;
        public FirestoreString redemptionId;
        public FirestoreString redemptionEventId;
        public FirestoreString lastRedemptionEventId;
        public FirestoreString seasonalEventId;
        public FirestoreBoolean active;
        public FirestoreBoolean owned;
        public FirestoreInteger amount;
        public FirestoreInteger delta;
        public FirestoreInteger sortOrder;
        public FirestoreInteger catalogVersion;
        public FirestoreMapValue prices;
        public FirestoreArrayValue rewards;
        public FirestoreTimestamp createdAt;
        public FirestoreTimestamp firstRedeemedAt;
        public FirestoreTimestamp lastRedeemedAt;
    }

    [Serializable]
    private sealed class FirestoreString
    {
        public string stringValue;
    }

    [Serializable]
    private sealed class FirestoreBoolean
    {
        public bool booleanValue;
    }

    [Serializable]
    private sealed class FirestoreInteger
    {
        public string integerValue;
    }

    [Serializable]
    private sealed class FirestoreTimestamp
    {
        public string timestampValue;
    }

    [Serializable]
    private sealed class FirestoreMapValue
    {
        public FirestoreMap mapValue;
    }

    [Serializable]
    private sealed class FirestoreMap
    {
        public FirestoreMapFields fields;
    }

    [Serializable]
    private sealed class FirestoreArrayValue
    {
        public FirestoreArray arrayValue;
    }

    [Serializable]
    private sealed class FirestoreArray
    {
        public FirestoreMapValue[] values;
    }

    [Serializable]
    private sealed class FirestoreMapFields
    {
        public FirestoreInteger gold;
        public FirestoreInteger atlas_coin;
        public FirestoreString type;
        public FirestoreString currencyId;
        public FirestoreString itemId;
        public FirestoreInteger amount;
    }
}
