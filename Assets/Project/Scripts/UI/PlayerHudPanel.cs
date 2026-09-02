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
            if (player.IsOnlineTemporaryBot)
            {
                controlTypeText.text =
                    GetTemporaryBotLabel();
            }
            else if (player.IsOnlinePermanentBot)
            {
                controlTypeText.text =
                    GetPermanentBotLabel();
            }
            else
            {
                bool isBot =
                    player.OnlineSeatStateActive
                        ? player.IsOnlineBotControlled
                        : botController != null &&
                          botController.BotEnabled;

                if (!isBot)
                {
                    controlTypeText.text =
                        AtlasBoardL.T(
                            "common.human")
                            .ToUpperInvariant();
                }
                else
                {
                    string personality =
                        botController
                            .PersonalityProfile != null
                            ? LocalizePersonality(
                                botController
                                    .PersonalityProfile
                                    .DisplayName)
                            : AtlasBoardL.T(
                                "common.bot");

                    controlTypeText.text =
                        $"{AtlasBoardL.T("common.bot").ToUpperInvariant()} • " +
                        $"{personality}";
                }
            }
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
            // Keep TURN/SIRA inside the HUD card but pin it to the upper-right
            // corner so it no longer covers long account display names.
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.anchoredPosition =
                new Vector2(-4f, -2f);

            // The previous top-right pin still allowed a long account name to
            // render underneath the badge. Reserve a dedicated right-side lane
            // for TURN/SIRA while keeping both elements inside the HUD card.
            if (badgeRect.sizeDelta.x > 56f)
            {
                badgeRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    56f);
            }
        }

        if (playerNameText != null)
        {
            RectTransform nameRect =
                playerNameText.rectTransform;

            if (nameRect != null)
            {
                Vector2 offsetMax = nameRect.offsetMax;
                offsetMax.x = Mathf.Min(offsetMax.x, -64f);
                nameRect.offsetMax = offsetMax;
            }
        }
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
        Refresh(false);
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
