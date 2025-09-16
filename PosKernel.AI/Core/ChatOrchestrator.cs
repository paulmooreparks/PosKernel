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
            // Parse tool results and update receipt
            if (toolCall.FunctionName == "add_item_to_transaction" && result.StartsWith("ADDED:"))
            {
                // Parse the result to extract item information
                // This is a simplified parser - in reality, we'd want more robust parsing
                if (TryParseAddedItem(result, out var item))
                {
                    _receipt.Items.Add(item);
                }
            }
            else if (toolCall.FunctionName == "process_payment" && result.Contains("Payment processed"))
            {
                _receipt.Status = PaymentStatus.Completed;
            }
        }

        private bool TryParseAddedItem(string result, out ReceiptLineItem item)
        {
            item = new ReceiptLineItem();
            
            try
            {
                // Parse something like "ADDED: Kaya Toast (prep: no sugar) x2 @ $1.80 each = $3.60"
                var parts = result.Replace("ADDED: ", "").Split(" x");
                if (parts.Length >= 2)
                {
                    var productPart = parts[0];
                    var quantityAndPricePart = parts[1];
                    
                    // Extract product name and prep notes
                    if (productPart.Contains(" (prep: "))
                    {
                        var prepIndex = productPart.IndexOf(" (prep: ");
                        item.ProductName = productPart.Substring(0, prepIndex);
                        var prepEnd = productPart.IndexOf(")", prepIndex);
                        if (prepEnd > prepIndex)
                        {
                            item.PreparationNotes = productPart.Substring(prepIndex + 8, prepEnd - prepIndex - 8);
                        }
                    }
                    else
                    {
                        item.ProductName = productPart;
                    }
                    
                    // Extract quantity and price
                    var atIndex = quantityAndPricePart.IndexOf(" @ $");
                    if (atIndex > 0)
                    {
                        if (int.TryParse(quantityAndPricePart.Substring(0, atIndex).Trim(), out var quantity))
                        {
                            item.Quantity = quantity;
                        }
                        
                        var priceStart = atIndex + 3;
                        var priceEnd = quantityAndPricePart.IndexOf(" each", priceStart);
                        if (priceEnd > priceStart)
                        {
                            var priceStr = quantityAndPricePart.Substring(priceStart, priceEnd - priceStart);
                            if (decimal.TryParse(priceStr, out var price))
                            {
                                item.UnitPrice = price;
                            }
                        }
                    }
                    
                    return !string.IsNullOrEmpty(item.ProductName) && item.Quantity > 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse added item: {Result}", result);
            }
            
            return false;
        }

        private Transaction CreateMockTransaction()
        {
            // Create a simple mock Transaction for compatibility
            return new Transaction
            {
                Id = new TransactionId(Guid.Parse(_receipt.TransactionId.PadRight(32, '0'))),
                Currency = _receipt.Store.Currency,
                State = TransactionState.Building
            };
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
    }
}
