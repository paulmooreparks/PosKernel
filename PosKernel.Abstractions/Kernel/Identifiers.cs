namespace PosKernel.Abstractions;

public readonly record struct TransactionId(Guid Value)
{
    public static TransactionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public readonly record struct ProductId(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct LineItemId(Guid Value)
{
    public static LineItemId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}
