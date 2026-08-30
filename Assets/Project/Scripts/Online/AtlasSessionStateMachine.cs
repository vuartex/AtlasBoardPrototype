using System;
using System.Collections.Generic;

public class AtlasSessionStateMachine
{
    private readonly List<AtlasPlayerSeat> seats =
        new List<AtlasPlayerSeat>();

    private readonly HashSet<string> matchBlockedAccounts =
        new HashSet<string>(StringComparer.Ordinal);

    public AtlasRoomDescriptor Room { get; private set; }

    public IReadOnlyList<AtlasPlayerSeat> Seats => seats;

    public int ReconnectWindowSeconds { get; set; } =
        AtlasOnlineDefaults.ReconnectWindowSeconds;

    public int AfkAutoRollLimit { get; set; } =
        AtlasOnlineDefaults.AfkConsecutiveAutoRollLimit;

    public void Initialize(AtlasRoomDescriptor room)
    {
        Room = room ?? throw new ArgumentNullException(nameof(room));
        seats.Clear();
        matchBlockedAccounts.Clear();

        for (int i = 0; i < Math.Max(2, Math.Min(room.MaxPlayers, AtlasOnlineDefaults.MaxPlayers)); i++)
        {
            seats.Add(new AtlasPlayerSeat
            {
                SeatId = $"seat_{i + 1}",
                PlayerSlotIndex = i,
                ControllerKind = AtlasSeatControllerKind.None,
                ConnectionState = AtlasSeatConnectionState.Empty,
                IsRequiredHumanSeat =
                    i < Math.Max(
                        1,
                        Math.Min(
                            room.RequiredHumanPlayers,
                            Math.Min(
                                room.MaxPlayers,
                                AtlasOnlineDefaults.MaxPlayers))),
                ReadyForRevision = 0
            });
        }
    }

    public bool IsAccountBlocked(string accountId)
    {
        return !string.IsNullOrWhiteSpace(accountId) &&
               matchBlockedAccounts.Contains(accountId);
    }

    public bool TryAssignHuman(
        int slotIndex,
        AtlasPlayerIdentity identity,
        bool isHost,
        out string error)
    {
        error = string.Empty;

        if (Room == null)
        {
            error = "Session has not been initialized.";
            return false;
        }

        if (identity == null || !identity.IsValid)
        {
            error = "Player identity is invalid.";
            return false;
        }

        if (IsAccountBlocked(identity.AccountId))
        {
            error = "This player was removed for inactivity and cannot rejoin this match.";
            return false;
        }

        AtlasPlayerSeat seat = GetSeat(slotIndex);
        if (seat == null)
        {
            error = "Requested player slot does not exist.";
            return false;
        }

        if (seat.HasIdentity &&
            !string.Equals(seat.Identity.AccountId, identity.AccountId, StringComparison.Ordinal))
        {
            error = "Requested player slot is already reserved by another player.";
            return false;
        }

        seat.Identity = identity.Clone();
        seat.ControllerKind = AtlasSeatControllerKind.Human;
        seat.ConnectionState = AtlasSeatConnectionState.Connected;
        seat.LastRemovalReason = AtlasSeatRemovalReason.None;
        seat.ReconnectExpiresUtcTicks = 0;
        seat.AfkLockedOut = false;
        seat.IsHostSeat = isHost;
        seat.ReadyForRevision = 0;

        if (isHost)
        {
            Room.HostAccountId = identity.AccountId;
        }

        return true;
    }

    public bool TryAssignPermanentBot(int slotIndex)
    {
        AtlasPlayerSeat seat = GetSeat(slotIndex);
        if (seat == null)
        {
            return false;
        }

        seat.Identity = null;
        seat.ControllerKind = AtlasSeatControllerKind.PermanentBot;
        seat.ConnectionState = AtlasSeatConnectionState.Empty;
        seat.LastRemovalReason = AtlasSeatRemovalReason.None;
        seat.ReconnectExpiresUtcTicks = 0;
        seat.AfkLockedOut = false;
        seat.IsHostSeat = false;
        seat.IsRequiredHumanSeat = false;
        seat.ReadyForRevision = 0;
        return true;
    }

    public bool HandleDisconnect(string accountId, long nowUtcTicks)
    {
        AtlasPlayerSeat seat = FindSeatByAccount(accountId);
        if (seat == null || seat.AfkLockedOut)
        {
            return false;
        }

        seat.ControllerKind = AtlasSeatControllerKind.TemporaryBot;
        seat.ConnectionState = AtlasSeatConnectionState.Reconnecting;
        seat.LastRemovalReason = AtlasSeatRemovalReason.Disconnect;
        seat.ReconnectExpiresUtcTicks =
            AddSeconds(nowUtcTicks, ReconnectWindowSeconds);
        seat.ReadyForRevision = 0;

        return true;
    }

