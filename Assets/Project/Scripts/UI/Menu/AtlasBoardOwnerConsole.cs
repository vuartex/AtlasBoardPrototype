#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Session-only owner QA console. The build contains salted SHA256 verifiers,
/// never literal owner access codes. The local code is a convenience factor,
/// NOT authorization: the canonical Firebase UID must also be administrator
/// allowlisted by /devWalletTesters, which clients cannot self-modify.
/// Public release players cannot execute developer mutation commands.
/// </summary>
[DefaultExecutionOrder(-32000)]
public sealed class AtlasBoardOwnerConsole : MonoBehaviour
{
    private const string Salt = "ATB-BI-C0N50L3-2026-11";
    // These values are generated for this owner checkpoint. Secrets are given
    // separately and must NEVER be copied into Assets, Git or release notes.
    private const string UnlockDigest = "79638DAF31BED6FBA6697090493D44088CCF46F49923BFCD6D3F5050B58ED5A0";
    private const string FinalRoundDigest = "2D984F5DA26E109C880D34D5497668ED4C67A97D201F9B1E751B3D14453B9C23";
    private const string FinishTurnDigest = "AFD5E889BFC7776B95097327CA2E26F250614EEC732E50F853C9082EC5A4F2C1";
    private const string Phase14JDigest = "1FE1985A7B9CEC941DD5AFF228CDC3B101D8EF9415BDA15FA82B56B3E0407D74";
    private const string GrantDigest = "24B287A91618F117DEE08AD3697B9662FF29547C1174D9F48B8A9B3ACB25BB3F";
    private const string XpPreviewDigest = "A5EE6E69D1996B49B7E3999A29D5CCEE4DBEAEE4706EDF98A7E494936B40CA25";

    private static AtlasBoardOwnerConsole instance;
    private bool visible;
    private bool unlocked;
    private bool checking;
    private bool harnessVisible;
    private int failures;
    private string entry = string.Empty;
    private string status = "SESSION LOCKED";
    private string ownerUid = string.Empty;
    private Rect window = new Rect(80, 70, 620, 275);
    private GUIStyle fieldStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        GameObject root = new GameObject("AtlasBoard_Owner_QA_Console");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<AtlasBoardOwnerConsole>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit()
    {
        // No credentials or grant status are written to PlayerPrefs or disk.
        unlocked = false;
        ownerUid = string.Empty;
        entry = string.Empty;
    }

    private void Update()
    {
        bool qaKeyPressed = false;
        bool toggle = false;
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        if (k != null)
        {
            bool ctrl = k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed;
            toggle = k.backquoteKey.wasPressedThisFrame || (ctrl && k.f10Key.wasPressedThisFrame);
            qaKeyPressed = k.f8Key.wasPressedThisFrame || k.f9Key.wasPressedThisFrame ||
                k.f10Key.wasPressedThisFrame || k.f11Key.wasPressedThisFrame ||
                k.f12Key.wasPressedThisFrame;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        toggle = Input.GetKeyDown(KeyCode.BackQuote) ||
            ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F10));
        qaKeyPressed = Input.GetKeyDown(KeyCode.F8) || Input.GetKeyDown(KeyCode.F9) ||
            Input.GetKeyDown(KeyCode.F10) || Input.GetKeyDown(KeyCode.F11) || Input.GetKeyDown(KeyCode.F12);
#endif
        if (toggle) visible = !visible;
        if (unlocked && !string.Equals(ownerUid, AtlasBoardRtdbMetaEconomy.CurrentUid, StringComparison.Ordinal))
        {
            unlocked = false;
            ownerUid = string.Empty;
            harnessVisible = false;
            status = "IDENTITY CHANGED — SESSION LOCKED";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Pre-existing QA components have their own F8/F9/F10... hotkeys.
        // Keep them disabled unless the current console action needs the UI.
        // Our early execution order prevents direct keys reaching the harness.
        Type fastType = typeof(AtlasBoardDevelopmentMatchFastForwardQA);
        if (fastType != null)
        {
            Behaviour fast = UnityEngine.Object.FindAnyObjectByType(fastType) as Behaviour;
            if (fast != null && fast.enabled) fast.enabled = false;
        }
        Type harnessType = typeof(AtlasBoardPhase14JRuntimeHarness);
        if (harnessType != null)
        {
            Behaviour harness = UnityEngine.Object.FindAnyObjectByType(harnessType) as Behaviour;
            if (harness != null)
            {
                bool permitted = unlocked && harnessVisible && !visible && !qaKeyPressed;
                if (harness.enabled != permitted) harness.enabled = permitted;
            }
        }
#endif
    }

    private void OnGUI()
    {
        if (!visible) return;
        window.x = Mathf.Clamp(window.x, 8f, Mathf.Max(8f, Screen.width - window.width - 8f));
        window.y = Mathf.Clamp(window.y, 8f, Mathf.Max(8f, Screen.height - window.height - 8f));
        window = GUI.Window(190015, window, DrawConsole, "ATLAS BOARD  |  OWNER QA CONSOLE");
    }

