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

namespace PosKernel.AI.Training.Core;

/// <summary>
/// Core training session interface - completely UI-agnostic
/// </summary>
public interface ITrainingSession
{
    /// <summary>
    /// Runs a complete training session with the specified configuration
    /// </summary>
    Task<TrainingResults> RunTrainingSessionAsync(TrainingConfiguration config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event fired when training progress is updated
    /// </summary>
    event EventHandler<TrainingProgressEventArgs> ProgressUpdated;

    /// <summary>
    /// Event fired when a scenario is tested and evaluated
    /// </summary>
    event EventHandler<ScenarioTestEventArgs> ScenarioTested;

    /// <summary>
    /// Event fired when a generation completes
    /// </summary>
    event EventHandler<GenerationCompleteEventArgs> GenerationComplete;

    /// <summary>
    /// Event fired when training session completes (success or failure)
    /// </summary>
    event EventHandler<TrainingCompleteEventArgs> TrainingComplete;

    /// <summary>
    /// Current state of the training session
    /// </summary>
    TrainingSessionState State { get; }

    /// <summary>
    /// Current training statistics
    /// </summary>
    TrainingStatistics CurrentStats { get; }

    /// <summary>
    /// Pauses the training session
    /// </summary>
    Task PauseAsync();

    /// <summary>
    /// Resumes a paused training session
    /// </summary>
    Task ResumeAsync();

    /// <summary>
    /// Aborts the training session
    /// </summary>
    Task AbortAsync();
}

/// <summary>
/// State of a training session
/// </summary>
public enum TrainingSessionState
{
    NotStarted,
    Initializing,
    Running,
    Paused,
    Completed,
    Aborted,
    Failed
}

/// <summary>
/// Current training statistics
/// </summary>
public class TrainingStatistics
{
    public int CurrentGeneration { get; init; }
    public int TotalGenerations { get; init; }
    public int ScenariosCompleted { get; init; }
    public int TotalScenarios { get; init; }
    public double CurrentScore { get; init; }
    public double BestScore { get; init; }
    public double Improvement { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public TimeSpan EstimatedRemaining { get; init; }
    public int ConsecutiveFailures { get; init; }
    public int ConsecutiveImprovements { get; init; }

    public double OverallProgress => TotalGenerations > 0 
        ? (double)CurrentGeneration / TotalGenerations 
        : 0.0;

    public double GenerationProgress => TotalScenarios > 0
        ? (double)ScenariosCompleted / TotalScenarios
        : 0.0;
}

/// <summary>
/// Results of a completed training session
/// </summary>
public class TrainingResults
{
    public required string SessionId { get; init; }
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
    public required TrainingConfiguration Configuration { get; init; }
    public required TrainingSessionState FinalState { get; init; }
    public required int GenerationsCompleted { get; init; }
    public required double FinalScore { get; init; }
    public required double BestScore { get; init; }
    public required double TotalImprovement { get; init; }
    public required string OptimizedPrompt { get; init; }
    public required IReadOnlyList<GenerationResult> GenerationHistory { get; init; }
    public required IReadOnlyDictionary<string, object> Metrics { get; init; }
    public string? AbortReason { get; init; }
    public string? ErrorMessage { get; init; }

    public TimeSpan Duration => EndTime - StartTime;
    public bool IsSuccessful => FinalState == TrainingSessionState.Completed;
}

/// <summary>
/// Result of a single generation
/// </summary>
public class GenerationResult
{
    public required int Generation { get; init; }
    public required DateTime Timestamp { get; init; }
    public required double Score { get; init; }
    public required double Improvement { get; init; }
    public required int ScenariosEvaluated { get; init; }
    public required string PromptVariation { get; init; }
    public required IReadOnlyDictionary<string, double> QualityMetrics { get; init; }
    public required TimeSpan Duration { get; init; }
}
