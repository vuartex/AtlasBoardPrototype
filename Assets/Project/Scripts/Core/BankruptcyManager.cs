using System.Collections.Generic;
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
            int transferredPropertyCount,
            int releasedPropertyCount = 0,
            int transferredPropertyValue = 0)
        {
            AmountDue = amountDue;
            AmountPaid = amountPaid;
            UnpaidAmount = unpaidAmount;
            PaidInFull = paidInFull;
            DebtorBankrupt = debtorBankrupt;
            TransferredPropertyCount =
                transferredPropertyCount;
            ReleasedPropertyCount =
                releasedPropertyCount;
            TransferredPropertyValue =
                transferredPropertyValue;
        }

        public int AmountDue { get; }
        public int AmountPaid { get; }
        public int UnpaidAmount { get; }
        public bool PaidInFull { get; }
        public bool DebtorBankrupt { get; }
        public int TransferredPropertyCount { get; }
        public int ReleasedPropertyCount { get; }
        public int TransferredPropertyValue { get; }
        public int LiquidatedPropertyCount =>
            TransferredPropertyCount +
            ReleasedPropertyCount;
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

        int unpaidAmount =
            Mathf.Max(
                0,
                amountDue - amountPaid);

        LiquidateOwnedProperties(
            debtor,
            creditor,
            unpaidAmount,
            out int transferredPropertyCount,
            out int releasedPropertyCount,
            out int transferredPropertyValue);

        debtor.DeclareBankrupt();

        EnsureTurnManager();

        if (turnManager != null)
        {
            turnManager.NotifyPlayerBankrupt(
                debtor);
        }

        string creditorDescription =
            creditor != null
                ? creditor.DisplayName
                : "bankaya";

        Debug.Log(
            $"{debtor.DisplayName} could not fully pay " +
            $"{amountDue} for {reason}. " +
            $"Paid: {amountPaid}, unpaid: {unpaidAmount}, " +
            $"properties transferred: {transferredPropertyCount} " +
            $"(value {transferredPropertyValue}), released: " +
            $"{releasedPropertyCount}, destination: " +
            $"{creditorDescription}.",
            this);

        return new PaymentResolution(
            amountDue,
            amountPaid,
            unpaidAmount,
            false,
            true,
            transferredPropertyCount,
            releasedPropertyCount,
            transferredPropertyValue);
    }

    private void LiquidateOwnedProperties(
        PlayerGameState debtor,
        PlayerGameState creditor,
        int unpaidAmount,
        out int transferredCount,
        out int releasedCount,
        out int transferredValue)
    {
        transferredCount = 0;
        releasedCount = 0;
        transferredValue = 0;

        EnsureBoardPath();

        if (boardPath == null ||
            debtor == null)
        {
            return;
        }

        bool transferToCreditor =
            creditor != null &&
            !creditor.IsBankrupt &&
            unpaidAmount > 0;

        List<BoardTile> ownedProperties =
            new List<BoardTile>();

        for (int tileIndex = 0;
             tileIndex < boardPath.TileCount;
             tileIndex++)
        {
            BoardTile tile =
                boardPath.GetTile(tileIndex);

            if (tile != null &&
                tile.IsOwned &&
                tile.OwnerPlayerIndex ==
                    debtor.PlayerSlotIndex)
            {
                ownedProperties.Add(tile);
            }
        }

        // Deterministic and intentionally debtor-friendly liquidation:
        // cheapest property first; tile index is the stable tie-breaker.
        ownedProperties.Sort(
            (left, right) =>
            {
                int valueCompare =
                    Mathf.Max(0, left.PurchasePrice)
                        .CompareTo(
                            Mathf.Max(
                                0,
                                right.PurchasePrice));

                return valueCompare != 0
                    ? valueCompare
                    : left.TileIndex
                        .CompareTo(
                            right.TileIndex);
            });

        int coveredValue = 0;

        foreach (BoardTile tile
                 in ownedProperties)
        {
            EnsureDevelopmentManager();

            propertyDevelopmentManager
                ?.ResetDevelopment(tile);

            tile.ClearOwner();

            bool shouldTransfer =
                transferToCreditor &&
                coveredValue < unpaidAmount;

            if (shouldTransfer)
            {
                bool assigned =
                    tile.TrySetOwner(
                        creditor.PlayerSlotIndex);

                if (assigned)
                {
                    int propertyValue =
                        Mathf.Max(
                            0,
                            tile.PurchasePrice);

                    transferredCount++;
                    transferredValue +=
                        propertyValue;
                    coveredValue +=
                        propertyValue;

                    if (creditor.OwnershipMaterial != null)
                    {
                        tile.ApplyOwnerMaterial(
                            creditor.OwnershipMaterial);
                    }

                    // Bankruptcy liquidates all development. Ownership may
                    // transfer, but houses/hotels never transfer with it.
                    propertyDevelopmentManager
                        ?.ResetDevelopment(tile);

                    continue;
                }

                Debug.LogWarning(
                    $"Could not transfer {tile.DisplayName} " +
                    $"to {creditor.DisplayName}; property was released.",
                    tile);
            }

            // No creditor, debt already covered by cheaper properties, or an
            // assignment failure: return the property to the unowned pool.
            releasedCount++;
        }
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
