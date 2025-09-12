# .NET Wrapper Documentation

This document describes the .NET wrapper for the POS Kernel, including the static API, object-oriented wrapper, and integration patterns.

## Overview

The .NET wrapper provides two complementary APIs:
1. **Static API** (`Pos` class) - Direct mapping to C functions
2. **Object-Oriented API** (`Transaction` class) - Managed wrapper with automatic resource disposal

Both APIs handle currency conversion, string marshaling, and error management automatically.

## Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│ C# Application  │───▶│ .NET Wrapper     │───▶│ Rust Library    │
│                 │    │ (Pos/Transaction)│    │ (pos_kernel.dll)│
└─────────────────┘    └──────────────────┘    └─────────────────┘
                              │
                              ▼
                       ┌──────────────────┐
                       │ RustNative       │
                       │ (P/Invoke Layer) │
                       └──────────────────┘
```

## Static API Reference

### Pos Class

The `Pos` static class provides direct access to POS Kernel functions with automatic error handling and type conversion.

#### Properties

```csharp
public static string Version { get; }
```
Gets the POS Kernel version string.

#### Transaction Management

```csharp
public static ulong BeginTransaction(string store, string currency)
```
**Purpose:** Create a new transaction and return its handle.

**Parameters:**
- `store`: Store identifier (any string)
- `currency`: Currency code (e.g., "USD", "EUR")

**Returns:** Transaction handle for use with other methods

**Throws:** `PosException` on failure

```csharp
public static void CloseTransaction(ulong handle)
```
**Purpose:** Close a transaction and free its resources.

**Parameters:**
- `handle`: Transaction handle from `BeginTransaction`

**Throws:** `PosException` if handle is invalid

#### Transaction Operations

```csharp
public static void AddLine(ulong handle, string sku, int quantity, decimal unitPrice)
```
**Purpose:** Add a line item to the transaction.

**Parameters:**
- `handle`: Transaction handle
- `sku`: Product SKU (any string identifier)
- `quantity`: Item quantity (must be positive)
- `unitPrice`: Unit price in major currency units (e.g., dollars)

**Example:**
```csharp
Pos.AddLine(handle, "WIDGET-001", 2, 12.99m); // 2 widgets at $12.99 each
```

```csharp
public static void AddCashTender(ulong handle, decimal amount)
```
**Purpose:** Add cash payment to the transaction.

**Parameters:**
- `handle`: Transaction handle
- `amount`: Cash amount in major currency units

#### Query Operations

```csharp
public static TransactionTotals GetTotals(ulong handle)
```
**Purpose:** Get current transaction totals and state.

**Returns:** `TransactionTotals` object with calculated amounts

```csharp
public static uint GetLineCount(ulong handle)
public static string GetStoreName(ulong handle)
public static string GetCurrency(ulong handle)
```
**Purpose:** Retrieve specific transaction properties.

### TransactionTotals Class

```csharp
public class TransactionTotals
{
    public decimal Total { get; init; }      // Total amount due
    public decimal Tendered { get; init; }   // Amount paid by customer
    public decimal Change { get; init; }     // Change due to customer
    public TransactionState State { get; init; } // Current state
    
