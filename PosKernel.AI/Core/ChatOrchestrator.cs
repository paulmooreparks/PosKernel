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

namespace PosKernel.AI.Core {
    /// <summary>
    /// Core business logic orchestrator for the POS AI demo.
    /// Manages conversation, receipt state, and tool execution.
    /// UI-agnostic - can be used with console, Terminal.Gui, or web interfaces.
    /// </summary>
    public class ChatOrchestrator {
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
        /// Gets the last prompt sent to the AI for debugging/refinement purposes.
        /// </summary>
        public string? LastPrompt { get; private set; }

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

            _receipt = new Receipt {
                Store = new StoreInfo {
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
        public async Task<ChatMessage> InitializeAsync() {
            try {
                var currentTime = DateTime.Now;
                var timeOfDay = currentTime.Hour < 12 ? "morning" : currentTime.Hour < 17 ? "afternoon" : "evening";

                if (_useRealKernel && _kernelToolsProvider != null) {
                    return await InitializeRealKernelAsync(timeOfDay);
                }
                else {
                    return await InitializeMockAsync(timeOfDay);
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error during initialization");
                var fallbackMessage = new ChatMessage {
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
        public async Task<ChatMessage> ProcessUserInputAsync(string userInput) {
            if (string.IsNullOrWhiteSpace(userInput)) {
                return new ChatMessage { Sender = "System", Content = "", IsSystem = true, ShowInCleanMode = false };
            }

            // Add user message to history
            var userMessage = new ChatMessage {
                Sender = "Customer",
                Content = userInput,
                IsSystem = false
            };
            _conversationHistory.Add(userMessage);

            // Check for exit commands
            if (IsExitCommand(userInput)) {
                var exitMessage = new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = $"Thank you for visiting {_storeConfig.StoreName}! Come back anytime!",
                    IsSystem = false
                };
                _conversationHistory.Add(exitMessage);
                return exitMessage;
            }

            // Check for completion requests
            if (IsCompletionRequest(userInput) && _receipt.Items.Any()) {
                _receipt.Status = PaymentStatus.ReadyForPayment;
                _logger.LogInformation("Order completion requested, setting status to ReadyForPayment with {ItemCount} items totaling {FormattedTotal}",
                    _receipt.Items.Count, CurrencyFormattingService.FormatWithSymbol(_receipt.Total, _storeConfig.Currency));

                var formattedTotal = CurrencyFormattingService.FormatWithSymbol(_receipt.Total, _storeConfig.Currency);
                var completionMessage = new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = $"Perfect! Your order is ready. That's {_receipt.Items.Count} items for {formattedTotal}. How would you like to pay?",
                    IsSystem = false
                };
                _conversationHistory.Add(completionMessage);
                return completionMessage;
            }

            try {
                var conversationContext = string.Join("\n", _conversationHistory.TakeLast(8).Select(m => $"{m.Sender}: {m.Content}"));
                var prompt = CreateIntelligentPrompt(conversationContext, userInput);

                // Store the prompt for UI debugging
                LastPrompt = prompt;

                var availableTools = _useRealKernel ? _kernelToolsProvider!.GetAvailableTools() : _mockToolsProvider!.GetAvailableTools();
                var response = await _mcpClient.CompleteWithToolsAsync(prompt, availableTools);

                var aiMessage = new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = response.Content,
                    IsSystem = false
                };

                // Execute any tool calls
                if (response.ToolCalls.Any()) {
                    _logger.LogInformation("Executing {ToolCallCount} tool calls", response.ToolCalls.Count);
                    var toolResults = await ExecuteToolsAsync(response.ToolCalls);
                    
                    // Check if this is a payment completion (don't generate follow-up)
                    var isPaymentCompletion = toolResults.Any(result => result.Contains("Payment processed") || result.Contains("Transaction completed"));
                    
                    if (isPaymentCompletion) {
                        _logger.LogInformation("Payment completed - using tool results directly without follow-up");
                        // For payment completion, just use the tool results as the response
                        var paymentResult = toolResults.FirstOrDefault(r => r.Contains("Payment processed")) ?? toolResults.First();
                        aiMessage.Content = FormatPaymentResponse(paymentResult);
                    } else {
                        // For non-payment tool calls, generate follow-up as normal
                        var followUpResponse = await GenerateFollowUpAsync(conversationContext, userInput, response.Content, toolResults);
                        if (!string.IsNullOrEmpty(followUpResponse)) {
                            aiMessage.Content = response.Content + " " + followUpResponse;
                        }
                    }
                }

                _conversationHistory.Add(aiMessage);
                return aiMessage;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error processing user input: {UserInput}", userInput);
                var errorMessage = new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = "Sorry, I'm having a technical issue. Please try again.",
                    IsSystem = false
                };
                _conversationHistory.Add(errorMessage);
                return errorMessage;
            }
        }

        /// <summary>
        /// Clears the prompt cache to allow live editing of prompt files during development.
        /// Call this method to reload prompts from disk without restarting the application.
        /// </summary>
        public void ReloadPrompts()
        {
            _logger.LogInformation("Reloading AI personality prompts from disk");
            AiPersonalityFactory.ClearPromptCache();
        }

        /// <summary>
        /// Checks if the current order is ready for completion.
        /// </summary>
        public bool IsOrderComplete() {
            return _receipt.Items.Any() && _receipt.Status == PaymentStatus.ReadyForPayment;
        }

        private async Task<ChatMessage> InitializeRealKernelAsync(string timeOfDay) {
            // Load menu context from real restaurant extension
            var menuContextCall = new McpToolCall {
                Id = "startup-" + Guid.NewGuid().ToString()[..8],
                FunctionName = "load_menu_context",
                Arguments = new Dictionary<string, System.Text.Json.JsonElement> {
                    ["include_categories"] = System.Text.Json.JsonSerializer.SerializeToElement(true)
                }
            };

            var menuContext = await _kernelToolsProvider!.ExecuteToolAsync(menuContextCall);

            // Use the proper greeting system with menu context injection
            var context = new PromptContext 
            {
                TimeOfDay = timeOfDay,
                CurrentTime = DateTime.Now.ToString("HH:mm")
            };
            
            var greetingPrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "greeting", context);
            
            // Append real menu context to the greeting prompt instead of hardcoding
            var enhancedGreetingPrompt = greetingPrompt + $@"

## REAL MENU INTELLIGENCE LOADED:
{menuContext}

## GREETING ENHANCEMENT:
- Use SPECIFIC items from your actual menu above (not generic terms)
- Show you know what you serve with actual product names and prices
- Mention 2-3 popular items naturally in your greeting
- Keep it conversational and match your personality style

Store: {_storeConfig.StoreName} | Currency: {_storeConfig.Currency}";

            // Store the prompt for UI debugging
            LastPrompt = enhancedGreetingPrompt;

            var availableTools = _kernelToolsProvider.GetAvailableTools();
            var response = await _mcpClient.CompleteWithToolsAsync(enhancedGreetingPrompt, availableTools);

            var greetingMessage = new ChatMessage {
                Sender = _personality.StaffTitle,
                Content = response.Content,
                IsSystem = false
            };

            _conversationHistory.Add(greetingMessage);
            return greetingMessage;
        }

        private async Task<ChatMessage> InitializeMockAsync(string timeOfDay) {
            var context = new PromptContext 
            {
                TimeOfDay = timeOfDay,
                CurrentTime = DateTime.Now.ToString("HH:mm")
            };
            
            var greetingPrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "greeting", context);

            // Store the prompt for UI debugging
            LastPrompt = greetingPrompt;

            var availableTools = _mockToolsProvider!.GetAvailableTools();
            var response = await _mcpClient.CompleteWithToolsAsync(greetingPrompt, availableTools);

            var greetingMessage = new ChatMessage {
                Sender = _personality.StaffTitle,
                Content = response.Content,
                IsSystem = false
            };

            _conversationHistory.Add(greetingMessage);
            return greetingMessage;
        }

