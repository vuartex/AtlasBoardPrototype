using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class AtlasPublicLobbyPageResult
{
    public bool Success { get; private set; }
    public string ErrorLocalizationKey { get; private set; }
    public string TechnicalMessage { get; private set; }
    public IReadOnlyList<AtlasPublicLobbyCard> Rooms { get; private set; }
    public string NextCursor { get; private set; }
    public bool HasMore { get; private set; }
    public int ScannedDocuments { get; private set; }
    public int QueryCount { get; private set; }
    public bool ScanLimitReached { get; private set; }

    public static AtlasPublicLobbyPageResult Ok(
        IReadOnlyList<AtlasPublicLobbyCard> rooms,
        string nextCursor,
        bool hasMore,
        int scannedDocuments,
        int queryCount,
        bool scanLimitReached)
    {
        return new AtlasPublicLobbyPageResult
        {
            Success = true,
            ErrorLocalizationKey = string.Empty,
            TechnicalMessage = string.Empty,
            Rooms = rooms ?? Array.Empty<AtlasPublicLobbyCard>(),
            NextCursor = nextCursor ?? string.Empty,
            HasMore = hasMore,
            ScannedDocuments = Mathf.Max(0, scannedDocuments),
            QueryCount = Mathf.Max(0, queryCount),
            ScanLimitReached = scanLimitReached
        };
    }

    public static AtlasPublicLobbyPageResult Fail(
        string errorLocalizationKey,
        string technicalMessage)
    {
        return new AtlasPublicLobbyPageResult
        {
            Success = false,
            ErrorLocalizationKey = string.IsNullOrWhiteSpace(errorLocalizationKey)
                ? "lobby.error.unknown"
                : errorLocalizationKey,
            TechnicalMessage = technicalMessage ?? string.Empty,
            Rooms = Array.Empty<AtlasPublicLobbyCard>(),
            NextCursor = string.Empty,
            HasMore = false,
            ScannedDocuments = 0,
            QueryCount = 0,
            ScanLimitReached = false
        };
    }
}

/// <summary>
/// Phase 16I additive client for cursor-paginated public-lobby discovery.
/// It intentionally does not alter the canonical lobby bridge's reconnect,
/// polling, match networking, or authoritative Join path.
/// </summary>
public static class AtlasBoardPublicLobbyPagingClient
{
    private const string ProjectId = "atlasboard-usa";
    private const string Region = "europe-west1";
    private const int FunctionsEmulatorPort = 5001;
    private const string FunctionName = "lobbyListPublicRoomsPage";

