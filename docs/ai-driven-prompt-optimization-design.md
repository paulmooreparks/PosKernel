# AI-Driven Prompt Optimization System Design Document

## Executive Summary

This design proposes an **AI-driven prompt optimization system** that uses artificial intelligence to generate thousands of realistic customer interactions, automatically test different prompt variations, and evolve prompts to achieve optimal performance. Unlike live training systems, this approach uses **synthetic data generation** to safely optimize prompts before deployment.

## Implementation Readiness Assessment

### **Current System Status**
Based on analysis of existing logs and architecture:

- **✅ Infrastructure Ready**: ChatOrchestrator, McpClient, tool execution framework
- **❌ Tool Selection Broken**: AI consistently chooses `load_menu_context` instead of `add_item_to_transaction`
- **❌ Payment Completion Failing**: Gets stuck in payment method loops
- **⚠️ Requires Fix-First Approach**: Must resolve tool selection before training optimization

### **ARCHITECTURAL PRINCIPLE**: Training a broken system optimizes the brokenness
**Action Required**: Fix tool selection guidance and payment flow completion before implementing training system.

## Sophisticated Quality Evaluation Requirements

### **Beyond Binary Pass/Fail Analysis**

Real-world testing has revealed that prompt optimization requires **multi-dimensional quality assessment** that goes far beyond simple success/failure metrics.

#### **Subtle Issue Categories Requiring Detection**

##### **1. Contextual Appropriateness**
- ✅ **Functional Success**: Transaction progresses correctly
- ❌ **Contextual Problems**: "What else you want?" after payment completion
- **Challenge**: System works but experience is awkward

##### **2. Information Completeness vs. Correctness**
- ✅ **Tool Selection**: AI uses correct tool (`search_products`)
- ✅ **Data Retrieval**: AI gets complete data (19 items including Local Food)
- ❌ **Information Sharing**: AI shares vague "variety" instead of actual items
- **Challenge**: Data available but presentation insufficient

##### **3. Value Optimization Within Constraints**
- ✅ **Factual Accuracy**: All items listed under budget constraint
- ❌ **Missed Optimization**: Could suggest combinations within budget
- ❌ **Business Value**: Didn't demonstrate domain expertise
- **Challenge**: Multiple valid responses with different value levels

#### **Multi-Dimensional Evaluation Framework**

```csharp
public class AdvancedResponseEvaluation
{
    // Basic Correctness (Table Stakes)
    public bool FactualAccuracy { get; set; }
    public bool ToolUsageCorrect { get; set; }
    public bool PersonalityConsistent { get; set; }
    
    // Value Optimization (Competitive Advantage)
    public bool MaximizedCustomerValue { get; set; }
    public bool ShowedBusinessAcumen { get; set; }
    public bool ProvidedActionableOptions { get; set; }
    
    // Contextual Intelligence (Differentiation)
    public bool UnderstoodConstraints { get; set; }    // Budget limit
    public bool OptimizedWithinConstraints { get; set; } // Best use of $5
    public bool ShowedDomainExpertise { get; set; }     // Menu combinations
    
    // Customer Experience (Satisfaction)
    public bool SimplifiedDecisionMaking { get; set; }
    public bool ProvidedClearNextSteps { get; set; }
    public bool ContextuallyAppropriate { get; set; }
    
    // Overall Quality Score (0.0 - 1.0)
    public double QualityScore => CalculateWeightedScore();
}
```

#### **Response Quality Spectrum Recognition**

```csharp
// TRAINING CHALLENGE: Multiple valid responses with different value levels
// Not just "correct vs incorrect" but "good vs optimal"
public class ResponseQualitySpectrum
{
    public ResponseLevel Minimal { get; set; }    // Factually correct
    public ResponseLevel Good { get; set; }       // Helpful and accurate  
    public ResponseLevel Optimal { get; set; }    // Maximizes customer value
    public ResponseLevel Exceptional { get; set; } // Shows expertise & personality
}
```

