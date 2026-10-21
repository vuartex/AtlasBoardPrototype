using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;


[DefaultExecutionOrder(16000)]
[DisallowMultipleComponent]
public sealed class AtlasBoardUnifiedProfilePresentation :
    MonoBehaviour
{
    private const float ReferenceRefreshInterval =
        0.75f;


    private const string LevelSeparator =
        " • Lv. ";


    private static AtlasBoardUnifiedProfilePresentation
        instance;


    private readonly List<TMP_Text> profileNameTexts =
        new List<TMP_Text>();


    private readonly List<TMP_Text> avatarInitialTexts =
        new List<TMP_Text>();


    private AtlasBoardCanonicalProfileHeader
        canonicalHeader;


    private AtlasBoardMainMenuController
        mainMenuController;


    private AtlasBoardPrivateLobbyUIController
        privateLobbyController;


    private FieldInfo canonicalDisplayNameField;
    private FieldInfo menuProfileNameField;


    private FieldInfo lobbySeatNamesField;
    private FieldInfo lobbySnapshotField;
    private FieldInfo lobbyLocalAccountIdField;
    private FieldInfo lobbyRoomActiveField;
    private FieldInfo lobbyLocalIsHostField;


    private string canonicalDisplayName =
        string.Empty;


    private float nextReferenceRefreshAt;


    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }


        AtlasBoardUnifiedProfilePresentation existing =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardUnifiedProfilePresentation>();


        if (existing != null)
        {
            instance = existing;
            return;
        }


        GameObject root =
            new GameObject(
                "AtlasBoard_UnifiedProfilePresentation");


        DontDestroyOnLoad(
            root);


        instance =
            root.AddComponent<
                AtlasBoardUnifiedProfilePresentation>();
    }


    private void Awake()
    {
        if (instance != null &&
            instance != this)
        {
            Destroy(
                gameObject);


            return;
        }


        instance = this;


        ResolveReflection();
        RefreshReferences();
    }


    private void LateUpdate()
    {
        if (Time.unscaledTime >=
            nextReferenceRefreshAt)
        {
            nextReferenceRefreshAt =
                Time.unscaledTime +
                ReferenceRefreshInterval;


            RefreshReferences();
        }


        string resolved =
            ResolveCanonicalDisplayName();


        if (string.IsNullOrWhiteSpace(
                resolved))
        {
            return;
        }


        canonicalDisplayName =
            resolved.Trim();


        SynchronizeMainMenuProfileSource();
        SynchronizeSharedProfileCards();
        SynchronizeLocalLobbySeat();
    }


    private void ResolveReflection()
    {
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.NonPublic;


        canonicalDisplayNameField =
            typeof(
                AtlasBoardCanonicalProfileHeader)
                .GetField(
                    "appliedDisplayName",
                    flags);


        menuProfileNameField =
            typeof(
                AtlasBoardMainMenuController)
                .GetField(
                    "profileName",
                    flags);


        lobbySeatNamesField =
            typeof(
                AtlasBoardPrivateLobbyUIController)
                .GetField(
                    "privateSeatNames",
                    flags);


        lobbySnapshotField =
            typeof(
                AtlasBoardPrivateLobbyUIController)
                .GetField(
                    "backendSnapshot",
                    flags);


        lobbyLocalAccountIdField =
            typeof(
                AtlasBoardPrivateLobbyUIController)
                .GetField(
                    "backendLocalAccountId",
                    flags);


        lobbyRoomActiveField =
            typeof(
                AtlasBoardPrivateLobbyUIController)
                .GetField(
                    "roomActive",
                    flags);


        lobbyLocalIsHostField =
            typeof(
                AtlasBoardPrivateLobbyUIController)
                .GetField(
                    "localIsHost",
                    flags);
    }


    private void RefreshReferences()
    {
        if (canonicalHeader == null)
        {
            canonicalHeader =
                UnityEngine.Object.FindAnyObjectByType<
                    AtlasBoardCanonicalProfileHeader>();
        }


        if (mainMenuController == null)
        {
            mainMenuController =
                UnityEngine.Object.FindAnyObjectByType<
                    AtlasBoardMainMenuController>();
        }


        if (privateLobbyController == null)
        {
            privateLobbyController =
                UnityEngine.Object.FindAnyObjectByType<
                    AtlasBoardPrivateLobbyUIController>();
        }


        profileNameTexts.Clear();
        avatarInitialTexts.Clear();


        TMP_Text[] texts =
            Resources.FindObjectsOfTypeAll<
                TMP_Text>();


        foreach (TMP_Text text
                 in texts)
        {
            if (text == null ||
                !text.gameObject.scene.IsValid())
            {
                continue;
            }


            if (string.Equals(
                    text.gameObject.name,
                    "ProfileName",
                    StringComparison.Ordinal))
            {
                profileNameTexts.Add(
                    text);


                continue;
            }


            if (string.Equals(
                    text.gameObject.name,
                    "AvatarInitial",
                    StringComparison.Ordinal))
            {
                avatarInitialTexts.Add(
                    text);
            }
        }
    }


    private string ResolveCanonicalDisplayName()
    {
        // Live QA's authoritative local alias becomes available only after
        // the anonymous Auth UID is assigned. A stale header snapshot must
        // never overwrite the UID-bound alias in the next LateUpdate.
        if (AtlasBoardAppCheckTokenSource.IsLiveGuestQaSession)
            return AtlasBoardGuestIdentity.DisplayName;

        if (canonicalHeader != null &&
            canonicalDisplayNameField != null)
        {
            string headerValue =
                canonicalDisplayNameField.GetValue(
                    canonicalHeader) as string;


            if (IsRealProfileName(
                    headerValue))
            {
                return headerValue.Trim();
            }
        }


        if (mainMenuController != null &&
            menuProfileNameField != null)
        {
            string controllerValue =
                menuProfileNameField.GetValue(
                    mainMenuController) as string;


            if (IsRealProfileName(
                    controllerValue))
            {
                return StripLevelSuffix(
                    controllerValue);
            }
        }


        return canonicalDisplayName;
    }


    private void SynchronizeMainMenuProfileSource()
    {
        if (mainMenuController == null ||
            menuProfileNameField == null ||
            string.IsNullOrWhiteSpace(
                canonicalDisplayName))
        {
            return;
        }


        string current =
            menuProfileNameField.GetValue(
                mainMenuController) as string;


        if (!string.Equals(
                current,
                canonicalDisplayName,
                StringComparison.Ordinal))
        {
            menuProfileNameField.SetValue(
                mainMenuController,
                canonicalDisplayName);
        }
    }


    private void SynchronizeSharedProfileCards()
    {
        if (profileNameTexts.Count == 0)
        {
            return;
        }


        string sharedLevelSuffix =
            ResolveSharedLevelSuffix();


        foreach (TMP_Text text
                 in profileNameTexts)
        {
            if (text == null)
            {
                continue;
            }


            string visible =
                text.text != null
                    ? text.text.Trim()
                    : string.Empty;


            string localSuffix =
                ExtractValidLevelSuffix(
                    visible);


            string suffix =
                !string.IsNullOrWhiteSpace(
                    localSuffix)
                    ? localSuffix
                    : sharedLevelSuffix;


            string expected =
                canonicalDisplayName +
                suffix;


            if (!string.Equals(
                    visible,
                    expected,
                    StringComparison.Ordinal))
            {
                text.text =
                    expected;
            }
        }


        string initial =
            GetInitial(
                canonicalDisplayName);


        foreach (TMP_Text text
                 in avatarInitialTexts)
        {
            if (text == null)
            {
                continue;
            }


            string visible =
                text.text != null
                    ? text.text.Trim()
                    : string.Empty;


            if (!string.Equals(
                    visible,
                    initial,
                    StringComparison.Ordinal))
            {
                text.text =
                    initial;
            }
        }
    }


    private string ResolveSharedLevelSuffix()
    {
        foreach (TMP_Text text
                 in profileNameTexts)
        {
            if (text == null)
            {
                continue;
            }


            string suffix =
                ExtractValidLevelSuffix(
                    text.text);


            if (!string.IsNullOrWhiteSpace(
                    suffix))
            {
                return suffix;
            }
        }


        return string.Empty;
    }


    private void SynchronizeLocalLobbySeat()
    {
        if (privateLobbyController == null ||
            lobbySeatNamesField == null ||
            lobbyRoomActiveField == null ||
            string.IsNullOrWhiteSpace(
                canonicalDisplayName))
        {
            return;
        }


        bool roomActive =
            lobbyRoomActiveField.GetValue(
                privateLobbyController) is bool active &&
            active;


        if (!roomActive)
        {
            return;
        }


        TMP_Text[] seatNames =
            lobbySeatNamesField.GetValue(
                privateLobbyController) as
                TMP_Text[];


        if (seatNames == null ||
            seatNames.Length == 0)
        {
            return;
        }


        int localSlot =
            ResolveLocalLobbySlot();


        if (localSlot < 0 ||
            localSlot >=
                seatNames.Length ||
            seatNames[localSlot] == null)
        {
            return;
        }


        string you =
            AtlasBoardL.T(
                "lobby.online.you_short");


        if (string.IsNullOrWhiteSpace(
                you) ||
            string.Equals(
                you,
                "lobby.online.you_short",
                StringComparison.Ordinal))
        {
            you =
                "YOU";
        }


        string expected =
            $"{canonicalDisplayName} ({you})";


        if (!string.Equals(
                seatNames[localSlot].text,
                expected,
                StringComparison.Ordinal))
        {
            seatNames[localSlot].text =
                expected;
        }
    }


    private int ResolveLocalLobbySlot()
    {
        if (privateLobbyController == null)
        {
            return -1;
        }


        AtlasLobbySnapshot snapshot =
            lobbySnapshotField != null
                ? lobbySnapshotField.GetValue(
                    privateLobbyController) as
                    AtlasLobbySnapshot
                : null;


        string localAccountId =
            lobbyLocalAccountIdField != null
                ? lobbyLocalAccountIdField.GetValue(
                    privateLobbyController) as
                    string
                : string.Empty;


        if (snapshot != null &&
            snapshot.Members != null &&
            !string.IsNullOrWhiteSpace(
                localAccountId))
        {
            AtlasLobbyMemberSnapshot local =
                snapshot.Members.FirstOrDefault(
                    member =>
                        member != null &&
                        member.Active &&
                        string.Equals(
                            member.AccountId,
                            localAccountId,
                            StringComparison.Ordinal));


            if (local != null)
            {
                return local.SlotIndex;
            }
        }


        bool localIsHost =
            lobbyLocalIsHostField != null &&
            lobbyLocalIsHostField.GetValue(
                privateLobbyController) is bool host &&
            host;


        return localIsHost
            ? 0
            : -1;
    }


    private static bool IsRealProfileName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }


        string baseName =
            StripLevelSuffix(
                value);


        return !string.Equals(
                   baseName,
                   "PLAYER",
                   StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(
                   baseName,
                   AtlasBoardL.T(
                       "menu.player"),
                   StringComparison.OrdinalIgnoreCase);
    }


    private static string StripLevelSuffix(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }


        string trimmed =
            value.Trim();


        int separator =
            trimmed.LastIndexOf(
                LevelSeparator,
                StringComparison.Ordinal);


        if (separator < 0)
        {
            return trimmed;
        }


        string suffix =
            trimmed.Substring(
                separator +
                LevelSeparator.Length)
                .Trim();


        if (!int.TryParse(
                suffix,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int level) ||
            level <= 0)
        {
            return trimmed;
        }


        return trimmed.Substring(
                0,
                separator)
            .Trim();
    }


    private static string ExtractValidLevelSuffix(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }


        string trimmed =
            value.Trim();


        int separator =
            trimmed.LastIndexOf(
                LevelSeparator,
                StringComparison.Ordinal);


        if (separator < 0)
        {
            return string.Empty;
        }


        string levelText =
            trimmed.Substring(
                separator +
                LevelSeparator.Length)
                .Trim();


        if (!int.TryParse(
                levelText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int level) ||
            level <= 0)
        {
            return string.Empty;
        }


        return
            LevelSeparator +
            level.ToString(
                CultureInfo.InvariantCulture);
    }


    private static string GetInitial(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return "P";
        }


        string element =
            StringInfo.GetNextTextElement(
                value.Trim());


        return string.IsNullOrWhiteSpace(
                   element)
            ? "P"
            : element.ToUpperInvariant();
    }
}


