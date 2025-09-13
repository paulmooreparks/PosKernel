using System;
using System.Runtime.InteropServices;
using PosKernel.Host;

Console.WriteLine("Loading Rust POS Kernel...");

// NEW: Initialize terminal with unique ID
var terminalId = Environment.GetEnvironmentVariable("POS_TERMINAL_ID") ?? "DEMO_TERMINAL";
Console.WriteLine($"Initializing Terminal: {terminalId}");

try 
{
    RustNative.InitializeTerminal(terminalId);
    Console.WriteLine("✓ Terminal initialized successfully");
} 
catch (Exception ex) 
{
    Console.WriteLine($"✗ Terminal initialization failed: {ex.Message}");
    Environment.Exit(1);
}

Console.WriteLine($"Kernel Version: {Pos.Version}");

// Display terminal information
try 
{
    var terminalInfo = RustNative.GetTerminalInfo();
    Console.WriteLine($"Terminal Info:\n{terminalInfo.Replace(":", ": ").Replace("\n", "\n  ")}");
} 
catch (Exception ex) 
{
    Console.WriteLine($"Warning: Could not get terminal info: {ex.Message}");
}

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

// Example 3: Real transactions with mixed currencies (now with terminal isolation)
Console.WriteLine("=== Multi-Process Isolated Transactions ===");
try 
{
    // Standard currency transaction
    using var usdTx = Pos.CreateTransaction($"Store-{terminalId}", "USD");
    usdTx.AddItem("COFFEE", 3.99m);
    usdTx.AddCashTender(5.00m);
    var usdTotals = usdTx.Totals;
    Console.WriteLine($"USD Transaction: Total={usdTotals.Total:C} Change={usdTotals.Change:C}");

    // Japanese Yen (no decimal places)
    using var jpyTx = Pos.CreateTransaction($"Store-Japan-{terminalId}", "JPY");
    jpyTx.AddItem("RAMEN", 800m); // ¥800
    jpyTx.AddCashTender(1000m);   // ¥1000
    var jpyTotals = jpyTx.Totals;
    Console.WriteLine($"JPY Transaction: Total=¥{jpyTotals.Total:F0} Change=¥{jpyTotals.Change:F0}");
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
Console.WriteLine("=== Multi-Process Architecture Benefits ===");
Console.WriteLine($"✓ Terminal '{terminalId}' runs in isolated process");
Console.WriteLine("✓ Process crash doesn't affect other terminals");
Console.WriteLine("✓ Each terminal has independent WAL (Write-Ahead Log)");
Console.WriteLine("✓ ACID-compliant transactions with legal compliance");
Console.WriteLine("✓ Automatic stale lock cleanup and recovery");
Console.WriteLine("✓ Cross-terminal coordination through shared file system");

Console.WriteLine();
Console.WriteLine("=== Architecture Features ===");
Console.WriteLine("✓ Zero recompilation needed for new currencies");
Console.WriteLine("✓ Fast path optimization for common currencies"); 
Console.WriteLine("✓ Safe defaults (2 decimal places) for custom currencies");
Console.WriteLine("✓ Proper handling of special cases (JPY with 0 decimals)");
Console.WriteLine("✓ Support for cryptocurrencies, loyalty points, custom currencies");
Console.WriteLine("✓ Strong validation prevents typos and invalid formats");

Console.WriteLine("\n=== Graceful Shutdown ===");
try 
{
    RustNative.ShutdownTerminal();
    Console.WriteLine("✓ Terminal shutdown complete");
} 
catch (Exception ex) 
{
    Console.WriteLine($"✗ Shutdown error: {ex.Message}");
}

Console.WriteLine("\n=== Demo Complete ===");
