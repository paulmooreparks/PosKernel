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
using PosKernel.AI.Core;
using PosKernel.AI.Models;
using PosKernel.AI.Services;
using PosKernel.AI.Training.Configuration;
using PosKernel.AI.Training.Scenarios;
using PosKernel.Abstractions;
using System.Text.Json;

namespace PosKernel.AI.Training.Core;

/// <summary>
/// Transaction-centric AI training engine that implements iterative prompt refinement.
/// ARCHITECTURAL PRINCIPLE: Each training iteration focuses on complete transaction lifecycles.
/// </summary>
public class TransactionTrainingEngine : ITrainingSession
{
    private readonly ChatOrchestrator _chatOrchestrator;
    private readonly IPromptManagementService _promptService;
    private readonly ILogger<TransactionTrainingEngine> _logger;
    private readonly PersonalityType _personalityType;
    private readonly object _stateLock = new();
    
    private TrainingSessionState _state = TrainingSessionState.NotStarted;
    private TrainingStatistics _currentStats = new();
    private DateTime _startTime;
    private string _sessionId = string.Empty;
    private CancellationTokenSource? _cancellationTokenSource;

    // Training state
    private List<TransactionScenario> _trainingScenarios = new();
    private Dictionary<string, string> _currentPrompts = new(); // promptType -> content
    private List<TransactionTrainingResult> _trainingResults = new();

    public event EventHandler<TrainingProgressEventArgs>? ProgressUpdated;
    public event EventHandler<ScenarioTestEventArgs>? ScenarioTested;
    public event EventHandler<GenerationCompleteEventArgs>? GenerationComplete;
    public event EventHandler<TrainingCompleteEventArgs>? TrainingComplete;

    public TrainingSessionState State 
    { 
        get 
        { 
            lock (_stateLock) 
            { 
                return _state; 
            } 
        } 
    }

    public TrainingStatistics CurrentStats 
    { 
        get 
        { 
            lock (_stateLock) 
            { 
                return _currentStats; 
            } 
        } 
    }

