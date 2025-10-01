using PosKernel.AI.Models;
using PosKernel.Configuration;
using Microsoft.Extensions.Logging;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// ARCHITECTURAL BRIDGE: Loads cultural context from prompt files for AI-first agent
    /// PRINCIPLE: Pure infrastructure - loads data from files, makes no business decisions
    /// </summary>
    public class StoreContextBuilder
    {
        private readonly PersonalityType _personalityType;
        private readonly ILogger<StoreContextBuilder> _logger;

        public StoreContextBuilder(PersonalityType personalityType, ILogger<StoreContextBuilder> logger)
        {
            _personalityType = personalityType;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Build rich cultural context by loading prompt files
        /// AI gets complete cultural knowledge without hardcoded assumptions in code
        /// </summary>
        public string BuildCulturalContext()
        {
            try
            {
                var culturalContext = new List<string>();

                // Load core cultural knowledge
                try
                {
                    var culturalReference = AiPersonalityFactory.LoadPrompt(_personalityType, "cultural-reference");
                    culturalContext.Add("## Cultural Knowledge");
                    culturalContext.Add(culturalReference);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Cultural reference not found for {PersonalityType}: {Error}", _personalityType, ex.Message);
                }

                // Load core behavioral rules
                try
                {
                    var coreRules = AiPersonalityFactory.LoadPrompt(_personalityType, "core-rules");
                    culturalContext.Add("## Core Behavioral Rules");
                    culturalContext.Add(coreRules);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Core rules not found for {PersonalityType}: {Error}", _personalityType, ex.Message);
                }

                // Load base context if available
                try
                {
                    var baseContext = AiPersonalityFactory.LoadPrompt(_personalityType, "base-context");
                    culturalContext.Add("## Base Context");
                    culturalContext.Add(baseContext);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Base context not found for {PersonalityType}: {Error}", _personalityType, ex.Message);
                }

                if (culturalContext.Count == 0)
                {
                    // ARCHITECTURAL PRINCIPLE: Fail fast if no cultural context available
                    throw new InvalidOperationException(
                        $"DESIGN DEFICIENCY: No cultural context files found for {_personalityType}. " +
                        $"AI-first architecture requires cultural knowledge to be in prompt files, not code. " +
                        $"Create cultural-reference.md, core-rules.md, or base-context.md files.");
                }

                return string.Join("\n\n", culturalContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build cultural context for {PersonalityType}", _personalityType);
                throw;
            }
        }

        /// <summary>
        /// ARCHITECTURAL PRINCIPLE: Build transaction-specific context
        /// Loads prompt files appropriate for current transaction state
        /// </summary>
        public string BuildTransactionContext(string transactionState)
        {
            try
            {
                return transactionState switch
                {
                    "StartTransaction" => LoadOptionalPrompt("greeting") ?? "You are ready to serve the next customer.",
                    "ItemsPending" => LoadOptionalPrompt("ordering-flow") ?? "Process customer orders and add items to transaction.",
                    "PaymentPending" => LoadOptionalPrompt("payment_request") ?? "Customer has items ready - request payment method.",
                    "EndOfTransaction" => LoadOptionalPrompt("payment_complete") ?? "Transaction complete - provide receipt and prepare for next customer.",
                    _ => "Handle customer interaction appropriately."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build transaction context for state {TransactionState}", transactionState);
                return "Handle customer interaction appropriately.";
            }
        }

        /// <summary>
        /// ARCHITECTURAL HELPER: Load prompt file with graceful fallback
        /// </summary>
        private string? LoadOptionalPrompt(string promptType)
        {
            try
            {
                return AiPersonalityFactory.LoadPrompt(_personalityType, promptType);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Optional prompt {PromptType} not found for {PersonalityType}: {Error}",
                    promptType, _personalityType, ex.Message);
                return null;
            }
        }
    }
}
