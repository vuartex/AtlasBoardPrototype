using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

public class DiceVisualController : MonoBehaviour
{
    [Header("Dice")]
    [SerializeField]
    private Transform dieOne;

    [SerializeField]
    private Transform dieTwo;

    // The actual five FBX assets are serialized on the EXISTING scene
    // component; no Resources copies or duplicate dice manager.
    [Header("Imported Dice Models — assigned by the scene")]
    [SerializeField] private GameObject classicDiceModel;
    [SerializeField] private GameObject rubyDiceModel;
    [SerializeField] private GameObject oceanDiceModel;
    [SerializeField] private GameObject midnightDiceModel;
    [SerializeField] private GameObject marbleDiceModel;

    [Header("PBR Little Games Pack — texture atlas")]
    // Bound from the package's existing GamePieces.mat by the original Editor
    // binding tool. Its UV atlas contains the face markings and skin colors.
    [SerializeField] private Material importedDiceAtlasMaterial;
    // Generated once by the existing Phase19N Editor binder and serialized on
    // this same DiceVisualController. Explicit material asset references keep
    // URP shader variants alive in player builds (no runtime Shader.Find).
    [SerializeField] private Material importedDiceRenderMaterial;

    // Stable seat-indexed cosmetic projections. Gameplay values remain wholly
    // authoritative in TurnManager/AtlasBoardTurnDiceNetworkCoordinator.
    private readonly string[] slotDiceIds = { "", "", "", "" };
    private readonly GameObject[,] renderedModel = new GameObject[2, 5];
    private Renderer[][] originalRenderers;
    private int projectionGeneration;
    private bool onlineDiceSession;
    private int localOnlineSlotIndex = -1;
    private string lobbyDiceSignature = "";
    private string activeVisualId = null;
    private string localDiceUid = "";
    private string localDiceProjection = "";

