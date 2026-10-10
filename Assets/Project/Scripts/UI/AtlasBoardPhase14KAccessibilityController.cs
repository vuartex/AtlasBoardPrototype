using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(14500)]
[DisallowMultipleComponent]
public sealed class AtlasBoardPhase14KAccessibilityController :
    MonoBehaviour
{
    private const float SettingsSyncInterval =
        0.50f;

    private const float StickThreshold =
        0.55f;

    private const float InitialRepeatDelay =
        0.32f;

    private const float RepeatDelay =
        0.14f;

    private const string RemovedLegacyBadgeName =
        "__OwnerAccessibilityBadge";

    private static AtlasBoardPhase14KAccessibilityController
        instance;

    private bool gamepadFocusActive;
    private Vector2 lastStickDirection;
    private float nextStickRepeatAt;
    private float nextSettingsSyncAt;

    private Canvas focusCanvas;
    private RectTransform[] focusBars;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        AtlasBoardPhase14KAccessibilityController existing =
            UnityEngine.Object.FindAnyObjectByType<
                AtlasBoardPhase14KAccessibilityController>();

        if (existing != null)
        {
            instance = existing;
            return;
        }

        GameObject root =
            new GameObject(
                "AtlasBoard_Phase14K_Accessibility");

        DontDestroyOnLoad(
            root);

        instance =
            root.AddComponent<
                AtlasBoardPhase14KAccessibilityController>();
    }

    private void Awake()
    {
        if (instance != null &&
            instance != this)
        {
            Destroy(
                gameObject);

            return;
        }

        instance = this;

        RemoveLegacyWorldOwnershipBadges();
        CreateFocusRing();
    }

    private void Update()
    {
        HandleControllerNavigation();
        RefreshFocusRing();

        if (Time.unscaledTime >=
            nextSettingsSyncAt)
        {
            nextSettingsSyncAt =
                Time.unscaledTime +
                SettingsSyncInterval;

            SyncReducedMotionSettings();
        }
    }

    private void RemoveLegacyWorldOwnershipBadges()
    {
        RectTransform[] rects =
            UnityEngine.Object.FindObjectsByType<
                RectTransform>(
                    FindObjectsInactive.Include);

        foreach (RectTransform rect
                 in rects)
        {
            if (rect == null ||
                !string.Equals(
                    rect.name,
                    RemovedLegacyBadgeName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            Destroy(
                rect.gameObject);
        }
    }

    private void HandleControllerNavigation()
    {
#if ENABLE_INPUT_SYSTEM
        Gamepad gamepad =
            Gamepad.current;

        if (gamepad == null)
        {
            HideFocusRing();
            return;
        }

        Mouse mouse =
            Mouse.current;

        if (mouse != null)
        {
            Vector2 mouseDelta =
                mouse.delta.ReadValue();

            if (mouseDelta.sqrMagnitude >
                    4f ||
                mouse.leftButton
                    .wasPressedThisFrame ||
                mouse.rightButton
                    .wasPressedThisFrame)
            {
                gamepadFocusActive =
                    false;
            }
        }

        bool moved =
            TryReadNavigationDirection(
                gamepad,
                out Vector2 direction);

        bool submitted =
            gamepad.buttonSouth
                .wasPressedThisFrame;

        bool cancelled =
            gamepad.buttonEast
                .wasPressedThisFrame;

        if (!moved &&
            !submitted &&
            !cancelled)
        {
            return;
        }

        gamepadFocusActive =
            true;

        EnsureCurrentSelection();

        if (moved)
        {
            MoveSelection(
                direction);

            return;
        }

        GameObject selected =
            EventSystem.current != null
                ? EventSystem.current
                    .currentSelectedGameObject
                : null;

        if (selected == null ||
            EventSystem.current == null)
        {
            return;
        }

        BaseEventData eventData =
            new BaseEventData(
                EventSystem.current);

        if (submitted)
        {
            ExecuteEvents.Execute(
                selected,
                eventData,
                ExecuteEvents.submitHandler);

            return;
        }

        if (cancelled)
        {
            ExecuteEvents.Execute(
                selected,
                eventData,
                ExecuteEvents.cancelHandler);
        }
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private bool TryReadNavigationDirection(
        Gamepad gamepad,
        out Vector2 direction)
    {
        direction =
            Vector2.zero;

        if (gamepad.dpad.up
            .wasPressedThisFrame)
        {
            direction =
                Vector2.up;

            ResetStickRepeat(
                direction);

            return true;
        }

        if (gamepad.dpad.down
            .wasPressedThisFrame)
        {
            direction =
                Vector2.down;

            ResetStickRepeat(
                direction);

            return true;
        }

        if (gamepad.dpad.left
            .wasPressedThisFrame)
        {
            direction =
                Vector2.left;

            ResetStickRepeat(
                direction);

            return true;
        }

        if (gamepad.dpad.right
            .wasPressedThisFrame)
        {
            direction =
                Vector2.right;

            ResetStickRepeat(
                direction);

            return true;
        }

        Vector2 stick =
            gamepad.leftStick
                .ReadValue();

        Vector2 cardinal =
            CardinalDirection(
                stick);

        if (cardinal ==
            Vector2.zero)
        {
            lastStickDirection =
                Vector2.zero;

            nextStickRepeatAt =
                0f;

            return false;
        }

        if (cardinal !=
            lastStickDirection)
        {
            lastStickDirection =
                cardinal;

            nextStickRepeatAt =
                Time.unscaledTime +
                InitialRepeatDelay;

            direction =
                cardinal;

            return true;
        }

        if (Time.unscaledTime >=
            nextStickRepeatAt)
        {
            nextStickRepeatAt =
                Time.unscaledTime +
                RepeatDelay;

            direction =
                cardinal;

            return true;
        }

        return false;
    }

    private void ResetStickRepeat(
        Vector2 direction)
    {
        lastStickDirection =
            direction;

        nextStickRepeatAt =
            Time.unscaledTime +
            InitialRepeatDelay;
    }

    private static Vector2 CardinalDirection(
        Vector2 value)
    {
        if (value.magnitude <
            StickThreshold)
        {
            return Vector2.zero;
        }

        if (Mathf.Abs(
                value.x) >
            Mathf.Abs(
                value.y))
        {
            return value.x >= 0f
                ? Vector2.right
                : Vector2.left;
        }

        return value.y >= 0f
            ? Vector2.up
            : Vector2.down;
    }
#endif

    private void EnsureCurrentSelection()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        GameObject selected =
            EventSystem.current
                .currentSelectedGameObject;

        Selectable selectable =
            selected != null
                ? selected.GetComponent<
                    Selectable>()
                : null;

        if (selectable != null &&
            IsUsable(
                selectable))
        {
            return;
        }

        Selectable first =
            FindFirstSelectable();

        EventSystem.current
            .SetSelectedGameObject(
                first != null
                    ? first.gameObject
                    : null);
    }

    private void MoveSelection(
        Vector2 direction)
    {
        if (EventSystem.current == null)
        {
            return;
        }

        GameObject selected =
            EventSystem.current
                .currentSelectedGameObject;

        Selectable current =
            selected != null
                ? selected.GetComponent<
                    Selectable>()
                : null;

        if (current == null ||
            !IsUsable(
                current))
        {
            EnsureCurrentSelection();
            return;
        }

        Selectable next =
            direction == Vector2.up
                ? current.FindSelectableOnUp()
                : direction == Vector2.down
                    ? current.FindSelectableOnDown()
                    : direction == Vector2.left
                        ? current.FindSelectableOnLeft()
                        : current.FindSelectableOnRight();

        if (!IsUsable(
                next))
        {
            next =
                FindNearestSelectable(
                    current,
                    direction);
        }

        if (next != null)
        {
            EventSystem.current
                .SetSelectedGameObject(
                    next.gameObject);
        }
    }

    private Selectable FindFirstSelectable()
    {
        List<Selectable> candidates =
            GetNavigationCandidates();

        if (candidates.Count == 0)
        {
            return null;
        }

        candidates.Sort(
            CompareTopLeft);

        return candidates[0];
    }

    private Selectable FindNearestSelectable(
        Selectable current,
        Vector2 direction)
    {
        if (current == null)
        {
            return FindFirstSelectable();
        }

        RectTransform currentRect =
            current.transform as
                RectTransform;

        Vector2 currentCenter =
            ScreenCenter(
                currentRect);

        Selectable best =
            null;

        float bestScore =
            float.PositiveInfinity;

        foreach (Selectable candidate
                 in GetNavigationCandidates())
        {
            if (candidate == null ||
                candidate == current)
            {
                continue;
            }

            RectTransform candidateRect =
                candidate.transform as
                    RectTransform;

            Vector2 delta =
                ScreenCenter(
                    candidateRect) -
                currentCenter;

            if (delta.sqrMagnitude <
                1f)
            {
                continue;
            }

            Vector2 normalized =
                delta.normalized;

            float forward =
                Vector2.Dot(
                    normalized,
                    direction);

            if (forward <
                0.45f)
            {
                continue;
            }

            float distance =
                delta.magnitude;

            float anglePenalty =
                (1f -
                 forward) *
                1000f;

            float score =
                distance +
                anglePenalty;

            if (score <
                bestScore)
            {
                bestScore =
                    score;

                best =
                    candidate;
            }
        }

        return best;
    }

    private List<Selectable>
        GetNavigationCandidates()
    {
        Selectable[] all =
            UnityEngine.Object
                .FindObjectsByType<
                    Selectable>(
                        FindObjectsInactive.Exclude);

        List<Selectable> usable =
            new List<Selectable>();

        Transform tabletScope =
            ResolveActiveTabletRoot();

        if (tabletScope != null)
        {
            foreach (Selectable selectable
                     in all)
            {
                if (IsUsable(
                        selectable) &&
                    selectable.transform
                        .IsChildOf(
                            tabletScope))
                {
                    usable.Add(
                        selectable);
                }
            }

            if (usable.Count > 0)
            {
                return usable;
            }
        }

        Canvas bestCanvas =
            null;

        int bestOrder =
            int.MinValue;

        foreach (Selectable selectable
                 in all)
        {
            if (!IsUsable(
                    selectable))
            {
                continue;
            }

            Canvas canvas =
                selectable
                    .GetComponentInParent<
                        Canvas>()
                    ?.rootCanvas;

            if (canvas == null ||
                canvas == focusCanvas)
            {
                continue;
            }

            int order =
                canvas.sortingOrder;

            if (bestCanvas == null ||
                order >
                bestOrder)
            {
                bestCanvas =
                    canvas;

                bestOrder =
                    order;
            }
        }

        foreach (Selectable selectable
                 in all)
        {
            if (!IsUsable(
                    selectable))
            {
                continue;
            }

            Canvas canvas =
                selectable
                    .GetComponentInParent<
                        Canvas>()
                    ?.rootCanvas;

            if (bestCanvas == null ||
                canvas ==
                bestCanvas)
            {
                usable.Add(
                    selectable);
            }
        }

        return usable;
    }

    private Transform ResolveActiveTabletRoot()
    {
        TabletUIManager tablet =
            UnityEngine.Object.FindAnyObjectByType<
                TabletUIManager>();

        if (tablet == null)
        {
            return null;
        }

        GameObject root =
            ReadPrivateField<GameObject>(
                tablet,
                "tabletRoot");

        if (root == null ||
            !root.activeInHierarchy)
        {
            return null;
        }

        return root.transform;
    }

    private static bool IsUsable(
        Selectable selectable)
    {
        return selectable != null &&
               selectable.isActiveAndEnabled &&
               selectable.gameObject
                   .activeInHierarchy &&
               selectable.interactable;
    }

    private static int CompareTopLeft(
        Selectable left,
        Selectable right)
    {
        Vector2 leftCenter =
            ScreenCenter(
                left != null
                    ? left.transform as
                        RectTransform
                    : null);

        Vector2 rightCenter =
            ScreenCenter(
                right != null
                    ? right.transform as
                        RectTransform
                    : null);

        int yCompare =
            rightCenter.y
                .CompareTo(
                    leftCenter.y);

        if (yCompare != 0)
        {
            return yCompare;
        }

        return leftCenter.x
            .CompareTo(
                rightCenter.x);
    }

    private static Vector2 ScreenCenter(
        RectTransform rect)
    {
        Rect screen =
            GetScreenRect(
                rect);

        return screen.center;
    }

    private void CreateFocusRing()
    {
        GameObject canvasObject =
            new GameObject(
                "AtlasBoard_AccessibilityFocusCanvas",
                typeof(RectTransform),
                typeof(Canvas));

        canvasObject.transform.SetParent(
            transform,
            false);

        focusCanvas =
            canvasObject.GetComponent<
                Canvas>();

        focusCanvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        focusCanvas.overrideSorting =
            true;

        focusCanvas.sortingOrder =
            32750;

        focusBars =
            new RectTransform[4];

        for (int index = 0;
             index < focusBars.Length;
             index++)
        {
            GameObject bar =
                new GameObject(
                    "FocusBar_" +
                    index,
                    typeof(RectTransform),
                    typeof(Image));

            bar.transform.SetParent(
                canvasObject.transform,
                false);

            RectTransform rect =
                bar.GetComponent<
                    RectTransform>();

            rect.anchorMin =
                Vector2.zero;

            rect.anchorMax =
                Vector2.zero;

            rect.pivot =
                Vector2.zero;

            Image image =
                bar.GetComponent<
                    Image>();

            image.color =
                new Color(
                    1f,
                    1f,
                    1f,
                    0.96f);

            image.raycastTarget =
                false;

            focusBars[index] =
                rect;
        }

        HideFocusRing();
    }

    private void RefreshFocusRing()
    {
        if (!gamepadFocusActive ||
            EventSystem.current == null)
        {
            HideFocusRing();
            return;
        }

        GameObject selected =
            EventSystem.current
                .currentSelectedGameObject;

        Selectable selectable =
            selected != null
                ? selected.GetComponent<
                    Selectable>()
                : null;

        if (!IsUsable(
                selectable))
        {
            HideFocusRing();
            return;
        }

        RectTransform rect =
            selectable.transform as
                RectTransform;

        Rect screen =
            GetScreenRect(
                rect);

        const float padding =
            5f;

        const float thickness =
            3f;

        SetFocusBar(
            0,
            new Vector2(
                screen.xMin -
                padding,
                screen.yMax +
                padding),
            new Vector2(
                screen.width +
                padding * 2f,
                thickness));

        SetFocusBar(
            1,
            new Vector2(
                screen.xMin -
                padding,
                screen.yMin -
                padding -
                thickness),
            new Vector2(
                screen.width +
                padding * 2f,
                thickness));

        SetFocusBar(
            2,
            new Vector2(
                screen.xMin -
                padding -
                thickness,
                screen.yMin -
                padding),
            new Vector2(
                thickness,
                screen.height +
                padding * 2f));

        SetFocusBar(
            3,
            new Vector2(
                screen.xMax +
                padding,
                screen.yMin -
                padding),
            new Vector2(
                thickness,
                screen.height +
                padding * 2f));
    }

    private void SetFocusBar(
        int index,
        Vector2 position,
        Vector2 size)
    {
        if (focusBars == null ||
            index < 0 ||
            index >=
                focusBars.Length ||
            focusBars[index] == null)
        {
            return;
        }

        RectTransform rect =
            focusBars[index];

        rect.gameObject.SetActive(
            true);

        rect.anchoredPosition =
            position;

        rect.sizeDelta =
            size;
    }

    private void HideFocusRing()
    {
        if (focusBars == null)
        {
            return;
        }

        foreach (RectTransform bar
                 in focusBars)
        {
            if (bar != null &&
                bar.gameObject.activeSelf)
            {
                bar.gameObject.SetActive(
                    false);
            }
        }
    }

    private static Rect GetScreenRect(
        RectTransform rect)
    {
        if (rect == null)
        {
            return default;
        }

        Vector3[] corners =
            new Vector3[4];

        rect.GetWorldCorners(
            corners);

        Canvas canvas =
            rect.GetComponentInParent<
                Canvas>();

        Canvas root =
            canvas != null
                ? canvas.rootCanvas
                : null;

        Camera camera =
            root == null ||
            root.renderMode ==
                RenderMode.ScreenSpaceOverlay
                ? null
                : root.worldCamera;

        Vector2 bottomLeft =
            RectTransformUtility
                .WorldToScreenPoint(
                    camera,
                    corners[0]);

        Vector2 topRight =
            RectTransformUtility
                .WorldToScreenPoint(
                    camera,
                    corners[2]);

        return Rect.MinMaxRect(
            Mathf.Min(
                bottomLeft.x,
                topRight.x),
            Mathf.Min(
                bottomLeft.y,
                topRight.y),
            Mathf.Max(
                bottomLeft.x,
                topRight.x),
            Mathf.Max(
                bottomLeft.y,
                topRight.y));
    }

    private void SyncReducedMotionSettings()
    {
        AtlasBoardGameplaySettingsValues gameplay =
            AtlasBoardUserSettingsRuntime
                .Current
                .Gameplay;

        float rotation =
            AtlasBoardUserSettingsRuntime
                .PercentToThreeXMultiplier(
                    gameplay.CameraSensitivity);

        float zoom =
            AtlasBoardUserSettingsRuntime
                .PercentToThreeXMultiplier(
                    gameplay.CameraZoomSensitivity);

        float pan =
            AtlasBoardUserSettingsRuntime
                .PercentToThreeXMultiplier(
                    gameplay.CameraPanSensitivity);

        BoardCameraController camera =
            UnityEngine.Object.FindAnyObjectByType<
                BoardCameraController>(
                    FindObjectsInactive.Include);

        if (camera != null)
        {
            camera.ApplyUserSettings(
                rotation,
                zoom,
                pan,
                gameplay.ReduceCameraMotion);
        }

        BoardCameraCollision collision =
            UnityEngine.Object.FindAnyObjectByType<
                BoardCameraCollision>(
                    FindObjectsInactive.Include);

        if (collision != null)
        {
            collision.ApplyReduceCameraMotion(
                gameplay.ReduceCameraMotion);
        }
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
