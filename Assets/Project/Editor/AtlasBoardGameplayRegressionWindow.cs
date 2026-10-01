using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AtlasBoardGameplayRegressionWindow : EditorWindow
{
    private const string WindowTitle = "Atlas Board Regression";
    private const string StateFileName =
        "AtlasBoardGameplayRegressionV1.json";

    private enum CaseStatus
    {
        NotTested = 0,
        Pass = 1,
        Fail = 2,
        Deferred = 3
    }

    [Serializable]
    private sealed class RegressionCase
    {
        public string id;
        public string category;
        public string title;
        public string setup;
        public string expected;
        public CaseStatus status;
        public string notes;
    }

    [Serializable]
    private sealed class RegressionState
    {
        public string schema = "atlasboard_gameplay_regression_v1";
        public List<RegressionCase> cases =
            new List<RegressionCase>();
    }

    private RegressionState state;
    private Vector2 scroll;
    private readonly Dictionary<string, bool> foldouts =
        new Dictionary<string, bool>();
    private readonly List<string> preflightLines =
        new List<string>();
    private bool preflightPassed;

    [MenuItem(
        "Atlas Board/QA & Diagnostics/QA/Gameplay Regression Matrix v1",
        false,
        700)]
    public static void Open()
    {
        AtlasBoardGameplayRegressionWindow window =
            GetWindow<AtlasBoardGameplayRegressionWindow>();
        window.titleContent =
            new GUIContent(WindowTitle);
        window.minSize =
            new Vector2(820f, 560f);
        window.Show();
    }

    private void OnEnable()
    {
        LoadOrCreateState();
    }

    private void OnGUI()
    {
        EnsureState();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            "Atlas Board — Full Gameplay Regression Matrix v1",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Phase 8A: formal 2–4 player gameplay/network regression before map expansion.",
            EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(6f);
        DrawToolbar();
        DrawPreflight();
        DrawSummary();

        scroll =
            EditorGUILayout.BeginScrollView(scroll);

        foreach (IGrouping<string, RegressionCase> group in
                 state.cases.GroupBy(item => item.category))
        {
            DrawCategory(group.Key, group.ToList());
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "Run Static Preflight",
                GUILayout.Height(28f)))
        {
            RunStaticPreflight();
        }

        if (GUILayout.Button(
                "Export Markdown Report",
                GUILayout.Height(28f)))
        {
            ExportMarkdownReport();
        }

        if (GUILayout.Button(
                "Save",
                GUILayout.Height(28f),
                GUILayout.Width(90f)))
        {
            SaveState();
        }

        if (GUILayout.Button(
                "Reset Results",
                GUILayout.Height(28f),
                GUILayout.Width(110f)))
        {
            if (EditorUtility.DisplayDialog(
                    "Reset regression results?",
                    "All PASS/FAIL notes will be cleared. Deferred late-resilience cases stay Deferred.",
                    "Reset",
                    "Cancel"))
            {
                ResetResults();
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPreflight()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        string status =
            preflightLines.Count == 0
                ? "NOT RUN"
                : preflightPassed
                    ? "PASS"
                    : "CHECK REQUIRED";

        EditorGUILayout.LabelField(
            $"Static Preflight: {status}",
            EditorStyles.boldLabel);

        foreach (string line in preflightLines)
        {
            EditorGUILayout.LabelField(
                line,
                EditorStyles.wordWrappedMiniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSummary()
    {
        int pass = state.cases.Count(item => item.status == CaseStatus.Pass);
        int fail = state.cases.Count(item => item.status == CaseStatus.Fail);
        int deferred = state.cases.Count(item => item.status == CaseStatus.Deferred);
        int untested = state.cases.Count(item => item.status == CaseStatus.NotTested);

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            $"PASS {pass}",
            GUILayout.Width(90f));
        EditorGUILayout.LabelField(
            $"FAIL {fail}",
            GUILayout.Width(90f));
        EditorGUILayout.LabelField(
            $"DEFERRED {deferred}",
            GUILayout.Width(110f));
        EditorGUILayout.LabelField(
            $"NOT TESTED {untested}",
            GUILayout.Width(130f));
        EditorGUILayout.LabelField(
            $"TOTAL {state.cases.Count}");
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCategory(
        string category,
        List<RegressionCase> cases)
    {
        if (!foldouts.ContainsKey(category))
        {
            foldouts[category] = true;
        }

        EditorGUILayout.Space(3f);
        foldouts[category] =
            EditorGUILayout.Foldout(
                foldouts[category],
                $"{category} ({cases.Count})",
                true,
                EditorStyles.foldoutHeader);

        if (!foldouts[category])
        {
            return;
        }

        foreach (RegressionCase regressionCase in cases)
        {
            DrawCase(regressionCase);
        }
    }

    private void DrawCase(RegressionCase regressionCase)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(
            regressionCase.id,
            EditorStyles.boldLabel,
            GUILayout.Width(64f));

        EditorGUILayout.LabelField(
            regressionCase.title,
            EditorStyles.boldLabel);

        CaseStatus nextStatus =
            (CaseStatus)EditorGUILayout.EnumPopup(
                regressionCase.status,
                GUILayout.Width(100f));

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(
            $"Setup: {regressionCase.setup}",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField(
            $"Expected: {regressionCase.expected}",
            EditorStyles.wordWrappedMiniLabel);

        string nextNotes =
            EditorGUILayout.TextField(
                "Notes",
                regressionCase.notes ?? string.Empty);

        if (nextStatus != regressionCase.status ||
            !string.Equals(
                nextNotes,
                regressionCase.notes,
                StringComparison.Ordinal))
        {
            regressionCase.status = nextStatus;
            regressionCase.notes = nextNotes;
            SaveState();
        }

        EditorGUILayout.EndVertical();
    }

    private void RunStaticPreflight()
    {
        preflightLines.Clear();
        preflightPassed = true;

        Scene scene =
            SceneManager.GetActiveScene();

        AddPreflight(
            scene.IsValid() &&
            !string.IsNullOrWhiteSpace(scene.path),
            "Active scene is saved and valid.");

        string[] requiredSceneComponents =
        {
            "TurnManager",
            "PrototypeDiceController",
            "DiceVisualController",
            "AuctionManager",
            "TradeManager",
            "PropertyDevelopmentManager",
            "BankruptcyManager",
            "MatchResultManager",
            "EventCardManager",
            "SpecialTileManager",
            "MatchSetupManager",
            "AtlasBoardTurnDiceNetworkCoordinator",
            "AtlasBoardLobbyRuntimeBridge"
        };

        HashSet<string> sceneTypeNames =
            new HashSet<string>(
                Resources
                    .FindObjectsOfTypeAll<MonoBehaviour>()
                    .Where(item =>
                        item != null &&
                        item.gameObject.scene.IsValid())
                    .Select(item => item.GetType().Name));

        foreach (string typeName in requiredSceneComponents)
        {
            AddPreflight(
                sceneTypeNames.Contains(typeName),
                $"Scene component present: {typeName}");
        }

        int playerCount =
            Resources
                .FindObjectsOfTypeAll<MonoBehaviour>()
                .Count(item =>
                    item != null &&
                    item.gameObject.scene.IsValid() &&
                    string.Equals(
                        item.GetType().Name,
                        "PlayerGameState",
                        StringComparison.Ordinal));

        AddPreflight(
            playerCount >= 4,
            $"PlayerGameState scene capacity >= 4 (found {playerCount}).");

        string[] eventDeckGuids =
            AssetDatabase.FindAssets(
                "t:EventDeckDefinition");

        bool eventDeckFound =
            eventDeckGuids.Length > 0;
        AddPreflight(
            eventDeckFound,
            "EventDeckDefinition asset exists.");

        if (eventDeckFound)
        {
            int largestDeck = 0;

            foreach (string guid in eventDeckGuids)
            {
                string assetPath =
                    AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object deck =
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

                if (deck == null)
                {
                    continue;
                }

                SerializedObject serialized =
                    new SerializedObject(deck);
                SerializedProperty cards =
                    serialized.FindProperty("cards");

                if (cards != null && cards.isArray)
                {
                    largestDeck =
                        Mathf.Max(
                            largestDeck,
                            cards.arraySize);
                }
            }

            AddPreflight(
                largestDeck >= 36,
                $"Event Deck contains at least 36 cards (largest={largestDeck}).");
        }

        string[] mapKeywords =
        {
            "Turkey",
            "Colorado",
            "USA"
        };

        foreach (string keyword in mapKeywords)
        {
            bool found =
                AssetDatabase.FindAssets(keyword)
                    .Length > 0;
            AddPreflight(
                found,
                $"Map/content asset search contains '{keyword}'.");
        }

        bool chatPresent =
            sceneTypeNames.Contains(
                "AtlasBoardChatUIController") ||
            AssetDatabase.FindAssets(
                    "AtlasBoardChatUIController")
                .Length > 0;

        AddPreflight(
            chatPresent,
            "Phase 6 chat UI source/component is present.");

        Repaint();
    }

    private void AddPreflight(
        bool passed,
        string description)
    {
        preflightLines.Add(
            $"{(passed ? "PASS" : "CHECK")} — {description}");

        if (!passed)
        {
            preflightPassed = false;
        }
    }

    private void ExportMarkdownReport()
    {
        EnsureState();

        string projectRoot =
            Directory.GetParent(
                Application.dataPath)?.FullName ??
            Application.dataPath;
        string reportDirectory =
            Path.Combine(
                projectRoot,
                "AtlasBoard_QA_Reports");

        Directory.CreateDirectory(reportDirectory);

        string timestamp =
            DateTime.UtcNow.ToString(
                "yyyyMMdd_HHmmss");
        string reportPath =
            Path.Combine(
                reportDirectory,
                $"GameplayRegression_{timestamp}_UTC.md");

        StringBuilder builder =
            new StringBuilder();
        builder.AppendLine("# Atlas Board Gameplay Regression Report");
        builder.AppendLine();
        builder.AppendLine($"Generated UTC: {DateTime.UtcNow:O}");
        builder.AppendLine($"Unity: {Application.unityVersion}");
        builder.AppendLine($"Scene: {SceneManager.GetActiveScene().path}");
        builder.AppendLine();

        builder.AppendLine("## Static preflight");
        builder.AppendLine();
        if (preflightLines.Count == 0)
        {
            builder.AppendLine("- Not run in this editor session.");
        }
        else
        {
            foreach (string line in preflightLines)
            {
                builder.AppendLine($"- {line}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Regression matrix");

        foreach (IGrouping<string, RegressionCase> group in
                 state.cases.GroupBy(item => item.category))
        {
            builder.AppendLine();
            builder.AppendLine($"### {group.Key}");
            builder.AppendLine();

            foreach (RegressionCase item in group)
            {
                builder.AppendLine(
                    $"- **{item.id} — {item.title}** — `{item.status}`");
                builder.AppendLine($"  - Setup: {item.setup}");
                builder.AppendLine($"  - Expected: {item.expected}");

                if (!string.IsNullOrWhiteSpace(item.notes))
                {
                    builder.AppendLine($"  - Notes: {item.notes}");
                }
            }
        }

        File.WriteAllText(
            reportPath,
            builder.ToString(),
            new UTF8Encoding(false));

        EditorUtility.RevealInFinder(reportPath);
        Debug.Log(
            $"Atlas Board gameplay regression report exported: {reportPath}");
    }

    private void LoadOrCreateState()
    {
        string path =
            StatePath();

        if (File.Exists(path))
        {
            try
            {
                state =
                    JsonUtility.FromJson<RegressionState>(
                        File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Atlas Board regression state could not be read. " +
                    exception.Message);
            }
        }

        EnsureState();
    }

    private void EnsureState()
    {
        if (state == null)
        {
            state =
                new RegressionState();
        }

        List<RegressionCase> canonical =
            BuildCanonicalCases();

        if (state.cases == null ||
            state.cases.Count == 0)
        {
            state.cases = canonical;
            SaveState();
            return;
        }

        Dictionary<string, RegressionCase> existing =
            state.cases
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.id))
                .ToDictionary(item => item.id, item => item);

        List<RegressionCase> merged =
            new List<RegressionCase>();

        foreach (RegressionCase template in canonical)
        {
            if (existing.TryGetValue(
                    template.id,
                    out RegressionCase saved))
            {
                template.status = saved.status;
                template.notes = saved.notes;
            }

            merged.Add(template);
        }

        state.cases = merged;
    }

    private void SaveState()
    {
        EnsureStateDirectory();
        File.WriteAllText(
            StatePath(),
            JsonUtility.ToJson(state, true),
            new UTF8Encoding(false));
    }

    private void ResetResults()
    {
        foreach (RegressionCase item in state.cases)
        {
            item.notes = string.Empty;
            item.status =
                IsDeferredCase(item.id)
                    ? CaseStatus.Deferred
                    : CaseStatus.NotTested;
        }

        SaveState();
    }

    private static string StatePath()
    {
        return Path.Combine(
            ProjectRoot(),
            "Library",
            StateFileName);
    }

    private static string ProjectRoot()
    {
        return Directory.GetParent(
                   Application.dataPath)?.FullName ??
               Application.dataPath;
    }

    private static void EnsureStateDirectory()
    {
        string directory =
            Path.GetDirectoryName(
                StatePath());

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static bool IsDeferredCase(string id)
    {
        return id == "NET-07" ||
               id == "NET-08" ||
               id == "DEV-05" ||
               id == "NET-09";
    }

    private static List<RegressionCase> BuildCanonicalCases()
    {
        List<RegressionCase> cases =
            new List<RegressionCase>();

        Add(cases, "TURN-01", "Turn / Dice", "Starting order", "2–4 players; fresh match.", "Each active seat rolls once; highest starts; no duplicate/missing seat.");
        Add(cases, "TURN-02", "Turn / Dice", "Starting-order tie reroll", "Force/observe a tie in starting order.", "Only tied players reroll until resolved.");
        Add(cases, "TURN-03", "Turn / Dice", "2d6 visual/result sync", "Roll repeatedly.", "Visual die faces equal authoritative gameplay result every roll.");
        Add(cases, "TURN-04", "Turn / Dice", "Doubles extra roll", "Doubles enabled; roll doubles.", "Same player receives exactly one extra roll.");
        Add(cases, "TURN-05", "Turn / Dice", "Triple-double penalty", "Triple-double rule enabled; obtain three consecutive doubles.", "Penalty triggers once and turn flow remains valid.");
        Add(cases, "TURN-06", "Turn / Dice", "Rest + doubles", "Land on Rest with a double, Continue.", "No extra roll after Rest acknowledgement; turn passes to next player.");
        Add(cases, "TURN-07", "Turn / Dice", "Human roll timeout", "Human does not roll for configured timeout.", "Authoritative automatic roll occurs without duplicate roll.");

        Add(cases, "ECO-01", "Economy / Property", "Unowned property purchase", "Human lands on purchasable unowned property and buys.", "Money decreases once; ownership updates on both clients.");
        Add(cases, "ECO-02", "Economy / Property", "Decline purchase -> auction", "Human declines an unowned property.", "Auction opens once with correct property and eligible bidders.");
        Add(cases, "ECO-03", "Economy / Property", "Rent payment", "Player lands on another player's property.", "Correct rent transfers exactly once on Host and Guest.");
        Add(cases, "ECO-04", "Economy / Property", "Ownership persistence", "Acquire several properties then continue multiple turns.", "Ownership remains stable across snapshots/UI refreshes.");
        Add(cases, "ECO-05", "Economy / Property", "Money synchronization", "Trigger purchase, rent, tax and bonus changes.", "Host/Guest balances converge to the same authoritative values.");

        Add(cases, "SPEC-01", "Special Tiles / Cards", "Event card", "Land on Event.", "One card resolves once; localized title/body/result appear; state syncs.");
        Add(cases, "SPEC-02", "Special Tiles / Cards", "Bonus", "Land on Bonus.", "Reward applies once and money syncs.");
        Add(cases, "SPEC-03", "Special Tiles / Cards", "Tax", "Land on Tax.", "Tax applies once and does not double-charge follower.");
        Add(cases, "SPEC-04", "Special Tiles / Cards", "Travel", "Land on Travel and complete selection/flow.", "Pawn/state resolve consistently on both clients.");
        Add(cases, "SPEC-05", "Special Tiles / Cards", "Vacation", "Land on Vacation.", "Vacation effect/state resolves and turn continues correctly.");
        Add(cases, "SPEC-06", "Special Tiles / Cards", "Rest", "Land on Rest without doubles.", "Continue/acknowledgement resolves once and passes turn.");
        Add(cases, "SPEC-07", "Special Tiles / Cards", "36-card Event Deck", "Run static preflight and sample cards.", "Deck asset contains >=36 cards; no missing runtime card reference.");

        Add(cases, "AUC-01", "Auction", "Human vs Human auction", "Two Human seats bid/pass.", "Bid authority, winner, price, money and ownership sync.");
        Add(cases, "AUC-02", "Auction", "Human vs Bot auction", "Include at least one active Bot bidder.", "Bot decision does not block Human and final state syncs.");
        Add(cases, "AUC-03", "Auction", "All but one pass", "All bidders except one pass.", "Auction closes deterministically with the correct winner/price.");

        Add(cases, "TRD-01", "Trade", "Host -> Guest trade", "Host Human proposes a trade to Guest Human.", "Guest alone owns the remote decision; accept/reject syncs once.");
        Add(cases, "TRD-02", "Trade", "Guest -> Host trade", "Guest Human proposes a trade to Host Human.", "Host receives correct decision and final assets/money sync.");
        Add(cases, "TRD-03", "Trade", "Trade cancel/reject", "Open a valid trade then cancel/reject.", "No assets/money change; turn/control flow remains responsive.");
        Add(cases, "TRD-04", "Trade", "Trade during roll timeout", "Open Trade during Human roll window.", "Roll timeout pauses/resets; no surprise auto-roll during trade.");

        Add(cases, "DEV-01", "Development", "Build development", "Own eligible set/property and develop.", "Cost/development level/visual update once on both clients.");
        Add(cases, "DEV-02", "Development", "Balanced development rule", "Balanced rule enabled; attempt invalid uneven build.", "Invalid development is blocked consistently.");
        Add(cases, "DEV-03", "Development", "Developed rent", "Land on developed opponent property.", "Rent uses current development level and syncs.");
        Add(cases, "DEV-04", "Development", "Development visual sync", "Build multiple levels while Host/Guest observe.", "Both clients show the same development visuals.");
        Add(cases, "DEV-05", "Development", "Bankruptcy development cleanup", "Bankrupt owner of developed property.", "All development levels and visuals clear on both clients.", CaseStatus.Deferred);

        Add(cases, "BANK-01", "Bankruptcy / Result", "Core bankruptcy", "Drive one player below bankruptcy threshold.", "Player is removed/converted according to rules without match deadlock.");
        Add(cases, "BANK-02", "Bankruptcy / Result", "Result winner/net worth", "Finish match with known standings.", "Winner and displayed net worth/estate values are correct.");
        Add(cases, "BANK-03", "Bankruptcy / Result", "Guest result controls", "Finish as Guest.", "Guest sees host-wait message + Leave; no Restart.");
        Add(cases, "BANK-04", "Bankruptcy / Result", "Host rematch", "Finish as Host and click rematch once.", "Button disables/loads; both clients return to same lobby.");

        Add(cases, "NET-01", "Online Authority / Session", "Human authority label", "First online match start from Unity Editor Human seat.", "HUD says HUMAN/İNSAN immediately and seat never auto-acts as Bot.");
        Add(cases, "NET-02", "Online Authority / Session", "Host/Guest decision ownership", "Exercise purchase/trade/auction/development decisions.", "Only the owning Human client can submit its decision; Host remains authoritative.");
        Add(cases, "NET-03", "Online Authority / Session", "Pawn synchronization", "Both clients observe several moves/special moves.", "Pawn positions converge without replay/duplicate movement.");
        Add(cases, "NET-04", "Online Authority / Session", "Rematch clean second match", "Complete match -> rematch -> start second match.", "No old money/property/pawn/modal/result state leaks into match two.");
        Add(cases, "NET-05", "Online Authority / Session", "New lobby without app restart", "Leave result/lobby flow then create/join another room.", "Fresh game starts cleanly without restarting executable.");
        Add(cases, "NET-06", "Online Authority / Session", "Dice reuse after rematch/new lobby", "Start second match/session without app restart.", "Dice remain present and functional.");
        Add(cases, "NET-07", "Online Authority / Session", "Voluntary Leave -> TemporaryBot -> Rejoin", "Late-resilience milestone.", "Same seat reclaims within reservation window with state catch-up.", CaseStatus.Deferred);
        Add(cases, "NET-08", "Online Authority / Session", "Repeated reconnect cycles", "Late-resilience milestone.", "Multiple disconnect/reconnect cycles do not duplicate/replay state.", CaseStatus.Deferred);
        Add(cases, "NET-09", "Online Authority / Session", "Host migration", "Dedicated future milestone.", "New Host receives authority safely after Host departure.", CaseStatus.Deferred);

        Add(cases, "PLY-01", "Player Matrix", "2-player Human/Human", "Run representative full-turn smoke with 2 Human players.", "No blocked turn/decision; state remains synchronized.");
        Add(cases, "PLY-02", "Player Matrix", "2-player Human/Bot", "Run representative full-turn smoke with Human + Bot.", "Bot acts only for its seat; Human remains interactive.");
        Add(cases, "PLY-03", "Player Matrix", "3-player mixed", "Run 3-player Human/Human/Bot or equivalent.", "All three seats rotate and resolve decisions correctly.");
        Add(cases, "PLY-04", "Player Matrix", "4-player mixed", "Run 4-player mixed Human/Bot session.", "All four seats remain distinct with stable HUD/pawns/turn order.");

        Add(cases, "UI-01", "UI / Themes / Localization", "Board invariant across themes", "Switch Classic/Garden/Beach/Pavilion/Street.", "Only environment changes; game-board walnut edge + warm center stay invariant.");
        Add(cases, "UI-02", "UI / Themes / Localization", "HUD no-overlap", "Observe all 4 HUD cards and long/localized names.", "Name/money/control/turn badge remain readable; P1 icon alignment is polish-only.");
        Add(cases, "UI-03", "UI / Themes / Localization", "Seven-language smoke", "Cycle EN/TR/ES/FR/DE/KO/RU through menu/lobby/match.", "No missing glyphs, major overlap or untranslated critical action labels.");
        Add(cases, "UI-04", "UI / Themes / Localization", "Lobby + Match Chat smoke", "Host/Guest send messages in lobby and active match.", "Messages sync, unread badge works, chat does not block gameplay HUD.");
        Add(cases, "UI-05", "UI / Themes / Localization", "Chat server constraints", "Try empty, 120, 121 chars, <10s resend, profanity.", "Server enforces empty/length/rate/profanity rules consistently.");
        Add(cases, "UI-06", "UI / Themes / Localization", "Chat server time/UTC offset", "Send from clients with different UTC offsets.", "Canonical server instant is used; sender-local HH:mm (UTC±X) displays correctly.");

        return cases;
    }

    private static void Add(
        List<RegressionCase> cases,
        string id,
        string category,
        string title,
        string setup,
        string expected,
        CaseStatus status = CaseStatus.NotTested)
    {
        cases.Add(
            new RegressionCase
            {
                id = id,
                category = category,
                title = title,
                setup = setup,
                expected = expected,
                status = status,
                notes = string.Empty
            });
    }
}
