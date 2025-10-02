using Microsoft.AspNetCore.Builder;//

using Microsoft.Extensions.DependencyInjection;// Copyright 2025 Paul Moore Parks and contributors

using Microsoft.Extensions.Hosting;//

using Microsoft.Extensions.Logging;// Licensed under the Apache License, Version 2.0 (the "License");

using PosKernel.AI.Core;// you may not use this file except in compliance with the License.

using PosKernel.AI.Models;// You may obtain a copy of the License at

using PosKernel.AI.Services;//

using PosKernel.Configuration;//     http://www.apache.org/licenses/LICENSE-2.0

using System.Text;//

// Unless required by applicable law or agreed to in writing, software

namespace PosKernel.AI.ServiceHost;// distributed under the License is distributed on an "AS IS" BASIS,

// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

class Program// See the License for the specific language governing permissions and

{// limitations under the License.

    static async Task<int> Main(string[] args)//

    {

        Console.OutputEncoding = Encoding.UTF8;using System.Text;

using Microsoft.AspNetCore.Builder;

        tryusing Microsoft.AspNetCore.Http;

        {using Microsoft.AspNetCore.Hosting;

            var config = PosKernelConfiguration.Initialize();using Microsoft.Extensions.DependencyInjection;

            using Microsoft.Extensions.Hosting;

            Console.WriteLine("AI Service Host v0.5.0");using Microsoft.Extensions.Logging;

            using Microsoft.Extensions.Configuration;

            var builder = WebApplication.CreateBuilder(args);using PosKernel.Abstractions;

            using PosKernel.Abstractions.Services;

            builder.Logging.ClearProviders();using PosKernel.AI.Services;

            builder.Logging.AddConsole();using PosKernel.AI.Tools;

            using PosKernel.AI.Core;

            var storeConfig = new StoreConfigurationusing PosKernel.AI.Interfaces;

            {using PosKernel.Configuration;

                StoreName = "Toast Boleh",using PosKernel.Configuration.Services;

                StoreType = StoreType.Kopitiam,using PosKernel.AI.Models;

                Currency = "SGD"using PosKernel.Extensions.Restaurant.Client;

            };using System.Diagnostics;

            using PosKernel.AI.Common.Abstractions;

            var personality = new PersonalityConfigurationusing PosKernel.AI.Common.Factories;

            {

                PersonalityType = PersonalityType.SingaporeanKopitiamUncle,namespace PosKernel.AI.ServiceHost;

                StaffTitle = "Uncle",

                CultureInfo = "en-SG"/// <summary>

            };/// Headless AI Service Host for POS Kernel AI integration.

            /// Provides cross-platform HTTP API for AI services.

            await builder.Services.ConfigurePosAiServicesAsync(storeConfig, personality, false);/// Can be controlled via CLI commands and provides continuous logging.

            ///

            var app = builder.Build();/// ARCHITECTURAL PRINCIPLE: Same business logic as Demo, different interface.

            /// </summary>

            app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));class Program

            {

            Console.WriteLine("Starting on http://localhost:5000");    static async Task<int> Main(string[] args)

                {

            await app.RunAsync();        Console.OutputEncoding = Encoding.UTF8;



            return 0;        try

        }        {

        catch (Exception ex)            // ARCHITECTURAL PRINCIPLE: Initialize PosKernel configuration to load .env file

        {            var config = PosKernelConfiguration.Initialize();

            Console.WriteLine($"FATAL ERROR: {ex.Message}");

            return 1;            var showDebug = args.Contains("--debug") || args.Contains("-d");

        }            var storeType = GetStoreTypeFromArgs(args);

    }

}            if (args.Contains("--help") || args.Contains("-h"))
            {
                ShowHelp();
                return 0;
            }

            Console.WriteLine("🤖 POS Kernel AI Service Host v0.5.0");
            Console.WriteLine("Cross-platform AI cashier service with HTTP API");

            // Create minimal configuration for service
            var storeConfig = new StoreConfiguration
            {
                StoreName = "Toast Boleh",
                StoreType = StoreType.Kopitiam,
                Currency = "SGD"
            };

            var personality = new PersonalityConfiguration
            {
                PersonalityType = PersonalityType.SingaporeanKopitiamUncle,
                StaffTitle = "Uncle",
                CultureInfo = "en-SG"
            };

