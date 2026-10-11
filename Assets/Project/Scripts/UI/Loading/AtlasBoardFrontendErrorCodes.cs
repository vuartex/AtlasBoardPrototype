using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public enum AtlasBoardFrontendErrorSeverity
{
    Informational = 0,
    Recoverable = 1,
    Blocking = 2
}

public sealed class AtlasBoardFrontendErrorDefinition
{
    public string Code { get; }
    public string Category { get; }
    public string InternalMeaning { get; }
    public AtlasBoardFrontendErrorSeverity Severity { get; }

    public AtlasBoardFrontendErrorDefinition(
        string code,
        string category,
        string internalMeaning,
        AtlasBoardFrontendErrorSeverity severity)
    {
        Code = code ?? "ATB990";
        Category = category ?? "Unknown";
        InternalMeaning = internalMeaning ?? "Unclassified client failure.";
        Severity = severity;
    }
}

/// <summary>
/// Stable player-facing Atlas Board support/error codes.
/// Public UI shows only a generic localized message plus the ATB code.
/// Technical details remain in diagnostics and must be redacted.
/// Canonical documentation:
/// Assets/Project/Documentation/FRONTEND_ERROR_CODEBOOK.md
/// </summary>
public static class AtlasBoardFrontendErrorCodes
{
    public const string UnexpectedClientFailure = "ATB001";
    public const string InvalidLocalState = "ATB002";
    public const string UnsupportedClientOperation = "ATB003";
    public const string RequiredUiUnavailable = "ATB004";
    public const string RequiredResourceUnavailable = "ATB005";
    public const string LocalReadFailure = "ATB006";
    public const string LocalWriteFailure = "ATB007";
    public const string InvalidClientResponseState = "ATB008";
    public const string UnknownClientFailure = "ATB009";
    public const string StartupUnavailable = "ATB100";
    public const string StartupDelay10 = "ATB101";
    public const string StartupDelay20 = "ATB102";
    public const string StartupDelay30 = "ATB103";
    public const string SessionPreparationFailed = "ATB110";
    public const string SessionExpired = "ATB111";
    public const string StartupProfileFailed = "ATB120";
    public const string StartupOnlineDataFailed = "ATB130";
    public const string StartupLocalizationFailed = "ATB140";
    public const string StartupConfigurationMismatch = "ATB150";
    public const string StartupReadinessUnknown = "ATB160";
    public const string LobbyCreateFailed = "ATB200";
    public const string LobbyJoinFailed = "ATB201";
    public const string LobbyListFailed = "ATB202";
    public const string LobbyCodeJoinFailed = "ATB203";
    public const string LobbyAccessUpdateFailed = "ATB204";
    public const string LobbySettingsFailed = "ATB205";
    public const string LobbySeatConfigurationFailed = "ATB206";
    public const string LobbyReadyFailed = "ATB207";
    public const string LobbyLeaveFailed = "ATB208";
    public const string LobbyStartFailed = "ATB209";
    public const string LobbySyncFailed = "ATB210";
    public const string LobbyHeartbeatFailed = "ATB211";
    public const string LobbyUnavailable = "ATB212";
    public const string LobbyCapacityUnavailable = "ATB213";
    public const string LobbyVersionMismatch = "ATB214";
    public const string LobbyAccessDenied = "ATB215";
    public const string MatchInitializationFailed = "ATB300";
    public const string MatchInitialSyncDelay = "ATB301";
    public const string MatchExtendedSyncDelay = "ATB302";
    public const string MatchRecoveryDelay = "ATB303";
    public const string MatchReconnectFailed = "ATB304";
    public const string MatchIntentFailed = "ATB305";
    public const string MatchPublishFailed = "ATB306";
    public const string MatchSnapshotFailed = "ATB307";
    public const string MatchPresenceFailed = "ATB308";
    public const string MatchHostMigrationFailed = "ATB309";
    public const string MatchIntentListFailed = "ATB310";
    public const string MatchIntentAcknowledgeFailed = "ATB311";
    public const string MatchDisconnectSweepFailed = "ATB312";
    public const string MatchReconnectExpiryFailed = "ATB313";
    public const string MatchRematchFailed = "ATB314";
    public const string MatchLeaveFailed = "ATB315";
    public const string MatchAuthoritativeStateInvalid = "ATB316";
    public const string MatchStaleClientState = "ATB317";
    public const string MatchTurnDiceSyncFailed = "ATB318";
    public const string MatchResultSyncFailed = "ATB319";
    public const string LobbyChatFailed = "ATB400";
    public const string MatchChatFailed = "ATB401";
    public const string ReportSubmitFailed = "ATB410";
    public const string ModerationActionFailed = "ATB411";
    public const string ContentFilterUnavailable = "ATB420";
    public const string ProgressionUnavailable = "ATB500";
    public const string ProfileLoadFailed = "ATB501";
    public const string HistoryLoadFailed = "ATB502";
    public const string AchievementSyncFailed = "ATB503";
    public const string SettingsSyncFailed = "ATB510";
    public const string CloudPreferenceSyncFailed = "ATB511";
    public const string CosmeticSyncFailed = "ATB520";
    public const string ProfilePresentationFailed = "ATB530";
    public const string StoreUnavailable = "ATB600";
    public const string SeasonalContentUnavailable = "ATB601";
    public const string WalletUnavailable = "ATB610";
    public const string InventoryUnavailable = "ATB620";
    public const string PurchaseFailed = "ATB630";
    public const string RefundFailed = "ATB631";
    public const string PromoFailed = "ATB640";
    public const string RewardClaimFailed = "ATB650";
    public const string TransactionConflict = "ATB660";
    public const string PlatformRuntimeUnavailable = "ATB700";
    public const string PlatformProviderUnavailable = "ATB701";
    public const string PlatformSessionFailed = "ATB710";
    public const string PlatformAccountLinkFailed = "ATB720";
    public const string PlatformReturningSignInFailed = "ATB721";
    public const string SecurityHandshakeFailed = "ATB730";
    public const string PlatformInviteFailed = "ATB740";
    public const string EntitlementCheckFailed = "ATB750";
    public const string PlatformAchievementSyncFailed = "ATB760";
    public const string NetworkUnavailable = "ATB800";
    public const string RequestTimedOut = "ATB810";
    public const string SlowConnection = "ATB811";
    public const string TransportFailure = "ATB820";
    public const string UnexpectedServiceResponse = "ATB821";
    public const string MalformedResponse = "ATB830";
    public const string InvalidSessionResponse = "ATB831";
    public const string ProtocolMismatch = "ATB840";
    public const string ContentVersionMismatch = "ATB841";
    public const string ServiceTemporarilyUnavailable = "ATB850";
    public const string RequestRateLimited = "ATB860";
    public const string RequestCancelled = "ATB870";
    public const string SecurityValidationRejected = "ATB880";
    public const string UiPresentationFailure = "ATB900";
    public const string InputFailure = "ATB901";
    public const string LocalizationFailure = "ATB910";
    public const string FontFailure = "ATB911";
    public const string AssetLoadFailure = "ATB920";
    public const string SceneLoadFailure = "ATB921";
    public const string GraphicsFailure = "ATB930";
    public const string AudioFailure = "ATB940";
    public const string SettingsStorageFailure = "ATB950";
    public const string LowMemory = "ATB960";
    public const string PlatformSpecificFailure = "ATB970";
    public const string DiagnosticsWriteFailure = "ATB980";
    public const string UnclassifiedFailure = "ATB990";

