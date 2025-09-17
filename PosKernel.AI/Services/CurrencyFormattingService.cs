namespace PosKernel.AI.Services
{
    /// <summary>
    /// Service for formatting monetary amounts according to currency-specific rules.
    /// Handles different decimal precision requirements across various currencies.
    /// </summary>
    public static class CurrencyFormattingService
    {
        // Well-known currency decimal places
        private static readonly Dictionary<string, int> CurrencyDecimalPlaces = new(StringComparer.OrdinalIgnoreCase)
        {
            // Zero decimal places
            ["JPY"] = 0, // Japanese Yen
            ["KRW"] = 0, // South Korean Won
            ["VND"] = 0, // Vietnamese Dong
            ["CLP"] = 0, // Chilean Peso
            ["ISK"] = 0, // Icelandic Krona
            ["PYG"] = 0, // Paraguayan Guarani
            ["UGX"] = 0, // Ugandan Shilling
            ["RWF"] = 0, // Rwandan Franc

            // Three decimal places
            ["KWD"] = 3, // Kuwaiti Dinar
            ["BHD"] = 3, // Bahraini Dinar
            ["OMR"] = 3, // Omani Rial
            ["JOD"] = 3, // Jordanian Dinar
            ["TND"] = 3, // Tunisian Dinar

            // Four decimal places (very rare)
            ["CLF"] = 4, // Chilean Unit of Account

            // Standard currencies (2 decimal places) - most common
            ["USD"] = 2, ["EUR"] = 2, ["GBP"] = 2, ["CAD"] = 2, ["AUD"] = 2,
            ["SGD"] = 2, ["NZD"] = 2, ["CHF"] = 2, ["SEK"] = 2, ["NOK"] = 2,
            ["DKK"] = 2, ["PLN"] = 2, ["CZK"] = 2, ["HUF"] = 2, ["BGN"] = 2,
            ["RON"] = 2, ["HRK"] = 2, ["RUB"] = 2, ["CNY"] = 2, ["INR"] = 2,
            ["THB"] = 2, ["MYR"] = 2, ["PHP"] = 2, ["IDR"] = 2, ["BRL"] = 2,
            ["MXN"] = 2, ["ARS"] = 2, ["COP"] = 2, ["PEN"] = 2, ["ZAR"] = 2,
            ["EGP"] = 2, ["NGN"] = 2, ["KES"] = 2, ["GHS"] = 2, ["MAD"] = 2,
            ["TRY"] = 2, ["ILS"] = 2, ["SAR"] = 2, ["AED"] = 2, ["QAR"] = 2,

            // Cryptocurrencies (typically 8 decimal places, but we'll use 2 for display)
            ["BTC"] = 2, ["ETH"] = 2, ["LTC"] = 2, ["XRP"] = 2, ["ADA"] = 2,
            ["DOT"] = 2, ["SOL"] = 2, ["AVAX"] = 2, ["MATIC"] = 2, ["LINK"] = 2,

            // Custom/loyalty currencies (typically 2 decimal places)
            ["PTS"] = 2, // Points
            ["GLD"] = 2, // Gold units
            ["SLV"] = 2, // Silver units
        };

        /// <summary>
        /// Formats a decimal amount according to the specified currency's formatting rules.
        /// </summary>
        /// <param name="amount">The monetary amount.</param>
        /// <param name="currencyCode">The 3-letter currency code (e.g., "USD", "JPY", "EUR").</param>
        /// <returns>Formatted amount string with appropriate decimal precision.</returns>
        public static string FormatAmount(decimal amount, string currencyCode)
        {
            var decimalPlaces = GetDecimalPlaces(currencyCode);
            return decimalPlaces switch
            {
                0 => amount.ToString("F0"),
                1 => amount.ToString("F1"),
                2 => amount.ToString("F2"),
                3 => amount.ToString("F3"),
                4 => amount.ToString("F4"),
                _ => amount.ToString($"F{decimalPlaces}")
            };
        }

        /// <summary>
        /// Gets the number of decimal places for a given currency.
        /// </summary>
        /// <param name="currencyCode">The 3-letter currency code.</param>
        /// <returns>Number of decimal places (0-4 typically, defaults to 2 for unknown currencies).</returns>
        public static int GetDecimalPlaces(string currencyCode)
        {
            return CurrencyDecimalPlaces.TryGetValue(currencyCode?.ToUpperInvariant() ?? "", out var places) ? places : 2;
        }

        /// <summary>
        /// Creates a formatted string with currency symbol for display purposes.
        /// </summary>
        /// <param name="amount">The monetary amount.</param>
        /// <param name="currencyCode">The 3-letter currency code.</param>
        /// <returns>Formatted string with currency symbol where applicable.</returns>
        public static string FormatWithSymbol(decimal amount, string currencyCode)
        {
            var formattedAmount = FormatAmount(amount, currencyCode);
            
            return currencyCode?.ToUpperInvariant() switch
            {
                "USD" => $"${formattedAmount}",
                "EUR" => $"€{formattedAmount}",
                "GBP" => $"£{formattedAmount}",
                "JPY" => $"¥{formattedAmount}",
                "KRW" => $"₩{formattedAmount}",
                "SGD" => $"S${formattedAmount}",
                "AUD" => $"A${formattedAmount}",
                "CAD" => $"C${formattedAmount}",
                "CNY" => $"¥{formattedAmount}",
                "INR" => $"₹{formattedAmount}",
                "CHF" => $"₣{formattedAmount}",
                "RUB" => $"₽{formattedAmount}",
                "BRL" => $"R${formattedAmount}",
                "MXN" => $"$MX{formattedAmount}",
                "ZAR" => $"R{formattedAmount}",
                "TRY" => $"₺{formattedAmount}",
                "ILS" => $"₪{formattedAmount}",
                "SAR" => $"﷼{formattedAmount}",
                "AED" => $"د.إ{formattedAmount}",
                "BTC" => $"₿{formattedAmount}",
                "ETH" => $"Ξ{formattedAmount}",
                _ => $"{formattedAmount} {currencyCode}" // Generic format for unknown currencies
            };
        }
    }
}
