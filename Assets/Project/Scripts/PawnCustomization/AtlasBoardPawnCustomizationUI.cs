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

    private RenderTexture previewTexture;
    private GameObject previewStage;
    private Transform previewModelRoot;
    private GameObject previewModel;
    private Camera previewCamera;

    private void Awake()
    {
        HookButtons();

        if (modalRoot != null)
        {
            modalRoot.SetActive(
                false);
        }
    }

    private void OnDestroy()
    {
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

        currentSlotIndex =
            Mathf.Clamp(
                playerSlotIndex,
                0,
                3);

        originalCosmeticId =
            AtlasBoardPawnSelectionStore
                .GetSelectedId(
                    currentSlotIndex,
                    catalog);

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

    private void ApplySelection()
    {
        if (catalog == null ||
            catalog.Count == 0)
        {
            return;
        }

        PawnCosmeticDefinition cosmetic =
            catalog.GetByIndex(
                currentCosmeticIndex);

        if (cosmetic == null)
        {
            return;
        }

        AtlasBoardPawnCosmeticService service =
            AtlasBoardPawnCosmeticService
                .Instance;

        if (service != null)
        {
            service.SelectCosmetic(
                currentSlotIndex,
                cosmetic.CosmeticId);
        }
        else
        {
            AtlasBoardPawnSelectionStore
                .SetSelectedId(
                    currentSlotIndex,
                    cosmetic.CosmeticId);
        }

        originalCosmeticId =
            cosmetic.CosmeticId;

        if (modalRoot != null)
        {
            modalRoot.SetActive(
                false);
        }
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

        if (!TryGetBounds(
                previewModel,
                out Bounds bounds))
        {
            return;
        }

        float scale =
            2.15f /
            Mathf.Max(
                0.001f,
                bounds.size.y);

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
