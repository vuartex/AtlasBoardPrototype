using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Phase 18E: three off-path Civic Project destinations per map.
/// Never add a BoardTile component to a Civic target: the dice path is 32 tiles.
/// The host / offline simulation alone can change ownership and money.
/// </summary>
[DisallowMultipleComponent]
public sealed class AtlasBoardCivicProjectManager : MonoBehaviour
{
    public const int ProjectCount = 3;
    private readonly int[] ownerSlots = { -1, -1, -1 };
    private readonly int[] pawnCivicLocations = { -1, -1, -1, -1 };
    private readonly HashSet<string> processedCommands = new HashSet<string>(StringComparer.Ordinal);
    private static readonly int[] Prices = { 650, 800, 950 };
    // Small, deterministic owner rewards; never randomized, never persisted as a
    // second economy. Ownership already belongs to the host Civic snapshot.
    private static readonly int[] StartBonuses = { 45, 55, 100 };
    private const int CombinedStartBonusCap = 150;
    private BoardGenerator generator;
    private TurnManager turnManager;
    private TileResolutionManager resolution;
    private AtlasBoardTurnDiceNetworkCoordinator network;
    private Transform targets;
    private readonly Transform[] targetTransforms = new Transform[3];
    private readonly Renderer[] targetRenderers = new Renderer[3];
    private readonly TextMeshPro[] nameLabels = new TextMeshPro[3];
    private readonly TextMeshPro[] ownerLabels = new TextMeshPro[3];
    private Canvas canvas;
    private GameObject overlay;
    private TMP_Text heading;
    private TMP_Text message;
    private readonly Button[] optionButtons = new Button[3];
    private readonly TMP_Text[] optionLabels = new TMP_Text[3];
    private Button declineButton;
    private TMP_Text declineLabel;

    // Read-only Civic inspection: marker hit areas are screen-space UI, not
    // BoardTile colliders. Nothing here can issue a purchase/economy command.
    private readonly Button[] markerHitButtons = new Button[ProjectCount];
    private readonly RectTransform[] markerHitRects = new RectTransform[ProjectCount];
    private GameObject infoPanel;
    private TMP_Text infoKicker;
    private TMP_Text infoHeading;
    private TMP_Text infoOwner;
    private TMP_Text infoBody;
    private TMP_Text infoFooter;
    private int selectedInfoProject = -1;
    private string shownInfoSignature = string.Empty;
    private float nextOverlayReferenceCheck;
    private BoardCameraController markerCameraController;
    private AtlasBoardDevelopmentsBankUI managementPanel;
    private AtlasBoardChatUIController chatPanel;
    private AtlasBoardCityInformationPanel cityInfoPanel;
    private TabletUIManager tabletPanel;
    private AtlasBoardLeaveFlowController leavePanel;
    private int pendingSlot = -1;
    private int pendingProject = -1;
    private Action completion;
    private int lastShownState = -1;
    private int lastShownSlot = -1;
    private int lastShownProject = -1;
    private bool hideTravelOffer;
    private int lastShownMoney = int.MinValue;
    private string lastLanguage = string.Empty;
    private bool inFlight;
    private string activeMapId = string.Empty;

    public int PendingSlot => pendingSlot;
    public int PendingProject => pendingProject;
    public bool HasPendingArrival => pendingSlot >= 0 && pendingProject >= 0;
    public int GetOwnerSlot(int id) => id >= 0 && id < 3 ? ownerSlots[id] : -1;
    public int GetPrice(int id) => id >= 0 && id < 3 ? Prices[id] : 0;
    public int GetOwnedValue(int slot)
    {
        int result = 0;
        for (int i = 0; i < 3; i++) if (ownerSlots[i] == slot) result += Prices[i];
        return result;
    }
    public int GetOwnedCount(int slot)
    {
        int result = 0;
        for (int i = 0; i < 3; i++) if (ownerSlots[i] == slot) result++;
        return result;
    }
    public bool HasTechnologyProsperityBonus(int slot) => OwnsProject(slot, 2);

    public bool OwnsProject(int slot, int project) =>
        slot >= 0 && slot <= 3 && project >= 0 && project < ProjectCount && ownerSlots[project] == slot;

    public int GetStartPassBonus(PlayerGameState player)
    {
        if (player == null || player.IsBankrupt || !player.IsParticipating) return 0;
        return CalculateOwnedStartBonus(player.PlayerSlotIndex, ownerSlots);
    }

    private static int CalculateOwnedStartBonus(int slot, int[] owners)
    {
        if (slot < 0 || slot > 3 || owners == null || owners.Length < ProjectCount) return 0;
        int sum = 0;
        for (int i = 0; i < ProjectCount; i++)
            if (owners[i] == slot) sum += StartBonuses[i];
        return Mathf.Min(CombinedStartBonusCap, sum);
    }

    // Metrics always derive from the SAME owner slots as snapshot, so an ownership
    // transfer, bankruptcy or new match immediately changes the eligible cities.
    // Demand: trade project +2; Traffic: transit project +3; Prosperity:
    // technology project +5% (the existing Alaşehir effect, now across maps).
    public int GetOwnedCityMetricBonus(int slot, int category, int rawScore)
    {
        if (slot < 0 || slot > 3) return 0;
        if (category == 0 && OwnsProject(slot, 0)) return 2;
        if (category == 2 && OwnsProject(slot, 1)) return 3;
        if (category == 1 && OwnsProject(slot, 2))
            return Mathf.CeilToInt(Mathf.Max(0, rawScore) * 0.05f);
        return 0;
    }

    public static int GetStartBonusForProject(int project) =>
        project >= 0 && project < ProjectCount ? StartBonuses[project] : 0;

    public static int CivicStartBonusCap => CombinedStartBonusCap;

