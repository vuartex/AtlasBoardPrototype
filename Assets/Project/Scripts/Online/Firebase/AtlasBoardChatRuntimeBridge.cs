using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public enum AtlasBoardChatScopeType
{
    Lobby = 0,
    Match = 1
}

[Serializable]
public sealed class AtlasBoardChatMessage
{
    public string messageId;
    public string accountId;
    public string displayName;
    public string body;
    public long createdAtEpochMs;
    public int senderUtcOffsetMinutes;
    public int schemaVersion;
}

public sealed class AtlasBoardChatScope
{
    public AtlasBoardChatScopeType ScopeType;
    public string ScopeId;

    public string StableKey =>
        $"{ScopeType}:{ScopeId}";
}

public sealed class AtlasBoardChatListResult
{
    public bool Success;
    public string ErrorKey;
    public string TechnicalMessage;
    public List<AtlasBoardChatMessage> Messages =
        new List<AtlasBoardChatMessage>();
    public long ServerNowEpochMs;
    public int CooldownMs = 10000;
    public int MaxMessageLength = 120;
}

public sealed class AtlasBoardChatSendResult
{
    public bool Success;
    public string ErrorKey;
    public string TechnicalMessage;
    public AtlasBoardChatMessage Message;
    public long ServerNowEpochMs;
    public int CooldownMs = 10000;
    public int RetryAfterMs;
}

public sealed class AtlasBoardChatRuntimeBridge : MonoBehaviour
{
    private const string ProjectId = "atlasboard-usa";
    private const string Region = "europe-west1";
    private const int FunctionsEmulatorPort = 5001;

    private AtlasBoardLobbyRuntimeBridge lobbyBridge;

    public string LocalAccountId =>
        lobbyBridge != null
            ? lobbyBridge.CurrentAccountId
            : string.Empty;

    private void Awake()
    {
        ResolveLobbyBridge();
    }

    public bool TryResolveActiveScope(
        out AtlasBoardChatScope scope)
    {
        scope = null;
        ResolveLobbyBridge();

        if (lobbyBridge == null ||
            !lobbyBridge.HasLobby ||
            lobbyBridge.CurrentSnapshot == null ||
            string.IsNullOrWhiteSpace(
                lobbyBridge.CurrentLobbyId))
        {
            return false;
        }

        AtlasLobbySnapshot snapshot =
            lobbyBridge.CurrentSnapshot;

        bool matchScope =
            !string.IsNullOrWhiteSpace(
                snapshot.MatchId) &&
            snapshot.LifecycleState !=
                AtlasRoomLifecycleState.Waiting;

        scope = new AtlasBoardChatScope
        {
            ScopeType = matchScope
                ? AtlasBoardChatScopeType.Match
                : AtlasBoardChatScopeType.Lobby,
            ScopeId = matchScope
                ? snapshot.MatchId
                : lobbyBridge.CurrentLobbyId
        };

        return !string.IsNullOrWhiteSpace(
            scope.ScopeId);
    }

    public async Task<AtlasBoardChatListResult>
        ListMessagesAsync(
            AtlasBoardChatScope scope,
            long afterEpochMs)
    {
        if (scope == null ||
            string.IsNullOrWhiteSpace(scope.ScopeId))
        {
            return FailList(
                "chat.error.invalid_scope",
                "No active chat scope is available.");
        }

        if (!await EnsureIdentityAsync())
        {
            return FailList(
                "chat.error.authentication_required",
                "Online identity is not available.");
        }

        ChatListRequest request =
            new ChatListRequest
            {
                scopeId = scope.ScopeId,
                afterEpochMs =
                    Math.Max(0L, afterEpochMs)
            };

        string functionName =
            scope.ScopeType == AtlasBoardChatScopeType.Match
                ? "matchChatList"
                : "lobbyChatList";

        ChatListEnvelope envelope =
            await CallAsync<ChatListRequest, ChatListEnvelope>(
                functionName,
                request);

        if (envelope == null)
        {
            return FailList(
                lastErrorKey,
                lastTechnicalMessage);
        }

        ChatListWire result =
            envelope.result;

        if (result == null || !result.ok)
        {
            return FailList(
                "chat.error.service_unavailable",
                "Chat list callable returned no valid result.");
        }

        AtlasBoardChatListResult output =
            new AtlasBoardChatListResult
            {
                Success = true,
                ServerNowEpochMs =
                    result.serverNowEpochMs,
                CooldownMs =
                    result.cooldownMs > 0
                        ? result.cooldownMs
                        : 10000,
                MaxMessageLength =
                    result.maxMessageLength > 0
                        ? result.maxMessageLength
                        : 120
            };

        if (result.messages != null)
        {
            output.Messages.AddRange(
                result.messages);
        }

        return output;
    }

