using System;
using System.Collections.Generic;

// Phase 19A: display-only content proposal.
// This is NOT the backend item_catalog, an entitlement grant, or an economy authority.
// Never pass PreviewOnly items to commerceTestPurchase.
public static class AtlasBoardShopContentDraft
{
    private sealed class DraftRow
    {
        public string Id, Type;
        public int Price;
        public readonly string[] Names;
        public DraftRow(string id, string type, int price, params string[] names)
        {
            Id = id; Type = type; Price = price; Names = names;
        }
    }

    private static readonly DraftRow[] Rows =
    {
        new DraftRow("board_midnight_azure", "board_theme", 1200, "Midnight Azure", "Gece Mavisi", "Azul Medianoche", "Azur de Minuit", "Mitternachtsblau", "미드나이트 블루", "Полуночная лазурь"),
        new DraftRow("board_ember_maple", "board_theme", 1500, "Ember Maple", "Kor Akçaağaç", "Arce Ardiente", "Érable Ardent", "Glutahorn", "불꽃 단풍", "Пламенный клён"),
        new DraftRow("board_arctic_aurora", "board_theme", 1700, "Arctic Aurora", "Kutup Işığı", "Aurora Ártica", "Aurore Arctique", "Arktische Aurora", "북극 오로라", "Арктическое сияние"),
        new DraftRow("dice_obsidian_gold", "dice_skin", 450, "Obsidian Gold", "Obsidyen Altın", "Oro Obsidiana", "Or Obsidienne", "Obsidiangold", "흑요석 골드", "Обсидиановое золото"),
        new DraftRow("dice_polar_ice", "dice_skin", 550, "Polar Ice", "Kutup Buzu", "Hielo Polar", "Glace Polaire", "Polareis", "극지의 얼음", "Полярный лёд"),
        // Phase 19B: D6 color variants awaiting the licensed Unitypackage import.
        // Visual-only until geometry / face orientation / server catalog pass.
        new DraftRow("dice_ruby_red", "dice_skin", 350, "Ruby Red", "Yakut Kırmızısı", "Rojo Rubí", "Rouge Rubis", "Rubinrot", "루비 레드", "Рубиново-красный"),
        new DraftRow("dice_ocean_blue", "dice_skin", 350, "Ocean Blue", "Okyanus Mavisi", "Azul Océano", "Bleu Océan", "Ozeanblau", "오션 블루", "Океанский синий"),
        new DraftRow("dice_midnight_black", "dice_skin", 400, "Midnight Black", "Gece Siyahı", "Negro Medianoche", "Noir Minuit", "Mitternachtsschwarz", "미드나이트 블랙", "Полуночный чёрный"),
        new DraftRow("dice_graphite_gray", "dice_skin", 400, "Graphite Gray", "Grafit Gri", "Gris Grafito", "Gris Graphite", "Graphitgrau", "그래파이트 그레이", "Графитово-серый"),
        new DraftRow("pawn_knight", "pawn", 300, "Explorer Knight", "Kâşif Şövalye", "Caballero Explorador", "Chevalier Explorateur", "Entdeckerritter", "탐험가 기사", "Рыцарь-исследователь"),
        new DraftRow("pawn_rocket", "pawn", 450, "Atlas Rocket", "Atlas Roketi", "Cohete Atlas", "Fusée Atlas", "Atlas-Rakete", "아틀라스 로켓", "Ракета Атлас"),
        new DraftRow("pawn_rover", "pawn", 550, "Trail Rover", "Yol Gezgini", "Explorador de Rutas", "Rover des Pistes", "Pfad-Rover", "탐사 로버", "Марсоход"),
        new DraftRow("pawn_compass", "pawn", 400, "Golden Compass", "Altın Pusula", "Brújula Dorada", "Boussole Dorée", "Goldener Kompass", "황금 나침반", "Золотой компас"),
        new DraftRow("frame_halloween", "profile_frame", 350, "Halloween Night", "Cadılar Bayramı", "Noche de Halloween", "Nuit d’Halloween", "Halloween-Nacht", "할로윈 밤", "Ночь Хэллоуина"),
        new DraftRow("frame_new_year", "profile_frame", 350, "New Year Lights", "Yılbaşı Işıkları", "Luces de Año Nuevo", "Lumières du Nouvel An", "Neujahrslichter", "새해 불빛", "Новогодние огни"),
        new DraftRow("frame_turkiye", "profile_frame", 400, "Türkiye Flag", "Türk Bayrağı", "Bandera de Turquía", "Drapeau de Turquie", "Türkei-Flagge", "튀르키예 국기", "Флаг Турции"),
        new DraftRow("frame_usa", "profile_frame", 400, "USA Flag", "ABD Bayrağı", "Bandera de EE. UU.", "Drapeau des États-Unis", "USA-Flagge", "미국 국기", "Флаг США"),
        new DraftRow("emote_wave", "emote", 120, "Wave", "El Salla", "Saludar", "Saluer", "Winken", "손 흔들기", "Помахать"),
        new DraftRow("emote_applause", "emote", 140, "Applause", "Alkış", "Aplauso", "Applaudir", "Applaus", "박수", "Аплодисменты"),
        new DraftRow("emote_thinking", "emote", 120, "Thinking", "Düşünüyor", "Pensando", "Réflexion", "Nachdenken", "생각 중", "Думаю"),
        new DraftRow("emote_gg", "emote", 150, "Good Game", "Güzel Oyun", "Buena Partida", "Bien Joué", "Gutes Spiel", "좋은 경기", "Хорошая игра"),
    };