    public string GetProjectName(int id) => GetProjectNameForLanguage(activeMapId, id,
        AtlasBoardLocalizationManager.Instance != null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en");

    // Language-complete, stable strings in this canonical Civic source.
    // KO/RU deliberately keep the EN proper project names, as requested;
    // ordinary interface/effect text in KO/RU remains localized.
    public static string GetProjectNameForLanguage(string mapId, int id, string language)
    {
        if (id < 0 || id >= ProjectCount) return string.Empty;
        string lang = (language ?? "en").ToLowerInvariant();
        switch (mapId)
        {
            case "map_turkey":
                switch (id)
                {
                    case 0: return LocalizedProjectName(lang, "İstanbul Airport", "İstanbul Havalimanı",
                        "Aeropuerto de Estambul", "Aéroport d'Istanbul", "Flughafen Istanbul");
                    case 1: return LocalizedProjectName(lang, "High-Speed Rail Network", "Yüksek Hızlı Tren Ağı",
                        "Red ferroviaria de alta velocidad", "Réseau ferroviaire à grande vitesse", "Hochgeschwindigkeitsbahnnetz");
                    default: return LocalizedProjectName(lang, "Alaşehir Technology Center", "Alaşehir Teknoloji Merkezi",
                        "Centro Tecnológico de Alaşehir", "Centre technologique d'Alaşehir", "Technologiezentrum Alaşehir");
                }
            case "map_colorado":
                switch (id)
                {
                    case 0: return LocalizedProjectName(lang, "Front Range Commerce Network", "Front Range Ticaret Ağı",
                        "Red comercial de Front Range", "Réseau commercial de Front Range", "Handelsnetz Front Range");
                    case 1: return LocalizedProjectName(lang, "Colorado Transit Network", "Colorado Ulaşım Ağı",
                        "Red de transporte de Colorado", "Réseau de transport du Colorado", "Verkehrsnetz Colorado");
                    default: return LocalizedProjectName(lang, "Colorado Innovation Center", "Colorado İnovasyon Merkezi",
                        "Centro de Innovación de Colorado", "Centre d'innovation du Colorado", "Innovationszentrum Colorado");
                }
            case "map_usa":
                switch (id)
                {
                    case 0: return LocalizedProjectName(lang, "Interstate Trade Corridor", "Eyaletler Arası Ticaret Koridoru",
                        "Corredor comercial interestatal", "Corridor commercial interétatique", "Zwischenstaatlicher Handelskorridor");
                    case 1: return LocalizedProjectName(lang, "National Rail Network", "Ulusal Demiryolu Ağı",
                        "Red ferroviaria nacional", "Réseau ferroviaire national", "Nationales Schienennetz");
                    default: return LocalizedProjectName(lang, "American Innovation Center", "Amerikan İnovasyon Merkezi",
                        "Centro de Innovación Estadounidense", "Centre d'innovation américain", "Amerikanisches Innovationszentrum");
                }
            default:
                return LocalizedProjectName(lang, "Civic Project " + (char)('A' + id),
                    "Kamu Projesi " + (char)('A' + id), "Proyecto Cívico " + (char)('A' + id),
                    "Projet civique " + (char)('A' + id), "Bürgerprojekt " + (char)('A' + id));
        }
    }

    private static string LocalizedProjectName(string language, string en, string tr, string es, string fr, string de)
    {
        switch (language)
        {
            case "tr": return tr;
            case "es": return es;
            case "fr": return fr;
            case "de": return de;
            // en, ko and ru deliberately share the English project name.
            default: return en;
        }
    }

    private void Awake()
    {
        generator = GetComponent<BoardGenerator>();
        BuildUI();
    }

    public void ConfigureMap(BoardMapDefinition map)
    {
        if (map == null) return;
        string mapId = map.MapId ?? string.Empty;
        if (activeMapId != mapId)
        {
            CloseInfo();
            activeMapId = mapId;
            ResetForNewMatch();
        }
        BuildVisualTargets();
    }

    public void ResetForNewMatch()
    {
        for (int i = 0; i < 3; i++) ownerSlots[i] = -1;
        for (int i = 0; i < 4; i++) pawnCivicLocations[i] = -1;
        processedCommands.Clear();
        hideTravelOffer = false;
        CloseInfo();
        CancelPending();
        RefreshVisuals();
    }

    public bool CanArrive(PlayerGameState player, int project)
    {
        return player != null && player.IsParticipating && !player.IsBankrupt &&
               project >= 0 && project < 3 && !HasPendingArrival &&
               turnManager != null && turnManager.IsMatchStarted;
    }

    public bool BeginArrival(PlayerGameState player, int project, Action onCompleted)
    {
        if (!CanArrive(player, project)) return false;
        pendingSlot = player.PlayerSlotIndex;
        pendingProject = project;
        completion = onCompleted;
        pawnCivicLocations[pendingSlot] = project;
        // Visual-only arrival. Stable dice tile index remains unchanged.
        SetPawnAtCivic(pendingSlot, project);
        if (ownerSlots[project] >= 0 || player.CurrentMoney < Prices[project])
        {
            FinishArrival();
            return true;
        }
        if (IsBot(player))
        {
            if (player.CurrentMoney >= Prices[project] + 180)
                TryPurchase(player, project, "bot-civic-" + Guid.NewGuid().ToString("N"));
            else FinishArrival();
        }
        return true;
    }

    public bool TryPurchase(PlayerGameState player, int project, string commandId)
    {
        if (player == null || project != pendingProject || player.PlayerSlotIndex != pendingSlot ||
            ownerSlots[project] != -1 || string.IsNullOrEmpty(commandId) ||
            processedCommands.Contains(commandId) || player.IsBankrupt ||
            !player.TrySpend(Prices[project])) return false;
        ownerSlots[project] = player.PlayerSlotIndex;
        processedCommands.Add(commandId);
        RefreshVisuals();
        Debug.Log("ATB_CIVIC_BUY project=" + project + " slot=" + player.PlayerSlotIndex + " cost=" + Prices[project], this);
        FinishArrival();
        return true;
    }

    public bool TrySkip(PlayerGameState player, int project)
    {
        if (player == null || pendingSlot != player.PlayerSlotIndex || pendingProject != project)
            return false;
        FinishArrival();
        return true;
    }

    public void HandleBankruptcy(PlayerGameState debtor, PlayerGameState creditor)
    {
        if (debtor == null) return;
        int destination = creditor != null && !creditor.IsBankrupt ? creditor.PlayerSlotIndex : -1;
        for (int id = 0; id < 3; id++)
            if (ownerSlots[id] == debtor.PlayerSlotIndex) ownerSlots[id] = destination;
        RefreshVisuals();
    }

    public int[] CaptureSnapshot()
    {
        return new[] { ownerSlots[0], ownerSlots[1], ownerSlots[2],
            pawnCivicLocations[0], pawnCivicLocations[1], pawnCivicLocations[2], pawnCivicLocations[3],
            pendingSlot, pendingProject };
    }

    public void ApplyAuthoritativeSnapshot(int[] values)
    {
        if (values == null || values.Length < 9) return;
        for (int i = 0; i < 3; i++) ownerSlots[i] = Mathf.Clamp(values[i], -1, 3);
        for (int i = 0; i < 4; i++) pawnCivicLocations[i] = Mathf.Clamp(values[3 + i], -1, 2);
        pendingSlot = Mathf.Clamp(values[7], -1, 3);
        pendingProject = Mathf.Clamp(values[8], -1, 2);
        if (pendingSlot < 0 || pendingProject < 0) { pendingSlot = -1; pendingProject = -1; }
        RefreshVisuals();
        RefreshInfoDisplay();
    }

    public void OnPawnStartedMoving(PlayerPawnMover pawn)
    {
        if (pawn == null) return;
        PlayerGameState player = pawn.GetComponent<PlayerGameState>();
        if (player != null && player.PlayerSlotIndex >= 0 && player.PlayerSlotIndex < 4)
            pawnCivicLocations[player.PlayerSlotIndex] = -1;
    }

    public bool CanTravelBot(PlayerGameState player, int fee, out int project)
    {
        project = -1;
        if (player == null || !IsBot(player)) return false;
        for (int i = 0; i < 3; i++)
        {
            if (ownerSlots[i] == -1 && player.CurrentMoney >= fee + Prices[i] + 180)
            { project = i; return true; }
        }
        return false;
    }

    private void FinishArrival()
    {
        Action callback = completion;
        completion = null;
        pendingSlot = -1;
        pendingProject = -1;
        inFlight = false;
        lastShownState = -1;
        if (overlay != null) overlay.SetActive(false);
        callback?.Invoke();
    }
    private void CancelPending()
    {
        completion = null;
        pendingSlot = -1;
        pendingProject = -1;
        inFlight = false;
        if (overlay != null) overlay.SetActive(false);
    }
    private void Update()
    {
        if (turnManager == null) turnManager = FindAnyObjectByType<TurnManager>();
        if (resolution == null) resolution = FindAnyObjectByType<TileResolutionManager>();
        if (network == null) network = FindAnyObjectByType<AtlasBoardTurnDiceNetworkCoordinator>();
        if (generator == null) generator = GetComponent<BoardGenerator>();
        if (generator != null && generator.ActiveMapId != activeMapId)
            ConfigureMap(generator.ActiveMapDefinition);
        if (canvas == null || turnManager == null || !turnManager.IsMatchStarted || turnManager.IsMatchFinished)
        {
            if (overlay != null) overlay.SetActive(false);
            CloseInfo();
            SetMarkerHitTargetsActive(false);
            return;
        }

        PlayerGameState local = ResolveLocalPlayer();
        bool purchase = local != null && HasPendingArrival && pendingSlot == local.PlayerSlotIndex;
        bool hasTravel = !purchase && local != null && resolution != null && resolution.HasPendingTravelFor(local);
        if (!hasTravel) hideTravelOffer = false;
        bool travel = hasTravel && !hideTravelOffer;
        int mode = purchase ? 2 : travel ? 1 : 0;
        int slot = local != null ? local.PlayerSlotIndex : -1;
        int money = local != null ? local.CurrentMoney : 0;
        string language = AtlasBoardLocalizationManager.Instance != null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";
        // The board-world marker captions have no regular UGUI localization
        // binding; re-apply their text when the selected language changes.
        if (!string.Equals(lastLanguage, language, StringComparison.Ordinal))
        {
            RefreshLocalizedFonts();
            RefreshVisuals();
        }
        // Screen projection must follow camera rotation/zoom EVERY frame, even
        // when all gameplay data has the same update signature.
        UpdateMarkerHitTargets(mode == 0);
        if (mode != 0) CloseInfo();
        else if (infoPanel != null && infoPanel.activeSelf)
        {
            if (IsAnotherOverlayOpen()) CloseInfo();
            else RefreshInfoDisplay();
        }
        if (mode == lastShownState && slot == lastShownSlot && pendingProject == lastShownProject &&
            money == lastShownMoney && language == lastLanguage) return;
        lastShownState = mode;
        lastShownSlot = slot;
        lastShownProject = pendingProject;
        lastShownMoney = money;
        lastLanguage = language;
        overlay.SetActive(mode != 0);
        if (mode != 0) SetMarkerHitTargetsActive(false);
        if (mode == 0) return;
        heading.text = mode == 2
            ? L("CIVIC PROJECT", "KAMU PROJESİ", "PROYECTO CÍVICO", "PROJET CIVIQUE", "BÜRGERPROJEKT", "시민 프로젝트", "ГОРОДСКОЙ ПРОЕКТ")
            : L("CIVIC DESTINATIONS", "KAMU PROJESİ HEDEFLERİ", "DESTINOS CÍVICOS", "DESTINATIONS CIVIQUES", "PROJEKTZIELE", "시민 프로젝트 목적지", "ЦЕЛИ ПРОЕКТОВ");
        message.text = mode == 2 ? GetProjectName(pendingProject) + "  |  " + GetPrice(pendingProject) + " ₵" :
            L("Optional Travel destination. Normal Travel remains available.",
              "İsteğe bağlı seyahat. Normal Seyahat hâlâ kullanılabilir.",
              "Viaje opcional. El viaje normal sigue disponible.",
              "Voyage optionnel. Le voyage normal reste disponible.",
              "Optionale Reise. Die normale Reise bleibt verfügbar.",
              "선택 이동입니다. 일반 이동도 이용할 수 있습니다.",
              "Дополнительный маршрут. Обычное путешествие доступно.");
        for (int i = 0; i < 3; i++)
        {
            bool shown = mode == 1 || i == pendingProject;
            optionButtons[i].gameObject.SetActive(shown);
            if (shown && mode == 2)
                (optionButtons[i].transform as RectTransform).anchoredPosition = new Vector2(0f, -145f);
            else
                (optionButtons[i].transform as RectTransform).anchoredPosition = new Vector2(0f, -144f - i * 44f);
            optionLabels[i].text = mode == 2
                ? L("PURCHASE", "SATIN AL", "COMPRAR", "ACHETER", "KAUFEN", "구매", "КУПИТЬ") + "  |  " + GetPrice(i) + " ₵"
                : GetProjectName(i) + "  |  " + Prices[i] + " ₵";
            optionButtons[i].interactable = !inFlight &&
                (mode == 1 || (ownerSlots[i] < 0 && local.CurrentMoney >= Prices[i]));
        }
        declineLabel.text = mode == 2
            ? L("SKIP PURCHASE", "SATIN ALMA", "OMITIR COMPRA", "NE PAS ACHETER", "NICHT KAUFEN", "구매 건너뛰기", "НЕ ПОКУПАТЬ")
            : L("USE NORMAL TRAVEL", "NORMAL SEYAHATE DEVAM", "VIAJE NORMAL", "VOYAGE NORMAL", "NORMALE REISE", "일반 이동", "ОБЫЧНЫЙ МАРШРУТ");
        declineButton.gameObject.SetActive(true);
    }

    // The Hub has no physical Collider by design: its three invisible UGUI
    // targets are projected from the three real world discs every frame.
    // This avoids interfering with the game's 32 BoardTile world raycasts.
    private void SetMarkerHitTargetsActive(bool enabled)
    {
        for (int i = 0; i < ProjectCount; i++)
            if (markerHitButtons[i] != null && markerHitButtons[i].gameObject.activeSelf != enabled)
                markerHitButtons[i].gameObject.SetActive(enabled);
    }

    private void RefreshLocalizedFonts()
    {
        AtlasBoardLocalizationManager locale = AtlasBoardLocalizationManager.Instance;
        if (locale == null) return;
        // Re-resolve fallback glyph sets (e.g. TR accents, KO/RU interface)
        // without duplicating the game's localization service.
        if (canvas != null)
        {
            foreach (TextMeshProUGUI label in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (label != null) label.font = locale.ResolveFont(label.font);
        }
        if (targets != null)
        {
            foreach (TextMeshPro label in targets.GetComponentsInChildren<TextMeshPro>(true))
                if (label != null) label.font = locale.ResolveFont(label.font);
        }
    }

    private Camera ResolveMarkerCamera()
    {
        if (markerCameraController == null)
            markerCameraController = FindAnyObjectByType<BoardCameraController>();
        Camera camera = markerCameraController != null
            ? markerCameraController.GetComponent<Camera>() : null;
        return camera != null ? camera : Camera.main;
    }

    private void RefreshOverlayReferences()
    {
        if (Time.unscaledTime < nextOverlayReferenceCheck) return;
        nextOverlayReferenceCheck = Time.unscaledTime + 1f;
        if (managementPanel == null) managementPanel = FindAnyObjectByType<AtlasBoardDevelopmentsBankUI>();
        if (chatPanel == null) chatPanel = FindAnyObjectByType<AtlasBoardChatUIController>();
        if (cityInfoPanel == null) cityInfoPanel = FindAnyObjectByType<AtlasBoardCityInformationPanel>();
        if (tabletPanel == null) tabletPanel = FindAnyObjectByType<TabletUIManager>();
        if (leavePanel == null) leavePanel = FindAnyObjectByType<AtlasBoardLeaveFlowController>();
    }

    private bool IsAnotherOverlayOpen()
    {
        RefreshOverlayReferences();
        return (managementPanel != null && managementPanel.IsPanelOpen) ||
               (chatPanel != null && chatPanel.IsPanelOpen) ||
               (cityInfoPanel != null && cityInfoPanel.IsOpen) ||
               (tabletPanel != null && tabletPanel.IsGameplayOverlayVisible) ||
               (leavePanel != null && leavePanel.IsModalOverlayVisible);
    }

    private void UpdateMarkerHitTargets(bool available)
    {
        bool canInspect = available && targets != null && !IsAnotherOverlayOpen();
        if (!canInspect)
        {
            SetMarkerHitTargetsActive(false);
            return;
        }
        Camera camera = ResolveMarkerCamera();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        if (camera == null || canvasRect == null)
        {
            SetMarkerHitTargetsActive(false);
            return;
        }
        Rect screenArea = Screen.safeArea;
        for (int i = 0; i < ProjectCount; i++)
        {
            Button button = markerHitButtons[i];
            RectTransform rect = markerHitRects[i];
            if (button == null || rect == null) continue;
            Transform marker = targetTransforms[i];
            Vector3 screen = marker != null
                ? camera.WorldToScreenPoint(marker.position + marker.up * 0.11f)
                : new Vector3(0f, 0f, -1f);
            bool inView = screen.z > 0f && screenArea.Contains(new Vector2(screen.x, screen.y));
            Vector2 local;
            if (inView && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, new Vector2(screen.x, screen.y), null, out local))
            {
                rect.anchoredPosition = local;
                if (!button.gameObject.activeSelf) button.gameObject.SetActive(true);
            }
            else if (button.gameObject.activeSelf)
                button.gameObject.SetActive(false);
        }
    }

    // Used by the existing camera guard. No Physics collider or extra manager.
    public bool ShouldBlockWorldCameraInput()
    {
        if (infoPanel != null && infoPanel.activeSelf) return true;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            Vector2 point = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            foreach (RectTransform rect in markerHitRects)
                if (rect != null && rect.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(rect, point)) return true;
        }
        if (UnityEngine.InputSystem.Touchscreen.current != null &&
            UnityEngine.InputSystem.Touchscreen.current.primaryTouch.press.isPressed)
        {
            Vector2 point = UnityEngine.InputSystem.Touchscreen.current.primaryTouch.position.ReadValue();
            foreach (RectTransform rect in markerHitRects)
                if (rect != null && rect.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(rect, point)) return true;
        }
#endif
        return false;
    }

