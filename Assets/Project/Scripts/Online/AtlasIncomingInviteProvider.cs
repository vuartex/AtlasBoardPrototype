using System;

public interface IAtlasIncomingInviteProvider
{
    event Action<string> JoinRequested;

    bool SupportsIncomingInvites { get; }
}
