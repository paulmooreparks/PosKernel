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
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PosKernel.Extensions.Restaurant;

namespace PosKernel.Extensions.Restaurant.Client
{
    /// <summary>
    /// Client for communicating with the restaurant domain extension service.
    /// Provides a clean interface for AI demo and other clients to access
    /// restaurant-specific product catalog functionality via IPC.
    /// </summary>
    public class RestaurantExtensionClient : IRestaurantExtension, IDisposable
    {
        private readonly string _pipeName;
        private readonly ILogger<RestaurantExtensionClient> _logger;
        private readonly object _lock = new();
        private NamedPipeClientStream? _client;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private int _requestId = 0;

        /// <summary>
        /// Initializes a new instance of the RestaurantExtensionClient.
        /// </summary>
        /// <param name="logger">Logger for debugging and diagnostics.</param>
        /// <param name="pipeName">Named pipe name for IPC communication.</param>
        public RestaurantExtensionClient(ILogger<RestaurantExtensionClient> logger, string pipeName = "poskernel-restaurant-extension")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _pipeName = pipeName;
        }

        /// <summary>
        /// Ensures connection to the restaurant extension service.
        /// </summary>
        private async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_client?.IsConnected == true)
                {
                    return;
                }

                // Dispose existing connection if any
                _reader?.Dispose();
                _writer?.Dispose();
                _client?.Dispose();

                // Create new connection
                _client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
            }

            try
            {
                _logger.LogInformation("Attempting to connect to restaurant extension service via pipe: {PipeName}", _pipeName);
                
                // NO FALLBACKS - fail fast with clear error message
                await _client.ConnectAsync(15000, cancellationToken);
                _reader = new StreamReader(_client);
                _writer = new StreamWriter(_client) { AutoFlush = true };
                
                _logger.LogInformation("✅ Successfully connected to restaurant extension service");
            }
            catch (TimeoutException ex)
            {
                var errorMessage = $"❌ RESTAURANT EXTENSION SERVICE NOT AVAILABLE: Connection timed out on pipe '{_pipeName}'. " +
                                 $"Ensure 'dotnet run --project PosKernel.Extensions.Restaurant' is running.";
                _logger.LogError(ex, errorMessage);
                throw new InvalidOperationException(errorMessage, ex);
            }
            catch (IOException ex)
            {
                var errorMessage = $"❌ RESTAURANT EXTENSION SERVICE CONNECTION FAILED: IO error on pipe '{_pipeName}'. " +
                                 $"Ensure 'dotnet run --project PosKernel.Extensions.Restaurant' is running.";
                _logger.LogError(ex, errorMessage);
                throw new InvalidOperationException(errorMessage, ex);
            }
            catch (Exception ex)
            {
                var errorMessage = $"❌ RESTAURANT EXTENSION SERVICE UNAVAILABLE: {ex.Message} on pipe '{_pipeName}'. " +
                                 $"Ensure 'dotnet run --project PosKernel.Extensions.Restaurant' is running.";
                _logger.LogError(ex, errorMessage);
                throw new InvalidOperationException(errorMessage, ex);
            }
        }

        /// <inheritdoc/>
        public async Task<ProductValidationResult> ValidateProductAsync(
            string productId, 
            string storeId = "STORE_COFFEE_001",
            string terminalId = "TERMINAL_01",
            string operatorId = "SYSTEM",
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);

            var requestId = Interlocked.Increment(ref _requestId).ToString();
            var request = new ExtensionRequest
            {
                Id = requestId,
                Method = "validate_product",
                Params = new Dictionary<string, JsonElement>
                {
                    ["product_id"] = JsonSerializer.SerializeToElement(new { StringValue = productId }),
                    ["store_id"] = JsonSerializer.SerializeToElement(storeId),
                    ["terminal_id"] = JsonSerializer.SerializeToElement(terminalId),
                    ["operator_id"] = JsonSerializer.SerializeToElement(operatorId)
                }
            };

            var response = await SendRequestAsync(request, cancellationToken);
            
            if (!response.Success)
            {
                return new ProductValidationResult
                {
                    IsValid = false,
                    ErrorMessage = response.Error ?? "Unknown error"
                };
            }

            if (response.Data?.TryGetValue("product_info", out var productInfoObj) == true &&
                response.Data.TryGetValue("effective_price_cents", out var priceObj))
            {
                var productInfo = JsonSerializer.Deserialize<ProductInfo>(JsonSerializer.Serialize(productInfoObj));
                var effectivePrice = Convert.ToInt64(priceObj);

                return new ProductValidationResult
                {
                    IsValid = true,
                    ProductInfo = productInfo!,
                    EffectivePriceCents = effectivePrice
                };
            }

            return new ProductValidationResult
            {
                IsValid = false,
                ErrorMessage = "Invalid response format"
            };
        }

        /// <inheritdoc/>
        public async Task<List<ProductInfo>> SearchProductsAsync(
            string searchTerm, 
            int maxResults = 50,
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);

            var requestId = Interlocked.Increment(ref _requestId).ToString();
            var request = new ExtensionRequest
            {
                Id = requestId,
                Method = "search_products",
                Params = new Dictionary<string, JsonElement>
                {
                    ["search_term"] = JsonSerializer.SerializeToElement(searchTerm),
                    ["max_results"] = JsonSerializer.SerializeToElement(maxResults)
                }
            };

            var response = await SendRequestAsync(request, cancellationToken);
            
            if (!response.Success || response.Data?.TryGetValue("products", out var productsObj) != true)
            {
                _logger.LogWarning("Product search failed: {Error}", response.Error);
                return new List<ProductInfo>();
            }

            try
            {
                var productsJson = JsonSerializer.Serialize(productsObj);
                var productResults = JsonSerializer.Deserialize<List<ProductSearchResult>>(productsJson);
                return productResults?.Select(r => r.ProductInfo).ToList() ?? new List<ProductInfo>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize search results");
                return new List<ProductInfo>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<ProductInfo>> GetPopularItemsAsync(CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);

            var requestId = Interlocked.Increment(ref _requestId).ToString();
            var request = new ExtensionRequest
            {
                Id = requestId,
                Method = "get_popular_items",
                Params = new Dictionary<string, JsonElement>()
            };

            var response = await SendRequestAsync(request, cancellationToken);
            
            if (!response.Success || response.Data?.TryGetValue("popular_items", out var itemsObj) != true)
            {
                _logger.LogWarning("Failed to get popular items: {Error}", response.Error);
                return new List<ProductInfo>();
            }

            try
            {
                var itemsJson = JsonSerializer.Serialize(itemsObj);
                var itemResults = JsonSerializer.Deserialize<List<ProductSearchResult>>(itemsJson);
                return itemResults?.Select(r => r.ProductInfo).ToList() ?? new List<ProductInfo>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize popular items");
                return new List<ProductInfo>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<ProductInfo>> GetCategoryProductsAsync(
            string category, 
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);

            var requestId = Interlocked.Increment(ref _requestId).ToString();
            var request = new ExtensionRequest
            {
                Id = requestId,
                Method = "get_category_products",
                Params = new Dictionary<string, JsonElement>
                {
                    ["category"] = JsonSerializer.SerializeToElement(category)
                }
            };

            var response = await SendRequestAsync(request, cancellationToken);
            
            if (!response.Success || response.Data?.TryGetValue("products", out var productsObj) != true)
            {
                _logger.LogWarning("Failed to get category products: {Error}", response.Error);
                return new List<ProductInfo>();
            }

            try
            {
                var productsJson = JsonSerializer.Serialize(productsObj);
                var productResults = JsonSerializer.Deserialize<List<ProductSearchResult>>(productsJson);
                return productResults?.Select(r => r.ProductInfo).ToList() ?? new List<ProductInfo>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize category products");
                return new List<ProductInfo>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);

            var requestId = Interlocked.Increment(ref _requestId).ToString();
            var request = new ExtensionRequest
            {
                Id = requestId,
                Method = "get_categories",
                Params = new Dictionary<string, JsonElement>()
            };

            var response = await SendRequestAsync(request, cancellationToken);
            
            if (!response.Success || response.Data?.TryGetValue("categories", out var categoriesObj) != true)
            {
                _logger.LogWarning("Failed to get categories: {Error}", response.Error);
                return new List<string>();
            }

            try
            {
                var categoriesJson = JsonSerializer.Serialize(categoriesObj);
                var categories = JsonSerializer.Deserialize<List<string>>(categoriesJson);
                return categories ?? new List<string>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize categories");
                return new List<string>();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureConnectedAsync(cancellationToken);
                return _client?.IsConnected == true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<ExtensionResponse> SendRequestAsync(ExtensionRequest request, CancellationToken cancellationToken)
        {
            if (_writer == null || _reader == null)
            {
                throw new InvalidOperationException("Not connected to restaurant extension service");
            }

            try
            {
                var requestJson = JsonSerializer.Serialize(request);
                _logger.LogDebug("Sending request: {Request}", requestJson);

                await _writer.WriteLineAsync(requestJson.AsMemory(), cancellationToken);

                // ADD TIMEOUT: Don't wait forever for a response
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(10)); // 10 second timeout
                
                var responseJson = await _reader.ReadLineAsync(timeoutCts.Token);
                if (responseJson == null)
                {
                    throw new InvalidOperationException("Connection closed by restaurant extension service");
                }

                _logger.LogDebug("Received response: {Response}", responseJson);

                var response = JsonSerializer.Deserialize<ExtensionResponse>(responseJson);
                return response ?? new ExtensionResponse 
                { 
                    Id = request.Id, 
                    Success = false, 
                    Error = "Invalid response format" 
                };
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
            {
                _logger.LogError("Request timed out waiting for restaurant extension service response");
                // Force reconnection on next call
                lock (_lock)
                {
                    _client?.Dispose();
                    _client = null;
                }
                throw new InvalidOperationException("Request timed out waiting for restaurant extension service response", ex);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Communication error with restaurant extension service");
                // Force reconnection on next call
                lock (_lock)
                {
                    _client?.Dispose();
                    _client = null;
                }
                throw new InvalidOperationException("Communication error with restaurant extension service", ex);
            }
        }

        /// <summary>
        /// Disconnects from the restaurant extension service and cleans up resources.
        /// </summary>
        /// <returns>A task representing the disconnect operation.</returns>
        public async Task DisconnectAsync()
        {
            try
            {
                if (_client?.IsConnected == true)
                {
                    // Create a simple disconnect message
                    var disconnectMessage = new
                    {
                        id = Guid.NewGuid().ToString(),
                        jsonrpc = "2.0",
                        method = "disconnect",
                        @params = new { }
                    };

                    // Send disconnect message using the writer if available
                    if (_writer != null)
                    {
                        await _writer.WriteLineAsync(JsonSerializer.Serialize(disconnectMessage));
                    }
                }

                // Clean up resources
                _writer?.Dispose();
                _reader?.Dispose();
                _client?.Close();
                
                _writer = null;
                _reader = null;
                _client = null;
                
                _logger?.LogInformation("Disconnected from restaurant extension service");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during disconnect");
            }
        }

        /// <summary>
        /// Disposes all resources used by the RestaurantExtensionClient.
        /// </summary>
        public void Dispose()
        {
            try
            {
                _reader?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Error disposing reader");
            }
            
            try
            {
                _writer?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Error disposing writer");
            }
            
            try
            {
                _client?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Error disposing client");
            }
        }
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
}