            // Build ASP.NET Core application
            var builder = WebApplication.CreateBuilder(args);

            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();

            await builder.Services.ConfigurePosAiServicesAsync(storeConfig, personality, showDebug);

            var app = builder.Build();

            app.MapGet("/health", () => Results.Ok(new { Status = "Healthy" }));

            Console.WriteLine("🚀 Starting on http://localhost:5000");

            await app.RunAsync();

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ FATAL ERROR: {ex.Message}");
            Console.WriteLine($"🔍 Exception details: {ex}");
            return 1;
        }
    }

    private static StoreType GetStoreTypeFromArgs(string[] args)
    {
        var storeArg = args.FirstOrDefault(arg => arg.StartsWith("--store="));
        if (storeArg != null)
        {
            var storeValue = storeArg.Split('=')[1];
            if (Enum.TryParse<StoreType>(storeValue, true, out var parsedStore))
            {
                return parsedStore;
            }
        }

        // Default to Kopitiam for testing
        return StoreType.Kopitiam;
    }

    private static void ShowHelp()
    {
        Console.WriteLine("🤖 POS Kernel AI Service Host");
        Console.WriteLine();
        Console.WriteLine("USAGE:");
        Console.WriteLine("  dotnet run [options]");
        Console.WriteLine();
        Console.WriteLine("OPTIONS:");
        Console.WriteLine("  --store=<type>    Store type (Kopitiam, CoffeeShop, Boulangerie, Generic)");
        Console.WriteLine("  --debug, -d       Enable debug logging");
        Console.WriteLine("  --help, -h        Show this help message");
        Console.WriteLine();
        Console.WriteLine("EXAMPLES:");
        Console.WriteLine("  dotnet run --store=Kopitiam --debug");
        Console.WriteLine("  dotnet run --store=CoffeeShop");
        Console.WriteLine();
        Console.WriteLine("NOTES:");
        Console.WriteLine("  - Restaurant Extension Service must be running first");
        Console.WriteLine("  - Use PosKernel.AI.Cli to send commands to this service");
        Console.WriteLine("  - HTTP API available at http://localhost:5000");
        Console.WriteLine("  - Cross-platform: Windows, Linux, macOS supported");
    }
}

/// <summary>
/// Request payload for chat API endpoint.
/// </summary>
public class ChatRequest
{
    public string Prompt { get; set; } = "";
    public string? Timestamp { get; set; }
}

