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

namespace PosKernel.AI.Training.Core;

/// <summary>
/// Event arguments for training progress updates
/// </summary>
public class TrainingProgressEventArgs : EventArgs
{
    public required int Generation { get; init; }
    public required int CurrentScenario { get; init; }
    public required int TotalScenarios { get; init; }
    public required double Progress { get; init; }
    public required double CurrentScore { get; init; }
    public required double BestScore { get; init; }
    public required double Improvement { get; init; }
    public required string CurrentActivity { get; init; }
    public required TimeSpan ElapsedTime { get; init; }
    public required TimeSpan EstimatedRemaining { get; init; }
}

/// <summary>
/// Event arguments for scenario testing
/// </summary>
public class ScenarioTestEventArgs : EventArgs
{
    public required int Generation { get; init; }
    public required int ScenarioIndex { get; init; }
    public required string ScenarioDescription { get; init; }
    public required string UserInput { get; init; }
    public required string AIResponse { get; init; }
    public required double Score { get; init; }
    public required IReadOnlyDictionary<string, double> QualityMetrics { get; init; }
    public required TimeSpan Duration { get; init; }
    public bool IsSuccessful { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Event arguments for generation completion
/// </summary>
public class GenerationCompleteEventArgs : EventArgs
{
    public required int Generation { get; init; }
    public required double Score { get; init; }
    public required double Improvement { get; init; }
    public required int ScenariosEvaluated { get; init; }
    public required string PromptVariation { get; init; }
    public required IReadOnlyDictionary<string, double> QualityMetrics { get; init; }
    public required TimeSpan Duration { get; init; }
    public required bool IsImprovement { get; init; }
    public required bool IsNewBest { get; init; }
}

/// <summary>
/// Event arguments for training completion
/// </summary>
public class TrainingCompleteEventArgs : EventArgs
{
    public required string SessionId { get; init; }
    public required TrainingSessionState FinalState { get; init; }
    public required double FinalScore { get; init; }
    public required double BestScore { get; init; }
    public required double TotalImprovement { get; init; }
    public required int GenerationsCompleted { get; init; }
    public required TimeSpan TotalDuration { get; init; }
    public string? OptimizedPrompt { get; init; }
    public string? AbortReason { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, object>? Metrics { get; init; }

    public bool IsSuccessful => FinalState == TrainingSessionState.Completed;
}
