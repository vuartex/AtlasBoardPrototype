using System.Threading.Tasks;

public interface IAtlasIdentityProvider
{
    string ProviderId { get; }
    AtlasPlatformKind Platform { get; }
    bool IsSignedIn { get; }
    AtlasPlayerIdentity CurrentIdentity { get; }

    Task<bool> EnsureSignedInAsync();
}
