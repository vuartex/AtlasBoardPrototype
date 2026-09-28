using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public sealed class AtlasBoardChatSafetyState
{
    public bool Success;
    public string ErrorKey;
    public string TechnicalMessage;
    public string[] MutedAccountIds = Array.Empty<string>();
    public string[] BlockedAccountIds = Array.Empty<string>();
    public bool CanModerate;
    public bool ChatBanned;
    public long MutedUntilEpochMs;
    public bool ChatDisabled;
    public bool MinorRestricted;
    public bool BlockExternalLinks;
    public int StaticBlockedTermCount;
    public int ExtraBlockedTermCount;
    public int MessageRetentionDays;
    public int ReportRetentionDays;
}

[Serializable]
public sealed class AtlasBoardChatModerationResult
{
    public bool Success;
    public string ErrorKey;
    public string TechnicalMessage;
    public string ReportId;
    public string TargetAccountId;
    public string TargetDisplayName;
    public bool Muted;
    public string MessageId;
    public string Action;
}

public sealed class AtlasBoardChatModerationBridge : MonoBehaviour
{
    private const string ProjectId = "atlasboard-usa";
    private const string Region = "europe-west1";
    private const int FunctionsEmulatorPort = 5001;

    private AtlasBoardLobbyRuntimeBridge lobbyBridge;
    private string lastErrorKey = string.Empty;
    private string lastTechnicalMessage = string.Empty;

    public string LocalAccountId =>
        lobbyBridge != null
            ? lobbyBridge.CurrentAccountId
            : string.Empty;

    private void Awake()
    {
        ResolveLobbyBridge();
    }

    public async Task<AtlasBoardChatSafetyState> GetSafetyStateAsync(
        AtlasBoardChatScope scope)
    {
        if (!ValidateScope(scope))
        {
            return FailSafety("chat.error.invalid_scope");
        }

        SafetyStateRequest request =
            new SafetyStateRequest
            {
                operation = "safety_state",
                scopeId = scope.ScopeId
            };

        SafetyStateEnvelope envelope =
            await CallAsync<SafetyStateRequest, SafetyStateEnvelope>(
                ResolveListFunction(scope),
                request);

        if (envelope == null || envelope.result == null)
        {
            return FailSafety(lastErrorKey);
        }

        return new AtlasBoardChatSafetyState
        {
            Success = envelope.result.ok,
            MutedAccountIds =
                envelope.result.mutedAccountIds ?? Array.Empty<string>(),
            BlockedAccountIds =
                envelope.result.blockedAccountIds ?? Array.Empty<string>(),
            CanModerate = envelope.result.canModerate,
            ChatBanned = envelope.result.chatBanned,
            MutedUntilEpochMs = envelope.result.mutedUntilEpochMs,
            ChatDisabled = envelope.result.chatDisabled,
            MinorRestricted = envelope.result.minorRestricted,
            BlockExternalLinks = envelope.result.blockExternalLinks,
            StaticBlockedTermCount =
                envelope.result.staticBlockedTermCount,
            ExtraBlockedTermCount =
                envelope.result.extraBlockedTermCount,
            MessageRetentionDays =
                envelope.result.messageRetentionDays,
            ReportRetentionDays =
                envelope.result.reportRetentionDays
        };
    }

    public async Task<AtlasBoardChatModerationResult> SetMutedAsync(
        AtlasBoardChatScope scope,
        string targetAccountId,
        bool muted)
    {
        ModerationWriteRequest request =
            CreateWriteRequest(
                scope,
                "set_muted",
                targetAccountId);
        request.muted = muted;
        return await SendWriteAsync(scope, request);
    }

    public async Task<AtlasBoardChatModerationResult> SetBlockedAsync(
        AtlasBoardChatScope scope,
        string targetAccountId,
        bool blocked)
    {
        ModerationWriteRequest request =
            CreateWriteRequest(
                scope,
                "set_blocked",
                targetAccountId);
        request.muted = blocked;
        return await SendWriteAsync(scope, request);
    }

    public async Task<AtlasBoardChatModerationResult> ReportPlayerAsync(
        AtlasBoardChatScope scope,
        string targetAccountId,
        string reasonCode)
    {
        ModerationWriteRequest request =
            CreateWriteRequest(
                scope,
                "report_player",
                targetAccountId);
        request.reasonCode = NormalizeReason(reasonCode);
        request.comment = "In-game player report";
        return await SendWriteAsync(scope, request);
    }

