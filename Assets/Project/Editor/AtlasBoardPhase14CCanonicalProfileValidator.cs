#if UNITY_EDITOR
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase14CCanonicalProfileValidator
{
    [MenuItem(
        "Atlas Board/Phases/Phase 14/14C - Profile Header/Validate Canonical Profile Header")]
    public static void Validate()
    {
        FieldInfo field =
            typeof(
                AtlasBoardMainMenuController)
                .GetField(
                    "profileName",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

        if (field == null)
        {
            Debug.LogError(
                "Phase 14C validation FAILED: " +
                "AtlasBoardMainMenuController.profileName was not found.");

            return;
        }

        AtlasBoardMainMenuController controller =
            Object.FindAnyObjectByType<
                AtlasBoardMainMenuController>();

        if (controller == null)
        {
            Debug.LogError(
                "Phase 14C validation FAILED: " +
                "AtlasBoardMainMenuController is missing from the open scene.");

            return;
        }

        Transform mainMenu =
            FindChildRecursive(
                controller.transform,
                "MainMenu");

        Transform profileCard =
            FindChildRecursive(
                mainMenu,
                "ProfileCard");

        TMP_Text profileName =
            FindChildRecursive(
                profileCard,
                "ProfileName")
            ?.GetComponent<TMP_Text>();

        TMP_Text avatarInitial =
            FindChildRecursive(
                profileCard,
                "AvatarInitial")
            ?.GetComponent<TMP_Text>();

        if (profileName == null ||
            avatarInitial == null)
        {
            Debug.LogError(
                "Phase 14C validation FAILED: " +
                "MainMenu/ProfileCard/ProfileName or AvatarInitial " +
                "could not be found.");

            return;
        }

        Debug.Log(
            "Phase 14C source/scene validation PASS. " +
            "Canonical account identity can bind to the existing " +
            "top-left ProfileCard without rebuilding Main Menu UI.");
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14C - Profile Header/Refresh Canonical Profile Now (Play Mode)")]
    public static void RefreshNow()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "Enter Play Mode before refreshing the canonical profile.");

            return;
        }

        AtlasBoardCanonicalProfileHeader binder =
            Object.FindAnyObjectByType<
                AtlasBoardCanonicalProfileHeader>();

        if (binder == null)
        {
            Debug.LogError(
                "Phase 14C runtime profile binder was not found.");

            return;
        }

        binder.RefreshNow();

        Debug.Log(
            "Phase 14C canonical profile refresh requested.");
    }

    private static Transform FindChildRecursive(
        Transform root,
        string childName)
    {
        if (root == null)
        {
            return null;
        }

        for (int index = 0;
             index < root.childCount;
             index++)
        {
            Transform child =
                root.GetChild(
                    index);

            if (child.name ==
                childName)
            {
                return child;
            }

            Transform nested =
                FindChildRecursive(
                    child,
                    childName);

            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
#endif
