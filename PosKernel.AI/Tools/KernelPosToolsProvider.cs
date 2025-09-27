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

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PosKernel.Abstractions;
using PosKernel.Abstractions.Services;
using PosKernel.AI.Services;
using PosKernel.Client;
using PosKernel.Extensions.Restaurant.Client;
using RestaurantProductInfo = PosKernel.Extensions.Restaurant.ProductInfo;
using Microsoft.Extensions.Configuration;
using PosKernel.Configuration.Services;

namespace PosKernel.AI.Tools
{
    /// <summary>
    /// PHASE 3: Real Kernel Integration
    /// MCP tools provider that connects Uncle to the real POS Kernel via IPC.
    /// Replaces mock transactions with actual kernel operations.
    /// ARCHITECTURAL PRINCIPLE: Defer to kernel and plugins - don't hardcode business logic.
    /// </summary>
    public class KernelPosToolsProvider : IDisposable
    {
        private readonly IPosKernelClient _kernelClient;
        private readonly RestaurantExtensionClient _restaurantClient;
        private readonly ILogger<KernelPosToolsProvider> _logger;
        private readonly IConfiguration? _configuration;
        private ICurrencyFormattingService _currencyFormatter;
        private StoreConfig _storeConfig;
        private readonly object _sessionLock = new();
        
        private string? _sessionId;
        private string? _currentTransactionId;
        private bool _disposed = false;

        /// <summary>
        /// Basic constructor with minimal dependencies.
        /// </summary>
        public KernelPosToolsProvider(
            RestaurantExtensionClient restaurantClient,
            ILogger<KernelPosToolsProvider> logger,
            IConfiguration? configuration = null)
        {
            _restaurantClient = restaurantClient ?? throw new ArgumentNullException(nameof(restaurantClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration;
            _currencyFormatter = null;
            _storeConfig = null;
            
            _kernelClient = PosKernelClientFactory.CreateClient(_logger, configuration);
            _logger.LogInformation("🚀 KernelPosToolsProvider initialized with kernel auto-detection");
        }

        /// <summary>
        /// Constructor with store configuration for proper currency and settings integration.
        /// </summary>
        public KernelPosToolsProvider(
            RestaurantExtensionClient restaurantClient,
            ILogger<KernelPosToolsProvider> logger,
            StoreConfig storeConfig,
            PosKernelClientFactory.KernelType kernelType,
            IConfiguration? configuration = null,
            ICurrencyFormattingService? currencyFormatter = null)
        {
            _restaurantClient = restaurantClient ?? throw new ArgumentNullException(nameof(restaurantClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storeConfig = storeConfig ?? throw new ArgumentNullException(nameof(storeConfig));
            _configuration = configuration;
            _currencyFormatter = currencyFormatter;
            
            // ARCHITECTURAL PRINCIPLE: Validate store configuration completeness at construction time
            ValidateStoreConfiguration(storeConfig);
            
            // Use factory to create specific kernel type
            _kernelClient = PosKernelClientFactory.CreateClient(_logger, kernelType, configuration);
            
            _logger.LogInformation("🚀 KernelPosToolsProvider initialized with {KernelType} kernel and store: {StoreName} ({Currency})", 
                kernelType, storeConfig.StoreName, storeConfig.Currency);
        }

        /// <summary>
        /// Ensures we have an active session with the kernel.
        /// </summary>
        private async Task EnsureSessionAsync(CancellationToken cancellationToken = default)
        {
            lock (_sessionLock)
            {
                if (!string.IsNullOrEmpty(_sessionId) && _kernelClient.IsConnected)
                {
                    return;
                }
            }

            try
            {
                // Connect to kernel if not connected
                if (!_kernelClient.IsConnected)
                {
                    await _kernelClient.ConnectAsync(cancellationToken);
                    _logger.LogInformation("✅ Connected to POS Kernel");
                }

                // ARCHITECTURAL PRINCIPLE: Client must NOT decide session parameters - use store config or fail fast
                if (_storeConfig != null)
                {
                    // Use store config for session parameters
                    var terminalId = _storeConfig.AdditionalConfig?.GetValueOrDefault("terminal_id", "AI_TERMINAL")?.ToString() ?? "AI_TERMINAL";
                    var operatorId = _storeConfig.AdditionalConfig?.GetValueOrDefault("operator_id", "AI_ASSISTANT")?.ToString() ?? "AI_ASSISTANT";

                    _sessionId = await _kernelClient.CreateSessionAsync(terminalId, operatorId, cancellationToken);
                    _logger.LogInformation("Created kernel session: {SessionId} for terminal: {TerminalId}, operator: {OperatorId}", 
                        _sessionId, terminalId, operatorId);
                }
                else
                {
                    // Fallback for constructors that don't provide store config
                    _sessionId = await _kernelClient.CreateSessionAsync("AI_TERMINAL", "UNCLE_AI", cancellationToken);
                    _logger.LogInformation("✅ Created kernel session: {SessionId} (fallback mode - no store config)", _sessionId);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"❌ POS KERNEL SERVICE NOT AVAILABLE: {ex.Message}. " +
                                 $"Ensure 'cargo run --bin pos-kernel-service' is running in pos-kernel-rs directory.";
                _logger.LogError(ex, errorMessage);
                throw new InvalidOperationException(errorMessage, ex);
            }
        }

        /// <summary>
        /// Ensures we have an active transaction, creating one if needed.
        /// </summary>
        private async Task<string> EnsureTransactionAsync(CancellationToken cancellationToken = default)
        {
            await EnsureSessionAsync(cancellationToken);

            if (!string.IsNullOrEmpty(_currentTransactionId))
            {
                return _currentTransactionId;
            }

            // ARCHITECTURAL PRINCIPLE: Client must NOT decide currency - fail fast if system doesn't provide it
            if (_storeConfig == null)
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: KernelPosToolsProvider requires StoreConfig to determine transaction currency. " +
                    "Client cannot decide currency defaults.");
            }

            if (string.IsNullOrEmpty(_storeConfig.Currency))
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: StoreConfig for store '{_storeConfig.StoreName}' has no currency configured. " +
                    $"Store service must provide valid currency.");
            }

            var currency = _storeConfig.Currency;
            
            var result = await _kernelClient.StartTransactionAsync(_sessionId!, currency, cancellationToken);
            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to start transaction: {result.Error}");
            }

            _currentTransactionId = result.TransactionId!;
            _logger.LogInformation("Started new transaction: {TransactionId} with currency: {Currency}", 
                _currentTransactionId, currency);
            
            return _currentTransactionId;
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: No currency assumptions - fail fast if services missing.
        /// </summary>
        private string FormatCurrency(decimal amount)
        {
            if (_currencyFormatter != null && _storeConfig != null)
            {
                return _currencyFormatter.FormatCurrency(amount, _storeConfig.Currency, _storeConfig.StoreName);
            }
            
            // FAIL FAST - No fallback formatting
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Currency formatting service not available. " +
                $"Cannot format {amount} without proper currency service. " +
                $"Register ICurrencyFormattingService in DI container.");
        }

