using System;
using System.Collections.Generic;
using System.Linq;

public static class AtlasBoardSteamAchievementCatalog
{
    public sealed class Entry
    {
        public string AtlasAchievementId;
        public string SteamApiName;

        public Entry(
            string atlasAchievementId,
            string steamApiName)
        {
            AtlasAchievementId =
                atlasAchievementId ?? string.Empty;
            SteamApiName =
                steamApiName ?? string.Empty;
        }
    }

    private static readonly Entry[] entries =
    {
        new Entry(
            "first_match",
            "ATLAS_FIRST_MATCH"),
        new Entry(
            "first_win",
            "ATLAS_FIRST_WIN"),
        new Entry(
            "win_streak_3",
            "ATLAS_WIN_STREAK_3"),
        new Entry(
            "matches_10",
            "ATLAS_MATCHES_10"),
        new Entry(
            "wins_10",
            "ATLAS_WINS_10"),
        new Entry(
            "dice_100",
            "ATLAS_DICE_100"),
        new Entry(
            "doubles_10",
            "ATLAS_DOUBLES_10"),
        new Entry(
            "properties_25",
            "ATLAS_PROPERTIES_25"),
        new Entry(
            "builder_20",
            "ATLAS_BUILDER_20"),
        new Entry(
            "networth_5000",
            "ATLAS_NETWORTH_5000"),
        new Entry(
            "turkey_explorer",
            "ATLAS_TURKEY_EXPLORER"),
        new Entry(
            "colorado_explorer",
            "ATLAS_COLORADO_EXPLORER"),
        new Entry(
            "usa_explorer",
            "ATLAS_USA_EXPLORER"),
        new Entry(
            "atlas_traveler",
            "ATLAS_TRAVELER")
    };

    private static readonly Dictionary<string, string>
        byAtlasId =
            entries.ToDictionary(
                item => item.AtlasAchievementId,
                item => item.SteamApiName,
                StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<Entry> Entries =>
        entries;

    public static bool TryGetSteamApiName(
        string atlasAchievementId,
        out string steamApiName)
    {
        steamApiName = string.Empty;

        if (string.IsNullOrWhiteSpace(
                atlasAchievementId))
        {
            return false;
        }

        return byAtlasId.TryGetValue(
            atlasAchievementId.Trim(),
            out steamApiName);
    }
}
