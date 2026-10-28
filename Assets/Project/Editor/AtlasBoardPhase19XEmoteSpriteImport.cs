#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Asset import helper only: does not create a second runtime Emote/Shop manager.
public static class AtlasBoardPhase19XEmoteSpriteImport
{
    private const string SpriteFolder =
        "Assets/Project/ThirdParty/Kenney/Emotes/Sprites";

    [MenuItem("Atlas Board/QA & Diagnostics/Configure Kenney Emote Sprites")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("ATB_EMOTE_SPRITES Exit Play Mode before configuring sprites.");
            return;
        }
        if (!AssetDatabase.IsValidFolder(SpriteFolder))
        {
            Debug.LogError("ATB_EMOTE_SPRITES missing_folder=" + SpriteFolder);
            return;
        }
        var files = Directory.GetFiles(SpriteFolder, "*.png", SearchOption.TopDirectoryOnly);
        Array.Sort(files, StringComparer.Ordinal);
        int configured = 0;
        int unchanged = 0;
        int errors = 0;
        foreach (var diskPath in files)
        {
            var assetPath = diskPath.Replace('\\', '/');
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("ATB_EMOTE_SPRITES missing_texture_importer=" + assetPath);
                errors++;
                continue;
            }
            bool needsImport =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.mipmapEnabled ||
                !importer.alphaIsTransparency ||
                !importer.sRGBTexture ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.filterMode != FilterMode.Bilinear;
            if (!needsImport) { unchanged++; continue; }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            configured++;
        }
        Debug.Log("ATB_EMOTE_SPRITES status=" +
                  (errors == 0 && files.Length == 30 ? "ready" : "check") +
                  " count=" + files.Length + " configured=" + configured +
                  " unchanged=" + unchanged + " errors=" + errors +
                  " scope=assets_only runtime_emote_system=not_installed");
    }
}
#endif
