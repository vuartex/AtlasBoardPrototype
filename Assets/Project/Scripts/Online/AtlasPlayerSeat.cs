using System;

[Serializable]
public class AtlasPlayerSeat
{
    public string SeatId;
    public int PlayerSlotIndex;
    public AtlasPlayerIdentity Identity;
    public AtlasSeatControllerKind ControllerKind;
    public AtlasSeatConnectionState ConnectionState;
    public AtlasSeatRemovalReason LastRemovalReason;
    public long ReconnectExpiresUtcTicks;
    public int ConsecutiveAutoRollTurns;
    public bool AfkLockedOut;
    public bool IsHostSeat;

    // Lobby-only readiness metadata. Gameplay ownership remains on this same
    // seat; Human/TemporaryBot/PermanentBot is still only the controller.
    public bool IsRequiredHumanSeat;
    public int ReadyForRevision;

    public bool HasIdentity =>
        Identity != null &&
        Identity.IsValid;

    public bool IsHumanConnected =>
        HasIdentity &&
        ControllerKind == AtlasSeatControllerKind.Human &&
        ConnectionState == AtlasSeatConnectionState.Connected;

    public bool HasReconnectReservation(long utcTicks)
    {
        return HasIdentity &&
               ConnectionState == AtlasSeatConnectionState.Reconnecting &&
               ReconnectExpiresUtcTicks > utcTicks;
    }

    public bool IsReadyForRevision(int settingsRevision)
    {
        return IsRequiredHumanSeat &&
               IsHumanConnected &&
               ReadyForRevision == settingsRevision;
    }

    public void ClearLobbyReady()
    {
        ReadyForRevision = 0;
    }

    public void ResetAfkStreak()
    {
        ConsecutiveAutoRollTurns = 0;
    }

    public void RegisterScheduledTurnRoll(bool rolledManually)
    {
        if (rolledManually)
        {
            ConsecutiveAutoRollTurns = 0;
            return;
        }

        ConsecutiveAutoRollTurns++;
    }

    public AtlasPlayerSeat Clone()
    {
        return new AtlasPlayerSeat
        {
            SeatId = SeatId,
            PlayerSlotIndex = PlayerSlotIndex,
            Identity = Identity != null ? Identity.Clone() : null,
            ControllerKind = ControllerKind,
            ConnectionState = ConnectionState,
            LastRemovalReason = LastRemovalReason,
            ReconnectExpiresUtcTicks = ReconnectExpiresUtcTicks,
            ConsecutiveAutoRollTurns = ConsecutiveAutoRollTurns,
            AfkLockedOut = AfkLockedOut,
            IsHostSeat = IsHostSeat,
            IsRequiredHumanSeat = IsRequiredHumanSeat,
            ReadyForRevision = ReadyForRevision
        };
    }
}
