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
using System.Linq;
using System.Threading.Tasks;
using PosKernel.AI.Core;
using PosKernel.AI.Services;
using PosKernel.Host;

// Resolve ambiguity by using aliases
using AiTransaction = PosKernel.AI.Services.Transaction;
using PosTransaction = PosKernel.Host.Transaction;

namespace PosKernel.AI.Examples
{
    /// <summary>
    /// Demonstration of AI-enhanced POS operations showing natural language processing,
    /// fraud detection, and intelligent assistance capabilities.
    /// </summary>
    public class AiPosDemo
    {
        private readonly PosAiService _aiService;
        private readonly McpClient _mcpClient;

        public AiPosDemo(PosAiService aiService, McpClient mcpClient)
        {
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _mcpClient = mcpClient ?? throw new ArgumentNullException(nameof(mcpClient));
        }

        /// <summary>
        /// Demonstrates natural language transaction processing.
        /// </summary>
        public async Task DemonstrateNaturalLanguageTransactionAsync()
        {
            Console.WriteLine("=== AI-Enhanced Natural Language Transaction Processing ===");
            Console.WriteLine();

            try
            {
                // Simulate natural language input
                var userInputs = new[]
                {
                    "I need two coffees and a muffin",
                    "Add three sandwiches and two bottles of water", 
                    "I want the lunch special for four people",
                    "Can I get a large coffee with extra milk and sugar?"
                };

                var context = new TransactionContext
                {
                    StoreId = "STORE_001",
                    TerminalId = "REGISTER_01",
                    Currency = "USD",
                    ProductCategories = new() { "beverage", "food", "snack" }
                };

                foreach (var input in userInputs)
                {
                    Console.WriteLine($"Customer says: '{input}'");
                    
                    try
                    {
                        // Process natural language with AI
                        var suggestion = await _aiService.ProcessNaturalLanguageAsync(input, context);
                        
                        Console.WriteLine($"AI Confidence: {suggestion.Confidence:P1}");
                        Console.WriteLine($"Suggested items: {suggestion.RecommendedItems.Count}");
                        Console.WriteLine($"Estimated total: ${suggestion.EstimatedTotal:F2}");
                        
                        if (suggestion.Warnings.Any())
                        {
                            Console.WriteLine($"Warnings: {string.Join(", ", suggestion.Warnings)}");
                        }

                        // Demonstrate actual transaction creation (if confidence is high enough)
                        if (suggestion.Confidence > 0.8)
                        {
                            Console.WriteLine("  🤖 High confidence - would create actual transaction");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ AI processing failed: {ex.Message}");
                    }
                    
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Demo failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Demonstrates real-time fraud detection capabilities.
        /// </summary>
        public async Task DemonstrateFraudDetectionAsync()
        {
            Console.WriteLine("=== AI-Powered Fraud Detection ===");
            Console.WriteLine();

            try
            {
                // Create sample transactions with varying risk profiles
                var transactions = new[]
                {
                    new AiTransaction // Normal transaction
                    {
                        Id = "TXN_001",
                        Lines = new()
                        {
                            new() { ProductId = "COFFEE", ProductName = "Coffee", Quantity = 1, UnitPrice = 3.99m },
                            new() { ProductId = "MUFFIN", ProductName = "Muffin", Quantity = 1, UnitPrice = 2.49m }
                        },
                        Total = 6.48m
                    },
                    
                    new AiTransaction // High-value transaction
                    {
                        Id = "TXN_002", 
                        Lines = new()
                        {
                            new() { ProductId = "LAPTOP", ProductName = "Laptop Computer", Quantity = 5, UnitPrice = 1299.99m },
                            new() { ProductId = "PHONE", ProductName = "Mobile Phone", Quantity = 10, UnitPrice = 899.99m }
                        },
                        Total = 15499.85m
                    },
                    
                    new AiTransaction // Unusual pattern
                    {
                        Id = "TXN_003",
                        Lines = new()
                        {
                            new() { ProductId = "GIFTCARD", ProductName = "Gift Card", Quantity = 20, UnitPrice = 100.00m }
                        },
                        Total = 2000.00m
                    }
                };

                foreach (var transaction in transactions)
                {
                    Console.WriteLine($"Analyzing transaction {transaction.Id} (${transaction.Total:F2}):");
                    
                    try
                    {
                        // Analyze transaction with AI
                        var riskAssessment = await _aiService.AnalyzeTransactionRiskAsync(transaction);
                        
                        Console.WriteLine($"  Risk Level: {riskAssessment.RiskLevel}");
                        Console.WriteLine($"  Confidence: {riskAssessment.Confidence:P1}");
                        Console.WriteLine($"  Reason: {riskAssessment.Reason}");
                        Console.WriteLine($"  Recommended Action: {riskAssessment.RecommendedAction}");
                        
                        // Take action based on risk level
                        switch (riskAssessment.RiskLevel)
                        {
                            case RiskLevel.High or RiskLevel.Critical:
                                Console.WriteLine("  ⚠️  ALERT: Transaction requires manager approval");
                                break;
                            case RiskLevel.Medium:
                                Console.WriteLine("  ⚡ INFO: Additional verification recommended");
                                break;
                            case RiskLevel.Low:
                                Console.WriteLine("  ✅ OK: Transaction appears normal");
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ❌ Risk analysis failed: {ex.Message}");
                    }
                    
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fraud detection demo failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Demonstrates AI-powered business intelligence queries.
        /// </summary>
        public async Task DemonstrateBusinessIntelligenceAsync()
        {
            Console.WriteLine("=== AI Business Intelligence Queries ===");
            Console.WriteLine();

            try
            {
                var businessContext = new BusinessContext
                {
                    StoreId = "STORE_001",
                    QueryPeriod = new() { Start = DateTime.Today.AddDays(-30), End = DateTime.Today },
                    AvailableMetrics = new() { "sales", "transactions", "customers", "inventory" }
                };

                var queries = new[]
                {
                    "What were our top-selling items yesterday?",
                    "How did our sales compare to last month?", 
                    "Which products should we reorder?",
                    "What time of day are we busiest?",
                    "Are there any unusual sales patterns I should know about?"
                };

                foreach (var query in queries)
                {
                    Console.WriteLine($"Business Query: '{query}'");
                    
                    try
                    {
                        var result = await _aiService.ProcessNaturalLanguageQueryAsync(query, businessContext);
                        
                        if (result.Success)
                        {
                            Console.WriteLine($"  Answer: {result.Answer}");
                            Console.WriteLine($"  Confidence: {result.Confidence:P1}");
                            Console.WriteLine($"  Sources: {result.Sources}");
                        }
                        else
                        {
                            Console.WriteLine($"  ❌ Query failed: {result.Error}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ❌ Query processing failed: {ex.Message}");
                    }
                    
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Business intelligence demo failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Demonstrates AI-powered upselling and recommendations.
        /// </summary>
        public async Task DemonstrateIntelligentRecommendationsAsync()
        {
            Console.WriteLine("=== AI-Powered Upselling and Recommendations ===");
            Console.WriteLine();

            try
            {
                // Create a sample transaction in progress
                var currentTransaction = new AiTransaction
                {
                    Id = "TXN_CURRENT",
                    Lines = new()
                    {
                        new() { ProductId = "COFFEE", ProductName = "Large Coffee", Quantity = 1, UnitPrice = 4.99m }
                    },
                    Total = 4.99m
                };

                Console.WriteLine("Current transaction:");
                Console.WriteLine($"  {currentTransaction.Lines[0].ProductName} - ${currentTransaction.Lines[0].UnitPrice:F2}");
                Console.WriteLine();

                // Get AI recommendations
                try
                {
                    var upsellSuggestions = await _aiService.GetUpsellSuggestionsAsync(currentTransaction);
                    
                    Console.WriteLine("AI Recommendations:");
                    foreach (var suggestion in upsellSuggestions)
                    {
                        Console.WriteLine($"  • {suggestion.ProductName} - ${suggestion.Price:F2}");
                        Console.WriteLine($"    Reason: {suggestion.Reason}");
                        Console.WriteLine($"    Confidence: {suggestion.Confidence:P1}");
                    }
                    
                    if (!upsellSuggestions.Any())
                    {
                        Console.WriteLine("  No recommendations available at this time.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ Recommendation engine failed: {ex.Message}");
                }

                Console.WriteLine();

                // Get inventory insights
                try
                {
                    var inventoryInsights = await _aiService.CheckInventoryImpactAsync(currentTransaction);
                    
                    Console.WriteLine("Inventory Insights:");
                    foreach (var insight in inventoryInsights)
                    {
                        Console.WriteLine($"  • {insight.InsightType}: {insight.Message}");
                        Console.WriteLine($"    Priority: {insight.Priority}/10");
                    }
                    
                    if (!inventoryInsights.Any())
                    {
                        Console.WriteLine("  No inventory concerns detected.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ Inventory analysis failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Recommendations demo failed: {ex.Message}");
            }
        }
    }
}
