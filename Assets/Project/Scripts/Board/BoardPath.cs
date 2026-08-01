using System;
using UnityEngine;

public class BoardPath : MonoBehaviour
{
    [SerializeField] private Transform tilesParent;
    [SerializeField] private BoardTile[] orderedTiles = Array.Empty<BoardTile>();

    public int TileCount => orderedTiles.Length;

    private void Awake()
    {
        RefreshTiles();
    }

    [ContextMenu("Refresh Tile Path")]
    public void RefreshTiles()
    {
        if (tilesParent == null)
        {
            tilesParent = transform.Find("Tiles");
        }

        if (tilesParent == null)
        {
            Debug.LogError("BoardPath could not find the Tiles parent.", this);
            orderedTiles = Array.Empty<BoardTile>();
            return;
        }

        orderedTiles = tilesParent.GetComponentsInChildren<BoardTile>(true);

        Array.Sort(
            orderedTiles,
            (firstTile, secondTile) =>
                firstTile.TileIndex.CompareTo(secondTile.TileIndex));

        Debug.Log(
            $"Board path refreshed. Tile count: {orderedTiles.Length}",
            this);
    }

    public BoardTile GetTile(int index)
    {
        if (orderedTiles.Length == 0)
        {
            RefreshTiles();
        }

        if (orderedTiles.Length == 0)
        {
            return null;
        }

        int wrappedIndex =
            ((index % orderedTiles.Length) + orderedTiles.Length)
            % orderedTiles.Length;

        return orderedTiles[wrappedIndex];
    }
}