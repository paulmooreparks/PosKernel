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
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PosKernel.Abstractions.Services;

namespace PosKernel.Extensions.Restaurant.Services
{
    /// <summary>
    /// Enhanced product catalog service with modifications and localization support.
    /// Supports Singapore kopitiam with cultural intelligence and multi-language features.
    /// </summary>
    public class EnhancedProductCatalogService : IProductCatalogService, IDisposable
    {
        private readonly string _databasePath;
        private readonly ILogger<EnhancedProductCatalogService> _logger;
        private readonly IModificationService _modificationService;
        private readonly ILocalizationService _localizationService;
        private bool _disposed = false;
        private bool _databaseInitialized = false;

        /// <summary>
        /// Initializes a new instance of the EnhancedProductCatalogService.
        /// </summary>
        public EnhancedProductCatalogService(
            ILogger<EnhancedProductCatalogService> logger,
            IModificationService modificationService,
            ILocalizationService localizationService,
            string? databasePath = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _modificationService = modificationService ?? throw new ArgumentNullException(nameof(modificationService));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
            _databasePath = databasePath ?? Path.Combine("data", "catalog", "kopitiam_catalog.db");
        }

        /// <summary>
        /// Validates that a product is available for sale and returns detailed information.
        /// </summary>
        public async Task<PosKernel.Abstractions.Services.ProductValidationResult> ValidateProductAsync(
            string productId,
            PosKernel.Abstractions.Services.ProductLookupContext context,
            CancellationToken cancellationToken = default)
        {
            await EnsureDatabaseInitializedAsync(cancellationToken);

            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync(cancellationToken);

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT p.sku, p.name, p.description, p.category_id, c.name as category_name, 
                           p.base_price, p.is_active, p.requires_preparation, p.preparation_time_minutes,
                           p.name_localization_key, p.description_localization_key
                    FROM products p 
                    JOIN categories c ON p.category_id = c.id
                    WHERE p.sku = @productId OR p.name LIKE '%' || @productId || '%'
                    LIMIT 1";
                command.Parameters.AddWithValue("@productId", productId);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var sku = reader.GetString(0);
                    var name = reader.GetString(1);
                    var description = reader.GetString(2);
                    var categoryId = reader.GetString(3);
                    var categoryName = reader.GetString(4);
                    var basePrice = reader.GetDecimal(5);
                    var isActive = reader.GetBoolean(6);
                    var requiresPreparation = reader.GetBoolean(7);
                    var preparationTimeMinutes = reader.GetInt32(8);
                    var nameLocalizationKey = reader.IsDBNull(9) ? null : reader.GetString(9);
                    var descriptionLocalizationKey = reader.IsDBNull(10) ? null : reader.GetString(10);

                    // Get localized name and description if requested
                    var localizedName = name;
                    var localizedDescription = description;
                    
                    if (!string.IsNullOrEmpty(context.PreferredLocale) && context.PreferredLocale != "en")
                    {
                        if (!string.IsNullOrEmpty(nameLocalizationKey))
                        {
                            localizedName = await _localizationService.GetLocalizedTextAsync(nameLocalizationKey, context.PreferredLocale, cancellationToken);
                        }
                        if (!string.IsNullOrEmpty(descriptionLocalizationKey))
                        {
                            localizedDescription = await _localizationService.GetLocalizedTextAsync(descriptionLocalizationKey, context.PreferredLocale, cancellationToken);
                        }
                    }

                    // Get available modification groups
                    var modificationGroups = await _modificationService.GetAvailableModificationGroupsAsync(sku, categoryId, cancellationToken);
                    var modificationGroupIds = modificationGroups.Select(g => g.Id).ToArray();

                    var productInfo = new EnhancedProductInfo
                    {
                        Sku = sku,
                        Name = localizedName,
                        Description = localizedDescription,
                        Category = categoryName,
                        BasePrice = basePrice,
                        IsActive = isActive,
                        RequiresPreparation = requiresPreparation,
                        PreparationTimeMinutes = preparationTimeMinutes,
                        NameLocalizationKey = nameLocalizationKey,
                        DescriptionLocalizationKey = descriptionLocalizationKey,
                        Attributes = new Dictionary<string, object>
                        {
                            ["category_id"] = categoryId,
                            ["original_name"] = name,
                            ["original_description"] = description,
                            ["preferred_locale"] = context.PreferredLocale
                        }
                    };

                    return new PosKernel.Abstractions.Services.ProductValidationResult
                    {
                        IsValid = isActive,
                        Product = productInfo,
                        EffectivePrice = basePrice,
                        AvailableModificationGroups = modificationGroupIds,
                        ErrorMessage = isActive ? "" : "Product is not active"
                    };
                }