    [Header("Face Rotations")]
    [Tooltip(
        "Local Euler rotation that puts each numbered face on TOP. " +
        "Element 0 = face 1, Element 5 = face 6.")]
    [SerializeField]
    private Vector3[] faceLocalEulerRotations =
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(0f, 0f, 90f),
        new Vector3(90f, 0f, 0f),
        new Vector3(-90f, 0f, 0f),
        new Vector3(0f, 0f, -90f),
        new Vector3(180f, 0f, 0f)
    };

    // Phase 19R owner calibration, observed independently on Ruby Red and
    // Ocean Blue in Unity Play Mode (2026-10-27).  The imported GamePieces FBX
    // models do NOT have the same face orientation as the existing classic
    // white dice.  Under the original scene-authored Euler rotations, the
    // imported top faces read 6,3,5,2,4,1 for desired faces 1..6.
    // To obtain desired top face 1..6, reuse the existing scene rotations for
    // legacy faces 6,4,2,5,3,1 respectively.  These are 0-based indices.
    // No new Euler angles and NO change to the original Classic/bot rotation
    // array, TurnManager outcomes, authoritative RNG or dice animation.
    private static readonly int[] ImportedFaceToClassicRotationIndex =
    {
        5, 3, 1, 4, 2, 0
    };

    private bool activeImportedDice;

    [Header("Animation")]
    [SerializeField, Min(0.1f)]
    private float rollDuration = 0.85f;

    [SerializeField, Min(0f)]
    private float resultHoldDuration = 0.45f;

    [SerializeField, Min(0f)]
    private float launchHeight = 1.1f;

    [SerializeField, Min(0f)]
    private float arcHeight = 0.45f;

    [SerializeField, Min(0f)]
    private float lateralScatter = 0.22f;

    [SerializeField, Min(0f)]
    private float spinTurns = 3.5f;

    [SerializeField]
    private bool hideWhenIdle = true;

    [Header("Debug Face Calibration")]
    [SerializeField, Range(1, 6)]
    private int debugPreviewFace = 1;

    private Vector3 dieOneRestLocalPosition;
    private Vector3 dieTwoRestLocalPosition;

    private Vector3 dieOneRestLocalScale;
    private Vector3 dieTwoRestLocalScale;

    private Coroutine rollCoroutine;
    private bool initialized;

    public bool IsRolling =>
        rollCoroutine != null;

    private void Awake()
    {
        Initialize();

        if (hideWhenIdle)
        {
            SetDiceVisible(false);
        }
    }

    private void Update()
    {
        // One public projection read on identity change, not on every frame.
        // Handles opening Local/Offline directly after application restart.
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid;
        if (localDiceUid == uid) return;
        localDiceUid = uid;
        localDiceProjection = "";
        if (!string.IsNullOrEmpty(uid)) RefreshLocalDiceProjectionAsync(uid);
    }

    private async void RefreshLocalDiceProjectionAsync(string uid)
    {
        try
        {
            string id = await AtlasBoardRtdbMetaEconomy.ReadPublicDiceAsync(uid);
            if (uid == AtlasBoardRtdbMetaEconomy.CurrentUid && uid == localDiceUid)
                localDiceProjection = id;
        }
        catch (Exception) { /* cosmetic fallback only */ }
    }

    // Controlled by the existing TurnManager seat/AI state, not inferred from
    // array index or profile name. A bot never inherits a human's cosmetic.
    public bool PlayRoll(
        int dieOneValue,
        int dieTwoValue,
        Action onCompleted,
        int stableSlotIndex = 0,
        bool botControlled = false)
    {
        Initialize();

        if (!ValidateDice())
        {
            onCompleted?.Invoke();
            return false;
        }

        dieOneValue =
            Mathf.Clamp(
                dieOneValue,
                1,
                6);

        dieTwoValue =
            Mathf.Clamp(
                dieTwoValue,
                1,
                6);

        if (rollCoroutine != null)
        {
            StopCoroutine(rollCoroutine);
            rollCoroutine = null;
        }
        string selected = "";
        if (!botControlled && stableSlotIndex >= 0 && stableSlotIndex < slotDiceIds.Length)
        {
            if (onlineDiceSession)
            {
                selected = slotDiceIds[stableSlotIndex];
                // The canonical local UID cache is more current than an async
                // public-profile read when the owner has just equipped a die.
                // Only trust it for the locally controlled online account seat.
                if (stableSlotIndex == localOnlineSlotIndex &&
                    AtlasBoardRtdbMetaEconomy.HasCurrentDiceCache)
                    selected = AtlasBoardRtdbMetaEconomy.CurrentEquippedDiceId;
            }
            else if (stableSlotIndex == 0)
            {
                selected = AtlasBoardRtdbMetaEconomy.HasCurrentDiceCache
                    ? AtlasBoardRtdbMetaEconomy.CurrentEquippedDiceId
                    : localDiceProjection;
            }
        }
        // Unknown, missing, unowned or legacy IDs always resolve to White.
        if (!AtlasBoardRtdbMetaEconomy.IsDice(selected)) selected = "";
        Debug.Log("ATB_DICE_SKIN_ROLL mode=" + (onlineDiceSession ? "online" : "local") +
                  " slot=" + stableSlotIndex + " bot=" + botControlled +
                  " skin=" + (string.IsNullOrEmpty(selected) ? "classic" : selected) +
                  " modelAvailable=" + (string.IsNullOrEmpty(selected) || ModelFor(selected) != null) +
                  " originalClassic=" + string.IsNullOrEmpty(selected), this);
        // Renderer bounds are reliable only while the existing die roots are
        // active; fit imported FBX geometry before starting the coroutine.
        SetDiceVisible(true);
        ApplyImportedDice(selected);
        Debug.Log("ATB_DICE_FACE_CALIBRATION mode=" +
                  (activeImportedDice ? "imported_19r" : "classic_unchanged") +
                  " slot=" + stableSlotIndex +
                  " target=" + dieOneValue + "+" + dieTwoValue, this);

        rollCoroutine =
            StartCoroutine(
                RollRoutine(
                    dieOneValue,
                    dieTwoValue,
                    onCompleted));

        return true;
    }

    public void ResetForNewMatchSession()
    {
        Initialize();
        projectionGeneration++;
        onlineDiceSession = false;
        localOnlineSlotIndex = -1;
        lobbyDiceSignature = "";
        for (int i = 0; i < slotDiceIds.Length; i++) slotDiceIds[i] = "";
        activeVisualId = null;
        activeImportedDice = false;

        if (rollCoroutine != null)
        {
            StopCoroutine(rollCoroutine);
            rollCoroutine = null;
        }

        if (dieOne != null)
        {
            dieOne.localPosition = dieOneRestLocalPosition;
            dieOne.localScale = dieOneRestLocalScale;
            dieOne.localRotation = Quaternion.identity;
        }

        if (dieTwo != null)
        {
            dieTwo.localPosition = dieTwoRestLocalPosition;
            dieTwo.localScale = dieTwoRestLocalScale;
            dieTwo.localRotation = Quaternion.identity;
        }

        SetDiceVisible(false);
    }

    // Explicit Local Offline entry. The scene persists across online -> local
    // transitions, so onlineDiceSession must not leak into the next match.
    // This only resets cosmetics; no gameplay RNG or room state is touched.
    public void BeginLocalOfflineDiceSession()
    {
        ResetForNewMatchSession();
        string uid = AtlasBoardRtdbMetaEconomy.CurrentUid;
        localDiceUid = uid;
        localDiceProjection = AtlasBoardRtdbMetaEconomy.HasCurrentDiceCache
            ? AtlasBoardRtdbMetaEconomy.CurrentEquippedDiceId : "";
        if (!string.IsNullOrEmpty(uid)) RefreshLocalDiceProjectionAsync(uid);
        Debug.Log("ATB_DICE_SKIN_SESSION mode=local " +
                  "cache=" + AtlasBoardRtdbMetaEconomy.HasCurrentDiceCache +
                  " skin=" + (string.IsNullOrEmpty(localDiceProjection)
                      ? "classic" : localDiceProjection), this);
    }

    // Refresh only when human seat identity/ownership changes, not on every
    // RTDB lobby heartbeat. All fetches are parallel and account-scoped.
    public bool NeedsOnlineDiceRefresh(AtlasLobbySnapshot snapshot) =>
        !onlineDiceSession || LobbySignature(snapshot) != lobbyDiceSignature;

    private static string LobbySignature(AtlasLobbySnapshot snapshot)
    {
        if (snapshot == null || snapshot.Members == null) return "";
        List<string> parts = new List<string>();
        foreach (AtlasLobbyMemberSnapshot m in snapshot.Members)
            if (m != null) parts.Add(m.SlotIndex + ":" + m.Active + ":" +
                m.IsHumanSeat + ":" + (m.AccountId ?? ""));
        return string.Join("|", parts);
    }

    // Called by the existing network coordinator when preparing a match or
    // when a human seat changes. Only signed-in owners can publish loadouts.
    public async void PrimeOnlineDiceSkins(AtlasLobbySnapshot snapshot)
    {
        int generation = ++projectionGeneration;
        onlineDiceSession = true;
        localOnlineSlotIndex = -1;
        lobbyDiceSignature = LobbySignature(snapshot);
        for (int i = 0; i < slotDiceIds.Length; i++) slotDiceIds[i] = "";
        if (snapshot == null || snapshot.Members == null) return;
        var lookups = new List<Task<string>>();
        var slots = new List<int>();
        var uids = new List<string>();
        foreach (AtlasLobbyMemberSnapshot member in snapshot.Members)
        {
            if (member == null || !member.Active || !member.IsHumanSeat ||
                member.SlotIndex < 0 || member.SlotIndex >= slotDiceIds.Length ||
                string.IsNullOrEmpty(member.AccountId)) continue;
            slots.Add(member.SlotIndex);
            uids.Add(member.AccountId);
            if (member.AccountId == AtlasBoardRtdbMetaEconomy.CurrentUid)
            {
                localOnlineSlotIndex = member.SlotIndex;
                if (AtlasBoardRtdbMetaEconomy.HasCurrentDiceCache)
                    slotDiceIds[member.SlotIndex] = AtlasBoardRtdbMetaEconomy.CurrentEquippedDiceId;
            }
            lookups.Add(AtlasBoardRtdbMetaEconomy.ReadPublicDiceAsync(member.AccountId));
        }
        try
        {
            string[] projections = await Task.WhenAll(lookups);
            if (generation != projectionGeneration) return;
            for (int i = 0; i < projections.Length; i++)
            {
                if (uids[i] == AtlasBoardRtdbMetaEconomy.CurrentUid &&
                    AtlasBoardRtdbMetaEconomy.HasCurrentDiceCache) continue;
                slotDiceIds[slots[i]] = AtlasBoardRtdbMetaEconomy.IsDice(projections[i])
                    ? projections[i] : "";
            }
        }
        catch (Exception) { /* cosmetic-only default fallback */ }
    }

    // Store/Career preview uses the exact same imported model reference that
    // will later be instantiated during the player’s roll.
    public GameObject GetModelForPreview(string id) => ModelFor(id);

    private GameObject ModelFor(string id)
    {
        switch (id)
        {
            case "dice_ruby_red": return rubyDiceModel;
            case "dice_ocean_blue": return oceanDiceModel;
            case "dice_midnight_black": return midnightDiceModel;
            case "dice_marble": return marbleDiceModel;
            default: return classicDiceModel;
        }
    }

    private int ModelIndex(string id)
    {
        switch (id)
        {
            case "dice_ruby_red": return 1;
            case "dice_ocean_blue": return 2;
            case "dice_midnight_black": return 3;
            case "dice_marble": return 4;
            default: return 0;
        }
    }

    // Phase 19P: runtime Shader.Find("Universal Render Pipeline/Lit") created
    // a non-persisted material that rendered magenta in the owner's Unity
    // project. Only use a persistent, serialized material built by Unity's
    // AssetDatabase, and match it to the active render pipeline. Never display
    // pink geometry if that asset is missing/unsupported: the original
    // numbered dice stay visible (tinted only for human purchased cosmetics).
    public Material GetImportedDiceSurfaceMaterial()
    {
        bool srp = GraphicsSettings.currentRenderPipeline != null;
        Material surface = srp ? importedDiceRenderMaterial : importedDiceAtlasMaterial;
        if (surface == null || surface.shader == null || !surface.shader.isSupported)
        {
            Debug.LogWarning("ATB_DICE_ATLAS status=invalid_render_material" +
                " pipeline=" + (srp ? "srp" : "builtin") +
                " assigned=" + (surface != null) +
                " shaderSupported=" +
                (surface != null && surface.shader != null && surface.shader.isSupported) +
                " fallback=original_pipped_tinted", this);
            return null;
        }
        string shaderName = surface.shader.name;
        // Phase19Q: a package URP/Unlit shader passed isSupported but rendered
        // bright magenta even in the Unity material Inspector. Reject that
        // older material until the Editor binder installs our source shader.
        if (srp && shaderName != "AtlasBoard/Dice/AtlasUnlitURP")
        {
            Debug.LogWarning("ATB_DICE_ATLAS status=old_magenta_shader shader=" + shaderName +
                " fallback=original_pipped_tinted action=run_dice_model_repair", this);
            return null;
        }
        bool isUrp = shaderName.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) ||
                     shaderName == "AtlasBoard/Dice/AtlasUnlitURP";
        if (srp != isUrp)
        {
            Debug.LogWarning("ATB_DICE_ATLAS status=pipeline_mismatch shader=" + shaderName +
                " pipeline=" + (srp ? "srp" : "builtin") +
                " fallback=original_pipped_tinted", this);
            return null;
        }
        Texture albedo = surface.HasProperty("_BaseMap") ? surface.GetTexture("_BaseMap") : null;
        if (albedo == null && surface.HasProperty("_MainTex"))
            albedo = surface.GetTexture("_MainTex");
        if (albedo == null)
        {
            Debug.LogWarning("ATB_DICE_ATLAS status=missing_albedo shader=" + shaderName +
                " fallback=original_pipped_tinted", this);
            return null;
        }
        // Return the same material to Store/Career and gameplay. No temporary
        // URP or Standard shader is synthesized at runtime, avoiding stripping.
        if (activeVisualId == null)
            Debug.Log("ATB_DICE_ATLAS status=ready source=serialized_material" +
                      " shader=" + shaderName + " shaderSupported=True albedo=true" +
                      " pipeline=" + (srp ? "srp" : "builtin"), this);
        return surface;
    }

    // Store and Career 3D previews use the same material as match dice.
    // Never mutate the shared source material or the original classic dice.
    public static void BindImportedSurface(Renderer[] renderers, Material surface)
    {
        if (renderers == null || surface == null) return;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            Material[] mats = renderer.sharedMaterials;
            if (mats == null || mats.Length == 0)
            {
                renderer.sharedMaterial = surface;
                continue;
            }
            for (int n = 0; n < mats.Length; n++) mats[n] = surface;
            renderer.sharedMaterials = mats;
        }
    }

    // If the imported FBX has no UV atlas available, retain the original
    // numbered/pipped mesh and tint it instead of showing an unreadable cube.
    private static Color SkinFallbackTint(string id)
    {
        switch (id)
        {
            case "dice_ruby_red": return new Color32(200, 43, 49, 255);
            case "dice_ocean_blue": return new Color32(35, 114, 210, 255);
            case "dice_midnight_black": return new Color32(53, 54, 63, 255);
            case "dice_marble": return new Color32(195, 197, 199, 255);
            default: return Color.white;
        }
    }

    private void ApplyImportedDice(string id)
    {
        // HARD RULE: bots, unowned/missing loadouts and Classic/Default use
        // the exact pre-Phase19K original numbered dice renderers. Never
        // replace those dice with Dice_White.fbx.
        bool classic = string.IsNullOrEmpty(id);
        Material atlas = classic ? null : GetImportedDiceSurfaceMaterial();
        GameObject prefab = classic ? null : ModelFor(id);
        int modelIndex = ModelIndex(id);
        bool useImported = !classic && prefab != null && atlas != null;
        // Apply the face remap ONLY if actual imported FBX meshes are rendered.
        // Bot/Classic and material-missing pipped fallbacks retain the exact
        // pre-Phase19K face orientation. Always update before early return.
        activeImportedDice = useImported;
        bool alreadyRendered = useImported && renderedModel[0, modelIndex] != null &&
                               renderedModel[1, modelIndex] != null;
        if (activeVisualId == id && originalRenderers != null &&
            (classic || alreadyRendered)) return;
        activeVisualId = id;
        if (!classic && !useImported)
            Debug.LogWarning("ATB_DICE_SKIN_FALLBACK skin=" + id +
                " reason=" + (prefab == null ? "model_missing" : "texture_atlas_missing") +
                " render=original_pipped_tinted", this);

        Transform[] roots = { dieOne, dieTwo };
        if (originalRenderers == null)
        {
            originalRenderers = new Renderer[2][];
            for (int i = 0; i < 2; i++)
                originalRenderers[i] = roots[i] != null
                    ? roots[i].GetComponentsInChildren<Renderer>(true)
                    : new Renderer[0];
        }
        for (int i = 0; i < 2; i++)
        {
            if (roots[i] == null) continue;
            GameObject sample = useImported ? renderedModel[i, modelIndex] : null;
            if (useImported && sample == null)
            {
                sample = Instantiate(prefab, roots[i], false);
                sample.name = "ATB_DiceModel_" + modelIndex;
                sample.transform.localPosition = Vector3.zero;
                sample.transform.localRotation = Quaternion.identity;
                Renderer[] imported = sample.GetComponentsInChildren<Renderer>(true);
                BindImportedSurface(imported, atlas);
                // Imported models are fitted to the original animated roots.
                if (imported.Length > 0 && originalRenderers[i].Length > 0)
                {
                    Bounds source = originalRenderers[i][0].bounds;
                    foreach (Renderer r in originalRenderers[i])
                        if (r != null) source.Encapsulate(r.bounds);
                    Bounds target = imported[0].bounds;
                    foreach (Renderer r in imported)
                        if (r != null) target.Encapsulate(r.bounds);
                    if (target.size.magnitude > .00001f)
                    {
                        float fit = Mathf.Clamp(source.size.magnitude / target.size.magnitude, .002f, 120f);
                        sample.transform.localScale *= fit;
                        Bounds scaled = imported[0].bounds;
                        foreach (Renderer r in imported)
                            if (r != null) scaled.Encapsulate(r.bounds);
                        sample.transform.localPosition += roots[i].InverseTransformVector(source.center - scaled.center);
                    }
                }
                foreach (Collider c in sample.GetComponentsInChildren<Collider>(true))
                    c.enabled = false;
                renderedModel[i, modelIndex] = sample;
            }
            foreach (Renderer r in originalRenderers[i])
            {
                if (r == null) continue;
                r.enabled = !useImported;
                // Remove any previous tint before returning to original white.
                if (classic || useImported) r.SetPropertyBlock(null);
                else
                {
                    var block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block);
                    Color color = SkinFallbackTint(id);
                    block.SetColor("_Color", color);
                    block.SetColor("_BaseColor", color);
                    r.SetPropertyBlock(block);
                }
            }
            for (int k = 0; k < 5; k++)
                if (renderedModel[i, k] != null)
                    renderedModel[i, k].SetActive(useImported && k == modelIndex);
        }
        Debug.Log("ATB_DICE_SKIN_RENDER skin=" + (classic ? "classic" : id) +
                  " originalDefault=" + classic +
                  " atlas=" + (atlas != null) +
                  " imported=" + useImported, this);
    }

    [ContextMenu("Preview Debug Face")]
    public void PreviewDebugFace()
    {
        Initialize();

        if (!ValidateDice())
        {
            return;
        }

        SetDiceVisible(true);

        Quaternion faceRotation =
            GetFaceRotation(
                debugPreviewFace);

        dieOne.localPosition =
            dieOneRestLocalPosition;

        dieTwo.localPosition =
            dieTwoRestLocalPosition;

        dieOne.localRotation =
            faceRotation;

        dieTwo.localRotation =
            faceRotation;

        dieOne.localScale =
            dieOneRestLocalScale;

        dieTwo.localScale =
            dieTwoRestLocalScale;

        Debug.Log(
            $"Dice preview face: {debugPreviewFace}. " +
            "Confirm that this exact number is on TOP.",
            this);
    }

    [ContextMenu("Hide Dice")]
    public void HideDice()
    {
        SetDiceVisible(false);
    }

    private IEnumerator RollRoutine(
        int dieOneValue,
        int dieTwoValue,
        Action onCompleted)
    {
        SetDiceVisible(true);

        dieOne.localScale =
            dieOneRestLocalScale;

        dieTwo.localScale =
            dieTwoRestLocalScale;

        Vector3 dieOneStartPosition =
            dieOneRestLocalPosition +
            Vector3.up *
            launchHeight +
            new Vector3(
                -lateralScatter,
                0f,
                lateralScatter);

        Vector3 dieTwoStartPosition =
            dieTwoRestLocalPosition +
            Vector3.up *
            launchHeight +
            new Vector3(
                lateralScatter,
                0f,
                -lateralScatter);

        dieOne.localPosition =
            dieOneStartPosition;

        dieTwo.localPosition =
            dieTwoStartPosition;

        Quaternion dieOneStartRotation =
            UnityEngine.Random.rotation;

        Quaternion dieTwoStartRotation =
            UnityEngine.Random.rotation;

        Quaternion dieOneTargetRotation =
            GetFaceRotation(
                dieOneValue);

        Quaternion dieTwoTargetRotation =
            GetFaceRotation(
                dieTwoValue);

        Vector3 dieOneSpinAxis =
            new Vector3(
                1f,
                0.73f,
                0.41f)
            .normalized;

        Vector3 dieTwoSpinAxis =
            new Vector3(
                0.51f,
                1f,
                0.82f)
            .normalized;

        float elapsed = 0f;

        while (elapsed < rollDuration)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    rollDuration);

            float smoothT =
                t * t *
                (3f - 2f * t);

            float arc =
                Mathf.Sin(
                    t * Mathf.PI) *
                arcHeight;

            dieOne.localPosition =
                Vector3.Lerp(
                    dieOneStartPosition,
                    dieOneRestLocalPosition,
                    smoothT) +
                Vector3.up * arc;

            dieTwo.localPosition =
                Vector3.Lerp(
                    dieTwoStartPosition,
                    dieTwoRestLocalPosition,
                    smoothT) +
                Vector3.up * arc;

            float remainingSpin =
                (1f - smoothT) *
                spinTurns *
                360f;

            Quaternion dieOneSpin =
                Quaternion.AngleAxis(
                    remainingSpin,
                    dieOneSpinAxis);

            Quaternion dieTwoSpin =
                Quaternion.AngleAxis(
                    -remainingSpin,
                    dieTwoSpinAxis);

            dieOne.localRotation =
                Quaternion.Slerp(
                    dieOneStartRotation *
                    dieOneSpin,
                    dieOneTargetRotation,
                    smoothT);

            dieTwo.localRotation =
                Quaternion.Slerp(
                    dieTwoStartRotation *
                    dieTwoSpin,
                    dieTwoTargetRotation,
                    smoothT);

            yield return null;
        }

        // Snap to exact final values. The visible result can never
        // disagree with the gameplay result passed by TurnManager.
        dieOne.localPosition =
            dieOneRestLocalPosition;

        dieTwo.localPosition =
            dieTwoRestLocalPosition;

        dieOne.localRotation =
            dieOneTargetRotation;

        dieTwo.localRotation =
            dieTwoTargetRotation;

        Debug.Log(
            $"Dice visual result: " +
            $"{dieOneValue} + {dieTwoValue} = " +
            $"{dieOneValue + dieTwoValue}.",
            this);

        if (resultHoldDuration > 0f)
        {
            yield return new WaitForSeconds(
                resultHoldDuration);
        }

        if (hideWhenIdle)
        {
            SetDiceVisible(false);
        }

        rollCoroutine = null;

        onCompleted?.Invoke();
    }

    private Quaternion GetFaceRotation(
        int value)
    {
        if (faceLocalEulerRotations == null ||
            faceLocalEulerRotations.Length < 6)
        {
            return Quaternion.identity;
        }

        int index =
            Mathf.Clamp(
                value - 1,
                0,
                5);

        // Scene-authored faceLocalEulerRotations calibrate the original
        // white/black-pip die.  The imported red/blue/black/marble models
        // share a different orientation. Their inverse face permutation is
        // derived from observed top-face results, not from cosmetic IDs.
        if (activeImportedDice)
            index = ImportedFaceToClassicRotationIndex[index];

        return Quaternion.Euler(
            faceLocalEulerRotations[index]);
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        if (dieOne != null)
        {
            dieOneRestLocalPosition =
                dieOne.localPosition;

            dieOneRestLocalScale =
                dieOne.localScale;
        }

        if (dieTwo != null)
        {
            dieTwoRestLocalPosition =
                dieTwo.localPosition;

            dieTwoRestLocalScale =
                dieTwo.localScale;
        }
    }

    private bool ValidateDice()
    {
        if (dieOne != null &&
            dieTwo != null)
        {
            return true;
        }

        Debug.LogError(
            "DiceVisualController requires Die One and Die Two.",
            this);

        return false;
    }

    private void SetDiceVisible(
        bool visible)
    {
        if (dieOne != null)
        {
            dieOne.gameObject.SetActive(
                visible);
        }

        if (dieTwo != null)
        {
            dieTwo.gameObject.SetActive(
                visible);
        }
    }
}
