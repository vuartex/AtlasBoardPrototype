using System;
using System.Collections.Generic;

public enum AtlasLobbySeatMode
{
    Unknown = 0,
    HostLocal = 1,
    OpenOnline = 2,
    LocalHuman = 3,
    RemoteHuman = 4,
    Bot = 5,
    Inactive = 6
}

[Serializable]
public class AtlasLobbySettings
{
    public string MapId = "Turkey";
    public string ThemeId = "classic_table";
    public int RoundLimit = 20;
    public int MaxPlayers = 4;
    public int RequiredHumanPlayers = 1;
    public int LocalHumanCount = 1;
    public int RemoteHumanCount;
    public int RemoteReadyRequiredCount;
    public int OpenOnlineSeatCount;
    public int BotCount;
    public bool BalancedDevelopment = true;
    public bool DoublesEnabled = true;
    public bool TripleDoublePenaltyEnabled = true;
}

[Serializable]
public class AtlasLobbyMemberSnapshot
{
    public string SeatId;
    public int SlotIndex;
    public bool Active;
    public AtlasLobbySeatMode SeatMode;
    public bool IsHumanSeat;
    public string AccountId;
    public string LocalOwnerAccountId;
    public string DisplayName;
    public string PawnCosmeticId;
    public bool IsHost;
    public AtlasSeatControllerKind ControllerKind;
    public AtlasSeatConnectionState ConnectionState;
    public int ReadyForRevision;
    public bool RequiresReady;

    public bool IsReadyFor(int settingsRevision)
    {
        return Active &&
               SeatMode == AtlasLobbySeatMode.RemoteHuman &&
               !string.IsNullOrWhiteSpace(AccountId) &&
               ReadyForRevision == settingsRevision;
    }
}

[Serializable]
public class AtlasLobbySnapshot
{
    public string LobbyId;
    public AtlasRoomLifecycleState LifecycleState;
    public AtlasRoomVisibility Visibility;
    public string HostAccountId;
    public int SettingsRevision;
    public AtlasLobbySettings Settings =
        new AtlasLobbySettings();

    public string GameVersion;
    public int ProtocolVersion;
    public int RulesVersion;
    public string ContentVersion;
    public string RegionId;

    public string MatchId;
    public string StartEventId;
    public long StartCountdownEndsAtEpochMs;
    public bool HasPassword;

    public List<AtlasLobbyMemberSnapshot> Members =
        new List<AtlasLobbyMemberSnapshot>();

    public bool HasRemoteHumans =>
        Settings != null &&
        Settings.RemoteHumanCount > 0;
}

[Serializable]
public class AtlasLobbyOperationResult
{
    public bool Success;
    public string ErrorLocalizationKey;
    public string TechnicalMessage;
    public bool Applied;
    public bool IdempotentReplay;
    public bool Started;
    public string RoomCode;
    public AtlasLobbySnapshot Snapshot;

    public static AtlasLobbyOperationResult Ok(
        AtlasLobbySnapshot snapshot,
        string roomCode = "",
        bool applied = false,
        bool started = false,
        bool idempotentReplay = false)
    {
        return new AtlasLobbyOperationResult
        {
            Success = true,
            Snapshot = snapshot,
            RoomCode = roomCode ?? string.Empty,
            Applied = applied,
            Started = started,
            IdempotentReplay = idempotentReplay
        };
    }

    public static AtlasLobbyOperationResult Fail(
        string localizationKey,
        string technicalMessage)
    {
        return new AtlasLobbyOperationResult
        {
            Success = false,
            ErrorLocalizationKey =
                string.IsNullOrWhiteSpace(localizationKey)
                    ? "lobby.error.unknown"
                    : localizationKey,
            TechnicalMessage =
                technicalMessage ?? string.Empty
        };
    }
}


[Serializable]
public class AtlasPublicLobbyCard
{
    public string LobbyId;
    public string HostDisplayName;
    public string MapId;
    public string ThemeId;
    public int RoundLimit;
    public int MaxPlayers;
    public int OccupiedPlayers;
    public int OpenOnlineSeatCount;
    public string RegionId;
    public string GameVersion;
    public int ProtocolVersion;
    public int RulesVersion;
    public string ContentVersion;
    public int SettingsRevision;
    public bool HasPassword;
    public long CreatedAtEpochMs;
}

[Serializable]
public class AtlasPublicLobbyListResult
{
    public bool Success;
    public string ErrorLocalizationKey;
    public string TechnicalMessage;
    public List<AtlasPublicLobbyCard> Rooms =
        new List<AtlasPublicLobbyCard>();

    public static AtlasPublicLobbyListResult Ok(
        List<AtlasPublicLobbyCard> rooms)
    {
        return new AtlasPublicLobbyListResult
        {
            Success = true,
            Rooms = rooms ?? new List<AtlasPublicLobbyCard>()
        };
    }

    public static AtlasPublicLobbyListResult Fail(
        string localizationKey,
        string technicalMessage)
    {
        return new AtlasPublicLobbyListResult
        {
            Success = false,
            ErrorLocalizationKey =
                string.IsNullOrWhiteSpace(localizationKey)
                    ? "lobby.error.unknown"
                    : localizationKey,
            TechnicalMessage =
                technicalMessage ?? string.Empty
        };
    }
}
