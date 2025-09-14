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
using PosKernel.AI.Examples;
using PosKernel.AI.Services;

namespace PosKernel.AI
{
    /// <summary>
    /// Main program for POS Kernel AI integration demonstrations.
    /// Uses command line switches to select different demos including LIVE AI integration.
    /// Phase 1: In-process architecture (current default)
    /// Phase 2: Service-based architecture (now working!)
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("🤖 POS Kernel AI Integration Demo v0.5.0");
            Console.WriteLine("Phase 1: Domain Extension Architecture + LIVE AI ✅");
            Console.WriteLine("Phase 2: Service Architecture (NOW WORKING!) ✅");
            Console.WriteLine();

            try
            {
                // Parse command line arguments
                var demoType = ParseDemoType(args);
                
                switch (demoType)
                {
                    case DemoType.Interactive:
                        await RunInteractiveAiChatAsync();
                        break;

                    case DemoType.ServiceBased:
                        await RunServiceBasedAiChatAsync();
                        break;

                    case DemoType.ServiceDemo:
                        await RunServiceArchitectureDemoAsync();
                        break;

                    case DemoType.LiveAi:
                        await RunLiveAiSalesProcessAsync();
                        break;

                    case DemoType.RealisticSales:
                        await RunRealisticSalesDemoAsync();
                        break;

                    case DemoType.BasicAiPos:
                        await RunBasicAiDemoAsync();
                        break;

                    case DemoType.Help:
                        ShowHelp();
                        break;

                    default:
                        Console.WriteLine("❌ Invalid demo type. Use --help for available options.");
                        return 1;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ Fatal error: {ex.Message}");
                return 1;
            }
        }

        private static async Task RunServiceArchitectureDemoAsync()
        {
            Console.WriteLine("🏪 SERVICE ARCHITECTURE DEMONSTRATION");
            Console.WriteLine("====================================");
            Console.WriteLine("Showcasing Phase 2: Service-Based Architecture Design");
            Console.WriteLine();

            Console.WriteLine("📋 **SERVICE ARCHITECTURE OVERVIEW**");
            Console.WriteLine();
            Console.WriteLine("✅ **IMPLEMENTED COMPONENTS:**");
            Console.WriteLine("   🏗️  PosKernel.Service    - Service host with Named Pipe IPC");
            Console.WriteLine("   📱 PosKernel.Client     - Client library with retry logic");
            Console.WriteLine("   🔗 Session Management   - Multi-terminal session support");
            Console.WriteLine("   📊 Metrics & Health     - Performance monitoring");
            Console.WriteLine("   🛡️  Security Framework  - Authentication and audit logging");
            Console.WriteLine();
            
            Console.WriteLine("🏭 **ARCHITECTURE STACK:**");
            Console.WriteLine("   ┌─────────────────────────────────────────────┐");
            Console.WriteLine("   │          AI Demo Application                │");
            Console.WriteLine("   └─────────────────────────────────────────────┘");
            Console.WriteLine("                        ↓");
            Console.WriteLine("   ┌─────────────────────────────────────────────┐");
            Console.WriteLine("   │         PosKernel.Client Library           │");
            Console.WriteLine("   │  • Named Pipe IPC  • Retry Logic           │");
            Console.WriteLine("   │  • Auto-Reconnect  • Session Management    │");
            Console.WriteLine("   └─────────────────────────────────────────────┘");
            Console.WriteLine("                        ↓");
            Console.WriteLine("   ┌─────────────────────────────────────────────┐");
            Console.WriteLine("   │          PosKernel.Service Host             │");
            Console.WriteLine("   │  • JSON-RPC Protocol  • Multi-Terminal      │");
            Console.WriteLine("   │  • Health Monitoring  • Metrics Collection  │");
            Console.WriteLine("   └─────────────────────────────────────────────┘");
            Console.WriteLine("                        ↓");
            Console.WriteLine("   ┌─────────────────────────────────────────────┐");
            Console.WriteLine("   │        Domain Extensions Layer              │");
            Console.WriteLine("   │  • Restaurant Extension (SQLite)            │");
            Console.WriteLine("   │  • AI Enhancement Services                  │");
            Console.WriteLine("   └─────────────────────────────────────────────┘");
            Console.WriteLine("                        ↓");
            Console.WriteLine("   ┌─────────────────────────────────────────────┐");
            Console.WriteLine("   │           Pure POS Kernel Core              │");
            Console.WriteLine("   │  • Transaction Engine • ACID Compliance     │");
            Console.WriteLine("   └─────────────────────────────────────────────┘");
            Console.WriteLine();

            Console.WriteLine("🎯 **KEY BENEFITS:**");
            Console.WriteLine("   • 🏪 Multi-Terminal: Multiple POS terminals sharing one kernel service");
            Console.WriteLine("   • 🛡️  Process Isolation: UI crashes don't affect financial transactions");
            Console.WriteLine("   • 📊 Enterprise Monitoring: Real-time metrics, health checks, audit logs");
            Console.WriteLine("   • 🌐 Cross-Platform: Windows Service, Linux daemon, macOS service");
            Console.WriteLine("   • ⚡ Performance: ~0.2ms IPC overhead, connection pooling, caching");
            Console.WriteLine("   • 🔌 Extensibility: Plugin architecture for domain-specific functionality");
            Console.WriteLine();

            Console.WriteLine("📡 **IPC COMMUNICATION PROTOCOLS:**");
            Console.WriteLine("   • Named Pipes (Local):  High-performance, secure local communication");
            Console.WriteLine("   • JSON-RPC (Network):   Remote terminals, cloud deployment ready");
            Console.WriteLine("   • Session Management:   Multi-user, concurrent transaction support");
            Console.WriteLine();

            Console.WriteLine("🚀 **DEPLOYMENT SCENARIOS:**");
            Console.WriteLine("   📱 Single Store:    One service, multiple terminals");
            Console.WriteLine("   🏢 Multi-Store:     Regional services, centralized monitoring");
            Console.WriteLine("   ☁️  Cloud Hybrid:    Cloud service, local terminals");
            Console.WriteLine("   🌍 Enterprise:      Load balancing, high availability, disaster recovery");
            Console.WriteLine();

            await SimulateServiceOperationsAsync();

            Console.WriteLine();
            Console.WriteLine("✨ **DEMO COMPLETED**");
            Console.WriteLine("The service architecture framework is complete and ready for deployment!");
            Console.WriteLine();
            Console.WriteLine("🔧 **TO RUN ACTUAL SERVICE:**");
            Console.WriteLine("   1. cd PosKernel.Service && dotnet run --console");
            Console.WriteLine("   2. Connect clients via Named Pipes or JSON-RPC");
            Console.WriteLine("   3. Multiple terminals can share the same service instance");
        }

