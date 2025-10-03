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

namespace PosKernel.HttpService.Services;

/// <summary>
/// Currency formatting utilities for culture-neutral kernel operations
/// </summary>
public static class CurrencyHelper
{
    /// <summary>
    /// Gets the number of decimal places for a given currency code
    /// ARCHITECTURAL PRINCIPLE: Currency specifications, not cultural assumptions
    /// </summary>
    public static int GetCurrencyDecimalPlaces(string currency)
    {
        return currency.ToUpperInvariant() switch
        {
            // Zero decimal place currencies
            "JPY" => 0,  // Japanese Yen
            "KRW" => 0,  // Korean Won
            "PYG" => 0,  // Paraguayan Guarani
            "RWF" => 0,  // Rwandan Franc
            "UGX" => 0,  // Ugandan Shilling
            "VND" => 0,  // Vietnamese Dong
            "VUV" => 0,  // Vanuatu Vatu
            "XAF" => 0,  // Central African CFA Franc
            "XOF" => 0,  // West African CFA Franc
            "XPF" => 0,  // CFP Franc

            // Three decimal place currencies
            "BHD" => 3,  // Bahraini Dinar
            "IQD" => 3,  // Iraqi Dinar
            "JOD" => 3,  // Jordanian Dinar
            "KWD" => 3,  // Kuwaiti Dinar
            "LYD" => 3,  // Libyan Dinar
            "OMR" => 3,  // Omani Rial
            "TND" => 3,  // Tunisian Dinar

            // Four decimal place currencies (rare)
            "CLF" => 4,  // Chilean Unit of Account (UF)

            // Default: Two decimal places (USD, EUR, SGD, GBP, etc.)
            _ => 2
        };
    }

    /// <summary>
    /// Rounds a decimal value to the appropriate number of decimal places for the currency
    /// </summary>
    public static decimal RoundToCurrency(decimal value, string currency)
    {
        var decimalPlaces = GetCurrencyDecimalPlaces(currency);
        return Math.Round(value, decimalPlaces, MidpointRounding.AwayFromZero);
    }
}
