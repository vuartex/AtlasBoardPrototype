using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class AtlasBoardProgressionUI : MonoBehaviour
{
    private enum ProgressionPage
    {
        Overview,
        History,
        Achievements
    }

    private static readonly Color BackgroundBlue =
        new Color32(151, 205, 232, 255);
    private static readonly Color Blue =
        new Color32(28, 157, 211, 255);
    private static readonly Color DarkBlue =
        new Color32(19, 113, 163, 255);
    private static readonly Color Green =
        new Color32(139, 170, 16, 255);
    private static readonly Color Orange =
        new Color32(248, 175, 0, 255);
    private static readonly Color Red =
        new Color32(226, 39, 90, 255);
    private static readonly Color Cream =
        new Color32(246, 241, 231, 255);
    private static readonly Color PanelBlue =
        new Color32(226, 239, 245, 255);
    private static readonly Color TextDark =
        new Color32(61, 62, 66, 255);
    private static readonly Color Muted =
        new Color32(103, 108, 115, 255);

    private AtlasBoardProgressionBridge bridge;
    private Canvas canvas;
    private GameObject root;
    private GameObject detailsRoot;
    private RectTransform contentRoot;
    private TMP_Text titleText;
    private TMP_Text detailsTitleText;
    private TMP_Text detailsBodyText;
    private TMP_Text detailsOkText;
    private RectTransform detailsPlayersRoot;
    private GameObject menuCard;
    private TMP_Text menuCardTitle;
    private TMP_Text menuCardSubtitle;
    private TMP_Text menuCardGlyph;

    private AtlasBoardProgressionSnapshot snapshot;
    private ProgressionPage currentPage = ProgressionPage.Overview;
    private string selectedMapId = string.Empty;
    private bool loading;
    private float nextBindAt;
    private float nextProfileRefreshAt;
    private float nextLevelSyncAt;

    private void Awake()
    {
        bridge = GetComponent<AtlasBoardProgressionBridge>();
        BuildUi();
        root.SetActive(false);
        detailsRoot.SetActive(false);

        if (bridge != null)
        {
            bridge.ProfileChanged += HandleProfileChanged;
        }

        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;
    }

    private void OnDestroy()
    {
        if (bridge != null)
        {
            bridge.ProfileChanged -= HandleProfileChanged;
        }

        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;
    }

    private async void Start()
    {
        await RefreshProfileAsync(false);
    }

    private void Update()
    {
        if (menuCard == null && Time.unscaledTime >= nextBindAt)
        {
            nextBindAt = Time.unscaledTime + 1f;
            TryCreateCareerCard();
        }

        if (Time.unscaledTime >= nextLevelSyncAt)
        {
            nextLevelSyncAt = Time.unscaledTime + 1f;
            SyncSharedProfileLevel();
        }

        if (snapshot == null &&
            !loading &&
            Time.unscaledTime >= nextProfileRefreshAt)
        {
            nextProfileRefreshAt = Time.unscaledTime + 5f;
            _ = RefreshProfileAsync(false);
        }
    }

    private void HandleProfileChanged(AtlasBoardProgressionSnapshot next)
    {
        if (next == null || !next.Success)
        {
            return;
        }

        snapshot = next;
        SyncSharedProfileLevel();
        if (root != null && root.activeSelf)
        {
            RebuildCurrentPage();
        }
    }

    private void HandleLanguageChanged()
    {
        RefreshCardLocalization();
        ApplyResolvedFonts();
        SyncSharedProfileLevel();

        if (root != null && root.activeSelf)
        {
            RefreshUiLocalization();
            RebuildCurrentPage();
            ApplyResolvedFonts();
        }
    }

    private void TryCreateCareerCard()
    {
        Button shopButton = FindSceneButton("ShopCard");
        if (shopButton == null)
        {
            return;
        }

        Transform parent = shopButton.transform.parent;
        Transform existing = FindDirectChild(parent, "ProgressionCard");
        if (existing != null)
        {
            menuCard = existing.gameObject;
        }
        else
        {
            menuCard = Instantiate(shopButton.gameObject, parent, false);
            menuCard.name = "ProgressionCard";
        }

        RectTransform cardRect = menuCard.GetComponent<RectTransform>();
        RectTransform shopRect = shopButton.GetComponent<RectTransform>();
        Button publicRoomsButton = FindSceneButton("PublicRoomsCard");
        RectTransform publicRect = publicRoomsButton != null
            ? publicRoomsButton.GetComponent<RectTransform>()
            : null;

        if (cardRect != null && shopRect != null)
        {
            cardRect.anchoredPosition = new Vector2(
                shopRect.anchoredPosition.x,
                publicRect != null
                    ? publicRect.anchoredPosition.y
                    : shopRect.anchoredPosition.y - 365f);
            cardRect.sizeDelta = shopRect.sizeDelta;
        }

        menuCardTitle =
            FindRecursive(menuCard.transform, "Title")?.GetComponent<TMP_Text>();
        menuCardSubtitle =
            FindRecursive(menuCard.transform, "Subtitle")?.GetComponent<TMP_Text>();
        menuCardGlyph =
            FindRecursive(menuCard.transform, "IllustrationGlyph")
                ?.GetComponent<TMP_Text>();

        Button button = menuCard.GetComponent<Button>();
        if (button == null)
        {
            button = menuCard.AddComponent<Button>();
            button.targetGraphic = menuCard.GetComponent<Image>();
        }

        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(OpenCareer);

        RefreshCardLocalization();
        ApplyResolvedFonts();
    }

    private void RefreshCardLocalization()
    {
        if (menuCardTitle != null)
        {
            DisableLocalizedBinding(menuCardTitle);
            menuCardTitle.text = T("progression.menu.title");
        }

        if (menuCardSubtitle != null)
        {
            DisableLocalizedBinding(menuCardSubtitle);
            menuCardSubtitle.text = T("progression.menu.subtitle");
            menuCardSubtitle.rectTransform.anchoredPosition =
                new Vector2(
                    menuCardSubtitle.rectTransform.anchoredPosition.x,
                    -169f);
        }

        if (menuCardGlyph != null)
        {
            DisableLocalizedBinding(menuCardGlyph);
            menuCardGlyph.text = "XP";
            menuCardGlyph.fontSize = 42f;
        }
    }

    private async void OpenCareer()
    {
        root.SetActive(true);
        detailsRoot.SetActive(false);
        currentPage = ProgressionPage.Overview;
        RefreshUiLocalization();
        RebuildNavigation();
        RebuildCurrentPage();
        ApplyResolvedFonts();
        await RefreshProfileAsync(true);
    }

    private void CloseCareer()
    {
        detailsRoot.SetActive(false);
        root.SetActive(false);
    }

    private async System.Threading.Tasks.Task RefreshProfileAsync(
        bool rebuildAfter)
    {
        if (loading || bridge == null)
        {
            return;
        }

        loading = true;
        try
        {
            AtlasBoardProgressionSnapshot result =
                await bridge.GetProfileAsync();
            if (result != null && result.Success)
            {
                snapshot = result;
                SyncSharedProfileLevel();
                if (rebuildAfter && root != null && root.activeSelf)
                {
                    RebuildCurrentPage();
                }
            }
        }
        finally
        {
            loading = false;
            nextProfileRefreshAt = Time.unscaledTime + 10f;
        }
    }

    private void ChangePage(ProgressionPage page)
    {
        currentPage = page;
        RebuildNavigation();
        RebuildCurrentPage();
    }

    private void RefreshUiLocalization()
    {
        if (titleText != null)
        {
            titleText.text = T("progression.title");
        }

        if (detailsOkText != null)
        {
            detailsOkText.text = T("progression.common.close");
        }

        RebuildNavigation();
    }

    private void RebuildNavigation()
    {
        Transform nav = FindDirectChild(root.transform, "Navigation");
        if (nav == null)
        {
            return;
        }

        ClearChildren(nav);
        ProgressionPage[] pages =
        {
            ProgressionPage.Overview,
            ProgressionPage.History,
            ProgressionPage.Achievements
        };
        string[] keys =
        {
            "progression.tab.overview",
            "progression.tab.history",
            "progression.tab.achievements"
        };

        for (int index = 0; index < pages.Length; index++)
        {
            ProgressionPage captured = pages[index];
            Button button = CreateButton(
                nav,
                T(keys[index]),
                pages[index] == currentPage ? DarkBlue : Blue,
                new Vector2(-260f + index * 260f, 0f),
                new Vector2(235f, 50f));
            button.onClick.AddListener(() => ChangePage(captured));
        }
    }

    private void RebuildCurrentPage()
    {
        if (contentRoot == null)
        {
            return;
        }

        ClearChildren(contentRoot);
        switch (currentPage)
        {
            case ProgressionPage.History:
                BuildHistoryPage();
                break;
            case ProgressionPage.Achievements:
                BuildAchievementsPage();
                break;
            default:
                BuildOverviewPage();
                break;
        }

        ApplyResolvedFonts();
    }

    private void BuildOverviewPage()
    {
        CreateSectionTitle(contentRoot, T("progression.overview.title"));

        if (snapshot == null)
        {
            AddCenteredMessage(contentRoot, T("progression.loading"));
            return;
        }

        BuildLevelCard();
        BuildMapFilterBar();

        AtlasBoardCareerStats displayedStats = GetDisplayedStats();
        BuildCareerStatsCard(displayedStats);
        BuildGameplayStatsCard(displayedStats);
        BuildMapStatsCard(displayedStats);
    }

    private void BuildLevelCard()
    {
        GameObject card = CreateCard(
            contentRoot,
            "LevelCard",
            new Vector2(0f, 155f),
            new Vector2(1110f, 170f),
            Cream);
        AddCardHeader(
            card.transform,
            T("progression.level.account_progress"),
            Orange,
            56f);

        TMP_Text level = CreateText(
            card.transform,
            31f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            TextDark);
        SetAnchored(
            level.rectTransform,
            new Vector2(-390f, -24f),
            new Vector2(250f, 60f));
        level.text = T("progression.level.value", snapshot.Level);

        TMP_Text xp = CreateText(
            card.transform,
            16f,
            FontStyles.Normal,
            TextAlignmentOptions.Center,
            TextDark);
        SetAnchored(
            xp.rectTransform,
            new Vector2(0f, -15f),
            new Vector2(460f, 34f));
        long levelProgressXp =
            Mathf.Max(
                0,
                snapshot.TotalXp - snapshot.LevelStartXp);
        long levelRequirementXp =
            Mathf.Max(
                1,
                snapshot.NextLevelXp - snapshot.LevelStartXp);

        xp.text = T(
            "progression.level.xp",
            levelProgressXp,
            levelRequirementXp);

        GameObject barBackground = CreatePanel(
            card.transform,
            "XpBarBackground",
            new Vector2(0f, -48f),
            new Vector2(460f, 22f),
            new Color32(218, 220, 217, 255));
        float span = Mathf.Max(
            1f,
            snapshot.NextLevelXp - snapshot.LevelStartXp);
        float progress = Mathf.Clamp01(
            (snapshot.TotalXp - snapshot.LevelStartXp) / span);

        GameObject fill = CreatePanel(
            barBackground.transform,
            "XpBarFill",
            Vector2.zero,
            new Vector2(460f * progress, 22f),
            Green);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(0f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;

        TMP_Text matches = CreateText(
            card.transform,
            16f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            TextDark);
        SetAnchored(
            matches.rectTransform,
            new Vector2(390f, -24f),
            new Vector2(250f, 60f));
        matches.text = T(
            "progression.level.matches",
            snapshot.Stats.matchesPlayed);
    }

    private void BuildMapFilterBar()
    {
        CreatePanel(
            contentRoot,
            "MapFilterDivider",
            new Vector2(0f, 62f),
            new Vector2(1080f, 2f),
            new Color32(203, 210, 214, 210));

        TMP_Text label = CreateText(
            contentRoot,
            13f,
            FontStyles.Bold,
            TextAlignmentOptions.Left,
            Muted);
        SetAnchored(
            label.rectTransform,
            new Vector2(-430f, 35f),
            new Vector2(170f, 32f));
        label.text = T("progression.filter.map");

        TMP_Dropdown dropdown = CreateMapFilterDropdown();
        if (dropdown == null)
        {
            return;
        }

        List<string> mapIds = BuildSelectableMapIds();
        List<string> labels = new List<string>
        {
            T("progression.map.all")
        };
        labels.AddRange(mapIds.Select(LocalizeMap));

        dropdown.ClearOptions();
        dropdown.AddOptions(labels);
        int selectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(selectedMapId))
        {
            int mapIndex = mapIds.FindIndex(item =>
                MapIdsEqual(item, selectedMapId));
            if (mapIndex >= 0)
            {
                selectedIndex = mapIndex + 1;
            }
            else
            {
                selectedMapId = string.Empty;
            }
        }

        dropdown.SetValueWithoutNotify(selectedIndex);
        dropdown.RefreshShownValue();
        dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        dropdown.onValueChanged.AddListener(index =>
        {
            selectedMapId = index <= 0 || index > mapIds.Count
                ? string.Empty
                : mapIds[index - 1];
            RebuildCurrentPage();
        });
    }

    private TMP_Dropdown CreateMapFilterDropdown()
    {
        TMP_Dropdown source = FindSceneMapDropdown();
        if (source == null)
        {
            return null;
        }

        GameObject clone = Instantiate(
            source.gameObject,
            contentRoot,
            false);
        clone.name = "ProgressionMapFilter";
        clone.SetActive(true);

        RectTransform rect = clone.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-235f, 35f);
        rect.sizeDelta = new Vector2(300f, 38f);
        rect.localScale = Vector3.one;

        foreach (AtlasBoardLocalizedDropdown binding in
                 clone.GetComponents<AtlasBoardLocalizedDropdown>())
        {
            if (binding != null)
            {
                binding.enabled = false;
            }
        }

        foreach (AtlasBoardLocalizedText binding in
                 clone.GetComponentsInChildren<AtlasBoardLocalizedText>(true))
        {
            if (binding != null)
            {
                binding.enabled = false;
            }
        }

        TMP_Dropdown dropdown = clone.GetComponent<TMP_Dropdown>();
        if (dropdown != null && dropdown.captionText != null)
        {
            dropdown.captionText.fontSize = 14f;
            dropdown.captionText.enableAutoSizing = true;
            dropdown.captionText.fontSizeMin = 10f;
            dropdown.captionText.fontSizeMax = 14f;
        }

        return dropdown;
    }

    private List<string> BuildSelectableMapIds()
    {
        List<string> mapIds = new List<string>
        {
            "Turkey",
            "Colorado",
            "USA"
        };

        if (snapshot != null && snapshot.MapStats != null)
        {
            foreach (AtlasBoardMapCareerStats entry in snapshot.MapStats)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.mapId))
                {
                    continue;
                }

                if (!mapIds.Any(item => MapIdsEqual(item, entry.mapId)))
                {
                    mapIds.Add(entry.mapId);
                }
            }
        }

        return mapIds;
    }

    private AtlasBoardCareerStats GetDisplayedStats()
    {
        if (snapshot == null)
        {
            return new AtlasBoardCareerStats();
        }

        if (string.IsNullOrWhiteSpace(selectedMapId))
        {
            return snapshot.Stats ?? new AtlasBoardCareerStats();
        }

        AtlasBoardMapCareerStats selected =
            snapshot.MapStats != null
                ? snapshot.MapStats.FirstOrDefault(entry =>
                    entry != null &&
                    MapIdsEqual(entry.mapId, selectedMapId))
                : null;

        return selected != null && selected.stats != null
            ? selected.stats
            : new AtlasBoardCareerStats();
    }

    private string CurrentMapFilterLabel()
    {
        return string.IsNullOrWhiteSpace(selectedMapId)
            ? T("progression.map.all")
            : LocalizeMap(selectedMapId);
    }

    private static TMP_Dropdown FindSceneMapDropdown()
    {
        TMP_Dropdown[] all = Resources.FindObjectsOfTypeAll<TMP_Dropdown>();
        foreach (TMP_Dropdown dropdown in all)
        {
            if (dropdown == null || !dropdown.gameObject.scene.IsValid())
            {
                continue;
            }

            string name = dropdown.gameObject.name ?? string.Empty;
            if (name.IndexOf("map", StringComparison.OrdinalIgnoreCase) >= 0 &&
                name != "ProgressionMapFilter")
            {
                return dropdown;
            }
        }

        return all.FirstOrDefault(item =>
            item != null &&
            item.gameObject.scene.IsValid() &&
            item.gameObject.name != "ProgressionMapFilter");
    }

    private static bool MapIdsEqual(string left, string right)
    {
        return string.Equals(
            NormalizeMapId(left),
            NormalizeMapId(right),
            StringComparison.Ordinal);
    }

    private static string NormalizeMapId(string value)
    {
        return (value ?? string.Empty)
            .Trim()
            .Replace(" ", "_")
            .ToLowerInvariant();
    }

    private void BuildCareerStatsCard(
        AtlasBoardCareerStats stats)
    {
        GameObject card = CreateCard(
            contentRoot,
            "CareerStats",
            new Vector2(-365f, -165f),
            new Vector2(350f, 330f),
            Cream);
        AddCardHeader(card.transform, T("progression.stats.career"), Green, 136f);

        if (stats == null)
        {
            stats = new AtlasBoardCareerStats();
        }
        AddStatLine(card.transform, 80f, T("progression.stats.matches"), stats.matchesPlayed);
        AddStatLine(card.transform, 38f, T("progression.stats.wins"), stats.wins);
        AddStatLine(card.transform, -4f, T("progression.stats.losses"), stats.losses);
        AddStatLine(card.transform, -46f, T("progression.stats.ties"), stats.ties);
        AddStatLine(
            card.transform,
            -88f,
            T("progression.stats.win_rate"),
            WinRateText(stats));
        AddStatLine(
            card.transform,
            -130f,
            T("progression.stats.best_streak"),
            stats.bestWinStreak);
    }

    private void BuildGameplayStatsCard(
        AtlasBoardCareerStats stats)
    {
        GameObject card = CreateCard(
            contentRoot,
            "GameplayStats",
            new Vector2(0f, -165f),
            new Vector2(350f, 330f),
            Cream);
        AddCardHeader(card.transform, T("progression.stats.gameplay"), Blue, 136f);

        if (stats == null)
        {
            stats = new AtlasBoardCareerStats();
        }
        AddStatLine(card.transform, 80f, T("progression.stats.dice_rolls"), stats.diceRolls);
        AddStatLine(card.transform, 38f, T("progression.stats.doubles"), stats.doublesRolled);
        AddStatLine(
            card.transform,
            -4f,
            T("progression.stats.avg_roll"),
            stats.diceRolls > 0
                ? ((float)stats.totalDiceValue / stats.diceRolls)
                    .ToString("0.00", CultureInfo.InvariantCulture)
                : "0.00");
        AddStatLine(card.transform, -46f, T("progression.stats.high_roll"), stats.highestRoll);
        AddStatLine(
            card.transform,
            -88f,
            T("progression.stats.properties"),
            stats.propertiesAtFinishTotal);
        AddStatLine(
            card.transform,
            -130f,
            T("progression.stats.development"),
            stats.developmentLevelsAtFinishTotal);
    }

    private void BuildMapStatsCard(
        AtlasBoardCareerStats stats)
    {
        GameObject card = CreateCard(
            contentRoot,
            "MapStats",
            new Vector2(365f, -165f),
            new Vector2(350f, 330f),
            Cream);
        AddCardHeader(card.transform, T("progression.stats.records"), Orange, 136f);

        if (stats == null)
        {
            stats = new AtlasBoardCareerStats();
        }
        AddStatLine(card.transform, 80f, T("progression.stats.networth"), stats.highestNetWorth);
        AddStatLine(card.transform, 38f, T("progression.stats.cash"), stats.highestCash);
        AddStatLine(card.transform, -4f, T("progression.stats.rounds"), stats.totalRounds);
        AddStatLine(card.transform, -46f, T("progression.stats.turns"), stats.totalTurns);
        AddStatLine(
            card.transform,
            -88f,
            T("progression.stats.maps"),
            T(
                "progression.stats.map_value",
                CurrentMapFilterLabel(),
                stats.matchesPlayed));
        AddStatLine(
            card.transform,
            -130f,
            T("progression.stats.bankruptcies"),
            stats.bankruptcies);
    }

    private void BuildHistoryPage()
    {
        CreateSectionTitle(contentRoot, T("progression.history.title"));

        TMP_Text note = CreateText(
            contentRoot,
            13f,
            FontStyles.Normal,
            TextAlignmentOptions.Center,
            Muted);
        SetAnchored(
            note.rectTransform,
            new Vector2(0f, 205f),
            new Vector2(1050f, 44f));
        note.text = T(
            "progression.history.retention",
            snapshot != null ? snapshot.HistoryLimit : 10,
            snapshot != null ? snapshot.HistoryRetentionHours : 72);

        RectTransform list = BuildScrollList(
            contentRoot,
            "MatchHistoryList",
            new Vector2(0f, -45f),
            new Vector2(1120f, 455f),
            PanelBlue);

        if (snapshot == null || snapshot.RecentMatches.Count == 0)
        {
            AddInfoRow(list, T("progression.history.empty"));
            return;
        }

        foreach (AtlasBoardProgressionMatchSummary match in snapshot.RecentMatches)
        {
            AddHistoryRow(list, match);
        }
    }

    private void AddHistoryRow(
        Transform parent,
        AtlasBoardProgressionMatchSummary match)
    {
        GameObject row = CreateListRow(parent, 116f, Cream);
        Color accent = ResultColor(match.result);
        CreatePanel(
            row.transform,
            "ResultAccent",
            new Vector2(-526f, 0f),
            new Vector2(8f, 100f),
            accent);

        TMP_Text header = CreateText(
            row.transform,
            16f,
            FontStyles.Bold,
            TextAlignmentOptions.TopLeft,
            TextDark);
        SetAnchored(
            header.rectTransform,
            new Vector2(-262f, 30f),
            new Vector2(490f, 30f));
        header.text =
            ResultLabel(match.result) +
            "  •  " + HostLobbyLabel(match) +
            "  •  " + FormatDate(match.finishedAtEpochMs);

        TMP_Text body = CreateText(
            row.transform,
            13f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft,
            TextDark);
        SetAnchored(
            body.rectTransform,
            new Vector2(-255f, -14f),
            new Vector2(510f, 52f));
        body.text = T(
            "progression.history.row",
            LocalizeMap(match.mapId),
            match.place,
            match.playerCount,
            match.netWorth,
            match.xpAwarded);

        Button details = CreateButton(
            row.transform,
            T("progression.history.details"),
            Blue,
            new Vector2(415f, 0f),
            new Vector2(170f, 48f));
        AtlasBoardProgressionMatchSummary captured = match;
        details.onClick.AddListener(() => ShowMatchDetails(captured));
    }

    private void ShowMatchDetails(
        AtlasBoardProgressionMatchSummary match)
    {
        if (match == null ||
            detailsRoot == null ||
            detailsTitleText == null ||
            detailsBodyText == null ||
            detailsPlayersRoot == null)
        {
            return;
        }

        detailsTitleText.text =
            T("progression.details.title");

        List<string> summaryLines =
            new List<string>
            {
                $"<b>{ResultLabel(match.result)}</b>  •  " +
                FormatDate(match.finishedAtEpochMs),
                T(
                    "progression.details.match_id",
                    ShortId(match.matchId)) +
                "    •    " +
                T(
                    "progression.details.lobby",
                    HostLobbyLabel(match)),
                T(
                    "progression.details.rules",
                    LocalizeMap(match.mapId),
                    LocalizeTheme(match.themeId),
                    match.roundLimit),
                T(
                    "progression.details.place",
                    match.place,
                    match.playerCount) +
                "    •    " +
                T(
                    "progression.details.xp",
                    match.xpAwarded,
                    match.levelAfter),
                T(
                    "progression.details.economy",
                    match.finalCash,
                    match.propertyCount,
                    match.developmentLevels,
                    match.netWorth),
                T(
                    "progression.details.dice",
                    match.diceRolls,
                    match.doublesRolled,
                    AverageRoll(match),
                    match.highestRoll)
            };

        detailsBodyText.text =
            string.Join("\n", summaryLines);

        RebuildDetailsPlayerCards(match);

        if (detailsOkText != null)
        {
            detailsOkText.text =
                T("progression.common.close");
        }

        detailsRoot.SetActive(true);
        detailsRoot.transform.SetAsLastSibling();
        ApplyResolvedFonts();
    }

    private void RebuildDetailsPlayerCards(
        AtlasBoardProgressionMatchSummary match)
    {
        ClearDetailsPlayerCards();

        if (match == null ||
            match.participants == null ||
            detailsPlayersRoot == null)
        {
            return;
        }

        List<AtlasBoardProgressionParticipant> participants =
            match.participants
                .Where(item => item != null)
                .OrderBy(item => item.place)
                .Take(4)
                .ToList();

        for (int index = 0;
             index < participants.Count;
             index++)
        {
            AddDetailsPlayerCard(
                participants[index],
                index,
                participants.Count);
        }
    }

    private void ClearDetailsPlayerCards()
    {
        if (detailsPlayersRoot == null)
        {
            return;
        }

        for (int index =
                 detailsPlayersRoot.childCount - 1;
             index >= 0;
             index--)
        {
            Transform child =
                detailsPlayersRoot.GetChild(index);

            if (child == null)
            {
                continue;
            }

            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private void AddDetailsPlayerCard(
        AtlasBoardProgressionParticipant participant,
        int index,
        int count)
    {
        if (participant == null ||
            detailsPlayersRoot == null)
        {
            return;
        }

        Vector2 position =
            DetailsPlayerCardPosition(
                index,
                count);

        GameObject card =
            CreateCard(
                detailsPlayersRoot,
                "MatchPlayerCard",
                position,
                new Vector2(440f, 136f),
                Cream);

        Color accent =
            ResultColor(
                participant.result);

        CreatePanel(
            card.transform,
            "ResultAccent",
            new Vector2(-214f, 0f),
            new Vector2(7f, 122f),
            accent);

        GameObject avatar =
            CreatePanel(
                card.transform,
                "PlayerBubble",
                new Vector2(-178f, 28f),
                new Vector2(48f, 48f),
                accent);

        TMP_Text avatarText =
            CreateText(
                avatar.transform,
                24f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                Color.white);
        Stretch(
            avatarText.rectTransform,
            4f);
        avatarText.text =
            PlayerInitial(
                participant.displayName);

        TMP_Text name =
            CreateText(
                card.transform,
                15f,
                FontStyles.Bold,
                TextAlignmentOptions.Left,
                TextDark);
        SetAnchored(
            name.rectTransform,
            new Vector2(-55f, 32f),
            new Vector2(190f, 30f));
        name.text =
            $"#{participant.place} " +
            (participant.displayName ?? string.Empty);

        TMP_Text result =
            CreateText(
                card.transform,
                14f,
                FontStyles.Bold,
                TextAlignmentOptions.Right,
                accent);
        SetAnchored(
            result.rectTransform,
            new Vector2(158f, 32f),
            new Vector2(92f, 30f));
        result.text =
            ResultLabel(
                participant.result);

        TMP_Text economy =
            CreateText(
                card.transform,
                11f,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft,
                TextDark);
        SetAnchored(
            economy.rectTransform,
            new Vector2(20f, -34f),
            new Vector2(350f, 52f));
        economy.enableAutoSizing = true;
        economy.fontSizeMin = 8f;
        economy.fontSizeMax = 11f;
        economy.text =
            T(
                "progression.details.economy",
                participant.cash,
                participant.propertyCount,
                participant.developmentLevels,
                participant.netWorth);
    }

    private static Vector2 DetailsPlayerCardPosition(
        int index,
        int count)
    {
        if (count <= 1)
        {
            return Vector2.zero;
        }

        if (count == 2)
        {
            return new Vector2(
                index == 0 ? -230f : 230f,
                0f);
        }

        if (count == 3)
        {
            if (index < 2)
            {
                return new Vector2(
                    index == 0 ? -230f : 230f,
                    72f);
            }

            return new Vector2(
                0f,
                -72f);
        }

        return new Vector2(
            index % 2 == 0 ? -230f : 230f,
            index < 2 ? 72f : -72f);
    }

    private static string PlayerInitial(
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(
                displayName))
        {
            return "?";
        }

        return displayName
            .Trim()
            .Substring(0, 1)
            .ToUpperInvariant();
    }

    private void BuildAchievementsPage()
    {
        CreateSectionTitle(contentRoot, T("progression.achievements.title"));

        TMP_Text note = CreateText(
            contentRoot,
            13f,
            FontStyles.Normal,
            TextAlignmentOptions.Center,
            Muted);
        SetAnchored(
            note.rectTransform,
            new Vector2(0f, 205f),
            new Vector2(1040f, 42f));
        note.text = T("progression.achievements.note");

        RectTransform list = BuildScrollList(
            contentRoot,
            "AchievementList",
            new Vector2(0f, -45f),
            new Vector2(1120f, 455f),
            PanelBlue);

        if (snapshot == null || snapshot.Achievements.Count == 0)
        {
            AddInfoRow(list, T("progression.achievements.empty"));
            return;
        }

        foreach (AtlasBoardProgressionAchievement achievement in
                 snapshot.Achievements
                     .OrderByDescending(item => item.unlocked)
                     .ThenBy(item => item.achievementId))
        {
            AddAchievementRow(list, achievement);
        }
    }

    private void AddAchievementRow(
        Transform parent,
        AtlasBoardProgressionAchievement achievement)
    {
        GameObject row = CreateListRow(parent, 104f, Cream);
        Color accent = achievement.unlocked ? Green : Blue;
        CreatePanel(
            row.transform,
            "AchievementAccent",
            new Vector2(-526f, 0f),
            new Vector2(8f, 90f),
            accent);

        TMP_Text title = CreateText(
            row.transform,
            16f,
            FontStyles.Bold,
            TextAlignmentOptions.TopLeft,
            TextDark);
        SetAnchored(
            title.rectTransform,
            new Vector2(-300f, 25f),
            new Vector2(430f, 28f));
        title.text = T(achievement.titleKey);

        TMP_Text body = CreateText(
            row.transform,
            12f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft,
            Muted);
        SetAnchored(
            body.rectTransform,
            new Vector2(-285f, -12f),
            new Vector2(460f, 42f));
        body.text = T(achievement.descriptionKey);

        TMP_Text progress = CreateText(
            row.transform,
            15f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            achievement.unlocked ? Green : TextDark);
        SetAnchored(
            progress.rectTransform,
            new Vector2(380f, 15f),
            new Vector2(230f, 32f));
        progress.text = achievement.unlocked
            ? T("progression.achievements.unlocked")
            : T(
                "progression.achievements.progress",
                achievement.progress,
                achievement.target);

        GameObject progressBack = CreatePanel(
            row.transform,
            "AchievementProgressBack",
            new Vector2(380f, -23f),
            new Vector2(220f, 16f),
            new Color32(218, 220, 217, 255));
        float ratio = achievement.unlocked
            ? 1f
            : achievement.target > 0
                ? Mathf.Clamp01(
                    (float)achievement.progress /
                    achievement.target)
                : 0f;
        GameObject fill = CreatePanel(
            progressBack.transform,
            "AchievementProgressFill",
            Vector2.zero,
            new Vector2(220f * ratio, 16f),
            accent);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0.5f);
        fillRect.anchorMax = new Vector2(0f, 0.5f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
    }

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject(
            "Canvas_AtlasProgression",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 570;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root = CreatePanel(
            canvasObject.transform,
            "AtlasCareer",
            Vector2.zero,
            new Vector2(1920f, 1080f),
            BackgroundBlue);
        Stretch(root.GetComponent<RectTransform>());
        root.AddComponent<AtlasBoardEscapeBlocker>();

        GameObject ribbon = CreatePanel(
            root.transform,
            "CareerRibbon",
            new Vector2(0f, 468f),
            new Vector2(780f, 96f),
            Orange);
        Outline outline = ribbon.AddComponent<Outline>();
        outline.effectColor = new Color32(169, 112, 0, 255);
        outline.effectDistance = new Vector2(4f, -4f);

        titleText = CreateText(
            ribbon.transform,
            36f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white);
        Stretch(titleText.rectTransform, 8f);

        Button close = CreateButton(
            root.transform,
            "X",
            Red,
            new Vector2(850f, 468f),
            new Vector2(82f, 70f));
        close.onClick.AddListener(CloseCareer);

        GameObject nav = CreatePanel(
            root.transform,
            "Navigation",
            new Vector2(0f, 345f),
            new Vector2(850f, 72f),
            new Color32(233, 233, 239, 245));
        RebuildNavigation();

        GameObject content = CreatePanel(
            root.transform,
            "ContentPanel",
            new Vector2(0f, -35f),
            new Vector2(1240f, 680f),
            new Color32(233, 233, 239, 245));
        contentRoot = content.GetComponent<RectTransform>();

        BuildDetailsPopup(root.transform);
        RefreshUiLocalization();
        ApplyResolvedFonts();
    }

    private void BuildDetailsPopup(
        Transform parent)
    {
        detailsRoot =
            CreatePanel(
                parent,
                "MatchDetailsPopup",
                Vector2.zero,
                new Vector2(1080f, 760f),
                Cream);
        detailsRoot.AddComponent<
            AtlasBoardEscapeBlocker>();

        Outline outline =
            detailsRoot.AddComponent<Outline>();
        outline.effectColor = Blue;
        outline.effectDistance =
            new Vector2(3f, -3f);

        detailsTitleText =
            CreateText(
                detailsRoot.transform,
                28f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                TextDark);
        SetAnchored(
            detailsTitleText.rectTransform,
            new Vector2(0f, 330f),
            new Vector2(920f, 52f));

        GameObject summaryCard =
            CreateCard(
                detailsRoot.transform,
                "MatchSummaryCard",
                new Vector2(0f, 195f),
                new Vector2(960f, 190f),
                PanelBlue);

        detailsBodyText =
            CreateText(
                summaryCard.transform,
                13f,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft,
                TextDark);
        SetAnchored(
            detailsBodyText.rectTransform,
            Vector2.zero,
            new Vector2(900f, 150f));
        detailsBodyText.overflowMode =
            TextOverflowModes.Ellipsis;
        detailsBodyText.enableAutoSizing = true;
        detailsBodyText.fontSizeMin = 9f;
        detailsBodyText.fontSizeMax = 13f;

        TMP_Text playersLabel =
            CreateText(
                detailsRoot.transform,
                18f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                TextDark);
        SetAnchored(
            playersLabel.rectTransform,
            new Vector2(0f, 70f),
            new Vector2(880f, 34f));
        playersLabel.text =
            T("progression.details.players");

        GameObject playersPanel =
            CreatePanel(
                detailsRoot.transform,
                "PlayersPanel",
                new Vector2(0f, -95f),
                new Vector2(960f, 300f),
                PanelBlue);
        detailsPlayersRoot =
            playersPanel.GetComponent<RectTransform>();

        Button close =
            CreateButton(
                detailsRoot.transform,
                T("progression.common.close"),
                Blue,
                new Vector2(0f, -332f),
                new Vector2(250f, 58f));
        detailsOkText =
            close.GetComponentInChildren<
                TMP_Text>(true);
        close.onClick.AddListener(
            () => detailsRoot.SetActive(false));
    }

    private void SyncSharedProfileLevel()
    {
        if (snapshot == null || snapshot.Level <= 0)
        {
            return;
        }

        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (TMP_Text text in texts)
        {
            if (text == null ||
                !text.gameObject.scene.IsValid() ||
                text.gameObject.name != "ProfileName")
            {
                continue;
            }

            string current = text.text ?? string.Empty;
            int suffixIndex = current.LastIndexOf(" • Lv. ", StringComparison.Ordinal);
            if (suffixIndex >= 0)
            {
                current = current.Substring(0, suffixIndex);
            }

            text.text = current + $" • Lv. {snapshot.Level}";
        }
    }

    private void ApplyResolvedFonts()
    {
        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;
        if (localization == null || canvas == null)
        {
            return;
        }

        TMP_Text[] texts = canvas.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text == null)
            {
                continue;
            }

            TMP_FontAsset resolved = localization.ResolveFont(text.font);
            if (resolved != null && text.font != resolved)
            {
                text.font = resolved;
            }
        }

        if (menuCard != null)
        {
            foreach (TMP_Text text in menuCard.GetComponentsInChildren<TMP_Text>(true))
            {
                TMP_FontAsset resolved = localization.ResolveFont(text.font);
                if (resolved != null)
                {
                    text.font = resolved;
                }
            }
        }
    }

    private static void DisableLocalizedBinding(TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        AtlasBoardLocalizedText[] bindings =
            text.GetComponents<AtlasBoardLocalizedText>();
        foreach (AtlasBoardLocalizedText binding in bindings)
        {
            if (binding != null)
            {
                binding.enabled = false;
            }
        }
    }

    private static string HostLobbyLabel(AtlasBoardProgressionMatchSummary match)
    {
        if (match == null || match.participants == null)
        {
            return T("progression.history.lobby_fallback");
        }

        AtlasBoardProgressionParticipant host =
            match.participants.FirstOrDefault(item => item != null && item.isHost);
        return host != null && !string.IsNullOrWhiteSpace(host.displayName)
            ? T("progression.history.host_lobby", host.displayName)
            : T("progression.history.lobby_fallback");
    }

    private static string ResultLabel(string result)
    {
        switch ((result ?? string.Empty).ToLowerInvariant())
        {
            case "win": return T("progression.result.win");
            case "tie": return T("progression.result.tie");
            default: return T("progression.result.loss");
        }
    }

    private static Color ResultColor(string result)
    {
        switch ((result ?? string.Empty).ToLowerInvariant())
        {
            case "win": return Green;
            case "tie": return Orange;
            default: return Red;
        }
    }

    private static string LocalizeMap(string mapId)
    {
        string normalized = (mapId ?? string.Empty).ToLowerInvariant();
        if (normalized.Contains("turkey") || normalized.Contains("türkiye"))
        {
            return T("progression.map.turkey");
        }
        if (normalized.Contains("colorado"))
        {
            return T("progression.map.colorado");
        }
        if (normalized == "usa" || normalized.Contains("united_states"))
        {
            return T("progression.map.usa");
        }
        return string.IsNullOrWhiteSpace(mapId) ? "-" : mapId;
    }

    private static string LocalizeTheme(string themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId))
        {
            return "-";
        }

        string key = "lobby.theme." + themeId.Trim().ToLowerInvariant();
        string localized = T(key);
        return string.Equals(localized, key, StringComparison.Ordinal)
            ? themeId
            : localized;
    }

    private static string WinRateText(AtlasBoardCareerStats stats)
    {
        if (stats == null || stats.matchesPlayed <= 0)
        {
            return "0%";
        }

        return ((float)stats.wins / stats.matchesPlayed * 100f)
            .ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }

    private static string AverageRoll(AtlasBoardProgressionMatchSummary match)
    {
        if (match == null || match.diceRolls <= 0)
        {
            return "0.00";
        }

        return ((float)match.totalDiceValue / match.diceRolls)
            .ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string FormatDate(long epochMs)
    {
        if (epochMs <= 0L)
        {
            return "-";
        }

        try
        {
            return DateTimeOffset
                .FromUnixTimeMilliseconds(epochMs)
                .ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
        catch
        {
            return "-";
        }
    }

    private static string ShortId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return value.Length <= 16 ? value : value.Substring(0, 16) + "…";
    }

    private static Button FindSceneButton(string name)
    {
        Button[] all = Resources.FindObjectsOfTypeAll<Button>();
        foreach (Button button in all)
        {
            if (button != null &&
                button.gameObject.scene.IsValid() &&
                string.Equals(button.gameObject.name, name, StringComparison.Ordinal))
            {
                return button;
            }
        }

        return null;
    }

    private static void AddStatLine(
        Transform parent,
        float y,
        string label,
        object value)
    {
        TMP_Text left = CreateText(
            parent,
            13f,
            FontStyles.Normal,
            TextAlignmentOptions.Left,
            Muted);
        SetAnchored(left.rectTransform, new Vector2(-80f, y), new Vector2(150f, 30f));
        left.text = label;

        TMP_Text right = CreateText(
            parent,
            14f,
            FontStyles.Bold,
            TextAlignmentOptions.Right,
            TextDark);
        SetAnchored(right.rectTransform, new Vector2(85f, y), new Vector2(135f, 30f));
        right.text = Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static GameObject CreatePanel(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject panel = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private static GameObject CreateCard(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject card = CreatePanel(parent, name, position, size, color);
        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = new Color32(210, 210, 215, 255);
        outline.effectDistance = new Vector2(2f, -2f);
        return card;
    }

    private static void AddCardHeader(
        Transform parent,
        string value,
        Color accent,
        float y)
    {
        RectTransform parentRect = parent as RectTransform;
        float width = parentRect != null && parentRect.rect.width > 0f
            ? Mathf.Max(180f, parentRect.rect.width - 24f)
            : 320f;
        GameObject bar = CreatePanel(
            parent,
            "CardHeader",
            new Vector2(0f, y),
            new Vector2(width, 52f),
            accent);
        TMP_Text text = CreateText(
            bar.transform,
            18f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white);
        Stretch(text.rectTransform, 7f);
        text.text = value;
    }

    private static void CreateSectionTitle(Transform parent, string value)
    {
        TMP_Text title = CreateText(
            parent,
            27f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            TextDark);
        SetAnchored(
            title.rectTransform,
            new Vector2(0f, 275f),
            new Vector2(900f, 52f));
        title.text = value;
    }

    private static void AddCenteredMessage(Transform parent, string value)
    {
        TMP_Text text = CreateText(
            parent,
            18f,
            FontStyles.Italic,
            TextAlignmentOptions.Center,
            TextDark);
        SetAnchored(
            text.rectTransform,
            Vector2.zero,
            new Vector2(900f, 100f));
        text.text = value;
    }

    private static Button CreateButton(
        Transform parent,
        string label,
        Color color,
        Vector2 position,
        Vector2 size)
    {
        GameObject buttonObject = new GameObject(
            "Button_" + label,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        buttonObject.GetComponent<Image>().color = color;

        TMP_Text text = CreateText(
            buttonObject.transform,
            14f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Color.white);
        Stretch(text.rectTransform, 5f);
        text.text = label;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = 16f;
        return buttonObject.GetComponent<Button>();
    }

    private static RectTransform BuildScrollList(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(ScrollRect));
        root.transform.SetParent(parent, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        SetAnchored(rootRect, position, size);
        root.GetComponent<Image>().color = color;

        GameObject viewport = new GameObject(
            "Viewport",
            typeof(RectTransform),
            typeof(Image),
            typeof(RectMask2D));
        viewport.transform.SetParent(root.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewport.GetComponent<Image>().color = Color.clear;

        GameObject content = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(8f, 0f);
        contentRect.offsetMax = new Vector2(-8f, 0f);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 7f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = root.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        return contentRect;
    }

    private static GameObject CreateListRow(
        Transform parent,
        float height,
        Color color)
    {
        GameObject row = new GameObject(
            "ProgressionRow",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = color;
        LayoutElement layout = row.GetComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        return row;
    }

    private static void AddInfoRow(Transform parent, string value)
    {
        GameObject row = CreateListRow(parent, 74f, Cream);
        TMP_Text text = CreateText(
            row.transform,
            14f,
            FontStyles.Italic,
            TextAlignmentOptions.Center,
            TextDark);
        Stretch(text.rectTransform, 8f);
        text.text = value;
    }

    private static TMP_Text CreateText(
        Transform parent,
        float size,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        AtlasBoardLocalizationManager localization =
            AtlasBoardLocalizationManager.Instance;
        if (localization != null)
        {
            text.font = localization.ResolveFont(text.font);
        }
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static void SetAnchored(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    private static Transform FindRecursive(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == name)
        {
            return parent;
        }

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform found = FindRecursive(parent.GetChild(index), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            if (child != null && child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            Destroy(parent.GetChild(index).gameObject);
        }
    }

    private static string T(string key, params object[] args)
    {
        return AtlasBoardL.T(key, args);
    }
}
