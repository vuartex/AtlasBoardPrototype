using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MatchResultManager : MonoBehaviour
{
    [Header("Players")]
    [SerializeField]
    private PlayerGameState[] playerStates;

    [Header("Board")]
    [SerializeField]
    private BoardPath boardPath;

    [Header("Result UI")]
    [SerializeField]
    private GameObject resultPanel;

    [SerializeField]
    private TMP_Text resultTitleText;

    [SerializeField]
    private TMP_Text resultSummaryText;

    private bool resultShown;

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

        resultShown = true;

        if (playerStates == null ||
            playerStates.Length == 0)
        {
            Debug.LogError(
                "No player states are configured " +
                "for match results.",
                this);

            return;
        }

        int highestNetWorth = int.MinValue;

        List<int> winnerIndexes =
            new List<int>();

        StringBuilder summaryBuilder =
            new StringBuilder();

        for (int index = 0;
             index < playerStates.Length;
             index++)
        {
            PlayerGameState player =
                playerStates[index];

            if (player == null)
            {
                continue;
            }

            int propertyCount;
            int propertyValue =
                CalculatePropertyValue(
                    player.PlayerIndex,
                    out propertyCount);

            int netWorth =
                player.CurrentMoney +
                propertyValue;

            if (netWorth > highestNetWorth)
            {
                highestNetWorth = netWorth;

                winnerIndexes.Clear();
                winnerIndexes.Add(index);
            }
            else if (netWorth == highestNetWorth)
            {
                winnerIndexes.Add(index);
            }

            if (summaryBuilder.Length > 0)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine();
            }

            summaryBuilder.AppendLine(
                player.DisplayName);

            summaryBuilder.Append(
                $"Nakit: {player.CurrentMoney} ₵ | " +
                $"Mülk: {propertyCount} | " +
                $"Mülk Değeri: {propertyValue} ₵");

            summaryBuilder.AppendLine();

            summaryBuilder.Append(
                $"Net Servet: {netWorth} ₵");
        }

        UpdateResultTitle(
            winnerIndexes,
            highestNetWorth);

        if (resultSummaryText != null)
        {
            resultSummaryText.text =
                summaryBuilder.ToString();
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        Debug.Log(
            $"Match completed. Highest net worth: " +
            $"{highestNetWorth}.",
            this);
    }

    public void RestartMatch()
    {
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
        int playerIndex,
        out int propertyCount)
    {
        propertyCount = 0;

        if (boardPath == null)
        {
            boardPath =
                FindFirstObjectByType<BoardPath>();
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
                tile.OwnerPlayerIndex != playerIndex)
            {
                continue;
            }

            propertyCount++;
            totalPropertyValue +=
                tile.PurchasePrice;
        }

        return totalPropertyValue;
    }

    private void UpdateResultTitle(
        List<int> winnerIndexes,
        int highestNetWorth)
    {
        if (resultTitleText == null)
        {
            return;
        }

        if (winnerIndexes.Count == 1)
        {
            PlayerGameState winner =
                playerStates[winnerIndexes[0]];

            resultTitleText.text =
                $"{winner.DisplayName} Kazandı!\n" +
                $"{highestNetWorth} ₵";
        }
        else
        {
            resultTitleText.text =
                $"Beraberlik!\n" +
                $"{highestNetWorth} ₵";
        }
    }
}