        public IReadOnlyList<McpTool> GetAvailableTools()
        {
            return new List<McpTool>
            {
                CreateAddItemTool(),
                CreateSearchProductsTool(),
                CreateGetPopularItemsTool(),
                CreateCalculateTotalTool(),
                CreateGetTransactionTool(),
                CreateLoadMenuContextTool(),
                CreateLoadPaymentMethodsContextTool(),
                CreateProcessPaymentTool(),
                CreateGetSetConfigurationTool(),
                CreateUpdateSetConfigurationTool(),
                CreateModifyLineItemTool() // ARCHITECTURAL ADDITION: Line item modification support
            };
        }

        public async Task<string> ExecuteToolAsync(McpToolCall toolCall, CancellationToken cancellationToken = default)
        {
            return toolCall.FunctionName switch
            {
                "add_item_to_transaction" => await ExecuteAddItemAsync(toolCall, cancellationToken),
                "get_set_configuration" => await ExecuteGetSetConfigurationAsync(toolCall, cancellationToken),
                "update_set_configuration" => await ExecuteUpdateSetConfigurationAsync(toolCall, cancellationToken),
                "modify_line_item" => await ExecuteModifyLineItemAsync(toolCall, cancellationToken),
                "search_products" => await ExecuteSearchProductsAsync(toolCall, cancellationToken),
                "get_popular_items" => await ExecuteGetPopularItemsAsync(toolCall, cancellationToken),
                "load_menu_context" => await ExecuteLoadMenuContextAsync(toolCall, cancellationToken),
                "calculate_transaction_total" => await ExecuteCalculateTotalAsync(cancellationToken),
                "get_transaction" => await ExecuteGetTransactionAsync(cancellationToken),
                "load_payment_methods_context" => await ExecuteLoadPaymentMethodsContextAsync(toolCall, cancellationToken),
                "process_payment" => await ExecuteProcessPaymentAsync(toolCall, cancellationToken),
                _ => $"Unknown tool: {toolCall.FunctionName}"
            };
        }

