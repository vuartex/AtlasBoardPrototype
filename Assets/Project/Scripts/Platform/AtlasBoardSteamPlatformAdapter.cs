using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

using Steamworks;

public sealed class AtlasBoardSteamPlatformAdapter :
    IAtlasIdentityProvider,
    IAtlasInviteProvider,
    IAtlasIncomingInviteProvider,
    IAtlasPresenceProvider,
    IAtlasAchievementProjectionProvider
{
    private AtlasPlayerIdentity currentIdentity =
        new AtlasPlayerIdentity
        {
            Platform = AtlasPlatformKind.Steam
        };

    private string canonicalAccountId =
        string.Empty;

    private string canonicalDisplayName =
        string.Empty;

    private string currentConnectString =
        string.Empty;

    private string lastError =
        string.Empty;

    private bool initialized;
    private bool ownsSteamApiInitialization;
    private bool shutdown;

    private uint currentAppId;
    private ulong currentSteamId;
    private string currentPersonaName =
        string.Empty;

    private int projectedAchievementCount;

    private Callback<GameRichPresenceJoinRequested_t>
        richPresenceJoinRequested;

    private Callback<UserStatsStored_t>
        userStatsStored;

    public static bool IsSteamworksCompiled
    {
        get
        {
            return true;
        }
    }

    public string ProviderId => "steam";
    public AtlasPlatformKind Platform =>
        AtlasPlatformKind.Steam;

    public bool IsSignedIn =>
        initialized &&
        currentSteamId != 0;

    public AtlasPlayerIdentity CurrentIdentity =>
        currentIdentity != null
            ? currentIdentity.Clone()
            : null;

    public bool SupportsNativeInvites =>
        initialized;

    public bool SupportsShareableJoinLink => true;
    public bool SupportsIncomingInvites =>
        initialized;

    public bool SupportsRichPresence =>
        initialized;

    public bool SupportsAchievementProjection =>
        initialized;

    public bool IsInitialized => initialized;
    public uint CurrentAppId => currentAppId;
    public ulong CurrentSteamId => currentSteamId;
    public string CurrentPersonaName =>
        currentPersonaName ?? string.Empty;
    public string LastError =>
        lastError ?? string.Empty;

    public int ProjectedAchievementCount =>
        projectedAchievementCount;

    public event Action<string> JoinRequested;

    public void BindCanonicalIdentity(
        string accountId,
        string displayName)
    {
        canonicalAccountId =
            accountId != null
                ? accountId.Trim()
                : string.Empty;

        canonicalDisplayName =
            displayName != null
                ? displayName.Trim()
                : string.Empty;

        RefreshIdentitySnapshot();
    }

    public Task<bool> EnsureSignedInAsync()
    {
        return Task.FromResult(
            EnsureInitialized());
    }

    public void RunCallbacks()
    {
        if (!initialized ||
            shutdown)
        {
            return;
        }

        try
        {
            SteamAPI.RunCallbacks();
        }
        catch (Exception exception)
        {
            lastError =
                "Steam callback pump failed: " +
                exception.Message;
        }
    }

    public void Shutdown()
    {
        if (shutdown)
        {
            return;
        }

        shutdown = true;

        if (initialized)
        {
            try
            {
                SteamFriends.ClearRichPresence();
            }
            catch
            {
                // Best-effort cleanup during process shutdown.
            }
        }

        richPresenceJoinRequested?.Dispose();
        richPresenceJoinRequested = null;

        userStatsStored?.Dispose();
        userStatsStored = null;

        if (initialized &&
            ownsSteamApiInitialization)
        {
            try
            {
                SteamAPI.Shutdown();
            }
            catch
            {
                // Steam teardown must never prevent application shutdown.
            }
        }

        initialized = false;
        ownsSteamApiInitialization = false;
    }

    public Task<bool> ShowNativeInviteAsync(
        AtlasRoomDescriptor room)
    {
        if (!initialized ||
            room == null)
        {
            return Task.FromResult(false);
        }

        try
        {
            string connect =
                AtlasBoardPlatformJoinPayload
                    .BuildSteamConnectString(
                        room.RoomCode);

            if (string.IsNullOrWhiteSpace(connect))
            {
                lastError =
                    "Steam invite could not build a valid room connect string.";

                return Task.FromResult(false);
            }

            currentConnectString =
                connect;

            // Direct invites may target an explicitly selected friend even for
            // a private room. The payload contains only the six-digit room
            // code; a private-room password is never included.
            SteamFriends.ActivateGameOverlayInviteDialogConnectString(
                currentConnectString);

            return Task.FromResult(true);
        }
        catch (Exception exception)
        {
            lastError =
                "Steam Friends overlay failed: " +
                exception.Message;

            return Task.FromResult(false);
        }
    }

    public string BuildShareableJoinLink(
        AtlasRoomDescriptor room)
    {
        return room != null
            ? AtlasBoardPlatformJoinPayload
                .BuildShareableLink(
                    room.RoomCode)
            : string.Empty;
    }

    public void SetConnectString(
        string connectString)
    {
        currentConnectString =
            connectString != null
                ? connectString.Trim()
                : string.Empty;
    }

    public Task<bool> SetPresenceAsync(
        AtlasBoardPlatformPresence presence)
    {
        if (!initialized)
        {
            return Task.FromResult(false);
        }

        try
        {
            AtlasBoardPlatformPresence safe =
                presence != null
                    ? presence
                    : new AtlasBoardPlatformPresence();

            SteamFriends.ClearRichPresence();

            bool success = true;

            success &=
                SteamFriends.SetRichPresence(
                    "status",
                    BuildStatusText(safe));

            success &=
                SteamFriends.SetRichPresence(
                    "atlas_state",
                    safe.State.ToString());

            if (!string.IsNullOrWhiteSpace(
                    safe.MapId))
            {
                success &=
                    SteamFriends.SetRichPresence(
                        "atlas_map",
                        safe.MapId);
            }

            success &=
                SteamFriends.SetRichPresence(
                    "atlas_players",
                    $"{Mathf.Max(0, safe.CurrentPlayers)}/" +
                    $"{Mathf.Max(1, safe.MaxPlayers)}");

            if (safe.Joinable &&
                !string.IsNullOrWhiteSpace(
                    currentConnectString))
            {
                success &=
                    SteamFriends.SetRichPresence(
                        "connect",
                        currentConnectString);
            }

            return Task.FromResult(success);
        }
        catch (Exception exception)
        {
            lastError =
                "Steam Rich Presence failed: " +
                exception.Message;

            return Task.FromResult(false);
        }
    }

    public Task ClearPresenceAsync()
    {
        if (initialized)
        {
            try
            {
                SteamFriends.ClearRichPresence();
            }
            catch (Exception exception)
            {
                lastError =
                    "Steam Rich Presence clear failed: " +
                    exception.Message;
            }
        }

        return Task.CompletedTask;
    }

    public Task<AtlasBoardAchievementProjectionResult>
        SynchronizeAchievementsAsync(
            IReadOnlyCollection<string> unlockedAchievementIds)
    {
        if (!initialized)
        {
            return Task.FromResult(
                AtlasBoardAchievementProjectionResult.Fail(
                    "Steam is not initialized."));
        }

        string[] canonicalIds =
            unlockedAchievementIds == null
                ? Array.Empty<string>()
                : unlockedAchievementIds
                    .Where(
                        item =>
                            !string.IsNullOrWhiteSpace(item))
                    .Select(
                        item => item.Trim())
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();

        int newlyProjected = 0;
        int totalProjected = 0;
        List<string> failures =
            new List<string>();

        try
        {
            foreach (string atlasId in canonicalIds)
            {
                if (!AtlasBoardSteamAchievementCatalog
                        .TryGetSteamApiName(
                            atlasId,
                            out string steamApiName))
                {
                    failures.Add(
                        atlasId +
                        " -> no Steam mapping");
                    continue;
                }

                if (!SteamUserStats.GetAchievement(
                        steamApiName,
                        out bool alreadyUnlocked))
                {
                    failures.Add(
                        steamApiName +
                        " -> not configured for AppID " +
                        currentAppId);
                    continue;
                }

                if (alreadyUnlocked)
                {
                    totalProjected++;
                    continue;
                }

                if (!SteamUserStats.SetAchievement(
                        steamApiName))
                {
                    failures.Add(
                        steamApiName +
                        " -> SetAchievement failed");
                    continue;
                }

                newlyProjected++;
                totalProjected++;
            }

            if (newlyProjected > 0 &&
                !SteamUserStats.StoreStats())
            {
                failures.Add(
                    "SteamUserStats.StoreStats failed");
            }

            projectedAchievementCount =
                Mathf.Max(
                    projectedAchievementCount,
                    totalProjected);

            if (failures.Count > 0)
            {
                return Task.FromResult(
                    AtlasBoardAchievementProjectionResult.Fail(
                        "Steam achievement projection incomplete. " +
                        string.Join(
                            "; ",
                            failures.Take(4)) +
                        (failures.Count > 4
                            ? $" (+{failures.Count - 4} more)"
                            : string.Empty)));
            }

            return Task.FromResult(
                AtlasBoardAchievementProjectionResult.Ok(
                    canonicalIds.Length,
                    newlyProjected,
                    totalProjected));
        }
        catch (Exception exception)
        {
            lastError =
                "Steam achievement projection failed: " +
                exception.Message;

            return Task.FromResult(
                AtlasBoardAchievementProjectionResult.Fail(
                    lastError));
        }
    }

    private bool EnsureInitialized()
    {
        if (initialized)
        {
            RefreshSteamIdentity();
            return true;
        }

        if (shutdown)
        {
            lastError =
                "Steam adapter was already shut down.";
            return false;
        }

        try
        {
#if UNITY_EDITOR
            uint editorAppId =
                AtlasBoardSteamRuntimeSettings
                    .DevelopmentAppId;

            Environment.SetEnvironmentVariable(
                "SteamAppId",
                editorAppId.ToString());

            Environment.SetEnvironmentVariable(
                "SteamGameId",
                editorAppId.ToString());
#endif

            if (!SteamAPI.Init())
            {
                lastError =
                    "SteamAPI.Init returned false. Ensure the Steam client " +
                    "is running and the development AppID is available.";

                return false;
            }

            ownsSteamApiInitialization = true;
            initialized = true;

            RegisterCallbacks();
            RefreshSteamIdentity();

            lastError = string.Empty;

            Debug.Log(
                "AtlasBoard Steam provider initialized. " +
                $"AppID={currentAppId}, " +
                $"SteamID={currentSteamId}, " +
                $"Persona={currentPersonaName}.");

            return currentSteamId != 0;
        }
        catch (DllNotFoundException exception)
        {
            lastError =
                "Steam native library not found: " +
                exception.Message;
        }
        catch (Exception exception)
        {
            lastError =
                "Steam initialization failed: " +
                exception.Message;
        }

        initialized = false;
        ownsSteamApiInitialization = false;

        return false;
    }

    private void RegisterCallbacks()
    {
        richPresenceJoinRequested =
            Callback<GameRichPresenceJoinRequested_t>
                .Create(
                    HandleRichPresenceJoinRequested);

        userStatsStored =
            Callback<UserStatsStored_t>
                .Create(
                    HandleUserStatsStored);
    }

    private void HandleRichPresenceJoinRequested(
        GameRichPresenceJoinRequested_t callback)
    {
        string payload =
            callback.m_rgchConnect ?? string.Empty;

        if (AtlasBoardPlatformJoinPayload.TryParse(
                payload,
                out string roomCode))
        {
            JoinRequested?.Invoke(roomCode);
        }
    }

    private void HandleUserStatsStored(
        UserStatsStored_t callback)
    {
        if (callback.m_eResult ==
            EResult.k_EResultOK)
        {
            return;
        }

        lastError =
            "Steam StoreStats callback returned " +
            callback.m_eResult + ".";
    }

    private void RefreshSteamIdentity()
    {
        if (!initialized)
        {
            return;
        }

        currentAppId =
            SteamUtils.GetAppID().m_AppId;

        CSteamID steamId =
            SteamUser.GetSteamID();

        currentSteamId =
            steamId.m_SteamID;

        currentPersonaName =
            SteamFriends.GetPersonaName() ??
            string.Empty;

        RefreshIdentitySnapshot();
    }

    private void RefreshIdentitySnapshot()
    {
        currentIdentity =
            new AtlasPlayerIdentity
            {
                AccountId =
                    canonicalAccountId,
                DisplayName =
                    !string.IsNullOrWhiteSpace(
                        currentPersonaName)
                        ? currentPersonaName
                        : canonicalDisplayName,
                Platform =
                    AtlasPlatformKind.Steam,
                PlatformUserId =
                    currentSteamId != 0
                        ? currentSteamId.ToString()
                        : string.Empty
            };
    }

    private static string BuildStatusText(
        AtlasBoardPlatformPresence presence)
    {
        switch (presence.State)
        {
            case AtlasBoardPlatformPresenceState.Lobby:
                return
                    "In Lobby" +
                    BuildPresenceSuffix(presence);
            case AtlasBoardPlatformPresenceState.Match:
                return
                    "In Match" +
                    BuildPresenceSuffix(presence);
            case AtlasBoardPlatformPresenceState.Result:
                return
                    "Viewing Match Result";
            default:
                return
                    "At Main Menu";
        }
    }

    private static string BuildPresenceSuffix(
        AtlasBoardPlatformPresence presence)
    {
        string map =
            string.IsNullOrWhiteSpace(
                presence.MapId)
                ? string.Empty
                : " • " + presence.MapId;

        return map +
               " • " +
               Mathf.Max(0, presence.CurrentPlayers) +
               "/" +
               Mathf.Max(1, presence.MaxPlayers);
    }
}
