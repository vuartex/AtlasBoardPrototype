using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class UXOverlayController : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private PlayerGameState[] players;

    [Header("HUD")]
    [SerializeField]
    private PlayerHudPanel[] playerHudPanels;

    [Header("Tablet / Modal Visibility")]
    [SerializeField]
    private GameObject tabletRoot;

    [Header("Status")]
    [SerializeField]
    private RectTransform statusBarRect;

    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private TMP_Text shortcutHintText;

    [SerializeField]
    private UXKeyboardShortcutController
        shortcutController;

    [Header("Legacy Board Text")]
    [SerializeField]
    private TMP_Text legacyTurnStatusText;

    [SerializeField]
    private TMP_Text legacyBalancesText;

    [SerializeField]
    private bool hideLegacyTurnStatus = true;

    [SerializeField]
    private bool hideLegacyBalances = true;

    [Header("Layout")]
    [SerializeField]
    private Vector2 cornerMargin =
        new Vector2(28f, 28f);

    [SerializeField]
    private float threePlayerTopOffset = 28f;

    private int lastParticipatingCount = -1;
    private int lastHighlightedSlot = int.MinValue;
    private string lastStatusSource = string.Empty;
    private string lastHint = string.Empty;

    private void Start()
    {
        HideLegacyBoardText();
        BindPlayers();
        RefreshAll(force: true);
    }

    private void LateUpdate()
    {
        HideLegacyBoardText();

        bool matchVisible =
            turnManager != null &&
            turnManager.IsMatchStarted;

        bool tabletVisible =
            tabletRoot != null &&
            tabletRoot.activeInHierarchy;

        bool gameplayOverlayVisible =
            matchVisible &&
            !tabletVisible;

        SetOverlayChildrenVisible(
            gameplayOverlayVisible);

        if (!gameplayOverlayVisible)
        {
            return;
        }

        RefreshAll(force: false);
    }

    private void BindPlayers()
    {
        if (players == null ||
            playerHudPanels == null)
        {
            return;
        }

        PlayerGameState[] orderedPlayers =
            players
                .Where(player => player != null)
                .OrderBy(
                    player =>
                        player.PlayerSlotIndex)
                .ToArray();

        int bindCount =
            Mathf.Min(
                orderedPlayers.Length,
                playerHudPanels.Length);

        for (int index = 0;
             index < bindCount;
             index++)
        {
            if (playerHudPanels[index] != null)
            {
                playerHudPanels[index]
                    .Bind(
                        orderedPlayers[index]);
            }
        }

        for (int index = bindCount;
             index < playerHudPanels.Length;
             index++)
        {
            if (playerHudPanels[index] != null)
            {
                playerHudPanels[index]
                    .gameObject
                    .SetActive(false);
            }
        }
    }

    private void RefreshAll(
        bool force)
    {
        List<PlayerGameState>
            participatingPlayers =
                GetParticipatingPlayers();

        int participatingCount =
            participatingPlayers.Count;

        if (force ||
            participatingCount !=
                lastParticipatingCount)
        {
            ApplyPlayerLayout(
                participatingPlayers);

            lastParticipatingCount =
                participatingCount;
        }

        PlayerGameState highlightedPlayer =
            turnManager != null
                ? turnManager
                      .StartingOrderPlayerState ??
                  turnManager
                      .CurrentPlayerState
                : null;

        int highlightedSlot =
            highlightedPlayer != null
                ? highlightedPlayer
                    .PlayerSlotIndex
                : -1;

        if (force ||
            highlightedSlot !=
                lastHighlightedSlot)
        {
            lastHighlightedSlot =
                highlightedSlot;
        }

        foreach (PlayerHudPanel panel
                 in playerHudPanels)
        {
            if (panel == null ||
                panel.Player == null)
            {
                continue;
            }

            panel.Refresh(
                panel.Player.PlayerSlotIndex ==
                highlightedSlot);
        }

        RefreshStatus(
            participatingCount,
            force);
    }

    private List<PlayerGameState>
        GetParticipatingPlayers()
    {
        if (players == null)
        {
            return new List<
                PlayerGameState>();
        }

        return players
            .Where(
                player =>
                    player != null &&
                    player.IsParticipating)
            .OrderBy(
                player =>
                    player.PlayerSlotIndex)
            .ToList();
    }

    private void ApplyPlayerLayout(
        List<PlayerGameState>
            participatingPlayers)
    {
        if (playerHudPanels == null)
        {
            return;
        }

        foreach (PlayerHudPanel panel
                 in playerHudPanels)
        {
            if (panel != null)
            {
                panel.gameObject
                    .SetActive(false);
            }
        }

        int count =
            participatingPlayers.Count;

        for (int index = 0;
             index < count;
             index++)
        {
            PlayerGameState player =
                participatingPlayers[index];

            PlayerHudPanel panel =
                FindPanelForPlayer(player);

            if (panel == null)
            {
                continue;
            }

            panel.gameObject
                .SetActive(true);

            ApplyLayoutSlot(
                panel.PanelRect,
                GetLayoutSlot(
                    count,
                    index));
        }

        if (statusBarRect != null)
        {
            Vector2 position =
                statusBarRect
                    .anchoredPosition;

            position.y =
                -28f;

            statusBarRect
                .anchoredPosition =
                    position;
        }
    }

    private PlayerHudPanel
        FindPanelForPlayer(
            PlayerGameState player)
    {
        if (player == null ||
            playerHudPanels == null)
        {
            return null;
        }

        foreach (PlayerHudPanel panel
                 in playerHudPanels)
        {
            if (panel != null &&
                panel.Player == player)
            {
                return panel;
            }
        }

        return null;
    }

    private enum LayoutSlot
    {
        BottomLeft,
        BottomRight,
        TopLeft,
        TopRight,
        TopCenter
    }

    private LayoutSlot GetLayoutSlot(
        int playerCount,
        int index)
    {
        if (playerCount <= 2)
        {
            return index == 0
                ? LayoutSlot.BottomLeft
                : LayoutSlot.BottomRight;
        }

        if (playerCount == 3)
        {
            return index switch
            {
                0 => LayoutSlot.BottomLeft,
                1 => LayoutSlot.BottomRight,
                _ => LayoutSlot.TopLeft
            };
        }

        return index switch
        {
            0 => LayoutSlot.BottomLeft,
            1 => LayoutSlot.BottomRight,
            2 => LayoutSlot.TopLeft,
            _ => LayoutSlot.TopRight
        };
    }

    private void ApplyLayoutSlot(
        RectTransform rect,
        LayoutSlot slot)
    {
        if (rect == null)
        {
            return;
        }

        switch (slot)
        {
            case LayoutSlot.BottomLeft:
                SetAnchor(
                    rect,
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(
                        cornerMargin.x,
                        cornerMargin.y));
                break;

            case LayoutSlot.BottomRight:
                SetAnchor(
                    rect,
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(
                        -cornerMargin.x,
                        cornerMargin.y));
                break;

            case LayoutSlot.TopLeft:
                SetAnchor(
                    rect,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(
                        cornerMargin.x,
                        -cornerMargin.y));
                break;

            case LayoutSlot.TopRight:
                SetAnchor(
                    rect,
                    new Vector2(1f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(
                        -cornerMargin.x,
                        -cornerMargin.y));
                break;

            case LayoutSlot.TopCenter:
                SetAnchor(
                    rect,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(
                        0f,
                        -threePlayerTopOffset));
                break;
        }
    }

    private static void SetAnchor(
        RectTransform rect,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPosition)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition =
            anchoredPosition;
    }

    private void RefreshStatus(
        int participatingCount,
        bool force)
    {
        string source =
            legacyTurnStatusText != null
                ? legacyTurnStatusText.text
                : string.Empty;

        source =
            CompactStatus(source);

        if (string.IsNullOrWhiteSpace(
                source) &&
            turnManager != null &&
            turnManager.CurrentPlayerState != null)
        {
            source =
                $"{turnManager.CurrentPlayerState.DisplayName} oynuyor";
        }

        if (force ||
            !string.Equals(
                source,
                lastStatusSource,
                System.StringComparison.Ordinal))
        {
            lastStatusSource =
                source;

            if (statusText != null)
            {
                statusText.text =
                    source;
            }
        }

        string hint =
            shortcutController != null
                ? shortcutController
                    .CurrentHint
                : string.Empty;

        if (force ||
            !string.Equals(
                hint,
                lastHint,
                System.StringComparison.Ordinal))
        {
            lastHint = hint;

            if (shortcutHintText != null)
            {
                shortcutHintText.text =
                    hint;

                shortcutHintText
                    .gameObject
                    .SetActive(
                        !string.IsNullOrWhiteSpace(
                            hint));
            }
        }
    }

    private string CompactStatus(
        string source)
    {
        if (string.IsNullOrWhiteSpace(
                source))
        {
            return string.Empty;
        }

        string compact =
            source
                .Replace(
                    "\r\n",
                    "  •  ")
                .Replace(
                    "\n",
                    "  •  ")
                .Trim();

        while (compact.Contains(
                   "    "))
        {
            compact =
                compact.Replace(
                    "    ",
                    "  ");
        }

        const int maxLength = 110;

        if (compact.Length >
            maxLength)
        {
            compact =
                compact.Substring(
                    0,
                    maxLength - 1) +
                "…";
        }

        return compact;
    }

    private void HideLegacyBoardText()
    {
        if (hideLegacyTurnStatus &&
            legacyTurnStatusText != null &&
            legacyTurnStatusText
                .gameObject
                .activeSelf)
        {
            legacyTurnStatusText
                .gameObject
                .SetActive(false);
        }

        if (hideLegacyBalances &&
            legacyBalancesText != null &&
            legacyBalancesText
                .gameObject
                .activeSelf)
        {
            legacyBalancesText
                .gameObject
                .SetActive(false);
        }
    }

    private void SetOverlayChildrenVisible(
        bool visible)
    {
        if (playerHudPanels != null)
        {
            foreach (PlayerHudPanel panel
                     in playerHudPanels)
            {
                if (panel == null)
                {
                    continue;
                }

                bool shouldShow =
                    visible &&
                    panel.Player != null &&
                    panel.Player
                        .IsParticipating;

                if (!shouldShow &&
                    panel.gameObject
                        .activeSelf)
                {
                    panel.gameObject
                        .SetActive(false);
                }
            }
        }

        if (statusBarRect != null)
        {
            GameObject statusObject =
                statusBarRect.gameObject;

            if (statusObject.activeSelf !=
                visible)
            {
                statusObject.SetActive(
                    visible);
            }
        }
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        TurnManager newTurnManager,
        PlayerGameState[] newPlayers,
        PlayerHudPanel[] newPlayerHudPanels,
        RectTransform newStatusBarRect,
        TMP_Text newStatusText,
        TMP_Text newShortcutHintText,
        UXKeyboardShortcutController
            newShortcutController,
        GameObject newTabletRoot,
        TMP_Text newLegacyTurnStatusText,
        TMP_Text newLegacyBalancesText)
    {
        turnManager = newTurnManager;
        players = newPlayers;
        playerHudPanels =
            newPlayerHudPanels;
        statusBarRect =
            newStatusBarRect;
        statusText = newStatusText;
        shortcutHintText =
            newShortcutHintText;
        shortcutController =
            newShortcutController;
        tabletRoot =
            newTabletRoot;
        legacyTurnStatusText =
            newLegacyTurnStatusText;
        legacyBalancesText =
            newLegacyBalancesText;
    }
#endif
}
