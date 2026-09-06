using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AtlasBoardChatUIController : MonoBehaviour
{
    private const float PollIntervalSeconds = 1f;
    private const int DefaultMaxLength = 120;
    private const int DefaultCooldownMs = 10000;

    private AtlasBoardChatRuntimeBridge bridge;
    private Canvas canvas;
    private GameObject launcherRoot;
    private Button launcherButton;
    private TMP_Text launcherText;
    private GameObject unreadRoot;
    private TMP_Text unreadText;
    private GameObject panelRoot;
    private TMP_Text titleText;
    private TMP_Text statusText;
    private TMP_Text counterText;
    private TMP_InputField inputField;
    private Button sendButton;
    private TMP_Text sendButtonText;
    private RectTransform messageContent;
    private ScrollRect scrollRect;

    private AtlasBoardChatScope currentScope;
    private readonly HashSet<string> knownMessageIds =
        new HashSet<string>();
    private long lastMessageEpochMs;
    private float nextPollAt;
    private bool pollInFlight;
    private bool sendInFlight;
    private bool initialSnapshotLoaded;
    private int unreadCount;
    private int maxMessageLength = DefaultMaxLength;
    private int cooldownMs = DefaultCooldownMs;
    private float localCooldownEndsAt;

    private void Awake()
    {
        bridge = GetComponent<AtlasBoardChatRuntimeBridge>();
        if (bridge == null)
        {
            bridge = gameObject.AddComponent<AtlasBoardChatRuntimeBridge>();
        }

        BuildUI();
        SetChatAvailable(false);
    }

    private void Update()
    {
        if (bridge == null)
        {
            return;
        }

        if (!bridge.TryResolveActiveScope(out AtlasBoardChatScope scope))
        {
            if (currentScope != null)
            {
                ResetScope(null);
            }

            SetChatAvailable(false);
            return;
        }

        SetChatAvailable(true);

        if (currentScope == null ||
            !string.Equals(
                currentScope.StableKey,
                scope.StableKey,
                StringComparison.Ordinal))
        {
            ResetScope(scope);
        }

        RefreshButtonState();

        if (!pollInFlight &&
            Time.unscaledTime >= nextPollAt)
        {
            nextPollAt =
                Time.unscaledTime + PollIntervalSeconds;
            PollAsync();
        }
    }

    private async void PollAsync()
    {
        if (pollInFlight || currentScope == null)
        {
            return;
        }

        pollInFlight = true;
        string expectedScopeKey = currentScope.StableKey;

        try
        {
            AtlasBoardChatListResult result =
                await bridge.ListMessagesAsync(
                    currentScope,
                    lastMessageEpochMs);

            if (currentScope == null ||
                !string.Equals(
                    expectedScopeKey,
                    currentScope.StableKey,
                    StringComparison.Ordinal))
            {
                return;
            }

            if (!result.Success)
            {
                SetStatus(LocalizeError(result.ErrorKey));
                return;
            }

            maxMessageLength =
                result.MaxMessageLength > 0
                    ? result.MaxMessageLength
                    : DefaultMaxLength;
            cooldownMs =
                result.CooldownMs > 0
                    ? result.CooldownMs
                    : DefaultCooldownMs;

            if (inputField != null)
            {
                inputField.characterLimit = maxMessageLength;
            }

            bool wasInitialLoad = !initialSnapshotLoaded;

            foreach (AtlasBoardChatMessage message in result.Messages)
            {
                if (message == null ||
                    string.IsNullOrWhiteSpace(message.messageId) ||
                    knownMessageIds.Contains(message.messageId))
                {
                    continue;
                }

                knownMessageIds.Add(message.messageId);
                lastMessageEpochMs =
                    Math.Max(
                        lastMessageEpochMs,
                        message.createdAtEpochMs);

                AddMessageBubble(message);

                if (!wasInitialLoad &&
                    !panelRoot.activeSelf &&
                    !string.Equals(
                        message.accountId,
                        bridge.LocalAccountId,
                        StringComparison.Ordinal))
                {
                    unreadCount++;
                }
            }

            initialSnapshotLoaded = true;
            UpdateUnreadBadge();

            if (panelRoot.activeSelf)
            {
                unreadCount = 0;
                UpdateUnreadBadge();
                ScrollToBottomSoon();
            }

            if (string.IsNullOrWhiteSpace(statusText.text) ||
                statusText.text == Localize("loading"))
            {
                SetStatus(string.Empty);
            }
        }
        finally
        {
            pollInFlight = false;
        }
    }

    private async void SendAsync()
    {
        if (sendInFlight ||
            currentScope == null ||
            inputField == null)
        {
            return;
        }

        string body = inputField.text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(body))
        {
            SetStatus(Localize("empty"));
            return;
        }

        if (Time.unscaledTime < localCooldownEndsAt)
        {
            SetStatus(Localize("rate"));
            return;
        }

        sendInFlight = true;
        RefreshButtonState();
        SetStatus(Localize("sending"));

        string expectedScopeKey = currentScope.StableKey;

        int utcOffsetMinutes =
            Mathf.RoundToInt(
                (float)DateTimeOffset.Now.Offset.TotalMinutes);

        AtlasBoardChatSendResult result =
            await bridge.SendMessageAsync(
                currentScope,
                body,
                utcOffsetMinutes);

        sendInFlight = false;

        if (currentScope == null ||
            !string.Equals(
                expectedScopeKey,
                currentScope.StableKey,
                StringComparison.Ordinal))
        {
            RefreshButtonState();
            return;
        }

        if (!result.Success)
        {
            if (result.RetryAfterMs > 0)
            {
                localCooldownEndsAt =
                    Time.unscaledTime +
                    result.RetryAfterMs / 1000f;
            }

            SetStatus(LocalizeError(result.ErrorKey));
            RefreshButtonState();
            return;
        }

        cooldownMs =
            result.CooldownMs > 0
                ? result.CooldownMs
                : DefaultCooldownMs;
        localCooldownEndsAt =
            Time.unscaledTime +
            cooldownMs / 1000f;

        inputField.text = string.Empty;
        SetStatus(string.Empty);

        if (result.Message != null &&
            !string.IsNullOrWhiteSpace(result.Message.messageId) &&
            !knownMessageIds.Contains(result.Message.messageId))
        {
            knownMessageIds.Add(result.Message.messageId);
            lastMessageEpochMs =
                Math.Max(
                    lastMessageEpochMs,
                    result.Message.createdAtEpochMs);
            AddMessageBubble(result.Message);
            ScrollToBottomSoon();
        }

        RefreshButtonState();
    }

    private void ResetScope(AtlasBoardChatScope scope)
    {
        currentScope = scope;
        knownMessageIds.Clear();
        lastMessageEpochMs = 0L;
        initialSnapshotLoaded = false;
        unreadCount = 0;
        nextPollAt = 0f;
        localCooldownEndsAt = 0f;

        ClearMessageBubbles();
        UpdateUnreadBadge();
        UpdateTitle();
        UpdateLauncherLayout();

        if (scope != null)
        {
            SetStatus(Localize("loading"));
        }
        else
        {
            SetStatus(string.Empty);
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }
    }

    private void TogglePanel()
    {
        if (panelRoot == null)
        {
            return;
        }

        bool next = !panelRoot.activeSelf;
        panelRoot.SetActive(next);

        if (next)
        {
            unreadCount = 0;
            UpdateUnreadBadge();
            ScrollToBottomSoon();

            if (inputField != null)
            {
                inputField.ActivateInputField();
            }
        }
    }

    private void ClosePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void HandleInputChanged(string value)
    {
        int count =
            value != null
                ? value.Length
                : 0;

        if (counterText != null)
        {
            counterText.text =
                $"{count}/{maxMessageLength}";
        }

        RefreshButtonState();
    }

    private void HandleInputSubmit(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            SendAsync();
        }
    }

    private void RefreshButtonState()
    {
        if (sendButton == null ||
            sendButtonText == null)
        {
            return;
        }

        float remaining =
            Mathf.Max(
                0f,
                localCooldownEndsAt - Time.unscaledTime);

        bool hasText =
            inputField != null &&
            !string.IsNullOrWhiteSpace(inputField.text);

        sendButton.interactable =
            !sendInFlight &&
            remaining <= 0f &&
            hasText &&
            currentScope != null;

        if (sendInFlight)
        {
            sendButtonText.text = Localize("sending_button");
        }
        else if (remaining > 0f)
        {
            sendButtonText.text =
                $"{Localize("send")} {Mathf.CeilToInt(remaining)}";
        }
        else
        {
            sendButtonText.text = Localize("send");
        }
    }

    private void SetChatAvailable(bool available)
    {
        if (launcherRoot != null &&
            launcherRoot.activeSelf != available)
        {
            launcherRoot.SetActive(available);
        }

        if (!available &&
            panelRoot != null &&
            panelRoot.activeSelf)
        {
            panelRoot.SetActive(false);
        }
    }

    private void UpdateTitle()
    {
        if (titleText == null)
        {
            return;
        }

        titleText.text =
            currentScope != null &&
            currentScope.ScopeType == AtlasBoardChatScopeType.Match
                ? Localize("match_title")
                : Localize("lobby_title");
    }

    private void UpdateLauncherLayout()
    {
        if (launcherRoot == null ||
            launcherText == null)
        {
            return;
        }

        bool matchScope =
            currentScope != null &&
            currentScope.ScopeType ==
                AtlasBoardChatScopeType.Match;

        RectTransform launcherRect =
            launcherRoot.GetComponent<RectTransform>();

        if (launcherRect != null)
        {
            launcherRect.anchorMin =
                new Vector2(1f, 0.5f);
            launcherRect.anchorMax =
                new Vector2(1f, 0.5f);
            launcherRect.pivot =
                new Vector2(1f, 0.5f);
            launcherRect.anchoredPosition =
                matchScope
                    ? new Vector2(-10f, 92f)
                    : new Vector2(-10f, 0f);
            launcherRect.sizeDelta =
                new Vector2(44f, 126f);
        }

        RectTransform textRect =
            launcherText.rectTransform;

        if (textRect != null)
        {
            textRect.anchorMin =
                new Vector2(0.5f, 0.5f);
            textRect.anchorMax =
                new Vector2(0.5f, 0.5f);
            textRect.pivot =
                new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition =
                Vector2.zero;
            textRect.sizeDelta =
                new Vector2(118f, 36f);
            textRect.localEulerAngles =
                new Vector3(0f, 0f, 90f);
        }

        launcherText.alignment =
            TextAlignmentOptions.Center;
        launcherText.enableAutoSizing = true;
        launcherText.fontSizeMin = 10f;
        launcherText.fontSizeMax = 16f;
        launcherText.textWrappingMode =
            TextWrappingModes.NoWrap;
        launcherText.overflowMode =
            TextOverflowModes.Ellipsis;

        if (panelRoot != null)
        {
            RectTransform panelRect =
                panelRoot.GetComponent<RectTransform>();

            if (panelRect != null)
            {
                panelRect.anchoredPosition =
                    matchScope
                        ? new Vector2(-18f, 238f)
                        : new Vector2(-18f, 72f);
            }
        }
    }

    private void UpdateUnreadBadge()
    {
        if (unreadRoot == null || unreadText == null)
        {
            return;
        }

        bool visible = unreadCount > 0;
        unreadRoot.SetActive(visible);
        unreadText.text =
            unreadCount > 99
                ? "99+"
                : unreadCount.ToString();
    }

    private void SetStatus(string value)
    {
        if (statusText != null)
        {
            statusText.text = value ?? string.Empty;
        }
    }

    private void ClearMessageBubbles()
    {
        if (messageContent == null)
        {
            return;
        }

        for (int index = messageContent.childCount - 1;
             index >= 0;
             index--)
        {
            Destroy(messageContent.GetChild(index).gameObject);
        }
    }

    private void AddMessageBubble(AtlasBoardChatMessage message)
    {
        if (messageContent == null || message == null)
        {
            return;
        }

        bool ownMessage =
            string.Equals(
                message.accountId,
                bridge.LocalAccountId,
                StringComparison.Ordinal);

        GameObject row = new GameObject(
            "ChatMessageRow",
            typeof(RectTransform),
            typeof(LayoutElement));
        row.transform.SetParent(messageContent, false);

        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.minHeight = 56f;
        rowLayout.preferredHeight =
            EstimateBubbleHeight(message.body);

        GameObject bubble = new GameObject(
            "Bubble",
            typeof(RectTransform),
            typeof(Image));
        bubble.transform.SetParent(row.transform, false);

        RectTransform bubbleRect =
            bubble.GetComponent<RectTransform>();
        bubbleRect.anchorMin =
            ownMessage
                ? new Vector2(0.18f, 0f)
                : new Vector2(0f, 0f);
        bubbleRect.anchorMax =
            ownMessage
                ? new Vector2(1f, 1f)
                : new Vector2(0.82f, 1f);
        bubbleRect.offsetMin = new Vector2(2f, 3f);
        bubbleRect.offsetMax = new Vector2(-2f, -3f);

        Image bubbleImage = bubble.GetComponent<Image>();
        bubbleImage.color =
            ownMessage
                ? new Color(0.10f, 0.30f, 0.40f, 0.96f)
                : new Color(0.10f, 0.11f, 0.14f, 0.96f);
        bubbleImage.raycastTarget = false;

        TMP_Text messageText = CreateText(
            bubble.transform,
            "Message",
            15f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft,
            Color.white);
        RectTransform messageRect = messageText.rectTransform;
        messageRect.anchorMin = new Vector2(0f, 0f);
        messageRect.anchorMax = new Vector2(1f, 1f);
        messageRect.offsetMin = new Vector2(10f, 22f);
        messageRect.offsetMax = new Vector2(-10f, -7f);
        messageText.textWrappingMode = TextWrappingModes.Normal;
        messageText.overflowMode = TextOverflowModes.Ellipsis;
        messageText.text =
            $"<b>{EscapeTmp(message.displayName)}</b>: " +
            EscapeTmp(message.body);

        TMP_Text timeText = CreateText(
            bubble.transform,
            "Time",
            10f,
            FontStyles.Normal,
            TextAlignmentOptions.BottomRight,
            new Color(0.78f, 0.82f, 0.88f, 1f));
        RectTransform timeRect = timeText.rectTransform;
        timeRect.anchorMin = new Vector2(0f, 0f);
        timeRect.anchorMax = new Vector2(1f, 0f);
        timeRect.pivot = new Vector2(1f, 0f);
        timeRect.offsetMin = new Vector2(8f, 4f);
        timeRect.offsetMax = new Vector2(-8f, 20f);
        timeText.text = FormatSenderTime(message);
    }

    private static float EstimateBubbleHeight(string body)
    {
        int length = string.IsNullOrEmpty(body)
            ? 0
            : body.Length;
        int estimatedLines = Mathf.Clamp(
            Mathf.CeilToInt(length / 38f),
            1,
            4);
        return 42f + estimatedLines * 18f;
    }

    private static string FormatSenderTime(
        AtlasBoardChatMessage message)
    {
        try
        {
            DateTimeOffset utc =
                DateTimeOffset.FromUnixTimeMilliseconds(
                    message.createdAtEpochMs);
            TimeSpan offset =
                TimeSpan.FromMinutes(
                    Mathf.Clamp(
                        message.senderUtcOffsetMinutes,
                        -14 * 60,
                        14 * 60));
            DateTimeOffset senderLocal =
                utc.ToOffset(offset);

            return $"{senderLocal:HH:mm} " +
                   $"({FormatUtcOffset(message.senderUtcOffsetMinutes)})";
        }
        catch
        {
            return FormatUtcOffset(
                message.senderUtcOffsetMinutes);
        }
    }

    private static string FormatUtcOffset(int offsetMinutes)
    {
        int sign = offsetMinutes < 0 ? -1 : 1;
        int absolute = Mathf.Abs(offsetMinutes);
        int hours = absolute / 60;
        int minutes = absolute % 60;
        string signText = sign < 0 ? "-" : "+";

        if (minutes == 0)
        {
            return $"UTC{signText}{hours}";
        }

        return $"UTC{signText}{hours}:{minutes:00}";
    }

    private async void ScrollToBottomSoon()
    {
        await System.Threading.Tasks.Task.Yield();
        await System.Threading.Tasks.Task.Yield();

        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject(
            "Canvas_AtlasChat",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 550;

        CanvasScaler scaler =
            canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution =
            new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        launcherRoot = CreateButtonObject(
            canvasObject.transform,
            "ChatLauncher",
            Localize("chat"),
            out launcherButton,
            out launcherText,
            new Color(0.07f, 0.09f, 0.13f, 0.95f));

        RectTransform launcherRect =
            launcherRoot.GetComponent<RectTransform>();
        launcherRect.anchorMin = new Vector2(1f, 0.5f);
        launcherRect.anchorMax = new Vector2(1f, 0.5f);
        launcherRect.pivot = new Vector2(1f, 0.5f);
        launcherRect.anchoredPosition = new Vector2(-10f, 0f);
        launcherRect.sizeDelta = new Vector2(44f, 126f);
        launcherButton.onClick.AddListener(TogglePanel);

        unreadRoot = new GameObject(
            "UnreadBadge",
            typeof(RectTransform),
            typeof(Image));
        unreadRoot.transform.SetParent(launcherRoot.transform, false);
        RectTransform unreadRect =
            unreadRoot.GetComponent<RectTransform>();
        unreadRect.anchorMin = new Vector2(1f, 1f);
        unreadRect.anchorMax = new Vector2(1f, 1f);
        unreadRect.pivot = new Vector2(0.5f, 0.5f);
        unreadRect.anchoredPosition = new Vector2(-3f, -3f);
        unreadRect.sizeDelta = new Vector2(28f, 22f);
        unreadRoot.GetComponent<Image>().color =
            new Color(0.82f, 0.12f, 0.18f, 1f);
        unreadText = CreateText(
            unreadRoot.transform,
            "Count",
            11f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white);
        Stretch(unreadText.rectTransform, 0f);

        panelRoot = new GameObject(
            "ChatPanel",
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
            new Color(0.035f, 0.045f, 0.065f, 0.985f);
        Outline outline = panelRoot.GetComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.55f, 0.72f, 0.65f);
        outline.effectDistance = new Vector2(2f, -2f);

        titleText = CreateText(
            panelRoot.transform,
            "Title",
            18f,
            FontStyles.Bold,
            TextAlignmentOptions.Left,
            Color.white);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.offsetMin = new Vector2(16f, -46f);
        titleRect.offsetMax = new Vector2(-62f, -10f);

        GameObject close = CreateButtonObject(
            panelRoot.transform,
            "Close",
            "×",
            out Button closeButton,
            out _,
            new Color(0.16f, 0.18f, 0.23f, 1f));
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-10f, -9f);
        closeRect.sizeDelta = new Vector2(38f, 34f);
        closeButton.onClick.AddListener(ClosePanel);

        BuildScrollView(panelRoot.transform);
        BuildComposer(panelRoot.transform);

        panelRoot.SetActive(false);
        unreadRoot.SetActive(false);
        UpdateTitle();
        UpdateLauncherLayout();
    }

    private void BuildScrollView(Transform parent)
    {
        GameObject scrollObject = new GameObject(
            "MessagesScroll",
            typeof(RectTransform),
            typeof(ScrollRect));
        scrollObject.transform.SetParent(parent, false);
        RectTransform scrollRectTransform =
            scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 1f);
        scrollRectTransform.offsetMin = new Vector2(12f, 104f);
        scrollRectTransform.offsetMax = new Vector2(-12f, -54f);

        GameObject viewport = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));
        viewport.transform.SetParent(scrollObject.transform, false);
        RectTransform viewportRect =
            viewport.GetComponent<RectTransform>();
        Stretch(viewportRect, 0f);
        viewport.GetComponent<Image>().color =
            new Color(0.02f, 0.025f, 0.035f, 0.45f);

        GameObject content = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        messageContent = content.GetComponent<RectTransform>();
        messageContent.anchorMin = new Vector2(0f, 1f);
        messageContent.anchorMax = new Vector2(1f, 1f);
        messageContent.pivot = new Vector2(0.5f, 1f);
        messageContent.offsetMin = new Vector2(8f, 0f);
        messageContent.offsetMax = new Vector2(-8f, 0f);

        VerticalLayoutGroup layout =
            content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(0, 0, 6, 6);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter =
            content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = messageContent;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;
    }

    private void BuildComposer(Transform parent)
    {
        GameObject inputRoot = new GameObject(
            "InputRoot",
            typeof(RectTransform),
            typeof(Image));
        inputRoot.transform.SetParent(parent, false);
        RectTransform inputRootRect =
            inputRoot.GetComponent<RectTransform>();
        inputRootRect.anchorMin = new Vector2(0f, 0f);
        inputRootRect.anchorMax = new Vector2(1f, 0f);
        inputRootRect.pivot = new Vector2(0.5f, 0f);
        inputRootRect.offsetMin = new Vector2(12f, 42f);
        inputRootRect.offsetMax = new Vector2(-108f, 92f);
        inputRoot.GetComponent<Image>().color =
            new Color(0.09f, 0.10f, 0.13f, 1f);

        inputField = inputRoot.AddComponent<TMP_InputField>();
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterLimit = maxMessageLength;

        TMP_Text inputText = CreateText(
            inputRoot.transform,
            "Text",
            14f,
            FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft,
            Color.white);
        RectTransform inputTextRect = inputText.rectTransform;
        Stretch(inputTextRect, 8f);
        inputTextRect.offsetMax = new Vector2(-8f, -4f);
        inputTextRect.offsetMin = new Vector2(8f, 4f);
        inputText.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_Text placeholder = CreateText(
            inputRoot.transform,
            "Placeholder",
            14f,
            FontStyles.Italic,
            TextAlignmentOptions.MidlineLeft,
            new Color(0.55f, 0.58f, 0.65f, 1f));
        Stretch(placeholder.rectTransform, 8f);
        placeholder.text = Localize("placeholder");

        inputField.textComponent = inputText;
        inputField.placeholder = placeholder;
        inputField.onValueChanged.AddListener(HandleInputChanged);
        inputField.onSubmit.AddListener(HandleInputSubmit);

        GameObject sendRoot = CreateButtonObject(
            parent,
            "Send",
            Localize("send"),
            out sendButton,
            out sendButtonText,
            new Color(0.08f, 0.38f, 0.50f, 1f));
        RectTransform sendRect = sendRoot.GetComponent<RectTransform>();
        sendRect.anchorMin = new Vector2(1f, 0f);
        sendRect.anchorMax = new Vector2(1f, 0f);
        sendRect.pivot = new Vector2(1f, 0f);
        sendRect.anchoredPosition = new Vector2(-12f, 42f);
        sendRect.sizeDelta = new Vector2(88f, 50f);
        sendButton.onClick.AddListener(SendAsync);

        counterText = CreateText(
            parent,
            "Counter",
            10f,
            FontStyles.Normal,
            TextAlignmentOptions.BottomLeft,
            new Color(0.65f, 0.68f, 0.75f, 1f));
        RectTransform counterRect = counterText.rectTransform;
        counterRect.anchorMin = new Vector2(0f, 0f);
        counterRect.anchorMax = new Vector2(0f, 0f);
        counterRect.pivot = new Vector2(0f, 0f);
        counterRect.anchoredPosition = new Vector2(14f, 12f);
        counterRect.sizeDelta = new Vector2(70f, 22f);
        counterText.text = $"0/{maxMessageLength}";

        statusText = CreateText(
            parent,
            "Status",
            10f,
            FontStyles.Normal,
            TextAlignmentOptions.BottomRight,
            new Color(0.86f, 0.69f, 0.35f, 1f));
        RectTransform statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(1f, 0f);
        statusRect.offsetMin = new Vector2(88f, 9f);
        statusRect.offsetMax = new Vector2(-14f, 34f);
    }

    private static GameObject CreateButtonObject(
        Transform parent,
        string name,
        string label,
        out Button button,
        out TMP_Text labelText,
        Color backgroundColor)
    {
        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        root.transform.SetParent(parent, false);

        Image image = root.GetComponent<Image>();
        image.color = backgroundColor;

        button = root.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = backgroundColor * 1.15f;
        colors.pressedColor = backgroundColor * 0.85f;
        colors.disabledColor = new Color(
            backgroundColor.r,
            backgroundColor.g,
            backgroundColor.b,
            0.45f);
        button.colors = colors;

        labelText = CreateText(
            root.transform,
            "Label",
            14f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white);
        Stretch(labelText.rectTransform, 4f);
        labelText.text = label;

        return root;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text =
            textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.richText = true;
        text.overflowMode = TextOverflowModes.Ellipsis;

        return text;
    }

    private static void Stretch(
        RectTransform rect,
        float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    private static string EscapeTmp(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static string CurrentLanguage()
    {
        return AtlasBoardLocalizationManager.Instance != null
            ? (AtlasBoardLocalizationManager.Instance.CurrentLanguageCode ?? "en")
                .ToLowerInvariant()
            : "en";
    }

    private static string Localize(string key)
    {
        string language = CurrentLanguage();

        return key switch
        {
            "chat" => language switch
            {
                "tr" => "SOHBET", "es" => "CHAT", "fr" => "CHAT",
                "de" => "CHAT", "ko" => "채팅", "ru" => "ЧАТ", _ => "CHAT"
            },
            "lobby_title" => language switch
            {
                "tr" => "LOBİ SOHBETİ", "es" => "CHAT DEL LOBBY",
                "fr" => "CHAT DU LOBBY", "de" => "LOBBY-CHAT",
                "ko" => "로비 채팅", "ru" => "ЧАТ ЛОББИ", _ => "LOBBY CHAT"
            },
            "match_title" => language switch
            {
                "tr" => "MAÇ SOHBETİ", "es" => "CHAT DE PARTIDA",
                "fr" => "CHAT DE PARTIE", "de" => "MATCH-CHAT",
                "ko" => "게임 채팅", "ru" => "ЧАТ МАТЧА", _ => "MATCH CHAT"
            },
            "placeholder" => language switch
            {
                "tr" => "Mesaj yaz...", "es" => "Escribe un mensaje...",
                "fr" => "Écrire un message...", "de" => "Nachricht schreiben...",
                "ko" => "메시지 입력...", "ru" => "Введите сообщение...",
                _ => "Type a message..."
            },
            "send" => language switch
            {
                "tr" => "GÖNDER", "es" => "ENVIAR", "fr" => "ENVOYER",
                "de" => "SENDEN", "ko" => "보내기", "ru" => "ОТПРАВИТЬ",
                _ => "SEND"
            },
            "sending_button" => language switch
            {
                "tr" => "...", "es" => "...", "fr" => "...", "de" => "...",
                "ko" => "...", "ru" => "...", _ => "..."
            },
            "loading" => language switch
            {
                "tr" => "Yükleniyor...", "es" => "Cargando...",
                "fr" => "Chargement...", "de" => "Lädt...",
                "ko" => "불러오는 중...", "ru" => "Загрузка...", _ => "Loading..."
            },
            "sending" => language switch
            {
                "tr" => "Gönderiliyor...", "es" => "Enviando...",
                "fr" => "Envoi...", "de" => "Wird gesendet...",
                "ko" => "보내는 중...", "ru" => "Отправка...", _ => "Sending..."
            },
            "empty" => language switch
            {
                "tr" => "Boş mesaj gönderilemez.",
                "es" => "No puedes enviar un mensaje vacío.",
                "fr" => "Impossible d'envoyer un message vide.",
                "de" => "Leere Nachrichten sind nicht erlaubt.",
                "ko" => "빈 메시지는 보낼 수 없습니다.",
                "ru" => "Нельзя отправить пустое сообщение.",
                _ => "Empty messages are not allowed."
            },
            "rate" => language switch
            {
                "tr" => "Her 10 saniyede bir mesaj gönderebilirsin.",
                "es" => "Puedes enviar un mensaje cada 10 segundos.",
                "fr" => "Un message toutes les 10 secondes.",
                "de" => "Du kannst alle 10 Sekunden eine Nachricht senden.",
                "ko" => "10초마다 한 번 메시지를 보낼 수 있습니다.",
                "ru" => "Сообщение можно отправлять раз в 10 секунд.",
                _ => "You can send one message every 10 seconds."
            },
            _ => key
        };
    }

    private static string LocalizeError(string errorKey)
    {
        string language = CurrentLanguage();

        if (errorKey == "chat.error.blocked_language")
        {
            return language switch
            {
                "tr" => "Mesaj engellenmiş/küfürlü ifade içeriyor.",
                "es" => "El mensaje contiene lenguaje bloqueado.",
                "fr" => "Le message contient un langage bloqué.",
                "de" => "Die Nachricht enthält gesperrte Sprache.",
                "ko" => "차단된 표현이 포함되어 있습니다.",
                "ru" => "Сообщение содержит запрещённое выражение.",
                _ => "The message contains blocked language."
            };
        }

        if (errorKey == "chat.error.message_too_long")
        {
            return language switch
            {
                "tr" => "Mesaj en fazla 120 karakter olabilir.",
                "es" => "El mensaje puede tener hasta 120 caracteres.",
                "fr" => "Le message est limité à 120 caractères.",
                "de" => "Nachrichten dürfen höchstens 120 Zeichen lang sein.",
                "ko" => "메시지는 최대 120자까지 가능합니다.",
                "ru" => "Максимальная длина сообщения — 120 символов.",
                _ => "Messages are limited to 120 characters."
            };
        }

        if (errorKey == "chat.error.empty_message")
        {
            return Localize("empty");
        }

        if (errorKey == "chat.error.rate_limited")
        {
            return Localize("rate");
        }

        if (errorKey == "chat.error.member_only")
        {
            return language switch
            {
                "tr" => "Bu sohbete erişimin yok.",
                "es" => "No tienes acceso a este chat.",
                "fr" => "Vous n'avez pas accès à ce chat.",
                "de" => "Du hast keinen Zugriff auf diesen Chat.",
                "ko" => "이 채팅에 접근할 수 없습니다.",
                "ru" => "У вас нет доступа к этому чату.",
                _ => "You do not have access to this chat."
            };
        }

        return language switch
        {
            "tr" => "Sohbet servisine ulaşılamadı.",
            "es" => "No se pudo acceder al servicio de chat.",
            "fr" => "Service de chat indisponible.",
            "de" => "Chat-Dienst nicht erreichbar.",
            "ko" => "채팅 서비스에 연결할 수 없습니다.",
            "ru" => "Сервис чата недоступен.",
            _ => "Chat service is unavailable."
        };
    }
}
