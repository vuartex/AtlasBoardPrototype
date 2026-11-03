#if (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN) && (UNITY_EDITOR || DEVELOPMENT_BUILD)
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using UnityEngine.Networking;
using UnityEngine;

/// <summary>
/// WINDOWS DEVELOPMENT-ONLY bridge for Steam identity QA when the trusted
/// production Steam returning-sign-in callable is not deployed. The local
/// credential is random, per SteamID, Windows-DPAPI encrypted; the same UID
/// survives relaunches and switching between Steam accounts on this PC.
/// NOT a retail cross-device Steam login or replacement for server proofs.
/// </summary>
public static class AtlasBoardSteamDevelopmentSession
{
    private const string SlotFormat = "ATB_STEAM_QA_SLOT_V1";
    private const string LastOwnerFile = "atlasboard_steam_qa_last_owner.dpapi";
    private static readonly object Gate = new object();
    private static Task<string> operation;
    private static ulong operationSteamId;

    [Serializable]
    private sealed class Slot
    {
        public string format = SlotFormat;
        public string steamId;
        public string uid;
        public string email;
        public string password;
        public bool linked;
    }

    public static Task<string> EnsureAsync(ulong steamId)
    {
        if (steamId == 0) return Task.FromResult(string.Empty);
        lock (Gate)
        {
            // A completed Task<string> only proves a PAST login. The Auth
            // provider can be changed by another subsystem at any time.
            // Revalidate the actual Firebase UID before reusing the task.
            if (operation != null && operationSteamId == steamId)
            {
                if (!operation.IsCompleted) return operation;
                if (CurrentUidMatchesStoredSlot(steamId)) return operation;
                operation = null; // force re-authentication, NOT new account creation
            }
            if (operation != null && !operation.IsCompleted)
                return Task.FromResult(string.Empty); // no cross-Steam concurrent auth
            operationSteamId = steamId;
            operation = EnsureCoreAsync(steamId);
            return operation;
        }
    }

