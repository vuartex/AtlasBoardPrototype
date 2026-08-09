using System;
using UnityEngine;

[Serializable]
public class BoardTileDefinition
{
    [Header("Placement")]
    [SerializeField, Range(0, 31)]
    private int tileIndex;

    [SerializeField]
    private TileType tileType;

    [Header("Display")]
    [SerializeField]
    private string displayName;

    [SerializeField, TextArea(1, 3)]
    private string description;

    [Header("Property Identity")]
    [SerializeField]
    private string propertyId;

    [SerializeField]
    private string groupId;

    [SerializeField]
    private string groupDisplayName;

    [Header("Property Economy")]
    [SerializeField, Min(0)]
    private int purchasePrice;

    [SerializeField, Min(0)]
    private int baseRent;

    [SerializeField, Min(0)]
    private int developmentCost;

    [Header("Optional Special-Tile Override")]
    [Tooltip(
        "0 means use the value from the active economy profile.")]
    [SerializeField]
    private int specialValueOverride;

    public int TileIndex => tileIndex;
    public TileType TileType => tileType;
    public string DisplayName => displayName;
    public string Description => description;
    public string PropertyId => propertyId;
    public string GroupId => groupId;
    public string GroupDisplayName =>
        groupDisplayName;
    public int PurchasePrice => purchasePrice;
    public int BaseRent => baseRent;
    public int DevelopmentCost =>
        developmentCost;
    public int SpecialValueOverride =>
        specialValueOverride;

    public bool IsProperty =>
        tileType == TileType.City;

    public BoardTileDefinition(
        int index,
        TileType type,
        string name,
        string propertyKey = "",
        string groupKey = "",
        string groupName = "",
        int price = 0,
        int rent = 0,
        int developCost = 0,
        int specialOverride = 0,
        string tileDescription = "")
    {
        tileIndex = index;
        tileType = type;
        displayName = name;
        propertyId = propertyKey;
        groupId = groupKey;
        groupDisplayName = groupName;
        purchasePrice = price;
        baseRent = rent;
        developmentCost = developCost;
        specialValueOverride =
            specialOverride;
        description = tileDescription;
    }
}
