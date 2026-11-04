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
        // Event-only unlocks. Zero price is NOT a free purchase.
        new DraftRow("map_world", "map", 0, "World", "Dünya", "Mundo", "Monde", "Welt", "세계", "Мир"),
        new DraftRow("board_atlas_horizon", "board_theme", 0, "Atlas Horizon Board", "Atlas Ufuk Tahtası", "Tablero Horizonte Atlas", "Plateau Horizon Atlas", "Atlas-Horizont-Spielbrett", "아틀라스 지평선 보드", "Доска Горизонт Атласа"),
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

    // Seven-language event UI string catalog; uses the existing locale selector.
    // No duplicate localization manager, no client-side reward authority.
    private static readonly Dictionary<string, string[]> WorldStrings =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        {"title", new [] {"Atlas World Journey", "Atlas Dünya Yolculuğu", "Viaje Mundial Atlas", "Voyage Mondial Atlas", "Atlas-Weltreise", "아틀라스 세계 여행", "Мировое путешествие Атласа"}},
        {"subtitle", new [] {"Explore the world, complete missions and unlock exclusive rewards.", "Dünyayı keşfet, görevleri tamamla ve özel ödülleri kazan.", "Explora el mundo, completa misiones y gana premios exclusivos.", "Explorez le monde, terminez des missions et gagnez des récompenses exclusives.", "Erkunde die Welt, erfülle Missionen und gewinne exklusive Belohnungen.", "세계를 탐험하고 미션을 완료하여 특별 보상을 받으세요.", "Исследуйте мир, выполняйте задания и получайте особые награды."}},
        {"overview", new [] {"OVERVIEW", "GENEL BAKIŞ", "RESUMEN", "APERÇU", "ÜBERSICHT", "개요", "ОБЗОР"}},
        {"missions", new [] {"MISSIONS", "GÖREVLER", "MISIONES", "MISSIONS", "MISSIONEN", "임무", "ЗАДАНИЯ"}},
        {"rewards", new [] {"REWARDS", "ÖDÜLLER", "RECOMPENSAS", "RÉCOMPENSES", "BELOHNUNGEN", "보상", "НАГРАДЫ"}},
        {"history", new [] {"CLAIM HISTORY", "ALIM GEÇMİŞİ", "HISTORIAL", "HISTORIQUE", "VERLAUF", "수령 내역", "ИСТОРИЯ"}},
        {"ends", new [] {"Event ends", "Etkinlik bitişi", "El evento termina", "Fin de l’événement", "Event endet", "이벤트 종료", "Окончание события"}},
        {"starts", new [] {"Event starts", "Etkinlik başlangıcı", "Inicio del evento", "Début de l’événement", "Event beginnt", "이벤트 시작", "Начало события"}},
        {"status_upcoming", new [] {"COMING SOON", "YAKINDA BAŞLAYACAK", "PRÓXIMAMENTE", "BIENTÔT", "BALD", "곧 시작", "СКОРО"}},
        {"progress", new [] {"Completed missions", "Tamamlanan görevler", "Misiones completadas", "Missions terminées", "Abgeschlossene Missionen", "완료한 임무", "Завершено заданий"}},
        {"claimed_progress", new [] {"Claimed rewards", "Alınan ödüller", "Recompensas cobradas", "Récompenses récupérées", "Beanspruchte Belohnungen", "받은 보상", "Получено наград"}},
        {"claim", new [] {"CLAIM", "ÖDÜLÜ AL", "RECLAMAR", "RÉCUPÉRER", "ABHOLEN", "받기", "ЗАБРАТЬ"}},
        {"claimed", new [] {"CLAIMED", "ALINDI", "RECLAMADO", "RÉCUPÉRÉ", "ERHALTEN", "수령함", "ПОЛУЧЕНО"}},
        {"locked", new [] {"LOCKED", "KİLİTLİ", "BLOQUEADO", "VERROUILLÉ", "GESPERRT", "잠김", "ЗАКРЫТО"}},
        {"ready", new [] {"READY TO CLAIM", "ALINMAYA HAZIR", "LISTO PARA RECLAMAR", "PRÊT À RÉCUPÉRER", "ABHOLBEREIT", "수령 가능", "ГОТОВО К ПОЛУЧЕНИЮ"}},
        {"bonus", new [] {"COMPLETION BONUS", "TAMAMLAMA BONUSU", "BONO FINAL", "BONUS FINAL", "ABSCHLUSSBONUS", "완료 보너스", "БОНУС ЗА ЗАВЕРШЕНИЕ"}},
        {"world_map_label", new [] {"World Map", "Dünya Haritası", "Mapa Mundial", "Carte du monde", "Weltkarte", "세계 지도", "Карта мира"}},
        {"bonus_desc", new [] {"Claim the exclusive Atlas Horizon Board after claiming all eight rewards.", "Sekiz ödülün tamamını aldıktan sonra özel Atlas Ufuk Tahtası’nı kazan.", "Reclama el Tablero Horizonte Atlas después de las ocho recompensas.", "Récupérez le Plateau Horizon Atlas après les huit récompenses.", "Erhalte das Atlas-Horizont-Spielbrett nach allen acht Belohnungen.", "8개의 보상을 모두 받은 뒤 특별한 보드를 받으세요.", "Заберите особую доску после получения всех восьми наград."}},
        {"world_reward", new [] {"WORLD MAP · EXCLUSIVE", "DÜNYA HARİTASI · ÖZEL", "MAPA MUNDIAL · EXCLUSIVO", "CARTE DU MONDE · EXCLUSIVE", "WELTKARTE · EXKLUSIV", "세계 지도 · 특별", "КАРТА МИРА · ОСОБАЯ"}},
        {"no_history", new [] {"No rewards claimed yet.", "Henüz ödül alınmadı.", "Aún no hay recompensas.", "Aucune récompense pour le moment.", "Noch keine Belohnungen abgeholt.", "아직 받은 보상이 없습니다.", "Наград пока нет."}},
        {"transaction", new [] {"Transaction ID", "İşlem Kimliği", "ID de transacción", "ID de transaction", "Transaktions-ID", "거래 ID", "ID транзакции"}},
        {"mission.two_matches", new [] {"Complete 2 online matches", "2 online maç tamamla", "Completa 2 partidas online", "Terminez 2 parties en ligne", "Beende 2 Online-Partien", "온라인 경기 2회 완료", "Завершите 2 онлайн-матча"}},
        {"mission.fifty_rolls", new [] {"Roll dice 50 times", "50 kez zar at", "Lanza el dado 50 veces", "Lancez les dés 50 fois", "Würfle 50-mal", "주사위 50번 굴리기", "Бросьте кубики 50 раз"}},
        {"mission.ten_developments", new [] {"Build 10 development levels", "10 geliştirme seviyesi kur", "Construye 10 niveles de desarrollo", "Construisez 10 niveaux", "Baue 10 Entwicklungsstufen", "개발 레벨 10회 건설", "Постройте 10 уровней развития"}},
        {"mission.three_wins", new [] {"Win 3 online matches", "3 online maç kazan", "Gana 3 partidas online", "Gagnez 3 parties en ligne", "Gewinne 3 Online-Partien", "온라인 경기 3회 승리", "Победите в 3 онлайн-матчах"}},
        {"mission.twenty_turns", new [] {"Play 20 turns", "20 tur oyna", "Juega 20 turnos", "Jouez 20 tours", "Spiele 20 Züge", "20턴 플레이", "Сыграйте 20 ходов"}},
        {"mission.three_maps", new [] {"Play on 3 different maps", "3 farklı haritada oyna", "Juega en 3 mapas diferentes", "Jouez sur 3 cartes différentes", "Spiele auf 3 Karten", "서로 다른 지도 3개 플레이", "Сыграйте на 3 разных картах"}},
        {"mission.networth_5000", new [] {"Reach 5,000 net worth", "5.000 net değere ulaş", "Alcanza 5000 de patrimonio", "Atteignez 5 000 de valeur nette", "Erreiche 5.000 Vermögen", "순자산 5,000 달성", "Достигните капитала 5 000"}},
        {"mission.world_explorer", new [] {"Claim the previous 7 rewards", "Önceki 7 ödülü al", "Reclama los 7 premios anteriores", "Récupérez les 7 récompenses", "Hole die ersten 7 Belohnungen ab", "이전 보상 7개 수령", "Получите первые 7 наград"}},
        {"progress_unavailable", new [] {"Progress will appear once your online match results are verified.", "Çevrim içi maç sonuçların doğrulanınca ilerlemen burada görünecek.", "El progreso aparecerá cuando se verifiquen tus partidas online.", "La progression apparaîtra après la vérification des parties en ligne.", "Der Fortschritt erscheint nach Prüfung deiner Online-Spiele.", "온라인 경기 결과가 확인되면 진행도가 표시됩니다.", "Прогресс появится после проверки онлайн-матчей."}},
        {"history_unavailable", new [] {"Reward history unavailable", "Ödül geçmişi doğrulanamadı", "Historial no disponible", "Historique indisponible", "Verlauf nicht verfügbar", "보상 기록을 불러올 수 없습니다", "История наград недоступна"}},
        {"history_unavailable_body", new [] {"We couldn't check your claim history right now. Your saved rewards have not been changed.", "Alım geçmişin şu an kontrol edilemiyor. Kayıtlı ödüllerinde değişiklik yapılmadı.", "No pudimos comprobar tu historial. Tus premios no cambiaron.", "Historique inaccessible pour le moment. Vos récompenses sont conservées.", "Der Verlauf konnte nicht geladen werden. Deine Belohnungen bleiben erhalten.", "수령 내역을 확인할 수 없습니다. 기존 보상은 그대로 유지됩니다.", "Не удалось проверить историю. Награды не изменены."}},
        {"no_history_body", new [] {"Finish missions and claim rewards here to see dated receipts and transaction IDs.", "Görevleri tamamlayıp Ödüller bölümünden ödülleri aldığında işlem kimlikleri burada görünecek.", "Completa misiones y reclama recompensas para ver recibos.", "Terminez des missions et récupérez les récompenses pour voir les reçus.", "Schließe Missionen ab und hole Belohnungen für Belege ab.", "임무 완료 후 보상을 받으면 영수증이 표시됩니다.", "Получите награды за задания, чтобы увидеть историю."}},
        {"promo_failed_title", new [] {"CODE NOT REDEEMED", "KOD KULLANILAMADI", "CÓDIGO NO CANJEADO", "CODE NON ÉCHANGÉ", "CODE NICHT EINGELÖST", "코드 사용 실패", "КОД НЕ АКТИВИРОВАН"}},
        {"promo_success_title", new [] {"REWARD CLAIMED", "ÖDÜL HESABINA EKLENDİ", "RECOMPENSA RECIBIDA", "RÉCOMPENSE REÇUE", "BELOHNUNG ERHALTEN", "보상이 지급되었습니다", "НАГРАДА ПОЛУЧЕНА"}},
        {"promo_success_body", new [] {"Code verified. The reward has been granted and logged to this account.", "Kod doğrulandı. Ödül bu hesaba eklendi ve işlem geçmişine kaydedildi.", "Código verificado. La recompensa se añadió y registró.", "Code vérifié. Récompense ajoutée et enregistrée.", "Code bestätigt. Belohnung gutgeschrieben und protokolliert.", "코드가 확인되어 보상이 지급되고 기록되었습니다.", "Код проверен. Награда начислена и сохранена."}},
        {"promo_invalid", new [] {"Invalid code format. Check the characters and try again.", "Kod biçimi geçersiz. Karakterleri kontrol edip yeniden dene.", "Formato de código no válido.", "Format de code invalide.", "Ungültiges Codeformat.", "잘못된 코드 형식입니다.", "Недопустимый формат кода."}},
        {"promo_used", new [] {"This code has already been used by this account.", "Bu promosyon kodu bu hesapta daha önce kullanılmış.", "Este código ya se usó en esta cuenta.", "Ce code a déjà été utilisé par ce compte.", "Dieser Code wurde auf diesem Konto bereits verwendet.", "이 계정에서 이미 사용한 코드입니다.", "Этот код уже использован в этом аккаунте."}},
        {"promo_owned", new [] {"You already own the item offered by this code.", "Bu kodun verdiği ürün zaten envanterinde bulunuyor.", "Ya tienes este artículo.", "Vous possédez déjà cet objet.", "Dieser Gegenstand ist bereits vorhanden.", "해당 아이템을 이미 보유하고 있습니다.", "Этот предмет уже есть в инвентаре."}},
        {"promo_not_found", new [] {"Code not found. It may not have been activated by the event administrator.", "Kod bulunamadı. Etkinlik yöneticisi tarafından henüz etkinleştirilmemiş olabilir.", "Código no encontrado o no activado.", "Code introuvable ou non activé.", "Code nicht gefunden oder noch nicht aktiviert.", "코드를 찾을 수 없거나 아직 활성화되지 않았습니다.", "Код не найден или еще не активирован."}},
        {"promo_disabled", new [] {"This code is currently disabled.", "Bu kod şu anda kullanım dışı.", "El código está desactivado.", "Ce code est désactivé.", "Dieser Code ist deaktiviert.", "비활성화된 코드입니다.", "Этот код отключен."}},
        {"promo_not_started", new [] {"This code is not available yet.", "Bu kod henüz kullanıma açılmadı.", "Este código aún no está disponible.", "Ce code n'est pas encore disponible.", "Dieser Code ist noch nicht verfügbar.", "아직 사용할 수 없는 코드입니다.", "Код еще недоступен."}},
        {"promo_expired", new [] {"This code has expired.", "Bu kodun kullanım süresi sona ermiş.", "El código ha caducado.", "Ce code a expiré.", "Dieser Code ist abgelaufen.", "코드가 만료되었습니다.", "Срок действия кода истек."}},
        {"promo_qa_denied", new [] {"This test code is not authorized for this account.", "Bu test kodu bu hesap için yetkilendirilmemiş.", "Cuenta no autorizada para este código de prueba.", "Compte non autorisé pour ce code de test.", "Konto für diesen Testcode nicht freigeschaltet.", "이 계정은 테스트 코드 사용 권한이 없습니다.", "Нет доступа к тестовому коду."}},
        {"promo_sign_in", new [] {"Sign in to your account to use a code.", "Kod kullanmak için hesabına giriş yap.", "Inicia sesión para usar un código.", "Connectez-vous pour utiliser un code.", "Melde dich an, um einen Code zu nutzen.", "코드를 사용하려면 로그인하세요.", "Войдите в аккаунт для активации кода."}},
        {"promo_account_not_ready", new [] {"Account data is not ready. Sign in again before redeeming.", "Hesap verileri hazır değil. Kod kullanmadan önce yeniden giriş yap.", "La cuenta no está lista. Inicia sesión de nuevo.", "Compte non prêt. Reconnectez-vous.", "Kontodaten nicht bereit. Bitte erneut anmelden.", "계정 데이터가 준비되지 않았습니다. 다시 로그인하세요.", "Данные аккаунта недоступны. Войдите снова."}},
        {"promo_limit", new [] {"This code has reached its usage limit.", "Bu kodun kullanım sınırına ulaşıldı.", "Se alcanzó el límite de usos.", "Limite d'utilisation atteinte.", "Nutzungslimit erreicht.", "사용 가능 횟수를 초과했습니다.", "Лимит использования исчерпан."}},
        {"promo_invalid_reward", new [] {"This code's reward could not be validated. No reward was granted.", "Bu kodun ödülü doğrulanamadı. Herhangi bir ödül verilmedi.", "No se pudo validar la recompensa.", "Récompense non validée.", "Belohnung konnte nicht geprüft werden.", "보상을 확인할 수 없습니다.", "Награда не прошла проверку."}},
        {"promo_unavailable", new [] {"Promo service unavailable. Code could not be verified and no reward was confirmed. Try later.", "Promosyon servisine ulaşılamadı. Kod doğrulanamadı ve ödül onaylanmadı. Daha sonra yeniden dene.", "Servicio de códigos no disponible. Inténtalo más tarde.", "Service de codes indisponible. Réessayez plus tard.", "Code-Dienst nicht erreichbar. Bitte später versuchen.", "코드 서비스를 사용할 수 없습니다. 나중에 다시 시도하세요.", "Сервис кодов недоступен. Повторите попытку позже."}},
        {"xp", new [] {"XP", "XP", "XP", "XP", "XP", "XP", "XP"}},
        {"gold", new [] {"Gold", "Altın", "Oro", "Or", "Gold", "골드", "Золото"}},
        {"atlas_coin", new [] {"Atlas Coin", "Atlas Coin", "Atlas Coin", "Atlas Coin", "Atlas Coin", "아틀라스 코인", "Атлас-монеты"}},
    };

    public static string WorldText(string key, string locale)
    {
        if (WorldStrings.TryGetValue(key ?? "", out string[] values))
            return values[AtlasBoardLocalizationLanguages.IndexOf(locale)];
        return key ?? "";
    }

}
