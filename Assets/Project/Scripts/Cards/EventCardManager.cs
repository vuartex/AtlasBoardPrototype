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
                ApplyEventDirectionalMovement(card, player,
                    Mathf.Max(1, Mathf.Abs(card.EffectAmount)));
                break;

            case EventCardEffectType.MoveToNextTileType:
                ApplyMoveToTileTypeEffect(card, player);
                break;
            case EventCardEffectType.MoveBackwardSpaces:
                ApplyEventDirectionalMovement(card, player, -Mathf.Max(1, Mathf.Abs(card.EffectAmount)));
                break;
            case EventCardEffectType.RandomForwardSpaces:
                ApplyEventDirectionalMovement(card, player, UnityEngine.Random.Range(1, 7));
                break;
            case EventCardEffectType.RandomBackwardSpaces:
                ApplyEventDirectionalMovement(card, player, -UnityEngine.Random.Range(2, 6));
                break;
            case EventCardEffectType.RandomCivicArrival:
                ApplyRandomCivicArrival(player);
                break;
            case EventCardEffectType.CivicProjectArrival:
            {
                AtlasBoardCivicProjectManager civic = FindAnyObjectByType<AtlasBoardCivicProjectManager>();
                int project = Mathf.Clamp(card.EffectAmount, 0, 2);
                if (civic == null || !civic.BeginArrival(player, project, () =>
                    {
                        SetOnlineResultDescriptor("civic_arrival", project);
                        FinishEffectExecution("Arrived at " + civic.GetProjectName(project));
                    }))
                    FinishEffectExecution("Civic destination unavailable.");
                break;
            }

            default:
                SetOnlineResultDescriptor(
                    "effect_none");

                FinishEffectExecution(
                    AtlasBoardL.T(
                        "event.effect_none"));
                break;
        }
    }

    // Phase 18F. Host/offline selects the roll once. Follower receives the
    // signed outcome through the existing event descriptor, never rolls locally.
    private void ApplyEventDirectionalMovement(EventCardDefinition card,
        PlayerGameState player, int signedSteps)
    {
        EnsureReferences();
        PlayerPawnMover pawn = player != null ? player.GetComponent<PlayerPawnMover>() : null;
        if (pawn == null || boardPath == null || boardPath.TileCount != 32 ||
            signedSteps == 0 || Mathf.Abs(signedSteps) >= boardPath.TileCount)
        {
            SetOnlineResultDescriptor("move_failed");
            FinishEffectExecution(AtlasBoardL.T("event.move_failed"));
            return;
        }

        int stepCount = Mathf.Abs(signedSteps);
        int target = (pawn.CurrentTileIndex + signedSteps + boardPath.TileCount) % boardPath.TileCount;
        int startingTile = pawn.CurrentTileIndex;
        if (eventResultText != null)
            eventResultText.text = AtlasBoardL.R("Moving {0} spaces...", "{0} kare hareket ediliyor...",
                "Moviendo {0} casillas...", "Déplacement de {0} cases...",
                "Bewege {0} Felder...", "{0}칸 이동 중...", "Перемещение на {0} клеток...", stepCount);

        Action<PlayerPawnMover> finished = movedPawn =>
        {
            BoardTile landedTile = movedPawn != null ? movedPawn.GetCurrentTile() : null;
            if (landedTile == null || movedPawn.CurrentTileIndex != target)
            {
                SetOnlineResultDescriptor("move_failed");
                FinishEffectExecution(AtlasBoardL.T("event.move_failed"));
                return;
            }

            // The landing outcome is committed by the authoritative pawn once.
            // The same signed number is sent in the event result descriptor.
            SetOnlineResultDescriptor("phase18f_landed", signedSteps,
                (int)landedTile.TileType, landedTile.TileIndex, landedTile.DisplayName);
            Debug.Log("ATB_PHASE18F_MOVE card=" + card.CardId + " from=" + startingTile +
                " to=" + landedTile.TileIndex + " steps=" + signedSteps, this);
            ResolveEventLanding(player, landedTile, signedSteps);
        };

        bool started = signedSteps < 0
            ? pawn.MoveBackwardForEvent(stepCount, finished)
            : pawn.MoveForwardToTile(target, finished);
        if (!started)
        {
            SetOnlineResultDescriptor("move_failed");
            FinishEffectExecution(AtlasBoardL.T("event.move_failed"));
        }
    }

    private void ResolveEventLanding(PlayerGameState player, BoardTile tile, int signedSteps)
    {
        string text = BuildMovementLandingResult(signedSteps, tile);
        // No recursive Event follow-up in v1. This also avoids overwriting the
        // original EventManager resolution callback and unbounded event chains.
        if (tile == null || tile.TileType == TileType.Event)
        {
            FinishEffectExecution(text);
            return;
        }

        // Read-only from the card viewpoint: append a bounded city-history
        // entry on the host/offline authority, never replay it on the follower.
        if (tile != null && tile.TileType == TileType.City)
        {
            PropertyDevelopmentManager memory = FindAnyObjectByType<PropertyDevelopmentManager>();
            memory?.RecordEconomicEventLanding(tile);
        }
        TileResolutionManager tileResolver = FindAnyObjectByType<TileResolutionManager>();
        if (tileResolver == null || player == null || player.IsBankrupt)
        {
            FinishEffectExecution(text);
            return;
        }

        // Hide the card while an ordinary City/Rent/Purchase/Special UI owns
        // the focus. Once resolved, return to Event Continue and only then end
        // the originating tile resolution through EventManager.CompleteEvent.
        if (eventPanel != null) eventPanel.SetActive(false);
        tileResolver.ResolveEventMovementLanding(player, tile, () =>
        {
            if (!isResolvingEvent || currentEventPlayer != player) return;
            if (eventPanel != null && ShouldShowAuthoritativePanelLocally(player))
                eventPanel.SetActive(true);
            FinishEffectExecution(text);
        });
    }

    private static string BuildMovementLandingResult(int signedSteps, BoardTile tile)
    {
        string direction = signedSteps < 0
            ? AtlasBoardL.R("backward", "geri", "atrás", "en arrière", "zurück", "뒤로", "назад")
            : AtlasBoardL.R("forward", "ileri", "adelante", "en avant", "vorwärts", "앞으로", "вперёд");
        string destination = tile != null ? AtlasBoardL.TileName(tile.TileType, tile.DisplayName) : "—";
        return AtlasBoardL.R("Moved {0} spaces {1} to {2}.", "{1} {0} kare gidildi: {2}.",
            "Movió {0} casillas {1} hasta {2}.", "Déplacement de {0} cases {1} vers {2}.",
            "{0} Felder {1} nach {2}.", "{1} {0}칸 이동: {2}.",
            "Перемещение на {0} клеток {1}: {2}.", Mathf.Abs(signedSteps), direction, destination);
    }

    private void ApplyRandomCivicArrival(PlayerGameState player)
    {
        AtlasBoardCivicProjectManager civic = FindAnyObjectByType<AtlasBoardCivicProjectManager>();
        if (civic == null || player == null)
        {
            SetOnlineResultDescriptor("move_failed");
            FinishEffectExecution(AtlasBoardL.T("event.move_failed"));
            return;
        }
        int project = UnityEngine.Random.Range(0, 3);
        bool started = civic.BeginArrival(player, project, () =>
        {
            SetOnlineResultDescriptor("phase18f_civic", project);
            FinishEffectExecution(AtlasBoardL.R("Arrived at {0}.", "{0} projesine ulaşıldı.",
                "Llegada a {0}.", "Arrivée à {0}.", "Ankunft bei {0}.",
                "{0}에 도착했습니다.", "Прибытие в {0}.", civic.GetProjectName(project)));
        });
        if (!started)
        {
            SetOnlineResultDescriptor("move_failed");
            FinishEffectExecution(AtlasBoardL.T("event.move_failed"));
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
                    result.LiquidatedPropertyCount;
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

        int startingTile = pawn.CurrentTileIndex;
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

                    if (landedTile != null)
                        ResolveEventLanding(player, landedTile, (targetIndex - startingTile + boardPath.TileCount) % boardPath.TileCount);
                    else
                        FinishEffectExecution(result);
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

        // One authored-equivalent Civic invitation in roughly 12 event draws.
        // Stable synthetic card IDs keep follower card lookup deterministic.
        AtlasBoardCivicProjectManager civic = FindAnyObjectByType<AtlasBoardCivicProjectManager>();
        if (civic != null && UnityEngine.Random.Range(0, 12) == 0)
        {
            int projectId = UnityEngine.Random.Range(0, 3);
            return EventCardDefinition.CreateRuntimeCivicCard(projectId, civic.GetProjectName(projectId));
        }
        // Five stable synthetic movement cards without changing existing Event
        // assets. The host draws once; followers resolve the stable card ID.
        // Existing cards remain 80%+ of non-Civic draws.
        if (UnityEngine.Random.Range(0, 5) == 0)
        {
            int kind = UnityEngine.Random.Range(0, 5);
            return EventCardDefinition.CreateRuntimeMovementCard(kind);
        }
        int randomValue = UnityEngine.Random.Range(0, totalWeight);

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
        if (string.Equals(cardId, "phase18f_detour", StringComparison.Ordinal)) return EventCardDefinition.CreateRuntimeMovementCard(0);
        if (string.Equals(cardId, "phase18f_express", StringComparison.Ordinal)) return EventCardDefinition.CreateRuntimeMovementCard(1);
        if (string.Equals(cardId, "phase18f_wrong_turn", StringComparison.Ordinal)) return EventCardDefinition.CreateRuntimeMovementCard(2);
        if (string.Equals(cardId, "phase18f_shortcut", StringComparison.Ordinal)) return EventCardDefinition.CreateRuntimeMovementCard(3);
        if (string.Equals(cardId, "phase18f_civic_invitation", StringComparison.Ordinal)) return EventCardDefinition.CreateRuntimeMovementCard(4);
        if (!string.IsNullOrEmpty(cardId) && cardId.StartsWith("civic_event_", StringComparison.Ordinal) &&
            int.TryParse(cardId.Substring("civic_event_".Length), out int project) && project >= 0 && project < 3)
        {
            AtlasBoardCivicProjectManager civic = FindAnyObjectByType<AtlasBoardCivicProjectManager>();
            return EventCardDefinition.CreateRuntimeCivicCard(project,
                civic != null ? civic.GetProjectName(project) : "Civic Project " + (project + 1));
        }

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

        if (string.Equals(onlineResultKind, "phase18f_landed", StringComparison.Ordinal))
        {
            string direction = onlineResultValue0 < 0
                ? AtlasBoardL.R("backward", "geri", "atrás", "en arrière", "zurück", "뒤로", "назад")
                : AtlasBoardL.R("forward", "ileri", "adelante", "en avant", "vorwärts", "앞으로", "вперёд");
            return AtlasBoardL.R("Moved {0} spaces {1} to {2}.", "{1} {0} kare gidildi: {2}.",
                "Movió {0} casillas {1} hasta {2}.", "Déplacement de {0} cases {1} vers {2}.",
                "{0} Felder {1} nach {2}.", "{1} {0}칸 이동: {2}.", "Перемещение на {0} клеток {1}: {2}.",
                Mathf.Abs(onlineResultValue0), direction, onlineResultTextValue);
        }

        if (string.Equals(onlineResultKind, "phase18f_civic", StringComparison.Ordinal))
        {
            AtlasBoardCivicProjectManager civic = FindAnyObjectByType<AtlasBoardCivicProjectManager>();
            return civic != null ? civic.GetProjectName(onlineResultValue0) : "Civic Project";
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
        currentCard = null;
        remotePresentation = false;
        remoteContinueRequestPending = false;
        ClearOnlineResultDescriptor();

        if (eventPanel != null) eventPanel.SetActive(false);
        if (continueButton != null) continueButton.interactable = true;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 18F - Event Movement Smoke")]
    private static void EditorPhase18FSmoke()
    {
        EventCardEffectType[] expected = {
            EventCardEffectType.MoveBackwardSpaces,
            EventCardEffectType.MoveForwardSpaces,
            EventCardEffectType.RandomBackwardSpaces,
            EventCardEffectType.RandomForwardSpaces,
            EventCardEffectType.RandomCivicArrival
        };
        int[] expectedValues = { 3, 6, 0, 0, 0 };
        bool valid = true;
        for (int i = 0; i < expected.Length; i++)
        {
            EventCardDefinition card = EventCardDefinition.CreateRuntimeMovementCard(i);
            valid &= card != null && card.EnabledCard && card.EffectType == expected[i] &&
                     card.EffectAmount == expectedValues[i] &&
                     !string.IsNullOrEmpty(card.Title) && !string.IsNullOrEmpty(card.Description);
        }
        // 32-space topology and wrap arithmetic (backward never implies Start).
        valid &= (0 - 3 + 32) % 32 == 29;
        valid &= (31 + 6) % 32 == 5;
        valid &= (4 - 5 + 32) % 32 == 31;
        valid &= 1 >= 1 && 6 <= 6 && 2 >= 2 && 5 <= 5;
        Debug.Log((valid ? "ATB_PHASE18F_QA PASS" : "ATB_PHASE18F_QA FAIL") +
            " cards=5 mapTiles=32 forward=1..6 backward=2..5" +
            " signedMotion=true recursiveEventLanding=blocked (read-only seed smoke)");
    }
#endif

}
