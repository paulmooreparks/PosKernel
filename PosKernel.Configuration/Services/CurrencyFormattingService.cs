//
// Copyright 2025 Paul Moore Parks and contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using PosKernel.Abstractions;

namespace PosKernel.Configuration.Services
{
    /// <summary>
    /// ARCHITECTURAL PROPER LOCATION: Configuration Service Layer
    ///
    /// Service for formatting monetary amounts according to store-specific currency rules.
    /// This belongs in the Configuration layer because:
    /// 1. Currency formatting rules are configuration, not business logic
    /// 2. Store context determines currency and locale
    /// 3. Rules are driven by store/regional configuration
    /// 4. Domain extensions and clients should receive pre-formatted values
    /// </summary>
    public interface ICurrencyFormattingService
    {
        /// <summary>
        /// Formats a monetary amount according to store configuration.
        /// </summary>
        /// <param name="amount">The monetary amount.</param>
        /// <param name="storeId">Store identifier for locale-specific formatting.</param>
        /// <returns>Formatted currency string (e.g., "$12.34", "¥1,234", "€12,34").</returns>
        string FormatCurrency(Money amount, string storeId);

        /// <summary>
        /// Formats a decimal amount with currency according to store configuration.
        /// </summary>
        /// <param name="amount">The decimal amount.</param>
        /// <param name="currency">The currency code.</param>
        /// <param name="storeId">Store identifier for locale-specific formatting.</param>
        /// <returns>Formatted currency string.</returns>
        string FormatCurrency(decimal amount, string currency, string storeId);
    }

    /// <summary>
    /// Implementation of currency formatting service that uses store configuration
    /// to determine appropriate currency formatting rules.
    /// </summary>
    public class CurrencyFormattingService : ICurrencyFormattingService
    {
        private readonly IStoreConfigurationService _storeConfigService;

        // Currency formatting rules - these could be loaded from configuration
        private static readonly Dictionary<string, CurrencyFormatRules> CurrencyRules = new(StringComparer.OrdinalIgnoreCase)
        {
            // Zero decimal places
            ["JPY"] = new() { Symbol = "¥", DecimalPlaces = 0, SymbolPosition = SymbolPosition.Before },
            ["KRW"] = new() { Symbol = "₩", DecimalPlaces = 0, SymbolPosition = SymbolPosition.Before },
            ["VND"] = new() { Symbol = "₫", DecimalPlaces = 0, SymbolPosition = SymbolPosition.After },

            // Three decimal places
            ["KWD"] = new() { Symbol = "د.ك", DecimalPlaces = 3, SymbolPosition = SymbolPosition.Before },
            ["BHD"] = new() { Symbol = ".د.ب", DecimalPlaces = 3, SymbolPosition = SymbolPosition.Before },
            ["OMR"] = new() { Symbol = "ر.ع.", DecimalPlaces = 3, SymbolPosition = SymbolPosition.Before },

            // Standard currencies (2 decimal places)
            ["USD"] = new() { Symbol = "$", DecimalPlaces = 2, SymbolPosition = SymbolPosition.Before },
            ["EUR"] = new() { Symbol = "€", DecimalPlaces = 2, SymbolPosition = SymbolPosition.Before },
            ["GBP"] = new() { Symbol = "£", DecimalPlaces = 2, SymbolPosition = SymbolPosition.Before },
            ["SGD"] = new() { Symbol = "S$", DecimalPlaces = 2, SymbolPosition = SymbolPosition.Before },
            ["AUD"] = new() { Symbol = "A$", DecimalPlaces = 2, SymbolPosition = SymbolPosition.Before },
            ["CAD"] = new() { Symbol = "C$", DecimalPlaces = 2, SymbolPosition = SymbolPosition.Before },
            ["CNY"] = new() { Symbol = "¥", DecimalPlaces = 2, SymbolPosition = SymbolPosition.Before },
            ["INR"] = new() { Symbol = "₹", DecimalPlaces = 2, SymbolPosition = SymbolPosition.Before }
        };

        /// <summary>
        /// Initializes a new instance of the CurrencyFormattingService.
        /// </summary>
        /// <param name="storeConfigService">Store configuration service for getting locale information.</param>
        public CurrencyFormattingService(IStoreConfigurationService storeConfigService)
        {
            _storeConfigService = storeConfigService ?? throw new ArgumentNullException(nameof(storeConfigService));
        }

        /// <inheritdoc />
        public string FormatCurrency(Money amount, string storeId)
        {
            return FormatCurrency(amount.Amount, amount.Currency, storeId);
        }

        /// <inheritdoc />
        public string FormatCurrency(decimal amount, string currency, string storeId)
        {
            // Get store configuration to determine locale preferences
            var storeConfig = _storeConfigService.GetStoreConfiguration(storeId);

            // Get currency formatting rules
            var rules = GetCurrencyRules(currency, storeConfig);

            // Format amount according to rules
            var formattedAmount = amount.ToString($"F{rules.DecimalPlaces}");

            // Apply symbol positioning
            return rules.SymbolPosition switch
            {
                SymbolPosition.Before => $"{rules.Symbol}{formattedAmount}",
                SymbolPosition.After => $"{formattedAmount} {currency}",
                _ => $"{rules.Symbol}{formattedAmount}"
            };
        }

        private CurrencyFormatRules GetCurrencyRules(string currency, StoreConfiguration storeConfig)
        {
            // Try to get rules for specific currency
            if (CurrencyRules.TryGetValue(currency, out var rules))
            {
                return rules;
            }

            // ARCHITECTURAL FIX: Fail fast for unknown currencies
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Currency '{currency}' not supported by CurrencyFormattingService. " +
                $"Add currency formatting rules for '{currency}' or configure a different currency. " +
                $"Do not assume '$' or '2 decimal places' defaults.");
        }
    }

    /// <summary>
    /// Currency formatting rules for a specific currency.
    /// </summary>
    public class CurrencyFormatRules
    {
        /// <summary>
        /// Gets or sets the currency symbol.
        /// </summary>
        public string Symbol { get; set; } = "$";

        /// <summary>
        /// Gets or sets the number of decimal places.
        /// </summary>
        public int DecimalPlaces { get; set; } = 2;

        /// <summary>
        /// Gets or sets the symbol position relative to the amount.
        /// </summary>
        public SymbolPosition SymbolPosition { get; set; } = SymbolPosition.Before;
    }

    /// <summary>
    /// Position of currency symbol relative to amount.
    /// </summary>
    public enum SymbolPosition
    {
        /// <summary>
        /// Symbol appears before amount (e.g., "$12.34").
        /// </summary>
        Before,

        /// <summary>
        /// Symbol appears after amount (e.g., "12.34 USD").
        /// </summary>
        After
    }

    // Placeholder interfaces that would be properly implemented

    /// <summary>
    /// Store configuration service interface.
    /// </summary>
    public interface IStoreConfigurationService
    {
        /// <summary>
        /// Gets store configuration for the specified store.
        /// </summary>
        StoreConfiguration GetStoreConfiguration(string storeId);
    }

    /// <summary>
    /// Store configuration data.
    /// </summary>
    public class StoreConfiguration
    {
        /// <summary>
        /// Gets or sets the store's primary currency.
        /// </summary>
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// Gets or sets the store's locale (e.g., "en-US", "en-SG").
        /// </summary>
        public string Locale { get; set; } = "en-US";

        /// <summary>
        /// Gets or sets the store identifier.
        /// </summary>
        public string StoreId { get; set; } = "";
    }
}
