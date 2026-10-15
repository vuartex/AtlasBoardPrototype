using UnityEngine;

/// <summary>
/// Compatibility marker left in place for projects that already received the
/// v5.29 runtime-integrity file. v5.30 consolidates the actual mobile runtime
/// behavior into AtlasBoardMobileGameplayUX so the same UI/performance fixes
/// cannot be applied twice after scene reloads.
/// </summary>
[DefaultExecutionOrder(14500)]
[DisallowMultipleComponent]
public sealed class AtlasBoardRuntimeIntegrityV529 : MonoBehaviour
{
    private static AtlasBoardRuntimeIntegrityV529 instance;
    private bool logged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        if (instance != null)
            return;

        GameObject host = new GameObject("AtlasBoard_Runtime_Integrity_v529_Compatibility");
        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardRuntimeIntegrityV529>();
#endif
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Update()
    {
        if (logged)
            return;

        AtlasBoardMobileGameplayUX owner =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardMobileGameplayUX>(
                FindObjectsInactive.Include);

        if (owner == null)
            return;

        logged = true;
        Debug.Log(
            "ATB_RUNTIME_INTEGRITY_V529 compatibility=true " +
            "runtime_owner=AtlasBoardMobileGameplayUX duplicate_mutation=false");
    }
}
