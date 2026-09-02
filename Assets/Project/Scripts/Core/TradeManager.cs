using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TradeManager : MonoBehaviour
{
    private enum TradePanelState
    {
        Closed,
        BuildingOffer,
        AwaitingResponse,
        ShowingResult
    }

    [Header("References")]
    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private BoardPath boardPath;

    [SerializeField]
    private PropertyDevelopmentManager
        propertyDevelopmentManager;

    [Header("Trade UI")]
    [SerializeField]
    private GameObject tradePanel;

    [SerializeField]
    private TMP_Text tradeTitleText;

    [SerializeField]
    private TMP_Dropdown targetPlayerDropdown;

    [SerializeField]
    private TMP_Dropdown offeredPropertyDropdown;

    [SerializeField]
    private TMP_InputField offeredCashInput;

    [SerializeField]
    private TMP_Dropdown requestedPropertyDropdown;

    [SerializeField]
    private TMP_InputField requestedCashInput;

    [SerializeField]
    private TMP_Text tradeSummaryText;

    [SerializeField]
    private Button sendOfferButton;

    [SerializeField]
    private Button cancelTradeButton;

    [SerializeField]
    private Button acceptTradeButton;

    [SerializeField]
    private Button rejectTradeButton;

    [Header("Presentation")]
    [SerializeField, Min(0f)]
    private float resultDisplayDuration = 1.25f;

    private readonly List<PlayerGameState>
        targetPlayers =
            new List<PlayerGameState>();

    private readonly List<BoardTile>
        offeredProperties =
            new List<BoardTile>();

    private readonly List<BoardTile>
        requestedProperties =
            new List<BoardTile>();

    private PlayerGameState initiator;
    private PlayerGameState target;

    private BoardTile offeredProperty;
    private BoardTile requestedProperty;

    private int offeredCash;
    private int requestedCash;

    private TradePanelState panelState =
        TradePanelState.Closed;

    private Coroutine closeCoroutine;

    // Online trade authority. Host owns validation/economy; Remote clients only
    // build or respond to offers for their locally-owned Human seats.
    private bool onlineAuthorityConfigured;
    private bool onlineHostAuthorityMode;
    private bool remoteTradePresentation;
    private bool remoteTradeRequestPending;
    private bool suppressRemoteWindowNotification;
    private readonly HashSet<int>
        onlineLocallyControlledHumanSlots =
            new HashSet<int>();

    public event Action<int, int, int, int, int, int>
        RemoteTradeOfferRequested;

    public event Action<int, bool>
        RemoteTradeResponseRequested;

    public event Action<int, bool>
        RemoteTradeWindowChanged;

    public bool IsRemoteTradePresentation =>
        remoteTradePresentation;

    public bool IsRemoteTradeRequestPending =>
        remoteTradeRequestPending;

    public bool IsTradeClosed =>
        panelState ==
        TradePanelState.Closed;

    public bool IsAwaitingResponse =>
        panelState ==
        TradePanelState.AwaitingResponse;

    public PlayerGameState TradeInitiator =>
        initiator;

    public PlayerGameState TradeTarget =>
        target;

    public BoardTile OfferedProperty =>
        offeredProperty;

    public BoardTile RequestedProperty =>
        requestedProperty;

    public int OfferedCash =>
        offeredCash;

    public int RequestedCash =>
        requestedCash;

    public bool HasPendingOfferFor(
        PlayerGameState player)
    {
        return IsAwaitingResponse &&
               player != null &&
               target != null &&
               (target == player ||
                target.PlayerSlotIndex ==
                player.PlayerSlotIndex);
    }

    public void ConfigureOnlineTradeAuthority(
        bool hostAuthorityMode,
        IEnumerable<int> locallyControlledHumanSlots)
    {
        onlineAuthorityConfigured = true;
        onlineHostAuthorityMode = hostAuthorityMode;
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

    public bool TryBeginAuthoritativeRemoteOffer(
        PlayerGameState remoteInitiator,
        PlayerGameState targetPlayer,
        BoardTile propertyOffered,
        int cashOffered,
        BoardTile propertyRequested,
        int cashRequested)
    {
        if (!onlineAuthorityConfigured ||
            !onlineHostAuthorityMode ||
            panelState != TradePanelState.Closed)
        {
            return false;
        }

        EnsureReferences();

        if (turnManager == null ||
            remoteInitiator == null ||
            targetPlayer == null ||
            remoteInitiator == targetPlayer ||
            remoteInitiator.IsBankrupt ||
            targetPlayer.IsBankrupt ||
            IsBotPlayer(remoteInitiator) ||
            !turnManager.TryBeginAuthoritativeNetworkManagementAction(
                remoteInitiator))
        {
            return false;
        }

        initiator = remoteInitiator;
        target = targetPlayer;
        offeredProperty = propertyOffered;
        requestedProperty = propertyRequested;
        offeredCash = Mathf.Max(0, cashOffered);
        requestedCash = Mathf.Max(0, cashRequested);

        if (!ValidateOffer(out string validationMessage))
        {
            Debug.LogWarning(
                $"Remote trade offer rejected by Host: {validationMessage}",
                this);
            CloseTradePanel();
            return false;
        }

        BuildTargetPlayerList();
        panelState = TradePanelState.AwaitingResponse;

        bool showLocally =
            IsLocallyControlledOnlineHuman(target) ||
            IsLocallyControlledOnlineHuman(initiator);

        SetPanelActive(showLocally);

        if (showLocally)
        {
            if (tradeTitleText != null)
            {
                tradeTitleText.text =
                    AtlasBoardL.T(
                        "trade.title",
                        AtlasBoardL.PlayerName(initiator));
            }

            SyncCurrentOfferToUI();
            SetSetupControlsInteractable(false);
            SetResponseButtonsVisible(
                IsLocallyControlledOnlineHuman(target));
            RefreshResponseButtonAvailability();
            SetSummary(
                BuildOfferSummary(
                    includeResponsePrompt: true));
        }

        Debug.Log(
            $"Host accepted remote trade offer from {initiator.DisplayName} " +
            $"to {target.DisplayName}: {BuildCompactOfferDescription()}.",
            this);

        return true;
    }

    public void ShowOnlineRemoteTradeState(
        PlayerGameState authoritativeInitiator,
        PlayerGameState authoritativeTarget,
        BoardTile propertyOffered,
        int cashOffered,
        BoardTile propertyRequested,
        int cashRequested)
    {
        if (onlineHostAuthorityMode ||
            authoritativeInitiator == null ||
            authoritativeTarget == null)
        {
            ClearOnlineRemoteTradeState();
            return;
        }

        bool localParticipant =
            IsLocallyControlledOnlineHuman(authoritativeInitiator) ||
            IsLocallyControlledOnlineHuman(authoritativeTarget);

        if (!localParticipant)
        {
            ClearOnlineRemoteTradeState();
            return;
        }

        EnsureReferences();

        bool sameState =
            remoteTradePresentation &&
            panelState == TradePanelState.AwaitingResponse &&
            initiator != null &&
            target != null &&
            initiator.PlayerSlotIndex ==
                authoritativeInitiator.PlayerSlotIndex &&
            target.PlayerSlotIndex ==
                authoritativeTarget.PlayerSlotIndex &&
            offeredProperty == propertyOffered &&
            requestedProperty == propertyRequested &&
            offeredCash == Mathf.Max(0, cashOffered) &&
            requestedCash == Mathf.Max(0, cashRequested);

        if (sameState)
        {
            remoteTradeRequestPending = false;
            RefreshResponseButtonAvailability();
            return;
        }

        remoteTradePresentation = true;
        remoteTradeRequestPending = false;
        initiator = authoritativeInitiator;
        target = authoritativeTarget;
        offeredProperty = propertyOffered;
        requestedProperty = propertyRequested;
        offeredCash = Mathf.Max(0, cashOffered);
        requestedCash = Mathf.Max(0, cashRequested);
        panelState = TradePanelState.AwaitingResponse;

        BuildTargetPlayerList();
        SetPanelActive(true);

        if (tradeTitleText != null)
        {
            tradeTitleText.text =
                AtlasBoardL.T(
                    "trade.title",
                    AtlasBoardL.PlayerName(initiator));
        }

        SyncCurrentOfferToUI();
        SetSetupControlsInteractable(false);

        bool localTarget =
            IsLocallyControlledOnlineHuman(target);

        SetResponseButtonsVisible(localTarget);
        RefreshResponseButtonAvailability();
        SetSummary(
            BuildOfferSummary(
                includeResponsePrompt: true));
    }

    public void ClearOnlineRemoteTradeState()
    {
        if (!remoteTradePresentation)
        {
            return;
        }

        remoteTradePresentation = false;
        remoteTradeRequestPending = false;
        CloseTradePanel();
    }

    public void ResetForNewMatchSession()
    {
        suppressRemoteWindowNotification = true;

        try
        {
            CloseTradePanel();
        }
        finally
        {
            suppressRemoteWindowNotification = false;
        }
    }

    public void NotifyOnlineRemoteTradeSubmitFailed()
    {
        if (!remoteTradePresentation &&
            panelState != TradePanelState.AwaitingResponse)
        {
            return;
        }

        remoteTradeRequestPending = false;
        RefreshResponseButtonAvailability();
    }

    private void Start()
    {
        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;

        SetPanelActive(false);
    }

    private void OnDestroy()
    {
        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;
    }

    private void HandleLanguageChanged()
    {
        if (panelState ==
            TradePanelState.Closed)
        {
            return;
        }

        if (tradeTitleText != null &&
            initiator != null)
        {
            tradeTitleText.text =
                AtlasBoardL.T(
                    "trade.title",
                    AtlasBoardL.PlayerName(
                        initiator));
        }

        if (panelState ==
            TradePanelState.BuildingOffer)
        {
            RefreshTargetAndPropertyOptions();
            UpdateSetupSummary();
            return;
        }

        if (panelState ==
            TradePanelState.AwaitingResponse)
        {
            SyncCurrentOfferToUI();

            SetSummary(
                BuildOfferSummary(
                    includeResponsePrompt: true));
        }
    }

    public bool TryBeginBotOffer(
        PlayerGameState botInitiator,
        PlayerGameState targetPlayer,
        BoardTile propertyOffered,
        int cashOffered,
        BoardTile propertyRequested,
        int cashRequested)
    {
        if (panelState != TradePanelState.Closed)
        {
            return false;
        }

        EnsureReferences();

        if (turnManager == null ||
            botInitiator == null ||
            targetPlayer == null ||
            botInitiator == targetPlayer ||
            botInitiator.IsBankrupt ||
            targetPlayer.IsBankrupt ||
            !IsBotPlayer(botInitiator) ||
            !turnManager.TryBeginBotManagementAction(
                botInitiator))
        {
            return false;
        }

        initiator = botInitiator;
        target = targetPlayer;

        offeredProperty = propertyOffered;
        requestedProperty = propertyRequested;

        offeredCash =
            Mathf.Max(0, cashOffered);

        requestedCash =
            Mathf.Max(0, cashRequested);

        if (!ValidateOffer(
                out string validationMessage))
        {
            Debug.LogWarning(
                $"{botInitiator.DisplayName} [BOT] generated an " +
                $"invalid trade offer: {validationMessage}",
                this);

            CloseTradePanel();
            return false;
        }

        BuildTargetPlayerList();

        panelState =
            TradePanelState.AwaitingResponse;

        SetPanelActive(true);

        if (tradeTitleText != null)
        {
            tradeTitleText.text =
                AtlasBoardL.T(
                    "trade.title",
                    AtlasBoardL.PlayerName(
                        initiator));
        }

        SyncCurrentOfferToUI();

        SetSetupControlsInteractable(false);
        SetResponseButtonsVisible(true);
        RefreshResponseButtonAvailability();

        SetSummary(
            BuildOfferSummary(
                includeResponsePrompt: true));

        Debug.Log(
            $"{initiator.DisplayName} [BOT] created a trade offer " +
            $"for {target.DisplayName}: " +
            $"{BuildCompactOfferDescription()}.",
            this);

        return true;
    }

    public void OpenTradePanel()
    {
        if (panelState != TradePanelState.Closed)
        {
            return;
        }

        EnsureReferences();

        if (turnManager == null)
        {
            return;
        }

        PlayerGameState currentPlayer =
            turnManager.CurrentPlayerState;

        bool beganManagement;

        if (onlineAuthorityConfigured &&
            !onlineHostAuthorityMode)
        {
            beganManagement =
                currentPlayer != null &&
                IsLocallyControlledOnlineHuman(currentPlayer) &&
                turnManager
                    .BeginOnlineFollowerManagementPresentation(
                        currentPlayer);
        }
        else
        {
            beganManagement =
                turnManager.TryBeginManagementAction();
        }

        if (!beganManagement)
        {
            Debug.LogWarning(
                "Trade cannot open right now. " +
                "Trades are available before rolling.",
                this);
            return;
        }

        initiator = currentPlayer;

        if (initiator == null ||
            initiator.IsBankrupt)
        {
            CloseTradePanel();
            return;
        }

        BuildTargetPlayerList();

        if (targetPlayers.Count == 0)
        {
            ShowTemporaryResult(
                AtlasBoardL.T(
                    "trade.no_players"));

            return;
        }

        panelState =
            TradePanelState.BuildingOffer;

        if (onlineAuthorityConfigured &&
            !onlineHostAuthorityMode &&
            IsLocallyControlledOnlineHuman(initiator))
        {
            RemoteTradeWindowChanged?.Invoke(
                initiator.PlayerSlotIndex,
                true);
        }

        SetPanelActive(true);
        SetSetupControlsInteractable(true);
        SetResponseButtonsVisible(false);

        if (tradeTitleText != null)
        {
            tradeTitleText.text =
                AtlasBoardL.T(
                    "trade.title",
                    AtlasBoardL.PlayerName(
                        initiator));
        }

        PopulateTargetDropdown();

        if (offeredCashInput != null)
        {
            offeredCashInput.text = "0";
        }

        if (requestedCashInput != null)
        {
            requestedCashInput.text = "0";
        }

        RefreshTargetAndPropertyOptions();
        UpdateSetupSummary();
    }

    public void OnTargetPlayerChanged(
        int dropdownIndex)
    {
        if (panelState !=
            TradePanelState.BuildingOffer)
        {
            return;
        }

        RefreshTargetAndPropertyOptions();
        UpdateSetupSummary();
    }

    public void OnTradeInputChanged(
        string unusedValue)
    {
        if (panelState ==
            TradePanelState.BuildingOffer)
        {
            UpdateSetupSummary();
        }
    }

    public void OnTradeDropdownChanged(
        int unusedValue)
    {
        if (panelState ==
            TradePanelState.BuildingOffer)
        {
            UpdateSetupSummary();
        }
    }

    public void SendTradeOffer()
    {
        if (panelState !=
            TradePanelState.BuildingOffer)
        {
            return;
        }

        ReadCurrentOffer();

        if (!ValidateOffer(
                out string validationMessage))
        {
            SetSummary(validationMessage);
            return;
        }

        if (onlineAuthorityConfigured &&
            !onlineHostAuthorityMode)
        {
            Action<int, int, int, int, int, int> callback =
                RemoteTradeOfferRequested;

            if (callback == null ||
                initiator == null ||
                target == null)
            {
                SetSummary(
                    "Online trade transport is not ready.");
                return;
            }

            panelState =
                TradePanelState.AwaitingResponse;
            remoteTradePresentation = true;
            remoteTradeRequestPending = true;

            SetSetupControlsInteractable(false);
            SetResponseButtonsVisible(false);
            SetSummary(
                BuildOfferSummary(
                    includeResponsePrompt: true));

            callback.Invoke(
                initiator.PlayerSlotIndex,
                target.PlayerSlotIndex,
                offeredProperty != null
                    ? offeredProperty.TileIndex
                    : -1,
                offeredCash,
                requestedProperty != null
                    ? requestedProperty.TileIndex
                    : -1,
                requestedCash);

            return;
        }

        panelState =
            TradePanelState.AwaitingResponse;

        SetSetupControlsInteractable(false);
        SetResponseButtonsVisible(true);
        RefreshResponseButtonAvailability();

        SetSummary(
            BuildOfferSummary(
                includeResponsePrompt: true));

        Debug.Log(
            $"{initiator.DisplayName} sent a trade offer " +
            $"to {target.DisplayName}: " +
            $"{BuildCompactOfferDescription()}.",
            this);
    }

    public bool TryResolveBotResponse(
        PlayerGameState respondingPlayer,
        bool acceptOffer)
    {
        if (!HasPendingOfferFor(
                respondingPlayer) ||
            !IsBotPlayer(
                respondingPlayer))
        {
            return false;
        }

        if (acceptOffer)
        {
            AcceptTradeOffer();
        }
        else
        {
            RejectTradeOffer();
        }

        return true;
    }

    public void AcceptTradeOffer()
    {
        if (panelState !=
            TradePanelState.AwaitingResponse)
        {
            return;
        }

        if (onlineAuthorityConfigured &&
            !onlineHostAuthorityMode &&
            remoteTradePresentation)
        {
            SubmitOnlineRemoteTradeResponse(true);
            return;
        }

        if (!ValidateOffer(
                out string validationMessage))
        {
            ShowTemporaryResult(
                AtlasBoardL.T(
                    "trade.no_longer_valid") +
                "\n" +
                validationMessage);

            return;
        }

        if (!ExecuteTrade())
        {
            ShowTemporaryResult(
                AtlasBoardL.T(
                    "trade.apply_failed"));

            return;
        }

        Debug.Log(
            $"{target.DisplayName} accepted the trade " +
            $"from {initiator.DisplayName}.",
            this);

        ShowTemporaryResult(
            AtlasBoardL.T(
                "trade.accepted") +
            "\n\n" +
            BuildOfferSummary(
                includeResponsePrompt: false));
    }

    public void RejectTradeOffer()
    {
        if (panelState !=
            TradePanelState.AwaitingResponse)
        {
            return;
        }

        if (onlineAuthorityConfigured &&
            !onlineHostAuthorityMode &&
            remoteTradePresentation)
        {
            SubmitOnlineRemoteTradeResponse(false);
            return;
        }

        Debug.Log(
            $"{target?.DisplayName ?? "Target player"} " +
            $"rejected the trade from " +
            $"{initiator?.DisplayName ?? "initiator"}.",
            this);

        ShowTemporaryResult(
            AtlasBoardL.T(
                "trade.rejected"));
    }

    public void CancelTrade()
    {
        if (panelState ==
            TradePanelState.Closed)
        {
            return;
        }

        Debug.Log(
            $"{initiator?.DisplayName ?? "Player"} " +
            "cancelled the trade setup.",
            this);

        CloseTradePanel();
    }

    private void BuildTargetPlayerList()
    {
        targetPlayers.Clear();

        if (turnManager == null ||
            initiator == null)
        {
            return;
        }

        List<PlayerGameState> orderedPlayers =
            turnManager.GetPlayersInTurnOrderFrom(
                initiator,
                includeReferencePlayer: false);

        foreach (PlayerGameState player
                 in orderedPlayers)
        {
            if (player != null &&
                !player.IsBankrupt)
            {
                targetPlayers.Add(player);
            }
        }
    }

    private void PopulateTargetDropdown()
    {
        if (targetPlayerDropdown == null)
        {
            return;
        }

        targetPlayerDropdown.ClearOptions();

        List<string> options =
            new List<string>();

        foreach (PlayerGameState player
                 in targetPlayers)
        {
            options.Add(
                AtlasBoardL.PlayerName(
                    player));
        }

        targetPlayerDropdown.AddOptions(options);
        targetPlayerDropdown.SetValueWithoutNotify(0);
        targetPlayerDropdown.RefreshShownValue();
    }

    private void RefreshTargetAndPropertyOptions()
    {
        if (targetPlayers.Count == 0)
        {
            target = null;
            return;
        }

        int targetIndex =
            targetPlayerDropdown != null
                ? Mathf.Clamp(
                    targetPlayerDropdown.value,
                    0,
                    targetPlayers.Count - 1)
                : 0;

        target =
            targetPlayers[targetIndex];

        BuildPropertyList(
            initiator,
            offeredProperties);

        BuildPropertyList(
            target,
            requestedProperties);

        PopulatePropertyDropdown(
            offeredPropertyDropdown,
            offeredProperties,
            AtlasBoardL.T(
                "trade.no_offered_property"));

        PopulatePropertyDropdown(
            requestedPropertyDropdown,
            requestedProperties,
            AtlasBoardL.T(
                "trade.no_requested_property"));
    }

    private void BuildPropertyList(
        PlayerGameState owner,
        List<BoardTile> destination)
    {
        destination.Clear();
        destination.Add(null);

        EnsureReferences();

        if (boardPath == null ||
            owner == null)
        {
            return;
        }

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile == null ||
                tile.TileType != TileType.City ||
                !tile.IsOwned ||
                tile.OwnerPlayerIndex !=
                owner.PlayerSlotIndex)
            {
                continue;
            }

            int developmentLevel =
                propertyDevelopmentManager != null
                    ? propertyDevelopmentManager
                        .GetDevelopmentLevel(tile)
                    : 0;

            if (developmentLevel > 0)
            {
                continue;
            }

            destination.Add(tile);
        }
    }

    private void PopulatePropertyDropdown(
        TMP_Dropdown dropdown,
        List<BoardTile> properties,
        string noneLabel)
    {
        if (dropdown == null)
        {
            return;
        }

        dropdown.ClearOptions();

        List<string> options =
            new List<string>();

        for (int index = 0;
             index < properties.Count;
             index++)
        {
            BoardTile tile =
                properties[index];

            options.Add(
                tile == null
                    ? noneLabel
                    : $"{tile.DisplayName} " +
                      $"({tile.PurchasePrice} ₵)");
        }

        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(0);
        dropdown.RefreshShownValue();
    }

    private void SyncCurrentOfferToUI()
    {
        PopulateTargetDropdown();

        if (targetPlayerDropdown != null)
        {
            int targetIndex =
                targetPlayers.IndexOf(target);

            if (targetIndex >= 0)
            {
                targetPlayerDropdown
                    .SetValueWithoutNotify(
                        targetIndex);

                targetPlayerDropdown
                    .RefreshShownValue();
            }
        }

        BuildPropertyList(
            initiator,
            offeredProperties);

        BuildPropertyList(
            target,
            requestedProperties);

        PopulatePropertyDropdown(
            offeredPropertyDropdown,
            offeredProperties,
            AtlasBoardL.T(
                "trade.no_offered_property"));

        PopulatePropertyDropdown(
            requestedPropertyDropdown,
            requestedProperties,
            AtlasBoardL.T(
                "trade.no_requested_property"));

        if (offeredPropertyDropdown != null)
        {
            int propertyIndex =
                offeredProperties.IndexOf(
                    offeredProperty);

            offeredPropertyDropdown
                .SetValueWithoutNotify(
                    Mathf.Max(
                        0,
                        propertyIndex));

            offeredPropertyDropdown
                .RefreshShownValue();
        }

        if (requestedPropertyDropdown != null)
        {
            int propertyIndex =
                requestedProperties.IndexOf(
                    requestedProperty);

            requestedPropertyDropdown
                .SetValueWithoutNotify(
                    Mathf.Max(
                        0,
                        propertyIndex));

            requestedPropertyDropdown
                .RefreshShownValue();
        }

        if (offeredCashInput != null)
        {
            offeredCashInput
                .SetTextWithoutNotify(
                    offeredCash.ToString());
        }

        if (requestedCashInput != null)
        {
            requestedCashInput
                .SetTextWithoutNotify(
                    requestedCash.ToString());
        }
    }

    private void ReadCurrentOffer()
    {
        offeredProperty =
            GetSelectedProperty(
                offeredPropertyDropdown,
                offeredProperties);

        requestedProperty =
            GetSelectedProperty(
                requestedPropertyDropdown,
                requestedProperties);

        offeredCash =
            ParseNonNegativeCash(
                offeredCashInput);

        requestedCash =
            ParseNonNegativeCash(
                requestedCashInput);
    }

    private BoardTile GetSelectedProperty(
        TMP_Dropdown dropdown,
        List<BoardTile> properties)
    {
        if (properties == null ||
            properties.Count == 0)
        {
            return null;
        }

        int selectedIndex =
            dropdown != null
                ? Mathf.Clamp(
                    dropdown.value,
                    0,
                    properties.Count - 1)
                : 0;

        return properties[selectedIndex];
    }

    private int ParseNonNegativeCash(
        TMP_InputField input)
    {
        if (input == null ||
            string.IsNullOrWhiteSpace(
                input.text))
        {
            return 0;
        }

        if (!int.TryParse(
                input.text,
                out int amount))
        {
            return 0;
        }

        return Mathf.Max(0, amount);
    }

    private bool ValidateOffer(
        out string message)
    {
        message = string.Empty;

        if (initiator == null ||
            target == null ||
            initiator == target)
        {
            message =
                "Geçerli bir hedef oyuncu seçilmedi.";

            return false;
        }

        if (initiator.IsBankrupt ||
            target.IsBankrupt)
        {
            message =
                "İflas etmiş oyuncular takas yapamaz.";

            return false;
        }

        if (offeredCash > 0 &&
            requestedCash > 0)
        {
            message =
                "Aynı teklifte iki yönde nakit " +
                "kullanılamaz. Yalnızca teklif edilen " +
                "veya talep edilen nakdi doldur.";

            return false;
        }

        bool initiatorContribution =
            offeredProperty != null ||
            offeredCash > 0;

        bool targetContribution =
            requestedProperty != null ||
            requestedCash > 0;

        if (!initiatorContribution ||
            !targetContribution)
        {
            message =
                "Her iki taraf da en az bir mülk " +
                "veya nakit sunmalıdır.";

            return false;
        }

        if (offeredProperty != null &&
            !ValidatePropertyOwnership(
                offeredProperty,
                initiator,
                out message))
        {
            return false;
        }

        if (requestedProperty != null &&
            !ValidatePropertyOwnership(
                requestedProperty,
                target,
                out message))
        {
            return false;
        }

        if (offeredCash >
            initiator.CurrentMoney)
        {
            message =
                $"{initiator.DisplayName} için " +
                "teklif edilen nakit bakiyeyi aşıyor.";

            return false;
        }

        if (requestedCash >
            target.CurrentMoney)
        {
            message =
                $"{target.DisplayName} için talep edilen " +
                "nakit bakiyeyi aşıyor.";

            return false;
        }

        return true;
    }

    private bool ValidatePropertyOwnership(
        BoardTile tile,
        PlayerGameState expectedOwner,
        out string message)
    {
        message = string.Empty;

        if (tile == null ||
            expectedOwner == null ||
            !tile.IsOwned ||
            tile.OwnerPlayerIndex !=
            expectedOwner.PlayerSlotIndex)
        {
            message =
                "Seçilen mülk artık beklenen oyuncuya " +
                "ait değil.";

            return false;
        }

        int developmentLevel =
            propertyDevelopmentManager != null
                ? propertyDevelopmentManager
                    .GetDevelopmentLevel(tile)
                : 0;

        if (developmentLevel > 0)
        {
            message =
                $"{tile.DisplayName} geliştirilmiş olduğu " +
                "için bu sürümde takas edilemez.";

            return false;
        }

        return true;
    }

    private bool ExecuteTrade()
    {
        PlayerGameState offeredOriginalOwner =
            initiator;

        PlayerGameState requestedOriginalOwner =
            target;

        bool offeredTransferred =
            TransferProperty(
                offeredProperty,
                initiator,
                target);

        if (!offeredTransferred)
        {
            return false;
        }

        bool requestedTransferred =
            TransferProperty(
                requestedProperty,
                target,
                initiator);

        if (!requestedTransferred)
        {
            RestoreProperty(
                offeredProperty,
                offeredOriginalOwner);

            return false;
        }

        if (offeredCash > 0)
        {
            if (!initiator.TrySpend(
                    offeredCash))
            {
                RestoreProperty(
                    offeredProperty,
                    offeredOriginalOwner);

                RestoreProperty(
                    requestedProperty,
                    requestedOriginalOwner);

                return false;
            }

            target.AddMoney(offeredCash);
        }
        else if (requestedCash > 0)
        {
            if (!target.TrySpend(
                    requestedCash))
            {
                RestoreProperty(
                    offeredProperty,
                    offeredOriginalOwner);

                RestoreProperty(
                    requestedProperty,
                    requestedOriginalOwner);

                return false;
            }

            initiator.AddMoney(requestedCash);
        }

        return true;
    }

    private bool TransferProperty(
        BoardTile tile,
        PlayerGameState fromPlayer,
        PlayerGameState toPlayer)
    {
        if (tile == null)
        {
            return true;
        }

        if (fromPlayer == null ||
            toPlayer == null ||
            tile.OwnerPlayerIndex !=
            fromPlayer.PlayerSlotIndex)
        {
            return false;
        }

        tile.ClearOwner();

        bool assigned =
            tile.TrySetOwner(
                toPlayer.PlayerSlotIndex);

        if (!assigned)
        {
            RestoreProperty(
                tile,
                fromPlayer);

            return false;
        }

        if (toPlayer.OwnershipMaterial != null)
        {
            tile.ApplyOwnerMaterial(
                toPlayer.OwnershipMaterial);
        }

        return true;
    }

    private void RestoreProperty(
        BoardTile tile,
        PlayerGameState owner)
    {
        if (tile == null ||
            owner == null)
        {
            return;
        }

        tile.ClearOwner();

        if (tile.TrySetOwner(
                owner.PlayerSlotIndex) &&
            owner.OwnershipMaterial != null)
        {
            tile.ApplyOwnerMaterial(
                owner.OwnershipMaterial);
        }
    }

    private void UpdateSetupSummary()
    {
        ReadCurrentOffer();

        string targetName =
            target != null
                ? AtlasBoardL.PlayerName(
                    target)
                : AtlasBoardL.T(
                    "trade.no_target");

        SetSummary(
            AtlasBoardL.T(
                "trade.target_summary",
                targetName,
                BuildOfferSummary(
                    includeResponsePrompt: false)));
    }

    private string BuildOfferSummary(
        bool includeResponsePrompt)
    {
        string offeredPropertyName =
            offeredProperty != null
                ? offeredProperty.DisplayName
                : AtlasBoardL.T(
                    "trade.no_property");

        string requestedPropertyName =
            requestedProperty != null
                ? requestedProperty.DisplayName
                : AtlasBoardL.T(
                    "trade.no_property");

        string initiatorName =
            initiator != null
                ? AtlasBoardL.PlayerName(
                    initiator)
                : AtlasBoardL.T(
                    "common.player");

        string targetName =
            target != null
                ? AtlasBoardL.PlayerName(
                    target)
                : AtlasBoardL.T(
                    "trade.no_target");

        string summary =
            AtlasBoardL.T(
                "trade.offer_summary",
                initiatorName,
                offeredPropertyName,
                offeredCash,
                targetName,
                requestedPropertyName,
                requestedCash);

        if (includeResponsePrompt &&
            target != null)
        {
            summary +=
                "\n\n" +
                AtlasBoardL.T(
                    "trade.response_prompt",
                    targetName);
        }

        return summary;
    }

    private string BuildCompactOfferDescription()
    {
        return
            $"offered property=" +
            $"{offeredProperty?.DisplayName ?? "none"}, " +
            $"offered cash={offeredCash}, " +
            $"requested property=" +
            $"{requestedProperty?.DisplayName ?? "none"}, " +
            $"requested cash={requestedCash}";
    }

    private void RefreshResponseButtonAvailability()
    {
        bool humanCanRespond =
            panelState ==
                TradePanelState.AwaitingResponse &&
            target != null &&
            !IsBotPlayer(target) &&
            (!onlineAuthorityConfigured ||
             IsLocallyControlledOnlineHuman(target)) &&
            (!remoteTradePresentation ||
             !remoteTradeRequestPending);

        if (acceptTradeButton != null)
        {
            acceptTradeButton.interactable =
                humanCanRespond;
        }

        if (rejectTradeButton != null)
        {
            rejectTradeButton.interactable =
                humanCanRespond;
        }
    }

    private void SubmitOnlineRemoteTradeResponse(
        bool acceptOffer)
    {
        if (remoteTradeRequestPending ||
            target == null ||
            !IsLocallyControlledOnlineHuman(target))
        {
            return;
        }

        Action<int, bool> callback =
            RemoteTradeResponseRequested;

        if (callback == null)
        {
            Debug.LogWarning(
                "Remote trade response has no online subscriber.",
                this);
            return;
        }

        remoteTradeRequestPending = true;
        RefreshResponseButtonAvailability();
        callback.Invoke(
            target.PlayerSlotIndex,
            acceptOffer);
    }

    private bool IsLocallyControlledOnlineHuman(
        PlayerGameState player)
    {
        return player != null &&
               onlineLocallyControlledHumanSlots.Contains(
                   player.PlayerSlotIndex);
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

    private void SetSetupControlsInteractable(
        bool interactable)
    {
        if (targetPlayerDropdown != null)
        {
            targetPlayerDropdown.interactable =
                interactable;
        }

        if (offeredPropertyDropdown != null)
        {
            offeredPropertyDropdown.interactable =
                interactable;
        }

        if (requestedPropertyDropdown != null)
        {
            requestedPropertyDropdown.interactable =
                interactable;
        }

        if (offeredCashInput != null)
        {
            offeredCashInput.interactable =
                interactable;
        }

        if (requestedCashInput != null)
        {
            requestedCashInput.interactable =
                interactable;
        }

        if (sendOfferButton != null)
        {
            sendOfferButton.gameObject.SetActive(
                interactable);
        }

        if (cancelTradeButton != null)
        {
            cancelTradeButton.gameObject.SetActive(
                interactable);
        }
    }

    private void SetResponseButtonsVisible(
        bool visible)
    {
        if (acceptTradeButton != null)
        {
            acceptTradeButton.gameObject.SetActive(
                visible);

            if (!visible)
            {
                acceptTradeButton.interactable = true;
            }
        }

        if (rejectTradeButton != null)
        {
            rejectTradeButton.gameObject.SetActive(
                visible);

            if (!visible)
            {
                rejectTradeButton.interactable = true;
            }
        }
    }

    private void ShowTemporaryResult(
        string message)
    {
        panelState =
            TradePanelState.ShowingResult;

        SetSetupControlsInteractable(false);
        SetResponseButtonsVisible(false);
        SetSummary(message);
        SetPanelActive(true);

        if (closeCoroutine != null)
        {
            StopCoroutine(closeCoroutine);
        }

        closeCoroutine =
            StartCoroutine(
                CloseAfterDelay());
    }

    private IEnumerator CloseAfterDelay()
    {
        if (resultDisplayDuration > 0f)
        {
            yield return new WaitForSeconds(
                resultDisplayDuration);
        }

        closeCoroutine = null;
        CloseTradePanel();
    }

    private void CloseTradePanel()
    {
        PlayerGameState closingInitiator =
            initiator;

        if (closeCoroutine != null)
        {
            StopCoroutine(closeCoroutine);
            closeCoroutine = null;
        }

        SetPanelActive(false);

        panelState =
            TradePanelState.Closed;

        initiator = null;
        target = null;
        offeredProperty = null;
        requestedProperty = null;
        offeredCash = 0;
        requestedCash = 0;
        remoteTradePresentation = false;
        remoteTradeRequestPending = false;

        if (!suppressRemoteWindowNotification &&
            onlineAuthorityConfigured &&
            !onlineHostAuthorityMode &&
            closingInitiator != null &&
            IsLocallyControlledOnlineHuman(
                closingInitiator))
        {
            RemoteTradeWindowChanged?.Invoke(
                closingInitiator.PlayerSlotIndex,
                false);
        }

        targetPlayers.Clear();
        offeredProperties.Clear();
        requestedProperties.Clear();

        if (turnManager != null)
        {
            if (onlineAuthorityConfigured &&
                !onlineHostAuthorityMode)
            {
                turnManager
                    .EndOnlineFollowerManagementPresentation();
            }
            else
            {
                turnManager.CompleteManagementAction();
            }
        }
    }

    private void SetSummary(
        string message)
    {
        if (tradeSummaryText != null)
        {
            tradeSummaryText.text = message;
        }
    }

    private void SetPanelActive(
        bool active)
    {
        if (tradePanel != null)
        {
            tradePanel.SetActive(active);
        }
    }

    private void EnsureReferences()
    {
        if (turnManager == null)
        {
            turnManager =
                FindAnyObjectByType<TurnManager>();
        }

        if (boardPath == null)
        {
            boardPath =
                FindAnyObjectByType<BoardPath>();
        }

        if (propertyDevelopmentManager == null)
        {
            propertyDevelopmentManager =
                FindAnyObjectByType<
                    PropertyDevelopmentManager>();
        }
    }
}
