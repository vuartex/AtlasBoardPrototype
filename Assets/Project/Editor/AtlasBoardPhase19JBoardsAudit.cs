#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only source and serialization smoke; NOT commerce/device QA.</summary>
public static class AtlasBoardPhase19JBoardsAudit
{
    [MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 19J - Board Shop and Room Smoke")]
    public static void Run()
    {
        string[] ids = { "board_midnight_azure", "board_ember_maple", "board_arctic_aurora" };
        int[] prices = { 1200, 1500, 1700 };
        var draft = AtlasBoardShopContentDraft.CreatePreview();
        int validated = 0;
        for (int i = 0; i < ids.Length; i++)
        {
            var item = draft.FirstOrDefault(x => x.ItemId == ids[i]);
            if (item == null || item.ItemType != "board_theme" ||
                item.GoldPrice != prices[i] ||
                AtlasBoardRtdbMetaEconomy.Price(ids[i]) != prices[i] ||
                !AtlasBoardRtdbMetaEconomy.IsBoard(ids[i]))
                throw new InvalidOperationException("Board catalog mismatch: " + ids[i]);
            for (int language = 0; language < AtlasBoardLocalizationLanguages.Codes.Length; language++)
                if (string.IsNullOrEmpty(AtlasBoardShopContentDraft.LocalizedName(
                    ids[i], AtlasBoardLocalizationLanguages.Codes[language])))
                    throw new InvalidOperationException("Missing translation: " + ids[i]);
            validated++;
        }
        var settings = new AtlasLobbySettings { BoardThemeId = ids[1] };
        var restored = JsonUtility.FromJson<AtlasLobbySettings>(JsonUtility.ToJson(settings));
        if (restored == null || restored.BoardThemeId != ids[1] ||
            !AtlasBoardBoardThemeVisuals.IsValid(restored.BoardThemeId) ||
            !AtlasBoardBoardThemeVisuals.IsValid("default_board") ||
            !AtlasBoardShopContentDraft.HasCompleteLanguages())
            throw new InvalidOperationException("Board room settings serialization or localization failed");
        Debug.Log("ATB_PHASE19J_BOARD_QA PASS boards=" + validated +
            " locales=7 roomSnapshotField=true purchasePriceContract=true readOnly=true" +
            " unityPurchase=OPEN onlineTwoClients=OPEN backendSecurity=OPEN");
    }
}
#endif
