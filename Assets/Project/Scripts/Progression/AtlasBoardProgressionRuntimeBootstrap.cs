using UnityEngine;

public static class AtlasBoardProgressionRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntime()
    {
        AtlasBoardProgressionBridge[] existing =
            Resources.FindObjectsOfTypeAll<AtlasBoardProgressionBridge>();
        foreach (AtlasBoardProgressionBridge item in existing)
        {
            if (item != null)
            {
                return;
            }
        }

        GameObject runtime = new GameObject("AtlasBoardProgressionRuntime");
        Object.DontDestroyOnLoad(runtime);
        runtime.AddComponent<AtlasBoardProgressionBridge>();
        runtime.AddComponent<AtlasBoardProgressionMatchRecorder>();
        runtime.AddComponent<AtlasBoardProgressionUI>();
    }
}
