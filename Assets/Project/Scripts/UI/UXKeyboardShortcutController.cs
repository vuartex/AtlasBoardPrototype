using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class UXKeyboardShortcutController : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private TileResolutionManager
        tileResolutionManager;

    [SerializeField]
    private TradeManager tradeManager;

    [SerializeField]
    private AuctionManager auctionManager;

    [SerializeField]
    private EventCardManager eventCardManager;

    [SerializeField]
    private SpecialTileManager specialTileManager;

    [Header("Modal State")]
    [SerializeField]
    private GameObject tripleDoublePenaltyPanel;

    [Header("Controls")]
    [SerializeField]
    private bool spaceAndEnterConfirm = true;

    [SerializeField]
    private bool escapeCancels = true;

    [SerializeField]
    private bool tOpensTrade = true;

    [SerializeField]
    private bool shiftSpaceLargeAuctionBid = true;

    public string CurrentHint { get; private set; }

    private void Update()
    {
        RefreshHint();

        if (turnManager == null ||
            !turnManager.IsMatchStarted ||
            IsTypingInInputField())
        {
            return;
        }

        PlayerGameState currentPlayer =
            GetHumanCurrentPlayer();

        if (currentPlayer == null)
        {
            return;
        }

        bool primaryPressed =
            spaceAndEnterConfirm &&
            (WasSpacePressed() ||
             WasEnterPressed());

        if (primaryPressed)
        {
            TryPrimaryAction(
                currentPlayer);

            return;
        }

        if (escapeCancels &&
            WasEscapePressed())
        {
            TrySecondaryAction(
                currentPlayer);

            return;
        }

        if (tOpensTrade &&
            WasTradePressed())
        {
            TryOpenTrade();
        }
    }

    private void TryPrimaryAction(
        PlayerGameState player)
    {
        if (tripleDoublePenaltyPanel != null &&
            tripleDoublePenaltyPanel
                .activeInHierarchy)
        {
            turnManager
                .ContinueTripleDoublePenalty();

            return;
        }

        if (eventCardManager != null &&
            eventCardManager
                .HasPendingEventFor(player))
        {
            eventCardManager
                .ContinueAfterEvent();

            return;
        }

        if (specialTileManager != null &&
            specialTileManager
                .HasPendingSpecialFor(player))
        {
            specialTileManager
                .ContinueAfterSpecialTile();

            return;
        }

        if (tradeManager != null &&
            tradeManager
                .HasPendingOfferFor(player))
        {
            tradeManager.AcceptTradeOffer();
            return;
        }

        if (tileResolutionManager != null &&
            tileResolutionManager
                .HasPendingPurchaseFor(player))
        {
            tileResolutionManager
                .BuyPendingTile();

            return;
        }

        if (tileResolutionManager != null &&
            tileResolutionManager
                .HasPendingTravelFor(player))
        {
            tileResolutionManager
                .TravelToNextEvent();

            return;
        }

        if (tileResolutionManager != null &&
            tileResolutionManager
                .HasPendingDevelopmentFor(player))
        {
            tileResolutionManager
                .DevelopPendingTile();

            return;
        }

        if (auctionManager != null &&
            auctionManager.IsAuctionActive &&
            auctionManager
                .IsCurrentBidder(player))
        {
            if (shiftSpaceLargeAuctionBid &&
                IsShiftHeld())
            {
                auctionManager.PlaceLargeBid();
            }
            else
            {
                auctionManager.PlaceSmallBid();
            }

            return;
        }

        turnManager.TryRequestRoll(
            player);
    }

    private void TrySecondaryAction(
        PlayerGameState player)
    {
        if (tradeManager != null &&
            tradeManager
                .HasPendingOfferFor(player))
        {
            tradeManager.RejectTradeOffer();
            return;
        }

        if (tradeManager != null &&
            !tradeManager.IsTradeClosed)
        {
            tradeManager.CancelTrade();
            return;
        }

        if (tileResolutionManager != null &&
            tileResolutionManager
                .HasPendingPurchaseFor(player))
        {
            tileResolutionManager
                .SkipPendingTile();

            return;
        }

        if (tileResolutionManager != null &&
            tileResolutionManager
                .HasPendingTravelFor(player))
        {
            tileResolutionManager
                .StayOnTravelTile();

            return;
        }

        if (tileResolutionManager != null &&
            tileResolutionManager
                .HasPendingDevelopmentFor(player))
        {
            tileResolutionManager
                .SkipPendingDevelopment();

            return;
        }

        if (auctionManager != null &&
            auctionManager.IsAuctionActive &&
            auctionManager
                .IsCurrentBidder(player))
        {
            auctionManager
                .PassCurrentBidder();
        }
    }

    private void TryOpenTrade()
    {
        if (tradeManager == null ||
            turnManager == null ||
            !turnManager.CanStartManagementAction ||
            !tradeManager.IsTradeClosed)
        {
            return;
        }

        tradeManager.OpenTradePanel();
    }

    private void RefreshHint()
    {
        CurrentHint = string.Empty;

        if (turnManager == null ||
            !turnManager.IsMatchStarted)
        {
            return;
        }

        PlayerGameState player =
            GetHumanCurrentPlayer();

        if (player == null)
        {
            return;
        }

        if (tripleDoublePenaltyPanel != null &&
            tripleDoublePenaltyPanel
                .activeInHierarchy)
        {
            CurrentHint =
                AtlasBoardL.T("hint.continue");

            return;
        }

        if (eventCardManager != null &&
            eventCardManager
                .HasPendingEventFor(player))
        {
            CurrentHint =
                AtlasBoardL.T("hint.continue");

            return;
        }

        if (specialTileManager != null &&
            specialTileManager
                .HasPendingSpecialFor(player))
        {
            CurrentHint =
                AtlasBoardL.T("hint.continue");

            return;
        }

        if (tradeManager != null &&
            tradeManager
                .HasPendingOfferFor(player))
        {
            CurrentHint =
                AtlasBoardL.T("hint.trade_offer");

            return;
        }

        if (tileResolutionManager != null &&
            tileResolutionManager
                .HasPendingPurchaseFor(player))
        {
            CurrentHint =
                AtlasBoardL.T("hint.purchase");

            return;
        }

        if (tileResolutionManager != null &&
            tileResolutionManager
                .HasPendingTravelFor(player))
        {
            CurrentHint =
                AtlasBoardL.T("hint.travel");

            return;
        }

        if (tileResolutionManager != null &&
            tileResolutionManager
                .HasPendingDevelopmentFor(player))
        {
            CurrentHint =
                AtlasBoardL.T("hint.develop");

            return;
        }

        if (auctionManager != null &&
            auctionManager.IsAuctionActive &&
            auctionManager
                .IsCurrentBidder(player))
        {
            CurrentHint =
                AtlasBoardL.T("hint.auction");

            return;
        }

        if (turnManager.CanStartManagementAction &&
            tradeManager != null &&
            tradeManager.IsTradeClosed)
        {
            CurrentHint =
                AtlasBoardL.T("hint.roll_trade");

            return;
        }

        if (turnManager
                .CanPlayerRequestRoll(player))
        {
            CurrentHint =
                AtlasBoardL.T("hint.roll");
        }
    }

    private PlayerGameState
        GetHumanCurrentPlayer()
    {
        if (turnManager == null)
        {
            return null;
        }

        PlayerGameState player =
            turnManager.StartingOrderPlayerState ??
            turnManager.CurrentPlayerState;

        if (player == null ||
            !player.IsParticipating ||
            player.IsBankrupt)
        {
            return null;
        }

        BotPlayerController bot =
            player.GetComponent<
                BotPlayerController>();

        if (bot != null &&
            bot.BotEnabled)
        {
            return null;
        }

        return player;
    }

    private bool IsTypingInInputField()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        GameObject selected =
            EventSystem.current
                .currentSelectedGameObject;

        if (selected == null)
        {
            return false;
        }

        return selected
                   .GetComponent<TMP_InputField>() != null ||
               selected
                   .GetComponent<InputField>() != null;
    }

    private bool WasSpacePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current
                   .spaceKey
                   .wasPressedThisFrame;
