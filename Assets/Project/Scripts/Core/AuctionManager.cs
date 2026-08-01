using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AuctionManager : MonoBehaviour
{
    [Header("Players")]
    [SerializeField]
    private PlayerGameState[] playerStates;

    [Header("Board")]
    [SerializeField]
    private BoardPath boardPath;

    [Header("Auction UI")]
    [SerializeField]
    private GameObject auctionPanel;

    [SerializeField]
    private TMP_Text auctionTitleText;

    [SerializeField]
    private TMP_Text auctionPropertyText;

    [SerializeField]
    private TMP_Text auctionStatusText;

    [SerializeField]
    private Button bidSmallButton;

    [SerializeField]
    private Button bidLargeButton;

    [SerializeField]
    private Button passButton;

    [Header("Auction Rules")]
    [SerializeField, Min(1)]
    private int minimumBid = 10;

    [SerializeField, Min(1)]
    private int smallBidStep = 10;

    [SerializeField, Min(1)]
    private int largeBidStep = 50;

    [SerializeField, Min(0f)]
    private float resultDisplayDuration = 1.25f;

    private BoardTile auctionProperty;
    private bool[] eligibleBidders;

    private int currentBidderIndex = -1;
    private int highestBidderIndex = -1;
    private int currentBid;

    private bool isAuctionActive;

    private Action resolutionCompleted;
    private Coroutine completionCoroutine;

    private void Start()
    {
        if (auctionPanel != null)
        {
            auctionPanel.SetActive(false);
        }
    }

    public void BeginAuction(
        PlayerGameState startingPlayer,
        Action onResolutionCompleted)
    {
        EnsureBoardPath();

        BoardTile randomProperty =
            SelectRandomUnownedCity();

        BeginAuctionInternal(
            startingPlayer,
            randomProperty,
            onResolutionCompleted,
            "Auction tile");
    }

    public void BeginAuctionForProperty(
        PlayerGameState startingPlayer,
        BoardTile property,
        Action onResolutionCompleted)
    {
        BeginAuctionInternal(
            startingPlayer,
            property,
            onResolutionCompleted,
            "Declined purchase");
    }

    public void PlaceSmallBid()
    {
        PlaceBid(smallBidStep);
    }

    public void PlaceLargeBid()
    {
        PlaceBid(largeBidStep);
    }

    public void PassCurrentBidder()
    {
        if (!isAuctionActive ||
            !IsValidPlayerIndex(currentBidderIndex))
        {
            return;
        }

        PlayerGameState passingPlayer =
            GetPlayer(currentBidderIndex);

        eligibleBidders[currentBidderIndex] =
            false;

        Debug.Log(
            $"{passingPlayer.DisplayName} passed the auction " +
            $"for {auctionProperty.DisplayName}.",
            this);

        if (highestBidderIndex >= 0)
        {
            int nextBidder =
                FindNextBidderWhoCanRaise(
                    currentBidderIndex);

            if (nextBidder < 0)
            {
                AwardPropertyToHighestBidder();
                return;
            }

            currentBidderIndex =
                nextBidder;

            RefreshAuctionUI();
            return;
        }

        int nextWithoutBid =
            FindNextEligibleBidder(
                currentBidderIndex,
                -1);

        if (nextWithoutBid < 0)
        {
            FinishAuctionWithMessage(
                "Kimse teklif vermedi.\n" +
                "Şehir satılmadı.");

            Debug.Log(
                $"Auction ended without a sale: " +
                $"{auctionProperty.DisplayName}.",
                this);

            return;
        }

        currentBidderIndex =
            nextWithoutBid;

        RefreshAuctionUI();
    }

    private void BeginAuctionInternal(
        PlayerGameState startingPlayer,
        BoardTile property,
        Action onResolutionCompleted,
        string source)
    {
        if (isAuctionActive)
        {
            Debug.LogWarning(
                "An auction is already active.",
                this);

            onResolutionCompleted?.Invoke();
            return;
        }

        resolutionCompleted =
            onResolutionCompleted;

        if (!IsValidAuctionProperty(property))
        {
            Debug.Log(
                "Auction skipped because the selected property " +
                "is missing, owned, or not purchasable.",
                this);

            CloseAuctionAndComplete();
            return;
        }

        int startingBidderArrayIndex =
            FindPlayerArrayIndex(startingPlayer);

        if (startingBidderArrayIndex < 0)
        {
            Debug.LogWarning(
                "The auction starting player could not be found " +
                "in Player States.",
                this);

            CloseAuctionAndComplete();
            return;
        }

        auctionProperty =
            property;

        PrepareEligibleBidders();

        currentBid = 0;
        highestBidderIndex = -1;

        currentBidderIndex =
            FindFirstEligibleBidder(
                startingBidderArrayIndex);

        if (currentBidderIndex < 0)
        {
            Debug.Log(
                "Auction skipped because no player can afford " +
                "the minimum bid.",
                this);

            CloseAuctionAndComplete();
            return;
        }

        isAuctionActive = true;

        if (auctionPanel != null)
        {
            auctionPanel.SetActive(true);
        }

        RefreshAuctionUI();

        PlayerGameState firstBidder =
            GetPlayer(currentBidderIndex);

        Debug.Log(
            $"Auction started for {auctionProperty.DisplayName}. " +
            $"Source: {source}. Starting bidder: " +
            $"{firstBidder.DisplayName} " +
            $"[Slot {firstBidder.PlayerSlotIndex}].",
            this);
    }

    private void PlaceBid(int bidIncrease)
    {
        if (!isAuctionActive ||
            !IsValidPlayerIndex(currentBidderIndex))
        {
            return;
        }

        PlayerGameState bidder =
            GetPlayer(currentBidderIndex);

        int proposedBid =
            Mathf.Max(
                minimumBid,
                currentBid + bidIncrease);

        if (bidder.CurrentMoney < proposedBid)
        {
            if (auctionStatusText != null)
            {
                auctionStatusText.text =
                    $"{bidder.DisplayName} için yetersiz bakiye.\n" +
                    $"Gerekli: {proposedBid} ₵ | " +
                    $"Mevcut: {bidder.CurrentMoney} ₵";
            }

            RefreshButtonAvailability();
            return;
        }

        currentBid =
            proposedBid;

        highestBidderIndex =
            currentBidderIndex;

        Debug.Log(
            $"{bidder.DisplayName} bid {currentBid} for " +
            $"{auctionProperty.DisplayName}.",
            this);

        int nextBidder =
            FindNextBidderWhoCanRaise(
                currentBidderIndex);

        if (nextBidder < 0)
        {
            AwardPropertyToHighestBidder();
            return;
        }

        currentBidderIndex =
            nextBidder;

        RefreshAuctionUI();
    }

    private void AwardPropertyToHighestBidder()
    {
        if (!IsValidPlayerIndex(highestBidderIndex) ||
            currentBid <= 0 ||
            auctionProperty == null)
        {
            FinishAuctionWithMessage(
                "Geçerli teklif bulunamadı.\n" +
                "Şehir satılmadı.");

            return;
        }

        PlayerGameState winner =
            GetPlayer(highestBidderIndex);

        if (!winner.TrySpend(currentBid))
        {
            Debug.LogWarning(
                $"{winner.DisplayName} could not pay the " +
                $"winning bid of {currentBid}.",
                this);

            FinishAuctionWithMessage(
                "Kazanan teklif ödenemedi.\n" +
                "Şehir satılmadı.");

            return;
        }

        bool ownershipAssigned =
            auctionProperty.TrySetOwner(
                winner.PlayerSlotIndex);

        if (!ownershipAssigned)
        {
            winner.AddMoney(currentBid);

            Debug.LogWarning(
                "Auction ownership could not be assigned. " +
                "The winning bid was refunded.",
                this);

            FinishAuctionWithMessage(
                "Mülkiyet aktarılamadı.\n" +
                "Teklif iade edildi.");

            return;
        }

        Material ownerMaterial =
            winner.OwnershipMaterial;

        if (ownerMaterial == null)
        {
            Debug.LogError(
                $"{winner.DisplayName} does not have an " +
                "ownership material.",
                winner);
        }
        else
        {
            auctionProperty.ApplyOwnerMaterial(
                ownerMaterial);
        }

        Debug.Log(
            $"{winner.DisplayName} " +
            $"[Slot {winner.PlayerSlotIndex}, " +
            $"Profile {GetProfileId(winner)}] won " +
            $"{auctionProperty.DisplayName} for {currentBid}.",
            this);

        FinishAuctionWithMessage(
            $"{winner.DisplayName} kazandı!\n" +
            $"{auctionProperty.DisplayName}\n" +
            $"Kazanan teklif: {currentBid} ₵");
    }

    private void PrepareEligibleBidders()
    {
        int playerCount =
            playerStates != null
                ? playerStates.Length
                : 0;

        eligibleBidders =
            new bool[playerCount];

        for (int index = 0;
             index < playerCount;
             index++)
        {
            PlayerGameState player =
                playerStates[index];

            eligibleBidders[index] =
                player != null &&
                player.CurrentMoney >= minimumBid;
        }
    }

    private BoardTile SelectRandomUnownedCity()
    {
        if (boardPath == null)
        {
            return null;
        }

        List<BoardTile> candidates =
            new List<BoardTile>();

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (IsValidAuctionProperty(tile))
            {
                candidates.Add(tile);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        int randomIndex =
            UnityEngine.Random.Range(
                0,
                candidates.Count);

        return candidates[randomIndex];
    }

    private bool IsValidAuctionProperty(
        BoardTile property)
    {
        return property != null &&
               property.TileType == TileType.City &&
               property.Purchasable &&
               !property.IsOwned;
    }

    private int FindPlayerArrayIndex(
        PlayerGameState player)
    {
        if (playerStates == null ||
            player == null)
        {
            return -1;
        }

        for (int index = 0;
             index < playerStates.Length;
             index++)
        {
            PlayerGameState candidate =
                playerStates[index];

            if (candidate == player)
            {
                return index;
            }

            if (candidate != null &&
                candidate.PlayerSlotIndex ==
                player.PlayerSlotIndex)
            {
                return index;
            }
        }

        return -1;
    }

    private int FindFirstEligibleBidder(
        int preferredPlayerIndex)
    {
        if (eligibleBidders == null ||
            eligibleBidders.Length == 0)
        {
            return -1;
        }

        int wrappedPreferred =
            WrapPlayerIndex(
                preferredPlayerIndex);

        if (eligibleBidders[wrappedPreferred])
        {
            return wrappedPreferred;
        }

        return FindNextEligibleBidder(
            wrappedPreferred,
            -1);
    }

    private int FindNextBidderWhoCanRaise(
        int afterPlayerIndex)
    {
        if (eligibleBidders == null)
        {
            return -1;
        }

        int searchAfter =
            afterPlayerIndex;

        for (int attempt = 0;
             attempt < eligibleBidders.Length;
             attempt++)
        {
            int candidateIndex =
                FindNextEligibleBidder(
                    searchAfter,
                    highestBidderIndex);

            if (candidateIndex < 0)
            {
                return -1;
            }

            PlayerGameState candidate =
                GetPlayer(candidateIndex);

            int requiredBid =
                Mathf.Max(
                    minimumBid,
                    currentBid + smallBidStep);

            if (candidate != null &&
                candidate.CurrentMoney >= requiredBid)
            {
                return candidateIndex;
            }

            eligibleBidders[candidateIndex] =
                false;

            Debug.Log(
                $"{candidate?.DisplayName ?? "Player"} left the " +
                $"auction because they cannot afford {requiredBid}.",
                this);

            searchAfter =
                candidateIndex;
        }

        return -1;
    }

    private int FindNextEligibleBidder(
        int afterPlayerIndex,
        int excludedPlayerIndex)
    {
        if (eligibleBidders == null ||
            eligibleBidders.Length == 0)
        {
            return -1;
        }

        for (int offset = 1;
             offset <= eligibleBidders.Length;
             offset++)
        {
            int candidateIndex =
                (afterPlayerIndex + offset) %
                eligibleBidders.Length;

            if (candidateIndex == excludedPlayerIndex)
            {
                continue;
            }

            if (eligibleBidders[candidateIndex] &&
                GetPlayer(candidateIndex) != null)
            {
                return candidateIndex;
            }
        }

        return -1;
    }

    private void RefreshAuctionUI()
    {
        if (!isAuctionActive ||
            auctionProperty == null ||
            !IsValidPlayerIndex(currentBidderIndex))
        {
            return;
        }

        PlayerGameState currentBidder =
            GetPlayer(currentBidderIndex);

        string highestBidderName =
            highestBidderIndex >= 0
                ? GetPlayer(highestBidderIndex).DisplayName
                : "Yok";

        if (auctionTitleText != null)
        {
            auctionTitleText.text =
                "AÇIK ARTIRMA";
        }

        if (auctionPropertyText != null)
        {
            auctionPropertyText.text =
                $"{auctionProperty.DisplayName}\n" +
                $"Liste Değeri: " +
                $"{auctionProperty.PurchasePrice} ₵ | " +
                $"Kira: {auctionProperty.BaseRent} ₵";
        }

        if (auctionStatusText != null)
        {
            auctionStatusText.text =
                $"Mevcut teklif: {currentBid} ₵\n" +
                $"En yüksek: {highestBidderName}\n" +
                $"Sıra: {currentBidder.DisplayName}";
        }

        RefreshButtonAvailability();
    }

    private void RefreshButtonAvailability()
    {
        PlayerGameState bidder =
            GetPlayer(currentBidderIndex);

        bool validBidder =
            isAuctionActive &&
            bidder != null;

        int nextSmallBid =
            Mathf.Max(
                minimumBid,
                currentBid + smallBidStep);

        int nextLargeBid =
            Mathf.Max(
                minimumBid,
                currentBid + largeBidStep);

        if (bidSmallButton != null)
        {
            bidSmallButton.interactable =
                validBidder &&
                bidder.CurrentMoney >= nextSmallBid;
        }

        if (bidLargeButton != null)
        {
            bidLargeButton.interactable =
                validBidder &&
                bidder.CurrentMoney >= nextLargeBid;
        }

        if (passButton != null)
        {
            passButton.interactable =
                validBidder;
        }
    }

    private void FinishAuctionWithMessage(
        string message)
    {
        if (auctionStatusText != null)
        {
            auctionStatusText.text =
                message;
        }

        if (bidSmallButton != null)
        {
            bidSmallButton.interactable =
                false;
        }

        if (bidLargeButton != null)
        {
            bidLargeButton.interactable =
                false;
        }

        if (passButton != null)
        {
            passButton.interactable =
                false;
        }

        if (completionCoroutine != null)
        {
            StopCoroutine(completionCoroutine);
        }

        completionCoroutine =
            StartCoroutine(CompleteAfterDelay());
    }

    private IEnumerator CompleteAfterDelay()
    {
        if (resultDisplayDuration > 0f)
        {
            yield return new WaitForSeconds(
                resultDisplayDuration);
        }

        completionCoroutine = null;

        CloseAuctionAndComplete();
    }

    private void CloseAuctionAndComplete()
    {
        if (auctionPanel != null)
        {
            auctionPanel.SetActive(false);
        }

        isAuctionActive = false;

        auctionProperty = null;
        eligibleBidders = null;

        currentBidderIndex = -1;
        highestBidderIndex = -1;
        currentBid = 0;

        Action callback =
            resolutionCompleted;

        resolutionCompleted = null;

        callback?.Invoke();
    }

    private PlayerGameState GetPlayer(
        int playerIndex)
    {
        if (!IsValidPlayerIndex(playerIndex))
        {
            return null;
        }

        return playerStates[playerIndex];
    }

    private bool IsValidPlayerIndex(
        int playerIndex)
    {
        return playerStates != null &&
               playerIndex >= 0 &&
               playerIndex < playerStates.Length;
    }

    private int WrapPlayerIndex(
        int playerIndex)
    {
        if (playerStates == null ||
            playerStates.Length == 0)
        {
            return 0;
        }

        return
            ((playerIndex % playerStates.Length) +
             playerStates.Length) %
            playerStates.Length;
    }

    private void EnsureBoardPath()
    {
        if (boardPath == null)
        {
            boardPath =
                FindFirstObjectByType<BoardPath>();
        }
    }

    private string GetProfileId(
        PlayerGameState player)
    {
        if (player == null ||
            player.VisualProfile == null)
        {
            return "none";
        }

        return player.VisualProfile.ProfileId;
    }
}
