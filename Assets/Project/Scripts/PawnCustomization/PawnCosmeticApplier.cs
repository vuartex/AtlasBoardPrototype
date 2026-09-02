using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-2000)]
[DisallowMultipleComponent]
public class PawnCosmeticApplier :
    MonoBehaviour
{
    [SerializeField]
    private PlayerGameState playerState;

    [SerializeField]
    private PlayerPawnMover pawnMover;

    [Header("Existing Pawn Visual")]
    [SerializeField]
    private Renderer[] legacyRenderers;

    [Header("Placement")]
    [SerializeField, Min(0f)]
    private float pawnHeightOffset =
        0.7f;

    [SerializeField]
    private float identityDiscRadius =
        0.34f;

    [SerializeField]
    private float identityDiscHeight =
        0.04f;

    private PawnMotionAnimator
        pawnMotionAnimator;

    private Transform cosmeticMount;
    private Transform visualMotionRoot;

    private GameObject currentVisual;
    private PawnCosmeticDefinition currentCosmetic;
    private GameObject identityDisc;
    private Material identityMaterial;
    private bool onlineAuthoritativeCosmeticActive;
    private string lastOnlineAuthoritativeCosmeticId = string.Empty;

    public GameObject CurrentVisual =>
        currentVisual;

    public PawnCosmeticDefinition
        CurrentCosmetic =>
            currentCosmetic;

    public Transform VisualMotionRoot =>
        visualMotionRoot;

    private void Awake()
    {
        ResolveReferences();
        EnsureMount();

        AtlasBoardPawnSelectionStore
            .SelectionChanged +=
                HandleSelectionChanged;

        ApplySelection();
    }

    private void OnDestroy()
    {
        AtlasBoardPawnSelectionStore
            .SelectionChanged -=
                HandleSelectionChanged;

        if (identityMaterial != null)
        {
            Destroy(
                identityMaterial);
        }
    }

    public void ApplySelection()
    {
        ResolveReferences();
        EnsureMount();

        AtlasBoardPawnCosmeticService service =
            AtlasBoardPawnCosmeticService
                .Instance;

        if (playerState == null ||
            service == null)
        {
            RestoreLegacyVisual();

            return;
        }

        PawnCosmeticDefinition cosmetic =
            service.GetSelectedCosmetic(
                playerState
                    .PlayerSlotIndex);

        if (cosmetic == null ||
            cosmetic.Prefab == null)
        {
            RestoreLegacyVisual();

            return;
        }

        ReplaceCosmetic(
            cosmetic);

        EnsureIdentityDisc();

        if (pawnMover != null)
        {
            pawnMover
                .RefreshPawnVisibilityCache();

            EnforceVisibility(
                pawnMover.IsPawnVisible);
        }
        else
        {
            EnforceVisibility(
                true);
        }
    }

    public bool ApplyOnlineAuthoritativeCosmeticId(
        string cosmeticId)
    {
        ResolveReferences();
        EnsureMount();

        AtlasBoardPawnCosmeticService service =
            AtlasBoardPawnCosmeticService.Instance;

        string normalizedCosmeticId =
            cosmeticId?.Trim() ?? string.Empty;

        if (service == null ||
            service.Catalog == null ||
            string.IsNullOrWhiteSpace(normalizedCosmeticId))
        {
            return false;
        }

        onlineAuthoritativeCosmeticActive = true;

        // Network snapshots are revisioned frequently while another pawn moves.
        // Do not Rebind/recreate an unchanged cosmetic on every snapshot; doing
        // so resets the idle motion root and can look like stationary pawns are
        // repeatedly spinning/reloading.
        if (currentVisual != null &&
            string.Equals(
                lastOnlineAuthoritativeCosmeticId,
                normalizedCosmeticId,
                System.StringComparison.Ordinal))
        {
            return true;
        }

        PawnCosmeticDefinition cosmetic =
            service.Catalog.FindById(normalizedCosmeticId);

        if (cosmetic == null ||
            cosmetic.Prefab == null)
        {
            return false;
        }

        if (currentCosmetic != null &&
            currentVisual != null &&
            string.Equals(
                currentCosmetic.CosmeticId,
                cosmetic.CosmeticId,
                System.StringComparison.Ordinal))
        {
            lastOnlineAuthoritativeCosmeticId =
                normalizedCosmeticId;
            return true;
        }

        ReplaceCosmetic(cosmetic);
        lastOnlineAuthoritativeCosmeticId =
            normalizedCosmeticId;
        EnsureIdentityDisc();

        if (pawnMover != null)
        {
            pawnMover.RefreshPawnVisibilityCache();
            EnforceVisibility(
                pawnMover.IsPawnVisible);
        }
        else
        {
            EnforceVisibility(true);
        }

        return true;
    }

    private void HandleSelectionChanged(
        int playerSlotIndex,
        string cosmeticId)
    {
        if (onlineAuthoritativeCosmeticActive ||
            playerState == null ||
            playerState.PlayerSlotIndex !=
            playerSlotIndex)
        {
            return;
        }

        ApplySelection();
    }

    private void ReplaceCosmetic(
        PawnCosmeticDefinition cosmetic)
    {
        if (pawnMotionAnimator != null)
        {
            pawnMotionAnimator
                .ClearCosmeticVisual();
        }

        if (currentVisual != null)
        {
            currentVisual.SetActive(
                false);

            Destroy(
                currentVisual);
        }

        currentCosmetic =
            cosmetic;

        EnsureMount();

        visualMotionRoot.localPosition =
            Vector3.zero;

        visualMotionRoot.localRotation =
            Quaternion.identity;

        visualMotionRoot.localScale =
            Vector3.one;

        currentVisual =
            Instantiate(
                cosmetic.Prefab,
                visualMotionRoot);

        currentVisual.name =
            "PawnCosmetic_" +
            cosmetic.CosmeticId;

        Transform visualTransform =
            currentVisual.transform;

        visualTransform.localPosition =
            Vector3.zero;

        visualTransform.localRotation =
            Quaternion.Euler(
                cosmetic.RotationOffset);

        visualTransform.localScale =
            Vector3.one;

        DisablePhysics(
            currentVisual);

        FitModelToMount(
            currentVisual,
            cosmetic.DesiredWorldHeight);

        if (pawnMotionAnimator != null)
        {
            pawnMotionAnimator
                .BindCosmeticVisual(
                    currentVisual,
                    visualMotionRoot,
                    cosmetic.DefaultMotionSet);
        }
    }

    private void FitModelToMount(
        GameObject model,
        float desiredHeight)
    {
        if (model == null)
        {
            return;
        }

        if (!TryGetBounds(
                model,
                out Bounds bounds))
        {
            return;
        }

        float currentHeight =
            Mathf.Max(
                0.001f,
                bounds.size.y);

        float scale =
            Mathf.Clamp(
                desiredHeight /
                currentHeight,
                0.02f,
                20f);

        model.transform.localScale *=
            scale;

        if (!TryGetBounds(
                model,
                out bounds))
        {
            return;
        }

        float desiredBottomY =
            cosmeticMount.position.y +
            identityDiscHeight;

        float shift =
            desiredBottomY -
            bounds.min.y;

        model.transform.position +=
            Vector3.up *
            shift;
    }

    private static bool TryGetBounds(
        GameObject root,
        out Bounds bounds)
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<
                Renderer>(
                    true);

        bool found = false;
        bounds =
            new Bounds(
                root.transform.position,
                Vector3.zero);

        foreach (Renderer renderer
                 in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!found)
            {
                bounds =
                    renderer.bounds;

                found = true;
            }
            else
            {
                bounds.Encapsulate(
                    renderer.bounds);
            }
        }

        return found;
    }

    private static void DisablePhysics(
        GameObject root)
    {
        Collider[] colliders =
            root.GetComponentsInChildren<
                Collider>(
                    true);

        foreach (Collider collider
                 in colliders)
        {
            if (collider != null)
            {
                collider.enabled =
                    false;
            }
        }

        Rigidbody[] bodies =
            root.GetComponentsInChildren<
                Rigidbody>(
                    true);

        foreach (Rigidbody body
                 in bodies)
        {
            if (body == null)
            {
                continue;
            }

            body.isKinematic =
                true;

            body.useGravity =
                false;
        }
    }

    private void EnsureIdentityDisc()
    {
        if (identityDisc == null)
        {
            identityDisc =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cylinder);

            identityDisc.name =
                "PawnIdentityDisc";

            identityDisc.transform
                .SetParent(
                    cosmeticMount,
                    false);

            Collider collider =
                identityDisc.GetComponent<
                    Collider>();

            if (collider != null)
            {
                Destroy(
                    collider);
            }

            Renderer renderer =
                identityDisc.GetComponent<
                    Renderer>();

            Shader shader =
                Shader.Find(
                    "Standard");

            if (shader == null)
            {
                shader =
                    Shader.Find(
                        "Unlit/Color");
            }

            if (shader != null)
            {
                identityMaterial =
                    new Material(
                        shader);

                renderer.sharedMaterial =
                    identityMaterial;
            }
        }

        identityDisc.transform
            .localPosition =
                new Vector3(
                    0f,
                    identityDiscHeight *
                    0.5f,
                    0f);

        identityDisc.transform
            .localScale =
                new Vector3(
                    identityDiscRadius *
                    2f,
                    identityDiscHeight *
                    0.5f,
                    identityDiscRadius *
                    2f);

        if (identityMaterial != null &&
            playerState != null)
        {
            identityMaterial.color =
                playerState.UIColor;
        }

        identityDisc.SetActive(
            true);
    }

    private void RestoreLegacyVisual()
    {
        if (pawnMotionAnimator != null)
        {
            pawnMotionAnimator
                .ClearCosmeticVisual();
        }

        if (currentVisual != null)
        {
            currentVisual.SetActive(
                false);

            Destroy(
                currentVisual);

            currentVisual =
                null;
        }

        currentCosmetic =
            null;

        if (identityDisc != null)
        {
            identityDisc.SetActive(
                false);
        }

        if (pawnMover != null)
        {
            pawnMover
                .RefreshPawnVisibilityCache();

            EnforceVisibility(
                pawnMover.IsPawnVisible);
        }
        else
        {
            EnforceVisibility(
                true);
        }
    }

    /// <summary>
    /// Re-applies the intended visual state after PlayerPawnMover refreshes
    /// its renderer/collider cache. This keeps the hidden prototype pawn
    /// renderers from being re-enabled underneath the selected cosmetic.
    /// </summary>
    public void EnforceVisibility(
        bool pawnVisible)
    {
        RefreshLegacyRendererCache();

        bool hasCosmetic =
            currentVisual != null &&
            currentCosmetic != null;

        SetLegacyVisualEnabled(
            pawnVisible &&
            !hasCosmetic);

        if (currentVisual != null)
        {
            currentVisual.SetActive(
                pawnVisible &&
                hasCosmetic);
        }

        if (identityDisc != null)
        {
            identityDisc.SetActive(
                pawnVisible &&
                hasCosmetic);
        }
    }

    private void SetLegacyVisualEnabled(
        bool enabledValue)
    {
        if (legacyRenderers == null)
        {
            return;
        }

        foreach (Renderer renderer
                 in legacyRenderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled =
                enabledValue;

            // The prototype pawns were primitive meshes, so their collider
            // normally lives on the same GameObject as the renderer.
            // Disable only those matching colliders; do not touch unrelated
            // gameplay colliders on the pawn root.
            Collider[] legacyColliders =
                renderer.GetComponents<
                    Collider>();

            foreach (Collider collider
                     in legacyColliders)
            {
                if (collider != null)
                {
                    collider.enabled =
                        enabledValue;
                }
            }
        }
    }

    private void RefreshLegacyRendererCache()
    {
        Renderer[] discovered =
            GetComponentsInChildren<
                Renderer>(
                    true);

        List<Renderer> filtered =
            new List<Renderer>();

        foreach (Renderer renderer
                 in discovered)
        {
            if (renderer == null ||
                IsInsideCosmeticMount(
                    renderer.transform))
            {
                continue;
            }

            if (!filtered.Contains(
                    renderer))
            {
                filtered.Add(
                    renderer);
            }
        }

        legacyRenderers =
            filtered.ToArray();
    }

    private bool IsInsideCosmeticMount(
        Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Transform current =
            candidate;

        while (current != null &&
               current != transform)
        {
            if (current == cosmeticMount ||
                current.name ==
                    "PawnCosmeticMount")
            {
                return true;
            }

            current =
                current.parent;
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (playerState == null)
        {
            playerState =
                GetComponent<
                    PlayerGameState>();
        }

        if (pawnMover == null)
        {
            pawnMover =
                GetComponent<
                    PlayerPawnMover>();
        }

        if (pawnMotionAnimator == null)
        {
            pawnMotionAnimator =
                GetComponent<
                    PawnMotionAnimator>();
        }
    }

    private void EnsureMount()
    {
        if (cosmeticMount == null)
        {
            Transform existing =
                transform.Find(
                    "PawnCosmeticMount");

            if (existing != null)
            {
                cosmeticMount =
                    existing;
            }
            else
            {
                GameObject mount =
                    new GameObject(
                        "PawnCosmeticMount");

                cosmeticMount =
                    mount.transform;

                cosmeticMount.SetParent(
                    transform,
                    false);
            }
        }

        cosmeticMount.localPosition =
            new Vector3(
                0f,
                -pawnHeightOffset,
                0f);

        cosmeticMount.localRotation =
            Quaternion.identity;

        cosmeticMount.localScale =
            Vector3.one;

        if (visualMotionRoot == null)
        {
            Transform existingVisualRoot =
                cosmeticMount.Find(
                    "PawnVisualMotionRoot");

            if (existingVisualRoot != null)
            {
                visualMotionRoot =
                    existingVisualRoot;
            }
            else
            {
                GameObject visualRoot =
                    new GameObject(
                        "PawnVisualMotionRoot");

                visualMotionRoot =
                    visualRoot.transform;

                visualMotionRoot.SetParent(
                    cosmeticMount,
                    false);
            }
        }

        visualMotionRoot.localPosition =
            Vector3.zero;

        visualMotionRoot.localRotation =
            Quaternion.identity;

        visualMotionRoot.localScale =
            Vector3.one;

        RefreshLegacyRendererCache();
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        PlayerGameState state,
        PlayerPawnMover mover,
        Renderer[] renderers,
        float heightOffset)
    {
        playerState =
            state;

        pawnMover =
            mover;

        legacyRenderers =
            renderers;

        pawnHeightOffset =
            Mathf.Max(
                0f,
                heightOffset);
    }
#endif
}
