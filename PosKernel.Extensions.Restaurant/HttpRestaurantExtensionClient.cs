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
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PosKernel.Extensions.Restaurant.Client
{
    /// <summary>
    /// HTTP-based client for Restaurant Extension Service.
    /// Cross-platform alternative to named pipes.
    /// </summary>
    public class HttpRestaurantExtensionClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly string _baseUrl;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the HttpRestaurantExtensionClient.
        /// </summary>
        public HttpRestaurantExtensionClient(ILogger logger, string baseUrl = "http://localhost:8081")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _baseUrl = baseUrl;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        /// <summary>
        /// Tests connection to the Restaurant Extension service.
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Testing connection to Restaurant Extension at {BaseUrl}", _baseUrl);
                var response = await _httpClient.GetAsync("/health");
                response.EnsureSuccessStatusCode();

                _logger.LogInformation("✅ Successfully connected to Restaurant Extension via HTTP");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to connect to Restaurant Extension at {BaseUrl}", _baseUrl);
                return false;
            }
        }

        /// <summary>
        /// Searches for products matching the given query.
        /// </summary>
        public async Task<IReadOnlyList<HttpProductInfo>> SearchProductsAsync(string query, int maxResults = 10)
        {
            try
            {
                var url = $"/products/search?q={Uri.EscapeDataString(query)}&limit={maxResults}";
                _logger.LogInformation("Searching products: {Query} (max {MaxResults})", query, maxResults);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var searchResult = JsonSerializer.Deserialize<HttpProductSearchResult>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var products = new List<HttpProductInfo>();
                if (searchResult?.Products != null)
                {
                    foreach (var product in searchResult.Products)
                    {
                        products.Add(new HttpProductInfo
                        {
                            Id = product.Id,
                            Name = product.Name,
                            Price = product.Price,
                            Category = product.Category,
                            Description = product.Description
                        });
                    }
                }

                _logger.LogInformation("Found {Count} products for query '{Query}'", products.Count, query);
                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search products for query '{Query}'", query);
                return Array.Empty<HttpProductInfo>();
            }
        }

        /// <summary>
        /// Gets a specific product by ID.
        /// </summary>
        public async Task<HttpProductInfo?> GetProductByIdAsync(string productId)
        {
            try
            {
                var url = $"/products/{Uri.EscapeDataString(productId)}";
                _logger.LogInformation("Getting product: {ProductId}", productId);

                var response = await _httpClient.GetAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Product not found: {ProductId}", productId);
                    return null;
                }

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var product = JsonSerializer.Deserialize<HttpProductInfo>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("Found product: {ProductName} ({Price})", product?.Name, product?.Price);
                return product;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get product by ID '{ProductId}'", productId);
                return null;
            }
        }

        /// <summary>
        /// Disposes the HTTP client.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Product information returned by HTTP API.
    /// </summary>
    public class HttpProductInfo
    {
        /// <summary>
        /// Gets or sets the product ID.
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the product price.
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Gets or sets the base price (catalog price for POS kernel integration).
        /// Maps to Price for proper terminology alignment.
        /// </summary>
        public decimal BasePrice
        {
            get => Price;
            set => Price = value;
        }

        /// <summary>
        /// Gets or sets the product category.
        /// </summary>
        public string Category { get; set; } = "";

        /// <summary>
        /// Gets or sets the product description.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets the product specifications for set products and other attributes.
        /// </summary>
        public Dictionary<string, object> Specifications { get; set; } = new();
    }

    /// <summary>
    /// Product search result from HTTP API.
    /// </summary>
    internal class HttpProductSearchResult
    {
        public string Query { get; set; } = "";
        public int Count { get; set; }
        public List<HttpProductInfo> Products { get; set; } = new();
    }
}
