using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI-only version badge plus cooperative compatibility guard for the existing
/// canonical RTDB lobby. This is not a new room, identity or networking manager.
/// Never infer compatibility from player names, buildGUID or screen appearance.
/// </summary>
[DisallowMultipleComponent]
public sealed class AtlasBoardReleaseVersionBadge : MonoBehaviour
{
    public const string ReleaseVersion = "0.9.0-beta.1";
    public const string ContentVersion = "1";

    private const string BadgeObjectName = "AtlasBoardReleaseVersion_BottomRight";
    private AtlasBoardLobbyRuntimeBridge bridge;
    private AtlasBoardMainMenuController menu;
    private TMP_Text badge;
    private bool incompatibilityHandling;
    private string lastMismatch = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallOnExistingMenu()
    {
        AtlasBoardMainMenuController[] menus =
            UnityEngine.Object.FindObjectsByType<AtlasBoardMainMenuController>(
                FindObjectsInactive.Include);
        foreach (AtlasBoardMainMenuController existing in menus)
        {
            if (existing == null || !existing.gameObject.scene.IsValid()) continue;
            if (existing.GetComponent<AtlasBoardReleaseVersionBadge>() == null)
                existing.gameObject.AddComponent<AtlasBoardReleaseVersionBadge>();
        }
    }

    private void Awake()
    {
        menu = GetComponent<AtlasBoardMainMenuController>();
        bridge = GetComponent<AtlasBoardLobbyRuntimeBridge>();
        if (bridge != null) bridge.SnapshotChanged += OnLobbySnapshotChanged;
        InstallBadge();
    }

    private void Start()
    {
        InstallBadge();
        if (bridge != null && bridge.CurrentSnapshot != null)
            OnLobbySnapshotChanged(bridge.CurrentSnapshot);
        if (!string.Equals(Application.version, ReleaseVersion, StringComparison.Ordinal))
            Debug.LogWarning("ATB_VERSION_BUILD_CONFIG mismatch=true project=" +
                Application.version + " expected=" + ReleaseVersion +
                " action=check_PlayerSettings_bundleVersion", this);
    }

    private void OnDestroy()
    {
        if (bridge != null) bridge.SnapshotChanged -= OnLobbySnapshotChanged;
    }