        private async Task<List<string>> ExecuteToolsAsync(IReadOnlyList<McpToolCall> toolCalls) {
            var results = new List<string>();

            foreach (var toolCall in toolCalls) {
                try {
                    string result;
                    if (_useRealKernel && _kernelToolsProvider != null) {
                        result = await _kernelToolsProvider.ExecuteToolAsync(toolCall);
                    }
                    else if (_mockToolsProvider != null) {
                        // Mock tools need a transaction - create a simple adapter
                        var mockTransaction = CreateMockTransaction();
                        result = await _mockToolsProvider.ExecuteToolAsync(toolCall, mockTransaction);
                        UpdateReceiptFromMockTransaction(mockTransaction);
                    }
                    else {
                        result = "Tool execution not available";
                    }

                    results.Add(result);
                    UpdateReceiptFromToolResult(toolCall, result);
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error executing tool {FunctionName}", toolCall.FunctionName);
                    results.Add($"Error executing {toolCall.FunctionName}: {ex.Message}");
                }
            }

            return results;
        }

        private void UpdateReceiptFromToolResult(McpToolCall toolCall, string result) {
            _logger.LogInformation("Updating receipt from tool result: {FunctionName} -> {Result}", toolCall.FunctionName, result);

            // Parse tool results and update receipt
            if (toolCall.FunctionName == "add_item_to_transaction") {
                _logger.LogInformation("Processing add_item_to_transaction with result: {Result}", result);

                // Check for disambiguation response first
                if (result.StartsWith("DISAMBIGUATION_NEEDED:")) {
                    _logger.LogInformation("Disambiguation needed - presenting options to user");
                    // Don't try to parse as an item - this is handled by the AI conversation
                    return;
                }

                // Check for other non-item responses
                if (result.StartsWith("ERROR:") || result.StartsWith("FAILED:") || result.StartsWith("INVALID:")) {
                    _logger.LogWarning("Tool execution returned error: {Result}", result);
                    return;
                }

                if (TryParseAddedItem(result, out var item)) {
                    _logger.LogInformation("Successfully parsed item: {ProductName} x{Quantity} @ {UnitPrice} = {LineTotal}",
                        item.ProductName, item.Quantity, 
                        CurrencyFormattingService.FormatWithSymbol(item.UnitPrice, _storeConfig.Currency),
                        CurrencyFormattingService.FormatWithSymbol(item.CalculatedTotal, _storeConfig.Currency));
                    _receipt.Items.Add(item);
                    _logger.LogInformation("Receipt now has {ItemCount} items, total: {Total}",
                        _receipt.Items.Count, CurrencyFormattingService.FormatWithSymbol(_receipt.Total, _storeConfig.Currency));
                }
                else {
                    _logger.LogWarning("Failed to parse item from result: {Result}", result);

                    // Fallback: Try to extract basic info from the result
                    if (TryCreateFallbackItem(result, toolCall, out var fallbackItem)) {
                        _logger.LogInformation("Created fallback item: {ProductName} x{Quantity} @ {UnitPrice}",
                            fallbackItem.ProductName, fallbackItem.Quantity, 
                            CurrencyFormattingService.FormatWithSymbol(fallbackItem.UnitPrice, _storeConfig.Currency));
                        _receipt.Items.Add(fallbackItem);
                        _logger.LogInformation("Receipt now has {ItemCount} items (fallback), total: {Total}",
                            _receipt.Items.Count, CurrencyFormattingService.FormatWithSymbol(_receipt.Total, _storeConfig.Currency));
                    }
                    else {
                        _logger.LogError("Failed to create any item from tool result: {Result}", result);
                    }
                }
            }
            else if (toolCall.FunctionName == "process_payment" && result.Contains("Payment processed")) {
                _receipt.Status = PaymentStatus.Completed;
                _logger.LogInformation("Payment processed, receipt status updated to Completed");

                // DON'T start new transaction immediately - let UI handle the post-payment flow
                // The UI will detect the Completed status and generate the post-payment message,
                // then the new transaction will be started in the post-payment flow
            }
        }

