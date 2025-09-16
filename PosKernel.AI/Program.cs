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
using PosKernel.AI.Interfaces;
using PosKernel.Client;
using PosKernel.Configuration;
using PosKernel.Extensions.Restaurant.Client;

namespace PosKernel.AI
{
    /// <summary>
    /// Main program for POS Kernel AI integration demonstrations.
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
                var useRealKernel = args.Contains("--real") || args.Contains("-r");
                
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

                // Select store configuration
                var storeConfig = await SelectStoreAsync(useRealKernel);
                if (storeConfig == null)
                {
                    return 1;
                }

                // Initialize UI
                var ui = new ConsoleUserInterface(showDebug);
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

            var availableStores = StoreConfigFactory.GetAvailableStores(useRealKernel: false);
            
            System.Console.WriteLine("Select a store experience:");
            for (int i = 0; i < availableStores.Count; i++)
            {
                var store = availableStores[i];
                var personality = AiPersonalityFactory.CreatePersonality(store.PersonalityType);
                System.Console.WriteLine($"{i + 1}. {store.StoreName} ({store.Currency}) - {personality.StaffTitle}");
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
            services.AddLogging(builder => 
            {
                builder.AddConsole();
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

                // Main conversation loop
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
                            Content = $"🎉 Order complete! Total: ${orchestrator.CurrentReceipt.Total:F2} {storeConfig.Currency}",
                            IsSystem = false
                        });
                        break;
                    }
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
            System.Console.WriteLine("  --real, -r           Use real kernel integration");
            System.Console.WriteLine("  --debug, -d          Show debug information");
            System.Console.WriteLine("  --help, -h           Show this help message");
            System.Console.WriteLine();
            System.Console.WriteLine("Examples:");
            System.Console.WriteLine("  PosKernel.AI                    # Mock integration, clean output");
            System.Console.WriteLine("  PosKernel.AI --real --debug     # Real kernel with debug info");
        }
    }
}
