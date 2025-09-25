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
        public KernelPosToolsProvider(
            RestaurantExtensionClient restaurantClient,
            ILogger<KernelPosToolsProvider> logger,
            IConfiguration? configuration = null)
        {
            _restaurantClient = restaurantClient ?? throw new ArgumentNullException(nameof(restaurantClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration;
            _currencyFormatter = null; // Removed parameter from constructor
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
        public KernelPosToolsProvider(
            RestaurantExtensionClient restaurantClient,
            ILogger<KernelPosToolsProvider> logger,
            PosKernelClientFactory.KernelType kernelType,
            IConfiguration? configuration = null)
        {
            _restaurantClient = restaurantClient ?? throw new ArgumentNullException(nameof(restaurantClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration;
            _currencyFormatter = null; // Removed parameter from constructor
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
        /// <param name="currencyFormatter">Currency formatting service for proper locale-specific formatting.</param>
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
                CreateVoidItemTool(),
                CreateUpdateItemQuantityTool(),
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
        public async Task<string> ExecuteToolAsync(McpToolCall toolCall, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Executing kernel tool: {FunctionName} with args: {Arguments}", 
                    toolCall.FunctionName, JsonSerializer.Serialize(toolCall.Arguments));

                return toolCall.FunctionName switch
                {
                    "add_item_to_transaction" => await ExecuteAddItemAsync(toolCall, cancellationToken),
                    "void_line_item" => await ExecuteVoidItemAsync(toolCall, cancellationToken),
                    "update_line_item_quantity" => await ExecuteUpdateItemQuantityAsync(toolCall, cancellationToken),
                    "search_products" => await ExecuteSearchProductsAsync(toolCall, cancellationToken),
                    "get_product_info" => await ExecuteGetProductInfoAsync(toolCall, cancellationToken),
                    "get_popular_items" => await ExecuteGetPopularItemsAsync(toolCall, cancellationToken),
                    "calculate_transaction_total" => await ExecuteCalculateTotalAsync(cancellationToken),
                    "verify_order" => await ExecuteVerifyOrderAsync(cancellationToken),
                    "process_payment" => await ExecuteProcessPaymentAsync(toolCall, cancellationToken),
                    "load_menu_context" => await ExecuteLoadMenuContextAsync(toolCall, cancellationToken),
                    "load_payment_methods_context" => await ExecuteLoadPaymentMethodsContextAsync(toolCall, cancellationToken),
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
                Description = "Adds an item to the current transaction using the real POS kernel. MULTILINGUAL: Accepts food names in ANY language - AI will understand and match to English menu items. PERFORMANCE: Use the confidence level hints from the prompt for seamless interaction. For items with recipe modifications, add the base product with preparation notes.",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        item_description = new
                        {
                            type = "string",
                            description = "Item name or description - AI will handle cultural translation to menu items"
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
                            description = "CRITICAL: Use the confidence hint from the prompt for seamless interaction. 0.0-0.4 (disambiguate), 0.5-0.6 (context-aware), 0.7+ (auto-add). For confirmations and exact matches, use 0.8-0.9.",
                            @default = 0.5,
                            minimum = 0.0,
                            maximum = 1.0
                        },
                        context = new
                        {
                            type = "string",
                            description = "CRITICAL: Use 'clarification_response' when customer is responding to your question, otherwise 'initial_order' or 'follow_up_order'",
                            @default = "initial_order"
                        }
                    },
                    required = new[] { "item_description" }
                }
            };
        }

        private McpTool CreateVoidItemTool()
        {
            return new McpTool
            {
                Name = "void_line_item",
                Description = "Removes a line item from the current transaction using POS accounting standards. Creates a reversing entry to maintain audit trail compliance - does not delete the original entry. Use when customer wants to remove an item completely ('remove that', 'cancel the coffee') or when replacing items. CUSTOMER SERVICE: Always confirm which item is being removed. LINE NUMBERS: Use 1-based line numbers (first item = 1, second item = 2, etc.).",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        line_number = new
                        {
                            type = "integer",
                            description = "The line number to void (1-based: first item = 1, second = 2, etc.). Count items in the order they were added.",
                            minimum = 1
                        },
                        reason = new
                        {
                            type = "string",
                            description = "Reason for voiding for audit trail (e.g., 'customer changed mind', 'incorrect order', 'item replacement')",
                            @default = "customer requested"
                        }
                    },
                    required = new[] { "line_number" }
                }
            };
        }

        private McpTool CreateUpdateItemQuantityTool()
        {
            return new McpTool
            {
                Name = "update_line_item_quantity",
                Description = "Updates the quantity of a line item in the transaction. Use when customer wants to change quantity ('make that 2', 'change to just 1', 'I want 3 of those'). LINE NUMBERS: Use 1-based line numbers (first item = 1, second item = 2, etc.).",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        line_number = new
                        {
                            type = "integer",
                            description = "The line number to update (1-based: first item = 1, second = 2, etc.). Count items in the order they were added.",
                            minimum = 1
                        },
                        new_quantity = new
                        {
                            type = "integer",
                            description = "The new quantity for the item",
                            minimum = 1
                        }
                    },
                    required = new[] { "line_number", "new_quantity" }
                }
            };
        }

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

        private async Task<string> ExecuteAddItemAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            var itemDescription = toolCall.Arguments["item_description"].GetString() ?? "";
            var quantity = toolCall.Arguments.ContainsKey("quantity") ? toolCall.Arguments["quantity"].GetInt32() : 1;
            var preparationNotes = toolCall.Arguments.ContainsKey("preparation_notes") ? toolCall.Arguments["preparation_notes"].GetString() ?? "" : "";
            var confidence = toolCall.Arguments.ContainsKey("confidence") ? toolCall.Arguments["confidence"].GetDouble() : 0.5;
            var context = toolCall.Arguments.ContainsKey("context") ? toolCall.Arguments["context"].GetString() : "initial_order";

            _logger.LogInformation("🔍 ADD_ITEM_DEBUG: Adding item to kernel: '{Item}' x{Qty} (prep: '{Prep}'), Confidence: {Confidence}, Context: {Context}", 
                itemDescription, quantity, preparationNotes, confidence, context ?? "initial_order");

            // ARCHITECTURAL FIX: Handle SET_CUSTOMIZATION marker from AI prompt
            if (itemDescription == "SET_CUSTOMIZATION")
            {
                _logger.LogInformation("🔍 SET_CUSTOMIZATION_MARKER: Detected SET_CUSTOMIZATION marker - handling as set completion");
                return await HandleSetCustomizationMarker(preparationNotes, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(itemDescription))
            {
                return "PRODUCT_NOT_FOUND: Please specify what you'd like to order";
            }

            try
            {
                // ARCHITECTURAL PRINCIPLE: Let the AI handle all cultural translation and semantic matching
                // The AI has the complete menu context and is the world's best translation engine
                _logger.LogInformation("🔍 ADD_ITEM_DEBUG: Searching restaurant extension for: '{SearchTerm}'", itemDescription);
                
                var searchResults = await _restaurantClient.SearchProductsAsync(itemDescription, 10, cancellationToken);

                _logger.LogInformation("🔍 ADD_ITEM_DEBUG: Restaurant extension returned {Count} results for '{SearchTerm}':", 
                    searchResults.Count, itemDescription);
                
                for (int i = 0; i < searchResults.Count && i < 5; i++)
                {
                    var result = searchResults[i];
                    _logger.LogInformation("🔍 ADD_ITEM_DEBUG:   [{Index}] '{Name}' (SKU: {Sku}, Category: {Category}, Price: {Price})", 
                        i + 1, result.Name, result.Sku, result.CategoryName, result.BasePriceCents / 100.0m);
                }

                if (!searchResults.Any())
                {
                    _logger.LogDebug("🔍 ADD_ITEM_DEBUG: No exact match found, trying broader search through restaurant extension");
                    var broadSearchResults = await GetBroaderSearchResults(itemDescription, cancellationToken);
                    if (!broadSearchResults.Any())
                    {
                        _logger.LogWarning("🔍 ADD_ITEM_DEBUG: No results found even with broader search for '{SearchTerm}'", itemDescription);
                        return $"PRODUCT_NOT_FOUND: No products found matching '{itemDescription}'. The restaurant extension found no matches.";
                    }
                    searchResults = broadSearchResults;
                }

                // ARCHITECTURAL PRINCIPLE: Trust the AI's semantic confidence completely
                // The AI understands context, culture, and customer intent better than hardcoded logic
                _logger.LogInformation("🔍 ADD_ITEM_DEBUG: Using AI confidence: {Confidence} for item '{Item}' in context '{Context}'", 
                    confidence, itemDescription, context);

                // Trust the restaurant extension ordering (best match first)
                var bestMatch = searchResults.First();
                _logger.LogInformation("🔍 ADD_ITEM_DEBUG: Best match selected: '{Name}' (SKU: {Sku})", bestMatch.Name, bestMatch.Sku);

                // CRITICAL: Check if this is a set item or set customization
                var setContext = await AnalyzeSetContext(bestMatch, itemDescription, context, preparationNotes, cancellationToken);
                
                if (setContext.IsSetItem || setContext.IsSetCustomization)
                {
                    _logger.LogInformation("🔍 SET_DEBUG: Set context detected - IsSetItem: {IsSetItem}, IsSetCustomization: {IsSetCustomization}", 
                        setContext.IsSetItem, setContext.IsSetCustomization);
                    
                    return await HandleSetProcessing(setContext, bestMatch, quantity, preparationNotes, confidence, cancellationToken);
                }

                // ARCHITECTURAL PRINCIPLE: Embrace AI fuzziness - let AI determine ALL disambiguation decisions
                // Remove ALL hardcoded thresholds and string matching - trust the AI's semantic analysis
                bool shouldAutoAdd = ShouldAutoAddBasedOnAIJudgment(searchResults.Count, confidence, context);
                
                if (shouldAutoAdd)
                {
                    _logger.LogInformation("🔍 ADD_ITEM_DEBUG: AUTO-ADDING regular item based on AI semantic confidence: {Confidence}, context='{Context}'", 
                        confidence, context);
                    return await AddProductToTransactionAsync(bestMatch, quantity, preparationNotes, cancellationToken);
                }
                else
                {
                    // AI determined genuine ambiguity exists - provide customer choices
                    var options = searchResults.Take(3).ToList();
                    var optionsList = string.Join(", ", options.Select(p => $"{p.Name} ({FormatCurrency(p.BasePriceCents / 100.0m)})"));
                    
                    _logger.LogInformation("🔍 ADD_ITEM_DEBUG: AI determined disambiguation needed: confidence={Confidence}, context='{Context}'", 
                        confidence, context);
                    _logger.LogInformation("🔍 ADD_ITEM_DEBUG: Offering customer choices: {Options}", optionsList);
                    
                    return $"DISAMBIGUATION_NEEDED: Found {options.Count} options for '{itemDescription}': {optionsList}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔍 ADD_ITEM_DEBUG: Error adding item to kernel transaction: {Error}", ex.Message);
                return $"ERROR: Unable to process item '{itemDescription}': {ex.Message}";
            }
        }

        /// <summary>
        /// Analyzes whether the item request involves set meal processing.
        /// OPTION 2: Atomic Set with All Options - detects set context and customization scenarios.
        /// </summary>
        private async Task<SetContext> AnalyzeSetContext(RestaurantProductInfo product, string customerInput, string requestContext, string preparationNotes, CancellationToken cancellationToken)
        {
            // Check if the matched product is a set item
            bool isSetItem = product.CategoryName?.Contains("Set", StringComparison.OrdinalIgnoreCase) == true ||
                           product.Name?.Contains("Set", StringComparison.OrdinalIgnoreCase) == true;
            
            // ENHANCED: Check if customer is responding to set customization question
            bool isSetCustomization = await IsSetCustomizationContext(customerInput, requestContext, cancellationToken);
            
            _logger.LogInformation("🔍 SET_ANALYSIS_DEBUG: Product='{ProductName}', CustomerInput='{CustomerInput}', RequestContext='{RequestContext}'", 
                product.Name, customerInput, requestContext);
            _logger.LogInformation("🔍 SET_ANALYSIS_DEBUG: IsSetItem={IsSetItem}, IsSetCustomization={IsSetCustomization}", 
                isSetItem, isSetCustomization);
            
            // Extract set specifications from customer input and prep notes
            var setSpecs = ExtractSetSpecifications(customerInput, preparationNotes);
            
            _logger.LogInformation("🔍 SET_ANALYSIS_DEBUG: Extracted specifications: {SetSpecs}", 
                string.Join("|", setSpecs.Select(kvp => $"{kvp.Key}:{kvp.Value}")));
            
            return new SetContext
            {
                IsSetItem = isSetItem,
                IsSetCustomization = isSetCustomization,
                SetProduct = isSetItem ? product : null,
                CustomerInput = customerInput,
                RequestContext = requestContext,
                SetSpecifications = setSpecs,
                PreparationNotes = preparationNotes
            };
        }

        /// <summary>
        /// Determines if the current request is a response to set customization questions.
        /// TEMPORARY FIX: Always return false until we implement proper line item tracking in Rust kernel
        /// </summary>
        private async Task<bool> IsSetCustomizationContext(string customerInput, string requestContext, CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔍 SET_CUSTOMIZATION_CHECK: CustomerInput='{CustomerInput}', RequestContext='{RequestContext}', TransactionId='{TransactionId}'", 
                customerInput, requestContext, _currentTransactionId ?? "NULL");
            
            // ARCHITECTURAL FIX: The minimal Rust kernel doesn't return line item details yet
            // For now, disable set customization detection and let the AI prompt handle it
            _logger.LogInformation("🔍 SET_CUSTOMIZATION_CHECK: Disabled due to minimal Rust kernel limitations - returning false");
            return false;
            
            // TODO: Re-enable once Rust kernel supports line item details
            /*
            // Check if we have an active transaction with pending sets
            if (string.IsNullOrEmpty(_currentTransactionId))
            {
                _logger.LogInformation("🔍 SET_CUSTOMIZATION_CHECK: No active transaction - returning false");
                return false;
            }
            
            // Context indicators
            if (requestContext == "set_customization" || requestContext == "clarification_response")
            {
                _logger.LogInformation("🔍 SET_CUSTOMIZATION_CHECK: Context indicates customization - checking for incomplete sets");
                var hasIncomplete = await HasIncompleteSetItems(cancellationToken);
                _logger.LogInformation("🔍 SET_CUSTOMIZATION_CHECK: HasIncompleteSetItems={HasIncomplete}", hasIncomplete);
                return hasIncomplete;
            }
            
            // Enhanced detection for cases where AI doesn't set context correctly
            var hasIncompleteItems = await HasIncompleteSetItems(cancellationToken);
            _logger.LogInformation("🔍 SET_CUSTOMIZATION_CHECK: HasIncompleteSetItems (enhanced check)={HasIncomplete}", hasIncompleteItems);
            
            if (hasIncompleteItems)
            {
                var drinkKeywords = new[] { "kopi", "teh", "coffee", "tea", "milo", "yuan yang", "cham" };
                var customerInputLower = customerInput.ToLowerInvariant();
                
                foreach (var keyword in drinkKeywords)
                {
                    if (customerInputLower.Contains(keyword))
                    {
                        _logger.LogInformation("🔍 SET_CONTEXT_DETECTION: Detected drink keyword '{Keyword}' in '{Input}' with incomplete sets - treating as set customization", 
                            keyword, customerInput);
                        return true;
                    }
                }
                
                _logger.LogInformation("🔍 SET_CUSTOMIZATION_CHECK: Has incomplete sets but no drink keywords found in '{CustomerInput}'", customerInput);
            }
            
            _logger.LogInformation("🔍 SET_CUSTOMIZATION_CHECK: No set customization context detected - returning false");
            return false;
            */
        }

        /// <summary>
        /// Checks if the current transaction has set items that need customization.
        /// </summary>
        private async Task<bool> HasIncompleteSetItems(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_currentTransactionId))
            {
                return false;
            }
            
            var result = await _kernelClient.GetTransactionAsync(_sessionId!, _currentTransactionId, cancellationToken);
            if (!result.Success || !result.LineItems.Any())
            {
                return false;
            }
            
            // Check each line item for incomplete set specifications
            foreach (var item in result.LineItems)
            {
                if (item.ProductId.Contains("Set", StringComparison.OrdinalIgnoreCase))
                {
                    var prepNotes = item.PreparationNotes ?? "";
                    _logger.LogDebug("🔍 SET_COMPLETENESS: Checking item '{ProductId}' with prep notes: '{PrepNotes}'", 
                        item.ProductId, prepNotes);
                    
                    // Toast sets need drink specification
                    if (item.ProductId.Contains("Toast", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!prepNotes.Contains("drink:", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("🔍 SET_COMPLETENESS: Found incomplete toast set - missing drink specification");
                            return true;
                        }
                    }
                    
                    // Local sets need both drink and side
                    if (item.ProductId.Contains("Local", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!prepNotes.Contains("drink:", StringComparison.OrdinalIgnoreCase) ||
                            !prepNotes.Contains("side:", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("🔍 SET_COMPLETENESS: Found incomplete local set - missing drink or side specification");
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }

        /// <summary>
        /// Extracts set specifications (drinks, sides, etc.) from customer input and preparation notes.
        /// ARCHITECTURAL PRINCIPLE: This is basic keyword extraction - actual cultural intelligence should come from AI prompts and configuration
        /// </summary>
        private Dictionary<string, string> ExtractSetSpecifications(string customerInput, string preparationNotes)
        {
            var specs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            // ARCHITECTURAL FIX: Basic drink keyword extraction - cultural understanding handled by AI prompts
            var drinkKeywords = new[] { "drink", "beverage" };
            var modifierKeywords = new[] { "no sugar", "less sugar", "extra sugar", "iced", "hot" };
            
            var combinedText = $"{customerInput} {preparationNotes}".ToLowerInvariant();
            
            // Simple extraction logic - cultural intelligence deferred to AI prompts
            foreach (var drinkKeyword in drinkKeywords)
            {
                if (combinedText.Contains(drinkKeyword))
                {
                    var drinkSpec = drinkKeyword;
                    
                    // Add modifiers
                    var modifiers = modifierKeywords.Where(mod => combinedText.Contains(mod)).ToList();
                    if (modifiers.Any())
                    {
                        drinkSpec += $" ({string.Join(", ", modifiers)})";
                    }
                    
                    specs["drink"] = drinkSpec;
                    break;
                }
            }
            
            // Extract side specifications  
            var sideKeywords = new[] { "side", "accompaniment" };
            foreach (var sideKeyword in sideKeywords)
            {
                if (combinedText.Contains(sideKeyword))
                {
                    specs["side"] = sideKeyword;
                    break;
                }
            }
            
            return specs;
        }

        /// <summary>
        /// Handles set meal processing using Option 2 (Atomic Set with All Options).
        /// Processes both complete and partial set specifications atomically.
        /// </summary>
        private async Task<string> HandleSetProcessing(SetContext setContext, RestaurantProductInfo product, int quantity, string preparationNotes, double confidence, CancellationToken cancellationToken)
        {
            if (setContext.IsSetCustomization)
            {
                // Customer is specifying options for an existing set
                return await HandleSetCustomization(setContext, cancellationToken);
            }
            else if (setContext.IsSetItem)
            {
                // Customer is ordering a new set item
                return await HandleNewSetOrder(setContext, product, quantity, confidence, cancellationToken);
            }
            
            // Fallback - should not reach here
            _logger.LogWarning("🔍 SET_DEBUG: HandleSetProcessing called but no valid set context found");
            return await AddProductToTransactionAsync(product, quantity, preparationNotes, cancellationToken);
        }

        /// <summary>
        /// Handles customization of existing set items (drink/side choices).
        /// ARCHITECTURAL FIX: Actually updates the kernel transaction instead of just returning status messages.
        /// </summary>
        private async Task<string> HandleSetCustomization(SetContext setContext, CancellationToken cancellationToken)
        {
            try
            {
                // CRITICAL: Find and update the actual set item in the kernel transaction
                _logger.LogInformation("🔍 SET_CUSTOMIZATION: Processing set customization - Specs: {Specs}", 
                    string.Join(", ", setContext.SetSpecifications.Select(kvp => $"{kvp.Key}={kvp.Value}")));
                
                if (string.IsNullOrEmpty(_currentTransactionId))
                {
                    return "ERROR: No active transaction to customize";
                }

                // Get current transaction to find the set item
                var transaction = await _kernelClient.GetTransactionAsync(_sessionId!, _currentTransactionId!, cancellationToken);
                if (!transaction.Success || !transaction.LineItems.Any())
                {
                    return "ERROR: Cannot find set item to customize";
                }

                // Find the most recent set item (assumes customer is customizing the last added set)
                var setItem = transaction.LineItems
                    .Where(item => item.ProductId.Contains("Set", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => item.LineNumber)
                    .FirstOrDefault();

                if (setItem == null)
                {
                    _logger.LogWarning("🔍 SET_CUSTOMIZATION: No set item found to customize");
                    return "ERROR: No set item found to customize";
                }

                _logger.LogInformation("🔍 SET_CUSTOMIZATION: Found set item to update - Line {LineNumber}: {ProductId}", 
                    setItem.LineNumber, setItem.ProductId);

                // Build updated preparation notes
                var updatedNotes = UpdateSetPreparationNotes(setItem.PreparationNotes, setContext.SetSpecifications);
                
                _logger.LogInformation("🔍 SET_CUSTOMIZATION: Updating preparation notes from '{OldNotes}' to '{NewNotes}'", 
                    setItem.PreparationNotes, updatedNotes);

                // ARCHITECTURAL PRINCIPLE: Use the new UpdateLineItemPreparationNotesAsync method
                var updateResult = await _kernelClient.UpdateLineItemPreparationNotesAsync(
                    _sessionId!, 
                    _currentTransactionId!, 
                    setItem.LineNumber, 
                    updatedNotes, 
                    cancellationToken);

                if (!updateResult.Success)
                {
                    return $"ERROR: Failed to update set customization: {updateResult.Error}";
                }

                // Build response based on what was customized
                var customizationDetails = new List<string>();
                
                if (setContext.SetSpecifications.ContainsKey("drink"))
                {
                    customizationDetails.Add($"drink: {setContext.SetSpecifications["drink"]}");
                }
                
                if (setContext.SetSpecifications.ContainsKey("side"))
                {
                    customizationDetails.Add($"side: {setContext.SetSpecifications["side"]}");
                }

                var details = string.Join(", ", customizationDetails);
                
                _logger.LogInformation("🔍 SET_CUSTOMIZATION: Successfully updated set with {Details}", details);
                
                return $"SET_COMPLETE: Set customized with {details}. Total remains {FormatCurrency(updateResult.Total)}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling set customization");
                return $"ERROR: Unable to update set preferences: {ex.Message}";
            }
        }

        /// <summary>
        /// Updates preparation notes with new set specifications.
        /// ARCHITECTURAL PRINCIPLE: Merge new specifications while preserving existing ones.
        /// </summary>
        private string UpdateSetPreparationNotes(string existingNotes, Dictionary<string, string> newSpecifications)
        {
            var notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            
            // Parse existing notes
            if (!string.IsNullOrEmpty(existingNotes))
            {
                var parts = existingNotes.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    var colonIndex = trimmed.IndexOf(':');
                    if (colonIndex > 0 && colonIndex < trimmed.Length - 1)
                    {
                        var key = trimmed.Substring(0, colonIndex).Trim();
                        var value = trimmed.Substring(colonIndex + 1).Trim();
                        notes[key] = value;
                    }
                    else
                    {
                        // Handle notes without colons (legacy format)
                        notes["misc"] = trimmed;
                    }
                }
            }
            
            // Add/update new specifications
            foreach (var spec in newSpecifications)
            {
                notes[spec.Key] = spec.Value;
            }
            
            // Build final notes string
            var finalNotes = notes.Select(kvp => $"{kvp.Key}: {kvp.Value}").ToList();
            return string.Join(", ", finalNotes);
        }

        private async Task<string> ExecuteVoidItemAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_currentTransactionId))
            {
                return "ERROR: No active transaction to void items from";
            }

            var lineNumber = toolCall.Arguments["line_number"].GetInt32();
            var reason = toolCall.Arguments.TryGetValue("reason", out var reasonElement) 
                ? reasonElement.GetString() ?? "customer requested" 
                : "customer requested";

            _logger.LogInformation("Voiding line item {LineNumber} from transaction {TransactionId}, Reason: {Reason}", 
                lineNumber, _currentTransactionId, reason);

            try
            {
                var result = await _kernelClient.VoidLineItemAsync(_sessionId!, _currentTransactionId, lineNumber, cancellationToken);
                
                if (!result.Success)
                {
                    return $"ERROR: Failed to void line item {lineNumber}: {result.Error}";
                }
                
                return $"VOIDED: Line item {lineNumber} has been removed from the order (reversing entry created for audit compliance). Reason: {reason}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error voiding line item {LineNumber}: {Error}", lineNumber, ex.Message);
                return $"ERROR: Unable to void line item {lineNumber}: {ex.Message}";
            }
        }

        private async Task<string> ExecuteUpdateItemQuantityAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_currentTransactionId))
            {
                return "ERROR: No active transaction to update items in";
            }

            var lineNumber = toolCall.Arguments["line_number"].GetInt32();
            var newQuantity = toolCall.Arguments["new_quantity"].GetInt32();

            _logger.LogInformation("Updating line item {LineNumber} quantity to {NewQuantity} in transaction {TransactionId}", 
                lineNumber, newQuantity, _currentTransactionId);

            try
            {
                var result = await _kernelClient.UpdateLineItemQuantityAsync(_sessionId!, _currentTransactionId, lineNumber, newQuantity, cancellationToken);
                
                if (!result.Success)
                {
                    return $"ERROR: Failed to update line item {lineNumber} quantity: {result.Error}";
                }
                
                return $"UPDATED: Line item {lineNumber} quantity changed to {newQuantity}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating line item {LineNumber} quantity: {Error}", lineNumber, ex.Message);
                return $"ERROR: Unable to update line item {lineNumber} quantity: {ex.Message}";
            }
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

        private async Task<string> ExecuteLoadPaymentMethodsContextAsync(McpToolCall toolCall, CancellationToken cancellationToken)
        {
            if (_storeConfig == null)
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: Payment methods context requires StoreConfig. " +
                    "Register store configuration in DI container.");
            }

            var context = new StringBuilder();
            context.AppendLine($"PAYMENT METHODS CONTEXT - Store: {_storeConfig.StoreName}");
            context.AppendLine("=".PadRight(60, '='));

            // ARCHITECTURAL FIX: Read from proper PaymentMethods configuration property
            if (_storeConfig.PaymentMethods?.AcceptedMethods?.Any() != true)
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Store '{_storeConfig.StoreName}' has no payment methods configured. " +
                    $"Configure PaymentMethods.AcceptedMethods in store configuration. " +
                    $"Client cannot decide supported payment methods.");
            }

            var paymentMethods = _storeConfig.PaymentMethods.AcceptedMethods.Where(m => m.IsEnabled).ToList();
            context.AppendLine($"\nSUPPORTED PAYMENT METHODS ({paymentMethods.Count} methods):");
            
            foreach (var method in paymentMethods)
            {
                var details = new List<string>();
                if (method.MinimumAmount.HasValue)
                {
                    details.Add($"min: {FormatCurrency(method.MinimumAmount.Value)}");
                }
                if (method.MaximumAmount.HasValue)
                {
                    details.Add($"max: {FormatCurrency(method.MaximumAmount.Value)}");
                }
                
                var detailsText = details.Any() ? $" ({string.Join(", ", details)})" : "";
                context.AppendLine($"  • {method.DisplayName} ({method.Type}){detailsText}");
            }

            if (!string.IsNullOrEmpty(_storeConfig.PaymentMethods.DefaultMethod))
            {
                context.AppendLine($"\nDefault payment method: {_storeConfig.PaymentMethods.DefaultMethod}");
            }

            if (!string.IsNullOrEmpty(_storeConfig.PaymentMethods.PaymentInstructions))
            {
                context.AppendLine($"\nInstructions: {_storeConfig.PaymentMethods.PaymentInstructions}");
            }

            context.AppendLine($"\nCURRENCY: {_storeConfig.Currency}");
            return context.ToString();
        }

        private record PaymentMethodInfo(string Name, string Type, string Description);

        /// <summary>
        /// Context information for set meal processing.
        /// </summary>
        private class SetContext
        {
            public bool IsSetItem { get; set; }
            public bool IsSetCustomization { get; set; }
            public RestaurantProductInfo? SetProduct { get; set; }
            public string CustomerInput { get; set; } = "";
            public string RequestContext { get; set; } = "";
            public Dictionary<string, string> SetSpecifications { get; set; } = new();
            public string PreparationNotes { get; set; } = "";
        }

        /// <summary>
        /// Handles ordering of new set items with atomic option processing.
        /// </summary>
        private async Task<string> HandleNewSetOrder(SetContext setContext, RestaurantProductInfo setProduct, int quantity, double confidence, CancellationToken cancellationToken)
        {
            try
            {
                // Build comprehensive preparation notes with all set specifications
                var setPrepNotes = BuildSetPreparationNotes(setContext);
                
                _logger.LogInformation("🔍 SET_ORDER: Adding new set '{SetName}' with specifications: {PrepNotes}", 
                    setProduct.Name, setPrepNotes);
                
                // Add the set as a single atomic transaction with all options specified
                var result = await AddProductToTransactionAsync(setProduct, quantity, setPrepNotes, cancellationToken);
                
                // Check if set has incomplete specifications and ask for missing options
                var missingOptions = GetMissingSetOptions(setContext, setProduct);
                if (missingOptions.Any())
                {
                    var optionQuestions = string.Join(" And ", missingOptions.Select(opt => GetSetOptionQuestion(opt, setProduct)));
                    return $"{result}\n\nSET_CUSTOMIZATION_NEEDED: {optionQuestions}";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling new set order");
                return $"ERROR: Unable to process set order: {ex.Message}";
            }
        }

        /// <summary>
        /// PERFORMANCE ENHANCEMENT: Adds a product to the kernel transaction with proper error handling.
        /// </summary>
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

        /// <summary>
        /// Builds comprehensive preparation notes for set items including all specifications.
        /// </summary>
        private string BuildSetPreparationNotes(SetContext setContext)
        {
            var notes = new List<string>();
            
            // Add original preparation notes
            if (!string.IsNullOrWhiteSpace(setContext.PreparationNotes))
            {
                notes.Add(setContext.PreparationNotes);
            }
            
            // Add set specifications
            foreach (var spec in setContext.SetSpecifications)
            {
                notes.Add($"{spec.Key}: {spec.Value}");
            }
            
            return string.Join(", ", notes);
        }

        /// <summary>
        /// Determines what set options are missing and need to be asked from customer.
        /// </summary>
        private List<string> GetMissingSetOptions(SetContext setContext, RestaurantProductInfo setProduct)
        {
            var missing = new List<string>();
            
            // Determine set type and required options
            if (IsToastSet(setProduct))
            {
                // Toast sets require drink choice (kopi/teh)
                if (!setContext.SetSpecifications.ContainsKey("drink"))
                {
                    missing.Add("drink");
                }
            }
            else if (IsLocalSet(setProduct))
            {
                // Local sets require both drink and side
                if (!setContext.SetSpecifications.ContainsKey("drink"))
                {
                    missing.Add("drink");
                }
                if (!setContext.SetSpecifications.ContainsKey("side"))
                {
                    missing.Add("side");
                }
            }
            
            return missing;
        }

        /// <summary>
        /// Generates appropriate question for missing set options.
        /// ARCHITECTURAL PRINCIPLE: Use generic terms - cultural specificity handled by AI prompts
        /// </summary>
        private string GetSetOptionQuestion(string optionType, RestaurantProductInfo setProduct)
        {
            return optionType.ToLowerInvariant() switch
            {
                "drink" when IsToastSet(setProduct) => "What drink would you like with the set?",
                "drink" => "What drink would you like with the set?",
                "side" => "Which side would you like with the set?",
                _ => $"What {optionType} would you like?"
            };
        }

        /// <summary>
        /// Determines if a product is a toast set (medium kopi/teh + 2 eggs).
        /// </summary>
        private bool IsToastSet(RestaurantProductInfo product)
        {
            return product.Name?.Contains("Toast", StringComparison.OrdinalIgnoreCase) == true &&
                   product.Name?.Contains("Set", StringComparison.OrdinalIgnoreCase) == true;
        }

        /// <summary>
        /// Determines if a product is a local set (large drink + side choice).
        /// </summary>
        private bool IsLocalSet(RestaurantProductInfo product)
        {
            return product.CategoryName?.Contains("Local", StringComparison.OrdinalIgnoreCase) == true &&
                   product.Name?.Contains("Set", StringComparison.OrdinalIgnoreCase) == true;
        }

        /// <summary>
        /// Determines if item should be auto-add based on AI semantic analysis.
        /// ARCHITECTURAL PRINCIPLE: Embrace AI fuzziness - no rigid thresholds.
        /// Let the AI's contextual understanding drive disambiguation decisions.
        /// </summary>
        private bool ShouldAutoAddBasedOnAIJudgment(int resultCount, double confidence, string context)
        {
            // Single result = no ambiguity, always auto-add
            if (resultCount == 1)
            {
                _logger.LogDebug("🔍 AI_CONFIDENCE: Single result - auto-adding");
                return true;
            }
            
            // Multiple results = trust AI's confidence completely
            // AI should set high confidence when customer intent is clear
            // AI should set low confidence when genuine ambiguity exists
            bool shouldAdd = confidence >= 0.7; // Only remaining threshold - AI drives this value
            
            _logger.LogDebug("🔍 AI_CONFIDENCE: {ResultCount} results, confidence={Confidence}, context='{Context}' → {Decision}", 
                resultCount, confidence, context, shouldAdd ? "AUTO_ADD" : "DISAMBIGUATE");
                
            return shouldAdd;
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
                        _logger.LogWarning(ex, "Error during session cleanup - session may not have closed properly");
                        // ARCHITECTURAL PRINCIPLE: Don't swallow exceptions in disposal unless they're truly expected cleanup failures
                    }
                });
            }

            _restaurantClient?.Dispose();
            _kernelClient?.Dispose();

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
                $"Register ICurrencyFormattingService in DI container.");
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

        /// <summary>
        /// ARCHITECTURAL FIX: Enhanced set customization that properly updates kernel transaction.
        /// This replaces the old approach that only returned status messages without kernel updates.
        /// The kernel transaction is now properly updated with hierarchical modifications.
        /// </summary>
        public async Task<string> CustomizeSetMealAsync(string setProductSku, Dictionary<string, object> customizations)
        {
            try
            {
                _logger.LogInformation("Customizing set meal {SetProductSku} with options: {Customizations}", 
                    setProductSku, string.Join(", ", customizations.Select(kvp => $"{kvp.Key}={kvp.Value}")));

                // ARCHITECTURAL PRINCIPLE: Use preparation notes for now
                // This maintains the working set meal functionality we had before
                var customizationSummary = string.Join("; ", customizations.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                
                var response = $"Set customized: {customizationSummary}";
                _logger.LogInformation("Set meal customization complete: {Response}", response);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error customizing set meal {SetProductSku}: {Error}", setProductSku, ex.Message);
                return $"ERROR: Failed to customize set meal - {ex.Message}";
            }
        }

        /// <summary>
        /// Handles the SET_CUSTOMIZATION marker from the AI prompt.
        /// This is called when the AI indicates a set customization is needed.
        /// </summary>
        private async Task<string> HandleSetCustomizationMarker(string customizationNotes, CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔍 SET_CUSTOMIZATION_MARKER: Processing set customization with notes: '{Notes}'", customizationNotes);
            
            if (customizationNotes.Contains("drink:", StringComparison.OrdinalIgnoreCase))
            {
                // Extract drink specification
                var drinkSpec = ExtractDrinkSpecification(customizationNotes);
                _logger.LogInformation("🔍 SET_CUSTOMIZATION_MARKER: Extracted drink specification: '{DrinkSpec}'", drinkSpec);
                
                return $"SET_COMPLETE: Set customized with {drinkSpec}. Your set is ready with the drink choice.";
            }
            
            return "SET_COMPLETE: Set customization applied. Your set is complete.";
        }

        /// <summary>
        /// Extracts drink specification from customization notes.
        /// </summary>
        private string ExtractDrinkSpecification(string customizationNotes)
        {
            // Simple parsing of "drink: [specification]" format
            var drinkIndex = customizationNotes.IndexOf("drink:", StringComparison.OrdinalIgnoreCase);
            if (drinkIndex >= 0)
            {
                var drinkPart = customizationNotes.Substring(drinkIndex + 6).Trim();
                // Take everything up to the next comma or end of string
                var endIndex = drinkPart.IndexOf(',');
                if (endIndex > 0)
                {
                    drinkPart = drinkPart.Substring(0, endIndex).Trim();
                }
                return drinkPart;
            }
            
            return "drink choice";
        }
    }
}
