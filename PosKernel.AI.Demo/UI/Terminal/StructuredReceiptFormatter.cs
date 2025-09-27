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

using System.Text;
using System.Text.RegularExpressions;
using PosKernel.AI.Models;
using PosKernel.AI.Services;
using PosKernel.Configuration.Services;

namespace PosKernel.AI.Demo.UI.Terminal;

/// <summary>
/// Structured receipt formatter with proper alignment, hierarchical modifications, and currency service integration.
/// ARCHITECTURAL PRINCIPLE: Service-based currency formatting, no hardcoded assumptions.
/// </summary>
public class StructuredReceiptFormatter
{
    private readonly ICurrencyFormattingService? _currencyFormatter;
    private readonly StoreConfig? _storeConfig;
    private readonly int _receiptWidth;
    private readonly int _priceColumnWidth;

    public StructuredReceiptFormatter(ICurrencyFormattingService? currencyFormatter = null, StoreConfig? storeConfig = null, int receiptWidth = 40)
    {
        _currencyFormatter = currencyFormatter;
        _storeConfig = storeConfig;
        _receiptWidth = receiptWidth;
        _priceColumnWidth = 8; // Width for price column (e.g., "S$12.34")
    }

    /// <summary>
    /// Formats a complete receipt with proper structure and alignment.
    /// ARCHITECTURAL PRINCIPLE: Uses currency service for all formatting, fails fast if unavailable.
    /// </summary>
    public string FormatReceipt(Receipt receipt)
    {
        var content = new StringBuilder();

        // Header section
        content.AppendLine(CenterText(receipt.Store.Name));
        content.AppendLine(CenterText($"Transaction #{receipt.TransactionId}"));
        content.AppendLine(new string('-', _receiptWidth));

        // Line items section
        if (receipt.Items.Any())
        {
            // ARCHITECTURAL PRINCIPLE: Render NRF-compliant hierarchical transaction structure
            // Read parent_line_item_id relationships, not cultural presentation strings
            RenderTransactionItems(receipt.Items, content, 0);

            // Totals section
            content.AppendLine();
            content.AppendLine(new string('-', _receiptWidth));
            content.AppendLine(FormatTotalLine("SUBTOTAL", receipt.Total));
            content.AppendLine(FormatTotalLine("TOTAL", receipt.Total));
        }
        else
        {
            content.AppendLine();
            content.AppendLine(CenterText("(Empty Order)"));
        }

        // Footer section
        content.AppendLine(new string('-', _receiptWidth));

        return content.ToString();
    }

    // ARCHITECTURAL PRINCIPLE: Render NRF-compliant hierarchical transaction structure
    // Read parent_line_item_id relationships, not cultural presentation strings
    
    private void RenderTransactionItems(List<PosKernel.AI.Models.ReceiptLineItem> items, StringBuilder content, int indentLevel)
    {
        // Find top-level items (no parent)
        var topLevelItems = items.Where(item => item.ParentLineItemId == null).ToList();
        
        foreach (var item in topLevelItems)
        {
            RenderLineItemWithChildren(item, items, content, indentLevel);
        }
    }
    
    private void RenderLineItemWithChildren(PosKernel.AI.Models.ReceiptLineItem item, List<PosKernel.AI.Models.ReceiptLineItem> allItems, StringBuilder content, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 2);
        var formattedPrice = FormatCurrency(item.ExtendedPrice);
        
        // Render current item
        var itemLine = $"{indent}{item.ProductName}";
        if (item.ExtendedPrice > 0 || indentLevel == 0) // Show price for top-level items or non-zero items
        {
            itemLine = itemLine.PadRight(35 - indent.Length) + formattedPrice;
        }
        content.AppendLine(itemLine);
        
