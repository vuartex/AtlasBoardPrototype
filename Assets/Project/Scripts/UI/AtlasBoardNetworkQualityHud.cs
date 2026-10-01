using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AtlasBoardNetworkQualityHud :
    MonoBehaviour
{
    private enum QualityState
    {
        Waiting,
        Good,
        Fair,
        Poor,
        Reconnecting,
        Offline
    }

    private readonly struct WindowSample
    {
        public WindowSample(
            bool successful,
            bool late,
            float rttMs)
        {
            Successful = successful;
            Late = late;
            RttMs = rttMs;
        }

        public bool Successful { get; }
        public bool Late { get; }
        public float RttMs { get; }
    }

    private const string SnapshotFunctionName =
        "matchGetSnapshot";

    private const int MaximumSamples = 24;
    private const float LateSampleThresholdMs = 1500f;
    private const float ReconnectingAfterSeconds = 2.5f;
    private const float OfflineAfterSeconds = 10f;
    private const float UiRefreshSeconds = 0.15f;

    private readonly List<WindowSample>
        samples =
            new List<WindowSample>(
                MaximumSamples);

    private AtlasBoardMatchRuntimeBridge
        matchBridge;

    private TurnManager
        turnManager;

    private RectTransform
        pauseWindow;

    private RectTransform
        panelRect;

    private Image
        panelBackground;

    private Image
        stateAccent;

    private TMP_Text
        statusText;

    private TMP_Text
        metricsText;

    private float
        smoothedPingMs = -1f;

    private float
        jitterMs = -1f;

    private float
        lossPercent = -1f;

    private int
        consecutiveFailures;

    private double
        lastSuccessfulSampleAt = -1d;

    private double
        lastAnySampleAt = -1d;

    private float
        nextUiRefreshAt;

    private string
        trackedMatchId = string.Empty;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        if (UnityEngine.Object.FindAnyObjectByType<
                BoardPath>() == null)
        {
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardNetworkQualityHud>() != null)
        {
            return;
        }

        GameObject root =
            new GameObject(
                "AtlasBoard_Phase14B_NetworkQualityHUD");

        root.AddComponent<
            AtlasBoardNetworkQualityHud>();
    }

    private void Awake()
    {
        ResolveReferences();
        TryAttachToPauseMenu();
    }

    private void OnEnable()
    {
        AtlasBoardNetworkTelemetry.SampleReported +=
            HandleTelemetrySample;

        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;
    }

    private void OnDisable()
    {
        AtlasBoardNetworkTelemetry.SampleReported -=
            HandleTelemetrySample;

        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;
    }

    private void Update()
    {
        ResolveReferences();

        if (panelRect == null)
        {
            TryAttachToPauseMenu();
        }

        string currentMatchId =
            matchBridge != null
                ? matchBridge.CurrentMatchId ??
                  string.Empty
                : string.Empty;

        if (!string.Equals(
                trackedMatchId,
                currentMatchId,
                StringComparison.Ordinal))
        {
            ResetMeasurements();
            trackedMatchId =
                currentMatchId;
        }

        bool shouldShow =
            ShouldShowInsidePauseMenu();

        SetVisible(
            shouldShow);

        if (!shouldShow)
        {
            return;
        }

        if (Time.unscaledTime >=
            nextUiRefreshAt)
        {
            nextUiRefreshAt =
                Time.unscaledTime +
                UiRefreshSeconds;

            RefreshPresentation();
        }
    }

    private void ResolveReferences()
    {
        if (matchBridge == null)
        {
            matchBridge =
                UnityEngine.Object.FindAnyObjectByType<
                    AtlasBoardMatchRuntimeBridge>();
        }

        if (turnManager == null)
        {
            turnManager =
                UnityEngine.Object.FindAnyObjectByType<
                    TurnManager>();
        }
    }

    private void TryAttachToPauseMenu()
    {
        if (panelRect != null)
        {
            return;
        }

        pauseWindow =
            FindSceneRect(
                "PauseWindow");

        if (pauseWindow == null)
        {
            return;
        }

        Transform existing =
            pauseWindow.Find(
                "NetworkQuality");

        if (existing != null)
        {
            panelRect =
                existing as RectTransform;

            if (panelRect != null)
            {
                panelBackground =
                    existing.GetComponent<
                        Image>();

                Transform accent =
                    existing.Find(
                        "StateAccent");

                if (accent != null)
                {
                    stateAccent =
                        accent.GetComponent<
                            Image>();
                }

                Transform state =
                    existing.Find(
                        "State");

                if (state != null)
                {
                    statusText =
                        state.GetComponent<
                            TMP_Text>();
                }

                Transform metrics =
                    existing.Find(
                        "Metrics");

                if (metrics != null)
                {
                    metricsText =
                        metrics.GetComponent<
                            TMP_Text>();
                }

                RefreshPresentation();
                return;
            }
        }

        BuildPauseMenuRow();
        RefreshPresentation();
    }

    private bool ShouldShowInsidePauseMenu()
    {
        if (panelRect == null ||
            pauseWindow == null ||
            matchBridge == null ||
            string.IsNullOrWhiteSpace(
                matchBridge.CurrentMatchId))
        {
            return false;
        }

        if (!pauseWindow.gameObject
            .activeInHierarchy)
        {
            return false;
        }

        if (turnManager != null &&
            !turnManager.IsMatchStarted)
        {
            return false;
        }

        return true;
    }

    private void HandleTelemetrySample(
        AtlasBoardNetworkTelemetrySample sample)
    {
        if (!string.Equals(
                sample.FunctionName,
                SnapshotFunctionName,
                StringComparison.Ordinal))
        {
            return;
        }

        if (matchBridge == null ||
            string.IsNullOrWhiteSpace(
                matchBridge.CurrentMatchId))
        {
            return;
        }

        lastAnySampleAt =
            sample.RealtimeAt;

        bool late =
            sample.RequestWasSent &&
            sample.Success &&
            sample.RoundTripMs >=
                LateSampleThresholdMs;

        samples.Add(
            new WindowSample(
                sample.Success,
                late,
                sample.RoundTripMs));

        if (samples.Count >
            MaximumSamples)
        {
            samples.RemoveAt(
                0);
        }

        if (sample.Success &&
            sample.RequestWasSent)
        {
            consecutiveFailures = 0;

            lastSuccessfulSampleAt =
                sample.RealtimeAt;

            if (smoothedPingMs < 0f)
            {
                smoothedPingMs =
                    sample.RoundTripMs;
            }
            else
            {
                smoothedPingMs =
                    Mathf.Lerp(
                        smoothedPingMs,
                        sample.RoundTripMs,
                        0.25f);
            }
        }
        else
        {
            consecutiveFailures++;
        }

        RecalculateWindowMetrics();

        if (ShouldShowInsidePauseMenu())
        {
            RefreshPresentation();
        }
    }

    private void RecalculateWindowMetrics()
    {
        if (samples.Count == 0)
        {
            jitterMs = -1f;
            lossPercent = -1f;
            return;
        }

        int lossCount = 0;
        float jitterTotal = 0f;
        int jitterPairs = 0;
        float previousSuccessfulRtt = -1f;

        for (int index = 0;
             index < samples.Count;
             index++)
        {
            WindowSample sample =
                samples[index];

            if (!sample.Successful ||
                sample.Late)
            {
                lossCount++;
            }

            if (!sample.Successful)
            {
                continue;
            }

            if (previousSuccessfulRtt >=
                0f)
            {
                jitterTotal +=
                    Mathf.Abs(
                        sample.RttMs -
                        previousSuccessfulRtt);

                jitterPairs++;
            }

            previousSuccessfulRtt =
                sample.RttMs;
        }

        lossPercent =
            100f *
            lossCount /
            samples.Count;

        jitterMs =
            jitterPairs > 0
                ? jitterTotal /
                  jitterPairs
                : 0f;
    }

    private void ResetMeasurements()
    {
        samples.Clear();

        smoothedPingMs = -1f;
        jitterMs = -1f;
        lossPercent = -1f;
        consecutiveFailures = 0;
        lastSuccessfulSampleAt = -1d;
        lastAnySampleAt = -1d;
        trackedMatchId = string.Empty;
    }

    private QualityState ResolveQualityState()
    {
        double now =
            Time.realtimeSinceStartupAsDouble;

        if (lastAnySampleAt < 0d)
        {
            return QualityState.Waiting;
        }

        if (lastSuccessfulSampleAt < 0d)
        {
            return now -
                   lastAnySampleAt >=
                   OfflineAfterSeconds
                ? QualityState.Offline
                : QualityState.Reconnecting;
        }

        double successAge =
            now -
            lastSuccessfulSampleAt;

        if (successAge >=
            OfflineAfterSeconds)
        {
            return QualityState.Offline;
        }

        if (consecutiveFailures > 0 ||
            successAge >=
                ReconnectingAfterSeconds)
        {
            return QualityState.Reconnecting;
        }

        float ping =
            Mathf.Max(
                0f,
                smoothedPingMs);

        float jitter =
            Mathf.Max(
                0f,
                jitterMs);

        float loss =
            Mathf.Max(
                0f,
                lossPercent);

        if (ping <= 220f &&
            jitter <= 70f &&
            loss < 5f)
        {
            return QualityState.Good;
        }

        if (ping <= 450f &&
            jitter <= 150f &&
            loss < 15f)
        {
            return QualityState.Fair;
        }

        return QualityState.Poor;
    }

    private void RefreshPresentation()
    {
        if (panelRect == null)
        {
            return;
        }

        QualityState state =
            ResolveQualityState();

        if (statusText != null)
        {
            statusText.text =
                $"{L("NETWORK", "AĞ", "RED", "RÉSEAU", "NETZ", "네트워크", "СЕТЬ")}  " +
                $"{StateIcon(state)} " +
                $"{StateLabel(state)}";
        }

        if (metricsText != null)
        {
            metricsText.text =
                $"{L("PING", "PING", "PING", "PING", "PING", "핑", "ПИНГ")} " +
                $"{FormatMilliseconds(smoothedPingMs)}   " +
                $"{L("JIT", "JIT", "JIT", "JIT", "JIT", "지터", "ДЖИТ")} " +
                $"{FormatMilliseconds(jitterMs)}   " +
                $"{L("LOSS", "KAYIP", "PÉRD", "PERTE", "VERL", "손실", "ПОТ")} " +
                $"{FormatPercent(lossPercent)}";
        }

        ApplyStateVisual(
            state);

        ApplyResolvedFonts();
    }

    private void ApplyStateVisual(
        QualityState state)
    {
        Color accent =
            state switch
            {
                QualityState.Good =>
                    new Color(
                        0.20f,
                        0.63f,
                        0.36f,
                        1f),

                QualityState.Fair =>
                    new Color(
                        0.77f,
                        0.57f,
                        0.04f,
                        1f),

                QualityState.Poor =>
                    new Color(
                        0.75f,
                        0.22f,
                        0.25f,
                        1f),

                QualityState.Reconnecting =>
                    new Color(
                        0.85f,
                        0.48f,
                        0.06f,
                        1f),

                QualityState.Offline =>
                    new Color(
                        0.36f,
                        0.39f,
                        0.44f,
                        1f),

                _ =>
                    new Color(
                        0.23f,
                        0.46f,
                        0.72f,
                        1f)
            };

        if (stateAccent != null)
        {
            stateAccent.color =
                accent;
        }

        if (statusText != null)
        {
            statusText.color =
                accent;
        }

        if (panelBackground != null)
        {
            panelBackground.color =
                new Color32(
                    224,
                    226,
                    230,
                    255);
        }
    }

    private void BuildPauseMenuRow()
    {
        GameObject panelObject =
            new GameObject(
                "NetworkQuality",
                typeof(RectTransform),
                typeof(Image));

        panelObject.transform.SetParent(
            pauseWindow,
            false);

        panelRect =
            panelObject.GetComponent<
                RectTransform>();

        panelRect.anchorMin =
            new Vector2(
                0.5f,
                0.5f);

        panelRect.anchorMax =
            new Vector2(
                0.5f,
                0.5f);

        panelRect.pivot =
            new Vector2(
                0.5f,
                0.5f);

        panelRect.anchoredPosition =
            new Vector2(
                0f,
                -310f);

        panelRect.sizeDelta =
            new Vector2(
                500f,
                38f);

        panelBackground =
            panelObject.GetComponent<
                Image>();

        panelBackground.color =
            new Color32(
                224,
                226,
                230,
                255);

        panelBackground.raycastTarget =
            false;

        GameObject accentObject =
            new GameObject(
                "StateAccent",
                typeof(RectTransform),
                typeof(Image));

        accentObject.transform.SetParent(
            panelObject.transform,
            false);

        RectTransform accentRect =
            accentObject.GetComponent<
                RectTransform>();

        accentRect.anchorMin =
            new Vector2(
                0f,
                0f);

        accentRect.anchorMax =
            new Vector2(
                0f,
                1f);

        accentRect.pivot =
            new Vector2(
                0f,
                0.5f);

        accentRect.offsetMin =
            Vector2.zero;

        accentRect.offsetMax =
            new Vector2(
                5f,
                0f);

        stateAccent =
            accentObject.GetComponent<
                Image>();

        stateAccent.raycastTarget =
            false;

        statusText =
            CreateText(
                "State",
                panelObject.transform,
                12f,
                FontStyles.Bold,
                TextAlignmentOptions.Left,
                new Color32(
                    38,
                    107,
                    180,
                    255));

        RectTransform statusRect =
            statusText.rectTransform;

        statusRect.anchorMin =
            new Vector2(
                0f,
                0f);

        statusRect.anchorMax =
            new Vector2(
                0f,
                1f);

        statusRect.pivot =
            new Vector2(
                0f,
                0.5f);

        statusRect.offsetMin =
            new Vector2(
                16f,
                5f);

        statusRect.offsetMax =
            new Vector2(
                178f,
                -5f);

        statusText.enableAutoSizing =
            true;

        statusText.fontSizeMin =
            8f;

        statusText.fontSizeMax =
            12f;

        statusText.textWrappingMode =
            TextWrappingModes.NoWrap;

        statusText.overflowMode =
            TextOverflowModes.Truncate;

        metricsText =
            CreateText(
                "Metrics",
                panelObject.transform,
                11f,
                FontStyles.Normal,
                TextAlignmentOptions.Right,
                new Color32(
                    52,
                    58,
                    68,
                    255));

        RectTransform metricsRect =
            metricsText.rectTransform;

        metricsRect.anchorMin =
            new Vector2(
                0f,
                0f);

        metricsRect.anchorMax =
            new Vector2(
                1f,
                1f);

        metricsRect.pivot =
            new Vector2(
                1f,
                0.5f);

        metricsRect.offsetMin =
            new Vector2(
                170f,
                5f);

        metricsRect.offsetMax =
            new Vector2(
                -12f,
                -5f);

        metricsText.enableAutoSizing =
            true;

        metricsText.fontSizeMin =
            7f;

        metricsText.fontSizeMax =
            11f;

        metricsText.textWrappingMode =
            TextWrappingModes.NoWrap;

        metricsText.overflowMode =
            TextOverflowModes.Truncate;

        panelObject.SetActive(
            false);
    }

    private void SetVisible(
        bool visible)
    {
        if (panelRect == null)
        {
            return;
        }

        if (panelRect.gameObject.activeSelf !=
            visible)
        {
            panelRect.gameObject.SetActive(
                visible);
        }
    }

    private void HandleLanguageChanged()
    {
        if (panelRect != null)
        {
            RefreshPresentation();
        }
    }

    private void ApplyResolvedFonts()
    {
        AtlasBoardLocalizationManager manager =
            AtlasBoardLocalizationManager.Instance;

        if (manager == null ||
            panelRect == null)
        {
            return;
        }

        TMP_Text[] texts =
            panelRect.GetComponentsInChildren<
                TMP_Text>(
                true);

        foreach (TMP_Text text
                 in texts)
        {
            if (text == null)
            {
                continue;
            }

            TMP_FontAsset baseFont =
                TMP_Settings.defaultFontAsset != null
                    ? TMP_Settings.defaultFontAsset
                    : text.font;

            TMP_FontAsset resolved =
                manager.ResolveFont(
                    baseFont);

            if (resolved != null)
            {
                text.font =
                    resolved;
            }
        }
    }

    private static RectTransform FindSceneRect(
        string objectName)
    {
        RectTransform[] rects =
            UnityEngine.Object.FindObjectsByType<
                RectTransform>(FindObjectsInactive.Include);

        foreach (RectTransform rect
                 in rects)
        {
            if (rect == null ||
                !rect.gameObject.scene.IsValid())
            {
                continue;
            }

            if (string.Equals(
                    rect.name,
                    objectName,
                    StringComparison.Ordinal))
            {
                return rect;
            }
        }

        return null;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject gameObject =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));

        gameObject.transform.SetParent(
            parent,
            false);

        TMP_Text text =
            gameObject.GetComponent<
                TMP_Text>();

        text.fontSize =
            fontSize;

        text.fontStyle =
            fontStyle;

        text.alignment =
            alignment;

        text.color =
            color;

        text.raycastTarget =
            false;

        text.richText =
            true;

        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font =
                TMP_Settings.defaultFontAsset;
        }

        return text;
    }

    private static string StateIcon(
        QualityState state)
    {
        return state switch
        {
            QualityState.Good => "●",
            QualityState.Fair => "◆",
            QualityState.Poor => "!",
            QualityState.Reconnecting => "↻",
            QualityState.Offline => "×",
            _ => "…"
        };
    }

    private static string FormatMilliseconds(
        float value)
    {
        if (value < 0f)
        {
            return "--";
        }

        return $"{Mathf.RoundToInt(value)}ms";
    }

    private static string FormatPercent(
        float value)
    {
        if (value < 0f)
        {
            return "--";
        }

        if (value < 1f)
        {
            return $"{value:0.0}%";
        }

        return $"{Mathf.RoundToInt(value)}%";
    }

    private static string StateLabel(
        QualityState state)
    {
        return state switch
        {
            QualityState.Good =>
                L(
                    "GOOD",
                    "İYİ",
                    "BUENA",
                    "BONNE",
                    "GUT",
                    "좋음",
                    "ХОРОШО"),

            QualityState.Fair =>
                L(
                    "FAIR",
                    "ORTA",
                    "MEDIA",
                    "MOYEN",
                    "MITTEL",
                    "보통",
                    "СРЕДНЕ"),

            QualityState.Poor =>
                L(
                    "POOR",
                    "ZAYIF",
                    "MALA",
                    "FAIBLE",
                    "SCHLECHT",
                    "나쁨",
                    "ПЛОХО"),

            QualityState.Reconnecting =>
                L(
                    "RECONNECTING",
                    "YENİDEN BAĞLANIYOR",
                    "RECONECTANDO",
                    "RECONNEXION",
                    "VERBINDET NEU",
                    "재연결 중",
                    "ПЕРЕПОДКЛЮЧЕНИЕ"),

            QualityState.Offline =>
                L(
                    "OFFLINE",
                    "ÇEVRİMDIŞI",
                    "SIN CONEXIÓN",
                    "HORS LIGNE",
                    "OFFLINE",
                    "오프라인",
                    "НЕ В СЕТИ"),

            _ =>
                L(
                    "MEASURING",
                    "ÖLÇÜLÜYOR",
                    "MIDIENDO",
                    "MESURE",
                    "MESSUNG",
                    "측정 중",
                    "ИЗМЕРЕНИЕ")
        };
    }

    private static string L(
        string en,
        string tr,
        string es,
        string fr,
        string de,
        string ko,
        string ru,
        params object[] args)
    {
        return AtlasBoardL.R(
            en,
            tr,
            es,
            fr,
            de,
            ko,
            ru,
            args);
    }
}
