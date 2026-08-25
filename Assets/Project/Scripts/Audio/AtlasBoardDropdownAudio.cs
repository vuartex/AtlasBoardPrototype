using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Dropdown))]
public class AtlasBoardDropdownAudio : MonoBehaviour
{
    private TMP_Dropdown dropdown;
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

        dropdown =
            GetComponent<TMP_Dropdown>();

        if (dropdown == null)
        {
            return;
        }

        dropdown.onValueChanged.AddListener(
            OnValueChanged);

        registered = true;
    }

    private void OnValueChanged(
        int _)
    {
        AtlasBoardAudioManager.Instance
            ?.PlayUiSelect();
    }
}