        /// <summary>
        /// Starts a new transaction after the previous one was completed and paid.
        /// This ensures proper transaction lifecycle management.
        /// </summary>
        private void StartNewTransactionAfterPayment() {
            _logger.LogInformation("Starting new transaction after payment completion");

            // Clear the current receipt and start fresh
            _receipt.Items.Clear();
            _receipt.Status = PaymentStatus.Building;
            _receipt.TransactionId = Guid.NewGuid().ToString()[..8]; // New transaction ID

            _logger.LogInformation("New transaction started: {TransactionId}", _receipt.TransactionId);
        }

        /// <summary>
        /// Generates a post-payment acknowledgment message for natural conversation flow.
        /// This should be called by the UI after payment completion to provide closure.
        /// </summary>
        public async Task<ChatMessage> GeneratePostPaymentMessageAsync() {
            try {
                var timeOfDay = DateTime.Now.Hour < 12 ? "morning" : DateTime.Now.Hour < 17 ? "afternoon" : "evening";
                
                // Use dedicated post-payment prompt if available, fallback to contextual prompt
                var context = new PromptContext 
                {
                    TimeOfDay = timeOfDay,
                    CurrentTime = DateTime.Now.ToString("HH:mm"),
                    // IMPORTANT: Don't pass conversation context - this is a fresh interaction with a new customer
                    ConversationContext = null,
                    CartItems = "none",
                    CurrentTotal = "0.00",
                    Currency = _storeConfig.Currency
                };

                string postPaymentPrompt;
                try {
                    // Try to load dedicated post-payment prompt
                    postPaymentPrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "post-payment", context);
                }
                catch {
                    // Fallback to contextual prompt if post-payment.md doesn't exist
                    postPaymentPrompt = $@"You are {_personality.StaffTitle} at {_storeConfig.StoreName},

## POST-PAYMENT SITUATION:
- Previous customer just completed their transaction successfully
- That customer has left and you now have a NEW CUSTOMER approaching
- You should greet this NEW customer warmly 
- Don't reference the previous transaction - this is a fresh interaction
- Be ready to take a completely new order from this new customer
- Time of day: {timeOfDay}

Greet the new customer and offer to take their fresh order:";
                }

                LastPrompt = postPaymentPrompt;

                var availableTools = _useRealKernel ? _kernelToolsProvider!.GetAvailableTools() : _mockToolsProvider!.GetAvailableTools();
                var response = await _mcpClient.CompleteWithToolsAsync(postPaymentPrompt, availableTools);

                var postPaymentMessage = new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = response.Content,
                    IsSystem = false
                };

