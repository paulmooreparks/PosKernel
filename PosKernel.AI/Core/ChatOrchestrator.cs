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

using PosKernel.AI.Models;
using PosKernel.AI.Interfaces;
using PosKernel.AI.Services;
using PosKernel.AI.Tools;
using PosKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace PosKernel.AI.Core
{
    /// <summary>
    /// Core business logic orchestrator for the POS AI demo.
    /// Manages conversation, receipt state, and tool execution.
    /// UI-agnostic - can be used with console, Terminal.Gui, or web interfaces.
    /// </summary>
    public class ChatOrchestrator
    {
        private readonly McpClient _mcpClient;
        private readonly KernelPosToolsProvider? _kernelToolsProvider;
        private readonly PosToolsProvider? _mockToolsProvider;
        private readonly ILogger<ChatOrchestrator> _logger;
        private readonly AiPersonalityConfig _personality;
        private readonly StoreConfig _storeConfig;
        
        private readonly Receipt _receipt;
        private readonly List<ChatMessage> _conversationHistory;
        private bool _useRealKernel;
        
        /// <summary>
        /// Gets the current receipt.
        /// </summary>
        public Receipt CurrentReceipt => _receipt;
        
        /// <summary>
        /// Gets the conversation history.
        /// </summary>
        public IReadOnlyList<ChatMessage> ConversationHistory => _conversationHistory;
        
        /// <summary>
        /// Gets the store configuration.
        /// </summary>
        public StoreConfig StoreConfig => _storeConfig;
        
        /// <summary>
        /// Gets the personality configuration.
        /// </summary>
        public AiPersonalityConfig Personality => _personality;

        /// <summary>
        /// Initializes a new instance of the ChatOrchestrator.
        /// </summary>
        /// <param name="mcpClient">The MCP client for AI communication.</param>
        /// <param name="personality">The AI personality configuration.</param>
        /// <param name="storeConfig">The store configuration.</param>
        /// <param name="logger">The logger for diagnostics.</param>
        /// <param name="kernelToolsProvider">Optional kernel tools provider for real kernel integration.</param>
        /// <param name="mockToolsProvider">Optional mock tools provider for demo mode.</param>
        public ChatOrchestrator(
            McpClient mcpClient,
            AiPersonalityConfig personality,
            StoreConfig storeConfig,
            ILogger<ChatOrchestrator> logger,
            KernelPosToolsProvider? kernelToolsProvider = null,
            PosToolsProvider? mockToolsProvider = null)
        {
            _mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
            _personality = personality ?? throw new ArgumentNullException(nameof(personality));
            _storeConfig = storeConfig ?? throw new ArgumentNullException(nameof(storeConfig));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kernelToolsProvider = kernelToolsProvider;
            _mockToolsProvider = mockToolsProvider;
            
            _useRealKernel = kernelToolsProvider != null;
            
            _receipt = new Receipt
            {
                Store = new StoreInfo
                {
                    Name = storeConfig.StoreName,
                    Currency = storeConfig.Currency,
                    StoreType = storeConfig.StoreType.ToString()
                },
                TransactionId = Guid.NewGuid().ToString()[..8]
            };
            
            _conversationHistory = new List<ChatMessage>();
            
            _logger.LogInformation("ChatOrchestrator initialized for {StoreName} using {Integration}", 
                storeConfig.StoreName, _useRealKernel ? "Real Kernel" : "Mock");
        }

        /// <summary>
        /// Initializes the orchestrator and loads menu context.
        /// </summary>
        public async Task<ChatMessage> InitializeAsync()
        {
            try
            {
                var currentTime = DateTime.Now;
                var timeOfDay = currentTime.Hour < 12 ? "morning" : currentTime.Hour < 17 ? "afternoon" : "evening";
                
                if (_useRealKernel && _kernelToolsProvider != null)
                {
                    return await InitializeRealKernelAsync(timeOfDay);
                }
                else
                {
                    return await InitializeMockAsync(timeOfDay);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during initialization");
                var fallbackMessage = new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = $"Welcome to {_storeConfig.StoreName}! How can I help you today?",
                    IsSystem = false
                };
                _conversationHistory.Add(fallbackMessage);
                return fallbackMessage;
            }
        }

        /// <summary>
        /// Processes user input and returns the AI response.
        /// </summary>
        public async Task<ChatMessage> ProcessUserInputAsync(string userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                return new ChatMessage { Sender = "System", Content = "", IsSystem = true, ShowInCleanMode = false };
            }

            // Add user message to history
            var userMessage = new ChatMessage
            {
                Sender = "Customer",
                Content = userInput,
                IsSystem = false
            };
            _conversationHistory.Add(userMessage);

            // Check for exit commands
            if (IsExitCommand(userInput))
            {
                var exitMessage = new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = $"Thank you for visiting {_storeConfig.StoreName}! Come back anytime!",
                    IsSystem = false
                };
                _conversationHistory.Add(exitMessage);
                return exitMessage;
            }

            // Check for completion requests
            if (IsCompletionRequest(userInput) && _receipt.Items.Any())
            {
                _receipt.Status = PaymentStatus.ReadyForPayment;
                _logger.LogInformation("Order completion requested, setting status to ReadyForPayment with {ItemCount} items totaling ${Total:F2}", 
                    _receipt.Items.Count, _receipt.Total);
                
                var completionMessage = new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = $"Perfect! Your order is ready. That's {_receipt.Items.Count} items for ${_receipt.Total:F2}. How would you like to pay?",
                    IsSystem = false
                };
                _conversationHistory.Add(completionMessage);
                return completionMessage;
            }

            try
            {
                var conversationContext = string.Join("\n", _conversationHistory.TakeLast(8).Select(m => $"{m.Sender}: {m.Content}"));
                var prompt = CreateIntelligentPrompt(conversationContext, userInput);
                
                var availableTools = _useRealKernel ? _kernelToolsProvider!.GetAvailableTools() : _mockToolsProvider!.GetAvailableTools();
                var response = await _mcpClient.CompleteWithToolsAsync(prompt, availableTools);
                
                var aiMessage = new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = response.Content,
                    IsSystem = false
                };

                // Execute any tool calls
                if (response.ToolCalls.Any())
                {
                    _logger.LogInformation("Executing {ToolCallCount} tool calls", response.ToolCalls.Count);
                    var toolResults = await ExecuteToolsAsync(response.ToolCalls);
                    var followUpResponse = await GenerateFollowUpAsync(conversationContext, userInput, response.Content, toolResults);
                    
                    if (!string.IsNullOrEmpty(followUpResponse))
                    {
                        aiMessage.Content = response.Content + " " + followUpResponse;
                    }
                }

                _conversationHistory.Add(aiMessage);
                return aiMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing user input: {UserInput}", userInput);
                var errorMessage = new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = "Sorry, I'm having a technical issue. Please try again.",
                    IsSystem = false
                };
                _conversationHistory.Add(errorMessage);
                return errorMessage;
            }
        }

        /// <summary>
        /// Checks if the current order is ready for completion.
        /// </summary>
        public bool IsOrderComplete()
        {
            return _receipt.Items.Any() && _receipt.Status == PaymentStatus.ReadyForPayment;
        }

        private async Task<ChatMessage> InitializeRealKernelAsync(string timeOfDay)
        {
            // Load menu context from real restaurant extension
            var menuContextCall = new McpToolCall
            {
                Id = "startup-" + Guid.NewGuid().ToString()[..8],
                FunctionName = "load_menu_context",
                Arguments = new Dictionary<string, System.Text.Json.JsonElement>
                {
                    ["include_categories"] = System.Text.Json.JsonSerializer.SerializeToElement(true)
                }
            };
            
            var menuContext = await _kernelToolsProvider!.ExecuteToolAsync(menuContextCall);
            
            var enhancedGreetingPrompt = $@"You are {_personality.StaffTitle} at {_storeConfig.StoreName}.

COMPLETE MENU INTELLIGENCE LOADED:
{menuContext}

GREETING INSTRUCTIONS:
- Greet the customer warmly in your personality style
- Mention SPECIFIC items from your actual menu above (not generic terms)
- Show you know what you serve with actual product names
- Keep it natural and conversational

Time of day: {timeOfDay}
Store: {_storeConfig.StoreName} | Currency: {_storeConfig.Currency}

Greet the customer using your actual menu knowledge!";
            
            var availableTools = _kernelToolsProvider.GetAvailableTools();
            var response = await _mcpClient.CompleteWithToolsAsync(enhancedGreetingPrompt, availableTools);
            
            var greetingMessage = new ChatMessage
            {
                Sender = _personality.StaffTitle,
                Content = response.Content,
                IsSystem = false
            };
            
            _conversationHistory.Add(greetingMessage);
            return greetingMessage;
        }

        private async Task<ChatMessage> InitializeMockAsync(string timeOfDay)
        {
            var greetingPrompt = _personality.GreetingTemplate
                .Replace("{timeOfDay}", timeOfDay)
                .Replace("{currentTime}", DateTime.Now.ToString("HH:mm"));
            
            var availableTools = _mockToolsProvider!.GetAvailableTools();
            var response = await _mcpClient.CompleteWithToolsAsync(greetingPrompt, availableTools);
            
            var greetingMessage = new ChatMessage
            {
                Sender = _personality.StaffTitle,
                Content = response.Content,
                IsSystem = false
            };
            
            _conversationHistory.Add(greetingMessage);
            return greetingMessage;
        }

        private async Task<List<string>> ExecuteToolsAsync(IReadOnlyList<McpToolCall> toolCalls)
        {
            var results = new List<string>();
            
            foreach (var toolCall in toolCalls)
            {
                try
                {
                    string result;
                    if (_useRealKernel && _kernelToolsProvider != null)
                    {
                        result = await _kernelToolsProvider.ExecuteToolAsync(toolCall);
                    }
                    else if (_mockToolsProvider != null)
                    {
                        // Mock tools need a transaction - create a simple adapter
                        var mockTransaction = CreateMockTransaction();
                        result = await _mockToolsProvider.ExecuteToolAsync(toolCall, mockTransaction);
                        UpdateReceiptFromMockTransaction(mockTransaction);
                    }
                    else
                    {
                        result = "Tool execution not available";
                    }
                    
                    results.Add(result);
                    UpdateReceiptFromToolResult(toolCall, result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing tool {FunctionName}", toolCall.FunctionName);
                    results.Add($"Error executing {toolCall.FunctionName}: {ex.Message}");
                }
            }
            
            return results;
        }

        private void UpdateReceiptFromToolResult(McpToolCall toolCall, string result)
        {
            _logger.LogInformation("Updating receipt from tool result: {FunctionName} -> {Result}", toolCall.FunctionName, result);
            
            // Parse tool results and update receipt
            if (toolCall.FunctionName == "add_item_to_transaction")
            {
                _logger.LogInformation("Processing add_item_to_transaction with result: {Result}", result);
                
                // Check for disambiguation response first
                if (result.StartsWith("DISAMBIGUATION_NEEDED:"))
                {
                    _logger.LogInformation("Disambiguation needed - presenting options to user");
                    // Don't try to parse as an item - this is handled by the AI conversation
                    return;
                }
                
                // Check for other non-item responses
                if (result.StartsWith("ERROR:") || result.StartsWith("FAILED:") || result.StartsWith("INVALID:"))
                {
                    _logger.LogWarning("Tool execution returned error: {Result}", result);
                    return;
                }
                
                if (TryParseAddedItem(result, out var item))
                {
                    _logger.LogInformation("Successfully parsed item: {ProductName} x{Quantity} @ ${UnitPrice} = ${LineTotal}", 
                        item.ProductName, item.Quantity, item.UnitPrice, item.CalculatedTotal);
                    _receipt.Items.Add(item);
                    _logger.LogInformation("Receipt now has {ItemCount} items, total: ${Total}", 
                        _receipt.Items.Count, _receipt.Total);
                }
                else
                {
                    _logger.LogWarning("Failed to parse item from result: {Result}", result);
                    
                    // Fallback: Try to extract basic info from the result
                    if (TryCreateFallbackItem(result, toolCall, out var fallbackItem))
                    {
                        _logger.LogInformation("Created fallback item: {ProductName} x{Quantity} @ ${UnitPrice}", 
                            fallbackItem.ProductName, fallbackItem.Quantity, fallbackItem.UnitPrice);
                        _receipt.Items.Add(fallbackItem);
                        _logger.LogInformation("Receipt now has {ItemCount} items (fallback), total: ${Total}", 
                            _receipt.Items.Count, _receipt.Total);
                    }
                    else
                    {
                        _logger.LogError("Failed to create any item from tool result: {Result}", result);
                    }
                }
            }
            else if (toolCall.FunctionName == "process_payment" && result.Contains("Payment processed"))
            {
                _receipt.Status = PaymentStatus.Completed;
                _logger.LogInformation("Payment processed, receipt status updated to Completed");
                
                // CRITICAL FIX: Start new transaction after payment completion
                Task.Delay(100).ContinueWith(_ => StartNewTransactionAfterPayment());
            }
        }
        
        /// <summary>
        /// Starts a new transaction after the previous one was completed and paid.
        /// This ensures proper transaction lifecycle management.
        /// </summary>
        private void StartNewTransactionAfterPayment()
        {
            _logger.LogInformation("Starting new transaction after payment completion");
            
            // Clear the current receipt and start fresh
            _receipt.Items.Clear();
            _receipt.Status = PaymentStatus.Building;
            _receipt.TransactionId = Guid.NewGuid().ToString()[..8]; // New transaction ID
            
            _logger.LogInformation("New transaction started: {TransactionId}", _receipt.TransactionId);
        }

        private bool TryCreateFallbackItem(string result, McpToolCall toolCall, out ReceiptLineItem item)
        {
            item = new ReceiptLineItem();
            
            try
            {
                // Try to extract from the tool call arguments if parsing the result fails
                if (toolCall.Arguments.TryGetValue("product_name", out var productElement))
                {
                    item.ProductName = productElement.GetString() ?? "Unknown Item";
                }
                
                if (toolCall.Arguments.TryGetValue("quantity", out var quantityElement))
                {
                    if (quantityElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        item.Quantity = quantityElement.GetInt32();
                    }
                    else if (int.TryParse(quantityElement.GetString(), out var qty))
                    {
                        item.Quantity = qty;
                    }
                }
                
                // Set a default price if we can't parse it
                item.UnitPrice = 1.80m; // Default kopitiam price
                
                if (!string.IsNullOrEmpty(item.ProductName) && item.Quantity > 0)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create fallback item from tool call");
            }
            
            return false;
        }

        private Transaction CreateMockTransaction()
        {
            // Create a mock Transaction with current receipt data
            var transaction = new Transaction
            {
                Id = new TransactionId(Guid.Parse(_receipt.TransactionId.PadRight(32, '0'))),
                Currency = _receipt.Store.Currency,
                State = TransactionState.Building
            };
            
            // Add all items from the receipt to the mock transaction
            foreach (var receiptItem in _receipt.Items)
            {
                var productId = new ProductId(receiptItem.ProductName); // Convert name to ProductId
                var quantity = receiptItem.Quantity;
                var unitPrice = new Money((long)(receiptItem.UnitPrice * 100), _receipt.Store.Currency); // Convert to Money
                
                transaction.AddLine(productId, quantity, unitPrice);
                _logger.LogInformation("Added receipt item to mock transaction: {ProductName} x{Quantity} @ ${UnitPrice}", 
                    receiptItem.ProductName, quantity, receiptItem.UnitPrice);
            }
            
            _logger.LogInformation("Created mock transaction with {ItemCount} items, total: ${Total}", 
                transaction.Lines.Count, transaction.Total.ToDecimal());
            
            return transaction;
        }

        private void UpdateReceiptFromMockTransaction(Transaction transaction)
        {
            // Update receipt from mock transaction changes
            // For now, keep our receipt as the source of truth
        }

        private string CreateIntelligentPrompt(string conversationContext, string userInput)
        {
            var template = _personality.OrderingTemplate
                .Replace("{conversationContext}", conversationContext)
                .Replace("{cartItems}", GetCartSummary())
                .Replace("{currentTotal}", _receipt.Total.ToString("F2"))
                .Replace("{currency}", _storeConfig.Currency)
                .Replace("{userInput}", userInput);

            return template + $@"
STORE CONTEXT:
- Store: {_storeConfig.StoreName}
- Type: {_storeConfig.StoreType}
- Currency: {_storeConfig.Currency}

Use your tools intelligently to help the customer.";
        }

        private string GetCartSummary()
        {
            if (!_receipt.Items.Any())
            {
                return "none";
            }
            
            return string.Join(", ", _receipt.Items.Select(i => $"{i.ProductName} x{i.Quantity}"));
        }

        private async Task<string> GenerateFollowUpAsync(string conversationContext, string userInput, string initialResponse, List<string> toolResults)
        {
            try
            {
                var toolSummary = string.Join("; ", toolResults);
                
                var followUpPrompt = $@"You are {_personality.StaffTitle} at {_storeConfig.StoreName}.

Customer said: '{userInput}'
Your initial response: '{initialResponse}'
System operations: {toolSummary}

Provide a brief follow-up response confirming what was done and asking what else they need.
Keep it natural and conversational.";

                var response = await _mcpClient.CompleteWithToolsAsync(followUpPrompt, Array.Empty<McpTool>());
                return response.Content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating follow-up response");
                return "Great! What else can I help you with?";
            }
        }

        private static bool IsExitCommand(string input)
        {
            var exitCommands = new[] { "exit", "quit", "bye", "goodbye", "done", "thanks", "thank you" };
            return exitCommands.Contains(input.ToLowerInvariant());
        }

        private static bool IsCompletionRequest(string input)
        {
            var completionCommands = new[] { "that's all", "that's it", "complete", "finish", "pay", "checkout", "done", "finish order", "that'll be all", "nothing else", "no", "nope", "ready" };
            return completionCommands.Any(cmd => input.ToLowerInvariant().Contains(cmd));
        }

        private bool TryParseAddedItem(string result, out ReceiptLineItem item)
        {
            item = new ReceiptLineItem();
            
            try
            {
                _logger.LogInformation("Attempting to parse item from result: '{Result}'", result);
                
                // Handle multiple possible formats:
                // Format 1: "ADDED: Kaya Toast (prep: no sugar) x2 @ $1.80 each = $3.60"
                // Format 2: "ADDED: Kopi C x1 @ $1.40 each = $1.40"
                // Format 3: Simple format without "ADDED:"
                
                var cleanResult = result;
                if (result.StartsWith("ADDED: "))
                {
                    cleanResult = result.Substring(7); // Remove "ADDED: "
                    _logger.LogInformation("Cleaned result: '{CleanResult}'", cleanResult);
                }
                
                // Try to split by " x" for quantity
                var parts = cleanResult.Split(" x");
                _logger.LogInformation("Split by ' x' resulted in {PartCount} parts: [{Parts}]", 
                    parts.Length, string.Join("', '", parts));
                
                if (parts.Length >= 2)
                {
                    var productPart = parts[0];
                    var quantityAndPricePart = parts[1];
                    _logger.LogInformation("Product part: '{ProductPart}', Quantity/Price part: '{QuantityPricePart}'", 
                        productPart, quantityAndPricePart);
                    
                    // Extract product name and prep notes
                    if (productPart.Contains(" (prep: "))
                    {
                        var prepIndex = productPart.IndexOf(" (prep: ");
                        item.ProductName = productPart.Substring(0, prepIndex).Trim();
                        var prepEnd = productPart.IndexOf(")", prepIndex);
                        if (prepEnd > prepIndex)
                        {
                            item.PreparationNotes = productPart.Substring(prepIndex + 8, prepEnd - prepIndex - 8);
                        }
                        _logger.LogInformation("Product with prep: '{ProductName}' (prep: '{PrepNotes}')", 
                            item.ProductName, item.PreparationNotes);
                    }
                    else
                    {
                        item.ProductName = productPart.Trim();
                        _logger.LogInformation("Simple product: '{ProductName}'", item.ProductName);
                    }
                    
                    // Extract quantity and price
                    var atIndex = quantityAndPricePart.IndexOf(" @ $");
                    _logger.LogInformation("Looking for ' @ $' in '{QuantityPricePart}', found at index: {AtIndex}", 
                        quantityAndPricePart, atIndex);
                    
                    if (atIndex > 0)
                    {
                        var quantityStr = quantityAndPricePart.Substring(0, atIndex).Trim();
                        _logger.LogInformation("Quantity string: '{QuantityStr}'", quantityStr);
                        
                        if (int.TryParse(quantityStr, out var quantity))
                        {
                            item.Quantity = quantity;
                            _logger.LogInformation("Parsed quantity: {Quantity}", quantity);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to parse quantity from: '{QuantityStr}'", quantityStr);
                        }
                        
                        var priceStart = atIndex + 4; // Skip " @ $"
                        var priceEnd = quantityAndPricePart.IndexOf(" each", priceStart);
                        _logger.LogInformation("Price search: start={PriceStart}, end={PriceEnd} in '{QuantityPricePart}'", 
                            priceStart, priceEnd, quantityAndPricePart);
                        
                        if (priceEnd > priceStart)
                        {
                            var priceStr = quantityAndPricePart.Substring(priceStart, priceEnd - priceStart);
                            _logger.LogInformation("Price string: '{PriceStr}'", priceStr);
                            
                            if (decimal.TryParse(priceStr, out var price))
                            {
                                item.UnitPrice = price;
                                _logger.LogInformation("Parsed unit price: ${UnitPrice}", price);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to parse price from: '{PriceStr}'", priceStr);
                            }
                        }
                        else
                        {
                            // Try to extract price from the end if "each" not found
                            var remainingPart = quantityAndPricePart.Substring(priceStart);
                            _logger.LogInformation("No 'each' found, trying to parse from remaining: '{RemainingPart}'", remainingPart);
                            var priceMatch = System.Text.RegularExpressions.Regex.Match(remainingPart, @"(\d+\.?\d*)");
                            if (priceMatch.Success && decimal.TryParse(priceMatch.Value, out var altPrice))
                            {
                                item.UnitPrice = altPrice;
                                _logger.LogInformation("Parsed alternate price: ${AltPrice}", altPrice);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to parse alternate price from: '{RemainingPart}'", remainingPart);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not find ' @ $' in quantity/price part: '{QuantityPricePart}'", quantityAndPricePart);
                    }
                    
                    var success = !string.IsNullOrEmpty(item.ProductName) && item.Quantity > 0 && item.UnitPrice > 0;
                    _logger.LogInformation("Final parsing result: Success={Success}, ProductName='{ProductName}', Quantity={Quantity}, UnitPrice=${UnitPrice}", 
                        success, item.ProductName, item.Quantity, item.UnitPrice);
                    
                    return success;
                }
                else
                {
                    _logger.LogWarning("Could not split result by ' x': '{Result}' - got {PartCount} parts", result, parts.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while parsing added item: '{Result}'", result);
            }
            
            return false;
        }
    }
}
