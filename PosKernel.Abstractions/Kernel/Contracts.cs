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
/// Represents the current state of a transaction lifecycle.
/// </summary>
public enum TransactionState
{
    /// <summary>
    /// Items and tenders are still being added.
    /// </summary>
    Building,

    /// <summary>
    /// The transaction has sufficient tender and is finalized.
    /// </summary>
    Completed,

    /// <summary>
    /// The transaction was voided and should not be used further.
    /// </summary>
    Voided
}

/// <summary>
/// Represents a single line item within a transaction including quantity and pricing.
/// </summary>
public sealed class TransactionLine
{
    /// <summary>
    /// Gets the unique identifier for this line item within the transaction.
    /// </summary>
    public LineItemId LineId { get; init; } = LineItemId.New();

    /// <summary>
    /// Gets the product identifier associated with this line.
    /// </summary>
    public ProductId ProductId { get; init; } = new("");

    /// <summary>
    /// Gets or sets the quantity for the product on this line.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the unit price for the product (in minor currency units).
    /// </summary>
    public Money UnitPrice { get; set; }

    /// <summary>
    /// Gets the extended price (unit price multiplied by quantity).
    /// </summary>
    public Money Extended => new(UnitPrice.MinorUnits * Quantity, UnitPrice.Currency);
}

/// <summary>
/// Represents a complete point-of-sale transaction composed of line items and tenders.
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
    public TransactionState State { get; set; } = TransactionState.Building;

    /// <summary>
    /// Gets the ISO 4217 currency code for the transaction.
    /// </summary>
    public string Currency { get; init; } = "USD";

    /// <summary>
    /// Gets the collection of line items for this transaction.
    /// </summary>
    public List<TransactionLine> Lines { get; } = new();

    /// <summary>
    /// Gets the total extended amount of all line items.
    /// </summary>
    public Money Total => Lines.Aggregate(Money.Zero(Currency), (acc, l) => acc.Add(l.Extended));

    /// <summary>
    /// Gets the total tendered amount applied to the transaction.
    /// </summary>
    public Money Tendered { get; private set; } = Money.Zero("USD");

    /// <summary>
    /// Gets the change due (zero if not fully paid or if exact payment).
    /// </summary>
    public Money ChangeDue => Tendered.MinorUnits > Total.MinorUnits ? new(Tendered.MinorUnits - Total.MinorUnits, Currency) : Money.Zero(Currency);

    /// <summary>
    /// Adds a line item to the transaction while in the <see cref="TransactionState.Building"/> state.
    /// </summary>
    /// <param name="productId">The product identifier to add.</param>
    /// <param name="quantity">The quantity being purchased.</param>
    /// <param name="unitPrice">The unit price for the product.</param>
    /// <exception cref="InvalidOperationException">Thrown if the transaction is not in the Building state.</exception>
    public void AddLine(ProductId productId, int quantity, Money unitPrice)
    {
        if (State != TransactionState.Building)
        {
            throw new InvalidOperationException("Cannot add line when not building");
        }

        var line = new TransactionLine
        {
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice
        };
        Lines.Add(line);
    }

    /// <summary>
    /// Adds a cash tender amount while in the <see cref="TransactionState.Building"/> state. Automatically
    /// transitions the transaction to <see cref="TransactionState.Completed"/> when total tendered meets or exceeds the total.
    /// </summary>
    /// <param name="amount">The cash amount tendered.</param>
    /// <exception cref="InvalidOperationException">Thrown if the transaction is not in the Building state.</exception>
    public void AddCashTender(Money amount)
    {
        if (State != TransactionState.Building)
        {
            throw new InvalidOperationException("Cannot tender when not building");
        }
        Tendered = new Money(Tendered.MinorUnits + amount.MinorUnits, amount.Currency);
        if (Tendered.MinorUnits >= Total.MinorUnits)
        {
            State = TransactionState.Completed;
        }
    }
}
