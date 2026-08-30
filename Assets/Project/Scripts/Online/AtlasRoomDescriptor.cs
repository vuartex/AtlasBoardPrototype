using System;
using System.Collections.Generic;

[Serializable]
public class AtlasRoomDescriptor
{
    public string SessionId;
    public string RoomCode;
    public string RoomName;
    public string HostAccountId;
    public AtlasRoomVisibility Visibility = AtlasRoomVisibility.Private;
    public AtlasRoomLifecycleState LifecycleState = AtlasRoomLifecycleState.Waiting;
    public AtlasCrossplayMode CrossplayMode = AtlasCrossplayMode.CrossPlatform;
    public AtlasSessionAuthorityMode AuthorityMode = AtlasSessionAuthorityMode.HostAuthoritative;

    public string MapId = "Turkey";
    public string ThemeId = "Classic Table";
    public int RoundLimit = 20;
    public int MaxPlayers = AtlasOnlineDefaults.MaxPlayers;

    // Online lobby readiness is revision-based. A host rule change advances
    // SettingsRevision; existing ReadyForRevision values then become stale
    // without rewriting every member document.
    public int SettingsRevision =
        AtlasOnlineDefaults.InitialLobbySettingsRevision;

    public int RequiredHumanPlayers = 1;
    public string MatchId;
    public string StartEventId;

    public string GameVersion;
    public int ProtocolVersion = AtlasOnlineDefaults.ProtocolVersion;
    public int RulesVersion = AtlasOnlineDefaults.RulesVersion;
    public string ContentVersion = "1";
    public string RegionId = "auto";

    public List<AtlasPlatformKind> AllowedPlatforms =
        new List<AtlasPlatformKind>();

    public bool IsJoinable =>
        LifecycleState == AtlasRoomLifecycleState.Waiting;

    public bool IsStarting =>
        LifecycleState == AtlasRoomLifecycleState.Starting;

    public bool HasAuthoritativeStart =>
        !string.IsNullOrWhiteSpace(MatchId) &&
        !string.IsNullOrWhiteSpace(StartEventId);
}
