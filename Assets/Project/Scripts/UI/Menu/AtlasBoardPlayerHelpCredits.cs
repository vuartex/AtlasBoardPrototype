using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Lightweight help/FAQ and third-party credits on the existing Main Menu.
/// This is presentation only: no parallel menu, wallet or game logic.
/// All entries fit a scrollable panel. No tutorial interrupts gameplay.
/// </summary>
[DefaultExecutionOrder(32001)]
public sealed class AtlasBoardPlayerHelpCredits : MonoBehaviour
{
    private static AtlasBoardPlayerHelpCredits instance;
    private AtlasBoardMainMenuController menu;
    private Transform menuRoot;
    private Vector2 scroll;
    private bool showHelp;
    private bool showCredits;
    private GUIStyle button;
    private GUIStyle paragraph;
    private GUIStyle heading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject root = new GameObject("AtlasBoard_Help_Credits_Presentation");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<AtlasBoardPlayerHelpCredits>();
    }

    private void LateUpdate()
    {
        if (menu != null && menuRoot != null) return;
        menu = UnityEngine.Object.FindAnyObjectByType<AtlasBoardMainMenuController>();
        if (menu == null) return;
        foreach (Transform child in menu.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "MainMenu") { menuRoot = child; break; }
        }
    }

    private string Language
    {
        get
        {
            AtlasBoardLocalizationManager lm = AtlasBoardLocalizationManager.Instance;
            return lm == null ? "en" : (lm.CurrentLanguageCode ?? "en").ToLowerInvariant();
        }
    }

    private static string Tr(string lang, string en, string tr, string es, string fr, string de, string ko, string ru)
    {
        switch (lang)
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

    private string HelpTitle => Tr(Language, "HELP / FAQ", "YARDIM / SSS", "AYUDA / PREGUNTAS", "AIDE / FAQ", "HILFE / FAQ", "도움말 / FAQ", "ПОМОЩЬ / FAQ");
    private string CreditsTitle => Tr(Language, "CREDITS & LICENSES", "KATKIDA BULUNANLAR / LİSANSLAR", "CRÉDITOS Y LICENCIAS", "CRÉDITS ET LICENCES", "MITWIRKENDE & LIZENZEN", "제작진 / 라이선스", "АВТОРЫ И ЛИЦЕНЗИИ");
    private string CloseLabel => Tr(Language,"CLOSE","KAPAT","CERRAR","FERMER","SCHLIESSEN","닫기","ЗАКРЫТЬ");

    private void OnGUI()
    {
        if (menuRoot == null || !menuRoot.gameObject.activeInHierarchy)
        {
            showHelp = false; showCredits = false;
            return;
        }
        if (button == null)
        {
            button = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            paragraph = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, richText = false };
            heading = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, wordWrap = true };
        }
        GUI.depth = -130;
        if (!showHelp && !showCredits)
        {
            // Bottom-left, away from the existing centered rotating tip and
            // the v0.9.0 beta badge at bottom-right.
            if (GUI.Button(new Rect(18, Screen.height - 48, 142, 32), HelpTitle, button))
            { showHelp = true; scroll = Vector2.zero; }
            if (GUI.Button(new Rect(168, Screen.height - 48, 155, 32), CreditsTitle, button))
            { showCredits = true; scroll = Vector2.zero; }
            return;
        }
        float width = Mathf.Min(850f, Screen.width - 24f);
        float height = Mathf.Min(680f, Screen.height - 24f);
        Rect panel = new Rect((Screen.width-width)/2f, (Screen.height-height)/2f, width, height);
        GUI.Box(panel, showHelp ? HelpTitle : CreditsTitle);
        GUILayout.BeginArea(new Rect(panel.x+20f, panel.y+44f, width-40f, height-60f));
        scroll = GUILayout.BeginScrollView(scroll);
        if (showHelp) DrawHelp(); else DrawCredits();
        GUILayout.EndScrollView();
        GUILayout.Space(8f);
        if (GUILayout.Button(CloseLabel, button, GUILayout.Height(35f)))
        { showHelp = false; showCredits = false; }
        GUILayout.EndArea();
    }

    private void Entry(string q, string a)
    {
        GUILayout.Label(q, heading);
        GUILayout.Label(a, paragraph);
        GUILayout.Space(14);
    }

    private void DrawHelp()
    {
        string l = Language;
        Entry(Tr(l,"How do I join a friend?","Arkadaşımın odasına nasıl katılırım?","¿Cómo me uno a un amigo?","Comment rejoindre un ami ?","Wie trete ich einem Freund bei?","친구 방에 어떻게 참가하나요?","Как присоединиться к другу?"),
            Tr(l,"Open Private Table and enter the six-digit room code, or browse Public Rooms.","Private Table'a girip altı haneli oda kodunu yaz veya Public Rooms'tan oda seç.","Abre Private Table y escribe el código de seis dígitos, o busca en Public Rooms.","Ouvrez Private Table et saisissez le code à six chiffres, ou utilisez Public Rooms.","Öffne Private Table und gib den sechsstelligen Raumcode ein, oder nutze Public Rooms.","Private Table에서 6자리 방 코드를 입력하거나 Public Rooms를 이용하세요.","Откройте Private Table и введите шестизначный код или выберите Public Rooms."));
        Entry(Tr(l,"What happens if I disconnect?","Bağlantım koparsa ne olur?","¿Qué pasa si pierdo la conexión?","Et si ma connexion est coupée ?","Was passiert bei Verbindungsabbruch?","연결이 끊기면 어떻게 되나요?","Что будет при обрыве связи?"),
            Tr(l,"Reconnect with the same account to recover your seat. A temporary bot may control your turn; a different account cannot claim that seat.","Aynı hesapla geri girerek koltuğunu kurtarabilirsin. Geçici bot sıranı oynayabilir; başka hesap o koltuğu alamaz.","Reconéctate con la misma cuenta. Un bot temporal puede jugar tu turno; otra cuenta no puede ocupar tu asiento.","Reconnectez-vous avec le même compte. Un bot temporaire peut jouer votre tour ; un autre compte ne récupère pas votre place.","Verbinde dich mit demselben Konto erneut. Ein temporärer Bot kann deinen Zug spielen; andere Konten übernehmen deinen Platz nicht.","동일한 계정으로 다시 접속하세요. 임시 봇이 턴을 대신할 수 있지만 다른 계정은 좌석을 차지할 수 없습니다.","Подключитесь с тем же аккаунтом. Временный бот может ходить вместо вас, но другой аккаунт не займёт ваше место."));
        Entry(Tr(l,"How do I rematch?","Tekrar maça nasıl başlarım?","¿Cómo vuelvo a jugar?","Comment rejouer ?","Wie starte ich ein Rematch?","재경기는 어떻게 시작하나요?","Как начать реванш?"),
            Tr(l,"After the result screen, choose Rematch. The existing lobby and authenticated player identities are preserved.","Sonuç ekranında Rematch'i seç. Oda ve doğrulanmış oyuncu kimlikleri korunur.","Tras el resultado, elige Rematch. Se conservan la sala y las identidades.","Après le résultat, choisissez Rematch. Le salon et les identités sont conservés.","Wähle nach dem Ergebnis Rematch. Raum und Spieleridentitäten bleiben erhalten.","결과 화면에서 Rematch를 선택하세요. 로비와 플레이어 신원은 유지됩니다.","После результата выберите Rematch. Комната и учётные записи сохраняются."));
        Entry(Tr(l,"Why can't I use a map or cosmetic?","Bir haritayı veya kozmetiği neden kullanamıyorum?","¿Por qué no puedo usar un mapa u objeto?","Pourquoi un objet est-il indisponible ?","Warum ist eine Karte oder Kosmetik gesperrt?","맵이나 꾸미기 아이템을 왜 못 쓰나요?","Почему карта или предмет недоступны?"),
            Tr(l,"Paid items require ownership on your signed-in account. Bots use the default dice. You cannot equip another player's items.","Ücretli içeriklerde hesabına ait sahiplik doğrulanır. Botlar varsayılan zarı kullanır; başkasının eşyası kuşanılamaz.","Los objetos de pago requieren propiedad verificada. Los bots usan el dado predeterminado.","Les objets payants nécessitent la propriété vérifiée. Les bots utilisent les dés par défaut.","Kostenpflichtige Gegenstände benötigen bestätigten Besitz. Bots verwenden Standardwürfel.","유료 아이템은 계정 소유권 확인이 필요합니다. 봇은 기본 주사위를 사용합니다.","Платные предметы требуют подтверждения владения. Боты используют стандартные кости."));
        Entry(Tr(l,"How do Career, XP and Gold work?","Career, XP ve Gold nasıl çalışıyor?","¿Cómo funcionan Career, XP y Gold?","Comment fonctionnent Career, XP et Gold ?","Wie funktionieren Career, XP und Gold?","Career, XP, Gold는 어떻게 작동하나요?","Как работают Career, XP и Gold?"),
            Tr(l,"Eligible completed online matches record Career progress and verified rewards. A pending result may synchronize after reconnect; rewards are not paid twice.","Uygun şekilde tamamlanan çevrim içi maçlar Career ve doğrulanmış ödülleri kaydeder. Sonuç bağlantı sonrası tamamlanabilir; ödüller iki kez verilmez.","Las partidas online válidas registran progreso y premios verificados sin duplicarlos.","Les parties en ligne éligibles enregistrent la progression et les récompenses, sans doublons.","Gültige abgeschlossene Online-Spiele speichern Karriere und Belohnungen ohne Doppelgutschrift.","조건을 충족하는 온라인 경기 결과는 기록되며 보상은 중복 지급되지 않습니다.","Подходящие завершённые онлайн-матчи сохраняют прогресс и награды без повторных выплат."));
        Entry(Tr(l,"Why is an update required?","Neden güncelleme gerekli?","¿Por qué debo actualizar?","Pourquoi une mise à jour ?","Warum ist ein Update erforderlich?","업데이트가 필요한 이유는?","Зачем нужно обновление?"),
            Tr(l,"Online rooms require compatible game, protocol, rules and content versions. Install the current build on every device.","Çevrim içi odalar uyumlu oyun, protokol, kural ve içerik sürümleri ister. Tüm cihazlara güncel sürümü kur.","Todos los dispositivos necesitan versiones compatibles de juego, protocolo, reglas y contenido.","Tous les appareils doivent utiliser des versions compatibles du jeu et du protocole.","Alle Geräte benötigen kompatible Spiel-, Protokoll-, Regel- und Inhaltsversionen.","모든 기기에 호환되는 게임 및 프로토콜 버전을 설치하세요.","Установите совместимые версии игры и протокола на всех устройствах."));
        Entry(Tr(l,"What do Pulse, Bank and Civic do?","Pulse, Bank ve Civic ne işe yarar?","¿Qué son Pulse, Bank y Civic?","À quoi servent Pulse, Bank et Civic ?","Was sind Pulse, Bank und Civic?","Pulse, Bank, Civic는 무엇인가요?","Что такое Pulse, Bank и Civic?"),
            Tr(l,"Pulse develops owned cities; Bank manages loans and mortgages; Civic projects offer separate investments through travel or events.","Pulse şehir geliştirmelerini, Bank kredi ve ipotekleri yönetir. Civic projeleri seyahat ve etkinliklerle ulaşılabilen ayrı yatırımlardır.","Pulse mejora ciudades; Bank gestiona préstamos; Civic ofrece inversiones por viajes y eventos.","Pulse développe les villes, Bank gère les prêts et Civic les projets spéciaux.","Pulse entwickelt Städte, Bank verwaltet Kredite und Civic bietet Sonderprojekte.","Pulse는 도시 개발, Bank는 대출, Civic은 별도 투자를 담당합니다.","Pulse развивает города, Bank управляет кредитами, Civic предлагает отдельные проекты."));
        GUILayout.Space(6);
        GUILayout.Label(Tr(l,"Support: quote the ATB error code shown on screen; never share passwords or authentication tokens.","Destek için ekrandaki ATB hata kodunu paylaş; şifre veya giriş token'ını paylaşma.","Para soporte, indica el código ATB; no compartas contraseñas.","Pour l'assistance, indiquez le code ATB, jamais vos identifiants.","Für Support den ATB-Code angeben, keine Passwörter teilen.","지원 시 ATB 오류 코드를 전달하고 비밀번호는 공유하지 마세요.","Для поддержки сообщите код ATB, но не пароли."), paragraph);
    }

    private void DrawCredits()
    {
        string l = Language;
        Entry(Tr(l,"Atlas Board","Atlas Board","Atlas Board","Atlas Board","Atlas Board","Atlas Board","Atlas Board"),
            Tr(l,"Game development and original integration: AtlasBoard project. Third-party assets retain their creators' rights and licenses.","Oyun geliştirme ve özgün entegrasyon: AtlasBoard projesi. Üçüncü taraf varlıkların hakları ve lisansları sahiplerine aittir.","Desarrollo: proyecto AtlasBoard. Los derechos de terceros siguen vigentes.","Développement : projet AtlasBoard. Droits et licences des auteurs respectés.","Entwicklung: AtlasBoard-Projekt. Rechte Dritter bleiben bei ihren Urhebern.","게임 개발: AtlasBoard 프로젝트. 제3자 에셋 권리는 원저작자에게 있습니다.","Разработка: проект AtlasBoard. Права сторонних авторов сохраняются."));
        Entry("KENNEY — CC0", "Emotes Pack, UI Pack, City Kit Commercial, Nature Kit, Furniture Kit, Mini Characters, Mini Dungeon, Watercraft Kit, interface/audio packs. kenney.nl");
        Entry("POLY HAVEN — CC0", "Suburban Garden, Secluded Beach, Boma, Palermo Sidewalk environment assets. polyhaven.com");
        Entry("GOOGLE / NOTO — SIL OPEN FONT LICENSE 1.1", "Noto Sans and Noto Sans KR; multilingual UI font support. fonts.google.com/noto");
        Entry("UNITY ASSET STORE — LICENSED ASSETS", "Ferocious Industries — PBR Little Games Pack dice; SR Studios Kerala — Little Ghost; OlyPoly — Cartoon Cars; Low Poly Simple Urban City. Each is subject to its applicable Unity Asset Store EULA, not CC0.");
        Entry("OPEN SOURCE / ENGINE", "Unity Engine, TextMesh Pro, Firebase and other dependencies retain their respective licenses and trademarks.");
        GUILayout.Label(Tr(l,"Full source/license ledger: Assets/Project/ThirdPartyLicenses/ and Assets/Project/Documentation/ATLASBOARD_LEGAL_CREDITS.md. Verify all imported assets before publishing.","Ayrıntılı lisans kayıtları: Assets/Project/ThirdPartyLicenses/ ve Assets/Project/Documentation/ATLASBOARD_LEGAL_CREDITS.md. Yayından önce tüm ithal varlıkları doğrula.","Registro de licencias en Assets/Project/ThirdPartyLicenses/.","Registre des licences dans Assets/Project/ThirdPartyLicenses/.","Lizenzregister unter Assets/Project/ThirdPartyLicenses/.","라이선스 기록: Assets/Project/ThirdPartyLicenses/.","Лицензии: Assets/Project/ThirdPartyLicenses/."), paragraph);
    }
}
