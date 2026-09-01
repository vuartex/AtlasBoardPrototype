using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AtlasBoardLobbySafetyUXV1Setup
{
    private const string CanvasName = "Canvas_MainMenu";
    private const string LobbyName = "Lobby";
    private const string OverlayName = "PrivateLobbySafetyOverlay";
    private const string LocalizationDatabasePath =
        "Assets/Project/Data/Localization/Localization_Default.asset";

    private static readonly Color Cream =
        new Color32(246, 241, 231, 255);

    private static readonly Color Dark =
        new Color32(61, 62, 66, 255);

    private static readonly Color Green =
        new Color32(139, 170, 16, 255);

    private static readonly Color Muted =
        new Color32(102, 104, 111, 255);

    [MenuItem(
        "Atlas Board/Online/Build Lobby Safety UX v1")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "Exit Play Mode before building Lobby Safety UX.");
            return;
        }

        GameObject canvas =
            GameObject.Find(CanvasName);

        if (canvas == null)
        {
            Debug.LogError(
                "Lobby Safety UX build FAILED: Canvas_MainMenu not found.");
            return;
        }

        Transform lobby =
            FindChildRecursive(
                canvas.transform,
                LobbyName);

        AtlasBoardPrivateLobbyUIController controller =
            canvas.GetComponent<
                AtlasBoardPrivateLobbyUIController>();

        if (lobby == null ||
            controller == null)
        {
            Debug.LogError(
                "Lobby Safety UX build FAILED: Lobby/controller not found.");
            return;
        }

        Transform old =
            FindChildRecursive(
                lobby,
                OverlayName);

        if (old != null)
        {
            Undo.DestroyObjectImmediate(
                old.gameObject);
        }

        TMP_Text textTemplate =
            FindChildRecursive(
                lobby,
                "RoomCodeValue")
            ?.GetComponent<TMP_Text>();

        Image panelTemplate =
            FindChildRecursive(
                lobby,
                "Panel_RoomCode")
            ?.GetComponent<Image>();

        Image buttonTemplate =
            FindChildRecursive(
                lobby,
                "Button_StartMatch")
            ?.GetComponent<Image>();

        TMP_FontAsset font =
            textTemplate != null
                ? textTemplate.font
                : Resources.Load<TMP_FontAsset>(
                    "Fonts & Materials/LiberationSans SDF");

        GameObject overlay =
            CreateStretchImage(
                lobby,
                OverlayName,
                new Color(
                    0f,
                    0f,
                    0f,
                    0.58f));

        Image overlayImage =
            overlay.GetComponent<Image>();

        overlayImage.raycastTarget =
            true;

        GameObject panel =
            CreateImage(
                overlay.transform,
                "Panel_LobbySafety",
                new Vector2(
                    0f,
                    0f),
                new Vector2(
                    700f,
                    390f),
                Cream,
                panelTemplate);

        TMP_Text title =
            CreateText(
                panel.transform,
                "SafetyTitle",
                "NOTICE",
                new Vector2(
                    0f,
                    120f),
                new Vector2(
                    590f,
                    50f),
                29f,
                Dark,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                font);

        TMP_Text body =
            CreateText(
                panel.transform,
                "SafetyBody",
                "Lobby message",
                new Vector2(
                    0f,
                    38f),
                new Vector2(
                    580f,
                    105f),
                19f,
                Dark,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                font);

        TMP_Text countdown =
            CreateText(
                panel.transform,
                "SafetyCountdown",
                string.Empty,
                new Vector2(
                    0f,
                    -42f),
                new Vector2(
                    250f,
                    95f),
                64f,
                Dark,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                font);

        GameObject primaryObject =
            CreateButton(
                panel.transform,
                "Button_SafetyPrimary",
                "OK",
                new Vector2(
                    -105f,
                    -125f),
                new Vector2(
                    190f,
                    58f),
                Green,
                buttonTemplate,
                font,
                out TMP_Text primaryText);

        GameObject secondaryObject =
            CreateButton(
                panel.transform,
                "Button_SafetySecondary",
                "CANCEL",
                new Vector2(
                    105f,
                    -125f),
                new Vector2(
                    190f,
                    58f),
                Muted,
                buttonTemplate,
                font,
                out TMP_Text secondaryText);

        controller.EditorConfigureSafetyUX(
            overlay,
            title,
            body,
            countdown,
            primaryObject.GetComponent<Button>(),
            primaryText,
            secondaryObject.GetComponent<Button>(),
            secondaryText);

        // Remove the unsupported eye emoji from the existing room-code button.
        // Runtime also writes text-only SHOW/HIDE, so this fixes both the
        // serialized scene default and the live state.
        TMP_Text showHideText =
            FindChildRecursive(
                lobby,
                "Button_ShowHideCode")
            ?.GetComponentInChildren<TMP_Text>(
                true);

        if (showHideText != null)
        {
            showHideText.text =
                "SHOW";
            EditorUtility.SetDirty(
                showHideText);
        }

        overlay.SetActive(false);
        overlay.transform.SetAsLastSibling();

        SeedLocalizationEntries();

        EditorUtility.SetDirty(
            controller);

        if (canvas.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                canvas.scene);
        }

        Selection.activeGameObject =
            overlay;

        Debug.Log(
            "AtlasBoard Lobby Safety UX v1 built. " +
            "Added reusable warning/kick/countdown modal and text-only SHOW/HIDE.");
    }

    [MenuItem(
        "Atlas Board/Online/Validate Lobby Safety UX v1")]
    public static void Validate()
    {
        GameObject canvas =
            GameObject.Find(CanvasName);

        if (canvas == null)
        {
            Debug.LogError(
                "Lobby Safety UX validation FAILED: Canvas_MainMenu missing.");
            return;
        }

        string[] names =
        {
            OverlayName,
            "Panel_LobbySafety",
            "SafetyTitle",
            "SafetyBody",
            "SafetyCountdown",
            "Button_SafetyPrimary",
            "Button_SafetySecondary"
        };

        List<string> missing =
            new List<string>();

        foreach (string name in names)
        {
            if (FindChildRecursive(
                    canvas.transform,
                    name) == null)
            {
                missing.Add(name);
            }
        }

        if (canvas.GetComponent<
                AtlasBoardPrivateLobbyUIController>() == null)
        {
            missing.Add(
                nameof(
                    AtlasBoardPrivateLobbyUIController));
        }

        if (missing.Count > 0)
        {
            Debug.LogError(
                "Lobby Safety UX v1 static validation FAILED. Missing: " +
                string.Join(", ", missing));
            return;
        }

        Debug.Log(
            "AtlasBoard Lobby Safety UX v1 static validation PASSED. " +
            "This proves only scene/component wiring; Play Mode + emulator " +
            "behavior still requires runtime validation.");
    }

    private static void SeedLocalizationEntries()
    {
        AtlasBoardLocalizationDatabase database =
            AssetDatabase.LoadAssetAtPath<
                AtlasBoardLocalizationDatabase>(
                    LocalizationDatabasePath);

        if (database == null)
        {
            Debug.LogWarning(
                "Localization_Default.asset not found. Safety UX will use English fallbacks.");
            return;
        }

        List<AtlasBoardLocalizationDatabase.Entry> entries =
            database.Entries != null
                ? database.Entries
                    .Where(
                        item => item != null)
                    .ToList()
                : new List<
                    AtlasBoardLocalizationDatabase.Entry>();

        AtlasBoardLocalizationDatabase.Entry[] additions =
        {
            E(
                "lobby.online.player_count_blocked_title",
                "PLAYER COUNT CANNOT BE REDUCED",
                "OYUNCU SAYISI AZALTILAMAZ",
                "NO SE PUEDE REDUCIR EL NÚMERO DE JUGADORES",
                "IMPOSSIBLE DE RÉDUIRE LE NOMBRE DE JOUEURS",
                "SPIELERANZAHL KANN NICHT REDUZIERT WERDEN",
                "플레이어 수를 줄일 수 없음",
                "НЕЛЬЗЯ УМЕНЬШИТЬ ЧИСЛО ИГРОКОВ"),
            E(
                "lobby.online.player_count_blocked_body",
                "An online player is using a slot that would be removed. Remove that player from the lobby first.",
                "Kaldırılacak koltuklardan birinde online oyuncu var. Önce o oyuncuyu lobiden çıkar.",
                "Un jugador online ocupa una plaza que se eliminaría. Elimínalo primero de la sala.",
                "Un joueur en ligne occupe une place qui serait supprimée. Retirez-le d'abord du salon.",
                "Ein Online-Spieler belegt einen Platz, der entfernt würde. Entferne diesen Spieler zuerst aus der Lobby.",
                "삭제될 슬롯에 온라인 플레이어가 있습니다. 먼저 해당 플레이어를 로비에서 제거하세요.",
                "Онлайн-игрок занимает удаляемое место. Сначала удалите этого игрока из лобби."),
            E(
                "lobby.online.kick_title",
                "REMOVE PLAYER?",
                "OYUNCU ÇIKARILSIN MI?",
                "¿ELIMINAR JUGADOR?",
                "RETIRER LE JOUEUR ?",
                "SPIELER ENTFERNEN?",
                "플레이어를 내보낼까요?",
                "УДАЛИТЬ ИГРОКА?"),
            E(
                "lobby.online.kick_body",
                "Remove {0} from this lobby?",
                "{0} bu lobiden çıkarılsın mı?",
                "¿Eliminar a {0} de esta sala?",
                "Retirer {0} de ce salon ?",
                "{0} aus dieser Lobby entfernen?",
                "{0}님을 이 로비에서 내보낼까요?",
                "Удалить {0} из этого лобби?"),
            E(
                "lobby.online.remove_player",
                "REMOVE",
                "ÇIKAR",
                "ELIMINAR",
                "RETIRER",
                "ENTFERNEN",
                "내보내기",
                "УДАЛИТЬ"),
            E(
                "lobby.online.removing_player",
                "REMOVING PLAYER...",
                "OYUNCU ÇIKARILIYOR...",
                "ELIMINANDO JUGADOR...",
                "RETRAIT DU JOUEUR...",
                "SPIELER WIRD ENTFERNT...",
                "플레이어 내보내는 중...",
                "ИГРОК УДАЛЯЕТСЯ..."),
            E(
                "lobby.online.kicked_title",
                "REMOVED FROM LOBBY",
                "LOBİDEN ÇIKARILDIN",
                "ELIMINADO DE LA SALA",
                "RETIRÉ DU SALON",
                "AUS DER LOBBY ENTFERNT",
                "로비에서 퇴장됨",
                "ВЫ УДАЛЕНЫ ИЗ ЛОББИ"),
            E(
                "lobby.online.kicked_body",
                "The host removed you from this lobby.",
                "Lobi kurucusu seni bu lobiden çıkardı.",
                "El anfitrión te eliminó de esta sala.",
                "L'hôte vous a retiré de ce salon.",
                "Der Host hat dich aus dieser Lobby entfernt.",
                "호스트가 회원님을 이 로비에서 내보냈습니다.",
                "Хост удалил вас из этого лобби."),
            E(
                "lobby.online.host_can_start_with_open",
                "HOST CAN START • OPEN SEATS BECOME BOTS",
                "KURUCU BAŞLATABİLİR • BOŞ KOLTUKLAR BOT OLUR",
                "EL ANFITRIÓN PUEDE INICIAR • LAS PLAZAS LIBRES SERÁN BOTS",
                "L'HÔTE PEUT DÉMARRER • LES PLACES LIBRES DEVIENNENT DES BOTS",
                "HOST KANN STARTEN • FREIE PLÄTZE WERDEN BOTS",
                "호스트 시작 가능 • 빈 슬롯은 봇으로 전환",
                "ХОСТ МОЖЕТ НАЧАТЬ • СВОБОДНЫЕ МЕСТА СТАНУТ БОТАМИ"),
            E(
                "lobby.online.match_starting_title",
                "MATCH STARTING",
                "MAÇ BAŞLIYOR",
                "LA PARTIDA COMIENZA",
                "LA PARTIE COMMENCE",
                "MATCH STARTET",
                "게임 시작",
                "МАТЧ НАЧИНАЕТСЯ"),
            E(
                "lobby.online.match_starting_body",
                "Lobby locked. Remaining open seats are filled by bots.",
                "Lobi kilitlendi. Kalan boş koltuklar botlarla dolduruldu.",
                "La sala está bloqueada. Las plazas abiertas restantes se llenan con bots.",
                "Le salon est verrouillé. Les places libres restantes sont remplies par des bots.",
                "Die Lobby ist gesperrt. Verbleibende freie Plätze werden mit Bots gefüllt.",
                "로비가 잠겼습니다. 남은 빈 슬롯은 봇으로 채워집니다.",
                "Лобби заблокировано. Оставшиеся свободные места заполняются ботами."),
            E(
                "lobby.error.invalid_room_code",
                "Invalid room code.",
                "Geçersiz oda kodu.",
                "Código de sala no válido.",
                "Code de salon invalide.",
                "Ungültiger Raumcode.",
                "잘못된 방 코드입니다.",
                "Неверный код комнаты."),
            E(
                "lobby.online.authoritative_start_ready",
                "START CONFIRMED",
                "BAŞLANGIÇ ONAYLANDI",
                "INICIO CONFIRMADO",
                "DÉMARRAGE CONFIRMÉ",
                "START BESTÄTIGT",
                "시작 확인됨",
                "СТАРТ ПОДТВЕРЖДЁН"),
            E(
                "lobby.error.kicked",
                "You were removed from this lobby.",
                "Bu lobiden çıkarıldın.",
                "Fuiste eliminado de esta sala.",
                "Vous avez été retiré de ce salon.",
                "Du wurdest aus dieser Lobby entfernt.",
                "이 로비에서 퇴장되었습니다.",
                "Вас удалили из этого лобби."),
            E(
                "lobby.error.remote_player_not_present",
                "That online player is no longer in the lobby.",
                "Bu online oyuncu artık lobide değil.",
                "Ese jugador online ya no está en la sala.",
                "Ce joueur en ligne n'est plus dans le salon.",
                "Dieser Online-Spieler ist nicht mehr in der Lobby.",
                "해당 온라인 플레이어는 더 이상 로비에 없습니다.",
                "Этого онлайн-игрока больше нет в лобби.")
        };

        HashSet<string> keys =
            new HashSet<string>(
                additions.Select(
                    item => item.key),
                StringComparer.OrdinalIgnoreCase);

        entries.RemoveAll(
            item =>
                item != null &&
                keys.Contains(item.key));

        entries.AddRange(
            additions);

        database.EditorReplaceEntries(
            entries);

        EditorUtility.SetDirty(
            database);

        AssetDatabase.SaveAssets();
    }

    private static AtlasBoardLocalizationDatabase.Entry E(
        string key,
        string en,
        string tr,
        string es,
        string fr,
        string de,
        string ko,
        string ru)
    {
        return new AtlasBoardLocalizationDatabase.Entry
        {
            key = key,
            en = en,
            tr = tr,
            es = es,
            fr = fr,
            de = de,
            ko = ko,
            ru = ru
        };
    }

    private static GameObject CreateStretchImage(
        Transform parent,
        string name,
        Color color)
    {
        GameObject gameObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        Undo.RegisterCreatedObjectUndo(
            gameObject,
            $"Create {name}");

        gameObject.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            gameObject.GetComponent<RectTransform>();

        rect.anchorMin =
            Vector2.zero;
        rect.anchorMax =
            Vector2.one;
        rect.offsetMin =
            Vector2.zero;
        rect.offsetMax =
            Vector2.zero;

        Image image =
            gameObject.GetComponent<Image>();

        image.color =
            color;

        return gameObject;
    }

    private static GameObject CreateImage(
        Transform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color,
        Image template)
    {
        GameObject gameObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

        Undo.RegisterCreatedObjectUndo(
            gameObject,
            $"Create {name}");

        gameObject.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            gameObject.GetComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(
                0.5f,
                0.5f);
        rect.anchorMax =
            rect.anchorMin;
        rect.pivot =
            new Vector2(
                0.5f,
                0.5f);
        rect.anchoredPosition =
            anchoredPosition;
        rect.sizeDelta =
            size;

        Image image =
            gameObject.GetComponent<Image>();

        image.color =
            color;

        if (template != null)
        {
            image.sprite =
                template.sprite;
            image.type =
                template.type;
        }

        return gameObject;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        Color color,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        TMP_FontAsset font)
    {
        GameObject gameObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

        Undo.RegisterCreatedObjectUndo(
            gameObject,
            $"Create {name}");

        gameObject.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            gameObject.GetComponent<RectTransform>();

        rect.anchorMin =
            new Vector2(
                0.5f,
                0.5f);
        rect.anchorMax =
            rect.anchorMin;
        rect.pivot =
            new Vector2(
                0.5f,
                0.5f);
        rect.anchoredPosition =
            anchoredPosition;
        rect.sizeDelta =
            size;

        TMP_Text text =
            gameObject.GetComponent<TMP_Text>();

        text.text =
            value;
        text.font =
            font;
        text.fontSize =
            fontSize;
        text.color =
            color;
        text.fontStyle =
            fontStyle;
        text.alignment =
            alignment;
        text.textWrappingMode =
            TextWrappingModes.Normal;
        text.raycastTarget =
            false;

        return text;
    }

    private static GameObject CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color,
        Image template,
        TMP_FontAsset font,
        out TMP_Text text)
    {
        GameObject gameObject =
            CreateImage(
                parent,
                name,
                anchoredPosition,
                size,
                color,
                template);

        Button button =
            gameObject.AddComponent<Button>();

        button.targetGraphic =
            gameObject.GetComponent<Image>();

        text =
            CreateText(
                gameObject.transform,
                "Label",
                label,
                Vector2.zero,
                size - new Vector2(
                    18f,
                    10f),
                20f,
                Color.white,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                font);

        return gameObject;
    }

    private static Transform FindChildRecursive(
        Transform parent,
        string name)
    {
        if (parent == null)
        {
            return null;
        }

        if (string.Equals(
                parent.name,
                name,
                StringComparison.Ordinal))
        {
            return parent;
        }

        for (int i = 0;
             i < parent.childCount;
             i++)
        {
            Transform found =
                FindChildRecursive(
                    parent.GetChild(i),
                    name);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
