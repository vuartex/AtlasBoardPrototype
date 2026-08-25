using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-20000)]
public class AtlasBoardSettingsOverlayController : MonoBehaviour
{
    public static AtlasBoardSettingsOverlayController Instance
    {
        get;
        private set;
    }

    [Header("Root")]
    [SerializeField]
    private GameObject settingsRoot;

    [SerializeField]
    private GameObject audioPanel;

    [SerializeField]
    private GameObject placeholderPanel;

    [SerializeField]
    private TMP_Text placeholderTitle;

    [Header("Tabs")]
    [SerializeField]
    private Button audioTabButton;

    [SerializeField]
    private Button gameplayTabButton;

    [SerializeField]
    private Button graphicsTabButton;

    [SerializeField]
    private Button controlsTabButton;

    [Header("Audio Sliders")]
    [SerializeField]
    private Slider masterSlider;

    [SerializeField]
    private Slider mainMusicSlider;

    [SerializeField]
    private Slider themeSlider;

    [SerializeField]
    private Slider diceSlider;

    [SerializeField]
    private Slider effectsSlider;

    [Header("Value Labels")]
    [SerializeField]
    private TMP_Text masterValueText;

    [SerializeField]
    private TMP_Text mainMusicValueText;

    [SerializeField]
    private TMP_Text themeValueText;

    [SerializeField]
    private TMP_Text diceValueText;

    [SerializeField]
    private TMP_Text effectsValueText;

    [Header("Actions")]
    [SerializeField]
    private Button applyButton;

    [SerializeField]
    private Button cancelButton;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private Button gameplaySettingsButton;

    private bool hooked;
    private bool mainMenuButtonHooked;

    private MonoBehaviour gameplayShortcutController;
    private bool gameplayShortcutWasEnabled;

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        HookControls();

