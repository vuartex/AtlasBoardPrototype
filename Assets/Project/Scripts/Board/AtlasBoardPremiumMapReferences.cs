using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Build-safe references to the two canonical premium BoardMapDefinition assets.
/// This is data only; existing MatchSetupManager owns selection and play.
/// </summary>
[CreateAssetMenu(menuName = "Atlas Board/Maps/Premium Map References")]
public sealed class AtlasBoardPremiumMapReferences : ScriptableObject
{
    [SerializeField] private BoardMapDefinition germany;
    [SerializeField] private BoardMapDefinition mexico;

    public IEnumerable<BoardMapDefinition> Maps
    {
        get
        {
            if (germany != null) yield return germany;
            if (mexico != null) yield return mexico;
        }
    }
}
