#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPhase14JResponsiveLanguageRegression
{
    [MenuItem(
        "Atlas Board/Phases/Phase 14/14J - Responsive & Languages/Show Runtime Harness")]
    public static void ShowRuntimeHarness()
    {
        if (!Application.isPlaying)
        {
            Debug.Log(
                "Enter Play Mode first. Phase 14J V2 also works directly inside a Development Build.");
            return;
        }

        AtlasBoardPhase14JRuntimeHarness.ShowPanel();
    }

    [MenuItem(
        "Atlas Board/Phases/Phase 14/14J - Responsive & Languages/Runtime Harness Instructions")]
    public static void Instructions()
    {
        Debug.Log(
            "Phase 14J V2: For the real responsive pass, make a Development Build, " +
            "start a match before rolling, then press F12. " +
            "The runtime harness automatically tests 1280x1024, 1600x900 and 1920x800, " +
            "cycling EN/TR/ES/FR/DE/KO/RU across gameplay, City Info, Match Result, " +
            "Auction, Trade and Development. F8 toggles the harness panel.");
    }
}
#endif
