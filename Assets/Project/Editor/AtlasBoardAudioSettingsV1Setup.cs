#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class AtlasBoardAudioSettingsV1Setup
{
    private const string AudioFolder =
        "Assets/Project/Audio";

    private const string DataFolder =
        "Assets/Project/Data/Audio";

    private const string LibraryPath =
        DataFolder +
        "/AudioLibrary_Default.asset";

    private const string SettingsCanvasName =
        "Canvas_Settings";

    private static TMP_FontAsset defaultFont;
    private static Sprite kenneyButtonSprite;
    private static Sprite kenneyPanelSprite;
    private static Sprite kenneyCircleSprite;
    private static Sprite kenneySliderHandleSprite;
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
        "Atlas Board/Audio/Build Audio + Settings Foundation v1.3.4")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "Exit Play Mode before building Audio + Settings.");

            return;
        }

        ResolveKenneySprites();
        EnsureFolder(AudioFolder);
        EnsureFolder(DataFolder);

        AtlasBoardAudioLibrary library =
            AssetDatabase.LoadAssetAtPath<
                AtlasBoardAudioLibrary>(
                    LibraryPath);

        if (library == null)
        {
            library =
                ScriptableObject.CreateInstance<
                    AtlasBoardAudioLibrary>();

            AssetDatabase.CreateAsset(
                library,
                LibraryPath);
        }

        EditorUtility.SetDirty(
            library);

        GameObject audioSystem =
            FindSceneObject(
                "AudioSystem");

        if (audioSystem == null)
        {
            audioSystem =
                new GameObject(
                    "AudioSystem");

            Undo.RegisterCreatedObjectUndo(
                audioSystem,
                "Create AudioSystem");
        }

        AtlasBoardAudioManager manager =
            audioSystem.GetComponent<
                AtlasBoardAudioManager>();

        if (manager == null)
        {
            manager =
                Undo.AddComponent<
                    AtlasBoardAudioManager>(
                        audioSystem);
        }

        manager.EditorConfigure(
            library);

        AttachDiceMotionAudio();
        BuildSettingsCanvas();
        AutoMarkKnownEscapeModals();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Audio files are normally imported before this setup is run.
        // Bind everything immediately so a separate Auto Bind step is not required.
        AutoBindAudioClips();
        ApplyToggleVisualFixToScene();

        if (audioSystem.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                audioSystem.scene);
        }

        Selection.activeGameObject =
            audioSystem;

        Debug.Log(
            "AtlasBoard Audio + Settings Foundation v1.3.4 built. " +
            "Settings window centered and imported audio clips auto-bound.");
    }

    [MenuItem(
        "Atlas Board/Audio/Auto Bind Audio Clips by AtlasBoard Names")]
    public static void AutoBindAudioClips()
    {
        AtlasBoardAudioLibrary library =
            AssetDatabase.LoadAssetAtPath<
                AtlasBoardAudioLibrary>(
                    LibraryPath);

        if (library == null)
        {
            Debug.LogWarning(
                "AudioLibrary_Default.asset was not found. " +
                "Run Build Audio + Settings Foundation v1 first.");

            return;
        }

        Dictionary<string, AudioClip>
            clips =
                FindAllAudioClipsByName();

        // Main music is optional for now. If the user later downloads
        // Two Simple Game Music Loops, either canonical or original names work.
        library.mainMenuMusic =
            FindFirstClip(
                clips,
                "AB_MainMenu_Music",
                "menumusicloop-tiggo");

        // Gameplay music intentionally remains optional because theme ambience
        // is the current gameplay bed.
        library.gameplayMusic =
            FindFirstClip(
                clips,
                "AB_Gameplay_Music",
                "levelmusicloop-tigrun");

        BindTheme(
            library,
            "classic_table",
            FindFirstClip(
                clips,
                "AB_Theme_Classic"));

        BindTheme(
            library,
            "garden",
            FindFirstClip(
                clips,
                "AB_Theme_Garden",
                "723913",
                "forest-birds-ambient-seamless-loop"));

        BindTheme(
            library,
            "beach",
            FindFirstClip(
                clips,
                "AB_Theme_Beach",
                "852826",
                "gentle-ocean-waves-loop"));

        BindTheme(
            library,
            "pavilion",
            FindFirstClip(
                clips,
                "AB_Theme_Pavilion",
                "482990",
                "people talking at cafe ambience"));

        BindTheme(
            library,
            "street",
            FindFirstClip(
                clips,
                "AB_Theme_Street",
                "866505",
                "peace l stone in focus"));

        AudioClip dice =
            FindFirstClip(
                clips,
                "AB_Dice_Roll_01",
                "AB_Dice_01",
                "die-throw-2");

        library.diceRolls =
            dice != null
                ? new[] { dice }
                : FindIndexedClips(
                    clips,
                    "AB_Dice_");

        library.uiClick =
            FindFirstClip(
                clips,
                "AB_UI_Click",
                "click_003");

        library.uiSelect =
            FindFirstClip(
                clips,
                "AB_UI_Select",
                "select_001");

        library.uiOpen =
            FindFirstClip(
                clips,
                "AB_UI_Open",
                "open_002");

        library.uiToggle =
            FindFirstClip(
                clips,
                "AB_UI_Toggle",
                "switch_001");

        library.uiError =
            FindFirstClip(
                clips,
                "AB_UI_Error",
                "error_001");

        library.pawnMove =
            FindFirstClip(
                clips,
                "AB_Pawn_Hop",
                "AB_Pawn_Move");

        library.card =
            FindFirstClip(
                clips,
                "AB_Card");

        library.coin =
            FindFirstClip(
                clips,
                "AB_Coin");

        library.purchase =
            FindFirstClip(
                clips,
                "AB_Purchase");

        library.rent =
            FindFirstClip(
                clips,
                "AB_Rent");

        library.auction =
            FindFirstClip(
                clips,
                "AB_Auction");

        library.trade =
            FindFirstClip(
                clips,
                "AB_Trade");

        library.success =
            FindFirstClip(
                clips,
                "AB_Success");

        library.warning =
            FindFirstClip(
                clips,
                "AB_Warning");

        EditorUtility.SetDirty(
            library);

        AssetDatabase.SaveAssets();

        int diceCount =
            library.diceRolls != null
                ? library.diceRolls.Length
                : 0;

        Debug.Log(
            "AudioLibrary auto-bind complete. " +
            $"MainMenu={(library.mainMenuMusic != null)}, " +
            $"Gameplay={(library.gameplayMusic != null)}, " +
            $"Dice clips={diceCount}. " +
            "Select AudioLibrary_Default to verify the remaining fields.");
    }

    [MenuItem(
        "Atlas Board/Audio/Attach Pawn Audio to Selected")]
    public static void AttachPawnAudio()
    {
        GameObject[] selected =
            Selection.gameObjects;

        if (selected == null ||
            selected.Length == 0)
        {
            Debug.LogWarning(
                "Select one or more pawn GameObjects first.");

            return;
        }

        int added = 0;

        foreach (GameObject target
                 in selected)
        {
            if (target == null)
            {
                continue;
            }

            if (target.GetComponent<
                    AtlasBoardPawnMotionAudio>() ==
                null)
            {
                Undo.AddComponent<
                    AtlasBoardPawnMotionAudio>(
                        target);

                added++;
            }
        }

        Debug.Log(
            $"Pawn motion audio added to {added} selected object(s).");
    }

    private static void BuildSettingsCanvas()
    {
        GameObject oldCanvas =
            FindSceneObject(
                SettingsCanvasName);

        if (oldCanvas != null)
        {
            Undo.DestroyObjectImmediate(
                oldCanvas);
        }

        GameObject canvasObject =
            new GameObject(
                SettingsCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

        Undo.RegisterCreatedObjectUndo(
            canvasObject,
            "Build AtlasBoard Settings");

        Canvas canvas =
            canvasObject.GetComponent<
                Canvas>();

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

        AtlasBoardSettingsOverlayController
            controller =
                canvasObject.AddComponent<
                    AtlasBoardSettingsOverlayController>();

        GameObject settingsRoot =
            CreateStretchImage(
                canvasObject.transform,
                "SettingsRoot",
                Overlay);

        GameObject window =
            CreatePanel(
                settingsRoot.transform,
                "SettingsWindow",
                Vector2.zero,
                new Vector2(
                    1120f,
                    840f),
                Panel);

        CreateImage(
            window.transform,
            "Header",
            new Vector2(
                0f,
                -370f),
            new Vector2(
                1120f,
                100f),
            BlueDark,
            kenneyButtonSprite);

        CreateText(
            window.transform,
            "Title",
            "SETTINGS",
            new Vector2(
                0f,
                -370f),
            new Vector2(
                700f,
                70f),
            42f,
            Color.white,
            FontStyles.Bold,
            TextAlignmentOptions.Center);

        Button closeButton =
            CreateButton(
                window.transform,
                "Button_CloseSettings",
                "X",
                new Vector2(
                    505f,
                    -370f),
                new Vector2(
                    70f,
                    70f),
                Red,
                kenneyCircleSprite ??
                kenneyButtonSprite);

        GameObject tabs =
            new GameObject(
                "Tabs",
                typeof(RectTransform));

        tabs.transform.SetParent(
            window.transform,
            false);

        SetLocalRect(
            tabs.GetComponent<
                RectTransform>(),
            new Vector2(
                0f,
                -285f),
            new Vector2(
                1000f,
                64f));

        Button audioTab =
            CreateButton(
                tabs.transform,
                "Tab_Audio",
                "AUDIO",
                new Vector2(
                    -375f,
                    0f),
                new Vector2(
                    230f,
                    58f),
                Blue,
                kenneyButtonSprite);

        Button gameplayTab =
            CreateButton(
                tabs.transform,
                "Tab_Gameplay",
                "GAMEPLAY",
                new Vector2(
                    -125f,
                    0f),
                new Vector2(
                    230f,
                    58f),
                Blue,
                kenneyButtonSprite);

        Button graphicsTab =
            CreateButton(
                tabs.transform,
                "Tab_Graphics",
                "GRAPHICS",
                new Vector2(
                    125f,
                    0f),
                new Vector2(
                    230f,
                    58f),
                Blue,
                kenneyButtonSprite);

        Button controlsTab =
            CreateButton(
                tabs.transform,
                "Tab_Controls",
                "CONTROLS",
                new Vector2(
                    375f,
                    0f),
                new Vector2(
                    230f,
                    58f),
                Blue,
                kenneyButtonSprite);

        GameObject content =
            CreatePanel(
                window.transform,
                "Content",
                new Vector2(
                    0f,
                    55f),
                new Vector2(
                    1000f,
                    500f),
                new Color32(
                    233,
                    233,
                    239,
                    255));

        GameObject audioPanel =
            new GameObject(
                "AudioSettings",
                typeof(RectTransform));

        audioPanel.transform.SetParent(
            content.transform,
            false);

        StretchLocal(
            audioPanel.GetComponent<
                RectTransform>());

        CreateText(
            audioPanel.transform,
            "AudioHeader",
            "AUDIO SETTINGS",
            new Vector2(
                0f,
                -195f),
            new Vector2(
                850f,
                55f),
            31f,
            TextDark,
            FontStyles.Bold,
            TextAlignmentOptions.Left);

        SliderRow master =
            CreateSliderRow(
                audioPanel.transform,
                "Master",
                "Master Volume",
                -115f);

        SliderRow music =
            CreateSliderRow(
                audioPanel.transform,
                "MainMusic",
                "Main Music",
                -30f);

        SliderRow theme =
            CreateSliderRow(
                audioPanel.transform,
                "Theme",
                "Theme / Ambience",
                55f);

        SliderRow dice =
            CreateSliderRow(
                audioPanel.transform,
                "Dice",
                "Dice",
                140f);

        SliderRow effects =
            CreateSliderRow(
                audioPanel.transform,
                "Effects",
                "Effects / UI / Pawn",
                225f);

        GameObject placeholderPanel =
            new GameObject(
                "PlaceholderSettings",
                typeof(RectTransform));

        placeholderPanel.transform.SetParent(
            content.transform,
            false);

        StretchLocal(
            placeholderPanel.GetComponent<
                RectTransform>());

        TMP_Text placeholderTitle =
            CreateText(
                placeholderPanel.transform,
                "PlaceholderTitle",
                "GAMEPLAY SETTINGS",
                new Vector2(
                    0f,
                    -80f),
                new Vector2(
                    800f,
                    70f),
                34f,
                TextDark,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        CreateText(
            placeholderPanel.transform,
            "PlaceholderBody",
            "This category is reserved for the next settings phase.",
            new Vector2(
                0f,
                30f),
            new Vector2(
                760f,
                110f),
            24f,
            new Color32(
                100,
                102,
                110,
                255),
            FontStyles.Normal,
            TextAlignmentOptions.Center);

        Button cancelButton =
            CreateButton(
                window.transform,
                "Button_Cancel",
                "CANCEL",
                new Vector2(
                    220f,
                    365f),
                new Vector2(
                    200f,
                    64f),
                new Color32(
                    125,
                    130,
                    145,
                    255),
                kenneyButtonSprite);

        Button applyButton =
            CreateButton(
                window.transform,
                "Button_Apply",
                "APPLY",
                new Vector2(
                    440f,
                    365f),
                new Vector2(
                    200f,
                    64f),
                Green,
                kenneyButtonSprite);

        Button gameplaySettingsButton =
            BuildGameplaySettingsButton();

        controller.EditorConfigure(
            settingsRoot,
            audioPanel,
            placeholderPanel,
            placeholderTitle,
            audioTab,
            gameplayTab,
            graphicsTab,
            controlsTab,
            master.Slider,
            music.Slider,
            theme.Slider,
            dice.Slider,
            effects.Slider,
            master.Value,
            music.Value,
            theme.Value,
            dice.Value,
            effects.Value,
            applyButton,
            cancelButton,
            closeButton,
            gameplaySettingsButton);

        placeholderPanel.SetActive(
            false);

        settingsRoot.SetActive(
            false);

        EnsureEventSystem();

        EditorUtility.SetDirty(
            controller);

        EditorSceneManager.MarkSceneDirty(
            canvasObject.scene);
    }

    private static Button BuildGameplaySettingsButton()
    {
        GameObject overlay =
            FindSceneObject(
                "Canvas_UXOverlay");

        if (overlay == null)
        {
            Debug.LogWarning(
                "Canvas_UXOverlay was not found. " +
                "Gameplay Settings button was not created. " +
                "ESC will still open Settings.");

            return null;
        }

        Transform existing =
            overlay.transform.Find(
                "Button_Settings");

        if (existing != null)
        {
            Undo.DestroyObjectImmediate(
                existing.gameObject);
        }

        GameObject buttonObject =
            new GameObject(
                "Button_Settings",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));

        RectTransform cameraResetRect =
            FindCameraResetRect(
                overlay.transform);

        Transform buttonParent =
            cameraResetRect != null
                ? cameraResetRect.parent
                : overlay.transform;

        buttonObject.transform.SetParent(
            buttonParent,
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

            rect.anchoredPosition =
                new Vector2(
                    -24f,
                    -92f);

            rect.sizeDelta =
                new Vector2(
                    64f,
                    64f);
        }

        Image image =
            buttonObject.GetComponent<
                Image>();

        image.color = Blue;

        if (kenneySettingsIconSprite != null)
        {
            image.sprite =
                kenneySettingsIconSprite;

            image.color =
                Color.white;

            image.preserveAspect =
                true;
        }
        else if (kenneyCircleSprite != null)
        {
            image.sprite =
                kenneyCircleSprite;
        }

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

        return button;
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
            if (behaviour == null)
            {
                continue;
            }

            if (behaviour.GetType().Name ==
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

            bool resetLike =
                lower.Contains("reset") &&
                (lower.Contains("camera") ||
                 lower.Contains("view"));

            if (resetLike)
            {
                return rect;
            }
        }

        return null;
    }

    private static SliderRow CreateSliderRow(
        Transform parent,
        string id,
        string label,
        float y)
    {
        SliderRow result =
            new SliderRow();

        CreateText(
            parent,
            $"Label_{id}",
            label,
            new Vector2(
                -300f,
                y),
            new Vector2(
                300f,
                48f),
            24f,
            TextDark,
            FontStyles.Bold,
            TextAlignmentOptions.Left);

        GameObject sliderObject =
            new GameObject(
                $"Slider_{id}",
                typeof(RectTransform),
                typeof(Slider));

        sliderObject.transform.SetParent(
            parent,
            false);

        SetLocalRect(
            sliderObject.GetComponent<
                RectTransform>(),
            new Vector2(
                115f,
                y),
            new Vector2(
                520f,
                42f));

        GameObject background =
            CreateImage(
                sliderObject.transform,
                "Background",
                Vector2.zero,
                new Vector2(
                    500f,
                    16f),
                new Color32(
                    190,
                    197,
                    207,
                    255),
                null);

        RectTransform fillArea =
            CreateEmptyRect(
                sliderObject.transform,
                "Fill Area");

        fillArea.anchorMin =
            new Vector2(
                0f,
                0f);

        fillArea.anchorMax =
            new Vector2(
                1f,
                1f);

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

        fillRect.anchorMin =
            Vector2.zero;

        fillRect.anchorMax =
            Vector2.one;

        fillRect.offsetMin =
            Vector2.zero;

        fillRect.offsetMax =
            Vector2.zero;

        RectTransform handleArea =
            CreateEmptyRect(
                sliderObject.transform,
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
                kenneySliderHandleSprite ??
                kenneyCircleSprite);

        Slider slider =
            sliderObject.GetComponent<
                Slider>();

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.value = 0.8f;
        slider.fillRect =
            fillRect;
        slider.handleRect =
            handle.GetComponent<
                RectTransform>();
        slider.targetGraphic =
            handle.GetComponent<
                Image>();
        slider.direction =
            Slider.Direction.LeftToRight;

        result.Slider =
            slider;

        result.Value =
            CreateText(
                parent,
                $"Value_{id}",
                "80%",
                new Vector2(
                    430f,
                    y),
                new Vector2(
                    100f,
                    42f),
                22f,
                TextDark,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        return result;
    }

    private static void AttachDiceMotionAudio()
    {
        GameObject boardRoot =
            FindSceneObject(
                "BoardRoot");

        if (boardRoot == null)
        {
            return;
        }

        Transform diceRoot =
            boardRoot.transform.Find(
                "DiceVisualRoot");

        if (diceRoot == null)
        {
            return;
        }

        if (diceRoot.GetComponent<
                AtlasBoardDiceMotionAudio>() ==
            null)
        {
            Undo.AddComponent<
                AtlasBoardDiceMotionAudio>(
                    diceRoot.gameObject);
        }
    }

    private static void AutoMarkKnownEscapeModals()
    {
        RectTransform[] rects =
            UnityEngine.Object
                .FindObjectsByType<
                    RectTransform>(
                        FindObjectsInactive.Include);

        string[] keywords =
        {
            "tablet",
            "trade",
            "takas",
            "auction",
            "ihale",
            "purchase",
            "event",
            "travel",
            "penalty",
            "ceza",
            "develop",
            "result"
        };

        int added = 0;

        foreach (RectTransform rect
                 in rects)
        {
            if (rect == null ||
                !rect.gameObject.scene.IsValid())
            {
                continue;
            }

            string lower =
                rect.name.ToLowerInvariant();

            if (lower.StartsWith(
                    "canvas_") ||
                lower.Contains(
                    "button") ||
                lower.Contains(
                    "label") ||
                lower.Contains(
                    "status") ||
                lower.Contains(
                    "hud") ||
                lower.Contains(
                    "settings"))
            {
                continue;
            }

            bool matches = false;

            foreach (string keyword
                     in keywords)
            {
                if (lower.Contains(
                        keyword))
                {
                    matches = true;
                    break;
                }
            }

            if (!matches)
            {
                continue;
            }

            if (rect.rect.width < 220f ||
                rect.rect.height < 120f)
            {
                continue;
            }

            if (rect.GetComponent<
                    AtlasBoardEscapeBlocker>() ==
                null)
            {
                Undo.AddComponent<
                    AtlasBoardEscapeBlocker>(
                        rect.gameObject);

                added++;
            }
        }

        Debug.Log(
            $"ESC priority scan completed. " +
            $"AtlasBoardEscapeBlocker added to {added} likely modal panel(s).");
    }

    private static void ApplyToggleVisualFixToScene()
    {
        Toggle[] toggles =
            UnityEngine.Object
                .FindObjectsByType<
                    Toggle>(
                        FindObjectsInactive.Include);

        int fixedCount = 0;

        foreach (Toggle toggle
                 in toggles)
        {
            if (toggle == null ||
                !toggle.gameObject.scene.IsValid())
            {
                continue;
            }

            AtlasBoardToggleVisualFix visualFix =
                toggle.GetComponent<
                    AtlasBoardToggleVisualFix>();

            if (visualFix == null)
            {
                visualFix =
                    Undo.AddComponent<
                        AtlasBoardToggleVisualFix>(
                            toggle.gameObject);
            }

            visualFix.ApplyStyle();

            EditorUtility.SetDirty(
                toggle);

            fixedCount++;
        }

        Debug.Log(
            $"Toggle visual polish applied to {fixedCount} Toggle(s).");
    }

    private static Dictionary<string, AudioClip>
        FindAllAudioClipsByName()
    {
        Dictionary<string, AudioClip> result =
            new Dictionary<string, AudioClip>(
                StringComparer.OrdinalIgnoreCase);

        string[] guids =
            AssetDatabase.FindAssets(
                "t:AudioClip");

        foreach (string guid
                 in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            AudioClip clip =
                AssetDatabase.LoadAssetAtPath<
                    AudioClip>(
                        path);

            if (clip == null)
            {
                continue;
            }

            result[clip.name] =
                clip;
        }

        return result;
    }

    private static AudioClip FindFirstClip(
        Dictionary<string, AudioClip> clips,
        params string[] namesOrFragments)
    {
        if (clips == null ||
            namesOrFragments == null)
        {
            return null;
        }

        foreach (string query
                 in namesOrFragments)
        {
            if (string.IsNullOrWhiteSpace(
                    query))
            {
                continue;
            }

            if (clips.TryGetValue(
                    query,
                    out AudioClip exact) &&
                exact != null)
            {
                return exact;
            }
        }

        foreach (string query
                 in namesOrFragments)
        {
            if (string.IsNullOrWhiteSpace(
                    query))
            {
                continue;
            }

            foreach (KeyValuePair<string, AudioClip>
                     pair in clips)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                if (pair.Key.IndexOf(
                        query,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return pair.Value;
                }
            }
        }

        return null;
    }

    private static AudioClip FindClip(
        Dictionary<string, AudioClip> clips,
        string name)
    {
        return clips.TryGetValue(
            name,
            out AudioClip clip)
                ? clip
                : null;
    }

    private static AudioClip[] FindIndexedClips(
        Dictionary<string, AudioClip> clips,
        string prefix)
    {
        return clips
            .Where(
                pair =>
                    pair.Key.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
            .OrderBy(
                pair =>
                    pair.Key)
            .Select(
                pair =>
                    pair.Value)
            .Where(
                clip =>
                    clip != null)
            .ToArray();
    }

    private static void BindTheme(
        AtlasBoardAudioLibrary library,
        string themeId,
        AudioClip clip)
    {
        if (library.themeAudio == null)
        {
            return;
        }

        foreach (AtlasBoardAudioLibrary
                     .ThemeAudioEntry entry
                 in library.themeAudio)
        {
            if (entry == null)
            {
                continue;
            }

            if (string.Equals(
                    entry.themeId,
                    themeId,
                    StringComparison.OrdinalIgnoreCase))
            {
                entry.ambienceOrMusic =
                    clip;

                return;
            }
        }
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
                "button",
                "rectangle");

        kenneyPanelSprite =
            FindSprite(
                sprites,
                "panel");

        kenneyCircleSprite =
            FindSprite(
                sprites,
                "button",
                "round");

        kenneySliderHandleSprite =
            FindSprite(
                sprites,
                "slider");

        kenneySettingsIconSprite =
            sprites.FirstOrDefault(
                sprite =>
                {
                    string n =
                        sprite.name.ToLowerInvariant();

                    return n.Contains(
                               "setting") ||
                           n.Contains(
                               "gear") ||
                           n.Contains(
                               "cog");
                });
    }

    private static Sprite FindSprite(
        List<Sprite> sprites,
        params string[] terms)
    {
        if (sprites == null)
        {
            return null;
        }

        foreach (Sprite sprite
                 in sprites)
        {
            string name =
                sprite.name.ToLowerInvariant();

            bool all =
                terms.All(
                    term =>
                        name.Contains(
                            term.ToLowerInvariant()));

            if (all)
            {
                return sprite;
            }
        }

        return null;
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
        Sprite sprite)
    {
        GameObject obj =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));

        obj.transform.SetParent(
            parent,
            false);

        SetLocalRect(
            obj.GetComponent<
                RectTransform>(),
            position,
            size);

        Image image =
            obj.GetComponent<
                Image>();

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

        return obj;
    }

    private static GameObject CreateStretchImage(
        Transform parent,
        string name,
        Color color)
    {
        GameObject obj =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));

        obj.transform.SetParent(
            parent,
            false);

        StretchLocal(
            obj.GetComponent<
                RectTransform>());

        obj.GetComponent<
            Image>().color =
                color;

        return obj;
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
        GameObject obj =
            CreateImage(
                parent,
                name,
                position,
                size,
                color,
                sprite);

        Button button =
            obj.AddComponent<
                Button>();

        button.targetGraphic =
            obj.GetComponent<
                Image>();

        CreateText(
            obj.transform,
            "Label",
            label,
            Vector2.zero,
            new Vector2(
                size.x - 18f,
                size.y - 10f),
            23f,
            Color.white,
            FontStyles.Bold,
            TextAlignmentOptions.Center);

        return button;
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

        SetLocalRect(
            obj.GetComponent<
                RectTransform>(),
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
        GameObject obj =
            new GameObject(
                name,
                typeof(RectTransform));

        obj.transform.SetParent(
            parent,
            false);

        return obj.GetComponent<
            RectTransform>();
    }

    private static void SetLocalRect(
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
            new Vector2(
                position.x,
                -position.y);

        rect.sizeDelta =
            size;
    }

    private static void StretchLocal(
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

        string folder =
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
            folder);
    }

    private class SliderRow
    {
        public Slider Slider;
        public TMP_Text Value;
    }
}
#endif
