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
    CivicProjectArrival,
    MoveBackwardSpaces,
    RandomForwardSpaces,
    RandomBackwardSpaces,
    RandomCivicArrival
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

    // Runtime Event Movement cards use stable IDs. Existing serialized enum values
    // and authored Event Deck assets remain intact.
    private static readonly System.Collections.Generic.Dictionary<string, EventCardDefinition>
        RuntimeMovementCards = new System.Collections.Generic.Dictionary<string, EventCardDefinition>();

    public static EventCardDefinition CreateRuntimeMovementCard(int kind)
    {
        if (kind < 0 || kind > 4) return null;
        string[] ids = { "phase18f_detour", "phase18f_express", "phase18f_wrong_turn", "phase18f_shortcut", "phase18f_civic_invitation" };
        if (RuntimeMovementCards.TryGetValue(ids[kind], out EventCardDefinition existing) && existing != null)
            return existing;
        EventCardDefinition card = CreateInstance<EventCardDefinition>();
        card.hideFlags = HideFlags.DontSave;
        card.cardId = ids[kind];
        card.enabledCard = true;
        card.weight = 1;
        card.category = kind == 1 || kind == 3 || kind == 4 ? EventCardCategory.Positive : EventCardCategory.Negative;
        card.effectType = kind == 0 ? EventCardEffectType.MoveBackwardSpaces :
                          kind == 1 ? EventCardEffectType.MoveForwardSpaces :
                          kind == 2 ? EventCardEffectType.RandomBackwardSpaces :
                          kind == 3 ? EventCardEffectType.RandomForwardSpaces :
                                      EventCardEffectType.RandomCivicArrival;
        card.effectAmount = kind == 0 ? 3 : kind == 1 ? 6 : 0;
        RuntimeMovementCards[ids[kind]] = card;
        return card;
    }

    private bool IsRuntimeMovementCard => !string.IsNullOrEmpty(cardId) &&
        cardId.StartsWith("phase18f_", System.StringComparison.Ordinal);

    private string RuntimeMovementTitle()
    {
        switch (cardId)
        {
            case "phase18f_detour": return AtlasBoardL.R("Unexpected Detour", "Beklenmedik Sapma", "Desvío inesperado", "Détour imprévu", "Unerwarteter Umweg", "예상치 못한 우회", "Неожиданный объезд");
            case "phase18f_express": return AtlasBoardL.R("Express Connection", "Ekspres Bağlantı", "Conexión exprés", "Correspondance express", "Expressverbindung", "급행 연결", "Экспресс-соединение");
            case "phase18f_wrong_turn": return AtlasBoardL.R("Wrong Turn", "Yanlış Dönüş", "Giro equivocado", "Mauvais virage", "Falsche Abzweigung", "잘못된 길", "Неверный поворот");
            case "phase18f_shortcut": return AtlasBoardL.R("Lucky Shortcut", "Şanslı Kestirme", "Atajo afortunado", "Raccourci chanceux", "Glückliche Abkürzung", "행운의 지름길", "Удачный короткий путь");
            default: return AtlasBoardL.R("Civic Project Invitation", "Kamu Projesi Daveti", "Invitación a proyecto cívico", "Invitation à un projet civique", "Einladung zum Civic-Projekt", "시민 프로젝트 초대", "Приглашение к общественному проекту");
        }
    }

    private string RuntimeMovementDescription()
    {
        switch (cardId)
        {
            case "phase18f_detour": return AtlasBoardL.R("Move back 3 spaces.", "3 kare geri git.", "Retrocede 3 casillas.", "Reculez de 3 cases.", "Gehe 3 Felder zurück.", "3칸 뒤로 이동하세요.", "Вернитесь на 3 клетки.");
            case "phase18f_express": return AtlasBoardL.R("Move forward 6 spaces.", "6 kare ileri git.", "Avanza 6 casillas.", "Avancez de 6 cases.", "Gehe 6 Felder vorwärts.", "6칸 앞으로 이동하세요.", "Пройдите на 6 клеток вперёд.");
            case "phase18f_wrong_turn": return AtlasBoardL.R("Move back 2–5 spaces (host roll).", "2–5 kare geri git (host çekilişi).", "Retrocede 2–5 casillas (sorteo del anfitrión).", "Reculez de 2 à 5 cases (tirage de l'hôte).", "Gehe 2–5 Felder zurück (Host-Zufall).", "2~5칸 뒤로 이동 (호스트 추첨).", "Вернитесь на 2–5 клеток (выбор хоста).");
            case "phase18f_shortcut": return AtlasBoardL.R("Move forward 1–6 spaces (host roll).", "1–6 kare ileri git (host çekilişi).", "Avanza 1–6 casillas (sorteo del anfitrión).", "Avancez de 1 à 6 cases (tirage de l'hôte).", "Gehe 1–6 Felder vor (Host-Zufall).", "1~6칸 앞으로 이동 (호스트 추첨).", "Пройдите на 1–6 клеток (выбор хоста).");
            default: return AtlasBoardL.R("Travel to one randomly selected Civic destination; no Start bonus.", "Rastgele bir Civic hedefine git; Start bonusu yok.", "Viaja a un destino cívico aleatorio; sin bono de salida.", "Allez à un projet civique aléatoire ; sans bonus de départ.", "Reise zu einem zufälligen Civic-Ziel, ohne Startbonus.", "무작위 시민 프로젝트로 이동; 출발 보너스 없음.", "Отправляйтесь к случайному Civic-проекту без стартового бонуса.");
        }
    }

    public string CardId => cardId;
    public EventCardCategory Category => category;
    public bool EnabledCard => enabledCard;
    public int Weight => Mathf.Max(1, weight);
    // Runtime Civic invitations do not have localization database assets.
    // Translate their label and destination on demand in all seven languages,
    // preserving the stable civic_event_# ID across locale switches.
    public string Title => IsRuntimeMovementCard ? RuntimeMovementTitle() : effectType == EventCardEffectType.CivicProjectArrival
        ? AtlasBoardL.R("Civic Project Opportunity", "Kamu Projesi Fırsatı",
            "Oportunidad de proyecto cívico", "Occasion de projet civique",
            "Civic-Projektchance", "시민 프로젝트 기회", "Возможность общественного проекта")
        : GetLocalizedOrFallback(TitleLocalizationKey, title);

    public string Description
    {
        get
        {
            if (IsRuntimeMovementCard) return RuntimeMovementDescription();
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