    // Computed properties
    public bool IsPaid => State == TransactionState.Completed;
    public decimal Balance => Total - Tendered; // Positive = still owed
}
```

### TransactionState Enum

```csharp
public enum TransactionState
{
    Building = 0,    // Adding items, accepting payment
    Completed = 1    // Sufficient payment received
}
```

## Object-Oriented API Reference

### Transaction Class

The `Transaction` class provides an object-oriented wrapper with automatic resource management via `IDisposable`.

#### Creation

```csharp
public static Transaction CreateTransaction(string store, string currency)
```
**Purpose:** Create a managed transaction object.

**Example:**
```csharp
using var transaction = Pos.CreateTransaction("Store-001", "USD");
// Transaction automatically closed when using block exits
```

#### Properties

```csharp
public ulong Handle { get; }           // Internal handle (read-only)
public string Store { get; }           // Store identifier
public string Currency { get; }        // Currency code  
public uint LineCount { get; }         // Number of line items
public TransactionTotals Totals { get; } // Current totals
public bool IsCompleted { get; }       // Sufficient payment received
public bool IsBuilding { get; }        // Still accepting items/payment
```

**Note:** All properties throw `ObjectDisposedException` if accessed after disposal.

#### Methods

```csharp
public void AddLine(string sku, int quantity, decimal unitPrice)
public void AddItems(string sku, int quantity, decimal unitPrice) // Alias for AddLine
public void AddItem(string sku, decimal unitPrice)               // Single item (qty=1)
public void AddCashTender(decimal amount)
```

**Example:**
```csharp
using var tx = Pos.CreateTransaction("Store-001", "USD");
tx.AddItem("BOOK-001", 19.99m);
tx.AddItems("PEN-002", 3, 2.49m);
tx.AddCashTender(30.00m);

var totals = tx.Totals;
Console.WriteLine($"Total: {totals.Total:C}, Change: {totals.Change:C}");
```

#### Resource Management

The `Transaction` class implements `IDisposable` for automatic cleanup:

```csharp
// Automatic disposal with 'using' statement
using var transaction = Pos.CreateTransaction("Store", "USD");
// ... use transaction ...
// Automatically closed here

// Manual disposal
var transaction = Pos.CreateTransaction("Store", "USD");
try {
    // ... use transaction ...
} finally {
    transaction.Dispose(); // Explicit cleanup
}
```

**Disposal Behavior:**
- Safe to call `Dispose()` multiple times
- Properties/methods throw `ObjectDisposedException` after disposal
- Finalizer ensures cleanup even if `Dispose()` is not called

## Error Handling

### PosException

All wrapper methods throw `PosException` for POS Kernel errors:

```csharp
public class PosException : Exception
{
    // Standard exception properties
}
```

**Example:**
```csharp
try 
{
    var handle = Pos.BeginTransaction("Store", "USD");
    Pos.AddLine(handle, "SKU", 1, 10.00m);
}
catch (PosException ex)
{
    Console.WriteLine($"POS Error: {ex.Message}");
}
```

### Error Codes

The wrapper translates C ABI result codes to exceptions:

| Result Code | Exception | Description |
|-------------|-----------|-------------|
| PK_OK | No exception | Operation succeeded |
| PK_NOT_FOUND | PosException | Invalid handle |
| PK_INVALID_STATE | PosException | Operation not allowed in current state |
| PK_VALIDATION_FAILED | PosException | Invalid parameters |
| PK_INTERNAL_ERROR | PosException | System error |

## Usage Patterns

### Basic Static API
```csharp
// Simple transaction processing
var handle = Pos.BeginTransaction("Store-001", "USD");
try 
{
    Pos.AddLine(handle, "ITEM-001", 1, 9.99m);
    Pos.AddLine(handle, "ITEM-002", 2, 4.99m);
    
    var totals = Pos.GetTotals(handle);
    Console.WriteLine($"Total: {totals.Total:C}");
    
    Pos.AddCashTender(handle, 25.00m);
    
    totals = Pos.GetTotals(handle);
    if (totals.IsPaid) 
    {
        Console.WriteLine($"Payment complete. Change: {totals.Change:C}");
    }
}
finally 
{
    Pos.CloseTransaction(handle);
}
```

### Object-Oriented API
```csharp
// Using statement ensures automatic cleanup
using var tx = Pos.CreateTransaction("Store-001", "USD");

// Fluent-style operations
tx.AddItem("COFFEE", 3.99m);
tx.AddItem("MUFFIN", 2.49m);

Console.WriteLine($"Store: {tx.Store}, Items: {tx.LineCount}");

