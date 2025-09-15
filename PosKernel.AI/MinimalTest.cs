using PosKernel.AI.Tools;
using PosKernel.AI.Services;
using PosKernel.Abstractions.Services;

namespace PosKernel.AI
{
    /// <summary>
    /// Minimal test to isolate and fix core translation/search issues.
    /// No AI, no MCP complexity - just test the basic functionality.
    /// </summary>
    public static class MinimalTest
    {
        /// <summary>
        /// Runs core translation and search tests without AI complexity.
        /// </summary>
        public static async Task RunCoreTranslationTestAsync()
        {
            Console.WriteLine("🔧 MINIMAL CORE TRANSLATION TEST");
            Console.WriteLine("================================");
            
            // Step 1: Test cultural translation
            Console.WriteLine("\n1️⃣ Testing Cultural Translation:");
            var testInputs = new[] 
            {
                "roti kaya",
                "teh si kosong", 
                "kopi si",
                "teh c kosong"
            };
            
            foreach (var input in testInputs)
            {
                var translated = TestCulturalTranslation(input);
                Console.WriteLine($"  '{input}' → '{translated}'");
            }
            
            // Step 2: Test product search
            Console.WriteLine("\n2️⃣ Testing Product Search:");
            var catalog = new KopitiamProductCatalogService();
            
            foreach (var input in testInputs)
            {
                var translated = TestCulturalTranslation(input);
                var products = await catalog.SearchProductsAsync(translated);
                Console.WriteLine($"  '{translated}' → Found {products.Count} products");
                
                foreach (var product in products)
                {
                    Console.WriteLine($"    - {product.Name} (${product.BasePriceCents / 100.0:F2})");
                }
            }
            
            // Step 3: Test exact matching logic
            Console.WriteLine("\n3️⃣ Testing Exact Matching Logic:");
            var exactTests = new[]
            {
                ("teh c kosong", "Teh C"),
                ("kaya toast", "Kaya Toast"),
                ("teh c", "Teh C")
            };
            
            foreach (var (search, expected) in exactTests)
            {
                var products = await catalog.SearchProductsAsync(search);
                var expectedProduct = products.FirstOrDefault(p => p.Name.Equals(expected, StringComparison.OrdinalIgnoreCase));
                
                if (expectedProduct != null)
                {
                    var isExactMatch = TestIsExactMatch(search, expectedProduct);
                    var isSpecificMatch = TestIsSpecificMatch(search, expectedProduct);
                    
                    Console.WriteLine($"  '{search}' → '{expected}':");
                    Console.WriteLine($"    Found: ✅");
                    Console.WriteLine($"    Exact Match: {(isExactMatch ? "✅" : "❌")}");
                    Console.WriteLine($"    Specific Match: {(isSpecificMatch ? "✅" : "❌")}");
                }
                else
                {
                    Console.WriteLine($"  '{search}' → '{expected}': ❌ NOT FOUND");
                }
            }
        }
        
        private static string TestCulturalTranslation(string input)
        {
            // Copy the logic from PosToolsProvider to test it in isolation
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }
            
            var translated = input.ToLowerInvariant().Trim();
            
            // Cultural translations
            translated = translated.Replace("teh si", "teh c");
            translated = translated.Replace("kopi si", "kopi c"); 
            translated = translated.Replace("roti kaya", "kaya toast");
            translated = translated.Replace("roti", "toast");
            
            return translated;
        }
        
        private static bool TestIsExactMatch(string searchTerm, IProductInfo product)
        {
            var normalizedSearch = searchTerm.ToLowerInvariant().Trim();
            var normalizedProductName = product.Name.ToLowerInvariant().Trim();
            
            // Remove preparation instructions for matching
            var baseSearchTerm = RemovePreparationInstructions(normalizedSearch);
            var baseProductName = RemovePreparationInstructions(normalizedProductName);
            
            return baseSearchTerm == baseProductName || 
                   normalizedSearch == product.Sku.ToLowerInvariant() ||
                   baseSearchTerm == baseProductName;
        }
        
        private static bool TestIsSpecificMatch(string searchTerm, IProductInfo product)
        {
            var normalizedSearch = searchTerm.ToLowerInvariant().Trim();
            var normalizedProductName = product.Name.ToLowerInvariant().Trim();
            
            // When customer says "kaya toast" after disambiguation, they mean the basic one
            if (normalizedSearch == "kaya toast" && normalizedProductName == "kaya toast")
            {
                return true;
            }
            
            // When customer says "teh c kosong", they're being specific
            if (normalizedSearch.Contains("teh c") && normalizedProductName.Contains("teh c"))
            {
                return true;
            }
            
            return false;
        }
        
        private static string RemovePreparationInstructions(string term)
        {
            var baseTerm = term;
            
            // Remove preparation modifiers to get base product
            var modifiers = new[] { "kosong", "siew dai", "gao", "poh", "peng" };
            
            foreach (var modifier in modifiers)
            {
                baseTerm = baseTerm.Replace($" {modifier}", "").Replace($"{modifier} ", "");
            }
            
            return baseTerm.Trim();
        }
    }
}
