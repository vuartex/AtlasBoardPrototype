using UnityEngine;

[CreateAssetMenu(
    fileName = "PawnCosmetic_New",
    menuName = "Atlas Board/Players/Pawn Cosmetic")]
public class PawnCosmeticDefinition :
    ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string cosmeticId;

    [SerializeField]
    private string displayName;

    [Header("Model")]
    [SerializeField]
    private GameObject prefab;

    [Header("Board Presentation")]
    [SerializeField, Min(0.25f)]
    private float desiredWorldHeight =
        1.15f;

    [SerializeField]
    private Vector3 rotationOffset;

    [Header("Motion")]
    [SerializeField]
    private PawnMotionSetDefinition
        defaultMotionSet;

    public string CosmeticId =>
        cosmeticId;

    public string DisplayName =>
        displayName;

    public GameObject Prefab =>
        prefab;

    public float DesiredWorldHeight =>
        Mathf.Max(
            0.25f,
            desiredWorldHeight);

    public Vector3 RotationOffset =>
        rotationOffset;

    public PawnMotionSetDefinition
        DefaultMotionSet =>
            defaultMotionSet;

#if UNITY_EDITOR
    public void EditorConfigure(
        string id,
        string visibleName,
        GameObject modelPrefab,
        float desiredHeight,
        Vector3 modelRotation)
    {
        cosmeticId =
            id;

        displayName =
            visibleName;

        prefab =
            modelPrefab;

        desiredWorldHeight =
            Mathf.Max(
                0.25f,
                desiredHeight);

        rotationOffset =
            modelRotation;
    }

    public void EditorSetDefaultMotionSet(
        PawnMotionSetDefinition motionSet)
    {
        defaultMotionSet =
            motionSet;
    }
#endif
}
