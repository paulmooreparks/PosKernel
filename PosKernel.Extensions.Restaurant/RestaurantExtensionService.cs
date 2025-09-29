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

using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PosKernel.Abstractions.Services;

namespace PosKernel.Extensions.Restaurant
{
    /// <summary>
    /// Retail Extension Service - provides product catalog operations for retail stores via named pipe IPC.
    /// ARCHITECTURAL PRINCIPLE: Generic retail extension that serves any retail store type through configuration.
    /// </summary>
    public class RetailExtensionService : IDisposable
    {
        private readonly string _pipeName;
        private readonly string _storeType;
        private readonly string _userDirectoryPath;
        private readonly string _databasePath;
        private readonly string _schemaPath;
        private readonly string _dataPath;
        private readonly ILogger<RetailExtensionService> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private Task? _serverTask;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the RetailExtensionService.
        /// ARCHITECTURAL PRINCIPLE: Store type comes from configuration, not hardcoded assumptions.
        /// </summary>
        public RetailExtensionService(ILogger<RetailExtensionService> logger, string storeType = "CoffeeShop", string pipeName = "poskernel-restaurant-extension")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pipeName = pipeName;
            _storeType = storeType;

            // ARCHITECTURAL PRINCIPLE: All runtime data goes under ~/.poskernel user directory
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _userDirectoryPath = Path.Combine(userHome, ".poskernel", "extensions", "retail", _storeType);
            var catalogPath = Path.Combine(_userDirectoryPath, "catalog");

            _databasePath = Path.Combine(catalogPath, "retail_catalog.db");
            _schemaPath = Path.Combine(catalogPath, "retail_schema.sql");
            _dataPath = Path.Combine(catalogPath, "retail_data.sql");
            
            _logger.LogInformation("Retail Extension Service configured for store type: {StoreType}", _storeType);
            _logger.LogInformation("User directory: {UserDirectory}", _userDirectoryPath);
            _logger.LogInformation("Database path: {DatabasePath}", _databasePath);
        }

        /// <summary>
        /// Starts the retail extension service.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Retail extension service starting for {StoreType} on pipe: {PipeName}", _storeType, _pipeName);
            
            // Initialize database first
            await InitializeDatabaseAsync();
            
            // Start the named pipe server
            _serverTask = Task.Run(RunServerLoopAsync, cancellationToken);
            
