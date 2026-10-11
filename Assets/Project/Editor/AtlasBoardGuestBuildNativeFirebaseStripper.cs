#if UNITY_EDITOR
using System;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Local two-client Guest QA must not boot the native Firebase desktop SDK.
/// The Guest already uses AtlasBoard's HTTP Auth/Functions emulator transport.
///
/// This processor changes only the temporary scene copy used by the canonical
/// AtlasBoard Guest build. Source scenes are not modified. Normal/production
/// builds keep the account/runtime Firebase components.
/// </summary>
public sealed class AtlasBoardGuestBuildNativeFirebaseStripper :
    IProcessSceneWithReport
{
    public int callbackOrder => -10000;

    public void OnProcessScene(
        Scene scene,
        BuildReport report)
    {
        if (!IsCanonicalGuestQaBuild(report))
        {
            return;
        }

        int accountServices =
            RemoveComponentsFromScene<AtlasBoardAccountService>(
                scene);

        int connectionTests =
            RemoveComponentsFromScene<AtlasBoardFirebaseConnectionTest>(
                scene);

        int authFirestoreTests =
            RemoveComponentsFromScene<AtlasBoardFirebaseAuthFirestoreTest>(
                scene);

        int backendHealthTests =
            RemoveComponentsFromScene<AtlasBoardBackendHealthCheckTest>(
                scene);

        Debug.Log(
            "AtlasBoard Guest QA native Firebase strip applied to build scene '" +
            scene.name + "'. Removed: AccountService=" +
            accountServices + ", ConnectionTest=" + connectionTests +
            ", AuthFirestoreTest=" + authFirestoreTests +
            ", BackendHealthTest=" + backendHealthTests + ". " +
            "Source scene was not modified.");
    }

    private static bool IsCanonicalGuestQaBuild(
        BuildReport report)
    {
        if (report == null)
        {
            return false;
        }

        string outputPath =
            report.summary.outputPath ??
            string.Empty;

        return
            outputPath.IndexOf(
                "AtlasBoard_3D3C_GuestBuild",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            outputPath.EndsWith(
                "AtlasBoardGuest.exe",
                StringComparison.OrdinalIgnoreCase);
    }

    private static int RemoveComponentsFromScene<T>(
        Scene scene)
        where T : Component
    {
        if (!scene.IsValid() ||
            !scene.isLoaded)
        {
            return 0;
        }

        int removed = 0;

        GameObject[] roots =
            scene.GetRootGameObjects();

        for (int rootIndex = 0;
             rootIndex < roots.Length;
             rootIndex++)
        {
            T[] components =
                roots[rootIndex]
                    .GetComponentsInChildren<T>(
                        true);

            for (int index = 0;
                 index < components.Length;
                 index++)
            {
                T component =
                    components[index];

                if (component == null)
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(
                    component,
                    true);

                removed++;
            }
        }

        return removed;
    }
}
#endif
