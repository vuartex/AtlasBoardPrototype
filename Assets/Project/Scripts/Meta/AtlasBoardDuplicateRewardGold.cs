using System;

/// <summary>
/// Deterministic fallback Gold for an item already owned when granted by a
/// promotion or seasonal event. This is not used for normal store purchases.
/// Server RTDB Rules independently verify the SAME fixed values.
/// </summary>
public static class AtlasBoardDuplicateRewardGold
{
    public const int Pawn = 200;
    public const int Dice = 300;
    public const int Board = 800;
    public const int Map = 1200;
    public const int ProfileFrame = 250;
    public const int Emote = 120;

    public static int ForCatalogItemType(string itemType)
    {
        switch (itemType ?? "")
        {
            case "pawn": return Pawn;
            case "dice_skin": return Dice;
            case "board_theme": return Board;
            case "map": return Map;
            case "profile_frame": return ProfileFrame;
            case "emote": return Emote;
            default: return 0;
        }
    }

    public static int ForItemId(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;
        if (itemId.StartsWith("map_", StringComparison.Ordinal)) return Map;
        if (itemId.StartsWith("board_", StringComparison.Ordinal)) return Board;
        if (itemId.StartsWith("dice_", StringComparison.Ordinal)) return Dice;
        if (itemId.StartsWith("frame_", StringComparison.Ordinal)) return ProfileFrame;
        if (itemId.StartsWith("emote_", StringComparison.Ordinal)) return Emote;
        if (itemId.StartsWith("ghost_", StringComparison.Ordinal) ||
            itemId.StartsWith("vehicle_", StringComparison.Ordinal)) return Pawn;
        return 0;
    }
}