    private void OpenInfo(int project)
    {
        if (project < 0 || project >= ProjectCount || infoPanel == null ||
            turnManager == null || !turnManager.IsMatchStarted || turnManager.IsMatchFinished ||
            (overlay != null && overlay.activeSelf) || IsAnotherOverlayOpen()) return;
        selectedInfoProject = project;
        shownInfoSignature = string.Empty;
        RefreshInfoDisplay();
        infoPanel.SetActive(true);
        Debug.Log("ATB_CIVIC_INFO_OPEN project=" + project + " readonly=true", this);
    }

    private void CloseInfo()
    {
        selectedInfoProject = -1;
        shownInfoSignature = string.Empty;
        if (infoPanel != null && infoPanel.activeSelf) infoPanel.SetActive(false);
    }

    private void RefreshInfoDisplay()
    {
        if (selectedInfoProject < 0 || selectedInfoProject >= ProjectCount || infoHeading == null) return;
        int project = selectedInfoProject;
        int ownerSlot = ownerSlots[project];
        string language = AtlasBoardLocalizationManager.Instance != null
            ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";
        string signature = activeMapId + ":" + project + ":" + ownerSlot + ":" + language;
        if (signature == shownInfoSignature) return;
        shownInfoSignature = signature;
        infoHeading.text = GetProjectName(project);
        if (infoKicker != null)
            infoKicker.text = "ATLAS BOARD  /  " + L("CIVIC PROJECT", "KAMU PROJESİ", "PROYECTO CÍVICO",
                "PROJET CIVIQUE", "BÜRGERPROJEKT", "시민 프로젝트", "ОБЩЕСТВЕННЫЙ ПРОЕКТ");
        string ownerName = L("Unowned", "Sahipsiz", "Sin dueño", "Sans propriétaire", "Ohne Eigentümer", "주인 없음", "Нет владельца");
        if (ownerSlot >= 0)
        {
            PlayerGameState owner = turnManager != null ? turnManager.GetPlayerStateBySlotIndex(ownerSlot) : null;
            ownerName = owner != null ? AtlasBoardL.PlayerName(owner) : "P" + (ownerSlot + 1);
        }
        infoOwner.text = L("Price", "Fiyat", "Precio", "Prix", "Preis", "가격", "Цена") + ": " + Prices[project] + " ₵    |    " +
            L("Owner", "Sahip", "Dueño", "Propriétaire", "Besitzer", "소유자", "Владелец") + ": " + ownerName;

        string effect;
        switch (project)
        {
            case 0:
                effect = L("Owner: +45 ₵ per real forward Start pass; +2 Demand on owned cities.",
                    "Sahip: gerçek ileri Start geçişinde +45 ₵; sahip olduğu şehirlerde +2 Talep.",
                    "Propietario: +45 ₵ al cruzar Inicio hacia delante; +2 Demanda en sus ciudades.",
                    "Propriétaire : +45 ₵ par passage avant au Départ ; +2 de demande dans ses villes.",
                    "Besitzer: +45 ₵ je regulärem Start-Durchlauf; +2 Nachfrage in eigenen Städten.",
                    "소유자: 정상적인 정방향 시작 지점 통과 시 +45 ₵; 보유 도시 수요 +2.",
                    "Владельцу: +45 ₵ за проход Старта вперёд; +2 спроса в своих городах.");
                break;
            case 1:
                effect = L("Owner: +55 ₵ per real forward Start pass; +3 Traffic on owned cities.",
                    "Sahip: gerçek ileri Start geçişinde +55 ₵; sahip olduğu şehirlerde +3 Ulaşım.",
                    "Propietario: +55 ₵ al cruzar Inicio hacia delante; +3 Tráfico en sus ciudades.",
                    "Propriétaire : +55 ₵ par passage avant au Départ ; +3 de trafic dans ses villes.",
                    "Besitzer: +55 ₵ je regulärem Start-Durchlauf; +3 Verkehr in eigenen Städten.",
                    "소유자: 정상적인 정방향 시작 지점 통과 시 +55 ₵; 보유 도시 교통 +3.",
                    "Владельцу: +55 ₵ за проход Старта вперёд; +3 транспорта в своих городах.");
                break;
            default:
                effect = L("Owner: +100 ₵ per real forward Start pass; +5% Prosperity on owned cities.",
                    "Sahip: gerçek ileri Start geçişinde +100 ₵; sahip olduğu şehirlerde +%5 Refah.",
                    "Propietario: +100 ₵ al cruzar Inicio hacia delante; +5% Prosperidad en sus ciudades.",
                    "Propriétaire : +100 ₵ par passage avant au Départ ; +5 % de prospérité dans ses villes.",
                    "Besitzer: +100 ₵ je regulärem Start-Durchlauf; +5 % Wohlstand in eigenen Städten.",
                    "소유자: 정상적인 정방향 시작 지점 통과 시 +100 ₵; 보유 도시 번영 +5%.",
                    "Владельцу: +100 ₵ за проход Старта вперёд; +5% процветания в своих городах.");
                break;
        }
        effect += "\n" + L("Combined Civic Start bonus is capped at +150 ₵; Pulse metrics stay at or below 94.",
            "Toplam Civic Start bonusu +150 ₵ ile, Pulse değerleri 94 ile sınırlıdır.",
            "Bono cívico total: máximo +150 ₵; métricas Pulse: máximo 94.",
            "Bonus civique total limité à +150 ₵ ; mesures Pulse plafonnées à 94.",
            "Civic-Startbonus insgesamt maximal +150 ₵; Pulse-Werte maximal 94.",
            "전체 시민 프로젝트 시작 보너스 최대 +150 ₵, Pulse 지표 최대 94.",
            "Общий бонус Civic за Старт не более +150 ₵; показатели Pulse до 94.");
        infoBody.text = effect;
        infoFooter.text = L("Reach via Event / Travel to purchase. Inspecting never spends money.",
            "Satın almak için Etkinlik / Seyahat ile ulaş. İnceleme para harcamaz.",
            "Llega por Evento / Viaje para comprar. Consultar no cuesta dinero.",
            "Accès via Événement / Voyage pour acheter. Consultation gratuite.",
            "Kauf nur nach Event-/Reiseziel. Ansicht kostet nichts.",
            "이벤트/이동으로 도착해야 구매할 수 있습니다. 조회에는 비용이 없습니다.",
            "Покупка после события или путешествия. Просмотр бесплатен.");
    }

