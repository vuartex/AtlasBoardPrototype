using UnityEngine;

/// <summary>
/// Phase 5B v1.0.2 local two-client E2E polish.
/// Keeps Editor/standalone gameplay and Firebase polling alive while another
/// Atlas Board client owns desktop focus during Alt+Tab testing.
/// </summary>
public static class AtlasBoardRunInBackgroundBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnableRunInBackground()
    {
        Application.runInBackground = true;

        Debug.Log(
            "AtlasBoard Phase 5B v1.0.2: Application.runInBackground=true. " +
            "This client will continue its player loop while unfocused.");
    }
}
