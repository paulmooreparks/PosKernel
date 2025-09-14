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
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("🤖 POS Kernel AI Integration Demo v0.4.0");
            Console.WriteLine("Demonstrating Domain Extension Architecture + LIVE AI");
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
     * ""That's all"" / ""That's everything"" 
     * ""I'm ready to pay"" / ""Let's pay"" / ""Checkout""
     * ""I'm done"" / ""That's it""
   - DO NOT offer payment for payment method questions (""Can I pay by card?"")
   - Answer payment method questions but continue conversation
   - DO NOT offer payment when customer just confirms adding items
   - DO NOT offer payment after adding items unless they indicate they're done

3. CONVERSATION FLOW:
   - Be friendly and conversational
   - After adding items, ask if they want anything else
   - Only when they clearly indicate they're finished, then offer payment
   - Maintain context from previous exchanges

4. EXAMPLES:
   Customer: ""I want a latte and muffin"" -> Describe items, ask for confirmation
   Customer: ""Yes please"" -> Add items, ask ""Anything else?"":
   Customer: ""That's all"" -> NOW offer to process payment

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
            Console.WriteLine("  --interactive, --chat, -i  💬 Interactive AI Barista Chat (LIVE OpenAI) - DEFAULT");
            Console.WriteLine("  --live, --real, -l          🔥 Run LIVE AI Sales Process with Kernel Integration");
            Console.WriteLine("  --realistic, -r             Run realistic AI sales demo with mock responses");
            Console.WriteLine("  --basic, -b                 Run basic AI-POS integration demo");  
            Console.WriteLine("  --help, -h                  Show this help message");
            Console.WriteLine();
            Console.WriteLine("💬 Interactive AI Barista Chat (DEFAULT):");
            Console.WriteLine("  • Talk naturally - no special commands needed!");
            Console.WriteLine("  • AI understands intent: \"I want coffee\", \"That's all\", \"What's good?\"");
            Console.WriteLine("  • Maintains conversation context throughout your order");
            Console.WriteLine("  • Automatically processes payment when ready");
            Console.WriteLine("  • Real OpenAI integration with your API key");
        }

        private enum DemoType
        {
            Invalid,
            Interactive,
            LiveAi,
            RealisticSales,
            BasicAiPos,
            Help
        }
    }
}
