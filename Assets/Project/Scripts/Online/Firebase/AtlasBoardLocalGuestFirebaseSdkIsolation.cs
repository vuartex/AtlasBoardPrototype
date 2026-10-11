#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using UnityEngine;

/// <summary>
/// Temporary Phase 16 isolation for local Windows Guest tests only.
/// The authoritative lobby bridge already signs the Guest into the Auth emulator
/// through its own HTTP flow. Avoid starting a SECOND FirebaseAuth + Firestore
/// SDK sign-in in the same process while investigating the native startup crash.
/// This does not change Editor, Release, server, snapshots, or reconnect logic.
/// </summary>
[DefaultExecutionOrder(-30000)]
internal sealed class AtlasBoardLocalGuestFirebaseSdkIsolation : MonoBehaviour
{
    private const string LocalEmulatorsSwitch = "-atlasLocalEmulators";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!WasLocalEmulatorModeExplicitlyRequested())
        {
            return;
        }

        var root = new GameObject("AtlasBoard_LocalGuestFirebaseSdkIsolation");
        DontDestroyOnLoad(root);
        root.AddComponent<AtlasBoardLocalGuestFirebaseSdkIsolation>();
    }

    private void Start()
    {
        // The original Phase15B bootstrap runs AfterSceneLoad, and its Start
        // has execution order +15050. Our -30000 Start runs first, so disabling
        // that component prevents its SDK sign-in and Firestore initialization.
        var unifiedSession =
            FindAnyObjectByType<AtlasBoardPhase15BUnifiedEmulatorSession>();

        if (unifiedSession == null)
        {
            Debug.LogWarning(
                "AtlasBoard LOCAL GUEST ISOLATION: Phase15B component not " +
                "found. No SDK sign-in was disabled.");
        }
        else
        {
            unifiedSession.enabled = false;
            Debug.Log(
                "AtlasBoard LOCAL GUEST ISOLATION ACTIVE: Phase15B " +
                "secondary FirebaseAuth/Firestore SDK sign-in suppressed. " +
                "Authoritative emulator HTTP lobby identity remains enabled. " +
                "Editor/Release behavior is unchanged.");
        }

        // Keep no recurring Update, polling or per-frame work.
        Destroy(this);
    }

    private static bool WasLocalEmulatorModeExplicitlyRequested()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], LocalEmulatorsSwitch,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
#endif
