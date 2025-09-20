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
using PosKernel.AI.Training.Configuration;
using Terminal.Gui;
using TGAttribute = Terminal.Gui.Attribute;

namespace PosKernel.AI.Training.TUI.Views;

/// <summary>
/// Training configuration dialog using proper Terminal.Gui 2.0.0 API
/// ARCHITECTURAL PRINCIPLE: UI contains NO training logic - complete separation of concerns
/// </summary>
public class TrainingConfigurationDialog
{
    private readonly ITrainingConfigurationService _configService;
    private readonly ILogger<TrainingConfigurationDialog> _logger;

    public TrainingConfigurationDialog(
        ITrainingConfigurationService configService,
        ILogger<TrainingConfigurationDialog> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads the current configuration without showing a dialog
    /// </summary>
    public async Task<TrainingConfiguration?> LoadConfigurationAsync()
    {
        try
        {
            return await _configService.LoadConfigurationAsync();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("DESIGN DEFICIENCY"))
        {
            // No configuration exists
            return null;
        }
    }

    /// <summary>
    /// Validates the current configuration
    /// </summary>
    public async Task<ValidationResult?> ValidateCurrentConfigurationAsync()
    {
        var config = await LoadConfigurationAsync();
        if (config == null)
        {
            return null;
        }
        
        return await _configService.ValidateConfigurationAsync(config);
    }

    /// <summary>
    /// Shows the configuration dialog and returns the updated configuration, or null if cancelled
    /// </summary>
    public async Task<TrainingConfiguration?> ShowDialogAsync()
    {
        TrainingConfiguration? config;

        try
        {
            _logger.LogDebug("ShowDialogAsync: Starting configuration dialog");
            
            // Try to load existing configuration
            _logger.LogDebug("ShowDialogAsync: About to load existing configuration");
            config = await LoadConfigurationAsync();
            
            if (config == null)
            {
                _logger.LogDebug("ShowDialogAsync: No existing config found, creating default");
                // Create default configuration for first-time setup
                config = _configService.CreateDefaultConfiguration();
                _logger.LogDebug("ShowDialogAsync: Created default configuration successfully");
            }
            else
            {
                _logger.LogDebug("ShowDialogAsync: Loaded existing configuration successfully");
            }

            _logger.LogDebug("ShowDialogAsync: About to show radio group dialog");
            var result = ShowRadioGroupDialog(config);
            _logger.LogDebug("ShowDialogAsync: Radio group dialog completed, result: {HasResult}", result != null);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ShowDialogAsync: Failed to show configuration dialog: {Error}", ex.Message);
            
            // Show error dialog
            var createDefault = MessageBox.Query("Configuration Error",
                $"Failed to load configuration:\n\n{ex.Message}\n\nCreate default configuration?",
                "Yes", "No") == 0;
            
            if (createDefault)
            {
                try
                {
                    _logger.LogDebug("ShowDialogAsync: Creating default configuration due to error");
                    var defaultConfig = _configService.CreateDefaultConfiguration();
                    await _configService.SaveConfigurationAsync(defaultConfig);
                    _logger.LogDebug("ShowDialogAsync: Successfully saved default configuration");
                    return defaultConfig;
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "ShowDialogAsync: Failed to create default configuration");
                    MessageBox.ErrorQuery("Error",
                        $"Failed to create default configuration:\n\n{saveEx.Message}",
                        "OK");
                }
            }
            
            return null;
        }
    }

