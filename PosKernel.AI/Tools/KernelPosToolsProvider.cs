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
        /// Uses the factory to auto-detect and connect to the best available kernel (Rust preferred).
        /// </summary>
        /// <param name="restaurantClient">The restaurant extension client for product catalog operations.</param>
        /// <param name="logger">Logger for diagnostics and debugging.</param>
        /// <param name="configuration">Application configuration for kernel selection.</param>
        /// <param name="customizationService">Service for parsing product customizations.</param>
        public KernelPosToolsProvider(
            RestaurantExtensionClient restaurantClient,
            ILogger<KernelPosToolsProvider> logger,
            IConfiguration? configuration = null,
            ProductCustomizationService? customizationService = null)
        {
            _restaurantClient = restaurantClient ?? throw new ArgumentNullException(nameof(restaurantClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _customizationService = customizationService ?? new ProductCustomizationService();
            
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
        public KernelPosToolsProvider(
            RestaurantExtensionClient restaurantClient,
            ILogger<KernelPosToolsProvider> logger,
            PosKernelClientFactory.KernelType kernelType,
            IConfiguration? configuration = null,
            ProductCustomizationService? customizationService = null)
        {
            _restaurantClient = restaurantClient ?? throw new ArgumentNullException(nameof(restaurantClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _customizationService = customizationService ?? new ProductCustomizationService();
            
            // Use factory to create specific kernel type
            _kernelClient = PosKernelClientFactory.CreateClient(_logger, kernelType, configuration);
            
            _logger.LogInformation("🚀 KernelPosToolsProvider initialized with {KernelType} kernel", kernelType);
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
                // Apply cultural translation first
                var translatedDescription = ApplyCulturalTranslation(itemDescription);
                var searchTerm = translatedDescription ?? itemDescription;

                // Search for BASE PRODUCT only - no recipe modifications in the search
                var searchResults = await _restaurantClient.SearchProductsAsync(searchTerm, 10, cancellationToken);

                // If restaurant extension returns no results, this is a real problem - don't mask it
                if (!searchResults.Any())
                {
                    // Try broader search only if exact search fails
                    var broadSearchResults = await GetBroaderSearchResults(searchTerm, cancellationToken);
                    if (!broadSearchResults.Any())
                    {
                        return $"PRODUCT_NOT_FOUND: No products found matching '{itemDescription}' (searched as: '{searchTerm}'). Please check that the Restaurant Extension database is properly initialized.";
                    }
                    searchResults = broadSearchResults;
                }

                // Apply confidence-based disambiguation logic
                var exactMatches = searchResults.Where(p => IsExactMatch(searchTerm, p)).ToList();
                var specificMatches = searchResults.Where(p => IsSpecificProductMatch(searchTerm, p)).ToList();

                // Simple decision logic - let AI handle cultural intelligence
                if (context == "clarification_response")
                {
                    var bestMatch = exactMatches.FirstOrDefault() ?? specificMatches.FirstOrDefault() ?? searchResults.First();
                    return await AddProductToTransactionAsync(bestMatch, quantity, preparationNotes, cancellationToken);
                }
                else if (exactMatches.Count == 1 && searchResults.Count == 1)
                {
                    return await AddProductToTransactionAsync(exactMatches.First(), quantity, preparationNotes, cancellationToken);
                }
                else if (exactMatches.Count == 1 && confidence >= 0.8)
                {
                    return await AddProductToTransactionAsync(exactMatches.First(), quantity, preparationNotes, cancellationToken);
                }
                else if (confidence >= 0.9)
                {
                    var bestMatch = exactMatches.FirstOrDefault() ?? specificMatches.FirstOrDefault() ?? searchResults.First();
                    return await AddProductToTransactionAsync(bestMatch, quantity, preparationNotes, cancellationToken);
                }
                else
                {
                    // Return options and let AI decide how to present them
                    var relevantOptions = GetRelevantDisambiguationOptions(searchTerm, searchResults).Take(3).ToList();
                    
                    if (relevantOptions.Any())
                    {
                        var optionsList = string.Join(", ", relevantOptions.Select(p => $"{p.Name} (${p.BasePriceCents / 100.0:F2})"));
                        return $"DISAMBIGUATION_NEEDED: Found {relevantOptions.Count} options for '{itemDescription}': {optionsList}";
                    }
                    else
                    {
                        return $"PRODUCT_NOT_FOUND: No suitable products found for '{itemDescription}'";
                    }
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
            return $"ADDED: {product.Name}{prepNote} x{quantity} @ ${unitPrice:F2} each = ${totalPrice:F2}";
        }

        private string? ApplyCulturalTranslation(string itemDescription)
        {
            var normalized = itemDescription.ToLowerInvariant().Trim();
            
            // Cultural translations for kopitiam terms
            var translations = new Dictionary<string, string>
            {
                ["roti kaya"] = "Kaya Toast",
                ["kaya roti"] = "Kaya Toast", 
                ["roti butter"] = "Butter Sugar Toast",
                ["telur separuh masak"] = "Half Boiled Eggs",
                ["telur setengah masak"] = "Half Boiled Eggs",
                ["soft boiled egg"] = "Soft Boiled Eggs",
                ["half boiled egg"] = "Half Boiled Eggs"
            };

            if (translations.TryGetValue(normalized, out var translation))
            {
                _logger.LogDebug("Cultural translation: '{Original}' → '{Translation}'", itemDescription, translation);
                return translation;
            }

            return null;
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

                if (!allProducts.Any())
                {
                    return "ERROR: Restaurant extension database is empty or not properly initialized. Cannot load menu context.";
                }

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

                menuContext.AppendLine("\n🧠 AI INTELLIGENCE GUIDANCE:");
                menuContext.AppendLine("=========================");
                menuContext.AppendLine("You are Uncle at a traditional kopitiam. You KNOW your complete menu.");
                menuContext.AppendLine("");
                menuContext.AppendLine("INTELLIGENT ORDER PROCESSING:");
                menuContext.AppendLine("1. When customer uses cultural terms → translate using YOUR menu knowledge");
                menuContext.AppendLine("2. When you KNOW the exact item exists → use high confidence (0.8-0.9)");
                menuContext.AppendLine("3. Only suggest items from your actual menu above");
                menuContext.AppendLine("4. Use your intelligence to match customer requests to actual products");
                menuContext.AppendLine("5. If customer wants something not on menu → explain what you actually have");
                menuContext.AppendLine("");
                menuContext.AppendLine("CULTURAL INTELLIGENCE:");
                menuContext.AppendLine("• Analyze customer language and match to your actual products");
                menuContext.AppendLine("• Use menu knowledge to suggest appropriate alternatives");
                menuContext.AppendLine("• Be honest about what you can and cannot serve");
                menuContext.AppendLine("• Let your personality guide the conversation naturally");
                menuContext.AppendLine("");
                menuContext.AppendLine("You now have complete menu knowledge and can serve customers intelligently!");
                
                return menuContext.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading menu context from restaurant extension: {Error}", ex.Message);
                return $"ERROR: Unable to load menu context: {ex.Message}. Please check that the Restaurant Extension service is running and the database is properly initialized.";
            }
        }

        // Helper methods (exact matching, broader search, etc.)
        private async Task<List<RestaurantProductInfo>> GetBroaderSearchResults(string searchTerm, CancellationToken cancellationToken)
        {
            // Try generic broader searches based on the actual search term, not hard-coded categories
            var broaderTerms = new List<string>();
            
            // Extract meaningful words from the search term for broader matching
            var words = searchTerm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                if (word.Length > 2) // Only meaningful words
                {
                    broaderTerms.Add(word);
                }
            }

            // Try each word individually
            foreach (var term in broaderTerms)
            {
                var results = await _restaurantClient.SearchProductsAsync(term, 10, cancellationToken);
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
            
            // Direct exact matches (SKU or full name match)
            if (normalizedSearch == normalizedProductName || normalizedSearch == product.Sku.ToLowerInvariant())
            {
                return true;
            }
            
            // Partial name match for compound products
            if (normalizedProductName.Contains(normalizedSearch) && normalizedSearch.Length > 3)
            {
                return true;
            }
            
            return false;
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
            
            // Strong partial matches for longer search terms
            if (normalizedSearch.Length > 4 && normalizedProductName.Contains(normalizedSearch))
            {
                return true;
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

        private List<RestaurantProductInfo> GetRelevantDisambiguationOptions(string searchTerm, IEnumerable<RestaurantProductInfo> allResults)
        {
            // Simply return the search results - let the AI decide what's relevant based on the actual data
            return allResults.OrderBy(p => p.Name).ToList();
        }
        
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
