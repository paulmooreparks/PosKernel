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

using System;
using System.Text;
using System.Runtime.InteropServices;

namespace PosKernel.Host;

/// <summary>
/// Provides a clean .NET wrapper around the POS Kernel Rust library.
/// </summary>
public static class Pos
{
    /// <summary>
    /// Gets the version of the POS Kernel library.
    /// </summary>
    public static string Version
    {
        get
        {
            var versionPtr = RustNative.pk_get_version();
            return RustNative.PtrToStringUTF8(versionPtr);
        }
    }

    /// <summary>
    /// Validates a currency code without creating a transaction.
    /// Useful for UI validation before transaction creation.
    /// </summary>
    /// <param name="currencyCode">3-letter currency code to validate</param>
    /// <returns>True if the currency code is valid</returns>
    public static bool IsValidCurrency(string currencyCode)
    {
        if (string.IsNullOrEmpty(currencyCode) || currencyCode.Length != 3)
        {
            return false;
        }

        var currencyBytes = Encoding.UTF8.GetBytes(currencyCode.ToUpperInvariant());
        var result = RustNative.pk_validate_currency_code(currencyBytes, (UIntPtr)currencyBytes.Length);
        return RustNative.pk_result_is_ok(result);
    }

    /// <summary>
    /// Check if a currency code is a well-known standard currency (gets fast-path optimization).
    /// </summary>
    /// <param name="currencyCode">3-letter currency code</param>
    /// <returns>True if this is a well-known standard currency (USD, EUR, JPY, GBP, CAD, AUD)</returns>
    public static bool IsStandardCurrency(string currencyCode)
    {
        if (string.IsNullOrEmpty(currencyCode) || currencyCode.Length != 3)
        {
            return false;
        }

        var currencyBytes = Encoding.UTF8.GetBytes(currencyCode.ToUpperInvariant());
        return RustNative.pk_is_standard_currency(currencyBytes, (UIntPtr)currencyBytes.Length);
    }

    /// <summary>
    /// Gets information about a currency code.
    /// </summary>
    /// <param name="currencyCode">3-letter currency code</param>
    /// <returns>Currency information or null if invalid</returns>
    public static CurrencyInfo? GetCurrencyInfo(string currencyCode)
    {
        if (!IsValidCurrency(currencyCode))
        {
            return null;
        }

        bool isStandard = IsStandardCurrency(currencyCode);
        
        // For standard currencies, we know the decimal places
        byte decimalPlaces = currencyCode.ToUpperInvariant() switch
        {
            "JPY" => 0, // Yen has no decimal places
            "USD" or "EUR" or "GBP" or "CAD" or "AUD" => 2,
            _ => 2 // Default for custom currencies
        };

        return new CurrencyInfo
        {
            Code = currencyCode.ToUpperInvariant(),
            IsStandard = isStandard,
            DecimalPlaces = decimalPlaces
        };
    }

    /// <summary>
    /// Begins a new POS transaction.
    /// </summary>
    /// <param name="store">Store identifier</param>
    /// <param name="currency">3-letter currency code (e.g., "USD", "EUR", "BTC", "PTS")</param>
    /// <returns>A transaction handle for use in subsequent operations</returns>
    /// <exception cref="PosException">Thrown when the operation fails</exception>
    /// <exception cref="ArgumentException">Thrown when currency code is invalid</exception>
    public static ulong BeginTransaction(string store, string currency)
    {
        // Pre-validate currency to give better error messages
        if (!IsValidCurrency(currency))
        {
            throw new ArgumentException("Currency code must be exactly 3 characters and contain only letters", nameof(currency));
        }

        var storeBytes = Encoding.UTF8.GetBytes(store);
        var currencyBytes = Encoding.UTF8.GetBytes(currency.ToUpperInvariant());
        
        var result = RustNative.pk_begin_transaction(
            storeBytes, (UIntPtr)storeBytes.Length,
            currencyBytes, (UIntPtr)currencyBytes.Length,
            out var handle);
        
        EnsureSuccess(result, nameof(BeginTransaction));
        return handle;
    }

    /// <summary>
    /// Closes a transaction and releases its resources.
    /// </summary>
    /// <param name="handle">Transaction handle</param>
    /// <exception cref="PosException">Thrown when the operation fails</exception>
    public static void CloseTransaction(ulong handle)
    {
        var result = RustNative.pk_close_transaction(handle);
        EnsureSuccess(result, nameof(CloseTransaction));
    }

    /// <summary>
    /// Creates a new transaction object for easier management.
    /// </summary>
    /// <param name="store">Store identifier</param>
    /// <param name="currency">3-letter currency code (e.g., "USD", "EUR", "BTC", "PTS")</param>
    /// <returns>A Transaction object</returns>
    public static Transaction CreateTransaction(string store, string currency)
    {
        var handle = BeginTransaction(store, currency);
        return new Transaction(handle, store, currency);
    }