    public TransactionTrainingEngine(
        ChatOrchestrator chatOrchestrator,
        IPromptManagementService promptService,
        PersonalityType personalityType,
        ILogger<TransactionTrainingEngine> logger)
    {
        _chatOrchestrator = chatOrchestrator ?? throw new ArgumentNullException(nameof(chatOrchestrator));
        _promptService = promptService ?? throw new ArgumentNullException(nameof(promptService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _personalityType = personalityType;
    }

    public async Task<TrainingResults> RunTrainingSessionAsync(TrainingConfiguration config, CancellationToken cancellationToken = default)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        lock (_stateLock)
        {
            if (_state != TrainingSessionState.NotStarted)
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Training session already started or completed. " +
                    $"Current state: {_state}. Create new session instance for each training run.");
            }
            
            _state = TrainingSessionState.Initializing;
            _sessionId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            _startTime = DateTime.UtcNow;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        _logger.LogInformation("Starting transaction-centric AI training session {SessionId}", _sessionId);

        try
        {
            return await RunTransactionTrainingLoopAsync(config, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await HandleAbortAsync("Training cancelled by user");
            throw;
        }
        catch (Exception ex)
        {
            await HandleFailureAsync($"Transaction training failed: {ex.Message}");
            throw;
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// Core training loop implementing the transaction-focused feedback cycle:
    /// 1. Create transaction scenarios
    /// 2. Define expected results  
    /// 3. Run scenarios against current prompts
    /// 4. Compare results to expectations
    /// 5. Refine prompts based on failures
    /// 6. Write updated prompts and reload
    /// 7. Repeat
    /// </summary>
    private async Task<TrainingResults> RunTransactionTrainingLoopAsync(TrainingConfiguration config, CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            _state = TrainingSessionState.Running;
            _currentStats = new TrainingStatistics
            {
                CurrentGeneration = 0,
                TotalGenerations = config.MaxGenerations,
                ScenariosCompleted = 0,
                TotalScenarios = config.ScenarioCount,
                CurrentScore = 0.0,
                BestScore = 0.0,
                Improvement = 0.0,
                ElapsedTime = TimeSpan.Zero,
                EstimatedRemaining = TimeSpan.FromMinutes(config.MaxGenerations * 2),
                ConsecutiveFailures = 0,
                ConsecutiveImprovements = 0
            };
        }

        var bestScore = 0.0;
        var totalImprovement = 0.0;

        // STEP 1: Load current prompts as baseline
        await LoadCurrentPromptsAsync();
        
        _logger.LogInformation("Transaction training loop started - loaded {PromptCount} baseline prompts", 
            _currentPrompts.Count);

        // Main training generations
        for (var generation = 1; generation <= config.MaxGenerations; generation++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            _logger.LogInformation("Starting generation {Generation}/{MaxGenerations}", 
                generation, config.MaxGenerations);

            // STEP 2: Create transaction scenarios for this generation
            _trainingScenarios = await CreateTransactionScenariosAsync(config, generation);
            
            // STEP 3: Run all scenarios against current prompts
            var generationResults = new List<TransactionTrainingResult>();
            for (var scenarioIndex = 0; scenarioIndex < _trainingScenarios.Count; scenarioIndex++)
            {
                var scenario = _trainingScenarios[scenarioIndex];
                
                // STEP 4: Execute complete transaction scenario
                var result = await ExecuteTransactionScenarioAsync(scenario, generation, scenarioIndex + 1);
                generationResults.Add(result);
                
                // Fire event for UI updates
                var eventArgs = new ScenarioTestEventArgs
                {
                    Generation = generation,
                    ScenarioIndex = scenarioIndex + 1,
                    ScenarioDescription = scenario.Description,
                    UserInput = string.Join(" → ", scenario.Interactions.Select(i => i.CustomerInput)),
                    AIResponse = result.Summary,
                    Score = result.OverallScore,
                    Duration = result.ExecutionTime,
                    IsSuccessful = result.TransactionSuccessful,
                    QualityMetrics = result.QualityMetrics
                };
                
                ScenarioTested?.Invoke(this, eventArgs);
                
                await UpdateProgressAsync(generation, scenarioIndex + 1, config);
            }

            // STEP 5: Analyze results and calculate generation score
            var generationScore = generationResults.Average(r => r.OverallScore);
            var improvement = generationScore - bestScore;
            
            _logger.LogInformation("Generation {Generation} completed: Score={Score:F3}, Improvement={Improvement:F3}", 
                generation, generationScore, improvement);

            // STEP 6: If improved, refine prompts and save them
            if (generationScore > bestScore)
            {
                bestScore = generationScore;
                totalImprovement += improvement;
                
                _logger.LogInformation("🎯 NEW BEST SCORE in generation {Generation}: {Score:F3}", 
                    generation, bestScore);

                await RefineAndSavePromptsAsync(generationResults, config);
            }

            // Store generation results
            _trainingResults.AddRange(generationResults);
            
            // Fire generation complete event
            var generationCompleteArgs = new GenerationCompleteEventArgs
            {
                Generation = generation,
                Score = generationScore,
                Improvement = improvement,
                ScenariosEvaluated = generationResults.Count,
                Duration = TimeSpan.FromSeconds(generationResults.Sum(r => r.ExecutionTime.TotalSeconds)),
                IsImprovement = improvement > 0,
                IsNewBest = generationScore >= bestScore,
                PromptVariation = "Transaction-optimized prompts",
                QualityMetrics = CalculateGenerationQualityMetrics(generationResults)
            };
            
            GenerationComplete?.Invoke(this, generationCompleteArgs);

            // Check if we've reached improvement threshold
            if (totalImprovement >= config.ImprovementThreshold && generation >= 2)
            {
                _logger.LogInformation("🏆 Training threshold achieved in generation {Generation}. Total improvement: {TotalImprovement:F3}", 
                    generation, totalImprovement);
                break;
            }
        }

        // Complete the session
        var finalState = TrainingSessionState.Completed;
        var endTime = DateTime.UtcNow;
        
        lock (_stateLock)
        {
            _state = finalState;
        }

        var results = new TrainingResults
        {
            SessionId = _sessionId,
            StartTime = _startTime,
            EndTime = endTime,
            Configuration = config,
            FinalState = finalState,
            GenerationsCompleted = _trainingResults.GroupBy(r => r.Generation).Count(),
            FinalScore = _trainingResults.LastOrDefault()?.OverallScore ?? 0.0,
            BestScore = bestScore,
            TotalImprovement = totalImprovement,
            OptimizedPrompt = "Transaction-optimized prompts saved to files",
            GenerationHistory = new List<GenerationResult>(), // Convert from transaction results if needed
            Metrics = new Dictionary<string, object>
            {
                { "TransactionScenariosTotal", _trainingResults.Count },
                { "CompletedTransactions", _trainingResults.Count(r => r.TransactionSuccessful) },
                { "AverageTransactionTime", _trainingResults.Average(r => r.ExecutionTime.TotalSeconds) },
                { "PromptsOptimized", _currentPrompts.Count }
            }
        };

        // Fire completion event
        var completionArgs = new TrainingCompleteEventArgs
        {
            SessionId = _sessionId,
            FinalState = finalState,
            FinalScore = results.FinalScore,
            BestScore = bestScore,
            TotalImprovement = totalImprovement,
            GenerationsCompleted = results.GenerationsCompleted,
            TotalDuration = results.Duration,
            OptimizedPrompt = results.OptimizedPrompt,
            Metrics = results.Metrics
        };
        
        TrainingComplete?.Invoke(this, completionArgs);

        _logger.LogInformation("🎉 Transaction training session {SessionId} completed successfully in {Duration} with final score {FinalScore:F3}", 
            _sessionId, results.Duration, results.FinalScore);

        return results;
    }

    /// <summary>
    /// STEP 1: Load current prompt files as training baseline.
    /// </summary>
    private async Task LoadCurrentPromptsAsync()
    {
        try
        {
            var promptTypes = new[] { "ordering", "tool_acknowledgment", "greeting" };
            var context = new PromptContext
            {
                TimeOfDay = "afternoon",
                CurrentTime = DateTime.Now.ToString("HH:mm")
            };

            foreach (var promptType in promptTypes)
            {
                var prompt = await _promptService.GetCurrentPromptAsync(_personalityType, promptType, context);
                _currentPrompts[promptType] = prompt;
                
                _logger.LogDebug("Loaded {PromptType} prompt: {Length} characters", 
                    promptType, prompt.Length);
            }
            
            _logger.LogInformation("Loaded {Count} baseline prompts for personality {PersonalityType}", 
                _currentPrompts.Count, _personalityType);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Failed to load current prompts for training baseline. " +
                $"Training requires access to existing prompt files. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// STEP 2: Create transaction scenarios based on configuration and generation number.
    /// </summary>
    private Task<List<TransactionScenario>> CreateTransactionScenariosAsync(TrainingConfiguration config, int generation)
    {
        var scenarios = new List<TransactionScenario>();
        
        // Create scenarios based on configured distribution
        var scenarioDistribution = CalculateScenarioDistribution(config.ScenarioMix, config.ScenarioCount);
        
        // Generate simple order scenarios
        for (int i = 0; i < scenarioDistribution.SimpleOrders; i++)
        {
            scenarios.Add(CreateSimpleOrderScenarioAsync($"simple-order-gen{generation}-{i + 1}").Result);
        }
        
        // Generate multi-item scenarios
        for (int i = 0; i < scenarioDistribution.MultiItemOrders; i++)
        {
            scenarios.Add(CreateMultiItemOrderScenarioAsync($"multi-item-gen{generation}-{i + 1}").Result);
        }
        
        // Generate payment flow scenarios
        for (int i = 0; i < scenarioDistribution.PaymentFlows; i++)
        {
            scenarios.Add(CreatePaymentFlowScenarioAsync($"payment-flow-gen{generation}-{i + 1}").Result);
        }
        
        _logger.LogInformation("Created {Count} transaction scenarios for generation {Generation}", 
            scenarios.Count, generation);
        
        return Task.FromResult(scenarios);
    }

    // Placeholder methods - these need to be implemented based on your restaurant extension data
    private async Task<TransactionScenario> CreateSimpleOrderScenarioAsync(string scenarioId)
    {
        // ARCHITECTURAL PRINCIPLE: Get currency from ChatOrchestrator's store configuration
        if (_chatOrchestrator == null)
        {
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Training requires ChatOrchestrator to access store configuration. " +
                "Cannot determine currency, payment methods, or store context without ChatOrchestrator. " +
                "Ensure TransactionTrainingEngine is initialized with valid ChatOrchestrator.");
        }

        // For now, use SGD as default - this should come from the ChatOrchestrator's configuration
        // TODO: ChatOrchestrator should expose its StoreConfig for training to access
        var defaultCurrency = "SGD"; // ARCHITECTURAL DEBT: Should come from ChatOrchestrator.StoreConfig
        
        // ARCHITECTURAL FIX: Generate diverse customer interactions using training AI
        var customerInteractions = GenerateDiverseCustomerInteractions("simple_order");
        
        return new TransactionScenario
        {
            ScenarioId = scenarioId,
            Description = "Simple single-item order with completion",
            ScenarioType = TransactionScenarioType.SimpleOrder,
            Interactions = customerInteractions,
            ExpectedOutcome = new TransactionExpectedResult
            {
                FinalState = TransactionState.Completed,
                ExpectedItems = new List<ExpectedLineItem>
                {
                    new ExpectedLineItem { ProductIdentifier = "Coffee", Quantity = 1 }
                },
                ExpectedTotal = 1.40m, // TODO: Should come from restaurant extension service
                ExpectedCurrency = defaultCurrency, // ARCHITECTURAL DEBT: From ChatOrchestrator config
                ExpectedPaymentMethod = null, // ARCHITECTURAL FIX: Let payment service validate methods
                SuccessCriteria = new List<TransactionSuccessCriterion>
                {
                    new TransactionSuccessCriterion 
                    { 
                        CriterionType = SuccessCriterionType.CorrectFinalState, 
                        Description = "Transaction completed successfully",
                        IsCritical = true 
                    }
                }
            }
        };
    }

    private async Task<TransactionScenario> CreateMultiItemOrderScenarioAsync(string scenarioId)
    {
        // ARCHITECTURAL PRINCIPLE: Get currency from ChatOrchestrator's store configuration
        if (_chatOrchestrator == null)
        {
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Training requires ChatOrchestrator to access store configuration. " +
                "Cannot determine currency, payment methods, or store context without ChatOrchestrator. " +
                "Ensure TransactionTrainingEngine is initialized with valid ChatOrchestrator.");
        }

        // For now, use SGD as default - this should come from the ChatOrchestrator's configuration
        // TODO: ChatOrchestrator should expose its StoreConfig for training to access
        var defaultCurrency = "SGD"; // ARCHITECTURAL DEBT: Should come from ChatOrchestrator.StoreConfig
        
        // ARCHITECTURAL FIX: Generate diverse multi-item customer interactions
        var customerInteractions = GenerateDiverseCustomerInteractions("multi_item_order");
        
        return new TransactionScenario
        {
            ScenarioId = scenarioId,
            Description = "Multi-item order with modifications",
            ScenarioType = TransactionScenarioType.MultiItemOrder,
            Interactions = customerInteractions,
            ExpectedOutcome = new TransactionExpectedResult
            {
                FinalState = TransactionState.Completed,
                ExpectedItems = new List<ExpectedLineItem>
                {
                    new ExpectedLineItem { ProductIdentifier = "Coffee", Quantity = 1 },
                    new ExpectedLineItem { ProductIdentifier = "Toast", Quantity = 2 }
                },
                ExpectedTotal = 4.80m, // TODO: Should come from restaurant extension service
                ExpectedCurrency = defaultCurrency, // ARCHITECTURAL DEBT: From ChatOrchestrator config
                ExpectedPaymentMethod = null, // ARCHITECTURAL FIX: Let payment service validate methods
                SuccessCriteria = new List<TransactionSuccessCriterion>
                {
                    new TransactionSuccessCriterion 
                    { 
                        CriterionType = SuccessCriterionType.CorrectFinalState, 
                        Description = "Multi-item transaction completed successfully",
                        IsCritical = true 
                    },
                    new TransactionSuccessCriterion
                    {
                        CriterionType = SuccessCriterionType.CorrectItemCount,
                        Description = "All items added correctly with proper quantities",
                        IsCritical = true
                    }
                }
            }
        };
    }

    private async Task<TransactionScenario> CreatePaymentFlowScenarioAsync(string scenarioId)
    {
        // ARCHITECTURAL PRINCIPLE: Get currency from ChatOrchestrator's store configuration
        if (_chatOrchestrator == null)
        {
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Training requires ChatOrchestrator to access store configuration. " +
                "Cannot determine currency, payment methods, or store context without ChatOrchestrator. " +
                "Ensure TransactionTrainingEngine is initialized with valid ChatOrchestrator.");
        }

        // For now, use SGD as default - this should come from the ChatOrchestrator's configuration
        // TODO: ChatOrchestrator should expose its StoreConfig for training to access
        var defaultCurrency = "SGD"; // ARCHITECTURAL DEBT: Should come from ChatOrchestrator.StoreConfig
        
        // ARCHITECTURAL FIX: Generate diverse payment flow interactions
        var customerInteractions = GenerateDiverseCustomerInteractions("payment_flow");
        
        return new TransactionScenario
        {
            ScenarioId = scenarioId,
            Description = "Payment method selection and processing flow",
            ScenarioType = TransactionScenarioType.PaymentFlowTesting,
            Interactions = customerInteractions,
            ExpectedOutcome = new TransactionExpectedResult
            {
                FinalState = TransactionState.Completed,
                ExpectedItems = new List<ExpectedLineItem>
                {
                    new ExpectedLineItem { ProductIdentifier = "Beverage", Quantity = 1 }
                },
                ExpectedTotal = 1.80m, // TODO: Should come from restaurant extension service
                ExpectedCurrency = defaultCurrency, // ARCHITECTURAL DEBT: From ChatOrchestrator config
                ExpectedPaymentMethod = null, // ARCHITECTURAL FIX: Let payment service validate methods
                SuccessCriteria = new List<TransactionSuccessCriterion>
                {
                    new TransactionSuccessCriterion 
                    { 
                        CriterionType = SuccessCriterionType.CorrectFinalState, 
                        Description = "Payment flow completed successfully",
                        IsCritical = true 
                    },
                    new TransactionSuccessCriterion
                    {
                        CriterionType = SuccessCriterionType.PaymentMethodsLoaded,
                        Description = "Payment methods context loaded when customer asked",
                        IsCritical = true
                    }
                }
            }
        };
    }

    private (int SimpleOrders, int MultiItemOrders, int PaymentFlows) CalculateScenarioDistribution(
        ScenarioDistribution mix, int totalScenarios)
    {
        return (
            SimpleOrders: (int)Math.Ceiling(totalScenarios * mix.BasicOrdering),
            MultiItemOrders: (int)Math.Ceiling(totalScenarios * mix.EdgeCases),
            PaymentFlows: (int)Math.Ceiling(totalScenarios * mix.PaymentScenarios)
        );
    }

    /// <summary>
    /// STEP 3: Execute a complete transaction scenario against the current AI.
    /// </summary>
    private async Task<TransactionTrainingResult> ExecuteTransactionScenarioAsync(
        TransactionScenario scenario, int generation, int scenarioIndex)
    {
        var startTime = DateTime.UtcNow;
        var result = new TransactionTrainingResult
        {
            ScenarioId = scenario.ScenarioId,
            Generation = generation,
            ScenarioIndex = scenarioIndex,
            StartTime = startTime
        };

        try
        {
            _logger.LogDebug("Executing transaction scenario {ScenarioId}: {Description}", 
                scenario.ScenarioId, scenario.Description);

            // Initialize fresh chat orchestrator for this scenario
            await _chatOrchestrator.InitializeAsync();

            var interactionResults = new List<InteractionResult>();

            // Execute each customer interaction in sequence
            foreach (var interaction in scenario.Interactions)
            {
                var interactionResult = await ExecuteCustomerInteractionAsync(interaction);
                interactionResults.Add(interactionResult);
                
                // Stop if interaction failed critically
                if (!interactionResult.Success && interaction.ExpectedToolCalls.Any(tc => tc.IsRequired))
                {
                    _logger.LogWarning("Critical interaction failed in scenario {ScenarioId}: {Input}", 
                        scenario.ScenarioId, interaction.CustomerInput);
                    break;
                }
            }

            // STEP 4: Compare results to expectations
            result = await EvaluateTransactionResultAsync(scenario, interactionResults, result);
            
            result.ExecutionTime = DateTime.UtcNow - startTime;
            result.TransactionSuccessful = result.OverallScore > 0.6; // Configurable threshold
            
            _logger.LogDebug("Transaction scenario {ScenarioId} completed: Score={Score:F3}, Success={Success}", 
                scenario.ScenarioId, result.OverallScore, result.TransactionSuccessful);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute transaction scenario {ScenarioId}: {Error}", 
                scenario.ScenarioId, ex.Message);
            
            result.ExecutionTime = DateTime.UtcNow - startTime;
            result.TransactionSuccessful = false;
            result.OverallScore = 0.0;
            result.ErrorMessage = ex.Message;
            
            return result;
        }
    }