#### **Scenario-Specific Intelligence Patterns**

##### **Budget Constraint Scenarios**
```markdown
**Pattern**: Customer mentions specific budget ("I only have $5")
**Minimal Response**: List items under budget
**Optimal Response**: Calculate combinations within budget, show value maximization
**Expected Enhancement**: 
- Calculate optimal combinations within budget
- Show value maximization opportunities  
- Suggest complete meal solutions within constraints
- Demonstrate menu expertise through intelligent recommendations
```

##### **Experience Optimization Scenarios**
```markdown
**Pattern**: Customer wants complete experience ("I need breakfast")
**Minimal Response**: Show popular breakfast items
**Optimal Response**: Suggest complete breakfast combinations with reasoning
**Expected Enhancement**:
- Suggest complete meal combinations
- Consider dietary balance and satisfaction
- Leverage personality knowledge of "what goes together"
- Show cultural authenticity in recommendations
```

#### **Contextual Appropriateness Detection**

```csharp
public class ContextualResponseAnalyzer
{
    // 1. Context Appropriateness
    bool DetectPostPaymentContinuation(ChatMessage response, PaymentStatus status)
    {
        return status == PaymentStatus.Completed && 
               response.Content.Contains("What else you want?");
    }
    
    // 2. Information Completeness  
    bool DetectIncompleteMenuResponse(string userQuery, string response, List<ProductInfo> availableData)
    {
        if (userQuery.Contains("what food") && availableData.Count > 5)
        {
            return !ContainsSpecificItems(response, availableData);
        }
        return false;
    }
    
    // 3. Value Maximization
    bool DetectMissedValueOptimization(string userQuery, string response, List<ProductInfo> products)
    {
        if (ExtractBudgetConstraint(userQuery, out decimal budget))
        {
            return !ContainsCombinationSuggestions(response, products, budget);
        }
        return false;
    }
}
```

### **Training System Requirements for Complex Scenarios**

#### **Sophisticated Pattern Detection**
The training system must automatically detect:
- **Subtle contextual inappropriateness** (functional but awkward)
- **Information completeness gaps** (partial vs. comprehensive responses)
- **Value optimization misses** (basic vs. expert-level recommendations)
- **Cultural authenticity variations** (generic vs. personality-specific)

#### **Multi-Dimensional Optimization Targets**
```csharp
public class OptimizationTargets
{
    // Primary Metrics (Must Achieve)
    public double FunctionalAccuracy { get; set; } = 0.95;    // 95% correct operations
    public double PersonalityConsistency { get; set; } = 0.90; // 90% authentic responses
    
    // Secondary Metrics (Competitive Advantage)
    public double ValueOptimization { get; set; } = 0.80;     // 80% optimal recommendations
    public double ContextualAppropriateness { get; set; } = 0.85; // 85% contextually appropriate
    
    // Tertiary Metrics (Differentiation)
    public double DomainExpertise { get; set; } = 0.75;       // 75% expert-level responses
    public double CustomerSatisfaction { get; set; } = 0.90;  // 90% satisfying interactions
}
```

#### **Progressive Quality Standards**
```csharp
public class QualityStandards
{
    // Evolution from Basic to Expert
    public QualityLevel Basic { get; set; } = new()
    {
        ToolSelectionAccuracy = 0.90,
        FactualCorrectness = 0.95,
        BasicPersonality = 0.80
    };
    
    public QualityLevel Professional { get; set; } = new()
    {
        ValueOptimization = 0.75,
        ContextualAwareness = 0.85,
        BusinessAcumen = 0.70
    };
    
    public QualityLevel Expert { get; set; } = new()
    {
        DomainExpertise = 0.90,
        CulturalAuthenticity = 0.95,
        CustomerDelight = 0.80
    };
}
```

### **Training Complexity Implications**

#### **Why This Demands AI-Driven Optimization**

