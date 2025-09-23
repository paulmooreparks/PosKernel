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

using Microsoft.Extensions.Logging;
using PosKernel.Abstractions.Services;
using PosKernel.AI.Services;
using PosKernel.AI.Demo.UI.Terminal;
using PosKernel.Configuration.Services;

namespace PosKernel.AI.Demo.Services;

/// <summary>
/// Kopitiam-specific product catalog service that properly implements IProductCatalogService
/// ARCHITECTURAL PRINCIPLE: Mock service for demo purposes - business logic remains in interfaces
/// </summary>
public class KopitiamProductCatalogService : IProductCatalogService
{
    private readonly List<IProductInfo> _products = new()
    {
        new KopitiamProductInfo { Sku = "KOPI001", Name = "Kopi", Category = "Beverages", BasePrice = 1.20m },
        new KopitiamProductInfo { Sku = "KOPI002", Name = "Kopi C", Category = "Beverages", BasePrice = 1.40m },
        new KopitiamProductInfo { Sku = "KOPI003", Name = "Kopi O", Category = "Beverages", BasePrice = 1.00m },
        new KopitiamProductInfo { Sku = "TEH001", Name = "Teh", Category = "Beverages", BasePrice = 1.20m },
        new KopitiamProductInfo { Sku = "TEH002", Name = "Teh C", Category = "Beverages", BasePrice = 1.40m },
        new KopitiamProductInfo { Sku = "TEH003", Name = "Teh O", Category = "Beverages", BasePrice = 1.00m },
        new KopitiamProductInfo { Sku = "FOOD001", Name = "Kaya Toast", Category = "Food", BasePrice = 2.50m },
        new KopitiamProductInfo { Sku = "FOOD002", Name = "Half Boiled Eggs", Category = "Food", BasePrice = 1.80m },
        new KopitiamProductInfo { Sku = "FOOD003", Name = "Mee Goreng", Category = "Food", BasePrice = 4.50m }
    };

    public async Task<ProductValidationResult> ValidateProductAsync(string productId, ProductLookupContext context, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        var product = _products.FirstOrDefault(p =>
            p.Name.Equals(productId, StringComparison.OrdinalIgnoreCase) ||
            p.Sku.Equals(productId, StringComparison.OrdinalIgnoreCase));

        if (product != null)
        {
            return new ProductValidationResult
            {
                IsValid = true,
                Product = product,
                EffectivePrice = product.BasePrice
            };
        }

        return new ProductValidationResult
        {
            IsValid = false,
            ErrorMessage = $"Product '{productId}' not found"
        };
    }

    public async Task<IReadOnlyList<IProductInfo>> SearchProductsAsync(string searchTerm, int maxResults, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        return _products
            .Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                       p.Category.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        return _products.Select(p => p.Category).Distinct().ToList();
    }

    public async Task<IReadOnlyList<IProductInfo>> GetPopularItemsAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        return _products.Take(5).ToList();
    }

    public async Task<IReadOnlyList<IProductInfo>> GetProductsByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        return _products.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<string> GetLocalizedProductNameAsync(string productId, string localeCode, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        var product = _products.FirstOrDefault(p => p.Sku == productId);
        return product?.Name ?? productId;
    }

    public async Task<string> GetLocalizedProductDescriptionAsync(string productId, string localeCode, CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        var product = _products.FirstOrDefault(p => p.Sku == productId);
        return product?.Description ?? "";
    }
}

/// <summary>
/// Demo product info implementation for Kopitiam products
/// ARCHITECTURAL PRINCIPLE: Simple data structure - no business logic
/// </summary>
public class KopitiamProductInfo : IProductInfo
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public bool RequiresPreparation { get; set; } = false;
    public int PreparationTimeMinutes { get; set; } = 2;
    public string? NameLocalizationKey { get; set; }
    public string? DescriptionLocalizationKey { get; set; }
    public IReadOnlyDictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Custom logger provider that routes log messages to Terminal.Gui debug pane instead of console.
/// Prevents console output from corrupting the TUI display.
/// ARCHITECTURAL PRINCIPLE: UI utility class for proper console logging in Terminal.Gui
/// </summary>
internal class TerminalGuiLoggerProvider : ILoggerProvider
{
    private readonly TerminalUserInterface _terminalUi;

    public TerminalGuiLoggerProvider(TerminalUserInterface terminalUi)
    {
        _terminalUi = terminalUi ?? throw new ArgumentNullException(nameof(terminalUi));
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new TerminalGuiLogger(categoryName, _terminalUi);
    }

    public void Dispose() { }
}

/// <summary>
/// Custom logger that sends log messages to Terminal.Gui debug pane.
/// ARCHITECTURAL PRINCIPLE: UI utility class - no business logic, pure logging redirection
/// </summary>
internal class TerminalGuiLogger : ILogger
{
    private readonly string _categoryName;
    private readonly TerminalUserInterface _terminalUi;

    public TerminalGuiLogger(string categoryName, TerminalUserInterface terminalUi)
    {
        _categoryName = categoryName;
        _terminalUi = terminalUi;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= LogLevel.Information;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var logLevelText = logLevel switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => "unkn"
        };

        var categoryShort = _categoryName.Split('.').LastOrDefault() ?? _categoryName;
        var logEntry = $"{logLevelText}: {categoryShort}[{eventId.Id}] {message}";

        if (exception != null)
        {
            logEntry += $"\n      {exception}";
        }

        // Route to debug pane instead of console
        _terminalUi.LogDisplay?.AddLog(logEntry);
    }
}

/// <summary>
/// Simple implementation of IStoreConfigurationService for demo purposes.
/// In a real system, this would load store configuration from database or configuration files.
/// ARCHITECTURAL PRINCIPLE: Demo service - minimal implementation for UI testing
/// </summary>
internal class SimpleStoreConfigurationService : IStoreConfigurationService
{
    public StoreConfiguration GetStoreConfiguration(string storeId)
    {
        // For now, return a generic store configuration
        // In a real system, this would be looked up from a database or configuration
        return new StoreConfiguration
        {
            StoreId = storeId,
            Currency = "USD", // Default - will be overridden by StoreConfig
            Locale = "en-US"  // Default - will be overridden by StoreConfig  
        };
    }
}
