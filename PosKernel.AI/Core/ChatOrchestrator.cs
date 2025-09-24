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
using System.Text.Json;

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
                
                // CRITICAL DEBUG: Log exactly what personality is being used
                _logger.LogInformation($"🔍 ORCHESTRATOR_DEBUG: Initializing with PersonalityType={_personality.Type}, StaffTitle={_personality.StaffTitle}");
                
                // ARCHITECTURAL FIX: Handle initialization differently for Real Kernel vs Mock
                if (_useRealKernel && _kernelToolsProvider != null)
                {
                    var availableTools = _kernelToolsProvider.GetAvailableTools();
                    
                    // STEP 1: Load contexts silently (tools only, no conversational response needed)
                    var contextLoadPrompt = "Load menu context and payment methods context for this store. " +
                                          "Execute the load_menu_context and load_payment_methods_context tools. " +
                                          "Do not provide conversational responses - just execute the tools to initialize your knowledge.";
                    
                    _logger.LogInformation("Loading store context at initialization...");
                    LastPrompt = contextLoadPrompt;
                    await _mcpClient.CompleteWithToolsAsync(contextLoadPrompt, availableTools);
                    
                    // STEP 2: Generate proper greeting using personality (no tools needed)
                    var greetingPrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "greeting", context);
                    _logger.LogInformation("Generating greeting message...");
                    
                    LastPrompt = greetingPrompt;
                    var emptyToolsList = new List<McpTool>(); // No tools for greeting
                    var greetingResponse = await _mcpClient.CompleteWithToolsAsync(greetingPrompt, emptyToolsList);

                    var greetingMessage = new ChatMessage {
                        Sender = _personality.StaffTitle,
                        Content = greetingResponse.Content ?? $"Welcome to {_storeConfig.StoreName}! How can I help you today?",
                        IsSystem = false
                    };

                    _conversationHistory.Add(greetingMessage);
                    return greetingMessage;
                }
                else if (_mockToolsProvider != null)
                {
                    // Mock initialization - just greeting, no context loading needed
                    _logger.LogInformation($"🔍 MOCK_DEBUG: About to call BuildPrompt with PersonalityType={_personality.Type}");
                    var greetingPrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "greeting", context);
                    LastPrompt = greetingPrompt;
                    var emptyToolsList = new List<McpTool>(); // No tools for mock greeting
                    var response = await _mcpClient.CompleteWithToolsAsync(greetingPrompt, emptyToolsList);

                    var greetingMessage = new ChatMessage {
                        Sender = _personality.StaffTitle,
                        Content = response.Content ?? $"Welcome to {_storeConfig.StoreName}! How can I help you today?",
                        IsSystem = false
                    };

                    _conversationHistory.Add(greetingMessage);
                    return greetingMessage;
                }
                else
                {
                    throw new InvalidOperationException("No tools provider available for initialization");
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error during initialization: {Error}", ex.Message);
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
            
            // ARCHITECTURAL PRINCIPLE: Simple structural analysis only
            var intentAnalysis = _orderAnalyzer.AnalyzeOrderIntent(userInput, _receipt, _conversationHistory);
            
            _thoughtLogger.LogThought($"STRUCTURAL_INTENT_ANALYSIS: Intent: {intentAnalysis.Intent}, Confidence: {intentAnalysis.ConfidenceLevel:F2}, Reason: {intentAnalysis.ReasonCode}");
            
            // Handle with simplified ordering logic
            var response = await HandleWithSimplifiedOrdering(userInput, intentAnalysis);
            
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

        private async Task<ChatMessage> HandleWithSimplifiedOrdering(string userInput, OrderIntentAnalysis intentAnalysis)
        {
            _thoughtLogger.LogThought(
                $"SIMPLIFIED_ORDER_HANDLING: Intent: {intentAnalysis.Intent}, Confidence: {intentAnalysis.ConfidenceLevel:F2}, " +
                $"Items in receipt: {_receipt.Items.Count}, Receipt status: {_receipt.Status}");

            // ARCHITECTURAL PRINCIPLE: If we have items in receipt, ensure payment context is preserved
            if (_receipt.Items.Any() && _receipt.Status != PaymentStatus.Completed)
            {
                _receipt.Status = PaymentStatus.ReadyForPayment;
                _thoughtLogger.LogThought("RECEIPT_STATUS_CORRECTED: Set to ReadyForPayment due to existing items");
            }

            // TRAINING FIX: Enhanced payment method detection
            if (_receipt.Items.Any() && IsPaymentMethodSpecification(userInput))
            {
                _thoughtLogger.LogThought($"PAYMENT_METHOD_DETECTED: Customer specified payment method in: '{userInput}'");
                _receipt.Status = PaymentStatus.ReadyForPayment;
                return await ProcessUserRequestAsync(userInput, "payment");
            }

            // ARCHITECTURAL PRINCIPLE: Simple intent handling - most cases defer to AI semantic understanding
            switch (intentAnalysis.Intent)
            {
                case OrderIntent.ContinueOrdering:
                    return await ProcessUserRequestAsync(userInput, "ordering");
                    
                case OrderIntent.ReadyForPayment:
                    // Check if we should transition to payment
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
                    
                case OrderIntent.Question:
                    _thoughtLogger.LogThought("QUESTION_INTENT: Customer asking question - provide information, don't automatically process");
                    return await ProcessUserRequestAsync(userInput, "ordering");
                    
                case OrderIntent.Ambiguous:
                    _thoughtLogger.LogThought("AMBIGUOUS_INTENT: Let AI semantic analysis determine intent");
                    return await ProcessUserRequestAsync(userInput, "ordering");
                    
                default:
                    // ARCHITECTURAL PRINCIPLE: Default to ordering context - let AI handle semantics
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
                _thoughtLogger.LogThought($"PHASE_1_PROMPT_LENGTH: {toolAnalysisPrompt.Length} chars");
                var promptPreview = toolAnalysisPrompt.SafeTruncateString(200);
                _thoughtLogger.LogThought($"PHASE_1_PROMPT_PREVIEW: {promptPreview}");
                
                LastPrompt = toolAnalysisPrompt;
                var availableTools = _useRealKernel ? _kernelToolsProvider!.GetAvailableTools() : _mockToolsProvider!.GetAvailableTools();
                _thoughtLogger.LogThought($"AVAILABLE_TOOLS_COUNT: {availableTools?.Count ?? 0}");
                
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
                    conversationalResponse = await GenerateToolAcknowledgmentAsync(toolResponse.ToolCalls, userInput, promptContext, toolResults);
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
            _thoughtLogger.LogThought($"BASE_PROMPT_LENGTH: {prompt?.Length ?? 0} chars");
            
            // Add context-specific tool guidance
            if (contextHint == "payment")
            {
                prompt += "\n\nTOOL_ANALYSIS_CONTEXT: User is specifying their payment method.";
                
                if (_receipt.Items.Any())
                {
                    prompt += $"\nCURRENT_ORDER: {string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}"))}";
                    prompt += $"\nORDER_TOTAL: {FormatCurrency(_receipt.Total)}";
                    prompt += "\n**CRITICAL**: The customer has specified their payment method. Execute process_payment tool immediately with their specified method.";
                    prompt += "\n**DO NOT** load payment methods context - the customer has already chosen their method.";
                    prompt += "\n**PAYMENT METHOD EXTRACTION**: Look at their input and determine which payment method they want to use.";
                }
            }
            else if (_receipt.Status == PaymentStatus.ReadyForPayment)
            {
                prompt += "\n\nTOOL_ANALYSIS_CONTEXT: Order is complete and ready for payment. If customer asks about payment methods, use load_payment_methods_context tool.";
            }

            // Add tool execution instruction
            prompt += "\n\n## TOOL_EXECUTION_PHASE: Analyze and Execute Tools Only\n" +
                     "**CRITICAL**: This is the tool execution phase. Do NOT provide conversational responses here.\n" +
                     "**FOCUS**: Determine which tools to execute based on the customer's request, then execute them.\n\n" +
                     "### TOOL SELECTION RULES (FOLLOW EXACTLY):\n" +
                     "1. **Customer mentions food/drink items** (like 'roti kaya', 'kopi', 'teh', specific product names):\n" +
                     "   → **ALWAYS use `add_item_to_transaction` tool**\n" +
                     "   → **NEVER use `load_menu_context` tool**\n" +
                     "   → The menu context is already loaded at session start!\n\n" +
                     "2. **Customer asks comprehensive menu questions** ('what food do you have?', 'show me the full menu', 'what's available?'):\n" +
                     "   → **Use `search_products` tool with empty search term** to get comprehensive results\n" +
                     "   → **Alternative: Use `load_menu_context` tool** for complete menu display\n" +
                     "   → **DO NOT use `get_popular_items`** for comprehensive menu requests\n\n" +
                     "3. **Customer asks for recommendations** ('what's popular?', 'what's good?', 'what do you recommend?'):\n" +
                     "   → **Use `get_popular_items` tool** to show recommendations\n" +
                     "   → This is different from asking for the complete menu\n\n" +
                     "4. **Customer indicates completion** ('that's all', 'habis', 'finished', 'done'):\n" +
                     "   → **Use `load_payment_methods_context` tool** to get available payment options\n" +
                     "   → Then ask for payment method in conversational response\n\n" +
                     "5. **Customer specifies payment method** (after you asked how they want to pay):\n" +
                     "   → **Use `process_payment` tool with their specified method**\n" +
                     "   → **DO NOT use `load_payment_methods_context` tool**\n\n" +
                     "6. **Customer asks about payment methods** ('can I pay cash?', 'what payment methods?'):\n" +
                     "   → **Use `load_payment_methods_context` tool**\n\n" +
                     "### CRITICAL DISTINCTION:\n" +
                     "**'What food do you have?'** = Customer wants to see ALL available items → Use `search_products` or `load_menu_context`\n" +
                     "**'What's popular?'** = Customer wants recommendations → Use `get_popular_items`\n\n" +
                     "### CRITICAL ARCHITECTURAL RULE:\n" +
                     "**NEVER use `load_menu_context` during customer ordering for item lookups!**\n" +
                     "The menu was loaded once at session initialization. Your job now is to:\n" +
                     "- Take orders using `add_item_to_transaction`\n" +
                     "- Answer menu questions using `search_products` or display knowledge\n" +
                     "- Process payments using `process_payment`\n\n" +
                     "**Execute the appropriate tools now, then stop. No conversational content in this phase.**";

            return prompt;
        }

        /// <summary>
        /// ARCHITECTURAL COMPONENT: Generates tool acknowledgment using personality-specific templates.
        /// This ensures authentic personality responses that acknowledge what tools were executed.
        /// </summary>
        private async Task<string> GenerateToolAcknowledgmentAsync(IReadOnlyList<McpToolCall> toolCalls, string originalUserInput, PosKernel.AI.Services.PromptContext baseContext, List<string>? toolResults = null)
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
            
            // CRITICAL FIX: Add tool results so AI knows what the tools returned
            if (toolResults?.Any() == true)
            {
                acknowledgmentPrompt += "\n\n## TOOL EXECUTION RESULTS:\n";
                for (int i = 0; i < toolCalls.Count && i < toolResults.Count; i++)
                {
                    acknowledgmentPrompt += $"\n### {toolCalls[i].FunctionName} returned:\n{toolResults[i]}\n";
                }
                acknowledgmentPrompt += "\n**CRITICAL**: You must share these detailed results with the customer in your response!";
            }
            
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

        public async Task<ChatMessage> GeneratePostPaymentMessageAsync() {
            try {
                _paymentState.Reset();
                _receipt.Items.Clear();
                _receipt.Status = PaymentStatus.Building;
                _receipt.TransactionId = Guid.NewGuid().ToString()[..8];

                // ARCHITECTURAL FIX: Use AI to generate next customer greeting, not raw file content
                var timeOfDay = DateTime.Now.Hour < 12 ? "morning" : DateTime.Now.Hour < 17 ? "afternoon" : "evening";
                var context = new PosKernel.AI.Services.PromptContext { 
                    TimeOfDay = timeOfDay, 
                    CurrentTime = DateTime.Now.ToString("HH:mm") 
                };
                
                // Generate proper next customer greeting using AI (same as initialization)
                var nextCustomerPrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "next_customer", context);
                
                _logger.LogInformation("Generating next customer greeting...");
                var emptyToolsList = new List<McpTool>(); // No tools for greeting
                var greetingResponse = await _mcpClient.CompleteWithToolsAsync(nextCustomerPrompt, emptyToolsList);

                var message = new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = greetingResponse.Content ?? "Next customer! What you want?",
                    IsSystem = false
                };

                _conversationHistory.Clear();
                _conversationHistory.Add(message);
                return message;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error generating post-payment message: {Error}", ex.Message);
                // Fallback to simple greeting if AI fails
                var fallbackMessage = new ChatMessage {
                    Sender = _personality.StaffTitle,
                    Content = "Next customer! What you want?",
                    IsSystem = false
                };
                _conversationHistory.Clear();
                _conversationHistory.Add(fallbackMessage);
                return fallbackMessage;
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
            else if (toolCall.FunctionName == "void_line_item" && !result.StartsWith("ERROR"))
            {
                // CRITICAL FIX: Handle void operations by removing the item from receipt display
                if (toolCall.Arguments.TryGetValue("line_number", out var lineNumberElement))
                {
                    var lineNumber = lineNumberElement.GetInt32();
                    
                    // Convert 1-based line number to 0-based index
                    var itemIndex = lineNumber - 1;
                    
                    if (itemIndex >= 0 && itemIndex < _receipt.Items.Count)
                    {
                        var voidedItem = _receipt.Items[itemIndex];
                        _receipt.Items.RemoveAt(itemIndex);
                        _logger.LogInformation("Removed {ProductName} from receipt display (line {LineNumber})", 
                            voidedItem.ProductName, lineNumber);
                        
                        // Update receipt status if no items left
                        if (!_receipt.Items.Any())
                        {
                            _receipt.Status = PaymentStatus.Building;
                            _logger.LogInformation("Receipt status updated to Building - no items remaining after void");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Invalid line number {LineNumber} for void operation - receipt has {Count} items", 
                            lineNumber, _receipt.Items.Count);
                    }
                }
            }
            else if (toolCall.FunctionName == "update_line_item_quantity" && !result.StartsWith("ERROR"))
            {
                // CRITICAL FIX: Handle quantity updates by modifying the receipt item
                if (toolCall.Arguments.TryGetValue("line_number", out var lineNumberElement) &&
                    toolCall.Arguments.TryGetValue("new_quantity", out var quantityElement))
                {
                    var lineNumber = lineNumberElement.GetInt32();
                    var newQuantity = quantityElement.GetInt32();
                    
                    // Convert 1-based line number to 0-based index
                    var itemIndex = lineNumber - 1;
                    
                    if (itemIndex >= 0 && itemIndex < _receipt.Items.Count)
                    {
                        var updatedItem = _receipt.Items[itemIndex];
                        var oldQuantity = updatedItem.Quantity;
                        updatedItem.Quantity = newQuantity;
                        _logger.LogInformation("Updated {ProductName} quantity from {OldQuantity} to {NewQuantity} (line {LineNumber})", 
                            updatedItem.ProductName, oldQuantity, newQuantity, lineNumber);
                    }
                    else
                    {
                        _logger.LogWarning("Invalid line number {LineNumber} for quantity update - receipt has {Count} items", 
                            lineNumber, _receipt.Items.Count);
                    }
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
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex) {
                // Log parsing exception but do not throw - allow failure to propagate
                _logger.LogError(ex, "Error parsing added item from result: {Result}", result);
            }
            
            return false;
        }

        /// <summary>
        /// ARCHITECTURAL COMPONENT: Currency formatting must use service - no client-side assumptions.
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
        /// Called when customer indicates order completion and items exist in the order.
        /// </summary>
        private async Task<ChatMessage> CreateOrderSummaryWithPaymentPrompt()
        {
            try
            {
                _thoughtLogger.LogThought("ORDER_SUMMARY_PAYMENT: Creating order summary with payment method request");
                
                // Execute load_payment_methods_context tool to get available payment options
                var loadPaymentMethodsCall = new McpToolCall
                {
                    FunctionName = "load_payment_methods_context",
                    Arguments = new Dictionary<string, JsonElement>()
                };
                
                var availableTools = _useRealKernel ? _kernelToolsProvider!.GetAvailableTools() : _mockToolsProvider!.GetAvailableTools();
                
                // Execute the tool to get payment methods
                List<string> toolResults;
                if (_useRealKernel && _kernelToolsProvider != null)
                {
                    var result = await _kernelToolsProvider.ExecuteToolAsync(loadPaymentMethodsCall);
                    toolResults = new List<string> { result };
                }
                else if (_mockToolsProvider != null)
                {
                    var mockTransaction = CreateMockTransaction();
                    var result = await _mockToolsProvider.ExecuteToolAsync(loadPaymentMethodsCall, mockTransaction);
                    toolResults = new List<string> { result };
                }
                else
                {
                    toolResults = new List<string> { "Payment methods: Cash, Card, PayNow, NETS" };
                }

                _thoughtLogger.LogThought($"PAYMENT_METHODS_LOADED: {string.Join(" | ", toolResults)}");

                // Generate conversational response asking for payment method
                var promptContext = new PosKernel.AI.Services.PromptContext
                {
                    UserInput = "Customer is ready to pay - ask for payment method",
                    TimeOfDay = DateTime.Now.Hour < 12 ? "morning" : DateTime.Now.Hour < 17 ? "afternoon" : "evening",
                    CurrentTime = DateTime.Now.ToString("HH:mm"),
                    CartItems = string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}")),
                    CurrentTotal = FormatCurrency(_receipt.Total),
                    Currency = _receipt.Store.Currency
                };

                var orderSummaryPrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "ordering", promptContext);
                
                // Add specific guidance for payment method request
                orderSummaryPrompt += "\n\n## ORDER COMPLETION CONTEXT:\n" +
                                     $"The customer has completed their order: {promptContext.CartItems}\n" +
                                     $"Order total: {promptContext.CurrentTotal}\n" +
                                     $"Available payment methods: {string.Join(", ", toolResults)}\n\n" +
                                     "**CRITICAL**: Provide an order summary and ask the customer how they want to pay. " +
                                     "List the available payment methods clearly.";

                LastPrompt = orderSummaryPrompt;
                var emptyToolsList = new List<McpTool>(); // No tools needed for conversational response
                var response = await _mcpClient.CompleteWithToolsAsync(orderSummaryPrompt, emptyToolsList);

                var chatMessage = new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = response.Content ?? $"Your order: {promptContext.CartItems}. Total: {promptContext.CurrentTotal}. How would you like to pay?",
                    Timestamp = DateTime.Now,
                    IsSystem = false,
                    ShowInCleanMode = true
                };

                _thoughtLogger.LogThought($"ORDER_SUMMARY_GENERATED: {chatMessage.Content.Length} chars");
                return chatMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order summary with payment prompt: {Error}", ex.Message);
                _thoughtLogger.LogThought($"ORDER_SUMMARY_ERROR: {ex.Message}");
                
                // Fallback response
                var fallbackContent = $"Your order: {string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}"))}. " +
                                     $"Total: {FormatCurrency(_receipt.Total)}. How would you like to pay?";
                
                return new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = fallbackContent,
                    Timestamp = DateTime.Now,
                    IsSystem = false,
                    ShowInCleanMode = true
                };
            }
        }

        /// <summary>
        /// ARCHITECTURAL FIX: Enhanced payment method detection for training scenarios
        /// Detects when customer specifies a payment method to trigger payment processing
        /// </summary>
        private bool IsPaymentMethodSpecification(string userInput)
        {
            var normalizedInput = userInput.ToLowerInvariant().Trim();
            
            // Common payment method names (enhanced for training scenarios)
            var paymentMethods = new[]
            {
                "cash", "card", "credit card", "debit card", "paynow", "nets", 
                "digital", "contactless", "visa", "mastercard", "amex",
                "paypal", "apple pay", "google pay", "samsung pay",
                // Training-specific additions
                "pay now", "pay with cash", "pay by card"
            };
            
            // Direct payment method mentions
            if (paymentMethods.Any(method => normalizedInput.Contains(method)))
            {
                return true;
            }
            
            // Payment context phrases (enhanced for training scenarios)
            var paymentPhrases = new[]
            {
                "can pay", "want to pay", "pay with", "pay by", "using", 
                "i'll pay", "pay now", "ready to pay", "can pay now",
                // Training-specific completions that should trigger payment
                "habis", "sudah", "selesai", "that's all", "that's it", "nothing else"
            };
            
            return paymentPhrases.Any(phrase => normalizedInput.Contains(phrase));
        }
    }
}
