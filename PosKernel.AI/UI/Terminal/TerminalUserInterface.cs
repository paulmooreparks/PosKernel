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
                            var response = await orchestrator.ProcessUserInputAsync(input);

                            Application.Invoke(() => {
                                ShowMessage(response);
                                terminalUI?.Receipt.UpdateReceipt(orchestrator.CurrentReceipt);
                                terminalUI?.LogDisplay?.AddLog($"Receipt updated: {orchestrator.CurrentReceipt.Items.Count} items, Status: {orchestrator.CurrentReceipt.Status}");
                                _inputField.SetFocus();
                            });
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
        private readonly Label _statusLabel;

        public TerminalReceiptDisplay(TextView receiptView, Label statusLabel, TerminalLogDisplay logDisplay) {
            _receiptView = receiptView ?? throw new ArgumentNullException(nameof(receiptView));
            _logDisplay = logDisplay ?? throw new ArgumentNullException(nameof(logDisplay));
            _statusLabel = statusLabel ?? throw new ArgumentNullException(nameof(statusLabel));
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
                _statusLabel.Text = $"Status: {status}";
            });
        }

        public void ShowPaymentStatus(string status) {
            Application.Invoke(() => {
                _statusLabel.Text = $"💳 {status}";
            });
        }

        public void Clear() {
            Application.Invoke(() => {
                _receiptView.Text = "";
                _statusLabel.Text = "Status: Empty";
            });
        }
    }

    /// <summary>
    /// Terminal.GUI-based log display for capturing debug output.
    /// </summary>
    public class TerminalLogDisplay : ILogDisplay {
        private readonly TextView _logView;
        private readonly StringBuilder _logContent;

        public TerminalLogDisplay(TextView logView) {
            _logView = logView ?? throw new ArgumentNullException(nameof(logView));
            _logContent = new StringBuilder();
        }

        public void AddLog(string message) {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            _logContent.AppendLine($"[{timestamp}] {message}");

            var lines = _logContent.ToString().Split('\n');
            if (lines.Length > 100) {
                var recentLines = lines.TakeLast(100);
                _logContent.Clear();
                _logContent.AppendLine(string.Join('\n', recentLines));
            }

            Application.Invoke(() => {
                _logView.Text = _logContent.ToString();
                _logView.MoveEnd(); // Always auto-scroll debug log to bottom
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
        private Window? _mainWindow;
        private TerminalChatDisplay? _terminalChat;
        private TerminalLogDisplay? _logDisplay;
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
        }

        public ChatOrchestrator? GetOrchestrator() => _orchestrator;

        public TerminalLogDisplay? LogDisplay => _logDisplay;

        public async Task InitializeAsync() {
            if (_initialized) {
                return;
            }

            Application.Init();

            _top = new Toplevel();

            // Set a custom color scheme for the top level
            _top.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.White, Color.Blue),
                Focus = new TGAttribute(Color.Yellow, Color.Blue),
                HotNormal = new TGAttribute(Color.BrightCyan, Color.Blue),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.Blue),
                Disabled = new TGAttribute(Color.Gray, Color.Blue)
            };

            // Create a simple window without complex nesting
            _mainWindow = new Window() {
                Title = "POS Kernel AI Demo - Terminal GUI"
            };

            // You can also set the window's color scheme if desired
            _mainWindow.ColorScheme = new ColorScheme() {
                Normal = new TGAttribute(Color.Black, Color.Gray),
                Focus = new TGAttribute(Color.White, Color.DarkGray),
                HotNormal = new TGAttribute(Color.BrightBlue, Color.Gray),
                HotFocus = new TGAttribute(Color.BrightYellow, Color.DarkGray),
                Disabled = new TGAttribute(Color.DarkGray, Color.Gray)
            };

            // Calculate layout dimensions
            var chatWidthPercent = 60; // Fixed width for chat area to match screenshot
            var inputHeight = 3;
            var logHeight = 10; // Fixed height for debug logs

            // Create a simple menu WITHOUT interfering with focus
            var quitItem = new MenuItem("_Quit", "", () => Application.RequestStop());
            var fileMenu = new MenuBarItem("_File", new MenuItem[] { quitItem });
            var helpItem = new MenuItem("_Help", "", ShowHelp);
            var helpMenu = new MenuBarItem("_Help", new MenuItem[] { helpItem });

            var menuBar = new MenuBar();
            menuBar.Menus = new MenuBarItem[] { fileMenu, helpMenu };
            menuBar.Y = 0; // Put menu at top

            _mainWindow.Y = Pos.Bottom(menuBar);

            // Create chat view above input - use Dim.Fill to calculate available space
            var chatView = new TextView() {
                X = 0,
                Y = Pos.Bottom(menuBar),
                Width = Dim.Percent(chatWidthPercent),
                Height = Dim.Fill(inputHeight + logHeight + 4), // Leave space for input, log, and labels
                ReadOnly = true,
                WordWrap = true,
                CanFocus = false,
                ColorScheme = new ColorScheme() {
                    Normal = new TGAttribute(Color.Blue, Color.White),
                    Focus = new TGAttribute(Color.BrightBlue, Color.White),
                    HotNormal = new TGAttribute(Color.BrightBlue, Color.White),
                    HotFocus = new TGAttribute(Color.Black, Color.White),
                    Disabled = new TGAttribute(Color.Gray, Color.White)
                }
            };

            // Create receipt view on the right side
            var receiptView = new TextView() {
                X = Pos.Right(chatView) + 1,
                Y = Pos.Bottom(menuBar),
                Width = Dim.Fill(),
                Height = Dim.Fill(inputHeight + logHeight + 4), // Same calculation as chat
                ReadOnly = true,
                CanFocus = false,
                ColorScheme = new ColorScheme() {
                    Normal = new TGAttribute(Color.Blue, Color.White),
                    Focus = new TGAttribute(Color.BrightBlue, Color.White),
                    HotNormal = new TGAttribute(Color.BrightBlue, Color.White),
                    HotFocus = new TGAttribute(Color.Black, Color.White),
                    Disabled = new TGAttribute(Color.Gray, Color.White)
                }
            };

            var inputField = new TextField() {
                X = 1,
                Y = Pos.Bottom(chatView) + 2, // Position above log area
                Width = Dim.Fill(1),
                Height = inputHeight,
                CanFocus = true,
                CursorVisibility = CursorVisibility.Default, // Ensure cursor is visible
                ColorScheme = new ColorScheme() {
                    Normal = new TGAttribute(Color.Black, Color.White),
                    Focus = new TGAttribute(Color.Black, Color.BrightCyan),
                    HotNormal = new TGAttribute(Color.Blue, Color.White),
                    HotFocus = new TGAttribute(Color.Blue, Color.BrightCyan),
                    Disabled = new TGAttribute(Color.Gray, Color.White)
                }
            };

            var inputPrompt = new Label() {
                Text = "➤ Type your order (Press Enter to send):",
                X = 1,
                Y = Pos.Top(inputField) - 1,
                Width = Dim.Fill()
            };

            // Create debug log view at bottom
            var logView = new TextView() {
                X = 0,
                Y = Pos.Bottom(inputField) + 2,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                ReadOnly = true,
                WordWrap = true,
                CanFocus = false,
                ColorScheme = new ColorScheme() {
                    Normal = new TGAttribute(Color.Blue, Color.White),
                    Focus = new TGAttribute(Color.BrightBlue, Color.White),
                    HotNormal = new TGAttribute(Color.BrightBlue, Color.White),
                    HotFocus = new TGAttribute(Color.Black, Color.White),
                    Disabled = new TGAttribute(Color.Gray, Color.White)
                }
            };

            var statusLabel = new Label() {
                Text = "Status: Empty",
                X = Pos.Left(receiptView) + 1,
                Y = Pos.Bottom(receiptView),
                Width = Dim.Fill()
            };

            var logLabel = new Label() {
                Text = "Debug Logs:",
                X = 1,
                Y = Pos.Top(logView) - 1,
                Width = Dim.Fill()
            };

            // Handle Enter key
            inputField.KeyDown += (_, e) => {
                if (e.KeyCode == KeyCode.Enter) {
                    _terminalChat?.OnUserInput();
                    e.Handled = true;
                }
            };

            _top.Add(inputField, menuBar, chatView, receiptView, inputPrompt, statusLabel, logLabel, logView);

            // Initialize display components
            _terminalChat = new TerminalChatDisplay(chatView, inputField);
            Chat = _terminalChat;
            _logDisplay = new TerminalLogDisplay(logView);
            Receipt = new TerminalReceiptDisplay(receiptView, statusLabel, _logDisplay);
            Log = _logDisplay; // Expose log display through ILogDisplay interface

            // Redirect console output to debug pane
            var outRedirector = new ConsoleOutputRedirector(_logDisplay, System.Console.Out, "OUT");
            var errorRedirector = new ConsoleOutputRedirector(_logDisplay, System.Console.Error, "ERROR");
            System.Console.SetOut(outRedirector);
            System.Console.SetError(errorRedirector);
            _consoleRedirector = outRedirector; // Keep reference for disposal

            // _mainWindow.Data = this;
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
                "• Receipt updates automatically\n\n" +
                "Shortcuts:\n" +
                "• Alt+F4 or Ctrl+Q - Quit\n" +
                "• F1 - Help\n\n" +
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
}
