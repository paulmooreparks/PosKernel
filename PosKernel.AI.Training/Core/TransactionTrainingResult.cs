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
/// Result of executing a complete transaction scenario.
/// </summary>
public class TransactionTrainingResult
{
    public required string ScenarioId { get; set; }
    public int Generation { get; set; }
    public int ScenarioIndex { get; set; }
    public DateTime StartTime { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public bool TransactionSuccessful { get; set; }
    public double OverallScore { get; set; }
    public Dictionary<string, double> QualityMetrics { get; set; } = new();
    public string Summary { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public List<InteractionResult> InteractionResults { get; set; } = new();
}

/// <summary>
/// Result of a single customer interaction within a transaction.
/// </summary>
public class InteractionResult
{
    public required string CustomerInput { get; set; }
    public required string AIResponse { get; set; }
    public bool Success { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public Exception? Error { get; set; }
}
