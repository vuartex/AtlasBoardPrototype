using System;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;

/// <summary>
/// Preserves Steam/Firebase isolation. Never chooses account ownership by
/// display name. Production ownership requires a verified Steam ticket and
/// the server-side Steam-to-UID link. Local QA fallback is provided by
/// AtlasBoardSteamDevelopmentSession only in Windows Editor/Development.
/// </summary>
[DefaultExecutionOrder(-30000)]
public sealed class AtlasBoardSteamIdentitySessionFence : MonoBehaviour
{
    private static AtlasBoardSteamIdentitySessionFence instance;
    private ulong activeSteamId;
    private string acceptedUid = string.Empty;
    private Task localRecovery;
    private bool recoveryFailed;
    private string reportedFailure = string.Empty;
    private FirebaseAuth observedAuth;
    private bool repairingOwner;
    private float retryOwnerAt;
    private bool insideAuthEvent;
    private int authRevision;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject root = new GameObject("AtlasBoard_Steam_Identity_Session_Fence");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<AtlasBoardSteamIdentitySessionFence>();
    }

    private void OnDestroy()
    {
        if (observedAuth != null) observedAuth.StateChanged -= OnAuthStateChanged;
        if (instance == this) instance = null;
    }

    // Firebase's Auth state can change while Career, Shop or Profile awaits
    // another operation. This observer rejects cross-UID transitions before
    // a new user can be written into the RTDB account mirror.
    private void ObserveAuth(FirebaseAuth auth)
    {
        if (auth == null || ReferenceEquals(auth, observedAuth)) return;
        if (observedAuth != null) observedAuth.StateChanged -= OnAuthStateChanged;
        observedAuth = auth;
        observedAuth.StateChanged += OnAuthStateChanged;
    }

    private void OnAuthStateChanged(object sender, System.EventArgs args)
    {
#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && (UNITY_EDITOR || DEVELOPMENT_BUILD)
        if (insideAuthEvent || acceptedUid.Length == 0 || activeSteamId == 0 || repairingOwner)
            return;
        if (!OwnerMismatch()) return;
        insideAuthEvent = true;
        try
        {
            Debug.LogWarning("ATB_STEAM_IDENTITY_FENCE unexpected_auth_change=true code=ATB833 restoring_owner=true");
            // The wrong signed-in identity is not allowed to serve as a room,
            // wallet, achievement or profile principal even temporarily.
            if (observedAuth != null && observedAuth.CurrentUser != null)
                observedAuth.SignOut();
            ResetVisibleOwner();
            StartOwnerRepair();
        }
        finally { insideAuthEvent = false; }
#endif
    }

    private bool OwnerMismatch()
    {
        FirebaseUser u = observedAuth != null ? observedAuth.CurrentUser : null;
        return u == null || !string.Equals(u.UserId, acceptedUid, StringComparison.Ordinal);
    }

    private void StartOwnerRepair()
    {
#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && (UNITY_EDITOR || DEVELOPMENT_BUILD)
        if (repairingOwner || activeSteamId == 0 || Time.unscaledTime < retryOwnerAt) return;
        repairingOwner = true;
        _ = RestoreAcceptedOwnerAsync(activeSteamId, ++authRevision);
#endif
    }

#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && (UNITY_EDITOR || DEVELOPMENT_BUILD)
    private async Task RestoreAcceptedOwnerAsync(ulong steamId, int revision)
    {
        try
        {
            string restored = await AtlasBoardSteamDevelopmentSession.EnsureAsync(steamId);
            if (this == null || steamId != activeSteamId || revision != authRevision) return;
            if (restored.Length == 0 ||
                !AtlasBoardSteamDevelopmentSession.CurrentUidMatchesStoredSlot(steamId))
            {
                Debug.LogError("ATB_STEAM_IDENTITY_FENCE restore_pending=true code=ATB834");
                retryOwnerAt = Time.unscaledTime + 4f;
                return;
            }
            acceptedUid = string.Empty;
            Accept(restored, "steam_development_recovered_after_auth_change");
        }
        catch (Exception ex)
        {
            Debug.LogError("ATB_STEAM_IDENTITY_FENCE restore_failed=" + ex.GetType().Name +
                " code=ATB834");
            retryOwnerAt = Time.unscaledTime + 4f;
        }
        finally { repairingOwner = false; }
    }
#endif

    private void Update()
    {
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession ||
            AtlasBoardAppCheckTokenSource.IsLocalEmulatorGuestSession) return;
        AtlasBoardPlatformRuntime platform = AtlasBoardPlatformRuntime.Instance;
        if (platform == null || !platform.IsSteamProviderActive ||
            !platform.SteamInitialized || platform.SteamId == 0) return;

        ulong steamId = platform.SteamId;
        if (activeSteamId != steamId)
        {
            activeSteamId = steamId;
            acceptedUid = string.Empty;
            recoveryFailed = false;
            reportedFailure = string.Empty;
            localRecovery = null;
            retryOwnerAt = 0;
            authRevision++;
            // Never show the preceding Steam user's wallet while switching.
            ResetVisibleOwner();
        }

        AtlasBoardSteamAccountLinkBridge bridge = AtlasBoardSteamAccountLinkBridge.Instance;
        AtlasBoardSteamReturningSignInResult verified = bridge != null
            ? bridge.LastReturningSignIn : null;
        FirebaseUser user;
        try
        {
            FirebaseAuth auth = FirebaseAuth.DefaultInstance;
            ObserveAuth(auth);
            user = auth.CurrentUser;
        }
        catch { return; }
