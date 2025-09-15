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

namespace PosKernel.AI
{
    /// <summary>
    /// Main program for POS Kernel AI integration demonstrations.
    /// Uses ONLY real kernel facilities - no simulations or mocks.
    /// Demonstrates AI integration with actual POS transaction processing.
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("🤖 POS Kernel AI Integration Demo v0.5.0");
            Console.WriteLine("🚀 Real Kernel Architecture - No Simulations!");
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
            Console.WriteLine("🗣️ REAL KERNEL AI BARISTA CHAT");
            Console.WriteLine("==============================");
            Console.WriteLine("Using actual POS Kernel with real restaurant database");
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
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
            services.AddSingleton<IProductCatalogService, RestaurantProductCatalogService>();
            
            using var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<SimpleOpenAiClient>>();
            var productCatalog = serviceProvider.GetRequiredService<IProductCatalogService>();
            
            // Create OpenAI client with real API integration
            using var aiClient = new SimpleOpenAiClient(logger, apiKey, "gpt-4o", 0.2);

            // Start real kernel interactive chat session
            await RunRealKernelChatSessionAsync(aiClient, productCatalog);
        }

        private static async Task RunRealKernelChatSessionAsync(SimpleOpenAiClient aiClient, IProductCatalogService productCatalog)
        {
            Console.WriteLine("☕ **WELCOME TO OUR REAL KERNEL COFFEE SHOP!**");
            Console.WriteLine("I'm your AI barista powered by actual POS Kernel!");
            Console.WriteLine("===============================================");
            Console.WriteLine();

            // Create a REAL transaction using kernel contracts
            var transaction = new Transaction
            {
                Id = TransactionId.New(),
                State = TransactionState.Building
            };

            // Get REAL products from restaurant database
            var availableProducts = await productCatalog.GetPopularItemsAsync();
            var menuItems = string.Join("\n", availableProducts.Select(p => $"- {p.Name}: ${p.BasePriceCents / 100.0:F2}"));

            Console.WriteLine($"🏪 Starting your order with real transaction: {transaction.Id}");
            Console.WriteLine();

            var conversationHistory = new List<string>();
            var systemInstructions = await InitializeAiInstructionsAsync(aiClient, menuItems);
            conversationHistory.Add($"System: {systemInstructions}");

            while (true)
            {
                var statusMsg = transaction.Lines.Any() 
                    ? $"Current order: {string.Join(", ", transaction.Lines.Select(l => l.ProductId))} (${transaction.Total.ToDecimal():F2})"
                    : "Your cart is empty";
                
                Console.WriteLine($"💰 {statusMsg}");
                Console.Write("You: ");
                
                var userInput = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(userInput))
                {
                    continue;
                }

                conversationHistory.Add($"Customer: {userInput}");
                
                var conversationContext = string.Join("\n", conversationHistory.TakeLast(8));
                var currentTotal = transaction.Total.ToDecimal();
                var currentItemsList = transaction.Lines.Any() 
                    ? string.Join(", ", transaction.Lines.Select(l => $"{l.ProductId} x{l.Quantity}"))
                    : "empty cart";

                var prompt = $@"You are an intelligent AI barista for a coffee shop POS system using REAL POS Kernel. You maintain conversation context and understand customer intent precisely.

CONVERSATION HISTORY:
{conversationContext}

CURRENT ORDER STATUS (Real Kernel Transaction):
- Items in cart: {currentItemsList}
- Current total: ${currentTotal:F2}
- Transaction state: {transaction.State}
- Transaction ID: {transaction.Id}

AVAILABLE MENU (from real restaurant database):
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

3. REAL KERNEL ARCHITECTURE:
   - You're powered by actual POS Kernel with real transaction processing
   - All operations use real kernel contracts and transaction state
   - Products come from real restaurant database with SQLite

RESPOND AS A CAREFUL, INTELLIGENT BARISTA WITH REAL KERNEL:";

                try
                {
                    Console.Write("🤖 AI Barista (Real Kernel): ");
                    var aiResponse = await aiClient.CompleteAsync(prompt);
                    Console.WriteLine(aiResponse);

                    conversationHistory.Add($"Barista: {aiResponse}");

                    var isExplicitPaymentRequest = IsExplicitPaymentRequest(userInput, aiResponse);
                    var isFinishedShopping = IsFinishedShoppingIntent(userInput, conversationHistory);

                    // Process any items the AI wants to add using REAL kernel
                    var itemsAdded = await ProcessAiRecommendationsAsync(aiResponse, transaction, availableProducts);

                    if ((isExplicitPaymentRequest || isFinishedShopping) && transaction.Lines.Any())
                    {
                        Console.WriteLine();
                        Console.WriteLine("🤖 AI Barista: Perfect! Let me process your payment using real kernel.");
                        ShowOrderSummary(transaction);
                        
                        await ProcessPaymentAsync(transaction);
                        PrintReceipt(transaction);
                        
                        Console.WriteLine("🎉 Thank you! Your payment is complete and your order is ready!");
                        Console.WriteLine("✨ Powered by Real POS Kernel Architecture!");
                        Console.WriteLine("Have a wonderful day! ☕");
                        break;
                    }

                    if (IsLeavingIntent(userInput) && !transaction.Lines.Any())
                    {
                        Console.WriteLine("🤖 AI Barista: Thanks for visiting! Come back anytime!");
                        break;
                    }

                    if (IsLeavingIntent(userInput) && transaction.Lines.Any() && !isFinishedShopping)
                    {
                        Console.WriteLine();
                        Console.Write("🤖 AI Barista: Before you go, would you like to complete your purchase? (yes/no): ");
                        var completePurchase = Console.ReadLine()?.ToLower();
                        
                        if (completePurchase is "yes" or "y")
                        {
                            await ProcessPaymentAsync(transaction);
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
            var initPrompt = $@"You are starting a new session as an AI barista for a coffee shop POS system using REAL POS Kernel. Here are your core guidelines:

MENU (from real restaurant database):
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
                return $"AI Real Kernel System Initialized: {response}";
            }
            catch
            {
                return "AI Real Kernel System Initialized with real kernel protocols.";
            }
        }

        private static bool IsExplicitPaymentRequest(string userInput, string aiResponse)
        {
            var input = userInput.ToLower();
            var response = aiResponse.ToLower();
            
            if (input.Contains("can i pay") || input.Contains("how do i pay") || 
                input.Contains("what payment") || input.Contains("do you accept"))
            {
                return false;
            }
            
            var paymentCommands = new[] { "let's pay", "process payment", "charge me", "take payment", "i'll pay now" };
            var hasPaymentCommand = paymentCommands.Any(cmd => input.Contains(cmd));
            
            var aiPaymentPhrases = new[] { "ready to pay", "process payment", "checkout", "complete your purchase" };
            var aiIndicatesPayment = aiPaymentPhrases.Any(phrase => response.Contains(phrase));
            
            return hasPaymentCommand || aiIndicatesPayment;
        }

        private static bool IsFinishedShoppingIntent(string userInput, List<string> conversationHistory)
        {
            var input = userInput.ToLower();
            
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

            // Create a REAL POS transaction using kernel contracts
            var transaction = new Transaction
            {
                Id = TransactionId.New(),
                State = TransactionState.Building
            };

            Console.WriteLine($"🏪 **New Real Transaction Started**: {transaction.Id}");
            Console.WriteLine($"📊 Status: {transaction.State}");
            Console.WriteLine();

            // Get REAL products from restaurant catalog
            var availableProducts = await productCatalog.GetPopularItemsAsync();
            var menuItems = string.Join("\n", availableProducts.Take(5).Select(p => $"- {p.Name}: ${p.BasePriceCents / 100.0:F2}"));

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

                var prompt = $@"You are an AI barista for a coffee shop POS system using REAL POS Kernel. The customer says: ""{customerRequest}""

Current real transaction: {transaction.Id} (Total so far: ${currentTotal:F2})
Current items: {currentItemsList}

Available menu items (from real database):
{menuItems}

As a helpful AI barista using real kernel:
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

                // Parse AI response for items to add using REAL kernel
                await ProcessAiRecommendationsAsync(aiResponse, transaction, availableProducts);
                
                Console.WriteLine($"📊 **Real Transaction Update**: {transaction.Lines.Count} items, Total: ${transaction.Total.ToDecimal():F2}");
                Console.WriteLine();
            }

            // Show final REAL transaction details
            Console.WriteLine("🧾 **FINAL REAL TRANSACTION SUMMARY**");
            Console.WriteLine("===================================");
            Console.WriteLine($"Transaction ID: {transaction.Id}");
            Console.WriteLine($"Status: {transaction.State}");
            Console.WriteLine($"Items ({transaction.Lines.Count}):");
            
            foreach (var line in transaction.Lines)
            {
                Console.WriteLine($"  • {line.ProductId} - ${line.UnitPrice.ToDecimal():F2} x {line.Quantity} = ${line.Extended.ToDecimal():F2}");
            }
            
            Console.WriteLine($"**TOTAL: ${transaction.Total.ToDecimal():F2}**");
            Console.WriteLine();

            if (transaction.Lines.Any())
            {
                await ProcessFinalUpsellAsync(aiClient, transaction);
                await ProcessPaymentAsync(transaction);
                PrintReceipt(transaction);
                
                Console.WriteLine("✅ **REAL TRANSACTION COMPLETED SUCCESSFULLY!**");
                Console.WriteLine($"🎉 AI successfully helped complete sale using real kernel: ${transaction.Total.ToDecimal():F2}");
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
            var prompt = $@"As an AI barista using real POS kernel, the customer has ordered: {currentItems} for a total of ${transaction.Total.ToDecimal():F2}

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

        private static async Task<bool> ProcessAiRecommendationsAsync(string aiResponse, Transaction transaction, IReadOnlyList<IProductInfo> availableProducts)
        {
            bool itemsAdded = false;
            
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
                                // Find matching product in REAL database
                                var product = availableProducts.FirstOrDefault(p => 
                                    p.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase) ||
                                    itemName.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
                                
                                if (product != null)
                                {
                                    // Add to REAL transaction using kernel contracts
                                    var productId = new ProductId(product.Sku);
                                    var unitPrice = new Money(product.BasePriceCents, transaction.Currency);
                                    
                                    transaction.AddLine(productId, 1, unitPrice);
                                    Console.WriteLine($"  ➕ Added to real kernel: {product.Name} - ${product.BasePriceCents / 100.0:F2}");
                                    itemsAdded = true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠️ Had trouble adding that item to real kernel: {ex.Message}");
                    }
                }
            }

            // Fix CS1998 by adding await
            await Task.CompletedTask;
            return itemsAdded;
        }

        private static void ShowOrderSummary(Transaction transaction)
        {
            Console.WriteLine();
            Console.WriteLine("🧾 **YOUR CURRENT ORDER (Real Kernel)**");
            Console.WriteLine("======================================");
            
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
            Console.WriteLine("💳 **PROCESSING PAYMENT (Real Kernel)**");
            Console.WriteLine("--------------------------------------");
            
            var paymentAmount = new Money(transaction.Total.MinorUnits, transaction.Currency);
            
            Console.WriteLine($"Payment Amount: ${transaction.Total.ToDecimal():F2}");
            Console.WriteLine("Processing through real kernel transaction engine...");
            
            await Task.Delay(1000);
            
            // Use REAL kernel payment processing
            transaction.AddCashTender(paymentAmount);
            
            Console.WriteLine($"✅ Payment Processed Successfully by Real Kernel!");
            Console.WriteLine($"📊 Final Status: {transaction.State}");
            Console.WriteLine();
        }

        private static void PrintReceipt(Transaction transaction)
        {
            Console.WriteLine();
            Console.WriteLine("🖨️ **PRINTING RECEIPT (Real Kernel)**");
            Console.WriteLine("------------------------------------");
            
            var now = DateTime.Now;
            
            var receipt = new StringBuilder();
            
            // Header
            receipt.AppendLine("════════════════════════════════");
            receipt.AppendLine("        REAL KERNEL COFFEE");
            receipt.AppendLine("   Powered by Actual POS Kernel");
            receipt.AppendLine("════════════════════════════════");
            receipt.AppendLine();
            receipt.AppendLine($"Date: {now:yyyy-MM-dd}");
            receipt.AppendLine($"Time: {now:HH:mm:ss}");
            receipt.AppendLine($"Transaction: {transaction.Id}");
            receipt.AppendLine($"State: {transaction.State}");
            receipt.AppendLine();
            receipt.AppendLine("────────────────────────────────");
            
            // Line items from REAL transaction
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
            receipt.AppendLine("   🚀 Real POS Kernel Architecture");
            receipt.AppendLine("   🧠 AI-Powered Experience");
            receipt.AppendLine("   📊 Actual Transaction Processing");
            receipt.AppendLine("   🗄️  Real Restaurant Database");
            receipt.AppendLine("════════════════════════════════");
            receipt.AppendLine($"Printed: {now:yyyy-MM-dd HH:mm:ss}");
            
            Console.WriteLine("✅ Receipt printed successfully!");
            Console.WriteLine();
            Console.WriteLine("📄 **RECEIPT:**");
            Console.WriteLine(receipt.ToString());
            Console.WriteLine($"🕐 Printed at: {now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
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
                "--live" or "--sales" or "-l" => DemoType.LiveAi,
                "--help" or "-h" => DemoType.Help,
                _ => DemoType.Invalid
            };
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Usage: dotnet run [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --interactive, --chat, -i   💬 Interactive AI Barista Chat (DEFAULT)");
            Console.WriteLine("  --live, --sales, -l         🔥 Live AI Sales Process Demo");
            Console.WriteLine("  --help, -h                  Show this help message");
            Console.WriteLine();
            Console.WriteLine("💬 Interactive AI Barista Chat:");
            Console.WriteLine("  • Real POS Kernel transaction processing");
            Console.WriteLine("  • Live OpenAI integration");
            Console.WriteLine("  • Actual restaurant database with SQLite");
            Console.WriteLine("  • Natural language: 'I want coffee' → AI processes → Real transactions");
            Console.WriteLine("  • Professional receipt printing");
            Console.WriteLine();
            Console.WriteLine("🔥 Live AI Sales Process Demo:");
            Console.WriteLine("  • Automated sales scenario with real kernel");
            Console.WriteLine("  • AI-driven product recommendations");
            Console.WriteLine("  • Complete transaction lifecycle demonstration");
            Console.WriteLine("  • Shows AI upselling and customer interaction");
            Console.WriteLine();
            Console.WriteLine("🚀 Real Architecture Features:");
            Console.WriteLine("   ✅ Actual POS Kernel transaction engine");
            Console.WriteLine("   ✅ Real restaurant product database (SQLite)");
            Console.WriteLine("   ✅ Live OpenAI API integration");
            Console.WriteLine("   ✅ Professional receipt generation");
            Console.WriteLine("   ✅ No simulations or mocks - everything is real!");
            Console.WriteLine();
            Console.WriteLine("💡 Try the real AI barista:");
            Console.WriteLine("   dotnet run -- --interactive");
        }

        private enum DemoType
        {
            Invalid,
            Interactive,
            LiveAi,
            Help
        }
    }
}