    private void InstallBadge()
    {
        if (badge != null) return;
        RectTransform mainMenu = null;
        foreach (RectTransform child in GetComponentsInChildren<RectTransform>(true))
        {
            if (child != null && child.gameObject.name == "MainMenu")
            {
                mainMenu = child;
                break;
            }
        }
        if (mainMenu == null) return; // The installed main menu may appear later.

        Transform found = mainMenu.Find(BadgeObjectName);
        GameObject labelObject = found != null ? found.gameObject :
            new GameObject(BadgeObjectName, typeof(RectTransform));
        if (found == null) labelObject.transform.SetParent(mainMenu, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-18f, 17f);
        rect.sizeDelta = new Vector2(220f, 32f);
        rect.SetAsLastSibling();

        badge = labelObject.GetComponent<TMP_Text>();
        if (badge == null) badge = labelObject.AddComponent<TextMeshProUGUI>();
        TMP_Text[] existingLabels = mainMenu.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text oldLabel in existingLabels)
        {
            if (oldLabel == null || oldLabel == badge || oldLabel.font == null)
                continue;
            badge.font = oldLabel.font;
            break;
        }
        badge.fontSize = 20;
        badge.fontStyle = FontStyles.Bold;
        badge.enableAutoSizing = false;
        badge.alignment = TextAlignmentOptions.BottomRight;
        badge.textWrappingMode = TextWrappingModes.NoWrap;
        badge.overflowMode = TextOverflowModes.Overflow;
        badge.raycastTarget = false;
        badge.color = new Color(0.06f, 0.09f, 0.13f, 1f);
        RefreshBadge();
    }

    private void LateUpdate()
    {
        if (badge == null) InstallBadge();
        // Some older serialized scenes attach the canonical lobby bridge after
        // the menu component. Subscribe once if it arrived later.
        if (bridge == null)
        {
            bridge = GetComponent<AtlasBoardLobbyRuntimeBridge>();
            if (bridge != null)
            {
                bridge.SnapshotChanged += OnLobbySnapshotChanged;
                if (bridge.CurrentSnapshot != null)
                    OnLobbySnapshotChanged(bridge.CurrentSnapshot);
            }
        }
    }

    private void RefreshBadge()
    {
        if (badge == null) return;
        badge.text = string.IsNullOrEmpty(lastMismatch)
            ? "v" + Application.version
            : "v" + Application.version + "  |  " + LocalizedMismatchLabel();
        badge.color = string.IsNullOrEmpty(lastMismatch)
            ? new Color(0.06f, 0.09f, 0.13f, 1f)
            : new Color(0.56f, 0.06f, 0.06f, 1f);
        badge.rectTransform.sizeDelta = string.IsNullOrEmpty(lastMismatch)
            ? new Vector2(220f, 32f)
            : new Vector2(345f, 32f);
    }

    private static string LocalizedMismatchLabel()
    {
        string language = AtlasBoardLocalizationManager.Instance != null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
            : "en";
        switch ((language ?? "en").ToLowerInvariant())
        {
            case "tr": return "GÜNCELLEME GEREKLİ";
            case "es": return "ACTUALIZACIÓN NECESARIA";
            case "fr": return "MISE À JOUR REQUISE";
            case "de": return "UPDATE ERFORDERLICH";
            case "ko": return "업데이트 필요";
            case "ru": return "НУЖНО ОБНОВЛЕНИЕ";
            default: return "UPDATE REQUIRED";
        }
    }

    /// <summary>
    /// Exact cross-platform update contract. Both Windows and Android use the
    /// same released GameVersion/ProtocolVersion/RulesVersion/ContentVersion.
    /// Missing/legacy metadata fails closed, including after reconnect/rematch.
    /// </summary>
    public static bool IsCompatible(AtlasLobbySnapshot snapshot)
    {
        return snapshot != null &&
            string.Equals(Application.version, ReleaseVersion, StringComparison.Ordinal) &&
            string.Equals(snapshot.GameVersion, ReleaseVersion, StringComparison.Ordinal) &&
            snapshot.ProtocolVersion == AtlasOnlineDefaults.ProtocolVersion &&
            snapshot.RulesVersion == AtlasOnlineDefaults.RulesVersion &&
            string.Equals(snapshot.ContentVersion, ContentVersion, StringComparison.Ordinal);
    }

    private void OnLobbySnapshotChanged(AtlasLobbySnapshot snapshot)
    {
        if (snapshot == null || IsCompatible(snapshot) || incompatibilityHandling)
            return;
        incompatibilityHandling = true;
        lastMismatch = "remote=" + (snapshot.GameVersion ?? "missing") +
            "/" + snapshot.ProtocolVersion + "/" + snapshot.RulesVersion +
            "/" + (snapshot.ContentVersion ?? "missing");
        RefreshBadge();
        Debug.LogError("ATB_VERSION_COMPATIBLE false local=" + Application.version +
            "/" + AtlasOnlineDefaults.ProtocolVersion + "/" +
            AtlasOnlineDefaults.RulesVersion + "/" + ContentVersion +
            " " + lastMismatch + " action=leave_incompatible_lobby", this);
        _ = ExitIncompatibleRoomAsync(snapshot);
    }

    private async System.Threading.Tasks.Task ExitIncompatibleRoomAsync(AtlasLobbySnapshot detected)
    {
        if (bridge == null) return;
        try
        {
            if (bridge.HasLobby)
            {
                bool localHost = string.Equals(detected.HostAccountId,
                    bridge.CurrentAccountId, StringComparison.Ordinal);
                AtlasLobbyOperationResult result = localHost
                    ? await bridge.CloseLobbyAsync()
                    : await bridge.LeaveLobbyAsync();
                if (result == null || !result.Success)
                    Debug.LogWarning("ATB_VERSION_ROOM_EXIT server_cleanup_pending=true", this);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_VERSION_ROOM_EXIT exception=" + e.GetType().Name, this);
        }
        finally
        {
            // Fail closed on this client even when server cleanup is pending.
            if (bridge != null) bridge.ClearLocalLobbyState();
            if (menu != null) menu.ShowMainMenu();
            incompatibilityHandling = false;
        }
    }
}
