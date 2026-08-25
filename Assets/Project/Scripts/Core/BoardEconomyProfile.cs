using System;
using UnityEngine;

[Serializable]
public class PropertyGroupEconomyTier
{
    [SerializeField]
    private string groupId = "group_01";

    [Header("Purchase Price")]
    [SerializeField, Min(0)]
    private int propertyOnePrice = 100;

    [SerializeField, Min(0)]
    private int propertyTwoPrice = 110;

    [SerializeField, Min(0)]
    private int propertyThreePrice = 120;

    [Header("Base Rent")]
    [SerializeField, Min(0)]
    private int propertyOneRent = 8;

    [SerializeField, Min(0)]
    private int propertyTwoRent = 9;

    [SerializeField, Min(0)]
    private int propertyThreeRent = 10;

    [Header("Development")]
    [SerializeField, Min(0)]
    private int developmentCost = 50;

    public string GroupId => groupId;
    public int DevelopmentCost => developmentCost;

    public int GetPurchasePrice(int memberIndex)
    {
        return Mathf.Max(
            0,
            memberIndex <= 0
                ? propertyOnePrice
                : memberIndex == 1
                    ? propertyTwoPrice
                    : propertyThreePrice);
    }

    public int GetBaseRent(int memberIndex)
    {
        return Mathf.Max(
            0,
            memberIndex <= 0
                ? propertyOneRent
                : memberIndex == 1
                    ? propertyTwoRent
                    : propertyThreeRent);
    }

    public PropertyGroupEconomyTier()
    {
    }

    public PropertyGroupEconomyTier(
        string newGroupId,
        int priceOne,
        int priceTwo,
        int priceThree,
        int rentOne,
        int rentTwo,
        int rentThree,
        int newDevelopmentCost)
    {
        groupId = newGroupId;
        propertyOnePrice = priceOne;
        propertyTwoPrice = priceTwo;
        propertyThreePrice = priceThree;
        propertyOneRent = rentOne;
        propertyTwoRent = rentTwo;
        propertyThreeRent = rentThree;
        developmentCost = newDevelopmentCost;
    }
}

[CreateAssetMenu(
    fileName = "EconomyProfile_New",
    menuName = "Atlas Board/Economy/Board Economy Profile")]
public class BoardEconomyProfile : ScriptableObject
{
    [Header("Version")]
    [SerializeField]
    private string balanceVersion = "economy_v1";

    [Header("Core Economy")]
    [SerializeField, Min(0)]
    private int startingMoney = 1500;

    [SerializeField, Min(0)]
    private int startPassReward = 200;

    [Header("Special Tiles")]
    [SerializeField, Min(0)]
    private int taxAmount = 180;

    [SerializeField, Min(0)]
    private int bonusAmount = 100;

    [SerializeField, Min(0)]
    private int vacationBonusAmount = 75;

    [SerializeField, Min(0)]
    private int travelFee = 50;

    [SerializeField, Min(0)]
    private int restAreaTurnsToSkip = 1;

    [Header("Auction")]
    [SerializeField, Min(1)]
    private int auctionMinimumBid = 10;

    [SerializeField, Min(1)]
    private int auctionSmallBidStep = 10;

    [SerializeField, Min(1)]
    private int auctionLargeBidStep = 50;

    [Header("Development Rent Multipliers")]
    [Tooltip("Level 0 through maximum development level.")]
    [SerializeField]
    private int[] rentMultipliers =
        { 1, 2, 3, 5, 8 };

    [Header("Property Group Economy")]
    [Tooltip(
        "Edit these values in the Economy Profile, then run " +
        "Atlas Board > Balance > Sync Property Economy From Map Profiles.")]
    [SerializeField]
    private PropertyGroupEconomyTier[] propertyGroupTiers =
    {
        new PropertyGroupEconomyTier(
            "group_01", 100, 110, 120, 8, 9, 10, 50),
        new PropertyGroupEconomyTier(
            "group_02", 130, 140, 150, 10, 11, 12, 60),
        new PropertyGroupEconomyTier(
            "group_03", 160, 175, 190, 13, 14, 15, 70),
        new PropertyGroupEconomyTier(
            "group_04", 205, 220, 235, 16, 17, 18, 80),
        new PropertyGroupEconomyTier(
            "group_05", 250, 265, 280, 20, 21, 22, 100),
        new PropertyGroupEconomyTier(
            "group_06", 295, 310, 325, 24, 25, 26, 120),
        new PropertyGroupEconomyTier(
            "group_07", 340, 360, 380, 28, 30, 32, 140),
        new PropertyGroupEconomyTier(
            "group_08", 400, 425, 450, 34, 37, 40, 160)
    };

    [Header("Bot - Cash Management")]
    [SerializeField, Min(0)]
    private int botSafeCashReserve = 250;

    [SerializeField, Min(0)]
    private int botGroupCompletionCashReserve = 150;

    [SerializeField, Min(0)]
    private int botDevelopmentCashReserve = 300;

