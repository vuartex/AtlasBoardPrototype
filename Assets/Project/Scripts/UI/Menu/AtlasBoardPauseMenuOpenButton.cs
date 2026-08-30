using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class AtlasBoardPauseMenuOpenButton : MonoBehaviour
{
    [SerializeField]
    private AtlasBoardLeaveFlowController leaveFlowController;

    private Button button;
    private bool hooked;

    private void Awake()
    {
        Hook();
    }

    private void OnEnable()
    {
        Hook();
    }

    private void OnDestroy()
    {
        if (hooked && button != null)
        {
            button.onClick.RemoveListener(OpenPauseMenu);
        }

        hooked = false;
    }

    private void Hook()
    {
        if (hooked)
        {
            return;
        }

        button = GetComponent<Button>();

        if (button == null)
        {
            return;
        }

        button.onClick.AddListener(OpenPauseMenu);
        hooked = true;
    }

    private void OpenPauseMenu()
    {
        AtlasBoardLeaveFlowController controller =
            leaveFlowController;

        if (controller == null)
        {
            controller = FindControllerIncludingInactive();
        }

        if (controller == null)
        {
            Debug.LogWarning(
                "MENU was clicked, but AtlasBoardLeaveFlowController was not found.",
                this);
            return;
        }

        controller.OpenPauseMenu();
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        AtlasBoardLeaveFlowController controller)
    {
        leaveFlowController = controller;
    }
#endif

    private static AtlasBoardLeaveFlowController
        FindControllerIncludingInactive()
    {
        AtlasBoardLeaveFlowController[] controllers =
            Resources.FindObjectsOfTypeAll<AtlasBoardLeaveFlowController>();

        foreach (AtlasBoardLeaveFlowController controller in controllers)
        {
            if (controller == null ||
                !controller.gameObject.scene.IsValid())
            {
                continue;
            }

            return controller;
        }

        return null;
    }
}
