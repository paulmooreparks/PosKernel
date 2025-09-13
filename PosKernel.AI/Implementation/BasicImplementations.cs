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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PosKernel.AI.Core;
using PosKernel.AI.Services;

namespace PosKernel.AI.Implementation
{
    /// <summary>
    /// Basic implementation of prompt sanitization for AI security.
    /// </summary>
    public class BasicPromptSanitizer : IPromptSanitizer
    {
        private readonly List<Regex> _sanitizationPatterns;

        public BasicPromptSanitizer()
        {
            _sanitizationPatterns = new List<Regex>
            {
                // Remove potential injection patterns
                new Regex(@"ignore\s+previous\s+instructions", RegexOptions.IgnoreCase),
                new Regex(@"system\s+prompt", RegexOptions.IgnoreCase),
                new Regex(@"jailbreak", RegexOptions.IgnoreCase),
                
                // Remove sensitive data patterns  
                new Regex(@"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b"), // Credit cards
                new Regex(@"\b\d{3}-\d{2}-\d{4}\b"), // SSN
                
                // Remove excessive special characters
                new Regex(@"[<>""']{3,}")
            };
        }

        public async Task<string> SanitizeAsync(string prompt)
        {
            if (string.IsNullOrEmpty(prompt))
                return string.Empty;

            var sanitized = prompt;

            // Apply sanitization patterns
            foreach (var pattern in _sanitizationPatterns)
            {
                sanitized = pattern.Replace(sanitized, "[REDACTED]");
            }

            // Limit length to prevent abuse
            if (sanitized.Length > 2000)
            {
                sanitized = sanitized.Substring(0, 2000) + "...";
            }

            // Trim whitespace
            sanitized = sanitized.Trim();

            return sanitized;
        }
    }

