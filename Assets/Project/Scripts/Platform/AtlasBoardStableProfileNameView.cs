using System;
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
        if (!string.IsNullOrWhiteSpace(canonical))
        {
            name = surface == AliasSurface.LocalHostLobbySeat
                ? canonical + " (" + ResolveYouLabel() + ")"
                : canonical;
        }
        // Keep the most recent valid canonical presentation during momentary
        // placeholder changes; never infer a new Firebase identity.
        if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(lastName))
            name = lastName;
        if (!string.Equals(name, lastName, StringComparison.Ordinal))
        {
            lastName = name;
            displayed.text = name;
        }
    }
}
