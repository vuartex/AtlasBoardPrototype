#if UNITY_EDITOR
using System.Collections.Generic;

public static class AtlasBoardEventLocalizationSeed
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
            "event.card.event_positive_large_order.title",
            "Large Order",
            "Büyük Sipariş",
            "Gran pedido",
            "Commande importante",
            "Großauftrag",
            "대규모 주문",
            "Крупный заказ");

        Add(
            list,
            "event.card.event_positive_large_order.description",
            "You received an unexpectedly large order.",
            "Beklenmedik büyük bir sipariş aldın.",
            "Recibiste un pedido inesperadamente grande.",
            "Vous avez reçu une commande exceptionnellement importante.",
            "Du hast unerwartet einen Großauftrag erhalten.",
            "예상치 못한 대규모 주문을 받았습니다.",
            "Вы неожиданно получили крупный заказ.");

        Add(
            list,
            "event.card.event_positive_busy_season.title",
            "Busy Season",
            "Yoğun Sezon",
            "Temporada alta",
            "Haute saison",
            "Hochsaison",
            "성수기",
            "Высокий сезон");

        Add(
            list,
            "event.card.event_positive_busy_season.description",
            "Business went better than expected.",
            "İşlerin beklenenden daha iyi gitti.",
            "El negocio fue mejor de lo esperado.",
            "Les affaires ont été meilleures que prévu.",
            "Das Geschäft lief besser als erwartet.",
            "사업이 예상보다 잘 풀렸습니다.",
            "Дела пошли лучше, чем ожидалось.");

        Add(
            list,
            "event.card.event_positive_local_support.title",
            "Local Support",
            "Yerel Destek",
            "Apoyo local",
            "Soutien local",
            "Lokale Förderung",
            "지역 지원",
            "Местная поддержка");

        Add(
            list,
            "event.card.event_positive_local_support.description",
            "You received a payment from a local support program.",
            "Yerel bir destek programından ödeme aldın.",
            "Recibiste un pago de un programa de apoyo local.",
            "Vous avez reçu une aide d'un programme local.",
            "Du hast eine Zahlung aus einem lokalen Förderprogramm erhalten.",
            "지역 지원 프로그램에서 지원금을 받았습니다.",
            "Вы получили выплату по местной программе поддержки.");

        Add(
            list,
            "event.card.event_positive_investment.title",
            "New Investment",
            "Yeni Yatırım",
            "Nueva inversión",
            "Nouvel investissement",
            "Neue Investition",
            "신규 투자",
            "Новая инвестиция");

        Add(
            list,
            "event.card.event_positive_investment.description",
            "A new investment opportunity provided extra resources.",
            "Yeni bir yatırım fırsatı sana ek kaynak sağladı.",
            "Una nueva oportunidad de inversión te dio recursos adicionales.",
            "Une nouvelle opportunité d'investissement vous a apporté des ressources supplémentaires.",
            "Eine neue Investitionsmöglichkeit brachte zusätzliche Mittel.",
            "새로운 투자 기회로 추가 자금을 확보했습니다.",
            "Новая инвестиционная возможность принесла дополнительные средства.");

        Add(
            list,
            "event.card.event_positive_savings.title",
            "Savings Success",
            "Tasarruf Başarısı",
            "Ahorro exitoso",
            "Économies réussies",
            "Erfolgreich gespart",
            "절약 성공",
            "Удачная экономия");

        Add(
            list,
            "event.card.event_positive_savings.description",
            "You closed the period with lower expenses than expected.",
            "Beklenenden düşük giderlerle dönemi kapattın.",
            "Cerraste el periodo con menos gastos de lo esperado.",
            "Vous avez terminé la période avec moins de dépenses que prévu.",
            "Du hast den Zeitraum mit geringeren Ausgaben als erwartet abgeschlossen.",
            "예상보다 적은 비용으로 기간을 마쳤습니다.",
            "Вы завершили период с меньшими расходами, чем ожидалось.");

        Add(
            list,
            "event.card.event_positive_campaign.title",
            "Campaign Success",
            "Kampanya Başarısı",
            "Campaña exitosa",
            "Campagne réussie",
            "Erfolgreiche Kampagne",
            "캠페인 성공",
            "Успешная кампания");

        Add(
            list,
            "event.card.event_positive_campaign.description",
            "Your campaign generated a strong return.",
            "Yaptığın kampanya güçlü bir geri dönüş sağladı.",
            "Tu campaña generó un gran retorno.",
            "Votre campagne a généré un excellent retour.",
            "Deine Kampagne brachte einen starken Ertrag.",
            "캠페인이 큰 성과를 냈습니다.",
            "Ваша кампания принесла высокий результат.");

        Add(
            list,
            "event.card.event_positive_tax_refund.title",
            "Tax Refund",
            "Vergi İadesi",
            "Reembolso fiscal",
            "Remboursement fiscal",
            "Steuerrückzahlung",
            "세금 환급",
            "Возврат налога");

        Add(
            list,
            "event.card.event_positive_tax_refund.description",
            "You received a larger tax refund than expected.",
            "Beklenenden yüksek bir vergi iadesi aldın.",
            "Recibiste un reembolso fiscal mayor de lo esperado.",
            "Vous avez reçu un remboursement fiscal plus élevé que prévu.",
            "Du hast eine höhere Steuerrückzahlung als erwartet erhalten.",
            "예상보다 큰 세금 환급을 받았습니다.",
            "Вы получили больший возврат налога, чем ожидалось.");

        Add(
            list,
            "event.card.event_positive_supplier_discount.title",
            "Supplier Discount",
            "Tedarikçi İndirimi",
            "Descuento del proveedor",
            "Remise fournisseur",
            "Lieferantenrabatt",
            "공급업체 할인",
            "Скидка поставщика");

        Add(
            list,
            "event.card.event_positive_supplier_discount.description",
            "Your supplier gave you a special discount.",
            "Tedarikçin özel bir indirim uyguladı.",
            "Tu proveedor te ofreció un descuento especial.",
            "Votre fournisseur vous a accordé une remise spéciale.",
            "Dein Lieferant hat dir einen Sonderrabatt gewährt.",
            "공급업체가 특별 할인을 제공했습니다.",
            "Поставщик предоставил вам специальную скидку.");

        Add(
            list,
            "event.card.event_positive_viral_post.title",
            "Viral Post",
            "Viral Paylaşım",
            "Publicación viral",
            "Publication virale",
            "Viraler Beitrag",
            "바이럴 게시물",
            "Вирусная публикация");

        Add(
            list,
            "event.card.event_positive_viral_post.description",
            "One of your posts unexpectedly gained attention.",
            "Bir paylaşımın beklenmedik şekilde ilgi gördü.",
            "Una de tus publicaciones llamó la atención inesperadamente.",
            "L'une de vos publications a attiré une attention inattendue.",
            "Einer deiner Beiträge bekam unerwartet viel Aufmerksamkeit.",
            "게시물 하나가 예상치 못하게 큰 관심을 받았습니다.",
            "Одна из ваших публикаций неожиданно привлекла много внимания.");

        Add(
            list,
            "event.card.event_positive_partnership.title",
            "New Partnership",
            "Yeni Ortaklık",
            "Nueva alianza",
            "Nouveau partenariat",
            "Neue Partnerschaft",
            "새 파트너십",
            "Новое партнёрство");

        Add(
            list,
            "event.card.event_positive_partnership.description",
            "A new partnership generated extra income.",
            "Yeni bir ortaklık ek gelir sağladı.",
            "Una nueva alianza generó ingresos adicionales.",
            "Un nouveau partenariat a généré des revenus supplémentaires.",
            "Eine neue Partnerschaft brachte zusätzliche Einnahmen.",
            "새로운 파트너십으로 추가 수익을 얻었습니다.",
            "Новое партнёрство принесло дополнительный доход.");

        Add(
            list,
            "event.card.event_positive_insurance_refund.title",
            "Insurance Refund",
            "Sigorta İadesi",
            "Reembolso del seguro",
            "Remboursement d'assurance",
            "Versicherungsrückzahlung",
            "보험 환급",
            "Страховая выплата");

        Add(
            list,
            "event.card.event_positive_insurance_refund.description",
            "You received a reimbursement from an old claim.",
            "Eski bir dosyadan geri ödeme aldın.",
            "Recibiste un reembolso de una reclamación anterior.",
            "Vous avez reçu un remboursement lié à un ancien dossier.",
            "Du hast eine Rückzahlung aus einem alten Versicherungsfall erhalten.",
            "이전 보험 청구에서 환급금을 받았습니다.",
            "Вы получили выплату по старому страховому случаю.");

        Add(
            list,
            "event.card.event_positive_efficiency.title",
            "Efficient Month",
            "Verimli Ay",
            "Mes eficiente",
            "Mois efficace",
            "Effizienter Monat",
            "효율적인 한 달",
            "Эффективный месяц");

        Add(
            list,
            "event.card.event_positive_efficiency.description",
            "Operational efficiency saved more than expected.",
            "Operasyonel verimlilik beklenenden fazla tasarruf sağladı.",
            "La eficiencia operativa ahorró más de lo esperado.",
            "L'efficacité opérationnelle a permis d'économiser plus que prévu.",
            "Betriebliche Effizienz sparte mehr als erwartet.",
            "운영 효율화로 예상보다 많은 비용을 절감했습니다.",
            "Операционная эффективность позволила сэкономить больше ожидаемого.");

        Add(
            list,
            "event.card.event_negative_maintenance.title",
            "Maintenance Cost",
            "Bakım Masrafı",
            "Coste de mantenimiento",
            "Frais d'entretien",
            "Wartungskosten",
            "유지보수 비용",
            "Расходы на обслуживание");

        Add(
            list,
            "event.card.event_negative_maintenance.description",
            "Unexpected maintenance expenses appeared.",
            "Beklenmedik bakım giderleri çıktı.",
            "Aparecieron gastos de mantenimiento inesperados.",
            "Des frais d'entretien imprévus sont apparus.",
            "Unerwartete Wartungskosten sind angefallen.",
            "예상치 못한 유지보수 비용이 발생했습니다.",
            "Возникли непредвиденные расходы на обслуживание.");

        Add(
            list,
            "event.card.event_negative_market_drop.title",
            "Market Drop",
            "Piyasa Düşüşü",
            "Caída del mercado",
            "Baisse du marché",
            "Marktrückgang",
            "시장 하락",
            "Падение рынка");

        Add(
            list,
            "event.card.event_negative_market_drop.description",
            "Market volatility affected your income.",
            "Piyasadaki dalgalanma gelirini etkiledi.",
            "La volatilidad del mercado afectó tus ingresos.",
            "La volatilité du marché a affecté vos revenus.",
            "Marktschwankungen haben deine Einnahmen beeinträchtigt.",
            "시장 변동으로 수입이 줄었습니다.",
            "Колебания рынка повлияли на ваш доход.");

        Add(
            list,
            "event.card.event_negative_invoice.title",
            "Unexpected Invoice",
            "Beklenmedik Fatura",
            "Factura inesperada",
            "Facture imprévue",
            "Unerwartete Rechnung",
            "예상치 못한 청구서",
            "Неожиданный счёт");

        Add(
            list,
            "event.card.event_negative_invoice.description",
            "An unplanned business invoice arrived.",
            "Planlanmayan bir işletme faturası geldi.",
            "Llegó una factura comercial no planificada.",
            "Une facture professionnelle imprévue est arrivée.",
            "Eine ungeplante Geschäftsrechnung ist eingegangen.",
            "예정에 없던 사업 청구서가 도착했습니다.",
            "Поступил незапланированный деловой счёт.");

        Add(
            list,
            "event.card.event_negative_damage.title",
            "Damage Expense",
            "Hasar Gideri",
            "Gasto por daños",
            "Frais de réparation",
            "Schadenskosten",
            "손상 비용",
            "Расходы на ущерб");

        Add(
            list,
            "event.card.event_negative_damage.description",
            "You need to pay for minor damage.",
            "Küçük bir hasar için ödeme yapman gerekiyor.",
            "Debes pagar por unos daños menores.",
            "Vous devez payer pour des dégâts mineurs.",
            "Du musst für einen kleinen Schaden bezahlen.",
            "경미한 손상에 대한 비용을 지불해야 합니다.",
            "Нужно оплатить небольшой ущерб.");

        Add(
            list,
            "event.card.event_negative_supply.title",
            "Supply Issue",
            "Tedarik Sorunu",
            "Problema de suministro",
            "Problème d'approvisionnement",
            "Lieferproblem",
            "공급 문제",
            "Проблема с поставками");

        Add(
            list,
            "event.card.event_negative_supply.description",
            "A supply-chain disruption created extra costs.",
            "Tedarik zincirindeki aksama ek maliyet çıkardı.",
            "Una interrupción de la cadena de suministro generó costes adicionales.",
            "Une perturbation de la chaîne d'approvisionnement a entraîné des coûts supplémentaires.",
            "Eine Störung in der Lieferkette verursachte zusätzliche Kosten.",
            "공급망 차질로 추가 비용이 발생했습니다.",
            "Сбой в цепочке поставок привёл к дополнительным расходам.");

        Add(
            list,
            "event.card.event_negative_penalty.title",
            "Unexpected Penalty",
            "Beklenmedik Ceza",
            "Sanción inesperada",
            "Pénalité imprévue",
            "Unerwartete Strafe",
            "예상치 못한 벌금",
            "Неожиданный штраф");

        Add(
            list,
            "event.card.event_negative_penalty.description",
            "A violation requires a large payment.",
            "Bir ihlal nedeniyle yüksek bir ödeme yapman gerekiyor.",
            "Una infracción exige un pago importante.",
            "Une infraction vous oblige à effectuer un paiement important.",
            "Ein Verstoß erfordert eine hohe Zahlung.",
            "위반 사항으로 큰 금액을 지불해야 합니다.",
            "Из-за нарушения требуется крупная выплата.");

        Add(
            list,
            "event.card.event_negative_late_fee.title",
            "Late Fee",
            "Gecikme Bedeli",
            "Cargo por demora",
            "Frais de retard",
            "Verspätungsgebühr",
            "연체료",
            "Плата за просрочку");

        Add(
            list,
            "event.card.event_negative_late_fee.description",
            "A delayed payment generated an extra fee.",
            "Geciken bir ödeme için ek ücret çıktı.",
            "Un pago atrasado generó un cargo adicional.",
            "Un paiement en retard a entraîné des frais supplémentaires.",
            "Eine verspätete Zahlung verursachte eine zusätzliche Gebühr.",
            "지연된 결제로 추가 수수료가 발생했습니다.",
            "Просроченный платёж привёл к дополнительной комиссии.");

        Add(
            list,
            "event.card.event_negative_equipment.title",
            "Equipment Repair",
            "Ekipman Tamiri",
            "Reparación de equipo",
            "Réparation d'équipement",
            "Gerätereparatur",
            "장비 수리",
            "Ремонт оборудования");

        Add(
            list,
            "event.card.event_negative_equipment.description",
            "A piece of equipment needs an urgent repair.",
            "Bir ekipmanın acil tamire ihtiyacı var.",
            "Un equipo necesita una reparación urgente.",
            "Un équipement nécessite une réparation urgente.",
            "Ein Gerät muss dringend repariert werden.",
            "장비 하나를 긴급히 수리해야 합니다.",
            "Оборудованию требуется срочный ремонт.");

        Add(
            list,
            "event.card.event_negative_delivery.title",
            "Delivery Issue",
            "Teslimat Sorunu",
            "Problema de entrega",
            "Problème de livraison",
            "Lieferproblem",
            "배송 문제",
            "Проблема с доставкой");

        Add(
            list,
            "event.card.event_negative_delivery.description",
            "A delivery disruption created extra costs.",
            "Teslimattaki aksama ek maliyet oluşturdu.",
            "Un problema de entrega generó costes adicionales.",
            "Un problème de livraison a entraîné des coûts supplémentaires.",
            "Eine Lieferstörung verursachte zusätzliche Kosten.",
            "배송 차질로 추가 비용이 발생했습니다.",
            "Сбой доставки привёл к дополнительным расходам.");

        Add(
            list,
            "event.card.event_negative_permit.title",
            "Permit Cost",
            "İzin Masrafı",
            "Coste de permiso",
            "Frais d'autorisation",
            "Genehmigungsgebühr",
            "허가 비용",
            "Расходы на разрешение");

        Add(
            list,
            "event.card.event_negative_permit.description",
            "An unexpected permit and processing fee appeared.",
            "Beklenmedik bir izin ve işlem ücreti çıktı.",
            "Apareció una tasa inesperada de permiso y gestión.",
            "Des frais imprévus d'autorisation et de traitement sont apparus.",
            "Eine unerwartete Genehmigungs- und Bearbeitungsgebühr ist angefallen.",
            "예상치 못한 허가 및 처리 수수료가 발생했습니다.",
            "Возникли непредвиденные сборы за разрешение и оформление.");

        Add(
            list,
            "event.card.event_negative_refunds.title",
            "Refund Wave",
            "İade Dalgası",
            "Oleada de reembolsos",
            "Vague de remboursements",
            "Rückerstattungswelle",
            "환불 증가",
            "Волна возвратов");

        Add(
            list,
            "event.card.event_negative_refunds.description",
            "Several refunds had to be paid at the same time.",
            "Aynı anda birkaç geri ödeme yapmak zorunda kaldın.",
            "Tuviste que realizar varios reembolsos al mismo tiempo.",
            "Vous avez dû effectuer plusieurs remboursements en même temps.",
            "Du musstest mehrere Rückerstattungen gleichzeitig zahlen.",
            "여러 건의 환불을 한꺼번에 처리해야 했습니다.",
            "Пришлось одновременно оформить несколько возвратов.");

        Add(
            list,
            "event.card.event_negative_utility.title",
            "High Utility Bill",
            "Yüksek Fatura",
            "Factura elevada",
            "Facture élevée",
            "Hohe Betriebskosten",
            "높은 공과금",
            "Высокий счёт");

        Add(
            list,
            "event.card.event_negative_utility.description",
            "Energy and service expenses increased this month.",
            "Enerji ve hizmet giderleri bu ay yükseldi.",
            "Los gastos de energía y servicios aumentaron este mes.",
            "Les dépenses d'énergie et de services ont augmenté ce mois-ci.",
            "Energie- und Betriebskosten sind diesen Monat gestiegen.",
            "이번 달 에너지와 서비스 비용이 증가했습니다.",
            "В этом месяце выросли расходы на энергию и услуги.");

        Add(
            list,
            "event.card.event_special_rest_day.title",
            "Mandatory Break",
            "Zorunlu Mola",
            "Pausa obligatoria",
            "Pause obligatoire",
            "Zwangspause",
            "강제 휴식",
            "Вынужденный перерыв");

        Add(
            list,
            "event.card.event_special_rest_day.description",
            "You need to take an unplanned break.",
            "Plan dışı bir mola vermen gerekiyor.",
            "Debes hacer una pausa no planificada.",
            "Vous devez prendre une pause imprévue.",
            "Du musst eine ungeplante Pause einlegen.",
            "예정에 없던 휴식을 취해야 합니다.",
            "Вам придётся сделать незапланированный перерыв.");

        Add(
            list,
            "event.card.event_special_move_3.title",
            "Fast Progress",
            "Hızlı İlerleme",
            "Avance rápido",
            "Progression rapide",
            "Schneller Fortschritt",
            "빠른 전진",
            "Быстрое продвижение");

        Add(
            list,
            "event.card.event_special_move_3.description",
            "Things sped up. Move forward three spaces.",
            "İşler hızlandı. Üç kare ileri git.",
            "Todo se aceleró. Avanza tres casillas.",
            "Les choses s'accélèrent. Avancez de trois cases.",
            "Es geht schneller voran. Gehe drei Felder vor.",
            "일이 빨라졌습니다. 세 칸 전진하세요.",
            "Дела ускорились. Продвиньтесь на три клетки.");

        Add(
            list,
            "event.card.event_special_move_5.title",
            "Big Opportunity",
            "Büyük Fırsat",
            "Gran oportunidad",
            "Grande opportunité",
            "Große Chance",
            "큰 기회",
            "Большая возможность");

        Add(
            list,
            "event.card.event_special_move_5.description",
            "A new opportunity moved you ahead. Move forward five spaces.",
            "Yeni bir fırsat seni öne taşıdı. Beş kare ileri git.",
            "Una nueva oportunidad te impulsa. Avanza cinco casillas.",
            "Une nouvelle opportunité vous fait avancer. Avancez de cinq cases.",
            "Eine neue Chance bringt dich voran. Gehe fünf Felder vor.",
            "새로운 기회가 찾아왔습니다. 다섯 칸 전진하세요.",
            "Новая возможность продвигает вас вперёд. Пройдите пять клеток.");

        Add(
            list,
            "event.card.event_special_to_bonus.title",
            "Bonus Route",
            "Bonus Rotası",
            "Ruta de bonificación",
            "Route bonus",
            "Bonusroute",
            "보너스 경로",
            "Путь к бонусу");

        Add(
            list,
            "event.card.event_special_to_bonus.description",
            "Move toward the nearest Bonus space.",
            "En yakın Bonus karesine doğru ilerle.",
            "Avanza hacia la casilla de bonificación más cercana.",
            "Avancez vers la case Bonus la plus proche.",
            "Gehe zum nächsten Bonusfeld.",
            "가장 가까운 보너스 칸으로 이동하세요.",
            "Переместитесь к ближайшей бонусной клетке.");

        Add(
            list,
            "event.card.event_special_to_travel.title",
            "New Route",
            "Yeni Rota",
            "Nueva ruta",
            "Nouvelle route",
            "Neue Route",
            "새 경로",
            "Новый маршрут");

        Add(
            list,
            "event.card.event_special_to_travel.description",
            "Move toward the nearest Travel space.",
            "En yakın Seyahat karesine doğru ilerle.",
            "Avanza hacia la casilla de viaje más cercana.",
            "Avancez vers la case Voyage la plus proche.",
            "Gehe zum nächsten Reisefeld.",
            "가장 가까운 여행 칸으로 이동하세요.",
            "Переместитесь к ближайшей клетке путешествия.");

        Add(
            list,
            "event.card.event_special_to_auction.title",
            "Market Opportunity",
            "Pazar Fırsatı",
            "Oportunidad de mercado",
            "Opportunité de marché",
            "Marktchance",
            "시장 기회",
            "Рыночная возможность");

        Add(
            list,
            "event.card.event_special_to_auction.description",
            "Move toward the nearest Auction space.",
            "En yakın Açık Artırma karesine doğru ilerle.",
            "Avanza hacia la casilla de subasta más cercana.",
            "Avancez vers la case Enchère la plus proche.",
            "Gehe zum nächsten Auktionsfeld.",
            "가장 가까운 경매 칸으로 이동하세요.",
            "Переместитесь к ближайшей клетке аукциона.");

        Add(
            list,
            "event.card.event_special_move_2.title",
            "Small Advantage",
            "Küçük Avantaj",
            "Pequeña ventaja",
            "Petit avantage",
            "Kleiner Vorteil",
            "작은 이점",
            "Небольшое преимущество");

        Add(
            list,
            "event.card.event_special_move_2.description",
            "Move forward two spaces.",
            "İki kare ileri git.",
            "Avanza dos casillas.",
            "Avancez de deux cases.",
            "Gehe zwei Felder vor.",
            "두 칸 전진하세요.",
            "Продвиньтесь на две клетки.");

        Add(
            list,
            "event.card.event_special_move_4.title",
            "Gained Momentum",
            "Hız Kazandın",
            "Has ganado impulso",
            "Vous prenez de l'élan",
            "Du nimmst Fahrt auf",
            "속도 상승",
            "Набранный темп");

        Add(
            list,
            "event.card.event_special_move_4.description",
            "Move forward four spaces.",
            "Dört kare ileri git.",
            "Avanza cuatro casillas.",
            "Avancez de quatre cases.",
            "Gehe vier Felder vor.",
            "네 칸 전진하세요.",
            "Продвиньтесь на четыре клетки.");

        Add(
            list,
            "event.card.event_special_move_6.title",
            "Big Leap",
            "Büyük Sıçrama",
            "Gran salto",
            "Grand bond",
            "Großer Sprung",
            "큰 도약",
            "Большой рывок");

        Add(
            list,
            "event.card.event_special_move_6.description",
            "Move forward six spaces.",
            "Altı kare ileri git.",
            "Avanza seis casillas.",
            "Avancez de six cases.",
            "Gehe sechs Felder vor.",
            "여섯 칸 전진하세요.",
            "Продвиньтесь на шесть клеток.");

        Add(
            list,
            "event.card.event_special_to_vacation.title",
            "Short Vacation",
            "Kısa Tatil",
            "Vacaciones cortas",
            "Courtes vacances",
            "Kurzurlaub",
            "짧은 휴가",
            "Короткий отпуск");

        Add(
            list,
            "event.card.event_special_to_vacation.description",
            "Move toward the nearest Vacation space.",
            "En yakın Tatil karesine doğru ilerle.",
            "Avanza hacia la casilla de vacaciones más cercana.",
            "Avancez vers la case Vacances la plus proche.",
            "Gehe zum nächsten Urlaubsfeld.",
            "가장 가까운 휴가 칸으로 이동하세요.",
            "Переместитесь к ближайшей клетке отпуска.");

        Add(
            list,
            "event.card.event_special_to_rest.title",
            "Rest Stop",
            "Dinlenme Noktası",
            "Punto de descanso",
            "Aire de repos",
            "Rastplatz",
            "휴식 지점",
            "Место отдыха");

        Add(
            list,
            "event.card.event_special_to_rest.description",
            "Move toward the nearest Rest space.",
            "En yakın Dinlenme karesine doğru ilerle.",
            "Avanza hacia la casilla de descanso más cercana.",
            "Avancez vers la case Repos la plus proche.",
            "Gehe zum nächsten Rastfeld.",
            "가장 가까운 휴식 칸으로 이동하세요.",
            "Переместитесь к ближайшей клетке отдыха.");

        Add(
            list,
            "event.card.event_special_skip_2.title",
            "System Outage",
            "Sistem Kesintisi",
            "Caída del sistema",
            "Panne système",
            "Systemausfall",
            "시스템 장애",
            "Сбой системы");

        Add(
            list,
            "event.card.event_special_skip_2.description",
            "An unexpected outage makes you wait for two turns.",
            "Beklenmedik bir kesinti nedeniyle iki tur bekle.",
            "Una caída inesperada te obliga a esperar dos turnos.",
            "Une panne imprévue vous fait attendre deux tours.",
            "Ein unerwarteter Ausfall lässt dich zwei Runden aussetzen.",
            "예상치 못한 장애로 두 턴을 쉬어야 합니다.",
            "Из-за неожиданного сбоя пропустите два хода.");

        Add(
            list,
            "event.applying",
            "Applying card...",
            "Kart uygulanıyor...",
            "Aplicando carta...",
            "Application de la carte...",
            "Karte wird angewendet...",
            "카드 적용 중...",
            "Применение карты...");

        Add(
            list,
            "event.effect_none",
            "No effect was applied.",
            "Etki uygulanmadı.",
            "No se aplicó ningún efecto.",
            "Aucun effet n'a été appliqué.",
            "Es wurde kein Effekt angewendet.",
            "효과가 적용되지 않았습니다.",
            "Эффект не применён.");

        Add(
            list,
            "event.bankrupt_result",
            "BANKRUPT\nPaid: {0} ₵\nTransferred/released properties: {1}",
            "İFLAS\nÖdenen: {0} ₵\nDevredilen/boşa çıkan mülk: {1}",
            "BANCARROTA\nPagado: {0} ₵\nPropiedades transferidas/liberadas: {1}",
            "FAILLITE\nPayé : {0} ₵\nPropriétés transférées/libérées : {1}",
            "INSOLVENT\nBezahlt: {0} ₵\nÜbertragene/freigegebene Grundstücke: {1}",
            "파산\n지불: {0} ₵\n이전/해제된 부동산: {1}",
            "БАНКРОТ\nОплачено: {0} ₵\nПередано/освобождено объектов: {1}");

        Add(
            list,
            "event.money_no_change",
            "No money change",
            "Para değişmedi",
            "Sin cambios de dinero",
            "Aucun changement d'argent",
            "Keine Geldänderung",
            "금액 변동 없음",
            "Деньги не изменились");

        Add(
            list,
            "event.skip_one",
            "You will skip your next turn.",
            "Sonraki turunu atlayacaksın.",
            "Perderás tu próximo turno.",
            "Vous passerez votre prochain tour.",
            "Du setzt deinen nächsten Zug aus.",
            "다음 턴을 쉽니다.",
            "Вы пропустите следующий ход.");

        Add(
            list,
            "event.skip_many",
            "You will skip your next {0} turns.",
            "Sonraki {0} turunu atlayacaksın.",
            "Perderás tus próximos {0} turnos.",
            "Vous passerez vos {0} prochains tours.",
            "Du setzt deine nächsten {0} Züge aus.",
            "다음 {0}턴을 쉽니다.",
            "Вы пропустите следующие {0} хода.");

        Add(
            list,
            "event.move_failed",
            "Movement could not be applied.",
            "Hareket uygulanamadı.",
            "No se pudo realizar el movimiento.",
            "Le déplacement n'a pas pu être effectué.",
            "Bewegung konnte nicht ausgeführt werden.",
            "이동할 수 없습니다.",
            "Не удалось выполнить перемещение.");

        Add(
            list,
            "event.move_progress",
            "Moving forward {0} spaces{1}",
            "{0} kare ilerliyorsun{1}",
            "Avanzando {0} casillas{1}",
            "Avance de {0} cases{1}",
            "Du gehst {0} Felder vor{1}",
            "{0}칸 전진 중{1}",
            "Продвижение на {0} клеток{1}");

        Add(
            list,
            "event.move_target_suffix",
            ": {0}",
            ": {0}",
            ": {0}",
            ": {0}",
            ": {0}",
            ": {0}",
            ": {0}");

        Add(
            list,
            "event.move_done_tile",
            "Moved forward {0} spaces: {1}",
            "{0} kare ilerledin: {1}",
            "Avanzaste {0} casillas: {1}",
            "Vous avez avancé de {0} cases : {1}",
            "Du bist {0} Felder vorgerückt: {1}",
            "{0}칸 전진: {1}",
            "Вы продвинулись на {0} клеток: {1}");

        Add(
            list,
            "event.move_done",
            "Moved forward {0} spaces.",
            "{0} kare ilerledin.",
            "Avanzaste {0} casillas.",
            "Vous avez avancé de {0} cases.",
            "Du bist {0} Felder vorgerückt.",
            "{0}칸 전진했습니다.",
            "Вы продвинулись на {0} клеток.");

        Add(
            list,
            "event.target_not_found",
            "No suitable target was found.",
            "Uygun hedef bulunamadı.",
            "No se encontró un destino adecuado.",
            "Aucune destination appropriée n'a été trouvée.",
            "Kein geeignetes Ziel gefunden.",
            "적절한 목적지를 찾을 수 없습니다.",
            "Подходящая цель не найдена.");

        Add(
            list,
            "event.target_moving_tile",
            "Moving to {0}.",
            "{0} konumuna ilerliyorsun.",
            "Avanzando hacia {0}.",
            "Déplacement vers {0}.",
            "Du bewegst dich zu {0}.",
            "{0}(으)로 이동 중입니다.",
            "Перемещение к {0}.");

        Add(
            list,
            "event.target_moving",
            "Moving to the target space.",
            "Hedef konuma ilerliyorsun.",
            "Avanzando hacia la casilla objetivo.",
            "Déplacement vers la case cible.",
            "Du bewegst dich zum Zielfeld.",
            "목표 칸으로 이동 중입니다.",
            "Перемещение к целевой клетке.");

        Add(
            list,
            "event.target_done_tile",
            "Moved to {0}.",
            "{0} konumuna ilerledin.",
            "Te moviste a {0}.",
            "Vous avez rejoint {0}.",
            "Du bist zu {0} gezogen.",
            "{0}(으)로 이동했습니다.",
            "Вы переместились к {0}.");

        Add(
            list,
            "event.target_done",
            "Moved to the target space.",
            "Hedef konuma ilerledin.",
            "Te moviste a la casilla objetivo.",
            "Vous avez rejoint la case cible.",
            "Du bist zum Zielfeld gezogen.",
            "목표 칸으로 이동했습니다.",
            "Вы переместились к целевой клетке.");

        Add(
            list,
            "event.deck.default",
            "Event Deck",
            "Etkinlik Destesi",
            "Mazo de eventos",
            "Paquet d'événements",
            "Ereignisstapel",
            "이벤트 덱",
            "Колода событий");
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
