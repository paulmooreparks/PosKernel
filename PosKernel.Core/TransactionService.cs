using PosKernel.Abstractions;

namespace PosKernel.Core;

public interface ITransactionService
{
    Transaction Begin(string currency);
    Transaction AddLine(Transaction tx, ProductId productId, int qty, Money unitPrice, Money extendedPrice);
    Transaction UpdateFromKernel(Transaction tx, Money total, Money tendered, Money changeDue, TransactionState state);
}

public sealed class TransactionService : ITransactionService
{
    public Transaction Begin(string currency) => new(currency);

    public Transaction AddLine(Transaction tx, ProductId productId, int qty, Money unitPrice, Money extendedPrice)
    {
        // ARCHITECTURAL PRINCIPLE: No calculations - values come from POS kernel
        var line = new TransactionLine(tx.Currency)
        {
            ProductId = productId,
            Quantity = qty,
            UnitPrice = unitPrice,
            Extended = extendedPrice // Value from kernel, not calculated
        };
        tx.Lines.Add(line);
        return tx;
    }

    public Transaction UpdateFromKernel(Transaction tx, Money total, Money tendered, Money changeDue, TransactionState state)
    {
        // ARCHITECTURAL PRINCIPLE: Update display values from authoritative kernel calculations
        tx.UpdateFromKernel(total, tendered, changeDue, state);
        return tx;
    }
}
