using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BoardGenerator : MonoBehaviour
{
    [Header("Map Data")]
    [SerializeField]
    private BoardMapDefinition activeMapDefinition;

    [Header("Board References")]
    [SerializeField]
    private Transform tilesParent;

    [Header("Tile Materials")]
    [SerializeField]
    private Material normalTileMaterial;

    [SerializeField]
    private Material startTileMaterial;

    [SerializeField]
    private Material cornerTileMaterial;

    [Header("Layout")]
    [SerializeField]
    private float boardEdge = 8f;

    [SerializeField]
    private float tileSpacing = 2f;

    [SerializeField]
    private float tileHeight = 0.35f;

    [SerializeField]
    private Vector3 tileScale =
        new Vector3(1.8f, 0.2f, 1.8f);

    public BoardMapDefinition ActiveMapDefinition =>
        activeMapDefinition;

    public string ActiveMapId =>
        activeMapDefinition != null
            ? activeMapDefinition.MapId
            : string.Empty;

    public BoardEconomyProfile ActiveEconomyProfile =>
        activeMapDefinition != null
            ? activeMapDefinition.EconomyProfile
            : null;

    public void SetActiveMapDefinition(
        BoardMapDefinition mapDefinition,
        bool applyImmediately = true)
    {
        if (mapDefinition == null)
        {
            Debug.LogError(
                "Cannot select a null BoardMapDefinition.",
                this);

            return;
        }

        activeMapDefinition =
            mapDefinition;

        if (applyImmediately)
        {
            ApplyActiveMapDataToExistingTiles();
        }
    }

    [ContextMenu(
        "Apply Active Map Data To Existing Tiles")]
    public void ApplyActiveMapDataToExistingTiles()
    {
        if (!ValidateActiveMapDefinition())
        {
            return;
        }

        FindOrCreateTilesParent();

        BoardTile[] existingTiles =
            tilesParent.GetComponentsInChildren<
                BoardTile>(true);

        if (existingTiles.Length != 32)
        {
            Debug.LogError(
                "Applying map data requires exactly 32 " +
                $"existing BoardTile objects. Found: " +
                $"{existingTiles.Length}.",
                this);

            return;
        }

        for (int index = 0;
             index < existingTiles.Length;
             index++)
        {
            BoardTile tile =
                existingTiles[index];

            BoardTileDefinition definition =
                activeMapDefinition
                    .GetTileDefinition(
                        tile.TileIndex);

            if (definition == null)
            {
                Debug.LogError(
                    $"{activeMapDefinition.DisplayName} has no " +
                    $"definition for tile index " +
                    $"{tile.TileIndex}.",
                    this);

                return;
            }
        }

        foreach (BoardTile tile
                 in existingTiles)
        {
            BoardTileDefinition definition =
                activeMapDefinition
                    .GetTileDefinition(
                        tile.TileIndex);

            tile.Configure(
                definition);

            tile.gameObject.name =
                GetTileObjectName(
                    definition.TileIndex,
                    definition.TileType);
        }

        RefreshBoardPath();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);

            foreach (BoardTile tile
                     in existingTiles)
            {
                EditorUtility.SetDirty(tile);
            }
        }
