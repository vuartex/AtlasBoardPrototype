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
                new Vector2(680f, 690f),
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
            new Vector2(600f, 70f),
            38f,
            Color.white,
            font);

        Button resumeButton =
            CreateButton(
                pauseWindow.transform,
                "Button_Resume",
                "leaveflow.pause.resume",
                "RESUME",
                new Vector2(0f, 160f),
                new Vector2(500f, 82f),
                Green,
                font);

        Button settingsButton =
            CreateButton(
                pauseWindow.transform,
                "Button_Settings",
                "leaveflow.pause.settings",
                "SETTINGS",
                new Vector2(0f, 55f),
                new Vector2(500f, 82f),
                Blue,
                font);

        Button leaveMatchButton =
            CreateButton(
                pauseWindow.transform,
                "Button_LeaveMatch",
                "leaveflow.pause.leave_match",
                "LEAVE MATCH",
                new Vector2(0f, -50f),
                new Vector2(500f, 82f),
                Red,
                font);

        Button quitGameButton =
            CreateButton(
                pauseWindow.transform,
                "Button_QuitGame",
                "leaveflow.pause.quit_game",
                "QUIT GAME",
                new Vector2(0f, -155f),
                new Vector2(500f, 82f),
                new Color32(78, 84, 95, 255),
                font);

        CreateText(
            pauseWindow.transform,
            "PauseHint",
            "ESC",
            new Vector2(0f, -275f),
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
            resumeButton,
            settingsButton,
            leaveMatchButton,
            quitGameButton,
            cancelLeaveButton,
            confirmLeaveButton,
            null);

        BuildGameplayPauseShortcut(controller, font);

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
            "Gameplay SET was replaced by a compact menu icon that opens this pause menu. " +
            "Single-Human offline matches may freeze locally; 2+ Human and future online sessions never freeze shared simulation from Pause. " +
            "Local Leave Match reloads the active scene for a clean reset; future online sessions can intercept leave requests through IAtlasBoardSessionExitHandler.");
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

        Transform oldPause =
            FindChildRecursive(
                overlay.transform,
                "Button_PauseMenu");

        if (oldPause != null)
        {
            Undo.DestroyObjectImmediate(
                oldPause.gameObject);
        }

        Transform oldSettings =
            FindChildRecursive(
                overlay.transform,
                "Button_Settings");

        Transform parent =
            oldSettings != null
                ? oldSettings.parent
                : overlay.transform;

        Vector2 anchorMin = new Vector2(1f, 1f);
        Vector2 anchorMax = new Vector2(1f, 1f);
        Vector2 pivot = new Vector2(1f, 1f);
        Vector2 sizeDelta = new Vector2(64f, 64f);
        Vector2 anchoredPosition = new Vector2(-24f, -92f);

        Sprite backgroundSprite = null;
        bool preserveAspect = false;

        if (oldSettings != null)
        {
            RectTransform oldRect =
                oldSettings as RectTransform;

            if (oldRect != null)
            {
                anchorMin = oldRect.anchorMin;
                anchorMax = oldRect.anchorMax;
                pivot = oldRect.pivot;
                sizeDelta = oldRect.sizeDelta;
                anchoredPosition = oldRect.anchoredPosition;
            }

            Image oldImage =
                oldSettings.GetComponent<Image>();

            // Reuse the existing compact button background when possible.
            // The replacement uses a geometry-based hamburger glyph so it never
            // depends on font size, localization or glyph availability.
            if (oldImage != null &&
                oldSettings.GetComponentInChildren<TMP_Text>(true) != null)
            {
                backgroundSprite = oldImage.sprite;
                preserveAspect = oldImage.preserveAspect;
            }

            Undo.DestroyObjectImmediate(
                oldSettings.gameObject);
        }

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
            "Build AtlasBoard gameplay pause shortcut");

        buttonObject.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            buttonObject.GetComponent<RectTransform>();

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;

        Image image =
            buttonObject.GetComponent<Image>();

        image.color = Blue;
        image.sprite = backgroundSprite;
        image.preserveAspect = preserveAspect;

        Button button =
            buttonObject.GetComponent<Button>();

        button.targetGraphic = image;

        CreateMenuIcon(
            buttonObject.transform,
            sizeDelta);

        AtlasBoardPauseMenuOpenButton opener =
            buttonObject.GetComponent<AtlasBoardPauseMenuOpenButton>();

        opener.EditorConfigure(controller);
        EditorUtility.SetDirty(opener);

        // Destroying Button_Settings clears the Settings controller's serialized
        // gameplay button reference. Mark it dirty so the scene persists that change.
        AtlasBoardSettingsV2Controller settingsController =
            FindSceneComponent<AtlasBoardSettingsV2Controller>();

        if (settingsController != null)
        {
            EditorUtility.SetDirty(settingsController);
        }
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
