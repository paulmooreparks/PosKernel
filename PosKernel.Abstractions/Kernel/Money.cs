namespace PosKernel.Abstractions;

public readonly record struct Money(long MinorUnits, string Currency)
{
    public static Money Zero(string currency) => new(0, currency);
    public decimal ToDecimal(int scale = 2) => MinorUnits / (decimal)Math.Pow(10, scale);
    public override string ToString() => $"{Currency} {ToDecimal()}";
    public Money Add(Money other)
        => Currency != other.Currency ? throw new InvalidOperationException("Currency mismatch") : new(MinorUnits + other.MinorUnits, Currency);
    public Money Subtract(Money other)
        => Currency != other.Currency ? throw new InvalidOperationException("Currency mismatch") : new(MinorUnits - other.MinorUnits, Currency);
}
