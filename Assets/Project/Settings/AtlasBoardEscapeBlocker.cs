using UnityEngine;

[DisallowMultipleComponent]
public class AtlasBoardEscapeBlocker : MonoBehaviour
{
    [SerializeField]
    private bool blocksSettingsEscape = true;

    public bool IsBlocking =>
        blocksSettingsEscape &&
        gameObject.activeInHierarchy;
}
