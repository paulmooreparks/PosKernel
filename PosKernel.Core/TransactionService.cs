using PosKernel.Abstractions;

namespace PosKernel.Core;

public interface ITransactionService
{
    Transaction Begin(string currency);
    Transaction AddLine(Transaction tx, ProductId productId, int qty, Money unitPrice);
    Transaction AddCashTender(Transaction tx, Money amount);
}

public sealed class TransactionService : ITransactionService
{
    public Transaction Begin(string currency) => new() { }; // currency set via default

    public Transaction AddLine(Transaction tx, ProductId productId, int qty, Money unitPrice)
    {
        tx.AddLine(productId, qty, unitPrice);
        return tx;
    }

    public Transaction AddCashTender(Transaction tx, Money amount)
    {
        tx.AddCashTender(amount);
        return tx;
    }
}
