using System;
using System.Threading.Tasks;

public interface IAtlasGameplayTransport
{
    string ProviderId { get; }
    bool IsRunning { get; }
    bool IsAuthority { get; }

    event Action<string, byte[]> PayloadReceived;
    event Action<string> PeerDisconnected;

    Task<bool> StartHostAsync(string sessionId);
    Task<bool> JoinAsync(string sessionId, string connectionToken);
    Task StopAsync();
    Task<bool> SendAsync(string peerAccountId, byte[] payload, bool reliable);
}
