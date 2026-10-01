#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AtlasBoardStoreLocalizationSeed
{
    private const string DatabasePath =
        "Assets/Project/Data/Localization/Localization_Default.asset";

    static AtlasBoardStoreLocalizationSeed()
    {
        EditorApplication.delayCall += MergeNow;
    }

    [MenuItem("Atlas Board/Meta & Progression/Meta/Refresh Store Localization v2")]
    public static void MergeNow()
    {
        AtlasBoardLocalizationDatabase database =
            AssetDatabase.LoadAssetAtPath<AtlasBoardLocalizationDatabase>(DatabasePath);

        if (database == null)
        {
            Debug.LogError(
                "AtlasBoard Store Localization v2.4: localization database not found at " +
                DatabasePath);
            return;
        }

        List<AtlasBoardLocalizationDatabase.Entry> list =
            database.Entries
                .Where(entry =>
                    entry != null &&
                    !string.IsNullOrWhiteSpace(entry.key) &&
                    !entry.key.StartsWith("store.") &&
                    !entry.key.StartsWith("seasonal.") &&
                    entry.key != "menu.profile_body")
                .Select(Clone)
                .ToList();

        Append(list);
        database.EditorReplaceEntries(list);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        int storeEntryCount =
            list.Count(entry =>
                entry != null &&
                entry.key != null &&
                entry.key.StartsWith("store."));
        int seasonalEntryCount =
            list.Count(entry =>
                entry != null &&
                entry.key != null &&
                entry.key.StartsWith("seasonal."));

        Debug.Log(
            $"AtlasBoard Store Localization v2.4 refreshed. " +
            $"Store entries={storeEntryCount}; " +
            $"seasonal entries={seasonalEntryCount}. " +
            $"Languages=EN/TR/ES/FR/DE/KO/RU. Database={DatabasePath}");
    }

    public static void Append(List<AtlasBoardLocalizationDatabase.Entry> list)
    {
        if (list == null)
        {
            return;
        }

        Add(list, "menu.profile_body",
            "{0}\nGold: {1:N0}\nAtlas Coin: {2:N0}",
            "{0}\nAltın: {1:N0}\nAtlas Coin: {2:N0}",
            "{0}\nOro: {1:N0}\nAtlas Coin: {2:N0}",
            "{0}\nOr : {1:N0}\nAtlas Coin : {2:N0}",
            "{0}\nGold: {1:N0}\nAtlas Coin: {2:N0}",
            "{0}\n골드: {1:N0}\n아틀라스 코인: {2:N0}",
            "{0}\nЗолото: {1:N0}\nAtlas Coin: {2:N0}");

        Add(list, "store.title",
            "ATLAS STORE",
            "ATLAS MAĞAZA",
            "TIENDA ATLAS",
            "BOUTIQUE ATLAS",
            "ATLAS SHOP",
            "아틀라스 상점",
            "МАГАЗИН ATLAS");

        Add(list, "store.tab.home",
            "HOME",
            "ANA SAYFA",
            "INICIO",
            "ACCUEIL",
            "START",
            "홈",
            "ГЛАВНАЯ");

        Add(list, "store.tab.items",
            "ITEMS",
            "ÜRÜNLER",
            "OBJETOS",
            "OBJETS",
            "ARTIKEL",
            "아이템",
            "ПРЕДМЕТЫ");

        Add(list, "store.tab.redeem",
            "REDEEM CODE",
            "KOD KULLAN",
            "CANJEAR CÓDIGO",
            "UTILISER UN CODE",
            "CODE EINLÖSEN",
            "코드 사용",
            "АКТИВИРОВАТЬ КОД");

        Add(list, "store.tab.history",
            "HISTORY",
            "GEÇMİŞ",
            "HISTORIAL",
            "HISTORIQUE",
            "VERLAUF",
            "내역",
            "ИСТОРИЯ");

        Add(list, "store.wallet.gold",
            "Gold",
            "Altın",
            "Oro",
            "Or",
            "Gold",
            "골드",
            "Золото");

        Add(list, "store.wallet.coin",
            "Atlas Coin",
            "Atlas Coin",
            "Atlas Coin",
            "Atlas Coin",
            "Atlas Coin",
            "아틀라스 코인",
            "Atlas Coin");

        Add(list, "store.common.refresh",
            "REFRESH",
            "YENİLE",
            "ACTUALIZAR",
            "ACTUALISER",
            "AKTUALISIEREN",
            "새로고침",
            "ОБНОВИТЬ");

        Add(list, "store.common.cancel",
            "CANCEL",
            "İPTAL",
            "CANCELAR",
            "ANNULER",
            "ABBRECHEN",
            "취소",
            "ОТМЕНА");

        Add(list, "store.response.ready",
            "Ready.",
            "Hazır.",
            "Listo.",
            "Prêt.",
            "Bereit.",
            "준비됨.",
            "Готово.");

        Add(list, "store.response.loading",
            "Loading store data...",
            "Mağaza verileri yükleniyor...",
            "Cargando datos de la tienda...",
            "Chargement des données de la boutique...",
            "Shop-Daten werden geladen...",
            "상점 데이터를 불러오는 중...",
            "Загрузка данных магазина...");

        Add(list, "store.response.processing",
            "Transaction is being processed...",
            "İşlem işleniyor...",
            "La transacción se está procesando...",
            "La transaction est en cours...",
            "Transaktion wird verarbeitet...",
            "거래 처리 중...",
            "Транзакция обрабатывается...");

        Add(list, "store.response.wait",
            "Wait for the current transaction to finish.",
            "Mevcut işlemin tamamlanmasını bekleyin.",
            "Espera a que termine la transacción actual.",
            "Attendez la fin de la transaction en cours.",
            "Warte, bis die aktuelle Transaktion abgeschlossen ist.",
            "현재 거래가 끝날 때까지 기다려 주세요.",
            "Дождитесь завершения текущей транзакции.");

        Add(list, "store.response.cooldown",
            "Please wait briefly before another transaction.",
            "Yeni bir işlem yapmadan önce kısa bir süre bekleyin.",
            "Espera un momento antes de otra transacción.",
            "Veuillez patienter avant une autre transaction.",
            "Bitte kurz vor der nächsten Transaktion warten.",
            "다음 거래 전에 잠시 기다려 주세요.",
            "Немного подождите перед следующей транзакцией.");

        Add(list, "store.response.dev_wallet_done",
            "Development wallet is ready.",
            "Geliştirme bakiyesi hazır.",
            "La cartera de desarrollo está lista.",
            "Le portefeuille de développement est prêt.",
            "Entwicklungs-Wallet ist bereit.",
            "개발 지갑이 준비되었습니다.",
            "Кошелёк разработки готов.");

        Add(list, "store.response.dev_reset_done",
            "Development purchase ownership reset: {0} item(s). Purchase history and wallet evidence were preserved.",
            "Geliştirme satın alım sahipliği sıfırlandı: {0} ürün. Satın alma geçmişi ve cüzdan kanıtları korundu.",
            "Se restableció la propiedad de {0} compra(s) de desarrollo. Se conservaron el historial y la evidencia del monedero.",
            "La propriété de {0} achat(s) de développement a été réinitialisée. L’historique et les preuves du portefeuille sont conservés.",
            "{0} Entwicklungs-Kaufberechtigung(en) wurden zurückgesetzt. Verlauf und Wallet-Nachweise bleiben erhalten.",
            "개발 구매 소유권 {0}개를 초기화했습니다. 구매 내역과 지갑 기록은 유지됩니다.",
            "Сброшено тестовых покупок: {0}. История и записи кошелька сохранены.");

        Add(list, "store.response.daily_already",
            "Today’s development daily reward was already claimed.",
            "Bugünün geliştirme günlük ödülü zaten alındı.",
            "La recompensa diaria de desarrollo de hoy ya fue reclamada.",
            "La récompense quotidienne de développement a déjà été réclamée aujourd’hui.",
            "Die heutige Entwicklungs-Tagesbelohnung wurde bereits abgeholt.",
            "오늘의 개발 일일 보상은 이미 받았습니다.",
            "Сегодняшняя тестовая ежедневная награда уже получена.");

        Add(list, "store.response.daily_claimed",
            "Daily reward claimed: +250 Gold.",
            "Günlük ödül alındı: +250 Altın.",
            "Recompensa diaria obtenida: +250 Oro.",
            "Récompense quotidienne obtenue : +250 Or.",
            "Tagesbelohnung erhalten: +250 Gold.",
            "일일 보상 획득: +250 골드.",
            "Ежедневная награда получена: +250 золота.");

        Add(list, "store.response.purchase_done",
            "Purchase completed and entitlement granted.",
            "Satın alma tamamlandı ve ürün hesabınıza tanımlandı.",
            "Compra completada y derecho concedido.",
            "Achat terminé et droit accordé.",
            "Kauf abgeschlossen und Berechtigung erteilt.",
            "구매 완료 및 권한 지급됨.",
            "Покупка завершена, право на предмет выдано.");

        Add(list, "store.response.redeem_done",
            "Code redeemed. Rewards: {0}",
            "Kod kullanıldı. Ödüller: {0}",
            "Código canjeado. Recompensas: {0}",
            "Code utilisé. Récompenses : {0}",
            "Code eingelöst. Belohnungen: {0}",
            "코드 사용 완료. 보상: {0}",
            "Код активирован. Награды: {0}");

        Add(list, "store.popup.info_title",
            "PLEASE WAIT",
            "LÜTFEN BEKLEYİN",
            "ESPERE",
            "VEUILLEZ PATIENTER",
            "BITTE WARTEN",
            "잠시 기다려 주세요",
            "ПОЖАЛУЙСТА, ПОДОЖДИТЕ");

        Add(list, "store.popup.error_title",
            "TRANSACTION FAILED",
            "İŞLEM BAŞARISIZ",
            "TRANSACCIÓN FALLIDA",
            "ÉCHEC DE LA TRANSACTION",
            "TRANSAKTION FEHLGESCHLAGEN",
            "거래 실패",
            "ОПЕРАЦИЯ НЕ ВЫПОЛНЕНА");

        Add(list, "store.popup.daily_title",
            "DAILY REWARD",
            "GÜNLÜK ÖDÜL",
            "RECOMPENSA DIARIA",
            "RÉCOMPENSE QUOTIDIENNE",
            "TAGESBELOHNUNG",
            "일일 보상",
            "ЕЖЕДНЕВНАЯ НАГРАДА");

        Add(list, "store.popup.redeem_title",
            "CODE REDEEMED",
            "KOD KULLANILDI",
            "CÓDIGO CANJEADO",
            "CODE UTILISÉ",
            "CODE EINGELÖST",
            "코드 사용 완료",
            "КОД АКТИВИРОВАН");

        Add(list, "store.popup.dev_title",
            "DEVELOPMENT TOOL",
            "GELİŞTİRME ARACI",
            "HERRAMIENTA DE DESARROLLO",
            "OUTIL DE DÉVELOPPEMENT",
            "ENTWICKLUNGSWERKZEUG",
            "개발 도구",
            "ИНСТРУМЕНТ РАЗРАБОТКИ");

        Add(list, "store.home.title",
            "STORE HOME",
            "MAĞAZA ANA SAYFA",
            "INICIO DE LA TIENDA",
            "ACCUEIL DE LA BOUTIQUE",
            "SHOP-STARTSEITE",
            "상점 홈",
            "ГЛАВНАЯ МАГАЗИНА");

        Add(list, "store.home.daily_title",
            "DAILY REWARD",
            "GÜNLÜK ÖDÜL",
            "RECOMPENSA DIARIA",
            "RÉCOMPENSE QUOTIDIENNE",
            "TAGESBELOHNUNG",
            "일일 보상",
            "ЕЖЕДНЕВНАЯ НАГРАДА");

        Add(list, "store.home.daily_body",
            "Claim a daily development reward here. Production rewards will later use a dedicated server claim.",
            "Günlük geliştirme ödülünü buradan al. Üretim ödülleri ileride özel sunucu claim sistemi kullanacak.",
            "Reclama aquí una recompensa diaria de desarrollo. En producción se usará una reclamación dedicada del servidor.",
            "Récupérez ici une récompense quotidienne de développement. La production utilisera ensuite une réclamation serveur dédiée.",
            "Hole hier eine tägliche Entwicklungsbelohnung ab. Später nutzt die Produktion einen eigenen Server-Claim.",
            "여기서 개발용 일일 보상을 받습니다. 정식 버전은 전용 서버 수령 방식을 사용합니다.",
            "Получайте здесь тестовую ежедневную награду. В релизе будет отдельный серверный механизм.");

        Add(list, "store.home.daily_claim",
            "CLAIM DAILY",
            "GÜNLÜK ÖDÜLÜ AL",
            "RECLAMAR",
            "RÉCUPÉRER",
            "ABHOLEN",
            "받기",
            "ПОЛУЧИТЬ");

        Add(list, "store.home.featured_title",
            "ITEM CATALOG",
            "ÜRÜN KATALOĞU",
            "CATÁLOGO",
            "CATALOGUE",
            "ARTIKELKATALOG",
            "아이템 카탈로그",
            "КАТАЛОГ");

        Add(list, "store.home.featured_body",
            "Browse scalable categories and buy test cosmetics with Gold or Atlas Coin.",
            "Kategorilere göz at ve test kozmetiklerini Altın veya Atlas Coin ile satın al.",
            "Explora categorías y compra cosméticos de prueba con Oro o Atlas Coin.",
            "Parcourez les catégories et achetez des cosmétiques de test avec de l’Or ou des Atlas Coins.",
            "Kategorien durchsuchen und Test-Kosmetik mit Gold oder Atlas Coin kaufen.",
            "카테고리를 보고 골드 또는 아틀라스 코인으로 테스트 꾸미기 아이템을 구매합니다.",
            "Просматривайте категории и покупайте тестовую косметику за золото или Atlas Coin.");

        Add(list, "store.home.browse",
            "BROWSE ITEMS",
            "ÜRÜNLERE GÖZ AT",
            "VER OBJETOS",
            "VOIR LES OBJETS",
            "ARTIKEL ANSEHEN",
            "아이템 보기",
            "СМОТРЕТЬ ПРЕДМЕТЫ");

        Add(list, "store.home.redeem_title",
            "REDEEM CODE",
            "KOD KULLAN",
            "CANJEAR CÓDIGO",
            "UTILISER UN CODE",
            "CODE EINLÖSEN",
            "코드 사용",
            "АКТИВИРОВАТЬ КОД");

        Add(list, "store.home.redeem_body",
            "Enter a promo/redeem code and review its server-recorded rewards and history.",
            "Promosyon/redeem kodu gir ve sunucuda kaydedilen ödülleri ile geçmişini görüntüle.",
            "Introduce un código y revisa sus recompensas e historial registrados por el servidor.",
            "Saisissez un code et consultez ses récompenses et son historique enregistrés par le serveur.",
            "Code eingeben und serverseitig erfasste Belohnungen und Verlauf prüfen.",
            "코드를 입력하고 서버에 기록된 보상과 내역을 확인합니다.",
            "Введите код и просмотрите награды и историю, записанные сервером.");

        Add(list, "store.home.redeem_open",
            "OPEN REDEEM",
            "KOD EKRANINI AÇ",
            "ABRIR CANJE",
            "OUVRIR",
            "CODE ÖFFNEN",
            "코드 화면 열기",
            "ОТКРЫТЬ");

        Add(list, "store.home.dev_title",
            "DEVELOPMENT TOOLS",
            "GELİŞTİRME ARAÇLARI",
            "HERRAMIENTAS DEV",
            "OUTILS DEV",
            "ENTWICKLUNGSTOOLS",
            "개발 도구",
            "ИНСТРУМЕНТЫ DEV");

        Add(list, "store.home.dev_body",
            "Local emulator only. Adds test currency for purchase validation; remove/lock before production.",
            "Sadece local emulator. Satın alma testleri için deneme bakiyesi ekler; production öncesi kaldırılacak/kilitlenecek.",
            "Solo emulador local. Añade saldo de prueba; debe bloquearse antes de producción.",
            "Émulateur local uniquement. Ajoute un solde de test ; à verrouiller avant la production.",
            "Nur lokaler Emulator. Fügt Testguthaben hinzu; vor Produktion sperren.",
            "로컬 에뮬레이터 전용. 구매 테스트용 재화를 추가하며 출시 전 차단합니다.",
            "Только локальный эмулятор. Добавляет тестовую валюту; перед релизом будет заблокировано.");

        Add(list, "store.home.dev_reset",
            "RESET PURCHASES",
            "SATIN ALIMLARI SIFIRLA",
            "RESTABLECER COMPRAS",
            "RÉINITIALISER ACHATS",
            "KÄUFE ZURÜCKSETZEN",
            "구매 초기화",
            "СБРОСИТЬ ПОКУПКИ");

        Add(list, "store.home.dev_wallet",
            "DEV WALLET",
            "DEV BAKİYE",
            "SALDO DEV",
            "SOLDE DEV",
            "DEV-WALLET",
            "DEV 지갑",
            "DEV БАЛАНС");

        Add(list, "store.items.title",
            "ITEMS & COSMETICS",
            "ÜRÜNLER VE KOZMETİKLER",
            "OBJETOS Y COSMÉTICOS",
            "OBJETS ET COSMÉTIQUES",
            "ARTIKEL & KOSMETIK",
            "아이템 & 꾸미기",
            "ПРЕДМЕТЫ И КОСМЕТИКА");

        Add(list, "store.items.empty",
            "No items in this category.",
            "Bu kategoride ürün yok.",
            "No hay objetos en esta categoría.",
            "Aucun objet dans cette catégorie.",
            "Keine Artikel in dieser Kategorie.",
            "이 카테고리에 아이템이 없습니다.",
            "В этой категории нет предметов.");

        Add(list, "store.items.owned",
            "OWNED",
            "SAHİPSİN",
            "EN PROPIEDAD",
            "POSSÉDÉ",
            "BESITZT",
            "보유 중",
            "ПОЛУЧЕНО");

        Add(list, "store.items.buy_gold",
            "BUY • {0:N0} G",
            "SATIN AL • {0:N0} A",
            "COMPRAR • {0:N0} O",
            "ACHETER • {0:N0} O",
            "KAUFEN • {0:N0} G",
            "구매 • {0:N0} G",
            "КУПИТЬ • {0:N0} G");

        Add(list, "store.items.buy_coin",
            "BUY • {0:N0} AC",
            "SATIN AL • {0:N0} AC",
            "COMPRAR • {0:N0} AC",
            "ACHETER • {0:N0} AC",
            "KAUFEN • {0:N0} AC",
            "구매 • {0:N0} AC",
            "КУПИТЬ • {0:N0} AC");

        Add(list, "store.category.all",
            "ALL",
            "TÜMÜ",
            "TODO",
            "TOUT",
            "ALLE",
            "전체",
            "ВСЕ");

        Add(list, "store.category.pawns",
            "PAWNS",
            "PİYONLAR",
            "PEONES",
            "PIONS",
            "SPIELFIGUREN",
            "말",
            "ФИШКИ");

        Add(list, "store.category.dice",
            "DICE",
            "ZARLAR",
            "DADOS",
            "DÉS",
            "WÜRFEL",
            "주사위",
            "КУБИКИ");

        Add(list, "store.category.boards",
            "BOARDS",
            "TAHTALAR",
            "TABLEROS",
            "PLATEAUX",
            "BRETTER",
            "보드",
            "ДОСКИ");

        Add(list, "store.category.profile",
            "PROFILE",
            "PROFİL",
            "PERFIL",
            "PROFIL",
            "PROFIL",
            "프로필",
            "ПРОФИЛЬ");

        Add(list, "store.category.emotes",
            "EMOTES",
            "EMOTELAR",
            "EMOTES",
            "ÉMOTES",
            "EMOTES",
            "이모트",
            "ЭМОЦИИ");

        Add(list, "store.category.motion",
            "MOTION",
            "HAREKET",
            "MOVIMIENTO",
            "MOUVEMENT",
            "BEWEGUNG",
            "모션",
            "АНИМАЦИИ");

        Add(list, "store.category.items",
            "ITEMS",
            "ÜRÜNLER",
            "OBJETOS",
            "OBJETS",
            "ARTIKEL",
            "아이템",
            "ПРЕДМЕТЫ");

        Add(list, "store.purchase.confirm_title",
            "CONFIRM PURCHASE",
            "SATIN ALMAYI ONAYLA",
            "CONFIRMAR COMPRA",
            "CONFIRMER L’ACHAT",
            "KAUF BESTÄTIGEN",
            "구매 확인",
            "ПОДТВЕРДИТЬ ПОКУПКУ");

        Add(list, "store.purchase.confirm_body",
            "The server verifies catalog price and entitlement. Wait for the response; repeated clicks are blocked and the operation uses an idempotency key.",
            "Sunucu katalog fiyatını ve ürün hakkını doğrular. Yanıtı bekleyin; tekrar tıklamalar engellenir ve işlem idempotency anahtarı kullanır.",
            "El servidor verifica precio y derecho. Espera la respuesta; se bloquean clics repetidos y se usa una clave de idempotencia.",
            "Le serveur vérifie le prix et le droit. Attendez la réponse ; les clics répétés sont bloqués et une clé d’idempotence est utilisée.",
            "Der Server prüft Preis und Berechtigung. Auf Antwort warten; Mehrfachklicks werden blockiert und ein Idempotenzschlüssel wird verwendet.",
            "서버가 가격과 권한을 확인합니다. 응답을 기다리며 중복 클릭은 차단되고 멱등성 키를 사용합니다.",
            "Сервер проверяет цену и право. Дождитесь ответа; повторные нажатия блокируются, используется ключ идемпотентности.");

        Add(list, "store.purchase.buy",
            "BUY",
            "SATIN AL",
            "COMPRAR",
            "ACHETER",
            "KAUFEN",
            "구매",
            "КУПИТЬ");

        Add(list, "store.purchase.success_title",
            "PURCHASE COMPLETE",
            "SATIN ALMA TAMAMLANDI",
            "COMPRA COMPLETADA",
            "ACHAT TERMINÉ",
            "KAUF ABGESCHLOSSEN",
            "구매 완료",
            "ПОКУПКА ЗАВЕРШЕНА");

        Add(list, "store.purchase.success_body",
            "{0} was purchased successfully for {1:N0} {2}.",
            "{0}, {1:N0} {2} karşılığında başarıyla satın alındı.",
            "{0} se compró correctamente por {1:N0} {2}.",
            "{0} a été acheté avec succès pour {1:N0} {2}.",
            "{0} wurde erfolgreich für {1:N0} {2} gekauft.",
            "{0}을(를) {1:N0} {2}에 성공적으로 구매했습니다.",
            "{0} успешно приобретён за {1:N0} {2}.");

        Add(list, "store.purchase.success_balance",
            "Remaining balance: {0:N0} {1}",
            "Kalan bakiye: {0:N0} {1}",
            "Saldo restante: {0:N0} {1}",
            "Solde restant : {0:N0} {1}",
            "Verbleibendes Guthaben: {0:N0} {1}",
            "남은 잔액: {0:N0} {1}",
            "Остаток: {0:N0} {1}");

        Add(list, "store.purchase.failed_title",
            "PURCHASE FAILED",
            "SATIN ALMA BAŞARISIZ",
            "COMPRA FALLIDA",
            "ÉCHEC DE L'ACHAT",
            "KAUF FEHLGESCHLAGEN",
            "구매 실패",
            "ПОКУПКА НЕ УДАЛАСЬ");

        Add(list, "store.purchase.failed_body",
            "{0} could not be purchased.\nReason: {1}",
            "{0} satın alınamadı.\nNeden: {1}",
            "No se pudo comprar {0}.\nMotivo: {1}",
            "Impossible d'acheter {0}.\nRaison : {1}",
            "{0} konnte nicht gekauft werden.\nGrund: {1}",
            "{0}을(를) 구매하지 못했습니다.\n사유: {1}",
            "Не удалось приобрести {0}.\nПричина: {1}");

        Add(list, "store.purchase.no_charge",
            "No successful charge was completed for this attempt.",
            "Bu denemede başarılı bir ücret kesintisi yapılmadı.",
            "No se completó ningún cargo correcto en este intento.",
            "Aucun débit réussi n'a été effectué pour cette tentative.",
            "Für diesen Versuch wurde keine erfolgreiche Abbuchung durchgeführt.",
            "이번 시도에서는 결제가 완료되지 않았습니다.",
            "При этой попытке успешного списания не произошло.");

        Add(list, "store.purchase.result_ok",
            "OK",
            "TAMAM",
            "ACEPTAR",
            "OK",
            "OK",
            "확인",
            "ОК");

        Add(list, "store.redeem.title",
            "REDEEM CODE",
            "KOD KULLAN",
            "CANJEAR CÓDIGO",
            "UTILISER UN CODE",
            "CODE EINLÖSEN",
            "코드 사용",
            "АКТИВИРОВАТЬ КОД");

        Add(list, "store.redeem.enter_title",
            "ENTER CODE",
            "KODU GİR",
            "INTRODUCIR CÓDIGO",
            "SAISIR LE CODE",
            "CODE EINGEBEN",
            "코드 입력",
            "ВВЕСТИ КОД");

        Add(list, "store.redeem.enter_body",
            "Codes are validated by the server. Rewards are granted atomically and recorded under your authenticated account.",
            "Kodlar sunucu tarafından doğrulanır. Ödüller atomik olarak verilir ve doğrulanmış hesabınız altında kaydedilir.",
            "Los códigos se validan en el servidor. Las recompensas se conceden de forma atómica y se registran en tu cuenta.",
            "Les codes sont validés par le serveur. Les récompenses sont accordées atomiquement et enregistrées sur votre compte.",
            "Codes werden serverseitig geprüft. Belohnungen werden atomar vergeben und dem authentifizierten Konto zugeordnet.",
            "코드는 서버에서 검증되며 보상은 원자적으로 지급되고 인증 계정에 기록됩니다.",
            "Коды проверяются сервером. Награды выдаются атомарно и записываются в ваш аккаунт.");

        Add(list, "store.redeem.placeholder",
            "ENTER CODE",
            "KODU GİR",
            "INTRODUCE EL CÓDIGO",
            "SAISISSEZ LE CODE",
            "CODE EINGEBEN",
            "코드 입력",
            "ВВЕДИТЕ КОД");

        Add(list, "store.redeem.button",
            "REDEEM",
            "KULLAN",
            "CANJEAR",
            "UTILISER",
            "EINLÖSEN",
            "사용",
            "АКТИВИРОВАТЬ");

        Add(list, "store.redeem.audit_note",
            "Do not spam the button. The client waits for a response and the server enforces code limits/idempotency.",
            "Butona spam yapmayın. İstemci yanıt bekler; sunucu kod limitlerini/idempotency kurallarını uygular.",
            "No pulses repetidamente. El cliente espera la respuesta y el servidor aplica límites e idempotencia.",
            "Ne spammez pas le bouton. Le client attend la réponse et le serveur applique les limites et l’idempotence.",
            "Button nicht spammen. Client wartet auf Antwort; Server erzwingt Limits und Idempotenz.",
            "버튼을 반복해서 누르지 마세요. 클라이언트는 응답을 기다리고 서버가 제한과 멱등성을 적용합니다.",
            "Не нажимайте кнопку многократно. Клиент ждёт ответ, сервер применяет лимиты и идемпотентность.");

        Add(list, "store.redeem.history_title",
            "USED CODES / REWARDS",
            "KULLANILAN KODLAR / ÖDÜLLER",
            "CÓDIGOS USADOS / PREMIOS",
            "CODES UTILISÉS / RÉCOMPENSES",
            "VERWENDETE CODES / BELOHNUNGEN",
            "사용 코드 / 보상",
            "ИСПОЛЬЗОВАННЫЕ КОДЫ / НАГРАДЫ");

        Add(list, "store.redeem.history_empty",
            "No redeemed codes yet.",
            "Henüz kullanılan kod yok.",
            "Aún no hay códigos canjeados.",
            "Aucun code utilisé pour le moment.",
            "Noch keine Codes eingelöst.",
            "사용한 코드가 없습니다.",
            "Коды ещё не активировались.");

        Add(list, "store.redeem.reward_generic",
            "Reward",
            "Ödül",
            "Recompensa",
            "Récompense",
            "Belohnung",
            "보상",
            "Награда");

        Add(list, "store.history.title",
            "TRANSACTION HISTORY",
            "İŞLEM GEÇMİŞİ",
            "HISTORIAL DE TRANSACCIONES",
            "HISTORIQUE DES TRANSACTIONS",
            "TRANSAKTIONSVERLAUF",
            "거래 내역",
            "ИСТОРИЯ ТРАНЗАКЦИЙ");

        Add(list, "store.history.audit_note",
            "Purchases, redeem codes and daily rewards are shown in one evidence-oriented timeline using server timestamps and canonical transaction IDs.",
            "Satın almalar, redeem kodları ve günlük ödüller; sunucu zamanları ve kanonik işlem kimlikleriyle tek kanıt odaklı zaman çizelgesinde gösterilir.",
            "Compras, códigos y recompensas diarias se muestran en una sola línea temporal con hora del servidor e ID canónicos.",
            "Achats, codes et récompenses quotidiennes sont réunis dans une chronologie avec horodatage serveur et ID canoniques.",
            "Käufe, Codes und Tagesbelohnungen erscheinen in einer gemeinsamen Zeitleiste mit Serverzeit und kanonischen IDs.",
            "구매, 코드, 일일 보상을 서버 시간과 정규 거래 ID 기반의 단일 내역으로 표시합니다.",
            "Покупки, коды и ежедневные награды показаны в единой истории с серверным временем и каноническими ID.");

        Add(list, "store.history.empty",
            "No transactions yet.",
            "Henüz işlem yok.",
            "Aún no hay transacciones.",
            "Aucune transaction pour le moment.",
            "Noch keine Transaktionen.",
            "거래 내역이 없습니다.",
            "Транзакций пока нет.");

        Add(list, "store.history.type_purchase",
            "PURCHASE",
            "SATIN ALMA",
            "COMPRA",
            "ACHAT",
            "KAUF",
            "구매",
            "ПОКУПКА");

        Add(list, "store.history.type_redeem",
            "REDEEM CODE",
            "KOD",
            "CÓDIGO",
            "CODE",
            "CODE",
            "코드",
            "КОД");

        Add(list, "store.history.type_daily",
            "DAILY REWARD",
            "GÜNLÜK ÖDÜL",
            "RECOMPENSA DIARIA",
            "RÉCOMPENSE QUOTIDIENNE",
            "TAGESBELOHNUNG",
            "일일 보상",
            "ЕЖЕДНЕВНАЯ НАГРАДА");

        Add(list, "store.history.transaction_id",
            "Transaction ID",
            "İşlem Kimliği",
            "ID de transacción",
            "ID de transaction",
            "Transaktions-ID",
            "거래 ID",
            "ID транзакции");

        Add(list, "store.history.time_pending",
            "Server time pending",
            "Sunucu zamanı bekleniyor",
            "Hora del servidor pendiente",
            "Heure serveur en attente",
            "Serverzeit ausstehend",
            "서버 시간 대기 중",
            "Ожидание времени сервера");

        Add(list, "store.error.insufficient",
            "Insufficient balance.",
            "Yetersiz bakiye.",
            "Saldo insuficiente.",
            "Solde insuffisant.",
            "Unzureichendes Guthaben.",
            "잔액이 부족합니다.",
            "Недостаточно средств.");

        Add(list, "store.error.already_owned",
            "You already own this item.",
            "Bu ürüne zaten sahipsin.",
            "Ya posees este objeto.",
            "Vous possédez déjà cet objet.",
            "Du besitzt diesen Artikel bereits.",
            "이미 보유한 아이템입니다.",
            "Этот предмет уже принадлежит вам.");

        Add(list, "store.error.catalog",
            "Catalog item or trusted server price is unavailable.",
            "Katalog ürünü veya güvenilir sunucu fiyatı kullanılamıyor.",
            "El objeto o precio del servidor no está disponible.",
            "L’objet ou le prix serveur est indisponible.",
            "Katalogartikel oder Serverpreis ist nicht verfügbar.",
            "카탈로그 아이템 또는 서버 가격을 사용할 수 없습니다.",
            "Предмет каталога или серверная цена недоступны.");

        Add(list, "store.error.promo_invalid",
            "The code format is invalid.",
            "Kod formatı geçersiz.",
            "El formato del código no es válido.",
            "Le format du code est invalide.",
            "Das Codeformat ist ungültig.",
            "코드 형식이 올바르지 않습니다.",
            "Неверный формат кода.");

        Add(list, "store.error.promo_not_found",
            "Code not found.",
            "Kod bulunamadı.",
            "Código no encontrado.",
            "Code introuvable.",
            "Code nicht gefunden.",
            "코드를 찾을 수 없습니다.",
            "Код не найден.");

        Add(list, "store.error.promo_disabled",
            "This code is disabled.",
            "Bu kod devre dışı.",
            "Este código está desactivado.",
            "Ce code est désactivé.",
            "Dieser Code ist deaktiviert.",
            "비활성화된 코드입니다.",
            "Этот код отключён.");

        Add(list, "store.error.promo_not_started",
            "This code is not active yet.",
            "Bu kod henüz aktif değil.",
            "Este código aún no está activo.",
            "Ce code n’est pas encore actif.",
            "Dieser Code ist noch nicht aktiv.",
            "아직 활성화되지 않은 코드입니다.",
            "Этот код ещё не активен.");

        Add(list, "store.error.promo_expired",
            "This code has expired.",
            "Bu kodun süresi doldu.",
            "Este código ha caducado.",
            "Ce code a expiré.",
            "Dieser Code ist abgelaufen.",
            "만료된 코드입니다.",
            "Срок действия кода истёк.");

        Add(list, "store.error.promo_global_limit",
            "This code reached its global redemption limit.",
            "Bu kod genel kullanım limitine ulaştı.",
            "Este código alcanzó su límite global.",
            "Ce code a atteint sa limite globale.",
            "Dieser Code hat sein globales Limit erreicht.",
            "전체 사용 한도에 도달한 코드입니다.",
            "Достигнут общий лимит использования кода.");

        Add(list, "store.error.promo_account_limit",
            "Your account has already reached this code’s limit.",
            "Hesabınız bu kodun kullanım limitine ulaştı.",
            "Tu cuenta alcanzó el límite de este código.",
            "Votre compte a atteint la limite de ce code.",
            "Dein Konto hat das Limit dieses Codes erreicht.",
            "이 계정의 코드 사용 한도에 도달했습니다.",
            "Ваш аккаунт достиг лимита этого кода.");

        Add(list, "store.error.emulator_only",
            "This development storefront currently works only with local emulators.",
            "Bu geliştirme mağazası şu anda yalnızca local emulator ile çalışır.",
            "Esta tienda de desarrollo solo funciona con emuladores locales.",
            "Cette boutique de développement fonctionne uniquement avec les émulateurs locaux.",
            "Dieser Entwicklungs-Shop funktioniert derzeit nur mit lokalen Emulatoren.",
            "현재 개발 상점은 로컬 에뮬레이터에서만 작동합니다.",
            "Эта тестовая версия магазина работает только с локальными эмуляторами.");

        Add(list, "store.error.identity",
            "Authenticated development identity is unavailable.",
            "Doğrulanmış geliştirme kimliği kullanılamıyor.",
            "La identidad autenticada de desarrollo no está disponible.",
            "L’identité de développement authentifiée est indisponible.",
            "Authentifizierte Entwicklungsidentität ist nicht verfügbar.",
            "인증된 개발 ID를 사용할 수 없습니다.",
            "Аутентифицированная тестовая учётная запись недоступна.");

        Add(list, "store.error.invalid_purchase",
            "Purchase request is invalid.",
            "Satın alma isteği geçersiz.",
            "La solicitud de compra no es válida.",
            "La demande d’achat est invalide.",
            "Kaufanfrage ist ungültig.",
            "구매 요청이 올바르지 않습니다.",
            "Неверный запрос покупки.");

        Add(list, "store.error.refresh",
            "Store data could not be refreshed.",
            "Mağaza verileri yenilenemedi.",
            "No se pudieron actualizar los datos.",
            "Impossible d’actualiser les données de la boutique.",
            "Shop-Daten konnten nicht aktualisiert werden.",
            "상점 데이터를 새로고칠 수 없습니다.",
            "Не удалось обновить данные магазина.");

        Add(list, "store.error.dev_reset",
            "Development purchase reset failed.",
            "Geliştirme satın alım sıfırlaması başarısız oldu.",
            "No se pudieron restablecer las compras de desarrollo.",
            "La réinitialisation des achats de développement a échoué.",
            "Das Zurücksetzen der Entwicklungskäufe ist fehlgeschlagen.",
            "개발 구매 초기화에 실패했습니다.",
            "Не удалось сбросить тестовые покупки.");

        Add(list, "store.error.generic",
            "Transaction failed. No value was confirmed.",
            "İşlem başarısız. Herhangi bir değer onaylanmadı.",
            "La transacción falló. No se confirmó ningún valor.",
            "La transaction a échoué. Aucune valeur n’a été confirmée.",
            "Transaktion fehlgeschlagen. Kein Wert wurde bestätigt.",
            "거래 실패. 값이 확정되지 않았습니다.",
            "Транзакция не выполнена. Изменение ценности не подтверждено.");

        Add(list, "store.error.idempotency_conflict",
            "This transaction no longer matches its original request. Please try again.",
            "Bu işlem ilk isteğiyle artık eşleşmiyor. Lütfen tekrar deneyin.",
            "La transacción ya no coincide con la solicitud original. Inténtalo de nuevo.",
            "La transaction ne correspond plus à la demande initiale. Réessayez.",
            "Die Transaktion stimmt nicht mehr mit der ursprünglichen Anfrage überein. Bitte erneut versuchen.",
            "거래가 원래 요청과 일치하지 않습니다. 다시 시도해 주세요.",
            "Транзакция больше не соответствует исходному запросу. Попробуйте снова.");

        Add(list, "store.item.pawn_explorer",
            "Explorer Pawn",
            "Kaşif Piyonu",
            "Peón Explorador",
            "Pion Explorateur",
            "Entdecker-Spielfigur",
            "탐험가 말",
            "Фишка Исследователь");

        Add(list, "store.item.dice_midnight",
            "Midnight Dice",
            "Gece Yarısı Zarı",
            "Dados Medianoche",
            "Dés de Minuit",
            "Mitternachtswürfel",
            "미드나잇 주사위",
            "Полуночные кубики");

        Add(list, "store.item.board_walnut",
            "Walnut Board",
            "Ceviz Tahta",
            "Tablero Nogal",
            "Plateau Noyer",
            "Walnussbrett",
            "호두나무 보드",
            "Ореховая доска");

        Add(list, "store.item.frame_founder",
            "Founder Frame",
            "Kurucu Çerçevesi",
            "Marco Fundador",
            "Cadre Fondateur",
            "Gründerrahmen",
            "파운더 프레임",
            "Рамка Основателя");

        Add(list, "store.item.emote_wave",
            "Wave Emote",
            "El Sallama Emote",
            "Emote Saludo",
            "Émote Salut",
            "Winken-Emote",
            "손흔들기 이모트",
            "Эмоция Приветствие");

        Add(list, "store.item.motion_bounce",
            "Bounce Motion",
            "Zıplama Hareketi",
            "Movimiento Rebote",
            "Mouvement Rebond",
            "Hüpfbewegung",
            "바운스 모션",
            "Анимация Прыжок");

        Add(list, "seasonal.tab.event",
            "EVENT", "ETKİNLİK", "EVENTO", "ÉVÉNEMENT",
            "EVENT", "이벤트", "СОБЫТИЕ");
        Add(list, "seasonal.home.title",
            "SEASONAL EVENT", "SEZON ETKİNLİĞİ", "EVENTO DE TEMPORADA",
            "ÉVÉNEMENT SAISONNIER", "SAISON-EVENT", "시즌 이벤트",
            "СЕЗОННОЕ СОБЫТИЕ");
        Add(list, "seasonal.home.inactive",
            "No seasonal event is active right now.",
            "Şu anda aktif bir sezon etkinliği yok.",
            "No hay ningún evento de temporada activo.",
            "Aucun événement saisonnier n’est actif.",
            "Derzeit ist kein Saison-Event aktiv.",
            "현재 활성 시즌 이벤트가 없습니다.",
            "Сейчас нет активного сезонного события.");
        Add(list, "seasonal.home.open",
            "OPEN EVENT", "ETKİNLİĞİ AÇ", "ABRIR EVENTO",
            "OUVRIR L’ÉVÉNEMENT", "EVENT ÖFFNEN", "이벤트 열기",
            "ОТКРЫТЬ СОБЫТИЕ");
        Add(list, "seasonal.page.title",
            "SEASONAL EVENT", "SEZON ETKİNLİĞİ", "EVENTO DE TEMPORADA",
            "ÉVÉNEMENT SAISONNIER", "SAISON-EVENT", "시즌 이벤트",
            "СЕЗОННОЕ СОБЫТИЕ");
        Add(list, "seasonal.harvest.title",
            "ATLAS HARVEST", "ATLAS HASAT", "COSECHA ATLAS",
            "RÉCOLTE ATLAS", "ATLAS-ERNTE", "아틀라스 수확",
            "УРОЖАЙ ATLAS");
        Add(list, "seasonal.harvest.subtitle",
            "Complete challenges, earn event tickets and unlock limited cosmetics.",
            "Görevleri tamamla, etkinlik bileti kazan ve sınırlı kozmetikleri aç.",
            "Completa desafíos, gana boletos y desbloquea cosméticos limitados.",
            "Terminez des défis, gagnez des tickets et débloquez des cosmétiques limités.",
            "Schließe Herausforderungen ab, verdiene Tickets und schalte limitierte Kosmetik frei.",
            "도전을 완료하고 이벤트 티켓과 한정 꾸미기 아이템을 획득하세요.",
            "Выполняйте задания, получайте билеты и открывайте ограниченную косметику.");
        Add(list, "seasonal.summary",
            "Tickets: {0:N0}   •   Event XP: {1:N0}   •   Ends: {2}",
            "Bilet: {0:N0}   •   Etkinlik XP: {1:N0}   •   Bitiş: {2}",
            "Boletos: {0:N0}   •   XP: {1:N0}   •   Termina: {2}",
            "Tickets : {0:N0}   •   XP : {1:N0}   •   Fin : {2}",
            "Tickets: {0:N0}   •   Event-XP: {1:N0}   •   Ende: {2}",
            "티켓: {0:N0}   •   이벤트 XP: {1:N0}   •   종료: {2}",
            "Билеты: {0:N0}   •   XP события: {1:N0}   •   До: {2}");
        Add(list, "seasonal.ticket",
            "Event Ticket", "Etkinlik Bileti", "Boleto del evento",
            "Ticket d’événement", "Event-Ticket", "이벤트 티켓",
            "Билет события");
        Add(list, "seasonal.challenges.title",
            "CHALLENGES", "GÖREVLER", "DESAFÍOS", "DÉFIS",
            "HERAUSFORDERUNGEN", "도전 과제", "ЗАДАНИЯ");
        Add(list, "seasonal.challenges.empty",
            "No challenges are available.", "Kullanılabilir görev yok.",
            "No hay desafíos disponibles.", "Aucun défi disponible.",
            "Keine Herausforderungen verfügbar.", "사용 가능한 도전 과제가 없습니다.",
            "Нет доступных заданий.");
        Add(list, "seasonal.challenge.daily.title",
            "Daily Check-In", "Günlük Giriş", "Registro diario",
            "Connexion quotidienne", "Täglicher Check-in", "일일 체크인",
            "Ежедневный вход");
        Add(list, "seasonal.challenge.daily.body",
            "Claim today’s daily reward.", "Bugünün günlük ödülünü al.",
            "Obtén la recompensa diaria.", "Récupérez la récompense quotidienne.",
            "Hole die heutige Tagesbelohnung ab.", "오늘의 일일 보상을 받으세요.",
            "Получите сегодняшнюю ежедневную награду.");
        Add(list, "seasonal.challenge.weekly.title",
            "Weekly Collector", "Haftalık Koleksiyoncu", "Coleccionista semanal",
            "Collectionneur hebdomadaire", "Wöchentlicher Sammler", "주간 수집가",
            "Еженедельный коллекционер");
        Add(list, "seasonal.challenge.weekly.body",
            "Own at least two cosmetic items.", "En az iki kozmetik ürüne sahip ol.",
            "Posee al menos dos cosméticos.", "Possédez au moins deux cosmétiques.",
            "Besitze mindestens zwei Kosmetikartikel.", "꾸미기 아이템을 2개 이상 보유하세요.",
            "Имейте минимум два косметических предмета.");
        Add(list, "seasonal.challenge.reward",
            "+{0} tickets • +{1} XP", "+{0} bilet • +{1} XP",
            "+{0} boletos • +{1} XP", "+{0} tickets • +{1} XP",
            "+{0} Tickets • +{1} XP", "+{0} 티켓 • +{1} XP",
            "+{0} билетов • +{1} XP");
        Add(list, "seasonal.track.title",
            "REWARD TRACK", "ÖDÜL YOLU", "RUTA DE RECOMPENSAS",
            "PARCOURS DE RÉCOMPENSES", "BELOHNUNGSPFAD", "보상 트랙",
            "ЛИНЕЙКА НАГРАД");
        Add(list, "seasonal.track.tier_1",
            "Tier 1", "Seviye 1", "Nivel 1", "Palier 1", "Stufe 1", "1단계", "Уровень 1");
        Add(list, "seasonal.track.tier_2",
            "Tier 2", "Seviye 2", "Nivel 2", "Palier 2", "Stufe 2", "2단계", "Уровень 2");
        Add(list, "seasonal.track.tier_3",
            "Tier 3", "Seviye 3", "Nivel 3", "Palier 3", "Stufe 3", "3단계", "Уровень 3");
        Add(list, "seasonal.track.requirement",
            "Requires {0} XP", "{0} XP gerekir", "Requiere {0} XP",
            "Nécessite {0} XP", "Benötigt {0} XP", "{0} XP 필요",
            "Требуется {0} XP");
        Add(list, "seasonal.track.reward",
            "Reward: {0} {1}", "Ödül: {0} {1}", "Recompensa: {0} {1}",
            "Récompense : {0} {1}", "Belohnung: {0} {1}", "보상: {0} {1}",
            "Награда: {0} {1}");
        Add(list, "seasonal.items.title",
            "LIMITED ITEMS", "SINIRLI ÜRÜNLER", "OBJETOS LIMITADOS",
            "OBJETS LIMITÉS", "LIMITIERTE ARTIKEL", "한정 아이템",
            "ОГРАНИЧЕННЫЕ ПРЕДМЕТЫ");
        Add(list, "seasonal.item.price",
            "{0} tickets", "{0} bilet", "{0} boletos", "{0} tickets",
            "{0} Tickets", "{0} 티켓", "{0} билетов");
        Add(list, "seasonal.item.buy",
            "BUY", "SATIN AL", "COMPRAR", "ACHETER", "KAUFEN", "구매", "КУПИТЬ");
        Add(list, "seasonal.claim",
            "CLAIM", "AL", "RECLAMAR", "RÉCUPÉRER", "ABHOLEN", "받기", "ПОЛУЧИТЬ");
        Add(list, "seasonal.claimed",
            "CLAIMED", "ALINDI", "OBTENIDO", "RÉCUPÉRÉ", "ABGEHOLT", "수령 완료", "ПОЛУЧЕНО");
        Add(list, "seasonal.response.loading",
            "Loading seasonal event...", "Sezon etkinliği yükleniyor...",
            "Cargando evento...", "Chargement de l’événement...",
            "Saison-Event wird geladen...", "시즌 이벤트 불러오는 중...",
            "Загрузка сезонного события...");
        Add(list, "seasonal.response.challenge_claimed",
            "Challenge reward claimed.", "Görev ödülü alındı.",
            "Recompensa del desafío obtenida.", "Récompense du défi récupérée.",
            "Herausforderungsbelohnung erhalten.", "도전 보상을 받았습니다.",
            "Награда за задание получена.");
        Add(list, "seasonal.response.track_claimed",
            "Reward-track prize claimed.", "Ödül yolu ödülü alındı.",
            "Premio de la ruta obtenido.", "Récompense du parcours récupérée.",
            "Belohnungspfad-Preis erhalten.", "보상 트랙 보상을 받았습니다.",
            "Награда линейки получена.");
        Add(list, "seasonal.response.purchase_done",
            "Seasonal item purchased: {0}", "Sezon ürünü satın alındı: {0}",
            "Objeto de temporada comprado: {0}", "Objet saisonnier acheté : {0}",
            "Saisonartikel gekauft: {0}", "시즌 아이템 구매 완료: {0}",
            "Сезонный предмет куплен: {0}");
        Add(list, "seasonal.popup.challenge_title",
            "CHALLENGE COMPLETE", "GÖREV TAMAMLANDI", "DESAFÍO COMPLETADO",
            "DÉFI TERMINÉ", "HERAUSFORDERUNG ERLEDIGT", "도전 완료",
            "ЗАДАНИЕ ВЫПОЛНЕНО");
        Add(list, "seasonal.popup.track_title",
            "REWARD CLAIMED", "ÖDÜL ALINDI", "RECOMPENSA OBTENIDA",
            "RÉCOMPENSE RÉCUPÉRÉE", "BELOHNUNG ERHALTEN", "보상 획득",
            "НАГРАДА ПОЛУЧЕНА");
        Add(list, "seasonal.popup.purchase_title",
            "SEASONAL PURCHASE", "SEZON SATIN ALIMI", "COMPRA DE TEMPORADA",
            "ACHAT SAISONNIER", "SAISONKAUF", "시즌 구매", "СЕЗОННАЯ ПОКУПКА");
        Add(list, "seasonal.error.event_inactive",
            "The seasonal event is not active.", "Sezon etkinliği aktif değil.",
            "El evento no está activo.", "L’événement n’est pas actif.",
            "Das Saison-Event ist nicht aktiv.", "시즌 이벤트가 활성화되어 있지 않습니다.",
            "Сезонное событие не активно.");
        Add(list, "seasonal.error.challenge_not_complete",
            "Challenge requirements are not complete yet.",
            "Görev şartları henüz tamamlanmadı.",
            "Los requisitos del desafío aún no se completaron.",
            "Les conditions du défi ne sont pas encore remplies.",
            "Die Herausforderung ist noch nicht abgeschlossen.",
            "도전 조건이 아직 완료되지 않았습니다.",
            "Условия задания ещё не выполнены.");
        Add(list, "seasonal.error.challenge_unavailable",
            "This challenge is unavailable.", "Bu görev kullanılamıyor.",
            "Este desafío no está disponible.", "Ce défi est indisponible.",
            "Diese Herausforderung ist nicht verfügbar.", "이 도전 과제를 사용할 수 없습니다.",
            "Это задание недоступно.");
        Add(list, "seasonal.error.track_locked",
            "This reward-track tier is still locked.", "Bu ödül yolu seviyesi henüz kilitli.",
            "Este nivel aún está bloqueado.", "Ce palier est encore verrouillé.",
            "Diese Belohnungsstufe ist noch gesperrt.", "이 보상 단계는 아직 잠겨 있습니다.",
            "Этот уровень наград ещё заблокирован.");
        Add(list, "seasonal.error.item_unavailable",
            "This limited item is unavailable.", "Bu sınırlı ürün kullanılamıyor.",
            "Este objeto limitado no está disponible.", "Cet objet limité est indisponible.",
            "Dieser limitierte Artikel ist nicht verfügbar.", "이 한정 아이템은 사용할 수 없습니다.",
            "Этот ограниченный предмет недоступен.");
        Add(list, "seasonal.error.insufficient_tickets",
            "You do not have enough event tickets.", "Yeterli etkinlik biletin yok.",
            "No tienes suficientes boletos del evento.", "Vous n’avez pas assez de tickets.",
            "Du hast nicht genug Event-Tickets.", "이벤트 티켓이 부족합니다.",
            "Недостаточно билетов события.");
        Add(list, "store.item.pawn_harvest_fox",
            "Harvest Fox Pawn", "Hasat Tilkisi Piyonu", "Peón Zorro de Cosecha",
            "Pion Renard des Moissons", "Erntefuchs-Spielfigur", "수확 여우 말",
            "Фишка Урожайная Лиса");
        Add(list, "store.item.dice_harvest_leaf",
            "Harvest Leaf Dice", "Hasat Yaprağı Zarı", "Dados Hoja de Cosecha",
            "Dés Feuille des Moissons", "Ernteblatt-Würfel", "수확 잎 주사위",
            "Кубики Лист Урожая");

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
        string key, string en, string tr, string es, string fr,
        string de, string ko, string ru)
    {
        list.Add(new AtlasBoardLocalizationDatabase.Entry
        {
            key = key, en = en, tr = tr, es = es, fr = fr,
            de = de, ko = ko, ru = ru
        });
    }
}
#endif