    private async Task<InteractionResult> ExecuteCustomerInteractionAsync(CustomerInteraction interaction)
    {
        try
        {
            var response = await _chatOrchestrator.ProcessUserInputAsync(interaction.CustomerInput);
            
            return new InteractionResult
            {
                CustomerInput = interaction.CustomerInput,
                AIResponse = response.Content ?? "",
                Success = !string.IsNullOrEmpty(response.Content),
                ExecutionTime = TimeSpan.FromMilliseconds(100) // TODO: Measure actual time
            };
        }
        catch (Exception ex)
        {
            return new InteractionResult
            {
                CustomerInput = interaction.CustomerInput,
                AIResponse = $"ERROR: {ex.Message}",
                Success = false,
                ExecutionTime = TimeSpan.FromSeconds(1),
                Error = ex
            };
        }
    }

    /// <summary>
    /// STEP 4: Evaluate transaction results against expected outcomes.
    /// </summary>
    private async Task<TransactionTrainingResult> EvaluateTransactionResultAsync(
        TransactionScenario scenario, 
        List<InteractionResult> interactionResults, 
        TransactionTrainingResult result)
    {
        var successfulInteractions = 0;
        var totalInteractions = interactionResults.Count;
        var itemMatchingScore = 1.0;
        var toolCallAccuracy = 1.0;
        var conversationFlowScore = 1.0;
        
        // Detailed interaction evaluation
        for (int i = 0; i < interactionResults.Count && i < scenario.Interactions.Count; i++)
        {
            var actualResult = interactionResults[i];
            var expectedInteraction = scenario.Interactions[i];
            
            // Basic success check
            if (actualResult.Success)
            {
                successfulInteractions++;
            }
            
            // ARCHITECTURAL FIX: Check for item matching accuracy
            if (expectedInteraction.ExpectedToolCalls.Any(tc => tc.ToolName == "add_item_to_transaction"))
            {
                var expectedItem = expectedInteraction.ExpectedToolCalls
                    .First(tc => tc.ToolName == "add_item_to_transaction")
                    .ExpectedParameters.GetValueOrDefault("item_description")?.ToString();
                    
                // Check if AI response mentions wrong item
                if (expectedItem != null)
                {
                    // Specific case: roti kaya should not result in Roti John
                    if (actualResult.CustomerInput.ToLower().Contains("roti kaya") && 
                        actualResult.AIResponse.ToLower().Contains("roti john"))
                    {
                        itemMatchingScore *= 0.1; // Severe penalty for wrong item
                        _logger.LogWarning("Item mismatch detected: Customer said 'roti kaya' but AI processed 'Roti John'");
                    }
                    
                    // General item matching check - if expected "Kaya Toast" but response mentions "John"
                    if (expectedItem.Equals("Kaya Toast", StringComparison.OrdinalIgnoreCase) &&
                        actualResult.AIResponse.ToLower().Contains("john"))
                    {
                        itemMatchingScore *= 0.2; // Major penalty for wrong item type
                        _logger.LogWarning("Expected Kaya Toast but AI response mentioned John-type product");
                    }
                }
            }
            
            // Tool call accuracy evaluation (simplified - would need actual tool call results)
            if (expectedInteraction.ExpectedToolCalls.Any() && !actualResult.Success)
            {
                toolCallAccuracy *= 0.7; // Penalty for failed tool calls
            }
            
            // Conversation flow evaluation
            if (actualResult.Error != null)
            {
                conversationFlowScore *= 0.8; // Penalty for errors
            }
        }
        
        // Calculate overall score with weighted components
        var baseScore = totalInteractions > 0 ? (double)successfulInteractions / totalInteractions : 0.0;
        result.OverallScore = (baseScore * 0.4) + (itemMatchingScore * 0.3) + (toolCallAccuracy * 0.2) + (conversationFlowScore * 0.1);
        
        result.QualityMetrics = new Dictionary<string, double>
        {
            { "InteractionSuccess", baseScore },
            { "ItemMatchingAccuracy", itemMatchingScore },
            { "ToolCallAccuracy", toolCallAccuracy },
            { "ConversationFlowCorrectness", conversationFlowScore },
            { "PersonalityConsistency", 0.8 } // TODO: Implement personality scoring
        };
        
        result.Summary = $"Completed {successfulInteractions}/{totalInteractions} interactions successfully. " +
                        $"Item matching: {itemMatchingScore:F2}, Tool calls: {toolCallAccuracy:F2}";
        
        // Mark transaction as failed if critical issues detected
        if (itemMatchingScore < 0.5 || toolCallAccuracy < 0.3)
        {
            result.TransactionSuccessful = false;
            _logger.LogWarning("Transaction marked as failed due to critical issues: ItemMatching={ItemMatchingScore:F2}, ToolCalls={ToolCallAccuracy:F2}",
                itemMatchingScore, toolCallAccuracy);
        }
        
        await Task.CompletedTask;
        return result;
    }

