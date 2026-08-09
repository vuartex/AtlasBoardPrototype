using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EventCardManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private BankruptcyManager bankruptcyManager;

    [SerializeField]
    private BoardPath boardPath;

    [SerializeField]
    private BoardGenerator boardGenerator;

    [Header("Event Deck")]
    [SerializeField]
    private EventDeckDefinition eventDeck;

    [Header("Event Card UI")]
    [SerializeField]
    private GameObject eventPanel;

    [SerializeField]
    private TMP_Text eventTitleText;

    [SerializeField]
    private TMP_Text eventDescriptionText;

    [SerializeField]
    private TMP_Text eventResultText;

    [SerializeField]
    private Button continueButton;

    [Header("Debug")]
    [Tooltip(
        "Optional. When assigned, every Event draw uses this " +
        "card. Clear it for normal weighted draws.")]
    [SerializeField]
    private EventCardDefinition debugForcedCard;

    private Action resolutionCompleted;
    private bool isResolvingEvent;
    private bool effectExecutionCompleted;
    private PlayerGameState currentEventPlayer;
    private EventCardDefinition currentCard;

    public bool IsResolvingEvent =>
        isResolvingEvent;

    public EventDeckDefinition EventDeck =>
        eventDeck;

    public void SetEventDeck(
        EventDeckDefinition deck)
    {
        if (isResolvingEvent)
        {
            Debug.LogWarning(
                "Event deck cannot be changed while an event " +
                "is being resolved.",
                this);

            return;
        }

        eventDeck = deck;
    }

    public bool HasPendingEventFor(
        PlayerGameState player)
    {
        return isResolvingEvent &&
               player != null &&
               currentEventPlayer != null &&
               (currentEventPlayer == player ||
                currentEventPlayer.PlayerSlotIndex ==
                player.PlayerSlotIndex);
    }

    private void Start()
    {
        EnsureReferences();

        if (eventPanel != null)
        {
            eventPanel.SetActive(false);
        }

        if (continueButton != null)
        {
            continueButton.interactable = true;
        }
    }

    public void ResolveRandomEvent(
        PlayerGameState player,
        Action onResolutionCompleted)
    {
        if (isResolvingEvent)
        {
            Debug.LogWarning(
                "An event card is already being resolved.",
                this);

            return;
        }

        resolutionCompleted =
            onResolutionCompleted;

        if (player == null)
        {
            CompleteEvent();
            return;
        }

        EnsureReferences();

        EventCardDefinition selectedCard =
            SelectWeightedRandomCard();

        if (selectedCard == null)
        {
            Debug.LogWarning(
                "No valid event cards are available for the " +
                "active map.",
                this);

            CompleteEvent();
            return;
        }

        isResolvingEvent = true;
        effectExecutionCompleted = false;
        currentEventPlayer = player;
        currentCard = selectedCard;

        if (eventTitleText != null)
        {
            eventTitleText.text =
                selectedCard.Title;
        }

        if (eventDescriptionText != null)
        {
            eventDescriptionText.text =
                selectedCard.Description;
        }

        if (eventResultText != null)
        {
            eventResultText.text =
                "Kart uygulanıyor...";
        }

        if (eventPanel != null)
        {
            eventPanel.SetActive(true);
        }

        RefreshContinueButtonAvailability();

        ApplyCardEffect(
            selectedCard,
            player);
    }

    public bool TryResolveBotContinue(
        PlayerGameState player)
    {
        if (!HasPendingEventFor(player) ||
            !IsBotPlayer(player) ||
            !effectExecutionCompleted)
        {
            return false;
        }

        ContinueAfterEvent();
        return true;
    }

    public void ContinueAfterEvent()
    {
        if (!isResolvingEvent ||
            !effectExecutionCompleted)
        {
            return;
        }

        CompleteEvent();
    }

    private void ApplyCardEffect(
        EventCardDefinition card,
        PlayerGameState player)
    {
        switch (card.EffectType)
        {
            case EventCardEffectType.Money:
                ApplyMoneyEffect(
                    card,
                    player);
                break;

            case EventCardEffectType.SkipTurns:
                ApplySkipTurnEffect(
                    card,
                    player);
                break;

            case EventCardEffectType.MoveForwardSpaces:
                ApplyMoveForwardEffect(
                    card,
                    player);
                break;

            case EventCardEffectType.MoveToNextTileType:
                ApplyMoveToTileTypeEffect(
                    card,
                    player);
                break;

            default:
                FinishEffectExecution(
                    "Etki uygulanmadı.");
                break;
        }
    }

    private void ApplyMoneyEffect(
        EventCardDefinition card,
        PlayerGameState player)
    {
        int requestedChange =
            card.EffectAmount;

        int appliedMoneyChange = 0;
        bool causedBankruptcy = false;
        int transferredProperties = 0;

        if (requestedChange > 0)
        {
            player.AddMoney(
                requestedChange);

            appliedMoneyChange =
                requestedChange;
        }
        else if (requestedChange < 0)
        {
            int requestedLoss =
                Mathf.Abs(
                    requestedChange);

            if (bankruptcyManager != null)
            {
                BankruptcyManager.PaymentResolution
                    result =
                        bankruptcyManager
                            .ResolveMandatoryPayment(
                                player,
                                null,
                                requestedLoss,
                                $"Event card: {card.Title}");

                appliedMoneyChange =
                    -result.AmountPaid;

                causedBankruptcy =
                    result.DebtorBankrupt;

                transferredProperties =
                    result.TransferredPropertyCount;
            }
            else
            {
                int actualLoss =
                    Mathf.Min(
                        requestedLoss,
                        player.CurrentMoney);

                player.TrySpend(
                    actualLoss);

                appliedMoneyChange =
                    -actualLoss;
            }
        }

        string resultText;

        if (causedBankruptcy)
        {
            resultText =
                "İFLAS\n" +
                $"Ödenen: " +
                $"{Mathf.Abs(appliedMoneyChange)} ₵\n" +
                $"Devredilen/boşa çıkan mülk: " +
                $"{transferredProperties}";
        }
        else if (appliedMoneyChange > 0)
        {
            resultText =
                $"+{appliedMoneyChange} ₵";
        }
        else if (appliedMoneyChange < 0)
        {
            resultText =
                $"{appliedMoneyChange} ₵";
        }
        else
        {
            resultText =
                "Para değişmedi";
        }

        Debug.Log(
            $"{player.DisplayName} drew event card " +
            $"'{card.Title}'. Money change: " +
            $"{appliedMoneyChange}. Bankrupt: " +
            $"{causedBankruptcy}.",
            this);

        FinishEffectExecution(
            resultText);
    }

    private void ApplySkipTurnEffect(
        EventCardDefinition card,
        PlayerGameState player)
    {
        int turns =
            Mathf.Max(
                1,
                Mathf.Abs(
                    card.EffectAmount));

        player.AddTurnsToSkip(
            turns);

        Debug.Log(
            $"{player.DisplayName} drew event card " +
            $"'{card.Title}' and will skip " +
            $"{turns} turn(s).",
            this);

        FinishEffectExecution(
            turns == 1
                ? "Sonraki turunu atlayacaksın."
                : $"Sonraki {turns} turunu " +
                  "atlayacaksın.");
    }

    private void ApplyMoveForwardEffect(
        EventCardDefinition card,
        PlayerGameState player)
    {
        int spaces =
            Mathf.Max(
                1,
                Mathf.Abs(
                    card.EffectAmount));

        PlayerPawnMover pawn =
            player.GetComponent<
                PlayerPawnMover>();

        EnsureReferences();

        if (pawn == null ||
            boardPath == null ||
            boardPath.TileCount == 0)
        {
            FinishEffectExecution(
                "Hareket uygulanamadı.");
            return;
        }

        int targetIndex =
            (pawn.CurrentTileIndex +
             spaces) %
            boardPath.TileCount;

        BoardTile targetTile =
            boardPath.GetTile(
                targetIndex);

        if (eventResultText != null)
        {
            eventResultText.text =
                $"{spaces} kare ilerliyorsun" +
                (targetTile != null
                    ? $": {targetTile.DisplayName}"
                    : string.Empty);
        }

        bool started =
            pawn.MoveForwardToTile(
                targetIndex,
                completedPawn =>
                {
                    BoardTile landedTile =
                        completedPawn
                            .GetCurrentTile();

                    string result =
                        landedTile != null
                            ? $"{spaces} kare ilerledin: " +
                              $"{landedTile.DisplayName}"
                            : $"{spaces} kare ilerledin.";

                    Debug.Log(
                        $"{player.DisplayName} drew event card " +
                        $"'{card.Title}' and moved forward " +
                        $"{spaces} spaces.",
                        this);

                    FinishEffectExecution(
                        result);
                });

        if (!started)
        {
            FinishEffectExecution(
                "Hareket uygulanamadı.");
        }
    }

    private void ApplyMoveToTileTypeEffect(
        EventCardDefinition card,
        PlayerGameState player)
    {
        PlayerPawnMover pawn =
            player.GetComponent<
                PlayerPawnMover>();

        EnsureReferences();

        if (pawn == null ||
            boardPath == null ||
            boardPath.TileCount == 0)
        {
            FinishEffectExecution(
                "Hareket uygulanamadı.");
            return;
        }

        int targetIndex =
            FindNextTileIndexOfType(
                pawn.CurrentTileIndex,
                card.TargetTileType);

        if (targetIndex < 0)
        {
            FinishEffectExecution(
                "Uygun hedef bulunamadı.");
            return;
        }

        BoardTile targetTile =
            boardPath.GetTile(
                targetIndex);

        if (eventResultText != null)
        {
            eventResultText.text =
                targetTile != null
                    ? $"{targetTile.DisplayName} konumuna " +
                      "ilerliyorsun."
                    : "Hedef konuma ilerliyorsun.";
        }

        bool started =
            pawn.MoveForwardToTile(
                targetIndex,
                completedPawn =>
                {
                    BoardTile landedTile =
                        completedPawn
                            .GetCurrentTile();

                    string result =
                        landedTile != null
                            ? $"{landedTile.DisplayName} " +
                              "konumuna ilerledin."
                            : "Hedef konuma ilerledin.";

                    Debug.Log(
                        $"{player.DisplayName} drew event card " +
                        $"'{card.Title}' and moved to the next " +
                        $"{card.TargetTileType} tile.",
                        this);

                    FinishEffectExecution(
                        result);
                });

        if (!started)
        {
            FinishEffectExecution(
                "Hareket uygulanamadı.");
        }
    }

    private int FindNextTileIndexOfType(
        int currentIndex,
        TileType targetType)
    {
        if (boardPath == null ||
            boardPath.TileCount <= 1)
        {
            return -1;
        }

        for (int offset = 1;
             offset < boardPath.TileCount;
             offset++)
        {
            int candidateIndex =
                (currentIndex + offset) %
                boardPath.TileCount;

            BoardTile tile =
                boardPath.GetTile(
                    candidateIndex);

            if (tile != null &&
                tile.TileType == targetType)
            {
                return candidateIndex;
            }
        }

        return -1;
    }

    private EventCardDefinition
        SelectWeightedRandomCard()
    {
        if (debugForcedCard != null &&
            debugForcedCard.EnabledCard)
        {
            return debugForcedCard;
        }

        if (eventDeck == null ||
            eventDeck.Cards == null ||
            eventDeck.Cards.Count == 0)
        {
            return null;
        }

        string activeMapId =
            boardGenerator != null &&
            boardGenerator.ActiveMapDefinition != null
                ? boardGenerator
                    .ActiveMapDefinition
                    .MapId
                : string.Empty;

        List<EventCardDefinition> validCards =
            new List<EventCardDefinition>();

        int totalWeight = 0;

        foreach (EventCardDefinition card
                 in eventDeck.Cards)
        {
            if (card == null ||
                !card.IsAvailableForMap(
                    activeMapId))
            {
                continue;
            }

            validCards.Add(card);
            totalWeight += card.Weight;
        }

        if (validCards.Count == 0 ||
            totalWeight <= 0)
        {
            return null;
        }

        int randomValue =
            UnityEngine.Random.Range(
                0,
                totalWeight);

        int cumulativeWeight = 0;

        foreach (EventCardDefinition card
                 in validCards)
        {
            cumulativeWeight +=
                card.Weight;

            if (randomValue <
                cumulativeWeight)
            {
                return card;
            }
        }

        return validCards[
            validCards.Count - 1];
    }

    private void FinishEffectExecution(
        string resultText)
    {
        effectExecutionCompleted = true;

        if (eventResultText != null)
        {
            eventResultText.text =
                resultText;
        }

        RefreshContinueButtonAvailability();
    }

    private void RefreshContinueButtonAvailability()
    {
        if (continueButton == null)
        {
            return;
        }

        continueButton.interactable =
            effectExecutionCompleted &&
            (currentEventPlayer == null ||
             !IsBotPlayer(
                currentEventPlayer));
    }

    private void EnsureReferences()
    {
        if (boardPath == null)
        {
            boardPath =
                FindAnyObjectByType<
                    BoardPath>();
        }

        if (boardGenerator == null)
        {
            boardGenerator =
                FindAnyObjectByType<
                    BoardGenerator>();
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
            player.GetComponent<
                BotPlayerController>();

        return botController != null &&
               botController.BotEnabled;
    }

    private void CompleteEvent()
    {
        if (eventPanel != null)
        {
            eventPanel.SetActive(false);
        }

        isResolvingEvent = false;
        effectExecutionCompleted = false;
        currentEventPlayer = null;
        currentCard = null;

        if (continueButton != null)
        {
            continueButton.interactable =
                true;
        }

        Action callback =
            resolutionCompleted;

        resolutionCompleted = null;

        callback?.Invoke();
    }
}
