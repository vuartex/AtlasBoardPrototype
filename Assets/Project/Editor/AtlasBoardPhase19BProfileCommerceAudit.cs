using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Static read-only contracts. Does not imply cloud purchase authorization.
public static class AtlasBoardPhase19BProfileCommerceAudit
{
    [MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 19B - Profile Commerce Contract Smoke")]
    public static void Smoke()
    {
        var rows = AtlasBoardShopContentDraft.CreatePreview();
        int frames = rows.Count(x => x.ItemType == "profile_frame");
        int dice = rows.Count(x => x.ItemType == "dice_skin");
        bool distinct = rows.Select(x => x.ItemId).Distinct().Count() == rows.Count;
        bool gated = rows.All(x => x.PreviewOnly && !x.Owned);
        bool methods = typeof(AtlasBoardMetaEconomyBridge).GetMethod("PurchaseAsync") != null &&
                       typeof(AtlasBoardAccountService).GetMethod("UpdatePublicProfileAsync") != null;
        bool pass = frames == 10 && dice == 6 && distinct && gated &&
                    rows.Count == 27 && AtlasBoardShopContentDraft.HasCompleteLanguages() && methods;
        string text = "ATB_PHASE19B_PROFILE_QA " + (pass ? "PASS" : "FAIL") +
            " items=" + rows.Count + " frames=" + frames + " diceDrafts=" + dice +
            " unique=" + distinct + " previewGated=" + gated +
            " languages=" + AtlasBoardLocalizationLanguages.Codes.Length +
            " emulatorPurchaseQA=OPEN liveCommerce=BLOCKED assetImport=OPEN readOnly=true";
        if (pass) Debug.Log(text); else Debug.LogError(text);
    }
}
