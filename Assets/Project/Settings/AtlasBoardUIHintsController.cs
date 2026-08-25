using TMPro;
using UnityEngine;

[DefaultExecutionOrder(22000)]
[DisallowMultipleComponent]
public class AtlasBoardUIHintsController : MonoBehaviour
{
    [SerializeField]
    private GameObject shortcutHintObject;

    private void Awake()
    {
        ResolveHint();
    }

    private void LateUpdate()
    {
        ResolveHint();

        if (shortcutHintObject == null)
        {
            return;
        }

        bool shouldShow =
            AtlasBoardUserSettingsRuntime
                .UiHintsEnabled;

        TMP_Text hintText =
            shortcutHintObject.GetComponent<
                TMP_Text>();

        bool hasText =
            hintText != null &&
            !string.IsNullOrWhiteSpace(
                hintText.text);

        // UXOverlayController can change visibility earlier in the same frame.
        // This component intentionally runs afterwards and enforces the
        // user's preference in both directions.
        bool targetVisible =
            shouldShow &&
            hasText;

        if (shortcutHintObject.activeSelf !=
            targetVisible)
        {
            shortcutHintObject.SetActive(
                targetVisible);
        }
    }

    private void ResolveHint()
    {
        if (shortcutHintObject != null)
        {
            return;
        }

        Transform hint =
            FindRecursive(
                transform,
                "ShortcutHint");

        if (hint != null)
        {
            shortcutHintObject =
                hint.gameObject;
        }
    }

    private static Transform FindRecursive(
        Transform root,
        string targetName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root)
        {
            if (child.name ==
                targetName)
            {
                return child;
            }

            Transform nested =
                FindRecursive(
                    child,
                    targetName);

            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
