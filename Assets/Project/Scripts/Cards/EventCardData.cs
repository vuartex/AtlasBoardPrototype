using UnityEngine;

[CreateAssetMenu(
    fileName = "EventCard_New",
    menuName = "Atlas Board/Cards/Event Card")]
public class EventCardData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string cardId;
    [SerializeField] private string title;

    [TextArea(3, 6)]
    [SerializeField] private string description;

    [Header("Effect")]
    [Tooltip("Positive values add money. Negative values remove money.")]
    [SerializeField] private int moneyChange;

    [Header("Random Selection")]
    [SerializeField, Min(1)] private int weight = 1;

    public string CardId => cardId;
    public string Title => title;
    public string Description => description;
    public int MoneyChange => moneyChange;
    public int Weight => weight;
}