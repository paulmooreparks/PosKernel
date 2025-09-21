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

using Microsoft.Extensions.Logging;
using PosKernel.AI.Training.Storage;
using PosKernel.AI.Services;
using PosKernel.AI.Models;

namespace PosKernel.AI.Training.Core;

/// <summary>
/// Prompt management service that integrates with existing PosKernel.AI personality system
/// ARCHITECTURAL PRINCIPLE: Uses production AI infrastructure instead of separate storage
/// </summary>
public class PromptManagementService : IPromptManagementService
{
    private readonly ITrainingDataStore _dataStore;
    private readonly ILogger<PromptManagementService> _logger;
    
    // Storage keys
    private const string PROMPT_HISTORY_KEY_PREFIX = "prompt-history";
    private const string PROMPT_BACKUP_KEY_PREFIX = "prompt-backup";

    public PromptManagementService(
        ITrainingDataStore dataStore,
        ILogger<PromptManagementService> logger)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<string> GetCurrentPromptAsync(PersonalityType personalityType, string promptType, PromptContext context)
    {
        try
        {
            // ARCHITECTURAL PRINCIPLE: Integrate with existing AiPersonalityFactory instead of separate storage
            var prompt = AiPersonalityFactory.BuildPrompt(personalityType, promptType, context);
            
            if (string.IsNullOrEmpty(prompt))
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: AiPersonalityFactory returned empty prompt for {personalityType}/{promptType}. " +
                    $"Check prompt file exists and is properly formatted in PosKernel.AI/Prompts/{personalityType}/{promptType}.md");
            }

            _logger.LogDebug("Retrieved {PromptType} prompt for {PersonalityType}: {Length} characters", 
                promptType, personalityType, prompt.Length);
            
