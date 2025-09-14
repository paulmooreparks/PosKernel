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
        /// <summary>
        /// Gets or sets the store identifier.
        /// </summary>
        public string StoreId { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the terminal identifier.
        /// </summary>
        public string TerminalId { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the currency code.
        /// </summary>
        public string Currency { get; set; } = "USD";
        
        /// <summary>
        /// Gets or sets the timestamp of the transaction context.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets the list of product categories available.
        /// </summary>
        public List<string> ProductCategories { get; set; } = new();
        
        /// <summary>
        /// Gets or sets additional metadata for the transaction context.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Business context for queries and analysis.
    /// </summary>
    public class BusinessContext
    {
        /// <summary>
        /// Gets or sets the store identifier for the business context.
        /// </summary>
        public string StoreId { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the date range for the query period.
        /// </summary>
        public DateRange QueryPeriod { get; set; } = new();
        
        /// <summary>
        /// Gets or sets the list of available metrics for analysis.
        /// </summary>
        public List<string> AvailableMetrics { get; set; } = new();
        
        /// <summary>
        /// Gets or sets the constraints for the business query.
        /// </summary>
        public Dictionary<string, object> Constraints { get; set; } = new();
    }

    /// <summary>
    /// AI-generated transaction suggestion.
    /// </summary>
    public class TransactionSuggestion
    {
        /// <summary>
        /// Gets or sets the list of recommended items for the transaction.
        /// </summary>
        public List<SuggestedItem> RecommendedItems { get; set; } = new();
        
        /// <summary>
        /// Gets or sets the estimated total for the suggested transaction.
        /// </summary>
        public decimal EstimatedTotal { get; set; }
        
        /// <summary>
        /// Gets or sets the confidence level of the AI suggestion (0-1).
        /// </summary>
        public double Confidence { get; set; }
        
        /// <summary>
        /// Gets or sets any warnings about the transaction suggestion.
        /// </summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Suggested item for transaction.
    /// </summary>
    public class SuggestedItem
    {
        /// <summary>
        /// Gets or sets the SKU (stock keeping unit) of the suggested item.
        /// </summary>
        public string Sku { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the name of the suggested item.
        /// </summary>
        public string Name { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the suggested quantity.
        /// </summary>
        public int Quantity { get; set; }
        
        /// <summary>
        /// Gets or sets the price of the suggested item.
        /// </summary>
        public decimal Price { get; set; }
        
        /// <summary>
        /// Gets or sets the confidence level for this specific item suggestion.
        /// </summary>
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Fraud risk assessment result.
    /// </summary>
    public class FraudRiskAssessment
    {
        /// <summary>
        /// Gets or sets the assessed risk level for the transaction.
        /// </summary>
        public RiskLevel RiskLevel { get; set; }
        
        /// <summary>
        /// Gets or sets the confidence level of the risk assessment.
        /// </summary>
        public double Confidence { get; set; }
        
        /// <summary>
        /// Gets or sets the reason for the risk assessment.
        /// </summary>
        public string Reason { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the recommended action based on the risk assessment.
        /// </summary>
        public string RecommendedAction { get; set; } = "";
    }

    /// <summary>
    /// Risk levels for transactions.
    /// </summary>
    public enum RiskLevel
    {
        /// <summary>
        /// Risk level cannot be determined.
        /// </summary>
        Unknown,
        
        /// <summary>
        /// Low risk transaction.
        /// </summary>
        Low,
        
        /// <summary>
        /// Medium risk transaction requiring attention.
        /// </summary>
        Medium,
        
        /// <summary>
        /// High risk transaction requiring approval.
        /// </summary>
        High,
        
        /// <summary>
        /// Critical risk transaction that should be blocked.
        /// </summary>
        Critical
    }

    /// <summary>
    /// Upselling suggestion.
    /// </summary>
    public class UpsellSuggestion
    {
        /// <summary>
        /// Gets or sets the product identifier for the upsell item.
        /// </summary>
        public string ProductId { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the name of the upsell product.
        /// </summary>
        public string ProductName { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the price of the upsell product.
        /// </summary>
        public decimal Price { get; set; }
        
        /// <summary>
        /// Gets or sets the reason for the upsell suggestion.
        /// </summary>
        public string Reason { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the confidence level of the upsell suggestion.
        /// </summary>
        public double Confidence { get; set; }
    }

    /// <summary>
    /// Inventory insight from AI analysis.
    /// </summary>
    public class InventoryInsight
    {
        /// <summary>
        /// Gets or sets the product identifier for the inventory insight.
        /// </summary>
        public string ProductId { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the type of inventory insight.
        /// </summary>
        public string InsightType { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the descriptive message about the insight.
        /// </summary>
        public string Message { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the priority level of the insight (1-10).
        /// </summary>
        public int Priority { get; set; }
    }

    /// <summary>
    /// Result of natural language query processing.
    /// </summary>
    public class QueryResult
    {
        /// <summary>
        /// Gets or sets whether the query processing was successful.
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Gets or sets the answer to the natural language query.
        /// </summary>
        public string Answer { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the confidence level of the answer.
        /// </summary>
        public double Confidence { get; set; }
        
        /// <summary>
        /// Gets or sets the sources used to generate the answer.
        /// </summary>
        public string Sources { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the error message if query processing failed.
        /// </summary>
        public string? Error { get; set; }
    }

    /// <summary>
    /// Date range for business queries.
    /// </summary>
    public class DateRange
    {
        /// <summary>
        /// Gets or sets the start date of the range.
        /// </summary>
        public DateTime Start { get; set; }
        
        /// <summary>
        /// Gets or sets the end date of the range.
        /// </summary>
        public DateTime End { get; set; }
    }

    /// <summary>
    /// Transaction wrapper for AI processing.
    /// </summary>
    public class Transaction
    {
        /// <summary>
        /// Gets or sets the unique transaction identifier.
        /// </summary>
        public string Id { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the list of transaction line items.
        /// </summary>
        public List<TransactionLine> Lines { get; set; } = new();
        
        /// <summary>
        /// Gets or sets the total amount of the transaction.
        /// </summary>
        public decimal Total { get; set; }
        
        /// <summary>
        /// Gets or sets the currency code for the transaction.
        /// </summary>
        public string Currency { get; set; } = "USD";
        
        /// <summary>
        /// Gets or sets the timestamp when the transaction occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Transaction line item.
    /// </summary>
    public class TransactionLine
    {
        /// <summary>
        /// Gets or sets the product identifier.
        /// </summary>
        public string ProductId { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string ProductName { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the quantity of the product.
        /// </summary>
        public int Quantity { get; set; }
        
        /// <summary>
        /// Gets or sets the unit price of the product.
        /// </summary>
        public decimal UnitPrice { get; set; }
        
        /// <summary>
        /// Gets or sets the total price for this line item.
        /// </summary>
        public decimal Total { get; set; }
    }

    /// <summary>
    /// Exception for AI processing errors.
    /// </summary>
    public class AiProcessingException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AiProcessingException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public AiProcessingException(string message) : base(message) { }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="AiProcessingException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public AiProcessingException(string message, Exception innerException) : base(message, innerException) { }
    }
}
