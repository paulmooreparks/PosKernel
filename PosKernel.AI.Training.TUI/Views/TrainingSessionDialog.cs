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
using PosKernel.AI.Training.Core;
using System.Text;
using Terminal.Gui;
using TGAttribute = Terminal.Gui.Attribute;

namespace PosKernel.AI.Training.TUI.Views;

/// <summary>
/// Training session monitoring dialog for real-time progress tracking
/// ARCHITECTURAL PRINCIPLE: UI contains NO training logic - pure event-driven display
/// </summary>
public class TrainingSessionDialog
{
    private readonly ITrainingSession _trainingSession;
    private readonly ILogger<TrainingSessionDialog> _logger;
    private readonly StringBuilder _eventLog = new();
    
    // UI Components
    private Window? _dialog;
    private TextView? _progressView;
    private TextView? _eventLogView;
    private Label? _statusLabel;
    private Button? _pauseResumeButton;
    private Button? _abortButton;
    private Button? _closeButton;
    private ProgressBar? _overallProgressBar;
    private ProgressBar? _generationProgressBar;
    
    // Session state
    private CancellationTokenSource? _cancellationTokenSource;
    private Task<TrainingResults>? _trainingTask;
    private TrainingConfiguration? _configuration;
    private bool _sessionCompleted = false;

    public TrainingSessionDialog(
        ITrainingSession trainingSession,
        ILogger<TrainingSessionDialog> logger)
    {
        _trainingSession = trainingSession ?? throw new ArgumentNullException(nameof(trainingSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to training events
        _trainingSession.ProgressUpdated += OnProgressUpdated;
        _trainingSession.ScenarioTested += OnScenarioTested;
        _trainingSession.GenerationComplete += OnGenerationComplete;
        _trainingSession.TrainingComplete += OnTrainingComplete;
    }

    /// <summary>
    /// Shows the training session dialog and runs training with the specified configuration
    /// </summary>
    public void ShowTrainingDialog(TrainingConfiguration config)
    {
        _configuration = config ?? throw new ArgumentNullException(nameof(config));
        _logger.LogDebug("ShowTrainingDialog: Starting training session dialog");

        Application.Invoke(() =>
        {
            try
            {
                CreateDialog();
                StartTrainingSession();
                
                // ARCHITECTURAL FIX: Ensure dialog is not null before running
                if (_dialog != null)
                {
                    Application.Run(_dialog);
                }
                else
                {
                    throw new InvalidOperationException(
                        "DESIGN DEFICIENCY: Dialog creation failed - dialog is null. " +
                        "Cannot run training session dialog without properly created dialog window.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ShowTrainingDialog: Exception showing training dialog");
                MessageBox.ErrorQuery("Training Error",
                    $"Failed to show training dialog:\n\n{ex.Message}",
                    "OK");
            }
        });
    }

    private void CreateDialog()
    {
        _dialog = new Window()
        {
            Title = "AI Training Session",
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = 100,
            Height = 35,
            Modal = true
        };

        _dialog.ColorScheme = new ColorScheme()
        {
            Normal = new TGAttribute(Color.Black, Color.White),
            Focus = new TGAttribute(Color.Black, Color.White),
            HotNormal = new TGAttribute(Color.BrightRed, Color.White),
            HotFocus = new TGAttribute(Color.BrightRed, Color.White),
            Disabled = new TGAttribute(Color.DarkGray, Color.White)
        };

        // Status label
        _statusLabel = new Label()
        {
            Text = "Initializing training session...",
            X = 2,
            Y = 1,
            Width = Dim.Fill(2),
            Height = 1
        };

        // Overall progress bar
        var overallLabel = new Label()
        {
            Text = "Overall Progress:",
            X = 2,
            Y = 3,
            Width = 20
        };

        _overallProgressBar = new ProgressBar()
        {
            X = 22,
            Y = 3,
            Width = Dim.Fill(2),
            Height = 1
        };

        // Generation progress bar
        var generationLabel = new Label()
        {
            Text = "Generation Progress:",
            X = 2,
            Y = 5,
            Width = 20
        };

        _generationProgressBar = new ProgressBar()
        {
            X = 22,
            Y = 5,
            Width = Dim.Fill(2),
            Height = 1
        };

        // Progress details
        var progressLabel = new Label()
        {
            Text = "Progress Details:",
            X = 2,
            Y = 7
        };

        _progressView = new TextView()
        {
            X = 2,
            Y = 8,
            Width = Dim.Percent(45),
            Height = 12,
            ReadOnly = true,
            WordWrap = true
        };

        _progressView.ColorScheme = new ColorScheme()
        {
            Normal = new TGAttribute(Color.White, Color.Blue),
            Focus = new TGAttribute(Color.BrightYellow, Color.Blue),
            HotNormal = new TGAttribute(Color.BrightCyan, Color.Blue),
            HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
            Disabled = new TGAttribute(Color.Gray, Color.Blue)
        };

        // Event log
        var eventLogLabel = new Label()
        {
            Text = "Recent Events:",
            X = Pos.Percent(55),
            Y = 7
        };

        _eventLogView = new TextView()
        {
            X = Pos.Percent(55),
            Y = 8,
            Width = Dim.Percent(43),
            Height = 12,
            ReadOnly = true,
            WordWrap = true
        };

        _eventLogView.ColorScheme = _progressView.ColorScheme;

        // Control buttons
        _pauseResumeButton = new Button()
        {
            Text = " Pause ",
            X = 5,
            Y = Pos.AnchorEnd(2),
            Width = 10
        };

        _abortButton = new Button()
        {
            Text = " Abort ",
            X = 20,
            Y = Pos.AnchorEnd(2),
            Width = 10
        };

        _closeButton = new Button()
        {
            Text = " Close ",
            X = 35,
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

        _pauseResumeButton.ColorScheme = buttonColorScheme;
        _abortButton.ColorScheme = buttonColorScheme;
        _closeButton.ColorScheme = buttonColorScheme;

        // Wire up button events
        _pauseResumeButton.MouseClick += (_, e) => HandlePauseResumeClick();
        _abortButton.MouseClick += (_, e) => HandleAbortClick();
        _closeButton.MouseClick += (_, e) => HandleCloseClick();

        // Initially disable close button
        _closeButton.Enabled = false;

        _dialog.Add(_statusLabel, overallLabel, _overallProgressBar, generationLabel, _generationProgressBar,
                   progressLabel, _progressView, eventLogLabel, _eventLogView,
                   _pauseResumeButton, _abortButton, _closeButton);

        _logger.LogDebug("CreateDialog: Training dialog created successfully");
    }

    private void StartTrainingSession()
    {
        if (_configuration == null)
        {
            throw new InvalidOperationException("DESIGN DEFICIENCY: Configuration not set before starting training session.");
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _sessionCompleted = false;
        
        AddEventLog("Training session starting...");
        UpdateStatus("Starting training session...");

        // Start training in background task
        _trainingTask = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Starting training session with {ScenarioCount} scenarios, {MaxGenerations} generations", 
                    _configuration.ScenarioCount, _configuration.MaxGenerations);
                
                var results = await _trainingSession.RunTrainingSessionAsync(_configuration, _cancellationTokenSource.Token);
                
                _logger.LogInformation("Training session completed successfully: {SessionId}, Final Score: {FinalScore:F3}", 
                    results.SessionId, results.FinalScore);
                
                return results;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Training session was cancelled by user");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Training session failed: {Error}", ex.Message);
                Application.Invoke(() =>
                {
                    AddEventLog($"Training failed: {ex.Message}");
                    UpdateStatus("Training failed");
                });
                throw;
            }
        });

        _logger.LogDebug("StartTrainingSession: Training task started");
    }