    /// <summary>
    /// STEP 5: Refine prompts based on training results and save them.
    /// </summary>
    private async Task RefineAndSavePromptsAsync(List<TransactionTrainingResult> results, TrainingConfiguration config)
    {
        _logger.LogInformation("Refining prompts based on {ResultCount} transaction results", results.Count);

        try
        {
            // Analyze failures to determine what needs improvement
            var failures = results.Where(r => !r.TransactionSuccessful).ToList();
            
            foreach (var promptType in _currentPrompts.Keys.ToList())
            {
                var refinedPrompt = await RefinePromptBasedOnFailuresAsync(
                    _currentPrompts[promptType], promptType, failures);
                
                if (refinedPrompt != _currentPrompts[promptType])
                {
                    _currentPrompts[promptType] = refinedPrompt;
                    
                    // STEP 6: Save refined prompt back to the actual prompt file
                    await SavePromptToFileAsync(_personalityType, promptType, refinedPrompt);
                    
                    _logger.LogInformation("Refined and saved {PromptType} prompt based on transaction results", 
                        promptType);
                }
            }
            
            // STEP 7: Reload prompts for next iteration
            _chatOrchestrator.ReloadPrompts();
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refine and save prompts: {Error}", ex.Message);
            throw;
        }
    }

    private Task<string> RefinePromptBasedOnFailuresAsync(
        string currentPrompt, string promptType, List<TransactionTrainingResult> failures)
    {
        // ARCHITECTURAL PRINCIPLE: Analyze failures and modify prompts accordingly
        if (!failures.Any())
        {
            return Task.FromResult(currentPrompt);
        }

        var refinedPrompt = currentPrompt;
        
        // STEP 1: Failure Pattern Analysis
        var patterns = AnalyzeFailurePatterns(failures);
        
        // STEP 2: Apply prompt modifications based on failure patterns
        if (patterns.HasToolCallErrors)
        {
            refinedPrompt = AddToolCallGuidance(refinedPrompt, patterns.ToolCallErrorExamples);
        }
        
        if (patterns.HasConversationFlowIssues)
        {
            refinedPrompt = AddConversationFlowGuidance(refinedPrompt, patterns.ConversationFlowExamples);
        }
        
        if (patterns.HasItemMismatchErrors)
        {
            refinedPrompt = AddItemMatchingGuidance(refinedPrompt, patterns.ItemMismatchExamples);
        }
        
        if (patterns.HasPaymentFlowErrors)
        {
            refinedPrompt = AddPaymentFlowGuidance(refinedPrompt, patterns.PaymentFlowExamples);
        }
        
        _logger.LogInformation("Refined {PromptType} prompt based on {FailureCount} failures: {PatternSummary}", 
            promptType, failures.Count, patterns.GetSummary());
        
        return Task.FromResult(refinedPrompt);
    }
    