These sophisticated quality requirements demonstrate why **manual prompt tuning** is insufficient:

1. **Scale**: Too many interaction patterns to manually test
2. **Subtlety**: Easy to miss contextual appropriateness issues  
3. **Consistency**: Need systematic evaluation across personalities
4. **Precision**: Requires detailed multi-dimensional assessment

#### **AI-Driven Optimization Advantages**

The **AI-driven prompt optimization system** can:
- ✅ **Detect subtle patterns** automatically across thousands of scenarios
- ✅ **Evaluate multi-dimensional quality** beyond simple pass/fail
- ✅ **Generate targeted improvements** for specific quality dimensions
- ✅ **Scale systematically** across different personality types and scenarios
- ✅ **Optimize progressively** from basic functionality to expert-level performance

## Training Parameters and Configuration

### **Training Configuration Service**

```csharp
// ARCHITECTURAL PRINCIPLE: Configuration-driven training with fail-fast validation
public interface ITrainingConfigurationService
{
    Task<TrainingConfiguration> LoadConfigurationAsync();
    Task SaveConfigurationAsync(TrainingConfiguration config);
    Task<ValidationResult> ValidateConfigurationAsync(TrainingConfiguration config);
}

public class TrainingConfigurationService : ITrainingConfigurationService
{
    private readonly IDataStore _dataStore;
    private readonly ILogger<TrainingConfigurationService> _logger;
    
    public async Task<TrainingConfiguration> LoadConfigurationAsync()
    {
        var config = await _dataStore.LoadAsync<TrainingConfiguration>("training-config");
        
        if (config == null)
        {
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Training configuration not found. " +
                "Initialize training configuration before starting optimization. " +
                "Use TrainingConfigurationDialog to set initial parameters.");
        }
        
        var validation = await ValidateConfigurationAsync(config);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Invalid training configuration. " +
                $"Errors: {string.Join(", ", validation.Errors)}. " +
                "Fix configuration before proceeding.");
        }
        
        return config;
    }
    
    public async Task<ValidationResult> ValidateConfigurationAsync(TrainingConfiguration config)
    {
        var errors = new List<string>();
        
        // FAIL FAST: Validate all parameters
        if (config.ScenarioCount < 50)
            errors.Add("ScenarioCount must be at least 50 for meaningful results");
            
        if (config.MaxGenerations > 100)
            errors.Add("MaxGenerations exceeding 100 may indicate training issues");
            
        if (config.ImprovementThreshold <= 0 || config.ImprovementThreshold > 0.1)
            errors.Add("ImprovementThreshold must be between 0.001 and 0.1");
            
        return new ValidationResult 
        { 
            IsValid = !errors.Any(), 
            Errors = errors 
        };
    }
}
```

### **Training Configuration Model**

