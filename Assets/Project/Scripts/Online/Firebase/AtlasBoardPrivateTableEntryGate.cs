using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Keeps Private Table's online chooser distinct from explicit Local Offline.
/// Builds ONE independent Local Offline button with no serialized Cancel
/// handlers or inherited localization components. Never fabricates a room code.
/// </summary>
[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public sealed class AtlasBoardPrivateTableEntryGate : MonoBehaviour
{
    public const string LocalButtonName = "Button_PrivateTable_LocalOffline_Explicit";

    private static AtlasBoardPrivateTableEntryGate instance;
    private AtlasBoardPrivateLobbyUIController lobby;
    private AtlasBoardOfflineModeBootstrap offline;
    private GameObject entryOverlay;
    private Button localButton;
    private TMP_Text localLabel;
    private bool explicitOffline;
    private bool wasChooserOpen;
    private float nextResolveAt;

    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject root = new GameObject("AtlasBoard_Private_Table_Mode_Gate");
        DontDestroyOnLoad(root);
        root.AddComponent<AtlasBoardPrivateTableEntryGate>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        DestroyLocalButton();
        if (offline != null) offline.enabled = true;
        instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DestroyLocalButton();
        lobby = null;
        offline = null;
        entryOverlay = null;
        wasChooserOpen = false;
        explicitOffline = false;
        nextResolveAt = 0f;
    }

    private void Update()
    {
        if (lobby == null || entryOverlay == null || offline == null)
        {
            if (Time.unscaledTime < nextResolveAt) return;
            nextResolveAt = Time.unscaledTime + 1f;
            ResolveSceneReferences();
        }
        if (lobby == null || entryOverlay == null || offline == null) return;

        bool open = entryOverlay.activeInHierarchy;
        if (open)
        {
            if (offline.enabled)
            {
                offline.enabled = false;
                Debug.Log("ATB_PRIVATE_ENTRY chooser=online_or_explicit_offline automatic_local_redirect=false");
            }
            if (!wasChooserOpen)
            {
                EnsureLocalButton();
                wasChooserOpen = true;
            }
            if (localLabel != null)
            {
                string translated = LocalLabel();
                if (!string.Equals(localLabel.text, translated, StringComparison.Ordinal))
                    localLabel.text = translated;
            }
        }
        else
        {
            wasChooserOpen = false;
            if (!explicitOffline && !offline.enabled) offline.enabled = true;
        }

        if (explicitOffline && !open && !ReadBool(lobby, "roomActive"))
        {
            explicitOffline = false;
            offline.enabled = true;
        }
    }

    private void ResolveSceneReferences()
    {
        if (offline == null)
            offline = FindAnyObjectByType<AtlasBoardOfflineModeBootstrap>(
                FindObjectsInactive.Include);
        if (lobby == null)
            lobby = FindAnyObjectByType<AtlasBoardPrivateLobbyUIController>(
                FindObjectsInactive.Include);
        if (lobby != null && entryOverlay == null)
        {
            FieldInfo field = typeof(AtlasBoardPrivateLobbyUIController)
                .GetField("roomEntryOverlay", PrivateInstance);
            entryOverlay = field != null ? field.GetValue(lobby) as GameObject : null;
        }
    }

    private void EnsureLocalButton()
    {
        Button cancel = ReadField<Button>(lobby, "cancelEntryButton");
        Button create = ReadField<Button>(lobby, "createRoomButton");
        if (cancel == null) return;

        // Previous versions cloned Cancel (including its event/localizer).
        // Remove ONLY those generated controls, never the real Cancel button.
        Transform parent = cancel.transform.parent;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == LocalButtonName &&
                (localButton == null || child.gameObject != localButton.gameObject))
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }
        if (localButton != null && localButton.transform.parent == parent)
        {
            PositionLocalButton(cancel, create, localButton.GetComponent<RectTransform>());
            return;
        }

        GameObject obj = new GameObject(
            LocalButtonName, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        Image source = cancel.GetComponent<Image>();
        Image target = obj.GetComponent<Image>();
        if (source != null)
        {
            target.sprite = source.sprite;
            target.type = source.type;
            target.color = source.color;
            target.material = source.material;
            target.preserveAspect = source.preserveAspect;
        }
        Button button = obj.GetComponent<Button>();
        button.transition = cancel.transition;
        button.colors = cancel.colors;
        button.spriteState = cancel.spriteState;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.targetGraphic = target;
        // A NEW button with EXACTLY one runtime action, zero inherited
        // serialized listeners or EventTriggers. Cancelling never creates a room.
        button.onClick.AddListener(EnterExplicitLocalOffline);

        RectTransform rectangle = obj.GetComponent<RectTransform>();
        PositionLocalButton(cancel, create, rectangle);

        TMP_Text sourceText = cancel.GetComponentInChildren<TMP_Text>(true);
        GameObject textObject = new GameObject(
            "Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(obj.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(5f, 3f);
        textRect.offsetMax = new Vector2(-5f, -3f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (sourceText != null)
        {
            text.font = sourceText.font;
            text.fontSharedMaterial = sourceText.fontSharedMaterial;
            text.color = sourceText.color;
            text.fontSize = sourceText.fontSize;
            text.fontStyle = sourceText.fontStyle;
        }
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = sourceText != null
            ? Mathf.Max(sourceText.fontSize, 13f) : 16f;
        text.raycastTarget = false;
        text.text = LocalLabel();

        localButton = button;
        localLabel = text;
        Debug.Log("ATB_PRIVATE_ENTRY local_button=independent aligned=true " +
                  "cancel_listener_cloned=false duplicates_removed=true");
    }

    private static void PositionLocalButton(
        Button cancel, Button create, RectTransform localRect)
    {
        RectTransform source = cancel.GetComponent<RectTransform>();
        if (source == null || localRect == null) return;
        localRect.anchorMin = source.anchorMin;
        localRect.anchorMax = source.anchorMax;
        localRect.pivot = source.pivot;
        localRect.sizeDelta = source.sizeDelta;
        localRect.localScale = source.localScale;
        Vector2 pos = source.anchoredPosition;
        RectTransform createRect = create != null
            ? create.GetComponent<RectTransform>() : null;
        if (createRect != null && createRect.parent == source.parent &&
            createRect.anchorMin == source.anchorMin &&
            createRect.anchorMax == source.anchorMax)
        {
            // Place at the midpoint: equal free space above and below.
            // Preserve the exact X coordinate of Create/Cancel's left column.
            pos.y = (source.anchoredPosition.y +
                     createRect.anchoredPosition.y) * 0.5f;
        }
        else
        {
            // Fallback if the scene's two buttons are not siblings.
            pos.y += Mathf.Max(55f, source.rect.height + 18f);
        }
        localRect.anchoredPosition = pos;
    }

    private static string LocalLabel()
    {
        return AtlasBoardL.R(
            "PLAY LOCAL OFFLINE", "YEREL ÇEVRİMDIŞI OYNA",
            "JUGAR SIN CONEXIÓN", "JOUER HORS LIGNE",
            "LOKAL OFFLINE SPIELEN", "로컬 오프라인 플레이",
            "ИГРАТЬ ЛОКАЛЬНО");
    }

    private void DestroyLocalButton()
    {
        if (localButton != null)
        {
            localButton.gameObject.SetActive(false);
            Destroy(localButton.gameObject);
        }
        localButton = null;
        localLabel = null;
    }

    private void EnterExplicitLocalOffline()
    {
        if (lobby == null || offline == null || entryOverlay == null ||
            !entryOverlay.activeInHierarchy) return;
        MethodInfo route = typeof(AtlasBoardOfflineModeBootstrap).GetMethod(
            "RoutePrivateTableToLocalLobby", PrivateInstance);
        if (route == null)
        {
            Debug.LogError("ATB_PRIVATE_ENTRY offline_route=unavailable");
            return;
        }
        explicitOffline = true;
        try
        {
            route.Invoke(offline, null);
            Debug.Log("ATB_PRIVATE_ENTRY selected=local_offline " +
                      "invite_code=none remote_join=false cloud_rewards=false");
        }
        catch (Exception error)
        {
            explicitOffline = false;
            Debug.LogError("ATB_PRIVATE_ENTRY offline_route=failed type=" +
                           error.GetType().Name);
        }
    }

    private static T ReadField<T>(object owner, string fieldName) where T : class
    {
        if (owner == null) return null;
        FieldInfo info = owner.GetType().GetField(fieldName, PrivateInstance);
        return info != null ? info.GetValue(owner) as T : null;
    }

    private static bool ReadBool(object owner, string fieldName)
    {
        if (owner == null) return false;
        FieldInfo field = owner.GetType().GetField(fieldName, PrivateInstance);
        return field != null && field.GetValue(owner) is bool value && value;
    }
}
