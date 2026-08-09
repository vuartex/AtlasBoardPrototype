#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardStarterMapDataCreator
{
    private const string Root =
        "Assets/Project/Data";

    private const string MapsFolder =
        Root + "/Maps";

    private const string EconomyFolder =
        Root + "/Rules";

    private sealed class PropertySeed
    {
        public readonly string Name;
        public readonly string GroupId;
        public readonly string GroupName;

        public PropertySeed(
            string name,
            string groupId,
            string groupName)
        {
            Name = name;
            GroupId = groupId;
            GroupName = groupName;
        }
    }

    private struct EconomySeed
    {
        public int Price;
        public int Rent;
        public int DevelopmentCost;

        public EconomySeed(
            int price,
            int rent,
            int developmentCost)
        {
            Price = price;
            Rent = rent;
            DevelopmentCost =
                developmentCost;
        }
    }

    [MenuItem(
        "Atlas Board/Data/Create Starter Map Data")]
    public static void CreateStarterMapData()
    {
        EnsureFolder(Root);
        EnsureFolder(MapsFolder);
        EnsureFolder(EconomyFolder);

        BoardEconomyProfile economy =
            CreateOrUpdateEconomy();

        CreateOrUpdateMap(
            "map_turkey",
            "Türkiye",
            TurkeyProperties(),
            economy);

        CreateOrUpdateMap(
            "map_colorado",
            "Colorado",
            ColoradoProperties(),
            economy);

        CreateOrUpdateMap(
            "map_usa",
            "Amerika",
            UsaProperties(),
            economy);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "Atlas Board starter map data created/updated.");
    }

    private static BoardEconomyProfile
        CreateOrUpdateEconomy()
    {
        string path =
            EconomyFolder +
            "/EconomyProfile_Default.asset";

        BoardEconomyProfile profile =
            AssetDatabase.LoadAssetAtPath<
                BoardEconomyProfile>(path);

        if (profile == null)
        {
            profile =
                ScriptableObject.CreateInstance<
                    BoardEconomyProfile>();

            AssetDatabase.CreateAsset(
                profile,
                path);
        }

        profile.EditorConfigure(
            1500, // starting money
            200,  // pass Start
            120,  // tax
            100,  // bonus
            75,   // vacation
            0,    // travel fee
            1,    // rest-area skipped turns
            10,   // auction minimum bid
            10,   // small bid
            50,   // large bid
            new[] { 1, 2, 3, 5, 8 });

        EditorUtility.SetDirty(profile);

        return profile;
    }

    private static void CreateOrUpdateMap(
        string mapId,
        string displayName,
        PropertySeed[] properties,
        BoardEconomyProfile economy)
    {
        string path =
            MapsFolder +
            "/" +
            mapId +
            ".asset";

        BoardMapDefinition map =
            AssetDatabase.LoadAssetAtPath<
                BoardMapDefinition>(path);

        if (map == null)
        {
            map =
                ScriptableObject.CreateInstance<
                    BoardMapDefinition>();

            AssetDatabase.CreateAsset(
                map,
                path);
        }

        map.EditorSetIdentity(
            mapId,
            displayName,
            "Konum",
            "Bölge");

        map.EditorSetEconomyProfile(
            economy);

        map.EditorSetTiles(
            BuildTiles(properties));

        EditorUtility.SetDirty(map);
    }

    private static BoardTileDefinition[]
        BuildTiles(
            PropertySeed[] propertySeeds)
    {
        BoardTileDefinition[] tiles =
            new BoardTileDefinition[32];

        Dictionary<int, TileType> specialTypes =
            new Dictionary<int, TileType>
            {
                { 0, TileType.Start },
                { 4, TileType.Event },
                { 8, TileType.Auction },
                { 12, TileType.Tax },
                { 16, TileType.RestArea },
                { 20, TileType.Travel },
                { 24, TileType.Vacation },
                { 28, TileType.Bonus }
            };

        Dictionary<TileType, string>
            specialNames =
                new Dictionary<TileType, string>
                {
                    {
                        TileType.Start,
                        "Başlangıç"
                    },
                    {
                        TileType.Event,
                        "Etkinlik"
                    },
                    {
                        TileType.Auction,
                        "Açık Artırma"
                    },
                    {
                        TileType.Tax,
                        "Vergi"
                    },
                    {
                        TileType.RestArea,
                        "Dinlenme"
                    },
                    {
                        TileType.Travel,
                        "Seyahat"
                    },
                    {
                        TileType.Vacation,
                        "Tatil"
                    },
                    {
                        TileType.Bonus,
                        "Bonus"
                    }
                };

        EconomySeed[] economies =
            PropertyEconomySeeds();

        int propertyIndex = 0;

        for (int tileIndex = 0;
             tileIndex < tiles.Length;
             tileIndex++)
        {
            if (specialTypes.TryGetValue(
                    tileIndex,
                    out TileType specialType))
            {
                tiles[tileIndex] =
                    new BoardTileDefinition(
                        tileIndex,
                        specialType,
                        specialNames[specialType]);

                continue;
            }

            PropertySeed property =
                propertySeeds[propertyIndex];

            EconomySeed economy =
                economies[propertyIndex];

            tiles[tileIndex] =
                new BoardTileDefinition(
                    tileIndex,
                    TileType.City,
                    property.Name,
                    $"property_{propertyIndex + 1:00}",
                    property.GroupId,
                    property.GroupName,
                    economy.Price,
                    economy.Rent,
                    economy.DevelopmentCost);

            propertyIndex++;
        }

        return tiles;
    }

    private static EconomySeed[]
        PropertyEconomySeeds()
    {
        return new[]
        {
            new EconomySeed(100, 10, 50),
            new EconomySeed(110, 12, 50),
            new EconomySeed(120, 14, 50),

            new EconomySeed(140, 14, 60),
            new EconomySeed(150, 16, 60),
            new EconomySeed(160, 18, 60),

            new EconomySeed(180, 18, 70),
            new EconomySeed(190, 20, 70),
            new EconomySeed(200, 22, 70),

            new EconomySeed(220, 22, 80),
            new EconomySeed(230, 24, 80),
            new EconomySeed(240, 26, 80),

            new EconomySeed(260, 27, 100),
            new EconomySeed(275, 30, 100),
            new EconomySeed(290, 33, 100),

            new EconomySeed(310, 34, 120),
            new EconomySeed(325, 37, 120),
            new EconomySeed(340, 40, 120),

            new EconomySeed(360, 42, 150),
            new EconomySeed(380, 46, 150),
            new EconomySeed(400, 50, 150),

            new EconomySeed(420, 52, 180),
            new EconomySeed(450, 58, 180),
            new EconomySeed(480, 64, 180)
        };
    }

    private static PropertySeed[]
        TurkeyProperties()
    {
        return new[]
        {
            P("Edirne", 1),
            P("Tekirdağ", 1),
            P("Çanakkale", 1),

            P("Balıkesir", 2),
            P("Bursa", 2),
            P("Kocaeli", 2),

            P("Manisa", 3),
            P("Aydın", 3),
            P("İzmir", 3),

            P("Denizli", 4),
            P("Muğla", 4),
            P("Antalya", 4),

            P("Eskişehir", 5),
            P("Konya", 5),
            P("Ankara", 5),

            P("Samsun", 6),
            P("Ordu", 6),
            P("Trabzon", 6),

            P("Mersin", 7),
            P("Adana", 7),
            P("Gaziantep", 7),

            P("Diyarbakır", 8),
            P("Erzurum", 8),
            P("Van", 8)
        };
    }

    private static PropertySeed[]
        ColoradoProperties()
    {
        return new[]
        {
            P("Sterling", 1),
            P("Fort Morgan", 1),
            P("Greeley", 1),

            P("Loveland", 2),
            P("Longmont", 2),
            P("Fort Collins", 2),

            P("Boulder", 3),
            P("Lakewood", 3),
            P("Golden", 3),

            P("Aurora", 4),
            P("Denver", 4),
            P("Colorado Springs", 4),

            P("Pueblo", 5),
            P("Canon City", 5),
            P("Trinidad", 5),

            P("Grand Junction", 6),
            P("Montrose", 6),
            P("Durango", 6),

            P("Glenwood Springs", 7),
            P("Steamboat Springs", 7),
            P("Breckenridge", 7),

            P("Vail", 8),
            P("Aspen", 8),
            P("Estes Park", 8)
        };
    }

    private static PropertySeed[]
        UsaProperties()
    {
        return new[]
        {
            P("Maine", 1),
            P("Vermont", 1),
            P("New Hampshire", 1),

            P("Massachusetts", 2),
            P("Pennsylvania", 2),
            P("Virginia", 2),

            P("North Carolina", 3),
            P("Georgia", 3),
            P("Florida", 3),

            P("Ohio", 4),
            P("Michigan", 4),
            P("Illinois", 4),

            P("Oklahoma", 5),
            P("Louisiana", 5),
            P("Texas", 5),

            P("Arizona", 6),
            P("Utah", 6),
            P("Colorado", 6),

            P("Oregon", 7),
            P("Washington", 7),
            P("Nevada", 7),

            P("Alaska", 8),
            P("Hawaii", 8),
            P("California", 8)
        };
    }

    private static PropertySeed P(
        string name,
        int groupNumber)
    {
        return new PropertySeed(
            name,
            $"group_{groupNumber:00}",
            $"Bölge {groupNumber}");
    }

    private static void EnsureFolder(
        string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent =
            Path.GetDirectoryName(path)
                ?.Replace("\\", "/");

        string folderName =
            Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) &&
            !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(
            parent,
            folderName);
    }
}
#endif