    private void HandlePauseResumeClick()
    {
        try
        {
            if (_trainingSession.State == TrainingSessionState.Running)
            {
                _trainingSession.PauseAsync();
                _pauseResumeButton!.Text = " Resume ";
                AddEventLog("Training paused by user");
                UpdateStatus("Training paused");
            }
            else if (_trainingSession.State == TrainingSessionState.Paused)
            {
                _trainingSession.ResumeAsync();
                _pauseResumeButton!.Text = " Pause ";
                AddEventLog("Training resumed by user");
                UpdateStatus("Training resumed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandlePauseResumeClick: Error handling pause/resume");
            AddEventLog($"Error: {ex.Message}");
        }
    }

    private void HandleAbortClick()
    {
        var result = MessageBox.Query("Abort Training",
            "Are you sure you want to abort the training session?\n\nThis will stop training and cannot be undone.",
            "Yes", "No");

        if (result == 0)
        {
            try
            {
                _trainingSession.AbortAsync();
                _cancellationTokenSource?.Cancel();
                AddEventLog("Training aborted by user");
                UpdateStatus("Aborting training...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HandleAbortClick: Error aborting training");
                AddEventLog($"Error aborting: {ex.Message}");
            }
        }
    }

    private void HandleCloseClick()
    {
        if (!_sessionCompleted)
        {
            var result = MessageBox.Query("Close Training Dialog",
                "Training session is still active.\n\nAre you sure you want to close this dialog?",
                "Yes", "No");

            if (result != 0)
            {
                return;
            }
        }

        // ARCHITECTURAL FIX: Ensure dialog is not null before requesting stop
        if (_dialog != null)
        {
            Application.RequestStop(_dialog);
        }
        else
        {
            _logger.LogWarning("HandleCloseClick: Dialog is null - cannot request stop");
        }
    }

    private void OnProgressUpdated(object? sender, TrainingProgressEventArgs e)
    {
        Application.Invoke(() =>
        {
            try
            {
                UpdateStatus($"Generation {e.Generation}: {e.CurrentActivity}");
                
                // Update progress bars
                _overallProgressBar!.Fraction = (float)e.Progress;
                var generationProgress = e.TotalScenarios > 0 ? (float)e.CurrentScenario / e.TotalScenarios : 0f;
                _generationProgressBar!.Fraction = generationProgress;

                // Update progress details
                var progressDetails = new StringBuilder();
                progressDetails.AppendLine($"Generation: {e.Generation}");
                progressDetails.AppendLine($"Scenario: {e.CurrentScenario}/{e.TotalScenarios}");
                progressDetails.AppendLine($"Progress: {e.Progress:P1}");
                progressDetails.AppendLine($"Current Score: {e.CurrentScore:F3}");
                progressDetails.AppendLine($"Best Score: {e.BestScore:F3}");
                progressDetails.AppendLine($"Improvement: {e.Improvement:F3}");
                progressDetails.AppendLine($"Elapsed: {e.ElapsedTime:hh\\:mm\\:ss}");
                progressDetails.AppendLine($"Remaining: {e.EstimatedRemaining:hh\\:mm\\:ss}");

                _progressView!.Text = progressDetails.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnProgressUpdated: Error updating progress display");
            }
        });
    }

    private void OnScenarioTested(object? sender, ScenarioTestEventArgs e)
    {
        Application.Invoke(() =>
        {
            AddEventLog($"Gen {e.Generation}: Scenario {e.ScenarioIndex} - Score: {e.Score:F3} ({(e.IsSuccessful ? "✓" : "✗")})");
        });
    }

    private void OnGenerationComplete(object? sender, GenerationCompleteEventArgs e)
    {
        Application.Invoke(() =>
        {
            var status = e.IsNewBest ? "🎯 NEW BEST" : e.IsImprovement ? "📈 IMPROVED" : "📊 COMPLETE";
            AddEventLog($"Generation {e.Generation} {status}: Score {e.Score:F3} (+{e.Improvement:F3})");
        });
    }

    private void OnTrainingComplete(object? sender, TrainingCompleteEventArgs e)
    {
        Application.Invoke(() =>
        {
            _sessionCompleted = true;
            _closeButton!.Enabled = true;
            _pauseResumeButton!.Enabled = false;
            _abortButton!.Enabled = false;

            var completionMessage = e.FinalState switch
            {
                TrainingSessionState.Completed => $"🎉 TRAINING COMPLETED! Final Score: {e.FinalScore:F3}",
                TrainingSessionState.Aborted => "⏹️ Training aborted by user",
                TrainingSessionState.Failed => $"❌ Training failed: {e.ErrorMessage}",
                _ => $"Training ended: {e.FinalState}"
            };

            AddEventLog(completionMessage);
            UpdateStatus(completionMessage);

            if (e.IsSuccessful)
            {
                MessageBox.Query("Training Complete",
                    $"Training completed successfully!\n\n" +
                    $"Final Score: {e.FinalScore:F3}\n" +
                    $"Best Score: {e.BestScore:F3}\n" +
                    $"Total Improvement: {e.TotalImprovement:F3}\n" +
                    $"Generations: {e.GenerationsCompleted}\n" +
                    $"Duration: {e.TotalDuration:hh\\:mm\\:ss}",
                    "OK");
            }
        });
    }

    private void AddEventLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _eventLog.AppendLine($"[{timestamp}] {message}");

        // Keep only last 100 lines to prevent memory issues
        var lines = _eventLog.ToString().Split('\n');
        if (lines.Length > 100)
        {
            _eventLog.Clear();
            _eventLog.AppendLine(string.Join('\n', lines.TakeLast(100)));
        }

        if (_eventLogView != null)
        {
            _eventLogView.Text = _eventLog.ToString();
            _eventLogView.MoveEnd(); // Auto-scroll to bottom
        }
    }

    private void UpdateStatus(string status)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = status;
        }
    }
}
