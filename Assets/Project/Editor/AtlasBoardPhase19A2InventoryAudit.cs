using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Read-only static guard. Does not simulate purchases or backend writes.
public static class AtlasBoardPhase19A2InventoryAudit
{
    [MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 19A2 - Career Inventory Smoke")]
    public static void Smoke()
    {
        Type pages = typeof(AtlasBoardProgressionUI).GetNestedType(
            "ProgressionPage", BindingFlags.NonPublic);
        bool inventoryTab = pages != null && pages.IsEnum &&
            Array.IndexOf(Enum.GetNames(pages), "Inventory") >= 0;
        bool tenFrames =
            AtlasBoardShopProfileFrameArt.IsSupported("frame_halloween") &&
            AtlasBoardShopProfileFrameArt.IsSupported("frame_new_year") &&
            AtlasBoardShopProfileFrameArt.IsSupported("frame_turkiye") &&
            AtlasBoardShopProfileFrameArt.IsSupported("frame_usa") &&
            AtlasBoardShopProfileFrameArt.IsSupported("frame_neon_circuit") &&
            AtlasBoardShopProfileFrameArt.IsSupported("frame_royal_crown") &&
            AtlasBoardShopProfileFrameArt.IsSupported("frame_ocean_tide") &&
            AtlasBoardShopProfileFrameArt.IsSupported("frame_sakura_blossom") &&
            AtlasBoardShopProfileFrameArt.IsSupported("frame_cosmic_nova") &&
            AtlasBoardShopProfileFrameArt.IsSupported("frame_forest_spirit");
        bool catalogs = AtlasBoardShopContentDraft.Count == 27 &&
                        AtlasBoardShopContentDraft.HasCompleteLanguages();
        bool accountApi = typeof(AtlasBoardAccountService).GetMethod(
            "UpdatePublicProfileAsync", new [] {typeof(string), typeof(string), typeof(string)}) != null;
        bool success = inventoryTab && tenFrames && catalogs && accountApi;
        string log = "ATB_PHASE19A2_INVENTORY_QA " + (success ? "PASS" : "FAIL") +
            " inventoryTab=" + inventoryTab + " frames=" + (tenFrames ? 10 : 0) +
            " items=" + AtlasBoardShopContentDraft.Count +
            " languages=" + AtlasBoardLocalizationLanguages.Codes.Length +
            " accountApi=" + accountApi +
            " livePurchaseQA=OPEN deviceQA=OPEN readOnly=true";
        if (success) Debug.Log(log); else Debug.LogError(log);
    }
}
