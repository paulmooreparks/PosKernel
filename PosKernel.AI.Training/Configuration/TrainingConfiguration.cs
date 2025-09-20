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

namespace PosKernel.AI.Training.Configuration;

/// <summary>
/// Configuration for AI training sessions with comprehensive validation
/// </summary>
public class TrainingConfiguration
{
    // Core Training Parameters
    [Range(50, 10000, ErrorMessage = "ScenarioCount must be between 50 and 10,000 for meaningful results")]
    public int ScenarioCount { get; set; } = 500;

    [Range(5, 100, ErrorMessage = "MaxGenerations must be between 5 and 100")]
    public int MaxGenerations { get; set; } = 50;

    [Range(0.001, 0.1, ErrorMessage = "ImprovementThreshold must be between 0.1% and 10%")]
    public double ImprovementThreshold { get; set; } = 0.02; // 2% improvement required

    [Range(50, 1000, ErrorMessage = "ValidationScenarios must be between 50 and 1,000")]
    public int ValidationScenarios { get; set; } = 200; // Fresh scenarios for validation

    // Scenario Generation Mix (must sum to 1.0)
    public ScenarioDistribution ScenarioMix { get; set; } = new()
    {
        BasicOrdering = 0.40,      // Simple orders
        EdgeCases = 0.20,          // Challenging scenarios
        CulturalVariations = 0.20, // Language/cultural tests
        AmbiguousRequests = 0.10,  // Disambiguation tests
        PaymentScenarios = 0.10    // Payment flow tests
    };

    // Training Aggressiveness Controls
    public TrainingAggressiveness Aggressiveness { get; set; } = new()
    {
        MutationRate = 0.15,              // 15% of prompt modified per mutation
        ExplorationRatio = 0.30,          // 30% exploratory vs targeted mutations
        RegressionTolerance = -0.05,      // 5% regression triggers abort
        StagnationLimit = 5,              // Generations without improvement
        MinimumProgress = 0.001           // 0.1% minimum improvement per generation
    };

    // Quality Thresholds - Enhanced for Multi-Dimensional Assessment
    public QualityTargets QualityTargets { get; set; } = new()
    {
        // Primary Quality Metrics
        ConversationCompletion = 0.90,    // 90% successful completions
        TechnicalAccuracy = 0.95,         // 95% correct tool usage
        PersonalityConsistency = 0.90,    // 90% authentic responses

        // Secondary Quality Metrics
        ContextualAppropriateness = 0.85, // 85% contextually appropriate
        ValueOptimization = 0.80,         // 80% optimal recommendations
        InformationCompleteness = 0.88,   // 88% comprehensive responses

        // Tertiary Quality Metrics
        DomainExpertise = 0.75,           // 75% expert-level responses
        CulturalAuthenticity = 0.85,      // 85% culturally appropriate
        CustomerSatisfaction = 0.82       // 82% satisfying interactions
    };

    // Training Focus Areas (0.0 = ignore, 1.0 = maximum focus)
    public TrainingFocus Focus { get; set; } = new()
    {
        ToolSelectionAccuracy = 1.0,      // Critical - ensure correct tools
        PersonalityAuthenticity = 0.8,    // Important - maintain Uncle character
        PaymentFlowCompletion = 1.0,      // Critical - complete payment flows
        ContextualAppropriateness = 0.9,  // High - appropriate responses
        InformationCompleteness = 0.85,   // High - provide detailed responses
        ValueOptimization = 0.7,          // Important - suggest combinations
        AmbiguityHandling = 0.6,          // Moderate - proper disambiguation
        CulturalTermRecognition = 0.6,    // Moderate - Singlish patterns
        ConversationEfficiency = 0.4      // Low - speed optimization
    };

    // Safety Guards
    public SafetyConfiguration Safety { get; set; } = new()
    {
        MaxTrainingDuration = TimeSpan.FromHours(8),  // Abort after 8 hours
        MaxPromptLength = 10000,                       // Prevent prompt bloat
        RequiredRegressionTests = true,                // Always run regression tests
        HumanApprovalThreshold = 0.15,                // 15% improvement needs review
        AutoBackupInterval = TimeSpan.FromMinutes(30)  // Backup every 30 minutes
    };

    // Persistence Settings
    public PersistenceConfiguration Persistence { get; set; } = new()
    {
        SaveIntermediateResults = true,
        ResultsRetentionDays = 30,
        DetailedLogging = true,
        MetricsCollection = true
    };
}

/// <summary>
/// Distribution of scenario types for training
/// </summary>
public class ScenarioDistribution
{
    [Range(0.0, 1.0, ErrorMessage = "BasicOrdering must be between 0.0 and 1.0")]
    public double BasicOrdering { get; set; }

    [Range(0.0, 1.0, ErrorMessage = "EdgeCases must be between 0.0 and 1.0")]
    public double EdgeCases { get; set; }

    [Range(0.0, 1.0, ErrorMessage = "CulturalVariations must be between 0.0 and 1.0")]
    public double CulturalVariations { get; set; }

    [Range(0.0, 1.0, ErrorMessage = "AmbiguousRequests must be between 0.0 and 1.0")]
    public double AmbiguousRequests { get; set; }

    [Range(0.0, 1.0, ErrorMessage = "PaymentScenarios must be between 0.0 and 1.0")]
    public double PaymentScenarios { get; set; }

