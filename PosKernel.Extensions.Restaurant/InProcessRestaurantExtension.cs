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

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace PosKernel.Extensions.Restaurant
{
    /// <summary>
    /// In-process implementation of the Restaurant Extension.
    /// Directly accesses the SQLite database for maximum performance.
    /// </summary>
    public class InProcessRestaurantExtension : IRestaurantExtension, IDisposable
    {
        private readonly string _databasePath;
        private readonly ILogger<InProcessRestaurantExtension> _logger;
        private bool _disposed = false;
        private bool _databaseInitialized = false;

        /// <summary>
        /// Initializes a new instance of the InProcessRestaurantExtension.
        /// </summary>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <param name="databasePath">Path to the SQLite database file.</param>
        public InProcessRestaurantExtension(ILogger<InProcessRestaurantExtension> logger, string? databasePath = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _databasePath = databasePath ?? Path.Combine("data", "catalog", "restaurant_catalog.db");
        }

        /// <summary>
        /// Ensures the database is initialized.
        /// </summary>
        private async Task EnsureDatabaseInitializedAsync(CancellationToken cancellationToken = default)
        {
            if (_databaseInitialized)
            {
                return;
            }

            await InitializeDatabaseAsync(cancellationToken);
            _databaseInitialized = true;
        }

        /// <inheritdoc/>
        public async Task<ProductValidationResult> ValidateProductAsync(
            string productId,
            string storeId = "STORE_DEFAULT",
            string terminalId = "TERMINAL_01",
            string operatorId = "SYSTEM",
            CancellationToken cancellationToken = default)
        {
            await EnsureDatabaseInitializedAsync(cancellationToken);

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT p.sku, p.name, p.description, c.name as category_name, p.base_price, p.is_active
                FROM products p
                JOIN categories c ON p.category_id = c.id
                WHERE p.sku = @productId OR p.name LIKE '%' || @productId || '%'
                LIMIT 1";
            command.Parameters.AddWithValue("@productId", productId);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var productInfo = new ProductInfo
                {
                    Sku = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CategoryName = reader.GetString(3),
                    BasePrice = reader.GetDecimal(4), // Direct decimal from base_price column
                    IsActive = reader.GetBoolean(5)
                };

                return new ProductValidationResult
                {
                    IsValid = true,
                    ProductInfo = productInfo,
                    EffectivePrice = productInfo.BasePrice
                };
            }

            return new ProductValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Product not found: {productId}"
            };
        }

        /// <inheritdoc/>
        public async Task<List<ProductInfo>> SearchProductsAsync(
            string searchTerm,
            int maxResults = 50,
            CancellationToken cancellationToken = default)
        {
            await EnsureDatabaseInitializedAsync(cancellationToken);

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();

            if (string.IsNullOrEmpty(searchTerm))
            {
                // Return all products
                command.CommandText = @"
                    SELECT p.sku, p.name, p.description, c.name as category_name, p.base_price, p.is_active
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
                    SELECT p.sku, p.name, p.description, c.name as category_name, p.base_price, p.is_active
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

            var products = new List<ProductInfo>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                products.Add(new ProductInfo
                {
                    Sku = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CategoryName = reader.GetString(3),
                    BasePrice = reader.GetInt64(4) / 100.0m, // Convert from cents to decimal
                    IsActive = reader.GetBoolean(5)
                });
            }

            return products;
        }

        /// <inheritdoc/>
        public async Task<List<ProductInfo>> GetPopularItemsAsync(CancellationToken cancellationToken = default)
        {
            await EnsureDatabaseInitializedAsync(cancellationToken);

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT p.sku, p.name, p.description, c.name as category_name, p.base_price, p.is_active
                FROM products p
                JOIN categories c ON p.category_id = c.id
                WHERE p.is_active = 1
                ORDER BY p.popularity_rank, c.display_order, p.name
                LIMIT 10";

            var products = new List<ProductInfo>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                products.Add(new ProductInfo
                {
                    Sku = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CategoryName = reader.GetString(3),
                    BasePrice = reader.GetDecimal(4), // Direct decimal from base_price column
                    IsActive = reader.GetBoolean(5)
                });
            }

            return products;
        }

        /// <inheritdoc/>
        public async Task<List<ProductInfo>> GetCategoryProductsAsync(
            string category,
            CancellationToken cancellationToken = default)
        {
            await EnsureDatabaseInitializedAsync(cancellationToken);

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT p.sku, p.name, p.description, c.name as category_name, p.base_price, p.is_active
                FROM products p
                JOIN categories c ON p.category_id = c.id
                WHERE p.is_active = 1 AND (c.name = @category OR c.id = @category)
                ORDER BY p.popularity_rank, p.name";
            command.Parameters.AddWithValue("@category", category);

            var products = new List<ProductInfo>();
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                products.Add(new ProductInfo
                {
                    Sku = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CategoryName = reader.GetString(3),
                    BasePrice = reader.GetDecimal(4), // Direct decimal from base_price column
                    IsActive = reader.GetBoolean(5)
                });
            }

            return products;
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            await EnsureDatabaseInitializedAsync(cancellationToken);

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

        /// <inheritdoc/>
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            // In-process is always available if database exists
            return Task.FromResult(File.Exists(_databasePath));
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
                _logger.LogInformation("Initializing restaurant catalog database: {DatabasePath}", _databasePath);

                // Read and execute schema script
                var schemaPath = FindSchemaFile();
                if (File.Exists(schemaPath))
                {
                    var schema = await File.ReadAllTextAsync(schemaPath, cancellationToken);
                    await ExecuteSqlAsync(connection, schema, cancellationToken);
                    _logger.LogInformation("Database schema created successfully");
                }
                else
                {
                    _logger.LogWarning("Schema file not found: {SchemaPath}", schemaPath);
                    throw new FileNotFoundException($"Database schema file not found: {schemaPath}");
                }

                // Read and execute data script
                var dataPath = FindDataFile();
                if (File.Exists(dataPath))
                {
                    var data = await File.ReadAllTextAsync(dataPath, cancellationToken);
                    await ExecuteSqlAsync(connection, data, cancellationToken);
                    _logger.LogInformation("Sample data loaded successfully");
                }
                else
                {
                    _logger.LogWarning("Data file not found: {DataPath}", dataPath);
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
                    using var command = connection.CreateCommand();
                    command.CommandText = trimmedStatement;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }

        private string FindSchemaFile()
        {
            // Try multiple possible locations
            var possiblePaths = new[]
            {
                Path.Combine("data", "catalog", "restaurant_catalog_schema.sql"),
                Path.Combine("..", "data", "catalog", "restaurant_catalog_schema.sql"),
                Path.Combine("..", "..", "data", "catalog", "restaurant_catalog_schema.sql"),
                Path.Combine("..", "..", "..", "data", "catalog", "restaurant_catalog_schema.sql")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            return Path.Combine("data", "catalog", "restaurant_catalog_schema.sql");
        }

        private string FindDataFile()
        {
            // Try multiple possible locations
            var possiblePaths = new[]
            {
                Path.Combine("data", "catalog", "restaurant_catalog_data.sql"),
                Path.Combine("..", "data", "catalog", "restaurant_catalog_data.sql"),
                Path.Combine("..", "..", "data", "catalog", "restaurant_catalog_data.sql"),
                Path.Combine("..", "..", "..", "data", "catalog", "restaurant_catalog_data.sql")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            return Path.Combine("data", "catalog", "restaurant_catalog_data.sql");
        }

        /// <inheritdoc />
        public async Task<SetDefinition?> GetSetDefinitionAsync(string productSku, CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    SELECT
                        p.sku as SetSku,
                        p.category_name as SetType,
                        '' as MainProductSku,
                        p.base_price_cents as BasePriceCents,
                        p.is_active as IsActive
                    FROM products p
                    WHERE p.sku = @productSku
                    AND EXISTS (
                        SELECT 1 FROM set_meal_components smc
                        WHERE smc.set_product_sku = @productSku
                    )";

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("@productSku", productSku);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    return new SetDefinition
                    {
                        SetSku = reader.GetString(0),
                        SetType = reader.GetString(1),
                        MainProductSku = reader.GetString(2),
                        BasePriceCents = reader.GetInt64(3) / 100.0m, // Convert from cents to decimal
                        IsActive = reader.GetBoolean(4)
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get set definition for product SKU: {ProductSku}", productSku);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<SetAvailableDrink>?> GetSetAvailableDrinksAsync(string setSku, CancellationToken cancellationToken = default)
        {
            var drinks = new List<SetAvailableDrink>();

            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    SELECT DISTINCT
                        @setSku as SetSku,
                        ma.option_product_sku as ProductSku,
                        p.name as ProductName,
                        '' as DrinkSize,
                        ma.is_default as IsDefault,
                        ma.is_available as IsActive
                    FROM modification_availability ma
                    INNER JOIN products p ON p.sku = ma.option_product_sku
                    WHERE ma.set_product_sku = @setSku
                    AND ma.component_type = 'drink'
                    AND ma.is_available = 1
                    ORDER BY p.name";

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("@setSku", setSku);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    drinks.Add(new SetAvailableDrink
                    {
                        SetSku = reader.GetString(0),
                        ProductSku = reader.GetString(1),
                        ProductName = reader.GetString(2),
                        DrinkSize = reader.GetString(3),
                        IsDefault = reader.GetBoolean(4),
                        IsActive = reader.GetBoolean(5)
                    });
                }

                return drinks;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get available drinks for set SKU: {SetSku}", setSku);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<SetAvailableSide>?> GetSetAvailableSidesAsync(string setSku, CancellationToken cancellationToken = default)
        {
            var sides = new List<SetAvailableSide>();

            try
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync(cancellationToken);

                const string sql = @"
                    SELECT DISTINCT
                        @setSku as SetSku,
                        ma.option_product_sku as ProductSku,
                        p.name as ProductName,
                        ma.is_default as IsDefault,
                        ma.is_available as IsActive
                    FROM modification_availability ma
                    INNER JOIN products p ON p.sku = ma.option_product_sku
                    WHERE ma.set_product_sku = @setSku
                    AND ma.component_type = 'side'
                    AND ma.is_available = 1
                    ORDER BY p.name";

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("@setSku", setSku);

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    sides.Add(new SetAvailableSide
                    {
                        SetSku = reader.GetString(0),
                        ProductSku = reader.GetString(1),
                        ProductName = reader.GetString(2),
                        IsDefault = reader.GetBoolean(3),
                        IsActive = reader.GetBoolean(4)
                    });
                }

                return sides;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get available sides for set SKU: {SetSku}", setSku);
                return null;
            }
        }

        /// <summary>
        /// Disposes resources used by the InProcessRestaurantExtension.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // No resources to dispose for in-process implementation
                _disposed = true;
            }
        }
    }
}