#endif

        Debug.Log(
            $"Applied map data: " +
            $"{activeMapDefinition.DisplayName}. " +
            "32 existing tiles updated.",
            this);
    }

    [ContextMenu("Generate 32 Tiles From Active Map")]
    public void GenerateBoard()
    {
        if (!ValidateActiveMapDefinition())
        {
            return;
        }

        FindOrCreateTilesParent();
        ClearExistingTiles();

        for (int index = 0;
             index < 32;
             index++)
        {
            CreateTile(index);
        }

        RefreshBoardPath();

        Debug.Log(
            $"32 board tiles generated from map: " +
            $"{activeMapDefinition.DisplayName}.",
            this);
    }

    private bool ValidateActiveMapDefinition()
    {
        if (activeMapDefinition == null)
        {
            Debug.LogError(
                "BoardGenerator has no Active Map Definition. " +
                "Assign a BoardMapDefinition asset first.",
                this);

            return false;
        }

        if (activeMapDefinition.Tiles == null ||
            activeMapDefinition.Tiles.Count != 32)
        {
            Debug.LogError(
                $"{activeMapDefinition.DisplayName} must contain " +
                "exactly 32 tile definitions.",
                activeMapDefinition);

            return false;
        }

        for (int index = 0;
             index < 32;
             index++)
        {
            BoardTileDefinition definition =
                activeMapDefinition
                    .GetTileDefinition(index);

            if (definition == null)
            {
                Debug.LogError(
                    $"{activeMapDefinition.DisplayName} is " +
                    $"missing tile index {index}.",
                    activeMapDefinition);

                return false;
            }
        }

        return true;
    }

    private void FindOrCreateTilesParent()
    {
        if (tilesParent != null)
        {
            return;
        }

        Transform existingTiles =
            transform.Find("Tiles");

        if (existingTiles != null)
        {
            tilesParent = existingTiles;
            return;
        }

        GameObject tilesObject =
            new GameObject("Tiles");

        tilesObject.transform.SetParent(
            transform);

        tilesObject.transform.localPosition =
            Vector3.zero;

        tilesObject.transform.localRotation =
            Quaternion.identity;

        tilesObject.transform.localScale =
            Vector3.one;

        tilesParent =
            tilesObject.transform;
    }

    private void CreateTile(
        int index)
    {
        BoardTileDefinition definition =
            activeMapDefinition
                .GetTileDefinition(index);

        GameObject tile =
            GameObject.CreatePrimitive(
                PrimitiveType.Cube);

        tile.name =
            GetTileObjectName(
                index,
                definition.TileType);

        tile.transform.SetParent(
            tilesParent);

        tile.transform.localPosition =
            GetTilePosition(index);

        tile.transform.localRotation =
            Quaternion.identity;

        tile.transform.localScale =
            tileScale;

        Renderer tileRenderer =
            tile.GetComponent<Renderer>();

        Material selectedMaterial =
            GetTileMaterial(index);

        if (selectedMaterial != null)
        {
            tileRenderer.sharedMaterial =
                selectedMaterial;
        }

        BoardTile boardTile =
            tile.AddComponent<BoardTile>();

        boardTile.Configure(
            definition);

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(
            tile,
            "Generate Board Tile");
#endif
    }

    private Vector3 GetTilePosition(
        int index)
    {
        float x;
        float z;

        if (index <= 8)
        {
            x =
                -boardEdge +
                index * tileSpacing;

            z =
                -boardEdge;
        }
        else if (index <= 16)
        {
            x =
                boardEdge;

            z =
                -boardEdge +
                (index - 8) *
                tileSpacing;
        }
        else if (index <= 24)
        {
            x =
                boardEdge -
                (index - 16) *
                tileSpacing;

            z =
                boardEdge;
        }
        else
        {
            x =
                -boardEdge;

            z =
                boardEdge -
                (index - 24) *
                tileSpacing;
        }

        return new Vector3(
            x,
            tileHeight,
            z);
    }

    private Material GetTileMaterial(
        int index)
    {
        if (index == 0 &&
            startTileMaterial != null)
        {
            return startTileMaterial;
        }

        if (IsCorner(index) &&
            cornerTileMaterial != null)
        {
            return cornerTileMaterial;
        }

        return normalTileMaterial;
    }

    private static bool IsCorner(
        int index)
    {
        return index == 0 ||
               index == 8 ||
               index == 16 ||
               index == 24;
    }

    private static string GetTileObjectName(
        int index,
        TileType type)
    {
        return
            $"Tile_{index:00}_{type}";
    }

    private void RefreshBoardPath()
    {
        BoardPath boardPath =
            GetComponent<BoardPath>();

        if (boardPath == null)
        {
            boardPath =
                FindAnyObjectByType<
                    BoardPath>();
        }

        if (boardPath != null)
        {
            boardPath.RefreshTiles();
        }
    }

    private void ClearExistingTiles()
    {
        if (tilesParent == null)
        {
            return;
        }

        for (int index =
                 tilesParent.childCount - 1;
             index >= 0;
             index--)
        {
            GameObject child =
                tilesParent
                    .GetChild(index)
                    .gameObject;

#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(
                child);
#else
            DestroyImmediate(child);
#endif
        }
    }
}
