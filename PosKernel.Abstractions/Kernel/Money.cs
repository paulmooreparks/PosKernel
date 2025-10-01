namespace PosKernel.Abstractions;

/// <summary>
/// Immutable monetary value represented as a decimal amount plus ISO 4217 currency code.
/// All arithmetic enforces currency consistency to prevent cross-currency mistakes.
/// ARCHITECTURAL PRINCIPLE: Culture-neutral decimal representation - no assumptions about currency fractions.
/// Significant digits and rounding rules are managed by currency definitions and store extensions.
/// </summary>
/// <param name="Amount">The monetary amount as a decimal value.</param>
/// <param name="Currency">The ISO 4217 currency code (e.g. USD, EUR).</param>
public readonly record struct Money(decimal Amount, string Currency)
{
    /// <summary>
    /// Creates a zero-valued money instance for the specified currency.
    /// </summary>
    /// <param name="currency">The currency code.</param>
    public static Money Zero(string currency) => new(0m, currency);

    /// <summary>
    /// Returns a culture-neutral string representation of the monetary amount.
    /// </summary>
    public override string ToString() => $"{Currency} {Amount}";

    /// <summary>
    /// Adds another money value of the same currency.
    /// </summary>
    /// <param name="other">The other amount.</param>
    /// <returns>The summed amount.</returns>
    /// <exception cref="InvalidOperationException">Thrown if currencies differ.</exception>
    public Money Add(Money other)
        => Currency != other.Currency ? throw new InvalidOperationException("Currency mismatch") : new(Amount + other.Amount, Currency);

    /// <summary>
    /// Subtracts another money value of the same currency.
    /// </summary>
    /// <param name="other">The other amount.</param>
    /// <returns>The result amount.</returns>
    /// <exception cref="InvalidOperationException">Thrown if currencies differ.</exception>
    public Money Subtract(Money other)
        => Currency != other.Currency ? throw new InvalidOperationException("Currency mismatch") : new(Amount - other.Amount, Currency);

    /// <summary>
    /// Multiplies the amount by a scalar value.
    /// </summary>
    /// <param name="multiplier">The multiplier.</param>
    /// <returns>The multiplied amount.</returns>
    public Money Multiply(decimal multiplier) => new(Amount * multiplier, Currency);

    /// <summary>
    /// Divides the amount by a scalar value.
    /// </summary>
    /// <param name="divisor">The divisor.</param>
    /// <returns>The divided amount.</returns>
    /// <exception cref="DivideByZeroException">Thrown if divisor is zero.</exception>
    public Money Divide(decimal divisor)
        => divisor == 0 ? throw new DivideByZeroException("Cannot divide money by zero") : new(Amount / divisor, Currency);
}
