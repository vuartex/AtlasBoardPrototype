using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Platform-independent pause menu fallback. Online room codes and real network
/// telemetry remain owned by LeaveFlow and NetworkQualityHud respectively.
/// A Local Offline game has neither a 6-digit invite code nor a network RTT:
/// the pause menu should explain N/A instead of hiding the entire region.
/// </summary>
[DefaultExecutionOrder(17000)]
public sealed class AtlasBoardSharedPauseDiagnostics : MonoBehaviour
{
    private static AtlasBoardSharedPauseDiagnostics instance;
    private RectTransform pauseWindow;
    private RectTransform offlineMetrics;
    private TMP_Text metricsLabel;
    private TMP_Text roomLabel;
    private TMP_Text roomValue;
    private GameObject roomShow;
    private GameObject roomCopy;
    private bool offlineRoomModified;
    private RectTransform missingRoomPlaceholder;
    private float nextResolveAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var host = new GameObject("AtlasBoard_Shared_Pause_Diagnostics");
        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardSharedPauseDiagnostics>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= nextResolveAt || pauseWindow == null)
        {
            nextResolveAt = Time.unscaledTime + 0.45f;
            ResolvePauseWindow();
        }
        if (pauseWindow == null) return;
        if (!pauseWindow.gameObject.activeInHierarchy)
        {
            if (offlineMetrics != null && offlineMetrics.gameObject.activeSelf)
                offlineMetrics.gameObject.SetActive(false);
            return;
        }

        var online = FindOnlineLobby();
        string code = online != null ? online.CurrentRoomCode : string.Empty;
        bool hasCode = IsSixDigits(code);
        if (hasCode)
        {
            // Leave the full Show/Copy UI and sensitive code masking to the
            // original LeaveFlowController. Never replace valid online data.
            RestoreOnlineRoomSection();
            if (missingRoomPlaceholder != null && missingRoomPlaceholder.gameObject.activeSelf)
                missingRoomPlaceholder.gameObject.SetActive(false);
        }
        else
        {
            ShowNoCodeExplanation();
        }

        bool hasActualTelemetry = false;
        var match = UnityEngine.Object.FindAnyObjectByType<AtlasBoardMatchRuntimeBridge>(
            FindObjectsInactive.Include);
        if (match != null && !string.IsNullOrWhiteSpace(match.CurrentMatchId))
            hasActualTelemetry = true;
        if (!hasActualTelemetry)
            ShowUnavailableNetworkMetrics();
        else if (offlineMetrics != null && offlineMetrics.gameObject.activeSelf)
            offlineMetrics.gameObject.SetActive(false);
    }

    private void ResolvePauseWindow()
    {
        RectTransform[] windows = Resources.FindObjectsOfTypeAll<RectTransform>();
        foreach (RectTransform candidate in windows)
        {
            if (candidate == null || !candidate.gameObject.scene.IsValid() ||
                candidate.gameObject.name != "PauseWindow") continue;
            if (pauseWindow == candidate) return;
            pauseWindow = candidate;
            offlineMetrics = null;
            metricsLabel = roomLabel = roomValue = null;
            roomShow = roomCopy = null;
            offlineRoomModified = false;
            missingRoomPlaceholder = pauseWindow.Find("AtlasBoardLocalRoomPlaceholder") as RectTransform;
            Transform existing = pauseWindow.Find("AtlasBoardNoNetworkMetrics");
            offlineMetrics = existing as RectTransform;
            if (offlineMetrics != null)
                metricsLabel = offlineMetrics.GetComponentInChildren<TMP_Text>(true);
            return;
        }
        pauseWindow = null;
    }

    private static bool IsSixDigits(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim();
        if (value.Length != 6) return false;
        foreach (char c in value) if (c < '0' || c > '9') return false;
        return true;
    }

    private static AtlasBoardLobbyRuntimeBridge FindOnlineLobby()
    {
        return UnityEngine.Object.FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>(
            FindObjectsInactive.Include);
    }

    private void ShowNoCodeExplanation()
    {
        Transform section = pauseWindow.Find("RoomCodeSection");
        if (section == null)
        {
            ShowMissingRoomPlaceholder();
            return;
        }
        if (missingRoomPlaceholder != null && missingRoomPlaceholder.gameObject.activeSelf)
            missingRoomPlaceholder.gameObject.SetActive(false);
        if (roomLabel == null)
        {
            Transform t = section.Find("RoomCodeLabel");
            roomLabel = t != null ? t.GetComponent<TMP_Text>() : null;
        }
        if (roomValue == null)
        {
            Transform t = section.Find("RoomCodeValue");
            roomValue = t != null ? t.GetComponent<TMP_Text>() : null;
        }
        if (roomShow == null)
        {
            Transform t = section.Find("Button_RoomCodeShowHide");
            roomShow = t != null ? t.gameObject : null;
        }
        if (roomCopy == null)
        {
            Transform t = section.Find("Button_RoomCodeCopy");
            roomCopy = t != null ? t.gameObject : null;
        }
        if (!section.gameObject.activeSelf) section.gameObject.SetActive(true);
        if (roomLabel != null) roomLabel.text = "LOCAL / NO ROOM CODE";
        if (roomValue != null) roomValue.text = "—";
        if (roomShow != null) roomShow.SetActive(false);
        if (roomCopy != null) roomCopy.SetActive(false);
        offlineRoomModified = true;
    }

    private void ShowMissingRoomPlaceholder()
    {
        if (missingRoomPlaceholder == null)
        {
            GameObject panel = new GameObject("AtlasBoardLocalRoomPlaceholder",
                typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(pauseWindow, false);
            missingRoomPlaceholder = panel.GetComponent<RectTransform>();
            missingRoomPlaceholder.anchorMin = missingRoomPlaceholder.anchorMax =
                new Vector2(0.5f, 0.5f);
            missingRoomPlaceholder.pivot = new Vector2(0.5f, 0.5f);
            missingRoomPlaceholder.anchoredPosition = new Vector2(0f, 170f);
            missingRoomPlaceholder.sizeDelta = new Vector2(600f, 110f);
            panel.GetComponent<Image>().color = new Color32(246, 241, 231, 255);
            panel.GetComponent<Image>().raycastTarget = false;
            GameObject labelObject = new GameObject("LocalRoomLabel",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(missingRoomPlaceholder, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10f, 10f);
            rect.offsetMax = new Vector2(-10f, -10f);
            TMP_Text label = labelObject.GetComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
                label.font = TMP_Settings.defaultFontAsset;
            label.text = "LOCAL SESSION\nNO INVITE CODE";
            label.fontSize = 23f;
            label.color = new Color32(61, 62, 66, 255);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }
        if (!missingRoomPlaceholder.gameObject.activeSelf)
            missingRoomPlaceholder.gameObject.SetActive(true);
    }

    private void RestoreOnlineRoomSection()
    {
        if (!offlineRoomModified) return;
        Transform section = pauseWindow.Find("RoomCodeSection");
        if (section != null && !section.gameObject.activeSelf)
            section.gameObject.SetActive(true);
        if (roomLabel != null) roomLabel.text = "ROOM CODE";
        if (roomShow != null) roomShow.SetActive(true);
        if (roomCopy != null) roomCopy.SetActive(true);
        // The LeaveFlowController owns the masked code value and can refresh
        // it next time the pause menu opens. We must never unmask it here.
        if (roomValue != null) roomValue.text = "••••••";
        offlineRoomModified = false;
    }

    private void ShowUnavailableNetworkMetrics()
    {
        if (offlineMetrics == null)
        {
            GameObject panel = new GameObject("AtlasBoardNoNetworkMetrics",
                typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(pauseWindow, false);
            offlineMetrics = panel.GetComponent<RectTransform>();
            offlineMetrics.anchorMin = offlineMetrics.anchorMax =
                new Vector2(0.5f, 0.5f);
            offlineMetrics.pivot = new Vector2(0.5f, 0.5f);
            offlineMetrics.anchoredPosition = new Vector2(0f, -310f);
            offlineMetrics.sizeDelta = new Vector2(500f, 32f);
            Image background = panel.GetComponent<Image>();
            background.color = new Color32(224, 226, 230, 255);
            background.raycastTarget = false;

            GameObject textObject = new GameObject("LocalNetworkStatus",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(offlineMetrics, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            metricsLabel = textObject.GetComponent<TextMeshProUGUI>();
            metricsLabel.fontSize = 15f;
            if (TMP_Settings.defaultFontAsset != null)
                metricsLabel.font = TMP_Settings.defaultFontAsset;
            metricsLabel.color = new Color32(58, 64, 74, 255);
            metricsLabel.alignment = TextAlignmentOptions.Center;
            metricsLabel.raycastTarget = false;
            metricsLabel.textWrappingMode = TextWrappingModes.NoWrap;
            metricsLabel.text = "NO NETWORK SESSION  ·  Ping —  ·  Jitter —  ·  Loss —";
        }
        if (!offlineMetrics.gameObject.activeSelf)
            offlineMetrics.gameObject.SetActive(true);
    }
}
