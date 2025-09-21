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
using Microsoft.Extensions.Logging;
using PosKernel.AI.Training.Storage;

namespace PosKernel.AI.Training.Configuration;

/// <summary>
/// Configuration service with fail-fast validation following POS Kernel principles
/// </summary>
public class TrainingConfigurationService : ITrainingConfigurationService
{
    private readonly ITrainingDataStore _dataStore;
    private readonly ILogger<TrainingConfigurationService> _logger;
    private const string ConfigurationKey = "training-config";

    public TrainingConfigurationService(
        ITrainingDataStore dataStore,
        ILogger<TrainingConfigurationService> logger)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TrainingConfiguration> LoadConfigurationAsync()
    {
        _logger.LogDebug("Loading training configuration");

        var config = _dataStore.Load<TrainingConfiguration>(ConfigurationKey);

        // ARCHITECTURAL FIX: Create default configuration file if it doesn't exist
        if (config == null)
        {
            _logger.LogInformation("Training configuration not found, creating default configuration file");
            config = CreateDefaultConfigurationWithTestingValues();
            
            // Save the default configuration to the user's .poskernel directory
            await _dataStore.SaveAsync(ConfigurationKey, config);
            _logger.LogInformation("Default training configuration saved to user's .poskernel directory");
        }

        // ARCHITECTURAL PRINCIPLE: Validate loaded/created configuration - fail fast if corrupt
        var validation = await ValidateConfigurationAsync(config);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Invalid training configuration loaded from storage. " +
                $"Configuration may be corrupted or incompatible. " +
                $"Errors: {string.Join(", ", validation.Errors)}. " +
                "Delete configuration and recreate or fix validation errors.");
        }

        // Log warnings but allow continuation
        if (validation.Warnings.Any())
        {
            foreach (var warning in validation.Warnings)
            {
                _logger.LogWarning("Configuration warning: {Warning}", warning);
            }
        }

        _logger.LogInformation("Training configuration loaded successfully with {ScenarioCount} scenarios, {MaxGenerations} max generations",
            config.ScenarioCount, config.MaxGenerations);

        return config;
    }

    public async Task SaveConfigurationAsync(TrainingConfiguration config)
    {
        // ARCHITECTURAL PRINCIPLE: Validate before save - fail fast on invalid configuration
        ArgumentNullException.ThrowIfNull(config);

        var validation = await ValidateConfigurationAsync(config);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Cannot save invalid training configuration. " +
                $"Fix validation errors before saving. " +
                $"Errors: {string.Join(", ", validation.Errors)}");
        }

        await _dataStore.SaveAsync(ConfigurationKey, config);
        
        _logger.LogInformation("Training configuration saved successfully");

        // Log warnings after successful save
        if (validation.Warnings.Any())
        {
            foreach (var warning in validation.Warnings)
            {
                _logger.LogWarning("Configuration warning: {Warning}", warning);
            }
        }
    }

    public Task<ValidationResult> ValidateConfigurationAsync(TrainingConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate using data annotations
        var validationContext = new ValidationContext(config);
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        
        if (!Validator.TryValidateObject(config, validationContext, validationResults, validateAllProperties: true))
        {
            errors.AddRange(validationResults.Select(r => r.ErrorMessage ?? "Unknown validation error"));
        }

        // Additional business logic validation
        try
        {
            config.ScenarioMix.Validate();
        }
        catch (InvalidOperationException ex)
        {
            errors.Add(ex.Message);
        }

        // Validate safety constraints
        if (config.Safety.MaxTrainingDuration > TimeSpan.FromHours(24))
        {
            warnings.Add("MaxTrainingDuration exceeds 24 hours - consider shorter sessions for better monitoring");
        }

        if (config.Safety.HumanApprovalThreshold < 0.10)
        {
            warnings.Add("HumanApprovalThreshold below 10% may allow significant changes without review");
        }

        // Validate quality target relationships
        if (config.QualityTargets.TechnicalAccuracy < config.QualityTargets.ConversationCompletion)
        {
            warnings.Add("TechnicalAccuracy should typically be higher than ConversationCompletion");
        }

        // Validate focus area balance
        var totalFocus = config.Focus.ToolSelectionAccuracy + config.Focus.PersonalityAuthenticity + 
                        config.Focus.PaymentFlowCompletion + config.Focus.ContextualAppropriateness;
        if (totalFocus < 1.0)
        {
            warnings.Add("Low total focus values may result in minimal training improvements");
        }

        // Performance warnings
        if (config.ScenarioCount > 2000 && config.MaxGenerations > 50)
        {
            warnings.Add("High scenario count with many generations may result in very long training times");
        }

        var result = errors.Any() 
            ? ValidationResult.Failure(errors.ToArray())
            : warnings.Any() 
                ? ValidationResult.WithWarnings(warnings.ToArray())
                : ValidationResult.Success();

        _logger.LogDebug("Configuration validation completed: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}",
            result.IsValid, errors.Count, warnings.Count);

        return Task.FromResult(result);
    }

    public TrainingConfiguration CreateDefaultConfiguration()
    {
        // ARCHITECTURAL FIX: Use testing values by default until moved to appconfig.json
        return CreateDefaultConfigurationWithTestingValues();
    }

    /// <summary>
    /// Creates default configuration with testing values (3/3) for initial validation.
    /// TODO: Move these hard-coded values to appconfig.json
    /// </summary>
    private TrainingConfiguration CreateDefaultConfigurationWithTestingValues()
    {
        // ARCHITECTURAL NOTE: Hard-coded testing values for now - will move to appconfig.json later
        var config = new TrainingConfiguration
        {
            // TESTING VALUES: Reduced for initial validation
            ScenarioCount = 3,
            MaxGenerations = 3,
            ImprovementThreshold = 0.02,
            ValidationScenarios = 3,

            // Scenario Generation Mix (must sum to 1.0)
            ScenarioMix = new ScenarioDistribution
            {
                BasicOrdering = 0.40,      // Simple orders
                EdgeCases = 0.20,          // Challenging scenarios
                CulturalVariations = 0.20, // Language/cultural tests
                AmbiguousRequests = 0.10,  // Disambiguation tests
                PaymentScenarios = 0.10    // Payment flow tests
            },

            // Training Aggressiveness Controls
            Aggressiveness = new TrainingAggressiveness
            {
                MutationRate = 0.15,              // 15% of prompt modified per mutation
                ExplorationRatio = 0.30,          // 30% exploratory vs targeted mutations
                RegressionTolerance = -0.05,      // 5% regression triggers abort
                StagnationLimit = 5,              // Generations without improvement
                MinimumProgress = 0.001           // 0.1% minimum improvement per generation
            },

            // Quality Thresholds - Enhanced for Multi-Dimensional Assessment
            QualityTargets = new QualityTargets
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
            },

            // Training Focus Areas (0.0 = ignore, 1.0 = maximum focus)
            Focus = new TrainingFocus
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
            },

            // Safety Guards
            Safety = new SafetyConfiguration
            {
                MaxTrainingDuration = TimeSpan.FromHours(8),  // Abort after 8 hours
                MaxPromptLength = 10000,                       // Prevent prompt bloat
                RequiredRegressionTests = true,                // Always run regression tests
                HumanApprovalThreshold = 0.15,                // 15% improvement needs review
                AutoBackupInterval = TimeSpan.FromMinutes(30)  // Backup every 30 minutes
            },

            // Persistence Settings
            Persistence = new PersistenceConfiguration
            {
                SaveIntermediateResults = true,
                ResultsRetentionDays = 30,
                DetailedLogging = true,
                MetricsCollection = true
            }
        };
        
        _logger.LogDebug("Created default training configuration with testing values: {ScenarioCount} scenarios, {MaxGenerations} generations",
            config.ScenarioCount, config.MaxGenerations);

        return config;
    }
}
