using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-4000)]
[DisallowMultipleComponent]
public class AtlasBoardSettingsV2Controller : MonoBehaviour
{
    public static AtlasBoardSettingsV2Controller Instance
    {
        get;
        private set;
    }

    private GameObject settingsRoot;

    private GameObject audioPanel;
    private GameObject gameplayPanel;
    private GameObject graphicsPanel;
    private GameObject controlsPanel;

    private Button audioTabButton;
    private Button gameplayTabButton;
    private Button graphicsTabButton;
    private Button controlsTabButton;

    private Slider masterSlider;
    private Slider mainMusicSlider;
    private Slider themeSlider;
    private Slider diceSlider;
    private Slider effectsSlider;

    private TMP_Text masterValueText;
    private TMP_Text mainMusicValueText;
    private TMP_Text themeValueText;
    private TMP_Text diceValueText;
    private TMP_Text effectsValueText;

    private Toggle muteToggle;

    private TMP_Dropdown languageDropdown;

    private Slider cameraSlider;
    private Slider zoomSlider;
    private Slider panSlider;
    private Slider botSpeedSlider;

    private TMP_Text cameraValueText;
    private TMP_Text zoomValueText;
    private TMP_Text panValueText;
    private TMP_Text botSpeedValueText;

    private Toggle reduceMotionToggle;
    private Toggle uiHintsToggle;
    private Toggle confirmationsToggle;

    private TMP_Dropdown resolutionDropdown;
    private TMP_Dropdown displayModeDropdown;
    private TMP_Dropdown qualityDropdown;
    private Toggle vsyncToggle;
    private TMP_Dropdown fpsLimitDropdown;
    private TMP_Dropdown shadowDropdown;
    private TMP_Dropdown antiAliasingDropdown;
    private Toggle showFpsToggle;

    private TMP_Text currentResolutionText;

    private Button resetDefaultsButton;
    private Button cancelButton;
    private Button applyButton;
    private Button closeButton;
    private Button gameplaySettingsButton;

    private MonoBehaviour gameplayShortcutController;
    private bool gameplayShortcutWasEnabled;

    private bool mainMenuButtonHooked;
    private bool initializedUi;

    private AtlasBoardUserSettingsValues savedUserSettings;
    private AtlasBoardAudioSettingsValues savedAudioSettings;

    private readonly List<Vector2Int>
        availableResolutions =
            new List<Vector2Int>();

    private static readonly int[] FpsOptions =
    {
        30,
        60,
        90,
        120,
        144,
        165,
        240,
        0
    };

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        ResolveReferences();
        HookControls();
        BuildResolutionOptions();

        savedUserSettings =
            AtlasBoardUserSettingsStore.Load();

        savedAudioSettings =
            AtlasBoardAudioSettings.Load();

        AtlasBoardUserSettingsRuntime.SetCurrent(
            savedUserSettings);

        ApplyUserSettings(
            savedUserSettings,
            applyDisplayChanges: true);

        ApplyAudioSettings(
            savedAudioSettings,
            savedUserSettings.AudioMuted);

