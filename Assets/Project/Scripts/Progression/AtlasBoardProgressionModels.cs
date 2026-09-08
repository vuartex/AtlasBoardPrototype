using System;
using System.Collections.Generic;

[Serializable]
public sealed class AtlasBoardCareerStats
{
    public int matchesPlayed;
    public int wins;
    public int losses;
    public int ties;
    public int bankruptcies;
    public int diceRolls;
    public int doublesRolled;
    public int totalDiceValue;
    public int highestRoll;
    public int propertiesAtFinishTotal;
    public int developmentLevelsAtFinishTotal;
    public int totalRounds;
    public int totalTurns;
    public int totalNetWorth;
    public int highestNetWorth;
    public int highestCash;
    public int currentWinStreak;
    public int bestWinStreak;
    public int mapTurkey;
    public int mapColorado;
    public int mapUsa;
    public int mapOther;
    public int mapWinsTurkey;
    public int mapWinsColorado;
    public int mapWinsUsa;
    public int mapWinsOther;
}

[Serializable]
public sealed class AtlasBoardMapCareerStats
{
    public string mapId;
    public AtlasBoardCareerStats stats = new AtlasBoardCareerStats();
}

[Serializable]
public sealed class AtlasBoardProgressionAchievement
{
    public string achievementId;
    public string titleKey;
    public string descriptionKey;
    public int target;
    public int progress;
    public bool unlocked;
}

[Serializable]
public sealed class AtlasBoardProgressionParticipant
{
    public int slotIndex;
    public string displayName;
    public string controllerKind;
    public bool isHost;
    public bool bankrupt;
    public int place;
    public string result;
    public int cash;
    public int propertyCount;
    public int propertyValue;
    public int developmentLevels;
    public int developmentValue;
    public int netWorth;
}

[Serializable]
public sealed class AtlasBoardProgressionMatchSummary
{
    public string matchId;
    public string lobbyId;
    public string hostAccountId;
    public string mode;
    public string mapId;
    public string themeId;
    public int roundLimit;
    public int maxPlayers;
    public string result;
    public int place;
    public int playerCount;
    public int localSlotIndex;
    public bool bankrupt;
    public int finalCash;
    public int propertyCount;
    public int propertyValue;
    public int developmentLevels;
    public int developmentValue;
    public int netWorth;
    public int highestMatchNetWorth;
    public int completedRounds;
    public int completedTurns;
    public int diceRolls;
    public int doublesRolled;
    public int totalDiceValue;
    public int highestRoll;
    public int xpAwarded;
    public int levelAfter;
    public long finishedAtEpochMs;
    public AtlasBoardProgressionParticipant[] participants;
}

[Serializable]
public sealed class AtlasBoardProgressionSnapshot
{
    public bool Success;
    public string ErrorKey;
    public string TechnicalMessage;
    public string AccountId;
    public string DisplayName;
    public string AvatarId;
    public int TotalXp;
    public int Level = 1;
    public int LevelStartXp;
    public int NextLevelXp;
    public int MaxLevel = 100;
    public int HistoryRetentionHours = 72;
    public int HistoryLimit = 10;
    public AtlasBoardCareerStats Stats = new AtlasBoardCareerStats();
    public List<AtlasBoardMapCareerStats> MapStats =
        new List<AtlasBoardMapCareerStats>();
    public List<AtlasBoardProgressionAchievement> Achievements =
        new List<AtlasBoardProgressionAchievement>();
    public List<AtlasBoardProgressionMatchSummary> RecentMatches =
        new List<AtlasBoardProgressionMatchSummary>();
}

[Serializable]
public sealed class AtlasBoardProgressionSlotTelemetry
{
    public int slotIndex;
    public int diceRolls;
    public int doublesRolled;
    public int totalDiceValue;
    public int highestRoll;
}

[Serializable]
public sealed class AtlasBoardProgressionRecordResult
{
    public bool Success;
    public string ErrorKey;
    public string TechnicalMessage;
    public string MatchId;
    public bool Applied;
    public bool IdempotentReplay;
    public int AffectedAccountCount;
}
