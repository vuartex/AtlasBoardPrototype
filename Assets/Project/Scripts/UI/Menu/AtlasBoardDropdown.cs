using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AtlasBoardDropdown : TMP_Dropdown
{
    [Header("AtlasBoard Popup")]
    [SerializeField, Range(1, 12)]
    private int maxVisibleRows = 6;

    [SerializeField, Min(32f)]
    private float rowHeight = 60f;

    [SerializeField]
    private int popupSortingOrder = 32000;

    protected override GameObject CreateDropdownList(
        GameObject template)
    {
        GameObject list =
            base.CreateDropdownList(
                template);

        if (list == null)
        {
            return null;
        }

        ForcePopupAboveLobby(
            list);

        ForcePopupCapacity(
            list);

        return list;
    }

    private void ForcePopupAboveLobby(
        GameObject list)
    {
        list.transform.SetAsLastSibling();

        Canvas popupCanvas =
            list.GetComponent<Canvas>();

        if (popupCanvas == null)
        {
            popupCanvas =
                list.AddComponent<Canvas>();
        }

        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder =
            popupSortingOrder;

        GraphicRaycaster raycaster =
            list.GetComponent<
                GraphicRaycaster>();

        if (raycaster == null)
        {
            list.AddComponent<
                GraphicRaycaster>();
        }

        CanvasGroup canvasGroup =
            list.GetComponent<
                CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup =
                list.AddComponent<
                    CanvasGroup>();
        }

        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
    }

    private void ForcePopupCapacity(
        GameObject list)
    {
        RectTransform listRect =
            list.transform as RectTransform;

        if (listRect == null)
        {
            return;
        }

        int optionCount =
            options != null
                ? options.Count
                : 0;

        int visibleRows =
            Mathf.Clamp(
                optionCount,
                1,
                maxVisibleRows);

        float popupHeight =
            visibleRows *
            rowHeight +
            28f;

        Vector2 listSize =
            listRect.sizeDelta;

        listSize.y =
            popupHeight;

        listRect.sizeDelta =
            listSize;

        ScrollRect scrollRect =
            list.GetComponent<
                ScrollRect>();

        if (scrollRect == null)
        {
            return;
        }

        if (scrollRect.viewport != null)
        {
            RectTransform viewport =
                scrollRect.viewport;

            viewport.anchorMin =
                Vector2.zero;

            viewport.anchorMax =
                Vector2.one;

            viewport.offsetMin =
                new Vector2(
                    4f,
                    4f);

            viewport.offsetMax =
                new Vector2(
                    -4f,
                    -4f);
        }

        if (scrollRect.content != null)
        {
            RectTransform content =
                scrollRect.content;

            float contentHeight =
                Mathf.Max(
                    optionCount *
                    rowHeight,
                    popupHeight - 8f);

            Vector2 contentSize =
                content.sizeDelta;

            contentSize.y =
                contentHeight;

            content.sizeDelta =
                contentSize;
        }

        scrollRect.vertical =
            optionCount >
            maxVisibleRows;

        scrollRect.horizontal =
            false;

        scrollRect.scrollSensitivity =
            rowHeight;
    }

#if UNITY_EDITOR
    public void EditorConfigurePopup(
        int visibleRows,
        float newRowHeight,
        int sortingOrder)
    {
        maxVisibleRows =
            Mathf.Clamp(
                visibleRows,
                1,
                12);

        rowHeight =
            Mathf.Max(
                32f,
                newRowHeight);

        popupSortingOrder =
            sortingOrder;
    }
#endif
}
