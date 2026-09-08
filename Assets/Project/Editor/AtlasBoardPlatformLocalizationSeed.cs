#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AtlasBoardPlatformLocalizationSeed
{
    private const string DatabasePath =
        "Assets/Project/Data/Localization/Localization_Default.asset";

    static AtlasBoardPlatformLocalizationSeed()
    {
        EditorApplication.delayCall +=
            MergeNow;
    }

    [MenuItem(
        "Atlas Board/Platform/Refresh Phase 11 Localization")]
    public static void MergeNow()
    {
        AtlasBoardLocalizationDatabase database =
            AssetDatabase.LoadAssetAtPath<
                AtlasBoardLocalizationDatabase>(
                    DatabasePath);

        if (database == null)
        {
            Debug.LogError(
                "AtlasBoard Phase 11 localization database not found at " +
                DatabasePath);
            return;
        }

        List<AtlasBoardLocalizationDatabase.Entry> list =
            database.Entries
                .Where(
                    entry =>
                        entry != null &&
                        !string.IsNullOrWhiteSpace(
                            entry.key) &&
                        !entry.key.StartsWith(
                            "platform.steam."))
                .Select(Clone)
                .ToList();

        list.Add(
            new AtlasBoardLocalizationDatabase.Entry
            {
                key =
                    "platform.steam.invite_friends",
                en =
                    "INVITE FRIENDS",
                tr =
                    "ARKADAŞ DAVET ET",
                es =
                    "INVITAR AMIGOS",
                fr =
                    "INVITER DES AMIS",
                de =
                    "FREUNDE EINLADEN",
                ko =
                    "친구 초대",
                ru =
                    "ПРИГЛАСИТЬ ДРУЗЕЙ"
            });

        database.EditorReplaceEntries(list);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "AtlasBoard Phase 11 localization refreshed. " +
            "Languages=EN/TR/ES/FR/DE/KO/RU.");
    }

    private static AtlasBoardLocalizationDatabase.Entry
        Clone(
            AtlasBoardLocalizationDatabase.Entry source)
    {
        return new AtlasBoardLocalizationDatabase.Entry
        {
            key = source.key,
            en = source.en,
            tr = source.tr,
            es = source.es,
            fr = source.fr,
            de = source.de,
            ko = source.ko,
            ru = source.ru
        };
    }
}
#endif
