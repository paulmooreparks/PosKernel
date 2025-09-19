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
using PosKernel.Configuration.Services;
using Microsoft.Extensions.Logging;
using System.Text;

namespace PosKernel.AI.Core {
    /// <summary>
    /// Core business logic orchestrator for the POS AI demo.
    /// Clean state machine approach for reliable payment processing.
    /// </summary>
    public class ChatOrchestrator {
        private readonly McpClient _mcpClient;
        private readonly KernelPosToolsProvider? _kernelToolsProvider;
        private readonly PosToolsProvider? _mockToolsProvider;
        private readonly ILogger<ChatOrchestrator> _logger;
        private readonly AiPersonalityConfig _personality;
        private readonly StoreConfig _storeConfig;
        private readonly ICurrencyFormattingService? _currencyFormatter;

        private readonly Receipt _receipt;
        private readonly List<ChatMessage> _conversationHistory;
        private readonly PaymentStateMachine _paymentState;
        private bool _useRealKernel;
        private ThoughtLogger _thoughtLogger;
        private OrderCompletionAnalyzer _orderAnalyzer;

        public Receipt CurrentReceipt => _receipt;
        public IReadOnlyList<ChatMessage> ConversationHistory => _conversationHistory;
        public StoreConfig StoreConfig => _storeConfig;
        public AiPersonalityConfig Personality => _personality;
        public string? LastPrompt { get; private set; }

        /// <summary>
        /// Updates the thought logger with the actual log display from the UI.
        /// Call this after UI initialization to enable thought logging.
        /// </summary>
        public void SetLogDisplay(ILogDisplay logDisplay)
        {
            _thoughtLogger?.Dispose();
            _thoughtLogger = new ThoughtLogger(logDisplay);
            _orderAnalyzer = new OrderCompletionAnalyzer(_thoughtLogger);
            _logger.LogInformation("Thought logger updated with UI log display");
        }

        public ChatOrchestrator(
            McpClient mcpClient,
            AiPersonalityConfig personality,
            StoreConfig storeConfig,
            ILogger<ChatOrchestrator> logger,
            KernelPosToolsProvider? kernelToolsProvider = null,
            PosToolsProvider? mockToolsProvider = null,
            ICurrencyFormattingService? currencyFormatter = null)
        {
            _mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
            _personality = personality ?? throw new ArgumentNullException(nameof(personality));
            _storeConfig = storeConfig ?? throw new ArgumentNullException(nameof(storeConfig));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kernelToolsProvider = kernelToolsProvider;
            _mockToolsProvider = mockToolsProvider;
            _currencyFormatter = currencyFormatter;

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
            _paymentState = new PaymentStateMachine();
            
            // Initialize thought logger with a temporary placeholder - will be updated when UI is available
            _thoughtLogger = new ThoughtLogger(new NullLogDisplay());
            _orderAnalyzer = new OrderCompletionAnalyzer(_thoughtLogger);

            _logger.LogInformation("ChatOrchestrator initialized for {StoreName} using {Integration}",
                storeConfig.StoreName, _useRealKernel ? "Real Kernel" : "Mock");
        }

        public async Task<ChatMessage> InitializeAsync() {
            try {
                var timeOfDay = DateTime.Now.Hour < 12 ? "morning" : DateTime.Now.Hour < 17 ? "afternoon" : "evening";
                var context = new PosKernel.AI.Services.PromptContext { TimeOfDay = timeOfDay, CurrentTime = DateTime.Now.ToString("HH:mm") };
                var greetingPrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "greeting", context);

                LastPrompt = greetingPrompt;
                var availableTools = _useRealKernel ? _kernelToolsProvider!.GetAvailableTools() : _mockToolsProvider!.GetAvailableTools();
                var response = await _mcpClient.CompleteWithToolsAsync(greetingPrompt, availableTools);

                var greetingMessage = new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = response.Content,
                    IsSystem = false
                };

                _conversationHistory.Add(greetingMessage);
                return greetingMessage;
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