    public bool HandleVoluntaryLeave(string accountId, long nowUtcTicks)
    {
        AtlasPlayerSeat seat = FindSeatByAccount(accountId);
        if (seat == null || seat.AfkLockedOut)
        {
            return false;
        }

        seat.ControllerKind = AtlasSeatControllerKind.TemporaryBot;
        seat.ConnectionState = AtlasSeatConnectionState.Reconnecting;
        seat.LastRemovalReason = AtlasSeatRemovalReason.VoluntaryLeave;
        seat.ReconnectExpiresUtcTicks =
            AddSeconds(nowUtcTicks, ReconnectWindowSeconds);
        seat.ReadyForRevision = 0;

        return true;
    }

    public bool HandleAfkRemoval(string accountId)
    {
        AtlasPlayerSeat seat = FindSeatByAccount(accountId);
        if (seat == null || !seat.HasIdentity)
        {
            return false;
        }

        matchBlockedAccounts.Add(seat.Identity.AccountId);

        seat.ControllerKind = AtlasSeatControllerKind.PermanentBot;
        seat.ConnectionState = AtlasSeatConnectionState.AfkRemoved;
        seat.LastRemovalReason = AtlasSeatRemovalReason.Afk;
        seat.ReconnectExpiresUtcTicks = 0;
        seat.AfkLockedOut = true;
        seat.ReadyForRevision = 0;

        return true;
    }

    public bool RegisterScheduledTurnRoll(
        string accountId,
        bool rolledManually,
        out bool afkLimitReached)
    {
        afkLimitReached = false;

        AtlasPlayerSeat seat = FindSeatByAccount(accountId);
        if (seat == null ||
            seat.ControllerKind != AtlasSeatControllerKind.Human ||
            seat.ConnectionState != AtlasSeatConnectionState.Connected)
        {
            return false;
        }

        seat.RegisterScheduledTurnRoll(rolledManually);
        afkLimitReached =
            !rolledManually &&
            seat.ConsecutiveAutoRollTurns >= Math.Max(1, AfkAutoRollLimit);

        return true;
    }

    public bool TryReconnect(
        AtlasPlayerIdentity identity,
        long nowUtcTicks,
        out AtlasPlayerSeat restoredSeat,
        out string error)
    {
        restoredSeat = null;
        error = string.Empty;

        if (identity == null || !identity.IsValid)
        {
            error = "Player identity is invalid.";
            return false;
        }

        if (IsAccountBlocked(identity.AccountId))
        {
            error = "You were removed for inactivity and cannot rejoin this match.";
            return false;
        }

        AtlasPlayerSeat seat = FindSeatByAccount(identity.AccountId);
        if (seat == null)
        {
            error = "No reserved seat was found for this player.";
            return false;
        }

        if (!seat.HasReconnectReservation(nowUtcTicks))
        {
            error = "The reconnect reservation has expired.";
            return false;
        }

        seat.Identity = identity.Clone();
        seat.ControllerKind = AtlasSeatControllerKind.Human;
        seat.ConnectionState = AtlasSeatConnectionState.Connected;
        seat.LastRemovalReason = AtlasSeatRemovalReason.None;
        seat.ReconnectExpiresUtcTicks = 0;
        seat.ReadyForRevision = 0;
        restoredSeat = seat;
        return true;
    }

    public void ExpireReconnectReservations(long nowUtcTicks)
    {
        foreach (AtlasPlayerSeat seat in seats)
        {
            if (seat.ConnectionState != AtlasSeatConnectionState.Reconnecting ||
                seat.ReconnectExpiresUtcTicks <= 0 ||
                seat.ReconnectExpiresUtcTicks > nowUtcTicks)
            {
                continue;
            }

            seat.ControllerKind = AtlasSeatControllerKind.PermanentBot;
            seat.ConnectionState = AtlasSeatConnectionState.LeftVoluntarily;
            seat.LastRemovalReason = AtlasSeatRemovalReason.ReconnectExpired;
            seat.ReconnectExpiresUtcTicks = 0;
        }
    }

    public bool ShouldCloseAfterExplicitHumanRemoval()
    {
        return CountConnectedHumans() == 0;
    }

    public bool ShouldCloseAfterDisconnects(long nowUtcTicks)
    {
        if (CountConnectedHumans() > 0)
        {
            return false;
        }

        foreach (AtlasPlayerSeat seat in seats)
        {
            if (seat.HasReconnectReservation(nowUtcTicks))
            {
                return false;
            }
        }

        return true;
    }