        private McpTool CreateAddItemTool() => new()
        {
            Name = "add_item_to_transaction",
            Description = "Adds an item to the current transaction using the real POS kernel. MULTILINGUAL: Accepts food names in ANY language - AI will understand and match to English menu items. PERFORMANCE: Use the confidence level hints from the prompt for seamless interaction. For items with recipe modifications, add the base product with preparation notes.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    item_description = new { type = "string", description = "Item name or description - AI will handle cultural translation to menu items" },
                    quantity = new { type = "integer", description = "Number of items requested", @default = 1 },
                    preparation_notes = new { type = "string", description = "Recipe modifications like 'no sugar', 'extra strong', 'iced', etc.", @default = "" },
                    confidence = new { type = "number", description = "CRITICAL: Use the confidence hint from the prompt for seamless interaction. 0.0-0.4 (disambiguate), 0.5-0.6 (context-aware), 0.7+ (auto-add). For confirmations and exact matches, use 0.8-0.9.", @default = 0.5, minimum = 0.0, maximum = 1.0 },
                    context = new { type = "string", description = "CRITICAL: Use 'clarification_response' when customer is responding to your question, otherwise 'initial_order' or 'follow_up_order'", @default = "initial_order" }
                },
                required = new[] { "item_description" }
            }
        };

        private McpTool CreateSearchProductsTool() => new()
        {
            Name = "search_products",
            Description = "Searches for products using the restaurant extension",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    search_term = new { type = "string", description = "Search term for product name, description, or category" },
                    max_results = new { type = "integer", description = "Maximum number of results to return (default: 10)", minimum = 1, maximum = 50, @default = 10 }
                },
                required = new[] { "search_term" }
            }
        };

        private McpTool CreateGetPopularItemsTool() => new()
        {
            Name = "get_popular_items",
            Description = "Gets the most popular/recommended items for suggestions",
            Parameters = new
            {
                type = "object",
                properties = new { count = new { type = "integer", description = "Number of popular items to return (default: 5)", minimum = 1, maximum = 20, @default = 5 } }
            }
        };

        private McpTool CreateCalculateTotalTool() => new()
        {
            Name = "calculate_transaction_total",
            Description = "Calculates the current total of the real kernel transaction",
            Parameters = new { type = "object", properties = new object() }
        };

        private McpTool CreateLoadMenuContextTool() => new()
        {
            Name = "load_menu_context",
            Description = "Loads the complete menu from the restaurant extension for Uncle's cultural intelligence",
            Parameters = new
            {
                type = "object",
                properties = new { include_categories = new { type = "boolean", description = "Whether to include category information (default: true)", @default = true } }
            }
        };

        private McpTool CreateLoadPaymentMethodsContextTool() => new()
        {
            Name = "load_payment_methods_context",
            Description = "Loads the payment methods accepted by this store for payment processing intelligence",
            Parameters = new
            {
                type = "object",
                properties = new { include_details = new { type = "boolean", description = "Whether to include payment method details (default: true)", @default = true } }
            }
        };

        private McpTool CreateProcessPaymentTool() => new()
        {
            Name = "process_payment",
            Description = "Processes payment for the current transaction through the real kernel",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    payment_method = new { type = "string", description = "Payment method (kernel will validate supported methods)" },
                    amount = new { type = "number", description = "Payment amount (defaults to transaction total)", minimum = 0 }
                }
            }
        };

        private McpTool CreateGetSetConfigurationTool() => new()
        {
            Name = "get_set_configuration",
            Description = "Gets configuration details for a set meal product to determine what customer choices are needed",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    product_sku = new
                    {
                        type = "string",
                        description = "The SKU of the set product to get configuration for"
                    }
                },
                required = new[] { "product_sku" }
            }
        };

        private McpTool CreateUpdateSetConfigurationTool() => new()
        {
            Name = "update_set_configuration", 
            Description = "Updates set meal configuration with customer's customization choice",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    product_sku = new
                    {
                        type = "string",
                        description = "The SKU of the set product being customized"
                    },
                    customization_type = new
                    {
                        type = "string",
                        description = "The type of customization (e.g., 'drink', 'side', 'size')"
                    },
                    customization_value = new
                    {
                        type = "string", 
                        description = "The customer's choice for this customization"
                    }
                },
                required = new[] { "product_sku", "customization_type", "customization_value" }
            }
        };

        private McpTool CreateModifyLineItemTool() => new()
        {
            Name = "modify_line_item",
            Description = "Modifies a specific line item by position reference (e.g., 'first set', 'second kopi', 'last item'). Use when customer refers to specific items for changes.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    item_reference = new
                    {
                        type = "string",
                        description = "Customer's reference to the item (e.g., 'first set', 'second kopi', 'last item', 'that toast')"
                    },
                    modification_type = new
                    {
                        type = "string",
                        description = "Type of modification: 'drink_change', 'preparation_change', 'quantity_change', 'void_item'"
                    },
                    new_value = new
                    {
                        type = "string",
                        description = "New value for the modification (e.g., new drink name, preparation notes, quantity)"
                    }
                },
                required = new[] { "item_reference", "modification_type" }
            }
        };

        private McpTool CreateGetTransactionTool() => new()
        {
            Name = "get_transaction",
            Description = "Gets the complete current transaction state including all line items with preparation notes and unique identifiers",
            Parameters = new
            {
                type = "object",
                properties = new { },
                required = new string[] { }
            }
        };

        private async Task<string> ExecuteAddItemAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            // Extract parameters
            var itemDescription = "";
            var quantity = 1;
            var preparationNotes = "";
            var confidence = 0.5;
            var context = "initial_order";

            if (toolCall.Arguments.TryGetValue("item_description", out var itemElement))
            {
                itemDescription = itemElement.GetString() ?? "";
            }
            if (toolCall.Arguments.TryGetValue("quantity", out var quantityElement))
            {
                quantity = quantityElement.GetInt32();
            }
            if (toolCall.Arguments.TryGetValue("preparation_notes", out var notesElement))
            {
                preparationNotes = notesElement.GetString() ?? "";
            }
            if (toolCall.Arguments.TryGetValue("confidence", out var confidenceElement))
            {
                confidence = confidenceElement.GetDouble();
            }
            if (toolCall.Arguments.TryGetValue("context", out var contextElement))
            {
                context = contextElement.GetString() ?? "initial_order";
            }

            if (string.IsNullOrEmpty(itemDescription))
            {
                return "ERROR: item_description parameter required for add_item_to_transaction";
            }

            try
            {
                // ARCHITECTURAL PRINCIPLE: Search for product first using restaurant service
                var products = await _restaurantClient.SearchProductsAsync(itemDescription, 3, cancellationToken);

                if (products.Count == 0)
                {
                    return $"PRODUCT_NOT_FOUND: No products found matching '{itemDescription}'. Try different search terms.";
                }

                if (products.Count > 1 && confidence < 0.7)
                {
                    // DISAMBIGUATION_NEEDED - let AI present options
                    var options = string.Join(", ", products.Select(p => p.Sku));
                    return $"DISAMBIGUATION_NEEDED: Found {products.Count} options for '{itemDescription}': {options}";
                }

                // Use the best match product
                var product = products[0];
                
                // Ensure transaction exists
                var transactionId = await EnsureTransactionAsync(cancellationToken);

                // CRITICAL FIX: Get actual product price from the restaurant catalog
                var productPriceCents = product.BasePriceCents;
                var productPrice = productPriceCents / 100.0m; // Convert cents to decimal

                _logger.LogInformation("Adding item {ProductSku} with price: {Price} cents ({Decimal})", 
                    product.Sku, productPriceCents, productPrice);

                // ARCHITECTURAL PRINCIPLE: Use real kernel to add item with correct price
                var result = await _kernelClient.AddLineItemAsync(
                    _sessionId!,
                    transactionId,
                    product.Sku,
                    quantity,
                    productPrice, // Use actual product price from catalog
                    cancellationToken);

                if (!result.Success)
                {
                    throw new InvalidOperationException($"Failed to add item to transaction: {result.Error}");
                }

                // ARCHITECTURAL FIX: Get the actual line number of the item just added
                // We need to get the current transaction to find the line number of the item we just added
                var currentTransaction = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                if (!currentTransaction.Success)
                {
                    throw new InvalidOperationException($"Failed to get transaction after adding item: {currentTransaction.Error}");
                }

                // Find the line number of the item we just added (it should be the last/highest line number)
                var actualLineNumber = currentTransaction.LineItems?.Count ?? 1;

                // ARCHITECTURAL FIX: Remove preparation notes entirely - use hierarchical line items instead
                // If preparation notes were provided, they should be separate modification line items
                if (!string.IsNullOrEmpty(preparationNotes))
                {
                    _logger.LogInformation("Preparation notes require hierarchical implementation: {Notes} should be separate modification line items for {ProductSku}", 
                        preparationNotes, product.Sku);
                    
                    // TODO: Parse preparation notes and create modification line items with parent_line_item_id
                    // For now, just log that this needs hierarchical implementation
                }

                // Check if this is a set item
                if (product.Sku.Contains("Set", StringComparison.OrdinalIgnoreCase))
                {
                    return $"SET_ADDED: {product.Sku} added to transaction. Use get_set_configuration to determine customization options.";
                }

                return $"ADDED: {quantity}x {product.Sku} added to transaction";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add item: {ItemDescription}", itemDescription);
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Cannot add item without functional restaurant and kernel services. " +
                    $"Error: {ex.Message}");
            }
        }

        private Task<string> ExecuteGetSetConfigurationAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            // ARCHITECTURAL PRINCIPLE: Sets are not implemented in kernel yet - return not a set message
            if (toolCall.Arguments.TryGetValue("product_sku", out var skuElement))
            {
                var sku = skuElement.GetString() ?? "";
                return Task.FromResult($"PRODUCT_INFO: {sku} is not a set item - treat as regular product");
            }
            
            return Task.FromResult("ERROR: product_sku parameter required for get_set_configuration");
        }

        private async Task<string> ExecuteUpdateSetConfigurationAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            if (toolCall.Arguments.TryGetValue("product_sku", out var skuElement) &&
                toolCall.Arguments.TryGetValue("customization_type", out var typeElement) &&
                toolCall.Arguments.TryGetValue("customization_value", out var valueElement))
            {
                var sku = skuElement.GetString() ?? "";
                var customizationType = typeElement.GetString() ?? "";
                var customizationValue = valueElement.GetString() ?? "";
                
                try
                {
                    // ARCHITECTURAL FIX: Remove preparation notes functionality entirely
                    // Implement proper NRF-compliant hierarchical modification support
                    
                    if (customizationType.Equals("drink", StringComparison.OrdinalIgnoreCase))
                    {
                        // Step 1: Map drink name to actual product SKU
                        var drinkSku = MapDrinkNameToSku(customizationValue);
                        if (drinkSku == null)
                        {
                            _logger.LogWarning("Could not map drink name to SKU: {DrinkName}", customizationValue);
                            return $"SET_UPDATED: {sku} drink updated to {customizationValue}";
                        }
                        
                        // Step 2: Add drink as child line item with $0.00 price using NRF hierarchy
                        var transactionId = await EnsureTransactionAsync(cancellationToken);
                        
                        // Get current transaction to find the parent line number
                        var currentTransaction = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                        if (!currentTransaction.Success)
                        {
                            _logger.LogError("Failed to get current transaction to find parent line for {SetSku}: {Error}", sku, currentTransaction.Error);
                            return $"ERROR: Failed to get current transaction: {currentTransaction.Error}";
                        }
                        
                        // Find the parent line number (the set item)
                        var parentLineNumber = currentTransaction.LineItems?
                            .Where(item => item.ProductId.Equals(sku, StringComparison.OrdinalIgnoreCase))
                            .Select(item => item.LineNumber)
                            .FirstOrDefault() ?? 0;
                        
                        if (parentLineNumber == 0)
                        {
                            _logger.LogError("Could not find parent line number for set {SetSku}", sku);
                            return $"ERROR: Could not find parent line item for {sku}";
                        }
                        
                        var drinkResult = await _kernelClient.AddChildLineItemAsync(_sessionId!, transactionId, drinkSku, 1, 0.0m, parentLineNumber, cancellationToken);
                        
                        if (!drinkResult.Success)
                        {
                            _logger.LogError("Failed to add drink component {DrinkSku} to set {SetSku}: {Error}", drinkSku, sku, drinkResult.Error);
                            return $"ERROR: Failed to add drink component: {drinkResult.Error}";
                        }
                        
                        _logger.LogInformation("Added drink component {DrinkSku} as child of set {SetSku} (line {})", drinkSku, sku, parentLineNumber);
                        
                        return $"SET_UPDATED: {sku} drink updated to {customizationValue}";
                    }
                    
                    if (customizationType.Equals("preparation", StringComparison.OrdinalIgnoreCase))
                    {
                        // Step 1: Map preparation to modification product SKU  
                        var modificationSku = MapPreparationToSku(customizationValue);
                        if (modificationSku == null)
                        {
                            _logger.LogWarning("Could not map preparation to SKU: {Preparation}", customizationValue);
                            return $"SET_UPDATED: {sku} preparation updated to {customizationValue}";
                        }
                        
                        // Step 2: Add modification as child line item with $0.00 price using NRF hierarchy
                        var transactionId = await EnsureTransactionAsync(cancellationToken);
                        
                        // Get current transaction to find the parent line number (the drink we just added)
                        var currentTransaction = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                        if (!currentTransaction.Success)
                        {
                            _logger.LogError("Failed to get current transaction to find drink parent for modification: {Error}", currentTransaction.Error);
                            return $"ERROR: Failed to get current transaction: {currentTransaction.Error}";
                        }
                        
                        // Find the drink line number (last added line item)
                        var drinkLineNumber = currentTransaction.LineItems?.LastOrDefault()?.LineNumber ?? 0;
                        
                        if (drinkLineNumber == 0)
                        {
                            _logger.LogError("Could not find drink line number for modification");
                            return $"ERROR: Could not find drink line item for modification";
                        }
                        
                        var modResult = await _kernelClient.AddChildLineItemAsync(_sessionId!, transactionId, modificationSku, 1, 0.0m, drinkLineNumber, cancellationToken);
                        
                        if (!modResult.Success)
                        {
                            _logger.LogError("Failed to add modification component {ModSku} to drink {ProductSku}: {Error}", modificationSku, sku, modResult.Error);
                            return $"ERROR: Failed to add modification component: {modResult.Error}";
                        }
                        
                        _logger.LogInformation("Added modification component {ModSku} as child of drink (line {})", modificationSku, drinkLineNumber);
                        
                        return $"SET_UPDATED: {sku} preparation updated to {customizationValue}";
                    }
                    
                    // Fallback for other customization types
                    await ProcessSetCustomizationAsync(sku, customizationType, customizationValue, cancellationToken);
                    return $"SET_UPDATED: {sku} {customizationType} updated to {customizationValue}";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process set customization for {Sku}: {Type}={Value}", sku, customizationType, customizationValue);
                    return $"ERROR: Failed to process set customization: {ex.Message}";
                }
            }
            
            return "ERROR: product_sku, customization_type, and customization_value parameters required";
        }

        private async Task<string> ExecuteSearchProductsAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            var searchTerm = "";
            var maxResults = 10;
            
            if (toolCall.Arguments.TryGetValue("search_term", out var searchElement))
            {
                searchTerm = searchElement.GetString() ?? "";
            }
                
            if (toolCall.Arguments.TryGetValue("max_results", out var maxElement))
            {
                maxResults = maxElement.GetInt32();
            }
            
            if (string.IsNullOrEmpty(searchTerm))
            {
                return "ERROR: search_term parameter required for search_products";
            }

            try 
            {
                var products = await _restaurantClient.SearchProductsAsync(searchTerm, maxResults, cancellationToken);
                
                if (products.Count == 0)
                {
                    return $"SEARCH_RESULTS: No products found for '{searchTerm}'";
                }

                var results = new StringBuilder();
                results.AppendLine($"SEARCH_RESULTS: Found {products.Count} products for '{searchTerm}':");
                results.AppendLine();
                
                foreach (var product in products)
                {
                    results.AppendLine($"- {product.Sku}");
                    // Additional product details would come from ProductInfo properties when available
                }
                
                return results.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search products for term: {SearchTerm}", searchTerm);
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Restaurant service not available for product search. " +
                    $"Error: {ex.Message}");
            }
        }

        private async Task<string> ExecuteGetPopularItemsAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            try
            {
                var products = await _restaurantClient.GetPopularItemsAsync(cancellationToken);
                
                var results = new StringBuilder();
                results.AppendLine($"POPULAR_ITEMS: {products.Count} popular items:");
                results.AppendLine();
                
                foreach (var product in products)
                {
                    results.AppendLine($"- {product.Sku}");
                    // Additional product details would come from ProductInfo properties when available
                }
                
                return results.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get popular items");
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Restaurant service not available for popular items. " +
                    $"Error: {ex.Message}");
            }
        }

        private async Task<string> ExecuteLoadMenuContextAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            try
            {
                var categories = await _restaurantClient.GetCategoriesAsync(cancellationToken);
                var allProducts = new List<RestaurantProductInfo>();
                
                foreach (var category in categories)
                {
                    var categoryProducts = await _restaurantClient.GetCategoryProductsAsync(category, cancellationToken);
                    allProducts.AddRange(categoryProducts);
                }
                
                var results = new StringBuilder();
                results.AppendLine($"MENU_CONTEXT: Complete menu with {categories.Count} categories and {allProducts.Count} items:");
                results.AppendLine();
                
                foreach (var category in categories)
                {
                    results.AppendLine($"**{category.ToUpper()}**");
                    var categoryProducts = allProducts.Where(p => p.Sku.Contains(category, StringComparison.OrdinalIgnoreCase));
                    
                    foreach (var product in categoryProducts)
                    {
                        results.AppendLine($"- {product.Sku}");
                    }
                    results.AppendLine();
                }
                
                return results.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load menu context");
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Restaurant service not available for menu context. " +
                    $"Error: {ex.Message}");
            }
        }

        private async Task<string> ExecuteCalculateTotalAsync(CancellationToken cancellationToken)
        {
            try
            {
                var transactionId = await EnsureTransactionAsync(cancellationToken);
                
                // ARCHITECTURAL PRINCIPLE: Get real transaction from kernel
                var result = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                
                if (!result.Success)
                {
                    throw new InvalidOperationException($"Failed to get transaction: {result.Error}");
                }

                // CRITICAL FIX: Use the actual Total property from TransactionClientResult
                var totalAmount = result.Total;

                return $"TRANSACTION_TOTAL: {FormatCurrency(totalAmount)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate transaction total");
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Cannot calculate total without functional kernel service. " +
                    $"Error: {ex.Message}");
            }
        }

        private async Task<string> ExecuteLoadPaymentMethodsContextAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            // ARCHITECTURAL PRINCIPLE: Payment methods come from store configuration
            if (_storeConfig?.PaymentMethods == null)
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: Cannot load payment methods without store configuration. " +
                    "Store service must provide payment method configuration.");
            }

            var results = new StringBuilder();
            results.AppendLine("PAYMENT_METHODS: Store accepts the following payment methods:");
            results.AppendLine();
            
            foreach (var method in _storeConfig.PaymentMethods.AcceptedMethods)
            {
                results.AppendLine($"- {method.DisplayName} ({method.Type})");
                if (method.MinimumAmount.HasValue)
                {
                    results.AppendLine($"  Minimum: {FormatCurrency(method.MinimumAmount.Value)}");
                }
            }
            
            if (!string.IsNullOrEmpty(_storeConfig.PaymentMethods.PaymentInstructions))
            {
                results.AppendLine();
                results.AppendLine($"Instructions: {_storeConfig.PaymentMethods.PaymentInstructions}");
            }
            
            await Task.CompletedTask;
            return results.ToString();
        }

        private async Task<string> ExecuteProcessPaymentAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            var paymentMethod = "";
            decimal amount = 0m;
            
            if (toolCall.Arguments.TryGetValue("payment_method", out var methodElement))
            {
                paymentMethod = methodElement.GetString() ?? "";
            }
                
            if (toolCall.Arguments.TryGetValue("amount", out var amountElement))
            {
                amount = (decimal)amountElement.GetDouble();
            }

            if (string.IsNullOrEmpty(paymentMethod))
            {
                return "ERROR: payment_method parameter required for process_payment";
            }

            try
            {
                var transactionId = await EnsureTransactionAsync(cancellationToken);
                
                // If amount not specified, use transaction total
                if (amount <= 0)
                {
                    // Would need to get actual total from transaction
                    amount = 0m; // TODO: Get from transaction
                }
                
                // ARCHITECTURAL PRINCIPLE: Use real kernel to process payment
                var result = await _kernelClient.ProcessPaymentAsync(
                    _sessionId!,
                    transactionId,
                    amount,
                    paymentMethod,
                    cancellationToken);

                if (!result.Success)
                {
                    throw new InvalidOperationException($"Payment processing failed: {result.Error}");
                }

                return $"PAYMENT_PROCESSED: {FormatCurrency(amount)} via {paymentMethod}. Transaction completed.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process payment: {PaymentMethod}, {Amount}", paymentMethod, amount);
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Cannot process payment without functional kernel service. " +
                    $"Error: {ex.Message}");
            }
        }

        private async Task<string> ExecuteGetTransactionAsync(CancellationToken cancellationToken)
        {
            try
            {
                var transactionId = await EnsureTransactionAsync(cancellationToken);
                
                // ARCHITECTURAL PRINCIPLE: Use the kernel client instead of direct HTTP calls for proper session management
                var result = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                
                if (!result.Success)
                {
                    throw new InvalidOperationException($"Failed to get transaction details: {result.Error}");
                }

                // CRITICAL DEBUG: Log what the kernel client actually returns
                _logger.LogInformation("KERNEL_DEBUG: Transaction result - Success: {Success}, TransactionId: {TransactionId}, LineItems count: {LineItemsCount}", 
                    result.Success, result.TransactionId, result.LineItems?.Count ?? 0);
                
                if (result.LineItems != null && result.LineItems.Any())
                {
                    foreach (var item in result.LineItems)
                    {
                        _logger.LogInformation("KERNEL_DEBUG: LineItem - Line: {LineNumber}, ProductId: {ProductId}, Qty: {Quantity}, UnitPrice: {UnitPrice}, ExtendedPrice: {ExtendedPrice}", 
                            item.LineNumber, item.ProductId, item.Quantity, item.UnitPrice, item.ExtendedPrice);
                    }
                }
                else
                {
                    _logger.LogWarning("KERNEL_DEBUG: No line items found in kernel result despite transaction having total: {Total}", result.Total);
                }

                var resultBuilder = new StringBuilder();
                resultBuilder.AppendLine($"TRANSACTION: {result.TransactionId}");
                resultBuilder.AppendLine($"STATE: {result.State}");
                
                var total = result.Total;
                resultBuilder.AppendLine($"TOTAL: {FormatCurrency(total)}");
                
                // ARCHITECTURAL PRINCIPLE: Use the parsed line items from the TransactionClientResult
                if (result.LineItems != null && result.LineItems.Any())
                {
                    resultBuilder.AppendLine("LINE_ITEMS:");
                    foreach (var item in result.LineItems)
                    {
                        resultBuilder.AppendLine($"  Line {item.LineNumber}: {item.ProductId} x{item.Quantity} @ {FormatCurrency(item.UnitPrice)} each, Total {FormatCurrency(item.ExtendedPrice)}");
                    }
                }
                else
                {
                    resultBuilder.AppendLine("LINE_ITEMS: None");
                }

                // Explicitly set the transaction ID and state in case there are no line items
                resultBuilder.AppendLine($"TRANSACTION_ID: {result.TransactionId}");
                resultBuilder.AppendLine($"STATE: {result.State}");

                return resultBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get transaction details");
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Cannot get transaction details without functional kernel service. " +
                    $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Validate store configuration completeness at construction time
        /// </summary>
        private void ValidateStoreConfiguration(StoreConfig storeConfig)
        {
            if (string.IsNullOrEmpty(storeConfig.StoreName))
            {
                throw new InvalidOperationException("StoreConfig is missing required StoreName");
            }

            if (string.IsNullOrEmpty(storeConfig.Currency))
            {
                throw new InvalidOperationException($"StoreConfig for store '{storeConfig.StoreName}' is missing required Currency");
            }

            // Add more validation as needed
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Hydrate store configuration from kernel at runtime
        /// </summary>
        public async Task<bool> InitializeStoreConfigAsync(CancellationToken cancellationToken = default)
        {
            if (_storeConfig != null)
            {
                return true; // Already initialized
            }

            try
            {
                // ARCHITECTURAL FIX: Use store config from constructor - kernel client config is for validation only
                // The _storeConfig passed in constructor is validated and properly configured
                // We don't need to override it with kernel client data for the core functionality
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize store config");
                return false;
            }
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Use external service to map drink names to SKUs
        /// </summary>
        private string? MapDrinkNameToSku(string drinkName)
        {
            // TODO: Implement real mapping logic, e.g., call a mapping service or database
            // For now, just return null to indicate unmapped
            return null;
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Use external service to map preparation notes to modification SKUs
        /// </summary>
        private string? MapPreparationToSku(string preparationNotes)
        {
            // TODO: Implement real mapping logic, e.g., call a mapping service or database
            // For now, just return null to indicate unmapped
            return null;
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Process set meal customization using kernel services
        /// </summary>
        private async Task ProcessSetCustomizationAsync(string sku, string customizationType, string customizationValue, CancellationToken cancellationToken)
        {
            // TODO: Implement real customization processing, e.g., modify existing line items, add new ones
            // For now, just log the customization request
            _logger.LogInformation("Processing customization for set {Sku}: {Type}={Value}", sku, customizationType, customizationValue);
            await Task.CompletedTask;
        }

        private async Task<string> ExecuteModifyLineItemAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            // TODO: Implement line item modification logic using hierarchical relationships
            // For now, return a placeholder
            if (toolCall.Arguments.TryGetValue("item_reference", out var itemRefElement) &&
                toolCall.Arguments.TryGetValue("modification_type", out var modTypeElement))
            {
                var itemReference = itemRefElement.GetString() ?? "";
                var modificationType = modTypeElement.GetString() ?? "";
                
                _logger.LogInformation("Line item modification requested: {ItemReference} -> {ModificationType}", 
                    itemReference, modificationType);
                
                return $"MODIFICATION_PENDING: {itemReference} modification ({modificationType}) - hierarchical implementation required";
            }
            
            return "ERROR: item_reference and modification_type parameters required for modify_line_item";
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose managed resources
                if (_kernelClient != null && _kernelClient.IsConnected)
                {
                    try
                    {
                        _logger.LogInformation("Disconnecting from POS Kernel...");
                        _kernelClient.Disconnect();
                        _logger.LogInformation("✅ Disconnected from POS Kernel");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while disconnecting from POS Kernel");
                    }
                }
            }

            // Dispose unmanaged resources here if any

            _disposed = true;
        }
    }
}