                return new PosKernel.Abstractions.Services.ProductValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Product not found: {productId}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating product {ProductId}", productId);
                return new PosKernel.Abstractions.Services.ProductValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Validation error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Searches for products matching the given criteria.
        /// </summary>
        public async Task<IReadOnlyList<IProductInfo>> SearchProductsAsync(
            string searchTerm,
            int maxResults = 50,
            CancellationToken cancellationToken = default)
        {
            await EnsureDatabaseInitializedAsync(cancellationToken);

            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync(cancellationToken);

                using var command = connection.CreateCommand();

                if (string.IsNullOrEmpty(searchTerm))
                {
                    // Return all products
                    command.CommandText = @"
                        SELECT p.sku, p.name, p.description, p.category_id, c.name as category_name, 
                               p.base_price, p.is_active, p.requires_preparation, p.preparation_time_minutes,
                               p.name_localization_key, p.description_localization_key
                        FROM products p 
                        JOIN categories c ON p.category_id = c.id
                        WHERE p.is_active = 1
                        ORDER BY p.popularity_rank, p.name
                        LIMIT @maxResults";
                }
                else
                {
                    // Search by term
                    command.CommandText = @"
                        SELECT p.sku, p.name, p.description, p.category_id, c.name as category_name, 
                               p.base_price, p.is_active, p.requires_preparation, p.preparation_time_minutes,
                               p.name_localization_key, p.description_localization_key
                        FROM products p 
                        JOIN categories c ON p.category_id = c.id
                        WHERE p.is_active = 1 
                        AND (p.sku LIKE '%' || @searchTerm || '%' 
                             OR p.name LIKE '%' || @searchTerm || '%' 
                             OR p.description LIKE '%' || @searchTerm || '%'
                             OR c.name LIKE '%' || @searchTerm || '%')
                        ORDER BY 
                            CASE 
                                WHEN p.name LIKE @searchTerm || '%' THEN 1
                                WHEN p.name LIKE '%' || @searchTerm || '%' THEN 2
                                ELSE 3
                            END,
                            p.popularity_rank,
                            p.name
                        LIMIT @maxResults";
                    command.Parameters.AddWithValue("@searchTerm", searchTerm);
                }

                command.Parameters.AddWithValue("@maxResults", maxResults);

                var products = new List<IProductInfo>();
                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var productInfo = new EnhancedProductInfo
                    {
                        Sku = reader.GetString(0),
                        Name = reader.GetString(1),
                        Description = reader.GetString(2),
                        Category = reader.GetString(4), // category name
                        BasePrice = reader.GetDecimal(5),
                        IsActive = reader.GetBoolean(6),
                        RequiresPreparation = reader.GetBoolean(7),
                        PreparationTimeMinutes = reader.GetInt32(8),
                        NameLocalizationKey = reader.IsDBNull(9) ? null : reader.GetString(9),
                        DescriptionLocalizationKey = reader.IsDBNull(10) ? null : reader.GetString(10),
                        Attributes = new Dictionary<string, object>
                        {
                            ["category_id"] = reader.GetString(3)
                        }
                    };
                    products.Add(productInfo);
                }

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products with term '{SearchTerm}'", searchTerm);
                return Array.Empty<IProductInfo>();
            }
        }

        /// <summary>
        /// Gets popular/recommended products for upselling and suggestions.
        /// </summary>
        public async Task<IReadOnlyList<IProductInfo>> GetPopularItemsAsync(CancellationToken cancellationToken = default)
        {
            await EnsureDatabaseInitializedAsync(cancellationToken);

            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync(cancellationToken);

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT p.sku, p.name, p.description, p.category_id, c.name as category_name, 
                           p.base_price, p.is_active, p.requires_preparation, p.preparation_time_minutes,
                           p.name_localization_key, p.description_localization_key
                    FROM products p 
                    JOIN categories c ON p.category_id = c.id
                    WHERE p.is_active = 1
                    ORDER BY p.popularity_rank, c.display_order, p.name
                    LIMIT 10";

                var products = new List<IProductInfo>();
                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var productInfo = new EnhancedProductInfo
                    {
                        Sku = reader.GetString(0),
                        Name = reader.GetString(1),
                        Description = reader.GetString(2),
                        Category = reader.GetString(4), // category name
                        BasePrice = reader.GetDecimal(5),
                        IsActive = reader.GetBoolean(6),
                        RequiresPreparation = reader.GetBoolean(7),
                        PreparationTimeMinutes = reader.GetInt32(8),
                        NameLocalizationKey = reader.IsDBNull(9) ? null : reader.GetString(9),
                        DescriptionLocalizationKey = reader.IsDBNull(10) ? null : reader.GetString(10),
                        Attributes = new Dictionary<string, object>
                        {
                            ["category_id"] = reader.GetString(3),
                            ["is_popular"] = true
                        }
                    };
                    products.Add(productInfo);
                }

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting popular items");
                return Array.Empty<IProductInfo>();
            }
        }

        /// <summary>
        /// Gets all products in a specific category.
        /// </summary>
        public async Task<IReadOnlyList<IProductInfo>> GetProductsByCategoryAsync(
            string category,
            CancellationToken cancellationToken = default)
        {
            await EnsureDatabaseInitializedAsync(cancellationToken);

            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync(cancellationToken);

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT p.sku, p.name, p.description, p.category_id, c.name as category_name, 
                           p.base_price, p.is_active, p.requires_preparation, p.preparation_time_minutes,
                           p.name_localization_key, p.description_localization_key
                    FROM products p 
                    JOIN categories c ON p.category_id = c.id
                    WHERE p.is_active = 1 AND (c.name = @category OR c.id = @category)
                    ORDER BY p.popularity_rank, p.name";
                command.Parameters.AddWithValue("@category", category);

                var products = new List<IProductInfo>();
                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var productInfo = new EnhancedProductInfo
                    {
                        Sku = reader.GetString(0),
                        Name = reader.GetString(1),
                        Description = reader.GetString(2),
                        Category = reader.GetString(4), // category name
                        BasePrice = reader.GetDecimal(5),
                        IsActive = reader.GetBoolean(6),
                        RequiresPreparation = reader.GetBoolean(7),
                        PreparationTimeMinutes = reader.GetInt32(8),
                        NameLocalizationKey = reader.IsDBNull(9) ? null : reader.GetString(9),
                        DescriptionLocalizationKey = reader.IsDBNull(10) ? null : reader.GetString(10),
                        Attributes = new Dictionary<string, object>
                        {
                            ["category_id"] = reader.GetString(3)
                        }
                    };
                    products.Add(productInfo);
                }

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products for category '{Category}'", category);
                return Array.Empty<IProductInfo>();
            }
        }

        /// <summary>
        /// Gets available product categories.
        /// </summary>
        public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            await EnsureDatabaseInitializedAsync(cancellationToken);

            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync(cancellationToken);

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT name
                    FROM categories 
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
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Gets localized product name for display.
        /// </summary>
        public async Task<string> GetLocalizedProductNameAsync(
            string productId,
            string localeCode,
            CancellationToken cancellationToken = default)
        {
            var localizationService = _localizationService as RestaurantLocalizationService;
            if (localizationService != null)
            {
                return await localizationService.GetLocalizedProductNameAsync(productId, localeCode, cancellationToken);
            }

            // Fallback implementation
            var product = await ValidateProductAsync(productId, new ProductLookupContext { PreferredLocale = localeCode }, cancellationToken);
            return product.Product?.Name ?? productId;
        }

        /// <summary>
        /// Gets localized product description.
        /// </summary>
        public async Task<string> GetLocalizedProductDescriptionAsync(
            string productId,
            string localeCode,
            CancellationToken cancellationToken = default)
        {
            var localizationService = _localizationService as RestaurantLocalizationService;
            if (localizationService != null)
            {
                return await localizationService.GetLocalizedProductDescriptionAsync(productId, localeCode, cancellationToken);
            }

            // Fallback implementation
            var product = await ValidateProductAsync(productId, new ProductLookupContext { PreferredLocale = localeCode }, cancellationToken);
            return product.Product?.Description ?? "";
        }

        private async Task EnsureDatabaseInitializedAsync(CancellationToken cancellationToken = default)
        {
            if (_databaseInitialized)
            {
                return;
            }

            await InitializeDatabaseAsync(cancellationToken);
            _databaseInitialized = true;
        }

        private async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
        {
            // Ensure database directory exists
            var dbDirectory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
                _logger.LogInformation("Created database directory: {Directory}", dbDirectory);
            }

            // Check if database exists and is initialized
            var dbExists = File.Exists(_databasePath);

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            if (!dbExists || await IsEmptyDatabaseAsync(connection, cancellationToken))
            {
                _logger.LogInformation("Initializing kopitiam catalog database: {DatabasePath}", _databasePath);

                // Execute modifications schema first
                var modSchemaPath = Path.Combine("data", "catalog", "modifications_schema.sql");
                if (File.Exists(modSchemaPath))
                {
                    var schema = await File.ReadAllTextAsync(modSchemaPath, cancellationToken);
                    await ExecuteSqlAsync(connection, schema, cancellationToken);
                    _logger.LogInformation("Modifications schema created successfully");
                }

                // Execute localization migration
                var migrationPath = Path.Combine("data", "catalog", "localization_migration.sql");
                if (File.Exists(migrationPath))
                {
                    var migration = await File.ReadAllTextAsync(migrationPath, cancellationToken);
                    await ExecuteSqlAsync(connection, migration, cancellationToken);
                    _logger.LogInformation("Localization migration completed");
                }

                // Load kopitiam catalog data
                var dataPath = Path.Combine("data", "catalog", "kopitiam_catalog_data.sql");
                if (File.Exists(dataPath))
                {
                    var data = await File.ReadAllTextAsync(dataPath, cancellationToken);
                    await ExecuteSqlAsync(connection, data, cancellationToken);
                    _logger.LogInformation("Kopitiam catalog data loaded successfully");
                }

                // Load modifications data
                var modDataPath = Path.Combine("data", "catalog", "kopitiam_modifications_data.sql");
                if (File.Exists(modDataPath))
                {
                    var data = await File.ReadAllTextAsync(modDataPath, cancellationToken);
                    await ExecuteSqlAsync(connection, data, cancellationToken);
                    _logger.LogInformation("Kopitiam modifications data loaded successfully");
                }
            }
            else
            {
                // Verify database integrity
                using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA integrity_check";
                var result = command.ExecuteScalar()?.ToString();

                if (result != "ok")
                {
                    throw new InvalidOperationException($"Database integrity check failed: {result}");
                }

                _logger.LogInformation("Database integrity verified: {DatabasePath}", _databasePath);
            }
        }

        private async Task<bool> IsEmptyDatabaseAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
            var tableCount = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            return tableCount == 0;
        }

        private async Task ExecuteSqlAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
        {
            // Split SQL by semicolon and execute each statement
            var statements = sql.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var statement in statements)
            {
                var trimmedStatement = statement.Trim();
                if (!string.IsNullOrEmpty(trimmedStatement))
                {
                    try
                    {
                        using var command = connection.CreateCommand();
                        command.CommandText = trimmedStatement;
                        await command.ExecuteNonQueryAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing SQL statement: {Statement}", trimmedStatement);
                        // Continue with other statements
                    }
                }
            }
        }

        /// <summary>
        /// Disposes resources used by the service.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // No resources to dispose
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Enhanced product information with modifications and localization support.
    /// </summary>
    public class EnhancedProductInfo : IProductInfo
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
        /// Gets or sets whether the product is active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets whether the product requires preparation.
        /// </summary>
        public bool RequiresPreparation { get; set; }

        /// <summary>
        /// Gets or sets the preparation time in minutes.
        /// </summary>
        public int PreparationTimeMinutes { get; set; }

        /// <summary>
        /// Gets or sets the localization key for the product name.
        /// </summary>
        public string? NameLocalizationKey { get; set; }

        /// <summary>
        /// Gets or sets the localization key for the product description.
        /// </summary>
        public string? DescriptionLocalizationKey { get; set; }

        /// <summary>
        /// Gets or sets the product attributes.
        /// </summary>
        public IReadOnlyDictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();
    }
}
