#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play Mode only: test the existing AtlasBoardReleaseVersionBadge AFK path
/// without waiting five real minutes. No extra runtime manager or QA window.
/// </summary>
internal static class AtlasBoardMenuAfkQa
{
    private const string Command =
        "Atlas Board/Design/UI & UX/UX/AFK Test - Simulate 5 Minutes";

    [MenuItem(Command, false, 270)]
    private static void Simulate()
    {
        AtlasBoardReleaseVersionBadge[] badges =
            Object.FindObjectsByType<AtlasBoardReleaseVersionBadge>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AtlasBoardReleaseVersionBadge badge in badges)
        {
            if (badge == null || !badge.gameObject.scene.IsValid() ||
                !badge.isActiveAndEnabled) continue;
            if (badge.EditorSimulateFiveMinutesOfMenuIdle()) return;
        }
        EditorUtility.DisplayDialog("Atlas Board AFK QA",
            "AFK simulation requires Play Mode, a fully visible Main Menu, " +
            "a permitted game version and no active room, match, modal or pending transition.",
            "OK");
    }

    [MenuItem(Command, true)]
    private static bool Validate()
    {
        return EditorApplication.isPlaying;
    }
}
#endif