```csharp
public class TrainingConfiguration
{
    // Core Training Parameters
    public int ScenarioCount { get; set; } = 500;           // Number of test scenarios
    public int MaxGenerations { get; set; } = 50;           // Maximum training generations
    public double ImprovementThreshold { get; set; } = 0.02; // 2% improvement required
    public int ValidationScenarios { get; set; } = 200;     // Fresh scenarios for validation
    
    // Scenario Generation Mix (must sum to 1.0)
    public ScenarioDistribution ScenarioMix { get; set; } = new()
    {
        BasicOrdering = 0.40,      // Simple orders
        EdgeCases = 0.20,          // Challenging scenarios
        CulturalVariations = 0.20, // Language/cultural tests
        AmbiguousRequests = 0.10,  // Disambiguation tests
        PaymentScenarios = 0.10    // Payment flow tests
    };
    
    // Training Aggressiveness Controls
    public TrainingAggressiveness Aggressiveness { get; set; } = new()
    {
        MutationRate = 0.15,              // 15% of prompt modified per mutation
        ExplorationRatio = 0.30,          // 30% exploratory vs targeted mutations
        RegressionTolerance = -0.05,      // 5% regression triggers abort
        StagnationLimit = 5,              // Generations without improvement
        MinimumProgress = 0.001           // 0.1% minimum improvement per generation
    };
    
    // Quality Thresholds - Enhanced for Multi-Dimensional Assessment
    public QualityTargets QualityTargets { get; set; } = new()
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
    };
    
    // Training Focus Areas (0.0 = ignore, 1.0 = maximum focus)
    public TrainingFocus Focus { get; set; } = new()
    {
        ToolSelectionAccuracy = 1.0,      // Critical - fix tool selection
        PersonalityAuthenticity = 0.8,    // Important - maintain Uncle character
        PaymentFlowCompletion = 1.0,      // Critical - fix payment loops
        ContextualAppropriateness = 0.9,  // High - fix post-payment responses
        InformationCompleteness = 0.85,   // High - provide detailed responses
        ValueOptimization = 0.7,          // Important - suggest combinations
        AmbiguityHandling = 0.6,          // Moderate - proper disambiguation
        CulturalTermRecognition = 0.6,    // Moderate - Singlish patterns
        ConversationEfficiency = 0.4      // Low - speed optimization
    };
    
    // Safety Guards
    public SafetyConfiguration Safety { get; set; } = new()
    {
        MaxTrainingDuration = TimeSpan.FromHours(8),  // Abort after 8 hours
        MaxPromptLength = 10000,                       // Prevent prompt bloat
        RequiredRegressionTests = true,                // Always run regression tests
        HumanApprovalThreshold = 0.15,                // 15% improvement needs review
        AutoBackupInterval = TimeSpan.FromMinutes(30)  // Backup every 30 minutes
    };
    
    // Persistence Settings
    public PersistenceConfiguration Persistence { get; set; } = new()
    {
        SaveIntermediateResults = true,
        ResultsRetentionDays = 30,
        DetailedLogging = true,
        MetricsCollection = true
    };
}

public class ScenarioDistribution
{
    public double BasicOrdering { get; set; }
    public double EdgeCases { get; set; }
    public double CulturalVariations { get; set; }
    public double AmbiguousRequests { get; set; }
    public double PaymentScenarios { get; set; }
    
    public void Validate()
    {
        var total = BasicOrdering + EdgeCases + CulturalVariations + 
                   AmbiguousRequests + PaymentScenarios;
                   
        if (Math.Abs(total - 1.0) > 0.001)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Scenario distribution must sum to 1.0, got {total:F3}. " +
                "Adjust scenario percentages to total 100%.");
        }
    }
}
```

### **Training Configuration Dialog (TUI)**

