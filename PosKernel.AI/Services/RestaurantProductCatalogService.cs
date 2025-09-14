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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PosKernel.Abstractions.Services;

namespace PosKernel.AI.Services
{
    /// <summary>
    /// Product catalog service that connects to the restaurant extension SQLite database.
    /// This replaces MockProductCatalogService with real restaurant data.
    /// </summary>
    public class RestaurantProductCatalogService : IProductCatalogService, IDisposable
    {
        private readonly ILogger<RestaurantProductCatalogService> _logger;
        private readonly SqliteConnection _database;
        private readonly string _databasePath;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the RestaurantProductCatalogService.
        /// </summary>
        /// <param name="logger">Logger instance for diagnostics and debugging.</param>
        public RestaurantProductCatalogService(ILogger<RestaurantProductCatalogService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Try multiple possible database paths
            var possiblePaths = new[]
            {
                // From PosKernel.AI directory
                Path.Combine("..", "data", "catalog", "restaurant_catalog.db"),
                // From solution root
                Path.Combine("data", "catalog", "restaurant_catalog.db"),
                // Absolute path to current location
                @"C:\Users\paul\source\repos\PosKernel\data\catalog\restaurant_catalog.db"
            };
            
            _databasePath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    _databasePath = path;
                    break;
                }
            }
            
            // If that doesn't exist, try the current directory structure
            if (string.IsNullOrEmpty(_databasePath))
            {
                _databasePath = Path.Combine("data", "catalog", "restaurant_catalog.db");
                throw new FileNotFoundException($"Restaurant database not found. Checked paths: {string.Join(", ", possiblePaths)}");
            }
            
            // Initialize database connection
            _database = new SqliteConnection($"Data Source={_databasePath}");
            _database.Open();
            
