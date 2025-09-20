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

namespace PosKernel.AI.Training.Configuration;

/// <summary>
/// Service for managing training configuration with fail-fast validation
/// </summary>
public interface ITrainingConfigurationService
{
    /// <summary>
    /// Loads training configuration, failing fast if not found or invalid
    /// </summary>
    Task<TrainingConfiguration> LoadConfigurationAsync();

    /// <summary>
    /// Saves training configuration with validation
    /// </summary>
    Task SaveConfigurationAsync(TrainingConfiguration config);

    /// <summary>
    /// Validates training configuration comprehensively
    /// </summary>
    Task<ValidationResult> ValidateConfigurationAsync(TrainingConfiguration config);

    /// <summary>
    /// Creates a default configuration for first-time setup
    /// </summary>
    TrainingConfiguration CreateDefaultConfiguration();
}

/// <summary>
/// Result of configuration validation
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static ValidationResult Success() => new() { IsValid = true };
    
    public static ValidationResult Failure(params string[] errors) => new() 
    { 
        IsValid = false, 
        Errors = errors.ToList() 
    };

    public static ValidationResult WithWarnings(params string[] warnings) => new()
    {
        IsValid = true,
        Warnings = warnings.ToList()
    };
}
