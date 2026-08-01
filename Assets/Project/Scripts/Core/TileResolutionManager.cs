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

    [Header("Travel")]
    [SerializeField]
    private BoardPath boardPath;

    [SerializeField]
    private GameObject travelPanel;

    [SerializeField]
    private TMP_Text travelInfoText;

    [SerializeField]
    private Button travelGoButton;

    [SerializeField]
    private Button travelStayButton;

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

    private Action resolutionCompleted;

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

        if (travelPanel != null)
        {
            travelPanel.SetActive(false);
        }

        if (boardPath == null)
        {
            boardPath = FindFirstObjectByType<BoardPath>();
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

        if (player == null || tile == null)
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

        PlayerGameState travellingPlayer = pendingTravelPlayer;
        PlayerPawnMover travellingPawn = pendingTravelPawn;
        int targetTileIndex = pendingTravelTargetIndex;

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

            return;
        }

        if (tile.OwnerPlayerIndex == player.PlayerSlotIndex)
        {
            Debug.Log(
                $"{player.DisplayName} landed on their own city.",
                this);

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

        int payableRent =
            Mathf.Min(tile.BaseRent, player.CurrentMoney);

        if (player.TrySpend(payableRent))
        {
            owner.AddMoney(payableRent);

            Debug.Log(
                $"{player.DisplayName} paid {payableRent} rent to " +
                $"{owner.DisplayName} for {tile.DisplayName}.",
                this);
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

        specialTileManager.ResolveMoneyEffect(
            player,
            "Vergi Ödemesi",
            "Yerel işletme ve şehir vergilerini ödemen gerekiyor.",
            -taxAmount,
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

        specialTileManager.ResolveMoneyEffect(
            player,
            "Şehir Bonusu",
            "Bölgesel ticaret desteği kazandın.",
            bonusAmount,
            CompleteResolution);
    }

    private void ResolveRestAreaTile(PlayerGameState player)
    {
        player.AddTurnsToSkip(restAreaTurnsToSkip);

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
            $"Bir sonraki {restAreaTurnsToSkip} turunu atlayacaksın.",
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

        specialTileManager.ResolveMoneyEffect(
            player,
            "Tatil Bölgesi",
            "Turizm etkinliğinden küçük bir gelir kazandın.",
            vacationBonusAmount,
            CompleteResolution);
    }

    private void ResolveTravelTile(PlayerGameState player)
    {
        if (boardPath == null)
        {
            boardPath = FindFirstObjectByType<BoardPath>();
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

        if (travelInfoText != null)
        {
            travelInfoText.text =
                "SEYAHAT MERKEZİ\n\n" +
                "En yakın Etkinlik karesine gitmek ister misin?\n" +
                $"Hedef: {targetTile.DisplayName}\n\n" +
                "Başlangıçtan geçersen +200 ₵ kazanırsın.";
        }

        if (travelPanel != null)
        {
            travelPanel.SetActive(true);
        }
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

        if (travelPanel != null)
        {
            travelPanel.SetActive(false);
        }

        pendingPlayer = null;
        pendingTile = null;

        ClearPendingTravelState();
        RefreshBalancesText();

        Action callback = resolutionCompleted;
        resolutionCompleted = null;

        callback?.Invoke();
    }

    private void ClearPendingTravelState()
    {
        pendingTravelPlayer = null;
        pendingTravelPawn = null;
        pendingTravelTargetIndex = -1;
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
            if (player == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(
                $"{player.DisplayName}: {player.CurrentMoney} ₵");
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
