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

namespace PosKernel.AI.Core
{
    /// <summary>
    /// Analyzes customer input to determine ordering state and payment readiness.
    /// Uses purely structural analysis - no cultural, language, or business assumptions.
    /// ARCHITECTURAL PRINCIPLE: Culture-neutral transaction analysis only.
    /// </summary>
    public class OrderCompletionAnalyzer
    {
        private readonly ThoughtLogger _thoughtLogger;

        public OrderCompletionAnalyzer(ThoughtLogger thoughtLogger)
        {
            _thoughtLogger = thoughtLogger ?? throw new ArgumentNullException(nameof(thoughtLogger));
        }

        /// <summary>
        /// Analyzes customer input for ordering intentions using structural patterns only.
        /// ARCHITECTURAL PRINCIPLE: No hardcoded language, currency, or cultural assumptions.
        /// </summary>
        public OrderIntentAnalysis AnalyzeOrderIntent(string userInput, Receipt currentReceipt, List<ChatMessage> conversationHistory)
        {
            var analysis = new OrderIntentAnalysis
            {
                OriginalInput = userInput,
                CurrentReceiptStatus = currentReceipt.Status,
                ItemCount = currentReceipt.Items.Count,
                ConversationLength = conversationHistory.Count
            };

            // ARCHITECTURAL PRINCIPLE: Structural analysis only - no semantic assumptions
            AnalyzeStructuralPatterns(userInput, analysis);
            AnalyzeConversationFlow(conversationHistory, analysis);
            AnalyzeContextualTiming(currentReceipt, conversationHistory, analysis);
            
            DetermineFinalIntent(analysis);
            
            return analysis;
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Purely structural analysis - no language assumptions.
        /// </summary>
        private void AnalyzeStructuralPatterns(string userInput, OrderIntentAnalysis analysis)
        {
            _thoughtLogger.LogStep(1, "Analyzing structural patterns");
            
            var input = userInput.Trim();
            var wordCount = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var hasNumbers = input.Any(char.IsDigit);
            var hasQuestionMark = input.EndsWith('?');
            var isVeryShort = wordCount <= 2;
            
            _thoughtLogger.LogThought($"Structural analysis - Length: {input.Length}, Words: {wordCount}, HasNumbers: {hasNumbers}, HasQuestion: {hasQuestionMark}");
            
            // ARCHITECTURAL PRINCIPLE: Universal structural patterns only
            if (isVeryShort && !hasNumbers)
            {
                analysis.StructuralIndicators.Add("Very short response - structurally ambiguous");
                analysis.CompletionLikelihood += 0.2; // Reduced - let AI determine semantics
            }
            
            if (hasNumbers)
            {
                analysis.StructuralIndicators.Add("Contains numbers - likely quantity specification");
                analysis.NewOrderLikelihood += 0.4; // Reduced - let AI handle context
            }
            
            if (hasQuestionMark)
            {
                analysis.StructuralIndicators.Add("Question format - inquiry detected");
                analysis.QuestionLikelihood = 0.6; // Reduced - let AI interpret meaning
            }
            
            if (wordCount >= 3)
            {
                analysis.StructuralIndicators.Add("Multi-word input - complex intent likely");
                analysis.NewOrderLikelihood += 0.3;
            }
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Conversation flow analysis without semantic assumptions.
        /// </summary>
        private void AnalyzeConversationFlow(List<ChatMessage> conversationHistory, OrderIntentAnalysis analysis)
        {
            _thoughtLogger.LogStep(2, "Analyzing conversation flow patterns");
            
            var recentMessages = conversationHistory.TakeLast(3).ToList();
            var lastSystemMessage = recentMessages.LastOrDefault(m => m.IsSystem || !m.Sender.Equals("Customer", StringComparison.OrdinalIgnoreCase));
            
            if (lastSystemMessage != null)
            {
                var systemContent = lastSystemMessage.Content;
                
                // ARCHITECTURAL PRINCIPLE: Universal structural patterns only - no language-specific terms
                if (systemContent.Contains("?"))
                {
                    analysis.RecentClarificationContext = true;
                    _thoughtLogger.LogThought("Recent system message was question format");
                }
                
                // ARCHITECTURAL PRINCIPLE: Look for structural completion indicators only
                if (IsRecentMessage(lastSystemMessage, TimeSpan.FromMinutes(2)) && 
                    ContainsStructuralCompletionIndicators(systemContent))
                {
                    analysis.RecentCompletionContext = true;
                    analysis.CompletionLikelihood += 0.2; // Reduced - let AI determine meaning
                    _thoughtLogger.LogThought("Recent system message has structural completion indicators");
                }
            }
            
            // Pattern: Message timing analysis (universal)
            if (conversationHistory.Count >= 2)
            {
                var lastTwoCustomerMessages = conversationHistory
                    .Where(m => m.Sender.Equals("Customer", StringComparison.OrdinalIgnoreCase))
                    .TakeLast(2)
                    .ToList();
                
                if (lastTwoCustomerMessages.Count == 2)
                {
                    var timeBetween = lastTwoCustomerMessages[1].Timestamp - lastTwoCustomerMessages[0].Timestamp;
                    if (timeBetween < TimeSpan.FromSeconds(10))
                    {
                        analysis.RapidResponsePattern = true;
                        analysis.NewOrderLikelihood += 0.1; // Reduced - structural hint only
                        _thoughtLogger.LogThought($"Rapid response pattern detected ({timeBetween.TotalSeconds:F1}s)");
                    }
                }
            }
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Universal structural indicators only - no currency or language assumptions.
        /// </summary>
        private bool ContainsStructuralCompletionIndicators(string content)
        {
            // ARCHITECTURAL FIX: Remove all hardcoded language terms
            // Look for universal structural patterns only
            var structuralIndicators = new[]
            {
                "x", "=", ":", "-" // Mathematical/listing symbols that suggest itemization
            };
            
            // ARCHITECTURAL PRINCIPLE: Universal number patterns (not currency-specific)
            var hasNumberPattern = System.Text.RegularExpressions.Regex.IsMatch(content, @"\d+\.\d+"); // Any decimal pattern
            var hasListingStructure = structuralIndicators.Any(indicator => content.Contains(indicator));
            
            return hasNumberPattern || hasListingStructure;
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Context timing analysis without business assumptions.
        /// </summary>
        private void AnalyzeContextualTiming(Receipt currentReceipt, List<ChatMessage> conversationHistory, OrderIntentAnalysis analysis)
        {
            _thoughtLogger.LogStep(3, "Analyzing contextual timing patterns");
            
            // ARCHITECTURAL PRINCIPLE: Structural analysis only - no business value assumptions
            if (currentReceipt.Items.Any())
            {
                var orderSize = currentReceipt.Items.Sum(i => i.Quantity);
                var conversationLength = conversationHistory.Count;
                
                if (orderSize >= 2 && conversationLength >= 4)
                {
                    analysis.SubstantialOrderPattern = true;
                    analysis.CompletionLikelihood += 0.1; // Reduced - just structural hint
                    _thoughtLogger.LogThought($"Substantial interaction pattern - {orderSize} items, {conversationLength} exchanges");
                }
                
                // ARCHITECTURAL FIX: Remove currency/value assumptions - just indicate items exist
                analysis.CompletionLikelihood += 0.05; // Minimal structural boost
            }
            else
            {
                // Empty order - structural observation only
                analysis.CompletionLikelihood -= 0.2; // Reduced impact
                _thoughtLogger.LogThought("Empty order - structural completion signals less relevant");
            }
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Check temporal relevance without semantic assumptions.
        /// </summary>
        private bool IsRecentMessage(ChatMessage message, TimeSpan maxAge)
        {
            return DateTime.Now - message.Timestamp <= maxAge;
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Conservative intent determination that defers to AI semantic understanding.
        /// </summary>
        private void DetermineFinalIntent(OrderIntentAnalysis analysis)
        {
            _thoughtLogger.LogStep(4, "Making final intent determination");
            
            _thoughtLogger.LogThought($"Likelihood scores - Completion: {analysis.CompletionLikelihood:F2}, NewOrder: {analysis.NewOrderLikelihood:F2}, Question: {analysis.QuestionLikelihood:F2}");
            
            // ARCHITECTURAL PRINCIPLE: Extremely conservative structural analysis - defer to AI for all but clear structural cases
            
            // Clear structural question indicators
            if (analysis.QuestionLikelihood > 0.5)
            {
                analysis.Intent = OrderIntent.Question;
                analysis.Reasoning = "Clear question structure detected";
            }
            // ARCHITECTURAL PRINCIPLE: Very high threshold for structural completion detection
            else if (analysis.CompletionLikelihood > 0.8 && 
                     analysis.HasItems &&
                     analysis.RecentCompletionContext &&
                     analysis.SubstantialOrderPattern)
            {
                analysis.Intent = OrderIntent.ReadyForPayment;
                analysis.Reasoning = $"High structural completion confidence ({analysis.CompletionLikelihood:F2}) with multiple indicators";
            }
            // Clear structural ordering signals
            else if (analysis.NewOrderLikelihood > 0.6 && 
                     analysis.NewOrderLikelihood > analysis.CompletionLikelihood)
            {
                analysis.Intent = OrderIntent.ContinueOrdering;
                analysis.Reasoning = $"Clear ordering structure detected ({analysis.NewOrderLikelihood:F2})";
            }
            // ARCHITECTURAL PRINCIPLE: Default to ambiguous - let AI semantic understanding handle most cases
            else
            {
                analysis.Intent = OrderIntent.Ambiguous;
                analysis.Reasoning = "Structural analysis inconclusive - deferring to AI semantic understanding";
                _thoughtLogger.LogThought("AMBIGUOUS_INTENT_DEFAULT: Structural analysis insufficient - using AI semantic processing");
            }
            
            _thoughtLogger.LogThought($"FINAL_INTENT_DECISION: Intent={analysis.Intent}, Reasoning={analysis.Reasoning}");
        }
    }

    /// <summary>
    /// Culture-neutral analysis of customer's ordering intentions.
    /// ARCHITECTURAL PRINCIPLE: Based on structural patterns only, no business or cultural assumptions.
    /// </summary>
    public class OrderIntentAnalysis
    {
        public string OriginalInput { get; set; } = "";
        public OrderIntent Intent { get; set; } = OrderIntent.Unknown;
        public double ConfidenceLevel { get; set; } = 0.0;
        public PaymentStatus CurrentReceiptStatus { get; set; }
        public int ItemCount { get; set; }
        public int ConversationLength { get; set; }
        public string ReasonCode { get; set; } = "";
        public List<string> StructuralIndicators { get; set; } = new();
        
        // Structural pattern properties
        public double CompletionLikelihood { get; set; } = 0.0;
        public double NewOrderLikelihood { get; set; } = 0.0;
        public double QuestionLikelihood { get; set; } = 0.0;
        public string Reasoning { get; set; } = "";
        public bool HasItems => ItemCount > 0;
        public bool RecentCompletionContext { get; set; }
        public bool RecentClarificationContext { get; set; }
        public bool RapidResponsePattern { get; set; }
        public bool SubstantialOrderPattern { get; set; }
    }

    public enum OrderIntent
    {
        ContinueOrdering,
        ReadyForPayment,
        Question,
        Ambiguous, // ARCHITECTURAL PRINCIPLE: Most inputs should be ambiguous - let AI handle semantics
        Unknown
    }
}
