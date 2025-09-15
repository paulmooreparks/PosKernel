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
using PosKernel.Configuration;

namespace PosKernel.AI
{
    /// <summary>
    /// Main program for POS Kernel AI integration demonstrations.
    /// Uses MCP (Model Context Protocol) for structured AI tool calling.
    /// Demonstrates AI integration with actual POS transaction processing.
    /// Follows documented security-first design with kernel isolation.
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("🤖 POS Kernel AI Integration Demo v0.6.0 (MCP)");
            Console.WriteLine("🚀 Real Kernel Architecture with Model Context Protocol");
            Console.WriteLine("🔐 Security-First Design with Kernel Isolation");
            Console.WriteLine();

            try
            {
                var demoType = ParseDemoType(args);
                
                switch (demoType)
                {
                    case DemoType.Interactive:
                        await RunInteractiveMcpChatAsync();
                        break;

                    case DemoType.LiveDemo:
                        await RunLiveMcpSalesProcessAsync();
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
                Console.Error.WriteLine($"📍 Stack trace: {ex.StackTrace}");
                return 1;
            }
        }

        private static async Task RunInteractiveMcpChatAsync()
        {
            Console.WriteLine("🗣️ INTERACTIVE MCP AI BARISTA CHAT");
            Console.WriteLine("==================================");
            Console.WriteLine("Using Model Context Protocol for structured AI tool calling");
            Console.WriteLine("Following documented security-first design patterns");
            Console.WriteLine();

            // Load configuration following documented patterns
            var config = PosKernelConfiguration.Initialize();
            var aiConfig = new AiConfiguration(config);
            
            var apiKey = aiConfig.ApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("❌ OpenAI API key not found!");
                Console.WriteLine("Please configure your API key in one of these locations:");
                Console.WriteLine($"  • {PosKernelConfiguration.SecretsFilePath}");
                Console.WriteLine($"  • Environment variable: OPENAI_API_KEY");
                Console.WriteLine();
                Console.WriteLine("Format in .env file: OPENAI_API_KEY=your-key-here");
                return;
            }

            Console.WriteLine($"✅ API key loaded from configuration (ends with: ...{apiKey.Substring(Math.Max(0, apiKey.Length - 4))})");
            Console.WriteLine();

