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

        /// <summary>
        /// Initializes a new instance of the PosToolsProvider.
        /// </summary>
        /// <param name="productCatalog">The product catalog service for database access.</param>
        /// <param name="logger">Logger for diagnostics and debugging.</param>
        public PosToolsProvider(IProductCatalogService productCatalog, ILogger<PosToolsProvider> logger)
        {
            _productCatalog = productCatalog ?? throw new ArgumentNullException(nameof(productCatalog));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                CreateProcessPaymentTool()
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
                    "process_payment" => ExecuteProcessPayment(toolCall, transaction),
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
                Description = "Adds an item to the current transaction using exact product SKU or name",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        product_identifier = new
                        {
                            type = "string",
                            description = "Product SKU, name, or barcode"
                        },
                        quantity = new
                        {
                            type = "integer",
                            description = "Quantity to add (default: 1)",
                            minimum = 1,
                            @default = 1
                        }
                    },
                    required = new[] { "product_identifier" }
                }
            };
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

        private McpTool CreateProcessPaymentTool()
        {
            return new McpTool
            {
                Name = "process_payment",
                Description = "Processes payment for the current transaction",
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

        private async Task<string> ExecuteAddItemAsync(McpToolCall toolCall, Transaction transaction)
        {
            var productId = toolCall.Arguments["product_identifier"].GetString()!;
            var quantity = toolCall.Arguments.TryGetValue("quantity", out var qtyElement) ? qtyElement.GetInt32() : 1;

            _logger.LogDebug("Attempting to add item: {ProductId} (qty: {Quantity})", productId, quantity);

            // Let the KERNEL (via product catalog) determine what matches this search term
            var matches = await _productCatalog.SearchProductsAsync(productId, 10);

            if (matches.Count == 0)
            {
                // Try semantic/category-based fallback for common vague terms
                var fallbackResults = await TrySemanticFallbackAsync(productId);
                if (fallbackResults.Any())
                {
                    // Apply business intelligence to semantic results
                    var strategicOptions = ApplyBusinessIntelligence(fallbackResults, transaction, productId);
                    var fallbackOptions = strategicOptions.Select(p => $"{p.Name} (${p.BasePriceCents / 100.0:F2})").ToList();
                    return $"DISAMBIGUATION_NEEDED: For '{productId}', I found these options: {string.Join(", ", fallbackOptions)}";
                }
                
                return $"PRODUCT_NOT_FOUND: No products match '{productId}'. Consider asking about categories or popular items.";
            }

            // CRITICAL FIX: Only auto-add if there's EXACTLY one match AND it's a very specific search
            if (matches.Count == 1 && IsSpecificProductMatch(productId, matches.First()))
            {
                // Unambiguous and specific match - apply business rules before adding
                var product = matches.First();
                var businessContext = AnalyzeBusinessContext(product, transaction);
                
                var posProductId = new ProductId(product.Sku);
                var unitPrice = new Money(product.BasePriceCents, transaction.Currency);

                transaction.AddLine(posProductId, quantity, unitPrice);
                
                // Return smart response with business intelligence
                var response = $"ADDED: {product.Name} (${product.BasePriceCents / 100.0:F2}) x{quantity} = ${(product.BasePriceCents * quantity) / 100.0:F2}. New total: ${transaction.Total.ToDecimal():F2}";
                
                // Add strategic upsell hints for the AI layer
                if (businessContext.SuggestUpsell)
                {
                    response += $" | UPSELL_HINT: {businessContext.UpsellCategory}";
                }
                
                return response;
            }

            // Multiple matches OR generic term - always require disambiguation
            var strategicMatches = ApplyBusinessIntelligence(matches, transaction, productId);
            var options = strategicMatches.Select(p => $"{p.Name} (${p.BasePriceCents / 100.0:F2})").ToList();
            return $"DISAMBIGUATION_NEEDED: Found {strategicMatches.Count} options for '{productId}': {string.Join(", ", options)}";
        }

        private bool IsSpecificProductMatch(string searchTerm, IProductInfo product)
        {
            var normalizedSearch = searchTerm.ToLowerInvariant().Trim();
            var normalizedProductName = product.Name.ToLowerInvariant().Trim();
            
            // Only consider it "specific" if:
            // 1. Exact name match, OR
            // 2. Exact SKU match, OR  
            // 3. The search term includes size/specificity qualifiers
            
            // Exact matches
            if (normalizedSearch == normalizedProductName || normalizedSearch == product.Sku.ToLowerInvariant())
            {
                return true;
            }
            
            // Generic terms should NEVER auto-add, always require disambiguation
            var genericTerms = new[] { "coffee", "drink", "food", "snack", "pastry", "muffin", "sandwich", "latte" };
            if (genericTerms.Contains(normalizedSearch))
            {
                return false; // Force disambiguation for generic terms
            }
            
            // Size/specificity qualifiers make it more specific
            var specificityIndicators = new[] { "large", "small", "medium", "hot", "iced", "decaf", "regular" };
            var hasSpecificity = specificityIndicators.Any(indicator => normalizedSearch.Contains(indicator));
            
            if (hasSpecificity && normalizedProductName.Contains(normalizedSearch))
            {
                return true; // "large coffee" matching "Large Coffee" is specific enough
            }
            
            return false; // When in doubt, require disambiguation
        }

        private BusinessContext AnalyzeBusinessContext(IProductInfo product, Transaction transaction)
        {
            // "Crusty Sales Manager" logic - configurable business rules
            var hasHotDrink = transaction.Lines.Any(l => l.ProductId.ToString().Contains("COFFEE"));
            var hasFood = transaction.Lines.Any(l => l.ProductId.ToString().Contains("CROISSANT") || l.ProductId.ToString().Contains("MUFFIN"));
            
            var context = new BusinessContext();
            
            // Business Rule 1: Suggest food with drinks
            if (product.Name.ToLower().Contains("coffee") && !hasFood)
            {
                context.SuggestUpsell = true;
                context.UpsellCategory = "pastry";
            }
            
            // Business Rule 2: Suggest drinks with food  
            if ((product.Name.ToLower().Contains("croissant") || product.Name.ToLower().Contains("muffin")) && !hasHotDrink)
            {
                context.SuggestUpsell = true;
                context.UpsellCategory = "coffee";
            }
            
            // Business Rule 3: Time-based recommendations (could be configurable)
            var currentHour = DateTime.Now.Hour;
            if (currentHour < 10 && product.Name.ToLower().Contains("coffee"))
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
                if (aContext.SuggestUpsell && !bContext.SuggestUpsell) return -1;
                if (!aContext.SuggestUpsell && bContext.SuggestUpsell) return 1;
                
                // Then prioritize by time-based rules
                if (aContext.PriorityBoost && !bContext.PriorityBoost) return -1;
                if (!aContext.PriorityBoost && bContext.PriorityBoost) return 1;
                
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
                var t when t.Contains("coffee") => 3, // Coffee has many options, limit to 3
                var t when t.Contains("bite to eat") => 4, // Food can have more variety
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

        private async Task<IReadOnlyList<IProductInfo>> TrySemanticFallbackAsync(string vagueTerm)
        {
            // Map common vague terms to categories or search terms
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
    }
}