            _logger.LogInformation("Retail extension service started successfully for {StoreType}", _storeType);
        }

        /// <summary>
        /// Stops the retail extension service.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Retail extension service stopping...");
            
            _cancellationTokenSource.Cancel();
            
            if (_serverTask != null)
            {
                await _serverTask.WaitAsync(cancellationToken);
            }
            
            _logger.LogInformation("Retail extension service stopped");
        }

        private async Task RunServerLoopAsync()
        {
            var tasks = new List<Task>();
            
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    _logger.LogDebug("Waiting for client connection on pipe: {PipeName}", _pipeName);
                    
                    var server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances, // Allow multiple clients
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(_cancellationTokenSource.Token);
                    _logger.LogInformation("Client connected to retail extension service");

                    // Handle each client in a separate task to allow concurrent connections
                    var clientTask = Task.Run(async () =>
                    {
                        try
                        {
                            await HandleClientAsync(server, _cancellationTokenSource.Token);
                        }
                        finally
                        {
                            server.Dispose();
                        }
                    });
                    
                    tasks.Add(clientTask);
                    
                    // Clean up completed tasks
                    tasks.RemoveAll(t => t.IsCompleted);
                }
                catch (OperationCanceledException)
                {
                    // Service is stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in retail extension server loop");
                    // Continue to next iteration
                }
            }
            
            // Wait for all client tasks to complete
            if (tasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error waiting for client tasks to complete");
                }
            }
        }

        private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
        {
            try
            {
                using var reader = new StreamReader(server);
                using var writer = new StreamWriter(server) { AutoFlush = true };

                while (server.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    var requestLine = await reader.ReadLineAsync(cancellationToken);
                    if (requestLine == null)
                    {
                        break; // Client disconnected
                    }

                    _logger.LogDebug("Received request: {Request}", requestLine);

                    try
                    {
                        var request = JsonSerializer.Deserialize<ExtensionRequest>(requestLine);
                        if (request == null)
                        {
                            throw new InvalidOperationException("Invalid request format");
                        }

                        var response = await ProcessRequestAsync(request, cancellationToken);
                        var responseJson = JsonSerializer.Serialize(response);
                        
                        _logger.LogDebug("Sending response: {Response}", responseJson);
                        await writer.WriteLineAsync(responseJson);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing request: {Request}", requestLine);
                        
                        var errorResponse = new ExtensionResponse
                        {
                            Id = "unknown",
                            Success = false,
                            Error = ex.Message
                        };
                        
                        await writer.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling client connection");
            }
            finally
            {
                _logger.LogInformation("Client disconnected from retail extension service");
            }
        }

        private async Task<ExtensionResponse> ProcessRequestAsync(ExtensionRequest request, CancellationToken cancellationToken)
        {
            try
            {
                return request.Method switch
                {
                    "validate_product" => await ValidateProductAsync(request),
                    "search_products" => await SearchProductsAsync(request),
                    "get_popular_items" => await GetPopularItemsAsync(request),
                    "get_category_products" => await GetCategoryProductsAsync(request),
                    "get_categories" => await GetCategoriesAsync(request),
                    "resolve_display_name" => await ResolveDisplayNameAsync(request),
                    "disconnect" => HandleDisconnect(request),
                    _ => new ExtensionResponse
                    {
                        Id = request.Id,
                        Success = false,
                        Error = $"Unknown method: {request.Method}"
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing method {Method}", request.Method);
                return new ExtensionResponse
                {
                    Id = request.Id,
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        private async Task<ExtensionResponse> ValidateProductAsync(ExtensionRequest request)
        {
            // Extract parameters
            var productId = GetStringParameter(request, "product_id");
            var storeId = GetStringParameter(request, "store_id", "STORE_DEFAULT");
            
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT p.sku, p.name, p.description, c.name as category_name, p.base_price_cents, p.is_active
                FROM products p 
                INNER JOIN categories c ON p.category_id = c.id
                WHERE p.sku = @productId OR p.name LIKE '%' || @productId ||'%'
                LIMIT 1";
            command.Parameters.AddWithValue("@productId", productId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var priceCents = reader.GetInt64(4); // base_price_cents from database
                
                var productInfo = new RestaurantCompatibleProductInfo
                {
                    Sku = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CategoryName = reader.GetString(3),
                    BasePriceCents = priceCents, // ← Fixed to use the actual price
                    IsActive = reader.GetBoolean(5)
                };

                return new ExtensionResponse
                {
                    Id = request.Id,
                    Success = true,
                    Data = new Dictionary<string, object>
                    {
                        ["product_info"] = productInfo,
                        ["effective_price_cents"] = priceCents
                    }
                };
            }

            return new ExtensionResponse
            {
                Id = request.Id,
                Success = false,
                Error = $"Product not found: {productId}"
            };
        }

        private async Task<ExtensionResponse> SearchProductsAsync(ExtensionRequest request)
        {
            var searchTerm = GetStringParameter(request, "search_term", "");
            var maxResults = GetIntParameter(request, "max_results", 50);

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            
            if (string.IsNullOrEmpty(searchTerm))
            {
                // Return all products
                command.CommandText = @"
                    SELECT p.sku, p.name, p.description, c.name as category_name, p.base_price_cents, p.is_active
                    FROM products p 
                    INNER JOIN categories c ON p.category_id = c.id
                    WHERE p.is_active = 1
                    ORDER BY p.name
                    LIMIT @maxResults";
            }
            else
            {
                // Search by term
                command.CommandText = @"
                    SELECT p.sku, p.name, p.description, c.name as category_name, p.base_price_cents, p.is_active
                    FROM products p 
                    INNER JOIN categories c ON p.category_id = c.id
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
                        p.name
                    LIMIT @maxResults";
                command.Parameters.AddWithValue("@searchTerm", searchTerm);
            }
            
            command.Parameters.AddWithValue("@maxResults", maxResults);

            var products = new List<RetailProductSearchResult>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var priceCents = reader.GetInt64(4); // base_price_cents from database
                
                var productInfo = new RestaurantCompatibleProductInfo
                {
                    Sku = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CategoryName = reader.GetString(3),
                    BasePriceCents = priceCents, // ← This is the key fix!
                    IsActive = reader.GetBoolean(5)
                };

                products.Add(new RetailProductSearchResult
                {
                    ProductInfo = productInfo,
                    EffectivePriceCents = priceCents
                });
            }

            return new ExtensionResponse
            {
                Id = request.Id,
                Success = true,
                Data = new Dictionary<string, object>
                {
                    ["products"] = products
                }
            };
        }

        private async Task<ExtensionResponse> GetPopularItemsAsync(ExtensionRequest request)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();

            // For demo, just return some popular items based on category priority
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT p.sku, p.name, p.description, c.name as category_name, p.base_price_cents, p.is_active
                FROM products p 
                INNER JOIN categories c ON p.category_id = c.id
                WHERE p.is_active = 1
                ORDER BY 
                    CASE c.name
                        WHEN 'Hot Coffees' THEN 1
                        WHEN 'Hot Drinks' THEN 2
                        WHEN 'Pastries' THEN 3
                        WHEN 'Iced Coffees' THEN 4
                        ELSE 5
                    END,
                    p.name
                LIMIT 10";

            var products = new List<RetailProductSearchResult>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var priceCents = reader.GetInt64(4); // base_price_cents from database
                
                var productInfo = new RestaurantCompatibleProductInfo
                {
                    Sku = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CategoryName = reader.GetString(3),
                    BasePriceCents = priceCents, // ← Fixed to use the actual price
                    IsActive = reader.GetBoolean(5)
                };

                products.Add(new RetailProductSearchResult
                {
                    ProductInfo = productInfo,
                    EffectivePriceCents = priceCents
                });
            }

            return new ExtensionResponse
            {
                Id = request.Id,
                Success = true,
                Data = new Dictionary<string, object>
                {
                    ["popular_items"] = products
                }
            };
        }

        private async Task<ExtensionResponse> GetCategoryProductsAsync(ExtensionRequest request)
        {
            var category = GetStringParameter(request, "category");

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT p.sku, p.name, p.description, c.name as category_name, p.base_price_cents, p.is_active
                FROM products p 
                INNER JOIN categories c ON p.category_id = c.id
                WHERE p.is_active = 1 AND c.name = @category
                ORDER BY p.name";
            command.Parameters.AddWithValue("@category", category);

            var products = new List<RetailProductSearchResult>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var priceCents = reader.GetInt64(4); // base_price_cents from database
                
                var productInfo = new RestaurantCompatibleProductInfo
                {
                    Sku = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CategoryName = reader.GetString(3),
                    BasePriceCents = priceCents, // ← Fixed to use the actual price
                    IsActive = reader.GetBoolean(5)
                };

                products.Add(new RetailProductSearchResult
                {
                    ProductInfo = productInfo,
                    EffectivePriceCents = priceCents
                });
            }

            return new ExtensionResponse
            {
                Id = request.Id,
                Success = true,
                Data = new Dictionary<string, object>
                {
                    ["products"] = products
                }
            };
        }

        private async Task<ExtensionResponse> GetCategoriesAsync(ExtensionRequest request)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT c.name
                FROM categories c 
                WHERE c.is_active = 1
                ORDER BY c.display_order, c.name";

            var categories = new List<string>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                categories.Add(reader.GetString(0));
            }

            return new ExtensionResponse
            {
                Id = request.Id,
                Success = true,
                Data = new Dictionary<string, object>
                {
                    ["categories"] = categories
                }
            };
        }

        private async Task<ExtensionResponse> ResolveDisplayNameAsync(ExtensionRequest request)
        {
            var sku = GetStringParameter(request, "sku");
            if (string.IsNullOrWhiteSpace(sku))
            {
                return new ExtensionResponse { Id = request.Id, Success = false, Error = "Parameter 'sku' is required" };
            }

            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();

            // Try products table first
            using (var productCmd = connection.CreateCommand())
            {
                productCmd.CommandText = @"SELECT name FROM products WHERE sku = @sku LIMIT 1";
                productCmd.Parameters.AddWithValue("@sku", sku);
                var prodName = await productCmd.ExecuteScalarAsync() as string;
                if (!string.IsNullOrWhiteSpace(prodName))
                {
                    return new ExtensionResponse
                    {
                        Id = request.Id,
                        Success = true,
                        Data = new Dictionary<string, object>
                        {
                            ["display_name"] = prodName
                        }
                    };
                }
            }

            // Check if product_modifications table exists
            using (var existsCmd = connection.CreateCommand())
            {
                existsCmd.CommandText = @"SELECT 1 FROM sqlite_master WHERE type='table' AND name='product_modifications'";
                var exists = await existsCmd.ExecuteScalarAsync();
                if (exists != null)
                {
                    using var modCmd = connection.CreateCommand();
                    modCmd.CommandText = @"SELECT name FROM product_modifications WHERE modification_id = @sku LIMIT 1";
                    modCmd.Parameters.AddWithValue("@sku", sku);
                    var modName = await modCmd.ExecuteScalarAsync() as string;
                    if (!string.IsNullOrWhiteSpace(modName))
                    {
                        return new ExtensionResponse
                        {
                            Id = request.Id,
                            Success = true,
                            Data = new Dictionary<string, object>
                            {
                                ["display_name"] = modName
                            }
                        };
                    }
                }
            }

            // Not found
            return new ExtensionResponse
            {
                Id = request.Id,
                Success = true,
                Data = new Dictionary<string, object>
                {
                    ["display_name"] = sku // let client decide fallback formatting
                }
            };
        }

        private ExtensionResponse HandleDisconnect(ExtensionRequest request)
        {
            return new ExtensionResponse
            {
                Id = request.Id,
                Success = true,
                Data = new Dictionary<string, object>
                {
                    ["message"] = "Disconnected successfully"
                }
            };
        }

        // Helper methods
        private string GetStringParameter(ExtensionRequest request, string name, string defaultValue = "")
        {
            if (request.Params.TryGetValue(name, out var element))
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    // Handle the wrapped format: { "StringValue": "actual_value" }
                    if (element.TryGetProperty("StringValue", out var stringValueElement))
                    {
                        return stringValueElement.GetString() ?? defaultValue;
                    }
                }
                return element.GetString() ?? defaultValue;
            }
            return defaultValue;
        }

        private int GetIntParameter(ExtensionRequest request, string name, int defaultValue = 0)
        {
            if (request.Params.TryGetValue(name, out var element))
            {
                if (element.ValueKind == JsonValueKind.Number)
                {
                    return element.GetInt32();
                }
            }
            return defaultValue;
        }

        private async Task InitializeDatabaseAsync()
        {
            // ARCHITECTURAL PRINCIPLE: Ensure user directory structure exists
            var catalogDirectory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(catalogDirectory) && !Directory.Exists(catalogDirectory))
            {
                Directory.CreateDirectory(catalogDirectory);
                _logger.LogInformation("Created catalog directory: {Directory}", catalogDirectory);
            }

            // Check if database exists and is initialized
            var dbExists = File.Exists(_databasePath);
            
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();

            if (!dbExists)
            {
                _logger.LogInformation("Initializing new retail catalog database for {StoreType}: {DatabasePath}", _storeType, _databasePath);
                
                // ARCHITECTURAL PRINCIPLE: Schema and data files come from user space, not solution directory
                if (File.Exists(_schemaPath))
                {
                    var schema = await File.ReadAllTextAsync(_schemaPath);
                    using var command = connection.CreateCommand();
                    command.CommandText = schema;
                    command.ExecuteNonQuery();
                    _logger.LogInformation("Database schema created successfully from: {SchemaPath}", _schemaPath);
                }
                else
                {
                    _logger.LogWarning("Schema file not found: {SchemaPath} - creating default retail schema", _schemaPath);
                    await CreateDefaultSchemaAsync(connection);
                }

                if (File.Exists(_dataPath))
                {
                    var data = await File.ReadAllTextAsync(_dataPath);
                    using var command = connection.CreateCommand();
                    command.CommandText = data;
                    command.ExecuteNonQuery();
                    _logger.LogInformation("Sample data loaded successfully from: {DataPath}", _dataPath);
                }
                else
                {
                    _logger.LogWarning("Data file not found: {DataPath} - database will be empty", _dataPath);
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
                
                _logger.LogInformation("Database integrity verified for {StoreType}: {DatabasePath}", _storeType, _databasePath);
            }
        }

        /// <summary>
        /// Creates default retail schema when no schema file is provided.
        /// ARCHITECTURAL PRINCIPLE: Generic retail schema that works for any store type.
        /// </summary>
        private async Task CreateDefaultSchemaAsync(SqliteConnection connection)
        {
            var defaultSchema = @"
                -- Default Retail Extension Schema
                -- Generic schema that works for any retail store type

                CREATE TABLE IF NOT EXISTS categories (
                    id VARCHAR(50) PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    description TEXT,
                    display_order INT DEFAULT 0,
                    is_active BOOLEAN DEFAULT TRUE,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS products (
                    sku VARCHAR(50) PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    description TEXT,
                    category_id VARCHAR(50) NOT NULL,
                    base_price_cents INTEGER NOT NULL,
                    is_active BOOLEAN DEFAULT TRUE,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (category_id) REFERENCES categories(id)
                );

                CREATE INDEX IF NOT EXISTS idx_products_category ON products(category_id);
                CREATE INDEX IF NOT EXISTS idx_products_active ON products(is_active);
                CREATE INDEX IF NOT EXISTS idx_products_search ON products(name, description);
            ";

            using var command = connection.CreateCommand();
            command.CommandText = defaultSchema;
            command.ExecuteNonQuery();
            
            _logger.LogInformation("Default retail schema created for {StoreType}", _storeType);

            // Also write the schema to the expected file location for future reference
            await File.WriteAllTextAsync(_schemaPath, defaultSchema);
            _logger.LogInformation("Default schema written to: {SchemaPath}", _schemaPath);
        }

        /// <summary>
        /// Disposes the service and cancellation token.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _disposed = true;
            }
        }

        /// <summary>
        /// Gets set meal components for a specific product SKU.
        /// ARCHITECTURAL PRINCIPLE: Data-driven set configuration from database, not hardcoded rules.
        /// </summary>
        /// <param name="productSku">The set product SKU to query.</param>
        /// <returns>List of set meal components.</returns>
        public async Task<IReadOnlyList<SetMealComponent>> GetSetMealComponentsAsync(string productSku)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    component_id,
                    set_product_sku,
                    component_type,
                    modification_id,
                    is_required,
                    is_automatic,
                    quantity,
                    price_adjustment_cents,
                    display_order
                FROM set_meal_components
                WHERE set_product_sku = @productSku 
                  AND is_active = 1
                ORDER BY display_order, component_type";
            command.Parameters.AddWithValue("@productSku", productSku);

            var components = new List<SetMealComponent>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                components.Add(new SetMealComponent
                {
                    ComponentId = reader.GetString(0),
                    SetProductSku = reader.GetString(1),
                    ComponentType = reader.GetString(2),
                    ModificationId = reader.IsDBNull(3) ? null : reader.GetString(3),
                    IsRequired = reader.GetBoolean(4),
                    IsAutomatic = reader.GetBoolean(5),
                    Quantity = reader.GetInt32(6),
                    PriceAdjustmentCents = reader.GetInt64(7),
                    DisplayOrder = reader.GetInt32(8)
                });
            }

            return components;
        }

        /// <summary>
        /// Gets available modifications for a specific product and component type.
        /// ARCHITECTURAL PRINCIPLE: Query actual database for modification options, don't assume.
        /// </summary>
        /// <param name="productSku">The product SKU to query modifications for.</param>
        /// <param name="componentType">The component type (e.g., "DRINK_CHOICE", "SIDE_CHOICE").</param>
        /// <returns>List of available modifications.</returns>
        public async Task<IReadOnlyList<ProductModification>> GetAvailableModificationsAsync(string productSku, string componentType)
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT DISTINCT
                    pm.modification_id,
                    pm.name,
                    pm.description,
                    pm.modification_type,
                    pm.price_adjustment_type,
                    pm.base_price_cents,
                    pm.display_order
                FROM product_modifications pm
                INNER JOIN modification_availability ma ON ma.modification_id = pm.modification_id
                WHERE ma.parent_sku = @productSku 
                  AND ma.parent_type = 'PRODUCT'
                  AND pm.is_active = 1
                  AND ma.is_active = 1
                  AND EXISTS (
                      SELECT 1 FROM set_meal_components smc 
                      WHERE smc.set_product_sku = @productSku 
                        AND smc.component_type = @componentType
                  )
                ORDER BY pm.display_order, pm.name";
            command.Parameters.AddWithValue("@productSku", productSku);
            command.Parameters.AddWithValue("@componentType", componentType);

            var modifications = new List<ProductModification>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                modifications.Add(new ProductModification
                {
                    ModificationId = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ModificationType = reader.GetString(3),
                    PriceAdjustmentType = reader.GetString(4),
                    BasePriceCents = reader.GetInt64(5),
                    DisplayOrder = reader.GetInt32(6)
                });
            }

            return modifications;
        }
    }

    /// <summary>
    /// Extension request for IPC communication.
    /// </summary>
    public class ExtensionRequest
    {
        /// <summary>
        /// Gets or sets the request identifier.
        /// </summary>
        public string Id { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the method name.
        /// </summary>
        public string Method { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the request parameters.
        /// </summary>
        public Dictionary<string, JsonElement> Params { get; set; } = new();
    }

    /// <summary>
    /// Extension response for IPC communication.
    /// </summary>
    public class ExtensionResponse
    {
        /// <summary>
        /// Gets or sets the response identifier.
        /// </summary>
        public string Id { get; set; } = "";
        
        /// <summary>
        /// Gets or sets whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Gets or sets the error message if the operation failed.
        /// </summary>
        public string? Error { get; set; }
        
        /// <summary>
        /// Gets or sets the response data.
        /// </summary>
        public Dictionary<string, object>? Data { get; set; }
    }

    /// <summary>
    /// Restaurant-compatible product information that matches the client expectations.
    /// ARCHITECTURAL PRINCIPLE: Ensure compatibility between client and service data structures.
    /// </summary>
    public class RestaurantCompatibleProductInfo
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
        /// Gets or sets the category name.
        /// </summary>
        public string CategoryName { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the base price in cents.
        /// ARCHITECTURAL PRINCIPLE: Store price as integers to avoid floating-point precision issues.
        /// </summary>
        public long BasePriceCents { get; set; }
        
        /// <summary>
        /// Gets or sets whether the product is active.
        /// </summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Result from product search operations with compatible structure.
    /// </summary>
    public class RetailProductSearchResult
    {
        /// <summary>
        /// Gets or sets the product information.
        /// </summary>
        public RestaurantCompatibleProductInfo ProductInfo { get; set; } = new();
        
        /// <summary>
        /// Gets or sets the effective price in cents.
        /// </summary>
        public long EffectivePriceCents { get; set; }
    }

    /// <summary>
    /// Represents a set meal component for a product.
    /// ARCHITECTURAL PRINCIPLE: Model all product-related data as first-class entities.
    /// </summary>
    public class SetMealComponent
    {
        /// <summary>
        /// Gets or sets the component identifier.
        /// </summary>
        public string ComponentId { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the set product SKU that this component belongs to.
        /// </summary>
        public string SetProductSku { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the component type (e.g., "DRINK_CHOICE", "SIDE_CHOICE").
        /// </summary>
        public string ComponentType { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the modification identifier if this component has a modification.
        /// </summary>
        public string? ModificationId { get; set; }
        
        /// <summary>
        /// Gets or sets whether this component is required.
        /// </summary>
        public bool IsRequired { get; set; }
        
        /// <summary>
        /// Gets or sets whether this component is automatic (i.e., added by default).
        /// </summary>
        public bool IsAutomatic { get; set; }
        
        /// <summary>
        /// Gets or sets the quantity of this component.
        /// </summary>
        public int Quantity { get; set; }
        
        /// <summary>
        /// Gets or sets the price adjustment in cents for this component.
        /// ARCHITECTURAL PRINCIPLE: All monetary values in cents to avoid floating-point issues.
        /// </summary>
        public long PriceAdjustmentCents { get; set; }
        
        /// <summary>
        /// Gets or sets the display order for this component in the set meal.
        /// </summary>
        public int DisplayOrder { get; set; }
    }

    /// <summary>
    /// Represents a product modification option.
    /// ARCHITECTURAL PRINCIPLE: Model all product-related data as first-class entities.
    /// </summary>
    public class ProductModification
    {
        /// <summary>
        /// Gets or sets the modification identifier.
        /// </summary>
        public string ModificationId { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the modification name.
        /// </summary>
        public string Name { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the modification description.
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Gets or sets the modification type.
        /// </summary>
        public string ModificationType { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the price adjustment type.
        /// </summary>
        public string PriceAdjustmentType { get; set; } = "";
        
        /// <summary>
        /// Gets or sets the base price in cents.
        /// </summary>
        public long BasePriceCents { get; set; }
        
        /// <summary>
        /// Gets or sets the display order.
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// Gets the price adjustment as decimal for display purposes.
        /// </summary>
        public decimal PriceAdjustment => BasePriceCents / 100.0m;
    }
}
