using System.Threading.Tasks;

public interface IAtlasPresenceProvider
{
    string ProviderId { get; }
    bool SupportsRichPresence { get; }

    Task<bool> SetPresenceAsync(
        AtlasBoardPlatformPresence presence);

    Task ClearPresenceAsync();
}
