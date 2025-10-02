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
using System.Text.Json;

namespace PosKernel.AI.Core
{
    /// <summary>
    /// AI AGENT - TRUE AI-FIRST ARCHITECTURE (REDESIGN 2025-09-29)
    ///
    /// ARCHITECTURAL PRINCIPLES:
    /// 1. AI has persistent knowledge of store context (menu, prices, culture, policies)
    /// 2. AI directly calls tools (AddLineItem, ProcessPayment) based on understanding
    /// 3. NO prompt building, response parsing, or orchestration logic
    /// 4. Code provides pure tool infrastructure only
    /// 5. AI output goes directly to customer - no interpretation
    ///
    /// This eliminates the orchestrator pattern that was causing cultural hardcoding.
    /// AI behaves like an experienced local cashier with full store knowledge.
    /// </summary>
    public class PosAiAgent : IReceiptChangeNotifier
    {
        private readonly McpClient _mcpClient;
        private readonly KernelPosToolsProvider _kernelToolsProvider;
        private readonly ILogger<PosAiAgent> _logger;
        private readonly StoreConfig _storeConfig;
        private readonly Receipt _receipt;
        private readonly Transaction _transaction;
        private readonly ConfigurableContextBuilder _contextBuilder;
        private readonly string _baseCulturalContext; // ARCHITECTURAL FIX: Load cultural context from prompt files
        private readonly AiInferenceLoop _inferenceLoop; // ARCHITECTURAL UPGRADE: Global inference loop for all interactions

        // ARCHITECTURAL PRINCIPLE: Receipt changes are infrastructure events, not business logic
        public event EventHandler<ReceiptChangedEventArgs>? ReceiptChanged;

        // ARCHITECTURAL PRINCIPLE: Expose receipt for UI integration
        public Receipt Receipt => _receipt;

