#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Editor-only validation. Never mutates live matches, RTDB, Firebase state or map assets.
// This is a balance MODEL / read-only integration diagnostic, not an automated game.
public static class AtlasBoardPhase18HIntegrationAudit
{
    private const int NormalBoardTiles = 32;
    private const int Rounds = 20;
    private static readonly string[] Maps =
    {
        "map_turkey", "map_colorado", "map_usa"
    };
    private static readonly int[] CivicPrices = { 650, 800, 950 };
    private static readonly int[] CivicStartIncome = { 45, 55, 100 };

    private sealed class Scenario
    {
        public readonly long[] cash;
        public readonly int[] position;
        public readonly int[] owner = new int[NormalBoardTiles];
        public readonly int[] pulse = new int[NormalBoardTiles];
        public readonly int[] debt = new int[NormalBoardTiles];
        public readonly int[] dueTurn = new int[NormalBoardTiles];
        public readonly int[] civicOwner = { -1, -1, -1 };
        public long minted;
        public long burned;
        public int failures;
        public int loans;
        public int foreclosure;
        public int citiesBought;
        public int civicBought;
        public int maxRent;
        public long peakCash;
        public long lowestNetWorth = long.MaxValue;
        public long highestNetWorth = long.MinValue;
        public int state = 0x35459;

        public Scenario(int players, int startingMoney, int seed)
        {
            cash = new long[players];
            position = new int[players];
            for (int i = 0; i < players; i++) cash[i] = startingMoney;
            for (int i = 0; i < owner.Length; i++) owner[i] = -1;
            state ^= seed;
        }

        public int Next(int maxExclusive)
        {
            unchecked
            {
                // No UnityEngine.Random or shared game RNG, no gameplay side effects.
                uint x = (uint)state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                state = (int)x;
                return (int)(x % (uint)maxExclusive);
            }
        }
    }