    public static bool CurrentUidMatchesStoredSlot(ulong steamId)
    {
        if (steamId == 0) return false;
        Slot slot = ReadSlot(steamId.ToString());
        if (slot == null || !slot.linked) return false;
        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            return user != null && string.Equals(user.UserId, slot.uid,
                StringComparison.Ordinal);
        }
        catch { return false; }
    }

    private static async Task<string> EnsureCoreAsync(ulong steamId)
    {
        DependencyStatus available = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (available != DependencyStatus.Available) return string.Empty;
        FirebaseAuth auth = FirebaseAuth.DefaultInstance;
        string steam = steamId.ToString();
        Slot existing = ReadSlot(steam);
        // A damaged/unreadable credential file must NEVER be treated as a new
        // Steam player: doing so would allocate a fresh UID and hide purchases.
        if (File.Exists(SlotPath(steam)) && existing == null)
        {
            Debug.LogError("ATB_STEAM_LOCAL_SESSION slot_unreadable=true code=ATB006 no_new_uid=true");
            return string.Empty;
        }
        FirebaseUser current = auth.CurrentUser;

        if (existing != null)
        {
            // A previous launch may have stored a pending slot before its
            // authenticated provider upgrade completed. If this exact UID is
            // still available, finish that SAME link rather than generating a
            // different user or replacing an existing wallet.
            if (!existing.linked && current != null &&
                string.Equals(current.UserId, existing.uid, StringComparison.Ordinal))
            {
                bool completed = await UpgradeAnonymousSlotAsync(current, existing);
                if (!completed) return string.Empty;
                SaveLastOwner(steam);
                return existing.uid;
            }
            // No silent wallet reset when returning to a previously linked
            // Steam account. Reauthenticate that SteamID's exact Firebase UID.
            if (!string.IsNullOrWhiteSpace(existing.uid) && current != null &&
                string.Equals(current.UserId, existing.uid, StringComparison.Ordinal))
            {
                SaveLastOwner(steam);
                return current.UserId;
            }
            try
            {
                AuthResult restored = await auth.SignInWithEmailAndPasswordAsync(
                    existing.email, existing.password);
                string uid = restored?.User?.UserId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(existing.uid) ||
                    !string.Equals(uid, existing.uid, StringComparison.Ordinal))
                {
                    auth.SignOut();
                    Debug.LogError("ATB_STEAM_LOCAL_SESSION restored_uid_mismatch=true code=ATB831");
                    return string.Empty;
                }
                SaveLastOwner(steam);
                return uid;
            }
            catch (Exception e)
            {
                Debug.LogWarning("ATB_STEAM_LOCAL_SESSION restore_failed=true category=" + e.GetType().Name);
                return string.Empty; // no replacement user, no wallet overwrite
            }
        }

        // A persisted user from a DIFFERENT SteamID cannot be adopted.
        string lastOwner = ReadLastOwner();
        if (File.Exists(OwnerPath()) && string.IsNullOrEmpty(lastOwner))
            return string.Empty; // corrupted owner stamp -> never adopt foreign wallet
        // If a previous session was persisted for THIS SteamID but its slot
        // disappeared, fail closed. Creating a new UID would hide paid assets.
        if (existing == null && lastOwner == steam)
        {
            Debug.LogError("ATB_STEAM_LOCAL_SESSION missing_owner_slot=true code=ATB832 no_new_uid=true");
            return string.Empty;
        }
        if (current != null && !string.IsNullOrEmpty(lastOwner) && lastOwner != steam)
        {
            auth.SignOut();
            current = null;
        }
        if (current != null && !current.IsAnonymous)
        {
            // Do not take ownership of somebody's email/registered account.
            Debug.LogWarning("ATB_STEAM_LOCAL_SESSION existing_registered_unclaimed=true code=ATB721");
            return string.Empty;
        }
        if (current == null)
        {
            AuthResult fresh = await auth.SignInAnonymouslyAsync();
            current = fresh?.User;
        }
        if (current == null || !current.IsAnonymous || string.IsNullOrWhiteSpace(current.UserId))
            return string.Empty;

        // Creation of the credentials and durable slot precedes linking; after
        // an interrupted link, the same slot can be recovered on next launch.
        Slot slot = new Slot {
            steamId = steam,
            uid = current.UserId,
            email = "atb-qa-" + RandomHex(16).ToLowerInvariant() + "@example.com",
            password = "Q!" + RandomHex(32),
            linked = false
        };
        if (!SaveSlot(slot)) return string.Empty;
        if (!await UpgradeAnonymousSlotAsync(current, slot)) return string.Empty;
        SaveLastOwner(steam);
        Debug.Log("ATB_STEAM_LOCAL_SESSION bound=true initial_steam_uid_persisted=true");
        return slot.uid;
    }

    private static async Task<bool> UpgradeAnonymousSlotAsync(FirebaseUser current, Slot slot)
    {
        try
        {
            // Identity Toolkit accounts:signUp with an ID token upgrades the
            // existing anonymous UID in place. Do not create an independent
            // email/password Firebase user (and do not use a password from
            // any user-visible account form).
            string idToken = await current.TokenAsync(true);
            if (string.IsNullOrWhiteSpace(idToken)) return false;
            string apiKey = FirebaseApp.DefaultInstance.Options.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey)) return false;
            string endpoint = "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" +
                UnityWebRequest.EscapeURL(apiKey);
            string payload = "{\"idToken\":\"" + idToken + "\",\"email\":\"" + slot.email +
                "\",\"password\":\"" + slot.password + "\",\"returnSecureToken\":true}";
            using (UnityWebRequest request = new UnityWebRequest(endpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 20;
                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();
                if (request.result != UnityWebRequest.Result.Success ||
                    request.responseCode < 200 || request.responseCode >= 300)
                {
                    Debug.LogError("ATB_STEAM_LOCAL_SESSION link_http_failed=true http=" +
                        request.responseCode + " source_uid_preserved=true");
                    return false;
                }
                RestUpgradeResult linked = null;
                try { linked = JsonUtility.FromJson<RestUpgradeResult>(request.downloadHandler.text); }
                catch { /* reject malformed response */ }
                if (linked == null ||
                    !string.Equals(linked.localId, slot.uid, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(linked.idToken))
                {
                    Debug.LogError("ATB_STEAM_LOCAL_SESSION uid_contract_failed=true");
                    return false;
                }
            }
            // Update native provider information without signing out. The
            // native Firebase principal keeps the identical UID and wallet.
            try { await current.ReloadAsync(); }
            catch { /* next authenticated refresh will load provider state */ }
            slot.linked = true;
            if (!SaveSlot(slot))
            {
                Debug.LogError("ATB_STEAM_LOCAL_SESSION linked_but_slot_save_failed=true code=ATB007");
                return false;
            }
            return true;
        }
        catch (Exception error)
        {
            Debug.LogError("ATB_STEAM_LOCAL_SESSION link_failed=true category=" + error.GetType().Name +
                " source_uid_preserved=true");
            return false;
        }
    }

    [Serializable]
    private sealed class RestUpgradeResult
    {
        public string localId;
        public string idToken;
        public string refreshToken;
    }

    private static string SlotPath(string steam) => Path.Combine(
        Application.persistentDataPath, "atlasboard_steam_qa_" + steam + ".dpapi");
    private static string OwnerPath() => Path.Combine(Application.persistentDataPath, LastOwnerFile);

    private static string RandomHex(int bytes)
    {
        byte[] data = new byte[bytes];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(data);
        StringBuilder hex = new StringBuilder(bytes * 2);
        foreach (byte v in data) hex.Append(v.ToString("x2"));
        return hex.ToString();
    }

    private static Slot ReadSlot(string steam)
    {
        try
        {
            string path = SlotPath(steam);
            if (!File.Exists(path) || !Transform(File.ReadAllBytes(path), false, out byte[] plain))
                return null;
            Slot slot = JsonUtility.FromJson<Slot>(Encoding.UTF8.GetString(plain));
            if (slot == null || slot.format != SlotFormat || slot.steamId != steam ||
                string.IsNullOrWhiteSpace(slot.uid) || string.IsNullOrWhiteSpace(slot.email) ||
                string.IsNullOrWhiteSpace(slot.password)) return null;
            return slot;
        }
        catch (Exception error)
        {
            Debug.LogWarning("ATB_STEAM_LOCAL_SESSION slot_read_failed=" + error.GetType().Name);
            return null;
        }
    }

    private static bool SaveSlot(Slot slot)
    {
        byte[] raw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(slot));
        return WriteEncrypted(SlotPath(slot.steamId), raw);
    }

    private static string ReadLastOwner()
    {
        try
        {
            if (!File.Exists(OwnerPath()) ||
                !Transform(File.ReadAllBytes(OwnerPath()), false, out byte[] data)) return string.Empty;
            return Encoding.UTF8.GetString(data);
        }
        catch { return string.Empty; }
    }

    private static void SaveLastOwner(string steam)
    {
        if (!WriteEncrypted(OwnerPath(), Encoding.UTF8.GetBytes(steam)))
            Debug.LogWarning("ATB_STEAM_LOCAL_SESSION owner_stamp_not_saved=true code=ATB007");
    }

    private static bool WriteEncrypted(string file, byte[] raw)
    {
        if (!Transform(raw, true, out byte[] secured)) return false;
        string tmp = file + ".tmp";
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllBytes(tmp, secured);
            if (File.Exists(file)) File.Replace(tmp, file, null);
            else File.Move(tmp, file);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("ATB_STEAM_LOCAL_SESSION storage_failed=" + e.GetType().Name);
            return false;
        }
        finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { public int Length; public IntPtr Data; }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob data, string description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob data, IntPtr description,
        IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr handle);

    private static bool Transform(byte[] raw, bool protect, out byte[] result)
    {
        result = null;
        if (raw == null || raw.Length == 0) return false;
        Blob input = new Blob {Length = raw.Length, Data = Marshal.AllocHGlobal(raw.Length)};
        Blob output = new Blob();
        try
        {
            Marshal.Copy(raw, 0, input.Data, raw.Length);
            bool ok = protect
                ? CryptProtectData(ref input, "AtlasBoard Steam QA", IntPtr.Zero,
                    IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero,
                    IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok || output.Data == IntPtr.Zero || output.Length <= 0) return false;
            result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero) LocalFree(output.Data);
        }
    }
}
#endif