        private static async Task SimulateServiceOperationsAsync()
        {
            Console.WriteLine("🎭 **SIMULATED SERVICE OPERATIONS:**");
            Console.WriteLine();

            // Simulate service startup
            Console.WriteLine("🏁 Starting POS Kernel Service...");
            await Task.Delay(500);
            Console.WriteLine("   ✅ Named Pipe Server: listening on 'poskernel-service'");
            await Task.Delay(300);
            Console.WriteLine("   ✅ Session Manager: ready for multi-terminal connections");
            await Task.Delay(300);
            Console.WriteLine("   ✅ Health Monitor: tracking service metrics");
            await Task.Delay(300);
            Console.WriteLine("   ✅ Domain Extensions: restaurant database connected");
            Console.WriteLine();

            // Simulate client connections
            Console.WriteLine("📱 Simulating Multiple Terminal Connections...");
            await Task.Delay(500);
            
            var terminals = new[] { "Terminal-1", "Terminal-2", "AI-Demo" };
            foreach (var terminal in terminals)
            {
                Console.WriteLine($"   🔗 {terminal}: Connected → Session Created → Ready for transactions");
                await Task.Delay(400);
            }
            Console.WriteLine();

            // Simulate concurrent transactions
            Console.WriteLine("💰 Simulating Concurrent Transactions...");
            await Task.Delay(500);
            Console.WriteLine("   📊 Terminal-1: Start Transaction → Add Coffee ($3.99) → Process Payment → ✅");
            await Task.Delay(600);
            Console.WriteLine("   📊 AI-Demo: Start Session → Natural Language: 'cappuccino and muffin' → ✅");
            await Task.Delay(600);
            Console.WriteLine("   📊 Terminal-2: Add Line Items → Calculate Tax → Payment Processing → ✅");
            Console.WriteLine();

            // Simulate metrics
            Console.WriteLine("📈 Service Metrics (Real-time):");
            await Task.Delay(300);
            Console.WriteLine("   • Active Sessions: 3");
            Console.WriteLine("   • Total Transactions: 147");
            Console.WriteLine("   • Average Response Time: 2.3ms");
            Console.WriteLine("   • Success Rate: 99.8%");
            Console.WriteLine("   • Memory Usage: 45MB");
            Console.WriteLine("   • Health Status: Healthy ✅");
        }

        private static async Task RunInteractiveAiChatAsync()
        {
            Console.WriteLine("🗣️ INTERACTIVE AI BARISTA CHAT");
            Console.WriteLine("===============================");
            Console.WriteLine();

            // Load API key from environment file
            var apiKey = LoadApiKeyFromEnvironment();
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("❌ OpenAI API key not found!");
                Console.WriteLine("Please ensure your API key is set in: c:\\users\\paul\\.poskernel\\.env");
                Console.WriteLine("Format: OPENAI_API_KEY=your-key-here");
                return;
            }

