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

namespace PosKernel.Extensions.Restaurant.Client
{
    /// <summary>
    /// Client for communicating with the restaurant domain extension service.
    /// Provides a clean interface for AI demo and other clients to access
    /// restaurant-specific product catalog functionality.
    /// </summary>
    public class RestaurantExtensionClient : IDisposable
    {
        private readonly string _pipeName;
        private readonly ILogger<RestaurantExtensionClient> _logger;
        private readonly object _lock = new();
        private NamedPipeClientStream? _client;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private int _requestId = 0;

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
                await _client.ConnectAsync(5000, cancellationToken);
                _reader = new StreamReader(_client);
                _writer = new StreamWriter(_client) { AutoFlush = true };
                
                _logger.LogDebug("Connected to restaurant extension service");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to restaurant extension service");
                throw new InvalidOperationException("Unable to connect to restaurant extension service. Ensure the service is running.", ex);
            }
        }

        /// <summary>
        /// Validates a product and returns detailed information including allergens and customizations.
        /// </summary>
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

        /// <summary>
        /// Searches for products matching the given search term.
        /// </summary>
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

        /// <summary>
        /// Gets the most popular products for recommendations and upselling.
        /// </summary>
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

        /// <summary>
        /// Gets all products in a specific category.
        /// </summary>
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

                var responseJson = await _reader.ReadLineAsync(cancellationToken);
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

        public void Dispose()
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _client?.Dispose();
        }
    }

    /// <summary>
    /// Result from product search operations.
    /// </summary>
    public class ProductSearchResult
    {
        public ProductInfo ProductInfo { get; set; } = new();
        public long EffectivePriceCents { get; set; }
    }

    // Note: ProductInfo, ProductValidationResult, etc. classes are shared from RestaurantExtension.cs
    // In a real implementation, these would be in a shared library
}
