using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class AtlasBoardUIButtonAudio : MonoBehaviour
{
    private Button button;
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

        button =
            GetComponent<Button>();

        if (button == null)
        {
            return;
        }

        button.onClick.AddListener(
            PlayClick);

        registered = true;
    }

    private void PlayClick()
    {
        AtlasBoardAudioManager.Instance
            ?.PlayUiClick();
    }
}
