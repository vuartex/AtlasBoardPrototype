using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum MatchPlayerControlType
{
    Human = 0,
    Bot = 1
}

public class MatchSetupManager : MonoBehaviour
{
    [Header("Match")]
    [SerializeField]
    private TurnManager turnManager;

    [Header("Players")]
    [SerializeField]
    private PlayerPawnMover[] players;

    [SerializeField]
    private MatchPlayerControlType[]
        defaultControlTypes;

    [Header("Setup UI")]
    [SerializeField]
    private GameObject setupPanel;

    [SerializeField]
    private TMP_Dropdown[]
        controlTypeDropdowns;

    [SerializeField]
    private Button startGameButton;

    [Header("Gameplay UI")]
    [SerializeField]
    private GameObject boardControlsRoot;

    private bool matchLaunchRequested;

    private void Awake()
    {
        if (setupPanel != null)
        {
            setupPanel.SetActive(true);
        }

        if (boardControlsRoot != null)
        {
            boardControlsRoot.SetActive(false);
        }

        ConfigureDropdowns();

        if (startGameButton != null)
        {
            startGameButton.interactable =
                ValidateSetup(
                    logErrors: false);
        }
    }

    public void StartConfiguredMatch()
    {
        if (matchLaunchRequested)
        {
            return;
        }

        if (!ValidateSetup(
                logErrors: true))
        {
            return;
        }

        matchLaunchRequested = true;

        for (int index = 0;
             index < players.Length;
             index++)
        {
            PlayerGameState playerState =
                players[index]
                    .GetComponent<
                        PlayerGameState>();

            BotPlayerController botController =
                players[index]
                    .GetComponent<
                        BotPlayerController>();

            bool shouldBeBot =
                controlTypeDropdowns[index]
                    .value ==
                (int)MatchPlayerControlType.Bot;

            botController.SetBotEnabled(
                shouldBeBot);

            Debug.Log(
                $"Match setup — " +
                $"{playerState.DisplayName} " +
                $"[Slot {playerState.PlayerSlotIndex}] = " +
                $"{(shouldBeBot ? "BOT" : "HUMAN")}.",
                players[index]);
        }

        if (boardControlsRoot != null)
        {
            boardControlsRoot.SetActive(true);
        }

        bool matchStarted =
            turnManager.BeginMatch();

        if (!matchStarted)
        {
            matchLaunchRequested = false;

            if (boardControlsRoot != null)
            {
                boardControlsRoot.SetActive(false);
            }

            return;
        }

        if (setupPanel != null)
        {
            setupPanel.SetActive(false);
        }
    }

    private void ConfigureDropdowns()
    {
        if (controlTypeDropdowns == null)
        {
            return;
        }

        for (int index = 0;
             index <
             controlTypeDropdowns.Length;
             index++)
        {
            TMP_Dropdown dropdown =
                controlTypeDropdowns[index];

            if (dropdown == null)
            {
                continue;
            }

            dropdown.ClearOptions();

            dropdown.AddOptions(
                new List<string>
                {
                    "İNSAN",
                    "BOT"
                });

            MatchPlayerControlType
                defaultControlType =
                    GetDefaultControlType(
                        index);

            dropdown.SetValueWithoutNotify(
                (int)defaultControlType);

            dropdown.RefreshShownValue();
        }
    }

    private MatchPlayerControlType
        GetDefaultControlType(
            int playerIndex)
    {
        if (defaultControlTypes != null &&
            playerIndex >= 0 &&
            playerIndex <
            defaultControlTypes.Length)
        {
            return
                defaultControlTypes[
                    playerIndex];
        }

        return playerIndex == 0
            ? MatchPlayerControlType.Human
            : MatchPlayerControlType.Bot;
    }

    private bool ValidateSetup(
        bool logErrors)
    {
        bool valid = true;

        if (turnManager == null)
        {
            valid = false;

            LogSetupError(
                "Turn Manager is not connected.",
                logErrors);
        }

        if (players == null ||
            players.Length < 2)
        {
            valid = false;

            LogSetupError(
                "At least two player pawns are required.",
                logErrors);
        }

        if (controlTypeDropdowns == null ||
            players == null ||
            controlTypeDropdowns.Length !=
            players.Length)
        {
            valid = false;

            LogSetupError(
                "Control Type Dropdowns must have exactly " +
                "one entry for each player.",
                logErrors);
        }

        if (players != null)
        {
            for (int index = 0;
                 index < players.Length;
                 index++)
            {
                PlayerPawnMover player =
                    players[index];

                if (player == null)
                {
                    valid = false;

                    LogSetupError(
                        $"Player {index + 1} is not connected.",
                        logErrors);

                    continue;
                }

                if (player.GetComponent<
                        PlayerGameState>() ==
                    null)
                {
                    valid = false;

                    LogSetupError(
                        $"{player.name} does not have " +
                        "PlayerGameState.",
                        logErrors);
                }

                if (player.GetComponent<
                        BotPlayerController>() ==
                    null)
                {
                    valid = false;

                    LogSetupError(
                        $"{player.name} does not have " +
                        "BotPlayerController. Keep the bot " +
                        "component on every player; this setup " +
                        "screen only turns it on or off.",
                        logErrors);
                }
            }
        }

        if (controlTypeDropdowns != null)
        {
            for (int index = 0;
                 index <
                 controlTypeDropdowns.Length;
                 index++)
            {
                if (controlTypeDropdowns[index] ==
                    null)
                {
                    valid = false;

                    LogSetupError(
                        $"Control dropdown {index + 1} " +
                        "is not connected.",
                        logErrors);
                }
            }
        }

        if (startGameButton == null)
        {
            valid = false;

            LogSetupError(
                "Start Game Button is not connected.",
                logErrors);
        }

        return valid;
    }

    private void LogSetupError(
        string message,
        bool logErrors)
    {
        if (!logErrors)
        {
            return;
        }

        Debug.LogError(
            $"Match Setup: {message}",
            this);
    }
}
