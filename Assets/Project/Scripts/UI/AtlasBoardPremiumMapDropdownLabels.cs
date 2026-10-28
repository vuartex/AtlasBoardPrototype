using TMPro;
using UnityEngine;

/// <summary>
/// Repair and localize only Germany/Mexico slots on the EXISTING lobby dropdown.
/// Index 0/1/2 remains Turkey/Colorado/USA. No second UI or map manager.
/// </summary>
[DisallowMultipleComponent]
public sealed class AtlasBoardPremiumMapDropdownLabels : MonoBehaviour
{
    private TMP_Dropdown dropdown;

    public static bool EnsureOptions(TMP_Dropdown target)
    {
        if (target == null || target.options == null || target.options.Count < 3)
            return false;
        // Existing three options must remain at their stable indices.
        // If an older scene has already been repaired, this is idempotent.
        if (target.options.Count == 3)
            target.options.Add(new TMP_Dropdown.OptionData("Germany"));
        if (target.options.Count == 4)
            target.options.Add(new TMP_Dropdown.OptionData("Mexico"));
        if (target.options.Count < 5)
            return false;

        string locale = AtlasBoardLocalizationManager.Instance != null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";
        target.options[3].text = AtlasBoardShopContentDraft.LocalizedName("map_germany", locale);
        target.options[4].text = AtlasBoardShopContentDraft.LocalizedName("map_mexico", locale);
        target.RefreshShownValue();
        return true;
    }

    private void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();
        EnsureOptions(dropdown);
    }

    private void OnEnable()
    {
        AtlasBoardLocalizationManager.LanguageChanged += Refresh;
        if (dropdown == null) dropdown = GetComponent<TMP_Dropdown>();
        if (dropdown != null) dropdown.onValueChanged.AddListener(PreviewSelection);
        Refresh();
    }

    private void OnDisable()
    {
        AtlasBoardLocalizationManager.LanguageChanged -= Refresh;
        if (dropdown != null) dropdown.onValueChanged.RemoveListener(PreviewSelection);
    }

    private void PreviewSelection(int index)
    {
        if (index >= 0 && index <= 4)
            PreviewMap(index == 3 ? "Germany" : index == 4 ? "Mexico" :
                index == 2 ? "USA" : index == 1 ? "Colorado" : "Turkey");
    }

    public static void PreviewMap(string mapId)
    {
        var setup = UnityEngine.Object.FindAnyObjectByType<MatchSetupManager>();
        if (setup != null) setup.PreviewLobbyMapById(mapId);
    }

    private void Refresh()
    {
        if (dropdown == null) dropdown = GetComponent<TMP_Dropdown>();
        EnsureOptions(dropdown);
    }
}