    public async Task<AtlasBoardChatSendResult>
        SendMessageAsync(
            AtlasBoardChatScope scope,
            string body,
            int senderUtcOffsetMinutes)
    {
        if (scope == null ||
            string.IsNullOrWhiteSpace(scope.ScopeId))
        {
            return FailSend(
                "chat.error.invalid_scope",
                "No active chat scope is available.");
        }

        if (!await EnsureIdentityAsync())
        {
            return FailSend(
                "chat.error.authentication_required",
                "Online identity is not available.");
        }

        ChatSendRequest request =
            new ChatSendRequest
            {
                scopeId = scope.ScopeId,
                body = body ?? string.Empty,
                senderUtcOffsetMinutes =
                    Mathf.Clamp(
                        senderUtcOffsetMinutes,
                        -14 * 60,
                        14 * 60)
            };

        string functionName =
            scope.ScopeType == AtlasBoardChatScopeType.Match
                ? "matchChatSend"
                : "lobbyChatSend";

        ChatSendEnvelope envelope =
            await CallAsync<ChatSendRequest, ChatSendEnvelope>(
                functionName,
                request);

        if (envelope == null)
        {
            return new AtlasBoardChatSendResult
            {
                Success = false,
                ErrorKey = string.IsNullOrWhiteSpace(lastErrorKey)
                    ? "chat.error.service_unavailable"
                    : lastErrorKey,
                TechnicalMessage =
                    lastTechnicalMessage,
                RetryAfterMs =
                    lastRetryAfterMs
            };
        }

        ChatSendWire result =
            envelope.result;

        if (result == null || !result.ok)
        {
            return FailSend(
                "chat.error.service_unavailable",
                "Chat send callable returned no valid result.");
        }

        return new AtlasBoardChatSendResult
        {
            Success = true,
            Message = result.message,
            ServerNowEpochMs =
                result.serverNowEpochMs,
            CooldownMs =
                result.cooldownMs > 0
                    ? result.cooldownMs
                    : 10000
        };
    }

    private string lastErrorKey = string.Empty;
    private string lastTechnicalMessage = string.Empty;
    private int lastRetryAfterMs;

    private async Task<TEnvelope> CallAsync<TRequest, TEnvelope>(
        string functionName,
        TRequest requestBody)
        where TEnvelope : class
    {
        lastErrorKey = string.Empty;
        lastTechnicalMessage = string.Empty;
        lastRetryAfterMs = 0;

        ResolveLobbyBridge();

        if (lobbyBridge == null)
        {
            lastErrorKey =
                "chat.error.authentication_required";
            lastTechnicalMessage =
                "AtlasBoardLobbyRuntimeBridge was not found.";
            return null;
        }

        string token =
            lobbyBridge.AuthTokenForOnlineSubsystems;

        if (string.IsNullOrWhiteSpace(token))
        {
            lastErrorKey =
                "chat.error.authentication_required";
            lastTechnicalMessage =
                "Firebase auth token is empty.";
            return null;
        }

        string url =
            lobbyBridge.UsingLocalEmulators
                ? $"http://{lobbyBridge.EmulatorHostForOnlineSubsystems}:{FunctionsEmulatorPort}/" +
                  $"{ProjectId}/{Region}/{functionName}"
                : $"https://{Region}-{ProjectId}.cloudfunctions.net/" +
                  functionName;

        string dataJson =
            JsonUtility.ToJson(requestBody);
        string callableJson =
            "{\"data\":" + dataJson + "}";

        using UnityWebRequest webRequest =
            new UnityWebRequest(
                url,
                UnityWebRequest.kHttpVerbPOST);

        webRequest.uploadHandler =
            new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(
                    callableJson));
        webRequest.downloadHandler =
            new DownloadHandlerBuffer();

        webRequest.SetRequestHeader(
            "Content-Type",
            "application/json");
        webRequest.SetRequestHeader(
            "Authorization",
            $"Bearer {token}");
        webRequest.timeout = 20;