    private static readonly Dictionary<string, AtlasBoardFrontendErrorDefinition>
        Definitions = BuildDefinitions();

    public static event Action<string, string> PublicCodeRaised;

    public static IReadOnlyDictionary<string, AtlasBoardFrontendErrorDefinition> All =>
        Definitions;

    public static AtlasBoardFrontendErrorDefinition Get(string code)
    {
        if (!string.IsNullOrWhiteSpace(code) &&
            Definitions.TryGetValue(code.Trim().ToUpperInvariant(), out AtlasBoardFrontendErrorDefinition definition))
        {
            return definition;
        }

        return Definitions[UnclassifiedFailure];
    }

    public static string PublicMessage(string code)
    {
        string normalized = Get(code).Code;

        if (normalized == StartupDelay10 ||
            normalized == StartupDelay20 ||
            normalized == StartupDelay30 ||
            normalized == MatchInitialSyncDelay ||
            normalized == MatchExtendedSyncDelay ||
            normalized == MatchRecoveryDelay ||
            normalized == SlowConnection)
        {
            return AtlasBoardL.R(
                $"This is taking a little longer than expected. Code: {normalized}",
                $"Bu işlem beklenenden biraz uzun sürüyor. Kod: {normalized}",
                $"Esto está tardando un poco más de lo esperado. Código: {normalized}",
                $"Cette opération prend un peu plus de temps que prévu. Code : {normalized}",
                $"Dieser Vorgang dauert etwas länger als erwartet. Code: {normalized}",
                $"예상보다 조금 더 오래 걸리고 있습니다. 코드: {normalized}",
                $"Это занимает немного больше времени, чем ожидалось. Код: {normalized}");
        }

        if (normalized.StartsWith("ATB8", StringComparison.Ordinal))
        {
            return AtlasBoardL.R(
                $"Online services are temporarily unavailable. Please try again. Code: {normalized}",
                $"Çevrimiçi hizmetlere şu anda ulaşılamıyor. Lütfen tekrar deneyin. Kod: {normalized}",
                $"Los servicios en línea no están disponibles temporalmente. Inténtalo de nuevo. Código: {normalized}",
                $"Les services en ligne sont temporairement indisponibles. Réessayez. Code : {normalized}",
                $"Onlinedienste sind vorübergehend nicht verfügbar. Bitte erneut versuchen. Code: {normalized}",
                $"온라인 서비스를 일시적으로 사용할 수 없습니다. 다시 시도해 주세요. 코드: {normalized}",
                $"Онлайн-сервисы временно недоступны. Повторите попытку. Код: {normalized}");
        }

        return AtlasBoardL.R(
            $"We couldn't complete this step. Please try again. Code: {normalized}",
            $"Bu adım tamamlanamadı. Lütfen tekrar deneyin. Kod: {normalized}",
            $"No pudimos completar este paso. Inténtalo de nuevo. Código: {normalized}",
            $"Cette étape n'a pas pu être terminée. Réessayez. Code : {normalized}",
            $"Dieser Schritt konnte nicht abgeschlossen werden. Bitte erneut versuchen. Code: {normalized}",
            $"이 단계를 완료하지 못했습니다. 다시 시도해 주세요. 코드: {normalized}",
            $"Не удалось завершить этот шаг. Повторите попытку. Код: {normalized}");
    }

