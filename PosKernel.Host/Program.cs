using System;
using System.Runtime.InteropServices;
using PosKernel.Host;

Console.WriteLine("Loading Rust POS Kernel...");
Console.WriteLine($"Kernel Version: {Pos.Version}");
Console.WriteLine();

// Example 1: Using static methods with manual resource management
Console.WriteLine("=== Static API Example (Manual Resource Management) ===");
try
{
    var handle = Pos.BeginTransaction("Store-1001", "USD");
    Console.WriteLine($"Transaction Handle: {handle}");

    Pos.AddLine(handle, "SKU-1001", 1, 1.99m);
    Pos.AddLine(handle, "SKU-2002", 2, 0.99m);
    
    var totals1 = Pos.GetTotals(handle);
    Console.WriteLine($"Store: {Pos.GetStoreName(handle)} | Currency: {Pos.GetCurrency(handle)} | Lines: {Pos.GetLineCount(handle)}");
    Console.WriteLine($"After adding items: {totals1.Total:C} (State: {totals1.State})");

    Pos.AddCashTender(handle, 5.00m);
    
    var totals2 = Pos.GetTotals(handle);
    Console.WriteLine($"After payment: Total={totals2.Total:C} Tendered={totals2.Tendered:C} Change={totals2.Change:C} State={totals2.State}");
    
    // Manually close transaction
    Pos.CloseTransaction(handle);
    Console.WriteLine("Transaction closed manually");
}
catch (PosException ex)
{
    Console.WriteLine($"POS Error: {ex.Message}");
}

Console.WriteLine();

// Example 2: Using object-oriented API with automatic resource management
Console.WriteLine("=== Object-Oriented API Example (Automatic Resource Management) ===");
try
{
    using var transaction = Pos.CreateTransaction("Store-2002", "USD");
    Console.WriteLine($"Created transaction with handle: {transaction.Handle}");

    // Add items using convenience methods
    transaction.AddItem("BOOK-001", 12.99m);
    transaction.AddItems("PEN-002", 3, 2.49m);
    
    Console.WriteLine($"Store: {transaction.Store} | Currency: {transaction.Currency} | Lines: {transaction.LineCount}");
    Console.WriteLine($"After adding items: {transaction.Totals.Total:C} (Building: {transaction.IsBuilding})");
    
    // Add cash tender
    transaction.AddCashTender(20.00m);
    
    var totals = transaction.Totals;
    Console.WriteLine($"Final: Total={totals.Total:C} Tendered={totals.Tendered:C} Change={totals.Change:C}");
    Console.WriteLine($"Transaction completed: {transaction.IsCompleted}");
    Console.WriteLine($"Balance due: {totals.Balance:C} (Paid: {totals.IsPaid})");
    
    // Transaction will be automatically closed when using block exits
}
catch (PosException ex)
{
    Console.WriteLine($"POS Error: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
}

Console.WriteLine();

// Example 3: Demonstrating resource cleanup
Console.WriteLine("=== Resource Management Demonstration ===");
try
{
    Transaction transaction;
    
    {
        using (transaction = Pos.CreateTransaction("Store-3003", "EUR"))
        {
            transaction.AddItem("WIDGET-001", 5.99m);
            Console.WriteLine($"Transaction {transaction.Handle} created and item added");
        } // Transaction automatically disposed here
    }
    
    // This will throw ObjectDisposedException
    try
    {
        var store = transaction.Store;
    }
    catch (ObjectDisposedException)
    {
        Console.WriteLine("✓ Transaction properly disposed - accessing properties throws ObjectDisposedException");
    }
}
catch (PosException ex)
{
    Console.WriteLine($"POS Error: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("=== Demos Complete ===");
