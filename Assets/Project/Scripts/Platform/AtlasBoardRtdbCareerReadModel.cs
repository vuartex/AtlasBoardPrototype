using System;
using System.Reflection;
using System.Threading.Tasks;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Unified Career read/presentation adapter (all platforms). Reads an optional
/// RTDB *server-owned* career projection and never writes XP/wins/achievements.
/// When no server-authoritative record exists, renders an explicitly empty
/// zero-value overview instead of an endless 'Loading career data...' banner.
/// LocalOffline guest sessions continue without cloud data or rewards.
/// </summary>
[DefaultExecutionOrder(15600)]
public sealed class AtlasBoardRtdbCareerReadModel : MonoBehaviour
{
    private const string DatabaseUrl =
        "https://atlasboard-usa-default-rtdb.europe-west1.firebasedatabase.app";
    private const float RefreshSeconds = 300f;
    private static AtlasBoardRtdbCareerReadModel instance;
    private static readonly FieldInfo SnapshotField =
        typeof(AtlasBoardProgressionUI).GetField("snapshot",
            BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo RebuildMethod =
        typeof(AtlasBoardProgressionUI).GetMethod("RebuildCurrentPage",
            BindingFlags.Instance | BindingFlags.NonPublic);

    private float nextScan;
    private RectTransform cachedRoot;
    private float lastRefresh;
    private string lastIdentity = string.Empty;
    private bool lastVisible;
    private bool fetching;
    private string lastState = "no_server_record";

    // Expose the latest RTDB read result for diagnostics without persisting
    // or repeatedly rewriting UI. This field is operational state, not dead data.
    public string LastReadState => lastState;

    [Serializable]
    private sealed class CareerWire
    {
        public string accountId;
        public string displayName;
        public string avatarId;
        public int totalXp;
        public int level = 1;
        public int levelStartXp;
        public int nextLevelXp;
        public int maxLevel = 100;
        public AtlasBoardCareerStats stats;
        public AtlasBoardMapCareerStats[] mapStats;
        public AtlasBoardProgressionAchievement[] achievements;
        public AtlasBoardProgressionMatchSummary[] recentMatches;
        public int historyRetentionHours = 72;
        public int historyLimit = 10;
    }

    public static async Task<AtlasBoardProgressionSnapshot> ReadProfileAsync(
        string uid,
        string idToken,
        string fallbackDisplayName)
    {
        if (string.IsNullOrWhiteSpace(uid) ||
            string.IsNullOrWhiteSpace(idToken))
        {
            return FailedProfile(
                "progression.error.identity_unavailable",
                "Authenticated RTDB career identity is unavailable.");
        }

        string endpoint = DatabaseUrl + "/careerProfiles/" +
            UnityWebRequest.EscapeURL(uid) + ".json?auth=" +
            UnityWebRequest.EscapeURL(idToken);

        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            request.timeout = 12;
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            TaskCompletionSource<bool> completion =
                new TaskCompletionSource<bool>();

            operation.completed += _ => completion.TrySetResult(true);
            if (operation.isDone)
                completion.TrySetResult(true);

            await completion.Task;

            if (request.result != UnityWebRequest.Result.Success)
            {
                return FailedProfile(
                    "progression.error.service_unavailable",
                    "RTDB_HTTP_" + request.responseCode);
            }

            string json = request.downloadHandler != null
                ? request.downloadHandler.text
                : string.Empty;

            if (string.IsNullOrWhiteSpace(json) ||
                json.Trim() == "null")
            {
                // A new/legacy account may have no server-owned projection yet.
                // Zero-value Career is valid and must not block startup (ATB120).
                return new AtlasBoardProgressionSnapshot
                {
                    Success = true,
                    AccountId = uid,
                    DisplayName = fallbackDisplayName ?? string.Empty,
                    TotalXp = 0,
                    Level = 1,
                    MaxLevel = 100,
                    HistoryRetentionHours = 72,
                    HistoryLimit = 10,
                    Stats = new AtlasBoardCareerStats(),
                    TechnicalMessage = "empty_authoritative_profile"
                };
            }

            CareerWire wire = null;
            try
            {
                wire = JsonUtility.FromJson<CareerWire>(json);
            }
            catch
            {
                wire = null;
            }

            if (wire == null ||
                !string.Equals(
                    wire.accountId,
                    uid,
                    StringComparison.Ordinal))
            {
                return FailedProfile(
                    "progression.error.service_unavailable",
                    "invalid_rtdb_career_projection");
            }

            AtlasBoardProgressionSnapshot snapshot =
                new AtlasBoardProgressionSnapshot
                {
                    Success = true,
                    AccountId = uid,
                    DisplayName = string.IsNullOrWhiteSpace(wire.displayName)
                        ? fallbackDisplayName ?? string.Empty
                        : wire.displayName,
                    AvatarId = wire.avatarId ?? string.Empty,
                    TotalXp = Mathf.Max(0, wire.totalXp),
                    Level = Mathf.Max(1, wire.level),
                    LevelStartXp = Mathf.Max(0, wire.levelStartXp),
                    NextLevelXp = Mathf.Max(0, wire.nextLevelXp),
                    MaxLevel = Mathf.Max(1, wire.maxLevel),
                    HistoryRetentionHours =
                        wire.historyRetentionHours > 0
                            ? wire.historyRetentionHours
                            : 72,
                    HistoryLimit =
                        wire.historyLimit > 0
                            ? wire.historyLimit
                            : 10,
                    Stats = wire.stats ?? new AtlasBoardCareerStats()
                };

            if (wire.mapStats != null)
                snapshot.MapStats.AddRange(wire.mapStats);
            if (wire.achievements != null)
                snapshot.Achievements.AddRange(wire.achievements);
            if (wire.recentMatches != null)
                snapshot.RecentMatches.AddRange(wire.recentMatches);

            return snapshot;
        }
    }

