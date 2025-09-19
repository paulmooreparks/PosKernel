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
using PosKernel.Extensions.Restaurant.Client;
using PosKernel.Extensions.Restaurant;
using PosKernel.AI.Services;

namespace PosKernel.AI.Tools
{
    /// <summary>
    /// POS-specific MCP tools for AI integration with the Point-of-Sale system.
    /// Provides structured function calling interface for transactions, inventory, and customer service.
    /// </summary>
    public class PosToolsProvider
    {
        private readonly IProductCatalogService? _productCatalog;
        private readonly RestaurantExtensionClient? _restaurantClient;
        private readonly ILogger<PosToolsProvider> _logger;
        private readonly StoreConfig? _storeConfig;

        /// <summary>
        /// Initializes a new instance of the PosToolsProvider with IProductCatalogService.
        /// </summary>
        /// <param name="productCatalog">The product catalog service for database access.</param>
        /// <param name="logger">Logger for diagnostics and debugging.</param>
        /// <param name="storeConfig">Store configuration for currency and locale information.</param>
        public PosToolsProvider(IProductCatalogService productCatalog, ILogger<PosToolsProvider> logger, StoreConfig? storeConfig = null)
        {
            _productCatalog = productCatalog ?? throw new ArgumentNullException(nameof(productCatalog));
            _restaurantClient = null;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storeConfig = storeConfig;
        }

        /// <summary>
        /// Initializes a new instance of the PosToolsProvider with RestaurantExtensionClient.
        /// </summary>
        /// <param name="restaurantClient">The restaurant extension client for product operations.</param>
        /// <param name="logger">Logger for diagnostics and debugging.</param>
        /// <param name="storeConfig">Store configuration for currency and locale information.</param>
        public PosToolsProvider(RestaurantExtensionClient restaurantClient, ILogger<PosToolsProvider> logger, StoreConfig? storeConfig = null)
        {
            _restaurantClient = restaurantClient ?? throw new ArgumentNullException(nameof(restaurantClient));
            _productCatalog = null;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storeConfig = storeConfig;
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
                CreateLoadMenuContextTool(),
                CreateLoadPaymentMethodsContextTool()
            };
        }

        /// <summary>
        /// Executes a tool call and returns the result.
        /// </summary>
        public async Task<string> ExecuteToolAsync(McpToolCall toolCall, Transaction transaction)
        {
            try
            {
                _logger.LogDebug("Executing tool: {FunctionName} with args: {Arguments}", 
                    toolCall.FunctionName, JsonSerializer.Serialize(toolCall.Arguments));

                return toolCall.FunctionName switch
                {
                    "add_item_to_transaction" => await ExecuteAddItemAsync(toolCall, transaction),
                    "search_products" => await ExecuteSearchProductsAsync(toolCall),
                    "get_product_info" => await ExecuteGetProductInfoAsync(toolCall),
                    "get_popular_items" => await ExecuteGetPopularItemsAsync(toolCall),
                    "calculate_transaction_total" => ExecuteCalculateTotal(transaction),
                    "verify_order" => ExecuteVerifyOrder(transaction),
                    "process_payment" => ExecuteProcessPayment(toolCall, transaction),
                    "load_menu_context" => await ExecuteLoadMenuContextAsync(toolCall),
                    "load_payment_methods_context" => await ExecuteLoadPaymentMethodsContextAsync(toolCall),
                    _ => $"Unknown tool: {toolCall.FunctionName}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing tool {FunctionName}", toolCall.FunctionName);
                return $"Tool execution error: {ex.Message}";
            }
        }

