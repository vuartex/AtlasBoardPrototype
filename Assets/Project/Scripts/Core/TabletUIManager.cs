using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TabletUIManager : MonoBehaviour
{
    [Header("Tablet Shell")]
    [SerializeField]
    private GameObject backdropDim;

    [SerializeField]
    private GameObject tabletRoot;

    [SerializeField]
    private TMP_Text tabletTitleText;

    [Header("Tablet Panels")]
    [SerializeField]
    private GameObject purchasePanel;

    [SerializeField]
    private GameObject auctionPanel;

    [SerializeField]
    private GameObject tradePanel;

    [SerializeField]
    private GameObject eventPanel;

    [SerializeField]
    private GameObject specialResultPanel;

    [SerializeField]
    private GameObject doublesPenaltyPanel;

    [SerializeField]
    private GameObject travelPanel;

    [SerializeField]
    private GameObject developmentPanel;

    [SerializeField]
    private GameObject matchResultPanel;

    [Header("Online Seat Notice (runtime-built fallback)")]
    [SerializeField]
    private GameObject onlineSeatNoticePanel;

    private TMP_Text onlineSeatNoticeText;
    private Button onlineSeatNoticeOkButton;
    private Coroutine onlineSeatNoticeRoutine;
    private GameObject currentPanel;

    private void Awake()
    {
        AtlasBoardLocalizationManager.LanguageChanged +=
            HandleLanguageChanged;

        HideTabletShell();
    }

    private void OnDestroy()
    {
        AtlasBoardLocalizationManager.LanguageChanged -=
            HandleLanguageChanged;
    }

    private void HandleLanguageChanged()
    {
        if (currentPanel != null)
        {
            UpdateTabletTitle(
                currentPanel);
        }
    }

    private void LateUpdate()
    {
        GameObject requestedPanel =
            FindRequestedPanel();

        if (requestedPanel == null)
        {
            currentPanel = null;
            HideTabletShell();
            return;
        }

        if (currentPanel != requestedPanel)
        {
            currentPanel = requestedPanel;
        }

        // Several gameplay managers still write to panel/header text.
        // Re-assert the localized tablet title every frame while a panel is open.
        UpdateTabletTitle(
            currentPanel);

        ShowTabletShell();
    }

    public void ResetForNewMatchSession()
    {
        HideOnlineSeatNotice();

        GameObject[] panels =
        {
            purchasePanel,
            auctionPanel,
            tradePanel,
            eventPanel,
            specialResultPanel,
            doublesPenaltyPanel,
            travelPanel,
            developmentPanel,
            matchResultPanel
        };

        foreach (GameObject panel in panels)
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        currentPanel = null;
        HideTabletShell();

        if (onlineSeatNoticeText != null)
        {
            onlineSeatNoticeText.text = string.Empty;
        }
    }

    public void ShowOnlineSeatNotice(
        string message,
        float autoHideSeconds = 3f)
    {
        EnsureOnlineSeatNoticePanel();

        if (onlineSeatNoticePanel == null)
        {
            return;
        }

        if (onlineSeatNoticeText != null)
        {
            onlineSeatNoticeText.text =
                message ?? string.Empty;
        }

        onlineSeatNoticePanel.SetActive(true);

        if (onlineSeatNoticeRoutine != null)
        {
            StopCoroutine(onlineSeatNoticeRoutine);
            onlineSeatNoticeRoutine = null;
        }

        if (autoHideSeconds > 0f)
        {
            onlineSeatNoticeRoutine =
                StartCoroutine(
                    HideOnlineSeatNoticeAfter(
                        autoHideSeconds));
        }
    }

    public void HideOnlineSeatNotice()
    {
        if (onlineSeatNoticeRoutine != null)
        {
            StopCoroutine(onlineSeatNoticeRoutine);
            onlineSeatNoticeRoutine = null;
        }

        if (onlineSeatNoticePanel != null)
        {
            onlineSeatNoticePanel.SetActive(false);
        }
    }

    private IEnumerator HideOnlineSeatNoticeAfter(
        float seconds)
    {
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0.25f, seconds));

        onlineSeatNoticeRoutine = null;

        if (onlineSeatNoticePanel != null)
        {
            onlineSeatNoticePanel.SetActive(false);
        }
    }

    private void EnsureOnlineSeatNoticePanel()
    {
        if (onlineSeatNoticePanel != null ||
            tabletRoot == null)
        {
            return;
        }

        GameObject panel =
            new GameObject(
                "Panel_OnlineSeatNotice",
                typeof(RectTransform),
                typeof(Image));

        panel.transform.SetParent(
            tabletRoot.transform,
            false);

        RectTransform rect =
            panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.06f, 0.08f);
        rect.anchorMax = new Vector2(0.94f, 0.86f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image background = panel.GetComponent<Image>();
        background.color =
            new Color(0.02f, 0.055f, 0.06f, 0.98f);

        GameObject textObject =
            new GameObject(
                "Text_OnlineSeatNotice",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);

        RectTransform textRect =
            textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.08f, 0.24f);
        textRect.anchorMax = new Vector2(0.92f, 0.88f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        onlineSeatNoticeText =
            textObject.GetComponent<TextMeshProUGUI>();
        onlineSeatNoticeText.alignment =
            TextAlignmentOptions.Center;
        onlineSeatNoticeText.fontSize = 24f;
        onlineSeatNoticeText.enableAutoSizing = true;
        onlineSeatNoticeText.fontSizeMin = 14f;
        onlineSeatNoticeText.fontSizeMax = 26f;
        onlineSeatNoticeText.color = Color.white;

        GameObject buttonObject =
            new GameObject(
                "Button_OnlineSeatNoticeOK",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
        buttonObject.transform.SetParent(panel.transform, false);

        RectTransform buttonRect =
            buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.36f, 0.06f);
        buttonRect.anchorMax = new Vector2(0.64f, 0.20f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        buttonObject.GetComponent<Image>().color =
            new Color(0.18f, 0.48f, 0.62f, 1f);

        onlineSeatNoticeOkButton =
            buttonObject.GetComponent<Button>();
        onlineSeatNoticeOkButton.onClick.AddListener(
            HideOnlineSeatNotice);

        GameObject labelObject =
            new GameObject(
                "Text_OK",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect =
            labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TMP_Text label =
            labelObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 20f;
        label.color = Color.white;
        label.text = "OK";

        onlineSeatNoticePanel = panel;
        onlineSeatNoticePanel.SetActive(false);
    }

    private GameObject FindRequestedPanel()
    {
        if (IsPanelRequested(onlineSeatNoticePanel))
        {
            return onlineSeatNoticePanel;
        }

        if (IsPanelRequested(purchasePanel))
        {
            return purchasePanel;
        }

        if (IsPanelRequested(auctionPanel))
        {
            return auctionPanel;
        }

        if (IsPanelRequested(tradePanel))
        {
            return tradePanel;
        }

        if (IsPanelRequested(eventPanel))
        {
            return eventPanel;
        }

        if (IsPanelRequested(specialResultPanel))
        {
            return specialResultPanel;
        }

        if (IsPanelRequested(doublesPenaltyPanel))
        {
            return doublesPenaltyPanel;
        }

        if (IsPanelRequested(travelPanel))
        {
            return travelPanel;
        }

        if (IsPanelRequested(developmentPanel))
        {
            return developmentPanel;
        }

        if (IsPanelRequested(matchResultPanel))
        {
            return matchResultPanel;
        }

        return null;
    }

    private bool IsPanelRequested(
        GameObject panel)
    {
        return panel != null &&
               panel.activeSelf;
    }

    private void ShowTabletShell()
    {
        if (backdropDim != null &&
            !backdropDim.activeSelf)
        {
            backdropDim.SetActive(true);
        }

        if (tabletRoot != null &&
            !tabletRoot.activeSelf)
        {
            tabletRoot.SetActive(true);
        }
    }

    private void HideTabletShell()
    {
        if (backdropDim != null &&
            backdropDim.activeSelf)
        {
            backdropDim.SetActive(false);
        }

        if (tabletRoot != null &&
            tabletRoot.activeSelf)
        {
            tabletRoot.SetActive(false);
        }
    }

    private void UpdateTabletTitle(
        GameObject panel)
    {
        if (tabletTitleText == null)
        {
            return;
        }

        if (panel == purchasePanel)
        {
            tabletTitleText.text = AtlasBoardL.T("tablet.property");
            return;
        }

        if (panel == auctionPanel)
        {
            tabletTitleText.text = AtlasBoardL.T("tablet.auction");
            return;
        }

        if (panel == tradePanel)
        {
            tabletTitleText.text = AtlasBoardL.T("tablet.trade");
            return;
        }

        if (panel == eventPanel)
        {
            tabletTitleText.text = AtlasBoardL.T("tablet.event");
            return;
        }

        if (panel == specialResultPanel)
        {
            tabletTitleText.text = AtlasBoardL.T("tablet.result");
            return;
        }

        if (panel == doublesPenaltyPanel)
        {
            tabletTitleText.text = AtlasBoardL.T("tablet.penalty");
            return;
        }

        if (panel == travelPanel)
        {
            tabletTitleText.text = AtlasBoardL.T("tablet.travel");
            return;
        }

        if (panel == developmentPanel)
        {
            tabletTitleText.text = AtlasBoardL.T("tablet.development");
            return;
        }

        if (panel == matchResultPanel)
        {
            tabletTitleText.text = AtlasBoardL.T("tablet.match_result");
            return;
        }

        if (panel == onlineSeatNoticePanel)
        {
            string language =
                AtlasBoardLocalizationManager.Instance != null
                    ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode
                    : "en";

            tabletTitleText.text =
                (language ?? "en").ToLowerInvariant() == "tr"
                    ? "OYUNCU DURUMU"
                    : "PLAYER STATUS";
            return;
        }

        tabletTitleText.text = "ATLAS BOARD";
    }
}
