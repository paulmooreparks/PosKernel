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

using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Terminal.Gui;
using PosKernel.Abstractions.Services;
using PosKernel.AI.Services;
using PosKernel.AI.Tools;
using PosKernel.AI.Core;
using PosKernel.AI.UI.Terminal;
using PosKernel.AI.Interfaces;
using PosKernel.Client;
using PosKernel.Configuration;
using PosKernel.Extensions.Restaurant.Client;

namespace PosKernel.AI {
    /// <summary>
    /// Main program for POS Kernel AI integration demonstrations.
    /// Features a generalized store system with multiple personalities and real kernel integration.
    /// Clean architecture with separated UI concerns for easy Terminal.Gui and web migration.
    /// </summary>
    class Program {
        static async Task<int> Main(string[] args) {
            System.Console.OutputEncoding = Encoding.UTF8;

            try {
                var showDebug = args.Contains("--debug") || args.Contains("-d");
                var useMockKernel = args.Contains("--mock") || args.Contains("-m");

                if (args.Contains("--help") || args.Contains("-h")) {
                    ShowHelp();
                    return 0;
                }

                // Initialize configuration
                var config = PosKernelConfiguration.Initialize();
                var aiConfig = new AiConfiguration(config);

                if (string.IsNullOrEmpty(aiConfig.ApiKey)) {
                    System.Console.WriteLine("❌ OpenAI API key not configured!");
                    System.Console.WriteLine("Set OPENAI_API_KEY environment variable or update appsettings.json");
                    return 1;
                }

                // Always use Terminal.Gui interface - no console mode
                var ui = new TerminalUserInterface();
                await ui.InitializeAsync();

                // Show store selection dialog within the TUI and run the demo
                var useRealKernel = !useMockKernel;
                await RunPosAiDemoAsync(ui, aiConfig, useRealKernel, showDebug);

                await ui.ShutdownAsync();
                return 0;
            }
            catch (Exception ex) {
                System.Console.Error.WriteLine($"❌ Fatal error: {ex.Message}");
                if (args.Contains("--debug")) {
                    System.Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                }
                return 1;
            }
        }

