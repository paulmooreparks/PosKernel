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
/// Strongly-typed identifier for transactions providing type safety over raw GUID usage.
/// </summary>
/// <param name="Value">The underlying GUID value.</param>
public readonly record struct TransactionId(Guid Value)
{
    /// <summary>
    /// Creates a new unique transaction identifier.
    /// </summary>
    public static TransactionId New() => new(Guid.NewGuid());
    /// <summary>
    /// Returns the identifier as a 32-digit hexadecimal string with no separators.
    /// </summary>
    public override string ToString() => Value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for products providing clarity and reducing accidental mix-ups.
/// </summary>
/// <param name="Value">The opaque product identifier (SKU/UPC/etc.).</param>
public readonly record struct ProductId(string Value)
{
    /// <summary>
    /// Returns the raw product identifier string.
    /// </summary>
    public override string ToString() => Value;
}

/// <summary>
/// Strongly-typed identifier for transaction line items.
/// </summary>
/// <param name="Value">The underlying GUID value.</param>
public readonly record struct LineItemId(Guid Value)
{
    /// <summary>
    /// Creates a new line item identifier.
    /// </summary>
    public static LineItemId New() => new(Guid.NewGuid());
    /// <summary>
    /// Returns the identifier as a 32-digit hexadecimal string with no separators.
    /// </summary>
    public override string ToString() => Value.ToString("N");
}
