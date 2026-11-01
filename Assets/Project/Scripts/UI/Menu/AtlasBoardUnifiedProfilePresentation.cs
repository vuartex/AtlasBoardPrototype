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


    private string lastLobbySlotResolutionSignature =
        string.Empty;


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

        // ONLINE LOBBY SEAT LABEL OWNERSHIP:
        // AtlasBoardPrivateLobbyUIController is the single authoritative
        // writer for P1..P4 names/statuses. Do not repaint a lobby seat from
        // the local Steam/profile presenter. During rematch host migration a
        // transient snapshot can temporarily have no exact local UID match;
        // the old host fallback then assumed slot 0 and overwrote the verified
        // Guest P1 label with the promoted host name (Vuartex) only in Editor.
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


        bool localIsHost =
            lobbyLocalIsHostField != null &&
            lobbyLocalIsHostField.GetValue(
                privateLobbyController) is bool host &&
            host;


        if (snapshot != null &&
            snapshot.Members != null &&
            !string.IsNullOrWhiteSpace(
                localAccountId))
        {
            List<AtlasLobbyMemberSnapshot> matches =
                snapshot.Members
                    .Where(member =>
                        member != null &&
                        member.Active &&
                        member.SlotIndex >= 0 &&
                        string.Equals(
                            member.AccountId,
                            localAccountId,
                            StringComparison.Ordinal))
                    .OrderBy(member => member.SlotIndex)
                    .ToList();


            if (matches.Count == 1)
            {
                lastLobbySlotResolutionSignature =
                    string.Empty;
                return matches[0].SlotIndex;
            }


            if (matches.Count > 1)
            {
                // During host migration/rematch a transient lobby projection
                // can contain the NEW host UID in both the real host slot and
                // the outgoing host's old reconnecting slot. Never use
                // FirstOrDefault here: it makes the global profile presenter
                // overwrite the old player's row with the host's display name.
                // Prefer the single authoritative HostLocal/IsHost row when
                // this client owns the room.
                AtlasLobbyMemberSnapshot authoritativeHost =
                    matches.FirstOrDefault(member =>
                        string.Equals(
                            snapshot.HostAccountId,
                            localAccountId,
                            StringComparison.Ordinal) &&
                        (member.SeatMode ==
                             AtlasLobbySeatMode.HostLocal ||
                         member.IsHost));


                if (authoritativeHost != null &&
                    (localIsHost ||
                     string.Equals(
                         snapshot.HostAccountId,
                         localAccountId,
                         StringComparison.Ordinal)))
                {
                    ReportAmbiguousLobbySlot(
                        snapshot,
                        localAccountId,
                        matches,
                        authoritativeHost.SlotIndex,
                        "authoritative_host");
                    return authoritativeHost.SlotIndex;
                }


                // A non-host client should have exactly one RemoteHuman row.
                // If a malformed snapshot duplicates its UID, only accept a
                // unique RemoteHuman candidate; otherwise fail closed and let
                // AtlasBoardPrivateLobbyUIController render the roster rather
                // than clobbering an unrelated player's label.
                List<AtlasLobbyMemberSnapshot> remoteCandidates =
                    matches
                        .Where(member =>
                            member.SeatMode ==
                                AtlasLobbySeatMode.RemoteHuman &&
                            !member.IsHost)
                        .ToList();


                if (remoteCandidates.Count == 1)
                {
                    ReportAmbiguousLobbySlot(
                        snapshot,
                        localAccountId,
                        matches,
                        remoteCandidates[0].SlotIndex,
                        "unique_remote_human");
                    return remoteCandidates[0].SlotIndex;
                }


                ReportAmbiguousLobbySlot(
                    snapshot,
                    localAccountId,
                    matches,
                    -1,
                    "fail_closed");
                return -1;
            }
        }


        // Never infer slot 0 from local host status. Host migration can move
        // the authoritative host to P2/P3/P4, and during a rematch poll the
        // local UID can be momentarily absent from Members. In that state the
        // only safe action for a presentation helper is to write nothing and
        // let AtlasBoardPrivateLobbyUIController keep the authoritative roster.
        if (localIsHost)
        {
            ReportAmbiguousLobbySlot(
                snapshot,
                localAccountId,
                new List<AtlasLobbyMemberSnapshot>(),
                -1,
                string.IsNullOrWhiteSpace(localAccountId)
                    ? "missing_local_uid_fail_closed"
                    : "no_local_uid_match_fail_closed");
        }

        return -1;
    }


    private void ReportAmbiguousLobbySlot(
        AtlasLobbySnapshot snapshot,
        string localAccountId,
        List<AtlasLobbyMemberSnapshot> matches,
        int chosenSlot,
        string reason)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string slots =
            matches != null
                ? string.Join(
                    ",",
                    matches.Select(member =>
                        member != null
                            ? member.SlotIndex.ToString()
                            : "null"))
                : string.Empty;


        string signature =
            (snapshot != null
                ? snapshot.LobbyId ?? string.Empty
                : string.Empty) +
            ":" + localAccountId +
            ":" + slots +
            ":" + chosenSlot +
            ":" + reason;


        if (string.Equals(
                signature,
                lastLobbySlotResolutionSignature,
                StringComparison.Ordinal))
        {
            return;
        }


        lastLobbySlotResolutionSignature =
            signature;


        Debug.LogWarning(
            "ATB_PROFILE_LOBBY_SLOT ambiguous=true " +
            "local_uid=" + localAccountId +
            " matches=" + slots +
            " chosen_slot=" + chosenSlot +
            " reason=" + reason,
            this);
#endif
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


