using TMPro;
using UnityEngine;

[CreateAssetMenu(
    fileName = "LocalizationFonts_Default",
    menuName = "Atlas Board/Localization/Font Profile")]
public class AtlasBoardLocalizationFontProfile :
    ScriptableObject
{
    [Tooltip(
        "Recommended: Noto Sans TMP font asset. " +
        "Covers Latin Extended and Cyrillic.")]
    [SerializeField]
    private TMP_FontAsset latinCyrillicFont;

    [Tooltip(
        "Recommended: Noto Sans KR TMP font asset.")]
    [SerializeField]
    private TMP_FontAsset koreanFont;

    [Header("Legacy Unity UI.Text")]
    [Tooltip(
        "Source Noto Sans font used by any remaining legacy UI.Text labels.")]
    [SerializeField]
    private Font latinCyrillicLegacyFont;

    [Tooltip(
        "Source Noto Sans KR font used by any remaining legacy UI.Text labels.")]
    [SerializeField]
    private Font koreanLegacyFont;

    public TMP_FontAsset LatinCyrillicFont =>
        latinCyrillicFont;

    public TMP_FontAsset KoreanFont =>
        koreanFont;

    public Font LatinCyrillicLegacyFont =>
        latinCyrillicLegacyFont;

    public Font KoreanLegacyFont =>
        koreanLegacyFont;

    public Font GetLegacyFont(
        string languageCode,
        Font currentFont)
    {
        string normalized =
            AtlasBoardLocalizationLanguages.Normalize(
                languageCode);

        if (normalized == "ko" &&
            koreanLegacyFont != null)
        {
            return koreanLegacyFont;
        }

        if (latinCyrillicLegacyFont != null)
        {
            return latinCyrillicLegacyFont;
        }

        return currentFont;
    }

    public TMP_FontAsset GetFont(
        string languageCode,
        TMP_FontAsset currentFont)
    {
        string normalized =
            AtlasBoardLocalizationLanguages.Normalize(
                languageCode);

        if (normalized == "ko" &&
            koreanFont != null)
        {
            return koreanFont;
        }

        if (latinCyrillicFont != null)
        {
            return latinCyrillicFont;
        }

        return currentFont;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        TMP_FontAsset latinFont,
        TMP_FontAsset koreanFallback)
    {
        latinCyrillicFont =
            latinFont;

        koreanFont =
            koreanFallback;
    }

    public void EditorConfigureLegacyFonts(
        Font latinFont,
        Font koreanFontSource)
    {
        latinCyrillicLegacyFont =
            latinFont;

        koreanLegacyFont =
            koreanFontSource;
    }
#endif
}
