using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class BoardCameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField]
    private Transform orbitTarget;

    [Header("View")]
    [SerializeField, Range(20f, 70f)]
    private float fixedPitch = 42f;

    [SerializeField]
    private float startingYaw = 45f;

    [SerializeField, Min(1f)]
    private float startingDistance = 24.6f;

    [Header("Zoom")]
    [SerializeField, Min(1f)]
    private float minimumDistance = 15f;

    [SerializeField, Min(1f)]
    private float maximumDistance = 34f;

    [SerializeField, Min(0.0001f)]
    private float zoomSensitivity = 0.0035f;

    [SerializeField, Range(0.01f, 0.5f)]
    private float zoomSmoothTime = 0.10f;

    [Header("Rotate")]
    [SerializeField, Min(0.0001f)]
    private float rotationSensitivity = 0.055f;

    [SerializeField, Range(0.01f, 0.5f)]
    private float rotationSmoothTime = 0.08f;

    [Header("Pan")]
    [SerializeField, Min(0.0001f)]
    private float panSensitivity = 0.0025f;

    [SerializeField, Min(0f)]
    private float maximumPanDistance = 4f;

    [SerializeField, Range(0.01f, 0.5f)]
    private float panSmoothTime = 0.09f;

    [Header("Controls")]
    [SerializeField]
    private bool enableMouseWheelZoom = true;

    [SerializeField]
    private bool enableRightDragRotate = true;

    [SerializeField]
    private bool enableMiddleDragPan = true;

    [Header("Debug")]
    [SerializeField]
    private bool logInputActivity;

    private float targetYaw;
    private float currentYaw;
    private float yawVelocity;

    private float targetDistance;
    private float currentDistance;
    private float distanceVelocity;

    private Vector3 baseTargetPosition;
    private Vector3 targetPanOffset;
    private Vector3 currentPanOffset;
    private Vector3 panVelocity;

    private bool initialized;

    // User settings are multipliers over the tuned Inspector values.
    // The player never sees the raw AtlasBoard sensitivity numbers.
    private float userRotationMultiplier = 1f;
    private float userZoomMultiplier = 1f;
    private float userPanMultiplier = 1f;
    private bool reduceCameraMotion;

    private void Awake()
    {
        EnsureOrbitTarget();
        InitializeFromCurrentCamera();

        Debug.Log(
            $"BoardCameraController active. " +
            $"Target: {(orbitTarget != null ? orbitTarget.name : "NONE")}.",
            this);
    }

    private void LateUpdate()
    {
        if (orbitTarget == null)
        {
            EnsureOrbitTarget();

            if (orbitTarget == null)
            {
                return;
            }
        }

        if (!initialized)
        {
            InitializeFromCurrentCamera();
        }

        Vector2 mouseDelta =
            GetMouseDelta();

        HandleZoom();
        HandleRotateAndPan(
            mouseDelta);

        if (WasResetPressed())
        {
            ResetView();
        }

        SmoothCameraState();
        ApplyCameraTransform();
    }

    private void EnsureOrbitTarget()
    {
        if (orbitTarget != null)
        {
            return;
        }

        BoardGenerator boardGenerator =
            FindAnyObjectByType<
                BoardGenerator>();

        if (boardGenerator != null)
        {
            orbitTarget =
                boardGenerator.transform;
        }
    }

    [ContextMenu("Capture Current Camera As Default")]
    public void CaptureCurrentCameraAsDefault()
    {
        EnsureOrbitTarget();

        if (orbitTarget == null)
        {
            Debug.LogError(
                "BoardCameraController could not find BoardRoot / BoardGenerator.",
                this);

            return;
        }

        baseTargetPosition =
            orbitTarget.position;

        startingYaw =
            transform.eulerAngles.y;

        fixedPitch =
            NormalizePitch(
                transform.eulerAngles.x);

        startingDistance =
            Vector3.Distance(
                transform.position,
                baseTargetPosition);

        targetYaw =
            startingYaw;

        currentYaw =
            startingYaw;

        targetDistance =
            startingDistance;

        currentDistance =
            startingDistance;

        targetPanOffset =
            Vector3.zero;

        currentPanOffset =
            Vector3.zero;

        initialized = true;
    }

    [ContextMenu("Reset View")]
    public void ResetView()
    {
        targetYaw =
            startingYaw;

        targetDistance =
            Mathf.Clamp(
                startingDistance,
                minimumDistance,
                maximumDistance);

        targetPanOffset =
            Vector3.zero;
    }

    private void InitializeFromCurrentCamera()
    {
        if (orbitTarget == null)
        {
            return;
        }

        initialized = true;

        baseTargetPosition =
            orbitTarget.position;

        startingYaw =
            transform.eulerAngles.y;

        fixedPitch =
            NormalizePitch(
                transform.eulerAngles.x);

        startingDistance =
            Vector3.Distance(
                transform.position,
                baseTargetPosition);

        targetYaw =
            startingYaw;

        currentYaw =
            startingYaw;

        targetDistance =
            startingDistance;

        currentDistance =
            startingDistance;

        targetPanOffset =
            Vector3.zero;

        currentPanOffset =
            Vector3.zero;
    }

    private void HandleZoom()
    {
        if (!enableMouseWheelZoom)
        {
            return;
        }

        float scroll =
            GetScrollY();

        if (Mathf.Abs(scroll) <
            0.001f)
        {
            return;
        }

        targetDistance -=
            scroll *
            zoomSensitivity *
            userZoomMultiplier;

        targetDistance =
            Mathf.Clamp(
                targetDistance,
                minimumDistance,
                maximumDistance);

        if (logInputActivity)
        {
            Debug.Log(
                $"Camera zoom input: {scroll:0.##} | " +
                $"target distance: {targetDistance:0.##}",
                this);
        }
    }

    private void HandleRotateAndPan(
        Vector2 mouseDelta)
    {
        bool shiftHeld =
            IsShiftHeld();

        bool rightHeld =
            IsRightMouseHeld();

        bool middleHeld =
            IsMiddleMouseHeld();

        if (enableRightDragRotate &&
            rightHeld &&
            !shiftHeld)
        {
            targetYaw +=
                mouseDelta.x *
                rotationSensitivity *
                userRotationMultiplier;

            if (logInputActivity &&
                Mathf.Abs(mouseDelta.x) >
                0.01f)
            {
                Debug.Log(
                    $"Camera rotate input: {mouseDelta.x:0.##}",
                    this);
            }
        }

        bool shouldPan =
            (enableMiddleDragPan &&
             middleHeld) ||
            (rightHeld &&
             shiftHeld);

        if (!shouldPan)
        {
            return;
        }

        Vector3 cameraRight =
            transform.right;

        cameraRight.y = 0f;

        if (cameraRight.sqrMagnitude >
            0.0001f)
        {
            cameraRight.Normalize();
        }

        Vector3 cameraForward =
            transform.forward;

        cameraForward.y = 0f;

        if (cameraForward.sqrMagnitude >
            0.0001f)
        {
            cameraForward.Normalize();
        }

        Vector3 movement =
            (-cameraRight *
             mouseDelta.x -
             cameraForward *
             mouseDelta.y) *
            panSensitivity *
            userPanMultiplier;

        targetPanOffset +=
            movement;

        if (maximumPanDistance > 0f &&
            targetPanOffset.magnitude >
                maximumPanDistance)
        {
            targetPanOffset =
                targetPanOffset.normalized *
                maximumPanDistance;
        }

        if (logInputActivity &&
            mouseDelta.sqrMagnitude >
            0.01f)
        {
            Debug.Log(
                $"Camera pan input: {mouseDelta}",
                this);
        }
    }

    private void SmoothCameraState()
    {
        currentYaw =
            Mathf.SmoothDampAngle(
                currentYaw,
                targetYaw,
                ref yawVelocity,
                GetMotionSmoothTime(
                    rotationSmoothTime),
                Mathf.Infinity,
                Time.unscaledDeltaTime);

        currentDistance =
            Mathf.SmoothDamp(
                currentDistance,
                targetDistance,
                ref distanceVelocity,
                GetMotionSmoothTime(
                    zoomSmoothTime),
                Mathf.Infinity,
                Time.unscaledDeltaTime);

        currentPanOffset =
            Vector3.SmoothDamp(
                currentPanOffset,
                targetPanOffset,
                ref panVelocity,
                GetMotionSmoothTime(
                    panSmoothTime),
                Mathf.Infinity,
                Time.unscaledDeltaTime);
    }

    public void ApplyUserSettings(
        float rotationMultiplier,
        float zoomMultiplier,
        float panMultiplier,
        bool newReduceCameraMotion)
    {
        userRotationMultiplier =
            Mathf.Max(
                0.05f,
                rotationMultiplier);

        userZoomMultiplier =
            Mathf.Max(
                0.05f,
                zoomMultiplier);

        userPanMultiplier =
            Mathf.Max(
                0.05f,
                panMultiplier);

        reduceCameraMotion =
            newReduceCameraMotion;
    }

    private float GetMotionSmoothTime(
        float baseline)
    {
        if (!reduceCameraMotion)
        {
            return baseline;
        }

        // Reduced camera motion means less lingering/inertial travel.
        // It does not alter the user's chosen sensitivity multiplier.
        return Mathf.Max(
            0.01f,
            baseline * 0.35f);
    }

    private void ApplyCameraTransform()
    {
        Vector3 targetPosition =
            baseTargetPosition +
            currentPanOffset;

        Quaternion rotation =
            Quaternion.Euler(
                fixedPitch,
                currentYaw,
                0f);

        Vector3 cameraPosition =
            targetPosition -
            rotation *
            Vector3.forward *
            currentDistance;

        transform.SetPositionAndRotation(
            cameraPosition,
            rotation);
    }

    private static float NormalizePitch(
        float eulerX)
    {
        if (eulerX > 180f)
        {
            eulerX -= 360f;
        }

        return Mathf.Clamp(
            eulerX,
            20f,
            70f);
    }

    private Vector2 GetMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null)
        {
            return Vector2.zero;
        }

        return Mouse.current.delta.ReadValue();
#else
        return new Vector2(
            Input.GetAxisRaw("Mouse X"),
            Input.GetAxisRaw("Mouse Y"));
#endif
    }

    private float GetScrollY()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null)
        {
            return 0f;
        }

        return Mouse.current.scroll.ReadValue().y;
#else
        return Input.mouseScrollDelta.y * 120f;
#endif
    }

    private bool IsRightMouseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null &&
               Mouse.current.rightButton.isPressed;
#else
        return Input.GetMouseButton(1);
#endif
    }

    private bool IsMiddleMouseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null &&
               Mouse.current.middleButton.isPressed;
#else
        return Input.GetMouseButton(2);
#endif
    }

    private bool IsShiftHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
        {
            return false;
        }

        return Keyboard.current.leftShiftKey.isPressed ||
               Keyboard.current.rightShiftKey.isPressed;
#else
        return Input.GetKey(KeyCode.LeftShift) ||
               Input.GetKey(KeyCode.RightShift);
#endif
    }

    private bool WasResetPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               Keyboard.current.homeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Home);
#endif
    }
}
