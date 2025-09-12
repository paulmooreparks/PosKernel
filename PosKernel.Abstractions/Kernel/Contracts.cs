namespace PosKernel.Abstractions;

public enum TransactionState
{
    Building,
    Completed,
    Voided
}

public sealed class TransactionLine
{
    public LineItemId LineId { get; init; } = LineItemId.New();
    public ProductId ProductId { get; init; } = new("");
    public int Quantity { get; set; }
    public Money UnitPrice { get; set; }
    public Money Extended => new(UnitPrice.MinorUnits * Quantity, UnitPrice.Currency);
}

public sealed class Transaction
{
    public TransactionId Id { get; init; } = TransactionId.New();
    public TransactionState State { get; set; } = TransactionState.Building;
    public string Currency { get; init; } = "USD";
    public List<TransactionLine> Lines { get; } = new();
    public Money Total => Lines.Aggregate(Money.Zero(Currency), (acc, l) => acc.Add(l.Extended));
    public Money Tendered { get; private set; } = Money.Zero("USD");
    public Money ChangeDue => Tendered.MinorUnits > Total.MinorUnits ? new(Tendered.MinorUnits - Total.MinorUnits, Currency) : Money.Zero(Currency);

    public void AddLine(ProductId productId, int quantity, Money unitPrice)
    {
        if (State != TransactionState.Building) throw new InvalidOperationException("Cannot add line when not building");
        Lines.Add(new TransactionLine { ProductId = productId, Quantity = quantity, UnitPrice = unitPrice });
    }

    public void AddCashTender(Money amount)
    {
        if (State != TransactionState.Building) throw new InvalidOperationException("Cannot tender when not building");
        Tendered = Tendered.Add(amount);
        if (Tendered.MinorUnits >= Total.MinorUnits) State = TransactionState.Completed;
    }
}
