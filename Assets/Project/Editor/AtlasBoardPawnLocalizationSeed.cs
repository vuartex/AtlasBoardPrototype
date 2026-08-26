#if UNITY_EDITOR
using System.Collections.Generic;

public static class AtlasBoardPawnLocalizationSeed
{
    public static void Append(
        List<AtlasBoardLocalizationDatabase.Entry> list)
    {
        if (list == null)
        {
            return;
        }

        Add(
            list,
            "lobby.pawn",
            "PAWN",
            "PİYON",
            "FICHA",
            "PION",
            "FIGUR",
            "말",
            "ФИШКА");
        Add(
            list,
            "pawn.customization.title",
            "PAWN CUSTOMIZATION",
            "PİYON ÖZELLEŞTİRME",
            "PERSONALIZAR FICHA",
            "PERSONNALISER LE PION",
            "FIGUR ANPASSEN",
            "말 꾸미기",
            "НАСТРОЙКА ФИШКИ");
        Add(
            list,
            "pawn.customization.player",
            "Player {0}",
            "Oyuncu {0}",
            "Jugador {0}",
            "Joueur {0}",
            "Spieler {0}",
            "플레이어 {0}",
            "Игрок {0}");
        Add(
            list,
            "pawn.customization.selection",
            "Pawn {0} / {1}",
            "Piyon {0} / {1}",
            "Ficha {0} / {1}",
            "Pion {0} / {1}",
            "Figur {0} / {1}",
            "말 {0} / {1}",
            "Фишка {0} / {1}");
        Add(
            list,
            "pawn.customization.use",
            "USE PAWN",
            "PİYONU KULLAN",
            "USAR FICHA",
            "UTILISER CE PION",
            "FIGUR VERWENDEN",
            "이 말 사용",
            "ИСПОЛЬЗОВАТЬ");
    }

    private static void Add(
        List<AtlasBoardLocalizationDatabase.Entry> list,
        string key,
        string en,
        string tr,
        string es,
        string fr,
        string de,
        string ko,
        string ru)
    {
        list.Add(
            new AtlasBoardLocalizationDatabase.Entry
            {
                key = key,
                en = en,
                tr = tr,
                es = es,
                fr = fr,
                de = de,
                ko = ko,
                ru = ru
            });
    }
}
#endif