        private McpTool CreateAddItemTool()
        {
            return new McpTool
            {
                Name = "add_item_to_transaction",
                Description = "Adds an item to the current transaction with confidence-based disambiguation",
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

        private async Task<string> ExecuteAddItemAsync(McpToolCall toolCall, Transaction transaction)
        {
            var itemDescription = toolCall.Arguments["item_description"].GetString() ?? "";
            var quantity = toolCall.Arguments.ContainsKey("quantity") ? toolCall.Arguments["quantity"].GetInt32() : 1;
            var confidence = toolCall.Arguments.ContainsKey("confidence") ? toolCall.Arguments["confidence"].GetDouble() : 0.3;
            var context = toolCall.Arguments.ContainsKey("context") ? toolCall.Arguments["context"].GetString() : "initial_order";

            _logger.LogInformation("Adding item: '{Item}' x{Qty}, Confidence: {Confidence}, Context: {Context}", 
                itemDescription, quantity, confidence, context ?? "initial_order");

            if (string.IsNullOrWhiteSpace(itemDescription))
            {
                return "PRODUCT_NOT_FOUND: Please specify what you'd like to order";
            }

            try
            {
                // Search for products using available service
                var searchResults = await SearchProductsAsync(itemDescription, 10);

                if (!searchResults.Any())
                {
                    // Try broader search
                    var broadSearchResults = await GetBroaderSearchResults(itemDescription);
                    if (!broadSearchResults.Any())
                    {
                        return $"PRODUCT_NOT_FOUND: I don't recognize '{itemDescription}'. Could you try describing it differently?";
                    }
                    searchResults = broadSearchResults;
                }

                // Apply confidence-based disambiguation logic
                var exactMatches = searchResults.Where(p => IsExactMatch(itemDescription, p)).ToList();
                var specificMatches = searchResults.Where(p => IsSpecificProductMatch(itemDescription, p)).ToList();

                // FIXED DECISION LOGIC - Proper confidence handling  
                if (context == "clarification_response")
                {
                    // Customer is clarifying after disambiguation - use best match
                    var bestMatch = exactMatches.FirstOrDefault() ?? specificMatches.FirstOrDefault() ?? searchResults.First();
                    return AddProductToTransaction(bestMatch, quantity, transaction);
                }
                else if (exactMatches.Count == 1 && searchResults.Count == 1)
                {
                    // Single exact match AND only one search result - always auto-add regardless of confidence
                    return AddProductToTransaction(exactMatches.First(), quantity, transaction);
                }
                else if (exactMatches.Count == 1 && confidence >= 0.8)
                {
                    // Single exact match with HIGH confidence - auto-add even if other results exist
                    return AddProductToTransaction(exactMatches.First(), quantity, transaction);
                }
                else if (confidence >= 0.9)
                {
                    // Very high confidence - pick best available match even with multiple options
                    var bestMatch = exactMatches.FirstOrDefault() ?? specificMatches.FirstOrDefault() ?? searchResults.First();
                    return AddProductToTransaction(bestMatch, quantity, transaction);
                }
                else
                {
                    // Low/Medium confidence OR multiple valid options - require disambiguation
                    var disambiguationOptions = searchResults.Take(3).ToList(); // Limit to 3 options
                    var optionsList = string.Join(", ", disambiguationOptions.Select(p => $"{p.Name} ({FormatCurrency(GetProductPrice(p))})"));
                    
                    return $"DISAMBIGUATION_NEEDED: Found {disambiguationOptions.Count} options for '{itemDescription}': {optionsList}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to transaction: {Error}", ex.Message);
                return $"ERROR: Unable to process item '{itemDescription}': {ex.Message}";
            }
        }

        /// <summary>
        /// Search products using available service (either catalog or restaurant extension).
        /// </summary>
        private async Task<List<IProductInfo>> SearchProductsAsync(string searchTerm, int maxResults)
        {
            if (_productCatalog != null)
            {
                var results = await _productCatalog.SearchProductsAsync(searchTerm, maxResults);
                return results.ToList();
            }
            else if (_restaurantClient != null)
            {
                var results = await _restaurantClient.SearchProductsAsync(searchTerm, maxResults);
                // Convert RestaurantProductInfo to IProductInfo compatible format
                return results.Select(p => new ProductInfoAdapter(p)).Cast<IProductInfo>().ToList();
            }
            else
            {
                throw new InvalidOperationException("DESIGN DEFICIENCY: No product catalog service available");
            }
        }

        /// <summary>
        /// Adapter to convert RestaurantExtension ProductInfo to IProductInfo interface.
        /// </summary>
        private class ProductInfoAdapter : IProductInfo
        {
            private readonly PosKernel.Extensions.Restaurant.ProductInfo _restaurantProduct;

            public ProductInfoAdapter(PosKernel.Extensions.Restaurant.ProductInfo restaurantProduct)
            {
                _restaurantProduct = restaurantProduct;
            }

            public string Sku => _restaurantProduct.Sku;
            public string Name => _restaurantProduct.Name;
            public string Description => _restaurantProduct.Description;
            public string Category => _restaurantProduct.CategoryName; // Use CategoryName property
            public decimal BasePrice => _restaurantProduct.BasePriceCents / 100.0m;
            public long BasePriceCents => _restaurantProduct.BasePriceCents;
            public bool IsActive => _restaurantProduct.IsActive;
            public bool RequiresPreparation => false;
            public int PreparationTimeMinutes => 0;
            public string? NameLocalizationKey => null;
            public string? DescriptionLocalizationKey => null;
            public IReadOnlyDictionary<string, object> Attributes => new Dictionary<string, object>();
        }

        private McpTool CreateSearchProductsTool()
        {
            return new McpTool
            {
                Name = "search_products",
                Description = "Searches for products by name, description, or category",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        search_term = new
                        {
                            type = "string",
                            description = "Search term for product name, description, or category"
                        },
                        max_results = new
                        {
                            type = "integer",
                            description = "Maximum number of results to return (default: 10)",
                            minimum = 1,
                            maximum = 50,
                            @default = 10
                        }
                    },
                    required = new[] { "search_term" }
                }
            };
        }

