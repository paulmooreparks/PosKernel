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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PosKernel.Abstractions.Services;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Mock implementation of IProductCatalogService for testing when domain extensions aren't available.
    /// This provides a fallback that demonstrates the interface without requiring external services.
    /// </summary>
    public class MockProductCatalogService : IProductCatalogService
    {
        private readonly ILogger<MockProductCatalogService> _logger;
        private readonly List<IProductInfo> _mockProducts;

        public MockProductCatalogService(ILogger<MockProductCatalogService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mockProducts = CreateMockProducts();
        }

        public async Task<ProductValidationResult> ValidateProductAsync(
            string productId, 
            ProductLookupContext context,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken); // Simulate network delay

            var product = _mockProducts.FirstOrDefault(p => 
                p.Sku.Equals(productId, StringComparison.OrdinalIgnoreCase));

            if (product == null)
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
                Product = product,
                EffectivePriceCents = product.BasePriceCents
            };
        }

        public async Task<IReadOnlyList<IProductInfo>> SearchProductsAsync(
            string searchTerm, 
            int maxResults = 50,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(20, cancellationToken); // Simulate search delay

            var results = _mockProducts.Where(p =>
                p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                p.Sku.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            ).Take(maxResults).ToList();

            return results;
        }

        public async Task<IReadOnlyList<IProductInfo>> GetPopularItemsAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);

            // Return products ordered by popularity (using a mock popularity score)
            var popularItems = _mockProducts
                .Where(p => p.Attributes.ContainsKey("popularity_rank"))
                .OrderBy(p => (int)p.Attributes["popularity_rank"])
                .Take(10)
                .ToList();

            return popularItems;
        }

        public async Task<IReadOnlyList<IProductInfo>> GetProductsByCategoryAsync(
            string category, 
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(15, cancellationToken);

            var categoryProducts = _mockProducts
                .Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return categoryProducts;
        }

        public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(5, cancellationToken);

            var categories = _mockProducts
                .Select(p => p.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            return categories;
        }

        private List<IProductInfo> CreateMockProducts()
        {
            return new List<IProductInfo>
            {
                new GenericProductInfo
                {
                    Sku = "COFFEE_LG",
                    Name = "Large Coffee",
                    Description = "Classic drip coffee, 16oz",
                    Category = "hot_beverages",
                    BasePriceCents = 399,
                    IsActive = true,
                    Attributes = new Dictionary<string, object>
                    {
                        ["popularity_rank"] = 1,
                        ["specifications"] = new Dictionary<string, object>
                        {
                            ["size"] = "16oz",
                            ["caffeine_content"] = "high",
                            ["calories"] = 10
                        },
                        ["upsell_suggestions"] = new List<string> { "Blueberry Muffin", "Plain Croissant" },
                        ["allergens"] = new List<string>(),
                        ["requires_preparation"] = true,
                        ["preparation_time_minutes"] = 2
                    }
                },

                new GenericProductInfo
                {
                    Sku = "LATTE",
                    Name = "Caffe Latte",
                    Description = "Espresso with steamed milk",
                    Category = "hot_beverages",
                    BasePriceCents = 499,
                    IsActive = true,
                    Attributes = new Dictionary<string, object>
                    {
                        ["popularity_rank"] = 2,
                        ["specifications"] = new Dictionary<string, object>
                        {
                            ["size"] = "12oz",
                            ["caffeine_content"] = "medium",
                            ["calories"] = 150
                        },
                        ["allergens"] = new List<string> { "Dairy" },
                        ["customizations"] = new Dictionary<string, object>
                        {
                            ["milk_options"] = new List<string> { "whole", "2%", "skim", "oat", "almond", "soy" },
                            ["syrup_options"] = new List<string> { "vanilla", "caramel", "hazelnut" }
                        },
                        ["requires_preparation"] = true,
                        ["preparation_time_minutes"] = 3
                    }
                },

                new GenericProductInfo
                {
                    Sku = "MUFFIN_BLUEBERRY",
                    Name = "Blueberry Muffin",
                    Description = "Fresh blueberry muffin with Maine blueberries",
                    Category = "bakery",
                    BasePriceCents = 249,
                    IsActive = true,
                    Attributes = new Dictionary<string, object>
                    {
                        ["popularity_rank"] = 3,
                        ["specifications"] = new Dictionary<string, object>
                        {
                            ["weight"] = "120g",
                            ["fresh_baked"] = true,
                            ["vegetarian_friendly"] = true
                        },
                        ["allergens"] = new List<string> { "Gluten", "Eggs", "Dairy" },
                        ["requires_preparation"] = false,
                        ["preparation_time_minutes"] = 0
                    }
                },

                new GenericProductInfo
                {
                    Sku = "BREAKFAST_SANDWICH",
                    Name = "Breakfast Sandwich",
                    Description = "Egg, aged cheddar, and Canadian bacon on English muffin",
                    Category = "breakfast",
                    BasePriceCents = 649,
                    IsActive = true,
                    Attributes = new Dictionary<string, object>
                    {
                        ["popularity_rank"] = 4,
                        ["specifications"] = new Dictionary<string, object>
                        {
                            ["weight"] = "180g",
                            ["served_hot"] = true,
                            ["protein_level"] = "high"
                        },
                        ["allergens"] = new List<string> { "Gluten", "Eggs", "Dairy" },
                        ["customizations"] = new Dictionary<string, object>
                        {
                            ["egg_options"] = new List<string> { "scrambled", "over_easy" },
                            ["meat_options"] = new List<string> { "canadian_bacon", "sausage" },
                            ["cheese_options"] = new List<string> { "cheddar", "swiss" }
                        },
                        ["requires_preparation"] = true,
                        ["preparation_time_minutes"] = 3
                    }
                },

                new GenericProductInfo
                {
                    Sku = "COFFEE_SM",
                    Name = "Small Coffee",
                    Description = "Classic drip coffee, 8oz",
                    Category = "hot_beverages",
                    BasePriceCents = 299,
                    IsActive = true,
                    Attributes = new Dictionary<string, object>
                    {
                        ["popularity_rank"] = 5,
                        ["specifications"] = new Dictionary<string, object>
                        {
                            ["size"] = "8oz",
                            ["caffeine_content"] = "medium",
                            ["calories"] = 5
                        },
                        ["allergens"] = new List<string>(),
                        ["requires_preparation"] = true,
                        ["preparation_time_minutes"] = 2
                    }
                }
            };
        }
    }
}
