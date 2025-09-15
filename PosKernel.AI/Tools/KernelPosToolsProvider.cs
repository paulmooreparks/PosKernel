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

namespace PosKernel.AI.Tools
{
    /// <summary>
    /// PHASE 3: Real Kernel Integration
    /// MCP tools provider that connects Uncle to the real POS Kernel via IPC.
    /// Replaces mock transactions with actual kernel operations.
    /// </summary>
    public class KernelPosToolsProvider : IDisposable
    {
        private readonly IPosKernelClient _kernelClient;
        private readonly RestaurantExtensionClient _restaurantClient;
        private readonly ILogger<KernelPosToolsProvider> _logger;
        private readonly ProductCustomizationService _customizationService;
        private readonly object _sessionLock = new();
        
        private string? _sessionId;
        private string? _currentTransactionId;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the KernelPosToolsProvider with real kernel integration.
        /// </summary>
        /// <param name="kernelClient">The POS kernel client for transaction operations.</param>
        /// <param name="restaurantClient">The restaurant extension client for product catalog operations.</param>
        /// <param name="logger">Logger for diagnostics and debugging.</param>
        /// <param name="customizationService">Service for parsing product customizations.</param>
        public KernelPosToolsProvider(
            IPosKernelClient kernelClient,
            RestaurantExtensionClient restaurantClient,
            ILogger<KernelPosToolsProvider> logger,
            ProductCustomizationService? customizationService = null)
        {
            _kernelClient = kernelClient ?? throw new ArgumentNullException(nameof(kernelClient));
            _restaurantClient = restaurantClient ?? throw new ArgumentNullException(nameof(restaurantClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _customizationService = customizationService ?? new ProductCustomizationService();
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
                    _logger.LogInformation("Connected to POS Kernel");
                }

                // Create session
                _sessionId = await _kernelClient.CreateSessionAsync("AI_TERMINAL", "UNCLE_AI", cancellationToken);
                _logger.LogInformation("Created kernel session: {SessionId}", _sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to establish kernel session");
                throw new InvalidOperationException("Unable to connect to POS Kernel. Ensure the kernel service is running.", ex);
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

            var result = await _kernelClient.StartTransactionAsync(_sessionId!, "SGD", cancellationToken);
            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to start transaction: {result.Error}");
            }

            _currentTransactionId = result.TransactionId!;
            _logger.LogInformation("Started new transaction: {TransactionId}", _currentTransactionId);
            
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
                Description = "Adds an item to the current transaction using the real POS kernel",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        item_description = new
                        {
                            type = "string",
                            description = "What the customer said they want (e.g., 'large coffee', 'kaya toast', 'teh si kosong')"
                        },
                        quantity = new
                        {
                            type = "integer",
                            description = "Number of items requested",
                            @default = 1
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
            var confidence = toolCall.Arguments.ContainsKey("confidence") ? toolCall.Arguments["confidence"].GetDouble() : 0.3;
            var context = toolCall.Arguments.ContainsKey("context") ? toolCall.Arguments["context"].GetString() : "initial_order";

            _logger.LogInformation("Adding item to kernel: '{Item}' x{Qty}, Confidence: {Confidence}, Context: {Context}", 
                itemDescription, quantity, confidence, context ?? "initial_order");

            if (string.IsNullOrWhiteSpace(itemDescription))
            {
                return "PRODUCT_NOT_FOUND: Please specify what you'd like to order";
            }

            try
            {
                // First, try cultural translation for kopitiam terms
                var translatedTerm = ApplyKopitiamCulturalTranslation(itemDescription);
                
                // Search for products using the restaurant extension
                var searchResults = await _restaurantClient.SearchProductsAsync(translatedTerm, 10, cancellationToken);

                if (!searchResults.Any())
                {
                    // Try broader search
                    var broadSearchResults = await GetBroaderSearchResults(translatedTerm, cancellationToken);
                    if (!broadSearchResults.Any())
                    {
                        return $"PRODUCT_NOT_FOUND: I don't recognize '{itemDescription}'. Could you try describing it differently?";
                    }
                    searchResults = broadSearchResults;
                }

                // Apply confidence-based disambiguation logic
                var exactMatches = searchResults.Where(p => IsExactMatch(translatedTerm, p)).ToList();
                var specificMatches = searchResults.Where(p => IsSpecificProductMatch(translatedTerm, p)).ToList();

                // FIXED DECISION LOGIC - Proper confidence handling  
                if (context == "clarification_response")
                {
                    // Customer is clarifying after disambiguation - use best match
                    var bestMatch = exactMatches.FirstOrDefault() ?? specificMatches.FirstOrDefault() ?? searchResults.First();
                    return await AddProductToTransactionAsync(bestMatch, quantity, cancellationToken);
                }
                else if (exactMatches.Count == 1 && searchResults.Count == 1)
                {
                    // Single exact match AND only one search result - always auto-add regardless of confidence
                    return await AddProductToTransactionAsync(exactMatches.First(), quantity, cancellationToken);
                }
                else if (exactMatches.Count == 1 && confidence >= 0.8)
                {
                    // Single exact match with HIGH confidence - auto-add even if other results exist
                    return await AddProductToTransactionAsync(exactMatches.First(), quantity, cancellationToken);
                }
                else if (confidence >= 0.9)
                {
                    // Very high confidence - pick best available match even with multiple options
                    var bestMatch = exactMatches.FirstOrDefault() ?? specificMatches.FirstOrDefault() ?? searchResults.First();
                    return await AddProductToTransactionAsync(bestMatch, quantity, cancellationToken);
                }
                else
                {
                    // Low/Medium confidence OR multiple valid options - require disambiguation
                    var disambiguationOptions = searchResults.Take(3).ToList(); // Limit to 3 options
                    var optionsList = string.Join(", ", disambiguationOptions.Select(p => $"{p.Name} (${p.BasePriceCents / 100.0:F2})"));
                    
                    return $"DISAMBIGUATION_NEEDED: Found {disambiguationOptions.Count} options for '{itemDescription}': {optionsList}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to kernel transaction: {Error}", ex.Message);
                return $"ERROR: Unable to process item '{itemDescription}': {ex.Message}";
            }
        }

        private async Task<string> AddProductToTransactionAsync(RestaurantProductInfo product, int quantity, CancellationToken cancellationToken)
        {
            var transactionId = await EnsureTransactionAsync(cancellationToken);
            var unitPrice = product.BasePriceCents / 100.0m; // Convert cents to dollars
            
            // Add item to the real kernel transaction
            var result = await _kernelClient.AddLineItemAsync(_sessionId!, transactionId, product.Sku, quantity, unitPrice, cancellationToken);
            
            if (!result.Success)
            {
                return $"ERROR: Failed to add {product.Name} to transaction: {result.Error}";
            }
            
            var totalPrice = unitPrice * quantity;
            return $"ADDED: {product.Name} x{quantity} @ ${unitPrice:F2} each = ${totalPrice:F2}";
        }

        private async Task<string> ExecuteCalculateTotalAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_currentTransactionId))
            {
                return "Transaction is empty. Total: $0.00";
            }

