using UnityEngine;

[CreateAssetMenu(
    fileName = "BotPersonality_New",
    menuName = "Atlas Board/Bots/Bot Personality Profile")]
public class BotPersonalityProfile : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string personalityId = "balanced";

    [SerializeField]
    private string displayName = "Dengeli";

    [SerializeField, TextArea(2, 4)]
    private string description =
        "Dengeli nakit, yatırım ve risk davranışı.";

    [Header("Risk / Cash")]
    [Tooltip(
        "Higher = holds more cash. Lower = accepts more liquidity risk.")]
    [SerializeField, Range(0.4f, 1.8f)]
    private float cashReserveMultiplier = 1f;

    [Header("Investment Personality")]
    [SerializeField, Range(0.5f, 1.5f)]
    private float purchaseWillingness = 1f;

    [SerializeField, Range(0.5f, 1.5f)]
    private float auctionWillingness = 1f;

    [SerializeField, Range(0.5f, 1.5f)]
    private float developmentWillingness = 1f;

    [Header("Trade Personality")]
    [SerializeField, Range(0.25f, 2f)]
    private float tradeFrequencyMultiplier = 1f;

    [Tooltip(
        "Higher = more demanding before accepting a trade. " +
        "Lower = more flexible/aggressive.")]
    [SerializeField, Range(0.75f, 1.25f)]
    private float tradeAcceptanceRatioMultiplier = 1f;

    [SerializeField, Range(0.5f, 1.6f)]
    private float groupCompletionFocus = 1f;

    [Header("Travel Personality")]
    [SerializeField, Range(0.5f, 1.5f)]
    private float travelWillingness = 1f;

    [Header("Situational Adaptation")]
    [Tooltip(
        "0 = fixed personality. 1 = strongly reacts to cash and " +
        "relative net-worth position.")]
    [SerializeField, Range(0f, 1f)]
    private float adaptiveStrength;

    public string PersonalityId => personalityId;
    public string DisplayName => displayName;
    public string Description => description;
    public float CashReserveMultiplier =>
        cashReserveMultiplier;
    public float PurchaseWillingness =>
        purchaseWillingness;
    public float AuctionWillingness =>
        auctionWillingness;
    public float DevelopmentWillingness =>
        developmentWillingness;
    public float TradeFrequencyMultiplier =>
        tradeFrequencyMultiplier;
    public float TradeAcceptanceRatioMultiplier =>
        tradeAcceptanceRatioMultiplier;
    public float GroupCompletionFocus =>
        groupCompletionFocus;
    public float TravelWillingness =>
        travelWillingness;
    public float AdaptiveStrength =>
        adaptiveStrength;

#if UNITY_EDITOR
    public void EditorConfigure(
        string newPersonalityId,
        string newDisplayName,
        string newDescription,
        float newCashReserveMultiplier,
        float newPurchaseWillingness,
        float newAuctionWillingness,
        float newDevelopmentWillingness,
        float newTradeFrequencyMultiplier,
        float newTradeAcceptanceRatioMultiplier,
        float newGroupCompletionFocus,
        float newTravelWillingness,
        float newAdaptiveStrength)
    {
        personalityId = newPersonalityId;
        displayName = newDisplayName;
        description = newDescription;
        cashReserveMultiplier = newCashReserveMultiplier;
        purchaseWillingness = newPurchaseWillingness;
        auctionWillingness = newAuctionWillingness;
        developmentWillingness = newDevelopmentWillingness;
        tradeFrequencyMultiplier = newTradeFrequencyMultiplier;
        tradeAcceptanceRatioMultiplier =
            newTradeAcceptanceRatioMultiplier;
        groupCompletionFocus = newGroupCompletionFocus;
        travelWillingness = newTravelWillingness;
        adaptiveStrength = newAdaptiveStrength;
    }
#endif
}
