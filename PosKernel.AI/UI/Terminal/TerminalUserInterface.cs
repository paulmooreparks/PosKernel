//
// Copyright 2025 Paul Moore Parks and contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using Terminal.Gui;
using Terminal.Gui.ConsoleDrivers;
using Terminal.Gui.EnumExtensions;
using PosKernel.AI.Models;
using PosKernel.AI.Interfaces;
using PosKernel.AI.Core;
using System.Text;
using TGAttribute = Terminal.Gui.Attribute;

namespace PosKernel.AI.UI.Terminal {
    /// <summary>
    /// Terminal.Gui-based chat display with rich text formatting.
    /// </summary>
    public class TerminalChatDisplay : IChatDisplay {
        private readonly TextView _chatView;
        private readonly TextField _inputField;
        private readonly StringBuilder _chatContent;
        private TaskCompletionSource<string?>? _inputWaiter;

        public TerminalChatDisplay(TextView chatView, TextField inputField) {
            _chatView = chatView ?? throw new ArgumentNullException(nameof(chatView));
            _inputField = inputField ?? throw new ArgumentNullException(nameof(inputField));
            _chatContent = new StringBuilder();
        }

        public TextField InputField => _inputField;

        public void ShowMessage(ChatMessage message) {
            if (!message.ShowInCleanMode) {
                return;
            }

            var timestamp = message.Timestamp.ToString("HH:mm:ss");
            var prefix = message.Sender switch {
                "Customer" => "You",
                _ when message.IsSystem => "System",
                _ => $"{message.Sender}"
            };

            var line = $"[{timestamp}] {prefix}: {message.Content}";

            _chatContent.AppendLine(line);
            _chatContent.AppendLine();

            Application.Invoke(() => {
                _chatView.Text = _chatContent.ToString();
                _chatView.MoveEnd(); // Auto-scroll to bottom for new messages
            });
        }

        public async Task<string?> GetUserInputAsync(string prompt = "You: ") {
            _inputWaiter = new TaskCompletionSource<string?>();

            Application.Invoke(() => {
                _inputField.SetFocus();
            });

            return await _inputWaiter.Task;
        }

        public void OnUserInput() {
            var input = _inputField.Text?.ToString()?.Trim();
            _inputField.Text = "";

            if (!string.IsNullOrEmpty(input)) {
                // Navigate up the UI hierarchy to find the TerminalUserInterface
                var currentView = _inputField.SuperView;
                TerminalUserInterface? terminalUI = null;

                while (currentView != null && terminalUI == null) {
                    if (currentView is Toplevel toplevel) {
                        terminalUI = toplevel.Data as TerminalUserInterface;
                    }
                    currentView = currentView.SuperView;
                }

                var orchestrator = terminalUI?.GetOrchestrator();

                if (orchestrator != null) {
                    ShowMessage(new ChatMessage {
                        Sender = "Customer",
                        Content = input,
                        IsSystem = false
                    });

                    Task.Run(async () => {
                        try {
                            var previousReceiptStatus = orchestrator.CurrentReceipt.Status;
                            terminalUI?.LogDisplay?.AddLog($"DEBUG: Previous receipt status: {previousReceiptStatus}");
                            
                            var response = await orchestrator.ProcessUserInputAsync(input);
                            
                            var newReceiptStatus = orchestrator.CurrentReceipt.Status;
                            terminalUI?.LogDisplay?.AddLog($"DEBUG: New receipt status: {newReceiptStatus}");

                            Application.Invoke(() => {
                                ShowMessage(response);
                                terminalUI?.Receipt.UpdateReceipt(orchestrator.CurrentReceipt);
                                terminalUI?.LogDisplay?.AddLog($"Receipt updated: {orchestrator.CurrentReceipt.Items.Count} items, Status: {orchestrator.CurrentReceipt.Status}");
                                
                                // Update prompt display with the last prompt sent to AI
                                terminalUI?.UpdatePromptDisplay();
                                
                                _inputField.SetFocus();
                            });

                            // ENHANCED: Check for payment completion and generate post-payment message
                            terminalUI?.LogDisplay?.AddLog($"DEBUG: Checking payment completion - Previous: {previousReceiptStatus}, New: {newReceiptStatus}");
                            
                            if (previousReceiptStatus != PaymentStatus.Completed && newReceiptStatus == PaymentStatus.Completed) {
                                terminalUI?.LogDisplay?.AddLog("INFO: Payment completed - generating post-payment message");
                                
                                // Generate a proper post-payment acknowledgment message
                                var postPaymentMessage = await orchestrator.GeneratePostPaymentMessageAsync();
                                
                                Application.Invoke(() => {
                                    ShowMessage(postPaymentMessage);
                                    terminalUI?.Receipt.UpdateReceipt(orchestrator.CurrentReceipt);
                                    terminalUI?.UpdatePromptDisplay();
                                    _inputField.SetFocus();
                                });
                            } else {
                                terminalUI?.LogDisplay?.AddLog($"DEBUG: No payment completion detected - Previous: {previousReceiptStatus}, New: {newReceiptStatus}");
                            }
                        }
                        catch (Exception ex) {
                            Application.Invoke(() => {
                                ShowError($"Error processing input: {ex.Message}");
                                terminalUI?.LogDisplay?.AddLog($"ERROR: {ex.Message}");
                                terminalUI?.LogDisplay?.AddLog($"STACK: {ex.StackTrace}");
                                _inputField.SetFocus();
                            });
                        }
                    });
                }
                else {
                    // Show a more specific error message for debugging
                    ShowMessage(new ChatMessage {
                        Sender = "System",
                        Content = "🔄 System is initializing... Please wait a moment.",
                        IsSystem = true,
                        ShowInCleanMode = true
                    });
                    
                    terminalUI?.LogDisplay?.AddLog($"DEBUG: terminalUI = {(terminalUI != null ? "found" : "NULL")}");
                    terminalUI?.LogDisplay?.AddLog($"DEBUG: orchestrator = {(orchestrator != null ? "found" : "NULL")}");
                    terminalUI?.LogDisplay?.AddLog("INFO: User tried to input but orchestrator not ready yet");
                    
                    // Re-enable input after a short delay and refocus
                    Task.Delay(1000).ContinueWith(_ => {
                        Application.Invoke(() => {
                            _inputField.SetFocus();
                        });
                    });
                }
            }

            _inputWaiter?.SetResult(input);
            _inputWaiter = null;
        }