            var result = await _kernelClient.GetTransactionAsync(_sessionId!, _currentTransactionId, cancellationToken);
            
            if (!result.Success)
            {
                return $"ERROR: Unable to get transaction total: {result.Error}";
            }

            return $"Current transaction total: ${result.Total:F2} {(result.State == "Completed" ? "(PAID)" : "(PENDING)")}";
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
            verification.AppendLine($"TOTAL: ${result.Total:F2} SGD");
            verification.AppendLine($"Status: Ready for payment confirmation");
            
            return verification.ToString().Trim();
        }

        private async Task<string> ExecuteProcessPaymentAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_currentTransactionId))
            {
                return "Cannot process payment: Transaction is empty";
            }

            var paymentMethod = toolCall.Arguments.TryGetValue("payment_method", out var methodElement) 
                ? methodElement.GetString() : "cash";
            
            // Get current total first
            var transactionResult = await _kernelClient.GetTransactionAsync(_sessionId!, _currentTransactionId, cancellationToken);
            if (!transactionResult.Success)
            {
                return $"ERROR: Unable to get transaction total: {transactionResult.Error}";
            }

            var amount = toolCall.Arguments.TryGetValue("amount", out var amountElement) 
                ? amountElement.GetDecimal() : transactionResult.Total;

            // For now, only support cash payments in the demo
            if (paymentMethod?.ToLower() != "cash")
            {
                return $"Payment method '{paymentMethod}' not supported in demo. Please use 'cash'";
            }

            // Process payment through the kernel
            var paymentResult = await _kernelClient.ProcessPaymentAsync(_sessionId!, _currentTransactionId, amount, "cash", cancellationToken);
            
            if (!paymentResult.Success)
            {
                return $"PAYMENT_FAILED: {paymentResult.Error}";
            }

            var change = amount - transactionResult.Total;
            var result = $"Payment processed: ${amount:F2} cash\n" +
                        $"Total: ${transactionResult.Total:F2}\n" +
                        $"Change due: ${(change > 0 ? change : 0):F2}\n" +
                        $"Transaction status: {paymentResult.State}";

            // Clear current transaction since it's complete
            _currentTransactionId = null;

            return result;
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

            var results = products.Select(p => 
                $"• {p.Name} - ${p.BasePriceCents / 100.0:F2} ({p.CategoryName})").ToList();

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
                   $"Price: ${product.BasePriceCents / 100.0:F2}\n" +
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

            var results = popularItems.Select(p => 
                $"• {p.Name} - ${p.BasePriceCents / 100.0:F2}").ToList();

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

                var menuContext = new StringBuilder();
                menuContext.AppendLine("KOPITIAM MENU CONTEXT - Uncle's Complete Product Knowledge:");
                menuContext.AppendLine("=============================================================");

                menuContext.AppendLine($"\nFULL MENU ({allProducts.Count} items):");
                
                // Group by category for better organization
                var productsByCategory = allProducts.GroupBy(p => p.CategoryName).OrderBy(g => g.Key);
                
                foreach (var categoryGroup in productsByCategory)
                {
                    menuContext.AppendLine($"\n{categoryGroup.Key.ToUpper()}:");
                    foreach (var product in categoryGroup.OrderBy(p => p.Name))
                    {
                        menuContext.AppendLine($"  • {product.Name} - ${product.BasePriceCents / 100.0:F2} (SKU: {product.Sku})");
                        if (!string.IsNullOrEmpty(product.Description))
                        {
                            menuContext.AppendLine($"    {product.Description}");
                        }
                    }
                }

                menuContext.AppendLine("\nCULTURAL TRANSLATIONS Uncle Should Know:");
                menuContext.AppendLine("• roti kaya → Kaya Toast");
                menuContext.AppendLine("• teh si → Teh C (tea with evaporated milk)");
                menuContext.AppendLine("• kopi si → Kopi C (coffee with evaporated milk)");
                menuContext.AppendLine("• kosong → no sugar (for drinks), plain (for food)");
                menuContext.AppendLine("• siew dai → less sugar");
                menuContext.AppendLine("• gao → strong/thick");
                menuContext.AppendLine("• poh → weak/diluted");
                menuContext.AppendLine("• peng → iced");
                
                menuContext.AppendLine("\nUncle now has complete menu knowledge for intelligent order processing!");
                
                return menuContext.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading menu context from restaurant extension: {Error}", ex.Message);
                return $"ERROR: Unable to load menu context: {ex.Message}";
            }
        }

        // Helper methods (cultural translation, exact matching, etc.)
        private string ApplyKopitiamCulturalTranslation(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }
            
            var translated = input.ToLowerInvariant().Trim();
            
            // Core translations: "si" means evaporated milk (C)
            translated = translated.Replace("teh si", "teh c");
            translated = translated.Replace("kopi si", "kopi c"); 
            translated = translated.Replace("roti kaya", "kaya toast");
            translated = translated.Replace("roti", "toast");
            
            return translated;
        }

        private async Task<List<RestaurantProductInfo>> GetBroaderSearchResults(string searchTerm, CancellationToken cancellationToken)
        {
            var broaderTerms = new List<string>();
            
            if (searchTerm.Contains("kaya"))
            {
                broaderTerms.Add("kaya");
            }
            
            if (searchTerm.Contains("teh") || searchTerm.Contains("tea"))
            {
                broaderTerms.Add("teh");
                broaderTerms.Add("tea");
            }
            
            if (searchTerm.Contains("kopi") || searchTerm.Contains("coffee"))
            {
                broaderTerms.Add("kopi");
                broaderTerms.Add("coffee");
            }

            foreach (var term in broaderTerms)
            {
                var results = await _restaurantClient.SearchProductsAsync(term, 5, cancellationToken);
                if (results.Any())
                {
                    return results;
                }
            }
            
            return new List<RestaurantProductInfo>();
        }

        private bool IsExactMatch(string searchTerm, RestaurantProductInfo product)
        {
            var normalizedSearch = searchTerm.ToLowerInvariant().Trim();
            var normalizedProductName = product.Name.ToLowerInvariant().Trim();
            
            // Remove preparation instructions from SEARCH TERM only
            var baseSearchTerm = RemovePreparationInstructions(normalizedSearch);
            
            // Direct exact matches (SKU or full name match)
            if (normalizedSearch == normalizedProductName || normalizedSearch == product.Sku.ToLowerInvariant())
            {
                return true;
            }
            
            // BASE PRODUCT MATCH: "teh c kosong" → matches "teh c" 
            if (baseSearchTerm == normalizedProductName)
            {
                return true;
            }
            
            return false;
        }
        
        private string RemovePreparationInstructions(string term)
        {
            var baseTerm = term;
            
            // Remove preparation modifiers to get base product
            var modifiers = new[] { "kosong", "siew dai", "gao", "poh", "peng" };
            
            foreach (var modifier in modifiers)
            {
                baseTerm = baseTerm.Replace($" {modifier}", "").Replace($"{modifier} ", "");
            }
            
            return baseTerm.Trim();
        }

        private bool IsSpecificProductMatch(string searchTerm, RestaurantProductInfo product)
        {
            var normalizedSearch = searchTerm.ToLowerInvariant().Trim();
            var normalizedProductName = product.Name.ToLowerInvariant().Trim();
            
            // Exact matches - always specific
            if (normalizedSearch == normalizedProductName || normalizedSearch == product.Sku.ToLowerInvariant())
            {
                return true;
            }
            
            // Handle specific kopitiam terms
            var specificKopitiamTerms = new Dictionary<string, string[]>
            {
                { "kaya toast", new[] { "kaya toast" } },
                { "teh c kosong", new[] { "teh c kosong" } },
                { "teh kosong", new[] { "teh kosong" } },
                { "kopi c", new[] { "kopi c" } },
                { "kopi", new[] { "kopi" } },
                { "teh c", new[] { "teh c" } }
            };
            
            if (specificKopitiamTerms.TryGetValue(normalizedSearch, out var matches))
            {
                return matches.Any(match => normalizedProductName.Contains(match));
            }
            
            return false;
        }

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
                    payment_method = new { type = "string", description = "Payment method: cash, card, or mobile", @enum = new[] { "cash", "card", "mobile" }, @default = "cash" },
                    amount = new { type = "number", description = "Payment amount (defaults to transaction total)", minimum = 0 }
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
    }
}