    [Header("Bot - Purchase")]
    [SerializeField, Range(0.1f, 0.95f)]
    private float botMaximumPurchaseCashRatio = 0.55f;

    [Tooltip(
        "Extra purchase-ratio room when the bot already owns another " +
        "property in the same group.")]
    [SerializeField, Range(0f, 0.4f)]
    private float botGroupInterestPurchaseBonus = 0.10f;

    [Tooltip(
        "Extra purchase-ratio room when this property completes the group.")]
    [SerializeField, Range(0f, 0.4f)]
    private float botGroupCompletionPurchaseBonus = 0.20f;

    [Header("Bot - Auction Valuation")]
    [SerializeField, Range(0.25f, 2f)]
    private float botAuctionNormalValueMultiplier = 0.90f;

    [SerializeField, Range(0.25f, 2f)]
    private float botAuctionGroupInterestValueMultiplier = 1.00f;

    [SerializeField, Range(0.25f, 2f)]
    private float botAuctionGroupCompletionValueMultiplier = 1.30f;

    [SerializeField, Min(0)]
    private int botLargeBidSafetyMargin = 30;

    [Header("Bot - Development")]
    [SerializeField, Range(0.05f, 1f)]
    private float botMaximumDevelopmentCashRatio = 0.30f;

    [Header("Bot - Trade")]
    [Tooltip(
        "Base chance to consider ONE outgoing trade before rolling. " +
        "Personality modifies this. Current baseline = 11%.")]
    [SerializeField, Range(0f, 1f)]
    private float botTradeAttemptChance = 0.11f;

    [SerializeField, Range(0.5f, 1.5f)]
    private float botMinimumTradeValueRatio = 0.95f;

    [SerializeField, Range(0.5f, 2f)]
    private float botGeneralPropertyOfferMultiplier = 1.00f;

    [SerializeField, Range(0.5f, 2f)]
    private float botGroupCompletionOfferMultiplier = 1.25f;

    [SerializeField, Range(0.5f, 2f)]
    private float botIncomingGroupCompletionValueMultiplier = 1.30f;

    [SerializeField, Range(0.5f, 2f)]
    private float botCompleteGroupProtectionValueMultiplier = 1.40f;

    [SerializeField, Range(0.5f, 2f)]
    private float botOpponentGroupCompletionProtectionValueMultiplier = 1.30f;

    [Header("Bot - Travel")]
    [SerializeField, Range(0f, 1f)]
    private float botTravelChanceWithoutStartReward = 0.60f;

    public string BalanceVersion => balanceVersion;
    public int StartingMoney => startingMoney;
    public int StartPassReward => startPassReward;
    public int TaxAmount => taxAmount;
    public int BonusAmount => bonusAmount;
    public int VacationBonusAmount => vacationBonusAmount;
    public int TravelFee => travelFee;
    public int RestAreaTurnsToSkip => restAreaTurnsToSkip;
    public int AuctionMinimumBid => auctionMinimumBid;
    public int AuctionSmallBidStep => auctionSmallBidStep;
    public int AuctionLargeBidStep => auctionLargeBidStep;

    public int BotSafeCashReserve => botSafeCashReserve;
    public int BotGroupCompletionCashReserve =>
        botGroupCompletionCashReserve;
    public int BotDevelopmentCashReserve =>
        botDevelopmentCashReserve;
    public float BotMaximumPurchaseCashRatio =>
        botMaximumPurchaseCashRatio;
    public float BotGroupInterestPurchaseBonus =>
        botGroupInterestPurchaseBonus;
    public float BotGroupCompletionPurchaseBonus =>
        botGroupCompletionPurchaseBonus;
    public float BotAuctionNormalValueMultiplier =>
        botAuctionNormalValueMultiplier;
    public float BotAuctionGroupInterestValueMultiplier =>
        botAuctionGroupInterestValueMultiplier;
    public float BotAuctionGroupCompletionValueMultiplier =>
        botAuctionGroupCompletionValueMultiplier;
    public int BotLargeBidSafetyMargin =>
        botLargeBidSafetyMargin;
    public float BotMaximumDevelopmentCashRatio =>
        botMaximumDevelopmentCashRatio;
    public float BotTradeAttemptChance =>
        botTradeAttemptChance;
    public float BotMinimumTradeValueRatio =>
        botMinimumTradeValueRatio;
    public float BotGeneralPropertyOfferMultiplier =>
        botGeneralPropertyOfferMultiplier;
    public float BotGroupCompletionOfferMultiplier =>
        botGroupCompletionOfferMultiplier;
    public float BotIncomingGroupCompletionValueMultiplier =>
        botIncomingGroupCompletionValueMultiplier;
    public float BotCompleteGroupProtectionValueMultiplier =>
        botCompleteGroupProtectionValueMultiplier;
    public float BotOpponentGroupCompletionProtectionValueMultiplier =>
        botOpponentGroupCompletionProtectionValueMultiplier;
    public float BotTravelChanceWithoutStartReward =>
        botTravelChanceWithoutStartReward;

