using System;
using System.Collections.Generic;

[Serializable]
public sealed class AtlasBoardStorefrontItem
{
    public string ItemId;
    public string ItemType;
    public string DisplayName;
    public int GoldPrice;
    public int AtlasCoinPrice;
    public int SortOrder;
    public int CatalogVersion;
    public bool Owned;
}

[Serializable]
public sealed class AtlasBoardMetaRewardLine
{
    public string Type;
    public string CurrencyId;
    public string ItemId;
    public int Amount;
}

[Serializable]
public sealed class AtlasBoardMetaHistoryItem
{
    public string Type;
    public string TransactionId;
    public string ReferenceId;
    public string Status;
    public string CurrencyId;
    public int Amount;
    public string Detail;
    public string CreatedAtUtc;
}

public sealed class AtlasBoardMetaEconomySnapshot
{
    public bool Success;
    public string ErrorKey;
    public string TechnicalMessage;
    public int Gold;
    public int AtlasCoin;
    public string DisplayName;
    public string AvatarId;

    public readonly List<AtlasBoardStorefrontItem> Items =
        new List<AtlasBoardStorefrontItem>();

    public readonly List<AtlasBoardMetaHistoryItem> History =
        new List<AtlasBoardMetaHistoryItem>();
}

public sealed class AtlasBoardMetaEconomyOperationResult
{
    public bool Success;
    public string ErrorKey;
    public string TechnicalMessage;
    public string Type;
    public string ItemId;
    public string TransactionId;
    public string ReferenceId;
    public string Detail;
    public bool IdempotentReplay;
    public bool AlreadyClaimed;
    public int Gold = -1;
    public int AtlasCoin = -1;

    public readonly List<AtlasBoardMetaRewardLine> Rewards =
        new List<AtlasBoardMetaRewardLine>();
}

[Serializable]
public sealed class AtlasBoardSeasonalEvent
{
    public bool Active;
    public string EventId;
    public string TitleKey;
    public string SubtitleKey;
    public long StartsAtEpochMs;
    public long EndsAtEpochMs;
    public string TicketCurrencyId;
}

[Serializable]
public sealed class AtlasBoardSeasonalChallenge
{
    public string ChallengeId;
    public string TitleKey;
    public string DescriptionKey;
    public string Cadence;
    public int Target;
    public int Progress;
    public int RewardTickets;
    public int RewardXp;
    public bool Claimed;
    public bool Claimable;
    public string PeriodKey;
    public int SortOrder;
}

[Serializable]
public sealed class AtlasBoardSeasonalTrackTier
{
    public string TierId;
    public string TitleKey;
    public int RequiredXp;
    public string RewardType;
    public int RewardAmount;
    public bool Claimed;
    public bool Claimable;
    public int SortOrder;
}

[Serializable]
public sealed class AtlasBoardSeasonalLimitedItem
{
    public string ItemId;
    public string ItemType;
    public string DisplayName;
    public int TicketPrice;
    public long LimitedUntilEpochMs;
    public bool Owned;
    public int SortOrder;
}

public sealed class AtlasBoardSeasonalOverview
{
    public bool Success;
    public string ErrorKey;
    public string TechnicalMessage;
    public AtlasBoardSeasonalEvent Event = new AtlasBoardSeasonalEvent();
    public int TicketBalance;
    public int EventXp;

    public readonly List<AtlasBoardSeasonalChallenge> Challenges =
        new List<AtlasBoardSeasonalChallenge>();

    public readonly List<AtlasBoardSeasonalTrackTier> TrackTiers =
        new List<AtlasBoardSeasonalTrackTier>();

    public readonly List<AtlasBoardSeasonalLimitedItem> LimitedItems =
        new List<AtlasBoardSeasonalLimitedItem>();
}
