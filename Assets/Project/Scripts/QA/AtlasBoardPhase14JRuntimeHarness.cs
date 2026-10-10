#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(20000)]
[DisallowMultipleComponent]
public sealed class AtlasBoardPhase14JRuntimeHarness :
    MonoBehaviour
{
    private sealed class ResolutionProfile
    {
        public string Name;
        public int Width;
        public int Height;

        public ResolutionProfile(
            string name,
            int width,
            int height)
        {
            Name = name;
            Width = width;
            Height = height;
        }
    }

    private sealed class SurfaceStep
    {
        public string Name;
        public Func<bool> Open;
        public Action Close;
    }

    private sealed class ValidationResult
    {
        public int Errors;
        public int Warnings;
        public int Texts;
    }

    private static readonly string[] Languages =
    {
        "en",
        "tr",
        "es",
        "fr",
        "de",
        "ko",
        "ru"
    };

    // These deliberately fit inside a 1920x1080 desktop in Windowed mode.
    private static readonly ResolutionProfile[] Profiles =
    {
        new ResolutionProfile(
            "NARROW",
            1280,
            1024),

        new ResolutionProfile(
            "STANDARD",
            1600,
            900),

        new ResolutionProfile(
            "WIDE",
            1920,
            800)
    };

    private static AtlasBoardPhase14JRuntimeHarness instance;

    private readonly Dictionary<string, string> matrix =
        new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

    private readonly List<string> reportLines =
        new List<string>();

    private Rect panelRect =
        new Rect(
            12f,
            80f,
            360f,
            525f);

    private bool panelVisible = false;
    private Vector2 scroll;

    private int currentProfileIndex = 1;
    private int currentLanguageIndex;

    private bool fullRunActive;
    private bool currentSweepActive;
    private int runProfileIndex;
    private int runSurfaceIndex;
    private int runLanguageIndex;
    private int waitFrames;
    private int requestedWidth;
    private int requestedHeight;
    private int surfaceErrors;
    private int surfaceWarnings;
    private int surfaceTexts;
    private bool surfaceSkipped;

    private string originalLanguage = "en";
    private float originalTimeScale = 1f;

    private List<SurfaceStep> surfaces =
        new List<SurfaceStep>();

    private SurfaceStep activeSurface;

    private string status =
        "Ready. F8 toggles this panel.";

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        AtlasBoardPhase14JRuntimeHarness existing =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardPhase14JRuntimeHarness>();

        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject root =
            new GameObject(
                "AtlasBoard_Phase14J_RuntimeHarness");

        DontDestroyOnLoad(
            root);

        instance =
            root.AddComponent<
                AtlasBoardPhase14JRuntimeHarness>();
    }

    public static void ShowPanel()
    {
        if (instance != null)
        {
            instance.panelVisible = true;
        }
    }

    private void Awake()
    {
        if (instance != null &&
            instance != this)
        {
            Destroy(
                gameObject);
            return;
        }

        instance = this;

        BuildSurfaceList();
        RefreshCurrentProfileFromScreen();

        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;

        if (localization != null)
        {
            originalLanguage =
                localization.CurrentLanguageCode;

            currentLanguageIndex =
                Array.IndexOf(
                    Languages,
                    originalLanguage);

            if (currentLanguageIndex < 0)
            {
                currentLanguageIndex = 0;
            }
        }

        Log(
            "Phase 14J runtime harness ready. " +
            "Use F12 for full responsive + 7-language regression.");
    }

    private void Update()
    {
        HandleHotkeys();

        if (fullRunActive)
        {
            TickFullRun();
        }
        else if (currentSweepActive)
        {
            TickCurrentSweep();
        }
    }

    private void OnGUI()
    {
        if (!panelVisible)
        {
            return;
        }

        panelRect =
            GUI.Window(
                140014,
                panelRect,
                DrawWindow,
                "PHASE 14J • RUNTIME REGRESSION");
    }

    private void DrawWindow(
        int windowId)
    {
        GUILayout.Label(
            $"Actual: {Screen.width}x{Screen.height}  " +
            $"({GetAspectProfile(Screen.width, Screen.height)})");

        GUILayout.Label(
            $"Language: {GetCurrentLanguage().ToUpperInvariant()}");

        GUILayout.Label(
            $"Status: {status}");

        GUILayout.Space(
            6f);

        GUI.enabled =
            !fullRunActive &&
            !currentSweepActive;

        if (GUILayout.Button(
                "RUN FULL 14J  •  F12",
                GUILayout.Height(
                    34f)))
        {
            BeginFullRun();
        }

        if (GUILayout.Button(
                "7-LANGUAGE CURRENT VIEW  •  F11",
                GUILayout.Height(
                    30f)))
        {
            BeginCurrentViewSweep();
        }

        GUILayout.Space(
            5f);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "NARROW\n1280×1024",
                GUILayout.Height(
                    48f)))
        {
            ApplyResolutionProfile(
                0);
        }

        if (GUILayout.Button(
                "STANDARD\n1600×900",
                GUILayout.Height(
                    48f)))
        {
            ApplyResolutionProfile(
                1);
        }

        if (GUILayout.Button(
                "WIDE\n1920×800",
                GUILayout.Height(
                    48f)))
        {
            ApplyResolutionProfile(
                2);
        }

        GUILayout.EndHorizontal();

        if (GUILayout.Button(
                "NEXT LANGUAGE  •  F10",
                GUILayout.Height(
                    27f)))
        {
            CycleLanguage();
        }

        GUI.enabled =
            true;

        GUILayout.Space(
            6f);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "COPY REPORT"))
        {
            GUIUtility.systemCopyBuffer =
                BuildReport();

            status =
                "Report copied to clipboard.";
        }

        if (GUILayout.Button(
                "CLEAR REPORT"))
        {
            matrix.Clear();
            reportLines.Clear();
            status =
                "Report cleared.";
        }

        GUILayout.EndHorizontal();

        GUILayout.Space(
            5f);

        scroll =
            GUILayout.BeginScrollView(
                scroll,
                GUILayout.Height(
                    215f));

        string report =
            BuildCompactReport();

        GUILayout.TextArea(
            report,
            GUILayout.ExpandHeight(
                true));

        GUILayout.EndScrollView();

        GUILayout.Label(
            "Hotkeys: F8 panel • F9 resolution • F10 language • " +
            "F11 current-view sweep • F12 full run");

        GUI.DragWindow(
            new Rect(
                0f,
                0f,
                10000f,
                24f));
    }

    private void HandleHotkeys()
    {
        Keyboard keyboard =
            Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.f8Key.wasPressedThisFrame)
        {
            panelVisible =
                !panelVisible;
        }

        if (fullRunActive ||
            currentSweepActive)
        {
            return;
        }

        if (keyboard.f9Key.wasPressedThisFrame)
        {
            ApplyResolutionProfile(
                (currentProfileIndex + 1) %
                Profiles.Length);
        }

        if (keyboard.f10Key.wasPressedThisFrame)
        {
            CycleLanguage();
        }

        if (keyboard.f11Key.wasPressedThisFrame)
        {
            BeginCurrentViewSweep();
        }

        if (keyboard.f12Key.wasPressedThisFrame)
        {
            BeginFullRun();
        }
    }

    private void BuildSurfaceList()
    {
        surfaces =
            new List<SurfaceStep>
            {
                new SurfaceStep
                {
                    Name = "Gameplay",
                    Open = () =>
                    {
                        CloseAllPreviewSurfaces();
                        return
                            UnityEngine.Object.FindAnyObjectByType<
                                BoardPath>() != null;
                    },
                    Close = null
                },

                new SurfaceStep
                {
                    Name = "CityInfo",
                    Open = OpenCityInformation,
                    Close = CloseCityInformation
                },

                new SurfaceStep
                {
                    Name = "MatchResult",
                    Open = OpenMatchResult,
                    Close = CloseMatchResult
                },

                new SurfaceStep
                {
                    Name = "Auction",
                    Open = OpenAuction,
                    Close = CloseAuction
                },

                new SurfaceStep
                {
                    Name = "Trade",
                    Open = OpenTrade,
                    Close = CloseTrade
                },

                new SurfaceStep
                {
                    Name = "Development",
                    Open = OpenDevelopment,
                    Close = CloseDevelopment
                }
            };
    }

    private void BeginFullRun()
    {
        if (fullRunActive ||
            currentSweepActive)
        {
            return;
        }

        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;

        if (localization == null)
        {
            status =
                "FAILED: Localization manager missing.";

            LogError(
                status);
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType<
                BoardPath>() == null)
        {
            status =
                "FAILED: start a gameplay match first.";

            LogError(
                "Phase 14J full run requires gameplay to be loaded.");
            return;
        }

        originalLanguage =
            localization.CurrentLanguageCode;

        originalTimeScale =
            Time.timeScale > 0f
                ? Time.timeScale
                : 1f;

        Time.timeScale =
            0f;

        matrix.Clear();
        reportLines.Clear();

        runProfileIndex =
            0;

        runSurfaceIndex =
            -1;

        runLanguageIndex =
            0;

        activeSurface =
            null;

        fullRunActive =
            true;

        status =
            "Full 14J run started.";

        Log(
            "Phase 14J FULL RUN START. " +
            "Gameplay is frozen. The harness will cycle 3 window profiles, " +
            "6 gameplay UI surfaces and 7 languages.");

        RequestRunResolution();
    }

    private void TickFullRun()
    {
        if (waitFrames > 0)
        {
            waitFrames--;
            return;
        }

        if (!ResolutionSettled())
        {
            waitFrames =
                3;

            return;
        }

        if (activeSurface == null)
        {
            AdvanceSurface();
            return;
        }

        if (surfaceSkipped)
        {
            CompleteSurface();
            return;
        }

        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;

        if (localization == null)
        {
            AbortRun(
                "Localization manager disappeared.");
            return;
        }

        Canvas.ForceUpdateCanvases();

        string language =
            Languages[runLanguageIndex];

        ValidationResult result =
            ValidateActiveView(
                activeSurface.Name,
                language);

        surfaceErrors +=
            result.Errors;

        surfaceWarnings +=
            result.Warnings;

        surfaceTexts +=
            result.Texts;

        runLanguageIndex++;

        if (runLanguageIndex >=
            Languages.Length)
        {
            CompleteSurface();
            return;
        }

        localization.SetLanguage(
            Languages[runLanguageIndex]);

        waitFrames =
            2;
    }

    private void AdvanceSurface()
    {
        if (activeSurface != null &&
            activeSurface.Close != null)
        {
            SafeClose(
                activeSurface);
        }

        runSurfaceIndex++;

        if (runSurfaceIndex >=
            surfaces.Count)
        {
            AdvanceProfile();
            return;
        }

        activeSurface =
            surfaces[runSurfaceIndex];

        surfaceErrors =
            0;

        surfaceWarnings =
            0;

        surfaceTexts =
            0;

        surfaceSkipped =
            false;

        runLanguageIndex =
            0;

        CloseAllPreviewSurfaces();

        bool opened =
            false;

        try
        {
            opened =
                activeSurface.Open == null ||
                activeSurface.Open();
        }
        catch (Exception exception)
        {
            LogWarning(
                $"Phase 14J {activeSurface.Name} preview threw: " +
                exception.Message);

            opened =
                false;
        }

        if (!opened)
        {
            surfaceSkipped =
                true;

            status =
                $"{Profiles[runProfileIndex].Name} / {activeSurface.Name}: SKIPPED";

            waitFrames =
                1;

            return;
        }

        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;

        localization.SetLanguage(
            Languages[0]);

        status =
            $"{Profiles[runProfileIndex].Name} / {activeSurface.Name} / EN";

        waitFrames =
            3;
    }

    private void CompleteSurface()
    {
        ResolutionProfile profile =
            Profiles[runProfileIndex];

        string value =
            surfaceSkipped
                ? "SKIPPED"
                : surfaceErrors > 0
                    ? $"FAIL E{surfaceErrors} W{surfaceWarnings} T{surfaceTexts}"
                    : surfaceWarnings > 0
                        ? $"PASS-WARN W{surfaceWarnings} T{surfaceTexts}"
                        : $"PASS T{surfaceTexts}";

        string key =
            MatrixKey(
                profile.Name,
                activeSurface.Name);

        matrix[key] =
            value;

        Log(
            $"Phase 14J [{profile.Name}] {activeSurface.Name}: {value}");

        SafeClose(
            activeSurface);

        activeSurface =
            null;

        waitFrames =
            2;
    }

    private void AdvanceProfile()
    {
        runProfileIndex++;

        runSurfaceIndex =
            -1;

        activeSurface =
            null;

        if (runProfileIndex >=
            Profiles.Length)
        {
            FinishFullRun();
            return;
        }

        RequestRunResolution();
    }

    private void RequestRunResolution()
    {
        ResolutionProfile profile =
            Profiles[runProfileIndex];

        currentProfileIndex =
            runProfileIndex;

        requestedWidth =
            profile.Width;

        requestedHeight =
            profile.Height;

        Screen.SetResolution(
            requestedWidth,
            requestedHeight,
            FullScreenMode.Windowed,
            new RefreshRate
            {
                numerator = 60,
                denominator = 1
            });

        status =
            $"Requesting {profile.Name}: " +
            $"{requestedWidth}x{requestedHeight}";

        waitFrames =
            12;
    }

    private bool ResolutionSettled()
    {
#if UNITY_EDITOR
        // The Unity Editor may ignore Screen.SetResolution for Game View.
        // We do not hang forever in that case. Full responsive runs are intended
        // for a Development Build; Editor remains useful for current-view sweeps.
        if (Mathf.Abs(
                Screen.width -
                requestedWidth) <= 8 &&
            Mathf.Abs(
                Screen.height -
                requestedHeight) <= 8)
        {
            return true;
        }

        LogWarning(
            "Phase 14J: Unity Editor did not adopt the requested runtime resolution. " +
            "Run F12 in a Development Build for automatic responsive testing.");

        AbortRun(
            "Automatic resolution switching is not supported by this Game View.");
        return false;
#else
        bool settled =
            Mathf.Abs(
                Screen.width -
                requestedWidth) <= 16 &&
            Mathf.Abs(
                Screen.height -
                requestedHeight) <= 16;

        if (!settled)
        {
            Screen.SetResolution(
                requestedWidth,
                requestedHeight,
                FullScreenMode.Windowed,
                new RefreshRate
                {
                    numerator = 60,
                    denominator = 1
                });
        }

        return settled;
#endif
    }

    private void FinishFullRun()
    {
        CloseAllPreviewSurfaces();
        RestoreOriginalState();

        fullRunActive =
            false;

        status =
            "FULL 14J COMPLETE. Copy report.";

        string report =
            BuildReport();

        Log(
            report);
    }

    private void BeginCurrentViewSweep()
    {
        if (fullRunActive ||
            currentSweepActive)
        {
            return;
        }

        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;

        if (localization == null)
        {
            status =
                "FAILED: Localization manager missing.";
            return;
        }

        originalLanguage =
            localization.CurrentLanguageCode;

        runLanguageIndex =
            0;

        surfaceErrors =
            0;

        surfaceWarnings =
            0;

        surfaceTexts =
            0;

        activeSurface =
            new SurfaceStep
            {
                Name =
                    DetectSurfaceName()
            };

        currentSweepActive =
            true;

        localization.SetLanguage(
            Languages[0]);

        status =
            $"Current view sweep: {activeSurface.Name} / EN";

        waitFrames =
            2;
    }

    private void TickCurrentSweep()
    {
        if (waitFrames > 0)
        {
            waitFrames--;
            return;
        }

        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;

        if (localization == null)
        {
            currentSweepActive =
                false;
            return;
        }

        Canvas.ForceUpdateCanvases();

        string language =
            Languages[runLanguageIndex];

        ValidationResult result =
            ValidateActiveView(
                activeSurface.Name,
                language);

        surfaceErrors +=
            result.Errors;

        surfaceWarnings +=
            result.Warnings;

        surfaceTexts +=
            result.Texts;

        runLanguageIndex++;

        if (runLanguageIndex >=
            Languages.Length)
        {
            localization.SetLanguage(
                originalLanguage);

            string profile =
                GetAspectProfile(
                    Screen.width,
                    Screen.height);

            string value =
                surfaceErrors > 0
                    ? $"FAIL E{surfaceErrors} W{surfaceWarnings} T{surfaceTexts}"
                    : surfaceWarnings > 0
                        ? $"PASS-WARN W{surfaceWarnings} T{surfaceTexts}"
                        : $"PASS T{surfaceTexts}";

            matrix[
                MatrixKey(
                    profile,
                    activeSurface.Name)] =
                value;

            status =
                $"Current view complete: {value}";

            Log(
                $"Phase 14J [{profile}] {activeSurface.Name}: {value}");

            currentSweepActive =
                false;

            return;
        }

        localization.SetLanguage(
            Languages[runLanguageIndex]);

        status =
            $"Current view sweep: {activeSurface.Name} / " +
            Languages[runLanguageIndex].ToUpperInvariant();

        waitFrames =
            2;
    }

    private void ApplyResolutionProfile(
        int profileIndex)
    {
        profileIndex =
            Mathf.Clamp(
                profileIndex,
                0,
                Profiles.Length - 1);

        currentProfileIndex =
            profileIndex;

        ResolutionProfile profile =
            Profiles[profileIndex];

        Screen.SetResolution(
            profile.Width,
            profile.Height,
            FullScreenMode.Windowed,
            new RefreshRate
            {
                numerator = 60,
                denominator = 1
            });

        status =
            $"Requested {profile.Name}: " +
            $"{profile.Width}x{profile.Height}";
    }

    private void RefreshCurrentProfileFromScreen()
    {
        string profile =
            GetAspectProfile(
                Screen.width,
                Screen.height);

        currentProfileIndex =
            profile == "NARROW"
                ? 0
                : profile == "WIDE"
                    ? 2
                    : 1;
    }

    private void CycleLanguage()
    {
        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;

        if (localization == null)
        {
            status =
                "Localization manager missing.";
            return;
        }

        currentLanguageIndex =
            (currentLanguageIndex + 1) %
            Languages.Length;

        localization.SetLanguage(
            Languages[currentLanguageIndex]);

        status =
            $"Language: {Languages[currentLanguageIndex].ToUpperInvariant()}";
    }

    private string GetCurrentLanguage()
    {
        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;

        return localization != null
            ? localization.CurrentLanguageCode
            : "n/a";
    }

    private ValidationResult ValidateActiveView(
        string surface,
        string language)
    {
        ValidationResult result =
            new ValidationResult();

        TMP_Text[] texts =
            UnityEngine.Object.FindObjectsByType<
                TMP_Text>(
                    FindObjectsInactive.Include);

        foreach (TMP_Text text
                 in texts)
        {
            if (!ShouldInspect(
                    text))
            {
                continue;
            }

            result.Texts++;

            if (text.font == null)
            {
                result.Errors++;

                AddIssue(
                    true,
                    surface,
                    language,
                    "Missing font",
                    text);

                continue;
            }

            RectTransform rect =
                text.rectTransform;

            Rect local =
                rect.rect;

            if (local.width <= 1f ||
                local.height <= 1f)
            {
                result.Errors++;

                AddIssue(
                    true,
                    surface,
                    language,
                    $"Tiny text rect {local.width:0.#}x{local.height:0.#}",
                    text);

                continue;
            }

            float preferredWidth =
                text.GetPreferredValues(
                    text.text,
                    0f,
                    local.height)
                    .x;

            Vector2 preferredWrapped =
                text.GetPreferredValues(
                    text.text,
                    local.width,
                    0f);

            bool atMinimum =
                text.enableAutoSizing &&
                text.fontSize <=
                    text.fontSizeMin +
                    0.25f;

            if (text.textWrappingMode ==
                    TextWrappingModes.NoWrap &&
                preferredWidth >
                    local.width *
                    1.18f &&
                atMinimum)
            {
                result.Warnings++;

                AddIssue(
                    false,
                    surface,
                    language,
                    $"Possible horizontal truncation " +
                    $"preferred={preferredWidth:0.#} rect={local.width:0.#}",
                    text);
            }

            if (text.textWrappingMode !=
                    TextWrappingModes.NoWrap &&
                text.overflowMode !=
                    TextOverflowModes.Overflow &&
                preferredWrapped.y >
                    local.height *
                    1.18f &&
                atMinimum)
            {
                result.Warnings++;

                AddIssue(
                    false,
                    surface,
                    language,
                    $"Possible vertical truncation " +
                    $"preferred={preferredWrapped.y:0.#} rect={local.height:0.#}",
                    text);
            }

            Rect screen =
                GetScreenRect(
                    rect);

            if (screen.width <= 1f ||
                screen.height <= 1f)
            {
                result.Errors++;

                AddIssue(
                    true,
                    surface,
                    language,
                    "Tiny rendered screen bounds",
                    text);
            }
        }

        ValidateTabletGeometry(
            surface,
            language,
            result);

        ValidateCityInfo(
            surface,
            language,
            result);

        if (result.Texts == 0)
        {
            result.Errors++;

            AddIssue(
                true,
                surface,
                language,
                "No active visible TMP text found",
                null);
        }

        status =
            $"{GetAspectProfile(Screen.width, Screen.height)} / " +
            $"{surface} / {language.ToUpperInvariant()} " +
            $"E{result.Errors} W{result.Warnings}";

        return result;
    }

    private static bool ShouldInspect(
        TMP_Text text)
    {
        if (text == null ||
            !text.gameObject.activeInHierarchy ||
            string.IsNullOrWhiteSpace(
                text.text))
        {
            return false;
        }

        Canvas canvas =
            text.GetComponentInParent<
                Canvas>();

        if (canvas == null ||
            !canvas.isActiveAndEnabled ||
            canvas.renderMode ==
                RenderMode.WorldSpace)
        {
            return false;
        }

        CanvasRenderer renderer =
            text.canvasRenderer;

        if (renderer != null &&
            renderer.GetAlpha() <=
                0.01f)
        {
            return false;
        }

        return true;
    }

    private void ValidateTabletGeometry(
        string surface,
        string language,
        ValidationResult result)
    {
        TabletUIManager manager =
            UnityEngine.Object.FindAnyObjectByType<
                TabletUIManager>();

        if (manager == null)
        {
            return;
        }

        GameObject tabletObject =
            ReadPrivateField<GameObject>(
                manager,
                "tabletRoot");

        if (tabletObject == null ||
            !tabletObject.activeInHierarchy)
        {
            return;
        }

        RectTransform tablet =
            tabletObject.transform as
                RectTransform;

        Rect tabletScreen =
            GetScreenRect(
                tablet);

        if (tabletScreen.width <
                Screen.width *
                0.35f ||
            tabletScreen.height <
                Screen.height *
                0.35f)
        {
            result.Errors++;

            AddIssue(
                true,
                surface,
                language,
                $"TabletRoot unexpectedly small " +
                $"{tabletScreen.width:0.#}x{tabletScreen.height:0.#}",
                tablet);
        }

        string[] fullTabletRoots =
        {
            "Phase14F_Final_PlayerCards",
            "Phase14G_AuctionPolish",
            "Phase14H_TradePolish_Final",
            "Phase14H_TradePolish",
            "Phase14I_DevelopmentPolish"
        };

        foreach (string name
                 in fullTabletRoots)
        {
            Transform transform =
                FindActiveRectByName(
                    name);

            if (transform == null)
            {
                continue;
            }

            if (!transform.IsChildOf(
                    tablet))
            {
                result.Errors++;

                AddIssue(
                    true,
                    surface,
                    language,
                    $"{name} is not under TabletUIManager.tabletRoot",
                    transform);

                continue;
            }

            RectTransform rect =
                transform as
                    RectTransform;

            Rect screen =
                GetScreenRect(
                    rect);

            if (screen.width <
                    tabletScreen.width *
                    0.88f ||
                screen.height <
                    tabletScreen.height *
                    0.88f)
            {
                result.Warnings++;

                AddIssue(
                    false,
                    surface,
                    language,
                    $"{name} spans too little of TabletRoot " +
                    $"{screen.width:0.#}x{screen.height:0.#}",
                    transform);
            }
        }
    }

    private void ValidateCityInfo(
        string surface,
        string language,
        ValidationResult result)
    {
        Transform panel =
            FindActiveRectByName(
                "CityInformationPanel");

        if (panel == null)
        {
            return;
        }

        Rect screen =
            GetScreenRect(
                panel as RectTransform);

        if (screen.xMin < -2f ||
            screen.yMin < -2f ||
            screen.xMax >
                Screen.width + 2f ||
            screen.yMax >
                Screen.height + 2f)
        {
            result.Errors++;

            AddIssue(
                true,
                surface,
                language,
                $"City Information exceeds screen bounds: {screen}",
                panel);
        }
    }

    private void AddIssue(
        bool error,
        string surface,
        string language,
        string message,
        UnityEngine.Object context)
    {
        string line =
            $"{(error ? "ERROR" : "WARN")} " +
            $"[{GetAspectProfile(Screen.width, Screen.height)}/" +
            $"{surface}/{language.ToUpperInvariant()}] {message}";

        reportLines.Add(
            line);

        if (error)
        {
            Debug.LogError(
                line,
                context);
        }
        else
        {
            Debug.LogWarning(
                line,
                context);
        }
    }

    private bool OpenCityInformation()
    {
        CloseAllPreviewSurfaces();

        AtlasBoardCityInformationPanel panel =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardCityInformationPanel>();

        BoardTile tile =
            FindFirstCity();

        if (panel == null ||
            tile == null)
        {
            return false;
        }

        panel.ShowTile(
            tile);

        return panel.IsOpen;
    }

    private void CloseCityInformation()
    {
        AtlasBoardCityInformationPanel panel =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardCityInformationPanel>();

        panel?.Hide();
    }

    private bool OpenMatchResult()
    {
        CloseAllPreviewSurfaces();

        MatchResultManager manager =
            UnityEngine.Object.FindAnyObjectByType<
                MatchResultManager>();

        if (manager == null)
        {
            return false;
        }

        manager.ResetForNewMatchSession();
        manager.ShowMatchResult();

        return manager.ResultShown;
    }

    private void CloseMatchResult()
    {
        MatchResultManager manager =
            UnityEngine.Object.FindAnyObjectByType<
                MatchResultManager>();

        manager?.ResetForNewMatchSession();
    }

    private bool OpenAuction()
    {
        CloseAllPreviewSurfaces();

        AuctionManager auction =
            UnityEngine.Object.FindAnyObjectByType<
                AuctionManager>();

        BoardTile tile =
            FindFirstCity();

        List<PlayerGameState> players =
            FindPlayers();

        if (auction == null ||
            tile == null ||
            players.Count == 0)
        {
            return false;
        }

        PlayerGameState bidder =
            players[0];

        PlayerGameState leader =
            players.Count > 1
                ? players[1]
                : players[0];

        auction.ShowOnlineRemoteAuctionState(
            tile,
            bidder,
            leader,
            150,
            10,
            10,
            50);

        return auction.IsAuctionActive;
    }

    private void CloseAuction()
    {
        AuctionManager auction =
            UnityEngine.Object.FindAnyObjectByType<
                AuctionManager>();

        auction?.ClearOnlineRemoteAuctionState();
    }

    private bool OpenTrade()
    {
        CloseAllPreviewSurfaces();

        TradeManager trade =
            UnityEngine.Object.FindAnyObjectByType<
                TradeManager>();

        if (trade == null)
        {
            return false;
        }

        trade.ResetForNewMatchSession();
        trade.OpenTradePanel();

        return !trade.IsTradeClosed;
    }

    private void CloseTrade()
    {
        TradeManager trade =
            UnityEngine.Object.FindAnyObjectByType<
                TradeManager>();

        trade?.ResetForNewMatchSession();
    }

    private bool OpenDevelopment()
    {
        CloseAllPreviewSurfaces();

        TileResolutionManager resolution =
            UnityEngine.Object.FindAnyObjectByType<
                TileResolutionManager>();

        List<PlayerGameState> players =
            FindPlayers();

        BoardTile tile =
            FindFirstCity();

        if (resolution == null ||
            players.Count == 0 ||
            tile == null)
        {
            return false;
        }

        resolution.ShowOnlineRemoteDevelopmentDecision(
            players[0],
            tile);

        return true;
    }

    private void CloseDevelopment()
    {
        TileResolutionManager resolution =
            UnityEngine.Object.FindAnyObjectByType<
                TileResolutionManager>();

        resolution?.ClearOnlineRemoteDevelopmentDecision();
    }

    private void CloseAllPreviewSurfaces()
    {
        CloseCityInformation();
        CloseMatchResult();
        CloseAuction();
        CloseTrade();
        CloseDevelopment();
    }

    private static BoardTile FindFirstCity()
    {
        BoardPath board =
            UnityEngine.Object.FindAnyObjectByType<
                BoardPath>();

        if (board == null)
        {
            return null;
        }

        for (int index = 0;
             index < board.TileCount;
             index++)
        {
            BoardTile tile =
                board.GetTile(
                    index);

            if (tile != null &&
                tile.TileType ==
                    TileType.City)
            {
                return tile;
            }
        }

        return null;
    }

    private static List<PlayerGameState> FindPlayers()
    {
        List<PlayerGameState> players =
            new List<PlayerGameState>();

        TurnManager turns =
            UnityEngine.Object.FindAnyObjectByType<
                TurnManager>();

        if (turns == null)
        {
            return players;
        }

        for (int slot = 0;
             slot < 4;
             slot++)
        {
            PlayerGameState player =
                turns.GetPlayerStateBySlotIndex(
                    slot);

            if (player != null &&
                !player.IsBankrupt)
            {
                players.Add(
                    player);
            }
        }

        return players;
    }

    private void SafeClose(
        SurfaceStep surface)
    {
        if (surface == null ||
            surface.Close == null)
        {
            return;
        }

        try
        {
            surface.Close();
        }
        catch (Exception exception)
        {
            LogWarning(
                $"Phase 14J could not close {surface.Name}: " +
                exception.Message);
        }
    }

    private void RestoreOriginalState()
    {
        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;

        if (localization != null &&
            !string.IsNullOrWhiteSpace(
                originalLanguage))
        {
            localization.SetLanguage(
                originalLanguage);
        }

        Time.timeScale =
            originalTimeScale > 0f
                ? originalTimeScale
                : 1f;
    }

    private void AbortRun(
        string reason)
    {
        CloseAllPreviewSurfaces();
        RestoreOriginalState();

        fullRunActive =
            false;

        currentSweepActive =
            false;

        status =
            "ABORTED: " +
            reason;

        LogError(
            "Phase 14J ABORTED: " +
            reason);
    }

    private static Transform FindActiveRectByName(
        string name)
    {
        RectTransform[] rects =
            UnityEngine.Object.FindObjectsByType<
                RectTransform>(
                    FindObjectsInactive.Exclude);

        foreach (RectTransform rect
                 in rects)
        {
            if (rect != null &&
                string.Equals(
                    rect.name,
                    name,
                    StringComparison.Ordinal))
            {
                return rect;
            }
        }

        return null;
    }

    private static Rect GetScreenRect(
        RectTransform rect)
    {
        if (rect == null)
        {
            return default;
        }

        Vector3[] corners =
            new Vector3[4];

        rect.GetWorldCorners(
            corners);

        Canvas canvas =
            rect.GetComponentInParent<
                Canvas>();

        Canvas root =
            canvas != null
                ? canvas.rootCanvas
                : null;

        Camera camera =
            root == null ||
            root.renderMode ==
                RenderMode.ScreenSpaceOverlay
                ? null
                : root.worldCamera;

        Vector2 bottomLeft =
            RectTransformUtility.WorldToScreenPoint(
                camera,
                corners[0]);

        Vector2 topRight =
            RectTransformUtility.WorldToScreenPoint(
                camera,
                corners[2]);

        return Rect.MinMaxRect(
            Mathf.Min(
                bottomLeft.x,
                topRight.x),
            Mathf.Min(
                bottomLeft.y,
                topRight.y),
            Mathf.Max(
                bottomLeft.x,
                topRight.x),
            Mathf.Max(
                bottomLeft.y,
                topRight.y));
    }

    private static T ReadPrivateField<T>(
        object target,
        string fieldName)
        where T : class
    {
        if (target == null)
        {
            return null;
        }

        FieldInfo field =
            target.GetType()
                .GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

        return field != null
            ? field.GetValue(
                target) as T
            : null;
    }

    private string DetectSurfaceName()
    {
        if (FindActiveRectByName(
                "Phase14I_DevelopmentPolish") != null)
        {
            return "Development";
        }

        if (FindActiveRectByName(
                "Phase14H_TradePolish_Final") != null ||
            FindActiveRectByName(
                "Phase14H_TradePolish") != null)
        {
            return "Trade";
        }

        if (FindActiveRectByName(
                "Phase14G_AuctionPolish") != null)
        {
            return "Auction";
        }

        if (FindActiveRectByName(
                "Phase14F_Final_PlayerCards") != null)
        {
            return "MatchResult";
        }

        Transform city =
            FindActiveRectByName(
                "CityInformationPanel");

        if (city != null)
        {
            return "CityInfo";
        }

        GameObject mainMenu =
            GameObject.Find(
                "Canvas_MainMenu");

        if (mainMenu != null &&
            mainMenu.activeInHierarchy)
        {
            return "MainMenu";
        }

        return
            UnityEngine.Object.FindAnyObjectByType<
                BoardPath>() != null
                ? "Gameplay"
                : "Other";
    }

    private static string GetAspectProfile(
        int width,
        int height)
    {
        if (height <= 0)
        {
            return "UNKNOWN";
        }

        float ratio =
            (float)width /
            height;

        if (ratio <
            1.55f)
        {
            return "NARROW";
        }

        if (ratio >=
            2.0f)
        {
            return "WIDE";
        }

        return "STANDARD";
    }

    private static string MatrixKey(
        string profile,
        string surface)
    {
        return
            profile +
            "|" +
            surface;
    }

    private string BuildCompactReport()
    {
        StringBuilder builder =
            new StringBuilder();

        string[] profiles =
        {
            "NARROW",
            "STANDARD",
            "WIDE"
        };

        string[] surfaces =
        {
            "Gameplay",
            "CityInfo",
            "MatchResult",
            "Auction",
            "Trade",
            "Development",
            "MainMenu",
            "Other"
        };

        foreach (string profile
                 in profiles)
        {
            builder.AppendLine(
                profile);

            foreach (string surface
                     in surfaces)
            {
                string key =
                    MatrixKey(
                        profile,
                        surface);

                if (matrix.TryGetValue(
                        key,
                        out string value))
                {
                    builder.AppendLine(
                        $"  {surface}: {value}");
                }
            }
        }

        return builder.Length > 0
            ? builder.ToString()
            : "No regression results yet.";
    }

    private string BuildReport()
    {
        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine(
            "ATLAS BOARD — PHASE 14J REGRESSION REPORT");

        builder.AppendLine(
            "Generated runtime.");

        builder.AppendLine();
        builder.Append(
            BuildCompactReport());

        if (reportLines.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine(
                "ISSUES:");

            foreach (string line
                     in reportLines)
            {
                builder.AppendLine(
                    line);
            }
        }

        return builder.ToString();
    }

    private void Log(
        string message)
    {
        Debug.Log(
            message);
    }

    private void LogWarning(
        string message)
    {
        Debug.LogWarning(
            message);
    }

    private void LogError(
        string message)
    {
        Debug.LogError(
            message);
    }
}
#endif
