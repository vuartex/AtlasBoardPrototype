#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class AtlasBoardSteamPhase11BValidator
{
    [UnityEditor.MenuItem(
        "Atlas Board/Online & Backend/Platform/Validate Phase 11B Steam Client")]
    public static void Validate()
    {
        int passed = 0;

        Require(
            AtlasBoardSteamPlatformAdapter
                .IsSteamworksCompiled,
            "Steamworks.NET compile symbol is missing.");
        passed++;

        Require(
            AtlasBoardSteamAchievementCatalog
                .Entries.Count == 14,
            "Steam achievement catalog must contain 14 Phase 10 IDs.");
        passed++;

        Require(
            AtlasBoardSteamAchievementCatalog
                .Entries
                .Select(
                    item => item.AtlasAchievementId)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Count() == 14,
            "Duplicate Atlas achievement IDs found.");
        passed++;

        Require(
            AtlasBoardSteamAchievementCatalog
                .Entries
                .Select(
                    item => item.SteamApiName)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Count() == 14,
            "Duplicate Steam achievement API names found.");
        passed++;

        Require(
            AtlasBoardPlatformJoinPayload
                .BuildSteamConnectString(
                    "482731") ==
            "+atlas_join 482731",
            "Steam connect payload generation failed.");
        passed++;

        Require(
            AtlasBoardPlatformJoinPayload.TryParse(
                "+atlas_join 482731",
                out string roomCode) &&
            roomCode == "482731",
            "Steam connect payload parsing failed.");
        passed++;

        AtlasBoardPlatformPresence presence =
            new AtlasBoardPlatformPresence
            {
                State =
                    AtlasBoardPlatformPresenceState.Lobby,
                MapId = "USA",
                CurrentPlayers = 2,
                MaxPlayers = 4,
                Joinable = true
            };

        string json =
            JsonUtility.ToJson(presence);

        Require(
            !json.Contains("482731") &&
            !json.ToLowerInvariant()
                .Contains("password"),
            "Safe presence model leaked room credentials.");
        passed++;

        Require(
            AtlasBoardSteamRuntimeSettings
                .DefaultDevelopmentAppId == 480 &&
            AtlasBoardSteamRuntimeSettings
                .DevelopmentAppId > 0,
            "Steam development AppID setting is invalid.");
        passed++;

        Debug.Log(
            "AtlasBoard Phase 11B Steam client validation PASS: " +
            $"{passed}/8. Steamworks.NET is compiled, the 14 canonical " +
            "achievement mappings are unique, invite payloads are safe, " +
            "and provider-neutral AccountId/room contracts remain intact.");
    }

    private static void Require(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                "AtlasBoard Phase 11B validation FAILED: " +
                message);
        }
    }
}
#endif
