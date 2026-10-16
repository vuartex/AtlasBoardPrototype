using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Cross-platform presentation gate. UXOverlayController hides PlayerHudPanel
/// objects when the tablet is open, but independent identity/money refreshes
/// can reactivate individual panels between overlay updates. CanvasGroup
/// controls the final draw and hit-testing without changing game authority,
/// player participation, or HUD Refresh bindings.
/// </summary>
[DefaultExecutionOrder(30000)]
[DisallowMultipleComponent]
public sealed class AtlasBoardTabletHudVisibilityGate : MonoBehaviour
{
    private static AtlasBoardTabletHudVisibilityGate instance;

    private static readonly FieldInfo TabletRootField =
        typeof(TabletUIManager).GetField(
            "tabletRoot", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo StatusBarField =
        typeof(UXOverlayController).GetField(
            "statusBarRect", BindingFlags.NonPublic | BindingFlags.Instance);

    private TabletUIManager tabletManager;
    private GameObject tabletRoot;
    private UXOverlayController overlay;
    private PlayerHudPanel[] huds = Array.Empty<PlayerHudPanel>();
    private CanvasGroup[] hudGroups = Array.Empty<CanvasGroup>();
    private CanvasGroup statusGroup;
    private float nextResolveAt;
    private bool? lastTabletOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null ||
            UnityEngine.Object.FindAnyObjectByType<BoardPath>(FindObjectsInactive.Include) == null)
            return;

        GameObject host = new GameObject("AtlasBoard_Tablet_HUD_Visibility_Gate");
        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardTabletHudVisibilityGate>();
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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResetBindings();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetBindings();
    }

    private void ResetBindings()
    {
        tabletManager = null;
        tabletRoot = null;
        overlay = null;
        huds = Array.Empty<PlayerHudPanel>();
        hudGroups = Array.Empty<CanvasGroup>();
        statusGroup = null;
        nextResolveAt = 0f;
        lastTabletOpen = null;
    }

    private void LateUpdate()
    {
        // No hierarchy-wide search or UI mutations each frame.  The cached
        // CanvasGroups remain effective even if another component SetActive's
        // a HUD while the tablet is already open.
        if (Time.unscaledTime >= nextResolveAt && NeedsBind())
        {
            ResolveReferences();
            // Recheck while the scene is being constructed; stop scene-wide
            // discovery once all required references have been resolved.
            nextResolveAt = Time.unscaledTime + 1f;
        }

        bool tabletOpen = tabletRoot != null && tabletRoot.activeInHierarchy;
        // IMPORTANT: Another system may call SetActive/CanvasGroup changes
        // after the previous frame, even without a tablet-mode transition.
        // Reassert on *cached* groups only. ApplyGroup skips unchanged values:
        // there are zero scene searches and zero Canvas writes on stable frames.
        ApplyVisibility(tabletOpen);
        lastTabletOpen = tabletOpen;
    }

    private bool NeedsBind()
    {
        if (tabletManager == null || tabletRoot == null || overlay == null ||
            statusGroup == null || hudGroups.Length == 0)
        {
            return true;
        }
        for (int i = 0; i < huds.Length; ++i)
        {
            if (huds[i] == null || hudGroups[i] == null)
                return true;
        }
        return false;
    }

    private void ResolveReferences()
    {
        TabletUIManager resolvedManager = UnityEngine.Object.FindAnyObjectByType<TabletUIManager>(
            FindObjectsInactive.Include);
        if (resolvedManager != tabletManager || tabletRoot == null)
        {
            tabletManager = resolvedManager;
            tabletRoot = tabletManager != null && TabletRootField != null
                ? TabletRootField.GetValue(tabletManager) as GameObject
                : null;
            lastTabletOpen = null;
        }

        UXOverlayController resolvedOverlay = UnityEngine.Object.FindAnyObjectByType<UXOverlayController>(
            FindObjectsInactive.Include);
        if (resolvedOverlay != overlay || statusGroup == null)
        {
            overlay = resolvedOverlay;
            RectTransform bar = overlay != null && StatusBarField != null
                ? StatusBarField.GetValue(overlay) as RectTransform
                : null;
            statusGroup = EnsureGroup(bar != null ? bar.gameObject : null);
            lastTabletOpen = null;
        }

        PlayerHudPanel[] found = UnityEngine.Object.FindObjectsByType<PlayerHudPanel>(
            FindObjectsInactive.Include);
        bool changed = found.Length != huds.Length;
        if (!changed)
        {
            for (int i = 0; i < found.Length; ++i)
            {
                if (Array.IndexOf(huds, found[i]) < 0)
                {
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
        {
            huds = found;
            hudGroups = new CanvasGroup[huds.Length];
            for (int i = 0; i < huds.Length; ++i)
            {
                hudGroups[i] = huds[i] != null
                    ? EnsureGroup(huds[i].gameObject)
                    : null;
            }
            lastTabletOpen = null;
        }
    }

    private static CanvasGroup EnsureGroup(GameObject target)
    {
        if (target == null)
        {
            return null;
        }
        CanvasGroup group = target.GetComponent<CanvasGroup>();
        return group != null ? group : target.AddComponent<CanvasGroup>();
    }

    private void ApplyVisibility(bool tabletOpen)
    {
        // We deliberately never force activeSelf=true or change the original
        // per-player logic; the standard UX overlay remains responsible for it.
        float alpha = tabletOpen ? 0f : 1f;
        bool hitTest = !tabletOpen;
        for (int i = 0; i < hudGroups.Length; ++i)
        {
            ApplyGroup(hudGroups[i], alpha, hitTest);
        }
        ApplyGroup(statusGroup, alpha, hitTest);
    }

    private static void ApplyGroup(CanvasGroup group, float alpha, bool hitTest)
    {
        if (group == null)
        {
            return;
        }
        if (!Mathf.Approximately(group.alpha, alpha)) group.alpha = alpha;
        if (group.blocksRaycasts != hitTest) group.blocksRaycasts = hitTest;
        if (group.interactable != hitTest) group.interactable = hitTest;
    }
}