        public PosAiAgent(
            McpClient mcpClient,
            ILogger<PosAiAgent> logger,
            StoreConfig storeConfig,
            Receipt receipt,
            Transaction transaction,
            ConfigurableContextBuilder contextBuilder,
            KernelPosToolsProvider kernelToolsProvider)
        {
            _mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storeConfig = storeConfig ?? throw new ArgumentNullException(nameof(storeConfig));
            _receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
            _kernelToolsProvider = kernelToolsProvider ?? throw new ArgumentNullException(nameof(kernelToolsProvider));

            // ARCHITECTURAL BRIDGE: Load cultural context from configuration-driven system
            var variables = new Dictionary<string, object>
            {
                ["StoreName"] = _storeConfig.StoreName,
                ["CultureCode"] = _storeConfig.CultureCode,
                ["MenuLanguage"] = _storeConfig.MenuLanguage,
                ["Currency"] = _storeConfig.Currency,
                ["CurrentTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["SystemMessage"] = "You are an experienced, friendly cashier who understands the local culture and helps customers naturally."
            };
            _baseCulturalContext = _contextBuilder.BuildContext("StartTransaction", variables);

            // ARCHITECTURAL UPGRADE: Initialize inference loop for global structured reasoning
            _inferenceLoop = new AiInferenceLoop(
                mcpClient,
                contextBuilder,
                storeConfig,
                kernelToolsProvider,
                logger as ILogger<AiInferenceLoop> ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<AiInferenceLoop>());

            _logger.LogInformation("AI Agent initialized with kernel tools provider: {StoreName} ({CultureCode})",
                _storeConfig.StoreName, _storeConfig.CultureCode);
        }

        /// <summary>
        /// Main interaction method - AI handles customer input with full context and direct tool access.
        /// ARCHITECTURAL PRINCIPLE: AI has agency - manages own context and decisions.
        /// ARCHITECTURAL UPGRADE: Now uses inference loop for structured reasoning and validation.
        /// </summary>
        public async Task<string> ProcessCustomerInputAsync(string customerInput)
        {
            if (string.IsNullOrWhiteSpace(customerInput))
            {
                // AI-FIRST REDESIGN: State-driven greeting behavior
                var transactionState = await GetCurrentTransactionStateAsync();
                var tools = GetAvailableTools();

                var variables = new Dictionary<string, object>
                {
                    ["StoreName"] = _storeConfig.StoreName,
                    ["CultureCode"] = _storeConfig.CultureCode,
                    ["CurrentTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["TransactionState"] = transactionState,
                    ["ItemCount"] = _transaction.Lines.Count,
                    ["TransactionTotal"] = FormatTransactionTotal()
                };

                var contextWithState = new
                {
                    StoreContext = _contextBuilder.BuildContext(transactionState, variables),
                    TransactionState = transactionState,
                    StateDescription = GetStateDescription(transactionState)
                };

                var aiResponse = await _mcpClient.CompleteWithContextAsync(
                    "Customer has just approached", // AI sees state and responds appropriately
                    tools,
                    contextWithState);
                return aiResponse.Content;
            }

            try
            {
                _logger.LogInformation("🔍 AI_AGENT_DEBUG: ProcessCustomerInputAsync called with input: '{Input}'", customerInput);

                // ARCHITECTURAL UPGRADE: Use inference loop for ALL customer interactions
                // This replaces the direct AI → Tool Call pattern with structured reasoning

                // Get current transaction state for inference loop context
                var transactionState = await GetCurrentTransactionStateAsync();
                _logger.LogDebug("Current transaction state: {State}", transactionState);

                // Get available tools for inference loop
                var tools = GetAvailableTools();
                _logger.LogInformation("🔍 AI_AGENT_DEBUG: Available tools count: {ToolCount}", tools.Count);

                // INFERENCE LOOP: Process through structured reasoning → validation → execution
                _logger.LogInformation("🔍 AI_AGENT_DEBUG: About to call inference loop with state: {State}", transactionState);

                InferenceResult inferenceResult;
                try
                {
                    inferenceResult = await _inferenceLoop.ProcessCustomerInteractionAsync(
                        customerInput,
                        transactionState,
                        tools);
                    _logger.LogInformation("🔍 AI_AGENT_DEBUG: Inference loop completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "🔍 AI_AGENT_DEBUG: Inference loop threw exception");
                    throw;
                }
                _logger.LogInformation("🧠 INFERENCE_RESULT: Success={Success}, Iterations={Iterations}, Tools={ToolCount}",
                    inferenceResult.Success,
                    inferenceResult.IterationsUsed,
                    inferenceResult.ToolsExecuted.Count);

                if (inferenceResult.Success)
                {
                    // ARCHITECTURAL PRINCIPLE: Sync Receipt/Transaction with kernel state after tool execution
                    if (inferenceResult.ToolsExecuted.Any())
                    {
                        await SyncWithKernelStateAsync();
                        NotifyReceiptChanged();
                    }

                    return inferenceResult.CustomerResponse;
                }
                else
                {
                    // ARCHITECTURAL PRINCIPLE: Let AI handle failures gracefully
                    _logger.LogWarning("🚨 INFERENCE_FAILED: {Reason}", inferenceResult.FailureReason);
                    return inferenceResult.CustomerResponse; // AI-generated failure response
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing customer input: {Input}", customerInput);
                // ARCHITECTURAL PRINCIPLE: Let AI handle technical errors in appropriate cultural context
                // Don't hardcode English error messages - AI knows the culture and language
                try
                {
                    var errorContext = $"System error occurred while processing request. Error: {ex.Message}";

                    // Build error context with current state for graceful error handling
                    var errorVariables = new Dictionary<string, object>
                    {
                        ["StoreName"] = _storeConfig.StoreName,
                        ["CultureCode"] = _storeConfig.CultureCode,
                        ["CurrentTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ["ErrorMessage"] = ex.Message,
                        ["SystemContext"] = "System error requiring graceful customer communication"
                    };

                    var errorResponse = await _mcpClient.CompleteWithContextAsync(
                        errorContext,
                        GetAvailableTools(),
                        _contextBuilder.BuildContext("error", errorVariables));
                    return errorResponse.Content;
                }
                catch
                {
                    // FAIL FAST - If AI can't handle the error, something is seriously wrong
                    throw new InvalidOperationException(
                        "DESIGN DEFICIENCY: AI system unavailable and cannot handle error responses. " +
                        "System requires functioning AI to provide culturally appropriate error handling.");
                }
            }
        }

        /// <summary>
        /// ARCHITECTURAL BRIDGE: Build complete store context from configuration-driven system
        /// Combines cultural knowledge from prompt files with current store/transaction info
        /// </summary>
        private string BuildCompleteStoreContext(string transactionState)
        {
            var variables = new Dictionary<string, object>
            {
                ["StoreName"] = _storeConfig.StoreName,
                ["CultureCode"] = _storeConfig.CultureCode,
                ["CurrentTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["TransactionState"] = transactionState,
                ["ItemCount"] = _transaction.Lines.Count,
                ["TransactionTotal"] = FormatTransactionTotal(),
                ["InventoryStatus"] = GetInventoryStatus(),
                ["ToolsAvailable"] = GetAvailableTools().Count > 0 ? "TRUE" : "FALSE"
            };

            return _contextBuilder.BuildContext(transactionState, variables);
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Build pure infrastructure context - AI owns all cultural intelligence.
        /// Load real store data from services, not hardcoded assumptions.
        /// </summary>
        private string BuildStoreContextMarkdown()
        {
            var context = $@"# Store Context: {_storeConfig.StoreName}

## Store Information
- **Culture**: {_storeConfig.CultureCode}
- **Primary Language**: {_storeConfig.MenuLanguage}
- **Currency**: {_storeConfig.Currency}

## Inventory Status
- **Status**: {GetInventoryStatus()}

## Current Transaction Status
- Items: {_transaction.Lines.Count} items
- Total: {FormatTransactionTotal()}
- Status: {_transaction.State}

## Available Tools
- **Tools Available**: {(GetAvailableTools().Count > 0 ? "Yes" : "No")}
- **Tool Count**: {GetAvailableTools().Count}
";

            return context;
        }

        /// <summary>
        /// Get inventory status for store context - provides factual data only, no AI instructions.
        /// ARCHITECTURAL PRINCIPLE: Pure infrastructure data - AI decides how to use this information.
        /// </summary>
        private string GetInventoryStatus()
        {
            // ARCHITECTURAL PRINCIPLE: Factual inventory status only - no prompting or instructions
            return "KERNEL_INVENTORY_AVAILABLE";
        }

        /// <summary>
        /// Get transaction total for AI context - provide raw data without formatting assumptions.
        /// ARCHITECTURAL PRINCIPLE: Let AI handle all formatting and display decisions.
        /// </summary>
        private string FormatTransactionTotal()
        {
            if (_transaction.Lines.Any())
            {
                var totalAmount = _transaction.Total.Amount;
                // Provide raw amount + currency - let AI decide formatting
                return $"{totalAmount} ({_storeConfig.Currency})";
            }
            return $"0 ({_storeConfig.Currency})";
        }

        /// <summary>
        /// Get available tools based on current configuration.
        /// ARCHITECTURAL PRINCIPLE: Tool availability is infrastructure, not business logic.
        /// </summary>
        private IReadOnlyList<McpTool> GetAvailableTools()
        {
            // ARCHITECTURAL PRINCIPLE: Only kernel tools - mock provider removed
            return _kernelToolsProvider.GetAvailableTools();
        }

        /// <summary>
        /// Execute a tool call requested by the AI.
        /// ARCHITECTURAL PRINCIPLE: Pure tool execution - no business logic or interpretation.
        /// </summary>
        private async Task<string> ExecuteToolCallAsync(McpToolCall toolCall)
        {
            // ARCHITECTURAL PRINCIPLE: Only kernel tools - execute directly
            var result = await _kernelToolsProvider.ExecuteToolAsync(toolCall, CancellationToken.None);
            return result?.ToString() ?? "";
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Pure infrastructure - always notify on tool execution.
        /// Don't interpret tool calls - let subscribers decide what matters.
        /// </summary>
        private void NotifyReceiptChanged()
        {
            try
            {
                _logger.LogDebug("[RECEIPT_EVENT] Firing ReceiptChanged event - TransactionId='{TransactionId}', StoreName='{StoreName}', ItemCount={ItemCount}",
                    _receipt.TransactionId ?? "NULL",
                    _receipt.Store?.Name ?? "NULL",
                    _receipt.Items?.Count ?? 0);

                var args = new ReceiptChangedEventArgs(ReceiptChangeType.ItemsUpdated, _receipt, "AI Agent tool execution");
                ReceiptChanged?.Invoke(this, args);
                _logger.LogDebug("Receipt change notification sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notifying receipt change");
            }
        }

        /// <summary>
        /// Gets human-readable description of transaction state for AI context.
        /// ARCHITECTURAL PRINCIPLE: AI understands state meaning naturally.
        /// </summary>
        private static string GetStateDescription(string transactionState)
        {
            return transactionState switch
            {
                "StartTransaction" => "Fresh transaction ready for customer interaction - AI should greet customer naturally",
                "ItemsPending" => "Items added but transaction incomplete - AI should offer suggestions and clarifications",
                "PaymentPending" => "Ready for payment processing - AI should guide payment method selection",
                "EndOfTransaction" => "Transaction complete - AI should provide receipt/farewell then auto-transition to StartTransaction",
                _ => "Unknown state - AI should handle gracefully"
            };
        }

        /// <summary>
        /// Gets current transaction state for AI context.
        /// ARCHITECTURAL PRINCIPLE: AI sees transaction state to drive behavior naturally.
        /// </summary>
        private async Task<string> GetCurrentTransactionStateAsync()
        {
            try
            {
                // ARCHITECTURAL PRINCIPLE: Always use kernel tools - no mock fallback
                var toolCall = new McpToolCall
                {
                    FunctionName = "get_transaction",
                    Arguments = new Dictionary<string, JsonElement>()
                };

                var result = await _kernelToolsProvider.ExecuteToolAsync(toolCall, CancellationToken.None);

                // Extract state from the comprehensive formatted result
                if (result is string formattedResult && formattedResult.Contains("Transaction_State:"))
                {
                    try
                    {
                        // Parse the LLM-optimized format to extract state
                        var lines = formattedResult.Split('\n');
                        var stateLine = lines.FirstOrDefault(line => line.StartsWith("Transaction_State:"));
                        if (stateLine != null)
                        {
                            var state = stateLine.Substring("Transaction_State:".Length).Trim();
                            return string.IsNullOrEmpty(state) ? "StartTransaction" : state;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse transaction state from formatted result");
                    }
                }

                return "StartTransaction"; // Default for new sessions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get transaction state");
                return "StartTransaction"; // Safe fallback
            }
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Sync Receipt/Transaction objects with kernel state after tool execution
        /// Ensures UI shows accurate data from authoritative kernel source - NO CALCULATIONS OR PARSING
        /// </summary>
        private async Task SyncWithKernelStateAsync()
        {
            try
            {
                _logger.LogDebug("🔄 SYNC_DEBUG: Starting sync with kernel state");

                // Get structured transaction data directly from kernel (not formatted text)
                var kernelTransaction = await _kernelToolsProvider.GetStructuredTransactionAsync();

                if (kernelTransaction == null)
                {
                    _logger.LogWarning("🔄 SYNC_DEBUG: No transaction data available from kernel");
                    return;
                }

                _logger.LogDebug("🔄 SYNC_DEBUG: Received structured kernel data with {LineCount} line items, State: {State}",
                    kernelTransaction.LineItems?.Count ?? 0, kernelTransaction.State);

                // ARCHITECTURAL FIX: Clear receipt when starting a new transaction OR when transaction completed
                bool isStartTransaction = kernelTransaction.State?.Equals("StartTransaction", StringComparison.OrdinalIgnoreCase) == true;
                bool isCompletedTransaction = kernelTransaction.State?.Equals("Completed", StringComparison.OrdinalIgnoreCase) == true;

                if ((isStartTransaction && kernelTransaction.LineItems?.Count == 0) || isCompletedTransaction)
                {
                    string reason = isCompletedTransaction ? "Transaction completed - clearing for next customer"
                                                         : "New transaction started";
                    _logger.LogInformation("🧾 RECEIPT_CLEAR: Clearing receipt for transition ({Reason})", reason);

                    // Clear transaction and receipt for fresh start
                    _transaction.Lines.Clear();
                    _receipt.Items.Clear();
                    _receipt.TransactionId = "";
                    _transaction.Total = new Money(0, _storeConfig.Currency);

                    // Notify UI of receipt clear
                    var clearArgs = new ReceiptChangedEventArgs(ReceiptChangeType.Cleared, _receipt, reason);
                    ReceiptChanged?.Invoke(this, clearArgs);
                    _logger.LogDebug("Receipt cleared notification sent for: {Reason}", reason);
                    return;
                }

                // Sync Transaction object with kernel-calculated values (NO CALCULATIONS HERE)
                _transaction.Lines.Clear();

                if (kernelTransaction.LineItems != null)
                {
                    foreach (var kernelLine in kernelTransaction.LineItems)
                    {
                        _logger.LogInformation("🔍 SYNC_DEBUG: KernelLine ProductId={ProductId}, ProductName='{ProductName}', ProductDescription='{ProductDescription}'",
                            kernelLine.ProductId, kernelLine.ProductName ?? "(null)", kernelLine.ProductDescription ?? "(null)");

                        var transactionLine = new TransactionLine(_storeConfig.Currency)
                        {
                            ProductId = new ProductId(kernelLine.ProductId),
                            ProductIdString = kernelLine.ProductId,
                            ProductName = kernelLine.ProductName ?? "", // ✅ FROM KERNEL
                            ProductDescription = kernelLine.ProductDescription ?? "", // ✅ FROM KERNEL
                            Quantity = kernelLine.Quantity,
                            UnitPrice = new Money(kernelLine.UnitPrice, _storeConfig.Currency),
                            Extended = new Money(kernelLine.ExtendedPrice, _storeConfig.Currency) // ✅ KERNEL-CALCULATED
                        };
                        _transaction.Lines.Add(transactionLine);

                        _logger.LogInformation("🔍 SYNC_DEBUG: TransactionLine ProductName='{ProductName}', ProductId='{ProductId}'",
                            transactionLine.ProductName ?? "(null)", transactionLine.ProductIdString);
                    }
                }

                // Update transaction totals from kernel
                _transaction.Total = new Money(kernelTransaction.Total, _storeConfig.Currency);

                // Sync Receipt object to display kernel data (NO CALCULATIONS HERE)
                _logger.LogInformation("🔍 KERNEL_TRANSACTION_DEBUG: kernelTransaction.TransactionId='{KernelTransactionId}'",
                    kernelTransaction.TransactionId ?? "NULL");

                _receipt.TransactionId = kernelTransaction.TransactionId ?? "";
                _receipt.Store.Name = _storeConfig.StoreName;
                _receipt.Store.Currency = _storeConfig.Currency;

                _logger.LogInformation("🔍 RECEIPT_SYNC_DEBUG: TransactionId='{TransactionId}', StoreName='{StoreName}'",
                    _receipt.TransactionId, _receipt.Store.Name);

                _receipt.Items.Clear();
                foreach (var transactionLine in _transaction.Lines)
                {
                    var receiptLine = new PosKernel.AI.Models.ReceiptLineItem
                    {
                        ProductName = !string.IsNullOrEmpty(transactionLine.ProductName)
                            ? transactionLine.ProductName
                            : transactionLine.ProductId.Value, // Fallback only if no name
                        Quantity = transactionLine.Quantity,
                        UnitPrice = transactionLine.UnitPrice.Amount,
                        ProductSku = transactionLine.ProductId.Value
                    };

                    _logger.LogInformation("🔍 RECEIPT_DEBUG: ReceiptLine ProductName='{ProductName}' (fallback used: {FallbackUsed})",
                        receiptLine.ProductName, string.IsNullOrEmpty(transactionLine.ProductName));

                    _receipt.Items.Add(receiptLine);
                }

                _logger.LogDebug("🔄 SYNC_DEBUG: Synced Receipt/Transaction with kernel: {ItemCount} items, total: {Total}",
                    _transaction.Lines.Count, FormatTransactionTotal());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔄 SYNC_DEBUG: Failed to sync with kernel state");
            }
        }
    }

    /// <summary>
    /// Rich store context with full inventory and cultural knowledge.
    /// ARCHITECTURAL PRINCIPLE: AI has complete store understanding like a local cashier.
    /// </summary>
    public class StoreContext
    {
        public StoreBasicInfo BasicInfo { get; set; } = new();
        public List<InventoryItem> Inventory { get; set; } = new();
        public CulturalContext CulturalContext { get; set; } = new();
        public StorePolicies Policies { get; set; } = new();
        public AgentTransactionContext CurrentTransaction { get; set; } = new();
    }

    public class StoreBasicInfo
    {
        public string Name { get; set; } = "";
        public string Location { get; set; } = "";
        public string Culture { get; set; } = "";
        public string PrimaryLanguage { get; set; } = "";
        public string Currency { get; set; } = "";
    }

    public class InventoryItem
    {
        public string Sku { get; set; } = "";
        public string[] Names { get; set; } = Array.Empty<string>();
        public ProductVariation[] Variations { get; set; } = Array.Empty<ProductVariation>();
    }

    public class ProductVariation
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Price { get; set; }
        public string[] Modifiers { get; set; } = Array.Empty<string>();
    }

    public class CulturalContext
    {
        public LocalPhrase[] LocalPhrases { get; set; } = Array.Empty<LocalPhrase>();
        public string[] CommonGreetings { get; set; } = Array.Empty<string>();
        public string[] CompletionIndicators { get; set; } = Array.Empty<string>();
    }

    public class LocalPhrase
    {
        public string Phrase { get; set; } = "";
        public string Meaning { get; set; } = "";
    }

    public class StorePolicies
    {
        public string GreetingStyle { get; set; } = "";
        public List<string> PaymentMethods { get; set; } = new();
        public string ServiceStyle { get; set; } = "";
    }

    public class AgentTransactionContext
    {
        public List<TransactionLine> Items { get; set; } = new();
        public decimal SubTotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public string PaymentStatus { get; set; } = "";
    }
}