    /// <summary>
    /// Basic implementation of response validation for AI security.
    /// </summary>
    public class BasicResponseValidator : IResponseValidator
    {
        public async Task<bool> ValidateAsync(McpResponse response)
        {
            if (response == null)
                return false;

            // Check for reasonable response length
            if (string.IsNullOrEmpty(response.Content) || response.Content.Length > 10000)
                return false;

            // Check confidence level
            if (response.Confidence < 0 || response.Confidence > 1)
                return false;

            // Check for suspicious content patterns
            var suspiciousPatterns = new[]
            {
                "credit card", "password", "token", "api key",
                "social security", "ssn", "personal information"
            };

            var lowerContent = response.Content.ToLowerInvariant();
            if (suspiciousPatterns.Any(pattern => lowerContent.Contains(pattern)))
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Console-based audit logger for development and testing.
    /// </summary>
    public class ConsoleAuditLogger : IAuditLogger
    {
        public async Task LogAiInteractionAsync(AiAuditEvent auditEvent)
        {
            Console.WriteLine($"[AI AUDIT] {auditEvent.Timestamp:HH:mm:ss} - {auditEvent.RequestType}");
            Console.WriteLine($"           Success: {auditEvent.Success}, Security: {auditEvent.SecurityLevel}");
            
            if (!string.IsNullOrEmpty(auditEvent.Error))
            {
                Console.WriteLine($"           Error: {auditEvent.Error}");
            }

            if (auditEvent.ComplianceFlags.Any())
            {
                Console.WriteLine($"           Compliance: {string.Join(", ", auditEvent.ComplianceFlags)}");
            }
        }

        public async Task LogSecurityEventAsync(SecurityAuditEvent securityEvent)
        {
            Console.WriteLine($"[SECURITY] {securityEvent.Timestamp:HH:mm:ss} - {securityEvent.EventType}");
            Console.WriteLine($"           Severity: {securityEvent.Severity}");
            
            if (!string.IsNullOrEmpty(securityEvent.Pattern))
            {
                Console.WriteLine($"           Pattern: {securityEvent.Pattern}");
            }
            
            if (!string.IsNullOrEmpty(securityEvent.Details))
            {
                Console.WriteLine($"           Details: {securityEvent.Details}");
            }
        }
    }

    /// <summary>
    /// Basic implementation of transaction context provider.
    /// </summary>
    public class BasicTransactionContextProvider : ITransactionContextProvider
    {
        public async Task<McpContext> BuildAiContextAsync(TransactionContext context)
        {
            return new McpContext
            {
                SessionId = Guid.NewGuid().ToString(),
                StoreId = context.StoreId,
                BusinessContext = new Dictionary<string, object>
                {
                    ["currency"] = context.Currency,
                    ["timestamp"] = context.Timestamp,
                    ["categories"] = context.ProductCategories
                },
                SecurityTags = new() { "transaction_processing", "filtered" }
            };
        }

        public async Task<object> BuildAnalysisContextAsync(Transaction transaction)
        {
            return new
            {
                transaction_id = transaction.Id,
                total_amount = transaction.Total,
                currency = transaction.Currency,
                line_count = transaction.Lines.Count,
                timestamp = transaction.Timestamp,
                
                // Aggregate data only - no specific product details for privacy
                categories = transaction.Lines.Select(l => GetProductCategory(l.ProductId)).Distinct(),
                quantity_distribution = transaction.Lines.GroupBy(l => l.Quantity).Select(g => new { quantity = g.Key, count = g.Count() }),
                price_ranges = transaction.Lines.Select(l => GetPriceRange(l.UnitPrice))
            };
        }

        public async Task<object> BuildUpsellContextAsync(Transaction transaction)
        {
            return new
            {
                current_items = transaction.Lines.Select(l => new { 
                    category = GetProductCategory(l.ProductId),
                    price_range = GetPriceRange(l.UnitPrice)
                }),
                total_value = transaction.Total,
                transaction_time = transaction.Timestamp.Hour // For time-based recommendations
            };
        }

        public async Task<object> BuildInventoryContextAsync(Transaction transaction)
        {
            return new
            {
                requested_items = transaction.Lines.Select(l => new {
                    product_category = GetProductCategory(l.ProductId),
                    quantity = l.Quantity
                })
            };
        }

        public async Task<McpContext> BuildQueryContextAsync(BusinessContext businessContext)
        {
            return new McpContext
            {
                StoreId = businessContext.StoreId,
                BusinessContext = new Dictionary<string, object>
                {
                    ["query_period"] = businessContext.QueryPeriod,
                    ["available_metrics"] = businessContext.AvailableMetrics,
                    ["constraints"] = businessContext.Constraints
                },
                SecurityTags = new() { "business_intelligence", "aggregated_data" }
            };
        }

        private string GetProductCategory(string productId)
        {
            // Simple category mapping for demo
            return productId.ToUpperInvariant() switch
            {
                string id when id.Contains("COFFEE") => "beverage",
                string id when id.Contains("MUFFIN") => "food", 
                string id when id.Contains("SANDWICH") => "food",
                string id when id.Contains("WATER") => "beverage",
                string id when id.Contains("LAPTOP") => "electronics",
                string id when id.Contains("PHONE") => "electronics",
                string id when id.Contains("GIFTCARD") => "service",
                _ => "other"
            };
        }

        private string GetPriceRange(decimal price)
        {
            return price switch
            {
                <= 5.00m => "low",
                <= 20.00m => "medium", 
                <= 100.00m => "high",
                _ => "premium"
            };
        }
    }

    /// <summary>
    /// Basic implementation of AI prompt engineering.
    /// </summary>
    public class BasicAiPromptEngine : IAiPromptEngine
    {
        public async Task<string> CreateTransactionPromptAsync(string userInput, TransactionContext context)
        {
            return $@"You are a POS system assistant. Parse this customer request into structured transaction items.

Customer request: ""{userInput}""

Store context:
- Store ID: {context.StoreId}
- Currency: {context.Currency}
- Available categories: {string.Join(", ", context.ProductCategories)}
- Current time: {context.Timestamp:yyyy-MM-dd HH:mm}

Please respond with a structured JSON format containing:
{{
  ""items"": [
    {{
      ""sku"": ""product_sku"",
      ""name"": ""product_name"", 
      ""quantity"": 1,
      ""estimated_price"": 0.00,
      ""confidence"": 0.85
    }}
  ],
  ""total_confidence"": 0.85,
  ""warnings"": [""any warnings or clarifications needed""]
}}

Rules:
- Only suggest items that make sense for a retail/cafe environment
- Be conservative with quantities unless explicitly specified
- Include confidence scores (0-1) for each item
- Add warnings if the request is ambiguous";
        }

        public async Task<string> CreateQueryPromptAsync(string query, BusinessContext businessContext)
        {
            return $@"You are a business intelligence assistant for a POS system. Answer this business query with insights and recommendations.

Query: ""{query}""

Business context:
- Store ID: {businessContext.StoreId}
- Query period: {businessContext.QueryPeriod.Start:yyyy-MM-dd} to {businessContext.QueryPeriod.End:yyyy-MM-dd}
- Available metrics: {string.Join(", ", businessContext.AvailableMetrics)}

Please provide:
1. A clear, actionable answer
2. Supporting data or reasoning
3. Confidence level (0-1)
4. Recommendations for next steps

Keep responses concise but informative, suitable for busy retail managers.";
        }
    }
}
