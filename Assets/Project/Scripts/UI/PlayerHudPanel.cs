using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHudPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private RectTransform panelRect;

    [SerializeField]
    private Image backgroundImage;

    [SerializeField]
    private Image accentImage;

    [SerializeField]
    private Image iconImage;

    [SerializeField]
    private TMP_Text iconText;

    [SerializeField]
    private TMP_Text playerNameText;

    [SerializeField]
    private TMP_Text moneyText;

    [SerializeField]
    private TMP_Text controlTypeText;

    [SerializeField]
    private GameObject turnBadgeRoot;

    [SerializeField]
    private Image turnBadgeImage;

    [SerializeField]
    private TMP_Text turnBadgeText;

    [SerializeField]
    private Outline panelOutline;

    private PlayerGameState player;
    private BotPlayerController botController;
    private bool subscribed;
    private bool lastIsCurrentTurn;

    public RectTransform PanelRect =>
        panelRect != null
            ? panelRect
            : transform as RectTransform;

    public PlayerGameState Player =>
        player;

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

    private void HandleLanguageChanged()
    {
        if (player != null)
        {
            Refresh(
                lastIsCurrentTurn);
        }
    }

    public void Bind(
        PlayerGameState playerState)
    {
        Unsubscribe();

        player = playerState;

        botController =
            player != null
                ? player.GetComponent<
                    BotPlayerController>()
                : null;

        Subscribe();
        Refresh(false);
    }

    public void Refresh(
        bool isCurrentTurn)
    {
        lastIsCurrentTurn =
            isCurrentTurn;

        if (player == null)
        {
            gameObject.SetActive(false);
            return;
        }

        bool visible =
            player.IsParticipating;

        if (gameObject.activeSelf != visible)
        {
            gameObject.SetActive(visible);
        }

        if (!visible)
        {
            return;
        }

        ApplyRuntimeLayout();

        Color playerColor =
            player.UIColor;

        if (accentImage != null)
        {
            accentImage.color =
                playerColor;
        }

        if (iconImage != null)
        {
            Color iconColor =
                playerColor;

            iconColor.a = 0.95f;

            iconImage.color =
                iconColor;
        }

        if (iconText != null)
        {
            iconText.text =
                $"P{player.PlayerSlotIndex + 1}";
        }

        if (playerNameText != null)
        {
            playerNameText.text =
                GetLocalizedPlayerName(
                    player);
        }

        if (moneyText != null)
        {
            moneyText.text =
                player.IsBankrupt
                    ? AtlasBoardL.T(
                        "hud.bankrupt")
                    : $"{player.CurrentMoney} ₵";
        }

        if (controlTypeText != null)
        {
            controlTypeText.text =
                ResolveControlTypeLabel();
        }

        ApplyTurnBadgeLayout();

        if (turnBadgeRoot != null)
        {
            turnBadgeRoot.SetActive(
                isCurrentTurn);
        }

        if (turnBadgeImage != null)
        {
            Color badgeColor =
                playerColor;

            badgeColor.a = 0.95f;

            turnBadgeImage.color =
                badgeColor;
        }

        if (turnBadgeText != null)
        {
            turnBadgeText.text =
                AtlasBoardL.T(
                    "hud.turn")
                    .ToUpperInvariant();
        }

        if (panelOutline != null)
        {
            panelOutline.enabled =
                isCurrentTurn;

            Color outlineColor =
                playerColor;

            outlineColor.a = 1f;

            panelOutline.effectColor =
                outlineColor;

            panelOutline.effectDistance =
                isCurrentTurn
                    ? new Vector2(3f, -3f)
                    : new Vector2(1f, -1f);
        }

        if (backgroundImage != null)
        {
            backgroundImage.color =
                player.IsBankrupt
                    ? new Color(
                        0.10f,
                        0.10f,
                        0.10f,
                        0.82f)
                    : isCurrentTurn
                        ? new Color(
                            0.08f,
                            0.10f,
                            0.14f,
                            0.96f)
                        : new Color(
                            0.055f,
                            0.065f,
                            0.085f,
                            0.90f);
        }
    }

    private void ApplyTurnBadgeLayout()
    {
        if (turnBadgeRoot == null)
        {
            return;
        }

        RectTransform badgeRect =
            turnBadgeRoot.GetComponent<RectTransform>();

        if (badgeRect != null)
        {
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal, 52f);
            badgeRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical, 18f);
            badgeRect.anchoredPosition =
                new Vector2(-8f, -6f);
        }
    }

    private void ApplyRuntimeLayout()
    {
        if (panelRect == null)
        {
            panelRect =
                transform as RectTransform;
        }

        if (backgroundImage != null)
        {
            backgroundImage.type =
                Image.Type.Sliced;
            backgroundImage.raycastTarget = false;
        }

        if (accentImage != null)
        {
            RectTransform accentRect =
                accentImage.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition =
                new Vector2(0f, 0f);
            accentRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                6f);
            accentRect.offsetMin =
                new Vector2(0f, 0f);
            accentRect.offsetMax =
                new Vector2(6f, 0f);
        }

        if (iconImage != null)
        {
            RectTransform iconRect =
                iconImage.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition =
                new Vector2(8f, 0f);
            iconRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                38f);
            iconRect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                38f);
            iconImage.raycastTarget = false;
        }

        if (iconText != null)
        {
            iconText.alignment =
                TextAlignmentOptions.Center;
            iconText.enableAutoSizing = true;
            iconText.fontSizeMin = 12f;
            iconText.fontSizeMax = 20f;
            iconText.fontStyle =
                FontStyles.Bold;
            iconText.color = Color.white;
            iconText.overflowMode =
                TextOverflowModes.Truncate;
        }

        ConfigureTopLane(
            playerNameText,
            54f,
            68f,
            4f,
            18f,
            TextAlignmentOptions.TopLeft,
            true,
            11f,
            18f,
            FontStyles.Bold,
            Color.white);

        ConfigureTopLane(
            moneyText,
            54f,
            12f,
            24f,
            20f,
            TextAlignmentOptions.TopLeft,
            true,
            12f,
            20f,
            FontStyles.Bold,
            new Color(0.96f, 0.95f, 0.90f, 1f));

        ConfigureBottomLane(
            controlTypeText,
            54f,
            12f,
            4f,
            16f,
            TextAlignmentOptions.BottomLeft,
            true,
            8f,
            12f,
            FontStyles.Normal,
            new Color(0.82f, 0.84f, 0.89f, 0.96f));

        if (turnBadgeImage != null)
        {
            turnBadgeImage.type =
                Image.Type.Sliced;
            turnBadgeImage.raycastTarget = false;
        }

        if (turnBadgeText != null)
        {
            turnBadgeText.alignment =
                TextAlignmentOptions.Center;
            turnBadgeText.enableAutoSizing = true;
            turnBadgeText.fontSizeMin = 7f;
            turnBadgeText.fontSizeMax = 11f;
            turnBadgeText.fontStyle =
                FontStyles.Bold;
            turnBadgeText.color = Color.white;
            turnBadgeText.overflowMode =
                TextOverflowModes.Truncate;
        }
    }

    private static void ConfigureTopLane(
        TMP_Text target,
        float left,
        float right,
        float top,
        float height,
        TextAlignmentOptions alignment,
        bool autoSize,
        float minSize,
        float maxSize,
        FontStyles fontStyle,
        Color color)
    {
        if (target == null)
        {
            return;
        }

        RectTransform rect =
            target.rectTransform;

        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

        ConfigureTextCommon(
            target,
            alignment,
            autoSize,
            minSize,
            maxSize,
            fontStyle,
            color);
    }

    private static void ConfigureBottomLane(
        TMP_Text target,
        float left,
        float right,
        float bottom,
        float height,
        TextAlignmentOptions alignment,
        bool autoSize,
        float minSize,
        float maxSize,
        FontStyles fontStyle,
        Color color)
    {
        if (target == null)
        {
            return;
        }

        RectTransform rect =
            target.rectTransform;

        if (rect != null)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
        }

        ConfigureTextCommon(
            target,
            alignment,
            autoSize,
            minSize,
            maxSize,
            fontStyle,
            color);
    }

    private static void ConfigureTextCommon(
        TMP_Text target,
        TextAlignmentOptions alignment,
        bool autoSize,
        float minSize,
        float maxSize,
        FontStyles fontStyle,
        Color color)
    {
        if (target == null)
        {
            return;
        }

        target.alignment = alignment;
        target.enableAutoSizing = autoSize;
        target.fontSizeMin = minSize;
        target.fontSizeMax = maxSize;
        target.fontStyle = fontStyle;
        target.overflowMode =
            TextOverflowModes.Truncate;
        target.textWrappingMode =
            TextWrappingModes.NoWrap;
        target.color = color;
        target.raycastTarget = false;
    }

    private string ResolveControlTypeLabel()
    {
        if (player == null)
        {
            return string.Empty;
        }

        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            FindAnyObjectByType<
                AtlasBoardTurnDiceNetworkCoordinator>();

        // The local authority list is the source of truth for a Human that
        // can actually act on this client. This prevents stale mirrored seat
        // metadata from painting an actively-controlled Human as BOT.
        if (coordinator != null &&
            coordinator.IsLocallyControlledHumanSlot(
                player.PlayerSlotIndex))
        {
            return AtlasBoardL.T(
                "common.human")
                .ToUpperInvariant();
        }

        if (player.IsOnlineTemporaryBot)
        {
            return GetTemporaryBotLabel();
        }

        if (player.IsOnlinePermanentBot)
        {
            return GetPermanentBotLabel();
        }

        bool isBot =
            player.OnlineSeatStateActive
                ? player.IsOnlineBotControlled
                : botController != null &&
                  botController.BotEnabled;

        if (!isBot)
        {
            return AtlasBoardL.T(
                "common.human")
                .ToUpperInvariant();
        }

        string personality =
            botController != null &&
            botController.PersonalityProfile != null
                ? LocalizePersonality(
                    botController
                        .PersonalityProfile
                        .DisplayName)
                : AtlasBoardL.T(
                    "common.bot");

        return $"{AtlasBoardL.T("common.bot").ToUpperInvariant()} • " +
               $"{personality}";
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private static string GetLocalizedPlayerName(
        PlayerGameState state)
    {
        if (state == null)
        {
            return AtlasBoardL.T(
                "common.player");
        }

        string raw =
            state.DisplayName ??
            string.Empty;

        bool looksDefault =
            raw.StartsWith(
                "Oyuncu ",
                System.StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith(
                "Player ",
                System.StringComparison.OrdinalIgnoreCase);

        if (!looksDefault)
        {
            return raw;
        }

        return $"{AtlasBoardL.T("common.player")} " +
               $"{state.PlayerSlotIndex + 1}";
    }

    private static string GetTemporaryBotLabel()
    {
        string language =
            AtlasBoardLocalizationManager.Instance != null
                ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
                : "en";

        return (language ?? "en").ToLowerInvariant() switch
        {
            "tr" => "GEÇİCİ BOT",
            "es" => "BOT TEMPORAL",
            "fr" => "BOT TEMPORAIRE",
            "de" => "TEMPORÄRER BOT",
            "ko" => "임시 봇",
            "ru" => "ВРЕМЕННЫЙ БОТ",
            _ => "TEMPORARY BOT"
        };
    }

    private static string GetPermanentBotLabel()
    {
        string language =
            AtlasBoardLocalizationManager.Instance != null
                ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
                : "en";

        return (language ?? "en").ToLowerInvariant() switch
        {
            "tr" => "KALICI BOT",
            "es" => "BOT PERMANENTE",
            "fr" => "BOT PERMANENT",
            "de" => "PERMANENTER BOT",
            "ko" => "영구 봇",
            "ru" => "ПОСТОЯННЫЙ БОТ",
            _ => "PERMANENT BOT"
        };
    }

    private static string LocalizePersonality(
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(
                displayName))
        {
            return AtlasBoardL.T(
                "common.bot");
        }

        string normalized =
            displayName.Trim().ToLowerInvariant();

        return normalized switch
        {
            "balanced" =>
                AtlasBoardL.T(
                    "bot.balanced"),

            "safe" =>
                AtlasBoardL.T(
                    "bot.safe"),

            "aggressive" =>
                AtlasBoardL.T(
                    "bot.aggressive"),

            "adaptive" =>
                AtlasBoardL.T(
                    "bot.adaptive"),

            _ => displayName
        };
    }

    private void Subscribe()
    {
        if (player == null ||
            subscribed)
        {
            return;
        }

        player.MoneyChanged +=
            HandlePlayerChanged;

        player.TurnStatusChanged +=
            HandlePlayerChanged;

        player.BankruptcyChanged +=
            HandlePlayerChanged;

        player.ParticipationChanged +=
            HandlePlayerChanged;

        player.IdentityChanged +=
            HandlePlayerChanged;

        player.OnlineControlStateChanged +=
            HandlePlayerChanged;

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (player == null ||
            !subscribed)
        {
            return;
        }

        player.MoneyChanged -=
            HandlePlayerChanged;

        player.TurnStatusChanged -=
            HandlePlayerChanged;

        player.BankruptcyChanged -=
            HandlePlayerChanged;

        player.ParticipationChanged -=
            HandlePlayerChanged;

        player.IdentityChanged -=
            HandlePlayerChanged;

        player.OnlineControlStateChanged -=
            HandlePlayerChanged;

        subscribed = false;
    }

    private void HandlePlayerChanged(
        PlayerGameState changedPlayer)
    {
        Refresh(lastIsCurrentTurn);
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        RectTransform newPanelRect,
        Image newBackgroundImage,
        Image newAccentImage,
        Image newIconImage,
        TMP_Text newIconText,
        TMP_Text newPlayerNameText,
        TMP_Text newMoneyText,
        TMP_Text newControlTypeText,
        GameObject newTurnBadgeRoot,
        Image newTurnBadgeImage,
        TMP_Text newTurnBadgeText,
        Outline newPanelOutline)
    {
        panelRect = newPanelRect;
        backgroundImage = newBackgroundImage;
        accentImage = newAccentImage;
        iconImage = newIconImage;
        iconText = newIconText;
        playerNameText = newPlayerNameText;
        moneyText = newMoneyText;
        controlTypeText = newControlTypeText;
        turnBadgeRoot = newTurnBadgeRoot;
        turnBadgeImage = newTurnBadgeImage;
        turnBadgeText = newTurnBadgeText;
        panelOutline = newPanelOutline;
    }
#endif
}