                // CLEAR the conversation history for the new customer interaction
                _conversationHistory.Clear();
                _conversationHistory.Add(postPaymentMessage);

                // NOW start the new transaction after the post-payment message is generated
                StartNewTransactionAfterPayment();

                return postPaymentMessage;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error generating post-payment message");
                
                // Fallback message based on personality - treat as NEW CUSTOMER
                var fallbackContent = _personality.Type switch {
                    PersonalityType.SingaporeanKopitiamUncle => "Next customer! What you want?",
                    PersonalityType.AmericanBarista => "Hi there! Welcome to the coffee shop. What can I get started for you?",
                    PersonalityType.FrenchBoulanger => "Bonjour! What can I get for you today?",
                    PersonalityType.JapaneseConbiniClerk => "Irasshaimase! Welcome! How may I help you?",
                    PersonalityType.IndianChaiWala => "Namaste! Fresh chai ready! What you want?",
                    _ => "Welcome! How can I help you today?"
                };
                
                var fallbackMessage = new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = fallbackContent,
                    IsSystem = false
                };

                // Clear conversation history for new customer
                _conversationHistory.Clear();
                _conversationHistory.Add(fallbackMessage);

                // Start new transaction even in fallback case
                StartNewTransactionAfterPayment();
                
                return fallbackMessage;
            }
        }

        private bool TryCreateFallbackItem(string result, McpToolCall toolCall, out ReceiptLineItem item) {
            item = new ReceiptLineItem();

            try {
                // Try to extract from the tool call arguments if parsing the result fails
                if (toolCall.Arguments.TryGetValue("product_name", out var productElement)) {
                    item.ProductName = productElement.GetString() ?? "Unknown Item";
                }

                if (toolCall.Arguments.TryGetValue("quantity", out var quantityElement)) {
                    if (quantityElement.ValueKind == System.Text.Json.JsonValueKind.Number) {
                        item.Quantity = quantityElement.GetInt32();
                    }
                    else if (int.TryParse(quantityElement.GetString(), out var qty)) {
                        item.Quantity = qty;
                    }
                }

                // Set a default price if we can't parse it
                item.UnitPrice = 1.80m; // Default kopitiam price

                if (!string.IsNullOrEmpty(item.ProductName) && item.Quantity > 0) {
                    return true;
                }
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to create fallback item from tool call");
            }

            return false;
        }

        private Transaction CreateMockTransaction() {
            // Create a mock Transaction with current receipt data
            var transaction = new Transaction {
                Id = new TransactionId(Guid.Parse(_receipt.TransactionId.PadRight(32, '0'))),
                Currency = _receipt.Store.Currency,
                State = TransactionState.Building
            };

            // Add all items from the receipt to the mock transaction
            foreach (var receiptItem in _receipt.Items) {
                var productId = new ProductId(receiptItem.ProductName); // Convert name to ProductId
                var quantity = receiptItem.Quantity;
                var unitPrice = new Money((long) (receiptItem.UnitPrice * 100), _receipt.Store.Currency); // Convert to Money

                transaction.AddLine(productId, quantity, unitPrice);
                _logger.LogInformation("Added receipt item to mock transaction: {ProductName} x{Quantity} @ ${UnitPrice}",
                    receiptItem.ProductName, quantity, receiptItem.UnitPrice);
            }

            _logger.LogInformation("Created mock transaction with {ItemCount} items, total: {Total}",
                transaction.Lines.Count, CurrencyFormattingService.FormatWithSymbol(transaction.Total.ToDecimal(), _storeConfig.Currency));

            return transaction;
        }

        private void UpdateReceiptFromMockTransaction(Transaction transaction) {
            // Update receipt from mock transaction changes
            // For now, keep our receipt as the source of truth
        }

        private string CreateIntelligentPrompt(string conversationContext, string userInput) {
            var context = new PromptContext {
                ConversationContext = conversationContext,
                CartItems = GetCartSummary(),
                CurrentTotal = CurrencyFormattingService.FormatAmount(_receipt.Total, _storeConfig.Currency),
                Currency = _storeConfig.Currency,
                UserInput = userInput
            };

            var prompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "ordering", context);

            // Append store-specific context
            return prompt + $@"
