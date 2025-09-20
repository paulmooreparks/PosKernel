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

        var config = await _dataStore.LoadAsync<TrainingConfiguration>(ConfigurationKey);

        // ARCHITECTURAL PRINCIPLE: Fail fast if configuration missing - no helpful defaults
        if (config == null)
        {
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Training configuration not found. " +
                "Initialize training configuration before starting optimization. " +
                "Use ITrainingConfigurationService.SaveConfigurationAsync() to create initial configuration " +
                "or implement configuration UI to set initial parameters.");
        }

        // ARCHITECTURAL PRINCIPLE: Validate loaded configuration - fail fast if corrupt
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
        var config = new TrainingConfiguration();
        
        _logger.LogDebug("Created default training configuration with {ScenarioCount} scenarios",
            config.ScenarioCount);

        return config;
    }
}