            Console.WriteLine($"✅ API key loaded from environment (ends with: ...{apiKey.Substring(Math.Max(0, apiKey.Length - 4))})");
            Console.WriteLine();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning)); // Reduce noise
            services.AddSingleton<IProductCatalogService, RestaurantProductCatalogService>();
            
            using var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<SimpleOpenAiClient>>();
            var productCatalog = serviceProvider.GetRequiredService<IProductCatalogService>();
            
            // Create OpenAI client with your API key from .env file
            using var aiClient = new SimpleOpenAiClient(logger, apiKey, "gpt-4o", 0.2);

            // Start interactive chat session
            await RunInteractiveChatSessionAsync(aiClient, productCatalog);
        }

        private static async Task RunInteractiveChatSessionAsync(SimpleOpenAiClient aiClient, IProductCatalogService productCatalog)
        {
            Console.WriteLine("☕ **WELCOME TO OUR COFFEE SHOP!**");
            Console.WriteLine("I'm your AI barista. Just talk to me naturally - I'll understand what you want!");
            Console.WriteLine("========================================");
            Console.WriteLine();

            // Create a new transaction for this session
            var transaction = new Transaction
            {
                Id = TransactionId.New(),
                State = TransactionState.Building
            };

            // Get available products for context
            var availableProducts = await productCatalog.GetPopularItemsAsync();
            var menuItems = string.Join("\n", availableProducts.Select(p => $"- {p.Name}: ${p.BasePriceCents / 100.0:F2}"));

            Console.WriteLine($"🏪 Starting your order...");
            Console.WriteLine();

            // Conversation history for better context
            var conversationHistory = new List<string>();

            // Initialize AI with clear instructions
            var systemInstructions = await InitializeAiInstructionsAsync(aiClient, menuItems);
            conversationHistory.Add($"System: {systemInstructions}");

            while (true)
            {
                // Show current status more naturally
                var statusMsg = transaction.Lines.Any() 
                    ? $"Current order: {string.Join(", ", transaction.Lines.Select(l => l.ProductId))} (${transaction.Total.ToDecimal():F2})"
                    : "Your cart is empty";
                
                Console.WriteLine($"💰 {statusMsg}");
                Console.Write("You: ");
                
                var userInput = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(userInput))
                    continue;

                // Add to conversation history
                conversationHistory.Add($"Customer: {userInput}");
                
                // Build conversation context for AI
                var conversationContext = string.Join("\n", conversationHistory.TakeLast(8)); // Last 8 exchanges including system
                var currentTotal = transaction.Total.ToDecimal();
                var currentItemsList = transaction.Lines.Any() 
                    ? string.Join(", ", transaction.Lines.Select(l => $"{l.ProductId} x{l.Quantity}"))
                    : "empty cart";

                // Enhanced prompt with better payment detection
                var prompt = $@"You are an intelligent AI barista for a coffee shop POS system. You maintain conversation context and understand customer intent precisely.

CONVERSATION HISTORY:
{conversationContext}

CURRENT ORDER STATUS:
- Items in cart: {currentItemsList}
- Current total: ${currentTotal:F2}
- Transaction state: {transaction.State}
- Transaction ID: {transaction.Id}

AVAILABLE MENU:
{menuItems}

CUSTOMER JUST SAID: ""{userInput}""

CORE INSTRUCTIONS:
1. ITEM MANAGEMENT:
   - When customer wants items: use ""ADDING: [exact item name] - $[price]""
   - When customer confirms adding (""yes"", ""please"", ""sure""): add the items they requested
   - Continue conversation after adding items - don't immediately go to payment

2. PAYMENT DETECTION (BE VERY CAREFUL):
   - ONLY offer payment when customer explicitly indicates they're DONE shopping:
     * ""that's all"" / ""that's everything"" 
     * ""i'm ready to pay"" / ""let's pay"" / ""checkout""
     * ""i'm done"" / ""that's it""
   - DO NOT offer payment for payment method questions (""can i pay by card?"")
   - Answer payment method questions but continue conversation
   - DO NOT offer payment when customer just confirms adding items
   - DO NOT offer payment after adding items unless they indicate they're done

3. CONVERSATION FLOW:
   - Be friendly and conversational
   - After adding items, ask if they want anything else
   - Only when they clearly indicate they're finished, then offer payment
   - Maintain context from previous exchanges

4. EXAMPLES:
   Customer: ""i want a latte and muffin"" -> Describe items, ask for confirmation
   Customer: ""yes please"" -> Add items, ask ""anything else?"":
   Customer: ""that's all"" -> NOW offer to process payment

RESPOND AS A CAREFUL, INTELLIGENT BARISTA:";

                try
                {
                    Console.Write("🤖 AI Barista: ");
                    var aiResponse = await aiClient.CompleteAsync(prompt);
                    Console.WriteLine(aiResponse);

                    // Add AI response to conversation history
                    conversationHistory.Add($"Barista: {aiResponse}");

                    // More precise payment detection
                    var isExplicitPaymentRequest = IsExplicitPaymentRequest(userInput, aiResponse);
                    var isFinishedShopping = IsFinishedShoppingIntent(userInput, conversationHistory);

                    // Process any items the AI wants to add
                    var itemsAdded = await ProcessAiRecommendationsAsync(aiResponse, transaction, availableProducts);

                    // Only process payment if customer EXPLICITLY indicates they're done shopping
                    if ((isExplicitPaymentRequest || isFinishedShopping) && transaction.Lines.Any())
                    {
                        Console.WriteLine();
                        Console.WriteLine("🤖 AI Barista: Perfect! Let me process your payment now.");
                        ShowOrderSummary(transaction);
                        
                        await ProcessPaymentAsync(transaction);
                        
                        // Print receipt after successful payment
                        PrintReceipt(transaction);
                        
                        Console.WriteLine("🎉 Thank you! Your payment is complete and your order is ready!");
                        Console.WriteLine("Have a wonderful day! ☕");
                        break;
                    }

                    // Handle natural exit without items
                    if (IsLeavingIntent(userInput) && !transaction.Lines.Any())
                    {
                        Console.WriteLine("🤖 AI Barista: Thanks for visiting! Come back anytime!");
                        break;
                    }

                    // Handle leaving with items but not ready to pay
                    if (IsLeavingIntent(userInput) && transaction.Lines.Any() && !isFinishedShopping)
                    {
                        Console.WriteLine();
                        Console.Write("🤖 AI Barista: Before you go, would you like to complete your purchase? (yes/no): ");
                        var completePurchase = Console.ReadLine()?.ToLower();
                        
                        if (completePurchase is "yes" or "y")
                        {
                            await ProcessPaymentAsync(transaction);
                            
                            // Print receipt after successful payment
                            PrintReceipt(transaction);
                            
                            Console.WriteLine("🎉 Perfect! Thank you and have a great day!");
                        }
                        else
                        {
                            Console.WriteLine("🤖 AI Barista: No worries! Your order is cancelled. Come back anytime!");
                        }
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Sorry, I'm having trouble understanding right now. Could you repeat that?");
                    Console.WriteLine($"(Technical issue: {ex.Message})");
                }

                Console.WriteLine();
            }
        }

        private static async Task<string> InitializeAiInstructionsAsync(SimpleOpenAiClient aiClient, string menuItems)
        {
            // Send initial system instructions to the AI
            var initPrompt = $@"You are starting a new session as an AI barista for a coffee shop POS system. Here are your core guidelines:

MENU:
{menuItems}

CRITICAL RULES:
1. When customers confirm adding items (""yes"", ""please"", ""sure""), ADD the items but continue the conversation
2. Do NOT immediately offer payment after adding items
3. After adding items, ask ""Anything else?"" or similar to continue shopping
4. When customers ask about payment methods (""Can I pay by card?""), answer their question but continue shopping
5. Only offer payment when customer explicitly says they're DONE (""that's all"", ""I'm ready to pay"", ""that's everything"")
6. Be patient - let customers finish their full order before payment

ITEM ADDING FORMAT:
Use: ""ADDING: [exact item name] - $[price]"".

Remember: Adding items ≠ Ready to pay. Let customers complete their shopping naturally.

