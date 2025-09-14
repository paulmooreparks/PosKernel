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
using System.Threading.Tasks;

namespace PosKernel.Core.Catalog
{
    /// <summary>
    /// Core product domain object representing an item available for sale.
    /// This is the proper location for product data - in the Core business logic layer.
    /// </summary>
    public class Product
    {
        /// <summary>
        /// Gets or sets the unique Stock Keeping Unit identifier.
        /// </summary>
        public string Sku { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the product display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the product category for classification.
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the base price in the store's default currency.
        /// </summary>
        public decimal BasePrice { get; set; }

        /// <summary>
        /// Gets or sets the detailed product description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this product requires kitchen preparation.
        /// </summary>
        public bool RequiresPreparation { get; set; }

        /// <summary>
        /// Gets or sets the tax category identifier for this product.
        /// </summary>
        public string TaxCategory { get; set; } = "STANDARD";

        /// <summary>
        /// Gets or sets a value indicating whether this product is currently active for sale.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets the timestamp when this product was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the timestamp when this product was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets additional metadata for this product (e.g., nutritional info, allergens).
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Product search and filtering criteria.
    /// </summary>
    public class ProductSearchCriteria
    {
        /// <summary>
        /// Gets or sets the search term to match against SKU, name, or description.
        /// </summary>
        public string? SearchTerm { get; set; }

        /// <summary>
        /// Gets or sets the category to filter by.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Gets or sets the minimum price filter.
        /// </summary>
        public decimal? MinPrice { get; set; }

        /// <summary>
        /// Gets or sets the maximum price filter.
        /// </summary>
        public decimal? MaxPrice { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to include inactive products.
        /// </summary>
        public bool IncludeInactive { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum number of results to return.
        /// </summary>
        public int MaxResults { get; set; } = 100;
    }

    /// <summary>
    /// Repository interface for product data access.
    /// This abstracts the data storage mechanism (SQLite, SQL Server, etc.).
    /// </summary>
    public interface IProductRepository
    {
        /// <summary>
        /// Gets a product by its SKU.
        /// </summary>
        /// <param name="sku">The product SKU.</param>
        /// <returns>The product if found, otherwise null.</returns>
        Task<Product?> GetBySkuAsync(string sku);

        /// <summary>
        /// Searches for products based on the specified criteria.
        /// </summary>
        /// <param name="criteria">The search criteria.</param>
        /// <returns>A list of matching products.</returns>
        Task<IReadOnlyList<Product>> SearchAsync(ProductSearchCriteria criteria);

        /// <summary>
        /// Gets products by category.
        /// </summary>
        /// <param name="category">The product category.</param>
        /// <returns>A list of products in the specified category.</returns>
        Task<IReadOnlyList<Product>> GetByCategoryAsync(string category);

        /// <summary>
        /// Gets all active products.
        /// </summary>
        /// <returns>A list of all active products.</returns>
        Task<IReadOnlyList<Product>> GetActiveProductsAsync();

        /// <summary>
        /// Adds or updates a product in the catalog.
        /// </summary>
        /// <param name="product">The product to save.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SaveAsync(Product product);

        /// <summary>
        /// Removes a product from the catalog.
        /// </summary>
        /// <param name="sku">The SKU of the product to remove.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteAsync(string sku);

        /// <summary>
        /// Gets the total count of products matching the criteria.
        /// </summary>
        /// <param name="criteria">The search criteria.</param>
        /// <returns>The total count of matching products.</returns>
        Task<int> CountAsync(ProductSearchCriteria criteria);
    }

    /// <summary>
    /// Service interface for product catalog operations.
    /// This is where business logic for product management resides.
    /// </summary>
    public interface IProductCatalogService
    {
        /// <summary>
        /// Validates that a product exists and is available for sale.
        /// </summary>
        /// <param name="sku">The product SKU.</param>
        /// <returns>True if the product is valid for sale, otherwise false.</returns>
        Task<bool> IsValidForSaleAsync(string sku);

        /// <summary>
        /// Gets the current effective price for a product, including any promotional pricing.
        /// </summary>
        /// <param name="sku">The product SKU.</param>
        /// <param name="storeId">The store identifier for location-specific pricing.</param>
        /// <param name="customerId">Optional customer identifier for personalized pricing.</param>
        /// <returns>The effective price for the product.</returns>
        Task<decimal?> GetEffectivePriceAsync(string sku, string storeId, string? customerId = null);

        /// <summary>
        /// Suggests related or complementary products for upselling.
        /// </summary>
        /// <param name="skus">The SKUs of products currently in the transaction.</param>
        /// <returns>A list of suggested products for upselling.</returns>
        Task<IReadOnlyList<Product>> GetUpsellSuggestionsAsync(IEnumerable<string> skus);

        /// <summary>
        /// Gets products that are currently popular or trending.
        /// </summary>
        /// <param name="storeId">The store identifier.</param>
        /// <param name="count">The number of popular items to return.</param>
        /// <returns>A list of popular products.</returns>
        Task<IReadOnlyList<Product>> GetPopularProductsAsync(string storeId, int count = 10);

        /// <summary>
        /// Bulk imports products from an external data source.
        /// </summary>
        /// <param name="products">The products to import.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ImportProductsAsync(IEnumerable<Product> products);
    }
}
