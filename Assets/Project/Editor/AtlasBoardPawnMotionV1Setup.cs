#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AtlasBoardPawnMotionV1Setup
{
    private const string CatalogPath =
        "Assets/Project/Data/Players/PawnCosmetics/PawnCosmeticCatalog_Default.asset";

    private const string MotionDataRoot =
        "Assets/Project/Data/Players/PawnMotion";

    private const string ControllerRoot =
        MotionDataRoot +
        "/Controllers";

    private const string MotionSetRoot =
        MotionDataRoot +
        "/MotionSets";

    [MenuItem(
        "Atlas Board/Pawns/Build Pawn Motion Polish v1")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "Exit Play Mode before building Pawn Motion Polish.");

            return;
        }

        PawnCosmeticCatalog catalog =
            AssetDatabase.LoadAssetAtPath<
                PawnCosmeticCatalog>(
                    CatalogPath);

        if (catalog == null ||
            catalog.Count == 0)
        {
            Debug.LogError(
                "Pawn Motion Polish was not built because the Pawn Cosmetic catalog is missing or empty. " +
                "Run Atlas Board > Pawns > Build Pawn Customization v1 first.");

            return;
        }

        EnsureFolder(
            MotionDataRoot);

        EnsureFolder(
            ControllerRoot);

        EnsureFolder(
            MotionSetRoot);

        BuildStats stats =
            new BuildStats();

        foreach (PawnCosmeticDefinition cosmetic
                 in catalog.Cosmetics)
        {
            if (cosmetic == null ||
                cosmetic.Prefab == null)
            {
                stats.Skipped++;
                continue;
            }

            MotionClips clips =
                DiscoverMotionClips(
                    cosmetic.Prefab);

            AnimationClip fallback =
                clips.Idle ??
                clips.Sit ??
                clips.Walk ??
                clips.Sprint ??
                clips.LookLeft ??
                clips.LookRight;

            if (fallback == null)
            {
                Debug.LogWarning(
                    $"No usable animation clips were found for pawn cosmetic '{cosmetic.CosmeticId}' " +
                    $"at '{AssetDatabase.GetAssetPath(cosmetic.Prefab)}'.",
                    cosmetic);

                stats.Skipped++;
                continue;
            }

            AnimationClip idle =
                clips.Idle ??
                clips.Sit ??
                fallback;

            AnimationClip walk =
                clips.Walk ??
                idle;

            AnimationClip sprint =
                clips.Sprint ??
                clips.Walk ??
                idle;

            AnimationClip sit =
                clips.Sit ??
                clips.Idle ??
                fallback;

            AnimationClip lookLeft =
                clips.LookLeft ??
                sit;

            AnimationClip lookRight =
                clips.LookRight ??
                sit;

            string safeId =
                SanitizeFileName(
                    cosmetic.CosmeticId);

            string controllerPath =
                ControllerRoot +
                "/PawnMotion_" +
                safeId +
                ".controller";

            AnimatorController controller =
                BuildOrRefreshController(
                    controllerPath,
                    idle,
                    walk,
                    sprint,
                    sit,
                    lookLeft,
                    lookRight);

            if (controller == null)
            {
                stats.Skipped++;
                continue;
            }

            string motionSetPath =
                MotionSetRoot +
                "/PawnMotionSet_" +
                safeId +
                ".asset";

            PawnMotionSetDefinition motionSet =
                AssetDatabase.LoadAssetAtPath<
                    PawnMotionSetDefinition>(
                        motionSetPath);

            if (motionSet == null)
            {
                motionSet =
                    ScriptableObject.CreateInstance<
                        PawnMotionSetDefinition>();

                AssetDatabase.CreateAsset(
                    motionSet,
                    motionSetPath);
            }

            motionSet.EditorConfigure(
                "default_" +
                cosmetic.CosmeticId,
                cosmetic.DisplayName +
                " Default Motion",
                controller,
                clips.Idle != null,
                clips.Walk != null,
                clips.Sprint != null,
                clips.Sit != null,
                clips.LookLeft != null,
                clips.LookRight != null,
                clips.Walk != null &&
                clips.Walk.isLooping,
                clips.Sprint != null &&
                clips.Sprint.isLooping,
                clips.LookLeft != null
                    ? clips.LookLeft.length
                    : 0.8f,
                clips.LookRight != null
                    ? clips.LookRight.length
                    : 0.8f,
                0f);

            EditorUtility.SetDirty(
                motionSet);

            cosmetic.EditorSetDefaultMotionSet(
                motionSet);

            EditorUtility.SetDirty(
                cosmetic);

            stats.CosmeticsConfigured++;

            if (clips.Idle != null)
            {
                stats.Idle++;
            }

            if (clips.Walk != null)
            {
                stats.Walk++;
            }

            if (clips.Sprint != null)
            {
                stats.Sprint++;
            }

            if (clips.Sit != null)
            {
                stats.Sit++;
            }

            if (clips.LookLeft != null)
            {
                stats.LookLeft++;
            }

            if (clips.LookRight != null)
            {
                stats.LookRight++;
            }
        }

        stats.PawnsConfigured =
            InstallPawnMotionComponents();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorSceneManager
            .MarkAllScenesDirty();

        Debug.Log(
            "AtlasBoard Pawn Motion Polish v1 ready. " +
            $"Pawns={stats.PawnsConfigured}, " +
            $"cosmetics={stats.CosmeticsConfigured}, " +
            $"idle={stats.Idle}, walk={stats.Walk}, sprint={stats.Sprint}, " +
            $"sit={stats.Sit}, lookLeft={stats.LookLeft}, lookRight={stats.LookRight}, " +
            $"skipped={stats.Skipped}. " +
            "Normal dice movement uses Walk; special forward movement uses Sprint; " +
            "landed pawns use Sit with occasional seated look-left/look-right. " +
            "Root Motion remains OFF and player ownership/UI colors are untouched.");
    }

    private static AnimatorController
        BuildOrRefreshController(
            string path,
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip sprint,
            AnimationClip sit,
            AnimationClip lookLeft,
            AnimationClip lookRight)
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<
                AnimatorController>(
                    path);

        if (controller == null)
        {
            controller =
                AnimatorController
                    .CreateAnimatorControllerAtPath(
                        path);
        }

        if (controller == null ||
            controller.layers == null ||
            controller.layers.Length == 0)
        {
            Debug.LogError(
                $"Could not create Animator Controller at '{path}'.");

            return null;
        }

        foreach (AnimatorControllerParameter parameter
                 in controller.parameters.ToArray())
        {
            controller.RemoveParameter(
                parameter);
        }

        AnimatorControllerLayer[] layers =
            controller.layers;

        layers[0].name =
            "Base Layer";

        controller.layers =
            layers;

        AnimatorStateMachine stateMachine =
            controller.layers[0]
                .stateMachine;

        foreach (ChildAnimatorState child
                 in stateMachine.states.ToArray())
        {
            stateMachine.RemoveState(
                child.state);
        }

        foreach (AnimatorStateTransition transition
                 in stateMachine.anyStateTransitions.ToArray())
        {
            stateMachine.RemoveAnyStateTransition(
                transition);
        }

        foreach (AnimatorTransition transition
                 in stateMachine.entryTransitions.ToArray())
        {
            stateMachine.RemoveEntryTransition(
                transition);
        }

        AnimatorState sitState =
            AddState(
                stateMachine,
                PawnMotionAnimator.StateSit,
                sit,
                new Vector3(
                    300f,
                    100f,
                    0f));

        AddState(
            stateMachine,
            PawnMotionAnimator.StateIdle,
            idle,
            new Vector3(
                100f,
                100f,
                0f));

        AddState(
            stateMachine,
            PawnMotionAnimator.StateWalk,
            walk,
            new Vector3(
                100f,
                220f,
                0f));

        AddState(
            stateMachine,
            PawnMotionAnimator.StateSprint,
            sprint,
            new Vector3(
                300f,
                220f,
                0f));

        AddState(
            stateMachine,
            PawnMotionAnimator.StateLookLeft,
            lookLeft,
            new Vector3(
                500f,
                80f,
                0f));

        AddState(
            stateMachine,
            PawnMotionAnimator.StateLookRight,
            lookRight,
            new Vector3(
                500f,
                160f,
                0f));

        stateMachine.defaultState =
            sitState;

        EditorUtility.SetDirty(
            controller);

        return controller;
    }

    private static AnimatorState AddState(
        AnimatorStateMachine stateMachine,
        string stateName,
        AnimationClip clip,
        Vector3 position)
    {
        AnimatorState state =
            stateMachine.AddState(
                stateName,
                position);

        state.motion =
            clip;

        state.speed =
            1f;

        state.writeDefaultValues =
            true;

        return state;
    }

    private static int InstallPawnMotionComponents()
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
            PawnCosmeticApplier applier =
                pawn.GetComponent<
                    PawnCosmeticApplier>();

            if (applier == null)
            {
                Debug.LogWarning(
                    $"{pawn.gameObject.name} does not have PawnCosmeticApplier. " +
                    "Run Build Pawn Customization v1 first.",
                    pawn);

                continue;
            }

            PawnMotionAnimator motion =
                pawn.GetComponent<
                    PawnMotionAnimator>();

            if (motion == null)
            {
                motion =
                    Undo.AddComponent<
                        PawnMotionAnimator>(
                            pawn.gameObject);
            }

            motion.EditorConfigure(
                pawn,
                applier);

            EditorUtility.SetDirty(
                motion);

            installed++;
        }

        return installed;
    }

    private static MotionClips DiscoverMotionClips(
        GameObject prefab)
    {
        MotionClips result =
            new MotionClips();

        if (prefab == null)
        {
            return result;
        }

        string prefabPath =
            AssetDatabase.GetAssetPath(
                prefab);

        List<AnimationClip> localClips =
            LoadAnimationClipsAtPath(
                prefabPath);

        FillMotionClips(
            localClips,
            result);

        if (result.IsComplete)
        {
            return result;
        }

        string assetRoot =
            DetectMiniCharactersRoot(
                prefabPath);

        if (string.IsNullOrWhiteSpace(
                assetRoot))
        {
            return result;
        }

        List<AnimationClip> packageClips =
            LoadAnimationClipsUnderRoot(
                assetRoot);

        FillMissingMotionClips(
            packageClips,
            result);

        return result;
    }

    private static List<AnimationClip>
        LoadAnimationClipsAtPath(
            string path)
    {
        if (string.IsNullOrWhiteSpace(
                path))
        {
            return new List<AnimationClip>();
        }

        return AssetDatabase
            .LoadAllAssetsAtPath(
                path)
            .OfType<AnimationClip>()
            .Where(IsUsableClip)
            .ToList();
    }

    private static List<AnimationClip>
        LoadAnimationClipsUnderRoot(
            string root)
    {
        List<AnimationClip> result =
            new List<AnimationClip>();

        HashSet<AnimationClip> seen =
            new HashSet<AnimationClip>();

        string[] guids =
            AssetDatabase.FindAssets(
                "t:AnimationClip",
                new[]
                {
                    root
                });

        foreach (string guid
                 in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guid);

            foreach (AnimationClip clip
                     in AssetDatabase
                         .LoadAllAssetsAtPath(
                             path)
                         .OfType<AnimationClip>())
            {
                if (!IsUsableClip(clip) ||
                    !seen.Add(
                        clip))
                {
                    continue;
                }

                result.Add(
                    clip);
            }
        }

        return result;
    }

    private static bool IsUsableClip(
        AnimationClip clip)
    {
        return clip != null &&
               !clip.name.StartsWith(
                   "__preview__",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void FillMotionClips(
        List<AnimationClip> clips,
        MotionClips result)
    {
        if (clips == null ||
            result == null)
        {
            return;
        }

        result.Idle =
            FindNamedClip(
                clips,
                "idle");

        result.Walk =
            FindNamedClip(
                clips,
                "walk");

        result.Sprint =
            FindNamedClip(
                clips,
                "sprint");

        result.Sit =
            FindNamedClip(
                clips,
                "sit");

        result.LookLeft =
            FindWheelchairLookClip(
                clips,
                true);

        result.LookRight =
            FindWheelchairLookClip(
                clips,
                false);
    }

    private static void FillMissingMotionClips(
        List<AnimationClip> clips,
        MotionClips result)
    {
        if (result.Idle == null)
        {
            result.Idle =
                FindNamedClip(
                    clips,
                    "idle");
        }

        if (result.Walk == null)
        {
            result.Walk =
                FindNamedClip(
                    clips,
                    "walk");
        }

        if (result.Sprint == null)
        {
            result.Sprint =
                FindNamedClip(
                    clips,
                    "sprint");
        }

        if (result.Sit == null)
        {
            result.Sit =
                FindNamedClip(
                    clips,
                    "sit");
        }

        if (result.LookLeft == null)
        {
            result.LookLeft =
                FindWheelchairLookClip(
                    clips,
                    true);
        }

        if (result.LookRight == null)
        {
            result.LookRight =
                FindWheelchairLookClip(
                    clips,
                    false);
        }
    }

    private static AnimationClip FindNamedClip(
        IEnumerable<AnimationClip> clips,
        string target)
    {
        string normalizedTarget =
            NormalizeName(
                target);

        List<AnimationClip> list =
            clips
                .Where(
                    clip =>
                        clip != null)
                .ToList();

        AnimationClip exact =
            list.FirstOrDefault(
                clip =>
                    NormalizeName(
                        clip.name) ==
                    normalizedTarget);

        if (exact != null)
        {
            return exact;
        }

        return list.FirstOrDefault(
            clip =>
            {
                string normalized =
                    NormalizeName(
                        clip.name);

                return normalized.EndsWith(
                    normalizedTarget,
                    StringComparison.OrdinalIgnoreCase);
            });
    }

    private static AnimationClip
        FindWheelchairLookClip(
            IEnumerable<AnimationClip> clips,
            bool left)
    {
        string side =
            left
                ? "left"
                : "right";

        List<AnimationClip> list =
            clips
                .Where(
                    clip =>
                        clip != null)
                .ToList();

        AnimationClip preferred =
            list.FirstOrDefault(
                clip =>
                {
                    string normalized =
                        NormalizeName(
                            clip.name);

                    return normalized.Contains(
                               "wheelchair") &&
                           normalized.Contains(
                               "look") &&
                           normalized.Contains(
                               side);
                });

        if (preferred != null)
        {
            return preferred;
        }

        AnimationClip wheelchairSide =
            list.FirstOrDefault(
                clip =>
                {
                    string normalized =
                        NormalizeName(
                            clip.name);

                    return normalized.Contains(
                               "wheelchair") &&
                           normalized.Contains(
                               side);
                });

        if (wheelchairSide != null)
        {
            return wheelchairSide;
        }

        AnimationClip genericLook =
            list.FirstOrDefault(
                clip =>
                {
                    string normalized =
                        NormalizeName(
                            clip.name);

                    return normalized.Contains(
                               "look") &&
                           normalized.Contains(
                               side);
                });

        if (genericLook != null)
        {
            return genericLook;
        }

        return FindNamedClip(
            list,
            "interact-" +
            side);
    }

    private static string DetectMiniCharactersRoot(
        string path)
    {
        if (string.IsNullOrWhiteSpace(
                path))
        {
            return null;
        }

        string normalizedPath =
            path.Replace(
                '\\',
                '/');

        string lower =
            normalizedPath
                .ToLowerInvariant();

        int modelsIndex =
            lower.IndexOf(
                "/models/",
                StringComparison.Ordinal);

        if (modelsIndex > 0)
        {
            return normalizedPath.Substring(
                0,
                modelsIndex);
        }

        int miniIndex =
            lower.IndexOf(
                "mini-characters",
                StringComparison.Ordinal);

        if (miniIndex < 0)
        {
            miniIndex =
                lower.IndexOf(
                    "mini_characters",
                    StringComparison.Ordinal);
        }

        if (miniIndex < 0)
        {
            miniIndex =
                lower.IndexOf(
                    "mini characters",
                    StringComparison.Ordinal);
        }

        if (miniIndex < 0)
        {
            return Path.GetDirectoryName(
                    normalizedPath)
                ?.Replace(
                    '\\',
                    '/');
        }

        int slashAfter =
            normalizedPath.IndexOf(
                '/',
                miniIndex);

        return slashAfter > 0
            ? normalizedPath.Substring(
                0,
                slashAfter)
            : normalizedPath;
    }

    private static string NormalizeName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        return new string(
            value
                .Where(
                    char.IsLetterOrDigit)
                .Select(
                    char.ToLowerInvariant)
                .ToArray());
    }

    private static string SanitizeFileName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return "unknown";
        }

        char[] invalid =
            Path.GetInvalidFileNameChars();

        return new string(
            value
                .Select(
                    character =>
                        invalid.Contains(
                            character)
                            ? '_'
                            : character)
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
                    '\\',
                    '/');

        string folderName =
            Path.GetFileName(
                path);

        if (string.IsNullOrWhiteSpace(
                parent) ||
            string.IsNullOrWhiteSpace(
                folderName))
        {
            return;
        }

        EnsureFolder(
            parent);

        AssetDatabase.CreateFolder(
            parent,
            folderName);
    }

    private sealed class MotionClips
    {
        public AnimationClip Idle;
        public AnimationClip Walk;
        public AnimationClip Sprint;
        public AnimationClip Sit;
        public AnimationClip LookLeft;
        public AnimationClip LookRight;

        public bool IsComplete =>
            Idle != null &&
            Walk != null &&
            Sprint != null &&
            Sit != null &&
            LookLeft != null &&
            LookRight != null;
    }

    private struct BuildStats
    {
        public int PawnsConfigured;
        public int CosmeticsConfigured;
        public int Idle;
        public int Walk;
        public int Sprint;
        public int Sit;
        public int LookLeft;
        public int LookRight;
        public int Skipped;
    }
}
#endif
