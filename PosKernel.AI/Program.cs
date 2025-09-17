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
using PosKernel.Abstractions.Services;
using PosKernel.AI.Services;
using PosKernel.AI.Tools;
using PosKernel.AI.Core;
using PosKernel.AI.UI.Console;
using PosKernel.AI.UI.Terminal;
using PosKernel.AI.Interfaces;
using PosKernel.Client;
using PosKernel.Configuration;
using PosKernel.Extensions.Restaurant.Client;

namespace PosKernel.AI
{
    /// <summary>
    /// Main program for POS Kernel AI integration demonstrations.
    /// Features a generalized store system with multiple personalities and real kernel integration.
    /// Clean architecture with separated UI concerns for easy Terminal.Gui and web migration.
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            System.Console.OutputEncoding = Encoding.UTF8;
            
            try
            {
                var showDebug = args.Contains("--debug") || args.Contains("-d");
                var useMockKernel = args.Contains("--mock") || args.Contains("-m");  // Changed: Mock is now the flag
                var useTerminalGui = args.Contains("--tui") || args.Contains("-t");
                
                // Auto-detect Terminal.Gui if no explicit UI choice and terminal supports it
                if (!useTerminalGui && !args.Contains("--console") && !args.Contains("-c"))
                {
                    useTerminalGui = IsTerminalGuiSupported();
                }
                
                if (args.Contains("--help") || args.Contains("-h"))
                {
                    ShowHelp();
                    return 0;
                }

                // Initialize configuration
                var config = PosKernelConfiguration.Initialize();
                var aiConfig = new AiConfiguration(config);
                
                if (string.IsNullOrEmpty(aiConfig.ApiKey))
                {
                    System.Console.WriteLine("❌ OpenAI API key not configured!");
                    System.Console.WriteLine("Set OPENAI_API_KEY environment variable or update appsettings.json");
                    return 1;
                }

                System.Console.WriteLine($"✅ API key configured");

                // Show which interface will be used
                if (useTerminalGui)
                {
                    if (args.Contains("--tui") || args.Contains("-t"))
                    {
                        System.Console.WriteLine("🎨 Terminal.Gui interface selected (forced)");
                    }
                    else
                    {
                        System.Console.WriteLine("🎨 Terminal.Gui interface selected (auto-detected)");
                    }
                }
                else
                {
                    if (args.Contains("--console") || args.Contains("-c"))
                    {
                        System.Console.WriteLine("📟 Console interface selected (forced)");
                    }
                    else
                    {
                        System.Console.WriteLine("📟 Console interface selected (auto-detected)");
                    }
                }

                // Show which kernel mode will be used
                if (useMockKernel)
                {
                    System.Console.WriteLine("🎭 Mock kernel integration selected");
                }
                else
                {
                    System.Console.WriteLine("🦀 Real kernel integration selected (default)");
                }
                System.Console.WriteLine();
                
                // Select store configuration - use real kernel by default
                var useRealKernel = !useMockKernel;  // Inverted logic
                var storeConfig = await SelectStoreAsync(useRealKernel);
                if (storeConfig == null)
                {
                    return 1;
                }

                // Initialize UI based on user preference
                IUserInterface ui;
                if (useTerminalGui)
                {
                    ui = new TerminalUserInterface(showDebug);
                    System.Console.WriteLine("Initializing Terminal.Gui interface...");
                    System.Console.WriteLine();
                }
                else
                {
                    ui = new ConsoleUserInterface(showDebug);
                }

                await ui.InitializeAsync();

                // Run the demo
                await RunPosAiDemoAsync(ui, storeConfig, aiConfig, useRealKernel, showDebug);

                await ui.ShutdownAsync();
                return 0;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine($"❌ Fatal error: {ex.Message}");
                if (args.Contains("--debug"))
                {
                    System.Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                }
                return 1;
            }
        }

        private static Task<StoreConfig?> SelectStoreAsync(bool useRealKernel)
        {
            System.Console.WriteLine("🏪 POS KERNEL AI DEMO");
            System.Console.WriteLine("═══════════════════════");
            System.Console.WriteLine();

            var availableStores = StoreConfigFactory.GetAvailableStores(useRealKernel: useRealKernel);
            
            System.Console.WriteLine("Select a store experience:");
            for (int i = 0; i < availableStores.Count; i++)
            {
                var store = availableStores[i];
                var personality = AiPersonalityFactory.CreatePersonality(store.PersonalityType);
                var integrationMode = useRealKernel ? "Real Kernel" : "Mock";
                System.Console.WriteLine($"{i + 1}. {store.StoreName} ({store.Currency}) - {personality.StaffTitle} [{integrationMode}]");
            }
            System.Console.WriteLine();
            
            System.Console.Write($"Select store (1-{availableStores.Count}): ");
            var input = System.Console.ReadLine()?.Trim();
            
            if (int.TryParse(input, out var storeIndex) && storeIndex >= 1 && storeIndex <= availableStores.Count)
            {
                return Task.FromResult<StoreConfig?>(StoreConfigFactory.CreateStore(availableStores[storeIndex - 1].StoreType, useRealKernel));
            }
            
            System.Console.WriteLine("❌ Invalid selection.");
            return Task.FromResult<StoreConfig?>(null);
        }

        private static async Task RunPosAiDemoAsync(
            IUserInterface ui, 
            StoreConfig storeConfig, 
            AiConfiguration aiConfig, 
            bool useRealKernel,
            bool showDebug)
        {
            // Configure services
            var services = new ServiceCollection();
            
            // Configure logging based on UI type - CRITICAL FIX
            if (ui is TerminalUserInterface terminalGuiUi)
            {
                // For Terminal.Gui, DON'T use console logging - it corrupts the display
                services.AddLogging(builder => 
                {
                    // Add a custom logger that routes to our debug pane
                    builder.AddProvider(new TerminalGuiLoggerProvider(terminalGuiUi));
                    builder.SetMinimumLevel(showDebug ? LogLevel.Information : LogLevel.Warning);
                });
            }
            else
            {
                // For console UI, use normal console logging
                services.AddLogging(builder => 
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(showDebug ? LogLevel.Information : LogLevel.Warning);
                });
            }

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

            // Add appropriate POS services
            if (useRealKernel)
            {
                services.AddSingleton<RestaurantExtensionClient>();
                services.AddSingleton<KernelPosToolsProvider>(serviceProvider =>
                {
                    var restaurantClient = serviceProvider.GetRequiredService<RestaurantExtensionClient>();
                    var logger = serviceProvider.GetRequiredService<ILogger<KernelPosToolsProvider>>();
                    var config = serviceProvider.GetRequiredService<IConfiguration>();
                    
                    return new KernelPosToolsProvider(
                        restaurantClient,
                        logger,
                        PosKernelClientFactory.KernelType.RustService,
                        config);
                });
            }
            else
            {
                // Register mock services
                services.AddSingleton<IProductCatalogService, KopitiamProductCatalogService>();
                services.AddSingleton<ProductCustomizationService>();
                services.AddSingleton<PosToolsProvider>();
            }

            // Add orchestrator
            services.AddSingleton<ChatOrchestrator>();

            using var serviceProvider = services.BuildServiceProvider();
            
            try
            {
                // Test connections if using real kernel
                if (useRealKernel)
                {
                    ui.Chat.ShowStatus("Connecting to real kernel services...");
                    var restaurantClient = serviceProvider.GetRequiredService<RestaurantExtensionClient>();
                    var testProducts = await restaurantClient.SearchProductsAsync("test", 1);
                    ui.Chat.ShowStatus($"Connected to Restaurant Extension ({testProducts.Count} test products found)");
                }

                // Create personality and orchestrator
                var personality = AiPersonalityFactory.CreatePersonality(storeConfig.PersonalityType);
                var mcpClient = serviceProvider.GetRequiredService<McpClient>();
                
                ChatOrchestrator orchestrator;
                if (useRealKernel)
                {
                    var kernelTools = serviceProvider.GetRequiredService<KernelPosToolsProvider>();
                    var logger = serviceProvider.GetRequiredService<ILogger<ChatOrchestrator>>();
                    orchestrator = new ChatOrchestrator(mcpClient, personality, storeConfig, logger, kernelToolsProvider: kernelTools);
                }
                else
                {
                    var mockTools = serviceProvider.GetRequiredService<PosToolsProvider>();
                    var logger = serviceProvider.GetRequiredService<ILogger<ChatOrchestrator>>();
                    orchestrator = new ChatOrchestrator(mcpClient, personality, storeConfig, logger, mockToolsProvider: mockTools);
                }

                // Show store info
                ui.Chat.ShowMessage(new Models.ChatMessage
                {
                    Sender = "System",
                    Content = $"🏪 Welcome to {storeConfig.StoreName}!",
                    IsSystem = false
                });

                ui.Chat.ShowMessage(new Models.ChatMessage
                {
                    Sender = "System", 
                    Content = $"Integration: {(useRealKernel ? "Real Kernel" : "Mock")} | Currency: {storeConfig.Currency}",
                    IsSystem = true,
                    ShowInCleanMode = showDebug
                });

                // Initialize and get AI greeting
                var greetingMessage = await orchestrator.InitializeAsync();
                ui.Chat.ShowMessage(greetingMessage);
                ui.Receipt.UpdateReceipt(orchestrator.CurrentReceipt);

                // For Terminal.Gui, set up the orchestrator reference
                if (ui is TerminalUserInterface terminalUi)
                {
                    terminalUi.SetOrchestrator(orchestrator);
                    
                    // Run the UI
                    await terminalUi.RunAsync();
                }
                else
                {
                    // Console mode - run conversation loop directly
                    await HandleConversationAsync(ui, orchestrator);
                }
            }
            catch (Exception ex)
            {
                ui.Chat.ShowError($"Demo failed: {ex.Message}");
                if (showDebug)
                {
                    ui.Chat.ShowError($"Details: {ex.StackTrace}");
                }
            }
        }

        private static async Task HandleConversationAsync(IUserInterface ui, ChatOrchestrator orchestrator)
        {
            // For Terminal.Gui, the conversation is handled through the UI event system
            if (ui is TerminalUserInterface)
            {
                // The Terminal.Gui will handle the conversation through its event system
                // We don't run a separate conversation loop
                return;
            }

            // Console mode - run conversation loop directly
            while (true)
            {
                var userInput = await ui.Chat.GetUserInputAsync();
                
                if (string.IsNullOrEmpty(userInput))
                {
                    continue;
                }

                // Process user input
                var response = await orchestrator.ProcessUserInputAsync(userInput);
                ui.Chat.ShowMessage(response);
                ui.Receipt.UpdateReceipt(orchestrator.CurrentReceipt);

                // Check for exit
                if (IsExitCommand(userInput))
                {
                    break;
                }

                // Handle completion
                if (IsCompletionRequest(userInput) && orchestrator.CurrentReceipt.Items.Any())
                {
                    ui.Chat.ShowMessage(new Models.ChatMessage
                    {
                        Sender = "System",
                        Content = $"🎉 Order complete! Total: ${orchestrator.CurrentReceipt.Total:F2} {orchestrator.StoreConfig.Currency}",
                        IsSystem = false
                    });
                    break;
                }
            }
        }

        private static bool IsExitCommand(string input)
        {
            var exitCommands = new[] { "exit", "quit", "bye", "goodbye", "done", "thanks", "thank you" };
            return exitCommands.Contains(input.ToLowerInvariant());
        }

        private static bool IsCompletionRequest(string input)
        {
            var completionCommands = new[] { "that's all", "that's it", "complete", "finish", "pay", "checkout", "done", "finish order", "that'll be all", "nothing else" };
            return completionCommands.Any(cmd => input.ToLowerInvariant().Contains(cmd));
        }

        private static void ShowHelp()
        {
            System.Console.WriteLine("Usage: PosKernel.AI [options]");
            System.Console.WriteLine();
            System.Console.WriteLine("Options:");
            System.Console.WriteLine("  --mock, -m           Use mock kernel integration (development mode)");
            System.Console.WriteLine("  --tui, -t            Force Terminal.Gui interface");
            System.Console.WriteLine("  --console, -c        Force console interface");
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
            System.Console.WriteLine("Interface Selection:");
            System.Console.WriteLine("  By default, the app auto-detects the best interface:");
            System.Console.WriteLine("  • Terminal.Gui (TUI) for full-featured terminals");
            System.Console.WriteLine("  • Console for basic terminals or redirected I/O");
            System.Console.WriteLine();
            System.Console.WriteLine("Examples:");
            System.Console.WriteLine("  PosKernel.AI                    # Real kernel + auto-detect interface");
            System.Console.WriteLine("  PosKernel.AI --tui              # Real kernel + Terminal.Gui interface");
            System.Console.WriteLine("  PosKernel.AI --debug            # Real kernel + debug logging");
            System.Console.WriteLine("  PosKernel.AI --mock             # Mock integration for development");
            System.Console.WriteLine("  PosKernel.AI --mock --console   # Mock + console for testing");
            System.Console.WriteLine();
            System.Console.WriteLine("Prerequisites for Real Mode:");
            System.Console.WriteLine("  1. Start the Rust kernel service: cd pos-kernel-rs && cargo run --bin service");
            System.Console.WriteLine("  2. Ensure SQLite database exists: data/catalog/restaurant_catalog.db");
            System.Console.WriteLine("  3. Configure OpenAI API key in environment or appsettings.json");
        }

        private static bool IsTerminalGuiSupported()
        {
            // Check if we're running in a console that supports Terminal.Gui
            try
            {
                // Basic checks for Terminal.Gui compatibility
                if (System.Console.IsOutputRedirected || System.Console.IsInputRedirected)
                {
                    return false; // Don't use TUI if output/input is redirected
                }

                // Check if we have a proper console window - be more lenient
                if (System.Console.WindowWidth < 60 || System.Console.WindowHeight < 20)
                {
                    return false; // Too small for a good TUI experience
                }

                return true; // Assume Terminal.Gui will work
            }
            catch
            {
                return false; // If we can't check, default to console mode
            }
        }
    }

    /// <summary>
    /// Custom logger provider that routes log messages to Terminal.Gui debug pane instead of console.
    /// Prevents console output from corrupting the TUI display.
    /// </summary>
    internal class TerminalGuiLoggerProvider : ILoggerProvider
    {
        private readonly TerminalUserInterface _terminalUi;

        public TerminalGuiLoggerProvider(TerminalUserInterface terminalUi)
        {
            _terminalUi = terminalUi ?? throw new ArgumentNullException(nameof(terminalUi));
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new TerminalGuiLogger(categoryName, _terminalUi);
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Custom logger that sends log messages to Terminal.Gui debug pane.
    /// </summary>
    internal class TerminalGuiLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly TerminalUserInterface _terminalUi;

        public TerminalGuiLogger(string categoryName, TerminalUserInterface terminalUi)
        {
            _categoryName = categoryName;
            _terminalUi = terminalUi;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var logLevelText = logLevel switch
            {
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
            
            if (exception != null)
            {
                logEntry += $"\n      {exception}";
            }

            // Route to debug pane instead of console
            _terminalUi.LogDisplay?.AddLog(logEntry);
        }
    }
}
