using UnityEngine;

public static class AtlasBoardMetaEconomyRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntime()
    {
        AtlasBoardMetaEconomyUI existing =
            Object.FindAnyObjectByType<AtlasBoardMetaEconomyUI>();

        if (existing != null)
        {
            return;
        }

        GameObject runtime =
            new GameObject("AtlasBoard_MetaEconomyRuntime");

        Object.DontDestroyOnLoad(runtime);
        runtime.AddComponent<AtlasBoardMetaEconomyBridge>();
        runtime.AddComponent<AtlasBoardMetaEconomyUI>();
    }
}
