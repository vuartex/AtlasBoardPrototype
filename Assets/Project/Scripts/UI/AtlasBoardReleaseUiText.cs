/// <summary>Seven-language honest empty states; no fake events or rewards.</summary>
public static class AtlasBoardReleaseUiText
{
    private static int Language()
    {
        var manager = AtlasBoardLocalizationManager.Instance;
        string code = manager != null ? manager.CurrentLanguageCode : "en";
        switch ((code ?? "en").ToLowerInvariant())
        {
            case "tr": return 1; case "es": return 2; case "fr": return 3;
            case "de": return 4; case "ko": return 5; case "ru": return 6;
            default: return 0;
        }
    }
    public static string EventsUnavailable() => new [] {
        "Event status is temporarily unavailable. Please check again later.",
        "Etkinlik durumu şu anda alınamıyor. Lütfen daha sonra tekrar kontrol et.",
        "El estado de los eventos no está disponible. Vuelve más tarde.",
        "Statut des événements indisponible. Réessayez plus tard.",
        "Event-Status derzeit nicht verfügbar. Bitte später erneut versuchen.",
        "이벤트 상태를 확인할 수 없습니다. 나중에 다시 시도하세요.",
        "Статус событий временно недоступен. Попробуйте позже." }[Language()];
    public static string NoEvents() => new [] {
        "No active events right now. Check back for future events and rewards.",
        "Şu anda aktif etkinlik yok. Yeni etkinlik ve ödüller için tekrar kontrol et.",
        "No hay eventos activos. Vuelve para ver futuros eventos y premios.",
        "Aucun événement actif. Revenez bientôt pour les prochains événements.",
        "Zurzeit keine aktiven Events. Schau später wieder vorbei.",
        "현재 진행 중인 이벤트가 없습니다. 새로운 이벤트를 기다려 주세요.",
        "Сейчас нет активных событий. Загляните позже за новыми событиями." }[Language()];
    public static string NoTransactions() => new [] {
        "No verified transaction receipts yet. New purchases appear here after the history rules are deployed. Older purchases are not reconstructed from inventory.",
        "Henüz doğrulanmış işlem makbuzu yok. Geçmiş kuralları deploy edilince yeni alımlar burada görünür. Eski alımlar envanterden tahmin edilmez.",
        "Aún no hay recibos verificados. Las compras nuevas aparecerán tras actualizar las reglas. No se inventan compras antiguas.",
        "Aucun reçu vérifié. Les nouveaux achats apparaîtront après la mise à jour des règles. Aucun achat ancien n’est inventé.",
        "Noch keine geprüften Belege. Neue Käufe erscheinen nach dem Regel-Update. Alte Käufe werden nicht erfunden.",
        "아직 검증된 거래 영수증이 없습니다. 규칙 배포 이후 새 구매가 표시됩니다. 이전 구매는 추정하지 않습니다.",
        "Подтверждённых чеков пока нет. Новые покупки появятся после обновления правил. Старые покупки не выдумываются." }[Language()];
    public static string NoVerifiedCareer() => new [] {
        "No verified Career record yet. Permanent XP requires verified rewarded online matches.",
        "Henüz doğrulanmış Career kaydı yok. Kalıcı XP için doğrulanmış ödüllü online maç gerekir.",
        "Aún no hay carrera verificada. La XP permanente exige partidas online verificadas.",
        "Aucune carrière vérifiée. L’XP durable exige des matchs en ligne validés.",
        "Noch kein bestätigter Karrierestand. Dauerhafte EP erfordern verifizierte Online-Spiele.",
        "검증된 커리어 기록이 없습니다. 영구 XP에는 검증된 온라인 경기가 필요합니다.",
        "Подтверждённой карьеры пока нет. Постоянный опыт требует проверенных онлайн-матчей." }[Language()];
    public static string CareerNoHistory(bool existingProfile) => new [] {
        "No verified online match history yet. Offline and bot-only games do not award permanent XP.",
        "Henüz doğrulanmış online maç geçmişi yok. Offline ve yalnız botlu maçlar kalıcı XP vermez.",
        "Aún no hay historial online verificado. Las partidas offline o solo con bots no dan XP permanente.",
        "Aucun match en ligne vérifié. Les parties hors ligne ou contre des bots ne donnent pas d’XP durable.",
        "Noch keine bestätigten Online-Spiele. Offline- und Bot-Spiele bringen keine dauerhaften EP.",
        "확인된 온라인 경기 기록이 없습니다. 오프라인 및 봇 전용 경기는 영구 XP를 제공하지 않습니다.",
        "Пока нет подтверждённых онлайн-матчей. Офлайн и игры только с ботами не дают постоянный опыт." }[Language()];
    public static string EventsComingSoonTitle() => new [] {
        "NEW EVENTS ARE ON THE WAY", "YENİ ETKİNLİKLER YOLDA",
        "PRÓXIMOS EVENTOS", "DE NOUVEAUX ÉVÉNEMENTS ARRIVENT",
        "NEUE EVENTS KOMMEN BALD", "새 이벤트가 곧 찾아옵니다",
        "СКОРО НОВЫЕ СОБЫТИЯ" }[Language()];
    public static string EventsComingSoonBody() => new [] {
        "There are no active events right now.\nStay tuned for future challenges and seasonal surprises!",
        "Şu anda yaklaşan bir etkinlik bulunmuyor.\nYeni görevler ve sezonluk sürprizler için takipte kal!",
        "No hay eventos próximos por ahora.\n¡Vuelve pronto para descubrir nuevos desafíos!",
        "Aucun événement à venir pour le moment.\nRevenez bientôt pour de nouveaux défis !",
        "Aktuell stehen keine Events an.\nSchau bald wieder für neue Herausforderungen vorbei!",
        "현재 예정된 이벤트가 없습니다.\n새로운 도전과 시즌 소식을 기대해 주세요!",
        "Пока нет предстоящих событий.\nЗагляните позже за новыми испытаниями!" }[Language()];
    public static string EventsComingSoonFooter() => new [] {
        "No tickets or rewards are available until an event begins.",
        "Etkinlik başlayana kadar bilet veya ödül sunulmuyor.",
        "No hay boletos ni premios hasta que comience un evento.",
        "Aucun billet ni récompense avant le début d'un événement.",
        "Tickets und Belohnungen gibt es erst zum Eventstart.",
        "이벤트 시작 전에는 티켓이나 보상이 제공되지 않습니다.",
        "Билеты и награды появятся после старта события." }[Language()];
    public static string EventsStatusPendingFooter() => new [] {
        "Event announcements will appear here when available.",
        "Etkinlik duyuruları hazır olduğunda burada gösterilecek.",
        "Los anuncios de eventos aparecerán aquí.",
        "Les annonces d'événements apparaîtront ici.",
        "Eventankündigungen erscheinen hier, sobald verfügbar.",
        "이벤트 공지는 준비되면 이곳에 표시됩니다.",
        "Объявления о событиях появятся здесь позже." }[Language()];
    public static string PeerAttestedCareerNotice() => new [] {
        "Career XP is confirmed by two players, not yet by an independent anti-cheat verifier. Gold and ranked rewards are not granted.",
        "Career XP iki oyuncunun onayıyla kaydedildi; bağımsız anti-cheat doğrulaması henüz yok. Gold ve dereceli ödüller verilmez.",
        "La XP está confirmada por dos jugadores, aún sin verificación anti-trampas independiente. Sin oro ni premios competitivos.",
        "XP confirmée par deux joueurs, sans vérification anti-triche indépendante. Aucun or ou prix classé.",
        "EP durch zwei Spieler bestätigt, noch ohne unabhängige Anti-Cheat-Prüfung. Kein Gold oder Ranglistenpreis.",
        "두 플레이어가 XP를 확인했지만 독립적인 치트 방지 검증은 아닙니다. 골드나 랭킹 보상은 없습니다.",
        "Опыт подтверждён двумя игроками, но без независимой защиты от читов. Золото и рейтинговые награды не выдаются." }[Language()];
}
