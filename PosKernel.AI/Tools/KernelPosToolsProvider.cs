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
        private readonly ProductCustomizationService _customizationService;
        private readonly IConfiguration? _configuration;
        private readonly StoreConfig? _storeConfig;
        private readonly ICurrencyFormattingService? _currencyFormatter;
        private readonly object _sessionLock = new();
        
        private string? _sessionId;
        private string? _currentTransactionId;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the KernelPosToolsProvider with real kernel integration.
        /// Uses the factory to auto-detect and connect to the best available kernel (Rust preferred).
        /// </summary>
        /// <param name="restaurantClient">The restaurant extension client for product catalog operations.</param>
        /// <param name="logger">Logger for diagnostics and debugging.</param>
        /// <param name="configuration">Application configuration for kernel selection.</param>
        /// <param name="customizationService">Service for parsing product customizations.</param>
        /// <param name="currencyFormatter">Currency formatting service for proper locale-specific formatting.</param>
        public KernelPosToolsProvider(
            RestaurantExtensionClient restaurantClient,
            ILogger<KernelPosToolsProvider> logger,
            IConfiguration? configuration = null,
            ProductCustomizationService? customizationService = null,
            ICurrencyFormattingService? currencyFormatter = null)
        {
            _restaurantClient = restaurantClient ?? throw new ArgumentNullException(nameof(restaurantClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration;
            _customizationService = customizationService ?? new ProductCustomizationService();
            _currencyFormatter = currencyFormatter; // Optional - falls back to generic formatting if not provided
            _storeConfig = null; // Will be injected later if available
            
            // Use factory to create the best available kernel client
            _kernelClient = PosKernelClientFactory.CreateClient(_logger, configuration);
            
            _logger.LogInformation("🚀 KernelPosToolsProvider initialized with kernel auto-detection");
        }

        /// <summary>
        /// Alternative constructor for when you want to specify the kernel type explicitly.
        /// </summary>
        /// <param name="restaurantClient">The restaurant extension client for product catalog operations.</param>
        /// <param name="logger">Logger for diagnostics and debugging.</param>
        /// <param name="kernelType">Specific kernel type to use.</param>
        /// <param name="configuration">Application configuration for kernel settings.</param>
        /// <param name="customizationService">Service for parsing product customizations.</param>
        /// <param name="currencyFormatter">Currency formatting service for proper locale-specific formatting.</param>
        public KernelPosToolsProvider(
            RestaurantExtensionClient restaurantClient,
            ILogger<KernelPosToolsProvider> logger,
            PosKernelClientFactory.KernelType kernelType,
            IConfiguration? configuration = null,
            ProductCustomizationService? customizationService = null,
            ICurrencyFormattingService? currencyFormatter = null)
        {
            _restaurantClient = restaurantClient ?? throw new ArgumentNullException(nameof(restaurantClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration;
            _customizationService = customizationService ?? new ProductCustomizationService();
            _currencyFormatter = currencyFormatter; // Optional - falls back to generic formatting if not provided
            _storeConfig = null; // Will be injected later if available
            
            // Use factory to create specific kernel type
            _kernelClient = PosKernelClientFactory.CreateClient(_logger, kernelType, configuration);
            
            _logger.LogInformation("🚀 KernelPosToolsProvider initialized with {KernelType} kernel", kernelType);
        }

        /// <summary>
        /// Constructor with store configuration for proper currency and settings integration.
        /// </summary>
        /// <param name="restaurantClient">The restaurant extension client for product catalog operations.</param>
        /// <param name="logger">Logger for diagnostics and debugging.</param>
        /// <param name="storeConfig">Store configuration for currency and settings.</param>
        /// <param name="kernelType">Specific kernel type to use.</param>
        /// <param name="configuration">Application configuration for kernel settings.</param>
        /// <param name="customizationService">Service for parsing product customizations.</param>
        /// <param name="currencyFormatter">Currency formatting service for proper locale-specific formatting.</param>
        public KernelPosToolsProvider(
            RestaurantExtensionClient restaurantClient,
            ILogger<KernelPosToolsProvider> logger,
            StoreConfig storeConfig,
            PosKernelClientFactory.KernelType kernelType,
            IConfiguration? configuration = null,
            ProductCustomizationService? customizationService = null,
            ICurrencyFormattingService? currencyFormatter = null)
        {
            _restaurantClient = restaurantClient ?? throw new ArgumentNullException(nameof(restaurantClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storeConfig = storeConfig ?? throw new ArgumentNullException(nameof(storeConfig));
            _configuration = configuration;
            _customizationService = customizationService ?? new ProductCustomizationService();
            _currencyFormatter = currencyFormatter; // Optional - falls back to generic formatting if not provided
            
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
                throw new InvalidOperationException("DESIGN DEFICIENCY: KernelPosToolsProvider requires StoreConfig to determine transaction currency. Client cannot decide currency defaults.");
            }

            if (string.IsNullOrEmpty(_storeConfig.Currency))
            {
                throw new InvalidOperationException($"DESIGN DEFICIENCY: StoreConfig for store '{_storeConfig.StoreName}' has no currency configured. Store service must provide valid currency.");
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
        /// Gets all available MCP tools for POS operations.
        /// </summary>
        public IReadOnlyList<McpTool> GetAvailableTools()
        {
            return new List<McpTool>
            {
                CreateAddItemTool(),
                CreateSearchProductsTool(),
                CreateGetProductInfoTool(),
                CreateGetPopularItemsTool(),
                CreateCalculateTotalTool(),
                CreateVerifyOrderTool(),
                CreateProcessPaymentTool(),
                CreateLoadMenuContextTool()
            };
        }

        /// <summary>
        /// Executes a tool call and returns the result.
        /// </summary>
        public async Task<string> ExecuteToolAsync(McpToolCall toolCall, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Executing kernel tool: {FunctionName} with args: {Arguments}", 
                    toolCall.FunctionName, JsonSerializer.Serialize(toolCall.Arguments));

                return toolCall.FunctionName switch
                {
                    "add_item_to_transaction" => await ExecuteAddItemAsync(toolCall, cancellationToken),
                    "search_products" => await ExecuteSearchProductsAsync(toolCall, cancellationToken),
                    "get_product_info" => await ExecuteGetProductInfoAsync(toolCall, cancellationToken),
                    "get_popular_items" => await ExecuteGetPopularItemsAsync(toolCall, cancellationToken),
                    "calculate_transaction_total" => await ExecuteCalculateTotalAsync(cancellationToken),
                    "verify_order" => await ExecuteVerifyOrderAsync(cancellationToken),
                    "process_payment" => await ExecuteProcessPaymentAsync(toolCall, cancellationToken),
                    "load_menu_context" => await ExecuteLoadMenuContextAsync(toolCall, cancellationToken),
                    _ => $"Unknown tool: {toolCall.FunctionName}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing kernel tool {FunctionName}", toolCall.FunctionName);
                return $"Tool execution error: {ex.Message}";
            }
        }

        private McpTool CreateAddItemTool()
        {
            return new McpTool
            {
                Name = "add_item_to_transaction",
                Description = "Adds an item to the current transaction using the real POS kernel. For items with recipe modifications (like 'kopi si kosong'), add the base product with preparation notes.",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        item_description = new
                        {
                            type = "string",
                            description = "Base product name (e.g., 'Kopi C' not 'Kopi C Kosong')"
                        },
                        quantity = new
                        {
                            type = "integer",
                            description = "Number of items requested",
                            @default = 1
                        },
                        preparation_notes = new
                        {
                            type = "string",
                            description = "Recipe modifications like 'no sugar', 'extra strong', 'iced', etc.",
                            @default = ""
                        },
                        confidence = new
                        {
                            type = "number",
                            description = "Confidence level: 0.0-0.4 (disambiguate), 0.5-0.7 (context-aware), 0.8-1.0 (auto-add)",
                            @default = 0.3,
                            minimum = 0.0,
                            maximum = 1.0
                        },
                        context = new
                        {
                            type = "string",
                            description = "Context: 'initial_order', 'clarification_response', 'follow_up_order'",
                            @default = "initial_order"
                        }
                    },
                    required = new[] { "item_description" }
                }
            };
        }

        private async Task<string> ExecuteAddItemAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            var itemDescription = toolCall.Arguments["item_description"].GetString() ?? "";
            var quantity = toolCall.Arguments.ContainsKey("quantity") ? toolCall.Arguments["quantity"].GetInt32() : 1;
            var preparationNotes = toolCall.Arguments.ContainsKey("preparation_notes") ? toolCall.Arguments["preparation_notes"].GetString() ?? "" : "";
            var confidence = toolCall.Arguments.ContainsKey("confidence") ? toolCall.Arguments["confidence"].GetDouble() : 0.3;
            var context = toolCall.Arguments.ContainsKey("context") ? toolCall.Arguments["context"].GetString() : "initial_order";

            _logger.LogInformation("Adding item to kernel: '{Item}' x{Qty} (prep: '{Prep}'), Confidence: {Confidence}, Context: {Context}", 
                itemDescription, quantity, preparationNotes, confidence, context ?? "initial_order");

            if (string.IsNullOrWhiteSpace(itemDescription))
            {
                return "PRODUCT_NOT_FOUND: Please specify what you'd like to order";
            }

            try
            {
                // ARCHITECTURAL FIX: Defer to restaurant extension for search and localization
                // Don't apply our own cultural translation - let the restaurant extension handle it
                var searchTerm = itemDescription; // Use raw input - let restaurant extension handle localization
                
                _logger.LogDebug("Searching restaurant extension for: '{SearchTerm}'", searchTerm);

                // Let restaurant extension handle cultural translations and product matching
                var searchResults = await _restaurantClient.SearchProductsAsync(searchTerm, 10, cancellationToken);

                if (!searchResults.Any())
                {
                    _logger.LogDebug("No exact match found, trying broader search through restaurant extension");
                    // Let restaurant extension handle broader search logic
                    var broadSearchResults = await GetBroaderSearchResults(searchTerm, cancellationToken);
                    if (!broadSearchResults.Any())
                    {
                        return $"PRODUCT_NOT_FOUND: No products found matching '{itemDescription}'. The restaurant extension found no matches.";
                    }
                    searchResults = broadSearchResults;
                }

                // ARCHITECTURAL FIX: Simplify matching logic - defer complex matching to restaurant extension
                // The restaurant extension should return products in relevance order
                var bestMatch = searchResults.First(); // Trust restaurant extension ordering

                // For high confidence or clarification responses, use the best match
                if (context == "clarification_response" || confidence >= 0.8 || searchResults.Count == 1)
                {
                    return await AddProductToTransactionAsync(bestMatch, quantity, preparationNotes, cancellationToken);
                }
                else
                {
                    // For low confidence, provide options but limit to top 3 from restaurant extension
                    var options = searchResults.Take(3).ToList();
                    
                    // Use proper currency formatting service
                    var optionsList = string.Join(", ", options.Select(p => $"{p.Name} ({FormatCurrency(p.BasePriceCents / 100.0m)})"));
                    
                    return $"DISAMBIGUATION_NEEDED: Found {options.Count} options for '{itemDescription}': {optionsList}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to kernel transaction: {Error}", ex.Message);
                return $"ERROR: Unable to process item '{itemDescription}': {ex.Message}";
            }
        }

        private async Task<string> AddProductToTransactionAsync(RestaurantProductInfo product, int quantity, string preparationNotes, CancellationToken cancellationToken)
        {
            var transactionId = await EnsureTransactionAsync(cancellationToken);
            var unitPrice = product.BasePriceCents / 100.0m;
            
            var result = await _kernelClient.AddLineItemAsync(_sessionId!, transactionId, product.Sku, quantity, unitPrice, cancellationToken);
            
            if (!result.Success)
            {
                return $"ERROR: Failed to add {product.Name} to transaction: {result.Error}";
            }
            
            var totalPrice = unitPrice * quantity;
            var prepNote = !string.IsNullOrEmpty(preparationNotes) ? $" (prep: {preparationNotes})" : "";
            
            // Use proper currency formatting service
            return $"ADDED: {product.Name}{prepNote} x{quantity} @ {FormatCurrency(unitPrice)} each = {FormatCurrency(totalPrice)}";
        }

        private async Task<string> ExecuteCalculateTotalAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_currentTransactionId))
            {
                // Use proper currency formatting service
                return $"Transaction is empty. Total: {FormatCurrency(0)}";
            }

            var result = await _kernelClient.GetTransactionAsync(_sessionId!, _currentTransactionId, cancellationToken);
            
            if (!result.Success)
            {
                return $"ERROR: Unable to get transaction total: {result.Error}";
            }

            // Use proper currency formatting service
            return $"Current transaction total: {FormatCurrency(result.Total)} {(result.State == "Completed" ? "(PAID)" : "(PENDING)")}";
        }

        private async Task<string> ExecuteVerifyOrderAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_currentTransactionId))
            {
                return "ORDER_VERIFICATION: Transaction is empty - nothing to verify.";
            }

            var result = await _kernelClient.GetTransactionAsync(_sessionId!, _currentTransactionId, cancellationToken);
            
            if (!result.Success)
            {
                return $"ERROR: Unable to verify order: {result.Error}";
            }

            var verification = new StringBuilder();
            verification.AppendLine("ORDER_VERIFICATION:");
            verification.AppendLine($"Transaction ID: {_currentTransactionId}");
            verification.AppendLine($"Session ID: {_sessionId}");
            verification.AppendLine($"State: {result.State}");
            
            // Use proper currency formatting service
            verification.AppendLine($"TOTAL: {FormatCurrency(result.Total)}");
            verification.AppendLine($"Status: Ready for payment confirmation");
            
            return verification.ToString().Trim();
        }

        private async Task<string> ExecuteProcessPaymentAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_currentTransactionId))
            {
                return "Cannot process payment: Transaction is empty";
            }

            // Get payment method from tool call arguments - let kernel validate supported methods
            var paymentMethod = toolCall.Arguments.TryGetValue("payment_method", out var methodElement) 
                ? methodElement.GetString() : null;
                
            if (string.IsNullOrEmpty(paymentMethod))
            {
                paymentMethod = toolCall.Arguments.TryGetValue("method", out var altMethodElement) 
                    ? altMethodElement.GetString() : "cash";
            }
            
            // Get current total first
            var transactionResult = await _kernelClient.GetTransactionAsync(_sessionId!, _currentTransactionId, cancellationToken);
            if (!transactionResult.Success)
            {
                return $"ERROR: Unable to get transaction total: {transactionResult.Error}";
            }

            var amount = toolCall.Arguments.TryGetValue("amount", out var amountElement) 
                ? amountElement.GetDecimal() : transactionResult.Total;

            // Let kernel handle payment method validation and processing - no client-side assumptions
            var method = paymentMethod?.ToLower()?.Trim() ?? "cash";
            
            try
            {
                // Attempt to process payment through kernel - let kernel determine if method is supported
                var paymentResult = await _kernelClient.ProcessPaymentAsync(_sessionId!, _currentTransactionId, amount, method, cancellationToken);
                
                if (!paymentResult.Success)
                {
                    return $"PAYMENT_FAILED: {paymentResult.Error}";
                }

                // Let kernel determine the exact payment details and change calculation
                var change = amount - transactionResult.Total;
                
                // Build response based on actual payment method processed by kernel
                var methodDisplay = $"{method} payment processed";
                var changeAmount = Math.Max(0, change);
                
                // Use proper currency formatting service
                var result = $"Payment processed: {FormatCurrency(amount)} via {method}\n" +
                            $"Total: {FormatCurrency(transactionResult.Total)}\n" +
                            $"Change due: {FormatCurrency(changeAmount)}\n" +
                            $"Transaction status: {paymentResult.State}";

                // Clear current transaction since it's complete
                _currentTransactionId = null;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kernel rejected payment method '{PaymentMethod}': {Error}", method, ex.Message);
                return $"PAYMENT_FAILED: {ex.Message}";
            }
        }

        // Product search methods using restaurant extension
        private async Task<string> ExecuteSearchProductsAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            var searchTerm = toolCall.Arguments["search_term"].GetString()!;
            var maxResults = toolCall.Arguments.TryGetValue("max_results", out var maxElement) ? maxElement.GetInt32() : 10;

            var products = await _restaurantClient.SearchProductsAsync(searchTerm, maxResults, cancellationToken);
            
            if (!products.Any())
            {
                return $"No products found matching: {searchTerm}";
            }

            var results = products.Select(p => $"• {p.Name} - {FormatCurrency(p.BasePriceCents / 100.0m)} ({p.CategoryName})").ToList();

            return $"Found {products.Count} products:\n" + string.Join("\n", results);
        }

        private async Task<string> ExecuteGetProductInfoAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            var productId = toolCall.Arguments["product_identifier"].GetString()!;

            var products = await _restaurantClient.SearchProductsAsync(productId, 1, cancellationToken);
            if (!products.Any())
            {
                return $"Product not found: {productId}";
            }

            var product = products.First();
            
            return $"Product: {product.Name}\n" +
                   $"Price: {FormatCurrency(product.BasePriceCents / 100.0m)}\n" +
                   $"Category: {product.CategoryName}\n" +
                   $"Description: {product.Description}\n" +
                   $"SKU: {product.Sku}\n" +
                   $"Available: {(product.IsActive ? "Yes" : "No")}";
        }

        private async Task<string> ExecuteGetPopularItemsAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            var count = toolCall.Arguments.TryGetValue("count", out var countElement) ? countElement.GetInt32() : 5;

            var products = await _restaurantClient.GetPopularItemsAsync(cancellationToken);
            var popularItems = products.Take(count);

            if (!popularItems.Any())
            {
                return "No popular items available";
            }

            var results = popularItems.Select(p => $"• {p.Name} - {FormatCurrency(p.BasePriceCents / 100.0m)}").ToList();

            return $"Top {popularItems.Count()} popular items:\n" + string.Join("\n", results);
        }

        private async Task<string> ExecuteLoadMenuContextAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            var includeCategories = toolCall.Arguments.TryGetValue("include_categories", out var categoriesElement) 
                ? categoriesElement.GetBoolean() : true;

            try
            {
                // Load the complete menu using restaurant extension
                var allProducts = await _restaurantClient.SearchProductsAsync("", 100, cancellationToken);

                if (!allProducts.Any())
                {
                    return "ERROR: Restaurant extension database is empty or not properly initialized. Cannot load menu context.";
                }

                var menuContext = new StringBuilder();
                
                menuContext.AppendLine($"MENU CONTEXT - Restaurant Product Catalog:");
                menuContext.AppendLine("=".PadRight(60, '='));

                menuContext.AppendLine($"\nFULL MENU ({allProducts.Count} items):");
                
                // Group by category for better organization
                var productsByCategory = allProducts.GroupBy(p => p.CategoryName).OrderBy(g => g.Key);
                
                foreach (var categoryGroup in productsByCategory)
                {
                    menuContext.AppendLine($"\n{categoryGroup.Key.ToUpper()}:");
                    foreach (var product in categoryGroup.OrderBy(p => p.Name))
                    {
                        // Use proper currency formatting service
                        menuContext.AppendLine($"  • {product.Name} - {FormatCurrency(product.BasePriceCents / 100.0m)} (SKU: {product.Sku})");
                        if (!string.IsNullOrEmpty(product.Description))
                        {
                            menuContext.AppendLine($"    {product.Description}");
                        }
                    }
                }

                menuContext.AppendLine("\nMENU LOADED: Use natural language to match customer requests to these products.");
                menuContext.AppendLine("The restaurant extension will handle cultural translations and product matching.");
                
                return menuContext.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading menu context from restaurant extension: {Error}", ex.Message);
                return $"ERROR: Unable to load menu context: {ex.Message}. Please check that the Restaurant Extension service is running and the database is properly initialized.";
            }
        }

        // Helper methods (broader search, etc.)
        private async Task<List<RestaurantProductInfo>> GetBroaderSearchResults(string searchTerm, CancellationToken cancellationToken)
        {
            // ARCHITECTURAL FIX: Defer to restaurant extension's built-in broader search capability
            // Don't implement our own search logic - let the restaurant extension handle this
            
            _logger.LogDebug("Requesting broader search from restaurant extension for: '{SearchTerm}'", searchTerm);
            
            // The restaurant extension should handle broader search logic internally
            // We'll try a few simple variations but defer the real intelligence to the extension
            var variations = new[] { searchTerm.Split(' ').FirstOrDefault(), searchTerm.ToLowerInvariant() }
                .Where(v => !string.IsNullOrEmpty(v) && v != searchTerm)
                .Distinct()
                .ToList();

            foreach (var variation in variations)
            {
                if (!string.IsNullOrEmpty(variation))
                {
                    var results = await _restaurantClient.SearchProductsAsync(variation, 10, cancellationToken);
                    if (results.Any())
                    {
                        _logger.LogDebug("Broader search found {Count} results for variation: '{Variation}'", results.Count, variation);
                        return results;
                    }
                }
            }
            
            _logger.LogDebug("No broader search results found for: '{SearchTerm}'", searchTerm);
            return new List<RestaurantProductInfo>();
        }
        
        // ARCHITECTURAL CLEANUP: Removed ApplyCulturalTranslation method
        // Cultural translations are now handled by the restaurant extension's localization services

        // Tool creation methods for other tools
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

        private McpTool CreateGetProductInfoTool() => new()
        {
            Name = "get_product_info",
            Description = "Gets detailed information about a specific product",
            Parameters = new
            {
                type = "object",
                properties = new { product_identifier = new { type = "string", description = "Product SKU, name, or barcode" } },
                required = new[] { "product_identifier" }
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

        private McpTool CreateVerifyOrderTool() => new()
        {
            Name = "verify_order",
            Description = "Verifies and summarizes the current order for customer confirmation before payment",
            Parameters = new
            {
                type = "object",
                properties = new { include_translations = new { type = "boolean", description = "Whether to include cultural translations made (default: true)", @default = true } }
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
                    // ARCHITECTURAL FIX: Don't hardcode supported payment methods
                    // Let the AI and kernel determine what's supported dynamically
                    payment_method = new { 
                        type = "string", 
                        description = "Payment method (kernel will validate supported methods)" 
                    },
                    amount = new { 
                        type = "number", 
                        description = "Payment amount (defaults to transaction total)", 
                        minimum = 0 
                    }
                }
            }
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

        /// <summary>
        /// Cleans up kernel session and disposes resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(_sessionId) && _kernelClient.IsConnected)
                {
                    // Close session - this is a fire-and-forget cleanup
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _kernelClient.CloseSessionAsync(_sessionId);
                            await _kernelClient.DisconnectAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error during session cleanup");
                        }
                    });
                }

                _restaurantClient?.Dispose();
                _kernelClient?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing KernelPosToolsProvider");
            }

            _disposed = true;
        }

        /// <summary>
        /// Formats a currency amount using the store's currency formatting service.
        /// ARCHITECTURAL PRINCIPLE: No client-side currency assumptions - fail fast if service unavailable.
        /// </summary>
        /// <param name="amount">The amount to format.</param>
        /// <returns>Formatted currency string.</returns>
        /// <exception cref="InvalidOperationException">Thrown when currency formatting service is not available.</exception>
        private string FormatCurrency(decimal amount)
        {
            if (_currencyFormatter != null && _storeConfig != null)
            {
                return _currencyFormatter.FormatCurrency(amount, _storeConfig.Currency, _storeConfig.StoreName);
            }
            
            // ARCHITECTURAL PRINCIPLE: Fail fast - no fallback currency formatting assumptions
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Currency formatting service not available. " +
                $"Cannot format {amount} without proper currency service. " +
                $"Register ICurrencyFormattingService in DI container and ensure StoreConfig is provided.");
        }

        /// <summary>
        /// Validates that the store configuration is complete and valid for POS operations.
        /// Fails fast if critical configuration is missing.
        /// </summary>
        /// <param name="storeConfig">Store configuration to validate.</param>
        /// <exception cref="InvalidOperationException">Thrown when store configuration is invalid or incomplete.</exception>
        private void ValidateStoreConfiguration(StoreConfig storeConfig)
        {
            var errors = new List<string>();
            
            // Validate required fields
            if (string.IsNullOrWhiteSpace(storeConfig.StoreName))
            {
                errors.Add("StoreName is required");
            }
            
            if (string.IsNullOrWhiteSpace(storeConfig.Currency))
            {
                errors.Add("Currency is required");
            }
            else if (!IsValidCurrencyCode(storeConfig.Currency))
            {
                errors.Add($"Currency '{storeConfig.Currency}' is not a valid 3-letter ISO currency code");
            }
            
            if (string.IsNullOrWhiteSpace(storeConfig.CultureCode))
            {
                errors.Add("CultureCode is required");
            }
            else if (!IsValidCultureCode(storeConfig.CultureCode))
            {
                errors.Add($"CultureCode '{storeConfig.CultureCode}' is not a valid culture code (e.g., 'en-US', 'en-SG')");
            }
            
            // Validate enum values are defined
            if (!Enum.IsDefined(typeof(StoreType), storeConfig.StoreType))
            {
                errors.Add($"StoreType '{storeConfig.StoreType}' is not a valid store type");
            }
            
            if (!Enum.IsDefined(typeof(PersonalityType), storeConfig.PersonalityType))
            {
                errors.Add($"PersonalityType '{storeConfig.PersonalityType}' is not a valid personality type");
            }
            
            if (!Enum.IsDefined(typeof(CatalogProviderType), storeConfig.CatalogProvider))
            {
                errors.Add($"CatalogProvider '{storeConfig.CatalogProvider}' is not a valid catalog provider type");
            }
            
            // Validate store type and personality type compatibility
            if (!AreCompatible(storeConfig.StoreType, storeConfig.PersonalityType))
            {
                errors.Add($"StoreType '{storeConfig.StoreType}' is not compatible with PersonalityType '{storeConfig.PersonalityType}'");
            }
            
            if (errors.Any())
            {
                var errorMessage = $"DESIGN DEFICIENCY: Store configuration for '{storeConfig.StoreName ?? "Unknown"}' is invalid:\n" +
                                 string.Join("\n", errors.Select(e => $"  - {e}"));
                throw new InvalidOperationException(errorMessage);
            }
            
            _logger.LogInformation("✅ Store configuration validation passed for {StoreName}", storeConfig.StoreName);
        }
        
        /// <summary>
        /// Validates if a currency code is a valid 3-letter ISO currency code.
        /// </summary>
        private static bool IsValidCurrencyCode(string currency)
        {
            if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            {
                return false;
            }
                
            // Basic validation - could be enhanced with full ISO 4217 list
            var validCurrencies = new[]
            {
                "USD", "EUR", "GBP", "SGD", "AUD", "CAD", "JPY", "CNY", "INR", "KRW", 
                "THB", "MYR", "PHP", "VND", "IDR", "HKD", "NZD", "CHF", "SEK", "NOK",
                "DKK", "PLN", "CZK", "HUF", "RUB", "BRL", "MXN", "ARS", "CLP", "COP",
                "ZAR", "EGP", "AED", "SAR", "QAR", "KWD", "BHD", "OMR", "JOD", "LBP"
            };
            
            return validCurrencies.Contains(currency.ToUpperInvariant());
        }
        
        /// <summary>
        /// Validates if a culture code is valid.
        /// </summary>
        private static bool IsValidCultureCode(string cultureCode)
        {
            if (string.IsNullOrWhiteSpace(cultureCode))
            {
                return false;
            }
                
            try
            {
                // Try to create CultureInfo - this validates the format
                var culture = new System.Globalization.CultureInfo(cultureCode);
                return !culture.IsNeutralCulture; // We want specific cultures like "en-US", not neutral like "en"
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Validates if store type and personality type are compatible.
        /// </summary>
        private static bool AreCompatible(StoreType storeType, PersonalityType personalityType)
        {
            return (storeType, personalityType) switch
            {
                (StoreType.Kopitiam, PersonalityType.SingaporeanKopitiamUncle) => true,
                (StoreType.CoffeeShop, PersonalityType.AmericanBarista) => true,
                (StoreType.Boulangerie, PersonalityType.FrenchBoulanger) => true,
                (StoreType.ConvenienceStore, PersonalityType.JapaneseConbiniClerk) => true,
                (StoreType.ChaiStall, PersonalityType.IndianChaiWala) => true,
                (StoreType.GenericStore, PersonalityType.GenericCashier) => true,
                _ => false // Incompatible combination
            };
        }
    }
}
