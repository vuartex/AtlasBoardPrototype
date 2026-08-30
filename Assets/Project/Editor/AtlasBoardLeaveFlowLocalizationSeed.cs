#if UNITY_EDITOR
using System.Collections.Generic;

public static class AtlasBoardLeaveFlowLocalizationSeed
{
    public const string Prefix = "leaveflow.";

    public static void Append(
        List<AtlasBoardLocalizationDatabase.Entry> list)
    {
        if (list == null)
        {
            return;
        }

        Add(list, "leaveflow.pause.title",
            "PAUSED", "DURAKLATILDI", "PAUSA", "PAUSE", "PAUSE", "일시 정지", "ПАУЗА");

        Add(list, "leaveflow.pause.resume",
            "RESUME", "DEVAM ET", "CONTINUAR", "REPRENDRE", "FORTSETZEN", "계속", "ПРОДОЛЖИТЬ");

        Add(list, "leaveflow.pause.settings",
            "SETTINGS", "AYARLAR", "AJUSTES", "PARAMÈTRES", "EINSTELLUNGEN", "설정", "НАСТРОЙКИ");

        Add(list, "leaveflow.pause.leave_match",
            "LEAVE MATCH", "MAÇTAN AYRIL", "SALIR DE LA PARTIDA", "QUITTER LA PARTIE", "SPIEL VERLASSEN", "게임 나가기", "ПОКИНУТЬ МАТЧ");

        Add(list, "leaveflow.pause.quit_game",
            "QUIT GAME", "OYUNDAN ÇIK", "SALIR DEL JUEGO", "QUITTER LE JEU", "SPIEL BEENDEN", "게임 종료", "ВЫЙТИ ИЗ ИГРЫ");

        Add(list, "leaveflow.confirm.title",
            "LEAVE CURRENT MATCH?", "MEVCUT MAÇTAN AYRIL?", "¿SALIR DE LA PARTIDA ACTUAL?", "QUITTER LA PARTIE EN COURS ?", "AKTUELLES SPIEL VERLASSEN?", "현재 게임에서 나갈까요?", "ПОКИНУТЬ ТЕКУЩИЙ МАТЧ?");

        Add(list, "leaveflow.confirm.body",
            "Progress in this match will be lost.", "Bu maçtaki ilerleme kaybolacak.", "El progreso de esta partida se perderá.", "La progression de cette partie sera perdue.", "Der Fortschritt in diesem Spiel geht verloren.", "이 게임의 진행 상황이 사라집니다.", "Прогресс в этом матче будет потерян.");

        Add(list, "leaveflow.confirm.cancel",
            "CANCEL", "İPTAL", "CANCELAR", "ANNULER", "ABBRECHEN", "취소", "ОТМЕНА");

        Add(list, "leaveflow.lobby.leave",
            "LEAVE LOBBY", "LOBİDEN AYRIL", "SALIR DE LA SALA", "QUITTER LE LOBBY", "LOBBY VERLASSEN", "로비 나가기", "ПОКИНУТЬ ЛОББИ");
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
