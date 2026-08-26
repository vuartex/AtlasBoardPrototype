using UnityEngine;

[CreateAssetMenu(
    fileName = "PawnMotionSet_New",
    menuName = "Atlas Board/Players/Pawn Motion Set")]
public class PawnMotionSetDefinition :
    ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string motionSetId;

    [SerializeField]
    private string displayName;

    [Header("Animator")]
    [SerializeField]
    private RuntimeAnimatorController animatorController;

    [Header("Available Motions")]
    [SerializeField]
    private bool hasIdle;

    [SerializeField]
    private bool hasWalk;

    [SerializeField]
    private bool hasSprint;

    [SerializeField]
    private bool hasSit;

    [SerializeField]
    private bool hasLookLeft;

    [SerializeField]
    private bool hasLookRight;

    [Header("Loop Information")]
    [SerializeField]
    private bool walkClipLoops;

    [SerializeField]
    private bool sprintClipLoops;

    [Header("Timing")]
    [SerializeField, Min(0.1f)]
    private float lookLeftDuration = 0.8f;

    [SerializeField, Min(0.1f)]
    private float lookRightDuration = 0.8f;

    [Header("Presentation")]
    [SerializeField]
    private float facingYawOffset;

    public string MotionSetId =>
        motionSetId;

    public string DisplayName =>
        displayName;

    public RuntimeAnimatorController
        AnimatorController =>
            animatorController;

    public bool HasIdle =>
        hasIdle;

    public bool HasWalk =>
        hasWalk;

    public bool HasSprint =>
        hasSprint;

    public bool HasSit =>
        hasSit;

    public bool HasLookLeft =>
        hasLookLeft;

    public bool HasLookRight =>
        hasLookRight;

    public bool WalkClipLoops =>
        walkClipLoops;

    public bool SprintClipLoops =>
        sprintClipLoops;

    public float LookLeftDuration =>
        Mathf.Max(
            0.1f,
            lookLeftDuration);

    public float LookRightDuration =>
        Mathf.Max(
            0.1f,
            lookRightDuration);

    public float FacingYawOffset =>
        facingYawOffset;

#if UNITY_EDITOR
    public void EditorConfigure(
        string id,
        string visibleName,
        RuntimeAnimatorController controller,
        bool idleAvailable,
        bool walkAvailable,
        bool sprintAvailable,
        bool sitAvailable,
        bool lookLeftAvailable,
        bool lookRightAvailable,
        bool walkLoops,
        bool sprintLoops,
        float leftDuration,
        float rightDuration,
        float yawOffset)
    {
        motionSetId =
            id;

        displayName =
            visibleName;

        animatorController =
            controller;

        hasIdle =
            idleAvailable;

        hasWalk =
            walkAvailable;

        hasSprint =
            sprintAvailable;

        hasSit =
            sitAvailable;

        hasLookLeft =
            lookLeftAvailable;

        hasLookRight =
            lookRightAvailable;

        walkClipLoops =
            walkLoops;

        sprintClipLoops =
            sprintLoops;

        lookLeftDuration =
            Mathf.Max(
                0.1f,
                leftDuration);

        lookRightDuration =
            Mathf.Max(
                0.1f,
                rightDuration);

        facingYawOffset =
            yawOffset;
    }
#endif
}
