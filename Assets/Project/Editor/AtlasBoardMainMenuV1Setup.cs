#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class AtlasBoardMainMenuV1Setup
{
    private const string CanvasName =
        "Canvas_MainMenu";

    private static TMP_FontAsset defaultFont;
    private static Sprite kenneyButtonSprite;
    private static Sprite kenneyPanelSprite;
    private static Sprite kenneyCircleSprite;

    private static readonly Color BackgroundBlue =
        new Color32(151, 205, 232, 255);

    private static readonly Color LobbyBackground =
        new Color32(145, 145, 165, 255);

    private static readonly Color Blue =
        new Color32(28, 157, 211, 255);

    private static readonly Color DarkBlue =
        new Color32(19, 113, 163, 255);

    private static readonly Color Green =
        new Color32(139, 170, 16, 255);

    private static readonly Color Orange =
        new Color32(248, 175, 0, 255);

    private static readonly Color Red =
        new Color32(226, 39, 90, 255);

    private static readonly Color Cream =
        new Color32(246, 241, 231, 255);

    private static readonly Color TextDark =
        new Color32(61, 62, 66, 255);

    [MenuItem(
        "Atlas Board/UI/Build or Refresh Main Menu + Lobby v1.3")]
    public static void BuildOrRefresh()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "Exit Play Mode before building the Main Menu.");

            return;
        }

        ResolveStyleAssets();

        GameObject oldCanvas =
            FindSceneObject(
                CanvasName);

        if (oldCanvas != null)
        {
            Undo.DestroyObjectImmediate(
                oldCanvas);
        }

        GameObject canvasObject =
            new GameObject(
                CanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

        Undo.RegisterCreatedObjectUndo(
            canvasObject,
            "Build AtlasBoard Main Menu");

        Canvas canvas =
            canvasObject.GetComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 500;

        CanvasScaler scaler =
            canvasObject.GetComponent<
                CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode
                .ScaleWithScreenSize;

        scaler.referenceResolution =
            new Vector2(
                1920f,
                1080f);

        scaler.matchWidthOrHeight =
            0.5f;

        AtlasBoardMainMenuController controller =
            canvasObject.AddComponent<
                AtlasBoardMainMenuController>();

        AtlasBoardMatchSetupBridge bridge =
            canvasObject.AddComponent<
                AtlasBoardMatchSetupBridge>();

        GameObject mainMenu =
            BuildMainMenu(
                canvasObject.transform,
                controller);

        LobbyRefs lobby =
            BuildLobby(
                canvasObject.transform,
                controller);

        ModalRefs modal =
            BuildModal(
                canvasObject.transform,
                controller);

        GameObject existingMatchSetup =
            FindSceneObject(
                "Canvas_MatchSetup");

        GameObject existingTablet =
            FindSceneObject(
                "Canvas_TabletUI");

        GameObject existingOverlay =
            FindSceneObject(
                "Canvas_UXOverlay");

        bridge.EditorConfigure(
            existingMatchSetup);

        controller.EditorConfigure(
            mainMenu,
            lobby.Root,
            modal.Root,
            modal.Title,
            modal.Body,
            lobby.ProfileName,
            lobby.ProfileCash,
            lobby.ProfileGold,
            lobby.Title,
            lobby.MapDropdown,
            lobby.PlayerCountDropdown,
            lobby.RoundDropdown,
            lobby.ThemeDropdown,
            lobby.BalancedToggle,
            lobby.DoublesToggle,
            lobby.TriplePenaltyToggle,
            lobby.Player1Dropdown,
            lobby.Player2Dropdown,
            lobby.Player3Dropdown,
            lobby.Player4Dropdown,
            lobby.Player3Row,
            lobby.Player4Row,
            existingMatchSetup,
            existingTablet,
            existingOverlay,
            bridge);

        EnsureEventSystem();

        mainMenu.SetActive(true);
        lobby.Root.SetActive(false);
        modal.Root.SetActive(false);

        EditorUtility.SetDirty(
            controller);

        EditorUtility.SetDirty(
            bridge);

        if (canvasObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                canvasObject.scene);
        }

        Selection.activeGameObject =
            canvasObject;

        Debug.Log(
            "AtlasBoard Main Menu + Lobby v1.3 built. " +
            "The existing Canvas_MatchSetup was not deleted or modified. " +
            "Press Play to enter through the new Main Menu.");
    }

    private static GameObject BuildMainMenu(
        Transform parent,
        AtlasBoardMainMenuController controller)
    {
        GameObject root =
            CreateFullScreenPanel(
                parent,
                "MainMenu",
                BackgroundBlue);

        // Decorative translucent board-like squares.
        CreateDecorSquare(
            root.transform,
            new Vector2(260f, 250f),
            28f);

        CreateDecorSquare(
            root.transform,
            new Vector2(1630f, 820f),
            -22f);

        CreateDecorSquare(
            root.transform,
            new Vector2(1500f, 180f),
            16f);

        CreateDecorSquare(
            root.transform,
            new Vector2(420f, 820f),
            -12f);

        CreateRibbon(
            root.transform,
            "TitleRibbon",
            "ATLAS BOARD",
            new Vector2(960f, 100f),
            new Vector2(760f, 105f),
            Orange);

        ProfileRefs profile =
            CreateProfileCard(
                root.transform,
                new Vector2(245f, 110f));

        // Main action cards.
        GameObject privateButton =
            CreateMainCardButton(
                root.transform,
                "PrivateTableCard",
                "PRIVATE TABLE",
                "LOCAL / PRIVATE",
                new Vector2(430f, 510f),
                new Vector2(430f, 320f),
                Blue);

        GameObject playButton =
            CreateMainCardButton(
                root.transform,
                "PlayCard",
                "PLAY",
                "SOLO / BOTS",
                new Vector2(960f, 510f),
                new Vector2(430f, 320f),
                Green);

        GameObject shopButton =
            CreateMainCardButton(
                root.transform,
                "ShopCard",
                "SHOP",
                "COSMETICS / ITEMS",
                new Vector2(1490f, 510f),
                new Vector2(430f, 320f),
                Orange);

        UnityEventTools.AddPersistentListener(
            privateButton
                .GetComponent<Button>()
                .onClick,
            controller.OpenPrivateLobby);

        UnityEventTools.AddPersistentListener(
            playButton
                .GetComponent<Button>()
                .onClick,
            controller.OpenPlayLobby);

        UnityEventTools.AddPersistentListener(
            shopButton
                .GetComponent<Button>()
                .onClick,
            controller.OpenShop);

        // Settings and quit, upper-right.
        GameObject settings =
            CreateCircleButton(
                root.transform,
                "Button_Settings",
                "SET",
                new Vector2(1700f, 95f),
                Blue);

        GameObject quit =
            CreateCircleButton(
                root.transform,
                "Button_Quit",
                "X",
                new Vector2(1815f, 95f),
                Red);

        UnityEventTools.AddPersistentListener(
            settings
                .GetComponent<Button>()
                .onClick,
            controller.OpenSettings);

        UnityEventTools.AddPersistentListener(
            quit
                .GetComponent<Button>()
                .onClick,
            controller.QuitGame);

        GameObject profileButton =
            profile.Root;

        Button profileButtonComponent =
            profileButton.AddComponent<Button>();

        profileButtonComponent.targetGraphic =
            profileButton.GetComponent<Image>();

        UnityEventTools.AddPersistentListener(
            profileButtonComponent.onClick,
            controller.OpenProfile);

        CreateText(
            root.transform,
            "FooterVersion",
            "Prototype • Main Menu v1",
            new Vector2(960f, 1035f),
            new Vector2(420f, 40f),
            21f,
            TextDark,
            FontStyles.Normal,
            TextAlignmentOptions.Center);

        return root;
    }

    private static LobbyRefs BuildLobby(
        Transform parent,
        AtlasBoardMainMenuController controller)
    {
        LobbyRefs refs =
            new LobbyRefs();

        refs.Root =
            CreateFullScreenPanel(
                parent,
                "Lobby",
                LobbyBackground);

        CreateRibbon(
            refs.Root.transform,
            "LobbyRibbon",
            "GAME LOBBY",
            new Vector2(960f, 105f),
            new Vector2(780f, 105f),
            Orange);

        ProfileRefs profile =
            CreateProfileCard(
                refs.Root.transform,
                new Vector2(245f, 110f));

        refs.ProfileName =
            profile.Name;

        refs.ProfileCash =
            profile.Cash;

        refs.ProfileGold =
            profile.Gold;

        refs.Title =
            CreateText(
                refs.Root.transform,
                "LobbyModeTitle",
                "PLAY",
                new Vector2(960f, 190f),
                new Vector2(520f, 60f),
                34f,
                Cream,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        GameObject settingsPanel =
            CreatePanel(
                refs.Root.transform,
                "Panel_GameSettings",
                new Vector2(485f, 580f),
                new Vector2(720f, 700f),
                new Color32(233, 233, 239, 245));

        CreateSectionHeader(
            settingsPanel.transform,
            "GAME SETTINGS",
            new Vector2(0f, -305f),
            new Vector2(650f, 58f));

        refs.MapDropdown =
            CreateLabeledDropdown(
                settingsPanel.transform,
                "Map",
                "Map",
                new[]
                {
                    "Turkey",
                    "Colorado",
                    "USA"
                },
                0,
                new Vector2(0f, -210f));

        refs.PlayerCountDropdown =
            CreateLabeledDropdown(
                settingsPanel.transform,
                "PlayerCount",
                "Player Count",
                new[]
                {
                    "2",
                    "3",
                    "4"
                },
                0,
                new Vector2(0f, -130f));

        refs.RoundDropdown =
            CreateLabeledDropdown(
                settingsPanel.transform,
                "RoundLimit",
                "Round Limit",
                new[]
                {
                    "10",
                    "15",
                    "20",
                    "30"
                },
                2,
                new Vector2(0f, -50f));

        refs.ThemeDropdown =
            CreateLabeledDropdown(
                settingsPanel.transform,
                "EnvironmentTheme",
                "Environment Theme",
                new[]
                {
                    "Classic Table",
                    "Garden",
                    "Beach",
                    "Pavilion",
                    "Street"
                },
                0,
                new Vector2(0f, 30f));

        refs.BalancedToggle =
            CreateLabeledToggle(
                settingsPanel.transform,
                "BalancedDevelopment",
                "Balanced Development",
                true,
                new Vector2(0f, 125f));

        refs.DoublesToggle =
            CreateLabeledToggle(
                settingsPanel.transform,
                "DoublesEnabled",
                "Doubles",
                true,
                new Vector2(0f, 205f));

        refs.TriplePenaltyToggle =
            CreateLabeledToggle(
                settingsPanel.transform,
                "TripleDoublePenalty",
                "Triple Double Penalty",
                true,
                new Vector2(0f, 285f));

        GameObject playersPanel =
            CreatePanel(
                refs.Root.transform,
                "Panel_Players",
                new Vector2(1355f, 580f),
                new Vector2(720f, 700f),
                new Color32(233, 233, 239, 245));

        CreateSectionHeader(
            playersPanel.transform,
            "PLAYERS",
            new Vector2(0f, -305f),
            new Vector2(650f, 58f));

        PlayerRowRefs p1 =
            CreatePlayerRow(
                playersPanel.transform,
                "P1",
                "PLAYER 1",
                1,
                new Vector2(0f, -190f),
                0);

        PlayerRowRefs p2 =
            CreatePlayerRow(
                playersPanel.transform,
                "P2",
                "PLAYER 2",
                2,
                new Vector2(0f, -60f),
                1);

        PlayerRowRefs p3 =
            CreatePlayerRow(
                playersPanel.transform,
                "P3",
                "PLAYER 3",
                3,
                new Vector2(0f, 70f),
                1);

        PlayerRowRefs p4 =
            CreatePlayerRow(
                playersPanel.transform,
                "P4",
                "PLAYER 4",
                4,
                new Vector2(0f, 200f),
                1);

        refs.Player1Dropdown =
            p1.Dropdown;

        refs.Player2Dropdown =
            p2.Dropdown;

        refs.Player3Dropdown =
            p3.Dropdown;

        refs.Player4Dropdown =
            p4.Dropdown;

        refs.Player3Row =
            p3.Root;

        refs.Player4Row =
            p4.Root;

        UnityEventTools.AddIntPersistentListener(
            refs.PlayerCountDropdown
                .onValueChanged,
            controller.OnPlayerCountChanged,
            0);

        GameObject back =
            CreateCircleButton(
                refs.Root.transform,
                "Button_Back",
                "<",
                new Vector2(95f, 1005f),
                Blue);

        UnityEventTools.AddPersistentListener(
            back.GetComponent<Button>().onClick,
            controller.BackFromLobby);

        GameObject start =
            CreateWideButton(
                refs.Root.transform,
                "Button_StartMatch",
                "START MATCH",
                new Vector2(1590f, 1005f),
                new Vector2(430f, 90f),
                Green);

        UnityEventTools.AddPersistentListener(
            start.GetComponent<Button>().onClick,
            controller.StartMatch);

        return refs;
    }

    private static ModalRefs BuildModal(
        Transform parent,
        AtlasBoardMainMenuController controller)
    {
        ModalRefs refs =
            new ModalRefs();

        refs.Root =
            CreateFullScreenPanel(
                parent,
                "Modal",
                new Color32(23, 31, 45, 150));

        GameObject window =
            CreatePanel(
                refs.Root.transform,
                "ModalWindow",
                new Vector2(960f, 540f),
                new Vector2(720f, 410f),
                Cream);

        CreateImage(
            window.transform,
            "Header",
            new Vector2(0f, -165f),
            new Vector2(720f, 80f),
            Blue);

        refs.Title =
            CreateText(
                window.transform,
                "ModalTitle",
                "SETTINGS",
                new Vector2(0f, -165f),
                new Vector2(620f, 60f),
                34f,
                Color.white,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        refs.Body =
            CreateText(
                window.transform,
                "ModalBody",
                string.Empty,
                new Vector2(0f, 10f),
                new Vector2(590f, 180f),
                25f,
                TextDark,
                FontStyles.Normal,
                TextAlignmentOptions.Center);

        GameObject close =
            CreateWideButton(
                window.transform,
                "Button_Close",
                "CLOSE",
                new Vector2(0f, 145f),
                new Vector2(260f, 65f),
                Red);

        UnityEventTools.AddPersistentListener(
            close.GetComponent<Button>().onClick,
            controller.CloseModal);

        return refs;
    }

    private static ProfileRefs CreateProfileCard(
        Transform parent,
        Vector2 position)
    {
        ProfileRefs refs =
            new ProfileRefs();

        refs.Root =
            CreatePanel(
                parent,
                "ProfileCard",
                position,
                new Vector2(455f, 155f),
                Cream);

        CreateImage(
            refs.Root.transform,
            "Avatar",
            new Vector2(-160f, 0f),
            new Vector2(118f, 118f),
            new Color32(68, 153, 203, 255));

        CreateText(
            refs.Root.transform,
            "AvatarInitial",
            "A",
            new Vector2(-160f, 0f),
            new Vector2(95f, 95f),
            50f,
            Color.white,
            FontStyles.Bold,
            TextAlignmentOptions.Center);

        refs.Name =
            CreateText(
                refs.Root.transform,
                "ProfileName",
                "PLAYER",
                new Vector2(70f, -42f),
                new Vector2(235f, 42f),
                29f,
                TextDark,
                FontStyles.Bold,
                TextAlignmentOptions.Left);

        refs.Cash =
            CreateText(
                refs.Root.transform,
                "ProfileCash",
                "$ 1,500",
                new Vector2(70f, 8f),
                new Vector2(235f, 34f),
                23f,
                TextDark,
                FontStyles.Normal,
                TextAlignmentOptions.Left);

        refs.Gold =
            CreateText(
                refs.Root.transform,
                "ProfileGold",
                "G 100",
                new Vector2(70f, 48f),
                new Vector2(235f, 34f),
                23f,
                TextDark,
                FontStyles.Normal,
                TextAlignmentOptions.Left);

        return refs;
    }

    private static GameObject CreateMainCardButton(
        Transform parent,
        string name,
        string title,
        string subtitle,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject root =
            CreatePanel(
                parent,
                name,
                position,
                size,
                new Color32(245, 242, 232, 245));

        Button button =
            root.AddComponent<Button>();

        button.targetGraphic =
            root.GetComponent<Image>();

        ColorBlock colors =
            button.colors;

        colors.normalColor =
            Color.white;

        colors.highlightedColor =
            new Color(1f, 1f, 1f, 0.92f);

        colors.pressedColor =
            new Color(0.9f, 0.9f, 0.9f, 1f);

        button.colors = colors;

        CreateImage(
            root.transform,
            "IllustrationPlate",
            new Vector2(0f, -40f),
            new Vector2(280f, 145f),
            new Color(
                color.r,
                color.g,
                color.b,
                0.18f));

        CreateText(
            root.transform,
            "IllustrationGlyph",
            title == "PLAY"
                ? "GO"
                : title == "SHOP"
                    ? "$"
                    : "PVT",
            new Vector2(0f, -40f),
            new Vector2(240f, 110f),
            title == "PRIVATE TABLE"
                ? 42f
                : 56f,
            color,
            FontStyles.Bold,
            TextAlignmentOptions.Center);

        CreateImage(
            root.transform,
            "Ribbon",
            new Vector2(0f, 96f),
            new Vector2(390f, 78f),
            color);

        CreateText(
            root.transform,
            "Title",
            title,
            new Vector2(0f, 94f),
            new Vector2(350f, 55f),
            34f,
            Color.white,
            FontStyles.Bold,
            TextAlignmentOptions.Center);

        CreateText(
            root.transform,
            "Subtitle",
            subtitle,
            new Vector2(0f, 169f),
            new Vector2(340f, 30f),
            18f,
            TextDark,
            FontStyles.Normal,
            TextAlignmentOptions.Center);

        return root;
    }

    private static GameObject CreateCircleButton(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        Color color)
    {
        GameObject root =
            CreateImage(
                parent,
                name,
                position,
                new Vector2(92f, 92f),
                color,
                kenneyCircleSprite ??
                kenneyButtonSprite);

        Button button =
            root.AddComponent<Button>();

        button.targetGraphic =
            root.GetComponent<Image>();

        CreateText(
            root.transform,
            "Label",
            label,
            Vector2.zero,
            new Vector2(72f, 72f),
            label.Length > 1
                ? 21f
                : 45f,
            Color.white,
            FontStyles.Bold,
            TextAlignmentOptions.Center);

        return root;
    }

    private static GameObject CreateWideButton(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject root =
            CreateImage(
                parent,
                name,
                position,
                size,
                color,
                kenneyButtonSprite);

        Button button =
            root.AddComponent<Button>();

        button.targetGraphic =
            root.GetComponent<Image>();

        CreateText(
            root.transform,
            "Label",
            label,
            Vector2.zero,
            new Vector2(
                size.x - 40f,
                size.y - 15f),
            30f,
            Color.white,
            FontStyles.Bold,
            TextAlignmentOptions.Center);

        return root;
    }

    private static TMP_Dropdown CreateLabeledDropdown(
        Transform parent,
        string name,
        string label,
        string[] options,
        int defaultIndex,
        Vector2 position)
    {
        CreateText(
            parent,
            $"Label_{name}",
            label,
            new Vector2(-215f, position.y),
            new Vector2(240f, 45f),
            23f,
            TextDark,
            FontStyles.Bold,
            TextAlignmentOptions.Left);

        TMP_Dropdown dropdown =
            CreateDropdown(
                parent,
                $"Dropdown_{name}",
                new Vector2(155f, position.y),
                new Vector2(330f, 54f),
                options,
                defaultIndex);

        return dropdown;
    }

    private static ToggleProxy CreateLabeledToggle(
        Transform parent,
        string name,
        string label,
        bool initialValue,
        Vector2 position)
    {
        GameObject row =
            new GameObject(
                $"Toggle_{name}",
                typeof(RectTransform));

        row.transform.SetParent(
            parent,
            false);

        SetRect(
            row.GetComponent<RectTransform>(),
            position,
            new Vector2(560f, 55f));

        CreateText(
            row.transform,
            "Label",
            label,
            new Vector2(-100f, 0f),
            new Vector2(350f, 45f),
            23f,
            TextDark,
            FontStyles.Bold,
            TextAlignmentOptions.Left);

        GameObject box =
            CreateImage(
                row.transform,
                "Box",
                new Vector2(225f, 0f),
                new Vector2(46f, 46f),
                Color.white,
                kenneyButtonSprite);

        Toggle toggle =
            row.AddComponent<Toggle>();

        toggle.targetGraphic =
            box.GetComponent<Image>();

        GameObject mark =
            CreateImage(
                box.transform,
                "Checkmark",
                Vector2.zero,
                new Vector2(30f, 30f),
                Green);

        toggle.graphic =
            mark.GetComponent<Image>();

        toggle.isOn =
            initialValue;

        ToggleProxy proxy =
            row.AddComponent<ToggleProxy>();

        proxy.EditorConfigure(
            toggle);

        return proxy;
    }

    private static PlayerRowRefs CreatePlayerRow(
        Transform parent,
        string id,
        string label,
        int slot,
        Vector2 position,
        int defaultTypeIndex)
    {
        PlayerRowRefs refs =
            new PlayerRowRefs();

        refs.Root =
            CreatePanel(
                parent,
                $"PlayerRow_{id}",
                position,
                new Vector2(620f, 105f),
                Cream);

        Color slotColor =
            slot == 1
                ? new Color32(125, 72, 190, 255)
                : slot == 2
                    ? new Color32(29, 184, 176, 255)
                    : slot == 3
                        ? new Color32(234, 145, 35, 255)
                        : new Color32(211, 62, 74, 255);

        CreateImage(
            refs.Root.transform,
            "SlotColor",
            new Vector2(-265f, 0f),
            new Vector2(62f, 62f),
            slotColor,
            kenneyCircleSprite);

        CreateText(
            refs.Root.transform,
            "PlayerLabel",
            label,
            new Vector2(-105f, -18f),
            new Vector2(240f, 38f),
            25f,
            TextDark,
            FontStyles.Bold,
            TextAlignmentOptions.Left);

        CreateText(
            refs.Root.transform,
            "Status",
            slot == 1
                ? "LOCAL PLAYER"
                : "READY",
            new Vector2(-105f, 20f),
            new Vector2(240f, 30f),
            18f,
            new Color32(102, 104, 111, 255),
            FontStyles.Normal,
            TextAlignmentOptions.Left);

        refs.Dropdown =
            CreateDropdown(
                refs.Root.transform,
                $"Dropdown_{id}_Type",
                new Vector2(190f, 0f),
                new Vector2(190f, 52f),
                new[]
                {
                    "Human",
                    "Bot"
                },
                defaultTypeIndex);

        return refs;
    }

    private static TMP_Dropdown CreateDropdown(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        string[] options,
        int defaultIndex)
    {
        GameObject root =
            CreateImage(
                parent,
                name,
                position,
                size,
                Color.white,
                null);

        TMP_Dropdown dropdown =
            root.AddComponent<
                TMP_Dropdown>();

        TMP_Text caption =
            CreateText(
                root.transform,
                "Label",
                string.Empty,
                new Vector2(-18f, 0f),
                new Vector2(size.x - 65f, size.y - 8f),
                22f,
                TextDark,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);

        TMP_Text arrow =
            CreateText(
                root.transform,
                "Arrow",
                "v",
                new Vector2(size.x * 0.5f - 28f, 0f),
                new Vector2(35f, 35f),
                22f,
                TextDark,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        int visibleRows =
            Mathf.Clamp(
                options.Length,
                1,
                6);

        float templateHeight =
            visibleRows * 64f +
            28f;

        GameObject template =
            CreateImage(
                root.transform,
                "Template",
                new Vector2(0f, size.y),
                new Vector2(size.x, templateHeight),
                Color.white,
                null);

        RectTransform templateRect =
            template.GetComponent<
                RectTransform>();

        templateRect.pivot =
            new Vector2(0.5f, 1f);

        template.SetActive(false);

        ScrollRect scrollRect =
            template.AddComponent<
                ScrollRect>();

        GameObject viewport =
            new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(RectMask2D));

        viewport.transform.SetParent(
            template.transform,
            false);

        RectTransform viewportRect =
            viewport.GetComponent<
                RectTransform>();

        viewportRect.anchorMin =
            Vector2.zero;

        viewportRect.anchorMax =
            Vector2.one;

        viewportRect.offsetMin =
            new Vector2(4f, 4f);

        viewportRect.offsetMax =
            new Vector2(-4f, -4f);

        GameObject content =
            new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));

        content.transform.SetParent(
            viewport.transform,
            false);

        RectTransform contentRect =
            content.GetComponent<
                RectTransform>();

        contentRect.anchorMin =
            new Vector2(0f, 1f);

        contentRect.anchorMax =
            new Vector2(1f, 1f);

        contentRect.pivot =
            new Vector2(0.5f, 1f);

        contentRect.sizeDelta =
            new Vector2(0f, 0f);

        VerticalLayoutGroup layout =
            content.GetComponent<
                VerticalLayoutGroup>();

        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.spacing = 2f;

        ContentSizeFitter fitter =
            content.GetComponent<
                ContentSizeFitter>();

        fitter.verticalFit =
            ContentSizeFitter.FitMode
                .PreferredSize;

        GameObject item =
            CreateImage(
                content.transform,
                "Item",
                Vector2.zero,
                new Vector2(size.x - 8f, 60f),
                Color.white);

        LayoutElement itemLayout =
            item.AddComponent<
                LayoutElement>();

        itemLayout.preferredHeight =
            60f;

        Toggle itemToggle =
            item.AddComponent<Toggle>();

        itemToggle.targetGraphic =
            item.GetComponent<Image>();

        TMP_Text itemLabel =
            CreateText(
                item.transform,
                "Item Label",
                "Option",
                new Vector2(4f, 0f),
                new Vector2(size.x - 40f, 38f),
                20f,
                TextDark,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);

        ColorBlock itemColors =
            itemToggle.colors;

        itemColors.normalColor =
            Color.white;

        itemColors.highlightedColor =
            new Color32(
                226,
                244,
                252,
                255);

        itemColors.pressedColor =
            new Color32(
                205,
                232,
                245,
                255);

        itemColors.selectedColor =
            Color.white;

        itemToggle.colors =
            itemColors;

        // No bright green check-square in the dropdown list.
        itemToggle.graphic =
            null;

        scrollRect.viewport =
            viewportRect;

        scrollRect.content =
            contentRect;

        scrollRect.horizontal =
            false;

        scrollRect.scrollSensitivity =
            28f;

        dropdown.targetGraphic =
            root.GetComponent<Image>();

        ColorBlock dropdownColors =
            dropdown.colors;

        dropdownColors.normalColor =
            Color.white;

        dropdownColors.highlightedColor =
            new Color32(
                244,
                250,
                253,
                255);

        dropdownColors.pressedColor =
            new Color32(
                228,
                242,
                249,
                255);

        dropdownColors.selectedColor =
            Color.white;

        dropdownColors.disabledColor =
            new Color32(
                238,
                238,
                238,
                255);

        dropdown.colors =
            dropdownColors;

        dropdown.captionText =
            caption;

        dropdown.template =
            templateRect;

        dropdown.itemText =
            itemLabel;

        dropdown.options.Clear();

        foreach (string option
                 in options)
        {
            dropdown.options.Add(
                new TMP_Dropdown.OptionData(
                    option));
        }

        dropdown.value =
            Mathf.Clamp(
                defaultIndex,
                0,
                Mathf.Max(
                    0,
                    options.Length - 1));

        dropdown.RefreshShownValue();

        // Arrow exists as visual-only child.
        arrow.raycastTarget = false;

        return dropdown;
    }

    private static void CreateSectionHeader(
        Transform parent,
        string text,
        Vector2 position,
        Vector2 size)
    {
        CreateImage(
            parent,
            $"Header_{text.Replace(" ", string.Empty)}",
            position,
            size,
            new Color32(137, 185, 218, 255));

        CreateText(
            parent,
            $"HeaderText_{text.Replace(" ", string.Empty)}",
            text,
            position,
            new Vector2(size.x - 30f, size.y - 8f),
            28f,
            TextDark,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
    }

    private static void CreateRibbon(
        Transform parent,
        string name,
        string text,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        CreateImage(
            parent,
            name,
            position,
            size,
            color,
            kenneyButtonSprite);

        CreateText(
            parent,
            $"{name}_Text",
            text,
            position,
            new Vector2(size.x - 80f, size.y - 20f),
            47f,
            Color.white,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
    }

    private static GameObject CreateFullScreenPanel(
        Transform parent,
        string name,
        Color color)
    {
        GameObject root =
            CreateImage(
                parent,
                name,
                Vector2.zero,
                new Vector2(1920f, 1080f),
                color);

        RectTransform rect =
            root.GetComponent<RectTransform>();

        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;

        return root;
    }

    private static GameObject CreatePanel(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        return CreateImage(
            parent,
            name,
            position,
            size,
            color,
            kenneyPanelSprite);
    }

    private static GameObject CreateImage(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color,
        Sprite sprite = null)
    {
        GameObject obj =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));

        obj.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            obj.GetComponent<RectTransform>();

        SetRect(
            rect,
            position,
            size);

        Image image =
            obj.GetComponent<Image>();

        image.color = color;

        if (sprite != null)
        {
            image.sprite = sprite;
            image.type =
                sprite.border != Vector4.zero
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
        }

        return obj;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject obj =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));

        obj.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            obj.GetComponent<RectTransform>();

        SetRect(
            rect,
            position,
            size);

        TextMeshProUGUI text =
            obj.GetComponent<
                TextMeshProUGUI>();

        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        if (defaultFont != null)
        {
            text.font = defaultFont;
        }

        return text;
    }

    private static void CreateDecorSquare(
        Transform parent,
        Vector2 position,
        float rotation)
    {
        GameObject square =
            CreateImage(
                parent,
                "DecorSquare",
                position,
                new Vector2(65f, 65f),
                new Color32(255, 255, 255, 24));

        square.transform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                rotation);
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin =
            new Vector2(0.5f, 0.5f);

        rect.anchorMax =
            new Vector2(0.5f, 0.5f);

        rect.pivot =
            new Vector2(0.5f, 0.5f);

        bool parentUsesAbsoluteScreenDesign =
            rect.parent != null &&
            (rect.parent.name == "MainMenu" ||
             rect.parent.name == "Lobby" ||
             rect.parent.name == "Modal");

        if (parentUsesAbsoluteScreenDesign)
        {
            // 1920x1080 design coordinates, origin at top-left.
            rect.anchoredPosition =
                new Vector2(
                    position.x - 960f,
                    540f - position.y);
        }
        else
        {
            // Local design coordinates inside cards/panels:
            // +X = right, +Y = downward.
            rect.anchoredPosition =
                new Vector2(
                    position.x,
                    -position.y);
        }

        rect.sizeDelta = size;
    }

    private static void ResolveStyleAssets()
    {
        defaultFont =
            TMP_Settings.defaultFontAsset;

        List<Sprite> kenneySprites =
            LoadKenneySprites();

        kenneyButtonSprite =
            FindBestSprite(
                kenneySprites,
                "button",
                "rectangle");

        kenneyPanelSprite =
            FindBestSprite(
                kenneySprites,
                "panel");

        kenneyCircleSprite =
            FindBestSprite(
                kenneySprites,
                "button",
                "circle");

        if (kenneyButtonSprite != null ||
            kenneyPanelSprite != null ||
            kenneyCircleSprite != null)
        {
            Debug.Log(
                "Kenney UI sprites detected and used by Main Menu v1.");
        }
        else
        {
            Debug.Log(
                "Kenney UI sprites were not detected. Main Menu v1 uses " +
                "clean fallback panels. Import Kenney UI Pack and rerun " +
                "Build or Refresh to apply available sprites.");
        }
    }

    private static List<Sprite> LoadKenneySprites()
    {
        List<Sprite> result =
            new List<Sprite>();

        string[] guids =
            AssetDatabase.FindAssets(
                "t:Texture2D");

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            if (string.IsNullOrWhiteSpace(path) ||
                path.IndexOf(
                    "kenney",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            UnityEngine.Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(
                    path);

            foreach (UnityEngine.Object asset
                     in assets)
            {
                if (asset is Sprite sprite)
                {
                    result.Add(sprite);
                }
            }
        }

        return result;
    }

    private static Sprite FindBestSprite(
        List<Sprite> sprites,
        params string[] terms)
    {
        if (sprites == null ||
            sprites.Count == 0)
        {
            return null;
        }

        IEnumerable<Sprite> greyFirst =
            sprites.OrderByDescending(
                sprite =>
                {
                    string path =
                        AssetDatabase
                            .GetAssetPath(sprite)
                            .Replace("\\", "/")
                            .ToLowerInvariant();

                    return path.Contains(
                        "/grey/")
                            ? 1
                            : 0;
                });

        foreach (Sprite sprite in greyFirst)
        {
            string name =
                sprite.name.ToLowerInvariant();

            bool allMatch =
                terms.All(
                    term =>
                        name.Contains(
                            term.ToLowerInvariant()));

            if (allMatch)
            {
                return sprite;
            }
        }

        foreach (Sprite sprite in greyFirst)
        {
            string name =
                sprite.name.ToLowerInvariant();

            if (terms.Any(
                    term =>
                        name.Contains(
                            term.ToLowerInvariant())))
            {
                return sprite;
            }
        }

        return null;
    }

    private static GameObject FindSceneObject(
        string name)
    {
        GameObject[] all =
            Resources.FindObjectsOfTypeAll<
                GameObject>();

        foreach (GameObject item in all)
        {
            if (item != null &&
                item.scene.IsValid() &&
                item.name == name)
            {
                return item;
            }
        }

        return null;
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing =
            UnityEngine.Object
                .FindAnyObjectByType<
                    EventSystem>();

        if (existing != null)
        {
            return;
        }

        GameObject eventSystem =
            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));

        Undo.RegisterCreatedObjectUndo(
            eventSystem,
            "Create EventSystem");
    }

    private class ProfileRefs
    {
        public GameObject Root;
        public TMP_Text Name;
        public TMP_Text Cash;
        public TMP_Text Gold;
    }

    private class PlayerRowRefs
    {
        public GameObject Root;
        public TMP_Dropdown Dropdown;
    }

    private class LobbyRefs
    {
        public GameObject Root;
        public TMP_Text ProfileName;
        public TMP_Text ProfileCash;
        public TMP_Text ProfileGold;

        public TMP_Text Title;

        public TMP_Dropdown MapDropdown;
        public TMP_Dropdown PlayerCountDropdown;
        public TMP_Dropdown RoundDropdown;
        public TMP_Dropdown ThemeDropdown;

        public ToggleProxy BalancedToggle;
        public ToggleProxy DoublesToggle;
        public ToggleProxy TriplePenaltyToggle;

        public TMP_Dropdown Player1Dropdown;
        public TMP_Dropdown Player2Dropdown;
        public TMP_Dropdown Player3Dropdown;
        public TMP_Dropdown Player4Dropdown;

        public GameObject Player3Row;
        public GameObject Player4Row;
    }

    private class ModalRefs
    {
        public GameObject Root;
        public TMP_Text Title;
        public TMP_Text Body;
    }
}
#endif
