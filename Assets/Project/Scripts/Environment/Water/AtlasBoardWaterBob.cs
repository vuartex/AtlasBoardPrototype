using UnityEngine;

public class AtlasBoardWaterBob : MonoBehaviour
{
    [Header("Vertical Bob")]
    [SerializeField, Min(0f)] private float bobHeight = 0.08f;
    [SerializeField, Min(0f)] private float bobSpeed = 0.75f;

    [Header("Gentle Rotation")]
    [SerializeField, Min(0f)] private float pitchDegrees = 1.5f;
    [SerializeField, Min(0f)] private float rollDegrees = 2.0f;
    [SerializeField, Min(0f)] private float rotationSpeed = 0.55f;

    [Header("Variation")]
    [SerializeField] private float phaseOffset = 0f;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;

    private void OnEnable()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
    }

    private void Update()
    {
        float t = Time.time + phaseOffset;

        transform.localPosition =
            baseLocalPosition +
            Vector3.up *
            (Mathf.Sin(t * bobSpeed) * bobHeight);

        float pitch =
            Mathf.Sin(t * rotationSpeed) *
            pitchDegrees;

        float roll =
            Mathf.Cos(t * rotationSpeed * 0.83f) *
            rollDegrees;

        transform.localRotation =
            baseLocalRotation *
            Quaternion.Euler(pitch, 0f, roll);
    }
}
