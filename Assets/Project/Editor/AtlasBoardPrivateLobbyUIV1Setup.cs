#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AtlasBoardPrivateLobbyUIV1Setup
{
    private const string CanvasName =
        "Canvas_MainMenu";

    private const string LobbyName =
        "Lobby";

    private const string RootName =
        "PrivateOnlineRoot";

    private const string LocalizationDatabasePath =
        "Assets/Project/Data/Localization/Localization_Default.asset";

    private static readonly Color Cream =
        new Color32(246, 241, 231, 255);

    private static readonly Color Dark =
        new Color32(61, 62, 66, 255);

    private static readonly Color Blue =
        new Color32(28, 157, 211, 255);

    private static readonly Color Green =
        new Color32(139, 170, 16, 255);

    private static readonly Color Orange =
        new Color32(248, 175, 0, 255);

    private static readonly Color Muted =
        new Color32(102, 104, 111, 255);

    [MenuItem(
        "Atlas Board/Online/Build Visible Private Lobby UI v1")]
    public static void BuildOrRefresh()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "Exit Play Mode before building the visible Private Lobby UI.");

            return;
        }

        GameObject canvas =
            FindSceneObject(
                CanvasName);

        if (canvas == null)
        {
            Debug.LogError(
                "Canvas_MainMenu was not found. Build the existing Main Menu + Lobby first.");

            return;
        }

        GameObject mainMenu =
            FindChildRecursive(
                canvas.transform,
                "MainMenu")
            ?.gameObject;

        GameObject lobby =
            FindChildRecursive(
                canvas.transform,
                LobbyName)
            ?.gameObject;

        if (mainMenu == null)
        {
            Debug.LogError(
                "MainMenu was not found under Canvas_MainMenu.");
            return;
        }

        if (lobby == null)
        {
            Debug.LogError(
                "Lobby was not found under Canvas_MainMenu.");

            return;
        }

        Transform oldEntry =
            FindChildRecursive(
                mainMenu.transform,
                "PrivateRoomEntryOverlay");

        if (oldEntry != null)
        {
            Undo.DestroyObjectImmediate(
                oldEntry.gameObject);
        }

        Transform oldRoot =
            FindChildRecursive(
                lobby.transform,
                RootName);

        if (oldRoot != null)
        {
            Undo.DestroyObjectImmediate(
                oldRoot.gameObject);
        }

        AtlasBoardPrivateLobbyUIController oldController =
            canvas.GetComponent<
                AtlasBoardPrivateLobbyUIController>();

        if (oldController != null)
        {
            Undo.DestroyObjectImmediate(
                oldController);
        }

        TMP_FontAsset font =
            lobby.GetComponentInChildren<
                TMP_Text>(
                    true)
            ?.font;

        Image panelTemplate =
            FindChildRecursive(
                lobby.transform,
                "Panel_GameSettings")
            ?.GetComponent<Image>();

        Image buttonTemplate =
            FindChildRecursive(
                lobby.transform,
                "Button_StartMatch")
            ?.GetComponent<Image>();

        GameObject root =
            CreateStretchRoot(
                lobby.transform,
                RootName);

        GameObject entryOverlay =
            CreateImage(
                mainMenu.transform,
                "PrivateRoomEntryOverlay",
                new Vector2(
                    960f,
                    540f),
                new Vector2(
                    1920f,
                    1080f),
                new Color(
                    0f,
                    0f,
                    0f,
                    0.48f),
                null);

        GameObject entryPanel =
            CreateImage(
                entryOverlay.transform,
                "Panel_PrivateRoomEntry",
                new Vector2(
                    960f,
                    540f),
                new Vector2(
                    780f,
                    390f),
                Cream,
                panelTemplate);

        TMP_Text entryTitle =
            CreateLocalizedText(
                entryPanel.transform,
                "PrivateRoomTitle",
                "lobby.online.private_room",
                "PRIVATE ROOM",
                new Vector2(
                    0f,
                    125f),
                new Vector2(
                    580f,
                    42f),
                27f,
                Dark,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                font);

        TMP_Text entryHelp =
            CreateLocalizedText(
                entryPanel.transform,
                "PrivateRoomHelp",
                "lobby.online.create_or_join",
                "Create a room or enter a 6-digit code.",
                new Vector2(
                    0f,
                    78f),
                new Vector2(
                    590f,
                    34f),
                16f,
                Muted,
                FontStyles.Normal,
                TextAlignmentOptions.Center,
                font);

        GameObject createButtonObject =
            CreateButton(
                entryPanel.transform,
                "Button_CreateRoom",
                "CREATE ROOM",
                new Vector2(
                    -190f,
                    5f),
                new Vector2(
                    250f,
                    62f),
                Green,
                buttonTemplate,
                font,
                out TMP_Text createButtonText);

        AddLocalizedBinding(
            createButtonText,
            "lobby.online.create_room");

        CreateLocalizedText(
            entryPanel.transform,
            "JoinByCodeLabel",
            "lobby.online.join_by_code",
            "JOIN BY CODE",
            new Vector2(
                170f,
                28f),
            new Vector2(
                270f,
                30f),
            16f,
            Dark,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            font);

        TMP_InputField joinInput =
            CreateInputField(
                entryPanel.transform,
                "Input_JoinCode",
                new Vector2(
                    115f,
                    -5f),
                new Vector2(
                    185f,
                    54f),
                "ENTER 6-DIGIT CODE",
                font);

        GameObject joinButtonObject =
            CreateButton(
                entryPanel.transform,
                "Button_JoinRoom",
                "JOIN ROOM",
                new Vector2(
                    280f,
                    -5f),
                new Vector2(
                    105f,
                    54f),
                Blue,
                buttonTemplate,
                font,
                out TMP_Text joinButtonText);

        AddLocalizedBinding(
            joinButtonText,
            "lobby.online.join_room");

        GameObject cancelButtonObject =
            CreateButton(
                entryPanel.transform,
                "Button_CancelPrivateRoomEntry",
                "CANCEL",
                new Vector2(
                    0f,
                    -65f),
                new Vector2(
                    180f,
                    48f),
                Muted,
                buttonTemplate,
                font,
                out TMP_Text cancelButtonText);

        AddLocalizedBinding(
            cancelButtonText,
            "common.cancel");

        TMP_Text entryStatus =
            CreateText(
                entryPanel.transform,
                "EntryStatus",
                "LOCAL UI PREVIEW • BACKEND LINK IN 3D.3",
                new Vector2(
                    0f,
                    -115f),
                new Vector2(
                    590f,
                    36f),
                14f,
                Orange,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                font);

        GameObject roomPanel =
            CreateImage(
                root.transform,
                "Panel_RoomCode",
                new Vector2(
                    1695f,
                    78f),
                new Vector2(
                    420f,
                    92f),
                Cream,
                panelTemplate);

        CreateLocalizedText(
            roomPanel.transform,
            "RoomCodeLabel",
            "lobby.online.room_code",
            "ROOM CODE",
            new Vector2(
                -152f,
                24f),
            new Vector2(
                105f,
                22f),
            12f,
            Muted,
            FontStyles.Bold,
            TextAlignmentOptions.Left,
            font);

        TMP_Text roomCodeText =
            CreateText(
                roomPanel.transform,
                "RoomCodeValue",
                "••••••",
                new Vector2(
                    -145f,
                    -14f),
                new Vector2(
                    122f,
                    38f),
                24f,
                Dark,
                FontStyles.Bold,
                TextAlignmentOptions.Left,
                font);

        GameObject eyeButtonObject =
            CreateButton(
                roomPanel.transform,
                "Button_ShowHideCode",
                "👁 SHOW",
                new Vector2(
                    36f,
                    -12f),
                new Vector2(
                    112f,
                    42f),
                Blue,
                buttonTemplate,
                font,
                out TMP_Text eyeButtonText);

        GameObject copyButtonObject =
            CreateButton(
                roomPanel.transform,
                "Button_CopyCode",
                "COPY",
                new Vector2(
                    151f,
                    -12f),
                new Vector2(
                    96f,
                    42f),
                Orange,
                buttonTemplate,
                font,
                out TMP_Text copyButtonText);

        TMP_Text revisionText =
            CreateText(
                roomPanel.transform,
                "SettingsRevision",
                "SETTINGS REV 1",
                new Vector2(
                    -38f,
                    24f),
                new Vector2(
                    125f,
                    20f),
                10f,
                Muted,
                FontStyles.Bold,
                TextAlignmentOptions.Left,
                font);

        TMP_Text roomStateText =
            CreateText(
                roomPanel.transform,
                "RoomState",
                "LOCAL UI PREVIEW",
                new Vector2(
                    105f,
                    24f),
                new Vector2(
                    175f,
                    20f),
                9f,
                Orange,
                FontStyles.Bold,
                TextAlignmentOptions.Right,
                font);

        GameObject readyButtonObject =
            CreateButton(
                root.transform,
                "Button_PrivateReady",
                "READY",
                new Vector2(
                    1590f,
                    1005f),
                new Vector2(
                    430f,
                    90f),
                Green,
                buttonTemplate,
                font,
                out TMP_Text readyButtonText);

        SetScreenRect(
            readyButtonObject.GetComponent<
                RectTransform>(),
            new Vector2(
                1590f,
                1005f),
            new Vector2(
                430f,
                90f));

        Button privateTableButton =
            FindChildRecursive(
                canvas.transform,
                "PrivateTableCard")
            ?.GetComponent<Button>();

        Button playButton =
            FindChildRecursive(
                canvas.transform,
                "PlayCard")
            ?.GetComponent<Button>();

        Button backButton =
            FindChildRecursive(
                lobby.transform,
                "Button_Back")
            ?.GetComponent<Button>();

        GameObject startButton =
            FindChildRecursive(
                lobby.transform,
                "Button_StartMatch")
            ?.gameObject;

        TMP_Dropdown mapDropdown =
            FindChildRecursive(
                lobby.transform,
                "Dropdown_Map")
            ?.GetComponent<TMP_Dropdown>();

        TMP_Dropdown playerCountDropdown =
            FindChildRecursive(
                lobby.transform,
                "Dropdown_PlayerCount")
            ?.GetComponent<TMP_Dropdown>();

        TMP_Dropdown roundDropdown =
            FindChildRecursive(
                lobby.transform,
                "Dropdown_RoundLimit")
            ?.GetComponent<TMP_Dropdown>();

        TMP_Dropdown themeDropdown =
            FindChildRecursive(
                lobby.transform,
                "Dropdown_EnvironmentTheme")
            ?.GetComponent<TMP_Dropdown>();

        Toggle[] settingsToggles =
        {
            FindChildRecursive(
                lobby.transform,
                "Toggle_BalancedDevelopment")
            ?.GetComponent<Toggle>(),
            FindChildRecursive(
                lobby.transform,
                "Toggle_DoublesEnabled")
            ?.GetComponent<Toggle>(),
            FindChildRecursive(
                lobby.transform,
                "Toggle_TripleDoublePenalty")
            ?.GetComponent<Toggle>()
        };

        TMP_Dropdown[] settingsDropdowns =
        {
            mapDropdown,
            playerCountDropdown,
            roundDropdown,
            themeDropdown
        };

        GameObject[] playerRows =
            new GameObject[4];

        GameObject[] legacyLabels =
            new GameObject[4];

        GameObject[] legacyStatuses =
            new GameObject[4];

        GameObject[] legacyDropdowns =
            new GameObject[4];

        TMP_Text[] privateNames =
            new TMP_Text[4];

        TMP_Text[] privateStatuses =
            new TMP_Text[4];

        Button[] privateAddButtons =
            new Button[4];
        GameObject[] privateChoicePanels =
            new GameObject[4];
        Button[] privateLocalButtons =
            new Button[4];
        Button[] privateBotButtons =
            new Button[4];
        Button[] privateRemoveButtons =
            new Button[4];

        for (int i = 0;
             i < 4;
             i++)
        {
            int slot = i + 1;

            Transform row =
                FindChildRecursive(
                    lobby.transform,
                    $"PlayerRow_P{slot}");

            if (row == null)
            {
                continue;
            }

            playerRows[i] =
                row.gameObject;

            Transform oldPrivateName =
                FindChildRecursive(
                    row,
                    "PrivateSeatName");

            if (oldPrivateName != null)
            {
                Undo.DestroyObjectImmediate(
                    oldPrivateName.gameObject);
            }

            Transform oldPrivateStatus =
                FindChildRecursive(
                    row,
                    "PrivateSeatStatus");

            if (oldPrivateStatus != null)
            {
                Undo.DestroyObjectImmediate(
                    oldPrivateStatus.gameObject);
            }

            string[] oldPrivateActionNames =
            {
                "PrivateSeatSelector",
                "PrivateSeatAddButton",
                "PrivateSeatChoicePanel",
                "PrivateSeatRemoveButton"
            };

            foreach (string oldActionName in oldPrivateActionNames)
            {
                Transform oldAction =
                    FindChildRecursive(
                        row,
                        oldActionName);

                if (oldAction != null)
                {
                    Undo.DestroyObjectImmediate(
                        oldAction.gameObject);
                }
            }

            legacyLabels[i] =
                FindChildRecursive(
                    row,
                    "PlayerLabel")
                ?.gameObject;

            legacyStatuses[i] =
                FindChildRecursive(
                    row,
                    "Status")
                ?.gameObject;

            legacyDropdowns[i] =
                FindChildRecursive(
                    row,
                    $"Dropdown_P{slot}_Type")
                ?.gameObject;

            if (i > 0)
            {
                GameObject addButtonObject =
                    CreateButton(
                        row,
                        "PrivateSeatAddButton",
                        "+",
                        Vector2.zero,
                        new Vector2(
                            52f,
                            52f),
                        Blue,
                        buttonTemplate,
                        font,
                        out TMP_Text addLabel);

                RectTransform addRect =
                    addButtonObject.GetComponent<RectTransform>();

                addRect.anchorMin =
                    new Vector2(
                        1f,
                        0.5f);
                addRect.anchorMax =
                    new Vector2(
                        1f,
                        0.5f);
                addRect.pivot =
                    new Vector2(
                        1f,
                        0.5f);
                addRect.anchoredPosition =
                    new Vector2(
                        -18f,
                        0f);

                addLabel.fontSize =
                    30f;

                privateAddButtons[i] =
                    addButtonObject.GetComponent<Button>();

                GameObject removeButtonObject =
                    CreateButton(
                        row,
                        "PrivateSeatRemoveButton",
                        "×",
                        Vector2.zero,
                        new Vector2(
                            52f,
                            52f),
                        Muted,
                        buttonTemplate,
                        font,
                        out TMP_Text removeLabel);

                RectTransform removeRect =
                    removeButtonObject.GetComponent<RectTransform>();

                removeRect.anchorMin =
                    new Vector2(
                        1f,
                        0.5f);
                removeRect.anchorMax =
                    new Vector2(
                        1f,
                        0.5f);
                removeRect.pivot =
                    new Vector2(
                        1f,
                        0.5f);
                removeRect.anchoredPosition =
                    new Vector2(
                        -18f,
                        0f);

                removeLabel.fontSize =
                    28f;

                privateRemoveButtons[i] =
                    removeButtonObject.GetComponent<Button>();

                GameObject choicePanel =
                    CreateImage(
                        row,
                        "PrivateSeatChoicePanel",
                        new Vector2(
                            960f,
                            540f),
                        new Vector2(
                            210f,
                            112f),
                        Cream,
                        panelTemplate);

                RectTransform choiceRect =
                    choicePanel.GetComponent<RectTransform>();

                choiceRect.anchorMin =
                    new Vector2(
                        1f,
                        0.5f);
                choiceRect.anchorMax =
                    new Vector2(
                        1f,
                        0.5f);
                choiceRect.pivot =
                    new Vector2(
                        1f,
                        0.5f);
                choiceRect.anchoredPosition =
                    new Vector2(
                        -12f,
                        0f);
                choiceRect.sizeDelta =
                    new Vector2(
                        210f,
                        112f);

                Canvas choiceCanvas =
                    choicePanel.AddComponent<Canvas>();
                choiceCanvas.overrideSorting =
                    true;
                choiceCanvas.sortingOrder =
                    200 + i;

                choicePanel.AddComponent<GraphicRaycaster>();

                GameObject localButtonObject =
                    CreateButton(
                        choicePanel.transform,
                        "Button_AddLocalPlayer",
                        "ADD LOCAL PLAYER",
                        new Vector2(
                            0f,
                            27f),
                        new Vector2(
                            184f,
                            44f),
                        Green,
                        buttonTemplate,
                        font,
                        out TMP_Text localLabel);

                AddLocalizedBinding(
                    localLabel,
                    "lobby.online.add_local_player");

                GameObject botButtonObject =
                    CreateButton(
                        choicePanel.transform,
                        "Button_AddBot",
                        "ADD BOT",
                        new Vector2(
                            0f,
                            -27f),
                        new Vector2(
                            184f,
                            44f),
                        Orange,
                        buttonTemplate,
                        font,
                        out TMP_Text botLabel);

                AddLocalizedBinding(
                    botLabel,
                    "lobby.online.add_bot");

                privateChoicePanels[i] =
                    choicePanel;
                privateLocalButtons[i] =
                    localButtonObject.GetComponent<Button>();
                privateBotButtons[i] =
                    botButtonObject.GetComponent<Button>();

                choicePanel.SetActive(false);
                removeButtonObject.SetActive(false);
                addButtonObject.SetActive(false);
            }

            privateNames[i] =
                CreateText(
                    row,
                    "PrivateSeatName",
                    $"PLAYER {slot}",
                    new Vector2(
                        10f,
                        -10f),
                    new Vector2(
                        285f,
                        32f),
                    20f,
                    Dark,
                    FontStyles.Bold,
                    TextAlignmentOptions.Left,
                    font);

            privateStatuses[i] =
                CreateText(
                    row,
                    "PrivateSeatStatus",
                    "WAITING",
                    new Vector2(
                        10f,
                        17f),
                    new Vector2(
                        285f,
                        24f),
                    13f,
                    Muted,
                    FontStyles.Normal,
                    TextAlignmentOptions.Left,
                    font);

            privateNames[i].gameObject.SetActive(
                false);

            privateStatuses[i].gameObject.SetActive(
                false);
        }

        AtlasBoardPrivateLobbyUIController controller =
            Undo.AddComponent<
                AtlasBoardPrivateLobbyUIController>(
                    canvas);

        controller.EditorConfigure(
            mainMenu,
            lobby,
            root,
            entryOverlay,
            roomPanel,
            startButton,
            createButtonObject.GetComponent<Button>(),
            joinInput,
            joinButtonObject.GetComponent<Button>(),
            cancelButtonObject.GetComponent<Button>(),
            entryStatus,
            roomCodeText,
            roomStateText,
            revisionText,
            eyeButtonObject.GetComponent<Button>(),
            eyeButtonText,
            copyButtonObject.GetComponent<Button>(),
            copyButtonText,
            readyButtonObject.GetComponent<Button>(),
            readyButtonText,
            playerCountDropdown,
            settingsDropdowns,
            settingsToggles,
            playerRows,
            legacyLabels,
            legacyStatuses,
            legacyDropdowns,
            privateNames,
            privateStatuses,
            privateAddButtons,
            privateChoicePanels,
            privateLocalButtons,
            privateBotButtons,
            privateRemoveButtons);

        root.SetActive(
            false);

        entryOverlay.SetActive(
            false);

        roomPanel.SetActive(
            false);

        readyButtonObject.SetActive(
            false);

        SeedLocalizationEntries();

        EditorUtility.SetDirty(
            controller);

        if (canvas.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                canvas.scene);
        }

        Selection.activeGameObject =
            root;

        Debug.Log(
            "AtlasBoard Visible Private Lobby UI v1 built in-place. " +
            "PRIVATE TABLE now opens Create Room / Join Code BEFORE the Lobby, " +
            "masked 6-digit room code with show/hide + copy, private seat presentation, " +
            "plus-button slot menus for open-online/local/bot seats, host settings lock, settings revision invalidation, and Ready UI preview. " +
            "This is Phase 3D.2 UI/local interaction only; Firebase runtime wiring remains Phase 3D.3.");
    }

    [MenuItem(
        "Atlas Board/Online/Validate Visible Private Lobby UI v1")]
    public static void Validate()
    {
        GameObject canvas =
            FindSceneObject(
                CanvasName);

        if (canvas == null)
        {
            Debug.LogError(
                "Visible Private Lobby UI validation FAILED: Canvas_MainMenu not found.");

            return;
        }

        string[] requiredObjects =
        {
            RootName,
            "PrivateRoomEntryOverlay",
            "Panel_PrivateRoomEntry",
            "Button_CreateRoom",
            "Input_JoinCode",
            "Button_JoinRoom",
            "Button_CancelPrivateRoomEntry",
            "Panel_RoomCode",
            "RoomCodeValue",
            "Button_ShowHideCode",
            "Button_CopyCode",
            "Button_PrivateReady",
            "PrivateSeatName",
            "PrivateSeatStatus",
            "PrivateSeatAddButton",
            "PrivateSeatChoicePanel",
            "Button_AddLocalPlayer",
            "Button_AddBot",
            "PrivateSeatRemoveButton"
        };

        List<string> missing =
            new List<string>();

        foreach (string required in requiredObjects)
        {
            if (FindChildRecursive(
                    canvas.transform,
                    required) == null)
            {
                missing.Add(
                    required);
            }
        }

        AtlasBoardPrivateLobbyUIController controller =
            canvas.GetComponent<
                AtlasBoardPrivateLobbyUIController>();

        if (controller == null)
        {
            missing.Add(
                nameof(
                    AtlasBoardPrivateLobbyUIController));
        }

        if (missing.Count > 0)
        {
            Debug.LogError(
                "Visible Private Lobby UI v1 static validation FAILED. Missing: " +
                string.Join(
                    ", ",
                    missing));

            return;
        }

        Debug.Log(
            "AtlasBoard Visible Private Lobby UI v1 static validation PASSED. " +
            "This only proves the visible UI hierarchy/controller wiring exists. " +
            "You must still verify in Play Mode: PRIVATE TABLE chooser on Main Menu -> Create/Join -> Lobby, " +
            "masked code, eye show/hide, clipboard copy, Open/Local/Bot slot selection, host START gate, guest Ready, and settings invalidation. " +
            "Firebase create/join is intentionally not claimed until Phase 3D.3.");
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
                "Localization_Default.asset was not found. Private Lobby UI will use English fallbacks until localization is rebuilt.");

            return;
        }

        List<AtlasBoardLocalizationDatabase.Entry> entries =
            database.Entries != null
                ? database.Entries
                    .Where(
                        entry =>
                            entry != null)
                    .ToList()
                : new List<
                    AtlasBoardLocalizationDatabase.Entry>();

        AtlasBoardLocalizationDatabase.Entry[] additions =
        {
            E("lobby.online.private_room",
                "PRIVATE ROOM", "ÖZEL ODA", "SALA PRIVADA", "SALON PRIVÉ", "PRIVATER RAUM", "비공개 방", "ПРИВАТНАЯ КОМНАТА"),
            E("lobby.online.create_room",
                "CREATE ROOM", "ODA OLUŞTUR", "CREAR SALA", "CRÉER UN SALON", "RAUM ERSTELLEN", "방 만들기", "СОЗДАТЬ КОМНАТУ"),
            E("lobby.online.join_by_code",
                "JOIN BY CODE", "KODLA KATIL", "UNIRSE CON CÓDIGO", "REJOINDRE PAR CODE", "MIT CODE BEITRETEN", "코드로 참가", "ВОЙТИ ПО КОДУ"),
            E("lobby.online.join_room",
                "JOIN ROOM", "ODAYA KATIL", "UNIRSE A LA SALA", "REJOINDRE LE SALON", "RAUM BEITRETEN", "방 참가", "ВОЙТИ В КОМНАТУ"),
            E("lobby.online.room_code",
                "ROOM CODE", "ODA KODU", "CÓDIGO DE SALA", "CODE DU SALON", "RAUMCODE", "방 코드", "КОД КОМНАТЫ"),
            E("lobby.online.create_or_join",
                "Create a room or enter a 6-digit code.", "Bir oda oluştur veya 6 haneli kod gir.", "Crea una sala o introduce un código de 6 dígitos.", "Créez un salon ou saisissez un code à 6 chiffres.", "Erstelle einen Raum oder gib einen 6-stelligen Code ein.", "방을 만들거나 6자리 코드를 입력하세요.", "Создайте комнату или введите 6-значный код."),
            E("lobby.online.create_or_join_short",
                "CREATE OR JOIN ROOM", "ODA OLUŞTUR VEYA KATIL", "CREAR O UNIRSE", "CRÉER OU REJOINDRE", "RAUM ERSTELLEN ODER BEITRETEN", "방 만들기 또는 참가", "СОЗДАТЬ ИЛИ ВОЙТИ"),
            E("lobby.online.invalid_code",
                "Enter exactly 6 digits.", "Tam olarak 6 hane gir.", "Introduce exactamente 6 dígitos.", "Saisissez exactement 6 chiffres.", "Gib genau 6 Ziffern ein.", "정확히 6자리를 입력하세요.", "Введите ровно 6 цифр."),
            E("lobby.online.preview_created",
                "LOCAL UI PREVIEW • ROOM CREATED", "YEREL ARAYÜZ ÖNİZLEME • ODA OLUŞTURULDU", "VISTA LOCAL • SALA CREADA", "APERÇU LOCAL • SALON CRÉÉ", "LOKALE UI-VORSCHAU • RAUM ERSTELLT", "로컬 UI 미리보기 • 방 생성됨", "ЛОКАЛЬНЫЙ ПРЕДПРОСМОТР • КОМНАТА СОЗДАНА"),
            E("lobby.online.preview_joined",
                "LOCAL UI PREVIEW • JOINED AS GUEST", "YEREL ARAYÜZ ÖNİZLEME • MİSAFİR OLARAK KATILDIN", "VISTA LOCAL • UNIDO COMO INVITADO", "APERÇU LOCAL • REJOINT COMME INVITÉ", "LOKALE UI-VORSCHAU • ALS GAST BEIGETRETEN", "로컬 UI 미리보기 • 게스트로 참가", "ЛОКАЛЬНЫЙ ПРЕДПРОСМОТР • ВХОД КАК ГОСТЬ"),
            E("lobby.online.show",
                "SHOW", "GÖSTER", "MOSTRAR", "AFFICHER", "ZEIGEN", "표시", "ПОКАЗАТЬ"),
            E("lobby.online.hide",
                "HIDE", "GİZLE", "OCULTAR", "MASQUER", "AUSBLENDEN", "숨기기", "СКРЫТЬ"),
            E("lobby.online.copy",
                "COPY", "KOPYALA", "COPIAR", "COPIER", "KOPIEREN", "복사", "КОПИРОВАТЬ"),
            E("lobby.online.copied",
                "COPIED", "KOPYALANDI", "COPIADO", "COPIÉ", "KOPIERT", "복사됨", "СКОПИРОВАНО"),
            E("lobby.online.ready",
                "READY", "HAZIR", "LISTO", "PRÊT", "BEREIT", "준비", "ГОТОВ"),
            E("lobby.online.ready_checked",
                "READY ✓", "HAZIR ✓", "LISTO ✓", "PRÊT ✓", "BEREIT ✓", "준비 ✓", "ГОТОВ ✓"),
            E("lobby.online.revision_format",
                "SETTINGS REV {0}", "AYAR SÜRÜMÜ {0}", "REV. AJUSTES {0}", "RÉV. RÉGLAGES {0}", "EINSTELLUNGS-REV {0}", "설정 리비전 {0}", "РЕВИЗИЯ НАСТРОЕК {0}"),
            E("lobby.online.settings_changed",
                "SETTINGS CHANGED • READY RESET", "AYARLAR DEĞİŞTİ • HAZIRLIK SIFIRLANDI", "AJUSTES CAMBIADOS • LISTO RESTABLECIDO", "RÉGLAGES MODIFIÉS • PRÊT RÉINITIALISÉ", "EINSTELLUNGEN GEÄNDERT • BEREIT ZURÜCKGESETZT", "설정 변경 • 준비 초기화", "НАСТРОЙКИ ИЗМЕНЕНЫ • ГОТОВНОСТЬ СБРОШЕНА"),
            E("lobby.online.player_format",
                "PLAYER {0}", "OYUNCU {0}", "JUGADOR {0}", "JOUEUR {0}", "SPIELER {0}", "플레이어 {0}", "ИГРОК {0}"),
            E("lobby.online.you",
                "PLAYER (YOU)", "OYUNCU (SEN)", "JUGADOR (TÚ)", "JOUEUR (VOUS)", "SPIELER (DU)", "플레이어 (나)", "ИГРОК (ВЫ)"),
            E("lobby.online.you_short",
                "YOU", "SEN", "TÚ", "VOUS", "DU", "나", "ВЫ"),
            E("lobby.online.host",
                "HOST", "KURUCU", "ANFITRIÓN", "HÔTE", "HOST", "호스트", "ХОСТ"),
            E("lobby.online.host_ready",
                "HOST • READY", "KURUCU • HAZIR", "ANFITRIÓN • LISTO", "HÔTE • PRÊT", "HOST • BEREIT", "호스트 • 준비", "ХОСТ • ГОТОВ"),
            E("lobby.online.host_player",
                "HOST PLAYER", "KURUCU OYUNCU", "JUGADOR ANFITRIÓN", "JOUEUR HÔTE", "HOST-SPIELER", "호스트 플레이어", "ИГРОК-ХОСТ"),
            E("lobby.online.waiting",
                "WAITING", "BEKLENİYOR", "ESPERANDO", "EN ATTENTE", "WARTET", "대기 중", "ОЖИДАНИЕ"),
            E("lobby.online.waiting_player",
                "WAITING FOR PLAYER", "OYUNCU BEKLENİYOR", "ESPERANDO JUGADOR", "EN ATTENTE D'UN JOUEUR", "WARTET AUF SPIELER", "플레이어 대기 중", "ОЖИДАНИЕ ИГРОКА"),
            E("lobby.online.open_human_seat",
                "OPEN HUMAN SEAT", "AÇIK OYUNCU KOLTUĞU", "PLAZA HUMANA LIBRE", "PLACE JOUEUR LIBRE", "FREIER SPIELERPLATZ", "빈 플레이어 자리", "СВОБОДНОЕ МЕСТО"),
            E("lobby.online.bot",
                "BOT", "BOT", "BOT", "BOT", "BOT", "봇", "БОТ"),
            E("lobby.online.bot_seat",
                "BOT SEAT", "BOT KOLTUĞU", "PLAZA DE BOT", "PLACE BOT", "BOT-PLATZ", "봇 자리", "МЕСТО БОТА"),
            E("lobby.online.ready_for_revision",
                "READY • REV {0}", "HAZIR • SÜRÜM {0}", "LISTO • REV {0}", "PRÊT • RÉV {0}", "BEREIT • REV {0}", "준비 • 리비전 {0}", "ГОТОВ • РЕВ {0}"),
            E("lobby.online.not_ready",
                "NOT READY", "HAZIR DEĞİL", "NO LISTO", "PAS PRÊT", "NICHT BEREIT", "준비 안 됨", "НЕ ГОТОВ"),
            E("lobby.online.starting",
                "STARTING...", "BAŞLATILIYOR...", "INICIANDO...", "DÉMARRAGE...", "STARTET...", "시작 중...", "ЗАПУСК..."),
            E("lobby.online.add_or_wait",
                "+ ADD / WAIT ONLINE", "+ EKLE / ONLINE BEKLE", "+ AÑADIR / ESPERAR ONLINE", "+ AJOUTER / ATTENDRE EN LIGNE", "+ HINZUFÜGEN / ONLINE WARTEN", "+ 추가 / 온라인 대기", "+ ДОБАВИТЬ / ЖДАТЬ ОНЛАЙН"),
            E("lobby.online.local_player",
                "LOCAL PLAYER", "YEREL OYUNCU", "JUGADOR LOCAL", "JOUEUR LOCAL", "LOKALER SPIELER", "로컬 플레이어", "ЛОКАЛЬНЫЙ ИГРОК"),
            E("lobby.online.local_player_number",
                "LOCAL PLAYER {0}", "YEREL OYUNCU {0}", "JUGADOR LOCAL {0}", "JOUEUR LOCAL {0}", "LOKALER SPIELER {0}", "로컬 플레이어 {0}", "ЛОКАЛЬНЫЙ ИГРОК {0}"),
            E("lobby.online.waiting_online",
                "WAITING FOR ONLINE PLAYER", "ONLINE OYUNCU BEKLENİYOR", "ESPERANDO JUGADOR ONLINE", "EN ATTENTE D'UN JOUEUR EN LIGNE", "WARTET AUF ONLINE-SPIELER", "온라인 플레이어 대기 중", "ОЖИДАНИЕ ОНЛАЙН-ИГРОКА"),
            E("lobby.online.open_online_seat",
                "OPEN ONLINE SEAT", "AÇIK ONLINE KOLTUK", "PLAZA ONLINE LIBRE", "PLACE EN LIGNE LIBRE", "FREIER ONLINE-PLATZ", "빈 온라인 자리", "СВОБОДНОЕ ОНЛАЙН-МЕСТО"),
            E("lobby.online.no_ready_required",
                "LOCAL • NO READY REQUIRED", "YEREL • HAZIR GEREKMEZ", "LOCAL • NO REQUIERE LISTO", "LOCAL • PAS DE PRÊT REQUIS", "LOKAL • KEIN BEREIT NÖTIG", "로컬 • 준비 불필요", "ЛОКАЛЬНЫЙ • ГОТОВНОСТЬ НЕ НУЖНА"),
            E("lobby.online.bot_no_ready",
                "BOT • NO READY", "BOT • HAZIR GEREKMEZ", "BOT • SIN LISTO", "BOT • PAS DE PRÊT", "BOT • KEIN BEREIT", "봇 • 준비 불필요", "БОТ • ГОТОВНОСТЬ НЕ НУЖНА"),
            E("lobby.online.host_local",
                "HOST • LOCAL", "KURUCU • YEREL", "ANFITRIÓN • LOCAL", "HÔTE • LOCAL", "HOST • LOKAL", "호스트 • 로컬", "ХОСТ • ЛОКАЛЬНЫЙ"),
            E("lobby.online.roster_resolved",
                "ROSTER READY • START AVAILABLE", "KADRO HAZIR • BAŞLATILABİLİR", "PLANTILLA LISTA • INICIO DISPONIBLE", "ÉQUIPE PRÊTE • DÉMARRAGE DISPONIBLE", "AUFSTELLUNG BEREIT • START VERFÜGBAR", "구성 완료 • 시작 가능", "СОСТАВ ГОТОВ • МОЖНО НАЧАТЬ"),
            E("lobby.online.waiting_online_or_add",
                "WAITING FOR ONLINE PLAYER • OR ADD LOCAL/BOT", "ONLINE OYUNCU BEKLENİYOR • VEYA YEREL/BOT EKLE", "ESPERANDO JUGADOR ONLINE • O AÑADE LOCAL/BOT", "EN ATTENTE D'UN JOUEUR EN LIGNE • OU AJOUTEZ LOCAL/BOT", "WARTET AUF ONLINE-SPIELER • ODER LOKAL/BOT HINZUFÜGEN", "온라인 플레이어 대기 • 또는 로컬/봇 추가", "ОЖИДАНИЕ ОНЛАЙН-ИГРОКА • ИЛИ ДОБАВЬТЕ ЛОКАЛЬНОГО/БОТА"),
            E("lobby.online.bot_number",
                "BOT {0}", "BOT {0}", "BOT {0}", "BOT {0}", "BOT {0}", "봇 {0}", "БОТ {0}"),
            E("lobby.online.add_local_player",
                "ADD LOCAL PLAYER", "YEREL OYUNCU EKLE", "AÑADIR JUGADOR LOCAL", "AJOUTER UN JOUEUR LOCAL", "LOKALEN SPIELER HINZUFÜGEN", "로컬 플레이어 추가", "ДОБАВИТЬ ЛОКАЛЬНОГО ИГРОКА"),
            E("lobby.online.add_bot",
                "ADD BOT", "BOT EKLE", "AÑADIR BOT", "AJOUTER UN BOT", "BOT HINZUFÜGEN", "봇 추가", "ДОБАВИТЬ БОТА"),
            E("lobby.online.local_bot_number",
                "LOCAL BOT {0}", "YEREL BOT {0}", "BOT LOCAL {0}", "BOT LOCAL {0}", "LOKALER BOT {0}", "로컬 봇 {0}", "ЛОКАЛЬНЫЙ БОТ {0}"),
            E("lobby.online.backend_connected",
                "BACKEND SNAPSHOT CONNECTED", "BACKEND DURUMU BAĞLANDI", "SNAPSHOT BACKEND CONECTADO", "ÉTAT BACKEND CONNECTÉ", "BACKEND-SNAPSHOT VERBUNDEN", "백엔드 스냅샷 연결됨", "СНИМОК BACKEND ПОДКЛЮЧЕН")
        };

        HashSet<string> keys =
            new HashSet<string>(
                additions
                    .Select(
                        entry =>
                            entry.key),
                StringComparer.OrdinalIgnoreCase);

        entries.RemoveAll(
            entry =>
                entry != null &&
                keys.Contains(
                    entry.key));

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

    private static void AddLocalizedBinding(
        TMP_Text text,
        string key)
    {
        if (text == null)
        {
            return;
        }

        AtlasBoardLocalizedText localized =
            text.GetComponent<
                AtlasBoardLocalizedText>();

        if (localized == null)
        {
            localized =
                text.gameObject.AddComponent<
                    AtlasBoardLocalizedText>();
        }

        localized.EditorConfigure(
            key,
            text);
    }

    private static TMP_Text CreateLocalizedText(
        Transform parent,
        string name,
        string key,
        string fallback,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        FontStyles style,
        TextAlignmentOptions alignment,
        TMP_FontAsset font)
    {
        TMP_Text text =
            CreateText(
                parent,
                name,
                fallback,
                position,
                size,
                fontSize,
                color,
                style,
                alignment,
                font);

        AddLocalizedBinding(
            text,
            key);

        return text;
    }

    private static GameObject CreateStretchRoot(
        Transform parent,
        string name)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform));

        Undo.RegisterCreatedObjectUndo(
            root,
            "Build AtlasBoard Private Lobby UI");

        root.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            root.GetComponent<
                RectTransform>();

        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;

        rect.SetAsLastSibling();

        return root;
    }

    private static GameObject CreateImage(
        Transform parent,
        string name,
        Vector2 screenPosition,
        Vector2 size,
        Color color,
        Image template)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));

        Undo.RegisterCreatedObjectUndo(
            root,
            "Build AtlasBoard Private Lobby UI");

        root.transform.SetParent(
            parent,
            false);

        SetScreenRect(
            root.GetComponent<
                RectTransform>(),
            screenPosition,
            size);

        Image image =
            root.GetComponent<Image>();

        image.color =
            color;

        if (template != null)
        {
            image.sprite =
                template.sprite;

            image.type =
                template.type;

            image.pixelsPerUnitMultiplier =
                template.pixelsPerUnitMultiplier;
        }

        return root;
    }

    private static GameObject CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 localPosition,
        Vector2 size,
        Color color,
        Image template,
        TMP_FontAsset font,
        out TMP_Text labelText)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));

        Undo.RegisterCreatedObjectUndo(
            root,
            "Build AtlasBoard Private Lobby UI");

        root.transform.SetParent(
            parent,
            false);

        SetLocalRect(
            root.GetComponent<
                RectTransform>(),
            localPosition,
            size);

        Image image =
            root.GetComponent<Image>();

        image.color =
            color;

        if (template != null)
        {
            image.sprite =
                template.sprite;

            image.type =
                template.type;

            image.pixelsPerUnitMultiplier =
                template.pixelsPerUnitMultiplier;
        }

        Button button =
            root.GetComponent<Button>();

        button.targetGraphic =
            image;

        labelText =
            CreateText(
                root.transform,
                "Label",
                label,
                Vector2.zero,
                new Vector2(
                    size.x - 18f,
                    size.y - 12f),
                19f,
                Color.white,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                font);

        return root;
    }

    private static TMP_InputField CreateInputField(
        Transform parent,
        string name,
        Vector2 localPosition,
        Vector2 size,
        string placeholder,
        TMP_FontAsset font)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(TMP_InputField));

        Undo.RegisterCreatedObjectUndo(
            root,
            "Build AtlasBoard Private Lobby UI");

        root.transform.SetParent(
            parent,
            false);

        SetLocalRect(
            root.GetComponent<
                RectTransform>(),
            localPosition,
            size);

        Image image =
            root.GetComponent<Image>();

        image.color =
            Color.white;

        GameObject viewport =
            new GameObject(
                "Text Area",
                typeof(RectTransform),
                typeof(RectMask2D));

        viewport.transform.SetParent(
            root.transform,
            false);

        RectTransform viewportRect =
            viewport.GetComponent<
                RectTransform>();

        viewportRect.anchorMin =
            Vector2.zero;

        viewportRect.anchorMax =
            Vector2.one;

        viewportRect.offsetMin =
            new Vector2(
                16f,
                8f);

        viewportRect.offsetMax =
            new Vector2(
                -16f,
                -8f);

        TMP_Text placeholderText =
            CreateText(
                viewport.transform,
                "Placeholder",
                placeholder,
                Vector2.zero,
                Vector2.zero,
                17f,
                new Color32(
                    130,
                    132,
                    139,
                    180),
                FontStyles.Normal,
                TextAlignmentOptions.Left,
                font);

        StretchText(
            placeholderText.rectTransform);

        TMP_Text inputText =
            CreateText(
                viewport.transform,
                "Text",
                string.Empty,
                Vector2.zero,
                Vector2.zero,
                25f,
                Dark,
                FontStyles.Bold,
                TextAlignmentOptions.Left,
                font);

        StretchText(
            inputText.rectTransform);

        TMP_InputField input =
            root.GetComponent<
                TMP_InputField>();

        input.textViewport =
            viewportRect;

        input.textComponent =
            inputText;

        input.placeholder =
            placeholderText;

        input.contentType =
            TMP_InputField.ContentType.IntegerNumber;

        input.characterLimit =
            6;

        input.lineType =
            TMP_InputField.LineType.SingleLine;

        input.targetGraphic =
            image;

        return input;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        Vector2 localPosition,
        Vector2 size,
        float fontSize,
        Color color,
        FontStyles style,
        TextAlignmentOptions alignment,
        TMP_FontAsset font)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));

        Undo.RegisterCreatedObjectUndo(
            root,
            "Build AtlasBoard Private Lobby UI");

        root.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            root.GetComponent<
                RectTransform>();

        SetLocalRect(
            rect,
            localPosition,
            size);

        TextMeshProUGUI text =
            root.GetComponent<
                TextMeshProUGUI>();

        text.text =
            value;

        text.fontSize =
            fontSize;

        text.color =
            color;

        text.fontStyle =
            style;

        text.alignment =
            alignment;

        text.textWrappingMode =
            TextWrappingModes.NoWrap;

        if (font != null)
        {
            text.font =
                font;
        }

        return text;
    }

    private static void StretchText(
        RectTransform rect)
    {
        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.pivot =
            new Vector2(
                0.5f,
                0.5f);

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;
    }

    private static void SetScreenRect(
        RectTransform rect,
        Vector2 screenPosition,
        Vector2 size)
    {
        rect.anchorMin =
            new Vector2(
                0.5f,
                0.5f);

        rect.anchorMax =
            new Vector2(
                0.5f,
                0.5f);

        rect.pivot =
            new Vector2(
                0.5f,
                0.5f);

        rect.anchoredPosition =
            new Vector2(
                screenPosition.x - 960f,
                540f - screenPosition.y);

        rect.sizeDelta =
            size;
    }

    private static void SetLocalRect(
        RectTransform rect,
        Vector2 localPosition,
        Vector2 size)
    {
        rect.anchorMin =
            new Vector2(
                0.5f,
                0.5f);

        rect.anchorMax =
            new Vector2(
                0.5f,
                0.5f);

        rect.pivot =
            new Vector2(
                0.5f,
                0.5f);

        rect.anchoredPosition =
            localPosition;

        rect.sizeDelta =
            size;
    }

    private static GameObject FindSceneObject(
        string name)
    {
        GameObject[] all =
            Resources.FindObjectsOfTypeAll<
                GameObject>();

        foreach (GameObject candidate in all)
        {
            if (candidate == null ||
                candidate.name != name ||
                !candidate.scene.IsValid())
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static Transform FindChildRecursive(
        Transform root,
        string name)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == name)
        {
            return root;
        }

        for (int i = 0;
             i < root.childCount;
             i++)
        {
            Transform result =
                FindChildRecursive(
                    root.GetChild(i),
                    name);

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
#endif