        public void ShowStatus(string message) {
            ShowMessage(new ChatMessage {
                Sender = "System",
                Content = $"✅ {message}",
                IsSystem = true,
                ShowInCleanMode = true
            });
        }

        public void ShowError(string message) {
            ShowMessage(new ChatMessage {
                Sender = "System",
                Content = $"❌ {message}",
                IsSystem = true,
                ShowInCleanMode = true
            });
        }

        public void Clear() {
            _chatContent.Clear();
            Application.Invoke(() => {
                _chatView.Text = "";
            });
        }
    }

    /// <summary>
    /// Terminal.GUI-based receipt display with formatted layout.
    /// </summary>
    public class TerminalReceiptDisplay : IReceiptDisplay {
        private readonly TextView _receiptView;
        private readonly TerminalLogDisplay _logDisplay;
        private readonly Label _statusBar;

        public TerminalReceiptDisplay(TextView receiptView, Label statusBar, TerminalLogDisplay logDisplay) {
            _receiptView = receiptView ?? throw new ArgumentNullException(nameof(receiptView));
            _logDisplay = logDisplay ?? throw new ArgumentNullException(nameof(logDisplay));
            _statusBar = statusBar ?? throw new ArgumentNullException(nameof(statusBar));
        }

        public void UpdateReceipt(Receipt receipt) {
            var content = new StringBuilder();

            _logDisplay.AddLog($"DEBUG: Receipt has {receipt.Items.Count} items");
            _logDisplay.AddLog($"DEBUG: Receipt status: {receipt.Status}");
            _logDisplay.AddLog($"DEBUG: Receipt total: {receipt.Total:F2}");

            content.AppendLine($"{receipt.Store.Name}");
            content.AppendLine($"Transaction #{receipt.TransactionId}");
            content.AppendLine();

            if (receipt.Items.Any()) {
                foreach (var item in receipt.Items) {
                    var lineTotal = item.CalculatedTotal;
                    content.AppendLine($"{item.Quantity}x {item.ProductName} - ${lineTotal:F2}");

                    if (!string.IsNullOrEmpty(item.PreparationNotes)) {
                        content.AppendLine($"  ({item.PreparationNotes})");
                    }
                }

                content.AppendLine();
                content.AppendLine($"TOTAL: ${receipt.Total:F2}");
            }
            else {
                content.AppendLine("(Empty Order)");
            }

            Application.Invoke(() => {
                _receiptView.Text = content.ToString();
                
                // Auto-scroll to bottom if content is long
                if (content.Length > _receiptView.Maxlength) {
                    _receiptView.MoveEnd();
                }

                var itemCount = receipt.Items.Count;
                var status = receipt.Status switch {
                    PaymentStatus.Building => $"Building Order ({itemCount} items)",
                    PaymentStatus.ReadyForPayment => $"Ready for Payment - ${receipt.Total:F2}",
                    PaymentStatus.Completed => "✅ PAID",
                    _ => receipt.Status.ToString()
                };
                _statusBar.Text = $"Status: {status}";
            });
        }

        public void ShowPaymentStatus(string status) {
            Application.Invoke(() => {
                // Only update if we're not currently showing a focus hint
                if (!_statusBar.Text.ToString().Contains("Viewing")) {
                    _statusBar.Text = $"Status: {status}";
                }
            });
        }

        public void Clear() {
            Application.Invoke(() => {
                _receiptView.Text = "";
                _statusBar.Text = "Status: Empty";
            });
        }
    }

