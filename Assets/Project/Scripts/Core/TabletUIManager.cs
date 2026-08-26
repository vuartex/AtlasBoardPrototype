using TMPro;
using UnityEngine;

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

    private GameObject FindRequestedPanel()
    {
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

        tabletTitleText.text = "ATLAS BOARD";
    }
}
