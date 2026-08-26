using UnityEngine;

[DefaultExecutionOrder(-2500)]
[DisallowMultipleComponent]
public class AtlasBoardPawnCosmeticService :
    MonoBehaviour
{
    public static AtlasBoardPawnCosmeticService
        Instance
    {
        get;
        private set;
    }

    [SerializeField]
    private PawnCosmeticCatalog catalog;

    public PawnCosmeticCatalog Catalog =>
        catalog;

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(
                gameObject);

            return;
        }

        Instance = this;
    }

    public PawnCosmeticDefinition
        GetSelectedCosmetic(
            int playerSlotIndex)
    {
        if (catalog == null)
        {
            return null;
        }

        string selectedId =
            AtlasBoardPawnSelectionStore
                .GetSelectedId(
                    playerSlotIndex,
                    catalog);

        PawnCosmeticDefinition selected =
            catalog.FindById(
                selectedId);

        return selected != null
            ? selected
            : catalog
                .GetDefaultForSlot(
                    playerSlotIndex);
    }

    public void SelectCosmetic(
        int playerSlotIndex,
        string cosmeticId)
    {
        if (catalog == null ||
            catalog.FindById(
                cosmeticId) == null)
        {
            return;
        }

        AtlasBoardPawnSelectionStore
            .SetSelectedId(
                playerSlotIndex,
                cosmeticId);
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        PawnCosmeticCatalog newCatalog)
    {
        catalog =
            newCatalog;
    }
#endif
}