        // Find and render child items
        var children = allItems.Where(child => child.ParentLineItemId == item.LineNumber).ToList();
        foreach (var child in children)
        {
            RenderLineItemWithChildren(child, allItems, content, indentLevel + 1);
        }
    }

    /// <summary>
    /// Formats a total line with proper alignment.
    /// Example: "TOTAL                         S$14.20"
    /// </summary>
    private string FormatTotalLine(string label, decimal amount)
    {
        var formattedAmount = FormatCurrency(amount);
        var labelColumnWidth = _receiptWidth - _priceColumnWidth;
        
        return $"{label.PadRight(labelColumnWidth)}{formattedAmount.PadLeft(_priceColumnWidth)}";
    }

    /// <summary>
    /// Centers text within the receipt width.
    /// </summary>
    private string CenterText(string text)
    {
        if (text.Length >= _receiptWidth)
        {
            return text;
        }

        var padding = (_receiptWidth - text.Length) / 2;
        return text.PadLeft(text.Length + padding).PadRight(_receiptWidth);
    }

    /// <summary>
    /// Parses preparation notes into structured modifications.
    /// Handles set customizations like "with Teh Si Kosong" -> drink choice + sugar modification.
    /// ARCHITECTURAL PRINCIPLE: Parse set customizations into hierarchical structure.
    /// </summary>
    private List<ReceiptModification> ParseModifications(string preparationNotes)
    {
        var modifications = new List<ReceiptModification>();

        if (string.IsNullOrEmpty(preparationNotes))
        {
            return modifications;
        }

        // Handle set customizations (e.g., "with Teh Si Kosong")
        if (preparationNotes.StartsWith("with ", StringComparison.OrdinalIgnoreCase))
        {
            var setCustomization = preparationNotes.Substring(5).Trim(); // Remove "with "
            return ParseSetCustomization(setCustomization);
        }

        // Handle regular modifications
        var parts = preparationNotes.Split(',')
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrEmpty(part));

        foreach (var part in parts)
        {
            modifications.Add(new ReceiptModification
            {
                Name = part,
                Cost = 0, // Most modifications are zero-cost
                SubModifications = new List<ReceiptModification>()
            });
        }

        return modifications;
    }

    /// <summary>
    /// Parses set customizations into hierarchical structure.
    /// Example: "Teh Si Kosong" -> "Teh C" with "No sugar" sub-modification
    /// ARCHITECTURAL PRINCIPLE: Parse kopitiam terminology into proper receipt format.
    /// </summary>
    private List<ReceiptModification> ParseSetCustomization(string customization)
    {
        var modifications = new List<ReceiptModification>();

        // Parse drink customizations with kopitiam terminology
        if (customization.Contains("kosong", StringComparison.OrdinalIgnoreCase))
        {
            // "Teh Si Kosong" -> "Teh C" with "No sugar"
            var drinkName = customization.Replace("kosong", "", StringComparison.OrdinalIgnoreCase).Trim();
            drinkName = NormalizeDrinkName(drinkName);
            
            var drink = new ReceiptModification
            {
                Name = drinkName,
                Cost = 0,
                SubModifications = new List<ReceiptModification>
                {
                    new ReceiptModification { Name = "No sugar", Cost = 0, SubModifications = new List<ReceiptModification>() }
                }
            };
            modifications.Add(drink);
        }
        else if (customization.Contains("siew dai", StringComparison.OrdinalIgnoreCase))
        {
            // "Teh C Siew Dai" -> "Teh C" with "Less sugar"
            var drinkName = customization.Replace("siew dai", "", StringComparison.OrdinalIgnoreCase).Trim();
            drinkName = NormalizeDrinkName(drinkName);
            
            var drink = new ReceiptModification
            {
                Name = drinkName,
                Cost = 0,
                SubModifications = new List<ReceiptModification>
                {
                    new ReceiptModification { Name = "Less sugar", Cost = 0, SubModifications = new List<ReceiptModification>() }
                }
            };
            modifications.Add(drink);
        }
        else if (customization.Contains("gah dai", StringComparison.OrdinalIgnoreCase))
        {
            // "Teh C Gah Dai" -> "Teh C" with "Extra sugar"
            var drinkName = customization.Replace("gah dai", "", StringComparison.OrdinalIgnoreCase).Trim();
            drinkName = NormalizeDrinkName(drinkName);
            
            var drink = new ReceiptModification
            {
                Name = drinkName,
                Cost = 0,
                SubModifications = new List<ReceiptModification>
                {
                    new ReceiptModification { Name = "Extra sugar", Cost = 0, SubModifications = new List<ReceiptModification>() }
                }
            };
            modifications.Add(drink);
        }
        else
        {
            // Simple drink customization without sugar modifications
            var drinkName = NormalizeDrinkName(customization);
            modifications.Add(new ReceiptModification
            {
                Name = drinkName,
                Cost = 0,
                SubModifications = new List<ReceiptModification>()
            });
        }

        return modifications;
    }

    /// <summary>
    /// Normalizes kopitiam drink names for receipt display.
    /// Converts "Teh Si" or "Teh si" to "Teh C" for proper receipt formatting.
    /// </summary>
    private string NormalizeDrinkName(string drinkName)
    {
        if (string.IsNullOrEmpty(drinkName))
        {
            return drinkName;
        }

        // Handle "Si" -> "C" conversion (evaporated milk)
        drinkName = Regex.Replace(drinkName, @"\bsi\b", "C", RegexOptions.IgnoreCase);
        
        // Standardize capitalization
        var words = drinkName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }

        return string.Join(" ", words);
    }

    /// <summary>
    /// Formats a currency amount using the store's currency service.
    /// ARCHITECTURAL PRINCIPLE: Fail fast if currency service unavailable - no hardcoded assumptions.
    /// </summary>
    private string FormatCurrency(decimal amount)
    {
        if (_currencyFormatter is not null && _storeConfig is not null)
        {
            return _currencyFormatter.FormatCurrency(amount, _storeConfig.Currency, _storeConfig.StoreName);
        }

        // ARCHITECTURAL PRINCIPLE: Fail fast - no fallback currency formatting assumptions
        throw new InvalidOperationException(
            $"DESIGN DEFICIENCY: Currency formatting service not available in StructuredReceiptFormatter. " +
            $"Cannot format {amount} without proper currency service. " +
            $"Inject ICurrencyFormattingService and StoreConfig into StructuredReceiptFormatter constructor.");
    }
}

/// <summary>
/// Represents a modification on a receipt item with hierarchical sub-modifications.
/// Supports Starbucks-style nested modifications like drink customizations.
/// </summary>
public class ReceiptModification
{
    public string Name { get; set; } = "";
    public decimal Cost { get; set; }
    public List<ReceiptModification> SubModifications { get; set; } = new();
}
