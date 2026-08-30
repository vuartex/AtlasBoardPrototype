using System;
using System.Collections.Generic;

[Serializable]
public class AtlasLobbySettings
{
    public string MapId = "Turkey";
    public string ThemeId = "Classic Table";
    public int RoundLimit = 20;
    public int MaxPlayers = 4;
    public int RequiredHumanPlayers = 1;
    public bool BalancedDevelopment = true;
    public bool DoublesEnabled = true;
    public bool TripleDoublePenaltyEnabled = true;
}

[Serializable]
public class AtlasLobbyMemberSnapshot
{
    public string SeatId;
    public int SlotIndex;
    public bool IsHumanSeat;
    public string AccountId;
    public string DisplayName;
    public bool IsHost;
    public AtlasSeatControllerKind ControllerKind;
    public AtlasSeatConnectionState ConnectionState;
    public int ReadyForRevision;

    public bool IsReadyFor(int settingsRevision)
    {
        return IsHumanSeat &&
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

    public List<AtlasLobbyMemberSnapshot> Members =
        new List<AtlasLobbyMemberSnapshot>();
}

[Serializable]
public class AtlasLobbyOperationResult
{
    public bool Success;
    public string ErrorLocalizationKey;
    public string TechnicalMessage;
    public bool IdempotentReplay;
    public bool Started;
    public string RoomCode;
    public AtlasLobbySnapshot Snapshot;

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