        public async Task<ChatMessage> ProcessUserInputAsync(string userInput)
        {
            _logger.LogInformation("Processing user input: {Input}", userInput);
            
            // Enhanced intent analysis with conversation context
            var intentAnalysis = _orderAnalyzer.AnalyzeOrderIntent(userInput, _receipt, _conversationHistory);
            
            _thoughtLogger.LogThought($"ENHANCED_INTENT_ANALYSIS: Intent: {intentAnalysis.Intent}, Confidence: {intentAnalysis.ConfidenceLevel:F2}, Reason: {intentAnalysis.ReasonCode}");
            
            if (intentAnalysis.IsPaymentMethodResponse)
            {
                _thoughtLogger.LogThought("CONVERSATION_CONTEXT: User is responding to payment method question - processing as payment intent");
            }
            
            if (intentAnalysis.IsOrderSummaryResponse)
            {
                _thoughtLogger.LogThought("CONVERSATION_CONTEXT: User is responding after order summary - considering payment context");
            }

            // Handle with enhanced ordering logic
            var response = await HandleWithEnhancedOrdering(userInput, intentAnalysis);
            
            // Add to conversation history for context
            _conversationHistory.Add(new ChatMessage
            {
                Sender = "Customer", 
                Content = userInput, 
                Timestamp = DateTime.Now,
                IsSystem = false,
                ShowInCleanMode = true
            });
            
            _conversationHistory.Add(response);
            
            return response;
        }

        private async Task<ChatMessage> HandleWithEnhancedOrdering(string userInput, OrderIntentAnalysis intentAnalysis)
        {
            _thoughtLogger.LogThought(
                $"ENHANCED_ORDER_HANDLING: Intent: {intentAnalysis.Intent}, Confidence: {intentAnalysis.ConfidenceLevel:F2}, " +
                $"PaymentMethodResponse: {intentAnalysis.IsPaymentMethodResponse}, " +
                $"OrderSummaryResponse: {intentAnalysis.IsOrderSummaryResponse}, " +
                $"Items in receipt: {_receipt.Items.Count}, Receipt status: {_receipt.Status}");

            // ARCHITECTURAL FIX: If we have items in receipt, ensure payment context is preserved
            if (_receipt.Items.Any() && _receipt.Status != PaymentStatus.Completed)
            {
                _receipt.Status = PaymentStatus.ReadyForPayment;
                _thoughtLogger.LogThought("RECEIPT_STATUS_CORRECTED: Set to ReadyForPayment due to existing items");
            }

            // ARCHITECTURAL ENHANCEMENT: Prioritize conversation context over structural analysis
            if (intentAnalysis.IsPaymentMethodResponse && intentAnalysis.Intent == OrderIntent.ProcessPayment)
            {
                _thoughtLogger.LogThought("PAYMENT_CONTEXT_DETECTED: User responding to payment method question - processing payment immediately");
                return await ProcessUserRequestAsync(userInput, "payment");
            }

            // Handle other intents with enhanced context awareness
            switch (intentAnalysis.Intent)
            {
                case OrderIntent.ContinueOrdering:
                    return await ProcessUserRequestAsync(userInput, "ordering");
                    
                case OrderIntent.ReadyForPayment:
                    // Enhanced: Check if we should transition to payment
                    if (_receipt.Items.Any())
                    {
                        _thoughtLogger.LogThought("ORDER_COMPLETION_CONTEXT: Order completion detected - asking for payment method");
                        _receipt.Status = PaymentStatus.ReadyForPayment;
                        return await CreateOrderSummaryWithPaymentPrompt();
                    }
                    else
                    {
                        _thoughtLogger.LogThought("ORDER_COMPLETION_NO_ITEMS: Customer indicating completion but no items in order");
                        return await ProcessUserRequestAsync(userInput, "ordering");
                    }
                    
                case OrderIntent.ProcessPayment:
                    _thoughtLogger.LogThought("DIRECT_PAYMENT_INTENT: Processing as payment request");
                    return await ProcessUserRequestAsync(userInput, "payment");
                    
                case OrderIntent.Question:
                    return await ProcessUserRequestAsync(userInput, "ordering"); // Questions go through normal ordering flow
                    
                default:
                    // Enhanced: Consider conversation context for ambiguous inputs
                    if (intentAnalysis.IsOrderSummaryResponse && _receipt.Status == PaymentStatus.ReadyForPayment)
                    {
                        _thoughtLogger.LogThought("CONTEXT_BASED_PAYMENT: Ambiguous input after order summary - treating as potential payment method");
                        return await ProcessUserRequestAsync(userInput, "payment");
                    }
                    
                    // CRITICAL FIX: If we have items and user says something payment-related, treat as payment
                    if (_receipt.Items.Any() && LooksLikePaymentMethod(userInput))
                    {
                        _thoughtLogger.LogThought("ITEMS_EXIST_PAYMENT_METHOD: Have items and input looks like payment method - processing payment");
                        return await ProcessUserRequestAsync(userInput, "payment");
                    }
                    
                    return await ProcessUserRequestAsync(userInput, "ordering");
            }
        }