    public bool TrySetLobbyReady(
        string accountId,
        bool ready,
        int expectedSettingsRevision,
        out string error)
    {
        error = string.Empty;

        if (Room == null)
        {
            error = "Session has not been initialized.";
            return false;
        }

        if (Room.LifecycleState != AtlasRoomLifecycleState.Waiting)
        {
            error = "Lobby is not waiting for players.";
            return false;
        }

        if (Room.SettingsRevision != expectedSettingsRevision)
        {
            error = "Lobby settings changed. Refresh before changing ready state.";
            return false;
        }

        AtlasPlayerSeat seat =
            FindSeatByAccount(accountId);

        if (seat == null ||
            !seat.IsRequiredHumanSeat ||
            !seat.IsHumanConnected)
        {
            error = "A connected required human seat was not found.";
            return false;
        }

        seat.ReadyForRevision =
            ready
                ? Room.SettingsRevision
                : 0;

        return true;
    }

    public bool TryAdvanceSettingsRevision(
        string accountId,
        out int newRevision,
        out string error)
    {
        newRevision =
            Room != null
                ? Room.SettingsRevision
                : 0;

        error = string.Empty;

        if (Room == null)
        {
            error = "Session has not been initialized.";
            return false;
        }

        if (Room.LifecycleState != AtlasRoomLifecycleState.Waiting)
        {
            error = "Lobby is not waiting for settings changes.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Room.HostAccountId) ||
            !string.Equals(
                Room.HostAccountId,
                accountId,
                StringComparison.Ordinal))
        {
            error = "Only the host can change lobby settings.";
            return false;
        }

        Room.SettingsRevision =
            Math.Max(
                AtlasOnlineDefaults.InitialLobbySettingsRevision,
                Room.SettingsRevision + 1);

        newRevision =
            Room.SettingsRevision;

        return true;
    }

    public bool AreAllRequiredHumansReady()
    {
        if (Room == null)
        {
            return false;
        }

        int required =
            Math.Max(
                1,
                Math.Min(
                    Room.RequiredHumanPlayers,
                    seats.Count));

        int occupied = 0;
        int ready = 0;

        foreach (AtlasPlayerSeat seat in seats)
        {
            if (!seat.IsRequiredHumanSeat)
            {
                continue;
            }

            if (seat.IsHumanConnected)
            {
                occupied++;

                if (seat.IsReadyForRevision(
                        Room.SettingsRevision))
                {
                    ready++;
                }
            }
        }

        return occupied == required &&
               ready == required;
    }

    public bool TryTransitionLobbyToStarting(
        out string matchId,
        out string startEventId,
        out string error)
    {
        matchId =
            Room != null
                ? Room.MatchId
                : string.Empty;

        startEventId =
            Room != null
                ? Room.StartEventId
                : string.Empty;

        error = string.Empty;

        if (Room == null)
        {
            error = "Session has not been initialized.";
            return false;
        }

        if (Room.LifecycleState ==
            AtlasRoomLifecycleState.Starting)
        {
            return Room.HasAuthoritativeStart;
        }

        if (Room.LifecycleState !=
            AtlasRoomLifecycleState.Waiting)
        {
            error = "Lobby is not waiting.";
            return false;
        }

        if (!AreAllRequiredHumansReady())
        {
            error = "Not all required human seats are ready for the current settings revision.";
            return false;
        }

        Room.MatchId =
            Guid.NewGuid()
                .ToString("N");

        Room.StartEventId =
            Guid.NewGuid()
                .ToString("N");

        Room.LifecycleState =
            AtlasRoomLifecycleState.Starting;

        matchId =
            Room.MatchId;

        startEventId =
            Room.StartEventId;

        return true;
    }

    public int CountConnectedHumans()
    {
        int count = 0;
        foreach (AtlasPlayerSeat seat in seats)
        {
            if (seat.IsHumanConnected)
            {
                count++;
            }
        }
        return count;
    }

    public AtlasPlayerSeat FindSeatByAccount(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return null;
        }

        foreach (AtlasPlayerSeat seat in seats)
        {
            if (seat.HasIdentity &&
                string.Equals(seat.Identity.AccountId, accountId, StringComparison.Ordinal))
            {
                return seat;
            }
        }

        return null;
    }

    public AtlasPlayerSeat FindSeatBySlot(
        int slotIndex)
    {
        return GetSeat(slotIndex);
    }

    private AtlasPlayerSeat GetSeat(int slotIndex)
    {
        foreach (AtlasPlayerSeat seat in seats)
        {
            if (seat.PlayerSlotIndex == slotIndex)
            {
                return seat;
            }
        }
        return null;
    }

    private static long AddSeconds(long utcTicks, int seconds)
    {
        return utcTicks + TimeSpan.FromSeconds(Math.Max(1, seconds)).Ticks;
    }
}