    private void DrawConsole(int id)
    {
        GUILayout.Space(12);
        GUILayout.Label(unlocked ? "SESSION: UNLOCKED (UID + SERVER ALLOWLIST VERIFIED)" : "SESSION: LOCKED");
        GUILayout.Label("Open/close: ~ or Ctrl+F10. Commands are not saved or logged.");
        GUILayout.Label("Owner QA commands work only with an approved Firebase account.");
        GUILayout.Space(12);
        GUI.SetNextControlName("ATB_Console_Input");
        entry = GUILayout.TextField(entry, 64, GUILayout.Height(30));
        Event ev = Event.current;
        if (ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter))
        {
            ev.Use();
            Submit();
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("EXECUTE", GUILayout.Height(30))) Submit();
        if (GUILayout.Button("LOCK SESSION", GUILayout.Height(30)))
        {
            unlocked = false; ownerUid = string.Empty; harnessVisible = false;
            entry = string.Empty; status = "SESSION LOCKED";
        }
        if (GUILayout.Button("CLOSE", GUILayout.Height(30))) visible = false;
        GUILayout.EndHorizontal();
        GUILayout.Space(12);
        GUILayout.Label("STATUS: " + status);
        GUILayout.Label("Note: XP preview is local-only; no persistent XP mutation is permitted.");
        GUI.DragWindow(new Rect(0, 0, 10000, 25));
    }

    private async void Submit()
    {
        string entered = (entry ?? string.Empty).Trim().ToUpperInvariant();
        entry = string.Empty;
        if (checking || entered.Length == 0) return;
        if (failures >= 8) { status = "ATTEMPT LIMIT REACHED — RESTART REQUIRED"; return; }
        if (!unlocked)
        {
            if (!EqualDigest(entered, UnlockDigest)) { failures++; status = "INVALID SESSION CODE"; return; }
            checking = true; status = "CHECKING ACCOUNT AUTHORIZATION...";
            try
            {
                AtlasBoardMetaEconomySnapshot wallet = await AtlasBoardRtdbMetaEconomy.LoadAsync();
                string uid = AtlasBoardRtdbMetaEconomy.CurrentUid;
                if (wallet != null && wallet.Success && wallet.WalletVerified && wallet.IsDeveloperTester &&
                    !string.IsNullOrWhiteSpace(uid))
                {
                    unlocked = true; ownerUid = uid; status = "SESSION UNLOCKED — QA ONLY";
                }
                else status = "ACCESS DENIED — UID NOT SERVER-AUTHORIZED";
            }
            catch (Exception) { status = "AUTHORIZATION UNAVAILABLE — FAIL CLOSED"; }
            finally { checking = false; }
            return;
        }

        if (!string.Equals(ownerUid, AtlasBoardRtdbMetaEconomy.CurrentUid, StringComparison.Ordinal))
        {
            unlocked = false; ownerUid = string.Empty; status = "SESSION LOCKED — ACCOUNT CHANGED"; return;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (EqualDigest(entered, FinalRoundDigest)) { RunFastForward(false); return; }
        if (EqualDigest(entered, FinishTurnDigest)) { RunFastForward(true); return; }
        if (EqualDigest(entered, Phase14JDigest)) { OpenPhaseHarness(); return; }
        if (EqualDigest(entered, XpPreviewDigest))
        {
            status = "XP TEST PREVIEW ONLY — +100 SIMULATED XP. NO SAVE / NO REWARD";
            return;
        }
        if (EqualDigest(entered, GrantDigest))
        {
            checking = true; status = "REQUESTING SERVER-VALIDATED DEV_GRANT...";
            try
            {
                AtlasBoardMetaEconomyOperationResult result = await AtlasBoardRtdbMetaEconomy.GrantDevAsync();
                status = result != null && result.Success
                    ? "DEV_GRANT ACCEPTED BY RTDB RULES (+5000 GOLD, +500 ATLAS COIN)"
                    : "DEV_GRANT REFUSED BY SERVER — NO LOCAL CURRENCY WRITTEN";
            }
            catch (Exception) { status = "DEV_GRANT FAILED — SERVER AUTHORITY UNCHANGED"; }
            finally { checking = false; }
            return;
        }
#else
        status = "RETAIL RELEASE: PRIVILEGED QA COMMANDS NOT COMPILED";
        return;
#endif
        failures++;
        status = "UNKNOWN CODE";
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void RunFastForward(bool finish)
    {
        Type t = typeof(AtlasBoardDevelopmentMatchFastForwardQA);
        MethodInfo method = t?.GetMethod("ApplyFinalRoundState", BindingFlags.Static | BindingFlags.NonPublic);
        if (method == null) { status = "QA FAST FORWARD NOT AVAILABLE"; return; }
        try { method.Invoke(null, new object[] { finish }); status = finish ? "FINAL TURN QA REQUESTED" : "FINAL ROUND QA REQUESTED"; }
        catch (Exception) { status = "QA REJECTED — CHECK UNITY CONSOLE / HOST AUTHORITY"; }
    }

    private void OpenPhaseHarness()
    {
        Type t = typeof(AtlasBoardPhase14JRuntimeHarness);
        MethodInfo method = t?.GetMethod("ShowPanel", BindingFlags.Static | BindingFlags.Public);
        if (method == null) { status = "PHASE14J HARNESS NOT AVAILABLE"; return; }
        harnessVisible = true;
        visible = false;
        method.Invoke(null, null);
        status = "PHASE14J QA WINDOW OPENED";
    }
#endif

    private static bool EqualDigest(string input, string expected)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Salt + ":" + input);
        byte[] actual;
        using (SHA256 sha = SHA256.Create()) actual = sha.ComputeHash(bytes);
        string digest = BitConverter.ToString(actual).Replace("-", string.Empty);
        // Fixed work length avoids accidentally accepting short prefix matches.
        int diff = digest.Length ^ expected.Length;
        int len = Math.Min(digest.Length, expected.Length);
        for (int i = 0; i < len; i++) diff |= digest[i] ^ expected[i];
        return diff == 0;
    }
}

#endif // Public release builds contain no owner QA console class.
