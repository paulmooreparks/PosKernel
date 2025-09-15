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
using PosKernel.Abstractions;
using PosKernel.Abstractions.Services;
using PosKernel.AI.Services;
using PosKernel.AI.Tools;
using PosKernel.Client;
using PosKernel.Configuration;
using PosKernel.Extensions.Restaurant.Client;

namespace PosKernel.AI
{
    /// <summary>
    /// Main program for POS Kernel AI integration demonstrations.
    /// Features a generalized store system with multiple personalities and real kernel integration.
    /// </summary>
    class Program
    {
        private enum DemoType
        {
            Invalid,
            StoreSelector,
            Help
        }

        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("🤖 POS Kernel AI Integration Demo v0.7.0 (Generalized Stores)");
            Console.WriteLine("🚀 Real Kernel Architecture with Multiple Store Personalities");
            Console.WriteLine("🔐 Security-First Design with Kernel Isolation");
            Console.WriteLine();

            try
            {
                var demoType = ParseDemoType(args);
                
                switch (demoType)
                {
                    case DemoType.StoreSelector:
                        await RunStoreSelectionDemoAsync();
                        break;

                    case DemoType.Help:
                        ShowHelp();
                        break;

                    default:
                        Console.WriteLine("🏪 GENERALIZED STORE AI DEMO");
                        Console.WriteLine("============================");
                        Console.WriteLine("Choose from multiple store types and personalities!");
                        Console.WriteLine();
                        await RunStoreSelectionDemoAsync();
                        break;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ Fatal error: {ex.Message}");
                Console.Error.WriteLine($"📍 Stack trace: {ex.StackTrace}");
                return 1;
            }
        }

        private static DemoType ParseDemoType(string[] args)
        {
            if (args.Length == 0)
            {
                return DemoType.StoreSelector; // Default to store selector
            }

            var arg = args[0].ToLowerInvariant();
            return arg switch
            {
                "--store" or "--select" or "-s" => DemoType.StoreSelector,
                "--help" or "-h" => DemoType.Help,
                _ => DemoType.StoreSelector // Default
            };
        }