    private PlayerGameState ResolveLocalPlayer()
    {
        if (turnManager == null) return null;
        if (network != null && network.IsOnlineSessionActive)
        {
            foreach (PlayerGameState player in FindObjectsByType<PlayerGameState>())
                if (player != null && player.IsParticipating && !player.IsBankrupt &&
                    !player.IsOnlineBotControlled && network.IsLocallyControlledHumanSlot(player.PlayerSlotIndex))
                    return player;
            return null;
        }
        PlayerGameState current = turnManager.CurrentPlayerState;
        return current != null && !turnManager.IsPlayerBot(current) && !current.IsBankrupt ? current : null;
    }

    private async void Select(int id)
    {
        if (inFlight || !turnManager.IsMatchStarted) return;
        PlayerGameState player = ResolveLocalPlayer();
        if (player == null) return;
        bool purchase = HasPendingArrival && pendingSlot == player.PlayerSlotIndex;
        if (!purchase && (resolution == null || !resolution.HasPendingTravelFor(player))) return;
        inFlight = true;
        try
        {
            if (network != null && network.IsOnlineSessionActive)
                await network.RequestCivicActionAsync(purchase ? "buy" : "travel", id);
            else if (purchase) TryPurchase(player, id, "local-civic-" + Guid.NewGuid().ToString("N"));
            else resolution.TryChooseCivicTravel(player, id);
        }
        catch (Exception ex) { Debug.LogWarning("ATB_CIVIC_ACTION_FAIL " + ex.GetType().Name, this); }
        finally { inFlight = false; lastShownState = -1; }
    }

