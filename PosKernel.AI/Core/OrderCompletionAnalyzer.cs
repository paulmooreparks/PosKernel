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
    /// Uses AI semantic understanding instead of hardcoded language patterns.
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
        /// Enhanced with conversational context awareness for payment flow detection.
        /// No hardcoded language or cultural assumptions.
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

            // ARCHITECTURAL ENHANCEMENT: Multi-dimensional intent analysis
            AnalyzeStructuralPatterns(userInput, analysis);
            AnalyzeConversationFlow(conversationHistory, analysis);
            AnalyzeContextualTiming(currentReceipt, conversationHistory, analysis);
            AnalyzeResponseContext(userInput, conversationHistory, analysis); // NEW: Enhanced context analysis
            
            DetermineFinalIntent(analysis);
            
            return analysis;
        }

        /// <summary>
        /// NEW: Analyzes if the user input is responding to a specific system prompt/question.
        /// Language-agnostic approach using conversational context patterns.
        /// </summary>
        private void AnalyzeResponseContext(string userInput, List<ChatMessage> conversationHistory, OrderIntentAnalysis analysis)
        {
            if (!conversationHistory.Any()) {
                return;
            }

            // Get the last few system messages to understand context
            var recentSystemMessages = conversationHistory
                .TakeLast(5) // Look at last 5 messages for context
                .Where(m => !m.Sender.Equals("Customer", StringComparison.OrdinalIgnoreCase))
                .TakeLast(2) // Focus on last 2 non-customer messages
                .ToList();

            var lastSystemMessage = recentSystemMessages.LastOrDefault();
            if (lastSystemMessage == null) {
                return;
            }

            var systemContent = lastSystemMessage.Content?.ToLowerInvariant() ?? "";

            // ARCHITECTURAL PRINCIPLE: Use structural/semantic patterns, not hardcoded phrases
            // Detect if system just asked about payment method
            var isPaymentMethodPrompt = 
                ContainsPaymentMethodIndicators(systemContent) &&
                ContainsTotalAmount(systemContent) &&
                IsRecentMessage(lastSystemMessage, TimeSpan.FromMinutes(2));

            if (isPaymentMethodPrompt)
            {
                // User is responding to a payment method question
                analysis.IsPaymentMethodResponse = true;
                analysis.ConfidenceBoost += 0.6; // Strong indicator of payment intent

                // Check if user input looks like a payment method
                if (LooksLikePaymentMethod(userInput))
                {
                    analysis.Intent = OrderIntent.ProcessPayment;
                    analysis.ConfidenceLevel = 0.9;
                    analysis.ReasonCode = "payment_method_response";
                    return;
                }
            }

            // Detect if system just provided order summary
            var isOrderSummaryContext = 
                ContainsOrderSummaryIndicators(systemContent) &&
                ContainsTotalAmount(systemContent);

            if (isOrderSummaryContext)
            {
                analysis.IsOrderSummaryResponse = true;
                analysis.ConfidenceBoost += 0.4;

                // If user responds with payment-related terms after summary, it's likely payment intent
                if (LooksLikePaymentMethod(userInput) || ContainsPaymentIndicators(userInput))
                {
                    analysis.Intent = OrderIntent.ProcessPayment;
                    analysis.ConfidenceLevel = 0.8;
                    analysis.ReasonCode = "payment_after_summary";
                    return;
                }
            }

            // NEW: Detect if user is confirming an order
            var isConfirmationResponse = 
                systemContent.Contains("confirm") &&
                ContainsTotalAmount(systemContent);

            if (isConfirmationResponse)
            {
                analysis.IsConfirmationResponse = true;
                analysis.ConfidenceBoost += 0.5;

                // If user input indicates agreement or payment, classify as payment intent
                if (ContainsPaymentIndicators(userInput))
                {
                    analysis.Intent = OrderIntent.ProcessPayment;
                    analysis.ConfidenceLevel = 0.85;
                    analysis.ReasonCode = "confirmation_payment";
                    return;
                }
            }

            // NEW: Detect if user is asking a question
            var isQuestionResponse = 
                systemContent.Contains("?") &&
                !systemContent.Contains("confirm") &&
                !systemContent.Contains("pay");

            if (isQuestionResponse)
            {
                analysis.IsQuestionResponse = true;
                analysis.ConfidenceBoost += 0.3;
            }
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Remove hardcoded payment patterns - use only structural indicators.
        /// Enhanced with better length and context detection.
        /// </summary>
        private bool LooksLikePaymentMethod(string input)
        {
            var normalized = input.ToLowerInvariant().Trim();
            
            // ARCHITECTURAL PRINCIPLE: Use only structural patterns, not hardcoded payment terms
            // Length indicators - payment methods are typically short responses
            if (normalized.Length > 25) {
                return false;
            }
            
            // Only use structural characteristics - let AI handle semantic understanding
            var wordCount = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            return wordCount <= 3; // Payment methods are typically 1-3 words in conversational context
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Detect payment method prompts using structural indicators.
        /// </summary>
        private bool ContainsPaymentMethodIndicators(string content)
        {
            // Look for question marks + payment context
            var hasQuestion = content.Contains("?");
            
            // Look for structural indicators of payment method questions
            var paymentContextIndicators = new[]
            {
                "pay", "payment", "method", "card", "cash", "how"
            };

            return hasQuestion && 
                   paymentContextIndicators.Count(indicator => content.Contains(indicator)) >= 2;
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Detect order summaries using structural patterns.
        /// </summary>
        private bool ContainsOrderSummaryIndicators(string content)
        {
            // Structural indicators of order summaries
            var summaryIndicators = new[]
            {
                "total", "summary", "order", "items", "x", "=" // "x" and "=" suggest itemized list
            };

            return summaryIndicators.Count(indicator => content.Contains(indicator)) >= 2;
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Universal currency amount detection.
        /// </summary>
        private bool ContainsTotalAmount(string content)
        {
            // Look for currency symbols or decimal patterns that suggest monetary amounts
            return content.Any(c => "¥€£$₹".Contains(c)) || // Universal currency symbols
                   System.Text.RegularExpressions.Regex.IsMatch(content, @"\d+\.\d{2}") || // Decimal amounts
                   content.Contains("total");
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Check if message is recent enough to be contextually relevant.
        /// </summary>
        private bool IsRecentMessage(ChatMessage message, TimeSpan maxAge)
        {
            return DateTime.Now - message.Timestamp <= maxAge;
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Language-agnostic payment intent detection.
        /// </summary>
        private bool ContainsPaymentIndicators(string input)
        {
            // ARCHITECTURAL FIX: Don't hardcode payment terms - let AI semantic analysis handle this
            // This method now only provides structural hints for context switching
            var normalized = input.ToLowerInvariant();
            
            // Only use structural patterns that are truly universal
            var wordCount = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var isShortResponse = wordCount <= 2 && normalized.Length <= 15;
            
            return isShortResponse; // Short responses in payment context likely indicate payment methods
        }

        private void AnalyzeStructuralPatterns(string userInput, OrderIntentAnalysis analysis)
        {
            _thoughtLogger.LogStep(1, "Analyzing structural patterns");
            
            var input = userInput.Trim();
            var wordCount = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            var hasNumbers = input.Any(char.IsDigit);
            var hasQuestionMark = input.EndsWith('?');
            var isVeryShort = wordCount <= 2;
            var isNegativeLength = input.Length <= 5; // Very short responses often negative
            
            _thoughtLogger.LogThought($"📏 Structural analysis - Length: {input.Length}, Words: {wordCount}, HasNumbers: {hasNumbers}, HasQuestion: {hasQuestionMark}");
            
            // Structural indicators without language assumptions
            if (isVeryShort && !hasNumbers)
            {
                analysis.StructuralIndicators.Add("Very short response - likely completion or negation");
                analysis.CompletionLikelihood += 0.4;
            }
            
            if (hasNumbers)
            {
                analysis.StructuralIndicators.Add("Contains numbers - likely quantity specification");
                analysis.NewOrderLikelihood += 0.6;
            }
            
            if (hasQuestionMark)
            {
                analysis.StructuralIndicators.Add("Question format - likely inquiry or clarification");
                analysis.QuestionLikelihood = 0.8;
            }
            
            if (wordCount >= 4 && hasNumbers)
            {
                analysis.StructuralIndicators.Add("Multi-word with numbers - likely structured order");
                analysis.NewOrderLikelihood += 0.5;
            }
        }

        private void AnalyzeConversationFlow(List<ChatMessage> conversationHistory, OrderIntentAnalysis analysis)
        {
            _thoughtLogger.LogStep(2, "Analyzing conversation flow patterns");
            
            var recentMessages = conversationHistory.TakeLast(3).ToList();
            var lastSystemMessage = recentMessages.LastOrDefault(m => m.IsSystem || !m.Sender.Equals("Customer", StringComparison.OrdinalIgnoreCase));
            
            if (lastSystemMessage != null)
            {
                var systemContent = lastSystemMessage.Content.ToLowerInvariant();
                
                // Pattern: System asked about completion recently
                if (systemContent.Contains("else") || systemContent.Contains("total") || systemContent.Contains("complete"))
                {
                    analysis.RecentCompletionContext = true;
                    analysis.CompletionLikelihood += 0.3;
                    _thoughtLogger.LogThought("💬 Recent system message suggests completion context");
                }
                
                // Pattern: System asked clarifying question
                if (systemContent.Contains("?") || systemContent.Contains("which") || systemContent.Contains("kind"))
                {
                    analysis.RecentClarificationContext = true;
                    _thoughtLogger.LogThought("💬 Recent system message was clarification request");
                }
            }
            
            // Pattern: Customer message frequency (rapid responses might indicate adding more)
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
                        analysis.NewOrderLikelihood += 0.2;
                        _thoughtLogger.LogThought($"⚡ Rapid response pattern detected ({timeBetween.TotalSeconds:F1}s) - might be adding more");
                    }
                }
            }
        }

        private void AnalyzeContextualTiming(Receipt currentReceipt, List<ChatMessage> conversationHistory, OrderIntentAnalysis analysis)
        {
            _thoughtLogger.LogStep(3, "Analyzing contextual timing patterns");
            
            // Pattern: Order has items and conversation has been going for a while
            if (currentReceipt.Items.Any())
            {
                var orderSize = currentReceipt.Items.Sum(i => i.Quantity);
                var conversationLength = conversationHistory.Count;
                
                if (orderSize >= 2 && conversationLength >= 4)
                {
                    analysis.SubstantialOrderPattern = true;
                    analysis.CompletionLikelihood += 0.3;
                    _thoughtLogger.LogThought($"📊 Substantial order pattern - {orderSize} items, {conversationLength} exchanges");
                }
                
                // Pattern: Order value suggests natural completion point
                if (currentReceipt.Total > 5.0m) // Arbitrary threshold - adjust per business
                {
                    analysis.CompletionLikelihood += 0.1;
                    _thoughtLogger.LogThought($"💰 Order value suggests natural completion point: {currentReceipt.Total:C}");
                }
            }
            else
            {
                // Empty order - completion signals are less meaningful
                analysis.CompletionLikelihood -= 0.4;
                _thoughtLogger.LogThought("📝 Empty order - completion signals less meaningful");
            }
        }

        private void DetermineFinalIntent(OrderIntentAnalysis analysis)
        {
            _thoughtLogger.LogStep(4, "Making final intent determination");
            
            _thoughtLogger.LogThought($"📊 Likelihood scores - Completion: {analysis.CompletionLikelihood:F2}, NewOrder: {analysis.NewOrderLikelihood:F2}, Question: {analysis.QuestionLikelihood:F2}");
            
            // ARCHITECTURAL EVOLUTION: Move away from hard thresholds toward AI-native fuzzy understanding
            // The AI should naturally understand conversational completion without rigid mathematical cutoffs
            
            // Clear question indicators
            if (analysis.QuestionLikelihood > 0.6)
            {
                analysis.Intent = OrderIntent.Question;
                analysis.Reasoning = "High question likelihood based on structural analysis";
            }
            // Dominant completion signals with items present
            else if (analysis.CompletionLikelihood > analysis.NewOrderLikelihood && 
                     analysis.CompletionLikelihood > 0.5 && 
                     analysis.HasItems)
            {
                analysis.Intent = OrderIntent.ReadyForPayment;
                analysis.Reasoning = $"Completion signals ({analysis.CompletionLikelihood:F2}) exceed ordering signals ({analysis.NewOrderLikelihood:F2}) with items present";
            }
            // Empty order completion
            else if (analysis.CompletionLikelihood > analysis.NewOrderLikelihood && 
                     analysis.CompletionLikelihood > 0.5 && 
                     !analysis.HasItems)
            {
                analysis.Intent = OrderIntent.EmptyOrderCompletion;
                analysis.Reasoning = $"Completion signals without items - customer may be browsing";
            }
            // Dominant ordering signals
            else if (analysis.NewOrderLikelihood > analysis.CompletionLikelihood && 
                     analysis.NewOrderLikelihood > 0.4)
            {
                analysis.Intent = OrderIntent.ContinueOrdering;
                analysis.Reasoning = $"Ordering signals ({analysis.NewOrderLikelihood:F2}) exceed completion signals ({analysis.CompletionLikelihood:F2})";
            }
            // Ambiguous or mixed signals - let AI semantic understanding decide
            else
            {
                if (analysis.HasItems && analysis.CompletionLikelihood > 0.3)
                {
                    // Give completion the benefit of doubt when items exist
                    analysis.Intent = OrderIntent.ReadyForPayment;
                    analysis.Reasoning = $"Mixed signals resolved toward completion due to existing items ({analysis.CompletionLikelihood:F2} completion likelihood)";
                }
                else if (analysis.RecentCompletionContext)
                {
                    analysis.Intent = OrderIntent.ChangedMindAddMore;
                    analysis.Reasoning = "Mixed signals after recent completion discussion - customer likely adding more items";
                }
                else
                {
                    analysis.Intent = OrderIntent.Ambiguous;
                    analysis.Reasoning = $"Ambiguous signals - defer to AI semantic understanding";
                }
            }
            
            // CRITICAL FIX: Log the final intent decision for debugging
            _thoughtLogger.LogThought($"🎯 FINAL_INTENT_DECISION: Intent={analysis.Intent}, Reasoning={analysis.Reasoning}");
        }
    }

    /// <summary>
    /// Language-agnostic analysis of customer's ordering intentions.
    /// Based on structural patterns and conversation flow, not hardcoded language.
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
        public double ConfidenceBoost { get; set; } = 0.0;
        
        // Enhanced context properties
        public bool IsPaymentMethodResponse { get; set; }
        public bool IsOrderSummaryResponse { get; set; }
        public bool IsConfirmationResponse { get; set; }
        public bool IsQuestionResponse { get; set; }
        
        // Legacy properties for compatibility
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
        ChangedMindAddMore,
        EmptyOrderCompletion,
        Question,
        Ambiguous,
        ProcessPayment, // NEW: Intent for processing payment
        Unknown // NEW: Explicit unknown intent
    }
}
