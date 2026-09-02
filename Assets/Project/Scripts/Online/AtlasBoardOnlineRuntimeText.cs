using System;
using UnityEngine;

public static class AtlasBoardOnlineRuntimeText
{
    public static string SeatLeftTemporaryBot(string name) =>
        Format(
            "{0} left the match. A Temporary Bot will play this seat for up to 5 minutes while reconnect is available.",
            "{0} maçtan ayrıldı. Yeniden bağlanabilmesi için 5 dakika boyunca bu koltuğu Geçici Bot yönetecek.",
            "{0} abandonó la partida. Un bot temporal jugará este asiento durante un máximo de 5 minutos mientras sea posible reconectarse.",
            "{0} a quitté la partie. Un bot temporaire jouera ce siège pendant 5 minutes maximum pendant que la reconnexion reste possible.",
            "{0} hat das Spiel verlassen. Bis zu 5 Minuten übernimmt ein temporärer Bot diesen Platz, solange eine Wiederverbindung möglich ist.",
            "{0}님이 경기에서 나갔습니다. 재접속이 가능한 최대 5분 동안 임시 봇이 이 자리를 플레이합니다.",
            "{0} покинул(а) матч. До 5 минут это место будет занимать временный бот, пока доступно переподключение.",
            name);

    public static string SeatRejoined(string name) =>
        Format(
            "{0} rejoined the match and reclaimed the seat.",
            "{0} maça yeniden katıldı ve koltuğunu geri aldı.",
            "{0} volvió a la partida y recuperó su asiento.",
            "{0} a rejoint la partie et a récupéré son siège.",
            "{0} ist dem Spiel wieder beigetreten und hat den Platz zurückerhalten.",
            "{0}님이 경기에 다시 참가하여 자리를 되찾았습니다.",
            "{0} вернулся(ась) в матч и снова занял(а) своё место.",
            name);

    public static string SeatAfkRemoved(string name) =>
        Format(
            "{0} was removed for inactivity. This seat is now a Permanent Bot.",
            "{0} hareketsizlik nedeniyle oyundan çıkarıldı. Bu koltuk artık Kalıcı Bot tarafından yönetiliyor.",
            "{0} fue expulsado por inactividad. Este asiento ahora lo controla un bot permanente.",
            "{0} a été retiré pour inactivité. Ce siège est désormais contrôlé par un bot permanent.",
            "{0} wurde wegen Inaktivität entfernt. Dieser Platz wird jetzt dauerhaft von einem Bot gesteuert.",
            "{0}님이 비활성 상태로 인해 퇴장되었습니다. 이제 이 자리는 영구 봇이 제어합니다.",
            "{0} удалён(а) за бездействие. Теперь этим местом управляет постоянный бот.",
            name);

    public static string SeatReconnectExpired(string name) =>
        Format(
            "{0} did not return within 5 minutes. This seat is now a Permanent Bot.",
            "{0} 5 dakika içinde geri dönmedi. Bu koltuk artık Kalıcı Bot tarafından yönetiliyor.",
            "{0} no regresó en 5 minutos. Este asiento ahora lo controla un bot permanente.",
            "{0} n’est pas revenu dans les 5 minutes. Ce siège est désormais contrôlé par un bot permanent.",
            "{0} ist innerhalb von 5 Minuten nicht zurückgekehrt. Dieser Platz wird jetzt dauerhaft von einem Bot gesteuert.",
            "{0}님이 5분 안에 돌아오지 않았습니다. 이제 이 자리는 영구 봇이 제어합니다.",
            "{0} не вернулся(ась) в течение 5 минут. Теперь этим местом управляет постоянный бот.",
            name);

    public static string LocalAfkRemoved(string name) =>
        Format(
            "{0} was removed from this match for inactivity. The seat is now controlled by a Permanent Bot.",
            "{0}, hareketsizlik nedeniyle bu maçtan çıkarıldın. Koltuğun artık Kalıcı Bot tarafından yönetiliyor.",
            "{0}, fuiste expulsado de esta partida por inactividad. Tu asiento ahora lo controla un bot permanente.",
            "{0}, vous avez été retiré de cette partie pour inactivité. Votre siège est désormais contrôlé par un bot permanent.",
            "{0}, du wurdest wegen Inaktivität aus diesem Spiel entfernt. Dein Platz wird jetzt dauerhaft von einem Bot gesteuert.",
            "{0}님, 비활성 상태로 인해 이 경기에서 퇴장되었습니다. 이제 해당 자리는 영구 봇이 제어합니다.",
            "{0}, вы были удалены из матча за бездействие. Теперь вашим местом управляет постоянный бот.",
            name);

