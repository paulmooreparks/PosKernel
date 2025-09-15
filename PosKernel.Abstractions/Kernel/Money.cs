namespace PosKernel.Abstractions;

/// <summary>
/// Immutable monetary value represented in minor currency units (e.g. cents) plus ISO 4217 currency code.
/// All arithmetic enforces currency consistency to prevent cross-currency mistakes.
/// </summary>
/// <param name="MinorUnits">The amount in minor units (e.g. cents) of the currency.</param>
/// <param name="Currency">The ISO 4217 currency code (e.g. USD, EUR).</param>
public readonly record struct Money(long MinorUnits, string Currency)
{
    /// <summary>
    /// Creates a zero-valued money instance for the specified currency.
    /// </summary>
    /// <param name="currency">The currency code.</param>
    public static Money Zero(string currency) => new(0, currency);

    /// <summary>
    /// Converts the minor unit representation to a decimal value using the provided scale (default 2).
    /// </summary>
    /// <param name="scale">Number of decimal places (typically 2, sometimes 0 or 3 for some currencies).</param>
    /// <returns>The decimal representation.</returns>
    public decimal ToDecimal(int scale = 2) => MinorUnits / (decimal)Math.Pow(10, scale);

    /// <summary>
    /// Returns a culture-neutral string representation of the monetary amount.
    /// </summary>
    public override string ToString() => $"{Currency} {ToDecimal()}";

    /// <summary>
    /// Adds another money value of the same currency.
    /// </summary>
    /// <param name="other">The other amount.</param>
    /// <returns>The summed amount.</returns>
    /// <exception cref="InvalidOperationException">Thrown if currencies differ.</exception>
    public Money Add(Money other)
        => Currency != other.Currency ? throw new InvalidOperationException("Currency mismatch") : new(MinorUnits + other.MinorUnits, Currency);

    /// <summary>
    /// Subtracts another money value of the same currency.
    /// </summary>
    /// <param name="other">The other amount.</param>
    /// <returns>The result amount.</returns>
    /// <exception cref="InvalidOperationException">Thrown if currencies differ.</exception>
    public Money Subtract(Money other)
        => Currency != other.Currency ? throw new InvalidOperationException("Currency mismatch") : new(MinorUnits - other.MinorUnits, Currency);
}
