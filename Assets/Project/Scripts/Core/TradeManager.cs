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

    private void Start()
    {
        SetPanelActive(false);
    }

    public void OpenTradePanel()
    {
        if (panelState != TradePanelState.Closed)
        {
            return;
        }

        EnsureReferences();

        if (turnManager == null ||
            !turnManager.TryBeginManagementAction())
        {
            Debug.LogWarning(
                "Trade cannot open right now. " +
                "Trades are available before rolling.",
                this);

            return;
        }

        initiator =
            turnManager.CurrentPlayerState;

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
                "Uygun takas oyuncusu bulunamadı.");

            return;
        }

        panelState =
            TradePanelState.BuildingOffer;

        SetPanelActive(true);
        SetSetupControlsInteractable(true);
        SetResponseButtonsVisible(false);

        if (tradeTitleText != null)
        {
            tradeTitleText.text =
                $"TAKAS — {initiator.DisplayName}";
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

        panelState =
            TradePanelState.AwaitingResponse;

        SetSetupControlsInteractable(false);
        SetResponseButtonsVisible(true);

        SetSummary(
            BuildOfferSummary(
                includeResponsePrompt: true));

        Debug.Log(
            $"{initiator.DisplayName} sent a trade offer " +
            $"to {target.DisplayName}: " +
            $"{BuildCompactOfferDescription()}.",
            this);
    }

    public void AcceptTradeOffer()
    {
        if (panelState !=
            TradePanelState.AwaitingResponse)
        {
            return;
        }

        if (!ValidateOffer(
                out string validationMessage))
        {
            ShowTemporaryResult(
                "Takas artık geçerli değil.\n" +
                validationMessage);

            return;
        }

        if (!ExecuteTrade())
        {
            ShowTemporaryResult(
                "Takas uygulanamadı. " +
                "Hiçbir değişiklik yapılmadı.");

            return;
        }

        Debug.Log(
            $"{target.DisplayName} accepted the trade " +
            $"from {initiator.DisplayName}.",
            this);

        ShowTemporaryResult(
            "TAKAS KABUL EDİLDİ\n\n" +
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

        Debug.Log(
            $"{target?.DisplayName ?? "Target player"} " +
            $"rejected the trade from " +
            $"{initiator?.DisplayName ?? "initiator"}.",
            this);

        ShowTemporaryResult(
            "TAKAS REDDEDİLDİ");
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
            options.Add(player.DisplayName);
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
            "Mülk teklif etme");

        PopulatePropertyDropdown(
            requestedPropertyDropdown,
            requestedProperties,
            "Mülk talep etme");
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
                ? target.DisplayName
                : "Hedef yok";

        SetSummary(
            $"Takas hedefi: {targetName}\n\n" +
            BuildOfferSummary(
                includeResponsePrompt: false));
    }

    private string BuildOfferSummary(
        bool includeResponsePrompt)
    {
        string offeredPropertyName =
            offeredProperty != null
                ? offeredProperty.DisplayName
                : "Mülk yok";

        string requestedPropertyName =
            requestedProperty != null
                ? requestedProperty.DisplayName
                : "Mülk yok";

        string summary =
            $"{initiator?.DisplayName ?? "Oyuncu"} verir:\n" +
            $"• {offeredPropertyName}\n" +
            $"• {offeredCash} ₵\n\n" +
            $"{target?.DisplayName ?? "Hedef"} verir:\n" +
            $"• {requestedPropertyName}\n" +
            $"• {requestedCash} ₵";

        if (includeResponsePrompt)
        {
            summary +=
                $"\n\n{target.DisplayName}, " +
                "teklifi kabul ediyor musun?";
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
        }

        if (rejectTradeButton != null)
        {
            rejectTradeButton.gameObject.SetActive(
                visible);
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

        targetPlayers.Clear();
        offeredProperties.Clear();
        requestedProperties.Clear();

        if (turnManager != null)
        {
            turnManager.CompleteManagementAction();
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
