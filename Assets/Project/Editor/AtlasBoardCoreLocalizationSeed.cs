#if UNITY_EDITOR
using System.Collections.Generic;

public static class AtlasBoardCoreLocalizationSeed
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
            "rent.bankrupt.title",
            "BANKRUPT",
            "İFLAS",
            "BANCARROTA",
            "FAILLITE",
            "INSOLVENT",
            "파산",
            "БАНКРОТ");
        Add(
            list,
            "rent.bankrupt.description",
            "{0} could not pay the {2} ₵ rent required for {1}. Remaining cash and properties were transferred to {3}.",
            "{0}, {1} için gereken {2} ₵ kirayı ödeyemedi. Kalan nakit ve mülkler {3} hesabına aktarıldı.",
            "{0} no pudo pagar el alquiler de {2} ₵ requerido por {1}. El dinero y las propiedades restantes se transfirieron a {3}.",
            "{0} n'a pas pu payer le loyer de {2} ₵ pour {1}. L'argent et les propriétés restants ont été transférés à {3}.",
            "{0} konnte die für {1} fällige Miete von {2} ₵ nicht bezahlen. Verbleibendes Geld und Grundstücke wurden an {3} übertragen.",
            "{0}님이 {1}의 임대료 {2} ₵를 지불하지 못했습니다. 남은 현금과 부동산은 {3}에게 이전되었습니다.",
            "{0} не смог оплатить аренду {2} ₵ за {1}. Оставшиеся деньги и собственность переданы {3}.");
        Add(
            list,
            "rent.bankrupt.result",
            "Paid: {0} ₵\nUnpaid: {1} ₵\nTransferred properties: {2}",
            "Ödenen: {0} ₵\nKarşılanamayan: {1} ₵\nDevredilen mülk: {2}",
            "Pagado: {0} ₵\nPendiente: {1} ₵\nPropiedades transferidas: {2}",
            "Payé : {0} ₵\nImpayé : {1} ₵\nPropriétés transférées : {2}",
            "Bezahlt: {0} ₵\nOffen: {1} ₵\nÜbertragene Grundstücke: {2}",
            "지불: {0} ₵\n미지급: {1} ₵\n이전된 부동산: {2}",
            "Оплачено: {0} ₵\nНе оплачено: {1} ₵\nПередано объектов: {2}");
        Add(
            list,
            "development.balanced_rule",
            "Balanced development rule: develop one of the Level {0} properties in this group first.",
            "Dengeli geliştirme kuralı: Önce bölgedeki Seviye {0} mülklerden birini geliştir.",
            "Regla de desarrollo equilibrado: primero desarrolla una de las propiedades de nivel {0} del grupo.",
            "Règle de développement équilibré : développez d'abord l'une des propriétés de niveau {0} du groupe.",
            "Regel für ausgeglichene Entwicklung: Entwickle zuerst eines der Grundstücke auf Stufe {0} in dieser Gruppe.",
            "균형 개발 규칙: 먼저 이 그룹의 레벨 {0} 부동산 중 하나를 개발하세요.",
            "Правило сбалансированного развития: сначала улучшите один из объектов уровня {0} в группе.");
        Add(
            list,
            "development.insufficient_balance",
            "Insufficient balance for this development.",
            "Bu geliştirme için bakiye yetersiz.",
            "Saldo insuficiente para este desarrollo.",
            "Solde insuffisant pour ce développement.",
            "Nicht genug Geld für diese Entwicklung.",
            "이 개발에 필요한 잔액이 부족합니다.",
            "Недостаточно средств для этого улучшения.");
        Add(
            list,
            "development.unknown_group",
            "Unknown Group",
            "Bilinmeyen Bölge",
            "Grupo desconocido",
            "Groupe inconnu",
            "Unbekannte Gruppe",
            "알 수 없는 그룹",
            "Неизвестная группа");
        Add(
            list,
            "development.group_number",
            "Group {0}",
            "Bölge {0}",
            "Grupo {0}",
            "Groupe {0}",
            "Gruppe {0}",
            "그룹 {0}",
            "Группа {0}");
        Add(
            list,
            "development.group_levels",
            "Group levels: {0}",
            "Bölge seviyeleri: {0}",
            "Niveles del grupo: {0}",
            "Niveaux du groupe : {0}",
            "Gruppenstufen: {0}",
            "그룹 레벨: {0}",
            "Уровни группы: {0}");
        Add(
            list,
            "development.group_complete",
            "{0} complete",
            "{0} tamamlandı",
            "{0} completo",
            "{0} terminé",
            "{0} vollständig",
            "{0} 완료",
            "{0} завершена");
        Add(
            list,
            "development.level",
            "Level: {0}/{1}",
            "Seviye: {0}/{1}",
            "Nivel: {0}/{1}",
            "Niveau : {0}/{1}",
            "Stufe: {0}/{1}",
            "레벨: {0}/{1}",
            "Уровень: {0}/{1}");
        Add(
            list,
            "development.rent",
            "Rent: {0} ₵ → {1} ₵",
            "Kira: {0} ₵ → {1} ₵",
            "Alquiler: {0} ₵ → {1} ₵",
            "Loyer : {0} ₵ → {1} ₵",
            "Miete: {0} ₵ → {1} ₵",
            "임대료: {0} ₵ → {1} ₵",
            "Аренда: {0} ₵ → {1} ₵");
        Add(
            list,
            "development.cost",
            "Development cost: {0} ₵",
            "Geliştirme maliyeti: {0} ₵",
            "Coste de desarrollo: {0} ₵",
            "Coût du développement : {0} ₵",
            "Entwicklungskosten: {0} ₵",
            "개발 비용: {0} ₵",
            "Стоимость улучшения: {0} ₵");
        Add(
            list,
            "development.balance",
            "Balance: {0} ₵",
            "Bakiye: {0} ₵",
            "Saldo: {0} ₵",
            "Solde : {0} ₵",
            "Guthaben: {0} ₵",
            "잔액: {0} ₵",
            "Баланс: {0} ₵");
        Add(
            list,
            "match.player_bankrupt",
            "{0} — BANKRUPT",
            "{0} — İFLAS",
            "{0} — BANCARROTA",
            "{0} — FAILLITE",
            "{0} — INSOLVENT",
            "{0} — 파산",
            "{0} — БАНКРОТ");
        Add(
            list,
            "match.cash_properties",
            "Cash: {0} ₵ | Properties: {1} | Property Value: {2} ₵",
            "Nakit: {0} ₵ | Mülk: {1} | Mülk Değeri: {2} ₵",
            "Efectivo: {0} ₵ | Propiedades: {1} | Valor: {2} ₵",
            "Liquidités : {0} ₵ | Propriétés : {1} | Valeur : {2} ₵",
            "Bargeld: {0} ₵ | Grundstücke: {1} | Wert: {2} ₵",
            "현금: {0} ₵ | 부동산: {1} | 가치: {2} ₵",
            "Наличные: {0} ₵ | Собственность: {1} | Стоимость: {2} ₵");
        Add(
            list,
            "match.development",
            "Development Levels: {0} | Development Value: {1} ₵",
            "Geliştirme Seviyesi: {0} | Geliştirme Değeri: {1} ₵",
            "Niveles de desarrollo: {0} | Valor de desarrollo: {1} ₵",
            "Niveaux de développement : {0} | Valeur : {1} ₵",
            "Entwicklungsstufen: {0} | Entwicklungswert: {1} ₵",
            "개발 레벨: {0} | 개발 가치: {1} ₵",
            "Уровни улучшений: {0} | Стоимость улучшений: {1} ₵");
        Add(
            list,
            "match.net_worth",
            "Net Worth: {0} ₵",
            "Net Servet: {0} ₵",
            "Patrimonio neto: {0} ₵",
            "Valeur nette : {0} ₵",
            "Nettovermögen: {0} ₵",
            "순자산: {0} ₵",
            "Чистая стоимость: {0} ₵");
        Add(
            list,
            "match.winner",
            "{0} Won!\n{1} ₵",
            "{0} Kazandı!\n{1} ₵",
            "¡{0} ganó!\n{1} ₵",
            "{0} a gagné !\n{1} ₵",
            "{0} hat gewonnen!\n{1} ₵",
            "{0} 승리!\n{1} ₵",
            "{0} победил!\n{1} ₵");
        Add(
            list,
            "match.tie",
            "Tie!\n{0} ₵",
            "Beraberlik!\n{0} ₵",
            "¡Empate!\n{0} ₵",
            "Égalité !\n{0} ₵",
            "Unentschieden!\n{0} ₵",
            "무승부!\n{0} ₵",
            "Ничья!\n{0} ₵");
        Add(
            list,
            "match.complete",
            "Match Complete",
            "Maç Tamamlandı",
            "Partida terminada",
            "Partie terminée",
            "Spiel beendet",
            "게임 종료",
            "Матч завершён");
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
