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

namespace PosKernel.Extensions.Restaurant
{
    /// <summary>
    /// Restaurant Extension Service - provides product catalog operations via named pipe IPC.
    /// This is the actual service that clients connect to.
    /// </summary>
    public class RestaurantExtensionService : IDisposable
    {
        private readonly string _pipeName;
        private readonly string _databasePath;
        private readonly ILogger<RestaurantExtensionService> _logger;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private Task? _serverTask;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the RestaurantExtensionService.
        /// </summary>
        public RestaurantExtensionService(ILogger<RestaurantExtensionService> logger, string pipeName = "poskernel-restaurant-extension", string? databasePath = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pipeName = pipeName;
            _databasePath = databasePath ?? Path.Combine("data", "catalog", "restaurant_catalog.db");
            
            // Debug: Log the actual database path being used
            _logger.LogInformation("Restaurant Extension Service configured with database path: {DatabasePath}", _databasePath);
        }

        /// <summary>
        /// Starts the restaurant extension service.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Restaurant extension service starting on pipe: {PipeName}", _pipeName);
            
            // Initialize database first
            await InitializeDatabaseAsync();
            
            // Start the named pipe server
            _serverTask = Task.Run(RunServerLoopAsync, cancellationToken);
            
            _logger.LogInformation("Restaurant extension service started successfully");
        }

        /// <summary>
        /// Stops the restaurant extension service.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Restaurant extension service stopping...");
            
            _cancellationTokenSource.Cancel();
            
            if (_serverTask != null)
            {
                await _serverTask.WaitAsync(cancellationToken);
            }
            
            _logger.LogInformation("Restaurant extension service stopped");
        }

        private async Task RunServerLoopAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    _logger.LogDebug("Waiting for client connection on pipe: {PipeName}", _pipeName);
                    
                    using var server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        1); // Single client at a time for simplicity

                    await server.WaitForConnectionAsync(_cancellationTokenSource.Token);
                    _logger.LogInformation("Client connected to restaurant extension service");

                    await HandleClientAsync(server, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // Service is stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in restaurant extension server loop");
                    // Continue to next iteration
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
                _logger.LogInformation("Client disconnected from restaurant extension service");
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
                var productInfo = new ProductInfo
                {
                    Sku = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CategoryName = reader.GetString(3),
                    BasePriceCents = reader.GetInt64(4),
                    IsActive = reader.GetBoolean(5)
                };

                return new ExtensionResponse
                {
                    Id = request.Id,
                    Success = true,
                    Data = new Dictionary<string, object>
                    {
                        ["product_info"] = productInfo,
                        ["effective_price_cents"] = productInfo.BasePriceCents
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

            var products = new List<ProductSearchResult>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var productInfo = new ProductInfo
                {
                    Sku = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CategoryName = reader.GetString(3),
                    BasePriceCents = reader.GetInt64(4),
                    IsActive = reader.GetBoolean(5)
                };

                products.Add(new ProductSearchResult
                {
                    ProductInfo = productInfo,
                    EffectivePriceCents = productInfo.BasePriceCents
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
                        WHEN 'Hot Drinks' THEN 1
                        WHEN 'Food' THEN 2
                        WHEN 'Cold Drinks' THEN 3
                        ELSE 4
                    END,
                    p.name
                LIMIT 10";

            var products = new List<ProductSearchResult>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var productInfo = new ProductInfo
                {
                    Sku = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CategoryName = reader.GetString(3),
                    BasePriceCents = reader.GetInt64(4),
                    IsActive = reader.GetBoolean(5)
                };

                products.Add(new ProductSearchResult
                {
                    ProductInfo = productInfo,
                    EffectivePriceCents = productInfo.BasePriceCents
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

            var products = new List<ProductSearchResult>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var productInfo = new ProductInfo
                {
                    Sku = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.GetString(2),
                    CategoryName = reader.GetString(3),
                    BasePriceCents = reader.GetInt64(4),
                    IsActive = reader.GetBoolean(5)
                };

                products.Add(new ProductSearchResult
                {
                    ProductInfo = productInfo,
                    EffectivePriceCents = productInfo.BasePriceCents
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
            connection.Open();

            if (!dbExists)
            {
                _logger.LogInformation("Initializing new restaurant catalog database: {DatabasePath}", _databasePath);
                
                // Read and execute schema script
                var schemaPath = Path.Combine("data", "catalog", "restaurant_catalog_schema.sql");
                if (File.Exists(schemaPath))
                {
                    var schema = await File.ReadAllTextAsync(schemaPath);
                    using var command = connection.CreateCommand();
                    command.CommandText = schema;
                    command.ExecuteNonQuery();
                    _logger.LogInformation("Database schema created successfully");
                }
                else
                {
                    _logger.LogWarning("Schema file not found: {SchemaPath}", schemaPath);
                }

                // Read and execute data script
                var dataPath = Path.Combine("data", "catalog", "restaurant_catalog_data.sql");
                if (File.Exists(dataPath))
                {
                    var data = await File.ReadAllTextAsync(dataPath);
                    using var command = connection.CreateCommand();
                    command.CommandText = data;
                    command.ExecuteNonQuery();
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
    /// Result from product search operations.
    /// </summary>
    public class ProductSearchResult
    {
        /// <summary>
        /// Gets or sets the product information.
        /// </summary>
        public ProductInfo ProductInfo { get; set; } = new();
        
        /// <summary>
        /// Gets or sets the effective price in cents.
        /// </summary>
        public long EffectivePriceCents { get; set; }
    }
}
