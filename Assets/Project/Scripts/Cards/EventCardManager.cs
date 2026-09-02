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

    // Phase 5E online authority/presentation state.
    private bool onlineHostAuthorityMode;
    private readonly HashSet<int> onlineLocallyControlledHumanSlots =
        new HashSet<int>();
    private bool remotePresentation;
    private bool remoteContinueRequestPending;

    private string onlineResultKind = string.Empty;
    private int onlineResultValue0;
    private int onlineResultValue1;
    private int onlineResultValue2;
    private string onlineResultTextValue = string.Empty;

    public event Action<int> RemoteContinueRequested;

    public bool IsResolvingEvent =>
        isResolvingEvent;

    public bool EffectExecutionCompleted =>
        effectExecutionCompleted;

    public PlayerGameState CurrentEventPlayer =>
        currentEventPlayer;

    public EventCardDefinition CurrentCard =>
        currentCard;

    public bool IsRemotePresentation =>
        remotePresentation;

    public string OnlineResultKind =>
        onlineResultKind;

    public int OnlineResultValue0 =>
        onlineResultValue0;

    public int OnlineResultValue1 =>
        onlineResultValue1;

    public int OnlineResultValue2 =>
        onlineResultValue2;

    public string OnlineResultTextValue =>
        onlineResultTextValue;

    public EventDeckDefinition EventDeck =>
        eventDeck;

    public void ConfigureOnlineDecisionAuthority(
        bool hostAuthorityMode,
        IEnumerable<int> locallyControlledHumanSlots)
    {
        onlineHostAuthorityMode =
            hostAuthorityMode;

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
        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;

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

    private void OnDestroy()
    {
        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;
    }

    private void HandleLanguageChanged()
    {
        if (!isResolvingEvent ||
            currentCard == null)
        {
            return;
        }

        if (eventTitleText != null)
        {
            eventTitleText.text =
                currentCard.Title;
        }

        if (eventDescriptionText != null)
        {
            eventDescriptionText.text =
                currentCard.Description;
        }

        if (remotePresentation &&
            eventResultText != null)
        {
            eventResultText.text =
                effectExecutionCompleted
                    ? BuildLocalizedRemoteResult()
                    : AtlasBoardL.T("event.applying");
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

        remotePresentation = false;
        remoteContinueRequestPending = false;
        ClearOnlineResultDescriptor();

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
                AtlasBoardL.T(
                    "event.applying");
        }

        if (eventPanel != null)
        {
            eventPanel.SetActive(
                ShouldShowAuthoritativePanelLocally(player));
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

        if (TrySubmitOnlineRemoteContinue())
        {
            return;
        }

        CompleteEvent();
    }

    public void ShowOnlineRemoteEventDecision(
        PlayerGameState player,
        string cardId,
        bool effectCompleted,
        string resultKind,
        int value0,
        int value1,
        int value2,
        string textValue)
    {
        EventCardDefinition card =
            FindCardById(cardId);

        if (player == null ||
            card == null)
        {
            ClearOnlineRemoteEventDecision();
            return;
        }

        bool samePresentation =
            remotePresentation &&
            currentEventPlayer != null &&
            currentEventPlayer.PlayerSlotIndex ==
                player.PlayerSlotIndex &&
            currentCard != null &&
            string.Equals(
                currentCard.CardId,
                card.CardId,
                StringComparison.Ordinal);

        remotePresentation = true;

        if (!samePresentation)
        {
            remoteContinueRequestPending = false;
        }

        isResolvingEvent = true;
        effectExecutionCompleted = effectCompleted;
        currentEventPlayer = player;
        currentCard = card;
        resolutionCompleted = null;

        onlineResultKind = resultKind ?? string.Empty;
        onlineResultValue0 = value0;
        onlineResultValue1 = value1;
        onlineResultValue2 = value2;
        onlineResultTextValue = textValue ?? string.Empty;

        if (eventTitleText != null)
        {
            eventTitleText.text = card.Title;
        }

        if (eventDescriptionText != null)
        {
            eventDescriptionText.text = card.Description;
        }

        if (eventResultText != null)
        {
            eventResultText.text =
                effectCompleted
                    ? BuildLocalizedRemoteResult()
                    : AtlasBoardL.T("event.applying");
        }

        if (eventPanel != null)
        {
            eventPanel.SetActive(true);
        }

        RefreshContinueButtonAvailability();
    }

    public void ClearOnlineRemoteEventDecision()
    {
        if (!remotePresentation)
        {
            return;
        }

        remotePresentation = false;
        remoteContinueRequestPending = false;
        isResolvingEvent = false;
        effectExecutionCompleted = false;
        currentEventPlayer = null;
        currentCard = null;
        ClearOnlineResultDescriptor();
        resolutionCompleted = null;

        if (eventPanel != null)
        {
            eventPanel.SetActive(false);
        }

        if (continueButton != null)
        {
            continueButton.interactable = true;
        }
    }

    public void NotifyOnlineRemoteContinueSubmitFailed()
    {
        if (!remotePresentation)
        {
            return;
        }

        remoteContinueRequestPending = false;
        RefreshContinueButtonAvailability();
    }

    private bool TrySubmitOnlineRemoteContinue()
    {
        if (!remotePresentation)
        {
            return false;
        }

        if (remoteContinueRequestPending ||
            currentEventPlayer == null)
        {
            return true;
        }

        Action<int> callback =
            RemoteContinueRequested;

        if (callback == null)
        {
            Debug.LogWarning(
                "Remote Event Continue has no online subscriber.",
                this);
            return true;
        }

        remoteContinueRequestPending = true;
        RefreshContinueButtonAvailability();
        callback.Invoke(
            currentEventPlayer.PlayerSlotIndex);
        return true;
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
                SetOnlineResultDescriptor(
                    "effect_none");

                FinishEffectExecution(
                    AtlasBoardL.T(
                        "event.effect_none"));
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
                                $"Event card: {card.CardId}");

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
                AtlasBoardL.T(
                    "event.bankrupt_result",
                    Mathf.Abs(
                        appliedMoneyChange),
                    transferredProperties);
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
                AtlasBoardL.T(
                    "event.money_no_change");
        }

        Debug.Log(
            $"{player.DisplayName} drew event card " +
            $"'{card.Title}'. Money change: " +
            $"{appliedMoneyChange}. Bankrupt: " +
            $"{causedBankruptcy}.",
            this);

        SetOnlineResultDescriptor(
            "money",
            appliedMoneyChange,
            causedBankruptcy ? 1 : 0,
            transferredProperties);

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

        SetOnlineResultDescriptor(
            "skip",
            turns);

        FinishEffectExecution(
            turns == 1
                ? AtlasBoardL.T(
                    "event.skip_one")
                : AtlasBoardL.T(
                    "event.skip_many",
                    turns));
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
            SetOnlineResultDescriptor(
                "move_failed");

            FinishEffectExecution(
                AtlasBoardL.T(
                    "event.move_failed"));
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
            string targetSuffix =
                targetTile != null
                    ? AtlasBoardL.T(
                        "event.move_target_suffix",
                        AtlasBoardL.TileName(
                            targetTile.TileType,
                            targetTile.DisplayName))
                    : string.Empty;

            eventResultText.text =
                AtlasBoardL.T(
                    "event.move_progress",
                    spaces,
                    targetSuffix);
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
                            ? AtlasBoardL.T(
                                "event.move_done_tile",
                                spaces,
                                AtlasBoardL.TileName(
                                    landedTile.TileType,
                                    landedTile.DisplayName))
                            : AtlasBoardL.T(
                                "event.move_done",
                                spaces);

                    Debug.Log(
                        $"{player.DisplayName} drew event card " +
                        $"'{card.Title}' and moved forward " +
                        $"{spaces} spaces.",
                        this);

                    if (landedTile != null)
                    {
                        SetOnlineResultDescriptor(
                            "move_done_tile",
                            spaces,
                            (int)landedTile.TileType,
                            0,
                            landedTile.DisplayName);
                    }
                    else
                    {
                        SetOnlineResultDescriptor(
                            "move_done",
                            spaces);
                    }

                    FinishEffectExecution(
                        result);
                });

        if (!started)
        {
            SetOnlineResultDescriptor(
                "move_failed");

            FinishEffectExecution(
                AtlasBoardL.T(
                    "event.move_failed"));
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
            SetOnlineResultDescriptor(
                "move_failed");

            FinishEffectExecution(
                AtlasBoardL.T(
                    "event.move_failed"));
            return;
        }

        int targetIndex =
            FindNextTileIndexOfType(
                pawn.CurrentTileIndex,
                card.TargetTileType);

        if (targetIndex < 0)
        {
            SetOnlineResultDescriptor(
                "target_not_found");

            FinishEffectExecution(
                AtlasBoardL.T(
                    "event.target_not_found"));
            return;
        }

        BoardTile targetTile =
            boardPath.GetTile(
                targetIndex);

        if (eventResultText != null)
        {
            eventResultText.text =
                targetTile != null
                    ? AtlasBoardL.T(
                        "event.target_moving_tile",
                        AtlasBoardL.TileName(
                            targetTile.TileType,
                            targetTile.DisplayName))
                    : AtlasBoardL.T(
                        "event.target_moving");
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
                            ? AtlasBoardL.T(
                                "event.target_done_tile",
                                AtlasBoardL.TileName(
                                    landedTile.TileType,
                                    landedTile.DisplayName))
                            : AtlasBoardL.T(
                                "event.target_done");

                    Debug.Log(
                        $"{player.DisplayName} drew event card " +
                        $"'{card.Title}' and moved to the next " +
                        $"{card.TargetTileType} tile.",
                        this);

                    if (landedTile != null)
                    {
                        SetOnlineResultDescriptor(
                            "target_done_tile",
                            (int)landedTile.TileType,
                            0,
                            0,
                            landedTile.DisplayName);
                    }
                    else
                    {
                        SetOnlineResultDescriptor(
                            "target_done");
                    }

                    FinishEffectExecution(
                        result);
                });

        if (!started)
        {
            SetOnlineResultDescriptor(
                "move_failed");

            FinishEffectExecution(
                AtlasBoardL.T(
                    "event.move_failed"));
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

        if (remotePresentation)
        {
            continueButton.interactable =
                effectExecutionCompleted &&
                !remoteContinueRequestPending;
            return;
        }

        bool locallyOwnedHuman =
            currentEventPlayer == null ||
            !onlineHostAuthorityMode ||
            onlineLocallyControlledHumanSlots.Contains(
                currentEventPlayer.PlayerSlotIndex);

        continueButton.interactable =
            effectExecutionCompleted &&
            locallyOwnedHuman &&
            (currentEventPlayer == null ||
             !IsBotPlayer(
                currentEventPlayer));
    }

    private bool ShouldShowAuthoritativePanelLocally(
        PlayerGameState player)
    {
        if (!onlineHostAuthorityMode ||
            player == null)
        {
            return true;
        }

        if (IsBotPlayer(player))
        {
            return false;
        }

        return onlineLocallyControlledHumanSlots.Contains(
            player.PlayerSlotIndex);
    }

    private EventCardDefinition FindCardById(
        string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId) ||
            eventDeck == null ||
            eventDeck.Cards == null)
        {
            return null;
        }

        foreach (EventCardDefinition card in eventDeck.Cards)
        {
            if (card != null &&
                string.Equals(
                    card.CardId,
                    cardId,
                    StringComparison.Ordinal))
            {
                return card;
            }
        }

        return null;
    }

    private void SetOnlineResultDescriptor(
        string kind,
        int value0 = 0,
        int value1 = 0,
        int value2 = 0,
        string textValue = "")
    {
        onlineResultKind = kind ?? string.Empty;
        onlineResultValue0 = value0;
        onlineResultValue1 = value1;
        onlineResultValue2 = value2;
        onlineResultTextValue = textValue ?? string.Empty;
    }

    private void ClearOnlineResultDescriptor()
    {
        onlineResultKind = string.Empty;
        onlineResultValue0 = 0;
        onlineResultValue1 = 0;
        onlineResultValue2 = 0;
        onlineResultTextValue = string.Empty;
    }

    private string BuildLocalizedRemoteResult()
    {
        if (string.Equals(
                onlineResultKind,
                "money",
                StringComparison.Ordinal))
        {
            if (onlineResultValue1 != 0)
            {
                return AtlasBoardL.T(
                    "event.bankrupt_result",
                    Mathf.Abs(onlineResultValue0),
                    onlineResultValue2);
            }

            if (onlineResultValue0 > 0)
            {
                return $"+{onlineResultValue0} ₵";
            }

            if (onlineResultValue0 < 0)
            {
                return $"{onlineResultValue0} ₵";
            }

            return AtlasBoardL.T(
                "event.money_no_change");
        }

        if (string.Equals(
                onlineResultKind,
                "skip",
                StringComparison.Ordinal))
        {
            return onlineResultValue0 == 1
                ? AtlasBoardL.T("event.skip_one")
                : AtlasBoardL.T(
                    "event.skip_many",
                    Mathf.Max(1, onlineResultValue0));
        }

        if (string.Equals(
                onlineResultKind,
                "move_done_tile",
                StringComparison.Ordinal))
        {
            return AtlasBoardL.T(
                "event.move_done_tile",
                onlineResultValue0,
                AtlasBoardL.TileName(
                    (TileType)onlineResultValue1,
                    onlineResultTextValue));
        }

        if (string.Equals(
                onlineResultKind,
                "move_done",
                StringComparison.Ordinal))
        {
            return AtlasBoardL.T(
                "event.move_done",
                onlineResultValue0);
        }

        if (string.Equals(
                onlineResultKind,
                "target_done_tile",
                StringComparison.Ordinal))
        {
            return AtlasBoardL.T(
                "event.target_done_tile",
                AtlasBoardL.TileName(
                    (TileType)onlineResultValue0,
                    onlineResultTextValue));
        }

        if (string.Equals(
                onlineResultKind,
                "target_done",
                StringComparison.Ordinal))
        {
            return AtlasBoardL.T(
                "event.target_done");
        }

        if (string.Equals(
                onlineResultKind,
                "target_not_found",
                StringComparison.Ordinal))
        {
            return AtlasBoardL.T(
                "event.target_not_found");
        }

        if (string.Equals(
                onlineResultKind,
                "move_failed",
                StringComparison.Ordinal))
        {
            return AtlasBoardL.T(
                "event.move_failed");
        }

        if (string.Equals(
                onlineResultKind,
                "effect_none",
                StringComparison.Ordinal))
        {
            return AtlasBoardL.T(
                "event.effect_none");
        }

        return AtlasBoardL.T(
            "event.applying");
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
        remotePresentation = false;
        remoteContinueRequestPending = false;
        currentEventPlayer = null;
        currentCard = null;
        ClearOnlineResultDescriptor();

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
    public void ResetForNewMatchSession()
    {
        resolutionCompleted = null;
        isResolvingEvent = false;
        effectExecutionCompleted = false;
        currentEventPlayer = null;
        remotePresentation = false;
        remoteContinueRequestPending = false;

        if (eventPanel != null) eventPanel.SetActive(false);
        if (continueButton != null) continueButton.interactable = true;
    }

}
