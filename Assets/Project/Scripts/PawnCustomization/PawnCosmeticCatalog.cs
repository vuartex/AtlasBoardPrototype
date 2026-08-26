using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PawnCosmeticCatalog_Default",
    menuName = "Atlas Board/Players/Pawn Cosmetic Catalog")]
public class PawnCosmeticCatalog :
    ScriptableObject
{
    [SerializeField]
    private List<PawnCosmeticDefinition>
        cosmetics =
            new List<PawnCosmeticDefinition>();

    public int Count =>
        cosmetics != null
            ? cosmetics.Count
            : 0;

    public IReadOnlyList<
        PawnCosmeticDefinition>
        Cosmetics =>
            cosmetics;

    public PawnCosmeticDefinition
        GetByIndex(
            int index)
    {
        if (cosmetics == null ||
            cosmetics.Count == 0)
        {
            return null;
        }

        int wrapped =
            ((index %
              cosmetics.Count) +
             cosmetics.Count) %
            cosmetics.Count;

        return cosmetics[
            wrapped];
    }

    public PawnCosmeticDefinition
        FindById(
            string cosmeticId)
    {
        if (cosmetics == null ||
            string.IsNullOrWhiteSpace(
                cosmeticId))
        {
            return null;
        }

        foreach (
            PawnCosmeticDefinition cosmetic
            in cosmetics)
        {
            if (cosmetic == null)
            {
                continue;
            }

            if (string.Equals(
                    cosmetic.CosmeticId,
                    cosmeticId,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                return cosmetic;
            }
        }

        return null;
    }

    public int IndexOf(
        string cosmeticId)
    {
        if (cosmetics == null ||
            string.IsNullOrWhiteSpace(
                cosmeticId))
        {
            return -1;
        }

        for (int index = 0;
             index < cosmetics.Count;
             index++)
        {
            PawnCosmeticDefinition cosmetic =
                cosmetics[index];

            if (cosmetic == null)
            {
                continue;
            }

            if (string.Equals(
                    cosmetic.CosmeticId,
                    cosmeticId,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    public PawnCosmeticDefinition
        GetDefaultForSlot(
            int playerSlotIndex)
    {
        if (Count == 0)
        {
            return null;
        }

        // Alternate through the discovered catalog
        // so the four default pawns are visibly distinct.
        int[] preferred =
        {
            0,
            1,
            2,
            3
        };

        int index =
            playerSlotIndex >= 0 &&
            playerSlotIndex <
            preferred.Length
                ? preferred[
                    playerSlotIndex]
                : playerSlotIndex;

        return GetByIndex(
            index);
    }

#if UNITY_EDITOR
    public void EditorReplace(
        List<PawnCosmeticDefinition> entries)
    {
        cosmetics =
            entries ??
            new List<PawnCosmeticDefinition>();
    }
#endif
}