    /// <summary>
    /// Adds a line item to the transaction.
    /// </summary>
    /// <param name="handle">Transaction handle</param>
    /// <param name="sku">Product SKU</param>
    /// <param name="quantity">Quantity of items</param>
    /// <param name="unitPrice">Unit price in major currency units (e.g., dollars)</param>
    /// <exception cref="PosException">Thrown when the operation fails</exception>
    public static void AddLine(ulong handle, string sku, int quantity, decimal unitPrice)
    {
        var skuBytes = Encoding.UTF8.GetBytes(sku);
        
        // Get currency decimal places for proper conversion
        var decimalPlaces = GetCurrencyDecimalPlaces(handle);
        var multiplier = (long)Math.Pow(10, decimalPlaces);
        var unitMinor = (long)(unitPrice * multiplier);
        
        var result = RustNative.pk_add_line(handle, skuBytes, (UIntPtr)skuBytes.Length, quantity, unitMinor);
        EnsureSuccess(result, nameof(AddLine));
    }

    /// <summary>
    /// Adds a cash tender to the transaction.
    /// </summary>
    /// <param name="handle">Transaction handle</param>
    /// <param name="amount">Cash amount in major currency units (e.g., dollars)</param>
    /// <exception cref="PosException">Thrown when the operation fails</exception>
    public static void AddCashTender(ulong handle, decimal amount)
    {
        // Get currency decimal places for proper conversion
        var decimalPlaces = GetCurrencyDecimalPlaces(handle);
        var multiplier = (long)Math.Pow(10, decimalPlaces);
        var amountMinor = (long)(amount * multiplier);
        
        var result = RustNative.pk_add_cash_tender(handle, amountMinor);
        EnsureSuccess(result, nameof(AddCashTender));
    }

    /// <summary>
    /// Gets the current totals for a transaction.
    /// </summary>
    /// <param name="handle">Transaction handle</param>
    /// <returns>Transaction totals</returns>
    /// <exception cref="PosException">Thrown when the operation fails</exception>
    public static TransactionTotals GetTotals(ulong handle)
    {
        var result = RustNative.pk_get_totals(handle, out var totalMinor, out var tenderedMinor, out var changeMinor, out var state);
        EnsureSuccess(result, nameof(GetTotals));

        // Get currency decimal places for proper conversion
        var decimalPlaces = GetCurrencyDecimalPlaces(handle);
        var divisor = (decimal)Math.Pow(10, decimalPlaces);

        return new TransactionTotals
        {
            Total = totalMinor / divisor,
            Tendered = tenderedMinor / divisor,
            Change = changeMinor / divisor,
            State = (TransactionState)state
        };
    }

    /// <summary>
    /// Gets the number of decimal places for the transaction's currency.
    /// </summary>
    /// <param name="handle">Transaction handle</param>
    /// <returns>Number of decimal places (0 for JPY, 2 for most others)</returns>
    /// <exception cref="PosException">Thrown when the operation fails</exception>
    public static byte GetCurrencyDecimalPlaces(ulong handle)
    {
        var result = RustNative.pk_get_currency_decimal_places(handle, out var decimalPlaces);
        EnsureSuccess(result, nameof(GetCurrencyDecimalPlaces));
        return decimalPlaces;
    }

    /// <summary>
    /// Gets the number of line items in a transaction.
    /// </summary>
    /// <param name="handle">Transaction handle</param>
    /// <returns>Number of line items</returns>
    /// <exception cref="PosException">Thrown when the operation fails</exception>
    public static uint GetLineCount(ulong handle)
    {
        var result = RustNative.pk_get_line_count(handle, out var count);
        EnsureSuccess(result, nameof(GetLineCount));
        return count;
    }

    /// <summary>
    /// Gets the store name for a transaction.
    /// </summary>
    /// <param name="handle">Transaction handle</param>
    /// <returns>Store name</returns>
    /// <exception cref="PosException">Thrown when the operation fails</exception>
    public static string GetStoreName(ulong handle)
    {
        return RustNative.GetStoreName(handle);
    }

    /// <summary>
    /// Gets the currency code for a transaction.
    /// </summary>
    /// <param name="handle">Transaction handle</param>
    /// <returns>Currency code</returns>
    /// <exception cref="PosException">Thrown when the operation fails</exception>
    public static string GetCurrency(ulong handle)
    {
        return RustNative.GetCurrency(handle);
    }

    private static void EnsureSuccess(RustNative.PkResult result, string operationName)
    {
        if (!RustNative.pk_result_is_ok(result))
        {
            throw new PosException($"POS Kernel operation '{operationName}' failed with code {result.code}");
        }
    }
}

/// <summary>
/// Represents a POS transaction with object-oriented convenience methods and automatic resource management.
/// </summary>
public class Transaction : IDisposable
{
    private bool _disposed = false;

    /// <summary>
    /// The internal handle for this transaction.
    /// </summary>
    public ulong Handle { get; private set; }

    /// <summary>
    /// The store identifier for this transaction.
    /// </summary>
    public string Store => _disposed ? throw new ObjectDisposedException(nameof(Transaction)) : Pos.GetStoreName(Handle);

