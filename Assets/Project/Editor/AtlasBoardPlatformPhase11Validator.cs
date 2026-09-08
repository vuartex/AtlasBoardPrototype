#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardPlatformPhase11Validator
{
    [MenuItem(
        "Atlas Board/Platform/Validate Phase 11A Foundation")]
    public static void Validate()
    {
        int passed = 0;

        Require(
            AtlasBoardPlatformJoinPayload.TryParse(
                "atlasboard://join?code=482731",
                out string uriCode) &&
            uriCode == "482731",
            "Shareable join-link parsing failed.");
        passed++;

        Require(
            AtlasBoardPlatformJoinPayload.TryParse(
                "+atlas_join 482731",
                out string commandCode) &&
            commandCode == "482731",
            "Steam-style connect-string parsing failed.");
        passed++;

        Require(
            !AtlasBoardPlatformJoinPayload.TryParse(
                "atlasboard://join?code=12345",
                out _),
            "Malformed room code was incorrectly accepted.");
        passed++;

        AtlasBoardPlatformPresence presence =
            new AtlasBoardPlatformPresence
            {
                State =
                    AtlasBoardPlatformPresenceState.Lobby,
                MapId = "map_usa",
                CurrentPlayers = 2,
                MaxPlayers = 4,
                Joinable = true
            };

        string presenceJson =
            JsonUtility.ToJson(presence);

        Require(
            !presenceJson.Contains("482731") &&
            !presenceJson.Contains("password"),
            "Presence payload leaked join/password material.");
        passed++;

        AtlasBoardDevelopmentPlatformAdapter adapter =
            new AtlasBoardDevelopmentPlatformAdapter();

        adapter.BindCanonicalIdentity(
            "account_phase11",
            "Phase 11 Player",
            AtlasPlatformKind.Windows);

        Require(
            adapter.CurrentIdentity != null &&
            adapter.CurrentIdentity.AccountId ==
                "account_phase11" &&
            adapter.CurrentIdentity.PlatformUserId !=
                adapter.CurrentIdentity.AccountId,
            "Canonical AccountId and platform user id were not kept separate.");
        passed++;

        AtlasBoardAchievementProjectionResult first =
            adapter
                .SynchronizeAchievementsAsync(
                    new[]
                    {
                        "first_match",
                        "first_win",
                        "first_match"
                    })
                .GetAwaiter()
                .GetResult();

        AtlasBoardAchievementProjectionResult replay =
            adapter
                .SynchronizeAchievementsAsync(
                    new[]
                    {
                        "first_match",
                        "first_win"
                    })
                .GetAwaiter()
                .GetResult();

        Require(
            first.Success &&
            first.NewlyProjectedCount == 2 &&
            replay.Success &&
            replay.NewlyProjectedCount == 0 &&
            adapter.ProjectedAchievementCount == 2,
            "Achievement projection was not replay-safe/idempotent.");
        passed++;

        Require(
            AtlasBoardPlatformJoinPayload
                .BuildShareableLink("482731") ==
            "atlasboard://join?code=482731" &&
            AtlasBoardPlatformJoinPayload
                .BuildSteamConnectString("482731") ==
            "+atlas_join 482731",
            "Invite payload generation failed.");
        passed++;

        Debug.Log(
            "AtlasBoard Phase 11A provider-neutral platform foundation " +
            $"validation PASS: {passed}/7. " +
            "Existing AccountId/SeatId, invite/session contracts, " +
            "safe presence payload, join parsing and replay-safe " +
            "achievement projection are preserved.");
    }

    private static void Require(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                "AtlasBoard Phase 11A validation FAILED: " +
                message);
        }
    }
}
#endif
