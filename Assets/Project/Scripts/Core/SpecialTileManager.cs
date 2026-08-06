using System;
using TMPro;
using UnityEngine;

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

    private Action resolutionCompleted;
    private bool isResolvingSpecialTile;

    private void Start()
    {
        if (specialPanel != null)
        {
            specialPanel.SetActive(false);
        }
    }

    public void ResolveMoneyEffect(
        PlayerGameState player,
        string title,
        string description,
        int requestedMoneyChange,
        Action onResolutionCompleted)
    {
        if (!BeginResolution(onResolutionCompleted))
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
        if (!BeginResolution(onResolutionCompleted))
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
        Action onResolutionCompleted)
    {
        if (isResolvingSpecialTile)
        {
            Debug.LogWarning(
                "A special tile is already being resolved.",
                this);

            return false;
        }

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

    private void CompleteSpecialTile()
    {
        if (specialPanel != null)
        {
            specialPanel.SetActive(false);
        }

        isResolvingSpecialTile = false;

        Action callback =
            resolutionCompleted;

        resolutionCompleted = null;

        callback?.Invoke();
    }
}
