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

using Microsoft.Extensions.Logging;
using PosKernel.AI.Services;

namespace PosKernel.AI.Examples
{
    /// <summary>
    /// Basic AI-POS integration demo showing natural language processing
    /// and intelligent transaction assistance.
    /// </summary>
    public class AiPosDemo
    {
        private readonly PosAiService _aiService;
        private readonly ILogger<AiPosDemo> _logger;

        public AiPosDemo(PosAiService aiService, ILogger<AiPosDemo> logger)
        {
            _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Runs the basic AI-POS demonstration.
        /// </summary>
        public async Task RunDemoAsync()
        {
            Console.WriteLine("🤖 AI-POS Integration Demo");
            Console.WriteLine("==========================");

            // Natural language processing demo
            await DemonstrateNaturalLanguageProcessing();

            // Transaction risk analysis demo
            await DemonstrateRiskAnalysis();

            // Upselling suggestions demo
            await DemonstrateUpsellSuggestions();

            Console.WriteLine("\n✅ Demo complete!");
        }

        private async Task DemonstrateNaturalLanguageProcessing()
        {
            Console.WriteLine("\n🗣️  Natural Language Processing Demo");
            Console.WriteLine("------------------------------------");

            var queries = new[]
            {
                "Add a large coffee to the transaction",
                "What's our best-selling item today?",
                "Show me yesterday's sales totals"
            };

            foreach (var query in queries)
            {
                Console.WriteLine($"\nUser: \"{query}\"");
                
                var response = await _aiService.ProcessNaturalLanguageAsync(query);
                
                if (response.IsSuccessful)
                {
                    Console.WriteLine($"🤖 AI: {response.ResponseText} (Confidence: {response.Confidence:P0})");
                }
                else
                {
                    Console.WriteLine($"❌ Error: {response.ErrorMessage}");
                }
            }
        }

        private async Task DemonstrateRiskAnalysis()
        {
            Console.WriteLine("\n🚨 Transaction Risk Analysis Demo");
            Console.WriteLine("---------------------------------");

            // Create a sample transaction
            var transaction = new SimpleTransaction
            {
                Id = "TXN_001",
                Lines = new List<SimpleTransactionLine>
                {
                    new() { ProductId = "COFFEE_LG", ProductName = "Large Coffee", Quantity = 1, UnitPrice = 3.99m, Total = 3.99m }
                },
                Total = 3.99m,
                Currency = "USD"
            };

            var riskAssessment = await _aiService.AnalyzeTransactionRiskAsync(transaction);

            Console.WriteLine($"Transaction: {transaction.Id} - ${transaction.Total:F2}");
            Console.WriteLine($"Risk Level: {riskAssessment.RiskLevel}");
            Console.WriteLine($"Confidence: {riskAssessment.Confidence:P0}");
            Console.WriteLine($"Reason: {riskAssessment.Reason}");
            Console.WriteLine($"Action: {riskAssessment.RecommendedAction}");
        }

        private async Task DemonstrateUpsellSuggestions()
        {
            Console.WriteLine("\n💰 Upselling Suggestions Demo");
            Console.WriteLine("-----------------------------");

            // Create a sample transaction
            var transaction = new SimpleTransaction
            {
                Id = "TXN_002",
                Lines = new List<SimpleTransactionLine>
                {
                    new() { ProductId = "COFFEE_SM", ProductName = "Small Coffee", Quantity = 1, UnitPrice = 2.99m, Total = 2.99m }
                },
                Total = 2.99m,
                Currency = "USD"
            };

            var suggestions = await _aiService.GetUpsellSuggestionsAsync(transaction);

            Console.WriteLine($"Current Transaction: {transaction.Total:C}");
            Console.WriteLine("🤖 AI Suggestions:");

            foreach (var suggestion in suggestions)
            {
                Console.WriteLine($"  • {suggestion.ProductName} - {suggestion.Price:C}");
                Console.WriteLine($"    Reason: {suggestion.Reason} (Confidence: {suggestion.Confidence:P0})");
            }
        }
    }
}
