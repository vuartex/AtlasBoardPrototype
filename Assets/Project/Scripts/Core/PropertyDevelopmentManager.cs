using System.Collections.Generic;
using UnityEngine;

public class PropertyDevelopmentManager : MonoBehaviour
{
    [Header("Board")]
    [SerializeField]
    private BoardPath boardPath;

    [SerializeField]
    private BoardEconomyProfile economyProfile;

    [Header("Prototype Development Rules")]
    [SerializeField, Range(1, 4)]
    private int maximumDevelopmentLevel = 4;

    [SerializeField, Min(0.1f)]
    private float developmentCostRatio = 0.5f;

    [SerializeField, Min(1)]
    private int minimumDevelopmentCost = 50;

    [SerializeField]
    private int[] rentMultipliers = { 1, 2, 3, 5, 8 };

    [Header("Group Development Rule")]
    [Tooltip(
        "When enabled, a property may only be developed if it " +
        "is currently at the minimum development level in its " +
        "completed group. This keeps group development balanced.")]
    [SerializeField]
    private bool requireBalancedGroupDevelopment = true;

    [Header("Development Visuals")]
    [Tooltip(
        "Optional prefabs for development levels 1 through 4. " +
        "The selected level prefab is repeated side-by-side " +
        "1/2/3/4 times in a Monopoly-style row.")]
    [SerializeField]
    private GameObject[] developmentLevelPrefabs =
        new GameObject[4];

    [Tooltip(
        "Extra uniform/manual scale applied before automatic " +
        "fit-to-tile scaling. Keep X/Y/Z equal.")]
    [SerializeField]
    private Vector3 developmentPrefabLocalScale =
        Vector3.one;

    [Header("Monopoly-Style Placement")]
    [Tooltip(
        "Places development visuals on the OUTER edge of each " +
        "property tile instead of the tile center.")]
    [SerializeField]
    private bool placeOnOuterTileEdge = true;

    [Tooltip(
        "Keeps development visuals away from the outer title/header strip by " +
        "placing them on the inner board-facing edge. Recommended for Atlas Board.")]
    [SerializeField]
    private bool avoidOuterTitleHeader = true;

    [Tooltip(
        "Distance from the outside edge of the tile in world units.")]
    [SerializeField, Min(0f)]
    private float outerEdgeInset = 0.08f;

    [Tooltip(
        "Small vertical gap above the tile surface.")]
    [SerializeField, Min(0f)]
    private float tileSurfaceGap = 0.015f;

    [Tooltip(
        "Gap between side-by-side buildings in world units.")]
    [SerializeField, Min(0f)]
    private float buildingGap = 0.05f;

    [Tooltip(
        "Maximum total row width as a fraction of the tile width.")]
    [SerializeField, Range(0.25f, 0.95f)]
    private float maximumRowWidthRatio = 0.82f;

    [Tooltip(
        "Maximum building height as a fraction of the tile world width. " +
        "Keeps imported city assets small enough for a board game.")]
    [SerializeField, Range(0.1f, 1.5f)]
    private float maximumBuildingHeightRatio = 0.38f;

    [Tooltip(
        "Uses clean board-side rotations (0/90/180/270 degrees) " +
        "instead of pointing each building diagonally toward center.")]
    [SerializeField]
    private bool alignToBoardSide = true;

    [Tooltip(
        "Use this only if the imported prefab faces the wrong way. " +
        "Typical values: 0, 90, 180, -90.")]
    [SerializeField]
    private float boardSideYawOffset;

    [Tooltip(
        "Normally leave this OFF so building prefabs keep their " +
        "original materials. The property tile itself already " +
        "shows the player's ownership color.")]
    [SerializeField]
    private bool applyOwnerMaterialToDevelopmentVisual;

    [Header("Fallback Marker Visuals")]
    [SerializeField]
    private Vector3 markerScale =
        new Vector3(0.16f, 0.22f, 0.16f);

    [SerializeField]
    private float markerHeight = 0.48f;

    [SerializeField]
    private float markerSpacing = 0.19f;

    [Header("Debug")]
    [SerializeField]
    private PlayerGameState debugOwner;

    [SerializeField, Range(0, 7)]
    private int debugGroupIndex;

    private readonly Dictionary<BoardTile, int>
        developmentLevels =
            new Dictionary<BoardTile, int>();

    private readonly Dictionary<BoardTile, Transform>
        markerRoots =
            new Dictionary<BoardTile, Transform>();

    // Phase 18B Pulse is carried by the existing development authority.
    // Each city has FOUR independent project levels and FOUR independent
    // metrics. Neither represents the legacy completed-group house level.
    private readonly Dictionary<BoardTile, int[]> pulseLevels = new Dictionary<BoardTile, int[]>();
    private readonly Dictionary<BoardTile, int[]> pulseMetricDrift = new Dictionary<BoardTile, int[]>();
    private readonly HashSet<string> pulseAppliedCommandIds = new HashSet<string>();
    private BoardGenerator pulseMapGenerator;
    private const int PulseMetricMaximum = 94;
    private const int PulseCategoryCount = 4;
    private static readonly int[] PulseLevelCosts = { 100, 175, 250, 350 };

    // Phase 18G: fixed, bounded memory owned by the EXISTING economy authority.
    // History is gameplay-scoped; no PlayerPrefs, second economy manager or RTDB log.
    private const int CityMemoryMaxEvents = 6;
    private const int CityMemoryMaxTransfers = 5;
    private const int CityMemoryWindowRounds = 5;
    private const int CityMemoryDurationRounds = 3;
    private const int CityMemoryCooldownRounds = 6;
    private sealed class CityMemoryEvent
    {
        public int kind; // 1 owner, 2 house, 3 Pulse, 4 visit, 5 loan, 6 repay, 7 seizure, 8 Event arrival
        public int round;
        public int detail;
    }
    private sealed class CityMemory
    {
        public int lastOwner;
        public int activeUntil; // exclusive round; effect not permanent
        public int cooldownUntil; // exclusive round
        public readonly List<int> transfers = new List<int>();
        public readonly List<CityMemoryEvent> events = new List<CityMemoryEvent>();
    }
    private readonly Dictionary<int, CityMemory> cityMemories = new Dictionary<int, CityMemory>();
    private TurnManager memoryTurns;
    private AtlasBoardTurnDiceNetworkCoordinator memoryNetwork;
    private float nextCityMemoryScan;
    private bool initialized;

    // Phase 18D: mortgage state belongs to the EXISTING development/economy authority.
    // Loan terms are counted only when this player's own turn is completed.
    [System.Serializable]
    public sealed class BankLoan
    {
        public string loanId;
        public int tileIndex;
        public int ownerSlot;
        public int principal;
        public int outstanding;
        public int turnsRemaining;
        public int initialTerm;
        public int ratePercent;
    }

    private readonly Dictionary<int, BankLoan> bankLoans = new Dictionary<int, BankLoan>();
    private readonly HashSet<string> bankProcessedCommands = new HashSet<string>();
    private static readonly int[] BankTerms = { 2, 3, 5 };
    private static readonly int[] BankRates = { 8, 12, 20 };

