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
    MoveToNextTileType,
    CivicProjectArrival
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

    private static readonly System.Collections.Generic.Dictionary<string, EventCardDefinition>
        RuntimeCivicCards = new System.Collections.Generic.Dictionary<string, EventCardDefinition>();

    public static EventCardDefinition CreateRuntimeCivicCard(int projectId, string projectName)
    {
        if (projectId < 0 || projectId >= 3) return null;
        string cacheKey = projectId + ":" + (projectName ?? string.Empty);
        if (RuntimeCivicCards.TryGetValue(cacheKey, out EventCardDefinition existing) && existing != null)
            return existing;
        EventCardDefinition card = CreateInstance<EventCardDefinition>();
        card.hideFlags = HideFlags.DontSave;
        card.cardId = "civic_event_" + projectId;
        card.category = EventCardCategory.Special;
        card.enabledCard = true;
        card.weight = 1;
        card.title = "Civic Project Opportunity";
        card.description = "Travel to " + projectName + " (outside the dice route).";
        card.effectType = EventCardEffectType.CivicProjectArrival;
        card.effectAmount = projectId;
        RuntimeCivicCards[cacheKey] = card;
        return card;
    }

    public string CardId => cardId;
    public EventCardCategory Category => category;
    public bool EnabledCard => enabledCard;
    public int Weight => Mathf.Max(1, weight);
    // Runtime Civic invitations do not have localization database assets.
    // Translate their label and destination on demand in all seven languages,
    // preserving the stable civic_event_# ID across locale switches.
    public string Title => effectType == EventCardEffectType.CivicProjectArrival
        ? AtlasBoardL.R("Civic Project Opportunity", "Kamu Projesi Fırsatı",
            "Oportunidad de proyecto cívico", "Occasion de projet civique",
            "Civic-Projektchance", "시민 프로젝트 기회", "Возможность общественного проекта")
        : GetLocalizedOrFallback(TitleLocalizationKey, title);

    public string Description
    {
        get
        {
            if (effectType != EventCardEffectType.CivicProjectArrival)
                return GetLocalizedOrFallback(DescriptionLocalizationKey, description);
            AtlasBoardCivicProjectManager civic = UnityEngine.Object.FindAnyObjectByType<AtlasBoardCivicProjectManager>();
            string projectName = civic != null ? civic.GetProjectName(effectAmount) : string.Empty;
            if (string.IsNullOrWhiteSpace(projectName)) return description;
            return AtlasBoardL.R("Travel to {0} (outside the dice route).",
                "{0} projesine seyahat et (zar rotasının dışında).",
                "Viaja a {0} (fuera del recorrido de dados).",
                "Voyagez vers {0} (hors du parcours des dés).",
                "Reise nach {0} (außerhalb der Würfelroute).",
                "{0}(으)로 이동하세요 (주사위 경로 외부).",
                "Отправляйтесь в {0} (вне маршрута по кубику).", projectName);
        }
    }

    public string RawTitle =>
        title;

    public string RawDescription =>
        description;

    public string TitleLocalizationKey =>
        string.IsNullOrWhiteSpace(
            cardId)
                ? string.Empty
                : $"event.card.{cardId}.title";

    public string DescriptionLocalizationKey =>
        string.IsNullOrWhiteSpace(
            cardId)
                ? string.Empty
                : $"event.card.{cardId}.description";
    public string RequiredMapId => requiredMapId;
    public EventCardEffectType EffectType => effectType;
    public int EffectAmount => effectAmount;
    public TileType TargetTileType => targetTileType;

    private static string GetLocalizedOrFallback(
        string key,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(
                key))
        {
            return fallback;
        }

        string localized =
            AtlasBoardL.T(
                key);

        return string.Equals(
                   localized,
                   key,
                   System.StringComparison.Ordinal)
            ? fallback
            : localized;
    }

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
