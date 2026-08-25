using UnityEngine;
using UnityEngine.UI;

public class ToggleProxy : MonoBehaviour
{
    [SerializeField] private Toggle toggle;

    public bool IsOn =>
        toggle != null &&
        toggle.isOn;

#if UNITY_EDITOR
    public void EditorConfigure(
        Toggle newToggle)
    {
        toggle = newToggle;
    }
#endif
}
