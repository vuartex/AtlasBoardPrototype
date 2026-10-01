using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(13000)]
[DisallowMultipleComponent]
public sealed class AtlasBoardMatchResultFinalLayout : MonoBehaviour
{
    private const float RefreshIntervalSeconds = 0.20f;

    private MatchResultManager resultManager;
    private TabletUIManager tabletManager;
    private TurnManager turnManager;

    private GameObject resultPanel;
    private TMP_Text legacySummaryText;
    private RectTransform tabletRoot;

    private RectTransform cardsRoot;

    private readonly RectTransform[] cards =
        new RectTransform[4];

    private readonly Image[] backgrounds =
        new Image[4];

    private readonly Image[] accents =
        new Image[4];

    private readonly Image[] iconBackgrounds =
        new Image[4];

    private readonly TMP_Text[] iconTexts =
        new TMP_Text[4];

    private readonly TMP_Text[] nameTexts =
        new TMP_Text[4];

    private readonly TMP_Text[] stateTexts =
        new TMP_Text[4];

    private readonly TMP_Text[] worthTexts =
        new TMP_Text[4];

    private readonly TMP_Text[] detailTexts =
        new TMP_Text[4];

    private bool summaryStateCaptured;
    private bool summaryWasActive;
    private float nextRefreshAt;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (UnityEngine.Object.FindAnyObjectByType<
                BoardPath>() == null)
        {
            return;
        }

        if (UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardMatchResultFinalLayout>() != null)
        {
            return;
        }

