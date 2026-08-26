using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Text))]
public class AtlasBoardLocalizedLegacyText :
    MonoBehaviour
{
    [SerializeField]
    private string localizationKey;

    [SerializeField]
    private Text targetText;

    [SerializeField]
    private int originalFontSize;

    public string LocalizationKey =>
        localizationKey;

    public Text TargetText =>
        targetText;

    private void Awake()
    {
        ResolveTarget();
        CaptureFontSize();
    }

    private void OnEnable()
    {
        AtlasBoardLocalizationManager
            .LanguageChanged +=
                Apply;

        Apply();
    }

    private void OnDisable()
    {
        AtlasBoardLocalizationManager
            .LanguageChanged -=
                Apply;
    }

    public void Apply()
    {
        ResolveTarget();

        if (targetText == null ||
            string.IsNullOrWhiteSpace(
                localizationKey))
        {
            return;
        }

        AtlasBoardLocalizationManager manager =
            AtlasBoardLocalizationManager.Instance;

        if (manager == null)
        {
            return;
        }

        targetText.text =
            manager.Translate(
                localizationKey);

        Font resolvedFont =
            manager.ResolveLegacyFont(
                targetText.font);

        if (resolvedFont != null &&
            resolvedFont != targetText.font)
        {
            targetText.font =
                resolvedFont;
        }

        CaptureFontSize();

        // Legacy board-control buttons have fixed RectTransforms.
        // Best Fit lets German/Russian/Korean labels shrink safely.
        targetText.resizeTextForBestFit =
            true;

        targetText.resizeTextMaxSize =
            Mathf.Max(
                8,
                originalFontSize);

        targetText.resizeTextMinSize =
            Mathf.Max(
                8,
                Mathf.RoundToInt(
                    originalFontSize *
                    0.55f));
    }

    private void ResolveTarget()
    {
        if (targetText == null)
        {
            targetText =
                GetComponent<Text>();
        }
    }

    private void CaptureFontSize()
    {
        if (targetText == null)
        {
            return;
        }

        if (originalFontSize <= 0)
        {
            originalFontSize =
                Mathf.Max(
                    8,
                    targetText.fontSize);
        }
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        string key,
        Text text)
    {
        localizationKey =
            key;

        targetText =
            text;

        if (targetText != null)
        {
            originalFontSize =
                Mathf.Max(
                    8,
                    targetText.fontSize);
        }
    }
#endif
}
