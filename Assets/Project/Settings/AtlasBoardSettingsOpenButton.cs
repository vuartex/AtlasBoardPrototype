using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class AtlasBoardSettingsOpenButton : MonoBehaviour
{
    [SerializeField]
    private AtlasBoardSettingsV2Controller settingsController;

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

    private void Hook()
    {
        if (hooked)
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
            OpenSettings);

        hooked = true;
    }

    private void OpenSettings()
    {
        AtlasBoardSettingsV2Controller controller =
            settingsController;

        if (controller == null)
        {
            controller =
                AtlasBoardSettingsV2Controller.Instance;
        }

        if (controller == null)
        {
            controller =
                FindControllerIncludingInactive();
        }

        if (controller == null)
        {
            Debug.LogWarning(
                "SET was clicked, but Canvas_Settings / AtlasBoardSettingsV2Controller was not found.");

            return;
        }

        controller.gameObject.SetActive(
            true);

        controller.OpenSettings();
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        AtlasBoardSettingsV2Controller controller)
    {
        settingsController =
            controller;
    }
#endif

    private static AtlasBoardSettingsV2Controller
        FindControllerIncludingInactive()
    {
        AtlasBoardSettingsV2Controller[] controllers =
            Resources.FindObjectsOfTypeAll<
                AtlasBoardSettingsV2Controller>();

        foreach (AtlasBoardSettingsV2Controller controller
                 in controllers)
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
