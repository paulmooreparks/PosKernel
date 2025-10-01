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

namespace PosKernel.Abstractions;

/// <summary>
/// NRF ARTS-compliant line item type enumeration.
/// Defines the standard types of line items in POS transactions.
/// </summary>
public enum LineItemType
{
    /// <summary>
    /// Standard sale item.
    /// </summary>
    Sale = 0,

    /// <summary>
    /// Return/refund item.
    /// </summary>
    Return = 1,

    /// <summary>
    /// Void/cancel item.
    /// </summary>
    Void = 2,

    /// <summary>
    /// Modification to another item (e.g., "extra cheese").
    /// </summary>
    Modification = 3,

    /// <summary>
    /// Discount applied to item or transaction.
    /// </summary>
    Discount = 4,

    /// <summary>
    /// Tax line item.
    /// </summary>
    Tax = 5,

    /// <summary>
    /// Service charge or fee.
    /// </summary>
    ServiceCharge = 6,

    /// <summary>
    /// Tip or gratuity.
    /// </summary>
    Tip = 7
}

/// <summary>
/// Represents the current state of a transaction lifecycle in the AI-first architecture.
/// States drive AI behavior - no prompts or orchestration logic.
/// </summary>
public enum TransactionState
{
    /// <summary>
    /// Fresh transaction ready for customer interaction.
    /// AI sees this state → provides natural greeting.
    /// </summary>
    StartTransaction = 0,

    /// <summary>
    /// Items added but transaction incomplete.
    /// AI sees this state → offers suggestions and clarifications.
    /// </summary>
    ItemsPending = 1,

    /// <summary>
    /// Ready for payment processing.
    /// AI sees this state → guides payment method selection.
    /// </summary>
    PaymentPending = 2,

    /// <summary>
    /// Transaction complete, ready for next customer.
    /// AI sees this state → provides receipt and farewell, then auto-transitions to StartTransaction.
    /// </summary>
    EndOfTransaction = 3,

    /// <summary>
    /// The transaction was voided and should not be used further.
    /// </summary>
    Voided = 4,

    /// <summary>
    /// Legacy state - maps to ItemsPending for backward compatibility.
    /// </summary>
    [Obsolete("Use ItemsPending instead")]
    Building = 1,

    /// <summary>
    /// Legacy state - maps to EndOfTransaction for backward compatibility.
    /// </summary>
    [Obsolete("Use EndOfTransaction instead")]
    Completed = 3
}

/// <summary>
/// ARCHITECTURAL PRINCIPLE: Pure data holder for displaying POS kernel line item calculations.
/// This class NEVER performs calculations - all values are set from authoritative POS kernel responses.
/// Includes ALL fields from kernel TransactionLineItem for complete display capabilities.
/// </summary>
public sealed class TransactionLine
{
    /// <summary>
    /// Gets the unique identifier for this line item within the transaction.
    /// Legacy field - use LineItemId for kernel compatibility.
    /// </summary>
    public LineItemId LineId { get; init; } = new LineItemId(Guid.Empty);

    /// <summary>
    /// Gets or sets the stable line item identifier assigned by the kernel.
    /// ARCHITECTURAL PRINCIPLE: Stable ID remains constant even when line numbers change due to voids.
    /// Use this for precise targeting of modifications, not the line number.
    /// </summary>
    public string LineItemId { get; set; } = "";

    /// <summary>
    /// Gets or sets the line number (1-based for POS display/void operations).
    /// NOTE: Line numbers may change due to voids/insertions. Use LineItemId for stable references.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Gets or sets the parent line number (0 for base items, parent line for modifications).
    /// </summary>
    public int ParentLineNumber { get; set; }

    /// <summary>
    /// Gets the product identifier associated with this line.
    /// </summary>
    public ProductId ProductId { get; init; } = new("");

    /// <summary>
    /// Gets or sets the product identifier or modification ID (kernel format).
    /// </summary>
    public string ProductIdString { get; set; } = "";

    /// <summary>
    /// Gets or sets the NRF ARTS-compliant line item type.
    /// </summary>
    public LineItemType ItemType { get; set; }

