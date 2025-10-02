using Microsoft.Extensions.Logging;
using PosKernel.AI.Services;
using PosKernel.AI.Tools;
using PosKernel.Abstractions;
using System.Text.Json;

namespace PosKernel.AI.Core
{
    /// <summary>
    /// AI INFERENCE LOOP - GLOBAL ARCHITECTURE FOR ALL INTERACTIONS
    ///
    /// ARCHITECTURAL PRINCIPLE: Bridge fuzzy understanding to discrete actions through structured reasoning
    ///
    /// This replaces the direct AI → Tool Call pattern with:
    /// Customer Input → AI Reasoning → Tool Selection → Validation → Execution → Response
    ///
    /// KEY BENEFITS:
    /// 1. Preserves fuzziness at reasoning level
    /// 2. Ensures discrete, correct actions
    /// 3. Self-correction through validation
    /// 4. Debugging visibility through reasoning logs
    /// 5. Cultural intelligence through context, not rules
    /// </summary>
    public class AiInferenceLoop
    {
        private readonly McpClient _mcpClient;
        private readonly ConfigurableContextBuilder _contextBuilder;
        private readonly StoreConfig _storeConfig;
        private readonly KernelPosToolsProvider _kernelToolsProvider;
        private readonly ILogger<AiInferenceLoop> _logger;

        private const int MaxInferenceIterations = 1; // TEMP FIX: Reduce to 1 to avoid OpenAI rate limits
        private string? _cachedInventoryContext; // Cache inventory context for efficiency

        public AiInferenceLoop(
            McpClient mcpClient,
            ConfigurableContextBuilder contextBuilder,
            StoreConfig storeConfig,
            KernelPosToolsProvider kernelToolsProvider,
            ILogger<AiInferenceLoop> logger)
        {
            _mcpClient = mcpClient;
            _contextBuilder = contextBuilder;
            _storeConfig = storeConfig;
            _kernelToolsProvider = kernelToolsProvider;
            _logger = logger;
        }

        /// <summary>
        /// CORE INFERENCE LOOP: Process any customer interaction through structured reasoning
        ///
        /// ARCHITECTURAL PRINCIPLE: This works for ALL interactions:
        /// - Ordering: "kopi c kosong" → reasoning about product selection → add_item_to_transaction
        /// - Cultural: "lah finish already" → reasoning about completion → load_payment_methods_context
        /// - Payment: "cash can?" → reasoning about payment method → process_payment
        /// - Questions: "got decaf?" → reasoning about inquiry → search_products
        /// </summary>
        public async Task<InferenceResult> ProcessCustomerInteractionAsync(
            string customerInput,
            string transactionState,
            IReadOnlyList<McpTool> availableTools)
        {
            _logger.LogInformation("🧠 INFERENCE_LOOP: Starting reasoning for input: '{Input}'", customerInput);
            _logger.LogInformation("🧠 INFERENCE_LOOP_DEBUG: Tools available: {ToolCount}, Transaction state: {State}",
                availableTools.Count, transactionState);

            for (int attempt = 1; attempt <= MaxInferenceIterations; attempt++)
            {
                _logger.LogInformation("🧠 INFERENCE_LOOP: Attempt {Attempt}/{Max}", attempt, MaxInferenceIterations);

                try
                {
                    // STEP 1: AI REASONING - Understanding intent and context
                    var reasoning = await ReasonAboutCustomerIntentAsync(customerInput, transactionState, attempt);
                    _logger.LogInformation("🧠 REASONING: {Reasoning}", reasoning.Summary);

                    // STEP 2: TOOL SELECTION - Choose actions based on reasoning
                    var toolPlan = await SelectToolsWithReasoningAsync(reasoning, availableTools);
                    _logger.LogInformation("🔧 TOOL_SELECTION: Selected {Count} tools: {Tools}",
                        toolPlan.SelectedTools.Count,
                        string.Join(", ", toolPlan.SelectedTools.Select(t => t.FunctionName)));

                    _logger.LogDebug("🔧 TOOL_SELECTION_DETAIL: Justification: {Justification}", toolPlan.Justification);

                    // STEP 3: VALIDATION - Verify tool selection makes sense
                    var validation = await ValidateToolSelectionAsync(reasoning, toolPlan, transactionState);
                    _logger.LogInformation("✅ VALIDATION: {Status} - {Message}",
                        validation.IsValid ? "PASSED" : "FAILED",
                        validation.Message);

                    if (!validation.IsValid)
                    {
                        _logger.LogWarning("❌ VALIDATION_FAILED: {Feedback}", validation.Feedback);
                    }

                    if (validation.IsValid)
                    {
                        // STEP 4: EXECUTION - Run validated tools
                        var executionResult = await ExecuteValidatedToolsAsync(toolPlan.SelectedTools);

                        // STEP 5: RESPONSE GENERATION - AI crafts customer response
                        var customerResponse = await GenerateCustomerResponseAsync(
                            customerInput, reasoning, toolPlan, executionResult, transactionState);

                        return new InferenceResult
                        {
                            Success = true,
                            CustomerResponse = customerResponse,
                            Reasoning = reasoning,
                            ToolsExecuted = toolPlan.SelectedTools,
                            ValidationDetails = validation,
                            IterationsUsed = attempt
                        };
                    }
                    else
                    {
                        // STEP 6: SELF-CORRECTION - Learn from validation failure
                        _logger.LogWarning("🔄 SELF_CORRECTION: Validation failed, attempting correction");
                        await IncorporateValidationFeedbackAsync(reasoning, validation);

                        if (attempt == MaxInferenceIterations)
                        {
                            return CreateFailureResult(customerInput, reasoning, validation, attempt);
                        }
                        // Continue to next iteration
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "🚨 INFERENCE_ERROR: Attempt {Attempt} failed", attempt);
                    if (attempt == MaxInferenceIterations)
                    {
                        return CreateErrorResult(customerInput, ex, attempt);
                    }
                }
            }

            throw new InvalidOperationException("Inference loop exceeded maximum iterations without success");
        }

