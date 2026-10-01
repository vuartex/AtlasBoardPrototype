using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AtlasBoardGameplayRegressionWindowV2 : EditorWindow
{
    private const string WindowTitle = "Atlas Board Regression v2";
    private const string StateFileName =
        "AtlasBoardGameplayRegressionV2.json";

    private enum CaseStatus
    {
        NotTested = 0,
        Pass = 1,
        Fail = 2
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
        public string schema = "atlasboard_gameplay_regression_v2";
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
        "Atlas Board/QA & Diagnostics/QA/Gameplay Regression Matrix v2 - Focused Gate",
        false,
        701)]
    public static void Open()
    {
        AtlasBoardGameplayRegressionWindowV2 window =
            GetWindow<AtlasBoardGameplayRegressionWindowV2>();
        window.titleContent = new GUIContent(WindowTitle);
        window.minSize = new Vector2(860f, 600f);
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
            "Atlas Board - Phase 8.1 Focused Runtime Acceptance",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "This v2 gate does not re-test already accepted Phase 5/6 paths. " +
            "It closes the remaining gameplay/player-matrix gaps before map expansion.",
            EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.Space(6f);
        DrawHistoricalBaseline();
        DrawToolbar();
        DrawPreflight();
        DrawSummary();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (IGrouping<string, RegressionCase> group in
                 state.cases.GroupBy(item => item.category))
        {
            DrawCategory(group.Key, group.ToList());
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawHistoricalBaseline()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            "Accepted baseline - not repeated in this matrix",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Physical/online dice sync; ordinary doubles; purchase/rent/money sync; " +
            "Event/Bonus/Tax foundations; pawn sync; Human authority label; Rest+doubles fix; " +
            "Guest result Leave; Host rematch; clean second match; dice reuse; board palette " +
            "across themes; Lobby/Match Chat; unread count; Phase 6B moderation backend 18/18; " +
            "report UX and confirmation popup runtime acceptance.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField(
            "Intentionally deferred: Leave->TemporaryBot->Rejoin, repeated reconnect, " +
            "bankruptcy development visual cleanup E2E, stale/duplicate intent torture, Host Migration.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
                "Run Static Preflight v2",
                GUILayout.Height(28f)))
        {
            RunStaticPreflight();
        }

        if (GUILayout.Button(
                "Export Focused Report",
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
                    "Reset focused regression results?",
                    "All PASS/FAIL notes in the Phase 8.1 focused gate will be cleared.",
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
            $"Static Preflight v2: {status}",
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
        int untested = state.cases.Count(item => item.status == CaseStatus.NotTested);
        bool ready = preflightLines.Count > 0 &&
                     preflightPassed &&
                     fail == 0 &&
                     untested == 0;

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            $"PASS {pass}",
            GUILayout.Width(90f));
        EditorGUILayout.LabelField(
            $"FAIL {fail}",
            GUILayout.Width(90f));
        EditorGUILayout.LabelField(
            $"NOT TESTED {untested}",
            GUILayout.Width(130f));
        EditorGUILayout.LabelField(
            $"TOTAL {state.cases.Count}",
            GUILayout.Width(100f));
        EditorGUILayout.LabelField(
            ready
                ? "GATE: READY FOR 8.2 MAP/CONTENT"
                : "GATE: NOT READY",
            EditorStyles.boldLabel);
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
        foldouts[category] = EditorGUILayout.Foldout(
            foldouts[category],
            $"{category} ({cases.Count})",
            true,
            EditorStyles.foldoutHeader);

        if (!foldouts[category])
        {
            return;
        }

        foreach (RegressionCase item in cases)
        {
            DrawCase(item);
        }
    }

    private void DrawCase(RegressionCase item)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(
            item.id,
            EditorStyles.boldLabel,
            GUILayout.Width(72f));
        EditorGUILayout.LabelField(
            item.title,
            EditorStyles.boldLabel);

        CaseStatus nextStatus =
            (CaseStatus)EditorGUILayout.EnumPopup(
                item.status,
                GUILayout.Width(105f));

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField(
            $"Setup: {item.setup}",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField(
            $"Expected: {item.expected}",
            EditorStyles.wordWrappedMiniLabel);

        string nextNotes = EditorGUILayout.TextField(
            "Notes",
            item.notes ?? string.Empty);

        if (nextStatus != item.status ||
            !string.Equals(
                nextNotes,
                item.notes,
                StringComparison.Ordinal))
        {
            item.status = nextStatus;
            item.notes = nextNotes;
            SaveState();
        }

        EditorGUILayout.EndVertical();
    }

    private void RunStaticPreflight()
    {
        preflightLines.Clear();
        preflightPassed = true;

        Scene scene = SceneManager.GetActiveScene();
        AddPreflight(
            scene.IsValid() && !string.IsNullOrWhiteSpace(scene.path),
            "Active scene is saved and valid.");

        HashSet<string> sceneTypeNames = new HashSet<string>(
            Resources
                .FindObjectsOfTypeAll<MonoBehaviour>()
                .Where(item =>
                    item != null &&
                    item.gameObject.scene.IsValid())
                .Select(item => item.GetType().Name));

        string[] requiredSceneComponents =
        {
            "TurnManager",
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

        foreach (string typeName in requiredSceneComponents)
        {
            AddPreflight(
                sceneTypeNames.Contains(typeName),
                $"Scene component present: {typeName}");
        }

        int playerCount = Resources
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

        ValidateEventDeck();
        ValidateRequiredMapContent();
        ValidatePhase6BSource();

        AddInfo(
            "PrototypeDiceController is legacy/optional and is NOT a required scene component. " +
            "At the reviewed GitHub checkpoint, code search found only its class source and the old v1 QA check/report; " +
            "the active dice path is validated through DiceVisualController + network coordinator.");

        Repaint();
    }

    private void ValidateEventDeck()
    {
        string[] deckGuids = AssetDatabase.FindAssets("t:EventDeckDefinition");
        AddPreflight(
            deckGuids.Length > 0,
            "EventDeckDefinition asset exists.");

        if (deckGuids.Length == 0)
        {
            return;
        }

        int largestDeck = 0;
        foreach (string guid in deckGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnityEngine.Object deck =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (deck == null)
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(deck);
            SerializedProperty cards = serialized.FindProperty("cards");
            if (cards != null && cards.isArray)
            {
                largestDeck = Mathf.Max(largestDeck, cards.arraySize);
            }
        }

        AddPreflight(
            largestDeck >= 36,
            $"Event Deck contains at least 36 cards (largest={largestDeck}).");
    }

    private void ValidateRequiredMapContent()
    {
        AddTextAssetContains(
            "Assets/Project/Data/Maps/map_turkey.asset",
            "Manisa",
            "Turkey map contains required Manisa content.");
        AddTextAssetContains(
            "Assets/Project/Data/Maps/map_colorado.asset",
            "Longmont",
            "Colorado map contains required Longmont content.");
        AddTextAssetContains(
            "Assets/Project/Data/Maps/map_usa.asset",
            "Colorado",
            "USA map contains required Colorado content.");
    }

    private void ValidatePhase6BSource()
    {
        string path =
            "Assets/Project/Scripts/UI/Chat/AtlasBoardChatSafetyUIController.cs";
        string absolute = Path.Combine(ProjectRoot(), path);

        bool exists = File.Exists(absolute);
        AddPreflight(exists, "Phase 6B Chat Safety UI source exists.");

        if (!exists)
        {
            return;
        }

        string text = File.ReadAllText(absolute);
        AddPreflight(
            !text.Contains("GetInstanceID()"),
            "Chat Safety does not use deprecated GetInstanceID().");
        AddPreflight(
            text.Contains("GetEntityId()"),
            "Chat Safety uses GetEntityId() for TMP text-scale identity.");
    }

    private void AddTextAssetContains(
        string relativePath,
        string requiredText,
        string description)
    {
        string absolute = Path.Combine(ProjectRoot(), relativePath);
        bool passed = File.Exists(absolute) &&
                      File.ReadAllText(absolute)
                          .IndexOf(
                              requiredText,
                              StringComparison.OrdinalIgnoreCase) >= 0;
        AddPreflight(passed, description);
    }

    private void AddPreflight(bool passed, string description)
    {
        preflightLines.Add(
            $"{(passed ? "PASS" : "CHECK")} - {description}");
        if (!passed)
        {
            preflightPassed = false;
        }
    }

    private void AddInfo(string description)
    {
        preflightLines.Add($"INFO - {description}");
    }

    private void ExportMarkdownReport()
    {
        EnsureState();

        string reportDirectory = Path.Combine(
            ProjectRoot(),
            "AtlasBoard_QA_Reports");
        Directory.CreateDirectory(reportDirectory);

        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string reportPath = Path.Combine(
            reportDirectory,
            $"GameplayRegressionFocusedV2_{timestamp}_UTC.md");

        int pass = state.cases.Count(item => item.status == CaseStatus.Pass);
        int fail = state.cases.Count(item => item.status == CaseStatus.Fail);
        int untested = state.cases.Count(item => item.status == CaseStatus.NotTested);
        bool ready = preflightLines.Count > 0 &&
                     preflightPassed &&
                     fail == 0 &&
                     untested == 0;

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# Atlas Board Phase 8.1 Focused Regression Report v2");
        builder.AppendLine();
        builder.AppendLine($"Generated UTC: {DateTime.UtcNow:O}");
        builder.AppendLine($"Unity: {Application.unityVersion}");
        builder.AppendLine($"Scene: {SceneManager.GetActiveScene().path}");
        builder.AppendLine($"Gate: {(ready ? "READY FOR 8.2" : "NOT READY")}");
        builder.AppendLine();

        builder.AppendLine("## Accepted historical baseline");
        builder.AppendLine();
        builder.AppendLine(
            "- Previously accepted Phase 5/6 paths are intentionally not duplicated in this focused matrix.");
        builder.AppendLine(
            "- Late resilience, bankruptcy development visual cleanup E2E, and Host Migration remain deliberately deferred.");
        builder.AppendLine();

        builder.AppendLine("## Static preflight v2");
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
        builder.AppendLine("## Focused runtime matrix");

        foreach (IGrouping<string, RegressionCase> group in
                 state.cases.GroupBy(item => item.category))
        {
            builder.AppendLine();
            builder.AppendLine($"### {group.Key}");
            builder.AppendLine();

            foreach (RegressionCase item in group)
            {
                builder.AppendLine(
                    $"- **{item.id} - {item.title}** - `{item.status}`");
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
        Debug.Log($"Atlas Board focused regression report exported: {reportPath}");
    }

    private void LoadOrCreateState()
    {
        string path = StatePath();
        if (File.Exists(path))
        {
            try
            {
                state = JsonUtility.FromJson<RegressionState>(
                    File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Atlas Board regression v2 state could not be read. " +
                    exception.Message);
            }
        }

        EnsureState();
    }

    private void EnsureState()
    {
        if (state == null)
        {
            state = new RegressionState();
        }

        List<RegressionCase> canonical = BuildCases();
        if (state.cases == null || state.cases.Count == 0)
        {
            state.cases = canonical;
            SaveState();
            return;
        }

        Dictionary<string, RegressionCase> existing = state.cases
            .Where(item =>
                item != null &&
                !string.IsNullOrWhiteSpace(item.id))
            .ToDictionary(item => item.id, item => item);

        foreach (RegressionCase template in canonical)
        {
            if (existing.TryGetValue(template.id, out RegressionCase saved))
            {
                template.status = saved.status;
                template.notes = saved.notes;
            }
        }

        state.cases = canonical;
    }

    private void SaveState()
    {
        File.WriteAllText(
            StatePath(),
            JsonUtility.ToJson(state, true),
            new UTF8Encoding(false));
    }

    private void ResetResults()
    {
        foreach (RegressionCase item in state.cases)
        {
            item.status = CaseStatus.NotTested;
            item.notes = string.Empty;
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
        return Directory.GetParent(Application.dataPath)?.FullName ??
               Application.dataPath;
    }

    private static List<RegressionCase> BuildCases()
    {
        List<RegressionCase> cases = new List<RegressionCase>();

        Add(cases, "F08-01", "Turn rules", "Starting-order tie reroll",
            "2-4 players; force or observe a tie during starting order.",
            "Only tied seats reroll until a unique winner is selected; no missing/duplicate seat.");
        Add(cases, "F08-02", "Turn rules", "Triple-double penalty",
            "Triple-double rule enabled; obtain three consecutive doubles.",
            "Penalty triggers exactly once and the next turn remains valid on Host/Guest.");
        Add(cases, "F08-03", "Turn rules", "Human roll timeout",
            "Human does not roll during the configured timeout.",
            "One authoritative automatic roll occurs; no duplicate roll or incorrect Bot label/control.");

        Add(cases, "F08-04", "Auction / Economy", "Decline purchase -> Human auction",
            "Two Human players; decline an unowned property.",
            "Auction opens once; Human bid/pass authority, winner, price, money and ownership synchronize.");
        Add(cases, "F08-05", "Auction / Economy", "Human vs Bot auction",
            "At least one Human and one Bot bidder.",
            "Bot acts only for its seat; Human remains interactive; final winner/price/state synchronize.");
        Add(cases, "F08-06", "Auction / Economy", "Developed rent",
            "Develop a property, then another player lands on it.",
            "Rent uses the current development level and transfers exactly once on both clients.");

        Add(cases, "F08-07", "Special tiles", "Travel path",
            "Land on Travel and complete its selection/flow.",
            "Authoritative destination/pawn/state agree on Host and Guest; turn flow remains responsive.");
        Add(cases, "F08-08", "Special tiles", "Vacation path",
            "Land on Vacation and complete its effect.",
            "Vacation state/effect resolves once and later turn flow remains correct.");
        Add(cases, "F08-09", "Special tiles", "Event-card runtime sample",
            "Resolve several Event cards including movement and money outcomes.",
            "Each card resolves once with localized UI and synchronized authoritative outcome.");

        Add(cases, "F08-10", "Trade", "Host -> Guest trade",
            "Host Human proposes a valid trade to Guest Human.",
            "Guest owns the remote decision; accept synchronizes assets/money exactly once.");
        Add(cases, "F08-11", "Trade", "Guest -> Host trade",
            "Guest Human proposes a valid trade to Host Human.",
            "Host receives the decision; accept synchronizes assets/money exactly once.");
        Add(cases, "F08-12", "Trade", "Trade reject/cancel + timeout pause",
            "Open a valid trade during a Human roll window, then reject/cancel.",
            "No asset change; no surprise auto-roll while trade is open; normal turn flow resumes.");

        Add(cases, "F08-13", "Development / Bankruptcy", "Balanced development rule",
            "Balanced-development rule enabled; attempt an invalid uneven build.",
            "Invalid build is blocked consistently; valid build still works afterward.");
        Add(cases, "F08-14", "Development / Bankruptcy", "Core bankruptcy + result values",
            "Drive a player into bankruptcy and finish a match with known standings.",
            "Match does not deadlock; bankrupt seat resolves correctly; winner/net-worth values are correct.");

        Add(cases, "F08-15", "Player matrix", "2-player Human/Human smoke",
            "Two Human clients; complete representative turns including at least one decision.",
            "Turns rotate, decisions remain seat-owned, money/pawns converge, no blocked UI.");
        Add(cases, "F08-16", "Player matrix", "2-player Human/Bot smoke",
            "One Human plus one Bot; complete representative turns.",
            "Bot acts only for its seat; Human remains interactive; turn loop remains stable.");
        Add(cases, "F08-17", "Player matrix", "3-player mixed smoke",
            "Three active seats, preferably Human/Human/Bot.",
            "All seats remain distinct; HUD/pawns/turn order and decisions stay correct.");
        Add(cases, "F08-18", "Player matrix", "4-player mixed smoke",
            "Four active seats with a Human/Bot mix.",
            "All four seats rotate without duplication, blocked decisions, HUD/pawn crossover or authority leaks.");

        Add(cases, "F08-19", "Localization", "Seven-language critical UI smoke",
            "Cycle EN/TR/ES/FR/DE/KO/RU through menu, lobby and active match.",
            "Critical action labels/glyphs are readable; no major overlap or missing glyph that blocks play.");

        return cases;
    }

    private static void Add(
        List<RegressionCase> cases,
        string id,
        string category,
        string title,
        string setup,
        string expected)
    {
        cases.Add(new RegressionCase
        {
            id = id,
            category = category,
            title = title,
            setup = setup,
            expected = expected,
            status = CaseStatus.NotTested,
            notes = string.Empty
        });
    }
}