// Check if more payment needed
tx.AddCashTender(10.00m);
if (!tx.IsCompleted) 
{
    var balance = tx.Totals.Balance;
    Console.WriteLine($"Additional payment needed: {balance:C}");
}
```

### Batch Processing
```csharp
// Process multiple transactions
var transactions = new[]
{
    ("Store-A", new[] { ("SKU-1", 1, 5.99m), ("SKU-2", 2, 3.99m) }, 20.00m),
    ("Store-B", new[] { ("SKU-3", 1, 12.99m) }, 15.00m)
};

foreach (var (store, items, payment) in transactions)
{
    using var tx = Pos.CreateTransaction(store, "USD");
    
    foreach (var (sku, qty, price) in items)
        tx.AddLine(sku, qty, price);
    
    tx.AddCashTender(payment);
    
    var totals = tx.Totals;
    Console.WriteLine($"{store}: Total={totals.Total:C} Change={totals.Change:C}");
}
```

## Integration Guidelines

### Currency Handling

The wrapper automatically converts between `decimal` (major units) and `long` (minor units):

```csharp
// .NET side - work with decimals
tx.AddItem("ITEM", 12.99m);      // $12.99
tx.AddCashTender(20.00m);        // $20.00

// Automatic conversion to/from minor units (cents)
// 12.99m → 1299L (cents)
// 20.00m → 2000L (cents)
```

**Precision Notes:**
- Conversion uses standard banker's rounding
- Maximum precision: 2 decimal places (currency minor units)
- Large amounts may lose precision beyond decimal limitations

### String Handling

All string parameters are automatically encoded as UTF-8:

```csharp
// Unicode strings are properly encoded
tx = Pos.CreateTransaction("Магазин-001", "RUB");  // Russian store name
tx.AddItem("商品-ABC", 100.00m);                    // Chinese product SKU
```

### Thread Safety

The wrapper provides the same thread safety guarantees as the underlying C ABI:

```csharp
// Safe - different transactions
var tx1 = Pos.CreateTransaction("Store-A", "USD");
var tx2 = Pos.CreateTransaction("Store-B", "USD");

// Unsafe - same transaction from multiple threads
// Requires external synchronization
```

### Performance Considerations

- **Handle caching**: Transaction handles are simple integers (no marshaling cost)
- **String marshaling**: UTF-8 conversion overhead on property access
- **Memory allocation**: Minimal - most operations are parameter marshaling
- **GC pressure**: Low - few managed objects created

### Best Practices

1. **Use `using` statements** for automatic resource cleanup
2. **Prefer object-oriented API** for new code (better resource management)
3. **Handle PosException** appropriately for your application
4. **Validate inputs** before calling wrapper methods
5. **Use decimal for currency** - avoid float/double precision issues

## Advanced Scenarios

### Custom Error Handling
```csharp
public static class PosExtensions 
{
    public static bool TryAddItem(this Transaction tx, string sku, decimal price)
    {
        try 
        {
            tx.AddItem(sku, price);
            return true;
        }
        catch (PosException)
        {
            return false;
        }
    }
}
```

### Async Operations
```csharp
public static async Task ProcessTransactionAsync(Transaction tx, IEnumerable<(string sku, decimal price)> items)
{
    foreach (var (sku, price) in items)
    {
        tx.AddItem(sku, price);
        await Task.Yield(); // Cooperative yielding for responsiveness
    }
}
```

### Logging Integration
```csharp
public class LoggingTransaction : IDisposable
{
    private readonly Transaction _transaction;
    private readonly ILogger _logger;
    
    public LoggingTransaction(Transaction transaction, ILogger logger)
    {
        _transaction = transaction;
        _logger = logger;
        _logger.LogInformation("Transaction {Handle} created", transaction.Handle);
    }
    
    public void AddItem(string sku, decimal price)
    {
        _transaction.AddItem(sku, price);
        _logger.LogDebug("Added item {Sku} for {Price:C}", sku, price);
    }
    
    public void Dispose()
    {
        _logger.LogInformation("Transaction {Handle} disposed", _transaction.Handle);
        _transaction.Dispose();
    }
}
```