/// <summary>
/// Response payload for chat API endpoint.
/// </summary>
public class ChatResponse
{
    public bool Success { get; set; } = true;
    public string Response { get; set; } = "";
    public string? ProcessingTime { get; set; }
    public string? Error { get; set; }
}

            if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("❌ OpenAI API key required when STORE_AI_PROVIDER=OpenAI");
                Console.WriteLine("Set OPENAI_API_KEY in ~/.poskernel/.env or use local provider (STORE_AI_PROVIDER=Ollama)");
                Console.WriteLine($"Config directory: {PosKernelConfiguration.ConfigDirectory}");
                return 1;
            }

            // Determine kernel mode
            var useRealKernel = !useMockKernel;

            // Initialize UI
            var ui = new TerminalUserInterface();
            await ui.InitializeAsync();

            // Show store selection dialog
            var storeSelectionResult = await ShowStoreSelectionDialogAsync(ui, useRealKernel);
            if (storeSelectionResult == null)
            {
                ui.Log.AddLog("No store selected - exiting application");
                await ui.ShutdownAsync();
                return 0;
            }

            // Setup services
            var services = new ServiceCollection();

            // Add Terminal.Gui logger that routes to debug pane instead of console
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddProvider(new TerminalGuiLoggerProvider(ui));
                builder.SetMinimumLevel(showDebug ? LogLevel.Debug : LogLevel.Information);
            });

            // Add configuration services
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection()
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<ICurrencyFormattingService, CurrencyFormattingService>();
            services.AddSingleton<IStoreConfigurationService, PosKernel.AI.Demo.Services.SimpleStoreConfigurationService>();

            // ARCHITECTURAL FIX: Use shared LLM factory and wire it into MCP client
            services.AddSingleton<ILargeLanguageModel>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ILargeLanguageModel>>();
                return LLMProviderFactory.CreateLLM(provider, model, baseUrl, apiKey, logger);
            });

            // Wire MCP client to use shared LLM interface
            var mcpConfig = storeAiConfig.ToMcpConfiguration();
            services.AddSingleton(mcpConfig);
            services.AddSingleton<McpClient>();

            // Configure payment methods for selected store
            if (storeSelectionResult.PaymentMethods == null)
            {
                storeSelectionResult.PaymentMethods = new PaymentMethodsConfiguration
                {
                    AcceptsCash = true,
                    AcceptsDigitalPayments = true,
                    DefaultMethod = "cash",
                    PaymentInstructions = GetPaymentInstructionsForStore(storeSelectionResult),
                    AcceptedMethods = GetAcceptedPaymentMethodsForStore(storeSelectionResult)
                };
            }

            // Create personality for store
            var personality = CreatePersonalityForStore(storeSelectionResult);

            // CRITICAL DEBUG: Verify personality creation
            ui.Log.AddLog($"🔍 PERSONALITY_DEBUG: Created personality Type={personality.Type}, StaffTitle={personality.StaffTitle} for StoreType={storeSelectionResult.StoreType}");
            ui.Log.AddLog($"🔍 STORE_DEBUG: Selected store PersonalityType={storeSelectionResult.PersonalityType}, StoreType={storeSelectionResult.StoreType}");

            // Add tools providers
            if (useRealKernel)
            {
                ui.Log.AddLog("Setting up real kernel integration...");

                try
                {
                    // ARCHITECTURAL PRINCIPLE: FAIL FAST - No silent fallbacks to mock
                    // Create RestaurantExtensionClient first with temporary service provider
                    using var tempServiceProvider = services.BuildServiceProvider();
                    var tempLogger = tempServiceProvider.GetRequiredService<ILogger<RestaurantExtensionClient>>();
                    var restaurantClient = new RestaurantExtensionClient(tempLogger);

                    ui.Log.AddLog("INFO: Testing Restaurant Extension Service connection...");

                    // Test the connection by attempting to search for products
                    var testProducts = await restaurantClient.SearchProductsAsync("test", 1);
                    ui.Log.ShowStatus($"✅ Connected to Restaurant Extension ({testProducts.Count} test products found)");

                    // Add real kernel tools to DI with the correct constructor signature
                    services.AddSingleton<RestaurantExtensionClient>(_ => restaurantClient);
                    services.AddSingleton<KernelPosToolsProvider>(provider =>
                        new KernelPosToolsProvider(
                            restaurantClient,
                            provider.GetRequiredService<ILogger<KernelPosToolsProvider>>(),
                            storeSelectionResult,
                            PosKernel.Client.PosKernelClientFactory.KernelType.RustService,
                            configuration,
                            provider.GetRequiredService<ICurrencyFormattingService>()));

                    // Register ConfigurableContextBuilder for context-driven AI system
                    services.AddSingleton<ConfigurableContextBuilder>(provider =>
                        new ConfigurableContextBuilder(
                            personality.Type,
                            provider.GetRequiredService<ILogger<ConfigurableContextBuilder>>()));

                    // Register PosAiAgent for real kernel mode
                    services.AddSingleton<PosAiAgent>(provider =>
                    {
                        var storeConfig = provider.GetRequiredService<StoreConfig>();
                        return new PosAiAgent(
                            provider.GetRequiredService<McpClient>(),
                            provider.GetRequiredService<ILogger<PosAiAgent>>(),
                            storeConfig,
                            new Receipt(),
                            new Transaction(storeConfig.Currency),
                            provider.GetRequiredService<ConfigurableContextBuilder>(),
                            provider.GetRequiredService<KernelPosToolsProvider>());
                    });
                }
                catch (Exception ex)
                {
                    // ARCHITECTURAL PRINCIPLE: FAIL FAST - Don't hide service unavailability
                    var errorMessage =
                        $"DESIGN DEFICIENCY: Real kernel mode requested but Restaurant Extension Service is not available.\n" +
                        $"Error: {ex.Message}\n\n" +
                        $"To use real kernel mode:\n" +
                        $"1. Start the Rust kernel service: cd pos-kernel-rs && cargo run --bin service\n" +
                        $"2. Ensure Restaurant Extension Service is running and accessible\n" +
                        $"3. Verify SQLite database exists: data/catalog/restaurant_catalog.db\n\n" +
                        $"To use development mode instead: PosKernel.AI.Demo --mock";

                    ui.Log.ShowError(errorMessage);
                    ui.Chat.ShowMessage(new ChatMessage
                    {
                        Sender = "System",
                        Content = "❌ Real kernel services not available. Cannot start without proper service connections.",
                        IsSystem = true,
                        ShowInCleanMode = true
                    });

                    // FAIL FAST - Exit immediately instead of falling back to mock
                    await ui.ShutdownAsync();
                    return 1;
                }
            }
            else
            {
                    // FAIL FAST - Exit immediately since mock mode is no longer supported
                    ui.Log.ShowError("ARCHITECTURAL CHANGE: Mock mode has been removed. Only real kernel mode is supported.");
                    ui.Chat.ShowMessage(new ChatMessage
                    {
                        Sender = "System",
                        Content = "❌ Mock mode no longer supported. Please start the Rust kernel service and restaurant extension.",
                        IsSystem = true,
                        ShowInCleanMode = true
                    });

                    await ui.ShutdownAsync();
                    return 1;
            }

            // Add store configuration and orchestrator to services
            services.AddSingleton(storeSelectionResult);
            services.AddSingleton(personality);

            // Rebuild service provider with all services including store config and tools
            using var updatedServiceProvider = services.BuildServiceProvider();

            try
            {
                // ARCHITECTURAL FIX: Get AI agent from DI container to ensure all dependencies are injected
                var aiAgent = updatedServiceProvider.GetRequiredService<PosAiAgent>();
                ui.Log.AddLog($"✅ {(useRealKernel ? "Real kernel" : "Mock")} orchestrator created successfully");

                // Set orchestrator - UI now handles the case where it's not available gracefully
                var terminalGuiUI = (TerminalUserInterface)ui;
                terminalGuiUI.SetAiAgent(aiAgent);

                // Set up currency services for receipt display
                var currencyFormatter = updatedServiceProvider.GetRequiredService<ICurrencyFormattingService>();
                terminalGuiUI.SetCurrencyServices(currencyFormatter, storeSelectionResult);

                ui.Log.AddLog("✅ Orchestrator set successfully");

                // Show store info
                ui.Chat.ShowMessage(new ChatMessage
                {
                    Sender = "System",
                    Content = $"Welcome to {storeSelectionResult.StoreName}!",
                    IsSystem = false,
                    ShowInCleanMode = true
                });

                ui.Log.AddLog($"Integration: {(useRealKernel ? "Real Kernel + Restaurant Extension" : "Mock Tools")} | Currency: {storeSelectionResult.Currency}");
                ui.Log.AddLog($"💰 Payment methods: {string.Join(", ", storeSelectionResult.PaymentMethods.AcceptedMethods.Select(m => m.DisplayName))}");
                ui.Log.AddLog("💡 PAYMENT METHODS ARCHITECTURE: AI validates customer payment methods against store configuration");

                // AI-FIRST DESIGN: No InitializeAsync needed - AI handles greeting on first interaction
                ui.Log.AddLog("AI-Direct Orchestrator ready - AI will greet customer on first input");
                ui.Receipt.UpdateReceipt(aiAgent.Receipt);

                // Now run the UI with orchestrator already set
                await terminalGuiUI.RunAsync();
            }
            catch (Exception ex)
            {
                ui.Log.ShowError($"Failed to initialize orchestrator: {ex.Message}");
                ui.Log.AddLog($"ERROR: {ex.StackTrace}");

                ui.Chat.ShowMessage(new ChatMessage
                {
                    Sender = "System",
                    Content = "❌ System initialization failed. Check debug logs for details.",
                    IsSystem = true,
                    ShowInCleanMode = true
                });

                // Still run the UI so user can see the error
                await ui.RunAsync();
            }

            await ui.ShutdownAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"❌ Fatal error: {ex.Message}");
            if (args.Contains("--debug"))
            {
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            return 1;
        }
    }

    // All the helper methods remain in the demo for UI-specific functionality
    private static void ShowHelp()
    {
        System.Console.WriteLine("POS Kernel AI Demo - Terminal.Gui Interface");
        System.Console.WriteLine();
        System.Console.WriteLine("Usage:");
        System.Console.WriteLine("  PosKernel.AI.Demo                    # Real kernel integration");
        System.Console.WriteLine("  PosKernel.AI.Demo --debug            # Real kernel + debug logging");
        System.Console.WriteLine("  PosKernel.AI.Demo --mock             # Mock integration for development");
        System.Console.WriteLine();
        System.Console.WriteLine("Prerequisites for Real Mode:");
        System.Console.WriteLine("  1. Start the Rust kernel service: cd pos-kernel-rs && cargo run --bin service");
        System.Console.WriteLine("  2. Ensure SQLite database exists: data/catalog/restaurant_catalog.db");
        System.Console.WriteLine("  3. Configure AI provider in ~/.poskernel/.env (STORE_AI_PROVIDER=Ollama or OpenAI)");
        System.Console.WriteLine("  4. If using OpenAI: Set OPENAI_API_KEY in ~/.poskernel/.env");
    }

    // Store selection and configuration methods remain here for demo UI
    private static Task<StoreConfig?> ShowStoreSelectionDialogAsync(IUserInterface ui, bool useRealKernel)
    {
        var availableStores = StoreConfigFactory.GetAvailableStores(useRealKernel: useRealKernel);
        StoreConfig? selectedStore = null;
        var dialogCancelled = false;

        // Create store descriptions
        var storeDescriptions = new string[availableStores.Count];
        for (int i = 0; i < availableStores.Count; i++)
        {
            var store = availableStores[i];
            var personality = CreatePersonalityForStore(store);
            storeDescriptions[i] = $"{store.StoreName} - {personality.StaffTitle} ({store.Currency})";
        }

        // Don't use Application.Invoke - run synchronously in the main thread
        ui.Log.AddLog("Creating store selection dialog...");

        // Create dialog window with proper Terminal.Gui v2 API
        var dialog = new Terminal.Gui.Window()
        {
            Title = $"Store Selection [{(useRealKernel ? "Real Kernel" : "Mock")}]",
            X = Terminal.Gui.Pos.Center(),
            Y = Terminal.Gui.Pos.Center(),
            Width = 60,
            Height = 12 + storeDescriptions.Length,
            Modal = true
        };

        // Add instruction label
        var instructionLabel = new Terminal.Gui.Label()
        {
            Text = "Select a store experience:",
            X = 2,
            Y = 1,
            Width = Terminal.Gui.Dim.Fill(2),
            Height = 1
        };

        // Create radio group with store options
        var radioGroup = new Terminal.Gui.RadioGroup()
        {
            X = 2,
            Y = 3,
            Width = Terminal.Gui.Dim.Fill(2),
            Height = storeDescriptions.Length,
            RadioLabels = storeDescriptions
        };

        // Pre-select Kopitiam
        var kopitiamIndex = availableStores.FindIndex(s => s.StoreType == StoreType.Kopitiam);
        if (kopitiamIndex >= 0)
        {
            radioGroup.SelectedItem = kopitiamIndex;
        }

        // Create OK button
        var okButton = new Terminal.Gui.Button()
        {
            Text = " OK ",
            Width = 10,
            X = Terminal.Gui.Pos.Center() - 5,
            Y = Terminal.Gui.Pos.AnchorEnd(2),
            IsDefault = true
        };

        // Create Cancel button
        var cancelButton = new Terminal.Gui.Button()
        {
            Text = " Cancel ",
            Width = 10,
            X = Terminal.Gui.Pos.Center() + 5,
            Y = Terminal.Gui.Pos.AnchorEnd(2)
        };

        // Handle button events using MouseClick (Terminal.Gui v2 pattern)
        okButton.MouseClick += (_, e) =>
        {
            selectedStore = availableStores[radioGroup.SelectedItem];
            ui.Log.AddLog($"User selected store: {selectedStore.StoreName}");
            Terminal.Gui.Application.RequestStop();
            e.Handled = true;
        };

        cancelButton.MouseClick += (_, e) =>
        {
            dialogCancelled = true;
            ui.Log.AddLog("User cancelled store selection");
            Terminal.Gui.Application.RequestStop();
            e.Handled = true;
        };

        // Handle Enter key on radio group and buttons
        radioGroup.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Terminal.Gui.KeyCode.Enter)
            {
                selectedStore = availableStores[radioGroup.SelectedItem];
                ui.Log.AddLog($"User selected store via Enter: {selectedStore.StoreName}");
                Terminal.Gui.Application.RequestStop();
                e.Handled = true;
            }
        };

        okButton.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Terminal.Gui.KeyCode.Space || e.KeyCode == Terminal.Gui.KeyCode.Enter)
            {
                selectedStore = availableStores[radioGroup.SelectedItem];
                ui.Log.AddLog($"User selected store: {selectedStore.StoreName}");
                Terminal.Gui.Application.RequestStop();
                e.Handled = true;
            }
        };

        cancelButton.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Terminal.Gui.KeyCode.Space || e.KeyCode == Terminal.Gui.KeyCode.Enter)
            {
                dialogCancelled = true;
                ui.Log.AddLog("User cancelled store selection");
                Terminal.Gui.Application.RequestStop();
                e.Handled = true;
            }
        };

        // Add all components to dialog
        dialog.Add(instructionLabel, radioGroup, okButton, cancelButton);

        ui.Log.AddLog("Running store selection dialog...");

        // Use the correct pattern: Run the dialog directly without Application.Invoke
        Terminal.Gui.Application.Run(dialog);

        // Complete the task based on dialog result
        if (dialogCancelled)
        {
            ui.Log.AddLog("Dialog was cancelled - returning null to exit application");
            return Task.FromResult<StoreConfig?>(null);
        }
        else if (selectedStore != null)
        {
            ui.Log.AddLog($"Dialog completed successfully - returning store: {selectedStore.StoreName}");
            ui.Log.AddLog($"💡 STORE CONFIG LOADED: {selectedStore.StoreName} with {selectedStore.Currency} currency");
            ui.Log.AddLog($"🎭 AI PERSONALITY: {GetStaffTitleForPersonality(selectedStore.PersonalityType)} in {GetVenueTitleForStoreType(selectedStore.StoreType)}");
            ui.Log.AddLog($"💳 PAYMENT METHODS will be configured based on store type: {selectedStore.StoreType}");
            return Task.FromResult<StoreConfig?>(selectedStore);
        }
        else
        {
            ui.Log.AddLog("Dialog completed but no store selected - returning null to exit application");
            return Task.FromResult<StoreConfig?>(null);
        }
    }

    private static string GetPaymentInstructionsForStore(StoreConfig storeConfig)
    {
        return storeConfig.StoreType switch
        {
            StoreType.Kopitiam => "Cash preferred, digital payments accepted for orders above S$5",
            StoreType.CoffeeShop => "All major payment methods accepted",
            StoreType.Boulangerie => "Cash and cards accepted, contactless preferred",
            StoreType.ConvenienceStore => "All payment methods accepted for quick service",
            StoreType.ChaiStall => "Cash preferred, digital payments for larger orders",
            _ => "Multiple payment options available"
        };
    }

    private static List<PaymentMethodConfig> GetAcceptedPaymentMethodsForStore(StoreConfig storeConfig)
    {
        var methods = new List<PaymentMethodConfig>();

        // All stores accept cash
        methods.Add(new PaymentMethodConfig
        {
            MethodId = "cash",
            DisplayName = "Cash",
            Type = PaymentMethodType.Cash,
            IsEnabled = true
        });

        // Add region-specific digital payment methods
        switch (storeConfig.StoreType)
        {
            case StoreType.Kopitiam:
                methods.AddRange(new[]
                {
                    new PaymentMethodConfig { MethodId = "nets", DisplayName = "NETS", Type = PaymentMethodType.DebitCard, IsEnabled = true, MinimumAmount = 5.00m },
                    new PaymentMethodConfig { MethodId = "grabpay", DisplayName = "GrabPay", Type = PaymentMethodType.DigitalWallet, IsEnabled = true, MinimumAmount = 2.00m },
                    new PaymentMethodConfig { MethodId = "paynow", DisplayName = "PayNow QR", Type = PaymentMethodType.BankTransfer, IsEnabled = true, MinimumAmount = 1.00m }
                });
                break;

            case StoreType.CoffeeShop:
                methods.AddRange(new[]
                {
                    new PaymentMethodConfig { MethodId = "visa", DisplayName = "Visa", Type = PaymentMethodType.CreditCard, IsEnabled = true },
                    new PaymentMethodConfig { MethodId = "mastercard", DisplayName = "Mastercard", Type = PaymentMethodType.CreditCard, IsEnabled = true },
                    new PaymentMethodConfig { MethodId = "apple_pay", DisplayName = "Apple Pay", Type = PaymentMethodType.DigitalWallet, IsEnabled = true },
                    new PaymentMethodConfig { MethodId = "google_pay", DisplayName = "Google Pay", Type = PaymentMethodType.DigitalWallet, IsEnabled = true }
                });
                break;

            default:
                // Generic payment methods for other store types
                methods.AddRange(new[]
                {
                    new PaymentMethodConfig { MethodId = "card", DisplayName = "Card", Type = PaymentMethodType.CreditCard, IsEnabled = true },
                    new PaymentMethodConfig { MethodId = "contactless", DisplayName = "Contactless", Type = PaymentMethodType.DigitalWallet, IsEnabled = true }
                });
                break;
        }

        return methods;
    }

    private static AiPersonalityConfig CreatePersonalityForStore(StoreConfig storeConfig)
    {
        return new AiPersonalityConfig
        {
            Type = storeConfig.PersonalityType,
            StaffTitle = GetStaffTitleForPersonality(storeConfig.PersonalityType),
            VenueTitle = GetVenueTitleForStoreType(storeConfig.StoreType),
            SupportedLanguages = GetSupportedLanguagesForStore(storeConfig),
            CultureCode = storeConfig.CultureCode,
            Currency = storeConfig.Currency
        };
    }

    private static string GetStaffTitleForPersonality(PersonalityType personalityType)
    {
        return personalityType switch
        {
            PersonalityType.SingaporeanKopitiamUncle => "Uncle",
            PersonalityType.AmericanBarista => "Barista",
            PersonalityType.FrenchBoulanger => "Boulanger",
            PersonalityType.JapaneseConbiniClerk => "Clerk",
            PersonalityType.IndianChaiWala => "Chai Wala",
            PersonalityType.GenericCashier => "Cashier",
            _ => "Staff"
        };
    }

    private static string GetVenueTitleForStoreType(StoreType storeType)
    {
        return storeType switch
        {
            StoreType.Kopitiam => "Kopitiam",
            StoreType.CoffeeShop => "Coffee Shop",
            StoreType.Boulangerie => "Boulangerie",
            StoreType.ConvenienceStore => "Convenience Store",
            StoreType.ChaiStall => "Chai Stall",
            StoreType.GenericStore => "Store",
            _ => "Store"
        };
    }

    private static List<string> GetSupportedLanguagesForStore(StoreConfig storeConfig)
    {
        return storeConfig.StoreType switch
        {
            StoreType.Kopitiam => new List<string> { "en", "zh", "ms", "ta" },
            StoreType.CoffeeShop => new List<string> { "en" },
            StoreType.Boulangerie => new List<string> { "fr", "en" },
            StoreType.ConvenienceStore => new List<string> { "ja", "en" },
            StoreType.ChaiStall => new List<string> { "hi", "en" },
            _ => new List<string> { "en" }
        };
    }
}

/// <summary>
/// Simple file logger provider for AI service dedicated logging.
/// ARCHITECTURAL PRINCIPLE: Dedicated AI service logs that won't get mixed with other applications.
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly object _lock = new object();

    public FileLoggerProvider(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, _filePath, _lock);
    }

    public void Dispose() { }
}

/// <summary>
/// Simple file logger implementation for AI service.
/// ARCHITECTURAL PRINCIPLE: Detailed logging with timestamps for debugging AI initialization and execution.
/// </summary>
public class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly string _filePath;
    private readonly object _lock;

    public FileLogger(string categoryName, string filePath, object lockObject)
    {
        _categoryName = categoryName;
        _filePath = filePath;
        _lock = lockObject;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var logLevelText = logLevel switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO ",
            LogLevel.Warning => "WARN ",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRIT ",
            _ => "UNKN "
        };

        var categoryShort = _categoryName.Split('.').LastOrDefault() ?? _categoryName;
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] {logLevelText}: {categoryShort}[{eventId.Id}] {message}";

        if (exception != null)
        {
            logEntry += Environment.NewLine + exception.ToString();
        }

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_filePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Fail silently for logging issues
            }
        }
    }
}
