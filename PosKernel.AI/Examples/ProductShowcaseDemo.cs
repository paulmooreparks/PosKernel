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
            
            // ARCHITECTURAL FIX: Remove hardcoded currency formatting - this is a demo class
            // In production, currency formatting would come from ICurrencyFormattingService
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: ProductShowcaseDemo requires ICurrencyFormattingService to display prices. " +
                "Cannot hardcode currency symbols or decimal formatting. " +
                "Inject ICurrencyFormattingService and StoreConfig to provide proper currency formatting.");

            foreach (var category in categories)
            {
                Console.WriteLine($"📂 {category.Key.ToUpper()} ({category.Count()} items)");
                
                foreach (var product in category.OrderBy(p => p.Price))
                {
                    var preparationIcon = product.RequiresPreparation ? "👨‍🍳" : "📦";
                    Console.WriteLine($"   {preparationIcon} {product.Name}");
                    Console.WriteLine($"      SKU: {product.Sku} | Price: [Currency service required]");
                    Console.WriteLine($"      {product.Description}");
                    Console.WriteLine();
                }
            }
        }

        private async Task DemonstrateAiSalesScenariosAsync()
        {
            // ARCHITECTURAL FIX: Remove all hardcoded currency formatting
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: AI sales scenarios demo requires ICurrencyFormattingService. " +
                "Cannot hardcode $ symbols or :F2 formatting assumptions. " +
                "Inject proper currency formatting services to display prices correctly.");
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
