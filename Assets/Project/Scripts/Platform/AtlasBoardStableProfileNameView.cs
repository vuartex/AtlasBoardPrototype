using System;
using System.Reflection;
using TMPro;
using UnityEngine;

/// <summary>
/// Shared presentation-only guard for canonical Player HUD names.
/// Legacy progression code can temporarily append " • Lv. N" to ProfileName,
/// while the canonical identity updater removes it. This child graphic renders
/// the normalized name without modifying the source name, Firebase identity or
/// Career level. Safe across scene reload and all platforms.
/// </summary>
[DefaultExecutionOrder(32000)]
[DisallowMultipleComponent]
public sealed class AtlasBoardStableProfileNameView : MonoBehaviour
{
    private TMP_Text source;
    private TextMeshProUGUI displayed;
    private Color sourceColor;
    private string lastName = string.Empty;

    public enum AliasSurface { ProfileCard, LocalHostLobbySeat, LocalHostMatchHud }
    private AliasSurface surface = AliasSurface.ProfileCard;
    private float nextLobbyAliasAuthorityCheck;
    private bool localHostLobbyAliasAllowed;

    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo SeatNamesField =
        typeof(AtlasBoardPrivateLobbyUIController).GetField(
            "privateSeatNames", PrivateInstance);
    private static readonly FieldInfo SnapshotField =
        typeof(AtlasBoardPrivateLobbyUIController).GetField(
            "backendSnapshot", PrivateInstance);
    private static readonly FieldInfo LocalAccountIdField =
        typeof(AtlasBoardPrivateLobbyUIController).GetField(
            "backendLocalAccountId", PrivateInstance);
    private static readonly FieldInfo LocalIsHostField =
        typeof(AtlasBoardPrivateLobbyUIController).GetField(
            "localIsHost", PrivateInstance);
    private static readonly FieldInfo RoomActiveField =
        typeof(AtlasBoardPrivateLobbyUIController).GetField(
            "roomActive", PrivateInstance);

    // Presentation only: Steam owns the alias, not an authenticated cloud UID.
    public void ConfigureAliasSurface(AliasSurface target)
    {
        surface = target;
        lastName = string.Empty;
    }

    public static string ResolvedSteamPersona() =>
        AtlasBoardCanonicalPlayerIdentity.ValidSteamPersona();

    private void Awake()
    {
        source = GetComponent<TMP_Text>();
        if (source == null) { enabled = false; return; }
        sourceColor = source.color;
        CreateDisplay();
    }

    private void OnEnable()
    {
        UpdateDisplay();
    }

    private void LateUpdate()
    {
        UpdateDisplay();
    }

    private void OnDestroy()
    {
        if (source != null)
            source.color = sourceColor;
    }