#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && (UNITY_EDITOR || DEVELOPMENT_BUILD)
        // Do not trust the cached completion of a previous Steam sign-in.
        // Always compare the CURRENT Auth user against the accepted Steam UID.
        if (acceptedUid.Length > 0 && !string.Equals(user != null ? user.UserId : "",
                acceptedUid, StringComparison.Ordinal))
        {
            StartOwnerRepair();
            return;
        }
#endif
        string uid = user != null ? user.UserId ?? string.Empty : string.Empty;
        bool serverProven = verified != null && verified.Success &&
            verified.FirebaseAuthApplied &&
            (verified.Verified || (bridge != null && bridge.UsingLocalEmulators && verified.DevelopmentOnly)) &&
            string.Equals(verified.SteamId, steamId.ToString(), StringComparison.Ordinal) &&
            string.Equals(verified.AccountId, uid, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(uid);
        if (serverProven)
        {
            Accept(uid, "steam_server_verified");
            return;
        }

#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && (UNITY_EDITOR || DEVELOPMENT_BUILD)
        // Do not SignOut on every startup: that destroys the persisted Firebase
        // anonymous login and forces a new UID on every relaunch. Development
        // sessions are instead reauthenticated from Windows DPAPI-encrypted
        // credentials bound to the exact SteamID, never the persona string.
        if (localRecovery == null && !recoveryFailed)
            localRecovery = RecoverDevelopmentSessionAsync(steamId);
        else if (acceptedUid.Length > 0 && OwnerMismatch())
            StartOwnerRepair();
        return;
#else
        // Retail cannot trust a local mapping or guess a Steam UID. Without a
        // verified server link, fail closed instead of minting a new wallet.
        if (user != null)
        {
            FirebaseAuth.DefaultInstance.SignOut();
            ResetVisibleOwner();
        }
        if (verified != null && !verified.Success && reportedFailure != verified.ErrorKey)
        {
            reportedFailure = verified.ErrorKey;
            Debug.LogWarning("ATB_STEAM_RETURNING_AUTH_FAIL code=ATB721");
        }
#endif
    }

#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && (UNITY_EDITOR || DEVELOPMENT_BUILD)
    private async Task RecoverDevelopmentSessionAsync(ulong steamId)
    {
        try
        {
            string recovered = await AtlasBoardSteamDevelopmentSession.EnsureAsync(steamId);
            if (this == null || activeSteamId != steamId) return;
            if (string.IsNullOrWhiteSpace(recovered))
            {
                recoveryFailed = true; // fail closed, never loop new anonymous IDs
                Debug.LogError("ATB_STEAM_LOCAL_SESSION failed=true code=ATB721 no_new_wallet_retry=true");
                return;
            }
            Accept(recovered, "steam_development_local_dpapi");
        }
        catch (Exception error)
        {
            recoveryFailed = true;
            Debug.LogError("ATB_STEAM_LOCAL_SESSION failed=true code=ATB721 category=" + error.GetType().Name);
        }
    }
#endif

    private void Accept(string uid, string mode)
    {
        if (acceptedUid == uid) return;
        acceptedUid = uid;
        RefreshVisibleOwner();
        Debug.Log("ATB_STEAM_IDENTITY ready=true uid_stable=true mode=" + mode +
            " uid_prefix=" + uid.Substring(0, Math.Min(6, uid.Length)));
    }

    private static void ResetVisibleOwner()
    {
        var lobby = FindAnyObjectByType<AtlasBoardLobbyRuntimeBridge>(FindObjectsInactive.Include);
        if (lobby != null) lobby.ClearLocalLobbyState();
        var header = FindAnyObjectByType<AtlasBoardCanonicalProfileHeader>(FindObjectsInactive.Include);
        if (header != null) header.RefreshNow();
        _ = ReloadWalletAsync();
    }

    private static void RefreshVisibleOwner()
    {
        var header = FindAnyObjectByType<AtlasBoardCanonicalProfileHeader>(FindObjectsInactive.Include);
        if (header != null) header.RefreshNow();
        _ = ReloadWalletAsync();
    }

    private static async Task ReloadWalletAsync()
    {
        try { await AtlasBoardRtdbMetaEconomy.LoadAsync(); }
        catch (Exception ex) { Debug.LogWarning("ATB_STEAM_IDENTITY wallet_sync=" + ex.GetType().Name); }
    }
}
