using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum AtlasBoardPlatformPresenceState
{
    Unknown = 0,
    MainMenu = 1,
    Lobby = 2,
    Match = 3,
    Result = 4
}

[Serializable]
public sealed class AtlasBoardPlatformPresence
{
    public AtlasBoardPlatformPresenceState State =
        AtlasBoardPlatformPresenceState.Unknown;

    public string MapId = string.Empty;
    public int CurrentPlayers;
    public int MaxPlayers;
    public bool Joinable;

    public string Fingerprint()
    {
        return string.Join(
            "|",
            ((int)State).ToString(),
            MapId ?? string.Empty,
            Mathf.Max(0, CurrentPlayers).ToString(),
            Mathf.Max(0, MaxPlayers).ToString(),
            Joinable ? "1" : "0");
    }

    public AtlasBoardPlatformPresence Clone()
    {
        return new AtlasBoardPlatformPresence
        {
            State = State,
            MapId = MapId ?? string.Empty,
            CurrentPlayers = Mathf.Max(0, CurrentPlayers),
            MaxPlayers = Mathf.Max(0, MaxPlayers),
            Joinable = Joinable
        };
    }
}

[Serializable]
public sealed class AtlasBoardAchievementProjectionResult
{
    public bool Success;
    public int RequestedCount;
    public int NewlyProjectedCount;
    public int TotalProjectedCount;
    public string TechnicalMessage = string.Empty;

    public static AtlasBoardAchievementProjectionResult Ok(
        int requestedCount,
        int newlyProjectedCount,
        int totalProjectedCount)
    {
        return new AtlasBoardAchievementProjectionResult
        {
            Success = true,
            RequestedCount = Mathf.Max(0, requestedCount),
            NewlyProjectedCount =
                Mathf.Max(0, newlyProjectedCount),
            TotalProjectedCount =
                Mathf.Max(0, totalProjectedCount)
        };
    }

    public static AtlasBoardAchievementProjectionResult Fail(
        string technicalMessage)
    {
        return new AtlasBoardAchievementProjectionResult
        {
            Success = false,
            TechnicalMessage =
                technicalMessage ?? string.Empty
        };
    }
}

public static class AtlasBoardPlatformJoinPayload
{
    private const string UriPrefix =
        "atlasboard://join?code=";

    private const string TokenPrefix =
        "atlas-room:";

    private const string CommandToken =
        "+atlas_join";

    public static string BuildShareableLink(
        string roomCode)
    {
        string code =
            SanitizeRoomCode(roomCode);

        return code.Length == 6
            ? UriPrefix + code
            : string.Empty;
    }

    public static string BuildSteamConnectString(
        string roomCode)
    {
        string code =
            SanitizeRoomCode(roomCode);

        return code.Length == 6
            ? CommandToken + " " + code
            : string.Empty;
    }

    public static string BuildProviderToken(
        string roomCode)
    {
        string code =
            SanitizeRoomCode(roomCode);

        return code.Length == 6
            ? TokenPrefix + code
            : string.Empty;
    }

    public static bool TryParse(
        string payload,
        out string roomCode)
    {
        roomCode = string.Empty;

        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        string trimmed =
            payload.Trim();

        if (trimmed.StartsWith(
                UriPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            roomCode =
                SanitizeRoomCode(
                    trimmed.Substring(
                        UriPrefix.Length));

            return roomCode.Length == 6;
        }

        if (trimmed.StartsWith(
                TokenPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            roomCode =
                SanitizeRoomCode(
                    trimmed.Substring(
                        TokenPrefix.Length));

            return roomCode.Length == 6;
        }

        if (trimmed.StartsWith(
                CommandToken,
                StringComparison.OrdinalIgnoreCase))
        {
            roomCode =
                SanitizeRoomCode(
                    trimmed.Substring(
                        CommandToken.Length));

            return roomCode.Length == 6;
        }

        roomCode =
            SanitizeRoomCode(trimmed);

        return roomCode.Length == 6;
    }

    public static string SanitizeRoomCode(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string digits =
            new string(
                value
                    .Where(char.IsDigit)
                    .Take(6)
                    .ToArray());

        return digits.Length == 6
            ? digits
            : string.Empty;
    }
}
