#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only QA. In Play Mode the selected account can receive mission
/// progress when the RTDB devWalletTesters allowlist explicitly permits it.
/// Reward claims still go through Store > Event > Rewards, with server rules.
/// </summary>
public sealed class AtlasBoardWorldJourneyQaWindow : EditorWindow
{
    private string status = "Start Unity Play Mode, sign in, then select a QA action.";
    private bool busy;

    [MenuItem("Atlas Board/QA & Diagnostics/World Journey (Editor Only)")]
    public static void ShowWindow() =>
        GetWindow<AtlasBoardWorldJourneyQaWindow>("World Journey QA");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Atlas World Journey · RTDB QA", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Works in Unity Play Mode. The test account must have " +
            "devWalletTesters/{uid} = true in RTDB. " +
            "Normal players NEVER get this shortcut. " +
            "No Cloud Functions and no Android/Windows build required.",
            MessageType.Info);
        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying || busy);
        if (GUILayout.Button("Complete All 7 Mission Requirements"))
            SetProgress(true);
        if (GUILayout.Button("Clear QA / Reset Claims (keep earned items)"))
            ResetClaims();
        if (GUILayout.Button("Clear QA Mission Counters Only"))
            SetProgress(false);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.LabelField("Next: In Play Mode open Store > Event > Rewards;");
        EditorGUILayout.LabelField("claim rewards one by one, then refresh Event.");
        EditorGUILayout.LabelField("World Map unlocks as mission 8;");
        EditorGUILayout.LabelField("Atlas Horizon Board unlocks after mission 8.");
        EditorGUILayout.HelpBox(
            "Only the allowlisted QA UID can reset World Journey claim markers. " +
            "Previously granted money, XP, maps and boards are NOT removed. " +
            "Reclaiming an already owned map/board grants fixed Gold instead.",
            MessageType.Warning);
    }

    private async void ResetClaims()
    {
        busy = true;
        status = "Checking QA authorization and resetting claimed markers...";
        Repaint();
        try
        {
            status = await AtlasBoardRtdbMetaEconomy.WorldEditorResetClaimsAsync();
        }
        catch (System.Exception error)
        {
            status = "QA reset failed: " + error.Message;
        }
        finally { busy = false; Repaint(); }
    }

    private async void SetProgress(bool complete)
    {
        busy = true;
        status = "Checking Firebase account and updating QA counters...";
        Repaint();
        try
        {
            status = await AtlasBoardRtdbMetaEconomy
                .WorldEditorSetProgressAsync(complete);
        }
        catch (System.Exception error)
        {
            status = "QA failed: " + error.Message;
        }
        finally
        {
            busy = false;
            Repaint();
        }
    }
}
#endif