        private static async Task RunPosAiDemoAsync(
            IUserInterface ui,
            AiConfiguration aiConfig,
            bool useRealKernel,
            bool showDebug) {
            try {
                // Configure services FIRST
                var services = new ServiceCollection();

                // Always use Terminal.Gui logging since we removed console mode
                var terminalGuiUi = (TerminalUserInterface) ui;
                services.AddLogging(builder => {
                    builder.AddProvider(new TerminalGuiLoggerProvider(terminalGuiUi));
                    builder.SetMinimumLevel(showDebug ? LogLevel.Information : LogLevel.Warning);
                });

                // Add configuration
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();
                services.AddSingleton<IConfiguration>(configuration);

                // Add MCP services
                var mcpConfig = aiConfig.ToMcpConfiguration();
                services.AddSingleton(mcpConfig);
                services.AddSingleton<McpClient>();

                // Add appropriate POS services - NO FALLBACKS!
                if (useRealKernel) {
                    ui.Log.AddLog("INFO: Configuring real kernel integration...");
                    
                    // Register services without try/catch - let failures propagate
                    services.AddSingleton<RestaurantExtensionClient>();
                    services.AddSingleton<KernelPosToolsProvider>(serviceProvider => {
                        var restaurantClient = serviceProvider.GetRequiredService<RestaurantExtensionClient>();
                        var logger = serviceProvider.GetRequiredService<ILogger<KernelPosToolsProvider>>();
                        var config = serviceProvider.GetRequiredService<IConfiguration>();

                        ui.Log.AddLog("INFO: Creating KernelPosToolsProvider with Rust service...");
                        
                        return new KernelPosToolsProvider(
                            restaurantClient,
                            logger,
                            PosKernelClientFactory.KernelType.RustService,
                            config);
                    });
                    ui.Log.AddLog("SUCCESS: Real kernel services configured");
                }
                else {
                    ui.Log.AddLog("INFO: Using mock services (development mode)");
                    // Register mock services
                    services.AddSingleton<IProductCatalogService, KopitiamProductCatalogService>();
                    services.AddSingleton<ProductCustomizationService>();
                    services.AddSingleton<PosToolsProvider>();
                }

                // Add orchestrator
                services.AddSingleton<ChatOrchestrator>();

                using var serviceProvider = services.BuildServiceProvider();

                // Show store selection dialog within the TUI AFTER services are configured
                var storeSelectionResult = await ShowStoreSelectionDialogAsync(ui, useRealKernel);
                if (storeSelectionResult == null) {
                    ui.Log.AddLog("Store selection cancelled - exiting application immediately");
                    // Force application to exit completely
                    await ui.ShutdownAsync();
                    Environment.Exit(0);
                    return; // This shouldn't be reached, but just in case
                }

                ui.Log.AddLog($"Selected store: {storeSelectionResult.StoreName}");

                try {
                    // Create the orchestrator after store selection
                    var personality = AiPersonalityFactory.CreatePersonality(storeSelectionResult.PersonalityType);
                    var mcpClient = serviceProvider.GetRequiredService<McpClient>();

                    // Add store configuration to DI for kernel tools to access
                    services.AddSingleton(storeSelectionResult);

                    // Rebuild service provider with store config
                    using var updatedServiceProvider = services.BuildServiceProvider();

                    ChatOrchestrator orchestrator;
                    if (useRealKernel) {
                        var kernelTools = updatedServiceProvider.GetRequiredService<KernelPosToolsProvider>();
                        var logger = updatedServiceProvider.GetRequiredService<ILogger<ChatOrchestrator>>();
                        orchestrator = new ChatOrchestrator(mcpClient, personality, storeSelectionResult, logger, kernelToolsProvider: kernelTools);
                    }
                    else {
                        var mockTools = updatedServiceProvider.GetRequiredService<PosToolsProvider>();
                        var logger = updatedServiceProvider.GetRequiredService<ILogger<ChatOrchestrator>>();
                        orchestrator = new ChatOrchestrator(mcpClient, personality, storeSelectionResult, logger, mockToolsProvider: mockTools);
                    }

                    // Set orchestrator - UI now handles the case where it's not available gracefully
                    terminalGuiUi.SetOrchestrator(orchestrator);
                    ui.Log.AddLog("Orchestrator set successfully");

                    // Test connections if using real kernel - NO GRACEFUL HANDLING
                    if (useRealKernel) {
                        ui.Log.ShowStatus("Connecting to real kernel services...");
                        
                        // Test Restaurant Extension connection - FAIL FAST if not available
                        var restaurantClient = serviceProvider.GetRequiredService<RestaurantExtensionClient>();
                        ui.Log.AddLog("INFO: Testing Restaurant Extension Service connection...");
                        var testProducts = await restaurantClient.SearchProductsAsync("test", 1);
                        ui.Log.ShowStatus($"✅ Connected to Restaurant Extension ({testProducts.Count} test products found)");
                        
                        // Test Kernel connection - FAIL FAST if not available  
                        var kernelTools = serviceProvider.GetRequiredService<KernelPosToolsProvider>();
                        ui.Log.AddLog("INFO: Testing Kernel Service connection...");
                        // The kernel connection will be tested on first use - let it fail there if needed
                        ui.Log.ShowStatus("✅ Kernel tools provider ready");
                    }

                    // Show store info
                    ui.Chat.ShowMessage(new Models.ChatMessage {
                        Sender = "System",
                        Content = $"Welcome to {storeSelectionResult.StoreName}!",
                        IsSystem = false
                    });

                    ui.Log.AddLog($"Integration: {(useRealKernel ? "Real Kernel" : "Mock")} | Currency: {storeSelectionResult.Currency}");

                    // Initialize and get AI greeting
                    var greetingMessage = await orchestrator.InitializeAsync();
                    ui.Chat.ShowMessage(greetingMessage);
                    ui.Receipt.UpdateReceipt(orchestrator.CurrentReceipt);

                    // Now run the UI with orchestrator already set
                    await terminalGuiUi.RunAsync();
                }
                catch (Exception ex) {
                    ui.Log.ShowError($"Demo initialization failed: {ex.Message}");
                    if (showDebug) {
                        ui.Log.AddLog($"Details: {ex.StackTrace}");
                    }

                    // Show error dialog and wait
                    MessageBox.ErrorQuery("Initialization Error", $"Failed to start demo:\n{ex.Message}\n\nPress OK to exit.", "OK");
                }
            }
            catch (Exception ex) {
                ui.Log.ShowError($"Fatal error in demo setup: {ex.Message}");
                if (showDebug) {
                    ui.Log.AddLog($"Details: {ex.StackTrace}");
                }

                // Show error dialog
                MessageBox.ErrorQuery("Fatal Error", $"Critical error:\n{ex.Message}\n\nPress OK to exit.", "OK");
            }
        }