        /// <summary>
        /// ARCHITECTURAL FIX: Remove hardcoded payment method detection - let AI handle semantically
        /// </summary>
        private bool LooksLikePaymentMethod(string input)
        {
            var normalized = input.ToLowerInvariant().Trim();
            
            // ARCHITECTURAL PRINCIPLE: Only use universal structural patterns, not specific payment terms
            // Length indicators - payment methods are typically short responses in conversation context
            if (normalized.Length > 25) {
                return false; // Very long responses unlikely to be payment methods
            }
            
            // Only check if this looks like a single-word/short response (structural pattern)
            // Let AI semantic understanding handle the actual payment method recognition
            var wordCount = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            return wordCount <= 3; // Short responses in payment context are likely payment methods
        }

        /// <summary>
        /// ARCHITECTURAL ENHANCEMENT: Two-Phase AI Pattern - separates tool execution from conversational response.
        /// Phase 1: Determine and execute tools (silent)
        /// Phase 2: Generate personality-driven conversational acknowledgment (always has content)
        /// </summary>
        private async Task<ChatMessage> ProcessUserRequestAsync(string userInput, string contextHint)
        {
            try
            {
                var promptContext = new PosKernel.AI.Services.PromptContext
                {
                    UserInput = userInput,
                    TimeOfDay = DateTime.Now.Hour < 12 ? "morning" : DateTime.Now.Hour < 17 ? "afternoon" : "evening",
                    CurrentTime = DateTime.Now.ToString("HH:mm"),
                    CartItems = _receipt.Items.Any() ? 
                        string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}")) : 
                        "none",
                    CurrentTotal = FormatCurrency(_receipt.Total),
                    Currency = _receipt.Store.Currency
                };

                // PHASE 1: Tool Analysis and Execution (Silent)
                _thoughtLogger.LogThought($"TWO_PHASE_PATTERN: Starting Phase 1 - Tool Analysis and Execution");
                
                var toolAnalysisPrompt = BuildToolAnalysisPrompt(promptContext, contextHint);
                LastPrompt = toolAnalysisPrompt;
                var availableTools = _useRealKernel ? _kernelToolsProvider!.GetAvailableTools() : _mockToolsProvider!.GetAvailableTools();
                
                var toolResponse = await _mcpClient.CompleteWithToolsAsync(toolAnalysisPrompt, availableTools);
                
                _thoughtLogger.LogThought($"PHASE_1_COMPLETE: Tools returned={toolResponse.ToolCalls?.Count ?? 0}, Content length={toolResponse.Content?.Length ?? 0}");
                
                // Log the actual tools that were detected
                if (toolResponse.ToolCalls?.Any() == true)
                {
                    foreach (var toolCall in toolResponse.ToolCalls)
                    {
                        _thoughtLogger.LogThought($"DETECTED_TOOL: {toolCall.FunctionName} with args: {string.Join(", ", toolCall.Arguments?.Select(kvp => $"{kvp.Key}={kvp.Value}") ?? new string[0])}");
                    }
                }

                // Execute tools if any were identified
                List<string>? toolResults = null;
                if (toolResponse.ToolCalls?.Any() == true)
                {
                    _thoughtLogger.LogThought($"EXECUTING_TOOLS: {string.Join(", ", toolResponse.ToolCalls.Select(t => t.FunctionName))}");
                    toolResults = await ExecuteToolsAsync(toolResponse.ToolCalls);
                    var toolResultsSummary = string.Join(" | ", toolResults);
                    _logger.LogInformation("Tool execution results: {Results}", toolResultsSummary);
                    
                    // ENHANCED DEBUG: Log each tool result individually
                    for (int i = 0; i < toolResponse.ToolCalls.Count && i < toolResults.Count; i++)
                    {
                        _thoughtLogger.LogThought($"TOOL_RESULT[{toolResponse.ToolCalls[i].FunctionName}]: {toolResults[i]}");
                    }
                    
                    // Update receipt status after tool execution
                    if (toolResponse.ToolCalls.Any(t => t.FunctionName == "process_payment"))
                    {
                        _receipt.Status = PaymentStatus.Completed;
                        _thoughtLogger.LogThought("PAYMENT_COMPLETED: Receipt status updated");
                    }
                    
                    // CRITICAL DEBUG: Log receipt state after tool execution
                    _thoughtLogger.LogThought($"RECEIPT_AFTER_TOOLS: Items={_receipt.Items.Count}, Total={_receipt.Total:F2}, Status={_receipt.Status}");
                }

                // PHASE 2: Conversational Response Generation (Always has content)
                _thoughtLogger.LogThought($"TWO_PHASE_PATTERN: Starting Phase 2 - Conversational Response Generation");
                
                string conversationalResponse;
                if (toolResponse.ToolCalls?.Any() == true)
                {
                    // Generate tool acknowledgment response using personality templates
                    conversationalResponse = await GenerateToolAcknowledgmentAsync(toolResponse.ToolCalls, userInput, promptContext);
                    _thoughtLogger.LogThought($"PHASE_2_TOOL_ACKNOWLEDGMENT: Generated response length={conversationalResponse.Length}");
                }
                else
                {
                    // No tools executed - generate direct conversational response
                    conversationalResponse = await GenerateDirectConversationAsync(userInput, promptContext, contextHint);
                    _thoughtLogger.LogThought($"PHASE_2_DIRECT_CONVERSATION: Generated response length={conversationalResponse.Length}");
                }

                // ARCHITECTURAL PRINCIPLE: Phase 2 always generates content
                if (string.IsNullOrEmpty(conversationalResponse))
                {
                    _thoughtLogger.LogThought("CRITICAL_FAILURE: Phase 2 returned empty content - this violates two-phase pattern");
                    throw new InvalidOperationException(
                        $"DESIGN DEFICIENCY: Two-Phase AI Pattern failed in Phase 2 (Conversational Response).\n" +
                        $"Phase 1 tools executed: {(toolResponse.ToolCalls?.Any() == true ? string.Join(", ", toolResponse.ToolCalls.Select(t => t.FunctionName)) : "none")}\n" +
                        $"Phase 2 must always generate conversational content.\n" +
                        $"Check personality template: {_personality.Type}/tool_acknowledgment.md or ordering.md");
                }

                var chatMessage = new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = conversationalResponse,
                    Timestamp = DateTime.Now,
                    IsSystem = false,
                    ShowInCleanMode = true
                };