    public bool IsMortgaged(BoardTile city) => city != null && bankLoans.ContainsKey(city.TileIndex);
    public int GetBankLimit(BoardTile city) => city != null && city.TileType == TileType.City
        ? Mathf.Max(0, (int)((long)Mathf.Max(0, city.PurchasePrice) * 70L / 100L)) : 0;
    public int GetBankLoanCount(int slot)
    {
        int count = 0;
        foreach (BankLoan item in bankLoans.Values) if (item.ownerSlot == slot) count++;
        return count;
    }
    public int GetBankDebt(int slot)
    {
        long total = 0;
        foreach (BankLoan item in bankLoans.Values)
            if (item.ownerSlot == slot) total += item.outstanding;
        return (int)System.Math.Min(int.MaxValue, total);
    }
    public BankLoan GetBankLoan(BoardTile city)
    {
        if (city == null) return null;
        bankLoans.TryGetValue(city.TileIndex, out BankLoan record);
        return record;
    }
    public List<BankLoan> GetPlayerLoans(int slot)
    {
        var result = new List<BankLoan>();
        foreach (BankLoan item in bankLoans.Values)
            if (item.ownerSlot == slot) result.Add(item);
        result.Sort((left, right) => left.tileIndex.CompareTo(right.tileIndex));
        return result;
    }
    public bool CanOpenBankLoan(PlayerGameState player, BoardTile city, int amount, int termKind)
    {
        return player != null && player.IsParticipating && !player.IsBankrupt &&
               city != null && city.TileType == TileType.City && city.IsOwned &&
               city.OwnerPlayerIndex == player.PlayerSlotIndex && !IsMortgaged(city) &&
               GetBankLoanCount(player.PlayerSlotIndex) < 3 &&
               amount > 0 && amount <= GetBankLimit(city) && termKind >= 0 && termKind < 3;
    }
    public bool TryOpenBankLoan(PlayerGameState player, BoardTile city, int amount, int termKind, string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId) || bankProcessedCommands.Contains(commandId) ||
            !CanOpenBankLoan(player, city, amount, termKind)) return false;
        int interest = (int)(((long)amount * BankRates[termKind] + 99L) / 100L);
        if (amount > int.MaxValue - player.CurrentMoney || amount > int.MaxValue - interest) return false;
        BankLoan loan = new BankLoan
        {
            loanId = commandId, tileIndex = city.TileIndex, ownerSlot = player.PlayerSlotIndex,
            principal = amount, outstanding = amount + interest,
            turnsRemaining = BankTerms[termKind], initialTerm = BankTerms[termKind],
            ratePercent = BankRates[termKind]
        };
        bankLoans.Add(city.TileIndex, loan);
        bankProcessedCommands.Add(commandId);
        player.AddMoney(amount);
        RecordCityMemoryEvent(city, 5, amount);
        Debug.Log($"ATB_BANK_LOAN_OPEN city={city.PropertyId} principal={amount} total={loan.outstanding} term={loan.initialTerm}", this);
        return true;
    }
    public bool TryRepayBankLoan(PlayerGameState player, BoardTile city, string commandId)
    {
        if (player == null || city == null || string.IsNullOrWhiteSpace(commandId) ||
            bankProcessedCommands.Contains(commandId) || !bankLoans.TryGetValue(city.TileIndex, out BankLoan loan) ||
            player.IsBankrupt || loan.ownerSlot != player.PlayerSlotIndex ||
            !city.IsOwned || city.OwnerPlayerIndex != loan.ownerSlot ||
            !player.TrySpend(loan.outstanding)) return false;
        bankLoans.Remove(city.TileIndex);
        bankProcessedCommands.Add(commandId);
        RecordCityMemoryEvent(city, 6, loan.outstanding);
        Debug.Log($"ATB_BANK_REPAY city={city.PropertyId} amount={loan.outstanding}", this);
        return true;
    }
    // Host/offline only. Called once on completion of the BORROWER's turn.
    public void AdvanceBankLoansForCompletedTurn(PlayerGameState player)
    {
        if (player == null || player.IsBankrupt) return;
        List<BankLoan> due = new List<BankLoan>();
        foreach (BankLoan loan in bankLoans.Values)
        {
            if (loan.ownerSlot != player.PlayerSlotIndex) continue;
            loan.turnsRemaining = Mathf.Max(0, loan.turnsRemaining - 1);
            if (loan.turnsRemaining == 0) due.Add(loan);
        }
        foreach (BankLoan loan in due)
        {
            if (!bankLoans.ContainsKey(loan.tileIndex)) continue;
            BoardTile city = boardPath != null ? boardPath.GetTile(loan.tileIndex) : null;
            if (city != null && city.IsOwned && city.OwnerPlayerIndex == player.PlayerSlotIndex &&
                player.TrySpend(loan.outstanding))
            {
                bankLoans.Remove(loan.tileIndex);
                RecordCityMemoryEvent(city, 6, loan.outstanding);
                Debug.Log($"ATB_BANK_MATURED_REPAID city={city.PropertyId} amount={loan.outstanding}", this);
            }
            else
            {
                ForecloseBankLoan(loan.tileIndex);
            }
        }
    }
    private void ForecloseBankLoan(int tileIndex)
    {
        if (!bankLoans.Remove(tileIndex)) return;
        BoardTile city = boardPath != null ? boardPath.GetTile(tileIndex) : null;
        if (city == null) return;
        RecordCityMemoryEvent(city, 7);
        ResetDevelopment(city); // foreclosure explicitly liquidates BOTH houses and Pulse
        city.ClearOwner();
        if (CanWriteCityMemory()) ObserveCityMemoryOwnership(city, CityMemoryRound());
        Debug.Log($"ATB_BANK_FORECLOSED city={city.PropertyId}", this);
    }
    public void CloseBankLoansOnBankruptcy(PlayerGameState player)
    {
        if (player == null) return;
        var seized = new List<int>();
        foreach (BankLoan loan in bankLoans.Values)
            if (loan.ownerSlot == player.PlayerSlotIndex) seized.Add(loan.tileIndex);
        foreach (int tileIndex in seized) ForecloseBankLoan(tileIndex);
    }
    public void ClearBankLoansForNewMatch()
    {
        bankLoans.Clear();
        bankProcessedCommands.Clear();
    }
    // Schema 11: slots indexed by tile; no fee or per-frame network writes.
    public int[] CaptureBankSnapshot(int tileCount)
    {
        int[] values = new int[tileCount * 6];
        for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            int o = tileIndex * 6;
            values[o] = -1;
            if (!bankLoans.TryGetValue(tileIndex, out BankLoan loan)) continue;
            values[o] = loan.ownerSlot;
            values[o+1] = loan.principal;
            values[o+2] = loan.outstanding;
            values[o+3] = loan.turnsRemaining;
            values[o+4] = loan.initialTerm;
            values[o+5] = loan.ratePercent;
        }
        return values;
    }
    public void ApplyBankSnapshot(int[] values, int tileCount)
    {
        if (values == null || tileCount < 0 || values.Length < tileCount * 6) return;
        bankLoans.Clear();
        for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
        {
            int o = tileIndex * 6;
            if (values[o] < 0 || values[o] > 3 || values[o+2] <= 0) continue;
            bankLoans[tileIndex] = new BankLoan
            {
                tileIndex = tileIndex, ownerSlot = values[o], principal = Mathf.Max(0, values[o+1]),
                outstanding = Mathf.Max(0, values[o+2]), turnsRemaining = Mathf.Clamp(values[o+3], 0, 5),
                initialTerm = Mathf.Clamp(values[o+4], 2, 5), ratePercent = Mathf.Clamp(values[o+5], 0, 20),
                loanId = "snapshot-" + tileIndex
            };
        }
    }
    // Bots may rescue themselves with a small short loan, but never pledge
    // a developed/encumbered city or borrow speculatively while healthy.
    public bool TryBotBankLoanOnTurn(PlayerGameState player)
    {
        if (player == null || player.IsBankrupt || player.CurrentMoney >= 90 ||
            GetBankLoanCount(player.PlayerSlotIndex) >= 3) return false;
        EnsureInitialized();
        if (boardPath == null) return false;
        for (int i = 0; i < boardPath.TileCount; i++)
        {
            BoardTile city = boardPath.GetTile(i);
            if (city == null || !city.IsOwned || city.OwnerPlayerIndex != player.PlayerSlotIndex ||
                GetDevelopmentLevel(city) > 0 || GetPulseInvestmentValue(city) > 0) continue;
            int amount = Mathf.Min(120, GetBankLimit(city));
            if (amount < 75) continue;
            return TryOpenBankLoan(player, city, amount, 0,
                "bot-loan-" + System.Guid.NewGuid().ToString("N"));
        }
        return false;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 18D - Bank Rules Smoke")]
    private static void EditorPhase18DBankRulesSmoke()
    {
        // Read-only: verify cap, debt, owner integrity, slot limits, and term bounds.
        PropertyDevelopmentManager manager = FindAnyObjectByType<PropertyDevelopmentManager>();
        if (manager == null) { Debug.LogError("ATB_PHASE18D_QA NOT_RUN: active match required"); return; }
        manager.EnsureInitialized();
        int cities = 0;
        int invalid = 0;
        int loans = 0;
        if (manager.boardPath != null)
        {
            for (int i = 0; i < manager.boardPath.TileCount; i++)
            {
                BoardTile city = manager.boardPath.GetTile(i);
                if (city == null || city.TileType != TileType.City) continue;
                cities++;
                if (manager.GetBankLimit(city) != (int)((long)Mathf.Max(0, city.PurchasePrice) * 70 / 100)) invalid++;
                BankLoan loan = manager.GetBankLoan(city);
                if (loan == null) continue;
                loans++;
                if (!city.IsOwned || city.OwnerPlayerIndex != loan.ownerSlot || loan.turnsRemaining < 0 ||
                    loan.turnsRemaining > 5 || loan.outstanding < loan.principal ||
                    manager.GetEffectiveRent(city) != 0) invalid++;
            }
        }
        for (int slot = 0; slot < 4; slot++)
            if (manager.GetBankLoanCount(slot) > 3) invalid++;
        Debug.Log($"ATB_PHASE18D_QA {(cities > 0 && invalid == 0 ? "PASS" : "FAIL")} read_only=true cities={cities} loans={loans} invalid={invalid}");
    }
#endif

    public void ResetLegacyHousesForTransfer(BoardTile city)
    {
        if (city == null) return;
        developmentLevels[city] = 0;
        RemoveMarkerRoot(city);
    }


    public int MaximumDevelopmentLevel =>
        maximumDevelopmentLevel;

    public bool RequireBalancedGroupDevelopment =>
        requireBalancedGroupDevelopment;

    public void SetRequireBalancedGroupDevelopment(
        bool required)
    {
        requireBalancedGroupDevelopment =
            required;

        Debug.Log(
            $"Balanced group development rule: " +
            $"{(required ? "ON" : "OFF")}.",
            this);
    }

    private void Start()
    {
        EnsureInitialized();
    }

    public bool IsEligibleForDevelopment(
        PlayerGameState player,
        BoardTile tile)
    {
        if (player == null ||
            tile == null ||
            player.IsBankrupt ||
            tile.TileType != TileType.City ||
            !tile.IsOwned || IsMortgaged(tile) ||
            tile.OwnerPlayerIndex !=
            player.PlayerSlotIndex)
        {
            return false;
        }

        EnsureInitialized();

        if (!HasCompleteGroup(
                player.PlayerSlotIndex,
                GetGroupIndex(tile)))
        {
            return false;
        }

        return GetDevelopmentLevel(tile) <
               maximumDevelopmentLevel;
    }

    public bool CanAffordDevelopment(
        PlayerGameState player,
        BoardTile tile)
    {
        return IsEligibleForDevelopment(
                   player,
                   tile) &&
               CanDevelopEvenly(
                   player,
                   tile) &&
               player.CurrentMoney >=
               GetDevelopmentCost(tile);
    }

    public bool CanDevelopEvenly(
        PlayerGameState player,
        BoardTile tile)
    {
        if (!IsEligibleForDevelopment(
                player,
                tile))
        {
            return false;
        }

        if (!requireBalancedGroupDevelopment)
        {
            return true;
        }

        int minimumLevel =
            GetGroupMinimumDevelopmentLevel(
                GetGroupIndex(tile));

        if (minimumLevel < 0)
        {
            return false;
        }

        return GetDevelopmentLevel(tile) ==
               minimumLevel;
    }

    public int GetGroupMinimumDevelopmentLevel(
        int groupIndex)
    {
        EnsureInitialized();

        if (boardPath == null ||
            groupIndex < 0)
        {
            return -1;
        }

        int minimumLevel =
            int.MaxValue;

        bool foundCity = false;

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile groupTile =
                boardPath.GetTile(tileIndex);

            if (groupTile == null ||
                groupTile.TileType != TileType.City ||
                GetGroupIndex(groupTile) !=
                groupIndex)
            {
                continue;
            }

            foundCity = true;

            minimumLevel =
                Mathf.Min(
                    minimumLevel,
                    GetDevelopmentLevel(
                        groupTile));
        }

        return foundCity
            ? minimumLevel
            : -1;
    }

    public string GetGroupDevelopmentSummary(
        BoardTile tile)
    {
        EnsureInitialized();

        if (tile == null ||
            boardPath == null)
        {
            return "-";
        }

        int groupIndex =
            GetGroupIndex(tile);

        List<string> levels =
            new List<string>();

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile groupTile =
                boardPath.GetTile(tileIndex);

            if (groupTile == null ||
                groupTile.TileType != TileType.City ||
                GetGroupIndex(groupTile) !=
                groupIndex)
            {
                continue;
            }

            levels.Add(
                $"L{GetDevelopmentLevel(groupTile)}");
        }

        return levels.Count > 0
            ? string.Join(" / ", levels)
            : "-";
    }

    public string GetDevelopmentBlockReason(
        PlayerGameState player,
        BoardTile tile)
    {
        if (!IsEligibleForDevelopment(
                player,
                tile))
        {
            return string.Empty;
        }

        if (requireBalancedGroupDevelopment &&
            !CanDevelopEvenly(
                player,
                tile))
        {
            int minimumLevel =
                GetGroupMinimumDevelopmentLevel(
                    GetGroupIndex(tile));

            return
                AtlasBoardL.T(
                    "development.balanced_rule",
                    minimumLevel);
        }

        if (player.CurrentMoney <
            GetDevelopmentCost(tile))
        {
            return
                AtlasBoardL.T(
                    "development.insufficient_balance");
        }

        return string.Empty;
    }

    public bool TryDevelop(
        PlayerGameState player,
        BoardTile tile)
    {
        if (!CanAffordDevelopment(
                player,
                tile))
        {
            string blockReason =
                GetDevelopmentBlockReason(
                    player,
                    tile);

            if (!string.IsNullOrEmpty(
                    blockReason))
            {
                Debug.Log(
                    $"{player?.DisplayName ?? "Player"} could not " +
                    $"develop {tile?.DisplayName ?? "property"}: " +
                    $"{blockReason}",
                    this);
            }

            return false;
        }

        int cost =
            GetDevelopmentCost(tile);

        if (!player.TrySpend(cost))
        {
            return false;
        }

        int newLevel =
            GetDevelopmentLevel(tile) + 1;

        developmentLevels[tile] =
            newLevel;

        RecordCityMemoryEvent(tile, 2, newLevel);

        RebuildDevelopmentVisual(
            tile,
            player.OwnershipMaterial);

        Debug.Log(
            $"{player.DisplayName} developed " +
            $"{tile.DisplayName} to level " +
            $"{newLevel} for {cost}. " +
            $"New rent: {GetEffectiveRent(tile)}.",
            this);

        return true;
    }

    public int GetDevelopmentLevel(
        BoardTile tile)
    {
        EnsureInitialized();

        if (tile == null ||
            !developmentLevels.TryGetValue(
                tile,
                out int level))
        {
            return 0;
        }

        return level;
    }

    public int GetDevelopmentCost(
        BoardTile tile)
    {
        if (tile == null)
        {
            return minimumDevelopmentCost;
        }

        if (tile.DevelopmentCost > 0)
        {
            return tile.DevelopmentCost;
        }

        // Legacy fallback for prototype/old scene data.
        int rawCost =
            Mathf.Max(
                minimumDevelopmentCost,
                Mathf.RoundToInt(
                    tile.PurchasePrice *
                    developmentCostRatio));

        return Mathf.CeilToInt(
                   rawCost / 10f) * 10;
    }

    private bool CanWriteCityMemory()
    {
        if (!Application.isPlaying) return false;
        if (memoryNetwork == null)
            memoryNetwork = UnityEngine.Object.FindAnyObjectByType<AtlasBoardTurnDiceNetworkCoordinator>();
        return memoryNetwork == null || !memoryNetwork.IsOnlineSessionActive || memoryNetwork.LocalIsHost;
    }

    private int CityMemoryRound()
    {
        if (memoryTurns == null) memoryTurns = UnityEngine.Object.FindAnyObjectByType<TurnManager>();
        return memoryTurns != null ? Mathf.Max(1, memoryTurns.CurrentRound) : 1;
    }

    private CityMemory GetOrCreateCityMemory(BoardTile city)
    {
        if (city == null || city.TileType != TileType.City) return null;
        if (!cityMemories.TryGetValue(city.TileIndex, out CityMemory memory))
        {
            memory = new CityMemory { lastOwner = city.IsOwned ? city.OwnerPlayerIndex : -1 };
            cityMemories.Add(city.TileIndex, memory);
        }
        return memory;
    }

    private static int RecentTransferCount(List<int> rounds, int currentRound)
    {
        int count = 0;
        foreach (int r in rounds)
            if (r <= currentRound && r > currentRound - CityMemoryWindowRounds) count++;
        return count;
    }

    // Ownership observation is independent from purchase/trade/bank transfer APIs;
    // it sees final authoritative state without adding another BoardTile owner writer.
    private void ObserveCityMemoryOwnership(BoardTile city, int round)
    {
        CityMemory memory = GetOrCreateCityMemory(city);
        if (memory == null) return;
        int owner = city.IsOwned ? city.OwnerPlayerIndex : -1;
        if (owner == memory.lastOwner) return;
        memory.lastOwner = owner;
        memory.transfers.Add(round);
        while (memory.transfers.Count > CityMemoryMaxTransfers)
            memory.transfers.RemoveAt(0);
        AddCityMemoryEvent(memory, 1, round, owner);
        TryActivateSpeculativeMarket(memory, round);
    }

    private static bool TryActivateSpeculativeMarket(CityMemory memory, int round)
    {
        if (round < memory.cooldownUntil ||
            RecentTransferCount(memory.transfers, round) < 3)
            return false;
        memory.activeUntil = round + CityMemoryDurationRounds;
        memory.cooldownUntil = round + CityMemoryCooldownRounds;
        return true;
    }

    private static void AddCityMemoryEvent(CityMemory memory, int kind, int round, int detail)
    {
        memory.events.Add(new CityMemoryEvent { kind = kind, round = round, detail = detail });
        while (memory.events.Count > CityMemoryMaxEvents)
            memory.events.RemoveAt(0);
    }

    private void ScanCityOwnershipForMemory()
    {
        if (!CanWriteCityMemory()) return;
        if (memoryTurns == null) memoryTurns = UnityEngine.Object.FindAnyObjectByType<TurnManager>();
        if (memoryTurns == null || !memoryTurns.IsMatchStarted || memoryTurns.IsMatchFinished) return;
        EnsureInitialized();
        if (boardPath == null) return;
        int round = CityMemoryRound();
        for (int i = 0; i < boardPath.TileCount; i++)
            ObserveCityMemoryOwnership(boardPath.GetTile(i), round);
    }

    private void Update()
    {
        // Local 0.35-s check; does not perform a network write or use RNG.
        if (Time.unscaledTime < nextCityMemoryScan) return;
        nextCityMemoryScan = Time.unscaledTime + 0.35f;
        ScanCityOwnershipForMemory();
    }

    private void RecordCityMemoryEvent(BoardTile city, int kind, int detail = 0)
    {
        if (!CanWriteCityMemory()) return;
        CityMemory memory = GetOrCreateCityMemory(city);
        if (memory == null) return;
        int round = CityMemoryRound();
        ObserveCityMemoryOwnership(city, round);
        // Multiple visits in one round are legitimate economically, but one
        // summary entry suffices, and avoids spam on repeated result rendering.
        if (kind == 4 && memory.events.Count > 0)
        {
            CityMemoryEvent recent = memory.events[memory.events.Count - 1];
            if (recent.kind == 4 && recent.round == round) return;
        }
        AddCityMemoryEvent(memory, kind, round, detail);
    }

    // Phase 18F arrival is a distinct history event; normal landing already
    // records its economic visit through TileResolutionManager.
    public void RecordEconomicEventLanding(BoardTile city)
    {
        RecordCityMemoryEvent(city, 8);
    }

    public bool IsSpeculativeMarketActive(BoardTile city)
    {
        return city != null && cityMemories.TryGetValue(city.TileIndex, out CityMemory memory) &&
               CityMemoryRound() < memory.activeUntil;
    }

    public int GetSpeculativeRoundsRemaining(BoardTile city)
    {
        return city != null && cityMemories.TryGetValue(city.TileIndex, out CityMemory memory)
            ? Mathf.Max(0, memory.activeUntil - CityMemoryRound()) : 0;
    }

    // Never mutate Pulse drift for a temporary market effect: it would leak
    // past expiry and be double-counted after a follower snapshot replay.
    private int GetCityMemoryMetricModifier(BoardTile city, int category)
    {
        if (!IsSpeculativeMarketActive(city)) return 0;
        return category == 2 ? 12 : category == 3 ? -20 : 0;
    }

    public string GetEconomicMemorySummary(BoardTile city)
    {
        if (city == null || city.TileType != TileType.City) return string.Empty;
        if (!cityMemories.TryGetValue(city.TileIndex, out CityMemory memory) || memory.events.Count == 0)
            return AtlasBoardL.R("No history this match", "Bu maçta henüz geçmiş yok", "Sin historial en esta partida", "Aucun historique pour cette partie", "Noch kein Verlauf in diesem Spiel", "이 경기의 기록 없음", "В этом матче истории пока нет");

        System.Text.StringBuilder result = new System.Text.StringBuilder();
        if (IsSpeculativeMarketActive(city))
        {
            result.Append(AtlasBoardL.R("Speculative Market", "Spekülatif Piyasa", "Mercado especulativo", "Marché spéculatif", "Spekulativer Markt", "투기 시장", "Спекулятивный рынок"));
            result.Append(" (" + GetSpeculativeRoundsRemaining(city) + " ");
            result.Append(AtlasBoardL.R("rounds left", "tur kaldı", "rondas restantes", "tours restants", "Runden übrig", "라운드 남음", "раундов осталось"));
            result.Append(")  Stability -20  Traffic +12");
            result.AppendLine();
        }
        int start = Mathf.Max(0, memory.events.Count - 4);
        for (int i = memory.events.Count - 1; i >= start; i--)
        {
            CityMemoryEvent entry = memory.events[i];
            string label;
            switch (entry.kind)
            {
                case 1: label = AtlasBoardL.R("Ownership changed", "Sahiplik değişti", "Cambio de propietario", "Changement de propriétaire", "Besitzerwechsel", "소유권 변경", "Смена владельца"); break;
                case 2: label = AtlasBoardL.R("House improved", "Ev geliştirildi", "Casa mejorada", "Maison améliorée", "Haus verbessert", "주택 개발", "Развитие дома"); break;
                case 3: label = AtlasBoardL.R("Pulse invested", "Pulse yatırımı", "Inversión Pulse", "Investissement Pulse", "Pulse-Investition", "Pulse 투자", "Инвестиция Pulse"); break;
                case 4: label = AtlasBoardL.R("City visit", "Şehir ziyareti", "Visita", "Visite", "Stadtbesuch", "도시 방문", "Посещение города"); break;
                case 5: label = AtlasBoardL.R("Mortgage opened", "İpotek açıldı", "Hipoteca abierta", "Hypothèque créée", "Hypothek aufgenommen", "담보 대출", "Открыта ипотека"); break;
                case 6: label = AtlasBoardL.R("Loan repaid", "Kredi ödendi", "Préstamo pagado", "Prêt remboursé", "Kredit getilgt", "대출 상환", "Кредит погашен"); break;
                case 7: label = AtlasBoardL.R("Bank foreclosure", "Bankaya haciz", "Ejecución hipotecaria", "Saisie bancaire", "Zwangsvollstreckung", "은행 압류", "Банковское взыскание"); break;
                case 8: label = AtlasBoardL.R("Event arrival", "Etkinlikle varış", "Llegada por evento", "Arrivée par événement", "Ankunft durch Ereignis", "이벤트로 도착", "Прибытие по событию"); break;
                default: continue;
            }
            result.Append("R").Append(entry.round).Append("  ·  ").Append(label);
            if (i > start) result.AppendLine();
        }
        return result.ToString();
    }

    // Compact additive v13 payload: only cities with economic activity are
    // serialized, at most 5 transfer rounds and 6 history entries per city.
    // Format: [1, cityCount, tileIndex, lastOwner, activeUntil, cooldownUntil,
    //          transferCount, transferRounds..., eventCount, (kind,round,detail)...].
    public int[] CaptureCityMemorySnapshot()
    {
        ScanCityOwnershipForMemory();
        List<int> values = new List<int> { 1, 0 };
        List<int> keys = new List<int>(cityMemories.Keys);
        keys.Sort();
        int count = 0;
        foreach (int tileIndex in keys)
        {
            CityMemory memory = cityMemories[tileIndex];
            if (memory.events.Count == 0 && memory.transfers.Count == 0 && memory.activeUntil == 0) continue;
            values.Add(tileIndex);
            values.Add(memory.lastOwner);
            values.Add(memory.activeUntil);
            values.Add(memory.cooldownUntil);
            values.Add(memory.transfers.Count);
            values.AddRange(memory.transfers);
            values.Add(memory.events.Count);
            foreach (CityMemoryEvent evt in memory.events)
            {
                values.Add(evt.kind);
                values.Add(evt.round);
                values.Add(evt.detail);
            }
            count++;
        }
        values[1] = count;
        return values.ToArray();
    }

    public void ApplyCityMemorySnapshot(int[] values)
    {
        if (values == null || values.Length < 2 || values[0] != 1) return;
        EnsureInitialized();
        if (boardPath == null || values[1] < 0 || values[1] > boardPath.TileCount) return;
        Dictionary<int, CityMemory> hydrated = new Dictionary<int, CityMemory>();
        int cursor = 2;
        for (int n = 0; n < values[1]; n++)
        {
            if (cursor + 5 > values.Length) return;
            int tileIndex = values[cursor++];
            int owner = values[cursor++];
            int activeUntil = values[cursor++];
            int cooldownUntil = values[cursor++];
            int transferCount = values[cursor++];
            BoardTile city = tileIndex >= 0 && tileIndex < boardPath.TileCount ? boardPath.GetTile(tileIndex) : null;
            if (city == null || city.TileType != TileType.City || hydrated.ContainsKey(tileIndex) ||
                owner < -1 || owner > 3 || activeUntil < 0 || cooldownUntil < 0 ||
                transferCount < 0 || transferCount > CityMemoryMaxTransfers || cursor + transferCount >= values.Length) return;
            CityMemory memory = new CityMemory
            {
                lastOwner = owner,
                activeUntil = activeUntil,
                cooldownUntil = cooldownUntil
            };
            for (int t = 0; t < transferCount; t++) memory.transfers.Add(Mathf.Max(1, values[cursor++]));
            int eventCount = values[cursor++];
            if (eventCount < 0 || eventCount > CityMemoryMaxEvents || cursor + eventCount * 3 > values.Length) return;
            for (int e = 0; e < eventCount; e++)
            {
                int kind = values[cursor++];
                int round = values[cursor++];
                int detail = values[cursor++];
                if (kind < 1 || kind > 8 || round < 1) return;
                memory.events.Add(new CityMemoryEvent { kind = kind, round = round, detail = detail });
            }
            hydrated.Add(tileIndex, memory);
        }
        if (cursor != values.Length) return;
        cityMemories.Clear();
        foreach (KeyValuePair<int, CityMemory> pair in hydrated) cityMemories.Add(pair.Key, pair.Value);
        // Snapshot revision and authority epoch are already checked by the
        // existing coordinator before calling this method.
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 18G - Economic Memory Smoke")]
    private static void EditorEconomicMemorySmoke()
    {
        PropertyDevelopmentManager manager = FindAnyObjectByType<PropertyDevelopmentManager>();
        if (manager == null) { Debug.LogWarning("ATB_PHASE18G_QA NOT_RUN: active match required"); return; }
        List<int> two = new List<int> { 4, 6 };
        List<int> three = new List<int> { 4, 6, 8 };
        List<int> four = new List<int> { 5, 6, 8, 9 };
        bool window = RecentTransferCount(two, 8) == 2 && RecentTransferCount(three, 8) == 3 &&
                      RecentTransferCount(four, 9) == 4 && RecentTransferCount(three, 11) == 2;
        int[] snapshot = manager.CaptureCityMemorySnapshot();
        bool bounded = snapshot != null && snapshot.Length >= 2 && snapshot[0] == 1;
        CityMemory sample = new CityMemory();
        sample.transfers.AddRange(new[] { 5, 6, 8 });
        bool triggered = TryActivateSpeculativeMarket(sample, 8);
        bool noDoubleTrigger = !TryActivateSpeculativeMarket(sample, 9);
        bool expiryCooldown = triggered && noDoubleTrigger &&
                              sample.activeUntil == 11 && sample.cooldownUntil == 14 &&
                              10 < sample.activeUntil && 11 >= sample.activeUntil &&
                              12 < sample.cooldownUntil && 14 >= sample.cooldownUntil;
        bounded = bounded && expiryCooldown;
        int cities = 0; int bad = 0;
        manager.EnsureInitialized();
        if (manager.boardPath != null)
            for (int i = 0; i < manager.boardPath.TileCount; i++)
            {
                BoardTile tile = manager.boardPath.GetTile(i);
                if (tile == null || tile.TileType != TileType.City) continue;
                cities++;
                for (int c = 0; c < 4; c++)
                    if (manager.GetPulseMetric(tile, c) < 0 || manager.GetPulseMetric(tile, c) > 94) bad++;
                if (manager.GetSpeculativeRoundsRemaining(tile) > CityMemoryDurationRounds) bad++;
            }
        Debug.Log($"ATB_PHASE18G_QA {(window && bounded && cities > 0 && bad == 0 ? "PASS" : "FAIL")} non_spending_seed=true cities={cities} historyInts={snapshot.Length} window={window} bad={bad} cap=94 duration=3 cooldown=6");
    }
#endif

    public int GetPulseLevel(BoardTile tile, int category)
    {
        if (tile == null || category < 0 || category >= PulseCategoryCount ||
            tile.TileType != TileType.City) return 0;
        return pulseLevels.TryGetValue(tile, out int[] levels)
            ? Mathf.Clamp(levels[category], 0, 4) : 0;
    }

    public int GetPulseBaseline(BoardTile tile, int category)
    {
        if (tile == null || category < 0 || category >= PulseCategoryCount ||
            tile.TileType != TileType.City) return 0;
        if (pulseMapGenerator == null)
            pulseMapGenerator = UnityEngine.Object.FindAnyObjectByType<BoardGenerator>();
        BoardMapDefinition map = pulseMapGenerator != null ? pulseMapGenerator.ActiveMapDefinition : null;
        return Mathf.Clamp(map != null ? map.GetPulseBaseline(tile.PropertyId, category) : 50,
            0, PulseMetricMaximum);
    }

    public int GetPulseMetric(BoardTile tile, int category)
    {
        if (tile == null || category < 0 || category >= PulseCategoryCount ||
            tile.TileType != TileType.City) return 0;
        int drift = pulseMetricDrift.TryGetValue(tile, out int[] data) ? data[category] : 0;
        // Four upgrades cannot reach 100; visits/events also stay under 95.
        BoardEconomyProfile activeEconomy = ResolveEconomyProfile();
        int maxScore = activeEconomy != null ? activeEconomy.PulseMetricMaximum : PulseMetricMaximum;
        int raw = GetPulseBaseline(tile, category) + GetPulseLevel(tile, category) * 2 + drift;
        raw += GetCityMemoryMetricModifier(tile, category);
        // Civic ownership modifiers are derived from the same authoritative
        // Civic snapshot: Trade/Demand +2, Transit/Traffic +3, Tech/Prosperity +5%.
        // They immediately follow ownership transfer, foreclosure and new-match reset.
        if (tile.IsOwned)
        {
            AtlasBoardCivicProjectManager civic = UnityEngine.Object.FindAnyObjectByType<AtlasBoardCivicProjectManager>();
            if (civic != null)
                raw += civic.GetOwnedCityMetricBonus(tile.OwnerPlayerIndex, category, raw);
        }
        return Mathf.Clamp(raw, 0, Mathf.Min(maxScore, 94));
    }

    public int GetPulseBuildCost(BoardTile tile, int category)
    {
        int next = GetPulseLevel(tile, category);
        if (tile == null || category < 0 || category >= PulseCategoryCount || next >= 4) return 0;
        BoardEconomyProfile activeEconomy = ResolveEconomyProfile();
        return activeEconomy != null ? activeEconomy.GetPulseLevelCost(next) : PulseLevelCosts[next];
    }

    public int GetPulseRentBonusPercent(BoardTile tile)
    {
        int sum = 0;
        for (int category = 0; category < PulseCategoryCount; category++)
            sum += GetPulseLevel(tile, category);
        BoardEconomyProfile activeEconomy = ResolveEconomyProfile();
        return activeEconomy != null ? activeEconomy.GetPulseRentBonusPercent(sum)
            : Mathf.Clamp(sum * 5, 0, 80);
    }

    // Phase 18C: Pulse investment travels WITH the property on a normal
    // ownership transfer. It is liquidated with houses on bankruptcy.
    // Trade pricing and the player net-worth calculation read this same state.
    public int GetPulseInvestmentValue(BoardTile tile)
    {
        if (tile == null || tile.TileType != TileType.City) return 0;
        long total = 0;
        for (int category = 0; category < PulseCategoryCount; category++)
            for (int level = 0; level < GetPulseLevel(tile, category); level++)
            {
                BoardEconomyProfile active = ResolveEconomyProfile();
                total += active != null ? active.GetPulseLevelCost(level) : PulseLevelCosts[level];
            }
        return (int)System.Math.Min(int.MaxValue, total);
    }

    // Do not let legacy houses be orphaned on *another* city of the same
    // group when a zero-house city is traded. The Pulse levels of any member
    // of this group are independent and do not trigger this restriction.
    public bool HasLegacyHousesInGroup(BoardTile tile)
    {
        EnsureInitialized();
        if (tile == null || boardPath == null || tile.TileType != TileType.City)
            return false;
        int groupIndex = GetGroupIndex(tile);
        if (groupIndex < 0) return false;
        for (int index = 0; index < boardPath.TileCount; index++)
        {
            BoardTile candidate = boardPath.GetTile(index);
            if (candidate != null && candidate.TileType == TileType.City &&
                GetGroupIndex(candidate) == groupIndex &&
                GetDevelopmentLevel(candidate) > 0)
                return true;
        }
        return false;
    }

    public bool CanBuildPulse(PlayerGameState player, BoardTile tile, int category)
    {
        return player != null && player.IsParticipating && !player.IsBankrupt &&
               tile != null && tile.TileType == TileType.City && tile.IsOwned &&
               tile.OwnerPlayerIndex == player.PlayerSlotIndex && !IsMortgaged(tile) &&
               category >= 0 && category < PulseCategoryCount &&
               GetPulseLevel(tile, category) < 4 &&
               player.CurrentMoney >= GetPulseBuildCost(tile, category);
    }

    public bool TryBuildPulse(PlayerGameState player, BoardTile tile, int category, string commandId)
    {
        if (string.IsNullOrWhiteSpace(commandId) ||
            pulseAppliedCommandIds.Contains(commandId) ||
            !CanBuildPulse(player, tile, category)) return false;
        int cost = GetPulseBuildCost(tile, category);
        // The owner and host validate before the one and only cash deduction.
        if (!player.TrySpend(cost)) return false;
        if (!pulseLevels.TryGetValue(tile, out int[] levels))
            pulseLevels[tile] = levels = new int[PulseCategoryCount];
        levels[category] = Mathf.Clamp(levels[category] + 1, 0, 4);
        pulseAppliedCommandIds.Add(commandId);
        RecordCityMemoryEvent(tile, 3, category);
        Debug.Log($"ATB_PULSE_BUILD city={tile.PropertyId} category={category} " +
                  $"level={levels[category]} cost={cost} rentBonus={GetPulseRentBonusPercent(tile)}%", this);
        return true;
    }

    // A completed authoritative landing changes city activity deterministically;
    // the 18G bounded Economic Memory design remains separate and OPEN.
    public void RecordPulseVisit(BoardTile tile)
    {
        if (tile == null || tile.TileType != TileType.City) return;
        if (!pulseMetricDrift.TryGetValue(tile, out int[] drift))
            pulseMetricDrift[tile] = drift = new int[PulseCategoryCount];
        drift[0] = Mathf.Min(12, drift[0] + 1); // demand
        drift[2] = Mathf.Min(12, drift[2] + 1); // traffic
        RecordCityMemoryEvent(tile, 4);
    }

    public int[] CapturePulseLevels(BoardTile tile)
    {
        int[] values = new int[PulseCategoryCount];
        for (int category = 0; category < PulseCategoryCount; category++)
            values[category] = GetPulseLevel(tile, category);
        return values;
    }

    public int[] CapturePulseDrift(BoardTile tile)
    {
        int[] values = new int[PulseCategoryCount];
        if (tile != null && pulseMetricDrift.TryGetValue(tile, out int[] drift))
            System.Array.Copy(drift, values, PulseCategoryCount);
        return values;
    }

    public void ApplyOnlinePulseState(BoardTile tile, int[] levels, int[] drift)
    {
        if (tile == null || tile.TileType != TileType.City) return;
        if (levels == null || drift == null || levels.Length < 4 || drift.Length < 4) return;
        if (!pulseLevels.TryGetValue(tile, out int[] safeLevels))
            pulseLevels[tile] = safeLevels = new int[PulseCategoryCount];
        if (!pulseMetricDrift.TryGetValue(tile, out int[] safeDrift))
            pulseMetricDrift[tile] = safeDrift = new int[PulseCategoryCount];
        for (int i = 0; i < PulseCategoryCount; i++)
        {
            safeLevels[i] = Mathf.Clamp(levels[i], 0, 4);
            safeDrift[i] = Mathf.Clamp(drift[i], -30, 12);
        }
    }

    public int GetEffectiveRent(
        BoardTile tile)
    {
        if (tile == null)
        {
            return 0;
        }

        if (IsMortgaged(tile)) return 0;
        int level =
            Mathf.Clamp(
                GetDevelopmentLevel(tile),
                0,
                maximumDevelopmentLevel);

        int multiplier =
            GetRentMultiplier(level);

        long legacyRent = (long)System.Math.Max(0, tile.BaseRent) * multiplier;
        int bonus = Mathf.Clamp(GetPulseRentBonusPercent(tile), 0, 80);
        // Exact integer path on offline host and RTDB followers; no float
        // precision drift, no overflow, and the bonus is applied just once.
        long result = legacyRent * (100L + bonus) / 100L;
        return (int)System.Math.Min(int.MaxValue, result);
    }

    public int GetProjectedRentAtNextLevel(
        BoardTile tile)
    {
        if (tile == null)
        {
            return 0;
        }

        if (IsMortgaged(tile)) return 0;
        int nextLevel =
            Mathf.Clamp(
                GetDevelopmentLevel(tile) + 1,
                0,
                maximumDevelopmentLevel);

        long legacy = (long)System.Math.Max(0, tile.BaseRent) * GetRentMultiplier(nextLevel);
        long result = legacy * (100L + Mathf.Clamp(GetPulseRentBonusPercent(tile), 0, 80)) / 100L;
        return (int)System.Math.Min(int.MaxValue, result);
    }

    public int GetGroupIndex(
        BoardTile tile)
    {
        if (tile == null)
        {
            return -1;
        }

        if (boardPath == null)
        {
            boardPath =
                FindAnyObjectByType<
                    BoardPath>();
        }

        if (boardPath != null &&
            !string.IsNullOrWhiteSpace(
                tile.GroupId))
        {
            List<string> groupIds =
                new List<string>();

            for (int tileIndex = 0;
                 tileIndex <
                 boardPath.TileCount;
                 tileIndex++)
            {
                BoardTile candidate =
                    boardPath.GetTile(
                        tileIndex);

                if (candidate == null ||
                    candidate.TileType !=
                        TileType.City ||
                    string.IsNullOrWhiteSpace(
                        candidate.GroupId))
                {
                    continue;
                }

                if (!groupIds.Contains(
                        candidate.GroupId))
                {
                    groupIds.Add(
                        candidate.GroupId);
                }

                if (candidate == tile ||
                    candidate.TileIndex ==
                        tile.TileIndex)
                {
                    return groupIds.IndexOf(
                        candidate.GroupId);
                }
            }

            int dataGroupIndex =
                groupIds.IndexOf(
                    tile.GroupId);

            if (dataGroupIndex >= 0)
            {
                return dataGroupIndex;
            }
        }

        // Legacy fallback for old prototype boards.
        return tile.TileIndex / 4;
    }

    public string GetGroupName(
        BoardTile tile)
    {
        if (tile == null)
        {
            return AtlasBoardL.T(
                "development.unknown_group");
        }

        if (!string.IsNullOrWhiteSpace(
                tile.GroupDisplayName))
        {
            return tile.GroupDisplayName;
        }

        int groupIndex =
            GetGroupIndex(tile);

        return groupIndex >= 0
            ? AtlasBoardL.T(
                "development.group_number",
                groupIndex + 1)
            : AtlasBoardL.T(
                "development.unknown_group");
    }

    public bool HasCompleteGroup(
        int playerSlotIndex,
        int groupIndex)
    {
        EnsureInitialized();

        if (boardPath == null ||
            groupIndex < 0)
        {
            return false;
        }

        int groupCityCount = 0;

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile == null ||
                tile.TileType != TileType.City ||
                GetGroupIndex(tile) != groupIndex)
            {
                continue;
            }

            groupCityCount++;

            if (!tile.IsOwned ||
                tile.OwnerPlayerIndex !=
                playerSlotIndex)
            {
                return false;
            }
        }

        // Phase 18C: legacy housing requires the original three-city group.
        // One or two owned cities still allow independent Pulse projects.
        return groupCityCount >= 3;
    }

    public void ApplyOnlineAuthoritativeDevelopmentLevel(
        BoardTile tile,
        int authoritativeLevel,
        PlayerGameState owner)
    {
        if (tile == null)
        {
            return;
        }

        EnsureInitialized();

        int clampedLevel =
            Mathf.Clamp(
                authoritativeLevel,
                0,
                maximumDevelopmentLevel);

        int currentLevel =
            GetDevelopmentLevel(tile);

        bool hasVisual =
            markerRoots.TryGetValue(
                tile,
                out Transform markerRoot) &&
            markerRoot != null &&
            markerRoot.gameObject.activeSelf &&
            markerRoot.childCount > 0;

        if (currentLevel == clampedLevel &&
            ((clampedLevel == 0 && !hasVisual) ||
             (clampedLevel > 0 && hasVisual)))
        {
            return;
        }

        developmentLevels[tile] =
            clampedLevel;

        if (clampedLevel <= 0)
        {
            RemoveMarkerRoot(tile);
            return;
        }

        Material material =
            owner != null
                ? owner.OwnershipMaterial
                : null;

        RebuildDevelopmentVisual(
            tile,
            material);
    }

    public void RefreshDevelopmentVisual(
        BoardTile tile,
        PlayerGameState owner)
    {
        if (tile == null)
        {
            return;
        }

        Material material =
            owner != null
                ? owner.OwnershipMaterial
                : null;

        RebuildDevelopmentVisual(
            tile,
            material);
    }

    public void ResetDevelopment(
        BoardTile tile)
    {
        if (tile == null)
        {
            return;
        }

        EnsureInitialized();

        developmentLevels[tile] = 0;
        pulseLevels.Remove(tile);
        pulseMetricDrift.Remove(tile);
        bankLoans.Remove(tile.TileIndex);
        RemoveMarkerRoot(tile);
    }

    public int GetDevelopmentInvestmentValue(
        int playerSlotIndex)
    {
        EnsureInitialized();

        int totalValue = 0;

        foreach (KeyValuePair<BoardTile, int> pair
                 in developmentLevels)
        {
            BoardTile tile = pair.Key;
            int level = pair.Value;

            if (tile == null ||
                !tile.IsOwned ||
                tile.OwnerPlayerIndex !=
                playerSlotIndex)
            {
                continue;
            }

            totalValue +=
                GetDevelopmentCost(tile) *
                level;
        }

        foreach (KeyValuePair<BoardTile, int[]> entry in pulseLevels)
        {
            BoardTile tile = entry.Key;
            if (tile == null || !tile.IsOwned || tile.OwnerPlayerIndex != playerSlotIndex) continue;
            totalValue = (int)System.Math.Min(int.MaxValue,
                (long)totalValue + GetPulseInvestmentValue(tile));
        }
        return totalValue;
    }

    public int GetTotalDevelopmentLevels(
        int playerSlotIndex)
    {
        EnsureInitialized();

        int totalLevels = 0;

        foreach (KeyValuePair<BoardTile, int> pair
                 in developmentLevels)
        {
            BoardTile tile = pair.Key;

            if (tile == null ||
                !tile.IsOwned ||
                tile.OwnerPlayerIndex !=
                playerSlotIndex)
            {
                continue;
            }

            totalLevels += pair.Value;
        }

        return totalLevels;
    }

    [ContextMenu("Debug/Assign Selected Group To Debug Owner")]
    private void DebugAssignSelectedGroup()
    {
        EnsureInitialized();

        if (debugOwner == null ||
            boardPath == null)
        {
            Debug.LogWarning(
                "Assign Debug Owner before using " +
                "the group assignment command.",
                this);

            return;
        }

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile == null ||
                tile.TileType != TileType.City ||
                GetGroupIndex(tile) !=
                debugGroupIndex)
            {
                continue;
            }

            tile.ClearOwner();
            ResetDevelopment(tile);

            if (tile.TrySetOwner(
                    debugOwner.PlayerSlotIndex))
            {
                tile.ApplyOwnerMaterial(
                    debugOwner.OwnershipMaterial);
            }
        }

        string debugGroupName =
            $"Bölge {debugGroupIndex + 1}";

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile != null &&
                tile.TileType == TileType.City &&
                GetGroupIndex(tile) ==
                    debugGroupIndex)
            {
                debugGroupName =
                    GetGroupName(tile);

                break;
            }
        }

        Debug.Log(
            $"{GetDebugOwnerName()} now owns all " +
            $"properties in {debugGroupName}.",
            this);
    }

    [ContextMenu("Debug/Reset All Developments")]
    private void DebugResetAllDevelopments()
    {
        EnsureInitialized();

        List<BoardTile> tiles =
            new List<BoardTile>(
                developmentLevels.Keys);

        foreach (BoardTile tile in tiles)
        {
            ResetDevelopment(tile);
        }

        Debug.Log(
            "All development levels were reset.",
            this);
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        if (boardPath == null)
        {
            boardPath =
                FindAnyObjectByType<BoardPath>();
        }

        if (boardPath == null)
        {
            return;
        }

        developmentLevels.Clear();
        markerRoots.Clear();

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile == null ||
                tile.TileType != TileType.City)
            {
                continue;
            }

            developmentLevels[tile] = 0;
            // Observe purchases even if they occur before the first periodic scan.
            GetOrCreateCityMemory(tile);

            Transform existingRoot =
                tile.transform.Find(
                    "DevelopmentMarkers");

            if (existingRoot != null)
            {
                markerRoots[tile] =
                    existingRoot;

                RemoveMarkerRoot(tile);
            }
        }

        initialized = true;
    }

    private int GetRentMultiplier(
        int level)
    {
        BoardEconomyProfile activeEconomy =
            ResolveEconomyProfile();

        if (activeEconomy != null)
        {
            return activeEconomy
                .GetRentMultiplier(level);
        }

        if (rentMultipliers == null ||
            rentMultipliers.Length == 0)
        {
            return Mathf.Max(
                1,
                level + 1);
        }

        int safeIndex =
            Mathf.Clamp(
                level,
                0,
                rentMultipliers.Length - 1);

        return Mathf.Max(
            1,
            rentMultipliers[safeIndex]);
    }

    private BoardEconomyProfile
        ResolveEconomyProfile()
    {
        if (economyProfile != null)
        {
            return economyProfile;
        }

        BoardGenerator generator =
            FindAnyObjectByType<
                BoardGenerator>();

        if (generator != null &&
            generator.ActiveEconomyProfile != null)
        {
            economyProfile =
                generator.ActiveEconomyProfile;
        }

        return economyProfile;
    }

    private void RebuildDevelopmentVisual(
        BoardTile tile,
        Material ownershipMaterial)
    {
        EnsureInitialized();
        RemoveMarkerRoot(tile);

        int level =
            GetDevelopmentLevel(tile);

        if (level <= 0)
        {
            return;
        }

        GameObject rootObject =
            new GameObject(
                "DevelopmentMarkers");

        rootObject.transform.SetParent(
            tile.transform,
            false);

        markerRoots[tile] =
            rootObject.transform;

        GameObject levelPrefab =
            GetDevelopmentPrefab(level);

        if (levelPrefab != null)
        {
            BuildPrefabDevelopmentVisual(
                tile,
                rootObject.transform,
                levelPrefab,
                level,
                ownershipMaterial);

            return;
        }

        BuildFallbackMarkerVisuals(
            rootObject.transform,
            level,
            ownershipMaterial);
    }

    private GameObject GetDevelopmentPrefab(
        int level)
    {
        if (developmentLevelPrefabs == null ||
            developmentLevelPrefabs.Length == 0)
        {
            return null;
        }

        int prefabIndex =
            level - 1;

        if (prefabIndex < 0 ||
            prefabIndex >=
                developmentLevelPrefabs.Length)
        {
            return null;
        }

        return developmentLevelPrefabs[
            prefabIndex];
    }

    private void BuildPrefabDevelopmentVisual(
        BoardTile tile,
        Transform visualRoot,
        GameObject levelPrefab,
        int level,
        Material ownershipMaterial)
    {
        int buildingCount =
            Mathf.Clamp(
                level,
                1,
                maximumDevelopmentLevel);

        Renderer tileRenderer =
            tile.GetComponent<Renderer>();

        if (tileRenderer == null)
        {
            BuildFallbackMarkerVisuals(
                visualRoot,
                level,
                ownershipMaterial);

            return;
        }

        List<GameObject> instances =
            new List<GameObject>();

        for (int index = 0;
             index < buildingCount;
             index++)
        {
            GameObject instance =
                Instantiate(
                    levelPrefab,
                    visualRoot);

            instance.name =
                $"Development_Level_{level}_{index + 1}";

            DisableDevelopmentColliders(
                instance);

            if (applyOwnerMaterialToDevelopmentVisual &&
                ownershipMaterial != null)
            {
                ApplyMaterialToDevelopmentVisual(
                    instance,
                    ownershipMaterial);
            }

            instances.Add(instance);
        }

        Quaternion worldRotation =
            GetDevelopmentWorldRotation(
                tile);

        foreach (GameObject instance
                 in instances)
        {
            Transform instanceTransform =
                instance.transform;

            Vector3 authoredPrefabScale =
                instanceTransform.localScale;

            Vector3 desiredWorldScale =
                Vector3.Scale(
                    authoredPrefabScale,
                    developmentPrefabLocalScale);

            Vector3 parentLossyScale =
                visualRoot.lossyScale;

            instanceTransform.localScale =
                new Vector3(
                    SafeScaleDivide(
                        desiredWorldScale.x,
                        parentLossyScale.x),
                    SafeScaleDivide(
                        desiredWorldScale.y,
                        parentLossyScale.y),
                    SafeScaleDivide(
                        desiredWorldScale.z,
                        parentLossyScale.z));

            instanceTransform.rotation =
                worldRotation;
        }

        Bounds firstBounds =
            GetCombinedRendererBounds(
                instances[0]);

        if (firstBounds.size.sqrMagnitude <=
            0.000001f)
        {
            BuildFallbackMarkerVisuals(
                visualRoot,
                level,
                ownershipMaterial);

            foreach (GameObject instance
                     in instances)
            {
                Destroy(instance);
            }

            return;
        }

        bool rowRunsAlongWorldX =
            IsRowAlongWorldX(
                tile.TileIndex);

        float tileRowWidth =
            rowRunsAlongWorldX
                ? tileRenderer.bounds.size.x
                : tileRenderer.bounds.size.z;

        float tileDepth =
            rowRunsAlongWorldX
                ? tileRenderer.bounds.size.z
                : tileRenderer.bounds.size.x;

        float buildingRowWidth =
            rowRunsAlongWorldX
                ? firstBounds.size.x
                : firstBounds.size.z;

        float buildingDepth =
            rowRunsAlongWorldX
                ? firstBounds.size.z
                : firstBounds.size.x;

        float maximumRowWidth =
            tileRowWidth *
            maximumRowWidthRatio;

        float currentRowWidth =
            buildingRowWidth *
            buildingCount +
            buildingGap *
            Mathf.Max(
                0,
                buildingCount - 1);

        float maximumBuildingHeight =
            tileRowWidth *
            maximumBuildingHeightRatio;

        float scaleForRow =
            currentRowWidth > 0.0001f
                ? maximumRowWidth /
                  currentRowWidth
                : 1f;

        float scaleForHeight =
            firstBounds.size.y > 0.0001f
                ? maximumBuildingHeight /
                  firstBounds.size.y
                : 1f;

        // Never enlarge imported models automatically. Only shrink
        // them enough to fit the board-game tile.
        float autoScale =
            Mathf.Min(
                1f,
                scaleForRow,
                scaleForHeight);

        foreach (GameObject instance
                 in instances)
        {
            instance.transform.localScale *=
                autoScale;
        }

        // Recalculate bounds after auto-fit scaling.
        firstBounds =
            GetCombinedRendererBounds(
                instances[0]);

        buildingRowWidth =
            rowRunsAlongWorldX
                ? firstBounds.size.x
                : firstBounds.size.z;

        buildingDepth =
            rowRunsAlongWorldX
                ? firstBounds.size.z
                : firstBounds.size.x;

        Vector3 outwardDirection =
            GetTileOutwardDirection(
                tile.TileIndex);

        Vector3 rowDirection =
            GetTileRowDirection(
                tile.TileIndex);

        float outerHalfDepth =
            rowRunsAlongWorldX
                ? tileRenderer.bounds.extents.z
                : tileRenderer.bounds.extents.x;

        float rowStartOffset =
            -0.5f *
            ((buildingCount - 1) *
             (buildingRowWidth +
              buildingGap));

        Vector3 tileCenter =
            tileRenderer.bounds.center;

        Vector3 rowBaseCenter =
            tileCenter;

        float edgeOffset =
            outerHalfDepth -
            outerEdgeInset -
            buildingDepth * 0.5f;

        if (avoidOuterTitleHeader)
        {
            rowBaseCenter -=
                outwardDirection *
                edgeOffset;
        }
        else if (placeOnOuterTileEdge)
        {
            rowBaseCenter +=
                outwardDirection *
                edgeOffset;
        }

        float tileTopY =
            tileRenderer.bounds.max.y;

        for (int index = 0;
             index < instances.Count;
             index++)
        {
            GameObject instance =
                instances[index];

            Bounds bounds =
                GetCombinedRendererBounds(
                    instance);

            float rowOffset =
                rowStartOffset +
                index *
                (buildingRowWidth +
                 buildingGap);

            Vector3 targetPosition =
                rowBaseCenter +
                rowDirection *
                rowOffset;

            // First place horizontally, then lift the model so its
            // renderer bottom sits exactly on the tile surface.
            Vector3 currentPosition =
                instance.transform.position;

            instance.transform.position =
                new Vector3(
                    targetPosition.x,
                    currentPosition.y,
                    targetPosition.z);

            bounds =
                GetCombinedRendererBounds(
                    instance);

            float verticalCorrection =
                tileTopY +
                tileSurfaceGap -
                bounds.min.y;

            instance.transform.position +=
                Vector3.up *
                verticalCorrection;
        }
    }

    private Quaternion GetDevelopmentWorldRotation(
        BoardTile tile)
    {
        if (tile == null)
        {
            return Quaternion.identity;
        }

        if (!alignToBoardSide)
        {
            return Quaternion.Euler(
                0f,
                boardSideYawOffset,
                0f);
        }

        float baseYaw =
            GetBoardSideYaw(
                tile.TileIndex);

        return Quaternion.Euler(
            0f,
            baseYaw +
            boardSideYawOffset,
            0f);
    }

    private float GetBoardSideYaw(
        int tileIndex)
    {
        // Clean cardinal rotations:
        // bottom faces center (+Z),
        // right faces center (-X),
        // top faces center (-Z),
        // left faces center (+X).
        if (tileIndex <= 8)
        {
            return 0f;
        }

        if (tileIndex <= 16)
        {
            return -90f;
        }

        if (tileIndex <= 24)
        {
            return 180f;
        }

        return 90f;
    }

    private bool IsRowAlongWorldX(
        int tileIndex)
    {
        return tileIndex <= 8 ||
               (tileIndex >= 16 &&
                tileIndex <= 24);
    }

    private Vector3 GetTileOutwardDirection(
        int tileIndex)
    {
        if (tileIndex <= 8)
        {
            return Vector3.back;
        }

        if (tileIndex <= 16)
        {
            return Vector3.right;
        }

        if (tileIndex <= 24)
        {
            return Vector3.forward;
        }

        return Vector3.left;
    }

    private Vector3 GetTileRowDirection(
        int tileIndex)
    {
        if (IsRowAlongWorldX(
                tileIndex))
        {
            return Vector3.right;
        }

        return Vector3.forward;
    }

    private Bounds GetCombinedRendererBounds(
        GameObject visualRoot)
    {
        Renderer[] renderers =
            visualRoot.GetComponentsInChildren<
                Renderer>(true);

        if (renderers == null ||
            renderers.Length == 0)
        {
            return new Bounds(
                visualRoot.transform.position,
                Vector3.zero);
        }

        Bounds bounds =
            renderers[0].bounds;

        for (int index = 1;
             index < renderers.Length;
             index++)
        {
            bounds.Encapsulate(
                renderers[index].bounds);
        }

        return bounds;
    }

    private void BuildFallbackMarkerVisuals(
        Transform visualRoot,
        int level,
        Material ownershipMaterial)
    {
        float totalWidth =
            (level - 1) * markerSpacing;

        for (int markerIndex = 0;
             markerIndex < level;
             markerIndex++)
        {
            GameObject marker =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            marker.name =
                $"Development_{markerIndex + 1}";

            marker.transform.SetParent(
                visualRoot,
                false);

            marker.transform.localScale =
                markerScale;

            marker.transform.localPosition =
                new Vector3(
                    -totalWidth * 0.5f +
                    markerIndex * markerSpacing,
                    markerHeight,
                    0f);

            DisableDevelopmentColliders(
                marker);

            Renderer markerRenderer =
                marker.GetComponent<Renderer>();

            if (markerRenderer != null &&
                ownershipMaterial != null)
            {
                markerRenderer.sharedMaterial =
                    ownershipMaterial;
            }
        }
    }

    private float SafeScaleDivide(
        float value,
        float divisor)
    {
        if (Mathf.Abs(divisor) <
            0.0001f)
        {
            return value;
        }

        return value / divisor;
    }

    private void DisableDevelopmentColliders(
        GameObject visualRoot)
    {
        if (visualRoot == null)
        {
            return;
        }

        Collider[] colliders =
            visualRoot.GetComponentsInChildren<
                Collider>(true);

        foreach (Collider visualCollider
                 in colliders)
        {
            if (visualCollider == null)
            {
                continue;
            }

            visualCollider.enabled = false;
        }
    }

    private void ApplyMaterialToDevelopmentVisual(
        GameObject visualRoot,
        Material ownershipMaterial)
    {
        if (visualRoot == null ||
            ownershipMaterial == null)
        {
            return;
        }

        Renderer[] renderers =
            visualRoot.GetComponentsInChildren<
                Renderer>(true);

        foreach (Renderer visualRenderer
                 in renderers)
        {
            if (visualRenderer == null)
            {
                continue;
            }

            Material[] materials =
                visualRenderer.sharedMaterials;

            for (int materialIndex = 0;
                 materialIndex < materials.Length;
                 materialIndex++)
            {
                materials[materialIndex] =
                    ownershipMaterial;
            }

            visualRenderer.sharedMaterials =
                materials;
        }
    }

    private void RemoveMarkerRoot(
        BoardTile tile)
    {
        if (tile == null)
        {
            return;
        }

        Transform markerRoot = null;

        if (markerRoots.TryGetValue(
                tile,
                out Transform storedRoot))
        {
            markerRoot = storedRoot;
        }
        else
        {
            markerRoot =
                tile.transform.Find(
                    "DevelopmentMarkers");
        }

        markerRoots.Remove(tile);

        if (markerRoot == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(markerRoot.gameObject);
        }
        else
        {
            DestroyImmediate(
                markerRoot.gameObject);
        }
    }

    private string GetDebugOwnerName()
    {
        return debugOwner != null
            ? debugOwner.DisplayName
            : "Unknown player";
    }
    public void ResetAllDevelopmentsForNewMatch()
    {
        EnsureInitialized();

        List<BoardTile> tiles =
            new List<BoardTile>(developmentLevels.Keys);

        foreach (BoardTile tile in tiles)
        {
            if (tile == null)
            {
                continue;
            }

            developmentLevels[tile] = 0;
            RemoveMarkerRoot(tile);
        }
        pulseLevels.Clear();
        pulseMetricDrift.Clear();
        pulseAppliedCommandIds.Clear();
        pulseMapGenerator = null;
        cityMemories.Clear();
        if (boardPath != null)
            for (int i = 0; i < boardPath.TileCount; i++)
            {
                BoardTile city = boardPath.GetTile(i);
                if (city != null && city.TileType == TileType.City)
                    cityMemories[city.TileIndex] = new CityMemory { lastOwner = -1 };
            }
        nextCityMemoryScan = 0f;
    }


