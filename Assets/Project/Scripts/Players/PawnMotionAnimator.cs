using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-1900)]
[DisallowMultipleComponent]
public class PawnMotionAnimator :
    MonoBehaviour
{
    public const string StateIdle =
        "Idle";

    public const string StateWalk =
        "Walk";

    public const string StateSprint =
        "Sprint";

    public const string StateSit =
        "Sit";

    public const string StateLookLeft =
        "LookLeft";

    public const string StateLookRight =
        "LookRight";

    [Header("References")]
    [SerializeField]
    private PlayerPawnMover pawnMover;

    [SerializeField]
    private PawnCosmeticApplier cosmeticApplier;

    [Header("Facing")]
    [SerializeField, Min(90f)]
    private float rotationSpeedDegrees =
        900f;

    [Header("Animation Blend")]
    [SerializeField, Range(0f, 0.35f)]
    private float crossFadeSeconds =
        0.10f;

    [Header("Movement Polish")]
    [SerializeField, Range(0f, 0.15f)]
    private float walkBobHeight =
        0.035f;

    [SerializeField, Range(0f, 0.20f)]
    private float sprintBobHeight =
        0.055f;

    [SerializeField, Min(0.1f)]
    private float walkBobFrequency =
        5.5f;

    [SerializeField, Min(0.1f)]
    private float sprintBobFrequency =
        8f;

    [SerializeField, Min(0.1f)]
    private float landingSettleSpeed =
        0.6f;

    [Header("Seated Idle")]
    [SerializeField, Min(0.25f)]
    private float idleLookMinimumDelay =
        2.5f;

    [SerializeField, Min(0.25f)]
    private float idleLookMaximumDelay =
        5.5f;

    private Transform motionRoot;
    private GameObject currentVisual;
    private Animator animator;
    private PawnMotionSetDefinition motionSet;

    private Vector3 baseMotionLocalPosition;
    private Quaternion targetLocalRotation =
        Quaternion.identity;

    private Coroutine idleLookRoutine;

    private bool isMoving;
    private bool isSprinting;
    private float bobPhase;

    private void Awake()
    {
        ResolveReferences();

        if (cosmeticApplier != null &&
            cosmeticApplier.CurrentVisual != null)
        {
            BindCosmeticVisual(
                cosmeticApplier.CurrentVisual,
                cosmeticApplier.VisualMotionRoot,
                cosmeticApplier
                    .CurrentCosmetic
                    ?.DefaultMotionSet);
        }
    }

    private void Update()
    {
        UpdateFacing();
        UpdateMovementBob();
        KeepNonLoopingMovementAlive();
    }

    private void OnDisable()
    {
        StopIdleLookRoutine();
        ResetMotionRootPosition();
    }

    public void BindCosmeticVisual(
        GameObject visual,
        Transform newMotionRoot,
        PawnMotionSetDefinition newMotionSet)
    {
        StopIdleLookRoutine();

        currentVisual =
            visual;

        motionRoot =
            newMotionRoot;

        motionSet =
            newMotionSet;

        animator = null;
        isMoving = false;
        isSprinting = false;
        bobPhase = 0f;

        if (motionRoot != null)
        {
            motionRoot.localPosition =
                Vector3.zero;

            motionRoot.localRotation =
                Quaternion.identity;

            baseMotionLocalPosition =
                motionRoot.localPosition;

            targetLocalRotation =
                motionRoot.localRotation;
        }

        if (currentVisual == null ||
            motionSet == null ||
            motionSet.AnimatorController == null)
        {
            return;
        }

        animator =
            currentVisual
                .GetComponentInChildren<Animator>(
                    true);

        if (animator == null)
        {
            animator =
                currentVisual.AddComponent<Animator>();

            Debug.LogWarning(
                $"{currentVisual.name} did not contain an Animator. " +
                "A runtime Animator was added. If animation does not deform " +
                "the model, verify the Kenney FBX Rig/Avatar import settings.",
                currentVisual);
        }

        animator.applyRootMotion =
            false;

        animator.cullingMode =
            AnimatorCullingMode.AlwaysAnimate;

        animator.updateMode =
            AnimatorUpdateMode.Normal;

        animator.runtimeAnimatorController =
            motionSet.AnimatorController;

        animator.Rebind();
        animator.Update(0f);

        SetLandedPose();
    }

    public void ClearCosmeticVisual()
    {
        StopIdleLookRoutine();

        isMoving = false;
        isSprinting = false;
        bobPhase = 0f;

        ResetMotionRootPosition();

        currentVisual = null;
        animator = null;
        motionSet = null;
        motionRoot = null;
    }

    public void BeginMovement(
        bool useSprint)
    {
        ResolveReferences();
        StopIdleLookRoutine();

        isMoving = true;
        isSprinting =
            useSprint;

        bobPhase = 0f;

        if (animator == null)
        {
            return;
        }

        string state =
            useSprint &&
            motionSet != null &&
            motionSet.HasSprint
                ? StateSprint
                : StateWalk;

        PlayState(
            state,
            crossFadeSeconds);
    }

    public void SetFacingDirection(
        Vector3 worldDirection)
    {
        if (motionRoot == null)
        {
            return;
        }

        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude <
            0.0001f)
        {
            return;
        }

        Transform orientationParent =
            motionRoot.parent != null
                ? motionRoot.parent
                : transform;

        Vector3 localDirection =
            orientationParent
                .InverseTransformDirection(
                    worldDirection.normalized);

        localDirection.y = 0f;

        if (localDirection.sqrMagnitude <
            0.0001f)
        {
            return;
        }

        Quaternion facing =
            Quaternion.LookRotation(
                localDirection.normalized,
                Vector3.up);

        float yawOffset =
            motionSet != null
                ? motionSet.FacingYawOffset
                : 0f;

        targetLocalRotation =
            facing *
            Quaternion.Euler(
                0f,
                yawOffset,
                0f);
    }

    public void EndMovement()
    {
        isMoving = false;
        isSprinting = false;
        bobPhase = 0f;

        SetLandedPose();
    }

    public void SetLandedPose()
    {
        isMoving = false;
        isSprinting = false;

        ResetMotionRootPosition();

        if (animator != null)
        {
            PlayState(
                StateSit,
                crossFadeSeconds);
        }

        StartIdleLookRoutine();
    }

    public void StopMotion()
    {
        isMoving = false;
        isSprinting = false;
        bobPhase = 0f;

        StopIdleLookRoutine();
        ResetMotionRootPosition();

        if (animator != null)
        {
            PlayState(
                StateSit,
                crossFadeSeconds);
        }
    }

    private void UpdateFacing()
    {
        if (motionRoot == null)
        {
            return;
        }

        motionRoot.localRotation =
            Quaternion.RotateTowards(
                motionRoot.localRotation,
                targetLocalRotation,
                rotationSpeedDegrees *
                Time.deltaTime);
    }

    private void UpdateMovementBob()
    {
        if (motionRoot == null)
        {
            return;
        }

        if (!isMoving)
        {
            motionRoot.localPosition =
                Vector3.MoveTowards(
                    motionRoot.localPosition,
                    baseMotionLocalPosition,
                    landingSettleSpeed *
                    Time.deltaTime);

            return;
        }

        float frequency =
            isSprinting
                ? sprintBobFrequency
                : walkBobFrequency;

        float height =
            isSprinting
                ? sprintBobHeight
                : walkBobHeight;

        bobPhase +=
            Time.deltaTime *
            frequency *
            Mathf.PI *
            2f;

        float bob =
            Mathf.Abs(
                Mathf.Sin(
                    bobPhase)) *
            height;

        motionRoot.localPosition =
            baseMotionLocalPosition +
            Vector3.up *
            bob;
    }

    private void KeepNonLoopingMovementAlive()
    {
        if (!isMoving ||
            animator == null ||
            motionSet == null ||
            animator.IsInTransition(0))
        {
            return;
        }

        bool clipLoops =
            isSprinting &&
            motionSet.HasSprint
                ? motionSet.SprintClipLoops
                : motionSet.WalkClipLoops;

        if (clipLoops)
        {
            return;
        }

        AnimatorStateInfo stateInfo =
            animator.GetCurrentAnimatorStateInfo(
                0);

        string expectedState =
            isSprinting &&
            motionSet.HasSprint
                ? StateSprint
                : StateWalk;

        if (!stateInfo.IsName(
                GetFullStateName(
                    expectedState)))
        {
            return;
        }

        if (stateInfo.normalizedTime >=
            0.98f)
        {
            animator.Play(
                GetFullStateName(
                    expectedState),
                0,
                0f);
        }
    }

    private IEnumerator IdleLookRoutine()
    {
        while (!isMoving &&
               animator != null &&
               motionSet != null)
        {
            float minimum =
                Mathf.Min(
                    idleLookMinimumDelay,
                    idleLookMaximumDelay);

            float maximum =
                Mathf.Max(
                    idleLookMinimumDelay,
                    idleLookMaximumDelay);

            yield return
                new WaitForSeconds(
                    Random.Range(
                        minimum,
                        maximum));

            if (isMoving ||
                animator == null ||
                motionSet == null)
            {
                yield break;
            }

            bool canLookLeft =
                motionSet.HasLookLeft;

            bool canLookRight =
                motionSet.HasLookRight;

            if (!canLookLeft &&
                !canLookRight)
            {
                continue;
            }

            bool lookLeft =
                canLookLeft &&
                (!canLookRight ||
                 Random.value < 0.5f);

            string state =
                lookLeft
                    ? StateLookLeft
                    : StateLookRight;

            float duration =
                lookLeft
                    ? motionSet.LookLeftDuration
                    : motionSet.LookRightDuration;

            PlayState(
                state,
                crossFadeSeconds);

            yield return
                new WaitForSeconds(
                    duration);

            if (isMoving ||
                animator == null)
            {
                yield break;
            }

            PlayState(
                StateSit,
                crossFadeSeconds);
        }

        idleLookRoutine = null;
    }

    private void StartIdleLookRoutine()
    {
        StopIdleLookRoutine();

        if (!isActiveAndEnabled ||
            animator == null ||
            motionSet == null ||
            (!motionSet.HasLookLeft &&
             !motionSet.HasLookRight))
        {
            return;
        }

        idleLookRoutine =
            StartCoroutine(
                IdleLookRoutine());
    }

    private void StopIdleLookRoutine()
    {
        if (idleLookRoutine == null)
        {
            return;
        }

        StopCoroutine(
            idleLookRoutine);

        idleLookRoutine = null;
    }

    private void PlayState(
        string stateName,
        float fadeSeconds)
    {
        if (animator == null ||
            animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (fadeSeconds <= 0.001f)
        {
            animator.Play(
                GetFullStateName(
                    stateName),
                0,
                0f);
        }
        else
        {
            animator.CrossFadeInFixedTime(
                GetFullStateName(
                    stateName),
                fadeSeconds,
                0,
                0f);
        }
    }

    private static string GetFullStateName(
        string stateName)
    {
        return "Base Layer." +
               stateName;
    }

    private void ResetMotionRootPosition()
    {
        if (motionRoot == null)
        {
            return;
        }

        motionRoot.localPosition =
            baseMotionLocalPosition;
    }

    private void ResolveReferences()
    {
        if (pawnMover == null)
        {
            pawnMover =
                GetComponent<PlayerPawnMover>();
        }

        if (cosmeticApplier == null)
        {
            cosmeticApplier =
                GetComponent<PawnCosmeticApplier>();
        }
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        PlayerPawnMover mover,
        PawnCosmeticApplier applier)
    {
        pawnMover =
            mover;

        cosmeticApplier =
            applier;
    }
#endif
}
