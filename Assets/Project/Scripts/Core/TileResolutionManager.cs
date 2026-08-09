using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TileResolutionManager : MonoBehaviour
{
    [Header("Players")]
    [SerializeField]
    private PlayerGameState[] playerStates;

    [Header("Purchase UI")]
    [SerializeField]
    private GameObject purchasePanel;

    [SerializeField]
    private TMP_Text purchaseInfoText;

    [SerializeField]
    private Button buyButton;

    [SerializeField]
    private Button skipButton;

    [Header("Economy UI")]
    [SerializeField]
    private TMP_Text balancesText;

    [Header("Special Tile Managers")]
    [SerializeField]
    private EventCardManager eventCardManager;

    [SerializeField]
    private SpecialTileManager specialTileManager;

    [SerializeField]
    private AuctionManager auctionManager;

    [SerializeField]
    private BankruptcyManager bankruptcyManager;

    [SerializeField]
    private PropertyDevelopmentManager propertyDevelopmentManager;

    [Header("Travel")]
    [SerializeField]
    private BoardPath boardPath;

    [SerializeField]
    private BoardEconomyProfile economyProfile;

    [SerializeField]
    private GameObject travelPanel;

    [SerializeField]
    private TMP_Text travelInfoText;

    [SerializeField]
    private Button travelGoButton;

    [SerializeField]
    private Button travelStayButton;

    [Header("Development UI")]
    [SerializeField]
    private GameObject developmentPanel;

    [SerializeField]
    private TMP_Text developmentInfoText;

    [SerializeField]
    private Button developButton;

    [SerializeField]
    private Button skipDevelopmentButton;

    [Header("Prototype Special Tile Values")]
    [SerializeField, Min(0)]
    private int taxAmount = 100;

    [SerializeField, Min(0)]
    private int bonusAmount = 100;

    [SerializeField, Min(0)]
    private int vacationBonusAmount = 50;

    [SerializeField, Min(1)]
    private int restAreaTurnsToSkip = 1;

    private PlayerGameState pendingPlayer;
    private BoardTile pendingTile;

    private PlayerGameState pendingTravelPlayer;
    private PlayerPawnMover pendingTravelPawn;
    private int pendingTravelTargetIndex = -1;
    private int pendingTravelFee;

    private PlayerGameState pendingDevelopmentPlayer;
    private BoardTile pendingDevelopmentTile;

    private Action resolutionCompleted;

    public PlayerGameState PendingPurchasePlayer =>
        pendingPlayer;

    public BoardTile PendingPurchaseTile =>
        pendingTile;

    public bool HasPendingPurchaseFor(
        PlayerGameState player)
    {
        return player != null &&
               pendingPlayer != null &&
               pendingTile != null &&
               (pendingPlayer == player ||
                pendingPlayer.PlayerSlotIndex ==
                player.PlayerSlotIndex);
    }

    public PlayerGameState PendingTravelPlayer =>
        pendingTravelPlayer;

    public int PendingTravelTargetIndex =>
        pendingTravelTargetIndex;

    public PlayerGameState PendingDevelopmentPlayer =>
        pendingDevelopmentPlayer;

    public BoardTile PendingDevelopmentTile =>
        pendingDevelopmentTile;

    public bool HasPendingTravelFor(
        PlayerGameState player)
    {
        return player != null &&
               pendingTravelPlayer != null &&
               pendingTravelTargetIndex >= 0 &&
               (pendingTravelPlayer == player ||
                pendingTravelPlayer.PlayerSlotIndex ==
                player.PlayerSlotIndex);
    }

    public bool HasPendingDevelopmentFor(
        PlayerGameState player)
    {
        return player != null &&
               pendingDevelopmentPlayer != null &&
               pendingDevelopmentTile != null &&
               (pendingDevelopmentPlayer == player ||
                pendingDevelopmentPlayer.PlayerSlotIndex ==
                player.PlayerSlotIndex);
    }

    private void Start()
    {
        if (!ValidatePlayerConfiguration())
        {
            enabled = false;
            return;
        }

        if (purchasePanel != null)
        {
            purchasePanel.SetActive(false);
        }

        if (buyButton != null)
        {
            buyButton.interactable = true;
        }

        if (skipButton != null)
        {
            skipButton.interactable = true;
        }

        if (travelPanel != null)
        {
            travelPanel.SetActive(false);
        }

        if (travelGoButton != null)
        {
            travelGoButton.interactable = true;
        }

        if (travelStayButton != null)
        {
            travelStayButton.interactable = true;
        }

        if (developmentPanel != null)
        {
            developmentPanel.SetActive(false);
        }

        if (developButton != null)
        {
            developButton.interactable = true;
        }

        if (skipDevelopmentButton != null)
        {
            skipDevelopmentButton.interactable = true;
        }

        if (boardPath == null)
        {
            boardPath = FindAnyObjectByType<BoardPath>();
        }

        SubscribeToMoneyChanges();
        RefreshBalancesText();
    }

    private void OnDestroy()
    {
        UnsubscribeFromMoneyChanges();
    }

    public void ResolveTile(
        PlayerGameState player,
        BoardTile tile,
        Action onResolutionCompleted)
    {
        resolutionCompleted = onResolutionCompleted;

        if (player == null ||
            tile == null ||
            player.IsBankrupt)
        {
            CompleteResolution();
            return;
        }

        Debug.Log(
            $"{player.DisplayName} [Slot {player.PlayerSlotIndex}] landed on " +
            $"{tile.DisplayName} ({tile.TileType}).",
            this);

        if (tile.TileType == TileType.City)
        {
            ResolveCityTile(player, tile);
            return;
        }

        ResolveSpecialTile(player, tile);
    }

    public bool TryResolveBotPurchase(
        PlayerGameState player,
        bool buyProperty)
    {
        if (!HasPendingPurchaseFor(player))
        {
            return false;
        }

        if (!IsBotPlayer(player))
        {
            Debug.LogWarning(
                $"{player.DisplayName} tried to use the bot " +
                "purchase path without an enabled bot controller.",
                this);

            return false;
        }

        if (buyProperty)
        {
            BuyPendingTile();
        }
        else
        {
            SkipPendingTile();
        }

        return true;
    }

    public void BuyPendingTile()
    {
        if (pendingPlayer == null || pendingTile == null)
        {
            Debug.LogWarning(
                "There is no pending city purchase.",
                this);

            return;
        }

        int price = pendingTile.PurchasePrice;

        if (!pendingPlayer.TrySpend(price))
        {
            Debug.LogWarning(
                $"{pendingPlayer.DisplayName} does not have enough money.",
                this);

            CompleteResolution();
            return;
        }

        bool ownershipAssigned =
            pendingTile.TrySetOwner(pendingPlayer.PlayerSlotIndex);

        if (!ownershipAssigned)
        {
            pendingPlayer.AddMoney(price);

            Debug.LogWarning(
                "Ownership could not be assigned. Money was refunded.",
                this);

            CompleteResolution();
            return;
        }

        ApplyOwnershipVisual(pendingTile, pendingPlayer);

        Debug.Log(
            $"{pendingPlayer.DisplayName} " +
            $"[Slot {pendingPlayer.PlayerSlotIndex}, " +
            $"Profile {GetProfileId(pendingPlayer)}] purchased " +
            $"{pendingTile.DisplayName} for {price}.",
            this);

        CompleteResolution();
    }

    public void SkipPendingTile()
    {
        if (pendingPlayer == null || pendingTile == null)
        {
            Debug.LogWarning(
                "There is no pending city purchase to skip.",
                this);

            return;
        }

        PlayerGameState decliningPlayer =
            pendingPlayer;

        BoardTile declinedTile =
            pendingTile;

        pendingPlayer = null;
        pendingTile = null;

        if (purchasePanel != null)
        {
            purchasePanel.SetActive(false);
        }

        Debug.Log(
            $"{decliningPlayer.DisplayName} declined " +
            $"{declinedTile.DisplayName}. " +
            "The same property is being sent to auction.",
            this);

        if (auctionManager == null)
        {
            Debug.LogWarning(
                "AuctionManager is not connected. " +
                "The declined property will remain unowned.",
                this);

            CompleteResolution();
            return;
        }

        auctionManager.BeginAuctionForProperty(
            decliningPlayer,
            declinedTile,
            CompleteResolution);
    }

    public bool TryResolveBotTravel(
        PlayerGameState player,
        bool shouldTravel)
    {
        if (!HasPendingTravelFor(player) ||
            !IsBotPlayer(player))
        {
            return false;
        }

        if (shouldTravel)
        {
            TravelToNextEvent();
        }
        else
        {
            StayOnTravelTile();
        }

        return true;
    }

    public bool TryResolveBotDevelopment(
        PlayerGameState player,
        bool shouldDevelop)
    {
        if (!HasPendingDevelopmentFor(player) ||
            !IsBotPlayer(player))
        {
            return false;
        }

        if (shouldDevelop)
        {
            DevelopPendingTile();
        }
        else
        {
            SkipPendingDevelopment();
        }

        return true;
    }

    public void DevelopPendingTile()
    {
        if (pendingDevelopmentPlayer == null ||
            pendingDevelopmentTile == null)
        {
            Debug.LogWarning(
                "There is no pending property development.",
                this);

            return;
        }

        PlayerGameState developingPlayer =
            pendingDevelopmentPlayer;

        BoardTile developingTile =
            pendingDevelopmentTile;

        if (propertyDevelopmentManager == null)
        {
            Debug.LogWarning(
                "PropertyDevelopmentManager is not connected.",
                this);

            CompleteResolution();
            return;
        }

        bool developed =
            propertyDevelopmentManager.TryDevelop(
                developingPlayer,
                developingTile);

        if (!developed)
        {
            Debug.LogWarning(
                $"{developingPlayer.DisplayName} could not " +
                $"develop {developingTile.DisplayName}.",
                this);

            RefreshDevelopmentPanel();
            return;
        }

        CompleteResolution();
    }

    public void SkipPendingDevelopment()
    {
        if (pendingDevelopmentPlayer != null &&
            pendingDevelopmentTile != null)
        {
            Debug.Log(
                $"{pendingDevelopmentPlayer.DisplayName} skipped " +
                $"development on " +
                $"{pendingDevelopmentTile.DisplayName}.",
                this);
        }

        CompleteResolution();
    }

    public void TravelToNextEvent()
    {
        if (pendingTravelPlayer == null ||
            pendingTravelPawn == null ||
            pendingTravelTargetIndex < 0)
        {
            Debug.LogWarning(
                "There is no pending travel decision.",
                this);

            return;
        }

        PlayerGameState travellingPlayer =
            pendingTravelPlayer;

        PlayerPawnMover travellingPawn =
            pendingTravelPawn;

        int targetTileIndex =
            pendingTravelTargetIndex;

        int travelFee =
            pendingTravelFee;

        if (travelFee > 0 &&
            !travellingPlayer.TrySpend(
                travelFee))
        {
            Debug.Log(
                $"{travellingPlayer.DisplayName} could not " +
                $"afford the {travelFee} ₵ travel fee.",
                this);

            StayOnTravelTile();
            return;
        }

        ClearPendingTravelState();

        if (travelPanel != null)
        {
            travelPanel.SetActive(false);
        }

        Debug.Log(
            $"{travellingPlayer.DisplayName} chose to travel " +
            $"to tile {targetTileIndex}.",
            this);

        bool movementStarted =
            travellingPawn.MoveForwardToTile(
                targetTileIndex,
                completedPawn =>
                    HandleTravelMovementCompleted(
                        travellingPlayer,
                        completedPawn));

        if (!movementStarted)
        {
            Debug.LogWarning(
                "Travel movement could not start.",
                this);

            CompleteResolution();
        }
    }

    public void StayOnTravelTile()
    {
        if (pendingTravelPlayer != null)
        {
            Debug.Log(
                $"{pendingTravelPlayer.DisplayName} chose to stay " +
                "on the Travel tile.",
                this);
        }

        CompleteResolution();
    }

    private void ResolveCityTile(
        PlayerGameState player,
        BoardTile tile)
    {
        if (!tile.Purchasable)
        {
            CompleteResolution();
            return;
        }

        if (!tile.IsOwned)
        {
            if (player.CurrentMoney < tile.PurchasePrice)
            {
                Debug.Log(
                    $"{player.DisplayName} cannot afford " +
                    $"{tile.DisplayName}.",
                    this);

                CompleteResolution();
                return;
            }

            pendingPlayer = player;
            pendingTile = tile;

            if (purchaseInfoText != null)
            {
                purchaseInfoText.text =
                    $"{tile.DisplayName}\n" +
                    $"Fiyat: {tile.PurchasePrice} ₵\n" +
                    "Satın almak istiyor musun?";
            }

            if (purchasePanel != null)
            {
                purchasePanel.SetActive(true);
            }

            RefreshPurchaseButtonAvailability();

            return;
        }

        if (tile.OwnerPlayerIndex == player.PlayerSlotIndex)
        {
            Debug.Log(
                $"{player.DisplayName} landed on their own city.",
                this);

            if (propertyDevelopmentManager != null &&
                propertyDevelopmentManager
                    .IsEligibleForDevelopment(
                        player,
                        tile))
            {
                pendingDevelopmentPlayer =
                    player;

                pendingDevelopmentTile =
                    tile;

                RefreshDevelopmentPanel();

                if (developmentPanel != null)
                {
                    developmentPanel.SetActive(true);
                }

                RefreshDevelopmentButtonAvailability();

                return;
            }

            CompleteResolution();
            return;
        }

        PlayerGameState owner =
            GetPlayerStateByStableSlot(tile.OwnerPlayerIndex);

        if (owner == null)
        {
            Debug.LogError(
                $"The owner slot {tile.OwnerPlayerIndex} of " +
                $"{tile.DisplayName} could not be resolved.",
                tile);

            CompleteResolution();
            return;
        }

        int effectiveRent =
            propertyDevelopmentManager != null
                ? propertyDevelopmentManager
                    .GetEffectiveRent(tile)
                : tile.BaseRent;

        if (bankruptcyManager == null)
        {
            int payableRent =
                Mathf.Min(
                    effectiveRent,
                    player.CurrentMoney);

            if (player.TrySpend(payableRent))
            {
                owner.AddMoney(payableRent);

                Debug.Log(
                    $"{player.DisplayName} paid " +
                    $"{payableRent} rent to " +
                    $"{owner.DisplayName} for " +
                    $"{tile.DisplayName}.",
                    this);
            }

            CompleteResolution();
            return;
        }

        BankruptcyManager.PaymentResolution payment =
            bankruptcyManager.ResolveMandatoryPayment(
                player,
                owner,
                effectiveRent,
                $"Rent: {tile.DisplayName}");

        if (!payment.DebtorBankrupt)
        {
            Debug.Log(
                $"{player.DisplayName} paid " +
                $"{payment.AmountPaid} rent to " +
                $"{owner.DisplayName} for " +
                $"{tile.DisplayName}.",
                this);

            CompleteResolution();
            return;
        }

        string bankruptcyDescription =
            $"{player.DisplayName}, " +
            $"{tile.DisplayName} için gereken " +
            $"{payment.AmountDue} ₵ kirayı " +
            "ödeyemedi. Kalan nakit ve mülkler " +
            $"{owner.DisplayName} hesabına aktarıldı.";

        string bankruptcyResult =
            $"Ödenen: {payment.AmountPaid} ₵\n" +
            $"Karşılanamayan: " +
            $"{payment.UnpaidAmount} ₵\n" +
            $"Devredilen mülk: " +
            $"{payment.TransferredPropertyCount}";

        if (specialTileManager != null)
        {
            specialTileManager.ShowResultMessage(
                player,
                "İFLAS",
                bankruptcyDescription,
                bankruptcyResult,
                CompleteResolution);

            return;
        }

        CompleteResolution();
    }

    private void ResolveSpecialTile(
        PlayerGameState player,
        BoardTile tile)
    {
        switch (tile.TileType)
        {
            case TileType.Start:
                Debug.Log(
                    $"{player.DisplayName} landed on Start.",
                    this);

                CompleteResolution();
                return;

            case TileType.Event:
                ResolveEventTile(player);
                return;

            case TileType.Tax:
                ResolveTaxTile(player);
                return;

            case TileType.Bonus:
                ResolveBonusTile(player);
                return;

            case TileType.RestArea:
                ResolveRestAreaTile(player);
                return;

            case TileType.Vacation:
                ResolveVacationTile(player);
                return;

            case TileType.Travel:
                ResolveTravelTile(player);
                return;

            case TileType.Auction:
                ResolveAuctionTile(player);
                return;

            default:
                Debug.Log(
                    "Special tile resolution is not implemented yet: " +
                    $"{tile.TileType}.",
                    this);

                CompleteResolution();
                return;
        }
    }

    private void ResolveEventTile(PlayerGameState player)
    {
        if (eventCardManager == null)
        {
            Debug.LogWarning(
                "EventCardManager is not connected.",
                this);

            CompleteResolution();
            return;
        }

        eventCardManager.ResolveRandomEvent(
            player,
            CompleteResolution);
    }

    private void ResolveTaxTile(PlayerGameState player)
    {
        if (specialTileManager == null)
        {
            Debug.LogWarning(
                "SpecialTileManager is not connected.",
                this);

            CompleteResolution();
            return;
        }

        int amount =
            GetSpecialTileValue(
                pendingTile,
                ResolveEconomyProfile()?.TaxAmount ??
                taxAmount);

        specialTileManager.ResolveMoneyEffect(
            player,
            "Vergi Ödemesi",
            "İşletme giderleri ve yerel vergileri " +
            "ödemen gerekiyor.",
            -amount,
            CompleteResolution);
    }

    private void ResolveBonusTile(PlayerGameState player)
    {
        if (specialTileManager == null)
        {
            Debug.LogWarning(
                "SpecialTileManager is not connected.",
                this);

            CompleteResolution();
            return;
        }

        int amount =
            GetSpecialTileValue(
                pendingTile,
                ResolveEconomyProfile()?.BonusAmount ??
                bonusAmount);

        specialTileManager.ResolveMoneyEffect(
            player,
            "Bölge Bonusu",
            "Yerel ticaret desteğinden bonus kazandın.",
            amount,
            CompleteResolution);
    }

    private void ResolveRestAreaTile(PlayerGameState player)
    {
        int turnsToSkip =
            GetSpecialTileValue(
                pendingTile,
                ResolveEconomyProfile()
                    ?.RestAreaTurnsToSkip ??
                restAreaTurnsToSkip);

        turnsToSkip =
            Mathf.Max(
                1,
                turnsToSkip);

        player.AddTurnsToSkip(turnsToSkip);

        if (specialTileManager == null)
        {
            Debug.LogWarning(
                "SpecialTileManager is not connected.",
                this);

            CompleteResolution();
            return;
        }

        specialTileManager.ResolveMoneyEffect(
            player,
            "Dinlenme Alanı",
            $"Bir sonraki {turnsToSkip} turunu atlayacaksın.",
            0,
            CompleteResolution);
    }

    private void ResolveVacationTile(PlayerGameState player)
    {
        if (specialTileManager == null)
        {
            Debug.LogWarning(
                "SpecialTileManager is not connected.",
                this);

            CompleteResolution();
            return;
        }

        int amount =
            GetSpecialTileValue(
                pendingTile,
                ResolveEconomyProfile()
                    ?.VacationBonusAmount ??
                vacationBonusAmount);

        specialTileManager.ResolveMoneyEffect(
            player,
            "Tatil Bölgesi",
            "Tatil etkinliğinden ek gelir kazandın.",
            amount,
            CompleteResolution);
    }

    private void ResolveTravelTile(PlayerGameState player)
    {
        if (boardPath == null)
        {
            boardPath = FindAnyObjectByType<BoardPath>();
        }

        PlayerPawnMover pawn =
            player.GetComponent<PlayerPawnMover>();

        if (boardPath == null || pawn == null)
        {
            Debug.LogWarning(
                "Travel cannot be resolved because BoardPath or " +
                "PlayerPawnMover is missing.",
                this);

            CompleteResolution();
            return;
        }

        int targetTileIndex =
            FindNextTileOfType(
                pawn.CurrentTileIndex,
                TileType.Event);

        if (targetTileIndex < 0)
        {
            Debug.LogWarning(
                "No Event tile was found after the Travel tile.",
                this);

            CompleteResolution();
            return;
        }

        BoardTile targetTile =
            boardPath.GetTile(targetTileIndex);

        pendingTravelPlayer = player;
        pendingTravelPawn = pawn;
        pendingTravelTargetIndex = targetTileIndex;

        BoardEconomyProfile activeEconomy =
            ResolveEconomyProfile();

        pendingTravelFee =
            GetSpecialTileValue(
                pendingTile,
                activeEconomy?.TravelFee ?? 0);

        int startReward =
            activeEconomy?.StartPassReward ?? 200;

        if (travelInfoText != null)
        {
            string feeLine =
                pendingTravelFee > 0
                    ? $"Seyahat ücreti: " +
                      $"{pendingTravelFee} ₵\n"
                    : "Seyahat ücreti: Ücretsiz\n";

            string affordabilityLine =
                pendingTravelFee > 0 &&
                player.CurrentMoney <
                    pendingTravelFee
                    ? "\nBakiye yetersiz: " +
                      "seyahat edemezsin."
                    : string.Empty;

            travelInfoText.text =
                "SEYAHAT MERKEZİ\n\n" +
                "En yakın Etkinlik karesine gitmek ister misin?\n" +
                $"Hedef: {targetTile.DisplayName}\n" +
                $"{feeLine}\n" +
                $"Başlangıçtan geçersen +" +
                $"{startReward} ₵ kazanırsın." +
                $"{affordabilityLine}";
        }

        if (travelPanel != null)
        {
            travelPanel.SetActive(true);
        }

        RefreshTravelButtonAvailability();
    }

    private void ResolveAuctionTile(PlayerGameState player)
    {
        if (auctionManager == null)
        {
            Debug.LogWarning(
                "AuctionManager is not connected.",
                this);

            CompleteResolution();
            return;
        }

        auctionManager.BeginAuction(
            player,
            CompleteResolution);
    }

    private void HandleTravelMovementCompleted(
        PlayerGameState travellingPlayer,
        PlayerPawnMover completedPawn)
    {
        if (travellingPlayer == null || completedPawn == null)
        {
            CompleteResolution();
            return;
        }

        BoardTile destinationTile =
            completedPawn.GetCurrentTile();

        if (destinationTile == null)
        {
            CompleteResolution();
            return;
        }

        Debug.Log(
            $"Travel completed. {travellingPlayer.DisplayName} " +
            $"arrived at {destinationTile.DisplayName}.",
            this);

        Action finalTurnCallback = resolutionCompleted;

        ResolveTile(
            travellingPlayer,
            destinationTile,
            finalTurnCallback);
    }

    private void RefreshDevelopmentPanel()
    {
        if (propertyDevelopmentManager == null ||
            pendingDevelopmentPlayer == null ||
            pendingDevelopmentTile == null)
        {
            return;
        }

        int currentLevel =
            propertyDevelopmentManager
                .GetDevelopmentLevel(
                    pendingDevelopmentTile);

        int cost =
            propertyDevelopmentManager
                .GetDevelopmentCost(
                    pendingDevelopmentTile);

        int currentRent =
            propertyDevelopmentManager
                .GetEffectiveRent(
                    pendingDevelopmentTile);

        int nextRent =
            propertyDevelopmentManager
                .GetProjectedRentAtNextLevel(
                    pendingDevelopmentTile);

        string groupName =
            propertyDevelopmentManager
                .GetGroupName(
                    pendingDevelopmentTile);

        bool canAfford =
            propertyDevelopmentManager
                .CanAffordDevelopment(
                    pendingDevelopmentPlayer,
                    pendingDevelopmentTile);

        bool canDevelopEvenly =
            propertyDevelopmentManager
                .CanDevelopEvenly(
                    pendingDevelopmentPlayer,
                    pendingDevelopmentTile);

        string groupLevels =
            propertyDevelopmentManager
                .GetGroupDevelopmentSummary(
                    pendingDevelopmentTile);

        string blockReason =
            propertyDevelopmentManager
                .GetDevelopmentBlockReason(
                    pendingDevelopmentPlayer,
                    pendingDevelopmentTile);

        if (developmentInfoText != null)
        {
            string ruleLine =
                propertyDevelopmentManager
                    .RequireBalancedGroupDevelopment
                    ? $"Bölge seviyeleri: {groupLevels}\n"
                    : string.Empty;

            string reasonLine =
                !string.IsNullOrEmpty(blockReason)
                    ? $"\n{blockReason}"
                    : string.Empty;

            developmentInfoText.text =
                $"{pendingDevelopmentTile.DisplayName}\n" +
                $"{groupName} tamamlandı\n" +
                $"Seviye: {currentLevel}/" +
                $"{propertyDevelopmentManager.MaximumDevelopmentLevel}\n" +
                $"{ruleLine}" +
                $"Kira: {currentRent} ₵ → " +
                $"{nextRent} ₵\n" +
                $"Geliştirme maliyeti: {cost} ₵\n" +
                $"Bakiye: " +
                $"{pendingDevelopmentPlayer.CurrentMoney} ₵" +
                $"{reasonLine}";
        }

        bool humanCanControl =
            !IsBotPlayer(
                pendingDevelopmentPlayer);

        if (developButton != null)
        {
            developButton.interactable =
                humanCanControl &&
                canDevelopEvenly &&
                canAfford;
        }

        if (skipDevelopmentButton != null)
        {
            skipDevelopmentButton.interactable =
                humanCanControl;
        }
    }

    private void RefreshDevelopmentButtonAvailability()
    {
        if (pendingDevelopmentPlayer == null)
        {
            return;
        }

        bool humanCanControl =
            !IsBotPlayer(
                pendingDevelopmentPlayer);

        bool canAfford =
            propertyDevelopmentManager != null &&
            pendingDevelopmentTile != null &&
            propertyDevelopmentManager
                .CanAffordDevelopment(
                    pendingDevelopmentPlayer,
                    pendingDevelopmentTile);

        if (developButton != null)
        {
            developButton.interactable =
                humanCanControl &&
                canAfford;
        }

        if (skipDevelopmentButton != null)
        {
            skipDevelopmentButton.interactable =
                humanCanControl;
        }
    }

    private void RefreshTravelButtonAvailability()
    {
        bool humanCanControl =
            pendingTravelPlayer != null &&
            !IsBotPlayer(
                pendingTravelPlayer);

        bool canAffordTravel =
            pendingTravelPlayer != null &&
            (pendingTravelFee <= 0 ||
             pendingTravelPlayer.CurrentMoney >=
                pendingTravelFee);

        if (travelGoButton != null)
        {
            travelGoButton.interactable =
                humanCanControl &&
                canAffordTravel;
        }

        if (travelStayButton != null)
        {
            travelStayButton.interactable =
                humanCanControl;
        }
    }

    private int FindNextTileOfType(
        int currentTileIndex,
        TileType targetType)
    {
        if (boardPath == null || boardPath.TileCount == 0)
        {
            return -1;
        }

        for (int offset = 1;
             offset <= boardPath.TileCount;
             offset++)
        {
            int candidateIndex =
                (currentTileIndex + offset) %
                boardPath.TileCount;

            BoardTile candidateTile =
                boardPath.GetTile(candidateIndex);

            if (candidateTile != null &&
                candidateTile.TileType == targetType)
            {
                return candidateIndex;
            }
        }

        return -1;
    }

    private void RefreshPurchaseButtonAvailability()
    {
        bool humanCanControl =
            pendingPlayer != null &&
            !IsBotPlayer(pendingPlayer);

        if (buyButton != null)
        {
            buyButton.interactable =
                humanCanControl &&
                pendingTile != null &&
                pendingPlayer.CurrentMoney >=
                pendingTile.PurchasePrice;
        }

        if (skipButton != null)
        {
            skipButton.interactable =
                humanCanControl;
        }
    }

    private bool IsBotPlayer(
        PlayerGameState player)
    {
        if (player == null)
        {
            return false;
        }

        BotPlayerController botController =
            player.GetComponent<BotPlayerController>();

        return botController != null &&
               botController.BotEnabled;
    }

    private void ApplyOwnershipVisual(
        BoardTile tile,
        PlayerGameState owner)
    {
        if (tile == null || owner == null)
        {
            return;
        }

        Material ownershipMaterial =
            owner.OwnershipMaterial;

        if (ownershipMaterial == null)
        {
            Debug.LogError(
                $"{owner.DisplayName} does not have an ownership material.",
                owner);

            return;
        }

        tile.ApplyOwnerMaterial(ownershipMaterial);
    }

    private PlayerGameState GetPlayerStateByStableSlot(
        int stablePlayerSlotIndex)
    {
        if (playerStates == null)
        {
            return null;
        }

        foreach (PlayerGameState player in playerStates)
        {
            if (player != null &&
                player.PlayerSlotIndex == stablePlayerSlotIndex)
            {
                return player;
            }
        }

        return null;
    }

    private void CompleteResolution()
    {
        if (purchasePanel != null)
        {
            purchasePanel.SetActive(false);
        }

        if (buyButton != null)
        {
            buyButton.interactable = true;
        }

        if (skipButton != null)
        {
            skipButton.interactable = true;
        }

        if (travelPanel != null)
        {
            travelPanel.SetActive(false);
        }

        if (travelGoButton != null)
        {
            travelGoButton.interactable = true;
        }

        if (travelStayButton != null)
        {
            travelStayButton.interactable = true;
        }

        if (developmentPanel != null)
        {
            developmentPanel.SetActive(false);
        }

        if (developButton != null)
        {
            developButton.interactable = true;
        }

        if (skipDevelopmentButton != null)
        {
            skipDevelopmentButton.interactable = true;
        }

        pendingPlayer = null;
        pendingTile = null;

        pendingDevelopmentPlayer = null;
        pendingDevelopmentTile = null;

        ClearPendingTravelState();
        RefreshBalancesText();

        Action callback = resolutionCompleted;
        resolutionCompleted = null;

        callback?.Invoke();
    }

    private BoardEconomyProfile
        ResolveEconomyProfile()
    {
        if (economyProfile != null)
        {
            return economyProfile;
        }

        BoardGenerator generator =
            FindAnyObjectByType<
                BoardGenerator>();

        if (generator != null)
        {
            economyProfile =
                generator.ActiveEconomyProfile;
        }

        return economyProfile;
    }

    private int GetSpecialTileValue(
        BoardTile tile,
        int profileValue)
    {
        if (tile != null &&
            tile.SpecialValueOverride != 0)
        {
            return Mathf.Abs(
                tile.SpecialValueOverride);
        }

        return Mathf.Max(
            0,
            profileValue);
    }

    private void ClearPendingTravelState()
    {
        pendingTravelPlayer = null;
        pendingTravelPawn = null;
        pendingTravelTargetIndex = -1;
        pendingTravelFee = 0;
    }

    private void RefreshBalancesText()
    {
        if (balancesText == null || playerStates == null)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();

        foreach (PlayerGameState player in playerStates)
        {
            if (player == null ||
                !player.IsParticipating)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(
                player.IsBankrupt
                    ? $"{player.DisplayName}: İFLAS"
                    : $"{player.DisplayName}: " +
                      $"{player.CurrentMoney} ₵");
        }

        balancesText.text = builder.ToString();
    }

    private void SubscribeToMoneyChanges()
    {
        if (playerStates == null)
        {
            return;
        }

        foreach (PlayerGameState player in playerStates)
        {
            if (player != null)
            {
                player.MoneyChanged += HandleMoneyChanged;
            }
        }
    }

    private void UnsubscribeFromMoneyChanges()
    {
        if (playerStates == null)
        {
            return;
        }

        foreach (PlayerGameState player in playerStates)
        {
            if (player != null)
            {
                player.MoneyChanged -= HandleMoneyChanged;
            }
        }
    }

    private void HandleMoneyChanged(PlayerGameState player)
    {
        RefreshBalancesText();
    }

    private bool ValidatePlayerConfiguration()
    {
        if (playerStates == null || playerStates.Length < 2)
        {
            Debug.LogError(
                "TileResolutionManager requires at least two player states.",
                this);

            return false;
        }

        HashSet<int> usedSlots = new HashSet<int>();

        foreach (PlayerGameState player in playerStates)
        {
            if (player == null)
            {
                Debug.LogError(
                    "TileResolutionManager contains an empty player slot.",
                    this);

                return false;
            }

            if (!usedSlots.Add(player.PlayerSlotIndex))
            {
                Debug.LogError(
                    $"Duplicate stable player slot detected: " +
                    $"{player.PlayerSlotIndex}.",
                    player);

                return false;
            }

            if (player.VisualProfile == null ||
                player.OwnershipMaterial == null)
            {
                Debug.LogError(
                    $"{player.DisplayName} has an incomplete visual profile.",
                    player);

                return false;
            }
        }

        return true;
    }

    private string GetProfileId(PlayerGameState player)
    {
        if (player == null || player.VisualProfile == null)
        {
            return "none";
        }

        return player.VisualProfile.ProfileId;
    }
}
