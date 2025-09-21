/*
Copyright 2025 Paul Moore Parks and contributors

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System.ComponentModel.DataAnnotations;
using PosKernel.Abstractions;

namespace PosKernel.AI.Training.Scenarios;

/// <summary>
/// Represents a complete transaction scenario for AI training.
/// ARCHITECTURAL PRINCIPLE: Each scenario represents a full customer transaction lifecycle.
/// </summary>
public class TransactionScenario
{
    /// <summary>
    /// Unique identifier for this scenario.
    /// </summary>
    [Required]
    public required string ScenarioId { get; set; }

    /// <summary>
    /// Human-readable description of the scenario.
    /// </summary>
    [Required]
    public required string Description { get; set; }

    /// <summary>
    /// The category of transaction this scenario tests.
    /// </summary>
    [Required]
    public required TransactionScenarioType ScenarioType { get; set; }

    /// <summary>
    /// Complete sequence of customer interactions that make up this transaction.
    /// </summary>
    [Required]
    public required List<CustomerInteraction> Interactions { get; set; }

    /// <summary>
    /// Expected final state of the transaction after all interactions.
    /// </summary>
    [Required]
    public required TransactionExpectedResult ExpectedOutcome { get; set; }

    /// <summary>
    /// Validation weight for this scenario (0.0 to 1.0).
    /// Higher weights penalize failures more heavily during training.
    /// </summary>
    [Range(0.0, 1.0)]
    public double ValidationWeight { get; set; } = 1.0;

    /// <summary>
    /// Tags for scenario categorization and filtering.
    /// </summary>
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Types of transaction scenarios for comprehensive AI training.
/// </summary>
public enum TransactionScenarioType
{
    /// <summary>Simple order with single item and completion.</summary>
    SimpleOrder,
    
    /// <summary>Order with multiple items of same or different types.</summary>
    MultiItemOrder,
    
    /// <summary>Order with modifications (void items, change quantities, substitutions).</summary>
    OrderWithModifications,
    
    /// <summary>Customer abandons transaction without payment.</summary>
    AbandonedTransaction,
    
    /// <summary>Customer voids entire transaction (future implementation).</summary>
    VoidedTransaction,
    
    /// <summary>Complex order with cultural language mixing.</summary>
    CulturalLanguageMixing,
    
    /// <summary>Ambiguous requests requiring clarification.</summary>
    AmbiguousRequests,
    
    /// <summary>Payment flow testing with different methods.</summary>
    PaymentFlowTesting,
    
    /// <summary>Edge cases and error handling scenarios.</summary>
    EdgeCases
}

/// <summary>
/// Individual customer interaction within a transaction scenario.
/// </summary>
public class CustomerInteraction
{
    /// <summary>
    /// What the customer says or does.
    /// </summary>
    [Required]
    public required string CustomerInput { get; set; }

    /// <summary>
    /// Expected tools the AI should call in response.
    /// </summary>
    public List<ExpectedToolCall> ExpectedToolCalls { get; set; } = new();

    /// <summary>
    /// Expected patterns in the AI's conversational response.
    /// </summary>
    public List<ResponsePattern> ExpectedResponsePatterns { get; set; } = new();

    /// <summary>
    /// Expected transaction state after this interaction.
    /// </summary>
    public TransactionState? ExpectedTransactionState { get; set; }

    /// <summary>
    /// Maximum time allowed for this interaction (for performance testing).
    /// </summary>
    public TimeSpan? MaxResponseTime { get; set; }
}

/// <summary>
/// Expected final result of a complete transaction scenario.
/// </summary>
public class TransactionExpectedResult
{
    /// <summary>
    /// Final state the transaction should reach.
    /// ARCHITECTURAL PRINCIPLE: Must align with Rust kernel transaction states.
    /// </summary>
    [Required]
    public required TransactionState FinalState { get; set; }

    /// <summary>
    /// Expected line items in the final transaction.
    /// </summary>
    public List<ExpectedLineItem> ExpectedItems { get; set; } = new();

    /// <summary>
    /// Expected total amount (must match currency formatting rules).
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal ExpectedTotal { get; set; }

    /// <summary>
    /// Expected currency for the transaction.
    /// ARCHITECTURAL PRINCIPLE: Must come from store configuration, not hardcoded.
    /// </summary>
    public string? ExpectedCurrency { get; set; }

    /// <summary>
    /// Expected payment method if transaction completes.
    /// </summary>
    public string? ExpectedPaymentMethod { get; set; }

    /// <summary>
    /// All tool calls that should have been made during the transaction.
    /// </summary>
    public List<ExpectedToolCall> RequiredToolCalls { get; set; } = new();

    /// <summary>
    /// Success criteria for evaluating this transaction.
    /// </summary>
    public List<TransactionSuccessCriterion> SuccessCriteria { get; set; } = new();
}

/// <summary>
/// Expected line item in the transaction.
/// </summary>
public class ExpectedLineItem
{
    /// <summary>
    /// Product name or SKU expected.
    /// </summary>
    [Required]
    public required string ProductIdentifier { get; set; }

    /// <summary>
    /// Expected quantity.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Expected unit price (if known).
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? ExpectedUnitPrice { get; set; }

    /// <summary>
    /// Any preparation notes or modifications.
    /// </summary>
    public string? PreparationNotes { get; set; }
}

/// <summary>
/// Expected tool call during transaction processing.
/// </summary>
public class ExpectedToolCall
{
    /// <summary>
    /// Name of the tool that should be called.
    /// </summary>
    [Required]
    public required string ToolName { get; set; }

    /// <summary>
    /// Expected parameters passed to the tool.
    /// </summary>
    public Dictionary<string, object> ExpectedParameters { get; set; } = new();

    /// <summary>
    /// Whether this tool call is required (true) or optional (false).
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Expected order/sequence of this tool call (0-based).
    /// </summary>
    public int? ExpectedOrder { get; set; }
}

/// <summary>
/// Expected patterns in AI conversational responses.
/// </summary>
public class ResponsePattern
{
    /// <summary>
    /// Type of pattern to look for.
    /// </summary>
    [Required]
    public required ResponsePatternType PatternType { get; set; }

    /// <summary>
    /// Expected text or regex pattern.
    /// </summary>
    [Required]
    public required string Pattern { get; set; }

    /// <summary>
    /// Whether this pattern is required (true) or optional (false).
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Cultural authenticity requirement (for personality consistency).
    /// </summary>
    public bool RequiresCulturalAuthenticity { get; set; } = false;
}

/// <summary>
/// Types of patterns to validate in AI responses.
/// </summary>
public enum ResponsePatternType
{
    /// <summary>Must contain specific text or phrase.</summary>
    ContainsText,
    
    /// <summary>Must match a regular expression.</summary>
    MatchesRegex,
    
    /// <summary>Must acknowledge the action taken.</summary>
    ActionAcknowledgment,
    
    /// <summary>Must show cultural personality (Singlish, etc.).</summary>
    CulturalPersonality,
    
    /// <summary>Must ask for next action or continue conversation.</summary>
    ConversationFlow,
    
    /// <summary>Must not contain certain problematic patterns.</summary>
    MustNotContain
}

/// <summary>
/// Success criteria for evaluating transaction completion.
/// </summary>
public class TransactionSuccessCriterion
{
    /// <summary>
    /// Type of success criterion.
    /// </summary>
    [Required]
    public required SuccessCriterionType CriterionType { get; set; }

    /// <summary>
    /// Description of the criterion.
    /// </summary>
    [Required]
    public required string Description { get; set; }

    /// <summary>
    /// Weight of this criterion in overall scoring (0.0 to 1.0).
    /// </summary>
    [Range(0.0, 1.0)]
    public double Weight { get; set; } = 1.0;

    /// <summary>
    /// Whether failure of this criterion fails the entire transaction.
    /// </summary>
    public bool IsCritical { get; set; } = false;
}

/// <summary>
/// Types of success criteria for transaction evaluation.
/// </summary>
public enum SuccessCriterionType
{
    /// <summary>Transaction reached the expected final state.</summary>
    CorrectFinalState,
    
    /// <summary>All required tools were called correctly.</summary>
    CorrectToolCalls,
    
    /// <summary>Transaction total matches expected amount.</summary>
    CorrectTotal,
    
    /// <summary>All expected items are present with correct quantities.</summary>
    CorrectLineItems,
    
    /// <summary>Correct number of items were added to transaction.</summary>
    CorrectItemCount,
    
    /// <summary>AI responses maintained cultural personality throughout.</summary>
    PersonalityConsistency,
    
    /// <summary>Transaction completed within acceptable time limits.</summary>
    PerformanceAcceptable,
    
    /// <summary>No errors occurred during transaction processing.</summary>
    NoErrors,
    
    /// <summary>Payment method was processed correctly (if applicable).</summary>
    CorrectPaymentProcessing,
    
    /// <summary>Payment methods context was loaded when requested.</summary>
    PaymentMethodsLoaded
}