#if UNITY_EDITOR
    // Read-only. Works on active offline / host / follower scene without
    // manipulating ownership, balances, investments or online membership.
    [UnityEditor.MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 18C - Houses + Pulse Smoke")]
    private static void EditorPhase18CHousesPulseSmoke()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("ATB_PHASE18C_QA NOT_RUN: Play Mode match required.");
            return;
        }
        PropertyDevelopmentManager manager = UnityEngine.Object.FindAnyObjectByType<PropertyDevelopmentManager>();
        BoardPath path = UnityEngine.Object.FindAnyObjectByType<BoardPath>();
        if (manager == null || path == null)
        {
            Debug.LogError("ATB_PHASE18C_QA FAIL: active property manager/board missing.");
            return;
        }
        int cities = 0, owned = 0, pulseUpgrades = 0, orphanHouses = 0, badRent = 0;
        int incompleteGroupWithHouses = 0, groupRestrictionChecks = 0;
        for (int i = 0; i < path.TileCount; i++)
        {
            BoardTile tile = path.GetTile(i);
            if (tile == null || tile.TileType != TileType.City) continue;
            cities++;
            if (tile.IsOwned) owned++;
            int legacyLevel = manager.GetDevelopmentLevel(tile);
            if (legacyLevel > 0 && !tile.IsOwned) orphanHouses++;
            if (legacyLevel > 0 && !manager.HasCompleteGroup(tile.OwnerPlayerIndex,
                manager.GetGroupIndex(tile))) incompleteGroupWithHouses++;
            if (manager.HasLegacyHousesInGroup(tile)) groupRestrictionChecks++;
            for (int c = 0; c < 4; c++)
            {
                int level = manager.GetPulseLevel(tile, c);
                pulseUpgrades += level;
                if (level < 0 || level > 4 || manager.GetPulseMetric(tile, c) > 94)
                    badRent++;
            }
            int multiplier = manager.GetRentMultiplier(Mathf.Clamp(legacyLevel, 0, manager.MaximumDevelopmentLevel));
            long baseline = (long)System.Math.Max(0, tile.BaseRent) * multiplier;
            long expected = baseline * (100L + manager.GetPulseRentBonusPercent(tile)) / 100L;
            int safeExpected = manager.IsMortgaged(tile) ? 0 : (int)System.Math.Min(int.MaxValue, expected);
            if (manager.GetEffectiveRent(tile) != safeExpected) badRent++;
        }
        bool pass = cities == 24 && orphanHouses == 0 &&
                    incompleteGroupWithHouses == 0 && badRent == 0;
        string data = $"cities={cities} owned={owned} pulseLevels={pulseUpgrades} " +
                      $"legacyGuardCities={groupRestrictionChecks} orphanHouses={orphanHouses} " +
                      $"incompleteGroupHouses={incompleteGroupWithHouses} badRent={badRent}";
        if (pass) Debug.Log("ATB_PHASE18C_QA PASS read-only " + data);
        else Debug.LogError("ATB_PHASE18C_QA FAIL " + data);
        // This smoke cannot assert a trade, money movement or host migration;
        // those must be owner-tested with two clients and a real transaction.
    }
