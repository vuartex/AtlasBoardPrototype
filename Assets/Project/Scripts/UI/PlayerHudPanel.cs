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

    public RectTransform PanelRect =>
        panelRect != null
            ? panelRect
            : transform as RectTransform;

    public PlayerGameState Player =>
        player;

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
                player.DisplayName;
        }

        if (moneyText != null)
        {
            moneyText.text =
                player.IsBankrupt
                    ? "İFLAS"
                    : $"{player.CurrentMoney} ₵";
        }

        if (controlTypeText != null)
        {
            bool isBot =
                botController != null &&
                botController.BotEnabled;

            if (!isBot)
            {
                controlTypeText.text =
                    "İNSAN";
            }
            else
            {
                string personality =
                    botController
                        .PersonalityProfile != null
                        ? botController
                            .PersonalityProfile
                            .DisplayName
                        : "BOT";

                controlTypeText.text =
                    $"BOT • {personality}";
            }
        }

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
                "SIRA";
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

    private void OnDestroy()
    {
        Unsubscribe();
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