    private static AtlasBoardProgressionSnapshot FailedProfile(
        string errorKey,
        string technicalMessage)
    {
        return new AtlasBoardProgressionSnapshot
        {
            Success = false,
            ErrorKey = errorKey,
            TechnicalMessage = technicalMessage ?? string.Empty,
            Level = 1,
            MaxLevel = 100,
            HistoryRetentionHours = 72,
            HistoryLimit = 10,
            Stats = new AtlasBoardCareerStats()
        };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject host = new GameObject("AtlasBoard_Career_Rtdb_Read_Model");
        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardRtdbCareerReadModel>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 1.5f;
        AtlasBoardProgressionUI career =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardProgressionUI>(
                FindObjectsInactive.Include);
        if (career == null || SnapshotField == null || RebuildMethod == null)
            return;
        if (cachedRoot == null) cachedRoot = FindCareerRoot();
        RectTransform root = cachedRoot;
        bool visible = root != null && root.gameObject.activeInHierarchy;
        string identity = IdentityKey();
        if (!string.Equals(identity, lastIdentity, StringComparison.Ordinal))
        {
            lastIdentity = identity;
            lastRefresh = -RefreshSeconds;
            lastState = "no_server_record";
            // Do not leak a previous account's Career display after sign-out.
            ApplyEmptyCareer(career, identity);
        }
        if (!visible)
        {
            lastVisible = false;
            return;
        }
        if (!lastVisible)
        {
            lastVisible = true;
            if (SnapshotField.GetValue(career) == null)
                ApplyEmptyCareer(career, identity);
        }
        // Career stats are primary content: no explanatory text may overlap
        // the stat cards. Missing backend remains explicit in diagnostics.
        if (!fetching && identity.StartsWith("uid:", StringComparison.Ordinal) &&
            Time.unscaledTime - lastRefresh >= RefreshSeconds)
        {
            lastRefresh = Time.unscaledTime;
            _ = ReadCareerAsync(career, identity.Substring(4));
        }
    }

    private static RectTransform FindCareerRoot()
    {
        RectTransform[] roots = Resources.FindObjectsOfTypeAll<RectTransform>();
        foreach (RectTransform candidate in roots)
            if (candidate != null && candidate.name == "AtlasCareer" &&
                candidate.gameObject.scene.IsValid()) return candidate;
        return null;
    }