    private FailurePatterns AnalyzeFailurePatterns(List<TransactionTrainingResult> failures)
    {
        var patterns = new FailurePatterns();
        
        foreach (var failure in failures)
        {
            // Analyze interaction results for pattern detection
            foreach (var interaction in failure.InteractionResults)
            {
                if (!interaction.Success)
                {
                    patterns.HasConversationFlowIssues = true;
                    patterns.ConversationFlowExamples.Add($"Input: '{interaction.CustomerInput}' -> Error: '{interaction.Error?.Message ?? "Unknown error"}'");
                }
                
                // Check for item matching issues by looking for common patterns
                if (interaction.CustomerInput.Contains("roti") && interaction.AIResponse.Contains("John"))
                {
                    patterns.HasItemMismatchErrors = true;
                    patterns.ItemMismatchExamples.Add($"Customer said '{interaction.CustomerInput}' but AI processed 'Roti John' instead of correct item");
                }
                
                // Check for tool call issues (this would need integration with actual tool call results)
                if (interaction.AIResponse.Contains("ERROR") || interaction.AIResponse.Contains("failed"))
                {
                    patterns.HasToolCallErrors = true;
                    patterns.ToolCallErrorExamples.Add($"Tool call failed for input: '{interaction.CustomerInput}'");
                }
                
                // Check for payment flow issues
                if (interaction.CustomerInput.ToLower().Contains("pay") && !interaction.Success)
                {
                    patterns.HasPaymentFlowErrors = true;
                    patterns.PaymentFlowExamples.Add($"Payment processing failed for: '{interaction.CustomerInput}'");
                }
            }
        }
        
        return patterns;
    }
    
    private string AddToolCallGuidance(string prompt, List<string> examples)
    {
        var guidance = "\n\n## TOOL CALL IMPROVEMENTS (Added by Training)\n";
        guidance += "Based on training failures, pay special attention to:\n";
        
        foreach (var example in examples.Take(3)) // Limit to top 3 examples
        {
            guidance += $"- {example}\n";
        }
        
        guidance += "\n**CRITICAL**: Ensure tool calls are made with correct parameters before responding to customer.\n";
        
        return prompt + guidance;
    }
    
    private string AddConversationFlowGuidance(string prompt, List<string> examples)
    {
        var guidance = "\n\n## CONVERSATION FLOW IMPROVEMENTS (Added by Training)\n";
        guidance += "Training identified conversation flow issues:\n";
        
        foreach (var example in examples.Take(3))
        {
            guidance += $"- {example}\n";
        }
        
        guidance += "\n**IMPROVEMENT**: Ensure proper acknowledgment and clear responses for all customer inputs.\n";
        
        return prompt + guidance;
    }
    
    private string AddItemMatchingGuidance(string prompt, List<string> examples)
    {
        var guidance = "\n\n## ITEM MATCHING IMPROVEMENTS (Added by Training)\n";
        guidance += "Training detected product identification errors - improve disambiguation process:\n";
        
        foreach (var example in examples.Take(3))
        {
            guidance += $"- {example}\n";
        }
        
        guidance += "\n**PROCESS IMPROVEMENT**: When customer uses informal product names:\n";
        guidance += "1. Use search_products tool to find exact matches\n";
        guidance += "2. If multiple products match, ask customer for clarification\n";
        guidance += "3. Confirm specific product name before adding to transaction\n";
        guidance += "4. Lower confidence thresholds for product identification\n";
        guidance += "\n**ARCHITECTURAL PRINCIPLE**: Product mappings come from restaurant catalog service, not hardcoded assumptions.\n";
        
        return prompt + guidance;
    }
    
