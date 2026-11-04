using System.Collections.Generic;
using UnityEngine;

/// <summary>Build-safe data-only references to existing and event premium maps.</summary>
[CreateAssetMenu(menuName = "Atlas Board/Maps/Premium Map References")]
public sealed class AtlasBoardPremiumMapReferences : ScriptableObject
{
    [SerializeField] private BoardMapDefinition germany;
    [SerializeField] private BoardMapDefinition mexico;
    [SerializeField] private BoardMapDefinition world;

    public IEnumerable<BoardMapDefinition> Maps {
        get {
            if (germany != null) yield return germany;
            if (mexico != null) yield return mexico;
            if (world != null) yield return world;
        }
    }
}
