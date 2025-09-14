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
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PosKernel.Extensions.Restaurant;

namespace PosKernel.Extensions.Restaurant
{
    /// <summary>
    /// Main entry point for the restaurant domain extension.
    /// This runs as a pure service that provides product catalog operations
    /// via named pipe IPC for any client (AI demo, kernel service, etc.).
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                Console.WriteLine("POS Kernel Restaurant Extension v0.4.0");
                Console.WriteLine("Pure domain service - no demo coupling");

                // Build configuration
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile("restaurant-config.json", optional: true)
                    .AddEnvironmentVariables("POSKERNEL_RESTAURANT_")
                    .AddCommandLine(args)
                    .Build();

                // Create host builder for proper service lifecycle
                var host = Host.CreateDefaultBuilder(args)
                    .ConfigureServices((context, services) =>
                    {
                        ConfigureServices(services, configuration);
                        services.AddHostedService<RestaurantExtensionService>();
                    })
                    .ConfigureLogging(logging =>
                    {
                        logging.AddConsole();
                        logging.AddConfiguration(configuration.GetSection("Logging"));
                    })
                    .UseConsoleLifetime()
                    .Build();

                // Initialize database before starting service
                var config = host.Services.GetRequiredService<RestaurantConfig>();
                var logger = host.Services.GetRequiredService<ILogger<Program>>();
                await InitializeDatabaseAsync(config.DatabasePath, logger);

                logger.LogInformation("Restaurant extension service starting...");
                await host.RunAsync();

