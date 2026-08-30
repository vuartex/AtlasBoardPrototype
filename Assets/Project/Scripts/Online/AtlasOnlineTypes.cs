using System;

public enum AtlasPlatformKind
{
    Unknown = 0,
    Steam = 1,
    Windows = 2,
    MacOS = 3,
    Linux = 4,
    IOS = 5,
    Android = 6,
    Console = 7
}

public enum AtlasCrossplayMode
{
    SamePlatformOnly = 0,
    CrossPlatform = 1
}

public enum AtlasRoomVisibility
{
    Private = 0,
    FriendsOnly = 1,
    Public = 2
}

public enum AtlasRoomLifecycleState
{
    Waiting = 0,
    InMatch = 1,
    Closing = 2,
    Closed = 3,

    // Added without renumbering the existing serialized enum values.
    Starting = 4
}

public enum AtlasSeatControllerKind
{
    None = 0,
    Human = 1,
    TemporaryBot = 2,
    PermanentBot = 3
}

public enum AtlasSeatConnectionState
{
    Empty = 0,
    Connected = 1,
    Reconnecting = 2,
    LeftVoluntarily = 3,
    AfkRemoved = 4,
    Kicked = 5
}

public enum AtlasSeatRemovalReason
{
    None = 0,
    Disconnect = 1,
    VoluntaryLeave = 2,
    Afk = 3,
    Kicked = 4,
    ReconnectExpired = 5
}

public enum AtlasSessionAuthorityMode
{
    HostAuthoritative = 0,
    RelayHostAuthoritative = 1,
    DedicatedServer = 2
}

public static class AtlasOnlineDefaults
{
    public const int MaxPlayers = 4;
    public const int RoomCodeDigits = 6;
    public const int ReconnectWindowSeconds = 300;
    public const float HumanRollTimeoutSeconds = 10f;
    public const int AfkConsecutiveAutoRollLimit = 10;
    public const int ProtocolVersion = 1;
    public const int RulesVersion = 1;
    public const int InitialLobbySettingsRevision = 1;
}
