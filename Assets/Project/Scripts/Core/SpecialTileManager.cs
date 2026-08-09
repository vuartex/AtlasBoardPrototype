using System;
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

    public bool IsResolvingSpecialTile =>
        isResolvingSpecialTile;

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

        UpdateUI(
            title,
            description,
            appliedMoneyChange,
            causedBankruptcy,
            transferredProperties);

        if (specialPanel != null)
        {
            specialPanel.SetActive(true);
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
            specialPanel.SetActive(true);
        }

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

        CompleteSpecialTile();
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

        currentSpecialPlayer = player;
        resolutionCompleted =
            onResolutionCompleted;

        isResolvingSpecialTile = true;

        return true;
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
                "İFLAS\n" +
                $"Ödenen: {Mathf.Abs(appliedMoneyChange)} ₵\n" +
                $"Devredilen/boşa çıkan mülk: " +
                $"{transferredProperties}";

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
                "Para değişmedi";
        }
    }

    private void RefreshContinueButtonAvailability()
    {
        if (continueButton == null)
        {
            return;
        }

        continueButton.interactable =
            currentSpecialPlayer == null ||
            !IsBotPlayer(currentSpecialPlayer);
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
        currentSpecialPlayer = null;

        if (continueButton != null)
        {
            continueButton.interactable = true;
        }

        Action callback =
            resolutionCompleted;

        resolutionCompleted = null;

        callback?.Invoke();
    }
}