            return Task.FromResult(prompt);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Failed to get current prompt for {personalityType}/{promptType}. " +
                $"Training system requires access to existing prompt files via AiPersonalityFactory. " +
                $"Error: {ex.Message}");
        }
    }

    public async Task SaveOptimizedPromptAsync(PersonalityType personalityType, string promptType, string optimizedPrompt, double performanceScore, string trainingSessionId)
    {
        try
        {
            // ARCHITECTURAL PRINCIPLE: Save directly to the prompt files that PosKernel.AI reads
            var promptFilePath = Path.Combine("PosKernel.AI", "Prompts", personalityType.ToString(), $"{promptType}.md");
            var promptDirectory = Path.GetDirectoryName(promptFilePath);
            
            if (!string.IsNullOrEmpty(promptDirectory) && !Directory.Exists(promptDirectory))
            {
                Directory.CreateDirectory(promptDirectory);
                _logger.LogInformation("Created prompt directory: {Directory}", promptDirectory);
            }
            
            // Save the optimized prompt to the actual file
            await File.WriteAllTextAsync(promptFilePath, optimizedPrompt);
            
            // Save optimization record to training data store for history tracking
            var optimizationRecord = new PromptOptimizationRecord
            {
                Id = Guid.NewGuid().ToString(),
                PersonalityType = personalityType,
                PromptType = promptType,
                OriginalPrompt = "Previous version", // TODO: Load original for comparison
                OptimizedPrompt = optimizedPrompt,
                PerformanceScore = performanceScore,
                Timestamp = DateTime.UtcNow,
                TrainingSessionId = trainingSessionId,
                QualityMetrics = new Dictionary<string, double>
                {
                    { "PerformanceScore", performanceScore }
                }
            };
            
            var historyKey = GetPromptHistoryKey(personalityType, promptType, optimizationRecord.Id);
            await _dataStore.SaveAsync(historyKey, optimizationRecord);
            
            // ARCHITECTURAL PRINCIPLE: Clear prompt cache so AiPersonalityFactory reloads from disk
            AiPersonalityFactory.ClearPromptCache();
            
            _logger.LogInformation("Saved optimized {PromptType} prompt for {PersonalityType} to {FilePath} with score {Score:F3}", 
                promptType, personalityType, promptFilePath, performanceScore);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Failed to save optimized prompt for {personalityType}/{promptType}. " +
                $"Training requires write access to PosKernel.AI/Prompts/ directory structure. " +
                $"Error: {ex.Message}");
        }
    }

    public IReadOnlyList<string> GetAvailablePromptTypes(PersonalityType personalityType)
    {
        try
        {
            // ARCHITECTURAL PRINCIPLE: Discover prompt types from AiPersonalityFactory, don't hardcode them
            var promptDirectory = Path.Combine("PosKernel.AI", "Prompts", personalityType.ToString());
            
            if (!Directory.Exists(promptDirectory))
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Prompt directory not found for personality {personalityType}. " +
                    $"Expected directory: {promptDirectory}. " +
                    $"Ensure AiPersonalityFactory prompt files are properly deployed.");
            }

            // Discover available prompt types from .md files in the personality directory
            var promptFiles = Directory.GetFiles(promptDirectory, "*.md");
            var promptTypes = promptFiles
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrEmpty(name))
                .Cast<string>()
                .ToList();

            if (!promptTypes.Any())
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: No prompt files found for personality {personalityType}. " +
                    $"Expected .md files in directory: {promptDirectory}. " +
                    $"Ensure personality prompt files are properly deployed.");
            }

            _logger.LogDebug("Discovered {Count} prompt types for {PersonalityType}: {Types}", 
                promptTypes.Count, personalityType, string.Join(", ", promptTypes));
            
            return promptTypes;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Failed to discover prompt types for {personalityType}. " +
                $"Cannot enumerate prompt files from AiPersonalityFactory directory structure. " +
                $"Error: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<PromptOptimizationRecord>> GetPromptHistoryAsync(PersonalityType personalityType, string promptType, int limit = 10)
    {
        try
        {
            var historyPattern = GetPromptHistoryKeyPattern(personalityType, promptType);
            var historyKeys = await _dataStore.ListKeysAsync(historyPattern);
            
            var records = new List<PromptOptimizationRecord>();
            
            // Get records in parallel for better performance
            var recordResults = historyKeys.Take(limit).Select( key =>
            {
                return _dataStore.Load<PromptOptimizationRecord>(key);
            });

            // var recordResults = await Task.WhenAll(recordTasks);
            
            records.AddRange(recordResults.Where(r => r != null)!);
            
            // Sort by timestamp descending (newest first)
            records.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

            _logger.LogDebug("Retrieved {Count} prompt optimization records for {PersonalityType}/{PromptType}", 
                records.Count, personalityType, promptType);
            
            return records.Take(limit).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Failed to get prompt history for {personalityType}/{promptType}. " +
                $"Check storage accessibility. Error: {ex.Message}");
        }
    }

    public Task<PromptTestResult> TestPromptVariantAsync(PersonalityType personalityType, string promptType, string promptVariant, string testScenario)
    {
        // ARCHITECTURAL PRINCIPLE: FAIL FAST when core functionality not implemented
        throw new InvalidOperationException(
            "DESIGN DEFICIENCY: Prompt testing against production system not implemented. " +
            "Training system requires real prompt testing to validate optimizations. " +
            "Implement TestPromptVariantAsync with:\n" +
            "1. Integration with ChatOrchestrator to test prompts in production context\n" +
            "2. Real scenario execution against actual AI models\n" +
            "3. Quality metrics calculation based on actual responses\n" +
            "4. Performance measurement of response times\n" +
            "Without this implementation, training cannot validate prompt improvements.");
    }

    private static string GetPromptHistoryKey(PersonalityType personalityType, string promptType, string recordId) 
        => $"{PROMPT_HISTORY_KEY_PREFIX}-{personalityType}-{promptType}-{recordId}";

    private static string GetPromptHistoryKeyPattern(PersonalityType personalityType, string promptType) 
        => $"{PROMPT_HISTORY_KEY_PREFIX}-{personalityType}-{promptType}*";
}
