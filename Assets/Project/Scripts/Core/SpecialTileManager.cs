using System;
using TMPro;
using UnityEngine;

public class SpecialTileManager : MonoBehaviour
{
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
        if (isResolvingSpecialTile)
        {
            Debug.LogWarning(
                "A special tile is already being resolved.",
                this);

            return;
        }

        resolutionCompleted =
            onResolutionCompleted;

        if (player == null)
        {
            CompleteSpecialTile();
            return;
        }

        isResolvingSpecialTile = true;

        int appliedMoneyChange =
            ApplyMoneyEffect(
                player,
                requestedMoneyChange);

        UpdateUI(
            title,
            description,
            appliedMoneyChange);

        if (specialPanel != null)
        {
            specialPanel.SetActive(true);
        }

        Debug.Log(
            $"{player.DisplayName} resolved special tile " +
            $"'{title}'. Money change: {appliedMoneyChange}.",
            this);
    }

    public void ContinueAfterSpecialTile()
    {
        if (!isResolvingSpecialTile)
        {
            return;
        }

        CompleteSpecialTile();
    }

    private int ApplyMoneyEffect(
        PlayerGameState player,
        int requestedMoneyChange)
    {
        if (requestedMoneyChange > 0)
        {
            player.AddMoney(requestedMoneyChange);
            return requestedMoneyChange;
        }

        if (requestedMoneyChange < 0)
        {
            int requestedLoss =
                Mathf.Abs(requestedMoneyChange);

            int actualLoss =
                Mathf.Min(
                    requestedLoss,
                    player.CurrentMoney);

            player.TrySpend(actualLoss);

            return -actualLoss;
        }

        return 0;
    }

    private void UpdateUI(
        string title,
        string description,
        int appliedMoneyChange)
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