    /// <summary>
    /// The currency code for this transaction.
    /// </summary>
    public string Currency => _disposed ? throw new ObjectDisposedException(nameof(Transaction)) : Pos.GetCurrency(Handle);

    /// <summary>
    /// Gets the number of line items in this transaction.
    /// </summary>
    public uint LineCount => _disposed ? throw new ObjectDisposedException(nameof(Transaction)) : Pos.GetLineCount(Handle);

    internal Transaction(ulong handle, string store, string currency)
    {
        Handle = handle;
    }

    /// <summary>
    /// Adds a line item to this transaction.
    /// </summary>
    /// <param name="sku">Product SKU</param>
    /// <param name="quantity">Quantity of items</param>
    /// <param name="unitPrice">Unit price in major currency units</param>
    public void AddLine(string sku, int quantity, decimal unitPrice)
    {
        ThrowIfDisposed();
        Pos.AddLine(Handle, sku, quantity, unitPrice);
    }

    /// <summary>
    /// Adds multiple units of the same item to this transaction.
    /// </summary>
    /// <param name="sku">Product SKU</param>
    /// <param name="quantity">Quantity of items</param>
    /// <param name="unitPrice">Unit price in major currency units</param>
    public void AddItems(string sku, int quantity, decimal unitPrice)
    {
        AddLine(sku, quantity, unitPrice);
    }

    /// <summary>
    /// Adds a single item to this transaction.
    /// </summary>
    /// <param name="sku">Product SKU</param>
    /// <param name="unitPrice">Unit price in major currency units</param>
    public void AddItem(string sku, decimal unitPrice)
    {
        AddLine(sku, 1, unitPrice);
    }

    /// <summary>
    /// Adds a cash tender to this transaction.
    /// </summary>
    /// <param name="amount">Cash amount in major currency units</param>
    public void AddCashTender(decimal amount)
    {
        ThrowIfDisposed();
        Pos.AddCashTender(Handle, amount);
    }

    /// <summary>
    /// Gets the current totals for this transaction.
    /// </summary>
    public TransactionTotals Totals 
    { 
        get 
        { 
            ThrowIfDisposed();
            return Pos.GetTotals(Handle); 
        } 
    }

    /// <summary>
    /// Gets whether this transaction is completed (sufficient tender provided).
    /// </summary>
    public bool IsCompleted => Totals.State == TransactionState.Completed;

    /// <summary>
    /// Gets whether this transaction is still building (insufficient tender).
    /// </summary>
    public bool IsBuilding => Totals.State == TransactionState.Building;

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(Transaction));
        }
    }

    /// <summary>
    /// Closes the transaction and releases its resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && Handle != RustNative.PK_INVALID_HANDLE)
        {
            try
            {
                Pos.CloseTransaction(Handle);
            }
            catch (PosException)
            {
                // Ignore disposal errors
            }
            
            Handle = RustNative.PK_INVALID_HANDLE;
            _disposed = true;
        }
    }

    ~Transaction()
    {
        Dispose(false);
    }
}

/// <summary>
/// Represents the totals for a POS transaction.
/// </summary>
public class TransactionTotals
{
    /// <summary>
    /// The total amount of the transaction.
    /// </summary>
    public decimal Total { get; init; }

    /// <summary>
    /// The amount tendered by the customer.
    /// </summary>
    public decimal Tendered { get; init; }

    /// <summary>
    /// The change due to the customer.
    /// </summary>
    public decimal Change { get; init; }

    /// <summary>
    /// The current state of the transaction.
    /// </summary>
    public TransactionState State { get; init; }

    /// <summary>
    /// Gets whether sufficient tender has been provided.
    /// </summary>
    public bool IsPaid => State == TransactionState.Completed;

    /// <summary>
    /// Gets the remaining balance due (negative if overpaid).
    /// </summary>
    public decimal Balance => Total - Tendered;
}

/// <summary>
/// Represents the state of a POS transaction.
/// </summary>
public enum TransactionState
{
    /// <summary>
    /// Transaction is still being built (items being added).
    /// </summary>
    Building = 0,

    /// <summary>
    /// Transaction is completed (sufficient tender provided).
    /// </summary>
    Completed = 1
}

/// <summary>
/// Exception thrown when a POS Kernel operation fails.
/// </summary>
public class PosException : Exception
{
    public PosException(string message) : base(message)
    {
    }

    public PosException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Information about a currency.
/// </summary>
public class CurrencyInfo
{
    /// <summary>
    /// 3-letter currency code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Whether this is a well-known standard currency with fast-path optimization.
    /// </summary>
    public required bool IsStandard { get; init; }

    /// <summary>
    /// Number of decimal places for this currency.
    /// </summary>
    public required byte DecimalPlaces { get; init; }

    /// <summary>
    /// Gets a human-readable description of the currency type.
    /// </summary>
    public string Type => IsStandard ? "Standard" : "Custom";
}
