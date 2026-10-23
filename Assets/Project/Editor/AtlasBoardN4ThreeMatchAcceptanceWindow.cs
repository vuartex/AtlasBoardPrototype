#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Non-authoritative QA *orchestrator*, not a second gameplay system.
/// Reuses existing Regression v1/v2, Phase14J, scene UI and match managers.
/// Manual PASS/FAIL entries are not fabricated by buttons or static tests.
/// </summary>
public sealed class AtlasBoardN4ThreeMatchAcceptanceWindow : EditorWindow
{
    [Serializable] private sealed class Case
    {
        public string id;
        public string label;
        public int[] results = new int[3]; // OPEN, PASS, FAIL, N/A
    }
    [Serializable] private sealed class SaveState
    {
        public string runId;
        public string notes;
        public int selectedMatch;
        public List<Case> cases = new List<Case>();
    }
    private static readonly string[] Status = { "OPEN", "PASS", "FAIL", "N/A" };
    private static readonly string[] Ids = {
        "CREATE_PRIVATE", "CREATE_PUBLIC", "CREATE_BROWSER", "JOIN_READY", "LOBBY_CHAT",
        "MATCH_CHAT", "CHAT_COOLDOWN", "CHAT_120_CHAR", "START_DICE_ORDER", "ROLL_TIMEOUT",
        "DOUBLES_TRIPLE", "BUY_RENT", "AUCTION_HUMAN_BOT", "TRADE_HOST_GUEST",
        "TRADE_GUEST_HOST", "TRADE_CANCEL_TIMEOUT", "CITY_INFO", "DEVELOPMENT",
        "EVENT_RANDOM", "EVENT_MOVE", "TAX_BONUS", "TRAVEL_VACATION_REST",
        "BANKRUPTCY", "RESULT_WINNER_VALUES", "RESULT_GUEST_LEAVE", "REMATCH_HOST",
        "HUD_TABLET", "PAWN_NAMES_TURN", "HOST_AUTHORITY", "REJOIN_TEMP_BOT",
        "COSMETIC_SYNC", "LOCALIZATION_7", "SCREEN_ASPECTS", "NO_STATE_LEAK",
        "NETWORK_ERROR_FREE"
    };
    private static readonly string[] Labels = {
        "Create PRIVATE room", "Create PUBLIC room", "Create room via Browser", "Guest join + READY / NOT READY",
        "Lobby chat, both directions", "Match chat, both directions", "10s server chat cooldown",
        "Chat empty/120/121 character boundaries", "Starting order and dice", "10s Human roll timeout",
        "Doubles and triple-doubles", "Buy / rent and money sync", "Human + Bot auction bid/pass",
        "Host -> Guest trade", "Guest -> Host trade", "Trade cancel + timeout suspension",
        "City Information screen", "Group development + rents", "Random Event/card window",
        "Event movement + no double award", "Tax / Bonus amounts exactly once",
        "Travel / Vacation / Rest actions", "Bankruptcy and property reset", "Result screen and net worth",
        "Guest result (Leave, no Restart)", "Host rematch to lobby", "HUD / Tablet / no overlap",
        "Pawn, player name, turn indicator", "Host migration epoch / single host",
        "Reconnect / temporary bot (5m)", "Cosmetic state on both clients",
        "EN/TR/ES/FR/DE/KO/RU", "1280x1024, 1600x900, 1920x800",
        "Match 2/3 new state / no leakage", "No red exception/stale intents"
    };
    private const string Pref = "ATB_N4_FINAL_THREE_MATCH_OWNER_QA_V1";
    private SaveState state;
    private Vector2 scroll;
    private readonly Dictionary<GameObject,bool> previewOriginals = new Dictionary<GameObject,bool>();

    [MenuItem("Atlas Board/QA & Diagnostics/N4 Final - Three Match Acceptance")]
    public static void Open()
    {
        var w = GetWindow<AtlasBoardN4ThreeMatchAcceptanceWindow>();
        w.titleContent = new GUIContent("N4 3-Match QA");
        w.minSize = new Vector2(850, 640);
        w.Show();
    }

    [MenuItem("Atlas Board/QA & Diagnostics/N4 Final - Chat Wire (No Network)")]
    public static void ChatWire()
    {
        bool pass = AtlasBoardChatRuntimeBridge.ValidateWireParserContract();
        if (pass) Debug.Log("ATB_N4_CHAT_WIRE_QA PASS mode=local_no_network live_chat=UNTESTED");
        else Debug.LogError("ATB_N4_CHAT_WIRE_QA FAIL mode=local_no_network");
    }

    private void OnEnable() { Load(); }
    private void OnDisable() { ClosePreviews(); Save(); }

