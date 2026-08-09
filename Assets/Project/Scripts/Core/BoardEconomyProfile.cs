using UnityEngine;

[CreateAssetMenu(
    fileName = "EconomyProfile_New",
    menuName = "Atlas Board/Economy/Board Economy Profile")]
public class BoardEconomyProfile : ScriptableObject
{
    [Header("Core Economy")]
    [SerializeField, Min(0)]
    private int startingMoney = 1500;

    [SerializeField, Min(0)]
    private int startPassReward = 200;

    [Header("Special Tiles")]
    [SerializeField, Min(0)]
    private int taxAmount = 120;

    [SerializeField, Min(0)]
    private int bonusAmount = 100;

    [SerializeField, Min(0)]
    private int vacationBonusAmount = 75;

    [SerializeField, Min(0)]
    private int travelFee = 0;

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
    [Tooltip(
        "Level 0 through maximum development level.")]
    [SerializeField]
    private int[] rentMultipliers =
        { 1, 2, 3, 5, 8 };

    public int StartingMoney => startingMoney;
    public int StartPassReward => startPassReward;
    public int TaxAmount => taxAmount;
    public int BonusAmount => bonusAmount;
    public int VacationBonusAmount =>
        vacationBonusAmount;
    public int TravelFee => travelFee;
    public int RestAreaTurnsToSkip =>
        restAreaTurnsToSkip;
    public int AuctionMinimumBid =>
        auctionMinimumBid;
    public int AuctionSmallBidStep =>
        auctionSmallBidStep;
    public int AuctionLargeBidStep =>
        auctionLargeBidStep;

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

#if UNITY_EDITOR
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
        vacationBonusAmount =
            newVacationBonusAmount;
        travelFee = newTravelFee;
        restAreaTurnsToSkip =
            newRestAreaTurnsToSkip;
        auctionMinimumBid =
            newAuctionMinimumBid;
        auctionSmallBidStep =
            newAuctionSmallBidStep;
        auctionLargeBidStep =
            newAuctionLargeBidStep;
        rentMultipliers =
            newRentMultipliers;
    }
#endif
}