        await SendRequestAsync(webRequest);

        string body =
            webRequest.downloadHandler != null
                ? webRequest.downloadHandler.text
                : string.Empty;

        if (webRequest.result !=
            UnityWebRequest.Result.Success)
        {
            CallableErrorEnvelope error =
                SafeFromJson<CallableErrorEnvelope>(
                    body);

            lastErrorKey =
                error != null &&
                error.error != null &&
                error.error.details != null &&
                !string.IsNullOrWhiteSpace(
                    error.error.details.errorKey)
                    ? error.error.details.errorKey
                    : "chat.error.service_unavailable";

            lastRetryAfterMs =
                error != null &&
                error.error != null &&
                error.error.details != null
                    ? error.error.details.retryAfterMs
                    : 0;

            lastTechnicalMessage =
                error != null &&
                error.error != null &&
                !string.IsNullOrWhiteSpace(
                    error.error.message)
                    ? error.error.message
                    : $"HTTP {webRequest.responseCode}: " +
                      webRequest.error;

            return null;
        }

        TEnvelope envelope =
            SafeFromJson<TEnvelope>(body);

        if (envelope == null)
        {
            lastErrorKey =
                "chat.error.service_unavailable";
            lastTechnicalMessage =
                "Chat callable response could not be parsed. Body=" +
                body;
        }

        return envelope;
    }

    private async Task<bool> EnsureIdentityAsync()
    {
        ResolveLobbyBridge();

        if (lobbyBridge == null)
        {
            return false;
        }

        return await lobbyBridge
            .EnsureOnlineIdentityAsync();
    }

    private void ResolveLobbyBridge()
    {
        if (lobbyBridge != null)
        {
            return;
        }

        AtlasBoardLobbyRuntimeBridge[] all =
            Resources.FindObjectsOfTypeAll<
                AtlasBoardLobbyRuntimeBridge>();

        foreach (AtlasBoardLobbyRuntimeBridge item in all)
        {
            if (item != null &&
                item.gameObject.scene.IsValid())
            {
                lobbyBridge = item;
                break;
            }
        }
    }

    private static async Task SendRequestAsync(
        UnityWebRequest request)
    {
        UnityWebRequestAsyncOperation operation =
            request.SendWebRequest();

        while (!operation.isDone)
        {
            await Task.Yield();
        }
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
        catch
        {
            return null;
        }
    }

    private static AtlasBoardChatListResult FailList(
        string errorKey,
        string technical)
    {
        return new AtlasBoardChatListResult
        {
            Success = false,
            ErrorKey = string.IsNullOrWhiteSpace(errorKey)
                ? "chat.error.service_unavailable"
                : errorKey,
            TechnicalMessage = technical ?? string.Empty
        };
    }

    private static AtlasBoardChatSendResult FailSend(
        string errorKey,
        string technical)
    {
        return new AtlasBoardChatSendResult
        {
            Success = false,
            ErrorKey = string.IsNullOrWhiteSpace(errorKey)
                ? "chat.error.service_unavailable"
                : errorKey,
            TechnicalMessage = technical ?? string.Empty
        };
    }

    [Serializable]
    private sealed class ChatListRequest
    {
        public string scopeId;
        public long afterEpochMs;
    }

    [Serializable]
    private sealed class ChatSendRequest
    {
        public string scopeId;
        public string body;
        public int senderUtcOffsetMinutes;
    }

    [Serializable]
    private sealed class ChatListEnvelope
    {
        public ChatListWire result;
    }

    [Serializable]
    private sealed class ChatSendEnvelope
    {
        public ChatSendWire result;
    }

    [Serializable]
    private sealed class ChatListWire
    {
        public bool ok;
        public AtlasBoardChatMessage[] messages;
        public long serverNowEpochMs;
        public int maxMessageLength;
        public int cooldownMs;
    }

    [Serializable]
    private sealed class ChatSendWire
    {
        public bool ok;
        public AtlasBoardChatMessage message;
        public long serverNowEpochMs;
        public int cooldownMs;
    }

    [Serializable]
    private sealed class CallableErrorEnvelope
    {
        public CallableError error;
    }

    [Serializable]
    private sealed class CallableError
    {
        public string message;
        public CallableErrorDetails details;
    }

    [Serializable]
    private sealed class CallableErrorDetails
    {
        public string errorKey;
        public int retryAfterMs;
    }
}
