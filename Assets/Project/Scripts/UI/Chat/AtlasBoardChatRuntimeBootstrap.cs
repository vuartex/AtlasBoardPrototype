using UnityEngine;

public static class AtlasBoardChatRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureChatRuntime()
    {
        AtlasBoardChatUIController existing =
            Object.FindAnyObjectByType<
                AtlasBoardChatUIController>();

        if (existing != null)
        {
            return;
        }

        GameObject runtime =
            new GameObject(
                "AtlasBoard_ChatRuntime");

        Object.DontDestroyOnLoad(runtime);
        runtime.AddComponent<
            AtlasBoardChatRuntimeBridge>();
        runtime.AddComponent<
            AtlasBoardChatUIController>();
    }
}