    [MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 18H - 20 Round Balance Model")]
    private static void BalanceModel()
    {
        int ok = 0;
        int invalid = 0;
        foreach (string id in Maps)
        {
            string path = "Assets/Project/Data/Maps/" + id + ".asset";
            BoardMapDefinition map = AssetDatabase.LoadAssetAtPath<BoardMapDefinition>(path);
            if (map == null || map.Tiles == null || map.Tiles.Count != NormalBoardTiles)
            {
                Debug.LogError("ATB_PHASE18H_BALANCE_QA FAIL mapAsset=" + path + " tiles!=32");
                invalid++;
                continue;
            }
            BoardEconomyProfile profile = map.EconomyProfile;
            if (profile == null)
            {
                Debug.LogError("ATB_PHASE18H_BALANCE_QA FAIL economyProfileMissing=" + path);
                invalid++;
                continue;
            }
            BoardTileDefinition[] tile = new BoardTileDefinition[NormalBoardTiles];
            HashSet<int> ids = new HashSet<int>();
            int cities = 0;
            bool valid = true;
            foreach (BoardTileDefinition definition in map.Tiles)
            {
                if (definition == null || definition.TileIndex < 0 || definition.TileIndex >= NormalBoardTiles ||
                    !ids.Add(definition.TileIndex)) { valid = false; continue; }
                tile[definition.TileIndex] = definition;
                if (definition.TileType == TileType.City)
                {
                    cities++;
                    if (definition.PurchasePrice <= 0 || definition.BaseRent < 0) valid = false;
                }
            }
            if (!valid || cities != 24 || ids.Count != NormalBoardTiles)
            {
                Debug.LogError("ATB_PHASE18H_BALANCE_QA FAIL map=" + id +
                    " cities=" + cities + " tileIndices=" + ids.Count);
                invalid++;
                continue;
            }
            for (int players = 2; players <= 4; players++)
            {
                Scenario model = new Scenario(players, profile.StartingMoney,
                    StableSeed(id) + players * 137); // model seed only
                Simulate(model, tile, profile);
                long supply = Sum(model.cash);
                long expected = (long)players * profile.StartingMoney + model.minted - model.burned;
                bool pass = model.failures == 0 && supply == expected && model.lowestNetWorth >= -1000000 &&
                    model.peakCash <= 1000000;
                if (pass) ok++; else invalid++;
                Debug.Log("ATB_PHASE18H_BALANCE_SCENARIO " + (pass ? "PASS" : "FAIL") +
                    " modelOnly=true map=" + id + " players=" + players + " rounds=" + Rounds +
                    " citiesPurchased=" + model.citiesBought + " civicPurchased=" + model.civicBought +
                    " loans=" + model.loans + " foreclosures=" + model.foreclosure +
                    " minted=" + model.minted + " burned=" + model.burned +
                    " supply=" + supply + " expected=" + expected +
                    " peakCash=" + model.peakCash + " maxRent=" + model.maxRent +
                    " netWorthRange=" + model.lowestNetWorth + ".." + model.highestNetWorth);
            }
        }
        // A borrower receives cash but also owes principal + interest.
        bool loanNotProfit = MatchResultManager.CalculateDebtAdjustedNetWorth(
            1500 + 150, 600, 100, 168) == 2182;
        bool foreclosureClearsDebt = MatchResultManager.CalculateDebtAdjustedNetWorth(1500, 0, 0, 0) == 1500;
        bool saturates = MatchResultManager.CalculateDebtAdjustedNetWorth(
            int.MaxValue, int.MaxValue, 500, 0) == int.MaxValue;
        if (!loanNotProfit || !foreclosureClearsDebt || !saturates) invalid++;
        Debug.Log((invalid == 0 && ok == 9 ? "ATB_PHASE18H_BALANCE_QA PASS" : "ATB_PHASE18H_BALANCE_QA FAIL") +
            " scenarios=" + ok + "/9 invalid=" + invalid +
            " 20rounds=true deterministicAnalyticalModel=true" +
            " actualGameplayOrDeviceQA=false loanNetWorth=" + loanNotProfit +
            " overflowClamp=" + saturates);
    }

    private static void Simulate(Scenario s, BoardTileDefinition[] board, BoardEconomyProfile profile)
    {
        // Conservative *illustrative* policy. Uses actual map price/rent and
        // actual configured economy values; does NOT simulate Unity animations,
        // all Event cards, real bot timing, auctions or networking.
        for (int round = 1; round <= Rounds; round++)
        {
            for (int seat = 0; seat < s.cash.Length; seat++)
            {
                // Own-turn loan maturity: repay in full or foreclose ONLY pledged city.
                for (int t = 0; t < NormalBoardTiles; t++)
                {
                    if (s.owner[t] != seat || s.debt[t] == 0 || s.dueTurn[t] > round) continue;
                    if (s.cash[seat] >= s.debt[t])
                    {
                        s.cash[seat] -= s.debt[t];
                        s.burned += s.debt[t];
                    }
                    else
                    {
                        s.owner[t] = -1;
                        s.pulse[t] = 0;
                        s.foreclosure++;
                    }
                    s.debt[t] = 0;
                    s.dueTurn[t] = 0;
                }
                int dice = 2 + s.Next(6) + s.Next(6);
                if (s.position[seat] + dice >= NormalBoardTiles)
                {
                    int reward = profile.StartPassReward + CivicBonus(s, seat);
                    s.cash[seat] += reward;
                    s.minted += reward;
                }
                s.position[seat] = (s.position[seat] + dice) % NormalBoardTiles;
                int index = s.position[seat];
                BoardTileDefinition location = board[index];
                if (location.TileType == TileType.City)
                {
                    int price = location.PurchasePrice;
                    if (s.owner[index] < 0 && s.cash[seat] >= price + 250)
                    {
                        s.cash[seat] -= price;
                        s.burned += price;
                        s.owner[index] = seat;
                        s.citiesBought++;
                    }
                    else if (s.owner[index] >= 0 && s.owner[index] != seat && s.debt[index] == 0)
                    {
                        int rent = (int)Math.Min(int.MaxValue,
                            (long)location.BaseRent * (100 + profile.GetPulseRentBonusPercent(s.pulse[index])) / 100);
                        s.maxRent = Math.Max(s.maxRent, rent);
                        long paid = Math.Min(s.cash[seat], rent);
                        s.cash[seat] -= paid;
                        s.cash[s.owner[index]] += paid;
                    }
                    else if (s.owner[index] == seat && s.debt[index] == 0 && s.pulse[index] < 4 &&
                        s.cash[seat] >= profile.GetPulseLevelCost(s.pulse[index]) + 450 && round % 3 == 0)
                    {
                        int cost = profile.GetPulseLevelCost(s.pulse[index]);
                        s.cash[seat] -= cost;
                        s.burned += cost;
                        s.pulse[index]++;
                    }
                }
                else if (location.TileType == TileType.Tax)
                {
                    long paid = Math.Min(s.cash[seat], profile.TaxAmount);
                    s.cash[seat] -= paid;
                    s.burned += paid;
                }
                else if (location.TileType == TileType.Bonus || location.TileType == TileType.Vacation)
                {
                    int reward = location.TileType == TileType.Bonus ? profile.BonusAmount : profile.VacationBonusAmount;
                    s.cash[seat] += reward;
                    s.minted += reward;
                }
                else if (location.TileType == TileType.Travel || location.TileType == TileType.Event)
                {
                    // Sparse off-path Civic invitation (not automatic purchase).
                    if (s.Next(5) == 0)
                    {
                        for (int project = 0; project < CivicPrices.Length; project++)
                        {
                            if (s.civicOwner[project] == -1 && s.cash[seat] >= CivicPrices[project] + 350)
                            {
                                s.cash[seat] -= CivicPrices[project];
                                s.burned += CivicPrices[project];
                                s.civicOwner[project] = seat;
                                s.civicBought++;
                                break;
                            }
                        }
                    }
                }
                // Illustrative 18D credit lifeline, term-2 and interest-8%.
                if (s.cash[seat] < 100)
                {
                    for (int t = 0; t < NormalBoardTiles; t++)
                    {
                        if (s.owner[t] != seat || s.debt[t] != 0 || s.pulse[t] != 0) continue;
                        int principal = Math.Min(120, (int)((long)board[t].PurchasePrice * 70 / 100));
                        if (principal < 75) continue;
                        s.debt[t] = principal + (principal * 8 + 99) / 100;
                        s.dueTurn[t] = round + 2;
                        s.cash[seat] += principal;
                        s.minted += principal;
                        s.loans++;
                        break;
                    }
                }
                if (s.cash[seat] < 0) s.failures++;
                s.peakCash = Math.Max(s.peakCash, s.cash[seat]);
            }
            for (int seat = 0; seat < s.cash.Length; seat++)
            {
                long assetValue = 0;
                long investment = 0;
                long liability = 0;
                for (int i = 0; i < NormalBoardTiles; i++)
                {
                    if (s.owner[i] != seat) continue;
                    assetValue += board[i].PurchasePrice;
                    liability += s.debt[i];
                    for (int lvl = 0; lvl < s.pulse[i]; lvl++) investment += profile.GetPulseLevelCost(lvl);
                }
                for (int i = 0; i < 3; i++) if (s.civicOwner[i] == seat) assetValue += CivicPrices[i];
                long net = s.cash[seat] + assetValue + investment - liability;
                s.lowestNetWorth = Math.Min(net, s.lowestNetWorth);
                s.highestNetWorth = Math.Max(net, s.highestNetWorth);
            }
        }
    }

    private static int CivicBonus(Scenario s, int seat)
    {
        int amount = 0;
        for (int i = 0; i < 3; i++) if (s.civicOwner[i] == seat) amount += CivicStartIncome[i];
        return Math.Min(150, amount);
    }

    private static int StableSeed(string value)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
            return hash;
        }
    }

    private static long Sum(long[] values)
    {
        long sum = 0;
        foreach (long item in values) sum += item;
        return sum;
    }

    [MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 18H - Live Match Read-Only Integrity")]
    private static void ReadOnlyLiveMatchAudit()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("ATB_PHASE18H_LIVE_QA NOT_RUN reason=Enter_Play_Mode_First");
            return;
        }
        BoardPath path = UnityEngine.Object.FindAnyObjectByType<BoardPath>();
        PropertyDevelopmentManager economy = UnityEngine.Object.FindAnyObjectByType<PropertyDevelopmentManager>();
        AtlasBoardCivicProjectManager civic = UnityEngine.Object.FindAnyObjectByType<AtlasBoardCivicProjectManager>();
        MatchResultManager result = UnityEngine.Object.FindAnyObjectByType<MatchResultManager>();
        if (path == null || economy == null || civic == null || result == null || path.TileCount != NormalBoardTiles)
        {
            Debug.LogError("ATB_PHASE18H_LIVE_QA FAIL reason=MissingCoreOr32Tiles");
            return;
        }
        int bad = 0;
        int activeLoans = 0;
        for (int index = 0; index < NormalBoardTiles; index++)
        {
            BoardTile city = path.GetTile(index);
            if (city == null) { bad++; continue; }
            if (city.TileType != TileType.City) continue;
            if (economy.IsMortgaged(city))
            {
                activeLoans++;
                PropertyDevelopmentManager.BankLoan loan = economy.GetBankLoan(city);
                if (!city.IsOwned || loan == null || loan.ownerSlot != city.OwnerPlayerIndex ||
                    loan.outstanding <= 0 || loan.principal > economy.GetBankLimit(city) ||
                    economy.GetEffectiveRent(city) != 0) bad++;
            }
            for (int category = 0; category < 4; category++)
            {
                int level = economy.GetPulseLevel(city, category);
                int metric = economy.GetPulseMetric(city, category);
                if (level < 0 || level > 4 || metric < 0 || metric > 94) bad++;
            }
        }
        int civicCount = 0;
        for (int i = 0; i < 3; i++)
        {
            int owner = civic.GetOwnerSlot(i);
            if (owner < -1 || owner > 3) bad++;
            if (owner >= 0) civicCount++;
        }
        for (int seat = 0; seat < 4; seat++)
            if (economy.GetBankLoanCount(seat) > 3) bad++;
        MatchResultManager.OnlineResultSnapshot snap = result.BuildOnlineResultSnapshot();
        if (snap == null || !snap.valid) bad++;
        else
        {
            for (int seat = 0; seat < 4; seat++)
            {
                if (!snap.participating[seat]) continue;
                int expected = MatchResultManager.CalculateDebtAdjustedNetWorth(
                    snap.cash[seat], snap.propertyValue[seat], snap.developmentValue[seat],
                    economy.GetBankDebt(seat));
                if (snap.netWorth[seat] != expected) bad++;
            }
        }
        Debug.Log((bad == 0 ? "ATB_PHASE18H_LIVE_QA PASS" : "ATB_PHASE18H_LIVE_QA FAIL") +
            " readOnly=true boardTiles=32 activeLoans=" + activeLoans +
            " civicOwned=" + civicCount + " invalid=" + bad +
            " networkSecurityAndDeviceQA=OPEN");
    }
}
#endif
