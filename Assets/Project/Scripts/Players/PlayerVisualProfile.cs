using UnityEngine;

[CreateAssetMenu(
    fileName = "PlayerVisualProfile_New",
    menuName = "Atlas Board/Players/Visual Profile")]
public class PlayerVisualProfile : ScriptableObject
{
    [Header("Identity")]
    [SerializeField]
    private string profileId;

    [Header("Visual Identity")]
    [SerializeField]
    private Color uiColor = Color.white;

    [SerializeField]
    private Material ownershipMaterial;

    public string ProfileId => profileId;
    public Color UIColor => uiColor;
    public Material OwnershipMaterial => ownershipMaterial;
}