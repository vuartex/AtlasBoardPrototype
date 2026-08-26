#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AtlasBoardPawnCustomizationV1Setup
{
    private const string DataRoot =
        "Assets/Project/Data/Players/PawnCosmetics";

    private const string CatalogPath =
        DataRoot +
        "/PawnCosmeticCatalog_Default.asset";

    private const string SystemName =
        "PawnCustomizationSystem";

    private const string ModalName =
        "PawnCustomizationModal";

    private static readonly Color Blue =
        new Color32(
            28,
            157,
            211,
            255);

    private static readonly Color DarkBlue =
        new Color32(
            19,
            113,
            163,
            255);

    private static readonly Color Green =
        new Color32(
            105,
            145,
            0,
            255);

    private static readonly Color Red =
        new Color32(
            180,
            22,
            70,
            255);

    private static readonly Color Cream =
        new Color32(
            246,
            241,
            231,
            255);

    private static readonly Color Panel =
        new Color32(
            233,
            233,
            239,
            255);

    private static readonly Color TextDark =
        new Color32(
            61,
            62,
            66,
            255);

    private static TMP_FontAsset defaultFont;

    [MenuItem(
        "Atlas Board/Pawns/Build Pawn Customization v1")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "Exit Play Mode before building Pawn Customization.");

            return;
        }

        ResolveDefaultFont();

        PawnCosmeticCatalog catalog =
            BuildOrRefreshCatalog();

        if (catalog == null ||
            catalog.Count == 0)
        {
            Debug.LogError(
                "Pawn Customization was not built because no Kenney Mini Characters were discovered. " +
                "Import/extract the Kenney Mini Characters pack into Assets, then run this menu again.");

            return;
        }

        AtlasBoardPawnCosmeticService service =
            BuildOrRefreshService(
                catalog);

        AtlasBoardPawnCustomizationUI ui =
            BuildOrRefreshLobbyUI(
                catalog);

        int appliers =
            InstallPawnAppliers();

        AtlasBoardLocalizationV1Setup
            .BuildOrRefresh();

        AssetDatabase.SaveAssets();

        EditorSceneManager
            .MarkAllScenesDirty();

        Debug.Log(
            "AtlasBoard Pawn Customization v1 ready. " +
            $"Cosmetics={catalog.Count}, " +
            $"pawn appliers={appliers}. " +
            "Player ownership/UI colors remain unchanged; only the pawn model is customizable.");
    }

    private static PawnCosmeticCatalog
        BuildOrRefreshCatalog()
    {
        EnsureFolder(
            DataRoot);

        List<CharacterCandidate> discovered =
            DiscoverMiniCharacters();

        if (discovered.Count == 0)
        {
            return null;
        }

        List<PawnCosmeticDefinition>
            definitions =
                new List<
                    PawnCosmeticDefinition>();

        foreach (CharacterCandidate candidate
                 in discovered.Take(12))
        {
            string assetName =
                "Pawn_" +
                candidate.Id;

            string path =
                DataRoot +
                "/" +
                assetName +
                ".asset";

            PawnCosmeticDefinition definition =
                AssetDatabase.LoadAssetAtPath<
                    PawnCosmeticDefinition>(
                        path);

            if (definition == null)
            {
                definition =
                    ScriptableObject.CreateInstance<
                        PawnCosmeticDefinition>();

                AssetDatabase.CreateAsset(
                    definition,
                    path);
            }

            definition.EditorConfigure(
                "kenney_" +
                candidate.Id.ToLowerInvariant(),
                candidate.DisplayName,
                candidate.Asset,
                1.15f,
                Vector3.zero);

            EditorUtility.SetDirty(
                definition);

            definitions.Add(
                definition);
        }

        PawnCosmeticCatalog catalog =
            AssetDatabase.LoadAssetAtPath<
                PawnCosmeticCatalog>(
                    CatalogPath);

        if (catalog == null)
        {
            catalog =
                ScriptableObject.CreateInstance<
                    PawnCosmeticCatalog>();

            AssetDatabase.CreateAsset(
                catalog,
                CatalogPath);
        }

        catalog.EditorReplace(
            definitions);

        EditorUtility.SetDirty(
            catalog);

        return catalog;
    }

    private static List<CharacterCandidate>
        DiscoverMiniCharacters()
    {
        Dictionary<string, GameObject> matches =
            new Dictionary<string, GameObject>(
                StringComparer.OrdinalIgnoreCase);

        string[] guids =
            AssetDatabase.FindAssets(
                "character");

        foreach (string guid
                 in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            if (string.IsNullOrWhiteSpace(
                    path))
            {
                continue;
            }

            string pathLower =
                path.ToLowerInvariant();

            bool miniCharactersPath =
                pathLower.Contains(
                    "mini-characters") ||
                pathLower.Contains(
                    "mini_characters") ||
                pathLower.Contains(
                    "mini characters") ||
                pathLower.Contains(
                    "minicharacters");

            if (!miniCharactersPath)
            {
                continue;
            }

            string extension =
                Path.GetExtension(
                    path)
                .ToLowerInvariant();

            if (extension != ".fbx" &&
                extension != ".obj" &&
                extension != ".prefab" &&
                extension != ".gltf" &&
                extension != ".glb")
            {
                continue;
            }

            GameObject asset =
                AssetDatabase.LoadAssetAtPath<
                    GameObject>(
                        path);

            if (asset == null)
            {
                continue;
            }

            string normalized =
                Normalize(
                    Path.GetFileNameWithoutExtension(
                        path));

            string id =
                DetectCharacterId(
                    normalized);

            if (string.IsNullOrWhiteSpace(
                    id))
            {
                continue;
            }

            if (!matches.ContainsKey(
                    id))
            {
                matches[id] =
                    asset;
            }
        }

        string[] order =
        {
            "Female_A",
            "Male_A",
            "Female_B",
            "Male_B",
            "Female_C",
            "Male_C",
            "Female_D",
            "Male_D",
            "Female_E",
            "Male_E",
            "Female_F",
            "Male_F"
        };

        List<CharacterCandidate> result =
            new List<CharacterCandidate>();

        foreach (string id
                 in order)
        {
            if (!matches.TryGetValue(
                    id,
                    out GameObject asset))
            {
                continue;
            }

            result.Add(
                new CharacterCandidate
                {
                    Id =
                        id,

                    DisplayName =
                        "Mini Character " +
                        id.Replace(
                            "_",
                            " "),

                    Asset =
                        asset
                });
        }

        // Fallback for a differently named import:
        // use any character-looking GameObject assets in the Mini Characters folder.
        if (result.Count == 0)
        {
            string[] allGuids =
                AssetDatabase.FindAssets(
                    "t:GameObject");

            foreach (string guid
                     in allGuids)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guid);

                string lower =
                    path.ToLowerInvariant();

                bool miniCharactersPath =
                    lower.Contains(
                        "mini-characters") ||
                    lower.Contains(
                        "mini_characters") ||
                    lower.Contains(
                        "mini characters") ||
                    lower.Contains(
                        "minicharacters");

                if (!miniCharactersPath ||
                    !lower.Contains(
                        "character"))
                {
                    continue;
                }

                GameObject asset =
                    AssetDatabase.LoadAssetAtPath<
                        GameObject>(
                            path);

                if (asset == null)
                {
                    continue;
                }

                result.Add(
                    new CharacterCandidate
                    {
                        Id =
                            "Character_" +
                            (result.Count + 1),

                        DisplayName =
                            "Mini Character " +
                            (result.Count + 1),

                        Asset =
                            asset
                    });

                if (result.Count >= 12)
                {
                    break;
                }
            }
        }

        return result;
    }

    private static string DetectCharacterId(
        string normalized)
    {
        char[] letters =
        {
            'a',
            'b',
            'c',
            'd',
            'e',
            'f'
        };

        foreach (char letter
                 in letters)
        {
            string female =
                "characterfemale" +
                letter;

            if (normalized.Contains(
                    female) ||
                normalized ==
                    "female" +
                    letter)
            {
                return
                    "Female_" +
                    char.ToUpperInvariant(
                        letter);
            }
        }

        foreach (char letter
                 in letters)
        {
            string male =
                "charactermale" +
                letter;

            if ((normalized.Contains(
                     male) ||
                 normalized ==
                     "male" +
                     letter) &&
                !normalized.Contains(
                    "female"))
            {
                return
                    "Male_" +
                    char.ToUpperInvariant(
                        letter);
            }
        }

        return string.Empty;
    }

    private static AtlasBoardPawnCosmeticService
        BuildOrRefreshService(
            PawnCosmeticCatalog catalog)
    {
        GameObject root =
            FindSceneObject(
                SystemName);

        if (root == null)
        {
            root =
                new GameObject(
                    SystemName);

            Undo.RegisterCreatedObjectUndo(
                root,
                "Create Pawn Customization System");
        }

        AtlasBoardPawnCosmeticService service =
            root.GetComponent<
                AtlasBoardPawnCosmeticService>();

        if (service == null)
        {
            service =
                Undo.AddComponent<
                    AtlasBoardPawnCosmeticService>(
                        root);
        }

        service.EditorConfigure(
            catalog);

        EditorUtility.SetDirty(
            service);

        return service;
    }

    private static AtlasBoardPawnCustomizationUI
        BuildOrRefreshLobbyUI(
            PawnCosmeticCatalog catalog)
    {
        GameObject canvas =
            FindSceneObject(
                "Canvas_MainMenu");

        if (canvas == null)
        {
            Debug.LogError(
                "Canvas_MainMenu was not found. Build the Main Menu/Lobby first.");

            return null;
        }

        Transform old =
            canvas.transform.Find(
                ModalName);

        if (old != null)
        {
            Undo.DestroyObjectImmediate(
                old.gameObject);
        }

        GameObject modal =
            CreateRectObject(
                canvas.transform,
                ModalName,
                Vector2.zero,
                new Vector2(
                    1920f,
                    1080f));

        Image dimmer =
            modal.AddComponent<Image>();

        dimmer.color =
            new Color32(
                17,
                25,
                37,
                175);

        GameObject window =
            CreatePanel(
                modal.transform,
                "Window",
                Vector2.zero,
                new Vector2(
                    980f,
                    740f),
                Cream);

        CreatePanel(
            window.transform,
            "Header",
            new Vector2(
                0f,
                -320f),
            new Vector2(
                900f,
                82f),
            DarkBlue);

        TMP_Text title =
            CreateText(
                window.transform,
                "Title",
                "PAWN CUSTOMIZATION",
                new Vector2(
                    0f,
                    -320f),
                new Vector2(
                    780f,
                    54f),
                30f,
                Color.white,
                FontStyles.Bold);

        AddLocalizedText(
            title,
            "pawn.customization.title");

        TMP_Text player =
            CreateText(
                window.transform,
                "Player",
                "Player 1",
                new Vector2(
                    0f,
                    -245f),
                new Vector2(
                    500f,
                    50f),
                26f,
                TextDark,
                FontStyles.Bold);

        GameObject previewBackground =
            CreatePanel(
                window.transform,
                "PreviewBackground",
                new Vector2(
                    0f,
                    5f),
                new Vector2(
                    520f,
                    420f),
                new Color32(
                    31,
                    40,
                    53,
                    255));

        GameObject rawObject =
            CreateRectObject(
                previewBackground.transform,
                "Preview",
                Vector2.zero,
                new Vector2(
                    490f,
                    390f));

        RawImage preview =
            rawObject.AddComponent<
                RawImage>();

        preview.color =
            Color.white;

        Button previous =
            CreateButton(
                window.transform,
                "Previous",
                "<",
                new Vector2(
                    -350f,
                    10f),
                new Vector2(
                    105f,
                    105f),
                Blue);

        Button next =
            CreateButton(
                window.transform,
                "Next",
                ">",
                new Vector2(
                    350f,
                    10f),
                new Vector2(
                    105f,
                    105f),
                Blue);

        TMP_Text selection =
            CreateText(
                window.transform,
                "Selection",
                "Pawn 1 / 12",
                new Vector2(
                    0f,
                    245f),
                new Vector2(
                    460f,
                    50f),
                24f,
                TextDark,
                FontStyles.Bold);

        Button cancel =
            CreateButton(
                window.transform,
                "Cancel",
                "CANCEL",
                new Vector2(
                    -135f,
                    315f),
                new Vector2(
                    220f,
                    64f),
                new Color32(
                    105,
                    115,
                    135,
                    255));

        TMP_Text cancelText =
            cancel.GetComponentInChildren<
                TMP_Text>(
                    true);

        AddLocalizedText(
            cancelText,
            "common.cancel");

        Button apply =
            CreateButton(
                window.transform,
                "Apply",
                "USE PAWN",
                new Vector2(
                    135f,
                    315f),
                new Vector2(
                    220f,
                    64f),
                Green);

        TMP_Text applyText =
            apply.GetComponentInChildren<
                TMP_Text>(
                    true);

        AddLocalizedText(
            applyText,
            "pawn.customization.use");

        AtlasBoardPawnCustomizationUI ui =
            modal.AddComponent<
                AtlasBoardPawnCustomizationUI>();

        ui.EditorConfigure(
            catalog,
            modal,
            player,
            selection,
            preview,
            previous,
            next,
            apply,
            cancel);

        InstallSlotButtons(
            canvas.transform,
            ui);

        modal.SetActive(
            false);

        EditorUtility.SetDirty(
            ui);

        return ui;
    }

    private static void InstallSlotButtons(
        Transform canvas,
        AtlasBoardPawnCustomizationUI ui)
    {
        for (int slot = 0;
             slot < 4;
             slot++)
        {
            string rowName =
                "PlayerRow_P" +
                (slot + 1);

            Transform row =
                FindDescendant(
                    canvas,
                    rowName);

            if (row == null)
            {
                Debug.LogWarning(
                    $"{rowName} was not found. Pawn button was not installed.");

                continue;
            }

            Transform slotColor =
                row.Find(
                    "SlotColor");

            if (slotColor == null)
            {
                Debug.LogWarning(
                    $"{rowName}/SlotColor was not found.");

                continue;
            }

            Button button =
                slotColor.GetComponent<
                    Button>();

            if (button == null)
            {
                button =
                    Undo.AddComponent<
                        Button>(
                            slotColor.gameObject);
            }

            Image image =
                slotColor.GetComponent<
                    Image>();

            button.targetGraphic =
                image;

            button.onClick
                .RemoveAllListeners();

            UnityEventTools
                .AddIntPersistentListener(
                    button.onClick,
                    ui.OpenForSlot,
                    slot);

            Transform oldLabel =
                slotColor.Find(
                    "PawnLabel");

            if (oldLabel != null)
            {
                Undo.DestroyObjectImmediate(
                    oldLabel.gameObject);
            }

            TMP_Text label =
                CreateText(
                    slotColor,
                    "PawnLabel",
                    "PAWN",
                    Vector2.zero,
                    new Vector2(
                        58f,
                        54f),
                    12f,
                    Color.white,
                    FontStyles.Bold);

            AddLocalizedText(
                label,
                "lobby.pawn");
        }
    }

    private static int InstallPawnAppliers()
    {
        PlayerPawnMover[] pawns =
            Resources.FindObjectsOfTypeAll<
                PlayerPawnMover>()
                .Where(
                    pawn =>
                        pawn != null &&
                        pawn.gameObject.scene
                            .IsValid())
                .ToArray();

        int installed = 0;

        foreach (PlayerPawnMover pawn
                 in pawns)
        {
            PlayerGameState state =
                pawn.GetComponent<
                    PlayerGameState>();

            if (state == null)
            {
                continue;
            }

            PawnCosmeticApplier applier =
                pawn.GetComponent<
                    PawnCosmeticApplier>();

            Renderer[] legacy =
                pawn.GetComponentsInChildren<
                    Renderer>(
                        true)
                    .Where(
                        renderer =>
                            renderer != null &&
                            !HasAncestorNamed(
                                renderer.transform,
                                "PawnCosmeticMount"))
                    .ToArray();

            float heightOffset =
                ReadPrivateFloat(
                    pawn,
                    "pawnHeightOffset",
                    0.7f);

            if (applier == null)
            {
                applier =
                    Undo.AddComponent<
                        PawnCosmeticApplier>(
                            pawn.gameObject);
            }

            applier.EditorConfigure(
                state,
                pawn,
                legacy,
                heightOffset);

            EditorUtility.SetDirty(
                applier);

            installed++;
        }

        return installed;
    }

    private static float ReadPrivateFloat(
        UnityEngine.Object target,
        string propertyName,
        float fallback)
    {
        if (target == null)
        {
            return fallback;
        }

        SerializedObject serialized =
            new SerializedObject(
                target);

        SerializedProperty property =
            serialized.FindProperty(
                propertyName);

        return property != null
            ? property.floatValue
            : fallback;
    }

    private static void ResolveDefaultFont()
    {
        AtlasBoardLocalizationFontProfile profile =
            AssetDatabase.LoadAssetAtPath<
                AtlasBoardLocalizationFontProfile>(
                    "Assets/Project/Data/Localization/LocalizationFonts_Default.asset");

        if (profile != null &&
            profile.LatinCyrillicFont != null)
        {
            defaultFont =
                profile.LatinCyrillicFont;

            return;
        }

        defaultFont =
            TMP_Settings.defaultFontAsset;
    }

    private static GameObject CreatePanel(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject root =
            CreateRectObject(
                parent,
                name,
                position,
                size);

        Image image =
            root.AddComponent<
                Image>();

        image.color =
            color;

        return root;
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        GameObject root =
            CreatePanel(
                parent,
                name,
                position,
                size,
                color);

        Button button =
            root.AddComponent<
                Button>();

        button.targetGraphic =
            root.GetComponent<
                Image>();

        CreateText(
            root.transform,
            "Label",
            label,
            Vector2.zero,
            new Vector2(
                size.x - 16f,
                size.y - 10f),
            22f,
            Color.white,
            FontStyles.Bold);

        return button;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        FontStyles style)
    {
        GameObject root =
            CreateRectObject(
                parent,
                name,
                position,
                size);

        TextMeshProUGUI text =
            root.AddComponent<
                TextMeshProUGUI>();

        text.text =
            value;

        text.fontSize =
            fontSize;

        text.color =
            color;

        text.fontStyle =
            style;

        text.alignment =
            TextAlignmentOptions.Center;

        text.enableAutoSizing =
            true;

        text.fontSizeMax =
            fontSize;

        text.fontSizeMin =
            Mathf.Max(
                9f,
                fontSize *
                0.55f);

        if (defaultFont != null)
        {
            text.font =
                defaultFont;
        }

        return text;
    }

    private static void AddLocalizedText(
        TMP_Text text,
        string key)
    {
        if (text == null)
        {
            return;
        }

        AtlasBoardLocalizedText localized =
            text.GetComponent<
                AtlasBoardLocalizedText>();

        if (localized == null)
        {
            localized =
                Undo.AddComponent<
                    AtlasBoardLocalizedText>(
                        text.gameObject);
        }

        localized.EditorConfigure(
            key,
            text);

        EditorUtility.SetDirty(
            localized);
    }

    private static GameObject CreateRectObject(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size)
    {
        GameObject root =
            new GameObject(
                name,
                typeof(RectTransform));

        Undo.RegisterCreatedObjectUndo(
            root,
            "Create " +
            name);

        root.transform.SetParent(
            parent,
            false);

        RectTransform rect =
            root.GetComponent<
                RectTransform>();

        rect.anchorMin =
            new Vector2(
                0.5f,
                0.5f);

        rect.anchorMax =
            new Vector2(
                0.5f,
                0.5f);

        rect.pivot =
            new Vector2(
                0.5f,
                0.5f);

        rect.anchoredPosition =
            position;

        rect.sizeDelta =
            size;

        return root;
    }

    private static Transform FindDescendant(
        Transform root,
        string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name ==
            objectName)
        {
            return root;
        }

        for (int index = 0;
             index < root.childCount;
             index++)
        {
            Transform found =
                FindDescendant(
                    root.GetChild(
                        index),
                    objectName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static bool HasAncestorNamed(
        Transform transform,
        string ancestorName)
    {
        Transform current =
            transform;

        while (current != null)
        {
            if (current.name ==
                ancestorName)
            {
                return true;
            }

            current =
                current.parent;
        }

        return false;
    }

    private static GameObject FindSceneObject(
        string objectName)
    {
        GameObject[] all =
            Resources.FindObjectsOfTypeAll<
                GameObject>();

        foreach (GameObject item
                 in all)
        {
            if (item == null ||
                !item.scene.IsValid() ||
                item.name != objectName)
            {
                continue;
            }

            return item;
        }

        return null;
    }

    private static string Normalize(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        return new string(
            value
                .ToLowerInvariant()
                .Where(
                    char.IsLetterOrDigit)
                .ToArray());
    }

    private static void EnsureFolder(
        string path)
    {
        if (AssetDatabase.IsValidFolder(
                path))
        {
            return;
        }

        string parent =
            Path.GetDirectoryName(
                    path)
                ?.Replace(
                    "\\",
                    "/");

        string name =
            Path.GetFileName(
                path);

        if (!string.IsNullOrWhiteSpace(
                parent) &&
            !AssetDatabase.IsValidFolder(
                parent))
        {
            EnsureFolder(
                parent);
        }

        AssetDatabase.CreateFolder(
            parent,
            name);
    }

    private sealed class CharacterCandidate
    {
        public string Id;
        public string DisplayName;
        public GameObject Asset;
    }
}
#endif
