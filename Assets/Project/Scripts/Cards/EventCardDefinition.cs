using UnityEngine;

public enum EventCardCategory
{
    Positive,
    Negative,
    Special
}

public enum EventCardEffectType
{
    Money,
    SkipTurns,
    MoveForwardSpaces,
    MoveToNextTileType
}

[CreateAssetMenu(
    fileName = "EventCard_New",
    menuName = "Atlas Board/Cards/Event Card")]
public class EventCardDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string cardId;

    [SerializeField]
    private EventCardCategory category;

    [SerializeField]
    private bool enabledCard = true;

    [SerializeField, Min(1)]
    private int weight = 10;

    [Header("Display")]
    [SerializeField]
    private string title;

    [SerializeField, TextArea(2, 5)]
    private string description;

    [Header("Map Scope")]
    [Tooltip(
        "Leave empty to allow this card on every map. " +
        "Otherwise use the exact BoardMapDefinition Map Id.")]
    [SerializeField]
    private string requiredMapId;

    [Header("Effect")]
    [SerializeField]
    private EventCardEffectType effectType;

    [Tooltip(
        "Money: signed amount. SkipTurns: positive turns. " +
        "MoveForwardSpaces: positive spaces.")]
    [SerializeField]
    private int effectAmount;

    [Tooltip(
        "Used only by MoveToNextTileType.")]
    [SerializeField]
    private TileType targetTileType = TileType.Bonus;

    public string CardId => cardId;
    public EventCardCategory Category => category;
    public bool EnabledCard => enabledCard;
    public int Weight => Mathf.Max(1, weight);
    public string Title => title;
    public string Description => description;
    public string RequiredMapId => requiredMapId;
    public EventCardEffectType EffectType => effectType;
    public int EffectAmount => effectAmount;
    public TileType TargetTileType => targetTileType;

    public bool IsAvailableForMap(
        string mapId)
    {
        return enabledCard &&
               (string.IsNullOrWhiteSpace(
                    requiredMapId) ||
                string.Equals(
                    requiredMapId,
                    mapId,
                    System.StringComparison.Ordinal));
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        string newCardId,
        EventCardCategory newCategory,
        int newWeight,
        string newTitle,
        string newDescription,
        EventCardEffectType newEffectType,
        int newEffectAmount = 0,
        TileType newTargetTileType = TileType.Bonus,
        string newRequiredMapId = "")
    {
        cardId = newCardId;
        category = newCategory;
        enabledCard = true;
        weight = Mathf.Max(1, newWeight);
        title = newTitle;
        description = newDescription;
        effectType = newEffectType;
        effectAmount = newEffectAmount;
        targetTileType = newTargetTileType;
        requiredMapId = newRequiredMapId;
    }
#endif
}