    public static async Task<AtlasPublicLobbyPageResult> ListPageAsync(
        AtlasBoardLobbyRuntimeBridge runtimeBridge,
        int limit,
        string cursor = "")
    {
        if (runtimeBridge == null)
        {
            return AtlasPublicLobbyPageResult.Fail(
                "lobby.error.service_unavailable",
                "Public lobby paging client has no runtime bridge.");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float startedAt = Time.realtimeSinceStartup;
#endif

        bool identityReady =
            await runtimeBridge.EnsureOnlineIdentityAsync();

        if (!identityReady ||
            string.IsNullOrWhiteSpace(
                runtimeBridge.AuthTokenForOnlineSubsystems))
        {
            return AtlasPublicLobbyPageResult.Fail(
                "account.error.authentication_required",
                "Online identity was not ready for public lobby paging.");
        }

        PublicLobbyPageRequest request =
            new PublicLobbyPageRequest
            {
                gameVersion = CurrentGameVersion(),
                protocolVersion =
                    AtlasOnlineDefaults.ProtocolVersion,
                rulesVersion =
                    AtlasOnlineDefaults.RulesVersion,
                contentVersion = "1",
                regionId = "eur3",
                limit = Mathf.Clamp(limit, 1, 25),
                cursor = cursor ?? string.Empty
            };

        string url = runtimeBridge.UsingLocalEmulators
            ? $"http://{runtimeBridge.EmulatorHostForOnlineSubsystems}:{FunctionsEmulatorPort}/" +
              $"{ProjectId}/{Region}/{FunctionName}"
            : $"https://{Region}-{ProjectId}.cloudfunctions.net/{FunctionName}";

        string callableJson =
            "{\"data\":" + JsonUtility.ToJson(request) + "}";

        using UnityWebRequest webRequest =
            new UnityWebRequest(
                url,
                UnityWebRequest.kHttpVerbPOST);

        webRequest.uploadHandler =
            new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(callableJson));
        webRequest.downloadHandler =
            new DownloadHandlerBuffer();
        webRequest.SetRequestHeader(
            "Content-Type",
            "application/json");
        webRequest.SetRequestHeader(
            "Authorization",
            $"Bearer {runtimeBridge.AuthTokenForOnlineSubsystems}");
        webRequest.timeout = 20;

        await AtlasBoardAppCheckRequestSecurity.ApplyAsync(
            webRequest);
        await SendRequestAsync(webRequest);

        string body = webRequest.downloadHandler != null
            ? webRequest.downloadHandler.text
            : string.Empty;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log(
            "ATB_PUBLIC_DISCOVERY_PAGE " +
            $"durationMs={(Time.realtimeSinceStartup - startedAt) * 1000f:F0} " +
            $"http={webRequest.responseCode}");
#endif

        if (webRequest.result !=
            UnityWebRequest.Result.Success)
        {
            CallableErrorEnvelope error =
                SafeFromJson<CallableErrorEnvelope>(body);

            string errorKey =
                error != null &&
                error.error != null &&
                error.error.details != null &&
                !string.IsNullOrWhiteSpace(
                    error.error.details.errorKey)
                    ? error.error.details.errorKey
                    : "lobby.error.unknown";

            string technical =
                error != null &&
                error.error != null &&
                !string.IsNullOrWhiteSpace(
                    error.error.message)
                    ? error.error.message
                    : $"HTTP {webRequest.responseCode}: {webRequest.error}";

            return AtlasPublicLobbyPageResult.Fail(
                errorKey,
                technical);
        }

        CallablePageEnvelope envelope =
            SafeFromJson<CallablePageEnvelope>(body);

        if (envelope == null ||
            envelope.result == null ||
            !envelope.result.ok)
        {
            return AtlasPublicLobbyPageResult.Fail(
                "lobby.error.service_unavailable",
                "Callable returned no valid paginated public-lobby result.");
        }

        List<AtlasPublicLobbyCard> cards =
            new List<AtlasPublicLobbyCard>();

        if (envelope.result.rooms != null)
        {
            foreach (PublicLobbyWireCard wire
                     in envelope.result.rooms)
            {
                if (wire == null ||
                    string.IsNullOrWhiteSpace(wire.lobbyId))
                {
                    continue;
                }

                cards.Add(
                    new AtlasPublicLobbyCard
                    {
                        LobbyId = wire.lobbyId ?? string.Empty,
                        HostDisplayName =
                            wire.hostDisplayName ?? string.Empty,
                        MapId = wire.mapId ?? string.Empty,
                        ThemeId = wire.themeId ?? string.Empty,
                        RoundLimit = wire.roundLimit,
                        MaxPlayers = wire.maxPlayers,
                        OccupiedPlayers = wire.occupiedPlayers,
                        OpenOnlineSeatCount =
                            wire.openOnlineSeatCount,
                        RegionId = wire.regionId ?? string.Empty,
                        GameVersion =
                            wire.gameVersion ?? string.Empty,
                        ProtocolVersion = wire.protocolVersion,
                        RulesVersion = wire.rulesVersion,
                        ContentVersion =
                            wire.contentVersion ?? string.Empty,
                        SettingsRevision = wire.settingsRevision,
                        HasPassword = wire.hasPassword,
                        CreatedAtEpochMs = wire.createdAtEpochMs
                    });
            }
        }

        PublicLobbyPageDiagnostics diagnostics =
            envelope.result.diagnostics;

        return AtlasPublicLobbyPageResult.Ok(
            cards,
            envelope.result.nextCursor,
            envelope.result.hasMore,
            diagnostics != null
                ? diagnostics.scannedDocuments
                : 0,
            diagnostics != null
                ? diagnostics.queryCount
                : 0,
            diagnostics != null &&
            diagnostics.scanLimitReached);
    }

    private static string CurrentGameVersion()
    {
        return string.IsNullOrWhiteSpace(Application.version)
            ? "0.1.0"
            : Application.version;
    }

    private static async Task SendRequestAsync(
        UnityWebRequest request)
    {
        UnityWebRequestAsyncOperation operation =
            request.SendWebRequest();
        TaskCompletionSource<bool> completion =
            new TaskCompletionSource<bool>();
        operation.completed +=
            _ => completion.TrySetResult(true);
        await completion.Task;
    }

    private static T SafeFromJson<T>(string json)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    [Serializable]
    private sealed class PublicLobbyPageRequest
    {
        public string gameVersion;
        public int protocolVersion;
        public int rulesVersion;
        public string contentVersion;
        public string regionId;
        public int limit;
        public string cursor;
    }

    [Serializable]
    private sealed class PublicLobbyWireCard
    {
        public string lobbyId;
        public string hostDisplayName;
        public string mapId;
        public string themeId;
        public int roundLimit;
        public int maxPlayers;
        public int occupiedPlayers;
        public int openOnlineSeatCount;
        public string regionId;
        public string gameVersion;
        public int protocolVersion;
        public int rulesVersion;
        public string contentVersion;
        public int settingsRevision;
        public bool hasPassword;
        public long createdAtEpochMs;
    }

    [Serializable]
    private sealed class PublicLobbyPageDiagnostics
    {
        public int scannedDocuments;
        public int queryCount;
        public int compatibleRooms;
        public int staleDocuments;
        public bool scanLimitReached;
    }

    [Serializable]
    private sealed class CallablePageResult
    {
        public bool ok;
        public PublicLobbyWireCard[] rooms;
        public string nextCursor;
        public bool hasMore;
        public PublicLobbyPageDiagnostics diagnostics;
    }

    [Serializable]
    private sealed class CallablePageEnvelope
    {
        public CallablePageResult result;
    }

    [Serializable]
    private sealed class CallableErrorEnvelope
    {
        public CallableError error;
    }

    [Serializable]
    private sealed class CallableError
    {
        public string status;
        public string message;
        public CallableErrorDetails details;
    }

    [Serializable]
    private sealed class CallableErrorDetails
    {
        public string errorKey;
    }
}
