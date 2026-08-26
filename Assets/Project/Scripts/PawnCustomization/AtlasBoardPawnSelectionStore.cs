using System;
using UnityEngine;

public static class AtlasBoardPawnSelectionStore
{
    private const string KeyPrefix =
        "AtlasBoard.PawnCosmetic.Slot.";

    public static event Action<int, string>
        SelectionChanged;

    public static string GetSelectedId(
        int playerSlotIndex,
        PawnCosmeticCatalog catalog)
    {
        string key =
            GetKey(
                playerSlotIndex);

        string saved =
            PlayerPrefs.GetString(
                key,
                string.Empty);

        if (catalog != null &&
            catalog.FindById(
                saved) != null)
        {
            return saved;
        }

        PawnCosmeticDefinition fallback =
            catalog != null
                ? catalog
                    .GetDefaultForSlot(
                        playerSlotIndex)
                : null;

        return fallback != null
            ? fallback.CosmeticId
            : string.Empty;
    }

    public static void SetSelectedId(
        int playerSlotIndex,
        string cosmeticId)
    {
        if (playerSlotIndex < 0 ||
            string.IsNullOrWhiteSpace(
                cosmeticId))
        {
            return;
        }

        PlayerPrefs.SetString(
            GetKey(
                playerSlotIndex),
            cosmeticId);

        PlayerPrefs.Save();

        SelectionChanged?.Invoke(
            playerSlotIndex,
            cosmeticId);
    }

    public static void ResetSlot(
        int playerSlotIndex)
    {
        PlayerPrefs.DeleteKey(
            GetKey(
                playerSlotIndex));

        PlayerPrefs.Save();

        SelectionChanged?.Invoke(
            playerSlotIndex,
            string.Empty);
    }

    private static string GetKey(
        int playerSlotIndex)
    {
        return KeyPrefix +
               Mathf.Max(
                   0,
                   playerSlotIndex);
    }
}