                _thoughtLogger.LogThought($"TWO_PHASE_SUCCESS: Complete - Response={chatMessage.Content.Length} chars");
                return chatMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Two-Phase AI Pattern processing with context: {Context}", contextHint);
                _thoughtLogger.LogThought($"TWO_PHASE_ERROR: {ex.Message}");
                
                return new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = "I'm sorry, I had trouble processing that. Could you please try again?",
                    Timestamp = DateTime.Now,
                    IsSystem = false,
                    ShowInCleanMode = true
                };
            }
        }

        /// <summary>
        /// ARCHITECTURAL COMPONENT: Builds the Phase 1 prompt for tool analysis and execution.
        /// This prompt focuses on identifying and executing appropriate tools based on user input.
        /// </summary>
        private string BuildToolAnalysisPrompt(PosKernel.AI.Services.PromptContext promptContext, string contextHint)
        {
            var prompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "ordering", promptContext);
            
            // Add context-specific tool guidance
            if (contextHint == "payment")
            {
                prompt += "\n\nTOOL_ANALYSIS_CONTEXT: User is responding to payment method question or indicating payment intent.";
                
                if (_receipt.Items.Any())
                {
                    prompt += $"\nCURRENT_ORDER: {string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}"))}";
                    prompt += $"\nORDER_TOTAL: {FormatCurrency(_receipt.Total)}";
                    prompt += "\nThe order is READY for payment. Execute process_payment tool if customer specified payment method.";
                }
            }
            else if (_receipt.Status == PaymentStatus.ReadyForPayment)
            {
                prompt += "\n\nTOOL_ANALYSIS_CONTEXT: Order is complete and ready for payment. No tools needed unless customer specifies payment method.";
            }

            // Add tool execution instruction
            prompt += "\n\nTOOL_EXECUTION_PHASE: Analyze the customer's request and execute appropriate tools. " +
                     "Do not provide conversational responses - focus only on tool execution. " +
                     "The conversational response will be generated in a separate phase. " +
                     "IMPORTANT: You MUST use tools when the customer is ordering items or requesting actions. " +
                     "For example: 'kopi C' should trigger add_item_to_transaction tool.";

            return prompt;
        }

        /// <summary>
        /// ARCHITECTURAL COMPONENT: Generates tool acknowledgment using personality-specific templates.
        /// This ensures authentic personality responses that acknowledge what tools were executed.
        /// </summary>
        private async Task<string> GenerateToolAcknowledgmentAsync(IReadOnlyList<McpToolCall> toolCalls, string originalUserInput, PosKernel.AI.Services.PromptContext baseContext)
        {
            // Update context with tool execution details
            var acknowledgmentContext = new PosKernel.AI.Services.PromptContext
            {
                UserInput = originalUserInput,
                TimeOfDay = baseContext.TimeOfDay,
                CurrentTime = baseContext.CurrentTime,
                CartItems = _receipt.Items.Any() ? 
                    string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}")) : 
                    "none",
                CurrentTotal = FormatCurrency(_receipt.Total),
                Currency = baseContext.Currency
            };

            var acknowledgmentPrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "tool_acknowledgment", acknowledgmentContext);
            
            // ARCHITECTURAL FIX: Replace ALL template variables that might not be in PromptContext
            var toolsExecutedSummary = string.Join(", ", toolCalls.Select(t => t.FunctionName));
            acknowledgmentPrompt = acknowledgmentPrompt.Replace("{ToolsExecuted}", toolsExecutedSummary);
            acknowledgmentPrompt = acknowledgmentPrompt.Replace("{UserInput}", originalUserInput ?? "");
            acknowledgmentPrompt = acknowledgmentPrompt.Replace("{CartItems}", acknowledgmentContext.CartItems ?? "none");
            acknowledgmentPrompt = acknowledgmentPrompt.Replace("{CurrentTotal}", acknowledgmentContext.CurrentTotal ?? "");
            
            _thoughtLogger.LogThought($"TOOL_ACKNOWLEDGMENT_PROMPT: {acknowledgmentPrompt}");

            // CRITICAL: No tools in acknowledgment phase - pure conversational response
            var emptyToolsList = new List<McpTool>();
            var acknowledgmentResponse = await _mcpClient.CompleteWithToolsAsync(acknowledgmentPrompt, emptyToolsList);
            
            return acknowledgmentResponse.Content ?? throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Tool acknowledgment phase returned empty content for personality {_personality.Type}. " +
                $"Check tool_acknowledgment.md template and AI model configuration.");
        }

        /// <summary>
        /// ARCHITECTURAL COMPONENT: Generates direct conversational response when no tools were executed.
        /// Uses standard ordering personality templates for natural conversation.
        /// </summary>
        private async Task<string> GenerateDirectConversationAsync(string userInput, PosKernel.AI.Services.PromptContext promptContext, string contextHint)
        {
            var conversationPrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "ordering", promptContext);
            
            // Add conversational guidance
            conversationPrompt += "\n\nCONVERSATIONAL_RESPONSE: Respond naturally to the customer in your authentic personality. " +
                                 "No tools need to be executed - focus on conversation, questions, or guidance.";

            if (contextHint == "payment" && _receipt.Items.Any())
            {
                conversationPrompt += $"\nPAYMENT_CONTEXT: Customer mentioned payment but didn't specify clear method. " +
                                     $"Current order: {string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}"))} " +
                                     $"Total: {FormatCurrency(_receipt.Total)}. Ask for payment method.";
            }

            // CRITICAL: No tools in direct conversation phase when no tools were identified
            var emptyToolsList = new List<McpTool>();
            var conversationResponse = await _mcpClient.CompleteWithToolsAsync(conversationPrompt, emptyToolsList);
            
            return conversationResponse.Content ?? throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Direct conversation phase returned empty content for personality {_personality.Type}. " +
                $"Check ordering.md template and AI model configuration.");
        }
        
        public bool IsOrderComplete() => _paymentState.CurrentState == PaymentFlowState.PaymentComplete;

        public void ReloadPrompts() {
            _logger.LogInformation("Reloading AI personality prompts from disk");
            AiPersonalityFactory.ClearPromptCache();
        }

        public Task<ChatMessage> GeneratePostPaymentMessageAsync() {
            try {
                _paymentState.Reset();
                _receipt.Items.Clear();
                _receipt.Status = PaymentStatus.Building;
                _receipt.TransactionId = Guid.NewGuid().ToString()[..8];

                // Let personality handle the next customer greeting - no hardcoded responses
                var nextCustomerPrompt = AiPersonalityFactory.LoadPrompt(_personality.Type, "next_customer");
                
                var message = new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = nextCustomerPrompt,
                    IsSystem = false
                };

                _conversationHistory.Clear();
                _conversationHistory.Add(message);
                return Task.FromResult(message);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error generating post-payment message");
                // Fallback to generic greeting if personality prompt fails
                var fallbackMessage = new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = "Welcome! How can I help you today?",
                    IsSystem = false
                };
                return Task.FromResult(fallbackMessage);
            }
        }

        private string GenerateFollowUp(string userInput, string response, List<string> toolResults) {
            try {
                var followUpPrompt = $@"Customer said: '{userInput}'
Your response: '{response}'

Provide a brief follow-up: ""What else can I get for you?"" or ""Anything else today?""";

                // For now, return a simple follow-up since this doesn't need to be async
                return "What else can I get for you?";
            }
            catch {
                return "What else can I get for you?";
            }
        }

        private string FormatPaymentResponse(string paymentResult) {
            var lines = paymentResult.Split('\n');
            var amountLine = lines.FirstOrDefault(l => l.Contains("Payment processed:"))?.Trim();
            
            if (amountLine == null) {
                return paymentResult;
            }

            // Let personality handle the specific response format - no hardcoded cultural responses
            var paymentCompletePrompt = AiPersonalityFactory.LoadPrompt(_personality.Type, "payment_complete");
            var processedAmount = amountLine.Replace("Payment processed: ", "");
            
            return paymentCompletePrompt
                .Replace("{AMOUNT}", processedAmount)
                .Replace("{PAYMENT_INFO}", processedAmount);
        }

        private static bool IsExitCommand(string input) {
            var exitCommands = new[] { "exit", "quit", "bye", "goodbye" };
            return exitCommands.Contains(input.ToLowerInvariant());
        }

        private Transaction CreateMockTransaction() {
            var transaction = new Transaction {
                Id = new TransactionId(Guid.Parse(_receipt.TransactionId.PadRight(32, '0'))),
                Currency = _receipt.Store.Currency,
                State = TransactionState.Building
            };

            foreach (var item in _receipt.Items) {
                transaction.AddLine(
                    new ProductId(item.ProductName), 
                    item.Quantity, 
                    new Money((long)(item.UnitPrice * 100), _receipt.Store.Currency)
                );
            }

            return transaction;
        }

        private async Task<List<string>> ExecuteToolsAsync(IReadOnlyList<McpToolCall> toolCalls) {
            var results = new List<string>();

            foreach (var toolCall in toolCalls) {
                try {
                    string result;
                    if (_useRealKernel && _kernelToolsProvider != null) {
                        result = await _kernelToolsProvider.ExecuteToolAsync(toolCall);
                    } else if (_mockToolsProvider != null) {
                        var mockTransaction = CreateMockTransaction();
                        result = await _mockToolsProvider.ExecuteToolAsync(toolCall, mockTransaction);
                    } else {
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
            if (toolCall.FunctionName == "add_item_to_transaction" && !result.StartsWith("ERROR")) {
                if (TryParseAddedItem(result, out var item)) {
                    _receipt.Items.Add(item);
                    
                    // CRITICAL: Update receipt status when items are added
                    if (_receipt.Status == PaymentStatus.Building || _receipt.Status == PaymentStatus.Completed)
                    {
                        _receipt.Status = PaymentStatus.Building; // Reset to building when adding items
                        _logger.LogInformation("Receipt status updated to Building due to new item: {ProductName}", item.ProductName);
                    }
                    
                    _logger.LogInformation("Added {ProductName} to receipt", item.ProductName);
                }
            }
            else if (toolCall.FunctionName == "process_payment" && !result.StartsWith("PAYMENT_FAILED"))
            {
                // CRITICAL: Update receipt status when payment succeeds
                _receipt.Status = PaymentStatus.Completed;
                _logger.LogInformation("Receipt status updated to Completed due to successful payment");
            }
        }

        private bool TryParseAddedItem(string result, out ReceiptLineItem item) {
            item = new ReceiptLineItem();
            
            try {
                // Simple parsing for "ADDED: Product x1 @ $1.40 each = $1.40"
                if (result.StartsWith("ADDED: ")) {
                    var parts = result.Substring(7).Split(" x");
                    if (parts.Length >= 2) {
                        item.ProductName = parts[0].Trim();
                        var remaining = parts[1];
                        
                        if (int.TryParse(remaining.Split(' ')[0], out var qty)) {
                            item.Quantity = qty;
                        }
                        
                        var match = System.Text.RegularExpressions.Regex.Match(remaining, @"\$(\d+\.?\d*)");
                        if (match.Success && decimal.TryParse(match.Groups[1].Value, out var price)) {
                            item.UnitPrice = price;
                        }
                        
                        return !string.IsNullOrEmpty(item.ProductName) && item.Quantity > 0 && item.UnitPrice > 0;
                    }
                }
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to parse item from result: {Result}", result);
            }
            
            return false;
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

        /// <summary>
        /// ARCHITECTURAL COMPONENT: Creates order summary with payment method prompt using Two-Phase pattern.
        /// Enhanced with proper error handling for currency formatting.
        /// </summary>
        private async Task<ChatMessage> CreateOrderSummaryWithPaymentPrompt()
        {
            // SAFETY CHECK: Ensure we have items before creating summary
            if (!_receipt.Items.Any())
            {
                _thoughtLogger.LogThought("WARNING: CreateOrderSummaryWithPaymentPrompt called with empty receipt");
                return new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = "I don't see any items in your order yet. What would you like to order?",
                    Timestamp = DateTime.Now,
                    IsSystem = false,
                    ShowInCleanMode = true
                };
            }
            
            // ARCHITECTURAL PRINCIPLE: Use AI personality to generate order summary instead of hardcoded text
            var promptContext = new PosKernel.AI.Services.PromptContext
            {
                UserInput = "Please summarize the order and ask for payment method",
                TimeOfDay = DateTime.Now.Hour < 12 ? "morning" : DateTime.Now.Hour < 17 ? "afternoon" : "evening",
                CurrentTime = DateTime.Now.ToString("HH:mm"),
                CartItems = string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}")),
                CurrentTotal = FormatCurrency(_receipt.Total),
                Currency = _receipt.Store.Currency
            };

            try
            {
                var prompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "ordering", promptContext);
                prompt += "\n\nCONTEXT: Create an order summary and ask for payment method. The customer has finished ordering.";
                
                // Use Two-Phase pattern for order summary
                var emptyToolsList = new List<McpTool>(); // No tools needed for order summary
                var response = await _mcpClient.CompleteWithToolsAsync(prompt, emptyToolsList);
                
                return new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = response.Content ?? throw new InvalidOperationException(
                        $"DESIGN DEFICIENCY: Order summary prompt returned empty content for personality {_personality.Type}. " +
                        $"Check ordering.md template and AI model configuration."),
                    Timestamp = DateTime.Now,
                    IsSystem = false,
                    ShowInCleanMode = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating AI order summary: {Error}", ex.Message);
                _thoughtLogger.LogThought($"ERROR_IN_ORDER_SUMMARY: {ex.Message}");
                
                // ARCHITECTURAL PRINCIPLE: FAIL FAST - Don't provide hardcoded fallbacks
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Cannot generate order summary via AI personality system.\n" +
                    $"Original error: {ex.Message}\n" +
                    $"Check AI configuration and personality prompt files.");
            }
        }
    }
}
