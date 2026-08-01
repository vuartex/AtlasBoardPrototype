using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BoardGenerator : MonoBehaviour
{
    [Header("Board References")]
    [SerializeField] private Transform tilesParent;

    [Header("Tile Materials")]
    [SerializeField] private Material normalTileMaterial;
    [SerializeField] private Material startTileMaterial;
    [SerializeField] private Material cornerTileMaterial;

    [Header("Layout")]
    [SerializeField] private float boardEdge = 8f;
    [SerializeField] private float tileSpacing = 2f;
    [SerializeField] private float tileHeight = 0.35f;
    [SerializeField] private Vector3 tileScale = new(1.8f, 0.2f, 1.8f);

    [ContextMenu("Generate 32 Tiles")]
    public void GenerateBoard()
    {
        FindOrCreateTilesParent();
        ClearExistingTiles();

        for (int index = 0; index < 32; index++)
        {
            CreateTile(index);
        }

        Debug.Log("32 board tiles generated successfully.");
    }

    private void FindOrCreateTilesParent()
    {
        if (tilesParent != null)
        {
            return;
        }

        Transform existingTiles = transform.Find("Tiles");

        if (existingTiles != null)
        {
            tilesParent = existingTiles;
            return;
        }

        GameObject tilesObject = new("Tiles");
        tilesObject.transform.SetParent(transform);
        tilesObject.transform.localPosition = Vector3.zero;
        tilesObject.transform.localRotation = Quaternion.identity;
        tilesObject.transform.localScale = Vector3.one;

        tilesParent = tilesObject.transform;
    }

    private void CreateTile(int index)
    {
        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);

        TileType tileType = GetPrototypeTileType(index);

        tile.name = GetTileObjectName(index, tileType);
        tile.transform.SetParent(tilesParent);
        tile.transform.localPosition = GetTilePosition(index);
        tile.transform.localRotation = Quaternion.identity;
        tile.transform.localScale = tileScale;

        Renderer tileRenderer = tile.GetComponent<Renderer>();
        Material selectedMaterial = GetTileMaterial(index);

        if (selectedMaterial != null)
        {
            tileRenderer.sharedMaterial = selectedMaterial;
        }

        BoardTile boardTile = tile.AddComponent<BoardTile>();

        bool purchasable = tileType == TileType.City;
        int purchasePrice = purchasable ? 100 + index * 10 : 0;
        int baseRent = purchasable ? 10 + index * 2 : 0;

        boardTile.Configure(
            index,
            tileType,
            GetPrototypeDisplayName(index, tileType),
            purchasable,
            purchasePrice,
            baseRent);

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(tile, "Generate Board Tile");
#endif
    }

    private Vector3 GetTilePosition(int index)
    {
        float x;
        float z;

        if (index <= 8)
        {
            x = -boardEdge + index * tileSpacing;
            z = -boardEdge;
        }
        else if (index <= 16)
        {
            x = boardEdge;
            z = -boardEdge + (index - 8) * tileSpacing;
        }
        else if (index <= 24)
        {
            x = boardEdge - (index - 16) * tileSpacing;
            z = boardEdge;
        }
        else
        {
            x = -boardEdge;
            z = boardEdge - (index - 24) * tileSpacing;
        }

        return new Vector3(x, tileHeight, z);
    }

    private TileType GetPrototypeTileType(int index)
    {
        return index switch
        {
            0 => TileType.Start,
            4 => TileType.Event,
            8 => TileType.Auction,
            12 => TileType.Tax,
            16 => TileType.RestArea,
            20 => TileType.Travel,
            24 => TileType.Vacation,
            28 => TileType.Bonus,
            _ => TileType.City
        };
    }

    private string GetPrototypeDisplayName(int index, TileType type)
    {
        return type switch
        {
            TileType.Start => "Başlangıç",
            TileType.Event => "Etkinlik Kartı",
            TileType.Tax => "Vergi",
            TileType.Auction => "Açık Artırma",
            TileType.Travel => "Seyahat Merkezi",
            TileType.Vacation => "Tatil Bölgesi",
            TileType.RestArea => "Dinlenme Alanı",
            TileType.Bonus => "Bonus",
            _ => $"Test Şehri {index:00}"
        };
    }

    private Material GetTileMaterial(int index)
    {
        if (index == 0 && startTileMaterial != null)
        {
            return startTileMaterial;
        }

        if (IsCorner(index) && cornerTileMaterial != null)
        {
            return cornerTileMaterial;
        }

        return normalTileMaterial;
    }

    private static bool IsCorner(int index)
    {
        return index == 0 ||
               index == 8 ||
               index == 16 ||
               index == 24;
    }

    private static string GetTileObjectName(int index, TileType type)
    {
        return $"Tile_{index:00}_{type}";
    }

    private void ClearExistingTiles()
    {
        if (tilesParent == null)
        {
            return;
        }

        for (int index = tilesParent.childCount - 1; index >= 0; index--)
        {
            GameObject child = tilesParent.GetChild(index).gameObject;

#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(child);
#else
            DestroyImmediate(child);
#endif
        }
    }
}