    private string AddPaymentFlowGuidance(string prompt, List<string> examples)
    {
        var guidance = "\n\n## PAYMENT FLOW IMPROVEMENTS (Added by Training)\n";
        guidance += "Training identified payment processing issues:\n";
        
        foreach (var example in examples.Take(3))
        {
            guidance += $"- {example}\n";
        }
        
        guidance += "\n**IMPROVEMENT**: Ensure payment methods are loaded and processed correctly.\n";
        
        return prompt + guidance;
    }
    
    private class FailurePatterns
    {
        public bool HasToolCallErrors { get; set; }
        public bool HasConversationFlowIssues { get; set; }
        public bool HasItemMismatchErrors { get; set; }
        public bool HasPaymentFlowErrors { get; set; }
        
        public List<string> ToolCallErrorExamples { get; set; } = new();
        public List<string> ConversationFlowExamples { get; set; } = new();
        public List<string> ItemMismatchExamples { get; set; } = new();
        public List<string> PaymentFlowExamples { get; set; } = new();
        
        public string GetSummary()
        {
            var issues = new List<string>();
            if (HasToolCallErrors) { issues.Add("Tool Call Errors"); }
            if (HasConversationFlowIssues) { issues.Add("Conversation Flow Issues"); }
            if (HasItemMismatchErrors) { issues.Add("Item Matching Errors"); }
            if (HasPaymentFlowErrors) { issues.Add("Payment Flow Errors"); }
            
            return issues.Any() ? string.Join(", ", issues) : "No patterns detected";
        }
    }
    
    // Standard training session interface methods
    public async Task PauseAsync()
    {
        lock (_stateLock)
        {
            if (_state != TrainingSessionState.Running)
            {
                throw new InvalidOperationException($"Cannot pause training session in state {_state}");
            }
            _state = TrainingSessionState.Paused;
        }
        await Task.CompletedTask;
    }

    public async Task ResumeAsync()
    {
        lock (_stateLock)
        {
            if (_state != TrainingSessionState.Paused)
            {
                throw new InvalidOperationException($"Cannot resume training session in state {_state}");
            }
            _state = TrainingSessionState.Running;
        }
        await Task.CompletedTask;
    }

    public async Task AbortAsync()
    {
        _cancellationTokenSource?.Cancel();
        await HandleAbortAsync("Transaction training aborted by user request");
    }

    private async Task UpdateProgressAsync(int generation, int currentScenario, TrainingConfiguration config)
    {
        var elapsedTime = DateTime.UtcNow - _startTime;
        var progress = (double)(generation - 1) / config.MaxGenerations + 
                      ((double)currentScenario / config.ScenarioCount / config.MaxGenerations);

        lock (_stateLock)
        {
            _currentStats = new TrainingStatistics
            {
                CurrentGeneration = generation,
                TotalGenerations = config.MaxGenerations,
                ScenariosCompleted = currentScenario,
                TotalScenarios = config.ScenarioCount,
                CurrentScore = _currentStats.CurrentScore,
                BestScore = _currentStats.BestScore,
                Improvement = _currentStats.Improvement,
                ElapsedTime = elapsedTime,
                EstimatedRemaining = TimeSpan.FromTicks(Math.Max(0, 
                    (long)(elapsedTime.Ticks / progress) - elapsedTime.Ticks)),
                ConsecutiveFailures = _currentStats.ConsecutiveFailures,
                ConsecutiveImprovements = _currentStats.ConsecutiveImprovements
            };
        }

        var progressArgs = new TrainingProgressEventArgs
        {
            Generation = generation,
            CurrentScenario = currentScenario,
            TotalScenarios = config.ScenarioCount,
            Progress = progress,
            CurrentScore = _currentStats.CurrentScore,
            BestScore = _currentStats.BestScore,
            Improvement = _currentStats.Improvement,
            CurrentActivity = $"Transaction Training Generation {generation}: Scenario {currentScenario}/{config.ScenarioCount}",
            ElapsedTime = elapsedTime,
            EstimatedRemaining = _currentStats.EstimatedRemaining
        };

        ProgressUpdated?.Invoke(this, progressArgs);
        await Task.CompletedTask;
    }

    private async Task HandleAbortAsync(string reason)
    {
        lock (_stateLock)
        {
            _state = TrainingSessionState.Aborted;
        }

        var completionArgs = new TrainingCompleteEventArgs
        {
            SessionId = _sessionId,
            FinalState = TrainingSessionState.Aborted,
            FinalScore = _currentStats.CurrentScore,
            BestScore = _currentStats.BestScore,
            TotalImprovement = _currentStats.Improvement,
            GenerationsCompleted = _currentStats.CurrentGeneration,
            TotalDuration = DateTime.UtcNow - _startTime,
            AbortReason = reason
        };

        TrainingComplete?.Invoke(this, completionArgs);
        await Task.CompletedTask;
    }

    private async Task HandleFailureAsync(string errorMessage)
    {
        lock (_stateLock)
        {
            _state = TrainingSessionState.Failed;
        }

        var completionArgs = new TrainingCompleteEventArgs
        {
            SessionId = _sessionId,
            FinalState = TrainingSessionState.Failed,
            FinalScore = _currentStats.CurrentScore,
            BestScore = _currentStats.BestScore,
            TotalImprovement = _currentStats.Improvement,
            GenerationsCompleted = _currentStats.CurrentGeneration,
            TotalDuration = DateTime.UtcNow - _startTime,
            ErrorMessage = errorMessage
        };

        TrainingComplete?.Invoke(this, completionArgs);
        await Task.CompletedTask;
    }

    private Dictionary<string, double> CalculateGenerationQualityMetrics(List<TransactionTrainingResult> results)
    {
        if (!results.Any()) 
        {
            return new Dictionary<string, double>();
        }

        return new Dictionary<string, double>
        {
            { "TransactionCompletionRate", results.Count(r => r.TransactionSuccessful) / (double)results.Count },
            { "AverageScore", results.Average(r => r.OverallScore) },
            { "ItemMatchingAccuracy", results.Average(r => r.QualityMetrics.GetValueOrDefault("ItemMatchingAccuracy", 0.0)) },
            { "ToolCallAccuracy", results.Average(r => r.QualityMetrics.GetValueOrDefault("ToolCallAccuracy", 0.0)) },
            { "PersonalityConsistency", results.Average(r => r.QualityMetrics.GetValueOrDefault("PersonalityConsistency", 0.0)) }
        };
    }

