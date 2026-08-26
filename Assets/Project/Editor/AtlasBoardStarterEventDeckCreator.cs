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
        "Atlas Board/Data/Create or Refresh Event Deck v3 (36 Cards)")]
    public static void CreateStarterEventDeck()
    {
        EnsureFolder(
            Root);

        EnsureFolder(
            EventsFolder);

        List<EventCardDefinition> cards =
            new List<EventCardDefinition>();

        cards.Add(
            CreateOrUpdateCard(
                "event_positive_large_order",
                EventCardCategory.Positive,
                10,
                "Büyük Sipariş",
                "Beklenmedik büyük bir sipariş aldın.",
                EventCardEffectType.Money,
                180));

        cards.Add(
            CreateOrUpdateCard(
                "event_positive_busy_season",
                EventCardCategory.Positive,
                12,
                "Yoğun Sezon",
                "İşlerin beklenenden daha iyi gitti.",
                EventCardEffectType.Money,
                120));

        cards.Add(
            CreateOrUpdateCard(
                "event_positive_local_support",
                EventCardCategory.Positive,
                10,
                "Yerel Destek",
                "Yerel bir destek programından ödeme aldın.",
                EventCardEffectType.Money,
                100));

        cards.Add(
            CreateOrUpdateCard(
                "event_positive_investment",
                EventCardCategory.Positive,
                7,
                "Yeni Yatırım",
                "Yeni bir yatırım fırsatı sana ek kaynak sağladı.",
                EventCardEffectType.Money,
                200));

        cards.Add(
            CreateOrUpdateCard(
                "event_positive_savings",
                EventCardCategory.Positive,
                12,
                "Tasarruf Başarısı",
                "Beklenenden düşük giderlerle dönemi kapattın.",
                EventCardEffectType.Money,
                80));

        cards.Add(
            CreateOrUpdateCard(
                "event_positive_campaign",
                EventCardCategory.Positive,
                9,
                "Kampanya Başarısı",
                "Yaptığın kampanya güçlü bir geri dönüş sağladı.",
                EventCardEffectType.Money,
                150));

        cards.Add(
            CreateOrUpdateCard(
                "event_positive_tax_refund",
                EventCardCategory.Positive,
                11,
                "Vergi İadesi",
                "Beklenenden yüksek bir vergi iadesi aldın.",
                EventCardEffectType.Money,
                90));

        cards.Add(
            CreateOrUpdateCard(
                "event_positive_supplier_discount",
                EventCardCategory.Positive,
                10,
                "Tedarikçi İndirimi",
                "Tedarikçin özel bir indirim uyguladı.",
                EventCardEffectType.Money,
                110));

        cards.Add(
            CreateOrUpdateCard(
                "event_positive_viral_post",
                EventCardCategory.Positive,
                9,
                "Viral Paylaşım",
                "Bir paylaşımın beklenmedik şekilde ilgi gördü.",
                EventCardEffectType.Money,
                130));

        cards.Add(
            CreateOrUpdateCard(
                "event_positive_partnership",
                EventCardCategory.Positive,
                8,
                "Yeni Ortaklık",
                "Yeni bir ortaklık ek gelir sağladı.",
                EventCardEffectType.Money,
                160));

        cards.Add(
            CreateOrUpdateCard(
                "event_positive_insurance_refund",
                EventCardCategory.Positive,
                12,
                "Sigorta İadesi",
                "Eski bir dosyadan geri ödeme aldın.",
                EventCardEffectType.Money,
                70));

        cards.Add(
            CreateOrUpdateCard(
                "event_positive_efficiency",
                EventCardCategory.Positive,
                9,
                "Verimli Ay",
                "Operasyonel verimlilik beklenenden fazla tasarruf sağladı.",
                EventCardEffectType.Money,
                140));

        cards.Add(
            CreateOrUpdateCard(
                "event_negative_maintenance",
                EventCardCategory.Negative,
                11,
                "Bakım Masrafı",
                "Beklenmedik bakım giderleri çıktı.",
                EventCardEffectType.Money,
                -100));

        cards.Add(
            CreateOrUpdateCard(
                "event_negative_market_drop",
                EventCardCategory.Negative,
                10,
                "Piyasa Düşüşü",
                "Piyasadaki dalgalanma gelirini etkiledi.",
                EventCardEffectType.Money,
                -120));

        cards.Add(
            CreateOrUpdateCard(
                "event_negative_invoice",
                EventCardCategory.Negative,
                12,
                "Beklenmedik Fatura",
                "Planlanmayan bir işletme faturası geldi.",
                EventCardEffectType.Money,
                -80));

        cards.Add(
            CreateOrUpdateCard(
                "event_negative_damage",
                EventCardCategory.Negative,
                8,
                "Hasar Gideri",
                "Küçük bir hasar için ödeme yapman gerekiyor.",
                EventCardEffectType.Money,
                -150));

        cards.Add(
            CreateOrUpdateCard(
                "event_negative_supply",
                EventCardCategory.Negative,
                11,
                "Tedarik Sorunu",
                "Tedarik zincirindeki aksama ek maliyet çıkardı.",
                EventCardEffectType.Money,
                -100));

        cards.Add(
            CreateOrUpdateCard(
                "event_negative_penalty",
                EventCardCategory.Negative,
                6,
                "Beklenmedik Ceza",
                "Bir ihlal nedeniyle yüksek bir ödeme yapman gerekiyor.",
                EventCardEffectType.Money,
                -200));

        cards.Add(
            CreateOrUpdateCard(
                "event_negative_late_fee",
                EventCardCategory.Negative,
                12,
                "Gecikme Bedeli",
                "Geciken bir ödeme için ek ücret çıktı.",
                EventCardEffectType.Money,
                -60));

        cards.Add(
            CreateOrUpdateCard(
                "event_negative_equipment",
                EventCardCategory.Negative,
                11,
                "Ekipman Tamiri",
                "Bir ekipmanın acil tamire ihtiyacı var.",
                EventCardEffectType.Money,
                -90));

        cards.Add(
            CreateOrUpdateCard(
                "event_negative_delivery",
                EventCardCategory.Negative,
                10,
                "Teslimat Sorunu",
                "Teslimattaki aksama ek maliyet oluşturdu.",
                EventCardEffectType.Money,
                -110));

        cards.Add(
            CreateOrUpdateCard(
                "event_negative_permit",
                EventCardCategory.Negative,
                9,
                "İzin Masrafı",
                "Beklenmedik bir izin ve işlem ücreti çıktı.",
                EventCardEffectType.Money,
                -130));

        cards.Add(
            CreateOrUpdateCard(
                "event_negative_refunds",
                EventCardCategory.Negative,
                8,
                "İade Dalgası",
                "Aynı anda birkaç geri ödeme yapmak zorunda kaldın.",
                EventCardEffectType.Money,
                -160));

        cards.Add(
            CreateOrUpdateCard(
                "event_negative_utility",
                EventCardCategory.Negative,
                12,
                "Yüksek Fatura",
                "Enerji ve hizmet giderleri bu ay yükseldi.",
                EventCardEffectType.Money,
                -70));

        cards.Add(
            CreateOrUpdateCard(
                "event_special_rest_day",
                EventCardCategory.Special,
                6,
                "Zorunlu Mola",
                "Plan dışı bir mola vermen gerekiyor.",
                EventCardEffectType.SkipTurns,
                1));

        cards.Add(
            CreateOrUpdateCard(
                "event_special_move_3",
                EventCardCategory.Special,
                8,
                "Hızlı İlerleme",
                "İşler hızlandı. Üç kare ileri git.",
                EventCardEffectType.MoveForwardSpaces,
                3));

        cards.Add(
            CreateOrUpdateCard(
                "event_special_move_5",
                EventCardCategory.Special,
                5,
                "Büyük Fırsat",
                "Yeni bir fırsat seni öne taşıdı. Beş kare ileri git.",
                EventCardEffectType.MoveForwardSpaces,
                5));

        cards.Add(
            CreateOrUpdateCard(
                "event_special_to_bonus",
                EventCardCategory.Special,
                5,
                "Bonus Rotası",
                "En yakın Bonus karesine doğru ilerle.",
                EventCardEffectType.MoveToNextTileType,
                0,
            TileType.Bonus));

        cards.Add(
            CreateOrUpdateCard(
                "event_special_to_travel",
                EventCardCategory.Special,
                4,
                "Yeni Rota",
                "En yakın Seyahat karesine doğru ilerle.",
                EventCardEffectType.MoveToNextTileType,
                0,
            TileType.Travel));

        cards.Add(
            CreateOrUpdateCard(
                "event_special_to_auction",
                EventCardCategory.Special,
                4,
                "Pazar Fırsatı",
                "En yakın Açık Artırma karesine doğru ilerle.",
                EventCardEffectType.MoveToNextTileType,
                0,
            TileType.Auction));

        cards.Add(
            CreateOrUpdateCard(
                "event_special_move_2",
                EventCardCategory.Special,
                10,
                "Küçük Avantaj",
                "İki kare ileri git.",
                EventCardEffectType.MoveForwardSpaces,
                2));

        cards.Add(
            CreateOrUpdateCard(
                "event_special_move_4",
                EventCardCategory.Special,
                7,
                "Hız Kazandın",
                "Dört kare ileri git.",
                EventCardEffectType.MoveForwardSpaces,
                4));

        cards.Add(
            CreateOrUpdateCard(
                "event_special_move_6",
                EventCardCategory.Special,
                4,
                "Büyük Sıçrama",
                "Altı kare ileri git.",
                EventCardEffectType.MoveForwardSpaces,
                6));

        cards.Add(
            CreateOrUpdateCard(
                "event_special_to_vacation",
                EventCardCategory.Special,
                4,
                "Kısa Tatil",
                "En yakın Tatil karesine doğru ilerle.",
                EventCardEffectType.MoveToNextTileType,
                0,
            TileType.Vacation));

        cards.Add(
            CreateOrUpdateCard(
                "event_special_to_rest",
                EventCardCategory.Special,
                4,
                "Dinlenme Noktası",
                "En yakın Dinlenme karesine doğru ilerle.",
                EventCardEffectType.MoveToNextTileType,
                0,
            TileType.RestArea));

        cards.Add(
            CreateOrUpdateCard(
                "event_special_skip_2",
                EventCardCategory.Special,
                3,
                "Sistem Kesintisi",
                "Beklenmedik bir kesinti nedeniyle iki tur bekle.",
                EventCardEffectType.SkipTurns,
                2));

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
            "Event Deck",
            cards.ToArray());

        EditorUtility.SetDirty(
            deck);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject =
            deck;

        Debug.Log(
            "Atlas Board Event Deck v3 created/updated. " +
            $"Cards: {cards.Count} " +
            "(12 Positive / 12 Negative / 12 Special).");
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

        EditorUtility.SetDirty(
            card);

        return card;
    }

    private static void EnsureFolder(
        string path)
    {
        if (AssetDatabase.IsValidFolder(
                path))
        {
            return;
        }

        string parent =
            Path.GetDirectoryName(
                    path)
                ?.Replace(
                    "\\",
                    "/");

        string folderName =
            Path.GetFileName(
                path);

        if (!string.IsNullOrEmpty(
                parent) &&
            !AssetDatabase.IsValidFolder(
                parent))
        {
            EnsureFolder(
                parent);
        }

        AssetDatabase.CreateFolder(
            parent,
            folderName);
    }
}
#endif