```csharp
public class TrainingConfigurationDialog
{
    private readonly ITrainingConfigurationService _configService;
    private TrainingConfiguration _config;
    
    public async Task<TrainingConfiguration?> ShowDialogAsync()
    {
        try
        {
            _config = await _configService.LoadConfigurationAsync();
        }
        catch (InvalidOperationException)
        {
            // Create default configuration for first-time setup
            _config = new TrainingConfiguration();
        }
        
        var dialog = new Dialog("Training Configuration")
        {
            Width = 80,
            Height = 25
        };
        
        // Core Parameters Section
        var coreFrame = new FrameView("Core Parameters")
        {
            X = 1, Y = 1, Width = Dim.Fill(2), Height = 8
        };
        
        var scenarioCountField = new TextField($"{_config.ScenarioCount}")
        {
            X = 20, Y = 1, Width = 10
        };
        coreFrame.Add(new Label(1, 1, "Scenario Count:"), scenarioCountField);
        
        var maxGenField = new TextField($"{_config.MaxGenerations}")
        {
            X = 20, Y = 2, Width = 10
        };
        coreFrame.Add(new Label(1, 2, "Max Generations:"), maxGenField);
        
        var improvementField = new TextField($"{_config.ImprovementThreshold:F3}")
        {
            X = 20, Y = 3, Width = 10
        };
        coreFrame.Add(new Label(1, 3, "Improvement Threshold:"), improvementField);
        
        // Aggressiveness Sliders Section
        var aggressivenessFrame = new FrameView("Training Aggressiveness")
        {
            X = 1, Y = 9, Width = Dim.Fill(2), Height = 8
        };
        
        var mutationSlider = new SliderView()
        {
            X = 25, Y = 1, Width = 30,
            MinimumValue = 0.05f, MaximumValue = 0.50f,
            Value = (float)_config.Aggressiveness.MutationRate,
            Type = SliderType.Double
        };
        aggressivenessFrame.Add(new Label(1, 1, "Mutation Rate (5-50%):"), mutationSlider);
        
        var explorationSlider = new SliderView()
        {
            X = 25, Y = 2, Width = 30,
            MinimumValue = 0.10f, MaximumValue = 0.60f,
            Value = (float)_config.Aggressiveness.ExplorationRatio,
            Type = SliderType.Double
        };
        aggressivenessFrame.Add(new Label(1, 2, "Exploration (10-60%):"), explorationSlider);
        
        var regressionSlider = new SliderView()
        {
            X = 25, Y = 3, Width = 30,
            MinimumValue = -0.10f, MaximumValue = -0.01f,
            Value = (float)_config.Aggressiveness.RegressionTolerance,
            Type = SliderType.Double
        };
        aggressivenessFrame.Add(new Label(1, 3, "Regression Tolerance:"), regressionSlider);
        
        // Focus Area Sliders Section  
        var focusFrame = new FrameView("Training Focus Areas")
        {
            X = 41, Y = 1, Width = Dim.Fill(2), Height = 16
        };
        
        var toolFocusSlider = CreateFocusSlider(1, "Tool Selection:", _config.Focus.ToolSelectionAccuracy);
        var personalityFocusSlider = CreateFocusSlider(2, "Personality:", _config.Focus.PersonalityAuthenticity);
        var paymentFocusSlider = CreateFocusSlider(3, "Payment Flow:", _config.Focus.PaymentFlowCompletion);
        var contextFocusSlider = CreateFocusSlider(4, "Context:", _config.Focus.ContextualAppropriateness);
        var completeFocusSlider = CreateFocusSlider(5, "Completeness:", _config.Focus.InformationCompleteness);
        var valueFocusSlider = CreateFocusSlider(6, "Value Opt:", _config.Focus.ValueOptimization);
        
        focusFrame.Add(
            new Label(1, 1, "Tool Selection:"), toolFocusSlider,
            new Label(1, 2, "Personality:"), personalityFocusSlider,
            new Label(1, 3, "Payment Flow:"), paymentFocusSlider,
            new Label(1, 4, "Context:"), contextFocusSlider,
            new Label(1, 5, "Completeness:"), completeFocusSlider,
            new Label(1, 6, "Value Opt:"), valueFocusSlider
        );
        
        dialog.Add(coreFrame, aggressivenessFrame, focusFrame);
        
        // Buttons
        var okButton = new Button("OK")
        {
            X = Pos.Center() - 10, Y = Pos.Bottom(dialog) - 4,
            IsDefault = true
        };
        
        var cancelButton = new Button("Cancel")
        {
            X = Pos.Center() + 2, Y = Pos.Bottom(dialog) - 4
        };
        
        var resetButton = new Button("Reset to Defaults")
        {
            X = 2, Y = Pos.Bottom(dialog) - 4
        };
        
        okButton.Clicked += async () =>
        {
            try
            {
                // Update configuration from dialog values
                UpdateConfigFromDialog();
                
                // Validate configuration
                var validation = await _configService.ValidateConfigurationAsync(_config);
                if (!validation.IsValid)
                {
                    MessageBox.ErrorQuery("Validation Error", 
                        $"Configuration invalid:\n{string.Join("\n", validation.Errors)}", "OK");
                    return;
                }
                
                // Save configuration
                await _configService.SaveConfigurationAsync(_config);
                Application.RequestStop();
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Error", $"Failed to save configuration: {ex.Message}", "OK");
            }
        };
        
        cancelButton.Clicked += () => {
            _config = null;
            Application.RequestStop();
        };
        
        resetButton.Clicked += () => {
            _config = new TrainingConfiguration();
            // Update all dialog fields with default values
            UpdateDialogFromConfig();
        };
        
        dialog.Add(okButton, cancelButton, resetButton);
        
        Application.Run(dialog);
        return _config;
    }
    
    private SliderView CreateFocusSlider(int y, string label, double value)
    {
        return new SliderView()
        {
            X = 15, Y = y, Width = 20,
            MinimumValue = 0.0f, MaximumValue = 1.0f,
            Value = (float)value,
            Type = SliderType.Double
        };
    }
}
```

