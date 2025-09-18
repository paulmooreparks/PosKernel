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
    /// Clean state machine approach for reliable payment processing.
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
        private readonly PaymentStateMachine _paymentState;
        private bool _useRealKernel;

        public Receipt CurrentReceipt => _receipt;
        public IReadOnlyList<ChatMessage> ConversationHistory => _conversationHistory;
        public StoreConfig StoreConfig => _storeConfig;
        public AiPersonalityConfig Personality => _personality;
        public string? LastPrompt { get; private set; }

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
            _paymentState = new PaymentStateMachine();

            _logger.LogInformation("ChatOrchestrator initialized for {StoreName} using {Integration}",
                storeConfig.StoreName, _useRealKernel ? "Real Kernel" : "Mock");
        }

        public async Task<ChatMessage> InitializeAsync() {
            try {
                var timeOfDay = DateTime.Now.Hour < 12 ? "morning" : DateTime.Now.Hour < 17 ? "afternoon" : "evening";
                var context = new PromptContext { TimeOfDay = timeOfDay, CurrentTime = DateTime.Now.ToString("HH:mm") };
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

        public async Task<ChatMessage> ProcessUserInputAsync(string userInput) {
            if (IsExitCommand(userInput)) {
                return new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = "Thanks for visiting! Have a great day!",
                    IsSystem = false
                };
            }

            try {
                var input = userInput.ToLowerInvariant().Trim();
                
                // Step 1: Check if customer is done ordering
                if (_paymentState.CurrentState == PaymentFlowState.Ordering && IsOrderCompletionSignal(input)) {
                    _paymentState.TransitionToReadyForPayment();
                    _logger.LogInformation("Customer indicated order completion");
                }
                
                // Step 2: Ask for payment method if needed
                if (_paymentState.ShouldAskForPaymentMethod()) {
                    _paymentState.MarkPaymentMethodRequested();
                    var total = CurrencyFormattingService.FormatWithSymbol(_receipt.Total, _storeConfig.Currency);
                    var paymentPrompt = CreatePaymentMethodPrompt(total);
                    
                    var message = new ChatMessage {
                        Sender = _personality.StaffTitle,
                        Content = paymentPrompt,
                        IsSystem = false
                    };
                    _conversationHistory.Add(message);
                    return message;
                }
                
                // Step 3: Process payment if method specified
                if (_paymentState.CurrentState == PaymentFlowState.ReadyForPayment && _paymentState.HasAskedForPaymentMethod) {
                    var paymentMethod = ExtractPaymentMethod(input);
                    if (!string.IsNullOrEmpty(paymentMethod)) {
                        _paymentState.SetPaymentMethod(paymentMethod);
                        var paymentResult = await ProcessPaymentWithTool(paymentMethod);
                        
                        if (paymentResult.Contains("Payment processed")) {
                            _paymentState.CompletePayment();
                            _receipt.Status = PaymentStatus.Completed;
                        }
                        
                        var message = new ChatMessage {
                            Sender = _personality.StaffTitle,
                            Content = FormatPaymentResponse(paymentResult),
                            IsSystem = false
                        };
                        _conversationHistory.Add(message);
                        return message;
                    }
                }
                
                // Step 4: Handle normal ordering
                if (_paymentState.CurrentState == PaymentFlowState.Ordering) {
                    var conversationContext = string.Join("\n", _conversationHistory.TakeLast(6).Select(m => $"{m.Sender}: {m.Content}"));
                    var prompt = CreateOrderingPrompt(conversationContext, userInput);

                    LastPrompt = prompt;
                    var availableTools = _useRealKernel ? _kernelToolsProvider!.GetAvailableTools() : _mockToolsProvider!.GetAvailableTools();
                    var response = await _mcpClient.CompleteWithToolsAsync(prompt, availableTools);

                    var message = new ChatMessage {
                        Sender = _personality.StaffTitle,
                        Content = response.Content,
                        IsSystem = false
                    };

                    if (response.ToolCalls.Any()) {
                        var toolResults = await ExecuteToolsAsync(response.ToolCalls);
                        if (toolResults.Any()) {
                            var followUp = await GenerateFollowUp(userInput, response.Content, toolResults);
                            if (!string.IsNullOrEmpty(followUp)) {
                                message.Content = response.Content + " " + followUp;
                            }
                        }
                    }

                    _conversationHistory.Add(message);
                    return message;
                }
                
                return new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = "What can I help you with?",
                    IsSystem = false
                };
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error processing user input: {UserInput}", userInput);
                return new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = "Sorry, I'm having a technical issue. Please try again.",
                    IsSystem = false
                };
            }
        }

        public bool IsOrderComplete() => _paymentState.CurrentState == PaymentFlowState.PaymentComplete;

        public void ReloadPrompts() {
            _logger.LogInformation("Reloading AI personality prompts from disk");
            AiPersonalityFactory.ClearPromptCache();
        }

        public async Task<ChatMessage> GeneratePostPaymentMessageAsync() {
            try {
                _paymentState.Reset();
                _receipt.Items.Clear();
                _receipt.Status = PaymentStatus.Building;
                _receipt.TransactionId = Guid.NewGuid().ToString()[..8];

                var content = _personality.Type switch {
                    PersonalityType.SingaporeanKopitiamUncle => "Next customer! What you want?",
                    PersonalityType.AmericanBarista => "Hi there! What can I get started for you?",
                    _ => "Welcome! How can I help you today?"
                };
                
                var message = new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = content,
                    IsSystem = false
                };

                _conversationHistory.Clear();
                _conversationHistory.Add(message);
                return message;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error generating post-payment message");
                return new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = "Welcome! How can I help you today?",
                    IsSystem = false
                };
            }
        }

        // Helper methods
        private bool IsOrderCompletionSignal(string input) {
            return input.Contains("that's all") || input.Contains("that's it") || 
                   input.Contains("i'm done") || input.Contains("nothing else") ||
                   input.Contains("complete") || input.Contains("finish");
        }

        private string? ExtractPaymentMethod(string input) {
            if (input.Contains("cash") || input == "cash") { return "cash"; }
            if (input.Contains("card") || input == "card") { return "card"; }
            if (input.Contains("credit") || input == "credit") { return "card"; }
            if (input.Contains("debit") || input == "debit") { return "card"; }
            return null;
        }

        private string CreatePaymentMethodPrompt(string total) {
            return _personality.Type switch {
                PersonalityType.SingaporeanKopitiamUncle => $"Great! Your total is {total}. How would you like to pay?",
                PersonalityType.AmericanBarista => $"Perfect! Your total comes to {total}. How would you like to pay today?",
                _ => $"Your total is {total}. How would you like to pay?"
            };
        }

        private async Task<string> ProcessPaymentWithTool(string paymentMethod) {
            try {
                var paymentCall = new McpToolCall {
                    Id = "payment-" + Guid.NewGuid().ToString()[..8],
                    FunctionName = "process_payment",
                    Arguments = new Dictionary<string, System.Text.Json.JsonElement> {
                        ["method"] = System.Text.Json.JsonSerializer.SerializeToElement(paymentMethod),
                        ["amount"] = System.Text.Json.JsonSerializer.SerializeToElement(_receipt.Total)
                    }
                };

                if (_useRealKernel && _kernelToolsProvider != null) {
                    return await _kernelToolsProvider.ExecuteToolAsync(paymentCall);
                } else if (_mockToolsProvider != null) {
                    var mockTransaction = CreateMockTransaction();
                    return await _mockToolsProvider.ExecuteToolAsync(paymentCall, mockTransaction);
                }
                return $"Payment processed: {CurrencyFormattingService.FormatWithSymbol(_receipt.Total, _storeConfig.Currency)} {paymentMethod}";
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error processing payment");
                return $"Payment failed: {ex.Message}";
            }
        }

        private string CreateOrderingPrompt(string context, string userInput) {
            var items = _receipt.Items.Any() 
                ? string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}"))
                : "None";

            var orderingPrompt = AiPersonalityFactory.LoadPrompt(_personality.Type, "ordering");

            return $@"CONTEXT: {context}