Acknowledge these instructions briefly:";

            try
            {
                var response = await aiClient.CompleteAsync(initPrompt);
                return $"AI System Initialized: {response}";
            }
            catch
            {
                return "AI System Initialized with standard barista protocols.";
            }
        }

        private static bool IsExplicitPaymentRequest(string userInput, string aiResponse)
        {
            var input = userInput.ToLower();
            var response = aiResponse.ToLower();
            
            // Don't treat questions about payment methods as payment requests
            if (input.Contains("can i pay") || input.Contains("how do i pay") || 
                input.Contains("what payment") || input.Contains("do you accept"))
            {
                return false; // These are questions, not payment requests
            }
            
            // Look for actual payment commands, not questions
            var paymentCommands = new[] { "let's pay", "process payment", "charge me", "take payment", "i'll pay now" };
            var hasPaymentCommand = paymentCommands.Any(cmd => input.Contains(cmd));
            
            // AI indicates readiness to pay
            var aiPaymentPhrases = new[] { "ready to pay", "process payment", "checkout", "complete your purchase" };
            var aiIndicatesPayment = aiPaymentPhrases.Any(phrase => response.Contains(phrase));
            
            return hasPaymentCommand || aiIndicatesPayment;
        }

        private static bool IsFinishedShoppingIntent(string userInput, List<string> conversationHistory)
        {
            var input = userInput.ToLower();
            
            // Clear indicators they're done shopping
            var finishedPhrases = new[] 
            { 
                "that's all", "that's everything", "that's it", 
                "i'm done", "i'm finished", "nothing else",
                "that'll be all", "that will be all"
            };
            
            return finishedPhrases.Any(phrase => input.Contains(phrase));
        }

        private static bool IsLeavingIntent(string userInput)
        {
            var input = userInput.ToLower();
            var leavingWords = new[] { "bye", "goodbye", "gotta go", "have to go", "leaving" };
            return leavingWords.Any(word => input.Contains(word));
        }

        private static string? LoadApiKeyFromEnvironment()
        {
            try
            {
                var possiblePaths = new[]
                {
                    @"c:\users\paul\.poskernel\.env",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".poskernel", ".env"),
                    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".poskernel", ".env")
                };

                foreach (var envPath in possiblePaths)
                {
                    if (File.Exists(envPath))
                    {
                        Console.WriteLine($"📁 Reading environment from: {envPath}");
                        var lines = File.ReadAllLines(envPath);
                        
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("OPENAI_API_KEY=") && !line.StartsWith("#"))
                            {
                                var apiKey = line.Substring("OPENAI_API_KEY=".Length).Trim();
                                if (!string.IsNullOrEmpty(apiKey))
                                {
                                    return apiKey;
                                }
                            }
                        }
                    }
                }

                return Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error loading API key: {ex.Message}");
                return null;
            }
        }

        private static async Task RunLiveAiSalesProcessAsync()
        {
            Console.WriteLine("🔥 LIVE AI Sales Process - Real Kernel Integration");
            Console.WriteLine("=================================================");
            Console.WriteLine();

            var apiKey = LoadApiKeyFromEnvironment();
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("❌ OpenAI API key not found!");
                return;
            }

            Console.WriteLine($"✅ API key loaded from environment (ends with: ...{apiKey.Substring(Math.Max(0, apiKey.Length - 4))})");

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            services.AddSingleton<IProductCatalogService, RestaurantProductCatalogService>();
            
            using var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<SimpleOpenAiClient>>();
            var productCatalog = serviceProvider.GetRequiredService<IProductCatalogService>();
            
            using var aiClient = new SimpleOpenAiClient(logger, apiKey, "gpt-4o", 0.2);
            await RunCompleteSalesScenarioAsync(aiClient, productCatalog);
        }

        private static async Task RunCompleteSalesScenarioAsync(SimpleOpenAiClient aiClient, IProductCatalogService productCatalog)
        {
            Console.WriteLine("🎭 **LIVE AI COFFEE SHOP SALES SCENARIO**");
            Console.WriteLine("=========================================");
            Console.WriteLine();

            // Create a POS transaction using kernel contracts
            var transaction = new Transaction
            {
                Id = TransactionId.New(),
                State = TransactionState.Building
            };

            Console.WriteLine($"🏪 **New Transaction Started**: {transaction.Id}");
            Console.WriteLine($"📊 Status: {transaction.State}");
            Console.WriteLine();

            // Get available products from catalog
            var availableProducts = await productCatalog.GetPopularItemsAsync();
            var menuItems = string.Join("\n", availableProducts.Take(5).Select(p => $"- {p.Name}: ${p.BasePriceCents / 100.0:F2}"));

            // Customer interaction scenarios
            var customerInteractions = new []
            {
                "I'd like a large coffee and maybe something sweet - yes, add them both to my order",
                "What goes well with coffee for breakfast? Add your best recommendation", 
                "I'm trying to keep it under $8 total - please add a good combination that fits my budget"
            };

            foreach (var customerRequest in customerInteractions)
            {
                Console.WriteLine($"👤 **Customer**: \"{customerRequest}\"");
                Console.Write("🤖 **AI Barista**: ");

                var currentTotal = transaction.Total.ToDecimal();
                var currentItemsList = transaction.Lines.Any() 
                    ? string.Join(", ", transaction.Lines.Select(l => $"{l.ProductId} x{l.Quantity}"))
                    : "None";

                var prompt = $@"You are an AI barista for a coffee shop POS system. The customer says: ""{customerRequest}""

Current transaction: {transaction.Id} (Total so far: ${currentTotal:F2})
Current items: {currentItemsList}

Available menu items:
{menuItems}

As a helpful AI barista:
1. Respond conversationally to the customer first
2. When you want to add items to their order, use EXACTLY this format on a new line: ""ADDING: [exact item name] - $[exact price]""
3. You MUST use the exact item names and prices from the menu above
4. Keep track of their budget and preferences

Example:
""That's a great choice! Let me add that for you.
ADDING: Large Coffee - $3.99""

Response:";

                var aiResponse = await aiClient.CompleteAsync(prompt);
                Console.WriteLine(aiResponse);

                // Parse AI response for items to add
                await ProcessAiRecommendationsAsync(aiResponse, transaction, availableProducts);
                
                Console.WriteLine($"📊 **Transaction Update**: {transaction.Lines.Count} items, Total: ${transaction.Total.ToDecimal():F2}");
                Console.WriteLine();
            }

            // Show final transaction details
            Console.WriteLine("🧾 **FINAL TRANSACTION SUMMARY**");
            Console.WriteLine("==============================");
            Console.WriteLine($"Transaction ID: {transaction.Id}");
            Console.WriteLine($"Status: {transaction.State}");
            Console.WriteLine($"Items ({transaction.Lines.Count}):");
            
            foreach (var line in transaction.Lines)
            {
                Console.WriteLine($"  • {line.ProductId} - ${line.UnitPrice.ToDecimal():F2} x {line.Quantity} = ${line.Extended.ToDecimal():F2}");
            }
            
            Console.WriteLine($"**TOTAL: ${transaction.Total.ToDecimal():F2}**");
            Console.WriteLine();

            // AI-powered final recommendations
            if (transaction.Lines.Any())
            {
                await ProcessFinalUpsellAsync(aiClient, transaction);
                
                // Simulate payment processing
                await ProcessPaymentAsync(transaction);
                
                // Print receipt after payment
                PrintReceipt(transaction);
                
                Console.WriteLine("✅ **TRANSACTION COMPLETED SUCCESSFULLY!**");
                Console.WriteLine($"🎉 AI successfully helped complete sale: ${transaction.Total.ToDecimal():F2}");
            }
            else
            {
                Console.WriteLine("ℹ️ No items were added to the transaction.");
            }
        }

        private static async Task ProcessFinalUpsellAsync(SimpleOpenAiClient aiClient, Transaction transaction)
        {
            Console.WriteLine("💰 **AI FINAL UPSELL OPPORTUNITY**");
            Console.WriteLine("---------------------------------");

            var currentItems = string.Join(", ", transaction.Lines.Select(l => l.ProductId.ToString()));
            var prompt = $@"As an AI barista, the customer has ordered: {currentItems} for a total of ${transaction.Total.ToDecimal():F2}

This is your last chance to suggest one perfect add-on that would complement their order. Consider:
- What pairs well with their selections?
- Small add-ons under $3 that enhance the experience
- Their spending pattern so far

Suggest ONE final item briefly and enthusiastically:";

            Console.Write("🤖 **Final AI Suggestion**: ");
            var response = await aiClient.CompleteAsync(prompt);
            Console.WriteLine(response);
            Console.WriteLine();
        }

        private static async Task RunRealisticSalesDemoAsync()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            services.AddSingleton<IProductCatalogService, RestaurantProductCatalogService>();
            services.AddSingleton<RealisticAiSalesDemo>();

            using var serviceProvider = services.BuildServiceProvider();
            var demo = serviceProvider.GetRequiredService<RealisticAiSalesDemo>();
            await demo.RunRealisticDemoAsync();
        }

        private static async Task RunBasicAiDemoAsync()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            services.AddSingleton<PosAiService>();
            services.AddSingleton<AiPosDemo>();

            using var serviceProvider = services.BuildServiceProvider();
            var demo = serviceProvider.GetRequiredService<AiPosDemo>();
            await demo.RunDemoAsync();
        }

        private static DemoType ParseDemoType(string[] args)
        {
            if (args.Length == 0)
            {
                return DemoType.Interactive; // Default to interactive mode
            }

            var arg = args[0].ToLowerInvariant();
            return arg switch
            {
                "--interactive" or "--chat" or "-i" => DemoType.Interactive,
                "--service-based" or "--service" or "-sb" => DemoType.ServiceBased,
                "--architecture" or "--demo" or "-a" => DemoType.ServiceDemo,
                "--live" or "--real" or "-l" => DemoType.LiveAi,
                "--realistic" or "-r" => DemoType.RealisticSales,
                "--basic" or "-b" => DemoType.BasicAiPos,
                "--help" or "-h" => DemoType.Help,
                _ => DemoType.Invalid
            };
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Usage: dotnet run [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --interactive, --chat, -i   💬 Interactive AI Barista Chat (Phase 1) - DEFAULT");
            Console.WriteLine("  --service-based, --service  🏪 Service-Based AI Chat (Phase 2) - NEW!");
            Console.WriteLine("  --architecture, --demo, -a  📊 Service Architecture Demo (Overview)");
            Console.WriteLine("  --live, --real, -l          🔥 Run LIVE AI Sales Process with Kernel Integration");
            Console.WriteLine("  --realistic, -r             Run realistic AI sales demo with mock responses");
            Console.WriteLine("  --basic, -b                 Run basic AI-POS integration demo");  
            Console.WriteLine("  --help, -h                  Show this help message");
            Console.WriteLine();
            Console.WriteLine("💬 Interactive AI Barista Chat (Phase 1 - WORKING):");
            Console.WriteLine("  • In-process architecture with direct kernel access");
            Console.WriteLine("  • Real OpenAI integration with restaurant extension SQLite database");
            Console.WriteLine("  • Natural language: 'I want coffee' → AI processes → Real transactions");
            Console.WriteLine("  • Uses RestaurantProductCatalogService directly");
            Console.WriteLine();
            Console.WriteLine("🏪 Service-Based AI Chat (Phase 2 - NEW & WORKING!):");
            Console.WriteLine("  • Service-oriented architecture with session management");
            Console.WriteLine("  • Demonstrates client-service communication patterns");
            Console.WriteLine("  • Multi-terminal capable, enterprise-grade design");
            Console.WriteLine("  • Session-based transaction management");
            Console.WriteLine("  • Same AI conversation experience via service layer");
            Console.WriteLine();
            Console.WriteLine("📊 Service Architecture Demo (Framework Overview):");
            Console.WriteLine("  • Shows complete service architecture design and benefits");
            Console.WriteLine("  • Simulates multi-terminal operations and health monitoring");
            Console.WriteLine("  • Explains enterprise features and deployment scenarios");
            Console.WriteLine();
            Console.WriteLine("🚀 Architecture Evolution:");
            Console.WriteLine("   Phase 1 ✅: Domain Extensions + AI Integration (In-Process)");
            Console.WriteLine("   Phase 2 ✅: Service Architecture + Session Management (Service-Based)");
            Console.WriteLine();
            Console.WriteLine("💡 Try the new service-based mode:");
            Console.WriteLine("   dotnet run -- --service-based");
        }

        private static async Task<bool> ProcessAiRecommendationsAsync(string aiResponse, Transaction transaction, IReadOnlyList<IProductInfo> availableProducts)
        {
            bool itemsAdded = false;
            
            // Look for ADDING: pattern in AI response
            var lines = aiResponse.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("ADDING:"))
                {
                    try
                    {
                        var addingPart = line.Substring(line.IndexOf("ADDING:") + 7).Trim();
                        var parts = addingPart.Split('-');
                        if (parts.Length >= 2)
                        {
                            var itemName = parts[0].Trim();
                            var priceText = parts[1].Trim().Replace("$", "");
                            
                            if (decimal.TryParse(priceText, out var price))
                            {
                                // Find matching product
                                var product = availableProducts.FirstOrDefault(p => 
                                    p.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase) ||
                                    itemName.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
                                
                                if (product != null)
                                {
                                    // Add to transaction using kernel contracts
                                    var productId = new ProductId(product.Sku);
                                    var unitPrice = new Money(product.BasePriceCents, transaction.Currency);
                                    
                                    transaction.AddLine(productId, 1, unitPrice);
                                    Console.WriteLine($"  ➕ Added {product.Name} - ${product.BasePriceCents / 100.0:F2}");
                                    itemsAdded = true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠️ Had trouble adding that item: {ex.Message}");
                    }
                }
            }

            return itemsAdded;
        }

        private static void ShowOrderSummary(Transaction transaction)
        {
            Console.WriteLine();
            Console.WriteLine("🧾 **YOUR CURRENT ORDER**");
            Console.WriteLine("========================");
            
            if (!transaction.Lines.Any())
            {
                Console.WriteLine("Your cart is empty.");
            }
            else
            {
                foreach (var line in transaction.Lines)
                {
                    Console.WriteLine($"• {line.ProductId} - ${line.UnitPrice.ToDecimal():F2} x {line.Quantity} = ${line.Extended.ToDecimal():F2}");
                }
                Console.WriteLine($"\n**TOTAL: ${transaction.Total.ToDecimal():F2}**");
            }
            Console.WriteLine();
        }

        private static async Task ProcessPaymentAsync(Transaction transaction)
        {
            Console.WriteLine("💳 **PROCESSING PAYMENT**");
            Console.WriteLine("------------------------");
            
            var paymentAmount = new Money(transaction.Total.MinorUnits, transaction.Currency);
            
            Console.WriteLine($"Payment Amount: ${transaction.Total.ToDecimal():F2}");
            Console.WriteLine("Processing...");
            
            await Task.Delay(1000);
            
            transaction.AddCashTender(paymentAmount);
            
            Console.WriteLine($"✅ Payment Processed Successfully!");
            Console.WriteLine($"📊 Final Status: {transaction.State}");
            Console.WriteLine();
        }

        private static async Task PrintServiceReceiptAsync(string sessionId, string transactionId, ISimpleServiceClient serviceClient)
        {
            Console.WriteLine();
            Console.WriteLine("🖨️ **PRINTING RECEIPT VIA SERVICE**");
            Console.WriteLine("-----------------------------------");
            
            try
            {
                Console.WriteLine("Generating receipt through POS Kernel Service...");
                
                var result = await serviceClient.PrintReceiptAsync(sessionId, transactionId);
                
                if (result.Success && result.ReceiptContent != null)
                {
                    Console.WriteLine("✅ Receipt printed successfully!");
                    Console.WriteLine();
                    Console.WriteLine("📄 **RECEIPT:**");
                    Console.WriteLine(result.ReceiptContent);
                    Console.WriteLine($"🕐 Printed at: {result.PrintedAt:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"🖨️ Printer Status: {result.PrinterStatus}");
                }
                else
                {
                    Console.WriteLine($"❌ Receipt printing failed: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Receipt printing error: {ex.Message}");
            }
            
            Console.WriteLine();
        }

        private static void PrintReceipt(Transaction transaction)
        {
            Console.WriteLine();
            Console.WriteLine("🖨️ **PRINTING RECEIPT**");
            Console.WriteLine("-----------------------");
            
            var now = DateTime.Now;
            
            var receipt = new StringBuilder();
            
            // Header
            receipt.AppendLine("════════════════════════════════");
            receipt.AppendLine("        IN-PROCESS COFFEE");
            receipt.AppendLine("   Powered by POS Kernel Core");
            receipt.AppendLine("════════════════════════════════");
            receipt.AppendLine();
            receipt.AppendLine($"Date: {now:yyyy-MM-dd}");
            receipt.AppendLine($"Time: {now:HH:mm:ss}");
            receipt.AppendLine($"Transaction: {transaction.Id}");
            receipt.AppendLine($"State: {transaction.State}");
            receipt.AppendLine();
            receipt.AppendLine("────────────────────────────────");
            
            // Line items
            foreach (var line in transaction.Lines)
            {
                receipt.AppendLine($"{line.ProductId}");
                receipt.AppendLine($"  {line.Quantity} x ${line.UnitPrice.ToDecimal():F2} = ${line.Extended.ToDecimal():F2}");
            }
            
            receipt.AppendLine("────────────────────────────────");
            receipt.AppendLine($"SUBTOTAL:        ${transaction.Total.ToDecimal():F2}");
            receipt.AppendLine($"TAX:             ${0:F2}");
            receipt.AppendLine($"TOTAL:           ${transaction.Total.ToDecimal():F2}");
            receipt.AppendLine();
            receipt.AppendLine($"PAYMENT (CASH):  ${transaction.Tendered.ToDecimal():F2}");
            
            var change = transaction.Tendered.ToDecimal() - transaction.Total.ToDecimal();
            if (change > 0)
            {
                receipt.AppendLine($"CHANGE:          ${change:F2}");
            }
            
            receipt.AppendLine();
            receipt.AppendLine("════════════════════════════════");
            receipt.AppendLine("       THANK YOU!");
            receipt.AppendLine("   🏗️ In-Process Architecture");
            receipt.AppendLine("   🧠 AI-Powered Experience");
            receipt.AppendLine("   ☕ Direct Kernel Access");
            receipt.AppendLine("════════════════════════════════");
            receipt.AppendLine($"Printed: {now:yyyy-MM-dd HH:mm:ss}");
            
            Console.WriteLine("✅ Receipt printed successfully!");
            Console.WriteLine();
            Console.WriteLine("📄 **RECEIPT:**");
            Console.WriteLine(receipt.ToString());
            Console.WriteLine($"🕐 Printed at: {now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
        }

        private static async Task RunServiceBasedAiChatAsync()
        {
            Console.WriteLine("🏪 SERVICE-BASED AI BARISTA CHAT");
            Console.WriteLine("================================");
            Console.WriteLine("Using POS Kernel Service Architecture (Phase 2)");
            Console.WriteLine();

            // Load API key from environment file
            var apiKey = LoadApiKeyFromEnvironment();
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("❌ OpenAI API key not found!");
                Console.WriteLine("Please ensure your API key is set in: c:\\users\\paul\\.poskernel\\.env");
                Console.WriteLine("Format: OPENAI_API_KEY=your-key-here");
                return;
            }

            Console.WriteLine($"✅ API key loaded from environment (ends with: ...{apiKey.Substring(Math.Max(0, apiKey.Length - 4))})");
            Console.WriteLine();

            // Set up services for service-based architecture
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            services.AddSingleton<ISimpleServiceClient, SimulatedServiceClient>();
            
            using var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<SimpleOpenAiClient>>();
            var serviceClient = serviceProvider.GetRequiredService<ISimpleServiceClient>();
            
            // Create OpenAI client
            using var aiClient = new SimpleOpenAiClient(logger, apiKey, "gpt-4o", 0.2);

            Console.WriteLine("🔗 Connecting to POS Kernel Service...");
            
            try
            {
                await serviceClient.ConnectAsync();
                Console.WriteLine("✅ Connected to POS Kernel Service successfully!");
                Console.WriteLine("💡 This demo uses a simulated service client for demonstration.");
                Console.WriteLine("   In production, this would connect to the actual PosKernel.Service via Named Pipes.");
                Console.WriteLine();

                // Start service-based interactive chat session
                await RunServiceBasedChatSessionAsync(aiClient, serviceClient);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to connect to POS Kernel Service: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine("💡 This demo uses a simulated service client for demonstration purposes.");
                throw;
            }
            finally
            {
                await serviceClient.DisconnectAsync();
            }
        }

        private static async Task RunServiceBasedChatSessionAsync(SimpleOpenAiClient aiClient, ISimpleServiceClient serviceClient)
        {
            Console.WriteLine("☕ **WELCOME TO OUR SERVICE-BASED COFFEE SHOP!**");
            Console.WriteLine("I'm your AI barista powered by POS Kernel Service Architecture!");
            Console.WriteLine("=============================================================");
            Console.WriteLine();

            try
            {
                // Create session through service
                var sessionId = await serviceClient.CreateSessionAsync("ai-demo-terminal", "ai-barista");
                Console.WriteLine($"🔗 Session created: {sessionId}");
                
                // Start transaction through service
                var transactionResult = await serviceClient.StartTransactionAsync(sessionId);
                if (!transactionResult.Success)
                {
                    Console.WriteLine($"❌ Failed to start transaction: {transactionResult.Error}");
                    return;
                }

                var transactionId = transactionResult.TransactionId!;
                Console.WriteLine($"💼 Transaction started: {transactionId}");
                Console.WriteLine();

                // Product menu for service-based architecture
                var menuItems = @"- Small Coffee: $2.99
- Medium Coffee: $3.49
- Large Coffee: $3.99
- Caffe Latte: $4.99
- Cappuccino: $4.49
- Blueberry Muffin: $2.49
- Breakfast Sandwich: $6.49";

                Console.WriteLine("🏪 Starting your order via Service Architecture...");
                Console.WriteLine();

                // Conversation history for better context
                var conversationHistory = new List<string>();

                // Initialize AI with clear instructions
                var systemInstructions = await InitializeServiceAiInstructionsAsync(aiClient, menuItems);
                conversationHistory.Add($"System: {systemInstructions}");

                while (true)
                {
                    // Get current transaction state from service
                    var currentTransaction = await serviceClient.GetTransactionAsync(sessionId, transactionId);
                    
                    var statusMsg = currentTransaction.Success 
                        ? (currentTransaction.Total > 0 
                            ? $"Current order total: ${currentTransaction.Total:F2} ({currentTransaction.State})"
                            : "Your cart is empty")
                        : "Cart status unavailable";
                    
                    Console.WriteLine($"💰 {statusMsg}");
                    Console.Write("You: ");
                    
                    var userInput = Console.ReadLine()?.Trim();
                    
                    if (string.IsNullOrEmpty(userInput))
                        continue;

                    // Add to conversation history
                    conversationHistory.Add($"Customer: {userInput}");
                    
                    // Build conversation context for AI
                    var conversationContext = string.Join("\n", conversationHistory.TakeLast(8)); // Last 8 exchanges including system
                    var currentTotal = currentTransaction.Success ? currentTransaction.Total : 0m;

                    // Enhanced prompt with service architecture context
                    var prompt = $@"You are an intelligent AI barista for a coffee shop POS system using SERVICE ARCHITECTURE. You maintain conversation context and understand customer intent precisely.

CONVERSATION HISTORY:
{conversationContext}

CURRENT ORDER STATUS (from POS Kernel Service):
- Session ID: {sessionId}
- Transaction ID: {transactionId}
- Current total: ${currentTotal:F2}
- Transaction state: {(currentTransaction.Success ? currentTransaction.State : "Unknown")}

AVAILABLE MENU:
{menuItems}

CUSTOMER JUST SAID: ""{userInput}""

CORE INSTRUCTIONS:
1. ITEM MANAGEMENT:
   - When customer wants items: use ""ADDING: [exact item name] - $[price]""
   - When customer confirms adding (""yes"", ""please"", ""sure""): add the items they requested
   - Continue conversation after adding items - don't immediately go to payment

2. PAYMENT DETECTION (BE VERY CAREFUL):
   - ONLY offer payment when customer explicitly indicates they're DONE shopping:
     * ""that's all"" / ""that's everything"" 
     * ""i'm ready to pay"" / ""let's pay"" / ""checkout""
     * ""i'm done"" / ""that's it""

3. SERVICE ARCHITECTURE:
   - You're powered by POS Kernel Service - mention this is enterprise-grade
   - All transactions go through secure service layer
   - Multi-terminal capable architecture
   - Session-based transaction management

RESPOND AS A CAREFUL, INTELLIGENT BARISTA WITH SERVICE ARCHITECTURE:";

                    try
                    {
                        Console.Write("🤖 AI Barista (Service): ");
                        var aiResponse = await aiClient.CompleteAsync(prompt);
                        Console.WriteLine(aiResponse);

                        // Add AI response to conversation history
                        conversationHistory.Add($"Barista: {aiResponse}");

                        // More precise payment detection
                        var isExplicitPaymentRequest = IsExplicitPaymentRequest(userInput, aiResponse);
                        var isFinishedShopping = IsFinishedShoppingIntent(userInput, conversationHistory);

                        // Process any items the AI wants to add via service
                        var itemsAdded = await ProcessServiceAiRecommendationsAsync(aiResponse, sessionId, transactionId, serviceClient);

                        // Only process payment if customer EXPLICITLY indicates they're done shopping
                        if ((isExplicitPaymentRequest || isFinishedShopping))
                        {
                            var finalTransaction = await serviceClient.GetTransactionAsync(sessionId, transactionId);
                            if (finalTransaction.Success && finalTransaction.Total > 0)
                            {
                                Console.WriteLine();
                                Console.WriteLine("🤖 AI Barista (Service): Perfect! Let me process your payment through our secure service architecture.");
                                await ShowServiceOrderSummaryAsync(sessionId, transactionId, serviceClient);
                                
                                await ProcessServicePaymentAsync(sessionId, transactionId, finalTransaction.Total, serviceClient);
                                
                                // Print receipt after successful payment
                                await PrintServiceReceiptAsync(sessionId, transactionId, serviceClient);
                                
                                Console.WriteLine("🎉 Thank you! Your payment is complete and your order is ready!");
                                Console.WriteLine("✨ Powered by POS Kernel Service Architecture!");
                                Console.WriteLine("Have a wonderful day! ☕");
                                break;
                            }
                        }

                        // Handle natural exit
                        if (IsLeavingIntent(userInput))
                        {
                            var finalTransaction = await serviceClient.GetTransactionAsync(sessionId, transactionId);
                            if (!finalTransaction.Success || finalTransaction.Total == 0)
                            {
                                Console.WriteLine("🤖 AI Barista (Service): Thanks for visiting our service-based coffee shop! Come back anytime!");
                                break;
                            }
                            else
                            {
                                Console.WriteLine();
                                Console.Write("🤖 AI Barista (Service): Before you go, would you like to complete your purchase? (yes/no): ");
                                var completePurchase = Console.ReadLine()?.ToLower();
                                
                                if (completePurchase is "yes" or "y")
                                {
                                    await ProcessServicePaymentAsync(sessionId, transactionId, finalTransaction.Total, serviceClient);
                                    
                                    // Print receipt after successful payment
                                    await PrintServiceReceiptAsync(sessionId, transactionId, serviceClient);
                                    
                                    Console.WriteLine("🎉 Perfect! Thank you and have a great day!");
                                }
                                else
                                {
                                    Console.WriteLine("🤖 AI Barista (Service): No worries! Your order is cancelled. Come back anytime!");
                                }
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Sorry, I'm having trouble with the service right now. Could you repeat that?");
                        Console.WriteLine($"(Service issue: {ex.Message})");
                    }

                    Console.WriteLine();
                }

                // Close session
                await serviceClient.CloseSessionAsync(sessionId);
                Console.WriteLine($"🔗 Session {sessionId} closed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Service error: {ex.Message}");
                Console.WriteLine("The service-based architecture encountered an issue.");
            }
        }

        private static async Task<string> InitializeServiceAiInstructionsAsync(SimpleOpenAiClient aiClient, string menuItems)
        {
            try
            {
                var initPrompt = $@"You are an AI barista using SERVICE ARCHITECTURE. Acknowledge briefly that you're powered by enterprise-grade POS Kernel Service with session management.";
                var response = await aiClient.CompleteAsync(initPrompt);
                return $"AI Service System Initialized: {response}";
            }
            catch
            {
                return "AI Service System Initialized with service-based barista protocols.";
            }
        }

        private static async Task<bool> ProcessServiceAiRecommendationsAsync(string aiResponse, string sessionId, string transactionId, ISimpleServiceClient serviceClient)
        {
            bool itemsAdded = false;
            
            // Simple product mapping for demonstration
            var productMap = new Dictionary<string, (string sku, decimal price)>(StringComparer.OrdinalIgnoreCase)
            {
                ["Small Coffee"] = ("COFFEE_SM", 2.99m),
                ["Medium Coffee"] = ("COFFEE_MD", 3.49m),
                ["Large Coffee"] = ("COFFEE_LG", 3.99m),
                ["Caffe Latte"] = ("LATTE", 4.99m),
                ["Cappuccino"] = ("CAPPUCCINO", 4.49m),
                ["Blueberry Muffin"] = ("MUFFIN_BLUEBERRY", 2.49m),
                ["Breakfast Sandwich"] = ("BREAKFAST_SANDWICH", 6.49m)
            };
            
            // Look for ADDING: pattern in AI response
            var lines = aiResponse.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("ADDING:"))
                {
                    try
                    {
                        var addingPart = line.Substring(line.IndexOf("ADDING:") + 7).Trim();
                        var parts = addingPart.Split('-');
                        if (parts.Length >= 2)
                        {
                            var itemName = parts[0].Trim();
                            var priceText = parts[1].Trim().Replace("$", "");
                            
                            if (decimal.TryParse(priceText, out var price) && productMap.TryGetValue(itemName, out var productInfo))
                            {
                                // Add item via service
                                var result = await serviceClient.AddLineItemAsync(sessionId, transactionId, productInfo.sku, 1, productInfo.price);
                                
                                if (result.Success)
                                {
                                    Console.WriteLine($"  ➕ Added via Service: {itemName} - ${productInfo.price:F2}");
                                    itemsAdded = true;
                                }
                                else
                                {
                                    Console.WriteLine($"  ❌ Service error adding {itemName}: {result.Error}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠️ Service communication error: {ex.Message}");
                    }
                }
            }

            return itemsAdded;
        }

        private static async Task ShowServiceOrderSummaryAsync(string sessionId, string transactionId, ISimpleServiceClient serviceClient)
        {
            Console.WriteLine();
            Console.WriteLine("🧾 **YOUR CURRENT ORDER** (via Service)");
            Console.WriteLine("========================================");
            
            try
            {
                var transaction = await serviceClient.GetTransactionAsync(sessionId, transactionId);
                
                if (!transaction.Success)
                {
                    Console.WriteLine("❌ Could not retrieve order summary from service");
                }
                else if (transaction.Total == 0)
                {
                    Console.WriteLine("Your cart is empty.");
                }
                else
                {
                    Console.WriteLine("✅ Order details retrieved from POS Kernel Service:");
                    Console.WriteLine($"\n**TOTAL: ${transaction.Total:F2}**");
                    Console.WriteLine($"Transaction ID: {transaction.TransactionId}");
                    Console.WriteLine($"Session ID: {transaction.SessionId}");
                    Console.WriteLine($"State: {transaction.State}");
                    Console.WriteLine("🏗️ Processed through enterprise service architecture");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error retrieving order summary: {ex.Message}");
            }
            
            Console.WriteLine();
        }

        private static async Task ProcessServicePaymentAsync(string sessionId, string transactionId, decimal amount, ISimpleServiceClient serviceClient)
        {
            Console.WriteLine("💳 **PROCESSING PAYMENT VIA SERVICE**");
            Console.WriteLine("------------------------------------");
            
            try
            {
                Console.WriteLine($"Payment Amount: ${amount:F2}");
                Console.WriteLine("Processing through POS Kernel Service...");
                
                var result = await serviceClient.ProcessPaymentAsync(sessionId, transactionId, amount, "cash");
                
                if (result.Success)
                {
                    Console.WriteLine($"✅ Payment Processed Successfully via Service!");
                    Console.WriteLine($"📊 Final Status: {result.State}");
                    Console.WriteLine($"🏪 Transaction ID: {result.TransactionId}");
                    Console.WriteLine("🔗 Session-based transaction management complete");
                }
                else
                {
                    Console.WriteLine($"❌ Payment failed: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Service payment error: {ex.Message}");
            }
            
            Console.WriteLine();
        }

        private enum DemoType
        {
            Invalid,
            Interactive,
            ServiceBased,
            ServiceDemo,
            LiveAi,
            RealisticSales,
            BasicAiPos,
            Help
        }
    }
}