        private McpTool CreateGetProductInfoTool()
        {
            return new McpTool
            {
                Name = "get_product_info",
                Description = "Gets detailed information about a specific product",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        product_identifier = new
                        {
                            type = "string",
                            description = "Product SKU, name, or barcode"
                        }
                    },
                    required = new[] { "product_identifier" }
                }
            };
        }

        private McpTool CreateGetPopularItemsTool()
        {
            return new McpTool
            {
                Name = "get_popular_items",
                Description = "Gets the most popular/recommended items for suggestions",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        count = new
                        {
                            type = "integer",
                            description = "Number of popular items to return (default: 5)",
                            minimum = 1,
                            maximum = 20,
                            @default = 5
                        }
                    }
                }
            };
        }

        private McpTool CreateCalculateTotalTool()
        {
            return new McpTool
            {
                Name = "calculate_transaction_total",
                Description = "Calculates the current total of the transaction including taxes",
                Parameters = new
                {
                    type = "object",
                    properties = new object()
                }
            };
        }

        private McpTool CreateVerifyOrderTool()
        {
            return new McpTool
            {
                Name = "verify_order",
                Description = "Verifies and summarizes the current order for customer confirmation before payment",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        include_translations = new
                        {
                            type = "boolean",
                            description = "Whether to include cultural translations made (default: true)",
                            @default = true
                        }
                    }
                }
            };
        }

        private McpTool CreateProcessPaymentTool()
        {
            return new McpTool
            {
                Name = "process_payment",
                Description = "Processes payment for the current transaction - should only be called after order verification",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        payment_method = new
                        {
                            type = "string",
                            description = "Payment method: cash, card, or mobile",
                            @enum = new[] { "cash", "card", "mobile" },
                            @default = "cash"
                        },
                        amount = new
                        {
                            type = "number",
                            description = "Payment amount (defaults to transaction total)",
                            minimum = 0
                        }
                    }
                }
            };
        }
        
        private McpTool CreateLoadMenuContextTool()
        {
            return new McpTool
            {
                Name = "load_menu_context",
                Description = "Loads the complete menu from the restaurant extension for Uncle's cultural intelligence",
                Parameters = new
                {
                    type = "object",
                    properties = new { include_categories = new { type = "boolean", description = "Whether to include category information (default: true)", @default = true } }
                }
            };
        }

        private McpTool CreateLoadPaymentMethodsContextTool()
        {
            return new McpTool
            {
                Name = "load_payment_methods_context", 
                Description = "Loads the accepted payment methods for this store - ARCHITECTURAL PRINCIPLE: Payment methods are store-specific configuration, not universal constants",
                Parameters = new
                {
                    type = "object",
                    properties = new { include_limits = new { type = "boolean", description = "Whether to include amount limits and restrictions (default: true)", @default = true } } 
                }
            };
        }

        private async Task<string> ExecuteSearchProductsAsync(McpToolCall toolCall)
        {
            var searchTerm = toolCall.Arguments["search_term"].GetString()!;
            var maxResults = toolCall.Arguments.TryGetValue("max_results", out var maxElement) ? maxElement.GetInt32() : 10;

            var products = await SearchProductsAsync(searchTerm, maxResults);
            
            if (!products.Any())
            {
                return $"No products found matching: {searchTerm}";
            }

            var results = products.Select(p => 
                $"• {p.Name} - {FormatCurrency(p.BasePrice)} ({p.Category})").ToList();

            return $"Found {products.Count} products:\n" + string.Join("\n", results);
        }

        private async Task<string> ExecuteGetProductInfoAsync(McpToolCall toolCall)
        {
            var productId = toolCall.Arguments["product_identifier"].GetString()!;

            var products = await SearchProductsAsync(productId, 1);
            if (!products.Any())
            {
                return $"Product not found: {productId}";
            }

            var product = products.First();
            return $"Product: {product.Name}\n" +
                   $"Price: {FormatCurrency(product.BasePrice)}\n" +
                   $"Category: {product.Category}\n" +
                   $"Description: {product.Description}\n" +
                   $"SKU: {product.Sku}\n" +
                   $"Available: {(product.IsActive ? "Yes" : "No")}";
        }

        private async Task<string> ExecuteGetPopularItemsAsync(McpToolCall toolCall)
        {
            var count = toolCall.Arguments.TryGetValue("count", out var countElement) ? countElement.GetInt32() : 5;

            List<IProductInfo> products;
            
            if (_productCatalog != null)
            {
                var catalogProducts = await _productCatalog.GetPopularItemsAsync();
                products = catalogProducts.ToList();
            }
            else if (_restaurantClient != null)
            {
                var restaurantProducts = await _restaurantClient.GetPopularItemsAsync();
                products = restaurantProducts.Select(p => new ProductInfoAdapter(p)).Cast<IProductInfo>().ToList();
            }
            else
            {
                return "No product service available";
            }
            
            var popularItems = products.Take(count);

            if (!popularItems.Any())
            {
                return "No popular items available";
            }

            var results = popularItems.Select(p => 
                $"• {p.Name} - {FormatCurrency(p.BasePrice)}").ToList();

            return $"Top {popularItems.Count()} popular items:\n" + string.Join("\n", results);
        }

        private string ExecuteCalculateTotal(Transaction transaction)
        {
            var itemCount = transaction.Lines.Count;
            var total = transaction.Total.ToDecimal();

            if (itemCount == 0)
            {
                return "Transaction is empty. Total: $0.00";
            }

            var itemsList = transaction.Lines.Select(line => 
                $"{line.ProductId} x{line.Quantity} = ${line.Extended.ToDecimal():F2}").ToList();

            return $"Transaction contains {itemCount} line items:\n" +
                   string.Join("\n", itemsList) +
                   $"\n\nTotal: ${total:F2}";
        }

        private string ExecuteVerifyOrder(Transaction transaction)
        {
            if (transaction.Lines.Count == 0)
            {
                return "ORDER_VERIFICATION: Transaction is empty - nothing to verify.";
            }

            var verification = new StringBuilder();
            verification.AppendLine("ORDER_VERIFICATION:");
            verification.AppendLine($"Transaction ID: {transaction.Id}");
            verification.AppendLine($"Currency: {transaction.Currency}");
            verification.AppendLine($"Items ({transaction.Lines.Count}):");

            foreach (var line in transaction.Lines)
            {
                verification.AppendLine($"  • {line.ProductId} x{line.Quantity} = ${line.Extended.ToDecimal():F2}");
            }

            verification.AppendLine($"TOTAL: ${transaction.Total.ToDecimal():F2}");
            verification.AppendLine($"Status: Ready for payment confirmation");
            
            return verification.ToString().Trim();
        }

        private string ExecuteProcessPayment(McpToolCall toolCall, Transaction transaction)
        {
            // CRITICAL FIX: Handle both "method" and "payment_method" parameter names
            var paymentMethod = toolCall.Arguments.TryGetValue("payment_method", out var methodElement) 
                ? methodElement.GetString() : null;
                
            if (string.IsNullOrEmpty(paymentMethod))
            {
                paymentMethod = toolCall.Arguments.TryGetValue("method", out var altMethodElement) 
                    ? altMethodElement.GetString() : "cash";
            }
            
            var amount = toolCall.Arguments.TryGetValue("amount", out var amountElement) 
                ? new Money((long)(amountElement.GetDecimal() * 100), transaction.Currency)
                : transaction.Total;

            if (transaction.Total.MinorUnits == 0)
            {
                return "Cannot process payment: Transaction is empty";
            }

            // CRITICAL FIX: Support both cash and card payments properly
            var method = paymentMethod?.ToLower() ?? "cash";
            
            if (method == "cash")
            {
                transaction.AddCashTender(amount);
                var change = transaction.ChangeDue.ToDecimal();
                
                return $"Payment processed: ${amount.ToDecimal():F2} cash received\n" +
                      $"Total: ${transaction.Total.ToDecimal():F2}\n" +
                      $"Change due: ${change:F2}\n" +
                      $"Transaction completed successfully.";
            }
            else if (method == "card" || method == "credit" || method == "debit")
            {
                // For card payments, assume exact amount (no change)
                transaction.AddCashTender(transaction.Total); // Mock as cash tender internally
                
                return $"Payment processed: ${transaction.Total.ToDecimal():F2} card payment\n" +
                      $"Total: ${transaction.Total.ToDecimal():F2}\n" +
                      $"Change due: $0.00\n" +
                      $"Transaction completed successfully.";
            }
            else
            {
                return $"Payment method '{paymentMethod}' not supported. Please use 'cash' or 'card'.";
            }
        }

        private async Task<string> ExecuteLoadMenuContextAsync(McpToolCall toolCall)
        {
            var includeCategories = toolCall.Arguments.TryGetValue("include_categories", out var categoriesElement) 
                ? categoriesElement.GetBoolean() : true;

            try
            {
                // Load the complete menu for Uncle's context
                var allProducts = await _productCatalog!.SearchProductsAsync("", maxResults: 100);
                var categories = includeCategories ? await _productCatalog!.GetCategoriesAsync() : new List<string>();

                var menuContext = new StringBuilder();
                menuContext.AppendLine("KOPITIAM MENU CONTEXT - Uncle's Complete Product Knowledge:");
                menuContext.AppendLine("=============================================================");
                
                if (categories.Any())
                {
                    menuContext.AppendLine("\nCATEGORIES:");
                    foreach (var category in categories)
                    {
                        menuContext.AppendLine($"  • {category}");
                    }
                }

                menuContext.AppendLine($"\nFULL MENU ({allProducts.Count} items):");
                
                // Group by category for better organization
                var productsByCategory = allProducts.GroupBy(p => p.Category).OrderBy(g => g.Key);
                
                foreach (var categoryGroup in productsByCategory)
                {
                    menuContext.AppendLine($"\n{categoryGroup.Key.ToUpper()}:");
                    foreach (var product in categoryGroup.OrderBy(p => p.Name))
                    {
                        var price = GetProductPrice(product);
                        menuContext.AppendLine($"  • {product.Name} - ${price:F2} (SKU: {product.Sku})");
                        if (!string.IsNullOrEmpty(product.Description))
                        {
                            menuContext.AppendLine($"    {product.Description}");
                        }
                    }
                }

                menuContext.AppendLine("\nUncle now has complete menu knowledge for intelligent order processing!");
                
                return menuContext.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading menu context: {Error}", ex.Message);
                return $"ERROR: Unable to load menu context: {ex.Message}";
            }
        }

        private async Task<string> ExecuteLoadPaymentMethodsContextAsync(McpToolCall toolCall)
        {
            var includeLimits = toolCall.Arguments.TryGetValue("include_limits", out var limitsElement) 
                ? limitsElement.GetBoolean() : true;

            try
            {
                // ARCHITECTURAL PRINCIPLE: Payment methods are loaded from store configuration, just like menu items
                if (_storeConfig == null)
                {
                    return "ERROR: Store configuration not available. Cannot load payment methods without store context.";
                }

                var paymentMethodsContext = new StringBuilder();
                
                paymentMethodsContext.AppendLine("PAYMENT METHODS CONTEXT - Store-Specific Payment Configuration:");
                paymentMethodsContext.AppendLine("=".PadRight(70, '='));
                paymentMethodsContext.AppendLine();
                paymentMethodsContext.AppendLine($"Store: {_storeConfig.StoreName} (ID: {_storeConfig.StoreId})");
                paymentMethodsContext.AppendLine($"Currency: {_storeConfig.Currency}");
                paymentMethodsContext.AppendLine();

                if (_storeConfig.PaymentMethods.AcceptedMethods.Any())
                {
                    paymentMethodsContext.AppendLine("ACCEPTED PAYMENT METHODS:");
                    
                    foreach (var method in _storeConfig.PaymentMethods.AcceptedMethods.Where(m => m.IsEnabled))
                    {
                        paymentMethodsContext.AppendLine($"  • {method.DisplayName} ({method.MethodId})");
                        paymentMethodsContext.AppendLine($"    Type: {method.Type}");
                        
                        if (includeLimits)
                        {
                            if (method.MinimumAmount.HasValue)
                            {
                                paymentMethodsContext.AppendLine($"    Minimum: {FormatCurrency(method.MinimumAmount.Value)}");
                            }
                            if (method.MaximumAmount.HasValue)
                            {
                                paymentMethodsContext.AppendLine($"    Maximum: {FormatCurrency(method.MaximumAmount.Value)}");
                            }
                        }
                        paymentMethodsContext.AppendLine();
                    }
                }
                else
                {
                    // ARCHITECTURAL PRINCIPLE: Fail fast if no payment methods configured
                    return "ERROR: No payment methods configured for this store. " +
                           "Store configuration must include accepted payment methods. " +
                           "Contact system administrator to configure payment methods.";
                }

                if (!string.IsNullOrEmpty(_storeConfig.PaymentMethods.PaymentInstructions))
                {
                    paymentMethodsContext.AppendLine("PAYMENT INSTRUCTIONS:");
                    paymentMethodsContext.AppendLine(_storeConfig.PaymentMethods.PaymentInstructions);
                    paymentMethodsContext.AppendLine();
                }

                if (!string.IsNullOrEmpty(_storeConfig.PaymentMethods.DefaultMethod))
                {
                    paymentMethodsContext.AppendLine($"SUGGESTED DEFAULT: {_storeConfig.PaymentMethods.DefaultMethod}");
                    paymentMethodsContext.AppendLine();
                }

                paymentMethodsContext.AppendLine("ARCHITECTURAL PRINCIPLE: Only process payments using methods listed above.");
                paymentMethodsContext.AppendLine("If customer requests unlisted payment method, inform them it's not accepted.");
                
                return paymentMethodsContext.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payment methods context: {Error}", ex.Message);
                return $"ERROR: Unable to load payment methods context: {ex.Message}. " +
                       "Please check store configuration and ensure payment methods are properly configured.";
            }
        }

        private async Task<IReadOnlyList<IProductInfo>> TrySemanticFallbackAsync(string vagueTerm)
        {
            // Simplified semantic mapping for very basic fallbacks only
            // Complex language/cultural patterns are now handled by AI layer
            var semanticMappings = new Dictionary<string, string[]>
            {
                { "bite to eat", new[] { "croissant", "muffin", "sandwich", "pastry" } },
                { "snack", new[] { "croissant", "muffin", "cookie", "pastry" } },
                { "something sweet", new[] { "muffin", "cookie", "pastry", "dessert" } },
                { "food", new[] { "sandwich", "croissant", "muffin" } },
                { "pastry", new[] { "croissant", "muffin", "danish" } }
            };

            var normalizedTerm = vagueTerm.ToLowerInvariant();
            
            if (semanticMappings.TryGetValue(normalizedTerm, out var searchTerms))
            {
                var results = new List<IProductInfo>();
                foreach (var term in searchTerms)
                {
                    var matches = await _productCatalog.SearchProductsAsync(term, 3);
                    results.AddRange(matches);
                }
                return results.GroupBy(p => p.Sku).Select(g => g.First()).ToList();
            }

            return Array.Empty<IProductInfo>();
        }

        private async Task<List<IProductInfo>> GetBroaderSearchResults(string searchTerm)
        {
            // Simple broader search without hardcoded language assumptions
            var searchWords = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var word in searchWords)
            {
                if (word.Length > 2) // Skip very short words
                {
                    var results = await SearchProductsAsync(word, 5);
                    if (results.Any())
                    {
                        return results;
                    }
                }
            }
            
            return new List<IProductInfo>();
        }

        private bool IsSpecificProductMatch(string searchTerm, IProductInfo product)
        {
            var normalizedSearch = searchTerm.ToLowerInvariant().Trim();
            var normalizedProductName = product.Name.ToLowerInvariant().Trim();
            
            // CRITICAL FIX: When customer clarifies with specific terms, auto-match them
            
            // Exact matches - always specific
            if (normalizedSearch == normalizedProductName || normalizedSearch == product.Sku.ToLowerInvariant())
            {
                return true;
            }
            
            // KOPITIAM INTELLIGENCE: Handle customer clarifications
            // When customer says "kaya toast" after disambiguation, they mean the basic one
            if (normalizedSearch == "kaya toast" && normalizedProductName == "kaya toast")
            {
                return true; // Customer clarified - choose basic kaya toast
            }
            
            // When customer says "teh c kosong", they're being specific
            if (normalizedSearch == "teh c kosong" && normalizedProductName == "teh c kosong")
            {
                return true; // Customer was specific - choose teh C kosong
            }
            
            // Handle other specific kopitiam terms
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
            
            // Only force disambiguation for very generic terms
            var veryGenericTerms = new[] { "coffee", "drink", "food", "snack", "pastry", "muffin", "sandwich" };
            if (veryGenericTerms.Contains(normalizedSearch))
            {
                return false; // These need disambiguation
            }
            
            // Size/specificity qualifiers make it more specific
            var specificityIndicators = new[] { "large", "small", "medium", "hot", "iced", "decaf", "regular", "kosong", "siew dai", "gao", "poh", "peng" };
            var hasSpecificity = specificityIndicators.Any(indicator => normalizedSearch.Contains(indicator));
            
            if (hasSpecificity && normalizedProductName.Contains(normalizedSearch.Replace(" kosong", "").Replace(" peng", "")))
            {
                return true; // Specific enough with modifiers
            }
            
            return false; // When in doubt, require disambiguation
        }

        private BusinessContext AnalyzeBusinessContext(IProductInfo product, Transaction transaction)
        {
            // "Crusty Sales Manager" logic - configurable business rules
            var hasHotDrink = transaction.Lines.Any(l => l.ProductId.ToString().Contains("COFFEE") || l.ProductId.ToString().Contains("KOPI") || l.ProductId.ToString().Contains("TEH"));
            var hasFood = transaction.Lines.Any(l => l.ProductId.ToString().Contains("CROISSANT") || l.ProductId.ToString().Contains("MUFFIN") || l.ProductId.ToString().Contains("TOAST") || l.ProductId.ToString().Contains("KUEH"));
            
            var context = new BusinessContext();
            
            // Business Rule 1: Suggest food with drinks
            if ((product.Name.ToLower().Contains("coffee") || product.Name.ToLower().Contains("kopi") || product.Name.ToLower().Contains("teh")) && !hasFood)
            {
                context.SuggestUpsell = true;
                context.UpsellCategory = product.Name.ToLower().Contains("kopi") || product.Name.ToLower().Contains("teh") ? "kueh" : "pastry";
            }
            
            // Business Rule 2: Suggest drinks with food  
            if ((product.Name.ToLower().Contains("croissant") || product.Name.ToLower().Contains("muffin") || product.Name.ToLower().Contains("toast") || product.Name.ToLower().Contains("kueh")) && !hasHotDrink)
            {
                context.SuggestUpsell = true;
                context.UpsellCategory = "drink";
            }
            
            // Business Rule 3: Time-based recommendations (could be configurable)
            var currentHour = DateTime.Now.Hour;
            if (currentHour < 10 && (product.Name.ToLower().Contains("coffee") || product.Name.ToLower().Contains("kopi")))
            {
                context.PriorityBoost = true; // Morning coffee rush priority
            }
            
            return context;
        }

        private IReadOnlyList<IProductInfo> ApplyBusinessIntelligence(IReadOnlyList<IProductInfo> products, Transaction transaction, string originalTerm)
        {
            // "Sales Manager" prioritization logic
            var prioritizedProducts = products.ToList();
            
            // Business Intelligence Rule 1: Prioritize items that create upsell opportunities
            prioritizedProducts.Sort((a, b) => 
            {
                var aContext = AnalyzeBusinessContext(a, transaction);
                var bContext = AnalyzeBusinessContext(b, transaction);
                
                // Prioritize items that enable upselling
                if (aContext.SuggestUpsell && !bContext.SuggestUpsell)
                {
                    return -1;
                }
                if (!aContext.SuggestUpsell && bContext.SuggestUpsell)
                {
                    return 1;
                }
                
                // Then prioritize by time-based rules
                if (aContext.PriorityBoost && !bContext.PriorityBoost)
                {
                    return -1;
                }
                if (!aContext.PriorityBoost && bContext.PriorityBoost)
                {
                    return 1;
                }
                
                // Finally, sort by price (configurable strategy)
                return a.BasePriceCents.CompareTo(b.BasePriceCents);
            });
            
            // Limit results based on business rules (avoid overwhelming customers)
            return prioritizedProducts.Take(GetMaxOptionsForTerm(originalTerm)).ToList();
        }

        private int GetMaxOptionsForTerm(string term)
        {
            // Configurable business rule: limit options to avoid decision paralysis
            return term.ToLower() switch
            {
                var t when t.Contains("coffee") || t.Contains("kopi") => 3, // Coffee has many options, limit to 3
                var t when t.Contains("bite to eat") => 4, // Food can have more variety
                var t when t.Contains("teh") => 3, // Tea options
                _ => 5 // Default max
            };
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Currency formatting uses store configuration - no hardcoded assumptions.
        /// </summary>
        private string FormatCurrency(decimal amount) {
            if (_storeConfig?.Currency == null) {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Store configuration missing currency. " +
                    $"Cannot format {amount} without valid currency in store config. " +
                    $"Ensure StoreConfig has valid Currency property.");
            }
            
            // Use store currency configuration for basic formatting
            return _storeConfig.Currency.ToUpperInvariant() switch {
                "USD" => $"${amount:F2}",
                "SGD" => $"S${amount:F2}",
                "EUR" => $"€{amount:F2}",
                "GBP" => $"£{amount:F2}",
                "JPY" => $"¥{amount:F0}",
                _ => $"{amount:F2} {_storeConfig.Currency}"
            };
        }

        private string AddProductToTransaction(IProductInfo product, int quantity, Transaction transaction)
        {
            var price = GetProductPrice(product);
            var unitPrice = new Money((long)(price * 100), "SGD"); // Convert to minor units
            
            for (int i = 0; i < quantity; i++)
            {
                transaction.AddLine(new ProductId(product.Name), 1, unitPrice);
            }
            
            var totalPrice = price * quantity;
            return $"ADDED: {product.Name} x{quantity} @ {FormatCurrency(price)} each = {FormatCurrency(totalPrice)}";
        }

        private decimal GetProductPrice(IProductInfo product)
        {
            // Try to get price from different possible properties
            var productType = product.GetType();
            
            // Try BasePrice property
            var basePriceProperty = productType.GetProperty("BasePrice");
            if (basePriceProperty != null && basePriceProperty.PropertyType == typeof(decimal))
            {
                return (decimal)basePriceProperty.GetValue(product)!;
            }
            
            // Try Price property
            var priceProperty = productType.GetProperty("Price");
            if (priceProperty != null && priceProperty.PropertyType == typeof(decimal))
            {
                return (decimal)priceProperty.GetValue(product)!;
            }
            
            // Default fallback price
            return 2.50m;
        }

        private bool IsExactMatch(string searchTerm, IProductInfo product)
        {
            var normalizedSearch = searchTerm.ToLowerInvariant().Trim();
            var normalizedProductName = product.Name.ToLowerInvariant().Trim();
            
            // Direct exact matches (SKU or full name match)
            if (normalizedSearch == normalizedProductName || normalizedSearch == product.Sku.ToLowerInvariant())
            {
                return true;
            }
            
            return false;
        }

        // Simple business context data structure
        private class BusinessContext
        {
            public bool SuggestUpsell { get; set; }
            public string UpsellCategory { get; set; } = "";
            public bool PriorityBoost { get; set; }
            public string ReasonCode { get; set; } = "";
        }
    }
}