    private void CreateDisplay()
    {
        Transform existing = transform.Find("AtlasBoardStableNameGraphic");
        if (existing != null)
            displayed = existing.GetComponent<TextMeshProUGUI>();

        if (displayed == null)
        {
            GameObject go = new GameObject("AtlasBoardStableNameGraphic",
                typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            displayed = go.GetComponent<TextMeshProUGUI>();
        }
        displayed.raycastTarget = false;
        displayed.maskable = source.maskable;
    }

    private static string WithoutLevel(string current)
    {
        if (string.IsNullOrWhiteSpace(current)) return string.Empty;
        const string marker = " • Lv. ";
        int at = current.LastIndexOf(marker, StringComparison.Ordinal);
        if (at < 0) return current;
        string suffix = current.Substring(at + marker.Length).Trim();
        return int.TryParse(suffix, out int level) && level >= 1
            ? current.Substring(0, at) : current;
    }

    private static string ResolveYouLabel()
    {
        string label = AtlasBoardL.T("lobby.online.you_short");
        return string.IsNullOrWhiteSpace(label) ||
               label == "lobby.online.you_short" ? "YOU" : label;
    }


    private bool IsAuthoritativeLocalHostLobbySeat()
    {
        if (surface != AliasSurface.LocalHostLobbySeat) return true;

        if (Time.unscaledTime < nextLobbyAliasAuthorityCheck)
            return localHostLobbyAliasAllowed;
        nextLobbyAliasAuthorityCheck = Time.unscaledTime + 0.20f;
        localHostLobbyAliasAllowed = false;

        AtlasBoardPrivateLobbyUIController lobby =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardPrivateLobbyUIController>(FindObjectsInactive.Include);
        if (lobby == null || SeatNamesField == null || SnapshotField == null ||
            LocalAccountIdField == null || LocalIsHostField == null ||
            RoomActiveField == null)
            return false;

        if (!(RoomActiveField.GetValue(lobby) is bool roomActive) || !roomActive ||
            !(LocalIsHostField.GetValue(lobby) is bool localIsHost) || !localIsHost)
            return false;

        TMP_Text[] seatNames = SeatNamesField.GetValue(lobby) as TMP_Text[];
        if (seatNames == null) return false;

        int sourceSlot = -1;
        for (int i = 0; i < seatNames.Length; i++)
        {
            if (seatNames[i] == source)
            {
                sourceSlot = i;
                break;
            }
        }
        if (sourceSlot < 0) return false;

        AtlasLobbySnapshot snapshot = SnapshotField.GetValue(lobby) as AtlasLobbySnapshot;
        string localAccountId = LocalAccountIdField.GetValue(lobby) as string;
        if (snapshot == null || snapshot.Members == null ||
            string.IsNullOrWhiteSpace(localAccountId) ||
            !string.Equals(snapshot.HostAccountId, localAccountId,
                StringComparison.Ordinal))
            return false;

        int matches = 0;
        foreach (AtlasLobbyMemberSnapshot member in snapshot.Members)
        {
            if (member == null || !member.Active ||
                member.SlotIndex != sourceSlot ||
                !string.Equals(member.AccountId, localAccountId,
                    StringComparison.Ordinal) ||
                (member.SeatMode != AtlasLobbySeatMode.HostLocal && !member.IsHost))
                continue;
            matches++;
        }

        localHostLobbyAliasAllowed = matches == 1;
        return localHostLobbyAliasAllowed;
    }

    private void UpdateDisplay()
    {
        if (source == null) return;
        if (displayed == null) CreateDisplay();
        if (displayed == null) return;

        // Another owner may have reset TMP color. Keep exactly one visible
        // name without fighting over the original data-bearing TMP.text.
        Color current = source.color;
        if (current.a > 0.001f)
        {
            sourceColor = current;
            current.a = 0f;
            source.color = current;
        }

        if (displayed.font != source.font) displayed.font = source.font;
        if (!Mathf.Approximately(displayed.fontSize, source.fontSize))
            displayed.fontSize = source.fontSize;
        if (displayed.fontStyle != source.fontStyle)
            displayed.fontStyle = source.fontStyle;
        if (displayed.enableAutoSizing != source.enableAutoSizing)
            displayed.enableAutoSizing = source.enableAutoSizing;
        if (!Mathf.Approximately(displayed.fontSizeMin, source.fontSizeMin))
            displayed.fontSizeMin = source.fontSizeMin;
        if (!Mathf.Approximately(displayed.fontSizeMax, source.fontSizeMax))
            displayed.fontSizeMax = source.fontSizeMax;
        if (displayed.alignment != source.alignment)
            displayed.alignment = source.alignment;
        if (displayed.textWrappingMode != TextWrappingModes.NoWrap)
            displayed.textWrappingMode = TextWrappingModes.NoWrap;
        if (displayed.overflowMode != TextOverflowModes.Ellipsis)
            displayed.overflowMode = TextOverflowModes.Ellipsis;
        if (displayed.color != sourceColor) displayed.color = sourceColor;

        string name = WithoutLevel(source.text);
        string canonical = AtlasBoardCanonicalPlayerIdentity.CurrentDisplayName;

        // A Steam alias on a lobby row is allowed only while THIS exact TMP
        // belongs to the authenticated local host's authoritative snapshot slot.
        // Host migration changes authority, not seat identity. A stale P1 alias
        // therefore mirrors the authoritative Guest/source text instead of
        // repainting that row as Vuartex.
        bool canonicalAllowed = surface != AliasSurface.LocalHostLobbySeat ||
                                IsAuthoritativeLocalHostLobbySeat();
        if (canonicalAllowed && !string.IsNullOrWhiteSpace(canonical))
        {
            name = surface == AliasSurface.LocalHostLobbySeat
                ? canonical + " (" + ResolveYouLabel() + ")"
                : canonical;
        }

        // Keep the most recent valid presentation during momentary placeholder
        // changes; never infer a new Firebase identity.
        if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(lastName))
            name = lastName;
        if (!string.Equals(name, lastName, StringComparison.Ordinal))
        {
            lastName = name;
            displayed.text = name;
        }
    }
}