        if (settingsRoot != null)
        {
            settingsRoot.SetActive(
                false);
        }
    }

    private void Start()
    {
        TryHookMainMenuSettingsButton();
        UpdateGameplayButtonVisibility();
    }

    private void Update()
    {
        if (!mainMenuButtonHooked)
        {
            TryHookMainMenuSettingsButton();
        }

        UpdateGameplayButtonVisibility();

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

        // ESC precedence:
        // Existing tablet/trade/auction/etc. gets first use of Escape.
        // This controller runs early and checks the state BEFORE those
        // systems close themselves later in the same frame.
        if (HasBlockingGameplayModal())
        {
            return;
        }

        OpenSettings();
    }

    public void OpenSettings()
    {
        HookControls();

        AtlasBoardAudioManager.Instance
            ?.PlayUiOpen();

        AtlasBoardAudioSettingsValues values =
            AtlasBoardAudioSettings.Load();

        SetSliders(
            values);

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
        AtlasBoardAudioSettingsValues values =
            ReadSliders();

        AtlasBoardAudioSettings.Save(
            values);

        AtlasBoardAudioManager.Instance
            ?.ApplySettings(
                values);

        CloseInternal();
    }

    public void CancelAndClose()
    {
        AtlasBoardAudioSettingsValues saved =
            AtlasBoardAudioSettings.Load();

        AtlasBoardAudioManager.Instance
            ?.ApplySettings(
                saved);

        CloseInternal();
    }

    public void ShowAudioTab()
    {
        SetActive(
            audioPanel,
            true);

        SetActive(
            placeholderPanel,
            false);
    }

    public void ShowGameplayTab()
    {
        ShowPlaceholder(
            "GAMEPLAY SETTINGS");
    }

    public void ShowGraphicsTab()
    {
        ShowPlaceholder(
            "GRAPHICS SETTINGS");
    }

    public void ShowControlsTab()
    {
        ShowPlaceholder(
            "CONTROLS");
    }

    private void ShowPlaceholder(
        string title)
    {
        SetActive(
            audioPanel,
            false);

        SetActive(
            placeholderPanel,
            true);

        if (placeholderTitle != null)
        {
            placeholderTitle.text =
                title;
        }
    }

    private void HookControls()
    {
        if (hooked)
        {
            return;
        }

        hooked = true;

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
            applyButton,
            ApplyAndClose);

        AddClick(
            cancelButton,
            CancelAndClose);

        AddClick(
            closeButton,
            CancelAndClose);

        AddClick(
            gameplaySettingsButton,
            OpenSettings);

        AddSliderListener(
            masterSlider);

        AddSliderListener(
            mainMusicSlider);

        AddSliderListener(
            themeSlider);

        AddSliderListener(
            diceSlider);

        AddSliderListener(
            effectsSlider);
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

    private void AddSliderListener(
        Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.AddListener(
            _ =>
            {
                AtlasBoardAudioSettingsValues preview =
                    ReadSliders();

                AtlasBoardAudioManager.Instance
                    ?.ApplySettings(
                        preview);

                RefreshValueLabels();
            });
    }

    private void SetSliders(
        AtlasBoardAudioSettingsValues values)
    {
        SetSlider(
            masterSlider,
            values.Master);

        SetSlider(
            mainMusicSlider,
            values.MainMusic);

        SetSlider(
            themeSlider,
            values.Theme);

        SetSlider(
            diceSlider,
            values.Dice);

        SetSlider(
            effectsSlider,
            values.Effects);

        RefreshValueLabels();

        AtlasBoardAudioManager.Instance
            ?.ApplySettings(
                values);
    }

    private static void SetSlider(
        Slider slider,
        float value)
    {
        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(
            Mathf.Clamp01(
                value));
    }

    private AtlasBoardAudioSettingsValues ReadSliders()
    {
        AtlasBoardAudioSettingsValues defaults =
            AtlasBoardAudioSettingsValues.Default;

        return new AtlasBoardAudioSettingsValues
        {
            Master =
                GetSliderValue(
                    masterSlider,
                    defaults.Master),

            MainMusic =
                GetSliderValue(
                    mainMusicSlider,
                    defaults.MainMusic),

            Theme =
                GetSliderValue(
                    themeSlider,
                    defaults.Theme),

            Dice =
                GetSliderValue(
                    diceSlider,
                    defaults.Dice),

            Effects =
                GetSliderValue(
                    effectsSlider,
                    defaults.Effects)
        };
    }

    private static float GetSliderValue(
        Slider slider,
        float fallback)
    {
        return slider != null
            ? slider.value
            : fallback;
    }

    private void RefreshValueLabels()
    {
        SetPercent(
            masterValueText,
            masterSlider);

        SetPercent(
            mainMusicValueText,
            mainMusicSlider);

        SetPercent(
            themeValueText,
            themeSlider);

        SetPercent(
            diceValueText,
            diceSlider);

        SetPercent(
            effectsValueText,
            effectsSlider);
    }

    private static void SetPercent(
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

    private void CloseInternal()
    {
        if (settingsRoot != null)
        {
            settingsRoot.SetActive(
                false);
        }

        RestoreGameplayShortcutController();
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

        foreach (Button button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            if (!string.Equals(
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
            FindDescendantByName(
                canvas.transform,
                "Modal");

        if (modal != null &&
            modal.gameObject.activeSelf)
        {
            modal.gameObject.SetActive(
                false);
        }
    }

    private void UpdateGameplayButtonVisibility()
    {
        if (gameplaySettingsButton == null)
        {
            return;
        }

        bool shouldShow =
            !IsMenuFlowVisible() &&
            (settingsRoot == null ||
             !settingsRoot.activeSelf);

        if (gameplaySettingsButton
                .gameObject
                .activeSelf !=
            shouldShow)
        {
            gameplaySettingsButton
                .gameObject
                .SetActive(
                    shouldShow);
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
            UnityEngine.Object.FindObjectsByType<
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

        RectTransform[] rects =
            UnityEngine.Object.FindObjectsByType<
                RectTransform>(
                    FindObjectsInactive.Exclude);

        string[] keywords =
        {
            "tablet",
            "trade",
            "takas",
            "auction",
            "ihale",
            "purchase",
            "buy",
            "event",
            "card",
            "travel",
            "penalty",
            "ceza",
            "develop",
            "matchresult",
            "result"
        };

        foreach (RectTransform rect
                 in rects)
        {
            if (rect == null ||
                rect.gameObject == null ||
                !rect.gameObject.activeInHierarchy)
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
                    "hud"))
            {
                continue;
            }

            bool keywordMatch =
                false;

            foreach (string keyword
                     in keywords)
            {
                if (lower.Contains(
                        keyword))
                {
                    keywordMatch = true;
                    break;
                }
            }

            if (!keywordMatch)
            {
                continue;
            }

            if (rect.rect.width >= 220f &&
                rect.rect.height >= 120f)
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
                UnityEngine.Object.FindObjectsByType<
                    MonoBehaviour>(
                        FindObjectsInactive.Include);

            foreach (MonoBehaviour behaviour
                     in behaviours)
            {
                if (behaviour == null)
                {
                    continue;
                }

                if (behaviour.GetType().Name ==
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

    private static Transform FindDescendantByName(
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
                FindDescendantByName(
                    child,
                    targetName);

            if (nested != null)
            {
                return nested;
            }
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

#if UNITY_EDITOR
    public void EditorConfigure(
        GameObject newSettingsRoot,
        GameObject newAudioPanel,
        GameObject newPlaceholderPanel,
        TMP_Text newPlaceholderTitle,
        Button newAudioTabButton,
        Button newGameplayTabButton,
        Button newGraphicsTabButton,
        Button newControlsTabButton,
        Slider newMasterSlider,
        Slider newMainMusicSlider,
        Slider newThemeSlider,
        Slider newDiceSlider,
        Slider newEffectsSlider,
        TMP_Text newMasterValueText,
        TMP_Text newMainMusicValueText,
        TMP_Text newThemeValueText,
        TMP_Text newDiceValueText,
        TMP_Text newEffectsValueText,
        Button newApplyButton,
        Button newCancelButton,
        Button newCloseButton,
        Button newGameplaySettingsButton)
    {
        settingsRoot =
            newSettingsRoot;

        audioPanel =
            newAudioPanel;

        placeholderPanel =
            newPlaceholderPanel;

        placeholderTitle =
            newPlaceholderTitle;

        audioTabButton =
            newAudioTabButton;

        gameplayTabButton =
            newGameplayTabButton;

        graphicsTabButton =
            newGraphicsTabButton;

        controlsTabButton =
            newControlsTabButton;

        masterSlider =
            newMasterSlider;

        mainMusicSlider =
            newMainMusicSlider;

        themeSlider =
            newThemeSlider;

        diceSlider =
            newDiceSlider;

        effectsSlider =
            newEffectsSlider;

        masterValueText =
            newMasterValueText;

        mainMusicValueText =
            newMainMusicValueText;

        themeValueText =
            newThemeValueText;

        diceValueText =
            newDiceValueText;

        effectsValueText =
            newEffectsValueText;

        applyButton =
            newApplyButton;

        cancelButton =
            newCancelButton;

        closeButton =
            newCloseButton;

        gameplaySettingsButton =
            newGameplaySettingsButton;
    }
#endif
}
