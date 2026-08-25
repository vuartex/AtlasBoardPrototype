using TMPro;
using UnityEngine;

[DefaultExecutionOrder(21000)]
[DisallowMultipleComponent]
public class AtlasBoardFPSDisplay : MonoBehaviour
{
    [SerializeField]
    private TMP_Text fpsText;

    [SerializeField, Min(0.1f)]
    private float refreshInterval = 0.5f;

    private float elapsed;
    private int frames;

    private void Awake()
    {
        if (fpsText == null)
        {
            fpsText =
                GetComponent<TMP_Text>();
        }
    }

    private void Update()
    {
        bool shouldShow =
            AtlasBoardUserSettingsRuntime.ShowFps;

        if (fpsText != null &&
            fpsText.enabled !=
            shouldShow)
        {
            fpsText.enabled =
                shouldShow;
        }

        if (!shouldShow ||
            fpsText == null)
        {
            return;
        }

        elapsed +=
            Time.unscaledDeltaTime;

        frames++;

        if (elapsed <
            refreshInterval)
        {
            return;
        }

        float fps =
            frames /
            Mathf.Max(
                0.0001f,
                elapsed);

        fpsText.text =
            $"FPS {Mathf.RoundToInt(fps)}";

        elapsed = 0f;
        frames = 0;
    }
}
