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
using PosKernel.Client; // Add this for IPosKernelClient
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PosKernel.AI.Core {
    
    public enum ConversationState
    {
        Initial,
        AwaitingDisambiguationChoice,
        AwaitingSetCustomization,
        AwaitingSetConfirmation,
        ShowingMenuOptions,
        ReadyForPayment,
        ProcessingPayment
    }
    
    public class ChatOrchestrator {
        private readonly McpClient _mcpClient;
        private readonly KernelPosToolsProvider? _kernelToolsProvider;
        private readonly PosToolsProvider? _mockToolsProvider;
        private readonly ILogger<ChatOrchestrator> _logger;
        private readonly AiPersonalityConfig _personality;
        private readonly StoreConfig _storeConfig;
        private readonly TimeSpan DisambiguationTimeout;
        private readonly ICurrencyFormattingService? _currencyFormatter;
        private readonly Receipt _receipt;
        private readonly List<ChatMessage> _conversationHistory;
        private readonly PaymentStateMachine _paymentState;
        private bool _useRealKernel;
        private ThoughtLogger _thoughtLogger;
        private OrderCompletionAnalyzer _orderAnalyzer;
        
        // Cache to avoid repeated name lookups for the same SKU
        private readonly Dictionary<string, string> _skuNameCache = new(StringComparer.OrdinalIgnoreCase);

        private TemplateRenderer _renderer;
        private bool _autoClearScheduled = false;

        public Receipt CurrentReceipt => _receipt;
        public IReadOnlyList<ChatMessage> ConversationHistory => _conversationHistory;
        public StoreConfig StoreConfig => _storeConfig;
        public AiPersonalityConfig Personality => _personality;
        public string? LastPrompt { get; private set; }

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

            if (!storeConfig.DisambiguationTimeoutMinutes.HasValue)
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: ChatOrchestrator requires StoreConfig.DisambiguationTimeoutMinutes to control clarification timing. " +
                    "Client cannot decide timeout defaults. Set DisambiguationTimeoutMinutes in store configuration.");
            }
            DisambiguationTimeout = TimeSpan.FromMinutes(storeConfig.DisambiguationTimeoutMinutes.Value);

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
            _thoughtLogger = new ThoughtLogger(new NullLogDisplay());
            _orderAnalyzer = new OrderCompletionAnalyzer(_thoughtLogger);

            _renderer = new TemplateRenderer(currencyFormatter, storeConfig);

            _logger.LogInformation("ChatOrchestrator initialized for {StoreName} using {Integration}",
                storeConfig.StoreName, _useRealKernel ? "Real Kernel" : "Mock");
        }

        public async Task<ChatMessage> InitializeAsync() {
            try {
                var context = new PosKernel.AI.Services.PromptContext();
                
                _logger.LogInformation($"🔍 ORCHESTRATOR_DEBUG: Initializing with PersonalityType={_personality.Type}, StaffTitle={_personality.StaffTitle}");
                
                if (_useRealKernel && _kernelToolsProvider != null)
                {
                    var availableTools = _kernelToolsProvider.GetAvailableTools();
                    
                    var contextLoadPrompt = "Load menu context and payment methods context for this store. " +
                                          "Execute the load_menu_context and load_payment_methods_context tools. " +
                                          "Do not provide conversational responses - just execute the tools to initialize your knowledge.";
                    
                    _logger.LogInformation("Loading store context at initialization...");
                    LastPrompt = contextLoadPrompt;
                    await _mcpClient.CompleteWithToolsAsync(contextLoadPrompt, availableTools ?? Array.Empty<McpTool>());
                    
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
            userInput ??= string.Empty; // defensive
            _logger.LogInformation("Processing user input: {Input}", userInput);
            
            var intentAnalysis = _orderAnalyzer.AnalyzeOrderIntent(userInput, _receipt, _conversationHistory);
            
            _thoughtLogger.LogThought($"STRUCTURAL_INTENT_ANALYSIS: Intent: {intentAnalysis.Intent}, Confidence: {intentAnalysis.ConfidenceLevel:F2}, Reason: {intentAnalysis. ReasonCode}");
            
            var response = await HandleWithSimplifiedOrdering(userInput, intentAnalysis);
            
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

        private bool LooksLikePaymentMethod(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var normalized = new string(input.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '_').ToArray()).ToLowerInvariant();
            var methods = _storeConfig?.PaymentMethods?.AcceptedMethods;
            if (methods == null)
            {
                return false;
            }

            foreach (var m in methods.Where(m => m.IsEnabled))
            {
                var idNorm = new string((m.MethodId ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '_').ToArray()).ToLowerInvariant();
                var nameNorm = new string((m.DisplayName ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '_').ToArray()).ToLowerInvariant();
                if (normalized == idNorm || normalized == nameNorm)
                {
                    return true;
                }
            }

            return false;
        }

        // Minimal deterministic tool planning wrapper for ordering/payment phases
        private async Task<ChatMessage> ProcessUserRequestAsync(string userInput, string contextHint)
        {
            var ctx = new PosKernel.AI.Services.PromptContext
            {
                UserInput = userInput,
                CartItems = _receipt.Items.Any() ? string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}")) : "none",
                CurrentTotal = FormatCurrency(_receipt.Total),
                Currency = _receipt.Store.Currency
            };

            var prompt = BuildToolAnalysisPrompt(ctx, contextHint);
            LastPrompt = prompt;

            var tools = _useRealKernel && _kernelToolsProvider != null
                ? _kernelToolsProvider.GetAvailableTools()
                : (_mockToolsProvider?.GetAvailableTools() ?? Array.Empty<McpTool>());

            // Snapshot before state to derive deterministic acknowledgments
            var beforeIds = new HashSet<string>(_receipt.Items.Select(i => i.LineItemId ?? string.Empty));
            var beforeCount = _receipt.Items.Count;

            // Single-phase: AI handles discovery and response naturally
            var aiResponse = await _mcpClient.CompleteWithToolsAsync(prompt, tools);

            // Execute any tool calls the AI requested
            bool anyToolExecuted = false;
            bool anyItemsAdded = false;
            
            if (_useRealKernel && _kernelToolsProvider != null && aiResponse.ToolCalls.Any())
            {
                foreach (var call in aiResponse.ToolCalls)
                {
                    try
                    {
                        await _kernelToolsProvider.ExecuteToolAsync(call, CancellationToken.None);
                        anyToolExecuted = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Tool execution failed: {Function}", call.FunctionName);
                    }
                }

                try
                {
                    await RefreshReceiptFromKernelAsync();
                    anyItemsAdded = _receipt.Items.Count > beforeCount;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh receipt after tool execution");
                }
            }

            // Deterministic discovery fallback: if AI didn't add items and this is ordering context
            string discoveryResults = string.Empty;
            if (!anyItemsAdded && !string.Equals(contextHint, "payment", StringComparison.OrdinalIgnoreCase) 
                && _useRealKernel && _kernelToolsProvider != null)
            {
                discoveryResults = await TryDiscoverProductAsync(userInput);
                if (!string.IsNullOrWhiteSpace(discoveryResults))
                {
                    // Re-prompt AI with search results to generate natural response
                    var discoveryPrompt = BuildDiscoveryPrompt(userInput, discoveryResults, ctx);
                    LastPrompt = discoveryPrompt;
                    aiResponse = await _mcpClient.CompleteWithToolsAsync(discoveryPrompt, new List<McpTool>());
                }
            }

            if (_receipt.Status == PaymentStatus.Completed)
            {
                ScheduleAutoClearIfConfigured();
            }

            // Use AI response content, with fallbacks
            var content = aiResponse.Content?.Trim() ?? string.Empty;
            
            // Check if new items were added for deterministic acknowledgment
            var addedLines = _receipt.Items.Where(i => !beforeIds.Contains(i.LineItemId ?? string.Empty)).ToList();
            if (addedLines.Count > 0 && string.IsNullOrWhiteSpace(content))
            {
                var added = addedLines.OrderByDescending(l => l.LineNumber).First();
                var name = !string.IsNullOrWhiteSpace(added.ProductName) ? added.ProductName : (added.ProductSku ?? "item");
                var qty = Math.Max(1, added.Quantity);
                content = _renderer.RenderAddItemAck(name, qty, _receipt.Total);
            }

            if (_receipt.Status == PaymentStatus.ReadyForPayment && string.IsNullOrWhiteSpace(content))
            {
                content = _renderer.RenderPaymentRequest(_receipt);
            }

            // Better fallback when we have discovery results but empty AI content
            if (string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(discoveryResults))
            {
                // Parse discovery results for a simple presentation
                if (discoveryResults.Contains("PRODUCT_NOT_FOUND"))
                {
                    content = "Sorry, I couldn't find that item. Can you tell me more about what you want?";
                }
                else
                {
                    content = "Let me check what we have for you...";
                }
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                content = "What you want to order?";
            }

            return new ChatMessage
            {
                Sender = _personality.StaffTitle,
                Content = content,
                IsSystem = false,
                ShowInCleanMode = true,
                Timestamp = DateTime.Now
            };
        }

        private async Task<string> TryDiscoverProductAsync(string userInput)
        {
            try
            {
                var searchCall = new McpToolCall
                {
                    FunctionName = "search_products",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["search_term"] = JsonDocument.Parse(JsonSerializer.Serialize(userInput)).RootElement
                    }
                };
                var result = await _kernelToolsProvider!.ExecuteToolAsync(searchCall, CancellationToken.None);
                var resultText = result?.ToString() ?? string.Empty;
                
                if (!string.IsNullOrWhiteSpace(resultText) && !resultText.StartsWith("PRODUCT_NOT_FOUND", StringComparison.OrdinalIgnoreCase))
                {
                    return resultText;
                }

                // Try token-based search for multilingual phrases
                var tokens = userInput.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(t => t.Length >= 3).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

                foreach (var token in tokens)
                {
                    var tokenCall = new McpToolCall
                    {
                        FunctionName = "search_products", 
                        Arguments = new Dictionary<string, JsonElement>
                        {
                            ["search_term"] = JsonDocument.Parse(JsonSerializer.Serialize(token)).RootElement
                        }
                    };
                    var tokenResult = await _kernelToolsProvider.ExecuteToolAsync(tokenCall, CancellationToken.None);
                    var tokenText = tokenResult?.ToString() ?? string.Empty;
                    
                    if (!string.IsNullOrWhiteSpace(tokenText) && !tokenText.StartsWith("PRODUCT_NOT_FOUND", StringComparison.OrdinalIgnoreCase))
                    {
                        return tokenText;
                    }
                }

                // Try popular items as last resort
                var popularCall = new McpToolCall { FunctionName = "get_popular_items", Arguments = new Dictionary<string, JsonElement>() };
                var popularResult = await _kernelToolsProvider.ExecuteToolAsync(popularCall, CancellationToken.None);
                return popularResult?.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discovery search failed for: {Input}", userInput);
                return string.Empty;
            }
        }

        private string BuildDiscoveryPrompt(string userInput, string searchResults, PosKernel.AI.Services.PromptContext ctx)
        {
            var basePrompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "ordering", ctx);
            
            return $@"{basePrompt}

