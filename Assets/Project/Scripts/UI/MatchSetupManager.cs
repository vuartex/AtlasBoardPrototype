using System.Collections.Generic;
using System.Linq;
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
    private const int MinimumPlayerCount = 2;

    [Header("Match")]
    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private PropertyDevelopmentManager
        propertyDevelopmentManager;

    [SerializeField]
    private BoardGenerator boardGenerator;

    [Header("Maps")]
    [SerializeField]
    private BoardMapDefinition[] availableMaps;

    [SerializeField, Min(0)]
    private int defaultMapIndex;

    [Header("Players")]
    [SerializeField]
    private PlayerPawnMover[] players;

    [SerializeField]
    private MatchPlayerControlType[]
        defaultControlTypes;

    [SerializeField, Range(2, 4)]
    private int defaultPlayerCount = 3;

    [Header("Default Match Rules")]
    [SerializeField]
    private int defaultRoundLimit = 20;

    [SerializeField]
    private bool defaultBalancedDevelopment = true;

    [SerializeField]
    private bool defaultDoublesExtraRoll = true;

    [SerializeField]
    private bool defaultTripleDoublePenalty = true;

    [Header("Setup UI")]
    [SerializeField]
    private GameObject setupPanel;

    [SerializeField]
    private TMP_Dropdown mapDropdown;

    [SerializeField]
    private TMP_Dropdown playerCountDropdown;

    [SerializeField]
    private TMP_Dropdown roundLimitDropdown;

    [SerializeField]
    private Toggle balancedDevelopmentToggle;

    [SerializeField]
    private Toggle doublesExtraRollToggle;

    [SerializeField]
    private Toggle tripleDoublePenaltyToggle;

    [SerializeField]
    private TMP_Text[] playerNameTexts;

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

        ConfigureMapDropdown();
        ConfigurePlayerCountDropdown();
        ConfigureRoundLimitDropdown();
        ConfigureControlTypeDropdowns();
        ConfigureRuleToggles();

        if (mapDropdown != null)
        {
            mapDropdown.onValueChanged
                .AddListener(
                    HandleMapChanged);
        }

        if (playerCountDropdown != null)
        {
            playerCountDropdown.onValueChanged
                .AddListener(
                    HandlePlayerCountChanged);
        }

        if (doublesExtraRollToggle != null)
        {
            doublesExtraRollToggle.onValueChanged
                .AddListener(
                    HandleDoublesRuleChanged);
        }

        ApplySelectedMapPreview();
        ApplyPlayerCountPreview();
        RefreshRuleInteractivity();

        if (startGameButton != null)
        {
            startGameButton.interactable =
                ValidateSetup(
                    logErrors: false);
        }
    }

    private void OnDestroy()
    {
        if (mapDropdown != null)
        {
            mapDropdown.onValueChanged
                .RemoveListener(
                    HandleMapChanged);
        }

        if (playerCountDropdown != null)
        {
            playerCountDropdown.onValueChanged
                .RemoveListener(
                    HandlePlayerCountChanged);
        }

        if (doublesExtraRollToggle != null)
        {
            doublesExtraRollToggle.onValueChanged
                .RemoveListener(
                    HandleDoublesRuleChanged);
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

        ApplySelectedMapPreview();
        ApplySelectedMatchRules();

        int selectedPlayerCount =
            GetSelectedPlayerCount();

        ResetBoardEconomyForNewMatch();

        for (int index = 0;
             index < players.Length;
             index++)
        {
            PlayerPawnMover pawn =
                players[index];

            PlayerGameState playerState =
                pawn.GetComponent<
                    PlayerGameState>();

            BotPlayerController botController =
                pawn.GetComponent<
                    BotPlayerController>();

            bool participating =
                index < selectedPlayerCount;

            int matchStartingMoney =
                boardGenerator != null &&
                boardGenerator.ActiveEconomyProfile != null
                    ? boardGenerator.ActiveEconomyProfile.StartingMoney
                    : playerState.StartingMoney;

            playerState.PrepareForMatch(
                matchStartingMoney,
                participating);
            pawn.ResetForNewMatchSession();

            if (!participating)
            {
                botController.SetBotEnabled(false);
                pawn.SetPawnVisible(false);
                continue;
            }

            bool shouldBeBot =
                controlTypeDropdowns[index]
                    .value ==
                (int)MatchPlayerControlType.Bot;

            if (playerState.OnlineSeatStateActive)
            {
                AtlasBoardTurnDiceNetworkCoordinator coordinator =
                    FindAnyObjectByType<
                        AtlasBoardTurnDiceNetworkCoordinator>();

                shouldBeBot =
                    playerState.IsOnlineBotControlled &&
                    (coordinator == null ||
                     !coordinator.IsPreparedOnlineMatch ||
                     coordinator.LocalIsHost);
            }

            botController.SetBotEnabled(
                shouldBeBot);

            pawn.SetPawnVisible(true);
            pawn.SnapToCurrentTile();

            Debug.Log(
                $"Match setup — " +
                $"{playerState.DisplayName} " +
                $"[Slot {playerState.PlayerSlotIndex}] = " +
                $"{(shouldBeBot ? "BOT" : "HUMAN")}.",
                pawn);
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

    private void ConfigureMapDropdown()
    {
        if (mapDropdown == null)
        {
            return;
        }

        mapDropdown.ClearOptions();

        List<string> options =
            new List<string>();

        if (availableMaps != null)
        {
            foreach (BoardMapDefinition map
                     in availableMaps)
            {
                options.Add(
                    map != null
                        ? map.DisplayName
                        : "Eksik Harita");
            }
        }

        if (options.Count == 0)
        {
            options.Add(
                "HARİTA YOK");
        }

        mapDropdown.AddOptions(
            options);

        int selectedIndex =
            Mathf.Clamp(
                defaultMapIndex,
                0,
                Mathf.Max(
                    0,
                    options.Count - 1));

        mapDropdown.SetValueWithoutNotify(
            selectedIndex);

        mapDropdown.RefreshShownValue();
    }

    private void ConfigurePlayerCountDropdown()
    {
        if (playerCountDropdown == null)
        {
            return;
        }

        playerCountDropdown.ClearOptions();

        List<string> options =
            new List<string>();

        int maximumPlayerCount =
            GetMaximumPlayerCount();

        for (int count = MinimumPlayerCount;
             count <= maximumPlayerCount;
             count++)
        {
            options.Add(
                $"{count} OYUNCU");
        }

        playerCountDropdown.AddOptions(
            options);

        int clampedDefault =
            Mathf.Clamp(
                defaultPlayerCount,
                MinimumPlayerCount,
                maximumPlayerCount);

        playerCountDropdown
            .SetValueWithoutNotify(
                clampedDefault -
                MinimumPlayerCount);

        playerCountDropdown.RefreshShownValue();
    }

    private void ConfigureRoundLimitDropdown()
    {
        if (roundLimitDropdown == null)
        {
            return;
        }

        int[] roundOptions =
        {
            10,
            15,
            20,
            30
        };

        roundLimitDropdown.ClearOptions();

        List<string> options =
            roundOptions
                .Select(
                    value =>
                        $"{value} TUR")
                .ToList();

        roundLimitDropdown.AddOptions(
            options);

        int selectedIndex = 0;
        int smallestDifference =
            int.MaxValue;

        for (int index = 0;
             index < roundOptions.Length;
             index++)
        {
            int difference =
                Mathf.Abs(
                    roundOptions[index] -
                    defaultRoundLimit);

            if (difference <
                smallestDifference)
            {
                smallestDifference =
                    difference;

                selectedIndex =
                    index;
            }
        }

        roundLimitDropdown
            .SetValueWithoutNotify(
                selectedIndex);

        roundLimitDropdown.RefreshShownValue();
    }

    private void ConfigureRuleToggles()
    {
        if (balancedDevelopmentToggle != null)
        {
            balancedDevelopmentToggle
                .SetIsOnWithoutNotify(
                    defaultBalancedDevelopment);
        }

        if (doublesExtraRollToggle != null)
        {
            doublesExtraRollToggle
                .SetIsOnWithoutNotify(
                    defaultDoublesExtraRoll);
        }

        if (tripleDoublePenaltyToggle != null)
        {
            tripleDoublePenaltyToggle
                .SetIsOnWithoutNotify(
                    defaultDoublesExtraRoll &&
                    defaultTripleDoublePenalty);
        }
    }

    private void ConfigureControlTypeDropdowns()
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

    private void HandleDoublesRuleChanged(
        bool enabledValue)
    {
        RefreshRuleInteractivity();
    }

    private void RefreshRuleInteractivity()
    {
        if (tripleDoublePenaltyToggle == null)
        {
            return;
        }

        bool doublesEnabled =
            doublesExtraRollToggle == null ||
            doublesExtraRollToggle.isOn;

        if (!doublesEnabled)
        {
            // The triple-double penalty has no meaning when
            // doubles do not grant another roll. Clear the
            // checkmark as well as disabling the control so
            // the setup screen cannot show a contradictory
            // rule combination.
            tripleDoublePenaltyToggle
                .SetIsOnWithoutNotify(
                    false);
        }

        tripleDoublePenaltyToggle.interactable =
            doublesEnabled;
    }

    private void ApplySelectedMatchRules()
    {
        int selectedRoundLimit =
            GetSelectedRoundLimit();

        bool balancedDevelopment =
            balancedDevelopmentToggle == null
                ? defaultBalancedDevelopment
                : balancedDevelopmentToggle.isOn;

        bool doublesRule =
            doublesExtraRollToggle == null
                ? defaultDoublesExtraRoll
                : doublesExtraRollToggle.isOn;

        bool triplePenalty =
            doublesRule &&
            (tripleDoublePenaltyToggle == null
                ? defaultTripleDoublePenalty
                : tripleDoublePenaltyToggle.isOn);

        turnManager.SetRoundLimit(
            selectedRoundLimit);

        turnManager.SetDoublesExtraRollRule(
            doublesRule);

        turnManager.SetTripleDoublePenaltyRule(
            triplePenalty);

        if (propertyDevelopmentManager != null)
        {
            propertyDevelopmentManager
                .SetRequireBalancedGroupDevelopment(
                    balancedDevelopment);
        }

        Debug.Log(
            "Match rules — " +
            $"Rounds: {selectedRoundLimit}, " +
            $"Balanced development: " +
            $"{(balancedDevelopment ? "ON" : "OFF")}, " +
            $"Doubles extra roll: " +
            $"{(doublesRule ? "ON" : "OFF")}, " +
            $"Triple-double penalty: " +
            $"{(triplePenalty ? "ON" : "OFF")}.",
            this);
    }

    private int GetSelectedRoundLimit()
    {
        int[] roundOptions =
        {
            10,
            15,
            20,
            30
        };

        if (roundLimitDropdown == null)
        {
            return Mathf.Max(
                1,
                defaultRoundLimit);
        }

        int index =
            Mathf.Clamp(
                roundLimitDropdown.value,
                0,
                roundOptions.Length - 1);

        return roundOptions[index];
    }

    private void HandleMapChanged(
        int dropdownValue)
    {
        ApplySelectedMapPreview();
    }

    private void ApplySelectedMapPreview()
    {
        if (boardGenerator == null ||
            availableMaps == null ||
            availableMaps.Length == 0)
        {
            return;
        }

        int selectedIndex =
            GetSelectedMapIndex();

        BoardMapDefinition selectedMap =
            availableMaps[selectedIndex];

        if (selectedMap == null)
        {
            Debug.LogError(
                $"Map slot {selectedIndex} is empty.",
                this);

            return;
        }

        boardGenerator.SetActiveMapDefinition(
            selectedMap,
            applyImmediately: true);

        Debug.Log(
            $"Selected map: " +
            $"{selectedMap.DisplayName} " +
            $"({selectedMap.MapId}).",
            this);
    }

    private int GetSelectedMapIndex()
    {
        if (availableMaps == null ||
            availableMaps.Length == 0)
        {
            return 0;
        }

        int selectedIndex =
            mapDropdown != null
                ? mapDropdown.value
                : defaultMapIndex;

        return Mathf.Clamp(
            selectedIndex,
            0,
            availableMaps.Length - 1);
    }

    private void HandlePlayerCountChanged(
        int dropdownValue)
    {
        ApplyPlayerCountPreview();
    }

    private void ApplyPlayerCountPreview()
    {
        int selectedPlayerCount =
            GetSelectedPlayerCount();

        for (int index = 0;
             index < players.Length;
             index++)
        {
            bool participating =
                index < selectedPlayerCount;

            PlayerPawnMover pawn =
                players[index];

            if (pawn != null)
            {
                PlayerGameState playerState =
                    pawn.GetComponent<
                        PlayerGameState>();

                if (playerState != null)
                {
                    playerState.SetParticipating(
                        participating);
                }

                pawn.SetPawnVisible(
                    participating);
            }

            if (playerNameTexts != null &&
                index < playerNameTexts.Length &&
                playerNameTexts[index] != null)
            {
                playerNameTexts[index]
                    .gameObject
                    .SetActive(participating);
            }

            if (controlTypeDropdowns != null &&
                index <
                controlTypeDropdowns.Length &&
                controlTypeDropdowns[index] !=
                null)
            {
                controlTypeDropdowns[index]
                    .gameObject
                    .SetActive(participating);
            }
        }
    }

    private int GetSelectedPlayerCount()
    {
        int maximumPlayerCount =
            GetMaximumPlayerCount();

        if (playerCountDropdown == null)
        {
            return Mathf.Clamp(
                defaultPlayerCount,
                MinimumPlayerCount,
                maximumPlayerCount);
        }

        return Mathf.Clamp(
            playerCountDropdown.value +
            MinimumPlayerCount,
            MinimumPlayerCount,
            maximumPlayerCount);
    }

    private int GetMaximumPlayerCount()
    {
        if (players == null)
        {
            return MinimumPlayerCount;
        }

        return Mathf.Clamp(
            players.Length,
            MinimumPlayerCount,
            4);
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

        if (propertyDevelopmentManager == null)
        {
            valid = false;

            LogSetupError(
                "Property Development Manager is not connected.",
                logErrors);
        }

        if (boardGenerator == null)
        {
            valid = false;

            LogSetupError(
                "Board Generator is not connected.",
                logErrors);
        }

        if (availableMaps == null ||
            availableMaps.Length == 0)
        {
            valid = false;

            LogSetupError(
                "Available Maps is empty.",
                logErrors);
        }
        else
        {
            for (int index = 0;
                 index < availableMaps.Length;
                 index++)
            {
                if (availableMaps[index] == null)
                {
                    valid = false;

                    LogSetupError(
                        $"Available Maps element {index} is empty.",
                        logErrors);
                }
            }
        }

        if (mapDropdown == null)
        {
            valid = false;

            LogSetupError(
                "Map Dropdown is not connected.",
                logErrors);
        }

        if (roundLimitDropdown == null)
        {
            valid = false;

            LogSetupError(
                "Round Limit Dropdown is not connected.",
                logErrors);
        }

        if (balancedDevelopmentToggle == null)
        {
            valid = false;

            LogSetupError(
                "Balanced Development Toggle is not connected.",
                logErrors);
        }

        if (doublesExtraRollToggle == null)
        {
            valid = false;

            LogSetupError(
                "Doubles Extra Roll Toggle is not connected.",
                logErrors);
        }

        if (tripleDoublePenaltyToggle == null)
        {
            valid = false;

            LogSetupError(
                "Triple Double Penalty Toggle is not connected.",
                logErrors);
        }

        if (players == null ||
            players.Length < MinimumPlayerCount)
        {
            valid = false;

            LogSetupError(
                "At least two player pawns are required.",
                logErrors);

            return valid;
        }

        if (players.Length > 4)
        {
            valid = false;

            LogSetupError(
                "Match Setup v2 supports a maximum " +
                "of four players.",
                logErrors);
        }

        if (controlTypeDropdowns == null ||
            controlTypeDropdowns.Length !=
            players.Length)
        {
            valid = false;

            LogSetupError(
                "Control Type Dropdowns must have exactly " +
                "one entry for each player.",
                logErrors);
        }

        if (playerNameTexts == null ||
            playerNameTexts.Length !=
            players.Length)
        {
            valid = false;

            LogSetupError(
                "Player Name Texts must have exactly " +
                "one entry for each player.",
                logErrors);
        }

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
                    "BotPlayerController.",
                    logErrors);
            }

            if (controlTypeDropdowns == null ||
                index >=
                controlTypeDropdowns.Length ||
                controlTypeDropdowns[index] ==
                null)
            {
                valid = false;

                LogSetupError(
                    $"Control dropdown {index + 1} " +
                    "is not connected.",
                    logErrors);
            }

            if (playerNameTexts == null ||
                index >=
                playerNameTexts.Length ||
                playerNameTexts[index] == null)
            {
                valid = false;

                LogSetupError(
                    $"Player label {index + 1} " +
                    "is not connected.",
                    logErrors);
            }
        }

        if (playerCountDropdown == null)
        {
            valid = false;

            LogSetupError(
                "Player Count Dropdown is not connected.",
                logErrors);
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
    public void ResetForNewMatchSession()
    {
        matchLaunchRequested = false;

        if (boardControlsRoot != null)
        {
            boardControlsRoot.SetActive(false);
        }

        if (players != null)
        {
            foreach (PlayerPawnMover pawn in players)
            {
                pawn?.ResetForNewMatchSession();

                BotPlayerController bot =
                    pawn != null
                        ? pawn.GetComponent<BotPlayerController>()
                        : null;
                bot?.SetBotEnabled(false);
            }
        }
    }

    private void ResetBoardEconomyForNewMatch()
    {
        BoardPath boardPath =
            FindAnyObjectByType<BoardPath>();

        if (boardPath != null)
        {
            for (int tileIndex = 0;
                 tileIndex < boardPath.TileCount;
                 tileIndex++)
            {
                BoardTile tile =
                    boardPath.GetTile(tileIndex);
                tile?.ClearOwner();
            }
        }

        propertyDevelopmentManager
            ?.ResetAllDevelopmentsForNewMatch();
    }

}