    public static int Count => Rows.Length;

    public static List<AtlasBoardStorefrontItem> CreatePreview()
    {
        List<AtlasBoardStorefrontItem> items =
            new List<AtlasBoardStorefrontItem>(Rows.Length);
        for (int i = 0; i < Rows.Length; ++i)
        {
            DraftRow row = Rows[i];
            items.Add(new AtlasBoardStorefrontItem
            {
                ItemId = row.Id,
                ItemType = row.Type,
                DisplayName = row.Names[0],
                GoldPrice = row.Price,
                AtlasCoinPrice = 0,
                SortOrder = i,
                CatalogVersion = 19,
                Owned = false,
                PreviewOnly = true
            });
        }
        return items;
    }

    // Merge visual-only staged designs with the trusted backend catalog.
    // A server row with the same id always wins, so a preview can never
    // override a real entitlement, price or inventory state.
    public static List<AtlasBoardStorefrontItem> MergeForDisplay(
        IEnumerable<AtlasBoardStorefrontItem> serverItems)
    {
        List<AtlasBoardStorefrontItem> result =
            new List<AtlasBoardStorefrontItem>();
        HashSet<string> present =
            new HashSet<string>(StringComparer.Ordinal);
        if (serverItems != null)
        {
            foreach (AtlasBoardStorefrontItem item in serverItems)
            {
                if (item == null || string.IsNullOrEmpty(item.ItemId) ||
                    !present.Add(item.ItemId)) continue;
                result.Add(item);
            }
        }
        foreach (AtlasBoardStorefrontItem draft in CreatePreview())
            if (present.Add(draft.ItemId)) result.Add(draft);
        return result;
    }

    public static string LocalizedName(string itemId, string languageCode)
    {
        int index = AtlasBoardLocalizationLanguages.IndexOf(languageCode);
        foreach (DraftRow row in Rows)
        {
            if (string.Equals(row.Id, itemId, StringComparison.Ordinal))
                return row.Names[index];
        }
        return itemId ?? string.Empty;
    }

    public static bool HasCompleteLanguages()
    {
        foreach (DraftRow row in Rows)
        {
            if (row.Names == null || row.Names.Length != AtlasBoardLocalizationLanguages.Codes.Length)
                return false;
            foreach (string name in row.Names)
                if (string.IsNullOrWhiteSpace(name)) return false;
        }
        return true;
    }

    public static string PreviewNotice(string languageCode)
    {
        switch (AtlasBoardLocalizationLanguages.Normalize(languageCode))
        {
            case "tr": return "ÜRÜN ÖNİZLEMESİ — Satın alma henüz açık değil. Gold bakiyesi değişmez.";
            case "es": return "VISTA PREVIA — Compras desactivadas; no se descuenta oro.";
            case "fr": return "APERÇU — Achats désactivés ; aucun or débité.";
            case "de": return "VORSCHAU — Käufe deaktiviert; kein Gold wird abgezogen.";
            case "ko": return "미리보기 — 구매 비활성화; 골드가 차감되지 않습니다.";
            case "ru": return "ПРОСМОТР — Покупки отключены, золото не списывается.";
            default: return "PREVIEW ONLY — Purchases disabled; no Gold is charged.";
        }
    }

    public static string PreviewLabel(string languageCode)
    {
        switch (AtlasBoardLocalizationLanguages.Normalize(languageCode))
        {
            case "tr": return "ÖNİZLEME";
            case "es": return "VISTA PREVIA";
            case "fr": return "APERÇU";
            case "de": return "VORSCHAU";
            case "ko": return "미리보기";
            case "ru": return "ПРОСМОТР";
            default: return "PREVIEW";
        }
    }
}
