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

        /// <summary>
        /// Gets localized product name for display.
        /// </summary>
        public async Task<string> GetLocalizedProductNameAsync(string productId, string localeCode, CancellationToken cancellationToken = default)
        {
            // For demo purposes, just return the product name (no localization)
            var validation = await ValidateProductAsync(productId, new ProductLookupContext(), cancellationToken);
            return validation.Product?.Name ?? productId;
        }

        /// <summary>
        /// Gets localized product description.
        /// </summary>
        public async Task<string> GetLocalizedProductDescriptionAsync(string productId, string localeCode, CancellationToken cancellationToken = default)
        {
            // For demo purposes, just return the product description (no localization)
            var validation = await ValidateProductAsync(productId, new ProductLookupContext(), cancellationToken);
            return validation.Product?.Description ?? "";
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
                new KopitiamProductInfo { Sku = "KOPI", Name = "Kopi", Description = "Traditional local coffee with condensed milk", Category = "Beverages", BasePrice = 1.80m, IsActive = true },
                new KopitiamProductInfo { Sku = "KOPI_C", Name = "Kopi C", Description = "Local coffee with evaporated milk and sugar", Category = "Beverages", BasePrice = 1.80m, IsActive = true },
                new KopitiamProductInfo { Sku = "KOPI_O", Name = "Kopi O", Description = "Black local coffee with sugar only", Category = "Beverages", BasePrice = 1.60m, IsActive = true },

                // Teh variations
                new KopitiamProductInfo { Sku = "TEH", Name = "Teh", Description = "Local tea with condensed milk", Category = "Beverages", BasePrice = 1.60m, IsActive = true },
                new KopitiamProductInfo { Sku = "TEH_C", Name = "Teh C", Description = "Local tea with evaporated milk and sugar", Category = "Beverages", BasePrice = 1.60m, IsActive = true },
                new KopitiamProductInfo { Sku = "TEH_O", Name = "Teh O", Description = "Black local tea with sugar only", Category = "Beverages", BasePrice = 1.40m, IsActive = true },

                // Food
                new KopitiamProductInfo { Sku = "KAYA_TOAST", Name = "Kaya Toast", Description = "Toasted bread with kaya and butter", Category = "Food", BasePrice = 2.80m, IsActive = true },
                new KopitiamProductInfo { Sku = "KAYA_BUTTER_TOAST", Name = "Kaya Butter Toast", Description = "Toasted bread with kaya and extra butter", Category = "Food", BasePrice = 3.00m, IsActive = true },
                new KopitiamProductInfo { Sku = "KAYA_TOAST_SET", Name = "Kaya Toast Set", Description = "Kaya toast with soft boiled eggs and kopi/teh", Category = "Food", BasePrice = 4.80m, IsActive = true },
                new KopitiamProductInfo { Sku = "SOFT_BOILED_EGG", Name = "Soft Boiled Egg", Description = "Traditional soft boiled eggs with soy sauce", Category = "Food", BasePrice = 1.20m, IsActive = true },
                new KopitiamProductInfo { Sku = "FRENCH_TOAST", Name = "French Toast", Description = "Thick toast with kaya filling", Category = "Food", BasePrice = 3.50m, IsActive = true }
            };
        }
    }

    /// <summary>
    /// Product information for kopitiam items with Singapore-specific attributes.
    /// </summary>
    public class KopitiamProductInfo : IProductInfo
    {
        /// <summary>
        /// Gets or sets the product SKU.
        /// </summary>
        public string Sku { get; set; } = "";

        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the product description.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets the product category.
        /// </summary>
        public string Category { get; set; } = "";

        /// <summary>
        /// Gets or sets the base price as decimal.
        /// </summary>
        public decimal BasePrice { get; set; }

        /// <summary>
        /// Gets the base price in cents (legacy support).
        /// </summary>
        public long BasePriceCents => (long)(BasePrice * 100);

        /// <summary>
        /// Gets or sets whether the product is active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets whether this product requires preparation.
        /// </summary>
        public bool RequiresPreparation { get; set; } = false;

        /// <summary>
        /// Gets or sets the preparation time in minutes.
        /// </summary>
        public int PreparationTimeMinutes { get; set; } = 0;

        /// <summary>
        /// Gets or sets the localization key for the product name.
        /// </summary>
        public string? NameLocalizationKey { get; set; }

        /// <summary>
        /// Gets or sets the localization key for the product description.
        /// </summary>
        public string? DescriptionLocalizationKey { get; set; }

        /// <summary>
        /// Gets or sets additional attributes.
        /// </summary>
        public IReadOnlyDictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();
    }
}