            // Configure services following DI best practices
            var services = new ServiceCollection();
            services.AddLogging(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Warning));
            
            // Register core services
            services.AddSingleton<IProductCatalogService, RestaurantProductCatalogService>();
            
            // Register MCP services with proper configuration
            var mcpConfig = aiConfig.ToMcpConfiguration();
            services.AddSingleton(mcpConfig);
            services.AddSingleton<McpClient>();
            services.AddSingleton<PosToolsProvider>();
            
            using var serviceProvider = services.BuildServiceProvider();
            
            try 
            {
                var productCatalog = serviceProvider.GetRequiredService<IProductCatalogService>();
                var mcpClient = serviceProvider.GetRequiredService<McpClient>();
                var toolsProvider = serviceProvider.GetRequiredService<PosToolsProvider>();

                await RunMcpChatSessionAsync(mcpClient, toolsProvider, productCatalog);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Service initialization failed: {ex.Message}");
                Console.WriteLine("Please check your configuration and try again.");
            }
        }

        private static async Task RunMcpChatSessionAsync(McpClient mcpClient, PosToolsProvider toolsProvider, IProductCatalogService productCatalog)
        {
            Console.WriteLine("☕ **WELCOME TO OUR MCP-POWERED COFFEE SHOP!**");
            Console.WriteLine("I'm your AI barista using Model Context Protocol!");
            Console.WriteLine("================================================");
            Console.WriteLine();

            // Create transaction using documented kernel contracts
            var transaction = new Transaction
            {
                Id = TransactionId.New(),
                State = TransactionState.Building,
                Currency = "USD" // Following ISO 4217 standard
            };

            var availableProducts = await productCatalog.GetPopularItemsAsync();
            var availableTools = toolsProvider.GetAvailableTools();

            Console.WriteLine($"🏪 Transaction started: {transaction.Id}");
            Console.WriteLine($"🛠️  MCP tools available: {availableTools.Count}");
            Console.WriteLine($"📦 Products in catalog: {availableProducts.Count}");
            Console.WriteLine();

            var conversationHistory = new List<string>();
            
            // Generate contextual AI greeting
            var currentTime = DateTime.Now;
            var timeOfDay = currentTime.Hour < 12 ? "morning" : currentTime.Hour < 17 ? "afternoon" : "evening";
            
            var initialGreetingPrompt = $@"You are an AI barista at a coffee shop. A customer just walked in. Give them a brief, natural greeting like a real barista would.

TIME OF DAY: {timeOfDay} ({currentTime:HH:mm})

Be friendly but concise - just greet them and ask what you can get them. Don't list menu items or be overly sales-focused.

Examples of good greetings:
- ""Good morning! What can I get for you today?""
- ""Hi there! What sounds good to you?""
- ""Good afternoon! How can I help you?""/

Greet the customer now:";

            // AI greeting with fallback
            try
            {
                Console.Write("🤖 AI Barista (MCP): ");
                var greetingResponse = await mcpClient.CompleteWithToolsAsync(initialGreetingPrompt, availableTools);
                Console.WriteLine(greetingResponse.Content);
                Console.WriteLine();
                conversationHistory.Add($"Barista: {greetingResponse.Content}");
            }
            catch
            {
                Console.WriteLine("Good morning! Welcome to our MCP-powered coffee shop! I can help you find exactly what you need. What can I get started for you today?");
                Console.WriteLine("(MCP greeting failed - using fallback)");
                Console.WriteLine();
                conversationHistory.Add("Barista: Good morning! Welcome to our MCP-powered coffee shop!");
            }

            // Main conversation loop
            while (true)
            {
                DisplayTransactionStatus(transaction);
                Console.Write("You: ");
                
                var userInput = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(userInput))
                {
                    continue;
                }

                // Handle exit commands
                if (IsExitCommand(userInput))
                {
                    HandleExitCommand(userInput, transaction);
                    break;
                }

                conversationHistory.Add($"Customer: {userInput}");
                var conversationContext = string.Join("\n", conversationHistory.TakeLast(8));

                // Create MCP-enhanced prompt following documented patterns
                var prompt = CreateMcpPrompt(conversationContext, transaction, userInput);

                try
                {
                    Console.Write("🤖 AI Barista (MCP): ");
                    var response = await mcpClient.CompleteWithToolsAsync(prompt, availableTools);
                    
                    // Display AI's conversational response FIRST
                    if (!string.IsNullOrEmpty(response.Content))
                    {
                        Console.WriteLine(response.Content);
                    }

                    // Execute MCP tool calls and show results
                    if (response.ToolCalls.Any())
                    {
                        Console.WriteLine($"  🛠️  Executing {response.ToolCalls.Count} MCP tool(s)...");
                        
                        var toolResults = new List<string>();
                        foreach (var toolCall in response.ToolCalls)
                        {
                            var result = await toolsProvider.ExecuteToolAsync(toolCall, transaction);
                            Console.WriteLine($"  📊 {toolCall.FunctionName}: {result}");
                            toolResults.Add(result);
                        }

                        // Generate follow-up response after tool execution
                        var followUpPrompt = CreateFollowUpPrompt(conversationContext, transaction, userInput, response.Content, toolResults);
                        
                        try 
                        {
                            var followUpResponse = await mcpClient.CompleteWithToolsAsync(followUpPrompt, availableTools);
                            if (!string.IsNullOrEmpty(followUpResponse.Content))
                            {
                                Console.WriteLine($"🤖 {followUpResponse.Content}");
                            }
                            
                            // Add the complete conversation to history
                            conversationHistory.Add($"Barista: {response.Content} [Executed tools] {followUpResponse.Content}");
                        }
                        catch
                        {
                            // Fallback follow-up if AI fails
                            var itemCount = transaction.Lines.Count;
                            var total = transaction.Total.ToDecimal();
                            Console.WriteLine($"🤖 Great! Your order now has {itemCount} items totaling ${total:F2}. What else can I get for you today?");
                            conversationHistory.Add($"Barista: {response.Content} [Executed tools] Great! What else can I get for you?");
                        }
                    }
                    else
                    {
                        // No tools called, just add the response to history
                        conversationHistory.Add($"Barista: {response.Content}");
                    }

                    // Check for completion
                    if (IsCompletionRequest(userInput) && transaction.Lines.Any())
                    {
                        // Don't call HandleTransactionCompletion if transaction is already completed by payment processing
                        if (transaction.State != TransactionState.Completed)
                        {
                            HandleTransactionCompletion(transaction);
                        }
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Sorry, I'm having trouble with my MCP connection right now. Could you repeat that?");
                    Console.WriteLine($"(Technical issue: {ex.Message})");
                }

                Console.WriteLine();
            }
        }

        private static string CreateFollowUpPrompt(string conversationContext, Transaction transaction, string userInput, string initialResponse, List<string> toolResults)
        {
            var toolSummary = string.Join("; ", toolResults);
            var currentItems = transaction.Lines.Select(l => l.ProductId.ToString()).ToList();
            var hasHotDrink = currentItems.Any(item => item.Contains("COFFEE") || item.Contains("LATTE") || item.Contains("CAPPUCCINO") || item.Contains("TEA"));
            var hasFood = currentItems.Any(item => item.Contains("CROISSANT") || item.Contains("MUFFIN") || item.Contains("SANDWICH"));
            
            return $@"You just executed MCP tools that returned structured results. Now provide a natural follow-up response based on what actually happened.

CONTEXT:
- Customer said: ""{userInput}""
- Your initial response: ""{initialResponse}""
- MCP tool results: {toolSummary}
- Current transaction: {transaction.Lines.Count} items, ${transaction.Total.ToDecimal():F2}
- Transaction state: {transaction.State}
- Items in order: {string.Join(", ", currentItems)}

ORDER AWARENESS:
- Has hot drink: {hasHotDrink}
- Has food item: {hasFood}

CRITICAL: CHECK TRANSACTION STATE FIRST!
If transaction state is Completed, the customer has PAID and the sale is DONE. Do NOT try to upsell or ask for more items!

FOLLOW-UP INSTRUCTIONS - INTERPRET MCP RESULTS LIKE A REAL BARISTA:

If MCP returned payment processed and transaction is COMPLETED:
- Thank them for their purchase
- Confirm what they ordered
- Tell them to enjoy their items
- DO NOT ask for more items or try to upsell
- Example: Thank you! Your Medium Coffee and Breakfast Sandwich are ready. Enjoy your meal!

If MCP returned item added and transaction is NOT completed:
- Confirm what was added and mention the new total
- Look for UPSELL_HINT in the response and use it naturally
- Ask what else you can get them

If MCP returned disambiguation needed:
- Present the options clearly from the MCP response
- Ask the customer which one they would like
- Don't add anything yet - wait for their choice

If MCP returned product not found:
- Be a helpful barista! Ask clarifying questions
- Suggest alternatives or ask what they had in mind

SMART UPSELLING - BUT ONLY IF TRANSACTION IS NOT COMPLETED:
- The MCP layer provides strategic upsell hints based on business rules
- Use these hints naturally in your conversation
- BUT NEVER upsell after payment is processed!

EXAMPLE RESPONSES:

If transaction is COMPLETED (payment processed):
Perfect! Your payment has been processed and your Medium Coffee and Breakfast Sandwich are ready. Thank you for visiting us today - enjoy your meal!

If transaction is NOT completed and MCP returned ""ADDED: Large Coffee ($3.99) | UPSELL_HINT: pastry"":
Excellent! I've added that Large Coffee ($3.99) to your order. Your total is now $3.99. Would you like one of our fresh pastries to go with that coffee? What else can I get for you?

REMEMBER: Once payment is processed, the interaction should END gracefully, not continue selling!

Provide your follow-up response now based on the actual MCP results:";
        }
        
        private static string CreateMcpPrompt(string conversationContext, Transaction transaction, string userInput)
        {
            return $@"You are a friendly, professional AI barista using MCP tools. The MCP layer is very smart - just call tools with what the customer said.

CONVERSATION HISTORY:
{conversationContext}

CURRENT TRANSACTION STATUS:
- Transaction ID: {transaction.Id}
- Items in cart: {(transaction.Lines.Any() ? string.Join(", ", transaction.Lines.Select(l => $"{l.ProductId} x{l.Quantity}")) : "none")}
- Current total: ${transaction.Total.ToDecimal():F2}
- Currency: {transaction.Currency}

CUSTOMER JUST SAID: ""{userInput}""

SIMPLE RULES:

WHEN CUSTOMER WANTS TO ORDER:
1. Respond conversationally first (""Perfect! Let me get that for you"")
2. For EACH item they mention, call 'add_item_to_transaction' with exactly what they said
   - If they say ""coffee and a bite to eat"" → call add_item_to_transaction twice: once with ""coffee"", once with ""bite to eat""
   - If they say ""large coffee"" → call add_item_to_transaction once with ""large coffee""
   - DON'T change their words (don't convert ""bite to eat"" to ""snack"")

