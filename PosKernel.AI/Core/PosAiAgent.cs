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
        private readonly KernelPosToolsProvider? _kernelToolsProvider;
        private readonly PosToolsProvider? _mockToolsProvider;
        private readonly ILogger<PosAiAgent> _logger;
        private readonly StoreConfig _storeConfig;
        private readonly Receipt _receipt;
        private readonly Transaction _transaction;
        private readonly string _storeContextMarkdown; // ARCHITECTURAL FIX: Markdown context as designed

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
            KernelPosToolsProvider? kernelToolsProvider = null,
            PosToolsProvider? mockToolsProvider = null)
        {
            _mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storeConfig = storeConfig ?? throw new ArgumentNullException(nameof(storeConfig));
            _receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            _kernelToolsProvider = kernelToolsProvider;
            _mockToolsProvider = mockToolsProvider;

            // ARCHITECTURAL PRINCIPLE: AI has persistent store knowledge, not injected via prompts
            _storeContextMarkdown = BuildStoreContextMarkdown();

            _logger.LogInformation("AI Agent initialized with persistent store context: {StoreName} ({CultureCode})",
                _storeConfig.StoreName, _storeConfig.CultureCode);
        }

        /// <summary>
        /// Main interaction method - AI handles customer input with full context and direct tool access.
        /// ARCHITECTURAL PRINCIPLE: AI has agency - manages own context and decisions.
        /// </summary>
        public async Task<string> ProcessCustomerInputAsync(string customerInput)
        {
            if (string.IsNullOrWhiteSpace(customerInput))
            {
                // AI-FIRST REDESIGN: State-driven greeting behavior
                var transactionState = await GetCurrentTransactionStateAsync();
                var tools = GetAvailableTools();
                
                var contextWithState = new 
                {
                    StoreContext = _storeContextMarkdown,
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
                _logger.LogDebug("Processing customer input: {Input}", customerInput);

                // AI-FIRST REDESIGN: Get current transaction state for AI context
                var transactionState = await GetCurrentTransactionStateAsync();
                _logger.LogDebug("Current transaction state: {State}", transactionState);

                // Get available tools
                var tools = GetAvailableTools();

                // AI-FIRST PRINCIPLE: AI sees transaction state and makes ALL decisions
                // Create context that includes both store context and current transaction state
                var contextWithState = new 
                {
                    StoreContext = _storeContextMarkdown,
                    TransactionState = transactionState,
                    StateDescription = GetStateDescription(transactionState)
                };

                var aiResponse = await _mcpClient.CompleteWithContextAsync(
                    customerInput, // Raw customer input
                    tools,
                    contextWithState); // Store context + transaction state

                // Execute any tool calls the AI requested
                var responseText = aiResponse.Content;
                if (aiResponse.ToolCalls.Any())
                {
                    _logger.LogInformation("AI requested {ToolCount} tool calls", aiResponse.ToolCalls.Count);

                    foreach (var toolCall in aiResponse.ToolCalls)
                    {
                        try
                        {
                            var toolResult = await ExecuteToolCallAsync(toolCall);
                            _logger.LogDebug("Tool {ToolName} executed: {Result}", toolCall.FunctionName, toolResult);

                            // ARCHITECTURAL PRINCIPLE: Always notify on tool execution - pure infrastructure
                            NotifyReceiptChanged();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Tool execution failed: {ToolName}", toolCall.FunctionName);
                            // ARCHITECTURAL PRINCIPLE: Let AI handle error responses in appropriate cultural context
                            // Don't hardcode English apologies - AI knows the culture and language
                            var errorContext = $"Tool execution failed: {toolCall.FunctionName}. Error: {ex.Message}";
                            var errorResponse = await _mcpClient.CompleteWithContextAsync(
                                errorContext,
                                GetAvailableTools(),
                                _storeContextMarkdown);
                            responseText = errorResponse.Content;
                        }
                    }
                }

                // ARCHITECTURAL PRINCIPLE: No conversation history management - AI maintains context naturally
                return responseText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing customer input: {Input}", customerInput);
                // ARCHITECTURAL PRINCIPLE: Let AI handle technical errors in appropriate cultural context
                // Don't hardcode English error messages - AI knows the culture and language
                try
                {
                    var errorContext = $"System error occurred while processing request. Error: {ex.Message}";
                    var errorResponse = await _mcpClient.CompleteWithContextAsync(
                        errorContext,
                        GetAvailableTools(),
                        _storeContextMarkdown);
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

## Current Inventory
{GetInventorySection()}

## Current Transaction Status
- Items: {_transaction.Lines.Count} items
- Total: {FormatTransactionTotal()}
- Status: {_transaction.State}

## Available Tools
You have access to tools for managing transactions, searching products, and processing payments. Use these tools as needed to serve customers naturally according to local customs and language preferences.
";

            return context;
        }

        /// <summary>
        /// Get inventory section for store context - uses real database if available, culture-neutral fallback.
        /// ARCHITECTURAL PRINCIPLE: Real data first, minimal culture-neutral fallback for development.
        /// </summary>
        private string GetInventorySection()
        {
            if (_kernelToolsProvider != null)
            {
                // ARCHITECTURAL PRINCIPLE: AI should access real inventory without exposing technical details to customers
                return @"**Complete store inventory available** - You have access to the full store inventory including:
- All available products with current pricing
- Real-time inventory status and availability
- Product categories and descriptions

Use the available tools to search products and check pricing when customers place orders. Handle all interactions naturally according to local language and cultural preferences.";
            }
            else
            {
                // ARCHITECTURAL PRINCIPLE: Culture-neutral fallback for development mode
                return @"### Sample Products (Development Mode)
Limited sample inventory for development and testing. Real store inventory will be loaded from database when connected to kernel services.

Use available tools to search and add products to transactions.";
            }
        }

        /// <summary>
        /// Get transaction total for AI context - provide raw data without formatting assumptions.
        /// ARCHITECTURAL PRINCIPLE: Let AI handle all formatting and display decisions.
        /// </summary>
        private string FormatTransactionTotal()
        {
            if (_transaction.Lines.Any())
            {
                var totalMinorUnits = _transaction.Total.MinorUnits;
                // Provide raw amount + currency - let AI decide formatting
                return $"{totalMinorUnits} minor units ({_storeConfig.Currency})";
            }
            return $"0 minor units ({_storeConfig.Currency})";
        }

        /// <summary>
        /// Get available tools based on current configuration.
        /// ARCHITECTURAL PRINCIPLE: Tool availability is infrastructure, not business logic.
        /// </summary>
        private IReadOnlyList<McpTool> GetAvailableTools()
        {
            // Use kernel tools if available, otherwise mock tools
            return _kernelToolsProvider?.GetAvailableTools() ??
                   _mockToolsProvider?.GetAvailableTools() ??
                   new List<McpTool>();
        }

        /// <summary>
        /// Execute a tool call requested by the AI.
        /// ARCHITECTURAL PRINCIPLE: Pure tool execution - no business logic or interpretation.
        /// </summary>
        private async Task<string> ExecuteToolCallAsync(McpToolCall toolCall)
        {
            // Use kernel tools if available, otherwise mock tools
            if (_kernelToolsProvider != null)
            {
                var result = await _kernelToolsProvider.ExecuteToolAsync(toolCall, CancellationToken.None);
                return result?.ToString() ?? "";
            }
            else if (_mockToolsProvider != null)
            {
                return await _mockToolsProvider.ExecuteToolAsync(toolCall, _transaction);
            }
            else
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: No tool provider available. " +
                    "Register either KernelPosToolsProvider or PosToolsProvider in DI container.");
            }
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Pure infrastructure - always notify on tool execution.
        /// Don't interpret tool calls - let subscribers decide what matters.
        /// </summary>
        private void NotifyReceiptChanged()
        {
            try
            {
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
                if (_kernelToolsProvider != null)
                {
                    // Use get_transaction tool to get state from kernel
                    var toolCall = new McpToolCall 
                    { 
                        FunctionName = "get_transaction",
                        Arguments = new Dictionary<string, JsonElement>()
                    };
                    
                    var result = await _kernelToolsProvider.ExecuteToolAsync(toolCall, CancellationToken.None);
                    
                    // Extract state from result
                    if (result is string jsonResult && jsonResult.Contains("State"))
                    {
                        try 
                        {
                            var transactionData = JsonSerializer.Deserialize<JsonElement>(jsonResult);
                            if (transactionData.TryGetProperty("State", out var stateProperty))
                            {
                                return stateProperty.GetString() ?? "StartTransaction";
                            }
                        }
                        catch (JsonException)
                        {
                            _logger.LogWarning("Failed to parse transaction state from: {Result}", jsonResult);
                        }
                    }
                    
                    return "StartTransaction"; // Default for new sessions
                }
                else if (_mockToolsProvider != null)
                {
                    // Mock implementation - check mock transaction state
                    if (_transaction.Lines.Any())
                    {
                        return _transaction.Total.MinorUnits > 0 ? "ItemsPending" : "StartTransaction";
                    }
                    return "StartTransaction";
                }
                
                return "StartTransaction"; // Fallback
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get transaction state");
                return "StartTransaction"; // Safe fallback
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
