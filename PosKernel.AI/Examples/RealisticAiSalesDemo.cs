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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PosKernel.Abstractions.Services;

namespace PosKernel.AI.Examples
{
    /// <summary>
    /// Realistic AI-driven sales demonstration that interacts with real domain extensions.
    /// Showcases AI capabilities while using production-ready product catalog services.
    /// </summary>
    public class RealisticAiSalesDemo
    {
        private readonly IProductCatalogService _productCatalogService;
        private readonly ILogger<RealisticAiSalesDemo> _logger;

        public RealisticAiSalesDemo(IProductCatalogService productCatalogService, ILogger<RealisticAiSalesDemo> logger)
        {
            _productCatalogService = productCatalogService ?? throw new ArgumentNullException(nameof(productCatalogService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Demonstrates AI-driven customer interactions using real product data.
        /// </summary>
        public async Task RunRealisticDemoAsync()
        {
            _logger.LogInformation("🤖 Starting Realistic AI Sales Demo with Real Domain Extensions");
            Console.WriteLine("═══════════════════════════════════════════");
            Console.WriteLine("🤖 AI-Powered Coffee Shop Sales Assistant");
            Console.WriteLine("   Using Real Restaurant Domain Extension");
            Console.WriteLine("═══════════════════════════════════════════");

            try
            {
                await DemonstrateCustomerInteractionAsync();
                await DemonstrateUpsellingSuggestionsAsync();
                await DemonstrateAllergEnHandlingAsync();
                await DemonstratePopularItemsAsync();
                await DemonstrateProductSearchAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during AI sales demo");
                Console.WriteLine($"❌ Demo error: {ex.Message}");
            }

            Console.WriteLine("\n✅ Realistic AI Sales Demo Complete!");
        }

        private async Task DemonstrateCustomerInteractionAsync()
        {
            Console.WriteLine("\n🎭 Customer Interaction Simulation");
            Console.WriteLine("Customer: \"I'd like a large coffee please\"");

            var context = new ProductLookupContext
            {
                StoreId = "STORE_COFFEE_001",
                TerminalId = "AI_DEMO_TERMINAL",
                OperatorId = "AI_ASSISTANT"
            };

            var validationResult = await _productCatalogService.ValidateProductAsync("COFFEE_LG", context);
            
            if (validationResult.IsValid && validationResult.Product != null)
            {
                var product = validationResult.Product;
                var price = validationResult.EffectivePriceCents / 100.0;
                
                Console.WriteLine($"🤖 AI: \"Great choice! One {product.Name} for ${price:F2}\"");
                
                // Check for attributes
                if (product.Attributes.TryGetValue("specifications", out var specsObj) && specsObj is Dictionary<string, object> specs)
                {
                    if (specs.TryGetValue("caffeine_content", out var caffeine))
                        Console.WriteLine($"🤖 AI: \"This has {caffeine} caffeine content - perfect for your energy needs!\"");
                }

                // Check for upsell suggestions
                if (product.Attributes.TryGetValue("upsell_suggestions", out var upsellObj) && upsellObj is List<string> upsells)
                {
                    if (upsells.Any())
                    {
                        var suggestion = upsells.First();
                        Console.WriteLine($"🤖 AI: \"Would you like to add a {suggestion}? They pair perfectly together!\"");
                    }
                }
            }
            else
            {
                Console.WriteLine($"🤖 AI: \"I'm sorry, that item isn't available right now. Error: {validationResult.ErrorMessage}\"");
            }
        }

        private async Task DemonstrateUpsellingSuggestionsAsync()
        {
            Console.WriteLine("\n💰 Smart Upselling Demonstration");
            Console.WriteLine("Customer adds a basic coffee to cart...");

            var popularItems = await _productCatalogService.GetPopularItemsAsync();
            
            if (popularItems.Any())
            {
                Console.WriteLine("🤖 AI: \"Based on what other customers enjoy, I recommend:\"");
                
                foreach (var item in popularItems.Take(3))
                {
                    var price = item.BasePriceCents / 100.0;
                    Console.WriteLine($"   • {item.Name} - ${price:F2}");
                    
                    // Show preparation info if available
                    if (item.Attributes.TryGetValue("requires_preparation", out var prepObj) && prepObj is bool requiresPrep && requiresPrep)
                    {
                        if (item.Attributes.TryGetValue("preparation_time_minutes", out var timeObj) && timeObj is int prepTime)
                            Console.WriteLine($"     (Fresh made to order - {prepTime} min)");
                    }
                }
            }
            else
            {
                Console.WriteLine("🤖 AI: \"Let me check our recommendations...\"");
                Console.WriteLine("⚠️  Unable to load popular items from restaurant extension");
            }
        }

        private async Task DemonstrateAllergEnHandlingAsync()
        {
            Console.WriteLine("\n🚨 Allergen Safety Demonstration");
            Console.WriteLine("Customer: \"I have a dairy allergy\"");

            var context = new ProductLookupContext
            {
                StoreId = "STORE_COFFEE_001",
                TerminalId = "AI_DEMO_TERMINAL", 
                OperatorId = "AI_ASSISTANT"
            };

            var latteResult = await _productCatalogService.ValidateProductAsync("LATTE", context);
            
            if (latteResult.IsValid && latteResult.Product != null)
            {
                var product = latteResult.Product;
                
                if (product.Attributes.TryGetValue("allergens", out var allergensObj) && allergensObj is List<string> allergens)
                {
                    if (allergens.Contains("Dairy", StringComparer.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"🤖 AI: \"I need to warn you - our {product.Name} contains dairy.\"");
                        Console.WriteLine("🤖 AI: \"Let me suggest dairy-free alternatives...\"");
                        
                        // Search for coffee alternatives
                        var coffeeAlternatives = await _productCatalogService.SearchProductsAsync("coffee");
                        var safeCoffees = coffeeAlternatives.Where(p => 
                        {
                            if (p.Attributes.TryGetValue("allergens", out var pAllergens) && pAllergens is List<string> pAllergenList)
                                return !pAllergenList.Contains("Dairy", StringComparer.OrdinalIgnoreCase);
                            return true; // Assume safe if no allergen data
                        }).Take(2);

                        foreach (var safeCoffee in safeCoffees)
                        {
                            var price = safeCoffee.BasePriceCents / 100.0;
                            Console.WriteLine($"   ✅ {safeCoffee.Name} - ${price:F2} (Dairy-free)");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"🤖 AI: \"Good news! Our {product.Name} is dairy-free.\"");
                }
            }
        }

        private async Task DemonstratePopularItemsAsync()
        {
            Console.WriteLine("\n⭐ Popular Items Showcase");
            Console.WriteLine("Customer: \"What do you recommend?\"");

            var popularItems = await _productCatalogService.GetPopularItemsAsync();
            
            if (popularItems.Any())
            {
                Console.WriteLine("🤖 AI: \"Here are our most popular items today:\"");
                
                foreach (var item in popularItems.Take(5))
                {
                    var price = item.BasePriceCents / 100.0;
                    var rank = item.Attributes.TryGetValue("popularity_rank", out var rankObj) && rankObj is int r ? r : 999;
                    
                    Console.WriteLine($"   #{rank}: {item.Name} - ${price:F2}");
                    Console.WriteLine($"        {item.Description}");
                }
            }
            else
            {
                Console.WriteLine("🤖 AI: \"Let me check what's popular today...\"");
                Console.WriteLine("⚠️  Unable to load popular items - restaurant extension may not be running");
            }
        }

        private async Task DemonstrateProductSearchAsync()
        {
            Console.WriteLine("\n🔍 Intelligent Product Search");
            Console.WriteLine("Customer: \"Do you have anything with blueberries?\"");

            var searchResults = await _productCatalogService.SearchProductsAsync("blueberry");
            
            if (searchResults.Any())
            {
                Console.WriteLine("🤖 AI: \"Yes! Here's what we have with blueberries:\"");
                
                foreach (var item in searchResults)
                {
                    var price = item.BasePriceCents / 100.0;
                    Console.WriteLine($"   • {item.Name} - ${price:F2}");
                    Console.WriteLine($"     {item.Description}");
                    
                    // Show nutritional info if available
                    if (item.Attributes.TryGetValue("specifications", out var specsObj) && specsObj is Dictionary<string, object> specs)
                    {
                        if (specs.TryGetValue("fresh_baked", out var freshObj) && freshObj is bool fresh && fresh)
                            Console.WriteLine("     🥐 Fresh baked daily!");
                    }
                }
            }
            else
            {
                Console.WriteLine("🤖 AI: \"I'm sorry, we don't currently have any items with blueberries available.\"");
            }
        }
    }
}
