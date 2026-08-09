#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardStarterEventDeckCreator
{
    private const string Root =
        "Assets/Project/Data/Cards";

    private const string EventsFolder =
        Root + "/Events";

    [MenuItem(
        "Atlas Board/Data/Create Starter Event Deck")]
    public static void CreateStarterEventDeck()
    {
        EnsureFolder(Root);
        EnsureFolder(EventsFolder);

        List<EventCardDefinition> cards =
            new List<EventCardDefinition>();

        // Positive
        cards.Add(CreateOrUpdateCard(
            "event_positive_large_order",
            EventCardCategory.Positive,
            10,
            "Büyük Sipariş",
            "Beklenmedik büyük bir sipariş aldın.",
            EventCardEffectType.Money,
            180));

        cards.Add(CreateOrUpdateCard(
            "event_positive_busy_season",
            EventCardCategory.Positive,
            12,
            "Yoğun Sezon",
            "İşlerin beklenenden daha iyi gitti.",
            EventCardEffectType.Money,
            120));

        cards.Add(CreateOrUpdateCard(
            "event_positive_local_support",
            EventCardCategory.Positive,
            10,
            "Yerel Destek",
            "Yerel bir destek programından ödeme aldın.",
            EventCardEffectType.Money,
            100));

        cards.Add(CreateOrUpdateCard(
            "event_positive_investment",
            EventCardCategory.Positive,
            7,
            "Yeni Yatırım",
            "Yeni bir yatırım fırsatı sana ek kaynak sağladı.",
            EventCardEffectType.Money,
            200));

        cards.Add(CreateOrUpdateCard(
            "event_positive_savings",
            EventCardCategory.Positive,
            12,
            "Tasarruf Başarısı",
            "Beklenenden düşük giderlerle dönemi kapattın.",
            EventCardEffectType.Money,
            80));

        cards.Add(CreateOrUpdateCard(
            "event_positive_campaign",
            EventCardCategory.Positive,
            9,
            "Kampanya Başarısı",
            "Yaptığın kampanya güçlü bir geri dönüş sağladı.",
            EventCardEffectType.Money,
            150));

        // Negative
        cards.Add(CreateOrUpdateCard(
            "event_negative_maintenance",
            EventCardCategory.Negative,
            11,
            "Bakım Masrafı",
            "Beklenmedik bakım giderleri çıktı.",
            EventCardEffectType.Money,
            -100));

        cards.Add(CreateOrUpdateCard(
            "event_negative_market_drop",
            EventCardCategory.Negative,
            10,
            "Piyasa Düşüşü",
            "Piyasadaki dalgalanma gelirini etkiledi.",
            EventCardEffectType.Money,
            -120));

        cards.Add(CreateOrUpdateCard(
            "event_negative_invoice",
            EventCardCategory.Negative,
            12,
            "Beklenmedik Fatura",
            "Planlanmayan bir işletme faturası geldi.",
            EventCardEffectType.Money,
            -80));

        cards.Add(CreateOrUpdateCard(
            "event_negative_damage",
            EventCardCategory.Negative,
            8,
            "Hasar Gideri",
            "Küçük bir hasar için ödeme yapman gerekiyor.",
            EventCardEffectType.Money,
            -150));

        cards.Add(CreateOrUpdateCard(
            "event_negative_supply",
            EventCardCategory.Negative,
            11,
            "Tedarik Sorunu",
            "Tedarik zincirindeki aksama ek maliyet çıkardı.",
            EventCardEffectType.Money,
            -100));

        cards.Add(CreateOrUpdateCard(
            "event_negative_penalty",
            EventCardCategory.Negative,
            6,
            "Beklenmedik Ceza",
            "Bir ihlal nedeniyle yüksek bir ödeme yapman gerekiyor.",
            EventCardEffectType.Money,
            -200));

        // Special
        cards.Add(CreateOrUpdateCard(
            "event_special_rest_day",
            EventCardCategory.Special,
            6,
            "Zorunlu Mola",
            "Plan dışı bir mola vermen gerekiyor.",
            EventCardEffectType.SkipTurns,
            1));

        cards.Add(CreateOrUpdateCard(
            "event_special_move_3",
            EventCardCategory.Special,
            8,
            "Hızlı İlerleme",
            "İşler hızlandı. Üç kare ileri git.",
            EventCardEffectType.MoveForwardSpaces,
            3));

        cards.Add(CreateOrUpdateCard(
            "event_special_move_5",
            EventCardCategory.Special,
            5,
            "Büyük Fırsat",
            "Yeni bir fırsat seni öne taşıdı. Beş kare ileri git.",
            EventCardEffectType.MoveForwardSpaces,
            5));

        cards.Add(CreateOrUpdateCard(
            "event_special_to_bonus",
            EventCardCategory.Special,
            5,
            "Bonus Rotası",
            "En yakın Bonus karesine doğru ilerle.",
            EventCardEffectType.MoveToNextTileType,
            0,
            TileType.Bonus));

        cards.Add(CreateOrUpdateCard(
            "event_special_to_travel",
            EventCardCategory.Special,
            4,
            "Yeni Rota",
            "En yakın Seyahat karesine doğru ilerle.",
            EventCardEffectType.MoveToNextTileType,
            0,
            TileType.Travel));

        cards.Add(CreateOrUpdateCard(
            "event_special_to_auction",
            EventCardCategory.Special,
            4,
            "Pazar Fırsatı",
            "En yakın Açık Artırma karesine doğru ilerle.",
            EventCardEffectType.MoveToNextTileType,
            0,
            TileType.Auction));

        string deckPath =
            Root +
            "/EventDeck_Default.asset";

        EventDeckDefinition deck =
            AssetDatabase.LoadAssetAtPath<
                EventDeckDefinition>(
                    deckPath);

        if (deck == null)
        {
            deck =
                ScriptableObject.CreateInstance<
                    EventDeckDefinition>();

            AssetDatabase.CreateAsset(
                deck,
                deckPath);
        }

        deck.EditorConfigure(
            "event_deck_default",
            "Varsayılan Etkinlik Destesi",
            cards.ToArray());

        EditorUtility.SetDirty(deck);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = deck;

        Debug.Log(
            "Atlas Board starter Event Deck created/updated. " +
            $"Cards: {cards.Count}.");
    }

    private static EventCardDefinition
        CreateOrUpdateCard(
            string cardId,
            EventCardCategory category,
            int weight,
            string title,
            string description,
            EventCardEffectType effectType,
            int effectAmount = 0,
            TileType targetTileType =
                TileType.Bonus,
            string requiredMapId = "")
    {
        string path =
            EventsFolder +
            "/" +
            cardId +
            ".asset";

        EventCardDefinition card =
            AssetDatabase.LoadAssetAtPath<
                EventCardDefinition>(
                    path);

        if (card == null)
        {
            card =
                ScriptableObject.CreateInstance<
                    EventCardDefinition>();

            AssetDatabase.CreateAsset(
                card,
                path);
        }

        card.EditorConfigure(
            cardId,
            category,
            weight,
            title,
            description,
            effectType,
            effectAmount,
            targetTileType,
            requiredMapId);

        EditorUtility.SetDirty(card);

        return card;
    }

    private static void EnsureFolder(
        string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent =
            Path.GetDirectoryName(path)
                ?.Replace("\\", "/");

        string folderName =
            Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) &&
            !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(
            parent,
            folderName);
    }
}
#endif
