using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(13400)]
[DisallowMultipleComponent]
public sealed class AtlasBoardPurchasePresentationPolish :
    MonoBehaviour
{
    private const float RefreshIntervalSeconds =
        0.10f;

    private TileResolutionManager tileResolutionManager;
    private TabletUIManager tabletManager;
    private PropertyDevelopmentManager developmentManager;

    private GameObject purchasePanel;
    private TMP_Text legacyInfoText;
    private Button buyButton;
    private Button passButton;

    private RectTransform tabletRoot;
    private RectTransform polishRoot;

    private TMP_Text propertyNameText;
    private TMP_Text groupText;
    private TMP_Text playerText;

    private TMP_Text priceLabelText;
    private TMP_Text priceValueText;

    private TMP_Text rentLabelText;
    private TMP_Text rentValueText;

    private TMP_Text nextRentLabelText;
    private TMP_Text nextRentValueText;

    private TMP_Text developmentLabelText;
    private TMP_Text developmentValueText;

    private TMP_Text balanceNowText;
    private TMP_Text balanceAfterText;
    private TMP_Text questionText;

    private Image groupAccent;

    private bool legacyCaptured;
    private bool legacyInfoWasActive;
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
                AtlasBoardPurchasePresentationPolish>() != null)
        {
            return;
        }

        new GameObject(
                "AtlasBoard_Phase14K_PurchasePresentation")
            .AddComponent<
                AtlasBoardPurchasePresentationPolish>();
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

        RestoreLegacyInfo();
        HidePolish();
    }

    public RectTransform TabletRoot =>
        tabletRoot;

    public RectTransform PolishRoot =>
        polishRoot;

    private void HandleLanguageChanged()
    {
        nextRefreshAt =
            0f;
    }

    private void LateUpdate()
    {
        ResolveReferences();

        bool visible =
            purchasePanel != null &&
            purchasePanel.activeSelf &&
            tabletRoot != null &&
            tabletRoot.gameObject.activeInHierarchy &&
            tileResolutionManager != null &&
            tileResolutionManager.PendingPurchaseTile != null;

        if (!visible)
        {
            RestoreLegacyInfo();
            HidePolish();
            return;
        }

        EnsurePresentation();
        HideLegacyInfo();

        polishRoot.SetAsLastSibling();

        if (!polishRoot.gameObject.activeSelf)
        {
            polishRoot.gameObject.SetActive(
                true);
        }

        if (Time.unscaledTime >=
            nextRefreshAt)
        {
            nextRefreshAt =
                Time.unscaledTime +
                RefreshIntervalSeconds;

            RefreshPresentation();
        }
    }

    private void ResolveReferences()
    {
        if (tileResolutionManager == null)
        {
            tileResolutionManager =
                UnityEngine.Object.FindAnyObjectByType<
                    TileResolutionManager>();
        }

        if (tabletManager == null)
        {
            tabletManager =
                UnityEngine.Object.FindAnyObjectByType<
                    TabletUIManager>();
        }

        if (developmentManager == null)
        {
            developmentManager =
                UnityEngine.Object.FindAnyObjectByType<
                    PropertyDevelopmentManager>();
        }

        if (tileResolutionManager != null)
        {
            purchasePanel =
                ReadPrivateField<GameObject>(
                    tileResolutionManager,
                    "purchasePanel");

            legacyInfoText =
                ReadPrivateField<TMP_Text>(
                    tileResolutionManager,
                    "purchaseInfoText");

            buyButton =
                ReadPrivateField<Button>(
                    tileResolutionManager,
                    "buyButton");

            passButton =
                ReadPrivateField<Button>(
                    tileResolutionManager,
                    "skipButton");
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

    private void EnsurePresentation()
    {
        if (tabletRoot == null)
        {
            return;
        }

        if (polishRoot == null)
        {
            Transform existing =
                tabletRoot.Find(
                    "Phase14K_PurchasePolish");

            if (existing != null)
            {
                polishRoot =
                    existing as RectTransform;

                ResolveExistingPresentation();
            }
            else
            {
                BuildPresentation();
            }
        }

        ReparentButtons();
    }

    private void BuildPresentation()
    {
        GameObject root =
            new GameObject(
                "Phase14K_PurchasePolish",
                typeof(RectTransform),
                typeof(LayoutElement));

        root.transform.SetParent(
            tabletRoot,
            false);

        polishRoot =
            root.GetComponent<
                RectTransform>();

        root.GetComponent<
                LayoutElement>()
            .ignoreLayout = true;

        Stretch(
            polishRoot);

        CreateHeroCard();
        CreateEconomyCards();
        CreateDecisionArea();

        ReparentButtons();

        polishRoot.gameObject.SetActive(
            false);
    }

    private void CreateHeroCard()
    {
        GameObject hero =
            CreatePanel(
                "PropertyHero",
                polishRoot,
                new Color(
                    0.055f,
                    0.080f,
                    0.115f,
                    0.99f));

        Box(
            hero.GetComponent<
                RectTransform>(),
            0.08f,
            0.66f,
            0.92f,
            0.82f);

        groupAccent =
            AddAccent(
                hero.transform);

        propertyNameText =
            CreateText(
                "PropertyName",
                hero.transform,
                24f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

        Box(
            propertyNameText.rectTransform,
            0.05f,
            0.46f,
            0.64f,
            0.92f);

        Auto(
            propertyNameText,
            13f,
            26f,
            TextWrappingModes.NoWrap);

        propertyNameText.color =
            new Color(
                1f,
                0.86f,
                0.42f,
                1f);

        groupText =
            CreateText(
                "Group",
                hero.transform,
                11f,
                FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);

        Box(
            groupText.rectTransform,
            0.05f,
            0.10f,
            0.64f,
            0.48f);

        Auto(
            groupText,
            7f,
            12f,
            TextWrappingModes.NoWrap);

        groupText.color =
            new Color(
                0.73f,
                0.80f,
                0.89f,
                1f);

        playerText =
            CreateText(
                "Player",
                hero.transform,
                13f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineRight);

        Box(
            playerText.rectTransform,
            0.64f,
            0.20f,
            0.95f,
            0.82f);

        Auto(
            playerText,
            8f,
            14f,
            TextWrappingModes.NoWrap);
    }

    private void CreateEconomyCards()
    {
        CreateMetricCard(
            "PriceCard",
            0.07f,
            0.38f,
            0.275f,
            0.61f,
            out priceLabelText,
            out priceValueText);

        CreateMetricCard(
            "RentCard",
            0.295f,
            0.38f,
            0.50f,
            0.61f,
            out rentLabelText,
            out rentValueText);

        CreateMetricCard(
            "NextRentCard",
            0.52f,
            0.38f,
            0.725f,
            0.61f,
            out nextRentLabelText,
            out nextRentValueText);

        CreateMetricCard(
            "DevelopmentCard",
            0.745f,
            0.38f,
            0.93f,
            0.61f,
            out developmentLabelText,
            out developmentValueText);
    }

    private void CreateMetricCard(
        string name,
        float xMin,
        float yMin,
        float xMax,
        float yMax,
        out TMP_Text label,
        out TMP_Text value)
    {
        GameObject card =
            CreatePanel(
                name,
                polishRoot,
                new Color(
                    0.070f,
                    0.095f,
                    0.135f,
                    0.99f));

        Box(
            card.GetComponent<
                RectTransform>(),
            xMin,
            yMin,
            xMax,
            yMax);

        label =
            CreateText(
                "Label",
                card.transform,
                9f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            label.rectTransform,
            0.08f,
            0.68f,
            0.92f,
            0.91f);

        Auto(
            label,
            6f,
            10f,
            TextWrappingModes.Normal);

        label.color =
            new Color(
                0.68f,
                0.75f,
                0.84f,
                1f);

        value =
            CreateText(
                "Value",
                card.transform,
                23f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            value.rectTransform,
            0.08f,
            0.19f,
            0.92f,
            0.67f);

        Auto(
            value,
            12f,
            25f,
            TextWrappingModes.NoWrap);

        value.color =
            new Color(
                1f,
                0.83f,
                0.28f,
                1f);
    }

    private void CreateDecisionArea()
    {
        GameObject balanceCard =
            CreatePanel(
                "BalanceCard",
                polishRoot,
                new Color(
                    0.065f,
                    0.085f,
                    0.115f,
                    0.99f));

        Box(
            balanceCard.GetComponent<
                RectTransform>(),
            0.13f,
            0.245f,
            0.87f,
            0.345f);

        balanceNowText =
            CreateText(
                "BalanceNow",
                balanceCard.transform,
                11f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);

        Box(
            balanceNowText.rectTransform,
            0.05f,
            0.10f,
            0.47f,
            0.90f);

        Auto(
            balanceNowText,
            7f,
            12f,
            TextWrappingModes.NoWrap);

        balanceAfterText =
            CreateText(
                "BalanceAfter",
                balanceCard.transform,
                11f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineRight);

        Box(
            balanceAfterText.rectTransform,
            0.53f,
            0.10f,
            0.95f,
            0.90f);

        Auto(
            balanceAfterText,
            7f,
            12f,
            TextWrappingModes.NoWrap);

        questionText =
            CreateText(
                "Question",
                polishRoot,
                12f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

        Box(
            questionText.rectTransform,
            0.18f,
            0.185f,
            0.82f,
            0.235f);

        Auto(
            questionText,
            8f,
            13f,
            TextWrappingModes.NoWrap);

        questionText.color =
            new Color(
                0.88f,
                0.91f,
                0.95f,
                1f);
    }

    private void ReparentButtons()
    {
        if (polishRoot == null)
        {
            return;
        }

        Reparent(
            buyButton,
            polishRoot);

        Reparent(
            passButton,
            polishRoot);

        StyleButton(
            buyButton,
            new Color(
                0.15f,
                0.64f,
                0.45f,
                1f));

        StyleButton(
            passButton,
            new Color(
                0.46f,
                0.49f,
                0.57f,
                1f));

        Box(
            buyButton != null
                ? buyButton.transform as
                    RectTransform
                : null,
            0.17f,
            0.07f,
            0.47f,
            0.17f);

        Box(
            passButton != null
                ? passButton.transform as
                    RectTransform
                : null,
            0.53f,
            0.07f,
            0.83f,
            0.17f);
    }

    private void RefreshPresentation()
    {
        BoardTile tile =
            tileResolutionManager
                .PendingPurchaseTile;

        PlayerGameState player =
            tileResolutionManager
                .PendingPurchasePlayer;

        if (tile == null)
        {
            return;
        }

        int price =
            Mathf.Max(
                0,
                tile.PurchasePrice);

        int baseRent =
            Mathf.Max(
                0,
                tile.BaseRent);

        int developmentCost =
            developmentManager != null
                ? Mathf.Max(
                    0,
                    developmentManager
                        .GetDevelopmentCost(
                            tile))
                : Mathf.Max(
                    0,
                    tile.DevelopmentCost);

        int nextRent =
            developmentManager != null
                ? Mathf.Max(
                    baseRent,
                    developmentManager
                        .GetProjectedRentAtNextLevel(
                            tile))
                : baseRent;

        int currentBalance =
            player != null
                ? Mathf.Max(
                    0,
                    player.CurrentMoney)
                : 0;

        int afterPurchase =
            Mathf.Max(
                0,
                currentBalance -
                price);

        SetText(
            propertyNameText,
            tile.DisplayName);

        string groupName =
            !string.IsNullOrWhiteSpace(
                tile.GroupDisplayName)
                ? tile.GroupDisplayName
                : tile.GroupId;

        SetText(
            groupText,
            AtlasBoardL.R(
                "GROUP  •  {0}",
                "GRUP  •  {0}",
                "GRUPO  •  {0}",
                "GROUPE  •  {0}",
                "GRUPPE  •  {0}",
                "그룹  •  {0}",
                "ГРУППА  •  {0}",
                string.IsNullOrWhiteSpace(
                    groupName)
                    ? "-"
                    : groupName));

        SetText(
            playerText,
            player != null
                ? AtlasBoardL.PlayerName(
                    player)
                : AtlasBoardL.T(
                    "common.player"));

        if (groupAccent != null)
        {
            groupAccent.color =
                tile.GroupColor !=
                    Color.clear
                    ? tile.GroupColor
                    : new Color(
                        1f,
                        0.75f,
                        0.18f,
                        1f);
        }

        SetText(
            priceLabelText,
            AtlasBoardL.R(
                "PURCHASE PRICE",
                "SATIN ALMA FİYATI",
                "PRECIO DE COMPRA",
                "PRIX D’ACHAT",
                "KAUFPREIS",
                "구매 가격",
                "ЦЕНА ПОКУПКИ"));

        SetText(
            priceValueText,
            $"{price:N0} ₵");

        SetText(
            rentLabelText,
            AtlasBoardL.R(
                "BASE RENT",
                "TEMEL KİRA",
                "ALQUILER BASE",
                "LOYER DE BASE",
                "GRUNDMIETE",
                "기본 임대료",
                "БАЗОВАЯ АРЕНДА"));

        SetText(
            rentValueText,
            $"{baseRent:N0} ₵");

        SetText(
            nextRentLabelText,
            AtlasBoardL.R(
                "LEVEL 1 RENT",
                "1. SEVİYE KİRASI",
                "ALQUILER NIVEL 1",
                "LOYER NIVEAU 1",
                "MIETE STUFE 1",
                "1레벨 임대료",
                "АРЕНДА УРОВНЯ 1"));

        SetText(
            nextRentValueText,
            $"{nextRent:N0} ₵");

        SetText(
            developmentLabelText,
            AtlasBoardL.R(
                "DEVELOPMENT COST",
                "GELİŞTİRME MALİYETİ",
                "COSTE DE DESARROLLO",
                "COÛT DE DÉVELOPPEMENT",
                "AUSBAUKOSTEN",
                "개발 비용",
                "СТОИМОСТЬ РАЗВИТИЯ"));

        SetText(
            developmentValueText,
            $"{developmentCost:N0} ₵");

        SetText(
            balanceNowText,
            AtlasBoardL.R(
                "BALANCE  {0:N0} ₵",
                "BAKİYE  {0:N0} ₵",
                "SALDO  {0:N0} ₵",
                "SOLDE  {0:N0} ₵",
                "GUTHABEN  {0:N0} ₵",
                "잔액  {0:N0} ₵",
                "БАЛАНС  {0:N0} ₵",
                currentBalance));

        SetText(
            balanceAfterText,
            AtlasBoardL.R(
                "AFTER PURCHASE  {0:N0} ₵",
                "SATIN ALMA SONRASI  {0:N0} ₵",
                "DESPUÉS DE COMPRAR  {0:N0} ₵",
                "APRÈS ACHAT  {0:N0} ₵",
                "NACH KAUF  {0:N0} ₵",
                "구매 후  {0:N0} ₵",
                "ПОСЛЕ ПОКУПКИ  {0:N0} ₵",
                afterPurchase));

        SetText(
            questionText,
            AtlasBoardL.R(
                "Add this property to your portfolio?",
                "Bu mülkü portföyüne eklemek ister misin?",
                "¿Añadir esta propiedad a tu cartera?",
                "Ajouter cette propriété à votre portefeuille ?",
                "Dieses Grundstück deinem Portfolio hinzufügen?",
                "이 부동산을 포트폴리오에 추가하시겠습니까?",
                "Добавить этот объект в ваш портфель?"));

        SetButtonLabel(
            buyButton,
            AtlasBoardL.R(
                "BUY",
                "SATIN AL",
                "COMPRAR",
                "ACHETER",
                "KAUFEN",
                "구매",
                "КУПИТЬ"));

        SetButtonLabel(
            passButton,
            AtlasBoardL.R(
                "PASS",
                "PAS",
                "PASAR",
                "PASSER",
                "PASSEN",
                "패스",
                "ПАС"));
    }

    private void HideLegacyInfo()
    {
        if (legacyInfoText == null)
        {
            return;
        }

        if (!legacyCaptured)
        {
            legacyInfoWasActive =
                legacyInfoText
                    .gameObject
                    .activeSelf;

            legacyCaptured =
                true;
        }

        legacyInfoText
            .gameObject
            .SetActive(
                false);
    }

    private void RestoreLegacyInfo()
    {
        if (!legacyCaptured ||
            legacyInfoText == null)
        {
            return;
        }

        legacyInfoText
            .gameObject
            .SetActive(
                legacyInfoWasActive);

        legacyCaptured =
            false;
    }

    private void HidePolish()
    {
        if (polishRoot != null &&
            polishRoot.gameObject.activeSelf)
        {
            polishRoot.gameObject.SetActive(
                false);
        }
    }

    private void ResolveExistingPresentation()
    {
        propertyNameText =
            FindText(
                "PropertyHero/PropertyName");

        groupText =
            FindText(
                "PropertyHero/Group");

        playerText =
            FindText(
                "PropertyHero/Player");

        groupAccent =
            FindImage(
                "PropertyHero/Accent");

        priceLabelText =
            FindText(
                "PriceCard/Label");

        priceValueText =
            FindText(
                "PriceCard/Value");

        rentLabelText =
            FindText(
                "RentCard/Label");

        rentValueText =
            FindText(
                "RentCard/Value");

        nextRentLabelText =
            FindText(
                "NextRentCard/Label");

        nextRentValueText =
            FindText(
                "NextRentCard/Value");

        developmentLabelText =
            FindText(
                "DevelopmentCard/Label");

        developmentValueText =
            FindText(
                "DevelopmentCard/Value");

        balanceNowText =
            FindText(
                "BalanceCard/BalanceNow");

        balanceAfterText =
            FindText(
                "BalanceCard/BalanceAfter");

        questionText =
            FindText(
                "Question");
    }

    private TMP_Text FindText(
        string path)
    {
        Transform child =
            polishRoot != null
                ? polishRoot.Find(
                    path)
                : null;

        return child != null
            ? child.GetComponent<
                TMP_Text>()
            : null;
    }

    private Image FindImage(
        string path)
    {
        Transform child =
            polishRoot != null
                ? polishRoot.Find(
                    path)
                : null;

        return child != null
            ? child.GetComponent<
                Image>()
            : null;
    }

    private static void Reparent(
        Component component,
        Transform parent)
    {
        if (component == null ||
            parent == null ||
            component.transform.parent ==
                parent)
        {
            return;
        }

        component.transform.SetParent(
            parent,
            false);
    }

    private static GameObject CreatePanel(
        string name,
        Transform parent,
        Color color)
    {
        GameObject panel =
            new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Shadow));

        panel.transform.SetParent(
            parent,
            false);

        Image image =
            panel.GetComponent<
                Image>();

        image.color =
            color;

        image.raycastTarget =
            false;

        Shadow shadow =
            panel.GetComponent<
                Shadow>();

        shadow.effectColor =
            new Color(
                0f,
                0f,
                0f,
                0.28f);

        shadow.effectDistance =
            new Vector2(
                0f,
                -2f);

        return panel;
    }

    private static Image AddAccent(
        Transform parent)
    {
        GameObject go =
            new GameObject(
                "Accent",
                typeof(RectTransform),
                typeof(Image));

        go.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            go.GetComponent<
                RectTransform>();

        rect.anchorMin =
            new Vector2(
                0f,
                0f);

        rect.anchorMax =
            new Vector2(
                0f,
                1f);

        rect.pivot =
            new Vector2(
                0f,
                0.5f);

        rect.sizeDelta =
            new Vector2(
                5f,
                0f);

        Image image =
            go.GetComponent<
                Image>();

        image.raycastTarget =
            false;

        return image;
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        float fontSize,
        FontStyles style,
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
            go.GetComponent<
                TMP_Text>();

        text.fontSize =
            fontSize;

        text.fontStyle =
            style;

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

    private static void SetText(
        TMP_Text text,
        string value)
    {
        if (text == null)
        {
            return;
        }

        ResolveFont(
            text);

        string resolvedValue =
            value ?? string.Empty;

        if (!string.Equals(
                text.text,
                resolvedValue,
                StringComparison.Ordinal))
        {
            text.text =
                resolvedValue;
        }
    }

    private static void Auto(
        TMP_Text text,
        float min,
        float max,
        TextWrappingModes wrapping)
    {
        if (text == null)
        {
            return;
        }

        text.enableAutoSizing =
            true;

        text.fontSizeMin =
            min;

        text.fontSizeMax =
            max;

        text.textWrappingMode =
            wrapping;

        text.overflowMode =
            TextOverflowModes.Truncate;
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

        TMP_FontAsset current =
            text.font != null
                ? text.font
                : TMP_Settings.defaultFontAsset;

        TMP_FontAsset resolved =
            manager != null
                ? manager.ResolveFont(
                    current)
                : current;

        if (resolved != null &&
            text.font != resolved)
        {
            text.font =
                resolved;
        }
    }

    private static void StyleButton(
        Button button,
        Color color)
    {
        if (button == null)
        {
            return;
        }

        Image image =
            button.GetComponent<
                Image>();

        if (image != null)
        {
            image.color =
                button.interactable
                    ? color
                    : new Color(
                        0.19f,
                        0.22f,
                        0.27f,
                        0.90f);
        }

        TMP_Text label =
            button.GetComponentInChildren<
                TMP_Text>(
                    true);

        if (label != null)
        {
            ResolveFont(
                label);

            Auto(
                label,
                8f,
                16f,
                TextWrappingModes.NoWrap);

            label.fontStyle =
                FontStyles.Bold;

            label.alignment =
                TextAlignmentOptions.Center;
        }
    }

    private static void SetButtonLabel(
        Button button,
        string value)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text label =
            button.GetComponentInChildren<
                TMP_Text>(
                    true);

        if (label != null)
        {
            ResolveFont(
                label);

            label.text =
                value;
        }
    }

    private static void Box(
        RectTransform rect,
        float xMin,
        float yMin,
        float xMax,
        float yMax)
    {
        if (rect == null)
        {
            return;
        }

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

        rect.localScale =
            Vector3.one;
    }

    private static void Stretch(
        RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin =
            Vector2.zero;

        rect.anchorMax =
            Vector2.one;

        rect.offsetMin =
            Vector2.zero;

        rect.offsetMax =
            Vector2.zero;

        rect.localScale =
            Vector3.one;
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
