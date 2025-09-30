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
using PosKernel.AI.Models;
using PosKernel.AI.Services;
using PosKernel.Configuration;
using PosKernel.Configuration.Services;

namespace PosKernel.AI.Demo.UI.Terminal;

/// <summary>
/// Structured receipt formatter with proper alignment, hierarchical modifications, and currency service integration.
/// ARCHITECTURAL PRINCIPLE: Pure infrastructure - no cultural assumptions or business logic.
/// AI owns all cultural intelligence; formatter simply displays data as provided.
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

        // Payment section - show payment status if completed
        if (receipt.Status == PaymentStatus.Completed)
        {
            content.AppendLine(new string('-', _receiptWidth));
            content.AppendLine(FormatTotalLine("PAID", receipt.Total));
        }
    }
    else
    {
        content.AppendLine();
        content.AppendLine(CenterText("(Empty Order)"));
    }

    // Footer section
    content.AppendLine(new string('-', _receiptWidth));        return content.ToString();
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
    /// ARCHITECTURAL PRINCIPLE: No cultural parsing - AI handles all cultural intelligence.
    /// Simply structure the data as provided without interpretation.
    /// </summary>
    private List<ReceiptModification> ParseModifications(string preparationNotes)
    {
        var modifications = new List<ReceiptModification>();

        if (string.IsNullOrEmpty(preparationNotes))
        {
            return modifications;
        }

        // Handle set customizations (e.g., "with Custom Drink")
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
    /// ARCHITECTURAL FIX: No longer parse cultural terminology - AI handles all cultural intelligence.
    /// Simply return customization as-provided by AI without cultural assumptions.
    /// </summary>
    private List<ReceiptModification> ParseSetCustomization(string customization)
    {
        var modifications = new List<ReceiptModification>();

        // ARCHITECTURAL PRINCIPLE: No cultural hardcoding - AI owns cultural intelligence
        // Simply display customization as provided without parsing cultural terms
        modifications.Add(new ReceiptModification
        {
            Name = customization.Trim(),
            Cost = 0,
            SubModifications = new List<ReceiptModification>()
        });

        return modifications;
    }

    /// <summary>
    /// ARCHITECTURAL FIX: No cultural normalization - AI handles all language intelligence.
    /// Simply return the name as provided without cultural assumptions.
    /// </summary>
    private string NormalizeDrinkName(string drinkName)
    {
        // ARCHITECTURAL PRINCIPLE: No cultural hardcoding - return name as-is
        return drinkName?.Trim() ?? "";
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
