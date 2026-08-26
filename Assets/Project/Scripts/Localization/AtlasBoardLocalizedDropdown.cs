using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Dropdown))]
public class AtlasBoardLocalizedDropdown :
    MonoBehaviour
{
    [SerializeField]
    private TMP_Dropdown dropdown;

    [SerializeField]
    private string[] optionKeys;

    public TMP_Dropdown Dropdown =>
        dropdown != null
            ? dropdown
            : GetComponent<TMP_Dropdown>();

    public string[] OptionKeys =>
        optionKeys;

    private void Awake()
    {
        Resolve();
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
        Resolve();

        if (dropdown == null ||
            optionKeys == null ||
            optionKeys.Length == 0)
        {
            return;
        }

        AtlasBoardLocalizationManager manager =
            AtlasBoardLocalizationManager.Instance;

        if (manager == null)
        {
            return;
        }

        int currentValue =
            dropdown.value;

        List<TMP_Dropdown.OptionData> options =
            new List<TMP_Dropdown.OptionData>();

        foreach (string optionKey
                 in optionKeys)
        {
            string value =
                optionKey != null &&
                optionKey.StartsWith(
                    "literal:")
                    ? optionKey.Substring(
                        "literal:".Length)
                    : manager.Translate(
                        optionKey);

            options.Add(
                new TMP_Dropdown.OptionData(
                    value));
        }

        dropdown.ClearOptions();
        dropdown.AddOptions(
            options);

        if (dropdown.options.Count > 0)
        {
            dropdown.SetValueWithoutNotify(
                Mathf.Clamp(
                    currentValue,
                    0,
                    dropdown.options.Count - 1));
        }

        dropdown.RefreshShownValue();

        ApplyFontAndSizing(
            dropdown.captionText,
            manager);

        ApplyFontAndSizing(
            dropdown.itemText,
            manager);
    }

    private void Resolve()
    {
        if (dropdown == null)
        {
            dropdown =
                GetComponent<TMP_Dropdown>();
        }
    }

    private static void ApplyFontAndSizing(
        TMP_Text text,
        AtlasBoardLocalizationManager manager)
    {
        if (text == null)
        {
            return;
        }

        TMP_FontAsset font =
            manager.ResolveFont(
                text.font);

        if (font != null &&
            font != text.font)
        {
            text.font =
                font;
        }

        float max =
            text.enableAutoSizing &&
            text.fontSizeMax > 0f
                ? text.fontSizeMax
                : Mathf.Max(
                    10f,
                    text.fontSize);

        text.enableAutoSizing =
            true;

        text.fontSizeMax =
            max;

        text.fontSizeMin =
            Mathf.Max(
                10f,
                max * 0.55f);
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        TMP_Dropdown target,
        string[] keys)
    {
        dropdown =
            target;

        optionKeys =
            keys;
    }
#endif
}
