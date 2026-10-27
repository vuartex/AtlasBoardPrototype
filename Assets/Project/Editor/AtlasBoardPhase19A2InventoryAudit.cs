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
        bool fourFrames =
            AtlasBoardShopProfileFrameArt.IsSupported("frame_halloween") &&
            AtlasBoardShopProfileFrameArt.IsSupported("frame_new_year") &&
            AtlasBoardShopProfileFrameArt.IsSupported("frame_turkiye") &&
            AtlasBoardShopProfileFrameArt.IsSupported("frame_usa");
        bool catalogs = AtlasBoardShopContentDraft.Count == 21 &&
                        AtlasBoardShopContentDraft.HasCompleteLanguages();
        bool accountApi = typeof(AtlasBoardAccountService).GetMethod(
            "UpdatePublicProfileAsync", new [] {typeof(string), typeof(string), typeof(string)}) != null;
        bool success = inventoryTab && fourFrames && catalogs && accountApi;
        string log = "ATB_PHASE19A2_INVENTORY_QA " + (success ? "PASS" : "FAIL") +
            " inventoryTab=" + inventoryTab + " frames=" + (fourFrames ? 4 : 0) +
            " items=" + AtlasBoardShopContentDraft.Count +
            " languages=" + AtlasBoardLocalizationLanguages.Codes.Length +
            " accountApi=" + accountApi +
            " livePurchaseQA=OPEN deviceQA=OPEN readOnly=true";
        if (success) Debug.Log(log); else Debug.LogError(log);
    }
}
