using UnityEngine;

public class BoardTile : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private int tileIndex;
    [SerializeField] private TileType tileType;
    [SerializeField] private string displayName;

    [Header("Economy")]
    [SerializeField] private bool purchasable;
    [SerializeField] private int purchasePrice;
    [SerializeField] private int baseRent;

    [Header("Ownership")]
    [SerializeField] private int ownerPlayerIndex = -1;

    [Header("Visual")]
    [SerializeField] private Renderer tileRenderer;
    [SerializeField] private Material originalMaterial;

    public int TileIndex => tileIndex;
    public TileType TileType => tileType;
    public string DisplayName => displayName;
    public bool Purchasable => purchasable;
    public int PurchasePrice => purchasePrice;
    public int BaseRent => baseRent;

    public bool IsOwned => ownerPlayerIndex >= 0;
    public int OwnerPlayerIndex => ownerPlayerIndex;

    private void Awake()
    {
        if (tileRenderer == null)
        {
            tileRenderer = GetComponent<Renderer>();
        }

        if (tileRenderer != null && originalMaterial == null)
        {
            originalMaterial = tileRenderer.sharedMaterial;
        }
    }

    public void Configure(
        int index,
        TileType type,
        string newDisplayName,
        bool canBePurchased,
        int newPurchasePrice,
        int newBaseRent)
    {
        tileIndex = index;
        tileType = type;
        displayName = newDisplayName;
        purchasable = canBePurchased;
        purchasePrice = newPurchasePrice;
        baseRent = newBaseRent;
        ownerPlayerIndex = -1;
    }

    public bool TrySetOwner(int playerIndex)
    {
        if (!purchasable || IsOwned || playerIndex < 0)
        {
            return false;
        }

        ownerPlayerIndex = playerIndex;
        return true;
    }

    public void ApplyOwnerMaterial(Material ownerMaterial)
    {
        if (ownerMaterial == null)
        {
            return;
        }

        if (tileRenderer == null)
        {
            tileRenderer = GetComponent<Renderer>();
        }

        if (tileRenderer == null)
        {
            return;
        }

        if (originalMaterial == null)
        {
            originalMaterial = tileRenderer.sharedMaterial;
        }

        tileRenderer.sharedMaterial = ownerMaterial;
    }

    public void ClearOwner()
    {
        ownerPlayerIndex = -1;

        if (tileRenderer == null)
        {
            tileRenderer = GetComponent<Renderer>();
        }

        if (tileRenderer != null && originalMaterial != null)
        {
            tileRenderer.sharedMaterial = originalMaterial;
        }
    }
}