        /// <summary>
        /// STEP 1: AI REASONING - Understanding fuzzy input through natural language intelligence
        ///
        /// ARCHITECTURAL PRINCIPLE: This is NOT pattern matching or hardcoded rules
        /// AI uses its natural language understanding to reason about:
        /// - Customer intent (ordering, payment, inquiry, completion)
        /// - Cultural context (language patterns, social cues)
        /// - Business context (transaction state, available actions)
        /// </summary>
        private async Task<ReasoningResult> ReasonAboutCustomerIntentAsync(
            string customerInput,
            string transactionState,
            int attemptNumber)
        {
            var variables = new Dictionary<string, object>
            {
                ["StoreName"] = _storeConfig.StoreName,
                ["CultureCode"] = _storeConfig.CultureCode,
                ["CurrentTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["TransactionState"] = transactionState,
                ["CustomerInput"] = customerInput,
                ["AttemptNumber"] = attemptNumber,
                ["PreviousAttempts"] = attemptNumber > 1 ? "Previous validation failed - reconsider approach" : "",
                ["InventoryContext"] = await GetInventoryContextAsync()
            };

            // ARCHITECTURAL FIX: Use configured prompts instead of hardcoded reasoning
            var reasoningContext = _contextBuilder.BuildContext("reasoning", variables);

            var reasoningResponse = await _mcpClient.CompleteWithContextAsync(
                customerInput, // Let the prompt handle the reasoning structure
                new List<McpTool>(), // No tools for reasoning phase
                reasoningContext);

            _logger.LogDebug("🧠 REASONING_RAW_RESPONSE: {Response}", reasoningResponse.Content);

            return new ReasoningResult
            {
                CustomerInput = customerInput,
                TransactionState = transactionState,
                RawReasoning = reasoningResponse.Content ?? "",
                Summary = ExtractReasoningSummary(reasoningResponse.Content ?? ""),
                AttemptNumber = attemptNumber
            };
        }        /// <summary>
        /// STEP 2: TOOL SELECTION - Map understood intent to discrete tool calls
        ///
        /// ARCHITECTURAL PRINCIPLE: AI selects tools based on reasoning, not hardcoded mappings
        /// The AI understands:
        /// - Which tools advance the transaction appropriately
        /// - How to map fuzzy intent to discrete actions
        /// - When to use search vs action vs payment tools
        /// </summary>
        private async Task<ToolSelectionResult> SelectToolsWithReasoningAsync(
            ReasoningResult reasoning,
            IReadOnlyList<McpTool> availableTools)
        {
            _logger.LogInformation("🔧 TOOL_SELECTION_DEBUG: Starting tool selection with {ToolCount} available tools", availableTools.Count);

            var variables = new Dictionary<string, object>
            {
                ["StoreName"] = _storeConfig.StoreName,
                ["CultureCode"] = _storeConfig.CultureCode,
                ["CurrentTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["ReasoningSummary"] = reasoning.Summary,
                ["AvailableTools"] = string.Join("\n", availableTools.Select(t => $"- {t.Name}: {t.Description}"))
            };

            // ARCHITECTURAL FIX: Use configured prompts for tool selection
            var toolSelectionContext = _contextBuilder.BuildContext("tool_selection", variables);

            _logger.LogInformation("🔧 TOOL_SELECTION_DEBUG: Calling MCP client with context and {ToolCount} tools", availableTools.Count);

            var toolResponse = await _mcpClient.CompleteWithContextAsync(
                reasoning.CustomerInput, // Original customer input for context
                availableTools, // AI can call tools to execute its selection
                toolSelectionContext);

            _logger.LogInformation("🔧 TOOL_SELECTION_RAW_RESPONSE: {Response}", toolResponse.Content);
            _logger.LogInformation("🔧 TOOL_SELECTION_PARSED_CALLS: {CallCount} calls parsed", toolResponse.ToolCalls.Count);

            var result = new ToolSelectionResult
            {
                SelectedTools = toolResponse.ToolCalls,
                Justification = toolResponse.Content ?? "",
                AvailableTools = availableTools
            };

            _logger.LogInformation("🔧 TOOL_SELECTION_DEBUG: Returning {SelectedCount} selected tools", result.SelectedTools.Count);
            return result;
        }

        /// <summary>
        /// STEP 3: VALIDATION - Verify tool selection makes business sense
        ///
        /// ARCHITECTURAL PRINCIPLE: Meta-cognitive validation prevents errors
        /// A "manager AI" perspective validates the "cashier AI" decisions:
        /// - Does tool selection match stated reasoning?
        /// - Will these tools advance the transaction correctly?
        /// - Are there any business rule violations?
        /// </summary>
        private async Task<ValidationResult> ValidateToolSelectionAsync(
            ReasoningResult reasoning,
            ToolSelectionResult toolPlan,
            string transactionState)
        {
            var variables = new Dictionary<string, object>
            {
                ["StoreName"] = _storeConfig.StoreName,
                ["CultureCode"] = _storeConfig.CultureCode,
                ["CurrentTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["CustomerInput"] = reasoning.CustomerInput,
                ["TransactionState"] = transactionState,
                ["CashierReasoning"] = reasoning.Summary,
                ["SelectedTools"] = string.Join("\n", toolPlan.SelectedTools.Select(t => $"- {t.FunctionName}: {System.Text.Json.JsonSerializer.Serialize(t.Arguments)}")),
                ["CashierJustification"] = toolPlan.Justification
            };

            // ARCHITECTURAL FIX: Use configured prompts for validation
            var validationContext = _contextBuilder.BuildContext("validation", variables);

            var validationResponse = await _mcpClient.CompleteWithContextAsync(
                reasoning.CustomerInput, // Original customer input for context
                new List<McpTool>(), // No tools for validation
                validationContext);

            var validationContent = validationResponse.Content ?? "";
            var isApproved = validationContent.Contains("APPROVED", StringComparison.OrdinalIgnoreCase);

            // ARCHITECTURAL FIX: Log validation decisions for debugging
            _logger.LogInformation("🔍 VALIDATION_RESPONSE: {Response}", validationContent);
            _logger.LogInformation("🔍 VALIDATION_DECISION: {Decision} (contains APPROVED: {HasApproved})",
                isApproved ? "APPROVED" : "REJECTED",
                validationContent.Contains("APPROVED", StringComparison.OrdinalIgnoreCase));

            return new ValidationResult
            {
                IsValid = isApproved,
                Message = validationContent,
                Feedback = isApproved ? "" : ExtractValidationFeedback(validationContent)
            };
        }

        /// <summary>
        /// STEP 4: EXECUTION - Run validated tools with error handling
        /// </summary>
        private async Task<ExecutionResult> ExecuteValidatedToolsAsync(IReadOnlyList<McpToolCall> toolCalls)
        {
            var results = new List<string>();
            var errors = new List<string>();

            foreach (var toolCall in toolCalls)
            {
                try
                {
                    _logger.LogInformation("🔧 TOOL_EXECUTION: Executing {Tool} with args: {Args}",
                        toolCall.FunctionName,
                        string.Join(", ", toolCall.Arguments.Select(kvp => $"{kvp.Key}={kvp.Value}")));

                    var result = await _kernelToolsProvider.ExecuteToolAsync(toolCall, CancellationToken.None);
                    var resultString = result?.ToString() ?? "Success";

                    results.Add(resultString);
                    _logger.LogInformation("✅ TOOL_SUCCESS: {Tool} returned: {Result}",
                        toolCall.FunctionName,
                        resultString.Length > 100 ? resultString.Substring(0, 100) + "..." : resultString);
                }
                catch (Exception ex)
                {
                    var error = $"Tool {toolCall.FunctionName} failed: {ex.Message}";
                    errors.Add(error);
                    _logger.LogError(ex, "❌ TOOL_EXECUTION_FAILED: {Tool} - {Error}", toolCall.FunctionName, ex.Message);
                }
            }

            return new ExecutionResult
            {
                ToolResults = results,
                Errors = errors,
                Success = !errors.Any()
            };
        }

        /// <summary>
        /// STEP 5: RESPONSE GENERATION - AI crafts culturally appropriate customer response
        /// </summary>
        private async Task<string> GenerateCustomerResponseAsync(
            string originalInput,
            ReasoningResult reasoning,
            ToolSelectionResult toolPlan,
            ExecutionResult execution,
            string transactionState)
        {
            var variables = new Dictionary<string, object>
            {
                ["StoreName"] = _storeConfig.StoreName,
                ["CultureCode"] = _storeConfig.CultureCode,
                ["CurrentTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["OriginalCustomerInput"] = originalInput,
                ["ReasoningPerformed"] = reasoning.Summary,
                ["ToolsExecuted"] = string.Join(", ", toolPlan.SelectedTools.Select(t => t.FunctionName)),
                ["ExecutionResults"] = string.Join("; ", execution.ToolResults),
                ["CurrentState"] = transactionState
            };

            // ARCHITECTURAL FIX: Use configured prompts for response generation
            var responseContext = _contextBuilder.BuildContext("response_generation", variables);

            var responseResult = await _mcpClient.CompleteWithContextAsync(
                originalInput, // Original customer input for context
                new List<McpTool>(), // No tools for response generation
                responseContext);

            return responseResult.Content ?? "I've processed your request.";
        }

        // Helper methods for self-correction and error handling
        private Task IncorporateValidationFeedbackAsync(ReasoningResult reasoning, ValidationResult validation)
        {
            _logger.LogInformation("🔄 Incorporating validation feedback for self-correction");
            // This could store feedback for the next iteration
            // For now, the attempt number in reasoning will trigger reconsideration
            return Task.CompletedTask;
        }

        private InferenceResult CreateFailureResult(string input, ReasoningResult reasoning, ValidationResult validation, int attempts)
        {
            return new InferenceResult
            {
                Success = false,
                CustomerResponse = "I'm having trouble understanding your request. Could you please rephrase that?",
                Reasoning = reasoning,
                ValidationDetails = validation,
                IterationsUsed = attempts,
                FailureReason = "Maximum validation attempts exceeded"
            };
        }

        private InferenceResult CreateErrorResult(string input, Exception ex, int attempts)
        {
            return new InferenceResult
            {
                Success = false,
                CustomerResponse = "I'm experiencing a technical issue. Please let me know if you'd like to try again.",
                IterationsUsed = attempts,
                FailureReason = ex.Message
            };
        }

        private string ExtractReasoningSummary(string reasoning)
        {
            // Extract key points from reasoning for logging
            var lines = reasoning.Split('\n');
            var summary = lines.Take(3).FirstOrDefault(l => l.Trim().Length > 0) ?? "Intent analysis completed";
            return summary.Length > 200 ? summary.Substring(0, 200) + "..." : summary;
        }

        private string ExtractValidationFeedback(string validationContent)
        {
            // Extract feedback for improvement
            var lines = validationContent.Split('\n');
            return lines.FirstOrDefault(l => l.Contains("feedback", StringComparison.OrdinalIgnoreCase)) ?? validationContent;
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Lightweight inventory context for AI validation
        /// Provides essential product knowledge without massive token usage
        /// </summary>
        private async Task<string> GetInventoryContextAsync()
        {
            // Use cached context if available to reduce token usage
            if (_cachedInventoryContext != null)
            {
                _logger.LogDebug("🧠 INVENTORY_CONTEXT: Using cached context ({Length} chars)", _cachedInventoryContext.Length);
                return _cachedInventoryContext;
            }

            try
            {
                _logger.LogDebug("🧠 INVENTORY_CONTEXT: Loading fresh inventory context via search_products");

                // Use search_products to get basic inventory summary
                var searchTerm = JsonSerializer.SerializeToElement("kopi");
                var dummyToolCall = new McpToolCall
                {
                    FunctionName = "search_products",
                    Arguments = new Dictionary<string, JsonElement> { ["search_term"] = searchTerm }
                };

                var result = await _kernelToolsProvider.ExecuteToolAsync(dummyToolCall, CancellationToken.None);
                var resultString = result?.ToString() ?? "";

                _logger.LogInformation("🧠 INVENTORY_CONTEXT: Search tool executed, result length: {Length} chars", resultString.Length);
                _logger.LogDebug("🧠 INVENTORY_CONTEXT: Full search result: {Result}", resultString);

                if (resultString.Contains("ERROR") || resultString.Contains("not found") || string.IsNullOrWhiteSpace(resultString))
                {
                    // ARCHITECTURAL PRINCIPLE: No cultural fallbacks - fail fast with clear error
                    throw new InvalidOperationException(
                        "DESIGN DEFICIENCY: Cannot load inventory context for AI validation. " +
                        $"Search tool returned: {resultString}. " +
                        "AI requires product knowledge to validate customer orders. " +
                        "Ensure restaurant extension catalog is properly initialized.");
                }
                else
                {
                    _cachedInventoryContext = $"Available products: {resultString}";
                    _logger.LogInformation("🧠 INVENTORY_CONTEXT: Successfully cached inventory context ({Length} chars)",
                        _cachedInventoryContext.Length);
                }

                return _cachedInventoryContext;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🧠 INVENTORY_CONTEXT: Failed to load inventory context");
                // ARCHITECTURAL PRINCIPLE: No cultural fallbacks - fail fast
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: Cannot load inventory context for AI validation. " +
                    $"Search service failed: {ex.Message}. " +
                    "AI requires product knowledge to validate customer orders. " +
                    "Ensure restaurant extension and catalog service are properly configured.", ex);
            }
        }
    }

    /// <summary>
    /// Results and data structures for inference loop
    /// </summary>
    public class InferenceResult
    {
        public bool Success { get; set; }
        public string CustomerResponse { get; set; } = "";
        public ReasoningResult? Reasoning { get; set; }
        public IReadOnlyList<McpToolCall> ToolsExecuted { get; set; } = new List<McpToolCall>();
        public ValidationResult? ValidationDetails { get; set; }
        public int IterationsUsed { get; set; }
        public string? FailureReason { get; set; }
    }

    public class ReasoningResult
    {
        public string CustomerInput { get; set; } = "";
        public string TransactionState { get; set; } = "";
        public string RawReasoning { get; set; } = "";
        public string Summary { get; set; } = "";
        public int AttemptNumber { get; set; }
    }

    public class ToolSelectionResult
    {
        public IReadOnlyList<McpToolCall> SelectedTools { get; set; } = new List<McpToolCall>();
        public string Justification { get; set; } = "";
        public IReadOnlyList<McpTool> AvailableTools { get; set; } = new List<McpTool>();
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = "";
        public string Feedback { get; set; } = "";
    }

    public class ExecutionResult
    {
        public List<string> ToolResults { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public bool Success { get; set; }
    }
}
