using System;
using System.Collections;
using UnityEngine;

public class DiceVisualController : MonoBehaviour
{
    [Header("Dice")]
    [SerializeField]
    private Transform dieOne;

    [SerializeField]
    private Transform dieTwo;

    [Header("Face Rotations")]
    [Tooltip(
        "Local Euler rotation that puts each numbered face on TOP. " +
        "Element 0 = face 1, Element 5 = face 6.")]
    [SerializeField]
    private Vector3[] faceLocalEulerRotations =
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(0f, 0f, 90f),
        new Vector3(90f, 0f, 0f),
        new Vector3(-90f, 0f, 0f),
        new Vector3(0f, 0f, -90f),
        new Vector3(180f, 0f, 0f)
    };

    [Header("Animation")]
    [SerializeField, Min(0.1f)]
    private float rollDuration = 0.85f;

    [SerializeField, Min(0f)]
    private float resultHoldDuration = 0.45f;

    [SerializeField, Min(0f)]
    private float launchHeight = 1.1f;

    [SerializeField, Min(0f)]
    private float arcHeight = 0.45f;

    [SerializeField, Min(0f)]
    private float lateralScatter = 0.22f;

    [SerializeField, Min(0f)]
    private float spinTurns = 3.5f;

    [SerializeField]
    private bool hideWhenIdle = true;

    [Header("Debug Face Calibration")]
    [SerializeField, Range(1, 6)]
    private int debugPreviewFace = 1;

    private Vector3 dieOneRestLocalPosition;
    private Vector3 dieTwoRestLocalPosition;

    private Vector3 dieOneRestLocalScale;
    private Vector3 dieTwoRestLocalScale;

    private Coroutine rollCoroutine;
    private bool initialized;

    public bool IsRolling =>
        rollCoroutine != null;

    private void Awake()
    {
        Initialize();

        if (hideWhenIdle)
        {
            SetDiceVisible(false);
        }
    }

    public bool PlayRoll(
        int dieOneValue,
        int dieTwoValue,
        Action onCompleted)
    {
        Initialize();

        if (!ValidateDice())
        {
            onCompleted?.Invoke();
            return false;
        }

        dieOneValue =
            Mathf.Clamp(
                dieOneValue,
                1,
                6);

        dieTwoValue =
            Mathf.Clamp(
                dieTwoValue,
                1,
                6);

        if (rollCoroutine != null)
        {
            StopCoroutine(
                rollCoroutine);
        }

        rollCoroutine =
            StartCoroutine(
                RollRoutine(
                    dieOneValue,
                    dieTwoValue,
                    onCompleted));

        return true;
    }

    public void ResetForNewMatchSession()
    {
        Initialize();

        if (rollCoroutine != null)
        {
            StopCoroutine(rollCoroutine);
            rollCoroutine = null;
        }

        if (dieOne != null)
        {
            dieOne.localPosition = dieOneRestLocalPosition;
            dieOne.localScale = dieOneRestLocalScale;
            dieOne.localRotation = Quaternion.identity;
        }

        if (dieTwo != null)
        {
            dieTwo.localPosition = dieTwoRestLocalPosition;
            dieTwo.localScale = dieTwoRestLocalScale;
            dieTwo.localRotation = Quaternion.identity;
        }

        SetDiceVisible(false);
    }

    [ContextMenu("Preview Debug Face")]
    public void PreviewDebugFace()
    {
        Initialize();

        if (!ValidateDice())
        {
            return;
        }

        SetDiceVisible(true);

        Quaternion faceRotation =
            GetFaceRotation(
                debugPreviewFace);

        dieOne.localPosition =
            dieOneRestLocalPosition;

        dieTwo.localPosition =
            dieTwoRestLocalPosition;

        dieOne.localRotation =
            faceRotation;

        dieTwo.localRotation =
            faceRotation;

        dieOne.localScale =
            dieOneRestLocalScale;

        dieTwo.localScale =
            dieTwoRestLocalScale;

        Debug.Log(
            $"Dice preview face: {debugPreviewFace}. " +
            "Confirm that this exact number is on TOP.",
            this);
    }

    [ContextMenu("Hide Dice")]
    public void HideDice()
    {
        SetDiceVisible(false);
    }

    private IEnumerator RollRoutine(
        int dieOneValue,
        int dieTwoValue,
        Action onCompleted)
    {
        SetDiceVisible(true);

        dieOne.localScale =
            dieOneRestLocalScale;

        dieTwo.localScale =
            dieTwoRestLocalScale;

        Vector3 dieOneStartPosition =
            dieOneRestLocalPosition +
            Vector3.up *
            launchHeight +
            new Vector3(
                -lateralScatter,
                0f,
                lateralScatter);

        Vector3 dieTwoStartPosition =
            dieTwoRestLocalPosition +
            Vector3.up *
            launchHeight +
            new Vector3(
                lateralScatter,
                0f,
                -lateralScatter);

        dieOne.localPosition =
            dieOneStartPosition;

        dieTwo.localPosition =
            dieTwoStartPosition;

        Quaternion dieOneStartRotation =
            UnityEngine.Random.rotation;

        Quaternion dieTwoStartRotation =
            UnityEngine.Random.rotation;

        Quaternion dieOneTargetRotation =
            GetFaceRotation(
                dieOneValue);

        Quaternion dieTwoTargetRotation =
            GetFaceRotation(
                dieTwoValue);

        Vector3 dieOneSpinAxis =
            new Vector3(
                1f,
                0.73f,
                0.41f)
            .normalized;

        Vector3 dieTwoSpinAxis =
            new Vector3(
                0.51f,
                1f,
                0.82f)
            .normalized;

        float elapsed = 0f;

        while (elapsed < rollDuration)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    rollDuration);

            float smoothT =
                t * t *
                (3f - 2f * t);

            float arc =
                Mathf.Sin(
                    t * Mathf.PI) *
                arcHeight;

            dieOne.localPosition =
                Vector3.Lerp(
                    dieOneStartPosition,
                    dieOneRestLocalPosition,
                    smoothT) +
                Vector3.up * arc;

            dieTwo.localPosition =
                Vector3.Lerp(
                    dieTwoStartPosition,
                    dieTwoRestLocalPosition,
                    smoothT) +
                Vector3.up * arc;

            float remainingSpin =
                (1f - smoothT) *
                spinTurns *
                360f;

            Quaternion dieOneSpin =
                Quaternion.AngleAxis(
                    remainingSpin,
                    dieOneSpinAxis);

            Quaternion dieTwoSpin =
                Quaternion.AngleAxis(
                    -remainingSpin,
                    dieTwoSpinAxis);

            dieOne.localRotation =
                Quaternion.Slerp(
                    dieOneStartRotation *
                    dieOneSpin,
                    dieOneTargetRotation,
                    smoothT);

            dieTwo.localRotation =
                Quaternion.Slerp(
                    dieTwoStartRotation *
                    dieTwoSpin,
                    dieTwoTargetRotation,
                    smoothT);

            yield return null;
        }

        // Snap to exact final values. The visible result can never
        // disagree with the gameplay result passed by TurnManager.
        dieOne.localPosition =
            dieOneRestLocalPosition;

        dieTwo.localPosition =
            dieTwoRestLocalPosition;

        dieOne.localRotation =
            dieOneTargetRotation;

        dieTwo.localRotation =
            dieTwoTargetRotation;

        Debug.Log(
            $"Dice visual result: " +
            $"{dieOneValue} + {dieTwoValue} = " +
            $"{dieOneValue + dieTwoValue}.",
            this);

        if (resultHoldDuration > 0f)
        {
            yield return new WaitForSeconds(
                resultHoldDuration);
        }

        if (hideWhenIdle)
        {
            SetDiceVisible(false);
        }

        rollCoroutine = null;

        onCompleted?.Invoke();
    }

    private Quaternion GetFaceRotation(
        int value)
    {
        if (faceLocalEulerRotations == null ||
            faceLocalEulerRotations.Length < 6)
        {
            return Quaternion.identity;
        }

        int index =
            Mathf.Clamp(
                value - 1,
                0,
                5);

        return Quaternion.Euler(
            faceLocalEulerRotations[index]);
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        if (dieOne != null)
        {
            dieOneRestLocalPosition =
                dieOne.localPosition;

            dieOneRestLocalScale =
                dieOne.localScale;
        }

        if (dieTwo != null)
        {
            dieTwoRestLocalPosition =
                dieTwo.localPosition;

            dieTwoRestLocalScale =
                dieTwo.localScale;
        }
    }

    private bool ValidateDice()
    {
        if (dieOne != null &&
            dieTwo != null)
        {
            return true;
        }

        Debug.LogError(
            "DiceVisualController requires Die One and Die Two.",
            this);

        return false;
    }

    private void SetDiceVisible(
        bool visible)
    {
        if (dieOne != null)
        {
            dieOne.gameObject.SetActive(
                visible);
        }

        if (dieTwo != null)
        {
            dieTwo.gameObject.SetActive(
                visible);
        }
    }
}
