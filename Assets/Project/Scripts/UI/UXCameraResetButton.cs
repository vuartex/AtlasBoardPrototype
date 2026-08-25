using UnityEngine;
using UnityEngine.UI;

public class UXCameraResetButton : MonoBehaviour
{
    [SerializeField]
    private Button button;

    [SerializeField]
    private BoardCameraController
        cameraController;

    [SerializeField]
    private Image iconImage;

    private void Awake()
    {
        if (button == null)
        {
            button =
                GetComponent<Button>();
        }

        if (cameraController == null)
        {
            cameraController =
                FindAnyObjectByType<
                    BoardCameraController>();
        }

        if (button != null)
        {
            button.onClick
                .AddListener(
                    ResetCamera);
        }

        if (iconImage != null)
        {
            iconImage.preserveAspect = true;
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick
                .RemoveListener(
                    ResetCamera);
        }
    }

    public void ResetCamera()
    {
        if (cameraController == null)
        {
            cameraController =
                FindAnyObjectByType<
                    BoardCameraController>();
        }

        if (cameraController == null)
        {
            Debug.LogWarning(
                "Camera reset requested, but no " +
                "BoardCameraController was found.",
                this);

            return;
        }

        cameraController.ResetView();
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        Button newButton,
        BoardCameraController
            newCameraController,
        Image newIconImage)
    {
        button = newButton;
        cameraController =
            newCameraController;
        iconImage =
            newIconImage;
    }
#endif
}
