#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AtlasBoardSettingsV2Setup
{
    private const string CanvasName =
        "Canvas_Settings";

    private static TMP_FontAsset defaultFont;

    private static Sprite kenneyButtonSprite;
    private static Sprite kenneyPanelSprite;
    private static Sprite kenneyCircleSprite;
    private static Sprite kenneySliderSprite;
    private static Sprite kenneySettingsIconSprite;

    private static readonly Color Overlay =
        new Color32(
            24,
            30,
            42,
            185);

    private static readonly Color Panel =
        new Color32(
            245,
            241,
            232,
            255);

    private static readonly Color Content =
        new Color32(
            235,
            235,
            241,
            255);

    private static readonly Color Blue =
        new Color32(
            31,
            158,
            211,
            255);

    private static readonly Color BlueDark =
        new Color32(
            17,
            110,
            161,
            255);

    private static readonly Color Green =
        new Color32(
            134,
            176,
            0,
            255);

    private static readonly Color Red =
        new Color32(
            220,
            35,
            88,
            255);

    private static readonly Color TextDark =
        new Color32(
            57,
            59,
            64,
            255);

    [MenuItem(
        "Atlas Board/Settings/Build Settings + Quality v2.0.3")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "Exit Play Mode before rebuilding Settings v2.");

            return;
        }

        ResolveKenneySprites();

        GameObject oldCanvas =
            FindSceneObject(
                CanvasName);

        if (oldCanvas != null)
        {
            Undo.DestroyObjectImmediate(
                oldCanvas);
        }

        BuildSettingsCanvas();
        InstallOverlayHelpers();

        AssetDatabase.SaveAssets();

        EditorSceneManager.MarkAllScenesDirty();

        Debug.Log(
            "AtlasBoard Settings + Quality v2.0.3 built. " +
            "Audio preferences were preserved; Localization is intentionally deferred.");
    }

    private static void BuildSettingsCanvas()
    {
        GameObject canvasObject =
            new GameObject(
                CanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

        Undo.RegisterCreatedObjectUndo(
            canvasObject,
            "Build Settings v2");

        Canvas canvas =
            canvasObject.GetComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder =
            950;

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

        canvasObject.AddComponent<
            AtlasBoardSettingsV2Controller>();

        GameObject root =
            CreateStretchImage(
                canvasObject.transform,
                "SettingsRoot",
                Overlay);

        GameObject window =
            CreateImage(
                root.transform,
                "SettingsWindow",
                Vector2.zero,
                new Vector2(
                    1360f,
                    940f),
                Panel,
                kenneyPanelSprite);

        CreateImage(
            window.transform,
            "Header",
            new Vector2(
                0f,
                410f),
            new Vector2(
                1360f,
                80f),
            BlueDark,
            kenneyButtonSprite);

        CreateText(
            window.transform,
            "Title",
            "SETTINGS",
            new Vector2(
                0f,
                410f),
            new Vector2(
                820f,
                62f),
            42f,
            Color.white,
            FontStyles.Bold,
            TextAlignmentOptions.Center);

        CreateButton(
            window.transform,
            "Button_CloseSettings",
            "X",
            new Vector2(
                560f,
                405f),
            new Vector2(
                70f,
                70f),
            Red,
            kenneyCircleSprite ??
            kenneyButtonSprite);

        BuildTabs(
            window.transform);

        GameObject content =
            CreateImage(
                window.transform,
                "Content",
                new Vector2(
                    0f,
                    -35f),
                new Vector2(
                    1240f,
                    620f),
                Content,
                null);

        BuildAudioPanel(
            content.transform);

        BuildGameplayPanel(
            content.transform);

        BuildGraphicsPanel(
            content.transform);

        BuildControlsPanel(
            content.transform);

        CreateButton(
            window.transform,
            "Button_ResetDefaults",
            "RESET DEFAULTS",
            new Vector2(
                -455f,
                -405f),
            new Vector2(
                250f,
                64f),
            new Color32(
                120,
                125,
                140,
                255),
            kenneyButtonSprite);

        CreateButton(
            window.transform,
            "Button_Cancel",
            "CANCEL",
            new Vector2(
                325f,
                -405f),
            new Vector2(
                200f,
                64f),
            new Color32(
                120,
                125,
                140,
                255),
            kenneyButtonSprite);

        CreateButton(
            window.transform,
            "Button_Apply",
            "APPLY",
            new Vector2(
                535f,
                -405f),
            new Vector2(
                200f,
                64f),
            Green,
            kenneyButtonSprite);

        root.SetActive(
            false);

        Selection.activeGameObject =
            canvasObject;
    }

    private static void BuildTabs(
        Transform parent)
    {
        string[] names =
        {
            "Audio",
            "Gameplay",
            "Graphics",
            "Controls"
        };

        float[] x =
        {
            -450f,
            -150f,
            150f,
            450f
        };

        for (int i = 0;
             i < names.Length;
             i++)
        {
            CreateButton(
                parent,
                $"Tab_{names[i]}",
                names[i].ToUpperInvariant(),
                new Vector2(
                    x[i],
                    325f),
                new Vector2(
                    280f,
                    54f),
                Blue,
                kenneyButtonSprite);
        }
    }

    private static void BuildAudioPanel(
        Transform parent)
    {
        GameObject panel =
            CreatePanelRoot(
                parent,
                "AudioSettings");

        CreateSectionTitle(
            panel.transform,
            "AUDIO SETTINGS");

        CreateToggleRow(
            panel.transform,
            "Mute",
            "Mute All Audio",
            200f,
            false);

        CreateAudioSliderRow(
            panel.transform,
            "Master",
            "Master Volume",
            115f);

        CreateAudioSliderRow(
            panel.transform,
            "MainMusic",
            "Main Music",
            30f);

        CreateAudioSliderRow(
            panel.transform,
            "Theme",
            "Theme / Ambience",
            -55f);

        CreateAudioSliderRow(
            panel.transform,
            "Dice",
            "Dice",
            -140f);

        CreateAudioSliderRow(
            panel.transform,
            "Effects",
            "Effects / UI / Pawn",
            -225f);
    }

    private static void BuildGameplayPanel(
        Transform parent)
    {
        GameObject panel =
            CreatePanelRoot(
                parent,
                "GameplaySettings");

        CreateSectionTitle(
            panel.transform,
            "GAMEPLAY SETTINGS");

        CreatePercentSliderRow(
            panel.transform,
            "Camera",
            "Camera Sensitivity",
            200f);

        CreatePercentSliderRow(
            panel.transform,
            "Zoom",
            "Camera Zoom Sensitivity",
            115f);

        CreatePercentSliderRow(
            panel.transform,
            "Pan",
            "Camera Pan Sensitivity",
            30f);

        CreatePercentSliderRow(
            panel.transform,
            "BotSpeed",
            "Bot Turn Speed",
            -55f);

        CreateToggleRow(
            panel.transform,
            "ReduceMotion",
            "Reduce Camera Motion",
            -140f,
            false);

        CreateToggleRow(
            panel.transform,
            "UIHints",
            "UI Hints",
            -215f,
            true);

        CreateToggleRow(
            panel.transform,
            "Confirmations",
            "Gameplay Confirmations",
            -290f,
            true);

    }

    private static void BuildGraphicsPanel(
        Transform parent)
    {
        GameObject panel =
            CreatePanelRoot(
                parent,
                "GraphicsSettings");

        CreateSectionTitle(
            panel.transform,
            "GRAPHICS SETTINGS");

        CreateText(
            panel.transform,
            "CurrentResolution",
            "Current: --",
            new Vector2(
                0f,
                230f),
            new Vector2(
                970f,
                34f),
            19f,
            new Color32(
                95,
                98,
                108,
                255),
            FontStyles.Normal,
            TextAlignmentOptions.Left);

        CreateDropdownRow(
            panel.transform,
            "Resolution",
            "Resolution",
            165f,
            new[]
            {
                "1920 x 1080"
            });

        CreateDropdownRow(
            panel.transform,
            "DisplayMode",
            "Display Mode",
            100f,
            new[]
            {
                "Exclusive Fullscreen",
                "Borderless Fullscreen",
                "Windowed"
            });

        CreateDropdownRow(
            panel.transform,
            "Quality",
            "Quality",
            35f,
            new[]
            {
                "Low",
                "Medium",
                "High",
                "Very High"
            });

        CreateToggleRow(
            panel.transform,
            "VSync",
            "VSync",
            -30f,
            true);

        CreateDropdownRow(
            panel.transform,
            "FPSLimit",
            "FPS Limit",
            -95f,
            new[]
            {
                "30",
                "60",
                "90",
                "120",
                "144",
                "165",
                "240",
                "Unlimited"
            });

        CreateDropdownRow(
            panel.transform,
            "Shadow",
            "Shadow Quality",
            -160f,
            new[]
            {
                "Off",
                "Low",
                "Medium",
                "High"
            });

        CreateDropdownRow(
            panel.transform,
            "AA",
            "Anti-Aliasing",
            -225f,
            new[]
            {
                "Off",
                "2x",
                "4x",
                "8x"
            });

        CreateToggleRow(
            panel.transform,
            "ShowFPS",
            "Show FPS",
            -290f,
            false);

        CreateText(
            panel.transform,
            "VSyncNote",
            "VSync can override the effective FPS limit. Display-mode/resolution changes are applied in the standalone build.",
            new Vector2(
                0f,
                -328f),
            new Vector2(
                980f,
                24f),
            14f,
            new Color32(
                100,
                102,
                110,
                255),
            FontStyles.Normal,
            TextAlignmentOptions.Center);
    }

    private static void BuildControlsPanel(
        Transform parent)
    {
        GameObject panel =
            CreatePanelRoot(
                parent,
                "ControlsSettings");

        CreateSectionTitle(
            panel.transform,
            "CONTROLS");

        CreateText(
            panel.transform,
            "CameraHeader",
            "CAMERA",
            new Vector2(
                -265f,
                175f),
            new Vector2(
                430f,
                40f),
            24f,
            TextDark,
            FontStyles.Bold,
            TextAlignmentOptions.Left);

        CreateControlLine(
            panel.transform,
            -265f,
            125f,
            "Right Mouse Drag",
            "Rotate Camera");

        CreateControlLine(
            panel.transform,
            -265f,
            75f,
            "Middle Mouse Drag",
            "Pan Camera");

        CreateControlLine(
            panel.transform,
            -265f,
            25f,
            "Shift + Right Drag",
            "Pan Camera");

        CreateControlLine(
            panel.transform,
            -265f,
            -25f,
            "Mouse Wheel",
            "Zoom");

        CreateControlLine(
            panel.transform,
            -265f,
            -75f,
            "Home",
            "Reset Camera");

        CreateText(
            panel.transform,
            "GameplayHeader",
            "GAMEPLAY",
            new Vector2(
                270f,
                175f),
            new Vector2(
                430f,
                40f),
            24f,
            TextDark,
            FontStyles.Bold,
            TextAlignmentOptions.Left);

        CreateControlLine(
            panel.transform,
            270f,
            125f,
            "Space / Enter",
            "Roll / Primary Action");

        CreateControlLine(
            panel.transform,
            270f,
            75f,
            "Shift + Space",
            "Large Auction Bid");

        CreateControlLine(
            panel.transform,
            270f,
            25f,
            "T",
            "Trade");

        CreateControlLine(
            panel.transform,
            270f,
            -25f,
            "Esc",
            "Close / Back / Settings");

        CreateText(
            panel.transform,
            "ContextNote",
            "Primary Action is contextual: Roll, Buy, Continue, Travel, Develop, Trade Accept, Auction Bid and similar actions.",
            new Vector2(
                0f,
                -190f),
            new Vector2(
                980f,
                70f),
            19f,
            new Color32(
                95,
                98,
                108,
                255),
            FontStyles.Normal,
            TextAlignmentOptions.Center);
    }

    private static void InstallOverlayHelpers()
    {
        GameObject overlay =
            FindSceneObject(
                "Canvas_UXOverlay");

        if (overlay == null)
        {
            Debug.LogWarning(
                "Canvas_UXOverlay not found. FPS / UI hint / in-game Settings helpers were not installed.");

            return;
        }

        if (overlay.GetComponent<
                AtlasBoardUIHintsController>() ==
            null)
        {
            Undo.AddComponent<
                AtlasBoardUIHintsController>(
                    overlay);
        }

        Transform existingFps =
            FindRecursive(
                overlay.transform,
                "FPSDisplay");

        if (existingFps != null)
        {
            Undo.DestroyObjectImmediate(
                existingFps.gameObject);
        }

        TMP_Text fpsText =
            CreateText(
                overlay.transform,
                "FPSDisplay",
                "FPS 60",
                Vector2.zero,
                new Vector2(
                    130f,
                    34f),
                18f,
                Color.white,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineRight);

        RectTransform fpsRect =
            fpsText.rectTransform;

        fpsRect.anchorMin =
            new Vector2(
                0.5f,
                1f);

        fpsRect.anchorMax =
            new Vector2(
                0.5f,
                1f);

        fpsRect.pivot =
            new Vector2(
                1f,
                1f);

        // Place FPS to the LEFT of the centered turn/status bar.
        // The gap is intentionally generous so "FPS 999" never touches
        // either the player HUD or the status bar.
        fpsRect.anchoredPosition =
            new Vector2(
                -430f,
                -18f);

        fpsText.gameObject.AddComponent<
            AtlasBoardFPSDisplay>();

        fpsText.enabled =
            false;

        RebuildGameplaySettingsButton(
            overlay);

        EditorUtility.SetDirty(
            overlay);
    }

    private static void RebuildGameplaySettingsButton(
        GameObject overlay)
    {
        Transform existing =
            FindRecursive(
                overlay.transform,
                "Button_Settings");

        if (existing != null)
        {
            Undo.DestroyObjectImmediate(
                existing.gameObject);
        }

        RectTransform cameraResetRect =
            FindCameraResetRect(
                overlay.transform);

        Transform parent =
            cameraResetRect != null
                ? cameraResetRect.parent
                : overlay.transform;

        GameObject buttonObject =
            new GameObject(
                "Button_Settings",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));

        buttonObject.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            buttonObject.GetComponent<
                RectTransform>();

        if (cameraResetRect != null)
        {
            rect.anchorMin =
                cameraResetRect.anchorMin;

            rect.anchorMax =
                cameraResetRect.anchorMax;

            rect.pivot =
                cameraResetRect.pivot;

            rect.sizeDelta =
                cameraResetRect.sizeDelta;

            float gap =
                Mathf.Max(
                    10f,
                    cameraResetRect.sizeDelta.x *
                    0.20f);

            rect.anchoredPosition =
                cameraResetRect.anchoredPosition +
                new Vector2(
                    cameraResetRect.sizeDelta.x +
                    gap,
                    0f);
        }
        else
        {
            rect.anchorMin =
                new Vector2(
                    1f,
                    1f);

            rect.anchorMax =
                new Vector2(
                    1f,
                    1f);

            rect.pivot =
                new Vector2(
                    1f,
                    1f);

            rect.sizeDelta =
                new Vector2(
                    64f,
                    64f);

            rect.anchoredPosition =
                new Vector2(
                    -24f,
                    -92f);
        }

        Image image =
            buttonObject.GetComponent<
                Image>();

        image.color =
            kenneySettingsIconSprite != null
                ? Color.white
                : Blue;

        image.sprite =
            kenneySettingsIconSprite ??
            kenneyCircleSprite;

        image.preserveAspect =
            kenneySettingsIconSprite != null;

        Button button =
            buttonObject.GetComponent<
                Button>();

        button.targetGraphic =
            image;

        if (kenneySettingsIconSprite == null)
        {
            CreateText(
                buttonObject.transform,
                "Label",
                "SET",
                Vector2.zero,
                new Vector2(
                    54f,
                    54f),
                18f,
                Color.white,
                FontStyles.Bold,
                TextAlignmentOptions.Center);
        }

        AtlasBoardSettingsOpenButton opener =
            buttonObject.GetComponent<
                AtlasBoardSettingsOpenButton>();

        if (opener == null)
        {
            opener =
                buttonObject.AddComponent<
                    AtlasBoardSettingsOpenButton>();
        }

        GameObject settingsCanvas =
            FindSceneObject(
                CanvasName);

        AtlasBoardSettingsV2Controller settingsController =
            settingsCanvas != null
                ? settingsCanvas.GetComponent<
                    AtlasBoardSettingsV2Controller>()
                : null;

        if (settingsController != null)
        {
            opener.EditorConfigure(
                settingsController);

            EditorUtility.SetDirty(
                opener);
        }
    }

    private static GameObject CreatePanelRoot(
        Transform parent,
        string name)
    {
        GameObject panel =
            new GameObject(
                name,
                typeof(RectTransform));

        panel.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            panel.GetComponent<
                RectTransform>();

        Stretch(
            rect);

        return panel;
    }

    private static void CreateSectionTitle(
        Transform parent,
        string title)
    {
        CreateText(
            parent,
            "SectionTitle",
            title,
            new Vector2(
                -10f,
                285f),
            new Vector2(
                980f,
                50f),
            30f,
            TextDark,
            FontStyles.Bold,
            TextAlignmentOptions.Left);
    }

    private static void CreateAudioSliderRow(
        Transform parent,
        string id,
        string label,
        float y)
    {
        CreateText(
            parent,
            $"Label_{id}",
            label,
            new Vector2(
                -335f,
                y),
            new Vector2(
                310f,
                42f),
            22f,
            TextDark,
            FontStyles.Normal,
            TextAlignmentOptions.Left);

        Slider slider =
            CreateSlider(
                parent,
                $"Slider_{id}",
                new Vector2(
                    110f,
                    y),
                new Vector2(
                    500f,
                    40f),
                0f,
                1f,
                0.5f,
                false);

        CreateText(
            parent,
            $"Value_{id}",
            "50%",
            new Vector2(
                430f,
                y),
            new Vector2(
                95f,
                36f),
            20f,
            TextDark,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
    }

    private static void CreatePercentSliderRow(
        Transform parent,
        string id,
        string label,
        float y)
    {
        CreateText(
            parent,
            $"Label_{id}",
            label,
            new Vector2(
                -335f,
                y),
            new Vector2(
                330f,
                42f),
            21f,
            TextDark,
            FontStyles.Normal,
            TextAlignmentOptions.Left);

        CreateSlider(
            parent,
            $"Slider_{id}",
            new Vector2(
                110f,
                y),
            new Vector2(
                500f,
                40f),
            1f,
            100f,
            50f,
            true);

        CreateText(
            parent,
            $"Value_{id}",
            "50%",
            new Vector2(
                430f,
                y),
            new Vector2(
                95f,
                36f),
            20f,
            TextDark,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
    }

    private static void CreateToggleRow(
        Transform parent,
        string id,
        string label,
        float y,
        bool defaultValue)
    {
        CreateText(
            parent,
            $"Label_{id}",
            label,
            new Vector2(
                -230f,
                y),
            new Vector2(
                520f,
                42f),
            21f,
            TextDark,
            FontStyles.Normal,
            TextAlignmentOptions.Left);

        Toggle toggle =
            CreateToggle(
                parent,
                $"Toggle_{id}",
                new Vector2(
                    390f,
                    y),
                defaultValue);

        EditorUtility.SetDirty(
            toggle);
    }

    private static void CreateDropdownRow(
        Transform parent,
        string id,
        string label,
        float y,
        string[] options)
    {
        CreateText(
            parent,
            $"Label_{id}",
            label,
            new Vector2(
                -260f,
                y),
            new Vector2(
                460f,
                40f),
            21f,
            TextDark,
            FontStyles.Normal,
            TextAlignmentOptions.Left);

        CreateDropdown(
            parent,
            $"Dropdown_{id}",
            new Vector2(
                305f,
                y),
            new Vector2(
                360f,
                46f),
            options);
    }

    private static void CreateControlLine(
        Transform parent,
        float x,
        float y,
        string key,
        string action)
    {
        CreateText(
            parent,
            $"Key_{key}_{y}",
            key,
            new Vector2(
                x - 105f,
                y),
            new Vector2(
                210f,
                34f),
            19f,
            BlueDark,
            FontStyles.Bold,
            TextAlignmentOptions.Left);

        CreateText(
            parent,
            $"Action_{action}_{y}",
            action,
            new Vector2(
                x + 110f,
                y),
            new Vector2(
                260f,
                34f),
            19f,
            TextDark,
            FontStyles.Normal,
            TextAlignmentOptions.Left);
    }

    private static Slider CreateSlider(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        float min,
        float max,
        float defaultValue,
        bool wholeNumbers)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Slider));

        root.transform.SetParent(
            parent,
            false);

        SetRect(
            root.GetComponent<
                RectTransform>(),
            position,
            size);

        GameObject background =
            CreateImage(
                root.transform,
                "Background",
                Vector2.zero,
                new Vector2(
                    size.x - 20f,
                    14f),
                new Color32(
                    190,
                    197,
                    207,
                    255),
                null);

        RectTransform fillArea =
            CreateEmptyRect(
                root.transform,
                "Fill Area");

        fillArea.anchorMin =
            Vector2.zero;

        fillArea.anchorMax =
            Vector2.one;

        fillArea.offsetMin =
            new Vector2(
                10f,
                8f);

        fillArea.offsetMax =
            new Vector2(
                -10f,
                -8f);

        GameObject fill =
            CreateImage(
                fillArea,
                "Fill",
                Vector2.zero,
                Vector2.zero,
                Blue,
                null);

        RectTransform fillRect =
            fill.GetComponent<
                RectTransform>();

        Stretch(
            fillRect);

        RectTransform handleArea =
            CreateEmptyRect(
                root.transform,
                "Handle Slide Area");

        handleArea.anchorMin =
            Vector2.zero;

        handleArea.anchorMax =
            Vector2.one;

        handleArea.offsetMin =
            new Vector2(
                10f,
                0f);

        handleArea.offsetMax =
            new Vector2(
                -10f,
                0f);

        GameObject handle =
            CreateImage(
                handleArea,
                "Handle",
                Vector2.zero,
                new Vector2(
                    34f,
                    34f),
                BlueDark,
                kenneySliderSprite ??
                kenneyCircleSprite);

        Slider slider =
            root.GetComponent<
                Slider>();

        slider.minValue = min;
        slider.maxValue = max;
        slider.value = defaultValue;
        slider.wholeNumbers = wholeNumbers;
        slider.fillRect = fillRect;
        slider.handleRect =
            handle.GetComponent<
                RectTransform>();
        slider.targetGraphic =
            handle.GetComponent<Image>();
        slider.direction =
            Slider.Direction.LeftToRight;

        return slider;
    }

    private static Toggle CreateToggle(
        Transform parent,
        string name,
        Vector2 position,
        bool defaultValue)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Toggle));

        root.transform.SetParent(
            parent,
            false);

        SetRect(
            root.GetComponent<
                RectTransform>(),
            position,
            new Vector2(
                36f,
                36f));

        Image background =
            root.GetComponent<Image>();

        background.color =
            Color.white;

        Toggle toggle =
            root.GetComponent<Toggle>();

        toggle.targetGraphic =
            background;

        GameObject check =
            CreateImage(
                root.transform,
                "Checkmark",
                Vector2.zero,
                new Vector2(
                    24f,
                    24f),
                Green,
                null);

        toggle.graphic =
            check.GetComponent<Image>();

        toggle.isOn =
            defaultValue;

        ColorBlock colors =
            toggle.colors;

        colors.normalColor =
            Color.white;

        colors.highlightedColor =
            new Color32(
                235,
                247,
                252,
                255);

        colors.pressedColor =
            new Color32(
                218,
                237,
                246,
                255);

        colors.selectedColor =
            Color.white;

        colors.disabledColor =
            new Color32(
                225,
                225,
                225,
                255);

        toggle.colors =
            colors;

        return toggle;
    }

    private static TMP_Dropdown CreateDropdown(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        string[] options)
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
                new Vector2(
                    -12f,
                    0f),
                new Vector2(
                    size.x - 62f,
                    size.y - 8f),
                19f,
                TextDark,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);

        TMP_Text arrow =
            CreateText(
                root.transform,
                "Arrow",
                "v",
                new Vector2(
                    size.x * 0.5f - 26f,
                    0f),
                new Vector2(
                    32f,
                    32f),
                19f,
                TextDark,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        int visibleRows =
            Mathf.Clamp(
                options.Length,
                1,
                6);

        float templateHeight =
            visibleRows * 58f +
            20f;

        GameObject template =
            CreateImage(
                root.transform,
                "Template",
                new Vector2(
                    0f,
                    -(size.y + 2f)),
                new Vector2(
                    size.x,
                    templateHeight),
                Color.white,
                null);

        RectTransform templateRect =
            template.GetComponent<
                RectTransform>();

        templateRect.pivot =
            new Vector2(
                0.5f,
                1f);

        template.SetActive(
            false);

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

        Stretch(
            viewportRect);

        viewportRect.offsetMin =
            new Vector2(
                3f,
                3f);

        viewportRect.offsetMax =
            new Vector2(
                -3f,
                -3f);

        // TMP_Dropdown already sizes and positions cloned items itself.
        // LayoutGroup/ContentSizeFitter can fight that logic and visually
        // reduce a 3-option popup to only 2 visible rows.
        GameObject content =
            new GameObject(
                "Content",
                typeof(RectTransform));

        content.transform.SetParent(
            viewport.transform,
            false);

        RectTransform contentRect =
            content.GetComponent<
                RectTransform>();

        contentRect.anchorMin =
            new Vector2(
                0f,
                1f);

        contentRect.anchorMax =
            new Vector2(
                1f,
                1f);

        contentRect.pivot =
            new Vector2(
                0.5f,
                1f);

        contentRect.anchoredPosition =
            Vector2.zero;

        contentRect.sizeDelta =
            new Vector2(
                0f,
                56f);

        GameObject item =
            CreateImage(
                content.transform,
                "Item",
                Vector2.zero,
                new Vector2(
                    size.x - 6f,
                    56f),
                Color.white,
                null);

        Toggle itemToggle =
            item.AddComponent<Toggle>();

        itemToggle.targetGraphic =
            item.GetComponent<Image>();

        TMP_Text itemLabel =
            CreateText(
                item.transform,
                "Item Label",
                "Option",
                new Vector2(
                    4f,
                    0f),
                new Vector2(
                    size.x - 34f,
                    34f),
                18f,
                TextDark,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);

        itemToggle.graphic =
            null;

        scrollRect.viewport =
            viewportRect;

        scrollRect.content =
            contentRect;

        scrollRect.horizontal =
            false;

        scrollRect.vertical =
            options.Length >
            visibleRows;

        scrollRect.scrollSensitivity =
            30f;

        dropdown.targetGraphic =
            root.GetComponent<Image>();

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

        dropdown.value = 0;
        dropdown.RefreshShownValue();

        arrow.raycastTarget =
            false;

        return dropdown;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        Vector2 size,
        Color color,
        Sprite sprite)
    {
        GameObject root =
            CreateImage(
                parent,
                name,
                position,
                size,
                color,
                sprite);

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
                size.x - 14f,
                size.y - 8f),
            22f,
            Color.white,
            FontStyles.Bold,
            TextAlignmentOptions.Center);

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

        root.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            root.GetComponent<
                RectTransform>();

        Stretch(
            rect);

        root.GetComponent<Image>()
            .color =
                color;

        return root;
    }

    private static GameObject CreateImage(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color,
        Sprite sprite)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));

        root.transform.SetParent(
            parent,
            false);

        SetRect(
            root.GetComponent<
                RectTransform>(),
            position,
            size);

        Image image =
            root.GetComponent<Image>();

        image.color =
            color;

        if (sprite != null)
        {
            image.sprite =
                sprite;

            image.type =
                sprite.border !=
                Vector4.zero
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
        }

        return root;
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
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));

        root.transform.SetParent(
            parent,
            false);

        SetRect(
            root.GetComponent<
                RectTransform>(),
            position,
            size);

        TextMeshProUGUI text =
            root.GetComponent<
                TextMeshProUGUI>();

        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode =
            TextWrappingModes.Normal;

        if (defaultFont != null)
        {
            text.font =
                defaultFont;
        }

        return text;
    }

    private static RectTransform CreateEmptyRect(
        Transform parent,
        string name)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform));

        root.transform.SetParent(
            parent,
            false);

        return root.GetComponent<
            RectTransform>();
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 position,
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
            position;

        rect.sizeDelta =
            size;
    }

    private static void Stretch(
        RectTransform rect)
    {
        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;
    }

    private static RectTransform FindCameraResetRect(
        Transform overlayRoot)
    {
        if (overlayRoot == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours =
            overlayRoot.GetComponentsInChildren<
                MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour
                 in behaviours)
        {
            if (behaviour != null &&
                behaviour.GetType().Name ==
                "UXCameraResetButton")
            {
                return behaviour.transform
                    as RectTransform;
            }
        }

        RectTransform[] rects =
            overlayRoot.GetComponentsInChildren<
                RectTransform>(true);

        foreach (RectTransform rect
                 in rects)
        {
            if (rect == null)
            {
                continue;
            }

            string lower =
                rect.name.ToLowerInvariant();

            if (lower.Contains("reset") &&
                lower.Contains("camera"))
            {
                return rect;
            }
        }

        return null;
    }

    private static void ResolveKenneySprites()
    {
        defaultFont =
            TMP_Settings.defaultFontAsset;

        List<Sprite> sprites =
            new List<Sprite>();

        string[] guids =
            AssetDatabase.FindAssets(
                "t:Texture2D");

        foreach (string guid
                 in guids)
        {
            string path =
                AssetDatabase
                    .GUIDToAssetPath(
                        guid)
                    .Replace(
                        "\\",
                        "/");

            if (path.IndexOf(
                    "kenney",
                    StringComparison.OrdinalIgnoreCase) <
                0)
            {
                continue;
            }

            UnityEngine.Object[] assets =
                AssetDatabase
                    .LoadAllAssetsAtPath(
                        path);

            foreach (UnityEngine.Object asset
                     in assets)
            {
                if (asset is Sprite sprite)
                {
                    sprites.Add(
                        sprite);
                }
            }
        }

        kenneyButtonSprite =
            FindSprite(
                sprites,
                "button");

        kenneyPanelSprite =
            FindSprite(
                sprites,
                "panel");

        kenneyCircleSprite =
            sprites.FirstOrDefault(
                sprite =>
                    sprite.name
                        .ToLowerInvariant()
                        .Contains("round"));

        kenneySliderSprite =
            FindSprite(
                sprites,
                "slider");

        kenneySettingsIconSprite =
            sprites.FirstOrDefault(
                sprite =>
                {
                    string name =
                        sprite.name
                            .ToLowerInvariant();

                    return name.Contains("setting") ||
                           name.Contains("gear") ||
                           name.Contains("cog");
                });
    }

    private static Sprite FindSprite(
        List<Sprite> sprites,
        string term)
    {
        return sprites.FirstOrDefault(
            sprite =>
                sprite.name
                    .IndexOf(
                        term,
                        StringComparison.OrdinalIgnoreCase) >=
                0);
    }

    private static Transform FindRecursive(
        Transform root,
        string targetName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child
                 in root)
        {
            if (child.name ==
                targetName)
            {
                return child;
            }

            Transform nested =
                FindRecursive(
                    child,
                    targetName);

            if (nested != null)
            {
                return nested;
            }
        }

        return null;
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
}
#endif
