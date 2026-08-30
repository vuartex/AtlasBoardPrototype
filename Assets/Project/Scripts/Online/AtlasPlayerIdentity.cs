using System;

[Serializable]
public class AtlasPlayerIdentity
{
    public string AccountId;
    public string DisplayName;
    public AtlasPlatformKind Platform;
    public string PlatformUserId;

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(AccountId);

    public AtlasPlayerIdentity Clone()
    {
        return new AtlasPlayerIdentity
        {
            AccountId = AccountId,
            DisplayName = DisplayName,
            Platform = Platform,
            PlatformUserId = PlatformUserId
        };
    }
}