    public static string ActiveMatchReservedSeatOnly() =>
        Select(
            "This room already has an active match. Only the same returning player can reclaim a reserved seat.",
            "Bu odada devam eden bir maç var. Ayrılmış koltuğu yalnızca aynı geri dönen oyuncu geri alabilir.",
            "Esta sala ya tiene una partida activa. Solo el mismo jugador que regresa puede recuperar un asiento reservado.",
            "Cette salle a déjà une partie en cours. Seul le même joueur de retour peut récupérer un siège réservé.",
            "In diesem Raum läuft bereits ein Spiel. Nur derselbe zurückkehrende Spieler kann einen reservierten Platz zurückfordern.",
            "이 방에는 이미 진행 중인 경기가 있습니다. 예약된 자리는 동일한 복귀 플레이어만 되찾을 수 있습니다.",
            "В этой комнате уже идёт матч. Зарезервированное место может вернуть только тот же возвращающийся игрок.");

    public static string WaitingForHost() =>
        Select(
            "Waiting for the lobby host's decision.",
            "Lobi sahibinin karar vermesi bekleniyor.",
            "Esperando la decisión del anfitrión de la sala.",
            "En attente de la décision de l’hôte du salon.",
            "Warten auf die Entscheidung des Lobby-Hosts.",
            "로비 호스트의 결정을 기다리는 중입니다.",
            "Ожидание решения владельца лобби.");

    public static string LeaveMatch() =>
        Select(
            "LEAVE MATCH",
            "OYUNDAN AYRIL",
            "SALIR DE LA PARTIDA",
            "QUITTER LA PARTIE",
            "SPIEL VERLASSEN",
            "경기 나가기",
            "ПОКИНУТЬ МАТЧ");

    public static string Rematch() =>
        Select(
            "REMATCH",
            "YENİDEN BAŞLAT",
            "REVANCHA",
            "REJOUER",
            "REVANCHE",
            "다시 하기",
            "РЕВАНШ");

    public static string Rematching() =>
        Select(
            "RESTARTING MATCH",
            "OYUN YENİDEN BAŞLATILIYOR",
            "REINICIANDO PARTIDA",
            "REDÉMARRAGE DE LA PARTIE",
            "SPIEL WIRD NEU GESTARTET",
            "경기를 다시 시작하는 중",
            "ПЕРЕЗАПУСК МАТЧА");

    public static string RematchFailed() =>
        Select(
            "Rematch could not be started. Please try again.",
            "Oyun yeniden başlatılamadı. Lütfen tekrar deneyin.",
            "No se pudo iniciar la revancha. Inténtalo de nuevo.",
            "Impossible de relancer la partie. Veuillez réessayer.",
            "Das Spiel konnte nicht neu gestartet werden. Bitte erneut versuchen.",
            "경기를 다시 시작할 수 없습니다. 다시 시도해 주세요.",
            "Не удалось начать реванш. Попробуйте ещё раз.");

    private static string Format(
        string en,
        string tr,
        string es,
        string fr,
        string de,
        string ko,
        string ru,
        params object[] args)
    {
        string template =
            Select(en, tr, es, fr, de, ko, ru);

        try
        {
            return args != null && args.Length > 0
                ? string.Format(template, args)
                : template;
        }
        catch (FormatException)
        {
            return template;
        }
    }

    private static string Select(
        string en,
        string tr,
        string es,
        string fr,
        string de,
        string ko,
        string ru)
    {
        string language =
            AtlasBoardLocalizationManager.Instance != null
                ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
                : "en";

        switch ((language ?? "en").Trim().ToLowerInvariant())
        {
            case "tr": return tr;
            case "es": return es;
            case "fr": return fr;
            case "de": return de;
            case "ko": return ko;
            case "ru": return ru;
            default: return en;
        }
    }
}