        new GameObject(
                "AtlasBoard_Phase14F_FinalMatchResult")
            .AddComponent<
                AtlasBoardMatchResultFinalLayout>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;
    }

    private void OnDisable()
    {
        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;

        RestoreLegacySummary();
        HideCards();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        bool resultVisible =
            resultPanel != null &&
            resultPanel.activeInHierarchy &&
            tabletRoot != null &&
            tabletRoot.gameObject.activeInHierarchy;

        if (!resultVisible)
        {
            RestoreLegacySummary();
            HideCards();
            return;
        }

        EnsureCards();
        ApplyCardGeometry();

        if (cardsRoot != null)
        {
            cardsRoot.SetAsLastSibling();

            if (!cardsRoot.gameObject.activeSelf)
            {
                cardsRoot.gameObject.SetActive(
                    true);
            }
        }

        HideLegacySummary();

        if (Time.unscaledTime >=
            nextRefreshAt)
        {
            nextRefreshAt =
                Time.unscaledTime +
                RefreshIntervalSeconds;

            RefreshCards();
        }
    }

    private void HandleLanguageChanged()
    {
        nextRefreshAt = 0f;

        if (cardsRoot != null)
        {
            RefreshCards();
        }
    }

    public void RefreshNow()
    {
        ResolveReferences();
        EnsureCards();
        ApplyCardGeometry();
        HideLegacySummary();
        RefreshCards();
    }

    public RectTransform TabletRoot =>
        tabletRoot;

    public RectTransform CardsRoot =>
        cardsRoot;

    public RectTransform GetCard(
        int slotIndex)
    {
        return slotIndex >= 0 &&
               slotIndex < cards.Length
            ? cards[slotIndex]
            : null;
    }

    private void ResolveReferences()
    {
        if (resultManager == null)
        {
            resultManager =
                UnityEngine.Object.FindAnyObjectByType<
                    MatchResultManager>();
        }

        if (tabletManager == null)
        {
            tabletManager =
                UnityEngine.Object.FindAnyObjectByType<
                    TabletUIManager>();
        }

        if (turnManager == null)
        {
            turnManager =
                UnityEngine.Object.FindAnyObjectByType<
                    TurnManager>();
        }

        if (resultManager != null)
        {
            resultPanel =
                ReadPrivateField<GameObject>(
                    resultManager,
                    "resultPanel");

            legacySummaryText =
                ReadPrivateField<TMP_Text>(
                    resultManager,
                    "resultSummaryText");
        }

        if (tabletManager != null)
        {
            GameObject root =
                ReadPrivateField<GameObject>(
                    tabletManager,
                    "tabletRoot");

            tabletRoot =
                root != null
                    ? root.transform as
                        RectTransform
                    : null;
        }
    }

    private void EnsureCards()
    {
        if (tabletRoot == null ||
            cardsRoot != null)
        {
            return;
        }

        Transform existing =
            tabletRoot.Find(
                "Phase14F_Final_PlayerCards");

        if (existing != null)
        {
            cardsRoot =
                existing as RectTransform;

            ResolveExistingCards();
            return;
        }

        GameObject rootObject =
            new GameObject(
                "Phase14F_Final_PlayerCards",
                typeof(RectTransform),
                typeof(LayoutElement));

        rootObject.transform.SetParent(
            tabletRoot,
            false);

        cardsRoot =
            rootObject.GetComponent<
                RectTransform>();

        LayoutElement rootLayout =
            rootObject.GetComponent<
                LayoutElement>();

        rootLayout.ignoreLayout =
            true;

        cardsRoot.anchorMin =
            Vector2.zero;

        cardsRoot.anchorMax =
            Vector2.one;

        cardsRoot.offsetMin =
            Vector2.zero;

        cardsRoot.offsetMax =
            Vector2.zero;

        for (int slot = 0;
             slot < 4;
             slot++)
        {
            CreatePlayerCard(
                slot);
        }

        cardsRoot.gameObject.SetActive(
            false);
    }

    private void CreatePlayerCard(
        int slot)
    {
        bool rightSide =
            slot % 2 == 1;

        GameObject cardObject =
            new GameObject(
                $"PlayerCard_{slot + 1}",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline),
                typeof(Shadow),
                typeof(LayoutElement));

        cardObject.transform.SetParent(
            cardsRoot,
            false);

        LayoutElement layout =
            cardObject.GetComponent<
                LayoutElement>();

        layout.ignoreLayout =
            true;

        RectTransform card =
            cardObject.GetComponent<
                RectTransform>();

        Image background =
            cardObject.GetComponent<
                Image>();

        background.color =
            new Color(
                0.070f,
                0.095f,
                0.135f,
                0.985f);

        background.raycastTarget =
            false;

        Outline outline =
            cardObject.GetComponent<
                Outline>();

        outline.effectColor =
            new Color(
                0.20f,
                0.29f,
                0.40f,
                0.92f);

        outline.effectDistance =
            new Vector2(
                1f,
                -1f);

        Shadow shadow =
            cardObject.GetComponent<
                Shadow>();

        shadow.effectColor =
            new Color(
                0f,
                0f,
                0f,
                0.32f);

        shadow.effectDistance =
            new Vector2(
                0f,
                -2f);

        GameObject accentObject =
            new GameObject(
                "Accent",
                typeof(RectTransform),
                typeof(Image));

        accentObject.transform.SetParent(
            cardObject.transform,
            false);

        RectTransform accentRect =
            accentObject.GetComponent<
                RectTransform>();

        accentRect.anchorMin =
            rightSide
                ? new Vector2(
                    1f,
                    0f)
                : new Vector2(
                    0f,
                    0f);

        accentRect.anchorMax =
            rightSide
                ? new Vector2(
                    1f,
                    1f)
                : new Vector2(
                    0f,
                    1f);

        accentRect.pivot =
            rightSide
                ? new Vector2(
                    1f,
                    0.5f)
                : new Vector2(
                    0f,
                    0.5f);

        accentRect.sizeDelta =
            new Vector2(
                4f,
                0f);

        Image accent =
            accentObject.GetComponent<
                Image>();

        accent.raycastTarget =
            false;

        GameObject iconObject =
            new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(Image));

        iconObject.transform.SetParent(
            cardObject.transform,
            false);

        RectTransform iconRect =
            iconObject.GetComponent<
                RectTransform>();

        if (rightSide)
        {
            SetAnchorBox(
                iconRect,
                0.83f,
                0.18f,
                0.96f,
                0.82f);
        }
        else
        {
            SetAnchorBox(
                iconRect,
                0.04f,
                0.18f,
                0.17f,
                0.82f);
        }

        Image iconBackground =
            iconObject.GetComponent<
                Image>();

        iconBackground.raycastTarget =
            false;

        TMP_Text iconText =
            CreateText(
                "IconText",
                iconObject.transform,
                13f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Stretch(
            iconText.rectTransform,
            1f);

        float contentMin =
            rightSide
                ? 0.05f
                : 0.21f;

        float contentMax =
            rightSide
                ? 0.79f
                : 0.95f;

        TMP_Text nameText =
            CreateText(
                "PlayerName",
                cardObject.transform,
                13f,
                FontStyles.Bold,
                rightSide
                    ? TextAlignmentOptions.MidlineRight
                    : TextAlignmentOptions.MidlineLeft);

        SetAnchorBox(
            nameText.rectTransform,
            contentMin,
            0.66f,
            contentMax,
            0.98f);

        nameText.enableAutoSizing =
            true;

        nameText.fontSizeMin = 8.5f;
        nameText.fontSizeMax = 14f;
        nameText.textWrappingMode =
            TextWrappingModes.NoWrap;
        nameText.overflowMode =
            TextOverflowModes.Truncate;

        TMP_Text stateText =
            CreateText(
                "State",
                cardObject.transform,
                8f,
                FontStyles.Bold,
                rightSide
                    ? TextAlignmentOptions.MidlineLeft
                    : TextAlignmentOptions.MidlineRight);

        if (rightSide)
        {
            SetAnchorBox(
                stateText.rectTransform,
                0.02f,
                0.68f,
                0.27f,
                0.97f);
        }
        else
        {
            SetAnchorBox(
                stateText.rectTransform,
                0.73f,
                0.68f,
                0.98f,
                0.97f);
        }

        stateText.enableAutoSizing =
            true;

        stateText.fontSizeMin = 5.8f;
        stateText.fontSizeMax = 8.5f;
        stateText.textWrappingMode =
            TextWrappingModes.NoWrap;
        stateText.overflowMode =
            TextOverflowModes.Truncate;

        TMP_Text worthText =
            CreateText(
                "NetWorth",
                cardObject.transform,
                11f,
                FontStyles.Bold,
                rightSide
                    ? TextAlignmentOptions.MidlineRight
                    : TextAlignmentOptions.MidlineLeft);

        SetAnchorBox(
            worthText.rectTransform,
            contentMin,
            0.32f,
            contentMax,
            0.66f);

        worthText.enableAutoSizing =
            true;

        worthText.fontSizeMin = 7.5f;
        worthText.fontSizeMax = 12f;
        worthText.textWrappingMode =
            TextWrappingModes.NoWrap;
        worthText.overflowMode =
            TextOverflowModes.Truncate;

        worthText.color =
            new Color(
                0.96f,
                0.87f,
                0.56f,
                1f);

        TMP_Text detailText =
            CreateText(
                "Details",
                cardObject.transform,
                8.5f,
                FontStyles.Normal,
                rightSide
                    ? TextAlignmentOptions.MidlineRight
                    : TextAlignmentOptions.MidlineLeft);

        SetAnchorBox(
            detailText.rectTransform,
            contentMin,
            0.02f,
            contentMax,
            0.34f);

        detailText.enableAutoSizing =
            true;

        detailText.fontSizeMin = 5.8f;
        detailText.fontSizeMax = 9f;
        detailText.textWrappingMode =
            TextWrappingModes.NoWrap;
        detailText.overflowMode =
            TextOverflowModes.Truncate;

        detailText.color =
            new Color(
                0.73f,
                0.79f,
                0.87f,
                1f);

        cards[slot] =
            card;

        backgrounds[slot] =
            background;

        accents[slot] =
            accent;

        iconBackgrounds[slot] =
            iconBackground;

        iconTexts[slot] =
            iconText;

        nameTexts[slot] =
            nameText;

        stateTexts[slot] =
            stateText;

        worthTexts[slot] =
            worthText;

        detailTexts[slot] =
            detailText;
    }

    private void ApplyCardGeometry()
    {
        if (cardsRoot == null)
        {
            return;
        }

        // The layout is relative to TABLET ROOT, not resultPanel.
        // Top result title remains above this area and Rematch/Leave remains below.
        Vector2[] mins =
        {
            new Vector2(0.060f, 0.445f),
            new Vector2(0.535f, 0.445f),
            new Vector2(0.060f, 0.245f),
            new Vector2(0.535f, 0.245f)
        };

        Vector2[] maxs =
        {
            new Vector2(0.465f, 0.595f),
            new Vector2(0.940f, 0.595f),
            new Vector2(0.465f, 0.395f),
            new Vector2(0.940f, 0.395f)
        };

        for (int slot = 0;
             slot < 4;
             slot++)
        {
            RectTransform card =
                cards[slot];

            if (card == null)
            {
                continue;
            }

            card.anchorMin =
                mins[slot];

            card.anchorMax =
                maxs[slot];

            card.offsetMin =
                Vector2.zero;

            card.offsetMax =
                Vector2.zero;

            card.localScale =
                Vector3.one;
        }
    }

    private void RefreshCards()
    {
        if (resultManager == null ||
            cardsRoot == null)
        {
            return;
        }

        MatchResultManager.OnlineResultSnapshot snapshot =
            resultManager.BuildOnlineResultSnapshot();

        if (snapshot == null ||
            !snapshot.valid)
        {
            return;
        }

        bool tied =
            snapshot.winnerSlots != null &&
            snapshot.winnerSlots.Length > 1;

        for (int slot = 0;
             slot < 4;
             slot++)
        {
            bool participating =
                snapshot.participating != null &&
                slot <
                    snapshot.participating.Length &&
                snapshot.participating[slot];

            RectTransform card =
                cards[slot];

            if (card == null)
            {
                continue;
            }

            card.gameObject.SetActive(
                participating);

            if (!participating)
            {
                continue;
            }

            PlayerGameState player =
                turnManager != null
                    ? turnManager
                        .GetPlayerStateBySlotIndex(
                            slot)
                    : null;

            string playerName =
                player != null
                    ? AtlasBoardL.PlayerName(
                        player)
                    : AtlasBoardL.R(
                        $"Player {slot + 1}",
                        $"Oyuncu {slot + 1}",
                        $"Jugador {slot + 1}",
                        $"Joueur {slot + 1}",
                        $"Spieler {slot + 1}",
                        $"플레이어 {slot + 1}",
                        $"Игрок {slot + 1}");

            bool bankrupt =
                snapshot.bankrupt != null &&
                slot <
                    snapshot.bankrupt.Length &&
                snapshot.bankrupt[slot];

            bool winner =
                IsWinner(
                    snapshot.winnerSlots,
                    slot);

            Color playerColor =
                player != null
                    ? player.UIColor
                    : Color.white;

            Color accentColor =
                winner
                    ? new Color(
                        0.96f,
                        0.72f,
                        0.16f,
                        1f)
                    : bankrupt
                        ? new Color(
                            0.80f,
                            0.25f,
                            0.28f,
                            1f)
                        : playerColor;

            backgrounds[slot].color =
                winner
                    ? new Color(
                        0.145f,
                        0.120f,
                        0.072f,
                        0.99f)
                    : new Color(
                        0.070f,
                        0.095f,
                        0.135f,
                        0.985f);

            accents[slot].color =
                accentColor;

            Color iconColor =
                playerColor;

            iconColor.a =
                0.98f;

            iconBackgrounds[slot].color =
                iconColor;

            ResolveFont(
                iconTexts[slot]);

            iconTexts[slot].text =
                $"P{slot + 1}";

            iconTexts[slot].color =
                ContrastTextColor(
                    playerColor);

            ResolveFont(
                nameTexts[slot]);

            nameTexts[slot].text =
                playerName;

            nameTexts[slot].color =
                winner
                    ? new Color(
                        1f,
                        0.88f,
                        0.48f,
                        1f)
                    : Color.white;

            ResolveFont(
                stateTexts[slot]);

            stateTexts[slot].text =
                winner
                    ? tied
                        ? TiedLabel()
                        : WinnerLabel()
                    : bankrupt
                        ? BankruptLabel()
                        : string.Empty;

            stateTexts[slot].color =
                accentColor;

            ResolveFont(
                worthTexts[slot]);

            worthTexts[slot].text =
                $"{NetWorthLabel()}  " +
                $"{GetArrayValue(snapshot.netWorth, slot):N0} ₵";

            ResolveFont(
                detailTexts[slot]);

            detailTexts[slot].text =
                $"{CashLabel()} {GetArrayValue(snapshot.cash, slot):N0}  •  " +
                $"{PropertyLabel()} {GetArrayValue(snapshot.propertyCount, slot)}/" +
                $"{GetArrayValue(snapshot.propertyValue, slot):N0}  •  " +
                $"{DevelopmentLabel()} {GetArrayValue(snapshot.developmentLevels, slot)}/" +
                $"{GetArrayValue(snapshot.developmentValue, slot):N0}";
        }
    }

    private void ResolveExistingCards()
    {
        if (cardsRoot == null)
        {
            return;
        }

        for (int slot = 0;
             slot < 4;
             slot++)
        {
            Transform card =
                cardsRoot.Find(
                    $"PlayerCard_{slot + 1}");

            if (card == null)
            {
                continue;
            }

            cards[slot] =
                card as RectTransform;

            backgrounds[slot] =
                card.GetComponent<Image>();

            accents[slot] =
                card.Find("Accent")
                    ?.GetComponent<Image>();

            iconBackgrounds[slot] =
                card.Find("Icon")
                    ?.GetComponent<Image>();

            iconTexts[slot] =
                card.Find("Icon/IconText")
                    ?.GetComponent<TMP_Text>();

            nameTexts[slot] =
                card.Find("PlayerName")
                    ?.GetComponent<TMP_Text>();

            stateTexts[slot] =
                card.Find("State")
                    ?.GetComponent<TMP_Text>();

            worthTexts[slot] =
                card.Find("NetWorth")
                    ?.GetComponent<TMP_Text>();

            detailTexts[slot] =
                card.Find("Details")
                    ?.GetComponent<TMP_Text>();
        }
    }

    private void HideLegacySummary()
    {
        if (legacySummaryText == null)
        {
            return;
        }

        if (!summaryStateCaptured)
        {
            summaryWasActive =
                legacySummaryText
                    .gameObject
                    .activeSelf;

            summaryStateCaptured =
                true;
        }

        legacySummaryText
            .gameObject
            .SetActive(
                false);
    }

    private void RestoreLegacySummary()
    {
        if (!summaryStateCaptured ||
            legacySummaryText == null)
        {
            return;
        }

        legacySummaryText
            .gameObject
            .SetActive(
                summaryWasActive);

        summaryStateCaptured =
            false;
    }

    private void HideCards()
    {
        if (cardsRoot != null &&
            cardsRoot.gameObject.activeSelf)
        {
            cardsRoot.gameObject.SetActive(
                false);
        }
    }

    private static bool IsWinner(
        int[] winnerSlots,
        int slot)
    {
        if (winnerSlots == null)
        {
            return false;
        }

        foreach (int winnerSlot
                 in winnerSlots)
        {
            if (winnerSlot == slot)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetArrayValue(
        int[] values,
        int slot)
    {
        return values != null &&
               slot >= 0 &&
               slot < values.Length
            ? values[slot]
            : 0;
    }

    private static string WinnerLabel() =>
        AtlasBoardL.R(
            "WINNER",
            "KAZANAN",
            "GANADOR",
            "VAINQUEUR",
            "SIEGER",
            "승자",
            "ПОБЕДИТЕЛЬ");

    private static string TiedLabel() =>
        AtlasBoardL.R(
            "TIED",
            "BERABERE",
            "EMPATE",
            "ÉGALITÉ",
            "GLEICHSTAND",
            "무승부",
            "НИЧЬЯ");

    private static string BankruptLabel() =>
        AtlasBoardL.R(
            "BANKRUPT",
            "İFLAS",
            "BANCARROTA",
            "FAILLITE",
            "BANKROTT",
            "파산",
            "БАНКРОТ");

    private static string NetWorthLabel() =>
        AtlasBoardL.R(
            "NET WORTH",
            "NET DEĞER",
            "VALOR NETO",
            "VALEUR NETTE",
            "NETTOWERT",
            "순자산",
            "ЧИСТЫЕ АКТИВЫ");

    private static string CashLabel() =>
        AtlasBoardL.R(
            "CASH",
            "NAKİT",
            "EFECTIVO",
            "LIQ.",
            "BARGELD",
            "현금",
            "НАЛ.");

    private static string PropertyLabel() =>
        AtlasBoardL.R(
            "PROP",
            "MÜLK",
            "PROP",
            "BIENS",
            "BESITZ",
            "부동산",
            "ИМУЩ");

    private static string DevelopmentLabel() =>
        AtlasBoardL.R(
            "DEV",
            "GEL.",
            "DES",
            "DÉV",
            "AUSBAU",
            "개발",
            "РАЗВ");

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment)
    {
        GameObject go =
            new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));

        go.transform.SetParent(
            parent,
            false);

        TMP_Text text =
            go.GetComponent<TMP_Text>();

        text.fontSize =
            fontSize;

        text.fontStyle =
            fontStyle;

        text.alignment =
            alignment;

        text.color =
            Color.white;

        text.raycastTarget =
            false;

        text.richText =
            true;

        ResolveFont(
            text);

        return text;
    }

    private static void ResolveFont(
        TMP_Text text)
    {
        if (text == null)
        {
            return;
        }

        AtlasBoardLocalizationManager manager =
            AtlasBoardLocalizationManager.Instance;

        if (manager == null)
        {
            if (text.font == null &&
                TMP_Settings.defaultFontAsset != null)
            {
                text.font =
                    TMP_Settings.defaultFontAsset;
            }

            return;
        }

        TMP_FontAsset current =
            text.font != null
                ? text.font
                : TMP_Settings.defaultFontAsset;

        TMP_FontAsset resolved =
            manager.ResolveFont(
                current);

        if (resolved != null)
        {
            text.font =
                resolved;
        }
    }

    private static Color ContrastTextColor(
        Color background)
    {
        float luminance =
            background.r * 0.299f +
            background.g * 0.587f +
            background.b * 0.114f;

        return luminance > 0.62f
            ? new Color(
                0.05f,
                0.06f,
                0.08f,
                1f)
            : Color.white;
    }

    private static void Stretch(
        RectTransform rect,
        float inset)
    {
        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            new Vector2(
                inset,
                inset);

        rect.offsetMax =
            new Vector2(
                -inset,
                -inset);
    }

    private static void SetAnchorBox(
        RectTransform rect,
        float xMin,
        float yMin,
        float xMax,
        float yMax)
    {
        rect.anchorMin =
            new Vector2(
                xMin,
                yMin);

        rect.anchorMax =
            new Vector2(
                xMax,
                yMax);

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;
    }

    private static T ReadPrivateField<T>(
        object target,
        string fieldName)
        where T : class
    {
        if (target == null)
        {
            return null;
        }

        FieldInfo field =
            target.GetType()
                .GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

        return field != null
            ? field.GetValue(
                target) as T
            : null;
    }
}