        private static async Task RunStoreSelectionDemoAsync()
        {
            Console.WriteLine("🏪 INTERACTIVE STORE SELECTION DEMO");
            Console.WriteLine("===================================");
            Console.WriteLine("Experience different store personalities and cultures!");
            Console.WriteLine();

            var config = PosKernelConfiguration.Initialize();
            var aiConfig = new AiConfiguration(config);
            
            if (string.IsNullOrEmpty(aiConfig.ApiKey))
            {
                Console.WriteLine("❌ OpenAI API key not configured!");
                Console.WriteLine("Please set up your API key in the configuration.");
                return;
            }

            Console.WriteLine($"✅ API key loaded (ends with: ...{aiConfig.ApiKey.Substring(Math.Max(0, aiConfig.ApiKey.Length - 4))})");
            Console.WriteLine();

            // Present store options
            var availableStores = StoreConfigFactory.GetAvailableStores(useRealKernel: false);
            
            Console.WriteLine("🌍 AVAILABLE STORE EXPERIENCES:");
            Console.WriteLine("================================");
            
            for (int i = 0; i < availableStores.Count; i++)
            {
                var store = availableStores[i];
                var personality = AiPersonalityFactory.CreatePersonality(store.PersonalityType);
                
                Console.WriteLine($"{i + 1}. {store.StoreName} ({store.Currency})");
                Console.WriteLine($"   Type: {store.StoreType}");
                Console.WriteLine($"   Staff: {personality.StaffTitle}");
                Console.WriteLine($"   Culture: {store.CultureCode}");
                Console.WriteLine($"   Description: {store.Description}");
                
                if (personality.SupportedLanguages.Count > 1)
                {
                    Console.WriteLine($"   Languages: {string.Join(", ", personality.SupportedLanguages)}");
                }
                
                Console.WriteLine();
            }

            Console.WriteLine("🔌 KERNEL INTEGRATION OPTIONS:");
            Console.WriteLine("R. Use REAL KERNEL integration (requires services running)");
            Console.WriteLine("M. Use MOCK integration (self-contained demo)");
            Console.WriteLine();

            // Get user selection
            Console.Write($"Select a store experience (1-{availableStores.Count}) or kernel type (R/M): ");
            var input = Console.ReadLine()?.Trim().ToUpperInvariant();
            
            if (string.IsNullOrEmpty(input))
            {
                Console.WriteLine("❌ No selection made. Exiting.");
                return;
            }

            bool useRealKernel = false;
            StoreConfig? selectedStore = null;

            // Handle kernel selection first
            if (input == "R")
            {
                useRealKernel = true;
                Console.WriteLine("🚀 Real kernel integration selected!");
                Console.Write($"Now select a store experience (1-{availableStores.Count}): ");
                input = Console.ReadLine()?.Trim();
            }
            else if (input == "M")
            {
                useRealKernel = false;
                Console.WriteLine("🎭 Mock integration selected!");
                Console.Write($"Now select a store experience (1-{availableStores.Count}): ");
                input = Console.ReadLine()?.Trim();
            }

            // Parse store selection
            if (int.TryParse(input, out var storeIndex) && storeIndex >= 1 && storeIndex <= availableStores.Count)
            {
                selectedStore = StoreConfigFactory.CreateStore(availableStores[storeIndex - 1].StoreType, useRealKernel);
            }
            else
            {
                Console.WriteLine("❌ Invalid selection. Exiting.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"🎯 Starting {selectedStore.StoreName} experience...");
            Console.WriteLine($"   Integration: {(useRealKernel ? "REAL KERNEL" : "MOCK")}");
            Console.WriteLine($"   Personality: {selectedStore.PersonalityType}");
            Console.WriteLine();

            // Run the selected store experience
            if (useRealKernel)
            {
                await RunRealKernelStoreExperienceAsync(selectedStore, aiConfig);
            }
            else
            {
                await RunMockStoreExperienceAsync(selectedStore, aiConfig);
            }
        }

        private static async Task RunRealKernelStoreExperienceAsync(StoreConfig storeConfig, AiConfiguration aiConfig)
        {
            Console.WriteLine("🔌 Connecting to REAL KERNEL services...");
            
            // Configure services for real kernel integration
            var services = new ServiceCollection();
            services.AddLogging(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Information));
            
            // Register real kernel client and restaurant extension
            services.AddSingleton<PosKernelClientOptions>();
            services.AddSingleton<IPosKernelClient, PosKernelClient>();
            services.AddSingleton<RestaurantExtensionClient>();
            services.AddSingleton<KernelPosToolsProvider>();
            
            // Register MCP services
            var mcpConfig = aiConfig.ToMcpConfiguration();
            services.AddSingleton(mcpConfig);
            services.AddSingleton<McpClient>();
            
            using var serviceProvider = services.BuildServiceProvider();
            
            try 
            {
                var kernelClient = serviceProvider.GetRequiredService<IPosKernelClient>();
                var restaurantClient = serviceProvider.GetRequiredService<RestaurantExtensionClient>();
                var kernelToolsProvider = serviceProvider.GetRequiredService<KernelPosToolsProvider>();
                var mcpClient = serviceProvider.GetRequiredService<McpClient>();

                // Test connectivity
                Console.WriteLine("📡 Testing kernel connectivity...");
                await kernelClient.ConnectAsync();
                Console.WriteLine("✅ Connected to POS Kernel");
                
                Console.WriteLine("🍽️ Testing restaurant extension connectivity...");
                var testProducts = await restaurantClient.SearchProductsAsync("test", 1);
                Console.WriteLine($"✅ Connected to Restaurant Extension ({testProducts.Count} test products found)");

                // Create personality and run experience
                var personality = AiPersonalityFactory.CreatePersonality(storeConfig.PersonalityType);
                Console.WriteLine($"🎭 {storeConfig.StoreName} with REAL KERNEL is ready!");
                Console.WriteLine();

                await RunGeneralizedMcpChatSessionAsync(mcpClient, kernelToolsProvider, personality, storeConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Real kernel integration failed: {ex.Message}");
                Console.WriteLine("🔍 Troubleshooting:");
                Console.WriteLine("1. Ensure PosKernel.Service is running");
                Console.WriteLine("2. Ensure Restaurant Extension is running");  
                Console.WriteLine("3. Check network connectivity and named pipes");
                Console.WriteLine();
                Console.WriteLine("💡 Try running with Mock integration instead (M option)");
            }
        }

        private static async Task RunMockStoreExperienceAsync(StoreConfig storeConfig, AiConfiguration aiConfig)
        {
            Console.WriteLine("🎭 Starting MOCK store experience...");
            
            // Configure services for mock integration
            var services = new ServiceCollection();
            services.AddLogging(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Warning)); // Keep it clean for demo
            
            // Register appropriate mock services based on store type
            switch (storeConfig.CatalogProvider)
            {
                case CatalogProviderType.Mock:
                    services.AddSingleton<IProductCatalogService, KopitiamProductCatalogService>();
                    break;
                case CatalogProviderType.InMemory:
                    services.AddSingleton<IProductCatalogService, RestaurantProductCatalogService>();
                    break;
                default:
                    services.AddSingleton<IProductCatalogService, RestaurantProductCatalogService>();
                    break;
            }
            
            services.AddSingleton<ProductCustomizationService>();
            services.AddSingleton<PosToolsProvider>(); // Use mock tools provider
            
            // Register MCP services
            var mcpConfig = aiConfig.ToMcpConfiguration();
            services.AddSingleton(mcpConfig);
            services.AddSingleton<McpClient>();
            
            using var serviceProvider = services.BuildServiceProvider();
            
            try 
            {
                var productCatalog = serviceProvider.GetRequiredService<IProductCatalogService>();
                var mcpClient = serviceProvider.GetRequiredService<McpClient>();
                var toolsProvider = serviceProvider.GetRequiredService<PosToolsProvider>();

                var personality = AiPersonalityFactory.CreatePersonality(storeConfig.PersonalityType);
                Console.WriteLine($"✅ {storeConfig.StoreName} mock experience ready!");
                Console.WriteLine();

                await RunMockChatSessionAsync(mcpClient, toolsProvider, personality, storeConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Mock store experience failed: {ex.Message}");
                Console.WriteLine("Please check your configuration and try again.");
            }
        }

        private static async Task RunGeneralizedMcpChatSessionAsync(McpClient mcpClient, KernelPosToolsProvider kernelToolsProvider, AiPersonalityConfig personality, StoreConfig storeConfig)
        {
            Console.WriteLine($"🏪 **WELCOME TO {storeConfig.StoreName.ToUpper()}!**");
            Console.WriteLine($"Your {personality.StaffTitle} is connected to the REAL POS KERNEL!");
            Console.WriteLine("================================================");
            Console.WriteLine($"Store Type: {storeConfig.StoreType}");
            Console.WriteLine($"Currency: {storeConfig.Currency}");
            Console.WriteLine($"Culture: {storeConfig.CultureCode}");
            
            if (personality.SupportedLanguages.Count > 1)
            {
                Console.WriteLine($"Languages: {string.Join(", ", personality.SupportedLanguages)}");
            }
            Console.WriteLine();

            var availableTools = kernelToolsProvider.GetAvailableTools();
            Console.WriteLine($"🛠️  MCP tools available: {availableTools.Count}");
            Console.WriteLine();

            var conversationHistory = new List<string>();
            
            // Generate contextual AI greeting
            var currentTime = DateTime.Now;
            var timeOfDay = currentTime.Hour < 12 ? "morning" : currentTime.Hour < 17 ? "afternoon" : "evening";
            
            var greetingPrompt = CreatePersonalizedGreetingPrompt(personality, timeOfDay, currentTime);

            // AI greeting with real kernel context loading
            try
            {
                Console.Write($"🤖 {personality.StaffTitle} (REAL KERNEL): ");
                
                // Load menu context from real restaurant extension
                var menuContextCall = new McpToolCall
                {
                    Id = "kernel-startup-" + Guid.NewGuid().ToString()[..8],
                    FunctionName = "load_menu_context",
                    Arguments = new Dictionary<string, System.Text.Json.JsonElement>
                    {
                        ["include_categories"] = System.Text.Json.JsonSerializer.SerializeToElement(true)
                    }
                };
                
                Console.WriteLine("Loading real menu from restaurant extension...");
                var menuContext = await kernelToolsProvider.ExecuteToolAsync(menuContextCall);
                
                // Create enhanced greeting prompt with real menu context
                var enhancedGreetingPrompt = greetingPrompt + $@"
REAL MENU KNOWLEDGE (loaded from restaurant extension):
{menuContext}

Store Context:
- Store: {storeConfig.StoreName}
- Type: {storeConfig.StoreType}  
- Currency: {storeConfig.Currency}

Now greet the customer - you're connected to the REAL kernel!";
                
                var greetingResponse = await mcpClient.CompleteWithToolsAsync(enhancedGreetingPrompt, availableTools);
                Console.WriteLine(greetingResponse.Content);
                Console.WriteLine();
                conversationHistory.Add($"{personality.StaffTitle}: {greetingResponse.Content}");
            }
            catch (Exception ex)
            {
                var fallbackGreeting = GetFallbackGreeting(personality, timeOfDay);
                Console.WriteLine(fallbackGreeting);
                Console.WriteLine($"(Real kernel greeting failed: {ex.Message})");
                Console.WriteLine();
                conversationHistory.Add($"{personality.StaffTitle}: {fallbackGreeting}");
            }

            // Main conversation loop
            while (true)
            {
                Console.Write("You: ");
                
                var userInput = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(userInput))
                {
                    continue;
                }

                // Handle exit commands
                if (IsExitCommand(userInput))
                {
                    Console.WriteLine($"🤖 {personality.StaffTitle}: Thank you for visiting {storeConfig.StoreName}! Come back anytime!");
                    break;
                }

                conversationHistory.Add($"Customer: {userInput}");
                var conversationContext = string.Join("\n", conversationHistory.TakeLast(8));

                // Create generalized MCP prompt
                var prompt = CreateGeneralizedMcpPrompt(personality, storeConfig, conversationContext, userInput);

                try
                {
                    Console.Write($"🤖 {personality.StaffTitle} (REAL KERNEL): ");
                    var response = await mcpClient.CompleteWithToolsAsync(prompt, availableTools);
                    
                    if (!string.IsNullOrEmpty(response.Content))
                    {
                        Console.WriteLine(response.Content);
                    }

                    // Execute REAL KERNEL MCP tool calls
                    if (response.ToolCalls.Any())
                    {
                        Console.WriteLine($"  🔧 Executing {response.ToolCalls.Count} REAL KERNEL operation(s)...");
                        
                        var toolResults = new List<string>();
                        foreach (var toolCall in response.ToolCalls)
                        {
                            var result = await kernelToolsProvider.ExecuteToolAsync(toolCall);
                            Console.WriteLine($"  📊 {toolCall.FunctionName}: {result}");
                            toolResults.Add(result);
                        }

                        // Generate follow-up response
                        var followUpPrompt = CreateGeneralizedFollowUpPrompt(personality, storeConfig, conversationContext, userInput, response.Content, toolResults);
                        
                        try 
                        {
                            var followUpResponse = await mcpClient.CompleteWithToolsAsync(followUpPrompt, availableTools);
                            if (!string.IsNullOrEmpty(followUpResponse.Content))
                            {
                                Console.WriteLine($"🤖 {followUpResponse.Content}");
                            }
                            
                            conversationHistory.Add($"{personality.StaffTitle}: {response.Content} [Real Kernel Operations] {followUpResponse.Content}");
                        }
                        catch
                        {
                            var fallback = GetPersonalityFallback(personality, storeConfig);
                            Console.WriteLine($"🤖 {fallback}");
                            conversationHistory.Add($"{personality.StaffTitle}: {response.Content} [Real Kernel Operations] {fallback}");
                        }
                    }
                    else
                    {
                        conversationHistory.Add($"{personality.StaffTitle}: {response.Content}");
                    }

                    // Handle completion
                    if (IsCompletionRequest(userInput))
                    {
                        Console.WriteLine($"🎉 Thank you! Your transaction has been processed through the REAL KERNEL at {storeConfig.StoreName}!");
                        Console.WriteLine("✨ PHASE 3: Generalized Real Kernel Integration Complete!");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    var errorResponse = GetPersonalityErrorResponse(personality);
                    Console.WriteLine($"{errorResponse}");
                    Console.WriteLine($"(Real kernel error: {ex.Message})");
                }

                Console.WriteLine();
            }
        }

        private static async Task RunMockChatSessionAsync(McpClient mcpClient, PosToolsProvider toolsProvider, AiPersonalityConfig personality, StoreConfig storeConfig)
        {
            Console.WriteLine($"🏪 **WELCOME TO {storeConfig.StoreName.ToUpper()}!**");
            Console.WriteLine($"Your {personality.StaffTitle} is ready to serve you using Model Context Protocol!");
            Console.WriteLine("================================================");
            Console.WriteLine($"Store Type: {storeConfig.StoreType}");
            Console.WriteLine($"Currency: {storeConfig.Currency}");
            Console.WriteLine($"Culture: {storeConfig.CultureCode}");
            
            if (personality.SupportedLanguages.Count > 1)
            {
                Console.WriteLine($"Languages: {string.Join(", ", personality.SupportedLanguages)}");
            }
            Console.WriteLine();

            // Create mock transaction
            var transaction = new Transaction
            {
                Id = TransactionId.New(),
                State = TransactionState.Building,
                Currency = storeConfig.Currency
            };

            var availableTools = toolsProvider.GetAvailableTools();
            Console.WriteLine($"🛠️  MCP tools available: {availableTools.Count}");
            Console.WriteLine($"🏪 Transaction started: {transaction.Id}");
            Console.WriteLine();

            var conversationHistory = new List<string>();
            
            // Generate contextual AI greeting
            var currentTime = DateTime.Now;
            var timeOfDay = currentTime.Hour < 12 ? "morning" : currentTime.Hour < 17 ? "afternoon" : "evening";
            
            var greetingPrompt = CreatePersonalizedGreetingPrompt(personality, timeOfDay, currentTime);

            // AI greeting
            try
            {
                Console.Write($"🤖 {personality.StaffTitle} (MOCK): ");
                var greetingResponse = await mcpClient.CompleteWithToolsAsync(greetingPrompt, availableTools);
                Console.WriteLine(greetingResponse.Content);
                Console.WriteLine();
                conversationHistory.Add($"{personality.StaffTitle}: {greetingResponse.Content}");
            }
            catch
            {
                var fallbackGreeting = GetFallbackGreeting(personality, timeOfDay);
                Console.WriteLine(fallbackGreeting);
                Console.WriteLine();
                conversationHistory.Add($"{personality.StaffTitle}: {fallbackGreeting}");
            }

            // Main conversation loop
            while (true)
            {
                Console.WriteLine($"💰 Current order: {GetOrderSummary(transaction)}");
                Console.Write("You: ");
                
                var userInput = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(userInput))
                {
                    continue;
                }

                // Handle exit commands
                if (IsExitCommand(userInput))
                {
                    Console.WriteLine($"🤖 {personality.StaffTitle}: Thank you for visiting {storeConfig.StoreName}! Have a great day!");
                    break;
                }

                conversationHistory.Add($"Customer: {userInput}");
                var conversationContext = string.Join("\n", conversationHistory.TakeLast(8));

                // Create generalized MCP prompt for mock
                var prompt = CreateMockMcpPrompt(personality, storeConfig, conversationContext, transaction, userInput);

                try
                {
                    Console.Write($"🤖 {personality.StaffTitle} (MOCK): ");
                    var response = await mcpClient.CompleteWithToolsAsync(prompt, availableTools);
                    
                    if (!string.IsNullOrEmpty(response.Content))
                    {
                        Console.WriteLine(response.Content);
                    }

                    // Execute MOCK MCP tool calls
                    if (response.ToolCalls.Any())
                    {
                        Console.WriteLine($"  🛠️  Executing {response.ToolCalls.Count} tool(s)...");
                        
                        var toolResults = new List<string>();
                        foreach (var toolCall in response.ToolCalls)
                        {
                            var result = await toolsProvider.ExecuteToolAsync(toolCall, transaction);
                            Console.WriteLine($"  📊 {toolCall.FunctionName}: {result}");
                            toolResults.Add(result);
                        }

                        // Generate follow-up response
                        var followUpPrompt = CreateMockFollowUpPrompt(personality, storeConfig, conversationContext, userInput, response.Content, toolResults);
                        
                        try 
                        {
                            var followUpResponse = await mcpClient.CompleteWithToolsAsync(followUpPrompt, availableTools);
                            if (!string.IsNullOrEmpty(followUpResponse.Content))
                            {
                                Console.WriteLine($"🤖 {followUpResponse.Content}");
                            }
                            
                            conversationHistory.Add($"{personality.StaffTitle}: {response.Content} [Executed tools] {followUpResponse.Content}");
                        }
                        catch
                        {
                            var fallback = GetPersonalityFallback(personality, storeConfig);
                            Console.WriteLine($"🤖 {fallback}");
                            conversationHistory.Add($"{personality.StaffTitle}: {response.Content} [Executed tools] {fallback}");
                        }
                    }
                    else
                    {
                        conversationHistory.Add($"{personality.StaffTitle}: {response.Content}");
                    }

                    // Handle completion
                    if (IsCompletionRequest(userInput))
                    {
                        if (transaction.Lines.Any())
                        {
                            Console.WriteLine($"🎉 Thank you! Your order at {storeConfig.StoreName} is complete!");
                            Console.WriteLine($"📋 Final total: {transaction.Lines.Count} items, ${transaction.Total.ToDecimal():F2} {storeConfig.Currency}");
                        }
                        break;
                    }
                }
                catch (Exception ex)
                {
                    var errorResponse = GetPersonalityErrorResponse(personality);
                    Console.WriteLine($"{errorResponse}");
                    Console.WriteLine($"(Error: {ex.Message})");
                }

                Console.WriteLine();
            }
        }

        // Helper methods
        private static string CreatePersonalizedGreetingPrompt(AiPersonalityConfig personality, string timeOfDay, DateTime currentTime)
        {
            return personality.GreetingTemplate
                .Replace("{timeOfDay}", timeOfDay)
                .Replace("{currentTime}", currentTime.ToString("HH:mm"));
        }

        private static string GetFallbackGreeting(AiPersonalityConfig personality, string timeOfDay)
        {
            return personality.CommonPhrases.TryGetValue($"greeting_{timeOfDay}", out var greeting) 
                ? greeting 
                : $"Welcome to our {personality.VenueTitle}! How can I help you?";
        }

        private static string CreateGeneralizedMcpPrompt(AiPersonalityConfig personality, StoreConfig storeConfig, string conversationContext, string userInput)
        {
            var template = personality.OrderingTemplate
                .Replace("{conversationContext}", conversationContext)
                .Replace("{cartItems}", "checking with real kernel...")
                .Replace("{currentTotal}", "checking with real kernel...")
                .Replace("{currency}", storeConfig.Currency)
                .Replace("{userInput}", userInput);

            return template + $@"
STORE CONTEXT:
- Store: {storeConfig.StoreName}
- Type: {storeConfig.StoreType}
- Currency: {storeConfig.Currency}
- Culture: {storeConfig.CultureCode}

You are connected to the REAL POS KERNEL! Use MCP tools to help customers efficiently.";
        }

        private static string CreateMockMcpPrompt(AiPersonalityConfig personality, StoreConfig storeConfig, string conversationContext, Transaction transaction, string userInput)
        {
            var cartItems = transaction.Lines.Any() 
                ? string.Join(", ", transaction.Lines.Select(l => $"{l.ProductId} x{l.Quantity}"))
                : "none";

            var template = personality.OrderingTemplate
                .Replace("{conversationContext}", conversationContext)
                .Replace("{cartItems}", cartItems)
                .Replace("{currentTotal}", transaction.Total.ToDecimal().ToString("F2"))
                .Replace("{currency}", storeConfig.Currency)
                .Replace("{userInput}", userInput);

            return template + $@"
STORE CONTEXT:
- Store: {storeConfig.StoreName}
- Type: {storeConfig.StoreType}
- Currency: {storeConfig.Currency}
- Culture: {storeConfig.CultureCode}

Use MCP tools to help customers with their orders!";
        }

        private static string CreateGeneralizedFollowUpPrompt(AiPersonalityConfig personality, StoreConfig storeConfig, string conversationContext, string userInput, string initialResponse, List<string> toolResults)
        {
            var toolSummary = string.Join("; ", toolResults);
            
            return $@"You are a {personality.StaffTitle} at {storeConfig.StoreName} connected to the REAL POS KERNEL. 

The real kernel operations returned: {toolSummary}

Customer said: '{userInput}'

REAL KERNEL FOLLOW-UP (adapt to your personality):

If kernel returned DISAMBIGUATION_NEEDED:
- Present options clearly in your cultural style

If kernel returned ADDED (real transaction updated):  
- Acknowledge in your personality style
- Confirm what was added from kernel result
- Ask what else they need

If kernel returned payment processed:
- Confirm completion in your style
- Thank them appropriately for your culture

If kernel returned error:
- Handle gracefully in your personality

Store: {storeConfig.StoreName} | Currency: {storeConfig.Currency}
Maintain your {personality.StaffTitle} personality while handling real kernel results!";
        }

        private static string CreateMockFollowUpPrompt(AiPersonalityConfig personality, StoreConfig storeConfig, string conversationContext, string userInput, string initialResponse, List<string> toolResults)
        {
            var toolSummary = string.Join("; ", toolResults);
            
            return $@"You are a {personality.StaffTitle} at {storeConfig.StoreName}. 

The system operations returned: {toolSummary}

Customer said: '{userInput}'

FOLLOW-UP (adapt to your personality):

If system returned DISAMBIGUATION_NEEDED:
- Present options clearly in your cultural style

If system returned ADDED:  
- Acknowledge in your personality style
- Ask what else they need

If system returned payment processed:
- Confirm completion in your style

Maintain your {personality.StaffTitle} personality!";
        }

        private static string GetPersonalityFallback(AiPersonalityConfig personality, StoreConfig storeConfig)
        {
            return personality.Type switch
            {
                PersonalityType.SingaporeanKopitiamUncle => "Can! What else?",
                PersonalityType.FrenchBoulanger => "Parfait! What else can I get for you?",
                PersonalityType.JapaneseConbiniClerk => "Arigatou gozaimasu! How else can I help?",
                PersonalityType.IndianChaiWala => "Bas! Aur kya chahiye?",
                PersonalityType.AmericanBarista => "Perfect! What else can I get you?",
                _ => "Great! What else can I help you with?"
            };
        }

        private static string GetPersonalityErrorResponse(AiPersonalityConfig personality)
        {
            return personality.Type switch
            {
                PersonalityType.SingaporeanKopitiamUncle => "Sorry, system got problem. Can try again?",
                PersonalityType.FrenchBoulanger => "Pardonnez-moi, there's a technical issue. Please try again?",
                PersonalityType.JapaneseConbiniClerk => "Sumimasen, there's a system issue. Could you please try again?",
                PersonalityType.IndianChaiWala => "Arre yaar, system mein problem hai. Try again?",
                PersonalityType.AmericanBarista => "Sorry about that, I'm having a technical issue. Could you try that again?",
                _ => "Sorry, I'm having a system issue. Please try again."
            };
        }

        private static bool IsExitCommand(string input)
        {
            var exitCommands = new[] { "exit", "quit", "bye", "goodbye", "done", "thanks", "thank you" };
            return exitCommands.Contains(input.ToLowerInvariant());
        }

        private static bool IsCompletionRequest(string input)
        {
            var completionCommands = new[] { "that's all", "that's it", "complete", "finish", "pay", "checkout", "done", "habis", "sudah", "selesai" };
            return completionCommands.Any(cmd => input.ToLowerInvariant().Contains(cmd));
        }

        private static string GetOrderSummary(Transaction transaction)
        {
            if (!transaction.Lines.Any())
            {
                return "Empty cart";
            }

            var items = transaction.Lines.Select(l => $"{l.ProductId.ToString().Replace("PRODUCT_", "")} x{l.Quantity}");
            return $"{string.Join(", ", items)} (${transaction.Total.ToDecimal():F2})";
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Usage: PosKernel.AI [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --store, --select, -s      🏪 Interactive Store Selection Demo");
            Console.WriteLine("  --help, -h                 Show this help message");
            Console.WriteLine();
            Console.WriteLine("🌍 Available Store Experiences:");
            Console.WriteLine("• American Coffee Shop - Friendly barista experience");
            Console.WriteLine("• Singaporean Kopitiam - Authentic uncle with cultural intelligence");
            Console.WriteLine("• French Boulangerie - Artisanal baker with French flair");
            Console.WriteLine("• Japanese Convenience Store - Polite and efficient service");
            Console.WriteLine("• Indian Chai Stall - Warm chai wala experience");
            Console.WriteLine("• Generic Store - Professional cashier service");
            Console.WriteLine();
            Console.WriteLine("🔌 Integration Options:");
            Console.WriteLine("• REAL KERNEL - Connect to actual POS Kernel services");
            Console.WriteLine("• MOCK - Self-contained demonstration");
        }
    }
}
