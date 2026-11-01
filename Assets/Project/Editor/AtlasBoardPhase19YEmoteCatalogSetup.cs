#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

[InitializeOnLoad]
public sealed class AtlasBoardPhase19YEmoteCatalogSetup : IPreprocessBuildWithReport
{
    private const string Root = "Assets/Project/ThirdParty/Kenney/Emotes/Sprites/";
    private const string AssetPath = "Assets/Project/Resources/AtlasBoard/EmoteSpriteCatalog.asset";
    private static bool preparing;
    public int callbackOrder => -100;

    static AtlasBoardPhase19YEmoteCatalogSetup()
    {
        EditorApplication.delayCall += EnsurePersistentCatalog;
    }

    private static void EnsurePersistentCatalog()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || preparing) return;
        var existing = AssetDatabase.LoadAssetAtPath<AtlasBoardEmoteSpriteCatalog>(AssetPath);
        if (CatalogComplete(existing)) return;
        Build();
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        EnsurePersistentCatalog();
        var asset = AssetDatabase.LoadAssetAtPath<AtlasBoardEmoteSpriteCatalog>(AssetPath);
        if (!CatalogComplete(asset))
            throw new BuildFailedException("AtlasBoard Emote Sprite Catalog is not ready. " +
                "Import Kenney/Emotes sprites then use Build Emote Runtime Catalog.");
    }
    private static bool CatalogComplete(AtlasBoardEmoteSpriteCatalog asset)
    {
        if (asset == null || asset.entries == null ||
            asset.entries.Count != AtlasBoardEmotes.All.Count) return false;
        foreach (var entry in asset.entries)
            if (entry == null || entry.sprite == null ||
                string.IsNullOrWhiteSpace(entry.id)) return false;
        return true;
    }

    [MenuItem("Atlas Board/QA & Diagnostics/Build Emote Runtime Catalog")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("Exit Play Mode before rebuilding emote catalog."); return; }
        if (preparing) return;
        preparing = true;
        try
        {
        Directory.CreateDirectory("Assets/Project/Resources/AtlasBoard");
        AtlasBoardEmoteSpriteCatalog catalog = AssetDatabase.LoadAssetAtPath<AtlasBoardEmoteSpriteCatalog>(AssetPath);
        if (catalog == null)
        {
            // A legacy asset could have a Missing Script because the
            // ScriptableObject class was declared in AtlasBoardEmotes.cs.
            // Rebuild only this generated registry, never the Kenney sprites.
            if (File.Exists(AssetPath))
                AssetDatabase.DeleteAsset(AssetPath);
            catalog = ScriptableObject.CreateInstance<AtlasBoardEmoteSpriteCatalog>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
        }
        var resolved = new List<AtlasBoardEmoteSpriteEntry>();
        int missing = 0;
        foreach (var emote in AtlasBoardEmotes.All)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + emote.Id + ".png");
            if (sprite == null) { missing++; Debug.LogError("Missing Kenney sprite: " + emote.Id); continue; }
            resolved.Add(new AtlasBoardEmoteSpriteEntry { id = emote.Id, sprite = sprite });
        }
        // Don't wipe a previously usable catalog when a texture is temporarily
        // missing or importer is in progress.
        if (missing == 0)
        {
            catalog.entries.Clear();
            catalog.entries.AddRange(resolved);
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("ATB_EMOTES_RUNTIME status=" + (missing == 0 ? "ready" : "missing") +
                  " sprites=" + catalog.entries.Count + " missing=" + missing);
        }
        finally { preparing = false; }
    }
}
#endif
