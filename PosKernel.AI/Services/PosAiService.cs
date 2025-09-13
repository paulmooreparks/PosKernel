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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PosKernel.Host;
using PosKernel.AI.Core;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Core AI service for POS operations, providing natural language processing and intelligent assistance.
    /// Acts as the primary interface between POS operations and AI capabilities.
    /// </summary>
    public class PosAiService : IDisposable
    {
        private readonly McpClient _mcpClient;
        private readonly ITransactionContextProvider _contextProvider;
        private readonly IAiPromptEngine _promptEngine;
        private readonly IAiSecurityService _securityService;
        private bool _disposed;

        public PosAiService(
            McpClient mcpClient,
            ITransactionContextProvider contextProvider,
            IAiPromptEngine promptEngine,
            IAiSecurityService securityService)
        {
            _mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
            _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
            _promptEngine = promptEngine ?? throw new ArgumentNullException(nameof(promptEngine));
            _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        }

        /// <summary>
        /// Processes natural language input to generate transaction suggestions.
        /// Example: "Add two coffees and a muffin" -> Structured transaction items
        /// </summary>
        public async Task<TransactionSuggestion> ProcessNaturalLanguageAsync(
            string userInput,
            TransactionContext context)
        {
            if (string.IsNullOrWhiteSpace(userInput))
                throw new ArgumentException("User input cannot be null or empty", nameof(userInput));

            try
            {
                // Create AI prompt for transaction processing
                var prompt = await _promptEngine.CreateTransactionPromptAsync(userInput, context);
                
                // Build secure context for AI
                var aiContext = await _contextProvider.BuildAiContextAsync(context);

                // Process with AI
                var response = await _mcpClient.CompleteAsync(prompt, aiContext);

                // Parse the AI response into structured transaction suggestion
                var suggestion = await ParseTransactionSuggestionAsync(response, context);

                return suggestion;
            }
            catch (Exception ex)
            {
                throw new AiProcessingException($"Failed to process natural language input: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Analyzes a transaction for potential fraud or anomalies in real-time.
        /// </summary>
        public async Task<FraudRiskAssessment> AnalyzeTransactionRiskAsync(Transaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            try
            {
                // Build analysis context - filtered for security
                var analysisContext = await _contextProvider.BuildAnalysisContextAsync(transaction);
                
                // Request fraud analysis from AI
                var response = await _mcpClient.AnalyzeAsync("fraud_detection", analysisContext);

                // Parse risk assessment
                var riskAssessment = await ParseFraudRiskAssessmentAsync(response);

                return riskAssessment;
            }
            catch (Exception ex)
            {
                // Fail safe - if AI is unavailable, assume low risk but log the issue
                await _securityService.LogAiInteractionAsync(new AiAuditEvent
                {
                    RequestType = "fraud_analysis",
                    Success = false,
                    Error = ex.Message,
                    Timestamp = DateTimeOffset.UtcNow
                });

                return new FraudRiskAssessment
                {
                    RiskLevel = RiskLevel.Unknown,
                    Confidence = 0.0,
                    Reason = "AI analysis unavailable",
                    RecommendedAction = "Manual review recommended"
                };
            }
        }

        /// <summary>
        /// Generates intelligent upselling suggestions based on current transaction.
        /// </summary>
        public async Task<IEnumerable<UpsellSuggestion>> GetUpsellSuggestionsAsync(Transaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            try
            {
                // Build context for upselling analysis
                var context = await _contextProvider.BuildUpsellContextAsync(transaction);
                
                // Request upselling suggestions
                var response = await _mcpClient.AnalyzeAsync("upsell_analysis", context);
                
                // Parse suggestions
                var suggestions = await ParseUpsellSuggestionsAsync(response);
                
                return suggestions;
            }
            catch (Exception ex)
            {
                // Fail gracefully - upselling is nice-to-have
                return Enumerable.Empty<UpsellSuggestion>();
            }
        }

        /// <summary>
        /// Analyzes inventory impact of current transaction.
        /// </summary>
        public async Task<IEnumerable<InventoryInsight>> CheckInventoryImpactAsync(Transaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            try
            {
                var context = await _contextProvider.BuildInventoryContextAsync(transaction);
                var response = await _mcpClient.AnalyzeAsync("inventory_impact", context);
                var insights = await ParseInventoryInsightsAsync(response);
                
                return insights;
            }
            catch (Exception ex)
            {
                return Enumerable.Empty<InventoryInsight>();
            }
        }

        /// <summary>
        /// Processes a natural language query about transaction data or business insights.
        /// Example: "How much did we sell yesterday?" or "What's our best-selling item?"
        /// </summary>
        public async Task<QueryResult> ProcessNaturalLanguageQueryAsync(string query, BusinessContext businessContext)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("Query cannot be null or empty", nameof(query));

            try
            {
                var prompt = await _promptEngine.CreateQueryPromptAsync(query, businessContext);
                var aiContext = await _contextProvider.BuildQueryContextAsync(businessContext);
                var response = await _mcpClient.CompleteAsync(prompt, aiContext);
                
                return await ParseQueryResultAsync(response);
            }
            catch (Exception ex)
            {
                return new QueryResult
                {
                    Success = false,
                    Answer = "I'm sorry, I couldn't process your query at this time.",
                    Error = ex.Message
                };
            }
        }

        private async Task<TransactionSuggestion> ParseTransactionSuggestionAsync(McpResponse response, TransactionContext context)
        {
            // Parse AI response into structured transaction suggestion
            // This would typically use JSON parsing of the AI response
            
            var suggestion = new TransactionSuggestion
            {
                Confidence = response.Confidence,
                RecommendedItems = new List<SuggestedItem>(),
                EstimatedTotal = 0.0m,
                Warnings = response.Warnings
            };

            // TODO: Implement actual parsing based on AI response format
            // For now, return empty suggestion
            
            return suggestion;
        }

        private async Task<FraudRiskAssessment> ParseFraudRiskAssessmentAsync(McpResponse response)
        {
            // Parse AI response into fraud risk assessment
            return new FraudRiskAssessment
            {
                RiskLevel = RiskLevel.Low, // TODO: Parse from response
                Confidence = response.Confidence,
                Reason = response.Content,
                RecommendedAction = "Continue processing"
            };
        }

        private async Task<IEnumerable<UpsellSuggestion>> ParseUpsellSuggestionsAsync(McpResponse response)
        {
            // TODO: Parse actual upsell suggestions from AI response
            return Enumerable.Empty<UpsellSuggestion>();
        }

        private async Task<IEnumerable<InventoryInsight>> ParseInventoryInsightsAsync(McpResponse response)
        {
            // TODO: Parse inventory insights from AI response
            return Enumerable.Empty<InventoryInsight>();
        }

        private async Task<QueryResult> ParseQueryResultAsync(McpResponse response)
        {
            return new QueryResult
            {
                Success = true,
                Answer = response.Content,
                Confidence = response.Confidence,
                Sources = response.Metadata.ContainsKey("sources") 
                    ? response.Metadata["sources"].ToString() 
                    : "AI Analysis"
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _mcpClient?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Provides contextual information for AI processing.
    /// </summary>
    public interface ITransactionContextProvider
    {
        Task<McpContext> BuildAiContextAsync(TransactionContext context);
        Task<object> BuildAnalysisContextAsync(Transaction transaction);
        Task<object> BuildUpsellContextAsync(Transaction transaction);
        Task<object> BuildInventoryContextAsync(Transaction transaction);
        Task<McpContext> BuildQueryContextAsync(BusinessContext businessContext);
    }

    /// <summary>
    /// Generates AI prompts for various POS scenarios.
    /// </summary>
    public interface IAiPromptEngine
    {
        Task<string> CreateTransactionPromptAsync(string userInput, TransactionContext context);
        Task<string> CreateQueryPromptAsync(string query, BusinessContext businessContext);
    }

    /// <summary>
    /// Transaction context for AI processing.
    /// </summary>
    public class TransactionContext
    {
        public string StoreId { get; set; } = "";
        public string TerminalId { get; set; } = "";
        public string Currency { get; set; } = "USD";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public List<string> ProductCategories { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Business context for queries and analysis.
    /// </summary>
    public class BusinessContext
    {
        public string StoreId { get; set; } = "";
        public DateRange QueryPeriod { get; set; } = new();
        public List<string> AvailableMetrics { get; set; } = new();
        public Dictionary<string, object> Constraints { get; set; } = new();
    }

    /// <summary>
    /// AI-generated transaction suggestion.
    /// </summary>
    public class TransactionSuggestion
    {
        public List<SuggestedItem> RecommendedItems { get; set; } = new();
        public decimal EstimatedTotal { get; set; }
        public double Confidence { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Suggested item for transaction.
    /// </summary>
    public class SuggestedItem
    {
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Fraud risk assessment result.
    /// </summary>
    public class FraudRiskAssessment
    {
        public RiskLevel RiskLevel { get; set; }
        public double Confidence { get; set; }
        public string Reason { get; set; } = "";
        public string RecommendedAction { get; set; } = "";
    }

    /// <summary>
    /// Risk levels for transactions.
    /// </summary>
    public enum RiskLevel
    {
        Unknown,
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Upselling suggestion.
    /// </summary>
    public class UpsellSuggestion
    {
        public string ProductId { get; set; } = "";
        public string ProductName { get; set; } = "";
        public decimal Price { get; set; }
        public string Reason { get; set; } = "";
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Inventory insight from AI analysis.
    /// </summary>
    public class InventoryInsight
    {
        public string ProductId { get; set; } = "";
        public string InsightType { get; set; } = "";
        public string Message { get; set; } = "";
        public int Priority { get; set; }
    }

    /// <summary>
    /// Result of natural language query processing.
    /// </summary>
    public class QueryResult
    {
        public bool Success { get; set; }
        public string Answer { get; set; } = "";
        public double Confidence { get; set; }
        public string Sources { get; set; } = "";
        public string? Error { get; set; }
    }

    /// <summary>
    /// Date range for business queries.
    /// </summary>
    public class DateRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    /// <summary>
    /// Transaction wrapper for AI processing.
    /// </summary>
    public class Transaction
    {
        public string Id { get; set; } = "";
        public List<TransactionLine> Lines { get; set; } = new();
        public decimal Total { get; set; }
        public string Currency { get; set; } = "USD";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Transaction line item.
    /// </summary>
    public class TransactionLine
    {
        public string ProductId { get; set; } = "";
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }

    /// <summary>
    /// Exception for AI processing errors.
    /// </summary>
    public class AiProcessingException : Exception
    {
        public AiProcessingException(string message) : base(message) { }
        public AiProcessingException(string message, Exception innerException) : base(message, innerException) { }
    }
}
