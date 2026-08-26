using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class AtlasBoardLocalizedText :
    MonoBehaviour
{
    [SerializeField]
    private string localizationKey;

    [SerializeField]
    private TMP_Text targetText;

    [Header("Length Safety")]
    [SerializeField]
    private bool enableAutoSizing = true;

    [SerializeField, Range(0.35f, 1f)]
    private float minimumFontScale = 0.55f;

    [SerializeField]
    private float originalFontSize;

    public string LocalizationKey =>
        localizationKey;

    public TMP_Text TargetText =>
        targetText;

    private void Awake()
    {
        ResolveTarget();
        CaptureBaseFontSize();
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

        ApplyLayoutSafety(
            manager);
    }

    private void ResolveTarget()
    {
        if (targetText == null)
        {
            targetText =
                GetComponent<TMP_Text>();
        }
    }

    private void CaptureBaseFontSize()
    {
        if (targetText == null)
        {
            return;
        }

        if (originalFontSize <= 0f)
        {
            originalFontSize =
                Mathf.Max(
                    1f,
                    targetText.fontSize);
        }
    }

    private void ApplyLayoutSafety(
        AtlasBoardLocalizationManager manager)
    {
        CaptureBaseFontSize();

        TMP_FontAsset resolvedFont =
            manager.ResolveFont(
                targetText.font);

        if (resolvedFont != null &&
            resolvedFont != targetText.font)
        {
            targetText.font =
                resolvedFont;
        }

        if (!enableAutoSizing)
        {
            return;
        }

        float maxSize =
            Mathf.Max(
                8f,
                originalFontSize);

        float minSize =
            Mathf.Max(
                9f,
                maxSize *
                minimumFontScale);

        targetText.enableAutoSizing =
            true;

        targetText.fontSizeMax =
            maxSize;

        targetText.fontSizeMin =
            Mathf.Min(
                maxSize,
                minSize);
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        string key,
        TMP_Text text)
    {
        localizationKey =
            key;

        targetText =
            text;

        if (targetText != null)
        {
            originalFontSize =
                Mathf.Max(
                    1f,
                    targetText.fontSize);
        }
    }
#endif
}