    private async void Decline()
    {
        if (inFlight) return;
        PlayerGameState player = ResolveLocalPlayer();
        if (player == null) return;
        if (HasPendingArrival && pendingSlot == player.PlayerSlotIndex)
        {
            inFlight = true;
            try
            {
                if (network != null && network.IsOnlineSessionActive)
                    await network.RequestCivicActionAsync("skip", pendingProject);
                else TrySkip(player, pendingProject);
            }
            finally { inFlight = false; lastShownState = -1; }
        }
        else
        {
            hideTravelOffer = true;
            overlay.SetActive(false);
            lastShownState = -1; // Existing normal Travel UI remains interactive.
        }
    }

    private static string L(string en, string tr, string es, string fr, string de, string ko, string ru) =>
        AtlasBoardL.R(en, tr, es, fr, de, ko, ru);

    private static bool IsBot(PlayerGameState p) => p != null && p.GetComponent<BotPlayerController>() != null &&
        p.GetComponent<BotPlayerController>().BotEnabled;

    // Three compact, decorative Civic markers share a single rear-interior hub.
    // The off-path destinations themselves retain stable project IDs and never
    // enter the 32-tile movement sequence. The HUD has priority over world art.
    // The hub layout is calculated once at map setup from the opening camera.
    private void BuildVisualTargets()
    {
        if (targets != null) Destroy(targets.gameObject);
        GameObject container = new GameObject("CivicHub_OffPath_3");
        container.transform.SetParent(transform, false);
        targets = container.transform;

        Vector3 awayFromCamera = new Vector3(-1f, 0f, -1f).normalized;
        Vector3 acrossScreen = new Vector3(1f, 0f, -1f).normalized;
        BoardCameraController boardCameraController = FindAnyObjectByType<BoardCameraController>();
        Camera boardCamera = boardCameraController != null
            ? boardCameraController.GetComponent<Camera>() : Camera.main;
        if (boardCamera != null)
        {
            Vector3 forward = transform.InverseTransformDirection(boardCamera.transform.forward);
            Vector3 right = transform.InverseTransformDirection(boardCamera.transform.right);
            forward.y = 0f;
            right.y = 0f;
            if (forward.sqrMagnitude > 0.01f && right.sqrMagnitude > 0.01f)
            {
                awayFromCamera = forward.normalized;
                acrossScreen = right.normalized;
            }
        }

        // The central part of the board is reserved for dice/Trade controls.
        // A rear-biased row of small discs projects ABOVE that gameplay area
        // in the default camera view; no marker occupies a walking BoardTile.
        Vector3 rowCenter = awayFromCamera * 4.4f;
        float[] offsets = { -3.6f, 0f, 3.6f };
        for (int i = 0; i < ProjectCount; i++)
        {
            GameObject anchor = new GameObject("CivicDestination_" + (i + 1));
            anchor.transform.SetParent(targets, false);
            anchor.transform.localPosition = rowCenter + acrossScreen * offsets[i]
                + new Vector3(0f, 0.42f, 0f);
            targetTransforms[i] = anchor.transform;

            // Instead of large cubes, shallow layered discs look like
            // board-game monuments and leave room for the board artwork.
            Renderer rim = CreateMarkerDisc(anchor.transform, "OuterRim",
                new Vector3(0f, 0f, 0f), new Vector3(1.05f, 0.035f, 1.05f),
                new Color(0.16f, 0.52f, 0.62f, 1f));
            CreateMarkerDisc(anchor.transform, "InnerSurface",
                new Vector3(0f, 0.056f, 0f), new Vector3(0.94f, 0.018f, 0.94f),
                new Color(0.045f, 0.085f, 0.13f, 1f));
            CreateMarkerDisc(anchor.transform, "InnerSeal",
                new Vector3(0f, 0.090f, 0f), new Vector3(0.20f, 0.016f, 0.20f),
                new Color(0.24f, 0.72f, 0.76f, 1f));
            targetRenderers[i] = rim;

            nameLabels[i] = CreateMarkerText(anchor.transform, "MarkerTitle",
                new Vector3(0f, 0.14f, 0.39f), 3.4f, 0.115f,
                new Color(0.91f, 0.96f, 0.98f, 1f));
            nameLabels[i].text = GetMarkerTitle(i);
            ownerLabels[i] = CreateMarkerText(anchor.transform, "MarkerOwner",
                new Vector3(0f, 0.14f, -0.43f), 2.5f, 0.102f,
                new Color(0.60f, 0.81f, 0.88f, 1f));
        }
        RefreshLocalizedFonts();
        RefreshVisuals();
        UpdateMarkerHitTargets(false);
        Debug.Log("ATB_CIVIC_VISUAL_LAYOUT style=compact_discs markers=3 " +
            "zone=rear_interior boardTilesUnchanged=32", this);
    }