    /// <summary>
    /// Validates that all percentages sum to 1.0
    /// </summary>
    public void Validate()
    {
        var total = BasicOrdering + EdgeCases + CulturalVariations + 
                   AmbiguousRequests + PaymentScenarios;

        // ARCHITECTURAL PRINCIPLE: Fail fast with clear error message
        if (Math.Abs(total - 1.0) > 0.001)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Scenario distribution must sum to 1.0, got {total:F3}. " +
                "Adjust scenario percentages to total 100%. " +
                $"Current: BasicOrdering={BasicOrdering:F3}, EdgeCases={EdgeCases:F3}, " +
                $"CulturalVariations={CulturalVariations:F3}, AmbiguousRequests={AmbiguousRequests:F3}, " +
                $"PaymentScenarios={PaymentScenarios:F3}");
        }
    }
}

/// <summary>
/// Training aggressiveness parameters
/// </summary>
public class TrainingAggressiveness
{
    [Range(0.05, 0.50, ErrorMessage = "MutationRate must be between 5% and 50%")]
    public double MutationRate { get; set; }

    [Range(0.10, 0.60, ErrorMessage = "ExplorationRatio must be between 10% and 60%")]
    public double ExplorationRatio { get; set; }

    [Range(-0.10, -0.01, ErrorMessage = "RegressionTolerance must be between -10% and -1%")]
    public double RegressionTolerance { get; set; }

    [Range(3, 20, ErrorMessage = "StagnationLimit must be between 3 and 20 generations")]
    public int StagnationLimit { get; set; }

    [Range(0.0001, 0.01, ErrorMessage = "MinimumProgress must be between 0.01% and 1%")]
    public double MinimumProgress { get; set; }
}

/// <summary>
/// Quality targets for training optimization
/// </summary>
public class QualityTargets
{
    // Primary Quality Metrics
    [Range(0.5, 1.0, ErrorMessage = "ConversationCompletion must be between 50% and 100%")]
    public double ConversationCompletion { get; set; }

    [Range(0.5, 1.0, ErrorMessage = "TechnicalAccuracy must be between 50% and 100%")]
    public double TechnicalAccuracy { get; set; }

    [Range(0.5, 1.0, ErrorMessage = "PersonalityConsistency must be between 50% and 100%")]
    public double PersonalityConsistency { get; set; }

    // Secondary Quality Metrics
    [Range(0.5, 1.0, ErrorMessage = "ContextualAppropriateness must be between 50% and 100%")]
    public double ContextualAppropriateness { get; set; }

    [Range(0.5, 1.0, ErrorMessage = "ValueOptimization must be between 50% and 100%")]
    public double ValueOptimization { get; set; }

    [Range(0.5, 1.0, ErrorMessage = "InformationCompleteness must be between 50% and 100%")]
    public double InformationCompleteness { get; set; }

    // Tertiary Quality Metrics
    [Range(0.5, 1.0, ErrorMessage = "DomainExpertise must be between 50% and 100%")]
    public double DomainExpertise { get; set; }

    [Range(0.5, 1.0, ErrorMessage = "CulturalAuthenticity must be between 50% and 100%")]
    public double CulturalAuthenticity { get; set; }

    [Range(0.5, 1.0, ErrorMessage = "CustomerSatisfaction must be between 50% and 100%")]
    public double CustomerSatisfaction { get; set; }
}

/// <summary>
/// Training focus areas with weighted importance
/// </summary>
public class TrainingFocus
{
    [Range(0.0, 1.0, ErrorMessage = "ToolSelectionAccuracy must be between 0.0 and 1.0")]
    public double ToolSelectionAccuracy { get; set; }

    [Range(0.0, 1.0, ErrorMessage = "PersonalityAuthenticity must be between 0.0 and 1.0")]
    public double PersonalityAuthenticity { get; set; }

    [Range(0.0, 1.0, ErrorMessage = "PaymentFlowCompletion must be between 0.0 and 1.0")]
    public double PaymentFlowCompletion { get; set; }

    [Range(0.0, 1.0, ErrorMessage = "ContextualAppropriateness must be between 0.0 and 1.0")]
    public double ContextualAppropriateness { get; set; }

    [Range(0.0, 1.0, ErrorMessage = "InformationCompleteness must be between 0.0 and 1.0")]
    public double InformationCompleteness { get; set; }

    [Range(0.0, 1.0, ErrorMessage = "ValueOptimization must be between 0.0 and 1.0")]
    public double ValueOptimization { get; set; }

    [Range(0.0, 1.0, ErrorMessage = "AmbiguityHandling must be between 0.0 and 1.0")]
    public double AmbiguityHandling { get; set; }

    [Range(0.0, 1.0, ErrorMessage = "CulturalTermRecognition must be between 0.0 and 1.0")]
    public double CulturalTermRecognition { get; set; }

    [Range(0.0, 1.0, ErrorMessage = "ConversationEfficiency must be between 0.0 and 1.0")]
    public double ConversationEfficiency { get; set; }
}

/// <summary>
/// Safety configuration to prevent training issues
/// </summary>
public class SafetyConfiguration
{
    public TimeSpan MaxTrainingDuration { get; set; }

    [Range(1000, 50000, ErrorMessage = "MaxPromptLength must be between 1,000 and 50,000 characters")]
    public int MaxPromptLength { get; set; }

    public bool RequiredRegressionTests { get; set; }

    [Range(0.05, 0.50, ErrorMessage = "HumanApprovalThreshold must be between 5% and 50%")]
    public double HumanApprovalThreshold { get; set; }

    public TimeSpan AutoBackupInterval { get; set; }
}

/// <summary>
/// Persistence and data retention configuration
/// </summary>
public class PersistenceConfiguration
{
    public bool SaveIntermediateResults { get; set; }

    [Range(1, 365, ErrorMessage = "ResultsRetentionDays must be between 1 and 365 days")]
    public int ResultsRetentionDays { get; set; }

    public bool DetailedLogging { get; set; }
    public bool MetricsCollection { get; set; }
}
