#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Read-only Editor Play Mode QA. Does not create rooms, change Firebase data,
/// or simulate a button click. Re-run after exiting and reentering the chooser.
/// </summary>
public static class AtlasBoardPrivateTableUiRegressionQa
{
    [MenuItem("Atlas Board/QA & Diagnostics/Private Table/Verify Button Layout (Play Mode)")]
    public static void Verify()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("ATB_PRIVATE_ENTRY_QA NOT_RUN reason=enter_play_mode_first");
            return;
        }
        AtlasBoardPrivateLobbyUIController lobby =
            UnityEngine.Object.FindAnyObjectByType<AtlasBoardPrivateLobbyUIController>(
                FindObjectsInactive.Include);
        if (lobby == null)
        {
            Debug.LogError("ATB_PRIVATE_ENTRY_QA FAIL reason=lobby_not_found");
            return;
        }
        Button cancel = Read<Button>(lobby, "cancelEntryButton");
        Button create = Read<Button>(lobby, "createRoomButton");
        GameObject chooser = Read<GameObject>(lobby, "roomEntryOverlay");
        if (cancel == null || create == null || chooser == null ||
            !chooser.activeInHierarchy)
        {
            Debug.LogWarning("ATB_PRIVATE_ENTRY_QA NOT_RUN reason=open_private_room_chooser");
            return;
        }
        Transform parent = cancel.transform.parent;
        int count = 0;
        Button local = null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name != AtlasBoardPrivateTableEntryGate.LocalButtonName ||
                !child.gameObject.activeSelf) continue;
            count++;
            local = child.GetComponent<Button>();
        }
        if (count != 1 || local == null)
        {
            Debug.LogError("ATB_PRIVATE_ENTRY_QA FAIL reason=local_button_count count=" + count);
            return;
        }
        RectTransform a = create.GetComponent<RectTransform>();
        RectTransform b = local.GetComponent<RectTransform>();
        RectTransform c = cancel.GetComponent<RectTransform>();
        bool sameParent = a.parent == b.parent && b.parent == c.parent;
        bool aligned = sameParent && Mathf.Abs(b.anchoredPosition.x - c.anchoredPosition.x) <= 0.5f;
        bool vertical = sameParent && a.anchoredPosition.y > b.anchoredPosition.y &&
            b.anchoredPosition.y > c.anchoredPosition.y;
        float gapTop = (a.anchoredPosition.y - b.anchoredPosition.y) -
            0.5f * (a.rect.height + b.rect.height);
        float gapBottom = (b.anchoredPosition.y - c.anchoredPosition.y) -
            0.5f * (b.rect.height + c.rect.height);
        // The generated button must not inherit persistent Cancel listeners.
        bool isolated = local.onClick.GetPersistentEventCount() == 0 &&
            local.gameObject != cancel.gameObject &&
            local.gameObject != create.gameObject;
        bool pass = aligned && vertical && isolated &&
            gapTop >= 5f && gapBottom >= 5f && Mathf.Abs(gapTop - gapBottom) <= 8f;
        Debug.Log((pass ? "ATB_PRIVATE_ENTRY_QA PASS" : "ATB_PRIVATE_ENTRY_QA FAIL") +
            " local_buttons=" + count +
            " aligned=" + aligned + " separated=" + vertical +
            " isolated=" + isolated + " gap_top=" + gapTop.ToString("F1") +
            " gap_bottom=" + gapBottom.ToString("F1"));
    }

    private static T Read<T>(object target, string name) where T : class
    {
        FieldInfo field = target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null ? field.GetValue(target) as T : null;
    }
}
#endif