#else
        return Input.GetKeyDown(
            KeyCode.Space);
#endif
    }

    private bool WasEnterPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               (Keyboard.current
                    .enterKey
                    .wasPressedThisFrame ||
                Keyboard.current
                    .numpadEnterKey
                    .wasPressedThisFrame);
#else
        return Input.GetKeyDown(
                   KeyCode.Return) ||
               Input.GetKeyDown(
                   KeyCode.KeypadEnter);
#endif
    }

    private bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current
                   .escapeKey
                   .wasPressedThisFrame;
#else
        return Input.GetKeyDown(
            KeyCode.Escape);
#endif
    }

    private bool WasTradePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current
                   .tKey
                   .wasPressedThisFrame;
#else
        return Input.GetKeyDown(
            KeyCode.T);
#endif
    }

    private bool IsShiftHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               (Keyboard.current
                    .leftShiftKey
                    .isPressed ||
                Keyboard.current
                    .rightShiftKey
                    .isPressed);
#else
        return Input.GetKey(
                   KeyCode.LeftShift) ||
               Input.GetKey(
                   KeyCode.RightShift);
#endif
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        TurnManager newTurnManager,
        TileResolutionManager
            newTileResolutionManager,
        TradeManager newTradeManager,
        AuctionManager newAuctionManager,
        EventCardManager newEventCardManager,
        SpecialTileManager newSpecialTileManager,
        GameObject newTripleDoublePenaltyPanel)
    {
        turnManager = newTurnManager;
        tileResolutionManager =
            newTileResolutionManager;
        tradeManager = newTradeManager;
        auctionManager = newAuctionManager;
        eventCardManager =
            newEventCardManager;
        specialTileManager =
            newSpecialTileManager;
        tripleDoublePenaltyPanel =
            newTripleDoublePenaltyPanel;
    }
#endif
}