    private TrainingConfiguration? ShowRadioGroupDialog(TrainingConfiguration config)
    {
        try
        {
            _logger.LogDebug("ShowRadioGroupDialog: Starting with config type: {ConfigType}", config.GetType().Name);
            
            TrainingConfiguration? selectedConfig = null;
            var dialogCancelled = false;

            _logger.LogDebug("ShowRadioGroupDialog: About to invoke Application.Invoke");
            
            Application.Invoke(() =>
            {
                try
                {
                    _logger.LogDebug("ShowRadioGroupDialog: Inside Application.Invoke - creating dialog window");
                    
                    // Create dialog window with proper Terminal.Gui v2 API
                    var dialog = new Window()
                    {
                        Title = "Training Configuration",
                        X = Pos.Center(),
                        Y = Pos.Center(),
                        Width = 70,
                        Height = 20,
                        Modal = true
                    };

                    _logger.LogDebug("ShowRadioGroupDialog: Dialog window created, setting color scheme");

                    dialog.ColorScheme = new ColorScheme()
                    {
                        Normal = new TGAttribute(Color.Black, Color.White),
                        Focus = new TGAttribute(Color.Black, Color.White),
                        HotNormal = new TGAttribute(Color.BrightRed, Color.White),
                        HotFocus = new TGAttribute(Color.BrightRed, Color.White),
                        Disabled = new TGAttribute(Color.DarkGray, Color.White)
                    };

                    _logger.LogDebug("ShowRadioGroupDialog: Creating instruction label");

                    // Add instruction label
                    var instructionLabel = new Label()
                    {
                        Text = "Select action for training configuration:",
                        X = 2,
                        Y = 1,
                        Width = Dim.Fill(2),
                        Height = 1
                    };

                    _logger.LogDebug("ShowRadioGroupDialog: Creating radio group");

                    // Create radio group with options
                    var options = new[] 
                    {
                        "Use current configuration (if valid)",
                        "Create and save default configuration",
                        "View configuration details only"
                    };

                    var radioGroup = new RadioGroup()
                    {
                        X = 2,
                        Y = 3,
                        Width = Dim.Fill(2),
                        Height = options.Length,
                        RadioLabels = options
                    };

                    // Pre-select first option
                    radioGroup.SelectedItem = 0;

                    _logger.LogDebug("ShowRadioGroupDialog: Creating buttons");

                    // Create OK button - following PosKernel.AI patterns
                    var okButton = new Button()
                    {
                        Text = " OK ",
                        Width = 10,
                        X = Pos.Center() - 8,
                        Y = Pos.AnchorEnd(2),
                        IsDefault = true
                    };

                    // Create Cancel button  
                    var cancelButton = new Button()
                    {
                        Text = " Cancel ",
                        Width = 10,
                        X = Pos.Center() + 2,
                        Y = Pos.AnchorEnd(2)
                    };

                    // Apply button styling like PosKernel.AI
                    var buttonColorScheme = new ColorScheme()
                    {
                        Normal = new TGAttribute(Color.White, Color.Blue),
                        Focus = new TGAttribute(Color.BrightYellow, Color.Blue),
                        HotNormal = new TGAttribute(Color.BrightCyan, Color.Blue),
                        HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
                        Disabled = new TGAttribute(Color.Gray, Color.Blue)
                    };

                    okButton.ColorScheme = buttonColorScheme;
                    cancelButton.ColorScheme = buttonColorScheme;

                    _logger.LogDebug("ShowRadioGroupDialog: Setting up event handlers");

                    // Create handlers for the actions
                    void HandleOkAction()
                    {
                        try
                        {
                            _logger.LogDebug("HandleOkAction: Starting with selected item: {SelectedItem}", radioGroup.SelectedItem);
                            
                            var selectedIndex = radioGroup.SelectedItem;
                            
                            switch (selectedIndex)
                            {
                                case 0: // Use current
                                    _logger.LogDebug("HandleOkAction: Case 0 - Validating current configuration");
                                    // Synchronous validation - no need for async here
                                    var validation = _configService.ValidateConfigurationAsync(config).GetAwaiter().GetResult();
                                    if (validation.IsValid)
                                    {
                                        _logger.LogDebug("HandleOkAction: Configuration is valid, setting result");
                                        selectedConfig = config;
                                        Application.RequestStop(dialog);
                                    }
                                    else
                                    {
                                        _logger.LogDebug("HandleOkAction: Configuration invalid, showing errors");
                                        var errors = string.Join("\n", validation.Errors);
                                        MessageBox.ErrorQuery("Validation Failed",
                                            $"Current configuration has errors:\n\n{errors}",
                                            "OK");
                                    }
                                    break;
                                    
                                case 1: // Create default
                                    _logger.LogDebug("HandleOkAction: Case 1 - Creating default configuration");
                                    var defaultConfig = _configService.CreateDefaultConfiguration();
                                    // Synchronous save - no need for async here
                                    _configService.SaveConfigurationAsync(defaultConfig).GetAwaiter().GetResult();
                                    selectedConfig = defaultConfig;
                                    Application.RequestStop(dialog);
                                    break;
                                    
                                case 2: // View details
                                    _logger.LogDebug("HandleOkAction: Case 2 - Showing configuration details");
                                    ShowConfigurationDetails(config);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "HandleOkAction: Error in OK handler");
                            MessageBox.ErrorQuery("Error", $"Operation failed:\n\n{ex.Message}", "OK");
                        }
                    }

                    void HandleCancelAction()
                    {
                        _logger.LogDebug("HandleCancelAction: User cancelled dialog");
                        dialogCancelled = true;
                        selectedConfig = null;
                        Application.RequestStop(dialog);
                    }

                    // Wire up events using working patterns from PosKernel.AI
                    okButton.MouseClick += (_, e) => {
                        _logger.LogDebug("ShowRadioGroupDialog: OK button clicked");
                        HandleOkAction();
                    };
                    
                    cancelButton.MouseClick += (_, e) => {
                        _logger.LogDebug("ShowRadioGroupDialog: Cancel button clicked");
                        HandleCancelAction();
                    };

                    // Handle keyboard shortcuts
                    dialog.KeyDown += (_, e) =>
                    {
                        _logger.LogDebug("ShowRadioGroupDialog: Key pressed: {Key}", e.KeyCode);
                        switch (e.KeyCode)
                        {
                            case KeyCode.Enter:
                                HandleOkAction();
                                e.Handled = true;
                                break;
                            case KeyCode.Esc:
                                HandleCancelAction();
                                e.Handled = true;
                                break;
                        }
                    };

                    _logger.LogDebug("ShowRadioGroupDialog: Adding components to dialog");
                    dialog.Add(instructionLabel, radioGroup, okButton, cancelButton);

                    _logger.LogDebug("ShowRadioGroupDialog: About to run dialog");
                    Application.Run(dialog);
                    _logger.LogDebug("ShowRadioGroupDialog: Dialog finished running");

                    if (!dialogCancelled && selectedConfig == null)
                    {
                        _logger.LogDebug("ShowRadioGroupDialog: Dialog not cancelled but no config selected, setting null");
                        selectedConfig = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ShowRadioGroupDialog: Exception in Application.Invoke");
                    throw;
                }
            });

            _logger.LogDebug("ShowRadioGroupDialog: Completed, result: {HasResult}", selectedConfig != null);
            
            return selectedConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ShowRadioGroupDialog: Exception in method");
            throw;
        }
    }

    private static void ShowConfigurationDetails(TrainingConfiguration config)
    {
        var details = $"Training Configuration Details:\n\n" +
                     $"Core Parameters:\n" +
                     $"• Scenario Count: {config.ScenarioCount:N0}\n" +
                     $"• Max Generations: {config.MaxGenerations:N0}\n" +
                     $"• Improvement Threshold: {config.ImprovementThreshold:P2}\n" +
                     $"• Validation Scenarios: {config.ValidationScenarios:N0}\n\n" +
                     $"Focus Areas:\n" +
                     $"• Tool Selection: {config.Focus.ToolSelectionAccuracy:P1}\n" +
                     $"• Personality: {config.Focus.PersonalityAuthenticity:P1}\n" +
                     $"• Payment Flow: {config.Focus.PaymentFlowCompletion:P1}\n\n" +
                     $"Safety:\n" +
                     $"• Max Duration: {config.Safety.MaxTrainingDuration}\n" +
                     $"• Max Prompt Length: {config.Safety.MaxPromptLength:N0}\n" +
                     $"• Human Approval: {config.Safety.HumanApprovalThreshold:P1}";

        MessageBox.Query("Configuration Details", details, "OK");
    }
}