### **Configuration Persistence Service**

```csharp
// ARCHITECTURAL PRINCIPLE: Pluggable persistence with fail-fast validation
public interface ITrainingDataStore
{
    Task<T?> LoadAsync<T>(string key) where T : class;
    Task SaveAsync<T>(string key, T data) where T : class;
    Task DeleteAsync(string key);
    Task<bool> ExistsAsync(string key);
}

// JSON file implementation for simplicity and transparency
public class JsonFileTrainingDataStore : ITrainingDataStore
{
    private readonly string _dataDirectory;
    private readonly ILogger<JsonFileTrainingDataStore> _logger;
    
    public JsonFileTrainingDataStore(string dataDirectory, ILogger<JsonFileTrainingDataStore> logger)
    {
        _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // ARCHITECTURAL PRINCIPLE: Fail fast if data directory not accessible
        if (!Directory.Exists(_dataDirectory))
        {
            try
            {
                Directory.CreateDirectory(_dataDirectory);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"DESIGN DEFICIENCY: Cannot create training data directory '{_dataDirectory}'. " +
                    $"Ensure write permissions exist. Error: {ex.Message}");
            }
        }
    }
    
    public async Task<T?> LoadAsync<T>(string key) where T : class
    {
        var filePath = GetFilePath(key);
        
        if (!File.Exists(filePath))
        {
            _logger.LogDebug("Training data file not found: {FilePath}", filePath);
            return null;
        }
        
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var data = JsonSerializer.Deserialize<T>(json, GetJsonOptions());
            
            _logger.LogDebug("Loaded training data: {Key}, Size: {Size} bytes", key, json.Length);
            return data;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Failed to load training data '{key}' from '{filePath}'. " +
                $"File may be corrupted or invalid JSON. Error: {ex.Message}");
        }
    }
    
    public async Task SaveAsync<T>(string key, T data) where T : class
    {
        var filePath = GetFilePath(key);
        var tempFilePath = filePath + ".tmp";
        
        try
        {
            var json = JsonSerializer.Serialize(data, GetJsonOptions());
            
            // Write to temp file first, then atomic rename
            await File.WriteAllTextAsync(tempFilePath, json);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            
            File.Move(tempFilePath, filePath);
            
            _logger.LogDebug("Saved training data: {Key}, Size: {Size} bytes", key, json.Length);
        }
        catch (Exception ex)
        {
            // Clean up temp file on error
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { }
            }
            
            throw new InvalidOperationException(
                $"DESIGN DEFICIENCY: Failed to save training data '{key}' to '{filePath}'. " +
                $"Check disk space and write permissions. Error: {ex.Message}");
        }
    }
    
    private string GetFilePath(string key)
    {
        // ARCHITECTURAL PRINCIPLE: Safe file naming with validation
        var safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_dataDirectory, $"{safeKey}.json");
    }
    
    private JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}

// Alternative: SQLite implementation for structured queries
public class SqliteTrainingDataStore : ITrainingDataStore
{
    private readonly string _connectionString;
    
    // Implementation would use Entity Framework Core with SQLite provider
    // Useful for complex training analytics and historical data queries
}
```

