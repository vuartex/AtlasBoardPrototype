#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardLocalizationV1Setup
{
    private const string DataFolder =
        "Assets/Project/Data/Localization";

    private const string DatabasePath =
        DataFolder +
        "/Localization_Default.asset";

    private const string FontProfilePath =
        DataFolder +
        "/LocalizationFonts_Default.asset";

    private const string SystemObjectName =
        "LocalizationSystem";

    [MenuItem(
        "Atlas Board/Localization/Build or Refresh Localization Foundation v1")]
    public static void BuildOrRefresh()
    {
        EnsureFolder(
            DataFolder);

        AtlasBoardLocalizationDatabase database =
            GetOrCreateDatabase();

        AtlasBoardLocalizationFontProfile fontProfile =
            GetOrCreateFontProfile();

        AutoDetectNotoFonts(
            fontProfile);

        EnsureLocalizationSystem(
            database,
            fontProfile);

        int repairedFonts =
            RepairMissingSceneFontAssets(
                fontProfile);

        int localizedTexts =
            BindStaticSceneTexts();

        int legacyTexts =
            BindLegacySceneTexts();

        int dropdowns =
            BindKnownDropdowns();

        int guarded =
            AddLayoutGuards();

        AssetDatabase.SaveAssets();

        EditorSceneManager.MarkAllScenesDirty();

        Debug.Log(
            "AtlasBoard Localization Foundation v1.1 ready. " +
            $"TMP texts bound={localizedTexts}, " +
            $"legacy texts bound={legacyTexts}, " +
            $"dropdowns bound={dropdowns}, " +
            $"TMP layout guards={guarded}, " +
            $"missing font references repaired={repairedFonts}. " +
            "Run Validate All Languages before release.");
    }

    [MenuItem(
        "Atlas Board/Localization/Validate All Languages")]
    public static void ValidateAllLanguages()
    {
        AtlasBoardLocalizationDatabase database =
            AssetDatabase.LoadAssetAtPath<
                AtlasBoardLocalizationDatabase>(
                    DatabasePath);

        AtlasBoardLocalizationFontProfile fonts =
            AssetDatabase.LoadAssetAtPath<
                AtlasBoardLocalizationFontProfile>(
                    FontProfilePath);

        if (database == null)
        {
            Debug.LogError(
                "Localization_Default.asset not found. Build Localization Foundation first.");

            return;
        }

        AtlasBoardLocalizedText[] localizedTexts =
            Resources.FindObjectsOfTypeAll<
                AtlasBoardLocalizedText>()
                .Where(
                    item =>
                        item != null &&
                        item.gameObject.scene.IsValid())
                .ToArray();

        AtlasBoardLocalizedDropdown[] localizedDropdowns =
            Resources.FindObjectsOfTypeAll<
                AtlasBoardLocalizedDropdown>()
                .Where(
                    item =>
                        item != null &&
                        item.gameObject.scene.IsValid())
                .ToArray();

        AtlasBoardLocalizedLegacyText[] legacyTexts =
            Resources.FindObjectsOfTypeAll<
                AtlasBoardLocalizedLegacyText>()
                .Where(
                    item =>
                        item != null &&
                        item.gameObject.scene.IsValid())
                .ToArray();

        string restoreCode =
            AtlasBoardLocalizationLanguages.Normalize(
                AtlasBoardUserSettingsStore
                    .Load()
                    .LanguageCode);

        int totalOverflow = 0;
        int totalMissing = 0;
        int totalMissingGlyphs = 0;

        foreach (string code
                 in AtlasBoardLocalizationLanguages.Codes)
        {
            int languageOverflow = 0;
            int languageMissing = 0;
            int languageMissingGlyphs = 0;

            foreach (AtlasBoardLocalizedText item
                     in localizedTexts)
            {
                TMP_Text text =
                    item.TargetText;

                if (text == null)
                {
                    continue;
                }

                string key =
                    item.LocalizationKey;

                if (!database.HasTranslation(
                        key,
                        code))
                {
                    languageMissing++;
                    continue;
                }

                text.text =
                    database.Get(
                        key,
                        code);

                ApplyPreviewFont(
                    text,
                    fonts,
                    code);

                ApplySafeSizing(
                    text);

                int missingGlyphs =
                    EnsureFontCharacters(
                        text.font,
                        text.text);

                if (missingGlyphs > 0)
                {
                    languageMissingGlyphs +=
                        missingGlyphs;

                    continue;
                }

                text.ForceMeshUpdate(
                    true,
                    true);

                if (text.isTextOverflowing)
                {
                    languageOverflow++;

                    Debug.LogWarning(
                        $"[Localization QA] {code} overflow: " +
                        $"{GetHierarchyPath(text.transform)} " +
                        $"key={key} text='{text.text}'",
                        text);
                }
            }

            foreach (AtlasBoardLocalizedDropdown localized
                     in localizedDropdowns)
            {
                PreviewDropdown(
                    localized,
                    database,
                    fonts,
                    code);

                TMP_Dropdown dropdown =
                    localized.Dropdown;

                if (dropdown == null ||
                    dropdown.captionText == null)
                {
                    continue;
                }

                int dropdownMissingGlyphs =
                    EnsureFontCharacters(
                        dropdown.captionText.font,
                        dropdown.captionText.text);

                if (dropdownMissingGlyphs > 0)
                {
                    languageMissingGlyphs +=
                        dropdownMissingGlyphs;
                }
                else
                {
                    dropdown.captionText.ForceMeshUpdate(
                        true,
                        true);
                }

                if (dropdownMissingGlyphs == 0 &&
                    dropdown.captionText.isTextOverflowing)
                {
                    languageOverflow++;

                    Debug.LogWarning(
                        $"[Localization QA] {code} dropdown overflow: " +
                        $"{GetHierarchyPath(dropdown.transform)}",
                        dropdown);
                }

                foreach (string key
                         in localized.OptionKeys ??
                            Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(
                            key) ||
                        key.StartsWith(
                            "literal:"))
                    {
                        continue;
                    }

                    if (!database.HasTranslation(
                            key,
                            code))
                    {
                        languageMissing++;
                    }
                }
            }

            foreach (AtlasBoardLocalizedLegacyText item
                     in legacyTexts)
            {
                UnityEngine.UI.Text text =
                    item.TargetText;

                if (text == null)
                {
                    continue;
                }

                string key =
                    item.LocalizationKey;

                if (!database.HasTranslation(
                        key,
                        code))
                {
                    languageMissing++;
                    continue;
                }

                text.text =
                    database.Get(
                        key,
                        code);

                if (fonts != null)
                {
                    Font resolved =
                        fonts.GetLegacyFont(
                            code,
                            text.font);

                    if (resolved != null)
                    {
                        text.font =
                            resolved;
                    }
                }

                if (text.font != null)
                {
                    int legacyMissing =
                        CountLegacyMissingCharacters(
                            text.font,
                            text.text);

                    languageMissingGlyphs +=
                        legacyMissing;
                }
            }

            totalOverflow +=
                languageOverflow;

            totalMissing +=
                languageMissing;

            totalMissingGlyphs +=
                languageMissingGlyphs;

            if (languageMissingGlyphs > 0)
            {
                Debug.LogWarning(
                    $"Localization QA {code}: " +
                    $"missing glyphs={languageMissingGlyphs}. " +
                    "Check LocalizationFonts_Default and source font coverage.");
            }

            Debug.Log(
                $"Localization QA {code}: " +
                $"overflow={languageOverflow}, " +
                $"missing={languageMissing}, " +
                $"glyphMissing={languageMissingGlyphs}.");
        }

        PreviewSceneLanguage(
            restoreCode,
            database,
            fonts,
            localizedTexts,
            localizedDropdowns);

        PreviewLegacyLanguage(
            restoreCode,
            database,
            fonts,
            legacyTexts);

        Debug.Log(
            "Localization QA complete across EN/TR/ES/FR/DE/KO/RU. " +
            $"Total overflow={totalOverflow}, " +
            $"total missing={totalMissing}, " +
            $"total glyphMissing={totalMissingGlyphs}. " +
            "Manual review is only needed for QA-reported items.");
    }

    [MenuItem(
        "Atlas Board/Localization/Preview/English")]
    private static void PreviewEnglish() =>
        PreviewLanguage("en");

    [MenuItem(
        "Atlas Board/Localization/Preview/Türkçe")]
    private static void PreviewTurkish() =>
        PreviewLanguage("tr");

    [MenuItem(
        "Atlas Board/Localization/Preview/Español")]
    private static void PreviewSpanish() =>
        PreviewLanguage("es");

    [MenuItem(
        "Atlas Board/Localization/Preview/Français")]
    private static void PreviewFrench() =>
        PreviewLanguage("fr");

    [MenuItem(
        "Atlas Board/Localization/Preview/Deutsch")]
    private static void PreviewGerman() =>
        PreviewLanguage("de");

    [MenuItem(
        "Atlas Board/Localization/Preview/한국어")]
    private static void PreviewKorean() =>
        PreviewLanguage("ko");

    [MenuItem(
        "Atlas Board/Localization/Preview/Русский")]
    private static void PreviewRussian() =>
        PreviewLanguage("ru");

    private static void PreviewLanguage(
        string code)
    {
        AtlasBoardLocalizationDatabase database =
            AssetDatabase.LoadAssetAtPath<
                AtlasBoardLocalizationDatabase>(
                    DatabasePath);

        AtlasBoardLocalizationFontProfile fonts =
            AssetDatabase.LoadAssetAtPath<
                AtlasBoardLocalizationFontProfile>(
                    FontProfilePath);

        if (database == null)
        {
            Debug.LogError(
                "Localization database not found.");

            return;
        }

        AtlasBoardLocalizedText[] texts =
            Resources.FindObjectsOfTypeAll<
                AtlasBoardLocalizedText>()
                .Where(
                    item =>
                        item != null &&
                        item.gameObject.scene.IsValid())
                .ToArray();

        AtlasBoardLocalizedDropdown[] dropdowns =
            Resources.FindObjectsOfTypeAll<
                AtlasBoardLocalizedDropdown>()
                .Where(
                    item =>
                        item != null &&
                        item.gameObject.scene.IsValid())
                .ToArray();

        AtlasBoardLocalizedLegacyText[] legacyTexts =
            Resources.FindObjectsOfTypeAll<
                AtlasBoardLocalizedLegacyText>()
                .Where(
                    item =>
                        item != null &&
                        item.gameObject.scene.IsValid())
                .ToArray();

        PreviewSceneLanguage(
            code,
            database,
            fonts,
            texts,
            dropdowns);

        PreviewLegacyLanguage(
            code,
            database,
            fonts,
            legacyTexts);

        EditorSceneManager.MarkAllScenesDirty();

        Debug.Log(
            $"Localization editor preview applied: {code}.");
    }

    private static AtlasBoardLocalizationDatabase
        GetOrCreateDatabase()
    {
        AtlasBoardLocalizationDatabase database =
            AssetDatabase.LoadAssetAtPath<
                AtlasBoardLocalizationDatabase>(
                    DatabasePath);

        if (database == null)
        {
            database =
                ScriptableObject.CreateInstance<
                    AtlasBoardLocalizationDatabase>();

            AssetDatabase.CreateAsset(
                database,
                DatabasePath);
        }

        List<AtlasBoardLocalizationDatabase.Entry> list =
            new List<AtlasBoardLocalizationDatabase.Entry>();

        AddEntry(
            list,
            "common.player",
            "Player",
            "Oyuncu",
            "Jugador",
            "Joueur",
            "Spieler",
            "플레이어",
            "Игрок");

        AddEntry(
            list,
            "common.bot_suffix",
            " (BOT)",
            " (BOT)",
            " (BOT)",
            " (BOT)",
            " (BOT)",
            " (봇)",
            " (БОТ)");

        AddEntry(
            list,
            "common.ready",
            "Ready",
            "Hazır",
            "Listo",
            "Prêt",
            "Bereit",
            "준비",
            "Готов");

        AddEntry(
            list,
            "common.human",
            "Human",
            "İnsan",
            "Humano",
            "Humain",
            "Mensch",
            "사람",
            "Человек");

        AddEntry(
            list,
            "common.bot",
            "Bot",
            "Bot",
            "Bot",
            "Bot",
            "Bot",
            "봇",
            "Бот");

        AddEntry(
            list,
            "common.cancel",
            "Cancel",
            "İptal",
            "Cancelar",
            "Annuler",
            "Abbrechen",
            "취소",
            "Отмена");

        AddEntry(
            list,
            "common.apply",
            "Apply",
            "Uygula",
            "Aplicar",
            "Appliquer",
            "Anwenden",
            "적용",
            "Применить");

        AddEntry(
            list,
            "common.reset_defaults",
            "Reset Defaults",
            "Varsayılanlara Dön",
            "Restablecer",
            "Réinitialiser",
            "Zurücksetzen",
            "기본값 복원",
            "Сбросить");

        AddEntry(
            list,
            "common.off",
            "Off",
            "Kapalı",
            "Desactivado",
            "Désactivé",
            "Aus",
            "끔",
            "Выкл.");

        AddEntry(
            list,
            "common.unlimited",
            "Unlimited",
            "Sınırsız",
            "Sin límite",
            "Illimité",
            "Unbegrenzt",
            "무제한",
            "Без ограничений");

        AddEntry(
            list,
            "menu.player",
            "PLAYER",
            "OYUNCU",
            "JUGADOR",
            "JOUEUR",
            "SPIELER",
            "플레이어",
            "ИГРОК");

        AddEntry(
            list,
            "menu.private_table",
            "PRIVATE TABLE",
            "ÖZEL MASA",
            "MESA PRIVADA",
            "TABLE PRIVÉE",
            "PRIVATER TISCH",
            "비공개 테이블",
            "ПРИВАТНЫЙ СТОЛ");

        AddEntry(
            list,
            "menu.local_private",
            "LOCAL / PRIVATE",
            "YEREL / ÖZEL",
            "LOCAL / PRIVADA",
            "LOCAL / PRIVÉ",
            "LOKAL / PRIVAT",
            "로컬 / 비공개",
            "ЛОКАЛЬНО / ПРИВАТНО");

        AddEntry(
            list,
            "menu.play",
            "PLAY",
            "OYNA",
            "JUGAR",
            "JOUER",
            "SPIELEN",
            "플레이",
            "ИГРАТЬ");

        AddEntry(
            list,
            "menu.solo_bots",
            "SOLO / BOTS",
            "TEK / BOTLAR",
            "SOLO / BOTS",
            "SOLO / BOTS",
            "SOLO / BOTS",
            "솔로 / 봇",
            "СОЛО / БОТЫ");

        AddEntry(
            list,
            "menu.shop",
            "SHOP",
            "MAĞAZA",
            "TIENDA",
            "BOUTIQUE",
            "SHOP",
            "상점",
            "МАГАЗИН");

        AddEntry(
            list,
            "menu.cosmetics_items",
            "COSMETICS / ITEMS",
            "KOZMETİK / EŞYA",
            "COSMÉTICOS / OBJETOS",
            "COSMÉTIQUES / OBJETS",
            "KOSMETIK / OBJEKTE",
            "꾸미기 / 아이템",
            "КОСМЕТИКА / ПРЕДМЕТЫ");

        AddEntry(
            list,
            "menu.shop_body",
            "Shop foundation is ready.\nItems, cosmetics and progression can be added later.",
            "Mağaza altyapısı hazır.\nEşyalar, kozmetikler ve ilerleme sistemi daha sonra eklenebilir.",
            "La base de la tienda está lista.\nLos objetos, cosméticos y la progresión se añadirán más adelante.",
            "La base de la boutique est prête.\nLes objets, cosmétiques et la progression pourront être ajoutés plus tard.",
            "Die Shop-Grundlage ist bereit.\nGegenstände, Kosmetik und Fortschritt können später ergänzt werden.",
            "상점 기반이 준비되었습니다.\n아이템, 꾸미기 요소와 진행 시스템은 나중에 추가할 수 있습니다.",
            "Основа магазина готова.\nПредметы, косметику и прогрессию можно добавить позже.");

        AddEntry(
            list,
            "menu.profile",
            "PROFILE",
            "PROFİL",
            "PERFIL",
            "PROFIL",
            "PROFIL",
            "프로필",
            "ПРОФИЛЬ");

        AddEntry(
            list,
            "menu.profile_body",
            "{0}\nCash: {1:N0}\nGold: {2:N0}",
            "{0}\nPara: {1:N0}\nAltın: {2:N0}",
            "{0}\nDinero: {1:N0}\nOro: {2:N0}",
            "{0}\nArgent : {1:N0}\nOr : {2:N0}",
            "{0}\nGeld: {1:N0}\nGold: {2:N0}",
            "{0}\n현금: {1:N0}\n골드: {2:N0}",
            "{0}\nДеньги: {1:N0}\nЗолото: {2:N0}");

        AddEntry(
            list,
            "menu.start_match_error_title",
            "START MATCH",
            "MAÇI BAŞLAT",
            "INICIAR PARTIDA",
            "LANCER LA PARTIE",
            "SPIEL STARTEN",
            "게임 시작",
            "НАЧАТЬ МАТЧ");

        AddEntry(
            list,
            "menu.start_match_error_body",
            "The new lobby could not start the match automatically.\nThe legacy setup screen was kept hidden.\nCheck the Console mapping message.",
            "Yeni lobi maçı otomatik olarak başlatamadı.\nEski kurulum ekranı gizli tutuldu.\nConsole eşleme mesajını kontrol et.",
            "El nuevo lobby no pudo iniciar la partida automáticamente.\nLa pantalla de configuración anterior permaneció oculta.\nRevisa el mensaje de asignación en la consola.",
            "Le nouveau lobby n'a pas pu lancer la partie automatiquement.\nL'ancien écran de configuration est resté masqué.\nVérifiez le message de correspondance dans la console.",
            "Die neue Lobby konnte das Spiel nicht automatisch starten.\nDer alte Einrichtungsbildschirm blieb ausgeblendet.\nPrüfe die Zuordnungsmeldung in der Konsole.",
            "새 로비에서 게임을 자동으로 시작하지 못했습니다.\n기존 설정 화면은 숨김 상태로 유지되었습니다.\n콘솔의 매핑 메시지를 확인하세요.",
            "Новая лобби-система не смогла автоматически начать матч.\nСтарый экран настройки остался скрыт.\nПроверьте сообщение сопоставления в консоли.");

        AddEntry(
            list,
            "lobby.game_lobby",
            "GAME LOBBY",
            "OYUN LOBİSİ",
            "SALA DE JUEGO",
            "SALON DE JEU",
            "SPIELLOBBY",
            "게임 로비",
            "ИГРОВОЕ ЛОББИ");

        AddEntry(
            list,
            "lobby.game_settings",
            "GAME SETTINGS",
            "OYUN AYARLARI",
            "AJUSTES DE PARTIDA",
            "PARAMÈTRES DE PARTIE",
            "SPIELEINSTELLUNGEN",
            "게임 설정",
            "НАСТРОЙКИ МАТЧА");

        AddEntry(
            list,
            "lobby.map",
            "Map",
            "Harita",
            "Mapa",
            "Carte",
            "Karte",
            "맵",
            "Карта");

        AddEntry(
            list,
            "lobby.player_count",
            "Player Count",
            "Oyuncu Sayısı",
            "Número de jugadores",
            "Nombre de joueurs",
            "Spieleranzahl",
            "플레이어 수",
            "Количество игроков");

        AddEntry(
            list,
            "lobby.round_limit",
            "Round Limit",
            "Tur Sayısı",
            "Límite de rondas",
            "Limite de tours",
            "Rundenlimit",
            "라운드 제한",
            "Лимит раундов");

        AddEntry(
            list,
            "lobby.environment_theme",
            "Environment Theme",
            "Ortam Teması",
            "Tema del entorno",
            "Thème de l'environnement",
            "Umgebungsthema",
            "환경 테마",
            "Тема окружения");

        AddEntry(
            list,
            "lobby.balanced_development",
            "Balanced Development",
            "Dengeli Geliştirme",
            "Desarrollo equilibrado",
            "Développement équilibré",
            "Ausgeglichene Entwicklung",
            "균형 개발",
            "Сбалансированное развитие");

        AddEntry(
            list,
            "lobby.doubles",
            "Doubles",
            "Çift Zar",
            "Dobles",
            "Doubles",
            "Pasch",
            "더블",
            "Дубли");

        AddEntry(
            list,
            "lobby.triple_double_penalty",
            "Triple Double Penalty",
            "3 Çift Üst Üste Ceza",
            "Penalización por triple doble",
            "Pénalité de triple double",
            "Strafe bei drei Paschen",
            "3연속 더블 페널티",
            "Штраф за три дубля");

        AddEntry(
            list,
            "lobby.players",
            "PLAYERS",
            "OYUNCULAR",
            "JUGADORES",
            "JOUEURS",
            "SPIELER",
            "플레이어",
            "ИГРОКИ");

        AddEntry(
            list,
            "lobby.local_player",
            "LOCAL PLAYER",
            "YEREL OYUNCU",
            "JUGADOR LOCAL",
            "JOUEUR LOCAL",
            "LOKALER SPIELER",
            "로컬 플레이어",
            "ЛОКАЛЬНЫЙ ИГРОК");

        AddEntry(
            list,
            "lobby.start_match",
            "START MATCH",
            "MAÇI BAŞLAT",
            "INICIAR PARTIDA",
            "LANCER LA PARTIE",
            "SPIEL STARTEN",
            "게임 시작",
            "НАЧАТЬ МАТЧ");

        AddEntry(
            list,
            "lobby.player_1",
            "PLAYER 1",
            "OYUNCU 1",
            "JUGADOR 1",
            "JOUEUR 1",
            "SPIELER 1",
            "플레이어 1",
            "ИГРОК 1");

        AddEntry(
            list,
            "lobby.player_2",
            "PLAYER 2",
            "OYUNCU 2",
            "JUGADOR 2",
            "JOUEUR 2",
            "SPIELER 2",
            "플레이어 2",
            "ИГРОК 2");

        AddEntry(
            list,
            "lobby.player_3",
            "PLAYER 3",
            "OYUNCU 3",
            "JUGADOR 3",
            "JOUEUR 3",
            "SPIELER 3",
            "플레이어 3",
            "ИГРОК 3");

        AddEntry(
            list,
            "lobby.player_4",
            "PLAYER 4",
            "OYUNCU 4",
            "JUGADOR 4",
            "JOUEUR 4",
            "SPIELER 4",
            "플레이어 4",
            "ИГРОК 4");

        AddEntry(
            list,
            "map.turkey",
            "Turkey",
            "Türkiye",
            "Turquía",
            "Turquie",
            "Türkei",
            "튀르키예",
            "Турция");

        AddEntry(
            list,
            "map.colorado",
            "Colorado",
            "Colorado",
            "Colorado",
            "Colorado",
            "Colorado",
            "콜로라도",
            "Колорадо");

        AddEntry(
            list,
            "map.usa",
            "USA",
            "ABD",
            "EE. UU.",
            "États-Unis",
            "USA",
            "미국",
            "США");

        AddEntry(
            list,
            "theme.classic_table",
            "Classic Table",
            "Klasik Masa",
            "Mesa clásica",
            "Table classique",
            "Klassischer Tisch",
            "클래식 테이블",
            "Классический стол");

        AddEntry(
            list,
            "theme.garden",
            "Garden",
            "Bahçe",
            "Jardín",
            "Jardin",
            "Garten",
            "정원",
            "Сад");

        AddEntry(
            list,
            "theme.beach",
            "Beach",
            "Sahil",
            "Playa",
            "Plage",
            "Strand",
            "해변",
            "Пляж");

        AddEntry(
            list,
            "theme.pavilion",
            "Pavilion",
            "Pavyon",
            "Pabellón",
            "Pavillon",
            "Pavillon",
            "파빌리온",
            "Павильон");

        AddEntry(
            list,
            "theme.street",
            "Street",
            "Sokak",
            "Calle",
            "Rue",
            "Straße",
            "거리",
            "Улица");

        AddEntry(
            list,
            "settings.title",
            "SETTINGS",
            "AYARLAR",
            "AJUSTES",
            "PARAMÈTRES",
            "EINSTELLUNGEN",
            "설정",
            "НАСТРОЙКИ");

        AddEntry(
            list,
            "settings.tab.audio",
            "AUDIO",
            "SES",
            "AUDIO",
            "AUDIO",
            "AUDIO",
            "오디오",
            "ЗВУК");

        AddEntry(
            list,
            "settings.tab.gameplay",
            "GAMEPLAY",
            "OYUN",
            "JUGABILIDAD",
            "JEU",
            "GAMEPLAY",
            "게임플레이",
            "ИГРА");

        AddEntry(
            list,
            "settings.tab.graphics",
            "GRAPHICS",
            "GRAFİK",
            "GRÁFICOS",
            "GRAPHISMES",
            "GRAFIK",
            "그래픽",
            "ГРАФИКА");

        AddEntry(
            list,
            "settings.tab.controls",
            "CONTROLS",
            "KONTROLLER",
            "CONTROLES",
            "COMMANDES",
            "STEUERUNG",
            "조작",
            "УПРАВЛЕНИЕ");

        AddEntry(
            list,
            "settings.audio_title",
            "AUDIO SETTINGS",
            "SES AYARLARI",
            "AJUSTES DE AUDIO",
            "PARAMÈTRES AUDIO",
            "AUDIOEINSTELLUNGEN",
            "오디오 설정",
            "НАСТРОЙКИ ЗВУКА");

        AddEntry(
            list,
            "settings.mute_all",
            "Mute All Audio",
            "Tüm Sesi Kapat",
            "Silenciar todo",
            "Couper tout le son",
            "Gesamten Ton stummschalten",
            "모든 소리 음소거",
            "Отключить весь звук");

        AddEntry(
            list,
            "settings.master_volume",
            "Master Volume",
            "Genel Ses",
            "Volumen general",
            "Volume général",
            "Gesamtlautstärke",
            "전체 음량",
            "Общая громкость");

        AddEntry(
            list,
            "settings.main_music",
            "Main Music",
            "Ana Müzik",
            "Música principal",
            "Musique principale",
            "Hauptmusik",
            "메인 음악",
            "Основная музыка");

        AddEntry(
            list,
            "settings.theme_ambience",
            "Theme / Ambience",
            "Tema / Ortam Sesi",
            "Tema / Ambiente",
            "Thème / Ambiance",
            "Thema / Ambiente",
            "테마 / 환경음",
            "Тема / Атмосфера");

        AddEntry(
            list,
            "settings.dice",
            "Dice",
            "Zar",
            "Dados",
            "Dés",
            "Würfel",
            "주사위",
            "Кости");

        AddEntry(
            list,
            "settings.effects_ui_pawn",
            "Effects / UI / Pawn",
            "Efekt / Arayüz / Piyon",
            "Efectos / UI / Ficha",
            "Effets / UI / Pion",
            "Effekte / UI / Figur",
            "효과 / UI / 말",
            "Эффекты / UI / Фишка");

        AddEntry(
            list,
            "settings.gameplay_title",
            "GAMEPLAY SETTINGS",
            "OYUN AYARLARI",
            "AJUSTES DE JUGABILIDAD",
            "PARAMÈTRES DE JEU",
            "GAMEPLAY-EINSTELLUNGEN",
            "게임플레이 설정",
            "НАСТРОЙКИ ИГРЫ");

        AddEntry(
            list,
            "settings.language",
            "Language",
            "Dil",
            "Idioma",
            "Langue",
            "Sprache",
            "언어",
            "Язык");

        AddEntry(
            list,
            "settings.camera_sensitivity",
            "Camera Sensitivity",
            "Kamera Hassasiyeti",
            "Sensibilidad de cámara",
            "Sensibilité de la caméra",
            "Kameraempfindlichkeit",
            "카메라 감도",
            "Чувствительность камеры");

        AddEntry(
            list,
            "settings.zoom_sensitivity",
            "Camera Zoom Sensitivity",
            "Kamera Yakınlaştırma Hassasiyeti",
            "Sensibilidad de zoom",
            "Sensibilité du zoom",
            "Zoom-Empfindlichkeit",
            "카메라 줌 감도",
            "Чувствительность зума");

        AddEntry(
            list,
            "settings.pan_sensitivity",
            "Camera Pan Sensitivity",
            "Kamera Kaydırma Hassasiyeti",
            "Sensibilidad de desplazamiento",
            "Sensibilité du déplacement",
            "Schwenk-Empfindlichkeit",
            "카메라 이동 감도",
            "Чувствительность панорамирования");

        AddEntry(
            list,
            "settings.bot_turn_speed",
            "Bot Turn Speed",
            "Bot Tur Hızı",
            "Velocidad de turno del bot",
            "Vitesse des bots",
            "Bot-Zuggeschwindigkeit",
            "봇 턴 속도",
            "Скорость хода ботов");

        AddEntry(
            list,
            "settings.reduce_camera_motion",
            "Reduce Camera Motion",
            "Kamera Hareketini Azalt",
            "Reducir movimiento de cámara",
            "Réduire les mouvements de caméra",
            "Kamerabewegung reduzieren",
            "카메라 움직임 줄이기",
            "Уменьшить движение камеры");

        AddEntry(
            list,
            "settings.ui_hints",
            "UI Hints",
            "Arayüz İpuçları",
            "Ayudas de interfaz",
            "Aides d'interface",
            "UI-Hinweise",
            "UI 힌트",
            "Подсказки интерфейса");

        AddEntry(
            list,
            "settings.gameplay_confirmations",
            "Gameplay Confirmations",
            "Oyun Onayları",
            "Confirmaciones de juego",
            "Confirmations de jeu",
            "Spielbestätigungen",
            "게임 확인",
            "Подтверждения действий");

        AddEntry(
            list,
            "settings.graphics_title",
            "GRAPHICS SETTINGS",
            "GRAFİK AYARLARI",
            "AJUSTES GRÁFICOS",
            "PARAMÈTRES GRAPHIQUES",
            "GRAFIKEINSTELLUNGEN",
            "그래픽 설정",
            "НАСТРОЙКИ ГРАФИКИ");

        AddEntry(
            list,
            "settings.resolution",
            "Resolution",
            "Çözünürlük",
            "Resolución",
            "Résolution",
            "Auflösung",
            "해상도",
            "Разрешение");

        AddEntry(
            list,
            "settings.display_mode",
            "Display Mode",
            "Ekran Modu",
            "Modo de pantalla",
            "Mode d'affichage",
            "Anzeigemodus",
            "화면 모드",
            "Режим экрана");

        AddEntry(
            list,
            "settings.quality",
            "Quality",
            "Kalite",
            "Calidad",
            "Qualité",
            "Qualität",
            "품질",
            "Качество");

        AddEntry(
            list,
            "settings.vsync",
            "VSync",
            "VSync",
            "VSync",
            "VSync",
            "VSync",
            "VSync",
            "VSync");

        AddEntry(
            list,
            "settings.fps_limit",
            "FPS Limit",
            "FPS Sınırı",
            "Límite de FPS",
            "Limite FPS",
            "FPS-Limit",
            "FPS 제한",
            "Лимит FPS");

        AddEntry(
            list,
            "settings.shadow_quality",
            "Shadow Quality",
            "Gölge Kalitesi",
            "Calidad de sombras",
            "Qualité des ombres",
            "Schattenqualität",
            "그림자 품질",
            "Качество теней");

        AddEntry(
            list,
            "settings.anti_aliasing",
            "Anti-Aliasing",
            "Kenar Yumuşatma",
            "Antialiasing",
            "Anticrénelage",
            "Kantenglättung",
            "안티앨리어싱",
            "Сглаживание");

        AddEntry(
            list,
            "settings.show_fps",
            "Show FPS",
            "FPS Göster",
            "Mostrar FPS",
            "Afficher les FPS",
            "FPS anzeigen",
            "FPS 표시",
            "Показывать FPS");

        AddEntry(
            list,
            "settings.graphics_note",
            "VSync can override the effective FPS limit. Display-mode/resolution changes are applied in the standalone build.",
            "VSync etkin FPS sınırını geçersiz kılabilir. Ekran modu/çözünürlük değişiklikleri bağımsız sürümde uygulanır.",
            "VSync puede anular el límite efectivo de FPS. Los cambios de modo y resolución se aplican en la versión ejecutable.",
            "La VSync peut remplacer la limite FPS effective. Les changements de mode et de résolution s'appliquent dans la version autonome.",
            "VSync kann das effektive FPS-Limit überschreiben. Anzeige- und Auflösungsänderungen werden im Standalone-Build angewendet.",
            "VSync는 실제 FPS 제한보다 우선할 수 있습니다. 화면 모드와 해상도 변경은 독립 실행 빌드에 적용됩니다.",
            "VSync может переопределять фактический лимит FPS. Режим экрана и разрешение применяются в отдельной сборке.");

        AddEntry(
            list,
            "settings.current_editor",
            "Current Game View: {0} x {1}  •  Display {2} x {3} @ {4} Hz",
            "Geçerli Oyun Görünümü: {0} x {1}  •  Ekran {2} x {3} @ {4} Hz",
            "Vista actual: {0} x {1}  •  Pantalla {2} x {3} @ {4} Hz",
            "Vue actuelle : {0} x {1}  •  Écran {2} x {3} @ {4} Hz",
            "Aktuelle Spielansicht: {0} x {1}  •  Anzeige {2} x {3} @ {4} Hz",
            "현재 게임 화면: {0} x {1}  •  디스플레이 {2} x {3} @ {4} Hz",
            "Текущее окно игры: {0} x {1}  •  Экран {2} x {3} @ {4} Гц");

        AddEntry(
            list,
            "settings.current_build",
            "Current: {0} x {1} @ {2} Hz",
            "Geçerli: {0} x {1} @ {2} Hz",
            "Actual: {0} x {1} @ {2} Hz",
            "Actuel : {0} x {1} @ {2} Hz",
            "Aktuell: {0} x {1} @ {2} Hz",
            "현재: {0} x {1} @ {2} Hz",
            "Текущее: {0} x {1} @ {2} Гц");

        AddEntry(
            list,
            "display.exclusive_fullscreen",
            "Exclusive Fullscreen",
            "Tam Ekran",
            "Pantalla completa exclusiva",
            "Plein écran exclusif",
            "Exklusives Vollbild",
            "전용 전체 화면",
            "Эксклюзивный полный экран");

        AddEntry(
            list,
            "display.borderless_fullscreen",
            "Borderless Fullscreen",
            "Çerçevesiz Tam Ekran",
            "Pantalla completa sin bordes",
            "Plein écran sans bordure",
            "Randloses Vollbild",
            "테두리 없는 전체 화면",
            "Полноэкранный без рамки");

        AddEntry(
            list,
            "display.windowed",
            "Windowed",
            "Pencereli",
            "Ventana",
            "Fenêtré",
            "Fenster",
            "창 모드",
            "Оконный");

        AddEntry(
            list,
            "quality.low",
            "Low",
            "Düşük",
            "Baja",
            "Faible",
            "Niedrig",
            "낮음",
            "Низкое");

        AddEntry(
            list,
            "quality.medium",
            "Medium",
            "Orta",
            "Media",
            "Moyenne",
            "Mittel",
            "중간",
            "Среднее");

        AddEntry(
            list,
            "quality.high",
            "High",
            "Yüksek",
            "Alta",
            "Élevée",
            "Hoch",
            "높음",
            "Высокое");

        AddEntry(
            list,
            "quality.very_high",
            "Very High",
            "Çok Yüksek",
            "Muy alta",
            "Très élevée",
            "Sehr hoch",
            "매우 높음",
            "Очень высокое");

        AddEntry(
            list,
            "settings.controls_title",
            "CONTROLS",
            "KONTROLLER",
            "CONTROLES",
            "COMMANDES",
            "STEUERUNG",
            "조작",
            "УПРАВЛЕНИЕ");

        AddEntry(
            list,
            "controls.camera",
            "CAMERA",
            "KAMERA",
            "CÁMARA",
            "CAMÉRA",
            "KAMERA",
            "카메라",
            "КАМЕРА");

        AddEntry(
            list,
            "controls.gameplay",
            "GAMEPLAY",
            "OYUN",
            "JUGABILIDAD",
            "JEU",
            "GAMEPLAY",
            "게임플레이",
            "ИГРА");

        AddEntry(
            list,
            "controls.rotate_camera",
            "Rotate Camera",
            "Kamerayı Döndür",
            "Girar cámara",
            "Faire pivoter la caméra",
            "Kamera drehen",
            "카메라 회전",
            "Вращать камеру");

        AddEntry(
            list,
            "controls.pan_camera",
            "Pan Camera",
            "Kamerayı Kaydır",
            "Desplazar cámara",
            "Déplacer la caméra",
            "Kamera schwenken",
            "카메라 이동",
            "Перемещать камеру");

        AddEntry(
            list,
            "controls.zoom",
            "Zoom",
            "Yakınlaştır",
            "Zoom",
            "Zoom",
            "Zoom",
            "줌",
            "Масштаб");

        AddEntry(
            list,
            "controls.reset_camera",
            "Reset Camera",
            "Kamerayı Sıfırla",
            "Restablecer cámara",
            "Réinitialiser la caméra",
            "Kamera zurücksetzen",
            "카메라 초기화",
            "Сбросить камеру");

        AddEntry(
            list,
            "controls.roll_primary",
            "Roll / Primary Action",
            "Zar At / Ana Eylem",
            "Tirar / Acción principal",
            "Lancer / Action principale",
            "Würfeln / Hauptaktion",
            "굴리기 / 기본 동작",
            "Бросок / Основное действие");

        AddEntry(
            list,
            "controls.large_auction_bid",
            "Large Auction Bid",
            "Büyük Açık Artırma Teklifi",
            "Puja grande",
            "Grande enchère",
            "Großes Auktionsgebot",
            "큰 경매 입찰",
            "Крупная ставка");

        AddEntry(
            list,
            "controls.trade",
            "Trade",
            "Takas",
            "Intercambiar",
            "Échanger",
            "Handeln",
            "거래",
            "Обмен");

        AddEntry(
            list,
            "controls.close_back_settings",
            "Close / Back / Settings",
            "Kapat / Geri / Ayarlar",
            "Cerrar / Atrás / Ajustes",
            "Fermer / Retour / Paramètres",
            "Schließen / Zurück / Einstellungen",
            "닫기 / 뒤로 / 설정",
            "Закрыть / Назад / Настройки");

        AddEntry(
            list,
            "controls.context_note",
            "Primary Action is contextual: Roll, Buy, Continue, Travel, Develop, Trade Accept, Auction Bid and similar actions.",
            "Ana Eylem duruma göre değişir: Zar At, Satın Al, Devam Et, Seyahat, Geliştir, Takası Kabul Et, Teklif Ver ve benzeri.",
            "La acción principal depende del contexto: tirar, comprar, continuar, viajar, desarrollar, aceptar intercambio, pujar y acciones similares.",
            "L'action principale dépend du contexte : lancer, acheter, continuer, voyager, développer, accepter un échange, enchérir, etc.",
            "Die Hauptaktion ist kontextabhängig: Würfeln, Kaufen, Weiter, Reisen, Entwickeln, Handel annehmen, Bieten und ähnliche Aktionen.",
            "기본 동작은 상황에 따라 달라집니다: 주사위 굴리기, 구매, 계속, 이동, 개발, 거래 수락, 경매 입찰 등.",
            "Основное действие зависит от контекста: бросок, покупка, продолжить, путешествие, развитие, принять обмен, ставка и т. п.");

        AddEntry(
            list,
            "game.roll_dice",
            "ROLL DICE",
            "ZAR AT",
            "TIRAR DADOS",
            "LANCER LES DÉS",
            "WÜRFELN",
            "주사위 굴리기",
            "БРОСИТЬ КОСТИ");

        AddEntry(
            list,
            "game.trade",
            "TRADE",
            "TAKAS",
            "INTERCAMBIO",
            "ÉCHANGE",
            "HANDEL",
            "거래",
            "ОБМЕН");

        AddEntry(
            list,
            "tablet.property",
            "PROPERTY",
            "MÜLK",
            "PROPIEDAD",
            "PROPRIÉTÉ",
            "GRUNDSTÜCK",
            "부동산",
            "СОБСТВЕННОСТЬ");

        AddEntry(
            list,
            "tablet.auction",
            "AUCTION",
            "AÇIK ARTIRMA",
            "SUBASTA",
            "ENCHÈRE",
            "AUKTION",
            "경매",
            "АУКЦИОН");

        AddEntry(
            list,
            "tablet.trade",
            "TRADE",
            "TAKAS",
            "INTERCAMBIO",
            "ÉCHANGE",
            "HANDEL",
            "거래",
            "ОБМЕН");

        AddEntry(
            list,
            "tablet.event",
            "EVENT",
            "ETKİNLİK",
            "EVENTO",
            "ÉVÉNEMENT",
            "EREIGNIS",
            "이벤트",
            "СОБЫТИЕ");

        AddEntry(
            list,
            "tablet.result",
            "RESULT",
            "SONUÇ",
            "RESULTADO",
            "RÉSULTAT",
            "ERGEBNIS",
            "결과",
            "РЕЗУЛЬТАТ");

        AddEntry(
            list,
            "tablet.travel",
            "TRAVEL",
            "SEYAHAT",
            "VIAJE",
            "VOYAGE",
            "REISE",
            "여행",
            "ПУТЕШЕСТВИЕ");

        AddEntry(
            list,
            "tablet.development",
            "DEVELOPMENT",
            "GELİŞTİRME",
            "DESARROLLO",
            "DÉVELOPPEMENT",
            "ENTWICKLUNG",
            "개발",
            "РАЗВИТИЕ");

        AddEntry(
            list,
            "tablet.match_result",
            "MATCH RESULT",
            "MAÇ SONUCU",
            "RESULTADO",
            "RÉSULTAT DU MATCH",
            "SPIELERGEBNIS",
            "게임 결과",
            "РЕЗУЛЬТАТ МАТЧА");

        AddEntry(
            list,
            "turn.waiting_player_settings",
            "Waiting for player setup",
            "Oyuncu ayarları bekleniyor",
            "Esperando la configuración",
            "En attente de la configuration",
            "Warte auf Spielereinstellungen",
            "플레이어 설정 대기 중",
            "Ожидание настройки игроков");

        AddEntry(
            list,
            "turn.starting_rolling",
            "{0} is rolling...",
            "{0} zar atıyor...",
            "{0} está tirando...",
            "{0} lance les dés...",
            "{0} würfelt...",
            "{0} 주사위를 굴리는 중...",
            "{0} бросает кости...");

        AddEntry(
            list,
            "turn.active_rolling",
            "Round {0}/{1}\n{2} is rolling...",
            "Tur {0}/{1}\n{2} zar atıyor...",
            "Ronda {0}/{1}\n{2} está tirando...",
            "Tour {0}/{1}\n{2} lance les dés...",
            "Runde {0}/{1}\n{2} würfelt...",
            "라운드 {0}/{1}\n{2} 주사위를 굴리는 중...",
            "Раунд {0}/{1}\n{2} бросает кости...");

        AddEntry(
            list,
            "turn.active_result",
            "Round {0}/{1}\n{2} rolled: {3}",
            "Tur {0}/{1}\n{2} zar attı: {3}",
            "Ronda {0}/{1}\n{2} sacó: {3}",
            "Tour {0}/{1}\n{2} a obtenu : {3}",
            "Runde {0}/{1}\n{2} würfelte: {3}",
            "라운드 {0}/{1}\n{2} 결과: {3}",
            "Раунд {0}/{1}\n{2} выбросил: {3}");

        AddEntry(
            list,
            "turn.triple_double_penalty",
            "{0} rolled doubles three times in a row!\n\nNo movement on this third roll.\nYou cannot roll on your next turn.",
            "{0}, 3 kez üst üste çift attı!\n\nBu üçüncü atışta hareket yok.\nBir sonraki turunda zar atamazsın.",
            "¡{0} sacó dobles tres veces seguidas!\n\nNo hay movimiento en esta tercera tirada.\nNo podrás tirar en tu próximo turno.",
            "{0} a fait trois doubles de suite !\n\nAucun déplacement sur ce troisième lancer.\nImpossible de lancer au prochain tour.",
            "{0} hat dreimal hintereinander einen Pasch gewürfelt!\n\nBeim dritten Wurf gibt es keine Bewegung.\nIm nächsten Zug darf nicht gewürfelt werden.",
            "{0}님이 3번 연속 더블을 굴렸습니다!\n\n세 번째 굴림에는 이동하지 않습니다.\n다음 턴에는 주사위를 굴릴 수 없습니다.",
            "{0} выбросил дубль три раза подряд!\n\nНа третьем броске движения нет.\nВ следующий ход бросок пропускается.");

        AddEntry(
            list,
            "turn.extra_roll",
            "Round {0}/{1}\n{2}: doubles ({3}+{4}) — roll again!",
            "Tur {0}/{1}\n{2}: çift zar ({3}+{4}) — tekrar zar at!",
            "Ronda {0}/{1}\n{2}: dobles ({3}+{4}) — ¡tira otra vez!",
            "Tour {0}/{1}\n{2} : double ({3}+{4}) — relancez !",
            "Runde {0}/{1}\n{2}: Pasch ({3}+{4}) — noch einmal würfeln!",
            "라운드 {0}/{1}\n{2}: 더블 ({3}+{4}) — 다시 굴리세요!",
            "Раунд {0}/{1}\n{2}: дубль ({3}+{4}) — бросайте снова!");

        AddEntry(
            list,
            "turn.skipping",
            "Round {0}/{1}\n{2} skips this turn",
            "Tur {0}/{1}\n{2} bu turu atlıyor",
            "Ronda {0}/{1}\n{2} pierde este turno",
            "Tour {0}/{1}\n{2} passe ce tour",
            "Runde {0}/{1}\n{2} setzt diesen Zug aus",
            "라운드 {0}/{1}\n{2} 이번 턴을 쉽니다",
            "Раунд {0}/{1}\n{2} пропускает ход");

        AddEntry(
            list,
            "turn.match_complete",
            "Match complete\nRound {0}",
            "Maç tamamlandı\nTur {0}",
            "Partida terminada\nRonda {0}",
            "Partie terminée\nTour {0}",
            "Spiel beendet\nRunde {0}",
            "게임 종료\n라운드 {0}",
            "Матч завершён\nРаунд {0}");

        AddEntry(
            list,
            "turn.starting_tie",
            "Tie: {0}{1} roll again",
            "Eşitlik: {0}{1} tekrar zar atsın",
            "Empate: {0}{1} tira de nuevo",
            "Égalité : {0}{1} relance",
            "Gleichstand: {0}{1} würfelt erneut",
            "동점: {0}{1} 다시 굴리세요",
            "Ничья: {0}{1} бросает снова");

        AddEntry(
            list,
            "turn.starting_order",
            "Starting order: {0}{1} roll",
            "Başlangıç sırası: {0}{1} zar atsın",
            "Orden inicial: {0}{1} tira",
            "Ordre de départ : {0}{1} lance",
            "Startreihenfolge: {0}{1} würfelt",
            "시작 순서: {0}{1} 굴리세요",
            "Порядок старта: {0}{1} бросает");

        AddEntry(
            list,
            "turn.current",
            "Round {0}/{1}\nTurn: {2}{3}",
            "Tur {0}/{1}\nSıra: {2}{3}",
            "Ronda {0}/{1}\nTurno: {2}{3}",
            "Tour {0}/{1}\nÀ {2}{3}",
            "Runde {0}/{1}\nAm Zug: {2}{3}",
            "라운드 {0}/{1}\n차례: {2}{3}",
            "Раунд {0}/{1}\nХод: {2}{3}");

        AddEntry(
            list,
            "hud.bankrupt",
            "BANKRUPT",
            "İFLAS",
            "BANCARROTA",
            "FAILLITE",
            "INSOLVENT",
            "파산",
            "БАНКРОТ");

        AddEntry(
            list,
            "hud.turn",
            "TURN",
            "SIRA",
            "TURNO",
            "TOUR",
            "ZUG",
            "차례",
            "ХОД");

        AddEntry(
            list,
            "bot.balanced",
            "Balanced",
            "Dengeli",
            "Equilibrado",
            "Équilibré",
            "Ausgeglichen",
            "균형형",
            "Сбалансированный");

        AddEntry(
            list,
            "bot.safe",
            "Safe",
            "Temkinli",
            "Prudente",
            "Prudent",
            "Vorsichtig",
            "안전형",
            "Осторожный");

        AddEntry(
            list,
            "bot.aggressive",
            "Aggressive",
            "Agresif",
            "Agresivo",
            "Agressif",
            "Aggressiv",
            "공격형",
            "Агрессивный");

        AddEntry(
            list,
            "bot.adaptive",
            "Adaptive",
            "Uyarlanabilir",
            "Adaptativo",
            "Adaptatif",
            "Adaptiv",
            "적응형",
            "Адаптивный");

        AddEntry(
            list,
            "turn.playing",
            "{0} is playing",
            "{0} oynuyor",
            "{0} está jugando",
            "{0} joue",
            "{0} spielt",
            "{0} 플레이 중",
            "{0} играет");

        AddEntry(
            list,
            "hint.continue",
            "SPACE / ENTER  Continue",
            "SPACE / ENTER  Devam",
            "SPACE / ENTER  Continuar",
            "SPACE / ENTER  Continuer",
            "SPACE / ENTER  Weiter",
            "SPACE / ENTER  계속",
            "SPACE / ENTER  Продолжить");

        AddEntry(
            list,
            "hint.trade_offer",
            "SPACE  Accept   •   ESC  Reject",
            "SPACE  Kabul Et   •   ESC  Reddet",
            "SPACE  Aceptar   •   ESC  Rechazar",
            "SPACE  Accepter   •   ESC  Refuser",
            "SPACE  Annehmen   •   ESC  Ablehnen",
            "SPACE  수락   •   ESC  거절",
            "SPACE  Принять   •   ESC  Отклонить");

        AddEntry(
            list,
            "hint.purchase",
            "SPACE  Buy   •   ESC  Pass",
            "SPACE  Satın Al   •   ESC  Geç",
            "SPACE  Comprar   •   ESC  Pasar",
            "SPACE  Acheter   •   ESC  Passer",
            "SPACE  Kaufen   •   ESC  Passen",
            "SPACE  구매   •   ESC  패스",
            "SPACE  Купить   •   ESC  Пропустить");

        AddEntry(
            list,
            "hint.travel",
            "SPACE  Travel   •   ESC  Stay",
            "SPACE  Seyahat Et   •   ESC  Kal",
            "SPACE  Viajar   •   ESC  Quedarse",
            "SPACE  Voyager   •   ESC  Rester",
            "SPACE  Reisen   •   ESC  Bleiben",
            "SPACE  이동   •   ESC  머무르기",
            "SPACE  Поехать   •   ESC  Остаться");

        AddEntry(
            list,
            "hint.develop",
            "SPACE  Develop   •   ESC  Pass",
            "SPACE  Geliştir   •   ESC  Geç",
            "SPACE  Desarrollar   •   ESC  Pasar",
            "SPACE  Développer   •   ESC  Passer",
            "SPACE  Entwickeln   •   ESC  Passen",
            "SPACE  개발   •   ESC  패스",
            "SPACE  Развить   •   ESC  Пропустить");

        AddEntry(
            list,
            "hint.auction",
            "SPACE  +Small Bid   •   SHIFT+SPACE  +Large Bid   •   ESC  Pass",
            "SPACE  +Küçük Teklif   •   SHIFT+SPACE  +Büyük Teklif   •   ESC  Pas",
            "SPACE  +Puja pequeña   •   SHIFT+SPACE  +Puja grande   •   ESC  Pasar",
            "SPACE  +Petite enchère   •   SHIFT+SPACE  +Grande enchère   •   ESC  Passer",
            "SPACE  +Kleines Gebot   •   SHIFT+SPACE  +Großes Gebot   •   ESC  Passen",
            "SPACE  +소액 입찰   •   SHIFT+SPACE  +큰 입찰   •   ESC  패스",
            "SPACE  +Малая ставка   •   SHIFT+SPACE  +Большая ставка   •   ESC  Пас");

        AddEntry(
            list,
            "hint.roll_trade",
            "SPACE  Roll Dice   •   T  Trade",
            "SPACE  Zar At   •   T  Takas",
            "SPACE  Tirar   •   T  Intercambio",
            "SPACE  Lancer   •   T  Échange",
            "SPACE  Würfeln   •   T  Handel",
            "SPACE  주사위   •   T  거래",
            "SPACE  Бросок   •   T  Обмен");

        AddEntry(
            list,
            "hint.roll",
            "SPACE  Roll Dice",
            "SPACE  Zar At",
            "SPACE  Tirar dados",
            "SPACE  Lancer les dés",
            "SPACE  Würfeln",
            "SPACE  주사위 굴리기",
            "SPACE  Бросить кости");

        AddEntry(
            list,
            "tablet.penalty",
            "PENALTY",
            "CEZA",
            "PENALIZACIÓN",
            "PÉNALITÉ",
            "STRAFE",
            "페널티",
            "ШТРАФ");

        AddEntry(
            list,
            "action.buy",
            "BUY",
            "SATIN AL",
            "COMPRAR",
            "ACHETER",
            "KAUFEN",
            "구매",
            "КУПИТЬ");

        AddEntry(
            list,
            "action.pass",
            "PASS",
            "GEÇ",
            "PASAR",
            "PASSER",
            "PASSEN",
            "패스",
            "ПАС");

        AddEntry(
            list,
            "action.accept",
            "ACCEPT",
            "KABUL ET",
            "ACEPTAR",
            "ACCEPTER",
            "ANNEHMEN",
            "수락",
            "ПРИНЯТЬ");

        AddEntry(
            list,
            "action.reject",
            "REJECT",
            "REDDET",
            "RECHAZAR",
            "REFUSER",
            "ABLEHNEN",
            "거절",
            "ОТКЛОНИТЬ");

        AddEntry(
            list,
            "action.send_offer",
            "SEND OFFER",
            "TEKLİF GÖNDER",
            "ENVIAR OFERTA",
            "ENVOYER L'OFFRE",
            "ANGEBOT SENDEN",
            "제안 보내기",
            "ОТПРАВИТЬ ПРЕДЛОЖЕНИЕ");

        AddEntry(
            list,
            "action.travel",
            "TRAVEL",
            "SEYAHAT ET",
            "VIAJAR",
            "VOYAGER",
            "REISEN",
            "이동",
            "ПОЕХАТЬ");

        AddEntry(
            list,
            "action.stay",
            "STAY",
            "KAL",
            "QUEDARSE",
            "RESTER",
            "BLEIBEN",
            "머무르기",
            "ОСТАТЬСЯ");

        AddEntry(
            list,
            "action.develop",
            "DEVELOP",
            "GELİŞTİR",
            "DESARROLLAR",
            "DÉVELOPPER",
            "ENTWICKELN",
            "개발",
            "РАЗВИТЬ");

        AddEntry(
            list,
            "action.continue",
            "CONTINUE",
            "DEVAM",
            "CONTINUAR",
            "CONTINUER",
            "WEITER",
            "계속",
            "ПРОДОЛЖИТЬ");

        AddEntry(
            list,
            "auction.title",
            "AUCTION",
            "AÇIK ARTIRMA",
            "SUBASTA",
            "ENCHÈRE",
            "AUKTION",
            "경매",
            "АУКЦИОН");

        AddEntry(
            list,
            "auction.none",
            "None",
            "Yok",
            "Ninguno",
            "Aucun",
            "Keiner",
            "없음",
            "Нет");

        AddEntry(
            list,
            "auction.property_info",
            "{0}\nList Value: {1} ₵ | Rent: {2} ₵",
            "{0}\nListe Değeri: {1} ₵ | Kira: {2} ₵",
            "{0}\nValor: {1} ₵ | Alquiler: {2} ₵",
            "{0}\nValeur : {1} ₵ | Loyer : {2} ₵",
            "{0}\nListenwert: {1} ₵ | Miete: {2} ₵",
            "{0}\n가격: {1} ₵ | 임대료: {2} ₵",
            "{0}\nСтоимость: {1} ₵ | Рента: {2} ₵");

        AddEntry(
            list,
            "auction.status",
            "Current bid: {0} ₵\nHighest: {1}\nTurn: {2}{3}",
            "Mevcut teklif: {0} ₵\nEn yüksek: {1}\nSıra: {2}{3}",
            "Oferta actual: {0} ₵\nMayor: {1}\nTurno: {2}{3}",
            "Offre actuelle : {0} ₵\nMeilleure : {1}\nTour : {2}{3}",
            "Aktuelles Gebot: {0} ₵\nHöchstes: {1}\nZug: {2}{3}",
            "현재 입찰: {0} ₵\n최고: {1}\n차례: {2}{3}",
            "Текущая ставка: {0} ₵\nЛидер: {1}\nХод: {2}{3}");

        AddEntry(
            list,
            "auction.required_available",
            "Required: {0} ₵ | Available: {1} ₵",
            "Gerekli: {0} ₵ | Mevcut: {1} ₵",
            "Necesario: {0} ₵ | Disponible: {1} ₵",
            "Requis : {0} ₵ | Disponible : {1} ₵",
            "Benötigt: {0} ₵ | Verfügbar: {1} ₵",
            "필요: {0} ₵ | 보유: {1} ₵",
            "Нужно: {0} ₵ | Доступно: {1} ₵");

        AddEntry(
            list,
            "auction.unsold",
            "Property was not sold.",
            "Mülk satılmadı.",
            "La propiedad no se vendió.",
            "La propriété n'a pas été vendue.",
            "Das Grundstück wurde nicht verkauft.",
            "부동산이 판매되지 않았습니다.",
            "Собственность не продана.");

        AddEntry(
            list,
            "auction.refunded",
            "Bid refunded.",
            "Teklif iade edildi.",
            "Oferta reembolsada.",
            "Offre remboursée.",
            "Gebot erstattet.",
            "입찰금이 환불되었습니다.",
            "Ставка возвращена.");

        AddEntry(
            list,
            "auction.winning_bid",
            "Winning bid: {0} ₵",
            "Kazanan teklif: {0} ₵",
            "Oferta ganadora: {0} ₵",
            "Offre gagnante : {0} ₵",
            "Siegergebot: {0} ₵",
            "낙찰가: {0} ₵",
            "Победная ставка: {0} ₵");

        AddEntry(
            list,
            "purchase.prompt",
            "{0}\nPrice: {1} ₵\nDo you want to buy it?",
            "{0}\nFiyat: {1} ₵\nSatın almak istiyor musun?",
            "{0}\nPrecio: {1} ₵\n¿Quieres comprarlo?",
            "{0}\nPrix : {1} ₵\nVoulez-vous l'acheter ?",
            "{0}\nPreis: {1} ₵\nMöchtest du es kaufen?",
            "{0}\n가격: {1} ₵\n구매하시겠습니까?",
            "{0}\nЦена: {1} ₵\nКупить?");

        AddEntry(
            list,
            "trade.title",
            "TRADE — {0}",
            "TAKAS — {0}",
            "INTERCAMBIO — {0}",
            "ÉCHANGE — {0}",
            "HANDEL — {0}",
            "거래 — {0}",
            "ОБМЕН — {0}");

        AddEntry(
            list,
            "trade.no_players",
            "No eligible trade player found.",
            "Uygun takas oyuncusu bulunamadı.",
            "No hay jugadores disponibles para intercambiar.",
            "Aucun joueur disponible pour l'échange.",
            "Kein geeigneter Handelspartner gefunden.",
            "거래 가능한 플레이어가 없습니다.",
            "Нет доступного игрока для обмена.");

        AddEntry(
            list,
            "trade.no_offered_property",
            "No property offered",
            "Mülk teklif etme",
            "Sin propiedad ofrecida",
            "Aucune propriété offerte",
            "Kein Grundstück anbieten",
            "제공할 부동산 없음",
            "Без предлагаемой собственности");

        AddEntry(
            list,
            "trade.no_requested_property",
            "No property requested",
            "Mülk talep etme",
            "Sin propiedad solicitada",
            "Aucune propriété demandée",
            "Kein Grundstück anfordern",
            "요청할 부동산 없음",
            "Без запрашиваемой собственности");

        AddEntry(
            list,
            "trade.no_property",
            "No property",
            "Mülk yok",
            "Sin propiedad",
            "Aucune propriété",
            "Kein Grundstück",
            "부동산 없음",
            "Нет собственности");

        AddEntry(
            list,
            "trade.no_target",
            "No target",
            "Hedef yok",
            "Sin objetivo",
            "Aucune cible",
            "Kein Ziel",
            "대상 없음",
            "Нет цели");

        AddEntry(
            list,
            "trade.target_summary",
            "Trade target: {0}\n\n{1}",
            "Takas hedefi: {0}\n\n{1}",
            "Objetivo del intercambio: {0}\n\n{1}",
            "Cible de l'échange : {0}\n\n{1}",
            "Handelspartner: {0}\n\n{1}",
            "거래 대상: {0}\n\n{1}",
            "Цель обмена: {0}\n\n{1}");

        AddEntry(
            list,
            "trade.offer_summary",
            "{0} gives:\n• {1}\n• {2} ₵\n\n{3} gives:\n• {4}\n• {5} ₵",
            "{0} verir:\n• {1}\n• {2} ₵\n\n{3} verir:\n• {4}\n• {5} ₵",
            "{0} entrega:\n• {1}\n• {2} ₵\n\n{3} entrega:\n• {4}\n• {5} ₵",
            "{0} donne :\n• {1}\n• {2} ₵\n\n{3} donne :\n• {4}\n• {5} ₵",
            "{0} gibt:\n• {1}\n• {2} ₵\n\n{3} gibt:\n• {4}\n• {5} ₵",
            "{0} 제공:\n• {1}\n• {2} ₵\n\n{3} 제공:\n• {4}\n• {5} ₵",
            "{0} отдаёт:\n• {1}\n• {2} ₵\n\n{3} отдаёт:\n• {4}\n• {5} ₵");

        AddEntry(
            list,
            "trade.response_prompt",
            "{0}, do you accept the offer?",
            "{0}, teklifi kabul ediyor musun?",
            "{0}, ¿aceptas la oferta?",
            "{0}, acceptez-vous l'offre ?",
            "{0}, nimmst du das Angebot an?",
            "{0}, 제안을 수락하시겠습니까?",
            "{0}, принять предложение?");

        AddEntry(
            list,
            "trade.accepted",
            "TRADE ACCEPTED",
            "TAKAS KABUL EDİLDİ",
            "INTERCAMBIO ACEPTADO",
            "ÉCHANGE ACCEPTÉ",
            "HANDEL ANGENOMMEN",
            "거래 수락됨",
            "ОБМЕН ПРИНЯТ");

        AddEntry(
            list,
            "trade.rejected",
            "TRADE REJECTED",
            "TAKAS REDDEDİLDİ",
            "INTERCAMBIO RECHAZADO",
            "ÉCHANGE REFUSÉ",
            "HANDEL ABGELEHNT",
            "거래 거절됨",
            "ОБМЕН ОТКЛОНЁН");

        AddEntry(
            list,
            "trade.no_longer_valid",
            "The trade is no longer valid.",
            "Takas artık geçerli değil.",
            "El intercambio ya no es válido.",
            "L'échange n'est plus valide.",
            "Der Handel ist nicht mehr gültig.",
            "거래가 더 이상 유효하지 않습니다.",
            "Обмен больше недействителен.");

        AddEntry(
            list,
            "trade.apply_failed",
            "Trade could not be applied. No changes were made.",
            "Takas uygulanamadı. Hiçbir değişiklik yapılmadı.",
            "No se pudo aplicar el intercambio. No se realizaron cambios.",
            "L'échange n'a pas pu être appliqué. Aucun changement n'a été effectué.",
            "Der Handel konnte nicht ausgeführt werden. Es wurden keine Änderungen vorgenommen.",
            "거래를 적용할 수 없습니다. 변경 사항이 없습니다.",
            "Не удалось применить обмен. Изменений не внесено.");

        AddEntry(
            list,
            "auction.no_bids",
            "No one placed a bid.\nProperty was not sold.",
            "Kimse teklif vermedi.\nMülk satılmadı.",
            "Nadie hizo una oferta.\nLa propiedad no se vendió.",
            "Aucune offre.\nLa propriété n'a pas été vendue.",
            "Niemand hat geboten.\nDas Grundstück wurde nicht verkauft.",
            "입찰이 없습니다.\n부동산이 판매되지 않았습니다.",
            "Никто не сделал ставку.\nСобственность не продана.");

        AddEntry(
            list,
            "auction.no_valid_bid",
            "No valid bid was found.\nProperty was not sold.",
            "Geçerli teklif bulunamadı.\nMülk satılmadı.",
            "No se encontró una oferta válida.\nLa propiedad no se vendió.",
            "Aucune offre valide.\nLa propriété n'a pas été vendue.",
            "Kein gültiges Gebot.\nDas Grundstück wurde nicht verkauft.",
            "유효한 입찰이 없습니다.\n부동산이 판매되지 않았습니다.",
            "Нет действительной ставки.\nСобственность не продана.");

        AddEntry(
            list,
            "auction.winner_cannot_pay",
            "The winning bid could not be paid.\nProperty was not sold.",
            "Kazanan teklif ödenemedi.\nMülk satılmadı.",
            "No se pudo pagar la oferta ganadora.\nLa propiedad no se vendió.",
            "L'offre gagnante n'a pas pu être payée.\nLa propriété n'a pas été vendue.",
            "Das Siegergebot konnte nicht bezahlt werden.\nDas Grundstück wurde nicht verkauft.",
            "낙찰금을 지불할 수 없습니다.\n부동산이 판매되지 않았습니다.",
            "Победную ставку не удалось оплатить.\nСобственность не продана.");

        AddEntry(
            list,
            "auction.ownership_failed",
            "Ownership could not be transferred.\nBid refunded.",
            "Mülkiyet aktarılamadı.\nTeklif iade edildi.",
            "No se pudo transferir la propiedad.\nOferta reembolsada.",
            "Le transfert de propriété a échoué.\nOffre remboursée.",
            "Eigentum konnte nicht übertragen werden.\nGebot erstattet.",
            "소유권을 이전할 수 없습니다.\n입찰금이 환불되었습니다.",
            "Не удалось передать собственность.\nСтавка возвращена.");

        AddEntry(
            list,
            "auction.winner_result",
            "{0} won!\n{1}\nWinning bid: {2} ₵",
            "{0} kazandı!\n{1}\nKazanan teklif: {2} ₵",
            "¡{0} ganó!\n{1}\nOferta ganadora: {2} ₵",
            "{0} a gagné !\n{1}\nOffre gagnante : {2} ₵",
            "{0} hat gewonnen!\n{1}\nSiegergebot: {2} ₵",
            "{0} 승리!\n{1}\n낙찰가: {2} ₵",
            "{0} победил!\n{1}\nПобедная ставка: {2} ₵");

        AddEntry(
            list,
            "auction.insufficient_balance",
            "{0} does not have enough money.\nRequired: {1} ₵ | Available: {2} ₵",
            "{0} için yetersiz bakiye.\nGerekli: {1} ₵ | Mevcut: {2} ₵",
            "{0} no tiene suficiente dinero.\nNecesario: {1} ₵ | Disponible: {2} ₵",
            "Solde insuffisant pour {0}.\nRequis : {1} ₵ | Disponible : {2} ₵",
            "{0} hat nicht genug Geld.\nBenötigt: {1} ₵ | Verfügbar: {2} ₵",
            "{0}의 잔액이 부족합니다.\n필요: {1} ₵ | 보유: {2} ₵",
            "У {0} недостаточно денег.\nНужно: {1} ₵ | Доступно: {2} ₵");

        AtlasBoardEventLocalizationSeed.Append(
            list);

        AtlasBoardSpecialLocalizationSeed.Append(
            list);

        AtlasBoardCoreLocalizationSeed.Append(
            list);

        AtlasBoardPawnLocalizationSeed.Append(
            list);

        database.EditorReplaceEntries(
            list);

        EditorUtility.SetDirty(
            database);

        return database;
    }

    private static AtlasBoardLocalizationFontProfile
        GetOrCreateFontProfile()
    {
        AtlasBoardLocalizationFontProfile profile =
            AssetDatabase.LoadAssetAtPath<
                AtlasBoardLocalizationFontProfile>(
                    FontProfilePath);

        if (profile == null)
        {
            profile =
                ScriptableObject.CreateInstance<
                    AtlasBoardLocalizationFontProfile>();

            AssetDatabase.CreateAsset(
                profile,
                FontProfilePath);
        }

        return profile;
    }

    private static void AddEntry(
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

    private static void AutoDetectNotoFonts(
        AtlasBoardLocalizationFontProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        TMP_FontAsset latin =
            profile.LatinCyrillicFont;

        TMP_FontAsset korean =
            profile.KoreanFont;

        // A Dynamic TMP asset without a source font cannot populate new glyphs.
        if (latin != null &&
            latin.sourceFontFile == null)
        {
            Debug.LogWarning(
                $"Latin/Cyrillic TMP font '{latin.name}' has no Source Font File. " +
                "It cannot dynamically add missing glyphs. A valid Noto Sans TMP asset will be searched.");

            latin = null;
        }

        if (korean != null)
        {
            string family =
                korean.faceInfo.familyName ??
                string.Empty;

            string search =
                (korean.name + " " + family)
                    .ToLowerInvariant();

            bool looksKorean =
                search.Contains("cjkkr") ||
                search.Contains("sans kr") ||
                search.Contains("sanskr") ||
                search.Contains("korean");

            if (!looksKorean)
            {
                Debug.LogWarning(
                    $"Assigned Korean TMP font '{korean.name}' reports Font Face family " +
                    $"'{family}', which does not look Korean. A genuine Korean Noto asset will be searched.");

                korean = null;
            }
            else if (korean.sourceFontFile == null)
            {
                Debug.LogWarning(
                    $"Korean TMP font '{korean.name}' has no Source Font File. " +
                    "A valid Korean Noto TMP asset will be searched.");

                korean = null;
            }
        }

        string[] tmpGuids =
            AssetDatabase.FindAssets(
                "t:TMP_FontAsset");

        foreach (string guid
                 in tmpGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            TMP_FontAsset font =
                AssetDatabase.LoadAssetAtPath<
                    TMP_FontAsset>(
                        path);

            if (font == null ||
                font.sourceFontFile == null)
            {
                continue;
            }

            string family =
                font.faceInfo.familyName ??
                string.Empty;

            string searchName =
                (font.name + " " + family + " " + path)
                    .ToLowerInvariant();

            if (!searchName.Contains(
                    "noto"))
            {
                continue;
            }

            bool looksKorean =
                searchName.Contains("notosanscjkkr") ||
                searchName.Contains("noto sans cjk kr") ||
                searchName.Contains("notosanskr") ||
                searchName.Contains("noto sans kr") ||
                searchName.Contains("korean");

            if (looksKorean)
            {
                if (korean == null)
                {
                    korean = font;
                }

                continue;
            }

            if (latin == null)
            {
                latin = font;
            }
        }

        Font latinLegacy =
            profile.LatinCyrillicLegacyFont;

        Font koreanLegacy =
            profile.KoreanLegacyFont;

        string[] fontGuids =
            AssetDatabase.FindAssets(
                "t:Font");

        foreach (string guid
                 in fontGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            Font font =
                AssetDatabase.LoadAssetAtPath<
                    Font>(
                        path);

            if (font == null)
            {
                continue;
            }

            string searchName =
                (font.name + " " + path)
                    .ToLowerInvariant();

            if (!searchName.Contains(
                    "noto"))
            {
                continue;
            }

            bool looksKorean =
                searchName.Contains("notosanscjkkr") ||
                searchName.Contains("noto sans cjk kr") ||
                searchName.Contains("notosanskr") ||
                searchName.Contains("noto sans kr") ||
                searchName.Contains("korean");

            if (looksKorean)
            {
                koreanLegacy = font;
                continue;
            }

            if (latinLegacy == null ||
                latinLegacy.name.ToLowerInvariant()
                    .Contains("kr"))
            {
                latinLegacy = font;
            }
        }

        profile.EditorConfigure(
            latin,
            korean);

        ConfigureFontFallbacks(
            latin,
            korean);

        profile.EditorConfigureLegacyFonts(
            latinLegacy,
            koreanLegacy);

        EditorUtility.SetDirty(
            profile);

        if (latin == null)
        {
            Debug.LogWarning(
                "No valid Latin/Cyrillic TMP font was found. " +
                "Create a Dynamic TMP asset directly from NotoSans-Regular.ttf.");
        }

        if (korean == null)
        {
            Debug.LogWarning(
                "No valid Korean TMP font was found. " +
                "Use genuine NotoSansCJKkr-Regular.otf / Noto Sans KR and create a Dynamic TMP asset.");
        }
    }

    private static void ConfigureFontFallbacks(
        TMP_FontAsset latin,
        TMP_FontAsset korean)
    {
        if (latin == null ||
            korean == null ||
            latin == korean)
        {
            return;
        }

        if (latin.fallbackFontAssetTable == null)
        {
            latin.fallbackFontAssetTable =
                new List<TMP_FontAsset>();
        }

        if (!latin.fallbackFontAssetTable.Contains(
                korean))
        {
            latin.fallbackFontAssetTable.Add(
                korean);

            EditorUtility.SetDirty(
                latin);
        }

        if (korean.fallbackFontAssetTable == null)
        {
            korean.fallbackFontAssetTable =
                new List<TMP_FontAsset>();
        }

        if (!korean.fallbackFontAssetTable.Contains(
                latin))
        {
            korean.fallbackFontAssetTable.Add(
                latin);

            EditorUtility.SetDirty(
                korean);
        }

        Debug.Log(
            "Localization font fallbacks verified: " +
            $"{latin.name} <-> {korean.name}. " +
            "Native language names and unchanged city names can use the required glyph set.");
    }

    private static int RepairMissingSceneFontAssets(
        AtlasBoardLocalizationFontProfile profile)
    {
        TMP_FontAsset fallback =
            profile != null
                ? profile.LatinCyrillicFont
                : null;

        if (fallback == null)
        {
            fallback =
                TMP_Settings.defaultFontAsset;
        }

        if (fallback == null)
        {
            return 0;
        }

        TMP_Text[] texts =
            Resources.FindObjectsOfTypeAll<
                TMP_Text>()
                .Where(
                    item =>
                        item != null &&
                        item.gameObject.scene.IsValid())
                .ToArray();

        int repaired = 0;

        foreach (TMP_Text text
                 in texts)
        {
            if (text.font != null)
            {
                continue;
            }

            text.font =
                fallback;

            EditorUtility.SetDirty(
                text);

            repaired++;
        }

        return repaired;
    }

    private static void EnsureLocalizationSystem(
        AtlasBoardLocalizationDatabase database,
        AtlasBoardLocalizationFontProfile fontProfile)
    {
        GameObject system =
            FindSceneObject(
                SystemObjectName);

        if (system == null)
        {
            system =
                new GameObject(
                    SystemObjectName);

            Undo.RegisterCreatedObjectUndo(
                system,
                "Create Localization System");
        }

        AtlasBoardLocalizationManager manager =
            system.GetComponent<
                AtlasBoardLocalizationManager>();

        if (manager == null)
        {
            manager =
                Undo.AddComponent<
                    AtlasBoardLocalizationManager>(
                        system);
        }

        manager.EditorConfigure(
            database,
            fontProfile);

        EditorUtility.SetDirty(
            manager);
    }

    private static int BindStaticSceneTexts()
    {
        // IMPORTANT:
        // This dictionary is case-insensitive. Do not add both "Trade" and
        // "TRADE" (or any other case-only duplicate), or BuildOrRefresh will
        // throw ArgumentException before static localization binding finishes.
        Dictionary<string, string> rawToKey =
            new Dictionary<string, string>(
                StringComparer.Ordinal)
        {
            { "PLAYER", "menu.player" },
            { "PRIVATE TABLE", "menu.private_table" },
            { "LOCAL / PRIVATE", "menu.local_private" },
            { "PLAY", "menu.play" },
            { "SOLO / BOTS", "menu.solo_bots" },
            { "SHOP", "menu.shop" },
            { "COSMETICS / ITEMS", "menu.cosmetics_items" },
            { "GAME LOBBY", "lobby.game_lobby" },
            { "GAME SETTINGS", "lobby.game_settings" },
            { "Map", "lobby.map" },
            { "Player Count", "lobby.player_count" },
            { "Round Limit", "lobby.round_limit" },
            { "Environment Theme", "lobby.environment_theme" },
            { "Balanced Development", "lobby.balanced_development" },
            { "Doubles", "lobby.doubles" },
            { "Triple Double Penalty", "lobby.triple_double_penalty" },
            { "PLAYERS", "lobby.players" },
            { "LOCAL PLAYER", "lobby.local_player" },
            { "READY", "common.ready" },
            { "START MATCH", "lobby.start_match" },
            { "PLAYER 1", "lobby.player_1" },
            { "PLAYER 2", "lobby.player_2" },
            { "PLAYER 3", "lobby.player_3" },
            { "PLAYER 4", "lobby.player_4" },
            { "SETTINGS", "settings.title" },
            { "AUDIO", "settings.tab.audio" },
            { "GAMEPLAY", "settings.tab.gameplay" },
            { "GRAPHICS", "settings.tab.graphics" },
            { "CONTROLS", "settings.controls_title" },
            { "AUDIO SETTINGS", "settings.audio_title" },
            { "Mute All Audio", "settings.mute_all" },
            { "Master Volume", "settings.master_volume" },
            { "Main Music", "settings.main_music" },
            { "Theme / Ambience", "settings.theme_ambience" },
            { "Dice", "settings.dice" },
            { "Effects / UI / Pawn", "settings.effects_ui_pawn" },
            { "GAMEPLAY SETTINGS", "settings.gameplay_title" },
            { "Language", "settings.language" },
            { "Camera Sensitivity", "settings.camera_sensitivity" },
            { "Camera Zoom Sensitivity", "settings.zoom_sensitivity" },
            { "Camera Pan Sensitivity", "settings.pan_sensitivity" },
            { "Bot Turn Speed", "settings.bot_turn_speed" },
            { "Reduce Camera Motion", "settings.reduce_camera_motion" },
            { "UI Hints", "settings.ui_hints" },
            { "Gameplay Confirmations", "settings.gameplay_confirmations" },
            { "GRAPHICS SETTINGS", "settings.graphics_title" },
            { "Resolution", "settings.resolution" },
            { "Display Mode", "settings.display_mode" },
            { "Quality", "settings.quality" },
            { "VSync", "settings.vsync" },
            { "FPS Limit", "settings.fps_limit" },
            { "Shadow Quality", "settings.shadow_quality" },
            { "Anti-Aliasing", "settings.anti_aliasing" },
            { "Show FPS", "settings.show_fps" },
            { "VSync can override the effective FPS limit. Display-mode/resolution changes are applied in the standalone build.", "settings.graphics_note" },
            { "Primary Action is contextual: Roll, Buy, Continue, Travel, Develop, Trade Accept, Auction Bid and similar actions.", "controls.context_note" },
            { "CAMERA", "controls.camera" },
            { "Rotate Camera", "controls.rotate_camera" },
            { "Pan Camera", "controls.pan_camera" },
            { "Zoom", "controls.zoom" },
            { "Reset Camera", "controls.reset_camera" },
            { "Roll / Primary Action", "controls.roll_primary" },
            { "Large Auction Bid", "controls.large_auction_bid" },
            { "Close / Back / Settings", "controls.close_back_settings" },
            { "Trade", "controls.trade" },
            { "RESET DEFAULTS", "common.reset_defaults" },
            { "CANCEL", "common.cancel" },
            { "APPLY", "common.apply" },
            { "SATIN AL", "action.buy" },
            { "GEÇ", "action.pass" },
            { "PAS", "action.pass" },
            { "KABUL ET", "action.accept" },
            { "REDDET", "action.reject" },
            { "İPTAL", "common.cancel" },
            { "TEKLİF GÖNDER", "action.send_offer" },
            { "SEYAHAT ET", "action.travel" },
            { "KAL", "action.stay" },
            { "GELİŞTİR", "action.develop" },
            { "DEVAM", "action.continue" },
            { "ZAR AT", "game.roll_dice" },
            { "TAKAS", "game.trade" },
            { "ROLL DICE", "game.roll_dice" },
            { "TRADE", "game.trade" },
        };

        TMP_Text[] texts =
            Resources.FindObjectsOfTypeAll<
                TMP_Text>()
                .Where(
                    item =>
                        item != null &&
                        item.gameObject.scene.IsValid())
                .ToArray();

        int bound = 0;

        foreach (TMP_Text text
                 in texts)
        {
            string raw =
                NormalizeText(
                    text.text);

            if (!rawToKey.TryGetValue(
                    raw,
                    out string key))
            {
                continue;
            }

            AtlasBoardLocalizedText localized =
                text.GetComponent<
                    AtlasBoardLocalizedText>();

            if (localized == null)
            {
                localized =
                    Undo.AddComponent<
                        AtlasBoardLocalizedText>(
                            text.gameObject);
            }

            localized.EditorConfigure(
                key,
                text);

            EditorUtility.SetDirty(
                localized);

            bound++;
        }

        return bound;
    }

    private static int BindLegacySceneTexts()
    {
        Dictionary<string, string> rawToKey =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
        {
            { "ZAR AT", "game.roll_dice" },
            { "TAKAS", "game.trade" },
            { "SATIN AL", "action.buy" },
            { "GEÇ", "action.pass" },
            { "PAS", "action.pass" },
            { "KABUL ET", "action.accept" },
            { "REDDET", "action.reject" },
            { "İPTAL", "common.cancel" },
            { "TEKLİF GÖNDER", "action.send_offer" },
            { "SEYAHAT ET", "action.travel" },
            { "KAL", "action.stay" },
            { "GELİŞTİR", "action.develop" },
            { "DEVAM", "action.continue" }
        };

        UnityEngine.UI.Text[] texts =
            Resources.FindObjectsOfTypeAll<
                UnityEngine.UI.Text>()
                .Where(
                    item =>
                        item != null &&
                        item.gameObject.scene.IsValid())
                .ToArray();

        int bound = 0;

        foreach (UnityEngine.UI.Text text
                 in texts)
        {
            string raw =
                NormalizeText(
                    text.text);

            if (!rawToKey.TryGetValue(
                    raw,
                    out string key))
            {
                continue;
            }

            AtlasBoardLocalizedLegacyText localized =
                text.GetComponent<
                    AtlasBoardLocalizedLegacyText>();

            if (localized == null)
            {
                localized =
                    Undo.AddComponent<
                        AtlasBoardLocalizedLegacyText>(
                            text.gameObject);
            }

            localized.EditorConfigure(
                key,
                text);

            EditorUtility.SetDirty(
                localized);

            bound++;
        }

        return bound;
    }

    private static int BindKnownDropdowns()
    {
        int bound = 0;

        bound += BindDropdown(
            "Dropdown_Map",
            new[]
            {
                "map.turkey",
                "map.colorado",
                "map.usa"
            });

        bound += BindDropdown(
            "Dropdown_EnvironmentTheme",
            new[]
            {
                "theme.classic_table",
                "theme.garden",
                "theme.beach",
                "theme.pavilion",
                "theme.street"
            });

        for (int i = 1;
             i <= 4;
             i++)
        {
            bound += BindDropdown(
                $"Dropdown_Player{i}_Type",
                new[]
                {
                    "common.human",
                    "common.bot"
                });
        }

        bound += BindDropdown(
            "Dropdown_DisplayMode",
            new[]
            {
                "display.exclusive_fullscreen",
                "display.borderless_fullscreen",
                "display.windowed"
            });

        bound += BindDropdown(
            "Dropdown_Quality",
            new[]
            {
                "quality.low",
                "quality.medium",
                "quality.high",
                "quality.very_high"
            });

        bound += BindDropdown(
            "Dropdown_FPSLimit",
            new[]
            {
                "literal:30",
                "literal:60",
                "literal:90",
                "literal:120",
                "literal:144",
                "literal:165",
                "literal:240",
                "common.unlimited"
            });

        bound += BindDropdown(
            "Dropdown_Shadow",
            new[]
            {
                "common.off",
                "quality.low",
                "quality.medium",
                "quality.high"
            });

        bound += BindDropdown(
            "Dropdown_AA",
            new[]
            {
                "common.off",
                "literal:2x",
                "literal:4x",
                "literal:8x"
            });

        bound +=
            BindPlayerTypeDropdownsByContent();

        return bound;
    }

    private static int BindPlayerTypeDropdownsByContent()
    {
        TMP_Dropdown[] dropdowns =
            Resources.FindObjectsOfTypeAll<
                TMP_Dropdown>();

        int count = 0;

        foreach (TMP_Dropdown dropdown
                 in dropdowns)
        {
            if (dropdown == null ||
                !dropdown.gameObject.scene.IsValid() ||
                !HasAncestorNamed(
                    dropdown.transform,
                    "Canvas_MainMenu"))
            {
                continue;
            }

            if (dropdown.options == null ||
                dropdown.options.Count != 2)
            {
                continue;
            }

            string first =
                dropdown.options[0].text ??
                string.Empty;

            string second =
                dropdown.options[1].text ??
                string.Empty;

            bool looksLikePlayerType =
                first.Equals(
                    "Human",
                    StringComparison.OrdinalIgnoreCase) &&
                second.Equals(
                    "Bot",
                    StringComparison.OrdinalIgnoreCase);

            AtlasBoardLocalizedDropdown existing =
                dropdown.GetComponent<
                    AtlasBoardLocalizedDropdown>();

            if (!looksLikePlayerType &&
                existing == null)
            {
                continue;
            }

            AtlasBoardLocalizedDropdown localized =
                existing != null
                    ? existing
                    : Undo.AddComponent<
                        AtlasBoardLocalizedDropdown>(
                            dropdown.gameObject);

            localized.EditorConfigure(
                dropdown,
                new[]
                {
                    "common.human",
                    "common.bot"
                });

            EditorUtility.SetDirty(
                localized);

            count++;
        }

        return count;
    }

    private static int BindDropdown(
        string objectName,
        string[] keys)
    {
        TMP_Dropdown[] dropdowns =
            Resources.FindObjectsOfTypeAll<
                TMP_Dropdown>();

        int count = 0;

        foreach (TMP_Dropdown dropdown
                 in dropdowns)
        {
            if (dropdown == null ||
                !dropdown.gameObject.scene.IsValid() ||
                dropdown.name != objectName)
            {
                continue;
            }

            bool belongsToSettings =
                objectName == "Dropdown_DisplayMode" ||
                objectName == "Dropdown_Quality" ||
                objectName == "Dropdown_FPSLimit" ||
                objectName == "Dropdown_Shadow" ||
                objectName == "Dropdown_AA";

            string requiredCanvas =
                belongsToSettings
                    ? "Canvas_Settings"
                    : "Canvas_MainMenu";

            if (!HasAncestorNamed(
                    dropdown.transform,
                    requiredCanvas))
            {
                continue;
            }

            AtlasBoardLocalizedDropdown localized =
                dropdown.GetComponent<
                    AtlasBoardLocalizedDropdown>();

            if (localized == null)
            {
                localized =
                    Undo.AddComponent<
                        AtlasBoardLocalizedDropdown>(
                            dropdown.gameObject);
            }

            localized.EditorConfigure(
                dropdown,
                keys);

            EditorUtility.SetDirty(
                localized);

            count++;
        }

        return count;
    }

    private static int AddLayoutGuards()
    {
        TMP_Text[] texts =
            Resources.FindObjectsOfTypeAll<
                TMP_Text>()
                .Where(
                    item =>
                        item != null &&
                        item.gameObject.scene.IsValid())
                .ToArray();

        int count = 0;

        foreach (TMP_Text text
                 in texts)
        {
            AtlasBoardLocalizationLayoutGuard guard =
                text.GetComponent<
                    AtlasBoardLocalizationLayoutGuard>();

            if (guard == null)
            {
                guard =
                    Undo.AddComponent<
                        AtlasBoardLocalizationLayoutGuard>(
                            text.gameObject);
            }

            guard.EditorCapture(
                text);

            EditorUtility.SetDirty(
                guard);

            count++;
        }

        return count;
    }

    private static int EnsureFontCharacters(
        TMP_FontAsset font,
        string value)
    {
        if (font == null ||
            string.IsNullOrEmpty(
                value))
        {
            return 0;
        }

        HashSet<char> missing =
            new HashSet<char>();

        foreach (char character
                 in value)
        {
            if (char.IsWhiteSpace(
                    character) ||
                char.IsControl(
                    character))
            {
                continue;
            }

            if (FontAssetSourceHasCharacter(
                    font,
                    character,
                    new HashSet<TMP_FontAsset>()))
            {
                continue;
            }

            missing.Add(
                character);
        }

        return missing.Count;
    }

    private static bool FontAssetSourceHasCharacter(
        TMP_FontAsset font,
        char character,
        HashSet<TMP_FontAsset> visited)
    {
        if (font == null ||
            visited == null ||
            !visited.Add(
                font))
        {
            return false;
        }

        if (font.sourceFontFile != null &&
            font.sourceFontFile.HasCharacter(
                character))
        {
            return true;
        }

        if (font.characterLookupTable != null &&
            font.characterLookupTable.ContainsKey(
                character))
        {
            return true;
        }

        if (font.fallbackFontAssetTable == null)
        {
            return false;
        }

        foreach (TMP_FontAsset fallback
                 in font.fallbackFontAssetTable)
        {
            if (FontAssetSourceHasCharacter(
                    fallback,
                    character,
                    visited))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountLegacyMissingCharacters(
        Font font,
        string value)
    {
        if (font == null ||
            string.IsNullOrEmpty(
                value))
        {
            return 0;
        }

        HashSet<char> missing =
            new HashSet<char>();

        foreach (char character
                 in value)
        {
            if (char.IsWhiteSpace(
                    character) ||
                font.HasCharacter(
                    character))
            {
                continue;
            }

            missing.Add(
                character);
        }

        return missing.Count;
    }

    private static void PreviewLegacyLanguage(
        string code,
        AtlasBoardLocalizationDatabase database,
        AtlasBoardLocalizationFontProfile fonts,
        AtlasBoardLocalizedLegacyText[] legacyTexts)
    {
        foreach (AtlasBoardLocalizedLegacyText localized
                 in legacyTexts)
        {
            UnityEngine.UI.Text text =
                localized.TargetText;

            if (text == null)
            {
                continue;
            }

            text.text =
                database.Get(
                    localized.LocalizationKey,
                    code);

            if (fonts != null)
            {
                Font resolved =
                    fonts.GetLegacyFont(
                        code,
                        text.font);

                if (resolved != null)
                {
                    text.font =
                        resolved;
                }
            }
        }
    }

    private static void PreviewSceneLanguage(
        string code,
        AtlasBoardLocalizationDatabase database,
        AtlasBoardLocalizationFontProfile fonts,
        AtlasBoardLocalizedText[] texts,
        AtlasBoardLocalizedDropdown[] dropdowns)
    {
        foreach (AtlasBoardLocalizedText localized
                 in texts)
        {
            TMP_Text text =
                localized.TargetText;

            if (text == null)
            {
                continue;
            }

            text.text =
                database.Get(
                    localized.LocalizationKey,
                    code);

            ApplyPreviewFont(
                text,
                fonts,
                code);

            ApplySafeSizing(
                text);
        }

        foreach (AtlasBoardLocalizedDropdown dropdown
                 in dropdowns)
        {
            PreviewDropdown(
                dropdown,
                database,
                fonts,
                code);
        }
    }

    private static void PreviewDropdown(
        AtlasBoardLocalizedDropdown localized,
        AtlasBoardLocalizationDatabase database,
        AtlasBoardLocalizationFontProfile fonts,
        string code)
    {
        TMP_Dropdown dropdown =
            localized.Dropdown;

        if (dropdown == null)
        {
            return;
        }

        int current =
            dropdown.value;

        List<TMP_Dropdown.OptionData> options =
            new List<TMP_Dropdown.OptionData>();

        foreach (string key
                 in localized.OptionKeys ??
                    Array.Empty<string>())
        {
            string value =
                key != null &&
                key.StartsWith(
                    "literal:")
                    ? key.Substring(
                        "literal:".Length)
                    : database.Get(
                        key,
                        code);

            options.Add(
                new TMP_Dropdown.OptionData(
                    value));
        }

        dropdown.ClearOptions();
        dropdown.AddOptions(
            options);

        if (dropdown.options.Count > 0)
        {
            dropdown.SetValueWithoutNotify(
                Mathf.Clamp(
                    current,
                    0,
                    dropdown.options.Count - 1));
        }

        dropdown.RefreshShownValue();

        ApplyPreviewFont(
            dropdown.captionText,
            fonts,
            code);

        ApplyPreviewFont(
            dropdown.itemText,
            fonts,
            code);

        ApplySafeSizing(
            dropdown.captionText);

        ApplySafeSizing(
            dropdown.itemText);
    }

    private static void ApplyPreviewFont(
        TMP_Text text,
        AtlasBoardLocalizationFontProfile fonts,
        string code)
    {
        if (text == null ||
            fonts == null)
        {
            return;
        }

        TMP_FontAsset resolved =
            fonts.GetFont(
                code,
                text.font);

        if (resolved != null)
        {
            text.font =
                resolved;
        }
    }

    private static void ApplySafeSizing(
        TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        float max =
            Mathf.Max(
                9f,
                text.fontSize);

        text.enableAutoSizing =
            true;

        text.fontSizeMax =
            max;

        text.fontSizeMin =
            Mathf.Max(
                9f,
                max *
                0.55f);
    }

    private static string NormalizeText(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        string normalized =
            value
                .Replace(
                    "\r",
                    " ")
                .Replace(
                    "\n",
                    " ");

        while (normalized.Contains(
                   "  "))
        {
            normalized =
                normalized.Replace(
                    "  ",
                    " ");
        }

        return normalized.Trim();
    }

    private static bool HasAncestorNamed(
        Transform target,
        string ancestorName)
    {
        Transform current =
            target;

        while (current != null)
        {
            if (string.Equals(
                    current.name,
                    ancestorName,
                    StringComparison.Ordinal))
            {
                return true;
            }

            current =
                current.parent;
        }

        return false;
    }

    private static string GetHierarchyPath(
        Transform target)
    {
        if (target == null)
        {
            return "<null>";
        }

        string path =
            target.name;

        Transform parent =
            target.parent;

        while (parent != null)
        {
            path =
                parent.name +
                "/" +
                path;

            parent =
                parent.parent;
        }

        return path;
    }

    private static GameObject FindSceneObject(
        string objectName)
    {
        GameObject[] all =
            Resources.FindObjectsOfTypeAll<
                GameObject>();

        foreach (GameObject item
                 in all)
        {
            if (item == null ||
                !item.scene.IsValid() ||
                item.name != objectName)
            {
                continue;
            }

            return item;
        }

        return null;
    }

    private static void EnsureFolder(
        string path)
    {
        if (AssetDatabase.IsValidFolder(
                path))
        {
            return;
        }

        string parent =
            System.IO.Path
                .GetDirectoryName(
                    path)
                ?.Replace(
                    "\\",
                    "/");

        string name =
            System.IO.Path
                .GetFileName(
                    path);

        if (!string.IsNullOrWhiteSpace(
                parent) &&
            !AssetDatabase.IsValidFolder(
                parent))
        {
            EnsureFolder(
                parent);
        }

        AssetDatabase.CreateFolder(
            parent,
            name);
    }
}
#endif
