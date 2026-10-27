using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Presentation for the existing Phase 18A drawer and Phase 18B/18D actions.
/// BoardTile / PlayerGameState and online follower mirrors remain the read model.
/// Authoritative Pulse and bank mutations are delegated to the existing
/// PropertyDevelopmentManager and network coordinator; this UI owns no economy.
/// </summary>
[DisallowMultipleComponent]
public sealed class AtlasBoardDevelopmentsBankUI : MonoBehaviour
{
    private enum Tab { Developments, Bank }
    private const float RefreshEverySeconds = 0.5f;
    private static readonly Color Navy = new Color(0.035f, 0.060f, 0.092f, 0.98f);
    private static readonly Color Card = new Color(0.073f, 0.11f, 0.15f, 1f);
    private static readonly Color CardAlt = new Color(0.09f, 0.15f, 0.20f, 1f);
    private static readonly Color CardSoft = new Color(0.10f, 0.19f, 0.26f, 1f);
    private static readonly Color Light = new Color(0.88f, 0.93f, 0.97f, 1f);
    private static readonly Color Muted = new Color(0.61f, 0.70f, 0.76f, 1f);
    private static readonly Color Accent = new Color(0.13f, 0.57f, 0.70f, 1f);
    private static readonly Color AccentSoft = new Color(0.16f, 0.37f, 0.46f, 1f);

    private Canvas canvas;
    private RectTransform safeAreaRoot;
    private GameObject launcher;
    private TMP_Text launcherText;
    private GameObject drawer;
    private TMP_Text title;
    private TMP_Text status;
    private TMP_Text developmentTabText;
    private TMP_Text bankTabText;
    private TMP_Text selectedCitySummaryHeader;
    private TMP_Text selectedCitySummaryDetails;
    private RectTransform scrollContent;
    private ScrollRect scroll;
    private Button developmentTab;
    private Button bankTab;
    private BoardPath board;
    private PropertyDevelopmentManager development;
    private TurnManager turns;
    private AtlasBoardTurnDiceNetworkCoordinator coordinator;
    private TabletUIManager tablet;
    private AtlasBoardLeaveFlowController leaveFlow;
    private bool matchWasRunning;
    private AtlasBoardChatUIController chat;
    private Tab tab;
    private string selectedCityId = string.Empty;
    private string selectedCollateralId = string.Empty;
    private string requestedLoanAmount = string.Empty;
    private string visibleSignature = string.Empty;
    private string previousLanguage = string.Empty;
    private float nextRefreshAt;
    private bool layoutInitialized;
    private bool dropdownExpanded;
    private bool pulseBuildPending;
    // 0=Short (2 turns, 8%), 1=Standard (3 turns, 12%), 2=Extended (5 turns, 20%).
    // UI-only selection; debt terms and authorization remain in the economy authority.
    private int bankTermKind = 1;
    // Guards repeated Apply/Repay while a host command or local operation is in flight.
    private bool bankPending;
    private readonly TMP_Text[] pulseValues = new TMP_Text[4];
    private readonly TMP_Text[] pulseDescriptions = new TMP_Text[4];
    private readonly Button[] pulseBuildButtons = new Button[4];
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    public bool IsPanelOpen => drawer != null && drawer.activeSelf;
    public bool IsVisible => launcher != null && launcher.activeSelf;
    public RectTransform DrawerRect => drawer != null ? drawer.transform as RectTransform : null;

    public bool ShouldBlockWorldCameraInput()
    {
        if (IsPanelOpen) return true;
        if (!IsVisible) return false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null &&
            RectTransformUtility.RectangleContainsScreenPoint(
                launcher.transform as RectTransform, Mouse.current.position.ReadValue()))
            return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed &&
            RectTransformUtility.RectangleContainsScreenPoint(
                launcher.transform as RectTransform, Touchscreen.current.primaryTouch.position.ReadValue()))
            return true;
