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

        List<int> activePlayerIndexes =
            new List<int>();

        for (int index = 0;
             index < playerStates.Length;
             index++)
        {
            PlayerGameState player =
                playerStates[index];

            if (player != null &&
                player.IsParticipating &&
                !player.IsBankrupt)
            {
                activePlayerIndexes.Add(index);
            }
        }

        int highestNetWorth =
            int.MinValue;

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

            if (player == null ||
                !player.IsParticipating)
            {
                continue;
            }

            int propertyCount;

            int propertyValue =
                CalculatePropertyValue(
                    player.PlayerSlotIndex,
                    out propertyCount);

            int developmentValue =
                propertyDevelopmentManager != null
                    ? propertyDevelopmentManager
                        .GetDevelopmentInvestmentValue(
                            player.PlayerSlotIndex)
                    : 0;

            int developmentLevels =
                propertyDevelopmentManager != null
                    ? propertyDevelopmentManager
                        .GetTotalDevelopmentLevels(
                            player.PlayerSlotIndex)
                    : 0;

            int netWorth =
                player.CurrentMoney +
                propertyValue +
                developmentValue;

            if (!player.IsBankrupt)
            {
                if (netWorth >
                    highestNetWorth)
                {
                    highestNetWorth =
                        netWorth;

                    winnerIndexes.Clear();
                    winnerIndexes.Add(index);
                }
                else if (netWorth ==
                         highestNetWorth)
                {
                    winnerIndexes.Add(index);
                }
            }

            if (summaryBuilder.Length > 0)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine();
            }

            summaryBuilder.AppendLine(
                player.IsBankrupt
                    ? $"{player.DisplayName} — İFLAS"
                    : player.DisplayName);

            summaryBuilder.Append(
                $"Nakit: {player.CurrentMoney} ₵ | " +
                $"Mülk: {propertyCount} | " +
                $"Mülk Değeri: {propertyValue} ₵");

            summaryBuilder.AppendLine();

            summaryBuilder.Append(
                $"Geliştirme Seviyesi: " +
                $"{developmentLevels} | " +
                $"Geliştirme Değeri: " +
                $"{developmentValue} ₵");

            summaryBuilder.AppendLine();

            summaryBuilder.Append(
                $"Net Servet: {netWorth} ₵");
        }

        if (activePlayerIndexes.Count == 1)
        {
            winnerIndexes.Clear();

            winnerIndexes.Add(
                activePlayerIndexes[0]);

            PlayerGameState soleWinner =
                playerStates[
                    activePlayerIndexes[0]];

            int solePropertyCount;

            int solePropertyValue =
                CalculatePropertyValue(
                    soleWinner.PlayerSlotIndex,
                    out solePropertyCount);

            int soleDevelopmentValue =
                propertyDevelopmentManager != null
                    ? propertyDevelopmentManager
                        .GetDevelopmentInvestmentValue(
                            soleWinner.PlayerSlotIndex)
                    : 0;

            highestNetWorth =
                soleWinner.CurrentMoney +
                solePropertyValue +
                soleDevelopmentValue;
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
            $"{highestNetWorth}. Active players: " +
            $"{activePlayerIndexes.Count}.",
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
                playerStates[
                    winnerIndexes[0]];

            resultTitleText.text =
                $"{winner.DisplayName} Kazandı!\n" +
                $"{highestNetWorth} ₵";
        }
        else if (winnerIndexes.Count > 1)
        {
            resultTitleText.text =
                $"Beraberlik!\n" +
                $"{highestNetWorth} ₵";
        }
        else
        {
            resultTitleText.text =
                "Maç Tamamlandı";
        }
    }
}