    public async Task<AtlasBoardChatModerationResult> ReportMessageAsync(
        AtlasBoardChatScope scope,
        string messageId,
        string reasonCode)
    {
        ModerationWriteRequest request =
            CreateWriteRequest(
                scope,
                "report_message",
                string.Empty);
        request.messageId = messageId ?? string.Empty;
        request.reasonCode = NormalizeReason(reasonCode);
        request.comment = "In-game message report";
        return await SendWriteAsync(scope, request);
    }

    public async Task<AtlasBoardChatModerationResult>
        ModeratorRemoveMessageAsync(
            AtlasBoardChatScope scope,
            string messageId)
    {
        ModerationWriteRequest request =
            CreateWriteRequest(
                scope,
                "moderator_remove_message",
                string.Empty);
        request.messageId = messageId ?? string.Empty;
        request.comment = "Removed from in-game moderator panel";
        return await SendWriteAsync(scope, request);
    }

    public async Task<AtlasBoardChatModerationResult>
        ModeratorSetSanctionAsync(
            AtlasBoardChatScope scope,
            string targetAccountId,
            string action,
            int durationMinutes = 10)
    {
        ModerationWriteRequest request =
            CreateWriteRequest(
                scope,
                "moderator_set_sanction",
                targetAccountId);
        request.action = action ?? string.Empty;
        request.durationMinutes = Mathf.Clamp(
            durationMinutes,
            1,
            43200);
        request.comment = "In-game moderator action";
        return await SendWriteAsync(scope, request);
    }

    public async Task<AtlasBoardChatModerationResult>
        ModeratorUpdateConfigAsync(
            AtlasBoardChatScope scope,
            string[] extraBlockedTerms)
    {
        ModerationWriteRequest request =
            CreateWriteRequest(
                scope,
                "moderator_update_config",
                string.Empty);
        request.extraBlockedTerms =
            extraBlockedTerms ?? Array.Empty<string>();
        request.blockExternalLinks = true;
        request.messageRetentionDays = 30;
        request.reportRetentionDays = 180;
        request.maxBurstPerMinute = 5;
        request.maxDuplicateRepeats = 2;
        request.duplicateWindowSeconds = 120;
        return await SendWriteAsync(scope, request);
    }

    private async Task<AtlasBoardChatModerationResult> SendWriteAsync(
        AtlasBoardChatScope scope,
        ModerationWriteRequest request)
    {
        if (!ValidateScope(scope))
        {
            return FailWrite("chat.error.invalid_scope");
        }

        ModerationWriteEnvelope envelope =
            await CallAsync<
                ModerationWriteRequest,
                ModerationWriteEnvelope>(
                    ResolveSendFunction(scope),
                    request);

        if (envelope == null || envelope.result == null)
        {
            return FailWrite(lastErrorKey);
        }

        return new AtlasBoardChatModerationResult
        {
            Success = envelope.result.ok,
            ReportId = envelope.result.reportId ?? string.Empty,
            TargetAccountId =
                envelope.result.targetAccountId ?? string.Empty,
            TargetDisplayName =
                envelope.result.targetDisplayName ?? string.Empty,
            Muted = envelope.result.muted,
            MessageId = envelope.result.messageId ?? string.Empty,
            Action = envelope.result.action ?? string.Empty
        };
    }

    private async Task<TEnvelope> CallAsync<TRequest, TEnvelope>(
        string functionName,
        TRequest requestBody)
        where TEnvelope : class
    {
        lastErrorKey = string.Empty;
        lastTechnicalMessage = string.Empty;

        if (!await EnsureIdentityAsync())
        {
            lastErrorKey = "chat.error.authentication_required";
            lastTechnicalMessage = "Online identity is unavailable.";
            return null;
        }

        string token = lobbyBridge.AuthTokenForOnlineSubsystems;
        string url =
            lobbyBridge.UsingLocalEmulators
                ? $"http://{lobbyBridge.EmulatorHostForOnlineSubsystems}:{FunctionsEmulatorPort}/" +
                  $"{ProjectId}/{Region}/{functionName}"
                : $"https://{Region}-{ProjectId}.cloudfunctions.net/" +
                  functionName;

        string dataJson = JsonUtility.ToJson(requestBody);
        string callableJson = "{\"data\":" + dataJson + "}";

        using UnityWebRequest webRequest =
            new UnityWebRequest(
                url,
                UnityWebRequest.kHttpVerbPOST);

        webRequest.uploadHandler =
            new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(callableJson));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
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

        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            CallableErrorEnvelope error =
                SafeFromJson<CallableErrorEnvelope>(body);