## DISCOVERY RESULTS
The customer said: ""{userInput}""
Search results found: {searchResults}

Based on these search results, respond naturally as a kopitiam uncle:
- If results show multiple options (like standalone item + set), present both and ask customer to choose
- If results show one clear match, acknowledge and ask if they want to add it
- Keep the tone conversational and helpful
- Use the actual product names and prices from the search results

DO NOT use tool calls in this response - just provide a natural conversational response based on the search results provided.";
        }

        private static string SanitizeToolPhaseContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            using var reader = new StringReader(content);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var trim = line.Trim();
                // Drop internal markers and low-level tool echo
                if (trim.StartsWith("TOOL_CALL", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (trim.StartsWith("TOOL_RESULT", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (trim.StartsWith("THOUGHT", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (trim.StartsWith("EXECUTING_TOOLS", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (trim.StartsWith("AVAILABLE_TOOLS", StringComparison.OrdinalIgnoreCase)) { continue; }
                sb.AppendLine(line);
            }
            return sb.ToString().Trim();
        }

        // ARCHITECTURAL PRINCIPLE: Use currency service; fail fast if missing.
        private string FormatCurrency(decimal amount)
        {
            if (_currencyFormatter != null && _storeConfig != null)
            {
                return _currencyFormatter.FormatCurrency(amount, _storeConfig.Currency, _storeConfig.StoreName);
            }
            
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Currency formatting service not available. " +
                $"Cannot format {amount} without proper currency service. " +
                $"Register ICurrencyFormattingService in DI container.");
        }

        private async Task<ChatMessage> HandleWithSimplifiedOrdering(string userInput, OrderIntentAnalysis intentAnalysis)
        {
            _thoughtLogger.LogThought(
                $"SIMPLIFIED_ORDER_HANDLING: Intent: {intentAnalysis.Intent}, Confidence: {intentAnalysis.ConfidenceLevel:F2}, " +
                $"Items in receipt: {_receipt.Items.Count}, Receipt status: {_receipt.Status}");

            // Content-based fast interpret path (no length heuristics)
            if (_receipt.Status == PaymentStatus.Building 
                && intentAnalysis.Intent != OrderIntent.Question 
                && !LooksLikePaymentMethod(userInput))
            {
                var quick = await TryInterpretAndAddAsync(userInput ?? string.Empty);
                if (quick != null)
                {
                    return quick;
                }
            }

            if (_receipt.Status == PaymentStatus.ReadyForPayment)
            {
                _thoughtLogger.LogThought("PAYMENT_PHASE_INPUT: Delegating to AI payment handling based on conversation context");

                // Stabilization: try deterministic direct payment when possible
                var direct = await TryProcessPaymentDirectAsync(userInput);
                if (direct != null)
                {
                    return direct;
                }

                return await ProcessUserRequestAsync(userInput ?? string.Empty, "payment");
            }

            if (_receipt.Items.Any() && LooksLikePaymentMethod(userInput))
            {
                _thoughtLogger.LogThought("PAYMENT_SHORT_INPUT_DETECTED: Routing to payment context");

                // Stabilization: try deterministic direct payment if method matches config
                var direct = await TryProcessPaymentDirectAsync(userInput);
                if (direct != null)
                {
                    return direct;
                }

                return await ProcessUserRequestAsync(userInput ?? string.Empty, "payment");
            }

            switch (intentAnalysis.Intent)
            {
                case OrderIntent.ContinueOrdering:
                    return await ProcessUserRequestAsync(userInput ?? string.Empty, "ordering");
                
                case OrderIntent.ReadyForPayment:
                    if (_receipt.Items.Any())
                    {
                        _thoughtLogger.LogThought("ORDER_COMPLETION_CONTEXT: Order completion detected - asking for payment method");
                        _receipt.Status = PaymentStatus.ReadyForPayment;
                        return await CreateOrderSummaryWithPaymentPrompt();
                    }
                    else
                    {
                        _thoughtLogger.LogThought("ORDER_COMPLETION_NO_ITEMS: Customer indicating completion but no items in order");
                        return await ProcessUserRequestAsync(userInput ?? string.Empty, "ordering");
                    }
                
                case OrderIntent.Question:
                    _thoughtLogger.LogThought("QUESTION_INTENT: Customer asking question - provide information, don't automatically process");
                    return await ProcessUserRequestAsync(userInput ?? string.Empty, "ordering");
                
                case OrderIntent.Ambiguous:
                    _thoughtLogger.LogThought("AMBIGUOUS_INTENT: Let AI semantic analysis determine intent");
                    return await ProcessUserRequestAsync(userInput ?? string.Empty, "ordering");
                
                default:
                    return await ProcessUserRequestAsync(userInput ?? string.Empty, "ordering");
            }
        }

        private bool TryResolvePaymentMethod(string input, out string methodId)
        {
            methodId = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var normalized = new string(input.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '_').ToArray()).ToLowerInvariant();
            var methods = _storeConfig?.PaymentMethods?.AcceptedMethods;
            if (methods == null)
            {
                return false;
            }

            foreach (var m in methods.Where(m => m.IsEnabled))
            {
                var idNorm = new string((m.MethodId ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '_').ToArray()).ToLowerInvariant();
                var nameNorm = new string((m.DisplayName ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '_').ToArray()).ToLowerInvariant();
                if (normalized == idNorm || normalized == nameNorm)
                {
                    methodId = m.MethodId ?? string.Empty;
                    return true;
                }
            }

            return false;
        }

        private async Task<ChatMessage?> TryProcessPaymentDirectAsync(string? userInput)
        {
            if (!_useRealKernel || _kernelToolsProvider == null)
            {
                return null;
            }

            if (!TryResolvePaymentMethod(userInput ?? string.Empty, out var methodId) || string.IsNullOrWhiteSpace(methodId))
            {
                return null;
            }

            // Execute process_payment directly using tools provider to avoid LLM ambiguity
            var call = new McpToolCall
            {
                FunctionName = "process_payment",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["payment_method"] = JsonDocument.Parse(JsonSerializer.Serialize(methodId)).RootElement
                    // Amount omitted to let kernel use transaction total
                }
            };

            var result = await _kernelToolsProvider.ExecuteToolAsync(call, CancellationToken.None);
            var resultText = result?.ToString() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(resultText) && resultText.StartsWith("PAYMENT_PROCESSED", StringComparison.OrdinalIgnoreCase))
            {
                _receipt.Status = PaymentStatus.Completed;
                try
                {
                    await RefreshReceiptFromKernelAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh receipt after direct payment");
                }

                var msg = new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = "OK, payment received. Thank you!",
                    Timestamp = DateTime.Now,
                    IsSystem = false,
                    ShowInCleanMode = true
                };
                return msg;
            }

            // If kernel reported failure, fall back to normal flow so LLM can handle clarification
            return null;
        }

        // RESTORED: Build tool analysis prompt
        private string BuildToolAnalysisPrompt(PosKernel.AI.Services.PromptContext promptContext, string contextHint)
        {
            var prompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "ordering", promptContext);

            var items = _receipt.Items.Any() ? string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}")) : "none";
            var total = _receipt.Items.Any() ? FormatCurrency(_receipt.Total) : FormatCurrency(0m);

            prompt += "\n\n## CUSTOMER INPUT (verbatim):\n" + (promptContext.UserInput ?? string.Empty) + "\n";
            
            // ARCHITECTURAL FIX: Include recent conversation history for contextual understanding
            prompt += "\n## RECENT CONVERSATION CONTEXT:\n";
            var recentMessages = _conversationHistory.TakeLast(4).Where(m => !m.IsSystem).ToList();
            if (recentMessages.Any())
            {
                foreach (var msg in recentMessages)
                {
                    prompt += $"{msg.Sender}: {msg.Content}\n";
                }
                // ARCHITECTURAL FIX: Removed hardcoded contextual reference guidance - now handled by personality prompts
            }
            else
            {
                prompt += "This is the first interaction in this conversation.\n";
            }
            
            prompt += "\n## CURRENT ORDER STATE:\n" +
                      $"Items: {items}\n" +
                      $"Total: {total}\n" +
                      $"ReceiptStatus: {_receipt.Status}\n";

            prompt += "\nPRODUCT DISCOVERY ENFORCEMENT (Culture-Neutral):\n" +
                      "- Allowed tools in this phase: search_products, get_set_configuration, update_set_configuration, add_item_to_transaction, apply_modifications_to_line, get_popular_items.\n" +
                      "- Forbidden tools in this phase: get_transaction, calculate_transaction_total, process_payment.\n" +
                      "- Do NOT list or suggest specific product names unless they come from tool results in THIS PHASE.\n" +
                      "- When the user mentions an item (any language), FIRST call search_products with their words. If 0 results, retry with a semantically translated/normalized query (token re-ordering, singular/plural, stopword removal, script normalization).\n" +
                      "- Only if still 0 results, ask one concise clarifying question and, if needed, call get_popular_items.\n" +
                      "- If a single result is a SET, add it immediately; otherwise present options from tool results and ask the customer to choose.\n";

            if (_receipt.Items.Any() && _receipt.Status == PaymentStatus.Building)
            {
                prompt += "\nORDER COMPLETION DETECTION (Culture-Neutral): If the customer indicates they are finished ordering in any language or phrasing, " +
                          "IMMEDIATELY transition to payment by calling load_payment_methods_context, provide an order summary with the current total, and ask how they want to pay. " +
                          "NEVER call calculate_transaction_total for completion handling; it is informational only and MUST NOT be used here.\n";
            }

            if (_receipt.Status == PaymentStatus.ReadyForPayment)
            {
                prompt += "\nPAYMENT_PHASE: The receipt is ReadyForPayment.\n" +
                          "If the customer specifies a valid payment method (by name or colloquial term), call process_payment with that method.\n" +
                          "If the method is unclear, first call load_payment_methods_context and then ask the customer to choose explicitly.\n" +
                          "Do not re-list payment methods without new information.\n";
            }

            if (contextHint == "payment" && _receipt.Items.Any())
            {
                prompt += "\n\nTOOL_ANALYSIS_CONTEXT: Customer is specifying a payment method.\n" +
                          $"CURRENT_ORDER: {items}\n" +
                          $"ORDER_TOTAL: {total}\n" +
                          "Act according to the payment method the customer specified using the appropriate tool.";
            }

            prompt += "\n\n## TOOL_EXECUTION_PHASE: Analyze and Execute Tools Only\n" +
                      "Do NOT provide conversational responses in this phase. Determine and execute tools based on the customer's request.\n";

            return prompt;
        }

        // NEW: Fast interpret prompt for short phrases (RAG-style planning via tools)
        private string BuildFastInterpretPrompt(string phrase)
        {
            var items = _receipt.Items.Any() ? string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}")) : "none";
            var total = _receipt.Items.Any() ? FormatCurrency(_receipt.Total) : FormatCurrency(0m);

            // Include the full ordering personality prompt to inherit domain guidance (translation, sets, rules)
            var ctx = new PosKernel.AI.Services.PromptContext
            {
                UserInput = phrase,
                CartItems = items,
                CurrentTotal = total,
                Currency = _receipt.Store.Currency
            };
            var baseOrdering = AiPersonalityFactory.BuildPrompt(_personality.Type, "ordering", ctx);

            var sb = new StringBuilder();
            sb.AppendLine(baseOrdering);
            sb.AppendLine();
            sb.AppendLine("# FAST_INTERPRET OVERRIDE (TOOLS ONLY)");
            sb.AppendLine("You are now in a tools-only micro-phase to interpret the phrase quickly and perform concrete tool calls.");
            sb.AppendLine("GROUNDING:");
            sb.AppendLine("- Allowed tools: search_products, get_set_configuration, update_set_configuration, add_item_to_transaction, apply_modifications_to_line.");
            sb.AppendLine("- Forbidden tools: get_transaction, calculate_transaction_total, process_payment (not ordering).");
            sb.AppendLine("- First call search_products using the exact user phrase. If 0 results, try a semantically translated/normalized variant (token re-ordering, singular/plural, stopword removal, script normalization). Use only results from tools.");
            sb.AppendLine("- Select the best matching SKU. If it is a set, call get_set_configuration/update_set_configuration as needed.");
            sb.AppendLine("- Call add_item_to_transaction with the chosen SKU and a sensible quantity (default 1 unless the phrase specifies otherwise).");
            sb.AppendLine("- If the phrase implies modifications (e.g., sweetness level, ice amount/temperature, add/remove options), call apply_modifications_to_line with appropriate modifications supported by the item.");
            sb.AppendLine("RULES:");
            sb.AppendLine("- Execute tools only. Do not produce conversational text.");
            sb.AppendLine("- Do not invent SKUs or options not present in tool results.");
            sb.AppendLine("- If no viable SKU after retries, ask ONE concise clarifying question and try search_products again. If still none, stop and return control (no more tool calls in this phase).");
            sb.AppendLine("- Do NOT call apply_modifications_to_line unless: (a) you just added the target item in THIS phase, or (b) the phrase explicitly refers to changing an existing line (e.g., 'make the coffee <prep>').");
            sb.AppendLine("- When the phrase names a product (standalone noun), treat it as a new item request (search -> add). Do NOT interpret it as a modification to a different existing item.");
            sb.AppendLine();
            sb.AppendLine($"PHRASE: {phrase}");
            sb.AppendLine("CURRENT ORDER STATE:");
            sb.AppendLine($"- Items: {items}");
            sb.AppendLine($"- Total: {total}");
            sb.AppendLine($"- ReceiptStatus: {_receipt.Status}");
            sb.AppendLine();
            sb.AppendLine("TOOL_EXECUTION_PHASE: Analyze and execute tools only. Do not write a chat reply.");
            return sb.ToString();
        }

        // NEW: Order summary + payment prompt creator (no hardcoded payment lists)
        private Task<ChatMessage> CreateOrderSummaryWithPaymentPrompt()
        {
            var items = _receipt.Items.Any()
                ? string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}"))
                : "none";

            var content = new StringBuilder();
            content.Append($"OK. Your current order: {items}. Total: {FormatCurrency(_receipt.Total)}. ");
            content.Append("How you want to pay?");

            var msg = new ChatMessage
            {
                Sender = _personality.StaffTitle,
                Content = content.ToString(),
                IsSystem = false,
                ShowInCleanMode = true,
                Timestamp = DateTime.Now
            };
            return Task.FromResult(msg);
        }

        // NEW: Refresh receipt snapshot from kernel using tools provider (reflection for portability)
        private async Task RefreshReceiptFromKernelAsync()
        {
            if (!_useRealKernel || _kernelToolsProvider == null)
            {
                return;
            }

            var getTxnCall = new McpToolCall { FunctionName = "get_transaction", Arguments = new Dictionary<string, JsonElement>() };
            var resultObj = await _kernelToolsProvider.ExecuteToolAsync(getTxnCall, CancellationToken.None);

            if (resultObj is string s)
            {
                if (s.StartsWith("NO_TRANSACTION", StringComparison.OrdinalIgnoreCase))
                {
                    _receipt.Items.Clear();
                    _receipt.Tax = 0m;
                    // Preserve ReadyForPayment if previously set by user-space logic
                    if (_receipt.Status != PaymentStatus.ReadyForPayment)
                    {
                        _receipt.Status = PaymentStatus.Building;
                    }
                    return;
                }
                throw new InvalidOperationException($"DESIGN DEFICIENCY: get_transaction returned string instead of structured result: {s}");
            }

            try
            {
                decimal kernelTotal = 0m;
                var type = resultObj.GetType();

                var txnIdProp = type.GetProperty("TransactionId", BindingFlags.Public | BindingFlags.Instance);
                var txnIdVal = txnIdProp?.GetValue(resultObj)?.ToString();
                if (!string.IsNullOrWhiteSpace(txnIdVal))
                {
                    _receipt.TransactionId = txnIdVal!;
                }

                var stateProp = type.GetProperty("State", BindingFlags.Public | BindingFlags.Instance);
                var stateVal = stateProp?.GetValue(resultObj);
                if (stateVal != null)
                {
                    var stateText = stateVal.ToString() ?? string.Empty;
                    if (stateText.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                    {
                        _receipt.Status = PaymentStatus.Completed;
                    }
                    else if (stateText.Equals("Building", StringComparison.OrdinalIgnoreCase))
                    {
                        // Do not downgrade ReadyForPayment just because kernel is Building
                        if (_receipt.Status != PaymentStatus.ReadyForPayment && _receipt.Status != PaymentStatus.Completed)
                        {
                            _receipt.Status = PaymentStatus.Building;
                        }
                    }
                }

                var totalProp = type.GetProperty("Total", BindingFlags.Public | BindingFlags.Instance);
                if (totalProp != null)
                {
                    var totalVal = totalProp.GetValue(resultObj);
                    if (totalVal != null)
                    {
                        kernelTotal = Convert.ToDecimal(totalVal);
                    }
                }

                var lineItemsProp = type.GetProperty("LineItems", BindingFlags.Public | BindingFlags.Instance);
                _receipt.Items.Clear();
                if (lineItemsProp != null)
                {
                    var itemsObj = lineItemsProp.GetValue(resultObj) as System.Collections.IEnumerable;
                    if (itemsObj != null)
                    {
                        foreach (var li in itemsObj)
                        {
                            var liType = li.GetType();
                            var sku = liType.GetProperty("ProductId")?.GetValue(li)?.ToString() ?? string.Empty;
                            var qtyObj = liType.GetProperty("Quantity")?.GetValue(li);
                            var qty = qtyObj != null ? Convert.ToInt32(qtyObj) : 1;
                            var unitPriceObj = liType.GetProperty("UnitPrice")?.GetValue(li);
                            decimal unitPrice = 0m;
                            if (unitPriceObj != null)
                            {
                                unitPrice = Convert.ToDecimal(unitPriceObj);
                            }

                            var lineItemId = liType.GetProperty("LineItemId")?.GetValue(li)?.ToString() ?? string.Empty;
                            var lineNumberObj = liType.GetProperty("LineNumber")?.GetValue(li);
                            var lineNumber = lineNumberObj != null ? Convert.ToInt32(lineNumberObj) : 0;
                            var parentLineNumberObj = liType.GetProperty("ParentLineNumber")?.GetValue(li);
                            uint? parentId = null;
                            if (parentLineNumberObj != null)
                            {
                                var p = Convert.ToInt32(parentLineNumberObj);
                                if (p > 0)
                                {
                                    parentId = (uint)p;
                                }
                            }

                            // Resolve display name using restaurant extension via tools provider, with simple cache
                            string displayName = sku;
                            if (!string.IsNullOrWhiteSpace(sku))
                            {
                                if (_skuNameCache.TryGetValue(sku, out var cached))
                                {
                                    displayName = cached;
                                }
                                else
                                {
                                    try
                                    {
                                        var resolved = await _kernelToolsProvider.ResolveDisplayNameAsync(sku, CancellationToken.None);
                                        if (!string.IsNullOrWhiteSpace(resolved))
                                        {
                                            displayName = resolved!;
                                            _skuNameCache[sku] = displayName;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "ResolveDisplayName failed for sku {Sku}", sku);
                                    }
                                }
                            }

                            _receipt.Items.Add(new PosKernel.AI.Models.ReceiptLineItem
                            {
                                LineItemId = lineItemId,
                                LineNumber = lineNumber,
                                Quantity = qty,
                                ProductName = displayName,
                                UnitPrice = unitPrice,
                                ProductSku = sku,
                                ParentLineItemId = parentId
                            });
                        }
                    }
                }

                // Align receipt total via Tax so that Subtotal + Tax equals kernel total
                var subtotal = _receipt.Subtotal;
                var computedTax = kernelTotal - subtotal;
                _receipt.Tax = computedTax > 0 ? computedTax : 0m;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"DESIGN DEFICIENCY: Could not map kernel transaction to receipt snapshot: {ex.Message}");
            }
        }

        private async Task<ChatMessage?> TryInterpretAndAddAsync(string userInput)
        {
            if (!_useRealKernel || _kernelToolsProvider == null)
            {
                return null;
            }

            // First try fast RAG-style interpretation via MCP tool planning
            var ragAck = await FastInterpretAndAddViaToolsAsync(userInput);
            if (ragAck != null)
            {
                return ragAck;
            }

            // Fallback to legacy interpret_order_phrase tool path
            var interpretCall = new McpToolCall
            {
                FunctionName = "interpret_order_phrase",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["phrase"] = JsonDocument.Parse(JsonSerializer.Serialize(userInput)).RootElement,
                    ["default_quantity"] = JsonDocument.Parse("1").RootElement
                }
            };

            var interpResult = await _kernelToolsProvider.ExecuteToolAsync(interpretCall, CancellationToken.None);
            var interpText = interpResult?.ToString() ?? string.Empty;
            if (!interpText.StartsWith("INTERPRETED:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Parse: INTERPRETED: sku=KOPI002; name=Kopi C; quantity=1; mods=MOD_NO_SUGAR:No Sugar|...
            string? sku = null; string? name = null; int qty = 1; string? mods = null;
            var parts = interpText.Substring("INTERPRETED:".Length).Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var kv = p.Split('=', 2);
                if (kv.Length != 2) { continue; }
                var key = kv[0].Trim();
                var value = kv[1].Trim();
                if (key.Equals("sku", StringComparison.OrdinalIgnoreCase)) { sku = value; }
                else if (key.Equals("name", StringComparison.OrdinalIgnoreCase)) { name = value; }
                else if (key.Equals("quantity", StringComparison.OrdinalIgnoreCase)) { if (int.TryParse(value, out var n)) qty = Math.Max(1, n); }
                else if (key.Equals("mods", StringComparison.OrdinalIgnoreCase)) { mods = value; }
            }
            if (string.IsNullOrWhiteSpace(sku))
            {
                return null;
            }

            var addCall = new McpToolCall
            {
                FunctionName = "add_item_to_transaction",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["product_sku"] = JsonDocument.Parse(JsonSerializer.Serialize(sku)).RootElement,
                    ["quantity"] = JsonDocument.Parse(JsonSerializer.Serialize(qty)).RootElement
                }
            };
            var addResult = await _kernelToolsProvider.ExecuteToolAsync(addCall, CancellationToken.None);
            var addText = addResult?.ToString() ?? string.Empty;
            if (!addText.StartsWith("ITEM_ADDED:", StringComparison.OrdinalIgnoreCase))
            {
                // Fallback to generic acknowledgment if provider returned legacy string
                await RefreshReceiptFromKernelAsync();
                var disp = name ?? sku;
                try
                {
                    var resolved = await _kernelToolsProvider.ResolveDisplayNameAsync(sku!, CancellationToken.None);
                    if (!string.IsNullOrWhiteSpace(resolved)) { disp = resolved; }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ResolveDisplayName failed for sku {Sku}", sku);
                }
                return new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = _renderer.RenderAddItemAck(disp!, 1, _receipt.Total),
                    Timestamp = DateTime.Now,
                    IsSystem = false,
                    ShowInCleanMode = true
                };
            }

            // Parse addText: ITEM_ADDED: sku=...; qty=...; line_id=...; line_number=...
            string? addedLineId = null; int addedLineNumber = 0; int addedQty = qty;
            var addParts = addText.Substring("ITEM_ADDED:".Length).Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in addParts)
            {
                var kv = p.Split('=', 2);
                if (kv.Length != 2) { continue; }
                var key = kv[0].Trim(); var value = kv[1].Trim();
                if (key.Equals("line_id", StringComparison.OrdinalIgnoreCase)) { addedLineId = value; }
                else if (key.Equals("line_number", StringComparison.OrdinalIgnoreCase)) { int.TryParse(value, out addedLineNumber); }
                else if (key.Equals("qty", StringComparison.OrdinalIgnoreCase)) { int.TryParse(value, out addedQty); }
            }

            if (!string.IsNullOrWhiteSpace(mods) && !string.IsNullOrWhiteSpace(addedLineId))
            {
                var applyMods = new McpToolCall
                {
                    FunctionName = "apply_modifications_to_line",
                    Arguments = new Dictionary<string, JsonElement>
                    {
                        ["line_item_id"] = JsonDocument.Parse(JsonSerializer.Serialize(addedLineId)).RootElement,
                        ["expected_sku"] = JsonDocument.Parse(JsonSerializer.Serialize(sku)).RootElement,
                        ["modifications"] = JsonDocument.Parse(JsonSerializer.Serialize(mods)).RootElement
                    }
                };
                await _kernelToolsProvider.ExecuteToolAsync(applyMods, CancellationToken.None);
            }

            await RefreshReceiptFromKernelAsync();
            var display = name ?? sku;
            try
            {
                var resolved = await _kernelToolsProvider.ResolveDisplayNameAsync(sku!, CancellationToken.None);
                if (!string.IsNullOrWhiteSpace(resolved)) { display = resolved; }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ResolveDisplayName failed for sku {Sku}", sku);
            }

            return new ChatMessage
            {
                Sender = _personality.StaffTitle,
                Content = _renderer.RenderAddItemAck(display!, addedQty, _receipt.Total),
                Timestamp = DateTime.Now,
                IsSystem = false,
                ShowInCleanMode = true
            };
        }

        // NEW: RAG-style fast interpret using MCP planning and tool execution
        private async Task<ChatMessage?> FastInterpretAndAddViaToolsAsync(string phrase)
        {
            try
            {
                if (!_useRealKernel || _kernelToolsProvider == null)
                {
                    return null;
                }

                // Snapshot existing line ids to compute diff
                var beforeIds = new HashSet<string>(_receipt.Items.Select(i => i.LineItemId ?? string.Empty));

                var tools = _kernelToolsProvider.GetAvailableTools() ?? Array.Empty<McpTool>();
                var prompt = BuildFastInterpretPrompt(phrase);
                LastPrompt = prompt;

                _thoughtLogger.LogThought("FAST_INTERPRET_START: Using MCP tools to interpret short phrase");
                await _mcpClient.CompleteWithToolsAsync(prompt, tools);

                await RefreshReceiptFromKernelAsync();

                // Find newly added line(s)
                var addedLines = _receipt.Items.Where(i => !beforeIds.Contains(i.LineItemId ?? string.Empty)).ToList();
                if (addedLines.Count == 0)
                {
                    // No concrete addition detected
                    _thoughtLogger.LogThought("FAST_INTERPRET_RESULT: No new items added");
                    return null;
                }

                var added = addedLines.OrderByDescending(l => l.LineNumber).First();
                var name = !string.IsNullOrWhiteSpace(added.ProductName) ? added.ProductName : (added.ProductSku ?? string.Empty);
                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.LogWarning("FAST_INTERPRET_NAME_MISSING: Added line has neither name nor SKU. LineNumber={LineNumber}", added.LineNumber);
                    return null;
                }
                var qty = Math.Max(1, added.Quantity);

                var msg = new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = _renderer.RenderAddItemAck(name, qty, _receipt.Total),
                    Timestamp = DateTime.Now,
                    IsSystem = false,
                    ShowInCleanMode = true
                };
                _thoughtLogger.LogThought($"FAST_INTERPRET_SUCCESS: Added {qty}x {name}");
                return msg;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FAST_INTERPRET_ERROR: {Message}", ex.Message);
                return null;
            }
        }

        private void ScheduleAutoClearIfConfigured()
        {
            if (_autoClearScheduled)
            {
                return;
            }
            if (_storeConfig?.AdditionalConfig == null)
            {
                return;
            }
            if (!_storeConfig.AdditionalConfig.TryGetValue("auto_clear_seconds", out var secondsObj))
            {
                return; // not configured → do nothing (fail-fast policy would throw only if feature was required)
            }
            if (!int.TryParse(secondsObj?.ToString(), out var seconds) || seconds <= 0)
            {
                return;
            }

            _autoClearScheduled = true;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(seconds));
                    // Reset for next customer (UI snapshot will show empty receipt)
                    _receipt.Items.Clear();
                    _receipt.Tax = 0m;
                    _receipt.Status = PaymentStatus.Building;
                    _receipt.TransactionId = Guid.NewGuid().ToString()[..8];
                    _autoClearScheduled = false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AUTO_CLEAR_FAILED: seconds={Seconds}", seconds);
                    _autoClearScheduled = false;
                }
            });
        }
        
        public async Task<ChatMessage> GeneratePostPaymentMessageAsync()
        {
            try
            {
                var ctx = new PosKernel.AI.Services.PromptContext
                {
                    CartItems = _receipt.Items.Any()
                        ? string.Join(", ", _receipt.Items.Select(i => $"{i.Quantity}x {i.ProductName}"))
                        : "none",
                    CurrentTotal = FormatCurrency(_receipt.Total),
                    Currency = _receipt.Store.Currency
                };

                // Prefer personality prompt "payment_complete"; fall back to "post-payment" if that mapping exists in factory
                var prompt = AiPersonalityFactory.BuildPrompt(_personality.Type, "payment_complete", ctx);
                LastPrompt = prompt;
                var response = await _mcpClient.CompleteWithToolsAsync(prompt, new List<McpTool>());

                var message = new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = response.Content ?? "OK, payment received. Thank you!",
                    Timestamp = DateTime.Now,
                    IsSystem = false,
                    ShowInCleanMode = true
                };

                _conversationHistory.Add(message);
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate post-payment message");
                return new ChatMessage
                {
                    Sender = _personality.StaffTitle,
                    Content = "Payment received. Thank you!",
                    Timestamp = DateTime.Now,
                    IsSystem = false,
                    ShowInCleanMode = true
                };
            }
        }
    }
}