        private static void ShowHelp() {
            System.Console.WriteLine("Usage: PosKernel.AI [options]");
            System.Console.WriteLine();
            System.Console.WriteLine("Options:");
            System.Console.WriteLine("  --mock, -m           Use mock kernel integration (development mode)");
            System.Console.WriteLine("  --debug, -d          Show debug information");
            System.Console.WriteLine("  --help, -h           Show this help message");
            System.Console.WriteLine();
            System.Console.WriteLine("Integration Modes:");
            System.Console.WriteLine("  By default, the app uses REAL kernel integration with:");
            System.Console.WriteLine("  • Real Rust POS Kernel service (http://127.0.0.1:8080)");
            System.Console.WriteLine("  • Restaurant Extension with SQLite product catalog");
            System.Console.WriteLine("  • ACID-compliant transaction processing");
            System.Console.WriteLine("  • Multi-process terminal coordination");
            System.Console.WriteLine();
            System.Console.WriteLine("Interface:");
            System.Console.WriteLine("  • Uses Terminal.Gui (TUI) interface");
            System.Console.WriteLine("  • Store selection shown as in-app dialog");
            System.Console.WriteLine();
            System.Console.WriteLine("Examples:");
            System.Console.WriteLine("  PosKernel.AI                    # Real kernel integration");
            System.Console.WriteLine("  PosKernel.AI --debug            # Real kernel + debug logging");
            System.Console.WriteLine("  PosKernel.AI --mock             # Mock integration for development");
            System.Console.WriteLine();
            System.Console.WriteLine("Prerequisites for Real Mode:");
            System.Console.WriteLine("  1. Start the Rust kernel service: cd pos-kernel-rs && cargo run --bin service");
            System.Console.WriteLine("  2. Ensure SQLite database exists: data/catalog/restaurant_catalog.db");
            System.Console.WriteLine("  3. Configure OpenAI API key in environment or appsettings.json");
        }