            lastErrorKey =
                error != null &&
                error.error != null &&
                error.error.details != null &&
                !string.IsNullOrWhiteSpace(
                    error.error.details.errorKey)
                    ? error.error.details.errorKey
                    : "chat.error.service_unavailable";
            lastTechnicalMessage =
                error != null &&
                error.error != null &&
                !string.IsNullOrWhiteSpace(error.error.message)
                    ? error.error.message
                    : $"HTTP {webRequest.responseCode}: " +
                      webRequest.error;
            return null;
        }

        TEnvelope envelope = SafeFromJson<TEnvelope>(body);
        if (envelope == null)
        {
            lastErrorKey = "chat.error.service_unavailable";
            lastTechnicalMessage =
                "Chat moderation response could not be parsed.";
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

        return await lobbyBridge.EnsureOnlineIdentityAsync();
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
            if (item != null && item.gameObject.scene.IsValid())
            {
                lobbyBridge = item;
                break;
            }
        }
    }

    private static bool ValidateScope(AtlasBoardChatScope scope)
    {
        return scope != null &&
               !string.IsNullOrWhiteSpace(scope.ScopeId);
    }

    private static string ResolveSendFunction(
        AtlasBoardChatScope scope)
    {
        return scope.ScopeType == AtlasBoardChatScopeType.Match
            ? "matchChatSend"
            : "lobbyChatSend";
    }

    private static string ResolveListFunction(
        AtlasBoardChatScope scope)
    {
        return scope.ScopeType == AtlasBoardChatScopeType.Match
            ? "matchChatList"
            : "lobbyChatList";
    }

    private static string NormalizeReason(string value)
    {
        switch ((value ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "abuse":
            case "harassment":
            case "spam":
            case "profanity":
            case "cheating":
            case "other":
                return value.Trim().ToLowerInvariant();
            default:
                return "other";
        }
    }

    private static ModerationWriteRequest CreateWriteRequest(
        AtlasBoardChatScope scope,
        string operation,
        string targetAccountId)
    {
        return new ModerationWriteRequest
        {
            operation = operation,
            scopeId = scope != null ? scope.ScopeId : string.Empty,
            targetAccountId = targetAccountId ?? string.Empty
        };
    }

    private AtlasBoardChatSafetyState FailSafety(string errorKey)
    {
        return new AtlasBoardChatSafetyState
        {
            Success = false,
            ErrorKey = string.IsNullOrWhiteSpace(errorKey)
                ? "chat.error.service_unavailable"
                : errorKey,
            TechnicalMessage = lastTechnicalMessage
        };
    }

    private AtlasBoardChatModerationResult FailWrite(string errorKey)
    {
        return new AtlasBoardChatModerationResult
        {
            Success = false,
            ErrorKey = string.IsNullOrWhiteSpace(errorKey)
                ? "chat.error.service_unavailable"
                : errorKey,
            TechnicalMessage = lastTechnicalMessage
        };
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

    [Serializable]
    private sealed class SafetyStateRequest
    {
        public string operation;
        public string scopeId;
    }

    [Serializable]
    private sealed class ModerationWriteRequest
    {
        public string operation;
        public string scopeId;
        public string targetAccountId;
        public string messageId;
        public string reasonCode;
        public string comment;
        public bool muted;
        public string action;
        public int durationMinutes;
        public string[] extraBlockedTerms;
        public bool blockExternalLinks;
        public int messageRetentionDays;
        public int reportRetentionDays;
        public int maxBurstPerMinute;
        public int maxDuplicateRepeats;
        public int duplicateWindowSeconds;
    }

    [Serializable]
    private sealed class SafetyStateEnvelope
    {
        public SafetyStateWire result;
    }

    [Serializable]
    private sealed class SafetyStateWire
    {
        public bool ok;
        public string[] mutedAccountIds;
        public string[] blockedAccountIds;
        public bool canModerate;
        public bool chatBanned;
        public long mutedUntilEpochMs;
        public bool chatDisabled;
        public bool minorRestricted;
        public bool blockExternalLinks;
        public int staticBlockedTermCount;
        public int extraBlockedTermCount;
        public int messageRetentionDays;
        public int reportRetentionDays;
    }

    [Serializable]
    private sealed class ModerationWriteEnvelope
    {
        public ModerationWriteWire result;
    }

    [Serializable]
    private sealed class ModerationWriteWire
    {
        public bool ok;
        public string reportId;
        public string targetAccountId;
        public string targetDisplayName;
        public bool muted;
        public string messageId;
        public string action;
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
    }
}