    private async Task SavePromptToFileAsync(PersonalityType personalityType, string promptType, string promptContent)
    {
        // ARCHITECTURAL PRINCIPLE: Save directly to the prompt files that PosKernel.AI reads
        var promptFilePath = Path.Combine("PosKernel.AI", "Prompts", personalityType.ToString(), $"{promptType}.md");
        var promptDirectory = Path.GetDirectoryName(promptFilePath);
        
        if (!string.IsNullOrEmpty(promptDirectory) && !Directory.Exists(promptDirectory))
        {
            Directory.CreateDirectory(promptDirectory);
            _logger.LogInformation("Created prompt directory: {Directory}", promptDirectory);
        }
        
        try
        {
            await File.WriteAllTextAsync(promptFilePath, promptContent);
            _logger.LogInformation("Saved optimized {PromptType} prompt to {FilePath}", promptType, promptFilePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Failed to save optimized prompt to file '{promptFilePath}'. " +
                $"Training requires write access to prompt files. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// ARCHITECTURAL FIX: Generate diverse, realistic customer interactions using training AI
    /// 
    /// ROADMAP:
    /// Phase 1: ✅ Basic diversity with randomized variations (current implementation)
    /// Phase 2: 🚧 AI-powered scenario generation using dedicated training AI client
    /// Phase 3: 📋 Load from external scenario template files
    /// Phase 4: 📋 Integration with historical customer data when available
    /// 
    /// ARCHITECTURAL PRINCIPLE: Never use identical hardcoded scenarios - training requires diversity
    /// </summary>
    private List<CustomerInteraction> GenerateDiverseCustomerInteractions(string scenarioType)
    {
        try
        {
            // ARCHITECTURAL PRINCIPLE: Use training AI to generate diverse customer language patterns
            var prompt = BuildScenarioGenerationPrompt(scenarioType);
            
            // TODO: Create dedicated training AI client (separate from production ChatOrchestrator)
            // For now, this is a placeholder that generates basic diversity
            
            var interactions = new List<CustomerInteraction>();
            
            switch (scenarioType)
            {
                case "simple_order":
                    interactions = GenerateSimpleOrderVariations();
                    break;
                case "multi_item_order":
                    interactions = GenerateMultiItemVariations();
                    break;
                case "payment_flow":
                    interactions = GeneratePaymentFlowVariations();
                    break;
                default:
                    throw new ArgumentException($"Unknown scenario type: {scenarioType}");
            }
            
            _logger.LogDebug("Generated {Count} diverse interactions for scenario type: {ScenarioType}", 
                interactions.Count, scenarioType);
                
            return interactions;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Failed to generate diverse customer interactions for '{scenarioType}'. " +
                $"Training system requires AI-powered scenario generation for realistic diversity. " +
                $"Error: {ex.Message}");
        }
    }
    
    private string BuildScenarioGenerationPrompt(string scenarioType)
    {
        // ARCHITECTURAL PRINCIPLE: Load training prompts from POSKERNEL_HOME, not hardcode them
        var posKernelHome = Environment.GetEnvironmentVariable("POSKERNEL_HOME") 
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".poskernel");
        
        var trainingPromptPath = Path.Combine(posKernelHome, "training", "training_prompt.md");
        
        try
        {
            // ARCHITECTURAL PRINCIPLE: Fail fast if training prompt file missing
            if (!File.Exists(trainingPromptPath))
            {
                CreateDefaultTrainingPrompt(trainingPromptPath);
                _logger.LogInformation("Created default training prompt at {FilePath}", trainingPromptPath);
            }
            
            // Use synchronous file reading to avoid hanging issues
            var basePrompt = File.ReadAllText(trainingPromptPath);
            
            // Add scenario-specific context
            var fullPrompt = basePrompt + $"\n\nSCENARIO TYPE: {scenarioType}\n\nGenerate realistic customer interactions for this specific scenario type.";
            
            _logger.LogDebug("Loaded training prompt from {FilePath} for scenario type: {ScenarioType}", 
                trainingPromptPath, scenarioType);
                
            return fullPrompt;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Failed to load training prompt from '{trainingPromptPath}'. " +
                $"Training system requires customizable scenario generation prompts. " +
                $"Error: {ex.Message}");
        }
    }
    
    private void CreateDefaultTrainingPrompt(string trainingPromptPath)
    {
        // ARCHITECTURAL PRINCIPLE: Create directory if it doesn't exist
        var trainingDirectory = Path.GetDirectoryName(trainingPromptPath);
        if (!string.IsNullOrEmpty(trainingDirectory) && !Directory.Exists(trainingDirectory))
        {
            Directory.CreateDirectory(trainingDirectory);
            _logger.LogInformation("Created training directory: {Directory}", trainingDirectory);
        }
        
        var defaultPrompt = @"# AI Training Scenario Generation Prompt

Generate realistic customer interactions for a Singapore kopitiam/coffee shop training scenario.

## REQUIREMENTS:
- Use natural Singapore English (Singlish) patterns when appropriate
- Include cultural variations and common phrases
- Generate diverse ways customers might express the same intent
- Avoid repetitive patterns - each interaction should feel authentic
- Consider different levels of politeness and directness
- Include both locals and tourists/foreigners interaction styles

## CULTURAL CONTEXT:
- Setting: Singapore kopitiam (traditional coffee shop)
- Language mix: English, Singlish, some Hokkien/Cantonese terms
- Products: Traditional coffee/tea (kopi, teh), local food items
- Service style: Fast, efficient, friendly but direct

## DIVERSITY GOALS:
- Vary sentence structure and vocabulary
- Include different customer personalities (polite, direct, uncertain, experienced)
- Mix formal and informal language patterns
- Include common misunderstandings or clarifications

## INSTRUCTIONS:
Generate varied customer language patterns that a real Singapore kopitiam would encounter.
Each scenario should test different aspects of the AI cashier's capabilities.

## CUSTOMIZATION NOTES:
Users can modify this prompt to:
- Change cultural context and language patterns
- Adjust formality levels and interaction styles
- Add specific product categories or service scenarios
- Include different customer demographics or situations

This prompt will be combined with specific scenario type information during training.";

        File.WriteAllText(trainingPromptPath, defaultPrompt);
        _logger.LogInformation("Created default training prompt at: {FilePath}", trainingPromptPath);
    }

