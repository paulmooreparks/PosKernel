using System;
using System.Runtime.InteropServices;
using PosKernel.Host;

Console.WriteLine("Loading Rust POS Kernel...");
Console.WriteLine($"Kernel Version: {Pos.Version}");
Console.WriteLine();

// Example 1: Standard currencies (fast path)
Console.WriteLine("=== Standard Currencies (Fast Path) ===");
var standardCurrencies = new[] { "USD", "EUR", "JPY", "GBP", "CAD", "AUD" };

foreach (var currency in standardCurrencies)
{
    var info = Pos.GetCurrencyInfo(currency);
    Console.WriteLine($"{currency}: {info?.Type}, {info?.DecimalPlaces} decimal places");
}

Console.WriteLine();

// Example 2: Custom currencies (auto-registration)
Console.WriteLine("=== Custom Currencies (Auto-Registration) ===");
var customCurrencies = new[] { "BTC", "ETH", "PTS", "GLD", "SOL", "XYZ" };

foreach (var currency in customCurrencies)
{
    var info = Pos.GetCurrencyInfo(currency);
    if (info != null)
    {
        Console.WriteLine($"{currency}: {info.Type}, {info.DecimalPlaces} decimal places - ✓ Valid");
    }
    else
    {
        Console.WriteLine($"{currency}: Invalid");
    }
}

Console.WriteLine();

// Example 3: Real transactions with mixed currencies
Console.WriteLine("=== Mixed Currency Transactions ===");
try
{
    // Standard currency transaction
    using var usdTx = Pos.CreateTransaction("Store-USD", "USD");
    usdTx.AddItem("COFFEE", 3.99m);
    usdTx.AddCashTender(5.00m);
    var usdTotals = usdTx.Totals;
    Console.WriteLine($"USD Transaction: Total={usdTotals.Total:C} Change={usdTotals.Change:C}");

    // Japanese Yen (no decimal places)
    using var jpyTx = Pos.CreateTransaction("Store-Japan", "JPY");
    jpyTx.AddItem("RAMEN", 800m); // ¥800
    jpyTx.AddCashTender(1000m);   // ¥1000
    var jpyTotals = jpyTx.Totals;
    Console.WriteLine($"JPY Transaction: Total=¥{jpyTotals.Total:F0} Change=¥{jpyTotals.Change:F0}");

    // Bitcoin transaction (auto-registered)
    using var btcTx = Pos.CreateTransaction("Crypto-Store", "BTC");
    btcTx.AddItem("DIGITAL_ITEM", 0.0001m); // 0.0001 BTC
    btcTx.AddCashTender(0.0002m);           // 0.0002 BTC
    var btcTotals = btcTx.Totals;
    Console.WriteLine($"BTC Transaction: Total={btcTotals.Total:F4} BTC Change={btcTotals.Change:F4} BTC");

    // Loyalty points (auto-registered)
    using var ptsTx = Pos.CreateTransaction("Loyalty-Store", "PTS");
    ptsTx.AddItem("REWARD_ITEM", 100.00m); // 100 points
    ptsTx.AddCashTender(150.00m);          // 150 points
    var ptsTotals = ptsTx.Totals;
    Console.WriteLine($"PTS Transaction: Total={ptsTotals.Total:F0} pts Change={ptsTotals.Change:F0} pts");

    // New cryptocurrency that didn't exist when we compiled (auto-registers!)
    using var solTx = Pos.CreateTransaction("Future-Store", "SOL");
    solTx.AddItem("NFT", 5.25m);   // 5.25 SOL
    solTx.AddCashTender(10.00m);   // 10 SOL
    var solTotals = solTx.Totals;
    Console.WriteLine($"SOL Transaction: Total={solTotals.Total:F2} SOL Change={solTotals.Change:F2} SOL");
}
catch (PosException ex)
{
    Console.WriteLine($"POS Error: {ex.Message}");
}

Console.WriteLine();

// Example 4: Currency validation
Console.WriteLine("=== Currency Validation ===");
var testCodes = new[] { "USD", "BTC", "XYZ", "INVALID", "AB", "" };

foreach (var code in testCodes)
{
    bool isValid = Pos.IsValidCurrency(code);
    bool isStandard = Pos.IsStandardCurrency(code);
    
    string status = isValid ? "✓ Valid" : "✗ Invalid";
    string type = isStandard ? " (Standard)" : isValid ? " (Custom)" : "";
    
    Console.WriteLine($"{code,-8}: {status}{type}");
}

Console.WriteLine();
Console.WriteLine("=== Key Benefits Demonstrated ===");
Console.WriteLine("✓ Zero recompilation needed for new currencies");
Console.WriteLine("✓ Fast path optimization for common currencies"); 
Console.WriteLine("✓ Safe defaults (2 decimal places) for custom currencies");
Console.WriteLine("✓ Proper handling of special cases (JPY with 0 decimals)");
Console.WriteLine("✓ Support for cryptocurrencies, loyalty points, custom currencies");
Console.WriteLine("✓ Strong validation prevents typos and invalid formats");

Console.WriteLine("\n=== Demo Complete ===");
