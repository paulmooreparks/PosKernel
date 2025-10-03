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
using HttpRestaurantExtensionClient = PosKernel.Extensions.Restaurant.Client.HttpRestaurantExtensionClient;
using HttpProductInfo = PosKernel.Extensions.Restaurant.Client.HttpProductInfo;
using RestaurantProductInfo = PosKernel.Extensions.Restaurant.ProductInfo;
using Microsoft.Extensions.Configuration;
using PosKernel.Configuration.Services;
using System.Linq;

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
        private readonly RestaurantExtensionClient? _restaurantClient;
        private readonly HttpRestaurantExtensionClient? _httpRestaurantClient;
        private readonly ILogger<KernelPosToolsProvider> _logger;
        private readonly IConfiguration? _configuration;
        private ICurrencyFormattingService? _currencyFormatter;
        private StoreConfig? _storeConfig;
        private readonly SemaphoreSlim _sessionSemaphore = new(1, 1); // ARCHITECTURAL FIX: Use async-safe semaphore for session management

        private string? _sessionId;
        private string? _currentTransactionId;
        private bool _disposed = false;

        /// <summary>
        /// Constructor with HTTP Restaurant Extension client (cross-platform).
        /// </summary>
        public KernelPosToolsProvider(
            HttpRestaurantExtensionClient httpRestaurantClient,
            ILogger<KernelPosToolsProvider> logger,
            IConfiguration? configuration = null)
        {
            _httpRestaurantClient = httpRestaurantClient ?? throw new ArgumentNullException(nameof(httpRestaurantClient));
            _restaurantClient = null;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration;
            _currencyFormatter = null;
            _storeConfig = null;

            _kernelClient = PosKernelClientFactory.CreateClient(_logger, configuration);
            _logger.LogInformation("🚀 KernelPosToolsProvider initialized with HTTP Restaurant Extension client");
        }

        /// <summary>
        /// Basic constructor with minimal dependencies.
        /// </summary>
        public KernelPosToolsProvider(
            RestaurantExtensionClient restaurantClient,
            ILogger<KernelPosToolsProvider> logger,
            IConfiguration? configuration = null)
        {
            _restaurantClient = restaurantClient ?? throw new ArgumentNullException(nameof(restaurantClient));
            _httpRestaurantClient = null;
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
            _httpRestaurantClient = null;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storeConfig = storeConfig ?? throw new ArgumentNullException(nameof(storeConfig));
            _configuration = configuration;
            _currencyFormatter = currencyFormatter;

            // ARCHITECTURAL PRINCIPLE: Validate store configuration completeness at construction time
            ValidateStoreConfiguration(storeConfig);

            _kernelClient = PosKernelClientFactory.CreateClient(_logger, kernelType, configuration);
            _logger.LogInformation("🚀 KernelPosToolsProvider initialized with {KernelType} kernel and store: {StoreName} ({Currency})",
                kernelType, storeConfig.StoreName, storeConfig.Currency);
        }

        /// <summary>
        /// Constructor with HTTP Restaurant Extension client and store configuration for cross-platform support.
        /// </summary>
        public KernelPosToolsProvider(
            HttpRestaurantExtensionClient httpRestaurantClient,
            ILogger<KernelPosToolsProvider> logger,
            StoreConfig storeConfig,
            PosKernelClientFactory.KernelType kernelType,
            IConfiguration? configuration = null,
            ICurrencyFormattingService? currencyFormatter = null)
        {
            _httpRestaurantClient = httpRestaurantClient ?? throw new ArgumentNullException(nameof(httpRestaurantClient));
            _restaurantClient = null;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storeConfig = storeConfig ?? throw new ArgumentNullException(nameof(storeConfig));
            _configuration = configuration;
            _currencyFormatter = currencyFormatter;

            // ARCHITECTURAL PRINCIPLE: Validate store configuration completeness at construction time
            ValidateStoreConfiguration(storeConfig);

            _kernelClient = PosKernelClientFactory.CreateClient(_logger, kernelType, configuration);
            _logger.LogInformation("🚀 KernelPosToolsProvider initialized with HTTP client, {KernelType} kernel and store: {StoreName} ({Currency})",
                kernelType, storeConfig.StoreName, storeConfig.Currency);
        }

        /// <summary>
        /// Helper method to search products using either client type.
        /// </summary>
        private async Task<IEnumerable<dynamic>> SearchProductsInternalAsync(string query, int maxResults, CancellationToken cancellationToken = default)
        {
            if (_httpRestaurantClient != null)
            {
                var products = await _httpRestaurantClient.SearchProductsAsync(query, maxResults);
                return products.Select(p => new
                {
                    Sku = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    Category = p.Category,
                    Description = p.Description
                });
            }
            else if (_restaurantClient != null)
            {
                // For now, throw exception - would need actual implementation
                throw new NotImplementedException("Named pipe client search not yet adapted");
            }
            else
            {
                throw new InvalidOperationException("No restaurant client available");
            }
        }

        /// <summary>
        /// Ensures we have an active session with the kernel.
        /// </summary>
        private async Task EnsureSessionAsync(CancellationToken cancellationToken = default)
        {
            // ARCHITECTURAL FIX: Use async-safe semaphore to prevent race conditions in session creation
            await _sessionSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (!string.IsNullOrEmpty(_sessionId) && _kernelClient.IsConnected)
                {
                    return;
                }

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
                                 "Ensure 'cargo run --bin pos-kernel-service' is running in pos-kernel-rs directory.";
                _logger.LogError(ex, errorMessage);
                throw new InvalidOperationException(errorMessage, ex);
            }
            finally
            {
                _sessionSemaphore.Release();
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
                CreateModifyLineItemTool(),
                CreateApplyModificationsToLineTool()
                // ARCHITECTURAL PRINCIPLE: Removed interpret_order_phrase - AI handles cultural understanding directly
            };
        }

        public async Task<object> ExecuteToolAsync(McpToolCall toolCall, CancellationToken cancellationToken = default)
        {
            switch (toolCall.FunctionName)
            {
                case "add_item_to_transaction":
                    return await ExecuteAddItemAsync(toolCall, cancellationToken);
                case "get_set_configuration":
                    return await ExecuteGetSetConfigurationAsync(toolCall, cancellationToken);
                case "update_set_configuration":
                    return await ExecuteUpdateSetConfigurationAsync(toolCall, cancellationToken);
                case "modify_line_item":
                    return await ExecuteModifyLineItemAsync(toolCall, cancellationToken);
                case "apply_modifications_to_line":
                    return await ExecuteApplyModificationsToLineAsync(toolCall, cancellationToken);
                case "search_products":
                    return await ExecuteSearchProductsAsync(toolCall, cancellationToken);
                case "get_popular_items":
                    return await ExecuteGetPopularItemsAsync(toolCall, cancellationToken);
                case "load_menu_context":
                    return await ExecuteLoadMenuContextAsync(toolCall, cancellationToken);
                case "calculate_transaction_total":
                    return await ExecuteCalculateTotalAsync(cancellationToken);
                case "get_transaction":
                    return await ExecuteGetTransactionAsync(cancellationToken);
                case "load_payment_methods_context":
                    return await ExecuteLoadPaymentMethodsContextAsync(toolCall, cancellationToken);
                case "process_payment":
                    return await ExecuteProcessPaymentAsync(toolCall, cancellationToken);
                // ARCHITECTURAL PRINCIPLE: Removed interpret_order_phrase - AI handles cultural understanding directly
                default:
                    return $"Unknown tool: {toolCall.FunctionName}";
            }
        }

        private McpTool CreateAddItemTool() => new()
        {
            Name = "add_item_to_transaction",
            Description = "Adds an item to the current transaction using the real POS kernel. Supports either product_sku (direct) or item_description (search). For items with recipe modifications, add the base product with preparation notes.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    product_sku = new { type = "string", description = "Optional: Exact product SKU to add directly (preferred when known)" },
                    item_description = new { type = "string", description = "Optional: Item name or description - AI will handle cultural translation to menu items" },
                    quantity = new { type = "integer", description = "Number of items requested", @default = 1 },
                    preparation_notes = new { type = "string", description = "Recipe modifications like 'no sugar', 'extra strong', 'iced', etc.", @default = "" },
                    confidence = new { type = "number", description = "Confidence hint from the prompt: 0.0-0.4 (disambiguate), 0.5-0.6 (context-aware), 0.7+ (auto-add).", @default = 0.5, minimum = 0.0, maximum = 1.0 },
                    context = new { type = "string", description = "Use 'clarification_response' when customer is responding to your question, otherwise 'initial_order' or 'follow_up_order'", @default = "initial_order" }
                },
                required = new string[] { }
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
            Description = "Updates set meal configuration with customer's customization choice. Prefer precise targeting via parent/child line identifiers.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    product_sku = new { type = "string", description = "The SKU of the set product being customized (used for validation/fallback)" },
                    customization_type = new { type = "string", description = "The type of customization (e.g., 'drink', 'side', 'size', 'preparation')" },
                    customization_value = new { type = "string", description = "The customer's choice for this customization. For preparation, use structured format 'MOD_SKU:Description|...'" },
                    target_parent_line_item_id = new { type = "string", description = "Preferred: Line item id (stable) of the set parent to attach the drink/component to" },
                    target_parent_line_number = new { type = "integer", description = "Alternative: Parent set line number to attach the drink/component to" },
                    expected_parent_sku = new { type = "string", description = "Optional: Validate that the targeted parent line matches this SKU" },
                    target_line_item_id = new { type = "string", description = "For 'preparation': target child line id to apply modifications to (e.g., the drink line id)" },
                    target_line_number = new { type = "integer", description = "For 'preparation': alternative child line number to apply modifications to" },
                    expected_sku = new { type = "string", description = "Optional: Validate that the targeted child line matches this SKU before modifying" }
                },
                required = new[] { "product_sku", "customization_type", "customization_value" }
            }
        };

        private McpTool CreateApplyModificationsToLineTool() => new()
        {
            Name = "apply_modifications_to_line",
            Description = "Applies NRF structured modifications to a specific line using stable id or line number. Format: 'MOD_SKU:Description|MOD_SKU2:Description2'",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    line_item_id = new { type = "string", description = "Stable line item id (preferred)" },
                    line_number = new { type = "integer", description = "Alternative target by line number" },
                    expected_sku = new { type = "string", description = "Optional: Validate target line SKU before applying" },
                    modifications = new { type = "string", description = "Structured modifications to apply: 'MOD_SKU:Description|...'" }
                },
                required = new[] { "modifications" }
            }
        };

        private McpTool CreateModifyLineItemTool() => new()
        {
            Name = "modify_line_item",
            Description = "Modifies a specific line item using stable id or line number. Use when customer refers to specific items for changes.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    line_item_id = new { type = "string", description = "Stable id of the line to modify (preferred)" },
                    line_number = new { type = "integer", description = "Alternative line number to identify target when id is not available" },
                    expected_sku = new { type = "string", description = "Optional validation: SKU expected at the target" },
                    modification_type = new { type = "string", description = "Type of modification: 'drink_change', 'preparation_change', 'quantity_change', 'void_item'" },
                    new_value = new { type = "string", description = "New value for the modification (e.g., new drink name, preparation notes, quantity)" },
                    item_reference = new { type = "string", description = "Loose, conversational reference (not used for targeting)" }
                },
                required = new[] { "modification_type", "new_value" }
            }
        };

        private McpTool CreateGetTransactionTool() => new()
        {
            Name = "get_transaction",
            Description = "Gets the complete current transaction state including all line items with preparation notes and unique identifiers",
            Parameters = new { type = "object", properties = new { }, required = new string[] { } }
        };

        // Execute get_set_configuration - fail fast if not properly implemented
        private Task<string> ExecuteGetSetConfigurationAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            if (!toolCall.Arguments.TryGetValue("product_sku", out var skuEl))
            {
                return Task.FromResult("ERROR: product_sku parameter required for get_set_configuration");
            }

            var sku = skuEl.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(sku))
            {
                return Task.FromResult("ERROR: product_sku parameter is empty");
            }

            // ARCHITECTURAL FIX: Fail fast rather than providing misleading defaults
            return Task.FromResult($"DESIGN_DEFICIENCY: Set configuration for {sku} requires database integration. " +
                                 "The restaurant extension must implement GetSetDefinitionAsync, GetSetAvailableDrinksAsync, and GetSetAvailableSidesAsync methods " +
                                 "to query the actual set_definitions, set_available_drinks, and set_available_sides tables.");
        }

        private async Task<string> ExecuteAddItemAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            // Support either product_sku or item_description
            var productSku = "";
            var itemDescription = "";
            var quantity = 1;
            var preparationNotes = "";
            var confidence = 0.5;
            var context = "initial_order";

            if (toolCall.Arguments.TryGetValue("product_sku", out var skuElement))
            {
                productSku = skuElement.GetString() ?? "";
            }
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

            if (string.IsNullOrWhiteSpace(productSku) && string.IsNullOrWhiteSpace(itemDescription))
            {
                return "ERROR: Provide product_sku or item_description for add_item_to_transaction";
            }

            try
            {
                string? compositeTail = null;
                string searchQuery;

                if (!string.IsNullOrWhiteSpace(productSku))
                {
                    searchQuery = productSku.Trim();
                }
                else
                {
                    searchQuery = itemDescription;

                    // Detect composite phrasing like "<set> with <drink mods>" and split for better matching
                    var lowered = itemDescription.ToLowerInvariant();
                    var withIdx = lowered.IndexOf(" with ", StringComparison.Ordinal);
                    var wslashIdx = lowered.IndexOf(" w/", StringComparison.Ordinal);
                    var connectorIdx = withIdx >= 0 ? withIdx : wslashIdx;
                    if (connectorIdx >= 0)
                    {
                        var basePart = itemDescription.Substring(0, connectorIdx).Trim();
                        var tailPart = itemDescription.Substring(connectorIdx).Trim();
                        tailPart = Regex.Replace(tailPart, "^\\s*(with|w/)\\s+", string.Empty, RegexOptions.IgnoreCase);
                        if (!string.IsNullOrWhiteSpace(basePart) && !string.IsNullOrWhiteSpace(tailPart))
                        {
                            searchQuery = basePart;
                            compositeTail = tailPart;
                        }
                    }
                }

                // Search for the product (by SKU or description)
                var products = await SearchProductsInternalAsync(searchQuery, 3, cancellationToken);
                var productsList = products.ToList();
                if (!productsList.Any())
                {
                    return $"PRODUCT_NOT_FOUND: No products found matching '{searchQuery}'. Try different search terms.";
                }

                // Pick the best match
                var product = productsList[0];

                // Ensure transaction exists
                var transactionId = await EnsureTransactionAsync(cancellationToken);

                // Add with correct price
                var productPrice = (int)product.BasePriceCents / 100.0m;
                var extractedProductSku = (string)product.Sku;
                var extractedProductName = (string)product.Name;
                var extractedProductDescription = (string)product.Description;
                var extractedProductBasePriceCents = (int)product.BasePriceCents;

                _logger.LogInformation("Adding item {ProductSku} with price: {PriceCents} cents ({Decimal})", extractedProductSku, extractedProductBasePriceCents, productPrice);
                _logger.LogInformation("Product metadata: Name='{ProductName}', Description='{ProductDescription}'", extractedProductName, extractedProductDescription);

                var result = await _kernelClient.AddLineItemAsync(
                    _sessionId!, transactionId, extractedProductSku, quantity, productPrice, extractedProductName, extractedProductDescription, cancellationToken);

                if (!result.Success)
                {
                    throw new InvalidOperationException($"Failed to add item to transaction: {result.Error}");
                }

                var newlyAddedLineItem = result.LineItems?.Where(item => item.ProductId == extractedProductSku)
                                                          .OrderByDescending(item => item.LineNumber)
                                                          .FirstOrDefault();

                if (newlyAddedLineItem == null)
                {
                    _logger.LogError("❌ CRITICAL: Could not find newly added line item for {ProductSku} in transaction result", extractedProductSku);
                    return $"ITEM_ADDED: sku={extractedProductSku}; qty={quantity}; line_id=<unknown>; line_number=0";
                }

                var newlyAddedLineItemId = newlyAddedLineItem.LineItemId;
                var parentLineNumber = newlyAddedLineItem.LineNumber; // cache to avoid nullability warnings across awaits

                if (!string.IsNullOrEmpty(preparationNotes))
                {
                    if (string.IsNullOrEmpty(newlyAddedLineItemId))
                    {
                        _logger.LogWarning("⚠️  MISSING_LINE_ID: Newly added line for {ProductSku} has no LineItemId; cannot apply preparation notes.", extractedProductSku);
                    }
                    else
                    {
                        _logger.LogInformation("Processing preparation notes for {ProductSku} (line item {LineItemId}): '{Notes}'", extractedProductSku, newlyAddedLineItemId, preparationNotes);
                        await ProcessPreparationNotesAsModificationsAsync(transactionId, newlyAddedLineItemId, preparationNotes, cancellationToken);
                    }
                }

                // ARCHITECTURAL FIX: Use proper business attributes instead of hardcoded English assumptions
                bool isSetProduct = product.Specifications.ContainsKey("is_set") &&
                                   product.Specifications["is_set"].ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

                if (isSetProduct)
                {
                    if (!string.IsNullOrWhiteSpace(compositeTail))
                    {
                        try
                        {
                            // ARCHITECTURAL PRINCIPLE: AI handles ALL cultural understanding
                            // Use the raw input - AI will understand cultural context naturally
                            var drinkProducts = await _restaurantClient.SearchProductsAsync(compositeTail, 1, cancellationToken);
                            if (drinkProducts.Count == 0)
                            {
                                _logger.LogWarning("Could not find drink product for composite order tail: {Tail}", compositeTail);
                                return $"SET_ADDED: {extractedProductSku} added to transaction. Use get_set_configuration to determine customization options.";
                            }

                            var drinkProduct = drinkProducts[0];
                            var addDrink = await _kernelClient.AddChildLineItemAsync(_sessionId!, transactionId, drinkProduct.Sku, 1, 0.0m, parentLineNumber, drinkProduct.Name, drinkProduct.Description, cancellationToken);
                            if (!addDrink.Success)
                            {
                                _logger.LogError("❌ Failed to add drink to set: {Error}", addDrink.Error);
                                return $"SET_ADDED: {extractedProductSku} added to transaction. Use get_set_configuration to determine customization options.";
                            }

                            var txnAfterDrink = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                            if (!txnAfterDrink.Success)
                            {
                                return $"SET_ADDED: {extractedProductSku} added but failed to retrieve transaction for drink confirmation: {txnAfterDrink.Error}";
                            }

                            var drinkChild = txnAfterDrink.LineItems?
                                .Where(li => li.ParentLineNumber == parentLineNumber && li.ProductId.Equals(drinkProduct.Sku, StringComparison.OrdinalIgnoreCase))
                                .OrderByDescending(li => li.LineNumber)
                                .FirstOrDefault();

                            string? drinkChildId = drinkChild?.LineItemId;
                            int drinkChildNumber = drinkChild?.LineNumber ?? 0;

                            // ARCHITECTURAL PRINCIPLE: AI handles ALL cultural modifications through direct tool calls
                            // No interpretation logic - AI uses modify_line_item tool directly for cultural variants

                            return $"SET_UPDATED: {extractedProductSku} drink updated to {drinkProduct.Name}{(string.IsNullOrEmpty(drinkChildId) ? string.Empty : $". DRINK_LINE_ID={drinkChildId}, DRINK_LINE_NUMBER={drinkChildNumber}")}";
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process composite set-with-drink order for {Item}", itemDescription);
                            return $"SET_ADDED: {extractedProductSku} added to transaction. Use get_set_configuration to determine customization options.";
                        }
                    }

                    return $"SET_ADDED: {extractedProductSku} added to transaction. Use get_set_configuration to determine customization options.";
                }

                return $"ITEM_ADDED: sku={extractedProductSku}; qty={quantity}; line_id={newlyAddedLineItemId}; line_number={newlyAddedLineItem.LineNumber}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add item: {ItemOrSku}", string.IsNullOrWhiteSpace(productSku) ? itemDescription : productSku);
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Cannot add item without functional restaurant and kernel services. Error: {ex.Message}");
            }
        }

        private async Task ProcessPreparationNotesAsModificationsAsync(string transactionId, string parentLineItemId, string preparationNotes, CancellationToken cancellationToken)
        {
            var notes = preparationNotes.Trim();
            if (string.IsNullOrEmpty(notes))
            {
                return;
            }

            // Kernel tools require structured modification requests
            // Expected format: "MOD_SKU:Description|MOD_SKU2:Description2"
            var modificationRequests = ParseStructuredModifications(notes);
            if (!modificationRequests.Any())
            {
                _logger.LogWarning("⚠️  UNSTRUCTURED_PREPARATION: AI cashier sent unstructured preparation notes: '{Notes}'. " +
                                 "Expected format: 'MOD_SKU:Description|MOD_SKU2:Description2'", preparationNotes);
                return;
            }

            // Load current transaction once to resolve parent line number and existing children
            var txn = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
            if (!txn.Success)
            {
                _logger.LogError("❌ Failed to load transaction before applying modifications: {Error}", txn.Error);
                return;
            }

            var parent = txn.LineItems?.LastOrDefault(li => li.LineItemId == parentLineItemId);
            if (parent == null)
            {
                _logger.LogError("❌ Parent line not found for applying modifications: {ParentLineItemId}", parentLineItemId);
                return;
            }

            foreach ((string modSku, string description) in modificationRequests)
            {
                try
                {
                    // Prevent duplicate addition of the same modification under the same parent
                    var parentLineNumber = parent?.LineNumber ?? 0;
                    var alreadyExists = txn.LineItems?.Any(li => li.ParentLineNumber == parentLineNumber &&
                        !string.IsNullOrEmpty(li.ProductId) && li.ProductId.Equals(modSku, StringComparison.OrdinalIgnoreCase)) == true;
                    if (alreadyExists)
                    {
                        _logger.LogInformation("⏭️  SKIP_DUPLICATE_MODIFICATION: {ModSku} already present under parent line {ParentLine}",
                            modSku, parentLineNumber);
                        continue;
                    }

                    var modResult = await _kernelClient.AddModificationByLineItemIdAsync(
                        _sessionId ?? throw new InvalidOperationException("Session ID cannot be null"),
                        transactionId,
                        parentLineItemId,
                        modSku,
                        1,
                        0.0m,
                        cancellationToken);

                    if (modResult.Success)
                    {
                        _logger.LogInformation("✅ NRF_MODIFICATION: Added {ModSku} ({Description}) as child of line item {ParentLineItemId}",
                            modSku, description, parentLineItemId);

                        // Refresh transaction snapshot for subsequent dedup checks
                        txn = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                        parent = txn.LineItems?.LastOrDefault(li => li.LineItemId == parentLineItemId);
                    }
                    else
                    {
                        _logger.LogError("❌ Failed to add modification {ModSku}: {Error}", modSku, modResult.Error);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Exception adding modification {ModSku} to line item {ParentLineItemId}", modSku, parentLineItemId);
                }
            }

            if (modificationRequests.Any())
            {
                _logger.LogInformation("🎯 NRF_COMPLIANCE: Processed {Count} structured modifications: '{Notes}'",
                    modificationRequests.Count, preparationNotes);
            }
        }

        private List<(string ModSku, string Description)> ParseStructuredModifications(string modificationString)
        {
            var modifications = new List<(string ModSku, string Description)>();

            if (string.IsNullOrEmpty(modificationString))
            {
                return modifications;
            }

            var modificationPairs = modificationString.Split('|', StringSplitOptions.RemoveEmptyEntries);

            foreach (var pair in modificationPairs)
            {
                var parts = pair.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var modSku = parts[0].Trim();
                    var description = parts[1].Trim();

                    if (!string.IsNullOrEmpty(modSku) && !string.IsNullOrEmpty(description))
                    {
                        modifications.Add((modSku, description));
                        _logger.LogInformation("🔍 STRUCTURED_MOD_PARSED: {ModSku} → {Description}", modSku, description);
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️  MALFORMED_MODIFICATION: Could not parse '{Pair}' - expected format 'MOD_SKU:Description'", pair);
                }
            }

            return modifications;
        }

        private async Task<string> ExecuteModifyLineItemAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            // Prefer stable IDs; support line_number fallback
            if (!toolCall.Arguments.TryGetValue("modification_type", out var typeEl) ||
                !toolCall.Arguments.TryGetValue("new_value", out var valueEl))
            {
                return "ERROR: modification_type and new_value are required";
            }

            var lineItemId = toolCall.Arguments.TryGetValue("line_item_id", out var idEl) ? idEl.GetString() : null;
            var hasLineNumber = toolCall.Arguments.TryGetValue("line_number", out var lnEl);
            var lineNumber = hasLineNumber ? lnEl.GetInt32() : 0;
            var expectedSku = toolCall.Arguments.TryGetValue("expected_sku", out var skuEl) ? skuEl.GetString() : null;
            var modificationType = typeEl.GetString() ?? string.Empty;
            var newValue = valueEl.GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(modificationType))
            {
                return "ERROR: Invalid parameters";
            }

            var transactionId = await EnsureTransactionAsync(cancellationToken);
            var txn = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
            if (!txn.Success)
            {
                return $"ERROR: Failed to get transaction: {txn.Error}";
            }

            var target = !string.IsNullOrWhiteSpace(lineItemId)
                ? txn.LineItems?.LastOrDefault(li => li.LineItemId == lineItemId)
                : txn.LineItems?.LastOrDefault(li => li.LineNumber == lineNumber);

            if (target == null || string.IsNullOrEmpty(target.LineItemId))
            {
                return "ERROR: Target line not found";
            }

            if (!string.IsNullOrEmpty(expectedSku) && !target.ProductId.Equals(expectedSku, StringComparison.OrdinalIgnoreCase))
            {
                return $"ERROR: Expected SKU '{expectedSku}' at target, found '{target.ProductId}'";
            }

            var result = await _kernelClient.ModifyLineItemByIdAsync(
                _sessionId!, transactionId, target.LineItemId!, modificationType, newValue, cancellationToken);

            if (!result.Success)
            {
                return $"ERROR: {result.Error}";
            }

            return "LINE_ITEM_MODIFIED";
        }

        private async Task<object> ExecuteUpdateSetConfigurationAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            if (toolCall.Arguments.TryGetValue("product_sku", out var skuElement) &&
                toolCall.Arguments.TryGetValue("customization_type", out var typeElement) &&
                toolCall.Arguments.TryGetValue("customization_value", out var valueElement))
            {
                var sku = skuElement.GetString() ?? string.Empty;
                var customizationType = typeElement.GetString() ?? string.Empty;
                var customizationValue = valueElement.GetString() ?? string.Empty;

                // Optional targeting parameters
                var parentLineItemId = toolCall.Arguments.TryGetValue("target_parent_line_item_id", out var pidEl) ? pidEl.GetString() : null;
                var hasParentLineNumber = toolCall.Arguments.TryGetValue("target_parent_line_number", out var plnEl);
                var parentLineNumber = hasParentLineNumber ? plnEl.GetInt32() : 0;
                var expectedParentSku = toolCall.Arguments.TryGetValue("expected_parent_sku", out var epsEl) ? epsEl.GetString() : null;

                var targetLineItemId = toolCall.Arguments.TryGetValue("target_line_item_id", out var lidEl) ? lidEl.GetString() : null;
                var hasTargetLineNumber = toolCall.Arguments.TryGetValue("target_line_number", out var tlnEl);
                var targetLineNumber = hasTargetLineNumber ? tlnEl.GetInt32() : 0;
                var expectedSku = toolCall.Arguments.TryGetValue("expected_sku", out var esEl) ? esEl.GetString() : null;

                try
                {
                    var transactionId = await EnsureTransactionAsync(cancellationToken);

                    if (customizationType.Equals("drink", StringComparison.OrdinalIgnoreCase))
                    {
                        // Support embedded structured modifications in customization_value
                        string baseDrink;
                        string? embeddedMods = null;
                        var value = customizationValue ?? string.Empty;

                        // ARCHITECTURAL PRINCIPLE: AI handles ALL cultural understanding
                        // Use raw input - AI will provide structured format when needed

                        var modStart = value.IndexOf("MOD_", StringComparison.OrdinalIgnoreCase);
                        if (modStart > 0)
                        {
                            baseDrink = value.Substring(0, modStart).Trim();
                            embeddedMods = value.Substring(modStart).Trim();
                        }
                        else
                        {
                            baseDrink = value.Trim();
                            // AI provides proper structured format - no interpretation needed
                        }

                        var drinkProducts = await _restaurantClient.SearchProductsAsync(baseDrink, 1, cancellationToken);
                        if (drinkProducts.Count == 0)
                        {
                            _logger.LogWarning("Could not find drink product for: {DrinkName}", baseDrink);
                            return $"ERROR: Drink '{baseDrink}' not found";
                        }
                        var drinkProduct = drinkProducts[0];

                        // Resolve parent set target
                        var currentTransaction = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                        if (!currentTransaction.Success)
                        {
                            return $"ERROR: Failed to get current transaction: {currentTransaction.Error}";
                        }

                        var parent = !string.IsNullOrWhiteSpace(parentLineItemId)
                            ? currentTransaction.LineItems?.LastOrDefault(li => li.LineItemId == parentLineItemId)
                            : (parentLineNumber > 0
                                ? currentTransaction.LineItems?.LastOrDefault(li => li.LineNumber == parentLineNumber)
                                : currentTransaction.LineItems?.LastOrDefault(item => item.ProductId.Equals(sku, StringComparison.OrdinalIgnoreCase)));

                        // If an explicit parent id was provided but not found (e.g., placeholder like "<SET_LINE_ID>"),
                        // fall back to resolving by SKU before deciding the parent is missing.
                        if (parent == null && !string.IsNullOrWhiteSpace(parentLineItemId))
                        {
                            parent = currentTransaction.LineItems?.LastOrDefault(item => item.ProductId.Equals(sku, StringComparison.OrdinalIgnoreCase));
                        }

                        // ARCHITECTURAL FIX: If no parent set exists yet, add the set deterministically using SKU and catalog price,
                        // then proceed to apply the drink customization.
                        if (parent == null)
                        {
                            _logger.LogInformation("ℹ️  SET_PARENT_MISSING: No parent set line found for SKU {Sku}. Auto-adding set before applying drink customization.", sku);

                            var setCandidates = await _restaurantClient.SearchProductsAsync(sku, 1, cancellationToken);
                            var setProduct = setCandidates.FirstOrDefault();
                            if (setProduct == null)
                            {
                                return $"ERROR: Could not find set target for SKU {sku}";
                            }

                            var setPrice = setProduct.BasePriceCents / 100.0m;
                            var addSet = await _kernelClient.AddLineItemAsync(
                                _sessionId!, transactionId, setProduct.Sku, 1, setPrice, setProduct.Name, setProduct.Description, cancellationToken);
                            if (!addSet.Success)
                            {
                                return $"ERROR: Failed to add set '{setProduct.Sku}' to transaction: {addSet.Error}";
                            }

                            currentTransaction = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                            if (!currentTransaction.Success)
                            {
                                return $"ERROR: Failed to get current transaction after adding set: {currentTransaction.Error}";
                            }

                            parent = currentTransaction.LineItems?.LastOrDefault(item => item.ProductId.Equals(sku, StringComparison.OrdinalIgnoreCase));
                        }

                        if (parent == null)
                        {
                            return $"ERROR: Could not find set target for SKU {sku}";
                        }

                        if (!string.IsNullOrEmpty(expectedParentSku) && !parent.ProductId.Equals(expectedParentSku, StringComparison.OrdinalIgnoreCase))
                        {
                            return $"ERROR: Expected parent SKU '{expectedParentSku}', found '{parent.ProductId}'";
                        }

                        var addDrink = await _kernelClient.AddChildLineItemAsync(_sessionId!, transactionId, drinkProduct.Sku, 1, 0.0m, parent.LineNumber, drinkProduct.Name, drinkProduct.Description, cancellationToken);
                        if (!addDrink.Success)
                        {
                            return $"ERROR: Failed to add drink to set: {addDrink.Error}";
                        }

                        // Resolve the newly added drink child
                        var txnAfterDrink = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                        if (!txnAfterDrink.Success)
                        {
                            return $"ERROR: Failed to retrieve transaction after adding drink: {txnAfterDrink.Error}";
                        }

                        var drinkChild = txnAfterDrink.LineItems?
                            .Where(li => li.ParentLineNumber == parentLineNumber && li.ProductId.Equals(drinkProduct.Sku, StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(li => li.LineNumber)
                            .FirstOrDefault();

                        string? drinkChildId = drinkChild?.LineItemId;
                        int drinkChildNumber = drinkChild?.LineNumber ?? 0;

                        // Attach embedded or inferred modifications to the drink child
                        if (!string.IsNullOrWhiteSpace(embeddedMods) && !string.IsNullOrEmpty(drinkChildId))
                        {
                            var structuredMods = ParseStructuredModifications(embeddedMods);
                            foreach (var (modSku, desc) in structuredMods)
                            {
                                // Deduplicate: skip if the same modification already exists under this drink child
                                var duplicate = txnAfterDrink.LineItems?.Any(li => li.ParentLineNumber == drinkChildNumber && li.ProductId.Equals(modSku, StringComparison.OrdinalIgnoreCase)) == true;
                                if (duplicate)
                                {
                                    _logger.LogInformation("⏭️  SKIP_DUPLICATE_MODIFICATION: {ModSku} already exists under drink line {DrinkLine}", modSku, drinkChildNumber);
                                    continue;
                                }

                                var modAdd = await _kernelClient.AddModificationByLineItemIdAsync(_sessionId!, transactionId, drinkChildId!, modSku, 1, 0.0m, cancellationToken);
                                if (!modAdd.Success)
                                {
                                    _logger.LogError("❌ Failed to add drink modification {ModSku}: {Error}", modSku, modAdd.Error);
                                }
                                else
                                {
                                    // Refresh txn snapshot for subsequent checks within the same loop
                                    txnAfterDrink = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                                }
                            }
                        }

                        return $"SET_UPDATED: {sku} drink updated to {drinkProduct.Name}{(string.IsNullOrEmpty(drinkChildId) ? string.Empty : $". DRINK_LINE_ID={drinkChildId}, DRINK_LINE_NUMBER={drinkChildNumber}")}";
                    }
                    else if (customizationType.Equals("preparation", StringComparison.OrdinalIgnoreCase))
                    {
                        var currentTransaction = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                        if (!currentTransaction.Success)
                        {
                            return $"ERROR: Failed to get current transaction: {currentTransaction.Error}";
                        }

                        // Resolve target child line by id, number, or fallback by SKU (last occurrence)
                        var target = !string.IsNullOrWhiteSpace(targetLineItemId)
                            ? currentTransaction.LineItems?.LastOrDefault(li => li.LineItemId == targetLineItemId)
                            : (targetLineNumber > 0
                                ? currentTransaction.LineItems?.LastOrDefault(li => li.LineNumber == targetLineNumber)
                                : currentTransaction.LineItems?.LastOrDefault(li => li.ProductId.Equals(sku, StringComparison.OrdinalIgnoreCase)));

                        if (target == null || string.IsNullOrEmpty(target.LineItemId))
                        {
                            return "ERROR: Target line for preparation not found";
                        }

                        if (!string.IsNullOrEmpty(expectedSku) && !target.ProductId.Equals(expectedSku, StringComparison.OrdinalIgnoreCase))
                        {
                            return $"ERROR: Expected SKU '{expectedSku}' at target, found '{target.ProductId}'";
                        }

                        var structured = ParseStructuredModifications(customizationValue);
                        if (!structured.Any())
                        {
                            return $"ERROR: Unstructured preparation: '{customizationValue}'";
                        }

                        var applied = 0;
                        foreach (var (modSku, desc) in structured)
                        {
                            // Prevent duplicate modification lines under the same parent
                            var exists = currentTransaction.LineItems?.Any(li => li.ParentLineNumber == target.LineNumber && li.ProductId.Equals(modSku, StringComparison.OrdinalIgnoreCase)) == true;
                            if (exists)
                            {
                                _logger.LogInformation("⏭️  SKIP_DUPLICATE_MODIFICATION: {ModSku} already exists under line {Line}", modSku, target.LineNumber);
                                continue;
                            }

                            var modResult = await _kernelClient.AddModificationByLineItemIdAsync(
                                _sessionId!, transactionId, target.LineItemId!, modSku, 1, 0.0m, cancellationToken);
                            if (modResult.Success)
                            {
                                applied++;
                                // Refresh snapshot for subsequent checks
                                currentTransaction = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                            }
                        }

                        return $"LINE_UPDATED: {target.LineItemId} {applied} preparation modifications applied";
                    }

                    _logger.LogInformation("Processing other customization type: {Type} = {Value} for {Sku}", customizationType, customizationValue, sku);
                    return await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
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

            if (toolCall.Arguments.TryGetValue("search_term", out var searchTermElement))
            {
                searchTerm = searchTermElement.GetString() ?? "";
            }
            if (toolCall.Arguments.TryGetValue("max_results", out var maxResultsElement))
            {
                maxResults = maxResultsElement.GetInt32();
            }

            if (string.IsNullOrEmpty(searchTerm))
            {
                return "ERROR: search_term parameter is required for search_products";
            }

            try
            {
                // ARCHITECTURAL FIX: Use appropriate client based on what's available
                if (_httpRestaurantClient != null)
                {
                    var products = await _httpRestaurantClient.SearchProductsAsync(searchTerm, maxResults);

                    if (products.Count == 0)
                    {
                        return $"PRODUCT_NOT_FOUND: No products found matching '{searchTerm}'. Try different search terms.";
                    }

                    var resultBuilder = new StringBuilder();
                    resultBuilder.AppendLine($"FOUND_PRODUCTS: {products.Count} products found for '{searchTerm}':");

                    foreach (var product in products)
                    {
                        resultBuilder.AppendLine($"- {product.Id}: {product.Name} ({FormatCurrency(product.Price)})");
                    }

                    return resultBuilder.ToString();
                }
                else if (_restaurantClient != null)
                {
                    var products = await _restaurantClient.SearchProductsAsync(searchTerm, maxResults, cancellationToken);

                    if (products.Count == 0)
                    {
                        return $"PRODUCT_NOT_FOUND: No products found matching '{searchTerm}'. Try different search terms.";
                    }

                    var resultBuilder = new StringBuilder();
                    resultBuilder.AppendLine($"FOUND_PRODUCTS: {products.Count} products found for '{searchTerm}':");

                    foreach (var product in products)
                    {
                        resultBuilder.AppendLine($"- {product.Sku}: {product.Name} ({FormatCurrency(product.BasePriceCents / 100.0m)})");
                    }

                    return resultBuilder.ToString();
                }
                else
                {
                    throw new InvalidOperationException(
                        "DESIGN DEFICIENCY: No restaurant client available. " +
                        "Either HttpRestaurantExtensionClient or RestaurantExtensionClient must be configured.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products");
                return "ERROR: Failed to search products";
            }
        }

        private async Task<string> ExecuteGetPopularItemsAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            var count = 5;
            if (toolCall.Arguments.TryGetValue("count", out var countElement))
            {
                count = countElement.GetInt32();
            }

            try
            {
                var products = await _restaurantClient.GetPopularItemsAsync(cancellationToken);

                var results = new StringBuilder();
                results.AppendLine($"POPULAR_ITEMS: {products.Count} popular items:");
                results.AppendLine();

                foreach (var product in products.Take(count))
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
            var includeCategories = true;
            if (toolCall.Arguments.TryGetValue("include_categories", out var catElement))
            {
                includeCategories = catElement.GetBoolean();
            }

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
                    var categoryProducts = allProducts.Where(p => p.CategoryName?.Equals(category, StringComparison.OrdinalIgnoreCase) == true);

                    foreach (var product in categoryProducts)
                    {
                        // ARCHITECTURAL PRINCIPLE: Provide complete product context for AI decisions
                        var price = product.BasePriceCents > 0 ? $" ({product.BasePriceCents / 100.0m} currency units)" : "";
                        var description = !string.IsNullOrEmpty(product.Description) ? $" - {product.Description}" : "";

                        results.AppendLine($"- **{product.Sku}**: {product.Name}{price}{description}");
                    }
                    results.AppendLine(); // Empty line between categories
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
                var result = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);

                if (!result.Success)
                {
                    throw new InvalidOperationException($"Failed to get transaction total: {result.Error}");
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

        private Task<string> ExecuteLoadPaymentMethodsContextAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            if (_storeConfig?.PaymentMethods == null)
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: Store configuration does not include payment methods. " +
                    "Client cannot decide payment methods. " +
                    "Ensure store.config has a [PaymentMethods] section.");
            }

            var results = new StringBuilder();
            results.AppendLine("PAYMENT_METHODS_CONTEXT: Available payment methods:");

            var methods = _storeConfig.PaymentMethods.AcceptedMethods;
            if (methods != null)
            {
                foreach (var method in methods.Where(m => m.IsEnabled))
                {
                    results.AppendLine($"- {method.DisplayName} ({method.MethodId})");
                }
            }

            if (!string.IsNullOrEmpty(_storeConfig.PaymentMethods.PaymentInstructions))
            {
                results.AppendLine($"Instructions: {_storeConfig.PaymentMethods.PaymentInstructions}");
            }

            return Task.FromResult(results.ToString());
        }

        private async Task<string> ExecuteProcessPaymentAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            var paymentMethod = "";
            var amount = 0.0m;

            if (toolCall.Arguments.TryGetValue("payment_method", out var methodElement))
            {
                paymentMethod = methodElement.GetString() ?? "";
            }
            if (toolCall.Arguments.TryGetValue("amount", out var amountElement))
            {
                amount = (decimal)amountElement.GetDouble();
            }

            try
            {
                var transactionId = await EnsureTransactionAsync(cancellationToken);

                // If amount not specified, use transaction total
                if (amount <= 0)
                {
                    var txn = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                    if (txn.Success)
                    {
                        amount = txn.Total;
                    }
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
                    return $"PAYMENT_FAILED: {result.Error}";
                }

                return $"PAYMENT_PROCESSED: {FormatCurrency(amount)} via {paymentMethod}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process payment");
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Cannot process payment without functional kernel service. " +
                    $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Gets structured transaction data directly from kernel (no string parsing)
        /// This method returns the raw kernel transaction for AI layer synchronization.
        /// </summary>
        public async Task<TransactionClientResult?> GetStructuredTransactionAsync()
        {
            try
            {
                await EnsureSessionAsync();
                var transactionId = await EnsureTransactionAsync();

                var result = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId);

                if (!result.Success)
                {
                    _logger.LogWarning("Failed to get structured transaction: {Error}", result.Error);
                    return null;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get structured transaction from kernel");
                return null;
            }
        }

        private async Task<string> ExecuteGetTransactionAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Ensure at least a session is connected; do not silently start a transaction here
                await EnsureSessionAsync(cancellationToken);

                if (string.IsNullOrEmpty(_currentTransactionId))
                {
                    return "NO_TRANSACTION: No active transaction";
                }

                var result = await _kernelClient.GetTransactionAsync(_sessionId!, _currentTransactionId!, cancellationToken);

                if (!result.Success)
                {
                    return $"Error getting transaction: {result.Error}";
                }

                // ARCHITECTURAL PRINCIPLE: Return comprehensive LLM-optimized transaction data
                return await FormatTransactionForAI(result, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get transaction");
                return $"DESIGN_DEFICIENCY: Cannot get transaction without functional kernel service. Error: {ex.Message}";
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

            if (_currencyFormatter == null)
            {
                throw new InvalidOperationException("DESIGN DEFICIENCY: ICurrencyFormattingService not provided to KernelPosToolsProvider.");
            }
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Hydrate store configuration from kernel at runtime
        /// </summary>
        private async Task HydrateStoreConfigAsync(CancellationToken cancellationToken)
        {
            try
            {
                var storeConfigObj = await _kernelClient.GetStoreConfigAsync(cancellationToken);
                if (storeConfigObj is StoreConfig sc)
                {
                    _storeConfig = sc;
                    _logger.LogInformation("🔄 Updated store configuration from kernel: {StoreName} ({Currency})",
                        _storeConfig.StoreName, _storeConfig.Currency);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to hydrate store config from kernel");
            }
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
                _restaurantClient?.Dispose();
                _sessionSemaphore?.Dispose(); // ARCHITECTURAL FIX: Clean up semaphore
            }

            // Dispose unmanaged resources here if any

            _disposed = true;
        }

        /// <summary>
        /// Resolves a human-friendly display name for a SKU using the restaurant extension as the source of truth.
        /// Falls back to standardized formatting for MOD_* SKUs when the catalog does not expose a record.
        /// ARCHITECTURAL PRINCIPLE: Prefer user-space store data; never hardcode business content.
        /// </summary>
        public async Task<string?> ResolveDisplayNameAsync(string sku, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sku))
                {
                    return null;
                }

                if (_restaurantClient != null)
                {
                    // Query by SKU directly
                    var results = await _restaurantClient.SearchProductsAsync(sku, 1, cancellationToken);
                    var product = results.FirstOrDefault(p => p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));
                    if (product != null && !string.IsNullOrWhiteSpace(product.Name))
                    {
                        return product.Name;
                    }
                }

                // Fallback: Standardized formatting for MOD_* identifiers
                var formatted = FormatDisplayNameFromSku(sku);
                return formatted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve display name for SKU {Sku}", sku);
                return null;
            }
        }

        private static string? FormatDisplayNameFromSku(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku))
            {
                return null;
            }

            if (sku.StartsWith("MOD_", StringComparison.OrdinalIgnoreCase))
            {
                // Convert standardized modification code to friendly text (e.g., MOD_NO_SUGAR -> No Sugar)
                var core = sku.Substring(4);
                core = core.Replace('_', ' ').ToLowerInvariant();
                // Title-case words
                var parts = core.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    var p = parts[i];
                    parts[i] = p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1) : string.Empty);
                }
                return string.Join(' ', parts);
            }

            return null;
        }

        private async Task<string> ExecuteApplyModificationsToLineAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            var modifications = toolCall.Arguments.TryGetValue("modifications", out var modsEl) ? modsEl.GetString() : null;
            var lineItemId = toolCall.Arguments.TryGetValue("line_item_id", out var idEl) ? idEl.GetString() : null;
            var hasLineNumber = toolCall.Arguments.TryGetValue("line_number", out var numEl);
            var lineNumber = hasLineNumber ? numEl.GetInt32() : 0;
            var expectedSku = toolCall.Arguments.TryGetValue("expected_sku", out var skuEl) ? skuEl.GetString() : null;

            if (string.IsNullOrWhiteSpace(modifications))
            {
                return "ERROR: modifications parameter is required";
            }

            var transactionId = await EnsureTransactionAsync(cancellationToken);
            var txn = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
            if (!txn.Success)
            {
                return $"ERROR: Failed to get transaction: {txn.Error}";
            }

            var target = !string.IsNullOrWhiteSpace(lineItemId)
                ? txn.LineItems?.LastOrDefault(li => li.LineItemId == lineItemId)
                : txn.LineItems?.LastOrDefault(li => li.LineNumber == lineNumber);

            if (target == null || string.IsNullOrEmpty(target.LineItemId))
            {
                return "ERROR: Target line not found";
            }

            if (!string.IsNullOrEmpty(expectedSku) && !target.ProductId.Equals(expectedSku, StringComparison.OrdinalIgnoreCase))
            {
                return $"ERROR: Expected SKU '{expectedSku}' at target, found '{target.ProductId}'";
            }

            var parsed = ParseStructuredModifications(modifications);
            if (!parsed.Any())
            {
                return $"ERROR: Unstructured modifications: '{modifications}'";
            }

            var applied = 0;
            foreach (var (modSku, desc) in parsed)
            {
                var exists = txn.LineItems?.Any(li => li.ParentLineNumber == target.LineNumber && li.ProductId.Equals(modSku, StringComparison.OrdinalIgnoreCase)) == true;
                if (exists)
                {
                    _logger.LogInformation("⏭️  SKIP_DUPLICATE_MODIFICATION: {ModSku} already exists under line {Line}", modSku, target.LineNumber);
                    continue;
                }

                var modResult = await _kernelClient.AddModificationByLineItemIdAsync(
                    _sessionId!, transactionId, target.LineItemId!, modSku, 1, 0.0m, cancellationToken);
                if (modResult.Success)
                {
                    applied++;
                    // Refresh snapshot for subsequent checks
                    txn = await _kernelClient.GetTransactionAsync(_sessionId!, transactionId, cancellationToken);
                }
                else
                {
                    _logger.LogError("❌ Failed to add modification {ModSku} to line {LineId}: {Error}", modSku, target.LineItemId, modResult.Error);
                }
            }

            return $"LINE_UPDATED: {target.LineItemId} {applied} modifications applied";
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Format complete transaction data for LLM consumption
        /// Includes ALL properties from kernel + restaurant extension product data
        /// </summary>
        private async Task<string> FormatTransactionForAI(TransactionClientResult transaction, CancellationToken cancellationToken)
        {
            var result = new StringBuilder();

            // Transaction-level properties (use reflection to get ALL properties)
            result.AppendLine("=== TRANSACTION DATA ===");
            result.AppendLine($"Transaction_ID: {transaction.TransactionId}");
            result.AppendLine($"Session_ID: {transaction.SessionId}");
            result.AppendLine($"Transaction_State: {transaction.State}");
            result.AppendLine($"Transaction_Success: {transaction.Success}");
            result.AppendLine($"Transaction_Total: {FormatCurrency(transaction.Total)}");
            result.AppendLine($"Transaction_Item_Count: {transaction.LineItems.Count}");
            result.AppendLine($"Transaction_Error: {transaction.Error ?? "None"}");

            // Additional properties using reflection
            var transactionProperties = typeof(TransactionClientResult).GetProperties()
                .Where(p => p.Name != nameof(transaction.LineItems) && // Handle separately
                           p.Name != nameof(transaction.Data)) // Handle separately
                .Where(p => p.CanRead);

            foreach (var prop in transactionProperties)
            {
                try
                {
                    var value = prop.GetValue(transaction);
                    var formattedValue = prop.Name.Contains("Total") && value is decimal decVal ? FormatCurrency(decVal) : value?.ToString() ?? "null";
                    result.AppendLine($"Transaction_{prop.Name}: {formattedValue}");
                }
                catch (Exception ex)
                {
                    result.AppendLine($"Transaction_{prop.Name}: Error reading ({ex.Message})");
                }
            }

            result.AppendLine();

            // Line items with comprehensive data including restaurant extension info
            if (transaction.LineItems.Any())
            {
                result.AppendLine("=== LINE ITEMS ===");

                for (int i = 0; i < transaction.LineItems.Count; i++)
                {
                    var lineItem = transaction.LineItems[i];
                    result.AppendLine($"--- LINE_ITEM_{i + 1} ---");

                    // Core line item properties using reflection
                    var lineItemProperties = typeof(TransactionLineItem).GetProperties().Where(p => p.CanRead);
                    foreach (var prop in lineItemProperties)
                    {
                        try
                        {
                            var value = prop.GetValue(lineItem);
                            var formattedValue = value;

                            // Special formatting for different property types
                            if (prop.Name.Contains("Price") && value is decimal priceVal)
                            {
                                formattedValue = FormatCurrency(priceVal);
                            }
                            else if (prop.Name.Contains("Number") && value != null)
                            {
                                formattedValue = value.ToString();
                            }
                            else
                            {
                                formattedValue = value?.ToString() ?? "null";
                            }

                            result.AppendLine($"  {prop.Name}: {formattedValue}");
                        }
                        catch (Exception ex)
                        {
                            result.AppendLine($"  {prop.Name}: Error reading ({ex.Message})");
                        }
                    }

                    // Enhanced product information from restaurant extension
                    try
                    {
                        var productInfo = await GetEnhancedProductInfo(lineItem.ProductId, cancellationToken);
                        if (productInfo != null)
                        {
                            result.AppendLine($"  Enhanced_Product_Name: {productInfo.Name}");
                            result.AppendLine($"  Enhanced_Product_Description: {productInfo.Description}");
                            result.AppendLine($"  Enhanced_Product_Category: {productInfo.CategoryName}");
                            result.AppendLine($"  Enhanced_Product_Base_Price: {FormatCurrency(productInfo.BasePrice)}");
                            result.AppendLine($"  Enhanced_Product_Is_Active: {productInfo.IsActive}");

                            // Additional restaurant extension properties using reflection
                            var productProperties = productInfo.GetType().GetProperties()
                                .Where(p => p.CanRead)
                                .Where(p => !new[] { "Sku", "Name", "Description", "CategoryName", "BasePrice", "BasePriceCents", "IsActive" }.Contains(p.Name));                            foreach (var prop in productProperties)
                            {
                                try
                                {
                                    var value = prop.GetValue(productInfo);
                                    result.AppendLine($"  Enhanced_Product_{prop.Name}: {value?.ToString() ?? "null"}");
                                }
                                catch (Exception ex)
                                {
                                    result.AppendLine($"  Enhanced_Product_{prop.Name}: Error reading ({ex.Message})");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.AppendLine($"  Enhanced_Product_Info: Error retrieving ({ex.Message})");
                    }

                    result.AppendLine();
                }
            }
            else
            {
                result.AppendLine("=== LINE ITEMS ===");
                result.AppendLine("No line items in transaction");
                result.AppendLine();
            }

            // Additional transaction data if present
            if (transaction.Data != null)
            {
                result.AppendLine("=== ADDITIONAL DATA ===");
                try
                {
                    result.AppendLine($"Additional_Data: {transaction.Data}");
                }
                catch (Exception ex)
                {
                    result.AppendLine($"Additional_Data: Error reading ({ex.Message})");
                }
                result.AppendLine();
            }

            result.AppendLine("=== END TRANSACTION DATA ===");
            return result.ToString();
        }

        /// <summary>
        /// Get enhanced product information from restaurant extension
        /// </summary>
        private async Task<PosKernel.Extensions.Restaurant.ProductInfo?> GetEnhancedProductInfo(string productId, CancellationToken cancellationToken)
        {
            try
            {
                if (_restaurantClient != null)
                {
                    var validationResult = await _restaurantClient.ValidateProductAsync(productId, cancellationToken: cancellationToken);
                    return validationResult.ProductInfo;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get enhanced product info for {ProductId}", productId);
            }
            return null;
        }
    }
}
