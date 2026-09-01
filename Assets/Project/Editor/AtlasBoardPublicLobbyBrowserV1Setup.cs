#if UNITY_EDITOR
using UnityEditor;

// Compatibility wrapper: the old Phase 4B menu now builds the newer unified
// Online Rooms UX so it cannot accidentally restore the obsolete bottom button.
public static class AtlasBoardPublicLobbyBrowserV1Setup
{
    [MenuItem("Atlas Board/Online/Build Public Lobby Browser v1", false, 470)]
    public static void Build()
    {
        AtlasBoardPublicLobbyBrowserV2Setup.Build();
    }

    [MenuItem("Atlas Board/Online/Validate Public Lobby Browser v1", false, 471)]
    public static void Validate()
    {
        AtlasBoardPublicLobbyBrowserV2Setup.Validate();
    }
}
#endif
