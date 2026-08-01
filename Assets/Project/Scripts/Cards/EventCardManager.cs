using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EventCardManager : MonoBehaviour
{
    [Header("Card Pool")]
    [SerializeField]
    private EventCardData[] eventCards;

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

    private Action resolutionCompleted;
    private bool isResolvingEvent;

    private void Start()
    {
        if (eventPanel != null)
        {
            eventPanel.SetActive(false);
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

        resolutionCompleted = onResolutionCompleted;

        if (player == null)
        {
            CompleteEvent();
            return;
        }

        EventCardData selectedCard =
            SelectWeightedRandomCard();

        if (selectedCard == null)
        {
            Debug.LogWarning(
                "No valid event cards are configured.",
                this);

            CompleteEvent();
            return;
        }

        isResolvingEvent = true;

        int appliedMoneyChange =
            ApplyMoneyEffect(
                player,
                selectedCard.MoneyChange);

        UpdateEventUI(
            selectedCard,
            appliedMoneyChange);

        if (eventPanel != null)
        {
            eventPanel.SetActive(true);
        }

        Debug.Log(
            $"{player.DisplayName} drew event card " +
            $"'{selectedCard.Title}'. " +
            $"Money change: {appliedMoneyChange}.",
            this);
    }

    public void ContinueAfterEvent()
    {
        if (!isResolvingEvent)
        {
            return;
        }

        CompleteEvent();
    }

    private EventCardData SelectWeightedRandomCard()
    {
        if (eventCards == null ||
            eventCards.Length == 0)
        {
            return null;
        }

        int totalWeight = 0;

        foreach (EventCardData card in eventCards)
        {
            if (card != null)
            {
                totalWeight += Mathf.Max(1, card.Weight);
            }
        }

        if (totalWeight <= 0)
        {
            return null;
        }

        int randomValue =
            UnityEngine.Random.Range(0, totalWeight);

        int cumulativeWeight = 0;

        foreach (EventCardData card in eventCards)
        {
            if (card == null)
            {
                continue;
            }

            cumulativeWeight +=
                Mathf.Max(1, card.Weight);

            if (randomValue < cumulativeWeight)
            {
                return card;
            }
        }

        return null;
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

    private void UpdateEventUI(
        EventCardData card,
        int appliedMoneyChange)
    {
        if (eventTitleText != null)
        {
            eventTitleText.text =
                card.Title;
        }

        if (eventDescriptionText != null)
        {
            eventDescriptionText.text =
                card.Description;
        }

        if (eventResultText != null)
        {
            if (appliedMoneyChange > 0)
            {
                eventResultText.text =
                    $"+{appliedMoneyChange} ₵";
            }
            else if (appliedMoneyChange < 0)
            {
                eventResultText.text =
                    $"{appliedMoneyChange} ₵";
            }
            else
            {
                eventResultText.text =
                    "Para değişmedi";
            }
        }
    }

    private void CompleteEvent()
    {
        if (eventPanel != null)
        {
            eventPanel.SetActive(false);
        }

        isResolvingEvent = false;

        Action callback =
            resolutionCompleted;

        resolutionCompleted = null;

        callback?.Invoke();
    }
}