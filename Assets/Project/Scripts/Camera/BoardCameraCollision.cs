using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class BoardCameraCollision : MonoBehaviour
{
    [Header("Collision Target")]
    [SerializeField]
    private Transform collisionTarget;

    [SerializeField]
    private Vector3 targetLocalOffset =
        new Vector3(0f, 0.75f, 0f);

    [Header("Obstacle Detection")]
    [SerializeField]
    private LayerMask obstacleMask;

    [SerializeField, Min(0.05f)]
    private float sphereRadius = 0.45f;

    [SerializeField, Min(0f)]
    private float collisionPadding = 0.35f;

    [SerializeField, Min(0.5f)]
    private float minimumCameraDistance = 4.5f;

    [Header("Recovery")]
    [Tooltip(
        "Entering collision is immediate so the camera never passes " +
        "through geometry. Returning to the normal camera distance is smoothed.")]
    [SerializeField, Min(0.01f)]
    private float recoverySmoothTime = 0.18f;

    [Header("Diagnostics")]
    [SerializeField]
    private bool drawDebugGizmos;

    private float currentAllowedDistance = -1f;
    private float recoveryVelocity;
    private bool collisionActive;
    private RaycastHit lastHit;
    private bool reduceCameraMotion;

    public bool CollisionActive =>
        collisionActive;

    private void LateUpdate()
    {
        if (collisionTarget == null ||
            obstacleMask.value == 0)
        {
            return;
        }

        Vector3 targetPosition =
            collisionTarget.TransformPoint(
                targetLocalOffset);

        Vector3 desiredPosition =
            transform.position;

        Vector3 fromTarget =
            desiredPosition -
            targetPosition;

        float desiredDistance =
            fromTarget.magnitude;

        if (desiredDistance <=
            Mathf.Epsilon)
        {
            return;
        }

        Vector3 direction =
            fromTarget /
            desiredDistance;

        if (currentAllowedDistance < 0f)
        {
            currentAllowedDistance =
                desiredDistance;
        }

        // If the normal camera controller zooms in, never hold the camera
        // farther out because of an old collision distance.
        currentAllowedDistance =
            Mathf.Min(
                currentAllowedDistance,
                desiredDistance);

        bool hitObstacle =
            Physics.SphereCast(
                targetPosition,
                sphereRadius,
                direction,
                out RaycastHit hit,
                desiredDistance,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

        if (hitObstacle)
        {
            lastHit = hit;
            collisionActive = true;

            float safeDistance =
                Mathf.Max(
                    minimumCameraDistance,
                    hit.distance -
                    collisionPadding);

            safeDistance =
                Mathf.Min(
                    safeDistance,
                    desiredDistance);

            // Enter immediately. A smoothed entry can allow the camera to
            // spend several frames inside the obstacle.
            currentAllowedDistance =
                Mathf.Min(
                    currentAllowedDistance,
                    safeDistance);

            recoveryVelocity = 0f;
        }
        else
        {
            collisionActive = false;

            currentAllowedDistance =
                Mathf.SmoothDamp(
                    currentAllowedDistance,
                    desiredDistance,
                    ref recoveryVelocity,
                    GetRecoverySmoothTime());

            currentAllowedDistance =
                Mathf.Min(
                    currentAllowedDistance,
                    desiredDistance);
        }

        transform.position =
            targetPosition +
            direction *
            currentAllowedDistance;
    }

    public void ApplyReduceCameraMotion(
        bool enabled)
    {
        reduceCameraMotion =
            enabled;
    }

    private float GetRecoverySmoothTime()
    {
        if (!reduceCameraMotion)
        {
            return recoverySmoothTime;
        }

        return Mathf.Max(
            0.01f,
            recoverySmoothTime * 0.45f);
    }

    private void OnDisable()
    {
        currentAllowedDistance = -1f;
        recoveryVelocity = 0f;
        collisionActive = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos ||
            collisionTarget == null)
        {
            return;
        }

        Vector3 targetPosition =
            collisionTarget.TransformPoint(
                targetLocalOffset);

        Gizmos.DrawLine(
            targetPosition,
            transform.position);

        Gizmos.DrawWireSphere(
            targetPosition,
            sphereRadius);

        if (collisionActive)
        {
            Gizmos.DrawWireSphere(
                lastHit.point,
                sphereRadius);
        }
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        Transform newCollisionTarget,
        LayerMask newObstacleMask)
    {
        collisionTarget =
            newCollisionTarget;

        obstacleMask =
            newObstacleMask;

        targetLocalOffset =
            new Vector3(
                0f,
                0.75f,
                0f);

        sphereRadius = 0.45f;
        collisionPadding = 0.35f;
        minimumCameraDistance = 4.5f;
        recoverySmoothTime = 0.18f;
    }
#endif
}
