#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public static class AtlasBoardPhase19YEmoteCatalogSetup
{
    private const string Root = "Assets/Project/ThirdParty/Kenney/Emotes/Sprites/";
    private const string AssetPath = "Assets/Project/Resources/AtlasBoard/EmoteSpriteCatalog.asset";
    [MenuItem("Atlas Board/QA & Diagnostics/Build Emote Runtime Catalog")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Exit Play Mode before rebuilding emote catalog."); return; }
        Directory.CreateDirectory("Assets/Project/Resources/AtlasBoard");
        AssetDatabase.Refresh();
        AtlasBoardEmoteSpriteCatalog catalog = AssetDatabase.LoadAssetAtPath<AtlasBoardEmoteSpriteCatalog>(AssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<AtlasBoardEmoteSpriteCatalog>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
        }
        catalog.entries.Clear();
        int missing = 0;
        foreach (var emote in AtlasBoardEmotes.All)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + emote.Id + ".png");
            if (sprite == null) { missing++; Debug.LogError("Missing Kenney sprite: " + emote.Id); continue; }
            catalog.entries.Add(new AtlasBoardEmoteSpriteEntry { id = emote.Id, sprite = sprite });
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("ATB_EMOTES_RUNTIME status=" + (missing == 0 ? "ready" : "missing") +
                  " sprites=" + catalog.entries.Count + " missing=" + missing);
    }
}
#endif