    private string GetMarkerTitle(int project)
    {
        if (project < 0 || project >= ProjectCount) return string.Empty;
        string shortName;
        switch (project)
        {
            case 0:
                shortName = L("TRADE", "TİCARET", "COMERCIO", "COMMERCE", "HANDEL", "TRADE", "TRADE");
                if (activeMapId == "map_turkey")
                    shortName = L("AIRPORT", "HAVALİMANI", "AEROPUERTO", "AÉROPORT", "FLUGHAFEN", "AIRPORT", "AIRPORT");
                break;
            case 1:
                shortName = L("TRANSIT", "ULAŞIM", "TRANSPORTE", "TRANSPORT", "VERKEHR", "TRANSIT", "TRANSIT");
                if (activeMapId != "map_colorado")
                    shortName = L("RAIL", "DEMİRYOLU", "FERROCARRIL", "FERROVIAIRE", "BAHN", "RAIL", "RAIL");
                break;
            default:
                shortName = L("TECH", "TEKNOLOJİ", "TECNOLOGÍA", "TECHNOLOGIE", "TECHNIK", "TECH", "TECH");
                break;
        }
        return (project + 1).ToString("00") + "  " + shortName;
    }

    private Renderer CreateMarkerDisc(Transform parent, string name,
        Vector3 position, Vector3 scale, Color color)
    {
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = name;
        disc.transform.SetParent(parent, false);
        disc.transform.localPosition = position;
        disc.transform.localScale = scale;
        Collider hitbox = disc.GetComponent<Collider>();
        if (hitbox != null) Destroy(hitbox);
        Renderer renderer = disc.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (generator != null && generator.CivicVisualMaterial != null)
                renderer.sharedMaterial = generator.CivicVisualMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }
        return renderer;
    }

    private static TextMeshPro CreateMarkerText(Transform parent, string name,
        Vector3 position, float fontSize, float scale, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(TextMeshPro));
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = position;
        // Label surface faces upward, not into the underside of the board.
        textObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        textObject.transform.localScale = Vector3.one * scale;
        TextMeshPro text = textObject.GetComponent<TextMeshPro>();
        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 1.9f;
        text.fontSizeMax = fontSize;
        text.rectTransform.sizeDelta = new Vector2(16f, 4.0f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private void RefreshVisuals()
    {
        for (int i = 0; i < 3; i++)
        {
            if (nameLabels[i] != null)
                nameLabels[i].text = GetMarkerTitle(i);
            if (ownerLabels[i] != null)
                ownerLabels[i].text = ownerSlots[i] < 0
                    ? Prices[i].ToString() + " ₵"
                    : "P" + (ownerSlots[i] + 1);
            if (targetRenderers[i] != null)
            {
                Color color = ownerSlots[i] < 0 ? new Color(.16f,.58f,.66f) : new Color(.88f,.65f,.26f);
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                targetRenderers[i].SetPropertyBlock(block);
            }
        }
    }

    private void SetPawnAtCivic(int slot, int id)
    {
        if (id < 0 || id >= 3 || targetTransforms[id] == null || turnManager == null) return;
        PlayerGameState player = turnManager.GetPlayerStateBySlotIndex(slot);
        if (player == null) return;
        PlayerPawnMover pawn = player.GetComponent<PlayerPawnMover>();
        if (pawn != null && !pawn.IsMoving)
            pawn.transform.position = targetTransforms[id].position
                + targetTransforms[id].right * 0.50f + Vector3.up * 0.68f;
    }

    public void SyncPawnPresentationFromSnapshot()
    {
        for (int i = 0; i < 4; i++)
            if (pawnCivicLocations[i] >= 0) SetPawnAtCivic(i, pawnCivicLocations[i]);
    }

    private void BuildUI()
    {
        GameObject root = new GameObject("CivicPanelCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 635;
        CanvasScaler scale = root.GetComponent<CanvasScaler>();
        scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scale.referenceResolution = new Vector2(1920,1080);
        overlay = new GameObject("CivicPanel", typeof(RectTransform),typeof(Image));
        overlay.transform.SetParent(root.transform,false);
        RectTransform box = overlay.transform as RectTransform;
        box.anchorMin = new Vector2(1f,0.53f);
        box.anchorMax = box.anchorMin;
        box.pivot = new Vector2(1f,0.5f);
        box.sizeDelta = new Vector2(424f, 335f);
        box.anchoredPosition = new Vector2(-38f,0f);
        overlay.GetComponent<Image>().color = new Color(.035f,.065f,.100f,.985f);
        GameObject accentLine = new GameObject("CivicAccent", typeof(RectTransform), typeof(Image));
        accentLine.transform.SetParent(overlay.transform, false);
        RectTransform accentRect = accentLine.transform as RectTransform;
        accentRect.anchorMin = new Vector2(0f,1f);
        accentRect.anchorMax = new Vector2(1f,1f);
        accentRect.pivot = new Vector2(0.5f,1f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(0f,4f);
        accentLine.GetComponent<Image>().color = new Color(.14f,.66f,.73f,1f);
        heading = Text(overlay.transform,"Heading",new Vector2(0,-25),new Vector2(385,36),21f);
        message = Text(overlay.transform,"Message",new Vector2(0,-76),new Vector2(385,50),14f);
        for(int i=0;i<3;i++)
        {
            int captured=i;
            optionButtons[i]=Button(overlay.transform,"Destination_"+i,new Vector2(0,-144-i*44),new Vector2(378,38),()=>Select(captured),out TMP_Text label);
            optionLabels[i]=label;
        }
        declineButton=Button(overlay.transform,"Decline",new Vector2(0,-279),new Vector2(378,38),Decline,out declineLabel);
        overlay.SetActive(false);
        BuildMarkerHitTargets(root.transform);
        BuildInfoPanel(root.transform);
        RefreshLocalizedFonts();
    }

    private void BuildMarkerHitTargets(Transform parent)
    {
        for (int i = 0; i < ProjectCount; i++)
        {
            int project = i;
            GameObject target = new GameObject("CivicMarkerHit_" + i,
                typeof(RectTransform), typeof(Image), typeof(Button));
            target.transform.SetParent(parent, false);
            RectTransform rect = target.transform as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(104f, 90f);
            Image image = target.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.005f);
            image.raycastTarget = true;
            Button button = target.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => OpenInfo(project));
            markerHitButtons[i] = button;
            markerHitRects[i] = rect;
            target.SetActive(false);
        }
    }

    private void BuildInfoPanel(Transform parent)
    {
        infoPanel = new GameObject("CivicProjectInfo_ReadOnly", typeof(RectTransform), typeof(Image));
        infoPanel.transform.SetParent(parent, false);
        RectTransform box = infoPanel.transform as RectTransform;
        box.anchorMin = box.anchorMax = new Vector2(1f, 0.55f);
        box.pivot = new Vector2(1f, 0.5f);
        box.sizeDelta = new Vector2(470f, 325f);
        box.anchoredPosition = new Vector2(-38f, 0f);
        infoPanel.GetComponent<Image>().color = new Color(.035f, .065f, .100f, .985f);
        GameObject accent = new GameObject("InfoAccent", typeof(RectTransform), typeof(Image));
        accent.transform.SetParent(box, false);
        RectTransform line = accent.transform as RectTransform;
        line.anchorMin = new Vector2(0f, 1f);
        line.anchorMax = new Vector2(1f, 1f);
        line.pivot = new Vector2(0.5f, 1f);
        line.anchoredPosition = Vector2.zero;
        line.sizeDelta = new Vector2(0f, 4f);
        accent.GetComponent<Image>().color = new Color(.14f, .66f, .73f, 1f);

        infoKicker = Text(box, "InfoKicker", new Vector2(0f, -18f), new Vector2(394f, 22f), 12f);
        infoKicker.text = "ATLAS BOARD  /  CIVIC PROJECT";
        infoKicker.color = new Color(.44f, .73f, .80f, 1f);
        infoHeading = Text(box, "InfoTitle", new Vector2(0f, -46f), new Vector2(412f, 44f), 22f);
        infoOwner = Text(box, "InfoOwner", new Vector2(0f, -96f), new Vector2(412f, 33f), 14f);
        infoOwner.color = new Color(.68f, .82f, .87f, 1f);
        infoBody = Text(box, "InfoDescription", new Vector2(0f, -145f), new Vector2(408f, 92f), 15f);
        infoFooter = Text(box, "InfoFooter", new Vector2(0f, -253f), new Vector2(396f, 48f), 13f);
        infoFooter.color = new Color(.59f, .76f, .82f, 1f);
        Button close = Button(box, "CloseInfo", new Vector2(210f, -16f),
            new Vector2(32f, 30f), CloseInfo, out TMP_Text closeText);
        close.GetComponent<Image>().color = new Color(.13f, .20f, .26f, 1f);
        closeText.text = "X";
        infoPanel.SetActive(false);
    }

    private static TMP_Text Text(Transform parent,string name,Vector2 pos,Vector2 size,float font)
    {
        GameObject go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));
        go.transform.SetParent(parent,false);
        RectTransform rect=go.transform as RectTransform;
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,1f);
        rect.pivot=new Vector2(.5f,1f);
        rect.anchoredPosition=pos;
        rect.sizeDelta=size;
        TMP_Text label=go.GetComponent<TMP_Text>();
        label.color=new Color(.91f,.95f,.98f);
        label.alignment=TextAlignmentOptions.Center;
        label.fontSize=font;
        label.enableAutoSizing=true;
        label.fontSizeMin=11f;
        label.fontSizeMax=font;
        label.textWrappingMode=TextWrappingModes.Normal;
        return label;
    }

    private static Button Button(Transform parent,string name,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction callback,out TMP_Text text)
    {
        GameObject go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));
        go.transform.SetParent(parent,false);
        RectTransform rect=go.transform as RectTransform;
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,1f);
        rect.pivot=new Vector2(.5f,1f);
        rect.anchoredPosition=pos;
        rect.sizeDelta=size;
        go.GetComponent<Image>().color=new Color(.12f,.34f,.42f);
        Button button=go.GetComponent<Button>();
        button.onClick.AddListener(callback);
        text=Text(go.transform,"Label",new Vector2(0,-7),new Vector2(size.x-15,size.y-10),14f);
        return button;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 18E - Civic Marker Info Click Smoke")]
    private static void EditorCivicInfoClickSmoke()
    {
        AtlasBoardCivicProjectManager manager = FindAnyObjectByType<AtlasBoardCivicProjectManager>();
        if (!Application.isPlaying || manager == null || manager.turnManager == null ||
            !manager.turnManager.IsMatchStarted || manager.overlay == null ||
            manager.overlay.activeSelf || manager.IsAnotherOverlayOpen())
        {
            Debug.LogWarning("ATB_PHASE18E_INFO_QA NOT_RUN: active match required; close all other panels and travel/purchase dialogs.");
            return;
        }
        int[] before = manager.CaptureSnapshot();
        int beforeMoney = manager.turnManager.CurrentPlayerState != null
            ? manager.turnManager.CurrentPlayerState.CurrentMoney : int.MinValue;
        bool valid = manager.markerHitButtons[0] != null;
        if (valid) manager.markerHitButtons[0].onClick.Invoke();
        bool opened = manager.infoPanel != null && manager.infoPanel.activeSelf &&
                      manager.selectedInfoProject == 0 &&
                      manager.infoHeading != null &&
                      manager.infoHeading.text == manager.GetProjectName(0);
        valid &= opened;
        manager.CloseInfo();
        int[] after = manager.CaptureSnapshot();
        bool stateUnchanged = before.Length == after.Length;
        if (stateUnchanged)
            for (int i = 0; i < before.Length; i++) stateUnchanged &= before[i] == after[i];
        stateUnchanged &= beforeMoney == (manager.turnManager.CurrentPlayerState != null
            ? manager.turnManager.CurrentPlayerState.CurrentMoney : int.MinValue);
        valid &= stateUnchanged;
        Debug.Log((valid ? "ATB_PHASE18E_INFO_QA PASS" : "ATB_PHASE18E_INFO_QA FAIL") +
                  " click_wiring=" + opened + " readonly=true state_unchanged=" + stateUnchanged);
    }

    [UnityEditor.MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 18E - Civic Path + Seed Smoke")]
    public static void EditorCivicSmoke()
    {
        AtlasBoardCivicProjectManager manager = FindAnyObjectByType<AtlasBoardCivicProjectManager>();
        BoardPath path = FindAnyObjectByType<BoardPath>();
        if (!Application.isPlaying || manager == null || path == null)
        { Debug.LogWarning("ATB_PHASE18E_QA NOT_RUN enter Play Mode and start a match."); return; }
        bool valid = path.TileCount == 32 && manager.targets != null && manager.targets.childCount == 3 &&
            manager.targetTransforms[0] != null && manager.targetTransforms[1] != null && manager.targetTransforms[2] != null;
        for (int i = 0; i < ProjectCount; i++)
        {
            valid &= manager.GetPrice(i) == Prices[i] &&
                     manager.markerHitButtons[i] != null &&
                     manager.markerHitRects[i] != null &&
                     manager.targetTransforms[i].GetComponent<BoardTile>() == null &&
                     manager.targetTransforms[i].Find("OuterRim") != null &&
                     manager.targetTransforms[i].Find("InnerSurface") != null &&
                     manager.targetTransforms[i].GetComponentInChildren<Collider>() == null;
        }
        valid &= manager.infoPanel != null && manager.infoHeading != null &&
                 manager.infoBody != null && !manager.infoPanel.activeSelf;
        // All three maps and seven locales have non-empty, stable names.
        string[] maps = { "map_turkey", "map_colorado", "map_usa" };
        string[] langs = { "en", "tr", "es", "fr", "de", "ko", "ru" };
        int labelsChecked = 0;
        foreach (string map in maps)
            for (int i = 0; i < ProjectCount; i++)
            {
                string en = GetProjectNameForLanguage(map, i, "en");
                foreach (string lang in langs)
                {
                    string name = GetProjectNameForLanguage(map, i, lang);
                    valid &= !string.IsNullOrWhiteSpace(name) && name.IndexOf("TBD", StringComparison.Ordinal) < 0 &&
                             (lang != "ko" && lang != "ru" || name == en);
                    labelsChecked++;
                }
            }
        valid &= StartBonuses[0] == 45 && StartBonuses[1] == 55 &&
                 StartBonuses[2] == 100 && CombinedStartBonusCap == 150 &&
                 CalculateOwnedStartBonus(0, new[] { 0, -1, -1 }) == 45 &&
                 CalculateOwnedStartBonus(0, new[] { -1, 0, -1 }) == 55 &&
                 CalculateOwnedStartBonus(0, new[] { -1, -1, 0 }) == 100 &&
                 CalculateOwnedStartBonus(0, new[] { 0, 0, 0 }) == 150 &&
                 CalculateOwnedStartBonus(1, new[] { 0, 0, 0 }) == 0;
        Debug.Log("ATB_PHASE18E_LOCALIZATION_QA " + (valid ? "PASS" : "FAIL") +
                  " project_labels=" + labelsChecked + " maps=3 languages=7 ko_ru_project_names=en " +
                  "startBonuses=45,55,100 cap=150 metricCap=94");
        Debug.Log((valid ? "ATB_PHASE18E_QA PASS" : "ATB_PHASE18E_QA FAIL")+
            " boardTiles="+path.TileCount+" civicTargets="+(manager.targets==null?0:manager.targets.childCount)+
            " names="+manager.GetProjectName(0)+" / "+manager.GetProjectName(1)+" / "+manager.GetProjectName(2));
    }
#endif
}