    public static void Report(
        string code,
        string internalDetail = "",
        string phase = "",
        AtlasBoardFrontendErrorSeverity? severityOverride = null)
    {
        AtlasBoardFrontendErrorDefinition definition = Get(code);
        AtlasBoardFrontendErrorSeverity severity = severityOverride ?? definition.Severity;

        string safeDetail = SanitizeForLog(internalDetail);
        string safePhase = SanitizeForLog(phase);

        string line =
            "ATB_FRONTEND_CODE code=" + definition.Code +
            " category=" + definition.Category +
            " severity=" + severity +
            (string.IsNullOrWhiteSpace(safePhase) ? "" : " phase=" + safePhase) +
            (string.IsNullOrWhiteSpace(safeDetail) ? "" : " detail=" + safeDetail);

        if (severity == AtlasBoardFrontendErrorSeverity.Informational)
            Debug.Log(line);
        else
            Debug.LogWarning(line);

        PublicCodeRaised?.Invoke(definition.Code, PublicMessage(definition.Code));
    }

    public static string SanitizeForLog(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        string text = input.Replace('\r', ' ').Replace('\n', ' ');

        text = Regex.Replace(
            text,
            @"(?i)(Bearer\s+)[A-Za-z0-9._~+/=-]+",
            "$1[REDACTED]");

        text = Regex.Replace(
            text,
            @"(?i)((?:idToken|refreshToken|password|secret|apiKey|ticket|authorization|cookie)\s*[:=]\s*[""']?)[^,""'\s]+",
            "$1[REDACTED]");

        text = Regex.Replace(
            text,
            @"(?i)(https?://)[^\s]+",
            "$1[ENDPOINT_REDACTED]");

        if (text.Length > 900)
            text = text.Substring(0, 900) + "...[TRUNCATED]";

        return text;
    }

