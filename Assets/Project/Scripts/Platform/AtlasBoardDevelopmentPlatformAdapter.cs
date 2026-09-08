using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public sealed class AtlasBoardDevelopmentPlatformAdapter :
    IAtlasIdentityProvider,
    IAtlasInviteProvider,
    IAtlasIncomingInviteProvider,
    IAtlasPresenceProvider,
    IAtlasAchievementProjectionProvider
{
    private readonly HashSet<string> projectedAchievements =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

    private AtlasPlayerIdentity currentIdentity =
        new AtlasPlayerIdentity();

    private AtlasBoardPlatformPresence currentPresence =
        new AtlasBoardPlatformPresence();

    public string ProviderId => "atlas_development";
    public AtlasPlatformKind Platform { get; private set; } =
        AtlasPlatformKind.Unknown;

    public bool IsSignedIn =>
        currentIdentity != null &&
        currentIdentity.IsValid;

    public AtlasPlayerIdentity CurrentIdentity =>
        currentIdentity != null
            ? currentIdentity.Clone()
            : null;

    public bool SupportsNativeInvites => false;
    public bool SupportsShareableJoinLink => true;
    public bool SupportsIncomingInvites => true;
    public bool SupportsRichPresence => true;
    public bool SupportsAchievementProjection => true;

    public AtlasBoardPlatformPresence CurrentPresence =>
        currentPresence != null
            ? currentPresence.Clone()
            : new AtlasBoardPlatformPresence();

    public int ProjectedAchievementCount =>
        projectedAchievements.Count;

    public event Action<string> JoinRequested;

    public void BindCanonicalIdentity(
        string accountId,
        string displayName,
        AtlasPlatformKind platform)
    {
        Platform = platform;

        string normalizedAccountId =
            accountId != null
                ? accountId.Trim()
                : string.Empty;

        currentIdentity =
            new AtlasPlayerIdentity
            {
                AccountId = normalizedAccountId,
                DisplayName =
                    string.IsNullOrWhiteSpace(displayName)
                        ? "Atlas Player"
                        : displayName.Trim(),
                Platform = platform,
                PlatformUserId =
                    string.IsNullOrWhiteSpace(
                        normalizedAccountId)
                        ? string.Empty
                        : "dev:" +
                          platform.ToString().ToLowerInvariant() +
                          ":" +
                          normalizedAccountId
            };
    }

    public Task<bool> EnsureSignedInAsync()
    {
        return Task.FromResult(IsSignedIn);
    }

    public Task<bool> ShowNativeInviteAsync(
        AtlasRoomDescriptor room)
    {
        return Task.FromResult(false);
    }

    public string BuildShareableJoinLink(
        AtlasRoomDescriptor room)
    {
        return room != null
            ? AtlasBoardPlatformJoinPayload
                .BuildShareableLink(room.RoomCode)
            : string.Empty;
    }

    public Task<bool> SetPresenceAsync(
        AtlasBoardPlatformPresence presence)
    {
        currentPresence =
            presence != null
                ? presence.Clone()
                : new AtlasBoardPlatformPresence();

        return Task.FromResult(true);
    }

    public Task ClearPresenceAsync()
    {
        currentPresence =
            new AtlasBoardPlatformPresence();

        return Task.CompletedTask;
    }

    public Task<AtlasBoardAchievementProjectionResult>
        SynchronizeAchievementsAsync(
            IReadOnlyCollection<string> unlockedAchievementIds)
    {
        string[] normalized =
            unlockedAchievementIds == null
                ? Array.Empty<string>()
                : unlockedAchievementIds
                    .Where(
                        item =>
                            !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

        int before =
            projectedAchievements.Count;

        foreach (string achievementId in normalized)
        {
            projectedAchievements.Add(
                achievementId);
        }

        return Task.FromResult(
            AtlasBoardAchievementProjectionResult.Ok(
                normalized.Length,
                projectedAchievements.Count - before,
                projectedAchievements.Count));
    }

    public void SimulateIncomingInvite(
        string payload)
    {
        if (!AtlasBoardPlatformJoinPayload.TryParse(
                payload,
                out string roomCode))
        {
            return;
        }

        JoinRequested?.Invoke(roomCode);
    }
}