        if (settingsRoot != null)
        {
            settingsRoot.SetActive(
                false);
        }
    }

    private void Start()
    {
        TryHookMainMenuSettingsButton();
        UpdateGameplaySettingsButtonVisibility();
        RefreshCurrentResolutionLabel();
    }

    private void Update()
    {
        if (!mainMenuButtonHooked)
        {
            TryHookMainMenuSettingsButton();
        }

        UpdateGameplaySettingsButtonVisibility();

        if (settingsRoot != null &&
            settingsRoot.activeSelf)
        {
            KeepLegacySettingsModalHidden();

            if (WasEscapePressedThisFrame())
            {
                CancelAndClose();
            }

            return;
        }

        if (IsMenuFlowVisible())
        {
            return;
        }

        if (!WasEscapePressedThisFrame())
        {
            return;
        }

        if (HasBlockingGameplayModal())
        {
            return;
        }

        OpenSettings();
    }

    public void OpenSettings()
    {
        // Some menu/gameplay transitions may disable Canvas_Settings itself.
        // The external gameplay SET button must be able to bring it back.
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(
                true);
        }

        ResolveReferences();
        HookControls();
        BuildResolutionOptions();

        AtlasBoardAudioManager.Instance
            ?.PlayUiOpen();

        savedUserSettings =
            AtlasBoardUserSettingsStore.Load();

        savedAudioSettings =
            AtlasBoardAudioSettings.Load();

        PopulateUi(
            savedUserSettings,
            savedAudioSettings);

        ShowAudioTab();

        if (settingsRoot != null)
        {
            settingsRoot.SetActive(
                true);
        }

        DisableGameplayShortcutController();
        KeepLegacySettingsModalHidden();
    }

    public void ApplyAndClose()
    {
        AtlasBoardAudioSettingsValues audio =
            ReadAudioUi();

        AtlasBoardUserSettingsValues user =
            ReadUserUi();

        AtlasBoardAudioSettings.Save(
            audio);

        AtlasBoardUserSettingsStore.Save(
            user);

        savedAudioSettings =
            audio;

        savedUserSettings =
            user;

        AtlasBoardUserSettingsRuntime.SetCurrent(
            user);

        AtlasBoardLocalizationManager.Instance
            ?.SetLanguage(
                user.LanguageCode);

        ApplyAudioSettings(
            audio,
            user.AudioMuted);

        ApplyUserSettings(
            user,
            applyDisplayChanges: true);

        CloseInternal();
    }

    public void CancelAndClose()
    {
        ApplyAudioSettings(
            savedAudioSettings,
            savedUserSettings.AudioMuted);

        AtlasBoardLocalizationManager.Instance
            ?.SetLanguage(
                savedUserSettings.LanguageCode);

        CloseInternal();
    }

    public void ResetDefaultsPreview()
    {
        AtlasBoardUserSettingsValues userDefaults =
            AtlasBoardUserSettingsValues.Default;

        AtlasBoardAudioSettingsValues audioDefaults =
            AtlasBoardAudioSettingsValues.Default;

        PopulateUi(
            userDefaults,
            audioDefaults);

        AtlasBoardLocalizationManager.Instance
            ?.SetLanguage(
                userDefaults.LanguageCode);

        ApplyAudioSettings(
            audioDefaults,
            userDefaults.AudioMuted);
    }

    public void ShowAudioTab()
    {
        ShowOnly(
            audioPanel);
    }

    public void ShowGameplayTab()
    {
        ShowOnly(
            gameplayPanel);
    }

    public void ShowGraphicsTab()
    {
        ShowOnly(
            graphicsPanel);

        RefreshCurrentResolutionLabel();
    }

    public void ShowControlsTab()
    {
        ShowOnly(
            controlsPanel);
    }

    private void ShowOnly(
        GameObject panel)
    {
        SetActive(
            audioPanel,
            panel == audioPanel);

        SetActive(
            gameplayPanel,
            panel == gameplayPanel);

        SetActive(
            graphicsPanel,
            panel == graphicsPanel);

        SetActive(
            controlsPanel,
            panel == controlsPanel);
    }

    private void ResolveReferences()
    {
        settingsRoot =
            FindChildObject(
                "SettingsRoot");

        audioPanel =
            FindChildObject(
                "AudioSettings");

        gameplayPanel =
            FindChildObject(
                "GameplaySettings");

        graphicsPanel =
            FindChildObject(
                "GraphicsSettings");

        controlsPanel =
            FindChildObject(
                "ControlsSettings");

        audioTabButton =
            FindButton(
                "Tab_Audio");

        gameplayTabButton =
            FindButton(
                "Tab_Gameplay");

        graphicsTabButton =
            FindButton(
                "Tab_Graphics");

        controlsTabButton =
            FindButton(
                "Tab_Controls");

        masterSlider =
            FindSlider(
                "Slider_Master");

        mainMusicSlider =
            FindSlider(
                "Slider_MainMusic");

        themeSlider =
            FindSlider(
                "Slider_Theme");

        diceSlider =
            FindSlider(
                "Slider_Dice");

        effectsSlider =
            FindSlider(
                "Slider_Effects");

        masterValueText =
            FindText(
                "Value_Master");

        mainMusicValueText =
            FindText(
                "Value_MainMusic");

        themeValueText =
            FindText(
                "Value_Theme");

        diceValueText =
            FindText(
                "Value_Dice");

        effectsValueText =
            FindText(
                "Value_Effects");

        muteToggle =
            FindToggle(
                "Toggle_Mute");

        languageDropdown =
            FindDropdown(
                "Dropdown_Language");

        cameraSlider =
            FindSlider(
                "Slider_Camera");

        zoomSlider =
            FindSlider(
                "Slider_Zoom");

        panSlider =
            FindSlider(
                "Slider_Pan");

        botSpeedSlider =
            FindSlider(
                "Slider_BotSpeed");

        cameraValueText =
            FindText(
                "Value_Camera");

        zoomValueText =
            FindText(
                "Value_Zoom");

        panValueText =
            FindText(
                "Value_Pan");

        botSpeedValueText =
            FindText(
                "Value_BotSpeed");

        reduceMotionToggle =
            FindToggle(
                "Toggle_ReduceMotion");

        uiHintsToggle =
            FindToggle(
                "Toggle_UIHints");

        confirmationsToggle =
            FindToggle(
                "Toggle_Confirmations");

        resolutionDropdown =
            FindDropdown(
                "Dropdown_Resolution");

        displayModeDropdown =
            FindDropdown(
                "Dropdown_DisplayMode");

        qualityDropdown =
            FindDropdown(
                "Dropdown_Quality");

        vsyncToggle =
            FindToggle(
                "Toggle_VSync");

        fpsLimitDropdown =
            FindDropdown(
                "Dropdown_FPSLimit");

        shadowDropdown =
            FindDropdown(
                "Dropdown_Shadow");

        antiAliasingDropdown =
            FindDropdown(
                "Dropdown_AA");

        showFpsToggle =
            FindToggle(
                "Toggle_ShowFPS");

        currentResolutionText =
            FindText(
                "CurrentResolution");

        resetDefaultsButton =
            FindButton(
                "Button_ResetDefaults");

        cancelButton =
            FindButton(
                "Button_Cancel");

        applyButton =
            FindButton(
                "Button_Apply");

        closeButton =
            FindButton(
                "Button_CloseSettings");

        GameObject overlay =
            FindSceneObject(
                "Canvas_UXOverlay");

        if (overlay != null)
        {
            Transform buttonTransform =
                FindRecursive(
                    overlay.transform,
                    "Button_Settings");

            if (buttonTransform != null)
            {
                gameplaySettingsButton =
                    buttonTransform
                        .GetComponent<Button>();
            }
        }
    }

    private void HookControls()
    {
        if (initializedUi)
        {
            return;
        }

        initializedUi = true;

        AddClick(
            audioTabButton,
            ShowAudioTab);

        AddClick(
            gameplayTabButton,
            ShowGameplayTab);

        AddClick(
            graphicsTabButton,
            ShowGraphicsTab);

        AddClick(
            controlsTabButton,
            ShowControlsTab);

        AddClick(
            resetDefaultsButton,
            ResetDefaultsPreview);

        AddClick(
            cancelButton,
            CancelAndClose);

        AddClick(
            applyButton,
            ApplyAndClose);

        AddClick(
            closeButton,
            CancelAndClose);

        AddClick(
            gameplaySettingsButton,
            OpenSettings);

        AddAudioPreviewListener(
            masterSlider);

        AddAudioPreviewListener(
            mainMusicSlider);

        AddAudioPreviewListener(
            themeSlider);

        AddAudioPreviewListener(
            diceSlider);

        AddAudioPreviewListener(
            effectsSlider);

        if (muteToggle != null)
        {
            muteToggle.onValueChanged.AddListener(
                _ =>
                {
                    ApplyAudioSettings(
                        ReadAudioUi(),
                        muteToggle.isOn);
                });
        }

        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.AddListener(
                OnLanguageDropdownChanged);
        }

        AddPercentRefreshListener(
            cameraSlider,
            cameraValueText);

        AddPercentRefreshListener(
            zoomSlider,
            zoomValueText);

        AddPercentRefreshListener(
            panSlider,
            panValueText);

        AddPercentRefreshListener(
            botSpeedSlider,
            botSpeedValueText);

        if (qualityDropdown != null)
        {
            qualityDropdown.onValueChanged
                .AddListener(
                    ApplyQualityPresetPreview);
        }
    }

    private static void AddClick(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(
                action);
        }
    }

    private void AddAudioPreviewListener(
        Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.AddListener(
            _ =>
            {
                RefreshAudioValueLabels();

                ApplyAudioSettings(
                    ReadAudioUi(),
                    muteToggle != null &&
                    muteToggle.isOn);
            });
    }

    private static void AddPercentRefreshListener(
        Slider slider,
        TMP_Text valueText)
    {
        if (slider == null ||
            valueText == null)
        {
            return;
        }

        slider.onValueChanged.AddListener(
            value =>
            {
                valueText.text =
                    $"{Mathf.RoundToInt(value)}%";
            });
    }

    private void PopulateUi(
        AtlasBoardUserSettingsValues user,
        AtlasBoardAudioSettingsValues audio)
    {
        user =
            AtlasBoardUserSettingsStore.Clamp(
                user);

        SetSlider01(
            masterSlider,
            audio.Master);

        SetSlider01(
            mainMusicSlider,
            audio.MainMusic);

        SetSlider01(
            themeSlider,
            audio.Theme);

        SetSlider01(
            diceSlider,
            audio.Dice);

        SetSlider01(
            effectsSlider,
            audio.Effects);

        if (muteToggle != null)
        {
            muteToggle.SetIsOnWithoutNotify(
                user.AudioMuted);
        }

        SetLanguageDropdown(
            user.LanguageCode);

        SetPercentSlider(
            cameraSlider,
            user.Gameplay.CameraSensitivity);

        SetPercentSlider(
            zoomSlider,
            user.Gameplay.CameraZoomSensitivity);

        SetPercentSlider(
            panSlider,
            user.Gameplay.CameraPanSensitivity);

        SetPercentSlider(
            botSpeedSlider,
            user.Gameplay.BotTurnSpeed);

        SetToggle(
            reduceMotionToggle,
            user.Gameplay.ReduceCameraMotion);

        SetToggle(
            uiHintsToggle,
            user.Gameplay.UiHints);

        SetToggle(
            confirmationsToggle,
            user.Gameplay.GameplayConfirmations);

        SetResolutionDropdownValue(
            user.Graphics.ResolutionWidth,
            user.Graphics.ResolutionHeight);

        SetDropdown(
            displayModeDropdown,
            user.Graphics.DisplayMode);

        SetDropdown(
            qualityDropdown,
            user.Graphics.QualityPreset);

        SetToggle(
            vsyncToggle,
            user.Graphics.VSync);

        SetFpsDropdown(
            user.Graphics.FpsLimit);

        SetDropdown(
            shadowDropdown,
            user.Graphics.ShadowQuality);

        SetDropdown(
            antiAliasingDropdown,
            user.Graphics.AntiAliasing);

        SetToggle(
            showFpsToggle,
            user.Graphics.ShowFps);

        RefreshAudioValueLabels();
        RefreshGameplayValueLabels();
        RefreshCurrentResolutionLabel();
    }

    private AtlasBoardAudioSettingsValues ReadAudioUi()
    {
        AtlasBoardAudioSettingsValues defaults =
            AtlasBoardAudioSettingsValues.Default;

        return new AtlasBoardAudioSettingsValues
        {
            Master =
                ReadSlider01(
                    masterSlider,
                    defaults.Master),

            MainMusic =
                ReadSlider01(
                    mainMusicSlider,
                    defaults.MainMusic),

            Theme =
                ReadSlider01(
                    themeSlider,
                    defaults.Theme),

            Dice =
                ReadSlider01(
                    diceSlider,
                    defaults.Dice),

            Effects =
                ReadSlider01(
                    effectsSlider,
                    defaults.Effects)
        };
    }

    private AtlasBoardUserSettingsValues ReadUserUi()
    {
        AtlasBoardUserSettingsValues values =
            savedUserSettings;

        if (resolutionDropdown != null &&
            resolutionDropdown.value >= 0 &&
            resolutionDropdown.value <
                availableResolutions.Count)
        {
            Vector2Int resolution =
                availableResolutions[
                    resolutionDropdown.value];

            values.Graphics.ResolutionWidth =
                resolution.x;

            values.Graphics.ResolutionHeight =
                resolution.y;
        }

        Resolution current =
            Screen.currentResolution;

        values.Graphics.RefreshRate =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    (float)current
                        .refreshRateRatio
                        .value));

        values.Graphics.DisplayMode =
            ReadDropdown(
                displayModeDropdown,
                values.Graphics.DisplayMode);

        values.Graphics.QualityPreset =
            ReadDropdown(
                qualityDropdown,
                values.Graphics.QualityPreset);

        values.Graphics.VSync =
            ReadToggle(
                vsyncToggle,
                values.Graphics.VSync);

        values.Graphics.FpsLimit =
            ReadFpsDropdown(
                values.Graphics.FpsLimit);

        values.Graphics.ShadowQuality =
            ReadDropdown(
                shadowDropdown,
                values.Graphics.ShadowQuality);

        values.Graphics.AntiAliasing =
            ReadDropdown(
                antiAliasingDropdown,
                values.Graphics.AntiAliasing);

        values.Graphics.ShowFps =
            ReadToggle(
                showFpsToggle,
                values.Graphics.ShowFps);

        values.LanguageCode =
            GetSelectedLanguageCode(
                values.LanguageCode);

        values.Gameplay.CameraSensitivity =
            ReadPercent(
                cameraSlider,
                values.Gameplay.CameraSensitivity);

        values.Gameplay.CameraZoomSensitivity =
            ReadPercent(
                zoomSlider,
                values.Gameplay.CameraZoomSensitivity);

        values.Gameplay.CameraPanSensitivity =
            ReadPercent(
                panSlider,
                values.Gameplay.CameraPanSensitivity);

        values.Gameplay.BotTurnSpeed =
            ReadPercent(
                botSpeedSlider,
                values.Gameplay.BotTurnSpeed);

        values.Gameplay.ReduceCameraMotion =
            ReadToggle(
                reduceMotionToggle,
                values.Gameplay.ReduceCameraMotion);

        values.Gameplay.UiHints =
            ReadToggle(
                uiHintsToggle,
                values.Gameplay.UiHints);

        values.Gameplay.GameplayConfirmations =
            ReadToggle(
                confirmationsToggle,
                values.Gameplay.GameplayConfirmations);

        values.AudioMuted =
            ReadToggle(
                muteToggle,
                values.AudioMuted);

        return AtlasBoardUserSettingsStore.Clamp(
            values);
    }

    private void ApplyAudioSettings(
        AtlasBoardAudioSettingsValues audio,
        bool muted)
    {
        AtlasBoardAudioManager.Instance
            ?.ApplySettings(
                audio);

        AudioListener.volume =
            muted ? 0f : 1f;
    }

    private void ApplyUserSettings(
        AtlasBoardUserSettingsValues values,
        bool applyDisplayChanges)
    {
        values =
            AtlasBoardUserSettingsStore.Clamp(
                values);

        AtlasBoardUserSettingsRuntime.SetCurrent(
            values);

        ApplyGraphics(
            values.Graphics,
            applyDisplayChanges);

        ApplyGameplay(
            values.Gameplay);
    }

    private static void ApplyGraphics(
        AtlasBoardGraphicsSettingsValues graphics,
        bool applyDisplayChanges)
    {
        int qualityLevelCount =
            Mathf.Max(
                1,
                QualitySettings.names.Length);

        int qualityLevel =
            MapPresetToQualityLevel(
                graphics.QualityPreset,
                qualityLevelCount);

        QualitySettings.SetQualityLevel(
            qualityLevel,
            true);

        QualitySettings.vSyncCount =
            graphics.VSync ? 1 : 0;

        Application.targetFrameRate =
            graphics.FpsLimit <= 0
                ? -1
                : graphics.FpsLimit;

        switch (graphics.ShadowQuality)
        {
            case 0:
                QualitySettings.shadows =
                    ShadowQuality.Disable;
                break;

            case 1:
                QualitySettings.shadows =
                    ShadowQuality.HardOnly;

                QualitySettings.shadowResolution =
                    ShadowResolution.Low;
                break;

            case 2:
                QualitySettings.shadows =
                    ShadowQuality.All;

                QualitySettings.shadowResolution =
                    ShadowResolution.Medium;
                break;

            default:
                QualitySettings.shadows =
                    ShadowQuality.All;

                QualitySettings.shadowResolution =
                    ShadowResolution.High;
                break;
        }

        QualitySettings.antiAliasing =
            graphics.AntiAliasing switch
            {
                1 => 2,
                2 => 4,
                3 => 8,
                _ => 0
            };

        if (!applyDisplayChanges ||
            Application.isEditor)
        {
            return;
        }

        FullScreenMode mode =
            graphics.DisplayMode switch
            {
                0 =>
                    FullScreenMode
                        .ExclusiveFullScreen,

                1 =>
                    FullScreenMode
                        .FullScreenWindow,

                _ =>
                    FullScreenMode
                        .Windowed
            };

        Screen.SetResolution(
            graphics.ResolutionWidth,
            graphics.ResolutionHeight,
            mode);
    }

    private static int MapPresetToQualityLevel(
        int preset,
        int levelCount)
    {
        if (levelCount <= 1)
        {
            return 0;
        }

        float normalized =
            Mathf.Clamp(
                preset,
                0,
                3) /
            3f;

        return Mathf.Clamp(
            Mathf.RoundToInt(
                normalized *
                (levelCount - 1)),
            0,
            levelCount - 1);
    }

    private static void ApplyGameplay(
        AtlasBoardGameplaySettingsValues gameplay)
    {
        float rotationMultiplier =
            AtlasBoardUserSettingsRuntime
                .PercentToThreeXMultiplier(
                    gameplay.CameraSensitivity);

        float zoomMultiplier =
            AtlasBoardUserSettingsRuntime
                .PercentToThreeXMultiplier(
                    gameplay.CameraZoomSensitivity);

        float panMultiplier =
            AtlasBoardUserSettingsRuntime
                .PercentToThreeXMultiplier(
                    gameplay.CameraPanSensitivity);

        BoardCameraController camera =
            UnityEngine.Object
                .FindAnyObjectByType<
                    BoardCameraController>(
                        FindObjectsInactive.Include);

        if (camera != null)
        {
            camera.ApplyUserSettings(
                rotationMultiplier,
                zoomMultiplier,
                panMultiplier,
                gameplay.ReduceCameraMotion);
        }

        BoardCameraCollision collision =
            UnityEngine.Object
                .FindAnyObjectByType<
                    BoardCameraCollision>(
                        FindObjectsInactive.Include);

        if (collision != null)
        {
            collision.ApplyReduceCameraMotion(
                gameplay.ReduceCameraMotion);
        }
    }

    private void BuildResolutionOptions()
    {
        if (resolutionDropdown == null)
        {
            return;
        }

        availableResolutions.Clear();

        HashSet<string> seen =
            new HashSet<string>();

        Resolution[] resolutions =
            Screen.resolutions;

        foreach (Resolution resolution
                 in resolutions)
        {
            string key =
                $"{resolution.width}x{resolution.height}";

            if (!seen.Add(key))
            {
                continue;
            }

            availableResolutions.Add(
                new Vector2Int(
                    resolution.width,
                    resolution.height));
        }

        if (availableResolutions.Count == 0)
        {
            availableResolutions.Add(
                new Vector2Int(
                    Screen.width,
                    Screen.height));
        }

        availableResolutions.Sort(
            (a, b) =>
            {
                int pixelCompare =
                    (a.x * a.y)
                    .CompareTo(
                        b.x * b.y);

                if (pixelCompare != 0)
                {
                    return pixelCompare;
                }

                return a.x.CompareTo(
                    b.x);
            });

        List<string> labels =
            new List<string>();

        foreach (Vector2Int resolution
                 in availableResolutions)
        {
            labels.Add(
                $"{resolution.x} x {resolution.y}");
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(
            labels);
    }

    private void SetResolutionDropdownValue(
        int width,
        int height)
    {
        if (resolutionDropdown == null ||
            availableResolutions.Count == 0)
        {
            return;
        }

        int bestIndex = 0;
        long bestDifference =
            long.MaxValue;

        for (int i = 0;
             i < availableResolutions.Count;
             i++)
        {
            Vector2Int candidate =
                availableResolutions[i];

            long difference =
                Mathf.Abs(
                    candidate.x - width) *
                100000L +
                Mathf.Abs(
                    candidate.y - height);

            if (difference <
                bestDifference)
            {
                bestDifference =
                    difference;

                bestIndex = i;
            }
        }

        resolutionDropdown
            .SetValueWithoutNotify(
                bestIndex);

        resolutionDropdown
            .RefreshShownValue();
    }

    private void OnLanguageDropdownChanged(
        int index)
    {
        string code =
            AtlasBoardLocalizationLanguages.Codes[
                Mathf.Clamp(
                    index,
                    0,
                    AtlasBoardLocalizationLanguages.Codes.Length - 1)];

        AtlasBoardLocalizationManager.Instance
            ?.SetLanguage(
                code);

        RefreshCurrentResolutionLabel();
    }

    private void SetLanguageDropdown(
        string languageCode)
    {
        if (languageDropdown == null)
        {
            return;
        }

        languageDropdown.ClearOptions();

        languageDropdown.AddOptions(
            new List<string>(
                AtlasBoardLocalizationLanguages.NativeNames));

        languageDropdown.SetValueWithoutNotify(
            AtlasBoardLocalizationLanguages.IndexOf(
                languageCode));

        languageDropdown.RefreshShownValue();
    }

    private string GetSelectedLanguageCode(
        string fallback)
    {
        if (languageDropdown == null)
        {
            return AtlasBoardLocalizationLanguages.Normalize(
                fallback);
        }

        int index =
            Mathf.Clamp(
                languageDropdown.value,
                0,
                AtlasBoardLocalizationLanguages.Codes.Length - 1);

        return AtlasBoardLocalizationLanguages.Codes[
            index];
    }

    private void SetFpsDropdown(
        int fps)
    {
        if (fpsLimitDropdown == null)
        {
            return;
        }

        int index =
            Array.IndexOf(
                FpsOptions,
                fps);

        if (index < 0)
        {
            index = 1;
        }

        fpsLimitDropdown
            .SetValueWithoutNotify(
                index);

        fpsLimitDropdown
            .RefreshShownValue();
    }

    private int ReadFpsDropdown(
        int fallback)
    {
        if (fpsLimitDropdown == null ||
            fpsLimitDropdown.value < 0 ||
            fpsLimitDropdown.value >=
                FpsOptions.Length)
        {
            return fallback;
        }

        return FpsOptions[
            fpsLimitDropdown.value];
    }

    private void ApplyQualityPresetPreview(
        int preset)
    {
        switch (preset)
        {
            case 0:
                SetDropdown(
                    shadowDropdown,
                    1);

                SetDropdown(
                    antiAliasingDropdown,
                    0);
                break;

            case 1:
                SetDropdown(
                    shadowDropdown,
                    2);

                SetDropdown(
                    antiAliasingDropdown,
                    1);
                break;

            case 2:
                SetDropdown(
                    shadowDropdown,
                    3);

                SetDropdown(
                    antiAliasingDropdown,
                    2);
                break;

            default:
                SetDropdown(
                    shadowDropdown,
                    3);

                SetDropdown(
                    antiAliasingDropdown,
                    3);
                break;
        }
    }

    private void RefreshCurrentResolutionLabel()
    {
        if (currentResolutionText == null)
        {
            return;
        }

        Resolution current =
            Screen.currentResolution;

        int hz =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    (float)current
                        .refreshRateRatio
                        .value));

        currentResolutionText.text =
            Application.isEditor
                ? AtlasBoardL.T(
                    "settings.current_editor",
                    Screen.width,
                    Screen.height,
                    current.width,
                    current.height,
                    hz)
                : AtlasBoardL.T(
                    "settings.current_build",
                    Screen.width,
                    Screen.height,
                    hz);
    }

    private void RefreshAudioValueLabels()
    {
        SetSlider01Label(
            masterValueText,
            masterSlider);

        SetSlider01Label(
            mainMusicValueText,
            mainMusicSlider);

        SetSlider01Label(
            themeValueText,
            themeSlider);

        SetSlider01Label(
            diceValueText,
            diceSlider);

        SetSlider01Label(
            effectsValueText,
            effectsSlider);
    }

    private void RefreshGameplayValueLabels()
    {
        SetPercentLabel(
            cameraValueText,
            cameraSlider);

        SetPercentLabel(
            zoomValueText,
            zoomSlider);

        SetPercentLabel(
            panValueText,
            panSlider);

        SetPercentLabel(
            botSpeedValueText,
            botSpeedSlider);
    }

    private static void SetSlider01Label(
        TMP_Text text,
        Slider slider)
    {
        if (text == null ||
            slider == null)
        {
            return;
        }

        text.text =
            $"{Mathf.RoundToInt(slider.value * 100f)}%";
    }

    private static void SetPercentLabel(
        TMP_Text text,
        Slider slider)
    {
        if (text == null ||
            slider == null)
        {
            return;
        }

        text.text =
            $"{Mathf.RoundToInt(slider.value)}%";
    }

    private static void SetSlider01(
        Slider slider,
        float value)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(
                Mathf.Clamp01(
                    value));
        }
    }

    private static float ReadSlider01(
        Slider slider,
        float fallback)
    {
        return slider != null
            ? Mathf.Clamp01(
                slider.value)
            : fallback;
    }

    private static void SetPercentSlider(
        Slider slider,
        int value)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(
                Mathf.Clamp(
                    value,
                    1,
                    100));
        }
    }

    private static int ReadPercent(
        Slider slider,
        int fallback)
    {
        return slider != null
            ? Mathf.Clamp(
                Mathf.RoundToInt(
                    slider.value),
                1,
                100)
            : fallback;
    }

    private static void SetToggle(
        Toggle toggle,
        bool value)
    {
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(
                value);
        }
    }

    private static bool ReadToggle(
        Toggle toggle,
        bool fallback)
    {
        return toggle != null
            ? toggle.isOn
            : fallback;
    }

    private static void SetDropdown(
        TMP_Dropdown dropdown,
        int value)
    {
        if (dropdown == null ||
            dropdown.options == null ||
            dropdown.options.Count == 0)
        {
            return;
        }

        int clamped =
            Mathf.Clamp(
                value,
                0,
                dropdown.options.Count - 1);

        dropdown.SetValueWithoutNotify(
            clamped);

        dropdown.RefreshShownValue();
    }

    private static int ReadDropdown(
        TMP_Dropdown dropdown,
        int fallback)
    {
        return dropdown != null
            ? dropdown.value
            : fallback;
    }

    private void TryHookMainMenuSettingsButton()
    {
        GameObject canvas =
            FindSceneObject(
                "Canvas_MainMenu");

        if (canvas == null)
        {
            return;
        }

        Button[] buttons =
            canvas.GetComponentsInChildren<
                Button>(true);

        foreach (Button button
                 in buttons)
        {
            if (button == null ||
                !string.Equals(
                    button.name,
                    "Button_Settings",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            button.onClick.AddListener(
                OpenSettings);

            mainMenuButtonHooked =
                true;

            return;
        }
    }

    private void KeepLegacySettingsModalHidden()
    {
        GameObject canvas =
            FindSceneObject(
                "Canvas_MainMenu");

        if (canvas == null)
        {
            return;
        }

        Transform modal =
            FindRecursive(
                canvas.transform,
                "Modal");

        if (modal != null &&
            modal.gameObject.activeSelf)
        {
            modal.gameObject.SetActive(
                false);
        }
    }

    private void UpdateGameplaySettingsButtonVisibility()
    {
        if (gameplaySettingsButton == null)
        {
            ResolveReferences();
        }

        if (gameplaySettingsButton == null)
        {
            return;
        }

        bool show =
            !IsMenuFlowVisible() &&
            (settingsRoot == null ||
             !settingsRoot.activeSelf);

        if (gameplaySettingsButton
                .gameObject
                .activeSelf !=
            show)
        {
            gameplaySettingsButton
                .gameObject
                .SetActive(
                    show);
        }
    }

    private static bool IsMenuFlowVisible()
    {
        GameObject canvas =
            FindSceneObject(
                "Canvas_MainMenu");

        return canvas != null &&
               canvas.activeInHierarchy;
    }

    private static bool HasBlockingGameplayModal()
    {
        AtlasBoardEscapeBlocker[] blockers =
            UnityEngine.Object
                .FindObjectsByType<
                    AtlasBoardEscapeBlocker>(
                        FindObjectsInactive.Include);

        foreach (AtlasBoardEscapeBlocker blocker
                 in blockers)
        {
            if (blocker != null &&
                blocker.IsBlocking)
            {
                return true;
            }
        }

        return false;
    }

    private void DisableGameplayShortcutController()
    {
        if (gameplayShortcutController ==
            null)
        {
            MonoBehaviour[] behaviours =
                UnityEngine.Object
                    .FindObjectsByType<
                        MonoBehaviour>(
                            FindObjectsInactive.Include);

            foreach (MonoBehaviour behaviour
                     in behaviours)
            {
                if (behaviour != null &&
                    behaviour.GetType().Name ==
                    "UXKeyboardShortcutController")
                {
                    gameplayShortcutController =
                        behaviour;

                    break;
                }
            }
        }

        if (gameplayShortcutController != null)
        {
            gameplayShortcutWasEnabled =
                gameplayShortcutController.enabled;

            gameplayShortcutController.enabled =
                false;
        }
    }

    private void RestoreGameplayShortcutController()
    {
        if (gameplayShortcutController != null)
        {
            gameplayShortcutController.enabled =
                gameplayShortcutWasEnabled;
        }
    }

    private void CloseInternal()
    {
        if (settingsRoot != null)
        {
            settingsRoot.SetActive(
                false);
        }

        RestoreGameplayShortcutController();
    }

    private GameObject FindChildObject(
        string name)
    {
        Transform child =
            FindRecursive(
                transform,
                name);

        return child != null
            ? child.gameObject
            : null;
    }

    private Button FindButton(
        string name)
    {
        Transform child =
            FindRecursive(
                transform,
                name);

        return child != null
            ? child.GetComponent<Button>()
            : null;
    }

    private Slider FindSlider(
        string name)
    {
        Transform child =
            FindRecursive(
                transform,
                name);

        return child != null
            ? child.GetComponent<Slider>()
            : null;
    }

    private Toggle FindToggle(
        string name)
    {
        Transform child =
            FindRecursive(
                transform,
                name);

        return child != null
            ? child.GetComponent<Toggle>()
            : null;
    }

    private TMP_Dropdown FindDropdown(
        string name)
    {
        Transform child =
            FindRecursive(
                transform,
                name);

        return child != null
            ? child.GetComponent<TMP_Dropdown>()
            : null;
    }

    private TMP_Text FindText(
        string name)
    {
        Transform child =
            FindRecursive(
                transform,
                name);

        return child != null
            ? child.GetComponent<TMP_Text>()
            : null;
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
            if (string.Equals(
                    child.name,
                    targetName,
                    StringComparison.OrdinalIgnoreCase))
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

    private static void SetActive(
        GameObject target,
        bool value)
    {
        if (target != null &&
            target.activeSelf != value)
        {
            target.SetActive(
                value);
        }
    }

    private static bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.escapeKey
                   .wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(
            KeyCode.Escape);
#else
        return false;
#endif
    }
}
