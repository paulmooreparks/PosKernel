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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PosKernel.AI.Training.Configuration;
using PosKernel.AI.Training.Core;
using PosKernel.AI.Services;
using System.Text;
using Terminal.Gui;
using TGAttribute = Terminal.Gui.Attribute;

namespace PosKernel.AI.Training.TUI.Views;

/// <summary>
/// Main TUI application for AI training configuration
/// ARCHITECTURAL PRINCIPLE: UI contains NO training logic - complete separation of concerns
/// </summary>
public class TrainingConfigurationTUI
{
    private readonly TrainingConfigurationDialog _configDialog;
    private readonly ILogger<TrainingConfigurationTUI> _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _initialized = false;
    private Toplevel? _top;
    private TextView? _configView;
    private TextView? _logView;
    private Label? _statusBar;
    private readonly StringBuilder _logContent = new();
    private TrainingConsoleRedirector? _outRedirector;
    private TrainingConsoleRedirector? _errorRedirector;

    public TrainingConfigurationTUI(
        TrainingConfigurationDialog configDialog,
        ILogger<TrainingConfigurationTUI> logger,
        IServiceProvider serviceProvider)
    {
        _configDialog = configDialog ?? throw new ArgumentNullException(nameof(configDialog));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async Task RunAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }

        try
        {
            if (_top != null)
            {
                Application.Run(_top);
            }
        }
        finally
        {
            // Ensure console redirection is cleaned up when TUI exits
            await CleanupAsync();
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Cleans up console redirection and other resources
    /// ARCHITECTURAL PRINCIPLE: Proper resource cleanup to restore original console behavior
    /// </summary>
    private async Task CleanupAsync()
    {
        try
        {
            // Restore original console streams
            _outRedirector?.Dispose();
            _errorRedirector?.Dispose();
            _outRedirector = null;
            _errorRedirector = null;

            AddLog("Console redirection disabled - console output restored to normal");
        }
        catch (Exception ex)
        {
            // Don't let cleanup errors crash the application
            System.Console.WriteLine($"Warning: Error during TUI cleanup: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    private async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        Application.Init();

        _top = new Toplevel();

        // Use the same color scheme as PosKernel.AI
        _top.ColorScheme = new ColorScheme()
        {
            Normal = new TGAttribute(Color.Black, Color.White),
            Focus = new TGAttribute(Color.Black, Color.White),
            HotNormal = new TGAttribute(Color.BrightRed, Color.White),
            HotFocus = new TGAttribute(Color.BrightRed, Color.White),
            Disabled = new TGAttribute(Color.DarkGray, Color.White)
        };

        // Create menu bar
        var quitItem = new MenuItem("_Quit", "", () => Application.RequestStop());
        var configItem = new MenuItem("_Configure", "", () => ShowConfiguration());
        var validateItem = new MenuItem("_Validate", "", () => ValidateConfiguration());
        var startTrainingItem = new MenuItem("_Start Training", "", () => StartTraining());
        var viewPromptsItem = new MenuItem("View _Prompts", "", () => ViewTrainedPrompts());
        var fileMenu = new MenuBarItem("_File", new MenuItem[] { configItem, validateItem, startTrainingItem, viewPromptsItem, quitItem });
        
        var helpItem = new MenuItem("Show _Help", "", ShowHelp);
        var helpMenu = new MenuBarItem("_Help", new MenuItem[] { helpItem });

        var menuBar = new MenuBar();
        menuBar.Menus = new MenuBarItem[] { fileMenu, helpMenu };
        menuBar.Y = 0;
        
        menuBar.ColorScheme = new ColorScheme()
        {
            Normal = new TGAttribute(Color.Black, Color.Cyan),
            Focus = new TGAttribute(Color.White, Color.Blue),
            HotNormal = new TGAttribute(Color.BrightRed, Color.Cyan),
            HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
            Disabled = new TGAttribute(Color.Gray, Color.Cyan)
        };

        // Welcome label
        var welcomeLabel = new Label()
        {
            Text = "POS Kernel AI Training Configuration",
            X = 0,
            Y = Pos.Bottom(menuBar),
            Width = Dim.Fill()
        };

        welcomeLabel.ColorScheme = new ColorScheme()
        {
            Normal = new TGAttribute(Color.Black, Color.White),
            Focus = new TGAttribute(Color.Black, Color.White),
            HotNormal = new TGAttribute(Color.BrightRed, Color.White),
            HotFocus = new TGAttribute(Color.BrightRed, Color.White),
            Disabled = new TGAttribute(Color.DarkGray, Color.White)
        };

        // Configuration display area
        var configLabel = new Label()
        {
            Text = "Current Configuration:",
            X = 0,
            Y = Pos.Bottom(welcomeLabel) + 1,
            Width = Dim.Fill()
        };

        configLabel.ColorScheme = welcomeLabel.ColorScheme;

        _configView = new TextView()
        {
            X = 0,
            Y = Pos.Bottom(configLabel),
            Width = Dim.Fill(),
            Height = Dim.Percent(60),
            ReadOnly = true,
            WordWrap = true,
            CanFocus = true
        };

        _configView.ColorScheme = new ColorScheme()
        {
            Normal = new TGAttribute(Color.White, Color.Blue),
            Focus = new TGAttribute(Color.BrightYellow, Color.Blue),
            HotNormal = new TGAttribute(Color.BrightCyan, Color.Blue),
            HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
            Disabled = new TGAttribute(Color.Gray, Color.Blue)
        };

        // Log display area
        var logLabel = new Label()
        {
            Text = "Activity Log:",
            X = 0,
            Y = Pos.Bottom(_configView),
            Width = Dim.Fill()
        };

        logLabel.ColorScheme = welcomeLabel.ColorScheme;

        _logView = new TextView()
        {
            X = 0,
            Y = Pos.Bottom(logLabel),
            Width = Dim.Fill(),
            Height = Dim.Fill(1), // Fill remaining space, leave room for status bar
            ReadOnly = true,
            WordWrap = true,
            CanFocus = true
        };

        _logView.ColorScheme = _configView.ColorScheme;

        // Status bar
        _statusBar = new Label()
        {
            Text = "Press F1 for help, F2 to configure, F3 to validate, F4 to start training, F5 to view prompts",
            X = 0,
            Y = Pos.AnchorEnd(),
            Width = Dim.Fill()
        };

        _statusBar.ColorScheme = new ColorScheme()
        {
            Normal = new TGAttribute(Color.Black, Color.Cyan),
            Focus = new TGAttribute(Color.Black, Color.Cyan),
            HotNormal = new TGAttribute(Color.Black, Color.Cyan),
            HotFocus = new TGAttribute(Color.Black, Color.Cyan),
            Disabled = new TGAttribute(Color.Gray, Color.Cyan)
        };

        // Add keyboard shortcuts using correct Terminal.Gui v2 event signature
        _top.KeyDown += (_, e) =>
        {
            switch (e.KeyCode)
            {
                case KeyCode.F1:
                    ShowHelp();
                    e.Handled = true;
                    break;
                case KeyCode.F2:
                    ShowConfiguration();
                    e.Handled = true;
                    break;
                case KeyCode.F3:
                    ValidateConfiguration();
                    e.Handled = true;
                    break;
                case KeyCode.F4:
                    StartTraining();
                    e.Handled = true;
                    break;
                case KeyCode.F5:
                    ViewTrainedPrompts();
                    e.Handled = true;
                    break;
                case KeyCode.CtrlMask | KeyCode.Q:
                    Application.RequestStop();
                    e.Handled = true;
                    break;
            }
        };

        _top.Add(menuBar, welcomeLabel, configLabel, _configView, logLabel, _logView, _statusBar);

        _initialized = true;

        // Redirect console output to Activity Log window (same pattern as PosKernel.AI)
        _outRedirector = new TrainingConsoleRedirector(AddLog, System.Console.Out, "OUT");
        _errorRedirector = new TrainingConsoleRedirector(AddLog, System.Console.Error, "ERROR");
        System.Console.SetOut(_outRedirector);
        System.Console.SetError(_errorRedirector);

        AddLog("Console redirection enabled - all console output will appear in Activity Log");

        // Load and display initial configuration
        await LoadAndDisplayConfigurationAsync();
    }

    private async Task LoadAndDisplayConfigurationAsync()
    {
        try
        {
            AddLog("Loading training configuration...");
            
            // Test console redirection during configuration loading
            Console.WriteLine("Configuration: Checking for existing training configuration...");
            
            var config = await _configDialog.LoadConfigurationAsync();
            
            if (config != null)
            {
                Console.WriteLine($"Configuration: Loaded configuration with {config.ScenarioCount} scenarios");
                DisplayConfiguration(config);
                AddLog("Configuration loaded successfully");
            }
            else
            {
                Console.WriteLine("Configuration: No existing configuration found");
                DisplayMessage("No configuration found. Use F2 to create configuration.");
                AddLog("No existing configuration found");
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to load configuration: {ex.Message}";
            Console.Error.WriteLine($"Configuration Error: {errorMsg}");
            DisplayMessage(errorMsg);
            AddLog($"ERROR: {errorMsg}");
        }
    }

    private void DisplayConfiguration(TrainingConfiguration config)
    {
        var content = new StringBuilder();
        
        content.AppendLine("=== AI Training Configuration ===");
        content.AppendLine();
        
        content.AppendLine("CORE PARAMETERS:");
        content.AppendLine($"  Scenario Count: {config.ScenarioCount:N0}");
        content.AppendLine($"  Max Generations: {config.MaxGenerations:N0}");
        content.AppendLine($"  Improvement Threshold: {config.ImprovementThreshold:P2}");
        content.AppendLine($"  Validation Scenarios: {config.ValidationScenarios:N0}");
        content.AppendLine();
        
        content.AppendLine("FOCUS AREAS:");
        content.AppendLine($"  Tool Selection: {config.Focus.ToolSelectionAccuracy:P1}");
        content.AppendLine($"  Personality: {config.Focus.PersonalityAuthenticity:P1}");
        content.AppendLine($"  Payment Flow: {config.Focus.PaymentFlowCompletion:P1}");
        content.AppendLine($"  Contextual: {config.Focus.ContextualAppropriateness:P1}");
        content.AppendLine($"  Completeness: {config.Focus.InformationCompleteness:P1}");
        content.AppendLine($"  Value Optimization: {config.Focus.ValueOptimization:P1}");
        content.AppendLine();
        
        content.AppendLine("SCENARIO DISTRIBUTION:");
        content.AppendLine($"  Basic Ordering: {config.ScenarioMix.BasicOrdering:P1}");
        content.AppendLine($"  Edge Cases: {config.ScenarioMix.EdgeCases:P1}");
        content.AppendLine($"  Cultural Variations: {config.ScenarioMix.CulturalVariations:P1}");
        content.AppendLine($"  Ambiguous Requests: {config.ScenarioMix.AmbiguousRequests:P1}");
        content.AppendLine($"  Payment Scenarios: {config.ScenarioMix.PaymentScenarios:P1}");
        content.AppendLine();
        
        content.AppendLine("TRAINING AGGRESSIVENESS:");
        content.AppendLine($"  Mutation Rate: {config.Aggressiveness.MutationRate:P1}");
        content.AppendLine($"  Exploration Ratio: {config.Aggressiveness.ExplorationRatio:P1}");
        content.AppendLine($"  Regression Tolerance: {config.Aggressiveness.RegressionTolerance:P1}");
        content.AppendLine($"  Stagnation Limit: {config.Aggressiveness.StagnationLimit} generations");
        content.AppendLine();
        
        content.AppendLine("QUALITY TARGETS:");
        content.AppendLine($"  Conversation Completion: {config.QualityTargets.ConversationCompletion:P1}");
        content.AppendLine($"  Technical Accuracy: {config.QualityTargets.TechnicalAccuracy:P1}");
        content.AppendLine($"  Personality Consistency: {config.QualityTargets.PersonalityConsistency:P1}");
        content.AppendLine($"  Contextual Appropriateness: {config.QualityTargets.ContextualAppropriateness:P1}");
        content.AppendLine();
        
        content.AppendLine("SAFETY CONFIGURATION:");
        content.AppendLine($"  Max Training Duration: {config.Safety.MaxTrainingDuration}");
        content.AppendLine($"  Max Prompt Length: {config.Safety.MaxPromptLength:N0} characters");
        content.AppendLine($"  Human Approval Threshold: {config.Safety.HumanApprovalThreshold:P1}");
        content.AppendLine($"  Auto Backup Interval: {config.Safety.AutoBackupInterval}");

        Application.Invoke(() =>
        {
            if (_configView != null)
            {
                _configView.Text = content.ToString();
                _configView.MoveHome();
            }
            
            if (_statusBar != null)
            {
                _statusBar.Text = "Configuration loaded - Press F2 to edit, F3 to validate";
            }
        });
    }

    private void DisplayMessage(string message)
    {
        Application.Invoke(() =>
        {
            if (_configView != null)
            {
                _configView.Text = $"\n\n{message}\n\nUse the File menu or keyboard shortcuts to manage configuration.";
            }
        });
    }

    private void ShowConfiguration()
    {
        try
        {
            AddLog("Opening configuration dialog...");
            UpdateStatus("Configuring...");
            
            // Use synchronous call - no need for async in TUI event handlers
            var result = _configDialog.ShowDialogAsync().GetAwaiter().GetResult();
            
            if (result != null)
            {
                DisplayConfiguration(result);
                AddLog("Configuration updated successfully");
                UpdateStatus("Configuration saved - Ready for training");
                
                // Show success message
                MessageBox.Query("Configuration Saved",
                    "Training configuration has been saved successfully.\n\n" +
                    "The system is now ready for AI training operations.",
                    "OK");
            }
            else
            {
                AddLog("Configuration dialog cancelled");
                UpdateStatus("Configuration cancelled");
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Configuration failed: {ex.Message}";
            AddLog($"ERROR: {errorMsg}");
            UpdateStatus("Configuration failed");
            
            MessageBox.ErrorQuery("Configuration Error",
                $"Failed to configure training:\n\n{ex.Message}",
                "OK");
        }
    }

    private void ValidateConfiguration()
    {
        try
        {
            AddLog("Validating configuration...");
            UpdateStatus("Validating...");
            
            // Use synchronous call - no need for async in TUI event handlers
            var result = _configDialog.ValidateCurrentConfigurationAsync().GetAwaiter().GetResult();
            
            if (result != null)
            {
                var status = result.IsValid ? "VALID" : "INVALID";
                var message = $"Configuration Status: {status}\n\n";
                
                if (result.Errors.Any())
                {
                    message += "Errors:\n";
                    foreach (var error in result.Errors)
                    {
                        message += $"• {error}\n";
                    }
                    message += "\n";
                }
                
                if (result.Warnings.Any())
                {
                    message += "Warnings:\n";
                    foreach (var warning in result.Warnings)
                    {
                        message += $"• {warning}\n";
                    }
                    message += "\n";
                }
                
                if (result.IsValid && !result.Warnings.Any())
                {
                    message += "Configuration is valid with no issues!";
                }

                AddLog($"Validation complete: {status}");
                UpdateStatus($"Validation complete: {status}");
                
                MessageBox.Query("Validation Results", message, "OK");
            }
            else
            {
                AddLog("No configuration to validate");
                UpdateStatus("No configuration found");
                
                MessageBox.ErrorQuery("Validation Error",
                    "No configuration found to validate.\n\nPlease create a configuration first.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Validation failed: {ex.Message}";
            AddLog($"ERROR: {errorMsg}");
            UpdateStatus("Validation failed");
            
            MessageBox.ErrorQuery("Validation Error",
                $"Failed to validate configuration:\n\n{ex.Message}",
                "OK");
        }
    }

    private void AddLog(string message) {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logEntry = $"[{timestamp}] {message}";

        _logContent.AppendLine(logEntry);

        if (_logView != null) {
            _logView.Text = _logContent.ToString();
            _logView.MoveEnd(); // Auto-scroll to bottom
        }
    }

    private void UpdateStatus(string status)
    {
        Application.Invoke(() =>
        {
            if (_statusBar != null)
            {
                _statusBar.Text = status;
            }
        });
    }

    private static void ShowHelp()
    {
        MessageBox.Query("Help",
            "POS Kernel AI Training Configuration Tool\n\n" +
            "This application manages configuration for AI training sessions.\n\n" +
            "Keyboard Shortcuts:\n" +
            "• F1 - Show this help\n" +
            "• F2 - Configure training parameters\n" +
            "• F3 - Validate current configuration\n" +
            "• F4 - Start training session\n" +
            "• F5 - View trained prompts\n" +
            "• Tab - Move between panels\n" +
            "• Arrow Keys - Scroll in panels\n" +
            "• Ctrl+Q - Quit application\n\n" +
            "Menu Options:\n" +
            "• File → Configure - Set training parameters\n" +
            "• File → Validate - Check configuration validity\n" +
            "• File → Start Training - Begin AI training session\n" +
            "• File → View Prompts - View saved trained prompts\n" +
            "• File → Quit - Exit application\n\n" +
            "Features:\n" +
            "• Multi-dimensional training configuration\n" +
            "• Comprehensive validation with error reporting\n" +
            "• Cross-platform data storage\n" +
            "• Console output capture in Activity Log\n" +
            "• Real-time training progress monitoring\n" +
            "• Fail-fast architectural compliance",
            "OK");
    }

    private void StartTraining()
    {
        AddLog("Starting training session...");
        UpdateStatus("Loading training configuration...");

        // Demonstrate console redirection is working
        Console.WriteLine("Testing console output redirection...");
        Console.Error.WriteLine("Testing error output redirection...");
            
        // Load current configuration
        var config = _configDialog.LoadConfigurationAsync().GetAwaiter().GetResult();
            
        if (config == null)
        {
            AddLog("No configuration found - creating default");
            MessageBox.Query("No Configuration", 
                "No training configuration found.\n\nPlease configure training parameters first (F2).",
                "OK");
            return;
        }

        // Validate configuration before training
        var validation = _configDialog.ValidateCurrentConfigurationAsync().GetAwaiter().GetResult();
        if (validation == null || !validation.IsValid)
        {
            var errors = validation?.Errors.Any() == true 
                ? string.Join("\n", validation.Errors)
                : "Unknown validation error";
                    
            AddLog($"Configuration validation failed: {errors}");
            MessageBox.ErrorQuery("Invalid Configuration",
                $"Training configuration has errors:\n\n{errors}\n\nPlease fix configuration first (F2).",
                "OK");
            return;
        }

        // Show warnings if any
        if (validation.Warnings.Any())
        {
            var warnings = string.Join("\n", validation.Warnings);
            var proceed = MessageBox.Query("Configuration Warnings",
                $"Configuration has warnings:\n\n{warnings}\n\nProceed with training anyway?",
                "Yes", "No") == 0;
                    
            if (!proceed)
            {
                AddLog("Training cancelled due to configuration warnings");
                return;
            }
        }

        // Create training session
        var trainingSession = _serviceProvider.GetRequiredService<ITrainingSession>();
        var sessionLogger = _serviceProvider.GetRequiredService<ILogger<TrainingSessionDialog>>();
        var sessionDialog = new TrainingSessionDialog(trainingSession, sessionLogger);
            
        AddLog($"Starting training with {config.ScenarioCount} scenarios, {config.MaxGenerations} generations");
        UpdateStatus("Training session starting...");
            
        // Show training dialog (this will block until training completes)
        sessionDialog.ShowTrainingDialog(config);
            
        AddLog("Training session dialog closed");
        UpdateStatus("Training session completed");
    }

    private void ViewTrainedPrompts()
    {
        try
        {
            AddLog("Loading prompt optimization history...");
            UpdateStatus("Loading prompt optimization history...");

            // Get prompt management service
            var promptService = _serviceProvider.GetRequiredService<IPromptManagementService>();
            
            // Load prompt optimization records
            var personalityType = PersonalityType.SingaporeanKopitiamUncle; // Default for demo
            var promptType = "ordering"; // Most optimized prompt type
            var records = promptService.GetPromptHistoryAsync(personalityType, promptType, 10).GetAwaiter().GetResult();
            
            if (!records.Any())
            {
                AddLog("No prompt optimizations found");
                MessageBox.Query("No Prompt Optimizations", 
                    "No prompt optimizations found.\n\nRun a training session (F4) to generate optimized prompts.",
                    "OK");
                return;
            }

            AddLog($"Found {records.Count} prompt optimizations");
            
            // Show prompt optimization history
            ShowPromptOptimizationsDialog(records, personalityType, promptType);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to load prompt optimizations: {ex.Message}";
            AddLog($"ERROR: {errorMsg}");
            UpdateStatus("Failed to load prompt optimizations");
            
            MessageBox.ErrorQuery("Error Loading Prompt Optimizations",
                $"Failed to load prompt optimizations:\n\n{ex.Message}",
                "OK");
        }
    }

    private void ShowPromptOptimizationsDialog(IReadOnlyList<PromptOptimizationRecord> records, PersonalityType personalityType, string promptType)
    {
        Application.Invoke(() =>
        {
            var dialog = new Window()
            {
                Title = $"Prompt Optimizations - {personalityType}/{promptType}",
                X = Pos.Center(),
                Y = Pos.Center(),
                Width = 120,
                Height = 30,
                Modal = true
            };

            dialog.ColorScheme = new ColorScheme()
            {
                Normal = new TGAttribute(Color.Black, Color.White),
                Focus = new TGAttribute(Color.Black, Color.White),
                HotNormal = new TGAttribute(Color.BrightRed, Color.White),
                HotFocus = new TGAttribute(Color.BrightRed, Color.White),
                Disabled = new TGAttribute(Color.DarkGray, Color.White)
            };

            var instructionLabel = new Label()
            {
                Text = $"Found {records.Count} prompt optimizations (best first). Scroll to see details:",
                X = 2,
                Y = 1,
                Width = Dim.Fill(2)
            };

            // Create simple text display of optimizations
            var optimizationDisplay = string.Join("\n\n", records.Select((r, i) => 
            {
                // TRAINING ENHANCEMENT: Extract change information from notes if available
                var changesSummary = "No change details available";
                if (!string.IsNullOrEmpty(r.Notes) && r.Notes.Contains("Changes: "))
                {
                    var changesIndex = r.Notes.IndexOf("Changes: ");
                    if (changesIndex >= 0 && changesIndex + 9 < r.Notes.Length)
                    {
                        // Safe substring extraction with bounds checking
                        var startIndex = changesIndex + 9;
                        if (startIndex < r.Notes.Length)
                        {
                            changesSummary = r.Notes.Substring(startIndex);
                        }
                    }
                }
                
                return $"#{i + 1} - Score: {r.PerformanceScore:F3} | Session: {r.TrainingSessionId} | {r.Timestamp:yyyy-MM-dd HH:mm}\n" +
                       $"    Quality Metrics: {string.Join(", ", r.QualityMetrics.Select(kv => $"{kv.Key}={kv.Value:F2}"))}\n" +
                       $"    Changes Made: {changesSummary}";
            }));

            var textView = new TextView()
            {
                Text = optimizationDisplay,
                X = 2,
                Y = 3,
                Width = Dim.Fill(2),
                Height = Dim.Fill(4),
                ReadOnly = true,
                WordWrap = true
            };

            textView.ColorScheme = new ColorScheme()
            {
                Normal = new TGAttribute(Color.White, Color.Blue),
                Focus = new TGAttribute(Color.BrightYellow, Color.Blue),
                HotNormal = new TGAttribute(Color.BrightCyan, Color.Blue),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
                Disabled = new TGAttribute(Color.Gray, Color.Blue)
            };

            var viewButton = new Button()
            {
                Text = " View Current Prompt ",
                X = 5,
                Y = Pos.AnchorEnd(2),
                Width = 20
            };

            var closeButton = new Button()
            {
                Text = " Close ",
                X = 30,
                Y = Pos.AnchorEnd(2),
                Width = 10
            };

            var buttonColorScheme = new ColorScheme()
            {
                Normal = new TGAttribute(Color.White, Color.Blue),
                Focus = new TGAttribute(Color.BrightYellow, Color.Blue),
                HotNormal = new TGAttribute(Color.BrightCyan, Color.Blue),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
                Disabled = new TGAttribute(Color.Gray, Color.Blue)
            };

            viewButton.ColorScheme = buttonColorScheme;
            closeButton.ColorScheme = buttonColorScheme;

            viewButton.MouseClick += (_, e) =>
            {
                ShowCurrentPromptDialog(personalityType, promptType);
            };

            closeButton.MouseClick += (_, e) => Application.RequestStop(dialog);

            dialog.KeyDown += (_, e) =>
            {
                switch (e.KeyCode)
                {
                    case KeyCode.Esc:
                        Application.RequestStop(dialog);
                        e.Handled = true;
                        break;
                }
            };

            dialog.Add(instructionLabel, textView, viewButton, closeButton);

            Application.Run(dialog);
        });
    }

    private void ShowCurrentPromptDialog(PersonalityType personalityType, string promptType)
    {
        try
        {
            var promptService = _serviceProvider.GetRequiredService<IPromptManagementService>();
            var context = new PromptContext 
            { 
                TimeOfDay = "afternoon", 
                CurrentTime = DateTime.Now.ToString("HH:mm") 
            };
            
            var currentPrompt = promptService.GetCurrentPromptAsync(personalityType, promptType, context).GetAwaiter().GetResult();
            
            var dialog = new Window()
            {
                Title = $"Current Production Prompt - {personalityType}/{promptType}",
                X = Pos.Center(),
                Y = Pos.Center(),
                Width = 100,
                Height = 25,
                Modal = true
            };

            var textView = new TextView()
            {
                Text = $"CURRENT PRODUCTION PROMPT:\n\n{currentPrompt}",
                X = 1,
                Y = 1,
                Width = Dim.Fill(1),
                Height = Dim.Fill(3),
                ReadOnly = true,
                WordWrap = true
            };

            textView.ColorScheme = new ColorScheme()
            {
                Normal = new TGAttribute(Color.White, Color.Blue),
                Focus = new TGAttribute(Color.BrightYellow, Color.Blue),
                HotNormal = new TGAttribute(Color.BrightCyan, Color.Blue),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
                Disabled = new TGAttribute(Color.Gray, Color.Blue)
            };

            var closeButton = new Button()
            {
                Text = " Close ",
                X = Pos.Center(),
                Y = Pos.AnchorEnd(1),
                Width = 10
            };

            closeButton.MouseClick += (_, e) => Application.RequestStop(dialog);
            
            dialog.KeyDown += (_, e) =>
            {
                if (e.KeyCode == KeyCode.Esc || e.KeyCode == KeyCode.Enter)
                {
                    Application.RequestStop(dialog);
                    e.Handled = true;
                }
            };

            dialog.Add(textView, closeButton);

            Application.Run(dialog);
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", 
                $"Failed to load current prompt:\n\n{ex.Message}",
                "OK");
        }
    }
}
