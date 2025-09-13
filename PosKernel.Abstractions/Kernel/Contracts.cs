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
/// Represents the current state of a transaction.
/// </summary>
public enum TransactionState
{
    Building,
    Completed,
    Voided
}

/// <summary>
/// Represents a line item within a transaction.
/// </summary>
public sealed class TransactionLine
{
    public LineItemId LineId { get; init; } = LineItemId.New();
    public ProductId ProductId { get; init; } = new("");
    public int Quantity { get; set; }
    public Money UnitPrice { get; set; }
    public Money Extended => new(UnitPrice.MinorUnits * Quantity, UnitPrice.Currency);
}

/// <summary>
/// Represents a complete point-of-sale transaction.
/// </summary>
public sealed class Transaction
{
    public TransactionId Id { get; init; } = TransactionId.New();
    public TransactionState State { get; set; } = TransactionState.Building;
    public string Currency { get; init; } = "USD";
    public List<TransactionLine> Lines { get; } = new();
    public Money Total => Lines.Aggregate(Money.Zero(Currency), (acc, l) => acc.Add(l.Extended));
    public Money Tendered { get; private set; } = Money.Zero("USD");
    public Money ChangeDue => Tendered.MinorUnits > Total.MinorUnits ? new(Tendered.MinorUnits - Total.MinorUnits, Currency) : Money.Zero(Currency);

    /// <summary>
    /// Adds a line item to the transaction.
    /// </summary>
    public void AddLine(ProductId productId, int quantity, Money unitPrice)
    {
        if (State != TransactionState.Building) throw new InvalidOperationException("Cannot add line when not building");
        Lines.Add(new TransactionLine { ProductId = productId, Quantity = quantity, UnitPrice = unitPrice });
    }

    /// <summary>
    /// Adds a cash tender to the transaction.
    /// </summary>
    public void AddCashTender(Money amount)
    {
        if (State != TransactionState.Building) throw new InvalidOperationException("Cannot tender when not building");
        Tendered = Tendered.Add(amount);
        if (Tendered.MinorUnits >= Total.MinorUnits) State = TransactionState.Completed;
    }
}
