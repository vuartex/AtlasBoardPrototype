using UnityEngine;

[DisallowMultipleComponent]
public class AtlasBoardPawnMotionAudio : MonoBehaviour
{
    [SerializeField, Min(0.001f)]
    private float movementThreshold = 0.01f;

    [SerializeField, Min(0.03f)]
    private float minimumStepInterval = 0.10f;

    private Vector3 lastPosition;
    private float nextStepTime;

    private void OnEnable()
    {
        lastPosition =
            transform.position;
    }

    private void Update()
    {
        float distance =
            Vector3.Distance(
                transform.position,
                lastPosition);

        if (distance >=
                movementThreshold &&
            Time.unscaledTime >=
                nextStepTime)
        {
            AtlasBoardAudioManager.Instance
                ?.PlayPawnMove();

            nextStepTime =
                Time.unscaledTime +
                minimumStepInterval;
        }

        lastPosition =
            transform.position;
    }
}
