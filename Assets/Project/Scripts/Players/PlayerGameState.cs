using System;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerGameState : MonoBehaviour
{
    [Header("Stable Identity")]
    [FormerlySerializedAs("playerIndex")]
    [SerializeField, Min(0)]
    private int playerSlotIndex;

    [SerializeField]
    private string displayName = "Oyuncu";

    [SerializeField]
    private PlayerVisualProfile visualProfile;

    [Header("Online Seat Mirror")]
    [SerializeField]
    private bool onlineSeatStateActive;

    [SerializeField]
    private string onlineControllerKind = string.Empty;

    [SerializeField]
    private string onlineConnectionState = string.Empty;

    [SerializeField]
    private long onlineReconnectExpiresAtEpochMs;

    [SerializeField]
    private bool onlineAfkLockedOut;

    [Header("Economy")]
    [Tooltip(
        "Scene fallback only. MatchSetupManager replaces this from " +
        "the active BoardEconomyProfile when the match starts.")]
    [SerializeField, Min(0)]
    private int startingMoney = 1500;

    [SerializeField, Min(0)]
    private int currentMoney;

    [Header("Participation")]
    [SerializeField]
    private bool isParticipating = true;

    [Header("Turn Status")]
    [SerializeField, Min(0)]
    private int turnsToSkip;

    [Header("Elimination")]
    [SerializeField]
    private bool isBankrupt;

    public int PlayerIndex => playerSlotIndex;
    public int PlayerSlotIndex => playerSlotIndex;
    public string DisplayName => displayName;
    public PlayerVisualProfile VisualProfile => visualProfile;
    public bool OnlineSeatStateActive => onlineSeatStateActive;
    public string OnlineControllerKind => onlineControllerKind;
    public string OnlineConnectionState => onlineConnectionState;
    public long OnlineReconnectExpiresAtEpochMs => onlineReconnectExpiresAtEpochMs;
    public bool OnlineAfkLockedOut => onlineAfkLockedOut;

    public bool IsOnlineTemporaryBot =>
        onlineSeatStateActive &&
        string.Equals(
            onlineControllerKind,
            "temporary_bot",
            StringComparison.OrdinalIgnoreCase);

    public bool IsOnlineBotControlled =>
        onlineSeatStateActive &&
        !string.IsNullOrWhiteSpace(
            onlineControllerKind) &&
        !string.Equals(
            onlineControllerKind,
            "human",
            StringComparison.OrdinalIgnoreCase);

    public bool IsOnlinePermanentBot =>
        onlineSeatStateActive &&
        (string.Equals(
             onlineControllerKind,
             "permanent_bot",
             StringComparison.OrdinalIgnoreCase) ||
         (string.Equals(
              onlineControllerKind,
              "bot",
              StringComparison.OrdinalIgnoreCase) &&
          onlineAfkLockedOut));

    public Material OwnershipMaterial =>
        visualProfile != null
            ? visualProfile.OwnershipMaterial
            : null;

    public Color UIColor =>
        visualProfile != null
            ? visualProfile.UIColor
            : Color.white;

    public int StartingMoney => startingMoney;
    public int CurrentMoney => currentMoney;
    public bool IsParticipating => isParticipating;
    public int TurnsToSkip => turnsToSkip;
    public bool HasTurnsToSkip => turnsToSkip > 0;
    public bool IsBankrupt => isBankrupt;

    public event Action<PlayerGameState> MoneyChanged;
    public event Action<PlayerGameState> TurnStatusChanged;
    public event Action<PlayerGameState> BankruptcyChanged;
    public event Action<PlayerGameState> ParticipationChanged;
    public event Action<PlayerGameState> IdentityChanged;
    public event Action<PlayerGameState> OnlineControlStateChanged;

    private void Awake()
    {
        currentMoney = startingMoney;
        turnsToSkip = 0;
        isBankrupt = false;

        ValidateStableIdentity();
    }

    public void PrepareForMatch(
        int matchStartingMoney,
        bool participating)
    {
        startingMoney =
            Mathf.Max(0, matchStartingMoney);

        currentMoney = startingMoney;
        turnsToSkip = 0;
        isBankrupt = false;
        isParticipating = participating;

        MoneyChanged?.Invoke(this);
        TurnStatusChanged?.Invoke(this);
        BankruptcyChanged?.Invoke(this);
        ParticipationChanged?.Invoke(this);

        Debug.Log(
            $"{displayName} [Slot {playerSlotIndex}] prepared for match. " +
            $"Participating: {isParticipating}, money: {currentMoney}.",
            this);
    }

    public void SetParticipating(
        bool participating)
    {
        if (isParticipating == participating)
        {
            return;
        }

        isParticipating = participating;
        ParticipationChanged?.Invoke(this);
    }

    public bool TrySpend(int amount)
    {
        if (!isParticipating ||
            isBankrupt ||
            amount < 0 ||
            currentMoney < amount)
        {
            return false;
        }

        currentMoney -= amount;
        MoneyChanged?.Invoke(this);

        return true;
    }

    public void AddMoney(int amount)
    {
        if (!isParticipating ||
            isBankrupt ||
            amount <= 0)
        {
            return;
        }

        currentMoney += amount;
        MoneyChanged?.Invoke(this);
    }

    // Phase 5D Remote follower state mirror. This assigns the Host-provided
    // balance exactly and raises the existing presentation refresh event. It
    // does NOT perform a purchase, rent payment, reward, or other economy rule.
    public void ApplyOnlineAuthoritativeMoney(
        int authoritativeMoney)
    {
        int sanitizedMoney =
            Mathf.Max(0, authoritativeMoney);

        if (currentMoney == sanitizedMoney)
        {
            return;
        }

        currentMoney = sanitizedMoney;
        MoneyChanged?.Invoke(this);
    }

    public void ApplyOnlineIdentityAndControlState(
        string authoritativeDisplayName,
        string controllerKind,
        string connectionState,
        long reconnectExpiresAtEpochMs,
        bool afkLockedOut)
    {
        bool identityChanged = false;

        if (!string.IsNullOrWhiteSpace(authoritativeDisplayName) &&
            !string.Equals(
                displayName,
                authoritativeDisplayName.Trim(),
                StringComparison.Ordinal))
        {
            displayName = authoritativeDisplayName.Trim();
            identityChanged = true;
        }

        string nextController =
            controllerKind ?? string.Empty;
        string nextConnection =
            connectionState ?? string.Empty;

        bool controlChanged =
            !onlineSeatStateActive ||
            !string.Equals(
                onlineControllerKind,
                nextController,
                StringComparison.Ordinal) ||
            !string.Equals(
                onlineConnectionState,
                nextConnection,
                StringComparison.Ordinal) ||
            onlineReconnectExpiresAtEpochMs != reconnectExpiresAtEpochMs ||
            onlineAfkLockedOut != afkLockedOut;

        onlineSeatStateActive = true;
        onlineControllerKind = nextController;
        onlineConnectionState = nextConnection;
        onlineReconnectExpiresAtEpochMs = reconnectExpiresAtEpochMs;
        onlineAfkLockedOut = afkLockedOut;

        if (identityChanged)
        {
            IdentityChanged?.Invoke(this);
        }

        if (controlChanged)
        {
            OnlineControlStateChanged?.Invoke(this);
        }
    }

    public void ClearOnlineSeatState()
    {
        if (!onlineSeatStateActive)
        {
            return;
        }

        onlineSeatStateActive = false;
        onlineControllerKind = string.Empty;
        onlineConnectionState = string.Empty;
        onlineReconnectExpiresAtEpochMs = 0L;
        onlineAfkLockedOut = false;
        OnlineControlStateChanged?.Invoke(this);
    }

    public int TakeAllMoney()
    {
        if (currentMoney <= 0)
        {
            return 0;
        }

        int removedAmount = currentMoney;
        currentMoney = 0;

        MoneyChanged?.Invoke(this);

        return removedAmount;
    }

    public void AddTurnsToSkip(int amount)
    {
        if (!isParticipating ||
            isBankrupt ||
            amount <= 0)
        {
            return;
        }

        turnsToSkip += amount;
        TurnStatusChanged?.Invoke(this);

        Debug.Log(
            $"{displayName} will skip {turnsToSkip} turn(s).",
            this);
    }

    public bool ConsumeSkippedTurn()
    {
        if (!isParticipating ||
            isBankrupt ||
            turnsToSkip <= 0)
        {
            return false;
        }

        turnsToSkip--;
        TurnStatusChanged?.Invoke(this);

        return true;
    }

    public bool DeclareBankrupt()
    {
        if (isBankrupt)
        {
            return false;
        }

        isBankrupt = true;
        currentMoney = 0;
        turnsToSkip = 0;

        MoneyChanged?.Invoke(this);
        TurnStatusChanged?.Invoke(this);
        BankruptcyChanged?.Invoke(this);

        Debug.Log(
            $"{displayName} [Slot {playerSlotIndex}] was declared bankrupt.",
            this);

        return true;
    }

    private void ValidateStableIdentity()
    {
        if (visualProfile == null)
        {
            Debug.LogError(
                $"{displayName} does not have a PlayerVisualProfile.",
                this);

            return;
        }

        if (visualProfile.OwnershipMaterial == null)
        {
            Debug.LogError(
                $"{displayName}'s visual profile does not have " +
                "an ownership material.",
                visualProfile);
        }
    }
}
