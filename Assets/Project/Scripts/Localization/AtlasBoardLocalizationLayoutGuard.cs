using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class AtlasBoardLocalizationLayoutGuard :
    MonoBehaviour
{
    [SerializeField, Range(0.35f, 1f)]
    private float minimumFontScale = 0.55f;

    [SerializeField]
    private float originalFontSize;

    private TMP_Text text;

    private void Awake()
    {
        Apply();
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
        text =
            text != null
                ? text
                : GetComponent<TMP_Text>();

        if (text == null)
        {
            return;
        }

        if (originalFontSize <= 0f)
        {
            originalFontSize =
                Mathf.Max(
                    1f,
                    text.fontSize);
        }

        AtlasBoardLocalizationManager manager =
            AtlasBoardLocalizationManager.Instance;

        if (manager != null)
        {
            TMP_FontAsset font =
                manager.ResolveFont(
                    text.font);

            if (font != null &&
                font != text.font)
            {
                text.font =
                    font;
            }
        }

        float maxSize =
            Mathf.Max(
                8f,
                originalFontSize);

        text.enableAutoSizing =
            true;

        text.fontSizeMax =
            maxSize;

        text.fontSizeMin =
            Mathf.Max(
                9f,
                maxSize *
                minimumFontScale);
    }

#if UNITY_EDITOR
    public void EditorCapture(
        TMP_Text target)
    {
        text =
            target;

        if (target != null)
        {
            originalFontSize =
                Mathf.Max(
                    1f,
                    target.fontSize);
        }
    }
#endif
}
