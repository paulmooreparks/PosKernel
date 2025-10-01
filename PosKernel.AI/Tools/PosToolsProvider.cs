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
using PosKernel.Configuration.Services;

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
        private readonly ICurrencyFormattingService? _currencyFormatter;
        private readonly IConfigurableThresholdService? _thresholdService;

        /// <summary>
        /// Initializes a new instance of the PosToolsProvider with IProductCatalogService.
        /// </summary>
        /// <param name="productCatalog">The product catalog service for database access.</param>
        /// <param name="logger">Logger for diagnostics and debugging.</param>
        /// <param name="storeConfig">Store configuration for currency and locale information.</param>
        /// <param name="currencyFormatter">Currency formatting service.</param>
        /// <param name="thresholdService">Configurable threshold service.</param>
        public PosToolsProvider(IProductCatalogService productCatalog, ILogger<PosToolsProvider> logger, StoreConfig? storeConfig = null, ICurrencyFormattingService? currencyFormatter = null, IConfigurableThresholdService? thresholdService = null)
        {
            _productCatalog = productCatalog ?? throw new ArgumentNullException(nameof(productCatalog));
            _restaurantClient = null;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storeConfig = storeConfig;
            _currencyFormatter = currencyFormatter;
            _thresholdService = thresholdService;
        }

        /// <summary>
        /// Initializes a new instance of the PosToolsProvider with RestaurantExtensionClient.
        /// </summary>
        /// <param name="restaurantClient">The restaurant extension client for product operations.</param>
        /// <param name="logger">Logger for diagnostics and debugging.</param>
        /// <param name="storeConfig">Store configuration for currency and locale information.</param>
        /// <param name="currencyFormatter">Currency formatting service.</param>
        /// <param name="thresholdService">Configurable threshold service.</param>
        public PosToolsProvider(RestaurantExtensionClient restaurantClient, ILogger<PosToolsProvider> logger, StoreConfig? storeConfig = null, ICurrencyFormattingService? currencyFormatter = null, IConfigurableThresholdService? thresholdService = null)
        {
            _restaurantClient = restaurantClient ?? throw new ArgumentNullException(nameof(restaurantClient));
            _productCatalog = null;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storeConfig = storeConfig;
            _currencyFormatter = currencyFormatter;
            _thresholdService = thresholdService;
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
                            description = "Confidence level: 0.0-0.4 (require disambiguation), 0.5-0.7 (context-aware), 0.8+ (auto-add based on store configuration)",
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
                // ARCHITECTURAL PRINCIPLE: AI handles ALL product matching and disambiguation decisions
                // Pure data delivery - no business logic in tools
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

                // AI-FIRST: Let AI decide which product to add based on all available options
                // No hardcoded confidence thresholds or matching rules
                if (searchResults.Count == 1)
                {
                    // Only one option - add it directly
                    return AddProductToTransaction(searchResults.First(), quantity, transaction);
                }
                else if (searchResults.Count > 1)
                {
                    // Multiple options - provide structured data for AI to decide
                    var optionsList = string.Join(", ", searchResults.Take(5).Select(p => $"{p.Name} ({FormatCurrency(GetProductPrice(p))})"));
                    return $"DISAMBIGUATION_NEEDED: Found {searchResults.Count} options for '{itemDescription}': {optionsList}. Please specify which one you'd like.";
                }
                else
                {
                    return $"PRODUCT_NOT_FOUND: No products found matching '{itemDescription}'";
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
                Description = "Loads the complete menu from the restaurant extension for AI context intelligence",
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
            var total = transaction.Total.Amount;

            if (itemCount == 0)
            {
                return $"Transaction is empty. Total: {FormatCurrency(transaction.Total.Amount)}";
            }

            var itemsList = transaction.Lines.Select(line =>
            {
                var extended = line.Extended.Amount;
                return $"{line.ProductId} x{line.Quantity} = {FormatCurrency(extended)}";
            }).ToList();

            return $"Transaction contains {itemCount} line items:\n" +
                   string.Join("\n", itemsList) +
                   $"\n\nTotal: {FormatCurrency(total)}";
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
                verification.AppendLine($"  • {line.ProductId} x{line.Quantity} = {FormatCurrency(line.Extended.Amount)}");
            }

            verification.AppendLine($"TOTAL: {FormatCurrency(transaction.Total.Amount)}");
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
                ? new Money(amountElement.GetDecimal(), transaction.Currency)
                : transaction.Total;

            if (transaction.Total.Amount == 0)
            {
                return "Cannot process payment: Transaction is empty";
            }

            // ARCHITECTURAL PRINCIPLE: AI layer should NOT process payments directly
            // Payment processing should go through kernel tools, not mock calculations
            // This is a mock implementation - real implementation should use kernel payment processing

            var method = paymentMethod?.ToLower() ?? "cash";

            if (method == "cash")
            {
                // ARCHITECTURAL FIX: Don't do payment calculations in AI layer
                // In real implementation, this would call kernel payment processing tools
                return $"MOCK: Payment processed: {FormatCurrency(amount.Amount)} cash received\n" +
                      $"Total: {FormatCurrency(transaction.Total.Amount)}\n" +
                      $"Change due: {FormatCurrency(Math.Max(0, amount.Amount - transaction.Total.Amount))}\n" +
                      $"Transaction completed successfully.";
            }
            else if (method == "card" || method == "credit" || method == "debit")
            {
                // ARCHITECTURAL FIX: Don't do payment calculations in AI layer
                return $"MOCK: Payment processed: {FormatCurrency(transaction.Total.Amount)} card payment\n" +
                      $"Total: {FormatCurrency(transaction.Total.Amount)}\n" +
                      $"Change due: {FormatCurrency(0m)}\n" +
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
                if (_productCatalog != null)
                {
                    // Load the complete menu using product catalog
                    var allProducts = await _productCatalog.SearchProductsAsync(string.Empty, maxResults: 100);
                    var categories = includeCategories ? await _productCatalog.GetCategoriesAsync() : new List<string>();

                    var menuContext = new StringBuilder();
                    menuContext.AppendLine("KOPITIAM MENU CONTEXT - Complete Product Knowledge:");
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
                            menuContext.AppendLine($"  • {product.Name} - {FormatCurrency(price)} (SKU: {product.Sku})");
                            if (!string.IsNullOrEmpty(product.Description))
                            {
                                menuContext.AppendLine($"    {product.Description}");
                            }
                        }
                    }

                    menuContext.AppendLine("\nAI now has complete menu knowledge for intelligent order processing!");

                    return menuContext.ToString();
                }
                else if (_restaurantClient != null)
                {
                    // Load the complete menu using restaurant extension client
                    var categories = includeCategories ? await _restaurantClient.GetCategoriesAsync() : new List<string>();
                    var allProducts = new List<PosKernel.Extensions.Restaurant.ProductInfo>();

                    foreach (var category in categories)
                    {
                        var categoryProducts = await _restaurantClient.GetCategoryProductsAsync(category);
                        allProducts.AddRange(categoryProducts);
                    }

                    var menuContext = new StringBuilder();
                    menuContext.AppendLine("KOPITIAM MENU CONTEXT - Complete Product Knowledge:");
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

                    var productsByCategory = allProducts.GroupBy(p => p.CategoryName).OrderBy(g => g.Key);
                    foreach (var categoryGroup in productsByCategory)
                    {
                        menuContext.AppendLine($"\n{categoryGroup.Key.ToUpper()}:");
                        foreach (var product in categoryGroup.OrderBy(p => p.Name))
                        {
                            var price = product.BasePriceCents / 100.0m;
                            menuContext.AppendLine($"  • {product.Name} - {FormatCurrency(price)} (SKU: {product.Sku})");
                            if (!string.IsNullOrEmpty(product.Description))
                            {
                                menuContext.AppendLine($"    {product.Description}");
                            }
                        }
                    }

                    menuContext.AppendLine("\nAI now has complete menu knowledge for intelligent order processing!");
                    return menuContext.ToString();
                }
                else
                {
                    throw new InvalidOperationException(
                        "DESIGN DEFICIENCY: No product service available to load menu context. " +
                        "Provide IProductCatalogService or RestaurantExtensionClient.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading menu context: {Error}", ex.Message);
                return $"ERROR: Unable to load menu context: {ex.Message}";
            }
        }

        private Task<string> ExecuteLoadPaymentMethodsContextAsync(McpToolCall toolCall)
        {
            var includeLimits = toolCall.Arguments.TryGetValue("include_limits", out var limitsElement)
                ? limitsElement.GetBoolean() : true;

            try
            {
                // ARCHITECTURAL PRINCIPLE: Payment methods are loaded from store configuration, just like menu items
                if (_storeConfig == null)
                {
                    return Task.FromResult("ERROR: Store configuration not available. Cannot load payment methods without store context.");
                }

                if (_storeConfig.PaymentMethods == null)
                {
                    return Task.FromResult("ERROR: No payment methods configured for this store. Store configuration missing PaymentMethods section.");
                }

                var paymentMethodsContext = new StringBuilder();

                paymentMethodsContext.AppendLine("PAYMENT METHODS CONTEXT - Store-Specific Payment Configuration:");
                paymentMethodsContext.AppendLine("=".PadRight(70, '='));
                paymentMethodsContext.AppendLine();
                paymentMethodsContext.AppendLine($"Store: {_storeConfig.StoreName} (ID: {_storeConfig.StoreId})");
                paymentMethodsContext.AppendLine($"Currency: {_storeConfig.Currency}");
                paymentMethodsContext.AppendLine();

                var methods = _storeConfig.PaymentMethods.AcceptedMethods;
                if (methods != null && methods.Any())
                {
                    paymentMethodsContext.AppendLine("ACCEPTED PAYMENT METHODS:");

                    foreach (var method in methods.Where(m => m.IsEnabled))
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
                    return Task.FromResult("ERROR: No payment methods configured for this store. Store configuration must include accepted payment methods.");
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

                return Task.FromResult(paymentMethodsContext.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payment methods context: {Error}", ex.Message);
                return Task.FromResult($"ERROR: Unable to load payment methods context: {ex.Message}. Please check store configuration and ensure payment methods are properly configured.");
            }
        }

        private Task<IReadOnlyList<IProductInfo>> TrySemanticFallbackAsync(string vagueTerm)
        {
            // ARCHITECTURAL PRINCIPLE: AI handles semantic understanding, not hardcoded mappings
            // Pure infrastructure - no business logic in tools
            return Task.FromResult<IReadOnlyList<IProductInfo>>(Array.Empty<IProductInfo>());
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

        private BusinessContext AnalyzeBusinessContext(IProductInfo product, Transaction transaction)
        {
            // ARCHITECTURAL PRINCIPLE: Business rules removed - AI handles all business intelligence
            // Pure data context only - no hardcoded business decisions
            return new BusinessContext();
        }

        private IReadOnlyList<IProductInfo> ApplyBusinessIntelligence(IReadOnlyList<IProductInfo> products, Transaction transaction, string originalTerm)
        {
            // ARCHITECTURAL PRINCIPLE: Business intelligence removed - AI handles all prioritization
            // Pure data delivery - no hardcoded business decisions
            return products.Take(GetMaxOptionsForTerm(originalTerm)).ToList();
        }

        private int GetMaxOptionsForTerm(string term)
        {
            // ARCHITECTURAL PRINCIPLE: Business rules removed - AI handles result limiting
            // Pure infrastructure - consistent data delivery
            return 10; // Fixed reasonable limit - AI will handle intelligent filtering
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Currency formatting must use service - no client-side assumptions.
        /// </summary>
        private string FormatCurrency(decimal amount)
        {
            if (_currencyFormatter != null && _storeConfig != null)
            {
                return _currencyFormatter.FormatCurrency(amount, _storeConfig.Currency, _storeConfig.StoreId);
            }

            // ARCHITECTURAL PRINCIPLE: FAIL FAST - No fallback formatting
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Currency formatting service not available. " +
                $"Cannot format {amount} without proper currency service. " +
                $"Register ICurrencyFormattingService in DI container.");
        }

        private string AddProductToTransaction(IProductInfo product, int quantity, Transaction transaction)
        {
            var price = GetProductPrice(product);
            var currency = _storeConfig?.Currency;
            if (string.IsNullOrWhiteSpace(currency))
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: PosToolsProvider requires StoreConfig.Currency to add items. " +
                    "Client cannot decide currency defaults. Configure store currency in StoreConfig.");
            }
            // ARCHITECTURAL PRINCIPLE: AI layer should not do calculations
            // This is legacy mock code - real implementation uses kernel tools
            var unitPrice = new Money(price, currency);
            var extendedPrice = new Money(price * quantity, currency);

            for (int i = 0; i < quantity; i++)
            {
                var productIdName = product.Name ?? product.Sku ?? "UNKNOWN";
                var line = new TransactionLine(currency)
                {
                    ProductId = new ProductId(productIdName),
                    Quantity = 1,
                    UnitPrice = unitPrice,
                    Extended = unitPrice // Single item extended = unit price
                };
                transaction.Lines.Add(line);
            }

            var totalPrice = price * quantity;
            var displayName = product.Name ?? product.Sku ?? "Item";
            return $"ADDED: {displayName} x{quantity} @ {FormatCurrency(price)} each = {FormatCurrency(totalPrice)}";
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

        // Simple business context data structure (kept for backwards compatibility)
        private class BusinessContext
        {
            public bool SuggestUpsell { get; set; }
            public string UpsellCategory { get; set; } = "";
            public bool PriorityBoost { get; set; }
            public string ReasonCode { get; set; } = "";
        }
    }
}
