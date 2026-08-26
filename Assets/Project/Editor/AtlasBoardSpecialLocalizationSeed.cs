#if UNITY_EDITOR
using System.Collections.Generic;

public static class AtlasBoardSpecialLocalizationSeed
{
    public static void Append(
        List<AtlasBoardLocalizationDatabase.Entry> list)
    {
        if (list == null)
        {
            return;
        }

        Add(
            list,
            "tile.special.start",
            "START",
            "BAŞLANGIÇ",
            "INICIO",
            "DÉPART",
            "START",
            "시작",
            "СТАРТ");
        Add(
            list,
            "tile.special.event",
            "EVENT",
            "ETKİNLİK",
            "EVENTO",
            "ÉVÉNEMENT",
            "EREIGNIS",
            "이벤트",
            "СОБЫТИЕ");
        Add(
            list,
            "tile.special.tax",
            "TAX",
            "VERGİ",
            "IMPUESTO",
            "TAXE",
            "STEUER",
            "세금",
            "НАЛОГ");
        Add(
            list,
            "tile.special.auction",
            "AUCTION",
            "AÇIK ARTIRMA",
            "SUBASTA",
            "ENCHÈRE",
            "AUKTION",
            "경매",
            "АУКЦИОН");
        Add(
            list,
            "tile.special.travel",
            "TRAVEL",
            "SEYAHAT",
            "VIAJE",
            "VOYAGE",
            "REISE",
            "여행",
            "ПУТЕШЕСТВИЕ");
        Add(
            list,
            "tile.special.vacation",
            "VACATION",
            "TATİL",
            "VACACIONES",
            "VACANCES",
            "URLAUB",
            "휴가",
            "ОТПУСК");
        Add(
            list,
            "tile.special.rest",
            "REST",
            "DİNLENME",
            "DESCANSO",
            "REPOS",
            "RAST",
            "휴식",
            "ОТДЫХ");
        Add(
            list,
            "tile.special.bonus",
            "BONUS",
            "BONUS",
            "BONUS",
            "BONUS",
            "BONUS",
            "보너스",
            "БОНУС");
        Add(
            list,
            "special.tax.title",
            "Tax Payment",
            "Vergi Ödemesi",
            "Pago de impuestos",
            "Paiement des taxes",
            "Steuerzahlung",
            "세금 납부",
            "Уплата налога");
        Add(
            list,
            "special.tax.description",
            "You need to pay business expenses and local taxes.",
            "İşletme giderleri ve yerel vergileri ödemen gerekiyor.",
            "Debes pagar los gastos del negocio y los impuestos locales.",
            "Vous devez payer les frais d'exploitation et les taxes locales.",
            "Du musst Betriebskosten und lokale Steuern bezahlen.",
            "사업 비용과 지방세를 납부해야 합니다.",
            "Необходимо оплатить деловые расходы и местные налоги.");
        Add(
            list,
            "special.bonus.title",
            "Regional Bonus",
            "Bölge Bonusu",
            "Bono regional",
            "Bonus régional",
            "Regionalbonus",
            "지역 보너스",
            "Региональный бонус");
        Add(
            list,
            "special.bonus.description",
            "You received a bonus from local business support.",
            "Yerel ticaret desteğinden bonus kazandın.",
            "Recibiste un bono de apoyo al comercio local.",
            "Vous avez reçu un bonus grâce au soutien du commerce local.",
            "Du hast einen Bonus aus lokaler Wirtschaftsförderung erhalten.",
            "지역 비즈니스 지원으로 보너스를 받았습니다.",
            "Вы получили бонус по программе поддержки местного бизнеса.");
        Add(
            list,
            "special.rest.title",
            "Rest Area",
            "Dinlenme Alanı",
            "Zona de descanso",
            "Aire de repos",
            "Rastplatz",
            "휴식 구역",
            "Зона отдыха");
        Add(
            list,
            "special.rest.skip_one",
            "You will skip your next turn.",
            "Bir sonraki turunu atlayacaksın.",
            "Perderás tu próximo turno.",
            "Vous passerez votre prochain tour.",
            "Du setzt deinen nächsten Zug aus.",
            "다음 턴을 쉽니다.",
            "Вы пропустите следующий ход.");
        Add(
            list,
            "special.rest.skip_many",
            "You will skip your next {0} turns.",
            "Sonraki {0} turunu atlayacaksın.",
            "Perderás tus próximos {0} turnos.",
            "Vous passerez vos {0} prochains tours.",
            "Du setzt deine nächsten {0} Züge aus.",
            "다음 {0}턴을 쉽니다.",
            "Вы пропустите следующие {0} хода.");
        Add(
            list,
            "special.vacation.title",
            "Vacation Area",
            "Tatil Bölgesi",
            "Zona de vacaciones",
            "Zone de vacances",
            "Urlaubsgebiet",
            "휴가 구역",
            "Зона отпуска");
        Add(
            list,
            "special.vacation.description",
            "You earned extra income from a vacation event.",
            "Tatil etkinliğinden ek gelir kazandın.",
            "Obtuviste ingresos extra gracias a un evento de vacaciones.",
            "Vous avez gagné un revenu supplémentaire grâce à un événement de vacances.",
            "Du hast durch ein Urlaubsereignis zusätzliches Einkommen erhalten.",
            "휴가 이벤트로 추가 수익을 얻었습니다.",
            "Вы получили дополнительный доход благодаря отпускному событию.");
        Add(
            list,
            "special.travel.center",
            "TRAVEL HUB",
            "SEYAHAT MERKEZİ",
            "CENTRO DE VIAJE",
            "CENTRE DE VOYAGE",
            "REISEZENTRUM",
            "여행 허브",
            "ЦЕНТР ПУТЕШЕСТВИЙ");
        Add(
            list,
            "special.travel.question",
            "Would you like to travel to the nearest Event space?",
            "En yakın Etkinlik karesine gitmek ister misin?",
            "¿Quieres viajar a la casilla de Evento más cercana?",
            "Voulez-vous aller vers la case Événement la plus proche ?",
            "Möchtest du zum nächsten Ereignisfeld reisen?",
            "가장 가까운 이벤트 칸으로 이동하시겠습니까?",
            "Хотите переместиться на ближайшую клетку события?");
        Add(
            list,
            "special.travel.target",
            "Target: {0}",
            "Hedef: {0}",
            "Destino: {0}",
            "Destination : {0}",
            "Ziel: {0}",
            "목표: {0}",
            "Цель: {0}");
        Add(
            list,
            "special.travel.fee",
            "Travel fee: {0} ₵",
            "Seyahat ücreti: {0} ₵",
            "Coste del viaje: {0} ₵",
            "Coût du voyage : {0} ₵",
            "Reisekosten: {0} ₵",
            "여행 비용: {0} ₵",
            "Стоимость поездки: {0} ₵");
        Add(
            list,
            "special.travel.free",
            "Travel fee: Free",
            "Seyahat ücreti: Ücretsiz",
            "Coste del viaje: Gratis",
            "Coût du voyage : Gratuit",
            "Reisekosten: Kostenlos",
            "여행 비용: 무료",
            "Стоимость поездки: Бесплатно");
        Add(
            list,
            "special.travel.start_reward",
            "If you pass Start, you earn +{0} ₵.",
            "Başlangıçtan geçersen +{0} ₵ kazanırsın.",
            "Si pasas por Inicio, ganas +{0} ₵.",
            "Si vous passez par Départ, vous gagnez +{0} ₵.",
            "Wenn du Start passierst, erhältst du +{0} ₵.",
            "시작 칸을 지나면 +{0} ₵를 받습니다.",
            "Если вы пройдёте через Старт, получите +{0} ₵.");
        Add(
            list,
            "special.travel.insufficient",
            "Insufficient balance: you cannot travel.",
            "Bakiye yetersiz: seyahat edemezsin.",
            "Saldo insuficiente: no puedes viajar.",
            "Solde insuffisant : vous ne pouvez pas voyager.",
            "Nicht genug Geld: Du kannst nicht reisen.",
            "잔액이 부족하여 이동할 수 없습니다.",
            "Недостаточно средств: путешествие недоступно.");
        Add(
            list,
            "special.result.no_money_change",
            "No money change",
            "Para değişmedi",
            "Sin cambios de dinero",
            "Aucun changement d'argent",
            "Keine Geldänderung",
            "금액 변동 없음",
            "Деньги не изменились");
        Add(
            list,
            "special.result.bankrupt",
            "BANKRUPT\nPaid: {0} ₵\nTransferred/released properties: {1}",
            "İFLAS\nÖdenen: {0} ₵\nDevredilen/boşa çıkan mülk: {1}",
            "BANCARROTA\nPagado: {0} ₵\nPropiedades transferidas/liberadas: {1}",
            "FAILLITE\nPayé : {0} ₵\nPropriétés transférées/libérées : {1}",
            "INSOLVENT\nBezahlt: {0} ₵\nÜbertragene/freigegebene Grundstücke: {1}",
            "파산\n지불: {0} ₵\n이전/해제된 부동산: {1}",
            "БАНКРОТ\nОплачено: {0} ₵\nПередано/освобождено объектов: {1}");
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
