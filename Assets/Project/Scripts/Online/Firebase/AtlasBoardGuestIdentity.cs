using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;


/// <summary>
/// Device-local identity for unsigned/offline AtlasBoard play.
///
/// Format is deliberately language-neutral and stable across restarts:
/// Guest + five digits + one uppercase ASCII letter, for example Guest48273K.
/// This is probabilistically unique for local presentation; it is NOT a
/// server-certified globally unique account name and grants no online rewards.
/// </summary>
public static class AtlasBoardGuestIdentity
{
    private const string PlayerPrefsKey =
        "atlasboard.identity.guestDisplayName.v1";


    private static string cached;
    // Process-scoped QA alias. Never store anonymous Firebase credentials or
    // this online identity in PlayerPrefs (the offline nickname stays intact).
    private static string liveAuthenticatedUid = string.Empty;
    private static string liveDisplayName = string.Empty;

    public static void BindLiveAuthenticatedUid(string uid)
    {
        if (!AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession ||
            string.IsNullOrWhiteSpace(uid))
            return;

        if (string.Equals(liveAuthenticatedUid, uid, StringComparison.Ordinal))
            return;

        liveAuthenticatedUid = uid;
        liveDisplayName = NameForAuthenticatedUid(uid);
        Debug.Log("ATB_LIVE_GUEST_NAME bound=true source=firebase_anonymous_uid " +
                  "uid_persisted=false name_persisted=false");
    }

    // Same deterministic calculation on the client and the room's host-authored
    // RTDB admission path. 64 bits of a cryptographic digest prevent practical
    // accidental collisions; the room admission code additionally checks names.
    // This is NOT a global nickname reservation or proof of account ownership.
    public static string NameForAuthenticatedUid(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return string.Empty;

        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(uid.Trim()));
            StringBuilder result = new StringBuilder("Guest", 21);
            for (int i = 0; i < 8; i++)
                result.Append(digest[i].ToString("X2"));
            return result.ToString();
        }
    }

    public static bool LooksLikeGuestName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        string candidate = name.Trim();
        if (!candidate.StartsWith("Guest", StringComparison.OrdinalIgnoreCase))
            return false;
        if (candidate.Length == 11)
        {
            for (int i = 5; i < 10; i++)
                if (candidate[i] < '0' || candidate[i] > '9') return false;
            return char.IsLetter(candidate[10]);
        }
        if (candidate.Length == 21)
        {
            for (int i = 5; i < 21; i++)
                if (!Uri.IsHexDigit(candidate[i])) return false;
            return true;
        }
        return false;
    }


    public static string DisplayName
    {
        get
        {
            if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession &&
                !string.IsNullOrWhiteSpace(liveDisplayName))
                return liveDisplayName;
            EnsureCreated();
            return cached;
        }
    }


    public static void EnsureCreated()
    {
        if (IsValid(cached))
            return;


        string stored =
            PlayerPrefs.GetString(
                PlayerPrefsKey,
                string.Empty);


        if (IsValid(stored))
        {
            cached = stored;
            return;
        }


        byte[] bytes = Guid.NewGuid().ToByteArray();
        uint raw = BitConverter.ToUInt32(bytes, 0);
        int number = (int)(raw % 100000u);
        char suffix = (char)('A' + (bytes[4] % 26));


        cached = $"Guest{number:D5}{suffix}";


        PlayerPrefs.SetString(
            PlayerPrefsKey,
            cached);
        PlayerPrefs.Save();


        Debug.Log(
            "ATB_GUEST_IDENTITY created=true name=" + cached +
            " scope=device_local persistent=true");
    }


    private static bool IsValid(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length != 11 ||
            !value.StartsWith("Guest", StringComparison.Ordinal))
        {
            return false;
        }


        for (int i = 5; i < 10; i++)
        {
            if (!char.IsDigit(value[i]))
                return false;
        }


        return value[10] >= 'A' &&
               value[10] <= 'Z';
    }
}


