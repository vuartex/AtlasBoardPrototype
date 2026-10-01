#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class AtlasBoardEditorMenuOrganizer
{
    private const string AssetsRoot =
        "Assets/Project";

    private static readonly string[,] PrefixMappings =
    {
        { "\"Atlas Board/UI/", "\"Atlas Board/Design/UI & UX/UI/" },
        { "\"Atlas Board/UX/", "\"Atlas Board/Design/UI & UX/UX/" },
        { "\"Atlas Board/Settings/", "\"Atlas Board/Design/UI & UX/Settings/" },
        { "\"Atlas Board/Audio/", "\"Atlas Board/Design/Audio/" },
        { "\"Atlas Board/Camera/", "\"Atlas Board/Design/Board & Presentation/Camera/" },
        { "\"Atlas Board/Environment/", "\"Atlas Board/Design/Board & Presentation/Environment/" },
        { "\"Atlas Board/Pawns/", "\"Atlas Board/Design/Board & Presentation/Pawns/" },

        { "\"Atlas Board/Online/", "\"Atlas Board/Online & Backend/Online/" },
        { "\"Atlas Board/Firebase/", "\"Atlas Board/Online & Backend/Firebase/" },
        { "\"Atlas Board/Platform/", "\"Atlas Board/Online & Backend/Platform/" },

        { "\"Atlas Board/Balance/", "\"Atlas Board/Game & Content/Balance/" },
        { "\"Atlas Board/Data/", "\"Atlas Board/Game & Content/Data/" },
        { "\"Atlas Board/Localization/", "\"Atlas Board/Game & Content/Localization/" },

        { "\"Atlas Board/Meta/", "\"Atlas Board/Meta & Progression/Meta/" },
        { "\"Atlas Board/Progression/", "\"Atlas Board/Meta & Progression/Progression/" },

        { "\"Atlas Board/QA/", "\"Atlas Board/QA & Diagnostics/QA/" },

        { "\"Atlas Board/Phase 14A/", "\"Atlas Board/Phases/Phase 14/14A - City Information/" },
        { "\"Atlas Board/Phase 14B/", "\"Atlas Board/Phases/Phase 14/14B - Network Quality/" },
        { "\"Atlas Board/Phase 14C/", "\"Atlas Board/Phases/Phase 14/14C - Profile Header/" },
        { "\"Atlas Board/Phase 14D/", "\"Atlas Board/Phases/Phase 14/14D - HUD Alignment/" }
    };

    private static readonly Regex MenuAttributeRegex =
        new Regex(
            @"(?s)\[\s*MenuItem\s*\(.*?\)\]",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    [MenuItem(
        "Atlas Board/Project Tools/Menu Organization/Preview Safe Taxonomy")]
    public static void Preview()
    {
        Rewrite(
            false,
            false);
    }

    [MenuItem(
        "Atlas Board/Project Tools/Menu Organization/Apply Safe Taxonomy")]
    public static void Apply()
    {
        if (!EditorUtility.DisplayDialog(
                "Atlas Board Menu Organization",
                "This safe version performs only exact literal menu-prefix " +
                "replacements. It does not parse or reconstruct MenuItem attributes.\n\n" +
                "Continue?",
                "Apply",
                "Cancel"))
        {
            return;
        }

        Rewrite(
            true,
            false);

        AssetDatabase.Refresh();
    }

    [MenuItem(
        "Atlas Board/Project Tools/Menu Organization/Restore Legacy Taxonomy")]
    public static void Restore()
    {
        if (!EditorUtility.DisplayDialog(
                "Restore Legacy Menu Taxonomy",
                "This performs exact reverse literal-prefix replacements only.",
                "Restore",
                "Cancel"))
        {
            return;
        }

        Rewrite(
            true,
            true);

        AssetDatabase.Refresh();
    }

    [MenuItem(
        "Atlas Board/Project Tools/Menu Organization/Validate Taxonomy")]
    public static void Validate()
    {
        string[] files =
            Directory.GetFiles(
                AssetsRoot,
                "*.cs",
                SearchOption.AllDirectories);

        int menuEntries = 0;
        int duplicatePaths = 0;
        int legacyPaths = 0;

        foreach (string file in files)
        {
            string source =
                File.ReadAllText(
                    file);

            MatchCollection attributes =
                MenuAttributeRegex.Matches(
                    source);

            foreach (Match attribute
                     in attributes)
            {
                string value =
                    attribute.Value;

                int firstAtlas =
                    value.IndexOf(
                        "Atlas Board/",
                        StringComparison.Ordinal);

                if (firstAtlas < 0)
                {
                    continue;
                }

                menuEntries++;

                int secondAtlas =
                    value.IndexOf(
                        "Atlas Board/",
                        firstAtlas + 12,
                        StringComparison.Ordinal);

                if (secondAtlas >= 0)
                {
                    duplicatePaths++;
                }

                for (int row = 0;
                     row < PrefixMappings.GetLength(0);
                     row++)
                {
                    string legacyPrefix =
                        PrefixMappings[row, 0];

                    if (value.IndexOf(
                            legacyPrefix,
                            StringComparison.Ordinal) >= 0)
                    {
                        legacyPaths++;
                    }
                }
            }
        }

        if (duplicatePaths > 0)
        {
            Debug.LogError(
                $"Atlas Board menu taxonomy validation FAILED: " +
                $"{duplicatePaths} duplicated Atlas Board MenuItem path(s) remain.");
            return;
        }

        if (legacyPaths > 0)
        {
            Debug.LogWarning(
                $"Atlas Board menu syntax is healthy, but " +
                $"{legacyPaths} legacy menu path(s) remain. " +
                "Run Apply Safe Taxonomy if you want the grouped menu.");
            return;
        }

        Debug.Log(
            $"Atlas Board menu taxonomy validation PASS. " +
            $"{menuEntries} Atlas Board MenuItem entry/entries scanned.");
    }

    private static void Rewrite(
        bool write,
        bool reverse)
    {
        string[] files =
            Directory.GetFiles(
                AssetsRoot,
                "*.cs",
                SearchOption.AllDirectories);

        int changedFiles = 0;
        int changedEntries = 0;

        foreach (string file in files)
        {
            string original =
                File.ReadAllText(
                    file);

            string rewritten =
                original;

            int localChanges = 0;

            for (int row = 0;
                 row < PrefixMappings.GetLength(0);
                 row++)
            {
                string from =
                    reverse
                        ? PrefixMappings[row, 1]
                        : PrefixMappings[row, 0];

                string to =
                    reverse
                        ? PrefixMappings[row, 0]
                        : PrefixMappings[row, 1];

                int count =
                    CountOccurrences(
                        rewritten,
                        from);

                if (count == 0)
                {
                    continue;
                }

                rewritten =
                    rewritten.Replace(
                        from,
                        to);

                localChanges +=
                    count;
            }

            if (localChanges == 0)
            {
                continue;
            }

            changedFiles++;
            changedEntries +=
                localChanges;

            if (write)
            {
                File.WriteAllText(
                    file,
                    rewritten,
                    new UTF8Encoding(
                        false));
            }
        }

        Debug.Log(
            $"Atlas Board SAFE menu organization " +
            $"{(write ? "APPLY" : "PREVIEW")}: " +
            $"{changedFiles} file(s), {changedEntries} exact prefix replacement(s).");
    }

    private static int CountOccurrences(
        string source,
        string value)
    {
        if (string.IsNullOrEmpty(
                source) ||
            string.IsNullOrEmpty(
                value))
        {
            return 0;
        }

        int count = 0;
        int index = 0;

        while (true)
        {
            index =
                source.IndexOf(
                    value,
                    index,
                    StringComparison.Ordinal);

            if (index < 0)
            {
                return count;
            }

            count++;
            index +=
                value.Length;
        }
    }
}
#endif
