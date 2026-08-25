using UnityEngine;

[DisallowMultipleComponent]
public class AtlasBoardDiceMotionAudio : MonoBehaviour
{
    [SerializeField]
    private Transform[] watchedDice;

    [SerializeField, Min(0.01f)]
    private float rotationThresholdDegrees = 0.25f;

    [SerializeField, Min(0.0001f)]
    private float positionThreshold = 0.0005f;

    [SerializeField, Min(0.05f)]
    private float retriggerDelay = 0.35f;

    private Vector3[] lastPositions;
    private Quaternion[] lastRotations;

    private bool wasMoving;
    private int stableFrames;
    private float nextAllowedTime;

    private void Awake()
    {
        ResolveDice();
        CaptureState();
    }

    private void OnEnable()
    {
        ResolveDice();
        CaptureState();
    }

    private void Update()
    {
        if (watchedDice == null ||
            watchedDice.Length == 0)
        {
            return;
        }

        bool moving = false;

        for (int i = 0;
             i < watchedDice.Length;
             i++)
        {
            Transform die =
                watchedDice[i];

            if (die == null)
            {
                continue;
            }

            float positionDelta =
                Vector3.Distance(
                    die.localPosition,
                    lastPositions[i]);

            float rotationDelta =
                Quaternion.Angle(
                    die.localRotation,
                    lastRotations[i]);

            if (positionDelta >
                    positionThreshold ||
                rotationDelta >
                    rotationThresholdDegrees)
            {
                moving = true;
            }

            lastPositions[i] =
                die.localPosition;

            lastRotations[i] =
                die.localRotation;
        }

        if (moving)
        {
            stableFrames = 0;

            if (!wasMoving &&
                Time.unscaledTime >=
                    nextAllowedTime)
            {
                AtlasBoardAudioManager.Instance
                    ?.PlayDice();

                nextAllowedTime =
                    Time.unscaledTime +
                    retriggerDelay;
            }

            wasMoving = true;
        }
        else if (wasMoving)
        {
            stableFrames++;

            if (stableFrames >= 3)
            {
                wasMoving = false;
                stableFrames = 0;
            }
        }
    }

    private void ResolveDice()
    {
        if (watchedDice != null &&
            watchedDice.Length > 0)
        {
            return;
        }

        int childCount =
            transform.childCount;

        watchedDice =
            new Transform[
                childCount];

        for (int i = 0;
             i < childCount;
             i++)
        {
            watchedDice[i] =
                transform.GetChild(i);
        }
    }

    private void CaptureState()
    {
        if (watchedDice == null)
        {
            return;
        }

        lastPositions =
            new Vector3[
                watchedDice.Length];

        lastRotations =
            new Quaternion[
                watchedDice.Length];

        for (int i = 0;
             i < watchedDice.Length;
             i++)
        {
            Transform die =
                watchedDice[i];

            if (die == null)
            {
                continue;
            }

            lastPositions[i] =
                die.localPosition;

            lastRotations[i] =
                die.localRotation;
        }
    }
}
