#if UNITY_EDITOR
using System.Collections.Generic;

public static class AtlasBoardPublicLobbyLocalizationSeed
{
    public const string Prefix = "public_browser.";

    public static void Append(List<AtlasBoardLocalizationDatabase.Entry> list)
    {
        Add(list, "menu.public_rooms", "PUBLIC ROOMS", "AÇIK ODALAR", "SALAS PÚBLICAS", "SALONS PUBLICS", "ÖFFENTLICHE RÄUME", "공개 방", "ПУБЛИЧНЫЕ КОМНАТЫ");
        Add(list, "menu.public_rooms_subtitle", "BROWSE / JOIN", "GÖZ AT / KATIL", "EXPLORAR / UNIRSE", "PARCOURIR / REJOINDRE", "SUCHEN / BEITRETEN", "찾기 / 참가", "ОБЗОР / ВОЙТИ");
        Add(list, "menu.play_online_subtitle", "HOST ONLINE", "ÇEVRİMİÇİ ODA KUR", "CREAR ONLINE", "HÉBERGER EN LIGNE", "ONLINE HOSTEN", "온라인 방 만들기", "СОЗДАТЬ ОНЛАЙН");
        Add(list, "menu.public_table", "PUBLIC TABLE", "AÇIK MASA", "MESA PÚBLICA", "TABLE PUBLIQUE", "ÖFFENTLICHER TISCH", "공개 테이블", "ПУБЛИЧНЫЙ СТОЛ");

        Add(list, "public_browser.title", "PUBLIC ROOMS", "AÇIK ODALAR", "SALAS PÚBLICAS", "SALONS PUBLICS", "ÖFFENTLICHE RÄUME", "공개 방", "ПУБЛИЧНЫЕ КОМНАТЫ");
        Add(list, "public_browser.subtitle", "Browse and join online tables", "Çevrimiçi masalara göz at ve katıl", "Explora y únete a mesas online", "Parcourez et rejoignez des tables en ligne", "Online-Tische durchsuchen und beitreten", "온라인 테이블 찾기 및 참가", "Просмотр и вход в онлайн-столы");
        Add(list, "public_browser.create", "CREATE PUBLIC ROOM", "AÇIK ODA OLUŞTUR", "CREAR SALA PÚBLICA", "CRÉER UN SALON PUBLIC", "ÖFFENTLICHEN RAUM ERSTELLEN", "공개 방 만들기", "СОЗДАТЬ ПУБЛИЧНУЮ КОМНАТУ");
        Add(list, "public_browser.refresh", "REFRESH", "YENİLE", "ACTUALIZAR", "ACTUALISER", "AKTUALISIEREN", "새로고침", "ОБНОВИТЬ");
        Add(list, "public_browser.join", "JOIN", "KATIL", "UNIRSE", "REJOINDRE", "BEITRETEN", "참가", "ВОЙТИ");

        Add(list, "public_browser.host", "HOST", "KURUCU", "ANFITRIÓN", "HÔTE", "HOST", "호스트", "ХОСТ");
        Add(list, "public_browser.map", "MAP", "HARİTA", "MAPA", "CARTE", "KARTE", "맵", "КАРТА");
        Add(list, "public_browser.players", "PLAYERS", "OYUNCULAR", "JUGADORES", "JOUEURS", "SPIELER", "플레이어", "ИГРОКИ");
        Add(list, "public_browser.rounds", "ROUNDS", "TURLAR", "RONDAS", "TOURS", "RUNDEN", "라운드", "РАУНДЫ");
        Add(list, "public_browser.region", "REGION", "BÖLGE", "REGIÓN", "RÉGION", "REGION", "지역", "РЕГИОН");
        Add(list, "public_browser.access", "ACCESS", "ERİŞİM", "ACCESO", "ACCÈS", "ZUGANG", "접근", "ДОСТУП");
        Add(list, "public_browser.open_short", "OPEN", "AÇIK", "ABIERTA", "OUVERT", "OFFEN", "공개", "ОТКРЫТО");
        Add(list, "public_browser.password_required_short", "PASSWORD", "ŞİFRELİ", "CONTRASEÑA", "MOT DE PASSE", "PASSWORT", "비밀번호", "ПАРОЛЬ");

        Add(list, "public_browser.search_placeholder", "Search host or map...", "Kurucu veya harita ara...", "Buscar anfitrión o mapa...", "Rechercher hôte ou carte...", "Host oder Karte suchen...", "호스트 또는 맵 검색...", "Поиск хоста или карты...");
        Add(list, "public_browser.all_maps", "ALL MAPS", "TÜM HARİTALAR", "TODOS LOS MAPAS", "TOUTES LES CARTES", "ALLE KARTEN", "모든 맵", "ВСЕ КАРТЫ");
        Add(list, "public_browser.any_players", "ANY PLAYERS", "TÜM OYUNCU SAYILARI", "CUALQUIER JUGADOR", "TOUS LES JOUEURS", "ALLE SPIELERZAHLEN", "모든 인원", "ЛЮБОЕ ЧИСЛО ИГРОКОВ");
        Add(list, "public_browser.any_rounds", "ANY ROUNDS", "TÜM TUR SAYILARI", "CUALQUIER RONDA", "TOUS LES TOURS", "ALLE RUNDEN", "모든 라운드", "ЛЮБОЕ ЧИСЛО РАУНДОВ");
        Add(list, "public_browser.any_access", "ANY ACCESS", "TÜM ERİŞİMLER", "CUALQUIER ACCESO", "TOUS LES ACCÈS", "ALLE ZUGÄNGE", "모든 접근", "ЛЮБОЙ ДОСТУП");

        Add(list, "public_browser.loading", "Loading public rooms...", "Açık odalar yükleniyor...", "Cargando salas públicas...", "Chargement des salons publics...", "Öffentliche Räume werden geladen...", "공개 방 불러오는 중...", "Загрузка публичных комнат...");
        Add(list, "public_browser.empty", "No joinable public rooms found.", "Katılınabilir açık oda bulunamadı.", "No se encontraron salas públicas disponibles.", "Aucun salon public disponible.", "Keine beitretbaren öffentlichen Räume gefunden.", "참가 가능한 공개 방이 없습니다.", "Нет доступных публичных комнат.");
        Add(list, "public_browser.found", "{0} public room(s) found.", "{0} açık oda bulundu.", "Se encontraron {0} sala(s) pública(s).", "{0} salon(s) public(s) trouvé(s).", "{0} öffentliche Räume gefunden.", "공개 방 {0}개를 찾았습니다.", "Найдено публичных комнат: {0}.");
        Add(list, "public_browser.creating", "Creating public room...", "Açık oda oluşturuluyor...", "Creando sala pública...", "Création du salon public...", "Öffentlicher Raum wird erstellt...", "공개 방 만드는 중...", "Создание публичной комнаты...");
        Add(list, "public_browser.joining", "Joining room...", "Odaya katılınıyor...", "Uniéndose a la sala...", "Connexion au salon...", "Raum wird betreten...", "방 참가 중...", "Вход в комнату...");
        Add(list, "public_browser.unavailable", "Public room service is unavailable.", "Açık oda hizmeti kullanılamıyor.", "El servicio de salas públicas no está disponible.", "Le service de salons publics est indisponible.", "Der öffentliche Raumdienst ist nicht verfügbar.", "공개 방 서비스를 사용할 수 없습니다.", "Сервис публичных комнат недоступен.");
        Add(list, "public_browser.unknown_host", "Unknown", "Bilinmiyor", "Desconocido", "Inconnu", "Unbekannt", "알 수 없음", "Неизвестно");

        Add(list, "public_browser.password_prompt_title", "PASSWORD REQUIRED", "ŞİFRE GEREKLİ", "CONTRASEÑA REQUERIDA", "MOT DE PASSE REQUIS", "PASSWORT ERFORDERLICH", "비밀번호 필요", "ТРЕБУЕТСЯ ПАРОЛЬ");
        Add(list, "public_browser.password_prompt_body", "Enter the password for {0}.", "{0} için oda şifresini gir.", "Introduce la contraseña para {0}.", "Entrez le mot de passe pour {0}.", "Passwort für {0} eingeben.", "{0}의 비밀번호를 입력하세요.", "Введите пароль для {0}.");
        Add(list, "public_browser.password_placeholder", "ROOM PASSWORD", "ODA ŞİFRESİ", "CONTRASEÑA DE SALA", "MOT DE PASSE DU SALON", "RAUMPASSWORT", "방 비밀번호", "ПАРОЛЬ КОМНАТЫ");

        Add(list, "lobby.game_settings.open", "GAME SETTINGS", "OYUN AYARLARI", "AJUSTES DE PARTIDA", "PARAMÈTRES DE JEU", "SPIELEINSTELLUNGEN", "게임 설정", "НАСТРОЙКИ ИГРЫ");
        Add(list, "lobby.game_settings.more", "MORE SETTINGS", "DAHA FAZLA AYAR", "MÁS AJUSTES", "PLUS DE RÉGLAGES", "WEITERE EINSTELLUNGEN", "추가 설정", "ДОП. НАСТРОЙКИ");
        Add(list, "lobby.game_settings.subtitle", "MAP • RULES • ACCESS", "HARİTA • KURALLAR • ERİŞİM", "MAPA • REGLAS • ACCESO", "CARTE • RÈGLES • ACCÈS", "KARTE • REGELN • ZUGANG", "맵 • 규칙 • 접근", "КАРТА • ПРАВИЛА • ДОСТУП");
        Add(list, "lobby.game_settings.done", "DONE", "TAMAM", "LISTO", "TERMINÉ", "FERTIG", "완료", "ГОТОВО");
        Add(list, "lobby.access.password", "ROOM PASSWORD", "ODA ŞİFRESİ", "CONTRASEÑA DE SALA", "MOT DE PASSE DU SALON", "RAUMPASSWORT", "방 비밀번호", "ПАРОЛЬ КОМНАТЫ");
        Add(list, "lobby.access.password_optional", "PASSWORD (OPTIONAL)", "ŞİFRE (İSTEĞE BAĞLI)", "CONTRASEÑA (OPCIONAL)", "MOT DE PASSE (FACULTATIF)", "PASSWORT (OPTIONAL)", "비밀번호 (선택)", "ПАРОЛЬ (НЕОБЯЗАТЕЛЬНО)");
        Add(list, "lobby.access.password_set", "PASSWORD PROTECTED", "ŞİFRE KORUMALI", "PROTEGIDA CON CONTRASEÑA", "PROTÉGÉ PAR MOT DE PASSE", "PASSWORTGESCHÜTZT", "비밀번호 보호", "ЗАЩИЩЕНО ПАРОЛЕМ");
        Add(list, "lobby.access.open", "NO PASSWORD", "ŞİFRE YOK", "SIN CONTRASEÑA", "SANS MOT DE PASSE", "KEIN PASSWORT", "비밀번호 없음", "БЕЗ ПАРОЛЯ");
        Add(list, "lobby.access.apply", "APPLY PASSWORD", "ŞİFREYİ UYGULA", "APLICAR CONTRASEÑA", "APPLIQUER LE MOT DE PASSE", "PASSWORT ANWENDEN", "비밀번호 적용", "ПРИМЕНИТЬ ПАРОЛЬ");
        Add(list, "lobby.access.saving", "SAVING...", "KAYDEDİLİYOR...", "GUARDANDO...", "ENREGISTREMENT...", "SPEICHERN...", "저장 중...", "СОХРАНЕНИЕ...");
        Add(list, "lobby.access.saved", "ACCESS UPDATED", "ERİŞİM GÜNCELLENDİ", "ACCESO ACTUALIZADO", "ACCÈS MIS À JOUR", "ZUGANG AKTUALISIERT", "접근 업데이트됨", "ДОСТУП ОБНОВЛЁН");

        Add(list, "lobby.error.password_required", "Room password is required.", "Oda şifresi gerekli.", "Se requiere la contraseña de la sala.", "Le mot de passe du salon est requis.", "Raumpasswort erforderlich.", "방 비밀번호가 필요합니다.", "Требуется пароль комнаты.");
        Add(list, "lobby.error.password_incorrect", "Incorrect room password.", "Oda şifresi yanlış.", "Contraseña de sala incorrecta.", "Mot de passe du salon incorrect.", "Falsches Raumpasswort.", "방 비밀번호가 올바르지 않습니다.", "Неверный пароль комнаты.");
        Add(list, "lobby.error.password_length", "Password must be 4-32 characters, or blank to remove it.", "Şifre 4-32 karakter olmalı; kaldırmak için boş bırak.", "La contraseña debe tener 4-32 caracteres o quedar vacía para eliminarla.", "Le mot de passe doit contenir 4 à 32 caractères, ou être vide pour le supprimer.", "Passwort muss 4-32 Zeichen lang sein oder zum Entfernen leer bleiben.", "비밀번호는 4-32자여야 하며 제거하려면 비워 두세요.", "Пароль должен содержать 4-32 символа; оставьте пустым для удаления.");
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
        list.Add(new AtlasBoardLocalizationDatabase.Entry
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
