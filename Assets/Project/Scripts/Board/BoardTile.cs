using UnityEngine;

public class BoardTile : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField]
    private int tileIndex;

    [SerializeField]
    private TileType tileType;

    [SerializeField]
    private string displayName;

    [Header("Map Data")]
    [SerializeField]
    private string description;

    [SerializeField]
    private string propertyId;

    [SerializeField]
    private string groupId;

    [SerializeField]
    private string groupDisplayName;

    [SerializeField]
    private int specialValueOverride;

    [Header("Economy")]
    [SerializeField]
    private bool purchasable;

    [SerializeField]
    private int purchasePrice;

    [SerializeField]
    private int baseRent;

    [SerializeField]
    private int developmentCost;

    [Header("Ownership")]
    [SerializeField]
    private int ownerPlayerIndex = -1;

    [Header("Visual")]
    [SerializeField]
    private Renderer tileRenderer;

    [SerializeField]
    private Material originalMaterial;

    public int TileIndex => tileIndex;
    public TileType TileType => tileType;
    public string DisplayName => displayName;
    public string Description => description;
    public string PropertyId => propertyId;
    public string GroupId => groupId;
    public string GroupDisplayName =>
        groupDisplayName;
    public int SpecialValueOverride =>
        specialValueOverride;

    public bool Purchasable => purchasable;
    public int PurchasePrice => purchasePrice;
    public int BaseRent => baseRent;
    public int DevelopmentCost =>
        developmentCost;

    public bool IsOwned =>
        ownerPlayerIndex >= 0;

    public int OwnerPlayerIndex =>
        ownerPlayerIndex;

    private void Awake()
    {
        EnsureRendererReferences();
    }

    public void Configure(
        BoardTileDefinition definition)
    {
        if (definition == null)
        {
            Debug.LogError(
                "BoardTile cannot be configured from a null " +
                "BoardTileDefinition.",
                this);

            return;
        }

        EnsureRendererReferences();

        // Map changes only happen before a match. Clear any
        // previous prototype ownership visual/state so the tile
        // starts clean with its new definition.
        ClearOwner();

        tileIndex =
            definition.TileIndex;

        tileType =
            definition.TileType;

        displayName =
            definition.DisplayName;

        description =
            definition.Description;

        propertyId =
            definition.PropertyId;

        groupId =
            definition.GroupId;

        groupDisplayName =
            definition.GroupDisplayName;

        purchasable =
            definition.IsProperty;

        purchasePrice =
            purchasable
                ? definition.PurchasePrice
                : 0;

        baseRent =
            purchasable
                ? definition.BaseRent
                : 0;

        developmentCost =
            purchasable
                ? definition.DevelopmentCost
                : 0;

        specialValueOverride =
            definition.SpecialValueOverride;

        ownerPlayerIndex = -1;
    }

    // Legacy-compatible overload. Existing scripts that still call
    // Configure directly continue to compile while map data is being
    // migrated system by system.
    public void Configure(
        int index,
        TileType type,
        string newDisplayName,
        bool canBePurchased,
        int newPurchasePrice,
        int newBaseRent)
    {
        EnsureRendererReferences();
        ClearOwner();

        tileIndex = index;
        tileType = type;
        displayName = newDisplayName;

        description = string.Empty;
        propertyId = string.Empty;
        groupId = string.Empty;
        groupDisplayName = string.Empty;
        specialValueOverride = 0;

        purchasable = canBePurchased;

        purchasePrice =
            canBePurchased
                ? newPurchasePrice
                : 0;

        baseRent =
            canBePurchased
                ? newBaseRent
                : 0;

        developmentCost = 0;
        ownerPlayerIndex = -1;
    }

    public bool TrySetOwner(
        int playerIndex)
    {
        if (!purchasable ||
            IsOwned ||
            playerIndex < 0)
        {
            return false;
        }

        ownerPlayerIndex = playerIndex;
        return true;
    }

    public void ApplyOwnerMaterial(
        Material ownerMaterial)
    {
        if (ownerMaterial == null)
        {
            return;
        }

        EnsureRendererReferences();

        if (tileRenderer == null)
        {
            return;
        }

        tileRenderer.sharedMaterial =
            ownerMaterial;
    }

    public void ClearOwner()
    {
        ownerPlayerIndex = -1;

        EnsureRendererReferences();

        if (tileRenderer != null &&
            originalMaterial != null)
        {
            tileRenderer.sharedMaterial =
                originalMaterial;
        }
    }

    private void EnsureRendererReferences()
    {
        if (tileRenderer == null)
        {
            tileRenderer =
                GetComponent<Renderer>();
        }

        if (tileRenderer != null &&
            originalMaterial == null)
        {
            originalMaterial =
                tileRenderer.sharedMaterial;
        }
    }
}
