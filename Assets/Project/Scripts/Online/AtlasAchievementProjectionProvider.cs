using System.Collections.Generic;
using System.Threading.Tasks;

public interface IAtlasAchievementProjectionProvider
{
    string ProviderId { get; }
    bool SupportsAchievementProjection { get; }

    Task<AtlasBoardAchievementProjectionResult>
        SynchronizeAchievementsAsync(
            IReadOnlyCollection<string> unlockedAchievementIds);
}
