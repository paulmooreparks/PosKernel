using System;
using PosKernel.Host;

// Example 1: Simple transaction with error handling
Console.WriteLine("=== Basic Transaction Example ===");
try
{
    using var tx = Pos.CreateTransaction("Store-001", "USD");
    
    // Add some items
    tx.AddItem("COFFEE", 3.99m);
    tx.AddItem("MUFFIN", 2.49m);
    tx.AddItems("SUGAR_PACKET", 3, 0.10m);
    
    Console.WriteLine($"Transaction for {tx.Store} ({tx.Currency})");
    Console.WriteLine($"Items: {tx.LineCount}, Total: {tx.Totals.Total:C}");
    
    // Add payment
    tx.AddCashTender(10.00m);
    
    var totals = tx.Totals;
    Console.WriteLine($"Payment: {totals.Tendered:C}, Change: {totals.Change:C}");
    Console.WriteLine($"Status: {(totals.IsPaid ? "Paid" : "Balance Due")}");
}
catch (PosException ex)
{
    Console.WriteLine($"Transaction failed: {ex.Message}");
}

Console.WriteLine();

// Example 2: Multiple transactions with different currencies
Console.WriteLine("=== Multi-Currency Example ===");
var transactions = new[]
{
    ("US-Store", "USD", new[] { ("WIDGET", 1, 9.99m) }, 10.00m),
    ("EU-Store", "EUR", new[] { ("GADGET", 2, 7.50m) }, 20.00m),
    ("UK-Store", "GBP", new[] { ("ITEM", 1, 5.99m) }, 10.00m)
};

foreach (var (store, currency, items, payment) in transactions)
{
    using var tx = Pos.CreateTransaction(store, currency);
    
    foreach (var (sku, qty, price) in items)
    {
        tx.AddLine(sku, qty, price);
    }
    
    tx.AddCashTender(payment);
    
    var totals = tx.Totals;
    var culture = currency switch
    {
        "USD" => "en-US",
        "EUR" => "de-DE", 
        "GBP" => "en-GB",
        _ => "en-US"
    };
    
    Console.WriteLine($"{store}: Total={totals.Total.ToString("C", new System.Globalization.CultureInfo(culture))}, " +
                     $"Change={totals.Change.ToString("C", new System.Globalization.CultureInfo(culture))}");
}

Console.WriteLine();

// Example 3: Demonstrating error conditions
Console.WriteLine("=== Error Handling Examples ===");

// Invalid quantity
try 
{
    using var tx = Pos.CreateTransaction("Test-Store", "USD");
    tx.AddLine("ITEM", 0, 5.00m); // Invalid quantity
}
catch (PosException ex)
{
    Console.WriteLine($"✓ Caught expected error: {ex.Message}");
}

// Accessing disposed transaction
try
{
    Transaction tx;
    {
        using (tx = Pos.CreateTransaction("Test-Store", "USD"))
        {
            tx.AddItem("ITEM", 5.00m);
        } // Transaction disposed here
    }
    
    var store = tx.Store; // Should throw ObjectDisposedException
}
catch (ObjectDisposedException)
{
    Console.WriteLine("✓ Caught ObjectDisposedException for disposed transaction");
}

Console.WriteLine();

// Example 4: Transaction state monitoring
Console.WriteLine("=== Transaction State Example ===");
using (var tx = Pos.CreateTransaction("Demo-Store", "USD"))
{
    Console.WriteLine($"Initial state: {tx.Totals.State}");
    
    tx.AddItem("EXPENSIVE_ITEM", 100.00m);
    Console.WriteLine($"After adding item - Building: {tx.IsBuilding}, Completed: {tx.IsCompleted}");
    
    tx.AddCashTender(50.00m); // Partial payment
    var totals = tx.Totals;
    Console.WriteLine($"After partial payment - Balance: {totals.Balance:C}, State: {totals.State}");
    
    tx.AddCashTender(60.00m); // Complete payment  
    totals = tx.Totals;
    Console.WriteLine($"After full payment - Change: {totals.Change:C}, State: {totals.State}");
}

Console.WriteLine("\n=== All Examples Complete ===");
