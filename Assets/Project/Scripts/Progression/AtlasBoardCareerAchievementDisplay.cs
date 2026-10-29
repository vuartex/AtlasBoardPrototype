using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Presentation-only catalog. No client achievement unlock or XP grants.
/// Preserves the fourteen IDs shared with Steam. An absent RTDB projection
/// must still show locked targets without fabricating progress.
/// </summary>
public static class AtlasBoardCareerAchievementDisplay
{
    private sealed class Definition
    {
        public readonly string Id;
        public readonly int Target;
        public readonly string[] Names;
        public readonly string[] Descriptions;
        public Definition(string id, int target, string[] names, string[] descriptions)
        { Id = id; Target = target; Names = names; Descriptions = descriptions; }
    }
    // Language order: English, Turkish, Spanish, French, German, Korean, Russian.
    private static Definition D(string id, int target, string[] n, string[] d) => new Definition(id, target, n, d);
    private static readonly Definition[] Entries =
    {
        D("first_match", 1, new [] {"First Match", "İlk Maç", "Primera partida", "Premier match", "Erstes Spiel", "첫 경기", "Первый матч"}, new [] {"Complete an eligible online match.", "Ödüle uygun bir online maç tamamla.", "Completa una partida online válida.", "Terminez une partie en ligne éligible.", "Beende ein berechtigtes Online-Spiel.", "보상 가능한 온라인 경기를 완료하세요.", "Завершите подходящий онлайн-матч."}),
        D("first_win", 1, new [] {"First Victory", "İlk Zafer", "Primera victoria", "Première victoire", "Erster Sieg", "첫 승리", "Первая победа"}, new [] {"Win an eligible online match.", "Ödüle uygun bir online maçı kazan.", "Gana una partida online válida.", "Gagnez une partie en ligne éligible.", "Gewinne ein berechtigtes Online-Spiel.", "보상 가능한 온라인 경기에서 승리하세요.", "Победите в подходящем онлайн-матче."}),
        D("win_streak_3", 3, new [] {"Winning Streak", "Galibiyet Serisi", "Racha ganadora", "Série de victoires", "Siegesserie", "연승", "Серия побед"}, new [] {"Win 3 eligible matches in a row.", "Art arda 3 uygun maçı kazan.", "Gana 3 partidas válidas seguidas.", "Gagnez 3 parties éligibles de suite.", "Gewinne 3 berechtigte Spiele in Folge.", "적격 경기 3연승을 달성하세요.", "Выиграйте 3 подходящих матча подряд."}),
        D("matches_10", 10, new [] {"Regular Player", "Düzenli Oyuncu", "Jugador habitual", "Joueur régulier", "Stammspieler", "단골 플레이어", "Постоянный игрок"}, new [] {"Complete 10 eligible matches.", "10 uygun maçı tamamla.", "Completa 10 partidas válidas.", "Terminez 10 parties éligibles.", "Beende 10 berechtigte Spiele.", "적격 경기 10회를 완료하세요.", "Завершите 10 подходящих матчей."}),
        D("wins_10", 10, new [] {"Ten Victories", "On Galibiyet", "Diez victorias", "Dix victoires", "Zehn Siege", "10승", "Десять побед"}, new [] {"Win 10 eligible matches.", "10 uygun maç kazan.", "Gana 10 partidas válidas.", "Gagnez 10 parties éligibles.", "Gewinne 10 berechtigte Spiele.", "적격 경기 10승을 달성하세요.", "Выиграйте 10 подходящих матчей."}),
        D("dice_100", 100, new [] {"Dice Master", "Zar Ustası", "Maestro del dado", "Maître des dés", "Würfelmeister", "주사위 마스터", "Мастер кубиков"}, new [] {"Roll dice 100 times in eligible matches.", "Uygun maçlarda 100 kez zar at.", "Lanza dados 100 veces en partidas válidas.", "Lancez les dés 100 fois en parties éligibles.", "Würfle 100 Mal in berechtigten Spielen.", "적격 경기에서 주사위 100번을 굴리세요.", "Бросьте кости 100 раз в подходящих матчах."}),
        D("doubles_10", 10, new [] {"Double Trouble", "Çifte Zar", "Dobles", "Doubles", "Doppelte Würfe", "더블", "Дубли"}, new [] {"Roll doubles 10 times.", "10 kez çift zar getir.", "Saca dobles 10 veces.", "Obtenez 10 doubles.", "Würfle 10 Pasche.", "더블을 10번 굴리세요.", "Выбросьте дубль 10 раз."}),
        D("properties_25", 25, new [] {"Property Collector", "Mülk Koleksiyoncusu", "Coleccionista de propiedades", "Collectionneur", "Immobiliensammler", "부동산 수집가", "Коллекционер недвижимости"}, new [] {"Acquire 25 properties in eligible play.", "Uygun oyunlarda 25 mülk edin.", "Adquiere 25 propiedades en partidas válidas.", "Acquérez 25 propriétés en parties éligibles.", "Erwirb 25 Immobilien in berechtigten Spielen.", "적격 게임에서 부동산 25개를 획득하세요.", "Приобретите 25 объектов в подходящих матчах."}),
        D("builder_20", 20, new [] {"City Builder", "Şehir Kurucusu", "Constructor", "Bâtisseur", "Stadtbauer", "도시 건설자", "Строитель города"}, new [] {"Build 20 development levels.", "20 geliştirme seviyesi inşa et.", "Construye 20 niveles de mejora.", "Construisez 20 niveaux de développement.", "Baue 20 Entwicklungsstufen.", "개발 단계 20개를 건설하세요.", "Постройте 20 уровней улучшений."}),
        D("networth_5000", 5000, new [] {"Wealth Builder", "Servet Ustası", "Gran fortuna", "Grande fortune", "Vermögensaufbau", "부의 축적", "Крупный капитал"}, new [] {"Reach net worth of 5,000 in eligible play.", "Uygun maçta 5.000 net servete ulaş.", "Alcanza un patrimonio neto de 5000.", "Atteignez une valeur nette de 5 000.", "Erreiche ein Nettovermögen von 5.000.", "순자산 5,000을 달성하세요.", "Достигните капитала 5000."}),
        D("turkey_explorer", 1, new [] {"Türkiye Explorer", "Türkiye Kâşifi", "Explorador de Turquía", "Explorateur de Turquie", "Türkei-Entdecker", "튀르키예 탐험가", "Исследователь Турции"}, new [] {"Complete an eligible match on Türkiye.", "Türkiye haritasında uygun maçı tamamla.", "Completa una partida válida en Turquía.", "Terminez une partie éligible en Turquie.", "Beende ein berechtigtes Türkei-Spiel.", "튀르키예 지도에서 적격 경기를 완료하세요.", "Завершите подходящий матч в Турции."}),
        D("colorado_explorer", 1, new [] {"Colorado Explorer", "Colorado Kâşifi", "Explorador de Colorado", "Explorateur du Colorado", "Colorado-Entdecker", "콜로라도 탐험가", "Исследователь Колорадо"}, new [] {"Complete an eligible match on Colorado.", "Colorado haritasında uygun maçı tamamla.", "Completa una partida válida en Colorado.", "Terminez une partie éligible au Colorado.", "Beende ein berechtigtes Colorado-Spiel.", "콜로라도 지도에서 적격 경기를 완료하세요.", "Завершите подходящий матч в Колорадо."}),
        D("usa_explorer", 1, new [] {"USA Explorer", "ABD Kâşifi", "Explorador de EE. UU.", "Explorateur des États-Unis", "USA-Entdecker", "미국 탐험가", "Исследователь США"}, new [] {"Complete an eligible match on USA.", "ABD haritasında uygun maçı tamamla.", "Completa una partida válida en EE. UU.", "Terminez une partie éligible aux États-Unis.", "Beende ein berechtigtes USA-Spiel.", "미국 지도에서 적격 경기를 완료하세요.", "Завершите подходящий матч в США."}),
        D("atlas_traveler", 3, new [] {"Atlas Traveler", "Atlas Gezgini", "Viajero Atlas", "Voyageur Atlas", "Atlas-Reisender", "아틀라스 여행자", "Путешественник Атласа"}, new [] {"Complete eligible games on Türkiye, Colorado and USA.", "Türkiye, Colorado ve ABD haritalarında uygun maç tamamla.", "Juega partidas válidas en Turquía, Colorado y EE. UU.", "Terminez des parties éligibles en Turquie, au Colorado et aux États-Unis.", "Beende berechtigte Spiele auf Türkei, Colorado und USA.", "튀르키예, 콜로라도, 미국 지도의 적격 경기를 완료하세요.", "Завершите подходящие матчи в Турции, Колорадо и США."}),
    };
    private static int LocaleIndex()
    {
        AtlasBoardLocalizationManager manager = AtlasBoardLocalizationManager.Instance;
        string lang = manager != null ? manager.CurrentLanguageCode : "en";
        switch ((lang ?? "en").ToLowerInvariant())
        {
            case "tr": return 1; case "es": return 2; case "fr": return 3;
            case "de": return 4; case "ko": return 5; case "ru": return 6;
            default: return 0;
        }
    }
    private static Definition Find(string id) => Entries.FirstOrDefault(e => e.Id == id);
    public static string Title(string id)
    { var e = Find(id); return e != null ? e.Names[LocaleIndex()] : id; }
    public static string Description(string id)
    { var e = Find(id); return e != null ? e.Descriptions[LocaleIndex()] : id; }
    public static List<AtlasBoardProgressionAchievement> MergeVerified(
        IEnumerable<AtlasBoardProgressionAchievement> verified)
    {
        var items = new List<AtlasBoardProgressionAchievement>();
        var source = new Dictionary<string, AtlasBoardProgressionAchievement>(StringComparer.Ordinal);
        if (verified != null)
            foreach (var a in verified)
                if (a != null && !string.IsNullOrWhiteSpace(a.achievementId))
                    source[a.achievementId] = a;
        foreach (Definition def in Entries)
        {
            AtlasBoardProgressionAchievement server;
            source.TryGetValue(def.Id, out server);
            items.Add(new AtlasBoardProgressionAchievement
            {
                achievementId = def.Id,
                target = server != null && server.target > 0 ? server.target : def.Target,
                progress = server != null ? Math.Max(0,server.progress) : 0,
                unlocked = server != null && server.unlocked
            });
            source.Remove(def.Id);
        }
        // Preserve future authoritative achievements not yet mapped in this client.
        items.AddRange(source.Values);
        return items;
    }
}
