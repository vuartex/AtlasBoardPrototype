using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpecialTileManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private BankruptcyManager bankruptcyManager;

    [Header("Special Result UI")]
    [SerializeField]
    private GameObject specialPanel;

    [SerializeField]
    private TMP_Text specialTitleText;

    [SerializeField]
    private TMP_Text specialDescriptionText;

    [SerializeField]
    private TMP_Text specialResultText;

    [SerializeField]
    private Button continueButton;

    private Action resolutionCompleted;
    private bool isResolvingSpecialTile;
    private PlayerGameState currentSpecialPlayer;

    // Phase 5E online decision authority. The Host still resolves the actual
    // gameplay effect, while a Remote-owned Human receives a presentation-only
    // prompt and submits only a Continue intent.
    private bool onlineHostAuthorityMode;
    private readonly HashSet<int> onlineLocallyControlledHumanSlots =
        new HashSet<int>();
    private bool remotePresentation;
    private bool remoteContinueRequestPending;

    // Stable descriptor used by the Host-authored match snapshot so every
    // client can rebuild the prompt in its own locale.
    private string onlinePresentationKind = string.Empty;
    private int onlineValue0;
    private int onlineValue1;
    private int onlineValue2;
    private string onlineFallbackTitle = string.Empty;
    private string onlineFallbackDescription = string.Empty;
    private string onlineFallbackResult = string.Empty;

    public event Action<int> RemoteContinueRequested;

    public bool IsResolvingSpecialTile =>
        isResolvingSpecialTile;

    public PlayerGameState CurrentSpecialPlayer =>
        currentSpecialPlayer;

    public bool IsRemotePresentation =>
        remotePresentation;

    public string OnlinePresentationKind =>
        onlinePresentationKind;

    public int OnlineValue0 =>
        onlineValue0;

    public int OnlineValue1 =>
        onlineValue1;

    public int OnlineValue2 =>
        onlineValue2;

    public string OnlineFallbackTitle =>
        onlineFallbackTitle;

    public string OnlineFallbackDescription =>
        onlineFallbackDescription;

    public string OnlineFallbackResult =>
        onlineFallbackResult;

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

    public void SetOnlinePresentationDescriptor(
        string kind,
        int value0 = 0,
        int value1 = 0,
        int value2 = 0)
    {
        onlinePresentationKind =
            kind ?? string.Empty;
        onlineValue0 = value0;
        onlineValue1 = value1;
        onlineValue2 = value2;
    }

    public bool HasPendingSpecialFor(
        PlayerGameState player)
    {
        return isResolvingSpecialTile &&
               player != null &&
               currentSpecialPlayer != null &&
               (currentSpecialPlayer == player ||
                currentSpecialPlayer.PlayerSlotIndex ==
                player.PlayerSlotIndex);
    }

    private void Start()
    {
        if (specialPanel != null)
        {
            specialPanel.SetActive(false);
        }

        if (continueButton != null)
        {
            continueButton.interactable = true;
        }
    }

    public void ResolveMoneyEffect(
        PlayerGameState player,
        string title,
        string description,
        int requestedMoneyChange,
        Action onResolutionCompleted)
    {
        if (!BeginResolution(
                player,
                onResolutionCompleted))
        {
            return;
        }

        if (player == null)
        {
            CompleteSpecialTile();
            return;
        }

        int appliedMoneyChange;
        bool causedBankruptcy = false;
        int transferredProperties = 0;

        if (requestedMoneyChange > 0)
        {
            player.AddMoney(requestedMoneyChange);
            appliedMoneyChange =
                requestedMoneyChange;
        }
        else if (requestedMoneyChange < 0)
        {
            int requestedLoss =
                Mathf.Abs(requestedMoneyChange);

            if (bankruptcyManager != null)
            {
                BankruptcyManager.PaymentResolution result =
                    bankruptcyManager.ResolveMandatoryPayment(
                        player,
                        null,
                        requestedLoss,
                        title);

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

                player.TrySpend(actualLoss);
                appliedMoneyChange = -actualLoss;
            }
        }
        else
        {
            appliedMoneyChange = 0;
        }

        onlineFallbackTitle = title ?? string.Empty;
        onlineFallbackDescription = description ?? string.Empty;

        // Rest keeps value0 as the authoritative turns-to-skip value that was
        // supplied by TileResolutionManager. Money-style special tiles use
        // value0 for the exact amount actually applied by the Host.
        if (!string.Equals(
                onlinePresentationKind,
                "rest",
                StringComparison.Ordinal))
        {
            onlineValue0 = appliedMoneyChange;
        }

        onlineValue1 = causedBankruptcy ? 1 : 0;
        onlineValue2 = transferredProperties;

        UpdateUI(
            title,
            description,
            appliedMoneyChange,
            causedBankruptcy,
            transferredProperties);

        onlineFallbackResult =
            specialResultText != null
                ? specialResultText.text
                : string.Empty;

        if (specialPanel != null)
        {
            specialPanel.SetActive(
                ShouldShowAuthoritativePanelLocally(player));
        }

        RefreshContinueButtonAvailability();

        Debug.Log(
            $"{player.DisplayName} resolved special tile " +
            $"'{title}'. Money change: {appliedMoneyChange}. " +
            $"Bankrupt: {causedBankruptcy}.",
            this);
    }

    public void ShowResultMessage(
        string title,
        string description,
        string result,
        Action onResolutionCompleted)
    {
        ShowResultMessage(
            null,
            title,
            description,
            result,
            onResolutionCompleted);
    }

    public void ShowResultMessage(
        PlayerGameState player,
        string title,
        string description,
        string result,
        Action onResolutionCompleted)
    {
        if (!BeginResolution(
                player,
                onResolutionCompleted))
        {
            return;
        }

        onlineFallbackTitle = title ?? string.Empty;
        onlineFallbackDescription = description ?? string.Empty;
        onlineFallbackResult = result ?? string.Empty;

        if (specialTitleText != null)
        {
            specialTitleText.text = title;
        }

        if (specialDescriptionText != null)
        {
            specialDescriptionText.text =
                description;
        }

        if (specialResultText != null)
        {
            specialResultText.text = result;
        }

        if (specialPanel != null)
        {
            specialPanel.SetActive(
                ShouldShowAuthoritativePanelLocally(player));
        }

        RefreshContinueButtonAvailability();
    }

    public void ShowOnlineRemoteSpecialDecision(
        PlayerGameState player,
        string kind,
        int value0,
        int value1,
        int value2,
        string fallbackTitle,
        string fallbackDescription,
        string fallbackResult)
    {
        if (player == null)
        {
            ClearOnlineRemoteSpecialDecision();
            return;
        }

        bool samePresentation =
            remotePresentation &&
            currentSpecialPlayer != null &&
            currentSpecialPlayer.PlayerSlotIndex ==
                player.PlayerSlotIndex &&
            string.Equals(
                onlinePresentationKind,
                kind ?? string.Empty,
                StringComparison.Ordinal);

        remotePresentation = true;

        if (!samePresentation)
        {
            remoteContinueRequestPending = false;
        }

        isResolvingSpecialTile = true;
        currentSpecialPlayer = player;
        resolutionCompleted = null;

        onlinePresentationKind = kind ?? string.Empty;
        onlineValue0 = value0;
        onlineValue1 = value1;
        onlineValue2 = value2;
        onlineFallbackTitle = fallbackTitle ?? string.Empty;
        onlineFallbackDescription = fallbackDescription ?? string.Empty;
        onlineFallbackResult = fallbackResult ?? string.Empty;

        string title = onlineFallbackTitle;
        string description = onlineFallbackDescription;

        if (string.Equals(
                onlinePresentationKind,
                "tax",
                StringComparison.Ordinal))
        {
            title = AtlasBoardL.T("special.tax.title");
            description = AtlasBoardL.T("special.tax.description");
            UpdateUI(
                title,
                description,
                onlineValue0,
                onlineValue1 != 0,
                onlineValue2);
        }
        else if (string.Equals(
                     onlinePresentationKind,
                     "bonus",
                     StringComparison.Ordinal))
        {
            title = AtlasBoardL.T("special.bonus.title");
            description = AtlasBoardL.T("special.bonus.description");
            UpdateUI(
                title,
                description,
                onlineValue0,
                onlineValue1 != 0,
                onlineValue2);
        }
        else if (string.Equals(
                     onlinePresentationKind,
                     "vacation",
                     StringComparison.Ordinal))
        {
            title = AtlasBoardL.T("special.vacation.title");
            description = AtlasBoardL.T("special.vacation.description");
            UpdateUI(
                title,
                description,
                onlineValue0,
                onlineValue1 != 0,
                onlineValue2);
        }
        else if (string.Equals(
                     onlinePresentationKind,
                     "rest",
                     StringComparison.Ordinal))
        {
            title = AtlasBoardL.T("special.rest.title");
            description =
                onlineValue0 == 1
                    ? AtlasBoardL.T("special.rest.skip_one")
                    : AtlasBoardL.T(
                        "special.rest.skip_many",
                        Mathf.Max(1, onlineValue0));

            UpdateUI(
                title,
                description,
                0,
                false,
                0);
        }
        else
        {
            if (specialTitleText != null)
            {
                specialTitleText.text = title;
            }

            if (specialDescriptionText != null)
            {
                specialDescriptionText.text = description;
            }

            if (specialResultText != null)
            {
                specialResultText.text = onlineFallbackResult;
            }
        }

        if (specialPanel != null)
        {
            specialPanel.SetActive(true);
        }

        RefreshContinueButtonAvailability();
    }

    public void ClearOnlineRemoteSpecialDecision()
    {
        if (!remotePresentation)
        {
            return;
        }

        remotePresentation = false;
        remoteContinueRequestPending = false;
        isResolvingSpecialTile = false;
        currentSpecialPlayer = null;
        resolutionCompleted = null;
        ClearOnlineDescriptor();

        if (specialPanel != null)
        {
            specialPanel.SetActive(false);
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

    public bool TryResolveBotContinue(
        PlayerGameState player)
    {
        if (!HasPendingSpecialFor(player) ||
            !IsBotPlayer(player))
        {
            return false;
        }

        ContinueAfterSpecialTile();
        return true;
    }

    public void ContinueAfterSpecialTile()
    {
        if (!isResolvingSpecialTile)
        {
            return;
        }

        if (TrySubmitOnlineRemoteContinue())
        {
            return;
        }

        CompleteSpecialTile();
    }

    private bool TrySubmitOnlineRemoteContinue()
    {
        if (!remotePresentation)
        {
            return false;
        }

        if (remoteContinueRequestPending ||
            currentSpecialPlayer == null)
        {
            return true;
        }

        Action<int> callback =
            RemoteContinueRequested;

        if (callback == null)
        {
            Debug.LogWarning(
                "Remote special-tile Continue has no online subscriber.",
                this);
            return true;
        }

        remoteContinueRequestPending = true;
        RefreshContinueButtonAvailability();
        callback.Invoke(
            currentSpecialPlayer.PlayerSlotIndex);

        return true;
    }

    private bool BeginResolution(
        PlayerGameState player,
        Action onResolutionCompleted)
    {
        if (isResolvingSpecialTile)
        {
            Debug.LogWarning(
                "A special tile is already being resolved.",
                this);

            return false;
        }

        remotePresentation = false;
        remoteContinueRequestPending = false;
        currentSpecialPlayer = player;
        resolutionCompleted =
            onResolutionCompleted;

        isResolvingSpecialTile = true;

        return true;
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

    private void UpdateUI(
        string title,
        string description,
        int appliedMoneyChange,
        bool causedBankruptcy,
        int transferredProperties)
    {
        if (specialTitleText != null)
        {
            specialTitleText.text = title;
        }

        if (specialDescriptionText != null)
        {
            specialDescriptionText.text =
                description;
        }

        if (specialResultText == null)
        {
            return;
        }

        if (causedBankruptcy)
        {
            specialResultText.text =
                AtlasBoardL.T(
                    "special.result.bankrupt",
                    Mathf.Abs(
                        appliedMoneyChange),
                    transferredProperties);

            return;
        }

        if (appliedMoneyChange > 0)
        {
            specialResultText.text =
                $"+{appliedMoneyChange} ₵";
        }
        else if (appliedMoneyChange < 0)
        {
            specialResultText.text =
                $"{appliedMoneyChange} ₵";
        }
        else
        {
            specialResultText.text =
                AtlasBoardL.T(
                    "special.result.no_money_change");
        }
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
                !remoteContinueRequestPending;
            return;
        }

        bool locallyOwnedHuman =
            currentSpecialPlayer == null ||
            !onlineHostAuthorityMode ||
            onlineLocallyControlledHumanSlots.Contains(
                currentSpecialPlayer.PlayerSlotIndex);

        continueButton.interactable =
            locallyOwnedHuman &&
            (currentSpecialPlayer == null ||
             !IsBotPlayer(currentSpecialPlayer));
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

    private void CompleteSpecialTile()
    {
        if (specialPanel != null)
        {
            specialPanel.SetActive(false);
        }

        isResolvingSpecialTile = false;
        remotePresentation = false;
        remoteContinueRequestPending = false;
        currentSpecialPlayer = null;

        if (continueButton != null)
        {
            continueButton.interactable = true;
        }

        ClearOnlineDescriptor();

        Action callback =
            resolutionCompleted;

        resolutionCompleted = null;

        callback?.Invoke();
    }

    private void ClearOnlineDescriptor()
    {
        onlinePresentationKind = string.Empty;
        onlineValue0 = 0;
        onlineValue1 = 0;
        onlineValue2 = 0;
        onlineFallbackTitle = string.Empty;
        onlineFallbackDescription = string.Empty;
        onlineFallbackResult = string.Empty;
    }
    public void ResetForNewMatchSession()
    {
        resolutionCompleted = null;
        isResolvingSpecialTile = false;
        currentSpecialPlayer = null;
        remotePresentation = false;
        remoteContinueRequestPending = false;

        if (specialPanel != null) specialPanel.SetActive(false);
        if (continueButton != null) continueButton.interactable = true;
    }

}
