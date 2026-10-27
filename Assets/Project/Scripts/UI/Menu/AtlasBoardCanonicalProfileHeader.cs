using System;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AtlasBoardCanonicalProfileHeader :
    MonoBehaviour
{
    private const float CheckIntervalSeconds = 0.75f;
    private const float RetryAfterFailureSeconds = 4f;
    private const string FrameOverlayPrefix = "AtlasBoardEquippedProfileFrame_";

    private AtlasBoardAccountService accountService;
    private AtlasBoardMainMenuController mainMenuController;

    private TMP_Text profileNameText;
    private TMP_Text avatarInitialText;

    private string observedAccountId = string.Empty;
    private string appliedDisplayName = string.Empty;
    private string appliedProfileFrameId = string.Empty;

    private bool loadInFlight;
    private float nextCheckAt;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        if (UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardMainMenuController>() == null)
        {
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardCanonicalProfileHeader>() != null)
        {
            return;
        }

        GameObject root =
            new GameObject(
                "AtlasBoard_Phase14C_CanonicalProfileHeader");

        root.AddComponent<
            AtlasBoardCanonicalProfileHeader>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;
    }

    private void OnDisable()
    {
        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;
    }

    private void Update()
    {
        if (Time.unscaledTime <
            nextCheckAt)
        {
            return;
        }

        nextCheckAt =
            Time.unscaledTime +
            CheckIntervalSeconds;

        ResolveReferences();
        SynchronizeProfileFrameSurfaces();

        if (accountService == null)
        {
            return;
        }

        string currentAccountId =
            accountService.IsSignedIn
                ? accountService.CurrentAccountId ??
                  string.Empty
                : string.Empty;

        if (string.IsNullOrWhiteSpace(
                currentAccountId))
        {
            if (!string.IsNullOrWhiteSpace(
                    observedAccountId))
            {
                observedAccountId =
                    string.Empty;

                appliedDisplayName =
                    string.Empty;
                ApplyProfileFrame(string.Empty);

                ApplyFallbackIdentity();
            }

            return;
        }

        bool accountChanged =
            !string.Equals(
                observedAccountId,
                currentAccountId,
                StringComparison.Ordinal);

        bool needsIdentity =
            string.IsNullOrWhiteSpace(
                appliedDisplayName);

        if (accountChanged) ApplyProfileFrame(string.Empty);
        if (!loadInFlight &&
            (accountChanged ||
             needsIdentity))
        {
            LoadCanonicalIdentityAsync(
                currentAccountId);

            return;
        }

        // Phase 15B:
        // Account identity can already be canonical while another Main Menu /
        // localization refresh rewrites only the visible TMP header back to the
        // fallback PLAYER label. Settings/Profile modal still shows the correct
        // controller identity in that case.
        //
        // Treat the canonical display name as an invariant: if the visible
        // header or avatar initial drifts, re-apply the already-loaded identity
        // without another Firestore read.
        if (!loadInFlight &&
            !accountChanged &&
            !string.IsNullOrWhiteSpace(
                appliedDisplayName) &&
            VisibleIdentityHasDrifted(
                appliedDisplayName))
        {
            ApplyCanonicalIdentity(
                appliedDisplayName);
        }
    }

    public void RefreshNow()
    {
        appliedDisplayName =
            string.Empty;

        nextCheckAt = 0f;
    }

    // Career calls this ONLY after both the server-owned EQUIP transaction
    // and the canonical public-profile update have succeeded. It reuses this
    // header's single presentation owner across menu, Store, Career and lobby.
    public void NotifyVerifiedProfileFrameEquipped(string ownerUid, string frameId)
    {
        ResolveReferences();
        if (accountService == null || !accountService.IsSignedIn ||
            !string.Equals(accountService.CurrentAccountId, ownerUid,
                StringComparison.Ordinal)) return;

        ApplyProfileFrame(frameId);
        RefreshNow(); // Reconcile with the saved RTDB profile on the next poll.
    }

    private void ResolveReferences()
    {
        if (accountService == null)
        {
            accountService =
                AtlasBoardAccountService.Instance;

            if (accountService == null)
            {
                accountService =
                    UnityEngine.Object.FindAnyObjectByType<
                        AtlasBoardAccountService>();
            }
        }

        if (mainMenuController == null)
        {
            mainMenuController =
                UnityEngine.Object.FindAnyObjectByType<
                    AtlasBoardMainMenuController>();
        }

        ResolveProfileUi();
    }

    private void ResolveProfileUi()
    {
        if (mainMenuController == null)
        {
            return;
        }

        if (profileNameText != null &&
            avatarInitialText != null)
        {
            return;
        }

        Transform mainMenu =
            FindChildRecursive(
                mainMenuController.transform,
                "MainMenu");

        if (mainMenu == null)
        {
            return;
        }

        Transform profileCard =
            FindChildRecursive(
                mainMenu,
                "ProfileCard");

        if (profileCard == null)
        {
            return;
        }

        Transform profileName =
            FindChildRecursive(
                profileCard,
                "ProfileName");

        if (profileName != null)
        {
            profileNameText =
                profileName.GetComponent<
                    TMP_Text>();
        }

        Transform avatarInitial =
            FindChildRecursive(
                profileCard,
                "AvatarInitial");

        if (avatarInitial != null)
        {
            avatarInitialText =
                avatarInitial.GetComponent<
                    TMP_Text>();
        }
    }

    private bool VisibleIdentityHasDrifted(
        string canonicalDisplayName)
    {
        if (string.IsNullOrWhiteSpace(
                canonicalDisplayName))
        {
            return false;
        }

        ResolveProfileUi();

        string canonical =
            canonicalDisplayName.Trim();

        if (profileNameText != null)
        {
            string visibleName =
                profileNameText.text != null
                    ? profileNameText.text.Trim()
                    : string.Empty;

            if (!IsAcceptedVisibleProfileName(
                    visibleName,
                    canonical))
            {
                return true;
            }
        }

        string expectedInitial =
            GetInitial(
                canonical);

        if (avatarInitialText != null &&
            !string.Equals(
                avatarInitialText.text != null
                    ? avatarInitialText.text.Trim()
                    : string.Empty,
                expectedInitial,
                StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private async void LoadCanonicalIdentityAsync(
        string requestedAccountId)
    {
        if (loadInFlight ||
            accountService == null ||
            string.IsNullOrWhiteSpace(
                requestedAccountId))
        {
            return;
        }

        loadInFlight = true;

        try
        {
            // Online Casual uses a UID-bound Guest name on the HOST and
            // all followers. The account-profile read model can still contain
            // an older device-local Guest alias: never let it overwrite the
            // authenticated lobby identity in the visible profile header.
            if (Application.platform == RuntimePlatform.Android)
            {
                Firebase.Auth.FirebaseUser user = null;
                try { user = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser; }
                catch { /* initialize safely; the normal path retries */ }
                if (user != null && user.IsAnonymous &&
                    string.Equals(user.UserId, requestedAccountId,
                        StringComparison.Ordinal))
                {
                    string alias = AtlasBoardGuestIdentity.NameForAuthenticatedUid(
                        requestedAccountId);
                    observedAccountId = requestedAccountId;
                    appliedDisplayName = alias;
                    ApplyCanonicalIdentity(alias);
                    Debug.Log("ATB_ANDROID_GUEST_PROFILE consistent=true " +
                        "source=authenticated_uid account_linked=false");
                    return;
                }
            }

            AtlasAccountSnapshot snapshot =
                await accountService
                    .LoadCurrentAccountAsync();

            if (this == null)
            {
                return;
            }

            ResolveReferences();

            string liveAccountId =
                accountService != null &&
                accountService.IsSignedIn
                    ? accountService.CurrentAccountId ??
                      string.Empty
                    : string.Empty;

            if (!string.Equals(
                    liveAccountId,
                    requestedAccountId,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (snapshot == null ||
                !string.Equals(
                    snapshot.AccountId,
                    requestedAccountId,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(
                    snapshot.DisplayName))
            {
                nextCheckAt =
                    Time.unscaledTime +
                    RetryAfterFailureSeconds;

                return;
            }

            observedAccountId =
                requestedAccountId;

            appliedDisplayName =
                snapshot.DisplayName.Trim();

            ApplyCanonicalIdentity(
                appliedDisplayName);
            ApplyProfileFrame(snapshot.ProfileFrameId);
        }
        catch (Exception exception)
        {
            string failureMessage =
                exception != null
                    ? exception.Message ?? string.Empty
                    : string.Empty;

            bool expectedOfflineFailure =
                failureMessage.IndexOf(
                    "client is offline",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                failureMessage.IndexOf(
                    "network is unreachable",
                    StringComparison.OrdinalIgnoreCase) >= 0;

            if (!expectedOfflineFailure)
            {
                Debug.LogWarning(
                    "Phase 14C could not load the canonical profile " +
                    "identity yet. Existing header presentation was kept. " +
                    failureMessage,
                    this);
            }

            nextCheckAt =
                Time.unscaledTime +
                (expectedOfflineFailure
                    ? 20f
                    : RetryAfterFailureSeconds);
        }
        finally
        {
            loadInFlight = false;
        }
    }

    private void ApplyProfileFrame(string frameId)
    {
        frameId = AtlasBoardShopProfileFrameArt.IsSupported(frameId)
            ? frameId : string.Empty;
        appliedProfileFrameId = frameId;
        SynchronizeProfileFrameSurfaces();
    }

    private void SynchronizeProfileFrameSurfaces()
    {
        // Static Menu/Lobby cards have a 118x118 "Avatar" Image SIBLING of
        // the 95x95 "AvatarInitial" TMP text. Store/Career put their initial
        // inside the avatar Image instead. Always decorate that actual avatar
        // tile, never its smaller text rectangle or the full ProfileCard.
        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        string expectedName = FrameOverlayPrefix + appliedProfileFrameId;
        foreach (TMP_Text text in texts)
        {
            if (text == null || !text.gameObject.scene.IsValid() ||
                !IsCanonicalProfileAvatar(text)) continue;

            RectTransform avatarTile = ResolveAvatarTile(text);
            if (avatarTile == null) continue;

            // Remove an overlay created by the previous text-anchored build.
            // That obsolete child was intentionally smaller than the blue tile.
            if (avatarTile != text.rectTransform)
                RemoveTextAnchoredLegacyOverlays(text.rectTransform);

            bool hasCurrent = false;
            for (int i = avatarTile.childCount - 1; i >= 0; i--)
            {
                Transform child = avatarTile.GetChild(i);
                if (!child.name.StartsWith(FrameOverlayPrefix,
                        StringComparison.Ordinal)) continue;
                if (!string.IsNullOrEmpty(appliedProfileFrameId) &&
                    child.name == expectedName && !hasCurrent)
                {
                    hasCurrent = true;
                    continue;
                }
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            // Removing a frame restores the original blue Image and its V.
            // An equipped frame instead REPLACES those defaults completely;
            // drawing both produces a distracting nested blue border.
            Image defaultAvatar = avatarTile.GetComponent<Image>();
            if (string.IsNullOrEmpty(appliedProfileFrameId))
            {
                if (defaultAvatar != null) defaultAvatar.enabled = true;
                text.enabled = true;
                continue;
            }

            if (hasCurrent)
            {
                if (defaultAvatar != null) defaultAvatar.enabled = false;
                text.enabled = false;
                continue;
            }

            Rect rect = avatarTile.rect;
            float side = Mathf.Min(Mathf.Abs(rect.width), Mathf.Abs(rect.height));
            // Wait for a valid layout instead of inventing an arbitrary size.
            // NO 88px cap: the scene avatar tile is 118px, the Career tile
            // 96px, and the Store tile 88px. Use each tile's FULL bounds.
            if (side < 24f) continue;

            GameObject art = AtlasBoardShopProfileFrameArt.Create(
                avatarTile, appliedProfileFrameId, side,
                GetInitial(appliedDisplayName));
            if (art == null) continue;

            art.name = expectedName;
            RectTransform artRect = art.GetComponent<RectTransform>();
            if (artRect != null)
            {
                artRect.anchorMin = new Vector2(0.5f, 0.5f);
                artRect.anchorMax = new Vector2(0.5f, 0.5f);
                artRect.pivot = new Vector2(0.5f, 0.5f);
                artRect.anchoredPosition = Vector2.zero;
                artRect.localScale = Vector3.one;
                artRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, side);
                artRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, side);
            }
            // Cosmetic graphics must not block Profile clicks or drag input.
            foreach (Graphic graphic in art.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            art.transform.SetAsLastSibling();

            if (defaultAvatar != null) defaultAvatar.enabled = false;
            text.enabled = false;
        }
    }

    private static RectTransform ResolveAvatarTile(TMP_Text initial)
    {
        RectTransform textRect = initial != null ? initial.rectTransform : null;
        RectTransform parent = textRect != null ? textRect.parent as RectTransform : null;
        if (parent == null) return textRect;

        // Runtime Store and Career: the Text is a child of the avatar Image.
        if ((parent.name == "Avatar" || parent.name == "CareerHudAvatar") &&
            parent.GetComponent<Image>() != null) return parent;

        // Scene Main Menu, Private and Public Lobby: Image and TMP are siblings.
        RectTransform sibling = parent.Find("Avatar") as RectTransform;
        if (sibling != null && sibling.GetComponent<Image>() != null) return sibling;

        return textRect;
    }

    private void RemoveTextAnchoredLegacyOverlays(Transform root)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (!child.name.StartsWith(FrameOverlayPrefix,
                    StringComparison.Ordinal)) continue;
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private static bool IsCanonicalProfileAvatar(TMP_Text initial)
    {
        bool sharedCard = initial.gameObject.name == "AvatarInitial";
        bool careerCard = initial.gameObject.name == "CareerHudInitial";
        if (!sharedCard && !careerCard) return false;

        for (Transform parent = initial.transform.parent; parent != null;
             parent = parent.parent)
        {
            if (parent.name.StartsWith(FrameOverlayPrefix,
                    StringComparison.Ordinal)) return false;
            if (sharedCard && parent.name == "ProfileCard") return true;
            if (careerCard && parent.name == "CareerProfileHeader") return true;
        }
        return false;
    }

    private void ApplyCanonicalIdentity(
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(
                displayName))
        {
            return;
        }

        ResolveProfileUi();

        string trimmed =
            displayName.Trim();

        if (profileNameText != null)
        {
            string visibleName =
                profileNameText.text != null
                    ? profileNameText.text.Trim()
                    : string.Empty;

            if (!IsAcceptedVisibleProfileName(
                    visibleName,
                    trimmed))
            {
                profileNameText.text =
                    trimmed;
            }
        }

        string initial =
            GetInitial(
                trimmed);

        if (avatarInitialText != null &&
            !string.Equals(
                avatarInitialText.text,
                initial,
                StringComparison.Ordinal))
        {
            avatarInitialText.text =
                initial;
        }

        ApplyMainMenuControllerIdentity(
            trimmed);

        Debug.Log(
            $"Phase 14C canonical profile header applied: " +
            $"'{trimmed}'.",
            this);
    }

    private void ApplyFallbackIdentity()
    {
        ResolveProfileUi();

        string fallback = Application.platform == RuntimePlatform.Android &&
            PlayerPrefs.HasKey("AtlasBoard.Android.CasualUidMarker.v1")
                ? AtlasBoardGuestIdentity.DisplayName
                : AtlasBoardL.T("menu.player");

        if (profileNameText != null)
        {
            profileNameText.text =
                fallback;
        }

        if (avatarInitialText != null)
        {
            avatarInitialText.text =
                GetInitial(
                    fallback);
        }

        ApplyMainMenuControllerIdentity(fallback);
    }

    private void ApplyMainMenuControllerIdentity(
        string value)
    {
        if (mainMenuController == null)
        {
            return;
        }

        FieldInfo profileNameField =
            typeof(
                AtlasBoardMainMenuController)
                .GetField(
                    "profileName",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

        if (profileNameField == null)
        {
            Debug.LogWarning(
                "Phase 14C could not find the Main Menu " +
                "profileName field. Header text is still updated, " +
                "but the legacy Profile modal may keep its fallback name.",
                this);

            return;
        }

        string currentValue =
            profileNameField.GetValue(
                mainMenuController) as string;

        if (!string.Equals(
                currentValue,
                value,
                StringComparison.Ordinal))
        {
            profileNameField.SetValue(
                mainMenuController,
                value);
        }
    }

    private void HandleLanguageChanged()
    {
        if (string.IsNullOrWhiteSpace(
                appliedDisplayName))
        {
            ApplyFallbackIdentity();
            return;
        }

        ApplyCanonicalIdentity(
            appliedDisplayName);
    }

    private static bool IsAcceptedVisibleProfileName(
        string visibleName,
        string canonicalDisplayName)
    {
        if (string.IsNullOrWhiteSpace(
                canonicalDisplayName))
        {
            return string.IsNullOrWhiteSpace(
                visibleName);
        }

        string canonical =
            canonicalDisplayName.Trim();

        if (string.Equals(
                visibleName,
                canonical,
                StringComparison.Ordinal))
        {
            return true;
        }

        string levelPrefix =
            canonical +
            " • Lv. ";

        if (!visibleName.StartsWith(
                levelPrefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        string levelText =
            visibleName.Substring(
                levelPrefix.Length)
                .Trim();

        return int.TryParse(
                   levelText,
                   out int level) &&
               level > 0;
    }

    private static string GetInitial(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return "P";
        }

        string trimmed =
            value.Trim();

        string textElement =
            StringInfo.GetNextTextElement(
                trimmed);

        return string.IsNullOrWhiteSpace(
                   textElement)
            ? "P"
            : textElement
                .ToUpperInvariant();
    }

    private static Transform FindChildRecursive(
        Transform root,
        string childName)
    {
        if (root == null)
        {
            return null;
        }

        for (int index = 0;
             index < root.childCount;
             index++)
        {
            Transform child =
                root.GetChild(
                    index);

            if (string.Equals(
                    child.name,
                    childName,
                    StringComparison.Ordinal))
            {
                return child;
            }

            Transform nested =
                FindChildRecursive(
                    child,
                    childName);

            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