    public int GetRentMultiplier(
        int developmentLevel)
    {
        if (rentMultipliers == null ||
            rentMultipliers.Length == 0)
        {
            return 1;
        }

        int index =
            Mathf.Clamp(
                developmentLevel,
                0,
                rentMultipliers.Length - 1);

        return Mathf.Max(
            1,
            rentMultipliers[index]);
    }

    public bool TryGetPropertyEconomy(
        string groupId,
        int memberIndex,
        out int purchasePrice,
        out int baseRent,
        out int developmentCost)
    {
        purchasePrice = 0;
        baseRent = 0;
        developmentCost = 0;

        if (propertyGroupTiers == null ||
            string.IsNullOrWhiteSpace(groupId))
        {
            return false;
        }

        foreach (PropertyGroupEconomyTier tier
                 in propertyGroupTiers)
        {
            if (tier == null ||
                !string.Equals(
                    tier.GroupId,
                    groupId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            int safeMemberIndex =
                Mathf.Clamp(memberIndex, 0, 2);

            purchasePrice =
                tier.GetPurchasePrice(safeMemberIndex);

            baseRent =
                tier.GetBaseRent(safeMemberIndex);

            developmentCost =
                tier.DevelopmentCost;

            return true;
        }

        return false;
    }

#if UNITY_EDITOR
    // Kept for compatibility with the existing starter-data creator.
    public void EditorConfigure(
        int newStartingMoney,
        int newStartPassReward,
        int newTaxAmount,
        int newBonusAmount,
        int newVacationBonusAmount,
        int newTravelFee,
        int newRestAreaTurnsToSkip,
        int newAuctionMinimumBid,
        int newAuctionSmallBidStep,
        int newAuctionLargeBidStep,
        int[] newRentMultipliers)
    {
        startingMoney = newStartingMoney;
        startPassReward = newStartPassReward;
        taxAmount = newTaxAmount;
        bonusAmount = newBonusAmount;
        vacationBonusAmount = newVacationBonusAmount;
        travelFee = newTravelFee;
        restAreaTurnsToSkip = newRestAreaTurnsToSkip;
        auctionMinimumBid = newAuctionMinimumBid;
        auctionSmallBidStep = newAuctionSmallBidStep;
        auctionLargeBidStep = newAuctionLargeBidStep;
        rentMultipliers = newRentMultipliers;
    }

    public void EditorApplyEconomyBalanceV1()
    {
        balanceVersion = "economy_v1";

        startingMoney = 1500;
        startPassReward = 200;

        taxAmount = 180;
        bonusAmount = 100;
        vacationBonusAmount = 75;
        travelFee = 50;
        restAreaTurnsToSkip = 1;

        auctionMinimumBid = 10;
        auctionSmallBidStep = 10;
        auctionLargeBidStep = 50;

        rentMultipliers =
            new[] { 1, 2, 3, 5, 8 };

        propertyGroupTiers =
            new[]
            {
                new PropertyGroupEconomyTier(
                    "group_01", 100, 110, 120, 8, 9, 10, 50),
                new PropertyGroupEconomyTier(
                    "group_02", 130, 140, 150, 10, 11, 12, 60),
                new PropertyGroupEconomyTier(
                    "group_03", 160, 175, 190, 13, 14, 15, 70),
                new PropertyGroupEconomyTier(
                    "group_04", 205, 220, 235, 16, 17, 18, 80),
                new PropertyGroupEconomyTier(
                    "group_05", 250, 265, 280, 20, 21, 22, 100),
                new PropertyGroupEconomyTier(
                    "group_06", 295, 310, 325, 24, 25, 26, 120),
                new PropertyGroupEconomyTier(
                    "group_07", 340, 360, 380, 28, 30, 32, 140),
                new PropertyGroupEconomyTier(
                    "group_08", 400, 425, 450, 34, 37, 40, 160)
            };

        botSafeCashReserve = 250;
        botGroupCompletionCashReserve = 150;
        botDevelopmentCashReserve = 300;

        botMaximumPurchaseCashRatio = 0.55f;
        botGroupInterestPurchaseBonus = 0.10f;
        botGroupCompletionPurchaseBonus = 0.20f;

        botAuctionNormalValueMultiplier = 0.90f;
        botAuctionGroupInterestValueMultiplier = 1.00f;
        botAuctionGroupCompletionValueMultiplier = 1.30f;
        botLargeBidSafetyMargin = 30;

        botMaximumDevelopmentCashRatio = 0.30f;

        botTradeAttemptChance = 0.11f;
        botMinimumTradeValueRatio = 0.95f;
        botGeneralPropertyOfferMultiplier = 1.00f;
        botGroupCompletionOfferMultiplier = 1.25f;
        botIncomingGroupCompletionValueMultiplier = 1.30f;
        botCompleteGroupProtectionValueMultiplier = 1.40f;
        botOpponentGroupCompletionProtectionValueMultiplier = 1.30f;

        botTravelChanceWithoutStartReward = 0.60f;
    }
#endif
}
