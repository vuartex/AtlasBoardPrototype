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
            EnsureSafetyComponents(existing.gameObject);
            return;
        }

        GameObject runtime =
            new GameObject(
                "AtlasBoard_ChatRuntime");

        Object.DontDestroyOnLoad(runtime);
        runtime.AddComponent<
            AtlasBoardChatRuntimeBridge>();
        runtime.AddComponent<
            AtlasBoardChatModerationBridge>();
        runtime.AddComponent<
            AtlasBoardChatUIController>();
        runtime.AddComponent<
            AtlasBoardChatSafetyUIController>();
    }

    private static void EnsureSafetyComponents(GameObject runtime)
    {
        if (runtime == null)
        {
            return;
        }

        if (runtime.GetComponent<AtlasBoardChatModerationBridge>() == null)
        {
            runtime.AddComponent<AtlasBoardChatModerationBridge>();
        }

        if (runtime.GetComponent<AtlasBoardChatSafetyUIController>() == null)
        {
            runtime.AddComponent<AtlasBoardChatSafetyUIController>();
        }
    }
}
