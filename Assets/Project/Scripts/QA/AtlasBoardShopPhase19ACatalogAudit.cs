#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardShopPhase19ACatalogAudit
{
    private static int GetCount(Dictionary<string, int> categories, string key)
    {
        return categories.TryGetValue(key, out int count) ? count : 0;
    }

    [MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 19A - Shop Draft Catalog Smoke")]
    public static void Run()
    {
        List<AtlasBoardStorefrontItem> rows =
            AtlasBoardShopContentDraft.CreatePreview();
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, int> categories = new Dictionary<string, int>(StringComparer.Ordinal);
        int invalid = 0;
        foreach (AtlasBoardStorefrontItem item in rows)
        {
            if (!ids.Add(item.ItemId) || !item.PreviewOnly || item.Owned ||
                item.GoldPrice <= 0 || item.AtlasCoinPrice != 0)
                invalid++;
            if (!categories.ContainsKey(item.ItemType)) categories[item.ItemType] = 0;
            categories[item.ItemType]++;
            foreach (string code in AtlasBoardLocalizationLanguages.Codes)
                if (string.IsNullOrWhiteSpace(
                    AtlasBoardShopContentDraft.LocalizedName(item.ItemId, code))) invalid++;
        }
        invalid += !AtlasBoardShopContentDraft.HasCompleteLanguages() ? 1 : 0;
        invalid += GetCount(categories, "board_theme") != 3 ? 1 : 0;
        invalid += GetCount(categories, "dice_skin") != 6 ? 1 : 0;
        invalid += GetCount(categories, "pawn") != 4 ? 1 : 0;
        invalid += GetCount(categories, "profile_frame") != 4 ? 1 : 0;
        invalid += GetCount(categories, "emote") != 4 ? 1 : 0;
        invalid += categories.ContainsKey("animation_pack") ? 1 : 0;
        foreach (string id in new [] {
            "frame_halloween", "frame_new_year", "frame_turkiye", "frame_usa" })
            if (!AtlasBoardShopProfileFrameArt.IsSupported(id)) invalid++;
        // Existing server product must suppress only its matching local draft.
        AtlasBoardStorefrontItem trusted = new AtlasBoardStorefrontItem {
            ItemId = "frame_halloween", ItemType = "profile_frame",
            DisplayName = "Trusted", Owned = true,
            PreviewOnly = false, GoldPrice = 999 };
        List<AtlasBoardStorefrontItem> merged =
            AtlasBoardShopContentDraft.MergeForDisplay(new [] { trusted });
        invalid += merged.Count != 21 ? 1 : 0;
        invalid += !object.ReferenceEquals(merged[0], trusted) ? 1 : 0;
        invalid += merged.FindAll(x => x.ItemId == "frame_halloween").Count != 1 ? 1 : 0;
        invalid += merged.FindAll(x => x.PreviewOnly).Count != 20 ? 1 : 0;
        if (invalid == 0 && rows.Count == 21)
            Debug.Log("ATB_PHASE19A_CATALOG_QA PASS items=21 boards=3 dice=6 pawns=4 frames=4 emotes=4 languages=7 currency=gold previewOnly=true backendPurchase=OPEN profileArt=4 mergedCatalog=true");
        else
            Debug.LogError("ATB_PHASE19A_CATALOG_QA FAIL invalid=" + invalid + " items=" + rows.Count);
    }
}
#endif
