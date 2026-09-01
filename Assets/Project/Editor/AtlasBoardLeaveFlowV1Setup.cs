#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AtlasBoardLeaveFlowV1Setup
{
    private const string LocalizationDatabasePath =
        "Assets/Project/Data/Localization/Localization_Default.asset";

    private static readonly Color Overlay =
        new Color32(10, 16, 27, 205);

    private static readonly Color Panel =
        new Color32(236, 235, 226, 255);

    private static readonly Color Header =
        new Color32(24, 40, 66, 255);

    private static readonly Color Blue =
        new Color32(38, 107, 180, 255);

    private static readonly Color Green =
        new Color32(50, 161, 93, 255);

    private static readonly Color Red =
        new Color32(191, 55, 63, 255);

    private static readonly Color DarkText =
        new Color32(28, 34, 43, 255);

    [MenuItem("Atlas Board/UI/Build Leave Flow v1")]
    public static void Build()
    {
        GameObject mainMenuCanvas =
            FindSceneObject("Canvas_MainMenu");

        if (mainMenuCanvas == null)
        {
            Debug.LogError(
                "AtlasBoard Leave Flow v1 requires Canvas_MainMenu. " +
                "Build the existing Main Menu + Lobby first.");
            return;
        }

        GameObject mainMenuRoot =
            FindChildRecursive(mainMenuCanvas.transform, "MainMenu")?.gameObject;

        GameObject lobbyRoot =
            FindChildRecursive(mainMenuCanvas.transform, "Lobby")?.gameObject;

        if (mainMenuRoot == null || lobbyRoot == null)
        {
            Debug.LogError(
                "AtlasBoard Leave Flow v1 could not find MainMenu and Lobby under Canvas_MainMenu.");
            return;
        }

        MergeLocalizationEntries();

        GameObject oldCanvas =
            FindSceneObject("Canvas_LeaveFlow");

        if (oldCanvas != null)
        {
            Undo.DestroyObjectImmediate(oldCanvas);
        }

        GameObject oldLobbyButton =
            FindChildRecursive(lobbyRoot.transform, "Button_LeaveLobby")?.gameObject;

        if (oldLobbyButton != null)
        {
            Undo.DestroyObjectImmediate(oldLobbyButton);
        }

        TMP_FontAsset font = FindSceneFont();

        GameObject canvasObject =
            new GameObject(
                "Canvas_LeaveFlow",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

        Undo.RegisterCreatedObjectUndo(
            canvasObject,
            "Build AtlasBoard Leave Flow");

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 940;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        AtlasBoardLeaveFlowController controller =
            canvasObject.AddComponent<AtlasBoardLeaveFlowController>();

        GameObject pauseRoot =
            CreateStretchImage(
                canvasObject.transform,
                "PauseRoot",
                Overlay);

        pauseRoot.AddComponent<AtlasBoardEscapeBlocker>();

        GameObject pauseWindow =
            CreatePanel(
                pauseRoot.transform,
                "PauseWindow",
                Vector2.zero,
                new Vector2(720f, 780f),
                Panel);

        CreateImage(
            pauseWindow.transform,
            "Header",
            new Vector2(0f, 295f),
            new Vector2(680f, 100f),
            Header);

        CreateLocalizedText(
            pauseWindow.transform,
            "PauseTitle",
            "leaveflow.pause.title",
            "PAUSED",
            new Vector2(0f, 295f),
            new Vector2(640f, 70f),
            38f,
            Color.white,
            font);

        GameObject roomCodeSectionRoot =
            CreatePanel(
                pauseWindow.transform,
                "RoomCodeSection",
                new Vector2(0f, 170f),
                new Vector2(600f, 110f),
                new Color32(232, 233, 236, 255));

        CreateLocalizedText(
            roomCodeSectionRoot.transform,
            "RoomCodeLabel",
            "leaveflow.pause.room_code",
            "ROOM CODE",
            new Vector2(-185f, 24f),
            new Vector2(230f, 28f),
            18f,
            DarkText,
            font);

        TMP_Text roomCodeValueText =
            CreateText(
                roomCodeSectionRoot.transform,
                "RoomCodeValue",
                "••••••",
                new Vector2(-185f, -18f),
                new Vector2(230f, 46f),
                29f,
                DarkText,
                font);

        roomCodeValueText.fontStyle =
            FontStyles.Bold;

        Button roomCodeShowHideButton =
            CreateButton(
                roomCodeSectionRoot.transform,
                "Button_RoomCodeShowHide",
                "leaveflow.pause.room_code_show",
                "SHOW",
                new Vector2(75f, -4f),
                new Vector2(130f, 54f),
                Blue,
                font);

        TMP_Text roomCodeShowHideButtonText =
            roomCodeShowHideButton.
                GetComponentInChildren<TMP_Text>(true);

        Button roomCodeCopyButton =
            CreateButton(
                roomCodeSectionRoot.transform,
                "Button_RoomCodeCopy",
                "leaveflow.pause.room_code_copy",
                "COPY",
                new Vector2(220f, -4f),
                new Vector2(130f, 54f),
                new Color32(196, 145, 0, 255),
                font);

        TMP_Text roomCodeCopyButtonText =
            roomCodeCopyButton.
                GetComponentInChildren<TMP_Text>(true);

        ConfigureRoomCodeActionText(
            roomCodeShowHideButtonText);

        ConfigureRoomCodeActionText(
            roomCodeCopyButtonText);

        Button resumeButton =
            CreateButton(
                pauseWindow.transform,
                "Button_Resume",
                "leaveflow.pause.resume",
                "RESUME",
                new Vector2(0f, 55f),
                new Vector2(500f, 82f),
                Green,
                font);

        Button settingsButton =
            CreateButton(
                pauseWindow.transform,
                "Button_Settings",
                "leaveflow.pause.settings",
                "SETTINGS",
                new Vector2(0f, -45f),
                new Vector2(500f, 82f),
                Blue,
                font);

        Button leaveMatchButton =
            CreateButton(
                pauseWindow.transform,
                "Button_LeaveMatch",
                "leaveflow.pause.leave_match",
                "LEAVE MATCH",
                new Vector2(0f, -145f),
                new Vector2(500f, 82f),
                Red,
                font);

        Button quitGameButton =
            CreateButton(
                pauseWindow.transform,
                "Button_QuitGame",
                "leaveflow.pause.quit_game",
                "QUIT GAME",
                new Vector2(0f, -245f),
                new Vector2(500f, 82f),
                new Color32(78, 84, 95, 255),
                font);

        CreateText(
            pauseWindow.transform,
            "PauseHint",
            "ESC",
            new Vector2(0f, -348f),
            new Vector2(500f, 40f),
            21f,
            new Color32(90, 95, 103, 255),
            font);

        GameObject confirmRoot =
            CreateStretchImage(
                canvasObject.transform,
                "LeaveConfirmationRoot",
                new Color32(8, 12, 20, 220));

        confirmRoot.AddComponent<AtlasBoardEscapeBlocker>();

        GameObject confirmWindow =
            CreatePanel(
                confirmRoot.transform,
                "ConfirmationWindow",
                Vector2.zero,
                new Vector2(820f, 470f),
                Panel);

        CreateImage(
            confirmWindow.transform,
            "Header",
            new Vector2(0f, 185f),
            new Vector2(820f, 100f),
            Red);

        CreateLocalizedText(
            confirmWindow.transform,
            "ConfirmTitle",
            "leaveflow.confirm.title",
            "LEAVE CURRENT MATCH?",
            new Vector2(0f, 185f),
            new Vector2(740f, 70f),
            34f,
            Color.white,
            font);

        CreateLocalizedText(
            confirmWindow.transform,
            "ConfirmBody",
            "leaveflow.confirm.body",
            "Progress in this match will be lost.",
            new Vector2(0f, 55f),
            new Vector2(690f, 100f),
            27f,
            DarkText,
            font);

        Button cancelLeaveButton =
            CreateButton(
                confirmWindow.transform,
                "Button_Cancel",
                "leaveflow.confirm.cancel",
                "CANCEL",
                new Vector2(-190f, -120f),
                new Vector2(320f, 82f),
                Blue,
                font);

        Button confirmLeaveButton =
            CreateButton(
                confirmWindow.transform,
                "Button_ConfirmLeave",
                "leaveflow.pause.leave_match",
                "LEAVE MATCH",
                new Vector2(190f, -120f),
                new Vector2(320f, 82f),
                Red,
                font);

        // The existing Lobby Back button is the canonical local Leave Lobby control.
        // Do not add a second LEAVE LOBBY button. Future online lobby work can
        // route that existing Back control through the session exit handler.

        TurnManager turnManager =
            FindSceneComponent<TurnManager>();

        UXKeyboardShortcutController shortcuts =
            FindSceneComponent<UXKeyboardShortcutController>();

        controller.EditorConfigure(
            turnManager,
            shortcuts,
            mainMenuCanvas,
            mainMenuRoot,
            lobbyRoot,
            pauseRoot,
            confirmRoot,
            roomCodeSectionRoot,
            roomCodeValueText,
            roomCodeShowHideButton,
            roomCodeShowHideButtonText,
            roomCodeCopyButton,
            roomCodeCopyButtonText,
            resumeButton,
            settingsButton,
            leaveMatchButton,
            quitGameButton,
            cancelLeaveButton,
            confirmLeaveButton,
            null);

        BuildGameplayPauseShortcut(controller, font);

        roomCodeSectionRoot.SetActive(false);
        pauseRoot.SetActive(false);
        confirmRoot.SetActive(false);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(mainMenuCanvas);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkAllScenesDirty();

        Selection.activeGameObject = canvasObject;

        Debug.Log(
            "AtlasBoard Leave Flow v1 ready. " +
            "Gameplay ESC now opens PAUSED only when no existing gameplay modal owns Escape; " +
            "Resume, Settings, Leave Match confirmation and Quit Game are wired. " +
            "The existing Lobby Back button remains the single Leave Lobby control. " +
            "Gameplay MENU stays beside the existing gameplay controls, while ESC opens this pause menu. " +
            "Single-Human offline matches may freeze locally; 2+ Human and future online sessions never freeze shared simulation from Pause. " +
            "Local Leave Match reloads the active scene for a clean reset; future online sessions can intercept leave requests through IAtlasBoardSessionExitHandler. " +
            "When a private online room code is available, ESC now exposes a masked SHOW/HIDE/COPY room-code strip for reconnect support.");
    }

    private static void MergeLocalizationEntries()
    {
        AtlasBoardLocalizationDatabase database =
            AssetDatabase.LoadAssetAtPath<AtlasBoardLocalizationDatabase>(
                LocalizationDatabasePath);

        if (database == null)
        {
            Debug.LogError(
                "Localization_Default.asset was not found. " +
                "Run Atlas Board > Localization > Build or Refresh Localization Foundation v1 first.");
            return;
        }

        List<AtlasBoardLocalizationDatabase.Entry> entries =
            new List<AtlasBoardLocalizationDatabase.Entry>();

        foreach (AtlasBoardLocalizationDatabase.Entry entry in database.Entries)
        {
            if (entry == null ||
                string.IsNullOrWhiteSpace(entry.key) ||
                entry.key.StartsWith(
                    AtlasBoardLeaveFlowLocalizationSeed.Prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entries.Add(entry);
        }

        AtlasBoardLeaveFlowLocalizationSeed.Append(entries);
        database.EditorReplaceEntries(entries);
        EditorUtility.SetDirty(database);
    }

    private static void BuildGameplayPauseShortcut(
        AtlasBoardLeaveFlowController controller,
        TMP_FontAsset font)
    {
        GameObject overlay =
            FindSceneObject("Canvas_UXOverlay");

        if (overlay == null)
        {
            Debug.LogWarning(
                "Canvas_UXOverlay was not found. Gameplay MENU shortcut was not installed; ESC pause still works.");
            return;
        }

        // Remove only the pause/menu shortcut created by earlier Leave Flow
        // versions. Do NOT move, clone or delete Player HUD controls.
        Transform oldPause =
            FindChildRecursive(
                overlay.transform,
                "Button_PauseMenu");

        if (oldPause != null)
        {
            Undo.DestroyObjectImmediate(
                oldPause.gameObject);
        }

        // The canonical location requested for this shortcut is beside the
        // existing camera-reset control on the UX StatusBar. CameraResetButton
        // itself is built by AtlasBoardUXPackV1Setup at x=18 from the right
        // edge of StatusBar, with a 48x48 size. Place MENU immediately after it.
        Transform statusBar =
            FindChildRecursive(
                overlay.transform,
                "StatusBar");

        Transform cameraReset =
            statusBar != null
                ? FindChildRecursive(
                    statusBar,
                    "CameraResetButton")
                : null;

        if (statusBar == null ||
            cameraReset == null)
        {
            Debug.LogWarning(
                "AtlasBoard Leave Flow could not find StatusBar/CameraResetButton. " +
                "Run Atlas Board -> UX -> Build or Refresh UX Pack v1 first. " +
                "The gameplay MENU shortcut was not moved to a fallback location because that could overlap Player HUD again.");
            return;
        }

        RectTransform resetRect =
            cameraReset as RectTransform;

        Vector2 buttonSize =
            resetRect != null
                ? resetRect.sizeDelta
                : new Vector2(48f, 48f);

        if (buttonSize.x <= 0f || buttonSize.y <= 0f)
        {
            buttonSize = new Vector2(48f, 48f);
        }

        float gap = 10f;
        Vector2 anchoredPosition =
            resetRect != null
                ? new Vector2(
                    resetRect.anchoredPosition.x +
                    buttonSize.x +
                    gap,
                    resetRect.anchoredPosition.y)
                : new Vector2(76f, 0f);

        GameObject buttonObject =
            new GameObject(
                "Button_PauseMenu",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(AtlasBoardUIButtonAudio),
                typeof(AtlasBoardPauseMenuOpenButton));

        Undo.RegisterCreatedObjectUndo(
            buttonObject,
            "Build AtlasBoard gameplay pause shortcut beside camera reset");

        buttonObject.transform.SetParent(
            statusBar,
            false);

        RectTransform rect =
            buttonObject.GetComponent<RectTransform>();

        if (resetRect != null)
        {
            rect.anchorMin = resetRect.anchorMin;
            rect.anchorMax = resetRect.anchorMax;
            rect.pivot = resetRect.pivot;
        }
        else
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
        }

        rect.sizeDelta = buttonSize;
        rect.anchoredPosition = anchoredPosition;

        Image resetImage =
            cameraReset.GetComponent<Image>();

        Image image =
            buttonObject.GetComponent<Image>();

        image.color = Blue;

        if (resetImage != null)
        {
            image.sprite = resetImage.sprite;
            image.type = resetImage.type;
            image.preserveAspect = resetImage.preserveAspect;
        }

        Button button =
            buttonObject.GetComponent<Button>();

        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        CreateMenuIcon(
            buttonObject.transform,
            buttonSize);

        AtlasBoardPauseMenuOpenButton opener =
            buttonObject.GetComponent<AtlasBoardPauseMenuOpenButton>();

        opener.EditorConfigure(controller);
        EditorUtility.SetDirty(opener);

        Debug.Log(
            "AtlasBoard gameplay MENU shortcut placed beside StatusBar/CameraResetButton. " +
            $"Position={anchoredPosition}, Size={buttonSize}.");
    }

    private static void CreateMenuIcon(
        Transform parent,
        Vector2 buttonSize)
    {
        float width = Mathf.Clamp(buttonSize.x * 0.48f, 16f, 30f);
        float thickness = Mathf.Clamp(buttonSize.y * 0.055f, 2f, 4f);
        float spacing = Mathf.Clamp(buttonSize.y * 0.16f, 5f, 9f);

        for (int i = -1; i <= 1; i++)
        {
            GameObject line =
                new GameObject(
                    $"MenuLine_{i + 2}",
                    typeof(RectTransform),
                    typeof(Image));

            line.transform.SetParent(parent, false);

            RectTransform rect =
                line.GetComponent<RectTransform>();

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, i * spacing);
            rect.sizeDelta = new Vector2(width, thickness);

            Image image = line.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
        }
    }

    private static void ConfigureRoomCodeActionText(
        TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 19f;
        text.textWrappingMode =
            TextWrappingModes.NoWrap;
        text.overflowMode =
            TextOverflowModes.Ellipsis;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string localizationKey,
        string fallbackText,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color,
        TMP_FontAsset font)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(AtlasBoardUIButtonAudio));

        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = root.GetComponent<Image>();
        image.color = color;

        Button button = root.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
        colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        CreateLocalizedText(
            root.transform,
            "Label",
            localizationKey,
            fallbackText,
            Vector2.zero,
            size - new Vector2(28f, 18f),
            27f,
            Color.white,
            font);

        return button;
    }

    private static GameObject CreateStretchImage(
        Transform parent,
        string name,
        Color color)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));

        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = root.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;

        return root;
    }

    private static GameObject CreatePanel(
        Transform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color)
    {
        return CreateImage(parent, name, anchoredPosition, size, color);
    }

    private static GameObject CreateImage(
        Transform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));

        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        root.GetComponent<Image>().color = color;

        return root;
    }

    private static TMP_Text CreateLocalizedText(
        Transform parent,
        string name,
        string localizationKey,
        string fallbackText,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        Color color,
        TMP_FontAsset font)
    {
        TMP_Text text =
            CreateText(
                parent,
                name,
                fallbackText,
                anchoredPosition,
                size,
                fontSize,
                color,
                font);

        AtlasBoardLocalizedText localized =
            text.gameObject.AddComponent<AtlasBoardLocalizedText>();

        localized.EditorConfigure(localizationKey, text);
        return text;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        Color color,
        TMP_FontAsset font)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));

        root.transform.SetParent(parent, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        if (font != null)
        {
            text.font = font;
        }

        return text;
    }

    private static TMP_FontAsset FindSceneFont()
    {
        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();

        foreach (TMP_Text text in texts)
        {
            if (text != null &&
                text.gameObject.scene.IsValid() &&
                text.font != null)
            {
                return text.font;
            }
        }

        return null;
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        T[] items = Resources.FindObjectsOfTypeAll<T>();

        foreach (T item in items)
        {
            if (item != null && item.gameObject.scene.IsValid())
            {
                return item;
            }
        }

        return null;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject item in all)
        {
            if (item != null &&
                item.scene.IsValid() &&
                item.name == objectName)
            {
                return item;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(
        Transform root,
        string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found =
                FindChildRecursive(root.GetChild(i), objectName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
#endif
