using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AtlasBoardPawnCustomizationUI :
    MonoBehaviour
{
    [SerializeField]
    private PawnCosmeticCatalog catalog;

    [Header("Modal")]
    [SerializeField]
    private GameObject modalRoot;

    [SerializeField]
    private TMP_Text playerText;

    [SerializeField]
    private TMP_Text selectionText;

    [SerializeField]
    private RawImage previewImage;

    [SerializeField]
    private Button previousButton;

    [SerializeField]
    private Button nextButton;

    [SerializeField]
    private Button applyButton;

    [SerializeField]
    private Button cancelButton;

    private int currentSlotIndex;
    private int currentCosmeticIndex;
    private string originalCosmeticId;
    private bool currentReadOnly;

    private RenderTexture previewTexture;
    private GameObject previewStage;
    private Transform previewModelRoot;
    private GameObject previewModel;
    private Camera previewCamera;
    private AtlasBoardLobbyRuntimeBridge
        subscribedLobbyBridge;

    private void Awake()
    {
        HookButtons();
        SubscribeToLobbyBridge();

        if (modalRoot != null)
        {
            modalRoot.SetActive(
                false);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromLobbyBridge();
        UnhookButtons();
        DestroyPreviewStage();
    }

    private void Update()
    {
        if (previewModelRoot == null ||
            modalRoot == null ||
            !modalRoot.activeSelf)
        {
            return;
        }

        previewModelRoot.Rotate(
            Vector3.up,
            24f *
            Time.unscaledDeltaTime,
            Space.World);
    }

    public void OpenForSlot(
        int playerSlotIndex)
    {
        if (catalog == null ||
            catalog.Count == 0 ||
            modalRoot == null)
        {
            return;
        }

        SubscribeToLobbyBridge();

        currentSlotIndex =
            Mathf.Clamp(
                playerSlotIndex,
                0,
                3);

        if (!CanOpenOnlineSlot(
                currentSlotIndex,
                out bool editable))
        {
            return;
        }

        currentReadOnly =
            !editable;

        originalCosmeticId =
            ResolveVisibleCosmeticId(
                currentSlotIndex,
                currentReadOnly);

        currentCosmeticIndex =
            catalog.IndexOf(
                originalCosmeticId);

        if (currentCosmeticIndex < 0)
        {
            currentCosmeticIndex =
                Mathf.Clamp(
                    currentSlotIndex,
                    0,
                    catalog.Count - 1);
        }

        modalRoot.SetActive(
            true);

        modalRoot.transform
            .SetAsLastSibling();

        EnsurePreviewStage();
        RefreshEditability();
        RefreshPreview();
    }

    public void CloseWithoutApplying()
    {
        if (modalRoot != null)
        {
            modalRoot.SetActive(
                false);
        }
    }

    private void SelectPrevious()
    {
        if (currentReadOnly)
        {
            return;
        }

        if (catalog == null ||
            catalog.Count == 0)
        {
            return;
        }

        currentCosmeticIndex =
            (currentCosmeticIndex -
             1 +
             catalog.Count) %
            catalog.Count;

        RefreshPreview();
    }

    private void SelectNext()
    {
        if (currentReadOnly)
        {
            return;
        }

        if (catalog == null ||
            catalog.Count == 0)
        {
            return;
        }

        currentCosmeticIndex =
            (currentCosmeticIndex +
             1) %
            catalog.Count;

        RefreshPreview();
    }

    private async void ApplySelection()
    {
        if (currentReadOnly)
        {
            return;
        }

        if (catalog == null ||
            catalog.Count == 0)
        {
            return;
        }

        PawnCosmeticDefinition cosmetic =
            catalog.GetByIndex(
                currentCosmeticIndex);

        if (cosmetic == null)
            return;
        if (AtlasBoardRtdbMetaEconomy.IsPaidPawn(cosmetic.CosmeticId) &&
            !AtlasBoardRtdbMetaEconomy.IsPawnOwnedOnThisAccount(cosmetic.CosmeticId))
        {
            Debug.LogWarning("ATB_PAWN_SELECT locked; purchase first in Atlas Store: " + cosmetic.CosmeticId);
            RefreshPreview();
            return;
        }

        AtlasBoardPawnCosmeticService service =
            AtlasBoardPawnCosmeticService
                .Instance;

        AtlasBoardLobbyRuntimeBridge bridge = FindLobbyBridge();
        if (bridge != null && bridge.HasLobby)
        {
            // Remote operations are authoritative. Do not cache a choice
            // locally and falsely show it as committed on an RTDB denial.
            AtlasLobbyOperationResult syncResult =
                await bridge.SetPawnCosmeticAsync(
                    currentSlotIndex,
                    cosmetic.CosmeticId);
            if (!syncResult.Success)
            {
                Debug.LogWarning(
                    "ATB_PAWN_SELECT denied; lobby remains authoritative: " +
                    syncResult.TechnicalMessage, this);
                RefreshPreview();
                return;
            }
        }

        if (service != null)
            service.SelectCosmetic(currentSlotIndex, cosmetic.CosmeticId);
        else
            AtlasBoardPawnSelectionStore.SetSelectedId(
                currentSlotIndex, cosmetic.CosmeticId);

        originalCosmeticId = cosmetic.CosmeticId;

        if (modalRoot != null)
        {
            modalRoot.SetActive(
                false);
        }
    }

    private void SubscribeToLobbyBridge()
    {
        AtlasBoardLobbyRuntimeBridge bridge =
            FindLobbyBridge();

        if (bridge == subscribedLobbyBridge)
        {
            return;
        }

        UnsubscribeFromLobbyBridge();

        subscribedLobbyBridge = bridge;

        if (subscribedLobbyBridge != null)
        {
            subscribedLobbyBridge.SnapshotChanged +=
                HandleLobbySnapshotChanged;
        }
    }

    private void UnsubscribeFromLobbyBridge()
    {
        if (subscribedLobbyBridge != null)
        {
            subscribedLobbyBridge.SnapshotChanged -=
                HandleLobbySnapshotChanged;
        }

        subscribedLobbyBridge = null;
    }

    private void HandleLobbySnapshotChanged(
        AtlasLobbySnapshot snapshot)
    {
        if (!currentReadOnly ||
            modalRoot == null ||
            !modalRoot.activeSelf ||
            snapshot == null ||
            snapshot.Members == null ||
            catalog == null)
        {
            return;
        }

        AtlasLobbyMemberSnapshot member =
            snapshot.Members.Find(
                item =>
                    item != null &&
                    item.Active &&
                    item.SlotIndex ==
                        currentSlotIndex);

        if (member == null ||
            string.IsNullOrWhiteSpace(
                member.PawnCosmeticId))
        {
            return;
        }

        int networkIndex =
            catalog.IndexOf(
                member.PawnCosmeticId);

        if (networkIndex < 0 ||
            networkIndex == currentCosmeticIndex)
        {
            return;
        }

        originalCosmeticId =
            member.PawnCosmeticId;
        currentCosmeticIndex =
            networkIndex;
        RefreshPreview();
    }

    private string ResolveVisibleCosmeticId(
        int slotIndex,
        bool readOnly)
    {
        AtlasBoardLobbyRuntimeBridge bridge =
            FindLobbyBridge();

        if (bridge != null &&
            bridge.HasLobby &&
            bridge.CurrentSnapshot != null &&
            bridge.CurrentSnapshot.Members != null)
        {
            AtlasLobbyMemberSnapshot member =
                bridge.CurrentSnapshot.Members.Find(
                    item =>
                        item != null &&
                        item.Active &&
                        item.SlotIndex == slotIndex);

            if (member != null)
            {
                if (!string.IsNullOrWhiteSpace(
                        member.PawnCosmeticId) &&
                    (readOnly ||
                     member.SeatMode == AtlasLobbySeatMode.Bot))
                {
                    return member.PawnCosmeticId;
                }

                // An online seat that has not committed a pawn yet must not
                // borrow another device's slot-local PlayerPrefs value.
                // Read-only viewers use the same deterministic catalog default
                // until the owning client publishes its real selection.
                if (readOnly &&
                    catalog != null &&
                    catalog.Count > 0)
                {
                    PawnCosmeticDefinition deterministicDefault =
                        catalog.GetByIndex(
                            Mathf.Clamp(
                                slotIndex,
                                0,
                                catalog.Count - 1));

                    if (deterministicDefault != null)
                    {
                        return deterministicDefault.CosmeticId;
                    }
                }
            }
        }

        return AtlasBoardPawnSelectionStore
            .GetSelectedId(
                slotIndex,
                catalog);
    }

    private void RefreshEditability()
    {
        if (previousButton != null)
        {
            previousButton.interactable =
                !currentReadOnly;
        }

        if (nextButton != null)
        {
            nextButton.interactable =
                !currentReadOnly;
        }

        if (applyButton != null)
        {
            applyButton.interactable =
                !currentReadOnly;

            applyButton.gameObject.SetActive(
                !currentReadOnly);
        }
    }

    private bool CanOpenOnlineSlot(
        int slotIndex,
        out bool editable)
    {
        editable = true;

        AtlasBoardLobbyRuntimeBridge bridge =
            FindLobbyBridge();

        if (bridge == null ||
            !bridge.HasLobby ||
            bridge.CurrentSnapshot == null)
        {
            // The local account controls slot 0. Other local seats are
            // not independently authenticated and must not inherit host
            // permission to reconfigure bots or other people's pawns.
            editable = slotIndex == 0;
            return true;
        }

        AtlasLobbySnapshot snapshot =
            bridge.CurrentSnapshot;

        AtlasLobbyMemberSnapshot member =
            snapshot.Members != null
                ? snapshot.Members.Find(
                    item =>
                        item != null &&
                        item.Active &&
                        item.SlotIndex ==
                            slotIndex)
                : null;

        if (member == null)
        {
            editable = false;
            return false;
        }

        string localAccountId =
            bridge.CurrentAccountId ?? string.Empty;

        bool localIsHost =
            string.Equals(
                snapshot.HostAccountId,
                localAccountId,
                StringComparison.Ordinal);

        bool ownsRemoteHuman =
            member.SeatMode ==
                AtlasLobbySeatMode.RemoteHuman &&
            string.Equals(
                member.AccountId,
                localAccountId,
                StringComparison.Ordinal);

        bool hostOwnedLocalHuman =
            localIsHost &&
            (member.SeatMode ==
                 AtlasLobbySeatMode.HostLocal ||
             member.SeatMode ==
                 AtlasLobbySeatMode.LocalHuman);

        bool botSeat =
            member.SeatMode ==
            AtlasLobbySeatMode.Bot;

        if (ownsRemoteHuman ||
            hostOwnedLocalHuman)
        {
            editable = true;
            return true;
        }

        if (botSeat)
        {
            // The host cannot customize bot pawns. Bots keep their own
            // deterministic/previously assigned cosmetic in the room.
            editable = false;
            return true;
        }

        // Other real Human seats remain inspectable, but read-only.
        editable = false;
        return true;
    }

    private static AtlasBoardLobbyRuntimeBridge
        FindLobbyBridge()
    {
        AtlasBoardLobbyRuntimeBridge[] bridges =
            Resources.FindObjectsOfTypeAll<
                AtlasBoardLobbyRuntimeBridge>();

        foreach (AtlasBoardLobbyRuntimeBridge bridge
                 in bridges)
        {
            if (bridge != null &&
                bridge.gameObject.scene.IsValid())
            {
                return bridge;
            }
        }

        return null;
    }

    private void RefreshPreview()
    {
        if (catalog == null ||
            catalog.Count == 0)
        {
            return;
        }

        PawnCosmeticDefinition cosmetic =
            catalog.GetByIndex(
                currentCosmeticIndex);

        if (playerText != null)
        {
            playerText.text =
                AtlasBoardL.T(
                    "pawn.customization.player",
                    currentSlotIndex + 1);
        }

        if (selectionText != null)
        {
            selectionText.text =
                AtlasBoardL.T(
                    "pawn.customization.selection",
                    currentCosmeticIndex + 1,
                    catalog.Count);
        }

        ShowPreviewModel(
            cosmetic);
        bool paidUnowned = cosmetic != null &&
            AtlasBoardRtdbMetaEconomy.IsPaidPawn(cosmetic.CosmeticId) &&
            !AtlasBoardRtdbMetaEconomy.IsPawnOwnedOnThisAccount(cosmetic.CosmeticId);
        if (selectionText != null && paidUnowned)
        {
            string lang = AtlasBoardLocalizationManager.Instance != null
                ? AtlasBoardLocalizationManager.Instance.CurrentLanguageCode : "en";
            selectionText.text += lang == "tr" ? "  •  MAĞAZADAN AL" :
                lang == "es" ? "  •  COMPRAR" :
                lang == "fr" ? "  •  ACHETER" :
                lang == "de" ? "  •  KAUFEN" :
                lang == "ko" ? "  •  구매 필요" :
                lang == "ru" ? "  •  КУПИТЬ" : "  •  BUY IN STORE";
        }
        if (applyButton != null)
            applyButton.interactable = !currentReadOnly && !paidUnowned;
    }

    private void HookButtons()
    {
        if (previousButton != null)
        {
            previousButton.onClick
                .AddListener(
                    SelectPrevious);
        }

        if (nextButton != null)
        {
            nextButton.onClick
                .AddListener(
                    SelectNext);
        }

        if (applyButton != null)
        {
            applyButton.onClick
                .AddListener(
                    ApplySelection);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick
                .AddListener(
                    CloseWithoutApplying);
        }
    }

    private void UnhookButtons()
    {
        if (previousButton != null)
        {
            previousButton.onClick
                .RemoveListener(
                    SelectPrevious);
        }

        if (nextButton != null)
        {
            nextButton.onClick
                .RemoveListener(
                    SelectNext);
        }

        if (applyButton != null)
        {
            applyButton.onClick
                .RemoveListener(
                    ApplySelection);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick
                .RemoveListener(
                    CloseWithoutApplying);
        }
    }

    private void EnsurePreviewStage()
    {
        if (previewStage != null)
        {
            return;
        }

        previewTexture =
            new RenderTexture(
                512,
                512,
                24,
                RenderTextureFormat.ARGB32);

        previewTexture.name =
            "PawnCustomizationPreviewRT";

        previewTexture.Create();

        previewStage =
            new GameObject(
                "PawnCustomizationPreviewStage");

        previewStage.hideFlags =
            HideFlags.HideAndDontSave;

        previewStage.transform.position =
            new Vector3(
                0f,
                -5000f,
                0f);

        GameObject modelRoot =
            new GameObject(
                "PreviewModelRoot");

        previewModelRoot =
            modelRoot.transform;

        previewModelRoot.SetParent(
            previewStage.transform,
            false);

        GameObject cameraObject =
            new GameObject(
                "PreviewCamera");

        cameraObject.transform
            .SetParent(
                previewStage.transform,
                false);

        previewCamera =
            cameraObject.AddComponent<
                Camera>();

        previewCamera.clearFlags =
            CameraClearFlags.SolidColor;

        previewCamera.backgroundColor =
            new Color(
                0.055f,
                0.075f,
                0.09f,
                1f);

        previewCamera.orthographic =
            false;

        previewCamera.fieldOfView =
            32f;

        previewCamera.nearClipPlane =
            0.05f;

        previewCamera.farClipPlane =
            50f;

        previewCamera.targetTexture =
            previewTexture;

        cameraObject.transform.localPosition =
            new Vector3(
                0f,
                1.15f,
                -4.25f);

        LookAtLocalPoint(
            cameraObject.transform,
            previewStage.transform,
            new Vector3(
                0f,
                1.05f,
                0f));

        GameObject keyLight =
            new GameObject(
                "KeyLight");

        keyLight.transform.SetParent(
            previewStage.transform,
            false);

        Light key =
            keyLight.AddComponent<
                Light>();

        key.type =
            LightType.Directional;

        key.intensity =
            1.2f;

        keyLight.transform.localRotation =
            Quaternion.Euler(
                35f,
                -30f,
                0f);

        GameObject fillLight =
            new GameObject(
                "FillLight");

        fillLight.transform.SetParent(
            previewStage.transform,
            false);

        Light fill =
            fillLight.AddComponent<
                Light>();

        fill.type =
            LightType.Directional;

        fill.intensity =
            0.55f;

        fillLight.transform.localRotation =
            Quaternion.Euler(
                20f,
                150f,
                0f);

        if (previewImage != null)
        {
            previewImage.texture =
                previewTexture;
        }
    }

    private void ShowPreviewModel(
        PawnCosmeticDefinition cosmetic)
    {
        EnsurePreviewStage();

        if (previewModel != null)
        {
            Destroy(
                previewModel);

            previewModel =
                null;
        }

        if (cosmetic == null ||
            cosmetic.Prefab == null ||
            previewModelRoot == null)
        {
            return;
        }

        previewModel =
            Instantiate(
                cosmetic.Prefab,
                previewModelRoot);

        previewModel.name =
            "Preview_" +
            cosmetic.CosmeticId;

        previewModel.transform
            .localPosition =
                Vector3.zero;

        previewModel.transform
            .localRotation =
                Quaternion.Euler(
                    cosmetic.RotationOffset);

        previewModel.transform
            .localScale =
                Vector3.one;

        DisablePhysics(
            previewModel);
        if (cosmetic.OverrideMaterial != null &&
            cosmetic.CosmeticId.StartsWith("vehicle_", StringComparison.Ordinal))
        {
            foreach (Renderer renderer in previewModel.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null) continue;
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = cosmetic.OverrideMaterial;
                renderer.sharedMaterials = slots;
            }
        }

        if (!TryGetBounds(
                previewModel,
                out Bounds bounds))
        {
            return;
        }

        float scale = Mathf.Min(
            2.15f / Mathf.Max(0.001f, bounds.size.y),
            2.65f / Mathf.Max(0.001f, bounds.size.x, bounds.size.z));

        previewModel.transform
            .localScale *=
                scale;

        if (!TryGetBounds(
                previewModel,
                out bounds))
        {
            return;
        }

        Vector3 desiredCenter =
            previewStage.transform
                .TransformPoint(
                    new Vector3(
                        0f,
                        1.08f,
                        0f));

        previewModel.transform.position +=
            desiredCenter -
            bounds.center;
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

    private static void LookAtLocalPoint(
        Transform cameraTransform,
        Transform localSpace,
        Vector3 localPoint)
    {
        Vector3 worldPoint =
            localSpace.TransformPoint(
                localPoint);

        cameraTransform.rotation =
            Quaternion.LookRotation(
                worldPoint -
                cameraTransform.position,
                Vector3.up);
    }

    private void DestroyPreviewStage()
    {
        if (previewTexture != null)
        {
            previewTexture.Release();

            Destroy(
                previewTexture);

            previewTexture =
                null;
        }

        if (previewStage != null)
        {
            Destroy(
                previewStage);

            previewStage =
                null;
        }
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        PawnCosmeticCatalog newCatalog,
        GameObject newModalRoot,
        TMP_Text newPlayerText,
        TMP_Text newSelectionText,
        RawImage newPreviewImage,
        Button newPreviousButton,
        Button newNextButton,
        Button newApplyButton,
        Button newCancelButton)
    {
        catalog =
            newCatalog;

        modalRoot =
            newModalRoot;

        playerText =
            newPlayerText;

        selectionText =
            newSelectionText;

        previewImage =
            newPreviewImage;

        previousButton =
            newPreviousButton;

        nextButton =
            newNextButton;

        applyButton =
            newApplyButton;

        cancelButton =
            newCancelButton;
    }
#endif
}
