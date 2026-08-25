using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-5000)]
public class AtlasBoardAudioManager : MonoBehaviour
{
    public static AtlasBoardAudioManager Instance
    {
        get;
        private set;
    }

    [SerializeField]
    private AtlasBoardAudioLibrary library;

    [Header("Audio Sources")]
    [SerializeField]
    private AudioSource mainMusicSource;

    [SerializeField]
    private AudioSource themeSource;

    [SerializeField]
    private AudioSource diceSource;

    [SerializeField]
    private AudioSource effectsSource;

    [SerializeField]
    private AudioSource uiSource;

    private AtlasBoardAudioSettingsValues currentSettings;

    private bool lastMainMenuState;
    private bool flowStateInitialized;
    private string activeThemeId =
        "classic_table";

    private float nextButtonScanTime;

    public AtlasBoardAudioLibrary Library =>
        library;

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(
            gameObject);

        EnsureSources();

        currentSettings =
            AtlasBoardAudioSettings.Load();

        ApplySettings(
            currentSettings);

        EnterMainMenu();
    }

    private void Start()
    {
        RegisterAllUIAudio();
    }

    private void Update()
    {
        UpdateFlow();

        if (Time.unscaledTime >=
            nextButtonScanTime)
        {
            nextButtonScanTime =
                Time.unscaledTime +
                2f;

            RegisterAllUIAudio();
        }
    }

    private void EnsureSources()
    {
        if (mainMusicSource == null)
        {
            mainMusicSource =
                CreateSource(
                    "MainMusicSource",
                    true);
        }

        if (themeSource == null)
        {
            themeSource =
                CreateSource(
                    "ThemeSource",
                    true);
        }

        if (diceSource == null)
        {
            diceSource =
                CreateSource(
                    "DiceSource",
                    false);
        }

        if (effectsSource == null)
        {
            effectsSource =
                CreateSource(
                    "EffectsSource",
                    false);
        }

        if (uiSource == null)
        {
            uiSource =
                CreateSource(
                    "UISource",
                    false);
        }
    }

    private AudioSource CreateSource(
        string objectName,
        bool loop)
    {
        GameObject child =
            new GameObject(
                objectName);

        child.transform.SetParent(
            transform,
            false);

        AudioSource source =
            child.AddComponent<
                AudioSource>();

        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;

        return source;
    }

    public void ApplySettings(
        AtlasBoardAudioSettingsValues values)
    {
        currentSettings =
            AtlasBoardAudioSettings.Clamp(
                values);

        float master =
            currentSettings.Master;

        if (mainMusicSource != null)
        {
            mainMusicSource.volume =
                master *
                currentSettings.MainMusic;
        }

        if (themeSource != null)
        {
            themeSource.volume =
                master *
                currentSettings.Theme;
        }

        if (diceSource != null)
        {
            diceSource.volume =
                master *
                currentSettings.Dice;
        }

        float effectsVolume =
            master *
            currentSettings.Effects;

        if (effectsSource != null)
        {
            effectsSource.volume =
                effectsVolume;
        }

        if (uiSource != null)
        {
            uiSource.volume =
                effectsVolume;
        }
    }

    public void EnterMainMenu()
    {
        PlayLoop(
            mainMusicSource,
            library != null
                ? library.mainMenuMusic
                : null);

        StopLoop(
            themeSource);
    }

    public void EnterGameplay()
    {
        RestoreGameplayUIAfterMenu();

        PlayLoop(
            mainMusicSource,
            library != null
                ? library.gameplayMusic
                : null);

        activeThemeId =
            DetectCurrentThemeId();

        PlayTheme(
            activeThemeId);
    }

    private static void RestoreGameplayUIAfterMenu()
    {
        // The Main Menu intentionally hides these canvases while the player
        // is in menu/lobby. The old restore path can remember the pre-match
        // inactive state of Canvas_BoardControls, leaving ZAR AT / TAKAS
        // invisible even though keyboard shortcuts still work.
        //
        // Restore them once when the game flow enters a match. Individual
        // gameplay controllers may still show/hide their child panels later.
        string[] gameplayCanvasNames =
        {
            "Canvas_BoardControls",
            "Canvas_UXOverlay",
            "Canvas_TabletUI"
        };

        foreach (string canvasName
                 in gameplayCanvasNames)
        {
            GameObject canvas =
                FindSceneObject(
                    canvasName);

            if (canvas != null &&
                !canvas.activeSelf)
            {
                canvas.SetActive(
                    true);
            }
        }

        Debug.Log(
            "Gameplay UI recovery after Main Menu completed.");
    }

    public void SetTheme(
        string themeId)
    {
        if (string.IsNullOrWhiteSpace(
                themeId))
        {
            themeId =
                "classic_table";
        }

        activeThemeId =
            themeId;

        bool mainMenuVisible =
            IsMainMenuCanvasVisible();

        if (!mainMenuVisible)
        {
            PlayTheme(
                activeThemeId);
        }
    }

    public void PlayDice()
    {
        PlayOneShot(
            diceSource,
            library != null
                ? library.GetRandomDice()
                : null);
    }

    public void PlayUiClick()
    {
        PlayOneShot(
            uiSource,
            library != null
                ? library.uiClick
                : null);
    }

    public void PlayUiSelect()
    {
        PlayOneShot(
            uiSource,
            library != null
                ? library.uiSelect
                : null);
    }

    public void PlayUiOpen()
    {
        PlayOneShot(
            uiSource,
            library != null
                ? library.uiOpen
                : null);
    }

    public void PlayUiToggle()
    {
        PlayOneShot(
            uiSource,
            library != null
                ? library.uiToggle
                : null);
    }

    public void PlayUiError()
    {
        PlayOneShot(
            uiSource,
            library != null
                ? library.uiError
                : null);
    }

    public void PlayPawnMove()
    {
        PlayOneShot(
            effectsSource,
            library != null
                ? library.pawnMove
                : null);
    }

    public void PlayCard()
    {
        PlayOneShot(
            effectsSource,
            library != null
                ? library.card
                : null);
    }

    public void PlayCoin()
    {
        PlayOneShot(
            effectsSource,
            library != null
                ? library.coin
                : null);
    }

    public void PlayPurchase()
    {
        PlayOneShot(
            effectsSource,
            library != null
                ? library.purchase
                : null);
    }

    public void PlayRent()
    {
        PlayOneShot(
            effectsSource,
            library != null
                ? library.rent
                : null);
    }

    public void PlayAuction()
    {
        PlayOneShot(
            effectsSource,
            library != null
                ? library.auction
                : null);
    }

    public void PlayTrade()
    {
        PlayOneShot(
            effectsSource,
            library != null
                ? library.trade
                : null);
    }

    public void PlaySuccess()
    {
        PlayOneShot(
            effectsSource,
            library != null
                ? library.success
                : null);
    }

    public void PlayWarning()
    {
        PlayOneShot(
            effectsSource,
            library != null
                ? library.warning
                : null);
    }

    private void PlayTheme(
        string themeId)
    {
        AudioClip clip =
            library != null
                ? library.GetThemeClip(
                    themeId)
                : null;

        PlayLoop(
            themeSource,
            clip);
    }

    private static void PlayOneShot(
        AudioSource source,
        AudioClip clip)
    {
        if (source == null ||
            clip == null)
        {
            return;
        }

        source.PlayOneShot(
            clip);
    }

    private static void PlayLoop(
        AudioSource source,
        AudioClip clip)
    {
        if (source == null)
        {
            return;
        }

        if (clip == null)
        {
            source.Stop();
            source.clip = null;
            return;
        }

        if (source.clip == clip &&
            source.isPlaying)
        {
            return;
        }

        source.Stop();
        source.clip = clip;
        source.loop = true;
        source.Play();
    }

    private static void StopLoop(
        AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.Stop();
        source.clip = null;
    }

    private void UpdateFlow()
    {
        bool mainMenuVisible =
            IsMainMenuCanvasVisible();

        if (!flowStateInitialized)
        {
            flowStateInitialized = true;
            lastMainMenuState =
                mainMenuVisible;

            if (mainMenuVisible)
            {
                EnterMainMenu();
            }
            else
            {
                EnterGameplay();
            }

            return;
        }

        if (mainMenuVisible ==
            lastMainMenuState)
        {
            if (!mainMenuVisible)
            {
                string detected =
                    DetectCurrentThemeId();

                if (!string.Equals(
                        detected,
                        activeThemeId,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    activeThemeId =
                        detected;

                    PlayTheme(
                        activeThemeId);
                }
            }

            return;
        }

        lastMainMenuState =
            mainMenuVisible;

        if (mainMenuVisible)
        {
            EnterMainMenu();
        }
        else
        {
            EnterGameplay();
        }
    }

    private static bool IsMainMenuCanvasVisible()
    {
        GameObject canvas =
            FindSceneObject(
                "Canvas_MainMenu");

        return canvas != null &&
               canvas.activeInHierarchy;
    }

    private static string DetectCurrentThemeId()
    {
        GameObject propsRoot =
            FindSceneObject(
                "PropsRoot");

        if (propsRoot != null)
        {
            Transform root =
                propsRoot.transform;

            if (IsChildActive(
                    root,
                    "PF_Theme_Garden_Props"))
            {
                return "garden";
            }

            if (IsChildActive(
                    root,
                    "PF_Theme_Beach_Props"))
            {
                return "beach";
            }

            if (IsChildActive(
                    root,
                    "PF_Theme_Pavilion_Props"))
            {
                return "pavilion";
            }

            if (IsChildActive(
                    root,
                    "PF_Theme_Street_Props"))
            {
                return "street";
            }
        }

        return "classic_table";
    }

    private static bool IsChildActive(
        Transform root,
        string name)
    {
        Transform child =
            root.Find(name);

        return child != null &&
               child.gameObject.activeInHierarchy;
    }

    private void RegisterAllUIAudio()
    {
        Button[] buttons =
            Object.FindObjectsByType<
                Button>(
                    FindObjectsInactive.Include);

        foreach (Button button
                 in buttons)
        {
            if (button == null ||
                !button.gameObject.scene.IsValid())
            {
                continue;
            }

            if (button.GetComponent<
                    AtlasBoardUIButtonAudio>() ==
                null)
            {
                button.gameObject.AddComponent<
                    AtlasBoardUIButtonAudio>();
            }
        }

        Toggle[] toggles =
            Object.FindObjectsByType<
                Toggle>(
                    FindObjectsInactive.Include);

        foreach (Toggle toggle
                 in toggles)
        {
            if (toggle == null ||
                !toggle.gameObject.scene.IsValid())
            {
                continue;
            }

            if (toggle.GetComponent<
                    AtlasBoardToggleAudio>() ==
                null)
            {
                toggle.gameObject.AddComponent<
                    AtlasBoardToggleAudio>();
            }

            AtlasBoardToggleVisualFix visualFix =
                toggle.GetComponent<
                    AtlasBoardToggleVisualFix>();

            if (visualFix == null)
            {
                visualFix =
                    toggle.gameObject.AddComponent<
                        AtlasBoardToggleVisualFix>();
            }

            visualFix.ApplyStyle();
        }

        TMPro.TMP_Dropdown[] dropdowns =
            Object.FindObjectsByType<
                TMPro.TMP_Dropdown>(
                    FindObjectsInactive.Include);

        foreach (TMPro.TMP_Dropdown dropdown
                 in dropdowns)
        {
            if (dropdown == null ||
                !dropdown.gameObject.scene.IsValid())
            {
                continue;
            }

            if (dropdown.GetComponent<
                    AtlasBoardDropdownAudio>() ==
                null)
            {
                dropdown.gameObject.AddComponent<
                    AtlasBoardDropdownAudio>();
            }
        }
    }

    private static GameObject FindSceneObject(
        string objectName)
    {
        GameObject[] all =
            Resources.FindObjectsOfTypeAll<
                GameObject>();

        foreach (GameObject item in all)
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

#if UNITY_EDITOR
    public void EditorConfigure(
        AtlasBoardAudioLibrary newLibrary)
    {
        library = newLibrary;
        EnsureSources();
    }
#endif
}