WHEN CUSTOMER ASKS QUESTIONS:
1. Use 'get_popular_items' or 'search_products' for general information

WHEN CUSTOMER IS DONE:
1. Use 'process_payment'

KEY RULE: Always use 'add_item_to_transaction' when they want to order something. The MCP layer will handle everything else!

CONVERSATIONAL BEHAVIOR:
- Always respond enthusiastically first
- Use natural coffee shop language
- Trust the MCP layer completely

Respond as a skilled barista:";
        }

        private static void DisplayTransactionStatus(Transaction transaction)
        {
            var statusMsg = transaction.Lines.Any() 
                ? $"Current order: {string.Join(", ", transaction.Lines.Select(l => l.ProductId))} (${transaction.Total.ToDecimal():F2})"
                : "Your cart is empty";
            
            Console.WriteLine($"💰 {statusMsg}");
        }

        private static bool IsExitCommand(string userInput)
        {
            var leavingWords = new[] { "bye", "goodbye", "exit", "quit", "leave" };
            return leavingWords.Any(word => userInput.ToLower().Contains(word));
        }

        private static bool IsCompletionRequest(string userInput)
        {
            var finishedPhrases = new[] { "that's all", "that's everything", "ready to pay", "i'm done", "checkout" };
            return finishedPhrases.Any(phrase => userInput.ToLower().Contains(phrase));
        }

        private static void HandleExitCommand(string userInput, Transaction transaction)
        {
            if (!transaction.Lines.Any())
            {
                Console.WriteLine("🤖 AI Barista: Thanks for visiting! Come back anytime!");
            }
            else if (transaction.State != TransactionState.Completed)
            {
                Console.WriteLine("🤖 AI Barista: You have items in your cart. Your session will be saved for next time!");
            }
            else
            {
                Console.WriteLine("🤖 AI Barista: Thank you for your purchase! Have a great day!");
            }
        }

        private static void HandleTransactionCompletion(Transaction transaction)
        {
            Console.WriteLine();
            Console.WriteLine("🎉 Thank you! Your payment is complete and your order is ready!");
            Console.WriteLine("✨ Powered by Real POS Kernel with MCP!");
            Console.WriteLine($"📋 Final transaction state: {transaction.State}");
            Console.WriteLine($"💰 Total processed: ${transaction.Total.ToDecimal():F2}");
            Console.WriteLine("Have a wonderful day! ☕");
        }

        private static async Task RunLiveMcpSalesProcessAsync()
        {
            Console.WriteLine("🔥 LIVE MCP AI SALES PROCESS");
            Console.WriteLine("===========================");
            Console.WriteLine("Automated MCP-powered sales scenario demonstration");
            Console.WriteLine();

            var config = PosKernelConfiguration.Initialize();
            var aiConfig = new AiConfiguration(config);
            
            if (string.IsNullOrEmpty(aiConfig.ApiKey))
            {
                Console.WriteLine("❌ OpenAI API key not configured for live demo!");
                return;
            }

            Console.WriteLine($"✅ API key loaded (ends with: ...{aiConfig.ApiKey.Substring(Math.Max(0, aiConfig.ApiKey.Length - 4))})");

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            services.AddSingleton<IProductCatalogService, RestaurantProductCatalogService>();
            
            var mcpConfig = aiConfig.ToMcpConfiguration();
            services.AddSingleton(mcpConfig);
            services.AddSingleton<McpClient>();
            services.AddSingleton<PosToolsProvider>();
            
            using var serviceProvider = services.BuildServiceProvider();
            var mcpClient = serviceProvider.GetRequiredService<McpClient>();
            var toolsProvider = serviceProvider.GetRequiredService<PosToolsProvider>();
            var productCatalog = serviceProvider.GetRequiredService<IProductCatalogService>();
            
            await RunMcpSalesScenarioAsync(mcpClient, toolsProvider, productCatalog);
        }

        private static async Task RunMcpSalesScenarioAsync(McpClient mcpClient, PosToolsProvider toolsProvider, IProductCatalogService productCatalog)
        {
            Console.WriteLine("🎭 **LIVE MCP AI SALES SCENARIO**");
            Console.WriteLine("=================================");
            Console.WriteLine();

            var transaction = new Transaction
            {
                Id = TransactionId.New(),
                State = TransactionState.Building,
                Currency = "USD"
            };

            var availableTools = toolsProvider.GetAvailableTools();

            Console.WriteLine($"🏪 **Transaction Started**: {transaction.Id}");
            Console.WriteLine($"🛠️  **MCP Tools**: {availableTools.Count} available");
            Console.WriteLine();

            var customerScenarios = new[]
            {
                "I'd like a large coffee and something sweet to go with it",
                "What pairs well with coffee for breakfast?",
                "I'm trying to keep it under $8 total - what do you recommend?"
            };

            foreach (var customerRequest in customerScenarios)
            {
                Console.WriteLine($"👤 **Customer**: \"{customerRequest}\"");
                Console.Write("🤖 **MCP AI Barista**: ");

                var prompt = $@"You are an AI barista using MCP tools. Customer says: ""{customerRequest}""

Current transaction: {transaction.Id} (Total: ${transaction.Total.ToDecimal():F2})
Items: {(transaction.Lines.Any() ? string.Join(", ", transaction.Lines.Select(l => $"{l.ProductId} x{l.Quantity}")) : "None")}

Be conversational and enthusiastic like a real barista. Use MCP tools to help them, then follow up naturally.";

                var response = await mcpClient.CompleteWithToolsAsync(prompt, availableTools);
                Console.WriteLine(response.Content);

                if (response.ToolCalls.Any())
                {
                    Console.WriteLine($"  🛠️  **Executing {response.ToolCalls.Count} MCP tool(s)**");
                    
                    var toolResults = new List<string>();
                    foreach (var toolCall in response.ToolCalls)
                    {
                        var result = await toolsProvider.ExecuteToolAsync(toolCall, transaction);
                        Console.WriteLine($"  📊 {toolCall.FunctionName}: {result}");
                        toolResults.Add(result);
                    }

                    // Generate follow-up for demo
                    var followUpPrompt = $@"You just helped a customer by executing tools. Results: {string.Join("; ", toolResults)}
                    
Current transaction: {transaction.Lines.Count} items, ${transaction.Total.ToDecimal():F2}

Give a brief, enthusiastic follow-up response acknowledging what you added and the running total:";

                    try 
                    {
                        var followUp = await mcpClient.CompleteWithToolsAsync(followUpPrompt, new List<McpTool>());
                        Console.WriteLine($"🤖 {followUp.Content}");
                    }
                    catch
                    {
                        Console.WriteLine($"🤖 Perfect! Your order now has {transaction.Lines.Count} items. What else can I get for you?");
                    }
                }
                
                Console.WriteLine($"📊 **Transaction Update**: {transaction.Lines.Count} items, Total: ${transaction.Total.ToDecimal():F2}");
                Console.WriteLine();
            }

            // Display final results
            if (transaction.Lines.Any())
            {
                Console.WriteLine("🧾 **FINAL MCP TRANSACTION SUMMARY**");
                Console.WriteLine("===================================");
                Console.WriteLine($"Transaction ID: {transaction.Id}");
                Console.WriteLine($"Currency: {transaction.Currency}");
                Console.WriteLine($"State: {transaction.State}");
                Console.WriteLine($"Items ({transaction.Lines.Count}):");
                
                foreach (var line in transaction.Lines)
                {
                    Console.WriteLine($"  • {line.ProductId} - ${line.UnitPrice.ToDecimal():F2} x {line.Quantity} = ${line.Extended.ToDecimal():F2}");
                }
                
                Console.WriteLine($"**TOTAL: ${transaction.Total.ToDecimal():F2} {transaction.Currency}**");
                Console.WriteLine();
                Console.WriteLine("✅ **MCP AI SUCCESSFULLY COMPLETED SALES INTERACTION!**");
            }
            else
            {
                Console.WriteLine("ℹ️ No items were added via MCP tools in this demo scenario.");
            }
        }

        private static DemoType ParseDemoType(string[] args)
        {
            if (args.Length == 0)
            {
                return DemoType.Interactive;
            }

            var arg = args[0].ToLowerInvariant();
            return arg switch
            {
                "--interactive" or "--chat" or "-i" => DemoType.Interactive,
                "--live" or "--demo" or "-l" => DemoType.LiveDemo,
                "--help" or "-h" => DemoType.Help,
                _ => DemoType.Invalid
            };
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Usage: dotnet run [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --interactive, --chat, -i   💬 Interactive MCP AI Barista Chat (DEFAULT)");
            Console.WriteLine("  --live, --demo, -l          🔥 Live MCP AI Sales Process Demo");
            Console.WriteLine("  --help, -h                  Show this help message");
            Console.WriteLine();
            Console.WriteLine("💬 Interactive MCP AI Barista Chat:");
            Console.WriteLine("  • Model Context Protocol (MCP) structured tool calling");
            Console.WriteLine("  • Real POS Kernel transaction processing");
            Console.WriteLine("  • Live OpenAI integration with function calling");
            Console.WriteLine("  • Actual restaurant database with SQLite");
            Console.WriteLine("  • Professional AI-driven customer service");
            Console.WriteLine("  • Security-first design with kernel isolation");
            Console.WriteLine();
            Console.WriteLine("🔥 Live MCP AI Sales Process Demo:");
            Console.WriteLine("  • Automated MCP-powered sales scenario");
            Console.WriteLine("  • AI-driven product recommendations via structured tools");
            Console.WriteLine("  • Complete transaction lifecycle with MCP");
            Console.WriteLine("  • Demonstrates advanced AI customer interaction patterns");
            Console.WriteLine();
            Console.WriteLine("🚀 MCP Architecture Features:");
            Console.WriteLine("   ✅ Model Context Protocol (MCP) industry standard");
            Console.WriteLine("   ✅ Structured function calling (no brittle text parsing)");
            Console.WriteLine("   ✅ Real-time inventory queries via MCP tools");
            Console.WriteLine("   ✅ Type-safe AI-to-POS integration");
            Console.WriteLine("   ✅ Actual POS Kernel transaction engine");
            Console.WriteLine("   ✅ Professional error handling and logging");
            Console.WriteLine("   ✅ Configuration-driven setup following documented patterns");
            Console.WriteLine();
            Console.WriteLine("🔐 Security & Design Standards:");
            Console.WriteLine("   ✅ Security-first design with AI isolation from kernel");
            Console.WriteLine("   ✅ Proper dependency injection patterns");
            Console.WriteLine("   ✅ Documented error handling strategies");
            Console.WriteLine("   ✅ ISO 4217 currency code compliance");
            Console.WriteLine("   ✅ Kernel contract adherence");
            Console.WriteLine();
            Console.WriteLine("💡 Try the MCP AI barista:");
            Console.WriteLine("   dotnet run -- --interactive");
            Console.WriteLine();
            Console.WriteLine("📁 Configuration:");
            Console.WriteLine($"   Config directory: {PosKernelConfiguration.ConfigDirectory}");
            Console.WriteLine($"   Secrets file: {PosKernelConfiguration.SecretsFilePath}");
        }

        private enum DemoType
        {
            Invalid,
            Interactive,
            LiveDemo,
            Help
        }
    }
}
