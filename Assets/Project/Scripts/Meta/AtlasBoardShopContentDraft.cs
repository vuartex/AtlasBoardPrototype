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
        new DraftRow("map_germany", "map", 30000, "Germany", "Almanya", "Alemania", "Allemagne", "Deutschland", "독일", "Германия"),
        new DraftRow("map_mexico", "map", 25000, "Mexico", "Meksika", "México", "Mexique", "Mexiko", "멕시코", "Мексика"),
        new DraftRow("board_midnight_azure", "board_theme", 1200, "Midnight Azure", "Gece Mavisi", "Azul Medianoche", "Azur de Minuit", "Mitternachtsblau", "미드나이트 블루", "Полуночная лазурь"),
        new DraftRow("board_ember_maple", "board_theme", 1500, "Ember Maple", "Kor Akçaağaç", "Arce Ardiente", "Érable Ardent", "Glutahorn", "불꽃 단풍", "Пламенный клён"),
        new DraftRow("board_arctic_aurora", "board_theme", 1700, "Arctic Aurora", "Kutup Işığı", "Aurora Ártica", "Aurore Arctique", "Arktische Aurora", "북극 오로라", "Арктическое сияние"),
        // Phase 19B: D6 color variants awaiting the licensed Unitypackage import.
        // Visual-only until geometry / face orientation / server catalog pass.
        new DraftRow("dice_ruby_red", "dice_skin", 350, "Ruby Red", "Yakut Kırmızısı", "Rojo Rubí", "Rouge Rubis", "Rubinrot", "루비 레드", "Рубиново-красный"),
        new DraftRow("dice_ocean_blue", "dice_skin", 350, "Ocean Blue", "Okyanus Mavisi", "Azul Océano", "Bleu Océan", "Ozeanblau", "오션 블루", "Океанский синий"),
        new DraftRow("dice_midnight_black", "dice_skin", 400, "Midnight Black", "Gece Siyahı", "Negro Medianoche", "Noir Minuit", "Mitternachtsschwarz", "미드나이트 블랙", "Полуночный чёрный"),
        new DraftRow("dice_marble", "dice_skin", 400, "Marble", "Mermer", "Mármol", "Marbre", "Marmor", "대리석", "Мрамор"),
        new DraftRow("ghost_01", "pawn", 300, "Little Ghost 1", "Küçük Hayalet 1", "Fantasma pequeño 1", "Petit fantôme 1", "Kleiner Geist 1", "작은 유령 1", "Маленькое привидение 1"),
        new DraftRow("ghost_02", "pawn", 300, "Little Ghost 2", "Küçük Hayalet 2", "Fantasma pequeño 2", "Petit fantôme 2", "Kleiner Geist 2", "작은 유령 2", "Маленькое привидение 2"),
        new DraftRow("ghost_03", "pawn", 300, "Little Ghost 3", "Küçük Hayalet 3", "Fantasma pequeño 3", "Petit fantôme 3", "Kleiner Geist 3", "작은 유령 3", "Маленькое привидение 3"),
        new DraftRow("ghost_04", "pawn", 300, "Little Ghost 4", "Küçük Hayalet 4", "Fantasma pequeño 4", "Petit fantôme 4", "Kleiner Geist 4", "작은 유령 4", "Маленькое привидение 4"),
        new DraftRow("ghost_05", "pawn", 300, "Little Ghost 5", "Küçük Hayalet 5", "Fantasma pequeño 5", "Petit fantôme 5", "Kleiner Geist 5", "작은 유령 5", "Маленькое привидение 5"),
        new DraftRow("ghost_06", "pawn", 300, "Little Ghost 6", "Küçük Hayalet 6", "Fantasma pequeño 6", "Petit fantôme 6", "Kleiner Geist 6", "작은 유령 6", "Маленькое привидение 6"),
        new DraftRow("ghost_07", "pawn", 300, "Little Ghost 7", "Küçük Hayalet 7", "Fantasma pequeño 7", "Petit fantôme 7", "Kleiner Geist 7", "작은 유령 7", "Маленькое привидение 7"),
        new DraftRow("vehicle_police", "pawn", 450, "Police Car", "Polis Arabası", "Coche de policía", "Voiture de police", "Polizeiauto", "경찰차", "Полицейская машина"),
        new DraftRow("vehicle_taxi", "pawn", 400, "Taxi", "Taksi", "Taxi", "Taxi", "Taxi", "택시", "Такси"),
        new DraftRow("vehicle_pickup", "pawn", 425, "Pickup Truck", "Kamyonet", "Camioneta", "Pick-up", "Pickup", "픽업트럭", "Пикап"),
        new DraftRow("pawn_knight", "pawn", 300, "Explorer Knight", "Kâşif Şövalye", "Caballero Explorador", "Chevalier Explorateur", "Entdeckerritter", "탐험가 기사", "Рыцарь-исследователь"),
        new DraftRow("pawn_rocket", "pawn", 450, "Atlas Rocket", "Atlas Roketi", "Cohete Atlas", "Fusée Atlas", "Atlas-Rakete", "아틀라스 로켓", "Ракета Атлас"),
        new DraftRow("pawn_rover", "pawn", 550, "Trail Rover", "Yol Gezgini", "Explorador de Rutas", "Rover des Pistes", "Pfad-Rover", "탐사 로버", "Марсоход"),
        new DraftRow("pawn_compass", "pawn", 400, "Golden Compass", "Altın Pusula", "Brújula Dorada", "Boussole Dorée", "Goldener Kompass", "황금 나침반", "Золотой компас"),
        new DraftRow("frame_halloween", "profile_frame", 350, "Halloween Night", "Cadılar Bayramı", "Noche de Halloween", "Nuit d’Halloween", "Halloween-Nacht", "할로윈 밤", "Ночь Хэллоуина"),
        new DraftRow("frame_new_year", "profile_frame", 350, "New Year Lights", "Yılbaşı Işıkları", "Luces de Año Nuevo", "Lumières du Nouvel An", "Neujahrslichter", "새해 불빛", "Новогодние огни"),
        new DraftRow("frame_turkiye", "profile_frame", 400, "Türkiye Flag", "Türk Bayrağı", "Bandera de Turquía", "Drapeau de Turquie", "Türkei-Flagge", "튀르키예 국기", "Флаг Турции"),
        new DraftRow("frame_usa", "profile_frame", 400, "USA Flag", "ABD Bayrağı", "Bandera de EE. UU.", "Drapeau des États-Unis", "USA-Flagge", "미국 국기", "Флаг США"),
        new DraftRow("frame_neon_circuit", "profile_frame", 450, "Neon Circuit", "Neon Devre", "Circuito Neón", "Circuit Néon", "Neonschaltkreis", "네온 회로", "Неоновая схема"),
        new DraftRow("frame_royal_crown", "profile_frame", 500, "Royal Crown", "Kraliyet Tacı", "Corona Real", "Couronne Royale", "Königskrone", "왕관", "Королевская корона"),
        new DraftRow("frame_ocean_tide", "profile_frame", 450, "Ocean Tide", "Okyanus Dalgası", "Marea Oceánica", "Marée Océanique", "Meeresflut", "바다의 물결", "Океанский прилив"),
        new DraftRow("frame_sakura_blossom", "profile_frame", 475, "Sakura Blossom", "Sakura Çiçeği", "Flor de Sakura", "Fleur de Sakura", "Sakurablüte", "벚꽃", "Цветущая сакура"),
        new DraftRow("frame_cosmic_nova", "profile_frame", 550, "Cosmic Nova", "Kozmik Nova", "Nova Cósmica", "Nova Cosmique", "Kosmische Nova", "우주 신성", "Космическая новая"),
        new DraftRow("frame_forest_spirit", "profile_frame", 475, "Forest Spirit", "Orman Ruhu", "Espíritu del Bosque", "Esprit de la Forêt", "Waldgeist", "숲의 정령", "Лесной дух"),



    };

    public static int Count => Rows.Length;

    public static List<AtlasBoardStorefrontItem> CreatePreview()
    {
        List<AtlasBoardStorefrontItem> items =
            new List<AtlasBoardStorefrontItem>(Rows.Length);
        for (int i = 0; i < Rows.Length; ++i)
        {
            DraftRow row = Rows[i];
            // Older paper-only pawn proposals are not real models or sale items.
            // Keep their design definitions for future work; do not show them
            // alongside the 10 imported buyable ghost and vehicle models.
            if (row.Id == "pawn_knight" || row.Id == "pawn_rocket" ||
                row.Id == "pawn_rover" || row.Id == "pawn_compass")
                continue;
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
        if (AtlasBoardEmotes.IsEmote(itemId)) return AtlasBoardEmotes.Name(itemId, languageCode);
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
