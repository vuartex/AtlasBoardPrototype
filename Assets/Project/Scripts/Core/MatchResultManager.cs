using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MatchResultManager : MonoBehaviour
{
    [Serializable]
    public sealed class OnlineResultSnapshot
    {
        public bool valid;
        public int highestNetWorth;
        public int[] winnerSlots = Array.Empty<int>();
        public bool[] participating = new bool[4];
        public bool[] bankrupt = new bool[4];
        public int[] cash = new int[4];
        public int[] propertyCount = new int[4];
        public int[] propertyValue = new int[4];
        public int[] developmentLevels = new int[4];
        public int[] developmentValue = new int[4];
        public int[] netWorth = new int[4];
    }

    [Header("Players")]
    [SerializeField]
    private PlayerGameState[] playerStates;

    [Header("Board")]
    [SerializeField]
    private BoardPath boardPath;

    [SerializeField]
    private PropertyDevelopmentManager propertyDevelopmentManager;

    [Header("Result UI")]
    [SerializeField]
    private GameObject resultPanel;

    [SerializeField]
    private TMP_Text resultTitleText;

    [SerializeField]
    private TMP_Text resultSummaryText;

    private bool resultShown;
    private bool onlineRematchInFlight;
    private bool onlineResultPresentationActive;
    private bool onlineResultLocalIsHost;

    public bool ResultShown =>
        resultShown;

    private void Start()
    {
        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
    }

    public void ShowMatchResult()
    {
        if (resultShown)
        {
            return;
        }

        OnlineResultSnapshot snapshot =
            BuildOnlineResultSnapshot();

        if (!snapshot.valid)
        {
            Debug.LogError(
                "No player states are configured for match results.",
                this);
            return;
        }

        ShowOnlineMatchResult(
            snapshot,
            IsLocalOnlineHost());
    }

    public OnlineResultSnapshot BuildOnlineResultSnapshot()
    {
        OnlineResultSnapshot snapshot =
            new OnlineResultSnapshot();

        if (playerStates == null ||
            playerStates.Length == 0)
        {
            return snapshot;
        }

        List<int> activeSlots =
            new List<int>();
        List<int> winnerSlots =
            new List<int>();
        int highestNetWorth = int.MinValue;

        for (int index = 0;
             index < playerStates.Length;
             index++)
        {
            PlayerGameState player =
                playerStates[index];

            if (player == null ||
                !player.IsParticipating)
            {
                continue;
            }

            int slot =
                Mathf.Clamp(
                    player.PlayerSlotIndex,
                    0,
                    3);

            int propertyCount;
            int propertyValue =
                CalculatePropertyValue(
                    slot,
                    out propertyCount);
            int developmentValue =
                propertyDevelopmentManager != null
                    ? propertyDevelopmentManager
                        .GetDevelopmentInvestmentValue(slot)
                    : 0;
            int developmentLevels =
                propertyDevelopmentManager != null
                    ? propertyDevelopmentManager
                        .GetTotalDevelopmentLevels(slot)
                    : 0;
            int netWorth =
                player.CurrentMoney +
                propertyValue +
                developmentValue;

            snapshot.participating[slot] = true;
            snapshot.bankrupt[slot] = player.IsBankrupt;
            snapshot.cash[slot] = player.CurrentMoney;
            snapshot.propertyCount[slot] = propertyCount;
            snapshot.propertyValue[slot] = propertyValue;
            snapshot.developmentLevels[slot] = developmentLevels;
            snapshot.developmentValue[slot] = developmentValue;
            snapshot.netWorth[slot] = netWorth;

            if (player.IsBankrupt)
            {
                continue;
            }

            activeSlots.Add(slot);

            if (netWorth > highestNetWorth)
            {
                highestNetWorth = netWorth;
                winnerSlots.Clear();
                winnerSlots.Add(slot);
            }
            else if (netWorth == highestNetWorth)
            {
                winnerSlots.Add(slot);
            }
        }

        if (activeSlots.Count == 1)
        {
            winnerSlots.Clear();
            winnerSlots.Add(activeSlots[0]);
            highestNetWorth =
                snapshot.netWorth[activeSlots[0]];
        }

        if (highestNetWorth == int.MinValue)
        {
            highestNetWorth = 0;
        }

        snapshot.highestNetWorth = highestNetWorth;
        snapshot.winnerSlots = winnerSlots.ToArray();
        snapshot.valid = true;
        return snapshot;
    }

    public void ShowOnlineMatchResult(
        OnlineResultSnapshot snapshot,
        bool localIsHost)
    {
        if (snapshot == null ||
            !snapshot.valid)
        {
            return;
        }

        resultShown = true;
        onlineResultPresentationActive = true;
        onlineResultLocalIsHost = localIsHost;

        UpdateResultTitleFromSlots(
            snapshot.winnerSlots,
            snapshot.highestNetWorth);

        if (resultSummaryText != null)
        {
            StringBuilder summaryBuilder =
                new StringBuilder();

            for (int slot = 0; slot < 4; slot++)
            {
                if (snapshot.participating == null ||
                    slot >= snapshot.participating.Length ||
                    !snapshot.participating[slot])
                {
                    continue;
                }

                if (summaryBuilder.Length > 0)
                {
                    summaryBuilder.AppendLine();
                    summaryBuilder.AppendLine();
                }

                PlayerGameState player =
                    GetPlayerByStableSlot(slot);
                string playerName =
                    player != null
                        ? AtlasBoardL.PlayerName(player)
                        : $"Player {slot + 1}";
                bool bankrupt =
                    snapshot.bankrupt != null &&
                    slot < snapshot.bankrupt.Length &&
                    snapshot.bankrupt[slot];

                summaryBuilder.AppendLine(
                    bankrupt
                        ? AtlasBoardL.T(
                            "match.player_bankrupt",
                            playerName)
                        : playerName);
                summaryBuilder.Append(
                    AtlasBoardL.T(
                        "match.cash_properties",
                        GetArrayValue(snapshot.cash, slot),
                        GetArrayValue(snapshot.propertyCount, slot),
                        GetArrayValue(snapshot.propertyValue, slot)));
                summaryBuilder.AppendLine();
                summaryBuilder.Append(
                    AtlasBoardL.T(
                        "match.development",
                        GetArrayValue(snapshot.developmentLevels, slot),
                        GetArrayValue(snapshot.developmentValue, slot)));
                summaryBuilder.AppendLine();
                summaryBuilder.Append(
                    AtlasBoardL.T(
                        "match.net_worth",
                        GetArrayValue(snapshot.netWorth, slot)));
            }

            if (!localIsHost)
            {
                summaryBuilder.Insert(
                    0,
                    GetRemoteWaitingText() +
                    System.Environment.NewLine +
                    System.Environment.NewLine);
            }

            resultSummaryText.text =
                summaryBuilder.ToString();
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        ConfigureOnlineResultActionButton(localIsHost);

        Debug.Log(
            $"Match completed. Highest net worth: " +
            $"{snapshot.highestNetWorth}.",
            this);
    }

    public void RestartMatch()
    {
        if (onlineRematchInFlight)
        {
            return;
        }

        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            FindAnyObjectByType<
                AtlasBoardTurnDiceNetworkCoordinator>();

        if (coordinator != null &&
            coordinator.IsPreparedOnlineMatch)
        {
            if (coordinator.LocalIsHost)
            {
                SetOnlineRematchBusy(true);
                coordinator.RequestOnlineRematch();
            }

            return;
        }

        Scene activeScene =
            SceneManager.GetActiveScene();

        if (activeScene.buildIndex < 0)
        {
            Debug.LogError(
                "The active scene is not included " +
                "in the Build Profile scene list.",
                this);

            return;
        }

        SceneManager.LoadScene(
            activeScene.buildIndex,
            LoadSceneMode.Single);
    }

    private int CalculatePropertyValue(
        int playerSlotIndex,
        out int propertyCount)
    {
        propertyCount = 0;

        if (boardPath == null)
        {
            boardPath =
                FindAnyObjectByType<
                    BoardPath>();
        }

        if (boardPath == null)
        {
            return 0;
        }

        int totalPropertyValue = 0;

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile == null ||
                !tile.IsOwned ||
                tile.OwnerPlayerIndex !=
                playerSlotIndex)
            {
                continue;
            }

            propertyCount++;

            totalPropertyValue +=
                tile.PurchasePrice;
        }

        return totalPropertyValue;
    }

    private void UpdateResultTitleFromSlots(
        int[] winnerSlots,
        int highestNetWorth)
    {
        if (resultTitleText == null)
        {
            return;
        }

        int winnerCount =
            winnerSlots != null
                ? winnerSlots.Length
                : 0;

        if (winnerCount == 1)
        {
            PlayerGameState winner =
                GetPlayerByStableSlot(
                    winnerSlots[0]);

            resultTitleText.text =
                AtlasBoardL.T(
                    "match.winner",
                    winner != null
                        ? AtlasBoardL.PlayerName(winner)
                        : $"Player {winnerSlots[0] + 1}",
                    highestNetWorth);
        }
        else if (winnerCount > 1)
        {
            resultTitleText.text =
                AtlasBoardL.T(
                    "match.tie",
                    highestNetWorth);
        }
        else
        {
            resultTitleText.text =
                AtlasBoardL.T(
                    "match.complete");
        }
    }

    private PlayerGameState GetPlayerByStableSlot(
        int slotIndex)
    {
        if (playerStates == null)
        {
            return null;
        }

        foreach (PlayerGameState player in playerStates)
        {
            if (player != null &&
                player.PlayerSlotIndex == slotIndex)
            {
                return player;
            }
        }

        return null;
    }

    private static int GetArrayValue(
        int[] values,
        int index)
    {
        return values != null &&
               index >= 0 &&
               index < values.Length
            ? values[index]
            : 0;
    }

    private bool IsLocalOnlineHost()
    {
        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            FindAnyObjectByType<
                AtlasBoardTurnDiceNetworkCoordinator>();

        return coordinator == null ||
               !coordinator.IsPreparedOnlineMatch ||
               coordinator.LocalIsHost;
    }

    private void ConfigureOnlineResultActionButton(
        bool localIsHost)
    {
        Button actionButton =
            FindOnlineResultActionButton();

        if (actionButton == null)
        {
            return;
        }

        AtlasBoardTurnDiceNetworkCoordinator coordinator =
            FindAnyObjectByType<AtlasBoardTurnDiceNetworkCoordinator>();

        bool online =
            coordinator != null &&
            (coordinator.IsPreparedOnlineMatch || !localIsHost);

        if (!online)
        {
            return;
        }

        actionButton.onClick =
            new Button.ButtonClickedEvent();
        actionButton.interactable = true;

        if (localIsHost)
        {
            actionButton.onClick.AddListener(RestartMatch);
            SetResultActionButtonText(
                actionButton,
                GetHostRematchText());
        }
        else
        {
            actionButton.onClick.AddListener(
                LeaveOnlineMatchFromResult);
            SetResultActionButtonText(
                actionButton,
                GetLeaveMatchText());
        }
    }

    private Button FindOnlineResultActionButton()
    {
        if (resultPanel == null)
        {
            return null;
        }

        Button[] buttons =
            resultPanel.GetComponentsInChildren<Button>(true);

        if (buttons == null || buttons.Length == 0)
        {
            return null;
        }

        Button best = null;
        int bestScore = int.MinValue;

        foreach (Button button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            string buttonName =
                button.name.ToLowerInvariant();
            TMP_Text tmp =
                button.GetComponentInChildren<TMP_Text>(true);
            UnityEngine.UI.Text legacy =
                button.GetComponentInChildren<UnityEngine.UI.Text>(true);
            string label =
                (tmp != null ? tmp.text : legacy != null ? legacy.text : string.Empty)
                    .ToLowerInvariant();

            int score = 0;
            if (buttonName.Contains("restart") ||
                buttonName.Contains("rematch") ||
                buttonName.Contains("result") ||
                buttonName.Contains("action")) score += 10;
            if (label.Contains("restart") ||
                label.Contains("rematch") ||
                label.Contains("yeniden") ||
                label.Contains("başlat")) score += 20;

            if (score > bestScore)
            {
                bestScore = score;
                best = button;
            }
        }

        return best ?? buttons[0];
    }

    private static void SetResultActionButtonText(
        Button button,
        string text)
    {
        if (button == null)
        {
            return;
        }

        AtlasBoardLocalizedText[] localized =
            button.GetComponentsInChildren<AtlasBoardLocalizedText>(true);
        foreach (AtlasBoardLocalizedText item in localized)
        {
            if (item != null) item.enabled = false;
        }

        TMP_Text[] tmpLabels =
            button.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text label in tmpLabels)
        {
            if (label != null) label.text = text;
        }

        UnityEngine.UI.Text[] legacyLabels =
            button.GetComponentsInChildren<UnityEngine.UI.Text>(true);
        foreach (UnityEngine.UI.Text label in legacyLabels)
        {
            if (label != null) label.text = text;
        }
    }

    private void LeaveOnlineMatchFromResult()
    {
        AtlasBoardLeaveFlowController leaveFlow =
            FindAnyObjectByType<
                AtlasBoardLeaveFlowController>();

        if (leaveFlow != null)
        {
            leaveFlow.ShowLeaveMatchConfirmation();
        }
    }

    private void LateUpdate()
    {
        if (!onlineResultPresentationActive ||
            resultPanel == null ||
            !resultPanel.activeInHierarchy)
        {
            return;
        }

        Button actionButton = FindOnlineResultActionButton();
        if (actionButton == null)
        {
            return;
        }

        SetResultActionButtonText(
            actionButton,
            onlineRematchInFlight && onlineResultLocalIsHost
                ? AtlasBoardOnlineRuntimeText.Rematching() + "..."
                : onlineResultLocalIsHost
                    ? GetHostRematchText()
                    : GetLeaveMatchText());
    }

    public void ResetForNewMatchSession()
    {
        resultShown = false;
        onlineRematchInFlight = false;
        onlineResultPresentationActive = false;
        onlineResultLocalIsHost = false;

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }
    }

    public void NotifyOnlineRematchRequestFailed()
    {
        SetOnlineRematchBusy(false);
        Debug.LogWarning(
            AtlasBoardOnlineRuntimeText.RematchFailed(),
            this);
    }

    private void SetOnlineRematchBusy(
        bool busy)
    {
        onlineRematchInFlight = busy;

        if (resultPanel == null)
        {
            return;
        }

        Button actionButton =
            FindOnlineResultActionButton();

        if (actionButton == null)
        {
            return;
        }
        actionButton.interactable = !busy;
        SetResultActionButtonText(
            actionButton,
            busy
                ? AtlasBoardOnlineRuntimeText.Rematching() + "..."
                : AtlasBoardOnlineRuntimeText.Rematch());
    }

    private static string GetRemoteWaitingText()
    {
        return AtlasBoardOnlineRuntimeText.WaitingForHost();
    }

    private static string GetLeaveMatchText()
    {
        return AtlasBoardOnlineRuntimeText.LeaveMatch();
    }

    private static string GetHostRematchText()
    {
        return AtlasBoardOnlineRuntimeText.Rematch();
    }

}
