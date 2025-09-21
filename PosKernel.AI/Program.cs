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
using PosKernel.Configuration;
using PosKernel.Configuration.Services;
using PosKernel.AI.Models;
using PosKernel.Extensions.Restaurant.Client;
using PosKernel.AI.Common.Abstractions;
using PosKernel.AI.Common.Factories;

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
                var useMockKernel = args.Contains("--mock") || args.Contains("-m");

                if (args.Contains("--help") || args.Contains("-h"))
                {
                    ShowHelp();
                    return 0;
                }

                // Initialize configuration system
                var config = PosKernelConfiguration.Initialize();
                var storeAiConfig = new AiConfiguration(config);

                // ARCHITECTURAL PRINCIPLE: Use shared factory - no hardcoded provider assumptions
                // Validate store AI configuration at startup (fail fast)
                var provider = storeAiConfig.Provider;
                var model = storeAiConfig.Model;
                var baseUrl = storeAiConfig.BaseUrl;
                var apiKey = storeAiConfig.ApiKey;
                
                if (string.IsNullOrWhiteSpace(provider))
                {
                    Console.WriteLine("❌ Store AI provider not configured!");
                    Console.WriteLine("Set STORE_AI_PROVIDER in ~/.poskernel/.env (e.g., STORE_AI_PROVIDER=Ollama)");
                    Console.WriteLine($"Config directory: {PosKernelConfiguration.ConfigDirectory}");
                    return 1;
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
                services.AddSingleton<IStoreConfigurationService, SimpleStoreConfigurationService>();

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
                        
                        // Register ChatOrchestrator for real kernel mode
                        services.AddSingleton<ChatOrchestrator>(provider => new ChatOrchestrator(
                            provider.GetRequiredService<McpClient>(),
                            provider.GetRequiredService<AiPersonalityConfig>(),
                            provider.GetRequiredService<StoreConfig>(),
                            provider.GetRequiredService<ILogger<ChatOrchestrator>>(),
                            kernelToolsProvider: provider.GetRequiredService<KernelPosToolsProvider>(),
                            mockToolsProvider: null,
                            currencyFormatter: provider.GetRequiredService<ICurrencyFormattingService>()));
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
                            $"To use development mode instead: PosKernel.AI --mock";
                        
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
                    // Mock mode explicitly requested
                    ui.Log.AddLog("Setting up mock tools integration (development mode)...");
                    services.AddSingleton<PosToolsProvider>(provider =>
                        new PosToolsProvider(
                            new KopitiamProductCatalogService(),
                            provider.GetRequiredService<ILogger<PosToolsProvider>>(),
                            storeSelectionResult));
                    
                    // Register ChatOrchestrator for mock mode
                    services.AddSingleton<ChatOrchestrator>(provider => new ChatOrchestrator(
                        provider.GetRequiredService<McpClient>(),
                        provider.GetRequiredService<AiPersonalityConfig>(),
                        provider.GetRequiredService<StoreConfig>(),
                        provider.GetRequiredService<ILogger<ChatOrchestrator>>(),
                        kernelToolsProvider: null,
                        mockToolsProvider: provider.GetRequiredService<PosToolsProvider>(),
                        currencyFormatter: provider.GetRequiredService<ICurrencyFormattingService>()));
                }

                // Add store configuration and orchestrator to services
                services.AddSingleton(storeSelectionResult);
                services.AddSingleton(personality);

                // Rebuild service provider with all services including store config and tools
                using var updatedServiceProvider = services.BuildServiceProvider();

                try
                {
                    // ARCHITECTURAL FIX: Get orchestrator from DI container to ensure all dependencies are injected
                    var orchestrator = updatedServiceProvider.GetRequiredService<ChatOrchestrator>();
                    ui.Log.AddLog($"✅ {(useRealKernel ? "Real kernel" : "Mock")} orchestrator created successfully");

                    // Set orchestrator - UI now handles the case where it's not available gracefully
                    var terminalGuiUI = (TerminalUserInterface)ui;
                    terminalGuiUI.SetOrchestrator(orchestrator);

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

                    // Initialize and get AI greeting
                    var greetingMessage = await orchestrator.InitializeAsync();
                    ui.Chat.ShowMessage(greetingMessage);
                    ui.Receipt.UpdateReceipt(orchestrator.CurrentReceipt);

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

        private static void ShowHelp()
        {
            System.Console.WriteLine("POS Kernel AI Integration");
            System.Console.WriteLine();
            System.Console.WriteLine("Usage:");
            System.Console.WriteLine("  PosKernel.AI                    # Real kernel integration");
            System.Console.WriteLine("  PosKernel.AI --debug            # Real kernel + debug logging");
            System.Console.WriteLine("  PosKernel.AI --mock             # Mock integration for development");
            System.Console.WriteLine();
            System.Console.WriteLine("Prerequisites for Real Mode:");
            System.Console.WriteLine("  1. Start the Rust kernel service: cd pos-kernel-rs && cargo run --bin service");
            System.Console.WriteLine("  2. Ensure SQLite database exists: data/catalog/restaurant_catalog.db");
            System.Console.WriteLine("  3. Configure AI provider in ~/.poskernel/.env (STORE_AI_PROVIDER=Ollama or OpenAI)");
            System.Console.WriteLine("  4. If using OpenAI: Set OPENAI_API_KEY in ~/.poskernel/.env");
        }

        private static Task<StoreConfig?> ShowStoreSelectionDialogAsync(IUserInterface ui, bool useRealKernel)
        {
            var availableStores = StoreConfigFactory.GetAvailableStores(useRealKernel: useRealKernel);
            var tcs = new TaskCompletionSource<StoreConfig?>();
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
            var dialog = new Window()
            {
                Title = $"Store Selection [{(useRealKernel ? "Real Kernel" : "Mock")}]",
                X = Pos.Center(),
                Y = Pos.Center(),
                Width = 60,
                Height = 12 + storeDescriptions.Length,
                Modal = true
            };

            // Add instruction label
            var instructionLabel = new Label()
            {
                Text = "Select a store experience:",
                X = 2,
                Y = 1,
                Width = Dim.Fill(2),
                Height = 1
            };

            // Create radio group with store options
            var radioGroup = new RadioGroup()
            {
                X = 2,
                Y = 3,
                Width = Dim.Fill(2),
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
            var okButton = new Button()
            {
                Text = " OK ",
                Width = 10,
                X = Pos.Center() - 5,
                Y = Pos.AnchorEnd(2),
                IsDefault = true
            };

            // Create Cancel button  
            var cancelButton = new Button()
            {
                Text = " Cancel ",
                Width = 10,
                X = Pos.Center() + 5,
                Y = Pos.AnchorEnd(2)
            };

            // Handle button events using MouseClick (Terminal.Gui v2 pattern)
            okButton.MouseClick += (_, e) =>
            {
                selectedStore = availableStores[radioGroup.SelectedItem];
                ui.Log.AddLog($"User selected store: {selectedStore.StoreName}");
                Application.RequestStop();
                e.Handled = true;
            };

            cancelButton.MouseClick += (_, e) =>
            {
                dialogCancelled = true;
                ui.Log.AddLog("User cancelled store selection");
                Application.RequestStop();
                e.Handled = true;
            };

            // Handle Enter key on radio group and buttons
            radioGroup.KeyDown += (_, e) =>
            {
                if (e.KeyCode == KeyCode.Enter)
                {
                    selectedStore = availableStores[radioGroup.SelectedItem];
                    ui.Log.AddLog($"User selected store via Enter: {selectedStore.StoreName}");
                    Application.RequestStop();
                    e.Handled = true;
                }
            };

            okButton.KeyDown += (_, e) =>
            {
                if (e.KeyCode == KeyCode.Space || e.KeyCode == KeyCode.Enter)
                {
                    selectedStore = availableStores[radioGroup.SelectedItem];
                    ui.Log.AddLog($"User selected store: {selectedStore.StoreName}");
                    Application.RequestStop();
                    e.Handled = true;
                }
            };

            cancelButton.KeyDown += (_, e) =>
            {
                if (e.KeyCode == KeyCode.Space || e.KeyCode == KeyCode.Enter)
                {
                    dialogCancelled = true;
                    ui.Log.AddLog("User cancelled store selection");
                    Application.RequestStop();
                    e.Handled = true;
                }
            };

            // Add all components to dialog
            dialog.Add(instructionLabel, radioGroup, okButton, cancelButton);

            ui.Log.AddLog("Running store selection dialog...");
            
            // Use the correct pattern: Run the dialog directly without Application.Invoke
            Application.Run(dialog);

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
    /// Kopitiam-specific product catalog service that properly implements IProductCatalogService
    /// </summary>
    public class KopitiamProductCatalogService : IProductCatalogService
    {
        private readonly List<IProductInfo> _products = new()
        {
            new KopitiamProductInfo { Sku = "KOPI001", Name = "Kopi", Category = "Beverages", BasePrice = 1.20m },
            new KopitiamProductInfo { Sku = "KOPI002", Name = "Kopi C", Category = "Beverages", BasePrice = 1.40m },
            new KopitiamProductInfo { Sku = "KOPI003", Name = "Kopi O", Category = "Beverages", BasePrice = 1.00m },
            new KopitiamProductInfo { Sku = "TEH001", Name = "Teh", Category = "Beverages", BasePrice = 1.20m },
            new KopitiamProductInfo { Sku = "TEH002", Name = "Teh C", Category = "Beverages", BasePrice = 1.40m },
            new KopitiamProductInfo { Sku = "TEH003", Name = "Teh O", Category = "Beverages", BasePrice = 1.00m },
            new KopitiamProductInfo { Sku = "FOOD001", Name = "Kaya Toast", Category = "Food", BasePrice = 2.50m },
            new KopitiamProductInfo { Sku = "FOOD002", Name = "Half Boiled Eggs", Category = "Food", BasePrice = 1.80m },
            new KopitiamProductInfo { Sku = "FOOD003", Name = "Mee Goreng", Category = "Food", BasePrice = 4.50m }
        };

        public async Task<ProductValidationResult> ValidateProductAsync(string productId, ProductLookupContext context, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            var product = _products.FirstOrDefault(p => 
                p.Name.Equals(productId, StringComparison.OrdinalIgnoreCase) ||
                p.Sku.Equals(productId, StringComparison.OrdinalIgnoreCase));

            if (product != null)
            {
                return new ProductValidationResult
                {
                    IsValid = true,
                    Product = product,
                    EffectivePrice = product.BasePrice
                };
            }

            return new ProductValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Product '{productId}' not found"
            };
        }

        public async Task<IReadOnlyList<IProductInfo>> SearchProductsAsync(string searchTerm, int maxResults, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            return _products
                .Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                           p.Category.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .Take(maxResults)
                .ToList();
        }

        public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            return _products.Select(p => p.Category).Distinct().ToList();
        }

        public async Task<IReadOnlyList<IProductInfo>> GetPopularItemsAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            return _products.Take(5).ToList();
        }

        public async Task<IReadOnlyList<IProductInfo>> GetProductsByCategoryAsync(string category, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            return _products.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public async Task<string> GetLocalizedProductNameAsync(string productId, string localeCode, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            var product = _products.FirstOrDefault(p => p.Sku == productId);
            return product?.Name ?? productId;
        }

        public async Task<string> GetLocalizedProductDescriptionAsync(string productId, string localeCode, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            var product = _products.FirstOrDefault(p => p.Sku == productId);
            return product?.Description ?? "";
        }
    }

    public class KopitiamProductInfo : IProductInfo
    {
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public decimal BasePrice { get; set; }
        public bool IsActive { get; set; } = true;
        public bool RequiresPreparation { get; set; } = false;
        public int PreparationTimeMinutes { get; set; } = 2;
        public string? NameLocalizationKey { get; set; }
        public string? DescriptionLocalizationKey { get; set; }
        public IReadOnlyDictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();
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

    /// <summary>
    /// Simple implementation of IStoreConfigurationService for demo purposes.
    /// In a real system, this would load store configuration from database or configuration files.
    /// </summary>
    internal class SimpleStoreConfigurationService : IStoreConfigurationService
    {
        public StoreConfiguration GetStoreConfiguration(string storeId)
        {
            // For now, return a generic store configuration
            // In a real system, this would be looked up from a database or configuration
            return new StoreConfiguration
            {
                StoreId = storeId,
                Currency = "USD", // Default - will be overridden by StoreConfig
                Locale = "en-US"  // Default - will be overridden by StoreConfig  
            };
        }
    }
}