STORE CONTEXT:
- Store: {_storeConfig.StoreName}
- Type: {_storeConfig.StoreType}
- Currency: {_storeConfig.Currency}

Use your tools intelligently to help the customer.";
        }

        private string GetCartSummary() {
            if (!_receipt.Items.Any()) {
                return "none";
            }

            return string.Join(", ", _receipt.Items.Select(i => $"{i.ProductName} x{i.Quantity}"));
        }

        private async Task<string> GenerateFollowUpAsync(string conversationContext, string userInput, string initialResponse, List<string> toolResults) {
            try {
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
            catch (Exception ex) {
                _logger.LogError(ex, "Error generating follow-up response");
                return "Great! What else can I help you with?";
            }
        }

        private static bool IsExitCommand(string input) {
            var exitCommands = new[] { "exit", "quit", "bye", "goodbye", "done", "thanks", "thank you" };
            return exitCommands.Contains(input.ToLowerInvariant());
        }

        private static bool IsCompletionRequest(string input) {
            var completionCommands = new[] { "that's all", "that's it", "complete", "finish", "pay", "checkout", "done", "finish order", "that'll be all", "nothing else", "no", "nope", "ready" };
            return completionCommands.Any(cmd => input.ToLowerInvariant().Contains(cmd));
        }

        private bool TryParseAddedItem(string result, out ReceiptLineItem item) {
            item = new ReceiptLineItem();

            try {
                _logger.LogInformation("Attempting to parse item from result: '{Result}'", result);

                // Handle multiple possible formats:
                // Format 1: "ADDED: Kaya Toast (prep: no sugar) x2 @ $1.80 each = $3.60"
                // Format 2: "ADDED: Kopi C x1 @ $1.40 each = $1.40"
                // Format 3: Simple format without "ADDED:"

                var cleanResult = result;
                if (result.StartsWith("ADDED: ")) {
                    cleanResult = result.Substring(7); // Remove "ADDED: "
                    _logger.LogInformation("Cleaned result: '{CleanResult}'", cleanResult);
                }

                // Try to split with regex to handle multiple spaces or delimiters
                var parts = System.Text.RegularExpressions.Regex.Split(cleanResult, @"\s*x\s*");
                _logger.LogInformation("Split by regex resulted in {PartCount} parts: [{Parts}]",
                    parts.Length, string.Join("', '", parts));

                if (parts.Length >= 2) {
                    var productPart = parts[0];
                    var quantityAndPricePart = parts[1];
                    _logger.LogInformation("Product part: '{ProductPart}', Quantity/Price part: '{QuantityPricePart}'",
                        productPart, quantityAndPricePart);

                    // Extract product name and prep notes
                    if (productPart.Contains(" (prep: ")) {
                        var prepIndex = productPart.IndexOf(" (prep: ");
                        item.ProductName = productPart.Substring(0, prepIndex).Trim();
                        var prepEnd = productPart.IndexOf(")", prepIndex);

                        if (prepEnd > prepIndex) {
                            item.PreparationNotes = productPart.Substring(prepIndex + 8, prepEnd - prepIndex - 8);
                        }
                        _logger.LogInformation("Product with prep: '{ProductName}' (prep: '{PrepNotes}')",
                            item.ProductName, item.PreparationNotes);
                    }
                    else {
                        item.ProductName = productPart.Trim();
                        _logger.LogInformation("Simple product: '{ProductName}'", item.ProductName);
                    }

                    // Extract quantity and price
                    var atIndex = quantityAndPricePart.IndexOf(" @ $");
                    _logger.LogInformation("Looking for ' @ $' in '{QuantityPricePart}', found at index: {AtIndex}",
                        quantityAndPricePart, atIndex);

                    if (atIndex > 0) {
                        var quantityStr = quantityAndPricePart.Substring(0, atIndex).Trim();
                        _logger.LogInformation("Quantity string: '{QuantityStr}'", quantityStr);

                        if (int.TryParse(quantityStr, out var quantity)) {
                            item.Quantity = quantity;
                            _logger.LogInformation("Parsed quantity: {Quantity}", quantity);
                        }
                        else {
                            _logger.LogWarning("Failed to parse quantity from: '{QuantityStr}'", quantityStr);
                        }

                        var priceStart = atIndex + 4; // Skip " @ $"
                        var priceEnd = quantityAndPricePart.IndexOf(" each", priceStart);
                        _logger.LogInformation("Price search: start={PriceStart}, end={PriceEnd} in '{QuantityPricePart}'",
                            priceStart, priceEnd, quantityAndPricePart);

                        if (priceEnd > priceStart) {
                            var priceStr = quantityAndPricePart.Substring(priceStart, priceEnd - priceStart);
                            _logger.LogInformation("Price string: '{PriceStr}'", priceStr);

                            if (decimal.TryParse(priceStr, out var price)) {
                                item.UnitPrice = price;
                                _logger.LogInformation("Parsed unit price: {UnitPrice}", 
                                    CurrencyFormattingService.FormatWithSymbol(price, _storeConfig.Currency));
                            }
                            else {
                                _logger.LogWarning("Failed to parse price from: '{PriceStr}'", priceStr);
                            }
                        }
                        else {
                            // Try to extract price from the end if "each" not found
                            var remainingPart = quantityAndPricePart.Substring(priceStart);
                            _logger.LogInformation("No 'each' found, trying to parse from remaining: '{RemainingPart}'", remainingPart);
                            var priceMatch = System.Text.RegularExpressions.Regex.Match(remainingPart, @"(\d+\.?\d*)");

                            if (priceMatch.Success && decimal.TryParse(priceMatch.Value, out var altPrice)) {
                                item.UnitPrice = altPrice;
                                _logger.LogInformation("Parsed alternate price: {AltPrice}", 
                                    CurrencyFormattingService.FormatWithSymbol(altPrice, _storeConfig.Currency));
                            }
                            else {
                                _logger.LogWarning("Failed to parse alternate price from: '{RemainingPart}'", remainingPart);
                            }
                        }
                    }
                    else {
                        _logger.LogWarning("Could not find ' @ $' in quantity/price part: '{QuantityPricePart}'", quantityAndPricePart);
                    }

                    var success = !string.IsNullOrEmpty(item.ProductName) && item.Quantity > 0 && item.UnitPrice > 0;
                    _logger.LogInformation("Final parsing result: Success={Success}, ProductName='{ProductName}', Quantity={Quantity}, UnitPrice={UnitPrice}",
                        success, item.ProductName, item.Quantity, 
                        CurrencyFormattingService.FormatWithSymbol(item.UnitPrice, _storeConfig.Currency));

                    return success;
                }
                else {
                    _logger.LogWarning("Could not split result by ' x': '{Result}' - got {PartCount} parts", result, parts.Length);
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Exception while parsing added item: '{Result}'", result);
            }

            return false;
        }

        /// <summary>
        /// Formats a payment completion response in the personality's style.
        /// </summary>
        private string FormatPaymentResponse(string paymentResult)
        {
            try {
                // Parse the payment result to extract key information
                var lines = paymentResult.Split('\n');
                var amountLine = lines.FirstOrDefault(l => l.Contains("Payment processed:"))?.Trim();
                var totalLine = lines.FirstOrDefault(l => l.StartsWith("Total:"))?.Trim();
                var changeLine = lines.FirstOrDefault(l => l.StartsWith("Change due:"))?.Trim();

                if (amountLine == null) {
                    return paymentResult; // Fallback to raw result
                }

                // Create personality-appropriate payment confirmation
                var response = _personality.Type switch {
                    PersonalityType.SingaporeanKopitiamUncle => FormatKopitiamPaymentResponse(amountLine, totalLine, changeLine),
                    PersonalityType.AmericanBarista => FormatAmericanPaymentResponse(amountLine, totalLine, changeLine),
                    PersonalityType.FrenchBoulanger => FormatFrenchPaymentResponse(amountLine, totalLine, changeLine),
                    PersonalityType.JapaneseConbiniClerk => FormatJapanesePaymentResponse(amountLine, totalLine, changeLine),
                    PersonalityType.IndianChaiWala => FormatIndianPaymentResponse(amountLine, totalLine, changeLine),
                    _ => FormatGenericPaymentResponse(amountLine, totalLine, changeLine)
                };

                return response;
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Error formatting payment response, using raw result");
                return paymentResult; // Fallback to raw payment result
            }
        }

        private string FormatKopitiamPaymentResponse(string amountLine, string? totalLine, string? changeLine)
        {
            var amount = ExtractAmount(amountLine);
            var change = ExtractAmount(changeLine ?? "Change due: $0.00");

            if (change > 0) {
                return $"Thank you! {amountLine?.Replace("Payment processed: ", "")} received. Your change is ${change:F2}. "
                    + "Come again!";
            } else {
                return $"Thank you! {amountLine?.Replace("Payment processed: ", "")} received. Exact amount, no change. "
                    + "See you next time!";
            }
        }

        private string FormatAmericanPaymentResponse(string amountLine, string? totalLine, string? changeLine)
        {
            var amount = ExtractAmount(amountLine);
            var change = ExtractAmount(changeLine ?? "Change due: $0.00");

            if (change > 0) {
                return $"Perfect! {amountLine?.Replace("Payment processed: ", "")} received. Here's your change of ${change:F2}. Have a great day!";
            } else {
                return $"Awesome! {amountLine?.Replace("Payment processed: ", "")} received. Exact change - you're all set!";
            }
        }

        private string FormatFrenchPaymentResponse(string amountLine, string? totalLine, string? changeLine)
        {
            var change = ExtractAmount(changeLine ?? "Change due: $0.00");

            if (change > 0) {
                return $"Merci beaucoup! {amountLine?.Replace("Payment processed: ", "")} received. Voici your change: ${change:F2}. À bientôt!";
            } else {
                return $"Parfait! {amountLine?.Replace("Payment processed: ", "")} reçu. Montant exact - merci!";
            }
        }

        private string FormatJapanesePaymentResponse(string amountLine, string? totalLine, string? changeLine)
        {
            var change = ExtractAmount(changeLine ?? "Change due: $0.00");

            if (change > 0) {
                return $"Arigatou gozaimasu! {amountLine?.Replace("Payment processed: ", "")} received. Your change: ${change:F2}. またお越しくださいませ!";
            } else {
                return $"Arigatou gozaimasu! {amountLine?.Replace("Payment processed: ", "")} received. Perfect amount! またね!";
            }
        }

        private string FormatIndianPaymentResponse(string amountLine, string? totalLine, string? changeLine)
        {
            var change = ExtractAmount(changeLine ?? "Change due: $0.00");

            if (change > 0) {
                return $"Thank you, sahib! {amountLine?.Replace("Payment processed: ", "")} received. Your change: ${change:F2}. Khush rahiye!";
            } else {
                return $"Perfect, sahib! {amountLine?.Replace("Payment processed: ", "")} received. Exact amount! Aapka din shubh ho!";
            }
        }

        private string FormatGenericPaymentResponse(string amountLine, string? totalLine, string? changeLine)
        {
            var change = ExtractAmount(changeLine ?? "Change due: $0.00");

            if (change > 0) {
                return $"Thank you! {amountLine?.Replace("Payment processed: ", "")} received. Your change is ${change:F2}.";
            } else {
                return $"Thank you! {amountLine?.Replace("Payment processed: ", "")} received. Exact amount.";
            }
        }

        private decimal ExtractAmount(string line)
        {
            try {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"\$(\d+\.?\d*)");
                if (match.Success && decimal.TryParse(match.Groups[1].Value, out var amount)) {
                    return amount;
                }
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Error extracting amount from line: {Line}", line);
            }
            return 0m;
        }
    }
}
