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

using PosKernel.AI.Training.Configuration;
using PosKernel.AI.Services;

namespace PosKernel.AI.Training.Core;

/// <summary>
/// Service for managing AI prompts used in production PosKernel.AI system
/// ARCHITECTURAL PRINCIPLE: Integrates with existing AiPersonalityFactory instead of creating separate storage
/// </summary>
public interface IPromptManagementService
{
    /// <summary>
    /// Gets the current prompt used in production for a specific personality and context
    /// </summary>
    Task<string> GetCurrentPromptAsync(PersonalityType personalityType, string promptType, PromptContext context);

    /// <summary>
    /// Updates the production prompt for a specific personality and context
    /// </summary>
    Task SaveOptimizedPromptAsync(PersonalityType personalityType, string promptType, string optimizedPrompt, double performanceScore, string trainingSessionId);

    /// <summary>
    /// Gets all available prompt types for a personality (ordering, greeting, tool_acknowledgment, etc.)
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailablePromptTypesAsync(PersonalityType personalityType);

    /// <summary>
    /// Gets prompt optimization history for analysis
    /// </summary>
    Task<IReadOnlyList<PromptOptimizationRecord>> GetPromptHistoryAsync(PersonalityType personalityType, string promptType, int limit = 10);

    /// <summary>
    /// Tests a prompt variant against real production system
    /// </summary>
    Task<PromptTestResult> TestPromptVariantAsync(PersonalityType personalityType, string promptType, string promptVariant, string testScenario);
}

/// <summary>
/// Record of a prompt optimization from training
/// </summary>
public record PromptOptimizationRecord
{
    public required string Id { get; init; }
    public required PersonalityType PersonalityType { get; init; }
    public required string PromptType { get; init; }
    public required string OriginalPrompt { get; init; }
    public required string OptimizedPrompt { get; init; }
    public required double PerformanceScore { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string TrainingSessionId { get; init; }
    public required IReadOnlyDictionary<string, double> QualityMetrics { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Result of testing a prompt variant against production system
/// </summary>
public record PromptTestResult
{
    public required bool IsSuccessful { get; init; }
    public required double ResponseQuality { get; init; }
    public required string ActualResponse { get; init; }
    public required TimeSpan ResponseTime { get; init; }
    public required IReadOnlyDictionary<string, double> QualityMetrics { get; init; }
    public string? ErrorMessage { get; init; }
}
