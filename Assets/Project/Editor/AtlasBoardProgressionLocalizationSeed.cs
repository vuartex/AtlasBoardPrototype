#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AtlasBoardProgressionLocalizationSeed
{
    private const string DatabasePath =
        "Assets/Project/Data/Localization/Localization_Default.asset";

    static AtlasBoardProgressionLocalizationSeed()
    {
        EditorApplication.delayCall += MergeNow;
    }

    [MenuItem("Atlas Board/Meta & Progression/Progression/Refresh Phase 10 Localization")]
    public static void MergeNow()
    {
        AtlasBoardLocalizationDatabase database =
            AssetDatabase.LoadAssetAtPath<AtlasBoardLocalizationDatabase>(DatabasePath);
        if (database == null)
        {
            Debug.LogError(
                "AtlasBoard Phase 10 localization database not found: " +
                DatabasePath);
            return;
        }

        List<AtlasBoardLocalizationDatabase.Entry> list =
            database.Entries
                .Where(entry =>
                    entry != null &&
                    !string.IsNullOrWhiteSpace(entry.key) &&
                    !entry.key.StartsWith("progression."))
                .Select(Clone)
                .ToList();

        Append(list);
        database.EditorReplaceEntries(list);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        int count = list.Count(entry =>
            entry != null &&
            entry.key != null &&
            entry.key.StartsWith("progression."));

        Debug.Log(
            $"AtlasBoard Phase 10 Progression localization refreshed. " +
            $"Entries={count}; Languages=EN/TR/ES/FR/DE/KO/RU.");
    }

    private static void Append(
        List<AtlasBoardLocalizationDatabase.Entry> list)
    {
        Add(list, "progression.menu.title",
            "CAREER",
            "KARİYER",
            "CARRERA",
            "CARRIÈRE",
            "KARRIERE",
            "커리어",
            "КАРЬЕРА");

        Add(list, "progression.menu.subtitle",
            "STATS / MATCHES / ACHIEVEMENTS",
            "İSTATİSTİK / MAÇ / BAŞARIM",
            "ESTADÍSTICAS / PARTIDAS / LOGROS",
            "STATS / PARTIES / SUCCÈS",
            "STATS / SPIELE / ERFOLGE",
            "통계 / 경기 / 업적",
            "СТАТИСТИКА / МАТЧИ / ДОСТИЖЕНИЯ");

        Add(list, "progression.title",
            "ATLAS CAREER",
            "ATLAS KARİYER",
            "CARRERA ATLAS",
            "CARRIÈRE ATLAS",
            "ATLAS KARRIERE",
            "아틀라스 커리어",
            "КАРЬЕРА ATLAS");

        Add(list, "progression.tab.overview",
            "OVERVIEW",
            "GENEL BAKIŞ",
            "RESUMEN",
            "APERÇU",
            "ÜBERSICHT",
            "개요",
            "ОБЗОР");

        Add(list, "progression.tab.history",
            "MATCH HISTORY",
            "MAÇ GEÇMİŞİ",
            "HISTORIAL",
            "HISTORIQUE",
            "SPIELVERLAUF",
            "경기 기록",
            "ИСТОРИЯ МАТЧЕЙ");

        Add(list, "progression.tab.achievements",
            "ACHIEVEMENTS",
            "BAŞARIMLAR",
            "LOGROS",
            "SUCCÈS",
            "ERFOLGE",
            "업적",
            "ДОСТИЖЕНИЯ");

        Add(list, "progression.overview.title",
            "PLAYER PROGRESSION",
            "OYUNCU İLERLEMESİ",
            "PROGRESO DEL JUGADOR",
            "PROGRESSION DU JOUEUR",
            "SPIELERFORTSCHRITT",
            "플레이어 진행도",
            "ПРОГРЕСС ИГРОКА");

        Add(list, "progression.loading",
            "Loading career data...",
            "Kariyer verileri yükleniyor...",
            "Cargando datos de carrera...",
            "Chargement des données de carrière...",
            "Karrieredaten werden geladen...",
            "커리어 데이터를 불러오는 중...",
            "Загрузка данных карьеры...");

        Add(list, "progression.common.close",
            "CLOSE",
            "KAPAT",
            "CERRAR",
            "FERMER",
            "SCHLIESSEN",
            "닫기",
            "ЗАКРЫТЬ");

        Add(list, "progression.level.account_progress",
            "ACCOUNT XP & LEVEL",
            "HESAP XP & SEVİYE",
            "XP Y NIVEL DE CUENTA",
            "XP ET NIVEAU DU COMPTE",
            "KONTO-XP & LEVEL",
            "계정 XP & 레벨",
            "XP И УРОВЕНЬ АККАУНТА");

        Add(list, "progression.level.value",
            "LEVEL {0}",
            "SEVİYE {0}",
            "NIVEL {0}",
            "NIVEAU {0}",
            "LEVEL {0}",
            "레벨 {0}",
            "УРОВЕНЬ {0}");

        Add(list, "progression.level.xp",
            "XP {0:N0} / {1:N0}",
            "XP {0:N0} / {1:N0}",
            "XP {0:N0} / {1:N0}",
            "XP {0:N0} / {1:N0}",
            "XP {0:N0} / {1:N0}",
            "XP {0:N0} / {1:N0}",
            "XP {0:N0} / {1:N0}");

        Add(list, "progression.level.matches",
            "{0:N0} completed matches",
            "{0:N0} tamamlanan maç",
            "{0:N0} partidas completadas",
            "{0:N0} parties terminées",
            "{0:N0} abgeschlossene Spiele",
            "완료한 경기 {0:N0}회",
            "Завершено матчей: {0:N0}");

        Add(list, "progression.stats.career",
            "CAREER STATS",
            "KARİYER İSTATİSTİKLERİ",
            "ESTADÍSTICAS",
            "STATS DE CARRIÈRE",
            "KARRIERE-STATS",
            "커리어 통계",
            "СТАТИСТИКА КАРЬЕРЫ");

        Add(list, "progression.stats.gameplay",
            "GAMEPLAY STATS",
            "OYUN İSTATİSTİKLERİ",
            "ESTADÍSTICAS DE JUEGO",
            "STATS DE JEU",
            "SPIELSTATISTIK",
            "게임 통계",
            "ИГРОВАЯ СТАТИСТИКА");

        Add(list, "progression.stats.records",
            "RECORDS & MAPS",
            "REKORLAR & HARİTALAR",
            "RÉCORDS Y MAPAS",
            "RECORDS & CARTES",
            "REKORDE & KARTEN",
            "기록 & 맵",
            "РЕКОРДЫ И КАРТЫ");

        Add(list, "progression.stats.matches",
            "Matches",
            "Maçlar",
            "Partidas",
            "Parties",
            "Spiele",
            "경기",
            "Матчи");

        Add(list, "progression.stats.wins",
            "Wins",
            "Galibiyet",
            "Victorias",
            "Victoires",
            "Siege",
            "승리",
            "Победы");

        Add(list, "progression.stats.losses",
            "Losses",
            "Mağlubiyet",
            "Derrotas",
            "Défaites",
            "Niederlagen",
            "패배",
            "Поражения");

        Add(list, "progression.stats.ties",
            "Ties",
            "Beraberlik",
            "Empates",
            "Égalités",
            "Unentschieden",
            "무승부",
            "Ничьи");

        Add(list, "progression.stats.win_rate",
            "Win rate",
            "Kazanma oranı",
            "Tasa de victoria",
            "Taux de victoire",
            "Siegquote",
            "승률",
            "Процент побед");

        Add(list, "progression.stats.best_streak",
            "Best win streak",
            "En iyi seri",
            "Mejor racha",
            "Meilleure série",
            "Beste Siegesserie",
            "최고 연승",
            "Лучшая серия побед");

        Add(list, "progression.stats.dice_rolls",
            "Dice rolls",
            "Zar atışları",
            "Tiradas de dados",
            "Lancers de dés",
            "Würfe",
            "주사위 굴림",
            "Броски кубиков");

        Add(list, "progression.stats.doubles",
            "Doubles",
            "Çift zar",
            "Dobles",
            "Doubles",
            "Pasche",
            "더블",
            "Дубли");

        Add(list, "progression.stats.avg_roll",
            "Average roll",
            "Ortalama zar",
            "Tirada media",
            "Moyenne des dés",
            "Ø Wurf",
            "평균 주사위",
            "Средний бросок");

        Add(list, "progression.stats.high_roll",
            "Highest roll",
            "En yüksek zar",
            "Tirada máxima",
            "Meilleur lancer",
            "Höchster Wurf",
            "최고 주사위",
            "Макс. бросок");

        Add(list, "progression.stats.properties",
            "Properties at finish",
            "Maç sonu mülk toplamı",
            "Propiedades al final",
            "Propriétés à l'arrivée",
            "Immobilien am Ende",
            "종료 시 부동산",
            "Недвижимость в конце");

        Add(list, "progression.stats.development",
            "Development levels",
            "Geliştirme seviyeleri",
            "Niveles de desarrollo",
            "Niveaux de développement",
            "Ausbaustufen",
            "개발 단계",
            "Уровни застройки");

        Add(list, "progression.stats.networth",
            "Highest net worth",
            "En yüksek net değer",
            "Patrimonio máximo",
            "Valeur nette max",
            "Höchstes Vermögen",
            "최고 순자산",
            "Макс. капитал");

        Add(list, "progression.stats.cash",
            "Highest cash",
            "En yüksek nakit",
            "Efectivo máximo",
            "Cash maximum",
            "Höchstes Bargeld",
            "최고 현금",
            "Макс. наличные");

        Add(list, "progression.stats.rounds",
            "Rounds played",
            "Oynanan turlar",
            "Rondas jugadas",
            "Manches jouées",
            "Gespielte Runden",
            "플레이 라운드",
            "Сыграно раундов");

        Add(list, "progression.stats.turns",
            "Turns completed",
            "Tamamlanan turlar",
            "Turnos completados",
            "Tours terminés",
            "Abgeschlossene Züge",
            "완료한 턴",
            "Завершено ходов");

        Add(list, "progression.stats.maps",
            "Map completions",
            "Harita bitirişleri",
            "Mapas completados",
            "Cartes terminées",
            "Kartenabschlüsse",
            "맵 완료",
            "Завершения карт");

        Add(list, "progression.stats.maps_value",
            "TR {0} • CO {1} • USA {2}",
            "TR {0} • CO {1} • ABD {2}",
            "TR {0} • CO {1} • EE. UU. {2}",
            "TR {0} • CO {1} • USA {2}",
            "TR {0} • CO {1} • USA {2}",
            "TR {0} • CO {1} • 미국 {2}",
            "TR {0} • CO {1} • США {2}");

        Add(list, "progression.stats.map_value",
            "{0}: {1:N0}",
            "{0}: {1:N0}",
            "{0}: {1:N0}",
            "{0}: {1:N0}",
            "{0}: {1:N0}",
            "{0}: {1:N0}",
            "{0}: {1:N0}");

        Add(list, "progression.stats.bankruptcies",
            "Bankrupt finishes",
            "İflasla bitiş",
            "Finales en quiebra",
            "Fins en faillite",
            "Bankrott beendet",
            "파산 종료",
            "Банкротства");

        Add(list, "progression.history.title",
            "RECENT MATCHES",
            "SON MAÇLAR",
            "PARTIDAS RECIENTES",
            "PARTIES RÉCENTES",
            "LETZTE SPIELE",
            "최근 경기",
            "ПОСЛЕДНИЕ МАТЧИ");

        Add(list, "progression.history.retention",
            "Only the newest {0} completed matches are shown; records older than {1} hours are removed.",
            "Yalnızca en yeni {0} tamamlanmış maç gösterilir; {1} saatten eski kayıtlar silinir.",
            "Solo se muestran las {0} partidas completadas más recientes; se eliminan registros de más de {1} horas.",
            "Seules les {0} parties terminées les plus récentes sont affichées ; les données de plus de {1} heures sont supprimées.",
            "Nur die neuesten {0} abgeschlossenen Spiele werden gezeigt; Einträge älter als {1} Stunden werden gelöscht.",
            "최근 완료 경기 {0}개만 표시되며 {1}시간이 지난 기록은 삭제됩니다.",
            "Показываются только последние {0} завершённых матчей; записи старше {1} часов удаляются.");

        Add(list, "progression.history.empty",
            "Complete a match to start your career history.",
            "Kariyer geçmişini başlatmak için bir maçı başarıyla tamamla.",
            "Completa una partida para iniciar tu historial.",
            "Terminez une partie pour commencer votre historique.",
            "Beende ein Spiel, um deinen Verlauf zu starten.",
            "경기를 완료하면 커리어 기록이 시작됩니다.",
            "Завершите матч, чтобы начать историю карьеры.");

        Add(list, "progression.history.details",
            "DETAILS",
            "DETAYLAR",
            "DETALLES",
            "DÉTAILS",
            "DETAILS",
            "상세",
            "ПОДРОБНОСТИ");

        Add(list, "progression.history.host_lobby",
            "{0}'s Lobby",
            "{0} Lobisi",
            "Sala de {0}",
            "Salon de {0}",
            "Lobby von {0}",
            "{0}의 로비",
            "Лобби {0}");

        Add(list, "progression.history.lobby_fallback",
            "Match Lobby",
            "Maç Lobisi",
            "Sala de partida",
            "Salon de partie",
            "Spiel-Lobby",
            "경기 로비",
            "Лобби матча");

        Add(list, "progression.history.row",
            "{0} • Place {1}/{2} • Net worth {3:N0} • +{4} XP",
            "{0} • Sıra {1}/{2} • Net değer {3:N0} • +{4} XP",
            "{0} • Puesto {1}/{2} • Patrimonio {3:N0} • +{4} XP",
            "{0} • Place {1}/{2} • Valeur nette {3:N0} • +{4} XP",
            "{0} • Platz {1}/{2} • Vermögen {3:N0} • +{4} XP",
            "{0} • 순위 {1}/{2} • 순자산 {3:N0} • +{4} XP",
            "{0} • Место {1}/{2} • Капитал {3:N0} • +{4} XP");

        Add(list, "progression.result.win",
            "WIN",
            "GALİBİYET",
            "VICTORIA",
            "VICTOIRE",
            "SIEG",
            "승리",
            "ПОБЕДА");

        Add(list, "progression.result.loss",
            "LOSS",
            "MAĞLUBİYET",
            "DERROTA",
            "DÉFAITE",
            "NIEDERLAGE",
            "패배",
            "ПОРАЖЕНИЕ");

        Add(list, "progression.result.tie",
            "TIE",
            "BERABERLİK",
            "EMPATE",
            "ÉGALITÉ",
            "UNENTSCHIEDEN",
            "무승부",
            "НИЧЬЯ");

        Add(list, "progression.details.title",
            "MATCH DETAILS",
            "MAÇ DETAYLARI",
            "DETALLES DE PARTIDA",
            "DÉTAILS DE LA PARTIE",
            "SPIELDETAILS",
            "경기 상세",
            "ДЕТАЛИ МАТЧА");

        Add(list, "progression.details.match_id",
            "Match ID: {0}",
            "Maç Kimliği: {0}",
            "ID de partida: {0}",
            "ID de partie : {0}",
            "Match-ID: {0}",
            "경기 ID: {0}",
            "ID матча: {0}");

        Add(list, "progression.details.lobby",
            "Lobby: {0}",
            "Lobi: {0}",
            "Sala: {0}",
            "Salon : {0}",
            "Lobby: {0}",
            "로비: {0}",
            "Лобби: {0}");

        Add(list, "progression.details.rules",
            "Map: {0} • Theme: {1} • Round limit: {2}",
            "Harita: {0} • Tema: {1} • Tur limiti: {2}",
            "Mapa: {0} • Tema: {1} • Límite: {2}",
            "Carte : {0} • Thème : {1} • Limite : {2}",
            "Karte: {0} • Thema: {1} • Rundenlimit: {2}",
            "맵: {0} • 테마: {1} • 라운드 제한: {2}",
            "Карта: {0} • Тема: {1} • Лимит раундов: {2}");

        Add(list, "progression.details.place",
            "Final place: {0}/{1}",
            "Final sıra: {0}/{1}",
            "Puesto final: {0}/{1}",
            "Place finale : {0}/{1}",
            "Endplatz: {0}/{1}",
            "최종 순위: {0}/{1}",
            "Итоговое место: {0}/{1}");

        Add(list, "progression.details.economy",
            "Cash {0:N0} • Properties {1} • Development {2} • Net worth {3:N0}",
            "Nakit {0:N0} • Mülk {1} • Geliştirme {2} • Net değer {3:N0}",
            "Efectivo {0:N0} • Propiedades {1} • Desarrollo {2} • Patrimonio {3:N0}",
            "Cash {0:N0} • Propriétés {1} • Développement {2} • Valeur nette {3:N0}",
            "Bargeld {0:N0} • Immobilien {1} • Ausbau {2} • Vermögen {3:N0}",
            "현금 {0:N0} • 부동산 {1} • 개발 {2} • 순자산 {3:N0}",
            "Наличные {0:N0} • Недвижимость {1} • Застройка {2} • Капитал {3:N0}");

        Add(list, "progression.details.dice",
            "Dice rolls {0} • Doubles {1} • Average {2} • High {3}",
            "Zar {0} • Çift {1} • Ortalama {2} • En yüksek {3}",
            "Tiradas {0} • Dobles {1} • Media {2} • Máxima {3}",
            "Lancers {0} • Doubles {1} • Moyenne {2} • Max {3}",
            "Würfe {0} • Pasche {1} • Ø {2} • Max {3}",
            "주사위 {0} • 더블 {1} • 평균 {2} • 최고 {3}",
            "Броски {0} • Дубли {1} • Среднее {2} • Макс {3}");

        Add(list, "progression.details.xp",
            "XP earned +{0} • Level after match {1}",
            "Kazanılan XP +{0} • Maç sonrası seviye {1}",
            "XP ganado +{0} • Nivel tras partida {1}",
            "XP gagné +{0} • Niveau après partie {1}",
            "XP erhalten +{0} • Level danach {1}",
            "획득 XP +{0} • 경기 후 레벨 {1}",
            "Получено XP +{0} • Уровень после матча {1}");

        Add(list, "progression.details.players",
            "PLAYERS",
            "OYUNCULAR",
            "JUGADORES",
            "JOUEURS",
            "SPIELER",
            "플레이어",
            "ИГРОКИ");

        Add(list, "progression.details.player_row",
            "#{0} {1} — {2} — Net {3:N0} — Properties {4} — Dev {5}",
            "#{0} {1} — {2} — Net {3:N0} — Mülk {4} — Gel. {5}",
            "#{0} {1} — {2} — Neto {3:N0} — Prop. {4} — Des. {5}",
            "#{0} {1} — {2} — Net {3:N0} — Prop. {4} — Dév. {5}",
            "#{0} {1} — {2} — Netto {3:N0} — Besitz {4} — Ausbau {5}",
            "#{0} {1} — {2} — 순자산 {3:N0} — 부동산 {4} — 개발 {5}",
            "#{0} {1} — {2} — Капитал {3:N0} — Объекты {4} — Застройка {5}");

        Add(list, "progression.achievements.title",
            "ACHIEVEMENTS",
            "BAŞARIMLAR",
            "LOGROS",
            "SUCCÈS",
            "ERFOLGE",
            "업적",
            "ДОСТИЖЕНИЯ");

        Add(list, "progression.achievements.note",
            "Achievements unlock from completed account-linked matches. Detailed dice counters are unranked telemetry for now.",
            "Başarımlar tamamlanmış hesap bağlantılı maçlardan açılır. Ayrıntılı zar sayaçları şimdilik derecesiz telemetridir.",
            "Los logros se desbloquean con partidas completadas vinculadas a la cuenta. Los contadores de dados aún no son ranked.",
            "Les succès se débloquent via des parties terminées liées au compte. Les compteurs de dés restent non classés pour l'instant.",
            "Erfolge werden durch abgeschlossene kontoverknüpfte Spiele freigeschaltet. Würfelzähler sind vorerst ungewertete Telemetrie.",
            "업적은 계정에 연결된 완료 경기로 해금됩니다. 세부 주사위 통계는 현재 랭크 판정에 사용되지 않습니다.",
            "Достижения открываются завершёнными матчами аккаунта. Детальная статистика кубиков пока не влияет на рейтинг.");

        Add(list, "progression.achievements.empty",
            "No achievement data yet.",
            "Henüz başarım verisi yok.",
            "Aún no hay datos de logros.",
            "Aucune donnée de succès.",
            "Noch keine Erfolgsdaten.",
            "아직 업적 데이터가 없습니다.",
            "Данных о достижениях пока нет.");

        Add(list, "progression.achievements.unlocked",
            "UNLOCKED",
            "AÇILDI",
            "DESBLOQUEADO",
            "DÉBLOQUÉ",
            "FREIGESCHALTET",
            "해금됨",
            "ОТКРЫТО");

        Add(list, "progression.achievements.progress",
            "{0:N0} / {1:N0}",
            "{0:N0} / {1:N0}",
            "{0:N0} / {1:N0}",
            "{0:N0} / {1:N0}",
            "{0:N0} / {1:N0}",
            "{0:N0} / {1:N0}",
            "{0:N0} / {1:N0}");

        Add(list, "progression.filter.map",
            "MAP",
            "HARİTA",
            "MAPA",
            "CARTE",
            "KARTE",
            "맵",
            "КАРТА");

        Add(list, "progression.map.all",
            "All maps",
            "Tüm haritalar",
            "Todos los mapas",
            "Toutes les cartes",
            "Alle Karten",
            "모든 맵",
            "Все карты");

        Add(list, "progression.map.turkey",
            "Turkey",
            "Türkiye",
            "Turquía",
            "Turquie",
            "Türkei",
            "튀르키예",
            "Турция");

        Add(list, "progression.map.colorado",
            "Colorado",
            "Colorado",
            "Colorado",
            "Colorado",
            "Colorado",
            "콜로라도",
            "Колорадо");

        Add(list, "progression.map.usa",
            "USA",
            "ABD",
            "EE. UU.",
            "USA",
            "USA",
            "미국",
            "США");

        Add(list, "progression.error.invalid_request",
            "Invalid progression request.",
            "Geçersiz ilerleme isteği.",
            "Solicitud de progreso no válida.",
            "Requête de progression invalide.",
            "Ungültige Fortschrittsanfrage.",
            "잘못된 진행도 요청입니다.",
            "Некорректный запрос прогресса.");

        Add(list, "progression.error.match_not_final",
            "The match result is not final yet.",
            "Maç sonucu henüz kesinleşmedi.",
            "El resultado aún no es definitivo.",
            "Le résultat du match n'est pas encore final.",
            "Das Spielergebnis ist noch nicht final.",
            "경기 결과가 아직 확정되지 않았습니다.",
            "Результат матча ещё не финальный.");

        Add(list, "progression.error.match_not_found",
            "Match record not found.",
            "Maç kaydı bulunamadı.",
            "No se encontró la partida.",
            "Partie introuvable.",
            "Spiel nicht gefunden.",
            "경기 기록을 찾을 수 없습니다.",
            "Матч не найден.");

        Add(list, "progression.error.host_required",
            "Only the authoritative host can finalize progression.",
            "İlerlemeyi yalnızca yetkili host kesinleştirebilir.",
            "Solo el host autoritativo puede finalizar el progreso.",
            "Seul l'hôte autoritaire peut finaliser la progression.",
            "Nur der autoritative Host kann den Fortschritt finalisieren.",
            "권한 있는 호스트만 진행도를 확정할 수 있습니다.",
            "Только авторитетный хост может завершить прогресс.");

        Add(list, "progression.error.no_account_seats",
            "No account-linked human players were found.",
            "Hesaba bağlı insan oyuncu bulunamadı.",
            "No se encontraron jugadores humanos vinculados.",
            "Aucun joueur humain lié à un compte.",
            "Keine kontoverknüpften menschlichen Spieler gefunden.",
            "계정에 연결된 인간 플레이어가 없습니다.",
            "Не найдены игроки-люди, привязанные к аккаунтам.");

        Add(list, "progression.error.identity_unavailable",
            "Player identity is unavailable.",
            "Oyuncu kimliği kullanılamıyor.",
            "La identidad del jugador no está disponible.",
            "Identité du joueur indisponible.",
            "Spieleridentität nicht verfügbar.",
            "플레이어 ID를 사용할 수 없습니다.",
            "Идентификатор игрока недоступен.");

        Add(list, "progression.error.service_unavailable",
            "Career service is unavailable.",
            "Kariyer hizmeti kullanılamıyor.",
            "El servicio de carrera no está disponible.",
            "Le service de carrière est indisponible.",
            "Karrieredienst nicht verfügbar.",
            "커리어 서비스를 사용할 수 없습니다.",
            "Сервис карьеры недоступен.");

        Add(list, "progression.achievement.first_match.title",
            "First Steps",
            "İlk Adımlar",
            "Primeros Pasos",
            "Premiers Pas",
            "Erste Schritte",
            "첫걸음",
            "Первые шаги");

        Add(list, "progression.achievement.first_match.body",
            "Complete your first match.",
            "İlk maçını tamamla.",
            "Completa tu primera partida.",
            "Terminez votre première partie.",
            "Beende dein erstes Spiel.",
            "첫 경기를 완료하세요.",
            "Завершите первый матч.");

        Add(list, "progression.achievement.first_win.title",
            "First Victory",
            "İlk Zafer",
            "Primera Victoria",
            "Première Victoire",
            "Erster Sieg",
            "첫 승리",
            "Первая победа");

        Add(list, "progression.achievement.first_win.body",
            "Win your first match.",
            "İlk maçını kazan.",
            "Gana tu primera partida.",
            "Gagnez votre première partie.",
            "Gewinne dein erstes Spiel.",
            "첫 승리를 달성하세요.",
            "Одержите первую победу.");

        Add(list, "progression.achievement.streak.title",
            "On a Roll",
            "Seri Başladı",
            "En Racha",
            "En Série",
            "Siegesserie",
            "연승 행진",
            "На серии");

        Add(list, "progression.achievement.streak.body",
            "Reach a 3-win streak.",
            "3 maçlık galibiyet serisine ulaş.",
            "Consigue 3 victorias seguidas.",
            "Atteignez 3 victoires d'affilée.",
            "Erreiche 3 Siege in Folge.",
            "3연승을 달성하세요.",
            "Выиграйте 3 матча подряд.");

        Add(list, "progression.achievement.matches_10.title",
            "Regular Player",
            "Müdavim",
            "Jugador Habitual",
            "Joueur Régulier",
            "Stammspieler",
            "단골 플레이어",
            "Постоянный игрок");

        Add(list, "progression.achievement.matches_10.body",
            "Complete 10 matches.",
            "10 maç tamamla.",
            "Completa 10 partidas.",
            "Terminez 10 parties.",
            "Beende 10 Spiele.",
            "10경기를 완료하세요.",
            "Завершите 10 матчей.");

        Add(list, "progression.achievement.wins_10.title",
            "Ten Victories",
            "On Zafer",
            "Diez Victorias",
            "Dix Victoires",
            "Zehn Siege",
            "10승",
            "Десять побед");

        Add(list, "progression.achievement.wins_10.body",
            "Win 10 matches.",
            "10 maç kazan.",
            "Gana 10 partidas.",
            "Gagnez 10 parties.",
            "Gewinne 10 Spiele.",
            "10승을 달성하세요.",
            "Выиграйте 10 матчей.");

        Add(list, "progression.achievement.dice_100.title",
            "Dice Veteran",
            "Zar Ustası",
            "Veterano de Dados",
            "Vétéran des Dés",
            "Würfelveteran",
            "주사위 베테랑",
            "Ветеран кубиков");

        Add(list, "progression.achievement.dice_100.body",
            "Roll the dice 100 times in completed matches.",
            "Tamamlanan maçlarda 100 kez zar at.",
            "Lanza los dados 100 veces en partidas completadas.",
            "Lancez les dés 100 fois dans des parties terminées.",
            "Würfle 100-mal in abgeschlossenen Spielen.",
            "완료 경기에서 주사위를 100회 굴리세요.",
            "Бросьте кубики 100 раз в завершённых матчах.");

        Add(list, "progression.achievement.doubles_10.title",
            "Double Trouble",
            "Çifte Bela",
            "Problema Doble",
            "Double Problème",
            "Doppeltes Glück",
            "더블 트러블",
            "Двойная проблема");

        Add(list, "progression.achievement.doubles_10.body",
            "Roll 10 doubles.",
            "10 kez çift zar at.",
            "Saca 10 dobles.",
            "Faites 10 doubles.",
            "Würfle 10 Pasche.",
            "더블을 10회 굴리세요.",
            "Выбросьте 10 дублей.");

        Add(list, "progression.achievement.properties_25.title",
            "Property Collector",
            "Mülk Koleksiyoncusu",
            "Coleccionista",
            "Collectionneur",
            "Immobiliensammler",
            "부동산 수집가",
            "Коллекционер недвижимости");

        Add(list, "progression.achievement.properties_25.body",
            "Finish matches with 25 properties in total.",
            "Maçları toplam 25 mülkle bitir.",
            "Termina partidas con 25 propiedades en total.",
            "Terminez des parties avec 25 propriétés au total.",
            "Beende Spiele mit insgesamt 25 Immobilien.",
            "경기 종료 시 부동산 누적 25개를 달성하세요.",
            "Завершите матчи суммарно с 25 объектами.");

        Add(list, "progression.achievement.builder_20.title",
            "Master Builder",
            "Usta İnşaatçı",
            "Maestro Constructor",
            "Maître Bâtisseur",
            "Baumeister",
            "건축 대가",
            "Мастер-строитель");

        Add(list, "progression.achievement.builder_20.body",
            "Accumulate 20 development levels at match finish.",
            "Maç sonlarında toplam 20 geliştirme seviyesine ulaş.",
            "Acumula 20 niveles de desarrollo al finalizar partidas.",
            "Cumulez 20 niveaux de développement en fin de partie.",
            "Sammle 20 Ausbaustufen am Spielende.",
            "경기 종료 시 개발 단계 누적 20을 달성하세요.",
            "Наберите 20 уровней застройки к концу матчей.");

        Add(list, "progression.achievement.networth.title",
            "Capital Leader",
            "Sermaye Lideri",
            "Líder de Capital",
            "Leader du Capital",
            "Kapitalführer",
            "자본 리더",
            "Лидер капитала");

        Add(list, "progression.achievement.networth.body",
            "Reach 5,000 net worth in a completed match.",
            "Tamamlanan bir maçta 5.000 net değere ulaş.",
            "Alcanza 5.000 de patrimonio en una partida.",
            "Atteignez 5 000 de valeur nette.",
            "Erreiche 5.000 Vermögen in einem Spiel.",
            "완료 경기에서 순자산 5,000을 달성하세요.",
            "Достигните капитала 5 000 в завершённом матче.");

        Add(list, "progression.achievement.turkey.title",
            "Turkey Explorer",
            "Türkiye Kaşifi",
            "Explorador de Turquía",
            "Explorateur de Turquie",
            "Türkei-Entdecker",
            "튀르키예 탐험가",
            "Исследователь Турции");

        Add(list, "progression.achievement.turkey.body",
            "Complete a match on Turkey.",
            "Türkiye haritasında bir maç tamamla.",
            "Completa una partida en Turquía.",
            "Terminez une partie en Turquie.",
            "Beende ein Spiel auf der Türkei-Karte.",
            "튀르키예 맵에서 경기를 완료하세요.",
            "Завершите матч на карте Турции.");

        Add(list, "progression.achievement.colorado.title",
            "Colorado Explorer",
            "Colorado Kaşifi",
            "Explorador de Colorado",
            "Explorateur du Colorado",
            "Colorado-Entdecker",
            "콜로라도 탐험가",
            "Исследователь Колорадо");

        Add(list, "progression.achievement.colorado.body",
            "Complete a match on Colorado.",
            "Colorado haritasında bir maç tamamla.",
            "Completa una partida en Colorado.",
            "Terminez une partie au Colorado.",
            "Beende ein Spiel auf Colorado.",
            "콜로라도 맵에서 경기를 완료하세요.",
            "Завершите матч на карте Колорадо.");

        Add(list, "progression.achievement.usa.title",
            "USA Explorer",
            "ABD Kaşifi",
            "Explorador de EE. UU.",
            "Explorateur des USA",
            "USA-Entdecker",
            "미국 탐험가",
            "Исследователь США");

        Add(list, "progression.achievement.usa.body",
            "Complete a match on USA.",
            "ABD haritasında bir maç tamamla.",
            "Completa una partida en EE. UU.",
            "Terminez une partie sur USA.",
            "Beende ein Spiel auf USA.",
            "미국 맵에서 경기를 완료하세요.",
            "Завершите матч на карте США.");

        Add(list, "progression.achievement.traveler.title",
            "Atlas Traveler",
            "Atlas Gezgini",
            "Viajero Atlas",
            "Voyageur Atlas",
            "Atlas-Reisender",
            "아틀라스 여행자",
            "Путешественник Atlas");

        Add(list, "progression.achievement.traveler.body",
            "Complete matches on Turkey, Colorado and USA.",
            "Türkiye, Colorado ve ABD haritalarında maç tamamla.",
            "Completa partidas en Turquía, Colorado y EE. UU.",
            "Terminez des parties en Turquie, au Colorado et aux USA.",
            "Beende Spiele auf Türkei, Colorado und USA.",
            "튀르키예, 콜로라도, 미국 맵에서 경기를 완료하세요.",
            "Завершите матчи на картах Турции, Колорадо и США.");

        Add(list, "progression.achievement.win_streak_3.title",
            "On a Roll",
            "Seri Başladı",
            "En Racha",
            "En Série",
            "Siegesserie",
            "연승 행진",
            "На серии");

        Add(list, "progression.achievement.win_streak_3.body",
            "Reach a 3-win streak.",
            "3 maçlık galibiyet serisine ulaş.",
            "Consigue 3 victorias seguidas.",
            "Atteignez 3 victoires d'affilée.",
            "Erreiche 3 Siege in Folge.",
            "3연승을 달성하세요.",
            "Выиграйте 3 матча подряд.");

        Add(list, "progression.achievement.networth_5000.title",
            "Capital Leader",
            "Sermaye Lideri",
            "Líder de Capital",
            "Leader du Capital",
            "Kapitalführer",
            "자본 리더",
            "Лидер капитала");

        Add(list, "progression.achievement.networth_5000.body",
            "Reach 5,000 net worth in a completed match.",
            "Tamamlanan bir maçta 5.000 net değere ulaş.",
            "Alcanza 5.000 de patrimonio en una partida.",
            "Atteignez 5 000 de valeur nette.",
            "Erreiche 5.000 Vermögen in einem Spiel.",
            "완료 경기에서 순자산 5,000을 달성하세요.",
            "Достигните капитала 5 000 в завершённом матче.");

        Add(list, "progression.achievement.turkey_explorer.title",
            "Turkey Explorer",
            "Türkiye Kaşifi",
            "Explorador de Turquía",
            "Explorateur de Turquie",
            "Türkei-Entdecker",
            "튀르키예 탐험가",
            "Исследователь Турции");

        Add(list, "progression.achievement.turkey_explorer.body",
            "Complete a match on Turkey.",
            "Türkiye haritasında bir maç tamamla.",
            "Completa una partida en Turquía.",
            "Terminez une partie en Turquie.",
            "Beende ein Spiel auf der Türkei-Karte.",
            "튀르키예 맵에서 경기를 완료하세요.",
            "Завершите матч на карте Турции.");

        Add(list, "progression.achievement.colorado_explorer.title",
            "Colorado Explorer",
            "Colorado Kaşifi",
            "Explorador de Colorado",
            "Explorateur du Colorado",
            "Colorado-Entdecker",
            "콜로라도 탐험가",
            "Исследователь Колорадо");

        Add(list, "progression.achievement.colorado_explorer.body",
            "Complete a match on Colorado.",
            "Colorado haritasında bir maç tamamla.",
            "Completa una partida en Colorado.",
            "Terminez une partie au Colorado.",
            "Beende ein Spiel auf Colorado.",
            "콜로라도 맵에서 경기를 완료하세요.",
            "Завершите матч на карте Колорадо.");

        Add(list, "progression.achievement.usa_explorer.title",
            "USA Explorer",
            "ABD Kaşifi",
            "Explorador de EE. UU.",
            "Explorateur des USA",
            "USA-Entdecker",
            "미국 탐험가",
            "Исследователь США");

        Add(list, "progression.achievement.usa_explorer.body",
            "Complete a match on USA.",
            "ABD haritasında bir maç tamamla.",
            "Completa una partida en EE. UU.",
            "Terminez une partie sur USA.",
            "Beende ein Spiel auf USA.",
            "미국 맵에서 경기를 완료하세요.",
            "Завершите матч на карте США.");

        Add(list, "progression.achievement.atlas_traveler.title",
            "Atlas Traveler",
            "Atlas Gezgini",
            "Viajero Atlas",
            "Voyageur Atlas",
            "Atlas-Reisender",
            "아틀라스 여행자",
            "Путешественник Atlas");

        Add(list, "progression.achievement.atlas_traveler.body",
            "Complete matches on Turkey, Colorado and USA.",
            "Türkiye, Colorado ve ABD haritalarında maç tamamla.",
            "Completa partidas en Turquía, Colorado y EE. UU.",
            "Terminez des parties en Turquie, au Colorado et aux USA.",
            "Beende Spiele auf Türkei, Colorado und USA.",
            "튀르키예, 콜로라도, 미국 맵에서 경기를 완료하세요.",
            "Завершите матчи на картах Турции, Колорадо и США.");
    }

    private static AtlasBoardLocalizationDatabase.Entry Clone(
        AtlasBoardLocalizationDatabase.Entry source)
    {
        return new AtlasBoardLocalizationDatabase.Entry
        {
            key = source.key,
            en = source.en,
            tr = source.tr,
            es = source.es,
            fr = source.fr,
            de = source.de,
            ko = source.ko,
            ru = source.ru
        };
    }

    private static void Add(
        List<AtlasBoardLocalizationDatabase.Entry> list,
        string key,
        string en,
        string tr,
        string es,
        string fr,
        string de,
        string ko,
        string ru)
    {
        list.Add(
            new AtlasBoardLocalizationDatabase.Entry
            {
                key = key,
                en = en,
                tr = tr,
                es = es,
                fr = fr,
                de = de,
                ko = ko,
                ru = ru
            });
    }
}
#endif
