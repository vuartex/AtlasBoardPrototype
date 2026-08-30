using System.Collections.Generic;
using System.Threading.Tasks;

public interface IAtlasSessionDirectoryProvider
{
    string ProviderId { get; }

    Task<AtlasRoomDescriptor> CreateRoomAsync(
        AtlasRoomDescriptor requestedRoom,
        AtlasPlayerIdentity hostIdentity);

    Task<bool> UpdateRoomAsync(AtlasRoomDescriptor room);

    Task<IReadOnlyList<AtlasRoomDescriptor>> QueryRoomsAsync(
        AtlasRoomQuery query);

    Task<AtlasRoomDescriptor> ResolveRoomCodeAsync(string roomCode);

    Task<bool> CloseRoomAsync(string sessionId);
}

public class AtlasRoomQuery
{
    public string SearchText;
    public string MapId;
    public int? PlayerCount;
    public int? RoundLimit;
    public bool AvailableSlotsOnly;
    public AtlasPlatformKind RequestingPlatform;
    public bool CrossplayEnabled;
    public int PageIndex;
    public int PageSize = 50;
}
