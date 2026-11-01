using System.Collections.Generic;
using UnityEngine;

// One registry asset refers to the original imported Kenney sprites. No duplicate PNGs.
public sealed class AtlasBoardEmoteSpriteCatalog : ScriptableObject
{
    public List<AtlasBoardEmoteSpriteEntry> entries = new List<AtlasBoardEmoteSpriteEntry>();
    public Sprite Find(string id)
    {
        foreach (var entry in entries)
            if (entry != null && entry.id == id) return entry.sprite;
        return null;
    }
}

