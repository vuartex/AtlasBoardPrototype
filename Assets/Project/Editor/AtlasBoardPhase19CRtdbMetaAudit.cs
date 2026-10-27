#if UNITY_EDITOR
using Firebase.Auth;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase19CRtdbMetaAudit
{
    [MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 19C - RTDB Profile Contract Smoke")]
    public static void Smoke()
    {
        bool prices = AtlasBoardRtdbMetaEconomy.Price("frame_halloween") == 350 &&
                      AtlasBoardRtdbMetaEconomy.Price("frame_new_year") == 350 &&
                      AtlasBoardRtdbMetaEconomy.Price("frame_turkiye") == 400 &&
                      AtlasBoardRtdbMetaEconomy.Price("frame_usa") == 400 &&
                      AtlasBoardRtdbMetaEconomy.Price("frame_neon_circuit") == 450 &&
                      AtlasBoardRtdbMetaEconomy.Price("frame_royal_crown") == 500 &&
                      AtlasBoardRtdbMetaEconomy.Price("frame_ocean_tide") == 450 &&
                      AtlasBoardRtdbMetaEconomy.Price("frame_sakura_blossom") == 475 &&
                      AtlasBoardRtdbMetaEconomy.Price("frame_cosmic_nova") == 550 &&
                      AtlasBoardRtdbMetaEconomy.Price("frame_forest_spirit") == 475 &&
                      AtlasBoardRtdbMetaEconomy.Price("fake_product") == 0;
        bool languages = AtlasBoardShopContentDraft.HasCompleteLanguages();
        bool count = AtlasBoardShopContentDraft.Count == 27;
        bool noStartupMoney =
            AtlasBoardRtdbMetaEconomy.Price("default_profile_frame") == 0;

        if (!prices || !languages || !count || !noStartupMoney)
        {
            Debug.LogError("ATB_PHASE19C_SOURCE_QA FAIL prices=" + prices +
                           " languages=" + languages + " catalog=" + count);
            return;
        }

        Debug.Log("ATB_PHASE19I_SOURCE_QA PASS frames=10 newGoldPrices=450,500,450,475,550,475 " +
                  "draftItems=27 languages=7 " +
                  "emulatorSecurity=SEPARATE_TEST liveRTDB=RETEST");
    }

    [MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 19C - Print Current Auth UID")]
    public static void PrintUid()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("ATB_PHASE19C_TEST_UID Start Unity Play Mode first.");
            return;
        }

        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null)
            {
                Debug.LogWarning("ATB_PHASE19C_TEST_UID no authenticated user");
                return;
            }
            bool steam = AtlasBoardRtdbMetaEconomy.IsSteamSession;
            Debug.Log("ATB_PHASE19E_IDENTITY uid=" + user.UserId +
                      " account=" + (steam ? "steam" :
                          user.IsAnonymous ? "guest" : "registered") +
                      " firebaseAuth=" + (user.IsAnonymous ? "anonymous" : "registered") +
                      " steamLinkVerified=false");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("ATB_PHASE19C_TEST_UID unavailable=" +
                             e.GetType().Name);
        }
    }
}
#endif