            _logger.LogInformation("Restaurant product catalog service initialized with database: {DatabasePath}", _databasePath);
        }

        /// <summary>
        /// Validates that a product exists in the catalog and is available for sale.
        /// </summary>
        /// <param name="productId">The product identifier to validate.</param>
        /// <param name="context">Context information for the product lookup operation.</param>
        /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
        /// <returns>A validation result containing product information if valid.</returns>
        public async Task<ProductValidationResult> ValidateProductAsync(
            string productId, 
            ProductLookupContext context,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            try
            {
                _logger.LogDebug("Validating product: {ProductId}", productId);

                using var command = _database.CreateCommand();
                command.CommandText = @"
                    SELECT p.sku, p.name, p.description, p.category_id, p.base_price_cents, 
                           p.is_active, p.requires_preparation, p.preparation_time_minutes, p.popularity_rank,
                           c.name as category_name, c.tax_category
                    FROM products p
                    JOIN categories c ON p.category_id = c.id
                    WHERE p.sku = @productId AND p.is_active = 1
                       OR EXISTS (SELECT 1 FROM product_identifiers pi 
                                 WHERE pi.product_sku = p.sku 
                                   AND pi.identifier_value = @productId)";
                command.Parameters.AddWithValue("@productId", productId);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return new ProductValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"Product not found: {productId}"
                    };
                }

                var product = new RestaurantProductInfo
                {
                    Sku = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    Category = reader.GetString(9),
                    BasePriceCents = reader.GetInt64(4),
                    IsActive = reader.GetBoolean(5),
                    Attributes = new Dictionary<string, object>
                    {
                        ["category_id"] = reader.GetString(3),
                        ["requires_preparation"] = reader.GetBoolean(6),
                        ["preparation_time_minutes"] = reader.GetInt32(7),
                        ["popularity_rank"] = reader.GetInt32(8),
                        ["tax_category"] = reader.GetString(10)
                    }
                };

                // Load additional attributes
                await LoadProductAttributesAsync(product, cancellationToken);

                return new ProductValidationResult
                {
                    IsValid = true,
                    EffectivePriceCents = product.BasePriceCents
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating product: {ProductId}", productId);
                return new ProductValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Internal error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Searches for products matching the specified search term.
        /// </summary>
        /// <param name="searchTerm">The term to search for in product names, descriptions, and SKUs.</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
        /// <returns>A list of products matching the search criteria.</returns>
        public async Task<IReadOnlyList<IProductInfo>> SearchProductsAsync(
            string searchTerm, 
            int maxResults = 50,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            try
            {
                _logger.LogDebug("Searching products: {SearchTerm}", searchTerm);

                using var command = _database.CreateCommand();
                command.CommandText = @"
                    SELECT p.sku, p.name, p.description, p.category_id, p.base_price_cents, 
                           p.is_active, p.requires_preparation, p.preparation_time_minutes, p.popularity_rank,
                           c.name as category_name, c.tax_category
                    FROM products p
                    JOIN categories c ON p.category_id = c.id
                    WHERE p.is_active = 1 
                      AND (p.name LIKE @searchTerm OR p.description LIKE @searchTerm OR p.sku LIKE @searchTerm)
                    ORDER BY p.popularity_rank
                    LIMIT @maxResults";
                command.Parameters.AddWithValue("@searchTerm", $"%{searchTerm}%");
                command.Parameters.AddWithValue("@maxResults", maxResults);

                var products = new List<IProductInfo>();
                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                
                while (await reader.ReadAsync(cancellationToken))
                {
                    var product = new RestaurantProductInfo
                    {
                        Sku = reader.GetString(0),
                        Name = reader.GetString(1),
                        Description = reader.GetString(2),
                        Category = reader.GetString(9),
                        BasePriceCents = reader.GetInt64(4),
                        IsActive = reader.GetBoolean(5),
                        Attributes = new Dictionary<string, object>
                        {
                            ["category_id"] = reader.GetString(3),
                            ["requires_preparation"] = reader.GetBoolean(6),
                            ["preparation_time_minutes"] = reader.GetInt32(7),
                            ["popularity_rank"] = reader.GetInt32(8),
                            ["tax_category"] = reader.GetString(10)
                        }
                    };
                    
                    await LoadProductAttributesAsync(product, cancellationToken);
                    products.Add(product);
                }

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products: {SearchTerm}", searchTerm);
                return new List<IProductInfo>();
            }
        }

        /// <summary>
        /// Gets the most popular items from the restaurant catalog.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
        /// <returns>A list of popular products ordered by popularity rank.</returns>
        public async Task<IReadOnlyList<IProductInfo>> GetPopularItemsAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            try
            {
                _logger.LogDebug("Getting popular items");

                using var command = _database.CreateCommand();
                command.CommandText = @"
                    SELECT p.sku, p.name, p.description, p.category_id, p.base_price_cents, 
                           p.is_active, p.requires_preparation, p.preparation_time_minutes, p.popularity_rank,
                           c.name as category_name, c.tax_category
                    FROM products p
                    JOIN categories c ON p.category_id = c.id
                    WHERE p.is_active = 1 
                    ORDER BY p.popularity_rank
                    LIMIT 10";

                var products = new List<IProductInfo>();
                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                
                while (await reader.ReadAsync(cancellationToken))
                {
                    var product = new RestaurantProductInfo
                    {
                        Sku = reader.GetString(0),
                        Name = reader.GetString(1),
                        Description = reader.GetString(2),
                        Category = reader.GetString(9),
                        BasePriceCents = reader.GetInt64(4),
                        IsActive = reader.GetBoolean(5),
                        Attributes = new Dictionary<string, object>
                        {
                            ["category_id"] = reader.GetString(3),
                            ["requires_preparation"] = reader.GetBoolean(6),
                            ["preparation_time_minutes"] = reader.GetInt32(7),
                            ["popularity_rank"] = reader.GetInt32(8),
                            ["tax_category"] = reader.GetString(10)
                        }
                    };
                    
                    await LoadProductAttributesAsync(product, cancellationToken);
                    products.Add(product);
                }

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting popular items");
                return new List<IProductInfo>();
            }
        }

        /// <summary>
        /// Gets all products in a specific category.
        /// </summary>
        /// <param name="category">The category name or ID to filter by.</param>
        /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
        /// <returns>A list of products in the specified category.</returns>
        public async Task<IReadOnlyList<IProductInfo>> GetProductsByCategoryAsync(
            string category, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            try
            {
                _logger.LogDebug("Getting products by category: {Category}", category);

                using var command = _database.CreateCommand();
                command.CommandText = @"
                    SELECT p.sku, p.name, p.description, p.category_id, p.base_price_cents, 
                           p.is_active, p.requires_preparation, p.preparation_time_minutes, p.popularity_rank,
                           c.name as category_name, c.tax_category
                    FROM products p
                    JOIN categories c ON p.category_id = c.id
                    WHERE p.is_active = 1 AND (p.category_id = @category OR c.name = @category)
                    ORDER BY p.popularity_rank";
                command.Parameters.AddWithValue("@category", category);

                var products = new List<IProductInfo>();
                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                
                while (await reader.ReadAsync(cancellationToken))
                {
                    var product = new RestaurantProductInfo
                    {
                        Sku = reader.GetString(0),
                        Name = reader.GetString(1),
                        Description = reader.GetString(2),
                        Category = reader.GetString(9),
                        BasePriceCents = reader.GetInt64(4),
                        IsActive = reader.GetBoolean(5),
                        Attributes = new Dictionary<string, object>
                        {
                            ["category_id"] = reader.GetString(3),
                            ["requires_preparation"] = reader.GetBoolean(6),
                            ["preparation_time_minutes"] = reader.GetInt32(7),
                            ["popularity_rank"] = reader.GetInt32(8),
                            ["tax_category"] = reader.GetString(10)
                        }
                    };
                    
                    await LoadProductAttributesAsync(product, cancellationToken);
                    products.Add(product);
                }

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products by category: {Category}", category);
                return new List<IProductInfo>();
            }
        }

        /// <summary>
        /// Gets all available product categories from the restaurant catalog.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation if needed.</param>
        /// <returns>A list of active category names.</returns>
        public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            try
            {
                _logger.LogDebug("Getting categories");

                using var command = _database.CreateCommand();
                command.CommandText = @"
                    SELECT name FROM categories 
                    WHERE is_active = 1 
                    ORDER BY display_order, name";

                var categories = new List<string>();
                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                
                while (await reader.ReadAsync(cancellationToken))
                {
                    categories.Add(reader.GetString(0));
                }

                return categories;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories");
                return new List<string>();
            }
        }

        private async Task LoadProductAttributesAsync(RestaurantProductInfo product, CancellationToken cancellationToken)
        {
            // Load allergens
            using var allergenCommand = _database.CreateCommand();
            allergenCommand.CommandText = @"
                SELECT a.name, pa.contamination_risk
                FROM product_allergens pa
                JOIN allergens a ON pa.allergen_id = a.id
                WHERE pa.product_sku = @sku AND a.is_active = 1";
            allergenCommand.Parameters.AddWithValue("@sku", product.Sku);

            var allergens = new List<string>();
            using var allergenReader = await allergenCommand.ExecuteReaderAsync(cancellationToken);
            while (await allergenReader.ReadAsync(cancellationToken))
            {
                allergens.Add(allergenReader.GetString(0));
            }
            product.Attributes["allergens"] = allergens;

            // Load specifications
            using var specCommand = _database.CreateCommand();
            specCommand.CommandText = @"
                SELECT spec_key, spec_value, spec_type
                FROM product_specifications
                WHERE product_sku = @sku";
            specCommand.Parameters.AddWithValue("@sku", product.Sku);

            var specifications = new Dictionary<string, object>();
            using var specReader = await specCommand.ExecuteReaderAsync(cancellationToken);
            while (await specReader.ReadAsync(cancellationToken))
            {
                var key = specReader.GetString(0);
                var value = specReader.GetString(1);
                var type = specReader.GetString(2);

                specifications[key] = type switch
                {
                    "number" => double.TryParse(value, out var num) ? num : value,
                    "boolean" => bool.TryParse(value, out var flag) ? flag : value,
                    _ => value
                };
            }
            product.Attributes["specifications"] = specifications;

            // Load upsell suggestions
            using var upsellCommand = _database.CreateCommand();
            upsellCommand.CommandText = @"
                SELECT p.name
                FROM product_upsells u
                JOIN products p ON u.suggested_sku = p.sku
                WHERE u.product_sku = @sku AND p.is_active = 1
                ORDER BY u.priority";
            upsellCommand.Parameters.AddWithValue("@sku", product.Sku);

            var upsells = new List<string>();
            using var upsellReader = await upsellCommand.ExecuteReaderAsync(cancellationToken);
            while (await upsellReader.ReadAsync(cancellationToken))
            {
                upsells.Add(upsellReader.GetString(0));
            }
            product.Attributes["upsell_suggestions"] = upsells;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RestaurantProductCatalogService));
            }
        }

        /// <summary>
        /// Disposes the database connection and releases all resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _database?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Product information from the restaurant extension database.
    /// </summary>
    public class RestaurantProductInfo : IProductInfo
    {
        /// <summary>
        /// Gets or sets the product SKU (stock keeping unit).
        /// </summary>
        public string Sku { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the product display name.
        /// </summary>
        public string Name { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the product description.
        /// </summary>
        public string Description { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the product category name.
        /// </summary>
        public string Category { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the base price in cents.
        /// </summary>
        public long BasePriceCents { get; set; }
        
        /// <summary>
        /// Gets or sets whether the product is currently active and available for sale.
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Gets or sets additional product attributes specific to the restaurant domain.
        /// </summary>
        public Dictionary<string, object> Attributes { get; set; } = new();
        
        IReadOnlyDictionary<string, object> IProductInfo.Attributes => Attributes;
    }
}