#endif
        return false;
    }

    private void Awake()
    {
        BuildCanvas();
        lastScreenSize = Vector2Int.zero;
        previousLanguage = LanguageCode();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += SceneLoaded;
        AtlasBoardLocalizationManager.LanguageChanged += LanguageChanged;
        Invalidate();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        AtlasBoardLocalizationManager.LanguageChanged -= LanguageChanged;
        ClosePanel();
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        board = null;
        development = null;
        turns = null;
        coordinator = null;
        tablet = null;
        leaveFlow = null;
        matchWasRunning = false;
        chat = null;
        selectedCityId = string.Empty;
        selectedCollateralId = string.Empty;
        requestedLoanAmount = string.Empty;
        bankTermKind = 1;
        Invalidate();
        ClosePanel();
    }

    private void LanguageChanged()
    {
        previousLanguage = LanguageCode();
        AtlasBoardLocalizationManager local = AtlasBoardLocalizationManager.Instance;
        if (local != null && canvas != null)
            foreach (TextMeshProUGUI label in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
                label.font = local.ResolveFont(label.font);
        Invalidate();
    }

    private void Update()
    {
        if (!layoutInitialized) return;
        if (lastSafeArea != Screen.safeArea ||
            lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
            ApplySafeArea();

        if (Time.unscaledTime >= nextRefreshAt)
        {
            nextRefreshAt = Time.unscaledTime + RefreshEverySeconds;
            BindScene();
            bool inMatch = turns != null && turns.IsMatchStarted && !turns.IsMatchFinished;
            if (inMatch && !matchWasRunning)
            {
                selectedCityId = string.Empty;
                selectedCollateralId = string.Empty;
                requestedLoanAmount = string.Empty;
                bankTermKind = 1;
                Invalidate();
            }

            matchWasRunning = inMatch;
            if (launcher.activeSelf != inMatch) launcher.SetActive(inMatch);
            if (!inMatch && IsPanelOpen) ClosePanel();
            if (IsPanelOpen && (IsTabletOpen() || IsModalOpen() || (chat != null && chat.IsPanelOpen)))
                ClosePanel();
            if (IsPanelOpen) RefreshView(false);
        }

        if (!IsVisible || IsTabletOpen() || IsModalOpen()) return;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            Toggle();
        if (Gamepad.current != null && Gamepad.current.startButton.isPressed &&
            Gamepad.current.leftShoulder.wasPressedThisFrame)
            Toggle();
#endif
    }

    private void BindScene()
    {
        if (board == null) board = UnityEngine.Object.FindAnyObjectByType<BoardPath>();
        if (development == null) development = UnityEngine.Object.FindAnyObjectByType<PropertyDevelopmentManager>();
        if (turns == null) turns = UnityEngine.Object.FindAnyObjectByType<TurnManager>();
        if (coordinator == null) coordinator = UnityEngine.Object.FindAnyObjectByType<AtlasBoardTurnDiceNetworkCoordinator>();
        if (tablet == null) tablet = UnityEngine.Object.FindAnyObjectByType<TabletUIManager>();
        if (leaveFlow == null) leaveFlow = UnityEngine.Object.FindAnyObjectByType<AtlasBoardLeaveFlowController>();
        if (chat == null) chat = GetComponent<AtlasBoardChatUIController>() ?? UnityEngine.Object.FindAnyObjectByType<AtlasBoardChatUIController>();
    }

    private bool IsTabletOpen() => tablet != null && tablet.IsGameplayOverlayVisible;
    private bool IsModalOpen() => leaveFlow != null && leaveFlow.IsModalOverlayVisible;

    public void Toggle()
    {
        if (!IsVisible || IsTabletOpen() || IsModalOpen()) return;
        if (IsPanelOpen) ClosePanel();
        else Open(Tab.Developments);
    }

    private void Open(Tab selected)
    {
        if (!IsVisible || IsTabletOpen() || IsModalOpen()) return;
        BindScene();
        tab = selected;
        chat?.CloseForExternalOverlay();
        drawer.SetActive(true);
        Invalidate();
        RefreshView(true);
    }

    public void ClosePanel()
    {
        dropdownExpanded = false;
        if (drawer != null) drawer.SetActive(false);
    }

    private void SetTab(Tab selected)
    {
        dropdownExpanded = false;
        tab = selected;
        Invalidate();
        RefreshView(true);
    }

    private PlayerGameState ResolveLocalPlayer()
    {
        if (turns == null || !turns.IsMatchStarted) return null;
        if (coordinator != null && coordinator.IsOnlineSessionActive)
        {
            PlayerGameState[] players = UnityEngine.Object.FindObjectsByType<PlayerGameState>();
            foreach (PlayerGameState state in players)
                if (state != null && state.IsParticipating &&
                    coordinator.IsLocallyControlledHumanSlot(state.PlayerSlotIndex) &&
                    !state.IsOnlineBotControlled)
                    return state;
            return null;
        }

        PlayerGameState current = turns.CurrentPlayerState;
        return current != null && current.IsParticipating && !current.IsBankrupt && !turns.IsPlayerBot(current)
            ? current : null;
    }

    private void RefreshView(bool force)
    {
        if (drawer == null || !drawer.activeSelf || scrollContent == null) return;
        // Keep a city chooser stable while the list is open, even when the
        // current turn or balance changes. The next refresh after closing
        // reconciles all ownership/money changes from the read model.
        if (!force && dropdownExpanded) return;
        if (force) dropdownExpanded = false;
        PlayerGameState player = ResolveLocalPlayer();
        List<BoardTile> owned = new List<BoardTile>();
        if (player != null && board != null)
        {
            for (int i = 0; i < board.TileCount; i++)
            {
                BoardTile tile = board.GetTile(i);
                if (tile != null && tile.TileType == TileType.City && tile.IsOwned && tile.OwnerPlayerIndex == player.PlayerSlotIndex)
                    owned.Add(tile);
            }
        }

        string language = LanguageCode();
        string stamp = language + ":" + tab + ":" + (turns != null ? turns.CurrentRound : -1) + ":" +
                       (player != null ? player.PlayerSlotIndex + ":" + player.CurrentMoney : "none");
        foreach (BoardTile tile in owned)
        {
            stamp += ":" + tile.TileIndex + ":" + tile.OwnerPlayerIndex + ":" +
                     (development != null ? development.GetDevelopmentLevel(tile) : 0);
            if (development != null)
                for (int category = 0; category < 4; category++)
                    stamp += ":" + development.GetPulseLevel(tile, category) +
                             ":" + development.GetPulseMetric(tile, category);
        }
        if (!force && stamp == visibleSignature) return;

        visibleSignature = stamp;
        title.text = L("CITY MANAGEMENT", "ŞEHİR YÖNETİMİ", "GESTIÓN URBANA", "GESTION DES VILLES", "STADTVERWALTUNG", "도시 관리", "УПРАВЛЕНИЕ ГОРОДАМИ");
        launcherText.text = L("MANAGE", "YÖNET", "GESTIÓN", "GESTION", "VERWALT.", "관리", "УПРАВ.");
        developmentTabText.text = L("DEVELOPMENTS", "GELİŞTİRMELER", "DESARROLLOS", "DÉVELOPPEMENTS", "AUSBAU", "개발", "РАЗВИТИЕ");
        bankTabText.text = L("BANK", "BANKA", "BANCO", "BANQUE", "BANK", "은행", "БАНК");
        developmentTab.GetComponent<Image>().color = tab == Tab.Developments ? Accent : CardAlt;
        bankTab.GetComponent<Image>().color = tab == Tab.Bank ? Accent : CardAlt;
        status.text = player != null
            ? L("Balance", "Bakiye", "Saldo", "Solde", "Guthaben", "잔액", "Баланс") + ": " + player.CurrentMoney + " ₵  •  F8 / LB+START"
            : L("Waiting for a local human player", "Yerel insan oyuncu bekleniyor", "Esperando jugador local", "En attente d'un joueur", "Warte auf lokalen Spieler", "로컬 플레이어 대기 중", "Ожидание локального игрока");

        for (int i = scrollContent.childCount - 1; i >= 0; i--)
            Destroy(scrollContent.GetChild(i).gameObject);

        if (player == null)
        {
            AddNotice(L("Open during your human turn to inspect your cities and bank.",
                "Şehirleri ve bankayı incelemek için kendi turunda aç.",
                "Abre en tu turno para revisar ciudades y banco.",
                "Ouvrez pendant votre tour pour voir les villes et la banque.",
                "Im eigenen Zug öffnen, um Städte und Bank zu prüfen.",
                "자신의 턴에 도시와 은행을 확인하세요.",
                "Откройте во время своего хода, чтобы проверить города и банк."));
            return;
        }

        if (tab == Tab.Developments) DrawDevelopments(owned);
        else DrawBank(owned);
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
    }

    private void DrawDevelopments(List<BoardTile> owned)
    {
        if (owned.Count == 0)
        {
            AddNotice(L("You don't own a city yet.", "Henüz bir şehrin yok.", "Aún no tienes ciudades.", "Vous ne possédez pas encore de ville.", "Du besitzt noch keine Stadt.", "아직 소유한 도시가 없습니다.", "У вас пока нет города."));
            return;
        }

        AddNotice(L("Choose a city from the dropdown to inspect its current development view.",
            "Geliştirme görünümünü incelemek için açılır listeden bir şehir seç.",
            "Elige una ciudad del desplegable para revisar su desarrollo.",
            "Choisissez une ville dans la liste pour inspecter le développement.",
            "Wähle eine Stadt aus der Liste, um den Ausbau zu prüfen.",
            "드롭다운에서 도시를 선택해 개발 현황을 확인하세요.",
            "Выберите город из списка, чтобы проверить развитие."));

        int selectedIndex = ResolveSelectedIndex(owned, selectedCityId);
        selectedCityId = StableCityId(owned[selectedIndex]);
        AddCityDropdown(
            L("City", "Şehir", "Ciudad", "Ville", "Stadt", "도시", "Город"),
            owned, selectedIndex,
            index =>
            {
                BoardTile city = owned[Mathf.Clamp(index, 0, owned.Count - 1)];
                selectedCityId = StableCityId(city);
                UpdateSelectedCitySummary(city);
                UpdatePulseCards(city);
            });

        BoardTile selected = owned[Mathf.Clamp(selectedIndex, 0, owned.Count - 1)];
        AddSummaryCard(selected);
        if (development != null && development.GetSpeculativeRoundsRemaining(selected) > 0)
            AddNotice(development.GetEconomicMemorySummary(selected));
        AddMetricGrid();
        UpdatePulseCards(selected);
        AddNotice(L("Pulse projects cost money and increase rent. Each category can reach Level 4; scores stay below 95.",
            "Pulse projeleri ücretlidir ve kirayı artırır. Her kategori Seviye 4'e çıkar; puanlar 95'in altında kalır.",
            "Los proyectos Pulse cuestan dinero y suben el alquiler. Máximo nivel 4; valores por debajo de 95.",
            "Les projets Pulse coûtent de l'argent et augmentent le loyer. Niveau 4 max, scores sous 95.",
            "Pulse-Projekte kosten Geld und erhöhen die Miete. Maximal Stufe 4, Werte unter 95.",
            "Pulse 프로젝트는 유료이며 임대료를 올립니다. 최대 4레벨, 점수는 95 미만입니다.",
            "Проекты Pulse платные и повышают аренду. Максимум 4 уровень, показатели ниже 95."));
    }

    private void DrawBank(List<BoardTile> owned)
    {
        PlayerGameState player = ResolveLocalPlayer();
        if (player == null || development == null) return;
        int slot = player.PlayerSlotIndex;
        AddNotice(L("A mortgage freezes rent, Build and Trade until full repayment. Three collateralized loans maximum.",
            "İpotek, tam ödenene kadar kira, inşa ve takası durdurur. En çok 3 teminatlı kredi.",
            "La hipoteca bloquea alquiler, obra y cambio hasta el pago completo. Máximo 3 préstamos.",
            "L'hypothèque bloque loyer, travaux et échange jusqu'au paiement intégral. Trois prêts max.",
            "Eine Hypothek sperrt Miete, Bauen und Handel bis zur vollständigen Zahlung. Maximal drei Kredite.",
            "완납 전까지 저당 도시는 임대료·건설·거래가 중지됩니다. 최대 3개 대출.",
            "Ипотека блокирует аренду, строительство и обмен до полного погашения. До трёх кредитов."));
        AddSection(L("COLLATERAL CITY", "TEMİNAT ŞEHRİ", "CIUDAD GARANTÍA", "VILLE EN GARANTIE", "SICHERHEITSSTADT", "담보 도시", "ГОРОД-ЗАЛОГ"));
        BoardTile collateral = null;
        if (owned.Count == 0)
        {
            AddNotice(L("No owned cities.", "Sahip olunan şehir yok.", "No hay ciudad propia.", "Aucune ville possédée.", "Keine Stadt in Besitz.", "소유 도시 없음.", "Нет своей недвижимости."));
        }
        else
        {
            int selectedIndex = ResolveSelectedIndex(owned, selectedCollateralId);
            selectedCollateralId = StableCityId(owned[selectedIndex]);
            collateral = owned[selectedIndex];
            AddCityDropdown(L("Collateral", "Teminat", "Garantía", "Garantie", "Sicherheit", "담보", "Залог"),
                owned, selectedIndex, index =>
                {
                    selectedCollateralId = StableCityId(owned[Mathf.Clamp(index, 0, owned.Count - 1)]);
                    Invalidate();
                });
            AddNotice(L("Loan limit", "Kredi limiti", "Límite", "Limite", "Kreditlimit", "대출 한도", "Лимит") +
                ": " + development.GetBankLimit(collateral) + " ₵   •   " +
                (development.IsMortgaged(collateral)
                    ? L("MORTGAGED", "İPOTEKLİ", "HIPOTECADA", "HYPOTHÉQUÉE", "BELASTET", "저당 상태", "В ЗАЛОГЕ")
                    : L("AVAILABLE", "UYGUN", "DISPONIBLE", "DISPONIBLE", "VERFÜGBAR", "가능", "ДОСТУПНО")));
        }
        AddSection(L("LOAN APPLICATION", "KREDİ BAŞVURUSU", "SOLICITUD DE PRÉSTAMO", "DEMANDE DE PRÊT", "KREDITANTRAG", "대출 신청", "ЗАЯВКА НА КРЕДИТ"));
        AddAmountInput();
        AddSection(L("TERM / INTEREST", "VADE / FAİZ", "PLAZO / INTERÉS", "DURÉE / INTÉRÊT", "LAUFZEIT / ZINS", "기간 / 금리", "СРОК / ПРОЦЕНТ"));
        for (int i = 0; i < 3; i++)
        {
            int chosen = i;
            int turns = i == 0 ? 2 : i == 1 ? 3 : 5;
            int rate = i == 0 ? 8 : i == 1 ? 12 : 20;
            AddButton(scrollContent, turns + " " + L("own turns", "kendi turu", "turnos", "tours", "eigene Züge", "자신의 턴", "своих ходов") + "  •  " + rate + "%" +
                (i == bankTermKind ? "  ✓" : ""),
                () => { bankTermKind = chosen; Invalidate(); RefreshView(true); },
                i == bankTermKind ? AccentSoft : Card, 34f, true);
        }
        int parsedAmount = 0;
        int.TryParse(requestedLoanAmount, out parsedAmount);
        bool eligible = collateral != null && !bankPending &&
            development.CanOpenBankLoan(player, collateral, parsedAmount, bankTermKind);
        AddButton(scrollContent, L("APPLY LOAN", "KREDİ BAŞVUR", "SOLICITAR", "DEMANDER", "KREDIT AUFNEHMEN", "대출 신청", "ВЗЯТЬ КРЕДИТ"),
            eligible ? (UnityEngine.Events.UnityAction)(() => RequestBankOperation(collateral.TileIndex, false)) : null,
            eligible ? Accent : Muted, 42f, eligible);
        AddSection(L("ACTIVE LOANS", "AKTİF KREDİLER", "PRÉSTAMOS ACTIVOS", "PRÊTS ACTIFS", "AKTIVE KREDITE", "활성 대출", "АКТИВНЫЕ КРЕДИТЫ") +
            " (" + development.GetBankLoanCount(slot) + "/3)");
        List<PropertyDevelopmentManager.BankLoan> loans = development.GetPlayerLoans(slot);
        for (int i = 0; i < 3; i++)
        {
            if (i >= loans.Count)
            {
                AddNotice("#" + (i+1) + "  ·  " + L("Empty", "Boş", "Vacío", "Vide", "Leer", "비어 있음", "Пусто"));
                continue;
            }
            PropertyDevelopmentManager.BankLoan loan = loans[i];
            BoardTile mortgagedCity = board != null ? board.GetTile(loan.tileIndex) : null;
            AddNotice("#" + (i+1) + "  " + (mortgagedCity != null ? mortgagedCity.BoardDisplayName : "City") +
                "   •   " + L("Remaining", "Kalan borç", "Restante", "Restant", "Restschuld", "잔액", "Остаток") + ": " + loan.outstanding + " ₵\n" +
                L("Due in", "Kalan vade", "Vence en", "Échéance", "Fällig in", "남은 기간", "До погашения") + ": " +
                loan.turnsRemaining + " " + L("turn(s)", "tur", "turnos", "tours", "Züge", "턴", "ходов") +
                "  •  " + loan.ratePercent + "%");
            bool canRepay = !bankPending && player.CurrentMoney >= loan.outstanding;
            int captured = loan.tileIndex;
            AddButton(scrollContent, L("REPAY IN FULL", "TAMAMINI ÖDE", "PAGAR TODO", "REMBOURSER", "VOLL TILGEN", "전액 상환", "ПОГАСИТЬ ПОЛНОСТЬЮ"),
                canRepay ? (UnityEngine.Events.UnityAction)(() => RequestBankOperation(captured, true)) : null,
                canRepay ? AccentSoft : Muted, 36f, canRepay);
        }
    }

    private async void RequestBankOperation(int cityTileIndex, bool repay)
    {
        if (bankPending || development == null || board == null || turns == null) return;
        PlayerGameState player = ResolveLocalPlayer();
        BoardTile city = board.GetTile(cityTileIndex);
        if (player == null || city == null) return;
        int amount = 0;
        int.TryParse(requestedLoanAmount, out amount);
        if (!repay && !development.CanOpenBankLoan(player, city, amount, bankTermKind)) return;
        bankPending = true;
        try
        {
            bool success = coordinator != null && coordinator.IsOnlineSessionActive
                ? await coordinator.RequestBankActionAsync(cityTileIndex, amount, bankTermKind, repay)
                : repay ? development.TryRepayBankLoan(player, city, Guid.NewGuid().ToString("N"))
                         : development.TryOpenBankLoan(player, city, amount, bankTermKind, Guid.NewGuid().ToString("N"));
            Debug.Log("ATB_PHASE18D_BANK " + (success ? "SUCCESS" : "REJECT") +
                      " action=" + (repay ? "repay" : "open") + " tile=" + cityTileIndex, this);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_PHASE18D_BANK ERROR=" + ex.GetType().Name, this);
        }
        finally
        {
            bankPending = false;
            Invalidate();
            RefreshView(true);
        }
    }

    private void AddSummaryCard(BoardTile selected)
    {
        GameObject card = CreatePanelCard(scrollContent, "SelectedCitySummary", 94f, CardSoft);
        selectedCitySummaryHeader = AddText(card.transform, "CityHeader", string.Empty,
            16f, FontStyles.Bold, Light);
        PlaceTopStretch(selectedCitySummaryHeader.rectTransform, 14f, 14f, 14f, 26f);
        selectedCitySummaryHeader.enableAutoSizing = true;
        selectedCitySummaryHeader.fontSizeMin = 12f;
        selectedCitySummaryHeader.fontSizeMax = 16f;

        selectedCitySummaryDetails = AddText(card.transform, "CityInfo", string.Empty,
            13f, FontStyles.Normal, Muted);
        PlaceTopStretch(selectedCitySummaryDetails.rectTransform, 14f, 47f, 14f, 31f);
        selectedCitySummaryDetails.enableAutoSizing = true;
        selectedCitySummaryDetails.fontSizeMin = 10f;
        selectedCitySummaryDetails.fontSizeMax = 13f;
        UpdateSelectedCitySummary(selected);
    }

    private void UpdateSelectedCitySummary(BoardTile city)
    {
        if (city == null) return;
        // Update only the two labels when the city selection changes. Destroying and
        // rebuilding the dropdown in its own onClick callback causes the white flash.
        if (selectedCitySummaryHeader != null)
            selectedCitySummaryHeader.text = L("CITY", "ŞEHİR", "CIUDAD", "VILLE", "STADT", "도시", "ГОРОД") + ": " + city.BoardDisplayName;
        if (selectedCitySummaryDetails != null)
        {
            int level = development != null ? development.GetDevelopmentLevel(city) : 0;
            selectedCitySummaryDetails.text =
                L("Legacy houses", "Mevcut ev seviyesi", "Casas existentes", "Maisons existantes", "Vorhandene Häuser", "기존 주택", "Обычные дома") + ": " + level + "/4    •    " +
                L("Base rent", "Temel kira", "Renta base", "Loyer de base", "Grundmiete", "기본 임대료", "Базовая аренда") + ": " + city.BaseRent + " ₵    •    " +
                L("Current rent", "Güncel kira", "Alquiler actual", "Loyer actuel", "Aktuelle Miete", "현재 임대료", "Текущая аренда") + ": " +
                (development != null ? development.GetEffectiveRent(city) : city.BaseRent) + " ₵" +
                (development != null && development.IsMortgaged(city)
                    ? "   •   " + L("MORTGAGED", "İPOTEKLİ", "HIPOTECADA", "HYPOTHÉQUÉE", "BELASTET", "저당 상태", "В ЗАЛОГЕ")
                    : string.Empty);
        }
    }

    private void AddMetricGrid()
    {
        GameObject row = new GameObject("MetricGrid", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(scrollContent, false);
        row.GetComponent<LayoutElement>().minHeight = 392f;
        HorizontalLayoutGroup horizontal = row.GetComponent<HorizontalLayoutGroup>();
        horizontal.spacing = 12f;
        horizontal.padding = new RectOffset(0, 0, 2, 2);
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = true;
        horizontal.childForceExpandHeight = false;

        GameObject leftColumn = CreateMetricColumn(row.transform, "LeftColumn");
        GameObject rightColumn = CreateMetricColumn(row.transform, "RightColumn");
        AddMetricCard(leftColumn.transform, 0, L("Demand", "Talep", "Demanda", "Demande", "Nachfrage", "수요", "Спрос"));
        AddMetricCard(leftColumn.transform, 1, L("Prosperity", "Refah", "Prosperidad", "Prospérité", "Wohlstand", "번영", "Благосостояние"));
        AddMetricCard(rightColumn.transform, 2, L("Traffic", "Ulaşım", "Tráfico", "Trafic", "Verkehr", "교통", "Транспорт"));
        AddMetricCard(rightColumn.transform, 3, L("Stability", "İstikrar", "Estabilidad", "Stabilité", "Stabilität", "안정성", "Стабильность"));
    }

    private static GameObject CreateMetricColumn(Transform parent, string name)
    {
        GameObject column = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
        column.transform.SetParent(parent, false);
        LayoutElement element = column.GetComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        VerticalLayoutGroup vertical = column.GetComponent<VerticalLayoutGroup>();
        vertical.spacing = 12f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        return column;
    }

    private void AddMetricCard(Transform parent, int category, string metricName)
    {
        GameObject card = CreatePanelCard(parent, "Metric_" + category, 186f, CardAlt);
        TMP_Text titleLabel = AddText(card.transform, "MetricTitle", metricName, 15f, FontStyles.Bold, Light);
        PlaceTopStretch(titleLabel.rectTransform, 12f, 12f, 12f, 25f);
        titleLabel.enableAutoSizing = true;
        titleLabel.fontSizeMin = 11f;
        titleLabel.fontSizeMax = 15f;

        pulseValues[category] = AddText(card.transform, "MetricValue", "— / 100", 19f, FontStyles.Bold, Light);
        PlaceTopStretch(pulseValues[category].rectTransform, 12f, 41f, 12f, 24f);
        pulseDescriptions[category] = AddText(card.transform, "MetricInfo", string.Empty, 12f, FontStyles.Normal, Muted);
        PlaceTopStretch(pulseDescriptions[category].rectTransform, 12f, 76f, 12f, 44f);
        pulseDescriptions[category].enableAutoSizing = true;
        pulseDescriptions[category].fontSizeMin = 9f;
        pulseDescriptions[category].fontSizeMax = 12f;
        pulseDescriptions[category].textWrappingMode = TextWrappingModes.Normal;

        int capturedCategory = category;
        GameObject buttonObject = AddButton(card.transform, string.Empty, () => RequestPulseBuild(capturedCategory), Muted, 36f, false);
        PlaceTopStretch(buttonObject.transform as RectTransform, 12f, 137f, 12f, 36f);
        pulseBuildButtons[category] = buttonObject.GetComponent<Button>();
        pulseBuildButtons[category].transition = Selectable.Transition.None;
    }

    private void UpdatePulseCards(BoardTile city)
    {
        PlayerGameState player = ResolveLocalPlayer();
        for (int category = 0; category < 4; category++)
        {
            if (pulseValues[category] == null || pulseBuildButtons[category] == null) continue;
            int level = development != null ? development.GetPulseLevel(city, category) : 0;
            int metric = development != null ? development.GetPulseMetric(city, category) : 0;
            int cost = development != null ? development.GetPulseBuildCost(city, category) : 0;
            int bonus = development != null ? development.GetPulseRentBonusPercent(city) : 0;
            pulseValues[category].text = metric + " / 100";
            pulseDescriptions[category].text =
                L("Level", "Seviye", "Nivel", "Niveau", "Stufe", "레벨", "Уровень") + " " + level + "/4   •   " +
                (level < 4 ? PulseProjectName(category, level + 1) : L("Complete", "Tamamlandı", "Completado", "Terminé", "Fertig", "완료", "Завершено")) +
                "   •   +" + bonus + "% " + L("rent", "kira", "renta", "loyer", "Miete", "임대료", "аренда");
            Button button = pulseBuildButtons[category];
            bool canBuild = !pulseBuildPending && development != null &&
                development.CanBuildPulse(player, city, category);
            button.interactable = canBuild;
            button.GetComponent<Image>().color = canBuild ? Accent : Muted;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = level >= 4 ? L("MAX LEVEL", "SON SEVİYE", "NIVEL MÁX.", "NIVEAU MAX", "MAX. STUFE", "최대 레벨", "МАКС. УРОВЕНЬ")
                    : L("BUILD", "İNŞA ET", "CONSTRUIR", "CONSTRUIRE", "BAUEN", "건설", "СТРОИТЬ") + "  " + cost + " ₵";
        }
    }

    private static string PulseProjectName(int category, int nextLevel)
    {
        switch (category * 4 + nextLevel)
        {
            case 1: return L("Local Market", "Yerel Pazar", "Mercado local", "Marché local", "Lokaler Markt", "지역 시장", "Местный рынок");
            case 2: return L("Shopping Center", "Alışveriş Merkezi", "Centro comercial", "Centre commercial", "Einkaufszentrum", "쇼핑 센터", "Торговый центр");
            case 3: return L("Commercial Plaza", "Ticaret Meydanı", "Plaza comercial", "Place commerciale", "Geschäftsplatz", "상업 광장", "Торговая площадь");
            case 4: return L("Global Marketplace", "Küresel Pazar", "Mercado global", "Marché mondial", "Weltmarkt", "세계 시장", "Мировой рынок");
            case 5: return L("Business Office", "İş Ofisi", "Oficina comercial", "Bureau d'affaires", "Geschäftsbüro", "비즈니스 오피스", "Бизнес-офис");
            case 6: return L("Corporate Center", "Kurumsal Merkez", "Centro empresarial", "Centre d'affaires", "Firmenzentrum", "기업 센터", "Корпоративный центр");
            case 7: return L("Financial District", "Finans Bölgesi", "Distrito financiero", "Quartier financier", "Finanzviertel", "금융 지구", "Финансовый район");
            case 8: return L("Economic Hub", "Ekonomi Merkezi", "Centro económico", "Pôle économique", "Wirtschaftszentrum", "경제 허브", "Экономический узел");
            case 9: return L("Bus Terminal", "Otobüs Terminali", "Terminal de autobuses", "Gare routière", "Busbahnhof", "버스 터미널", "Автовокзал");
            case 10: return L("Transit Station", "Aktarma İstasyonu", "Estación de tránsito", "Station de transit", "Umsteigebahnhof", "환승역", "Пересадочная станция");
            case 11: return L("Metro Network", "Metro Ağı", "Red de metro", "Réseau de métro", "U-Bahn-Netz", "지하철망", "Сеть метро");
            case 12: return L("Transport Megahub", "Ulaşım Mega Merkezi", "Gran nodo de transporte", "Méga-pôle de transport", "Verkehrs-Megahub", "대형 교통 허브", "Крупный транспортный узел");
            case 13: return L("Security Office", "Güvenlik Ofisi", "Oficina de seguridad", "Bureau de sécurité", "Sicherheitsbüro", "보안 사무소", "Отдел безопасности");
            case 14: return L("Safety Network", "Güvenlik Ağı", "Red de seguridad", "Réseau de sécurité", "Sicherheitsnetz", "안전망", "Сеть безопасности");
            case 15: return L("Emergency Center", "Acil Durum Merkezi", "Centro de emergencias", "Centre d'urgence", "Notfallzentrum", "비상 센터", "Центр экстренной помощи");
            case 16: return L("Resilient City", "Dirençli Şehir", "Ciudad resiliente", "Ville résiliente", "Widerstandsfähige Stadt", "회복력 있는 도시", "Устойчивый город");
            default: return string.Empty;
        }
    }

    private async void RequestPulseBuild(int category)
    {
        if (pulseBuildPending || board == null || development == null || turns == null) return;
        BoardTile city = null;
        for (int i = 0; i < board.TileCount; i++)
        {
            BoardTile candidate = board.GetTile(i);
            if (candidate != null && StableCityId(candidate) == selectedCityId) { city = candidate; break; }
        }
        PlayerGameState player = ResolveLocalPlayer();
        if (!development.CanBuildPulse(player, city, category)) return;
        pulseBuildPending = true;
        UpdatePulseCards(city);
        try
        {
            bool success = coordinator != null && coordinator.IsOnlineSessionActive
                ? await coordinator.RequestPulseBuildAsync(city.TileIndex, category)
                : development.TryBuildPulse(player, city, category, Guid.NewGuid().ToString("N"));
            Debug.Log("ATB_PHASE18B_BUILD " + (success ? "SUCCESS" : "REJECTED") +
                      " tile=" + city.TileIndex + " category=" + category, this);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("ATB_PHASE18B_BUILD ERROR=" + ex.GetType().Name, this);
        }
        finally
        {
            pulseBuildPending = false;
            Invalidate();
            RefreshView(true);
        }
    }

    private static GameObject CreatePanelCard(Transform parent, string name, float height, Color color)
    {
        GameObject card = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        card.transform.SetParent(parent, false);
        card.GetComponent<Image>().color = color;
        LayoutElement layout = card.GetComponent<LayoutElement>();
        layout.minHeight = height;
        return card;
    }

    private void AddCityDropdown(string label, List<BoardTile> owned, int selectedIndex, Action<int> onChanged)
    {
        List<string> options = new List<string>(owned.Count);
        for (int i = 0; i < owned.Count; i++)
            options.Add(owned[i].BoardDisplayName);
        AddDropdown(label, options, selectedIndex, onChanged);
    }

    private void AddDropdown(string label, List<string> options, int selectedIndex, Action<int> onChanged)
    {
        if (options == null || options.Count == 0) return;
        // A bounded inline dropdown: options expand the actual layout row and push
        // the city summary down instead of drawing through it. No TMP_Dropdown
        // template clones or destroying the dropdown while it is processing a click.
        const float collapsedHeight = 96f;
        const float optionHeight = 36f;
        int visibleRows = Mathf.Min(5, options.Count);
        float listHeight = visibleRows * optionHeight + Mathf.Max(0, visibleRows - 1) * 3f + 12f;

        GameObject root = CreatePanelCard(scrollContent, label + "DropdownRow", collapsedHeight, CardSoft);
        LayoutElement rowLayout = root.GetComponent<LayoutElement>();
        TMP_Text titleLabel = AddText(root.transform, "DropdownLabel", label.ToUpperInvariant(), 13f, FontStyles.Bold, Muted);
        PlaceTopStretch(titleLabel.rectTransform, 14f, 10f, 14f, 20f);

        GameObject selectorObject = AddButton(root.transform, string.Empty, null, Card, 40f, true);
        selectorObject.name = "DropdownSelector";
        PlaceTopStretch(selectorObject.transform as RectTransform, 14f, 39f, 14f, 40f);
        Button selector = selectorObject.GetComponent<Button>();
        selector.transition = Selectable.Transition.None; // Avoid white hover/pressed tint.
        TMP_Text caption = selectorObject.GetComponentInChildren<TMP_Text>();
        int currentIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, options.Count - 1));
        caption.text = options.Count > 0 ? options[currentIndex] : string.Empty;
        caption.alignment = TextAlignmentOptions.MidlineLeft;
        caption.fontSize = 14f;
        caption.enableAutoSizing = true;
        caption.fontSizeMin = 11f;
        caption.fontSizeMax = 14f;
        Stretch(caption.rectTransform, 14f, 2f, 37f, 2f);

        TMP_Text caret = AddText(selectorObject.transform, "Caret", "v", 16f, FontStyles.Bold, Light);
        RectTransform caretRect = caret.rectTransform;
        caretRect.anchorMin = new Vector2(1f, 0.5f);
        caretRect.anchorMax = new Vector2(1f, 0.5f);
        caretRect.pivot = new Vector2(1f, 0.5f);
        caretRect.anchoredPosition = new Vector2(-12f, 0f);
        caretRect.sizeDelta = new Vector2(20f, 28f);
        caret.alignment = TextAlignmentOptions.Center;

        GameObject list = new GameObject("DropdownChoices", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        list.transform.SetParent(root.transform, false);
        list.GetComponent<Image>().color = Navy;
        PlaceTopStretch(list.transform as RectTransform, 14f, 88f, 14f, listHeight);
        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(list.transform, false);
        RectTransform viewportRect = viewport.transform as RectTransform;
        Stretch(viewportRect, 2f, 2f, 2f, 2f);
        viewport.GetComponent<Image>().color = Card;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.transform as RectTransform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup group = content.GetComponent<VerticalLayoutGroup>();
        group.spacing = 3f;
        group.padding = new RectOffset(5, 5, 5, 5);
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect listScroll = list.GetComponent<ScrollRect>();
        listScroll.viewport = viewportRect;
        listScroll.content = contentRect;
        listScroll.horizontal = false;
        listScroll.vertical = true;
        listScroll.movementType = ScrollRect.MovementType.Clamped;
        listScroll.scrollSensitivity = 20f;

        List<Button> optionButtons = new List<Button>(options.Count);
        for (int i = 0; i < options.Count; i++)
        {
            int optionIndex = i;
            GameObject option = AddButton(content.transform, options[i], null,
                optionIndex == currentIndex ? AccentSoft : CardAlt, optionHeight, true);
            option.name = "CityChoice_" + i;
            Button optionButton = option.GetComponent<Button>();
            optionButton.transition = Selectable.Transition.None;
            optionButtons.Add(optionButton);
            TMP_Text optionCaption = option.GetComponentInChildren<TMP_Text>();
            optionCaption.alignment = TextAlignmentOptions.MidlineLeft;
            optionCaption.fontSizeMin = 11f;
            Stretch(optionCaption.rectTransform, 12f, 2f, 10f, 2f);
            optionButton.onClick.AddListener(() =>
            {
                currentIndex = optionIndex;
                caption.text = options[optionIndex];
                for (int j = 0; j < optionButtons.Count; j++)
                    optionButtons[j].GetComponent<Image>().color = j == currentIndex ? AccentSoft : CardAlt;
                list.SetActive(false);
                dropdownExpanded = false;
                rowLayout.minHeight = collapsedHeight;
                caret.text = "v";
                LayoutRebuilder.MarkLayoutForRebuild(scrollContent);
                onChanged?.Invoke(optionIndex);
            });
        }
        list.SetActive(false);
        selector.onClick.AddListener(() =>
        {
            bool willOpen = !list.activeSelf;
            list.SetActive(willOpen);
            dropdownExpanded = willOpen;
            rowLayout.minHeight = willOpen ? 88f + listHeight + 12f : collapsedHeight;
            caret.text = willOpen ? "^" : "v";
            LayoutRebuilder.MarkLayoutForRebuild(scrollContent);
        });
    }

    private void AddAmountInput()
    {
        GameObject inputRoot = new GameObject("LoanAmountDraft", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(TMP_InputField));
        inputRoot.transform.SetParent(scrollContent, false);
        inputRoot.GetComponent<Image>().color = CardSoft;
        inputRoot.GetComponent<LayoutElement>().minHeight = 48f;
        TMP_InputField input = inputRoot.GetComponent<TMP_InputField>();
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.characterLimit = 9;
        RectTransform area = CreateRect(inputRoot.transform, "InputTextArea");
        Stretch(area, 12f, 4f, 12f, 4f);
        TMP_Text value = AddText(area, "RequestedAmount", requestedLoanAmount, 16f, FontStyles.Normal, Light);
        Stretch(value.rectTransform, 0f, 0f, 0f, 0f);
        value.alignment = TextAlignmentOptions.MidlineLeft;
        TMP_Text hint = AddText(area, "Hint", L("Enter loan amount",
                "Kredi tutarını gir", "Importe solicitado (taslak)",
                "Montant demandé (brouillon)", "Gewünschter Betrag (Entwurf)",
                "요청 금액 (초안)", "Запрошенная сумма (черновик)"),
            13f, FontStyles.Italic, Muted);
        Stretch(hint.rectTransform, 0f, 0f, 0f, 0f);
        input.textViewport = area;
        input.textComponent = value as TextMeshProUGUI;
        input.placeholder = hint as Graphic;
        input.SetTextWithoutNotify(requestedLoanAmount);
        input.onValueChanged.AddListener(valueChanged => requestedLoanAmount = valueChanged);
    }

    private static string StableCityId(BoardTile tile) =>
        tile != null && !string.IsNullOrWhiteSpace(tile.PropertyId)
            ? tile.PropertyId : (tile != null ? tile.TileIndex.ToString() : string.Empty);

    private static int ResolveSelectedIndex(List<BoardTile> tiles, string selectedId)
    {
        for (int i = 0; i < tiles.Count; i++)
            if (StableCityId(tiles[i]) == selectedId)
                return i;
        return 0;
    }

    private void AddSection(string content)
    {
        TMP_Text label = AddText(scrollContent, "Section", content, 16f, FontStyles.Bold, Light);
        LayoutElement element = label.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 30f;
    }

    private void AddNotice(string content)
    {
        TMP_Text label = AddText(scrollContent, "Info", content, 13f, FontStyles.Normal, Muted);
        LayoutElement element = label.gameObject.AddComponent<LayoutElement>();
        element.minHeight = 34f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 11f;
        label.fontSizeMax = 13f;
        label.textWrappingMode = TextWrappingModes.Normal;
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas_AtlasManagement", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 545;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        safeAreaRoot = CreateRect(canvasObject.transform, "SafeArea");
        Stretch(safeAreaRoot, 0f, 0f, 0f, 0f);
        launcher = AddButton(safeAreaRoot, "MANAGE", Toggle, CardAlt, 42f, true);
        launcherText = launcher.GetComponentInChildren<TMP_Text>();
        RectTransform launcherRect = launcher.transform as RectTransform;
        launcherRect.anchorMin = new Vector2(0f, 0.5f);
        launcherRect.anchorMax = new Vector2(0f, 0.5f);
        launcherRect.pivot = new Vector2(0f, 0.5f);
        launcherRect.anchoredPosition = new Vector2(8f, 0f);
        launcherRect.sizeDelta = new Vector2(64f, 182f);
        launcherText.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
        launcherText.fontSize = 12f;
        launcherText.enableAutoSizing = true;
        launcherText.fontSizeMin = 10f;
        launcherText.fontSizeMax = 12f;
        launcherText.margin = new Vector4(0f, 8f, 0f, 8f);

        drawer = new GameObject("DevelopmentsBankDrawer", typeof(RectTransform), typeof(Image), typeof(Outline));
        drawer.transform.SetParent(safeAreaRoot, false);
        RectTransform drawerRect = drawer.transform as RectTransform;
        drawerRect.anchorMin = new Vector2(0f, 0.5f);
        drawerRect.anchorMax = new Vector2(0f, 0.5f);
        drawerRect.pivot = new Vector2(0f, 0.5f);
        drawerRect.anchoredPosition = new Vector2(78f, 0f);
        drawerRect.sizeDelta = new Vector2(640f, 820f);
        drawer.GetComponent<Image>().color = Navy;
        Outline outline = drawer.GetComponent<Outline>();
        outline.effectColor = Accent;
        outline.effectDistance = new Vector2(2f, -2f);

        title = AddText(drawer.transform, "Title", "CITY MANAGEMENT", 22f, FontStyles.Bold, Light);
        Place(title.rectTransform, 18f, -46f, 522f, 36f);
        title.enableAutoSizing = true;
        title.fontSizeMin = 16f;
        title.fontSizeMax = 22f;

        GameObject close = AddButton(drawer.transform, "×", ClosePanel, CardAlt, 38f, true);
        Place(close.transform as RectTransform, 584f, -46f, 38f, 38f);

        developmentTab = AddButton(drawer.transform, "DEVELOPMENTS", () => SetTab(Tab.Developments), Accent, 40f, true).GetComponent<Button>();
        Place(developmentTab.transform as RectTransform, 18f, -98f, 294f, 40f);
        developmentTabText = developmentTab.GetComponentInChildren<TMP_Text>();
        developmentTabText.enableAutoSizing = true;
        developmentTabText.fontSizeMin = 11f;
        developmentTabText.fontSizeMax = 14f;

        bankTab = AddButton(drawer.transform, "BANK", () => SetTab(Tab.Bank), CardAlt, 40f, true).GetComponent<Button>();
        Place(bankTab.transform as RectTransform, 328f, -98f, 294f, 40f);
        bankTabText = bankTab.GetComponentInChildren<TMP_Text>();
        bankTabText.enableAutoSizing = true;
        bankTabText.fontSizeMin = 11f;
        bankTabText.fontSizeMax = 14f;

        status = AddText(drawer.transform, "Status", string.Empty, 14f, FontStyles.Normal, Muted);
        Place(status.rectTransform, 18f, -148f, 604f, 34f);
        status.enableAutoSizing = true;
        status.fontSizeMin = 11f;
        status.fontSizeMax = 14f;

        BuildScroll();
        drawer.SetActive(false);
        launcher.SetActive(false);
        ApplySafeArea();
        layoutInitialized = true;
    }

    private void BuildScroll()
    {
        GameObject objectRoot = new GameObject("PanelScroll", typeof(RectTransform), typeof(ScrollRect));
        objectRoot.transform.SetParent(drawer.transform, false);
        RectTransform rect = objectRoot.transform as RectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(16f, 16f);
        rect.offsetMax = new Vector2(-16f, -186f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewport.transform.SetParent(objectRoot.transform, false);
        RectTransform viewportRect = viewport.transform as RectTransform;
        Stretch(viewportRect, 0f, 0f, 0f, 0f);
        viewport.GetComponent<Image>().color = new Color(0.025f, 0.045f, 0.065f, 0.82f);

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        scrollContent = content.transform as RectTransform;
        scrollContent.anchorMin = new Vector2(0f, 1f);
        scrollContent.anchorMax = new Vector2(1f, 1f);
        scrollContent.pivot = new Vector2(0.5f, 1f);
        scrollContent.offsetMin = new Vector2(10f, 0f);
        scrollContent.offsetMax = new Vector2(-10f, 0f);

        VerticalLayoutGroup group = content.GetComponent<VerticalLayoutGroup>();
        group.childAlignment = TextAnchor.UpperLeft;
        group.spacing = 7f;
        group.padding = new RectOffset(8, 8, 10, 12);
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = objectRoot.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = scrollContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 23f;
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj.transform as RectTransform;
    }

    private static TMP_Text AddText(Transform parent, string name, string value, float size, FontStyles style, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TMP_Text label = obj.GetComponent<TMP_Text>();
        label.text = value;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = TextAlignmentOptions.Left;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        AtlasBoardLocalizationManager local = AtlasBoardLocalizationManager.Instance;
        if (local != null) label.font = local.ResolveFont(label.font);
        return label;
    }

    private static GameObject AddButton(Transform parent, string label, UnityEngine.Events.UnityAction action, Color color, float height, bool enabled)
    {
        GameObject obj = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        Button button = obj.GetComponent<Button>();
        button.interactable = enabled;
        if (action != null) button.onClick.AddListener(action);
        LayoutElement layout = obj.GetComponent<LayoutElement>();
        layout.minHeight = height;

        TMP_Text text = AddText(obj.transform, "Label", label, 14f, FontStyles.Bold, Light);
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 14f;
        Stretch(text.rectTransform, 6f, 2f, 6f, 2f);
        return obj;
    }

    private void ApplySafeArea()
    {
        if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
        Rect area = Screen.safeArea;
        Vector2 min = area.position;
        Vector2 max = area.position + area.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;
        safeAreaRoot.anchorMin = min;
        safeAreaRoot.anchorMax = max;
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;
        lastSafeArea = area;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        if (drawer != null)
        {
            float referenceWidth = 1920f * area.width / Screen.width;
            RectTransform rect = drawer.transform as RectTransform;
            rect.sizeDelta = new Vector2(Mathf.Min(640f, Mathf.Max(390f, referenceWidth - 96f)),
                Mathf.Min(820f, Mathf.Max(360f, 1080f * area.height / Screen.height - 70f)));
        }
    }

    private static void PlaceTopStretch(RectTransform rect, float left, float top, float right, float height)
    {
        // Width stays inside its card as the parent resizes or language changes.
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2((left - right) * 0.5f, -top);
        rect.sizeDelta = new Vector2(-left - right, height);
    }

    private static void Place(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void Invalidate() => visibleSignature = string.Empty;

    private static string LanguageCode() => AtlasBoardLocalizationManager.Instance != null
        ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";

    private static string L(string en, string tr, string es, string fr, string de, string ko, string ru) =>
        AtlasBoardL.R(en, tr, es, fr, de, ko, ru);

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Atlas Board/QA & Diagnostics/QA/Phase 18A - Desktop UI Smoke")]
    private static void DesktopSmoke()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("ATB_PHASE18A_DESKTOP_QA NOT_RUN: enter Play Mode and start a local match first.");
            return;
        }

        AtlasBoardDevelopmentsBankUI ui = UnityEngine.Object.FindAnyObjectByType<AtlasBoardDevelopmentsBankUI>();
        if (ui == null)
        {
            Debug.LogError("ATB_PHASE18A_DESKTOP_QA FAIL: panel was not bootstrapped.");
            return;
        }

        ui.BindScene();
        bool ready = ui.turns != null && ui.turns.IsMatchStarted;
        bool canvasReady = ui.canvas != null && ui.scroll != null && ui.launcher != null;
        if (ready && canvasReady)
        {
            ui.launcher.SetActive(true);
            ui.Open(Tab.Developments);
            bool developmentsVisible = ui.IsPanelOpen && ui.tab == Tab.Developments;
            ui.SetTab(Tab.Bank);
            bool bankVisible = ui.IsPanelOpen && ui.tab == Tab.Bank;
            ui.ClosePanel();
            Debug.Log(developmentsVisible && bankVisible && !ui.IsPanelOpen
                ? "ATB_PHASE18A_DESKTOP_QA PASS: launcher, Developments, Bank, close (UI-only); Build/Repay locked until 18B/18D."
                : "ATB_PHASE18A_DESKTOP_QA FAIL: drawer tabs/close failed.");
        }
        else
        {
            Debug.LogError("ATB_PHASE18A_DESKTOP_QA FAIL: active match or required canvas/scroll missing.");
        }
    }
#endif
}