    private void Load()
    {
        try { state = JsonUtility.FromJson<SaveState>(EditorPrefs.GetString(Pref, string.Empty)); }
        catch { state = null; }
        if (state == null) state = new SaveState();
        if (string.IsNullOrEmpty(state.runId)) state.runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-UTC";
        if (state.cases == null) state.cases = new List<Case>();
        foreach (string id in Ids)
        {
            if (!state.cases.Exists(c => c.id == id)) state.cases.Add(new Case { id = id });
        }
        foreach (Case item in state.cases)
            if (item.results == null || item.results.Length != 3) item.results = new int[3];
    }
    private void Save() { if (state != null) EditorPrefs.SetString(Pref, JsonUtility.ToJson(state)); }
    private void OnGUI()
    {
        if (state == null) Load();
        EditorGUILayout.LabelField("ATLASBOARD N4 — OWNER LIVE / 3 MATCH ACCEPTANCE", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Editor only. Windows Guest EXE has no QA menu. Run Guest manually; never mark PASS just because a button opened a preview. Data is saved in EditorPrefs. No Firebase writes by this window.", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        state.selectedMatch = GUILayout.Toolbar(state.selectedMatch, new[] { "MATCH 1", "MATCH 2", "MATCH 3" });
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("Run ID: " + state.runId);
        bool live = HasOnlineRoom();
        EditorGUILayout.LabelField("Play Mode: " + EditorApplication.isPlaying + " | Online lobby/session: " + live + " | Role: manually verify on both clients");
        EditorGUILayout.HelpBox("Match 1: Host=Unity, Guest follower. Match 2: Guest Host, Unity follower. Match 3: leave/rejoin/host migration and state reset. No automatic result manipulation on live RTDB.", MessageType.None);
        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Regression v1", GUILayout.Height(28))) AtlasBoardGameplayRegressionWindow.Open();
        if (GUILayout.Button("Focused v2", GUILayout.Height(28))) AtlasBoardGameplayRegressionWindowV2.Open();
        if (GUILayout.Button("Phase14J UI / F12", GUILayout.Height(28)))
        {
            if (EditorApplication.isPlaying && !live)
                AtlasBoardPhase14JRuntimeHarness.ShowPanel();
            else Debug.LogWarning("ATB_N4_UI_PREVIEW_BLOCK reason=requires_offline_playmode. Full 14J can reset gameplay surfaces; DO NOT run it in live RTDB.");
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Chat UI toggle")) ToggleChat();
        if (GUILayout.Button("Trade (normal UI)")) OpenTrade();
        if (GUILayout.Button("Event UI preview")) Preview(typeof(EventCardManager), "eventPanel");
        if (GUILayout.Button("Special/Tax UI preview")) Preview(typeof(SpecialTileManager), "specialPanel");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auction UI preview")) Preview(typeof(AuctionManager), "auctionPanel");
        if (GUILayout.Button("Result UI preview")) Preview(typeof(MatchResultManager), "resultPanel");
        if (GUILayout.Button("Close UI previews")) ClosePreviews();
        if (GUILayout.Button("Local Chat wire QA")) ChatWire();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = EditorApplication.isPlaying && !live;
        if (GUILayout.Button(EditorApplication.isPaused ? "RESUME (offline only)" : "FREEZE (offline only)"))
            EditorApplication.isPaused = !EditorApplication.isPaused;
        GUI.enabled = true;
        if (GUILayout.Button("Random Event (OFFLINE only)", GUILayout.Width(230)))
            TriggerOfflineEvent();
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(4);
        EditorGUILayout.LabelField("Mark per-match observations (OPEN / PASS / FAIL / N/A)", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        int col = Mathf.Clamp(state.selectedMatch,0,2);
        for (int i = 0; i < Ids.Length; i++)
        {
            Case c = state.cases.Find(x => x.id == Ids[i]);
            if (c == null) continue;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Ids[i], GUILayout.Width(173));
            EditorGUILayout.LabelField(Labels[i], GUILayout.MinWidth(260), GUILayout.ExpandWidth(true));
            int selected = EditorGUILayout.Popup(c.results[col], Status, GUILayout.Width(87));
            if (selected != c.results[col]) { c.results[col] = selected; Save(); }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        int pass = 0, fail = 0, open = 0, na = 0;
        foreach (Case c in state.cases) foreach(int s in c.results) {
            if (s == 1) pass++; else if (s == 2) fail++; else if (s == 3) na++; else open++;
        }
        EditorGUILayout.LabelField($"Three-match results: PASS={pass}, FAIL={fail}, OPEN={open}, N/A={na}");
        state.notes = EditorGUILayout.TextField("Owner notes", state.notes ?? string.Empty);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Export Markdown report")) Export();
        if (GUILayout.Button("Copy QA summary")) GUIUtility.systemCopyBuffer = Summary(pass,fail,open,na);
        if (GUILayout.Button("Start NEW 3-match run"))
        {
            if (EditorUtility.DisplayDialog("Reset QA run", "Archive the report first. Clear all manual results?", "Clear", "Cancel"))
            { state = null; EditorPrefs.DeleteKey(Pref); Load(); }
        }
        EditorGUILayout.EndHorizontal();
        if (GUI.changed) Save();
    }
    private static bool HasOnlineRoom()
    {
        foreach (AtlasBoardLobbyRuntimeBridge item in Resources.FindObjectsOfTypeAll<AtlasBoardLobbyRuntimeBridge>())
            if (item != null && item.gameObject.scene.IsValid() && item.HasLobby) return true;
        return false;
    }
    private static T FindScene<T>() where T : MonoBehaviour
    {
        foreach (T item in Resources.FindObjectsOfTypeAll<T>())
            if (item != null && item.gameObject.scene.IsValid()) return item;
        return null;
    }
    private static void ToggleChat()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("ATB_N4_UI needs Play Mode."); return; }
        var controller = FindScene<AtlasBoardChatUIController>();
        if (controller == null) { Debug.LogWarning("ATB_N4_UI_PREVIEW chat_missing"); return; }
        var method = typeof(AtlasBoardChatUIController).GetMethod("TogglePanel", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) { Debug.LogWarning("ATB_N4_UI_PREVIEW chat_toggle_missing"); return; }
        try { method.Invoke(controller,null); Debug.Log("ATB_N4_UI_PREVIEW opened=chat ui_only=true live_send=UNTESTED"); }
        catch(Exception e) { Debug.LogError("ATB_N4_UI_PREVIEW chat_failed " + e.GetBaseException().Message); }
    }
    private static void OpenTrade()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("ATB_N4_UI needs Play Mode."); return; }
        TradeManager trade = FindScene<TradeManager>();
        if (trade == null) { Debug.LogWarning("ATB_N4_UI_PREVIEW trade_missing"); return; }
        trade.OpenTradePanel(); // use the original authority-aware entrypoint
        Debug.Log("ATB_N4_UI_PREVIEW requested=trade authority_checked_by_game=true outcome_requires_owner_observation");
    }
    private void Preview(Type componentType, string field)
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("ATB_N4_UI needs Play Mode."); return; }
        Component owner = null;
        foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(componentType))
        {
            Component item = obj as Component;
            if (item != null && item.gameObject.scene.IsValid()) { owner = item; break; }
        }
        if (owner == null) { Debug.LogWarning("ATB_N4_UI_PREVIEW missing_manager=" + componentType.Name); return; }
        var info = componentType.GetField(field,BindingFlags.Instance|BindingFlags.NonPublic);
        GameObject panel = info != null ? info.GetValue(owner) as GameObject : null;
        if (panel == null) { Debug.LogWarning("ATB_N4_UI_PREVIEW missing_panel=" + field); return; }
        if (!previewOriginals.ContainsKey(panel)) previewOriginals[panel] = panel.activeSelf;
        panel.SetActive(true);
        Debug.Log("ATB_N4_UI_PREVIEW opened=" + componentType.Name + " ui_only=true no_money_or_authority_mutation=true game_state_PASS=UNTESTED");
    }
    private void ClosePreviews()
    {
        foreach (var entry in previewOriginals)
            if (entry.Key != null) entry.Key.SetActive(entry.Value);
        previewOriginals.Clear();
    }
    private static void TriggerOfflineEvent()
    {
        if (!EditorApplication.isPlaying || HasOnlineRoom())
        { Debug.LogWarning("ATB_N4_EVENT blocked=true reason=requires_offline_playmode"); return; }
        TurnManager turn = FindScene<TurnManager>();
        EventCardManager events = FindScene<EventCardManager>();
        if (turn == null || events == null || turn.CurrentPlayerState == null || events.IsResolvingEvent)
        { Debug.LogWarning("ATB_N4_EVENT blocked=true reason=not_idle_valid_turn"); return; }
        if (!EditorUtility.DisplayDialog("Offline random Event QA", "This executes an actual random Event on the OFFLINE game state. No rollback is available. Continue?", "Execute", "Cancel")) return;
        events.ResolveRandomEvent(turn.CurrentPlayerState,
            () => Debug.Log("ATB_N4_EVENT_OFFLINE resolved_callback=true owner_qa_required=true"));
    }
    private string Summary(int pass,int fail,int open,int na) =>
        $"ATB_N4_THREE_MATCH_QA run={state.runId} source=owner_manual pass={pass} fail={fail} open={open} na={na} live_backend=OWNER_VERIFICATION_REQUIRED";
    private void Export()
    {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "AtlasBoard_QA_Reports");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "N4_ThreeMatch_" + state.runId + ".md");
        var b = new StringBuilder();
        b.AppendLine("# AtlasBoard N4 three-match acceptance (owner manual)");
        b.AppendLine("Run: " + state.runId + "  ");
        b.AppendLine("Editor + separate Windows Guest; no Editor QA on Guest.  ");
        b.AppendLine("**Preview buttons do not imply gameplay PASS.**  ");
        b.AppendLine("| Case | Match 1 | Match 2 | Match 3 |");
        b.AppendLine("|---|---|---|---|");
        foreach (Case c in state.cases)
            b.AppendLine("| " + c.id + " | " + Status[c.results[0]] + " | " + Status[c.results[1]] + " | " + Status[c.results[2]] + " |");
        b.AppendLine("\nOwner notes: " + (state.notes ?? string.Empty).Replace("|", "\\|").Replace("\n", " "));
        File.WriteAllText(path, b.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log("ATB_N4_THREE_MATCH_REPORT exported=true path=" + path);
        EditorUtility.RevealInFinder(path);
    }
}
#endif
