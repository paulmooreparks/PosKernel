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
using PosKernel.Configuration;

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
            _logger.LogDebug("Loading prompt for {PersonalityType}/{PromptType}", 
                personalityType, promptType);

            // ARCHITECTURAL PRINCIPLE: AI personalities handle time and cultural context naturally
            // No need to provide time context - AI can access current time and apply cultural intelligence
            var prompt = AiPersonalityFactory.BuildPrompt(personalityType, promptType, context);
            
            if (string.IsNullOrEmpty(prompt))
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: AiPersonalityFactory returned empty prompt for {personalityType}/{promptType}. " +
                    $"Check prompt file exists and is properly formatted in ~/.poskernel/ai_config/prompts/{personalityType}/{promptType}.md");
            }

            _logger.LogDebug("Retrieved {PromptType} prompt for {PersonalityType}: {Length} characters", 
                promptType, personalityType, prompt.Length);
            
            return Task.FromResult(prompt);
        }
        catch (Exception ex)
        {
            var errorMessage = $"DESIGN DEFICIENCY: Failed to get current prompt for {personalityType}/{promptType}. " +
                              $"Training system requires access to existing prompt files via AiPersonalityFactory. " +
                              $"Error: {ex.Message}";
            
            _logger.LogError(ex, errorMessage);
            throw new InvalidOperationException(errorMessage, ex);
        }
    }

    public async Task SaveOptimizedPromptAsync(PersonalityType personalityType, string promptType, string optimizedPrompt, double performanceScore, string trainingSessionId)
    {
        await SaveOptimizedPromptAsync(personalityType, promptType, optimizedPrompt, performanceScore, trainingSessionId, "No change tracking available");
    }

    /// <summary>
    /// Saves optimized prompt with change tracking information
    /// TRAINING ENHANCEMENT: Track what changes were actually made
    /// </summary>
    public async Task SaveOptimizedPromptAsync(PersonalityType personalityType, string promptType, string optimizedPrompt, double performanceScore, string trainingSessionId, string changesSummary)
    {
        try
        {
            _logger.LogInformation("SAVING optimized prompt for {PersonalityType}/{PromptType} with score {Score:F3} from session {SessionId}", 
                personalityType, promptType, performanceScore, trainingSessionId);

            // ARCHITECTURAL FIX: Actually save to the production prompt files that the cashier system reads
            await SavePromptToProductionFileAsync(personalityType, promptType, optimizedPrompt);

            // Save to optimization history for analysis with change tracking
            await SaveOptimizationRecordAsync(new PromptOptimizationRecord
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
                    { "PerformanceScore", performanceScore },
                    { "PromptLength", optimizedPrompt.Length },
                    { "OptimizationTimestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                    { "UniqueContentHash", GetPromptContentHash(optimizedPrompt) }, // TRAINING FIX: Add content hash for uniqueness verification
                    { "EnhancementMarkers", CountEnhancementMarkers(optimizedPrompt) } // TRAINING FIX: Count specific enhancement indicators
                },
                Notes = $"Training session: {trainingSessionId}, Content length: {optimizedPrompt.Length}, " +
                       $"Hash: {GetPromptContentHash(optimizedPrompt):F0}, " +
                       $"Enhancement markers: {CountEnhancementMarkers(optimizedPrompt)}, " +
                       $"Changes: {changesSummary}" // TRAINING ENHANCEMENT: Include actual changes made
            });

            // ARCHITECTURAL PRINCIPLE: Clear prompt cache so AiPersonalityFactory reloads from disk
            AiPersonalityFactory.ClearPromptCache();
            
            _logger.LogInformation("PRODUCTION PROMPT UPDATED: {PersonalityType}/{PromptType} prompt file saved and cache cleared", 
                personalityType, promptType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save optimized prompt for {PersonalityType}/{PromptType}: {Error}", 
                personalityType, promptType, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Normalizes a model name to be safe for use in file paths.
    /// Replaces characters that are invalid in Windows/Unix paths with safe alternatives.
    /// </summary>
    /// <param name="modelName">The model name to normalize</param>
    /// <returns>A path-safe model name</returns>
    private static string NormalizeModelNameForPath(string modelName)
    {
        if (string.IsNullOrEmpty(modelName))
        {
            return modelName;
        }

        // ARCHITECTURAL FIX: Use Path.GetInvalidFileNameChars() to handle all invalid characters properly
        // This covers both path and filename restrictions across Windows/Unix systems
        var invalidChars = Path.GetInvalidFileNameChars();
        var normalizedName = new string(modelName.Select(c => invalidChars.Contains(c) ? '-' : c).ToArray());
        
        return normalizedName;
    }

    /// <summary>
    /// Saves the optimized prompt to the actual production file that the cashier system reads
    /// ARCHITECTURAL PRINCIPLE: Training must update the SAME files that production AI uses
    /// ARCHITECTURAL FIX: Training should ALWAYS save to STORE configuration paths (where cashier reads)
    /// </summary>
    private async Task SavePromptToProductionFileAsync(PersonalityType personalityType, string promptType, string optimizedPrompt)
    {
        // ARCHITECTURAL FIX: Use standard configuration directory resolution
        var posKernelHome = Environment.GetEnvironmentVariable("POSKERNEL_HOME") 
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".poskernel");

        var aiConfigPath = Path.Combine(posKernelHome, "ai_config");
        var promptsPath = Path.Combine(aiConfigPath, "prompts");

        // ARCHITECTURAL BUG FIX: Training should ALWAYS use STORE configuration for prompt file paths
        // The cashier uses STORE_AI_PROVIDER/STORE_AI_MODEL, so optimized prompts must go there
        var config = PosKernelConfiguration.Initialize();
        
        // Always use STORE configuration - this is where the cashier will read from
        var provider = config.GetValue<string>("STORE_AI_PROVIDER");
        var model = config.GetValue<string>("STORE_AI_MODEL");
        
        if (string.IsNullOrEmpty(provider))
        {
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Store AI provider not configured. " +
                "Training system optimizes prompts for the store cashier system. " +
                "Set STORE_AI_PROVIDER in ~/.poskernel/.env file. " +
                "Training cannot optimize prompts without knowing the target store configuration.");
        }
        
        if (string.IsNullOrEmpty(model))
        {
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Store AI model not configured. " +
                "Training system optimizes prompts for the store cashier system. " +
                "Set STORE_AI_MODEL in ~/.poskernel/.env file. " +
                "Training cannot optimize prompts without knowing the target store configuration.");
        }

        // ARCHITECTURAL FIX: Normalize model name for safe path usage
        var normalizedModel = NormalizeModelNameForPath(model);
        
        _logger.LogInformation("📂 Saving optimized prompts to STORE configuration: {Provider}/{Model} (normalized: {NormalizedModel})", 
            provider, model, normalizedModel);

        // ARCHITECTURAL PRINCIPLE: Use hierarchical path structure like AiPersonalityFactory
        // Use normalized model name for path safety
        var providerModelPath = Path.Combine(promptsPath, provider, normalizedModel, personalityType.ToString(), $"{promptType}.md");
        var basePath = Path.Combine(promptsPath, "Base", personalityType.ToString(), $"{promptType}.md");

        string actualFilePath;
        bool isProviderSpecific = false;

        // Check if provider-specific file exists, or if we should create it
        if (File.Exists(providerModelPath))
        {
            actualFilePath = providerModelPath;
            isProviderSpecific = true;
            _logger.LogInformation("📂 Found existing provider-specific prompt file: {FilePath}", actualFilePath);
        }
        else if (File.Exists(basePath))
        {
            // Base file exists - decide whether to upgrade to provider-specific or update base
            // For training, we want to create provider-specific files to avoid affecting other providers
            actualFilePath = providerModelPath;
            isProviderSpecific = true;
            _logger.LogInformation("📂 Creating provider-specific prompt file: {FilePath}", actualFilePath);
        }
        else
        {
            // Neither exists - create provider-specific file
            actualFilePath = providerModelPath;
            isProviderSpecific = true;
            _logger.LogInformation("📂 Creating new provider-specific prompt file: {FilePath}", actualFilePath);
        }

        // Ensure directory structure exists
        var directory = Path.GetDirectoryName(actualFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogInformation("📁 Created prompt directory: {Directory}", directory);
        }

        // Backup the existing file before overwriting
        if (File.Exists(actualFilePath))
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var backupPath = $"{actualFilePath}.backup.{timestamp}";
            
            // ARCHITECTURAL FIX: Check if backup already exists to avoid the error we saw
            int backupCounter = 1;
            while (File.Exists(backupPath))
            {
                backupPath = $"{actualFilePath}.backup.{timestamp}-{backupCounter}";
                backupCounter++;
            }
            
            File.Copy(actualFilePath, backupPath);
            _logger.LogInformation("💾 Backed up existing prompt to: {BackupPath}", backupPath);
        }

        // Save the optimized prompt
        await File.WriteAllTextAsync(actualFilePath, optimizedPrompt);
        _logger.LogInformation("✅ CASHIER PROMPT UPDATED: Saved optimized prompt to {FilePath} (provider-specific: {IsProviderSpecific})", 
            actualFilePath, isProviderSpecific);
        
        // Verify the file was written correctly
        var writtenContent = await File.ReadAllTextAsync(actualFilePath);
        if (writtenContent != optimizedPrompt)
        {
            throw new InvalidOperationException($"DESIGN DEFICIENCY: Prompt file verification failed. Written content does not match optimized prompt.");
        }
        
        _logger.LogInformation("✅ VERIFICATION PASSED: Prompt file contains correct optimized content ({Length} characters)", writtenContent.Length);
    }

    /// <summary>
    /// Saves an optimization record to the history file
    /// </summary>
    private async Task SaveOptimizationRecordAsync(PromptOptimizationRecord record)
    {
        var historyKey = GetPromptHistoryKey(record.PersonalityType, record.PromptType, record.Id);
        await _dataStore.SaveAsync(historyKey, record);
    }

    public IReadOnlyList<string> GetAvailablePromptTypes(PersonalityType personalityType)
    {
        try
        {
            // ARCHITECTURAL PRINCIPLE: Use standard configuration directory resolution
            var posKernelHome = Environment.GetEnvironmentVariable("POSKERNEL_HOME") 
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".poskernel");

            var promptsPath = Path.Combine(posKernelHome, "ai_config", "prompts");
            
            // ARCHITECTURAL BUG FIX: Training should ALWAYS use STORE configuration for prompt file paths
            // The cashier uses STORE_AI_PROVIDER/STORE_AI_MODEL, so we must discover from those paths
            var config = PosKernelConfiguration.Initialize();
            
            // Always use STORE configuration - this is where the cashier reads from
            var provider = config.GetValue<string>("STORE_AI_PROVIDER");
            var model = config.GetValue<string>("STORE_AI_MODEL");
            
            if (string.IsNullOrEmpty(provider))
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: Store AI provider not configured. " +
                    "Training system needs to discover prompts from store cashier system. " +
                    "Set STORE_AI_PROVIDER in ~/.poskernel/.env file. " +
                    "Training cannot discover prompts without knowing the store configuration.");
            }
            
            if (string.IsNullOrEmpty(model))
            {
                throw new InvalidOperationException(
                    "DESIGN DEFICIENCY: Store AI model not configured. " +
                    "Training system needs to discover prompts from store cashier system. " +
                    "Set STORE_AI_MODEL in ~/.poskernel/.env file. " +
                    "Training cannot discover prompts without knowing the store configuration.");
            }

            // ARCHITECTURAL FIX: Normalize model name for safe path usage
            var normalizedModel = NormalizeModelNameForPath(model);

            // Check both provider-specific and base directories
            var providerModelDirectory = Path.Combine(promptsPath, provider, normalizedModel, personalityType.ToString());
            var baseDirectory = Path.Combine(promptsPath, "Base", personalityType.ToString());

            var promptTypes = new HashSet<string>();

            // Collect from provider-specific directory
            if (Directory.Exists(providerModelDirectory))
            {
                var providerFiles = Directory.GetFiles(providerModelDirectory, "*.md");
                foreach (var file in providerFiles)
                {
                    var promptType = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrEmpty(promptType))
                    {
                        promptTypes.Add(promptType);
                    }
                }
            }

            // Collect from base directory
            if (Directory.Exists(baseDirectory))
            {
                var baseFiles = Directory.GetFiles(baseDirectory, "*.md");
                foreach (var file in baseFiles)
                {
                    var promptType = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrEmpty(promptType))
                    {
                        promptTypes.Add(promptType);
                    }
                }
            }

            if (!promptTypes.Any())
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: No prompt files found for personality {personalityType}. " +
                    $"Checked directories:\n" +
                    $"  - Provider-specific: {providerModelDirectory}\n" +
                    $"  - Base: {baseDirectory}\n" +
                    $"Ensure personality prompt files are properly deployed.");
            }

            var result = promptTypes.Cast<string>().ToList();

            _logger.LogDebug("Discovered {Count} prompt types for {PersonalityType}: {Types}", 
                result.Count, personalityType, string.Join(", ", result));
            
            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Failed to discover prompt types for {personalityType}. " +
                $"Cannot enumerate prompt files from ~/.poskernel/ai_config/prompts/ directory structure. " +
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

    /// <summary>
    /// Generates a hash of the prompt content to detect meaningful changes
    /// TRAINING PRINCIPLE: Helps distinguish between truly different prompt variations
    /// </summary>
    private static double GetPromptContentHash(string prompt)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            return 0;
        }

        // Simple hash based on content characteristics that indicate real differences
        var hash = prompt.Length * 37;
        hash += prompt.Count(c => c == '#') * 101; // Section markers
        hash += prompt.Count(c => c == '*') * 67;  // Emphasis markers  
        hash += prompt.Count(c => c == ':') * 53;  // Definition markers
        hash += prompt.Count(c => c == '-') * 41;  // List markers
        
        // Add hash contribution from specific training keywords that indicate enhancements
        var trainingKeywords = new[] { "confidence", "tool", "execute", "conversation", "cultural", "payment" };
        foreach (var keyword in trainingKeywords)
        {
            var keywordCount = prompt.Split(keyword, StringSplitOptions.None).Length - 1;
            hash += keywordCount * keyword.Length * 29;
        }
        
        return hash % 1000000; // Keep it reasonable for display
    }

    /// <summary>
    /// Counts enhancement markers in the prompt to detect training improvements
    /// TRAINING PRINCIPLE: More enhancement markers indicate more sophisticated prompts
    /// </summary>
    private static double CountEnhancementMarkers(string prompt)
    {
        if (string.IsNullOrEmpty(prompt))
        {
            return 0;
        }

        var enhancementMarkers = new[]
        {
            "confidence", "execute", "immediately", "confirm", "specific", "natural",
            "cultural", "authentic", "systematic", "workflow", "validation", "error",
            "recovery", "context", "awareness", "timing", "flow", "clarity"
        };

        return enhancementMarkers.Sum(marker => 
        {
            // Use case-insensitive comparison manually since StringSplitOptions doesn't have OrdinalIgnoreCase
            var lowerPrompt = prompt.ToLowerInvariant();
            var lowerMarker = marker.ToLowerInvariant();
            return lowerPrompt.Split(lowerMarker, StringSplitOptions.None).Length - 1;
        });
    }
}