        private static Task<StoreConfig?> ShowStoreSelectionDialogAsync(IUserInterface ui, bool useRealKernel) {
            var availableStores = StoreConfigFactory.GetAvailableStores(useRealKernel: useRealKernel);

            // Create store descriptions
            var storeDescriptions = new string[availableStores.Count];
            for (int i = 0; i < availableStores.Count; i++) {
                var store = availableStores[i];
                var personality = AiPersonalityFactory.CreatePersonality(store.PersonalityType);
                storeDescriptions[i] = $"{store.StoreName} - {personality.StaffTitle}";
            }

            // Create dialog window with proper Terminal.Gui v2 API
            var dialog = new Window() {
                Title = $"Store Selection [{(useRealKernel ? "Real Kernel" : "Mock")}]",
                X = Pos.Center(),
                Y = Pos.Center(),
                Width = 60,
                Height = 12 + storeDescriptions.Length,
                Modal = true
            };

            // Add instruction label
            var instructionLabel = new Label() {
                Text = "Select a store experience:",
                X = 2,
                Y = 1,
                Width = Dim.Fill(2),
                Height = 1
            };

            // Create radio group with store options
            var radioGroup = new RadioGroup() {
                X = 2,
                Y = 3,
                Width = Dim.Fill(2),
                Height = storeDescriptions.Length,
                RadioLabels = storeDescriptions
            };

            // Create OK button
            var okButton = new Button() {
                Title = " OK ",
                Width = 10,
                X = Pos.Center() - 5,
                Y = Pos.AnchorEnd(2),
                IsDefault = true
            };

            // Create Cancel button  
            var cancelButton = new Button() {
                Title = " Cancel ",
                Width = 10,
                X = Pos.Center() + 5,
                Y = Pos.AnchorEnd(2)
            };

            StoreConfig? selectedStore = null;
            bool dialogCancelled = false;

            okButton.Selecting += (sender, e) => {
                var selectedIndex = radioGroup.SelectedItem;
                if (selectedIndex >= 0 && selectedIndex < availableStores.Count) {
                    selectedStore = StoreConfigFactory.CreateStore(availableStores[selectedIndex].StoreType, useRealKernel);
                    dialogCancelled = false;
                    ui.Log.AddLog($"OK pressed - selected store: {selectedStore.StoreName}");
                }
                Application.RequestStop();
            };

            // Handle Cancel button - FORCE EXIT
            cancelButton.Selecting += (sender, e) => {
                ui.Log.AddLog("Cancel button pressed - will exit application");
                selectedStore = null;
                dialogCancelled = true;
                Application.RequestStop();
            };

            // Handle Enter key on radio group
            radioGroup.KeyDown += (_, e) => {
                if (e.KeyCode == KeyCode.Enter) {
                    var selectedIndex = radioGroup.SelectedItem;
                    if (selectedIndex >= 0 && selectedIndex < availableStores.Count) {
                        selectedStore = StoreConfigFactory.CreateStore(availableStores[selectedIndex].StoreType, useRealKernel);
                        dialogCancelled = false;
                        ui.Log.AddLog($"Enter pressed - selected store: {selectedStore.StoreName}");
                    }
                    Application.RequestStop();
                    e.Handled = true;
                }
            };

            // Handle Escape key on dialog - FORCE EXIT
            dialog.KeyDown += (_, e) => {
                if (e.KeyCode == KeyCode.Esc) {
                    ui.Log.AddLog("ESC key pressed - will exit application");
                    selectedStore = null;
                    dialogCancelled = true;
                    Application.RequestStop();
                    e.Handled = true;
                }
            };

            // Add all controls to dialog
            dialog.Add(instructionLabel, radioGroup, okButton, cancelButton);

            // Set initial focus to radio group
            radioGroup.SetFocus();

            // Run the dialog
            Application.Run(dialog);
            dialog.Dispose();

            // Final check and logging
            if (dialogCancelled) {
                ui.Log.AddLog("Dialog was cancelled - returning null to exit application");
                return Task.FromResult<StoreConfig?>(null);
            }
            else if (selectedStore != null) {
                ui.Log.AddLog($"Dialog completed successfully - returning store: {selectedStore.StoreName}");
                return Task.FromResult(selectedStore);
            }
            else {
                ui.Log.AddLog("Dialog completed but no store selected - returning null to exit application");
                return Task.FromResult<StoreConfig?>(null);
            }
        }
    }

    /// <summary>
    /// Custom logger provider that routes log messages to Terminal.Gui debug pane instead of console.
    /// Prevents console output from corrupting the TUI display.
    /// </summary>
    internal class TerminalGuiLoggerProvider : ILoggerProvider {
        private readonly TerminalUserInterface _terminalUi;

        public TerminalGuiLoggerProvider(TerminalUserInterface terminalUi) {
            _terminalUi = terminalUi ?? throw new ArgumentNullException(nameof(terminalUi));
        }

        public ILogger CreateLogger(string categoryName) {
            return new TerminalGuiLogger(categoryName, _terminalUi);
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Custom logger that sends log messages to Terminal.Gui debug pane.
    /// </summary>
    internal class TerminalGuiLogger : ILogger {
        private readonly string _categoryName;
        private readonly TerminalUserInterface _terminalUi;

        public TerminalGuiLogger(string categoryName, TerminalUserInterface terminalUi) {
            _categoryName = categoryName;
            _terminalUi = terminalUi;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) {
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (!IsEnabled(logLevel)) {
                return;
            }

            var message = formatter(state, exception);
            var logLevelText = logLevel switch {
                LogLevel.Trace => "trce",
                LogLevel.Debug => "dbug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "fail",
                LogLevel.Critical => "crit",
                _ => "unkn"
            };

            var categoryShort = _categoryName.Split('.').LastOrDefault() ?? _categoryName;
            var logEntry = $"{logLevelText}: {categoryShort}[{eventId.Id}] {message}";

            if (exception != null) {
                logEntry += $"\n      {exception}";
            }

            // Route to debug pane instead of console
            _terminalUi.LogDisplay?.AddLog(logEntry);
        }
    }
}
