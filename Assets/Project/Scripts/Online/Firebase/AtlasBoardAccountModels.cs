using System;
using System.Collections.Generic;

public static class AtlasBoardAccountConstants
{
    public const int SchemaVersion = 1;
    public const string DefaultMembershipTier = "normal";
    public const string DefaultAccountStatus = "active";
    public const string DefaultCrossplayPreference = "enabled";

    // Atlas Board currently ships English plus six additional locales.
    // Keep these as stable storage codes; never store localized display names.
    public static readonly HashSet<string> SupportedLanguageCodes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "en",
            "tr",
            "es",
            "fr",
            "de",
            "ko",
            "ru"
        };
}

public enum AtlasAccountErrorCode
{
    None = 0,
    NotInitialized,
    InvalidEmail,
    WeakPassword,
    EmailAlreadyInUse,
    InvalidCredentials,
    UserDisabled,
    TooManyRequests,
    Network,
    PermissionDenied,
    NotAuthenticated,
    InvalidDisplayName,
    InvalidCountryCode,
    UnsupportedLanguage,
    FirestoreWriteFailed,
    FirestoreReadFailed,
    Unknown
}

[Serializable]
public sealed class AtlasAccountOperationResult
{
    public bool Success;
    public AtlasAccountErrorCode ErrorCode;
    public string ErrorLocalizationKey;
    public string TechnicalMessage;
    public string AccountId;

    public static AtlasAccountOperationResult Ok(string accountId)
    {
        return new AtlasAccountOperationResult
        {
            Success = true,
            ErrorCode = AtlasAccountErrorCode.None,
            ErrorLocalizationKey = string.Empty,
            TechnicalMessage = string.Empty,
            AccountId = accountId ?? string.Empty
        };
    }

    public static AtlasAccountOperationResult Fail(
        AtlasAccountErrorCode code,
        string localizationKey,
        string technicalMessage)
    {
        return new AtlasAccountOperationResult
        {
            Success = false,
            ErrorCode = code,
            ErrorLocalizationKey = localizationKey ?? "account.error.unknown",
            TechnicalMessage = technicalMessage ?? string.Empty,
            AccountId = string.Empty
        };
    }
}

[Serializable]
public sealed class AtlasAccountSnapshot
{
    public string AccountId;
    public string AccountStatus;
    public string MembershipTier;
    public string CountryCode;
    public string PreferredLanguage;
    public int SchemaVersion;

    public string DisplayName;
    public string AvatarId;
    public string ProfileFrameId;

    public AtlasCloudPreferences Preferences;
}

[Serializable]
public sealed class AtlasCloudPreferences
{
    public string Language = "en";
    public string CrossplayPreference =
        AtlasBoardAccountConstants.DefaultCrossplayPreference;

    // These maps intentionally stay provider/data-driven. Existing PlayerPrefs
    // settings will be bridged later without changing the current Settings UI.
    // Runtime-only maps. Properties are intentional: Unity's serializer does not
    // support Dictionary<string, object>, while Firestore conversion is handled
    // explicitly by AtlasBoardAccountService.
    public Dictionary<string, object> Audio { get; set; } =
        new Dictionary<string, object>();

    public Dictionary<string, object> Graphics { get; set; } =
        new Dictionary<string, object>();

    public Dictionary<string, object> Controls { get; set; } =
        new Dictionary<string, object>();

    public Dictionary<string, object> Gameplay { get; set; } =
        new Dictionary<string, object>();
}
