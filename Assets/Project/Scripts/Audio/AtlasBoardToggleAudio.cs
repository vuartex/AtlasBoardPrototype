using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Toggle))]
public class AtlasBoardToggleAudio : MonoBehaviour
{
    private Toggle toggle;
    private bool registered;

    private void Awake()
    {
        Register();
    }

    private void OnEnable()
    {
        Register();
    }

    private void Register()
    {
        if (registered)
        {
            return;
        }

        toggle =
            GetComponent<Toggle>();

        if (toggle == null)
        {
            return;
        }

        toggle.onValueChanged.AddListener(
            OnValueChanged);

        registered = true;
    }

    private void OnValueChanged(
        bool _)
    {
        AtlasBoardAudioManager.Instance
            ?.PlayUiToggle();
    }
}