    /// <summary>
    /// Terminal.GUI-based log display for capturing debug output.
    /// </summary>
    public class TerminalLogDisplay : ILogDisplay {
        private readonly TextView _logView;
        private readonly ScrollBar _logScrollBar;
        private readonly StringBuilder _logContent;
        private readonly List<string> _logEntries = new List<string>();

        public TerminalLogDisplay(TextView logView, ScrollBar logScrollBar) {
            _logView = logView ?? throw new ArgumentNullException(nameof(logView));
            _logScrollBar = logScrollBar ?? throw new ArgumentNullException(nameof(logScrollBar));
            _logContent = new StringBuilder();
            
            // Connect ScrollBar to TextView following Terminal.Gui ScrollBarDemo pattern
            _logScrollBar.PositionChanged += (sender, args) => {
                _logView.TopRow = args.CurrentValue;
            };
        }

        private void OnScrollBarPositionChanged(object? sender, EventArgs<int> e) {
            if (e.CurrentValue >= 0 && e.CurrentValue < _logEntries.Count) {
                // Update TextView scroll position based on scroll bar
                _logView.TopRow = e.CurrentValue;
            }
        }

        private void OnTextViewKeyDown(object? sender, Key e) {
            // Update scroll bar position when TextView scrolls via keyboard
            // Use a small delay to let Terminal.Gui process the scroll first
            Task.Delay(10).ContinueWith(_ => {
                Application.Invoke(() => {
                    _logScrollBar.Position = _logView.TopRow;
                });
            });
        }

        private void OnTextViewMouseClick(object? sender, MouseEventArgs e) {
            // Update scroll bar position after mouse interaction
            Task.Delay(10).ContinueWith(_ => {
                Application.Invoke(() => {
                    _logScrollBar.Position = _logView.TopRow;
                });
            });
        }

        public void AddLog(string message) {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}";
            
            _logEntries.Add(logEntry);
            
            // Keep only last 1000 entries to prevent memory issues
            if (_logEntries.Count > 1000) {
                _logEntries.RemoveAt(0);
            }
            
            Application.Invoke(() => {
                var allLogs = string.Join("\n", _logEntries);
                _logView.Text = allLogs;
                
                // Update ScrollBar based on content size
                var lines = allLogs.Split('\n').Length;
                var viewHeight = _logView.Frame.Height;
                
                if (lines > viewHeight) {
                    _logScrollBar.ScrollableContentSize = lines;
                    _logScrollBar.Visible = true;
                } else {
                    _logScrollBar.Visible = false;
                }
                
                // Auto-scroll to bottom to show latest logs
                _logView.MoveEnd();
                if (_logScrollBar.Visible) {
                    _logScrollBar.Position = Math.Max(0, lines - viewHeight);
                }
            });
        }

        public void ShowStatus(string message) {
            AddLog($"✅ STATUS: {message}");
        }

        public void ShowError(string message) {
            AddLog($"❌ ERROR: {message}");
        }