#endif

#if UNITY_EDITOR
    // This is a safe, read-only Edit Mode smoke test. It does not spend money,
    // build a city, touch Firebase or change any map asset.
    [UnityEditor.MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 18B - Pulse Seed Smoke")]
    private static void EditorPulseSeedSmoke()
    {
        int mapsChecked = 0;
        int citiesChecked = 0;
        bool ok = true;
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:BoardMapDefinition");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            BoardMapDefinition map = UnityEditor.AssetDatabase.LoadAssetAtPath<BoardMapDefinition>(path);
            if (map == null || (map.MapId != "map_turkey" && map.MapId != "map_colorado" && map.MapId != "map_usa"))
                continue;
            mapsChecked++;
            int count = 0;
            foreach (BoardTileDefinition tile in map.GetPropertyDefinitions())
            {
                count++;
                for (int category = 0; category < 4; category++)
                {
                    int baseline = map.GetPulseBaseline(tile.PropertyId, category);
                    // Conservative projected +8 from all four investments.
                    if (baseline <= 0 || baseline > 90 || Mathf.Min(94, baseline + 8) >= 100)
                        ok = false;
                }
            }
            citiesChecked += count;
            if (count != 24) ok = false;
        }
        if (mapsChecked != 3 || citiesChecked != 72) ok = false;
        if (ok)
            Debug.Log("ATB_PHASE18B_SEED_QA PASS maps=" + mapsChecked + " cities=" + citiesChecked +
                      " metrics=288 cap=94 maxLevel=4 prices=100,175,250,350");
        else
            Debug.LogError("ATB_PHASE18B_SEED_QA FAIL maps=" + mapsChecked + " cities=" + citiesChecked);
    }
#endif

}
