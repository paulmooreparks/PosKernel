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

using PosKernel.AI.Catalog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PosKernel.AI.Examples
{
    /// <summary>
    /// Product catalog showcase demonstrating the comprehensive inventory 
    /// available for sale in POS Kernel with AI-enhanced sales scenarios.
    /// </summary>
    class ProductShowcaseDemo
    {
        /// <summary>
        /// Runs the interactive product showcase demonstration.
        /// </summary>
        public static async Task RunProductShowcaseAsync()
        {
            Console.WriteLine("🏪 POS Kernel - Complete Product Catalog & AI Sales Showcase");
            Console.WriteLine("===========================================================");
            Console.WriteLine();
            
            var demo = new ProductShowcaseDemo();
            await demo.ShowcaseProductCatalogAsync();
        }

        public async Task ShowcaseProductCatalogAsync()
        {
            Console.WriteLine("📋 COMPLETE PRODUCT INVENTORY");
            Console.WriteLine("=============================");
            Console.WriteLine();
            ShowCompleteCatalog();
            
            Console.WriteLine();
            Console.WriteLine("🤖 AI-POWERED SALES SCENARIOS");
            Console.WriteLine("=============================");
            await DemonstrateAiSalesScenariosAsync();
            
            Console.WriteLine();
            Console.WriteLine("📊 BUSINESS ANALYTICS");
            Console.WriteLine("====================");
            ShowBusinessAnalytics();
        }

        private void ShowCompleteCatalog()
        {
            var categories = ProductCatalog.Products.GroupBy(p => p.Category).OrderBy(g => g.Key);
            decimal totalInventoryValue = ProductCatalog.Products.Sum(p => p.Price);
            
            Console.WriteLine($"📦 Total Products: {ProductCatalog.Products.Count}");
            Console.WriteLine($"🏷️ Categories: {categories.Count()}");
            Console.WriteLine($"💰 Average Price: ${ProductCatalog.Products.Average(p => p.Price):F2}");
            Console.WriteLine($"🎯 Price Range: ${ProductCatalog.Products.Min(p => p.Price):F2} - ${ProductCatalog.Products.Max(p => p.Price):F2}");
            Console.WriteLine();

            foreach (var category in categories)
            {
                Console.WriteLine($"📂 {category.Key.ToUpper()} ({category.Count()} items)");
                Console.WriteLine($"   Price range: ${category.Min(p => p.Price):F2} - ${category.Max(p => p.Price):F2}");
                Console.WriteLine();
                
                foreach (var product in category.OrderBy(p => p.Price))
                {
                    var preparationIcon = product.RequiresPreparation ? "👨‍🍳" : "📦";
                    Console.WriteLine($"   {preparationIcon} {product.Name}");
                    Console.WriteLine($"      SKU: {product.Sku} | Price: ${product.Price:F2}");
                    Console.WriteLine($"      {product.Description}");
                    Console.WriteLine();
                }
            }
        }

        private async Task DemonstrateAiSalesScenariosAsync()
        {
            var scenarios = new[]
            {
                new
                {
                    CustomerType = "Morning Commuter",
                    Request = "I need something quick for my morning commute",
                    AiResponse = "Based on your needs, I recommend a Large Coffee ($3.99) and a Breakfast Sandwich ($6.49) for a quick, portable breakfast.",
                    Items = new[] { "COFFEE_LG", "BREAKFAST_SANDWICH" }
                },
                new
                {
                    CustomerType = "Office Manager",
                    Request = "I'm ordering for a team meeting - 8 people, mix of coffee and pastries",
                    AiResponse = "For your team meeting, I suggest: 4 Large Coffees, 2 Lattes, 2 Cappuccinos, and an assortment of 8 pastries including muffins and croissants.",
                    Items = new[] { "COFFEE_LG", "LATTE", "CAPPUCCINO", "MUFFIN_BLUEBERRY", "CROISSANT_PLAIN" }
                },
                new
                {
                    CustomerType = "Health-Conscious Customer", 
                    Request = "I want something healthy but filling",
                    AiResponse = "Perfect! I recommend our Avocado Toast ($8.99) with a Green Tea ($2.49), plus a Greek Yogurt Parfait ($5.99) for a nutritious meal.",
                    Items = new[] { "TOAST_AVOCADO", "TEA_GREEN", "YOGURT_PARFAIT" }
                },
                new
                {
                    CustomerType = "Coffee Enthusiast",
                    Request = "I want to try your best coffee and take some beans home",
                    AiResponse = "Excellent choice! Try our signature Caffe Mocha ($5.49) and take home our premium Espresso Beans 1lb ($14.99) with a Travel Tumbler ($15.99).",
                    Items = new[] { "MOCHA", "COFFEE_BEANS_ESPRESSO", "TUMBLER" }
                },
                new
                {
                    CustomerType = "Student", 
                    Request = "I need affordable food and drink for studying",
                    AiResponse = "Great for studying! A Medium Coffee ($3.49) for focus, a Bagel with cream cheese ($1.99), and a Chocolate Chip Cookie ($1.99) for energy.",
                    Items = new[] { "COFFEE_MD", "BAGEL_PLAIN", "COOKIE_CHOC" }
                }
            };

            foreach (var scenario in scenarios)
            {
                Console.WriteLine($"👤 Customer Type: {scenario.CustomerType}");
                Console.WriteLine($"💬 Customer Says: \"{scenario.Request}\"");
                Console.WriteLine();
                Console.WriteLine($"🤖 AI Assistant Response:");
                Console.WriteLine($"   {scenario.AiResponse}");
                Console.WriteLine();
                
                Console.WriteLine("📋 Recommended Items:");
                decimal total = 0m;
                foreach (var sku in scenario.Items)
                {
                    var product = ProductCatalog.Products.FirstOrDefault(p => p.Sku == sku);
                    if (product != null)
                    {
                        Console.WriteLine($"   ✓ {product.Name} - ${product.Price:F2}");
                        Console.WriteLine($"     {product.Description}");
                        total += product.Price;
                    }
                }
                Console.WriteLine($"   💰 Estimated Total: ${total:F2}");
                Console.WriteLine();
                
                // Simulate AI thinking time
                await Task.Delay(200);
                Console.WriteLine("─────────────────────────────────────────────────────────");
                Console.WriteLine();
            }
        }

        private void ShowBusinessAnalytics()
        {
            Console.WriteLine("📈 Popular Items (Based on AI Analysis):");
            var popular = ProductCatalog.GetPopularItems().Take(10);
            
            int rank = 1;
            foreach (var item in popular)
            {
                var dailySales = rank switch
                {
                    1 => 47,  // Large Coffee - most popular
                    2 => 32,  // Latte  
                    3 => 28,  // Blueberry Muffin
                    4 => 23,  // Breakfast Sandwich
                    5 => 19,  // Turkey Club
                    _ => 15 - rank
                };
                
                var revenue = dailySales * item.Price;
                Console.WriteLine($"   {rank,2}. {item.Name,-25} | {dailySales,2} sales/day | ${revenue,6:F2} revenue");
                rank++;
            }
            
            Console.WriteLine();
            Console.WriteLine("💡 AI Business Insights:");
            Console.WriteLine("   • Hot beverages account for 45% of total sales volume");
            Console.WriteLine("   • Breakfast items peak between 7-9 AM (38% of daily sales)");
            Console.WriteLine("   • Average transaction value: $8.73");
            Console.WriteLine("   • Customers who buy specialty drinks spend 34% more on average");
            Console.WriteLine("   • Bakery items have highest profit margins (68% average)");
            Console.WriteLine();
            
            Console.WriteLine("🚨 Fraud Detection Patterns:");
            Console.WriteLine("   • High-value gift card purchases ($500+) trigger additional verification");
            Console.WriteLine("   • Bulk retail purchases (10+ items) require business justification");
            Console.WriteLine("   • Large catering orders (50+ items) need advance confirmation");
            Console.WriteLine("   • Multiple high-value items from single customer flagged for review");
            Console.WriteLine();
            
            Console.WriteLine("🎯 Smart Upselling Opportunities:");
            Console.WriteLine("   • Coffee + pastry bundles increase basket size by 23%");
            Console.WriteLine("   • Lunch customers respond well to beverage suggestions (+18% uptake)");
            Console.WriteLine("   • Morning customers likely to purchase tomorrow's coffee beans (+12%)");
            Console.WriteLine("   • Specialty drink customers are premium product buyers (+34% conversion)");
            Console.WriteLine();
            
            Console.WriteLine("📦 Inventory Optimization:");
            var lowStock = new[] { "COFFEE_LG", "MUFFIN_BLUEBERRY", "SANDWICH_TURKEY" };
            var overStock = new[] { "TEA_HERBAL", "JUICE_APPLE" };
            
            Console.WriteLine("   🔴 Low Stock Alerts:");
            foreach (var sku in lowStock)
            {
                var product = ProductCatalog.Products.First(p => p.Sku == sku);
                Console.WriteLine($"      • {product.Name} - Reorder recommended");
            }
            
            Console.WriteLine("   🟡 Overstock Items:");
            foreach (var sku in overStock)
            {
                var product = ProductCatalog.Products.First(p => p.Sku == sku);
                Console.WriteLine($"      • {product.Name} - Consider promotion");
            }
            
            Console.WriteLine();
            Console.WriteLine("✨ AI-Powered Features Summary:");
            Console.WriteLine("   🗣️  Natural Language Order Processing");
            Console.WriteLine("   🔍 Real-time Fraud Detection");
            Console.WriteLine("   💡 Intelligent Upselling Suggestions");
            Console.WriteLine("   📊 Predictive Business Analytics");
            Console.WriteLine("   📈 Dynamic Inventory Management");
            Console.WriteLine("   🎯 Customer Behavior Analysis");
            Console.WriteLine();
            Console.WriteLine("🎉 POS Kernel AI: Making every sale smarter and more secure!");
        }
    }
}