    private static Dictionary<string, AtlasBoardFrontendErrorDefinition> BuildDefinitions()
    {
        Dictionary<string, AtlasBoardFrontendErrorDefinition> map =
            new Dictionary<string, AtlasBoardFrontendErrorDefinition>(
                StringComparer.OrdinalIgnoreCase);

        void Add(
            string code,
            string category,
            string meaning,
            AtlasBoardFrontendErrorSeverity severity)
        {
            map[code] = new AtlasBoardFrontendErrorDefinition(
                code,
                category,
                meaning,
                severity);
        }

        Add(UnexpectedClientFailure, "Client", "Unexpected managed client failure.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(InvalidLocalState, "Client", "Local state is inconsistent with the requested action.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(UnsupportedClientOperation, "Client", "Requested client operation is not available in the current state.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(RequiredUiUnavailable, "Client", "Required UI controller or presentation surface was unavailable.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(RequiredResourceUnavailable, "Client", "Required local resource was unavailable.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LocalReadFailure, "Client", "Local client data could not be read.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LocalWriteFailure, "Client", "Local client data could not be written.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(InvalidClientResponseState, "Client", "Client received a result that could not be represented safely.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(UnknownClientFailure, "Client", "Unknown client failure.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(StartupUnavailable, "Startup", "Startup pipeline could not begin.", AtlasBoardFrontendErrorSeverity.Blocking);
        Add(StartupDelay10, "Startup", "Startup exceeded the first 10-second delay threshold.", AtlasBoardFrontendErrorSeverity.Informational);
        Add(StartupDelay20, "Startup", "Startup exceeded the 20-second delay threshold.", AtlasBoardFrontendErrorSeverity.Informational);
        Add(StartupDelay30, "Startup", "Startup exceeded the 30-second delay threshold.", AtlasBoardFrontendErrorSeverity.Informational);
        Add(SessionPreparationFailed, "Startup", "Online session/identity preparation did not complete.", AtlasBoardFrontendErrorSeverity.Blocking);
        Add(SessionExpired, "Startup", "Existing client session is no longer valid.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(StartupProfileFailed, "Startup", "Optional startup profile preparation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(StartupOnlineDataFailed, "Startup", "Optional startup online-data preparation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(StartupLocalizationFailed, "Startup", "Startup localization preparation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(StartupConfigurationMismatch, "Startup", "Runtime configuration is incompatible with the requested environment.", AtlasBoardFrontendErrorSeverity.Blocking);
        Add(StartupReadinessUnknown, "Startup", "Startup readiness could not be determined.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbyCreateFailed, "Lobby", "Lobby creation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbyJoinFailed, "Lobby", "Lobby join failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbyListFailed, "Lobby", "Public lobby discovery failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbyCodeJoinFailed, "Lobby", "Join-by-code failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbyAccessUpdateFailed, "Lobby", "Lobby access/password update failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbySettingsFailed, "Lobby", "Lobby settings update failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbySeatConfigurationFailed, "Lobby", "Lobby seat configuration failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbyReadyFailed, "Lobby", "Lobby ready-state update failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbyLeaveFailed, "Lobby", "Lobby leave/close operation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbyStartFailed, "Lobby", "Authoritative lobby start failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbySyncFailed, "Lobby", "Lobby snapshot synchronization failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbyHeartbeatFailed, "Lobby", "Lobby host heartbeat failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbyUnavailable, "Lobby", "Requested lobby no longer exists or is unavailable.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbyCapacityUnavailable, "Lobby", "Requested lobby has no suitable capacity.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbyVersionMismatch, "Lobby", "Lobby client/content/protocol version is incompatible.", AtlasBoardFrontendErrorSeverity.Blocking);
        Add(LobbyAccessDenied, "Lobby", "Lobby action was rejected by authoritative access rules.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchInitializationFailed, "Match", "Match runtime initialization failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchInitialSyncDelay, "Match", "Initial authoritative match state exceeded the first 10-second threshold.", AtlasBoardFrontendErrorSeverity.Informational);
        Add(MatchExtendedSyncDelay, "Match", "Initial authoritative match state exceeded the 20-second threshold.", AtlasBoardFrontendErrorSeverity.Informational);
        Add(MatchRecoveryDelay, "Match", "Initial authoritative match state exceeded the 30-second threshold.", AtlasBoardFrontendErrorSeverity.Informational);
        Add(MatchReconnectFailed, "Match", "Reconnect/catch-up operation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchIntentFailed, "Match", "Client gameplay intent submission failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchPublishFailed, "Match", "Host authoritative state publication failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchSnapshotFailed, "Match", "Authoritative match snapshot request failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchPresenceFailed, "Match", "Match presence update failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchHostMigrationFailed, "Match", "Host authority recovery/migration failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchIntentListFailed, "Match", "Host pending-intent list operation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchIntentAcknowledgeFailed, "Match", "Host intent acknowledgement failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchDisconnectSweepFailed, "Match", "Disconnected-seat sweep failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchReconnectExpiryFailed, "Match", "Reconnect expiry processing failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchRematchFailed, "Match", "Rematch preparation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchLeaveFailed, "Match", "Active-match leave failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchAuthoritativeStateInvalid, "Match", "Authoritative match state failed client validation.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchStaleClientState, "Match", "Client state was stale relative to authority.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchTurnDiceSyncFailed, "Match", "Turn/dice presentation could not synchronize.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchResultSyncFailed, "Match", "Match-result synchronization failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LobbyChatFailed, "Communication", "Lobby chat operation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MatchChatFailed, "Communication", "Match chat operation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(ReportSubmitFailed, "Communication", "Report submission failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(ModerationActionFailed, "Communication", "Authorized moderation action failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(ContentFilterUnavailable, "Communication", "Content filtering service was unavailable.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(ProgressionUnavailable, "Profile", "Progression service unavailable.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(ProfileLoadFailed, "Profile", "Player profile load failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(HistoryLoadFailed, "Profile", "Match history load failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(AchievementSyncFailed, "Profile", "Achievement synchronization failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(SettingsSyncFailed, "Profile", "Settings synchronization failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(CloudPreferenceSyncFailed, "Profile", "Cloud preference synchronization failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(CosmeticSyncFailed, "Profile", "Cosmetic selection synchronization failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(ProfilePresentationFailed, "Profile", "Profile presentation could not be refreshed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(StoreUnavailable, "Economy", "Store/catalog data unavailable.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(SeasonalContentUnavailable, "Economy", "Seasonal content unavailable.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(WalletUnavailable, "Economy", "Wallet data unavailable.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(InventoryUnavailable, "Economy", "Inventory data unavailable.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(PurchaseFailed, "Economy", "Purchase operation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(RefundFailed, "Economy", "Refund operation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(PromoFailed, "Economy", "Promo redemption failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(RewardClaimFailed, "Economy", "Reward claim failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(TransactionConflict, "Economy", "Authoritative transaction conflicted or replayed unexpectedly.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(PlatformRuntimeUnavailable, "Platform", "Platform runtime unavailable.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(PlatformProviderUnavailable, "Platform", "External platform provider unavailable.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(PlatformSessionFailed, "Platform", "Platform session preparation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(PlatformAccountLinkFailed, "Platform", "Platform account-link operation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(PlatformReturningSignInFailed, "Platform", "Returning platform sign-in failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(SecurityHandshakeFailed, "Platform", "Client security/attestation handshake failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(PlatformInviteFailed, "Platform", "Platform invite/join request failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(EntitlementCheckFailed, "Platform", "Platform entitlement check failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(PlatformAchievementSyncFailed, "Platform", "Platform achievement synchronization failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(NetworkUnavailable, "Connectivity", "Operating system reports no network route.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(RequestTimedOut, "Connectivity", "Request exceeded its allowed time.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(SlowConnection, "Connectivity", "Observed response latency exceeded the UX threshold.", AtlasBoardFrontendErrorSeverity.Informational);
        Add(TransportFailure, "Connectivity", "Transport failed before a valid application response.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(UnexpectedServiceResponse, "Connectivity", "Service returned an unexpected status.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(MalformedResponse, "Connectivity", "Response could not be parsed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(InvalidSessionResponse, "Connectivity", "Response did not match the expected authenticated session.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(ProtocolMismatch, "Connectivity", "Client protocol version mismatch.", AtlasBoardFrontendErrorSeverity.Blocking);
        Add(ContentVersionMismatch, "Connectivity", "Client content version mismatch.", AtlasBoardFrontendErrorSeverity.Blocking);
        Add(ServiceTemporarilyUnavailable, "Connectivity", "Required online service is temporarily unavailable.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(RequestRateLimited, "Connectivity", "Authoritative service rate-limited the request.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(RequestCancelled, "Connectivity", "Request was cancelled because the client state changed.", AtlasBoardFrontendErrorSeverity.Informational);
        Add(SecurityValidationRejected, "Connectivity", "Request failed authoritative security validation.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(UiPresentationFailure, "Presentation", "UI presentation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(InputFailure, "Presentation", "Input action could not be processed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LocalizationFailure, "Presentation", "Localization entry/language resolution failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(FontFailure, "Presentation", "Required localized font could not be resolved.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(AssetLoadFailure, "Presentation", "Required local asset could not be loaded.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(SceneLoadFailure, "Presentation", "Scene transition/load failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(GraphicsFailure, "Presentation", "Graphics/presentation subsystem failure.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(AudioFailure, "Presentation", "Audio subsystem failure.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(SettingsStorageFailure, "Presentation", "Settings storage operation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(LowMemory, "Presentation", "Client received a low-memory signal.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(PlatformSpecificFailure, "Presentation", "Platform-specific client operation failed.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(DiagnosticsWriteFailure, "Presentation", "Diagnostic output could not be written.", AtlasBoardFrontendErrorSeverity.Recoverable);
        Add(UnclassifiedFailure, "Unknown", "Failure did not map to a more specific code.", AtlasBoardFrontendErrorSeverity.Recoverable);

        return map;
    }
}
