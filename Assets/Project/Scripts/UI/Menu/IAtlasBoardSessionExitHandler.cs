/// <summary>
/// Optional integration seam between the local Leave Flow UI and a future
/// online session/network layer.
///
/// Local/offline Atlas Board does not need an implementation. In that case
/// AtlasBoardLeaveFlowController keeps using its safe local scene-reload flow.
///
/// A future Steam/network session component can implement this interface so
/// leaving a lobby or match is handled by the authoritative online session
/// instead of reloading the local scene. The online implementation owns seat
/// reservation, temporary bot takeover, reconnect windows, host/session
/// lifetime and final disconnect policy.
/// </summary>
public interface IAtlasBoardSessionExitHandler
{
    /// <summary>
    /// True only while an authoritative online session is currently active.
    /// When true, opening the pause menu must not freeze Time.timeScale because
    /// remote players / host simulation may continue.
    /// </summary>
    bool IsOnlineSessionActive { get; }

    /// <summary>
    /// Handle a confirmed Leave Match request. Return true once the online
    /// session layer has accepted ownership of the request.
    /// </summary>
    bool TryHandleLeaveMatch();

    /// <summary>
    /// Handle Leave Lobby. Return true once the online session layer has
    /// accepted ownership of the request.
    /// </summary>
    bool TryHandleLeaveLobby();

    /// <summary>
    /// Handle Quit Game while an online session is active. Return true once
    /// the online session layer has accepted ownership of the request. The
    /// session layer is then responsible for orderly disconnect/session cleanup
    /// and closing the application when it is safe to do so.
    /// </summary>
    bool TryHandleQuitGame();
}