                logger.LogInformation("Restaurant extension service stopped");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex}");
                return 1;
            }
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Configuration
            var restaurantConfig = new RestaurantConfig();
            configuration.GetSection("Restaurant").Bind(restaurantConfig);
            services.AddSingleton(restaurantConfig);

            // Restaurant extension
            services.AddSingleton<RestaurantExtension>();
        }

        private static async Task InitializeDatabaseAsync(string databasePath, ILogger logger)
        {
            // Ensure database directory exists
            var dbDirectory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
                logger.LogInformation("Created database directory: {Directory}", dbDirectory);
            }

            // Check if database exists and is initialized
            var dbExists = File.Exists(databasePath);
            
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            if (!dbExists)
            {
                logger.LogInformation("Initializing new restaurant catalog database: {DatabasePath}", databasePath);
                
                // Read and execute schema script
                var schemaPath = Path.Combine("data", "catalog", "restaurant_catalog_schema.sql");
                if (File.Exists(schemaPath))
                {
                    var schema = await File.ReadAllTextAsync(schemaPath);
                    using var command = connection.CreateCommand();
                    command.CommandText = schema;
                    command.ExecuteNonQuery();
                    logger.LogInformation("Database schema created successfully");
                }
                else
                {
                    logger.LogWarning("Schema file not found: {SchemaPath}", schemaPath);
                }

                // Read and execute data script
                var dataPath = Path.Combine("data", "catalog", "restaurant_catalog_data.sql");
                if (File.Exists(dataPath))
                {
                    var data = await File.ReadAllTextAsync(dataPath);
                    using var command = connection.CreateCommand();
                    command.CommandText = data;
                    command.ExecuteNonQuery();
                    logger.LogInformation("Sample data loaded successfully");
                }
                else
                {
                    logger.LogWarning("Data file not found: {DataPath}", dataPath);
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
                
                logger.LogInformation("Database integrity verified: {DatabasePath}", databasePath);
            }
        }
    }

    /// <summary>
    /// Hosted service that runs the restaurant extension and handles IPC communication.
    /// Provides a clean separation between service lifecycle and business logic.
    /// </summary>
    public class RestaurantExtensionService : BackgroundService
    {
        private readonly RestaurantExtension _extension;
        private readonly RestaurantConfig _config;
        private readonly ILogger<RestaurantExtensionService> _logger;
        private readonly string _pipeName;

        public RestaurantExtensionService(
            RestaurantExtension extension, 
            RestaurantConfig config,
            ILogger<RestaurantExtensionService> logger)
        {
            _extension = extension ?? throw new ArgumentNullException(nameof(extension));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pipeName = "poskernel-restaurant-extension";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Restaurant extension service started on pipe: {PipeName}", _pipeName);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message);

                    _logger.LogDebug("Waiting for client connection...");
                    await server.WaitForConnectionAsync(stoppingToken);

                    _logger.LogDebug("Client connected, processing requests...");
                    await ProcessClientRequestsAsync(server, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in IPC server loop");
                    await Task.Delay(1000, stoppingToken); // Brief delay before retrying
                }
            }

            _logger.LogInformation("Restaurant extension service stopped");
        }

        private async Task ProcessClientRequestsAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(server);
            using var writer = new StreamWriter(server) { AutoFlush = true };

            try
            {
                while (server.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    var requestJson = await reader.ReadLineAsync(cancellationToken);
                    if (requestJson == null) break;

                    _logger.LogDebug("Received request: {Request}", requestJson);

                    try
                    {
                        var request = JsonSerializer.Deserialize<ExtensionRequest>(requestJson);
                        if (request == null)
                        {
                            await SendErrorResponseAsync(writer, "Invalid request format");
                            continue;
                        }

                        var response = await ProcessRequestAsync(request);
                        var responseJson = JsonSerializer.Serialize(response);
                        
                        await writer.WriteLineAsync(responseJson);
                        _logger.LogDebug("Sent response: {Response}", responseJson);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Invalid JSON in request: {Request}", requestJson);
                        await SendErrorResponseAsync(writer, $"Invalid JSON: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing request: {Request}", requestJson);
                        await SendErrorResponseAsync(writer, $"Internal error: {ex.Message}");
                    }
                }
            }
            catch (IOException ex)
            {
                _logger.LogDebug(ex, "Client disconnected");
            }
        }

        private async Task<ExtensionResponse> ProcessRequestAsync(ExtensionRequest request)
        {
            return request.Method switch
            {
                "validate_product" => await HandleValidateProductAsync(request),
                "search_products" => await HandleSearchProductsAsync(request),
                "get_popular_items" => await HandleGetPopularItemsAsync(request),
                "get_category_products" => await HandleGetCategoryProductsAsync(request),
                _ => new ExtensionResponse
                {
                    Id = request.Id,
                    Success = false,
                    Error = $"Unknown method: {request.Method}"
                }
            };
        }

        private async Task<ExtensionResponse> HandleValidateProductAsync(ExtensionRequest request)
        {
            if (request.Params?.TryGetValue("product_id", out var productIdObj) != true ||
                productIdObj is not JsonElement productIdElement ||
                !productIdElement.TryGetProperty("StringValue", out var productIdProp))
            {
                return new ExtensionResponse
                {
                    Id = request.Id,
                    Success = false,
                    Error = "Missing or invalid product_id parameter"
                };
            }

            var productId = productIdProp.GetString() ?? "";
            var context = ExtractValidationContext(request.Params);

            var result = _extension.ValidateProduct(productId, context);

            return new ExtensionResponse
            {
                Id = request.Id,
                Success = result.IsValid,
                Error = result.IsValid ? null : result.ErrorMessage,
                Data = result.IsValid ? new Dictionary<string, object>
                {
                    ["product_info"] = result.ProductInfo!,
                    ["effective_price_cents"] = result.EffectivePriceCents
                } : null
            };
        }

        private async Task<ExtensionResponse> HandleSearchProductsAsync(ExtensionRequest request)
        {
            var searchTerm = "";
            var maxResults = 50;

            if (request.Params?.TryGetValue("search_term", out var searchTermObj) == true &&
                searchTermObj is JsonElement searchTermElement)
            {
                searchTerm = searchTermElement.GetString() ?? "";
            }

            if (request.Params?.TryGetValue("max_results", out var maxResultsObj) == true &&
                maxResultsObj is JsonElement maxResultsElement)
            {
                maxResults = maxResultsElement.GetInt32();
            }

            // Simple search implementation - in real system would use proper search criteria
            var context = ExtractValidationContext(request.Params);
            var allProducts = new List<ProductValidationResult>();

            // Search by validating each product that matches the term
            // This is inefficient but works for demo purposes
            using var connection = new SqliteConnection($"Data Source={_config.DatabasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT p.sku FROM products p 
                WHERE p.is_active = 1 
                  AND (p.name LIKE @searchTerm OR p.description LIKE @searchTerm OR p.sku LIKE @searchTerm)
                ORDER BY p.popularity_rank
                LIMIT @maxResults";
            command.Parameters.AddWithValue("@searchTerm", $"%{searchTerm}%");
            command.Parameters.AddWithValue("@maxResults", maxResults);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var sku = reader.GetString("sku");
                var result = _extension.ValidateProduct(sku, context);
                if (result.IsValid)
                {
                    allProducts.Add(result);
                }
            }

            return new ExtensionResponse
            {
                Id = request.Id,
                Success = true,
                Data = new Dictionary<string, object>
                {
                    ["products"] = allProducts.Select(r => new
                    {
                        product_info = r.ProductInfo,
                        effective_price_cents = r.EffectivePriceCents
                    }).ToList()
                }
            };
        }

        private async Task<ExtensionResponse> HandleGetPopularItemsAsync(ExtensionRequest request)
        {
            var context = ExtractValidationContext(request.Params);
            var popularProducts = new List<ProductValidationResult>();

            using var connection = new SqliteConnection($"Data Source={_config.DatabasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT p.sku FROM products p 
                WHERE p.is_active = 1 
                ORDER BY p.popularity_rank
                LIMIT 10";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var sku = reader.GetString("sku");
                var result = _extension.ValidateProduct(sku, context);
                if (result.IsValid)
                {
                    popularProducts.Add(result);
                }
            }

            return new ExtensionResponse
            {
                Id = request.Id,
                Success = true,
                Data = new Dictionary<string, object>
                {
                    ["popular_items"] = popularProducts.Select(r => new
                    {
                        product_info = r.ProductInfo,
                        effective_price_cents = r.EffectivePriceCents
                    }).ToList()
                }
            };
        }

        private async Task<ExtensionResponse> HandleGetCategoryProductsAsync(ExtensionRequest request)
        {
            if (request.Params?.TryGetValue("category", out var categoryObj) != true ||
                categoryObj is not JsonElement categoryElement)
            {
                return new ExtensionResponse
                {
                    Id = request.Id,
                    Success = false,
                    Error = "Missing or invalid category parameter"
                };
            }

            var category = categoryElement.GetString() ?? "";
            var context = ExtractValidationContext(request.Params);
            var categoryProducts = new List<ProductValidationResult>();

            using var connection = new SqliteConnection($"Data Source={_config.DatabasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT p.sku FROM products p 
                WHERE p.is_active = 1 AND p.category_id = @category
                ORDER BY p.popularity_rank";
            command.Parameters.AddWithValue("@category", category);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var sku = reader.GetString("sku");
                var result = _extension.ValidateProduct(sku, context);
                if (result.IsValid)
                {
                    categoryProducts.Add(result);
                }
            }

            return new ExtensionResponse
            {
                Id = request.Id,
                Success = true,
                Data = new Dictionary<string, object>
                {
                    ["products"] = categoryProducts.Select(r => new
                    {
                        product_info = r.ProductInfo,
                        effective_price_cents = r.EffectivePriceCents
                    }).ToList()
                }
            };
        }

        private ValidationContext ExtractValidationContext(Dictionary<string, JsonElement>? parameters)
        {
            var context = new ValidationContext
            {
                StoreId = "STORE_COFFEE_001", // Default
                TerminalId = "TERMINAL_01",   // Default  
                OperatorId = "SYSTEM",        // Default
                RequestTime = DateTime.Now
            };

            if (parameters == null) return context;

            if (parameters.TryGetValue("store_id", out var storeIdElement))
                context.StoreId = storeIdElement.GetString() ?? context.StoreId;

            if (parameters.TryGetValue("terminal_id", out var terminalIdElement))
                context.TerminalId = terminalIdElement.GetString() ?? context.TerminalId;

            if (parameters.TryGetValue("operator_id", out var operatorIdElement))
                context.OperatorId = operatorIdElement.GetString() ?? context.OperatorId;

            return context;
        }

        private async Task SendErrorResponseAsync(StreamWriter writer, string error)
        {
            var response = new ExtensionResponse
            {
                Id = "unknown",
                Success = false,
                Error = error
            };
            var responseJson = JsonSerializer.Serialize(response);
            await writer.WriteLineAsync(responseJson);
        }
    }

    /// <summary>
    /// JSON-RPC style request for extension operations.
    /// </summary>
    public class ExtensionRequest
    {
        public string Id { get; set; } = "";
        public string Method { get; set; } = "";
        public Dictionary<string, JsonElement>? Params { get; set; }
    }

    /// <summary>
    /// JSON-RPC style response from extension operations.
    /// </summary>
    public class ExtensionResponse
    {
        public string Id { get; set; } = "";
        public bool Success { get; set; }
        public string? Error { get; set; }
        public Dictionary<string, object>? Data { get; set; }
    }
