using System;
using System.Collections.Generic;

[Serializable]
public sealed class AtlasMatchNetworkSeat
{
    public string SeatId;
    public int SlotIndex;
    public string SeatMode;
    public string DisplayName;
    public bool IsHost;
    public string ControllerKind;
    public string ConnectionState;
    public long ReconnectExpiresAtEpochMs;
    public bool AfkLockedOut;
    public string RemovalReason;
}

[Serializable]
public sealed class AtlasMatchNetworkSnapshot
{
    public string MatchId;
    public string LobbyId;
    public string Status;
    public string LocalSeatId;
    public bool LocalIsHost;
    public int Revision;
    public string Phase;
    public string TurnSeatId;
    public int EventSequence;
    public string SnapshotJson;
    public long UpdatedAtEpochMs;
    public int NetworkSchemaVersion;

    public List<AtlasMatchNetworkSeat> Seats =
        new List<AtlasMatchNetworkSeat>();
}

[Serializable]
public sealed class AtlasMatchIntent
{
    public string IntentId;
    public string ClientCommandId;
    public string AccountId;
    public string SeatId;
    public string IntentType;
    public string PayloadJson;
    public long CreatedAtEpochMs;
}

public sealed class AtlasMatchNetworkResult
{
    public bool Success;
    public string ErrorLocalizationKey;
    public string TechnicalMessage;
    public AtlasMatchNetworkSnapshot Snapshot;
    public AtlasMatchIntent[] Intents;
    public string IntentId;
    public bool IdempotentReplay;
    public int Acknowledged;
    public int Revision;

    public static AtlasMatchNetworkResult Fail(
        string key,
        string technical)
    {
        return new AtlasMatchNetworkResult
        {
            Success = false,
            ErrorLocalizationKey =
                string.IsNullOrWhiteSpace(key)
                    ? "match.error.unknown"
                    : key,
            TechnicalMessage =
                technical ?? string.Empty
        };
    }
}
