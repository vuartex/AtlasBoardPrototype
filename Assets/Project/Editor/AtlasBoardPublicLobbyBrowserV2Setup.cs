#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AtlasBoardPublicLobbyBrowserV2Setup
{
    private const string LocalizationDatabasePath =
        "Assets/Project/Data/Localization/Localization_Default.asset";

    private static readonly Color Background = new Color32(143, 146, 169, 255);
    private static readonly Color Panel = new Color32(238, 237, 228, 255);
    private static readonly Color Cream = new Color32(246, 241, 231, 255);
    private static readonly Color Header = new Color32(126, 183, 216, 255);
    private static readonly Color Blue = new Color32(10, 130, 180, 255);
    private static readonly Color Green = new Color32(89, 116, 0, 255);
    private static readonly Color Gold = new Color32(196, 145, 0, 255);
    private static readonly Color Dark = new Color32(54, 54, 61, 255);
    private static readonly Color Muted = new Color32(110, 111, 118, 255);

    [MenuItem("Atlas Board/Online/Build Online Rooms UX v2", false, 472)]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Exit Play Mode before building Online Rooms UX v2.");
            return;
        }

        GameObject canvas = FindSceneObject("Canvas_MainMenu");
        if (canvas == null)
        {
            Debug.LogError("Online Rooms UX v2 requires Canvas_MainMenu.");
            return;
        }

        Transform mainMenu = FindChildRecursive(canvas.transform, "MainMenu");
        Transform lobby = FindChildRecursive(canvas.transform, "Lobby");
        if (mainMenu == null || lobby == null)
        {
            Debug.LogError("Online Rooms UX v2 requires MainMenu and Lobby roots.");
            return;
        }

        AtlasBoardMainMenuController menuController =
            canvas.GetComponent<AtlasBoardMainMenuController>();
        AtlasBoardLobbyRuntimeBridge runtimeBridge =
            canvas.GetComponent<AtlasBoardLobbyRuntimeBridge>();
        AtlasBoardPrivateLobbyUIController lobbyUi =
            canvas.GetComponent<AtlasBoardPrivateLobbyUIController>();

        if (menuController == null || runtimeBridge == null || lobbyUi == null)
        {
            Debug.LogError(
                "Online Rooms UX v2 requires MainMenuController, LobbyRuntimeBridge and PrivateLobbyUIController on Canvas_MainMenu.");
            return;
        }

        MergeLocalizationEntries();
        TMP_FontAsset font = FindSceneFont();

        // Remove Phase 4B v1 bottom button/browser/controller.
        DestroyChildIfPresent(mainMenu, "Button_PublicRooms");
        DestroyChildIfPresent(mainMenu, "PublicRoomsCard");
        DestroyChildIfPresent(canvas.transform, "PublicLobbyBrowser");

        AtlasBoardPublicLobbyBrowserController existingController =
            canvas.GetComponent<AtlasBoardPublicLobbyBrowserController>();
        if (existingController != null)
        {
            Undo.DestroyObjectImmediate(existingController);
        }

        AtlasBoardPublicLobbyBrowserController browserController =
            Undo.AddComponent<AtlasBoardPublicLobbyBrowserController>(canvas);

        UpdateMainMenuCards(mainMenu, browserController, font);

        // Shared lobby settings popup for BOTH private and public rooms.
        BuildSharedLobbySettingsPopup(lobby, font);
        BuildPrivateJoinPasswordField(mainMenu, font);

        BrowserRefs browser = BuildBrowser(canvas.transform, font);

        browserController.EditorConfigure(
            mainMenu.gameObject,
            browser.Root,
            browser.SearchInput,
            browser.SearchPlaceholder,
            browser.MapFilter,
            browser.PlayersFilter,
            browser.RoundFilter,
            browser.PasswordFilter,
            browser.StatusText,
            browser.EmptyState,
            browser.BackButton,
            browser.RefreshButton,
            browser.CreateButton,
            browser.PasswordPromptRoot,
            browser.PasswordPromptBody,
            browser.PasswordPromptInput,
            browser.PasswordPromptStatus,
            browser.PasswordPromptJoin,
            browser.PasswordPromptCancel,
            browser.RowRoots,
            browser.RowButtons,
            browser.RowHostTexts,
            browser.RowMapTexts,
            browser.RowPlayersTexts,
            browser.RowRoundsTexts,
            browser.RowRegionTexts,
            browser.RowAccessTexts,
            browser.RowJoinButtons,
            runtimeBridge,
            menuController,
            lobbyUi);

        browser.Root.SetActive(false);
        browser.PasswordPromptRoot.SetActive(false);
        browser.EmptyState.SetActive(false);

        EditorUtility.SetDirty(browserController);
        EditorUtility.SetDirty(canvas);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkAllScenesDirty();
        Selection.activeGameObject = browser.Root;

        Debug.Log(
            "AtlasBoard Online Rooms UX v2 built. " +
            "PLAY now hosts an online public room; PUBLIC ROOMS is a separated full card under PLAY; " +
            "browser filters/JOIN/double-click/password prompt are wired; " +
            "Private/Public lobbies share one GAME SETTINGS popup with password access controls.");
    }

    [MenuItem("Atlas Board/Online/Validate Online Rooms UX v2", false, 473)]
    public static void Validate()
    {
        GameObject canvas = FindSceneObject("Canvas_MainMenu");
        if (canvas == null)
        {
            Debug.LogError("Online Rooms UX v2 validation FAILED: Canvas_MainMenu missing.");
            return;
        }

        string[] required =
        {
            "PublicRoomsCard",
            "PublicLobbyBrowser",
            "LobbyGameSettingsOverlay",
            "Button_LobbyGameSettingsOpen",
            "Input_LobbyPassword",
            "Button_LobbyPasswordVisibility",
            "Button_CopyLobbyPassword",
            "Dropdown_FilterRounds",
            "Input_JoinPassword"
        };

        foreach (string name in required)
        {
            if (FindChildRecursive(canvas.transform, name) == null)
            {
                Debug.LogError($"Online Rooms UX v2 validation FAILED: {name} missing.");
                return;
            }
        }

        if (canvas.GetComponent<AtlasBoardPublicLobbyBrowserController>() == null)
        {
            Debug.LogError("Online Rooms UX v2 validation FAILED: browser controller missing.");
            return;
        }

        Transform settingsPanel =
            FindChildRecursive(
                canvas.transform,
                "Panel_GameSettings");

        Transform playersPanel =
            FindChildRecursive(
                canvas.transform,
                "Panel_Players");

        if (settingsPanel is RectTransform settingsRect &&
            playersPanel is RectTransform playersRect)
        {
            if (Mathf.Abs(settingsRect.anchoredPosition.y - playersRect.anchoredPosition.y) > 0.1f ||
                Mathf.Abs(settingsRect.sizeDelta.y - playersRect.sizeDelta.y) > 0.1f)
            {
                Debug.LogError(
                    "Online Rooms UX v2 validation FAILED: Game Settings / Players panels are not vertically aligned.");
                return;
            }
        }

        Debug.Log(
            "AtlasBoard Online Rooms UX v2 static validation PASSED. " +
            "This proves scene/component wiring only; real PASS still requires Play Mode + Firebase emulator + two-client tests.");
    }

    private static void UpdateMainMenuCards(
        Transform mainMenu,
        AtlasBoardPublicLobbyBrowserController browserController,
        TMP_FontAsset font)
    {
        Transform playCard =
            FindChildRecursive(
                mainMenu,
                "PlayCard");

        Transform privateCard =
            FindChildRecursive(
                mainMenu,
                "PrivateTableCard");

        Transform shopCard =
            FindChildRecursive(
                mainMenu,
                "ShopCard");

        if (playCard == null ||
            privateCard == null ||
            shopCard == null)
        {
            Debug.LogError(
                "Online Rooms UX v2 could not find the three main action cards.");
            return;
        }

        // Keep the three original cards aligned and move the WHOLE top row up.
        // This creates vertical room below PLAY without pulling its subtitle into
        // the card itself.
        SetMainCardY(privateCard, 90f);
        SetMainCardY(playCard, 90f);
        SetMainCardY(shopCard, 90f);

        TMP_Text playSubtitle =
            FindChildRecursive(
                playCard,
                "Subtitle")
            ?.GetComponent<TMP_Text>();

        if (playSubtitle != null)
        {
            playSubtitle.text = "HOST ONLINE";
            BindLocalized(
                playSubtitle,
                "menu.play_online_subtitle");

            // Restore the original subtitle position OUTSIDE the card.
            playSubtitle.rectTransform.anchoredPosition =
                new Vector2(
                    playSubtitle.rectTransform.anchoredPosition.x,
                    -169f);
        }

        GameObject publicCard =
            UnityEngine.Object.Instantiate(
                privateCard.gameObject,
                mainMenu,
                false);

        Undo.RegisterCreatedObjectUndo(
            publicCard,
            "Create AtlasBoard Public Rooms card");

        publicCard.name =
            "PublicRoomsCard";

        RectTransform publicRect =
            publicCard.GetComponent<RectTransform>();

        RectTransform playRect =
            playCard.GetComponent<RectTransform>();

        publicRect.anchoredPosition =
            new Vector2(
                playRect.anchoredPosition.x,
                playRect.anchoredPosition.y - 365f);

        publicRect.sizeDelta =
            playRect.sizeDelta;

        TMP_Text title =
            FindChildRecursive(
                publicCard.transform,
                "Title")
            ?.GetComponent<TMP_Text>();

        TMP_Text subtitle =
            FindChildRecursive(
                publicCard.transform,
                "Subtitle")
            ?.GetComponent<TMP_Text>();

        TMP_Text glyph =
            FindChildRecursive(
                publicCard.transform,
                "IllustrationGlyph")
            ?.GetComponent<TMP_Text>();

        if (title != null)
        {
            title.text = "PUBLIC ROOMS";
            BindLocalized(
                title,
                "menu.public_rooms");
        }

        if (subtitle != null)
        {
            subtitle.text = "BROWSE / JOIN";
            BindLocalized(
                subtitle,
                "menu.public_rooms_subtitle");

            // Keep this subtitle below the card just like the other cards.
            subtitle.rectTransform.anchoredPosition =
                new Vector2(
                    subtitle.rectTransform.anchoredPosition.x,
                    -169f);
        }

        if (glyph != null)
        {
            glyph.text = "PUB";
            glyph.fontSize = 42f;
        }

        Button inherited =
            publicCard.GetComponent<Button>();

        ColorBlock colors =
            inherited != null
                ? inherited.colors
                : ColorBlock.defaultColorBlock;

        if (inherited != null)
        {
            Undo.DestroyObjectImmediate(
                inherited);
        }

        Button publicButton =
            Undo.AddComponent<Button>(
                publicCard);

        publicButton.targetGraphic =
            publicCard.GetComponent<Image>();

        publicButton.colors =
            colors;

        UnityEventTools.AddPersistentListener(
            publicButton.onClick,
            browserController.ShowBrowser);

        // Restore the footer to its original stable location. With the top row
        // shifted upward there is now real separation from BROWSE / JOIN.
        Transform footer =
            FindChildRecursive(
                mainMenu,
                "FooterVersion");

        if (footer is RectTransform footerRect)
        {
            footerRect.anchoredPosition =
                new Vector2(
                    footerRect.anchoredPosition.x,
                    -495f);
        }
    }

    private static void SetMainCardY(
        Transform card,
        float anchoredY)
    {
        if (card is not RectTransform rect)
        {
            return;
        }

        rect.anchoredPosition =
            new Vector2(
                rect.anchoredPosition.x,
                anchoredY);
    }

    private static void BuildSharedLobbySettingsPopup(
        Transform lobby,
        TMP_FontAsset font)
    {
        Transform oldOverlay =
            FindChildRecursive(
                lobby,
                "LobbyGameSettingsOverlay");

        Transform settingsPanel =
            FindChildRecursive(
                lobby,
                "Panel_GameSettings");

        Transform playersPanel =
            FindChildRecursive(
                lobby,
                "Panel_Players");

        // Older Phase 4 revisions temporarily re-parented the original settings
        // panel into the popup. Always restore it first.
        if (settingsPanel != null &&
            oldOverlay != null &&
            settingsPanel.IsChildOf(oldOverlay))
        {
            settingsPanel.SetParent(
                lobby,
                false);
        }

        if (oldOverlay != null)
        {
            Undo.DestroyObjectImmediate(
                oldOverlay.gameObject);
        }

        DestroyChildIfPresent(
            lobby,
            "Button_LobbyGameSettingsOpen");

        if (settingsPanel == null ||
            playersPanel == null)
        {
            Debug.LogWarning(
                "Panel_GameSettings/Panel_Players not found; lobby layout was not rebuilt.");
            return;
        }

        RectTransform settingsRect =
            settingsPanel.GetComponent<RectTransform>();

        RectTransform playersRect =
            playersPanel.GetComponent<RectTransform>();

        // IMPORTANT: exact vertical alignment with PLAYERS. No guessed Y value.
        settingsRect.anchorMin =
            playersRect.anchorMin;
        settingsRect.anchorMax =
            playersRect.anchorMax;
        settingsRect.pivot =
            playersRect.pivot;
        settingsRect.anchoredPosition =
            new Vector2(
                settingsRect.anchoredPosition.x,
                playersRect.anchoredPosition.y);
        settingsRect.sizeDelta =
            new Vector2(
                settingsRect.sizeDelta.x,
                playersRect.sizeDelta.y);

        // Create room for the extra footer button WITHOUT making the panel taller.
        // The three existing toggle rows are compacted slightly upward.
        SetLocalAnchoredY(
            settingsPanel,
            "Toggle_BalancedDevelopment",
            -105f);
        SetLocalAnchoredY(
            settingsPanel,
            "Toggle_DoublesEnabled",
            -170f);
        SetLocalAnchoredY(
            settingsPanel,
            "Toggle_TripleDoublePenalty",
            -235f);

        Button openButton =
            CreateButton(
                settingsPanel,
                "Button_LobbyGameSettingsOpen",
                "MORE SETTINGS",
                new Vector2(0f, -315f),
                new Vector2(470f, 48f),
                Blue,
                font);

        TMP_Text openTitle =
            FindChildRecursive(
                openButton.transform,
                "Label")
            ?.GetComponent<TMP_Text>();

        if (openTitle != null)
        {
            openTitle.fontSize = 15f;
            BindLocalized(
                openTitle,
                "lobby.game_settings.more");
        }

        // Shared Private/Public extra settings dialog.
        GameObject overlay =
            CreateStretchPanel(
                lobby,
                "LobbyGameSettingsOverlay",
                new Color32(8, 12, 20, 210));

        overlay.AddComponent<
            AtlasBoardEscapeBlocker>();

        GameObject dialog =
            CreatePanel(
                overlay.transform,
                "LobbyGameSettingsDialog",
                Vector2.zero,
                new Vector2(760f, 520f),
                Cream);

        CreateLocalizedText(
            dialog.transform,
            "LobbyGameSettingsTitle",
            "lobby.game_settings.open",
            "GAME SETTINGS",
            new Vector2(0f, 190f),
            new Vector2(620f, 54f),
            30f,
            Dark,
            font);

        CreateLocalizedText(
            dialog.transform,
            "LobbyAccessSectionTitle",
            "lobby.access.password",
            "ROOM PASSWORD",
            new Vector2(0f, 112f),
            new Vector2(610f, 36f),
            18f,
            Dark,
            font);

        CreateRawText(
            dialog.transform,
            "LobbyPasswordState",
            "NO PASSWORD",
            new Vector2(0f, 86f),
            new Vector2(610f, 32f),
            16f,
            Muted,
            font);

        CreatePanel(
            dialog.transform,
            "LobbyPasswordValuePanel",
            new Vector2(-120f, 38f),
            new Vector2(260f, 56f),
            Panel);

        TMP_Text passwordValueText =
            CreateRawText(
                dialog.transform,
                "LobbyPasswordValue",
                "------",
                new Vector2(-120f, 38f),
                new Vector2(240f, 46f),
                24f,
                Dark,
                font);

        passwordValueText.fontStyle =
            FontStyles.Bold;

        Button showPassword =
            CreateButton(
                dialog.transform,
                "Button_LobbyPasswordVisibility",
                "SHOW",
                new Vector2(120f, 38f),
                new Vector2(120f, 48f),
                Blue,
                font);

        BindLocalized(
            FindChildRecursive(
                showPassword.transform,
                "Label")
            ?.GetComponent<TMP_Text>(),
            "lobby.online.show");

        Button copyPassword =
            CreateButton(
                dialog.transform,
                "Button_CopyLobbyPassword",
                "COPY",
                new Vector2(255f, 38f),
                new Vector2(120f, 48f),
                Gold,
                font);

        BindLocalized(
            FindChildRecursive(
                copyPassword.transform,
                "Label")
            ?.GetComponent<TMP_Text>(),
            "lobby.online.copy");

        TMP_InputField passwordInput =
            CreateInputField(
                dialog.transform,
                "Input_LobbyPassword",
                new Vector2(0f, -28f),
                new Vector2(500f, 58f),
                "PASSWORD (OPTIONAL)",
                font,
                password: true);

        BindInputPlaceholder(
            passwordInput,
            "lobby.access.password_optional");

        Button apply =
            CreateButton(
                dialog.transform,
                "Button_ApplyLobbyPassword",
                "APPLY PASSWORD",
                new Vector2(0f, -102f),
                new Vector2(300f, 58f),
                Gold,
                font);

        BindLocalized(
            FindChildRecursive(
                apply.transform,
                "Label")
            ?.GetComponent<TMP_Text>(),
            "lobby.access.apply");

        CreateRawText(
            dialog.transform,
            "LobbyPasswordStatus",
            string.Empty,
            new Vector2(0f, -156f),
            new Vector2(610f, 36f),
            14f,
            Muted,
            font);

        Button close =
            CreateButton(
                dialog.transform,
                "Button_CloseLobbyGameSettings",
                "DONE",
                new Vector2(0f, -218f),
                new Vector2(260f, 62f),
                Blue,
                font);

        BindLocalized(
            FindChildRecursive(
                close.transform,
                "Label")
            ?.GetComponent<TMP_Text>(),
            "lobby.game_settings.done");

        overlay.SetActive(false);
        overlay.transform.SetAsLastSibling();
    }

    private static void SetLocalAnchoredY(
        Transform root,
        string childName,
        float anchoredY)
    {
        Transform child =
            FindChildRecursive(
                root,
                childName);

        if (child is not RectTransform rect)
        {
            return;
        }

        rect.anchoredPosition =
            new Vector2(
                rect.anchoredPosition.x,
                anchoredY);
    }

    private static void BuildPrivateJoinPasswordField(
        Transform mainMenu,
        TMP_FontAsset font)
    {
        Transform panel =
            FindChildRecursive(
                mainMenu,
                "Panel_PrivateRoomEntry");

        if (panel == null)
        {
            Debug.LogWarning(
                "Panel_PrivateRoomEntry not found; private join layout was not polished.");
            return;
        }

        DestroyChildIfPresent(
            panel,
            "Input_JoinPassword");

        DestroyChildIfPresent(
            panel,
            "JoinPasswordLabel");

        RectTransform panelRect =
            panel.GetComponent<RectTransform>();

        panelRect.sizeDelta =
            new Vector2(820f, 540f);

        Transform entryOverlay =
            panel.parent;

        if (entryOverlay != null &&
            entryOverlay.name ==
                "PrivateRoomEntryOverlay")
        {
            entryOverlay.SetAsLastSibling();
        }

        // Use the new vertical space above the join controls instead of letting
        // PRIVATE ROOM / help copy collide with JOIN BY CODE.
        SetLocalAnchoredY(
            panel,
            "PrivateRoomTitle",
            155f);
        SetLocalAnchoredY(
            panel,
            "PrivateRoomHelp",
            108f);

        Transform codeLabel =
            FindChildRecursive(
                panel,
                "JoinByCodeLabel");

        Transform codeInput =
            FindChildRecursive(
                panel,
                "Input_JoinCode");

        Transform joinButton =
            FindChildRecursive(
                panel,
                "Button_JoinRoom");

        Transform createButton =
            FindChildRecursive(
                panel,
                "Button_CreateRoom");

        Transform cancelButton =
            FindChildRecursive(
                panel,
                "Button_CancelPrivateRoomEntry");

        Transform status =
            FindChildRecursive(
                panel,
                "EntryStatus");

        if (createButton is RectTransform createRect)
        {
            createRect.anchoredPosition =
                new Vector2(-190f, 10f);
            createRect.sizeDelta =
                new Vector2(270f, 62f);
        }

        if (codeLabel is RectTransform codeLabelRect)
        {
            codeLabelRect.anchoredPosition =
                new Vector2(175f, 52f);
            codeLabelRect.sizeDelta =
                new Vector2(310f, 30f);
        }

        if (codeInput is RectTransform codeRect)
        {
            codeRect.anchoredPosition =
                new Vector2(175f, 12f);
            codeRect.sizeDelta =
                new Vector2(310f, 54f);
        }

        CreateLocalizedText(
            panel,
            "JoinPasswordLabel",
            "lobby.access.password_optional",
            "PASSWORD (OPTIONAL)",
            new Vector2(175f, -58f),
            new Vector2(310f, 28f),
            14f,
            Dark,
            font);

        TMP_InputField password =
            CreateInputField(
                panel,
                "Input_JoinPassword",
                new Vector2(175f, -98f),
                new Vector2(310f, 54f),
                "ROOM PASSWORD",
                font,
                password: true);

        BindInputPlaceholder(
            password,
            "public_browser.password_placeholder");

        if (joinButton is RectTransform joinRect)
        {
            joinRect.anchoredPosition =
                new Vector2(175f, -168f);
            joinRect.sizeDelta =
                new Vector2(310f, 56f);
        }

        if (cancelButton is RectTransform cancelRect)
        {
            cancelRect.anchoredPosition =
                new Vector2(-190f, -168f);
            cancelRect.sizeDelta =
                new Vector2(270f, 56f);
        }

        if (status is RectTransform statusRect)
        {
            statusRect.anchoredPosition =
                new Vector2(0f, -240f);
            statusRect.sizeDelta =
                new Vector2(650f, 36f);
        }
    }

    private static BrowserRefs BuildBrowser(Transform canvas, TMP_FontAsset font)
    {
        BrowserRefs refs = new BrowserRefs();
        refs.Root =
            CreateStretchPanel(
                canvas,
                "PublicLobbyBrowser",
                new Color32(118, 166, 192, 255));

        CreatePanel(
            refs.Root.transform,
            "BrowserHeaderPanel",
            new Vector2(0f, 430f),
            new Vector2(1580f, 125f),
            new Color32(27, 55, 87, 255));

        CreateLocalizedText(
            refs.Root.transform,
            "Title",
            "public_browser.title",
            "PUBLIC ROOMS",
            new Vector2(0f, 455f),
            new Vector2(1000f, 66f),
            38f,
            Color.white,
            font);

        CreateLocalizedText(
            refs.Root.transform,
            "Subtitle",
            "public_browser.subtitle",
            "Browse and join online tables",
            new Vector2(0f, 410f),
            new Vector2(1000f, 36f),
            18f,
            new Color32(235, 235, 235, 255),
            font);

        GameObject filterPanel =
            CreatePanel(
                refs.Root.transform,
                "FilterPanel",
                new Vector2(0f, 325f),
                new Vector2(1600f, 100f),
                Cream);

        TMP_InputField inputTemplate = FindAnyInputField();
        TMP_Dropdown dropdownTemplate = FindAnyDropdown();

        refs.SearchInput = CloneInputField(
            inputTemplate,
            filterPanel.transform,
            "Input_SearchPublicRooms",
            new Vector2(-585f, 0f),
            new Vector2(340f, 54f));
        refs.SearchPlaceholder = GetPlaceholderText(refs.SearchInput);

        refs.MapFilter = CloneDropdown(
            dropdownTemplate,
            filterPanel.transform,
            "Dropdown_FilterMap",
            new Vector2(-245f, 0f),
            new Vector2(250f, 54f));
        refs.PlayersFilter = CloneDropdown(
            dropdownTemplate,
            filterPanel.transform,
            "Dropdown_FilterPlayers",
            new Vector2(35f, 0f),
            new Vector2(250f, 54f));
        refs.RoundFilter = CloneDropdown(
            dropdownTemplate,
            filterPanel.transform,
            "Dropdown_FilterRounds",
            new Vector2(315f, 0f),
            new Vector2(250f, 54f));
        refs.PasswordFilter = CloneDropdown(
            dropdownTemplate,
            filterPanel.transform,
            "Dropdown_FilterAccess",
            new Vector2(595f, 0f),
            new Vector2(250f, 54f));

        GameObject listPanel =
            CreatePanel(
                refs.Root.transform,
                "RoomListPanel",
                new Vector2(0f, -35f),
                new Vector2(1580f, 610f),
                Cream);

        CreatePanel(
            listPanel.transform,
            "TableHeaderBand",
            new Vector2(0f, 247f),
            new Vector2(1510f, 52f),
            Header);

        string[] keys =
        {
            "public_browser.host",
            "public_browser.map",
            "public_browser.players",
            "public_browser.rounds",
            "public_browser.region",
            "public_browser.access"
        };
        string[] fallbacks = { "HOST", "MAP", "PLAYERS", "ROUNDS", "REGION", "ACCESS" };
        float[] xs = { -590f, -330f, -105f, 105f, 290f, 465f };
        float[] widths = { 260f, 190f, 160f, 150f, 140f, 160f };

        for (int i = 0; i < keys.Length; i++)
        {
            CreateLocalizedText(
                listPanel.transform,
                $"Header_{i}",
                keys[i],
                fallbacks[i],
                new Vector2(xs[i], 245f),
                new Vector2(widths[i], 36f),
                15f,
                Color.white,
                font);
        }

        const int rowCount = 6;
        refs.RowRoots = new GameObject[rowCount];
        refs.RowButtons = new Button[rowCount];
        refs.RowHostTexts = new TMP_Text[rowCount];
        refs.RowMapTexts = new TMP_Text[rowCount];
        refs.RowPlayersTexts = new TMP_Text[rowCount];
        refs.RowRoundsTexts = new TMP_Text[rowCount];
        refs.RowRegionTexts = new TMP_Text[rowCount];
        refs.RowAccessTexts = new TMP_Text[rowCount];
        refs.RowJoinButtons = new Button[rowCount];

        for (int row = 0; row < rowCount; row++)
        {
            float y = 188f - row * 73f;
            GameObject rowRoot = CreatePanel(
                listPanel.transform,
                $"RoomRow_{row + 1}",
                new Vector2(-45f, y),
                new Vector2(1420f, 60f),
                row % 2 == 0
                    ? new Color32(250, 247, 238, 255)
                    : new Color32(232, 241, 246, 255));

            Button rowButton = Undo.AddComponent<Button>(rowRoot);
            rowButton.targetGraphic = rowRoot.GetComponent<Image>();
            Undo.AddComponent<AtlasBoardPublicLobbyRowIdentity>(rowRoot);

            refs.RowRoots[row] = rowRoot;
            refs.RowButtons[row] = rowButton;
            refs.RowHostTexts[row] = CreateRawText(rowRoot.transform, "Host", "-", new Vector2(-545f, 0f), new Vector2(250f, 38f), 17f, Dark, font);
            refs.RowMapTexts[row] = CreateRawText(rowRoot.transform, "Map", "-", new Vector2(-285f, 0f), new Vector2(180f, 38f), 17f, Dark, font);
            refs.RowPlayersTexts[row] = CreateRawText(rowRoot.transform, "Players", "-", new Vector2(-60f, 0f), new Vector2(140f, 38f), 17f, Dark, font);
            refs.RowRoundsTexts[row] = CreateRawText(rowRoot.transform, "Rounds", "-", new Vector2(150f, 0f), new Vector2(120f, 38f), 17f, Dark, font);
            refs.RowRegionTexts[row] = CreateRawText(rowRoot.transform, "Region", "-", new Vector2(335f, 0f), new Vector2(100f, 38f), 17f, Dark, font);
            refs.RowAccessTexts[row] = CreateRawText(rowRoot.transform, "Access", "-", new Vector2(510f, 0f), new Vector2(150f, 38f), 15f, Dark, font);

            Button join = CreateButton(
                rowRoot.transform,
                "Button_Join",
                "JOIN",
                new Vector2(655f, 0f),
                new Vector2(130f, 44f),
                Green,
                font);
            TMP_Text joinLabel = FindChildRecursive(join.transform, "Label")?.GetComponent<TMP_Text>();
            BindLocalized(joinLabel, "public_browser.join");
            refs.RowJoinButtons[row] = join;
            rowRoot.SetActive(false);
        }

        refs.EmptyState = new GameObject("EmptyState", typeof(RectTransform));
        refs.EmptyState.transform.SetParent(listPanel.transform, false);
        RectTransform emptyRect = refs.EmptyState.GetComponent<RectTransform>();
        emptyRect.anchorMin = emptyRect.anchorMax = emptyRect.pivot = new Vector2(0.5f, 0.5f);
        emptyRect.sizeDelta = new Vector2(900f, 100f);
        CreateLocalizedText(
            refs.EmptyState.transform,
            "EmptyText",
            "public_browser.empty",
            "No joinable public rooms found.",
            Vector2.zero,
            new Vector2(900f, 70f),
            22f,
            Dark,
            font);

        refs.StatusText = CreateRawText(
            refs.Root.transform,
            "Status",
            string.Empty,
            new Vector2(0f, -350f),
            new Vector2(950f, 40f),
            17f,
            Color.white,
            font);

        refs.BackButton = CreateButton(refs.Root.transform, "Button_Back", "<", new Vector2(-835f, -455f), new Vector2(86f, 70f), Blue, font);
        refs.CreateButton = CreateButton(refs.Root.transform, "Button_CreatePublicRoom", "CREATE PUBLIC ROOM", new Vector2(0f, -455f), new Vector2(430f, 72f), Green, font);
        BindLocalized(FindChildRecursive(refs.CreateButton.transform, "Label")?.GetComponent<TMP_Text>(), "public_browser.create");
        refs.RefreshButton = CreateButton(refs.Root.transform, "Button_Refresh", "REFRESH", new Vector2(570f, -455f), new Vector2(260f, 72f), Blue, font);
        BindLocalized(FindChildRecursive(refs.RefreshButton.transform, "Label")?.GetComponent<TMP_Text>(), "public_browser.refresh");

        BuildPasswordPrompt(refs, font);
        return refs;
    }

    private static void BuildPasswordPrompt(BrowserRefs refs, TMP_FontAsset font)
    {
        refs.PasswordPromptRoot = CreateStretchPanel(
            refs.Root.transform,
            "PublicRoomPasswordPrompt",
            new Color32(8, 12, 20, 220));
        refs.PasswordPromptRoot.AddComponent<AtlasBoardEscapeBlocker>();

        GameObject panel = CreatePanel(
            refs.PasswordPromptRoot.transform,
            "PasswordPromptPanel",
            Vector2.zero,
            new Vector2(700f, 410f),
            Cream);

        CreateLocalizedText(
            panel.transform,
            "PasswordPromptTitle",
            "public_browser.password_prompt_title",
            "PASSWORD REQUIRED",
            new Vector2(0f, 135f),
            new Vector2(560f, 50f),
            29f,
            Dark,
            font);

        refs.PasswordPromptBody = CreateRawText(
            panel.transform,
            "PasswordPromptBody",
            "Enter the room password.",
            new Vector2(0f, 65f),
            new Vector2(570f, 54f),
            18f,
            Muted,
            font);

        refs.PasswordPromptInput = CreateInputField(
            panel.transform,
            "Input_PublicRoomPassword",
            new Vector2(0f, 5f),
            new Vector2(420f, 58f),
            "ROOM PASSWORD",
            font,
            password: true);
        BindInputPlaceholder(refs.PasswordPromptInput, "public_browser.password_placeholder");

        refs.PasswordPromptStatus = CreateRawText(
            panel.transform,
            "PasswordPromptStatus",
            string.Empty,
            new Vector2(0f, -55f),
            new Vector2(570f, 38f),
            15f,
            new Color32(220, 120, 0, 255),
            font);

        refs.PasswordPromptJoin = CreateButton(
            panel.transform,
            "Button_PasswordJoin",
            "JOIN",
            new Vector2(-125f, -125f),
            new Vector2(210f, 60f),
            Green,
            font);
        BindLocalized(FindChildRecursive(refs.PasswordPromptJoin.transform, "Label")?.GetComponent<TMP_Text>(), "public_browser.join");

        refs.PasswordPromptCancel = CreateButton(
            panel.transform,
            "Button_PasswordCancel",
            "CANCEL",
            new Vector2(125f, -125f),
            new Vector2(210f, 60f),
            Muted,
            font);
        BindLocalized(FindChildRecursive(refs.PasswordPromptCancel.transform, "Label")?.GetComponent<TMP_Text>(), "common.cancel");
    }

    private static TMP_Dropdown CloneDropdown(
        TMP_Dropdown template,
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size)
    {
        if (template == null)
        {
            Debug.LogError($"Could not create {name}: no TMP_Dropdown template exists in the scene.");
            return null;
        }

        TMP_Dropdown clone = UnityEngine.Object.Instantiate(template, parent, false);
        clone.name = name;
        RectTransform rect = clone.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        clone.onValueChanged = new TMP_Dropdown.DropdownEvent();
        clone.ClearOptions();
        clone.AddOptions(new List<string> { "ALL" });
        clone.SetValueWithoutNotify(0);
        return clone;
    }

    private static TMP_InputField CloneInputField(
        TMP_InputField template,
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size)
    {
        if (template == null)
        {
            Debug.LogError($"Could not create {name}: no TMP_InputField template exists in the scene.");
            return null;
        }

        TMP_InputField clone = UnityEngine.Object.Instantiate(template, parent, false);
        clone.name = name;
        RectTransform rect = clone.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        clone.onValueChanged = new TMP_InputField.OnChangeEvent();
        clone.onSubmit = new TMP_InputField.SubmitEvent();
        clone.text = string.Empty;
        clone.contentType = TMP_InputField.ContentType.Standard;
        return clone;
    }

    private static TMP_InputField CreateInputField(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        string placeholder,
        TMP_FontAsset font,
        bool password)
    {
        TMP_InputField template = FindAnyInputField();
        TMP_InputField input = CloneInputField(template, parent, name, position, size);
        if (input == null) return null;
        input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
        TMP_Text placeholderText = GetPlaceholderText(input);
        if (placeholderText != null)
        {
            placeholderText.text = placeholder;
            placeholderText.font = font != null ? font : placeholderText.font;
        }
        return input;
    }

    private static TMP_Text GetPlaceholderText(TMP_InputField input)
    {
        return input?.placeholder as TMP_Text;
    }

    private static TMP_InputField FindAnyInputField()
    {
        TMP_InputField[] fields = Resources.FindObjectsOfTypeAll<TMP_InputField>();
        foreach (TMP_InputField field in fields)
        {
            if (field != null && field.gameObject.scene.IsValid()) return field;
        }
        return null;
    }

    private static TMP_Dropdown FindAnyDropdown()
    {
        TMP_Dropdown[] dropdowns = Resources.FindObjectsOfTypeAll<TMP_Dropdown>();
        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            if (dropdown != null && dropdown.gameObject.scene.IsValid()) return dropdown;
        }
        return null;
    }

    private static void BindInputPlaceholder(TMP_InputField input, string key)
    {
        TMP_Text placeholder = GetPlaceholderText(input);
        if (placeholder != null) BindLocalized(placeholder, key);
    }

    private static void BindLocalized(TMP_Text text, string key)
    {
        if (text == null || string.IsNullOrWhiteSpace(key)) return;
        AtlasBoardLocalizedText localized = text.GetComponent<AtlasBoardLocalizedText>();
        if (localized == null)
        {
            localized = text.gameObject.AddComponent<AtlasBoardLocalizedText>();
        }
        localized.EditorConfigure(key, text);
        EditorUtility.SetDirty(localized);
    }

    private static void MergeLocalizationEntries()
    {
        AtlasBoardLocalizationDatabase database =
            AssetDatabase.LoadAssetAtPath<AtlasBoardLocalizationDatabase>(LocalizationDatabasePath);
        if (database == null)
        {
            Debug.LogError("Localization_Default.asset missing.");
            return;
        }

        List<AtlasBoardLocalizationDatabase.Entry> entries =
            new List<AtlasBoardLocalizationDatabase.Entry>();
        foreach (AtlasBoardLocalizationDatabase.Entry entry in database.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
            if (entry.key.StartsWith(AtlasBoardPublicLobbyLocalizationSeed.Prefix, StringComparison.OrdinalIgnoreCase) ||
                entry.key == "menu.public_rooms" ||
                entry.key == "menu.public_rooms_subtitle" ||
                entry.key == "menu.play_online_subtitle" ||
                entry.key == "menu.public_table" ||
                entry.key.StartsWith("lobby.game_settings.", StringComparison.OrdinalIgnoreCase) ||
                entry.key.StartsWith("lobby.access.", StringComparison.OrdinalIgnoreCase) ||
                entry.key == "lobby.error.password_required" ||
                entry.key == "lobby.error.password_incorrect" ||
                entry.key == "lobby.error.password_length")
            {
                continue;
            }
            entries.Add(entry);
        }

        AtlasBoardPublicLobbyLocalizationSeed.Append(entries);
        database.EditorReplaceEntries(entries);
        EditorUtility.SetDirty(database);
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string fallback,
        Vector2 position,
        Vector2 size,
        Color color,
        TMP_FontAsset font)
    {
        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(AtlasBoardUIButtonAudio));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = root.GetComponent<Image>();
        image.color = color;
        Button button = root.GetComponent<Button>();
        button.targetGraphic = image;
        TMP_Text label = CreateRawText(
            root.transform,
            "Label",
            fallback,
            Vector2.zero,
            size - new Vector2(24f, 14f),
            21f,
            Color.white,
            font);
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 11f;
        label.fontSizeMax = 22f;
        return button;
    }

    private static GameObject CreateStretchPanel(Transform parent, string name, Color color)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = color;
        return root;
    }

    private static GameObject CreatePanel(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        root.GetComponent<Image>().color = color;
        return root;
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
        TMP_FontAsset font)
    {
        TMP_Text text = CreateRawText(parent, name, fallback, position, size, fontSize, color, font);
        BindLocalized(text, key);
        return text;
    }

    private static TMP_Text CreateRawText(
        Transform parent,
        string name,
        string value,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        TMP_FontAsset font)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        if (font != null) text.font = font;
        return text;
    }


    private static void NudgeTextsContaining(Transform root, string contains, Vector2 delta)
    {
        if (root == null || string.IsNullOrWhiteSpace(contains))
        {
            return;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text == null || text.rectTransform == null)
            {
                continue;
            }

            string value = text.text ?? string.Empty;
            if (value.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                text.rectTransform.anchoredPosition += delta;
            }
        }
    }

    private static TMP_FontAsset FindSceneFont()
    {
        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (TMP_Text text in texts)
        {
            if (text != null && text.gameObject.scene.IsValid() && text.font != null)
            {
                return text.font;
            }
        }
        return null;
    }

    private static GameObject FindSceneObject(string name)
    {
        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject item in all)
        {
            if (item != null && item.scene.IsValid() && item.name == name) return item;
        }
        return null;
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private static void DestroyChildIfPresent(Transform root, string name)
    {
        Transform item = FindChildRecursive(root, name);
        if (item != null) Undo.DestroyObjectImmediate(item.gameObject);
    }

    private sealed class BrowserRefs
    {
        public GameObject Root;
        public TMP_InputField SearchInput;
        public TMP_Text SearchPlaceholder;
        public TMP_Dropdown MapFilter;
        public TMP_Dropdown PlayersFilter;
        public TMP_Dropdown RoundFilter;
        public TMP_Dropdown PasswordFilter;
        public TMP_Text StatusText;
        public GameObject EmptyState;
        public Button BackButton;
        public Button RefreshButton;
        public Button CreateButton;
        public GameObject PasswordPromptRoot;
        public TMP_Text PasswordPromptBody;
        public TMP_InputField PasswordPromptInput;
        public TMP_Text PasswordPromptStatus;
        public Button PasswordPromptJoin;
        public Button PasswordPromptCancel;
        public GameObject[] RowRoots;
        public Button[] RowButtons;
        public TMP_Text[] RowHostTexts;
        public TMP_Text[] RowMapTexts;
        public TMP_Text[] RowPlayersTexts;
        public TMP_Text[] RowRoundsTexts;
        public TMP_Text[] RowRegionTexts;
        public TMP_Text[] RowAccessTexts;
        public Button[] RowJoinButtons;
    }
}
#endif
