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
            if (string.IsNullOrEmpty(idToken)) return;
            string endpoint = DatabaseUrl + "/careerProfiles/" +
                UnityWebRequest.EscapeURL(uid) + ".json?auth=" +
                UnityWebRequest.EscapeURL(idToken);
            using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
            {
                request.timeout = 12;
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
                operation.completed += _ => completion.TrySetResult(true);
                if (operation.isDone) completion.TrySetResult(true);
                await completion.Task;
                if (this == null || career == null || lastIdentity != identityAtStart)
                    return;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    lastState = "unavailable";
                    Debug.LogWarning("ATB_CAREER_RTDB_READ unavailable status=" +
                        request.responseCode + " (no reward fallback)");
                    return;
                }
                string json = request.downloadHandler != null
                    ? request.downloadHandler.text : "";
                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
                {
                    lastState = "empty_authoritative_profile";
                    return;
                }
                CareerWire wire = JsonUtility.FromJson<CareerWire>(json);
                if (wire == null || wire.accountId != uid)
                {
                    lastState = "invalid_projection";
                    return;
                }
                // Values are trusted only because careerProfiles is non-client-
                // writable in RTDB Rules. No client ever writes to this path.
                AtlasBoardProgressionSnapshot snapshot =
                    new AtlasBoardProgressionSnapshot
                    {
                        Success = true,
                        AccountId = uid,
                        DisplayName = string.IsNullOrEmpty(wire.displayName)
                            ? DisplayName(identityAtStart) : wire.displayName,
                        AvatarId = wire.avatarId ?? "",
                        TotalXp = Mathf.Max(0, wire.totalXp),
                        Level = Mathf.Max(1, wire.level),
                        LevelStartXp = Mathf.Max(0, wire.levelStartXp),
                        NextLevelXp = Mathf.Max(0, wire.nextLevelXp),
                        MaxLevel = Mathf.Max(1, wire.maxLevel),
                        Stats = wire.stats ?? new AtlasBoardCareerStats()
                    };
                if (wire.mapStats != null) snapshot.MapStats.AddRange(wire.mapStats);
                if (wire.achievements != null)
                    snapshot.Achievements.AddRange(wire.achievements);
                if (wire.recentMatches != null)
                    snapshot.RecentMatches.AddRange(wire.recentMatches);
                SnapshotField.SetValue(career, snapshot);
                RebuildMethod.Invoke(career, null);
                lastState = "server_projection_loaded";
                Debug.Log("ATB_CAREER_RTDB_READ projection=true uidPrefix=" +
                    uid.Substring(0, Mathf.Min(7, uid.Length)));
            }
        }
        catch (Exception exception)
        {
            lastState = "unavailable";
            Debug.LogWarning("ATB_CAREER_RTDB_READ failedType=" +
                exception.GetType().Name);
        }
        finally { fetching = false; }
    }

    // Status remains in lastState for diagnostics and secure read decisions.
    // No persistent footer is rendered on top of the Career stat cards.
}