    // ARCHITECTURAL NOTE: These are temporary diversity generators
    // TODO: Replace with AI-powered generation using training AI client
    private List<CustomerInteraction> GenerateSimpleOrderVariations()
    {
        var variations = new[]
        {
            "kopi si", "one coffee with milk", "I want kopi", "give me coffee", "coffee please",
            "kopi susu", "white coffee", "can I have kopi", "one kopi si", "coffee with milk"
        };
        
        var selectedVariation = variations[Random.Shared.Next(variations.Length)];
        
        return new List<CustomerInteraction>
        {
            new CustomerInteraction
            {
                CustomerInput = selectedVariation,
                ExpectedToolCalls = new List<ExpectedToolCall>
                {
                    new ExpectedToolCall 
                    { 
                        ToolName = "add_item_to_transaction", 
                        ExpectedParameters = new Dictionary<string, object>
                        {
                            ["item_description"] = "Coffee with milk",
                            ["quantity"] = 1
                        }
                    }
                },
                ExpectedTransactionState = TransactionState.Building
            },
            new CustomerInteraction
            {
                CustomerInput = Random.Shared.NextDouble() > 0.5 ? "that's all" : "finish already",
                ExpectedTransactionState = TransactionState.Building
            },
            new CustomerInteraction
            {
                CustomerInput = Random.Shared.NextDouble() > 0.5 ? "cash" : "I pay cash",
                ExpectedToolCalls = new List<ExpectedToolCall>
                {
                    new ExpectedToolCall 
                    { 
                        ToolName = "process_payment", 
                        ExpectedParameters = new Dictionary<string, object>
                        {
                            ["payment_method"] = "cash"
                        }
                    }
                },
                ExpectedTransactionState = TransactionState.Completed
            }
        };
    }
    
    private List<CustomerInteraction> GenerateMultiItemVariations()
    {
        var drinkVariations = new[] { "kopi o", "coffee black", "one black coffee", "kopi no milk" };
        var toastVariations = new[] { "roti kaya", "toast with kaya", "kaya toast", "bread with coconut jam" };
        var quantityVariations = new[] { "two pieces", "two", "give me two", "I want 2" };
        
        var selectedDrink = drinkVariations[Random.Shared.Next(drinkVariations.Length)];
        var selectedToast = toastVariations[Random.Shared.Next(toastVariations.Length)];
        var selectedQuantity = quantityVariations[Random.Shared.Next(quantityVariations.Length)];
        
        return new List<CustomerInteraction>
        {
            new CustomerInteraction
            {
                CustomerInput = selectedDrink,
                ExpectedToolCalls = new List<ExpectedToolCall>
                {
                    new ExpectedToolCall 
                    { 
                        ToolName = "add_item_to_transaction", 
                        ExpectedParameters = new Dictionary<string, object>
                        {
                            ["item_description"] = "Black Coffee",
                            ["quantity"] = 1
                        }
                    }
                },
                ExpectedTransactionState = TransactionState.Building
            },
            new CustomerInteraction
            {
                CustomerInput = $"{selectedToast} {selectedQuantity}",
                ExpectedToolCalls = new List<ExpectedToolCall>
                {
                    new ExpectedToolCall 
                    { 
                        ToolName = "add_item_to_transaction", 
                        ExpectedParameters = new Dictionary<string, object>
                        {
                            ["item_description"] = "Kaya Toast",
                            ["quantity"] = 2
                        }
                    }
                },
                ExpectedTransactionState = TransactionState.Building
            },
            new CustomerInteraction
            {
                CustomerInput = Random.Shared.NextDouble() > 0.5 ? "that's all" : "ok finish",
                ExpectedTransactionState = TransactionState.Building
            },
            new CustomerInteraction
            {
                CustomerInput = "cash",
                ExpectedToolCalls = new List<ExpectedToolCall>
                {
                    new ExpectedToolCall 
                    { 
                        ToolName = "process_payment", 
                        ExpectedParameters = new Dictionary<string, object>
                        {
                            ["payment_method"] = "cash"
                        }
                    }
                },
                ExpectedTransactionState = TransactionState.Completed
            }
        };
    }
    
    private List<CustomerInteraction> GeneratePaymentFlowVariations()
    {
        var beverageVariations = new[] { "milo", "hot chocolate", "chocolate drink", "one milo" };
        var paymentQuestions = new[] 
        { 
            "what payment methods you accept?", 
            "can pay by card?", 
            "you take paynow?",
            "how to pay?"
        };
        var paymentMethods = new[] { "paynow", "card", "nets" };
        
        var selectedBeverage = beverageVariations[Random.Shared.Next(beverageVariations.Length)];
        var selectedQuestion = paymentQuestions[Random.Shared.Next(paymentQuestions.Length)];
        var selectedPayment = paymentMethods[Random.Shared.Next(paymentMethods.Length)];
        
        return new List<CustomerInteraction>
        {
            new CustomerInteraction
            {
                CustomerInput = selectedBeverage,
                ExpectedToolCalls = new List<ExpectedToolCall>
                {
                    new ExpectedToolCall 
                    { 
                        ToolName = "add_item_to_transaction", 
                        ExpectedParameters = new Dictionary<string, object>
                        {
                            ["item_description"] = "Hot chocolate drink",
                            ["quantity"] = 1
                        }
                    }
                },
                ExpectedTransactionState = TransactionState.Building
            },
            new CustomerInteraction
            {
                CustomerInput = Random.Shared.NextDouble() > 0.5 ? "that's all" : "done",
                ExpectedTransactionState = TransactionState.Building
            },
            new CustomerInteraction
            {
                CustomerInput = selectedQuestion,
                ExpectedToolCalls = new List<ExpectedToolCall>
                {
                    new ExpectedToolCall 
                    { 
                        ToolName = "load_payment_methods_context", 
                        ExpectedParameters = new Dictionary<string, object>
                        {
                            ["currency"] = "SGD"
                        }
                    }
                },
                ExpectedTransactionState = TransactionState.Building
            },
            new CustomerInteraction
            {
                CustomerInput = selectedPayment,
                ExpectedToolCalls = new List<ExpectedToolCall>
                {
                    new ExpectedToolCall 
                    { 
                        ToolName = "process_payment", 
                        ExpectedParameters = new Dictionary<string, object>
                        {
                            ["payment_method"] = selectedPayment
                        }
                    }
                },
                ExpectedTransactionState = TransactionState.Completed
            }
        };
    }
}
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
