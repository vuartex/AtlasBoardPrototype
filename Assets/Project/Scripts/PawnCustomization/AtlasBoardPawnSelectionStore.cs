using System;
using Firebase.Auth;
using UnityEngine;

public static class AtlasBoardPawnSelectionStore
{
    private const string LegacyKeyPrefix =
        "AtlasBoard.PawnCosmetic.Slot.";
    private const string AccountKeyPrefix =
        "AtlasBoard.PawnCosmetic.Account.";

    public static event Action<int, string> SelectionChanged;

    public static string GetSelectedId(int playerSlotIndex, PawnCosmeticCatalog catalog)
    {
        string saved = PlayerPrefs.GetString(GetKey(playerSlotIndex), string.Empty);
        if (catalog != null && catalog.FindById(saved) != null) return saved;
        PawnCosmeticDefinition fallback =
            catalog != null ? catalog.GetDefaultForSlot(playerSlotIndex) : null;
        return fallback != null ? fallback.CosmeticId : string.Empty;
    }

    public static void SetSelectedId(int playerSlotIndex, string cosmeticId)
    {
        if (playerSlotIndex < 0 || string.IsNullOrWhiteSpace(cosmeticId)) return;
        PlayerPrefs.SetString(GetKey(playerSlotIndex), cosmeticId);
        PlayerPrefs.Save();
        SelectionChanged?.Invoke(playerSlotIndex, cosmeticId);
    }

    public static void ResetSlot(int playerSlotIndex)
    {
        PlayerPrefs.DeleteKey(GetKey(playerSlotIndex));
        PlayerPrefs.Save();
        SelectionChanged?.Invoke(playerSlotIndex, string.Empty);
    }

    private static string GetKey(int playerSlotIndex)
    {
        string identity = ResolveCurrentAccountId();
        if (!string.IsNullOrWhiteSpace(identity))
            return AccountKeyPrefix + identity + ".Slot." + Mathf.Max(0, playerSlotIndex);

        // Offline play keeps the historical key. Online identities NEVER
        // inherit it: Editor and standalone can share one Windows PlayerPrefs
        // namespace, so an unscoped slot key can leak another account's pawn.
        return LegacyKeyPrefix + Mathf.Max(0, playerSlotIndex);
    }

    private static string ResolveCurrentAccountId()
    {
        try
        {
            FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
            if (user != null && !string.IsNullOrWhiteSpace(user.UserId))
                return user.UserId;
        }
        catch { }

        AtlasBoardLobbyRuntimeBridge[] bridges =
            Resources.FindObjectsOfTypeAll<AtlasBoardLobbyRuntimeBridge>();
        foreach (AtlasBoardLobbyRuntimeBridge bridge in bridges)
            if (bridge != null && bridge.gameObject.scene.IsValid() &&
                !string.IsNullOrWhiteSpace(bridge.CurrentAccountId))
                return bridge.CurrentAccountId;

        return string.Empty;
    }
}
