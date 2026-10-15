using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// All-platform, locally scoped recovery for Main Menu -> match -> Main Menu.
/// It never replaces a network transport, fabricates an online room, writes
/// progression, or changes Android/Steam settings. In particular a Steam
/// persona is only used to label a LOCAL human PlayerGameState.
/// </summary>
[DefaultExecutionOrder(15500)]
public sealed class AtlasBoardSharedRuntimeRecovery : MonoBehaviour
{
    private static AtlasBoardSharedRuntimeRecovery instance;
    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo DisplayNameField =
        typeof(PlayerGameState).GetField("displayName", PrivateInstance);
    private static readonly FieldInfo IdentityChangedField =
        typeof(PlayerGameState).GetField("IdentityChanged", PrivateInstance);
    private static readonly MethodInfo OpenStoreMethod =
        typeof(AtlasBoardMetaEconomyUI).GetMethod("OpenFromMainMenu", PrivateInstance);

    private BoardPath board;
    private TurnManager turn;
    private bool wasInMatch;
    private bool resetting;
    private float nextAuditAt;
    private float nextNameAt;
    private float nextShopAt;
    private Button shopButton;
    private bool loggedShopBinding;
    private PlayerGameState[] trackedPlayers = Array.Empty<PlayerGameState>();
    private AtlasBoardTurnDiceNetworkCoordinator trackedCoordinator;
    private AtlasBoardMatchRuntimeBridge trackedMatchBridge;
    private TMP_Text statusText;
    private TMP_Text legacyStatusText;
    private TMP_Text tradeFromText;
    private TMP_Text tradeToText;
    private TMP_Text tradeHintText;
    private TMP_Text propertyPlayerText;
    private float nextStatusResolve;
    private bool steamPlayerBindingsReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject host = new GameObject("AtlasBoard_Shared_Runtime_Recovery");
        DontDestroyOnLoad(host);
        host.AddComponent<AtlasBoardSharedRuntimeRecovery>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        board = null;
        turn = null;
        wasInMatch = false;
        shopButton = null;
        loggedShopBinding = false;
        nextAuditAt = nextShopAt = nextNameAt = 0f;
        statusText = legacyStatusText = tradeFromText = tradeToText =
            tradeHintText = propertyPlayerText = null;
        trackedPlayers = Array.Empty<PlayerGameState>();
        trackedCoordinator = null;
        trackedMatchBridge = null;
        nextStatusResolve = 0f;
        steamPlayerBindingsReady = false;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        if (now >= nextAuditAt)
        {
            nextAuditAt = now + 0.9f;
            AuditGameSession();
        }
        if (now >= nextShopAt)
        {
            nextShopAt = now + 0.8f;
            EnsureShopEntry();
        }
        if (now >= nextNameAt)
        {
            nextNameAt = now + 0.8f;
            SynchronizeLocallyOwnedSteamName();
        }
    }

    private void LateUpdate()
    {
        string persona = AtlasBoardCanonicalPlayerIdentity.ValidSteamPersona();
        if (string.IsNullOrWhiteSpace(persona)) return;
        // Cached player references only, with no hierarchy search per frame.
        // The authoritative PlayerGameState is synchronized in Update().
        if (Time.unscaledTime >= nextStatusResolve)
        {
            nextStatusResolve = Time.unscaledTime + 5f;
            UXOverlayController overlay = FindScene<UXOverlayController>();
            AtlasBoardTradePresentationPolish trade = FindScene<AtlasBoardTradePresentationPolish>();
            AtlasBoardPurchasePresentationPolish purchase = FindScene<AtlasBoardPurchasePresentationPolish>();
            statusText = ReadLabel(overlay, "statusText");
            legacyStatusText = ReadLabel(overlay, "legacyTurnStatusText");
            tradeFromText = ReadLabel(trade, "fromNameText");
            tradeToText = ReadLabel(trade, "toNameText");
            tradeHintText = ReadLabel(trade, "setupHintText");
            propertyPlayerText = ReadLabel(purchase, "playerText");
        }
        string guest = AtlasBoardGuestIdentity.DisplayName;
        if (string.IsNullOrWhiteSpace(guest) || guest == persona) return;
        ReplaceLocalGuest(statusText, guest, persona);
        ReplaceLocalGuest(legacyStatusText, guest, persona);
        ReplaceLocalGuest(tradeFromText, guest, persona);
        ReplaceLocalGuest(tradeToText, guest, persona);
        ReplaceLocalGuest(tradeHintText, guest, persona);
        ReplaceLocalGuest(propertyPlayerText, guest, persona);
    }

    private static TMP_Text ReadLabel(Component owner, string field)
    {
        if (owner == null) return null;
        FieldInfo info = owner.GetType().GetField(field, PrivateInstance);
        return info != null ? info.GetValue(owner) as TMP_Text : null;
    }

    private static void ReplaceLocalGuest(TMP_Text target, string guest, string steam)
    {
        if (target == null || string.IsNullOrEmpty(target.text)) return;
        if (target.text.IndexOf(guest, StringComparison.Ordinal) < 0) return;
        target.text = target.text.Replace(guest, steam);
    }

    private void AuditGameSession()
    {
        BoardPath currentBoard = board != null ? board : FindScene<BoardPath>();
        if (currentBoard == null)
        {
            board = null;
            turn = null;
            wasInMatch = false;
            return;
        }
        if (board != currentBoard)
        {
            board = currentBoard;
            turn = null;
            wasInMatch = false;
            EnsureModernComponents();
        }
        if (turn == null) turn = FindScene<TurnManager>();
        bool active = turn != null && turn.IsMatchStarted;
        if (active != wasInMatch)
            steamPlayerBindingsReady = false;
        if (wasInMatch && !active && !resetting)
        {
            StartCoroutine(RearmAfterExit());
        }
        wasInMatch = active;
        if (!resetting && !active) EnsureModernComponents();
    }

    private IEnumerator RearmAfterExit()
    {
        resetting = true;
        // Let the canonical reset run first; never destroy the presentation
        // while purchase/auction/trade is still active in gameplay.
        yield return null;
        yield return null;
        if (board == null || (turn != null && turn.IsMatchStarted))
        {
            resetting = false;
            yield break;
        }
        // Both reentry into the SAME scene and a full scene reload are handled.
        // Re-creating presentation owners refreshes their cached manager/panel
        // references; existing full-size Phase14 cards are reused by name.
        RecreateIfPresent<AtlasBoardPurchasePresentationPolish>();
        RecreateIfPresent<AtlasBoardAuctionPresentationPolish>();
        RecreateIfPresent<AtlasBoardTradePresentationPolish>();
        RecreateIfPresent<AtlasBoardDevelopmentPresentationPolish>();
        RecreateIfPresent<AtlasBoardMatchResultFinalLayout>();
        RecreateIfPresent<AtlasBoardCityInformationPanel>();
        RecreateIfPresent<AtlasBoardCityInfoCameraInputGuard>();
        RecreateIfPresent<AtlasBoardTabletWorldInteractionGuard>();
        yield return null;
        EnsureModernComponents();
        resetting = false;
        Debug.Log("ATB_SHARED_REENTRY presentation_owners_rearmed=true next_match_ready=true");
    }

    private static T FindScene<T>() where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (T item in all)
            if (item != null && item.gameObject.scene.IsValid())
                return item;
        return null;
    }

    private static void RecreateIfPresent<T>() where T : MonoBehaviour
    {
        T existing = FindScene<T>();
        if (existing == null) return;
        // These original runtime owners are standalone scene roots. Destroy
        // their own generated child Canvas (not just the Component), otherwise
        // a second City Information Canvas remains over the next match.
        Component[] parts = existing.GetComponents<Component>();
        if (existing.transform.parent == null && parts.Length <= 2 &&
            existing.name.StartsWith("AtlasBoard_", StringComparison.Ordinal))
            UnityEngine.Object.Destroy(existing.gameObject);
        else
            UnityEngine.Object.Destroy(existing);
        // Delayed AddComponent occurs in next frame in EnsureModernComponents;
        // do not create a second owner before Unity destroys the first one.
    }

    private static void EnsureModernComponents()
    {
        if (FindScene<BoardPath>() == null) return;
        Ensure<AtlasBoardPurchasePresentationPolish>("AtlasBoard_Phase14K_PurchasePresentation");
        Ensure<AtlasBoardAuctionPresentationPolish>("AtlasBoard_Phase14I_AuctionPresentation");
        Ensure<AtlasBoardTradePresentationPolish>("AtlasBoard_Phase14H_TradePresentation");
        Ensure<AtlasBoardDevelopmentPresentationPolish>("AtlasBoard_Phase14_DevelopmentPresentation");
        Ensure<AtlasBoardMatchResultFinalLayout>("AtlasBoard_Phase14_MatchResultPresentation");
        Ensure<AtlasBoardCityInformationPanel>("AtlasBoard_Phase14A_CityInformationPanel");
        Ensure<AtlasBoardCityInfoCameraInputGuard>("AtlasBoard_CityInfo_Camera_Input_Guard");
        Ensure<AtlasBoardTabletWorldInteractionGuard>("AtlasBoard_Tablet_World_Input_Guard");
    }

    private static void Ensure<T>(string name) where T : MonoBehaviour
    {
        if (FindScene<T>() != null) return;
        new GameObject(name).AddComponent<T>();
    }

    private void EnsureShopEntry()
    {
        // A single successfully installed input overlay survives menu->match->
        // menu. Don't run FindObjectsByType on every 800ms maintenance tick.
        if (shopButton != null &&
            shopButton.transform.Find("AtlasBoardStoreV2Input") != null)
            return;
        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Button current = null;
        foreach (Button button in buttons)
        {
            if (button != null && button.gameObject.scene.IsValid() &&
                button.name == "ShopCard")
            {
                current = button;
                break;
            }
        }
        if (current == null) { shopButton = null; return; }
        if (current == shopButton && current.transform.Find("AtlasBoardStoreV2Input") != null)
            return;
        shopButton = current;
        RectTransform card = current.transform as RectTransform;
        if (card == null) return;
        Transform existing = card.Find("AtlasBoardStoreV2Input");
        if (existing != null) return;
        GameObject overlay = new GameObject("AtlasBoardStoreV2Input",
            typeof(RectTransform), typeof(Image), typeof(Button));
        overlay.transform.SetParent(card, false);
        RectTransform rect = overlay.transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = overlay.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = true;
        Button intercept = overlay.GetComponent<Button>();
        intercept.targetGraphic = image;
        intercept.onClick.AddListener(OpenCurrentStore);
        // The transparent, topmost input target prevents the original
        // inspector-wired legacy Shop foundation modal from receiving clicks.
        overlay.transform.SetAsLastSibling();
        if (!loggedShopBinding)
        {
            loggedShopBinding = true;
            Debug.Log("ATB_STORE_REENTRY legacy_button_bypassed=true target=StoreV2");
        }
    }

    private void OpenCurrentStore()
    {
        AtlasBoardMetaEconomyUI store = FindScene<AtlasBoardMetaEconomyUI>();
        if (store == null || OpenStoreMethod == null)
        {
            Debug.LogWarning("ATB_STORE_REENTRY v2_missing=true legacy_fallback_not_invoked");
            return;
        }
        try
        {
            OpenStoreMethod.Invoke(store, null);
            StartCoroutine(CloseLegacyModalNextFrame());
        }
        catch (Exception e)
        {
            Debug.LogWarning("ATB_STORE_REENTRY open_failed=" + e.GetType().Name);
        }
    }

    private static IEnumerator CloseLegacyModalNextFrame()
    {
        yield return null;
        AtlasBoardMainMenuController menu = FindScene<AtlasBoardMainMenuController>();
        if (menu != null) menu.CloseModal();
    }

    private void SynchronizeLocallyOwnedSteamName()
    {
        string persona = AtlasBoardCanonicalPlayerIdentity.ValidSteamPersona();
        if (string.IsNullOrWhiteSpace(persona)) return;
        // Read Steam name from the one shared identity resolver. Scene object
        // discovery occurs only after a scene or match transition, not while
        // the tablet is open/closed or while merely looking at a Profile card.
        if (!steamPlayerBindingsReady || trackedPlayers == null ||
            trackedPlayers.Length == 0 || Array.Exists(trackedPlayers, p => p == null))
        {
            trackedCoordinator = FindScene<AtlasBoardTurnDiceNetworkCoordinator>();
            trackedMatchBridge = FindScene<AtlasBoardMatchRuntimeBridge>();
            trackedPlayers = UnityEngine.Object.FindObjectsByType<PlayerGameState>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            steamPlayerBindingsReady = true;
        }
        ApplyNameToTrackedPlayers(persona);
    }

    private void ApplyNameToTrackedPlayers(string persona)
    {
        if (DisplayNameField == null || trackedPlayers == null) return;
        foreach (PlayerGameState player in trackedPlayers)
        {
            if (player == null || !player.gameObject.scene.IsValid() ||
                !player.IsParticipating || player.OnlineSeatStateActive ||
                string.Equals(player.DisplayName, persona, StringComparison.Ordinal))
                continue;
            int slot = player.PlayerSlotIndex;
            BotPlayerController bot = player.GetComponent<BotPlayerController>();
            bool localSeatAuthority = trackedCoordinator != null &&
                trackedCoordinator.IsLocallyControlledHumanSlot(slot);
            bool actuallyOffline = trackedMatchBridge == null ||
                string.IsNullOrWhiteSpace(trackedMatchBridge.CurrentMatchId);
            bool offlineHost = actuallyOffline && slot == 0 &&
                (bot == null || !bot.BotEnabled);
            if (!localSeatAuthority && !offlineHost) continue;
            DisplayNameField.SetValue(player, persona);
            Action<PlayerGameState> changed = IdentityChangedField != null
                ? IdentityChangedField.GetValue(player) as Action<PlayerGameState>
                : null;
            if (changed != null) changed(player);
            Debug.Log("ATB_STEAM_LOCAL_DISPLAY synced=true slot=" + slot);
        }
    }
}
