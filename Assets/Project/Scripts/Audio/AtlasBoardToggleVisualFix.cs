using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Toggle))]
public class AtlasBoardToggleVisualFix : MonoBehaviour
{
    private Toggle toggle;

    private static readonly Color White =
        Color.white;

    private static readonly Color Hover =
        new Color32(
            235,
            247,
            252,
            255);

    private static readonly Color Pressed =
        new Color32(
            218,
            237,
            246,
            255);

    private static readonly Color Disabled =
        new Color32(
            225,
            225,
            225,
            255);

    private static readonly Color Checked =
        new Color32(
            134,
            176,
            0,
            255);

    private void Awake()
    {
        ApplyStyle();
    }

    private void OnEnable()
    {
        ApplyStyle();
    }

    public void ApplyStyle()
    {
        toggle =
            GetComponent<Toggle>();

        if (toggle == null)
        {
            return;
        }

        Graphic background =
            toggle.targetGraphic;

        if (background != null)
        {
            background.color =
                White;

            // The Lobby checkbox background was created with a grey Kenney
            // sprite. Tinting that sprite white still leaves it visibly grey.
            // For this control, OFF should be unambiguously white.
            Image backgroundImage =
                background as Image;

            if (backgroundImage != null)
            {
                backgroundImage.sprite =
                    null;

                backgroundImage.type =
                    Image.Type.Simple;

                backgroundImage.color =
                    White;
            }
        }

        ColorBlock colors =
            toggle.colors;

        colors.normalColor =
            White;

        colors.highlightedColor =
            Hover;

        colors.pressedColor =
            Pressed;

        // Important: an unchecked focused Toggle must still look white,
        // not grey. The green check graphic communicates the ON state.
        colors.selectedColor =
            White;

        colors.disabledColor =
            Disabled;

        toggle.colors =
            colors;

        if (toggle.graphic != null)
        {
            toggle.graphic.color =
                Checked;
        }
    }
}
