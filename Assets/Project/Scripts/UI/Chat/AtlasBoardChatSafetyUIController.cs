using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AtlasBoardChatSafetyUIController : MonoBehaviour
{
    private const string TextScalePref =
        "AtlasBoard.Chat.TextScale";
    private const float RefreshSeconds = 3f;

    private AtlasBoardChatRuntimeBridge chatBridge;
    private AtlasBoardChatModerationBridge moderationBridge;
    private AtlasBoardLobbyRuntimeBridge lobbyBridge;
    private AtlasBoardChatUIController mainChat;

    private GameObject launcherRoot;
    private GameObject panelRoot;
    private RectTransform playerContent;
    private RectTransform messageContent;
    private TMP_Text titleText;
    private TMP_Text descriptionText;
    private TMP_Text textSizeLabel;
    private TMP_Text reasonLabel;
    private TMP_Text playersLabel;
    private TMP_Text messagesLabel;
    private TMP_Text reasonButtonText;
    private TMP_Text statusText;
    private TMP_Text scaleText;

    private AtlasBoardChatScope currentScope;
    private AtlasBoardChatSafetyState safetyState;
    private readonly HashSet<string> mutedAccountIds =
        new HashSet<string>();
    private readonly HashSet<string> blockedAccountIds =
        new HashSet<string>();
    private readonly Dictionary<EntityId, float> baseFontSizes =
        new Dictionary<EntityId, float>();
    private readonly List<AtlasBoardChatMessage> cachedRecentMessages =
        new List<AtlasBoardChatMessage>();

    private float textScale = 1f;
    private float nextRefreshAt;
    private float nextScaleApplyAt;
    private bool refreshInFlight;
    private int reasonIndex;
    private string lastLanguageCode = string.Empty;

    private static readonly string[] ReasonCodes =
    {
        "abuse",
        "harassment",
        "spam",
        "profanity",
        "cheating",
        "other"
    };

    private void Awake()
    {
        chatBridge = GetComponent<AtlasBoardChatRuntimeBridge>();
        moderationBridge =
            GetComponent<AtlasBoardChatModerationBridge>();

        if (moderationBridge == null)
        {
            moderationBridge =
                gameObject.AddComponent<
                    AtlasBoardChatModerationBridge>();
        }

        ResolveLobbyBridge();
        ResolveMainChat();
        textScale = Mathf.Clamp(
            PlayerPrefs.GetFloat(TextScalePref, 1f),
            0.85f,
            1.30f);
        BuildUI();
        lastLanguageCode = CurrentLanguageCode();
        RefreshLocalizedUi();
        SetAvailable(false);
    }

    private void Update()
    {
        if (chatBridge == null)
        {
            return;
        }

        ResolveMainChat();
        AttachLauncherToMainChat();

        string language = CurrentLanguageCode();
        if (!string.Equals(
                language,
                lastLanguageCode,
                StringComparison.Ordinal))
        {
            lastLanguageCode = language;
            RefreshLocalizedUi();
            nextRefreshAt = 0f;
        }

        if (panelRoot != null &&
            panelRoot.activeSelf &&
            (mainChat == null || !mainChat.IsPanelOpen))
        {
            panelRoot.SetActive(false);
        }

        if (!chatBridge.TryResolveActiveScope(out AtlasBoardChatScope scope))
        {
            currentScope = null;
            SetAvailable(false);
            return;
        }

        SetAvailable(true);
        UpdateLayout();

        if (currentScope == null ||
            !string.Equals(
                currentScope.StableKey,
                scope.StableKey,
                StringComparison.Ordinal))
        {
            currentScope = scope;
            nextRefreshAt = 0f;
            UpdateLayout();
        }

        if (panelRoot != null &&
            panelRoot.activeSelf &&
            !refreshInFlight &&
            Time.unscaledTime >= nextRefreshAt)
        {
            nextRefreshAt = Time.unscaledTime + RefreshSeconds;
            RefreshAsync();
        }

        if (Time.unscaledTime >= nextScaleApplyAt)
        {
            nextScaleApplyAt = Time.unscaledTime + 0.5f;
            ApplyChatTextScale();
        }
    }

    private void TogglePanel()
    {
        if (panelRoot == null ||
            mainChat == null ||
            !mainChat.IsPanelOpen)
        {
            return;
        }

        bool next = !panelRoot.activeSelf;
        panelRoot.SetActive(next);
        if (next)
        {
            nextRefreshAt = 0f;
            RefreshAsync();
        }
    }

    private void ClosePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private async void RefreshAsync()
    {
        if (refreshInFlight || currentScope == null)
        {
            return;
        }

        refreshInFlight = true;
        string expectedKey = currentScope.StableKey;
        SetStatus(Loc("loading"));

        try
        {
            AtlasBoardChatSafetyState nextSafety =
                await moderationBridge.GetSafetyStateAsync(currentScope);

            if (!ScopeStillMatches(expectedKey))
            {
                return;
            }

            if (!nextSafety.Success)
            {
                SetStatus(LocalizeError(nextSafety.ErrorKey));
                return;
            }

            safetyState = nextSafety;
            mutedAccountIds.Clear();
            blockedAccountIds.Clear();
            if (nextSafety.MutedAccountIds != null)
            {
                foreach (string accountId in nextSafety.MutedAccountIds)
                {
                    if (!string.IsNullOrWhiteSpace(accountId))
                    {
                        mutedAccountIds.Add(accountId);
                    }
                }
            }

            if (nextSafety.BlockedAccountIds != null)
            {
                foreach (string accountId in nextSafety.BlockedAccountIds)
                {
                    if (!string.IsNullOrWhiteSpace(accountId))
                    {
                        blockedAccountIds.Add(accountId);
                    }
                }
            }

            RebuildPlayers();

            AtlasBoardChatListResult recent =
                await chatBridge.ListMessagesAsync(
                    currentScope,
                    0L);

            if (!ScopeStillMatches(expectedKey))
            {
                return;
            }

            if (recent.Success)
            {
                cachedRecentMessages.Clear();
                if (recent.Messages != null)
                {
                    cachedRecentMessages.AddRange(recent.Messages);
                }
                RebuildRecentMessages(cachedRecentMessages);
            }

            SetStatus(BuildSafetySummary());
        }
        finally
        {
            refreshInFlight = false;
        }
    }

    private bool ScopeStillMatches(string expectedKey)
    {
        return currentScope != null &&
               string.Equals(
                   currentScope.StableKey,
                   expectedKey,
                   StringComparison.Ordinal);
    }

    private void RebuildPlayers()
    {
        ClearChildren(playerContent);
        ResolveLobbyBridge();

        AtlasLobbySnapshot snapshot =
            lobbyBridge != null
                ? lobbyBridge.CurrentSnapshot
                : null;

        if (snapshot == null || snapshot.Members == null)
        {
            AddInfoRow(playerContent, Loc("no_players"));
            return;
        }

        string localId = moderationBridge.LocalAccountId;
        List<AtlasLobbyMemberSnapshot> members =
            snapshot.Members
                .Where(item =>
                    item != null &&
                    item.Active &&
                    item.IsHumanSeat)
                .OrderBy(item => item.SlotIndex)
                .ToList();

        int added = 0;
        foreach (AtlasLobbyMemberSnapshot member in members)
        {
            string accountId =
                !string.IsNullOrWhiteSpace(member.AccountId)
                    ? member.AccountId
                    : member.LocalOwnerAccountId;

            if (string.IsNullOrWhiteSpace(accountId) ||
                string.Equals(
                    accountId,
                    localId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            AddPlayerRow(
                accountId,
                string.IsNullOrWhiteSpace(member.DisplayName)
                    ? $"Player {member.SlotIndex + 1}"
                    : member.DisplayName);
            added++;
        }

        if (added == 0)
        {
            AddInfoRow(playerContent, Loc("no_players_report_hint"));
        }
    }

    private void AddPlayerRow(
        string accountId,
        string displayName)
    {
        GameObject row = CreateRow(playerContent, 68f);

        TMP_Text name = CreateText(
            row.transform,
            "PlayerName",
            12f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft,
            Color.white);
        SetAnchored(
            name.rectTransform,
            new Vector2(8f, -30f),
            new Vector2(-8f, -4f));
        name.text = displayName;

        bool muted = mutedAccountIds.Contains(accountId);
        Button muteButton = CreateSmallButton(
            row.transform,
            muted ? Loc("unmute") : Loc("mute"),
            new Vector2(8f, -62f),
            new Vector2(-296f, -34f));
        muteButton.onClick.AddListener(
            () => SetMutedAsync(accountId, !muted));

        bool blocked = blockedAccountIds.Contains(accountId);
        Button blockButton = CreateSmallButton(
            row.transform,
            blocked ? Loc("unblock") : Loc("block"),
            new Vector2(108f, -62f),
            new Vector2(-196f, -34f));
        blockButton.onClick.AddListener(
            () => SetBlockedAsync(accountId, !blocked));

        Button reportButton = CreateSmallButton(
            row.transform,
            Loc("report"),
            new Vector2(208f, -62f),
            new Vector2(-96f, -34f));
        reportButton.onClick.AddListener(
            () => ReportPlayerAsync(accountId));

        if (safetyState != null && safetyState.CanModerate)
        {
            Button modButton = CreateSmallButton(
                row.transform,
                "M10",
                new Vector2(308f, -62f),
                new Vector2(-8f, -34f));
            modButton.onClick.AddListener(
                () => ModeratorMuteAsync(accountId));
        }
    }

    private void RebuildRecentMessages(
        List<AtlasBoardChatMessage> messages)
    {
        ClearChildren(messageContent);

        if (messages == null || messages.Count == 0)
        {
            AddInfoRow(messageContent, Loc("no_messages"));
            return;
        }

        List<AtlasBoardChatMessage> recent =
            messages
                .Where(item =>
                    item != null &&
                    !string.IsNullOrWhiteSpace(item.messageId))
                .ToList();

        int startIndex = Mathf.Max(0, recent.Count - 8);
        recent = recent
            .Skip(startIndex)
            .Reverse()
            .ToList();

        int added = 0;
        foreach (AtlasBoardChatMessage message in recent)
        {
            AddMessageReportRow(message);
            added++;
        }

        if (added == 0)
        {
            AddInfoRow(messageContent, Loc("no_messages"));
        }
    }

    private void AddMessageReportRow(AtlasBoardChatMessage message)
    {
        bool ownMessage =
            string.Equals(
                message.accountId,
                moderationBridge.LocalAccountId,
                StringComparison.Ordinal);

        GameObject row = CreateRow(messageContent, 72f);

        TMP_Text text = CreateText(
            row.transform,
            "MessagePreview",
            11f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft,
            Color.white);
        SetAnchored(
            text.rectTransform,
            new Vector2(8f, -38f),
            new Vector2(-8f, -5f));
        string body = message.body ?? string.Empty;
        if (body.Length > 76)
        {
            body = body.Substring(0, 76) + "…";
        }
        text.text = $"<b>{EscapeTmp(message.displayName)}</b>: " +
                    EscapeTmp(body);
        text.textWrappingMode = TextWrappingModes.Normal;

        if (ownMessage)
        {
            Button ownReportButton = CreateSmallButton(
                row.transform,
                Loc("report_message"),
                new Vector2(8f, -66f),
                new Vector2(-220f, -42f));
            ownReportButton.interactable = false;

            TMP_Text own = CreateText(
                row.transform,
                "OwnMessageLabel",
                10f,
                FontStyles.Italic,
                TextAlignmentOptions.MidlineRight,
                new Color(0.65f, 0.70f, 0.78f, 1f));
            SetAnchored(
                own.rectTransform,
                new Vector2(216f, -66f),
                new Vector2(-8f, -42f));
            own.text = Loc("your_message");
            return;
        }

        Button reportButton = CreateSmallButton(
            row.transform,
            Loc("report_message"),
            new Vector2(8f, -66f),
            new Vector2(-220f, -42f));
        reportButton.onClick.AddListener(
            () => ReportMessageAsync(message.messageId));

        if (safetyState != null && safetyState.CanModerate)
        {
            Button removeButton = CreateSmallButton(
                row.transform,
                Loc("remove"),
                new Vector2(216f, -66f),
                new Vector2(-8f, -42f));
            removeButton.onClick.AddListener(
                () => ModeratorRemoveMessageAsync(message.messageId));
        }
    }

    private async void SetMutedAsync(
        string targetAccountId,
        bool muted)
    {
        if (currentScope == null)
        {
            return;
        }

        SetStatus(Loc("working"));
        AtlasBoardChatModerationResult result =
            await moderationBridge.SetMutedAsync(
                currentScope,
                targetAccountId,
                muted);

        if (!result.Success)
        {
            SetStatus(LocalizeError(result.ErrorKey));
            return;
        }

        SetStatus(muted ? Loc("muted_done") : Loc("unmuted_done"));
        ForceMainChatReload();
        nextRefreshAt = 0f;
        RefreshAsync();
    }

    private async void SetBlockedAsync(
        string targetAccountId,
        bool blocked)
    {
        if (currentScope == null)
        {
            return;
        }

        SetStatus(Loc("working"));
        AtlasBoardChatModerationResult result =
            await moderationBridge.SetBlockedAsync(
                currentScope,
                targetAccountId,
                blocked);

        if (!result.Success)
        {
            SetStatus(LocalizeError(result.ErrorKey));
            return;
        }

        SetStatus(blocked ? Loc("blocked_done") : Loc("unblocked_done"));
        ForceMainChatReload();
        nextRefreshAt = 0f;
        RefreshAsync();
    }

    private async void ReportPlayerAsync(string targetAccountId)
    {
        if (currentScope == null)
        {
            return;
        }

        SetStatus(Loc("working"));
        AtlasBoardChatModerationResult result =
            await moderationBridge.ReportPlayerAsync(
                currentScope,
                targetAccountId,
                CurrentReason());
        SetStatus(
            result.Success
                ? BuildReportSuccess(result.ReportId)
                : LocalizeError(result.ErrorKey));
    }

    private async void ReportMessageAsync(string messageId)
    {
        if (currentScope == null)
        {
            return;
        }

        SetStatus(Loc("working"));
        AtlasBoardChatModerationResult result =
            await moderationBridge.ReportMessageAsync(
                currentScope,
                messageId,
                CurrentReason());
        SetStatus(
            result.Success
                ? BuildReportSuccess(result.ReportId)
                : LocalizeError(result.ErrorKey));
    }

    private async void ModeratorMuteAsync(string targetAccountId)
    {
        if (currentScope == null)
        {
            return;
        }

        SetStatus(Loc("working"));
        AtlasBoardChatModerationResult result =
            await moderationBridge.ModeratorSetSanctionAsync(
                currentScope,
                targetAccountId,
                "mute",
                10);
        SetStatus(
            result.Success
                ? "Moderator mute: 10m"
                : LocalizeError(result.ErrorKey));
    }

    private async void ModeratorRemoveMessageAsync(string messageId)
    {
        if (currentScope == null)
        {
            return;
        }

        SetStatus(Loc("working"));
        AtlasBoardChatModerationResult result =
            await moderationBridge.ModeratorRemoveMessageAsync(
                currentScope,
                messageId);
        SetStatus(
            result.Success
                ? Loc("removed")
                : LocalizeError(result.ErrorKey));

        if (result.Success)
        {
            ForceMainChatReload();
            nextRefreshAt = 0f;
            RefreshAsync();
        }
    }

    private string CurrentReason()
    {
        int index = Mathf.Clamp(
            reasonIndex,
            0,
            ReasonCodes.Length - 1);
        return ReasonCodes[index];
    }

    private void CycleReason()
    {
        reasonIndex = (reasonIndex + 1) % ReasonCodes.Length;
        UpdateReasonLabel();
    }

    private void UpdateReasonLabel()
    {
        if (reasonButtonText == null)
        {
            return;
        }

        reasonButtonText.text =
            Loc("reason_" + CurrentReason()).ToUpperInvariant();
    }

    private void ChangeScale(float delta)
    {
        textScale = Mathf.Clamp(textScale + delta, 0.85f, 1.30f);
        PlayerPrefs.SetFloat(TextScalePref, textScale);
        PlayerPrefs.Save();
        UpdateScaleLabel();
        ApplyChatTextScale();
    }

    private void ApplyChatTextScale()
    {
        AtlasBoardChatUIController mainChat =
            FindAnyObjectByType<AtlasBoardChatUIController>();
        if (mainChat == null)
        {
            return;
        }

        TMP_Text[] texts =
            mainChat.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text == null || !IsScalableChatText(text.transform))
            {
                continue;
            }

            EntityId id = text.GetEntityId();
            if (!baseFontSizes.TryGetValue(id, out float baseline))
            {
                baseline = text.fontSize;
                baseFontSizes[id] = baseline;
            }

            text.fontSize = baseline * textScale;
        }
    }

    private static bool IsScalableChatText(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        string objectName = target.gameObject.name;
        if (
            objectName != "Message" &&
            objectName != "Time" &&
            objectName != "Text" &&
            objectName != "Placeholder"
        )
        {
            return false;
        }

        Transform current = target;
        while (current != null)
        {
            if (current.gameObject.name == "Canvas_AtlasChat")
            {
                return true;
            }
            current = current.parent;
        }

        return false;
    }

    private void ForceMainChatReload()
    {
        ResolveMainChat();
        mainChat?.ForceReloadCurrentScope();
    }

    private void BuildUI()
    {
        ResolveMainChat();

        GameObject canvasObject = new GameObject(
            "Canvas_AtlasChatSafety",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 565;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Transform launcherParent =
            mainChat != null && mainChat.ChatPanelTransform != null
                ? mainChat.ChatPanelTransform
                : canvasObject.transform;

        launcherRoot = CreateButton(
            launcherParent,
            "SafetyLauncher",
            "!",
            out Button launcher,
            new Color(0.48f, 0.20f, 0.08f, 1f));
        RectTransform launcherRect =
            launcherRoot.GetComponent<RectTransform>();
        launcherRect.anchorMin = new Vector2(1f, 1f);
        launcherRect.anchorMax = new Vector2(1f, 1f);
        launcherRect.pivot = new Vector2(1f, 1f);
        launcherRect.anchoredPosition = new Vector2(-54f, -9f);
        launcherRect.sizeDelta = new Vector2(36f, 34f);
        launcher.onClick.AddListener(TogglePanel);

        panelRoot = new GameObject(
            "ChatSafetyPanel",
            typeof(RectTransform),
            typeof(Image),
            typeof(Outline));
        panelRoot.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect =
            panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-18f, 72f);
        panelRect.sizeDelta = new Vector2(430f, 465f);
        panelRoot.GetComponent<Image>().color =
            new Color(0.035f, 0.045f, 0.060f, 0.995f);
        Outline outline = panelRoot.GetComponent<Outline>();
        outline.effectColor = new Color(0.65f, 0.32f, 0.15f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        titleText = CreateText(
            panelRoot.transform,
            "Title",
            17f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft,
            Color.white);
        SetAnchored(
            titleText.rectTransform,
            new Vector2(14f, -40f),
            new Vector2(-54f, -7f));

        GameObject closeRoot = CreateButton(
            panelRoot.transform,
            "Close",
            "×",
            out Button close,
            new Color(0.15f, 0.17f, 0.22f, 1f));
        RectTransform closeRect =
            closeRoot.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-9f, -7f);
        closeRect.sizeDelta = new Vector2(36f, 32f);
        close.onClick.AddListener(ClosePanel);

        descriptionText = CreateText(
            panelRoot.transform,
            "Description",
            10f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft,
            new Color(0.68f, 0.72f, 0.78f, 1f));
        SetAnchored(
            descriptionText.rectTransform,
            new Vector2(14f, -62f),
            new Vector2(-14f, -40f));

        BuildScaleRow(panelRoot.transform);
        BuildReasonRow(panelRoot.transform);

        playersLabel = CreateText(
            panelRoot.transform,
            "PlayersLabel",
            11f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft,
            new Color(0.82f, 0.86f, 0.92f, 1f));
        SetAnchored(
            playersLabel.rectTransform,
            new Vector2(14f, -157f),
            new Vector2(-14f, -137f));

        playerContent = BuildList(
            panelRoot.transform,
            "PlayerList",
            new Vector2(14f, -260f),
            new Vector2(-14f, -160f));

        messagesLabel = CreateText(
            panelRoot.transform,
            "MessagesLabel",
            11f,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft,
            new Color(0.82f, 0.86f, 0.92f, 1f));
        SetAnchored(
            messagesLabel.rectTransform,
            new Vector2(14f, -283f),
            new Vector2(-14f, -263f));

        messageContent = BuildList(
            panelRoot.transform,
            "MessageList",
            new Vector2(14f, -414f),
            new Vector2(-14f, -286f));

        statusText = CreateText(
            panelRoot.transform,
            "Status",
            10f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft,
            new Color(0.90f, 0.74f, 0.42f, 1f));
        RectTransform statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.offsetMin = new Vector2(14f, 8f);
        statusRect.offsetMax = new Vector2(-14f, 42f);

        panelRoot.SetActive(false);
        UpdateScaleLabel();
        UpdateLayout();
    }

    private void BuildScaleRow(Transform parent)
    {
        textSizeLabel = CreateText(
            parent,
            "TextSizeLabel",
            12f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft,
            Color.white);
        SetAnchored(
            textSizeLabel.rectTransform,
            new Vector2(14f, -94f),
            new Vector2(-288f, -66f));

        GameObject minusRoot = CreateButton(
            parent,
            "TextMinus",
            "A−",
            out Button minus,
            new Color(0.12f, 0.15f, 0.20f, 1f));
        SetAnchored(
            minusRoot.GetComponent<RectTransform>(),
            new Vector2(176f, -94f),
            new Vector2(-214f, -66f));
        minus.onClick.AddListener(() => ChangeScale(-0.15f));

        scaleText = CreateText(
            parent,
            "TextScale",
            12f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white);
        SetAnchored(
            scaleText.rectTransform,
            new Vector2(224f, -94f),
            new Vector2(-136f, -66f));

        GameObject plusRoot = CreateButton(
            parent,
            "TextPlus",
            "A+",
            out Button plus,
            new Color(0.12f, 0.15f, 0.20f, 1f));
        SetAnchored(
            plusRoot.GetComponent<RectTransform>(),
            new Vector2(302f, -94f),
            new Vector2(-14f, -66f));
        plus.onClick.AddListener(() => ChangeScale(0.15f));
    }

    private void BuildReasonRow(Transform parent)
    {
        reasonLabel = CreateText(
            parent,
            "ReasonLabel",
            12f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft,
            Color.white);
        SetAnchored(
            reasonLabel.rectTransform,
            new Vector2(14f, -130f),
            new Vector2(-288f, -100f));

        GameObject reasonRoot = CreateButton(
            parent,
            "ReasonButton",
            "ABUSE",
            out Button reasonButton,
            new Color(0.10f, 0.12f, 0.16f, 1f));
        SetAnchored(
            reasonRoot.GetComponent<RectTransform>(),
            new Vector2(176f, -130f),
            new Vector2(-14f, -100f));
        reasonButtonText = reasonRoot
            .transform
            .Find("Label")
            ?.GetComponent<TMP_Text>();
        reasonButton.onClick.AddListener(CycleReason);
        UpdateReasonLabel();
    }

    private RectTransform BuildList(
        Transform parent,
        string name,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.offsetMin = offsetMin;
        rootRect.offsetMax = offsetMax;
        root.GetComponent<Image>().color =
            new Color(0.02f, 0.025f, 0.035f, 0.55f);

        GameObject viewport = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));
        viewport.transform.SetParent(root.transform, false);
        RectTransform viewportRect =
            viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewport.GetComponent<Image>().color = Color.clear;

        GameObject content = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect =
            content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(6f, 0f);
        contentRect.offsetMax = new Vector2(-6f, 0f);

        VerticalLayoutGroup layout =
            content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(0, 0, 5, 5);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter =
            content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect scroll = root.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 22f;

        return contentRect;
    }

    private static GameObject CreateRow(
        Transform parent,
        float preferredHeight)
    {
        GameObject row = new GameObject(
            "SafetyRow",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color =
            new Color(0.08f, 0.095f, 0.12f, 0.95f);
        LayoutElement layout = row.GetComponent<LayoutElement>();
        layout.minHeight = preferredHeight;
        layout.preferredHeight = preferredHeight;
        return row;
    }

    private static Button CreateSmallButton(
        Transform parent,
        string label,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject root = CreateButton(
            parent,
            "Action",
            label,
            out Button button,
            new Color(0.14f, 0.18f, 0.24f, 1f));
        SetAnchored(
            root.GetComponent<RectTransform>(),
            offsetMin,
            offsetMax);
        return button;
    }

    private static GameObject CreateButton(
        Transform parent,
        string name,
        string label,
        out Button button,
        Color color)
    {
        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        root.transform.SetParent(parent, false);
        root.GetComponent<Image>().color = color;
        button = root.GetComponent<Button>();

        TMP_Text text = CreateText(
            root.transform,
            "Label",
            12f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white);
        Stretch(text.rectTransform);
        text.text = label ?? string.Empty;
        text.enableAutoSizing = true;
        text.fontSizeMin = 7f;
        text.fontSizeMax = 12f;
        return root;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static void AddInfoRow(
        Transform parent,
        string value)
    {
        GameObject row = CreateRow(parent, 38f);
        TMP_Text text = CreateText(
            row.transform,
            "Info",
            11f,
            FontStyles.Italic,
            TextAlignmentOptions.MidlineLeft,
            new Color(0.65f, 0.69f, 0.75f, 1f));
        Stretch(text.rectTransform, 8f);
        text.text = value;
    }

    private static void SetAnchored(
        RectTransform rect,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void Stretch(
        RectTransform rect,
        float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            Destroy(parent.GetChild(index).gameObject);
        }
    }

    private void ResolveLobbyBridge()
    {
        if (lobbyBridge != null)
        {
            return;
        }

        AtlasBoardLobbyRuntimeBridge[] all =
            Resources.FindObjectsOfTypeAll<
                AtlasBoardLobbyRuntimeBridge>();

        foreach (AtlasBoardLobbyRuntimeBridge item in all)
        {
            if (item != null && item.gameObject.scene.IsValid())
            {
                lobbyBridge = item;
                break;
            }
        }
    }

    private void ResolveMainChat()
    {
        if (mainChat != null)
        {
            return;
        }

        mainChat = GetComponent<AtlasBoardChatUIController>();
        if (mainChat == null)
        {
            mainChat = FindAnyObjectByType<AtlasBoardChatUIController>();
        }
    }

    private void AttachLauncherToMainChat()
    {
        if (launcherRoot == null ||
            mainChat == null ||
            mainChat.ChatPanelTransform == null)
        {
            return;
        }

        Transform targetParent = mainChat.ChatPanelTransform;
        if (launcherRoot.transform.parent != targetParent)
        {
            launcherRoot.transform.SetParent(targetParent, false);
        }

        RectTransform launcherRect =
            launcherRoot.GetComponent<RectTransform>();
        launcherRect.anchorMin = new Vector2(1f, 1f);
        launcherRect.anchorMax = new Vector2(1f, 1f);
        launcherRect.pivot = new Vector2(1f, 1f);
        launcherRect.anchoredPosition = new Vector2(-54f, -9f);
        launcherRect.sizeDelta = new Vector2(36f, 34f);
    }

    private static string CurrentLanguageCode()
    {
        return AtlasBoardLocalizationManager.Instance != null
            ? (AtlasBoardLocalizationManager.Instance.CurrentLanguageCode ?? "en")
                .ToLowerInvariant()
            : "en";
    }

    private void RefreshLocalizedUi()
    {
        if (titleText != null) titleText.text = Loc("title");
        if (descriptionText != null) descriptionText.text = Loc("description");
        if (textSizeLabel != null) textSizeLabel.text = Loc("text_size");
        if (reasonLabel != null) reasonLabel.text = Loc("reason");
        if (playersLabel != null) playersLabel.text = Loc("players");
        if (messagesLabel != null) messagesLabel.text = Loc("recent_messages");
        UpdateReasonLabel();
        UpdateScaleLabel();

        if (panelRoot != null && panelRoot.activeSelf)
        {
            RebuildPlayers();
            RebuildRecentMessages(cachedRecentMessages);
        }
    }

    private static string BuildReportSuccess(string reportId)
    {
        if (string.IsNullOrWhiteSpace(reportId))
        {
            return Loc("reported");
        }

        string shortId = reportId.Length > 8
            ? reportId.Substring(0, 8)
            : reportId;
        return $"{Loc("reported")} ID: {shortId}";
    }

    private void SetAvailable(bool available)
    {
        if (launcherRoot != null)
        {
            launcherRoot.SetActive(available);
        }

        if (!available && panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void UpdateLayout()
    {
        ResolveMainChat();
        if (panelRoot == null || mainChat == null)
        {
            return;
        }

        RectTransform source = mainChat.ChatPanelRect;
        RectTransform panel = panelRoot.GetComponent<RectTransform>();
        if (source != null && panel != null)
        {
            panel.anchorMin = source.anchorMin;
            panel.anchorMax = source.anchorMax;
            panel.pivot = source.pivot;
            panel.anchoredPosition = source.anchoredPosition;
            panel.sizeDelta = source.sizeDelta;
        }
    }

    private void UpdateScaleLabel()
    {
        if (scaleText != null)
        {
            scaleText.text = $"{Mathf.RoundToInt(textScale * 100f)}%";
        }
    }

    private string BuildSafetySummary()
    {
        if (safetyState == null)
        {
            return string.Empty;
        }

        return $"{Loc("filter")}: " +
               $"{safetyState.StaticBlockedTermCount}+" +
               $"{safetyState.ExtraBlockedTermCount} | " +
               $"TTL {safetyState.MessageRetentionDays}d";
    }

    private void SetStatus(string value)
    {
        if (statusText != null)
        {
            statusText.text = value ?? string.Empty;
        }
    }

    private static string EscapeTmp(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static string LocalizeError(string key)
    {
        switch (key)
        {
            case "chat.error.report_rate_limited":
                return Loc("report_wait");
            case "chat.error.muted":
                return Loc("you_are_muted");
            case "chat.error.banned":
                return Loc("you_are_banned");
            case "chat.error.disabled":
                return Loc("chat_disabled");
            case "chat.error.moderator_required":
                return Loc("mod_required");
            case "chat.error.target_not_found":
                return Loc("target_missing");
            case "chat.error.message_not_found":
                return Loc("message_missing");
            case "chat.error.invalid_request":
                return Loc("invalid_action");
            default:
                return Loc("failed");
        }
    }

    private static string Loc(string key)
    {
        string language =
            AtlasBoardLocalizationManager.Instance != null
                ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
                : "en";

        if (language == "tr")
        {
            return Turkish(key);
        }
        if (language == "es")
        {
            return Spanish(key);
        }
        if (language == "fr")
        {
            return French(key);
        }
        if (language == "de")
        {
            return German(key);
        }
        if (language == "ko")
        {
            return Korean(key);
        }
        if (language == "ru")
        {
            return Russian(key);
        }

        return English(key);
    }

    private static string English(string key)
    {
        switch (key)
        {
            case "title": return "CHAT SAFETY";
            case "description": return "Choose a reason, then use REPORT next to a player or message.";
            case "text_size": return "Chat text size";
            case "reason": return "Report reason";
            case "players": return "PLAYERS";
            case "recent_messages": return "RECENT MESSAGES";
            case "mute": return "MUTE";
            case "unmute": return "UNMUTE";
            case "report": return "REPORT PLAYER";
            case "report_message": return "REPORT MESSAGE";
            case "your_message": return "YOUR MESSAGE — cannot report yourself";
            case "block": return "BLOCK";
            case "unblock": return "UNBLOCK";
            case "remove": return "DEL";
            case "loading": return "Loading safety state...";
            case "working": return "Working...";
            case "reported": return "Report submitted.";
            case "removed": return "Message removed.";
            case "muted_done": return "Player muted.";
            case "unmuted_done": return "Player unmuted.";
            case "blocked_done": return "Player blocked.";
            case "unblocked_done": return "Player unblocked.";
            case "no_players": return "No other Human players.";
            case "no_players_report_hint": return "No other Human players. REPORT PLAYER appears here when another Human is present.";
            case "no_messages": return "No recent messages yet.";
            case "filter": return "Filter";
            case "report_wait": return "Please wait before reporting again.";
            case "you_are_muted": return "Your chat is muted.";
            case "you_are_banned": return "Your chat access is banned.";
            case "chat_disabled": return "Chat is disabled for this account.";
            case "mod_required": return "Moderator authority required.";
            case "target_missing": return "That player is no longer in this chat.";
            case "message_missing": return "That message is no longer available.";
            case "invalid_action": return "That report action is not available.";
            case "reason_abuse": return "Abuse";
            case "reason_harassment": return "Harassment";
            case "reason_spam": return "Spam";
            case "reason_profanity": return "Profanity";
            case "reason_cheating": return "Cheating";
            case "reason_other": return "Other";
            default: return "Action failed.";
        }
    }

    private static string Turkish(string key)
    {
        switch (key)
        {
            case "title": return "SOHBET GÜVENLİĞİ";
            case "description": return "Nedeni seç, sonra oyuncu veya mesaj yanındaki ŞİKÂYET düğmesine bas.";
            case "text_size": return "Sohbet yazı boyutu";
            case "reason": return "Şikâyet nedeni";
            case "players": return "OYUNCULAR";
            case "recent_messages": return "SON MESAJLAR";
            case "mute": return "SUSTUR";
            case "unmute": return "AÇ";
            case "report": return "OYUNCUYU ŞİKÂYET";
            case "report_message": return "MESAJI ŞİKÂYET";
            case "your_message": return "SENİN MESAJIN — kendini şikâyet edemezsin";
            case "block": return "ENGELLE";
            case "unblock": return "ENGELİ AÇ";
            case "remove": return "SİL";
            case "loading": return "Güvenlik bilgisi yükleniyor...";
            case "working": return "İşleniyor...";
            case "reported": return "Şikâyet gönderildi.";
            case "removed": return "Mesaj kaldırıldı.";
            case "muted_done": return "Oyuncu susturuldu.";
            case "unmuted_done": return "Oyuncunun sesi açıldı.";
            case "blocked_done": return "Oyuncu engellendi.";
            case "unblocked_done": return "Oyuncunun engeli kaldırıldı.";
            case "no_players": return "Başka insan oyuncu yok.";
            case "no_players_report_hint": return "Başka insan oyuncu yok. Başka bir insan olduğunda OYUNCUYU ŞİKÂYET düğmesi burada görünür.";
            case "no_messages": return "Henüz son mesaj yok.";
            case "filter": return "Filtre";
            case "report_wait": return "Yeni şikâyet için biraz bekle.";
            case "you_are_muted": return "Sohbetin susturuldu.";
            case "you_are_banned": return "Sohbet erişimin engellendi.";
            case "chat_disabled": return "Bu hesapta sohbet kapalı.";
            case "mod_required": return "Moderatör yetkisi gerekli.";
            case "target_missing": return "Bu oyuncu artık sohbette değil.";
            case "message_missing": return "Bu mesaj artık mevcut değil.";
            case "invalid_action": return "Bu şikâyet işlemi kullanılamıyor.";
            case "reason_abuse": return "Kötüye kullanım";
            case "reason_harassment": return "Taciz";
            case "reason_spam": return "Spam";
            case "reason_profanity": return "Küfür";
            case "reason_cheating": return "Hile";
            case "reason_other": return "Diğer";
            default: return "İşlem başarısız.";
        }
    }

    private static string Spanish(string key)
    {
        switch (key)
        {
            case "title": return "SEGURIDAD DEL CHAT";
            case "description": return "Elige un motivo y usa REPORTAR junto a un jugador o mensaje.";
            case "no_players_report_hint": return "No hay otros jugadores humanos. REPORTAR aparecerá aquí cuando haya otro jugador.";
            case "text_size": return "Tamaño del chat";
            case "reason": return "Motivo";
            case "players": return "JUGADORES";
            case "recent_messages": return "MENSAJES RECIENTES";
            case "mute": return "SILENCIAR";
            case "unmute": return "ACTIVAR";
            case "report": return "REPORTAR JUGADOR";
            case "report_message": return "REPORTAR MENSAJE";
            case "your_message": return "TU MENSAJE";
            case "block": return "BLOQUEAR";
            case "unblock": return "DESBLOQ.";
            case "remove": return "BORRAR";
            default: return English(key);
        }
    }

    private static string French(string key)
    {
        switch (key)
        {
            case "title": return "SÉCURITÉ DU CHAT";
            case "description": return "Choisissez un motif puis utilisez SIGNALER près d’un joueur ou message.";
            case "text_size": return "Taille du chat";
            case "reason": return "Motif";
            case "players": return "JOUEURS";
            case "recent_messages": return "MESSAGES RÉCENTS";
            case "mute": return "MUET";
            case "unmute": return "RÉTABLIR";
            case "report": return "SIGNALER JOUEUR";
            case "report_message": return "SIGNALER MESSAGE";
            case "your_message": return "VOTRE MESSAGE — non signalable";
            case "no_players_report_hint": return "Aucun autre joueur humain. SIGNALER JOUEUR apparaîtra ici quand un autre joueur sera présent.";
            case "block": return "BLOQUER";
            case "unblock": return "DÉBLOQ.";
            case "remove": return "SUPPR.";
            default: return English(key);
        }
    }

    private static string German(string key)
    {
        switch (key)
        {
            case "title": return "CHAT-SICHERHEIT";
            case "description": return "Grund wählen und MELDEN neben Spieler oder Nachricht benutzen.";
            case "no_players_report_hint": return "Keine anderen menschlichen Spieler. MELDEN erscheint hier, sobald ein weiterer Spieler da ist.";
            case "text_size": return "Chat-Textgröße";
            case "reason": return "Meldegrund";
            case "players": return "SPIELER";
            case "recent_messages": return "LETZTE NACHRICHTEN";
            case "mute": return "STUMM";
            case "unmute": return "AN";
            case "report": return "SPIELER MELDEN";
            case "report_message": return "NACHRICHT MELDEN";
            case "your_message": return "DEINE NACHRICHT";
            case "block": return "BLOCK";
            case "unblock": return "FREIGEBEN";
            case "remove": return "LÖSCHEN";
            default: return English(key);
        }
    }

    private static string Korean(string key)
    {
        switch (key)
        {
            case "title": return "채팅 안전";
            case "description": return "신고 사유를 고른 뒤 플레이어 또는 메시지 옆의 신고 버튼을 누르세요.";
            case "no_players_report_hint": return "다른 인간 플레이어가 없습니다. 다른 플레이어가 있으면 신고 버튼이 여기에 표시됩니다.";
            case "text_size": return "채팅 글자 크기";
            case "reason": return "신고 사유";
            case "players": return "플레이어";
            case "recent_messages": return "최근 메시지";
            case "mute": return "음소거";
            case "unmute": return "해제";
            case "report": return "플레이어 신고";
            case "report_message": return "메시지 신고";
            case "your_message": return "내 메시지";
            case "block": return "차단";
            case "unblock": return "차단 해제";
            case "remove": return "삭제";
            default: return English(key);
        }
    }

    private static string Russian(string key)
    {
        switch (key)
        {
            case "title": return "БЕЗОПАСНОСТЬ ЧАТА";
            case "description": return "Выберите причину и нажмите ЖАЛОБА рядом с игроком или сообщением.";
            case "no_players_report_hint": return "Других игроков-людей нет. Кнопка ЖАЛОБА появится здесь, когда будет другой игрок.";
            case "text_size": return "Размер текста";
            case "reason": return "Причина жалобы";
            case "players": return "ИГРОКИ";
            case "recent_messages": return "НЕДАВНИЕ СООБЩЕНИЯ";
            case "mute": return "ЗАГЛУШИТЬ";
            case "unmute": return "ВКЛЮЧИТЬ";
            case "report": return "ЖАЛОБА НА ИГРОКА";
            case "report_message": return "ЖАЛОБА НА СООБЩЕНИЕ";
            case "your_message": return "ВАШЕ СООБЩЕНИЕ";
            case "block": return "БЛОК";
            case "unblock": return "РАЗБЛОК";
            case "remove": return "УДАЛИТЬ";
            default: return English(key);
        }
    }
}
