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
    private GameObject travelPanel;

    [SerializeField]
    private GameObject developmentPanel;

    [SerializeField]
    private GameObject matchResultPanel;

    private GameObject currentPanel;

    private void Awake()
    {
        HideTabletShell();
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
            UpdateTabletTitle(currentPanel);
        }

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
            tabletTitleText.text = "MÜLK";
            return;
        }

        if (panel == auctionPanel)
        {
            tabletTitleText.text = "AÇIK ARTIRMA";
            return;
        }

        if (panel == tradePanel)
        {
            tabletTitleText.text = "TAKAS";
            return;
        }

        if (panel == eventPanel)
        {
            tabletTitleText.text = "ETKİNLİK";
            return;
        }

        if (panel == specialResultPanel)
        {
            tabletTitleText.text = "SONUÇ";
            return;
        }

        if (panel == travelPanel)
        {
            tabletTitleText.text = "SEYAHAT";
            return;
        }

        if (panel == developmentPanel)
        {
            tabletTitleText.text = "GELİŞTİRME";
            return;
        }

        if (panel == matchResultPanel)
        {
            tabletTitleText.text = "MAÇ SONUCU";
            return;
        }

        tabletTitleText.text = "ATLAS BOARD";
    }
}
