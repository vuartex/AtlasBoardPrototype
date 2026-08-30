using System.Threading.Tasks;

public interface IAtlasInviteProvider
{
    string ProviderId { get; }
    bool SupportsNativeInvites { get; }
    bool SupportsShareableJoinLink { get; }

    Task<bool> ShowNativeInviteAsync(AtlasRoomDescriptor room);
    string BuildShareableJoinLink(AtlasRoomDescriptor room);
}
