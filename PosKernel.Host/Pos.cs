using System.Text;

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
    /// Begins a new POS transaction.
    /// </summary>
    /// <param name="store">Store identifier</param>
    /// <param name="currency">Currency code (e.g., "USD")</param>
    /// <returns>A transaction handle for use in subsequent operations</returns>
    /// <exception cref="PosException">Thrown when the operation fails</exception>
    public static ulong BeginTransaction(string store, string currency)
    {
        var storeBytes = Encoding.UTF8.GetBytes(store);
        var currencyBytes = Encoding.UTF8.GetBytes(currency);
        
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
    /// <param name="currency">Currency code (e.g., "USD")</param>
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
        var unitMinor = (long)(unitPrice * 100); // Convert to minor units (cents)
        
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
        var amountMinor = (long)(amount * 100); // Convert to minor units (cents)
        
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

        return new TransactionTotals
        {
            Total = (decimal)totalMinor / 100m,
            Tendered = (decimal)tenderedMinor / 100m,
            Change = (decimal)changeMinor / 100m,
            State = (TransactionState)state
        };
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
