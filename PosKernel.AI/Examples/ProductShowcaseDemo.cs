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
        }

        private Task DemonstrateAiSalesScenariosAsync()
        {
            // ARCHITECTURAL FIX: Remove all hardcoded currency formatting
            return Task.FromException(new InvalidOperationException(
                "DESIGN DEFICIENCY: AI sales scenarios demo requires ICurrencyFormattingService. " +
                "Cannot hardcode $ symbols or :F2 formatting assumptions. " +
                "Inject proper currency formatting services to display prices correctly."));
        }

        private void ShowBusinessAnalytics()
        {
            // ARCHITECTURAL FIX: Remove all hardcoded currency formatting
            throw new InvalidOperationException(
                "DESIGN DEFICIENCY: Business analytics demo requires ICurrencyFormattingService. " +
                "Cannot hardcode $ symbols or :F2 formatting assumptions. " +
                "Client must provide proper currency formatting services to display prices correctly.");
        }

        // NOTE: Original implementation removed due to hardcoded currency formatting violations
        // TODO: Reimplement with proper ICurrencyFormattingService injection
    }
}
