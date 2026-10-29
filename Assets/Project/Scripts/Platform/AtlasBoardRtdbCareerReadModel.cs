using System;
using System.Reflection;
using System.Threading.Tasks;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Canonical RTDB Career reader. Server projection remains primary; when no
/// authoritative projection exists, includes bounded peer-attested, non-currency
/// Career claims from the same account. Does not grant any client-owned Gold.
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
        string uid, string idToken, string fallbackDisplayName)
    {
        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(idToken))
            return FailedProfile("progression.error.identity_unavailable",
                "Authenticated RTDB Career identity unavailable.");
        string endpoint = DatabaseUrl + "/careerProfiles/" +
            UnityWebRequest.EscapeURL(uid) + ".json?auth=" +
            UnityWebRequest.EscapeURL(idToken);
        AtlasBoardProgressionSnapshot snapshot;
        using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
        {
            request.timeout = 12;
            await AtlasBoardAppCheckRequestSecurity.ApplyAsync(request);
            var completion = new TaskCompletionSource<bool>();
            var operation = request.SendWebRequest();
            operation.completed += _ => completion.TrySetResult(true);
            if (operation.isDone) completion.TrySetResult(true);
            await completion.Task;
            if (request.result != UnityWebRequest.Result.Success)
                return FailedProfile("progression.error.service_unavailable",
                    "RTDB_HTTP_" + request.responseCode);
            string json = request.downloadHandler != null ?
                request.downloadHandler.text : "null";
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
                snapshot = new AtlasBoardProgressionSnapshot
                {
                    Success = true, AccountId = uid,
                    DisplayName = fallbackDisplayName ?? string.Empty,
                    TotalXp = 0, Level = 1, MaxLevel = 100,
                    HistoryRetentionHours = 72, HistoryLimit = 10,
                    Stats = new AtlasBoardCareerStats(),
                    TechnicalMessage = "empty_authoritative_profile"
                };
            else
            {
                CareerWire wire;
                try { wire = JsonUtility.FromJson<CareerWire>(json); }
                catch { wire = null; }
                if (wire == null || !string.Equals(wire.accountId, uid, StringComparison.Ordinal))
                    return FailedProfile("progression.error.service_unavailable",
                        "invalid_rtdb_career_projection");
                snapshot = new AtlasBoardProgressionSnapshot
                {
                    Success = true, AccountId = uid,
                    DisplayName = string.IsNullOrWhiteSpace(wire.displayName) ?
                        fallbackDisplayName ?? string.Empty : wire.displayName,
                    AvatarId = wire.avatarId ?? string.Empty,
                    TotalXp = Mathf.Max(0, wire.totalXp), Level = Mathf.Max(1, wire.level),
                    LevelStartXp = Mathf.Max(0, wire.levelStartXp),
                    NextLevelXp = Mathf.Max(0, wire.nextLevelXp),
                    MaxLevel = Mathf.Max(1, wire.maxLevel),
                    HistoryRetentionHours = wire.historyRetentionHours > 0 ? wire.historyRetentionHours : 72,
                    HistoryLimit = wire.historyLimit > 0 ? wire.historyLimit : 10,
                    Stats = wire.stats ?? new AtlasBoardCareerStats()
                };
                if (wire.mapStats != null) snapshot.MapStats.AddRange(wire.mapStats);
                if (wire.achievements != null) snapshot.Achievements.AddRange(wire.achievements);
                if (wire.recentMatches != null) snapshot.RecentMatches.AddRange(wire.recentMatches);
            }
        }
        try { return await AtlasBoardRtdbCareerJournal.MergeAsync(snapshot, uid, idToken); }
        catch (Exception exception)
        {
            Debug.LogWarning("ATB_CAREER_RTDB_READ journal_unavailable=" +
                exception.GetType().Name);
            return snapshot;
        }
    }

    private static AtlasBoardProgressionSnapshot FailedProfile(string errorKey, string technicalMessage)
    {
        return new AtlasBoardProgressionSnapshot {
            Success = false, ErrorKey = errorKey,
            TechnicalMessage = technicalMessage ?? string.Empty,
            Level = 1, MaxLevel = 100, HistoryRetentionHours = 72,
            HistoryLimit = 10, Stats = new AtlasBoardCareerStats()
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
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 1.5f;
        AtlasBoardProgressionUI career =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardProgressionUI>(FindObjectsInactive.Include);
        if (career == null || SnapshotField == null || RebuildMethod == null) return;
        if (cachedRoot == null || !cachedRoot.gameObject.scene.IsValid()) cachedRoot = FindCareerRoot();
        bool visible = cachedRoot != null && cachedRoot.gameObject.activeInHierarchy;
        string identity = IdentityKey();
        if (!string.Equals(identity, lastIdentity, StringComparison.Ordinal))
        {
            lastIdentity = identity; lastRefresh = -RefreshSeconds;
            lastState = "no_server_record";
            ApplyEmptyCareer(career, identity);
        }
        if (!visible) { lastVisible = false; return; }
        if (!lastVisible)
        {
            lastVisible = true;
            // Refresh immediately when returning to Career after a completed
            // match; never keep a five-minute-old snapshot on first reopen.
            lastRefresh = -RefreshSeconds;
            if (SnapshotField.GetValue(career) == null)
                ApplyEmptyCareer(career, identity);
        }
        if (!fetching && identity.StartsWith("uid:", StringComparison.Ordinal) &&
            Time.unscaledTime - lastRefresh >= RefreshSeconds)
        {
            lastRefresh = Time.unscaledTime;
            _ = ReadCareerAsync(career, identity.Substring(4));
        }
    }

    private static RectTransform FindCareerRoot()
    {
        foreach (RectTransform candidate in Resources.FindObjectsOfTypeAll<RectTransform>())
            if (candidate != null && candidate.name == "AtlasCareer" &&
                candidate.gameObject.scene.IsValid()) return candidate;
        return null;
    }

    private static string IdentityKey()
    {
        FirebaseUser user = null;
        try { user = FirebaseAuth.DefaultInstance.CurrentUser; } catch { }
        if (user != null && !string.IsNullOrWhiteSpace(user.UserId) && !user.IsAnonymous)
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
        foreach (TMP_Text label in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (label == null || label.name != "ProfileName" || !label.gameObject.scene.IsValid())
                continue;
            string value = (label.text ?? "").Trim();
            int levelIndex = value.LastIndexOf(" • Lv. ", StringComparison.Ordinal);
            if (levelIndex >= 0) value = value.Substring(0, levelIndex);
            if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("Guest", StringComparison.Ordinal))
                return value;
        }
        return "Atlas Player";
    }

    private static void ApplyEmptyCareer(AtlasBoardProgressionUI career, string identity)
    {
        if (career == null || SnapshotField == null) return;
        SnapshotField.SetValue(career, new AtlasBoardProgressionSnapshot {
            Success = false,
            AccountId = identity.StartsWith("uid:", StringComparison.Ordinal) ? identity.Substring(4) : string.Empty,
            DisplayName = DisplayName(identity), TotalXp = 0, Level = 1,
            MaxLevel = 100, Stats = new AtlasBoardCareerStats()
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
            if (user == null || user.IsAnonymous || user.UserId != uid) return;
            string token = await user.TokenAsync(false);
            if (string.IsNullOrEmpty(token)) return;
            AtlasBoardProgressionSnapshot snapshot =
                await ReadProfileAsync(uid, token, DisplayName(identityAtStart));
            if (this == null || career == null || lastIdentity != identityAtStart) return;
            if (snapshot == null || !snapshot.Success)
            {
                lastState = "unavailable";
                Debug.LogWarning("ATB_CAREER_RTDB_READ unavailable=true reason=" +
                    (snapshot != null ? snapshot.TechnicalMessage : "null_snapshot"));
                return;
            }
            SnapshotField.SetValue(career, snapshot);
            RebuildMethod.Invoke(career, null);
            lastState = snapshot.TechnicalMessage == "peer_attested_not_server_verified"
                ? "peer_attested_provisional"
                : snapshot.TechnicalMessage == "empty_authoritative_profile"
                    ? "empty_authoritative_profile" : "server_projection_loaded";
            Debug.Log("ATB_CAREER_RTDB_READ state=" + lastState +
                " uidPrefix=" + uid.Substring(0, Mathf.Min(7, uid.Length)));
        }
        catch (Exception ex)
        {
            lastState = "unavailable";
            Debug.LogWarning("ATB_CAREER_RTDB_READ failedType=" + ex.GetType().Name);
        }
        finally { fetching = false; }
    }
}