STORE: {_storeConfig.StoreName}
CURRENT ITEMS: {items}
CUSTOMER SAID: ""{userInput}""

IMPORTANT: You are in ORDERING mode. Help the customer add items, answer questions, or provide information. 
DO NOT mention payment or ask how they want to pay - that will be handled separately.

{orderingPrompt}

Use your tools to help the customer with their order.";
        }

        private async Task<string> GenerateFollowUp(string userInput, string response, List<string> toolResults) {
            try {
                var followUpPrompt = $@"Customer said: '{userInput}'
Your response: '{response}'
Tools used: {string.Join("; ", toolResults)}

Provide a brief follow-up: ""What else can I get for you?"" or ""Anything else today?""";

                var followUpResponse = await _mcpClient.CompleteWithToolsAsync(followUpPrompt, Array.Empty<McpTool>());
                return followUpResponse.Content;
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

            return _personality.Type switch {
                PersonalityType.SingaporeanKopitiamUncle => $"Thank you! {amountLine.Replace("Payment processed: ", "")} received. Come again!",
                PersonalityType.AmericanBarista => $"Perfect! {amountLine.Replace("Payment processed: ", "")} received. Have a great day!",
                _ => $"Thank you! {amountLine.Replace("Payment processed: ", "")} received."
            };
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
                    _logger.LogInformation("Added {ProductName} to receipt", item.ProductName);
                }
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
    }
}
