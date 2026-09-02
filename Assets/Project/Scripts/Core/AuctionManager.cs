using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AuctionManager : MonoBehaviour
{
    [Header("Turn Order")]
    [SerializeField]
    private TurnManager turnManager;

    [Header("Board")]
    [SerializeField]
    private BoardPath boardPath;

    [SerializeField]
    private BoardEconomyProfile economyProfile;

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

    private List<PlayerGameState> auctionPlayers;
    private bool[] eligibleBidders;

    private int currentBidderIndex = -1;
    private int highestBidderIndex = -1;
    private int currentBid;

    private bool isAuctionActive;

    private Action resolutionCompleted;
    private Coroutine completionCoroutine;

    // Phase 5E online auction presentation/authority.
    private bool onlineDecisionConfigured;
    private readonly HashSet<int> onlineLocallyControlledHumanSlots =
        new HashSet<int>();
    private bool remotePresentation;
    private bool remoteRequestPending;
    private PlayerGameState remoteCurrentBidder;
    private PlayerGameState remoteHighestBidder;
    private int remoteCurrentBid;
    private int remoteMinimumBid;
    private int remoteSmallBidStep;
    private int remoteLargeBidStep;

    public event Action<int, string> RemoteAuctionDecisionRequested;

    public bool IsAuctionActive =>
        remotePresentation || isAuctionActive;

    public BoardTile AuctionProperty =>
        auctionProperty;

    public bool IsRemotePresentation =>
        remotePresentation;

    public int CurrentBid =>
        remotePresentation
            ? remoteCurrentBid
            : currentBid;

    public int MinimumBid =>
        remotePresentation
            ? remoteMinimumBid
            : minimumBid;

    public int SmallBidStep =>
        remotePresentation
            ? remoteSmallBidStep
            : smallBidStep;

    public int LargeBidStep =>
        remotePresentation
            ? remoteLargeBidStep
            : largeBidStep;

    public PlayerGameState CurrentBidder
    {
        get
        {
            if (remotePresentation)
            {
                return remoteCurrentBidder;
            }

            return IsValidPlayerIndex(
                       currentBidderIndex)
                ? GetPlayer(currentBidderIndex)
                : null;
        }
    }

    public PlayerGameState HighestBidder
    {
        get
        {
            if (remotePresentation)
            {
                return remoteHighestBidder;
            }

            return IsValidPlayerIndex(
                       highestBidderIndex)
                ? GetPlayer(highestBidderIndex)
                : null;
        }
    }

    public void ConfigureOnlineDecisionAuthority(
        IEnumerable<int> locallyControlledHumanSlots)
    {
        onlineDecisionConfigured = true;
        onlineLocallyControlledHumanSlots.Clear();

        if (locallyControlledHumanSlots == null)
        {
            return;
        }

        foreach (int slotIndex in locallyControlledHumanSlots)
        {
            if (slotIndex >= 0 && slotIndex < 4)
            {
                onlineLocallyControlledHumanSlots.Add(slotIndex);
            }
        }
    }

    public int NextSmallBidAmount =>
        Mathf.Max(
            MinimumBid,
            CurrentBid + SmallBidStep);

    public int NextLargeBidAmount =>
        Mathf.Max(
            MinimumBid,
            CurrentBid + LargeBidStep);

    public bool IsCurrentBidder(
        PlayerGameState player)
    {
        PlayerGameState bidder =
            CurrentBidder;

        return player != null &&
               bidder != null &&
               (bidder == player ||
                bidder.PlayerSlotIndex ==
                player.PlayerSlotIndex);
    }

    private void Start()
    {
        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;

        if (auctionPanel != null)
        {
            auctionPanel.SetActive(false);
        }

        EnsureTurnManager();
        ApplyEconomyProfileRules();
    }

    private void OnDestroy()
    {
        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;
    }

    private void HandleLanguageChanged()
    {
        if (auctionPanel != null &&
            auctionPanel.activeSelf &&
            IsAuctionActive)
        {
            RefreshAuctionUI();
        }
    }

    public void BeginAuction(
        PlayerGameState startingPlayer,
        Action onResolutionCompleted)
    {
        ApplyEconomyProfileRules();
        EnsureBoardPath();

        BoardTile randomProperty =
            SelectRandomUnownedCity();

        BeginAuctionInternal(
            startingPlayer,
            randomProperty,
            includeStartingPlayer: true,
            onResolutionCompleted,
            "Auction tile");
    }

    public void BeginAuctionForProperty(
        PlayerGameState decliningPlayer,
        BoardTile property,
        Action onResolutionCompleted)
    {
        ApplyEconomyProfileRules();

        BeginAuctionInternal(
            decliningPlayer,
            property,
            includeStartingPlayer: false,
            onResolutionCompleted,
            "Declined purchase");
    }

    public bool TryResolveBotAction(
        PlayerGameState player,
        BotAuctionAction action)
    {
        if (!isAuctionActive ||
            !IsCurrentBidder(player) ||
            !IsBotPlayer(player))
        {
            return false;
        }

        if (action == BotAuctionAction.BidLarge)
        {
            PlaceLargeBid();
            return true;
        }

        if (action == BotAuctionAction.BidSmall)
        {
            PlaceSmallBid();
            return true;
        }

        PassCurrentBidder();
        return true;
    }

    public void PlaceSmallBid()
    {
        if (TrySubmitOnlineRemoteAuctionAction(
                "bid_small"))
        {
            return;
        }

        PlaceBid(smallBidStep);
    }

    public void PlaceLargeBid()
    {
        if (TrySubmitOnlineRemoteAuctionAction(
                "bid_large"))
        {
            return;
        }

        PlaceBid(largeBidStep);
    }

    public void PassCurrentBidder()
    {
        if (TrySubmitOnlineRemoteAuctionAction(
                "pass"))
        {
            return;
        }

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
                AtlasBoardL.T(
                    "auction.no_bids"));

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

    public void ShowOnlineRemoteAuctionState(
        BoardTile property,
        PlayerGameState currentBidder,
        PlayerGameState highestBidder,
        int authoritativeCurrentBid,
        int authoritativeMinimumBid,
        int authoritativeSmallBidStep,
        int authoritativeLargeBidStep)
    {
        if (property == null ||
            currentBidder == null)
        {
            ClearOnlineRemoteAuctionState();
            return;
        }

        bool samePresentation =
            remotePresentation &&
            auctionProperty != null &&
            auctionProperty.TileIndex == property.TileIndex &&
            remoteCurrentBidder != null &&
            remoteCurrentBidder.PlayerSlotIndex ==
                currentBidder.PlayerSlotIndex &&
            remoteCurrentBid == authoritativeCurrentBid;

        remotePresentation = true;

        if (!samePresentation)
        {
            remoteRequestPending = false;
        }

        auctionProperty = property;
        remoteCurrentBidder = currentBidder;
        remoteHighestBidder = highestBidder;
        remoteCurrentBid =
            Mathf.Max(0, authoritativeCurrentBid);
        remoteMinimumBid =
            Mathf.Max(1, authoritativeMinimumBid);
        remoteSmallBidStep =
            Mathf.Max(1, authoritativeSmallBidStep);
        remoteLargeBidStep =
            Mathf.Max(1, authoritativeLargeBidStep);

        if (auctionPanel != null)
        {
            auctionPanel.SetActive(true);
        }

        RefreshAuctionUI();
    }

    public void ClearOnlineRemoteAuctionState()
    {
        if (!remotePresentation)
        {
            return;
        }

        remotePresentation = false;
        remoteRequestPending = false;
        remoteCurrentBidder = null;
        remoteHighestBidder = null;
        remoteCurrentBid = 0;
        remoteMinimumBid = 0;
        remoteSmallBidStep = 0;
        remoteLargeBidStep = 0;
        auctionProperty = null;

        if (auctionPanel != null)
        {
            auctionPanel.SetActive(false);
        }

        if (bidSmallButton != null)
        {
            bidSmallButton.interactable = false;
        }

        if (bidLargeButton != null)
        {
            bidLargeButton.interactable = false;
        }

        if (passButton != null)
        {
            passButton.interactable = false;
        }
    }

    public void NotifyOnlineRemoteAuctionSubmitFailed()
    {
        if (!remotePresentation)
        {
            return;
        }

        remoteRequestPending = false;
        RefreshButtonAvailability();
    }

    private bool TrySubmitOnlineRemoteAuctionAction(
        string action)
    {
        if (!remotePresentation)
        {
            return false;
        }

        if (remoteRequestPending ||
            remoteCurrentBidder == null)
        {
            return true;
        }

        if (!onlineLocallyControlledHumanSlots.Contains(
                remoteCurrentBidder.PlayerSlotIndex))
        {
            return true;
        }

        Action<int, string> callback =
            RemoteAuctionDecisionRequested;

        if (callback == null)
        {
            Debug.LogWarning(
                "Remote Auction action has no online subscriber.",
                this);
            return true;
        }

        remoteRequestPending = true;
        RefreshButtonAvailability();

        callback.Invoke(
            remoteCurrentBidder.PlayerSlotIndex,
            action ?? string.Empty);

        return true;
    }

    private void BeginAuctionInternal(
        PlayerGameState referencePlayer,
        BoardTile property,
        bool includeStartingPlayer,
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

        EnsureTurnManager();

        if (turnManager == null)
        {
            Debug.LogError(
                "AuctionManager requires a TurnManager reference.",
                this);

            CloseAuctionAndComplete();
            return;
        }

        auctionPlayers =
            turnManager.GetPlayersInTurnOrderFrom(
                referencePlayer,
                includeStartingPlayer);

        if (auctionPlayers == null ||
            auctionPlayers.Count == 0)
        {
            Debug.Log(
                "Auction skipped because there are no eligible " +
                "participants in turn order.",
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
            FindFirstEligibleBidder(0);

        if (currentBidderIndex < 0)
        {
            Debug.Log(
                "Auction skipped because no participant can " +
                "afford the minimum bid.",
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
            $"[Slot {firstBidder.PlayerSlotIndex}]. " +
            $"Participant order: {BuildParticipantOrderText()}.",
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
                    AtlasBoardL.T(
                        "auction.insufficient_balance",
                        AtlasBoardL.PlayerName(
                            bidder),
                        proposedBid,
                        bidder.CurrentMoney);
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
                AtlasBoardL.T(
                    "auction.no_valid_bid"));

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
                AtlasBoardL.T(
                    "auction.winner_cannot_pay"));

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
                AtlasBoardL.T(
                    "auction.ownership_failed"));

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
            AtlasBoardL.T(
                "auction.winner_result",
                AtlasBoardL.PlayerName(
                    winner),
                auctionProperty.DisplayName,
                currentBid));
    }

    private void PrepareEligibleBidders()
    {
        int playerCount =
            auctionPlayers != null
                ? auctionPlayers.Count
                : 0;

        eligibleBidders =
            new bool[playerCount];

        for (int index = 0;
             index < playerCount;
             index++)
        {
            PlayerGameState player =
                auctionPlayers[index];

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
        if (remotePresentation)
        {
            RefreshRemoteAuctionUI();
            return;
        }

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
                ? AtlasBoardL.PlayerName(
                    GetPlayer(
                        highestBidderIndex))
                : AtlasBoardL.T(
                    "auction.none");

        if (auctionTitleText != null)
        {
            auctionTitleText.text =
                AtlasBoardL.T(
                    "auction.title");
        }

        if (auctionPropertyText != null)
        {
            auctionPropertyText.text =
                AtlasBoardL.T(
                    "auction.property_info",
                    auctionProperty.DisplayName,
                    auctionProperty.PurchasePrice,
                    auctionProperty.BaseRent);
        }

        if (auctionStatusText != null)
        {
            string bidderSuffix =
                IsBotPlayer(currentBidder)
                    ? AtlasBoardL.T(
                        "common.bot_suffix")
                    : string.Empty;

            auctionStatusText.text =
                AtlasBoardL.T(
                    "auction.status",
                    currentBid,
                    highestBidderName,
                    AtlasBoardL.PlayerName(
                        currentBidder),
                    bidderSuffix);
        }

        RefreshButtonAvailability();
    }

    private void RefreshButtonAvailability()
    {
        if (remotePresentation)
        {
            PlayerGameState remoteBidder =
                remoteCurrentBidder;

            bool remoteHumanCanControl =
                remoteBidder != null &&
                !remoteRequestPending &&
                !IsBotPlayer(remoteBidder) &&
                onlineLocallyControlledHumanSlots.Contains(
                    remoteBidder.PlayerSlotIndex);

            int remoteNextSmallBid =
                Mathf.Max(
                    Mathf.Max(1, remoteMinimumBid),
                    remoteCurrentBid +
                    Mathf.Max(1, remoteSmallBidStep));

            int remoteNextLargeBid =
                Mathf.Max(
                    Mathf.Max(1, remoteMinimumBid),
                    remoteCurrentBid +
                    Mathf.Max(1, remoteLargeBidStep));

            if (bidSmallButton != null)
            {
                bidSmallButton.interactable =
                    remoteHumanCanControl &&
                    remoteBidder.CurrentMoney >= remoteNextSmallBid;
            }

            if (bidLargeButton != null)
            {
                bidLargeButton.interactable =
                    remoteHumanCanControl &&
                    remoteBidder.CurrentMoney >= remoteNextLargeBid;
            }

            if (passButton != null)
            {
                passButton.interactable =
                    remoteHumanCanControl;
            }

            return;
        }

        PlayerGameState bidder =
            GetPlayer(currentBidderIndex);

        bool validBidder =
            isAuctionActive &&
            bidder != null;

        bool humanCanControl =
            validBidder &&
            !IsBotPlayer(bidder) &&
            (!onlineDecisionConfigured ||
             onlineLocallyControlledHumanSlots.Contains(
                 bidder.PlayerSlotIndex));

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
                humanCanControl &&
                bidder.CurrentMoney >= nextSmallBid;
        }

        if (bidLargeButton != null)
        {
            bidLargeButton.interactable =
                humanCanControl &&
                bidder.CurrentMoney >= nextLargeBid;
        }

        if (passButton != null)
        {
            passButton.interactable =
                humanCanControl;
        }
    }

    private void RefreshRemoteAuctionUI()
    {
        if (!remotePresentation ||
            auctionProperty == null ||
            remoteCurrentBidder == null)
        {
            return;
        }

        string highestBidderName =
            remoteHighestBidder != null
                ? AtlasBoardL.PlayerName(
                    remoteHighestBidder)
                : AtlasBoardL.T(
                    "auction.none");

        if (auctionTitleText != null)
        {
            auctionTitleText.text =
                AtlasBoardL.T(
                    "auction.title");
        }

        if (auctionPropertyText != null)
        {
            auctionPropertyText.text =
                AtlasBoardL.T(
                    "auction.property_info",
                    auctionProperty.DisplayName,
                    auctionProperty.PurchasePrice,
                    auctionProperty.BaseRent);
        }

        if (auctionStatusText != null)
        {
            string bidderSuffix =
                IsBotPlayer(remoteCurrentBidder)
                    ? AtlasBoardL.T(
                        "common.bot_suffix")
                    : string.Empty;

            auctionStatusText.text =
                AtlasBoardL.T(
                    "auction.status",
                    remoteCurrentBid,
                    highestBidderName,
                    AtlasBoardL.PlayerName(
                        remoteCurrentBidder),
                    bidderSuffix);
        }

        RefreshButtonAvailability();
    }

    private void FinishAuctionWithMessage(
        string message)
    {
        // The auction result may remain visible for a short delay,
        // but gameplay actions must stop immediately. Without this,
        // bot controllers can submit another bid/pass during the
        // result-display window and try to award the same property
        // a second time.
        isAuctionActive = false;

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
        remotePresentation = false;
        remoteRequestPending = false;
        remoteCurrentBidder = null;
        remoteHighestBidder = null;
        remoteCurrentBid = 0;
        remoteMinimumBid = 0;
        remoteSmallBidStep = 0;
        remoteLargeBidStep = 0;

        auctionProperty = null;
        auctionPlayers = null;
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

        return auctionPlayers[playerIndex];
    }

    private bool IsValidPlayerIndex(
        int playerIndex)
    {
        return auctionPlayers != null &&
               playerIndex >= 0 &&
               playerIndex < auctionPlayers.Count;
    }

    private int WrapPlayerIndex(
        int playerIndex)
    {
        if (auctionPlayers == null ||
            auctionPlayers.Count == 0)
        {
            return 0;
        }

        return
            ((playerIndex % auctionPlayers.Count) +
             auctionPlayers.Count) %
            auctionPlayers.Count;
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

    private void ApplyEconomyProfileRules()
    {
        BoardEconomyProfile profile =
            ResolveEconomyProfile();

        if (profile == null)
        {
            return;
        }

        minimumBid =
            Mathf.Max(
                1,
                profile.AuctionMinimumBid);

        smallBidStep =
            Mathf.Max(
                1,
                profile.AuctionSmallBidStep);

        largeBidStep =
            Mathf.Max(
                1,
                profile.AuctionLargeBidStep);
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

    private void EnsureTurnManager()
    {
        if (turnManager == null)
        {
            turnManager =
                FindAnyObjectByType<TurnManager>();
        }
    }

    private void EnsureBoardPath()
    {
        if (boardPath == null)
        {
            boardPath =
                FindAnyObjectByType<BoardPath>();
        }
    }

    private string BuildParticipantOrderText()
    {
        if (auctionPlayers == null ||
            auctionPlayers.Count == 0)
        {
            return "none";
        }

        List<string> names =
            new List<string>();

        foreach (PlayerGameState player in auctionPlayers)
        {
            names.Add(
                $"{player.DisplayName} " +
                $"[Slot {player.PlayerSlotIndex}]");
        }

        return string.Join(" → ", names);
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
    public void ResetForNewMatchSession()
    {
        StopAllCoroutines();
        completionCoroutine = null;
        isAuctionActive = false;
        resolutionCompleted = null;
        auctionProperty = null;
        auctionPlayers = null;
        eligibleBidders = null;
        currentBidderIndex = -1;
        highestBidderIndex = -1;
        currentBid = 0;
        remotePresentation = false;
        remoteRequestPending = false;
        remoteCurrentBidder = null;
        remoteHighestBidder = null;
        remoteCurrentBid = 0;
        remoteMinimumBid = 0;
        remoteSmallBidStep = 0;
        remoteLargeBidStep = 0;

        if (auctionPanel != null) auctionPanel.SetActive(false);
    }

}
