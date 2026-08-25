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
