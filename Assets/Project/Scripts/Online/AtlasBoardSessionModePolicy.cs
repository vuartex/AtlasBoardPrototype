using System;

/// <summary>
/// Canonical AtlasBoard session/cost policy.
///
/// This is deliberately transport-agnostic. It records the product/security
/// contract so future Firebase RTDB, Firestore, Steam and mobile adapters make
/// the same decision about rewards, authority and network requirements.
/// </summary>
public enum AtlasBoardSessionMode
{
    LocalOffline = 0,
    PrivateCasualOnline = 1,
    OnlineRewarded = 2
}

public enum AtlasBoardLiveTransport
{
    LocalOnly = 0,
    FirebaseRealtimeDatabase = 1
}

public readonly struct AtlasBoardSessionCapabilities
{
    public AtlasBoardSessionMode Mode { get; }
    public AtlasBoardLiveTransport LiveTransport { get; }
    public bool RequiresInternet { get; }
    public bool UsesHostAuthoritativeSimulation { get; }
    public bool UsesCloudFunctionsForLivePolling { get; }
    public bool PersistentProgressionEligible { get; }
    public bool CompetitiveVerificationRequired { get; }
    public bool PersistMatchHistoryByDefault { get; }

    public AtlasBoardSessionCapabilities(
        AtlasBoardSessionMode mode,
        AtlasBoardLiveTransport liveTransport,
        bool requiresInternet,
        bool usesHostAuthoritativeSimulation,
        bool usesCloudFunctionsForLivePolling,
        bool persistentProgressionEligible,
        bool competitiveVerificationRequired,
        bool persistMatchHistoryByDefault)
    {
        Mode = mode;
        LiveTransport = liveTransport;
        RequiresInternet = requiresInternet;
        UsesHostAuthoritativeSimulation = usesHostAuthoritativeSimulation;
        UsesCloudFunctionsForLivePolling = usesCloudFunctionsForLivePolling;
        PersistentProgressionEligible = persistentProgressionEligible;
        CompetitiveVerificationRequired = competitiveVerificationRequired;
        PersistMatchHistoryByDefault = persistMatchHistoryByDefault;
    }
}

public static class AtlasBoardSessionModePolicy
{
    public static AtlasBoardSessionCapabilities Get(
        AtlasBoardSessionMode mode)
    {
        switch (mode)
        {
            case AtlasBoardSessionMode.LocalOffline:
                return new AtlasBoardSessionCapabilities(
                    mode,
                    AtlasBoardLiveTransport.LocalOnly,
                    requiresInternet: false,
                    usesHostAuthoritativeSimulation: true,
                    usesCloudFunctionsForLivePolling: false,
                    persistentProgressionEligible: false,
                    competitiveVerificationRequired: false,
                    persistMatchHistoryByDefault: false);

            case AtlasBoardSessionMode.PrivateCasualOnline:
                return new AtlasBoardSessionCapabilities(
                    mode,
                    AtlasBoardLiveTransport.FirebaseRealtimeDatabase,
                    requiresInternet: true,
                    usesHostAuthoritativeSimulation: true,
                    usesCloudFunctionsForLivePolling: false,
                    persistentProgressionEligible: false,
                    competitiveVerificationRequired: false,
                    persistMatchHistoryByDefault: false);

            case AtlasBoardSessionMode.OnlineRewarded:
                return new AtlasBoardSessionCapabilities(
                    mode,
                    AtlasBoardLiveTransport.FirebaseRealtimeDatabase,
                    requiresInternet: true,
                    usesHostAuthoritativeSimulation: true,
                    usesCloudFunctionsForLivePolling: false,
                    persistentProgressionEligible: true,
                    competitiveVerificationRequired: true,
                    persistMatchHistoryByDefault: true);

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mode),
                    mode,
                    "Unknown AtlasBoard session mode.");
        }
    }

    /// <summary>
    /// Permanent XP/wins/achievements require an OnlineRewarded session and at
    /// least two distinct authenticated human accounts. Local humans and bots
    /// on one device never make an otherwise-offline match reward eligible.
    /// Independent server-authoritative result verification is still required
    /// before any permanent reward is minted.
    /// </summary>
    public static bool CanEverGrantPersistentProgression(
        AtlasBoardSessionMode mode,
        int distinctAuthenticatedHumanAccounts)
    {
        return mode == AtlasBoardSessionMode.OnlineRewarded &&
               distinctAuthenticatedHumanAccounts >= 2;
    }

    public static bool IsFreePlay(
        AtlasBoardSessionMode mode)
    {
        return mode == AtlasBoardSessionMode.LocalOffline ||
               mode == AtlasBoardSessionMode.PrivateCasualOnline;
    }
}