    /// <summary>
    /// Gets or sets the quantity for the product on this line.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the unit price for the product.
    /// ARCHITECTURAL PRINCIPLE: Value ONLY set from POS kernel, never calculated in AI layer.
    /// </summary>
    public Money UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the extended price for this line item.
    /// ARCHITECTURAL PRINCIPLE: Value ONLY set from POS kernel calculations, never computed in AI layer.
    /// </summary>
    public Money Extended { get; set; }

    /// <summary>
    /// Gets or sets the display indent level for NRF-style receipt formatting.
    /// </summary>
    public int DisplayIndentLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this line item is voided.
    /// </summary>
    public bool IsVoided { get; set; }

    /// <summary>
    /// Gets or sets the void reason.
    /// </summary>
    public string? VoidReason { get; set; }

    /// <summary>
    /// Gets or sets additional product information for display (name, description, etc.).
    /// Populated from restaurant extension or other store services.
    /// </summary>
    public string ProductName { get; set; } = "";

    /// <summary>
    /// Gets or sets the product description for detailed display.
    /// </summary>
    public string ProductDescription { get; set; } = "";

    /// <summary>
    /// Gets or sets additional metadata from the kernel or extensions.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// ARCHITECTURAL PRINCIPLE: Constructor requires currency for Money initialization.
    /// All Money properties initialized with correct currency from store configuration.
    /// </summary>
    public TransactionLine(string currency)
    {
        UnitPrice = Money.Zero(currency);
        Extended = Money.Zero(currency);
    }

    /// <summary>
    /// Default constructor for serialization - requires currency to be set explicitly.
    /// </summary>
    public TransactionLine()
    {
        // Note: UnitPrice and Extended must be set explicitly with correct currency
    }
}

/// <summary>
/// ARCHITECTURAL PRINCIPLE: Pure data transfer object for displaying POS kernel calculations.
/// This class NEVER performs calculations - all values are set from authoritative POS kernel responses.
/// Currency is determined by store configuration, never hardcoded.
/// </summary>
public sealed class Transaction
{
    /// <summary>
    /// Gets the unique transaction identifier.
    /// </summary>
    public TransactionId Id { get; init; } = TransactionId.New();

    /// <summary>
    /// Gets or sets the current state of the transaction.
    /// </summary>
    public TransactionState State { get; set; } = TransactionState.StartTransaction;

    /// <summary>
    /// Gets the ISO 4217 currency code for the transaction.
    /// ARCHITECTURAL PRINCIPLE: Must be provided from store configuration, never hardcoded.
    /// </summary>
    public string Currency { get; init; }

    /// <summary>
    /// Gets the collection of line items for this transaction.
    /// </summary>
    public List<TransactionLine> Lines { get; } = new();

    /// <summary>
    /// Gets or sets the total amount for this transaction.
    /// ARCHITECTURAL PRINCIPLE: Value ONLY set from POS kernel calculations, never computed in AI layer.
    /// </summary>
    public Money Total { get; set; }

    /// <summary>
    /// Gets or sets the total tendered amount applied to the transaction.
    /// ARCHITECTURAL PRINCIPLE: Value ONLY set from POS kernel calculations, never computed in AI layer.
    /// </summary>
    public Money Tendered { get; set; }

    /// <summary>
    /// Gets or sets the change due amount.
    /// ARCHITECTURAL PRINCIPLE: Value ONLY set from POS kernel calculations, never computed in AI layer.
    /// </summary>
    public Money ChangeDue { get; set; }

    /// <summary>
    /// ARCHITECTURAL PRINCIPLE: Constructor requires currency from store configuration.
    /// No hardcoded currency defaults - fail fast if not provided.
    /// </summary>
    public Transaction(string currency)
    {
        Currency = currency ?? throw new ArgumentNullException(nameof(currency));
        Total = Money.Zero(currency);
        Tendered = Money.Zero(currency);
        ChangeDue = Money.Zero(currency);
    }

    /// <summary>
    /// ARCHITECTURAL PRINCIPLE: Updates transaction state from authoritative POS kernel data.
    /// This method receives calculated values from kernel and updates display properties.
    /// No calculations performed in AI layer.
    /// </summary>
    public void UpdateFromKernel(Money total, Money tendered, Money changeDue, TransactionState state)
    {
        Total = total;
        Tendered = tendered;
        ChangeDue = changeDue;
        State = state;
    }
}
