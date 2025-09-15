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

namespace PosKernel.AI.Tools
{
    /// <summary>
    /// POS-specific MCP tools for AI integration with the Point-of-Sale system.
    /// Provides structured function calling interface for transactions, inventory, and customer service.
    /// </summary>
    public class PosToolsProvider
    {
        private readonly IProductCatalogService _productCatalog;
        private readonly ILogger<PosToolsProvider> _logger;
        private readonly ProductCustomizationService _customizationService;

        /// <summary>
        /// Initializes a new instance of the PosToolsProvider.
        /// </summary>
        /// <param name="productCatalog">The product catalog service for database access.</param>
        /// <param name="logger">Logger for diagnostics and debugging.</param>
        /// <param name="customizationService">Service for parsing product customizations.</param>
        public PosToolsProvider(IProductCatalogService productCatalog, ILogger<PosToolsProvider> logger, ProductCustomizationService? customizationService = null)
        {
            _productCatalog = productCatalog ?? throw new ArgumentNullException(nameof(productCatalog));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _customizationService = customizationService ?? new ProductCustomizationService();
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
                var productCatalog = _productCatalog ?? throw new InvalidOperationException("Product catalog not configured");

                // First, try cultural translation for kopitiam terms
                var translatedTerm = ApplyKopitiamCulturalTranslation(itemDescription);
                
                // Search for products in the catalog
                var searchResults = await productCatalog.SearchProductsAsync(translatedTerm, maxResults: 10);

                if (!searchResults.Any())
                {
                    // Try broader search
                    var broadSearchResults = await GetBroaderSearchResults(translatedTerm);
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
                    var optionsList = string.Join(", ", disambiguationOptions.Select(p => $"{p.Name} (${GetProductPrice(p):F2})"));
                    
                    return $"DISAMBIGUATION_NEEDED: Found {disambiguationOptions.Count} options for '{itemDescription}': {optionsList}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to transaction: {Error}", ex.Message);
                return $"ERROR: Unable to process item '{itemDescription}': {ex.Message}";
            }
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
            return $"ADDED: {product.Name} x{quantity} @ ${price:F2} each = ${totalPrice:F2}";
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
            
            // KOPITIAM INTELLIGENCE: Handle base product matching
            // "teh c kosong" should match "Teh C" (base product)
            // The "kosong" is a preparation instruction, not part of the product name
            
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
                Description = "Loads the complete menu for Uncle's cultural intelligence and product knowledge",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        include_categories = new
                        {
                            type = "boolean",
                            description = "Whether to include category information (default: true)",
                            @default = true
                        }
                    }
                }
            };
        }

        private async Task<string> ExecuteSearchProductsAsync(McpToolCall toolCall)
        {
            var searchTerm = toolCall.Arguments["search_term"].GetString()!;
            var maxResults = toolCall.Arguments.TryGetValue("max_results", out var maxElement) ? maxElement.GetInt32() : 10;

            var products = await _productCatalog.SearchProductsAsync(searchTerm, maxResults);
            
            if (!products.Any())
            {
                return $"No products found matching: {searchTerm}";
            }

            var results = products.Select(p => 
                $"• {p.Name} - ${p.BasePriceCents / 100.0:F2} ({p.Category})").ToList();

            return $"Found {products.Count} products:\n" + string.Join("\n", results);
        }

        private async Task<string> ExecuteGetProductInfoAsync(McpToolCall toolCall)
        {
            var productId = toolCall.Arguments["product_identifier"].GetString()!;

            var products = await _productCatalog.SearchProductsAsync(productId, 1);
            if (!products.Any())
            {
                return $"Product not found: {productId}";
            }

            var product = products.First();
            return $"Product: {product.Name}\n" +
                   $"Price: ${product.BasePriceCents / 100.0:F2}\n" +
                   $"Category: {product.Category}\n" +
                   $"Description: {product.Description}\n" +
                   $"SKU: {product.Sku}\n" +
                   $"Available: {(product.IsActive ? "Yes" : "No")}";
        }

        private async Task<string> ExecuteGetPopularItemsAsync(McpToolCall toolCall)
        {
            var count = toolCall.Arguments.TryGetValue("count", out var countElement) ? countElement.GetInt32() : 5;

            var products = await _productCatalog.GetPopularItemsAsync();
            var popularItems = products.Take(count);

            if (!popularItems.Any())
            {
                return "No popular items available";
            }

            var results = popularItems.Select(p => 
                $"• {p.Name} - ${p.BasePriceCents / 100.0:F2}").ToList();

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
            var paymentMethod = toolCall.Arguments.TryGetValue("payment_method", out var methodElement) 
                ? methodElement.GetString() : "cash";
            
            var amount = toolCall.Arguments.TryGetValue("amount", out var amountElement) 
                ? new Money((long)(amountElement.GetDecimal() * 100), transaction.Currency)
                : transaction.Total;

            if (transaction.Total.MinorUnits == 0)
            {
                return "Cannot process payment: Transaction is empty";
            }

            // For now, only support cash payments in the demo
            if (paymentMethod?.ToLower() != "cash")
            {
                return $"Payment method '{paymentMethod}' not supported in demo. Please use 'cash'";
            }

            transaction.AddCashTender(amount);

            var change = transaction.ChangeDue.ToDecimal();
            var result = $"Payment processed: ${amount.ToDecimal():F2} cash\n" +
                        $"Total: ${transaction.Total.ToDecimal():F2}\n" +
                        $"Change due: ${change:F2}\n" +
                        $"Transaction status: {transaction.State}";

            return result;
        }

        private async Task<string> ExecuteLoadMenuContextAsync(McpToolCall toolCall)
        {
            var includeCategories = toolCall.Arguments.TryGetValue("include_categories", out var categoriesElement) 
                ? categoriesElement.GetBoolean() : true;

            try
            {
                // Load the complete menu for Uncle's context
                var allProducts = await _productCatalog.SearchProductsAsync("", maxResults: 100);
                var categories = includeCategories ? await _productCatalog.GetCategoriesAsync() : new List<string>();

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
                _logger.LogError(ex, "Error loading menu context: {Error}", ex.Message);
                return $"ERROR: Unable to load menu context: {ex.Message}";
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

        private string ApplyKopitiamCulturalTranslation(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }
            
            var translated = input.ToLowerInvariant().Trim();
            
            // CULTURAL INTELLIGENCE: Extract preparation instructions
            // These are instructions for the drink maker, not separate products
            
            // Core translations: "si" means evaporated milk (C)
            translated = translated.Replace("teh si", "teh c");
            translated = translated.Replace("kopi si", "kopi c"); 
            translated = translated.Replace("roti kaya", "kaya toast");
            translated = translated.Replace("roti", "toast");
            
            // IMPORTANT: Keep modifiers for cultural context but search for base product
            // Uncle will understand "teh c kosong" means:
            // - Add "Teh C" to cart
            // - Tell drink maker: "kosong" (no sugar)
            
            return translated;
        }

        private async Task<IReadOnlyList<IProductInfo>> GetBroaderSearchResults(string searchTerm)
        {
            // Try searching for broader terms
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
                var results = await _productCatalog.SearchProductsAsync(term, maxResults: 5);
                if (results.Any())
                {
                    return results;
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