        public void Clear() {
            _logContent.Clear();
            _logEntries.Clear();
            Application.Invoke(() => {
                _logView.Text = "";
            });
        }
    }

    /// <summary>
    /// Terminal.Gui-based user interface with split-pane layout.
    /// </summary>
    public class TerminalUserInterface : IUserInterface {
        public IChatDisplay Chat { get; private set; } = null!;
        public IReceiptDisplay Receipt { get; private set; } = null!;
        public ILogDisplay Log { get; private set; } = null!;

        private bool _initialized = false;
        private Toplevel? _top;
        private TerminalChatDisplay? _terminalChat;
        private TerminalLogDisplay? _logDisplay;
        private TerminalPromptDisplay? _promptDisplay;
        private ChatOrchestrator? _orchestrator;
        private ConsoleOutputRedirector? _consoleRedirector;

        public TerminalUserInterface() {
        }

        public void SetOrchestrator(ChatOrchestrator orchestrator) {
            _orchestrator = orchestrator;
            
            // When orchestrator is set, show a ready message to let user know they can type
            if (_terminalChat != null) {
                _terminalChat.ShowMessage(new ChatMessage {
                    Sender = "System",
                    Content = "✅ System ready! You can now place your order.",
                    IsSystem = true,
                    ShowInCleanMode = true
                });
            }
            
            _logDisplay?.AddLog("INFO: Orchestrator set and system is now ready for input");
            _logDisplay?.AddLog($"DEBUG: LastPrompt available: {(_orchestrator?.LastPrompt != null ? "YES" : "NO")}");
            _logDisplay?.AddLog($"DEBUG: PromptDisplay available: {(_promptDisplay != null ? "YES" : "NO")}");
            
            // Display the initial prompt that was used during initialization
            // Use a small delay to ensure orchestrator initialization is complete
            Task.Delay(100).ContinueWith(_ => {
                Application.Invoke(() => {
                    UpdatePromptDisplay();
                });
            });
        }

        /// <summary>
        /// Updates the prompt display with the current prompt from the orchestrator.
        /// Call this after processing user input to show what prompt was sent to the AI.
        /// </summary>
        public void UpdatePromptDisplay() {
            _logDisplay?.AddLog($"DEBUG: UpdatePromptDisplay called - orchestrator: {(_orchestrator != null ? "YES" : "NO")}, promptDisplay: {(_promptDisplay != null ? "YES" : "NO")}");
            
            if (_orchestrator?.LastPrompt != null && _promptDisplay != null) {
                _logDisplay?.AddLog($"DEBUG: Showing prompt with length: {_orchestrator.LastPrompt.Length}");
                _promptDisplay.ShowPrompt(_orchestrator.LastPrompt);
                _logDisplay?.AddLog("DEBUG: ShowPrompt called successfully");
            } else {
                _logDisplay?.AddLog($"DEBUG: Cannot show prompt - LastPrompt: {(_orchestrator?.LastPrompt != null ? "available" : "null")}, PromptDisplay: {(_promptDisplay != null ? "available" : "null")}");
            }
        }

        public ChatOrchestrator? GetOrchestrator() => _orchestrator;

        public TerminalLogDisplay? LogDisplay => _logDisplay;

        public TerminalPromptDisplay? PromptDisplay => _promptDisplay;

        public async Task InitializeAsync() {
            if (_initialized) {
                return;
            }

            Application.Init();

            _top = new Toplevel();

            _top.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.Black, Color.White),
                Focus = new TGAttribute(Color.Black, Color.White),
                HotNormal = new TGAttribute(Color.BrightRed, Color.White),
                HotFocus = new TGAttribute(Color.BrightRed, Color.White),
                Disabled = new TGAttribute(Color.DarkGray, Color.White)
            };

            // Calculate layout dimensions
            var inputHeight = 3;
            var chatHeightPercent = 50; // Fixed width for chat area to match screenshot
            var chatWidthPercent = 60; // Fixed width for chat area to match screenshot
            var promptHeightPercent = 20; // Height when expanded, 1 when collapsed

            // Create a simple menu WITHOUT interfering with focus
            var quitItem = new MenuItem("_Quit", "", () => Application.RequestStop());
            var fileMenu = new MenuBarItem("_File", new MenuItem[] { quitItem });
            var helpItem = new MenuItem("Show _Help", "", ShowHelp);
            var helpMenu = new MenuBarItem("_Help", new MenuItem[] { helpItem });

            var menuBar = new MenuBar();
            menuBar.Menus = new MenuBarItem[] { fileMenu, helpMenu };
            menuBar.Y = 0; // Put menu at top
            
            menuBar.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.Black, Color.Cyan),
                Focus = new TGAttribute(Color.White, Color.Blue),
                HotNormal = new TGAttribute(Color.BrightRed, Color.Cyan),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
                Disabled = new TGAttribute(Color.Gray, Color.Cyan)
            };

            var inputPrompt = new Label() {
                Text = "Type your order here and press Enter to send:",
                X = 0,
                Y = Pos.Bottom(menuBar),
                Width = Dim.Fill()
            };

            inputPrompt.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.Black, Color.White),
                Focus = new TGAttribute(Color.Black, Color.White),
                HotNormal = new TGAttribute(Color.BrightRed, Color.White),
                HotFocus = new TGAttribute(Color.BrightRed, Color.White),
                Disabled = new TGAttribute(Color.DarkGray, Color.White)
            };

            var inputField = new TextField() {
                X = 0,
                Y = Pos.Bottom(inputPrompt),
                Width = Dim.Fill(),
                Height = inputHeight,
                CanFocus = true,
                CursorVisibility = CursorVisibility.Default, // Ensure cursor is visible
            };

            inputField.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.White, Color.Black),
                Focus = new TGAttribute(Color.BrightYellow, Color.Black),
                HotNormal = new TGAttribute(Color.BrightCyan, Color.Black),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.Black),
                Disabled = new TGAttribute(Color.Gray, Color.Black)
            };

            Label? chatLabel = new Label() {
                Text = "Chat content:",
                X = 0,
                Y = Pos.Bottom(inputField),
                Width = Dim.Fill()
            };

            chatLabel.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.Black, Color.White),
                Focus = new TGAttribute(Color.Black, Color.White),
                HotNormal = new TGAttribute(Color.BrightRed, Color.White),
                HotFocus = new TGAttribute(Color.BrightRed, Color.White),
                Disabled = new TGAttribute(Color.DarkGray, Color.White)
            };

            var chatView = new TextView() {
                X = 0,
                Y = Pos.Bottom(chatLabel),
                Width = Dim.Percent(chatWidthPercent),
                Height = Dim.Percent(chatHeightPercent),
                ReadOnly = true,
                WordWrap = true,
                CanFocus = false,
            };

            chatView.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.White, Color.Blue),
                Focus = new TGAttribute(Color.BrightYellow, Color.Blue),
                HotNormal = new TGAttribute(Color.BrightCyan, Color.Blue),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
                Disabled = new TGAttribute(Color.Gray, Color.Blue)
            };

            Label? receiptLabel = new Label() {
                Text = "Receipt:",
                X = Pos.Right(chatView) + 1,
                Y = Pos.Bottom(inputField),
                Width = Dim.Fill()
            };

            receiptLabel.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.Black, Color.White),
                Focus = new TGAttribute(Color.Black, Color.White),
                HotNormal = new TGAttribute(Color.BrightRed, Color.White),
                HotFocus = new TGAttribute(Color.BrightRed, Color.White),
                Disabled = new TGAttribute(Color.DarkGray, Color.White)
            };

            var receiptView = new TextView() {
                X = Pos.Right(chatView) + 1,
                Y = Pos.Bottom(receiptLabel),
                Width = Dim.Fill(),
                Height = Dim.Percent(chatHeightPercent),
                ReadOnly = true,
                CanFocus = false,
            };

            receiptView.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.White, Color.Blue),
                Focus = new TGAttribute(Color.BrightYellow, Color.Blue),
                HotNormal = new TGAttribute(Color.BrightCyan, Color.Blue),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
                Disabled = new TGAttribute(Color.Gray, Color.Blue)
            };

            // Create prompt context view between input and log
            var promptLabel = new Label() {
                Text = "▼ Prompt Context (Click to collapse)",
                X = 0,
                Y = Pos.Bottom(chatView),
                Width = Dim.Fill(),
                CanFocus = true
            };

            promptLabel.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.Black, Color.White),
                Focus = new TGAttribute(Color.BrightRed, Color.White),
                HotNormal = new TGAttribute(Color.BrightRed, Color.White),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.White),
                Disabled = new TGAttribute(Color.DarkGray, Color.White)
            };

            // Container view for prompt display (starts expanded)
            var promptContainer = new View() {
                X = 0,
                Y = Pos.Bottom(promptLabel),
                Width = Dim.Fill(),
                Height = Dim.Percent(promptHeightPercent),
            };

            var promptView = new TextView() {
                X = 0,
                Y = 0,
                Width = Dim.Fill(1), // Leave space for scroll bar
                Height = Dim.Fill(),
                ReadOnly = true,
                WordWrap = false, // Disable word wrap for horizontal scrolling
                CanFocus = true, // Enable focus for scrolling
                Visible = true, // Show initially instead of hidden
            };

            promptView.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.BrightCyan, Color.Blue),
                Focus = new TGAttribute(Color.BrightYellow, Color.Blue),
                HotNormal = new TGAttribute(Color.White, Color.Blue),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
                Disabled = new TGAttribute(Color.Gray, Color.Blue)
            };

            // Add vertical scroll bar for prompt view
            var promptScrollBar = new ScrollBar {
                X = Pos.AnchorEnd(),
                Y = 0,
                Height = Dim.Fill(),
                AutoShow = false, // Don't auto-show, let content determine visibility
                Visible = true // Show initially instead of hidden
            };

            promptScrollBar.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.White, Color.Blue),
                Focus = new TGAttribute(Color.BrightYellow, Color.Blue),
                HotNormal = new TGAttribute(Color.BrightCyan, Color.Blue),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
                Disabled = new TGAttribute(Color.Gray, Color.Blue)
            };

            promptContainer.Add(promptView);
            promptContainer.Add(promptScrollBar);

            // Create collapsible debug log view at bottom
            var logLabel = new Label() {
                Text = "▼ Debug Logs (Click to collapse):",
                X = 0,
                Y = Pos.Bottom(promptContainer),
                Width = Dim.Fill(),
                CanFocus = true
            };

            logLabel.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.Black, Color.White),
                Focus = new TGAttribute(Color.BrightRed, Color.White),
                HotNormal = new TGAttribute(Color.BrightRed, Color.White),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.White),
                Disabled = new TGAttribute(Color.DarkGray, Color.White)
            };

            var logContainer = new View() {
                X = 0,
                Y = Pos.Bottom(logLabel),
                Width = Dim.Fill(),
                Height = Dim.Fill(1) // Fill all remaining space, leaving room for status bar
            };

            var logView = new TextView() {
                X = 0,
                Y = 0,
                Width = Dim.Fill(1), // Leave space for scroll bar
                Height = Dim.Fill(),
                ReadOnly = true,
                WordWrap = false, // Disable word wrap for horizontal scrolling
                CanFocus = true, // Enable focus for scrolling
            };

            logView.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.White, Color.Blue),
                Focus = new TGAttribute(Color.BrightYellow, Color.Blue),
                HotNormal = new TGAttribute(Color.BrightCyan, Color.Blue),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
                Disabled = new TGAttribute(Color.Gray, Color.Blue)
            };

            // Add vertical scroll bar for log view
            var logScrollBar = new ScrollBar {
                X = Pos.AnchorEnd(),
                Y = 0,
                Height = Dim.Fill(),
                AutoShow = false // Don't auto-show, let content determine visibility
            };

            logScrollBar.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.White, Color.Blue),
                Focus = new TGAttribute(Color.BrightYellow, Color.Blue),
                HotNormal = new TGAttribute(Color.BrightCyan, Color.Blue),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
                Disabled = new TGAttribute(Color.Gray, Color.Blue)
            };

            logContainer.Add(logView);
            logContainer.Add(logScrollBar);

            // Create status bar at bottom - following Notepad.cs example
            var statusBar = new Label() {
                Text = "Status: Empty",
                X = 0,
                Y = Pos.AnchorEnd(),
                Width = Dim.Fill(),
            };

            statusBar.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.Black, Color.Cyan),
                Focus = new TGAttribute(Color.Black, Color.Cyan),
                HotNormal = new TGAttribute(Color.Black, Color.Cyan),
                HotFocus = new TGAttribute(Color.Black, Color.Cyan),
                Disabled = new TGAttribute(Color.Gray, Color.Cyan)
            };

            // Handle Enter key for input
            inputField.KeyDown += (_, e) => {
                if (e.KeyCode == KeyCode.Enter) {
                    _terminalChat?.OnUserInput();
                    e.Handled = true;
                }
            };

            // Handle click events for collapsible panes
            promptLabel.MouseClick += (_, e) => {
                _promptDisplay?.ToggleCollapsed();
                e.Handled = true;
            };

            // Add keyboard shortcuts for quick access to prompt view
            promptLabel.KeyDown += (_, e) => {
                if (e.KeyCode == KeyCode.Space || e.KeyCode == KeyCode.Enter) {
                    _promptDisplay?.ToggleCollapsed();
                    e.Handled = true;
                }
            };

            // When prompt view gets focus, show a status hint
            promptView.HasFocusChanged += (_, e) => {
                if (promptView.HasFocus) {
                    statusBar.Text = "Status: Viewing prompt context (Use arrows/PgUp/PgDn to scroll, Tab to move focus)";
                } else {
                    statusBar.Text = "Status: Building Order (0 items)";
                }
            };

            var logCollapsed = false;
            logLabel.MouseClick += (_, e) => {
                logCollapsed = !logCollapsed;
                var arrow = logCollapsed ? "▶" : "▼";
                logLabel.Text = $"{arrow} Debug Logs (Click to {(logCollapsed ? "expand" : "collapse")}):";
                
                if (logCollapsed) {
                    // When collapsed, keep container at minimum height but ensure label stays visible
                    logContainer.Height = Dim.Absolute(0); // Hide the container completely
                    logView.Visible = false;
                    logScrollBar.Visible = false;
                } else {
                    // When expanded, show container and contents
                    logContainer.Height = Dim.Fill(1); // Fill remaining space
                    logView.Visible = true;
                    logScrollBar.Visible = true;
                }
                
                // Update prompt container size based on debug pane state
                var newPromptHeight = logCollapsed ? Dim.Fill(3) : Dim.Percent(promptHeightPercent);
                _promptDisplay?.UpdateContainerSize(newPromptHeight);
                
                logContainer.SetNeedsLayout();
                e.Handled = true;
            };

            // Add keyboard shortcuts for debug log access
            logLabel.KeyDown += (_, e) => {
                if (e.KeyCode == KeyCode.Space || e.KeyCode == KeyCode.Enter) {
                    logCollapsed = !logCollapsed;
                    var arrow = logCollapsed ? "▶" : "▼";
                    logLabel.Text = $"{arrow} Debug Logs (Click to {(logCollapsed ? "expand" : "collapse")}):";
                    
                    if (logCollapsed) {
                        // When collapsed, hide container but keep label visible
                        logContainer.Height = Dim.Absolute(0);
                        logView.Visible = false;
                        logScrollBar.Visible = false;
                    } else {
                        // When expanded, show container and contents
                        logContainer.Height = Dim.Fill(1);
                        logView.Visible = true;
                        logScrollBar.Visible = true;
                    }
                    
                    // Update prompt container size based on debug pane state
                    var newPromptHeight = logCollapsed ? Dim.Fill(3) : Dim.Percent(promptHeightPercent);
                    _promptDisplay?.UpdateContainerSize(newPromptHeight);
                    
                    logContainer.SetNeedsLayout();
                    e.Handled = true;
                }
            };

            // When log view gets focus, show a status hint
            logView.HasFocusChanged += (_, e) => {
                if (logView.HasFocus) {
                    statusBar.Text = "Status: Viewing debug logs (Use arrows/PgUp/PgDn to scroll, Tab to move focus)";
                } else {
                    statusBar.Text = "Status: Building Order (0 items)";
                }
            };

            _top.Add(inputField, inputPrompt, menuBar, chatLabel, chatView, receiptLabel, receiptView, 
                    promptLabel, promptContainer, logLabel, logContainer, statusBar);

            // Initialize display components
            _terminalChat = new TerminalChatDisplay(chatView, inputField);
            Chat = _terminalChat;
            _logDisplay = new TerminalLogDisplay(logView, logScrollBar);
            _promptDisplay = new TerminalPromptDisplay(promptView, promptScrollBar, promptLabel, promptContainer);
            Receipt = new TerminalReceiptDisplay(receiptView, statusBar, _logDisplay);
            Log = _logDisplay; // Expose log display through ILogDisplay interface

            // Redirect console output to debug pane
            var outRedirector = new ConsoleOutputRedirector(_logDisplay, System.Console.Out, "OUT");
            var errorRedirector = new ConsoleOutputRedirector(_logDisplay, System.Console.Error, "ERROR");
            System.Console.SetOut(outRedirector);
            System.Console.SetError(errorRedirector);
            _consoleRedirector = outRedirector; // Keep reference for disposal

            _top.Data = this;
            _initialized = true;
            await Task.CompletedTask;
        }

        public async Task RunAsync() {
            if (!_initialized) {
                await InitializeAsync();
            }

            if (_top != null) {
                // Set focus to input field right before running
                var inputField = _terminalChat?.InputField;
                inputField?.SetFocus();
                
                Application.Run(_top);
            }

            await Task.CompletedTask;
        }

        public async Task ShutdownAsync() {
            // Restore console output before shutting down
            _consoleRedirector?.Dispose();

            Application.Shutdown();
            await Task.CompletedTask;
        }

        private static void ShowHelp() {
            MessageBox.Query("Help",
                "POS Kernel AI Demo\n\n" +
                "• Terminal.Gui interface with store selection dialog\n" +
                "• Type your order in the chat area\n" +
                "• Press Enter to send\n" +
                "• Receipt updates automatically\n" +
                "• Click prompt/log labels to expand/collapse debug info\n\n" +
                "Navigation:\n" +
                "• Tab - Move between focusable areas\n" +
                "• Arrow Keys - Scroll in prompt/log views\n" +
                "• Page Up/Down - Fast scroll in prompt/log views\n" +
                "• Home/End - Jump to top/bottom of prompt/log\n\n" +
                "Shortcuts:\n" +
                "• Alt+F4 or Ctrl+Q - Quit\n" +
                "• F1 - Help\n\n" +
                "Debug Features:\n" +
                "• Prompt Context: Shows exact prompt sent to AI (scrollable)\n" +
                "• Debug Logs: System diagnostics and tool execution (scrollable)\n\n" +
                "Integration Modes:\n" +
                "• Real Kernel: Uses Rust POS service + SQLite\n" +
                "• Mock: Development mode with simulated data",
                "OK");
        }
    }

    /// <summary>
    /// Redirects Console.Out and Console.Error to the Terminal.Gui debug pane.
    /// Prevents console output from corrupting the TUI display.
    /// </summary>
    internal class ConsoleOutputRedirector : TextWriter {
        private readonly TerminalLogDisplay _logDisplay;
        private readonly TextWriter _originalOut;
        private readonly TextWriter _originalError;
        private readonly string _streamType;

        public ConsoleOutputRedirector(TerminalLogDisplay logDisplay, TextWriter originalWriter, string streamType) {
            _logDisplay = logDisplay;
            _originalOut = streamType == "OUT" ? originalWriter : System.Console.Out;
            _originalError = streamType == "ERROR" ? originalWriter : System.Console.Error;
            _streamType = streamType;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) {
            // Buffer single characters
        }

        public override void Write(string? value) {
            if (!string.IsNullOrEmpty(value)) {
                _logDisplay?.AddLog($"[CONSOLE.{_streamType}] {value}");
            }
        }

        public override void WriteLine(string? value) {
            if (!string.IsNullOrEmpty(value)) {
                _logDisplay?.AddLog($"[CONSOLE.{_streamType}] {value}");
            }
        }

        public override void WriteLine() {
            // Ignore empty lines
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                // Restore original console streams
                System.Console.SetOut(_originalOut);
                System.Console.SetError(_originalError);
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Terminal.GUI-based prompt context display for debugging and refinement.
    /// Shows the actual prompt sent to the AI with collapsible functionality.
    /// </summary>
    public class TerminalPromptDisplay {
        private readonly TextView _promptView;
        private readonly ScrollBar _promptScrollBar;
        private readonly Label _promptLabel;
        private readonly StringBuilder _promptContent;
        private bool _isCollapsed = false;
        private readonly View _containerView;

        public TerminalPromptDisplay(TextView promptView, ScrollBar promptScrollBar, Label promptLabel, View containerView) {
            _promptView = promptView ?? throw new ArgumentNullException(nameof(promptView));
            _promptScrollBar = promptScrollBar ?? throw new ArgumentNullException(nameof(promptScrollBar));
            _promptLabel = promptLabel ?? throw new ArgumentNullException(nameof(promptLabel));
            _containerView = containerView ?? throw new ArgumentNullException(nameof(containerView));
            _promptContent = new StringBuilder();
            
            // Connect ScrollBar to TextView following Terminal.Gui ScrollBarDemo pattern
            _promptScrollBar.PositionChanged += (sender, args) => {
                _promptView.TopRow = args.CurrentValue;
            };
            
            UpdateLabelText();
        }

        /// <summary>
        /// Gets whether the prompt display is currently collapsed.
        /// </summary>
        public bool IsCollapsed => _isCollapsed;

        /// <summary>
        /// Updates the container size (called externally when debug pane state changes).
        /// Call this after processing user input to show what prompt was sent to the AI.
        /// </summary>
        public void UpdateContainerSize(Dim newHeight) {
            if (!_isCollapsed) {
                Application.Invoke(() => {
                    _containerView.Height = newHeight;
                    _containerView.SetNeedsLayout();
                });
            }
        }

        private void OnScrollBarPositionChanged(object? sender, EventArgs<int> e) {
            var lines = _promptContent.ToString().Split('\n');
            if (e.CurrentValue >= 0 && e.CurrentValue < lines.Length) {
                // Update TextView scroll position based on scroll bar
                _promptView.TopRow = e.CurrentValue;
            }
        }

        private void OnTextViewKeyDown(object? sender, Key e) {
            // Update scroll bar position when TextView scrolls via keyboard
            // Use a small delay to let Terminal.Gui process the scroll first
            Task.Delay(10).ContinueWith(_ => {
                Application.Invoke(() => {
                    _promptScrollBar.Position = _promptView.TopRow;
                });
            });
        }

        private void OnTextViewMouseClick(object? sender, MouseEventArgs e) {
            // Update scroll bar position after mouse interaction
            Task.Delay(10).ContinueWith(_ => {
                Application.Invoke(() => {
                    _promptScrollBar.Position = _promptView.TopRow;
                });
            });
        }

        public void ShowPrompt(string prompt) {
            _promptContent.Clear();
            _promptContent.AppendLine("=== PROMPT SENT TO AI ===");
            _promptContent.AppendLine();
            _promptContent.AppendLine(prompt);
            _promptContent.AppendLine();
            _promptContent.AppendLine("=== END PROMPT ===");

            // Add some debug logging
            var parentUI = FindParentTerminalUI();
            parentUI?.LogDisplay?.AddLog($"DEBUG: TerminalPromptDisplay.ShowPrompt called with prompt length: {prompt.Length}");
            parentUI?.LogDisplay?.AddLog($"DEBUG: Prompt content length after formatting: {_promptContent.Length}");

            Application.Invoke(() => {
                _promptView.Text = _promptContent.ToString();
                
                // Update ScrollBar based on content size
                var lines = _promptContent.ToString().Split('\n').Length;
                var viewHeight = _promptView.Frame.Height;
                
                parentUI?.LogDisplay?.AddLog($"DEBUG: TextView updated - lines: {lines}, viewHeight: {viewHeight}");
                
                if (lines > viewHeight) {
                    _promptScrollBar.ScrollableContentSize = lines;
                    _promptScrollBar.Visible = true;
                } else {
                    _promptScrollBar.Visible = false;
                }
                
                // Auto-scroll to bottom for new content
                if (_promptView.Visible) {
                    _promptView.MoveEnd();
                    if (_promptScrollBar.Visible) {
                        _promptScrollBar.Position = Math.Max(0, lines - viewHeight);
                    }
                }
                
                parentUI?.LogDisplay?.AddLog("DEBUG: ShowPrompt Application.Invoke completed");
            });
        }

        // Helper method to find parent TerminalUserInterface for logging
        private TerminalUserInterface? FindParentTerminalUI() {
            var currentView = _promptView.SuperView;
            while (currentView != null) {
                if (currentView is Toplevel toplevel) {
                    return toplevel.Data as TerminalUserInterface;
                }
                currentView = currentView.SuperView;
            }
            return null;
        }

        public void ToggleCollapsed() {
            _isCollapsed = !_isCollapsed;
            UpdateLabelText();
            
            Application.Invoke(() => {
                if (_isCollapsed) {
                    // When prompt is collapsed, use minimal height
                    _containerView.Height = 1;
                } else {
                    // When prompt is expanded, use percentage or fill available space
                    // Check if we need to expand based on debug pane state
                    _containerView.Height = Dim.Percent(20); // Default expanded size
                }
                
                _promptView.Visible = !_isCollapsed;
                _promptScrollBar.Visible = !_isCollapsed;
                
                // When expanding with content, scroll to bottom
                if (!_isCollapsed && _promptContent.Length > 0) {
                    _promptView.MoveEnd();
                }
                
                _containerView.SetNeedsLayout();
            });
        }

        private void UpdateLabelText() {
            var arrow = _isCollapsed ? "▶" : "▼";
            Application.Invoke(() => {
                _promptLabel.Text = $"{arrow} Prompt Context (Click to {(_isCollapsed ? "expand" : "collapse")})";
            });
        }

        public void Clear() {
            _promptContent.Clear();
            Application.Invoke(() => {
                _promptView.Text = "";
            });
        }
    }
}
