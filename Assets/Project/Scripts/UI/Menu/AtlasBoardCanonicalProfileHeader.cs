using System;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AtlasBoardCanonicalProfileHeader :
    MonoBehaviour
{
    private const float CheckIntervalSeconds = 0.75f;
    private const float RetryAfterFailureSeconds = 4f;

    private AtlasBoardAccountService accountService;
    private AtlasBoardMainMenuController mainMenuController;

    private TMP_Text profileNameText;
    private TMP_Text avatarInitialText;

    private string observedAccountId = string.Empty;
    private string appliedDisplayName = string.Empty;

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

        string fallback =
            AtlasBoardL.T(
                "menu.player");

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

        ApplyMainMenuControllerIdentity(
            "PLAYER");
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