    private static string IdentityKey()
    {
        FirebaseUser user = null;
        try { user = FirebaseAuth.DefaultInstance.CurrentUser; }
        catch { }
        if (user != null && !string.IsNullOrWhiteSpace(user.UserId) &&
            !user.IsAnonymous)
            return "uid:" + user.UserId;
        AtlasBoardPlatformRuntime platform = AtlasBoardPlatformRuntime.Instance;
        if (platform != null && platform.IsSteamProviderActive &&
            platform.SteamInitialized && platform.SteamId != 0)
            return "steam:" + platform.SteamId;
        return "guest:" + AtlasBoardGuestIdentity.DisplayName;
    }

    private static string DisplayName(string identity)
    {
        if (identity.StartsWith("steam:", StringComparison.Ordinal))
            return AtlasBoardPlatformRuntime.Instance != null
                ? (AtlasBoardPlatformRuntime.Instance.SteamPersonaName ?? "Steam Player")
                : "Steam Player";
        if (!identity.StartsWith("uid:", StringComparison.Ordinal))
            return AtlasBoardGuestIdentity.DisplayName;
        TMP_Text[] labels = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (TMP_Text label in labels)
        {
            if (label == null || label.name != "ProfileName" ||
                !label.gameObject.scene.IsValid()) continue;
            string result = (label.text ?? "").Trim();
            int levelIndex = result.LastIndexOf(" • Lv. ", StringComparison.Ordinal);
            if (levelIndex >= 0) result = result.Substring(0, levelIndex);
            if (!string.IsNullOrWhiteSpace(result) &&
                !result.StartsWith("Guest", StringComparison.Ordinal))
                return result;
        }
        return "Atlas Player";
    }

    private static void ApplyEmptyCareer(AtlasBoardProgressionUI career, string identity)
    {
        if (career == null || SnapshotField == null) return;
        SnapshotField.SetValue(career, new AtlasBoardProgressionSnapshot
        {
            Success = false,
            AccountId = identity.StartsWith("uid:", StringComparison.Ordinal)
                ? identity.Substring(4) : string.Empty,
            DisplayName = DisplayName(identity),
            TotalXp = 0,
            Level = 1,
            MaxLevel = 100,
            Stats = new AtlasBoardCareerStats()
        });
        RebuildMethod?.Invoke(career, null);
    }

    private async Task ReadCareerAsync(AtlasBoardProgressionUI career, string uid)
    {
        fetching = true;
        string identityAtStart = lastIdentity;
        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null || user.IsAnonymous || user.UserId != uid)
                return;

            string idToken = await user.TokenAsync(false);
            if (string.IsNullOrEmpty(idToken))
                return;

            AtlasBoardProgressionSnapshot snapshot =
                await ReadProfileAsync(
                    uid,
                    idToken,
                    DisplayName(identityAtStart));

            if (this == null ||
                career == null ||
                lastIdentity != identityAtStart)
            {
                return;
            }

            if (snapshot == null || !snapshot.Success)
            {
                lastState = "unavailable";
                Debug.LogWarning(
                    "ATB_CAREER_RTDB_READ unavailable=true reason=" +
                    (snapshot != null
                        ? snapshot.TechnicalMessage
                        : "null_snapshot") +
                    " (no reward fallback)");
                return;
            }

            SnapshotField.SetValue(career, snapshot);
            RebuildMethod.Invoke(career, null);

            lastState =
                string.Equals(
                    snapshot.TechnicalMessage,
                    "empty_authoritative_profile",
                    StringComparison.Ordinal)
                    ? "empty_authoritative_profile"
                    : "server_projection_loaded";

            Debug.Log(
                "ATB_CAREER_RTDB_READ projection=" +
                (lastState == "server_projection_loaded"
                    ? "true"
                    : "empty") +
                " uidPrefix=" +
                uid.Substring(0, Mathf.Min(7, uid.Length)));
        }
        catch (Exception exception)
        {
            lastState = "unavailable";
            Debug.LogWarning(
                "ATB_CAREER_RTDB_READ failedType=" +
                exception.GetType().Name);
        }
        finally
        {
            fetching = false;
        }
    }

    // Status remains in lastState for diagnostics and secure read decisions.
    // No persistent footer is rendered on top of the Career stat cards.
}
