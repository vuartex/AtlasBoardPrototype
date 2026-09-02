using UnityEngine;

public class BankruptcyManager : MonoBehaviour
{
    public readonly struct PaymentResolution
    {
        public PaymentResolution(
            int amountDue,
            int amountPaid,
            int unpaidAmount,
            bool paidInFull,
            bool debtorBankrupt,
            int transferredPropertyCount)
        {
            AmountDue = amountDue;
            AmountPaid = amountPaid;
            UnpaidAmount = unpaidAmount;
            PaidInFull = paidInFull;
            DebtorBankrupt = debtorBankrupt;
            TransferredPropertyCount =
                transferredPropertyCount;
        }

        public int AmountDue { get; }
        public int AmountPaid { get; }
        public int UnpaidAmount { get; }
        public bool PaidInFull { get; }
        public bool DebtorBankrupt { get; }
        public int TransferredPropertyCount { get; }
    }

    [Header("References")]
    [SerializeField]
    private BoardPath boardPath;

    [SerializeField]
    private TurnManager turnManager;

    [SerializeField]
    private PropertyDevelopmentManager propertyDevelopmentManager;

    public PaymentResolution ResolveMandatoryPayment(
        PlayerGameState debtor,
        PlayerGameState creditor,
        int amountDue,
        string reason)
    {
        if (debtor == null || amountDue <= 0)
        {
            return new PaymentResolution(
                amountDue,
                0,
                Mathf.Max(0, amountDue),
                amountDue <= 0,
                debtor != null && debtor.IsBankrupt,
                0);
        }

        if (debtor.IsBankrupt)
        {
            return new PaymentResolution(
                amountDue,
                0,
                amountDue,
                false,
                true,
                0);
        }

        if (creditor == debtor)
        {
            return new PaymentResolution(
                amountDue,
                amountDue,
                0,
                true,
                false,
                0);
        }

        if (debtor.CurrentMoney >= amountDue)
        {
            debtor.TrySpend(amountDue);

            if (creditor != null &&
                !creditor.IsBankrupt)
            {
                creditor.AddMoney(amountDue);
            }

            Debug.Log(
                $"{debtor.DisplayName} paid {amountDue} for " +
                $"{reason}.",
                this);

            return new PaymentResolution(
                amountDue,
                amountDue,
                0,
                true,
                false,
                0);
        }

        int amountPaid =
            debtor.TakeAllMoney();

        if (creditor != null &&
            !creditor.IsBankrupt &&
            amountPaid > 0)
        {
            creditor.AddMoney(amountPaid);
        }

        int transferredPropertyCount =
            TransferOwnedProperties(
                debtor,
                creditor);

        debtor.DeclareBankrupt();

        EnsureTurnManager();

        if (turnManager != null)
        {
            turnManager.NotifyPlayerBankrupt(
                debtor);
        }

        int unpaidAmount =
            Mathf.Max(
                0,
                amountDue - amountPaid);

        string creditorDescription =
            creditor != null
                ? creditor.DisplayName
                : "bankaya";

        Debug.Log(
            $"{debtor.DisplayName} could not fully pay " +
            $"{amountDue} for {reason}. " +
            $"Paid: {amountPaid}, unpaid: {unpaidAmount}, " +
            $"properties transferred/released: " +
            $"{transferredPropertyCount}, destination: " +
            $"{creditorDescription}.",
            this);

        return new PaymentResolution(
            amountDue,
            amountPaid,
            unpaidAmount,
            false,
            true,
            transferredPropertyCount);
    }

    private int TransferOwnedProperties(
        PlayerGameState debtor,
        PlayerGameState creditor)
    {
        EnsureBoardPath();

        if (boardPath == null || debtor == null)
        {
            return 0;
        }

        bool transferToCreditor =
            creditor != null &&
            !creditor.IsBankrupt;

        int transferredCount = 0;

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile == null ||
                !tile.IsOwned ||
                tile.OwnerPlayerIndex !=
                debtor.PlayerSlotIndex)
            {
                continue;
            }

            // Development is liquidated when a player goes bankrupt. Even if
            // the property itself transfers to a creditor, old house/hotel
            // visuals and rent-development state must not survive the bankrupt
            // owner.
            EnsureDevelopmentManager();

            if (propertyDevelopmentManager != null)
            {
                propertyDevelopmentManager
                    .ResetDevelopment(tile);
            }

            tile.ClearOwner();
            transferredCount++;

            if (!transferToCreditor)
            {
                continue;
            }

            bool assigned =
                tile.TrySetOwner(
                    creditor.PlayerSlotIndex);

            if (!assigned)
            {
                Debug.LogWarning(
                    $"Could not transfer {tile.DisplayName} " +
                    $"to {creditor.DisplayName}.",
                    tile);

                EnsureDevelopmentManager();

                if (propertyDevelopmentManager != null)
                {
                    propertyDevelopmentManager
                        .ResetDevelopment(tile);
                }

                continue;
            }

            if (creditor.OwnershipMaterial != null)
            {
                tile.ApplyOwnerMaterial(
                    creditor.OwnershipMaterial);
            }

            EnsureDevelopmentManager();

            if (propertyDevelopmentManager != null)
            {
                propertyDevelopmentManager
                    .RefreshDevelopmentVisual(
                        tile,
                        creditor);
            }
        }

        return transferredCount;
    }

    private void EnsureBoardPath()
    {
        if (boardPath == null)
        {
            boardPath =
                FindAnyObjectByType<BoardPath>();
        }
    }

    private void EnsureDevelopmentManager()
    {
        if (propertyDevelopmentManager == null)
        {
            propertyDevelopmentManager =
                FindAnyObjectByType<
                    PropertyDevelopmentManager>();
        }
    }

    private void EnsureTurnManager()
    {
        if (turnManager == null)
        {
            turnManager =
                FindAnyObjectByType<TurnManager>();
        }
    }
}
