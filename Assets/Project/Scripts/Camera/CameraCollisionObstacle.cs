using UnityEngine;

[DisallowMultipleComponent]
public class CameraCollisionObstacle : MonoBehaviour
{
    public enum GeneratedColliderKind
    {
        None = 0,
        Box = 1,
        Mesh = 2
    }

    [SerializeField, HideInInspector]
    private bool colliderAddedByAtlasBoard;

    [SerializeField, HideInInspector]
    private GeneratedColliderKind generatedColliderKind;

    public bool ColliderAddedByAtlasBoard =>
        colliderAddedByAtlasBoard;

    public GeneratedColliderKind ColliderKind =>
        generatedColliderKind;

#if UNITY_EDITOR
    // Backward-compatible overload for Camera Collision v1 setup.
    // The original setup script calls EditorSetColliderAdded(bool).
    public void EditorSetColliderAdded(
        bool value)
    {
        colliderAddedByAtlasBoard =
            value;

        generatedColliderKind =
            value
                ? GeneratedColliderKind.Box
                : GeneratedColliderKind.None;
    }

    // v1.0.1+ can explicitly record which collider type was generated.
    public void EditorSetColliderAdded(
        bool value,
        GeneratedColliderKind kind)
    {
        colliderAddedByAtlasBoard =
            value;

        generatedColliderKind =
            value
                ? kind
                : GeneratedColliderKind.None;
    }
#endif
}