## UI Architecture - TUI with Clean Separation

### **ARCHITECTURAL PRINCIPLE**: UI contains NO training logic - complete separation of concerns

```csharp
// Training engine (UI-agnostic)
public interface ITrainingSession
{
    Task<TrainingResults> RunTrainingSessionAsync(TrainingConfiguration config);
    event EventHandler<TrainingProgressEventArgs> ProgressUpdated;
    event EventHandler<ScenarioTestEventArgs> ScenarioTested;
    event EventHandler<GenerationCompleteEventArgs> GenerationComplete;
    
    Task PauseAsync();
    Task ResumeAsync();
    Task AbortAsync();
    
    TrainingSessionState State { get; }
    TrainingStatistics CurrentStats { get; }
}

// TUI observes and controls (zero training logic)
public class TrainingMonitorTUI
{
    private readonly ITrainingSession _trainingSession;
    private readonly ITrainingConfigurationService _configService;
    
    private Window _mainWindow;
    private FrameView _progressFrame;
    private FrameView _currentScenarioFrame;
    private FrameView _statisticsFrame;
    private TextView _logView;
    private ProgressBar _generationProgress;
    private Label _currentScoreLabel;
    private Label _bestScoreLabel;
    
    public async Task RunAsync()
    {
        // Load configuration or show config dialog
        var config = await LoadOrConfigureTrainingAsync();
        if (config == null) return; // User cancelled
        
        InitializeUI();
        
        // Wire up training session events
        _trainingSession.ProgressUpdated += OnProgressUpdated;
        _trainingSession.ScenarioTested += OnScenarioTested;
        _trainingSession.GenerationComplete += OnGenerationComplete;
        
        // Start training in background
        var trainingTask = _trainingSession.RunTrainingSessionAsync(config);
        
        // Handle UI interactions
        _mainWindow.KeyPress += HandleKeyPress;
        
        Application.Run(_mainWindow);
        
        // Wait for training completion
        var results = await trainingTask;
        ShowResults(results);
    }
    
    private void HandleKeyPress(KeyEventEventArgs e)
    {
        switch (e.KeyEvent.Key)
        {
            case Key.Esc:
                if (_trainingSession.State == TrainingSessionState.Running)
                {
                    _ = _trainingSession.PauseAsync();
                    AddLog("Training paused by user");
                }
                else if (_trainingSession.State == TrainingSessionState.Paused)
                {
                    _ = _trainingSession.ResumeAsync();
                    AddLog("Training resumed");
                }
                break;
                
            case Key.F10:
                if (ConfirmAbort())
                {
                    _ = _trainingSession.AbortAsync();
                    AddLog("Training aborted by user");
                }
                break;
                
            case Key.F1:
                ShowHelp();
                break;
        }
    }
    
    private void OnProgressUpdated(object sender, TrainingProgressEventArgs e)
    {
        Application.Invoke(() =>
        {
            _generationProgress.Fraction = e.Progress;
            _currentScoreLabel.Text = $"Current: {e.CurrentScore:F3}";
            _bestScoreLabel.Text = $"Best: {e.BestScore:F3}";
            
            AddLog($"Generation {e.Generation}: Score {e.CurrentScore:F3}, " +
                  $"Improvement {e.Improvement:+F3}");
        });
    }
    
    private void OnScenarioTested(object sender, ScenarioTestEventArgs e)
    {
        Application.Invoke(() =>
        {
            UpdateCurrentScenarioDisplay(e.Scenario, e.Response, e.Score);
        });
    }
}

public class TrainingProgressEventArgs : EventArgs
{
    public int Generation { get; set; }
    public double Progress { get; set; }
    public double CurrentScore { get; set; }
    public double BestScore { get; set; }
    public double Improvement { get; set; }
    public string CurrentActivity { get; set; } = "";
}
