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

using PosKernel.Abstractions.Services;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Traditional Singaporean kopitiam product catalog service.
    /// Provides authentic local coffee, tea, and food options with proper local terminology.
    /// </summary>
    public class KopitiamProductCatalogService : IProductCatalogService
    {
        private readonly List<IProductInfo> _products;

        /// <summary>
        /// Initializes a new instance of the KopitiamProductCatalogService.
        /// </summary>
        public KopitiamProductCatalogService()
        {
            _products = InitializeKopitiamProducts();
        }

        /// <summary>
        /// Validates that a product exists in the catalog and is available for sale.
        /// </summary>
        /// <param name="productId">The product identifier to validate.</param>
        /// <param name="context">Context information for the product lookup operation.</param>
        /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
        /// <returns>A validation result containing product information if valid.</returns>
        public async Task<ProductValidationResult> ValidateProductAsync(string productId, ProductLookupContext context, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken); // Simulate async operation
            
            var product = _products.FirstOrDefault(p => 
                p.Sku.Equals(productId, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals(productId, StringComparison.OrdinalIgnoreCase));

            if (product == null || !product.IsActive)
            {
                return new ProductValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Product not found: {productId}"
                };
            }

            return new ProductValidationResult
            {
                IsValid = true,
                EffectivePriceCents = product.BasePriceCents
            };
        }

        /// <summary>
        /// Gets popular/recommended items from the kopitiam menu.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
        /// <returns>A list of popular kopitiam products.</returns>
        public async Task<IReadOnlyList<IProductInfo>> GetPopularItemsAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken); // Simulate async operation
            
            // Return most popular kopitiam items
            return _products.Where(p => p.IsActive && IsPopularItem(p.Name)).ToList();
        }

        /// <summary>
        /// Gets all products in a specific category.
        /// </summary>
        /// <param name="category">The category name to filter by.</param>
        /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
        /// <returns>A list of products in the specified category.</returns>
        public async Task<IReadOnlyList<IProductInfo>> GetProductsByCategoryAsync(string category, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            
            return _products.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase) && p.IsActive).ToList();
        }

        /// <summary>
        /// Gets all available product categories.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
        /// <returns>A list of category names.</returns>
        public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            
            return _products.Select(p => p.Category).Distinct().ToList();
        }

        /// <summary>
        /// Searches for products matching the specified search term.
        /// </summary>
        /// <param name="searchTerm">The term to search for in product names, descriptions, and SKUs.</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
        /// <returns>A list of products matching the search criteria.</returns>
        public async Task<IReadOnlyList<IProductInfo>> SearchProductsAsync(string searchTerm, int maxResults = 10, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            
            var normalizedSearch = searchTerm.ToLowerInvariant().Trim();
            
            var results = _products.Where(p => p.IsActive && (
                p.Name.ToLowerInvariant().Contains(normalizedSearch) ||
                p.Description.ToLowerInvariant().Contains(normalizedSearch) ||
                p.Sku.ToLowerInvariant().Contains(normalizedSearch) ||
                MatchesKopitiamTerminology(p, normalizedSearch)
            )).Take(maxResults).ToList();

            return results;
        }

        private bool MatchesKopitiamTerminology(IProductInfo product, string searchTerm)
        {
            // Handle common kopitiam terminology mapping
            var kopiTerms = new Dictionary<string, string[]>
            {
                { "kopi", new[] { "coffee", "kopi" } },
                { "teh", new[] { "tea", "teh" } },
                { "c", new[] { "evaporated milk", "carnation" } },
                { "o", new[] { "black", "kosong milk" } },
                { "kosong", new[] { "no sugar", "plain" } },
                { "siew dai", new[] { "less sugar", "little sweet" } },
                { "gao", new[] { "thick", "strong", "concentrated" } },
                { "poh", new[] { "weak", "diluted" } },
                { "peng", new[] { "ice", "cold", "iced" } }
            };

            var productLower = product.Name.ToLowerInvariant();
            
            foreach (var mapping in kopiTerms)
            {
                if (searchTerm.Contains(mapping.Key))
                {
                    foreach (var synonym in mapping.Value)
                    {
                        if (productLower.Contains(synonym))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private bool IsPopularItem(string productName)
        {
            var popularItems = new[] { "kopi", "kopi c", "teh", "teh c", "kaya toast", "soft boiled egg", "mee siam" };
            return popularItems.Any(item => productName.ToLowerInvariant().Contains(item.ToLowerInvariant()));
        }

        private List<IProductInfo> InitializeKopitiamProducts()
        {
            return new List<IProductInfo>
            {
                // BASE Kopi (Coffee) Products - simple base products only
                new KopitiamProductInfo("KOPI", "Kopi", "Traditional local coffee with condensed milk", "Beverages", 180, true),
                new KopitiamProductInfo("KOPI_C", "Kopi C", "Local coffee with evaporated milk and sugar", "Beverages", 180, true),
                new KopitiamProductInfo("KOPI_O", "Kopi O", "Black local coffee with sugar only", "Beverages", 160, true),

                // BASE Teh (Tea) Products  
                new KopitiamProductInfo("TEH", "Teh", "Local tea with condensed milk", "Beverages", 160, true),
                new KopitiamProductInfo("TEH_C", "Teh C", "Local tea with evaporated milk and sugar", "Beverages", 160, true),
                new KopitiamProductInfo("TEH_O", "Teh O", "Black local tea with sugar only", "Beverages", 140, true),

                // Traditional Breakfast Items
                new KopitiamProductInfo("KAYA_TOAST", "Kaya Toast", "Toasted bread with kaya and butter", "Food", 280, true),
                new KopitiamProductInfo("KAYA_BUTTER_TOAST", "Kaya Butter Toast", "Toasted bread with kaya and extra butter", "Food", 300, true),
                new KopitiamProductInfo("KAYA_TOAST_SET", "Kaya Toast Set", "Kaya toast with soft boiled eggs and kopi/teh", "Food", 480, true),
                new KopitiamProductInfo("SOFT_BOILED_EGG", "Soft Boiled Egg", "Traditional soft boiled eggs with soy sauce", "Food", 120, true),
                new KopitiamProductInfo("FRENCH_TOAST", "French Toast", "Thick toast with kaya filling", "Food", 350, true)
            };
        }
    }

    /// <summary>
    /// Product information for kopitiam items with Singapore-specific attributes.
    /// </summary>
    public class KopitiamProductInfo : IProductInfo
    {
        /// <summary>
        /// Gets the product SKU (stock keeping unit).
        /// </summary>
        public string Sku { get; }
        
        /// <summary>
        /// Gets the product display name.
        /// </summary>
        public string Name { get; }
        
        /// <summary>
        /// Gets the product description.
        /// </summary>
        public string Description { get; }
        
        /// <summary>
        /// Gets the product category name.
        /// </summary>
        public string Category { get; }
        
        /// <summary>
        /// Gets the base price in cents (Singapore cents).
        /// </summary>
        public long BasePriceCents { get; }
        
        /// <summary>
        /// Gets whether the product is currently active and available for sale.
        /// </summary>
        public bool IsActive { get; }
        
        /// <summary>
        /// Gets additional product attributes specific to kopitiam items.
        /// </summary>
        public IReadOnlyDictionary<string, object> Attributes { get; }

        /// <summary>
        /// Initializes a new instance of the KopitiamProductInfo.
        /// </summary>
        /// <param name="sku">The product SKU.</param>
        /// <param name="name">The product name.</param>
        /// <param name="description">The product description.</param>
        /// <param name="category">The product category.</param>
        /// <param name="basePriceCents">The base price in Singapore cents.</param>
        /// <param name="isActive">Whether the product is active.</param>
        public KopitiamProductInfo(string sku, string name, string description, string category, long basePriceCents, bool isActive)
        {
            Sku = sku;
            Name = name;
            Description = description;
            Category = category;
            BasePriceCents = basePriceCents;
            IsActive = isActive;
            Attributes = new Dictionary<string, object>
            {
                ["is_traditional"] = true,
                ["origin"] = "Singapore",
                ["currency"] = "SGD"
            };
        }
    }
}
